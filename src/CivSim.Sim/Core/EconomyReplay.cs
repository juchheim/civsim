using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Content;
using CivSim.Sim.Economy;
using CivSim.Sim.World;

namespace CivSim.Sim.Core;

/// <summary>
/// Match configuration around the unchanged v1 command replay. Worlds are regenerated from the match
/// seed using the default M2 settings; the fingerprint rejects a changed generator before simulation.
/// The content pack is supplied separately and must have the recorded SHA-256.
/// </summary>
public sealed class EconomyReplay
{
    public const uint Magic = 0x52455343; // "CSER" little-endian, distinct from the toy replay.
    public const uint FormatVersion = 1;
    private readonly EconomyArchetype[] _archetypes;

    private EconomyReplay(Replay replay, EconomyArchetype[] archetypes, ulong worldFingerprint, ulong finalChecksum)
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

    public static EconomyReplay Record(ulong seed, ContentPack pack, long totalTicks,
        IEnumerable<Command> commands, ReadOnlySpan<EconomyArchetype> archetypes = default,
        int checksumInterval = SimRunner.DefaultChecksumInterval)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(commands);
        if (totalTicks < 0) throw new ArgumentOutOfRangeException(nameof(totalTicks));
        if (checksumInterval <= 0) throw new ArgumentOutOfRangeException(nameof(checksumInterval));
        var profiles = new EconomyArchetype[4];
        if (!archetypes.IsEmpty)
        {
            if (archetypes.Length != profiles.Length)
                throw new ArgumentException("An economy replay requires four archetypes.", nameof(archetypes));
            archetypes.CopyTo(profiles);
        }
        ValidateArchetypes(profiles);
        var script = new List<Command>();
        foreach (Command command in commands)
        {
            if (command.Tick < 0 || command.Tick >= totalTicks)
                throw new ArgumentException("Replay commands must fall within the recorded run.", nameof(commands));
            script.Add(command);
        }
        WorldEconomyInput world = WorldEconomyInput.FromGeneratedWorld(WorldGenerator.Generate(seed));
        var model = new LivingEconomyModel(seed, EconomyContent.FromPack(pack), world, profiles);
        Replay replay = global::CivSim.Sim.Core.Replay.Record(model, seed, totalTicks, script, checksumInterval, pack.Hash);
        return new EconomyReplay(replay, profiles, world.Fingerprint, model.ComputeChecksum());
    }

    public ReplayVerification Verify(ContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        if (!pack.Hash.AsSpan().SequenceEqual(Replay.ContentHash))
            return new ReplayVerification(false, -1, "Replay content hash differs from the loaded pack.");
        WorldEconomyInput world = WorldEconomyInput.FromGeneratedWorld(WorldGenerator.Generate(Replay.MatchSeed));
        if (world.Fingerprint != WorldFingerprint)
            return new ReplayVerification(false, -1, "Replay world fingerprint differs from the generated world.");
        var model = new LivingEconomyModel(Replay.MatchSeed, EconomyContent.FromPack(pack), world, _archetypes);
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
        writer.Write(LivingEconomyModel.SnapshotVersion);
        writer.Write(WorldFingerprint);
        writer.Write(FinalChecksum);
        writer.Write(_archetypes.Length);
        foreach (EconomyArchetype archetype in _archetypes) writer.Write((byte)archetype);
        writer.Flush();
        Replay.Write(stream);
    }

    public static EconomyReplay Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt32() != Magic || reader.ReadUInt32() != FormatVersion)
            throw new InvalidDataException("Unsupported economy replay.");
        if (reader.ReadInt32() != LivingEconomyModel.SnapshotVersion)
            throw new InvalidDataException("Economy replay model version differs from this build.");
        ulong worldFingerprint = reader.ReadUInt64();
        ulong finalChecksum = reader.ReadUInt64();
        if (reader.ReadInt32() != 4) throw new InvalidDataException("An economy replay requires four archetypes.");
        var archetypes = new EconomyArchetype[4];
        for (int i = 0; i < archetypes.Length; i++) archetypes[i] = (EconomyArchetype)reader.ReadByte();
        ValidateArchetypes(archetypes);
        Replay replay = global::CivSim.Sim.Core.Replay.Read(stream);
        if (replay.TotalTicks < 0 || replay.ChecksumInterval <= 0)
            throw new InvalidDataException("Invalid economy replay tick count or checksum interval.");
        foreach (Command command in replay.Commands)
            if (command.Tick < 0 || command.Tick >= replay.TotalTicks)
                throw new InvalidDataException("Replay command is outside the recorded run.");
        return new EconomyReplay(replay, archetypes, worldFingerprint, finalChecksum);
    }

    private static void ValidateArchetypes(ReadOnlySpan<EconomyArchetype> archetypes)
    {
        foreach (EconomyArchetype archetype in archetypes)
            if (archetype > EconomyArchetype.Sage)
                throw new InvalidDataException("Unknown economy replay archetype.");
    }
}
