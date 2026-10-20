using System;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Ai.Forecast;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Tests;

/// <summary>The forecast model's own rules (docs/MVP/14 §3.2): deterministic, bounded in iterations, and fed exact own state.</summary>
public class ForecastModelTests
{
    private static readonly Lazy<(MatchModel Match, ForecastStart Start)> Minute8 = new(() =>
    {
        var fc = new ForecastCatalog(TestPack.Catalog);
        MatchModel match = ForecastTestKit.Match(777);
        var runner = new SimRunner(match);
        var tracker = new KnowHowTracker();
        for (int s = 0; s < 32; s++)
        {
            runner.Advance(ResearchSystem.SeasonTicks);
            tracker.Observe(new SystemReadout(match.Research, 1), match.Politics.Season);
        }
        return (match, ForecastTestKit.Capture(match, 1, tracker, fc));
    });

    [Fact]
    public void A_rollout_is_a_pure_function_of_its_start_and_plan()
    {
        ForecastStart start = Minute8.Value.Start;
        var a = new ResearchForecast(start.Catalog, ForecastTuning.PlannerStepsPerSeason);
        var b = new ResearchForecast(start.Catalog, ForecastTuning.PlannerStepsPerSeason);
        a.Prepare(start, 90);
        b.Prepare(start, 90);
        var value = new ForecastValuation(start.Catalog.Count);
        for (int p = 0; p < value.RivalEta.Length; p++) value.RivalEta[p] = 40 + p % 50;
        int[] plan = start.Catalog.Numeric.Where(p => !start.Done[p]).Take(4).ToArray();
        ForecastResult first = a.Run(plan, value, promoteKeystone: true);
        int[] doneAt = a.DoneAt.ToArray();
        a.Run(ReadOnlySpan<int>.Empty);                       // a different run in between leaves no trace
        ForecastResult again = a.Run(plan, value, promoteKeystone: true);
        ForecastResult other = b.Run(plan, value, promoteKeystone: true);
        Assert.Equal(first, again);
        Assert.Equal(first, other);
        Assert.Equal(doneAt, a.DoneAt);
        Assert.Equal(doneAt, b.DoneAt);
        Assert.True(first.Completed > 20 && first.Utility > Fix64.Zero);
    }

    [Fact]
    public void A_rollout_costs_a_fixed_number_of_steps_whatever_the_plan()
    {
        // The budget is counted in iterations (14 §12): a rollout never takes more than horizon × steps-per-season steps.
        ForecastStart start = Minute8.Value.Start;
        var f = new ResearchForecast(start.Catalog, ForecastTuning.PlannerStepsPerSeason);
        f.Prepare(start, 60);
        foreach (int take in new[] { 0, 3, 11 })
        {
            int[] plan = start.Catalog.Numeric.Where(p => !start.Done[p]).Take(take).ToArray();
            ForecastResult r = f.Run(plan);
            Assert.InRange(r.Steps, 1, 60 * ForecastTuning.PlannerStepsPerSeason);
        }
        Assert.Equal(3, f.Rollouts);
    }

    [Fact]
    public void A_forecast_crosses_keystones_and_counts_Mastery_by_era()
    {
        // From minute 8 (E1) the Council's own order reaches KEY-2 inside fifteen minutes; every project done is worth at least
        // one Mastery point and E1 projects two (docs/MVP/01 §7.1).
        ForecastStart start = Minute8.Value.Start;
        var f = new ResearchForecast(start.Catalog);
        f.Prepare(start, 60);
        ForecastResult baseline = f.Run(ReadOnlySpan<int>.Empty, promoteKeystone: true);
        Assert.True(baseline.Key2 > 0, "KEY-2 within fifteen minutes of minute 8");
        Assert.True(baseline.Era >= 2);
        Assert.True(baseline.Mastery > Fix64.FromInt(baseline.Completed - 1));
    }

    [Fact]
    public void The_AI_recovers_its_elders_from_its_own_Commons_Insight()
    {
        // The Spark reads only the port and the own view; its estimate of the elders must match research's own count.
        var ctl = CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Sage);
        MatchModel match = ForecastTestKit.Match(20261019, ctl, CivControl.Council, CivControl.Council, CivControl.Council);
        var runner = new SimRunner(match);
        var fc = new ForecastCatalog(TestPack.Catalog);
        foreach (int minutes in new[] { 3, 9 })
        {
            runner.Advance(minutes * ForecastTestKit.TicksPerMinute - runner.Tick);
            AiContext ctx = match.Ai!.SeatOf(0)!.Context;
            var start = new ForecastStart(fc);
            start.CaptureResearch(new PortReadout(ctx.Research), 0, EconomyArchetype.Sage);
            ForecastPeople people = start.EstimatePeople(ctx.Own.Population, ctx.Own.Adults, ctx.Own.Settlements.Count);
            InsightInputs exact = match.Research.ForecastInsightInputs(0);
            Assert.InRange((people.Elders - exact.Elders).ToDoubleForDisplay(), -1.0, 1.0);
            Assert.Equal(exact.Adults, people.Adults);
        }
    }
}
