using System;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using CivSim.Sim.Ai;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>Opt-in Release diagnostic for B5-11; normal match paths are not instrumented.</summary>
internal static class FiefPerformanceCommand
{
    private static readonly EconomyArchetype[] Archetypes =
        { EconomyArchetype.Builder, EconomyArchetype.Grower, EconomyArchetype.Warlord, EconomyArchetype.Sage };

    public static int Run(string[] args)
    {
        string? packPath = null, seedsPath = null, outputPrefix = null;
        int minutes = 130, warmupMinutes = 5;
        bool grantE5 = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pack": packPath = Next(args, ref i); break;
                case "--seeds-file": seedsPath = Next(args, ref i); break;
                case "--minutes": minutes = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--warmup-minutes": warmupMinutes = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--output-prefix": outputPrefix = Next(args, ref i); break;
                case "--grant-e5": grantE5 = true; break;
                default: throw new ArgumentException($"Unknown fief-perf option '{args[i]}'.");
            }
        }

        if (packPath is null || seedsPath is null || outputPrefix is null)
            throw new ArgumentException("usage: fief-perf --pack FILE --seeds-file FILE --output-prefix PATH [--minutes N] [--warmup-minutes N] [--grant-e5]");
        if (minutes <= 0 || warmupMinutes < 0 || warmupMinutes >= minutes)
            throw new ArgumentOutOfRangeException(nameof(minutes), "Minutes must be positive and warm-up must be in [0, minutes).");

        ulong[] seeds = ReadSeeds(seedsPath);
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPrefix))!);
        string tickPath = outputPrefix + "-ticks.csv", seatPath = outputPrefix + "-seats.csv";
        string samplePath = outputPrefix + "-tick-samples.csv", provenancePath = outputPrefix + "-provenance.txt";
        var allTickMs = new List<double>();
        var allSeatRows = new List<SeatSample>();
        double totalGcPauseMs = 0;
        int totalGen0 = 0, totalGen1 = 0, totalGen2 = 0;
        long totalAllocatedBytes = 0;
        using var tickWriter = new StreamWriter(tickPath, false, new UTF8Encoding(false));
        using var seatWriter = new StreamWriter(seatPath, false, new UTF8Encoding(false));
        using var sampleWriter = new StreamWriter(samplePath, false, new UTF8Encoding(false));
        tickWriter.WriteLine("seed,minutes,warmup_minutes,timed_ticks,expected_timed_ticks,stop_reason,censored,final_tick,final_checksum,mean_ms,p50_ms,p95_ms,p99_ms,max_ms,gc_pause_ms,gc_pause_ms_per_tick,gen0,gen1,gen2,allocated_bytes,allocated_bytes_per_tick");
        seatWriter.WriteLine("seed,civ,archetype,season,tick,key5_completed_tick,population,view_figure_count,view_budget,over_budget");
        sampleWriter.WriteLine("seed,sim_tick,elapsed_ms");

        string executablePath = Assembly.GetEntryAssembly()?.Location ?? string.Empty;
        File.WriteAllLines(provenancePath, new[]
        {
            "cohort=" + (grantE5 ? "e5-granted" : "natural"),
            "grant_e5=" + grantE5.ToString(CultureInfo.InvariantCulture),
            "minutes=" + minutes.ToString(CultureInfo.InvariantCulture),
            "warmup_minutes=" + warmupMinutes.ToString(CultureInfo.InvariantCulture),
            "seeds_file=" + Path.GetFullPath(seedsPath),
            "seeds_sha256=" + Sha256File(seedsPath),
            "pack_file=" + Path.GetFullPath(packPath),
            "pack_sha256=" + Sha256File(packPath),
            "executable=" + executablePath,
            "executable_sha256=" + (File.Exists(executablePath) ? Sha256File(executablePath) : "unavailable"),
            "runtime=" + RuntimeInformation.FrameworkDescription,
            "os=" + RuntimeInformation.OSDescription,
            "architecture=" + RuntimeInformation.ProcessArchitecture,
            "process_id=" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
        }, new UTF8Encoding(false));

        foreach (ulong seed in seeds)
        {
            MatchMetrics metrics = RunOne(pack, seed, minutes, warmupMinutes, grantE5);
            allTickMs.AddRange(metrics.TickMilliseconds);
            allSeatRows.AddRange(metrics.SeatSamples);
            totalGcPauseMs += metrics.GcPauseMilliseconds;
            totalGen0 += metrics.Gen0; totalGen1 += metrics.Gen1; totalGen2 += metrics.Gen2;
            totalAllocatedBytes += metrics.AllocatedBytes;
            for (int sample = 0; sample < metrics.TickMilliseconds.Count; sample++)
                sampleWriter.WriteLine($"{seed.ToString(CultureInfo.InvariantCulture)},{metrics.FirstTimedTick + sample},{F(metrics.TickMilliseconds[sample])}");
            tickWriter.WriteLine(string.Join(',',
                seed.ToString(CultureInfo.InvariantCulture), minutes, warmupMinutes, metrics.TickMilliseconds.Count,
                metrics.ExpectedTimedTicks, metrics.StopReason, metrics.Censored ? 1 : 0, metrics.FinalTick,
                metrics.FinalChecksum.ToString("x16", CultureInfo.InvariantCulture),
                F(Mean(metrics.TickMilliseconds)), F(Quantile(metrics.TickMilliseconds, .50)),
                F(Quantile(metrics.TickMilliseconds, .95)), F(Quantile(metrics.TickMilliseconds, .99)),
                F(metrics.TickMilliseconds.Count == 0 ? 0 : metrics.TickMilliseconds.Max()),
                F(metrics.GcPauseMilliseconds), F(metrics.TickMilliseconds.Count == 0 ? 0 : metrics.GcPauseMilliseconds / metrics.TickMilliseconds.Count),
                metrics.Gen0, metrics.Gen1, metrics.Gen2, metrics.AllocatedBytes,
                metrics.TickMilliseconds.Count == 0 ? "0" : F((double)metrics.AllocatedBytes / metrics.TickMilliseconds.Count)));
            foreach (SeatSample row in metrics.SeatSamples)
                seatWriter.WriteLine(string.Join(',', row.Seed, row.Civ, row.Archetype, row.Season, row.Tick,
                    row.Key5Tick, row.Population, row.ViewFigureCount, ViewBudgetPerCiv,
                    row.ViewFigureCount > ViewBudgetPerCiv ? 1 : 0));
            tickWriter.Flush();
            seatWriter.Flush();
            sampleWriter.Flush();
            Console.WriteLine($"fief-perf seed={seed} timedTicks={metrics.TickMilliseconds.Count} meanMs={F(Mean(metrics.TickMilliseconds))} p95Ms={F(Quantile(metrics.TickMilliseconds, .95))} gcPauseMs={F(metrics.GcPauseMilliseconds)} samples={metrics.SeatSamples.Count}");
        }

        Console.WriteLine($"fief-perf-summary seeds={seeds.Length} minutes={minutes} warmupMinutes={warmupMinutes} grantE5={grantE5} timedTicks={allTickMs.Count} meanMs={F(Mean(allTickMs))} p50Ms={F(Quantile(allTickMs, .50))} p95Ms={F(Quantile(allTickMs, .95))} p99Ms={F(Quantile(allTickMs, .99))} maxMs={F(allTickMs.Count == 0 ? 0 : allTickMs.Max())}");
        Console.WriteLine($"fief-perf-gc-summary pauseMs={F(totalGcPauseMs)} pauseMsPerTick={F(allTickMs.Count == 0 ? 0 : totalGcPauseMs / allTickMs.Count)} gen0={totalGen0} gen1={totalGen1} gen2={totalGen2} allocatedBytes={totalAllocatedBytes} allocatedBytesPerTick={F(allTickMs.Count == 0 ? 0 : (double)totalAllocatedBytes / allTickMs.Count)}");
        Console.WriteLine($"fief-perf-figure-summary seatSeasonRows={allSeatRows.Count} p95Population={F(Quantile(allSeatRows.Select(r => (double)r.Population).ToList(), .95))} p95ViewFigures={F(Quantile(allSeatRows.Select(r => (double)r.ViewFigureCount).ToList(), .95))} overBudgetRows={allSeatRows.Count(r => r.ViewFigureCount > ViewBudgetPerCiv)} viewBudgetPerCiv={ViewBudgetPerCiv}");
        Console.WriteLine($"tickCsv={tickPath}");
        Console.WriteLine($"seatCsv={seatPath}");
        Console.WriteLine($"tickSamplesCsv={samplePath}");
        Console.WriteLine($"provenance={provenancePath}");
        Console.WriteLine("GC pause is total process GC pause during measured ticks; the 16 ms gate is interpreted per simulated tick (pause delta / measured tick count). Warm-up ticks are executed but excluded from timing, GC, allocation, and rate calculations.");
        return 0;
    }

    internal const int ViewBudgetPerCiv = 600;

    private static string Sha256File(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    internal static ulong[] ReadSeeds(string path)
    {
        var seeds = new List<ulong>();
        var unique = new HashSet<ulong>();
        int lineNumber = 0;
        foreach (string line in File.ReadLines(path))
        {
            lineNumber++;
            string value = line.Split('#', 2)[0].Trim();
            if (value.Length == 0) continue;
            if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed))
                throw new FormatException($"Invalid seed on line {lineNumber}: '{value}'.");
            if (!unique.Add(seed)) throw new FormatException($"Duplicate seed on line {lineNumber}: {seed}.");
            seeds.Add(seed);
        }
        if (seeds.Count == 0) throw new FormatException("The frozen seed list contains no seeds.");
        return seeds.ToArray();
    }

    internal static double Quantile(IReadOnlyList<double> values, double probability)
    {
        if (values.Count == 0) return 0;
        if (probability is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(probability));
        double[] sorted = values.Order().ToArray();
        double index = (sorted.Length - 1) * probability;
        int lower = (int)index, upper = Math.Min(lower + 1, sorted.Length - 1);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (index - lower);
    }

    internal static MatchMetrics RunOne(ContentPack pack, ulong seed, int minutes, int warmupMinutes, bool grantE5)
    {
        GeneratedWorld world = WorldGenerator.Generate(seed);
        var controls = new CivControl[Archetypes.Length];
        for (int c = 0; c < controls.Length; c++) controls[c] = CivControl.Ai(Difficulty.Warlord, Archetypes[c]);
        MatchModel match = MatchModel.Create(seed, pack, world, Archetypes, controls, rules: ChapterRules.Fief);
        var runner = new SimRunner(match);
        ResearchSystem research = match.Research;
        int key5 = research.Catalog.IndexOf("KEY-5");
        int key6 = research.Catalog.IndexOf("KEY-6");
        var granted = new bool[Archetypes.Length];
        var frameBuilders = Enumerable.Range(0, Archetypes.Length).Select(c => new FrameBuilder(match, c)).ToArray();
        var frames = Enumerable.Range(0, Archetypes.Length).Select(_ => new MatchFrame()).ToArray();
        int expectedTimedTicks = (int)Math.Min(2_000_000L, (long)(minutes - warmupMinutes) * 600L);
        var timings = new List<double>(expectedTimedTicks);
        var samples = new List<SeatSample>();
        long measuredStart = (long)warmupMinutes * 600;
        long limit = (long)minutes * 600;
        long firstTimedTick = measuredStart + 1;
        MatchEnd end = match.EndOfMatch();
        TimeSpan pauseStart = default;
        long allocatedStart = 0;
        int gen0Start = 0, gen1Start = 0, gen2Start = 0;
        bool metricsStarted = false;

        while (runner.Tick < limit && !end.IsOver(runner.Tick))
        {
            if (grantE5 && runner.Tick % LivingEconomyModel.SeasonTicks == 0)
            {
                for (int civ = 0; civ < Archetypes.Length; civ++)
                {
                    if (granted[civ] || !research.IsDone(civ, key5)) continue;
                    granted[civ] = true;
                    foreach (ProjectDef project in research.Catalog.Projects)
                        if (project.Era == 5 && project.Index != key6)
                            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, project.Index, 0, 0));
                }
            }

            if (!metricsStarted && runner.Tick >= measuredStart)
            {
                pauseStart = GC.GetTotalPauseDuration();
                allocatedStart = GC.GetTotalAllocatedBytes(precise: false);
                gen0Start = GC.CollectionCount(0); gen1Start = GC.CollectionCount(1); gen2Start = GC.CollectionCount(2);
                metricsStarted = true;
            }

            long start = Stopwatch.GetTimestamp();
            runner.Advance(1);
            long finishTimestamp = Stopwatch.GetTimestamp();
            if (runner.Tick > measuredStart)
                timings.Add((finishTimestamp - start) * 1000.0 / Stopwatch.Frequency);

            if (runner.Tick % LivingEconomyModel.SeasonTicks != 0) continue;
            end = match.EndOfMatch();
            int season = checked((int)(runner.Tick / LivingEconomyModel.SeasonTicks));
            for (int civ = 0; civ < Archetypes.Length; civ++)
            {
                if (!research.IsDone(civ, key5)) continue;
                int population = 0;
                for (int f = 0; f < match.Economy.FigureCount; f++)
                    if (match.Economy.LookFigure(f).Civilization == civ) population++;
                frameBuilders[civ].Build(match, frames[civ]);
                frames[civ].Tick = runner.Tick;
                samples.Add(new SeatSample(seed, civ, Archetypes[civ].ToString(), season, runner.Tick,
                    research.CompletedTick(civ, key5), population, frames[civ].FigureCount));
            }
        }

        bool censored = runner.Tick < limit;
        return new MatchMetrics(timings, samples,
            metricsStarted ? (GC.GetTotalPauseDuration() - pauseStart).TotalMilliseconds : 0,
            metricsStarted ? GC.CollectionCount(0) - gen0Start : 0,
            metricsStarted ? GC.CollectionCount(1) - gen1Start : 0,
            metricsStarted ? GC.CollectionCount(2) - gen2Start : 0,
            metricsStarted ? GC.GetTotalAllocatedBytes(precise: false) - allocatedStart : 0,
            runner.Tick, runner.CurrentChecksum, expectedTimedTicks, firstTimedTick,
            censored ? "terminal-before-horizon" : "horizon", censored);
    }

    private static string Next(string[] args, ref int i)
    {
        if (++i >= args.Length) throw new ArgumentException($"Missing value after {args[i - 1]}.");
        return args[i];
    }

    private static double Mean(IReadOnlyList<double> values) => values.Count == 0 ? 0 : values.Average();
    private static string F(double value) => value.ToString("F6", CultureInfo.InvariantCulture);

    internal sealed record SeatSample(ulong Seed, int Civ, string Archetype, int Season, long Tick,
        long Key5Tick, int Population, int ViewFigureCount);

    internal sealed record MatchMetrics(List<double> TickMilliseconds, List<SeatSample> SeatSamples,
        double GcPauseMilliseconds, int Gen0, int Gen1, int Gen2, long AllocatedBytes, long FinalTick, ulong FinalChecksum,
        int ExpectedTimedTicks, long FirstTimedTick, string StopReason, bool Censored);
}
