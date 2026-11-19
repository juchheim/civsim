using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CivSim.Sim.Content;
using CivSim.Sim.Research;

namespace CivSim.Harness;

/// <summary>Verifies a replay batch and writes a deterministic index suitable for host-to-host comparison.</summary>
public static class ReplayIndexCommand
{
    private const string Schema = "civsim-replay-index-v1";

    public sealed record ChecksumEntry(long Tick, string Checksum);
    public sealed record SeedEntry(ulong Seed, string ReplayFile, long? Ticks, int? CommandCount, int? ChecksumInterval,
        int? ChecksumCount, string PackHash, string Mode, string? FinalChecksum, IReadOnlyList<ChecksumEntry> Checksums, bool Verified,
        long? FirstMismatchTick, string VerificationMessage);
    public sealed record Artifact(string Schema, string PackHash, IReadOnlyList<SeedEntry> Seeds);

    public static int Run(string[] args)
    {
        string? replayDirectory = null, packPath = null, seedsPath = null, outputPath = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--replay-dir": replayDirectory = Next(args, ref i); break;
                case "--pack": packPath = Next(args, ref i); break;
                case "--seeds-file": seedsPath = Next(args, ref i); break;
                case "--output": outputPath = Next(args, ref i); break;
                default: throw new ArgumentException($"Unknown replay index option '{args[i]}'.");
            }
        }
        if (replayDirectory is null || packPath is null || seedsPath is null || outputPath is null)
            throw new ArgumentException("replay index requires --replay-dir DIR --pack FILE --seeds-file FILE --output FILE.");
        if (!Directory.Exists(replayDirectory)) throw new DirectoryNotFoundException($"Replay directory not found: {replayDirectory}");
        string absoluteOutputPath = Path.GetFullPath(outputPath);
        if (File.Exists(absoluteOutputPath) || Directory.Exists(absoluteOutputPath))
            throw new IOException($"Refusing to overwrite existing replay index output: {absoluteOutputPath}");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        ulong[] expectedSeeds = ReadSeeds(seedsPath);
        (ulong Seed, string Path)[] paths = ReadReplayPaths(replayDirectory);
        Artifact artifact = Build(pack, expectedSeeds, paths);
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        WriteAtomicallyWithoutOverwrite(absoluteOutputPath, artifact, options);
        int verified = artifact.Seeds.Count(static s => s.Verified);
        Console.WriteLine($"replay index: {verified}/{artifact.Seeds.Count} verified; wrote {outputPath}");
        return verified == artifact.Seeds.Count ? 0 : 1;
    }

    private static void WriteAtomicallyWithoutOverwrite(string outputPath, Artifact artifact, JsonSerializerOptions options)
    {
        string directory = Path.GetDirectoryName(outputPath)!;
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, artifact, options);
                stream.Flush(flushToDisk: true);
            }
            // Same-directory move is an atomic publication; overwrite:false protects against races.
            File.Move(temporaryPath, outputPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    /// <summary>Creates one entry for every expected seed, preserving seed-file order and representing absent/bad files as failures.</summary>
    public static Artifact Build(ContentPack pack, IReadOnlyList<ulong> expectedSeeds, IReadOnlyList<(ulong Seed, string Path)> replayPaths)
    {
        ArgumentNullException.ThrowIfNull(pack);
        if (expectedSeeds.Count == 0) throw new InvalidDataException("The expected seed list is empty.");
        var seen = new HashSet<ulong>();
        foreach (ulong seed in expectedSeeds)
            if (!seen.Add(seed)) throw new InvalidDataException($"Duplicate expected seed {seed}.");
        ulong[] orderedSeeds = expectedSeeds.OrderBy(static seed => seed).ToArray();

        var pathsBySeed = new Dictionary<ulong, List<string>>();
        foreach ((ulong seed, string path) in replayPaths)
        {
            if (!seen.Contains(seed)) throw new InvalidDataException($"Replay file '{Path.GetFileName(path)}' contains unrequested seed {seed}.");
            if (!pathsBySeed.TryGetValue(seed, out List<string>? paths)) pathsBySeed[seed] = paths = new List<string>();
            paths.Add(path);
        }

        string packHash = Convert.ToHexString(pack.Hash).ToLowerInvariant();
        var entries = new List<SeedEntry>(orderedSeeds.Length);
        foreach (ulong seed in orderedSeeds)
        {
            if (!pathsBySeed.TryGetValue(seed, out List<string>? paths))
            {
                entries.Add(Failure(seed, $"{seed}.replay", packHash, "Replay file is missing."));
                continue;
            }
            if (paths.Count != 1)
            {
                entries.Add(Failure(seed, $"{seed}.replay", packHash, $"Found {paths.Count} replay files for seed {seed}; expected exactly one."));
                continue;
            }
            string path = paths[0];
            string fileName = Path.GetFileName(path);
            try
            {
                using FileStream stream = File.OpenRead(path);
                MatchReplay replay = MatchReplay.Read(stream);
                if (replay.Replay.MatchSeed != seed)
                    throw new InvalidDataException($"Replay seed {replay.Replay.MatchSeed} does not match expected seed {seed}.");
                var verification = replay.Verify(pack);
                entries.Add(new SeedEntry(seed, fileName, replay.Replay.TotalTicks, replay.Replay.Commands.Count,
                    replay.Replay.ChecksumInterval, replay.Replay.Checksums.Count, packHash,
                    replay.Rules.Mode.ToString().ToLowerInvariant(), replay.FinalChecksum.ToString("x16", CultureInfo.InvariantCulture),
                    replay.Replay.Checksums.Select(static c => new ChecksumEntry(c.Tick, c.Hash.ToString("x16", CultureInfo.InvariantCulture))).ToArray(),
                    verification.Ok, verification.FirstMismatchTick < 0 ? null : verification.FirstMismatchTick, verification.Message));
            }
            catch (Exception e) when (e is IOException or ArgumentException or FormatException)
            {
                entries.Add(Failure(seed, fileName, packHash, e.Message));
            }
        }
        return new Artifact(Schema, packHash, entries);
    }

    private static SeedEntry Failure(ulong seed, string file, string packHash, string message) =>
        new(seed, file, null, null, null, null, packHash, "unknown", null, Array.Empty<ChecksumEntry>(), false, null, message);

    private static ulong[] ReadSeeds(string path)
    {
        string[] lines = File.ReadAllLines(path);
        var seeds = new ulong[lines.Length];
        var seen = new HashSet<ulong>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (!ulong.TryParse(lines[i].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out seeds[i]))
                throw new InvalidDataException($"Invalid unsigned seed on line {i + 1} of '{path}'.");
            if (!seen.Add(seeds[i])) throw new InvalidDataException($"Duplicate expected seed {seeds[i]} on line {i + 1} of '{path}'.");
        }
        if (seeds.Length == 0) throw new InvalidDataException("The expected seed list is empty.");
        return seeds;
    }

    private static (ulong Seed, string Path)[] ReadReplayPaths(string directory)
    {
        var result = new List<(ulong Seed, string Path)>();
        foreach (string path in Directory.EnumerateFiles(directory, "*.replay", SearchOption.TopDirectoryOnly))
        {
            string stem = Path.GetFileNameWithoutExtension(path);
            if (!ulong.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed))
                throw new InvalidDataException($"Replay filename '{Path.GetFileName(path)}' must be named <seed>.replay.");
            result.Add((seed, path));
        }
        return result.OrderBy(static p => p.Seed).ThenBy(static p => p.Path, StringComparer.Ordinal).ToArray();
    }

    private static string Next(string[] args, ref int index)
    {
        if (index + 1 >= args.Length) throw new ArgumentException($"{args[index]} needs a value.");
        return args[++index];
    }
}
