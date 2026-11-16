using System;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Randomness;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

/// <summary>The Fief-only peaceful Free Folk inheritance effect of DIP-08. It has no leader lifecycle dependency.</summary>
public sealed partial class PoliticsSystem
{
    private static readonly ulong DynasticInheritanceStream = Hash64.OfString("politics.dynastic-inheritance");
    private const int DynasticInheritanceRange = 8;
    private const int DynasticInheritanceDelay = 2;
    private const uint DynasticInheritanceNumerator = 2, DynasticInheritanceDenominator = 100;
    private int[] _dynasticPendingCiv = Array.Empty<int>();
    private int[] _dynasticConfirmSeason = Array.Empty<int>();

    private void EnsureDynasticInheritanceState()
    {
        if (_dynasticPendingCiv.Length >= _sCount) return;
        int old = _dynasticPendingCiv.Length;
        Array.Resize(ref _dynasticPendingCiv, _sCount);
        Array.Resize(ref _dynasticConfirmSeason, _sCount);
        Array.Fill(_dynasticPendingCiv, -1, old, _sCount - old);
        Array.Fill(_dynasticConfirmSeason, -1, old, _sCount - old);
    }

    private bool HasDynasticInheritanceState()
    {
        for (int i = 0; i < _dynasticPendingCiv.Length; i++)
            if (_dynasticPendingCiv[i] >= 0) return true;
        return false;
    }

    /// <summary>Dedicated keyed 2% draw. Neither call order nor other political rolls can change its result.</summary>
    internal static bool RollDynasticInheritance(ulong seed, int civ, int settlement, int season)
        => RollDynasticInheritanceWithDraw(seed, civ, settlement, season).Hit;

    private static (bool Hit, uint Draw) RollDynasticInheritanceWithDraw(ulong seed, int civ, int settlement, int season)
    {
        ulong entity = ((ulong)(uint)civ << 32) | (uint)settlement;
        Pcg32 roll = Pcg32.ForStream(seed, DynasticInheritanceStream, entity, season);
        uint draw = roll.NextBelow(DynasticInheritanceDenominator);
        return (draw < DynasticInheritanceNumerator, draw);
    }

    private bool HasDynasticAlly(int civ)
    {
        for (int other = 0; other < _civs; other++)
            if (other != civ && _dip.Allied(civ, other)) return true;
        return false;
    }

    private bool HasDynasticReach(int civ, int folkSettlement)
    {
        for (int settlement = 0; settlement < _sCount; settlement++)
        {
            if (_sPop[settlement] <= 0 || _free[settlement]) continue;
            int owner = _sCiv[settlement];
            bool ownOrAlly = owner == civ || (uint)owner < (uint)_civs && owner != civ && _dip.Allied(civ, owner);
            if (ownOrAlly && HexGrid.Distance(_sTile[settlement], _sTile[folkSettlement], _width) <= DynasticInheritanceRange)
                return true;
        }
        return false;
    }

    private bool DynasticEligible(int civ, int folkSettlement)
    {
        int dip08 = _catalog.IndexOf("DIP-08");
        return _economy.FiefChain && dip08 >= 0 && (uint)civ < (uint)_civs &&
               _research.IsDone(civ, dip08) && HasDynasticAlly(civ) &&
               (uint)folkSettlement < (uint)_sCount && _free[folkSettlement] && _sPop[folkSettlement] > 0 &&
               HasDynasticReach(civ, folkSettlement);
    }

    /// <summary>Resolve pending peaceful inheritances, then start one keyed two-season confirmation for eligible Free Folk sites.</summary>
    private void AdvanceFiefInheritance(int season)
    {
        if (!_economy.FiefChain) return;
        EnsureDynasticInheritanceState();

        for (int settlement = 0; settlement < _sCount; settlement++)
        {
            int claimant = _dynasticPendingCiv[settlement];
            if (claimant < 0 || _dynasticConfirmSeason[settlement] > season) continue;
            _dynasticPendingCiv[settlement] = -1;
            _dynasticConfirmSeason[settlement] = -1;
            bool eligibleAgain = DynasticEligible(claimant, settlement);
            (int settlementId, _, _, _, _, _) = _economy.SettlementLite(settlement);
            FiefWp7TraceObserverForDiagnostics?.Invoke($"wp7-dip08 seed={_seed} season={season} civ={claimant} settlement={settlementId} pendingStart={season - DynasticInheritanceDelay} confirmSeason={season} recheck={eligibleAgain.ToString().ToLowerInvariant()} event={(eligibleAgain ? "confirmation" : "reversal")}");
            if (!eligibleAgain) continue;

            _free[settlement] = false;
            _economy.TransferSettlement(settlement, claimant);
            foreach (TinFolkVillage village in _tinFolk)
                if (village.Settlement == settlement) village.Joined = claimant;
            RefreshSettlements();
        }

        for (int settlement = 0; settlement < _sCount; settlement++)
        {
            if (!_free[settlement] || _sPop[settlement] <= 0 || _dynasticPendingCiv[settlement] >= 0) continue;
            for (int civ = 0; civ < _civs; civ++)
            {
                if (!DynasticEligible(civ, settlement)) continue;
                (bool hit, uint draw) = RollDynasticInheritanceWithDraw(_seed, civ, settlement, season);
                (int settlementId, _, _, _, _, _) = _economy.SettlementLite(settlement);
                FiefWp7TraceObserverForDiagnostics?.Invoke($"wp7-dip08 seed={_seed} season={season} civ={civ} settlement={settlementId} eligible=true settlementSeasonKey={season}:{settlementId} eligibleClaimantCheck=true chanceNumerator={DynasticInheritanceNumerator} chanceDenominator={DynasticInheritanceDenominator} keyedDraw={draw} keyedHit={hit.ToString().ToLowerInvariant()} distanceRule={DynasticInheritanceRange} event=eligible_check");
                if (!hit) continue;
                _dynasticPendingCiv[settlement] = civ;
                _dynasticConfirmSeason[settlement] = season + DynasticInheritanceDelay;
                FiefWp7TraceObserverForDiagnostics?.Invoke($"wp7-dip08 seed={_seed} season={season} civ={civ} settlement={settlementId} confirmSeason={season + DynasticInheritanceDelay} event=pending_start");
                break;
            }
        }
    }

    private void HashDynasticInheritance(ref StateHasher h)
    {
        EnsureDynasticInheritanceState();
        h.Add(0x44594E494E483031UL);
        h.Add(_dynasticPendingCiv.Length);
        for (int i = 0; i < _dynasticPendingCiv.Length; i++)
        {
            h.Add(_dynasticPendingCiv[i]);
            h.Add(_dynasticConfirmSeason[i]);
        }
    }

    private void WriteDynasticInheritance(BinaryWriter w)
    {
        EnsureDynasticInheritanceState();
        w.Write(_dynasticPendingCiv.Length);
        for (int i = 0; i < _dynasticPendingCiv.Length; i++)
        {
            w.Write(_dynasticPendingCiv[i]);
            w.Write(_dynasticConfirmSeason[i]);
        }
    }

    private void ReadDynasticInheritance(BinaryReader r)
    {
        int count = r.ReadInt32();
        if (count != _sCount) throw new InvalidDataException("Dynastic inheritance state has the wrong settlement count.");
        _dynasticPendingCiv = new int[count];
        _dynasticConfirmSeason = new int[count];
        for (int i = 0; i < count; i++)
        {
            int civ = r.ReadInt32(), season = r.ReadInt32();
            if (civ < -1 || civ >= _civs || civ < 0 != (season < 0))
                throw new InvalidDataException("Dynastic inheritance state has an invalid pending claimant.");
            _dynasticPendingCiv[i] = civ;
            _dynasticConfirmSeason[i] = season;
        }
    }

    internal void FixtureSetFreeFolkSettlement(int settlement, int tile)
    {
        _economy.FixtureMoveSite(settlement, tile);
        RefreshSettlements();
        _free[settlement] = true;
    }

    internal bool FixtureDynasticEligible(int civ, int settlement) => DynasticEligible(civ, settlement);
    internal void FixtureAdvanceDynasticInheritance(int season)
    {
        AdvanceFiefInheritance(season);
    }
    internal (int Civ, int ConfirmSeason) FixtureDynasticPending(int settlement)
    {
        EnsureDynasticInheritanceState();
        return (_dynasticPendingCiv[settlement], _dynasticConfirmSeason[settlement]);
    }
}
