using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>FOR-09 botanical gardens and MED-09 anatomy (M13 WP-E6-8 slice 3, docs/MVP/38 §5.4). Discovery chapter only.</summary>
public class DiscoveryHealthTests
{
    private const ulong Seed = 20261019;
    private const string Garden = "botanical_garden";
    private static readonly string[] Medicine = { "KEY-5", "MED-07", "FOR-09" };

    private static BronzeFixture Town(params string[] projects)
    {
        BronzeFixture f = BronzeFixture.CreateDiscoveryWithCoal(TestPack.Pack, Seed, 150, projects.Length == 0 ? Medicine : projects);
        f.Economy.FixtureSetTier(0, 1);
        return f;
    }

    [Fact]
    public void The_garden_gate_needs_FOR09_and_a_staffed_hospital_to_be_wanted_and_is_one_to_a_town()
    {
        BronzeFixture f = Town();
        Assert.True(f.Economy.FixtureIronGateOpen(0, Garden, includeAppetite: false));
        Assert.False(f.Economy.FixtureIronGateOpen(0, Garden, includeAppetite: true), "no hospital, no use for the bonus");
        f.Economy.FixtureBuildIronStructure(0, "hospital");
        f.RunSeasons(1);
        Assert.True(f.Economy.FixtureHospitalStaffed(0));
        Assert.True(f.Economy.FixtureIronGateOpen(0, Garden, includeAppetite: true));
        f.Economy.FixtureSetIronWork(0, Garden, 1);
        Assert.False(f.Economy.FixtureIronGateOpen(0, Garden, includeAppetite: false), "one garden to a town");

        BronzeFixture noProject = Town("KEY-5", "MED-07");
        Assert.False(noProject.Economy.FixtureIronGateOpen(0, Garden, includeAppetite: false));
        BronzeFixture fief = BronzeFixture.CreateFiefWithCoalDeposit(TestPack.Pack, Seed, 150, Medicine);
        fief.Economy.FixtureSetTier(0, 1);
        Assert.False(fief.Economy.FixtureIronGateOpen(0, Garden, includeAppetite: false), "no garden outside Discovery");
    }

    [Fact]
    public void A_garden_works_only_with_a_botanist_and_then_strengthens_medical_care_by_fifteen_percent()
    {
        BronzeFixture f = Town();
        f.Economy.FixtureSetIronWork(0, Garden, 1);
        Fix64 m = Fix64.FromRatio(3, 4);
        Fix64 plain = f.Economy.FixtureMedCare(0, m);
        f.Economy.FixtureClearRoles(0);
        Assert.False(f.Economy.FixtureGardenStaffed(0), "an empty garden has no effect");
        Assert.Equal(plain, f.Economy.FixtureCareFor(0, m));

        f.RunSeasons(1);
        Assert.Equal(1, f.Economy.FixtureRoleCount(0, "botanist"));
        Assert.True(f.Economy.FixtureGardenStaffed(0));
        Fix64 expected = Fix64.One - (Fix64.One - plain) * Fix64.FromRatio(115, 100);
        Assert.Equal(expected, f.Economy.FixtureCareFor(0, m));
        Assert.True(f.Economy.FixtureCareFor(0, m) < plain, "lower is better");

        f.Economy.FixtureClearRoles(0);
        Assert.Equal(plain, f.Economy.FixtureCareFor(0, m));
    }

    [Fact]
    public void Anatomy_strengthens_wound_care_only_in_a_staffed_hospital_and_only_in_Discovery()
    {
        string[] all = { "KEY-5", "MED-07", "MED-09" };
        BronzeFixture f = Town(all);
        Fix64 baseline = Fix64.Parse("0.7");
        Assert.Equal(baseline, f.Economy.FixtureWoundCareFactor(0));          // no hospital yet
        f.Economy.FixtureBuildIronStructure(0, "hospital");
        f.RunSeasons(1);
        Assert.True(f.Economy.FixtureHospitalStaffed(0));
        Fix64 surgical = f.Economy.FixtureWoundCareFactor(0);
        Assert.Equal(Fix64.One - (Fix64.One - baseline) * Fix64.FromRatio(13, 10), surgical);
        f.Economy.FixtureClearRoles(0);
        Assert.True(baseline == f.Economy.FixtureWoundCareFactor(0), "an unstaffed hospital does not operate");

        BronzeFixture noAnatomy = Town("KEY-5", "MED-07");
        noAnatomy.Economy.FixtureBuildIronStructure(0, "hospital");
        noAnatomy.RunSeasons(1);
        Assert.Equal(baseline, noAnatomy.Economy.FixtureWoundCareFactor(0));

        BronzeFixture fief = BronzeFixture.CreateFiefWithCoalDeposit(TestPack.Pack, Seed, 150, all);
        fief.Economy.FixtureSetTier(0, 1);
        fief.Economy.FixtureBuildIronStructure(0, "hospital");
        fief.RunSeasons(1);
        Assert.True(baseline == fief.Economy.FixtureWoundCareFactor(0), "anatomy does nothing in Fief");
    }

    [Fact]
    public void Stall_fed_herds_raise_field_yield_five_percent_only_where_herds_and_fields_share_a_town_in_Discovery()
    {
        string[] hus = { "KEY-5", "HUS-11" };
        BronzeFixture f = Town(hus);
        Assert.Equal(Fix64.One, f.Economy.FixtureStallManure(0)); // no herds
        f.Economy.FixtureFood(0, fields: 4, Fix64.One, pastures: 2, cattle: Fix64.FromInt(2), sheep: Fix64.Zero, carts: 0);
        Assert.Equal(Fix64.FromRatio(105, 100), f.Economy.FixtureStallManure(0));

        BronzeFixture noProject = Town("KEY-5");
        noProject.Economy.FixtureFood(0, fields: 4, Fix64.One, pastures: 2, cattle: Fix64.FromInt(2), sheep: Fix64.Zero, carts: 0);
        Assert.Equal(Fix64.One, noProject.Economy.FixtureStallManure(0));

        BronzeFixture fief = BronzeFixture.CreateFiefWithCoalDeposit(TestPack.Pack, Seed, 150, hus);
        fief.Economy.FixtureFood(0, fields: 4, Fix64.One, pastures: 2, cattle: Fix64.FromInt(2), sheep: Fix64.Zero, carts: 0);
        Assert.Equal(Fix64.One, fief.Economy.FixtureStallManure(0));
    }
}
