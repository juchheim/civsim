using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>The glass-and-instruments chain (M13 WP-E6-7 slice 3, docs/MVP/38 §5.3). Discovery chapter only.</summary>
public class DiscoveryInstrumentsTests
{
    private const ulong Seed = 20261019;

    [Theory]
    [InlineData("make_alkali"), InlineData("make_acid"), InlineData("make_plate_glass"), InlineData("make_parts"), InlineData("make_chronometer"), InlineData("make_telescope")]
    public void The_chain_recipes_exist_in_Discovery_and_not_outside_it(string recipe)
    {
        BronzeFixture discovery = BronzeFixture.CreateDiscoveryWithCoal(TestPack.Pack, Seed, 150, "KEY-5");
        Assert.True(discovery.Economy.FixtureRecipeEnabled(recipe), $"{recipe} compiles in a Discovery match");
        BronzeFixture fief = BronzeFixture.CreateFiefWithCoalDeposit(TestPack.Pack, Seed, 150, "KEY-5");
        Assert.False(fief.Economy.FixtureRecipeEnabled(recipe), $"{recipe} is off outside Discovery");
    }

    private static BronzeFixture Harbor(params string[] projects)
    {
        BronzeFixture f = BronzeFixture.CreateDiscoveryWithCoal(TestPack.Pack, Seed, 150, projects);
        f.Economy.FixtureSetTier(0, 1);
        return f;
    }

    [Fact]
    public void An_instrument_counts_only_while_one_stands_in_store_in_one_of_the_realms_towns()
    {
        BronzeFixture f = Harbor("KEY-5", "WAY-10", "INQ-07");
        Assert.False(f.Economy.FixtureChronometerHeld(0));
        f.Economy.FixtureAddDiscoveryGood(0, "G-CHRONOMETER", Fix64.One);
        Assert.True(f.Economy.FixtureChronometerHeld(0));
        Assert.False(f.Economy.FixtureTelescopeHeld(0), "a chronometer is not a telescope");
        f.Economy.FixtureAddDiscoveryGood(0, "G-TELESCOPE", Fix64.One);
        Assert.True(f.Economy.FixtureTelescopeHeld(0));
        Assert.Equal(Fix64.One, f.Economy.DiscoveryGoodStock(0, "G-TELESCOPE"));
    }

    [Fact]
    public void Held_alkali_doubles_soap_and_glass_only_with_CHM08_and_only_in_Discovery()
    {
        BronzeFixture f = Harbor("KEY-5", "CHM-08", "CHM-05", "CER-06");
        Assert.Equal(Fix64.One, f.Economy.FixtureAlkaliFactor(0, "soap"));
        f.Economy.FixtureAddDiscoveryGood(0, "G-ALKALI", Fix64.FromInt(2));
        Fix64 soap = f.Economy.FixtureAlkaliFactor(0, "soap"), glass = f.Economy.FixtureAlkaliFactor(0, "glass");
        Assert.True(soap > Fix64.One && soap <= Fix64.FromInt(2), "the typed x2, scaled by adoption");
        Assert.True(glass > Fix64.One && glass <= Fix64.FromInt(2));
        BronzeFixture noProject = Harbor("KEY-5", "CHM-05");
        noProject.Economy.FixtureAddDiscoveryGood(0, "G-ALKALI", Fix64.FromInt(2));
        Assert.Equal(Fix64.One, noProject.Economy.FixtureAlkaliFactor(0, "soap"));
    }
}
