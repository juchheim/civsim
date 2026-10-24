using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;

namespace CivSim.Sim.Ai.Forecast;

/// <summary>Own-only snapshot of the E3 inputs represented by the coarse research rollout.</summary>
public sealed class ForecastBronzeStart
{
    public bool Active, MandateUnlocked, CopperAccess, TinAccess, ArsenicAccess;
    public bool CopperSource, TinSource, ArsenicSource;
    public int BestCastTier, Kit8, Kit9, Kit10;
    public int Mandate, MandateCap, MandateRecharge, MandateRechargeRaw, MandateCounter, MandateRechargeMin;
    public int AdminSpan;
    public int SettlementCount;
    public Fix64 AdminReduction;
    public Fix64 SpanPenaltyPerExcess;
    public Fix64 MeanCohesion, MeanOverreach, MeanSpanPenalty;
    public int WalledSettlements, HillfortSettlements;
    public MetalOutputRates MetalOutputRates;
    public readonly List<ForecastBronzeSettlement> Settlements = new();
    public readonly List<ForecastBronzeCompact> Compacts = new();

    public ForecastBronzeStart Copy()
    {
        var copy = new ForecastBronzeStart
        {
            Active = Active, MandateUnlocked = MandateUnlocked, CopperAccess = CopperAccess, TinAccess = TinAccess, ArsenicAccess = ArsenicAccess,
            CopperSource = CopperSource, TinSource = TinSource, ArsenicSource = ArsenicSource,
            BestCastTier = BestCastTier, Kit8 = Kit8, Kit9 = Kit9, Kit10 = Kit10,
            Mandate = Mandate, MandateCap = MandateCap, MandateRecharge = MandateRecharge,
            MandateRechargeRaw = MandateRechargeRaw,
            MandateCounter = MandateCounter, MandateRechargeMin = MandateRechargeMin, AdminSpan = AdminSpan,
            AdminReduction = AdminReduction, SpanPenaltyPerExcess = SpanPenaltyPerExcess, SettlementCount = SettlementCount,
            MeanCohesion = MeanCohesion, MeanOverreach = MeanOverreach,
            MeanSpanPenalty = MeanSpanPenalty, WalledSettlements = WalledSettlements, HillfortSettlements = HillfortSettlements,
            MetalOutputRates = MetalOutputRates,
        };
        copy.Settlements.AddRange(Settlements);
        copy.Compacts.AddRange(Compacts);
        return copy;
    }
}

public readonly record struct ForecastBronzeSettlement(int Id, int Tile, int Tier, Fix64 Cohesion,
    int Overreach, int SpanPenalty, bool Hillfort, bool StoneWalls);

/// <summary>One active compact involving the forecasting civilization; no third-party compacts are copied.</summary>
public readonly record struct ForecastBronzeCompact(int Id, int Partner, TradeGood Good, bool Buying, int AgeSeasons,
    Fix64 LastVolume, Fix64 Delivered);

/// <summary>Season-end Bronze state emitted by the mean-field rollout.</summary>
public readonly record struct ForecastBronzeSeason(bool Active, int Season, int ToolTier, int Kit8, int Kit9, int Kit10,
    bool CopperAccess, bool TinAccess, bool ArsenicAccess,
    int Mandate, int MandateCap, int MandateRecharge, int AdminSpan, Fix64 AdminReduction,
    Fix64 MeanCohesion, Fix64 MeanOverreach, Fix64 MeanSpanPenalty, int WalledSettlements, int HillfortSettlements,
    int ActiveCompacts, int TinCompacts, bool HasMetalOutputRate, MetalOutputRates MetalOutputRates);

internal sealed class ForecastBronzeRollout
{
    private readonly ForecastBronzeStart _start;
    private readonly ResearchCatalog _catalog;
    private readonly int _minimumRecharge;
    private readonly int _key3;
    private bool _active;
    private int _mandate, _cap, _recharge, _rawRecharge, _counter, _span;
    private Fix64 _adminKeep;
    private readonly Fix64 _startAdminKeep;

    public ForecastBronzeRollout(ForecastBronzeStart start, ResearchCatalog catalog)
    {
        _start = start;
        _catalog = catalog;
        _active = start.Active;
        _key3 = catalog.IndexOf("KEY-3");
        _mandate = start.Mandate; _cap = start.MandateCap;
        // Before the first Mandate effect the live derived recharge is the global floor, but the first unlock
        // recomputes from effect deltas starting at zero. Do the same so KEY-3's +8 becomes 8, not floor+8.
        _rawRecharge = start.MandateUnlocked ? start.MandateRechargeRaw : 0;
        _recharge = start.MandateUnlocked ? Math.Max(Math.Max(1, start.MandateRechargeMin), _rawRecharge) : 0;
        _counter = start.MandateCounter; _minimumRecharge = Math.Max(1, start.MandateRechargeMin);
        _span = start.AdminSpan; _adminKeep = Fix64.One - start.AdminReduction; _startAdminKeep = _adminKeep;
    }

    public void OnProjectCompleted(int project)
    {
        if ((uint)project >= (uint)_catalog.Count) return;
        if (project == _key3) _active = true;
        foreach (EffectDef effect in _catalog.Projects[project].Effects)
        {
            if (effect.Kind == EffectKind.Mandate)
            {
                ApplyMandateDelta(effect.MandateCap, effect.MandateRecharge, effect.MandateStart);
            }
            else if (effect.Kind == EffectKind.Admin)
            {
                _span += effect.AdminSpan;
                _adminKeep *= Fix64.One - Fix64.Min(Fix64.One, effect.AdminOverreach);
            }
        }
        _mandate = Math.Min(_mandate, _cap);
    }

    // Keep the un-floored delta sum: a later positive delta must still be floored against the raw sum,
    // matching ResearchSystem.RebuildDerived rather than adding to an already-clamped recharge.
    internal void ApplyMandateDelta(int cap, int recharge, int start)
    {
        _cap += cap;
        _rawRecharge += recharge;
        _recharge = Math.Max(_minimumRecharge, _rawRecharge);
        _mandate = Math.Min(_cap, _mandate + start);
    }

    public void EndSeason()
    {
        if (_cap <= 0 || _mandate >= _cap) { _counter = 0; return; }
        if (++_counter >= _recharge) { _counter = 0; _mandate++; }
    }

    public ForecastBronzeSeason Snapshot(int season)
    {
        // The captured own source/compact/tier and civic state are held as a mean-field baseline. Research can
        // improve institutions, but the rollout does not invent new resource discoveries, buildings or trade.
        return new ForecastBronzeSeason(_active, season, _start.BestCastTier, _start.Kit8, _start.Kit9, _start.Kit10,
            _start.CopperAccess, _start.TinAccess, _start.ArsenicAccess,
            _mandate, _cap, _recharge, _span, Fix64.One - _adminKeep, _start.MeanCohesion,
            _startAdminKeep.Raw > 0 ? _start.MeanOverreach / _startAdminKeep * _adminKeep : _start.MeanOverreach,
            _start.SpanPenaltyPerExcess * Math.Max(0, _start.SettlementCount - _span),
            _start.WalledSettlements, _start.HillfortSettlements, _start.Compacts.Count, CountTinCompacts(), true, _start.MetalOutputRates);
    }

    private int CountTinCompacts()
    {
        int count = 0;
        foreach (ForecastBronzeCompact compact in _start.Compacts) if (compact.Good == TradeGood.Tin) count++;
        return count;
    }
}
