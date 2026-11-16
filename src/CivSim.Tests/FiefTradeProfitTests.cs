using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>TRD-09 improves a positive Fief seller spread through the ordinary invoice and settlement.</summary>
public sealed class FiefTradeProfitTests
{
    private const ulong Seed = 20261114;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric("KEY-5", "TRD-09"));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner) Start()
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: ChapterRules.Fief);
        return (match, new SimRunner(match));
    }

    private static void Grant(SimRunner runner, MatchModel match, int civ, params string[] projects)
    {
        foreach (string project in projects)
        {
            int index = match.Research.Catalog.IndexOf(project);
            Assert.True(index >= 0, $"{project} must exist in the test pack");
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, index, 0, 0));
            runner.Advance(2);
        }
    }

    private static int Settlement(MatchModel match, int civ)
        => Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == civ);

    private static (Expedition Caravan, Compact Compact, int Seller, int Buyer, Fix64 Basis) Prepare(MatchModel match, int profitResearchCiv)
    {
        int seller = Settlement(match, 0), buyer = Settlement(match, 1);
        match.Economy.GiveGoods(buyer, Fix64.FromInt(1000));
        Compact compact = match.Politics.FixtureAddCompact(0, 1, TradeGood.Salt);
        Fix64 basis = match.Politics.UnitValue(seller, TradeGood.Salt);
        var caravan = new Expedition
        {
            Kind = ExpeditionKind.Caravan, Civ = 0, TargetCiv = 1, FromSettlement = seller, ToSettlement = buyer,
            Good = TradeGood.Salt, Cargo = Fix64.FromInt(2), Value = match.Politics.FixtureFiefTradeInvoice(
                0, basis + Fix64.One, basis), Id = 41, CompactId = compact.Id,
            SellerBookValue = basis, CarriesPlague = true,
        };
        match.Politics.FixtureAddCaravan(caravan);
        return (caravan, compact, seller, buyer, basis);
    }

    private static Fix64 RunArrival(int researchCiv, out Fix64 sellerFood, out Fix64 buyerCopper, out Fix64 sellerProfitFactor)
    {
        (MatchModel match, SimRunner runner) = Start();
        Grant(runner, match, 0, "KEY-5");
        Grant(runner, match, 1, "KEY-5");
        Grant(runner, match, researchCiv, "TRD-09");
        (Expedition caravan, _, int seller, int buyer, Fix64 basis) = Prepare(match, researchCiv);
        sellerProfitFactor = match.Research.ModifierMultiplier(0, "trade_profit");
        Fix64 sellerFoodBefore = match.Economy.FoodStores(seller);
        Fix64 buyerSaltBefore = match.Economy.GoodStock(buyer, "G-SALT");
        match.Politics.FixtureArriveCaravan(caravan);
        sellerFood = match.Economy.FoodStores(seller) - sellerFoodBefore;
        buyerCopper = match.Economy.GoodStock(buyer, "G-SALT") - buyerSaltBefore;
        return match.Politics.FixtureFiefTradeProfit(0);
    }

    [Fact]
    public void TRD09_increases_positive_dispatch_spread_and_settles_the_higher_invoice_in_goods()
    {
        Fix64 sellerFoodWith, buyerCopperWith, sellerFoodWithout, buyerCopperWithout, factorWith, factorWithout;
        Fix64 withSellerResearch = RunArrival(0, out sellerFoodWith, out buyerCopperWith, out factorWith);
        Fix64 withoutSellerResearch = RunArrival(1, out sellerFoodWithout, out buyerCopperWithout, out factorWithout);

        Assert.True(factorWith > factorWithout);
        Assert.InRange((sellerFoodWith - sellerFoodWithout - Fix64.FromInt(2) * (factorWith - factorWithout)).Raw, -64L, 64L);
        Assert.Equal(Fix64.FromInt(2), buyerCopperWith);
        Assert.Equal(Fix64.FromInt(2), buyerCopperWithout);
        Assert.InRange((withSellerResearch - factorWith * withoutSellerResearch).Raw, -64L, 64L);
        Assert.True(withoutSellerResearch != Fix64.Zero, "fixture should realize a nonzero seller margin");
    }

    [Fact]
    public void TRD09_locks_the_dispatch_basis_and_persists_the_Fief_ledger_and_inflight_basis()
    {
        (MatchModel match, SimRunner runner) = Start();
        Grant(runner, match, 0, "KEY-5", "TRD-09");
        Grant(runner, match, 1, "KEY-5");
        (Expedition _, Compact _, int seller, int buyer, Fix64 basis) = Prepare(match, 0);

        using var snapshot = new MemoryStream();
        using (var writer = new BinaryWriter(snapshot, System.Text.Encoding.UTF8, leaveOpen: true)) match.WriteSnapshot(writer);
        snapshot.Position = 0;
        MatchModel resumed = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: ChapterRules.Fief);
        using (var reader = new BinaryReader(snapshot)) resumed.ReadSnapshot(reader);
        Assert.Equal(match.ComputeChecksum(), resumed.ComputeChecksum());
        Expedition loaded = Assert.Single(resumed.Politics.Caravans());
        Assert.Equal(basis, loaded.SellerBookValue);
        Assert.True(loaded.CarriesPlague);
        ulong checksumWithPayload = resumed.ComputeChecksum();
        loaded.CarriesPlague = false;
        Assert.NotEqual(checksumWithPayload, resumed.ComputeChecksum());
        loaded.CarriesPlague = true;

        resumed.Economy.FixtureStock(seller, "G-SALT", Fix64.FromInt(1000) - resumed.Economy.GoodStock(seller, "G-SALT"));
        Assert.NotEqual(basis, resumed.Politics.UnitValue(seller, TradeGood.Salt));
        resumed.Politics.FixtureArriveCaravan(loaded);
        Fix64 rawMargin = loaded.Value * Fix64.FromInt(2) - Fix64.FromInt(2) * basis;
        Assert.InRange((resumed.Politics.FixtureFiefTradeProfit(0) - rawMargin).Raw, -64L, 64L);
        Assert.Equal(Fix64.Zero, resumed.Politics.FixtureFiefTradeProfit(1));

        using var completed = new MemoryStream();
        using (var writer = new BinaryWriter(completed, System.Text.Encoding.UTF8, leaveOpen: true)) resumed.WriteSnapshot(writer);
        completed.Position = 0;
        MatchModel roundTrip = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: ChapterRules.Fief);
        using (var reader = new BinaryReader(completed)) roundTrip.ReadSnapshot(reader);
        Assert.Equal(resumed.ComputeChecksum(), roundTrip.ComputeChecksum());
        Assert.Equal(resumed.Politics.FixtureFiefTradeProfit(0), roundTrip.Politics.FixtureFiefTradeProfit(0));
        Assert.Equal(Fix64.FromInt(2), roundTrip.Economy.GoodStock(buyer, "G-SALT") - match.Economy.GoodStock(buyer, "G-SALT"));
    }

    [Fact]
    public void Fief_profit_ledger_saturates_shipment_margin_and_cumulative_sum()
    {
        (MatchModel productMatch, _) = Start();
        Compact productCompact = productMatch.Politics.FixtureAddCompact(0, 1, TradeGood.Salt);
        var product = new Expedition { Kind = ExpeditionKind.Caravan, SellerBookValue = Fix64.MaxValue };
        productMatch.Politics.FixtureRecordFiefTradeProfit(productCompact, product, Fix64.MaxValue, Fix64.Zero);
        Assert.Equal(Fix64.FromRaw(long.MinValue + 1), productMatch.Politics.FixtureFiefTradeProfit(0));

        (MatchModel sumMatch, _) = Start();
        Compact sumCompact = sumMatch.Politics.FixtureAddCompact(0, 1, TradeGood.Salt);
        var zeroCost = new Expedition { Kind = ExpeditionKind.Caravan, SellerBookValue = Fix64.Zero };
        sumMatch.Politics.FixtureRecordFiefTradeProfit(sumCompact, zeroCost, Fix64.Zero, Fix64.MaxValue);
        sumMatch.Politics.FixtureRecordFiefTradeProfit(sumCompact, zeroCost, Fix64.Zero, Fix64.MaxValue);
        Assert.Equal(Fix64.MaxValue, sumMatch.Politics.FixtureFiefTradeProfit(0));

        (MatchModel researchedMatch, SimRunner runner) = Start();
        Grant(runner, researchedMatch, 0, "KEY-5", "TRD-09");
        Compact researchedCompact = researchedMatch.Politics.FixtureAddCompact(0, 1, TradeGood.Salt);
        researchedMatch.Politics.FixtureRecordFiefTradeProfit(researchedCompact, zeroCost, Fix64.Zero, Fix64.MaxValue);
        Assert.Equal(Fix64.MaxValue, researchedMatch.Politics.FixtureFiefTradeProfit(0));
    }

    [Fact]
    public void Free_Folk_seller_sentinel_is_ignored_without_mutating_the_Fief_ledger()
    {
        (MatchModel match, _) = Start();
        Compact compact = match.Politics.FixtureAddCompact(-1, 0, TradeGood.Salt);
        ulong before = match.ComputeChecksum();

        match.Politics.FixtureRecordFiefTradeProfit(compact, new Expedition(), Fix64.MaxValue, Fix64.MaxValue);

        Assert.Equal(before, match.ComputeChecksum());
        for (int civ = 0; civ < match.Economy.CivilizationCount; civ++)
            Assert.Equal(Fix64.Zero, match.Politics.FixtureFiefTradeProfit(civ));
    }

    [Fact]
    public void TRD09_leaves_nonpositive_spreads_and_non_Fief_invoice_values_unchanged()
    {
        (MatchModel fief, SimRunner runner) = Start();
        Grant(runner, fief, 0, "KEY-5", "TRD-09");
        Assert.Equal(Fix64.FromInt(3), fief.Politics.FixtureFiefTradeInvoice(0, Fix64.FromInt(3), Fix64.FromInt(4)));
        Assert.Equal(Fix64.FromInt(4), fief.Politics.FixtureFiefTradeInvoice(0, Fix64.FromInt(4), Fix64.FromInt(4)));

        MatchModel iron = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: ChapterRules.Iron);
        SimRunner ironRunner = new(iron);
        Grant(ironRunner, iron, 0, "KEY-5", "TRD-09");
        Assert.Equal(Fix64.FromInt(5), iron.Politics.FixtureFiefTradeInvoice(0, Fix64.FromInt(5), Fix64.FromInt(4)));
    }
}
