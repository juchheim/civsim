using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Tests;

/// <summary>
/// The early state, E3 food and E3 health (WP7, docs/MVP/22 §8–§10, 23 §1.2, §8–§12, 07d R2/R4/R7): settlement tiers with
/// hysteresis, the Market town housing rule, roles by structure, live Cohesion with its target, smoothing, secession and the
/// captured-town rule, the ard and canals, outbreaks and the plague hook, and snapshot exactness.
/// </summary>
public class E3StateTests
{
    private const ulong Seed = 20261019;

    private static BronzeFixture Fixture(int people = 60, params string[] extra)
        => BronzeFixture.Create(TestPack.Pack, Seed, people, false, true, true, true, extra);

    private static readonly string[] State = { "TRD-04", "SOC-08", "SOC-09", "BEL-06", "COM-05", "LRN-04", "GOV-05", "AGR-05", "AGR-06" };

    [Fact]
    public void E3_cause_shares_match_the_health_spec_and_remain_distinct_from_E2()
    {
        string[] causes = { "endemic", "epidemic", "starvation", "exposure", "injury", "predators", "violence" };
        Fix64[] adult = { Fix64.Parse("0.35"), Fix64.Parse("0.10"), Fix64.Parse("0.10"), Fix64.Parse("0.10"), Fix64.Parse("0.20"), Fix64.Zero, Fix64.Parse("0.15") };
        Fix64[] child = { Fix64.Parse("0.50"), Fix64.Parse("0.10"), Fix64.Parse("0.15"), Fix64.Parse("0.10"), Fix64.Parse("0.10"), Fix64.Zero, Fix64.Parse("0.05") };

        Assert.InRange(adult.Aggregate(Fix64.Zero, (sum, value) => sum + value).ToDoubleForDisplay(), 0.999999, 1.000001);
        Assert.InRange(child.Aggregate(Fix64.Zero, (sum, value) => sum + value).ToDoubleForDisplay(), 0.999999, 1.000001);
        for (int i = 0; i < causes.Length; i++)
        {
            Assert.Equal(adult[i], LivingEconomyModel.CauseShare(false, causes[i], 3));
            Assert.Equal(child[i], LivingEconomyModel.CauseShare(true, causes[i], 3));
        }
        Assert.Equal(Fix64.Parse("0.27"), LivingEconomyModel.CauseShare(false, "endemic", 2));
        Assert.Equal(Fix64.Parse("0.42"), LivingEconomyModel.CauseShare(true, "endemic", 2));
    }

    // ---- tiers ----------------------------------------------------------------------------------------

    [Fact]
    public void A_settlement_becomes_a_Town_with_a_Market_and_60_people_and_holds_it_through_a_dip()
    {
        BronzeFixture f = Fixture(62, State);
        LivingEconomyModel e = f.Economy;
        f.RunSeasons(1);
        Assert.Equal(SettlementTier.Village, e.Tier(0));
        e.FixtureCivic(0, "market");
        f.RunSeasons(1);
        Assert.Equal(SettlementTier.Town, e.Tier(0));
        Assert.Equal(5, e.ClaimRadius(0, 4));
        // 0.85 × 60 = 51: at 55 the tier holds without counting down.
        e.FixtureRemovePeople(0, e.Population(0) - 55);
        f.RunSeasons(6);
        Assert.Equal(SettlementTier.Town, e.Tier(0));
    }

    [Fact]
    public void A_Town_below_85_percent_of_its_threshold_for_four_seasons_is_demoted()
    {
        BronzeFixture f = Fixture(62, State);
        LivingEconomyModel e = f.Economy;
        e.FixtureCivic(0, "market");
        f.RunSeasons(1);
        Assert.Equal(SettlementTier.Town, e.Tier(0));
        e.FixtureRemovePeople(0, e.Population(0) - 45);
        f.RunSeasons(3);
        Assert.Equal(SettlementTier.Town, e.Tier(0));
        f.RunSeasons(2);
        Assert.Equal(SettlementTier.Village, e.Tier(0));
        Assert.Equal(4, e.ClaimRadius(0, 4));
    }

    [Fact]
    public void A_City_needs_90_people_and_a_Temple_or_the_Palace()
    {
        BronzeFixture f = Fixture(95, State);
        LivingEconomyModel e = f.Economy;
        e.FixtureCivic(0, "market");
        f.RunSeasons(1);
        Assert.Equal(SettlementTier.Town, e.Tier(0));
        e.FixtureCivic(0, "temple");
        f.RunSeasons(1);
        Assert.Equal(SettlementTier.City, e.Tier(0));
        Assert.Equal(6, e.ClaimRadius(0, 4));
    }

    [Fact]
    public void A_city_eligible_Town_prioritizes_its_Temple_over_housing_and_finishes_it()
    {
        BronzeFixture f = BronzeFixture.Create(TestPack.Pack, Seed, 95, false, true, true, true,
            "TRD-04", "SOC-09", "BEL-06", "STN-11", "BLD-05");
        LivingEconomyModel e = f.Economy;
        e.FixtureCivic(0, "market");
        e.FixtureBuildStructure(0, "longhouse");
        e.FixtureBuildStructure(0, "longhouse");
        e.FixtureBuildStructure(0, "longhouse");
        Assert.True(e.FixtureHousingCap(0) >= Fix64.FromInt(90));
        e.FixtureStock(0, "G-BRICK", Fix64.FromInt(120));
        e.FixtureStock(0, "G-ASHLAR", Fix64.FromInt(10));
        f.RunSeasons(2);
        Assert.True(e.Civic(0).Temples == 1 || e.DescribeCivic(0).Contains("build Temple:", StringComparison.Ordinal), e.DescribeCivic(0));
        f.RunSeasons(8);
        Assert.Equal(1, e.Civic(0).Temples);
        Assert.Equal(SettlementTier.City, e.Tier(0));
    }

    [Fact]
    public void Selected_City_site_stages_bricks_before_SOC09_and_stone_tech_then_waits_for_BEL06()
    {
        BronzeFixture f = BronzeFixture.Create(TestPack.Pack, Seed, 95, false, true, true, true,
            "TRD-04", "BEL-05", "BLD-05");
        LivingEconomyModel e = f.Economy;
        e.FixtureCivic(0, "market");
        e.FixtureBuildStructure(0, "longhouse");
        e.FixtureBuildStructure(0, "longhouse");
        e.FixtureBuildStructure(0, "longhouse");
        Assert.True(e.FixtureHousingCap(0) >= Fix64.FromInt(90));

        f.RunSeasons(12);

        Assert.Equal(SettlementTier.Town, e.Tier(0));
        Assert.Equal(0, e.Civic(0).Temples);
        Assert.Equal(Fix64.Zero, e.FixtureCivicBuildProgress(0));
        Assert.Contains("build Temple:0.0", e.DescribeCivic(0));
        Assert.True(e.GoodStock(0, "G-BRICK") >= Fix64.FromInt(120), e.DescribeCivic(0));
        Assert.Equal(Fix64.Zero, e.GoodStock(0, "G-ASHLAR"));

        e.FixtureComplete(0, "SOC-09", "STN-11", "MIN-03");
        e.FixtureSetRangeStone(0, Fix64.FromInt(40));
        f.RunSeasons(12);

        Assert.Equal(SettlementTier.Town, e.Tier(0));
        Assert.Equal(0, e.Civic(0).Temples);
        Assert.True(e.GoodStock(0, "G-ASHLAR") >= Fix64.FromInt(10), e.DescribeCivic(0));

        e.FixtureComplete(0, "BLD-07");
        f.RunSeasons(8);
        Assert.Equal(SettlementTier.Town, e.Tier(0));
        Assert.Equal(0, e.Civic(0).Temples);
        Assert.True(e.FixtureCivicBuildProgress(0) > Fix64.Zero, e.DescribeCivic(0));

        e.FixtureComplete(0, "BEL-06");
        f.RunSeasons(8);

        Assert.Equal(1, e.Civic(0).Temples);
        Assert.Equal(SettlementTier.City, e.Tier(0));
    }

    [Fact]
    public void A_pending_fission_is_cancelled_when_a_Town_becomes_a_viable_City_site()
    {
        BronzeFixture f = BronzeFixture.Create(TestPack.Pack, Seed, 95, false, true, true, true,
            "TRD-04");
        LivingEconomyModel e = f.Economy;
        e.FixtureCivic(0, "market");
        Assert.True(e.Settlement(0).HousingCap < Fix64.FromInt(90));
        e.FixtureHoldSettlements = false;
        e.SetFissionThresholdScale(0, Fix64.Zero);
        e.FixtureCohesion(0, 60);
        f.RunSeasons(1);
        Assert.True(e.Settlement(0).FissionPending);

        e.FixtureBuildStructure(0, "longhouse");
        e.FixtureBuildStructure(0, "longhouse");
        e.FixtureBuildStructure(0, "longhouse");
        Assert.True(e.Settlement(0).FoodCap >= Fix64.FromInt(90));
        Assert.True(e.Settlement(0).HousingCap >= Fix64.FromInt(90));
        e.FixtureComplete(0, "SOC-09");
        e.FixtureCivic(0, "temple");
        e.SetFissionThresholdScale(0, Fix64.One);
        f.RunSeasons(1);

        Assert.Equal(0, e.Settlement(0).Fissions);
        Assert.False(e.Settlement(0).FissionPending);
        Assert.Equal(SettlementTier.City, e.Tier(0));
    }

    [Fact]
    public void A_selected_viable_City_site_does_not_fission_while_SOC09_is_pending()
    {
        BronzeFixture f = BronzeFixture.Create(TestPack.Pack, Seed, 95, false, true, true, true,
            "TRD-04", "BEL-05", "BLD-05");
        LivingEconomyModel e = f.Economy;
        e.FixtureCivic(0, "market");
        e.FixtureBuildStructure(0, "longhouse");
        e.FixtureBuildStructure(0, "longhouse");
        e.FixtureBuildStructure(0, "longhouse");
        Assert.True(e.Settlement(0).FoodCap >= Fix64.FromInt(90));
        Assert.True(e.Settlement(0).HousingCap >= Fix64.FromInt(90));
        e.FixtureHoldSettlements = false;
        e.SetFissionThresholdScale(0, Fix64.Zero);
        e.FixtureCohesion(0, 60);

        f.RunSeasons(1);

        Assert.Equal(0, e.Settlement(0).Fissions);
        Assert.False(e.Settlement(0).FissionPending);
        Assert.Equal(SettlementTier.Town, e.Tier(0));
    }

    [Fact]
    public void Temple_city_path_quarries_and_dresses_local_stone_with_civic_labor()
    {
        BronzeFixture f = BronzeFixture.Create(TestPack.Pack, Seed, 95, false, true, true, true,
            "TRD-04", "SOC-09", "BEL-06", "STN-11", "MIN-03", "BLD-05");
        LivingEconomyModel e = f.Economy;
        e.FixtureCivic(0, "market");
        f.RunSeasons(1);
        Assert.Equal(SettlementTier.Town, e.Tier(0));
        e.FixtureStock(0, "G-BRICK", Fix64.FromInt(120));
        e.FixtureSetRangeStone(0, Fix64.FromInt(40));
        Fix64 stoneBefore = e.FixtureRangeStone(0);

        f.RunSeasons(16);

        Assert.True(e.Civic(0).Temples == 1, e.DescribeCivic(0));
        Assert.True(e.Tier(0) == SettlementTier.City, e.DescribeCivic(0));
        Assert.True(e.FixtureRangeStone(0) < stoneBefore, $"the Temple must quarry its cut stone from the local range: {e.DescribeCivic(0)}");
    }

    [Fact]
    public void Palace_city_path_keeps_its_progress_when_Temple_research_arrives()
    {
        BronzeFixture f = BronzeFixture.Create(TestPack.Pack, Seed, 95, false, true, true, true,
            "TRD-04", "SOC-09", "GOV-05", "MIN-03", "STN-11", "BLD-05");
        LivingEconomyModel e = f.Economy;
        e.FixtureCivic(0, "market");
        f.RunSeasons(1);
        e.FixtureStock(0, "G-BRICK", Fix64.FromInt(200));
        e.FixtureSetRangeStone(0, Fix64.FromInt(40));
        f.RunSeasons(3);
        Assert.Contains("build Palace:", e.DescribeCivic(0));
        Assert.True(e.GoodStock(0, "G-BLOCK") > Fix64.Zero, "the Palace should quarry its block material");
        Fix64 progress = e.FixtureCivicBuildProgress(0);
        Assert.True(progress > Fix64.Zero);

        e.FixtureComplete(0, "BEL-06");
        f.RunSeasons(1);

        Assert.Contains("build Palace:", e.DescribeCivic(0));
        Assert.True(e.FixtureCivicBuildProgress(0) > progress, "Temple research must not erase the active Palace work");
    }

    [Fact]
    public void City_site_ranks_food_bound_capacity_before_population_and_uses_stable_ties()
    {
        Assert.True(LivingEconomyModel.FixtureBetterPalaceSite(
            candidateTown: true, candidateCapacity: Fix64.FromInt(248), candidatePop: 62, candidateId: 6,
            currentTown: true, currentCapacity: Fix64.FromInt(120), currentPop: 63, currentId: 3));
        Assert.False(LivingEconomyModel.FixtureBetterPalaceSite(
            candidateTown: true, candidateCapacity: Fix64.FromInt(120), candidatePop: 63, candidateId: 3,
            currentTown: true, currentCapacity: Fix64.FromInt(248), currentPop: 62, currentId: 6));
        Assert.True(LivingEconomyModel.FixtureBetterPalaceSite(
            candidateTown: true, candidateCapacity: Fix64.FromInt(25), candidatePop: 25, candidateId: 6,
            currentTown: false, currentCapacity: Fix64.FromInt(200), currentPop: 90, currentId: 3));
        Assert.True(LivingEconomyModel.FixtureBetterPalaceSite(
            candidateTown: true, candidateCapacity: Fix64.FromInt(120), candidatePop: 63, candidateId: 2,
            currentTown: true, currentCapacity: Fix64.FromInt(120), currentPop: 63, currentId: 3));
    }

    [Fact]
    public void The_capital_is_the_oldest_settlement_until_a_Palace_stands_and_then_claims_more()
    {
        BronzeFixture f = Fixture(60, State);
        LivingEconomyModel e = f.Economy;
        f.RunSeasons(1);
        Assert.Equal(0, e.Capital(0));
        Assert.Equal(Fix64.One, e.ClaimMultiplier(0));
        e.FixtureCivic(0, "palace");
        Assert.Equal(Fix64.Parse("1.25"), e.ClaimMultiplier(0));
    }

    // ---- housing ahead (07d R2) ----------------------------------------------------------------------

    [Fact]
    public void A_Market_town_builds_housing_ahead_to_85_percent_of_its_cap_and_a_village_just_in_time()
    {
        // Without TRD-04 no Market is planned: a village builds just in time.
        BronzeFixture village = Fixture(50, "SOC-09", "AGR-05");
        village.RunSeasons(1);
        Assert.Equal(Fix64.FromInt(village.Economy.Population(0) + 8), village.Economy.FixtureHousingTarget(0));
        BronzeFixture f = Fixture(50, State);
        LivingEconomyModel e = f.Economy;
        f.RunSeasons(1);
        e.FixtureCivic(0, "market");
        SettlementView v = e.Settlement(0);
        Fix64 expected = Fix64.FromRatio(85, 100) * Fix64.Min(v.FoodCap, v.GroupCap) + Fix64.FromInt(8);
        Assert.Equal(Fix64.Max(expected, Fix64.FromInt(e.Population(0) + 8)), e.FixtureHousingTarget(0));
    }

    // ---- roles by structure -------------------------------------------------------------------------

    [Fact]
    public void Priests_staff_Temples_and_Scribes_sit_at_the_capital_and_two_to_a_School()
    {
        BronzeFixture f = Fixture(70, State);
        LivingEconomyModel e = f.Economy;
        f.RunSeasons(1);
        Assert.Equal((0, 1), e.RoleHeads(0)); // COM-05: the capital's Scribe
        e.FixtureCivic(0, "temple");
        e.FixtureCivic(0, "school");
        f.RunSeasons(1);
        Assert.Equal((1, 3), e.RoleHeads(0));
        CivicReadout c = e.Civic(0);
        Assert.Equal(1, c.Priests);
        Assert.Equal(3, c.Scribes);
    }

    // ---- Cohesion ----------------------------------------------------------------------------------

    [Fact]
    public void Cohesion_status_matches_the_full_civic_readout()
    {
        BronzeFixture f = Fixture(60, State);
        LivingEconomyModel e = f.Economy;
        e.FixtureCivic(0, "market");
        CivicReadout full = e.Civic(0);
        Assert.Equal((full.CohesionLive, full.Cohesion), e.CohesionStatus(0));
    }

    [Fact]
    public void Cohesion_moves_a_quarter_of_the_way_to_its_target_each_season()
    {
        BronzeFixture f = Fixture(60, State);
        LivingEconomyModel e = f.Economy;
        f.RunSeasons(1);
        e.FixtureCohesion(0, 100);
        int target = e.FixtureCohesionTarget(0);
        Assert.InRange(target, 50, 90);
        f.RunSeasons(1);
        int c1 = e.Civic(0).Cohesion;
        Assert.Equal(100 - (100 - e.Civic(0).CohesionTarget) / 4, c1);
        int last = c1;
        for (int i = 0; i < 12; i++)
        {
            f.RunSeasons(1);
            int now = e.Civic(0).Cohesion;
            Assert.True(now <= last, $"Cohesion rose from {last} to {now} above a lower target");
            last = now;
        }
        Assert.InRange(last, e.Civic(0).CohesionTarget, e.Civic(0).CohesionTarget + 3);
    }

    [Fact]
    public void Cohesion_sets_the_work_pace_from_KEY_3_only()
    {
        BronzeFixture f = Fixture(60, State);
        LivingEconomyModel e = f.Economy;
        e.FixtureCohesion(0, 100);
        f.RunSeasons(1);
        // C 100 → ×1.15 at the season start (the clamp), on an adult of full health.
        int adult = -1;
        for (int i = 0; i < e.FigureCount; i++) if (e.Figure(i).Age is >= 14 and < 45 && e.Figure(i).Health == Fix64.One) { adult = i; break; }
        Assert.True(adult >= 0);
        Assert.True(e.FixtureWorkCapacity(adult) > Fix64.One);
    }

    [Fact]
    public void A_settlement_below_15_for_eight_seasons_secedes_and_politics_is_told_but_the_capital_never_does()
    {
        BronzeFixture f = Fixture(60, State);
        LivingEconomyModel e = f.Economy;
        f.RunSeasons(1);
        // A second settlement 18 tiles from the capital, made of 20 of its people.
        var world = CivSim.Sim.World.WorldGenerator.Generate(Seed);
        int capitalTile = e.SettlementLite(0).SiteTile, site = -1;
        for (int t = 0; t < world.TileCount && site < 0; t++)
            if (world.TileAt(t).IsLand && world.TileAt(t).Biome is not (CivSim.Sim.World.Biome.Peak or CivSim.Sim.World.Biome.Mountain) &&
                CivSim.Sim.World.HexGrid.Distance(capitalTile, t, world.Width) == 18) site = t;
        Assert.True(site >= 0);
        var people = new List<FigureView>();
        e.ListFigures(0, people);
        var ids = new List<long>();
        for (int i = people.Count - 20; i < people.Count; i++) ids.Add(people[i].Id);
        int far = e.FoundSettlement(0, site, ids);
        f.RunSeasons(1);
        e.FixtureCohesion(far, 5, -80);
        e.FixtureCohesion(0, 5, -80);
        var secessions = new List<int>();
        for (int season = 0; season < 7; season++)
        {
            f.RunSeasons(1);
            e.DrainSecessions(secessions);
            Assert.Empty(secessions);
        }
        f.RunSeasons(2);
        e.DrainSecessions(secessions);
        Assert.Equal(new[] { far }, secessions);
        Assert.True(e.Civic(far).Seceded);
        Assert.False(e.IsCohesionLive(far));
        Assert.False(e.Civic(0).Seceded);
    }

    [Fact]
    public void Politics_makes_a_seceded_settlement_Free_Folk()
    {
        var archetypes = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        MatchModel match = MatchModel.Create(Seed, TestPack.Pack, FogFairnessHarness.World, archetypes);
        var runner = new SimRunner(match);
        LivingEconomyModel e = match.Economy;
        int civ = -1, target = -1;
        while (target < 0 && runner.Tick < 30_000)
        {
            runner.Advance(150);
            for (int s = 0; s < e.SettlementCount && target < 0; s++)
            {
                int c = e.SettlementLite(s).Civilization;
                if (e.IsCohesionLive(s) && e.Capital(c) != s && e.Population(e.SettlementLite(s).Id) > 0) { civ = c; target = s; }
            }
        }
        Assert.True(target >= 0, "a non-capital settlement with live Cohesion");
        for (int season = 0; season < 12 && !match.Politics.IsFreeFolk(target); season++)
        {
            e.FixtureCohesion(target, 3, -90);
            runner.Advance(150);
        }
        Assert.True(match.Politics.IsFreeFolk(target));
        Assert.True(e.Civic(target).Seceded);
        Assert.Equal(1, match.Politics.Secessions);
        Assert.NotEqual(target, e.Capital(civ));
    }

    [Fact]
    public void A_captured_town_starts_at_25_with_a_conquered_term_that_recovers_one_a_season()
    {
        BronzeFixture f = Fixture(60, State);
        LivingEconomyModel e = f.Economy;
        f.RunSeasons(1);
        Assert.True(e.NoteCaptured(0));
        CivicReadout c = e.Civic(0);
        Assert.Equal(25, c.Cohesion);
        Assert.Equal(-45, c.ConqueredTerm);
        Assert.Equal(0, c.LowCohesionSeasons);
        f.RunSeasons(1);
        c = e.Civic(0);
        Assert.Equal(-44, c.ConqueredTerm);
        Assert.Equal(0, c.LowCohesionSeasons);
        f.RunSeasons(2);
        c = e.Civic(0);
        Assert.Equal(-42, e.Civic(0).ConqueredTerm);
        Assert.True(c.CohesionTarget < 60, "no hearth, feast, shrine or GOV-03 while conquered");
    }

    [Fact]
    public void Before_KEY_3_nothing_is_live()
    {
        var world = CivSim.Sim.Economy.WorldEconomyInput.FromGeneratedWorld(FogFairnessHarness.World);
        var e = new LivingEconomyModel(Seed, EconomyContent.FromPack(TestPack.Pack), world);
        for (long t = 0; t < 600; t++) e.Step(t, ReadOnlySpan<Command>.Empty);
        Assert.False(e.IsCohesionLive(0));
        Assert.False(e.NoteCaptured(0));
        Assert.Equal(4, e.ClaimRadius(0, 4));
        Assert.Equal(SettlementTier.Village, e.Tier(0));
    }

    // ---- food --------------------------------------------------------------------------------------

    [Fact]
    public void The_ard_raises_the_field_limit_to_40_percent_and_halves_the_labor_of_ploughed_fields()
    {
        BronzeFixture without = Fixture(60);
        without.RunSeasons(1);
        int range = 37;
        Assert.Equal(range / 4, without.Economy.FixtureFieldLimit(0));
        BronzeFixture f = Fixture(60, State);
        LivingEconomyModel e = f.Economy;
        f.RunSeasons(1);
        Assert.Equal(range * 4 / 10, e.FixtureFieldLimit(0));
        e.FixtureFood(0, 12, Fix64.Parse("0.7"), 2, Fix64.FromInt(4), Fix64.Zero, 0);
        // 2 ox pairs plough 8 of 12 fields at half labor: 4 + 8 × 0.5 = 8 work-seasons a year.
        Assert.Equal(Fix64.FromInt(8), e.FixtureFieldLaborNeed(0));
    }

    [Fact]
    public void Canals_go_only_on_fields_within_2_tiles_of_a_river_and_lift_their_yield()
    {
        BronzeFixture f = Fixture(60, State);
        LivingEconomyModel e = f.Economy;
        f.RunSeasons(1);
        (int fields, int canals) = e.FixtureFood(0, 14, Fix64.Parse("0.7"), 0, Fix64.Zero, Fix64.Zero, 0);
        Assert.Equal(e.FixtureNearRiverFields(0), canals);
        Assert.True(canals <= fields);
        Assert.Equal(canals, e.Civic(0).Canals);
    }

    [Fact]
    public void Winter_forecast_clamps_tiny_positive_herd_maintenance_before_dividing()
    {
        BronzeFixture baseline = Fixture(60, State.Concat(new[] { "HUS-03" }).ToArray());
        LivingEconomyModel baselineEconomy = baseline.Economy;
        baseline.RunSeasons(1);
        baselineEconomy.FixtureFood(0, 0, Fix64.Zero, 1, Fix64.Zero, Fix64.Zero, 0);
        Fix64 baselineProduction = baselineEconomy.FixtureForecastWinterProduction(0);

        BronzeFixture tinyHerd = Fixture(60, State.Concat(new[] { "HUS-03" }).ToArray());
        LivingEconomyModel e = tinyHerd.Economy;
        tinyHerd.RunSeasons(1);
        // 110 raw herd units yield a one-raw-unit maintenance need after the model's
        // sequential fixed-point divisions. Dividing an ordinary material budget by it overflows.
        e.FixtureFood(0, 0, Fix64.Zero, 1, Fix64.Zero, Fix64.FromRaw(110), 0);
        Assert.True(e.FixtureCanTameFromRange(0));
        Assert.Equal(Fix64.FromRaw(1), e.FixtureDomesticHerdWorkNeed(0));

        Fix64 production = e.FixtureForecastWinterProduction(0);

        Assert.True(production > baselineProduction);
    }

    // ---- health ------------------------------------------------------------------------------------

    [Fact]
    public void The_final_size_relation_matches_the_SIR_table()
    {
        Assert.Equal(Fix64.Zero, LivingEconomyModel.FinalSize(Fix64.One));
        Fix64 z = LivingEconomyModel.FinalSize(Fix64.FromInt(2));
        Assert.InRange(z.ToDoubleForDisplay(), 0.795, 0.800); // z = 1 − e^(−2z): 0.7968
        Assert.InRange(LivingEconomyModel.FinalSize(Fix64.Parse("1.5")).ToDoubleForDisplay(), 0.580, 0.585);
    }

    [Fact]
    public void From_KEY_3_epidemics_are_discrete_outbreaks_and_an_imported_plague_takes_hold()
    {
        BronzeFixture f = Fixture(60, State);
        LivingEconomyModel e = f.Economy;
        f.RunSeasons(1);
        Assert.True(e.FixtureDiscreteEpidemics(0));
        Assert.Equal(OutbreakClass.None, e.OutbreakAt(0).Class);
        e.ImportOutbreak(0, OutbreakClass.Plague);
        f.RunSeasons(1);
        Assert.Equal((OutbreakClass.Plague, 1), e.OutbreakAt(0));
        f.RunSeasons(1);
        Assert.Equal((OutbreakClass.Plague, 2), e.OutbreakAt(0));
        f.RunSeasons(1);
        Assert.Equal(OutbreakClass.None, e.OutbreakAt(0).Class);
    }

    // ---- determinism -------------------------------------------------------------------------------

    [Fact]
    public void A_snapshot_round_trip_of_the_early_state_is_exact()
    {
        BronzeFixture a = Fixture(70, State), b = Fixture(70, State);
        foreach (BronzeFixture x in new[] { a, b })
        {
            x.Economy.FixtureCivic(0, "market");
            x.Economy.FixtureCivic(0, "temple");
            x.Economy.ImportOutbreak(0, OutbreakClass.Fever);
            x.RunSeasons(6);
        }
        Assert.Equal(a.Economy.ComputeChecksum(), b.Economy.ComputeChecksum());
        foreach (int extra in new[] { 41, 150 })
        {
            for (int t = 0; t < extra; t++) a.Economy.Step(a.Economy.Tick, ReadOnlySpan<Command>.Empty);
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) a.Economy.WriteSnapshot(w);
            stream.Position = 0;
            BronzeFixture restored = Fixture(70, State);
            using (var r = new BinaryReader(stream)) restored.Economy.ReadSnapshot(r);
            Assert.Equal(a.Economy.ComputeChecksum(), restored.Economy.ComputeChecksum());
            for (int t = 0; t < 450; t++)
            {
                a.Economy.Step(a.Economy.Tick, ReadOnlySpan<Command>.Empty);
                restored.Economy.Step(restored.Economy.Tick, ReadOnlySpan<Command>.Empty);
            }
            Assert.Equal(a.Economy.ComputeChecksum(), restored.Economy.ComputeChecksum());
            Assert.Equal(a.Economy.Civic(0), restored.Economy.Civic(0));
        }
    }

    [Fact]
    public void A_restored_match_deep_in_the_Bronze_Age_runs_on_exactly_as_the_live_one()
    {
        var archetypes = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        MatchModel live = MatchModel.Create(Seed, TestPack.Pack, FogFairnessHarness.World, archetypes);
        var runner = new SimRunner(live);
        long at = 26_100 + 23; // minute 43.5, mid-season, with Markets, roles and live Cohesion
        runner.Advance(at);
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) live.WriteSnapshot(w);
        stream.Position = 0;
        MatchModel copy = MatchModel.Create(Seed, TestPack.Pack, FogFairnessHarness.World, archetypes);
        using (var r = new BinaryReader(stream)) copy.ReadSnapshot(r);
        Assert.Equal(live.ComputeChecksum(), copy.ComputeChecksum());
        var copyRunner = new SimRunner(copy, SimRunner.DefaultChecksumInterval, at);
        for (int step = 0; step < 4; step++)
        {
            runner.Advance(150);
            copyRunner.Advance(150);
            Assert.Equal(live.ComputeChecksum(), copy.ComputeChecksum());
            Assert.Equal(live.Research.InsightPerYear(1), copy.Research.InsightPerYear(1));
        }
        bool anyLive = false;
        for (int s = 0; s < live.Economy.SettlementCount; s++) anyLive |= live.Economy.IsCohesionLive(s);
        Assert.True(anyLive);
    }
}
