using CivSim.Harness;
using CivSim.Sim.Research;

namespace CivSim.Tests;

public sealed class ResearchM10GateTests
{
    [Fact]
    public void B5_requires_its_documented_seeds_even_when_running_the_broader_B6_sample()
    {
        Assert.False(ResearchM10.HasB5AcceptanceSeeds(new ulong[] { 20261019UL }));
        Assert.True(ResearchM10.HasB5AcceptanceSeeds(new ulong[] { 20261019UL, 777UL }));
        Assert.True(ResearchM10.HasB5AcceptanceSeeds(new ulong[] { 20261019UL, 20261020UL, 777UL }));
    }

    [Theory]
    [InlineData(MatchEndReason.RivalCountdown, 112, 100, 4, 4, 12, 255, true)]
    [InlineData(MatchEndReason.YearCap, 255, -1, -1, -1, 12, 255, true)]
    [InlineData(MatchEndReason.YearCap, 255, 250, -1, 2, 12, 255, true)]
    [InlineData(MatchEndReason.YearCap, 255, 100, -1, 4, 12, 255, false)]
    [InlineData(MatchEndReason.RivalCountdown, 111, 100, 4, 4, 12, 255, false)]
    [InlineData(MatchEndReason.RivalCountdown, 112, 100, 3, 4, 12, 255, false)]
    [InlineData(MatchEndReason.YearCap, 255, -1, -1, 2, 12, 255, false)]
    public void B6_end_reason_and_season_follow_countdown_or_cap(
        MatchEndReason reason, int endSeason, int countdownStartSeason, int civ, int countdownCiv,
        int countdownSeasons, int capSeason, bool expected)
    {
        var end = new MatchEnd(reason, endSeason, civ, countdownStartSeason, countdownCiv);
        Assert.Equal(expected, ResearchM10.IsB6EndRuleCorrect(end, countdownSeasons, capSeason));
    }

    [Theory]
    [InlineData(1, 32, true)]
    [InlineData(1, 10, true)]
    [InlineData(2, 10, false)]
    [InlineData(0, 0, false)]
    [InlineData(-1, 32, false)]
    public void B6_year_cap_rate_allows_at_most_ten_percent(int yearCapMatches, int matches, bool expected) =>
        Assert.Equal(expected, ResearchM10.IsB6YearCapRateInRange(yearCapMatches, matches));
}
