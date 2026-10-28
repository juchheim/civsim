using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Harness;
using CivSim.Sim.Content;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

public sealed class A7HarnessTelemetryTests
{
    [Fact]
    public void Food_shortage_scout_skip_diagnostic_is_warlord_only_by_default_and_can_opt_in_chieftain()
    {
        Assert.True(Tournament.FoodShortageScoutSkipAppliesForDiagnostics("warlord", chieftainOptIn: false));
        Assert.False(Tournament.FoodShortageScoutSkipAppliesForDiagnostics("chieftain", chieftainOptIn: false));
        Assert.True(Tournament.FoodShortageScoutSkipAppliesForDiagnostics("chieftain", chieftainOptIn: true));
        Assert.False(Tournament.FoodShortageScoutSkipAppliesForDiagnostics("rusher", chieftainOptIn: true));
    }

    [Fact]
    public void Ffa_match_detail_formatter_reports_winner_runner_up_margin_and_components()
    {
        EconomyArchetype[] seating =
        {
            EconomyArchetype.Builder, EconomyArchetype.Grower,
            EconomyArchetype.Wanderer, EconomyArchetype.Warlord,
        };
        CivSim.Harness.CivOutcome[] civs =
        {
            new CivSim.Harness.CivOutcome { Dominion = 4, Mastery = 3, Firsts = 2, Wonders = 1 },
            new CivSim.Harness.CivOutcome { Dominion = 3, Mastery = 2, Firsts = 2, Wonders = 1 },
            new CivSim.Harness.CivOutcome { Dominion = 2, Mastery = 2, Firsts = 2, Wonders = 1 },
            new CivSim.Harness.CivOutcome { Dominion = 1, Mastery = 1, Firsts = 2, Wonders = 1 },
        };

        string row = Tournament.FormatFfaMatchDetail(20261019, 2, seating, civs);

        Assert.Contains("winner=Builder/c0 L=10.0", row, StringComparison.Ordinal);
        Assert.Contains("runnerUp=Grower/c1 L=8.0 margin=2.0", row, StringComparison.Ordinal);
        Assert.Contains("Builder/c0:L10.0(D4.0,M3.0,F2.0,W1.0)", row, StringComparison.Ordinal);
        Assert.Contains("Warlord/c3:L5.0(D1.0,M1.0,F2.0,W1.0)", row, StringComparison.Ordinal);
    }

    [Fact]
    public void Ffa_firsts_attribution_formats_project_era_and_reconciles_scores()
    {
        EconomyArchetype[] seating =
        {
            EconomyArchetype.Builder, EconomyArchetype.Grower,
            EconomyArchetype.Wanderer, EconomyArchetype.Sage,
        };
        CivSim.Harness.CivOutcome[] civs =
        {
            new CivSim.Harness.CivOutcome { Dominion = 100, Firsts = 15 },
            new CivSim.Harness.CivOutcome { Dominion = 111, Firsts = 3 },
            new CivSim.Harness.CivOutcome { Dominion = 90 },
            new CivSim.Harness.CivOutcome { Dominion = 80 },
        };
        FfaFirstClaimDiagnostic[] claims =
        {
            new(0, 1, "KEY-1", 0, 3), new(0, 7, "KEY-2", 1, 12), new(1, 2, "CRA-1", 0, 3),
        };

        string row = Tournament.FormatFfaFirstsAttribution(20261025, 1, seating, civs, claims);

        Assert.Contains("winner=Builder/c0 L=115.0", row, StringComparison.Ordinal);
        Assert.Contains("runnerUp=Grower/c1 L=114.0 margin=1.0", row, StringComparison.Ordinal);
        Assert.Contains("builderFirsts=score=15.0/events=15/reconciled=True[E0:[KEY-1=3];E1:[KEY-2=12]]", row, StringComparison.Ordinal);
        Assert.Contains("runnerUpFirsts=score=3.0/events=3/reconciled=True[E0:[CRA-1=3]]", row, StringComparison.Ordinal);
    }

    [Fact]
    public void Ffa_firsts_diagnostic_preserves_checksum_and_scores()
    {
        Player[] seats =
        {
            Player.Parse("warlord", EconomyArchetype.Builder), Player.Parse("warlord", EconomyArchetype.Grower),
            Player.Parse("warlord", EconomyArchetype.Wanderer), Player.Parse("warlord", EconomyArchetype.Sage),
        };
        EconomyArchetype[] archetypes =
        {
            EconomyArchetype.Builder, EconomyArchetype.Grower,
            EconomyArchetype.Wanderer, EconomyArchetype.Sage,
        };

        MatchOutcome baseline = Tournament.Play(TestPack.Pack, 20261025, seats, archetypes, minutes: 1);
        MatchOutcome observed = Tournament.Play(TestPack.Pack, 20261025, seats, archetypes, minutes: 1,
            captureFfaFirstsDiagnostic: true);

        Assert.Equal(baseline.Checksum, observed.Checksum);
        Assert.Equal(baseline.Civs.Select(c => c.Legacy), observed.Civs.Select(c => c.Legacy));
        for (int civ = 0; civ < observed.Civs.Length; civ++)
            Assert.Equal(observed.Civs[civ].Firsts,
                observed.FfaFirstClaims.Where(claim => claim.Civ == civ).Sum(claim => (double)claim.Points), 2);
    }

    [Fact]
    public void Ffa_food_demography_timeline_preserves_checksum_and_scores_through_season_24()
    {
        Player[] seats =
        {
            Player.Parse("warlord", EconomyArchetype.Builder), Player.Parse("warlord", EconomyArchetype.Grower),
            Player.Parse("warlord", EconomyArchetype.Wanderer), Player.Parse("warlord", EconomyArchetype.Sage),
        };
        EconomyArchetype[] archetypes =
        {
            EconomyArchetype.Builder, EconomyArchetype.Grower,
            EconomyArchetype.Wanderer, EconomyArchetype.Sage,
        };

        MatchOutcome baseline = Tournament.Play(TestPack.Pack, 20261025, seats, archetypes, minutes: 6);
        MatchOutcome observed = Tournament.Play(TestPack.Pack, 20261025, seats, archetypes, minutes: 6,
            captureFfaResearchTimeline: true, captureFfaFoodDemographyTimeline: true, ffaRotation: 2);

        Assert.Equal(baseline.Checksum, observed.Checksum);
        Assert.Equal(baseline.Civs.Select(c => c.Legacy), observed.Civs.Select(c => c.Legacy));
        Assert.Contains(observed.FfaResearchTimeline, row => row.Contains("rotation=2 season=8", StringComparison.Ordinal));
        Assert.Contains(observed.FfaResearchTimeline, row => row.Contains("A5_RESEARCH_RESULT", StringComparison.Ordinal));
        Assert.Contains(observed.FfaFoodDemographyTraces, row => row.Contains("rotation=2 season=24", StringComparison.Ordinal));
    }

    [Fact]
    public void A5_fission_deferral_is_opt_in_targeted_and_preserves_default_trace_invariance()
    {
        ArgumentException parserError = Assert.Throws<ArgumentException>(() =>
            AiCommand.Run(new[] { "--a5-defer-builder-s1-fission-until64" }));
        Assert.Contains("ai needs --pack FILE", parserError.Message, StringComparison.Ordinal);

        Player[] seats =
        {
            Player.Parse("warlord", EconomyArchetype.Wanderer), Player.Parse("warlord", EconomyArchetype.Builder),
            Player.Parse("warlord", EconomyArchetype.Sage), Player.Parse("warlord", EconomyArchetype.Grower),
        };
        EconomyArchetype[] archetypes =
        {
            EconomyArchetype.Wanderer, EconomyArchetype.Builder,
            EconomyArchetype.Sage, EconomyArchetype.Grower,
        };

        MatchOutcome baseline = Tournament.Play(TestPack.Pack, 20261025, seats, archetypes, minutes: 16);
        MatchOutcome traceOnly = Tournament.Play(TestPack.Pack, 20261025, seats, archetypes, minutes: 16,
            captureFfaFoodDemographyTimeline: true, ffaRotation: 4);
        Assert.Equal(baseline.Checksum, traceOnly.Checksum);
        Assert.Equal(baseline.Civs.Select(c => c.Legacy), traceOnly.Civs.Select(c => c.Legacy));

        MatchOutcome deferred = Tournament.Play(TestPack.Pack, 20261025, seats, archetypes, minutes: 20,
            captureFfaFoodDemographyTimeline: true, ffaRotation: 4,
            deferFissionSettlementUntilSeasonForDiagnostics: 64);
        string[] builderParentRows = deferred.FfaFoodDemographyTraces
            .Where(row => row.Contains("seat=Builder/c1", StringComparison.Ordinal) &&
                row.Contains("settlement=s1", StringComparison.Ordinal)).ToArray();
        Assert.Contains(builderParentRows, row => row.Contains("season=40", StringComparison.Ordinal) &&
            row.Contains("fission(pending/firstPending/firstSplit/splits)=True/", StringComparison.Ordinal));
        Assert.Contains(builderParentRows, row => row.Contains("season=64", StringComparison.Ordinal));
        Assert.Contains(builderParentRows, row => row.Contains("season=80", StringComparison.Ordinal) &&
            row.Contains("fission(pending/firstPending/firstSplit/splits)=False/", StringComparison.Ordinal) &&
            row.Contains("/never/0", StringComparison.Ordinal));
    }

    [Fact]
    public void A3_warlord_s2_fission_delay_candidate_is_opt_in()
    {
        ArgumentException parserError = Assert.Throws<ArgumentException>(() =>
            AiCommand.Run(new[] { "--a3-defer-warlord-s2-fission-until30" }));
        Assert.Contains("ai needs --pack FILE", parserError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A5_case_control_timeline_preserves_checksum_and_reports_split_and_checkpoints()
    {
        Assert.True(Tournament.IsFfaCaseControlTerminal(matchEnded: true, horizonReached: false));
        Assert.True(Tournament.IsFfaCaseControlTerminal(matchEnded: false, horizonReached: true));
        Assert.False(Tournament.IsFfaCaseControlTerminal(matchEnded: false, horizonReached: false));

        Player[] seats =
        {
            Player.Parse("warlord", EconomyArchetype.Wanderer), Player.Parse("warlord", EconomyArchetype.Builder),
            Player.Parse("warlord", EconomyArchetype.Sage), Player.Parse("warlord", EconomyArchetype.Grower),
        };
        EconomyArchetype[] archetypes =
        {
            EconomyArchetype.Wanderer, EconomyArchetype.Builder,
            EconomyArchetype.Sage, EconomyArchetype.Grower,
        };

        MatchOutcome baseline = Tournament.Play(TestPack.Pack, 20261025, seats, archetypes, minutes: 20);
        MatchOutcome observed = Tournament.Play(TestPack.Pack, 20261025, seats, archetypes, minutes: 20,
            ffaRotation: 4, captureFfaCaseControlTimeline: true);

        Assert.Equal(baseline.Checksum, observed.Checksum);
        Assert.Equal(baseline.Civs.Select(c => c.Legacy), observed.Civs.Select(c => c.Legacy));
        Assert.Contains(observed.FfaCaseControlTraces, row => row.StartsWith("A5_CASE_SPLIT", StringComparison.Ordinal) &&
            row.Contains("donorPre(pop/child/adult/elder)=", StringComparison.Ordinal) &&
            row.Contains("adultWorkThisSeason=", StringComparison.Ordinal));
        Assert.Contains(observed.FfaCaseControlTraces, row => row.Contains("season=64 seat=Builder/c1", StringComparison.Ordinal) &&
            row.Contains("population=", StringComparison.Ordinal) && row.Contains("cumulativeFirstClaimPoints=", StringComparison.Ordinal) &&
            row.Contains("insightGenerated=", StringComparison.Ordinal) && row.Contains("completedProjects=", StringComparison.Ordinal) &&
            row.Contains("D=", StringComparison.Ordinal) && row.Contains("M=", StringComparison.Ordinal) && row.Contains("Legacy=", StringComparison.Ordinal));
        Assert.Contains(observed.FfaCaseControlTraces, row => row.Contains("season=80:terminal", StringComparison.Ordinal) &&
            row.Contains("seat=Builder/c1", StringComparison.Ordinal) && row.Contains("terminalWinner=", StringComparison.Ordinal) &&
            row.Contains("fissions=", StringComparison.Ordinal) && row.Contains("meanFoodSecurity=", StringComparison.Ordinal));
    }

    [Fact]
    public void Check_a7_option_is_recognized_by_the_ai_command_parser()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => AiCommand.Run(new[] { "--check-a7" }));
        Assert.Contains("ai needs --pack FILE", error.Message);
    }

    [Fact]
    public void Short_tournament_match_populates_A7_match_counters_from_AI_seats()
    {
        Player[] seats =
        {
            Player.Parse("warlord", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
        };
        EconomyArchetype[] archetypes =
        {
            EconomyArchetype.Balanced, EconomyArchetype.Balanced,
            EconomyArchetype.Balanced, EconomyArchetype.Balanced,
        };

        MatchOutcome match = Tournament.Play(TestPack.Pack, Tournament.BaseSeed, seats, archetypes, minutes: 1);

        Assert.Equal(1, match.A7AiSeatMatches);
        Assert.Equal(match.Civs[0].AvoidableStarvationDeaths, match.AvoidableStarvationDeaths);
        Assert.Equal(match.Civs[0].UndefendedLosses, match.UndefendedLosses);
        Assert.Equal(match.Civs[0].AttributedHerdCrashes, match.AttributedHerdCrashes);
        Assert.Equal(match.Civs[0].IdleLaborSeasons, match.IdleLaborSeasons);
        Assert.True(match.AvoidableStarvationDeaths >= 0);
        Assert.True(match.UndefendedLosses >= 0);
        Assert.True(match.AttributedHerdCrashes >= 0);
        Assert.True(match.IdleLaborSeasons >= 0);
    }

    [Fact]
    public void Posture_unlock_override_is_applied_only_to_warlord_harness_seats()
    {
        EconomyArchetype[] archetypes = Enumerable.Repeat(EconomyArchetype.Balanced, 4).ToArray();
        Player[] warlordSeats =
        {
            Player.Parse("warlord", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
        };
        Player[] councilSeats = Enumerable.Repeat(Player.Parse("council", EconomyArchetype.Balanced), 4).ToArray();

        MatchOutcome productionDefault = Tournament.Play(TestPack.Pack, Tournament.BaseSeed, warlordSeats,
            archetypes, minutes: 1);
        MatchOutcome overriddenWarlord = Tournament.Play(TestPack.Pack, Tournament.BaseSeed + 1, warlordSeats,
            archetypes, minutes: 1, warlordPostureUnlockBonusOverride: Fix64.FromInt(54));
        MatchOutcome noWarlord = Tournament.Play(TestPack.Pack, Tournament.BaseSeed + 2, councilSeats,
            archetypes, minutes: 1, warlordPostureUnlockBonusOverride: Fix64.FromInt(54));

        Assert.Null(productionDefault.WarlordPostureUnlockBonusOverride);
        Assert.Equal(Fix64.FromInt(54), overriddenWarlord.WarlordPostureUnlockBonusOverride);
        Assert.Null(noWarlord.WarlordPostureUnlockBonusOverride);
    }

    [Fact]
    public void Frontier_conquest_plan_floor_is_opt_in_and_applied_only_to_warlord_harness_seats()
    {
        EconomyArchetype[] archetypes = Enumerable.Repeat(EconomyArchetype.Balanced, 4).ToArray();
        Player[] warlordSeats =
        {
            Player.Parse("warlord", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
        };
        Player[] councilSeats = Enumerable.Repeat(Player.Parse("council", EconomyArchetype.Balanced), 4).ToArray();

        MatchOutcome productionDefault = Tournament.Play(TestPack.Pack, Tournament.BaseSeed, warlordSeats,
            archetypes, minutes: 1);
        MatchOutcome overriddenWarlord = Tournament.Play(TestPack.Pack, Tournament.BaseSeed + 1, warlordSeats,
            archetypes, minutes: 1, warlordFrontierConquestPlanFloorDiagnostic: true);
        MatchOutcome noWarlord = Tournament.Play(TestPack.Pack, Tournament.BaseSeed + 2, councilSeats,
            archetypes, minutes: 1, warlordFrontierConquestPlanFloorDiagnostic: true);

        Assert.False(productionDefault.WarlordFrontierConquestPlanFloorDiagnostic);
        Assert.True(overriddenWarlord.WarlordFrontierConquestPlanFloorDiagnostic);
        Assert.False(noWarlord.WarlordFrontierConquestPlanFloorDiagnostic);
    }

    [Fact]
    public void Fission_site_diagnostic_does_not_change_tournament_checksum_or_scores()
    {
        Player[] seats =
        {
            Player.Parse("warlord", EconomyArchetype.Balanced), Player.Parse("grower", EconomyArchetype.Balanced),
            Player.Parse("grower", EconomyArchetype.Balanced), Player.Parse("warlord", EconomyArchetype.Balanced),
        };
        EconomyArchetype[] archetypes = Enumerable.Repeat(EconomyArchetype.Balanced, 4).ToArray();

        MatchOutcome baseline = Tournament.Play(TestPack.Pack, 20261024, seats, archetypes, minutes: 32);
        MatchOutcome observed = Tournament.Play(TestPack.Pack, 20261024, seats, archetypes, minutes: 32,
            warlordFissionSiteDiagnostic: true);

        Assert.True(observed.WarlordFissionSiteDiagnosticEnabled);
        Assert.Equal(baseline.Checksum, observed.Checksum);
        Assert.Equal(baseline.Civs.Select(c => c.Legacy), observed.Civs.Select(c => c.Legacy));
        Assert.Contains(observed.WarlordFissionSiteDiagnostics,
            row => row.StartsWith("FISSION_RESOLUTION", StringComparison.Ordinal) && row.Contains("cohort(children=", StringComparison.Ordinal));
        Assert.Contains(observed.WarlordFissionSiteDiagnostics,
            row => row.StartsWith("FISSION_RESOLUTION", StringComparison.Ordinal) && row.Contains("growerGOV02=[", StringComparison.Ordinal));
        Assert.Contains(observed.WarlordFissionSiteDiagnostics,
            row => row.StartsWith("FISSION_TRIGGER_TRACE", StringComparison.Ordinal) && row.Contains("season=128", StringComparison.Ordinal));
        Assert.Contains(observed.WarlordFissionSiteDiagnostics,
            row => row.StartsWith("FISSION_TRIGGER_TRACE", StringComparison.Ordinal) && row.Contains("births4=", StringComparison.Ordinal) &&
                row.Contains("deaths4=[", StringComparison.Ordinal) && row.Contains("capPressureNow=", StringComparison.Ordinal) &&
                row.Contains("leanWinterPressureNow=", StringComparison.Ordinal));
        Assert.Contains(observed.WarlordFissionSiteDiagnostics,
            row => row.StartsWith("A2_LEAD_CHECKPOINT", StringComparison.Ordinal) && row.Contains("season=128", StringComparison.Ordinal) &&
                row.Contains("team=warlord", StringComparison.Ordinal) && row.Contains("GOV02=[", StringComparison.Ordinal));
    }

    [Fact]
    public void A2_lead_checkpoint_is_opt_in_and_report_only_for_seed_20261025()
    {
        Player[] seats =
        {
            Player.Parse("warlord", EconomyArchetype.Balanced), Player.Parse("grower", EconomyArchetype.Balanced),
            Player.Parse("grower", EconomyArchetype.Balanced), Player.Parse("warlord", EconomyArchetype.Balanced),
        };
        EconomyArchetype[] archetypes = Enumerable.Repeat(EconomyArchetype.Balanced, 4).ToArray();

        MatchOutcome baseline = Tournament.Play(TestPack.Pack, 20261025, seats, archetypes, minutes: 16);
        MatchOutcome observed = Tournament.Play(TestPack.Pack, 20261025, seats, archetypes, minutes: 16,
            a2LeadCheckpointDiagnostic: true);

        Assert.False(baseline.A2LeadCheckpointDiagnosticEnabled);
        Assert.Empty(baseline.A2LeadCheckpointTraces);
        Assert.True(observed.A2LeadCheckpointDiagnosticEnabled);
        Assert.Equal(baseline.Checksum, observed.Checksum);
        Assert.Equal(baseline.Civs.Select(c => c.Legacy), observed.Civs.Select(c => c.Legacy));
        Assert.Contains(observed.A2LeadCheckpointTraces, row => row.Contains("season=64", StringComparison.Ordinal) &&
            row.Contains("effectiveWork=", StringComparison.Ordinal) && row.Contains("InsightPerYear=", StringComparison.Ordinal) &&
            row.Contains("slots=", StringComparison.Ordinal) && row.Contains("projects=", StringComparison.Ordinal) &&
            row.Contains("mastery=", StringComparison.Ordinal) && row.Contains("firsts=", StringComparison.Ordinal) &&
            row.Contains("FirstClaimDelta=", StringComparison.Ordinal) && row.Contains("delta(warlord-grower)", StringComparison.Ordinal));
    }

    [Fact]
    public void Warlord_campaign_opportunity_trace_is_report_only_for_seed_20261024()
    {
        Player[] seats =
        {
            Player.Parse("warlord", EconomyArchetype.Balanced), Player.Parse("grower", EconomyArchetype.Balanced),
            Player.Parse("grower", EconomyArchetype.Balanced), Player.Parse("warlord", EconomyArchetype.Balanced),
        };
        EconomyArchetype[] archetypes = Enumerable.Repeat(EconomyArchetype.Balanced, 4).ToArray();

        MatchOutcome baseline = Tournament.Play(TestPack.Pack, 20261024, seats, archetypes, minutes: 32);
        MatchOutcome observed = Tournament.Play(TestPack.Pack, 20261024, seats, archetypes, minutes: 32,
            warlordCampaignOpportunityTrace: true);

        Assert.True(observed.WarlordCampaignOpportunityTraceEnabled);
        Assert.Equal(baseline.Checksum, observed.Checksum);
        Assert.Equal(baseline.Civs.Select(c => c.Legacy), observed.Civs.Select(c => c.Legacy));
        Assert.Contains(observed.WarlordCampaignOpportunityTraces,
            row => row.StartsWith("A2 campaign-milestone", StringComparison.Ordinal) && row.Contains("firstKnown=", StringComparison.Ordinal));
        Assert.Contains(observed.WarlordCampaignOpportunityTraces,
            row => row.StartsWith("A2 campaign-state", StringComparison.Ordinal) &&
                row.Contains("estimatedAvailableAttackers=", StringComparison.Ordinal) &&
                row.Contains("rangeEligible=", StringComparison.Ordinal) &&
                row.Contains("remainingCapture/strategy-gates=not-evaluated", StringComparison.Ordinal));
        Assert.Contains(observed.WarlordCampaignOpportunityTraces,
            row => row.StartsWith("A2 campaign-candidate", StringComparison.Ordinal) &&
                row.Contains("plan=", StringComparison.Ordinal) && row.Contains("planTarget=", StringComparison.Ordinal) &&
                row.Contains("predictedCapture=", StringComparison.Ordinal) &&
                row.Contains("launchFeasible=", StringComparison.Ordinal) &&
                row.Contains("reason=", StringComparison.Ordinal));
    }

    [Fact]
    public void Settlement_trace_demography_checkpoints_are_report_only()
    {
        Player[] seats =
        {
            Player.Parse("warlord", EconomyArchetype.Balanced),
            Player.Parse("rusher", EconomyArchetype.Balanced),
            Player.Parse("rusher", EconomyArchetype.Balanced),
            Player.Parse("warlord", EconomyArchetype.Balanced),
        };
        EconomyArchetype[] archetypes = Enumerable.Repeat(EconomyArchetype.Balanced, 4).ToArray();

        MatchOutcome baseline = Tournament.Play(TestPack.Pack, Tournament.BaseSeed, seats, archetypes, minutes: 7);
        MatchOutcome observed = Tournament.Play(TestPack.Pack, Tournament.BaseSeed, seats, archetypes, minutes: 7,
            warlordSettlementTrace: true);

        Assert.Equal(baseline.Checksum, observed.Checksum);
        Assert.Equal(baseline.Civs.Select(c => c.Legacy), observed.Civs.Select(c => c.Legacy));
        Assert.Contains(observed.WarlordDemographyTraces, row => row.Contains("births=", StringComparison.Ordinal));
        Assert.Contains(observed.WarlordDemographyTraces, row => row.Contains("GOV-02=", StringComparison.Ordinal));
        Assert.Contains(observed.WarlordDemographyTraces, row => row.Contains("caps(food/housing/group)=", StringComparison.Ordinal));
        Assert.Contains(observed.WarlordFoodLedgerTraces, row => row.Contains("foodJobWork=", StringComparison.Ordinal));
        Assert.Contains(observed.WarlordFoodLedgerTraces, row => row.Contains("preEat=", StringComparison.Ordinal));
        Assert.Contains(observed.WarlordFoodWorkAttributionTraces, row => row.Contains("capacity(base/effective)=", StringComparison.Ordinal));
        Assert.Contains(observed.WarlordFoodWorkAttributionTraces, row => row.Contains("foodWork(requested/assigned)=", StringComparison.Ordinal));
        Assert.Contains(observed.WarlordFoodWorkAttributionTraces, row => row.Contains("yield(forage/meat/fish/weir/grain/orchardFruit)=", StringComparison.Ordinal));
        Assert.Contains(observed.WarlordFoodWorkAttributionTraces, row => row.Contains("ages(child/adult/elder)=", StringComparison.Ordinal));
        Assert.Contains(observed.WarlordFoodWorkAttributionTraces, row => row.Contains("births=", StringComparison.Ordinal));
    }

    [Fact]
    public void Food_shortage_scout_reserve_is_opt_in_and_releases_work_only_for_warlord_seats()
    {
        Player[] seats =
        {
            Player.Parse("warlord", EconomyArchetype.Balanced), Player.Parse("rusher", EconomyArchetype.Balanced),
            Player.Parse("rusher", EconomyArchetype.Balanced), Player.Parse("warlord", EconomyArchetype.Balanced),
        };
        EconomyArchetype[] archetypes = Enumerable.Repeat(EconomyArchetype.Balanced, 4).ToArray();
        const ulong seed = 20261020;

        MatchOutcome defaultPath = Tournament.Play(TestPack.Pack, seed, seats, archetypes, minutes: 16);
        MatchOutcome baseline = Tournament.Play(TestPack.Pack, seed, seats, archetypes, minutes: 16,
            warlordSettlementTrace: true);
        MatchOutcome candidate = Tournament.Play(TestPack.Pack, seed, seats, archetypes, minutes: 16,
            warlordSettlementTrace: true, foodShortageSkipScoutsForDiagnostics: true);

        Assert.Equal(defaultPath.Checksum, baseline.Checksum);
        Assert.Equal(defaultPath.Civs.Select(c => c.Legacy), baseline.Civs.Select(c => c.Legacy));
        Assert.Contains(baseline.WarlordFoodWorkAttributionTraces,
            row => row.Contains("foodShort=True", StringComparison.Ordinal) && row.Contains("scoutWork=2.00", StringComparison.Ordinal));
        Assert.Contains(baseline.WarlordFoodWorkAttributionTraces,
            row => row.Contains("season=35 settlement=s3 civ=3/warlord", StringComparison.Ordinal) &&
                row.Contains("foodWork(requested/assigned)=3.19/3.19", StringComparison.Ordinal));
        Assert.Contains(candidate.WarlordFoodWorkAttributionTraces,
            row => row.Contains("foodShort=True", StringComparison.Ordinal) && row.Contains("scoutWork=0.00", StringComparison.Ordinal));
        Assert.Contains(candidate.WarlordFoodWorkAttributionTraces,
            row => row.Contains("season=35 settlement=s3 civ=3/warlord", StringComparison.Ordinal) &&
                row.Contains("foodWork(requested/assigned)=5.31/5.31", StringComparison.Ordinal));
        Assert.Contains(candidate.WarlordFoodWorkAttributionTraces,
            row => row.Contains("civ=1/rusher", StringComparison.Ordinal) && row.Contains("foodShort=True", StringComparison.Ordinal) &&
                row.Contains("scoutWork=2.00", StringComparison.Ordinal));
    }

    [Fact]
    public void Tournament_stops_and_scores_at_the_chapter_cap_even_when_more_minutes_were_requested()
    {
        Player[] seats =
        {
            Player.Parse("warlord", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
        };
        EconomyArchetype[] archetypes = Enumerable.Repeat(EconomyArchetype.Balanced, 4).ToArray();

        // A one-year cap ends at four seasons (one simulated minute); request three minutes to catch
        // regressions where the harness keeps recording post-chapter AI scores or telemetry.
        MatchOutcome match = Tournament.Play(PackWithChapterCapYear(1), Tournament.BaseSeed,
            seats, archetypes, minutes: 3);

        Assert.Equal(600, match.FinalTick);
        Assert.Equal(4, match.SeasonsProcessed);
        Assert.Equal(1, match.A7AiSeatMatches);
        Assert.Equal(match.Civs[0].AvoidableStarvationDeaths, match.AvoidableStarvationDeaths);
        Assert.Equal(match.Civs[0].IdleLaborSeasons, match.IdleLaborSeasons);
    }

    [Fact]
    public void Tournament_stops_and_scores_at_the_KEY4_rival_countdown()
    {
        Player[] seats = Enumerable.Repeat(Player.Parse("council", EconomyArchetype.Balanced), 4).ToArray();
        EconomyArchetype[] archetypes = Enumerable.Repeat(EconomyArchetype.Balanced, 4).ToArray();

        // Make the complete research tree cheap and immediately eligible so this stays a small harness regression,
        // while retaining the real KEY-4 end rule and a Year 64 cap.
        MatchOutcome match = Tournament.Play(PackWithFastResearch(), Tournament.BaseSeed,
            seats, archetypes, minutes: 64);

        Assert.True(match.FinalTick < 64 * 600, $"Expected the KEY-4 countdown before Year 64, got tick {match.FinalTick}.");
        Assert.Equal(match.FinalTick / 150, match.SeasonsProcessed);
        Assert.Equal(0, match.FinalTick % 150); // the match closes only after a complete season advance
        Assert.All(match.Civs, civ => Assert.True(civ.Projects > 0));
    }

    private static ContentPack PackWithChapterCapYear(int year)
    {
        ContentValue root = TestPack.Pack.Root;
        ContentValue balance = root.Get("balance")!;
        ContentValue constants = balance.Get("constants")!;
        var changedConstants = new List<ContentValue>();
        foreach (ContentValue constant in constants.Items)
        {
            if (constant.GetString("id") != "ChapterCapYear")
            {
                changedConstants.Add(constant);
                continue;
            }

            changedConstants.Add(ContentValue.MapOf(constant.Entries.Select(entry =>
                new KeyValuePair<string, ContentValue>(entry.Key,
                    entry.Key == "value" ? ContentValue.Of((long)year) : entry.Value))));
        }

        ContentValue changedBalance = ContentValue.MapOf(balance.Entries.Select(entry =>
            new KeyValuePair<string, ContentValue>(entry.Key,
                entry.Key == "constants" ? ContentValue.ListOf(changedConstants) : entry.Value)));
        ContentValue changedRoot = ContentValue.MapOf(root.Entries.Select(entry =>
            new KeyValuePair<string, ContentValue>(entry.Key,
                entry.Key == "balance" ? changedBalance : entry.Value)));
        return ContentPack.FromTree(changedRoot);
    }

    private static ContentPack PackWithFastResearch()
    {
        ContentValue root = TestPack.Pack.Root;
        ContentValue projects = root.Get("projects")!;
        var changedProjects = new List<ContentValue>();
        foreach (ContentValue project in projects.Items)
        {
            if (project.GetString("status") != "numeric")
            {
                changedProjects.Add(project);
                continue;
            }

            changedProjects.Add(ContentValue.MapOf(project.Entries.Select(entry =>
                new KeyValuePair<string, ContentValue>(entry.Key, entry.Key switch
                {
                    "cost" => ContentValue.Of(Fix64.One),
                    "requires" or "requires_any" => ContentValue.ListOf(Array.Empty<ContentValue>()),
                    "breadth" when entry.Value.Kind == ValueKind.Map => ContentValue.MapOf(entry.Value.Entries.Select(breadth =>
                        new KeyValuePair<string, ContentValue>(breadth.Key,
                            breadth.Key == "min" ? ContentValue.Of(0L) : breadth.Value))),
                    _ => entry.Value,
                }))));
        }

        ContentValue changedRoot = ContentValue.MapOf(root.Entries.Select(entry =>
            new KeyValuePair<string, ContentValue>(entry.Key,
                entry.Key == "projects" ? ContentValue.ListOf(changedProjects) : entry.Value)));
        return ContentPack.FromTree(changedRoot);
    }
}
