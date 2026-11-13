using System;
using System.IO;

namespace CivSim.Sim.Core;

/// <summary>A civilization-wide preference for autonomous decisions. It never replaces direct player orders.</summary>
public enum CivilizationDirection : byte
{
    Balanced = 0,
    Defensive = 1,
    Offensive = 2,
    Growth = 3,
    Prosperity = 4,
    Discovery = 5,
}

/// <summary>
/// Persistent civilization directions. The shared instance is read by research, construction, and politics, and is changed
/// only by replayable <see cref="CommandKind.SetCivilizationDirection"/> commands.
/// </summary>
public sealed class CivilizationGuidance
{
    private const uint TrailerMagic = 0x44495643; // "CIVD" little-endian
    private const int TrailerVersion = 1;
    private const ulong HashTag = 0x4349564449524543UL; // "CIVDIREC"
    private readonly CivilizationDirection[] _directions;

    public CivilizationGuidance(int civilizationCount)
    {
        if (civilizationCount < 1) throw new ArgumentOutOfRangeException(nameof(civilizationCount));
        _directions = new CivilizationDirection[civilizationCount];
    }

    public int CivilizationCount => _directions.Length;
    public CivilizationDirection DirectionOf(int civ) => (uint)civ < (uint)_directions.Length ? _directions[civ] : CivilizationDirection.Balanced;

    internal void Apply(ReadOnlySpan<Command> commands)
    {
        foreach (Command command in commands)
            if (command.Kind == CommandKind.SetCivilizationDirection && (uint)command.Civ < (uint)_directions.Length &&
                command.A is >= 0 and <= (int)CivilizationDirection.Discovery)
                _directions[command.Civ] = (CivilizationDirection)command.A;
    }

    public static string Name(CivilizationDirection direction) => direction switch
    {
        CivilizationDirection.Defensive => "Defensive",
        CivilizationDirection.Offensive => "Offensive",
        CivilizationDirection.Growth => "Growth",
        CivilizationDirection.Prosperity => "Prosperity",
        CivilizationDirection.Discovery => "Discovery",
        _ => "Balanced",
    };

    public static string Description(CivilizationDirection direction) => direction switch
    {
        CivilizationDirection.Defensive => "Favor defensive knowledge and fortifications. Avoid starting conflicts, keep trade open, and answer threats.",
        CivilizationDirection.Offensive => "Favor military knowledge and supporting works. Consider autonomous raids and wars when the existing safety checks say they are viable.",
        CivilizationDirection.Growth => "Favor food, housing, and settlement capacity. Found new villages when the Council can do so safely.",
        CivilizationDirection.Prosperity => "Favor production, commerce, and civic works. Keep relations peaceful and pursue gifts and barter more often.",
        CivilizationDirection.Discovery => "Favor exploration, routes, and knowledge. Seek new lands and peaceful contact.",
        _ => "Keep the current mix of research, construction, and autonomous decisions.",
    };

    public static string ResearchSummary(CivilizationDirection direction) => direction switch
    {
        CivilizationDirection.Defensive => "Defense, health, and cohesion",
        CivilizationDirection.Offensive => "War, weapons, and metalworking",
        CivilizationDirection.Growth => "Food, farming, and housing",
        CivilizationDirection.Prosperity => "Commerce, production, and civic life",
        CivilizationDirection.Discovery => "Wayfinding, travel, and learning",
        _ => "The Council's current priorities",
    };

    public static string BuildingSummary(CivilizationDirection direction) => direction switch
    {
        CivilizationDirection.Defensive => "Lookouts, barriers, and walls",
        CivilizationDirection.Offensive => "Military and campaign support",
        CivilizationDirection.Growth => "Fields, housing, and food works",
        CivilizationDirection.Prosperity => "Markets, workshops, and civic works",
        CivilizationDirection.Discovery => "Exploration and knowledge works",
        _ => "The existing build order",
    };

    public static string PoliticsSummary(CivilizationDirection direction) => direction switch
    {
        CivilizationDirection.Defensive => "Avoid starting conflicts; keep trade and defensive responses",
        CivilizationDirection.Offensive => "Consider raids and wars when safety checks allow",
        CivilizationDirection.Growth => "Favor founding; avoid discretionary conflict",
        CivilizationDirection.Prosperity => "Pursue gifts and barter more often; avoid discretionary conflict",
        CivilizationDirection.Discovery => "Favor expeditions and peaceful contact",
        _ => "The existing autonomous policy",
    };

    /// <summary>A small, deterministic bonus rank for optional buildings; zero leaves the legacy order intact.</summary>
    public static int BuildPreference(CivilizationDirection direction, string structureId) => direction switch
    {
        CivilizationDirection.Defensive when structureId is "lookout_post" or "thorn_barrier" or "signal_post" or "hillfort" or "palisade" or
            "stone_walls" or "gatehouse" or "frontier_wall_segment" or "castle" => 2,
        CivilizationDirection.Defensive when structureId is "academy" or "school" or "temple" => 1,
        CivilizationDirection.Offensive when structureId is "galley" or "hillfort" or "palisade" => 2,
        CivilizationDirection.Offensive when structureId is "road" or "crane" or "gatehouse" or "frontier_wall_segment" => 1,
        CivilizationDirection.Growth when structureId is "field" or "orchard" or "pasture" or "weir" or "pit_house" or "wattle_daub_house" or
            "mudbrick_house" or "longhouse" or "bakery" or "terrace" or "aqueduct" or "windlass_well" or "water_mill" => 2,
        CivilizationDirection.Growth when structureId is "granary" or "market" => 1,
        CivilizationDirection.Prosperity when structureId is "market" or "caravanserai" or "school" or "hall" or "kiln" or
            "potters_workshop" or "quern" or "press" or "water_mill" or "academy" => 2,
        CivilizationDirection.Prosperity when structureId is "bakery" or "temple" or "great_library" => 1,
        CivilizationDirection.Discovery when structureId is "marker" or "lookout_post" or "signal_post" or "academy" or "great_library" or
            "great_lighthouse" => 2,
        CivilizationDirection.Discovery when structureId is "road" or "bridge" or "harbor" or "school" or "totem" => 1,
        _ => 0,
    };

    internal void Hash(ref StateHasher hasher)
    {
        if (!HasNonBalanced) return;
        hasher.Add(HashTag);
        hasher.Add(_directions.Length);
        for (int civ = 0; civ < _directions.Length; civ++)
            if (_directions[civ] != CivilizationDirection.Balanced)
            {
                hasher.Add(civ);
                hasher.Add((int)_directions[civ]);
            }
    }

    internal void WriteOptionalTrailer(BinaryWriter writer)
    {
        if (!HasNonBalanced) return;
        writer.Write(TrailerMagic);
        writer.Write(TrailerVersion);
        writer.Write(_directions.Length);
        foreach (CivilizationDirection direction in _directions) writer.Write((byte)direction);
    }

    internal void ReadOptionalTrailer(BinaryReader reader)
    {
        Array.Clear(_directions);
        Stream stream = reader.BaseStream;
        if (!stream.CanSeek || stream.Position >= stream.Length) return;
        if (reader.ReadUInt32() != TrailerMagic || reader.ReadInt32() != TrailerVersion || reader.ReadInt32() != _directions.Length)
            throw new InvalidDataException("The snapshot has an unsupported civilization direction trailer.");
        var restored = new CivilizationDirection[_directions.Length];
        for (int civ = 0; civ < restored.Length; civ++)
        {
            byte value = reader.ReadByte();
            if (value > (byte)CivilizationDirection.Discovery)
                throw new InvalidDataException("The snapshot contains an invalid civilization direction.");
            restored[civ] = (CivilizationDirection)value;
        }
        Array.Copy(restored, _directions, restored.Length);
    }

    private bool HasNonBalanced
    {
        get
        {
            foreach (CivilizationDirection direction in _directions)
                if (direction != CivilizationDirection.Balanced) return true;
            return false;
        }
    }
}
