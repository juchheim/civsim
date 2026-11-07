using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Tests;

public sealed class BronzeDefenseBuildTests
{
    private const ulong Seed = 20261019;

    [Fact]
    public void Belief_priority_keeps_an_eligible_palisade_ahead_of_optional_builds()
    {
        BronzeFixture fixture = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, "DEF-03");
        LivingEconomyModel economy = fixture.Economy;
        economy.FixtureStock(0, "G-WOOD", Fix64.FromInt(100));

        Assert.True(economy.PrioritizePhysicalDefenseBuild(0));
        int palisade = LivingEconomyModel.StructureIndex("palisade");
        Assert.Equal(palisade, economy.FixtureBuildTarget(0));
        Assert.True(economy.FixtureHasBuildCandidate(0));
        Assert.Equal(palisade, economy.FixtureBuildTarget(0));
    }

    [Fact]
    public void Unaffordable_stone_walls_do_not_hold_the_basic_build_slot()
    {
        BronzeFixture fixture = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, "DEF-03");
        LivingEconomyModel economy = fixture.Economy;
        fixture.RunSeasons(40); // let the earlier basic builds (storage, shelter, fields) settle
        economy.FixtureComplete(0, "DEF-05"); // stone_walls unlock
        economy.FixtureBuildStructure(0, "palisade");
        int walls = LivingEconomyModel.StructureIndex("stone_walls");

        // No cut stone in store: the slot must not be parked on walls, whatever else it picks.
        economy.FixtureSetBuildTarget(0, "palisade");
        economy.FixtureHasBuildCandidate(0);
        Assert.NotEqual(walls, economy.FixtureBuildTarget(0));
    }

    [Fact]
    public void Defense_priority_respects_unlocks_resources_and_existing_defenses()
    {
        GeneratedWorld world = WorldGenerator.Generate(Seed);
        var locked = new LivingEconomyModel(Seed, EconomyContent.FromPack(TestPack.Pack), WorldEconomyInput.FromGeneratedWorld(world));
        locked.FixtureComplete(0, "KEY-3");
        locked.FixtureStock(0, "G-WOOD", Fix64.FromInt(100));
        Assert.False(locked.PrioritizePhysicalDefenseBuild(0));

        BronzeFixture poor = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, "DEF-03");
        poor.Economy.FixtureStock(0, "G-WOOD", -poor.Economy.GoodStock(0, "G-WOOD"));
        Assert.False(poor.Economy.PrioritizePhysicalDefenseBuild(0));

        BronzeFixture fortified = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, "DEF-03");
        fortified.Economy.FixtureStock(0, "G-WOOD", Fix64.FromInt(100));
        fortified.Economy.FixtureBuildStructure(0, "palisade");
        Assert.False(fortified.Economy.PrioritizePhysicalDefenseBuild(0));
    }

    [Fact]
    public void Palisade_cost_scales_by_tier_and_winter_hearth_reserve_remains()
    {
        Assert.Equal(Fix64.FromInt(8), LivingEconomyModel.PalisadeWoodCost(0));
        Assert.Equal(Fix64.FromInt(16), LivingEconomyModel.PalisadeWoodCost(1));
        Assert.Equal(Fix64.FromInt(24), LivingEconomyModel.PalisadeWoodCost(2));
        Assert.Equal(Fix64.FromInt(8), LivingEconomyModel.PalisadeRequiredWood(0, 0));
        Assert.Equal(Fix64.FromInt(11), LivingEconomyModel.PalisadeRequiredWood(0, 2));
        Assert.Equal(Fix64.FromInt(8), LivingEconomyModel.PalisadeRequiredWood(0, 3));

        BronzeFixture ready = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true,
            shaftMining: true, holdPopulation: true);
        ready.RunSeasons(2);
        ready.Economy.FixtureComplete(0, "DEF-03");
        ready.Economy.FixtureSetBuildTarget(0, "palisade");
        ready.Economy.FixtureSetBuildProgress(0, Fix64.FromInt(6));
        ready.Economy.FixtureStock(0, "G-WOOD", Fix64.FromInt(11) - ready.Economy.GoodStock(0, "G-WOOD"));
        ready.Economy.FixtureBuildIfReady(0);
        int palisade = LivingEconomyModel.StructureIndex("palisade");
        Assert.Equal(1, ready.Economy.StructureCount(0, palisade));
        Assert.Equal(Fix64.FromInt(3), ready.Economy.GoodStock(0, "G-WOOD"));

        BronzeFixture shortfall = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true,
            shaftMining: true, holdPopulation: true);
        shortfall.RunSeasons(2);
        shortfall.Economy.FixtureComplete(0, "DEF-03");
        shortfall.Economy.FixtureSetBuildTarget(0, "palisade");
        shortfall.Economy.FixtureSetBuildProgress(0, Fix64.FromInt(6));
        Fix64 belowReserveThreshold = Fix64.FromRatio(899, 100);
        shortfall.Economy.FixtureStock(0, "G-WOOD", belowReserveThreshold - shortfall.Economy.GoodStock(0, "G-WOOD"));
        shortfall.Economy.FixtureBuildIfReady(0);
        Assert.Equal(0, shortfall.Economy.StructureCount(0, palisade));
        Assert.Equal(belowReserveThreshold, shortfall.Economy.GoodStock(0, "G-WOOD"));
    }

    [Fact]
    public void Attack_preparation_queues_locked_palisade_and_waits_for_its_technology()
    {
        GeneratedWorld world = WorldGenerator.Generate(Seed);
        var economy = new LivingEconomyModel(Seed, EconomyContent.FromPack(TestPack.Pack), WorldEconomyInput.FromGeneratedWorld(world));
        economy.FixtureStock(0, "G-WOOD", Fix64.FromInt(100));

        Assert.True(economy.PreparePhysicalDefenseBuild(0));
        int palisade = LivingEconomyModel.StructureIndex("palisade");
        Assert.Equal(palisade, economy.FixtureBuildTarget(0));
        Assert.True(economy.FixtureHasBuildCandidate(0));
        economy.FixtureSetBuildProgress(0, Fix64.FromInt(100));
        economy.FixtureBuildIfReady(0);
        Assert.Equal(palisade, economy.FixtureBuildTarget(0));
        Assert.Equal(0, economy.StructureCount(0, palisade));

        economy.FixtureComplete(0, "DEF-01", "DEF-02", "WOD-03", "DEF-03");
        economy.FixtureBuildIfReady(0);
        Assert.Equal(1, economy.StructureCount(0, palisade));

        Assert.Equal(0.5d, LivingEconomyModel.B28LockedDefenseBuildWork(Fix64.FromInt(2), Fix64.FromRatio(11, 2), Fix64.FromInt(6))
            .ToDoubleForDisplay(), 5);
        Assert.Equal(Fix64.Zero, LivingEconomyModel.B28LockedDefenseBuildWork(Fix64.One, Fix64.FromInt(6), Fix64.FromInt(6)));
    }

    [Fact]
    public void Proactive_diagnostic_queues_unaffordable_palisade_but_default_order_does_not()
    {
        BronzeFixture ordinary = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, "DEF-03");
        ordinary.Economy.FixtureStock(0, "G-WOOD", -ordinary.Economy.GoodStock(0, "G-WOOD"));
        Assert.False(ordinary.Economy.PrioritizePhysicalDefenseBuild(0));

        BronzeFixture diagnostic = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, "DEF-03");
        diagnostic.Economy.FixtureStock(0, "G-WOOD", -diagnostic.Economy.GoodStock(0, "G-WOOD"));
        Assert.True(diagnostic.Economy.PrioritizePhysicalDefenseBuild(0, proactiveDiagnostic: true));
        Assert.Equal(LivingEconomyModel.StructureIndex("palisade"), diagnostic.Economy.FixtureBuildTarget(0));
    }

    [Fact]
    public void Proactive_diagnostic_keeps_existing_construction_and_caps_reserve_at_material_work()
    {
        BronzeFixture fixture = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, "DEF-03");
        fixture.Economy.FixtureSetBuildTarget(0, "field");
        fixture.Economy.FixtureSetBuildProgress(0, Fix64.One);
        Assert.False(fixture.Economy.PrioritizePhysicalDefenseBuild(0, proactiveDiagnostic: true));

        Fix64 material = Fix64.FromInt(4), target = Fix64.FromInt(40);
        Assert.Equal(Fix64.FromInt(4), LivingEconomyModel.B28WoodShortWork(material, Fix64.Zero, target,
            metalPlanAvailable: false, proactivePalisade: true));
        Assert.Equal(Fix64.Zero, LivingEconomyModel.B28WoodShortWork(material, Fix64.Zero, target,
            metalPlanAvailable: false, proactivePalisade: false));
        Assert.Equal(Fix64.FromInt(2), LivingEconomyModel.B28WoodShortWork(material, Fix64.Zero, target,
            metalPlanAvailable: true, proactivePalisade: false));

        // The traced effective Chop yield is about 1.8 L per work-season. A 3.6 L gap
        // therefore needs 2 work-seasons, still capped by available material labor.
        Fix64 estimatedPalisadeWork = LivingEconomyModel.B28WoodShortWork(Fix64.FromInt(4), Fix64.FromRatio(82, 5),
            Fix64.FromInt(20), metalPlanAvailable: false, proactivePalisade: true);
        Assert.Equal(2d, estimatedPalisadeWork.ToDoubleForDisplay(), 5);
        Assert.Equal(0.5d, LivingEconomyModel.B28WoodShortWork(Fix64.FromInt(4), Fix64.FromInt(16),
            Fix64.FromInt(20), metalPlanAvailable: true, proactivePalisade: false).ToDoubleForDisplay(), 5);
    }

    [Fact]
    public void Proactive_material_cap_uses_only_unassigned_work_and_preserves_food_and_build_budgets()
    {
        Fix64 total = Fix64.FromInt(100);
        Fix64 food = Fix64.FromInt(48), build = Fix64.FromInt(12);

        Assert.Equal(18d, LivingEconomyModel.B28MaterialWork(total, food, build, proactive: false).ToDoubleForDisplay(), 5);
        Assert.Equal(40d, LivingEconomyModel.B28MaterialWork(total, food, build, proactive: true).ToDoubleForDisplay(), 5);

        // At a high food share, the remaining room is only 3%; the diagnostic cannot
        // take work already assigned to food or construction. At an even higher share,
        // it has no unassigned work to route.
        Assert.Equal(3d, LivingEconomyModel.B28MaterialWork(total,
            Fix64.FromInt(85), Fix64.FromInt(12), proactive: true).ToDoubleForDisplay(), 5);
        Assert.Equal(Fix64.Zero, LivingEconomyModel.B28MaterialWork(total,
            Fix64.FromInt(90), Fix64.FromInt(10), proactive: true));
    }

    [Fact]
    public void Urgent_palisade_releases_material_work_after_wood_reserve_is_met()
    {
        Fix64 herd = Fix64.FromRatio(1, 4);
        Fix64 reservedWood = Fix64.FromRatio(1, 2);

        Assert.Equal(Fix64.FromRatio(3, 4),
            LivingEconomyModel.B28ProtectedMaterial(herd, reservedWood, woodShortfall: true));
        Assert.Equal(herd,
            LivingEconomyModel.B28ProtectedMaterial(herd, reservedWood, woodShortfall: false));
    }

    [Fact]
    public void Urgent_palisade_uses_bounded_post_food_labor_and_stops_at_remaining_work()
    {
        Fix64 ordinary = Fix64.One;
        Fix64 urgent = LivingEconomyModel.B28UrgentPalisadeBuildWork(ordinary,
            buildBudget: Fix64.FromInt(20), totalWork: Fix64.FromInt(100),
            buildProgress: Fix64.Zero, palisadeLabor: Fix64.FromInt(6),
            materialBudget: Fix64.FromInt(40), protectedMaterial: Fix64.FromInt(10));
        Assert.Equal(Fix64.FromInt(3), urgent);

        // Construction still comes only from the amount left after food and civic work.
        Assert.Equal(Fix64.FromInt(2), LivingEconomyModel.B28UrgentPalisadeBuildWork(ordinary,
            buildBudget: Fix64.FromInt(2), totalWork: Fix64.FromInt(100),
            buildProgress: Fix64.Zero, palisadeLabor: Fix64.FromInt(6),
            materialBudget: Fix64.FromInt(40), protectedMaterial: Fix64.FromInt(10)));

        // A nearly finished Palisade receives only the missing 0.5 labor, never a full new-season allotment.
        Assert.Equal(Fix64.FromRatio(1, 2), LivingEconomyModel.B28UrgentPalisadeBuildWork(ordinary,
            buildBudget: Fix64.FromInt(20), totalWork: Fix64.FromInt(100),
            buildProgress: Fix64.FromRatio(11, 2), palisadeLabor: Fix64.FromInt(6),
            materialBudget: Fix64.FromInt(40), protectedMaterial: Fix64.FromInt(10)));
        Assert.Equal(Fix64.Zero, LivingEconomyModel.B28UrgentPalisadeBuildWork(ordinary,
            buildBudget: Fix64.FromInt(20), totalWork: Fix64.FromInt(100),
            buildProgress: Fix64.FromInt(6), palisadeLabor: Fix64.FromInt(6),
            materialBudget: Fix64.FromInt(40), protectedMaterial: Fix64.FromInt(10)));

        // If the entire material allocation is needed for ordinary work, only the normal build allotment remains.
        Assert.Equal(ordinary, LivingEconomyModel.B28UrgentPalisadeBuildWork(ordinary,
            buildBudget: Fix64.FromInt(20), totalWork: Fix64.FromInt(100),
            buildProgress: Fix64.Zero, palisadeLabor: Fix64.FromInt(6),
            materialBudget: Fix64.FromInt(10), protectedMaterial: Fix64.FromInt(10)));
    }

    [Fact]
    public void Attack_preparation_retargets_a_low_progress_build_and_queues_a_palisade_without_wood()
    {
        BronzeFixture fixture = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, "DEF-03");
        fixture.Economy.FixtureStock(0, "G-WOOD", -fixture.Economy.GoodStock(0, "G-WOOD"));
        fixture.Economy.FixtureSetBuildTarget(0, "lean_to");
        fixture.Economy.FixtureSetBuildProgress(0, Fix64.FromRatio(22, 100));

        Assert.True(fixture.Economy.PreparePhysicalDefenseBuild(0));
        Assert.Equal(LivingEconomyModel.StructureIndex("palisade"), fixture.Economy.FixtureBuildTarget(0));
        Assert.Equal(Fix64.FromRatio(22, 100), fixture.Economy.FixtureBuildProgress(0));
    }

    [Fact]
    public void Attack_preparation_preserves_more_advanced_projects_and_never_retargets_defense()
    {
        BronzeFixture advanced = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, "DEF-03");
        advanced.Economy.FixtureSetBuildTarget(0, "lean_to");
        advanced.Economy.FixtureSetBuildProgress(0, Fix64.FromRatio(3, 10));
        Assert.False(advanced.Economy.PreparePhysicalDefenseBuild(0));
        Assert.Equal(LivingEconomyModel.StructureIndex("lean_to"), advanced.Economy.FixtureBuildTarget(0));

        BronzeFixture defense = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, "DEF-03");
        defense.Economy.FixtureSetBuildTarget(0, "palisade");
        defense.Economy.FixtureSetBuildProgress(0, Fix64.FromRatio(1, 5));
        Assert.False(defense.Economy.PreparePhysicalDefenseBuild(0));
        Assert.Equal(LivingEconomyModel.StructureIndex("palisade"), defense.Economy.FixtureBuildTarget(0));
    }

    [Fact]
    public void Fission_daughter_inherits_a_queued_physical_defense_without_free_progress()
    {
        BronzeFixture fixture = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true,
            shaftMining: true, holdPopulation: true, extraProjects: new[] { "DEF-03" });
        LivingEconomyModel economy = fixture.Economy;
        economy.FixtureSetBuildTarget(0, "palisade");
        economy.FixtureSetBuildProgress(0, Fix64.FromRatio(1, 2));

        int daughter = economy.FoundByDecree(0, fixture.CampTile, Fix64.FromRatio(2, 5), minDaughter: 6, startCohesion: 60);

        Assert.True(daughter >= 0);
        Assert.Equal(LivingEconomyModel.StructureIndex("palisade"), economy.FixtureBuildTarget(daughter));
        Assert.Equal(Fix64.Zero, economy.FixtureBuildProgress(daughter));
        Assert.Equal(LivingEconomyModel.StructureIndex("palisade"), economy.FixtureBuildTarget(0));
        Assert.Equal(Fix64.FromRatio(1, 2), economy.FixtureBuildProgress(0));
    }

    [Fact]
    public void Fission_daughter_does_not_inherit_an_unrelated_build_order()
    {
        BronzeFixture fixture = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true,
            shaftMining: true, holdPopulation: true, extraProjects: new[] { "DEF-03" });
        LivingEconomyModel economy = fixture.Economy;
        economy.FixtureSetBuildTarget(0, "field");

        int daughter = economy.FoundByDecree(0, fixture.CampTile, Fix64.FromRatio(2, 5), minDaughter: 6, startCohesion: 60);

        Assert.True(daughter >= 0);
        Assert.NotEqual(LivingEconomyModel.StructureIndex("field"), economy.FixtureBuildTarget(daughter));
    }

    [Fact]
    public void Reachable_sister_stores_fund_a_queued_palisade_without_spending_donor_reserves()
    {
        BronzeFixture fixture = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true,
            shaftMining: true, holdPopulation: true, extraProjects: new[] { "SOC-07", "GOV-04", "DEF-03" });
        LivingEconomyModel economy = fixture.Economy;
        economy.FixtureComplete(0, "SOC-07", "GOV-04", "DEF-03");
        int daughter = economy.FoundByDecree(0, fixture.CampTile, Fix64.FromRatio(2, 5), minDaughter: 6, startCohesion: 60);
        Assert.True(daughter >= 0);

        economy.FixtureStock(0, "G-WOOD", Fix64.FromInt(40));
        economy.FixtureStock(daughter, "G-WOOD", -economy.GoodStock(daughter, "G-WOOD"));
        economy.FixtureSetBuildTarget(0, "longhouse");
        economy.FixtureSetCivicBuildTarget(0, "market");
        economy.FixtureSetBuildTarget(daughter, "palisade");
        Fix64 donorReserve = economy.FixtureDefenseWoodDonorReserve(0, season: 0);
        Assert.True(donorReserve >= Fix64.FromInt(22)); // hearth/fuel + longhouse + market commitments
        Fix64 totalBefore = economy.GoodStock(0, "G-WOOD") + economy.GoodStock(daughter, "G-WOOD");

        economy.FixtureShareDefenseWood(season: 0);

        Assert.True(economy.GoodStock(daughter, "G-WOOD") >= Fix64.FromInt(8));
        Assert.True(economy.GoodStock(0, "G-WOOD") >= donorReserve);
        Assert.Equal(totalBefore, economy.GoodStock(0, "G-WOOD") + economy.GoodStock(daughter, "G-WOOD"));
    }

    [Fact]
    public void Defense_wood_sharing_does_not_cross_the_civilization_share_reach()
    {
        BronzeFixture fixture = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true,
            shaftMining: true, holdPopulation: true, extraProjects: new[] { "GOV-04", "DEF-03" });
        LivingEconomyModel economy = fixture.Economy;
        economy.FixtureComplete(0, "GOV-04", "DEF-03");
        int distantSite = economy.FixtureFindHabitableSiteFartherThan(0, 20);
        Assert.True(distantSite >= 0);
        int daughter = economy.FoundByDecree(0, distantSite, Fix64.FromRatio(2, 5), minDaughter: 6, startCohesion: 60);
        Assert.True(daughter >= 0);

        economy.FixtureStock(0, "G-WOOD", Fix64.FromInt(40));
        economy.FixtureStock(daughter, "G-WOOD", -economy.GoodStock(daughter, "G-WOOD"));
        economy.FixtureSetBuildTarget(daughter, "palisade");
        Fix64 donorBefore = economy.GoodStock(0, "G-WOOD");

        economy.FixtureShareDefenseWood(season: 0);

        Assert.Equal(Fix64.Zero, economy.GoodStock(daughter, "G-WOOD"));
        Assert.Equal(donorBefore, economy.GoodStock(0, "G-WOOD"));
    }

    [Fact]
    public void Queued_palisade_donor_still_reserves_concurrent_civic_wood()
    {
        BronzeFixture fixture = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true,
            shaftMining: true, holdPopulation: true, extraProjects: new[] { "SOC-07", "GOV-04", "DEF-03" });
        LivingEconomyModel economy = fixture.Economy;
        economy.FixtureComplete(0, "SOC-07", "GOV-04", "DEF-03");
        economy.FixtureSetBuildTarget(0, "palisade");
        Fix64 withoutCivic = economy.FixtureDefenseWoodDonorReserve(0, season: 0);

        economy.FixtureSetCivicBuildTarget(0, "market");

        Assert.True(economy.FixtureDefenseWoodDonorReserve(0, season: 0) - withoutCivic >= Fix64.FromInt(8));
    }

    [Fact]
    public void Urgent_preparation_retargets_advanced_non_defense_build_and_preserves_progress()
    {
        BronzeFixture fixture = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, "DEF-03");
        fixture.Economy.FixtureSetBuildTarget(0, "field");
        fixture.Economy.FixtureSetBuildProgress(0, Fix64.FromRatio(79, 100));

        Assert.False(fixture.Economy.PreparePhysicalDefenseBuild(0));
        Assert.Equal(LivingEconomyModel.StructureIndex("field"), fixture.Economy.FixtureBuildTarget(0));
        Assert.True(fixture.Economy.PreparePhysicalDefenseBuild(0, urgent: true));
        Assert.Equal(LivingEconomyModel.StructureIndex("palisade"), fixture.Economy.FixtureBuildTarget(0));
        Assert.Equal(Fix64.FromRatio(79, 100), fixture.Economy.FixtureBuildProgress(0));
    }

    [Fact]
    public void Auto_scouts_temporarily_chop_for_underfunded_palisade_when_winter_is_covered()
    {
        BronzeFixture control = ScoutFixture();
        BronzeFixture defense = ScoutFixture();
        PrepareBeforeWinter(control);
        PrepareBeforeWinter(defense);
        control.Economy.FixtureStock(0, "G-GRAIN", Fix64.FromInt(500));
        defense.Economy.FixtureStock(0, "G-GRAIN", Fix64.FromInt(500));
        control.Economy.FixtureStock(0, "G-WOOD", -control.Economy.GoodStock(0, "G-WOOD"));
        defense.Economy.FixtureStock(0, "G-WOOD", -defense.Economy.GoodStock(0, "G-WOOD"));
        defense.Economy.FixtureSetBuildTarget(0, "palisade");

        control.RunSeasons(1);
        defense.RunSeasons(1);

        Assert.True(CountJob(defense.Economy, EconomyJob.Scout) < CountJob(control.Economy, EconomyJob.Scout));
        Assert.True(CountJob(defense.Economy, EconomyJob.Chop) > CountJob(control.Economy, EconomyJob.Chop));
        Assert.True(defense.Economy.GoodStock(0, "G-WOOD") > control.Economy.GoodStock(0, "G-WOOD"));
        // The active metal plan reserves at least six wood for fuel. Scouts must
        // keep being released until the full Palisade-plus-fuel target is met.
        Assert.True(defense.Economy.GoodStock(0, "G-WOOD") < Fix64.FromInt(17));
        Assert.Equal(Fix64.One, defense.Economy.SettlementLite(0).LateWinterFoodForecast);
    }

    [Fact]
    public void Auto_scouts_are_not_released_for_palisade_when_reduced_plan_cannot_cover_winter()
    {
        BronzeFixture control = ScoutFixture(300);
        BronzeFixture defense = ScoutFixture(300);
        PrepareBeforeWinter(control);
        PrepareBeforeWinter(defense);
        EmptyFoodStores(control.Economy);
        EmptyFoodStores(defense.Economy);
        control.Economy.FixtureStock(0, "G-WOOD", -control.Economy.GoodStock(0, "G-WOOD"));
        defense.Economy.FixtureStock(0, "G-WOOD", -defense.Economy.GoodStock(0, "G-WOOD"));
        defense.Economy.FixtureSetBuildTarget(0, "palisade");

        control.RunSeasonsWithoutFoodTopUp(1);
        defense.RunSeasonsWithoutFoodTopUp(1);

        Assert.True(defense.Economy.SettlementLite(0).LateWinterFoodForecast < Fix64.One);
        Assert.Equal(CountJob(control.Economy, EconomyJob.Scout), CountJob(defense.Economy, EconomyJob.Scout));
    }

    [Fact]
    public void Explicit_scout_policy_is_unchanged_by_queued_palisade()
    {
        BronzeFixture control = ScoutFixture();
        BronzeFixture defense = ScoutFixture();
        PrepareBeforeWinter(control);
        PrepareBeforeWinter(defense);
        control.Economy.FixtureStock(0, "G-GRAIN", Fix64.FromInt(500));
        defense.Economy.FixtureStock(0, "G-GRAIN", Fix64.FromInt(500));
        control.Economy.FixtureStock(0, "G-WOOD", -control.Economy.GoodStock(0, "G-WOOD"));
        defense.Economy.FixtureStock(0, "G-WOOD", -defense.Economy.GoodStock(0, "G-WOOD"));
        control.Economy.FixtureSetScoutPolicy(0, ScoutPolicy.One);
        defense.Economy.FixtureSetScoutPolicy(0, ScoutPolicy.One);
        defense.Economy.FixtureSetBuildTarget(0, "palisade");

        control.RunSeasons(1);
        defense.RunSeasons(1);

        Assert.Equal(CountJob(control.Economy, EconomyJob.Scout), CountJob(defense.Economy, EconomyJob.Scout));
    }

    private static BronzeFixture ScoutFixture(int people = 60)
        => BronzeFixture.Create(TestPack.Pack, Seed, people, false, true, true,
            shaftMining: true, holdPopulation: true, "DEF-03");

    private static void PrepareBeforeWinter(BronzeFixture fixture) => fixture.RunSeasons(2);

    private static int CountJob(LivingEconomyModel economy, EconomyJob job)
    {
        int count = 0;
        for (int i = 0; i < economy.FigureCount; i++)
            if (economy.LookFigure(i).Job == job) count++;
        return count;
    }

    private static void EmptyFoodStores(LivingEconomyModel economy)
    {
        foreach (string good in new[] { "G-PLANT", "G-FRUIT", "G-MEAT", "G-ROOT", "G-NUTS", "G-WGRAIN", "G-GRAIN", "G-FISH", "G-DRIED" })
            economy.FixtureStock(0, good, -economy.GoodStock(0, good));
    }

    [Fact]
    public void Proactive_diagnostic_can_queue_over_residual_progress_without_an_active_build()
    {
        BronzeFixture ordinary = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, "DEF-03");
        ordinary.Economy.FixtureSetBuildProgress(0, Fix64.FromRatio(1, 2));
        Assert.NotEqual(LivingEconomyModel.StructureIndex("palisade"), ordinary.Economy.FixtureBuildTarget(0));
        Assert.False(ordinary.Economy.PrioritizePhysicalDefenseBuild(0));

        BronzeFixture diagnostic = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, "DEF-03");
        diagnostic.Economy.FixtureSetBuildProgress(0, Fix64.FromRatio(1, 2));
        Assert.True(diagnostic.Economy.PrioritizePhysicalDefenseBuild(0, proactiveDiagnostic: true));
        Assert.Equal(LivingEconomyModel.StructureIndex("palisade"), diagnostic.Economy.FixtureBuildTarget(0));
    }

}
