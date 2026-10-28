using System;
using System.Collections.Generic;
using CivSim.Sim.Politics;
using CivSim.Sim.Randomness;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Sim.View;

/// <summary>
/// Worn trails: the walking routes between a civilization's villages, and between civilizations that have bartered. They are
/// the same routes the parties walk (docs/MVP/13), traced as shortest walking paths on the map, so the ground the front end
/// scuffs is the ground people cross. The links depend only on where villages stand and who has traded, so they are rebuilt
/// when that changes and the rest (how worn, what is explored) is read fresh each frame. View-only.
/// </summary>
public sealed partial class FrameBuilder
{
    /// <summary>Villages farther apart than this (in tiles) are not joined by a trail.</summary>
    private const int TrailReach = 16, TradeReach = 30, TrailMaxCost = 700, MaxTrails = 48;

    private sealed class TrailLink
    {
        public int A, B;
        public int CivA, CivB;
        public bool Trade;
        public int[] Tiles = Array.Empty<int>();
        public int TileCount;
    }

    private readonly List<TrailLink> _trailLinks = new(MaxTrails);
    private readonly List<TrailLink> _trailLinkPool = new(MaxTrails);
    private readonly List<long> _trailSeen = new(MaxTrails);
    private int[] _trailCosts = Array.Empty<int>();
    private int[] _trailBack = Array.Empty<int>();
    private PriorityQueue<int, int> _trailCostQueue = new();
    private ulong _trailSignature;
    private bool _trailSignatureSet;
    private int _way01 = -2, _trd03, _trn02, _trn04;

    private void InitializeTrailScratch()
    {
        _trailCosts = new int[_tileCount];
        _trailBack = new int[400];
        _trailCostQueue = new PriorityQueue<int, int>(checked(_tileCount * 6));
        for (int i = 0; i < MaxTrails; i++)
            _trailLinkPool.Add(new TrailLink { Tiles = new int[400] });
    }

    private void FillTrails(MatchModel match, MatchFrame target)
    {
        if (_way01 == -2)
        {
            _way01 = _catalog.IndexOf("WAY-01"); _trd03 = _catalog.IndexOf("TRD-03");
            _trn02 = _catalog.IndexOf("TRN-02"); _trn04 = _catalog.IndexOf("TRN-04");
        }
        RefreshTrailLinks(match);
        ResearchSystem research = match.Research;
        target.EnsureTrails(_trailLinks.Count);
        int maximumTileTotal = 0;
        for (int l = 0; l < _trailLinks.Count; l++) maximumTileTotal += _trailLinks[l].TileCount;
        target.EnsureTrailTiles(maximumTileTotal);
        int count = 0, tileTotal = 0;
        for (int l = 0; l < _trailLinks.Count; l++)
        {
            TrailLink link = _trailLinks[l];
            int level = Math.Max(TrailLevel(research, link.CivA), link.Trade ? TrailLevel(research, link.CivB) : 0);
            bool any = false;
            for (int i = 0; i < link.TileCount; i++)
                if (link.Tiles[i] >= 0 && target.Visibility[link.Tiles[i]] > 0) { any = true; break; }
            if (!any) continue;
            target.EnsureTrailTiles(tileTotal + link.TileCount);
            target.TrailLevel[count] = (byte)level;
            target.TrailCiv[count] = (sbyte)link.CivA;
            target.TrailTrade[count] = link.Trade;
            target.TrailStart[count] = tileTotal;
            target.TrailLength[count] = link.TileCount;
            for (int i = 0; i < link.TileCount; i++)
            {
                int tile = link.Tiles[i];
                // a tile nobody has explored stays off the map (-1), so a trail never gives away unseen ground
                target.TrailTiles[tileTotal++] = tile >= 0 && target.Visibility[tile] > 0 ? tile : -1;
            }
            count++;
        }
        target.TrailCount = count;
        target.TrailTileCount = tileTotal;
    }

    /// <summary>1 a footpath; 2 a marked trail (Landmarks &amp; Trails); 3 a beaten road (exchange networks, sledges or pack animals).</summary>
    private int TrailLevel(ResearchSystem research, int civ)
    {
        if ((uint)civ >= (uint)_civs) return 1;
        int level = 1;
        if (_way01 >= 0 && research.IsDone(civ, _way01)) level++;
        bool hauling = _trd03 >= 0 && research.IsDone(civ, _trd03) || _trn02 >= 0 && research.IsDone(civ, _trn02) || _trn04 >= 0 && research.IsDone(civ, _trn04);
        if (hauling) level++;
        return level;
    }

    private void RefreshTrailLinks(MatchModel match)
    {
        Diplomacy dip = match.Politics.Diplomacy;
        ulong signature = 0xcbf29ce484222325UL;
        unchecked
        {
            for (int s = 0; s < _settlementCount; s++)
            {
                ulong live = !_sFree[s] && _sPop[s] > 0 ? 1UL : 0UL;
                signature = Hash64.Mix(signature ^ ((ulong)(uint)_sTile[s] << 8 | (ulong)(byte)(sbyte)_sCiv[s] << 1 | live));
            }
            for (int a = 0; a < _civs; a++)
                for (int b = a + 1; b < _civs; b++)
                    if (dip.LastBarter(a, b) > -1000) signature = Hash64.Mix(signature ^ (ulong)(uint)(a * 64 + b));
        }
        if (_trailSignatureSet && signature == _trailSignature) return;
        _trailSignature = signature;
        _trailSignatureSet = true;
        _trailLinks.Clear();
        _trailSeen.Clear();
        // each live village joins its nearest live neighbour of the same civilization
        for (int s = 0; s < _settlementCount && _trailLinks.Count < MaxTrails; s++)
        {
            if (!Live(s)) continue;
            int best = -1, bestDistance = int.MaxValue;
            for (int t = 0; t < _settlementCount; t++)
            {
                if (t == s || !Live(t) || _sCiv[t] != _sCiv[s]) continue;
                int d = HexGrid.Distance(_sTile[s], _sTile[t], _width);
                if (d < bestDistance) { bestDistance = d; best = t; }
            }
            if (best < 0 || bestDistance > TrailReach) continue;
            AddTrail(Math.Min(s, best), Math.Max(s, best), false);
        }
        // a pair of civilizations that has bartered is joined at their two closest villages
        for (int a = 0; a < _civs; a++)
            for (int b = a + 1; b < _civs && _trailLinks.Count < MaxTrails; b++)
            {
                if (dip.LastBarter(a, b) <= -1000) continue;
                int bestA = -1, bestB = -1, bestDistance = int.MaxValue;
                for (int s = 0; s < _settlementCount; s++)
                {
                    if (!Live(s) || _sCiv[s] != a) continue;
                    for (int t = 0; t < _settlementCount; t++)
                    {
                        if (!Live(t) || _sCiv[t] != b) continue;
                        int d = HexGrid.Distance(_sTile[s], _sTile[t], _width);
                        if (d < bestDistance) { bestDistance = d; bestA = s; bestB = t; }
                    }
                }
                if (bestA >= 0 && bestDistance <= TradeReach) AddTrail(bestA, bestB, true);
            }
    }

    private bool Live(int s) => !_sFree[s] && _sPop[s] > 0 && _sCiv[s] >= 0 && _sCiv[s] < _civs;

    private void AddTrail(int a, int b, bool trade)
    {
        long key = (long)a * 1_000_000 + b;
        if (_trailSeen.Contains(key)) return;
        TrailLink link = _trailLinkPool[_trailLinks.Count];
        if (!TracePath(_sTile[a], _sTile[b], link)) return;
        _trailSeen.Add(key);
        link.A = a; link.B = b; link.CivA = _sCiv[a]; link.CivB = _sCiv[b]; link.Trade = trade;
        _trailLinks.Add(link);
    }

    /// <summary>The cheapest walking route as tiles from <paramref name="from"/> to <paramref name="to"/> (empty when there is none); water crossings are -1.</summary>
    private bool TracePath(int from, int to, TrailLink link)
    {
        if (from == to) return false;
        HexGrid.WalkingCostsFrom(_tiles, _width, _height, from, _trailCosts, _trailCostQueue, TrailMaxCost);
        if (to < 0 || to >= _trailCosts.Length || _trailCosts[to] == int.MaxValue) return false;
        Span<int> neighbors = stackalloc int[6];
        int at = to, countBack = 1;
        _trailBack[0] = to;
        while (at != from && countBack < 400)
        {
            int count = HexGrid.Neighbors(at, _width, _height, neighbors);
            int next = -1, nextCost = _trailCosts[at];
            for (int n = 0; n < count; n++)
                if (_trailCosts[neighbors[n]] < nextCost) { nextCost = _trailCosts[neighbors[n]]; next = neighbors[n]; }
            if (next < 0) return false;
            _trailBack[countBack++] = next;
            at = next;
        }
        if (at != from) return false;
        if (link.Tiles.Length < countBack) Array.Resize(ref link.Tiles, countBack);
        link.TileCount = countBack;
        for (int i = 0; i < countBack; i++)
        {
            int tile = _trailBack[countBack - i - 1];
            link.Tiles[i] = _tiles[tile].IsLand ? tile : -1;
        }
        return true;
    }
}
