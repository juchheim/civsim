using System;
using System.Linq;
using CivSim.Harness;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>
/// The Strategist in real matches (docs/MVP/14 §6, §7, §8.4): Strategist AIs on civilizations 0 and 3, the Council on
/// 1 and 2, omniscient beliefs from <see cref="PerfectBeliefBuilder"/>. One shared run to minute 26 (contact comes near
/// minute 20); each scenario resumes from its snapshot and stages the situation with the test hooks.
/// </summary>
public class StrategistScenarioTests
{
    private const ulong Seed = 20261019;
    private const int BaseSeason = 104; // minute 26
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly CivControl Smart = CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced);
    private static readonly CivControl[] Controls = { Smart, CivControl.Council, CivControl.Council, Smart };

    private sealed class Base
    {
        public readonly StrategistRig Rig;
        public readonly byte[] Snapshot;
        public readonly ulong Checksum;
        public Base()
        {
            Rig = new StrategistRig(Seed, World.Value, Controls);
            Rig.Seasons(BaseSeason);
            Snapshot = Rig.Snapshot();
            Checksum = Rig.Match.ComputeChecksum();
        }
    }

    private static readonly Lazy<Base> Shared = new(() => new Base());

    private static StrategistRig Resume() => new(Seed, World.Value, Controls, snapshot: Shared.Value.Snapshot);

    private static void WaitOutWarTruce(StrategistRig rig)
    {
        Diplomacy diplomacy = rig.Politics.Diplomacy;
        diplomacy.Set(0, 1, 0);
        if (diplomacy.AtWar(0, 1)) rig.Politics.SetWar(0, 1, false);
        int waited = 0;
        while (diplomacy.InTruce(0, 1, rig.Politics.Season) && waited <= Diplomacy.TruceSeasons)
        {
            rig.Seasons(1);
            waited++;
        }
        Assert.False(diplomacy.AtWar(0, 1));
        Assert.False(diplomacy.InTruce(0, 1, rig.Politics.Season));
    }

    private static readonly string[] Bows = { "WAR-01", "HNT-02", "HNT-03", "HNT-04", "HNT-05", "HNT-06", "WAR-02", "WAR-03", "SOC-05", "WAR-04" };

    [Fact]
    public void After_contact_it_gifts_and_barters_and_logs_its_reasons()
    {
        StrategistRig rig = Shared.Value.Rig;
        int contact = rig.Politics.Events.First(e => e.Kind == PoliticsEventKind.Contact && e.Civ == 0).Tick is long t ? (int)((t + 1) / 150) : -1;
        Assert.InRange(contact, 40, 100); // This seed's moving scouts meet at season 43; the independent F1 gate checks 15–25 min on >=90% of 24 maps.
        Assert.Contains(rig.Notes(0, "Gift"), n => n.Subject == 1 && n.Season >= contact);
        Assert.Contains(rig.Politics.Events, e => e.Kind == PoliticsEventKind.Gift && e.Civ == 0 && e.Other == 1);
        Assert.Contains(rig.Notes(0, "Barter"), n => n.Subject == 1);
        // The trip takes seasons each way: it has come home with the goods, or is on the road.
        Assert.True(rig.Politics.Events.Any(e => e.Kind == PoliticsEventKind.Barter && e.Civ == 0) ||
                    rig.Politics.Expeditions.Any(e => e.Kind == ExpeditionKind.Barter && e.Civ == 0), "no barter trip of civ 0");
        Assert.True(rig.Politics.Diplomacy.Relations(0, 1) > 0);
        // Every decision carries its reasons (14 §11), and the plan starts as the frontier race (the E0-E3 default).
        Assert.All(rig.Log.Entries.Where(e => e.Layer == Strategist.Layer), e => Assert.False(string.IsNullOrWhiteSpace(e.Reason)));
        Assert.Contains(rig.Notes(0, "Plan"), n => n.Subject == (int)GrandPlan.FrontierRace);
        Assert.Equal(GrandPlan.FrontierRace, rig.Match.Ai!.SeatOf(0)!.Context.Intent.Plan);
        StrategistAudit.Result audit = StrategistAudit.Run(0, rig.Log.Entries, rig.Politics.Events);
        Assert.Equal((0, 0, 0, 0), (audit.SuicideRaids, audit.UndefendedLosses, audit.WinterAggression, audit.Dithering));
    }

    [Fact]
    public void The_match_is_deterministic_and_resumes_exactly()
    {
        var again = new StrategistRig(Seed, World.Value, Controls);
        again.Seasons(BaseSeason);
        Assert.Equal(Shared.Value.Checksum, again.Match.ComputeChecksum());
        Assert.Equal(Shared.Value.Rig.Log.Entries.Select(e => e.Reason), again.Log.Entries.Select(e => e.Reason));
        // A resumed match follows the straight one (the Strategist's plan, memories and cooldowns are saved).
        StrategistRig resumed = Resume();
        Assert.Equal(Shared.Value.Checksum, resumed.Match.ComputeChecksum());
        again.Seasons(8);
        resumed.Seasons(8);
        Assert.Equal(again.Match.ComputeChecksum(), resumed.Match.ComputeChecksum());
    }

    [Fact]
    public void It_fortifies_when_a_raider_neighbour_appears()
    {
        StrategistRig rig = Resume();
        rig.Grant(0, "GOV-02");
        rig.Grant(1, "WAR-01", "HNT-02", "HNT-03", "HNT-04", "WAR-02", "GOV-01", "SOC-05", "GOV-02");
        Assert.True(rig.Politics.OrderPosture(1, Posture.Raid));
        rig.Politics.Diplomacy.Set(0, 1, -50);
        rig.Seasons(2);
        Assert.Equal(Posture.Fortify, rig.Politics.PostureOf(0));
        Assert.Contains(rig.Notes(0, "Threat"), n => n.Subject == 1);
        Assert.Contains(rig.Notes(0, "Posture"), n => n.Subject == (int)Posture.Fortify);
        Assert.True(rig.Match.Ai!.SeatOf(0)!.Context.Intent.DefenseUrgency >= StrategistTuning.FortifyAt);
    }

    [Fact]
    public void It_builds_physical_defense_before_a_qualifying_believed_bronze_raid()
    {
        var rig = new StrategistRig(Seed, World.Value, Controls);
        const int defender = 0, attacker = 1;
        int target = rig.SettlementsOf(defender)
            .OrderBy(settlement => rig.SettlementsOf(attacker)
                .Min(other => HexGrid.Distance(rig.Politics.SettlementTileOf(settlement),
                    rig.Politics.SettlementTileOf(other), 160)))
            .First();

        rig.Grant(defender, "DEF-01", "DEF-02", "WOD-03", "DEF-03");
        rig.Grant(attacker, "WAR-01", "HNT-02", "HNT-03", "HNT-04", "WAR-02", "GOV-01", "SOC-05", "GOV-02", "MET-04");
        rig.Match.Economy.FixtureStock(target, "G-WOOD", Fix64.FromInt(100));
        rig.Match.Economy.FixtureAddPeople(rig.SettlementsOf(attacker)[0], 40, 25);
        rig.Politics.Diplomacy.MarkContacted(defender, attacker);
        rig.Politics.Diplomacy.Set(defender, attacker, -60);
        rig.Politics.SetWar(attacker, defender, true);
        // The decision-level test covers the Strategist dispatch; this scenario follows that exact order
        // through the live economy and into the B28 launch-time telemetry.
        Assert.True(rig.Politics.OrderPrioritizePhysicalDefenseBuild(defender, target));

        bool defendedBeforeRaid = false, supportedThreatObserved = false;
        for (int season = 0; season < 24; season++)
        {
            rig.Seasons(1);
            RivalBelief currentBelief = rig.Match.Ai!.SeatOf(defender)!.Belief.Rivals[attacker];
            supportedThreatObserved |= currentBelief.AtWar || currentBelief.ThreatProbability >= Fix64.FromRatio(1, 10);
            Defenses defenses = rig.Politics.MilitaryOf(target).Defenses;
            if (defenses.Palisade || defenses.Hillfort || defenses.StoneWalls)
            {
                defendedBeforeRaid = true;
                break;
            }
        }

        RivalBelief belief = rig.Match.Ai!.SeatOf(defender)!.Belief.Rivals[attacker];
        (int buildKind, Fix64 buildShare) = rig.Match.Economy.SettlementBuild(target);
        int defenderTile = rig.Politics.SettlementTileOf(target);
        int attackerTile = rig.Politics.SettlementTileOf(rig.SettlementsOf(attacker)[0]);
        string defenseState = $"target={target} built={defendedBeforeRaid} believed MET04={belief.ProjectBelieved[TestPack.Catalog.IndexOf("MET-04")]} " +
            $"threat={belief.ThreatProbability} atWar={belief.AtWar} lastRaid={belief.LastRaidOnUsSeason} " +
            $"distance={HexGrid.Distance(defenderTile, attackerTile, 160)} defenseTech={rig.Match.Research.IsDone(defender, TestPack.Catalog.IndexOf("DEF-03"))} " +
            $"wood={rig.Match.Economy.GoodStock(target, "G-WOOD")} build={buildKind}/{buildShare}; " +
            string.Join(" | ", rig.Notes(defender, "DefenseBuild").Select(note => note.Reason));
        Assert.True(defendedBeforeRaid, defenseState);
        Assert.True(belief.ProjectBelieved[TestPack.Catalog.IndexOf("MET-04")]);
        Assert.True(supportedThreatObserved);

        // A peace/truce between the construction phase and the probe raid must expire before the legal scripted order.
        for (int season = 0; rig.Politics.Diplomacy.InTruce(attacker, defender, rig.Politics.Season) && season < 20; season++)
            rig.Seasons(1);
        Assert.False(rig.Politics.Diplomacy.InTruce(attacker, defender, rig.Politics.Season));
        rig.Politics.Diplomacy.Set(defender, attacker, -60);

        var defensesBeforeAdvance = new Defenses[rig.Match.Economy.SettlementCount];
        for (int settlement = 0; settlement < defensesBeforeAdvance.Length; settlement++)
            defensesBeforeAdvance[settlement] = rig.Politics.MilitaryOf(settlement).Defenses;
        Assert.True(rig.Politics.OrderRaid(attacker, target, 20));
        PoliticsEvent raid = rig.Politics.Events.Last(e => e.Kind == PoliticsEventKind.RaidLaunched && e.Civ == attacker);
        Assert.True(M10TelemetryRules.IsSettlementRaidLaunch(raid));
        Assert.True(M10TelemetryRules.TryMeasureBelievedBronzeRaid(false, believedBronze: true, target,
            defensesBeforeAdvance, out bool measuredDefense));
        Assert.True(measuredDefense);
    }

    [Fact]
    public void It_declares_war_only_with_the_force_ratio()
    {
        // Bows and warriors against spears, Relations hostile: war, an assault, and the ratio in the log.
        StrategistRig strong = Resume();
        WaitOutWarTruce(strong);
        strong.Grant(0, Bows);
        // A sickness has halved the neighbour (its army could otherwise take one of our young daughter settlements in reply,
        // which the Strategist refuses to risk).
        foreach (int s in strong.SettlementsOf(1))
        {
            var people = new System.Collections.Generic.List<FigureView>();
            strong.Match.Economy.ListFigures(s, people);
            for (int i = 0; i < people.Count; i += 2) strong.Match.Economy.KillFigure(people[i].Id);
        }
        strong.Politics.Diplomacy.Set(0, 1, -60);
        strong.Seasons(8);
        Assert.True(strong.Notes(0, "War").Count() == 1, string.Join(" | ", strong.Log.Entries.Where(e => e.Civ == 0 && e.Season > BaseSeason && e.Kind.StartsWith("War")).Select(e => $"{e.Season} {e.Kind} {e.Reason}")));
        AiDecision war = strong.Notes(0, "War").Single();
        Assert.True(war.Score >= StrategistTuning.WarRatio, war.Reason);
        Assert.Contains(strong.Politics.Events, e => e.Kind == PoliticsEventKind.WarDeclared && e.Civ == 0 && e.Other == 1 && e.A == 0);
        Assert.Contains(strong.Notes(0, "Assault"), n => n.Other == 1);

        // The same hostility without the arms: no war, and the refusal says why.
        StrategistRig even = Resume();
        WaitOutWarTruce(even);
        // Make the rival's field strength explicit so this branch exercises the 1.5 force-ratio gate,
        // rather than relying on whatever population the shared opening happened to produce.
        foreach (int settlement in even.SettlementsOf(1))
            even.Match.Economy.FixtureAddPeople(settlement, 300, 25);
        // Keep the prospective war party above the minimum size so rejection reaches the ratio check.
        even.Match.Economy.FixtureAddPeople(even.SettlementsOf(0)[0], 60, 25);
        even.Politics.Diplomacy.Set(0, 1, -60);
        even.Seasons(4);
        Assert.Empty(even.Notes(0, "War"));
        Assert.False(even.Politics.Diplomacy.AtWar(0, 1));
        Assert.Contains(even.Notes(0, "WarRejected"), n => n.Other == 1 &&
            n.Score < StrategistTuning.WarRatio && n.Reason.Contains("force ratio", StringComparison.Ordinal));
    }

    [Fact]
    public void It_makes_peace_when_losing()
    {
        StrategistRig rig = Resume();
        rig.Grant(1, Bows);
        rig.Politics.SetWar(1, 0, true);
        rig.Seasons(2);
        Assert.False(rig.Politics.Diplomacy.AtWar(0, 1));
        Assert.True(rig.Politics.Diplomacy.InTruce(0, 1, rig.Politics.Season));
        Assert.Contains(rig.Notes(0, "Peace"), n => n.Subject == 1 && n.Reason.Contains("cannot win"));
        Assert.Contains(rig.Politics.Events, e => e.Kind == PoliticsEventKind.PeaceMade && e.Civ == 0);
    }

    [Fact]
    public void It_refuses_a_raid_on_a_palisade_and_attacks_an_undefended_neighbour()
    {
        // Give the neighbor built defenses; DEF-03 only unlocks palisade construction.
        string[] spears = { "WAR-01", "HNT-02", "HNT-03", "HNT-04", "WAR-02" };
        StrategistRig walled = Resume();
        walled.Grant(0, spears);
        walled.Grant(1, "DEF-01", "DEF-02", "WOD-03", "DEF-03");
        foreach (int settlement in walled.SettlementsOf(1)) walled.Match.Economy.FixtureBuildStructure(settlement, "palisade");
        walled.Grant(1, Bows); // and warriors with bows behind it (13 §5.5: "palisade + warriors", 5% success)
        walled.Politics.Diplomacy.Set(0, 1, -40);
        walled.Seasons(8);
        Assert.True(!walled.Notes(0, "Raid").Any(), string.Join(" || ", walled.Log.Entries.Where(e => e.Civ == 0 && e.Season > BaseSeason && e.Kind.StartsWith("Raid")).Select(e => $"{e.Season} {e.Kind} {e.Reason}")) + " ## " + string.Join(" ", walled.Match.Ai!.SeatOf(0)!.Belief.Rivals[1].Settlements.Select(b => $"s{b.Settlement} seen{b.LastSeenSeason} pop{b.PopulationEstimate} f{b.FightersEstimate} d{b.DefendingStrengthEstimate} pal{b.PalisadeSeen} w{b.WatchersSeen}")));
        Assert.Contains(walled.Notes(0, "RaidRejected"), n => n.Other == 1 && n.Reason.Contains("palisade seen"));
        Assert.DoesNotContain(walled.Politics.Events, e => e.Kind == PoliticsEventKind.RaidLaunched && e.Civ == 0);

        // The same neighbour without walls is raided (expected value positive on the belief).
        StrategistRig open = Resume();
        open.Grant(0, spears);
        // Keep the counter-threat realistic: this seed has a young daughter settlement that an intact neighbour can
        // take after a 4–5 figure raid party leaves. The raid fixture needs the same weakened rival used by the war test
        // above, otherwise refusing the raid is the intended departure-safety behavior, not a raid-selection regression.
        foreach (int s in open.SettlementsOf(1))
        {
            var people = new System.Collections.Generic.List<FigureView>();
            open.Match.Economy.ListFigures(s, people);
            for (int i = 0; i < people.Count; i += 2) open.Match.Economy.KillFigure(people[i].Id);
        }
        open.Politics.Diplomacy.Set(0, 1, -40);
        open.Seasons(8);
        bool raided = open.Notes(0, "Raid").Any();
        bool conquered = open.Notes(0, "Assault").Any();
        Assert.True(raided || conquered, string.Join(" | ", open.Log.Entries.Where(e => e.Civ == 0 && e.Season > 104).Select(e => $"{e.Season} {e.Kind} {e.Reason}")));
        if (raided)
        {
            AiDecision raid = open.Notes(0, "Raid").First();
            Assert.True(raid.Score >= Fix64.FromRatio(45, 100), raid.Reason);
            Assert.Contains(open.Politics.Events, e => e.Kind == PoliticsEventKind.RaidLaunched && e.Civ == 0 && e.Other == 1);
        }
        else
        {
            Assert.Contains(open.Notes(0, "War"), note => note.Other == 1);
            Assert.Contains(open.Politics.Events, e => e.Kind == PoliticsEventKind.AssaultLaunched && e.Civ == 0 && e.Other == 1);
        }
    }

    [Fact]
    public void It_places_a_Beacon_on_open_land_toward_the_frontier()
    {
        StrategistRig rig = Resume();
        rig.Grant(0, "ART-05");
        rig.Seasons(2);
        int beacon = Assert.Single(rig.Politics.BeaconsOf(0));
        AiDecision note = Assert.Single(rig.Notes(0, "Beacon"));
        Assert.Equal(beacon, note.Subject);
        Assert.NotEqual(1, rig.Politics.Territory.OwnerOf(beacon));
        int rival = rig.SettlementsOf(1).Select(s => rig.Politics.SettlementTileOf(s)).OrderBy(t => HexGrid.Distance(t, beacon, 160)).First();
        int home = rig.SettlementsOf(0).Select(s => rig.Politics.SettlementTileOf(s)).OrderBy(t => HexGrid.Distance(t, rival, 160)).First();
        Assert.True(HexGrid.Distance(beacon, rival, 160) >= StrategistTuning.BeaconRivalKeepOff);
        Assert.True(HexGrid.Distance(beacon, rival, 160) <= HexGrid.Distance(home, rival, 160) + 2, note.Reason);
    }

    [Fact]
    public void Against_a_Rusher_it_loses_no_settlement_and_commits_no_blunder()
    {
        var rig = new StrategistRig(Seed, World.Value, Controls);
        rig.Rushers.Add(new Rusher(1, 0));
        rig.Rushers.Add(new Rusher(2, 3));
        rig.Seasons(132);
        foreach (int civ in new[] { 0, 3 })
        {
            StrategistAudit.Result audit = StrategistAudit.Run(civ, rig.Log.Entries, rig.Politics.Events);
            Assert.Equal(0, audit.SettlementsLost);
            Assert.Equal((0, 0, 0, 0), (audit.SuicideRaids, audit.UndefendedLosses, audit.WinterAggression, audit.Dithering));
            Assert.True(audit.ThreatsNoticed > 0, $"civ {civ} never noticed the Rusher");
        }
    }

    [Fact]
    public void Without_beliefs_it_stays_safe_and_blind_to_targets()
    {
        // The placeholder belief builder knows only the diplomacy: no settlement is ever a target.
        var rig = new StrategistRig(Seed, World.Value, Controls) { Omniscient = false };
        rig.Seasons(BaseSeason);
        Assert.Empty(rig.Notes(0, "Raid"));
        Assert.Empty(rig.Notes(0, "War"));
        Assert.Empty(rig.Notes(0, "Beacon"));
        Assert.Contains(rig.Notes(0, "Gift"), n => n.Subject == 1);
    }
}
