using CivSim.Harness;

namespace CivSim.Tests;

public sealed class AcceptanceSampleStatusTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(9, true)]
    [InlineData(10, false)]
    [InlineData(11, false)]
    public void S13SampleClassificationUsesTenMinuteAcceptanceMinimum(int sampleMinutes, bool diagnostic)
    {
        Assert.Equal(diagnostic, AcceptanceCommand.IsDiagnosticSample(sampleMinutes));
    }

    [Fact]
    public void ShortS13SampleIsDiagnosticAndMakesOverallResultIncomplete()
    {
        Assert.Equal("S13 DIAGNOSTIC (sample shorter than 10-minute acceptance window)",
            AcceptanceCommand.GetS13StatusLine(diagnostic: true, passed: true));

        AcceptanceCommand.OverallStatus status = AcceptanceCommand.GetOverallStatus(gatesPassed: true, hasDiagnosticS13: true);
        Assert.Equal(AcceptanceCommand.OverallStatus.Incomplete, status);
        Assert.Equal("ACCEPTANCE INCOMPLETE", AcceptanceCommand.GetOverallStatusLine(status));
        Assert.Equal(2, AcceptanceCommand.GetExitCode(status));
    }

    [Theory]
    [InlineData(true, "S13 PASS")]
    [InlineData(false, "S13 FAIL")]
    public void FullS13SampleKeepsAcceptanceStatus(bool passed, string expectedLine)
    {
        Assert.Equal(expectedLine, AcceptanceCommand.GetS13StatusLine(diagnostic: false, passed));

        AcceptanceCommand.OverallStatus status = AcceptanceCommand.GetOverallStatus(gatesPassed: passed, hasDiagnosticS13: false);
        Assert.Equal(passed ? "ACCEPTANCE GATES PASSED" : "ACCEPTANCE GATES FAILED",
            AcceptanceCommand.GetOverallStatusLine(status));
        Assert.Equal(passed ? 0 : 1, AcceptanceCommand.GetExitCode(status));
    }
}
