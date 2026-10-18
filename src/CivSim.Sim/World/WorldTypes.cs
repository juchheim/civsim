using System;
using CivSim.Sim.Core;

namespace CivSim.Sim.World;

public enum ClimateBand : byte { Cold, Temperate, Warm, Arid, Tropical }

public enum Biome : byte
{
    Ocean, Coast, Ice, Tundra, Taiga, TemperateForest, OpenWoodland, GrasslandSteppe,
    Savanna, TropicalForest, Wetland, Desert, Mountain, Peak
}

[System.Flags]
public enum TileFlags : ushort
{
    None = 0, River = 1, Lake = 2, Spring = 4, SalmonRun = 8, WildGrain = 16,
    CoastAdjacent = 32
}

public enum DepositKind : byte
{
    None, Stone, Flint, Clay, Ochre, NativeCopper, CopperOre, ArsenicalCopper, Tin,
    Gold, Silver, Iron, Salt, Coal, Oil, Saltpeter, Sulfur, Bauxite, RareEarths,
    Lithium, SilkMoths, Sugarcane
}

public enum HerdSpecies : byte
{
    Deer, Aurochs, WildBoar, Bison, Horse, Reindeer, WildGoatSheep,
    GazelleAntelope, CamelLlama, SmallGame
}

public enum PredatorSpecies : byte { Wolves, BigCat, Bear }

public readonly record struct Herd(int Tile, HerdSpecies Species, ushort Size, byte GrowthPercent);
public readonly record struct PredatorPack(int Tile, PredatorSpecies Species, byte Count);
public readonly record struct WorldStart(byte Civilization, int Tile, int StartScore = 0);

/// <summary>One immutable-by-convention tile record in the generated, engine-independent world.</summary>
public struct WorldTile
{
    public byte Elevation { get; internal set; }
    public ClimateBand Climate { get; internal set; }
    public Biome Biome { get; internal set; }
    public TileFlags Flags { get; internal set; }
    public byte Rainfall { get; internal set; }
    public byte SoilFertility { get; internal set; }
    public byte Continent { get; internal set; }
    public DepositKind Deposit { get; internal set; }
    public byte DepositAmount { get; internal set; }
    public byte DepositDepth { get; internal set; }

    public bool IsLand => Biome is not (Biome.Ocean or Biome.Coast);

    /// <summary>Habitable-land weight from 05 §11.3, represented in hundredths.</summary>
    public byte HabitableWeight => Biome switch
    {
        Biome.Ice or Biome.Peak or Biome.Ocean or Biome.Coast => 0,
        Biome.Tundra or Biome.Desert => 50,
        _ => 100
    };

    public byte ForageScore => Biome switch
    {
        Biome.TemperateForest => 100,
        Biome.OpenWoodland => 110,
        Biome.GrasslandSteppe => 80,
        Biome.Savanna => 115,
        Biome.TropicalForest => 90,
        Biome.Wetland => 115,
        Biome.Taiga => 70,
        Biome.Tundra => 45,
        Biome.Desert => 35,
        Biome.Mountain => 40,
        _ => 0
    };
}

/// <summary>World generation options. The MVP gate is the four-civilization standard map.</summary>
public readonly record struct WorldGenSettings(int Width, int Height, int PlayerCount)
{
    public static WorldGenSettings Standard => new(160, 100, 4);

    public void Validate()
    {
        if (PlayerCount != 4)
            throw new ArgumentOutOfRangeException(nameof(PlayerCount), "M2 implements the four-player MVP world.");
        if (Width < 80 || Height < 60 || (Width & 1) != 0 || (Height & 1) != 0)
            throw new ArgumentOutOfRangeException(nameof(Width), "MVP maps must be at least 80×60 with even dimensions.");
    }
}

/// <summary>Acceptance measurements emitted with each generated world.</summary>
public sealed class WorldGenerationReport
{
    internal WorldGenerationReport(int landTiles, int riverTiles, int lakeTiles, int springs,
        int herdCount, int predatorCount, int guaranteeRepairs, int fairnessAttempts,
        int[] e7SharesBasisPoints, int[] e7SettlementCounts, int blockedSeasons,
        int firstContactSeason, int width, int height)
    {
        LandTiles = landTiles;
        RiverTiles = riverTiles;
        LakeTiles = lakeTiles;
        SpringCount = springs;
        HerdCount = herdCount;
        PredatorCount = predatorCount;
        GuaranteeRepairs = guaranteeRepairs;
        FairnessAttempts = fairnessAttempts;
        E7SharesBasisPoints = e7SharesBasisPoints;
        E7SettlementCounts = e7SettlementCounts;
        BlockedSeasons = blockedSeasons;
        FirstContactSeason = firstContactSeason;
        Width = width;
        Height = height;
    }

    public int LandTiles { get; }
    public int RiverTiles { get; }
    public int LakeTiles { get; }
    public int SpringCount { get; }
    public int HerdCount { get; }
    public int PredatorCount { get; }
    public int GuaranteeRepairs { get; }
    public int FairnessAttempts { get; }
    public ReadOnlyMemory<int> E7SharesBasisPoints { get; }
    public ReadOnlyMemory<int> E7SettlementCounts { get; }
    public int BlockedSeasons { get; }
    public int FirstContactSeason { get; }
    public int Width { get; }
    public int Height { get; }
    public int LandShareBasisPoints => (int)((long)LandTiles * 10_000 / Width / Height);
}

/// <summary>A complete deterministic world snapshot produced by <see cref="WorldGenerator"/>.</summary>
public sealed class GeneratedWorld
{
    private readonly WorldTile[] _tiles;
    private readonly WorldStart[] _starts;
    private readonly Herd[] _herds;
    private readonly PredatorPack[] _predators;

    internal GeneratedWorld(ulong seed, WorldGenSettings settings, WorldTile[] tiles,
        WorldStart[] starts, Herd[] herds, PredatorPack[] predators, WorldGenerationReport report)
    {
        Seed = seed;
        Settings = settings;
        _tiles = tiles;
        _starts = starts;
        _herds = herds;
        _predators = predators;
        Report = report;
    }

    public ulong Seed { get; }
    public WorldGenSettings Settings { get; }
    public int Width => Settings.Width;
    public int Height => Settings.Height;
    public int TileCount => _tiles.Length;
    public WorldGenerationReport Report { get; }
    public ReadOnlySpan<WorldTile> Tiles => _tiles;
    public ReadOnlySpan<WorldStart> Starts => _starts;
    public ReadOnlySpan<Herd> Herds => _herds;
    public ReadOnlySpan<PredatorPack> Predators => _predators;
    public WorldTile TileAt(int index) => _tiles[index];
    public WorldTile TileAt(int x, int y) => _tiles[HexGrid.Index(x, y, Width, Height)];

    /// <summary>Stable ordered fingerprint of all gameplay-relevant generated state.</summary>
    public ulong Fingerprint()
    {
        var h = new StateHasher();
        h.Add(Seed);
        h.Add(Width);
        h.Add(Height);
        h.Add(Settings.PlayerCount);
        h.Add(_tiles.Length);
        foreach (WorldTile tile in _tiles)
        {
            h.Add(tile.Elevation);
            h.Add((byte)tile.Climate);
            h.Add((byte)tile.Biome);
            h.Add((ushort)tile.Flags);
            h.Add(tile.Rainfall);
            h.Add(tile.SoilFertility);
            h.Add(tile.Continent);
            h.Add((byte)tile.Deposit);
            h.Add(tile.DepositAmount);
            h.Add(tile.DepositDepth);
        }
        h.Add(_starts.Length);
        foreach (WorldStart start in _starts) { h.Add(start.Civilization); h.Add(start.Tile); h.Add(start.StartScore); }
        h.Add(_herds.Length);
        foreach (Herd herd in _herds)
        {
            h.Add(herd.Tile); h.Add((byte)herd.Species); h.Add(herd.Size); h.Add(herd.GrowthPercent);
        }
        h.Add(_predators.Length);
        foreach (PredatorPack predator in _predators)
        {
            h.Add(predator.Tile); h.Add((byte)predator.Species); h.Add(predator.Count);
        }
        return h.Finish();
    }
}
