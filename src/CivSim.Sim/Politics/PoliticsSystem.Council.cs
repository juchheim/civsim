using System;
using System.Collections.Generic;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

/// <summary>
/// Site and partner choices the Decree policies share (the Council's here; the AI work package's Strategist may use them too). They
/// read only what the civilization knows: its own state, tiles within its Reach or revealed by its expeditions, and what it has seen.
/// </summary>
public sealed partial class PoliticsSystem
{
    private List<int>?[] _depositTiles = Array.Empty<List<int>?>();

    /// <summary>The economy, for policies that read their own civilization's metal access and civic work.</summary>
    public LivingEconomyModel Economy => _economy;
    public int CivCount => _civs;
    /// <summary>The villages' claim radius in an era (the eras registry).</summary>
    public int ClaimRadiusOfEra(int era) => _eraRadius[Math.Clamp(era, 0, _eraRadius.Length - 1)];

    private List<int> DepositTiles(DepositKind kind)
    {
        if (_depositTiles.Length == 0)
        {
            _depositTiles = new List<int>?[32];
            for (int t = 0; t < _tileArray.Length; t++)
            {
                int k = (int)_tileArray[t].Deposit;
                if (k == 0 || k >= _depositTiles.Length) continue;
                (_depositTiles[k] ??= new List<int>()).Add(t);
            }
        }
        return unchecked((uint)kind) < unchecked((uint)_depositTiles.Length) && _depositTiles[(int)kind] is { } list ? list : new List<int>();
    }

    /// <summary>
    /// The nearest deposit of <paramref name="kind"/> an <c>Open a mine</c> Decree would accept: known, workable, not owned by a rival or
    /// Free Folk, beyond every own settlement's range (the planner digs those itself) and within the nearest settlement's mining reach.
    /// Lowest tile index on ties. -1 when there is none.
    /// </summary>
    public int NearestMineSite(int civ, DepositKind kind)
    {
        int best = -1, bestDistance = int.MaxValue;
        foreach (int tile in DepositTiles(kind))
        {
            int nearest = -1, d = int.MaxValue;
            for (int s = 0; s < _sCount; s++)
            {
                if (_sCiv[s] != civ || _free[s] || _sPop[s] <= 0) continue;
                int dd = HexGrid.Distance(_sTile[s], tile, _width);
                if (dd < d) { d = dd; nearest = s; }
            }
            if (nearest < 0 || d <= 3 || d >= bestDistance || d > _economy.MiningReachFrom(nearest)) continue;
            if (MineCheck(civ, tile) != 0 || _economy.CheckOpenMiningCamp(civ, tile) != MiningCampResult.Opened) continue;
            best = tile;
            bestDistance = d;
        }
        return best;
    }

    /// <summary>
    /// The best tile for a founding Decree when a donor is at <paramref name="minFill"/> of its cap or more: the fission score (the
    /// site's food × Beacon pull × rival pressure, as daughters choose) ×1.5 within 3 tiles of an unclaimed metal deposit the civilization
    /// can dig, over known tiles the rules accept (<see cref="FoundCheck"/>). Lowest index on ties; -1 when there is none.
    /// </summary>
    public int ChooseFoundSite(int civ, Fix64 minFill)
    {
        int donorTile = -1;
        Fix64 donorFill = Fix64.Zero;
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] != civ || _free[s] || _sPop[s] < _rules.DonorMinPop || _economy.SeasonsSinceFission(s) < 4) continue;
            Fix64 fill = _economy.PopulationFill(s);
            if (fill >= minFill && fill > donorFill) { donorFill = fill; donorTile = _sTile[s]; }
        }
        if (donorTile < 0) return -1;
        int reach = FoundReach(civ, bySea: false);
        int best = -1;
        Fix64 bestScore = Fix64.Zero;
        HexGrid.Coordinates(donorTile, _width, out int cx, out int cy);
        for (int y = Math.Max(0, cy - reach); y <= Math.Min(_height - 1, cy + reach); y++)
            for (int x = Math.Max(0, cx - reach - 1); x <= Math.Min(_width - 1, cx + reach + 1); x++)
            {
                int t = y * _width + x;
                int d = HexGrid.Distance(donorTile, t, _width);
                if (d > reach || d <= EraRadiusOf(civ) || !_economy.IsHabitableSite(t) || !KnowsTile(civ, t)) continue;
                Fix64 score = _economy.SiteFood(t);
                if (score <= Fix64.Zero) continue;
                score *= Multiplier(civ, t);
                if (NearUsableDeposit(civ, t)) score = score * Fix64.FromRatio(3, 2);
                if (score <= bestScore) continue;
                if (FoundCheck(civ, t, out int donor) != 0 || donor < 0 || _economy.PopulationFill(donor) < minFill) continue;
                best = t;
                bestScore = score;
            }
        return best;
    }

    private bool NearUsableDeposit(int civ, int tile)
    {
        HexGrid.Coordinates(tile, _width, out int cx, out int cy);
        for (int y = Math.Max(0, cy - 3); y <= Math.Min(_height - 1, cy + 3); y++)
            for (int x = Math.Max(0, cx - 4); x <= Math.Min(_width - 1, cx + 4); x++)
            {
                int t = y * _width + x;
                if (_tileArray[t].Deposit is not (DepositKind.CopperOre or DepositKind.Tin or DepositKind.ArsenicalCopper)) continue;
                if (HexGrid.Distance(tile, t, _width) > 3 || _territory.OwnerOf(t) >= 0 && _territory.OwnerOf(t) != civ) continue;
                if (_economy.CanDig(civ, t)) return true;
            }
        return false;
    }

    /// <summary>Whether the civilization buys tin through an active compact.</summary>
    public bool BuysTin(int civ)
    {
        foreach (Compact c in _compacts) if (c.Active && c.BuyerCiv == civ && c.Good == TradeGood.Tin) return true;
        return false;
    }

    /// <summary>
    /// Whether the civilization knows a Free Folk village: it has seen it, or it lies within twice its Reach of an own settlement
    /// ("traders speak of the tin-folk of the western hills", docs/MVP/22 §6.4).
    /// </summary>
    public bool KnowsFolk(int civ, int folk)
    {
        if (unchecked((uint)folk) >= unchecked((uint)_sCount) || !_free[folk]) return false;
        if (KnowsTile(civ, _sTile[folk])) return true;
        int range = ExpeditionRange(civ);
        for (int s = 0; s < _sCount; s++)
            if (_sCiv[s] == civ && !_free[s] && _sPop[s] > 0 && HexGrid.Distance(_sTile[s], _sTile[folk], _width) <= range) return true;
        return false;
    }

    /// <summary>
    /// Whom the civilization would ask for tin (a Compact's partner code), or int.MinValue: the nearest free Tin Folk village it knows
    /// and can reach, else the nearest contacted civilization at Relations ≥ 0, not at war or embargoing tin, that it has seen working a
    /// tin deposit.
    /// </summary>
    public int TinSellerFor(int civ)
    {
        int best = int.MinValue, bestDistance = int.MaxValue;
        foreach (TinFolkVillage v in _tinFolk)
        {
            if (v.Joined >= 0 || !KnowsFolk(civ, v.Settlement) || FolkTieOf(civ, v.Settlement, create: false) is { Relations: < 0 }) continue;
            int home = NearestOwnSettlement(civ, _sTile[v.Settlement]);
            if (home < 0) continue;
            int travel = Travel(civ, _sTile[home], _sTile[v.Settlement]);
            if (travel == int.MaxValue || travel >= bestDistance) continue;
            best = DecreeArgs.FolkParty(v.Settlement);
            bestDistance = travel;
        }
        if (best != int.MinValue) return best;
        for (int i = 0; i < _economy.MiningCampCount; i++)
        {
            MiningCampView camp = _economy.MiningCamp(i);
            int o = camp.Civilization;
            if (o == civ || camp.Deposit != DepositKind.Tin || !KnowsTile(civ, camp.Tile)) continue;
            if (!_dip.Contacted(civ, o) || _dip.AtWar(civ, o) || _dip.Relations(civ, o) < 0 || Embargoes(o, civ, TradeGood.Tin)) continue;
            int home = NearestOwnSettlement(civ, camp.Tile);
            if (home < 0) continue;
            int d = HexGrid.Distance(_sTile[home], camp.Tile, _width);
            if (d < bestDistance) { best = o; bestDistance = d; }
        }
        return best;
    }

    /// <summary>
    /// Where an Expedition learns most: on a coarse grid (every third tile) within range of the civilization's largest settlement, the
    /// reachable tile with the most unknown tiles within the reveal radius (at least 20). Lowest index on ties; -1 when none.
    /// </summary>
    public int ChooseExpeditionTarget(int civ)
    {
        int home = -1, homePop = 0;
        for (int s = 0; s < _sCount; s++)
            if (_sCiv[s] == civ && !_free[s] && _sPop[s] > homePop && AdultCount(s) >= _rules.ExpeditionSize + 4) { home = s; homePop = _sPop[s]; }
        if (home < 0) return -1;
        int range = ExpeditionRange(civ), origin = _sTile[home];
        HexGrid.Coordinates(origin, _width, out int cx, out int cy);
        int best = -1, bestUnknown = 19, radius = _rules.RevealRadius;
        for (int y = Math.Max(0, cy - range); y <= Math.Min(_height - 1, cy + range); y += 3)
            for (int x = Math.Max(0, cx - range); x <= Math.Min(_width - 1, cx + range); x += 3)
            {
                int t = y * _width + x;
                if (HexGrid.Distance(origin, t, _width) > range || !_passable[t] || KnowsTile(civ, t)) continue;
                int unknown = 0;
                for (int yy = Math.Max(0, y - radius); yy <= Math.Min(_height - 1, y + radius); yy++)
                    for (int xx = Math.Max(0, x - radius - 1); xx <= Math.Min(_width - 1, x + radius + 1); xx++)
                    {
                        int u = yy * _width + xx;
                        if (HexGrid.Distance(t, u, _width) <= radius && !KnowsTile(civ, u)) unknown++;
                    }
                if (unknown <= bestUnknown || Travel(civ, origin, t) == int.MaxValue) continue;
                best = t;
                bestUnknown = unknown;
            }
        return best;
    }
}
