using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;

namespace CivSim.Harness;

/// <summary>
/// <c>research --check-m10</c> (docs/MVP/22 §16 B5 and B6): four Balanced civilizations on the Council play until the Bronze
/// chapter ends (docs/MVP/22 §3.1) over each seed, and the run is repeated from scratch to prove the end rule is
/// replay-identical. Prints KEY-3 and KEY-4 times, E3 duration per civilization and the chapter end, with PASS or FAIL.
/// It reports; it never tunes.
/// </summary>
internal static class ResearchM10
{
    private const double Key4Target = 61.25;
    private const double E3Target = 19.75;
    private const int B6AcceptanceSeeds = 32;
    private static readonly ulong[] DefaultSeeds = { 20261019UL, 777UL };

    private sealed class Run
    {
        public ulong Seed;
        public double[] Key3 = Array.Empty<double>(), Key4 = Array.Empty<double>();
        public int[] Done = Array.Empty<int>();
        public MatchEnd End;
        public long Ticks;
        public ulong Checksum;
        public bool OverByRule;
        public int CountdownSeasons;
    }

    public static int Execute(ContentPack pack, IReadOnlyList<ulong>? seeds, int maxMinutes, int humanSeat)
    {
        IReadOnlyList<ulong> list = seeds is { Count: > 0 } ? seeds : DefaultSeeds;
        var archetypes = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        var first = new Run[list.Count];
        var again = new Run[list.Count];
        // Each match is independent; the second pass is the "replay": same seed, no commands, built from scratch.
        Parallel.For(0, list.Count * 2, i =>
        {
            int s = i / 2;
            Run run = Play(list[s], pack, archetypes, maxMinutes, humanSeat);
            if (i % 2 == 0) first[s] = run; else again[s] = run;
        });

        var b5Key4 = new List<double>();
        var b5E3 = new List<double>();
        bool identical = true;
        bool b6EndRulesOk = true;
        var ends = new List<double>();
        int closed = 0;
        var text = new StringBuilder();
        text.AppendLine($"M10 check: {list.Count} seed(s), four Balanced civilizations on the Council, human seat {(humanSeat < 0 ? "none (AI-only: the rival countdown ends the chapter)" : humanSeat.ToString(CultureInfo.InvariantCulture))}");
        for (int s = 0; s < list.Count; s++)
        {
            Run r = first[s];
            text.AppendLine($"seed {r.Seed}:");
            text.AppendLine($"  {"civ",-4}{"KEY-3",8}{"KEY-4",8}{"E3 min",8}{"done",6}");
            for (int c = 0; c < r.Key3.Length; c++)
            {
                double k3 = r.Key3[c], k4 = r.Key4[c];
                double e3 = double.IsNaN(k3) || double.IsNaN(k4) ? double.NaN : k4 - k3;
                if (list[s] == DefaultSeeds[0] || list[s] == DefaultSeeds[1])
                {
                    if (!double.IsNaN(k4)) b5Key4.Add(k4);
                    if (!double.IsNaN(e3)) b5E3.Add(e3);
                }
                string late = !double.IsNaN(k4) && k4 * 600 >= r.End.EndTick ? " *after the chapter end" : "";
                text.AppendLine($"  {c,-4}{Fmt(k3),8}{Fmt(k4),8}{Fmt(e3),8}{r.Done[c],6}{late}");
            }
            MatchEnd end = r.End;
            bool closedInHorizon = end.HasEnd && r.OverByRule;
            double endMinutes = end.HasEnd ? end.EndTick / 600.0 : double.NaN;
            if (closedInHorizon) { ends.Add(endMinutes); closed++; }
            bool same = first[s].End == again[s].End && first[s].Checksum == again[s].Checksum && first[s].Ticks == again[s].Ticks;
            identical &= same;
            bool followsRule = IsB6EndRuleCorrect(end, r.CountdownSeasons);
            b6EndRulesOk &= followsRule;
            string endText = end.HasEnd ? $"minute {endMinutes:F1} ({end.Reason}{(end.Civ >= 0 ? $", civ {end.Civ}" : "")})" : "none (no keystone clause fired; the chapter is open)";
            text.AppendLine($"  chapter end: {endText}; first rival KEY-4 in season {end.CountdownStartSeason}" +
                            $"; replay {(same ? "identical" : "DIFFERENT")} (checksum {r.Checksum:x16})" + (closedInHorizon ? "" : "  [the chapter did not end within --minutes]"));
        }

        bool pass = true;
        bool b5AcceptanceSample = HasB5AcceptanceSeeds(list);
        int b5Civs = DefaultSeeds.Length * 4;
        double key4Median = Median(b5Key4), e3Median = Median(b5E3);
        bool key4Ok = b5AcceptanceSample && b5Key4.Count == b5Civs && key4Median >= 57.0 && key4Median <= 64.0;
        bool e3Ok = b5AcceptanceSample && b5E3.Count == b5Civs && e3Median >= E3Target * 0.75 && e3Median <= E3Target * 1.25;
        bool e3Each = b5AcceptanceSample && b5E3.Count == b5Civs && b5E3.TrueForAll(x => x >= E3Target * 0.75 && x <= E3Target * 1.25);
        double endMedian = Median(ends);
        bool b6ClosedOk = IsB6ClosedRateInRange(closed, list.Count);
        bool b6AcceptanceSample = list.Count >= B6AcceptanceSeeds;
        text.AppendLine($"B5 KEY-4 Council median {Fmt(key4Median)} (projection {Key4Target:F2}; acceptance window 57-64; {b5Key4.Count}/{b5Civs} civilizations finished): {GateStatus(key4Ok, b5AcceptanceSample)}");
        text.AppendLine($"B5 E3 duration median {Fmt(e3Median)} (projection {E3Target:F2} +-25% = {E3Target * 0.75:F2}-{E3Target * 1.25:F2}): {GateStatus(e3Ok, b5AcceptanceSample)}");
        text.AppendLine($"B5 every civilization's E3 duration within {E3Target:F2} +-25%: {GateStatus(e3Each, b5AcceptanceSample)}");
        text.AppendLine($"B5 acceptance sample: {b5Key4.Count}/{b5Civs} civilization timings from seeds {DefaultSeeds[0]} and {DefaultSeeds[1]} {(b5AcceptanceSample ? "(complete)" : "(not included)")}");
        text.AppendLine($"B6 chapter end median minute {Fmt(endMedian)} (observed; first rival KEY-4 + {first[0].CountdownSeasons} seasons, or the human keystone; every sample's end matches the rule): {GateStatus(b6EndRulesOk, b6AcceptanceSample)}");
        text.AppendLine($"B6 chapters closed by the countdown or the human keystone within --minutes: {closed}/{list.Count} (target >= 60%): {GateStatus(b6ClosedOk, b6AcceptanceSample)}");
        text.AppendLine($"B6 the end rule is replay-identical: {GateStatus(identical, b6AcceptanceSample)}");
        text.AppendLine($"B6 acceptance sample: {list.Count}/{B6AcceptanceSeeds} seeds {(b6AcceptanceSample ? "(complete)" : "(diagnostic only)")}");
        bool b5Pass = b5AcceptanceSample && key4Ok && e3Ok && e3Each && identical;
        bool b6Pass = b6AcceptanceSample && b6EndRulesOk && b6ClosedOk && identical;
        pass = b6AcceptanceSample
            ? b6Pass && (!b5AcceptanceSample || b5Pass)
            : b5AcceptanceSample && b5Pass;
        if (b6AcceptanceSample && b6Pass && !b5AcceptanceSample)
            text.AppendLine("B6 chapter-end acceptance PASS; B5 remains outside its documented two-seed timing sample.");
        else if (pass && !b6AcceptanceSample)
            text.AppendLine("B5 research timing PASS; B6 remains INCOMPLETE until its 32-seed sample.");
        else if (!b5AcceptanceSample && !b6AcceptanceSample)
            text.AppendLine("M10 research check INCOMPLETE: run B5 with seeds 20261019,777 or provide all 32 B6 seeds.");
        else
            text.AppendLine(pass ? "M10 research check PASS" : "M10 research check FAIL (reported, not tuned; balance is a later work package)");
        Console.Write(text.ToString());
        return pass ? 0 : 1;
    }

    /// <summary>
    /// The B6 end-rule check. There is no year cap (removed 2026-11-08, docs/MVP/28 §17), so a chapter ends only by a countdown or the
    /// human's keystone: with no rival keystone recorded the chapter must report the open state (<see cref="MatchEnd.Open"/>), and with
    /// one it must be closed by exactly <paramref name="countdownSeasons"/> after the first rival's season, or earlier by the human clause.
    /// Whether the chapter closed within the horizon is a separate clause (<see cref="IsB6ClosedRateInRange"/>).
    /// </summary>
    internal static bool IsB6EndRuleCorrect(MatchEnd end, int countdownSeasons)
    {
        if (countdownSeasons < 0)
            return false;
        if (end.CountdownStartSeason < 0)
        {
            // Nobody but possibly the human finished the keystone.
            if (end.CountdownCiv != -1) return false;
            if (end.Reason == MatchEndReason.HumanKeystone) return end.Civ >= 0 && end.EndSeason >= 0 && end.HasEnd;
            return end.Reason == MatchEndReason.None && end.EndSeason == MatchEnd.NoEndSeason && end.Civ == -1;
        }

        int countdownEnd = end.CountdownStartSeason + countdownSeasons;
        if (end.CountdownCiv < 0) return false;
        if (end.Reason == MatchEndReason.HumanKeystone) return end.Civ >= 0 && end.EndSeason <= countdownEnd;
        return end.Reason == MatchEndReason.RivalCountdown && end.EndSeason == countdownEnd && end.Civ == end.CountdownCiv;
    }

    /// <summary>
    /// B6 asks that at least 60% of the Balanced matches close within the horizon by the countdown or the human keystone. This replaces
    /// the old "at most 40% reach the Year-64 cap" clause (itself relaxed from 10% on 2026-11-03, docs/MVP/07d): the cap is gone, so
    /// the same tolerance is now stated as the share of chapters that actually close. A match nobody finishes is reported, not capped.
    /// </summary>
    internal static bool IsB6ClosedRateInRange(int closedMatches, int matches) =>
        matches > 0 && closedMatches >= 0 && closedMatches <= matches && (long)closedMatches * 5 >= (long)matches * 3;

    internal static bool HasB5AcceptanceSeeds(IReadOnlyList<ulong> seeds)
    {
        bool first = false, second = false;
        for (int i = 0; i < seeds.Count; i++)
        {
            first |= seeds[i] == DefaultSeeds[0];
            second |= seeds[i] == DefaultSeeds[1];
        }
        return first && second;
    }

    private static string GateStatus(bool pass, bool acceptanceSample) =>
        acceptanceSample ? (pass ? "PASS" : "FAIL") : (pass ? "diagnostic" : "diagnostic miss");

    /// <summary>Single-seed full chapter replay canary, separate from the multi-seed balance gate.</summary>
    public static int ExecuteGolden(ContentPack pack, string path, bool write)
    {
        var archetypes = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        const ulong seed = 20261019UL;
        Run? first = null, replay = null;
        Parallel.For(0, 2, i =>
        {
            Run run = Play(seed, pack, archetypes, 120, -1, stopAtChapterEnd: true);
            if (i == 0) first = run; else replay = run;
        });
        Run initial = first!;
        Run second = replay!;
        bool same = initial.End == second.End && initial.Checksum == second.Checksum && initial.Ticks == second.Ticks;
        if (!same)
        {
            Console.Error.WriteLine("BRONZE GOLDEN REPLAY FAILED: fresh full-chapter runs differed.");
            return 1;
        }
        string text = string.Join('\n',
            "bronze-v1",
            $"seed={seed}",
            $"ticks={initial.Ticks}",
            $"end={initial.End.Reason} endTick={initial.End.EndTick} civ={initial.End.Civ} countdownStartSeason={initial.End.CountdownStartSeason}",
            $"checksum={initial.Checksum:x16}");
        if (write)
        {
            File.WriteAllText(path, text + "\n");
            Console.WriteLine($"wrote golden {path}");
        }
        else
        {
            string expected = File.ReadAllText(path).Replace("\r\n", "\n").Trim();
            if (expected != text)
            {
                Console.Error.WriteLine("BRONZE GOLDEN CHECK FAILED\nexpected:\n" + expected + "\nactual:\n" + text);
                return 1;
            }
            Console.WriteLine("Bronze full-chapter golden check passed (two fresh runs identical).");
        }
        Console.WriteLine(text);
        return 0;
    }

    private static Run Play(ulong seed, ContentPack pack, EconomyArchetype[] archetypes, int maxMinutes, int humanSeat, bool stopAtChapterEnd = false)
    {
        MatchModel match = MatchModel.Create(seed, pack, archetypes);
        // This acceptance path compares the final checksum only; retaining the every-50-tick
        // history adds one full-model hash per interval without contributing to its result.
        var runner = new SimRunner(match, int.MaxValue);
        long limit = maxMinutes * 600L;
        ResearchSystem r = match.Research;
        int civs = r.CivCount;
        int key3 = r.Catalog.IndexOf("KEY-3"), key4 = r.Catalog.IndexOf("KEY-4");
        // The timing check wants every civilization's KEY-4, so it keeps running past the chapter end until all four finish
        // or the bound is reached. The golden path opts out and stops at the exact chapter-ending season instead.
        while (runner.Tick < limit)
        {
            runner.Advance(1);
            if (runner.Tick % ResearchSystem.SeasonTicks != 0) continue;
            bool endReached = match.EndOfMatch(humanSeat).IsOver(runner.Tick);
            if (stopAtChapterEnd && endReached) break;
            bool all = true;
            for (int c = 0; c < civs; c++) all &= r.IsDone(c, key4);
            // B5 needs every KEY-4 completion; B6 also needs the match to reach the actual chapter end.
            // When all four Keystones finish before the 12-season countdown, keep simulating through that countdown.
            if (all && endReached) break;
        }
        MatchEnd end = match.EndOfMatch(humanSeat);
        // The end is read from recorded completions, so it is known as soon as a clause has fired; a chapter nobody has finished is still open and needs the clock (--minutes).
        bool over = end.IsOver(runner.Tick);
        var run = new Run
        {
            Seed = seed,
            Key3 = new double[civs],
            Key4 = new double[civs],
            Done = new int[civs],
            End = end,
            Ticks = runner.Tick,
            Checksum = runner.CurrentChecksum,
            OverByRule = over,
            CountdownSeasons = r.Catalog.ChapterCountdownSeasons,
        };
        for (int c = 0; c < civs; c++)
        {
            run.Key3[c] = r.IsDone(c, key3) ? r.CompletedTick(c, key3) / 600.0 : double.NaN;
            run.Key4[c] = r.IsDone(c, key4) ? r.CompletedTick(c, key4) / 600.0 : double.NaN;
            run.Done[c] = r.DoneCount(c);
        }
        return run;
    }

    private static double Median(List<double> values)
    {
        if (values.Count == 0) return double.NaN;
        var sorted = new List<double>(values);
        sorted.Sort();
        int n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2;
    }

    private static string Fmt(double v) => double.IsNaN(v) ? "-" : v.ToString("F1", CultureInfo.InvariantCulture);
}
