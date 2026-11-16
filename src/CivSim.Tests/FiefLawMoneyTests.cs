using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>First measurable WP-E5-7 rule: chartered representation adds one admin slot.</summary>
public sealed class FiefLawMoneyTests
{
    private const ulong Seed = 20261115;
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner, GeneratedWorld World) Start(ulong seed, CivSim.Sim.Content.ContentPack pack)
    {
        GeneratedWorld world = WorldGenerator.Generate(seed);
        MatchModel match = MatchModel.Create(seed, pack, world, Balanced, default, rules: ChapterRules.Fief);
        return (match, new SimRunner(match), world);
    }

    private static int SettlementFor(MatchModel match, int civ)
        => Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == civ);

    private static void Grant(SimRunner runner, MatchModel match, int civ, params string[] projects)
    {
        foreach (string project in projects)
        {
            int index = match.Research.Catalog.IndexOf(project);
            Assert.True(index >= 0, $"{project} must exist in the test pack");
            if (match.Research.Catalog.Projects[index].Era == ChapterRules.Fief.LastResearchEra)
            {
                int key5 = match.Research.Catalog.IndexOf("KEY-5");
                if (key5 >= 0 && !match.Research.IsDone(civ, key5))
                {
                    runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, key5, 0, 0));
                    runner.Advance(2);
                }
            }
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, index, 0, 0));
            runner.Advance(2);
        }
    }

    [Fact]
    public void GOV11_charter_adds_one_admin_span_in_Fief()
    {
        var pack = TestPack.WithNumeric("GOV-11");
        BronzeFixture before = BronzeFixture.CreateFiefWithCamp(pack, Seed, 100, "KEY-5");
        BronzeFixture charter = BronzeFixture.CreateFiefWithCamp(pack, Seed, 100, "KEY-5", "GOV-11");

        Assert.Equal(before.Economy.AdminSpan(0) + 1, charter.Economy.AdminSpan(0));

        BronzeFixture iron = BronzeFixture.CreateIron(pack, Seed, 100, "KEY-5");
        Assert.Equal(before.Economy.AdminSpan(0), iron.Economy.AdminSpan(0));
    }

    [Fact]
    public void GOV11_charters_city_tier_settlements_for_growth_only_in_Fief()
    {
        var pack = TestPack.WithNumeric("GOV-11");
        BronzeFixture unchartered = BronzeFixture.CreateFiefWithCamp(pack, Seed + 1, 100, "KEY-5");
        unchartered.Economy.FixtureSetTier(0, (int)SettlementTier.City);
        Assert.Equal(Fix64.One, unchartered.Economy.FixtureFiefCityGrowthMultiplier(0));

        BronzeFixture chartered = BronzeFixture.CreateFiefWithCamp(pack, Seed + 1, 100, "KEY-5", "GOV-11");
        chartered.Economy.FixtureSetTier(0, (int)SettlementTier.City);
        Assert.Equal(Fix64.FromRatio(11, 10), chartered.Economy.FixtureFiefCityGrowthMultiplier(0));

        BronzeFixture town = BronzeFixture.CreateFiefWithCamp(pack, Seed + 2, 100, "KEY-5", "GOV-11");
        town.Economy.FixtureSetTier(0, (int)SettlementTier.Town);
        Assert.Equal(Fix64.One, town.Economy.FixtureFiefCityGrowthMultiplier(0));

        BronzeFixture iron = BronzeFixture.CreateIron(pack, Seed + 1, 100, "KEY-5", "GOV-11");
        iron.Economy.FixtureSetTier(0, (int)SettlementTier.City);
        Assert.Equal(Fix64.One, iron.Economy.FixtureFiefCityGrowthMultiplier(0));
    }

    [Fact]
    public void GOV10_halves_matched_keyed_incident_and_Fief_only_gate_is_live()
    {
        var pack = TestPack.WithNumeric("GOV-10");
        (MatchModel control, SimRunner controlRunner, _) = Start(Seed + 10, pack);
        (MatchModel court, SimRunner courtRunner, _) = Start(Seed + 10, pack);
        Grant(controlRunner, control, 0, "KEY-3");
        Grant(courtRunner, court, 0, "KEY-3", "GOV-10");
        int controlSettlement = SettlementFor(control, 0), courtSettlement = SettlementFor(court, 0);
        Assert.True(control.Economy.FiefChain && court.Economy.FiefChain);
        control.Economy.FixtureCohesion(controlSettlement, 67);
        court.Economy.FixtureCohesion(courtSettlement, 67);
        Assert.True(control.Economy.CohesionStatus(controlSettlement).Live);
        Assert.True(court.Economy.CohesionStatus(courtSettlement).Live);

        int chosenYear = -1;
        for (int year = 0; year < 100_000; year++)
        {
            control.Economy.FixtureCohesion(controlSettlement, 67);
            court.Economy.FixtureCohesion(courtSettlement, 67);
            var baseline = control.Politics.FixtureFiefLawEventRoll(controlSettlement, year);
            var protectedRoll = court.Politics.FixtureFiefLawEventRoll(courtSettlement, year);
            Assert.True(baseline.Eligible && protectedRoll.Eligible, $"year {year}: pop={baseline.Population}, live={baseline.Live}, points={baseline.Points}");
            Assert.Equal(baseline.Roll, protectedRoll.Roll);
            Assert.Equal(baseline.Chance / 2, protectedRoll.Chance);
            if (baseline.Roll < baseline.Chance && baseline.Roll >= protectedRoll.Chance)
            {
                chosenYear = year;
                Fix64 controlBefore = control.Economy.SettlementCohesion(controlSettlement);
                Fix64 courtBefore = court.Economy.SettlementCohesion(courtSettlement);
                control.Politics.FixtureResolveFiefLawEvents(year * 4 + 3);
                court.Politics.FixtureResolveFiefLawEvents(year * 4 + 3);
                Assert.Equal(Fix64.FromRatio(6, 1000), controlBefore - control.Economy.SettlementCohesion(controlSettlement));
                Assert.Equal(courtBefore, court.Economy.SettlementCohesion(courtSettlement));
                break;
            }
        }
        Assert.True(chosenYear >= 0, "the seed sweep must find an eligible keyed roll in the full-probability band");

        BronzeFixture iron = BronzeFixture.CreateIron(pack, Seed + 10, 100, "KEY-3", "GOV-10");
        Assert.False(iron.Economy.FiefChain);
        Assert.Equal(Fix64.One, iron.Economy.FixtureFiefCityGrowthMultiplier(0));
        GeneratedWorld legacyWorld = WorldGenerator.Generate(Seed + 11);
        MatchModel legacy = MatchModel.Create(Seed + 11, pack, legacyWorld, Balanced, default, rules: ChapterRules.Iron);
        legacy.Economy.FixtureComplete(0, "KEY-3", "GOV-10");
        int legacySettlement = SettlementFor(legacy, 0);
        legacy.Economy.FixtureCohesion(legacySettlement, 67);
        Fix64 legacyBefore = legacy.Economy.SettlementCohesion(legacySettlement);
        legacy.Politics.FixtureResolveFiefLawEvents(chosenYear * 4 + 3);
        Assert.Equal(legacyBefore, legacy.Economy.SettlementCohesion(legacySettlement));
    }

    [Fact]
    public void WP7_diagnostic_observer_is_checksum_neutral_and_emits_eligible_denominator_rows()
    {
        var pack = TestPack.WithNumeric("GOV-10");
        (MatchModel quiet, SimRunner quietRunner, _) = Start(Seed + 12, pack);
        (MatchModel traced, SimRunner tracedRunner, _) = Start(Seed + 12, pack);
        Grant(quietRunner, quiet, 0, "KEY-3");
        Grant(tracedRunner, traced, 0, "KEY-3");
        int a = SettlementFor(quiet, 0), b = SettlementFor(traced, 0);
        quiet.Economy.FixtureCohesion(a, 67);
        traced.Economy.FixtureCohesion(b, 67);
        var rows = new System.Collections.Generic.List<string>();
        traced.Politics.FiefWp7TraceObserverForDiagnostics = rows.Add;
        Assert.Equal(quiet.ComputeChecksum(), traced.ComputeChecksum());

        int year = 0;
        var baseRoll = quiet.Politics.FixtureFiefLawEventRoll(a, year);
        Assert.True(baseRoll.Eligible);
        quiet.Politics.FixtureResolveFiefLawEvents(year * 4 + 3);
        traced.Politics.FixtureResolveFiefLawEvents(year * 4 + 3);
        Assert.Equal(quiet.ComputeChecksum(), traced.ComputeChecksum());
        Assert.Contains(rows, row => row.Contains("wp7-gov10", System.StringComparison.Ordinal) &&
            row.Contains("eligible=true", System.StringComparison.Ordinal) && row.Contains("hit=", System.StringComparison.Ordinal));
    }

    [Fact]
    public void TRD08_credit_claims_protect_food_partial_pay_and_resume_exactly_from_snapshot()
    {
        const ulong seed = Seed + 20;
        var pack = TestPack.WithNumeric("TRD-08");
        (MatchModel match, SimRunner runner, GeneratedWorld world) = Start(seed, pack);
        Grant(runner, match, 1, "KEY-5", "TRD-08");
        int debtor = SettlementFor(match, 1), creditor = SettlementFor(match, 0);
        Fix64 copperNeed = match.Economy.TradeNeed(debtor, "G-COPPER", 4);
        match.Economy.PutGood(debtor, "G-COPPER", copperNeed + Fix64.FromInt(10));
        match.Politics.FixtureOriginateFiefCredit(1, 0, debtor, creditor, Fix64.FromInt(1000));
        Fix64 originalBalance = match.Politics.FixtureFiefCreditBalance;
        Fix64 creditorCopperBefore = match.Economy.GoodStock(creditor, "G-COPPER");
        Assert.Equal(1, match.Politics.FixtureFiefCreditClaimCount);

        // Empty food stocks: a seasonal collection is suspended until forecast needs plus the one-season reserve are met.
        match.Economy.TakeGoods(debtor, match.Economy.FoodStores(debtor), LootPreference.FoodOnly);
        var traceRows = new System.Collections.Generic.List<string>();
        match.Politics.FiefWp7TraceObserverForDiagnostics = traceRows.Add;
        match.Politics.FixtureCollectFiefCreditClaims(1);
        Assert.Equal(originalBalance, match.Politics.FixtureFiefCreditBalance);
        Assert.Equal(creditorCopperBefore, match.Economy.GoodStock(creditor, "G-COPPER"));

        Fix64 foodReserve = match.Economy.TradeNeed(debtor, "G-FOOD", 5);
        match.Economy.GiveGoods(debtor, foodReserve + Fix64.One);
        match.Politics.FixtureCollectFiefCreditClaims(2);
        Assert.True(match.Politics.FixtureFiefCreditBalance < originalBalance, "the surplus copper units are a partial payment at their locked origination rate");
        Assert.True(match.Economy.GoodStock(creditor, "G-COPPER") > creditorCopperBefore);
        Assert.Contains(traceRows, row => row.Contains("event=claim_season", System.StringComparison.Ordinal) && row.Contains("foodEligible=false", System.StringComparison.Ordinal));
        string paidRow = Assert.Single(traceRows, row => row.Contains("event=payment", System.StringComparison.Ordinal));
        static long Field(string row, string name) => long.Parse(row.Split(' ').Single(part => part.StartsWith(name + "=", System.StringComparison.Ordinal)).Split('=')[1]);
        Assert.Equal(Field(paidRow, "opening") - Field(paidRow, "paid"), Field(paidRow, "closing"));
        Assert.Contains("routeIndependent=true", paidRow, System.StringComparison.Ordinal);

        ulong checksum = match.ComputeChecksum();
        using var memory = new MemoryStream();
        using (var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, leaveOpen: true)) match.WriteSnapshot(writer);
        memory.Position = 0;
        MatchModel restored = MatchModel.Create(seed, pack, world, Balanced, default, rules: ChapterRules.Fief);
        using (var reader = new BinaryReader(memory, System.Text.Encoding.UTF8, leaveOpen: true)) restored.ReadSnapshot(reader);
        Assert.Equal(checksum, restored.ComputeChecksum());
        Assert.Equal(match.Politics.FixtureFiefCreditBalance, restored.Politics.FixtureFiefCreditBalance);
    }

    [Fact]
    public void TRD08_claim_survives_compact_termination_and_is_written_off_with_debtor_loss()
    {
        var pack = TestPack.WithNumeric("TRD-08");
        (MatchModel match, SimRunner runner, _) = Start(Seed + 30, pack);
        Grant(runner, match, 1, "KEY-5", "TRD-08");
        int debtor = SettlementFor(match, 1), creditor = SettlementFor(match, 0);
        Fix64 copperNeed = match.Economy.TradeNeed(debtor, "G-COPPER", 4);
        match.Economy.FixtureStock(debtor, "G-COPPER", copperNeed + Fix64.FromInt(10));
        match.Economy.GiveGoods(debtor, match.Economy.TradeNeed(debtor, "G-FOOD", 5) + Fix64.One);
        match.Politics.FixtureOriginateFiefCredit(1, 0, debtor, creditor, Fix64.FromInt(100));
        Fix64 balance = match.Politics.FixtureFiefCreditBalance;
        Fix64 creditorCopper = match.Economy.GoodStock(creditor, "G-COPPER");
        var compact = match.Politics.FixtureAddCompact(0, 1, TradeGood.Food);
        match.Politics.FixtureEndCompact(compact);
        Assert.False(compact.Active);
        Assert.Equal(balance, match.Politics.FixtureFiefCreditBalance);
        Assert.Equal(1, match.Politics.FixtureFiefCreditClaimCount);
        match.Politics.FixtureCollectFiefCreditClaims(1);
        Assert.True(match.Politics.FixtureFiefCreditBalance < balance);
        Assert.True(match.Economy.GoodStock(creditor, "G-COPPER") > creditorCopper);

        match.Economy.FixtureRemovePeople(debtor, int.MaxValue);
        match.Politics.FixtureCollectFiefCreditClaims(1);
        Assert.Equal(0, match.Politics.FixtureFiefCreditClaimCount);
    }

    [Fact]
    public void TRD08_claim_expires_after_eight_seasons_without_payment()
    {
        var pack = TestPack.WithNumeric("TRD-08");
        (MatchModel match, SimRunner runner, _) = Start(Seed + 31, pack);
        Grant(runner, match, 1, "KEY-5", "TRD-08");
        int debtor = SettlementFor(match, 1), creditor = SettlementFor(match, 0);
        foreach (TradeGood good in new[] { TradeGood.Copper, TradeGood.Tin, TradeGood.Bronze, TradeGood.Salt, TradeGood.Gold, TradeGood.Luxury, TradeGood.Iron })
            match.Economy.FixtureStock(debtor, DecreeArgs.GoodId(good), -match.Economy.GoodStock(debtor, DecreeArgs.GoodId(good)));
        match.Politics.FixtureOriginateFiefCredit(1, 0, debtor, creditor, Fix64.FromInt(100));
        Assert.Equal(1, match.Politics.FixtureFiefCreditClaimCount);
        match.Politics.FixtureCollectFiefCreditClaims(8);
        Assert.Equal(0, match.Politics.FixtureFiefCreditClaimCount);
    }


    [Fact]
    public void TRD08_arrival_delivers_full_caravan_cargo_and_records_unpaid_invoice()
    {
        var pack = TestPack.WithNumeric("TRD-08");
        (MatchModel match, SimRunner runner, _) = Start(Seed + 32, pack);
        Grant(runner, match, 1, "KEY-5", "TRD-08");
        int seller = SettlementFor(match, 0), buyer = SettlementFor(match, 1);
        match.Economy.FixtureStock(buyer, "G-COPPER", Fix64.FromInt(10));
        var compact = match.Politics.FixtureAddCompact(0, 1, TradeGood.Copper);
        Fix64 buyerCopper = match.Economy.GoodStock(buyer, "G-COPPER");
        var caravan = new Expedition
        {
            Kind = ExpeditionKind.Caravan, Civ = 0, TargetCiv = 1, FromSettlement = seller, ToSettlement = buyer,
            Good = TradeGood.Copper, Cargo = Fix64.FromInt(2), Value = Fix64.FromInt(50), Id = 42, CompactId = compact.Id,
        };
        var traceRows = new System.Collections.Generic.List<string>();
        match.Politics.FiefWp7TraceObserverForDiagnostics = traceRows.Add;
        match.Politics.FixtureArriveCaravan(caravan);
        Assert.Equal(buyerCopper + caravan.Cargo, match.Economy.GoodStock(buyer, "G-COPPER"));
        Assert.Equal(Fix64.FromInt(100), match.Politics.FixtureFiefCreditBalance);
        Assert.Equal(1, match.Politics.FixtureFiefCreditClaimCount);
        string transaction = Assert.Single(traceRows, row => row.Contains("wp7-trd09-transaction", System.StringComparison.Ordinal));
        Assert.Contains("dispatchFrozenUnitValue=", transaction, System.StringComparison.Ordinal);
        Assert.Contains("delivered=", transaction, System.StringComparison.Ordinal);
        Assert.Contains("paid=", transaction, System.StringComparison.Ordinal);
        Assert.Contains("stockDeltas=", transaction, System.StringComparison.Ordinal);
    }

}
