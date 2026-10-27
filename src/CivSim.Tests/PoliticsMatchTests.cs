using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>M5 on the real living economy: territory, contact, trade, raids, capture. Scenarios use the hooks that stand in for a decision.</summary>
public class PoliticsMatchTests
{
    private const ulong Seed = 20261019;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private sealed class Rig
    {
        public readonly MatchModel Match = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        public readonly SimRunner Runner;
        public Rig() { Runner = new SimRunner(Match); }
        public PoliticsSystem Politics => Match.Politics;
        public ResearchSystem Research => Match.Research;
        public LivingEconomyModel Economy => Match.Economy;
        public void Seasons(int n) => Runner.Advance(150L * n);
        public void Minutes(double m) => Runner.Advance((long)(m * 600) - Runner.Tick);

        /// <summary>Hands a civilization completed projects (and the economy with them), as a fixture.</summary>
        public void Grant(int civ, params string[] ids)
        {
            foreach (string id in ids)
                Runner.Submit(new Command(Runner.Tick, (byte)civ, CommandKind.CompleteProject, TestPack.Catalog.IndexOf(id), 0, 0));
            Runner.Advance(1);
        }

        public int SettlementOf(int civ)
        {
            for (int i = 0; i < Economy.SettlementCount; i++)
                if (Economy.SettlementLite(i).Civilization == civ && !Politics.IsFreeFolk(i)) return i;
            throw new InvalidOperationException($"civ {civ} has no settlement");
        }
    }

    [Fact]
    public void Paired_neighbours_meet_in_the_window_and_the_meeting_is_recorded_everywhere()
    {
        var rig = new Rig();
        rig.Minutes(24.5);
        var contacts = rig.Politics.Events.Where(e => e.Kind == PoliticsEventKind.Contact).ToList();
        Assert.NotEmpty(contacts);
        double first = contacts.Min(e => (e.Tick + 1) / 600.0);
        Assert.InRange(first, 15, 25); // 13 §11 F1 for this seed (the harness audits 24 seeds)
        PoliticsEvent c = contacts[0];
        int a = c.Civ, b = c.Other;
        Assert.True(rig.Politics.Diplomacy.Contacted(a, b));
        Assert.True(rig.Politics.Diplomacy.Aware(b, a));
        Assert.True(rig.Research.InContact(a, b)); // research learns of the contact: Diffusion and Glimpses begin
        Assert.Contains(rig.Politics.Events, e => e.Kind == PoliticsEventKind.Aware && e.Civ == a);
        // Aware before or when they meet; the other pair has not necessarily met.
        Assert.Equal(0, rig.Politics.Diplomacy.Relations(a, b) % 1);
    }

    [Fact]
    public void Peaceful_neighbours_exchange_gifts_and_barter_and_their_relations_grow()
    {
        var rig = new Rig();
        // Give late-contact neighbours enough seasons to exchange gifts and raise relations.
        rig.Minutes(30);
        PoliticsSystem p = rig.Politics;
        Assert.True(p.Gifts > 0, "no gifts were delivered");
        Assert.True(p.BarterTrips > 0, "no barter trip completed");
        var met = new List<(int, int)>();
        for (int a = 0; a < 4; a++) for (int b = a + 1; b < 4; b++) if (p.Diplomacy.Contacted(a, b)) met.Add((a, b));
        Assert.NotEmpty(met);
        foreach ((int a, int b) in met) Assert.True(p.Diplomacy.Relations(a, b) > 20, $"{a}-{b} relations {p.Diplomacy.Relations(a, b)}");
        Assert.Contains(p.Events, e => e.Kind == PoliticsEventKind.Gift);
        Assert.Contains(p.Events, e => e.Kind == PoliticsEventKind.Barter);
        // Peaceful meetings end in no raids or wars.
        Assert.Equal(0, p.RaidCount);
        Assert.Equal(0, p.Wars);
        // And the land: claims have grown to something like 07b's end-of-E2 figure (12-13% there; the live trajectory holds a third settlement).
        Assert.InRange(p.Territory.ClaimedBasisPoints(), 900, 1700);
    }

    [Fact]
    public void Barter_unit_value_clamps_before_dividing_by_tiny_positive_stock()
    {
        var rig = new Rig();
        int settlement = rig.SettlementOf(0);
        rig.Economy.FixtureStock(settlement, "G-SALT", -Fix64.FromInt(1_000_000_000));
        rig.Economy.FixtureStock(settlement, "G-SALT", Fix64.Epsilon);

        Fix64 value = rig.Politics.UnitValue(settlement, TradeGood.Salt);

        Assert.Equal(rig.Economy.GoodBarterValue("G-SALT") * Fix64.FromInt(3), value);
    }

    [Fact]
    public void Territory_is_owned_by_the_settlements_that_radiate_it_and_dominion_accrues()
    {
        var rig = new Rig();
        rig.Minutes(8);
        Territory t = rig.Politics.Territory;
        for (int civ = 0; civ < 4; civ++)
        {
            int s = rig.SettlementOf(civ);
            int site = rig.Economy.SettlementLite(s).SiteTile;
            Assert.Equal(civ, t.OwnerOf(site));
            Assert.True(t.OwnedTiles(civ) >= 5);
            Assert.True(t.Dominion(civ) > Fix64.Zero);
        }
        double total = 0;
        for (int civ = 0; civ < 4; civ++) total += t.Dominion(civ).ToDoubleForDisplay();
        // 8 minutes is 32 seasons, 8 years of 100 points each.
        Assert.InRange(total, 700, 800);
    }

    [Fact]
    public void Seasonal_camps_start_with_the_Seasonal_Round_and_widen_the_claim()
    {
        var rig = new Rig();
        rig.Minutes(5);
        int before = rig.Politics.Territory.OwnedTiles(0);
        rig.Grant(0, "WAY-01", "FOR-01", "WOD-01", "FOR-02", "WAY-02", "COM-01", "STN-01", "FIR-01", "SOC-01", "STN-02"); // grants skip prerequisites
        rig.Seasons(3);
        Assert.True(rig.Research.IsDone(0, TestPack.Catalog.IndexOf("WAY-02")));
        Assert.True(rig.Politics.Territory.OwnedTiles(0) > before + 15, $"before {before}, after {rig.Politics.Territory.OwnedTiles(0)}");
    }

    [Fact]
    public void A_raid_sets_out_arrives_and_leaves_the_dead_the_loot_and_a_grudge()
    {
        var rig = new Rig();
        rig.Minutes(24); // bands are big enough to spare a war party by now
        rig.Grant(0, "HNT-02", "HNT-03", "WAR-01", "WAR-02", "HNT-04");
        rig.Politics.ForceContact(0, 1);
        rig.Politics.Diplomacy.Set(0, 1, -40);
        int home = rig.SettlementOf(0), prey = rig.SettlementOf(1);
        int figuresBefore = rig.Economy.FigureCount;

        var trace = new List<string>();
        rig.Politics.Trace = trace.Add;
        Assert.True(rig.Politics.LaunchRaid(0, 1, forecast: false), string.Join("; ", trace.Where(t => t.StartsWith("raid"))));
        Assert.Contains(rig.Politics.Expeditions, e => e.Kind == ExpeditionKind.Raid && e.Civ == 0);
        Assert.Contains(rig.Politics.Events, e => e.Kind == PoliticsEventKind.RaidLaunched);
        rig.Seasons(8);

        Assert.DoesNotContain(rig.Politics.Expeditions, e => e.Kind == ExpeditionKind.Raid);
        PoliticsEvent done = rig.Politics.Events.First(e => e.Kind == PoliticsEventKind.RaidResolved && e.Civ == 0);
        Assert.True(rig.Politics.RaidCount >= 1);
        Assert.True(rig.Politics.Diplomacy.Relations(0, 1) <= -50, $"relations {rig.Politics.Diplomacy.Relations(0, 1)}"); // -40 then -30 (-50 with deaths), less a few points of yearly drift
        Assert.True(rig.Politics.Diplomacy.LastRaidBy(0, 1) > 0);
        if (done.A == 1)
        {
            Assert.True(done.B > 0, "a successful raid carries something off");
            Assert.True(rig.Politics.RaidsSucceeded >= 1);
        }
        // Either way people were hurt or killed: fewer figures than natural births alone would leave, or wounded ones.
        int wounded = 0;
        for (int i = 0; i < rig.Economy.FigureCount; i++) if (rig.Economy.IsWounded(rig.Economy.Figure(i).Id)) wounded++;
        Assert.True(wounded > 0 || rig.Economy.FigureCount < figuresBefore + 10, $"wounded {wounded}");
        Assert.True(home >= 0 && prey >= 0);
    }

    [Fact]
    public void A_raid_uses_and_consumes_the_host_gathered_for_its_target()
    {
        var rig = new Rig();
        rig.Minutes(24);
        rig.Grant(0, "KEY-3", "WAR-02", "HNT-02", "HNT-03", "WAR-01");
        rig.Politics.ForceContact(0, 1);
        int target = rig.SettlementOf(1);
        int home = rig.SettlementOf(0);
        var records = new List<LivingEconomyModel.FighterRecord>();
        rig.Economy.ListFightingAge(home, records);
        var host = new Levy
        {
            Civ = 0,
            TargetSettlement = target,
            SinceSeason = rig.Politics.Season,
            UntilSeason = rig.Politics.Season + 2
        };
        foreach (LivingEconomyModel.FighterRecord record in records.Where(r => !r.Wounded).Take(Math.Max(0, records.Count - 4)).Take(8))
        {
            host.Members.Add(record.Id);
            host.Strengths.Add(Fix64.One);
        }
        Assert.True(host.Members.Count >= 4);
        rig.Economy.SetIdle(host.Members, host.UntilSeason);
        var levies = (List<Levy>)typeof(PoliticsSystem).GetField("_levies", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(rig.Politics)!;
        levies.Add(host);
        int hostSize = host.Members.Count;
        Assert.True(hostSize >= 4);
        Assert.True(rig.Politics.OrderRaid(0, target, 4));

        PoliticsEvent launch = rig.Politics.Events.Last(e => e.Kind == PoliticsEventKind.RaidLaunched && e.Civ == 0);
        Assert.Equal(hostSize, launch.A);
        Assert.Null(rig.Politics.LevyFor(0, target));
    }

    [Fact]
    public void Mining_camp_raid_targets_the_tile_and_transfers_only_bounded_stored_ore()
    {
        var rig = new Rig();
        rig.Minutes(24);
        rig.Grant(0, "KEY-3", "WAR-02", "HNT-02", "HNT-03", "WAR-01");
        rig.Grant(1, "KEY-3");
        int home = rig.SettlementOf(0), parent = rig.SettlementOf(1);
        rig.Economy.FixtureAddPeople(home, 20, 20);
        rig.Economy.FixtureAddPeople(parent, 4, 20);
        rig.Politics.ForceContact(0, 1);
        int parentTile = rig.Economy.SettlementLite(parent).SiteTile;
        int campTile = Enumerable.Range(0, World.Value.TileCount)
            .Where(tile => World.Value.TileAt(tile).Deposit == DepositKind.CopperOre && tile != parentTile)
            .OrderBy(tile => HexGrid.Distance(parentTile, tile, World.Value.Width))
            .First();
        rig.Economy.FixtureAddMiningCamp(parent, campTile, crew: 2, built: false);
        MiningCampView camp = Enumerable.Range(0, rig.Economy.MiningCampCount)
            .Select(rig.Economy.MiningCamp).First(c => c.Tile == campTile);
        Assert.Equal(2, camp.Crew);
        rig.Economy.FixtureStock(parent, "G-CUORE", Fix64.FromInt(100));
        Fix64 targetStockBefore = rig.Economy.GoodStock(parent, "G-CUORE");
        Fix64 homeStockBefore = rig.Economy.GoodStock(home, "G-CUORE");

        Assert.True(rig.Politics.OrderRaidMiningCamp(0, camp.Id, 12));
        Expedition raid = Assert.Single(rig.Politics.Expeditions, e => e.Kind == ExpeditionKind.Raid && e.Civ == 0);
        Assert.Equal(-1, raid.ToSettlement);
        Assert.Equal(-1, raid.Id); // camp IDs do not share the caravan/explorer party namespace
        Assert.Equal(camp.Id, raid.TargetCampId);
        Assert.Equal(campTile, raid.TargetTile);

        using var snapshot = new MemoryStream();
        using (var writer = new BinaryWriter(snapshot, System.Text.Encoding.UTF8, leaveOpen: true)) rig.Match.WriteSnapshot(writer);
        snapshot.Position = 0;
        MatchModel resumed = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        using (var reader = new BinaryReader(snapshot)) resumed.ReadSnapshot(reader);
        Assert.Equal(rig.Match.ComputeChecksum(), resumed.ComputeChecksum());
        var resumedRunner = new SimRunner(resumed, 50, rig.Runner.Tick);

        var frame = new MatchFrame();
        var frameBuilder = new FrameBuilder(rig.Match, 0);
        frameBuilder.Build(rig.Match, frame);
        int party = Enumerable.Range(0, frame.PartyCount)
            .First(i => frame.PartyCiv[i] == 0 && frame.PartyTargetCiv[i] == 1 && frame.PartyKind[i] == (byte)ExpeditionKind.Raid);
        Assert.Equal(-1, frame.PartyTo[party]);
        Assert.Equal(campTile, frame.PartyToTile[party]);
        ViewEvent launch = Enumerable.Range(0, frameBuilder.EventCount).Select(frameBuilder.Event)
            .Last(e => e.Kind == ViewEventKind.RaidLaunched && e.Civ == 0);
        Assert.Equal(-1, launch.Settlement);
        Assert.Equal(campTile, launch.Tile);
        Assert.Contains(rig.Politics.Events, e => e.Kind == PoliticsEventKind.MiningCampRaidLaunched && e.B == campTile);

        int arrivalSeason = raid.ArriveSeason;
        Fix64 oreAtDeposit = camp.OreLeft;
        int arrivalTicks = Math.Max(1, arrivalSeason - rig.Politics.Season + 1) * 150;
        rig.Runner.Advance(arrivalTicks);
        resumedRunner.Advance(arrivalTicks);

        Assert.Contains(rig.Politics.Events, e => e.Kind == PoliticsEventKind.RaidResolved && e.Civ == 0);
        Assert.Equal(rig.Match.ComputeChecksum(), resumed.ComputeChecksum());
        Fix64 deliveredValue = (rig.Economy.GoodStock(home, "G-CUORE") - homeStockBefore) * rig.Economy.GoodBarterValue("G-CUORE");
        Assert.InRange(deliveredValue.ToDoubleForDisplay(), 0,
            (Raids.CarryPerRaider * raid.Members.Count * Raids.DeliveredLootShare).ToDoubleForDisplay());
        Assert.True(rig.Economy.GoodStock(parent, "G-CUORE") <= targetStockBefore);
        Assert.True(rig.Economy.TryMiningCampById(camp.Id, out MiningCampView afterRaid));
        Assert.Equal(oreAtDeposit, afterRaid.OreLeft); // a raid loots hauled stock, never underground ore
    }

    [Fact]
    public void Def03_unlocks_palisade_construction_but_does_not_grant_one_to_every_settlement()
    {
        var rig = new Rig();
        rig.Grant(0, "DEF-01", "DEF-02", "WOD-03", "DEF-03");
        int home = rig.SettlementOf(0);
        int palisade = LivingEconomyModel.StructureIndex("palisade");

        Assert.True(palisade >= 0);
        Assert.False(rig.Politics.MilitaryOf(home).Defenses.Palisade);

        rig.Economy.FixtureBuildStructure(home, "palisade");

        Assert.True(rig.Politics.MilitaryOf(home).Defenses.Palisade);
        Assert.Equal(1, rig.Economy.StructureCount(home, palisade));
    }

    [Fact]
    public void A_captured_settlement_changes_hands_with_half_its_people_and_the_rest_flee()
    {
        var rig = new Rig();
        rig.Minutes(17); // each civilization has two settlements by now
        int loserCivSettlements = 0;
        for (int i = 0; i < rig.Economy.SettlementCount; i++) if (rig.Economy.SettlementLite(i).Civilization == 1) loserCivSettlements++;
        Assert.True(loserCivSettlements >= 2, "the scenario needs a rival with a refuge");
        rig.Grant(0, "HNT-02", "HNT-03", "WAR-01", "WAR-02");
        rig.Politics.ForceContact(0, 1);
        int target = rig.SettlementOf(1);
        int targetId = rig.Economy.SettlementLite(target).Id;
        int popBefore = rig.Economy.Population(targetId);
        int civ1Before = Enumerable.Range(0, rig.Economy.FigureCount).Count(i => rig.Economy.Figure(i).Civilization == 1);
        int civ0Before = Enumerable.Range(0, rig.Economy.FigureCount).Count(i => rig.Economy.Figure(i).Civilization == 0);

        Assert.True(rig.Politics.Occupy(0, target));
        rig.Seasons(1);
        Assert.Contains(rig.Politics.Events, e => e.Kind == PoliticsEventKind.Captured);
        Assert.Equal(0, rig.Economy.SettlementLite(target).Civilization);
        int staying = rig.Economy.Population(targetId);
        Assert.InRange(staying, popBefore * 4 / 10, popBefore * 7 / 10); // about half stay
        // The ones who left are still alive, with the loser, so nobody vanished.
        int civ1After = Enumerable.Range(0, rig.Economy.FigureCount).Count(i => rig.Economy.Figure(i).Civilization == 1);
        int civ0After = Enumerable.Range(0, rig.Economy.FigureCount).Count(i => rig.Economy.Figure(i).Civilization == 0);
        Assert.True(civ0After > civ0Before - 3 && civ1After > civ1Before - popBefore + 1);
        Assert.True(rig.Politics.Diplomacy.Relations(0, 1) <= -70 + 10);
        Assert.Equal(1, rig.Politics.CaptureCount);
        // The captured are unsettled: Cohesion starts 0.2 down and recovers 0.02 a season.
        Fix64 cohesion = rig.Economy.SettlementCohesion(target);
        Assert.True(cohesion < Fix64.One - Fix64.FromRatio(1, 10), cohesion.ToString());
        rig.Seasons(6);
        Assert.True(rig.Economy.SettlementCohesion(target) > cohesion);
        // Ownership of the land follows within a couple of seasons (the claim rule's hysteresis, 05 §11.2).
        rig.Seasons(3);
        int site = rig.Economy.SettlementLite(target).SiteTile;
        Assert.Equal(0, rig.Politics.Territory.OwnerOf(site));
    }

    [Fact]
    public void People_with_nowhere_to_flee_become_Free_Folk_who_make_no_Insight_and_claim_nothing()
    {
        var rig = new Rig();
        rig.Minutes(5); // one settlement each
        rig.Grant(0, "HNT-02", "WAR-01", "WAR-02");
        rig.Politics.ForceContact(0, 1);
        int target = rig.SettlementOf(1);
        Assert.True(rig.Politics.Occupy(0, target));
        int before = rig.Economy.SettlementCount;
        rig.Seasons(1);
        Assert.Equal(before + 1, rig.Economy.SettlementCount);
        int camp = rig.Economy.SettlementCount - 1;
        Assert.True(rig.Politics.IsFreeFolk(camp));
        Assert.Contains(rig.Politics.Events, e => e.Kind == PoliticsEventKind.FreeFolkFounded);
        Assert.True(rig.Economy.Population(rig.Economy.SettlementLite(camp).Id) > 0);
        rig.Seasons(2);
        // Research ignores them, and so does the claim field.
        Assert.Equal(0, rig.Politics.Territory.ClaimOf(1, rig.Economy.SettlementLite(camp).SiteTile));
        Assert.Equal(0, rig.Match.Source.SettlementCount(1));
    }

    [Fact]
    public void Posture_and_Beacons_are_levers_that_need_their_projects()
    {
        var rig = new Rig();
        rig.Minutes(3);
        rig.Runner.Submit(new Command(rig.Runner.Tick, 0, CommandKind.SetPosture, (int)Posture.Expand, 0, 0));
        rig.Runner.Submit(new Command(rig.Runner.Tick, 0, CommandKind.PlaceBeacon, 40, 40, 0));
        rig.Seasons(1);
        Assert.Equal(Posture.Balanced, rig.Politics.PostureOf(0)); // GOV-02 first
        Assert.Empty(rig.Politics.BeaconsOf(0));                   // ART-05 first
        Assert.Equal(Fix64.One, rig.Economy.FissionScaleOf(0));

        rig.Grant(0, "GOV-02", "ART-05");
        rig.Runner.Submit(new Command(rig.Runner.Tick, 0, CommandKind.SetPosture, (int)Posture.Expand, 0, 0));
        rig.Runner.Submit(new Command(rig.Runner.Tick, 0, CommandKind.PlaceBeacon, 40, 40, 0));
        rig.Runner.Submit(new Command(rig.Runner.Tick, 0, CommandKind.PlaceBeacon, 41, 41, 0)); // one Beacon only
        rig.Runner.Submit(new Command(rig.Runner.Tick, 1, CommandKind.SetPosture, (int)Posture.Raid, 0, 0)); // civ 1 has no GOV-02
        rig.Seasons(1);
        Assert.Equal(Posture.Expand, rig.Politics.PostureOf(0));
        Assert.Equal(Posture.Balanced, rig.Politics.PostureOf(1));
        Assert.Single(rig.Politics.BeaconsOf(0));
        Assert.Equal(Fix64.FromRatio(8, 10), rig.Economy.FissionScaleOf(0)); // Expand: fission threshold -20%

        rig.Runner.Submit(new Command(rig.Runner.Tick, 0, CommandKind.RemoveBeacon, 0, 0, 0));
        rig.Runner.Submit(new Command(rig.Runner.Tick, 0, CommandKind.SetPosture, (int)Posture.Prosper, 0, 0));
        rig.Seasons(1);
        Assert.Empty(rig.Politics.BeaconsOf(0));
        Assert.Equal(Fix64.FromRatio(12, 10), rig.Economy.FissionScaleOf(0));
        // A Beacon pulls fission sites toward it and rival-owned ground is refused.
        rig.Runner.Submit(new Command(rig.Runner.Tick, 0, CommandKind.PlaceBeacon, 20, 20, 0));
        rig.Seasons(1);
        int near = 20 * 160 + 20;
        Assert.True(rig.Politics.Multiplier(0, near) > Fix64.One);
        Assert.True(rig.Politics.Multiplier(0, near + 16 * 160) < rig.Politics.Multiplier(0, near));
    }

    [Fact]
    public void A_snapshot_taken_after_contact_resumes_to_the_same_checksum()
    {
        var straight = new Rig();
        straight.Minutes(22.5);
        var first = new Rig();
        first.Minutes(21);
        Assert.True(first.Politics.Diplomacy.Contacted(0, 1) || first.Politics.Diplomacy.Contacted(2, 3));
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) first.Match.WriteSnapshot(w);
        stream.Position = 0;
        MatchModel resumed = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        using (var r = new BinaryReader(stream)) resumed.ReadSnapshot(r);
        Assert.Equal(first.Match.ComputeChecksum(), resumed.ComputeChecksum());
        var runner = new SimRunner(resumed, 50, first.Runner.Tick);
        runner.Advance(900);
        Assert.Equal(straight.Match.ComputeChecksum(), resumed.ComputeChecksum());
    }

    [Fact]
    public void A_civilization_on_Raid_posture_raids_a_neighbour_by_its_own_rules()
    {
        var rig = new Rig();
        rig.Minutes(24);
        rig.Grant(0, "GOV-02", "HNT-02", "HNT-03", "WAR-01", "WAR-02", "HNT-04");
        rig.Politics.ForceContact(0, 1);
        rig.Runner.Submit(new Command(rig.Runner.Tick, 0, CommandKind.SetPosture, (int)Posture.Raid, 0, 0));
        var trace = new List<string>();
        rig.Politics.Trace = trace.Add;
        rig.Seasons(10);
        Assert.Equal(Posture.Raid, rig.Politics.PostureOf(0));
        Assert.True(rig.Politics.RaidCount >= 1, "the Elders never judged a raid worth sending: " + string.Join(" | ", trace.Where(t => t.StartsWith("raid")).Take(6)));
        Assert.All(rig.Politics.Events.Where(e => e.Kind == PoliticsEventKind.RaidLaunched), e => Assert.Equal(0, e.Civ));
        // Fortify and Prosper never raid.
        var calm = new Rig();
        calm.Minutes(24);
        calm.Grant(0, "GOV-02", "HNT-02", "HNT-03", "WAR-01", "WAR-02", "HNT-04");
        calm.Politics.ForceContact(0, 1);
        calm.Politics.Diplomacy.Set(0, 1, -60);
        calm.Runner.Submit(new Command(calm.Runner.Tick, 0, CommandKind.SetPosture, (int)Posture.Fortify, 0, 0));
        calm.Seasons(10);
        Assert.Equal(0, calm.Politics.RaidCount);
    }

    [Fact]
    public void A_war_sends_an_army_fights_for_a_village_and_ends_in_a_truce()
    {
        var rig = new Rig();
        rig.Minutes(24);
        rig.Grant(0, "HNT-02", "HNT-03", "WAR-01", "WAR-02", "WAR-04");
        rig.Politics.ForceContact(0, 1);
        rig.Politics.SetWar(0, 1, true);
        Assert.True(rig.Politics.Diplomacy.AtWar(1, 0));
        Assert.True(rig.Politics.LaunchAssault(0, 1, requireOdds: false));
        rig.Seasons(5);
        // The assault was fought: it either failed or an occupation began, which then ended in capture, relief or a broken hold.
        Assert.Contains(rig.Politics.Events, e => e.Kind == PoliticsEventKind.AssaultResolved && e.Civ == 0);
        Assert.Contains(rig.Politics.Events, e => e.Kind == PoliticsEventKind.AssaultResolved && e.Civ == 1);
        rig.Seasons(14);
        Assert.False(rig.Politics.Diplomacy.AtWar(0, 1), "a war that has run its course ends in peace");
        Assert.Contains(rig.Politics.Events, e => e.Kind == PoliticsEventKind.PeaceMade);
        Assert.True(rig.Politics.Diplomacy.InTruce(0, 1, rig.Politics.Season));
    }

    [Fact]
    public void Barter_shares_what_each_side_has_seen_of_the_traded_materials()
    {
        var rig = new Rig();
        rig.Minutes(21);
        ulong goods = 0;
        foreach (string name in new[] { "Clay", "Native copper", "Salt", "Tin", "Gold or Silver", "Coal", "Silk moths" })
        {
            int t = TestPack.Catalog.GateTargetIndex(name);
            if (t >= 0) goods |= 1UL << t;
        }
        rig.Grant(0, "TRD-01", "TRD-02");
        rig.Grant(1, "TRD-01", "TRD-02");
        rig.Politics.ForceContact(0, 1);
        rig.Politics.Diplomacy.Set(0, 1, 30);
        ulong had0 = rig.Research.SeenMask(0) & goods, had1 = rig.Research.SeenMask(1) & goods;
        rig.Seasons(12);
        Assert.True(rig.Politics.BarterTrips >= 1);
        ulong now0 = rig.Research.SeenMask(0) & goods, now1 = rig.Research.SeenMask(1) & goods;
        // Each side now has seen at least what the other had seen of the traded goods (13 §4.2).
        Assert.Equal(had1, had1 & now0);
        Assert.Equal(had0, had0 & now1);
    }

    [Fact]
    public void Tin_caravan_checks_its_current_tile_when_only_that_tile_is_hostile()
    {
        var rig = new Rig();
        PoliticsSystem politics = rig.Politics;
        int from = rig.SettlementOf(0), to = rig.SettlementOf(1);
        var caravan = new Expedition
        {
            Kind = ExpeditionKind.Caravan, Civ = 0, FromSettlement = from, ToSettlement = to,
            // Keep it at the origin so the only hostile tile can be its current position.
            ArriveSeason = politics.Season + 100, Id = 99001, Good = TradeGood.Tin,
        };
        int currentTile = politics.PartyTile(caravan);
        Assert.Equal(rig.Economy.SettlementLite(from).SiteTile, currentTile);

        var owners = (sbyte[])typeof(Territory)
            .GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(politics.Territory)!;
        Array.Fill(owners, (sbyte)0);
        owners[currentTile] = 1;
        politics.Diplomacy.Set(0, 1, -20);

        Assert.True(politics.CaravanCrossesHostileTerritory(caravan));
    }
}
