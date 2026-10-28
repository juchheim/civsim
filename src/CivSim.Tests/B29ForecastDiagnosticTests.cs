using System.Collections.Generic;
using CivSim.Harness;
using CivSim.Sim.Ai.Forecast;

namespace CivSim.Tests;

public sealed class B29ForecastDiagnosticTests
{
    [Fact]
    public void B29_uses_the_separate_report_estimate_when_the_primary_forecast_misses()
    {
        var primary = new ForecastResult { Key4 = -1 };
        var reportOnly = new ForecastResult { Key4 = 93 };

        Assert.True(B29ForecastDiagnostic.TryGetKey4Forecast(primary, reportOnly, out ForecastResult selected));
        Assert.Equal(93, selected.Key4);
    }

    [Fact]
    public void B29_keeps_the_primary_estimate_when_it_is_available()
    {
        var primary = new ForecastResult { Key4 = 41 };
        var reportOnly = new ForecastResult { Key4 = 93 };

        Assert.True(B29ForecastDiagnostic.TryGetKey4Forecast(primary, reportOnly, out ForecastResult selected));
        Assert.Equal(41, selected.Key4);
    }

    [Fact]
    public void Summary_applies_the_per_forecast_and_median_targets()
    {
        var observations = new List<B29ForecastDiagnostic.Observation>
        {
            new(1, 0, 47.5, 50),
            new(2, 1, 50, 50),
            new(3, 2, 52.5, 50),
        };

        B29ForecastDiagnostic.Summary summary = B29ForecastDiagnostic.Summarize(observations, attempts: 3);

        Assert.True(summary.Pass);
        Assert.Equal(3, summary.Forecasts);
        Assert.Equal(3, summary.Within15Percent);
        Assert.Equal(0.05, summary.MedianError, 8);
    }

    [Fact]
    public void Missing_forecast_and_error_over_fifteen_percent_fail_the_gate()
    {
        var observations = new List<B29ForecastDiagnostic.Observation>
        {
            new(1, 0, 50, 50),
            new(2, 1, 60, 50),
        };

        B29ForecastDiagnostic.Summary missing = B29ForecastDiagnostic.Summarize(observations, attempts: 3,
            missingForecasts: 1);
        B29ForecastDiagnostic.Summary inaccurate = B29ForecastDiagnostic.Summarize(observations, attempts: 2);

        Assert.False(missing.Pass);
        Assert.False(inaccurate.Pass);
        Assert.Equal(1, inaccurate.Within15Percent);
        Assert.Equal(1, missing.MissingForecasts);
        Assert.Equal(0, missing.CensoredForecasts);
    }

    [Fact]
    public void Median_error_over_six_percent_fails_when_all_forecasts_are_observed()
    {
        var observations = new List<B29ForecastDiagnostic.Observation>
        {
            new(1, 0, 53.5, 50),
            new(2, 1, 54.0, 50),
            new(3, 2, 54.5, 50),
        };

        B29ForecastDiagnostic.Summary summary = B29ForecastDiagnostic.Summarize(observations, attempts: 3);

        Assert.Equal(B29ForecastDiagnostic.Disposition.Fail, summary.Result);
        Assert.False(summary.Pass);
        Assert.Equal(0.08, summary.MedianError, 8);
    }

    [Fact]
    public void Forecast_captured_after_key3_pairs_with_a_later_key4_completion()
    {
        var attempt = new B29ForecastDiagnostic.SeatAttempt(seed: 42, civ: 2);
        attempt.Capture(predictedMinute: 51.0);

        B29ForecastDiagnostic.Observation? observation = attempt.Complete(actualMinute: 52.0);

        Assert.True(attempt.Attempted);
        Assert.True(observation.HasValue);
        Assert.Equal(51.0, observation.Value.PredictedMinute);
        Assert.Equal(52.0, observation.Value.ActualMinute);
        Assert.Null(attempt.Complete(actualMinute: 53.0));
    }

    [Fact]
    public void Forecast_without_key4_completion_is_counted_as_censored_and_leaves_the_gate_incomplete()
    {
        var attempt = new B29ForecastDiagnostic.SeatAttempt(seed: 42, civ: 2);
        attempt.Capture(predictedMinute: 51.0);
        attempt.MarkHorizonReached();

        Assert.True(attempt.Attempted);
        Assert.False(attempt.MissingForecastAtCapture);
        Assert.True(attempt.CensoredAtHorizon);
        // KEY-4 never completes before the match horizon, so Complete is never called.
        B29ForecastDiagnostic.Summary summary = B29ForecastDiagnostic.Summarize(
            new List<B29ForecastDiagnostic.Observation>(), attempts: 1, censoredForecasts: 1);
        Assert.Equal(B29ForecastDiagnostic.Disposition.Incomplete, summary.Result);
        Assert.False(summary.Pass);
        Assert.Equal(0, summary.Forecasts);
        Assert.Equal(1, summary.CensoredForecasts);
    }

    [Fact]
    public void A_known_accuracy_miss_fails_even_if_another_forecast_is_censored()
    {
        var observations = new List<B29ForecastDiagnostic.Observation>
        {
            new(1, 0, 60, 50),
        };

        B29ForecastDiagnostic.Summary summary = B29ForecastDiagnostic.Summarize(
            observations, attempts: 2, censoredForecasts: 1);

        Assert.Equal(B29ForecastDiagnostic.Disposition.Fail, summary.Result);
        Assert.False(summary.Pass);
    }

    [Fact]
    public void Empty_sample_is_incomplete()
    {
        B29ForecastDiagnostic.Summary summary = B29ForecastDiagnostic.Summarize(
            new List<B29ForecastDiagnostic.Observation>(), attempts: 0);

        Assert.Equal(B29ForecastDiagnostic.Disposition.Incomplete, summary.Result);
        Assert.False(summary.Pass);
    }

    [Fact]
    public void Missing_at_capture_is_distinct_from_a_forecast_censored_by_horizon()
    {
        var missing = new B29ForecastDiagnostic.SeatAttempt(seed: 42, civ: 2);
        missing.Capture(predictedMinute: null);
        missing.MarkHorizonReached();
        var censored = new B29ForecastDiagnostic.SeatAttempt(seed: 42, civ: 3);
        censored.Capture(predictedMinute: 51.0);
        censored.MarkHorizonReached();

        Assert.True(missing.MissingForecastAtCapture);
        Assert.False(missing.CensoredAtHorizon);
        Assert.False(censored.MissingForecastAtCapture);
        Assert.True(censored.CensoredAtHorizon);
    }
}
