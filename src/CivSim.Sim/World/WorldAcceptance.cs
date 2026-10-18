using System;
using System.Collections.Generic;

namespace CivSim.Sim.World;

/// <summary>Checks the objective M2 world-generation gates from 12 §12 and 17 §3.</summary>
public static class WorldAcceptance
{
    internal static bool ContactSeasonIsInStandardWindow(int season) => season is >= 60 and <= 100;

    public static WorldAcceptanceResult Validate(GeneratedWorld world)
    {
        var failures = new List<string>();
        if (world.Report.LandShareBasisPoints is < 3_800 or > 4_200)
            failures.Add($"land share {world.Report.LandShareBasisPoints} bp is outside 3800–4200");
        if (world.Starts.Length != 4) failures.Add("expected four civilization starts");
        int pointMismatch = PointSymmetryMismatches(world);
        if (pointMismatch != 0) failures.Add($"point symmetry differs on {pointMismatch} tiles");

        int riverStarts = 0, coastalStarts = 0;
        for (int i = 0; i < world.Starts.Length; i++)
        {
            int start = world.Starts[i].Tile;
            if (AnyTile(world, start, 2, t => (t.Flags & (TileFlags.River | TileFlags.Lake | TileFlags.Spring)) != 0) == false)
                failures.Add($"civ {i} has no freshwater within 2 tiles");
            if (!HasDeposit(world, start, DepositKind.Clay, 20)) failures.Add($"civ {i} has no clay within 20 tiles");
            if (!HasDeposit(world, start, DepositKind.NativeCopper, 20)) failures.Add($"civ {i} has no native copper within 20 tiles");
            if (!HasDeposit(world, start, DepositKind.Flint, 15)) failures.Add($"civ {i} has no flint within 15 tiles");
            if (!HasPredator(world, start, PredatorSpecies.Wolves, 20)) failures.Add($"civ {i} has no wolf pack within 20 tiles");
            if (!HasSmallHerd(world, start, 25)) failures.Add($"civ {i} has no goat/sheep or camelid herd within 25 tiles");
            if (!HasTwoHerds(world, start, 12)) failures.Add($"civ {i} has fewer than two nearby herds or no large game");
            if (CountWildGrain(world, start, 10) < 4) failures.Add($"civ {i} has fewer than four wild-grain tiles within 10 tiles");
            if (AnyTile(world, start, 6, static t => (t.Flags & TileFlags.River) != 0)) riverStarts++;
            // "Coastal" means coast access in the start's radius-20 economy, as in the §9.2 score.
            if (AnyTile(world, start, 20, static t => (t.Flags & TileFlags.CoastAdjacent) != 0)) coastalStarts++;
        }
        CheckSpacing(world, failures);
        CheckContinents(world, failures);
        if (!E7Fair(world.Report.E7SharesBasisPoints.Span)) failures.Add("E7 simulated shares exceed the ±15% mean tolerance");
        if (world.Report.BlockedSeasons > 8) failures.Add($"settlement expansion was blocked for {world.Report.BlockedSeasons} seasons");
        if (!ContactSeasonIsInStandardWindow(world.Report.FirstContactSeason))
            failures.Add("first contact is outside minutes 15–25");

        return new WorldAcceptanceResult(failures.ToArray(), riverStarts, coastalStarts);
    }

    private static int PointSymmetryMismatches(GeneratedWorld world)
    {
        int mismatches = 0;
        for (int i = 0; i < world.TileCount; i++)
        {
            WorldTile a = world.TileAt(i);
            WorldTile b = world.TileAt(HexGrid.Rotate180(i, world.Width, world.Height));
            byte expectedContinent = a.Continent switch { 0 => 1, 1 => 0, 2 => 3, 3 => 2, _ => a.Continent };
            bool continentMatches = !a.IsLand && (a.Flags & TileFlags.Lake) == 0 || b.Continent == expectedContinent;
            if (a.Elevation != b.Elevation || a.Climate != b.Climate || a.Biome != b.Biome ||
                a.Flags != b.Flags || a.Rainfall != b.Rainfall || a.SoilFertility != b.SoilFertility ||
                !continentMatches || a.Deposit != b.Deposit || a.DepositAmount != b.DepositAmount ||
                a.DepositDepth != b.DepositDepth) mismatches++;
        }
        for (int i = 0; i < world.Herds.Length; i++)
        {
            Herd a = world.Herds[i];
            if (!HasMirrorHerd(world, a, HexGrid.Rotate180(a.Tile, world.Width, world.Height))) mismatches++;
        }
        for (int i = 0; i < world.Predators.Length; i++)
        {
            PredatorPack a = world.Predators[i];
            if (!HasMirrorPredator(world, a, HexGrid.Rotate180(a.Tile, world.Width, world.Height))) mismatches++;
        }
        return mismatches;
    }

    private static bool HasMirrorHerd(GeneratedWorld world, Herd herd, int tile)
    {
        foreach (Herd other in world.Herds)
            if (other.Tile == tile && other.Species == herd.Species && other.Size == herd.Size && other.GrowthPercent == herd.GrowthPercent) return true;
        return false;
    }

    private static bool HasMirrorPredator(GeneratedWorld world, PredatorPack pack, int tile)
    {
        foreach (PredatorPack other in world.Predators)
            if (other.Tile == tile && other.Species == pack.Species && other.Count == pack.Count) return true;
        return false;
    }

    private static void CheckSpacing(GeneratedWorld world, List<string> failures)
    {
        (int first, int second) = NeighborPairWalkingDistances(world);
        if (first is < 22 or > 28) failures.Add($"west neighbor pair spacing is {first}");
        if (second is < 22 or > 28) failures.Add($"east neighbor pair spacing is {second}");
        if (world.TileAt(world.Starts[0].Tile).Continent != world.TileAt(world.Starts[1].Tile).Continent ||
            world.TileAt(world.Starts[2].Tile).Continent != world.TileAt(world.Starts[3].Tile).Continent)
            failures.Add("a neighbor pair is split across continents");
        if (world.TileAt(world.Starts[0].Tile).Continent == world.TileAt(world.Starts[2].Tile).Continent)
            failures.Add("the two neighbor pairs are not on separate continents");
        if (world.Starts[2].Tile != HexGrid.Rotate180(world.Starts[0].Tile, world.Width, world.Height) ||
            world.Starts[3].Tile != HexGrid.Rotate180(world.Starts[1].Tile, world.Width, world.Height))
            failures.Add("east pair is not the exact point-symmetric image of the west pair");

        CheckParity(world.Starts[0].StartScore, world.Starts[1].StartScore, 5, "neighbor-pair start scores", failures);
        WorldTile[] tiles = world.Tiles.ToArray();
        Herd[] herds = world.Herds.ToArray();
        PredatorPack[] predators = world.Predators.ToArray();
        int firstGuaranteeDistance = GuaranteeDistanceScore(tiles, herds, predators, world.Width, world.Starts[0].Tile);
        int secondGuaranteeDistance = GuaranteeDistanceScore(tiles, herds, predators, world.Width, world.Starts[1].Tile);
        if (!GuaranteeDistancesAreInParity(firstGuaranteeDistance, secondGuaranteeDistance))
            failures.Add($"neighbor-pair guarantee distances differ by more than 20% of their radii " +
                $"({firstGuaranteeDistance} vs {secondGuaranteeDistance})");
    }

    internal static bool GuaranteeDistancesAreInParity(int first, int second) =>
        Math.Abs(first - second) <= 2_000;

    public static (int First, int Second) NeighborPairWalkingDistances(GeneratedWorld world)
    {
        WorldTile[] tiles = world.Tiles.ToArray();
        int[] west = HexGrid.WalkingCostsFrom(tiles, world.Width, world.Height, world.Starts[0].Tile, 280);
        int[] east = HexGrid.WalkingCostsFrom(tiles, world.Width, world.Height, world.Starts[2].Tile, 280);
        return (RoundTenths(west[world.Starts[1].Tile]), RoundTenths(east[world.Starts[3].Tile]));
    }

    private static int RoundTenths(int cost) => cost == int.MaxValue ? int.MaxValue : (cost + 5) / 10;

    private static void CheckParity(int first, int second, int tolerancePercent, string label, List<string> failures)
    {
        int mean = (first + second) / 2;
        if (mean == 0 || (long)Math.Abs(first - second) * 100 > (long)mean * tolerancePercent)
            failures.Add($"{label} exceed ±{tolerancePercent}% parity ({first} vs {second})");
    }

    internal static int GuaranteeDistanceScore(WorldTile[] tiles, Herd[] herds,
        PredatorPack[] predators, int width, int start)
    {
        int score = 0;
        int water = int.MaxValue, clay = int.MaxValue, copper = int.MaxValue, flint = int.MaxValue, grain = int.MaxValue;
        for (int tile = 0; tile < tiles.Length; tile++)
        {
            WorldTile value = tiles[tile];
            if ((value.Flags & (TileFlags.River | TileFlags.Lake | TileFlags.Spring)) == 0 &&
                value.Deposit is not (DepositKind.Clay or DepositKind.NativeCopper or DepositKind.Flint) &&
                (value.Flags & TileFlags.WildGrain) == 0) continue;
            int distance = HexGrid.Distance(start, tile, width);
            if ((value.Flags & (TileFlags.River | TileFlags.Lake | TileFlags.Spring)) != 0) water = Math.Min(water, distance);
            if (value.Deposit == DepositKind.Clay) clay = Math.Min(clay, distance);
            if (value.Deposit == DepositKind.NativeCopper) copper = Math.Min(copper, distance);
            if (value.Deposit == DepositKind.Flint) flint = Math.Min(flint, distance);
            if ((value.Flags & TileFlags.WildGrain) != 0) grain = Math.Min(grain, distance);
        }
        score += NormalizeDistance(water, 2);
        score += NormalizeDistance(clay, 20);
        score += NormalizeDistance(copper, 20);
        score += NormalizeDistance(flint, 15);

        int wolf = int.MaxValue;
        foreach (PredatorPack pack in predators)
            if (pack.Species == PredatorSpecies.Wolves)
                wolf = Math.Min(wolf, HexGrid.Distance(start, pack.Tile, width));
        score += NormalizeDistance(wolf, 20);

        int smallHerd = int.MaxValue, firstHerd = int.MaxValue, secondHerd = int.MaxValue;
        int largeGame = int.MaxValue;
        foreach (Herd herd in herds)
        {
            int distance = HexGrid.Distance(start, herd.Tile, width);
            if (herd.Species is HerdSpecies.WildGoatSheep or HerdSpecies.CamelLlama)
                smallHerd = Math.Min(smallHerd, distance);
            if (herd.Species is HerdSpecies.Aurochs or HerdSpecies.Bison or HerdSpecies.Horse)
                largeGame = Math.Min(largeGame, distance);
            if (distance < firstHerd) { secondHerd = firstHerd; firstHerd = distance; }
            else if (distance < secondHerd) secondHerd = distance;
        }
        score += NormalizeDistance(smallHerd, 25);
        score += NormalizeDistance(largeGame, 12);
        score += NormalizeDistance(firstHerd, 12);
        score += NormalizeDistance(secondHerd, 12);
        score += NormalizeDistance(grain, 10);
        return score;
    }

    private static int NormalizeDistance(int distance, int radius)
    {
        if (distance == int.MaxValue) distance = radius + 1;
        return distance * 1_000 / radius;
    }

    private static void CheckContinents(GeneratedWorld world, List<string> failures)
    {
        for (byte continent = 0; continent < 2; continent++)
        {
            bool goldOrSilver = false, coal = false, oil = false, tin = false, salt = false;
            int iron = 0;
            foreach (WorldTile tile in world.Tiles)
            {
                if (tile.Continent != continent) continue;
                goldOrSilver |= tile.Deposit is DepositKind.Gold or DepositKind.Silver;
                coal |= tile.Deposit == DepositKind.Coal;
                oil |= tile.Deposit == DepositKind.Oil;
                tin |= tile.Deposit == DepositKind.Tin;
                salt |= tile.Deposit == DepositKind.Salt;
                if (tile.Deposit == DepositKind.Iron) iron++;
            }
            if (!goldOrSilver) failures.Add($"continent {continent} lacks gold or silver");
            if (!coal) failures.Add($"continent {continent} lacks coal");
            if (!oil) failures.Add($"continent {continent} lacks oil");
            if (!tin) failures.Add($"continent {continent} lacks tin");
            if (iron < 2) failures.Add($"continent {continent} has fewer than two iron deposits");
            if (!salt) failures.Add($"continent {continent} lacks salt");
        }
    }

    private static bool E7Fair(ReadOnlySpan<int> shares)
    {
        if (shares.Length != 4) return false;
        int total = 0;
        foreach (int share in shares) total += share;
        int mean = total / shares.Length;
        foreach (int share in shares)
            if ((long)Math.Abs(share - mean) * 100 > (long)mean * 15) return false;
        return true;
    }

    private static bool AnyTile(GeneratedWorld world, int center, int radius, Predicate<WorldTile> predicate)
    {
        for (int i = 0; i < world.TileCount; i++)
            if (HexGrid.Distance(center, i, world.Width) <= radius && predicate(world.TileAt(i))) return true;
        return false;
    }

    private static bool HasDeposit(GeneratedWorld world, int center, DepositKind kind, int radius)
    {
        for (int i = 0; i < world.TileCount; i++)
            if (world.TileAt(i).Deposit == kind && HexGrid.Distance(center, i, world.Width) <= radius) return true;
        return false;
    }

    private static bool HasPredator(GeneratedWorld world, int center, PredatorSpecies species, int radius)
    {
        foreach (PredatorPack pack in world.Predators)
            if (pack.Species == species && HexGrid.Distance(center, pack.Tile, world.Width) <= radius) return true;
        return false;
    }

    private static bool HasSmallHerd(GeneratedWorld world, int center, int radius)
    {
        foreach (Herd herd in world.Herds)
            if ((herd.Species is HerdSpecies.WildGoatSheep or HerdSpecies.CamelLlama) &&
                HexGrid.Distance(center, herd.Tile, world.Width) <= radius) return true;
        return false;
    }

    private static bool HasTwoHerds(GeneratedWorld world, int center, int radius)
    {
        int count = 0;
        bool large = false;
        foreach (Herd herd in world.Herds)
        {
            if (HexGrid.Distance(center, herd.Tile, world.Width) > radius) continue;
            count++;
            large |= herd.Species is HerdSpecies.Aurochs or HerdSpecies.Bison or HerdSpecies.Horse;
        }
        return count >= 2 && large;
    }

    private static int CountWildGrain(GeneratedWorld world, int center, int radius)
    {
        int count = 0;
        for (int i = 0; i < world.TileCount; i++)
            if ((world.TileAt(i).Flags & TileFlags.WildGrain) != 0 && HexGrid.Distance(center, i, world.Width) <= radius) count++;
        return count;
    }
}

public sealed class WorldAcceptanceResult
{
    internal WorldAcceptanceResult(string[] failures, int riverStarts, int coastalStarts)
    {
        Failures = failures;
        RiverStarts = riverStarts;
        CoastalStarts = coastalStarts;
    }

    public string[] Failures { get; }
    public bool Passed => Failures.Length == 0;
    public int RiverStarts { get; }
    public int CoastalStarts { get; }
}
