using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>End-to-end wall investment lifecycle checks for docs/MVP/22 §7.4 and 07d §6.</summary>
public sealed class WP8bSiegeLifecycleTests
{
    private const ulong Seed = 20261024;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private sealed class Rig
    {
        public readonly MatchModel Match = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        public readonly SimRunner Runner;
        public Rig() => Runner = new SimRunner(Match);
        public void Grant(int civ, params string[] projects)
        {
            foreach (string project in projects)
                Runner.Submit(new Command(Runner.Tick, (byte)civ, CommandKind.CompleteProject,
                    TestPack.Catalog.IndexOf(project), 0, 0));
            Runner.Advance(1);
        }
        public int SettlementOf(int civ) => Enumerable.Range(0, Match.Economy.SettlementCount)
            .First(s => Match.Economy.SettlementLite(s).Civilization == civ && !Match.Politics.IsFreeFolk(s));
        public int SettlementTargetedBy(int civ) => Enumerable.Range(0, Match.Economy.SettlementCount)
            .First(s => Match.Economy.SettlementLite(s).Civilization == civ && !Match.Politics.IsFreeFolk(s));
        public void Season() => Runner.Advance(150);
    }

    private static (Rig Rig, int Home, int Target) Prepared()
    {
        var rig = new Rig();
        rig.Grant(0, "KEY-3");
        rig.Grant(1, "KEY-3", "DEF-05");
        rig.Season();
        int home = rig.SettlementOf(0), target = rig.SettlementTargetedBy(1);
        rig.Match.Economy.FixtureAddPeople(home, 240, 24);
        rig.Match.Economy.FixtureStock(home, "G-GRAIN", Fix64.FromInt(1000));
        rig.Match.Economy.FixtureStock(target, "G-GRAIN", Fix64.FromInt(1000));
        rig.Match.Economy.FixtureBuildStructure(target, "stone_walls");
        rig.Match.Politics.SetExternalMind(0, true);
        rig.Match.Politics.SetExternalMind(1, true);
        rig.Match.Politics.ForceContact(0, 1);
        rig.Match.Politics.SetWar(0, 1, true);
        return (rig, home, target);
    }

    [Fact]
    public void Assault_against_walls_becomes_persistent_investment_and_survives_snapshot_resume()
    {
        (Rig rig, _, int target) = Prepared();
        Assert.True(rig.Match.Research.IsDone(1, TestPack.Catalog.IndexOf("DEF-05")));
        Assert.Equal(1, rig.Match.Economy.StructureCount(target, LivingEconomyModel.StructureIndex("stone_walls")));

        Assert.True(rig.Match.Politics.OrderAssault(0, target));
        Assert.Contains(rig.Match.Politics.Expeditions, e => e.Kind == ExpeditionKind.Assault && e.ToSettlement == target);
        int arrival = rig.Match.Politics.Expeditions.Single(e => e.Kind == ExpeditionKind.Assault).ArriveSeason;
        rig.Runner.Advance(150 * (arrival - rig.Match.Politics.Season));

        Assert.True(rig.Match.Politics.Events.Any(e => e.Kind == PoliticsEventKind.InvestmentBegan),
            string.Join(" | ", rig.Match.Politics.Events.Where(e => e.Kind is PoliticsEventKind.AssaultResolved or PoliticsEventKind.VoyageLost).Select(e => e.ToString())));
        Investment investment = Assert.Single(rig.Match.Politics.Investments);
        Assert.Equal(target, investment.Settlement);
        Assert.Equal(1, investment.State.BesiegerFoodSeasons);
        Assert.True(rig.Match.Economy.IsUnderInvestment(target));
        Assert.Contains(rig.Match.Politics.Events, e => e.Kind == PoliticsEventKind.InvestmentBegan && e.Civ == 0);

        using var snapshot = new MemoryStream();
        using (var writer = new BinaryWriter(snapshot, System.Text.Encoding.UTF8, leaveOpen: true)) rig.Match.WriteSnapshot(writer);
        snapshot.Position = 0;
        MatchModel resumed = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        using (var reader = new BinaryReader(snapshot)) resumed.ReadSnapshot(reader);
        Assert.Equal(rig.Match.ComputeChecksum(), resumed.ComputeChecksum());
        Assert.Single(resumed.Politics.Investments);
        Assert.True(resumed.Economy.IsUnderInvestment(target));

        rig.Season();
        new SimRunner(resumed, 50, rig.Runner.Tick - 150).Advance(150);
        Assert.Equal(rig.Match.ComputeChecksum(), resumed.ComputeChecksum());
    }

    [Fact]
    public void Owner_relief_transition_fights_in_open_field_and_can_lift_the_investment()
    {
        (Rig rig, _, int target) = Prepared();
        Assert.True(rig.Match.Politics.OrderAssault(0, target));
        int arrival = rig.Match.Politics.Expeditions.Single(e => e.Kind == ExpeditionKind.Assault).ArriveSeason;
        rig.Runner.Advance(150 * (arrival - rig.Match.Politics.Season));
        Assert.NotNull(rig.Match.Politics.InvestmentAt(target));

        // Model a relief force already arrived at the settlement, without adding a new command surface.
        rig.Match.Economy.FixtureAddPeople(target, 240, 24);
        rig.Match.Economy.FixtureStock(target, "G-GRAIN", Fix64.FromInt(1000));
        var records = new List<LivingEconomyModel.FighterRecord>();
        rig.Match.Economy.ListFightingAge(target, records);
        long[] relief = records.Select(f => f.Id).ToArray();
        Assert.True(rig.Match.Politics.ResolveInvestmentRelief(1, target, relief));

        Assert.Null(rig.Match.Politics.InvestmentAt(target));
        Assert.False(rig.Match.Economy.IsUnderInvestment(target));
        Assert.Contains(rig.Match.Politics.Events, e => e.Kind == PoliticsEventKind.InvestmentRelieved && e.Civ == 1);
    }

    [Fact]
    public void Siege_helper_uses_the_documented_investment_lift_and_store_gates_deterministically()
    {
        Assert.True(Sieges.CanInvest(Fix64.FromRatio(132, 10), Fix64.FromInt(10)));
        Assert.False(Sieges.CanInvest(Fix64.FromRatio(119, 10), Fix64.FromInt(10)));
        var safe = new SiegeState(Fix64.FromInt(20), Fix64.FromInt(10), Fix64.FromInt(30), Fix64.FromInt(2), 1, 0);
        Assert.Equal(SiegeResult.Ongoing, Sieges.Advance(ref safe, supplied: true, Seed, 19, settlement: 7));

        int lowCapitulations = 0, emptyCapitulations = 0;
        for (ulong seed = 1; seed <= 64; seed++)
        {
            var low = new SiegeState(Fix64.FromInt(20), Fix64.FromInt(10), Fix64.FromInt(30), Fix64.One, 1, 0);
            SiegeResult lowResult = Sieges.Advance(ref low, supplied: true, seed, 19, settlement: 7);
            var replay = new SiegeState(Fix64.FromInt(20), Fix64.FromInt(10), Fix64.FromInt(30), Fix64.One, 1, 0);
            Assert.Equal(lowResult, Sieges.Advance(ref replay, supplied: true, seed, 19, settlement: 7));
            if (lowResult == SiegeResult.Capitulated) lowCapitulations++;

            var empty = new SiegeState(Fix64.FromInt(20), Fix64.FromInt(10), Fix64.FromInt(30),
                Sieges.RationNeed, 1, 0);
            if (Sieges.Advance(ref empty, supplied: true, seed, 19, settlement: 7) == SiegeResult.Capitulated)
                emptyCapitulations++;
        }
        Assert.InRange(lowCapitulations, 1, 40); // 35% deterministic keyed stream.
        Assert.True(emptyCapitulations > lowCapitulations); // 70% at zero stores.

        var carried = new SiegeState(Fix64.FromInt(20), Fix64.FromInt(10), Fix64.FromInt(30),
            Fix64.FromInt(4), 1, 0);
        Assert.Equal(SiegeResult.Ongoing, Sieges.Advance(ref carried, supplied: false, Seed, 19, settlement: 7));
        Assert.Equal(Fix64.FromInt(20), carried.BesiegerStrength);
        Assert.Equal(0, carried.BesiegerFoodSeasons);
        Assert.Equal(SiegeResult.Ongoing, Sieges.Advance(ref carried, supplied: false, Seed, 19, settlement: 7));
        Assert.True(carried.BesiegerStrength >= Fix64.FromRatio(17999, 1000) &&
            carried.BesiegerStrength <= Fix64.FromRatio(18001, 1000));
    }
}
