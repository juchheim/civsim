using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

public sealed class FiefSeaSpeedTests
{
    private const ulong Seed = 20261019;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric("WAY-09", "SEA-11", "KEY-6"));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner, int Home) Start(ChapterRules? rules = null)
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules ?? ChapterRules.Fief);
        var runner = new SimRunner(match);
        int home = Enumerable.Range(0, match.Economy.SettlementCount)
            .First(s => match.Economy.SettlementLite(s).Civilization == 0);
        return (match, runner, match.Economy.SettlementLite(home).SiteTile);
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

    [Fact]
    public void Sea11_composes_with_Way09_before_rounding_and_changes_only_Fief_sea_routes()
    {
        (MatchModel match, SimRunner runner, int home) = Start();
        Grant(runner, match, 0, "KEY-5", "SEA-04", "KEY-6");
        PoliticsSystem politics = match.Politics;
        int seaDestination = Enumerable.Range(0, World.Value.TileCount)
            .Where(t => World.Value.TileAt(t).IsLand && politics.SeaTilesOnRoute(0, home, t) > 0)
            .OrderByDescending(t => politics.TravelSeasons(0, home, t))
            .First();
        int baseSeasons = politics.TravelSeasons(0, home, seaDestination);
        Assert.True(baseSeasons >= 4, "fixture must expose the distinction between combined and staged rounding");

        int landDestination = Enumerable.Range(0, World.Value.TileCount)
            .Where(t => politics.SeaTilesOnRoute(0, home, t) == 0)
            .OrderByDescending(t => politics.TravelSeasons(0, home, t))
            .First();
        int landSeasons = politics.TravelSeasons(0, home, landDestination);
        Grant(runner, match, 0, "WAY-09", "SEA-11");

        int expected = Math.Max(1, (baseSeasons * 100 + 181) / 182);
        Assert.Equal(expected, politics.TravelSeasons(0, home, seaDestination));
        Assert.Equal(landSeasons, politics.TravelSeasons(0, home, landDestination));

        (MatchModel iron, SimRunner ironRunner, int ironHome) = Start(ChapterRules.Iron);
        Grant(ironRunner, iron, 0, "KEY-4", "SEA-04");
        int ironDestination = Enumerable.Range(0, World.Value.TileCount)
            .First(t => World.Value.TileAt(t).IsLand && iron.Politics.SeaTilesOnRoute(0, ironHome, t) > 0);
        int ironBefore = iron.Politics.TravelSeasons(0, ironHome, ironDestination);
        Grant(ironRunner, iron, 0, "WAY-09", "SEA-11");
        Assert.Equal(ironBefore, iron.Politics.TravelSeasons(0, ironHome, ironDestination));
    }
}
