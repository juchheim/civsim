using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Toy;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>
/// Headless runner (docs/MVP/15 A9). At M0 it drives the toy determinism canary; later it will run full
/// AI tournaments and regression suites.
/// <code>
/// civsim-harness toy [--seed N] [--ticks N] [--checksum-every N]      run and print the result
/// civsim-harness toy --check-golden FILE                              compare against a golden file (CI)
/// civsim-harness toy --write-golden FILE                              (re)write the golden file
/// civsim-harness toy --record FILE                                    write a replay file
/// civsim-harness worldgen [--seed N] [--audit-seeds N] [--check-fingerprint FILE]
///                                                                      generate/report, sweep seeds, or verify a golden fingerprint
/// civsim-harness replay verify FILE                                   re-run a replay and verify checksums
/// </code>
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0) return Usage();
            return args[0] switch
            {
                "toy" => RunToy(args[1..]),
                "worldgen" => RunWorldGen(args[1..]),
                "replay" when args.Length >= 3 && args[1] == "verify" => VerifyReplay(args[2]),
                _ => Usage(),
            };
        }
        catch (Exception e) when (e is IOException or InvalidDataException or FormatException or ArgumentException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 2;
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("usage: civsim-harness toy [--seed N] [--ticks N] [--checksum-every N] [--check-golden FILE | --write-golden FILE | --record FILE]");
        Console.Error.WriteLine("       civsim-harness worldgen [--seed N] [--audit-seeds N] [--check-fingerprint FILE]");
        Console.Error.WriteLine("       civsim-harness replay verify FILE");
        return 2;
    }

    private static int RunToy(string[] args)
    {
        ulong seed = ToyScenario.GoldenSeed;
        long ticks = ToyScenario.GoldenTicks;
        int interval = SimRunner.DefaultChecksumInterval;
        string? checkGolden = null, writeGolden = null, record = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seed": seed = ulong.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--ticks": ticks = long.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--checksum-every": interval = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--check-golden": checkGolden = Next(args, ref i); break;
                case "--write-golden": writeGolden = Next(args, ref i); break;
                case "--record": record = Next(args, ref i); break;
                default: return Usage();
            }
        }

        var runner = new SimRunner(new ToyModel(seed), interval);
        foreach (Command c in ToyScenario.Commands(seed, ticks)) runner.Submit(c);
        runner.Advance(ticks);

        string result = Describe(seed, ticks, interval, runner);
        Console.WriteLine(result);

        if (record is not null)
        {
            Replay replay = Replay.Record(new ToyModel(seed), seed, ticks, ToyScenario.Commands(seed, ticks), interval);
            using FileStream stream = File.Create(record);
            replay.Write(stream);
            Console.WriteLine($"wrote replay {record}");
        }
        if (writeGolden is not null)
        {
            File.WriteAllText(writeGolden, result + "\n");
            Console.WriteLine($"wrote golden {writeGolden}");
        }
        if (checkGolden is not null)
        {
            string expected = File.ReadAllText(checkGolden).Replace("\r\n", "\n").Trim();
            if (expected != result.Trim())
            {
                Console.Error.WriteLine("DETERMINISM CHECK FAILED");
                Console.Error.WriteLine("expected:");
                Console.Error.WriteLine(expected);
                Console.Error.WriteLine("actual:");
                Console.Error.WriteLine(result);
                return 1;
            }
            Console.WriteLine("determinism check passed");
        }
        return 0;
    }

    private static int RunWorldGen(string[] args)
    {
        ulong seed = 1;
        int auditSeeds = 1;
        string? checkFingerprint = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seed": seed = ulong.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--audit-seeds": auditSeeds = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--check-fingerprint": checkFingerprint = Next(args, ref i); break;
                default: return Usage();
            }
        }
        if (auditSeeds < 1) throw new ArgumentOutOfRangeException(nameof(auditSeeds), "Audit count must be positive.");
        if (checkFingerprint is not null && auditSeeds != 1)
            throw new ArgumentException("Fingerprint checks require exactly one seed.");

        int failures = 0, riverStarts = 0, coastalStarts = 0, accepted = 0;
        int minLand = int.MaxValue, maxLand = 0, maxGenerationMs = 0;
        int minShare = int.MaxValue, maxShare = 0;
        GeneratedWorld? firstWorld = null;
        var startBiomeCounts = new int[Enum.GetValues<Biome>().Length];
        var windowStartBiomeCounts = new int[Enum.GetValues<Biome>().Length];
        bool varietyWindowFailed = false;
        var all = Stopwatch.StartNew();
        int progressInterval = auditSeeds >= 10 ? 10 : 0;
        for (int offset = 0; offset < auditSeeds; offset++)
        {
            ulong currentSeed = checked(seed + (ulong)offset);
            var generationTimer = Stopwatch.StartNew();
            GeneratedWorld world;
            try { world = WorldGenerator.Generate(currentSeed); }
            catch (InvalidOperationException e)
            {
                generationTimer.Stop();
                int failedGenerationMs = checked((int)generationTimer.ElapsedMilliseconds);
                maxGenerationMs = Math.Max(maxGenerationMs, failedGenerationMs);
                failures++;
                string error = $"seed {currentSeed}: generation rejected: {e.Message}";
                Console.WriteLine("FAIL " + error);
                if (progressInterval > 0 && (offset + 1) % progressInterval == 0)
                    Console.WriteLine($"audited {offset + 1:N0}/{auditSeeds:N0} seeds; accepted={accepted} failures={failures}");
                continue;
            }
            if (offset == 0) firstWorld = world;
            generationTimer.Stop();
            int generationMs = checked((int)generationTimer.ElapsedMilliseconds);
            if (generationMs > maxGenerationMs) maxGenerationMs = generationMs;
            WorldAcceptanceResult result = WorldAcceptance.Validate(world);
            if (result.Passed) accepted++;
            else
            {
                failures++;
                foreach (string error in result.Failures)
                    Console.WriteLine($"FAIL seed {currentSeed}: {error}");
            }
            riverStarts += result.RiverStarts;
            coastalStarts += result.CoastalStarts;
            minLand = Math.Min(minLand, world.Report.LandShareBasisPoints);
            maxLand = Math.Max(maxLand, world.Report.LandShareBasisPoints);
            foreach (int share in world.Report.E7SharesBasisPoints.Span)
            {
                minShare = Math.Min(minShare, share);
                maxShare = Math.Max(maxShare, share);
            }
            foreach (WorldStart startAt in world.Starts)
            {
                int biome = (int)world.TileAt(startAt.Tile).Biome;
                startBiomeCounts[biome]++;
                windowStartBiomeCounts[biome]++;
            }
            if (auditSeeds >= 1_000 && (offset + 1) % 1_000 == 0)
            {
                Biome[] windowMissing = RequiredStartBiomes()
                    .Where(b => windowStartBiomeCounts[(int)b] == 0).ToArray();
                ulong firstSeed = checked(seed + (ulong)(offset + 1 - 1_000));
                ulong lastSeed = currentSeed;
                if (windowMissing.Length == 0)
                    Console.WriteLine($"biome variety passed for seeds {firstSeed:N0}–{lastSeed:N0}");
                else
                {
                    varietyWindowFailed = true;
                    Console.WriteLine($"FAIL biome variety for seeds {firstSeed:N0}–{lastSeed:N0}: " +
                        string.Join(", ", windowMissing));
                }
                Array.Clear(windowStartBiomeCounts);
            }
            if (progressInterval > 0 && (offset + 1) % progressInterval == 0)
                Console.WriteLine($"audited {offset + 1:N0}/{auditSeeds:N0} seeds; accepted={accepted} failures={failures}");
        }
        all.Stop();

        int totalStarts = auditSeeds * 4;
        Console.WriteLine($"seeds={auditSeeds} accepted={accepted} failures={failures}");
        Console.WriteLine($"landShareBp={minLand}..{maxLand} riverStarts={riverStarts}/{totalStarts} coastalStarts={coastalStarts}/{totalStarts}");
        Console.WriteLine($"E7ShareBp={minShare}..{maxShare} maxGenerateMs={maxGenerationMs} totalMs={all.ElapsedMilliseconds}");
        Console.WriteLine("startBiomes=" + string.Join(", ", Enum.GetValues<Biome>().Where(b => startBiomeCounts[(int)b] > 0)
            .Select(b => $"{b}:{startBiomeCounts[(int)b]}")));
        if (auditSeeds == 1 && accepted == 1)
        {
            GeneratedWorld world = firstWorld!;
            Console.WriteLine($"fairnessAttempts={world.Report.FairnessAttempts} blockedSeasons={world.Report.BlockedSeasons} contactSeason={world.Report.FirstContactSeason}");
            var startText = new string[world.Starts.Length];
            for (int i = 0; i < world.Starts.Length; i++)
            {
                WorldStart startAt = world.Starts[i];
                startText[i] = $"{startAt.Civilization}:{startAt.Tile % world.Width},{startAt.Tile / world.Width}";
            }
            Console.WriteLine("starts=" + string.Join(", ", startText));
            Console.WriteLine("E7SharesBp=" + string.Join(",", world.Report.E7SharesBasisPoints.ToArray()));
            string fingerprintLine = $"seed={seed} fingerprint={world.Fingerprint():x16}";
            Console.WriteLine(fingerprintLine);
            if (checkFingerprint is not null)
            {
                string expected = File.ReadAllText(checkFingerprint).Replace("\r\n", "\n").Trim();
                if (expected != fingerprintLine)
                {
                    Console.Error.WriteLine("WORLD GENERATION FINGERPRINT CHECK FAILED");
                    Console.Error.WriteLine($"expected: {expected}");
                    Console.Error.WriteLine($"actual:   {fingerprintLine}");
                    return 1;
                }
                Console.WriteLine("world generation fingerprint check passed");
            }
        }
        bool hasRequiredBiomes = RequiredStartBiomes().All(b => startBiomeCounts[(int)b] > 0);
        bool varied = auditSeeds < 1000 || (!varietyWindowFailed && hasRequiredBiomes);
        bool hasRivers = riverStarts * 100 >= totalStarts * 60;
        bool hasCoastal = coastalStarts * 100 >= totalStarts * 30;
        bool fast = maxGenerationMs < 5_000;
        if (auditSeeds >= 1000 && !hasRequiredBiomes)
            Console.WriteLine("FAIL missing start biomes in the aggregate: " +
                string.Join(", ", RequiredStartBiomes().Where(b => startBiomeCounts[(int)b] == 0)));
        if (auditSeeds >= 1000 && !hasRivers) Console.WriteLine("FAIL fewer than 60% of starts have nearby rivers");
        if (auditSeeds >= 1000 && !hasCoastal) Console.WriteLine("FAIL fewer than 30% of starts have nearby coast");
        if (!fast) Console.WriteLine("FAIL a world exceeded the 5-second generation budget");
        return failures == 0 && varied && fast && (auditSeeds < 1000 || (hasRivers && hasCoastal)) ? 0 : 1;
    }

    // Desert's forage is below the §9.1 90%-of-map-median start threshold, so it is not a required type.
    private static Biome[] RequiredStartBiomes() =>
    [
        Biome.TemperateForest, Biome.OpenWoodland, Biome.GrasslandSteppe,
        Biome.Savanna, Biome.TropicalForest, Biome.Wetland
    ];

    private static string Describe(ulong seed, long ticks, int interval, SimRunner runner)
    {
        ulong digest = runner.Digest();
        return string.Join('\n',
            $"seed={seed} ticks={ticks} checksumInterval={interval} commands={runner.AppliedCommands.Count}",
            $"final={runner.CurrentChecksum:x16}",
            $"digest={digest:x16}");
    }

    private static int VerifyReplay(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Replay replay = Replay.Read(stream);
        ReplayVerification result = ReplayVerifier.Verify(replay, seed => new ToyModel(seed));
        Console.WriteLine(result.Ok ? $"replay OK ({replay.Checksums.Count} checksums, {replay.Commands.Count} commands)" : result.Message);
        return result.Ok ? 0 : 1;
    }

    private static string Next(string[] args, ref int i)
    {
        if (i + 1 >= args.Length) throw new ArgumentException($"{args[i]} needs a value.");
        return args[++i];
    }
}
