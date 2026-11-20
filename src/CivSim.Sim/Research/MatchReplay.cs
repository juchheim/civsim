using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Ai;
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
    /// <summary>The Bronze format, unchanged since before M11: no chapter mode on disk.</summary>
    public const uint FormatVersion = 1;
    /// <summary>The format of a replay that records its chapter mode. Written only for non-Bronze chapters, so Bronze files keep their bytes.</summary>
    public const uint ModeFormatVersion = 2;
    /// <summary>Fief replay format carrying the AI controls used by the headless match.</summary>
    public const uint FiefControlFormatVersion = 3;
    private readonly EconomyArchetype[] _archetypes;
    private readonly CivControl[]? _controls;

    private MatchReplay(Replay replay, EconomyArchetype[] archetypes, ulong worldFingerprint, ulong finalChecksum, ChapterRules rules,
        CivControl[]? controls = null)
    {
        Rules = rules;
        Replay = replay;
        _archetypes = archetypes;
        WorldFingerprint = worldFingerprint;
        FinalChecksum = finalChecksum;
        _controls = controls;
    }

    public Replay Replay { get; }
    /// <summary>The chapter the recorded match played; Bronze for every replay written before M11.</summary>
    public ChapterRules Rules { get; }
    public ReadOnlySpan<EconomyArchetype> Archetypes => _archetypes;
    public ulong WorldFingerprint { get; }
    public ulong FinalChecksum { get; }
    public ReadOnlySpan<CivControl> Controls => _controls;

    /// <summary>Captures a finished run: the commands the runner applied and the checksums it recorded.</summary>
    public static MatchReplay FromRun(ulong seed, ContentPack pack, ReadOnlySpan<EconomyArchetype> archetypes, SimRunner runner, ulong worldFingerprint,
        ChapterRules? rules = null, CivControl[]? controls = null)
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
        ChapterRules chapterRules = rules ?? ChapterRules.Bronze;
        CivControl[]? recordedControls = null;
        if (controls is not null)
        {
            if (!chapterRules.IsFief || controls.Length != 4 || Array.Exists(controls, static c => c.Kind != ControlKind.Ai))
                throw new ArgumentException("Recorded match controls are currently supported for four-AI Fief matches only.", nameof(controls));
            recordedControls = (CivControl[])controls.Clone();
        }
        return new MatchReplay(replay, profiles, worldFingerprint, runner.CurrentChecksum, chapterRules, recordedControls);
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
        MatchModel model = MatchModel.Create(Replay.MatchSeed, pack, generated, _archetypes,
            _controls is null ? ReadOnlySpan<CivControl>.Empty : _controls, rules: Rules);
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
        uint version = Rules.IsFief && _controls is not null ? FiefControlFormatVersion : Rules.HasMode ? ModeFormatVersion : FormatVersion;
        writer.Write(version);
        if (Rules.HasMode) writer.Write((byte)Rules.Mode);
        if (version == FiefControlFormatVersion)
            foreach (CivControl control in _controls!)
            {
                writer.Write((byte)control.Spec.Level);
                writer.Write((byte)control.Spec.Archetype);
            }
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
        if (reader.ReadUInt32() != Magic) throw new InvalidDataException("Unsupported match replay.");
        uint version = reader.ReadUInt32();
        if (version != FormatVersion && version != ModeFormatVersion && version != FiefControlFormatVersion) throw new InvalidDataException("Unsupported match replay.");
        ChapterRules rules = ChapterRules.Bronze;
        if (version is ModeFormatVersion or FiefControlFormatVersion)
        {
            byte mode = reader.ReadByte();
            if (mode != (byte)ChapterMode.Iron && mode != (byte)ChapterMode.Fief && mode != (byte)ChapterMode.Discovery) throw new InvalidDataException("Unknown match replay chapter mode.");
            rules = ChapterRules.For((ChapterMode)mode);
        }
        CivControl[]? controls = null;
        if (version == FiefControlFormatVersion)
        {
            if (!rules.IsFief) throw new InvalidDataException("Fief control data requires a Fief match replay.");
            controls = new CivControl[4];
            for (int i = 0; i < controls.Length; i++)
            {
                byte level = reader.ReadByte();
                byte archetype = reader.ReadByte();
                if (level > (byte)Difficulty.Legend || archetype > (byte)EconomyArchetype.Sage)
                    throw new InvalidDataException("Unknown Fief match replay control.");
                controls[i] = CivControl.Ai((Difficulty)level, (EconomyArchetype)archetype);
            }
        }
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
        return new MatchReplay(replay, archetypes, worldFingerprint, finalChecksum, rules, controls);
    }
}
