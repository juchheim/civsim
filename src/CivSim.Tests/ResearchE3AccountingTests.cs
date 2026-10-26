using CivSim.Harness;

namespace CivSim.Tests;

public sealed class ResearchE3AccountingTests
{
    [Theory]
    [InlineData(0, -1)] // a newborn was not exposed to the season just measured
    [InlineData(1, 0)]
    [InlineData(14, 13)] // the child-to-adult boundary belongs to the pre-aging child cohort
    [InlineData(15, 14)]
    [InlineData(45, 44)] // the adult-to-elder boundary belongs to the pre-aging adult cohort
    [InlineData(46, 45)]
    [InlineData(99, 98)]
    public void Survivor_age_is_rewound_to_the_pre_mortality_cohort(int postSeasonAge, int expectedPreMortalityAge)
    {
        Assert.Equal(expectedPreMortalityAge, ResearchE3.PreMortalityAgeOfSurvivor(postSeasonAge));
    }

    [Fact]
    public void T13_baseline_excludes_the_conditional_winter_shortfall_without_losing_total_mortality()
    {
        (double baseline, double winterShortfall) = ResearchE3.SplitExpectedHazard(total: 0.011,
            winterShortfall: 0.00236);

        Assert.Equal(0.00864, baseline, 10);
        Assert.Equal(0.00236, winterShortfall, 10);
        Assert.Equal(0.011, baseline + winterShortfall, 10);
    }

    [Theory]
    [InlineData(0.036, 2.0, 0.034, 1.25, 0.8027777777777778)]
    [InlineData(0.012, 2.0, 0.011, 1.25, 0.7791666666666667)]
    public void T13_anchor_ratio_uses_annual_effective_hazards(double e0Hazard, double e0Winter,
        double e3Hazard, double e3Winter, double expectedRatio)
    {
        double e0Annual = ResearchE3.AnnualEffectiveHazard(e0Hazard, e0Winter);
        double e3Annual = ResearchE3.AnnualEffectiveHazard(e3Hazard, e3Winter);

        Assert.Equal(expectedRatio, e3Annual / e0Annual, 12);
    }

    [Fact]
    public void B10_expected_epidemic_share_uses_baseline_expected_hazards_and_excludes_winter_shortfall()
    {
        double baselineShare = ResearchE3.ExpectedEpidemicShare(epidemicHazard: 0.00071,
            totalExpectedHazard: 0.012, conditionalWinterShortfall: 0.002, includeWinterShortfall: false);
        double inclusiveDiagnostic = ResearchE3.ExpectedEpidemicShare(epidemicHazard: 0.00071,
            totalExpectedHazard: 0.012, conditionalWinterShortfall: 0.002, includeWinterShortfall: true);

        Assert.Equal(0.071, baselineShare, 12);
        Assert.Equal(0.0591666666666667, inclusiveDiagnostic, 12);
    }
}
