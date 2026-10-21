using System;
using CivSim.Sim.Economy;
using CivSim.Sim.Randomness;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

/// <summary>
/// Island crossing (docs/MVP/21): a civilization that knows SEA-04 can sail where walking finds no route. Travel looks on the
/// walking graph first and falls back to the sea graph, so every walkable route keeps its length. A voyage can turn back
/// (shipwreck); its fate is recomputed from its key, never stored, so replays and reloads agree.
/// </summary>
public sealed partial class PoliticsSystem
{
    /// <summary>A water tile is open to boats while it is at most this many tiles from land: islands up to 22 tiles from a shore, never the 33-tile gap between continents.</summary>
    public const int OpenSeaReach = 11;
    /// <summary>Shipwreck risk in permille: base plus a step per open-sea tile on the route, capped.</summary>
    public const int VoyageBaseRiskPermille = 30, VoyageRiskPerSeaTile = 8, VoyageMaxRiskPermille = 400;
    /// <summary>Hex distance a daughter band may settle from its parent: on foot, and by boat.</summary>
    public const int LandSettleReach = 14, SailSettleReach = 24;
    /// <summary>A landmass this small is an island (12 §3.1).</summary>
    public const int IslandMaxTiles = 40;

    private static readonly ulong VoyageStream = Hash64.OfString("politics.voyage");

    private int[] _landDistance = Array.Empty<int>();
    private int[] _realm = Array.Empty<int>();         // continent, or an island's nearest continent; 0 on water
    private int[] _landmass = Array.Empty<int>();      // 0 water, 1.. landmass id
    private int[] _landmassSize = Array.Empty<int>();
    private int[]?[] _walkFrom = Array.Empty<int[]?>();
    private int[]?[] _sailFrom = Array.Empty<int[]?>();

    private void InitVoyages()
    {
        int n = _tileArray.Length;
        _landDistance = HexGrid.LandDistances(_tileArray, _width, _height);
        _realm = HexGrid.LandRealms(_tileArray, _width, _height, IslandMaxTiles);
        _landmass = new int[n];
        var sizes = new System.Collections.Generic.List<int> { 0 };
        var queue = new int[n];
        Span<int> neighbors = stackalloc int[6];
        for (int t = 0; t < n; t++)
        {
            if (!_tileArray[t].IsLand || _landmass[t] != 0) continue;
            int id = sizes.Count, head = 0, tail = 0, size = 0;
            _landmass[t] = id; queue[tail++] = t;
            while (head < tail)
            {
                int tile = queue[head++]; size++;
                int count = HexGrid.Neighbors(tile, _width, _height, neighbors);
                for (int k = 0; k < count; k++)
                    if (_tileArray[neighbors[k]].IsLand && _landmass[neighbors[k]] == 0) { _landmass[neighbors[k]] = id; queue[tail++] = neighbors[k]; }
            }
            sizes.Add(size);
        }
        _landmassSize = sizes.ToArray();
        _walkFrom = new int[]?[n];
        _sailFrom = new int[]?[n];
    }

    /// <summary>Whether this civilization's people know the outrigger (SEA-04) and so can cross open water.</summary>
    public bool CanSail(int civ) => _p.Sea04 >= 0 && (uint)civ < (uint)_civs && _research.IsDone(civ, _p.Sea04);

    private int[] WalkFrom(int tile) => _walkFrom[tile] ??= HexGrid.WalkingDistancesFrom(_width, _height, _passable, tile);

    private int[] SailFrom(int tile) => _sailFrom[tile] ??= HexGrid.WalkingCostsFrom(_tileArray, _width, _height, tile, int.MaxValue, _landDistance, OpenSeaReach, _realm);

    /// <summary>Tiles from one tile to another for this civilization: on foot, else by sea if it can sail; int.MaxValue when there is no way.</summary>
    private int Travel(int civ, int from, int to)
    {
        int walk = WalkFrom(from)[to];
        if (walk != int.MaxValue || !CanSail(civ)) return walk;
        int cost = SailFrom(from)[to];
        return cost == int.MaxValue ? int.MaxValue : (cost + 9) / 10;
    }

    /// <summary>Whether people of <paramref name="civ"/> can get from one tile to another at all.</summary>
    public bool CanReach(int civ, int fromTile, int toTile) => Travel(civ, fromTile, toTile) != int.MaxValue;

    /// <summary>Seasons the trip takes (at least one), or int.MaxValue when there is no way.</summary>
    public int TravelSeasons(int civ, int fromTile, int toTile)
    {
        int tiles = Travel(civ, fromTile, toTile);
        return tiles == int.MaxValue ? int.MaxValue : Math.Max(1, SeasonsFor(tiles));
    }

    /// <summary>Open-sea tiles on the trip (0 when it can be walked, -1 when there is no way).</summary>
    public int SeaTilesOnRoute(int civ, int fromTile, int toTile)
    {
        if (WalkFrom(fromTile)[toTile] != int.MaxValue) return 0;
        if (!CanSail(civ)) return -1;
        int[] cost = SailFrom(fromTile);
        if (cost[toTile] == int.MaxValue) return -1;
        Span<int> neighbors = stackalloc int[6];
        int at = toTile, sea = 0;
        for (int guard = 0; guard < 4096; guard++)
        {
            if (!_tileArray[at].IsLand && !_passable[at]) sea++;
            if (at == fromTile) break;
            int count = HexGrid.Neighbors(at, _width, _height, neighbors);
            int next = -1, best = cost[at];
            for (int k = 0; k < count; k++)
                if (cost[neighbors[k]] < best) { best = cost[neighbors[k]]; next = neighbors[k]; }
            if (next < 0) break;
            at = next;
        }
        return sea;
    }

    /// <summary>Shipwreck risk of a trip in permille: 0 on foot, else base plus a step per open-sea tile.</summary>
    public int VoyageRiskPermille(int civ, int fromTile, int toTile)
    {
        int sea = SeaTilesOnRoute(civ, fromTile, toTile);
        return sea <= 0 ? 0 : Math.Min(VoyageMaxRiskPermille, VoyageBaseRiskPermille + VoyageRiskPerSeaTile * sea);
    }

    private bool RollVoyageLost(int risk, ulong entity, int season)
    {
        if (risk <= 0) return false;
        Pcg32 roll = Pcg32.ForStream(_seed, VoyageStream, entity, season);
        return roll.NextBelow(1000) < (uint)risk;
    }

    /// <summary>
    /// An outbound party that crossed open water may turn back at the shore it left: goods return home and nothing is
    /// resolved. The return leg of a barter trip is not rolled.
    /// </summary>
    private bool TurnedBackAtSea(Expedition e)
    {
        if (e.Kind == ExpeditionKind.Barter && e.Leg == 1) return false;
        if ((uint)e.FromSettlement >= (uint)_sCount || (uint)e.ToSettlement >= (uint)_sCount) return false;
        int from = _sTile[e.FromSettlement], to = _sTile[e.ToSettlement];
        int sea = SeaTilesOnRoute(e.Civ, from, to);
        if (sea <= 0) return false;
        int risk = Math.Min(VoyageMaxRiskPermille, VoyageBaseRiskPermille + VoyageRiskPerSeaTile * sea);
        ulong entity = (ulong)(e.FromSettlement * 4099L + e.ToSettlement * 7L + (int)e.Kind);
        if (!RollVoyageLost(risk, entity, _season)) return false;
        _events.Add(new PoliticsEvent(_tick, (byte)e.Civ, PoliticsEventKind.VoyageLost, e.TargetCiv, sea, (int)e.Kind));
        if (e.Kind is ExpeditionKind.Gift or ExpeditionKind.Barter) _economy.GiveGoods(e.FromSettlement, e.Value);
        return true;
    }

    /// <summary>The shortest sea route in tiles between two civilizations' villages within <paramref name="limit"/>, or int.MaxValue.</summary>
    private int SailTilesBetween(int a, int b, int limit)
    {
        int best = int.MaxValue;
        var targets = PresencePoints(b);
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] != a || _free[s]) continue;
            int[] cost = HexGrid.WalkingCostsFrom(_tileArray, _width, _height, _sTile[s], limit * 10, _landDistance, OpenSeaReach, _realm);
            foreach (int tile in targets)
                if (cost[tile] != int.MaxValue) best = Math.Min(best, (cost[tile] + 9) / 10);
        }
        return best;
    }

    // ---- IFissionSiteAdvisor: reach, reachability and the voyage --------------------------

    public int SettleReach(int civ) => CanSail(civ) ? SailSettleReach : LandSettleReach;

    public bool Reachable(int civ, int fromTile, int toTile) => CanReach(civ, fromTile, toTile);

    /// <summary>A daughter band's voyage rolls once as it sets out; if it is lost the band stays (and may try again next season).</summary>
    public bool VoyageLost(int civ, int fromTile, int toTile, int season)
    {
        int sea = SeaTilesOnRoute(civ, fromTile, toTile);
        if (sea <= 0) return false;
        int risk = Math.Min(VoyageMaxRiskPermille, VoyageBaseRiskPermille + VoyageRiskPerSeaTile * sea);
        ulong entity = (ulong)(fromTile * 8191L + toTile) * 31UL + 4UL;
        if (!RollVoyageLost(risk, entity, season)) return false;
        _events.Add(new PoliticsEvent(_economy.Tick, (byte)civ, PoliticsEventKind.VoyageLost, -1, sea, 4));
        return true;
    }

    /// <summary>Whether the tile lies on an island (a landmass of at most <see cref="IslandMaxTiles"/> tiles).</summary>
    public bool IsIsland(int tile) => (uint)tile < (uint)_landmass.Length && _landmass[tile] > 0 && _landmassSize[_landmass[tile]] <= IslandMaxTiles;

    private bool HasVillageOnLandmass(int civ, int tile)
    {
        int id = _landmass[tile];
        for (int s = 0; s < _sCount; s++)
            if (_sCiv[s] == civ && !_free[s] && _landmass[_sTile[s]] == id) return true;
        return false;
    }
}
