using System;
using System.Linq;
using System.Reflection;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.World;
using Xunit;

namespace CivSim.Tests;

public sealed class DecreeForecasterMineBeliefTests
{
    private const ulong Seed = 20261023;

    [Fact]
    public void Ai_mine_forecast_uses_recent_owner_sighting_instead_of_live_territory()
    {
        var world = WorldGenerator.Generate(Seed);
        var rig = new StrategistRig(Seed, world,
            new[] { CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3", "MET-03", "MET-04", "MET-05", "MET-07", "MET-08", "MIN-04", "MIN-06");
        int home = rig.SettlementsOf(0)[0];
        int homeTile = rig.Politics.SettlementTileOf(home);
        int tile = Enumerable.Range(0, world.Tiles.Length).First(t =>
            world.Tiles[t].Deposit is DepositKind.CopperOre or DepositKind.ArsenicalCopper or DepositKind.Tin or DepositKind.Gold or
                DepositKind.Silver or DepositKind.Salt or DepositKind.Iron &&
            HexGrid.Distance(homeTile, t, world.Width) <= rig.Politics.Economy.MiningReachFrom(home));

        var belief = NewBelief(world, rig.Season, tile, owner: -1);
        SetTerritoryOwner(rig.Politics.Territory, tile, 1);

        DecreeForecast ai = DecreeForecaster.Forecast(rig.Politics, 0, DecreeKind.Mine, tile, 0, belief);
        DecreeForecast omniscient = DecreeForecaster.Forecast(rig.Politics, 0, DecreeKind.Mine, tile, 0);

        Assert.True(ai.Allowed, ai.LikelyRefusal.ToString());
        Assert.True(omniscient.Allowed, omniscient.LikelyRefusal.ToString());
    }

    [Fact]
    public void Ai_mine_forecast_does_not_read_live_ore_remaining()
    {
        var world = WorldGenerator.Generate(Seed);
        var rig = new StrategistRig(Seed, world,
            new[] { CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3", "MET-03", "MET-04", "MET-05", "MET-07", "MET-08", "MIN-04", "MIN-06");
        int home = rig.SettlementsOf(0)[0];
        int homeTile = rig.Politics.SettlementTileOf(home);
        int tile = Enumerable.Range(0, world.Tiles.Length).First(t =>
            world.Tiles[t].Deposit is DepositKind.CopperOre or DepositKind.ArsenicalCopper or DepositKind.Tin or DepositKind.Gold or
                DepositKind.Silver or DepositKind.Salt or DepositKind.Iron &&
            HexGrid.Distance(homeTile, t, world.Width) <= rig.Politics.Economy.MiningReachFrom(home));
        var belief = NewBelief(world, rig.Season, tile, owner: rig.Politics.Territory.OwnerOf(tile));

        SetOreRemaining(rig.Match.Economy, tile, 0);

        DecreeForecast ai = DecreeForecaster.Forecast(rig.Politics, 0, DecreeKind.Mine, tile, 0, belief);
        DecreeForecast omniscient = DecreeForecaster.Forecast(rig.Politics, 0, DecreeKind.Mine, tile, 0);

        Assert.True(ai.Allowed, ai.LikelyRefusal.ToString());
        Assert.True(omniscient.Allowed, omniscient.LikelyRefusal.ToString());
    }

    private static Belief NewBelief(GeneratedWorld world, int season, int tile, int owner)
    {
        var belief = new Belief(0, 4, TestPack.Catalog.Count,
            new MapKnowledge(world.Tiles.ToArray(), world.Width, world.Height)) { Season = season };
        belief.Map.MarkExplored(tile);
        belief.Map.RecordOwner(tile, owner, season);
        return belief;
    }

    private static void SetTerritoryOwner(Territory territory, int tile, sbyte owner)
    {
        var owners = (sbyte[])typeof(Territory).GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(territory)!;
        owners[tile] = owner;
    }

    private static void SetOreRemaining(object economy, int tile, int amount)
    {
        var ore = (CivSim.Sim.Numerics.Fix64[])economy.GetType()
            .GetField("_ore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(economy)!;
        ore[tile] = CivSim.Sim.Numerics.Fix64.FromInt(amount);
    }
}
