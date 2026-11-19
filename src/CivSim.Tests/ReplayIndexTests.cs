using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using CivSim.Harness;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

public sealed class ReplayIndexTests
{
    private static MatchReplay Record(ulong seed, long ticks = 2)
    {
        GeneratedWorld world = WorldGenerator.Generate(seed);
        var seats = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        MatchModel model = MatchModel.Create(seed, TestPack.Pack, world, seats, default, rules: ChapterRules.Fief);
        var runner = new SimRunner(model, checksumInterval: 1);
        runner.Advance(ticks);
        ulong fingerprint = WorldEconomyInput.FromGeneratedWorld(world).Fingerprint;
        return MatchReplay.FromRun(seed, TestPack.Pack, seats, runner, fingerprint, ChapterRules.Fief);
    }

    private static string WriteReplay(string directory, ulong seed, MatchReplay replay, bool corruptLastChecksum = false)
    {
        string path = Path.Combine(directory, $"{seed}.replay");
        using var stream = new MemoryStream();
        replay.Write(stream);
        byte[] bytes = stream.ToArray();
        if (corruptLastChecksum) bytes[^1] ^= 1;
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void Batch_index_is_seed_sorted_and_keeps_missing_or_duplicate_seeds_as_failures()
    {
        string root = Path.Combine(Path.GetTempPath(), "civsim-replay-index-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string nine = WriteReplay(root, 9, Record(9));
            string sevenA = WriteReplay(root, 7, Record(7));
            string sevenB = Path.Combine(root, "07.replay");
            File.Copy(sevenA, sevenB);
            var artifact = ReplayIndexCommand.Build(TestPack.Pack, new ulong[] { 9, 2, 7 },
                new[] { (9UL, nine), (7UL, sevenA), (7UL, sevenB) });

            Assert.Equal(new ulong[] { 2, 7, 9 }, artifact.Seeds.Select(static item => item.Seed));
            Assert.False(artifact.Seeds[0].Verified);
            Assert.Contains("missing", artifact.Seeds[0].VerificationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.False(artifact.Seeds[1].Verified);
            Assert.Contains("exactly one", artifact.Seeds[1].VerificationMessage, StringComparison.Ordinal);
            Assert.True(artifact.Seeds[2].Verified);
            Assert.Equal(2, artifact.Seeds[2].ChecksumCount);
            Assert.Equal(1, artifact.Seeds[2].ChecksumInterval);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Batch_index_rejects_duplicate_manifest_seeds()
    {
        Assert.Throws<InvalidDataException>(() => ReplayIndexCommand.Build(TestPack.Pack,
            new ulong[] { 5, 5 }, Array.Empty<(ulong Seed, string Path)>()));
    }

    [Fact]
    public void Harness_index_command_writes_the_stable_replay_checksum_sequence()
    {
        string root = Path.Combine(Path.GetTempPath(), "civsim-replay-index-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string replayDirectory = Path.Combine(root, "replays");
            Directory.CreateDirectory(replayDirectory);
            WriteReplay(replayDirectory, 22, Record(22));
            WriteReplay(replayDirectory, 21, Record(21));
            string packPath = Path.Combine(root, "pack.json");
            string seedsPath = Path.Combine(root, "seeds.txt");
            string outputPath = Path.Combine(root, "index.json");
            File.WriteAllBytes(packPath, TestPack.Pack.Bytes);
            File.WriteAllText(seedsPath, "22\n21\n");

            Assert.Equal(0, ReplayIndexCommand.Run(new[] { "--replay-dir", replayDirectory, "--pack", packPath,
                "--seeds-file", seedsPath, "--output", outputPath }));
            using JsonDocument json = JsonDocument.Parse(File.ReadAllBytes(outputPath));
            JsonElement rootElement = json.RootElement;
            Assert.Equal("civsim-replay-index-v1", rootElement.GetProperty("schema").GetString());
            JsonElement[] seeds = rootElement.GetProperty("seeds").EnumerateArray().ToArray();
            Assert.Equal(new[] { 21UL, 22UL }, seeds.Select(static item => item.GetProperty("seed").GetUInt64()));
            JsonElement first = seeds[0];
            Assert.True(first.GetProperty("verified").GetBoolean());
            Assert.Equal("fief", first.GetProperty("mode").GetString());
            Assert.Equal(2, first.GetProperty("ticks").GetInt32());
            Assert.Equal(0, first.GetProperty("commandCount").GetInt32());
            Assert.Equal(2, first.GetProperty("checksumCount").GetInt32());
            Assert.Equal(16, first.GetProperty("checksums")[0].GetProperty("checksum").GetString()!.Length);
            Assert.Equal(1, first.GetProperty("checksums")[0].GetProperty("tick").GetInt32());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Harness_index_command_refuses_to_overwrite_an_existing_output()
    {
        string root = Path.Combine(Path.GetTempPath(), "civsim-replay-index-no-clobber-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string replayDirectory = Path.Combine(root, "replays");
            Directory.CreateDirectory(replayDirectory);
            string outputPath = Path.Combine(root, "index.json");
            byte[] original = System.Text.Encoding.UTF8.GetBytes("existing evidence\n");
            File.WriteAllBytes(outputPath, original);

            IOException error = Assert.Throws<IOException>(() => ReplayIndexCommand.Run(new[] {
                "--replay-dir", replayDirectory, "--pack", Path.Combine(root, "unused.pack"),
                "--seeds-file", Path.Combine(root, "unused-seeds.txt"), "--output", outputPath
            }));

            Assert.Contains("Refusing to overwrite", error.Message, StringComparison.Ordinal);
            Assert.Equal(original, File.ReadAllBytes(outputPath));
            Assert.Empty(Directory.EnumerateFiles(root, ".index.json.*.tmp"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Batch_index_marks_corrupt_replays_and_checksum_mismatches_failed_but_keeps_recorded_checksums()
    {
        string root = Path.Combine(Path.GetTempPath(), "civsim-replay-corrupt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string broken = Path.Combine(root, "11.replay");
            File.WriteAllBytes(broken, new byte[] { 0x01, 0x02, 0x03 });
            string checksumBroken = WriteReplay(root, 12, Record(12), corruptLastChecksum: true);
            var artifact = ReplayIndexCommand.Build(TestPack.Pack, new ulong[] { 11, 12 },
                new[] { (11UL, broken), (12UL, checksumBroken) });

            Assert.All(artifact.Seeds, static entry => Assert.False(entry.Verified));
            Assert.NotEmpty(artifact.Seeds[0].VerificationMessage);
            Assert.Equal(2, artifact.Seeds[1].Checksums.Count);
            Assert.Equal(2, artifact.Seeds[1].Ticks);
            Assert.Contains("Checksum mismatch", artifact.Seeds[1].VerificationMessage, StringComparison.Ordinal);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
