using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Tests;

public sealed class HideYieldTests
{
    private const ulong Seed = 20261019;

    private static BronzeFixture Fixture() => BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100);

    [Fact]
    public void Large_animal_hunt_yields_fifty_percent_more_hides_without_changing_meat_or_remaining_herds()
    {
        BronzeFixture fixture = Fixture();
        LivingEconomyModel economy = fixture.Economy;
        int tile = economy.Settlement(0).SiteTile;
        int herdIndex = -1;
        for (int i = 0; i < economy.WildHerdCount; i++)
        {
            WildHerdView herd = economy.WildHerd(i);
            if (herd.Species == HerdSpecies.Aurochs) herdIndex = i;
            economy.FixtureSetWildHerd(i, tile, Fix64.Zero);
        }
        Assert.True(herdIndex >= 0, "Fixture should include an aurochs herd.");
        economy.FixtureSetWildHerd(herdIndex, tile, Fix64.One);
        Fix64 hideBefore = economy.GoodStock(0, "G-HIDE");
        Fix64 meatBefore = economy.GoodStock(0, "G-MEAT");

        Fix64 meatHarvested = economy.FixtureHuntWildHerd(0, Fix64.FromInt(100), Fix64.One,
            enforceSustainableLimit: false);

        Assert.Equal(Fix64.FromInt(15), meatHarvested);
        Assert.Equal(meatBefore + meatHarvested, economy.GoodStock(0, "G-MEAT"));
        Assert.Equal(hideBefore + Fix64.Parse("1.5"), economy.GoodStock(0, "G-HIDE"));
        Assert.Equal(Fix64.Zero, economy.WildHerd(herdIndex).AnimalCount);
    }

    [Fact]
    public void Domestic_sheep_and_cattle_hide_byproducts_rise_fifty_percent_without_changing_meat_or_herd_size()
    {
        BronzeFixture fixture = Fixture();
        LivingEconomyModel economy = fixture.Economy;
        economy.FixtureFood(0, fields: 0, soilShare: Fix64.Zero, pastures: 1,
            cattle: Fix64.FromInt(2), sheep: Fix64.FromInt(4), carts: 0);
        Fix64 hideBefore = economy.GoodStock(0, "G-HIDE");
        Fix64 meatBefore = economy.GoodStock(0, "G-MEAT");

        economy.FixtureRunDomesticHerdByproducts(0);

        AssertFixClose(hideBefore + Fix64.Parse("0.525"), economy.GoodStock(0, "G-HIDE"));
        AssertFixClose(meatBefore + Fix64.Parse("2.05"), economy.GoodStock(0, "G-MEAT"));
        DomesticHerdView herd = economy.DomesticHerd(0);
        Assert.Equal(Fix64.FromInt(4), herd.SmallStock);
        Assert.Equal(Fix64.FromInt(2), herd.Cattle);
    }

    private static void AssertFixClose(Fix64 expected, Fix64 actual)
        => Assert.True(Fix64.Abs(expected - actual) <= Fix64.Parse("0.00001"), $"Expected {expected}, got {actual}.");

    [Fact]
    public void Aggregate_hunting_byproducts_yield_fifty_percent_more_hides_and_leave_meat_untouched()
    {
        BronzeFixture fixture = Fixture();
        LivingEconomyModel economy = fixture.Economy;
        Fix64 hideBefore = economy.GoodStock(0, "G-HIDE");
        Fix64 meatBefore = economy.GoodStock(0, "G-MEAT");

        economy.FixtureApplyAggregateHuntByproducts(0, Fix64.FromInt(40));

        AssertFixClose(hideBefore + Fix64.FromInt(3), economy.GoodStock(0, "G-HIDE"));
        Assert.Equal(meatBefore, economy.GoodStock(0, "G-MEAT"));
    }
}
