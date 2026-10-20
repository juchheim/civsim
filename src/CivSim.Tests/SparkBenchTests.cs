using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using Xunit.Abstractions;

namespace CivSim.Tests;

/// <summary>
/// The Spark bench (docs/MVP/14 §10.3, research part): smart-AI civilizations against the Council bot on mirrored seats.
/// The quick check runs in every test pass; the full bench (eight seeds, every difficulty, 33 minutes) runs when
/// CIVSIM_BENCH=1 and prints the margins (CIVSIM_BENCH_OUT=FILE also writes them to a file).
/// </summary>
public class SparkBenchTests
{
    private readonly ITestOutputHelper _out;
    public SparkBenchTests(ITestOutputHelper output) => _out = output;

    private static readonly ulong[] BenchSeeds = { 20261019, 777, 31, 4242, 99, 1234, 555, 8080 };

    [Fact]
    public void A_Warlord_Spark_outresearches_its_Council_neighbours()
    {
        // One seed, 24 minutes, AI on 0 and 3: each AI civilization beats its Council neighbour on Firsts, Mastery + Firsts,
        // projects done and KEY-2.
        BenchMatch ai = SparkBenchKit.Run(20261019, Difficulty.Warlord, SparkBenchKit.SeatsA, minutes: 24);
        _out.WriteLine(SparkBenchKit.Describe(ai));
        foreach ((CivOutcome a, CivOutcome c) in SparkBenchKit.Neighbours(ai))
        {
            Assert.True(a.Firsts > c.Firsts, $"civ {a.Civ}: AI Firsts {a.Firsts} vs Council {c.Firsts}");
            Assert.True(a.Legacy > c.Legacy, $"civ {a.Civ}: AI Legacy {a.Legacy:0} vs Council {c.Legacy:0}");
            Assert.True(a.Done > c.Done, $"civ {a.Civ}: AI done {a.Done} vs Council {c.Done}");
            Assert.True(SparkBenchKit.Earlier(a.Key2, c.Key2), $"civ {a.Civ}: AI KEY-2 {a.Key2:0.0} vs Council {c.Key2:0.0}");
        }
    }

    [Fact]
    public void Full_bench()
    {
        if (Environment.GetEnvironmentVariable("CIVSIM_BENCH") != "1") return;
        var report = new StringBuilder();
        var council = BenchSeeds.AsParallel().AsOrdered().Select(s => SparkBenchKit.Run(s, Difficulty.Warlord, Array.Empty<int>())).ToList();
        report.AppendLine("Council only (positional baseline, neighbours 0 v 1 and 3 v 2 as if 0 and 3 were the AI):");
        report.AppendLine("  " + PairScore.Of(council.SelectMany(m => new[] { new Pair(m.Civs[0], m.Civs[1]), new Pair(m.Civs[3], m.Civs[2]) })));
        var scores = new List<(Difficulty Level, PairScore Neighbours, PairScore Mirrored, PairScore SameSeat, double Legacy, long Rollouts)>();
        Difficulty[] levels = Environment.GetEnvironmentVariable("CIVSIM_BENCH_LEVELS") is { } only
            ? only.Split(',').Select(Enum.Parse<Difficulty>).ToArray()
            : new[] { Difficulty.Settler, Difficulty.Chieftain, Difficulty.Warlord, Difficulty.Legend };
        foreach (Difficulty level in levels)
        {
            var a = BenchSeeds.AsParallel().AsOrdered().Select(s => SparkBenchKit.Run(s, level, SparkBenchKit.SeatsA)).ToList();
            var b = BenchSeeds.AsParallel().AsOrdered().Select(s => SparkBenchKit.Run(s, level, SparkBenchKit.SeatsB)).ToList();
            PairScore neighbours = PairScore.Of(a.SelectMany(SparkBenchKit.Neighbours));
            PairScore mirrored = PairScore.Of(a.Zip(b).SelectMany(x => SparkBenchKit.Mirrored(x.First, x.Second)));
            PairScore same = PairScore.Of(a.Zip(council).SelectMany(x => SparkBenchKit.SameSeat(x.First, x.Second))
                .Concat(b.Zip(council).SelectMany(x => SparkBenchKit.SameSeat(x.First, x.Second))));
            double legacy = a.Concat(b).SelectMany(m => m.Civs.Where(c => c.Ai)).Average(c => c.Legacy);
            long rollouts = a.Concat(b).Sum(m => m.Rollouts) / Math.Max(1, a.Concat(b).Sum(m => m.Decisions));
            scores.Add((level, neighbours, mirrored, same, legacy, rollouts));
            report.AppendLine($"{level}: rollouts/decision {rollouts}, mean AI Mastery+Firsts {legacy:0}");
            report.AppendLine($"  neighbours (AI 0,3 v Council 1,2): {neighbours}");
            report.AppendLine($"  mirrored 2-3 seats:               {mirrored}");
            report.AppendLine($"  same seat v all-Council match:    {same}");
            foreach (BenchMatch m in a.Concat(b)) report.AppendLine("    " + SparkBenchKit.Describe(m));
        }
        string text = report.ToString();
        _out.WriteLine(text);
        if (Environment.GetEnvironmentVariable("CIVSIM_BENCH_OUT") is { } file) File.WriteAllText(file, text);
        if (!levels.Contains(Difficulty.Warlord)) return;
        var warlord = scores.Single(s => s.Level == Difficulty.Warlord);
        Assert.True(warlord.SameSeat.Share(warlord.SameSeat.Legacy) > 0.7, text);
        Assert.True(warlord.Mirrored.Share(warlord.Mirrored.Legacy) > 0.7, text);
    }
}

/// <summary>One civilization's research outcome in a bench match.</summary>
internal sealed record CivOutcome(int Civ, bool Ai, double Key1, double Key2, double Key3, int Firsts, double Mastery, double FirstsLegacy, int Done, string Notes = "")
{
    public double Legacy => Mastery + FirstsLegacy;
}

/// <summary>One bench match.</summary>
internal sealed record BenchMatch(ulong Seed, Difficulty Level, int[] AiSeats, CivOutcome[] Civs, long Rollouts, int Decisions, double Milliseconds);

/// <summary>A paired comparison: an AI civilization against a Council civilization.</summary>
internal readonly record struct Pair(CivOutcome Ai, CivOutcome Council);

/// <summary>Win counts over a set of pairs, per research outcome (docs/MVP/14 §10.3, the research part).</summary>
internal sealed record PairScore(int Pairs, int Legacy, int Firsts, int Done, int Key1, int Key2, int Key3)
{
    public static PairScore Of(IEnumerable<Pair> pairs)
    {
        int n = 0, legacy = 0, firsts = 0, done = 0, k1 = 0, k2 = 0, k3 = 0;
        foreach ((CivOutcome a, CivOutcome c) in pairs)
        {
            n++;
            if (a.Legacy > c.Legacy) legacy++;
            if (a.Firsts > c.Firsts) firsts++;
            if (a.Done > c.Done) done++;
            if (SparkBenchKit.Earlier(a.Key1, c.Key1)) k1++;
            if (SparkBenchKit.Earlier(a.Key2, c.Key2)) k2++;
            if (SparkBenchKit.Earlier(a.Key3, c.Key3)) k3++;
        }
        return new PairScore(n, legacy, firsts, done, k1, k2, k3);
    }

    public double Share(int wins) => Pairs == 0 ? 0 : (double)wins / Pairs;

    public override string ToString()
        => $"pairs {Pairs}: Legacy {Share(Legacy):P0}, Firsts {Share(Firsts):P0}, done@33 {Share(Done):P0}, KEY-1 {Share(Key1):P0}, KEY-2 {Share(Key2):P0}, KEY-3 {Share(Key3):P0}";
}

/// <summary>
/// The Spark bench: AI seats against Council civilizations on the point-symmetric worlds (civilizations 0 and 3 are
/// geographic mirrors, as are 1 and 2; the neighbour pairs are 0-1 and 2-3). Three comparisons are reported:
/// <list type="bullet">
/// <item>Neighbours: AI on 0 and 3 against the Council on 1 and 2, each AI against its neighbour (0 v 1, 3 v 2).</item>
/// <item>Mirrored seats: the 2-3 neighbour pair played both ways on the same seed (AI on 3 against the Council on 2, then AI
/// on 2 against the Council on 3), so geography cancels. Civilization 0 is the player's seat, whose Council waits a season
/// before filling a slot, so it is kept out of this comparison.</item>
/// <item>Same seat: each AI civilization against the Council playing the same seat in an all-Council match on the same seed.</item>
/// </list>
/// </summary>
internal static class SparkBenchKit
{
    public static readonly int[] SeatsA = { 0, 3 };
    public static readonly int[] SeatsB = { 1, 2 };

    public static BenchMatch Run(ulong seed, Difficulty level, int[] aiSeats, int minutes = 33, EconomyArchetype archetype = EconomyArchetype.Balanced)
    {
        var controls = new CivControl[4];
        for (int c = 0; c < 4; c++) controls[c] = aiSeats.Contains(c) ? CivControl.Ai(level, archetype) : CivControl.Council;
        MatchModel match = aiSeats.Length == 0 ? ForecastTestKit.Match(seed) : ForecastTestKit.Match(seed, controls);
        var runner = new SimRunner(match);
        var watch = Stopwatch.StartNew();
        runner.Advance(minutes * (long)ForecastTestKit.TicksPerMinute);
        watch.Stop();
        ResearchSystem r = match.Research;
        var civs = new CivOutcome[r.CivCount];
        for (int c = 0; c < civs.Length; c++)
            civs[c] = new CivOutcome(c, aiSeats.Contains(c), ForecastTestKit.KeyMinute(r, c, 1), ForecastTestKit.KeyMinute(r, c, 2),
                ForecastTestKit.KeyMinute(r, c, 3), Enumerable.Range(0, r.Catalog.Count).Count(p => r.FirstBy(p) == c),
                r.MasteryLegacy(c).ToDoubleForDisplay(), r.FirstsLegacy(c).ToDoubleForDisplay(), r.DoneCount(c), Notes(r, c));
        long rollouts = 0;
        int decisions = 0;
        if (match.Ai is { } system)
            foreach (int c in aiSeats)
                if (system.SeatOf(c)?.Spark is ResearchSpark spark) { rollouts += spark.Rollouts; decisions += spark.Decisions; }
        return new BenchMatch(seed, level, aiSeats, civs, rollouts, decisions, watch.Elapsed.TotalMilliseconds);
    }

    private static string Notes(ResearchSystem r, int c)
    {
        string T(string id) { int p = r.Catalog.IndexOf(id); return r.IsDone(c, p) ? (r.CompletedTick(c, p) / 600.0).ToString("0.0") : "-"; }
        double kh = 0, spent = 0;
        for (int p = 0; p < r.Catalog.Count; p++) { kh += r.KnowHowApplied(c, p).ToDoubleForDisplay(); spent += r.Progress(c, p).ToDoubleForDisplay(); }
        return $"FIR3 {T("FIR-03")} SOC3 {T("SOC-03")} GOV1 {T("GOV-01")} BEL3 {T("BEL-03")} LRN2 {T("LRN-02")} KH {kh / Math.Max(1, spent):P0} I {r.InsightPerYear(c).ToDoubleForDisplay():0}";
    }

    /// <summary>AI on 0 and 3: each against its Council neighbour.</summary>
    public static IEnumerable<Pair> Neighbours(BenchMatch a)
    {
        yield return new Pair(a.Civs[0], a.Civs[1]);
        yield return new Pair(a.Civs[3], a.Civs[2]);
    }

    /// <summary>The 2-3 pair both ways: AI 3 v Council 2 (seats A), AI 2 v Council 3 (seats B).</summary>
    public static IEnumerable<Pair> Mirrored(BenchMatch a, BenchMatch b)
    {
        yield return new Pair(a.Civs[3], a.Civs[2]);
        yield return new Pair(b.Civs[2], b.Civs[3]);
    }

    /// <summary>Each AI civilization against the Council in the same seat of the all-Council match.</summary>
    public static IEnumerable<Pair> SameSeat(BenchMatch ai, BenchMatch council)
    {
        foreach (int c in ai.AiSeats) yield return new Pair(ai.Civs[c], council.Civs[c]);
    }

    /// <summary>Earlier keystone is better; a keystone not reached counts as later than any reached.</summary>
    public static bool Earlier(double a, double b) => !double.IsNaN(a) && (double.IsNaN(b) || a < b);

    public static string Describe(BenchMatch m)
        => $"seed {m.Seed} AI {string.Join(",", m.AiSeats)} rollouts/decision {(m.Decisions == 0 ? 0 : m.Rollouts / m.Decisions)} | " +
           string.Join(" | ", m.Civs.Select(c => $"c{c.Civ}{(c.Ai ? "A" : "C")} K {c.Key1:0.0}/{c.Key2:0.0}/{c.Key3:0.0} F{c.Firsts} M{c.Mastery:0} L{c.Legacy:0} D{c.Done}"));
}
