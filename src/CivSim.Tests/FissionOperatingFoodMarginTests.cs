using System.Linq;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;
using CivSim.Harness;
using Xunit;

namespace CivSim.Tests;

public sealed class FissionOperatingFoodMarginTests
{
    [Fact]
    public void Shared_fission_sampler_returns_exact_size_required_representatives_and_stable_mask()
    {
        FissionFigureCandidate[] candidates =
        {
            new(0, 100, 8, false, false), new(1, 101, 17, true, false),
            new(2, 102, 22, false, true), new(3, 103, 35, true, false),
            new(4, 104, 52, false, false), new(5, 105, 11, true, false),
            new(6, 106, 28, false, false), new(7, 107, 44, true, false),
        };

        FissionCohortSample first = FissionCohortSampler.Select(0x1234, 0x5678, 2, 18, 8, 6, candidates);
        FissionCohortSample second = FissionCohortSampler.Select(0x1234, 0x5678, 2, 18, 8, 6, candidates);

        Assert.True(first.Success, first.Result);
        Assert.Equal(6, first.DaughterFigures.Count(selected => selected));
        Assert.Equal(first.DaughterFigures, second.DaughterFigures);
        Assert.Contains(candidates, figure => first.DaughterFigures[figure.FigureIndex] && figure.Female && figure.Age is >= 14 and < 45);
        Assert.Contains(candidates, figure => first.DaughterFigures[figure.FigureIndex] &&
            (figure.Age >= 45 || figure.Age is >= 14 and < 45 && figure.Skilled));
    }

    [Fact]
    public void Four_season_estimate_is_stable_and_caps_recent_yield_by_local_sustainable_income()
    {
        Fix64 first = FissionCohortSampler.EstimatedQuarterFoodOutput(
            Fix64.FromInt(5), Fix64.FromInt(3), Fix64.FromRatio(4, 10), Fix64.FromInt(20));
        Fix64 second = FissionCohortSampler.EstimatedQuarterFoodOutput(
            Fix64.FromInt(5), Fix64.FromInt(3), Fix64.FromRatio(4, 10), Fix64.FromInt(20));
        Fix64 stores = Fix64.FromInt(2), fourSeasonNeed = Fix64.FromInt(24);
        Fix64 margin = stores + first * Fix64.FromInt(4) - fourSeasonNeed;

        Assert.Equal(Fix64.FromInt(5), first); // 6 rations from observed yield/work, capped at 20/4 sustainable.
        Assert.Equal(first, second);
        Assert.Equal(Fix64.FromInt(-2), margin);
    }

    [Fact]
    public void Report_only_trace_switch_does_not_change_economy_checksum()
    {
        const ulong seed = 20261024;
        GeneratedWorld generated = WorldGenerator.Generate(seed);
        WorldEconomyInput world = WorldEconomyInput.FromGeneratedWorld(generated);
        var baseline = new LivingEconomyModel(seed, EconomyContent.FromPack(TestPack.Pack), world);
        var diagnostic = new LivingEconomyModel(seed, EconomyContent.FromPack(TestPack.Pack), world)
        {
            FissionOperatingFoodMarginTraceEnabled = true,
            FissionOperatingFoodMarginCivsForDiagnostics = new[] { 1 },
        };
        Assert.Equal(baseline.ComputeChecksum(), diagnostic.ComputeChecksum());
    }

    [Fact]
    public void Harness_trace_targeting_is_Warlord_only_and_opt_in()
    {
        Assert.True(Tournament.FissionOperatingMarginTraceAppliesForDiagnostics("warlord", enabled: true));
        Assert.False(Tournament.FissionOperatingMarginTraceAppliesForDiagnostics("grower", enabled: true));
        Assert.False(Tournament.FissionOperatingMarginTraceAppliesForDiagnostics("warlord", enabled: false));
    }

}
