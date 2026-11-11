using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Economy;
using CivSim.Sim.Content;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;
using Xunit;

namespace CivSim.Tests;

public sealed class SettlementCoreGrowthTests
{
    [Fact]
    public void Step_matches_HexGrid_neighbors_on_even_and_odd_rows()
    {
        const int width = 13, height = 9;
        Span<int> neighbors = stackalloc int[6];
        Span<int> stepped = stackalloc int[6];
        for (int tile = 0; tile < width * height; tile++)
        {
            int neighborCount = HexGrid.Neighbors(tile, width, height, neighbors);
            int steppedCount = 0;
            for (int direction = 0; direction < 6; direction++)
            {
                int next = HexGrid.Step(tile, direction, width, height);
                if (next >= 0) stepped[steppedCount++] = next;
            }
            if (steppedCount != neighborCount) Assert.Fail($"tile {tile}: Step found {steppedCount}, Neighbors found {neighborCount}");
            for (int i = 0; i < neighborCount; i++)
            {
                bool found = false;
                for (int j = 0; j < steppedCount; j++) found |= neighbors[i] == stepped[j];
                if (!found) Assert.Fail($"tile {tile}: Step and Neighbors disagree");
            }
            for (int i = 0; i < steppedCount; i++)
                for (int j = i + 1; j < steppedCount; j++)
                    if (stepped[i] == stepped[j]) Assert.Fail($"tile {tile}: Step repeated neighbor {stepped[i]}");
        }
        Assert.Equal(-1, HexGrid.Step(0, 3, width, height));
        Assert.Equal(-1, HexGrid.Step(0, 4, width, height));
        Assert.Equal(-1, HexGrid.Step(0, 5, width, height));
    }

    [Fact]
    public void Positions_are_the_twelve_ring_two_hexes()
    {
        GeneratedWorld generated = WorldGenerator.Generate(20261019);
        Span<int> positions = stackalloc int[SettlementCoreGrowth.OuterPositions];
        for (int site = 0; site < generated.TileCount; site++)
        {
            int front = site % 6;
            bool complete = true;
            for (int p = 0; p < positions.Length; p++)
            {
                int tile = SettlementCoreGrowth.PositionTile(site, front, p, generated.Width, generated.Height);
                positions[p] = tile;
                if (tile < 0) { complete = false; continue; }
                Assert.Equal(2, HexGrid.Distance(site, tile, generated.Width));
                for (int previous = 0; previous < p; previous++)
                    Assert.NotEqual(tile, positions[previous]);
            }
            if (!complete) continue;
            for (int y = Math.Max(0, site / generated.Width - 2); y <= Math.Min(generated.Height - 1, site / generated.Width + 2); y++)
                for (int x = Math.Max(0, site % generated.Width - 3); x <= Math.Min(generated.Width - 1, site % generated.Width + 3); x++)
                {
                    int tile = y * generated.Width + x;
                    if (HexGrid.Distance(site, tile, generated.Width) == 2 && !positions.Contains(tile))
                        Assert.Fail($"site {site}: ring-two tile {tile} was omitted");
                }
        }
    }

    [Fact]
    public void Front_prefers_sea_then_river_then_site_and_uses_the_integer_seed_fraction()
    {
        const int width = 1002, height = 3;
        WorldEconomyTile[] allWater = FlatTiles(width * height, Biome.Ocean);
        WorldEconomyInput waterWorld = MakeWorld(width, height, allWater);
        // These sites cover every residue modulo 997 while staying away from the horizontal map edge.
        for (int x = 2; x <= 998; x++)
        {
            int site = width + x;
            int expected = (int)(((double)(site % 997) / 997.0) * 6.0);
            Assert.Equal(expected, SettlementCoreGrowth.Front(waterWorld, site));
        }

        WorldEconomyTile[] riverTiles = FlatTiles(49, Biome.GrasslandSteppe);
        int riverSite = 24;
        int riverDirection = 4;
        int riverTile = HexGrid.Step(riverSite, riverDirection, 7, 7);
        riverTiles[riverTile] = riverTiles[riverTile] with { River = true, FreshWater = true };
        WorldEconomyInput riverWorld = MakeWorld(7, 7, riverTiles);
        Assert.Equal(riverDirection, SettlementCoreGrowth.Front(riverWorld, riverSite));

        WorldEconomyTile[] fallbackTiles = FlatTiles(49, Biome.GrasslandSteppe);
        WorldEconomyInput fallbackWorld = MakeWorld(7, 7, fallbackTiles);
        Assert.Equal(riverSite % 6, SettlementCoreGrowth.Front(fallbackWorld, riverSite));
    }

    [Fact]
    public void Assign_is_pure_and_a_usable_home_never_moves()
    {
        ushort usable = 0x0fff;
        ushort present = (ushort)((1 << (int)CoreDistrict.Ziggurat) | (1 << (int)CoreDistrict.Citadel) |
            (1 << (int)CoreDistrict.HomesC));
        Span<sbyte> first = stackalloc sbyte[(int)CoreDistrict.Count];
        Span<sbyte> scratch = stackalloc sbyte[(int)CoreDistrict.Count];
        ushort claimed = SettlementCoreGrowth.Assign(usable, present, first);
        _ = SettlementCoreGrowth.Assign(0x0555, 0x0125, scratch);
        ushort repeated = SettlementCoreGrowth.Assign(usable, present, scratch);
        Assert.Equal(claimed, repeated);
        Assert.Equal(first.ToArray(), scratch.ToArray());

        for (int presentMask = 0; presentMask < (1 << (int)CoreDistrict.Count); presentMask++)
            for (int usableMask = 0; usableMask < (1 << SettlementCoreGrowth.OuterPositions); usableMask++)
            {
                SettlementCoreGrowth.Assign((ushort)usableMask, (ushort)presentMask, first);
                foreach (byte district in SettlementCoreGrowth.Priority)
                    if ((presentMask & (1 << district)) != 0 && (usableMask & (1 << SettlementCoreGrowth.Home[district])) != 0 &&
                        first[district] != SettlementCoreGrowth.Home[district])
                        Assert.Fail($"present {presentMask:x3}, usable {usableMask:x3}: district {district} left its usable home");
            }
    }

    [Fact]
    public void Each_added_district_adds_one_hex_while_room()
    {
        Span<sbyte> positions = stackalloc sbyte[(int)CoreDistrict.Count];
        for (int presentMask = 0; presentMask < (1 << (int)CoreDistrict.Count); presentMask++)
            for (int usableMask = 0; usableMask < (1 << SettlementCoreGrowth.OuterPositions); usableMask++)
            {
                ushort claimed = SettlementCoreGrowth.Assign((ushort)usableMask, (ushort)presentMask, positions);
                if (CountBits(claimed) != Math.Min(CountBits((ushort)presentMask), CountBits((ushort)usableMask)))
                    Assert.Fail($"present {presentMask:x3}, usable {usableMask:x3}: not every available position was assigned");
            }
    }

    [Fact]
    public void Skips_water_mountain_river_deposit_and_offmap()
    {
        WorldEconomyTile[] tiles = FlatTiles(49, Biome.GrasslandSteppe);
        WorldDeposit[] deposits = new WorldDeposit[tiles.Length];
        tiles[1] = tiles[1] with { BiomeId = (int)Biome.Coast };
        tiles[2] = tiles[2] with { Lake = true, FreshWater = true };
        tiles[3] = tiles[3] with { BiomeId = (int)Biome.Mountain };
        tiles[4] = tiles[4] with { BiomeId = (int)Biome.Peak };
        tiles[5] = tiles[5] with { BiomeId = (int)Biome.Ice };
        tiles[6] = tiles[6] with { River = true, FreshWater = true };
        deposits[7] = new WorldDeposit(DepositKind.CopperOre, 1, 1);
        WorldEconomyInput world = MakeWorld(7, 7, tiles, deposits);
        foreach (int tile in new[] { 1, 2, 3, 4, 5, 6, 7 }) Assert.False(SettlementCoreGrowth.IsUsableStatic(world, tile));
        Assert.False(SettlementCoreGrowth.IsUsableStatic(world, -1));
        Assert.False(SettlementCoreGrowth.IsUsableStatic(world, tiles.Length));
        Assert.True(SettlementCoreGrowth.IsUsableStatic(world, 8));
        Assert.Equal(-1, SettlementCoreGrowth.PositionTile(0, 3, 0, 7, 7));
    }

    [Fact]
    public void Every_registry_structure_has_exactly_one_core_size_class()
    {
        string[][] classes =
        {
            new[] { "castle", "academy", "theatre_arena", "bathhouse", "ziggurat", "great_dome", "great_library" },
            new[] { "temple", "temple_tier2", "palace" },
            new[] { "granary", "potters_workshop", "forge", "market", "caravanserai", "school", "crane", "press", "longhouse", "hall" },
            new[] { "hearth", "lean_to", "hide_tent", "pit_house", "wattle_daub_house", "mudbrick_house", "storage_pit", "drying_rack", "kiln", "quern", "totem", "lookout_post", "signal_post", "shrine", "bakery", "smelter", "windlass_well", "aqueduct" },
            new[] { "thorn_barrier", "palisade", "stone_walls", "gatehouse" },
            new[] { "weir", "harbor" },
            new[] { "field", "orchard", "pasture", "terrace", "canal", "mining_camp", "mine_head", "marker", "seasonal_camp", "hillfort", "megalith", "great_lighthouse", "water_mill", "road", "bridge", "frontier_wall_segment" },
        };
        var classified = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string[] sizeClass in classes)
            foreach (string id in sizeClass)
                classified[id] = classified.TryGetValue(id, out int count) ? count + 1 : 1;

        ContentValue structures = TestPack.Pack.Root.Get("registries")!.Get("structures")!;
        string[] registry = structures.Items.Select(item => item.GetString("id")!).ToArray();
        Assert.Equal(60, registry.Length);
        Assert.Equal(registry.Length, classified.Count);
        foreach (string id in registry)
        {
            Assert.True(classified.TryGetValue(id, out int count), $"registry id {id} is not classified");
            Assert.Equal(1, count);
        }
        foreach (string id in classified.Keys) Assert.Contains(id, registry);
    }

    private static WorldEconomyTile[] FlatTiles(int count, Biome biome)
    {
        var tiles = new WorldEconomyTile[count];
        for (int i = 0; i < count; i++)
            tiles[i] = new WorldEconomyTile { BiomeId = (int)biome, ClimateId = (int)ClimateBand.Temperate, SoilFertility = Fix64.Half };
        return tiles;
    }

    private static WorldEconomyInput MakeWorld(int width, int height, WorldEconomyTile[] tiles, WorldDeposit[]? deposits = null)
    {
        var starts = new[] { 0, 1, 2, 3 };
        if (deposits is null) return new WorldEconomyInput(width, height, tiles, starts);
        return new WorldEconomyInput(width, height, tiles, starts, Array.Empty<WorldHerd>(), deposits);
    }

    private static int CountBits(ushort bits)
    {
        int count = 0;
        while (bits != 0) { count += bits & 1; bits >>= 1; }
        return count;
    }
}
