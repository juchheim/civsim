using System;
using System.Collections.Generic;
using System.Globalization;
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
    private const double Key4Target = 43.0;
    private const double E3Target = 13.0;
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

        var key4All = new List<double>();
        var e3All = new List<double>();
        bool identical = true;
        var ends = new List<double>();
        int yearCap = 0;
        var text = new StringBuilder();
        text.AppendLine($"M10 check: {list.Count} seed(s), four Balanced civilizations on the Council, human seat {(humanSeat < 0 ? "none (AI-only: countdown and Year cap)" : humanSeat.ToString(CultureInfo.InvariantCulture))}");
        for (int s = 0; s < list.Count; s++)
        {
            Run r = first[s];
            text.AppendLine($"seed {r.Seed}:");
            text.AppendLine($"  {"civ",-4}{"KEY-3",8}{"KEY-4",8}{"E3 min",8}{"done",6}");
            for (int c = 0; c < r.Key3.Length; c++)
            {
                double k3 = r.Key3[c], k4 = r.Key4[c];
                double e3 = double.IsNaN(k3) || double.IsNaN(k4) ? double.NaN : k4 - k3;
                if (!double.IsNaN(k4)) key4All.Add(k4);
                if (!double.IsNaN(e3)) e3All.Add(e3);
                string late = !double.IsNaN(k4) && k4 * 600 >= r.End.EndTick ? " *after the chapter end" : "";
                text.AppendLine($"  {c,-4}{Fmt(k3),8}{Fmt(k4),8}{Fmt(e3),8}{r.Done[c],6}{late}");
            }
            MatchEnd end = r.End;
            double endMinutes = end.EndTick / 600.0;
            ends.Add(endMinutes);
            if (end.Reason == MatchEndReason.YearCap) yearCap++;
            bool same = first[s].End == again[s].End && first[s].Checksum == again[s].Checksum && first[s].Ticks == again[s].Ticks;
            identical &= same;
            text.AppendLine($"  chapter end: minute {endMinutes:F1} ({end.Reason}{(end.Civ >= 0 ? $", civ {end.Civ}" : "")}); first rival KEY-4 in season {end.CountdownStartSeason}" +
                            $"; replay {(same ? "identical" : "DIFFERENT")} (checksum {r.Checksum:x16})" + (r.OverByRule ? "" : "  [the chapter did not end within --minutes]"));
        }

        bool pass = true;
        double key4Median = Median(key4All), e3Median = Median(e3All);
        int civs = list.Count * 4;
        bool key4Ok = key4All.Count == civs && key4Median >= Key4Target * 0.75 && key4Median <= Key4Target * 1.25;
        bool e3Ok = e3All.Count == civs && e3Median >= 11.5 && e3Median <= 14.5;
        bool e3Each = e3All.Count == civs && e3All.TrueForAll(x => x >= E3Target * 0.75 && x <= E3Target * 1.25);
        double endMedian = Median(ends);
        text.AppendLine($"B5 KEY-4 Council median {Fmt(key4Median)} (target {Key4Target:F0} +-25% = 32.3-53.8; {key4All.Count}/{civs} civilizations finished): {(key4Ok ? "PASS" : "FAIL")}");
        text.AppendLine($"B5 E3 duration median {Fmt(e3Median)} (target 11.5-14.5): {(e3Ok ? "PASS" : "FAIL")}");
        text.AppendLine($"B5 every civilization's E3 duration within 13.0 +-25% (9.75-16.25): {(e3Each ? "PASS" : "FAIL")}");
        text.AppendLine($"B6 chapter end median minute {Fmt(endMedian)} over {list.Count} seed(s) (target 44-48; B6 itself is judged over 32 seeds): {(endMedian >= 44 && endMedian <= 48 ? "in band" : "outside band")}");
        text.AppendLine($"B6 matches reaching Year 64: {yearCap}/{list.Count} (target <= 10%)");
        text.AppendLine($"B6 the end rule is replay-identical: {(identical ? "PASS" : "FAIL")}");
        pass = key4Ok && e3Ok && e3Each && identical;
        text.AppendLine(pass ? "M10 research check PASS" : "M10 research check FAIL (reported, not tuned; balance is a later work package)");
        Console.Write(text.ToString());
        return pass ? 0 : 1;
    }

    private static Run Play(ulong seed, ContentPack pack, EconomyArchetype[] archetypes, int maxMinutes, int humanSeat)
    {
        MatchModel match = MatchModel.Create(seed, pack, archetypes);
        var runner = new SimRunner(match);
        long limit = maxMinutes * 600L;
        ResearchSystem r = match.Research;
        int civs = r.CivCount;
        int key3 = r.Catalog.IndexOf("KEY-3"), key4 = r.Catalog.IndexOf("KEY-4");
        // The timing check wants every civilization's KEY-4, so the match keeps running past the chapter end (which is a pure
        // function of the recorded completion ticks and is read afterwards) until all four finish or the bound is reached.
        while (runner.Tick < limit)
        {
            runner.Advance(1);
            if (runner.Tick % ResearchSystem.SeasonTicks != 0) continue;
            bool all = true;
            for (int c = 0; c < civs; c++) all &= r.IsDone(c, key4);
            if (all) break;
        }
        MatchEnd end = match.EndOfMatch(humanSeat);
        // The end is read from recorded completions, so it is known as soon as a clause has fired; only an undecided Year 64 needs the clock.
        bool over = end.Reason != MatchEndReason.YearCap || end.IsOver(runner.Tick);
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
