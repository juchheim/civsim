using CivSim.Harness;
using CivSim.Sim.Ai;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;

namespace CivSim.Tests;

public sealed class M10B30TelemetryTests
{
    [Fact]
    public void Decree_EV_ranking_changes_with_forecast_outcomes()
    {
        DecreeForecast ordinaryMine = new(DecreeKind.Mine, true, 0, 3, 2, 500, 0, 0, Fix64.FromInt(10), 0, 1);
        DecreeForecast richFound = new(DecreeKind.Found, true, 0, 3, 2, 1000, 20, 40, Fix64.Zero, 0, 1);
        Fix64 mineEv = StrategistDecreePolicy.CandidateNetEv(ordinaryMine,
            StrategistDecreePolicy.ForecastBenefit(DecreeKind.Mine, ordinaryMine), Fix64.One);
        Fix64 foundEv = StrategistDecreePolicy.CandidateNetEv(richFound,
            StrategistDecreePolicy.ForecastBenefit(DecreeKind.Found, richFound), Fix64.One);
        Assert.True(foundEv > mineEv);

        DecreeForecast poorFound = richFound with { People = 8, Tiles = 10 };
        Fix64 poorFoundEv = StrategistDecreePolicy.CandidateNetEv(poorFound,
            StrategistDecreePolicy.ForecastBenefit(DecreeKind.Found, poorFound), Fix64.One);
        Assert.True(foundEv > poorFoundEv);
    }

    [Fact]
    public void Decree_EV_reflects_success_probability_and_rejects_zero_or_negative_value()
    {
        DecreeForecast likelyLevy = new(DecreeKind.Levy, true, 0, 2, 1, 900, 10, 0, Fix64.Zero, 0, 2);
        DecreeForecast unlikelyLevy = likelyLevy with { SuccessPermille = 300 };
        Fix64 gross = StrategistDecreePolicy.ForecastBenefit(DecreeKind.Levy, likelyLevy);
        Fix64 likely = StrategistDecreePolicy.CandidateNetEv(likelyLevy, gross, Fix64.One);
        Fix64 unlikely = StrategistDecreePolicy.CandidateNetEv(unlikelyLevy, gross, Fix64.One);
        Assert.True(likely > unlikely);

        DecreeForecast emptyExpedition = new(DecreeKind.Expedition, true, 0, 1, 0, 1000, 0, 0, Fix64.Zero, 0, 2);
        Assert.True(StrategistDecreePolicy.CandidateNetEv(emptyExpedition,
            StrategistDecreePolicy.ForecastBenefit(DecreeKind.Expedition, emptyExpedition), Fix64.One) < Fix64.Zero);
        Assert.Equal(Fix64.MinValue, StrategistDecreePolicy.CandidateNetEv(emptyExpedition with { Allowed = false }, Fix64.FromInt(100), Fix64.One));
    }

    [Fact]
    public void Overflow_requires_an_unused_positive_candidate_while_starting_at_cap()
    {
        Assert.False(M10B30Rules.IsMandateOverflowSeason(false, false, 4, 4));
        Assert.False(M10B30Rules.IsMandateOverflowSeason(true, false, 3, 4));
        Assert.True(M10B30Rules.IsMandateOverflowSeason(true, false, 4, 4));
        Assert.False(M10B30Rules.IsMandateOverflowSeason(true, true, 4, 4));
        Assert.False(M10B30Rules.IsMandateOverflowSeason(true, false, 4, 0));
    }

    [Fact]
    public void Overflow_candidates_must_have_positive_ev_and_spend_mandate()
    {
        Assert.False(M10B30Rules.IsPositivePaidDecreeCandidate(true, 0)); // free DIP-04 Compact cannot relieve the cap
        Assert.False(M10B30Rules.IsPositivePaidDecreeCandidate(false, 1));
        Assert.True(M10B30Rules.IsPositivePaidDecreeCandidate(true, 1));
    }

    [Fact]
    public void Overflow_streak_requires_nine_consecutive_start_of_season_cap_seasons_with_an_unused_positive_paid_candidate()
    {
        int streak = 0;
        for (int season = 1; season <= 8; season++)
        {
            streak = M10B30Rules.AdvanceMandateOverflowStreak(streak, positiveDecreeCandidate: true,
                paidDecreeResolved: false, mandate: 4, cap: 4);
            Assert.Equal(season, streak);
            Assert.False(streak > 8);
        }

        streak = M10B30Rules.AdvanceMandateOverflowStreak(streak, positiveDecreeCandidate: true,
            paidDecreeResolved: false, mandate: 4, cap: 4);
        Assert.Equal(9, streak);
        Assert.True(streak > 8);

        // Losing the positive-EV option, resolving a paid decree, or starting below cap breaks consecutiveness.
        Assert.Equal(0, M10B30Rules.AdvanceMandateOverflowStreak(streak, positiveDecreeCandidate: false,
            paidDecreeResolved: false, mandate: 4, cap: 4));
        Assert.Equal(0, M10B30Rules.AdvanceMandateOverflowStreak(streak, positiveDecreeCandidate: true,
            paidDecreeResolved: true, mandate: 4, cap: 4));
        Assert.Equal(0, M10B30Rules.AdvanceMandateOverflowStreak(streak, positiveDecreeCandidate: true,
            paidDecreeResolved: false, mandate: 3, cap: 4));
        Assert.Equal(0, M10B30Rules.AdvanceMandateOverflowStreak(streak, positiveDecreeCandidate: true,
            paidDecreeResolved: false, mandate: 4, cap: 0));

        streak = M10B30Rules.AdvanceMandateOverflowStreak(0, positiveDecreeCandidate: true,
            paidDecreeResolved: false, mandate: 4, cap: 4);
        Assert.Equal(1, streak);
    }

    [Fact]
    public void Naked_caravan_requires_hostile_route_and_fewer_than_two_escorts()
    {
        Assert.False(M10B30Rules.IsNakedCaravan(TradeGood.Tin, false, 0));
        Assert.True(M10B30Rules.IsNakedCaravan(TradeGood.Tin, true, 0));
        Assert.True(M10B30Rules.IsNakedCaravan(TradeGood.Tin, true, 1));
        Assert.False(M10B30Rules.IsNakedCaravan(TradeGood.Tin, true, 2));
        Assert.False(M10B30Rules.IsNakedCaravan(TradeGood.Copper, true, 0));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Caravan_dispatch_waits_only_for_hostile_routes_without_two_escorts(bool hostile, bool canEscort, bool waits)
    {
        Assert.Equal(waits, PoliticsSystem.ShouldWaitForSafeCaravanRoute(hostile, canEscort));
    }

    [Fact]
    public void Tin_caravan_is_counted_once_when_it_first_becomes_naked()
    {
        var tracker = new NakedTinCaravanTracker();

        Assert.False(tracker.Observe(17, TradeGood.Tin, hostileRoute: false, escortCount: 0));
        // A later hostile exposure qualifies it once; a safe first sighting alone does not latch it out.
        Assert.True(tracker.Observe(17, TradeGood.Tin, hostileRoute: true, escortCount: 0));
        Assert.False(tracker.Observe(17, TradeGood.Tin, hostileRoute: true, escortCount: 0));
        Assert.True(tracker.Observe(18, TradeGood.Tin, hostileRoute: true, escortCount: 1));
        // Nor can later observations count a qualifying caravan twice.
        Assert.False(tracker.Observe(18, TradeGood.Tin, hostileRoute: false, escortCount: 0));
    }
}
