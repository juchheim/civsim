using System.IO;
using CivSim.Sim.Research;

namespace CivSim.Game;

/// <summary>Mode byte shared by quick saves and the optional game replay footer.</summary>
internal static class GameSaveMode
{
    /// <summary>The chapters that name themselves in labels and details (Bronze and Iron stay unlabelled).</summary>
    private static bool Labelled(ChapterMode mode) => mode is ChapterMode.Fief or ChapterMode.Discovery;

    public static bool ShowsRivalCountdown(ChapterMode mode)
        => ChapterRules.For(mode).HasRivalCountdown;

    public static string ChapterLabel(ChapterMode mode)
        => mode switch
        {
            ChapterMode.Bronze => "Bronze mode",
            ChapterMode.Iron => "Iron mode",
            ChapterMode.Fief => "Fief mode",
            ChapterMode.Discovery => "Discovery mode",
            _ => throw new InvalidDataException("Invalid saved chapter mode."),
        };

    public static string AgeLabel(ChapterMode mode, string ageName)
    {
        string age = ageName.ToUpperInvariant() + " AGE";
        return Labelled(mode) ? $"{ChapterLabel(mode).ToUpperInvariant()} · {age}" : age;
    }

    public static string LoadDetail(ChapterMode mode, int year)
        => Labelled(mode) ? $"{ChapterLabel(mode)} · Year {year}." : $"Year {year}.";

    public static string ReplayDetail(ChapterMode mode, string duration)
        => Labelled(mode) ? $"{ChapterLabel(mode)} · {duration}" : duration;

    public static byte Encode(ChapterMode mode)
        => mode switch
        {
            ChapterMode.Iron => (byte)ChapterMode.Iron,
            ChapterMode.Fief => (byte)ChapterMode.Fief,
            ChapterMode.Discovery => (byte)ChapterMode.Discovery,
            _ => throw new InvalidDataException("Invalid saved chapter mode."),
        };

    public static ChapterMode Decode(byte mode)
        => mode switch
        {
            (byte)ChapterMode.Iron => ChapterMode.Iron,
            (byte)ChapterMode.Fief => ChapterMode.Fief,
            (byte)ChapterMode.Discovery => ChapterMode.Discovery,
            _ => throw new InvalidDataException("Invalid saved chapter mode."),
        };

    public static void WriteTag(BinaryWriter writer, ChapterMode mode)
        => writer.Write(Encode(mode));

    public static ChapterMode ReadTag(BinaryReader reader)
        => Decode(reader.ReadByte());

    public static void WriteOptionalTag(BinaryWriter writer, ChapterMode mode)
    {
        if (mode != ChapterMode.Bronze) WriteTag(writer, mode);
    }

    public static ChapterMode ReadOptionalTag(BinaryReader reader)
    {
        Stream stream = reader.BaseStream;
        if (!stream.CanSeek) throw new InvalidDataException("Chapter mode footer requires a seekable stream.");
        return stream.Position < stream.Length ? ReadTag(reader) : ChapterMode.Bronze;
    }
}
