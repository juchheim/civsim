using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.View;

namespace CivSim.Game;

/// <summary>Versioned Godot-side container for a sim snapshot and its replay history.</summary>
internal sealed class GameSaveData
{
    public ulong Seed;
    public int ViewerCiv;
    /// <summary>Rival difficulty; version 1 saves were all played at Chieftain.</summary>
    public Difficulty Difficulty = Difficulty.Chieftain;
    public long Tick;
    public int ChecksumInterval;
    public int SpeedIndex;
    public bool Paused;
    public ulong StateChecksum;
    public byte[] PackHash = Array.Empty<byte>();
    public byte[] Snapshot = Array.Empty<byte>();
    public byte[] ViewState = Array.Empty<byte>();
    public List<Command> Commands = new();
    public List<ChecksumRecord> Checksums = new();
    public List<Command> QueuedCommands = new();
    public List<AiWhyEntry> AiWhyEntries = new();
    public ulong OnboardingSeen;
    public bool IntroDismissed;

    public Replay ToReplay() => new()
    {
        MatchSeed = Seed,
        ContentHash = (byte[])PackHash.Clone(),
        ChecksumInterval = ChecksumInterval,
        TotalTicks = Tick,
        Commands = new List<Command>(Commands),
        Checksums = new List<ChecksumRecord>(Checksums),
    };
}

internal static class GameSave
{
    private const uint Magic = 0x53475343; // "CSGS", little-endian
    private const uint Version = 2;
    private const uint FirstVersionWithDifficulty = 2;
    private const int MaxSnapshotBytes = 512 * 1024 * 1024;
    private const int MaxLogEntries = 10_000_000;

    public static string QuickSavePath => Path.Combine(Godot.OS.GetUserDataDir(), "saves", "quicksave.civsave");
    public static string LatestReplayPath => Path.Combine(Godot.OS.GetUserDataDir(), "replays", "latest.csr");

    private const uint ReplayDifficultyMagic = 0x46444943; // "CIDF", little-endian

    /// <summary>
    /// A replay file is the sim's <see cref="Replay"/> followed by a small footer naming the rival difficulty, which the
    /// replay format itself does not carry but a faithful re-run needs. Files without a footer were played at Chieftain.
    /// </summary>
    public static void WriteReplay(Stream stream, Replay replay, Difficulty difficulty)
    {
        replay.Write(stream);
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(ReplayDifficultyMagic);
        writer.Write((byte)difficulty);
    }

    public static Replay ReadReplay(Stream stream, out Difficulty difficulty)
    {
        Replay replay = Replay.Read(stream);
        difficulty = Difficulty.Chieftain;
        if (stream.Length - stream.Position >= 5)
        {
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            if (reader.ReadUInt32() == ReplayDifficultyMagic)
            {
                byte level = reader.ReadByte();
                if (Enum.IsDefined(typeof(Difficulty), level)) difficulty = (Difficulty)level;
            }
        }
        return replay;
    }

    public static void Write(string path, GameSaveData data)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        string temporary = fullPath + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: false))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write(data.Seed);
                writer.Write(data.ViewerCiv);
                writer.Write(data.Tick);
                writer.Write(data.ChecksumInterval);
                writer.Write(data.SpeedIndex);
                writer.Write(data.Paused);
                writer.Write(data.StateChecksum);
                if (data.PackHash.Length != Replay.ContentHashLength)
                    throw new InvalidDataException("The save content hash is not SHA-256.");
                writer.Write(data.PackHash);
                WriteBytes(writer, data.Snapshot);
                WriteBytes(writer, data.ViewState);
                WriteCommands(writer, data.Commands);
                WriteChecksums(writer, data.Checksums);
                WriteCommands(writer, data.QueuedCommands);
                writer.Write(data.AiWhyEntries.Count);
                foreach (AiWhyEntry entry in data.AiWhyEntries)
                {
                    writer.Write(entry.Tick);
                    writer.Write(entry.Civ);
                    writer.Write(entry.Layer);
                    writer.Write(entry.Kind);
                    writer.Write(entry.ScoreRaw);
                    writer.Write(entry.Reason);
                }
                writer.Write(data.OnboardingSeen);
                writer.Write(data.IntroDismissed);
                writer.Write((byte)data.Difficulty);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, fullPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            throw;
        }
    }

    public static GameSaveData Read(string path, ReadOnlySpan<byte> expectedPackHash)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt32() != Magic) throw new InvalidDataException("Unsupported CivSim save file.");
        uint version = reader.ReadUInt32();
        if (version is < 1 or > Version) throw new InvalidDataException("Unsupported CivSim save file.");

        var data = new GameSaveData
        {
            Seed = reader.ReadUInt64(),
            ViewerCiv = reader.ReadInt32(),
            Tick = reader.ReadInt64(),
            ChecksumInterval = reader.ReadInt32(),
            SpeedIndex = reader.ReadInt32(),
            Paused = reader.ReadBoolean(),
            StateChecksum = reader.ReadUInt64(),
            PackHash = ReadFixed(reader, Replay.ContentHashLength),
            Snapshot = ReadBytes(reader, MaxSnapshotBytes),
            ViewState = ReadBytes(reader, MaxSnapshotBytes),
            Commands = ReadCommands(reader),
            Checksums = ReadChecksums(reader),
            QueuedCommands = ReadCommands(reader),
            AiWhyEntries = ReadAiWhy(reader),
            OnboardingSeen = reader.ReadUInt64(),
            IntroDismissed = reader.ReadBoolean(),
        };
        if (version >= FirstVersionWithDifficulty)
        {
            byte level = reader.ReadByte();
            if (!Enum.IsDefined(typeof(Difficulty), level)) throw new InvalidDataException("Invalid saved difficulty.");
            data.Difficulty = (Difficulty)level;
        }
        if (!data.PackHash.AsSpan().SequenceEqual(expectedPackHash))
            throw new InvalidDataException("Save content hash differs from the loaded content pack.");
        if (data.Tick < 0 || data.ChecksumInterval <= 0 || data.SpeedIndex is < 0 or > 3 ||
            data.ViewerCiv is < -1 or > 3 || data.Snapshot.Length == 0 || data.ViewState.Length == 0)
            throw new InvalidDataException("Invalid CivSim save header.");
        if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected trailing data in CivSim save.");
        return data;
    }

    private static void WriteBytes(BinaryWriter writer, byte[] bytes)
    {
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static byte[] ReadBytes(BinaryReader reader, int maximum)
    {
        int length = reader.ReadInt32();
        if (length < 0 || length > maximum) throw new InvalidDataException("Invalid save snapshot size.");
        return ReadFixed(reader, length);
    }

    private static byte[] ReadFixed(BinaryReader reader, int length)
    {
        byte[] result = reader.ReadBytes(length);
        if (result.Length != length) throw new InvalidDataException("Truncated CivSim save.");
        return result;
    }

    private static void WriteCommands(BinaryWriter writer, IReadOnlyList<Command> commands)
    {
        writer.Write(commands.Count);
        foreach (Command command in commands)
        {
            writer.Write(command.Tick);
            writer.Write(command.Civ);
            writer.Write((ushort)command.Kind);
            writer.Write(command.A);
            writer.Write(command.B);
            writer.Write(command.C);
        }
    }

    private static List<Command> ReadCommands(BinaryReader reader)
    {
        int count = ReadCount(reader);
        var commands = new List<Command>(count);
        for (int i = 0; i < count; i++)
            commands.Add(new Command(reader.ReadInt64(), reader.ReadByte(), (CommandKind)reader.ReadUInt16(),
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));
        return commands;
    }

    private static void WriteChecksums(BinaryWriter writer, IReadOnlyList<ChecksumRecord> checksums)
    {
        writer.Write(checksums.Count);
        foreach (ChecksumRecord checksum in checksums)
        {
            writer.Write(checksum.Tick);
            writer.Write(checksum.Hash);
        }
    }

    private static List<ChecksumRecord> ReadChecksums(BinaryReader reader)
    {
        int count = ReadCount(reader);
        var checksums = new List<ChecksumRecord>(count);
        for (int i = 0; i < count; i++) checksums.Add(new ChecksumRecord(reader.ReadInt64(), reader.ReadUInt64()));
        return checksums;
    }

    private static List<AiWhyEntry> ReadAiWhy(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > 32) throw new InvalidDataException("Invalid saved AI explanation count.");
        var entries = new List<AiWhyEntry>(count);
        for (int i = 0; i < count; i++)
        {
            long tick = reader.ReadInt64();
            byte civ = reader.ReadByte();
            string layer = ReadBoundedString(reader, 128);
            string kind = ReadBoundedString(reader, 128);
            long score = reader.ReadInt64();
            string reason = ReadBoundedString(reader, 4096);
            entries.Add(new AiWhyEntry(tick, civ, layer, kind, score, reason));
        }
        return entries;
    }

    private static string ReadBoundedString(BinaryReader reader, int maximumCharacters)
    {
        string value = reader.ReadString();
        if (value.Length > maximumCharacters) throw new InvalidDataException("Saved text field is too long.");
        return value;
    }

    private static int ReadCount(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > MaxLogEntries) throw new InvalidDataException("Invalid CivSim save log length.");
        return count;
    }
}
