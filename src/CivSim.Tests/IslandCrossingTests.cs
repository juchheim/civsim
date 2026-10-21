using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Island crossing (docs/MVP/21): outrigger voyages reach the islands, walkers do not, and nothing else moves.</summary>
public class IslandCrossingTests
{
    private sealed class Landmasses
    {
        public readonly int[] Id;
        public readonly int[] Size;
        public Landmasses(GeneratedWorld world)
        {
            WorldTile[] tiles = world.Tiles.ToArray();
            Id = new int[tiles.Length];
            var sizes = new List<int> { 0 };
            var nb = new int[6];
            for (int t = 0; t < tiles.Length; t++)
            {
                if (!tiles[t].IsLand || Id[t] != 0) continue;
                int id = sizes.Count, size = 0;
                var queue = new Queue<int>();
                queue.Enqueue(t); Id[t] = id;
                while (queue.Count > 0)
                {
                    int a = queue.Dequeue(); size++;
                    int count = HexGrid.Neighbors(a, world.Width, world.Height, nb);
                    for (int i = 0; i < count; i++)
                        if (tiles[nb[i]].IsLand && Id[nb[i]] == 0) { Id[nb[i]] = id; queue.Enqueue(nb[i]); }
                }
                sizes.Add(size);
            }
            Size = sizes.ToArray();
        }
        public bool IsIsland(int tile) => Id[tile] > 0 && Size[Id[tile]] <= PoliticsSystem.IslandMaxTiles;
    }

    private static (MatchModel Match, SimRunner Runner, int Home, Landmasses Land) Start()
    {
        MatchModel match = ViewKit.NewMatch();
        var runner = new SimRunner(match);
        runner.Advance(10);
        int home = -1;
        for (int s = 0; s < match.Economy.SettlementCount && home < 0; s++)
            if (match.Economy.SettlementLite(s).Civilization == 0) home = match.Economy.SettlementLite(s).SiteTile;
        return (match, runner, home, new Landmasses(ViewKit.World.Value));
    }

    private static void Learn(MatchModel match, SimRunner runner, int civ, string project)
    {
        runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, match.Research.Catalog.IndexOf(project), 0, 0));
        runner.Advance(2);
    }

    [Fact]
    public void Islands_are_reachable_only_by_outrigger_and_the_other_continent_never()
    {
        var (match, runner, home, land) = Start();
        PoliticsSystem pol = match.Politics;
        int[] islands = Enumerable.Range(0, land.Id.Length).Where(land.IsIsland).ToArray();
        Assert.NotEmpty(islands);
        int other = Enumerable.Range(0, land.Id.Length).First(t => land.Id[t] > 0 && land.Id[t] != land.Id[home] && land.Size[land.Id[t]] > 100);

        foreach (int tile in islands) Assert.False(pol.CanReach(0, home, tile), "walkers must not reach an island");
        Assert.False(pol.CanReach(0, home, other));
        Assert.Equal(PoliticsSystem.LandSettleReach, pol.SettleReach(0));

        Learn(match, runner, 0, "SEA-04");
        Assert.True(pol.CanSail(0));
        Assert.Equal(PoliticsSystem.SailSettleReach, pol.SettleReach(0));
        Assert.Contains(islands, tile => pol.CanReach(0, home, tile));
        Assert.False(pol.CanReach(0, home, other), "the 33-tile gap between continents is out of reach until Sail");
        foreach (int tile in islands)
            if (pol.CanReach(0, home, tile)) Assert.True(pol.SeaTilesOnRoute(0, home, tile) > 0);
    }

    [Fact]
    public void Routes_that_can_be_walked_keep_their_length_with_or_without_boats()
    {
        var (match, runner, home, land) = Start();
        PoliticsSystem pol = match.Politics;
        int[] same = Enumerable.Range(0, land.Id.Length).Where(t => land.Id[t] == land.Id[home]).Where((_, i) => i % 97 == 0).ToArray();
        int[] before = same.Select(t => pol.TravelSeasons(0, home, t)).ToArray();
        Learn(match, runner, 0, "SEA-04");
        int[] after = same.Select(t => pol.TravelSeasons(0, home, t)).ToArray();
        Assert.Equal(before, after);
        foreach (int tile in same) Assert.Equal(0, pol.SeaTilesOnRoute(0, home, tile));
    }

    [Fact]
    public void Shipwreck_risk_follows_the_formula_and_each_roll_is_reproducible()
    {
        var (match, runner, home, land) = Start();
        Learn(match, runner, 0, "SEA-04");
        PoliticsSystem pol = match.Politics;
        int island = Enumerable.Range(0, land.Id.Length).First(t => land.IsIsland(t) && pol.CanReach(0, home, t));
        int sea = pol.SeaTilesOnRoute(0, home, island);
        int risk = pol.VoyageRiskPermille(0, home, island);
        Assert.Equal(Math.Min(PoliticsSystem.VoyageMaxRiskPermille, PoliticsSystem.VoyageBaseRiskPermille + PoliticsSystem.VoyageRiskPerSeaTile * sea), risk);
        Assert.InRange(risk, 1, PoliticsSystem.VoyageMaxRiskPermille);

        int lost = 0;
        const int rolls = 4000;
        for (int season = 0; season < rolls; season++)
        {
            bool first = pol.VoyageLost(0, home, island, season);
            Assert.Equal(first, pol.VoyageLost(0, home, island, season));
            if (first) lost++;
        }
        double rate = lost * 1000.0 / rolls;
        Assert.InRange(rate, risk - 40, risk + 40);
        // walking trips never roll
        int mainland = Enumerable.Range(0, land.Id.Length).First(t => land.Id[t] == land.Id[home] && t != home);
        Assert.False(pol.VoyageLost(0, home, mainland, 1));
        Assert.Equal(0, pol.VoyageRiskPermille(0, home, mainland));
    }

    [Fact]
    public void Nobody_settles_an_island_without_the_outrigger()
    {
        WatchedMatch watched = ViewKit.Watched.Value;
        var land = new Landmasses(ViewKit.World.Value);
        PoliticsSystem pol = watched.Match.Politics;
        var frame = watched.OmniFrame;
        for (int i = 0; i < frame.SettlementCount; i++)
        {
            if (frame.SettlementPopulation[i] <= 0 || frame.SettlementCiv[i] < 0) continue;
            if (land.IsIsland(frame.SettlementTile[i])) Assert.True(pol.CanSail(frame.SettlementCiv[i]), "an island village needs a civilization that sails");
        }
    }
}
