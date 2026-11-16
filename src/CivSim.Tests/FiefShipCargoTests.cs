using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>SEA-05's catalogued 12 L plank-boat load and SEA-10's Fief-only ×2 sea-cargo limit.</summary>
public sealed class FiefShipCargoTests
{
    private const ulong Seed = 20261114;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric("KEY-6", "SEA-04", "SEA-10", "TRD-08"));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner) Start(ChapterRules rules)
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules);
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

    private static (int From, int To) SeaRoute(MatchModel match, int mover)
        => (from source in Enumerable.Range(0, match.Economy.SettlementCount)
            where match.Economy.SettlementLite(source).Civilization == mover
            from target in Enumerable.Range(0, match.Economy.SettlementCount)
            where match.Economy.SettlementLite(target).Civilization != mover &&
                  match.Politics.SeaTilesOnRoute(mover, match.Economy.SettlementLite(source).SiteTile, match.Economy.SettlementLite(target).SiteTile) > 0
            select (source, target)).First();

    [Fact]
    public void Sea_capacity_is_12L_on_boat_routes_and_24L_after_SEA10_only_in_Fief()
    {
        (MatchModel fief, SimRunner runner) = Start(ChapterRules.Fief);
        Grant(runner, fief, 0, "KEY-5", "SEA-04");
        int a = Settlement(fief, 0);
        Assert.False(fief.Politics.CanSail(0), "Fief sea routes stay closed through E5");
        int b = Settlement(fief, 1);
        Assert.Equal(Fix64.MaxValue, fief.Politics.FixtureCaravanCargoCapacity(0, a, b));
        Grant(runner, fief, 0, "KEY-6");
        Assert.True(fief.Politics.CanSail(0), "sea crossing becomes available at KEY-6");
        (a, b) = SeaRoute(fief, 0);
        Assert.Equal(Fix64.FromInt(12), fief.Politics.FixtureCaravanCargoCapacity(0, a, b));
        Assert.Equal(Fix64.FromInt(12), fief.Politics.FixtureLimitCaravanCargo(0, a, b, TradeGood.Copper, Fix64.FromInt(40)));
        Assert.Equal(Fix64.FromInt(24), fief.Politics.FixtureLimitCaravanCargo(0, a, b, TradeGood.Food, Fix64.FromInt(40)));
        Assert.Equal(Fix64.MaxValue, fief.Politics.FixtureCaravanCargoCapacity(0, a, a));
        Assert.Equal(Fix64.FromInt(40), fief.Politics.FixtureLimitCaravanCargo(0, a, a, TradeGood.Copper, Fix64.FromInt(40)));

        Grant(runner, fief, 0, "SEA-10");
        Assert.Equal(Fix64.FromInt(24), fief.Politics.FixtureCaravanCargoCapacity(0, a, b));
        Assert.Equal(Fix64.FromInt(24), fief.Politics.FixtureLimitCaravanCargo(0, a, b, TradeGood.Copper, Fix64.FromInt(40)));
        Assert.Equal(Fix64.FromInt(48), fief.Politics.FixtureLimitCaravanCargo(0, a, b, TradeGood.Food, Fix64.FromInt(60)));

        (MatchModel iron, SimRunner ironRunner) = Start(ChapterRules.Iron);
        Grant(ironRunner, iron, 0, "KEY-4", "SEA-04");
        (int ia, int ib) = SeaRoute(iron, 0);
        Assert.Equal(Fix64.MaxValue, iron.Politics.FixtureCaravanCargoCapacity(0, ia, ib));
        (MatchModel bronze, _) = Start(ChapterRules.Bronze);
        Assert.Equal(Fix64.MaxValue, bronze.Politics.FixtureCaravanCargoCapacity(0, Settlement(bronze, 0), Settlement(bronze, 1)));
    }

    [Fact]
    public void Capped_cargo_is_the_amount_paid_for_and_compact_can_deliver_repeatedly()
    {
        (MatchModel match, SimRunner runner) = Start(ChapterRules.Fief);
        Grant(runner, match, 0, "KEY-5", "SEA-04", "KEY-6", "TRD-08");
        (int seller, int buyer) = SeaRoute(match, 0);
        match.Economy.GiveGoods(buyer, Fix64.FromInt(1000));
        var compact = match.Politics.FixtureAddCompact(0, 1, TradeGood.Copper);
        Fix64 sellerFoodBefore = match.Economy.FoodStores(seller);
        Fix64 buyerCopperBefore = match.Economy.GoodStock(buyer, "G-COPPER");
        var caravan = new Expedition
        {
            Kind = ExpeditionKind.Caravan, Civ = 0, TargetCiv = 1, FromSettlement = seller, ToSettlement = buyer,
            Good = TradeGood.Copper, Cargo = match.Politics.FixtureLimitCaravanCargo(0, seller, buyer, TradeGood.Copper, Fix64.FromInt(40)),
            Value = Fix64.FromInt(3), Id = 41, CompactId = compact.Id,
        };

        match.Politics.FixtureArriveCaravan(caravan);
        match.Politics.FixtureArriveCaravan(caravan);

        Assert.Equal(buyerCopperBefore + Fix64.FromInt(24), match.Economy.GoodStock(buyer, "G-COPPER"));
        Assert.Equal(sellerFoodBefore + Fix64.FromInt(72), match.Economy.FoodStores(seller));
        Assert.Equal(2, compact.Deliveries);
        Assert.Equal(Fix64.FromInt(24), compact.Delivered);
        Assert.True(compact.Active, "the cargo cap limits each delivery, not the standing Compact");
    }

    [Fact]
    public void Sea10_report_trace_is_checksum_neutral()
    {
        (MatchModel plain, SimRunner plainRunner) = Start(ChapterRules.Fief);
        (MatchModel traced, SimRunner tracedRunner) = Start(ChapterRules.Fief);
        var rows = new System.Collections.Generic.List<string>();
        traced.Politics.FiefSea10TraceObserverForDiagnostics = rows.Add;

        Grant(plainRunner, plain, 0, "KEY-5", "SEA-04", "KEY-6", "SEA-10");
        Grant(tracedRunner, traced, 0, "KEY-5", "SEA-04", "KEY-6", "SEA-10");
        plainRunner.Advance(150);
        tracedRunner.Advance(150);
        (int seller, int buyer) = SeaRoute(plain, 0);
        int buyerCiv = plain.Economy.SettlementLite(buyer).Civilization;

        static void DispatchAndDeliver(MatchModel match, int sellerHub, int buyerCiv)
        {
            int buyerHub = Settlement(match, buyerCiv);
            match.Economy.FixtureStock(sellerHub, "G-SALT", Fix64.FromInt(1_000));
            match.Economy.GiveGoods(buyerHub, Fix64.FromInt(1_000));
            Fix64 buyerSalt = match.Economy.GoodStock(buyerHub, "G-SALT");
            if (buyerSalt > Fix64.Zero) match.Economy.FixtureStock(buyerHub, "G-SALT", -buyerSalt);
            Compact compact = match.Politics.FixtureAddCompact(0, buyerCiv, TradeGood.Salt);
            match.Politics.FixtureSendCaravan(compact);
            Expedition caravan = Assert.Single(match.Politics.Caravans(), e => e.CompactId == compact.Id);
            Assert.True(caravan.Cargo > Fix64.Zero);
            match.Politics.FixtureArriveCaravan(caravan);
        }

        DispatchAndDeliver(plain, seller, buyerCiv);
        DispatchAndDeliver(traced, seller, buyerCiv);
        Assert.Equal(plain.ComputeChecksum(), traced.ComputeChecksum());
        Assert.Contains(rows, row => row.Contains("kind=shipment_dispatched", StringComparison.Ordinal) &&
            row.Contains("route=water", StringComparison.Ordinal) && row.Contains("KEY6=true", StringComparison.Ordinal) &&
            row.Contains("SEA10=true", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Contains("kind=shipment_delivered", StringComparison.Ordinal) &&
            row.Contains("route=water", StringComparison.Ordinal) && row.Contains("paid=", StringComparison.Ordinal));

        plainRunner.Advance(150);
        tracedRunner.Advance(150);
        Assert.Equal(plain.ComputeChecksum(), traced.ComputeChecksum());
        Assert.Contains(rows, row => row.Contains("kind=milestone", StringComparison.Ordinal) && row.Contains("project=KEY-6", StringComparison.Ordinal));
    }

}
