using System.Collections.Generic;
using CivSim.Harness;
using Xunit;

namespace CivSim.Tests;

public sealed class BronzeAiGateTests
{
    [Fact]
    public void B8_firsts_ledger_formats_claimants_opponent_timing_and_team_era_totals()
    {
        var claims = new List<BronzeAiGates.B8FirstClaim>
        {
            new("MET-03", 3, false, 12, 1, "enabled", 140, 155),
            new("KEY-3", 3, true, 36, 0, "disabled", 120, 140),
        };

        string ledger = BronzeAiGates.FormatB8FirstLedger(claims);

        Assert.Contains("MET-03:pts=12:claim=civ1/enabled:s140:opp=s155", ledger);
        Assert.Contains("KEY-3:pts=36:claim=civ0/disabled:s120:opp=s140", ledger);
        Assert.Contains("E3(ordinary=12,keystone=0)", ledger);
        Assert.Contains("E3(ordinary=0,keystone=36)", ledger);
    }

    [Fact]
    public void B8_summary_pairs_mirrors_then_reports_cohort_and_project_deltas()
    {
        static BronzeAiGates.B8SampleDelta Sample(ulong seed, int swap, double legacy, double firsts, double project, double margin) =>
            new(seed, swap, 1, 2, firsts, 3, legacy, 1, 1, 0, 0, 4, 1, 0,
                new double[7], new double[7], new[] { project }, new[] { "KEY-3" }, margin);
        var samples = new[] { Sample(10, 0, 20, 8, 8, 20), Sample(10, 1, 10, 2, 2, -3),
            Sample(11, 0, -6, -4, -4, -6), Sample(11, 1, -2, 0, 0, -2) };

        var paired = BronzeAiGates.PairB8Samples(samples);
        string summary = BronzeAiGates.FormatB8CohortSummary(paired, new[] { "KEY-3" }, samples);

        Assert.Contains("B8 cohort summary seeds=2", summary);
        Assert.Contains("KEY-3=+5.0", summary);
        Assert.Contains("B8 worst paired seeds by Legacy delta=11=-4.0,10=+15.0", summary);
        Assert.Contains("B8 mirrored orientation split-pairs=1/2", summary);
        Assert.Contains("B8 orientation seed=10 swap=0 enabledLegacyMargin=+20.0 result=win", summary);
        Assert.Contains("B8 orientation seed=10 swap=1 enabledLegacyMargin=-3.0 result=loss", summary);
        Assert.Contains("B8 five closest losing orientation margins to zero=11/swap1=-2.0,10/swap1=-3.0,11/swap0=-6.0", summary);
    }

    [Fact]
    public void B8_summary_ignores_none_slot_and_maps_all_decree_slots()
    {
        static double[] Counts(double scale) => new[] { 999.0, 1 * scale, 2 * scale, 3 * scale,
            4 * scale, 5 * scale, 6 * scale };
        static BronzeAiGates.B8SampleDelta Sample(int swap) =>
            new(21, swap, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                Counts(1), Counts(-1), new double[0], new[] { "KEY-3" }, 0);

        string summary = BronzeAiGates.FormatB8CohortSummary(
            BronzeAiGates.PairB8Samples(new[] { Sample(0), Sample(1) }), new[] { "KEY-3" });

        Assert.Contains("decreeDone(mean/median)=[Found=+1.0/+1.0,Expedition=+2.0/+2.0,Mine=+3.0/+3.0,Levy=+4.0/+4.0,Corvee=+5.0/+5.0,Compact=+6.0/+6.0]", summary);
        Assert.Contains("done=[Found=+1.0,Expedition=+2.0,Mine=+3.0,Levy=+4.0,Corvee=+5.0,Compact=+6.0]", summary);
        Assert.DoesNotContain("Compact=0.0", summary);
        Assert.DoesNotContain("Found=+999.0", summary);
    }

    [Theory]
    [InlineData(true, 0, false, false)]
    [InlineData(true, 1, false, true)]
    [InlineData(false, 0, false, true)]
    [InlineData(false, 1, true, false)]
    public void Mirrored_match_result_is_attributed_to_the_intended_side(bool b7, int swap, bool offTeamA, bool expectedTeamA)
    {
        Assert.Equal(expectedTeamA, BronzeAiGates.ScoresTeamA(b7, swap, offTeamA));
    }

    [Theory]
    [InlineData(31, 64)]
    [InlineData(32, 63)]
    [InlineData(1, 2)]
    public void B7_and_B8_samples_below_full_dimensions_are_diagnostic(int seeds, int minutes)
    {
        Assert.Equal(BronzeAiGates.Result.Diagnostic, BronzeAiGates.Classify(seeds, minutes, 1, .60));
    }

    [Theory]
    [InlineData(.65, "Pass")]
    [InlineData(.64, "Fail")]
    public void B7_requires_65_percent(double rate, string expected)
    {
        Assert.Equal(expected, BronzeAiGates.Classify(32, 64, rate, .65).ToString());
    }

    [Theory]
    [InlineData(.60, "Pass")]
    [InlineData(.59, "Fail")]
    public void B8_requires_60_percent(double rate, string expected)
    {
        Assert.Equal(expected, BronzeAiGates.Classify(32, 64, rate, .60).ToString());
    }
}
