using CivSim.Sim.Core;
using CivSim.Sim.Content;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;
using System;
using System.Linq;
using System.Text;

namespace CivSim.Tests;

public sealed class FiefWeaponReadinessTests
{
    private const ulong Seed = 20261118;
    private static Fix64 F(string value) => Fix64.Parse(value);

    [Fact]
    public void Current_season_metal_weapon_output_is_not_aged_after_production()
    {
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100,
            "KEY-5", "MET-10", "MET-11", "WAR-08");
        LivingEconomyModel economy = fixture.Economy;
        economy.FixtureSetMetalFacilities(0, forges: 1, smelters: 0);
        var trace = new StringBuilder();
        economy.MetalDebug = trace;
        economy.MetalDebugSettlementId = 0;

        fixture.RunSeasons(1);

        System.Text.RegularExpressions.Match output = System.Text.RegularExpressions.Regex.Match(trace.ToString(), @"cast_arms1:[^>]+->(?<made>[0-9.]+)\(pool");
        Assert.True(output.Success, trace.ToString());
        Fix64 plannedOutput = F(output.Groups["made"].Value);
        Fix64 stock = economy.GoodStock(0, "G-ARMS1");
        Assert.True(plannedOutput > Fix64.One, $"the deterministic fixture should complete a whole weapon: {trace}");
        Assert.True(Fix64.Abs(stock - plannedOutput) < F("0.01"), $"new output {plannedOutput} was aged before use: stock is {stock}; {trace}");
    }

    [Fact]
    public void Lower_tier_stock_does_not_suppress_the_professional_upgrade_target()
    {
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed + 3, 100,
            "KEY-5", "MET-10", "MET-11", "WAR-08");
        LivingEconomyModel economy = fixture.Economy;
        economy.FixtureSetMetalFacilities(0, forges: 1, smelters: 0);
        economy.FixtureSetSoldierRoles(0, 1);
        economy.FixtureSetSoldiersSupplied(0, 1);
        economy.FixtureSetGoodStock(0, "G-ARMS3", Fix64.FromInt(8));
        Assert.Equal(Fix64.FromInt(2), economy.FixtureProfessionalArmsWant(0, "G-ARMS5"));
    }

    [Fact]
    public void Chariot_and_engine_targets_produce_whole_units_and_ignore_fractional_stock()
    {
        BronzeFixture oneChariot = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed + 4, 50, "KEY-5", "WAR-06");
        BronzeFixture twoChariots = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed + 5, 100, "KEY-5", "WAR-06");
        foreach ((BronzeFixture fixture, int target) in new[] { (oneChariot, 1), (twoChariots, 2) })
        {
            fixture.Economy.FixtureSetMetalFacilities(0, forges: 1, smelters: 0);
            fixture.Economy.FixtureSetGoodStock(0, "G-WHEEL", Fix64.FromInt(target * 3));
            fixture.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(target * 4));
            fixture.Economy.FixtureSetGoodStock(0, "G-BRONZE", Fix64.FromInt(target));
            fixture.Economy.FixtureSetGoodStock(0, "G-LEATHER", Fix64.FromInt(target * 2));
            Assert.Equal(Fix64.FromInt(target), fixture.Economy.FixtureChariotWant(0));
            EconomyRecipe[] recipes = EconomyContent.FromPack(TestPack.Pack).Recipes.ToArray();
            Assert.True(recipes.Single(r => r.Id == "make_wheel").Order < recipes.Single(r => r.Id == "build_cart").Order);
            Assert.True(recipes.Single(r => r.Id == "build_cart").Order < recipes.Single(r => r.Id == "build_chariot").Order);
            Fix64 made = fixture.Economy.FixtureRunRecipe(0, "build_chariot", Fix64.FromInt(target), Fix64.FromInt(target * 3));
            Assert.Equal(Fix64.FromInt(target), made);
            Assert.Equal(target, fixture.Economy.GoodStock(0, "G-CHARIOT").FloorToInt());
        }

        BronzeFixture engines = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed + 6, 100, "KEY-5", "WAR-09");
        engines.Economy.FixtureSetMetalFacilities(0, forges: 1, smelters: 0);
        engines.Economy.FixtureSetSoldiersSupplied(0, 8);
        engines.Economy.FixtureSetGoodStock(0, "G-ENGINE", F("0.75"));
        Assert.Equal(Fix64.FromInt(2), engines.Economy.FixtureEngineWant(0));
        engines.Economy.FixtureSetGoodStock(0, "G-ENGINE", Fix64.Zero);
        engines.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(18));
        engines.Economy.FixtureSetGoodStock(0, "G-BLOCK", Fix64.FromInt(10));
        engines.Economy.FixtureSetGoodStock(0, "G-WROUGHT", Fix64.One);
        Fix64 engineOutput = engines.Economy.FixtureRunRecipe(0, "make_siege_engine", Fix64.FromInt(2), Fix64.FromInt(48));
        Assert.Equal(Fix64.FromInt(2), engineOutput);
        Assert.Equal(2, engines.Economy.GoodStock(0, "G-ENGINE").FloorToInt());
    }

    [Fact]
    public void Existing_one_unit_toward_two_plans_exactly_one_more_wheel_chariot_and_engine()
    {
        ContentPack wheelPack = TestPack.WithNumeric("KEY-5", "MEC-03");
        BronzeFixture wheels = BronzeFixture.CreateFiefWithCamp(wheelPack, Seed + 10, 100,
            "KEY-5", "MEC-03");
        wheels.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(10));
        wheels.Economy.FixtureSetGoodStock(0, "G-WHEEL", Fix64.One);
        Assert.Equal(Fix64.One, wheels.Economy.FixturePlannedMilitaryRecipeBatches(0, "make_wheel"));

        ContentPack bronzePack = TestPack.WithNumeric("KEY-5", "WAR-06");
        BronzeFixture bronze = BronzeFixture.CreateFiefWithCamp(bronzePack, Seed + 13, 100,
            "KEY-5", "WAR-06");
        bronze.Economy.FixtureSetMetalFacilities(0, forges: 1, smelters: 0);
        bronze.Economy.FixtureSetGoodStock(0, "G-CHARIOT", Fix64.One);
        bronze.Economy.FixtureSetGoodStock(0, "G-WHEEL", Fix64.FromInt(3));
        bronze.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(10));
        bronze.Economy.FixtureSetGoodStock(0, "G-BRONZE", Fix64.One);
        bronze.Economy.FixtureSetGoodStock(0, "G-LEATHER", Fix64.FromInt(2));
        Assert.Equal(Fix64.One, bronze.Economy.FixturePlannedMilitaryRecipeBatches(0, "build_chariot"));

        ContentPack ironPack = TestPack.WithNumeric("KEY-5", "MET-08", "WAR-08", "WAR-09", "MIN-03");
        BronzeFixture iron = BronzeFixture.CreateIronWithCamp(ironPack, Seed + 11, 60,
            "KEY-5", "MET-08", "WAR-08", "WAR-09", "MIN-03");
        iron.Economy.FixtureSetMetalFacilities(0, forges: 1, smelters: 1);
        iron.Economy.FixtureSetSoldiersSupplied(0, 8);
        iron.Economy.FixtureSetGoodStock(0, "G-ENGINE", Fix64.One);
        iron.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(16));
        iron.Economy.FixtureSetGoodStock(0, "G-BLOCK", Fix64.FromInt(4));
        iron.Economy.FixtureSetGoodStock(0, "G-WROUGHT", Fix64.One);
        Assert.Equal(Fix64.One, iron.Economy.FixturePlannedMilitaryRecipeBatches(0, "make_siege_engine"));
    }

    [Fact]
    public void Assembly_reserve_is_capped_at_twenty_percent_after_tools()
    {
        ContentPack pack = TestPack.WithNumeric("KEY-5");
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(pack, Seed + 20, 40, "KEY-5");

        (Fix64 remaining, Fix64 reserved, _) =
            fixture.Economy.FixtureCraftToolsWithAssemblyReserve(0, Fix64.FromInt(10), Fix64.FromInt(8));

        Assert.True(reserved > Fix64.Zero && reserved <= Fix64.FromInt(2),
            $"the reserve must be positive but cannot exceed 20% of the initial ten work-seasons after essential tools: {reserved}");
        Assert.True(remaining >= reserved, "the held share remains available to the finishing recipe pass");
    }

    [Fact]
    public void Missing_military_recipe_inputs_create_no_assembly_demand()
    {
        ContentPack pack = TestPack.WithNumeric("KEY-5", "WAR-06");
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(pack, Seed + 21, 100, "KEY-5", "WAR-06");
        fixture.Economy.FixtureSetMetalFacilities(0, forges: 1, smelters: 0);
        fixture.Economy.FixtureSetGoodStock(0, "G-WHEEL", Fix64.FromInt(8));
        fixture.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.Zero); // the only missing prerequisite
        fixture.Economy.FixtureSetGoodStock(0, "G-BRONZE", Fix64.FromInt(8));
        fixture.Economy.FixtureSetGoodStock(0, "G-LEATHER", Fix64.FromInt(8));

        Assert.Equal(Fix64.Zero, fixture.Economy.FixturePlanMilitaryAssemblyNeed(0, Fix64.FromInt(80)));
    }

    [Fact]
    public void Advanced_weapon_and_siege_finishing_recipes_are_readiness_first()
    {
        ContentPack pack = TestPack.WithNumeric("KEY-5", "WAR-11", "MET-11", "WAR-12", "CHM-07");
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(pack, Seed + 22, 100, "KEY-5");
        foreach (string recipe in new[]
                 { "make_crossbow", "make_longbow", "make_crucible_arms", "cast_cannon", "make_powder" })
        {
            (bool militaryAssembly, bool deferred) = fixture.Economy.FixtureFinishingRecipePolicy(recipe);
            Assert.True(militaryAssembly, $"{recipe} must participate in readiness allocation");
            Assert.False(deferred, $"{recipe} must run before discretionary finishing output");
        }

        (bool ashlarMilitary, bool ashlarDeferred) = fixture.Economy.FixtureFinishingRecipePolicy("dress_stone");
        Assert.False(ashlarMilitary);
        Assert.True(ashlarDeferred, "Ashlar remains behind military readiness when inputs or craft work are contested");
    }

    [Fact]
    public void Advanced_weapons_and_siege_goods_run_through_the_metal_plan()
    {
        ContentPack pack = TestPack.WithNumeric("KEY-5", "WAR-08", "WAR-11", "MET-11", "WAR-12", "CHM-07", "MET-12", "STN-11");
        string[] projects = ["KEY-5", "WAR-08", "WAR-11", "MET-11", "WAR-12", "CHM-07", "MET-12", "STN-11"];
        string[][] cases =
        [
            ["make_crossbow", "G-CROSSBOW"], ["make_longbow", "G-LONGBOW"],
            ["make_crucible_arms", "G-ARMS5"], ["cast_cannon", "G-CANNON"], ["make_powder", "G-POWDER"],
        ];

        foreach (string[] recipeCase in cases)
        {
            BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(pack, Seed + 23, 100, projects);
            var economy = fixture.Economy;
            economy.FixtureSetMetalFacilities(0, forges: recipeCase[0] == "make_longbow" ? 0 : 1, smelters: 0);
            economy.FixtureSetSoldierRoles(0, 1);
            economy.FixtureSetSoldiersSupplied(0, 1);
            economy.FixtureBuildIronStructure(0, "blast_furnace");
            economy.FixtureBuildIronStructure(0, "powder_mill");
            economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(200));
            economy.FixtureSetGoodStock(0, "G-WROUGHT", Fix64.FromInt(20));
            economy.FixtureSetGoodStock(0, "G-CORD", Fix64.FromInt(20));
            economy.FixtureSetGoodStock(0, "G-CSTEEL", Fix64.FromInt(20));
            economy.FixtureSetGoodStock(0, "G-STEEL", Fix64.FromInt(20));
            economy.FixtureSetGoodStock(0, "G-LEATHER", Fix64.FromInt(20));
            economy.FixtureSetGoodStock(0, "G-CAST", Fix64.FromInt(20));
            economy.FixtureSetGoodStock(0, "G-SALTPETER", Fix64.FromInt(20));
            economy.FixtureSetGoodStock(0, "G-SULFUR", Fix64.FromInt(20));
            economy.FixtureSetGoodStock(0, "G-CHAR", Fix64.FromInt(20));
            economy.FixtureSetGoodStock(0, "G-BLOCK", Fix64.FromInt(20));
            economy.FixtureSetGoodStock(0, "G-STONE", Fix64.FromInt(100));
            economy.FixtureSetGoodStock(0, "G-ASHLAR", Fix64.Zero);
            var trace = new StringBuilder();
            economy.MetalDebug = trace;
            economy.MetalDebugSettlementId = 0;

            economy.FixtureRunMetalPlan(0, Fix64.FromInt(500), Fix64.FromInt(500));

            Assert.True(economy.GoodStock(0, recipeCase[1]) > Fix64.Zero,
                $"{recipeCase[0]} should make its planned readiness output through RunMetal: {trace}");
            string traceText = trace.ToString();
            int readinessAt = traceText.IndexOf(recipeCase[0] + ":", StringComparison.Ordinal);
            Assert.True(readinessAt >= 0, $"{recipeCase[0]} should appear in the actual recipe trace: {traceText}");
            int optionalAt = traceText.IndexOf("dress_stone:", StringComparison.Ordinal);
            Assert.True(optionalAt < 0 || readinessAt < optionalAt,
                $"{recipeCase[0]} should run before discretionary Ashlar: {traceText}");
        }
    }

    [Fact]
    public void Tool_starved_season_does_not_hold_craft_for_military_assembly()
    {
        ContentPack pack = TestPack.WithNumeric("KEY-5", "STN-01");
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(pack, Seed + 19, 40, "KEY-5", "STN-01");
        fixture.Economy.FixtureSetGoodStock(0, "G-STONE", Fix64.FromInt(10));
        fixture.Economy.FixtureSetGoodStock(0, "G-TOOL1", Fix64.Zero);

        (Fix64 remaining, Fix64 reserved, Fix64 toolStock) =
            fixture.Economy.FixtureCraftToolsWithAssemblyReserve(0, F("0.05"), Fix64.One);

        Assert.True(toolStock > Fix64.Zero, "the available work repairs the essential tool first");
        Assert.True(reserved <= F("0.01"), $"tools should consume nearly all work before assembly can reserve any: {reserved}");
        Assert.True(remaining <= F("0.01"), $"the small reserve remainder must be returned: {remaining}");
    }

    [Fact]
    public void Live_season_funds_planned_chariot_assembly_after_essential_work()
    {
        ContentPack pack = TestPack.WithNumeric("KEY-5", "WAR-06");
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(pack, Seed + 16, 100, "KEY-5", "WAR-06");
        fixture.Economy.FixtureSetMetalFacilities(0, forges: 1, smelters: 0);
        fixture.Economy.FixtureSetGoodStock(0, "G-WHEEL", Fix64.FromInt(8));
        fixture.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(40));
        fixture.Economy.FixtureSetGoodStock(0, "G-BRONZE", Fix64.FromInt(8));
        fixture.Economy.FixtureSetGoodStock(0, "G-LEATHER", Fix64.FromInt(8));
        var trace = new StringBuilder();
        fixture.Economy.MetalDebug = trace;
        fixture.Economy.MetalDebugSettlementId = 0;

        fixture.RunSeasons(1);

        Assert.True(fixture.Economy.GoodStock(0, "G-CHARIOT") > Fix64.Zero,
            $"planned chariot work should receive a bounded post-tool craft allocation: {trace}");
        string traceText = trace.ToString();
        Assert.Contains("assemblyNeed", traceText);
        Assert.Contains("toolsBefore", traceText);
        System.Text.RegularExpressions.Match reserved = System.Text.RegularExpressions.Regex.Match(traceText, @"assemblyReserved (?<work>[0-9.]+)");
        Assert.True(reserved.Success && F(reserved.Groups["work"].Value) > Fix64.Zero, traceText);
        System.Text.RegularExpressions.Match made = System.Text.RegularExpressions.Regex.Match(traceText, @"build_chariot:(?<planned>[0-9.]+)->(?<made>[0-9.]+)");
        Assert.True(made.Success && F(made.Groups["planned"].Value) > Fix64.Zero && F(made.Groups["made"].Value) > Fix64.Zero, traceText);
    }

    [Fact]
    public void Engine_only_block_demand_quarries_stone_before_the_engine_recipe()
    {
        ContentPack pack = TestPack.WithNumeric("KEY-5", "MET-08", "WAR-08", "WAR-09", "MIN-03");
        BronzeFixture fixture = BronzeFixture.CreateIronWithCamp(pack, Seed + 12, 60,
            "KEY-5", "MET-08", "WAR-08", "WAR-09", "MIN-03");
        fixture.Economy.FixtureSetMetalFacilities(0, forges: 1, smelters: 1);
        fixture.Economy.FixtureSetSoldiersSupplied(0, 8);
        fixture.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(24));
        fixture.Economy.FixtureSetGoodStock(0, "G-WROUGHT", Fix64.FromInt(2));
        fixture.Economy.FixtureSetGoodStock(0, "G-BLOCK", Fix64.Zero);
        fixture.Economy.FixtureSetRangeStone(0, Fix64.FromInt(40));
        Fix64 stoneBefore = fixture.Economy.FixtureRangeStone(0);
        fixture.Economy.MetalDebug = new StringBuilder();
        fixture.Economy.MetalDebugSettlementId = 0;

        fixture.Economy.FixtureRunMetalPlan(0, Fix64.FromInt(80), Fix64.FromInt(80));

        Assert.True(fixture.Economy.GoodStock(0, "G-ENGINE") > Fix64.Zero,
            $"engine demand should obtain its blocks through quarrying: {fixture.Economy.MetalDebug}");
        Assert.True(fixture.Economy.FixtureRangeStone(0) < stoneBefore,
            "stone should be quarried for the engine even with no Ashlar demand");
        string traceText = fixture.Economy.MetalDebug.ToString();
        Assert.Contains("inputs=[", traceText);
        Assert.Contains("G-WOOD", traceText);
        Assert.Contains("assemblyNeed", traceText);
        System.Text.RegularExpressions.Match made = System.Text.RegularExpressions.Regex.Match(traceText, @"make_siege_engine:(?<planned>[0-9.]+)->(?<made>[0-9.]+)");
        Assert.True(made.Success && F(made.Groups["planned"].Value) > Fix64.Zero && F(made.Groups["made"].Value) > Fix64.Zero, traceText);
    }

    [Fact]
    public void Feasible_engine_batches_use_blocks_before_discretionary_ashlar()
    {
        ContentPack pack = TestPack.WithNumeric("KEY-5", "MET-08", "WAR-08", "WAR-09", "MIN-03", "STN-11");
        BronzeFixture fixture = BronzeFixture.CreateIronWithCamp(pack, Seed + 17, 60,
            "KEY-5", "MET-08", "WAR-08", "WAR-09", "MIN-03", "STN-11");
        fixture.Economy.FixtureSetMetalFacilities(0, forges: 1, smelters: 1);
        fixture.Economy.FixtureSetSoldiersSupplied(0, 8);
        fixture.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(24));
        fixture.Economy.FixtureSetGoodStock(0, "G-WROUGHT", Fix64.FromInt(2));
        fixture.Economy.FixtureSetGoodStock(0, "G-BLOCK", Fix64.Zero);
        fixture.Economy.FixtureSetRangeStone(0, Fix64.FromInt(8));

        fixture.Economy.FixtureRunMetalPlan(0, Fix64.FromInt(80), Fix64.FromInt(80));

        Assert.Equal(2, fixture.Economy.GoodStock(0, "G-ENGINE").FloorToInt());
        Assert.Equal(Fix64.Zero, fixture.Economy.GoodStock(0, "G-ASHLAR"));
    }

    [Fact]
    public void No_stone_engine_has_no_feasible_assembly_reservation()
    {
        ContentPack pack = TestPack.WithNumeric("KEY-5", "MET-08", "WAR-08", "WAR-09", "MIN-03");
        BronzeFixture fixture = BronzeFixture.CreateIronWithCamp(pack, Seed + 18, 60,
            "KEY-5", "MET-08", "WAR-08", "WAR-09", "MIN-03");
        fixture.Economy.FixtureSetMetalFacilities(0, forges: 1, smelters: 1);
        fixture.Economy.FixtureSetSoldiersSupplied(0, 8);
        fixture.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(24));
        fixture.Economy.FixtureSetGoodStock(0, "G-WROUGHT", Fix64.FromInt(2));
        fixture.Economy.FixtureSetGoodStock(0, "G-BLOCK", Fix64.Zero);
        fixture.Economy.FixtureSetRangeStone(0, Fix64.Zero);

        Assert.Equal(Fix64.Zero, fixture.Economy.FixturePlanMilitaryAssemblyNeed(0, Fix64.FromInt(80)));
    }

    [Fact]
    public void Fractional_engine_assembly_is_not_aged_as_finished_stock()
    {
        BronzeFixture fixture = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed + 14, 60, "MET-08", "WAR-08");
        fixture.Economy.FixtureSetGoodStock(0, "G-ENGINE", F("0.75"));

        fixture.Economy.FixtureWearMilitaryStockBeforePlanning(0);

        Assert.Equal(F("0.75"), fixture.Economy.GoodStock(0, "G-ENGINE"));
    }

    [Fact]
    public void Assembly_reserve_follows_essential_tool_replenishment()
    {
        ContentPack pack = TestPack.WithNumeric("KEY-5", "STN-01");
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(pack, Seed + 15, 40, "KEY-5", "STN-01");
        fixture.Economy.FixtureSetGoodStock(0, "G-STONE", Fix64.FromInt(100));
        fixture.Economy.FixtureSetGoodStock(0, "G-TOOL1", Fix64.Zero);

        (Fix64 remaining, Fix64 reserved, Fix64 toolStock) =
            fixture.Economy.FixtureCraftToolsWithAssemblyReserve(0, Fix64.FromInt(10), F("0.25"));

        Assert.True(toolStock > Fix64.Zero, "the essential stone tool is made before assembly labor is held");
        Assert.Equal(F("0.25"), reserved);
        Assert.True(remaining >= reserved, "the held assembly work is returned for finishing recipes");
    }

    [Fact]
    public void War11_bow_replacement_demand_returns_after_stock_wears_before_planning()
    {
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed + 7, 100, "KEY-5", "WAR-11");
        fixture.Economy.FixtureSetMetalFacilities(0, forges: 1, smelters: 0);
        fixture.Economy.FixtureSetGoodStock(0, "G-CROSSBOW", Fix64.FromInt(40));
        fixture.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(120));
        fixture.Economy.FixtureSetGoodStock(0, "G-WROUGHT", Fix64.FromInt(10));
        fixture.Economy.FixtureSetGoodStock(0, "G-CORD", Fix64.FromInt(20));
        Assert.Equal(Fix64.Zero, fixture.Economy.FixtureFiefBowWant(0).Crossbows);
        fixture.Economy.FixtureWearMilitaryStockBeforePlanning(0);
        Assert.True(fixture.Economy.FixtureFiefBowWant(0).Crossbows > Fix64.Zero);
    }

    [Fact]
    public void War11_without_crossbow_inputs_keeps_g_bow_replacements_for_uncovered_levies()
    {
        ContentPack pack = TestPack.WithNumeric("KEY-5", "HNT-06", "WAR-03", "WAR-11");
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(pack, Seed + 8, 100,
            "KEY-5", "HNT-06", "WAR-03", "WAR-11");
        fixture.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(20));
        fixture.Economy.FixtureSetGoodStock(0, "G-CORD", Fix64.Zero);
        fixture.Economy.FixtureSetGoodStock(0, "G-WROUGHT", Fix64.Zero);
        fixture.Economy.FixtureSetGoodStock(0, "G-CROSSBOW", Fix64.Zero);
        fixture.Economy.FixtureSetGoodStock(0, "G-LONGBOW", Fix64.Zero);

        fixture.Economy.FixtureMakeLevyBows(0, Fix64.FromInt(10));

        Assert.True(fixture.Economy.GoodStock(0, "G-BOW") > Fix64.Zero,
            "WAR-11 must retain cheap G-BOW replacements while advanced bow inputs are unavailable");
        Assert.Equal(Fix64.Zero, fixture.Economy.GoodStock(0, "G-CROSSBOW"));
        Assert.Equal(Fix64.Zero, fixture.Economy.GoodStock(0, "G-LONGBOW"));
    }

    [Fact]
    public void Ready_bow_stock_reduces_redundant_metal_arm_demand()
    {
        ContentPack pack = TestPack.WithNumeric("KEY-5", "MET-03", "HNT-06", "WAR-03", "WAR-11");
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(pack, Seed + 9, 100,
            "KEY-5", "MET-03", "HNT-06", "WAR-03", "WAR-11");
        fixture.Economy.FixtureSetMetalFacilities(0, forges: 1, smelters: 0);
        fixture.Economy.FixtureSetGoodStock(0, "G-COPPER", Fix64.FromInt(4));
        Fix64 withoutBows = fixture.Economy.FixtureBaselineArmsWant(0);
        fixture.Economy.FixtureSetGoodStock(0, "G-BOW", Fix64.FromInt(4));
        fixture.Economy.FixtureSetGoodStock(0, "G-CROSSBOW", Fix64.FromInt(3));
        fixture.Economy.FixtureSetGoodStock(0, "G-LONGBOW", Fix64.FromInt(3));

        Fix64 withBows = fixture.Economy.FixtureBaselineArmsWant(0);

        Assert.Equal(Fix64.FromInt(10), withoutBows - withBows);
    }

    [Fact]
    public void Finished_arms_share_only_whole_units_within_reach_and_preserve_donor_coverage()
    {
        BronzeFixture fixture = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed + 1, 60, "MET-08", "WAR-08");
        LivingEconomyModel economy = fixture.Economy;
        economy.FixtureStock(0, "G-CHAR", Fix64.FromInt(60));
        int daughter = economy.FoundByDecree(0, fixture.CampTile, F("0.4"), minDaughter: 6, startCohesion: 60);
        Assert.True(daughter >= 0);
        economy.FixtureSetGoodStock(0, "G-ARMS3", Fix64.FromInt(30));

        economy.FixtureShareFinishedMilitaryGoods(0);

        Assert.True(economy.GoodStock(daughter, "G-ARMS3") > Fix64.Zero);
        Assert.True(economy.GoodStock(0, "G-ARMS3") > Fix64.Zero);
        Assert.Equal(Fix64.FromInt(30), economy.GoodStock(0, "G-ARMS3") + economy.GoodStock(daughter, "G-ARMS3"));

        GeneratedWorld world = WorldGenerator.Generate(Seed + 1);
        int parentTile = economy.SettlementLite(0).SiteTile;
        int farTile = Enumerable.Range(0, world.Tiles.Length).MaxBy(tile => HexGrid.Distance(parentTile, tile, world.Width));
        Assert.True(HexGrid.Distance(parentTile, farTile, world.Width) > 15, "the fixture map must offer a disconnected site");
        economy.FixtureMoveSite(daughter, farTile);
        economy.FixtureSetGoodStock(0, "G-ARMS3", Fix64.FromInt(30));
        economy.FixtureSetGoodStock(daughter, "G-ARMS3", Fix64.Zero);
        economy.FixtureShareFinishedMilitaryGoods(0);
        Assert.Equal(Fix64.Zero, economy.GoodStock(daughter, "G-ARMS3"));

        economy.FixtureSetGoodStock(0, "G-ARMS3", F("0.75"));
        economy.FixtureSetGoodStock(daughter, "G-ARMS3", Fix64.Zero);
        economy.FixtureShareFinishedMilitaryGoods(0);
        Assert.Equal(Fix64.Zero, economy.GoodStock(daughter, "G-ARMS3")); // fractional stores are not pooled into a weapon
    }

    [Fact]
    public void Arms_readout_uses_combat_allocation_for_crucible_arms_and_bows()
    {
        var generated = WorldGenerator.Generate(Seed + 2);
        EconomyArchetype[] balanced = { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        ContentPack pack = TestPack.WithNumeric("KEY-5", "MET-10", "MET-11", "WAR-08", "WAR-11");
        ResearchCatalog catalog = new(pack);
        MatchModel match = MatchModel.Create(Seed + 2, pack, generated, balanced, default, rules: ChapterRules.Fief);
        var runner = new SimRunner(match);
        foreach (string id in new[] { "KEY-5", "MET-10", "MET-11", "WAR-08", "WAR-11" })
        {
            runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, catalog.IndexOf(id), 0, 0));
            runner.Advance(1);
        }
        match.Economy.FixtureSetSoldierRoles(0, 1);
        match.Economy.FixtureSetSoldiersSupplied(0, 1);
        match.Economy.FixtureSetGoodStock(0, "G-ARMS5", Fix64.One);
        match.Economy.FixtureSetGoodStock(0, "G-CROSSBOW", Fix64.One);
        match.Economy.FixtureSetGoodStock(0, "G-LONGBOW", Fix64.One);

        SettlementArms arms = match.Politics.ArmsOf(0);

        Assert.Equal(1, arms.StockCrucible);
        Assert.Equal(1, arms.Crucible);
        Assert.Equal(1, arms.StockCrossbow);
        Assert.Equal(1, arms.Crossbow);
        Assert.Equal(1, arms.StockLongbow);
        Assert.Equal(1, arms.Longbow);
        Assert.Equal(arms.Fighters - arms.BaseOnly,
            arms.Crucible + arms.Steel + arms.Iron + arms.Tin + arms.Arsenical + arms.Copper + arms.Crossbow + arms.Longbow + arms.WarBow);
    }
}
