using System;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// Deep-sea fishing fleets (HNT-12, docs/MVP/38 §5.2, M13 WP-E6-6 slice 4). A harbor town of a civilization with HNT-12 fishes open water out to the edge of its
/// range at three times the labor rate (the row's offshore_fish_yield x3; the coast's boats give 1.5 today), from the same finite fish stock every other
/// fisher draws on, so the fleet moves food from the sea to the town and mints none. Discovery chapter only: elsewhere every method answers "no fleet".
/// The row's G-COD trade good and its salting recipe are not built yet (a trade-good slice with sugar).
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>The labor rate of a fleet fishing open water (the row's x3, replacing the coast boats' x1.5; provisional).</summary>
    private static readonly Fix64 FleetFishRate = Fix64.FromInt(3);
    /// <summary>The nearest tile (in hexes) a fleet works: the water beyond the one-tile shore the shore boats fish.</summary>
    private const int FleetMinDistance = 2;
    /// <summary>
    /// Offshore banks: the world's fish live only in rivers, lakes and the water within two tiles of land, so open water has none to take. In a Discovery match every
    /// other water tile regrows fish like a river does (4.8 a year, a stock of up to 7.2), which only a fleet can reach (provisional; Bronze, Iron and Fief do not
    /// have banks, so their stocks and hashes are unchanged).
    /// </summary>
    private static readonly Fix64 BankFishGrowthPerYear = Fix64.Parse("4.8");
    private static readonly Fix64 BankFishCap = Fix64.Parse("7.2");

    /// <summary>A water tile with no fish of its own: where a Discovery match grows an offshore bank.</summary>
    private static bool IsFishBank(WorldEconomyTile input) => input.FishGrowthPerYear <= Fix64.Zero && !IsLandBiome(input.BiomeId);

    /// <summary>Whether this town sends a fleet to sea: HNT-12, a harbor, the Discovery chapter.</summary>
    private bool OffshoreFleet(SettlementState s)
        => DiscoveryChain && HasProject(s.Civilization, "HNT-12") && HasHarbor(s.Id);

    /// <summary>Water tiles from two out to the edge of the settlement's range, shore fish or bank (geography only; the fleet rule is <see cref="OffshoreFleet"/>).</summary>
    private bool FleetWater(SettlementState s, int tile)
        => !IsLandBiome(_world.Tile(tile).BiomeId) &&
           HexGrid.Distance(s.SiteTile, tile, _world.Width) >= FleetMinDistance;

    internal bool FixtureFleetWater(int settlementIndex, int tile) => FleetWater(_settlements[settlementIndex], tile);

    /// <summary>Fixture: a land tile with water two or three tiles out in its range (the starts are inland, so a test moves a settlement here), and one such water tile.</summary>
    internal (int Site, int Water) FixtureFindFleetSite()
    {
        for (int t = 0; t < _world.TileCount; t++)
        {
            if (!IsLandBiome(_world.Tile(t).BiomeId)) continue;
            foreach (int w in _rangeTiles[t])
                if (!IsLandBiome(_world.Tile(w).BiomeId) && HexGrid.Distance(t, w, _world.Width) >= FleetMinDistance) return (t, w);
        }
        return (-1, -1);
    }

    internal Fix64 FixtureFishStock(int tile) => _fishStock[tile];

    internal bool FixtureIsFishBank(int tile) => DiscoveryChain && IsFishBank(_world.Tile(tile));

    internal bool FixtureOffshoreFleet(int settlementIndex) => OffshoreFleet(_settlements[settlementIndex]);

    internal bool FixtureCanFishTile(int settlementIndex, int tile) => CanFishTile(_settlements[settlementIndex], tile);

    internal Fix64 FixtureFishTileRate(int settlementIndex, int tile, Fix64 baseRate) => FishTileRate(_settlements[settlementIndex], tile, baseRate);

    internal ReadOnlySpan<int> FixtureRangeTiles(int settlementIndex) => RangeTiles(_settlements[settlementIndex].SiteTile);
}
