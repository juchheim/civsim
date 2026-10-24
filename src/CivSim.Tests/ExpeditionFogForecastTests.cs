using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CivSim.Sim.Ai;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.World;
using Xunit;

namespace CivSim.Tests;

public sealed class ExpeditionFogForecastTests
{
    private const ulong Seed = 20261023;

    [Fact]
    public void Expedition_preview_does_not_change_when_unseen_route_passability_changes()
    {
        var world = WorldGenerator.Generate(Seed);
        var rig = new StrategistRig(Seed, world,
            new[] { CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3");
        rig.Seasons(12);
        int home = rig.SettlementsOf(0)[0], origin = rig.Politics.SettlementTileOf(home);
        int target = Enumerable.Range(0, world.Tiles.Length).First(t =>
            HexGrid.Distance(origin, t, world.Width) is >= 1 and <= 3 && rig.Politics.CanReach(0, origin, t));

        DecreeForecast before = DecreeForecaster.Forecast(rig.Politics, 0, DecreeKind.Expedition, target, 0);
        bool[] passable = (bool[])typeof(PoliticsSystem).GetField("_passable", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(rig.Politics)!;
        bool[] saved = (bool[])passable.Clone();
        System.Array.Clear(passable);
        foreach (string fieldName in new[] { "_walkFrom", "_sailFrom" })
            System.Array.Clear((System.Array)typeof(PoliticsSystem).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(rig.Politics)!);
        Assert.False(rig.Politics.CanReach(0, origin, target));
        DecreeForecast after = DecreeForecaster.Forecast(rig.Politics, 0, DecreeKind.Expedition, target, 0);
        System.Array.Copy(saved, passable, passable.Length);

        Assert.Equal(before.Allowed, after.Allowed);
        Assert.Equal(before.LikelyRefusal, after.LikelyRefusal);
        Assert.Equal(before.Seasons, after.Seasons);
        Assert.Equal(before.SuccessPermille, after.SuccessPermille);
    }

    [Fact]
    public void Strategist_expedition_candidates_do_not_change_with_unseen_passability()
    {
        var world = WorldGenerator.Generate(Seed);
        var rig = new StrategistRig(Seed, world,
            new[] { CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3");
        rig.Seasons(12);
        Belief belief = new(0, 4, TestPack.Catalog.Count,
            new MapKnowledge(world.Tiles.ToArray(), world.Width, world.Height)) { Season = rig.Season };
        var research = typeof(PoliticsSystem).GetField("_research", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(rig.Politics)!;
        Array civResearch = (Array)research.GetType().GetField("_civs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(research)!;
        object civState = civResearch.GetValue(0)!;
        int cap = (int)civState.GetType().GetField("MandateCap")!.GetValue(civState)!;
        civState.GetType().GetField("Mandate")!.SetValue(civState, cap);
        var before = new List<DecreeRequest>();
        StrategistDecreePolicy.AddBeliefExpeditionAlternatives(rig.Politics, 0, belief, before);
        Assert.NotEmpty(before);

        bool[] passable = (bool[])typeof(PoliticsSystem).GetField("_passable", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(rig.Politics)!;
        bool[] saved = (bool[])passable.Clone();
        System.Array.Clear(passable);
        var after = new List<DecreeRequest>();
        StrategistDecreePolicy.AddBeliefExpeditionAlternatives(rig.Politics, 0, belief, after);
        System.Array.Copy(saved, passable, passable.Length);

        Assert.Equal(before.Select(x => (x.Kind, x.Target, x.Extra)), after.Select(x => (x.Kind, x.Target, x.Extra)));
    }

    [Fact]
    public void Levy_preview_does_not_change_when_unseen_route_passability_changes()
    {
        var world = WorldGenerator.Generate(Seed);
        var rig = new StrategistRig(Seed, world,
            new[] { CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3");
        rig.Seasons(12);
        int home = rig.SettlementsOf(0)[0], origin = rig.Politics.SettlementTileOf(home);
        int target = Enumerable.Range(0, rig.Politics.SettlementTotal)
            .Where(s => rig.Politics.SettlementCivOf(s) == 1)
            .OrderBy(s => HexGrid.Distance(origin, rig.Politics.SettlementTileOf(s), world.Width)).First();
        int targetTile = rig.Politics.SettlementTileOf(target);
        var belief = new Belief(0, 4, TestPack.Catalog.Count,
            new MapKnowledge(world.Tiles.ToArray(), world.Width, world.Height)) { Season = rig.Season };
        belief.Rivals[1].Settlements.Add(new BeliefSettlement
        {
            Settlement = target, Tile = targetTile, LastSeenSeason = rig.Season, PopulationEstimate = 24,
            FightersEstimate = 8, DefendingStrengthEstimate = CivSim.Sim.Numerics.Fix64.FromInt(16),
        });

        DecreeForecast before = DecreeForecaster.Forecast(rig.Politics, 0, DecreeKind.Levy, target, 0, belief);
        bool[] passable = (bool[])typeof(PoliticsSystem).GetField("_passable", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(rig.Politics)!;
        bool[] saved = (bool[])passable.Clone();
        System.Array.Clear(passable);
        foreach (string fieldName in new[] { "_walkFrom", "_sailFrom" })
            System.Array.Clear((System.Array)typeof(PoliticsSystem).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(rig.Politics)!);
        DecreeForecast after = DecreeForecaster.Forecast(rig.Politics, 0, DecreeKind.Levy, target, 0, belief);
        System.Array.Copy(saved, passable, passable.Length);

        Assert.Equal(before.Allowed, after.Allowed);
        Assert.Equal(before.LikelyRefusal, after.LikelyRefusal);
        Assert.Equal(before.People, after.People);
        Assert.Equal(before.SuccessPermille, after.SuccessPermille);
    }
}
