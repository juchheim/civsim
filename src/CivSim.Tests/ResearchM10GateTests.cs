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
    // A rival finished at season 100: the countdown closes the chapter exactly countdownSeasons later.
    [InlineData(MatchEndReason.RivalCountdown, 112, 100, 4, 4, 12, true)]
    [InlineData(MatchEndReason.RivalCountdown, 111, 100, 4, 4, 12, false)]
    [InlineData(MatchEndReason.RivalCountdown, 112, 100, 3, 4, 12, false)]
    // The human finished first (or in the window): their clause closes it sooner, never later.
    [InlineData(MatchEndReason.HumanKeystone, 105, 100, 0, 4, 12, true)]
    [InlineData(MatchEndReason.HumanKeystone, 113, 100, 0, 4, 12, false)]
    // Nobody finished: the chapter is open (no end season); a made-up cap end is wrong.
    [InlineData(MatchEndReason.None, MatchEnd.NoEndSeason, -1, -1, -1, 12, true)]
    [InlineData(MatchEndReason.YearCap, 255, -1, -1, -1, 12, false)]
    [InlineData(MatchEndReason.RivalCountdown, 255, 243, -1, 2, 12, false)]
    public void B6_end_reason_and_season_follow_the_countdown_or_stay_open(
        MatchEndReason reason, int endSeason, int countdownStartSeason, int civ, int countdownCiv,
        int countdownSeasons, bool expected)
    {
        var end = new MatchEnd(reason, endSeason, civ, countdownStartSeason, countdownCiv);
        Assert.Equal(expected, ResearchM10.IsB6EndRuleCorrect(end, countdownSeasons));
    }

    [Theory]
    [InlineData(32, 32, true)]
    [InlineData(20, 32, true)]
    [InlineData(19, 32, false)]
    [InlineData(6, 10, true)]
    [InlineData(5, 10, false)]
    [InlineData(0, 0, false)]
    [InlineData(-1, 32, false)]
    [InlineData(33, 32, false)]
    public void B6_closed_rate_needs_at_least_sixty_percent_of_chapters_closed_within_the_horizon(int closedMatches, int matches, bool expected) =>
        Assert.Equal(expected, ResearchM10.IsB6ClosedRateInRange(closedMatches, matches));
}
