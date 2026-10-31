using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Sim.View;

/// <summary>
/// The economy block of the frame (the "Your people" sheet): stocks every build, the per-season numbers and the
/// season-by-season history of the viewer's own settlements. The per-season numbers are read once when the builder first
/// sees a new season and remembered (saved by <see cref="WriteState"/>), so a resumed match shows the same ledger
/// although the economy keeps no copy of it in its snapshot.
/// </summary>
public sealed partial class FrameBuilder
{
    private int[] _econStamp = Array.Empty<int>();
    private long[] _econ = Array.Empty<long>();
    private int[]?[] _hist = Array.Empty<int[]?>();
    private byte[] _histCount = Array.Empty<byte>();
    private int _histVersion;
    private readonly int[] _focusHistory = new int[MatchFrame.HistoryLength * HistorySeries.CivSeries];
    private int _focusHistoryCount, _focusHistorySeason = -1;
    private int[] _payoffMap = Array.Empty<int>();
    private long[]?[] _payoff = Array.Empty<long[]?>(), _payoffTotal = Array.Empty<long[]?>();
    private byte[]?[] _payoffInfo = Array.Empty<byte[]?>();
    private readonly long[] _projection = new long[9];
    private readonly byte[] _projectionInfo = new byte[9];
    private readonly int[] _projectionProject = { -1, -1, -1 }, _projectionSeason = { -1, -1, -1 };

    private void EnsureEconScratch(int n)
    {
        if (_econStamp.Length >= n) return;
        int old = _econStamp.Length, size = Math.Max(n, Math.Max(128, old * 2));
        Array.Resize(ref _econStamp, size);
        Array.Resize(ref _econ, size * EconField.Stride);
        Array.Resize(ref _hist, size);
        Array.Resize(ref _histCount, size);
        Array.Resize(ref _payoff, size); Array.Resize(ref _payoffTotal, size); Array.Resize(ref _payoffInfo, size);
        for (int i = old; i < size; i++) _econStamp[i] = -1;
    }

    private void FillEconomy(LivingEconomyModel eco, ResearchSystem research, MatchFrame target, int season)
    {
        EnsureEconScratch(_settlementCount);
        if (_payoffMap.Length == 0 && eco.PayoffProjects.Length > 0) _payoffMap = eco.PayoffProjects.ToArray();
        int payoffCount = _payoffMap.Length;
        target.EnsurePayoff(payoffCount);
        Array.Copy(_payoffMap, target.PayoffProject, payoffCount);
        int slots = MatchFrame.StoreSlots, stride = EconField.Stride;
        int layout = 17;
        bool sampled = false;
        for (int w = 0; w < target.SettlementCount; w++)
        {
            int s = target.SettlementIndex[w];
            SettlementViewFlags flags = (SettlementViewFlags)target.SettlementFlags[w];
            bool own = (flags & (SettlementViewFlags.Own | SettlementViewFlags.Remembered | SettlementViewFlags.FreeFolk)) == SettlementViewFlags.Own;
            if (!own || (uint)s >= (uint)_settlementCount)
            {
                Array.Clear(target.SettlementStores, w * slots, slots);
                Array.Clear(target.SettlementEcon, w * stride, stride);
                target.SettlementFoodStores[w] = 0;
                target.SettlementHistoryCount[w] = 0;
                continue;
            }

            int storeBase = w * slots;
            for (int slot = 0; slot < slots; slot++) target.SettlementStores[storeBase + slot] = eco.StoreAt(s, slot).Raw;
            target.SettlementFoodStores[w] = eco.FoodStoresAt(s).Raw;
            if (_econStamp[s] != season)
            {
                RefreshEconRow(eco, s);
                RefreshPayoffRow(eco, research, s, season);
                PushHistorySample(eco, target, w, s);
                _econStamp[s] = season;
                sampled = true;
            }
            Array.Copy(_econ, s * stride, target.SettlementEcon, w * stride, stride);
            layout = unchecked(layout * 31 + s + 1);
        }

        if (sampled) _histVersion++;
        if (target.HistoryVersion == _histVersion && target.HistoryLayout == layout) return;
        int series = MatchFrame.HistorySeriesCount, perRow = MatchFrame.HistoryLength * series;
        for (int w = 0; w < target.SettlementCount; w++)
        {
            int s = target.SettlementIndex[w];
            int count = 0;
            if ((uint)s < (uint)_hist.Length && _hist[s] is { } ring && target.SettlementEcon[w * stride + EconField.Valid] != 0)
            {
                count = _histCount[s];
                Array.Copy(ring, 0, target.SettlementHistory, w * perRow, count * series);
            }
            target.SettlementHistoryCount[w] = (byte)count;
            int span = payoffCount * 3;
            bool hasPayoff = count > 0 && payoffCount > 0 && (uint)s < (uint)_payoff.Length && _payoff[s] is not null;
            if (hasPayoff)
            {
                Array.Copy(_payoff[s]!, 0, target.SettlementPayoff, w * span, span);
                Array.Copy(_payoffTotal[s]!, 0, target.SettlementPayoffTotal, w * span, span);
                Array.Copy(_payoffInfo[s]!, 0, target.SettlementPayoffInfo, w * span, span);
            }
            else if (span > 0)
            {
                Array.Clear(target.SettlementPayoff, w * span, span);
                Array.Clear(target.SettlementPayoffTotal, w * span, span);
                Array.Clear(target.SettlementPayoffInfo, w * span, span);
            }
        }
        target.HistoryVersion = _histVersion;
        target.HistoryLayout = layout;
    }

    private static int Permille(Fix64 share) => (Fix64.Clamp(share, Fix64.Zero, Fix64.FromInt(4)) * 1000).FloorToInt();

    private void RefreshEconRow(LivingEconomyModel eco, int s)
    {
        int row = s * EconField.Stride;
        Array.Clear(_econ, row, EconField.Stride);
        LivingEconomyModel.GrowthReadout growth = eco.Growth(s);
        var lite = eco.SettlementLite(s);
        _econ[row + EconField.Valid] = 1;
        _econ[row + EconField.FoodCapacity] = eco.FoodStorageCapacity(s).Raw;
        _econ[row + EconField.FoodNeed] = eco.FoodNeedThisSeason(s).Raw;
        _econ[row + EconField.FoodSecurityPermille] = Permille(lite.LastFoodSecurity);
        _econ[row + EconField.ForecastPermille] = Permille(lite.LateWinterFoodForecast);
        _econ[row + EconField.FoodCap] = growth.FoodCap.Raw;
        _econ[row + EconField.HousingCap] = growth.HousingCap.Raw;
        _econ[row + EconField.GroupCap] = growth.GroupCap.Raw;
        _econ[row + EconField.LeanCap] = growth.LeanCap.Raw;
        _econ[row + EconField.HealthPermille] = Permille(growth.HealthFactor);
        _econ[row + EconField.CrowdingFreePermille] = Permille(growth.CrowdingFreeFactor);
        _econ[row + EconField.FuelPerSeason] = eco.FuelThisSeason(s).Raw;
        Span<Fix64> ledger = stackalloc Fix64[LivingEconomyModel.FoodLedgerLineCount];
        bool valid = eco.FoodLedgerLines(s, ledger);
        _econ[row + EconField.LedgerValid] = valid ? 1 : 0;
        for (int i = 0; i < ledger.Length; i++) _econ[row + EconField.Ledger + i] = ledger[i].Raw;
        Span<Fix64> actions = stackalloc Fix64[LivingEconomyModel.ActionLedgerLineCount];
        eco.ActionLedgerLines(s, actions);
        for (int i = 0; i < actions.Length; i++) _econ[row + EconField.Actions + i] = actions[i].Raw;
        Span<Fix64> labor = stackalloc Fix64[LivingEconomyModel.LaborLineCount];
        eco.LaborLines(s, labor);
        for (int i = 0; i < labor.Length; i++) _econ[row + EconField.Labor + i] = labor[i].Raw;
        LivingEconomyModel.BuildBlockerReadout blocker = eco.BuildBlocker(s);
        _econ[row + EconField.BlockerStructure] = blocker.Structure;
        _econ[row + EconField.BlockerSlot] = blocker.Slot;
        _econ[row + EconField.BlockerHave] = blocker.Have.Raw;
        _econ[row + EconField.BlockerNeed] = blocker.Need.Raw;
    }

    private void RefreshPayoffRow(LivingEconomyModel eco, ResearchSystem research, int s, int season)
    {
        int count = _payoffMap.Length, span = count * 3;
        if (count == 0) return;
        long[] values = _payoff[s] ??= new long[span];
        long[] totals = _payoffTotal[s] ??= new long[span];
        byte[] info = _payoffInfo[s] ??= new byte[span];
        Array.Clear(values); Array.Clear(info);
        int civ = _sCiv[s];
        for (int m = 0; m < count; m++)
        {
            int project = _payoffMap[m];
            if (!research.IsDone(civ, project)) { Array.Clear(totals, m * 3, 3); continue; }
            LivingEconomyModel.PayoffValue v = eco.Payoff(s, project);
            values[m * 3] = v.Rations.Raw; values[m * 3 + 1] = v.Wood.Raw; values[m * 3 + 2] = v.Lives.Raw;
            info[m * 3] = (byte)v.Status; info[m * 3 + 1] = (byte)v.Reason; info[m * 3 + 2] = (byte)v.Tool;
            // The season a project was learned only half counts; totals start with the first full season after it.
            int learned = (int)Math.Max(0, research.CompletedTick(civ, project) / LivingEconomyModel.SeasonTicks);
            if (learned < season - 1)
            {
                totals[m * 3] += v.Rations.Raw; totals[m * 3 + 1] += v.Wood.Raw; totals[m * 3 + 2] += v.Lives.Raw;
            }
        }
    }

    /// <summary>Raw Q32.32 to hundredths of a unit, clamped to an int.</summary>
    private static int Hundredths(long raw)
    {
        long whole = raw >> 32, fraction = raw & 0xFFFFFFFFL;
        long value = whole * 100 + ((fraction * 100) >> 32);
        return (int)Math.Clamp(value, int.MinValue, int.MaxValue);
    }

    private void PushHistorySample(LivingEconomyModel eco, MatchFrame target, int w, int s)
    {
        int series = MatchFrame.HistorySeriesCount, length = MatchFrame.HistoryLength;
        int[] ring = _hist[s] ??= new int[length * series];
        int count = _histCount[s];
        if (count == length)
        {
            Array.Copy(ring, series, ring, 0, (length - 1) * series);
            count--;
        }
        int at = count * series, row = s * EconField.Stride;
        ring[at + HistorySeries.Population] = _sPop[s];
        ring[at + HistorySeries.FoodStores] = Hundredths(target.SettlementFoodStores[w]);
        ring[at + HistorySeries.FoodSecurityPermille] = (int)_econ[row + EconField.FoodSecurityPermille];
        ring[at + HistorySeries.Gathered] = Hundredths(_econ[row + EconField.Ledger + EconField.LedgerGathered]);
        ring[at + HistorySeries.Eaten] = Hundredths(_econ[row + EconField.Ledger + EconField.LedgerEaten]);
        ring[at + HistorySeries.Spoiled] = Hundredths(_econ[row + EconField.Ledger + EconField.LedgerSpoiled]);
        ring[at + HistorySeries.LostToCap] = Hundredths(_econ[row + EconField.Ledger + EconField.LedgerLostToCap]);
        int slots = MatchFrame.StoreSlots;
        for (int slot = 0; slot < slots; slot++)
            ring[at + HistorySeries.StockBase + slot] = Hundredths(target.SettlementStores[w * slots + slot]);
        _histCount[s] = (byte)(count + 1);
    }

    /// <summary>The focus civilization's people, research speed and projects, one sample a season, and the copy into the frame.</summary>
    private void FillEconomyCiv(LivingEconomyModel eco, MatchFrame target, int season)
    {
        int civ = target.FocusCiv;
        for (int slot = 0; slot < 3; slot++)
        {
            int project = target.FocusSlotProject[slot];
            if (_omniscient || project < 0)
            {
                _projectionProject[slot] = -1;
                Array.Clear(_projection, slot * 3, 3); Array.Clear(_projectionInfo, slot * 3, 3);
            }
            else if (_projectionProject[slot] != project || _projectionSeason[slot] != season)
            {
                LivingEconomyModel.PayoffValue v = eco.Projection(civ, project);
                _projectionProject[slot] = project; _projectionSeason[slot] = season;
                _projection[slot * 3] = v.Rations.Raw; _projection[slot * 3 + 1] = v.Wood.Raw; _projection[slot * 3 + 2] = v.Lives.Raw;
                _projectionInfo[slot * 3] = (byte)v.Status; _projectionInfo[slot * 3 + 1] = (byte)v.Reason; _projectionInfo[slot * 3 + 2] = (byte)v.Tool;
            }
        }
        Array.Copy(_projection, target.FocusSlotProjection, 9);
        Array.Copy(_projectionInfo, target.FocusSlotProjectionInfo, 9);
        if (!_omniscient && _focusHistorySeason != season && (uint)civ < (uint)target.CivCount)
        {
            int series = HistorySeries.CivSeries, length = MatchFrame.HistoryLength;
            if (_focusHistoryCount == length)
            {
                Array.Copy(_focusHistory, series, _focusHistory, 0, (length - 1) * series);
                _focusHistoryCount--;
            }
            int at = _focusHistoryCount * series;
            _focusHistory[at + HistorySeries.CivPopulation] = target.CivPopulation[civ];
            _focusHistory[at + HistorySeries.CivInsight] = Hundredths(target.CivInsightPerYear[civ]);
            _focusHistory[at + HistorySeries.CivProjects] = target.CivProjectsDone[civ];
            _focusHistoryCount++;
            _focusHistorySeason = season;
        }
        target.FocusHistoryCount = _focusHistoryCount;
        Array.Copy(_focusHistory, target.FocusHistory, _focusHistory.Length);
    }

    private void WriteEconomyState(BinaryWriter w)
    {
        int series = MatchFrame.HistorySeriesCount;
        w.Write(series);
        w.Write(EconField.Stride);
        w.Write(_payoffMap.Length);
        int entries = 0;
        for (int s = 0; s < _econStamp.Length; s++) if (_econStamp[s] >= 0) entries++;
        w.Write(entries);
        for (int s = 0; s < _econStamp.Length; s++)
        {
            if (_econStamp[s] < 0) continue;
            w.Write(s);
            w.Write(_econStamp[s]);
            for (int i = 0; i < EconField.Stride; i++) w.Write(_econ[s * EconField.Stride + i]);
            int count = _histCount[s];
            w.Write(count);
            if (count > 0 && _hist[s] is { } ring)
                for (int i = 0; i < count * series; i++) w.Write(ring[i]);
            bool hasPayoff = _payoffMap.Length > 0 && _payoff[s] is not null;
            w.Write(hasPayoff);
            if (hasPayoff)
            {
                for (int i = 0; i < _payoffMap.Length * 3; i++) { w.Write(_payoff[s]![i]); w.Write(_payoffTotal[s]![i]); w.Write(_payoffInfo[s]![i]); }
            }
        }
        for (int slot = 0; slot < 3; slot++)
        {
            w.Write(_projectionProject[slot]); w.Write(_projectionSeason[slot]);
            for (int i = 0; i < 3; i++) { w.Write(_projection[slot * 3 + i]); w.Write(_projectionInfo[slot * 3 + i]); }
        }
        w.Write(_focusHistorySeason);
        w.Write(_focusHistoryCount);
        for (int i = 0; i < _focusHistoryCount * HistorySeries.CivSeries; i++) w.Write(_focusHistory[i]);
    }

    private void ReadEconomyState(BinaryReader r, int version)
    {
        int series = MatchFrame.HistorySeriesCount;
        if (r.ReadInt32() != series) throw new InvalidDataException("View state has another goods list.");
        // Version 5 held 32 numbers a settlement and no payoff block: read it past and start the cache over.
        bool legacy = version == 5;
        int stride = legacy ? 32 : r.ReadInt32();
        int payoffCount = legacy ? 0 : r.ReadInt32();
        if (stride != (legacy ? 32 : EconField.Stride) || payoffCount < 0 || payoffCount > 4096) throw new InvalidDataException("Bad view state.");
        int entries = r.ReadInt32();
        if (entries < 0 || entries > 1_000_000) throw new InvalidDataException("Bad view state.");
        ClearEconomyState();
        for (int e = 0; e < entries; e++)
        {
            int s = r.ReadInt32();
            if (s < 0 || s > 1_000_000) throw new InvalidDataException("Bad view state.");
            EnsureEconScratch(s + 1);
            int stamp = r.ReadInt32();
            for (int i = 0; i < stride; i++) { long value = r.ReadInt64(); if (!legacy) _econ[s * EconField.Stride + i] = value; }
            _econStamp[s] = legacy ? -1 : stamp;
            int count = r.ReadInt32();
            if (count < 0 || count > MatchFrame.HistoryLength) throw new InvalidDataException("Bad view state.");
            _histCount[s] = (byte)count;
            if (count > 0)
            {
                int[] ring = _hist[s] = new int[MatchFrame.HistoryLength * series];
                for (int i = 0; i < count * series; i++) ring[i] = r.ReadInt32();
            }
            if (legacy) continue;
            if (r.ReadBoolean())
            {
                int span = payoffCount * 3;
                _payoff[s] = new long[span]; _payoffTotal[s] = new long[span]; _payoffInfo[s] = new byte[span];
                for (int i = 0; i < span; i++) { _payoff[s]![i] = r.ReadInt64(); _payoffTotal[s]![i] = r.ReadInt64(); _payoffInfo[s]![i] = r.ReadByte(); }
            }
        }
        for (int slot = 0; slot < 3; slot++)
        {
            int project = r.ReadInt32(), projectSeason = r.ReadInt32();
            _projectionProject[slot] = legacy ? -1 : project; _projectionSeason[slot] = projectSeason;
            for (int i = 0; i < 3; i++) { _projection[slot * 3 + i] = r.ReadInt64(); _projectionInfo[slot * 3 + i] = r.ReadByte(); }
        }
        _focusHistorySeason = r.ReadInt32();
        _focusHistoryCount = r.ReadInt32();
        if (_focusHistoryCount < 0 || _focusHistoryCount > MatchFrame.HistoryLength) throw new InvalidDataException("Bad view state.");
        for (int i = 0; i < _focusHistoryCount * HistorySeries.CivSeries; i++) _focusHistory[i] = r.ReadInt32();
        _histVersion++;
    }

    private void ClearEconomyState()
    {
        Array.Fill(_econStamp, -1);
        Array.Clear(_econ);
        Array.Clear(_histCount);
        Array.Clear(_hist);
        Array.Clear(_payoff); Array.Clear(_payoffTotal); Array.Clear(_payoffInfo);
        Array.Clear(_projection); Array.Clear(_projectionInfo);
        Array.Fill(_projectionProject, -1); Array.Fill(_projectionSeason, -1);
        Array.Clear(_focusHistory);
        _focusHistoryCount = 0;
        _focusHistorySeason = -1;
        _histVersion++;
    }
}
