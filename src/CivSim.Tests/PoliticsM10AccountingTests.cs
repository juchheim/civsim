using CivSim.Harness;

namespace CivSim.Tests;

public sealed class PoliticsM10AccountingTests
{
    [Fact]
    public void Border_denominator_includes_every_expected_pair()
    {
        Assert.Equal(2, PoliticsM10.ExpectedBorderPairs(4));
        Assert.Equal(12, PoliticsM10.ExpectedBorderPairs(24));
    }

    [Fact]
    public void Incomplete_key4_run_still_reports_actual_horizon_minutes()
    {
        Assert.Equal(64.0, PoliticsM10.ElapsedMinutes(64 * 600));
    }

    [Fact]
    public void B17_means_exclude_civilizations_without_key4_samples()
    {
        // Two civs completed KEY-4; the other two must not be treated as zero-valued observations.
        var metrics = PoliticsM10.SummarizeB17(settlementTotal: 24, claimedLandShareTotal: 1.12,
            completedCivs: 2, touchingPairs: 0, sampledPairs: 1);

        Assert.Equal(12.0, metrics.SettlementsPerCiv);
        Assert.Equal(0.56, metrics.ClaimedLandShare, 10);
    }

    [Fact]
    public void B17_border_rate_excludes_unobserved_pairs()
    {
        // One of two map pairs reached both KEY-4 samples and touched; the other is unobserved.
        var metrics = PoliticsM10.SummarizeB17(settlementTotal: 20, claimedLandShareTotal: 0.7,
            completedCivs: 2, touchingPairs: 1, sampledPairs: 1);

        Assert.Equal(1.0, metrics.PairedBorderTouchRate);
        Assert.Equal(0.0, PoliticsM10.SummarizeB17(0, 0, 0, 0, 0).PairedBorderTouchRate);
    }

    [Fact]
    public void E3_exposure_includes_key3_and_key4_boundaries_but_not_outside()
    {
        Assert.False(PoliticsM10.IsE3ExposureTick(99, 100, 200));
        Assert.True(PoliticsM10.IsE3ExposureTick(100, 100, 200));
        Assert.True(PoliticsM10.IsE3ExposureTick(150, 100, 200));
        Assert.True(PoliticsM10.IsE3ExposureTick(200, 100, 200));
        Assert.False(PoliticsM10.IsE3ExposureTick(201, 100, 200));
        Assert.False(PoliticsM10.IsE3ExposureTick(99, -1, -1));
        Assert.True(PoliticsM10.IsE3ExposureTick(500, 100, -1));
    }
}
