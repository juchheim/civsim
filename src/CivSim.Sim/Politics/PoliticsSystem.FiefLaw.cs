using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;

namespace CivSim.Sim.Politics;

/// <summary>Minimal deterministic GOV-10 crime/dispute pressure for the Fief chapter.</summary>
public sealed partial class PoliticsSystem
{
    /// <summary>Optional read-only observer for harness WP7 traces. It is never hashed or called by simulation decisions.</summary>
    public Action<string>? FiefWp7TraceObserverForDiagnostics { get; set; }
    private static readonly ulong FiefLawEventStream = Hash64.OfString("politics.fief-law-event");
    private static readonly Fix64 FiefLawEventBaseChance = Fix64.FromRatio(1, 100);
    private static readonly Fix64 FiefLawEventChancePerDisorder = Fix64.FromRatio(1, 10_000);
    private static readonly Fix64 FiefLawEventChanceLimit = Fix64.FromRatio(1, 10);
    private static readonly TradeGood[] FiefCreditRepaymentGoods =
        { TradeGood.Copper, TradeGood.Tin, TradeGood.Bronze, TradeGood.Salt, TradeGood.Gold, TradeGood.Luxury, TradeGood.Iron };

    private sealed class FiefCreditClaim
    {
        public int Id, DebtorCiv, CreditorCiv, DebtorSettlement, CreditorSettlement;
        public int OriginationSeason, LastPaymentSeason;
        public Fix64 RemainingValue;
        public readonly Fix64[] UnitValues = new Fix64[(int)TradeGood.Count];
    }

    private readonly List<FiefCreditClaim> _fiefCreditClaims = new();
    private int _nextFiefCreditClaimId;
    private readonly Fix64[] _fiefTradeProfit = Array.Empty<Fix64>();

    /// <summary>Seller-side realized book margin in ration-equivalent value after the agreed invoice is paid.</summary>
    internal Fix64 FixtureFiefTradeProfit(int civ)
        => _economy.FiefChain && (uint)civ < (uint)_fiefTradeProfit.Length ? _fiefTradeProfit[civ] : Fix64.Zero;

    internal void FixtureAddCaravan(Expedition expedition) => _expeditions.Add(expedition);

    internal void FixtureRecordFiefTradeProfit(Compact compact, Expedition shipment, Fix64 delivered, Fix64 paid)
        => RecordFiefTradeProfit(compact, shipment, delivered, paid);

    private static Fix64 SaturatingAccountingMultiply(Fix64 a, Fix64 b)
    {
        Int128 raw = ((Int128)a.Raw * b.Raw) / Fix64.OneRaw;
        return Fix64.FromRaw(raw > long.MaxValue ? long.MaxValue : raw < long.MinValue ? long.MinValue : (long)raw);
    }

    private static Fix64 SaturatingAccountingAdd(Fix64 a, Fix64 b)
    {
        Int128 raw = (Int128)a.Raw + b.Raw;
        return Fix64.FromRaw(raw > long.MaxValue ? long.MaxValue : raw < long.MinValue ? long.MinValue : (long)raw);
    }

    private static Fix64 SaturatingAccountingSubtract(Fix64 a, Fix64 b)
    {
        Int128 raw = (Int128)a.Raw - b.Raw;
        return Fix64.FromRaw(raw > long.MaxValue ? long.MaxValue : raw < long.MinValue ? long.MinValue : (long)raw);
    }

    private void RecordFiefTradeProfit(Compact compact, Expedition shipment, Fix64 delivered, Fix64 paid)
    {
        int seller = compact.SellerCiv;
        if (!_economy.FiefChain || unchecked((uint)seller) >= (uint)_civs) return; // Free Folk have no civilization ledger.
        // This diagnostic must not make an otherwise valid settlement crash when unusually large stocks
        // produce a very large book value. Saturate only the separate accounting ledger; trade terms stay exact.
        Fix64 margin = SaturatingAccountingSubtract(paid, SaturatingAccountingMultiply(delivered, shipment.SellerBookValue));
        _fiefTradeProfit[seller] = SaturatingAccountingAdd(_fiefTradeProfit[seller], margin);
        FiefWp7TraceObserverForDiagnostics?.Invoke($"wp7-trd09-margin seed={_seed} season={_season} sellerCiv={seller} sellerSettlement={shipment.FromSettlement} buyerSettlement={shipment.ToSettlement} quantity={delivered.Raw} dispatchUnitValue={shipment.SellerBookValue.Raw} paid={paid.Raw} realizedMargin={margin.Raw} dispatch=true delivery=true");
    }

    /// <summary>Raise only a positive Fief seller spread from its frozen book basis; the increased invoice is settled normally.</summary>
    private Fix64 FiefTradeInvoice(int sellerCiv, Fix64 agreedInvoice, Fix64 dispatchBookBasis)
    {
        int trd09 = _research.Catalog.IndexOf("TRD-09");
        if (!_economy.FiefChain || sellerCiv < 0 || trd09 < 0 || !_research.IsDone(sellerCiv, trd09)
            || agreedInvoice <= dispatchBookBasis)
            return agreedInvoice;
        Fix64 spread = agreedInvoice - dispatchBookBasis;
        return dispatchBookBasis + spread * _research.ModifierMultiplier(sellerCiv, "trade_profit");
    }

    internal Fix64 FixtureFiefTradeInvoice(int sellerCiv, Fix64 agreedInvoice, Fix64 dispatchBookBasis)
        => FiefTradeInvoice(sellerCiv, agreedInvoice, dispatchBookBasis);

    private void HashFiefTradeAccounting(ref Core.StateHasher h)
    {
        if (!_economy.FiefChain) return;
        foreach (Fix64 profit in _fiefTradeProfit) h.Add(profit);
        int caravans = 0;
        foreach (Expedition e in _expeditions) if (e.Kind == ExpeditionKind.Caravan) caravans++;
        h.Add(caravans);
        foreach (Expedition e in _expeditions)
            if (e.Kind == ExpeditionKind.Caravan) { h.Add(e.Id); h.Add(e.SellerBookValue); h.Add(e.CarriesPlague); }
    }

    private void WriteFiefTradeAccounting(System.IO.BinaryWriter writer)
    {
        if (!_economy.FiefChain) return;
        writer.Write(_fiefTradeProfit.Length);
        foreach (Fix64 profit in _fiefTradeProfit) writer.Write(profit.Raw);
        int caravans = 0;
        foreach (Expedition e in _expeditions) if (e.Kind == ExpeditionKind.Caravan) caravans++;
        writer.Write(caravans);
        foreach (Expedition e in _expeditions)
            if (e.Kind == ExpeditionKind.Caravan) { writer.Write(e.Id); writer.Write(e.SellerBookValue.Raw); writer.Write(e.CarriesPlague); }
    }

    private void ReadFiefTradeAccounting(System.IO.BinaryReader reader)
    {
        if (!_economy.FiefChain) return;
        if (reader.ReadInt32() != _fiefTradeProfit.Length) throw new System.IO.InvalidDataException("Snapshot contains an invalid Fief trade-profit ledger.");
        for (int civ = 0; civ < _fiefTradeProfit.Length; civ++) _fiefTradeProfit[civ] = Fix64.FromRaw(reader.ReadInt64());
        int caravans = reader.ReadInt32();
        int expected = 0;
        foreach (Expedition e in _expeditions) if (e.Kind == ExpeditionKind.Caravan) expected++;
        if (caravans != expected) throw new System.IO.InvalidDataException("Snapshot contains an invalid Fief caravan accounting table.");
        for (int i = 0; i < caravans; i++)
        {
            int id = reader.ReadInt32();
            Fix64 bookValue = Fix64.FromRaw(reader.ReadInt64());
            bool carriesPlague = _economy.FiefSnapshotVersionForRestore >= 44 && reader.ReadBoolean();
            Expedition? found = null;
            foreach (Expedition e in _expeditions) if (e.Kind == ExpeditionKind.Caravan && e.Id == id) { found = e; break; }
            if (found is null || bookValue < Fix64.Zero) throw new System.IO.InvalidDataException("Snapshot contains an invalid Fief caravan book basis.");
            found.SellerBookValue = bookValue;
            found.CarriesPlague = carriesPlague;
        }
    }

    /// <summary>At most one keyed crime/dispute check per settlement-year; no extra state or random-stream consumption.</summary>
    private void ResolveFiefLawEvents()
    {
        if (!_economy.FiefChain || _season % 4 != 3) return;
        int year = _season / 4;
        for (int settlement = 0; settlement < _sCount; settlement++)
        {
            int civ = _sCiv[settlement];
            if (civ < 0 || _sPop[settlement] <= 0) continue;
            (bool live, int points) = _economy.CohesionStatus(settlement);
            int gov10Index = _research.Catalog.IndexOf("GOV-10");
            bool gov10 = gov10Index >= 0 && _research.IsDone(civ, gov10Index);
            (int overreach, int spanPenalty) = _economy.CohesionPressures(settlement);
            int disorder = Math.Max(0, 100 - points) + Math.Max(0, overreach) + Math.Max(0, spanPenalty);
            (bool eligible, Fix64 chance, Fix64 roll) = FiefLawEventRoll(settlement, civ, year);
            bool hit = eligible && roll < chance;
            Fix64 before = Fix64.FromRatio(6, 10) + Fix64.FromRatio(points, 1000) * Fix64.FromInt(6);
            if (hit)
                _economy.AdjustCohesion(settlement, -Fix64.FromRatio(6, 1000)); // one existing Cohesion point
            Fix64 after = hit ? before - Fix64.FromRatio(6, 1000) : before;
            (int id, _, _, _, _, _) = _economy.SettlementLite(settlement);
            FiefWp7TraceObserverForDiagnostics?.Invoke(string.Join(" ", "wp7-gov10", $"seed={_seed}", $"season={_season}", $"year={year}",
                $"civ={civ}", $"settlement={id}", $"live={live.ToString().ToLowerInvariant()}", $"eligible={eligible.ToString().ToLowerInvariant()}",
                $"disorder={disorder}", $"gov10={gov10.ToString().ToLowerInvariant()}", $"chance={chance.Raw}", $"roll={roll.Raw}",
                $"hit={hit.ToString().ToLowerInvariant()}", $"cohesionBefore={before.Raw}", $"cohesionAfter={after.Raw}", $"event={(hit ? "crime_or_dispute" : "none")}"));
        }
    }

    private (bool Eligible, Fix64 Chance, Fix64 Roll) FiefLawEventRoll(int settlement, int civ, int year)
    {
        (bool live, int points) = _economy.CohesionStatus(settlement);
        if (!_economy.FiefChain || civ < 0 || _sPop[settlement] <= 0 || !live)
            return (false, Fix64.Zero, Fix64.One);
        (int overreach, int spanPenalty) = _economy.CohesionPressures(settlement);
        int disorder = Math.Max(0, 100 - points) + Math.Max(0, overreach) + Math.Max(0, spanPenalty);
        if (disorder == 0) return (false, Fix64.Zero, Fix64.One);
        Fix64 chance = Fix64.Min(FiefLawEventChanceLimit,
            FiefLawEventBaseChance + Fix64.FromInt(disorder) * FiefLawEventChancePerDisorder);
        int gov10 = _research.Catalog.IndexOf("GOV-10");
        if (gov10 >= 0 && _research.IsDone(civ, gov10)) chance *= Fix64.Half;
        Fix64 roll = Pcg32.ForStream(_seed, FiefLawEventStream, (ulong)(uint)settlement, year).NextFix01();
        return (true, chance, roll);
    }

    private bool CanUseFiefCredit(int buyerCiv)
    {
        if (!_economy.FiefChain || buyerCiv < 0) return false;
        int trd08 = _research.Catalog.IndexOf("TRD-08");
        return trd08 >= 0 && _research.IsDone(buyerCiv, trd08);
    }

    /// <summary>Record the unpaid portion of a Fief caravan invoice at the buyer's origination exchange rates.</summary>
    private bool TryOriginateFiefCredit(int debtorCiv, int creditorCiv, int debtorSettlement, int creditorSettlement, Fix64 unpaidValue)
    {
        if (!_economy.FiefChain || !CanUseFiefCredit(debtorCiv) || debtorCiv < 0 || creditorCiv < 0 ||
            (uint)debtorSettlement >= (uint)_sCount || (uint)creditorSettlement >= (uint)_sCount || unpaidValue <= Fix64.Zero)
            return false;

        var claim = new FiefCreditClaim
        {
            Id = _nextFiefCreditClaimId++, DebtorCiv = debtorCiv, CreditorCiv = creditorCiv,
            DebtorSettlement = debtorSettlement, CreditorSettlement = creditorSettlement,
            OriginationSeason = _season, LastPaymentSeason = _season, RemainingValue = unpaidValue,
        };
        bool hasRepaymentGood = false;
        foreach (TradeGood good in FiefCreditRepaymentGoods)
        {
            Fix64 value = UnitValue(debtorSettlement, good);
            claim.UnitValues[(int)good] = value;
            hasRepaymentGood |= value > Fix64.Zero;
        }
        if (!hasRepaymentGood) return false;
        _fiefCreditClaims.Add(claim);
        FiefWp7TraceObserverForDiagnostics?.Invoke(string.Join(" ", "wp7-trd08", $"seed={_seed}", $"season={_season}", $"claim={claim.Id}",
            $"debtorCiv={debtorCiv}", $"creditorCiv={creditorCiv}", $"debtorSettlement={debtorSettlement}", $"creditorSettlement={creditorSettlement}",
            $"event=origination", $"principal={unpaidValue.Raw}", "routeIndependent=true", $"lockedValues={string.Join(',', claim.UnitValues.Select(v => v.Raw.ToString(CultureInfo.InvariantCulture)))}"));
        return true;
    }

    /// <summary>Collect existing non-food goods after the debtor's forecast food need plus one season reserve.</summary>
    private void CollectFiefCreditClaims()
    {
        if (!_economy.FiefChain) return;
        for (int i = _fiefCreditClaims.Count - 1; i >= 0; i--)
        {
            FiefCreditClaim claim = _fiefCreditClaims[i];
            int debtor = claim.DebtorSettlement;
            if ((uint)debtor >= (uint)_sCount || _sPop[debtor] <= 0 || _sCiv[debtor] != claim.DebtorCiv)
            {
                FiefWp7TraceObserverForDiagnostics?.Invoke($"wp7-trd08 seed={_seed} season={_season} claim={claim.Id} event=writeoff cause=debtor_loss remaining={claim.RemainingValue.Raw}");
                _fiefCreditClaims.RemoveAt(i); // the debtor's settlement was lost; the remaining claim is written off
                continue;
            }
            if (_season <= claim.OriginationSeason) continue;
            Fix64 foodReserve = _economy.TradeNeed(debtor, DecreeArgs.GoodId(TradeGood.Food), _trade.Horizon + 1);
            Fix64 foodStores = _economy.FoodStores(debtor);
            FiefWp7TraceObserverForDiagnostics?.Invoke($"wp7-trd08 seed={_seed} season={_season} claim={claim.Id} event=claim_season opening={claim.RemainingValue.Raw} foodStores={foodStores.Raw} protectedReserve={foodReserve.Raw} foodEligible={(foodStores >= foodReserve).ToString().ToLowerInvariant()} routeIndependent=true collection=local");
            if (_season - claim.LastPaymentSeason >= 8)
            {
                FiefWp7TraceObserverForDiagnostics?.Invoke($"wp7-trd08 seed={_seed} season={_season} claim={claim.Id} event=expiry cause=eight_unpaid_seasons remaining={claim.RemainingValue.Raw}");
                _fiefCreditClaims.RemoveAt(i);
                continue;
            }
            if (foodStores < foodReserve)
                continue;

            int creditor = CurrentFiefCreditCreditorSettlement(claim);
            if (creditor < 0) continue;
            Fix64 paidValue = Fix64.Zero;
            foreach (TradeGood good in FiefCreditRepaymentGoods)
            {
                Fix64 unitValue = claim.UnitValues[(int)good];
                if (unitValue <= Fix64.Zero || paidValue >= claim.RemainingValue) continue;
                Fix64 available = Surplus(debtor, good);
                Fix64 quantity = Fix64.Min(available, (claim.RemainingValue - paidValue) / unitValue);
                if (quantity <= Fix64.Zero) continue;
                string goodId = DecreeArgs.GoodId(good);
                Fix64 moved = _economy.TakeGood(debtor, goodId, quantity);
                if (moved <= Fix64.Zero) continue;
                _economy.PutGood(creditor, goodId, moved);
                paidValue += moved * unitValue;
            }
            if (paidValue > Fix64.Zero)
            {
                Fix64 opening = claim.RemainingValue;
                claim.RemainingValue = Fix64.Max(Fix64.Zero, claim.RemainingValue - paidValue);
                claim.LastPaymentSeason = _season;
                FiefWp7TraceObserverForDiagnostics?.Invoke($"wp7-trd08 seed={_seed} season={_season} claim={claim.Id} event=payment opening={opening.Raw} paid={paidValue.Raw} closing={claim.RemainingValue.Raw} foodAfter={_economy.FoodStores(debtor).Raw} protectedReserve={foodReserve.Raw} collection=local reserveProtected=true routeIndependent=true");
            }
            if (claim.RemainingValue <= Fix64.Zero) _fiefCreditClaims.RemoveAt(i);
        }
    }

    private int CurrentFiefCreditCreditorSettlement(FiefCreditClaim claim)
    {
        int original = claim.CreditorSettlement;
        if ((uint)original < (uint)_sCount && _sPop[original] > 0 && _sCiv[original] == claim.CreditorCiv) return original;
        for (int i = 0; i < _sCount; i++)
            if (_sPop[i] > 0 && _sCiv[i] == claim.CreditorCiv) return i;
        return -1;
    }

    private Fix64 FiefCreditFoodPaymentLimit(int buyer)
    {
        if (!_economy.FiefChain || (uint)buyer >= (uint)_sCount) return Fix64.MaxValue;
        Fix64 protectedFood = _economy.TradeNeed(buyer, DecreeArgs.GoodId(TradeGood.Food), _trade.Horizon + 1);
        return Fix64.Max(Fix64.Zero, _economy.FoodStores(buyer) - protectedFood);
    }

    private void HashFiefCreditClaims(ref Core.StateHasher h)
    {
        if (!_economy.FiefChain) return;
        h.Add(_nextFiefCreditClaimId);
        h.Add(_fiefCreditClaims.Count);
        foreach (FiefCreditClaim claim in _fiefCreditClaims)
        {
            h.Add(claim.Id); h.Add(claim.DebtorCiv); h.Add(claim.CreditorCiv); h.Add(claim.DebtorSettlement); h.Add(claim.CreditorSettlement);
            h.Add(claim.OriginationSeason); h.Add(claim.LastPaymentSeason); h.Add(claim.RemainingValue);
            foreach (Fix64 unitValue in claim.UnitValues) h.Add(unitValue);
        }
    }

    private void WriteFiefCreditClaims(System.IO.BinaryWriter writer)
    {
        if (!_economy.FiefChain) return;
        writer.Write(_nextFiefCreditClaimId);
        writer.Write(_fiefCreditClaims.Count);
        foreach (FiefCreditClaim claim in _fiefCreditClaims)
        {
            writer.Write(claim.Id); writer.Write(claim.DebtorCiv); writer.Write(claim.CreditorCiv); writer.Write(claim.DebtorSettlement); writer.Write(claim.CreditorSettlement);
            writer.Write(claim.OriginationSeason); writer.Write(claim.LastPaymentSeason); writer.Write(claim.RemainingValue.Raw);
            writer.Write(claim.UnitValues.Length);
            foreach (Fix64 unitValue in claim.UnitValues) writer.Write(unitValue.Raw);
        }
    }

    private void ReadFiefCreditClaims(System.IO.BinaryReader reader)
    {
        if (!_economy.FiefChain) return;
        _fiefCreditClaims.Clear();
        _nextFiefCreditClaimId = reader.ReadInt32();
        int count = reader.ReadInt32();
        if (count is < 0 or > 100_000) throw new System.IO.InvalidDataException("Snapshot contains an invalid Fief credit-claim count.");
        for (int i = 0; i < count; i++)
        {
            var claim = new FiefCreditClaim
            {
                Id = reader.ReadInt32(), DebtorCiv = reader.ReadInt32(), CreditorCiv = reader.ReadInt32(),
                DebtorSettlement = reader.ReadInt32(), CreditorSettlement = reader.ReadInt32(), OriginationSeason = reader.ReadInt32(),
                LastPaymentSeason = reader.ReadInt32(), RemainingValue = Fix64.FromRaw(reader.ReadInt64()),
            };
            if (reader.ReadInt32() != claim.UnitValues.Length) throw new System.IO.InvalidDataException("Snapshot contains an invalid Fief credit valuation table.");
            for (int good = 0; good < claim.UnitValues.Length; good++) claim.UnitValues[good] = Fix64.FromRaw(reader.ReadInt64());
            if (claim.Id < 0 || claim.RemainingValue <= Fix64.Zero || claim.OriginationSeason < 0 || claim.LastPaymentSeason < claim.OriginationSeason)
                throw new System.IO.InvalidDataException("Snapshot contains an invalid Fief credit claim.");
            _fiefCreditClaims.Add(claim);
        }
    }

    internal void FixtureOriginateFiefCredit(int debtorCiv, int creditorCiv, int debtorSettlement, int creditorSettlement, Fix64 value)
    {
        if (!TryOriginateFiefCredit(debtorCiv, creditorCiv, debtorSettlement, creditorSettlement, value))
            throw new InvalidOperationException("Fief credit claim could not be originated.");
    }

    internal Fix64 FixtureFiefCreditBalance => _fiefCreditClaims.Count == 0 ? Fix64.Zero : _fiefCreditClaims[0].RemainingValue;
    internal int FixtureFiefCreditClaimCount => _fiefCreditClaims.Count;
    internal int FixtureFiefCreditClaimId => _fiefCreditClaims.Count == 0 ? -1 : _fiefCreditClaims[0].Id;
    internal void FixtureResolveFiefLawEvents(int season) { RefreshSettlements(); _season = season; ResolveFiefLawEvents(); }
    internal (bool Eligible, Fix64 Chance, Fix64 Roll, int Population, bool Live, int Points) FixtureFiefLawEventRoll(int settlement, int year)
    {
        RefreshSettlements();
        int civ = _sCiv[settlement];
        (bool eligible, Fix64 chance, Fix64 roll) = FiefLawEventRoll(settlement, civ, year);
        (bool live, int points) = _economy.CohesionStatus(settlement);
        return (eligible, chance, roll, _sPop[settlement], live, points);
    }
    internal void FixtureCollectFiefCreditClaims(int season) { RefreshSettlements(); _season = season; CollectFiefCreditClaims(); }
}
