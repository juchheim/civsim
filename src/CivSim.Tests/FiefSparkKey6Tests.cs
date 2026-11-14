using System;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Ai.Forecast;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Fief Spark routes ready KEY-6; the Council prior and forecast also include the ready keystone.</summary>
public sealed class FiefSparkKey6Tests
{
    private const ulong Seed = 20261114;
    private static readonly Lazy<ContentPack> PromotedFiefPack = new(() =>
        TestPack.WithNumeric(TestPack.Catalog.Projects
            .Where(p => p.Era == ChapterRules.Fief.LastResearchEra && !p.Numeric)
            .Select(p => p.Id)
            .ToArray()));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    [Fact]
    public void Fief_Spark_routes_ready_KEY6_and_Council_prior_and_forecast_include_it()
    {
        ContentPack pack = PromotedFiefPack.Value;
        ResearchCatalog catalog = new(pack);
        int key5 = catalog.IndexOf("KEY-5"), key6 = catalog.IndexOf("KEY-6");
        Assert.True(key5 >= 0 && key6 >= 0);
        Assert.True(catalog.Projects[key6].Numeric, "the fixture promotes E5 rows only for this test");

        CivControl[] controls =
        {
            CivControl.Council,
            CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced),
            CivControl.Council,
            CivControl.Council,
        };
        MatchModel match = MatchModel.Create(Seed, pack, World.Value, Balanced, controls, rules: ChapterRules.Fief);
        var runner = new SimRunner(match);

        // Disable fallback Council picks for the Warlord seat so the event-driven Spark path is observable on its own.
        runner.Submit(new Command(0, 1, CommandKind.SetCouncil, 0, 0, 0));
        foreach (int civ in new[] { 0, 1 })
        {
            runner.Submit(new Command(0, (byte)civ, CommandKind.CompleteProject, key5, 0, 0));
            foreach (ProjectDef project in catalog.Projects)
                if (project.Era == ChapterRules.Fief.LastResearchEra && project.Index != key6)
                    runner.Submit(new Command(0, (byte)civ, CommandKind.CompleteProject, project.Index, 0, 0));
        }

        // Every E5 prerequisite and the breadth requirement are complete; KEY-6 is the only unfinished E5 project.
        runner.Advance(ResearchSystem.SeasonTicks + ResearchSystem.StepTicks + 1L);
        Assert.Equal(5, match.Research.Era(0));
        Assert.Equal(5, match.Research.Era(1));
        Assert.Equal(key6, match.Research.Slot(1, 0));
        Assert.Contains(match.Ai!.Log.Entries, e => e.Civ == 1 && e.Layer == "Spark" && e.Kind == "Primary" &&
            e.Subject == key6 && e.Reason == "keystone ready");
        Assert.Equal(ProjectState.Available, match.Research.State(0, key6));
        var councilScores = new Fix64[catalog.Count];
        match.Research.CouncilPrior(0, councilScores);
        Assert.True(councilScores[key6] > Fix64.Zero,
            "the Council prior should score the ready KEY-6 among available research");

        var forecastCatalog = new ForecastCatalog(catalog, ChapterRules.Fief.LastResearchEra);
        var start = new ForecastStart(forecastCatalog);
        ForecastCapture.FromSystem(start, match.Research, 0, EconomyArchetype.Balanced, new KnowHowTracker(), match.Politics.Season);
        var forecast = new ResearchForecast(forecastCatalog);
        forecast.Prepare(start, horizonSeasons: 1);
        ForecastResult result = forecast.Run(ReadOnlySpan<int>.Empty, promoteKeystone: true);
        Assert.True(result.Key6 >= 0 || forecast.IsAvailableAtEnd(key6),
            "the Fief forecast should either complete KEY-6 in-horizon or retain it as available at the horizon");
    }
}
