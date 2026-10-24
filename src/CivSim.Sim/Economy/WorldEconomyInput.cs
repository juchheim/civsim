using System;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// The economy-facing projection of an M2 world. World generation owns geography and resource placement;
/// this immutable input exposes the seasonal stocks and growth rates that settlements can use.
/// </summary>
public readonly record struct WorldEconomyTile(
    int BiomeId,
    int ClimateId,
    Fix64 SoilFertility,
    Fix64 InitialForage,
    Fix64 SpringForage,
    Fix64 SummerForage,
    Fix64 AutumnForage,
    Fix64 WinterForage,
    Fix64 InitialHerdFood,
    Fix64 HerdFoodGrowthPerYear,
    Fix64 InitialWood,
    Fix64 WoodGrowthPerYear,
    Fix64 InitialFish,
    Fix64 FishGrowthPerYear,
    Fix64 FieldYieldPerYear,
    bool FreshWater,
    bool Clay,
    bool Stone,
    bool Flint,
    Fix64 InitialStoneStock,
    Fix64 InitialClayStock,
    Fix64 InitialFlintStock,
    bool SalmonRun = false,
    bool River = false,
    bool Lake = false,
    bool Coast = false,
    bool WildGrainStand = false,
    byte Elevation = 0)
{
    /// <summary>The extra two ordinary seasons of fish growth delivered by the Autumn salmon run.</summary>
    public Fix64 SalmonAutumnGrowth => SalmonRun ? FishGrowthPerYear / 2 : Fix64.Zero;

    /// <summary>Ordinary annual growth plus the one-season salmon bonus, when present.</summary>
    public Fix64 TotalFishGrowthPerYear => FishGrowthPerYear + SalmonAutumnGrowth;
}

/// <summary>One generated wild herd, retained separately from the tile-level economy aggregates.</summary>
public readonly record struct WorldHerd(
    int Id,
    HerdSpecies Species,
    int Tile,
    int InitialAnimalCount,
    byte AnnualGrowthPercent);

/// <summary>
/// A mineral deposit on a tile as world generation placed it (docs/MVP/12 §8, 22 §5.1): the kind, the quantity tier and the
/// depth (1 surface, 2 needs Shaft Mining). The economy maps kind, tier and depth to litres of ore (docs/MVP/22 §5.1 "As built").
/// </summary>
public readonly record struct WorldDeposit(DepositKind Kind, byte Tier, byte Depth);

/// <summary>Stable, ordered world data passed from M2 into the M3 simulation.</summary>
public sealed class WorldEconomyInput
{
    private readonly WorldEconomyTile[] _tiles;
    private readonly int[] _startingTiles;
    private readonly WorldHerd[] _herds;
    private readonly WorldDeposit[] _deposits;

    public WorldEconomyInput(int width, int height, ReadOnlySpan<WorldEconomyTile> tiles,
        ReadOnlySpan<int> startingTileByCiv)
        : this(width, height, tiles, startingTileByCiv, ReadOnlySpan<WorldHerd>.Empty)
    {
    }

    public WorldEconomyInput(int width, int height, ReadOnlySpan<WorldEconomyTile> tiles,
        ReadOnlySpan<int> startingTileByCiv, ReadOnlySpan<WorldHerd> herds)
        : this(width, height, tiles, startingTileByCiv, herds, ReadOnlySpan<WorldDeposit>.Empty)
    {
    }

    /// <param name="deposits">One entry per tile, or empty for a world without minerals (E0–E2 fixtures).</param>
    public WorldEconomyInput(int width, int height, ReadOnlySpan<WorldEconomyTile> tiles,
        ReadOnlySpan<int> startingTileByCiv, ReadOnlySpan<WorldHerd> herds, ReadOnlySpan<WorldDeposit> deposits)
    {
        if (width <= 0 || height <= 0 || (long)width * height != tiles.Length)
            throw new ArgumentOutOfRangeException(nameof(width), "Width and height must match the tile array.");
        if (tiles.IsEmpty) throw new ArgumentException("An economy world needs at least one tile.", nameof(tiles));
        if (startingTileByCiv.IsEmpty) throw new ArgumentException("An economy world needs at least one civilization start.", nameof(startingTileByCiv));

        _tiles = tiles.ToArray();
        _startingTiles = startingTileByCiv.ToArray();
        _herds = herds.ToArray();
        if (!deposits.IsEmpty && deposits.Length != tiles.Length)
            throw new ArgumentException("Deposits must be given for every tile or not at all.", nameof(deposits));
        _deposits = deposits.IsEmpty ? new WorldDeposit[tiles.Length] : deposits.ToArray();
        Width = width;
        Height = height;
        for (int i = 0; i < _startingTiles.Length; i++)
            if (_startingTiles[i] < 0 || _startingTiles[i] >= _tiles.Length)
                throw new ArgumentOutOfRangeException(nameof(startingTileByCiv), $"Start {_startingTiles[i]} is outside the tile array.");
        for (int i = 0; i < _tiles.Length; i++)
        {
            WorldEconomyTile tile = _tiles[i];
            if (tile.BiomeId < 0 || tile.BiomeId >= Enum.GetValues<Biome>().Length ||
                tile.ClimateId < 0 || tile.ClimateId >= Enum.GetValues<ClimateBand>().Length ||
                tile.SoilFertility < Fix64.Zero || tile.SoilFertility > Fix64.One ||
                tile.SpringForage < Fix64.Zero || tile.SummerForage < Fix64.Zero || tile.AutumnForage < Fix64.Zero ||
                tile.WinterForage < Fix64.Zero || tile.InitialForage < Fix64.Zero || tile.InitialHerdFood < Fix64.Zero ||
                tile.HerdFoodGrowthPerYear < Fix64.Zero || tile.InitialWood < Fix64.Zero || tile.WoodGrowthPerYear < Fix64.Zero ||
                tile.InitialFish < Fix64.Zero || tile.FishGrowthPerYear < Fix64.Zero || tile.FieldYieldPerYear < Fix64.Zero ||
                tile.InitialStoneStock < Fix64.Zero || tile.InitialClayStock < Fix64.Zero || tile.InitialFlintStock < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(tiles), $"Tile {i} has a negative stock or yield.");
            if (tile.SalmonRun && !tile.River || tile.River && tile.Lake ||
                (tile.River || tile.Lake) && !tile.FreshWater ||
                tile.Coast && tile.FreshWater)
                throw new ArgumentException($"Tile {i} has inconsistent water flags.", nameof(tiles));
        }
        for (int i = 0; i < _herds.Length; i++)
        {
            WorldHerd herd = _herds[i];
            if (herd.Id < 0 || herd.Tile < 0 || herd.Tile >= _tiles.Length ||
                !Enum.IsDefined(herd.Species) || herd.InitialAnimalCount <= 0 || herd.AnnualGrowthPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(herds), $"Herd {i} has an invalid id, species, tile, count, or growth percentage.");
            for (int previous = 0; previous < i; previous++)
                if (_herds[previous].Id == herd.Id)
                    throw new ArgumentException($"Herd id {herd.Id} is duplicated.", nameof(herds));
        }
        Fingerprint = ComputeFingerprint();
        DepositFingerprint = ComputeDepositFingerprint();
    }

    public int TileCount => _tiles.Length;
    public int CivilizationCount => _startingTiles.Length;
    public int Width { get; }
    public int Height { get; }
    public ulong Fingerprint { get; }
    /// <summary>
    /// Fingerprint of the mineral deposits, kept apart from <see cref="Fingerprint"/> so that matches which never reach the
    /// Bronze Age (where deposits first matter) keep the checksums they had before deposits reached the economy.
    /// </summary>
    public ulong DepositFingerprint { get; }
    /// <summary>The deposit on a tile; <see cref="DepositKind.None"/> where there is none.</summary>
    public WorldDeposit Deposit(int tile) => _deposits[tile];
    /// <summary>The ordered per-herd records retained from world generation.</summary>
    public ReadOnlySpan<WorldHerd> Herds => _herds;
    public WorldEconomyTile Tile(int index) => _tiles[index];
    public int StartingTile(int civ) => _startingTiles[civ];
    public WorldHerd Herd(int index) => _herds[index];

    /// <summary>Projects the deterministic M2 world and its fauna into the M3 economy input.</summary>
    public static WorldEconomyInput FromGeneratedWorld(GeneratedWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        WorldEconomyTile[] tiles = new WorldEconomyTile[world.TileCount];
        WorldHerd[] herds = new WorldHerd[world.Herds.Length];
        var deposits = new WorldDeposit[world.TileCount];
        Fix64[] herdGrowthByTile = new Fix64[world.TileCount];
        Fix64[] herdInitialByTile = new Fix64[world.TileCount];
        for (int i = 0; i < world.Herds.Length; i++)
        {
            Herd herd = world.Herds[i];
            if (herd.Tile < 0 || herd.Tile >= world.TileCount || herd.Size == 0 ||
                !Enum.IsDefined(herd.Species) || herd.GrowthPercent > 100)
                throw new ArgumentException($"Generated herd {i} has invalid data.", nameof(world));
            herds[i] = new WorldHerd(i, herd.Species, herd.Tile, herd.Size, herd.GrowthPercent);
            Fix64 meatPerAnimal = MeatPerAnimal(herd.Species);
            Fix64 total = Fix64.FromInt(herd.Size) * meatPerAnimal;
            herdInitialByTile[herd.Tile] += total;
            herdGrowthByTile[herd.Tile] += total * Fix64.FromRatio(herd.GrowthPercent, 100);
        }
        bool[] coastalWater = CoastalWaterWithinTwoTiles(world);
        for (int i = 0; i < tiles.Length; i++)
        {
            WorldTile source = world.TileAt(i);
            deposits[i] = new WorldDeposit(source.Deposit, source.DepositAmount, source.DepositDepth);
            Fix64 forageMultiplier = ForageMultiplier(source.Biome);
            (Fix64 spring, Fix64 summer, Fix64 autumn, Fix64 winter) = SeasonalProfile(source.Climate);
            Fix64 springYield = spring * forageMultiplier;
            Fix64 summerYield = summer * forageMultiplier;
            Fix64 autumnYield = autumn * forageMultiplier;
            Fix64 winterYield = winter * forageMultiplier;
            Fix64 yearlyPlantYield = (springYield + summerYield + autumnYield + winterYield) * Fix64.FromRatio(2, 5);
            Fix64 soil = Fix64.FromRatio(source.SoilFertility, 100);
            Fix64 herdGrowth = herdGrowthByTile[i];
            Fix64 herdInitial = herdInitialByTile[i];
            bool river = (source.Flags & TileFlags.River) != 0;
            bool lake = (source.Flags & TileFlags.Lake) != 0;
            bool coast = coastalWater[i];
            bool salmonRun = (source.Flags & TileFlags.SalmonRun) != 0;
            Fix64 wood = WoodStock(source.Biome);
            bool fresh = river || lake || (source.Flags & TileFlags.Spring) != 0;
            Fix64 fishPerSeason = river ? Fix64.Parse("1.2") : lake ? Fix64.One : coast ? Fix64.Parse("1.5") : Fix64.Zero;
            tiles[i] = new WorldEconomyTile(
                (int)source.Biome, (int)source.Climate, soil, yearlyPlantYield * 2, springYield * Fix64.FromRatio(2, 5),
                summerYield * Fix64.FromRatio(2, 5), autumnYield * Fix64.FromRatio(2, 5),
                winterYield * Fix64.FromRatio(2, 5), herdInitial, herdGrowth,
                wood, Fix64.FromInt(2), fishPerSeason * 2, fishPerSeason * 4, Fix64.FromInt(4) * soil,
                fresh, source.Deposit == DepositKind.Clay,
                source.Deposit is DepositKind.Stone or DepositKind.Flint,
                source.Deposit == DepositKind.Flint,
                source.IsLand ? Fix64.FromInt(2) + (source.Deposit == DepositKind.Stone ? Fix64.FromInt(source.DepositAmount) : Fix64.Zero) : Fix64.Zero,
                source.Deposit == DepositKind.Clay ? Fix64.FromInt(source.DepositAmount) : Fix64.Zero,
                source.Deposit == DepositKind.Flint ? Fix64.FromInt(source.DepositAmount) : Fix64.Zero,
                salmonRun, river, lake, coast, (source.Flags & TileFlags.WildGrain) != 0, source.Elevation);
        }

        int[] starts = new int[world.Starts.Length];
        for (int i = 0; i < world.Starts.Length; i++) starts[world.Starts[i].Civilization] = world.Starts[i].Tile;
        return new WorldEconomyInput(world.Width, world.Height, tiles, starts, herds, deposits);
    }

    private static bool[] CoastalWaterWithinTwoTiles(GeneratedWorld world)
    {
        var coastal = new bool[world.TileCount];
        Span<int> firstRing = stackalloc int[6];
        Span<int> secondRing = stackalloc int[6];
        for (int tile = 0; tile < world.TileCount; tile++)
        {
            WorldTile water = world.TileAt(tile);
            if (water.IsLand || (water.Flags & TileFlags.Lake) != 0 ||
                water.Biome is not (Biome.Ocean or Biome.Coast)) continue;
            if (water.Biome == Biome.Coast)
            {
                coastal[tile] = true;
                continue;
            }

            int firstCount = HexGrid.Neighbors(tile, world.Width, world.Height, firstRing);
            for (int first = 0; first < firstCount && !coastal[tile]; first++)
            {
                int neighbor = firstRing[first];
                if (world.TileAt(neighbor).IsLand)
                {
                    coastal[tile] = true;
                    break;
                }
                int secondCount = HexGrid.Neighbors(neighbor, world.Width, world.Height, secondRing);
                for (int second = 0; second < secondCount; second++)
                    if (world.TileAt(secondRing[second]).IsLand)
                    {
                        coastal[tile] = true;
                        break;
                    }
            }
        }
        return coastal;
    }

    private ulong ComputeFingerprint()
    {
        var hash = new StateHasher();
        hash.Add(_tiles.Length);
        foreach (WorldEconomyTile tile in _tiles)
        {
            hash.Add(tile.BiomeId); hash.Add(tile.ClimateId); hash.Add(tile.SoilFertility);
            hash.Add(tile.InitialForage); hash.Add(tile.SpringForage); hash.Add(tile.SummerForage);
            hash.Add(tile.AutumnForage); hash.Add(tile.WinterForage); hash.Add(tile.InitialHerdFood);
            hash.Add(tile.HerdFoodGrowthPerYear); hash.Add(tile.InitialWood); hash.Add(tile.WoodGrowthPerYear);
            hash.Add(tile.InitialFish); hash.Add(tile.FishGrowthPerYear); hash.Add(tile.FieldYieldPerYear);
            hash.Add(tile.FreshWater); hash.Add(tile.Clay); hash.Add(tile.Stone); hash.Add(tile.Flint);
            hash.Add(tile.InitialStoneStock); hash.Add(tile.InitialClayStock); hash.Add(tile.InitialFlintStock);
            hash.Add(tile.SalmonRun);
            hash.Add(tile.River); hash.Add(tile.Lake); hash.Add(tile.Coast); hash.Add(tile.WildGrainStand);
        }
        hash.Add(_startingTiles.Length);
        foreach (int start in _startingTiles) hash.Add(start);
        hash.Add(_herds.Length);
        foreach (WorldHerd herd in _herds)
        {
            hash.Add(herd.Id);
            hash.Add((byte)herd.Species);
            hash.Add(herd.Tile);
            hash.Add(herd.InitialAnimalCount);
            hash.Add(herd.AnnualGrowthPercent);
        }
        hash.Add(Width); hash.Add(Height);
        return hash.Finish();
    }

    private ulong ComputeDepositFingerprint()
    {
        var hash = new StateHasher();
        hash.Add(_deposits.Length);
        for (int i = 0; i < _deposits.Length; i++)
        {
            WorldDeposit d = _deposits[i];
            if (d.Kind == DepositKind.None) continue;
            hash.Add(i); hash.Add((byte)d.Kind); hash.Add(d.Tier); hash.Add(d.Depth);
        }
        return hash.Finish();
    }

    /// <summary>Forage multiplier of a biome (for a forest tile cleared to open woodland or grassland, docs/MVP/23 §7.4).</summary>
    internal static Fix64 ForageMultiplierOf(Biome biome) => ForageMultiplier(biome);

    private static Fix64 ForageMultiplier(Biome biome) => biome switch
    {
        Biome.TemperateForest => Fix64.One,
        Biome.OpenWoodland => Fix64.Parse("1.1"),
        Biome.GrasslandSteppe => Fix64.Parse("0.8"),
        Biome.Savanna => Fix64.Parse("1.15"),
        Biome.TropicalForest => Fix64.Parse("0.9"),
        Biome.Wetland => Fix64.Parse("1.15"),
        Biome.Taiga => Fix64.Parse("0.7"),
        Biome.Tundra => Fix64.Parse("0.45"),
        Biome.Desert => Fix64.Parse("0.35"),
        Biome.Mountain => Fix64.Parse("0.4"),
        _ => Fix64.Zero,
    };

    private static (Fix64 Spring, Fix64 Summer, Fix64 Autumn, Fix64 Winter) SeasonalProfile(ClimateBand climate) => climate switch
    {
        ClimateBand.Cold => (Fix64.Parse("0.4"), Fix64.Parse("1.1"), Fix64.Parse("0.8"), Fix64.Parse("0.05")),
        ClimateBand.Warm => (Fix64.Parse("0.9"), Fix64.Parse("0.8"), Fix64.One, Fix64.Half),
        ClimateBand.Arid => (Fix64.One, Fix64.Parse("0.4"), Fix64.Parse("0.6"), Fix64.Parse("0.6")),
        ClimateBand.Tropical => (Fix64.Parse("1.1"), Fix64.Parse("0.8"), Fix64.Parse("1.1"), Fix64.Parse("0.8")),
        _ => (Fix64.Parse("0.6"), Fix64.One, Fix64.Parse("0.9"), Fix64.Parse("0.15")),
    };

    private static Fix64 MeatPerAnimal(HerdSpecies species) => species switch
    {
        HerdSpecies.Deer => Fix64.FromInt(4),
        HerdSpecies.Aurochs or HerdSpecies.Bison => Fix64.FromInt(15),
        HerdSpecies.WildBoar => Fix64.FromInt(5),
        HerdSpecies.Horse => Fix64.FromInt(10),
        HerdSpecies.Reindeer => Fix64.FromInt(5),
        HerdSpecies.WildGoatSheep or HerdSpecies.GazelleAntelope => Fix64.FromInt(3),
        HerdSpecies.CamelLlama => Fix64.FromInt(8),
        _ => Fix64.One,
    };

    private static Fix64 WoodStock(Biome biome) => biome switch
    {
        Biome.TemperateForest => Fix64.FromInt(30),
        Biome.OpenWoodland => Fix64.FromInt(15),
        Biome.GrasslandSteppe => Fix64.FromInt(2),
        Biome.Savanna => Fix64.FromInt(4),
        Biome.TropicalForest => Fix64.FromInt(40),
        Biome.Wetland => Fix64.FromInt(5),
        Biome.Taiga => Fix64.FromInt(25),
        Biome.Mountain => Fix64.FromInt(10),
        _ => Fix64.Zero,
    };
}
