using CivSim.Harness;

namespace CivSim.Tests;

public sealed class PoliticsM10AccountingTests
{
    [Fact]
    public void Border_denominator_counts_each_civilization_key4_sample()
    {
        Assert.Equal(4, PoliticsM10.ExpectedBorderCivSamples(4));
        Assert.Equal(96, PoliticsM10.ExpectedBorderCivSamples(24 * 4));
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
            completedCivs: 2, touchingCivs: 0, sampledCivs: 2);

        Assert.Equal(12.0, metrics.SettlementsPerCiv);
        Assert.Equal(0.56, metrics.ClaimedLandShare, 10);
    }

    [Fact]
    public void B17_border_rate_excludes_civilizations_without_key4_samples()
    {
        // One of four civilizations has reached its own KEY-4 and its paired borders touch.
        var metrics = PoliticsM10.SummarizeB17(settlementTotal: 20, claimedLandShareTotal: 0.7,
            completedCivs: 1, touchingCivs: 1, sampledCivs: 1);

        Assert.Equal(1.0, metrics.OwnKey4BorderTouchRate);
        Assert.Equal(0.0, PoliticsM10.SummarizeB17(0, 0, 0, 0, 0).OwnKey4BorderTouchRate);
    }

    [Fact]
    public void B17_samples_borders_at_both_civilizations_own_key4_times()
    {
        // The pair is not touching at civ0's KEY-4, then touches before civ1's later KEY-4.
        var sampled = new bool[2];
        int samples = 0, touching = 0;

        if (PoliticsM10.TryRecordOwnKey4BorderSample(sampled, 0, touching: false, out bool firstTouches))
        {
            samples++;
            if (firstTouches) touching++;
        }
        Assert.False(PoliticsM10.TryRecordOwnKey4BorderSample(sampled, 0, touching: true, out _));
        if (PoliticsM10.TryRecordOwnKey4BorderSample(sampled, 1, touching: true, out bool secondTouches))
        {
            samples++;
            if (secondTouches) touching++;
        }

        Assert.Equal(2, samples);
        Assert.Equal(1, touching);
        Assert.Equal(0.5, PoliticsM10.SummarizeB17(0, 0, 2, touching, samples).OwnKey4BorderTouchRate);
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
