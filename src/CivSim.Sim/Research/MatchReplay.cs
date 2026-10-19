using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.World;

namespace CivSim.Sim.Research;

/// <summary>
/// A replay of a match: the living economy driven by research (<see cref="MatchModel"/>). Like
/// <see cref="EconomyReplay"/> it wraps the unchanged v1 command replay with the configuration the model needs: the
/// archetypes, a fingerprint of the world regenerated from the seed, and the final checksum. The content pack is
/// supplied separately and must have the recorded SHA-256.
/// </summary>
public sealed class MatchReplay
{
    public const uint Magic = 0x52434D53; // "SMCR" little-endian, distinct from the toy and economy replays
    public const uint FormatVersion = 1;
    private readonly EconomyArchetype[] _archetypes;

    private MatchReplay(Replay replay, EconomyArchetype[] archetypes, ulong worldFingerprint, ulong finalChecksum)
    {
        Replay = replay;
        _archetypes = archetypes;
        WorldFingerprint = worldFingerprint;
        FinalChecksum = finalChecksum;
    }

    public Replay Replay { get; }
    public ReadOnlySpan<EconomyArchetype> Archetypes => _archetypes;
    public ulong WorldFingerprint { get; }
    public ulong FinalChecksum { get; }

    /// <summary>Captures a finished run: the commands the runner applied and the checksums it recorded.</summary>
    public static MatchReplay FromRun(ulong seed, ContentPack pack, ReadOnlySpan<EconomyArchetype> archetypes, SimRunner runner, ulong worldFingerprint)
    {
        var profiles = new EconomyArchetype[4];
        if (archetypes.Length != profiles.Length) throw new ArgumentException("A match replay needs four archetypes.", nameof(archetypes));
        archetypes.CopyTo(profiles);
        var replay = new Replay
        {
            MatchSeed = seed,
            ContentHash = (byte[])pack.Hash.Clone(),
            ChecksumInterval = runner.ChecksumInterval,
            TotalTicks = runner.Tick,
            Commands = new List<Command>(runner.AppliedCommands),
            Checksums = new List<ChecksumRecord>(runner.Checksums),
        };
        return new MatchReplay(replay, profiles, worldFingerprint, runner.CurrentChecksum);
    }

    public ReplayVerification Verify(ContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        if (!pack.Hash.AsSpan().SequenceEqual(Replay.ContentHash))
            return new ReplayVerification(false, -1, "Replay content hash differs from the loaded pack.");
        GeneratedWorld generated = WorldGenerator.Generate(Replay.MatchSeed);
        ulong fingerprint = WorldEconomyInput.FromGeneratedWorld(generated).Fingerprint;
        if (fingerprint != WorldFingerprint)
            return new ReplayVerification(false, -1, "Replay world fingerprint differs from the generated world.");
        MatchModel model = MatchModel.Create(Replay.MatchSeed, pack, generated, _archetypes);
        ReplayVerification result = ReplayVerifier.Verify(Replay, _ => model, pack.Hash);
        if (!result.Ok) return result;
        ulong actual = model.ComputeChecksum();
        return actual == FinalChecksum ? result : new ReplayVerification(false, Replay.TotalTicks,
            $"Final checksum mismatch at tick {Replay.TotalTicks}: expected {FinalChecksum:x16}, got {actual:x16}.");
    }

    public void Write(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(FormatVersion);
        writer.Write(WorldFingerprint);
        writer.Write(FinalChecksum);
        writer.Write(_archetypes.Length);
        foreach (EconomyArchetype archetype in _archetypes) writer.Write((byte)archetype);
        writer.Flush();
        Replay.Write(stream);
    }

    public static MatchReplay Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt32() != Magic || reader.ReadUInt32() != FormatVersion)
            throw new InvalidDataException("Unsupported match replay.");
        ulong worldFingerprint = reader.ReadUInt64();
        ulong finalChecksum = reader.ReadUInt64();
        if (reader.ReadInt32() != 4) throw new InvalidDataException("A match replay needs four archetypes.");
        var archetypes = new EconomyArchetype[4];
        for (int i = 0; i < archetypes.Length; i++)
        {
            archetypes[i] = (EconomyArchetype)reader.ReadByte();
            if (archetypes[i] > EconomyArchetype.Sage) throw new InvalidDataException("Unknown match replay archetype.");
        }
        Replay replay = Replay.Read(stream);
        if (replay.TotalTicks < 0 || replay.ChecksumInterval <= 0) throw new InvalidDataException("Invalid match replay tick count or interval.");
        return new MatchReplay(replay, archetypes, worldFingerprint, finalChecksum);
    }
}
