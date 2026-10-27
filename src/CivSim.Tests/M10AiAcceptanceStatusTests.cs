using CivSim.Harness;

namespace CivSim.Tests;

public sealed class M10AiAcceptanceStatusTests
{
    [Fact]
    public void Missing_M10_sample_options_default_each_dimension_to_quick_diagnostic_values()
    {
        Assert.Equal((1, 2), AiCommand.ResolveM10Sample(null, null));
        Assert.Equal((8, 2), AiCommand.ResolveM10Sample(8, null));
        Assert.Equal((1, 64), AiCommand.ResolveM10Sample(null, 64));
    }

    [Fact]
    public void Explicit_M10_sample_options_are_preserved()
    {
        Assert.Equal((8, 64), AiCommand.ResolveM10Sample(8, 64));
    }

    [Fact]
    public void Check_b27_defaults_to_one_full_horizon_seed_for_a_focused_diagnostic()
    {
        Assert.Equal((1, 64), AiCommand.ResolveB27Sample(null, null));
        Assert.Equal((2, 64), AiCommand.ResolveB27Sample(2, null));
        Assert.Equal((1, 10), AiCommand.ResolveB27Sample(null, 10));
        Assert.Equal((8, 64), AiCommand.ResolveB27Sample(8, 64));
    }

    [Fact]
    public void Core_check_summary_uses_the_invoking_gate_name()
    {
        Assert.Equal("M10 AI CORE CHECK DIAGNOSTIC ONLY",
            AiCommand.FormatCoreCheckSummary("M10 AI CORE", diagnostic: true, pass: false));
        Assert.Equal("M6 CHECK PASSED",
            AiCommand.FormatCoreCheckSummary("M6", diagnostic: false, pass: true));
    }

    [Fact]
    public void Fewer_than_eight_seeds_are_diagnostic_even_if_metrics_pass()
    {
        Assert.Equal(M10AiAcceptanceStatus.Result.Diagnostic, M10AiAcceptanceStatus.Classify(7, 64));
        Assert.Equal(
            "M10 measurable gates DIAGNOSTIC ONLY: acceptance requires at least 8 seeds and 64-minute chapter horizon (got 7 seeds, 64 minutes).",
            M10AiAcceptanceStatus.FormatSummary(measuredPass: true, seeds: 7, minutes: 64));
    }

    [Fact]
    public void Horizon_shorter_than_sixty_four_minutes_is_diagnostic_even_if_metrics_pass()
    {
        Assert.Equal(M10AiAcceptanceStatus.Result.Diagnostic, M10AiAcceptanceStatus.Classify(8, 63));
        Assert.Equal(
            "M10 measurable gates DIAGNOSTIC ONLY: acceptance requires at least 8 seeds and 64-minute chapter horizon (got 8 seeds, 63 minutes).",
            M10AiAcceptanceStatus.FormatSummary(measuredPass: true, seeds: 8, minutes: 63));
    }

    [Theory]
    [InlineData(7, 64, true)]
    [InlineData(7, 64, false)]
    [InlineData(8, 63, true)]
    [InlineData(8, 63, false)]
    public void Every_gate_label_is_diagnostic_for_an_under_sized_sample(int seeds, int minutes, bool passed)
    {
        Assert.Equal("DIAGNOSTIC", M10AiAcceptanceStatus.FormatGateLabel(passed, seeds, minutes));
    }

    [Theory]
    [InlineData(true, "PASS")]
    [InlineData(false, "FAIL")]
    public void Full_window_gate_labels_preserve_pass_and_fail(bool passed, string expected)
    {
        Assert.Equal(expected, M10AiAcceptanceStatus.FormatGateLabel(passed, seeds: 8, minutes: 64));
    }

    [Theory]
    [InlineData(true, "M10 measurable gates PASS; full B26-B30 result remains INCOMPLETE.")]
    [InlineData(false, "M10 measurable gates FAIL; full B26-B30 result remains INCOMPLETE.")]
    public void Full_eight_seed_chapter_window_preserves_existing_status(bool measuredPass, string expected)
    {
        Assert.Equal(M10AiAcceptanceStatus.Result.AcceptanceQuality, M10AiAcceptanceStatus.Classify(8, 64));
        Assert.Equal(expected, M10AiAcceptanceStatus.FormatSummary(measuredPass, seeds: 8, minutes: 64));
    }
}
