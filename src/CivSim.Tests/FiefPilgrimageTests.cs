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

/// <summary>BEL-11 completed-road pilgrimage trips, journey food, route-local modifiers, and chapter isolation.</summary>
public sealed class FiefPilgrimageTests
{
    private const ulong Seed = 20261116;
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
    private static readonly string[] ReadyProjects = { "KEY-5", "BEL-10", "TRN-07", "BEL-11", "BLD-11", "STN-13", "BLD-12" };

    private static (MatchModel Match, SimRunner Runner) Start(ChapterRules? rules = null)
    {
        var pack = TestPack.WithNumeric("BEL-10", "BEL-11", "TRN-07", "BLD-11", "STN-13", "BLD-12");
        GeneratedWorld world = WorldGenerator.Generate(Seed);
        MatchModel match = MatchModel.Create(Seed, pack, world, Balanced, default,
            rules: rules ?? ChapterRules.Fief);
        return (match, new SimRunner(match));
    }

    private static void Grant(SimRunner runner, MatchModel match, int civ, params string[] projects)
    {
        foreach (string project in projects)
        {
            int index = match.Research.Catalog.IndexOf(project);
            Assert.True(index >= 0, $"{project} must exist in the test pack");
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, index, 0, 0));
            runner.Advance(2);
        }
    }

    private static (int From, int To) ConnectTwoOwnTowns(MatchModel match)
    {
        int from = Enumerable.Range(0, match.Economy.SettlementCount)
            .First(i => match.Economy.SettlementLite(i).Civilization == 0);
        int to = Enumerable.Range(0, match.Economy.SettlementCount)
            .First(i => match.Economy.SettlementLite(i).Civilization == 1);
        match.Economy.FixtureSetCivilizationAndResidents(to, 0);
        match.Economy.FixtureSetTier(from, 1);
        match.Economy.FixtureSetTier(to, 1);
        match.Economy.FixtureBuildIronStructure(to, "road");
        if (match.Economy.FixtureRiversOnLink(to) > 0) match.Economy.FixtureBuildIronStructure(to, "bridge");
        Assert.True(match.Economy.FixtureRoadLinked(to));
        match.Politics.FixtureRefreshPilgrimageSettlements();
        return (from, to);
    }

    [Fact]
    public void BEL_11_orders_only_over_completed_connected_roads_and_spends_food_without_revealing_fog()
    {
        (MatchModel match, SimRunner runner) = Start();
        Grant(runner, match, 0, ReadyProjects);
        (int from, int to) = ConnectTwoOwnTowns(match);
        var settlements = new List<int>();
        var tiles = new List<int>();
        Assert.True(match.Economy.TryPilgrimageRoadPath(0, from, to, settlements, tiles));
        int unknownTile = tiles.FirstOrDefault(tile => !match.Politics.IsRevealed(0, tile), -1);
        Assert.True(unknownTile >= 0, "the route fixture should include an unrevealed road tile");
        match.Economy.TakeGoods(from, match.Economy.FoodStores(from), LootPreference.FoodOnly);
        Assert.False(match.Politics.OrderPilgrimage(0, from, to)); // No food means no partial trip.

        match.Economy.GiveGoods(from, Fix64.FromInt(1000));
        Fix64 foodBefore = match.Economy.FoodStores(from);
        int seasons = Math.Max(1, (tiles.Count - 1 + 7) / 8);
        Fix64 expectedFood = Fix64.FromInt(4 * seasons);
        var command = new Command(runner.Tick, 0, CommandKind.Pilgrimage, from, to, 0);
        match.Politics.Step(runner.Tick, new[] { command });

        Assert.Equal(expectedFood, foodBefore - match.Economy.FoodStores(from));
        Assert.False(match.Politics.IsRevealed(0, unknownTile));
        Assert.Contains(match.Politics.Events, e => e.Kind == PoliticsEventKind.PilgrimageCompleted && e.Civ == 0 && e.Other == to);
        Assert.Equal(Fix64.FromRatio(115, 100), match.Politics.PilgrimageTradeMultiplier(from, to));
        Assert.Equal(Fix64.One, match.Politics.PilgrimageTradeMultiplier(from, (to + 1) % match.Economy.SettlementCount));
    }

    [Fact]
    public void Cathedral_factor_applies_only_to_the_direction_that_ended_at_the_cathedral_and_claim_gain_tracks_traveled_tiles()
    {
        (MatchModel match, SimRunner runner) = Start();
        Grant(runner, match, 0, ReadyProjects);
        (int from, int to) = ConnectTwoOwnTowns(match);
        match.Economy.FixtureCompleteIron(to, "gothic_cathedral");
        var settlements = new List<int>();
        var tiles = new List<int>();
        Assert.True(match.Economy.TryPilgrimageRoadPath(0, from, to, settlements, tiles));
        int claimTile = tiles[^1];
        Fix64 ClaimFor(PoliticsSystem p) => p.FixturePilgrimageClaimSources()
            .Where(s => s.Civ == 0 && s.Tile == claimTile && s.Radius == 0).Aggregate(Fix64.Zero, (sum, s) => sum + s.Emission);
        Fix64 before = ClaimFor(match.Politics);
        match.Economy.GiveGoods(from, Fix64.FromInt(1000));
        match.Politics.Step(runner.Tick, new[] { new Command(runner.Tick, 0, CommandKind.Pilgrimage, from, to, 0) });

        Assert.InRange((match.Politics.PilgrimageTradeMultiplier(from, to) - Fix64.FromRatio(1265, 1000)).Raw, -2L, 2L);
        Assert.Equal(Fix64.FromRatio(115, 100), match.Politics.PilgrimageTradeMultiplier(to, from));
        Fix64 after = ClaimFor(match.Politics);
        Assert.InRange((after / before - Fix64.FromRatio(3, 2)).Raw, -8L, 8L); // Existing road +10%, pilgrimage adds 5% on its traveled tiles.

        runner.Advance(150); // End-of-season clears the transient completed-trip marker.
        Assert.Equal(Fix64.One, match.Politics.PilgrimageTradeMultiplier(from, to));
    }

    [Fact]
    public void Pilgrimage_action_and_path_are_inert_outside_Fief_or_before_both_prerequisites()
    {
        (MatchModel fief, SimRunner runner) = Start();
        (int from, int to) = ConnectTwoOwnTowns(fief);
        Assert.False(fief.Economy.TryPilgrimageRoadPath(0, from, to, new List<int>(), new List<int>()));
        Assert.False(fief.Politics.OrderPilgrimage(0, from, to));

        (MatchModel iron, _) = Start(ChapterRules.Iron);
        int ironFrom = Enumerable.Range(0, iron.Economy.SettlementCount).First(i => iron.Economy.SettlementLite(i).Civilization == 0);
        int ironTo = Enumerable.Range(0, iron.Economy.SettlementCount).First(i => iron.Economy.SettlementLite(i).Civilization == 1);
        Assert.False(iron.Economy.TryPilgrimageRoadPath(0, ironFrom, ironTo, new List<int>(), new List<int>()));
        Assert.False(iron.Politics.OrderPilgrimage(0, ironFrom, ironTo));

        (MatchModel bronze, _) = Start(ChapterRules.Bronze);
        int bronzeFrom = Enumerable.Range(0, bronze.Economy.SettlementCount).First(i => bronze.Economy.SettlementLite(i).Civilization == 0);
        int bronzeTo = Enumerable.Range(0, bronze.Economy.SettlementCount).First(i => bronze.Economy.SettlementLite(i).Civilization == 1);
        Assert.False(bronze.Politics.OrderPilgrimage(0, bronzeFrom, bronzeTo));
    }

    [Fact]
    public void Completed_pilgrimage_marker_survives_Fief_snapshot_and_clears_at_season_end()
    {
        (MatchModel match, SimRunner runner) = Start();
        Grant(runner, match, 0, ReadyProjects);
        (int from, int to) = ConnectTwoOwnTowns(match);
        match.Economy.GiveGoods(from, Fix64.FromInt(1000));
        Assert.True(match.Politics.OrderPilgrimage(0, from, to));
        Assert.Equal(Fix64.FromRatio(115, 100), match.Politics.PilgrimageTradeMultiplier(from, to));

        (MatchModel restored, _) = Start();
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) match.WriteSnapshot(writer);
        stream.Position = 0;
        using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true)) restored.ReadSnapshot(reader);
        var restoredRunner = new SimRunner(restored, startTick: runner.Tick);

        Assert.Equal(match.ComputeChecksum(), restored.ComputeChecksum());
        Assert.Equal(Fix64.FromRatio(115, 100), restored.Politics.PilgrimageTradeMultiplier(from, to));
        runner.Advance(150);
        restoredRunner.Advance(150);
        Assert.Equal(Fix64.One, restored.Politics.PilgrimageTradeMultiplier(from, to));
        Assert.Equal(match.ComputeChecksum(), restored.ComputeChecksum());
    }
}
