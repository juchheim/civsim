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
        bool s1 = false, s13 = false, s15 = false;
        int matches = 1_000, minutes = 10, sampleMinutes = 10;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pack": packPath = Next(args, ref i); break;
                case "--s1": s1 = true; break;
                case "--s13": s13 = true; break;
                case "--s15": s15 = true; break;
                case "--matches": matches = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--minutes": minutes = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--sample-minutes": sampleMinutes = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                default: throw new ArgumentException($"Unknown option '{args[i]}'.");
            }
        }
        if (packPath is null) throw new ArgumentException("acceptance needs --pack FILE.");
        if (!s1 && !s13 && !s15) throw new ArgumentException("Choose at least one of --s1, --s13, or --s15.");
        if (matches <= 0 || minutes <= 0 || sampleMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(matches), "Match and sample counts must be positive.");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));

        bool pass = true;
        if (s1) pass &= RunReplayGate(pack, matches, minutes);
        if (s13) pass &= RunTickGate(pack, sampleMinutes);
        if (s15) pass &= RunSoakGate(pack, matches, minutes);
        Console.WriteLine(pass ? "ACCEPTANCE GATES PASSED" : "ACCEPTANCE GATES FAILED");
        return pass ? 0 : 1;
    }

    private static bool RunReplayGate(ContentPack pack, int matches, int minutes)
    {
        long ticks = checked((long)minutes * 600);
        Console.WriteLine($"S1 replay determinism: {matches} matches, {minutes} minutes each, binary replay round trip + every checksum");
        var failures = new ConcurrentQueue<string>();
        int completed = 0;
        var timer = Stopwatch.StartNew();
        Parallel.For(0, matches, new ParallelOptions { MaxDegreeOfParallelism = WorkerCount }, index =>
        {
            try
            {
                ulong seed = FirstSeed + (ulong)index;
                var archetypes = ArchetypesFor(index);
                MatchModel model = MatchModel.Create(seed, pack, archetypes);
                var runner = new SimRunner(model);
                foreach (Command command in ReplayCommands(ticks, pack)) runner.Submit(command);
                runner.Advance(ticks);

                ulong recordedFinal = runner.CurrentChecksum;
                MatchReplay recorded = MatchReplay.FromRun(seed, pack, archetypes, runner,
                    WorldEconomyInput.FromGeneratedWorld(WorldGenerator.Generate(seed)).Fingerprint);
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
            if (done % 50 == 0 || done == matches) Console.WriteLine($"  S1 progress {done}/{matches}");
        });
        timer.Stop();
        Console.WriteLine($"S1 result: {matches - failures.Count}/{matches} replay checks matched; elapsed {timer.Elapsed:hh\\:mm\\:ss}");
        foreach (string failure in failures) Console.WriteLine("  FAIL " + failure);
        return failures.IsEmpty && matches == 1_000;
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
        Console.WriteLine(pass ? "S13 PASS" : "S13 FAIL");
        return pass;
    }

    private static bool RunSoakGate(ContentPack pack, int matches, int minutes)
    {
        long ticks = checked((long)minutes * 600);
        const int checkpointTicks = 750; // five seasons; every checkpoint exercises full snapshot validation and restore.
        Console.WriteLine($"S15 headless soak: {matches} matches, {minutes} minutes each; all six archetypes × four AI levels; snapshot checks every five seasons");
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
                var runner = new SimRunner(match);
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
                    ulong expected = match.ComputeChecksum();
                    ulong actual = restored.ComputeChecksum();
                    if (actual != expected)
                        throw new InvalidDataException($"snapshot restore checksum mismatch at tick {runner.Tick}: {expected:x16} != {actual:x16}");
                    if (runner.CurrentChecksum != expected)
                        throw new InvalidDataException($"runner checksum differs at tick {runner.Tick}");
                }
            }
            catch (Exception e)
            {
                failures.Enqueue($"match {index} seed {seed}: {e.GetType().Name}: {e.Message}");
            }
            int done = Interlocked.Increment(ref completed);
            if (done % 50 == 0 || done == matches) Console.WriteLine($"  S15 progress {done}/{matches}");
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
