using System;
using System.Collections.Generic;
using CivSim.Sim.Randomness;

namespace CivSim.Sim.World;

/// <summary>
/// Deterministic M2 world-generation pipeline. All procedural stages use named seed streams and
/// integer arithmetic; the 4-player MVP uses exact axial point symmetry (12 §2–3).
/// </summary>
public static class WorldGenerator
{
    private const int NoiseOne = 65_536;
    private const int WaterLandTargetPercent = 40;
    private static readonly ulong Root = Hash64.OfString("worldgen");

    private readonly record struct ScoredTile(int Score, int Tile);
    private readonly record struct StartCandidate(int Tile, int Score);
    private readonly record struct StartPair(int First, int Second, int WalkingDistance,
        int GeometricDistance, int ScoreGap, int GuaranteeGap, int ScoreSum);

    private sealed class Work
    {
        public readonly ulong Seed;
        public readonly int Width;
        public readonly int Height;
        public readonly int Count;
        public readonly WorldTile[] Tiles;
        public readonly List<Herd> Herds = new();
        public readonly List<PredatorPack> Predators = new();
        public int Repairs;

        public Work(ulong seed, WorldGenSettings settings)
        {
            Seed = seed;
            Width = settings.Width;
            Height = settings.Height;
            Count = checked(Width * Height);
            Tiles = new WorldTile[Count];
        }
    }

    public static GeneratedWorld Generate(ulong seed) => Generate(seed, WorldGenSettings.Standard);

    public static GeneratedWorld Generate(ulong seed, WorldGenSettings settings)
    {
        settings.Validate();
        var work = new Work(seed, settings);
        BuildLand(work);
        BuildElevation(work);
        BuildWater(work);
        BuildClimateAndBiomes(work);
        BuildNaturalDeposits(work);
        BuildFauna(work);
        int[] coastDistance = CoastDistances(work);
        int[] starts = PlaceStarts(work, coastDistance, out TerritoryFairnessResult fairness, out int fairnessAttempts);
        int[] startScores = new int[starts.Length];
        for (int i = 0; i < starts.Length; i++)
            startScores[i] = StartScore(work, starts[i], coastDistance[starts[i]],
                AverageForageWithin(work, starts[i], 3));
        ApplyStartGuarantees(work, starts, coastDistance);
        ApplyContinentalGuarantees(work);
        SortFauna(work);

        int land = CountLand(work.Tiles);
        int rivers = 0, lakes = 0, springs = 0;
        foreach (WorldTile tile in work.Tiles)
        {
            if ((tile.Flags & TileFlags.River) != 0) rivers++;
            if ((tile.Flags & TileFlags.Lake) != 0) lakes++;
            if ((tile.Flags & TileFlags.Spring) != 0) springs++;
        }
        var report = new WorldGenerationReport(land, rivers, lakes, springs,
            work.Herds.Count, work.Predators.Count, work.Repairs, fairnessAttempts,
            fairness.E7SharesBasisPoints.ToArray(), fairness.SettlementCounts.ToArray(),
            fairness.BlockedSeasons, fairness.FirstContactSeason,
            settings.Width, settings.Height);
        var publicStarts = new WorldStart[starts.Length];
        for (byte i = 0; i < starts.Length; i++) publicStarts[i] = new WorldStart(i, starts[i], startScores[i]);
        var generated = new GeneratedWorld(seed, settings, work.Tiles, publicStarts,
            work.Herds.ToArray(), work.Predators.ToArray(), report);
        WorldAcceptanceResult acceptance = WorldAcceptance.Validate(generated);
        if (!acceptance.Passed)
            throw new InvalidOperationException("Generated world failed M2 acceptance: " +
                string.Join("; ", acceptance.Failures));
        return generated;
    }

    private static void BuildLand(Work w)
    {
        int target = checked((w.Count * WaterLandTargetPercent + 50) / 100);
        target = checked(((target + 2) / 4) * 4); // four equal axial-rotation orbits
        int islandRadius = 1;
        int[] islandCenters =
        {
            HexGrid.Index(w.Width * 48 / 100, w.Height * 16 / 100, w.Width, w.Height),
            HexGrid.Index(w.Width * 48 / 100, w.Height * 31 / 100, w.Width, w.Height),
            HexGrid.Index(w.Width * 48 / 100, w.Height * 43 / 100, w.Width, w.Height)
        };
        int islandTileCount = 0;
        foreach (int center in islandCenters)
            for (int i = 0; i < w.Count; i++)
                if (HexGrid.Distance(center, i, w.Width) <= islandRadius &&
                    i % w.Width < w.Width / 2 && i / w.Width < w.Height / 2)
                {
                    SetSymmetricLand(w, i, 2);
                    islandTileCount += 4;
                }

        var candidates = new List<ScoredTile>();
        int channelStart = w.Width * 43 / 100;
        for (int y = 0; y < w.Height / 2; y++)
        for (int x = 0; x < w.Width / 2; x++)
        {
            if (x >= channelStart) continue;
            int index = y * w.Width + x;
            if (w.Tiles[index].IsLand) continue;
            int cxWest = w.Width * 25 / 100;
            int cxEast = w.Width - 1 - cxWest;
            int cy = (w.Height - 1) / 2;
            int dx = Math.Min(Math.Abs(x - cxWest), Math.Abs(x - cxEast));
            int dy = Math.Abs(y - cy);
            int rx = Math.Max(1, w.Width * 18 / 100);
            int ry = Math.Max(1, w.Height * 55 / 100);
            long ellipse = (long)dx * dx * 10_000 / (rx * rx) + (long)dy * dy * 10_000 / (ry * ry);
            int noise = SymmetricNoise(w.Seed, "land", x, y, w.Width, w.Height, 11, 7);
            int score = checked(100_000_000 - (int)ellipse * 900 + (noise - NoiseOne / 2) * 35);
            candidates.Add(new ScoredTile(score, index));
        }
        candidates.Sort(static (a, b) =>
        {
            int byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : a.Tile.CompareTo(b.Tile);
        });
        int representativesNeeded = target / 4 - islandTileCount / 4;
        if (representativesNeeded < 0 || representativesNeeded > candidates.Count)
            throw new InvalidOperationException("Could not meet the requested land fraction.");
        for (int i = 0; i < representativesNeeded; i++) SetSymmetricLand(w, candidates[i].Tile, 0);
    }

    private static void SetSymmetricLand(Work w, int index, byte continent)
    {
        Span<int> orbit = stackalloc int[4];
        GetSymmetricOrbit(index, w.Width, w.Height, orbit);
        for (int i = 0; i < orbit.Length; i++)
        {
            int tile = orbit[i];
            WorldTile t = w.Tiles[tile];
            int x = tile % w.Width;
            t.Biome = Biome.TemperateForest; // temporary land marker until biomes are assigned
            t.Continent = (byte)(continent == 0 ? (x < w.Width / 2 ? 0 : 1) : (x < w.Width / 2 ? 2 : 3));
            w.Tiles[tile] = t;
        }
    }

    private static void BuildElevation(Work w)
    {
        for (int i = 0; i < w.Count; i++)
        {
            WorldTile tile = w.Tiles[i];
            if (!tile.IsLand) { tile.Elevation = 0; w.Tiles[i] = tile; continue; }
            int x = i % w.Width, y = i / w.Width;
            int noise = SymmetricNoise(w.Seed, "elevation", x, y, w.Width, w.Height, 17, 11);
            int cxWest = w.Width * 25 / 100;
            int cx = tile.Continent is 0 or 2 ? cxWest : w.Width - 1 - cxWest;
            int ridgeDistance = Math.Abs((x - cx) * 8 - (2 * y - (w.Height - 1)));
            int ridge = Math.Max(0, 40 - ridgeDistance) * 3;
            int elevation = 55 + noise * 105 / NoiseOne + ridge;
            if (elevation > 255) elevation = 255;
            tile.Elevation = (byte)elevation;
            w.Tiles[i] = tile;
        }
    }

    private static void BuildWater(Work w)
    {
        int[] toCoast = CoastDistances(w);
        var sourceCandidates = new List<ScoredTile>();
        for (int y = 0; y < w.Height / 2; y++)
        for (int x = w.Width * 8 / 100; x < w.Width * 42 / 100; x++)
        {
            int i = y * w.Width + x;
            WorldTile t = w.Tiles[i];
            if (!t.IsLand || t.Continent != 0 || toCoast[i] < 10) continue;
            int n = SymmetricNoise(w.Seed, "riverheads", x, y, w.Width, w.Height, 9, 7);
            sourceCandidates.Add(new ScoredTile(t.Elevation * 1000 + n, i));
        }
        sourceCandidates.Sort(static (a, b) =>
        {
            int byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : a.Tile.CompareTo(b.Tile);
        });
        int[] riverHeads = ChooseSeparated(sourceCandidates, 2, 14, w.Width);
        foreach (int head in riverHeads) RouteRiver(w, head, toCoast);

        // One lake per quadrant gives two small basins per continent after the map symmetries.
        int lake = -1;
        int lakeScore = int.MaxValue;
        for (int y = w.Height / 4; y < w.Height / 2 - 3; y++)
        for (int x = w.Width * 8 / 100; x < w.Width * 42 / 100; x++)
        {
            int i = y * w.Width + x;
            WorldTile t = w.Tiles[i];
            if (!t.IsLand || t.Continent != 0 || toCoast[i] < 7 || toCoast[i] > 24 ||
                (t.Flags & TileFlags.River) != 0) continue;
            int score = t.Elevation * 100 + SymmetricNoise(w.Seed, "lakes", x, y, w.Width, w.Height, 8, 6);
            if (score < lakeScore) { lake = i; lakeScore = score; }
        }
        if (lake >= 0)
        {
            MarkSymmetricTile(w, lake, (tile, _) =>
            {
                tile.Flags |= TileFlags.Lake;
                tile.Biome = Biome.Ocean;
                return tile;
            });
        }

        // Springs are seeded at approximately one per sixty land tiles, evenly by symmetry orbit.
        var springCandidates = new List<ScoredTile>();
        for (int y = 0; y < w.Height / 2; y++)
        for (int x = 0; x < w.Width / 2; x++)
        {
            int i = y * w.Width + x;
            WorldTile t = w.Tiles[i];
            if (!t.IsLand || t.Continent is not (0 or 2) || (t.Flags & (TileFlags.River | TileFlags.Lake)) != 0) continue;
            int noise = SymmetricNoise(w.Seed, "springs", x, y, w.Width, w.Height, 37, 23);
            springCandidates.Add(new ScoredTile(noise, i));
        }
        springCandidates.Sort(static (a, b) =>
        {
            int byScore = a.Score.CompareTo(b.Score);
            return byScore != 0 ? byScore : a.Tile.CompareTo(b.Tile);
        });
        int springCount = Math.Max(1, CountLand(w.Tiles) / 60 / 4);
        for (int i = 0; i < Math.Min(springCount, springCandidates.Count); i++)
            SetSymmetricFlag(w, springCandidates[i].Tile, TileFlags.Spring);
    }

    private static void RouteRiver(Work w, int head, int[] toCoast, bool pointOnly = false)
    {
        int current = head;
        int guard = w.Width + w.Height * 2;
        Span<int> neighbors = stackalloc int[6];
        while (guard-- > 0)
        {
            if (pointOnly) SetPointFlag(w, current, TileFlags.River);
            else SetSymmetricFlag(w, current, TileFlags.River);
            if (toCoast[current] == 0) break;
            int count = HexGrid.Neighbors(current, w.Width, w.Height, neighbors);
            int next = -1;
            for (int n = 0; n < count; n++)
            {
                int candidate = neighbors[n];
                WorldTile t = w.Tiles[candidate];
                if (!t.IsLand || t.Continent != 0 || toCoast[candidate] >= toCoast[current]) continue;
                if (next < 0 || t.Elevation < w.Tiles[next].Elevation ||
                    (t.Elevation == w.Tiles[next].Elevation && HexGrid.CanonicalWestNorth(candidate, w.Width, w.Height) < HexGrid.CanonicalWestNorth(next, w.Width, w.Height)))
                    next = candidate;
            }
            if (next < 0) break;
            current = next;
        }
    }

    private static void BuildClimateAndBiomes(Work w)
    {
        for (int i = 0; i < w.Count; i++)
        {
            WorldTile tile = w.Tiles[i];
            if (!tile.IsLand) continue;
            int x = i % w.Width, y = i / w.Width;
            int latDistance = Math.Abs(2 * y - (w.Height - 1));
            int latitude = latDistance * 100 / w.Height;
            int rainNoise = SymmetricNoise(w.Seed, "rainfall", x, y, w.Width, w.Height, 13, 9);
            int rainfall = 10 + rainNoise * 70 / NoiseOne;
            if (tile.Elevation > 175) rainfall = Math.Min(100, rainfall + 5);
            if (tile.Elevation < 90) rainfall = Math.Min(100, rainfall + 7);
            if (latitude > 86) tile.Climate = ClimateBand.Cold;
            else if (latitude > 67) tile.Climate = ClimateBand.Cold;
            else if (latitude > 34) tile.Climate = ClimateBand.Temperate;
            else if (latitude > 11) tile.Climate = ClimateBand.Warm;
            else tile.Climate = ClimateBand.Tropical;
            if (rainfall < 30 && tile.Climate is ClimateBand.Warm or ClimateBand.Temperate)
                tile.Climate = ClimateBand.Arid;
            tile.Rainfall = (byte)Math.Clamp(rainfall, 0, 100);

            if (tile.Elevation >= 226) tile.Biome = Biome.Peak;
            else if (tile.Elevation >= 183) tile.Biome = Biome.Mountain;
            else if (tile.Climate == ClimateBand.Cold)
                tile.Biome = latitude > 83 ? Biome.Ice : rainfall > 60 ? Biome.Taiga : Biome.Tundra;
            else if (tile.Climate == ClimateBand.Arid) tile.Biome = Biome.Desert;
            // Wetlands cover damp lowlands, not just the exceptionally rainy bottom tail.
            // The old 76/108 cutoffs produced almost no start candidates across large seed audits.
            else if (rainfall >= 68 && tile.Elevation < 120) tile.Biome = Biome.Wetland;
            else if (tile.Climate == ClimateBand.Tropical) tile.Biome = rainfall > 42 ? Biome.TropicalForest : Biome.Savanna;
            else if (tile.Climate == ClimateBand.Warm) tile.Biome = rainfall > 55 ? Biome.OpenWoodland : Biome.Savanna;
            else if (rainfall > 56) tile.Biome = Biome.TemperateForest;
            else if (rainfall > 42) tile.Biome = Biome.OpenWoodland;
            else tile.Biome = Biome.GrasslandSteppe;
            if ((tile.Flags & TileFlags.Lake) != 0) tile.Biome = Biome.Ocean;
            tile.SoilFertility = tile.Biome switch
            {
                Biome.TemperateForest => 70,
                Biome.OpenWoodland => 65,
                Biome.GrasslandSteppe => 75,
                Biome.Savanna => 55,
                Biome.TropicalForest => 45,
                Biome.Wetland => 85,
                Biome.Taiga => 35,
                Biome.Tundra => 15,
                Biome.Desert => 20,
                Biome.Mountain => 25,
                _ => 0
            };
            if ((tile.Flags & TileFlags.River) != 0 && tile.Biome is not (Biome.Mountain or Biome.Peak))
                tile.SoilFertility = (byte)Math.Min(100, tile.SoilFertility + 10);
            if ((tile.Flags & TileFlags.River) != 0 && (tile.Climate is ClimateBand.Cold or ClimateBand.Temperate))
                tile.Flags |= TileFlags.SalmonRun;
            w.Tiles[i] = tile;
        }
        MarkCoasts(w);
        PlaceWildGrain(w);
    }

    private static void BuildNaturalDeposits(Work w)
    {
        PlaceDepositType(w, DepositKind.Stone, 100_000, 1, 1, static t => t.Elevation >= 130);
        PlaceDepositType(w, DepositKind.Flint, 25, 1, 2, static t => t.Elevation < 190);
        PlaceDepositType(w, DepositKind.Clay, 20, 1, 1, static t => (t.Flags & (TileFlags.River | TileFlags.Lake | TileFlags.Spring)) != 0 || t.Biome == Biome.Wetland);
        PlaceDepositType(w, DepositKind.Ochre, 8, 1, 1, static t => t.Elevation >= 115);
        PlaceDepositType(w, DepositKind.NativeCopper, 3, 1, 1, static t => t.Elevation >= 140);
        PlaceDepositType(w, DepositKind.CopperOre, 6, 1, 2, static t => t.Elevation >= 150);
        PlaceDepositType(w, DepositKind.ArsenicalCopper, 3, 2, 2, static t => t.Elevation >= 175);
        PlaceDepositType(w, DepositKind.Tin, 1, 1, 2, static t => t.Elevation >= 160);
        PlaceDepositType(w, DepositKind.Gold, 2, 1, 2, static t => t.Elevation >= 145);
        PlaceDepositType(w, DepositKind.Silver, 1, 1, 2, static t => t.Elevation >= 145);
        PlaceDepositType(w, DepositKind.Iron, 10, 1, 2, static t => t.Elevation >= 105);
        PlaceDepositType(w, DepositKind.Salt, 3, 1, 1, static t => t.Climate == ClimateBand.Arid || (t.Flags & TileFlags.CoastAdjacent) != 0);
        PlaceDepositType(w, DepositKind.Coal, 4, 2, 3, static t => t.Elevation < 160);
        PlaceDepositType(w, DepositKind.Oil, 2, 3, 3, static t => t.Elevation < 150);
        PlaceDepositType(w, DepositKind.Saltpeter, 2, 1, 1, static t => t.Climate == ClimateBand.Arid || t.Elevation > 140);
        PlaceDepositType(w, DepositKind.Sulfur, 1, 1, 1, static t => t.Elevation > 165);
        PlaceDepositType(w, DepositKind.Bauxite, 2, 1, 1, static t => t.Climate == ClimateBand.Tropical && t.Elevation > 100);
        PlaceDepositType(w, DepositKind.RareEarths, 1, 2, 2, static t => t.Elevation > 175);
        PlaceDepositType(w, DepositKind.Lithium, 1, 2, 2, static t => t.Climate == ClimateBand.Arid);
        PlaceDepositType(w, DepositKind.SilkMoths, 4, 0, 0, static t => (t.Climate is ClimateBand.Warm or ClimateBand.Temperate) && (t.Biome is Biome.OpenWoodland or Biome.TemperateForest));
        PlaceDepositType(w, DepositKind.Sugarcane, 3, 0, 0, static t => (t.Climate is ClimateBand.Warm or ClimateBand.Tropical) && t.Biome == Biome.Wetland);
    }

    private static void PlaceDepositType(Work w, DepositKind kind, int perThousand, byte minDepth,
        byte maxDepth, Predicate<WorldTile> eligible)
    {
        var candidates = new List<ScoredTile>();
        for (int y = 0; y < w.Height / 2; y++)
        for (int x = 0; x < w.Width / 2; x++)
        {
            int index = y * w.Width + x;
            WorldTile tile = w.Tiles[index];
            if (!tile.IsLand || tile.Continent != 0 || tile.Deposit != DepositKind.None || !eligible(tile)) continue;
            ulong h = Hash64.Combine(Hash64.Combine(w.Seed, Hash64.OfString("deposits." + kind)), (ulong)index);
            candidates.Add(new ScoredTile(unchecked((int)(h >> 32)), index));
        }
        candidates.Sort(static (a, b) =>
        {
            int byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : a.Tile.CompareTo(b.Tile);
        });
        int amount = perThousand == 100_000
            ? candidates.Count / 5
            : Math.Max(1, (CountLand(w.Tiles) * perThousand + 2_000) / 4_000);
        if (kind == DepositKind.SilkMoths || kind == DepositKind.Sugarcane) amount = perThousand;
        amount = Math.Min(amount, candidates.Count);
        for (int i = 0; i < amount; i++)
        {
            int index = candidates[i].Tile;
            int span = maxDepth - minDepth + 1;
            byte depth = minDepth == 0 ? (byte)0 : (byte)(minDepth + (int)(Hash64.Mix((ulong)index + (byte)kind) % (uint)span));
            byte quantity = (byte)(1 + Hash64.Mix(w.Seed ^ (ulong)index ^ (byte)kind) % 3);
            PlaceDepositSymmetric(w, index, kind, quantity, depth);
        }
    }

    private static void BuildFauna(Work w)
    {
        for (int i = 0; i < w.Count; i++)
        {
            if (HexGrid.CanonicalWestNorth(i, w.Width, w.Height) != i) continue;
            WorldTile tile = w.Tiles[i];
            if (!tile.IsLand || tile.Continent != 0) continue;
            int density = tile.Biome switch
            {
                Biome.GrasslandSteppe => 7,
                Biome.Savanna => 8,
                Biome.OpenWoodland => 5,
                Biome.TemperateForest => 4,
                Biome.Taiga => 4,
                Biome.Tundra => 5,
                Biome.Wetland => 3,
                Biome.Mountain => 2,
                Biome.Desert => 1,
                _ => 0
            };
            uint roll = (uint)(Hash64.Mix(Hash64.Combine(w.Seed, (ulong)i ^ Hash64.OfString("herds"))) % 100);
            if (roll >= density) continue;
            HerdSpecies species = SpeciesFor(tile, roll);
            ushort size = species switch
            {
                HerdSpecies.Deer => (ushort)(15 + roll % 16),
                HerdSpecies.Aurochs => (ushort)(20 + roll % 21),
                HerdSpecies.WildBoar => (ushort)(10 + roll % 11),
                HerdSpecies.Bison => (ushort)(40 + roll % 61),
                HerdSpecies.Horse => (ushort)(20 + roll % 31),
                HerdSpecies.Reindeer => (ushort)(50 + roll % 101),
                HerdSpecies.WildGoatSheep => (ushort)(15 + roll % 16),
                HerdSpecies.GazelleAntelope => (ushort)(30 + roll % 31),
                HerdSpecies.CamelLlama => (ushort)(10 + roll % 11),
                _ => 10
            };
            byte growth = species switch
            {
                HerdSpecies.Aurochs => 18,
                HerdSpecies.Bison => 15,
                HerdSpecies.Horse => 18,
                HerdSpecies.Reindeer => 20,
                _ => 30
            };
            AddHerdSymmetric(w, i, species, size, growth);
        }

        PlacePredators(w, PredatorSpecies.Wolves, Math.Max(1, CountLand(w.Tiles) / (120 * 4)),
            static t => t.Biome is not (Biome.Desert or Biome.Ice or Biome.Peak));
        PlacePredators(w, PredatorSpecies.BigCat, Math.Max(1, CountLand(w.Tiles) / (200 * 4)),
            static t => t.Biome is Biome.Savanna or Biome.TemperateForest or Biome.TropicalForest);
        PlacePredators(w, PredatorSpecies.Bear, Math.Max(1, CountLand(w.Tiles) / (250 * 4)),
            static t => t.Biome is Biome.TemperateForest or Biome.Taiga);
    }

    private static void PlacePredators(Work w, PredatorSpecies kind, int target, Predicate<WorldTile> eligible)
    {
        var candidates = new List<ScoredTile>();
        for (int i = 0; i < w.Count; i++)
        {
            if (HexGrid.CanonicalWestNorth(i, w.Width, w.Height) != i) continue;
            WorldTile tile = w.Tiles[i];
            if (!tile.IsLand || tile.Continent != 0 || !eligible(tile)) continue;
            int score = unchecked((int)(Hash64.Mix(Hash64.Combine(w.Seed, Hash64.Combine((byte)kind, (ulong)i))) >> 32));
            candidates.Add(new ScoredTile(score, i));
        }
        candidates.Sort(static (a, b) =>
        {
            int byScore = a.Score.CompareTo(b.Score);
            return byScore != 0 ? byScore : a.Tile.CompareTo(b.Tile);
        });
        for (int i = 0; i < Math.Min(target, candidates.Count); i++)
        {
            int tile = candidates[i].Tile;
            byte pack = kind == PredatorSpecies.Wolves ? (byte)(4 + (tile % 5)) : (byte)1;
            AddPredatorSymmetric(w, tile, kind, pack);
        }
    }

    private static int[] PlaceStarts(Work w, int[] coastDistance,
        out TerritoryFairnessResult chosenFairness, out int fairnessAttempts)
    {
        int mapMedian = MedianForage(w.Tiles);
        int[] peakDistance = DistancesToPeaks(w);
        var candidates = new List<StartCandidate>();
        int minY = w.Height / 5, maxY = w.Height - minY;
        for (int y = minY; y < maxY; y++)
        for (int x = w.Width * 8 / 100; x < w.Width * 43 / 100; x++)
        {
            int index = y * w.Width + x;
            WorldTile tile = w.Tiles[index];
            if (!tile.IsLand || tile.Continent != 0 || tile.Climate == ClimateBand.Cold || tile.HabitableWeight == 0 ||
                tile.Biome is Biome.Mountain or Biome.Peak || peakDistance[index] <= 4 || coastDistance[index] < 5) continue;
            // Water and two local herds are repaired by the post-placement guarantees. Keeping
            // them out of this prefilter prevents sparse fauna seeds from collapsing pair search.
            if (HabitableRatioWithin(w, index, 6) < 60) continue;
            int forage = AverageForageWithin(w, index, 3);
            if (forage * 100 < mapMedian * 90) continue;
            candidates.Add(new StartCandidate(index, StartScore(w, index, coastDistance[index], forage)));
        }
        candidates.Sort(static (a, b) =>
        {
            int byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : a.Tile.CompareTo(b.Tile);
        });
        if (candidates.Count > 640) candidates.RemoveRange(640, candidates.Count - 640);

        Herd[] herds = w.Herds.ToArray();
        PredatorPack[] predators = w.Predators.ToArray();
        int[] guaranteeDistanceScores = new int[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
            guaranteeDistanceScores[i] = WorldAcceptance.GuaranteeDistanceScore(
                w.Tiles, herds, predators, w.Width, candidates[i].Tile);

        int[][] walkingDistances = new int[candidates.Count][];
        for (int i = 0; i < candidates.Count; i++)
            walkingDistances[i] = HexGrid.WalkingCostsFrom(w.Tiles, w.Width, w.Height, candidates[i].Tile, 280);

        var pairs = new List<StartPair>();
        for (int i = 0; i < candidates.Count; i++)
        for (int j = i + 1; j < candidates.Count; j++)
        {
            StartCandidate a = candidates[i], b = candidates[j];
            int distance = walkingDistances[i][b.Tile];
            if (distance is < 220 or > 280) continue;
            int mean = (a.Score + b.Score) / 2;
            int gap = Math.Abs(a.Score - b.Score);
            if (mean == 0 || gap * 100 > mean * 5) continue;
            int guaranteeGap = Math.Abs(guaranteeDistanceScores[i] - guaranteeDistanceScores[j]);
            pairs.Add(new StartPair(a.Tile, b.Tile, distance,
                HexGrid.Distance(a.Tile, b.Tile, w.Width), gap, guaranteeGap, a.Score + b.Score));
        }
        pairs.Sort(static (a, b) =>
        {
            int byGap = a.ScoreGap.CompareTo(b.ScoreGap);
            if (byGap != 0) return byGap;
            int byGuarantees = a.GuaranteeGap.CompareTo(b.GuaranteeGap);
            if (byGuarantees != 0) return byGuarantees;
            int byGeometricSpacing = b.GeometricDistance.CompareTo(a.GeometricDistance);
            if (byGeometricSpacing != 0) return byGeometricSpacing;
            int byContactSpacing = b.WalkingDistance.CompareTo(a.WalkingDistance);
            if (byContactSpacing != 0) return byContactSpacing;
            int byScore = b.ScoreSum.CompareTo(a.ScoreSum);
            if (byScore != 0) return byScore;
            int byFirst = a.First.CompareTo(b.First);
            return byFirst != 0 ? byFirst : a.Second.CompareTo(b.Second);
        });

        // Resource parity is a useful pre-filter, but terrain geometry decides the frontier race.
        // Search a broad deterministic slice of similarly-scored pairs before accepting a map.
        int[] candidateOrder = BuildCandidateOrder(pairs.Count, Math.Min(1_024, pairs.Count));
        int attempts = candidateOrder.Length;
        TerritoryFairnessResult? best = null;
        int bestDeviation = int.MaxValue;
        int blockedRejects = 0, contactWindowRejects = 0, noContactRejects = 0;
        int completedGateRejects = 0, repairedParityRejects = 0;
        int earliestRejectedContact = int.MaxValue, latestRejectedContact = 0;
        for (int i = 0; i < attempts; i++)
        {
            StartPair pair = pairs[candidateOrder[i]];
            int[] starts = { pair.First, pair.Second,
                HexGrid.Rotate180(pair.First, w.Width, w.Height),
                HexGrid.Rotate180(pair.Second, w.Width, w.Height) };
            TerritoryFairnessResult fairness = TerritoryFairnessSimulator.EvaluateCandidate(w.Tiles, w.Width, w.Height, starts);
            if (!fairness.IsComplete)
            {
                if (fairness.BlockedSeasons > 8) blockedRejects++;
                else if (fairness.FirstContactSeason == int.MaxValue) noContactRejects++;
                else
                {
                    contactWindowRejects++;
                    earliestRejectedContact = Math.Min(earliestRejectedContact, fairness.FirstContactSeason);
                    latestRejectedContact = Math.Max(latestRejectedContact, fairness.FirstContactSeason);
                }
                continue;
            }
            int deviation = RelativeFairnessDeviation(fairness.E7SharesBasisPoints.Span);
            if (deviation < bestDeviation)
            {
                best = fairness;
                bestDeviation = deviation;
            }
            if (!fairness.PassesE7Tolerance() || fairness.BlockedSeasons > 8 ||
                !WorldAcceptance.ContactSeasonIsInStandardWindow(fairness.FirstContactSeason))
            {
                completedGateRejects++;
                continue;
            }
            if (!GuaranteeParityAfterRepairs(w, starts, coastDistance))
            {
                repairedParityRejects++;
                continue;
            }

            chosenFairness = fairness;
            fairnessAttempts = i + 1;
            return starts;
        }

        string bestText = best is null
            ? "no valid neighbor pair was available"
            : $"best E7 shares were [{string.Join(",", best.E7SharesBasisPoints.ToArray())}], " +
              $"blocked seasons={best.BlockedSeasons}, first contact={best.FirstContactSeason}";
        throw new InvalidOperationException(
            $"No passing M2 start pair found after {attempts} of {pairs.Count} candidate pairs " +
            $"(blocked={blockedRejects}, contact-outside-window={contactWindowRejects}, no-contact={noContactRejects}, " +
            $"contact-range={earliestRejectedContact}..{latestRejectedContact}, " +
            $"other fairness gates={completedGateRejects}, repaired guarantee parity={repairedParityRejects}); {bestText}.");
    }

    private static bool GuaranteeParityAfterRepairs(Work source, int[] starts, int[] coastDistance)
    {
        var repaired = new Work(source.Seed, new WorldGenSettings(source.Width, source.Height, 4));
        Array.Copy(source.Tiles, repaired.Tiles, source.Count);
        repaired.Herds.AddRange(source.Herds);
        repaired.Predators.AddRange(source.Predators);
        try
        {
            ApplyStartGuarantees(repaired, starts, coastDistance);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        Herd[] herds = repaired.Herds.ToArray();
        PredatorPack[] predators = repaired.Predators.ToArray();
        int first = WorldAcceptance.GuaranteeDistanceScore(repaired.Tiles, herds, predators,
            repaired.Width, starts[0]);
        int second = WorldAcceptance.GuaranteeDistanceScore(repaired.Tiles, herds, predators,
            repaired.Width, starts[1]);
        return WorldAcceptance.GuaranteeDistancesAreInParity(first, second);
    }

    private static int[] BuildCandidateOrder(int count, int initialSampleCount)
    {
        int[] order = new int[count];
        bool[] used = new bool[count];
        int written = 0;
        for (int sample = 0; sample < initialSampleCount; sample++)
        {
            int index = (int)(((long)(2 * sample + 1) * count) / (2 * initialSampleCount));
            order[written++] = index;
            used[index] = true;
        }
        for (int index = 0; index < count; index++)
            if (!used[index]) order[written++] = index;
        return order;
    }

    private static int[] DistancesToPeaks(Work w)
    {
        int[] distance = new int[w.Count];
        Array.Fill(distance, int.MaxValue);
        int[] queue = new int[w.Count];
        int head = 0, tail = 0;
        for (int tile = 0; tile < w.Count; tile++)
            if (w.Tiles[tile].Biome == Biome.Peak) { distance[tile] = 0; queue[tail++] = tile; }
        Span<int> neighbors = stackalloc int[6];
        while (head < tail)
        {
            int tile = queue[head++];
            int count = HexGrid.Neighbors(tile, w.Width, w.Height, neighbors);
            for (int n = 0; n < count; n++)
            {
                int next = neighbors[n];
                if (distance[next] != int.MaxValue) continue;
                distance[next] = distance[tile] + 1;
                queue[tail++] = next;
            }
        }
        return distance;
    }

    private static int RelativeFairnessDeviation(ReadOnlySpan<int> shares)
    {
        if (shares.Length == 0) return int.MaxValue;
        int total = 0;
        foreach (int share in shares) total += share;
        int mean = total / shares.Length;
        int largest = 0;
        foreach (int share in shares) largest = Math.Max(largest, Math.Abs(share - mean));
        return mean == 0 ? int.MaxValue : checked((int)((long)largest * 10_000 / mean));
    }

    private static int StartScore(Work w, int index, int coastDistance, int forage)
    {
        int game = Math.Min(100, HerdCountWithin(w, index, 12, out _) * 15);
        int water = FreshwaterWithin(w, index, 2) > 0 ? 100 : 0;
        int stone = 0;
        int soil = 0, soilCount = 0;
        int x = index % w.Width, y = index / w.Width;
        for (int ny = Math.Max(0, y - 10); ny <= Math.Min(w.Height - 1, y + 10); ny++)
        for (int nx = Math.Max(0, x - 11); nx <= Math.Min(w.Width - 1, x + 11); nx++)
        {
            int i = ny * w.Width + nx;
            int d = HexGrid.Distance(index, i, w.Width);
            if (d <= 10 && w.Tiles[i].Deposit is DepositKind.Stone or DepositKind.Flint) stone += 5;
            if (d <= 6 && w.Tiles[i].IsLand) { soil += w.Tiles[i].SoilFertility; soilCount++; }
        }
        int room = HabitableRatioWithin(w, index, 6);
        int coast = coastDistance <= 20 ? 100 - coastDistance * 3 : 40;
        return forage * 30 + Math.Min(100, game) * 20 + water * 15 + Math.Min(100, stone) * 10 +
               (soilCount == 0 ? 0 : soil / soilCount) * 10 + room * 10 + Math.Max(0, coast) * 5;
    }

    private static void ApplyStartGuarantees(Work w, int[] starts, int[] coastDistance)
    {
        // Repair each west-side start independently, then point-mirror it to the matching east start.
        // Offset-grid north/south reflection is not a hex-lattice isometry, so it cannot carry local radii.
        EnsureStartGuarantees(w, starts[0]);
        EnsureStartGuarantees(w, starts[1]);
        _ = coastDistance;
    }

    private static void EnsureStartGuarantees(Work w, int start)
    {
        if (FreshwaterWithin(w, start, 2) == 0)
        {
            SetPointFlag(w, start, TileFlags.Spring);
            w.Repairs++;
        }
        if (!HasFlagWithin(w, start, TileFlags.River, 4))
        {
            int[] coast = CoastDistances(w);
            RouteRiver(w, start, coast, pointOnly: true);
            w.Repairs++;
        }
        EnsureDepositNear(w, start, DepositKind.Clay, 20, 1);
        EnsureDepositNear(w, start, DepositKind.NativeCopper, 20, 1);
        EnsureDepositNear(w, start, DepositKind.Flint, 15, 1);
        if (!HasPredatorWithin(w, start, PredatorSpecies.Wolves, 20))
        {
            int tile = FindSymmetricSite(w, start, 18, static t => t.IsLand && t.Continent == 0, false);
            AddPredatorPointSymmetric(w, tile, PredatorSpecies.Wolves, 6);
            w.Repairs++;
        }
        if (!HasHerdWithin(w, start, 25, HerdSpecies.WildGoatSheep) &&
            !HasHerdWithin(w, start, 25, HerdSpecies.CamelLlama))
        {
            int tile = FindSymmetricSite(w, start, 22, static t => t.IsLand && t.Continent == 0, false);
            AddHerdPointSymmetric(w, tile, HerdSpecies.WildGoatSheep, 22, 30);
            w.Repairs++;
        }
        if (HerdCountWithin(w, start, 12, out bool large) < 2 || !large)
        {
            int tile = FindSymmetricSite(w, start, 10, static t => t.IsLand && t.Continent == 0, false);
            AddHerdPointSymmetric(w, tile, HerdSpecies.Aurochs, 30, 18);
            int other = FindSymmetricSite(w, start, 9, static t => t.IsLand && t.Continent == 0, true);
            AddHerdPointSymmetric(w, other, HerdSpecies.Deer, 20, 30);
            w.Repairs++;
        }
        if (CountWildGrainWithin(w, start, 10) < 4)
        {
            Span<int> neighbors = stackalloc int[6];
            int count = HexGrid.Neighbors(start, w.Width, w.Height, neighbors);
            int placed = 0;
            SetPointFlag(w, start, TileFlags.WildGrain);
            placed++;
            for (int n = 0; n < count && placed < 4; n++)
            {
                if (!w.Tiles[neighbors[n]].IsLand || w.Tiles[neighbors[n]].Continent != 0) continue;
                SetPointFlag(w, neighbors[n], TileFlags.WildGrain);
                placed++;
            }
            w.Repairs++;
        }
    }

    private static void ApplyContinentalGuarantees(Work w)
    {
        EnsureContinentDeposit(w, DepositKind.Gold, DepositKind.Silver, 1);
        EnsureContinentDeposit(w, DepositKind.Coal, DepositKind.Coal, 1);
        EnsureContinentDeposit(w, DepositKind.Oil, DepositKind.Oil, 1);
        EnsureContinentDeposit(w, DepositKind.Tin, DepositKind.Tin, 1);
        EnsureContinentDeposit(w, DepositKind.Iron, DepositKind.Iron, 2);
        EnsureContinentDeposit(w, DepositKind.Salt, DepositKind.Salt, 1);
    }

    private static void EnsureDepositNear(Work w, int start, DepositKind kind, int radius, byte depth)
    {
        if (DepositWithin(w, start, kind, radius)) return;
        int tile = FindSymmetricSite(w, start, Math.Max(1, radius - 2),
            t => t.IsLand && t.Continent == 0 && t.Deposit == DepositKind.None, false);
        PlaceDepositPointSymmetric(w, tile, kind, 1, depth);
        w.Repairs++;
    }

    private static void EnsureContinentDeposit(Work w, DepositKind kind, DepositKind alternative, int needed)
    {
        int have = 0;
        for (int i = 0; i < w.Count; i++)
            if (w.Tiles[i].Continent == 0 && (w.Tiles[i].Deposit == kind || (alternative != kind && w.Tiles[i].Deposit == alternative))) have++;
        if (have >= needed) return;
        for (int n = have; n < needed; n++)
        {
            int center = (w.Height / 4) * w.Width + w.Width / 4;
            int tile = FindSymmetricSite(w, center, w.Width,
                t => t.IsLand && t.Continent == 0 && t.Deposit == DepositKind.None, true);
            PlaceDepositSymmetric(w, tile, kind, 2, kind is DepositKind.Coal or DepositKind.Oil ? (byte)2 : (byte)1);
            w.Repairs++;
        }
    }

    private static int FindSymmetricSite(Work w, int center, int radius, Predicate<WorldTile> eligible, bool preferInterior)
    {
        int best = -1, bestScore = int.MinValue;
        for (int i = 0; i < w.Count; i++)
        {
            if (i % w.Width >= w.Width / 2) continue;
            WorldTile tile = w.Tiles[i];
            if (!eligible(tile)) continue;
            int distance = HexGrid.Distance(center, i, w.Width);
            if (distance > radius) continue;
            int score = preferInterior ? -distance * 100 : distance * 100;
            score -= tile.Elevation / 4;
            score -= (int)(Hash64.Mix(w.Seed ^ (ulong)i) & 63);
            if (score > bestScore || (score == bestScore && (best < 0 || i < best)))
            { best = i; bestScore = score; }
        }
        if (best < 0)
        {
            for (int i = 0; i < w.Count; i++)
                if (i % w.Width < w.Width / 2 && eligible(w.Tiles[i])) { best = i; break; }
        }
        if (best < 0) throw new InvalidOperationException("No suitable tile exists for a required world guarantee.");
        return best;
    }

    private static void MarkCoasts(Work w)
    {
        Span<int> neighbors = stackalloc int[6];
        for (int i = 0; i < w.Count; i++)
        {
            WorldTile tile = w.Tiles[i];
            if (!tile.IsLand) continue;
            int count = HexGrid.Neighbors(i, w.Width, w.Height, neighbors);
            bool coast = count < 6;
            for (int n = 0; n < count && !coast; n++)
                if (!w.Tiles[neighbors[n]].IsLand) { coast = true; break; }
            if (!coast) continue;
            tile.Flags |= TileFlags.CoastAdjacent;
            w.Tiles[i] = tile;
            for (int n = 0; n < count; n++)
            {
                int neighbor = neighbors[n];
                WorldTile water = w.Tiles[neighbor];
                if (water.IsLand || water.Biome != Biome.Ocean) continue;
                water.Biome = Biome.Coast;
                w.Tiles[neighbor] = water;
            }
        }
    }

    private static void PlaceWildGrain(Work w)
    {
        for (int i = 0; i < w.Count; i++)
        {
            if (HexGrid.CanonicalWestNorth(i, w.Width, w.Height) != i) continue;
            WorldTile tile = w.Tiles[i];
            if (!tile.IsLand || tile.Biome is not (Biome.OpenWoodland or Biome.GrasslandSteppe or Biome.Savanna)) continue;
            int chance = tile.Biome == Biome.GrasslandSteppe ? 40 : tile.Biome == Biome.OpenWoodland ? 25 : 35;
            if (Hash64.Mix(w.Seed ^ (ulong)i ^ Hash64.OfString("wildgrain")) % 100 < (uint)chance)
                SetSymmetricFlag(w, i, TileFlags.WildGrain);
        }
    }

    private static void MarkSymmetricTile(Work w, int index, Func<WorldTile, int, WorldTile> mutate)
    {
        Span<int> orbit = stackalloc int[4];
        GetSymmetricOrbit(index, w.Width, w.Height, orbit);
        for (int n = 0; n < orbit.Length; n++)
        {
            int i = orbit[n];
            w.Tiles[i] = mutate(w.Tiles[i], i);
        }
    }

    private static void SetSymmetricFlag(Work w, int index, TileFlags flags)
        => MarkSymmetricTile(w, index, (tile, _) => { tile.Flags |= flags; return tile; });

    private static void MarkPointSymmetricTile(Work w, int index, Func<WorldTile, WorldTile> mutate)
    {
        int mirror = HexGrid.Rotate180(index, w.Width, w.Height);
        w.Tiles[index] = mutate(w.Tiles[index]);
        w.Tiles[mirror] = mutate(w.Tiles[mirror]);
    }

    private static void SetPointFlag(Work w, int index, TileFlags flags)
        => MarkPointSymmetricTile(w, index, tile => { tile.Flags |= flags; return tile; });

    private static void PlaceDepositSymmetric(Work w, int index, DepositKind kind, byte amount, byte depth)
        => MarkSymmetricTile(w, index, (tile, _) =>
        {
            tile.Deposit = kind;
            tile.DepositAmount = amount;
            tile.DepositDepth = depth;
            return tile;
        });

    private static void PlaceDepositPointSymmetric(Work w, int index, DepositKind kind, byte amount, byte depth)
        => MarkPointSymmetricTile(w, index, tile =>
        {
            tile.Deposit = kind;
            tile.DepositAmount = amount;
            tile.DepositDepth = depth;
            return tile;
        });

    private static void AddHerdSymmetric(Work w, int index, HerdSpecies species, ushort size, byte growth)
    {
        Span<int> orbit = stackalloc int[4];
        GetSymmetricOrbit(index, w.Width, w.Height, orbit);
        for (int n = 0; n < orbit.Length; n++) w.Herds.Add(new Herd(orbit[n], species, size, growth));
    }

    private static void AddHerdPointSymmetric(Work w, int index, HerdSpecies species, ushort size, byte growth)
    {
        w.Herds.Add(new Herd(index, species, size, growth));
        w.Herds.Add(new Herd(HexGrid.Rotate180(index, w.Width, w.Height), species, size, growth));
    }

    private static void AddPredatorSymmetric(Work w, int index, PredatorSpecies species, byte count)
    {
        Span<int> orbit = stackalloc int[4];
        GetSymmetricOrbit(index, w.Width, w.Height, orbit);
        for (int n = 0; n < orbit.Length; n++) w.Predators.Add(new PredatorPack(orbit[n], species, count));
    }

    private static void AddPredatorPointSymmetric(Work w, int index, PredatorSpecies species, byte count)
    {
        w.Predators.Add(new PredatorPack(index, species, count));
        w.Predators.Add(new PredatorPack(HexGrid.Rotate180(index, w.Width, w.Height), species, count));
    }

    private static int[] ChooseSeparated(List<ScoredTile> candidates, int count, int spacing, int width)
    {
        var chosen = new List<int>();
        foreach (ScoredTile candidate in candidates)
        {
            bool far = true;
            for (int i = 0; i < chosen.Count; i++)
                if (HexGrid.Distance(candidate.Tile, chosen[i], width) < spacing) { far = false; break; }
            if (!far) continue;
            chosen.Add(candidate.Tile);
            if (chosen.Count == count) break;
        }
        return chosen.ToArray();
    }

    private static int[] CoastDistances(Work w)
    {
        int[] distance = new int[w.Count];
        Array.Fill(distance, int.MaxValue);
        int[] queue = new int[w.Count];
        int head = 0, tail = 0;
        Span<int> neighbors = stackalloc int[6];
        for (int i = 0; i < w.Count; i++)
        {
            if (!w.Tiles[i].IsLand) continue;
            int count = HexGrid.Neighbors(i, w.Width, w.Height, neighbors);
            bool coast = count < 6;
            for (int n = 0; n < count && !coast; n++)
                if (!w.Tiles[neighbors[n]].IsLand) coast = true;
            if (coast) { distance[i] = 0; queue[tail++] = i; }
        }
        while (head < tail)
        {
            int i = queue[head++];
            int count = HexGrid.Neighbors(i, w.Width, w.Height, neighbors);
            for (int n = 0; n < count; n++)
            {
                int j = neighbors[n];
                if (!w.Tiles[j].IsLand || distance[j] <= distance[i] + 1) continue;
                distance[j] = distance[i] + 1;
                queue[tail++] = j;
            }
        }
        return distance;
    }

    private static int FreshwaterWithin(Work w, int center, int radius)
    {
        int found = 0;
        int x = center % w.Width, y = center / w.Width;
        for (int ny = Math.Max(0, y - radius); ny <= Math.Min(w.Height - 1, y + radius); ny++)
        for (int nx = Math.Max(0, x - radius - 1); nx <= Math.Min(w.Width - 1, x + radius + 1); nx++)
        {
            int i = ny * w.Width + nx;
            if (HexGrid.Distance(center, i, w.Width) <= radius &&
                (w.Tiles[i].Flags & (TileFlags.River | TileFlags.Lake | TileFlags.Spring)) != 0) found++;
        }
        return found;
    }

    private static bool HasFlagWithin(Work w, int center, TileFlags flag, int radius)
    {
        for (int i = 0; i < w.Count; i++)
            if ((w.Tiles[i].Flags & flag) != 0 && HexGrid.Distance(center, i, w.Width) <= radius) return true;
        return false;
    }

    private static int HabitableRatioWithin(Work w, int center, int radius)
    {
        int area = 0, habitable = 0;
        int x = center % w.Width, y = center / w.Width;
        for (int ny = Math.Max(0, y - radius); ny <= Math.Min(w.Height - 1, y + radius); ny++)
        for (int nx = Math.Max(0, x - radius - 1); nx <= Math.Min(w.Width - 1, x + radius + 1); nx++)
        {
            int i = ny * w.Width + nx;
            if (HexGrid.Distance(center, i, w.Width) > radius) continue;
            area++;
            if (w.Tiles[i].HabitableWeight > 0) habitable++;
        }
        return area == 0 ? 0 : habitable * 100 / area;
    }

    private static int HerdCountWithin(Work w, int center, int radius, out bool largeGame)
    {
        int count = 0;
        largeGame = false;
        foreach (Herd herd in w.Herds)
        {
            if (HexGrid.Distance(center, herd.Tile, w.Width) > radius) continue;
            count++;
            if (herd.Species is HerdSpecies.Aurochs or HerdSpecies.Bison or HerdSpecies.Horse && herd.Size >= 20)
                largeGame = true;
        }
        return count;
    }

    private static int AverageForageWithin(Work w, int center, int radius)
    {
        int total = 0, count = 0;
        int x = center % w.Width, y = center / w.Width;
        for (int ny = Math.Max(0, y - radius); ny <= Math.Min(w.Height - 1, y + radius); ny++)
        for (int nx = Math.Max(0, x - radius - 1); nx <= Math.Min(w.Width - 1, x + radius + 1); nx++)
        {
            int i = ny * w.Width + nx;
            if (HexGrid.Distance(center, i, w.Width) <= radius && w.Tiles[i].IsLand)
            { total += w.Tiles[i].ForageScore; count++; }
        }
        return count == 0 ? 0 : total / count;
    }

    private static int MedianForage(WorldTile[] tiles)
    {
        var values = new List<byte>();
        foreach (WorldTile tile in tiles) if (tile.IsLand && tile.HabitableWeight > 0) values.Add(tile.ForageScore);
        values.Sort();
        return values.Count == 0 ? 0 : values[values.Count / 2];
    }

    private static int CountLand(WorldTile[] tiles)
    {
        int count = 0;
        foreach (WorldTile tile in tiles) if (tile.IsLand) count++;
        return count;
    }

    private static bool DepositWithin(Work w, int center, DepositKind kind, int radius)
    {
        int x = center % w.Width, y = center / w.Width;
        for (int ny = Math.Max(0, y - radius); ny <= Math.Min(w.Height - 1, y + radius); ny++)
        for (int nx = Math.Max(0, x - radius - 1); nx <= Math.Min(w.Width - 1, x + radius + 1); nx++)
        {
            int i = ny * w.Width + nx;
            if (w.Tiles[i].Deposit == kind && HexGrid.Distance(center, i, w.Width) <= radius) return true;
        }
        return false;
    }

    private static int CountWildGrainWithin(Work w, int center, int radius)
    {
        int count = 0;
        int x = center % w.Width, y = center / w.Width;
        for (int ny = Math.Max(0, y - radius); ny <= Math.Min(w.Height - 1, y + radius); ny++)
        for (int nx = Math.Max(0, x - radius - 1); nx <= Math.Min(w.Width - 1, x + radius + 1); nx++)
        {
            int i = ny * w.Width + nx;
            if ((w.Tiles[i].Flags & TileFlags.WildGrain) != 0 && HexGrid.Distance(center, i, w.Width) <= radius) count++;
        }
        return count;
    }

    private static bool HasHerdWithin(Work w, int center, int radius, HerdSpecies species)
    {
        foreach (Herd herd in w.Herds)
            if (herd.Species == species && HexGrid.Distance(center, herd.Tile, w.Width) <= radius) return true;
        return false;
    }

    private static bool HasPredatorWithin(Work w, int center, PredatorSpecies species, int radius)
    {
        foreach (PredatorPack pack in w.Predators)
            if (pack.Species == species && HexGrid.Distance(center, pack.Tile, w.Width) <= radius) return true;
        return false;
    }

    private static int FindHerdTile(Work w, int center, int radius, bool avoidExisting)
        => FindSymmetricSite(w, center, radius, t => t.IsLand && t.Continent == 0, avoidExisting);

    private static HerdSpecies SpeciesFor(WorldTile tile, uint roll) => tile.Biome switch
    {
        Biome.TemperateForest => roll % 3 == 0 ? HerdSpecies.Aurochs : roll % 3 == 1 ? HerdSpecies.WildBoar : HerdSpecies.Deer,
        Biome.OpenWoodland => roll % 2 == 0 ? HerdSpecies.Deer : HerdSpecies.Aurochs,
        Biome.GrasslandSteppe => roll % 3 == 0 ? HerdSpecies.Bison : roll % 3 == 1 ? HerdSpecies.Horse : HerdSpecies.Aurochs,
        Biome.Savanna => HerdSpecies.GazelleAntelope,
        Biome.TropicalForest => roll % 2 == 0 ? HerdSpecies.WildBoar : HerdSpecies.Deer,
        Biome.Wetland => roll % 2 == 0 ? HerdSpecies.WildBoar : HerdSpecies.Deer,
        Biome.Taiga or Biome.Tundra => HerdSpecies.Reindeer,
        Biome.Mountain => HerdSpecies.WildGoatSheep,
        Biome.Desert => roll % 2 == 0 ? HerdSpecies.CamelLlama : HerdSpecies.GazelleAntelope,
        _ => HerdSpecies.SmallGame
    };

    private static void SortFauna(Work w)
    {
        w.Herds.Sort(static (a, b) =>
        {
            int tile = a.Tile.CompareTo(b.Tile);
            return tile != 0 ? tile : a.Species.CompareTo(b.Species);
        });
        w.Predators.Sort(static (a, b) =>
        {
            int tile = a.Tile.CompareTo(b.Tile);
            return tile != 0 ? tile : a.Species.CompareTo(b.Species);
        });
    }

    private static void GetSymmetricOrbit(int index, int width, int height, Span<int> destination)
    {
        destination[0] = index;
        destination[1] = HexGrid.ReflectNorthSouth(index, width, height);
        destination[2] = HexGrid.Rotate180(index, width, height);
        destination[3] = HexGrid.ReflectEastWest(index, width, height);
    }

    private static int SymmetricNoise(ulong seed, string stage, int x, int y,
        int width, int height, int cellsX, int cellsY)
    {
        int a = ValueNoise(seed, stage, x, y, width, height, cellsX, cellsY);
        int b = ValueNoise(seed, stage, width - 1 - x, height - 1 - y, width, height, cellsX, cellsY);
        int c = ValueNoise(seed, stage, x, height - 1 - y, width, height, cellsX, cellsY);
        int d = ValueNoise(seed, stage, width - 1 - x, y, width, height, cellsX, cellsY);
        return (a + b + c + d) / 4;
    }

    private static int ValueNoise(ulong seed, string stage, int x, int y,
        int width, int height, int cellsX, int cellsY)
    {
        int gx = (int)((long)x * (cellsX - 1) * NoiseOne / Math.Max(1, width - 1));
        int gy = (int)((long)y * (cellsY - 1) * NoiseOne / Math.Max(1, height - 1));
        int x0 = gx / NoiseOne, y0 = gy / NoiseOne;
        int tx = Smooth(gx - x0 * NoiseOne), ty = Smooth(gy - y0 * NoiseOne);
        int a = Lerp(NoiseValue(seed, stage, x0, y0), NoiseValue(seed, stage, x0 + 1, y0), tx);
        int b = Lerp(NoiseValue(seed, stage, x0, y0 + 1), NoiseValue(seed, stage, x0 + 1, y0 + 1), tx);
        return Lerp(a, b, ty);
    }

    private static int NoiseValue(ulong seed, string stage, int x, int y)
    {
        ulong h = Hash64.Combine(Hash64.Combine(Hash64.Combine(seed, Root), Hash64.OfString(stage)),
            unchecked((ulong)((long)y << 32 | (uint)x)));
        return (int)(Hash64.Mix(h) & 0xFFFF);
    }

    private static int Smooth(int t)
    {
        long t2 = (long)t * t / NoiseOne;
        return (int)(t2 * (3L * NoiseOne - 2L * t) / NoiseOne);
    }

    private static int Lerp(int a, int b, int t) => a + (int)((long)(b - a) * t / NoiseOne);
}
