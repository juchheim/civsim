using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>SEA-12 ocean reach and WAY-10 the chronometer (M13 WP-E6-6 slice 1, docs/MVP/38 §5.2). Discovery chapter only; the voyage machinery is the existing one.</summary>
public sealed class DiscoveryOceanTests
{
    private const ulong Seed = 20261020;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack =
        new(() => TestPack.WithNumeric("WAY-08", "WAY-09", "KEY-6", "SEA-11", "SEA-12", "WAY-10"));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner, int Home) Start(ChapterRules rules)
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules);
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

    private static IEnumerable<int> LandTiles() => Enumerable.Range(0, World.Value.TileCount).Where(t => World.Value.TileAt(t).IsLand);

    [Fact]
    public void SEA12_opens_the_deep_ocean_in_Discovery_and_a_crossing_that_needed_islands_goes_in_one_leg()
    {
        (MatchModel match, SimRunner runner, int home) = Start(ChapterRules.Discovery);
        Grant(runner, match, 0, "KEY-5", "SEA-04", "KEY-6");
        PoliticsSystem politics = match.Politics;
        Assert.False(politics.HasOceanReach(0));

        int[] before = LandTiles().Select(t => politics.DeepSeaTilesOnRoute(0, home, t)).ToArray();
        Assert.All(before, deep => Assert.True(deep <= 0, "outriggers never cross a tile farther than 11 from land"));
        int[] timeBefore = LandTiles().Select(t => politics.TravelSeasons(0, home, t)).ToArray();

        Grant(runner, match, 0, "SEA-12");
        Assert.True(politics.HasOceanReach(0));
        Assert.False(politics.HasOceanReach(1), "reach is a civilization's own project");
        int[] after = LandTiles().Select(t => politics.DeepSeaTilesOnRoute(0, home, t)).ToArray();
        int[] timeAfter = LandTiles().Select(t => politics.TravelSeasons(0, home, t)).ToArray();

        Assert.Contains(after, deep => deep > 0);
        for (int i = 0; i < timeBefore.Length; i++)
            Assert.True(timeAfter[i] <= timeBefore[i], "a wider reach never makes a route longer");
        Assert.Contains(Enumerable.Range(0, timeBefore.Length), i => timeAfter[i] < timeBefore[i]);
    }

    [Fact]
    public void Deep_ocean_tiles_raise_the_loss_roll_and_WAY10_cuts_it_to_three_tenths()
    {
        (MatchModel match, SimRunner runner, int home) = Start(ChapterRules.Discovery);
        Grant(runner, match, 0, "KEY-5", "SEA-04", "KEY-6", "SEA-12");
        PoliticsSystem politics = match.Politics;
        int destination = LandTiles().Where(t => politics.DeepSeaTilesOnRoute(0, home, t) > 0).OrderBy(t => politics.SeaTilesOnRoute(0, home, t)).First();
        int sea = politics.SeaTilesOnRoute(0, home, destination);
        int deep = politics.DeepSeaTilesOnRoute(0, home, destination);
        int baseRisk = Math.Min(PoliticsSystem.VoyageMaxRiskPermille,
            PoliticsSystem.VoyageBaseRiskPermille + PoliticsSystem.VoyageRiskPerSeaTile * sea + PoliticsSystem.OceanDeepRiskPerTile * deep);
        Assert.Equal(baseRisk, politics.VoyageRiskPermille(0, home, destination));
        int coastOnly = Math.Min(PoliticsSystem.VoyageMaxRiskPermille, PoliticsSystem.VoyageBaseRiskPermille + PoliticsSystem.VoyageRiskPerSeaTile * sea);
        Assert.True(baseRisk >= coastOnly, "deep water never costs less than coast water");
        if (coastOnly < PoliticsSystem.VoyageMaxRiskPermille) Assert.True(baseRisk > coastOnly, "deep water costs more than coast water");

        Grant(runner, match, 0, "WAY-10");
        Assert.Equal(baseRisk * PoliticsSystem.ChronometerRiskTenths / 10, politics.VoyageRiskPermille(0, home, destination));

        Grant(runner, match, 0, "WAY-08");
        Assert.True(baseRisk / 2 * PoliticsSystem.ChronometerRiskTenths / 10 == politics.VoyageRiskPermille(0, home, destination), "WAY-08 halves, then WAY-10 takes three tenths");

        const int samples = 2000;
        for (int season = 0; season < samples; season++)
            Assert.Equal(politics.VoyageLost(0, home, destination, season), politics.VoyageLost(0, home, destination, season));
    }

    [Fact]
    public void Walking_routes_and_other_civilizations_are_untouched_by_SEA12()
    {
        (MatchModel match, SimRunner runner, int home) = Start(ChapterRules.Discovery);
        Grant(runner, match, 0, "KEY-5", "SEA-04", "KEY-6");
        PoliticsSystem politics = match.Politics;
        int mainland = LandTiles().First(t => t != home && politics.SeaTilesOnRoute(0, home, t) == 0);
        int destination = LandTiles().First(t => politics.SeaTilesOnRoute(0, home, t) > 0);
        int risk = politics.VoyageRiskPermille(0, home, destination);
        int seasons = politics.TravelSeasons(0, home, mainland);

        Grant(runner, match, 0, "SEA-12", "WAY-10");

        Assert.Equal(0, politics.VoyageRiskPermille(0, home, mainland));
        Assert.Equal(seasons, politics.TravelSeasons(0, home, mainland));
        Assert.Equal(0, politics.DeepSeaTilesOnRoute(0, home, mainland));
        Assert.True(politics.VoyageRiskPermille(0, home, destination) <= risk * PoliticsSystem.ChronometerRiskTenths / 10 + PoliticsSystem.OceanDeepRiskPerTile * 40,
            "the chronometer cuts what the route already risked");
    }

    [Fact]
    public void SEA12_and_WAY10_do_nothing_in_Fief()
    {
        (MatchModel match, SimRunner runner, int home) = Start(ChapterRules.Fief);
        Grant(runner, match, 0, "KEY-5", "SEA-04", "KEY-6");
        PoliticsSystem politics = match.Politics;
        int destination = LandTiles().First(t => politics.SeaTilesOnRoute(0, home, t) > 0);
        int risk = politics.VoyageRiskPermille(0, home, destination);
        int seasons = politics.TravelSeasons(0, home, destination);

        Grant(runner, match, 0, "SEA-12", "WAY-10"); // the Fief ceiling refuses these era-six completions

        Assert.False(politics.HasOceanReach(0));
        Assert.Equal(risk, politics.VoyageRiskPermille(0, home, destination));
        Assert.Equal(seasons, politics.TravelSeasons(0, home, destination));
    }
}
