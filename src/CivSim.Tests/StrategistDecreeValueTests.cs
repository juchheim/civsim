using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using CivSim.Sim.Ai;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.World;
using Xunit;

namespace CivSim.Tests;

public sealed class StrategistDecreeValueTests
{
    private const ulong Seed = 20261023;

    [Fact]
    public void Found_value_uses_known_unclaimed_land_and_estimated_dominion()
    {
        GeneratedWorld world = WorldGenerator.Generate(Seed);
        var rig = new StrategistRig(Seed, world,
            new[] { CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3");
        rig.Seasons(8);
        Belief belief = new(0, 4, TestPack.Catalog.Count, new MapKnowledge(world.Tiles.ToArray(), world.Width, world.Height)) { Season = rig.Season };
        for (int tile = 0; tile < world.Tiles.Length; tile++)
        {
            belief.Map.MarkExplored(tile);
            belief.Map.RecordOwner(tile, rig.Politics.Territory.OwnerOf(tile), belief.Season);
        }
        int radius = rig.Politics.ClaimRadiusOfEra(rig.Politics.DecreeResearch.Era(0));
        int home = rig.Politics.SettlementTileOf(rig.SettlementsOf(0)[0]);
        int center = Enumerable.Range(0, world.Tiles.Length).First(t => world.Tiles[t].IsLand &&
            rig.Politics.Territory.OwnerOf(t) < 0 && HexGrid.Distance(home, t, world.Width) is >= 3 and <= 8);
        var (openTiles, openWeight, openDominion) = DecreeForecaster.FoundLandValue(rig.Politics, 0, center, radius, belief);
        Assert.True(openTiles > 0);
        Assert.True(openWeight > CivSim.Sim.Numerics.Fix64.Zero);
        Assert.True(openDominion > CivSim.Sim.Numerics.Fix64.Zero);

        HexGrid.Coordinates(center, world.Width, out int cx, out int cy);
        for (int y = Math.Max(0, cy - radius); y <= Math.Min(world.Height - 1, cy + radius); y++)
        for (int x = Math.Max(0, cx - radius - 1); x <= Math.Min(world.Width - 1, cx + radius + 1); x++)
        {
            int tile = y * world.Width + x;
            if (HexGrid.Distance(center, tile, world.Width) <= radius)
                belief.Map.RecordOwner(tile, 0, belief.Season);
        }
        var (claimedTiles, claimedWeight, claimedDominion) = DecreeForecaster.FoundLandValue(rig.Politics, 0, center, radius, belief);
        Assert.True(claimedWeight < openWeight);
        Assert.True(claimedDominion < openDominion);
        var open = new DecreeForecast(DecreeKind.Found, true, 0, 1, 1, 1000, 20, openTiles, default, 0, 1,
            openWeight, openDominion);
        var claimed = new DecreeForecast(DecreeKind.Found, true, 0, 1, 1, 1000, 20, claimedTiles, default, 0, 1,
            claimedWeight, claimedDominion);
        Assert.True(StrategistDecreePolicy.ForecastBenefit(DecreeKind.Found, open) >
                    StrategistDecreePolicy.ForecastBenefit(DecreeKind.Found, claimed));
    }

    [Fact]
    public void Expedition_value_weights_known_frontier_and_ignores_hidden_deposits()
    {
        GeneratedWorld world = WorldGenerator.Generate(Seed);
        var rig = new StrategistRig(Seed, world,
            new[] { CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3");
        rig.Seasons(8);
        int origin = rig.Politics.SettlementTileOf(rig.SettlementsOf(0)[0]);
        var belief = new Belief(0, 4, TestPack.Catalog.Count, new MapKnowledge(world.Tiles.ToArray(), world.Width, world.Height)) { Season = rig.Season };
        DecreeForecast hidden = DecreeForecaster.Forecast(rig.Politics, 0, DecreeKind.Expedition, origin, 0, belief);
        Assert.True(hidden.Allowed);
        Assert.True(hidden.Information >= CivSim.Sim.Numerics.Fix64.FromInt(hidden.Tiles));

        HexGrid.Coordinates(origin, world.Width, out int cx, out int cy);
        for (int y = Math.Max(0, cy - 5); y <= Math.Min(world.Height - 1, cy + 5); y++)
        for (int x = Math.Max(0, cx - 6); x <= Math.Min(world.Width - 1, cx + 6); x++)
        {
            int tile = y * world.Width + x;
            if (HexGrid.Distance(origin, tile, world.Width) <= 5) belief.Map.MarkExplored(tile);
        }
        DecreeForecast explored = DecreeForecaster.Forecast(rig.Politics, 0, DecreeKind.Expedition, origin, 0, belief);
        Assert.Equal(0, explored.Tiles);
        Assert.Equal(CivSim.Sim.Numerics.Fix64.Zero, explored.Information);
        Assert.True(StrategistDecreePolicy.ForecastBenefit(DecreeKind.Expedition, hidden) >
                    StrategistDecreePolicy.ForecastBenefit(DecreeKind.Expedition, explored));
    }

    [Fact]
    public void Mine_value_depends_on_the_alloy_route_and_annual_demand()
    {
        GeneratedWorld world = WorldGenerator.Generate(Seed);
        var rig = new StrategistRig(Seed, world,
            new[] { CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3", "MET-04", "MET-05", "MIN-04");
        rig.Seasons(8);
        int tin = Enumerable.Range(0, world.Tiles.Length).First(t => world.Tiles[t].Deposit == DepositKind.Tin);
        int arsenical = Enumerable.Range(0, world.Tiles.Length).First(t => world.Tiles[t].Deposit == DepositKind.ArsenicalCopper);
        var tinValue = DecreeForecaster.EstimateMineValue(rig.Politics, 0, tin, world.Tiles[tin]);
        var arsenicalValue = DecreeForecaster.EstimateMineValue(rig.Politics, 0, arsenical, world.Tiles[arsenical]);

        Assert.True(tinValue.Value > CivSim.Sim.Numerics.Fix64.Zero);
        Assert.True(arsenicalValue.Value > tinValue.Value);
        Assert.True(tinValue.AnnualOutput > CivSim.Sim.Numerics.Fix64.Zero);
        Assert.True(arsenicalValue.AnnualOutput > CivSim.Sim.Numerics.Fix64.Zero);
    }

    [Fact]
    public void Wall_corvee_uses_completion_margin_to_a_belief_supported_threat()
    {
        GeneratedWorld world = WorldGenerator.Generate(Seed);
        var rig = new StrategistRig(Seed, world,
            new[] { CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3", "DEF-04", "DEF-05");
        rig.Seasons(8);
        int own = rig.SettlementsOf(0)[0];
        object states = typeof(LivingEconomyModel).GetField("_settlements", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(rig.Match.Economy)!;
        object state = ((IList)states)[own]!;
        Type stateType = state.GetType();
        int walls = LivingEconomyModel.StructureIndex("stone_walls");
        stateType.GetField("BuildTarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(state, Enum.ToObject(stateType.GetField("BuildTarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.FieldType, walls));
        stateType.GetField("BuildProgress", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(state, Fix64.FromInt(24));
        int ownTile = rig.Politics.SettlementTileOf(own);
        int close = Enumerable.Range(0, world.Tiles.Length).First(t => HexGrid.Distance(ownTile, t, world.Width) == 1);
        int far = Enumerable.Range(0, world.Tiles.Length).First(t => HexGrid.Distance(ownTile, t, world.Width) >= 30);
        var belief = new Belief(0, 4, TestPack.Catalog.Count,
            new MapKnowledge(world.Tiles.ToArray(), world.Width, world.Height)) { Season = rig.Season };
        RivalBelief rival = belief.Rivals[1];
        rival.AtWar = true;
        rival.Settlements.Add(new BeliefSettlement { Settlement = 1, Tile = close, LastSeenSeason = rig.Season, PopulationEstimate = 30 });
        var nearTiming = DecreeForecaster.EstimateWallThreatTiming(rig.Politics, 0, own, belief);
        rival.Settlements[0].Tile = far;
        var farTiming = DecreeForecaster.EstimateWallThreatTiming(rig.Politics, 0, own, belief);

        Assert.Equal(1, nearTiming.ThreatEta);
        Assert.True(nearTiming.Margin < CivSim.Sim.Numerics.Fix64.Zero);
        Assert.True(farTiming.ThreatEta > nearTiming.ThreatEta);
        Assert.True(farTiming.Margin > nearTiming.Margin);
        Assert.True(farTiming.Margin > CivSim.Sim.Numerics.Fix64.Zero);
    }
}
