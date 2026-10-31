using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using CivSim.Sim.Core;
using CivSim.Sim.Ai;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using CivSim.Sim.World;
using Xunit;

namespace CivSim.Tests;

public sealed class RaidResponseTests
{
    private const ulong Seed = 20261019;
    private static readonly EconomyArchetype[] Seats = { EconomyArchetype.Balanced, EconomyArchetype.Balanced,
        EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private sealed class Rig
    {
        public readonly MatchModel Match = MatchModel.Create(Seed, TestPack.Pack, WorldGenerator.Generate(Seed), Seats);
        public readonly SimRunner Runner;
        public Rig() => Runner = new SimRunner(Match);
        public PoliticsSystem Politics => Match.Politics;
        public void Grant(int civ, params string[] ids)
        {
            foreach (string id in ids)
                Runner.Submit(new Command(Runner.Tick, (byte)civ, CommandKind.CompleteProject, TestPack.Catalog.IndexOf(id), 0, 0));
            Runner.Advance(1);
        }
        public int SettlementOf(int civ)
        {
            for (int i = 0; i < Match.Economy.SettlementCount; i++)
                if (Match.Economy.SettlementLite(i).Civilization == civ && !Politics.IsFreeFolk(i)) return i;
            throw new System.InvalidOperationException();
        }
    }

    private static Fix64 ToolValue(LivingEconomyModel economy, int settlement)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags allFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object states = typeof(LivingEconomyModel).GetField("_settlements", flags)!.GetValue(economy)!;
        object state = ((IList)states)[settlement]!;
        Fix64[] stores = (Fix64[])state.GetType().GetField("Stores", allFlags)!.GetValue(state)!;
        Type goodType = typeof(LivingEconomyModel).GetNestedType("Good", flags)!;
        MethodInfo durability = typeof(LivingEconomyModel).GetMethod("ToolDurability", flags)!;
        Fix64 total = Fix64.Zero;
        for (int i = 0; i < stores.Length; i++)
            if ((Fix64)durability.Invoke(economy, new[] { state, Enum.ToObject(goodType, i) })! > Fix64.Zero)
                total += stores[i] * Fix64.FromInt(3);
        return total;
    }

    [Fact]
    public void Command_validates_ownership_and_activates_at_next_season_with_snapshot_replay()
    {
        var rig = new Rig();
        int own = rig.SettlementOf(0), foreign = rig.SettlementOf(1);
        rig.Runner.Submit(PlayerCommands.SetRaidResponse(rig.Runner.Tick, 0, own, RaidResponse.Withdraw));
        rig.Runner.Advance(1);
        Assert.Equal(RaidResponse.Stand, rig.Politics.RaidResponseOf(own));
        rig.Runner.Advance(149);
        Assert.Equal(RaidResponse.Withdraw, rig.Politics.RaidResponseOf(own));
        ulong fingerprint = WorldEconomyInput.FromGeneratedWorld(WorldGenerator.Generate(Seed)).Fingerprint;
        MatchReplay replay = MatchReplay.FromRun(Seed, TestPack.Pack, Seats, rig.Runner, fingerprint);
        Assert.True(replay.Verify(TestPack.Pack).Ok);

        rig.Runner.Submit(PlayerCommands.SetRaidResponse(rig.Runner.Tick, 0, foreign, RaidResponse.Withdraw));
        rig.Runner.Advance(1);
        Assert.Contains(rig.Politics.Events, e => e.Kind == PoliticsEventKind.OrderRefused && e.A == (int)CommandKind.SetRaidResponse);

        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, leaveOpen: true)) rig.Match.WriteSnapshot(writer);
        var restored = new Rig();
        bytes.Position = 0;
        using (var reader = new BinaryReader(bytes, System.Text.Encoding.UTF8, leaveOpen: true)) restored.Match.ReadSnapshot(reader);
        Assert.Equal(rig.Match.ComputeChecksum(), restored.Match.ComputeChecksum());
        Assert.Equal(RaidResponse.Withdraw, restored.Politics.RaidResponseOf(own));
    }

    [Fact]
    public void Prepared_raid_warning_is_hashed_and_restored_with_its_expedition()
    {
        var rig = new Rig();
        int own = rig.SettlementOf(0), attacker = rig.SettlementOf(1);
        var expedition = new Expedition
        {
            Kind = ExpeditionKind.Raid, Civ = 1, TargetCiv = 0,
            FromSettlement = attacker, ToSettlement = own, ArriveSeason = 4,
            WarnedTargetCiv = 0, WarnedTargetSettlement = own, WarningSeason = 2,
        };
        var expeditions = (List<Expedition>)typeof(PoliticsSystem).GetField("_expeditions",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(rig.Politics)!;
        expeditions.Add(expedition);

        ulong checksum = rig.Match.ComputeChecksum();
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, leaveOpen: true)) rig.Match.WriteSnapshot(writer);
        var restored = new Rig();
        bytes.Position = 0;
        using (var reader = new BinaryReader(bytes, System.Text.Encoding.UTF8, leaveOpen: true)) restored.Match.ReadSnapshot(reader);

        Expedition restoredRaid = Assert.Single(restored.Politics.Expeditions);
        Assert.Equal(0, restoredRaid.WarnedTargetCiv);
        Assert.Equal(own, restoredRaid.WarnedTargetSettlement);
        Assert.Equal(2, restoredRaid.WarningSeason);
        Assert.Equal(checksum, restored.Match.ComputeChecksum());
    }

    [Fact]
    public void Alerted_withdrawal_is_deterministic_carries_full_party_capacity_and_has_one_burn_roll()
    {
        var attackers = CombatTables.Raiders();
        var defenders = CombatTables.Band(10);
        var defenses = new Defenses(false, false, true, true, false, true, false, false, false, true);
        Assert.Equal(CivSim.Sim.Numerics.Fix64.Zero, Raids.SurpriseChance(defenses));

        RaidOutcome a = Raids.Resolve(attackers, defenders, defenses, false, 1234, 5678, withdrawIfAlerted: true);
        RaidOutcome b = Raids.Resolve(attackers, defenders, defenses, false, 1234, 5678, withdrawIfAlerted: true);
        Assert.True(a.Withdrew);
        Assert.True(a.Success);
        Assert.False(a.Surprised);
        Assert.Empty(a.Battle.AttackerDead);
        Assert.Empty(a.Battle.AttackerWounded);
        Assert.Empty(a.Battle.DefenderDead);
        Assert.Empty(a.Battle.DefenderWounded);
        Assert.Equal(Raids.CarryPerRaider * attackers.Count * defenses.LootMultiplier(), a.LootCapacity);
        Assert.InRange(a.StructuresBurned, 0, 1);
        Assert.Equal(a.StructuresBurned, b.StructuresBurned);
        Assert.Equal(a.LootCapacity, b.LootCapacity);

        RaidOutcome surprise = Raids.Resolve(attackers, defenders, default, false, 1234, 5678, withdrawIfAlerted: true);
        Assert.False(surprise.Withdrew);
        Assert.True(surprise.Surprised);

        RaidOutcome prepared = Raids.Resolve(attackers, defenders, default, false, 1234, 5678,
            withdrawIfAlerted: true, preparedWithdrawal: true);
        Assert.True(prepared.Withdrew);
        Assert.False(prepared.Surprised);
        Assert.True(prepared.SurpriseRoll);
        Assert.Empty(prepared.Battle.DefenderDead);
        Assert.Empty(prepared.Battle.DefenderWounded);
    }

    [Fact]
    public void Default_stand_uses_the_unchanged_combat_resolution()
    {
        var attackers = CombatTables.Raiders();
        var defenders = CombatTables.Band(10);
        RaidOutcome ordinary = Raids.Resolve(attackers, defenders, default, false, 991, 77);
        RaidOutcome stand = Raids.Resolve(attackers, defenders, default, false, 991, 77, withdrawIfAlerted: false);
        Assert.Equal(ordinary.Success, stand.Success);
        Assert.Equal(ordinary.Surprised, stand.Surprised);
        Assert.Equal(ordinary.Battle.Rounds, stand.Battle.Rounds);
        Assert.Equal(ordinary.Battle.AttackerDead, stand.Battle.AttackerDead);
        Assert.Equal(ordinary.Battle.DefenderDead, stand.Battle.DefenderDead);
        Assert.Equal(ordinary.StructuresBurned, stand.StructuresBurned);
    }

    [Fact]
    public void Arriving_alerted_withdrawal_deducts_real_loot_delivers_eighty_percent_and_keeps_people_home()
    {
        var rig = new Rig();
        rig.Runner.Advance(24 * 600);
        rig.Grant(0, "HNT-02", "HNT-03", "WAR-01", "WAR-02");
        rig.Grant(1, "HUS-01", "DEF-02", "COM-04", "COM-01");
        rig.Politics.ForceContact(0, 1);
        rig.Politics.Diplomacy.Set(0, 1, 20);
        int home = rig.SettlementOf(0), target = rig.SettlementOf(1);
        rig.Politics.Diplomacy.RecordRaid(0, 1, rig.Politics.Season); // recent observation plus alarm defenses guarantees no surprise
        Assert.Equal(CivSim.Sim.Numerics.Fix64.Zero, Raids.SurpriseChance(
            new Defenses(false, false, true, true, true, true, false, false, false, true)));
        Assert.True(rig.Politics.OrderRaidResponse(1, target, RaidResponse.Withdraw));
        rig.Runner.Advance(150);
        Assert.Equal(RaidResponse.Withdraw, rig.Politics.RaidResponseOf(target));
        Assert.True(rig.Politics.OrderRaid(0, target, 4));
        Expedition raid = rig.Politics.Expeditions.Last(e => e.Civ == 0 && e.Kind == ExpeditionKind.Raid && e.ToSettlement == target);
        raid.ArriveSeason = rig.Politics.Season;

        Fix64 targetStoresBefore = rig.Match.Economy.FoodStores(target);
        Fix64 targetToolsBefore = ToolValue(rig.Match.Economy, target);
        Fix64 homeStoresBefore = rig.Match.Economy.FoodStores(home);
        Fix64 targetCohesionBefore = rig.Match.Economy.SettlementCohesion(target);
        Fix64 homeCohesionBefore = rig.Match.Economy.SettlementCohesion(home);
        int populationBefore = rig.Match.Economy.Population(target);
        int figuresBefore = rig.Match.Economy.FigureCount;
        int relationsBefore = rig.Politics.Diplomacy.Relations(0, 1);

        MethodInfo arrive = typeof(PoliticsSystem).GetMethod("ArriveRaid", BindingFlags.Instance | BindingFlags.NonPublic)!;
        arrive.Invoke(rig.Politics, new object[] { raid });

        PoliticsEvent resolved = rig.Politics.Events.Last(e => e.Kind == PoliticsEventKind.RaidResolved && e.Civ == 0);
        Fix64 loot = targetStoresBefore - rig.Match.Economy.FoodStores(target);
        loot += targetToolsBefore - ToolValue(rig.Match.Economy, target);
        Assert.Equal(1, resolved.A);
        Assert.Equal(loot.FloorToInt(), resolved.B);
        Assert.Equal(populationBefore, rig.Match.Economy.Population(target));
        Assert.Equal(figuresBefore, rig.Match.Economy.FigureCount);
        Assert.Equal(0, (resolved.Fight >> 16) & 255);
        Assert.Equal(0, (resolved.Fight >> 24) & 127);
        Assert.True(Fix64.Abs(loot * Raids.DeliveredLootShare -
            (rig.Match.Economy.FoodStores(home) - homeStoresBefore)) <= Fix64.FromRaw(2));
        int burialRites = rig.Match.Research.Catalog.IndexOf("BEL-01");
        Fix64 raidCohesionCost = rig.Match.Research.IsDone(1, burialRites)
            ? Fix64.FromRatio(5, 100) : Fix64.FromRatio(10, 100);
        Assert.Equal(targetCohesionBefore - raidCohesionCost, rig.Match.Economy.SettlementCohesion(target));
        Assert.Equal(homeCohesionBefore + Fix64.FromRatio(5, 100), rig.Match.Economy.SettlementCohesion(home));
        Assert.Equal(relationsBefore - 30, rig.Politics.Diplomacy.Relations(0, 1));
        Assert.Equal(rig.Politics.Season, rig.Politics.Diplomacy.LastRaidBy(0, 1));
    }

    [Fact]
    public void Warlord_difficulty_with_balanced_archetype_withdraws_after_weak_raid_evidence_but_chieftain_does_not()
    {
        CivControl[] controls =
        {
            CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced),
            CivControl.Ai(Difficulty.Chieftain, EconomyArchetype.Balanced),
            CivControl.Council,
            CivControl.Council,
        };
        var match = MatchModel.Create(Seed, TestPack.Pack, WorldGenerator.Generate(Seed), Seats, controls,
            warlordRaidWithdrawalPolicy: false);
        var runner = new SimRunner(match);
        AiSeat warlord = match.Ai!.SeatOf(0)!;
        AiSeat chieftain = match.Ai.SeatOf(1)!;
        int warlordSettlement = warlord.Own.Settlements.Count == 0 ? 0 : warlord.Own.Settlements[0].Index;
        int chieftainSettlement = 1;
        for (int i = 0; i < match.Politics.SettlementTotal; i++)
        {
            if (match.Politics.SettlementCivOf(i) == 0) warlordSettlement = i;
            if (match.Politics.SettlementCivOf(i) == 1) chieftainSettlement = i;
        }
        warlord.Own.Settlements.Clear();
        chieftain.Own.Settlements.Clear();
        warlord.Own.Settlements.Add(new OwnSettlement
        {
            Index = warlordSettlement, LastRaidObservedSeason = 0, LastRaidObservedAttackers = 4, LastRaidObservedByCiv = 1,
        });
        chieftain.Own.Settlements.Add(new OwnSettlement
        {
            Index = chieftainSettlement, LastRaidObservedSeason = 0, LastRaidObservedAttackers = 4, LastRaidObservedByCiv = 0,
        });
        warlord.Belief.Rivals[1].WeaponStrength = Fix64.FromRatio(6, 10);
        chieftain.Belief.Rivals[0].WeaponStrength = Fix64.FromRatio(6, 10);

        RaidWithdrawalPolicy.Decide(warlord, match.Politics, Seed, 0);
        RaidWithdrawalPolicy.Decide(chieftain, match.Politics, Seed, 0);
        runner.Advance(150);

        Assert.Equal(RaidResponse.Withdraw, match.Politics.RaidResponseOf(warlordSettlement));
        Assert.Equal(RaidResponse.Stand, match.Politics.RaidResponseOf(chieftainSettlement));
    }

    [Fact]
    public void Visible_next_season_raid_triggers_forecast_without_prior_raid_but_other_incoming_parties_do_not()
    {
        Assert.Equal(RaidResponse.Withdraw, ResponseForVisibleIncoming(ExpeditionKind.Raid, arrivalSeason: 1));
        Assert.Equal(RaidResponse.Stand, ResponseForVisibleIncoming(ExpeditionKind.Assault, arrivalSeason: 1));
        Assert.Equal(RaidResponse.Stand, ResponseForVisibleIncoming(ExpeditionKind.Raid, arrivalSeason: 2));
    }

    [Fact]
    public void Visible_raids_to_same_settlement_combine_risk_across_parties()
    {
        IncomingParty[] parties =
        {
            Incoming(1, ExpeditionKind.Raid, targetSettlement: 0, partySize: 2, arrivalSeason: 1, fromSettlement: 10),
            Incoming(2, ExpeditionKind.Raid, targetSettlement: 0, partySize: 2, arrivalSeason: 1, fromSettlement: 20),
            Incoming(3, ExpeditionKind.Raid, targetSettlement: 0, partySize: 2, arrivalSeason: 1, fromSettlement: 30),
        };

        foreach (IncomingParty party in parties)
            Assert.Equal(RaidResponse.Stand, ResponseForIncoming(party));
        Assert.Equal(RaidResponse.Withdraw, ResponseForIncoming(parties));
    }

    [Fact]
    public void Visible_raids_are_not_combined_across_targets_arrivals_or_party_kinds()
    {
        IncomingParty valid = Incoming(1, ExpeditionKind.Raid, targetSettlement: 0, partySize: 2,
            arrivalSeason: 1, fromSettlement: 10);
        Assert.Equal(RaidResponse.Stand, ResponseForIncoming(valid,
            Incoming(2, ExpeditionKind.Raid, targetSettlement: -2, partySize: 2, arrivalSeason: 1, fromSettlement: 20)));
        Assert.Equal(RaidResponse.Stand, ResponseForIncoming(valid,
            Incoming(2, ExpeditionKind.Raid, targetSettlement: 0, partySize: 2, arrivalSeason: 2, fromSettlement: 20)));
        Assert.Equal(RaidResponse.Stand, ResponseForIncoming(valid,
            Incoming(2, ExpeditionKind.Assault, targetSettlement: 0, partySize: 2, arrivalSeason: 1, fromSettlement: 20)));
    }

    [Fact]
    public void Historical_evidence_keeps_priority_over_a_weaker_visible_incoming_raid()
    {
        IncomingParty weakIncoming = Incoming(1, ExpeditionKind.Raid, targetSettlement: 0,
            partySize: 2, arrivalSeason: 1, fromSettlement: 10);

        Assert.Equal(RaidResponse.Withdraw, ResponseForIncomingWithHistorical(20));
        Assert.Equal(RaidResponse.Withdraw, ResponseForIncomingWithHistorical(20, weakIncoming));
    }

    [Fact]
    public void Failed_incoming_raid_removes_dead_and_wounded_defenders_before_the_next_party()
    {
        var defenders = new List<Fighter>
        {
            new(101, Fix64.One), new(102, Fix64.One), new(103, Fix64.One), new(104, Fix64.One),
        };
        var firstPartyBattle = new BattleResult { Winner = BattleWinner.Defender };
        firstPartyBattle.DefenderDead.Add(102);
        firstPartyBattle.DefenderWounded.Add(104);

        RaidWithdrawalPolicy.RemoveDefenderCasualties(defenders, firstPartyBattle);

        Assert.Equal(new long[] { 101, 103 }, defenders.Select(defender => defender.Id));
    }

    [Fact]
    public void Later_incoming_parties_use_the_recently_raided_surprise_defense()
    {
        Defenses original = new(ThornBarrier: false, Palisade: false, Watchers: true, Dogs: true,
            SignalFires: false, AlarmCalls: false, HillSite: false, Archery: false, Night: false,
            RaidedRecently: false);

        Assert.Equal(original, RaidWithdrawalPolicy.DefensesForIncomingParty(original, partyIndex: 0));
        Assert.Equal(original with { RaidedRecently = true },
            RaidWithdrawalPolicy.DefensesForIncomingParty(original, partyIndex: 1));
    }

    [Theory]
    [InlineData(4, false, false)]
    [InlineData(5, false, true)]
    [InlineData(2, true, false)]
    [InlineData(3, true, false)]
    [InlineData(4, true, true)]
    [InlineData(5, true, true)]
    public void Withdrawal_threshold_is_more_responsive_only_for_visible_incoming_raids(
        int attackerWins, bool incoming, bool expected)
    {
        Assert.Equal(expected, RaidWithdrawalPolicy.ShouldWithdraw(attackerWins, incoming));
    }

    [Theory]
    [InlineData(12, 4, true)]
    [InlineData(13, 4, false)]
    public void Historical_raid_evidence_is_fresh_through_age_eight_only(
        int season, int observedSeason, bool expected)
    {
        Assert.Equal(expected, RaidWithdrawalPolicy.HasRecentEvidence(
            season, observedSeason, attackers: 4, byCiv: 1, rivalCount: 4));
    }

    [Fact]
    public void Raid_withdrawal_diagnostic_formats_decisions_and_arrivals()
    {
        Assert.Equal("RAID_WITHDRAWAL_DECISION season=12 settlement=s3 evidence=group incoming=2 partySize=8 " +
            "civs=c1,c2 forecast=3/8 responseBefore=Stand responseDesired=Withdraw effectiveSeason=13",
            RaidWithdrawalPolicy.FormatDecisionTrace(12, 3, "group", 2, 8, "c1,c2", 3,
                RaidResponse.Stand, RaidResponse.Withdraw, 13));
        var shadow = new RaidOutcome { Success = false, LootCapacity = Fix64.FromRatio(19, 2) };
        shadow.Battle.DefenderDead.Add(101);
        shadow.Battle.DefenderWounded.Add(102);
        Assert.Equal("RAID_WITHDRAWAL_ARRIVAL season=13 target=s3 attacker=c2 partySize=6 effectiveResponse=Withdraw " +
            "surprise=no surpriseRoll=no success=yes withdrawn=yes loot=12 defenderDead=1 defenderWounded=2 preparedWarning=no " +
            "shadowStandSuccess=no shadowStandDefenderDead=1 " +
            "shadowStandDefenderWounded=1 shadowStandLootCapacity=9.50",
            RaidWithdrawalPolicy.FormatArrivalTrace(13, 3, 2, 6, RaidResponse.Withdraw,
                surprised: false, success: true, withdrawn: true, loot: 12, defenderDead: 1,
                defenderWounded: 2, shadowStand: shadow));
        Assert.False(RaidWithdrawalPolicy.ShouldTraceDecision(enabled: true, recentEvidence: false,
            incomingCount: 0, before: RaidResponse.Stand, desired: RaidResponse.Stand));
        Assert.True(RaidWithdrawalPolicy.ShouldTraceDecision(enabled: true, recentEvidence: false,
            incomingCount: 1, before: RaidResponse.Stand, desired: RaidResponse.Stand));
        Assert.True(RaidWithdrawalPolicy.ShouldTraceDecision(enabled: true, recentEvidence: false,
            incomingCount: 0, before: RaidResponse.Stand, desired: RaidResponse.Withdraw));
        Assert.False(RaidWithdrawalPolicy.ShouldTraceDecision(enabled: false, recentEvidence: true,
            incomingCount: 1, before: RaidResponse.Stand, desired: RaidResponse.Withdraw));
    }

    private static RaidResponse ResponseForVisibleIncoming(ExpeditionKind kind, int arrivalSeason)
        => ResponseForIncoming(Incoming(1, kind, targetSettlement: 0, partySize: 4,
            arrivalSeason, fromSettlement: 10));

    private static IncomingParty Incoming(int civ, ExpeditionKind kind, int targetSettlement,
        int partySize, int arrivalSeason, int fromSettlement) => new()
    {
        Civ = civ,
        Kind = kind,
        FromSettlement = fromSettlement,
        TargetSettlement = targetSettlement,
        PartySize = partySize,
        ArriveSeason = arrivalSeason,
    };

    private static RaidResponse ResponseForIncoming(params IncomingParty[] incomingParties)
        => ResponseForIncomingWithHistorical(0, incomingParties);

    private static RaidResponse ResponseForIncomingWithHistorical(int historicalAttackers,
        params IncomingParty[] incomingParties)
    {
        CivControl[] controls =
        {
            CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced),
            CivControl.Council,
            CivControl.Council,
            CivControl.Council,
        };
        var match = MatchModel.Create(Seed, TestPack.Pack, WorldGenerator.Generate(Seed), Seats, controls,
            warlordRaidWithdrawalPolicy: false);
        var runner = new SimRunner(match);
        AiSeat warlord = match.Ai!.SeatOf(0)!;
        int settlement = Enumerable.Range(0, match.Politics.SettlementTotal)
            .First(i => match.Politics.SettlementCivOf(i) == 0);
        var ownSettlement = new OwnSettlement { Index = settlement };
        for (int i = 0; i < 8; i++) ownSettlement.Defenders.Add(new Fighter(100 + i, Fix64.FromRatio(6, 10)));
        if (historicalAttackers > 0)
        {
            ownSettlement.LastRaidObservedSeason = 0;
            ownSettlement.LastRaidObservedAttackers = historicalAttackers;
            ownSettlement.LastRaidObservedByCiv = 1;
        }
        warlord.Own.Settlements.Clear();
        warlord.Own.Settlements.Add(ownSettlement);
        foreach (IncomingParty incoming in incomingParties)
        {
            if (incoming.TargetSettlement == 0) incoming.TargetSettlement = settlement;
            else if (incoming.TargetSettlement == -2)
                incoming.TargetSettlement = Enumerable.Range(0, match.Politics.SettlementTotal)
                    .First(i => i != settlement);
            warlord.Belief.Rivals[incoming.Civ].WeaponStrength = Fix64.FromRatio(6, 10);
            warlord.Belief.IncomingParties.Add(incoming);
        }

        RaidWithdrawalPolicy.Decide(warlord, match.Politics, Seed, season: 0);
        Assert.Equal(RaidResponse.Stand, match.Politics.RaidResponseOf(settlement));
        runner.Advance(150);
        return match.Politics.RaidResponseOf(settlement);
    }
}
