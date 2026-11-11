using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// The reserved settlement core (docs/MVP/31 §3, WP3): the site tile and every tile within <see cref="SettlementCoreRadius"/> of it
/// (the centre hex and ring 1, seven hexes) hold the village only. No field, orchard, Iron terrace or mining camp may stand on the core
/// of any living settlement, its own or a neighbour's. Gathering, hunting, fishing and wood still count across the whole radius-3 range,
/// and in-range mining of a deposit already under a village goes on (the grandfather rule of 31 §5.4; WP4 keeps new cores clean).
/// The core is derived from the site tile, so it adds no saved state. Everything here is integer hex distance in ascending settlement
/// order (D1 to D10).
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>The core radius used when the content pack has no <c>SettlementCoreRadius</c> constant.</summary>
    public const int DefaultSettlementCoreRadius = SettlementCoreRules.CoreRadius;

    /// <summary>Tiles from the site that belong to the reserved core: 1 means the centre hex and ring 1 (docs/MVP/31 §3.1).</summary>
    public int SettlementCoreRadius => _coreRadius;

    /// <summary>
    /// Whether <paramref name="tile"/> lies in the reserved core of any living settlement (one with people), so no plot or mining camp may
    /// use it. This is the single answer the planner, the build gates, the view and the tests share.
    /// </summary>
    public bool IsReservedCoreTile(int tile) => (uint)tile < (uint)_world.TileCount && InReservedCore(tile, null);

    /// <summary>
    /// <see cref="IsReservedCoreTile"/> for the economy's own placement code. <paramref name="living"/> is a settlement the caller knows is
    /// alive (the one placing a plot): its core is tested by distance alone and it is skipped in the scan of the others.
    /// </summary>
    private bool InReservedCore(int tile, SettlementState? living)
    {
        int width = _world.Width;
        if (living is not null && HexGrid.Distance(living.SiteTile, tile, width) <= _coreRadius) return true;
        foreach (SettlementState other in _settlements)
        {
            if (ReferenceEquals(other, living) || HexGrid.Distance(other.SiteTile, tile, width) > _coreRadius) continue;
            if (Population(other.Id) > 0) return true;
        }
        return false;
    }

    /// <summary>
    /// Whether a settlement may be founded on <paramref name="site"/> by its core (docs/MVP/31 §3.3, §7.1, WP4): no metal, salt or coal
    /// deposit lies within <see cref="SettlementCoreRadius"/> of it, discovered or not. Habitability and spacing are checked elsewhere.
    /// </summary>
    public bool IsCleanCoreSite(int site) => (uint)site < (uint)_world.TileCount && _coreClean[site];

    /// <summary>The lowest-index tile in the core of <paramref name="site"/> that holds a valuable deposit, or -1 when the core is clean.</summary>
    public int CoreDepositTile(int site)
    {
        if ((uint)site >= (uint)_world.TileCount || _coreClean[site]) return -1;
        int best = -1;
        foreach (int tile in BuildCoreTiles(site))
            if (SettlementCoreRules.IsValuableCoreDeposit(_world.Deposit(tile).Kind) && (best < 0 || tile < best)) best = tile;
        return best;
    }

    /// <summary>Precomputes the clean-core flag of every tile from the world's true deposits (they never move during a match).</summary>
    private static bool[] BuildCoreClean(WorldEconomyInput world, int[][] coreTiles)
    {
        var clean = new bool[world.TileCount];
        for (int site = 0; site < clean.Length; site++)
        {
            bool ok = true;
            foreach (int tile in coreTiles[site])
                if (SettlementCoreRules.IsValuableCoreDeposit(world.Deposit(tile).Kind)) { ok = false; break; }
            clean[site] = ok;
        }
        return clean;
    }

    private int[] BuildCoreTiles(int site)
    {
        var tiles = new List<int>(7);
        for (int t = 0; t < _world.TileCount; t++)
            if (HexGrid.Distance(site, t, _world.Width) <= _coreRadius) tiles.Add(t);
        return tiles.ToArray();
    }

    /// <summary>Whether a range tile of <paramref name="s"/> may carry a plot: outside its own core and every other living settlement's core.</summary>
    private bool IsPlotTile(SettlementState s, int tile) => !InReservedCore(tile, s);

    /// <summary>
    /// At the start of every season, releases each field, orchard and Iron terrace that stands on a living settlement's core (docs/MVP/31
    /// §3.3, §5.4): the plot's tile is freed and the structure count drops, so the planner re-places it outside the core when it next wants
    /// one. In a new game it only fires when a settlement is founded close enough that its core covers a neighbour's plot; it is what cleans
    /// up a save from before the rule. Ascending settlement id; the outcome does not depend on the order
    /// of a settlement's own plots.
    /// </summary>
    private void ReleaseCorePlots()
    {
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState s = _settlements[i];
            for (int j = s.FieldTiles.Count - 1; j >= 0; j--)
            {
                int tile = s.FieldTiles[j];
                if (!InReservedCore(tile, null)) continue;
                s.FieldTiles.RemoveAt(j);
                if (_fieldOwner[tile] == s.Id) _fieldOwner[tile] = -1;
                _tendedStandYieldBonus[tile] = false;
                s.Civic?.Canals.Remove(tile); // a canal lives on its field (docs/MVP/31 §5.1)
                if (s.Metal?.IronTerraces is { } terraces && terraces.Remove(tile))
                {
                    if (terraces.Count == 0) s.Metal.IronTerraces = null;
                    if (s.Metal.IronWorks is { } works)
                        works[(int)IronStructure.Terrace] = Math.Max(0, works[(int)IronStructure.Terrace] - 1);
                }
                else
                {
                    s.Structures[(int)Structure.Field] = Math.Max(0, s.Structures[(int)Structure.Field] - 1);
                }
            }
            for (int p = s.OrchardPlots.Count - 1; p >= 0; p--)
            {
                if (!InReservedCore(s.OrchardPlots[p].Tile, null)) continue;
                s.OrchardPlots.RemoveAt(p);
                s.Structures[(int)Structure.Orchard] = Math.Max(0, s.Structures[(int)Structure.Orchard] - 1);
            }
        }
    }

    /// <summary>Whether a free, cultivable plot tile is left in the settlement's range for one more field (the planner's ground test).</summary>
    private bool CanPlaceField(SettlementState s)
    {
        foreach (int tile in RangeTiles(s.SiteTile))
            if (_fieldOwner[tile] < 0 && _world.Tile(tile).FieldYieldPerYear > Fix64.Zero && !IsOrchardOccupied(tile) && IsPlotTile(s, tile))
                return true;
        return false;
    }

    // ---- fixtures (tests only) --------------------------------------------------------------------------

    /// <summary>
    /// Puts a plot on a tile without the core rule, as a save from before docs/MVP/31 could hold it: kind 0 a field, 1 an orchard, 2 an
    /// Iron terrace (counted as a finished terrace work).
    /// </summary>
    internal void FixtureForcePlot(int settlementIndex, int tile, int kind)
    {
        SettlementState s = _settlements[settlementIndex];
        if (_fieldOwner[tile] >= 0 || IsOrchardOccupied(tile)) throw new InvalidOperationException($"Tile {tile} already holds a plot.");
        if (kind == 1)
        {
            s.OrchardPlots.Add(new OrchardPlot { Tile = tile, AgeSeasons = 0 });
            s.Structures[(int)Structure.Orchard]++;
            return;
        }
        _fieldOwner[tile] = s.Id;
        s.FieldTiles.Add(tile);
        if (kind == 2)
        {
            MetalState m = EnsureMetal(s);
            (m.IronTerraces ??= new List<int>()).Add(tile);
            (m.IronWorks ??= new int[(int)IronStructure.Count])[(int)IronStructure.Terrace]++;
        }
        else s.Structures[(int)Structure.Field]++;
    }

    internal void FixtureReleaseCorePlots() => ReleaseCorePlots();
    internal bool FixtureCanPlaceField(int settlementIndex) => CanPlaceField(_settlements[settlementIndex]);
    internal bool FixtureCanPlaceOrchard(int settlementIndex) => CanPlaceOrchard(_settlements[settlementIndex]);
    internal bool FixturePlaceOrchard(int settlementIndex) => PlaceOrchard(_settlements[settlementIndex]);
    internal int FixtureNextTerraceTile(int settlementIndex) => NextTerraceTile(_settlements[settlementIndex]);
    internal int FixtureFieldTile(int settlementIndex, int index) => _settlements[settlementIndex].FieldTiles[index];
    internal int FixtureOrchardCount(int settlementIndex) => _settlements[settlementIndex].OrchardPlots.Count;
    internal int FixtureOrchardTile(int settlementIndex, int index) => _settlements[settlementIndex].OrchardPlots[index].Tile;
    internal int FixtureFieldStructures(int settlementIndex) => _settlements[settlementIndex].Structures[(int)Structure.Field];
    internal int FixtureOrchardStructures(int settlementIndex) => _settlements[settlementIndex].Structures[(int)Structure.Orchard];
    internal int FixtureTerraceWorks(int settlementIndex) => IronWorksCount(_settlements[settlementIndex], IronStructure.Terrace);
    internal int FixtureSiteTile(int settlementIndex) => _settlements[settlementIndex].SiteTile;
    internal int FixtureFieldOwner(int tile) => _fieldOwner[tile];
    internal bool FixtureFieldWanted(int settlementIndex) => FieldLimit(_settlements[settlementIndex]) > _settlements[settlementIndex].FieldTiles.Count && CanPlaceField(_settlements[settlementIndex]);
}
