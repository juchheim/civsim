using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CivSim.Sim.Ai;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.World;
using Xunit.Abstractions;

namespace CivSim.Tests;

/// <summary>
/// The Strategist's duel benches (docs/MVP/14 §10.3). Long: run them with <c>CIVSIM_BENCH=1</c> (optionally
/// <c>CIVSIM_BENCH_SEEDS=n</c>); otherwise they return at once. Strategist AIs sit on civilizations 0 and 3, whose paired
/// neighbours are 1 and 2; the placeholder Spark leaves research to the Council, so only the Strategist differs.
/// </summary>
public class StrategistBenchTests
{
    private readonly ITestOutputHelper _out;
    public StrategistBenchTests(ITestOutputHelper output) { _out = output; }

    private const int Seasons = 132; // 33 minutes

    private sealed record Outcome(ulong Seed, string Mode, double[] Dominion, double[] Land, int[] Tiles, int[] Settlements, int[] Lost,
        StrategistAudit.Result[] Audit, double MeanMs, double MaxMs, int Decisions);

    private static Outcome Run(ulong seed, string mode)
    {
        GeneratedWorld world = WorldGenerator.Generate(seed);
        // "ai-..." uses the real, sight-limited belief builder; "omni-..." overlays the omniscient test double.
        bool ai = mode.StartsWith("ai-", StringComparison.Ordinal) || mode.StartsWith("omni-", StringComparison.Ordinal);
        CivControl smart = CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced);
        bool swap = mode.EndsWith("-swap", StringComparison.Ordinal);
        int[] aiSeats = swap ? new[] { 1, 2 } : new[] { 0, 3 };
        CivControl[] controls = ai && swap ? new[] { CivControl.Council, smart, smart, CivControl.Council }
            : ai ? new[] { smart, CivControl.Council, CivControl.Council, smart }
            : new[] { CivControl.Council, CivControl.Council, CivControl.Council, CivControl.Council };
        var rig = new StrategistRig(seed, world, controls) { Omniscient = mode.StartsWith("omni-", StringComparison.Ordinal) };
        if (mode.Contains("-rusher", StringComparison.Ordinal) && swap) { rig.Rushers.Add(new Rusher(0, 1)); rig.Rushers.Add(new Rusher(3, 2)); }
        else if (mode.Contains("-rusher", StringComparison.Ordinal)) { rig.Rushers.Add(new Rusher(1, 0)); rig.Rushers.Add(new Rusher(2, 3)); }
        double total = 0, max = 0;
        int decisions = 0;
        for (int s = 0; s < Seasons; s++)
        {
            rig.Seasons(1);
            if (!ai) continue;
            foreach (int civ in aiSeats)
            {
                // Time a copy of the seat's Strategist on the season it just decided (the copy's orders go nowhere).
                AiSeat seat = rig.Match.Ai!.SeatOf(civ)!;
                var clone = new Strategist(seat.Control.Spec, seat.Profile);
                using (var ms = new MemoryStream())
                {
                    using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true)) seat.Strategist.Write(w);
                    ms.Position = 0;
                    using var r = new BinaryReader(ms);
                    clone.Read(r);
                }
                StrategistView view = StrategistView.From(seat.Context);
                var probe = new StrategistView
                {
                    Civ = view.Civ, Season = view.Season, Tick = view.Tick, MatchSeed = view.MatchSeed, Spec = view.Spec,
                    Profile = view.Profile, Own = view.Own, Belief = view.Belief, Intent = new StrategicIntent(), Catalog = view.Catalog,
                    OwnDone = view.OwnDone,
                };
                var sw = Stopwatch.StartNew();
                clone.Decide(probe, new ActionRecorder { Accept = false });
                double ms2 = sw.Elapsed.TotalMilliseconds;
                if (s > 2) { total += ms2; max = Math.Max(max, ms2); decisions++; }
            }
        }
        var m = rig.Match;
        var dominion = new double[4];
        var land = new double[4];
        var tiles = new int[4];
        var settlements = new int[4];
        var lost = new int[4];
        var audit = new StrategistAudit.Result[4];
        for (int c = 0; c < 4; c++)
        {
            dominion[c] = Scoring.Of(m, c).Legacy.ToDoubleForDisplay();
            land[c] = m.Politics.Territory.Dominion(c).ToDoubleForDisplay();
            tiles[c] = m.Politics.Territory.OwnedTiles(c);
            settlements[c] = rig.SettlementsOf(c).Count;
            lost[c] = m.Politics.Events.Count(e => e.Civ == c && e.Kind == PoliticsEventKind.Captured && e.B < 0);
            audit[c] = StrategistAudit.Run(c, ai ? rig.Log.Entries : Array.Empty<AiDecision>(), m.Politics.Events);
        }
        return new Outcome(seed, mode, dominion, land, tiles, settlements, lost, audit, decisions == 0 ? 0 : total / decisions, max, decisions);
    }

    [Fact]
    public void Duel_bench()
    {
        if (Environment.GetEnvironmentVariable("CIVSIM_BENCH") is null) return;
        int count = int.Parse(Environment.GetEnvironmentVariable("CIVSIM_BENCH_SEEDS") ?? "8");
        string[] modes = (Environment.GetEnvironmentVariable("CIVSIM_BENCH_MODES") ?? "council,ai-vs-council,omni-vs-council,council-vs-rusher,ai-vs-rusher").Split(',');
        ulong[] seeds = Enumerable.Range(0, count).Select(i => 20261019UL + (ulong)i * ulong.Parse(Environment.GetEnvironmentVariable("CIVSIM_BENCH_STEP") ?? "7919")).ToArray();
        var jobs = new List<(ulong, string)>();
        foreach (ulong seed in seeds) foreach (string mode in modes) jobs.Add((seed, mode));
        var results = new Outcome[jobs.Count];
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = 6 }, i => results[i] = Run(jobs[i].Item1, jobs[i].Item2));
        var sb = new StringBuilder();
        foreach (string mode in modes)
        {
            var rows = results.Where(r => r.Mode == mode).ToList();
            sb.AppendLine($"== {mode}: civs 0,3 vs 1,2 over {rows.Count} seeds, {Seasons / 4} minutes");
            foreach (Outcome r in rows)
                sb.AppendLine($"seed {r.Seed}: legacy {string.Join(" ", r.Dominion.Select(d => d.ToString("F0")))} dominion {string.Join(" ", r.Land.Select(d => d.ToString("F0")))} tiles {string.Join(" ", r.Tiles)} " +
                              $"settlements {string.Join(" ", r.Settlements)} lost {string.Join(" ", r.Lost)} undefended {r.Audit[0].UndefendedLosses + r.Audit[3].UndefendedLosses}" +
                              (r.Decisions > 0 ? $" | decide {r.MeanMs:F2} ms mean {r.MaxMs:F1} max" : ""));
            double side03 = rows.Average(r => (r.Dominion[0] + r.Dominion[3]) / 2), side12 = rows.Average(r => (r.Dominion[1] + r.Dominion[2]) / 2);
            double dom03 = rows.Average(r => (r.Land[0] + r.Land[3]) / 2), dom12 = rows.Average(r => (r.Land[1] + r.Land[2]) / 2);
            sb.AppendLine($"  mean dominion 0/3 {dom03:F0} vs 1/2 {dom12:F0}");
            double tiles03 = rows.Average(r => (r.Tiles[0] + r.Tiles[3]) / 2.0), tiles12 = rows.Average(r => (r.Tiles[1] + r.Tiles[2]) / 2.0);
            int noLoss = rows.Sum(r => (r.Lost[0] == 0 ? 1 : 0) + (r.Lost[3] == 0 ? 1 : 0));
            int wins = rows.Sum(r => (r.Dominion[0] > r.Dominion[1] ? 1 : 0) + (r.Dominion[3] > r.Dominion[2] ? 1 : 0));
            sb.AppendLine($"  mean legacy 0/3 {side03:F0} vs 1/2 {side12:F0}; tiles {tiles03:F0} vs {tiles12:F0}; " +
                          $"civ 0/3 beat their neighbour {wins}/{rows.Count * 2}; lost no settlement {noLoss}/{rows.Count * 2}; " +
                          $"settlements lost by 1/2 {rows.Sum(r => r.Lost[1] + r.Lost[2])}");
            var a = rows.SelectMany(r => new[] { r.Audit[0], r.Audit[3] }).ToList();
            sb.AppendLine($"  audit (0/3): suicide {a.Sum(x => x.SuicideRaids)}, undefended {a.Sum(x => x.UndefendedLosses)}, winter {a.Sum(x => x.WinterAggression)}, " +
                          $"dithering {a.Sum(x => x.Dithering)}; raids {a.Sum(x => x.Raids)}, assaults {a.Sum(x => x.Assaults)}, threats {a.Sum(x => x.ThreatsNoticed)}, raided {a.Sum(x => x.LossesToRaids)}");
            if (rows.Any(r => r.Decisions > 0))
                sb.AppendLine($"  Strategist.Decide: {rows.Average(r => r.MeanMs):F2} ms mean per civ-season, {rows.Max(r => r.MaxMs):F1} ms worst");
        }
        _out.WriteLine(sb.ToString());
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "civsim-strategist-bench.txt"), sb.ToString());
    }
}
