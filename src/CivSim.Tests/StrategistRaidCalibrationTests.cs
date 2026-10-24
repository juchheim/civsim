using CivSim.Sim.Ai;
using CivSim.Sim.Numerics;
using Xunit;

namespace CivSim.Tests;

public sealed class StrategistRaidCalibrationTests
{
    [Fact]
    public void Raid_probability_is_scaled_by_the_baseline_observed_to_predicted_ratio()
    {
        Assert.Equal(Fix64.FromRatio(3, 8), StrategistTuning.CalibrateRaidSuccess(Fix64.Half));
        Assert.Equal(Fix64.FromRatio(9, 16), StrategistTuning.CalibrateRaidSuccess(Fix64.FromRatio(3, 4)));
        Assert.Equal(Fix64.FromRatio(3, 4), StrategistTuning.CalibrateRaidSuccess(Fix64.One));
    }

    [Fact]
    public void Raid_threshold_uses_the_calibrated_probability()
    {
        Fix64 threshold = StrategistTuning.MinRaidSuccess;
        Fix64 simulated = Fix64.FromRatio(39, 100);
        Fix64 calibrated = StrategistTuning.CalibrateRaidSuccess(simulated);

        Assert.True(simulated >= threshold);
        Assert.False(StrategistTuning.MeetsRaidSuccessThreshold(calibrated, threshold));
        Assert.True(StrategistTuning.MeetsRaidSuccessThreshold(
            StrategistTuning.CalibrateRaidSuccess(Fix64.FromRatio(40, 100)), threshold));
    }
}
