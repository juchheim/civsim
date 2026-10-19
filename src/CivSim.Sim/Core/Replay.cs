using System;
using System.Collections.Generic;
using System.IO;

namespace CivSim.Sim.Core;

/// <summary>
/// A replay: the seed, the content-pack hash, and the command log, plus the checksums a faithful re-run must
/// reproduce (docs/MVP/15 §5.6). A bug report is a replay file. The binary layout is little-endian and
/// versioned; the toy model's golden checksum in <c>tests/golden</c> guards it.
/// </summary>
public sealed class Replay
{
    public const uint FormatVersion = 1;
    private const uint Magic = 0x50525343; // "CSRP" little-endian
    public const int ContentHashLength = 32;

    public ulong MatchSeed { get; init; }
    /// <summary>SHA-256 of the content pack when known; legacy toy replays use 32 zero bytes.</summary>
    public byte[] ContentHash { get; init; } = new byte[ContentHashLength];
    public int ChecksumInterval { get; init; } = SimRunner.DefaultChecksumInterval;
    public long TotalTicks { get; init; }
    public IReadOnlyList<Command> Commands { get; init; } = Array.Empty<Command>();
    public IReadOnlyList<ChecksumRecord> Checksums { get; init; } = Array.Empty<ChecksumRecord>();

    public void Write(Stream stream)
    {
        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write(Magic);
        w.Write(FormatVersion);
        w.Write(MatchSeed);
        if (ContentHash.Length != ContentHashLength) throw new InvalidOperationException("ContentHash must be 32 bytes.");
        w.Write(ContentHash);
        w.Write(ChecksumInterval);
        w.Write(TotalTicks);
        w.Write(Commands.Count);
        foreach (Command c in Commands)
        {
            w.Write(c.Tick);
            w.Write(c.Civ);
            w.Write((ushort)c.Kind);
            w.Write(c.A);
            w.Write(c.B);
            w.Write(c.C);
        }
        w.Write(Checksums.Count);
        foreach (ChecksumRecord r in Checksums)
        {
            w.Write(r.Tick);
            w.Write(r.Hash);
        }
    }

    public static Replay Read(Stream stream)
    {
        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (r.ReadUInt32() != Magic) throw new InvalidDataException("Not a CivSim replay.");
        uint version = r.ReadUInt32();
        if (version != FormatVersion) throw new InvalidDataException($"Unsupported replay version {version}.");
        ulong seed = r.ReadUInt64();
        byte[] hash = r.ReadBytes(ContentHashLength);
        if (hash.Length != ContentHashLength) throw new InvalidDataException("Truncated replay.");
        int interval = r.ReadInt32();
        long totalTicks = r.ReadInt64();
        int commandCount = r.ReadInt32();
        if (commandCount < 0 || commandCount > 10_000_000) throw new InvalidDataException("Bad command count.");
        var commands = new List<Command>(commandCount);
        for (int i = 0; i < commandCount; i++)
        {
            long tick = r.ReadInt64();
            byte civ = r.ReadByte();
            var kind = (CommandKind)r.ReadUInt16();
            commands.Add(new Command(tick, civ, kind, r.ReadInt32(), r.ReadInt32(), r.ReadInt32()));
        }
        int checksumCount = r.ReadInt32();
        if (checksumCount < 0 || checksumCount > 10_000_000) throw new InvalidDataException("Bad checksum count.");
        var checksums = new List<ChecksumRecord>(checksumCount);
        for (int i = 0; i < checksumCount; i++) checksums.Add(new ChecksumRecord(r.ReadInt64(), r.ReadUInt64()));
        return new Replay
        {
            MatchSeed = seed,
            ContentHash = hash,
            ChecksumInterval = interval,
            TotalTicks = totalTicks,
            Commands = commands,
            Checksums = checksums,
        };
    }

    /// <summary>Runs a model for <paramref name="totalTicks"/> ticks with the given commands and captures the result as a replay.</summary>
    public static Replay Record(ISimModel model, ulong matchSeed, long totalTicks, IEnumerable<Command> commands,
        int checksumInterval = SimRunner.DefaultChecksumInterval, byte[]? contentHash = null)
    {
        if (contentHash is not null && contentHash.Length != ContentHashLength)
            throw new ArgumentException("Content hash must be 32 bytes.", nameof(contentHash));
        byte[] recordedHash = contentHash is null ? new byte[ContentHashLength] : (byte[])contentHash.Clone();
        var runner = new SimRunner(model, checksumInterval);
        foreach (Command c in commands) runner.Submit(c);
        runner.Advance(totalTicks);
        return new Replay
        {
            MatchSeed = matchSeed,
            ContentHash = recordedHash,
            ChecksumInterval = checksumInterval,
            TotalTicks = totalTicks,
            Commands = new List<Command>(runner.AppliedCommands),
            Checksums = new List<ChecksumRecord>(runner.Checksums),
        };
    }
}

/// <summary>The outcome of re-running a replay.</summary>
public readonly record struct ReplayVerification(bool Ok, long FirstMismatchTick, string Message);

public static class ReplayVerifier
{
    /// <summary>Rejects mismatched content before constructing or running a model.</summary>
    public static ReplayVerification Verify(Replay replay, Func<ulong, ISimModel> modelFactory,
        ReadOnlySpan<byte> expectedContentHash)
    {
        if (expectedContentHash.Length != Replay.ContentHashLength ||
            !expectedContentHash.SequenceEqual(replay.ContentHash))
            return new ReplayVerification(false, -1, "Replay content hash differs from the loaded pack.");
        return Verify(replay, modelFactory);
    }

    /// <summary>
    /// Re-runs the replay on a fresh model (from <paramref name="modelFactory"/>, given the match seed) and
    /// compares every recorded checksum. A mismatch means the simulation is no longer deterministic or the
    /// build or content differs from the one that recorded it.
    /// </summary>
    public static ReplayVerification Verify(Replay replay, Func<ulong, ISimModel> modelFactory)
    {
        var runner = new SimRunner(modelFactory(replay.MatchSeed), replay.ChecksumInterval);
        foreach (Command c in replay.Commands) runner.Submit(c);
        runner.Advance(replay.TotalTicks);
        if (runner.Checksums.Count != replay.Checksums.Count)
            return new ReplayVerification(false, -1,
                $"Expected {replay.Checksums.Count} checksums, produced {runner.Checksums.Count}.");
        for (int i = 0; i < replay.Checksums.Count; i++)
        {
            if (runner.Checksums[i] != replay.Checksums[i])
                return new ReplayVerification(false, replay.Checksums[i].Tick,
                    $"Checksum mismatch at tick {replay.Checksums[i].Tick}: expected {replay.Checksums[i].Hash:x16}, got {runner.Checksums[i].Hash:x16}.");
        }
        return new ReplayVerification(true, -1, "OK");
    }
}
