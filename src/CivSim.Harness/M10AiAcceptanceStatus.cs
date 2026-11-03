namespace CivSim.Harness;

internal static class M10AiAcceptanceStatus
{
    internal const int MinimumSeeds = 8;
    internal const int MinimumMinutes = 64;
    /// <summary>
    /// Gates that sit close to their bar (A2 against the opening scripts, B31) sample at least this many seeds (64 matches);
    /// 8 seeds carry about +/-12 points of noise and cannot resolve them.
    /// </summary>
    internal const int MinimumWideSampleSeeds = 32;

    internal enum Result
    {
        Diagnostic,
        AcceptanceQuality,
    }

    internal enum GateResult
    {
        Pass,
        Fail,
        Incomplete,
    }

    internal static Result Classify(int seeds, int minutes) =>
        seeds < MinimumSeeds || minutes < MinimumMinutes ? Result.Diagnostic : Result.AcceptanceQuality;

    internal static string FormatSummary(bool measuredPass, int seeds, int minutes)
        => FormatSummary(measuredPass ? GateResult.Pass : GateResult.Fail, seeds, minutes);

    internal static string FormatSummary(GateResult gateResult, int seeds, int minutes)
    {
        if (Classify(seeds, minutes) == Result.Diagnostic)
            return $"M10 measurable gates DIAGNOSTIC ONLY: acceptance requires at least {MinimumSeeds} seeds and {MinimumMinutes}-minute chapter horizon (got {seeds} seeds, {minutes} minutes).";

        return $"M10 measurable gates {gateResult.ToString().ToUpperInvariant()}; full B26-B30 result remains INCOMPLETE.";
    }

    internal static string FormatGateLabel(bool passed, int seeds, int minutes) =>
        Classify(seeds, minutes) == Result.Diagnostic ? "DIAGNOSTIC" : passed ? "PASS" : "FAIL";
}
