using CivSim.Harness;
using Xunit;

namespace CivSim.Tests;

public sealed class BronzeAiGateTests
{
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
