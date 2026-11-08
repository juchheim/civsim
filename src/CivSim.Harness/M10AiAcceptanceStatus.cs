using System;

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
    /// <summary>
    /// A4 (Chieftain against Warlord, 25 to 40%) is judged on this many seeds (80 mirrored matches), the block size its tuning used (docs/MVP/14 §9.2). On the
    /// 16 matches of an 8-seed run it read 50 to 56% on three builds that win 35 to 38% over 80 (docs/MVP/28 §12); the bar is unchanged.
    /// </summary>
    internal const int MinimumA4Seeds = 40;

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

    /// <summary>
    /// The project owner accepted A2 Grower as passed on 2026-11-03 (docs/MVP/07d): the exact 32-seed cohort reads 44/64 = 68.75%
    /// against the 70% bar, while 144 pooled matches read 72.2%. The exception holds only while Warlord still wins at least this
    /// share of the Grower duel, so a real regression below the observed near-miss band fails again.
    /// </summary>
    internal const double AcceptedA2GrowerFloor = 0.65;

    /// <summary>True when a failed A2 line is the owner-accepted Grower near-miss (and no worse than the floor).</summary>
    internal static bool IsAcceptedException(string gate, double winRate) =>
        gate == "A2 grower" && winRate >= AcceptedA2GrowerFloor;

    /// <summary>
    /// B29 scores a KEY-4 forecast only once KEY-4 completes, and on the 8-seed cohort six seats finish KEY-4 after minute 64, so
    /// an integrated check that stops at the capture horizon can never complete B29. Acceptance-quality runs keep the same
    /// simulations going to this minute (the standalone `--check-b29 --observation-minutes 100` run leaves none censored).
    /// </summary>
    internal const int B29ObservationMinutes = 100;

    /// <summary>The observation window the integrated check gives B29: the standard window, or none (capture horizon) for a diagnostic sample.</summary>
    internal static int? ResolveB29ObservationMinutes(int seeds, int minutes) =>
        Classify(seeds, minutes) == Result.Diagnostic ? null : Math.Max(minutes, B29ObservationMinutes);

    internal static string FormatGateLabel(bool passed, int seeds, int minutes) =>
        Classify(seeds, minutes) == Result.Diagnostic ? "DIAGNOSTIC" : passed ? "PASS" : "FAIL";
}
