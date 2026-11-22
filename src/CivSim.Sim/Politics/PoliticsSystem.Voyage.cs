using System;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
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
    /// <summary>A water tile is open to boats while it is at most this many tiles from land (any land: islands are stepping stones, so the 33-tile gap between the continents is crossed in two or three legs).</summary>
    public const int OpenSeaReach = 11;
    /// <summary>
    /// SEA-12 caravels and ocean voyages (docs/MVP/38 §5.2, Discovery only): a water tile is open to a ship while it is at most this many tiles from land, so one leg
    /// crosses the channel (it needs 15 to 19; 20 works in every world). The same travel and loss machinery carries it. Provisional.
    /// </summary>
    public const int OceanSeaReach = 24;
    /// <summary>Extra shipwreck risk in permille for each deep-ocean tile on a route (one farther from land than <see cref="OpenSeaReach"/>, which only SEA-12 ships can cross; provisional).</summary>
    public const int OceanDeepRiskPerTile = 12;
    /// <summary>WAY-10 (marine chronometer) leaves this many parts in ten of the shipwreck risk, after WAY-08 (provisional).</summary>
    public const int ChronometerRiskTenths = 3;
    /// <summary>A caravan escorted by a crewed galleon keeps this many parts in ten of its shipwreck risk (SEA-13 convoy_loss x0.6; provisional).</summary>
    public const int ConvoyRiskTenths = 6;
    /// <summary>Shipwreck risk in permille: base plus a step per open-sea tile on the route, capped.</summary>
    public const int VoyageBaseRiskPermille = 30, VoyageRiskPerSeaTile = 8, VoyageMaxRiskPermille = 400;
    /// <summary>Hex distance a daughter band may settle from its parent: on foot, and by boat.</summary>
    public const int LandSettleReach = 14, SailSettleReach = 24;
    /// <summary>The sea settle reach of a civilization with TER-11 and SEA-12: far enough to found across the 31 to 39 tile channel (provisional).</summary>
    public const int ColonySettleReach = 40;
    /// <summary>A landmass this small is an island (12 §3.1).</summary>
    public const int IslandMaxTiles = 40;

    private static readonly ulong VoyageStream = Hash64.OfString("politics.voyage");

    private int[] _landDistance = Array.Empty<int>();
    private int[] _landmass = Array.Empty<int>();      // 0 water, 1.. landmass id
    private int[] _landmassSize = Array.Empty<int>();
    private int[]?[] _walkFrom = Array.Empty<int[]?>();
    private int[]?[] _sailFrom = Array.Empty<int[]?>();
    private int[]?[] _oceanFrom = Array.Empty<int[]?>();
    private int _sea12 = -1;
    private int _way10 = -1;
    private int _ter11 = -1;
    private int _inq07 = -1;
    private int _trd10 = -1;
    private int _way08 = -1;
    private int _way09 = -1;
    private int _sea11 = -1;

    private void InitVoyages()
    {
        _way08 = _research.Catalog.IndexOf("WAY-08");
        _way09 = _research.Catalog.IndexOf("WAY-09");
        _sea11 = _research.Catalog.IndexOf("SEA-11");
        _sea12 = _research.Catalog.IndexOf("SEA-12");
        _way10 = _research.Catalog.IndexOf("WAY-10");
        _ter11 = _research.Catalog.IndexOf("TER-11");
        _inq07 = _research.Catalog.IndexOf("INQ-07");
        _trd10 = _research.Catalog.IndexOf("TRD-10");
        int n = _tileArray.Length;
        _landDistance = HexGrid.LandDistances(_tileArray, _width, _height);
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
        _oceanFrom = new int[]?[n];
    }

    /// <summary>Whether this civilization's people know the outrigger (SEA-04) and so can cross open water.</summary>
    public bool CanSail(int civ)
    {
        if (_p.Sea04 < 0 || (uint)civ >= (uint)_civs || !_research.IsDone(civ, _p.Sea04)) return false;
        if (!_economy.FiefChain) return true;
        int key6 = _catalog.IndexOf("KEY-6");
        return key6 >= 0 && _research.IsDone(civ, key6);
    }

    private int[] WalkFrom(int tile) => _walkFrom[tile] ??= HexGrid.WalkingDistancesFrom(_width, _height, _passable, tile);

    private int[] SailFrom(int tile) => _sailFrom[tile] ??= HexGrid.WalkingCostsFrom(_tileArray, _width, _height, tile, int.MaxValue, _landDistance, OpenSeaReach);

    private int[] OceanFrom(int tile) => _oceanFrom[tile] ??= HexGrid.WalkingCostsFrom(_tileArray, _width, _height, tile, int.MaxValue, _landDistance, OceanSeaReach);

    /// <summary>Whether this civilization's ships cross the deep ocean in one leg: SEA-12, in the Discovery chapter only.</summary>
    public bool HasOceanReach(int civ)
        => _economy.DiscoveryChain && _sea12 >= 0 && (uint)civ < (uint)_civs && _research.IsDone(civ, _sea12);

    /// <summary>The sea graph this civilization sails on: the outrigger's coast-hugging reach, or SEA-12's open-ocean reach.</summary>
    private int[] SailFrom(int civ, int tile) => HasOceanReach(civ) ? OceanFrom(tile) : SailFrom(tile);

    /// <summary>Tiles from one tile to another for this civilization: on foot, else by sea if it can sail; int.MaxValue when there is no way.</summary>
    private int Travel(int civ, int from, int to)
    {
        int walk = WalkFrom(from)[to];
        if (walk != int.MaxValue || !CanSail(civ)) return walk;
        int cost = SailFrom(civ, from)[to];
        if (cost == int.MaxValue) return int.MaxValue;
        int sailingTiles = (cost + 9) / 10;
        // Modifiers apply only when this route actually crosses water. Combine the
        // speed fractions before rounding so their composition is deterministic.
        if (_economy.FiefChain && (uint)civ < (uint)_civs && SeaTilesOnRoute(civ, from, to) > 0)
        {
            long distanceNumerator = 1, distanceDenominator = 1;
            if (_way09 >= 0 && _research.IsDone(civ, _way09)) { distanceNumerator *= 10; distanceDenominator *= 13; }
            if (_sea11 >= 0 && _research.IsDone(civ, _sea11)) { distanceNumerator *= 10; distanceDenominator *= 14; }
            // TRD-10 (Discovery): a joint-stock venture, with several harbor towns pooling their goods into one voyage ledger, sails x1.5 faster (the row's venture_speed).
            if (_economy.DiscoveryChain && _trd10 >= 0 && _economy.JointStockOpen(civ)) { distanceNumerator *= 2; distanceDenominator *= 3; }
            if (distanceNumerator != distanceDenominator)
                sailingTiles = Math.Max(1, (int)(((long)sailingTiles * distanceNumerator + distanceDenominator - 1) / distanceDenominator));
        }
        return sailingTiles;
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
    public int SeaTilesOnRoute(int civ, int fromTile, int toTile) => SeaRoute(civ, fromTile, toTile, out _);

    /// <summary>Deep-ocean tiles on the trip: open-sea tiles farther from land than the outrigger's reach, which only a SEA-12 ship can cross (0 on foot or when none).</summary>
    public int DeepSeaTilesOnRoute(int civ, int fromTile, int toTile)
    {
        SeaRoute(civ, fromTile, toTile, out int deep);
        return deep;
    }

    private int SeaRoute(int civ, int fromTile, int toTile, out int deep)
    {
        deep = 0;
        if (WalkFrom(fromTile)[toTile] != int.MaxValue) return 0;
        if (!CanSail(civ)) return -1;
        int[] cost = SailFrom(civ, fromTile);
        if (cost[toTile] == int.MaxValue) return -1;
        Span<int> neighbors = stackalloc int[6];
        int at = toTile, sea = 0;
        for (int guard = 0; guard < 4096; guard++)
        {
            if (!_tileArray[at].IsLand && !_passable[at])
            {
                sea++;
                if (_landDistance[at] > OpenSeaReach) deep++;
            }
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
        int sea = SeaRoute(civ, fromTile, toTile, out int deep);
        return VoyageRiskForSeaTiles(civ, sea, deep);
    }

    /// <summary>Base open-sea risk, with Fief's WAY-08 navigation reducing ship-loss risk by half.</summary>
    private int VoyageRiskForSeaTiles(int civ, int sea, int deep = 0)
    {
        if (sea <= 0) return 0;
        int risk = Math.Min(VoyageMaxRiskPermille, VoyageBaseRiskPermille + VoyageRiskPerSeaTile * sea + OceanDeepRiskPerTile * deep);
        if (_economy.FiefChain && (uint)civ < (uint)_civs && _way08 >= 0 && _research.IsDone(civ, _way08))
            risk /= 2;
        // WAY-10 (Discovery): a chronometer solves longitude and cuts the loss to three parts in ten. The benefit needs the instrument in a harbor town (the planner
        // wants one in each); the navigator's staffing is not modelled.
        if (_economy.DiscoveryChain && (uint)civ < (uint)_civs && _way10 >= 0 && _research.IsDone(civ, _way10) && _economy.ChronometerHeld(civ))
            risk = risk * ChronometerRiskTenths / 10;
        // INQ-07 (Discovery): the telescope makes sea routes 5% more reliable (the row's sea_route_reliability x1.05), taken off the loss, once one is in a harbor town.
        if (_economy.DiscoveryChain && (uint)civ < (uint)_civs && _inq07 >= 0 && _research.IsDone(civ, _inq07) && _economy.TelescopeHeld(civ))
            risk = risk * 100 / 105;
        // TRD-11 (Discovery): insurance spreads a loss across the realm's harbor towns, so a staffed exchange house halves it (voyage_loss x0.5; mints nothing).
        if (_economy.DiscoveryChain && (uint)civ < (uint)_civs && _economy.InsuranceOpen(civ)) risk /= 2;
        return risk;
    }

    internal bool FixtureRollVoyageLost(int risk, ulong entity, int season, int civ) => RollVoyageLost(risk, entity, season, civ);

    private bool RollVoyageLost(int risk, ulong entity, int season, int civ = -1)
    {
        if (civ >= 0 && risk > 0 && _economy.HoldsWonder(civ, "great_lighthouse")) risk /= 2; // the Great Lighthouse (SEA-08): shipwreck risk x0.5 civilization-wide
        if (risk <= 0) return false;
        Pcg32 roll = Pcg32.ForStream(_seed, VoyageStream, entity, season);
        return roll.NextBelow(1000) < (uint)risk;
    }

    /// <summary>
    /// An outbound party that crossed open water may turn back at the shore it left: goods return home and nothing is
    /// resolved. The return leg of a barter trip is not rolled.
    /// </summary>
    private int ConvoyRisk(int risk, int fromSettlement)
        => _economy.DiscoveryChain && _economy.CrewedGalleon(fromSettlement) ? risk * ConvoyRiskTenths / 10 : risk;

    internal int FixtureConvoyRisk(int risk, int fromSettlement) => ConvoyRisk(risk, fromSettlement);

    private bool TurnedBackAtSea(Expedition e)
    {
        if (e.Kind == ExpeditionKind.Barter && e.Leg == 1) return false;
        if (e.FromSettlement < 0 || e.ToSettlement < 0 || e.FromSettlement >= _sCount || e.ToSettlement >= _sCount) return false;
        int from = _sTile[e.FromSettlement], to = _sTile[e.ToSettlement];
        int sea = SeaRoute(e.Civ, from, to, out int deep);
        if (sea <= 0) return false;
        int risk = VoyageRiskForSeaTiles(e.Civ, sea, deep);
        // SEA-13: a trade fleet under escort of a crewed galleon at its home harbor loses 40% less (convoy_loss x0.6, Discovery only).
        if (e.Kind == ExpeditionKind.Caravan) risk = ConvoyRisk(risk, e.FromSettlement);
        ulong entity = (ulong)(e.FromSettlement * 4099L + e.ToSettlement * 7L + (int)e.Kind);
        bool turnedBack = RollVoyageLost(risk, entity, _season, e.Civ);
        if (e.Kind == ExpeditionKind.Caravan)
        {
            Compact? compact = CompactById(e.CompactId);
            TraceSea10Route(turnedBack ? "voyage_turnback" : "voyage_survived", compact, e,
                e.FromSettlement, e.ToSettlement, turnedBack ? "cargo_returned_to_seller" : "voyage_completed", e.Cargo, e.Cargo, e.Cargo,
                returned: turnedBack ? e.Cargo : Fix64.Zero);
        }
        if (!turnedBack) return false;
        _events.Add(new PoliticsEvent(_tick, (byte)e.Civ, PoliticsEventKind.VoyageLost, e.TargetCiv, sea, (int)e.Kind));
        if (e.Kind is ExpeditionKind.Gift or ExpeditionKind.Barter) _economy.GiveGoods(e.FromSettlement, e.Value);
        else if (e.Kind == ExpeditionKind.Caravan) Unload(e.FromSettlement, e.Good, e.Cargo);
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
            int[] cost = HexGrid.WalkingCostsFrom(_tileArray, _width, _height, _sTile[s], limit * 10, _landDistance, HasOceanReach(a) ? OceanSeaReach : OpenSeaReach);
            foreach (int tile in targets)
                if (cost[tile] != int.MaxValue) best = Math.Min(best, (cost[tile] + 9) / 10);
        }
        return best;
    }

    // ---- IFissionSiteAdvisor: reach, reachability and the voyage --------------------------

    public int SettleReach(int civ) => CanSail(civ) ? HasColonyReach(civ) ? ColonySettleReach : SailSettleReach : LandSettleReach;

    private bool[] _overseas = Array.Empty<bool>();
    private int _overseasSeason = -1;

    /// <summary>
    /// Whether this civilization has met a civilization whose home landmass (its oldest live settlement's) is a different one: the contact across an ocean the
    /// Columbian Exchange needs (FOR-10). Worked out once a season for every civilization; it follows contact, never knowledge of the map.
    /// </summary>
    public bool OverseasExchange(int civ)
    {
        if (!_economy.DiscoveryChain || (uint)civ >= (uint)_civs) return false;
        if (_overseasSeason != _season || _overseas.Length != _civs)
        {
            _overseas = new bool[_civs];
            var home = new int[_civs];
            Array.Fill(home, -1);
            for (int s = 0; s < _sCount; s++)
            {
                int c = _sCiv[s];
                if ((uint)c < (uint)_civs && !_free[s] && _sPop[s] > 0 && home[c] < 0) home[c] = _landmass[_sTile[s]];
            }
            for (int a = 0; a < _civs; a++)
                for (int b = 0; b < _civs && !_overseas[a]; b++)
                    if (a != b && home[a] >= 0 && home[b] >= 0 && home[a] != home[b] && _dip.Contacted(a, b)) _overseas[a] = true;
            _overseasSeason = _season;
        }
        return _overseas[civ];
    }

    /// <summary>Whether a blockade holds this settlement's harbor (the colony-cutoff hook of the economy).</summary>
    public bool CutOffBySea(int settlement) => IsBlockaded(settlement);

    /// <summary>TER-11 with SEA-12 (Discovery): colonists cross the channel in one leg, so a daughter or a decree may found this far across the sea.</summary>
    public bool HasColonyReach(int civ) => HasOceanReach(civ) && _ter11 >= 0 && _research.IsDone(civ, _ter11);

    public bool Reachable(int civ, int fromTile, int toTile) => CanReach(civ, fromTile, toTile);

    /// <summary>A daughter band's voyage rolls once as it sets out; if it is lost the band stays (and may try again next season).</summary>
    public bool VoyageLost(int civ, int fromTile, int toTile, int season)
    {
        int sea = SeaRoute(civ, fromTile, toTile, out int deep);
        if (sea <= 0) return false;
        int risk = VoyageRiskForSeaTiles(civ, sea, deep);
        ulong entity = (ulong)(fromTile * 8191L + toTile) * 31UL + 4UL;
        if (!RollVoyageLost(risk, entity, season, civ)) return false;
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
