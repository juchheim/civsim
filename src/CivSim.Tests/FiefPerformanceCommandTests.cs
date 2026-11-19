using System;
using System.IO;
using CivSim.Harness;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

public sealed class FiefPerformanceCommandTests
{
    [Fact]
    public void Seed_file_accepts_comments_and_rejects_duplicates_or_empty_input()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fief-perf-seeds-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, "# frozen seeds\n42\n  # comment\n17 # note\n");
            Assert.Equal(new ulong[] { 42, 17 }, FiefPerformanceCommand.ReadSeeds(path));
            File.WriteAllText(path, "9\n9\n");
            Assert.Throws<FormatException>(() => FiefPerformanceCommand.ReadSeeds(path));
            File.WriteAllText(path, " # no seeds\n");
            Assert.Throws<FormatException>(() => FiefPerformanceCommand.ReadSeeds(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Quantiles_use_linear_interpolation_and_empty_samples_are_zero()
    {
        Assert.Equal(2.5, FiefPerformanceCommand.Quantile(new[] { 1d, 2d, 3d, 4d }, .5), 8);
        Assert.Equal(3.7, FiefPerformanceCommand.Quantile(new[] { 1d, 2d, 3d, 4d }, .9), 8);
        Assert.Equal(0, FiefPerformanceCommand.Quantile(Array.Empty<double>(), .95));
    }

    [Fact]
    public void Profiling_one_minute_preserves_the_normal_Fief_simulation_checksum()
    {
        const ulong seed = 14681;
        EconomyArchetype[] archetypes =
            { EconomyArchetype.Builder, EconomyArchetype.Grower, EconomyArchetype.Warlord, EconomyArchetype.Sage };
        var controls = new CivControl[archetypes.Length];
        for (int civ = 0; civ < controls.Length; civ++) controls[civ] = CivControl.Ai(Difficulty.Warlord, archetypes[civ]);
        MatchModel normal = MatchModel.Create(seed, TestPack.Pack, WorldGenerator.Generate(seed), archetypes, controls, rules: ChapterRules.Fief);
        var runner = new SimRunner(normal);
        runner.Advance(600);

        FiefPerformanceCommand.MatchMetrics measured = FiefPerformanceCommand.RunOne(TestPack.Pack, seed, 1, 0, grantE5: false);

        Assert.Equal(runner.Tick, measured.FinalTick);
        Assert.Equal(runner.CurrentChecksum, measured.FinalChecksum);
        Assert.Equal(600, measured.TickMilliseconds.Count);
        Assert.Equal(600, measured.ExpectedTimedTicks);
        Assert.Equal(1, measured.FirstTimedTick);
        Assert.Equal("horizon", measured.StopReason);
        Assert.False(measured.Censored);
        Assert.Equal(600, measured.FinalTick);
        Assert.All(measured.TickMilliseconds, ms => Assert.True(ms >= 0));
    }
}
