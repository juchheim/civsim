using CivSim.Harness;

namespace CivSim.Tests;

public sealed class ResearchM10GateTests
{
    [Theory]
    [InlineData(44.0, true)]
    [InlineData(48.0, true)]
    [InlineData(43.99, false)]
    [InlineData(48.01, false)]
    [InlineData(double.NaN, false)]
    public void B6_end_median_uses_inclusive_44_to_48_minute_window(double median, bool expected) =>
        Assert.Equal(expected, ResearchM10.IsB6EndMedianInRange(median));

    [Theory]
    [InlineData(1, 32, true)]
    [InlineData(1, 10, true)]
    [InlineData(2, 10, false)]
    [InlineData(0, 0, false)]
    [InlineData(-1, 32, false)]
    public void B6_year_cap_rate_allows_at_most_ten_percent(int yearCapMatches, int matches, bool expected) =>
        Assert.Equal(expected, ResearchM10.IsB6YearCapRateInRange(yearCapMatches, matches));
}
