using System;
using System.IO;
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

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public void ShortS13SampleSkipsTickGateAndReportsDiagnostic(int sampleMinutes)
    {
        bool gateExecuted = false;
        using var output = new StringWriter();
        TextWriter original = Console.Out;
        Console.SetOut(output);
        try
        {
            bool passed = AcceptanceCommand.RunTickGateForHorizon(sampleMinutes, () =>
            {
                gateExecuted = true;
                return false;
            }, out bool diagnostic);

            Assert.True(passed);
            Assert.True(diagnostic);
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.False(gateExecuted);
        Assert.Contains("S13 DIAGNOSTIC (sample shorter than 10-minute acceptance window)", output.ToString());
    }

    [Fact]
    public void FullS13SampleExecutesTickGate()
    {
        bool gateExecuted = false;

        bool passed = AcceptanceCommand.RunTickGateForHorizon(10, () =>
        {
            gateExecuted = true;
            return false;
        }, out bool diagnostic);

        Assert.False(passed);
        Assert.False(diagnostic);
        Assert.True(gateExecuted);
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
    [InlineData(1, true)]
    [InlineData(63, true)]
    [InlineData(64, false)]
    [InlineData(65, false)]
    public void S15HorizonClassificationUsesSixtyFourMinuteAcceptanceMinimum(int minutes, bool diagnostic)
    {
        Assert.Equal(diagnostic, AcceptanceCommand.IsDiagnosticSoakSample(minutes));
    }

    [Fact]
    public void ShortS15HorizonIsDiagnosticAndMakesOverallResultIncomplete()
    {
        Assert.Equal("S15 DIAGNOSTIC (horizon shorter than 64-minute acceptance window)",
            AcceptanceCommand.GetS15StatusLine(diagnostic: true, passed: true));

        AcceptanceCommand.OverallStatus status = AcceptanceCommand.GetOverallStatus(
            gatesPassed: true, hasDiagnosticS13: false, hasDiagnosticS15: true);
        Assert.Equal(AcceptanceCommand.OverallStatus.Incomplete, status);
        Assert.Equal("ACCEPTANCE INCOMPLETE", AcceptanceCommand.GetOverallStatusLine(status));
        Assert.Equal(2, AcceptanceCommand.GetExitCode(status));
    }

    [Fact]
    public void ShortS15HorizonSkipsSoakExecution()
    {
        bool soakExecuted = false;

        bool passed = AcceptanceCommand.RunSoakGateForHorizon(10, () =>
        {
            soakExecuted = true;
            return false;
        }, out bool diagnostic);

        Assert.True(diagnostic);
        Assert.True(passed);
        Assert.False(soakExecuted);
    }

    [Theory]
    [InlineData(999, 64, true)]
    [InlineData(1_000, 63, true)]
    [InlineData(1_000, 64, false)]
    [InlineData(1_001, 64, true)]
    public void S1RequiresExactlyOneThousandMatchesAndSixtyFourMinuteHorizon(int matches, int minutes, bool diagnostic)
    {
        Assert.Equal(diagnostic, AcceptanceCommand.IsDiagnosticReplaySample(matches, minutes));
    }

    [Fact]
    public void DiagnosticS1MakesOverallAcceptanceIncomplete()
    {
        AcceptanceCommand.OverallStatus status = AcceptanceCommand.GetOverallStatus(
            gatesPassed: true, hasDiagnosticS13: false, hasDiagnosticS1: true);

        Assert.Equal(AcceptanceCommand.OverallStatus.Incomplete, status);
        Assert.Equal("ACCEPTANCE INCOMPLETE", AcceptanceCommand.GetOverallStatusLine(status));
        Assert.Equal(2, AcceptanceCommand.GetExitCode(status));
    }

    [Fact]
    public void UndersizedS1RequestSkipsReplayWork()
    {
        bool replayExecuted = false;
        bool passed = AcceptanceCommand.RunReplayGateForHorizon(999, 64, () =>
        {
            replayExecuted = true;
            return false;
        }, out bool diagnostic);

        Assert.True(diagnostic);
        Assert.True(passed);
        Assert.False(replayExecuted);
    }

    [Fact]
    public void FullS1RequestInvokesReplayGateCallback()
    {
        bool replayExecuted = false;

        bool passed = AcceptanceCommand.RunReplayGateForHorizon(1_000, 64, () =>
        {
            replayExecuted = true;
            return false;
        }, out bool diagnostic);

        Assert.False(diagnostic);
        Assert.False(passed);
        Assert.True(replayExecuted);
    }

    [Fact]
    public void ChapterReplayMustStopAtExactEndTick()
    {
        var end = new CivSim.Sim.Research.MatchEnd(
            CivSim.Sim.Research.MatchEndReason.RivalCountdown, 10, 1, 0, 1);

        Assert.True(AcceptanceCommand.IsExactChapterEnd(end.EndTick, end));
        Assert.False(AcceptanceCommand.IsExactChapterEnd(end.EndTick - 1, end));
        Assert.False(AcceptanceCommand.IsExactChapterEnd(end.EndTick + 1, end));
    }

    [Theory]
    [InlineData(true, "S15 PASS")]
    [InlineData(false, "S15 FAIL")]
    public void FullS15HorizonKeepsAcceptanceStatus(bool passed, string expectedLine)
    {
        Assert.Equal(expectedLine, AcceptanceCommand.GetS15StatusLine(diagnostic: false, passed));

        AcceptanceCommand.OverallStatus status = AcceptanceCommand.GetOverallStatus(
            gatesPassed: passed, hasDiagnosticS13: false, hasDiagnosticS15: false);
        Assert.Equal(passed ? "ACCEPTANCE GATES PASSED" : "ACCEPTANCE GATES FAILED",
            AcceptanceCommand.GetOverallStatusLine(status));
        Assert.Equal(passed ? 0 : 1, AcceptanceCommand.GetExitCode(status));
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
