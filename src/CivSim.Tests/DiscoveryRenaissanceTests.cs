using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.World;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>
/// The five Discovery rows that had no system (M13 WP-E6-7 slice 7, docs/MVP/38 §5.3 and §5.4): BLD-13 city build speed, NUM-10 instrument calculation,
/// INQ-09 Newtonian physics, COM-11 newspapers and ART-10 patronage. Each is exactly 1 outside a Discovery match and without its own project.
/// </summary>
public class DiscoveryRenaissanceTests
{
    private const ulong Seed = 20261019;
    private static readonly string[] Base = { "KEY-5" };

    private static BronzeFixture Town(int tier, params string[] projects)
    {
        var all = new List<string>(Base);
        all.AddRange(projects);
        BronzeFixture f = BronzeFixture.CreateDiscoveryWithCoal(TestPack.Pack, Seed, 150, all.ToArray());
        f.Economy.FixtureSetTier(0, tier);
        f.RunSeasons(1);
        f.Economy.FixtureSetTier(0, tier);
        return f;
    }

    private static BronzeFixture FiefTown(int tier, params string[] projects)
    {
        var all = new List<string>(Base);
        all.AddRange(projects);
        BronzeFixture f = BronzeFixture.CreateFiefWithCoalDeposit(TestPack.Pack, Seed, 150, all.ToArray());
        f.Economy.FixtureSetTier(0, tier);
        return f;
    }

    // ---- BLD-13 ----------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void BLD13_cuts_a_Citys_construction_labor_by_the_rows_speed_and_nobody_elses()
    {
        BronzeFixture plainCity = Town(2);
        BronzeFixture city = Town(2, "BLD-13");
        BronzeFixture town = Town(1, "BLD-13");
        Fix64 speed = city.Economy.FixtureModifier(0, "city_move_and_build_speed");
        Assert.True(speed > Fix64.One);

        Assert.Equal(Fix64.One, plainCity.Economy.FixtureCityBuildLaborFactor(0));
        Assert.Equal(Fix64.One, town.Economy.FixtureCityBuildLaborFactor(0));
        Assert.Equal(Fix64.One / speed, city.Economy.FixtureCityBuildLaborFactor(0));

        Fix64 before = plainCity.Economy.FixtureIronLabor(0, "harbor");
        Fix64 after = city.Economy.FixtureIronLabor(0, "harbor");
        Assert.True(after < before, "an Iron work is quicker to raise in a City with perspective-planned streets");
        Assert.Equal(town.Economy.FixtureIronLabor(0, "harbor"), before);
        Assert.True(city.Economy.FixtureStructureBuildLabor(0, "shrine") < plainCity.Economy.FixtureStructureBuildLabor(0, "shrine"));
    }

    [Fact]
    public void BLD13_does_nothing_in_Fief()
    {
        BronzeFixture fief = FiefTown(2, "BLD-13");
        Assert.Equal(Fix64.One, fief.Economy.FixtureCityBuildLaborFactor(0));
    }

    // ---- NUM-10 ----------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void NUM10_halves_the_labor_of_parts_chronometers_and_telescopes_only()
    {
        BronzeFixture with = Town(1, "NUM-10");
        BronzeFixture without = Town(1);
        Fix64 speed = with.Economy.FixtureModifier(0, "navigation_calc_speed");
        Assert.Equal(Fix64.FromInt(2), speed);
        foreach (string recipe in new[] { "make_parts", "make_chronometer", "make_telescope" })
        {
            Assert.Equal(Fix64.One / speed, with.Economy.FixtureInstrumentCalcFactor(0, recipe));
            Assert.Equal(Fix64.One, without.Economy.FixtureInstrumentCalcFactor(0, recipe));
        }
        Assert.Equal(Fix64.One, with.Economy.FixtureInstrumentCalcFactor(0, "make_coke"));
        Assert.Equal(Fix64.One, FiefTown(1, "NUM-10").Economy.FixtureInstrumentCalcFactor(0, "make_parts"));
    }

    // ---- INQ-09 ----------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void INQ09_makes_MEC_and_SEA_effects_a_tenth_stronger_and_leaves_other_lineages_and_discounts_alone()
    {
        BronzeFixture without = Town(1, "MEC-09", "SEA-08", "BLD-13", "NUM-10");
        BronzeFixture with = Town(1, "MEC-09", "SEA-08", "BLD-13", "NUM-10", "INQ-09");
        Fix64 strength = with.Economy.FixtureModifier(0, "mec_nrg_sea_effects");
        Assert.Equal(Fix64.Parse("1.1"), strength);

        // MEC-09's survey accuracy x1.2 and SEA-08's trade throughput x1.2: the part above 1 grows by the row's value.
        Fix64 survey = without.Economy.FixtureModifier(0, "survey_accuracy");
        Assert.Equal(Fix64.Parse("1.2"), survey);
        Assert.Equal(Fix64.One + (survey - Fix64.One) * strength, with.Economy.FixtureModifier(0, "survey_accuracy"));
        Fix64 trade = without.Economy.FixtureModifier(0, "sea_trade_throughput");
        Assert.Equal(Fix64.One + (trade - Fix64.One) * strength, with.Economy.FixtureModifier(0, "sea_trade_throughput"));

        // BLD-13 and NUM-10 are not mechanics, energy or seafaring: untouched.
        Assert.Equal(without.Economy.FixtureModifier(0, "city_move_and_build_speed"), with.Economy.FixtureModifier(0, "city_move_and_build_speed"));
        Assert.Equal(without.Economy.FixtureModifier(0, "navigation_calc_speed"), with.Economy.FixtureModifier(0, "navigation_calc_speed"));
        // A discount stays a discount: physics does not make a cart cheaper.
        Assert.Equal(without.Economy.FixtureModifier(0, "wheel_and_cart_labor"), with.Economy.FixtureModifier(0, "wheel_and_cart_labor"));
    }

    [Fact]
    public void INQ09_does_nothing_in_Fief()
    {
        Fix64 plain = FiefTown(1, "MEC-09").Economy.FixtureModifier(0, "survey_accuracy");
        Assert.Equal(plain, FiefTown(1, "MEC-09", "INQ-09").Economy.FixtureModifier(0, "survey_accuracy"));
    }

    // ---- COM-11 ----------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void COM11_speeds_Cohesion_and_lifts_the_GOV_terms_only_where_a_print_shop_stands()
    {
        BronzeFixture shop = Town(1, "COM-11", "GOV-03");
        shop.Economy.FixtureSetIronWork(0, "print_shop", 1);
        BronzeFixture noShop = Town(1, "COM-11", "GOV-03");
        BronzeFixture noProject = Town(1, "GOV-03");
        noProject.Economy.FixtureSetIronWork(0, "print_shop", 1);

        Fix64 response = shop.Economy.FixtureModifier(0, "cohesion_event_response");
        Assert.Equal(Fix64.Parse("1.5"), response);
        Assert.Equal(response, shop.Economy.FixtureCohesionResponse(0));
        Assert.Equal(Fix64.One, noShop.Economy.FixtureCohesionResponse(0));
        Assert.Equal(Fix64.One, noProject.Economy.FixtureCohesionResponse(0));

        Assert.Equal(Fix64.Parse("0.1"), shop.Economy.FixturePublicOpinionGain(0));
        Assert.Equal(Fix64.Zero, noShop.Economy.FixturePublicOpinionGain(0));
        Assert.True(shop.Economy.FixtureCohesionTarget(0) >= noShop.Economy.FixtureCohesionTarget(0), "the lift is a bounded bonus, never a loss");
        Assert.True(shop.Economy.FixtureCohesionTarget(0) <= noShop.Economy.FixtureCohesionTarget(0) + 2, "a tenth of a few GOV points is a point or two at most");
    }

    [Fact]
    public void COM11_does_nothing_in_Fief()
    {
        BronzeFixture fief = FiefTown(1, "COM-11", "GOV-03");
        fief.Economy.FixtureSetIronWork(0, "print_shop", 1);
        Assert.Equal(Fix64.One, fief.Economy.FixtureCohesionResponse(0));
        Assert.Equal(Fix64.Zero, fief.Economy.FixturePublicOpinionGain(0));
    }

    // ---- ART-10 ----------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ART10_needs_a_patron_a_capital_Palace_or_any_Wonder_before_it_does_anything()
    {
        BronzeFixture f = Town(1, "ART-10");
        Assert.False(f.Economy.PatronStands(0));
        Assert.Equal(Fix64.One, f.Economy.LegendChanceFactor(0));
        Assert.Equal(0, f.Economy.PatronBeaconSlots(0));
        Assert.False(f.Economy.PatronFundsMasters(0));

        f.Economy.FixtureSetPalaces(0, 1); // the court at the capital
        Assert.True(f.Economy.PatronStands(0));
        Assert.Equal(Fix64.FromInt(2), f.Economy.LegendChanceFactor(0));
        Assert.Equal(1, f.Economy.PatronBeaconSlots(0));
        Assert.True(f.Economy.PatronFundsMasters(0));

        BronzeFixture wonder = Town(1, "ART-10");
        wonder.Economy.FixtureSetIronWork(0, "great_library", 1); // a Wonder is a patron too
        Assert.True(wonder.Economy.PatronStands(0));
    }

    [Fact]
    public void ART10_does_nothing_without_the_project_or_outside_Discovery()
    {
        BronzeFixture noProject = Town(1);
        noProject.Economy.FixtureSetPalaces(0, 1);
        Assert.False(noProject.Economy.PatronStands(0));
        Assert.Equal(0, noProject.Economy.PatronBeaconSlots(0));

        BronzeFixture fief = FiefTown(1, "ART-10");
        fief.Economy.FixtureSetPalaces(0, 1);
        Assert.False(fief.Economy.PatronStands(0));
        Assert.Equal(Fix64.One, fief.Economy.LegendChanceFactor(0));
        Assert.Equal(0, fief.Economy.PatronBeaconSlots(0));
    }

    [Fact]
    public void ART10_adds_one_Beacon_slot_to_the_civilizations_allowance_while_a_patron_court_stands()
    {
        var pack = new Lazy<CivSim.Sim.Content.ContentPack>(() => TestPack.WithNumeric("KEY-6", "ART-10"));
        var world = WorldGenerator.Generate(Seed);
        EconomyArchetype[] balanced = { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        MatchModel match = MatchModel.Create(Seed, pack.Value, world, balanced, default, rules: ChapterRules.Discovery);
        var runner = new SimRunner(match);
        foreach (string project in new[] { "KEY-5", "KEY-6", "ART-10" })
        {
            runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, match.Research.Catalog.IndexOf(project), 0, 0));
            runner.Advance(2);
        }
        int before = match.Politics.BeaconAllowance(0), otherBefore = match.Politics.BeaconAllowance(1);
        Assert.False(match.Economy.PatronStands(0));

        int home = Enumerable.Range(0, match.Economy.SettlementCount).First(i => match.Economy.SettlementLite(i).Civilization == 0);
        match.Economy.FixtureSetPalaces(home, 1);
        Assert.True(match.Economy.PatronStands(0));
        Assert.Equal(before + 1, match.Politics.BeaconAllowance(0));
        Assert.Equal(otherBefore, match.Politics.BeaconAllowance(1)); // another civilization's allowance does not move
    }
}
