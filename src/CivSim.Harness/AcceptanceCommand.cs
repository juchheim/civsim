using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CivSim.Sim.Ai;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>Runs the long-form deterministic replay, simulation tick, and invalid-state soak gates (MVP 17 §4).</summary>
internal static class AcceptanceCommand
{
    internal const int MinimumAcceptanceSampleMinutes = 10;
    internal const int MinimumSoakAcceptanceMinutes = 64;
    internal const int RequiredReplayMatches = 1_000;
    internal const int MaximumReplayProbeMatches = 32;
    internal const int MaximumReplayProbeMinutes = 64;

    internal enum OverallStatus
    {
        Passed,
        Failed,
        Incomplete,
    }

    private const ulong FirstSeed = 20261019;
    private static readonly EconomyArchetype[] Archetypes =
    {
        EconomyArchetype.Balanced, EconomyArchetype.Grower, EconomyArchetype.Builder,
        EconomyArchetype.Wanderer, EconomyArchetype.Warlord, EconomyArchetype.Sage,
    };
    private static readonly Difficulty[] Difficulties =
    {
        Difficulty.Settler, Difficulty.Chieftain, Difficulty.Warlord, Difficulty.Legend,
    };

    public static int Run(string[] args)
    {
        string? packPath = null;
        bool s1 = false, s1Probe = false, s12 = false, s13 = false, s15 = false;
        int matches = 1_000, minutes = 10, sampleMinutes = 10;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pack": packPath = Next(args, ref i); break;
                case "--s1": s1 = true; break;
                case "--s1-probe": s1Probe = true; break;
                case "--s12": s12 = true; break;
                case "--s13": s13 = true; break;
                case "--s15": s15 = true; break;
                case "--matches": matches = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--minutes": minutes = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--sample-minutes": sampleMinutes = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                default: throw new ArgumentException($"Unknown option '{args[i]}'.");
            }
        }
        if (packPath is null) throw new ArgumentException("acceptance needs --pack FILE.");
        if (!s1 && !s12 && !s13 && !s15) throw new ArgumentException("Choose at least one of --s1, --s12, --s13, or --s15.");
        if (matches <= 0 || minutes <= 0 || sampleMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(matches), "Match and sample counts must be positive.");
        if (s1Probe) ValidateReplayProbe(s1, matches, minutes);
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));

        bool pass = true;
        bool diagnosticS1 = false;
        if (s1)
        {
            bool replayPassed;
            if (s1Probe)
            {
                replayPassed = RunReplayProbe(matches, minutes, () => RunReplayGate(pack, matches, minutes));
                diagnosticS1 = true;
            }
            else replayPassed = RunReplayGateForHorizon(matches, minutes, () => RunReplayGate(pack, matches, minutes), out diagnosticS1);
            pass &= replayPassed;
        }
        if (s12) pass &= RunChapterGate(pack);
        if (s13) pass &= RunTickGateForHorizon(sampleMinutes, () => RunTickGate(pack, sampleMinutes), out _);
        if (s15)
        {
            bool s15Passed = RunSoakGateForHorizon(minutes, () => RunSoakGate(pack, matches, minutes), out bool diagnostic);
            pass &= s15Passed;
            Console.WriteLine(GetS15StatusLine(diagnostic, s15Passed));
        }
        OverallStatus status = GetOverallStatus(pass,
            hasDiagnosticS13: s13 && IsDiagnosticSample(sampleMinutes),
            hasDiagnosticS15: s15 && IsDiagnosticSoakSample(minutes),
            hasDiagnosticS1: diagnosticS1);
        Console.WriteLine(GetOverallStatusLine(status));
        return GetExitCode(status);
    }

    internal static bool IsDiagnosticSample(int sampleMinutes) => sampleMinutes < MinimumAcceptanceSampleMinutes;

    internal static bool RunTickGateForHorizon(int sampleMinutes, Func<bool> runTickGate, out bool diagnostic)
    {
        diagnostic = IsDiagnosticSample(sampleMinutes);
        if (diagnostic)
        {
            Console.WriteLine(GetS13StatusLine(diagnostic: true, passed: true));
            return true;
        }
        return runTickGate();
    }

    internal static bool IsDiagnosticSoakSample(int minutes) => minutes < MinimumSoakAcceptanceMinutes;

    internal static bool IsDiagnosticReplaySample(int matches, int minutes) =>
        matches != RequiredReplayMatches || minutes < MinimumSoakAcceptanceMinutes;

    internal static void ValidateReplayProbe(bool s1, int matches, int minutes)
    {
        if (!s1) throw new ArgumentException("--s1-probe requires --s1.");
        if (matches <= 0 || minutes <= 0) throw new ArgumentOutOfRangeException(nameof(matches), "Match and sample counts must be positive.");
        if (matches > MaximumReplayProbeMatches || minutes > MaximumReplayProbeMinutes)
            throw new ArgumentOutOfRangeException(nameof(matches), $"S1 probe is bounded to at most {MaximumReplayProbeMatches} matches and {MaximumReplayProbeMinutes} minutes.");
    }

    internal static bool RunReplayProbe(int matches, int minutes, Func<bool> runReplayGate)
    {
        ValidateReplayProbe(s1: true, matches, minutes);
        Console.WriteLine($"S1 DIAGNOSTIC PROBE (throughput only; bounded to {MaximumReplayProbeMatches} matches and {MaximumReplayProbeMinutes} minutes)");
        return runReplayGate();
    }

    internal static bool RunReplayGateForHorizon(int matches, int minutes, Func<bool> runReplayGate, out bool diagnostic)
    {
        diagnostic = IsDiagnosticReplaySample(matches, minutes);
        if (diagnostic)
        {
            Console.WriteLine($"S1 DIAGNOSTIC (requires {RequiredReplayMatches} matches and at least {MinimumSoakAcceptanceMinutes} minutes; requested {matches} matches and {minutes} minutes)");
            return true;
        }
        return runReplayGate();
    }

    internal static bool RunSoakGateForHorizon(int minutes, Func<bool> runSoakGate, out bool diagnostic)
    {
        diagnostic = IsDiagnosticSoakSample(minutes);
        return diagnostic || runSoakGate();
    }

    internal static OverallStatus GetOverallStatus(bool gatesPassed, bool hasDiagnosticS13, bool hasDiagnosticS15 = false,
        bool hasDiagnosticS1 = false) =>
        hasDiagnosticS13 || hasDiagnosticS15 || hasDiagnosticS1 ? OverallStatus.Incomplete : gatesPassed ? OverallStatus.Passed : OverallStatus.Failed;

    internal static string GetOverallStatusLine(OverallStatus status) => status switch
    {
        OverallStatus.Passed => "ACCEPTANCE GATES PASSED",
        OverallStatus.Failed => "ACCEPTANCE GATES FAILED",
        _ => "ACCEPTANCE INCOMPLETE",
    };

    internal static int GetExitCode(OverallStatus status) => status switch
    {
        OverallStatus.Passed => 0,
        OverallStatus.Failed => 1,
        _ => 2,
    };

    internal static string GetS13StatusLine(bool diagnostic, bool passed) => diagnostic
        ? "S13 DIAGNOSTIC (sample shorter than 10-minute acceptance window)"
        : passed ? "S13 PASS" : "S13 FAIL";

    internal static string GetS15StatusLine(bool diagnostic, bool passed) => diagnostic
        ? "S15 DIAGNOSTIC (horizon shorter than 64-minute acceptance window)"
        : passed ? "S15 PASS" : "S15 FAIL";

    internal static bool IsExactChapterEnd(long tick, MatchEnd end) => end.IsOver(tick) && tick == end.EndTick;

    private static bool RunChapterGate(ContentPack pack)
    {
        const double budgetSeconds = 30.0;
        const long ticksPerYear = ResearchSystem.SeasonTicks * MatchEnd.SeasonsPerYear;
        var archetypes = new[]
        {
            EconomyArchetype.Balanced, EconomyArchetype.Balanced,
            EconomyArchetype.Balanced, EconomyArchetype.Balanced,
        };
        int capYear = new ResearchCatalog(pack).ChapterCapYear;
        if (capYear <= 0) throw new InvalidDataException($"S12 requires a positive chapter cap year; pack has {capYear}.");
        long capTick = checked((long)capYear * ticksPerYear);
        MatchModel match = MatchModel.Create(FirstSeed, pack, archetypes);
        var runner = new SimRunner(match);
        var timer = Stopwatch.StartNew();
        MatchEnd end = match.EndOfMatch();
        bool timedOut = false;
        while (!end.IsOver(runner.Tick) && runner.Tick < capTick)
        {
            if (timer.Elapsed.TotalSeconds >= budgetSeconds)
            {
                timedOut = true;
                break;
            }
            long remaining = capTick - runner.Tick;
            int chunk = (int)Math.Min(ResearchSystem.SeasonTicks, remaining);
            runner.Advance(chunk);
            end = match.EndOfMatch();
        }
        timer.Stop();
        double elapsed = timer.Elapsed.TotalSeconds;
        bool overDeadline = timedOut || elapsed > budgetSeconds;
        bool endedAtRuleTick = end.IsOver(runner.Tick) && runner.Tick == end.EndTick;
        bool reachedCap = runner.Tick >= capTick;
        bool pass = !overDeadline && endedAtRuleTick;
        Console.WriteLine($"S12 headless Bronze chapter: four Balanced AIs; seed {FirstSeed}; reason {end.Reason}; simulated tick {runner.Tick}, rule end tick {end.EndTick}, cap tick {capTick}; elapsed {elapsed:F3} s (limit {budgetSeconds:F0} s); checksum {runner.CurrentChecksum:x16}");
        if (overDeadline) Console.WriteLine(timedOut
            ? $"S12 stopped at tick {runner.Tick}: reached the {budgetSeconds:F0}-second deadline between season advances."
            : $"S12 exceeded the {budgetSeconds:F0}-second deadline during its final season advance.");
        else if (reachedCap && !endedAtRuleTick) Console.WriteLine($"S12 stopped at the chapter cap tick {capTick}, but the end rule did not report a completed chapter.");
        else if (!endedAtRuleTick) Console.WriteLine("S12 failed: simulated tick does not match the chapter end rule.");
        Console.WriteLine(pass ? "S12 PASS" : "S12 FAIL");
        return pass;
    }

    private static bool RunReplayGate(ContentPack pack, int matches, int minutes)
    {
        long capTicks = checked((long)minutes * 600);
        Console.WriteLine($"S1 replay determinism: {matches} chapter matches, up to {minutes} minutes each, binary replay round trip + every checksum; {WorkerCount} workers");
        // The command schedule depends only on the horizon and pack. Build it once instead of
        // rebuilding the research catalog and command array for every independent match.
        Command[] commands = ReplayCommands(capTicks, pack);
        var failures = new ConcurrentQueue<string>();
        int completed = 0;
        var timer = Stopwatch.StartNew();
        Parallel.For(0, matches, new ParallelOptions { MaxDegreeOfParallelism = WorkerCount }, index =>
        {
            try
            {
                ulong seed = FirstSeed + (ulong)index;
                var archetypes = ArchetypesFor(index);
                // Share the same generated world between match construction and fingerprinting.
                // WorldGenerator is deterministic and MatchModel.Create(seed, ...) delegates to
                // this overload with exactly this generated value.
                GeneratedWorld world = WorldGenerator.Generate(seed);
                MatchModel model = MatchModel.Create(seed, pack, world, archetypes);
                var runner = new SimRunner(model);
                foreach (Command command in commands) runner.Submit(command);
                MatchEnd end = model.EndOfMatch();
                while (!end.IsOver(runner.Tick) && runner.Tick < capTicks)
                {
                    long remaining = capTicks - runner.Tick;
                    runner.Advance((int)Math.Min(ResearchSystem.SeasonTicks, remaining));
                    end = model.EndOfMatch();
                }
                if (!IsExactChapterEnd(runner.Tick, end))
                    throw new InvalidDataException($"Chapter did not end exactly within the {minutes}-minute horizon (tick {runner.Tick}, rule end tick {end.EndTick}, reason {end.Reason}).");

                ulong recordedFinal = runner.CurrentChecksum;
                MatchReplay recorded = MatchReplay.FromRun(seed, pack, archetypes, runner,
                    WorldEconomyInput.FromGeneratedWorld(world).Fingerprint);
                using var stream = new MemoryStream();
                recorded.Write(stream);
                stream.Position = 0;
                MatchReplay decoded = MatchReplay.Read(stream);
                if (decoded.FinalChecksum != recordedFinal || decoded.Replay.Commands.Count != runner.AppliedCommands.Count)
                    throw new InvalidDataException("Binary replay round trip changed the final checksum or command count.");
                ReplayVerification verification = decoded.Verify(pack);
                if (!verification.Ok)
                    throw new InvalidDataException($"Replay mismatch at tick {verification.FirstMismatchTick}: {verification.Message}");
            }
            catch (Exception e)
            {
                failures.Enqueue($"match {index} seed {FirstSeed + (ulong)index}: {e.GetType().Name}: {e.Message}");
            }

            int done = Interlocked.Increment(ref completed);
            Console.WriteLine($"  S1 progress {done}/{matches}; {timer.Elapsed.TotalMinutes:F1} min elapsed");
        });
        timer.Stop();
        Console.WriteLine($"S1 result: {matches - failures.Count}/{matches} replay checks matched; elapsed {timer.Elapsed:hh\\:mm\\:ss}");
        foreach (string failure in failures) Console.WriteLine("  FAIL " + failure);
        return failures.IsEmpty && matches == RequiredReplayMatches;
    }

    private static Command[] ReplayCommands(long ticks, ContentPack pack)
    {
        // Mix research/politics orders, including refused orders, so each replay exercises the persisted command stream.
        int forage = new ResearchCatalog(pack).IndexOf("FOR-01");
        var commands = new[]
        {
            new Command(0, 0, CommandKind.SetCouncil, 0, 0, 0),
            new Command(150, 0, CommandKind.SetLeaning, 0, 40, 0),
            new Command(300, 0, CommandKind.SetSlot, 0, forage, 0),
            new Command(450, 0, CommandKind.QueueProject, forage, 0, 0),
            new Command(600, 0, CommandKind.DequeueProject, 0, 0, 0),
            new Command(750, 0, CommandKind.SetPosture, (int)Posture.Expand, 0, 0),
            new Command(900, 0, CommandKind.DeclareWar, 1, 0, 0),
            new Command(1_050, 0, CommandKind.MakePeace, 1, 0, 0),
            new Command(1_200, 0, CommandKind.FormAlliance, 1, 0, 0),
            new Command(1_350, 0, CommandKind.SendGift, 1, 0, 0),
            new Command(1_500, 0, CommandKind.SendBarter, 1, 0, 0),
            new Command(1_650, 0, CommandKind.LaunchRaid, 0, 4, 0),
            new Command(1_800, 0, CommandKind.LaunchAssault, 0, 0, 0),
            new Command(1_950, 0, CommandKind.RecallParty, 0, 0, 0),
            new Command(2_100, 0, CommandKind.RemoveBeacon, 0, 0, 0),
            new Command(2_250, 0, CommandKind.SetCouncil, 1, 0, 0),
        };
        int count = 0;
        while (count < commands.Length && commands[count].Tick < ticks) count++;
        if (count == commands.Length) return commands;
        Array.Resize(ref commands, count);
        return commands;
    }

    private static bool RunTickGate(ContentPack pack, int sampleMinutes)
    {
        const int requiredFigures = 2_400;
        const int maxWarmMinutes = 90;
        int sampleTicks = checked(sampleMinutes * 600);
        ulong seed = FirstSeed;
        var archetypes = new[]
        {
            EconomyArchetype.Grower, EconomyArchetype.Builder,
            EconomyArchetype.Wanderer, EconomyArchetype.Warlord,
        };
        var controls = new CivControl[4];
        for (int c = 0; c < controls.Length; c++) controls[c] = CivControl.Ai(Difficulty.Warlord, archetypes[c]);
        GeneratedWorld world = WorldGenerator.Generate(seed);
        MatchModel match = MatchModel.Create(seed, pack, world, archetypes, controls, playerCouncil: false);
        var runner = new SimRunner(match);
        Console.WriteLine($"S13 sim tick: four Warlord AIs; warm until {requiredFigures:N0} figures; then sample {sampleMinutes} minutes ({sampleTicks:N0} individual ticks)");

        int lastProgressMinute = 0;
        while (match.Economy.FigureCount < requiredFigures && runner.Tick < maxWarmMinutes * 600L)
        {
            runner.Advance(150);
            int minute = (int)(runner.Tick / 600);
            if (minute >= lastProgressMinute + 10)
            {
                lastProgressMinute = minute;
                Console.WriteLine($"  warm-up minute {minute}: {match.Economy.FigureCount:N0} figures");
            }
        }
        if (match.Economy.FigureCount < requiredFigures)
        {
            Console.WriteLine($"S13 FAIL: reached only {match.Economy.FigureCount:N0} figures by minute {runner.Tick / 600}");
            return false;
        }
        Console.WriteLine($"  workload reached at minute {runner.Tick / 600}: {match.Economy.FigureCount:N0} figures");

        // Stabilize JIT and the mature simulation state, then start a clean measurement interval.
        runner.Advance(600);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        runner.Advance(600);

        long elapsedTicks = 0, maxWallTicks = 0, maxPauseTicks = 0, maxWallAtSimTick = -1;
        int minFigures = match.Economy.FigureCount, maxFigures = minFigures;
        long frequency = Stopwatch.Frequency;
        for (int i = 0; i < sampleTicks; i++)
        {
            long pauseBefore = GC.GetTotalPauseDuration().Ticks;
            long start = Stopwatch.GetTimestamp();
            runner.Advance(1);
            long wall = Stopwatch.GetTimestamp() - start;
            long pause = GC.GetTotalPauseDuration().Ticks - pauseBefore;
            elapsedTicks += wall;
            if (wall > maxWallTicks) { maxWallTicks = wall; maxWallAtSimTick = runner.Tick - 1; }
            maxPauseTicks = Math.Max(maxPauseTicks, pause);
            int figures = match.Economy.FigureCount;
            minFigures = Math.Min(minFigures, figures);
            maxFigures = Math.Max(maxFigures, figures);
        }
        double avgMs = elapsedTicks * 1000.0 / frequency / sampleTicks;
        double maxWallMs = maxWallTicks * 1000.0 / frequency;
        double maxPauseMs = maxPauseTicks / TimeSpan.TicksPerMillisecond;
        bool workloadOk = minFigures >= requiredFigures;
        bool pass = workloadOk && avgMs <= 4.0 && maxPauseMs <= 16.0;
        Console.WriteLine($"S13 measurements: avg tick {avgMs:F3} ms (limit 4.000); max tick wall {maxWallMs:F3} ms at tick {maxWallAtSimTick}; max GC pause in a tick {maxPauseMs:F3} ms (limit 16.000)");
        Console.WriteLine($"  sample figures {minFigures:N0}–{maxFigures:N0}; sample ticks {sampleTicks:N0}; digest {runner.Digest():x16}; final {runner.CurrentChecksum:x16}");
        Console.WriteLine(GetS13StatusLine(IsDiagnosticSample(sampleMinutes), pass));
        return pass;
    }

    private static bool RunSoakGate(ContentPack pack, int matches, int minutes)
    {
        long ticks = checked((long)minutes * 600);
        const int checkpointTicks = 750; // five seasons; every checkpoint exercises full snapshot validation and restore.
        Console.WriteLine($"S15 headless soak: {matches} matches, {minutes} minutes each; all six archetypes × four AI levels; snapshot checks every five seasons; {WorkerCount} workers");
        var failures = new ConcurrentQueue<string>();
        int completed = 0;
        var timer = Stopwatch.StartNew();
        Parallel.For(0, matches, new ParallelOptions { MaxDegreeOfParallelism = WorkerCount }, index =>
        {
            ulong seed = FirstSeed + (ulong)index;
            try
            {
                var archetypes = new EconomyArchetype[4];
                var controls = new CivControl[4];
                for (int c = 0; c < 4; c++)
                {
                    // Across every block of six matches, each archetype/difficulty pair is exercised.
                    int pair = (index * 4 + c) % 24;
                    archetypes[c] = Archetypes[pair % Archetypes.Length];
                    controls[c] = CivControl.Ai(Difficulties[pair / Archetypes.Length], archetypes[c]);
                }
                GeneratedWorld world = WorldGenerator.Generate(seed);
                MatchModel match = MatchModel.Create(seed, pack, world, archetypes, controls, playerCouncil: false);
                MatchModel restored = MatchModel.Create(seed, pack, world, archetypes, controls, playerCouncil: false);
                // S15 validates the full horizon at each snapshot boundary. Recording checksums
                // between those boundaries adds full-state traversals without adding a soak check.
                var runner = new SimRunner(match, checkpointTicks);
                using var snapshot = new MemoryStream();
                using var writer = new BinaryWriter(snapshot, System.Text.Encoding.UTF8, leaveOpen: true);
                using var reader = new BinaryReader(snapshot, System.Text.Encoding.UTF8, leaveOpen: true);
                while (runner.Tick < ticks)
                {
                    int step = (int)Math.Min(checkpointTicks, ticks - runner.Tick);
                    runner.Advance(step);
                    if (runner.Tick % checkpointTicks != 0 && runner.Tick != ticks) continue;
                    snapshot.Position = 0;
                    snapshot.SetLength(0);
                    match.WriteSnapshot(writer);
                    writer.Flush();
                    snapshot.Position = 0;
                    restored.ReadSnapshot(reader);
                    // Snapshot checkpoints align with periodic runner checksums in the common case.
                    // Reuse that already-computed live hash instead of traversing the whole model a
                    // second time; the final horizon may not align, so compute it when needed.
                    ulong expected = runner.Checksums.Count > 0 &&
                        runner.Checksums[runner.Checksums.Count - 1].Tick == runner.Tick
                        ? runner.Checksums[runner.Checksums.Count - 1].Hash
                        : runner.CurrentChecksum;
                    ulong actual = restored.ComputeChecksum();
                    if (actual != expected)
                        throw new InvalidDataException($"snapshot restore checksum mismatch at tick {runner.Tick}: {expected:x16} != {actual:x16}");
                }
            }
            catch (Exception e)
            {
                failures.Enqueue($"match {index} seed {seed}: {e.GetType().Name}: {e.Message}");
            }
            int done = Interlocked.Increment(ref completed);
            Console.WriteLine($"  S15 progress {done}/{matches}; {timer.Elapsed.TotalMinutes:F1} min elapsed");
        });
        timer.Stop();
        Console.WriteLine($"S15 result: {matches - failures.Count}/{matches} clean; elapsed {timer.Elapsed:hh\\:mm\\:ss}");
        foreach (string failure in failures) Console.WriteLine("  FAIL " + failure);
        return failures.IsEmpty && matches == 1_000;
    }

    private static EconomyArchetype[] ArchetypesFor(int index)
    {
        var result = new EconomyArchetype[4];
        for (int c = 0; c < result.Length; c++) result[c] = Archetypes[(index + c) % Archetypes.Length];
        return result;
    }

    private static int WorkerCount => Math.Max(1, Environment.ProcessorCount - 1);

    private static string Next(string[] args, ref int i)
    {
        if (++i >= args.Length) throw new ArgumentException($"Missing value for {args[i - 1]}.");
        return args[i];
    }
}
