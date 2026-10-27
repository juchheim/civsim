using CivSim.Harness;
using CivSim.Sim.Ai;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;

namespace CivSim.Tests;

public sealed class M10TelemetryRulesTests
{
    [Fact]
    public void A7_dithering_counts_only_ordinary_switches_inside_a_strict_three_year_window()
    {
        static AiDecision Plan(int season, int from, bool hard, int civ = 0)
            => new(season * 4L, season, civ, Strategist.Layer, "Plan", 1, from, Fix64.One,
                $"hard={(hard ? 1 : 0)} plan transition");

        AiDecision[] threeOrdinaryInside = { Plan(2, 0, false), Plan(7, 1, false), Plan(13, 2, false) };
        Assert.Equal(3, M10TelemetryRules.MaxOrdinaryPlanSwitchesInThreeYears(threeOrdinaryInside, 0));

        // The oldest transition is exactly 12 seasons old at season 14, so only two fit the strict window.
        AiDecision[] onWindowBoundary = { Plan(2, 0, false), Plan(8, 1, false), Plan(14, 2, false) };
        Assert.Equal(2, M10TelemetryRules.MaxOrdinaryPlanSwitchesInThreeYears(onWindowBoundary, 0));

        AiDecision[] hardAndInitial =
        {
            Plan(0, -1, true), Plan(1, 0, true), Plan(2, 1, true), Plan(3, 2, true),
            Plan(4, 3, false), Plan(10, 4, false), Plan(16, 5, false),
        };
        Assert.Equal(2, M10TelemetryRules.MaxOrdinaryPlanSwitchesInThreeYears(hardAndInitial, 0));
        Assert.Equal(0, M10TelemetryRules.MaxOrdinaryPlanSwitchesInThreeYears(threeOrdinaryInside, 1));
    }

    [Fact]
    public void Believed_bronze_requires_observed_bronze_project()
    {
        var rival = new RivalBelief { ProjectBelieved = new bool[5] };
        Assert.False(M10TelemetryRules.BelievesBronze(rival, 3, 4));
        rival.ProjectBelieved[4] = true;
        Assert.True(M10TelemetryRules.BelievesBronze(rival, 3, 4));
        Assert.False(M10TelemetryRules.BelievesBronze(rival, 9, 10));
    }

    [Fact]
    public void B28_counts_only_launched_settlement_raids()
    {
        Assert.True(M10TelemetryRules.IsSettlementRaidLaunch(new PoliticsEvent(0, 0,
            PoliticsEventKind.RaidLaunched, 1, 4, 12)));
        Assert.False(M10TelemetryRules.IsSettlementRaidLaunch(new PoliticsEvent(0, 0,
            PoliticsEventKind.AssaultLaunched, 1, 4, 12)));
        Assert.False(M10TelemetryRules.IsSettlementRaidLaunch(new PoliticsEvent(0, 0,
            PoliticsEventKind.RaidLaunched, 1, 4, -1)));
    }

    [Fact]
    public void B27_fallback_route_is_latched_after_tin_poor_checkpoint()
    {
        Assert.False(M10TelemetryRules.LatchFallbackRoute(false, false, true, false));
        Assert.True(M10TelemetryRules.LatchFallbackRoute(true, false, false, true));
        Assert.True(M10TelemetryRules.LatchFallbackRoute(true, true, false, false));
    }

    [Fact]
    public void Timely_defense_requires_a_fortification()
    {
        Assert.False(M10TelemetryRules.HasTimelyDefense(Defenses.None));
        Assert.True(M10TelemetryRules.HasTimelyDefense(Defenses.None with { Palisade = true }));
        Assert.True(M10TelemetryRules.HasTimelyDefense(Defenses.None with { Hillfort = true }));
        Assert.True(M10TelemetryRules.HasTimelyDefense(Defenses.None with { StoneWalls = true }));
    }

    [Fact]
    public void A7_starvation_requires_stores_in_own_or_reachable_sister_settlement()
    {
        Assert.False(M10TelemetryRules.IsReachableSisterFood(true, true, 16, 15));
        Assert.False(M10TelemetryRules.IsReachableSisterFood(false, true, 1, 15));
        Assert.False(M10TelemetryRules.IsReachableSisterFood(true, false, 1, 15));
        Assert.True(M10TelemetryRules.IsReachableSisterFood(true, true, 15, 15));
        Assert.False(M10TelemetryRules.HasReachableFood(false, false));
        Assert.True(M10TelemetryRules.HasReachableFood(true, false));
        Assert.True(M10TelemetryRules.HasReachableFood(false, true));
    }

    [Fact]
    public void A7_undefended_loss_needs_two_seasons_of_observed_threat_without_response()
    {
        Assert.False(M10TelemetryRules.IsUndefendedLoss(-1, 10, false));
        Assert.False(M10TelemetryRules.IsUndefendedLoss(8, 9, false));
        Assert.False(M10TelemetryRules.IsUndefendedLoss(8, 10, true));
        Assert.True(M10TelemetryRules.IsUndefendedLoss(8, 10, false));
        Assert.True(M10TelemetryRules.IsUndefendedLoss(8, 12, false));
    }

    [Fact]
    public void A7_threat_history_matches_capture_by_own_settlement_not_rival_sighting()
    {
        const int rivalSightingSettlement = 3, ownSettlement = 11;
        var observedByOwnSettlement = new int[1, 16];
        var respondedByOwnSettlement = new bool[1, 16];
        for (int settlement = 0; settlement < 16; settlement++) observedByOwnSettlement[0, settlement] = -1;
        observedByOwnSettlement[0, ownSettlement] = 5;

        Assert.True(M10TelemetryRules.IsUndefendedLossForSettlement(
            observedByOwnSettlement, respondedByOwnSettlement, 0, ownSettlement, currentSeason: 7));
        Assert.False(M10TelemetryRules.IsUndefendedLossForSettlement(
            observedByOwnSettlement, respondedByOwnSettlement, 0, rivalSightingSettlement, currentSeason: 7));
        Assert.False(M10TelemetryRules.IsUndefendedLossForSettlement(
            observedByOwnSettlement, respondedByOwnSettlement, 0, 16, currentSeason: 7));
    }

    [Fact]
    public void A7_idle_labor_is_strictly_over_fifteen_percent_and_requires_food_deficit()
    {
        Assert.False(M10TelemetryRules.IsIdleLaborBlunder(3, 20, true));
        Assert.True(M10TelemetryRules.IsIdleLaborBlunder(4, 20, true));
        Assert.False(M10TelemetryRules.IsIdleLaborBlunder(5, 20, false));
        Assert.False(M10TelemetryRules.IsIdleLaborBlunder(1, 0, true));
    }

    [Fact]
    public void A7_herd_crash_requires_direct_civilization_attribution()
    {
        Assert.False(M10TelemetryRules.IsAttributedHerdCrash(-1, 0, 2));
        Assert.False(M10TelemetryRules.IsAttributedHerdCrash(1, 0, 2));
        Assert.False(M10TelemetryRules.IsAttributedHerdCrash(2, 2, 2));
        Assert.True(M10TelemetryRules.IsAttributedHerdCrash(0, 0, 2));
    }

    [Fact]
    public void A7_avoidable_starvation_requires_shortfall_attribution_and_reachable_food()
    {
        var starvation = new EconomyDeathEvent(1, 0, 0, 20, false, 0, EconomyDeathCause.Starvation,
            Fix64.One, Fix64.Zero, Fix64.Zero, Fix64.FromRatio(2, 10), Fix64.Zero, Fix64.Zero,
            Fix64.Zero, Fix64.Zero, Fix64.Zero, Fix64.Zero);
        var otherCause = starvation with { Cause = EconomyDeathCause.Endemic };

        Assert.False(M10TelemetryRules.IsAvoidableStarvationDeath(starvation, shortfallAttributed: false, reachableFood: true));
        Assert.False(M10TelemetryRules.IsAvoidableStarvationDeath(starvation, shortfallAttributed: true, reachableFood: false));
        Assert.False(M10TelemetryRules.IsAvoidableStarvationDeath(otherCause, shortfallAttributed: true, reachableFood: true));
        Assert.True(M10TelemetryRules.IsAvoidableStarvationDeath(starvation, shortfallAttributed: true, reachableFood: true));
    }

    [Fact]
    public void A7_separates_baseline_starvation_from_conditional_winter_shortfall()
    {
        var death = new EconomyDeathEvent(1, 0, 0, 20, false, 0, EconomyDeathCause.Starvation,
            Fix64.FromInt(1), Fix64.FromRatio(1, 10), Fix64.FromRatio(1, 10), Fix64.FromRatio(5, 10),
            Fix64.Zero, Fix64.Zero, Fix64.Zero, Fix64.Zero, Fix64.Zero, Fix64.FromRatio(2, 10));

        Assert.False(LivingEconomyModel.IsWinterShortfallCauseDraw(death, Fix64.FromRatio(49, 100)));
        Assert.True(LivingEconomyModel.IsWinterShortfallCauseDraw(death, Fix64.FromRatio(50, 100)));
        Assert.True(LivingEconomyModel.IsWinterShortfallCauseDraw(death, Fix64.FromRatio(69, 100)));
        Assert.False(LivingEconomyModel.IsWinterShortfallCauseDraw(death, Fix64.FromRatio(70, 100)));
    }

    [Fact]
    public void Believed_bronze_raid_uses_defenses_from_before_the_event_season_advance()
    {
        Defenses[] defensesBeforeAdvance = { Defenses.None };
        Defenses liveDefenseAfterAdvance = Defenses.None with { Palisade = true };

        Assert.True(M10TelemetryRules.HasTimelyDefense(liveDefenseAfterAdvance));
        Assert.True(M10TelemetryRules.TryMeasureBelievedBronzeRaid(alreadyMeasured: false, believedBronze: true,
            targetSettlement: 0, defensesBeforeAdvance, out bool defendedBeforeRaid));
        Assert.False(defendedBeforeRaid);

        Assert.True(M10TelemetryRules.TryMeasureBelievedBronzeRaid(alreadyMeasured: false, believedBronze: true,
            targetSettlement: 1, defensesBeforeAdvance, out defendedBeforeRaid));
        Assert.False(defendedBeforeRaid);
    }

    [Fact]
    public void Unwalled_frontier_requires_sighted_bronze_warriors_within_reach_and_four_unanswered_seasons()
    {
        Assert.False(M10TelemetryRules.IsUnwalledFrontierThreat(false, true, true));
        Assert.False(M10TelemetryRules.IsUnwalledFrontierThreat(true, false, true));
        Assert.False(M10TelemetryRules.IsUnwalledFrontierThreat(true, true, false));
        Assert.True(M10TelemetryRules.IsUnwalledFrontierThreat(true, true, true));

        Assert.False(M10TelemetryRules.IsUnwalledFrontierBlunder(true, false, 3));
        Assert.True(M10TelemetryRules.IsUnwalledFrontierBlunder(true, false, 4));
        Assert.False(M10TelemetryRules.IsUnwalledFrontierBlunder(true, true, 8));
        Assert.False(M10TelemetryRules.IsUnwalledFrontierBlunder(false, false, 8));
    }

    [Fact]
    public void Dormant_bronze_blunder_requires_a_persistent_option_and_no_recovery_action()
    {
        static bool Blunder(ProjectState met04, bool arsenicOption, ProjectState met05, bool tinOption,
            bool hasAnyMetalAccess,
            int optionSeasons, bool recoveryActionOccurred = false)
            => M10TelemetryRules.IsDormantRouteBlunder(met04, arsenicOption, met05, tinOption,
                hasAnyMetalAccess, optionSeasons, recoveryActionOccurred);

        Assert.False(Blunder(ProjectState.Completed, true, ProjectState.Completed, true, false, 12));
        Assert.False(Blunder(ProjectState.Dormant, false, ProjectState.Available, false, false, 12));
        Assert.False(Blunder(ProjectState.Completed, false, ProjectState.Dormant, true, false, 8));
        Assert.True(Blunder(ProjectState.Completed, false, ProjectState.Dormant, true, false, 9));
        Assert.True(Blunder(ProjectState.Dormant, true, ProjectState.Hidden, false, false, 9));
        Assert.False(Blunder(ProjectState.Completed, false, ProjectState.Dormant, true, true, 12));

        // A recovery action/order in response to the same known option prevents this from being counted as inaction.
        Assert.False(Blunder(ProjectState.Completed, false, ProjectState.Dormant, true, false, 20, recoveryActionOccurred: true));
    }

    [Fact]
    public void Suicide_raid_threshold_is_strictly_below_thirty_percent()
    {
        var launch = new PoliticsEvent(10, 0, PoliticsEventKind.RaidLaunched, 1, 5, 9);
        AiDecision Raid(Fix64 score) => new(10, 1, 0, "Strategist", "Raid", 9, 1, score, "test");
        Assert.True(M10TelemetryRules.IsSuicideRaidLaunch(launch, Raid(Fix64.FromRatio(29, 100))));
        Assert.False(M10TelemetryRules.IsSuicideRaidLaunch(launch, Raid(Fix64.FromRatio(3, 10))));
    }

    [Fact]
    public void Suicide_raid_requires_a_matching_actual_launch()
    {
        var launch = new PoliticsEvent(10, 0, PoliticsEventKind.RaidLaunched, 1, 5, 9);
        var prediction = new AiDecision(10, 1, 0, "Strategist", "Raid", 9, 1,
            Fix64.FromRatio(29, 100), "forecast");
        Assert.True(M10TelemetryRules.IsSuicideRaidLaunch(launch, prediction));
        Assert.False(M10TelemetryRules.IsSuicideRaidLaunch(launch with { Kind = PoliticsEventKind.AssaultLaunched }, prediction));
        Assert.False(M10TelemetryRules.IsSuicideRaidLaunch(launch, prediction with { Subject = 8 }));
    }

    [Fact]
    public void Unsupplied_siege_blunder_requires_over_thirty_percent_desertion_after_carried_food()
    {
        Assert.False(M10TelemetryRules.IsUnsuppliedSiegeBlunder(10, 7, 0));
        Assert.True(M10TelemetryRules.IsUnsuppliedSiegeBlunder(10, 6, 0));
        Assert.False(M10TelemetryRules.IsUnsuppliedSiegeBlunder(10, 6, 1));
        Assert.False(M10TelemetryRules.IsUnsuppliedSiegeBlunder(0, 0, 0));
    }

    [Fact]
    public void Siege_desertion_baseline_is_fresh_for_a_later_investment_at_the_same_target()
    {
        var tracker = new SiegeBlunderTracker();
        object firstInvestment = new();
        object laterInvestment = new();

        Assert.False(tracker.Observe(firstInvestment, beganSeason: 3, remainingMembers: 10, foodSeasons: 0));
        Assert.True(tracker.Observe(firstInvestment, beganSeason: 3, remainingMembers: 6, foodSeasons: 0));
        Assert.False(tracker.Observe(firstInvestment, beganSeason: 3, remainingMembers: 5, foodSeasons: 0));

        // The second episode starts with eight members; it must not inherit the first episode's baseline of ten.
        Assert.False(tracker.Observe(laterInvestment, beganSeason: 9, remainingMembers: 8, foodSeasons: 0));
        Assert.True(tracker.Observe(laterInvestment, beganSeason: 9, remainingMembers: 5, foodSeasons: 0));
    }

    [Fact]
    public void Siege_desertion_event_survives_an_investment_ending_in_the_same_advance()
    {
        var tracker = new SiegeBlunderTracker();
        tracker.BeginFromEvent(civ: 2, settlement: 7, initialMembers: 10);
        Assert.True(tracker.ObserveDesertionEvent(civ: 2, settlement: 7, remainingMembers: 6));
        Assert.False(tracker.ObserveDesertionEvent(civ: 2, settlement: 7, remainingMembers: 5));
    }
}
