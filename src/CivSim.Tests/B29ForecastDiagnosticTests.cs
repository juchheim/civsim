using System.Collections.Generic;
using CivSim.Harness;

namespace CivSim.Tests;

public sealed class B29ForecastDiagnosticTests
{
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

        B29ForecastDiagnostic.Summary missing = B29ForecastDiagnostic.Summarize(observations, attempts: 3);
        B29ForecastDiagnostic.Summary inaccurate = B29ForecastDiagnostic.Summarize(observations, attempts: 2);

        Assert.False(missing.Pass);
        Assert.False(inaccurate.Pass);
        Assert.Equal(1, inaccurate.Within15Percent);
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
    public void Censored_key4_attempt_remains_missing_and_fails_the_gate()
    {
        var attempt = new B29ForecastDiagnostic.SeatAttempt(seed: 42, civ: 2);
        attempt.Capture(predictedMinute: 51.0);

        Assert.True(attempt.Attempted);
        // KEY-4 never completes before the match horizon, so Complete is never called.
        B29ForecastDiagnostic.Summary summary = B29ForecastDiagnostic.Summarize(
            new List<B29ForecastDiagnostic.Observation>(), attempts: 1);
        Assert.False(summary.Pass);
        Assert.Equal(0, summary.Forecasts);
    }
}
