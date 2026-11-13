using System;
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
    public void Coverage_preserving_fission_uses_one_valid_swap_to_keep_both_sides_above_seventy_percent()
    {
        FissionFigureCandidate[] candidates =
        {
            new(0, 100, 22, true, false, 11, true),
            new(1, 101, 28, false, true, 11, true),
            new(2, 102, 24, false, false, 11, true),
            new(3, 103, 31, true, false, 11, true),
            new(4, 104, 42, false, false, 11, true),
            new(5, 105, 18, true, false, 11, true),
            new(6, 106, 26, false, false, 11, true),
            new(7, 107, 35, false, false, 0, true),
            // Idle holders supply the per-side growth buffer that fission now also requires on top of the
            // 70% coverage floor (eff4831, 12c4c4c); without them no split can keep both sides buffered.
            new(8, 108, 29, false, false, 11, false),
            new(9, 109, 33, true, false, 11, false),
            new(10, 110, 21, false, false, 11, false),
            new(11, 111, 38, false, false, 11, false),
            new(12, 112, 27, false, false, 11, false),
            new(13, 113, 40, false, false, 11, false),
        };

        FissionCohortSample? balanced = null;
        for (ulong seed = 0; seed < 64 && balanced is null; seed++)
        {
            FissionCohortSample original = FissionCohortSampler.Select(seed, 0x5678, 2, 18, 14, 6, candidates);
            FissionCohortSample candidate = FissionCohortSampler.Select(seed, 0x5678, 2, 18, 14, 6, candidates,
                preserveCurrentToolCoverage: true);
            if (original.Success && !MeetsBothCoverage(original.DaughterFigures, candidates) &&
                candidate.Result.Contains("CoveragePreserved", StringComparison.Ordinal))
                balanced = candidate;
        }

        Assert.NotNull(balanced);
        Assert.True(balanced.Success, balanced.Result);
        Assert.Equal(6, balanced.DaughterFigures.Count(selected => selected));
        Assert.Contains("CoveragePreserved", balanced.Result);
        AssertCoverageAtLeastSeventyPercent(balanced.DaughterFigures, candidates, inDaughter: true);
        AssertCoverageAtLeastSeventyPercent(balanced.DaughterFigures, candidates, inDaughter: false);
        Assert.Contains(candidates, figure => balanced.DaughterFigures[figure.FigureIndex] && figure.Female && figure.Age is >= 14 and < 45);
        Assert.Contains(candidates, figure => balanced.DaughterFigures[figure.FigureIndex] &&
            (figure.Age >= 45 || figure.Age is >= 14 and < 45 && figure.Skilled));
    }

    [Fact]
    public void Coverage_preserving_fission_keeps_idle_iron_kit_share_balanced_without_moving_active_toolworkers()
    {
        FissionFigureCandidate[] candidates =
        {
            new(0, 100, 22, true, false, 11, true),
            new(1, 101, 28, false, true, 11, true),
            new(2, 102, 24, false, false, 11, true),
            new(3, 103, 31, true, false, 11, true),
            new(4, 104, 42, false, false, 11, false),
            new(5, 105, 18, true, false, 11, false),
            new(6, 106, 26, false, false, 0, false),
            new(7, 107, 35, false, false, 0, false),
            new(8, 108, 19, true, false, 0, false),
            new(9, 109, 47, false, false, 0, false),
        };

        FissionCohortSample? original = null, balanced = null;
        for (ulong seed = 0; seed < 64 && balanced is null; seed++)
        {
            FissionCohortSample before = FissionCohortSampler.Select(seed, 0x5678, 2, 18, 10, 6, candidates);
            FissionCohortSample after = FissionCohortSampler.Select(seed, 0x5678, 2, 18, 10, 6, candidates,
                preserveCurrentToolCoverage: true);
            if (before.Success && MeetsBothCoverage(before.DaughterFigures, candidates) &&
                after.Result.Contains("CoveragePreserved", StringComparison.Ordinal) &&
                KitHolderShareDifference(after.DaughterFigures, candidates) < KitHolderShareDifference(before.DaughterFigures, candidates))
            {
                original = before;
                balanced = after;
            }
        }

        Assert.NotNull(original);
        Assert.NotNull(balanced);
        Assert.Equal(6, balanced.DaughterFigures.Count(selected => selected));
        Assert.True(KitHolderShareDifference(balanced.DaughterFigures, candidates) <
            KitHolderShareDifference(original.DaughterFigures, candidates));
        AssertCoverageAtLeastSeventyPercent(balanced.DaughterFigures, candidates, inDaughter: true);
        AssertCoverageAtLeastSeventyPercent(balanced.DaughterFigures, candidates, inDaughter: false);
    }

    private static void AssertCoverageAtLeastSeventyPercent(bool[] daughter, FissionFigureCandidate[] candidates, bool inDaughter)
    {
        FissionFigureCandidate[] workers = candidates.Where(figure => figure.WorkedWithToolLastSeason && figure.Age >= 14 &&
            daughter[figure.FigureIndex] == inDaughter).ToArray();
        int equipped = workers.Count(figure => figure.Kit is 11 or 12);
        Assert.True(equipped * 10 >= workers.Length * 7, $"{equipped}/{workers.Length} active toolworkers have Iron kits");
    }

    private static bool MeetsBothCoverage(bool[] daughter, FissionFigureCandidate[] candidates)
        => MeetsCoverage(daughter, candidates, inDaughter: true) && MeetsCoverage(daughter, candidates, inDaughter: false);

    private static bool MeetsCoverage(bool[] daughter, FissionFigureCandidate[] candidates, bool inDaughter)
    {
        FissionFigureCandidate[] workers = candidates.Where(figure => figure.WorkedWithToolLastSeason && figure.Age >= 14 &&
            daughter[figure.FigureIndex] == inDaughter).ToArray();
        return workers.Length == 0 || workers.Count(figure => figure.Kit is 11 or 12) * 10 >= workers.Length * 7;
    }

    private static int KitHolderShareDifference(bool[] daughter, FissionFigureCandidate[] candidates)
    {
        int daughterCount = candidates.Count(figure => daughter[figure.FigureIndex]);
        int parentCount = candidates.Length - daughterCount;
        int daughterHolders = candidates.Count(figure => daughter[figure.FigureIndex] && figure.Kit is 11 or 12);
        int parentHolders = candidates.Count(figure => !daughter[figure.FigureIndex] && figure.Kit is 11 or 12);
        return Math.Abs(daughterHolders * parentCount - parentHolders * daughterCount);
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
