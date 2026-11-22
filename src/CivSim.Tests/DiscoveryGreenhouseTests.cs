using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>CER-11 plate glass: glazed windows and the greenhouse (M13 WP-E6-7 slice 5, docs/MVP/38 §5.3). Discovery chapter only.</summary>
public class DiscoveryGreenhouseTests
{
    private const ulong Seed = 20261019;
    private static readonly string[] Projects = { "KEY-5", "MED-07", "FOR-09", "CER-11" };

    private static BronzeFixture Town(params string[] projects)
    {
        BronzeFixture f = BronzeFixture.CreateDiscoveryWithCoal(TestPack.Pack, Seed, 150, projects.Length == 0 ? Projects : projects);
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureSetIronWork(0, "botanical_garden", 1);
        f.RunSeasons(1);
        return f;
    }

    [Fact]
    public void A_greenhouse_needs_a_staffed_garden_and_plate_glass_in_store_together()
    {
        BronzeFixture f = Town();
        Assert.True(f.Economy.FixtureGardenStaffed(0));
        Assert.False(f.Economy.FixtureGreenhouseWorking(0), "a garden without glass is no greenhouse");
        Assert.Equal(Fix64.One, f.Economy.FixtureGreenhouseNeedFactor(0));

        f.Economy.FixtureAddDiscoveryGood(0, "G-PLATEGLASS", Fix64.One);
        Assert.True(f.Economy.FixtureGreenhouseWorking(0));
        Assert.Equal(Fix64.One - LivingEconomyModel.GreenhouseWinterShare, f.Economy.FixtureGreenhouseNeedFactor(0));

        f.Economy.FixtureClearRoles(0);
        Assert.False(f.Economy.FixtureGreenhouseWorking(0), "no botanist, no greenhouse");
    }

    [Fact]
    public void Glass_without_CER11_or_outside_Discovery_does_nothing()
    {
        BronzeFixture noProject = Town("KEY-5", "MED-07", "FOR-09");
        noProject.Economy.FixtureAddDiscoveryGood(0, "G-PLATEGLASS", Fix64.One);
        Assert.False(noProject.Economy.FixtureGreenhouseWorking(0));
        Assert.Equal(Fix64.One, noProject.Economy.FixtureGlazedHousingFactor(0));

        BronzeFixture fief = BronzeFixture.CreateFiefWithCoalDeposit(TestPack.Pack, Seed, 150, Projects);
        fief.Economy.FixtureSetTier(0, 1);
        Assert.False(fief.Economy.FixtureGreenhouseWorking(0));
        Assert.Equal(Fix64.One, fief.Economy.FixtureGlazedHousingFactor(0));
    }

    [Fact]
    public void A_glazed_town_houses_a_little_more_by_the_row_value_scaled_by_adoption()
    {
        BronzeFixture f = Town();
        Fix64 plain = f.Economy.FixtureHousingCapPublic(0);
        f.Economy.FixtureAddDiscoveryGood(0, "G-PLATEGLASS", Fix64.One);
        Fix64 factor = f.Economy.FixtureGlazedHousingFactor(0);
        Assert.True(factor >= Fix64.One && factor <= Fix64.FromRatio(105, 100));
        Assert.Equal(plain * factor, f.Economy.FixtureHousingCapPublic(0));
    }
}
