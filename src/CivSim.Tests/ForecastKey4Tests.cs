using System;
using CivSim.Sim.Ai.Forecast;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>KEY-4 ETA output from a prepared E3 forecast (docs/MVP/22 §13.1).</summary>
public sealed class ForecastKey4Tests
{
    private static (ForecastCatalog Catalog, ForecastStart Start, int Key4) BronzeStart(Fix64 insightPerYear)
    {
        ForecastCatalog catalog = new(TestPack.Catalog);
        var start = new ForecastStart(catalog) { Civ = 0, Era = 3, InsightPerYear = insightPerYear };
        Array.Fill(start.FirstBy, -1);
        int key4 = catalog.Catalog.IndexOf("KEY-4");
        foreach (int p in catalog.Numeric)
        {
            start.Cost[p] = catalog.Catalog.Projects[p].Cost;
            start.Done[p] = catalog.Era[p] <= 3 && p != key4;
            if (!start.Done[p]) continue;
            start.DoneByEra[catalog.Era[p]]++;
            start.Mastery += Fix64.FromInt(catalog.Mastery[p]);
        }
        start.People = new ForecastPeople(Fix64.FromInt(100), Fix64.Zero, Fix64.Zero, 1);
        return (catalog, start, key4);
    }

    [Fact]
    public void A_prepared_Bronze_forecast_reports_KEY4_eta()
    {
        (ForecastCatalog catalog, ForecastStart start, int key4) = BronzeStart(Fix64.FromInt(100_000));
        var forecast = new ResearchForecast(catalog);
        forecast.Prepare(start, 12);

        ForecastResult result = forecast.Run(ReadOnlySpan<int>.Empty, promoteKeystone: true);

        Assert.True(result.Key4 > 0, "KEY-4 should complete in the prepared Bronze horizon");
        Assert.Equal(forecast.DoneAt[key4], result.Key4);
    }

    [Fact]
    public void KEY4_eta_is_absent_when_it_lies_beyond_the_horizon()
    {
        (ForecastCatalog catalog, ForecastStart start, int _) = BronzeStart(Fix64.One);
        var forecast = new ResearchForecast(catalog);
        forecast.Prepare(start, 1);

        ForecastResult result = forecast.Run(ReadOnlySpan<int>.Empty, promoteKeystone: true);

        Assert.Equal(-1, result.Key4);
    }

    [Fact]
    public void Extended_report_horizon_finds_KEY4_after_the_72_season_planner_miss()
    {
        (ForecastCatalog catalog, ForecastStart start, int key4) = BronzeStart(Fix64.FromInt(1_000));
        int planProject = Array.Find(catalog.Numeric, p => catalog.Era[p] == 3 && p != key4);
        start.Done[planProject] = false;
        start.DoneByEra[3]--;
        start.Mastery -= Fix64.FromInt(catalog.Mastery[planProject]);
        start.Cost[key4] *= Fix64.FromInt(20);
        int[] samePlan = { planProject };

        var planner = new ResearchForecast(catalog);
        planner.Prepare(start, 72);
        ForecastResult plannerResult = planner.Run(samePlan, promoteKeystone: true);
        int plannerDoneAt = planner.DoneAt[key4];

        var report = new ResearchForecast(catalog);
        report.Prepare(start, 120);
        ForecastResult reportResult = report.Run(samePlan, promoteKeystone: true);

        Assert.Equal(-1, plannerResult.Key4);
        Assert.True(reportResult.Key4 > 72 * report.StepsPerSeason && reportResult.Key4 <= 120 * report.StepsPerSeason);
        Assert.Equal(plannerDoneAt, planner.DoneAt[key4]);
        Assert.Equal(planProject, samePlan[0]);
    }
}
