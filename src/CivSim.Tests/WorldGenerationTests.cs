using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Economy;
using CivSim.Sim.World;

namespace CivSim.Tests;

public sealed class WorldGenerationTests
{
    [Fact]
    public void Odd_r_hex_rotation_preserves_neighbors_and_distance()
    {
        const int width = 160, height = 100;
        Span<int> neighbors = stackalloc int[6];
        for (int tile = 0; tile < width * height; tile += 37)
        {
            int rotated = HexGrid.Rotate180(tile, width, height);
            Assert.Equal(tile, HexGrid.Rotate180(rotated, width, height));
            Assert.Equal(HexGrid.Distance(tile, 1_234, width),
                HexGrid.Distance(rotated, HexGrid.Rotate180(1_234, width, height), width));
            int count = HexGrid.Neighbors(tile, width, height, neighbors);
            for (int i = 0; i < count; i++)
                Assert.Contains(HexGrid.Rotate180(neighbors[i], width, height),
                    NeighborsOf(rotated, width, height));
        }
    }

    [Fact]
    public void Fission_site_ties_choose_point_symmetric_tiles_for_mirrored_parents()
    {
        const int width = 8, height = 6;
        int westParent = 5;
        int eastParent = HexGrid.Rotate180(westParent, width, height);
        int westCandidateA = 6, westCandidateB = 8;
        int eastCandidateA = HexGrid.Rotate180(westCandidateA, width, height);
        int eastCandidateB = HexGrid.Rotate180(westCandidateB, width, height);

        int westChoice = LivingEconomyModel.PreferFissionTieCandidate(
            westParent, westCandidateA, westCandidateB, width, height) ? westCandidateA : westCandidateB;
        int eastChoice = LivingEconomyModel.PreferFissionTieCandidate(
            eastParent, eastCandidateA, eastCandidateB, width, height) ? eastCandidateA : eastCandidateB;

        Assert.Equal(HexGrid.Rotate180(westChoice, width, height), eastChoice);
    }

    [Fact]
    public void Same_seed_produces_identical_four_way_symmetric_worlds()
    {
        GeneratedWorld first = WorldGenerator.Generate(271828);
        GeneratedWorld second = WorldGenerator.Generate(271828);
        Assert.Equal(first.Fingerprint(), second.Fingerprint());
        Assert.NotEqual(first.Fingerprint(), WorldGenerator.Generate(271829).Fingerprint());

        for (int i = 0; i < first.TileCount; i++)
        {
            WorldTile tile = first.TileAt(i);
            WorldTile rotated = first.TileAt(HexGrid.Rotate180(i, first.Width, first.Height));
            Assert.Equal(tile.Elevation, rotated.Elevation);
            Assert.Equal(tile.Climate, rotated.Climate);
            Assert.Equal(tile.Biome, rotated.Biome);
            Assert.Equal(tile.Flags, rotated.Flags);
            Assert.Equal(tile.Rainfall, rotated.Rainfall);
            Assert.Equal(tile.SoilFertility, rotated.SoilFertility);
            if (tile.IsLand || (tile.Flags & TileFlags.Lake) != 0)
                Assert.Equal(tile.Continent == 0 ? 1 : tile.Continent == 1 ? 0 : tile.Continent == 2 ? 3 : 2, rotated.Continent);
            Assert.Equal(tile.Deposit, rotated.Deposit);
            Assert.Equal(tile.DepositAmount, rotated.DepositAmount);
            Assert.Equal(tile.DepositDepth, rotated.DepositDepth);
        }
    }

    [Fact]
    public void Standard_world_meets_land_start_guarantees_and_contact_window()
    {
        GeneratedWorld world = WorldGenerator.Generate(42);
        WorldAcceptanceResult acceptance = WorldAcceptance.Validate(world);
        Assert.True(acceptance.Passed, string.Join("\n", acceptance.Failures));
        Assert.Equal(16_000, world.TileCount);
        Assert.InRange(world.Report.LandShareBasisPoints, 3_800, 4_200);
        Assert.Equal(4, world.Starts.Length);
        (int firstPairWalk, int secondPairWalk) = WorldAcceptance.NeighborPairWalkingDistances(world);
        Assert.InRange(firstPairWalk, 22, 28);
        Assert.InRange(secondPairWalk, 22, 28);
        Assert.InRange(world.Report.FirstContactSeason, 60, 100);
        Assert.True(world.Report.BlockedSeasons <= 8,
            $"blocked={world.Report.BlockedSeasons}, settlements={string.Join(',', world.Report.E7SettlementCounts.ToArray())}");
        Assert.True(world.Report.E7SharesBasisPoints.Span.Length == 4);
        Assert.True(E7Fair(world.Report.E7SharesBasisPoints.Span));

        for (int c = 0; c < world.Starts.Length; c++) AssertStartGuarantees(world, world.Starts[c].Tile);
        foreach (int continent in new[] { 0, 1 })
        {
            Assert.Contains(world.Tiles.ToArray(), t => t.Continent == continent && t.Deposit is DepositKind.Gold or DepositKind.Silver);
            Assert.Contains(world.Tiles.ToArray(), t => t.Continent == continent && t.Deposit == DepositKind.Coal);
            Assert.Contains(world.Tiles.ToArray(), t => t.Continent == continent && t.Deposit == DepositKind.Oil);
            Assert.Contains(world.Tiles.ToArray(), t => t.Continent == continent && t.Deposit == DepositKind.Tin);
            Assert.True(world.Tiles.ToArray().Count(t => t.Continent == continent && t.Deposit == DepositKind.Iron) >= 2);
            Assert.Contains(world.Tiles.ToArray(), t => t.Continent == continent && t.Deposit == DepositKind.Salt);
        }
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(101UL)]
    [InlineData(777UL)]
    public void Bronze_geography_places_a_contested_tin_field_far_placers_and_island_tin(ulong seed)
    {
        GeneratedWorld world = WorldGenerator.Generate(seed);
        WorldTile[] tiles = world.Tiles.ToArray();
        foreach (byte continent in new byte[] { 0, 1 })
        {
            Assert.Equal(1, tiles.Count(t => t.Continent == continent && t.Deposit == DepositKind.Tin && t.DepositAmount == 2));
            Assert.Equal(1, tiles.Count(t => t.Continent == continent && t.Deposit == DepositKind.Tin && t.DepositAmount == 3));
            Assert.Equal(2, tiles.Count(t => t.Continent == continent && t.Deposit == DepositKind.Tin && t.DepositAmount == 1));
            Assert.Equal(1, tiles.Count(t => t.Continent == continent + 2 && t.Deposit == DepositKind.Tin));
        }
        // Nobody starts with tin in the early territory, and every start has the arsenical route.
        foreach (WorldStart start in world.Starts)
        {
            for (int i = 0; i < world.TileCount; i++)
                if (tiles[i].Deposit == DepositKind.Tin) Assert.True(HexGrid.Distance(start.Tile, i, world.Width) >= 8);
            Assert.Contains(Enumerable.Range(0, world.TileCount), i =>
                tiles[i].Deposit == DepositKind.ArsenicalCopper && HexGrid.Distance(start.Tile, i, world.Width) <= 25);
        }
    }

    [Theory]
    [InlineData(20261087UL)]
    [InlineData(20261202UL)]
    public void Tin_middle_field_stays_inside_the_m2_acceptance_envelope(ulong seed)
    {
        GeneratedWorld world = WorldGenerator.Generate(seed);
        WorldTile[] tiles = world.Tiles.ToArray();
        int tin = Array.FindIndex(tiles, tile => tile.Continent == 0 &&
            tile.Deposit == DepositKind.Tin && tile.DepositAmount == 2);
        Assert.True(tin >= 0, $"seed {seed} has no west middle-field placer");

        int first = world.Starts[0].Tile, second = world.Starts[1].Tile;
        int[] toFirst = HexGrid.WalkingCostsFrom(tiles, world.Width, world.Height, first, 400);
        int[] toSecond = HexGrid.WalkingCostsFrom(tiles, world.Width, world.Height, second, 400);
        Assert.InRange(toFirst[tin], 80, 240);
        Assert.InRange(toSecond[tin], 80, 240);
        Assert.InRange(Math.Abs(toFirst[tin] - toSecond[tin]), 0, 50);
        Assert.True(HexGrid.Distance(first, tin, world.Width) >= 8);
        Assert.True(HexGrid.Distance(second, tin, world.Width) >= 8);
        Assert.True(WorldAcceptance.Validate(world).Passed,
            string.Join("\n", WorldAcceptance.Validate(world).Failures));
    }

    [Theory]
    [InlineData(3696UL)]
    [InlineData(3864UL)]
    public void Far_tin_placers_stay_inside_the_m2_acceptance_envelope(ulong seed)
    {
        GeneratedWorld world = WorldGenerator.Generate(seed);
        WorldTile[] tiles = world.Tiles.ToArray();

        for (int pair = 0; pair < 2; pair++)
        {
            int first = world.Starts[pair * 2].Tile;
            int second = world.Starts[pair * 2 + 1].Tile;
            byte continent = world.TileAt(first).Continent;
            int[] toFirst = HexGrid.WalkingCostsFrom(tiles, world.Width, world.Height, first, 400);
            int[] toSecond = HexGrid.WalkingCostsFrom(tiles, world.Width, world.Height, second, 400);
            int[] farFirst = Enumerable.Range(0, world.TileCount)
                .Where(i => tiles[i].Continent == continent && tiles[i].Deposit == DepositKind.Tin &&
                            tiles[i].DepositAmount == 1 && toFirst[i] < toSecond[i])
                .ToArray();
            int[] farSecond = Enumerable.Range(0, world.TileCount)
                .Where(i => tiles[i].Continent == continent && tiles[i].Deposit == DepositKind.Tin &&
                            tiles[i].DepositAmount == 1 && toSecond[i] <= toFirst[i])
                .ToArray();

            Assert.Single(farFirst);
            Assert.Single(farSecond);
            Assert.InRange(toFirst[farFirst[0]], 180, 360);
            Assert.InRange(toSecond[farSecond[0]], 180, 360);
            Assert.InRange(Math.Abs(toFirst[farFirst[0]] - toSecond[farSecond[0]]), 0, 50);
        }

        WorldAcceptanceResult acceptance = WorldAcceptance.Validate(world);
        Assert.True(acceptance.Passed, string.Join("\n", acceptance.Failures));
    }

    [Fact]
    public void Seed_42_matches_world_generation_fingerprint_golden()
    {
        string expected = File.ReadAllText(RepoPaths.Golden("worldgen-v1.txt")).Replace("\r\n", "\n").Trim();
        GeneratedWorld world = WorldGenerator.Generate(42);
        string actual = $"seed={world.Seed} fingerprint={world.Fingerprint():x16}";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Generated_world_exposes_expected_terrain_water_and_fauna_layers()
    {
        GeneratedWorld world = WorldGenerator.Generate(101);
        Assert.True(world.Report.RiverTiles > 100);
        Assert.InRange(world.Report.LakeTiles, 4, 16);
        Assert.True(world.Report.SpringCount >= 80);
        Assert.True(world.Herds.Length >= 200);
        Assert.True(world.Predators.Length >= 40);
        Assert.Contains(world.Tiles.ToArray(), t => t.Biome == Biome.TemperateForest);
        Assert.Contains(world.Tiles.ToArray(), t => t.Biome == Biome.GrasslandSteppe);
        Assert.Contains(world.Tiles.ToArray(), t => t.Biome == Biome.Desert);
        Assert.Contains(world.Tiles.ToArray(), t => t.Biome == Biome.Mountain);
    }

    private static int[] NeighborsOf(int tile, int width, int height)
    {
        Span<int> buffer = stackalloc int[6];
        int count = HexGrid.Neighbors(tile, width, height, buffer);
        return buffer[..count].ToArray();
    }

    private static string Describe(WorldTile tile) =>
        $"elev={tile.Elevation} climate={tile.Climate} biome={tile.Biome} flags={tile.Flags} rain={tile.Rainfall} soil={tile.SoilFertility} continent={tile.Continent} deposit={tile.Deposit}/{tile.DepositAmount}/{tile.DepositDepth}";

    private static bool E7Fair(ReadOnlySpan<int> shares)
    {
        int mean = 0;
        foreach (int share in shares) mean += share;
        mean /= shares.Length;
        foreach (int share in shares) if ((long)Math.Abs(share - mean) * 100 > (long)mean * 15) return false;
        return true;
    }

    private static void AssertStartGuarantees(GeneratedWorld world, int start)
    {
        Assert.True(AnyTile(world, start, 2, t => (t.Flags & (TileFlags.River | TileFlags.Lake | TileFlags.Spring)) != 0));
        Assert.True(AnyTile(world, start, 20, t => t.Deposit == DepositKind.Clay));
        Assert.True(AnyTile(world, start, 20, t => t.Deposit == DepositKind.NativeCopper));
        Assert.True(AnyTile(world, start, 15, t => t.Deposit == DepositKind.Flint));
        Assert.Contains(world.Predators.ToArray(), p => p.Species == PredatorSpecies.Wolves && HexGrid.Distance(start, p.Tile, world.Width) <= 20);
        Assert.Contains(world.Herds.ToArray(), h => (h.Species is HerdSpecies.WildGoatSheep or HerdSpecies.CamelLlama) && HexGrid.Distance(start, h.Tile, world.Width) <= 25);
        Herd[] near = world.Herds.ToArray().Where(h => HexGrid.Distance(start, h.Tile, world.Width) <= 12).ToArray();
        Assert.True(near.Length >= 2);
        Assert.Contains(near, h => h.Species is HerdSpecies.Aurochs or HerdSpecies.Bison or HerdSpecies.Horse);
        Assert.True(AnyTile(world, start, 10, t => (t.Flags & TileFlags.WildGrain) != 0, minimum: 4));
    }

    private static bool AnyTile(GeneratedWorld world, int center, int radius,
        Func<WorldTile, bool> predicate, int minimum = 1)
    {
        int found = 0;
        for (int tile = 0; tile < world.TileCount; tile++)
            if (HexGrid.Distance(center, tile, world.Width) <= radius && predicate(world.TileAt(tile)) && ++found >= minimum)
                return true;
        return false;
    }
}
