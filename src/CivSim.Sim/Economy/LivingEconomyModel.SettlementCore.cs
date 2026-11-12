using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// The reserved settlement core (docs/MVP/31 and 31b): the site and ring 1, plus outer districts in ring 2, hold the village only.
/// No field, orchard, Iron terrace, mining camp or new seasonal camp may stand on a claimed hex of any living settlement. Gathering,
/// hunting, fishing and wood still count across the whole radius-3 range, and in-range mining of a grandfathered deposit continues.
/// Claims are derived from saved buildings, city population and immutable world facts, so they add no saved state. Everything here is
/// integer hex distance in ascending settlement order (D1 to D10).
/// </summary>
public sealed partial class LivingEconomyModel
{
    private int[] _settlementPopulationScratch = Array.Empty<int>();

    /// <summary>The core radius used when the content pack has no <c>SettlementCoreRadius</c> constant.</summary>
    public const int DefaultSettlementCoreRadius = SettlementCoreRules.CoreRadius;

    /// <summary>Tiles from the site that belong to the reserved core: 1 means the centre hex and ring 1 (docs/MVP/31 §3.1).</summary>
    public int SettlementCoreRadius => _coreRadius;

    private int SettlementAtSite(int site)
    {
        for (int i = 0; i < _settlements.Count; i++)
            if (_settlements[i].SiteTile == site) return i;
        return -1;
    }

    private int FindAbandonedSettlement(int civ, int site)
    {
        int settlement = SettlementAtSite(site);
        if (settlement < 0) return -1;
        SettlementState state = _settlements[settlement];
        return state.Civilization == civ && !IsFreeFolk(state) && Population(state.Id) == 0 ? settlement : -1;
    }

    private int[] FillSettlementPopulationScratch()
    {
        int count = _settlements.Count;
        if (_settlementPopulationScratch.Length < count) Array.Resize(ref _settlementPopulationScratch, count);
        Array.Clear(_settlementPopulationScratch, 0, count);
        for (int i = 0; i < _figures.Count; i++)
        {
            int settlement = _figures[i].SettlementId;
            if ((uint)settlement < (uint)count) _settlementPopulationScratch[settlement]++;
        }
        return _settlementPopulationScratch;
    }

    /// <summary>
    /// Whether <paramref name="tile"/> lies in the reserved core of any living settlement (one with people), so no plot or mining camp may
    /// use it. This is the single answer the planner, the build gates, the view and the tests share.
    /// </summary>
    public bool IsReservedCoreTile(int tile) => tile >= 0 && tile < _world.TileCount && InReservedCore(tile, null);

    /// <summary>
    /// <see cref="IsReservedCoreTile"/> for the economy's own placement code. <paramref name="living"/> is a settlement the caller knows is
    /// alive (the one placing a plot): its core is tested by distance alone and it is skipped in the scan of the others.
    /// </summary>
    private bool InReservedCore(int tile, SettlementState? living)
    {
        int width = _world.Width;
        if (living is not null && HexGrid.Distance(living.SiteTile, tile, width) <= _coreRadius) return true;
        List<int>? candidates = _settlementSitesWithinTwoByTile[tile];
        if (candidates is null) return false;
        Span<int> claimTiles = stackalloc int[(int)CoreDistrict.Count];
        Span<sbyte> positions = stackalloc sbyte[(int)CoreDistrict.Count];
        foreach (int settlementIndex in candidates)
        {
            SettlementState other = _settlements[settlementIndex];
            if (Population(other.Id) <= 0) continue;
            int distance = HexGrid.Distance(other.SiteTile, tile, width);
            if (distance <= _coreRadius) return true;
            if (distance != 2 || CoreDistrictMask(other) == 0) continue;
            OuterClaims(other, claimTiles, positions);
            for (int district = 0; district < (int)CoreDistrict.Count; district++)
                if (claimTiles[district] == tile) return true;
        }
        return false;
    }

    /// <summary>Ring-two claims, returned in district-priority order. Present but unhoused districts have tile -1 and position 255.</summary>
    public int OuterCoreHexes(int settlementIndex, Span<int> tiles, Span<byte> districts, Span<byte> positions)
    {
        if (settlementIndex < 0 || settlementIndex >= _settlements.Count) throw new ArgumentOutOfRangeException(nameof(settlementIndex));
        RequireOuterOutput(tiles.Length, districts.Length, positions.Length);
        Span<int> byDistrict = stackalloc int[(int)CoreDistrict.Count];
        Span<sbyte> positionByDistrict = stackalloc sbyte[(int)CoreDistrict.Count];
        SettlementState settlement = _settlements[settlementIndex];
        OuterClaims(settlement, byDistrict, positionByDistrict);
        return CopyOuterRows(CoreDistrictMask(settlement), byDistrict, positionByDistrict, tiles, districts, positions);
    }

    /// <summary>The static gate/front direction (0..5) for a settlement row.</summary>
    public int CoreFront(int settlementIndex)
    {
        if (settlementIndex < 0 || settlementIndex >= _settlements.Count) throw new ArgumentOutOfRangeException(nameof(settlementIndex));
        return _coreFront[_settlements[settlementIndex].SiteTile];
    }

    /// <summary>Runs the outer-hex rule for a gallery site without changing the match state.</summary>
    public int PreviewOuterCore(int site, ushort districtMask, Span<int> tiles, Span<byte> districts, Span<byte> positions)
    {
        if (site < 0 || site >= _world.TileCount) throw new ArgumentOutOfRangeException(nameof(site));
        RequireOuterOutput(tiles.Length, districts.Length, positions.Length);
        SettlementState? represented = null;
        int settlementId = int.MaxValue;
        foreach (SettlementState other in _settlements)
            if (other.SiteTile == site && Population(other.Id) > 0)
            {
                represented = other;
                settlementId = other.Id;
                break;
            }
        Span<int> byDistrict = stackalloc int[(int)CoreDistrict.Count];
        Span<sbyte> positionByDistrict = stackalloc sbyte[(int)CoreDistrict.Count];
        OuterClaims(site, settlementId, districtMask, represented, byDistrict, positionByDistrict);
        return CopyOuterRows(districtMask, byDistrict, positionByDistrict, tiles, districts, positions);
    }

    private static void RequireOuterOutput(int tiles, int districts, int positions)
    {
        int count = (int)CoreDistrict.Count;
        if (tiles < count || districts < count || positions < count)
            throw new ArgumentException($"Outer-core output spans must each hold at least {count} rows.");
    }

    private static int CopyOuterRows(ushort mask, Span<int> byDistrict, Span<sbyte> positionByDistrict,
        Span<int> tiles, Span<byte> districts, Span<byte> positions)
    {
        int count = 0;
        foreach (byte district in SettlementCoreGrowth.Priority)
        {
            if ((mask & (1 << district)) == 0) continue;
            tiles[count] = byDistrict[district];
            districts[count] = district;
            positions[count] = positionByDistrict[district] < 0 ? byte.MaxValue : (byte)positionByDistrict[district];
            count++;
        }
        return count;
    }

    private ushort CoreDistrictMask(SettlementState s)
    {
        if (Population(s.Id) <= 0) return 0;
        ushort mask = 0;
        CivicState? civic = s.Civic;
        if (civic is not null)
        {
            if (civic.Ziggurats > 0 || civic.BuildKind == CivicBuild.Ziggurat) Add(ref mask, CoreDistrict.Ziggurat);
            if (civic.Tier == (byte)SettlementTier.City)
            {
                int people = Math.Max(Population(s.Id), civic.PeopleLastYear);
                if (people >= _coreHomesFirst) Add(ref mask, CoreDistrict.HomesC);
                if (people >= _coreHomesFirst + _coreHomesStep) Add(ref mask, CoreDistrict.HomesD);
            }
        }
        if (HasIronDistrict(s, IronStructure.GreatDome)) Add(ref mask, CoreDistrict.GreatDome);
        if (HasIronDistrict(s, IronStructure.GreatLibrary)) Add(ref mask, CoreDistrict.GreatLibrary);
        if (HasIronDistrict(s, IronStructure.Castle)) Add(ref mask, CoreDistrict.Citadel);
        if (HasIronDistrict(s, IronStructure.Academy)) Add(ref mask, CoreDistrict.Learning);
        if (HasIronDistrict(s, IronStructure.Theatre)) Add(ref mask, CoreDistrict.Civic);
        if (HasIronDistrict(s, IronStructure.Bathhouse)) Add(ref mask, CoreDistrict.Health);
        return mask;
    }

    private static void Add(ref ushort mask, CoreDistrict district) => mask |= (ushort)(1 << (int)district);

    private static bool HasIronDistrict(SettlementState s, IronStructure structure)
    {
        if (s.Metal is not { } metal) return false;
        int index = (int)structure;
        return metal.IronBuildKind == index || (metal.IronWorks is { } works && index < works.Length && works[index] > 0);
    }

    private int OuterClaims(SettlementState s, Span<int> tiles, Span<sbyte> positionOf)
        => OuterClaims(s.SiteTile, s.Id, CoreDistrictMask(s), s, tiles, positionOf);

    private int OuterClaims(int site, int settlementId, ushort districtMask, SettlementState? represented,
        Span<int> tiles, Span<sbyte> positionOf)
    {
        int districtCount = (int)CoreDistrict.Count;
        if (tiles.Length < districtCount || positionOf.Length < districtCount)
            throw new ArgumentException("Outer-claim scratch spans are too small.");
        tiles[..districtCount].Fill(-1);
        positionOf[..districtCount].Fill(-1);
        districtMask &= (ushort)((1 << districtCount) - 1);
        if (districtMask == 0) return 0;

        ushort usable = 0;
        int front = _coreFront[site];
        for (int position = 0; position < SettlementCoreGrowth.OuterPositions; position++)
        {
            int tile = SettlementCoreGrowth.PositionTile(site, front, position, _world.Width, _world.Height);
            if (tile < 0 || !_outerUsable[tile] || IsOuterGuarded(tile, settlementId, represented)) continue;
            usable |= (ushort)(1 << position);
        }

        ushort claimed = SettlementCoreGrowth.Assign(usable, districtMask, positionOf);
        int count = 0;
        foreach (byte district in SettlementCoreGrowth.Priority)
        {
            int position = positionOf[district];
            if (position < 0) continue;
            tiles[district] = SettlementCoreGrowth.PositionTile(site, front, position, _world.Width, _world.Height);
            count++;
        }
        _ = claimed;
        return count;
    }

    private void IndexSettlementSite(SettlementState settlement)
    {
        int site = settlement.SiteTile;
        foreach (int tile in _rangeTiles[site])
        {
            if (HexGrid.Distance(site, tile, _world.Width) > 2) continue;
            (_settlementSitesWithinTwoByTile[tile] ??= new List<int>(2)).Add(settlement.Id);
        }
    }

    private void RebuildSettlementSiteIndex()
    {
        Array.Clear(_settlementSitesWithinTwoByTile, 0, _settlementSitesWithinTwoByTile.Length);
        foreach (SettlementState settlement in _settlements) IndexSettlementSite(settlement);
    }

    private bool IsOuterGuarded(int tile, int settlementId, SettlementState? represented)
    {
        List<int>? candidates = _settlementSitesWithinTwoByTile[tile];
        if (candidates is null) return false;
        foreach (int settlementIndex in candidates)
        {
            SettlementState other = _settlements[settlementIndex];
            if (ReferenceEquals(other, represented) || Population(other.Id) <= 0) continue;
            int distance = HexGrid.Distance(other.SiteTile, tile, _world.Width);
            if (distance <= _coreRadius || (other.Id < settlementId && distance <= 2)) return true;
        }
        return false;
    }

    private static byte[] BuildCoreFront(WorldEconomyInput world)
    {
        var front = new byte[world.TileCount];
        for (int site = 0; site < front.Length; site++) front[site] = (byte)SettlementCoreGrowth.Front(world, site);
        return front;
    }

    private static bool[] BuildOuterUsable(WorldEconomyInput world)
    {
        var usable = new bool[world.TileCount];
        for (int tile = 0; tile < usable.Length; tile++) usable[tile] = SettlementCoreGrowth.IsUsableStatic(world, tile);
        return usable;
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
