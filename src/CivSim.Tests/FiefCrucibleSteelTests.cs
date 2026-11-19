using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;

namespace CivSim.Tests;

public sealed class FiefCrucibleSteelTests
{
    private const ulong Seed = 20261116;
    private static Fix64 F(string value) => Fix64.Parse(value);

    [Fact]
    public void Crucible_recipes_store_and_consume_their_registered_goods_only_in_Fief()
    {
        BronzeFixture f = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, "KEY-5", "MET-10", "MET-11");
        LivingEconomyModel e = f.Economy;
        e.FixtureSetMetalFacilities(0, forges: 1, smelters: 0);
        e.FixtureSetGoodStock(0, "G-STEEL", Fix64.One);
        e.FixtureSetGoodStock(0, "G-CLAY", Fix64.One);
        e.FixtureSetGoodStock(0, "G-CHAR", Fix64.One);
        Assert.Equal(Fix64.One, e.FixtureRunRecipe(0, "crucible_steel", Fix64.One, Fix64.FromInt(10)));
        Assert.True(Fix64.Abs(F("0.4") - e.GoodStock(0, "G-CSTEEL")) < F("0.000001"));
        Assert.True(Fix64.Abs(F("0.58") - e.GoodStock(0, "G-STEEL")) < F("0.000001")); // Fief craft loss adds 5% input.

        e.FixtureSetGoodStock(0, "G-WOOD", Fix64.One);
        e.FixtureSetGoodStock(0, "G-LEATHER", Fix64.One);
        Assert.Equal(Fix64.One, e.FixtureRunRecipe(0, "make_crucible_arms", Fix64.One, Fix64.FromInt(10)));
        Assert.Equal(Fix64.One, e.GoodStock(0, "G-ARMS5"));
        Assert.True(Fix64.Abs(F("0.19") - e.GoodStock(0, "G-CSTEEL")) < F("0.000001"));

        BronzeFixture locked = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, "KEY-5", "MET-10");
        locked.Economy.FixtureSetMetalFacilities(0, forges: 1, smelters: 0);
        locked.Economy.FixtureSetGoodStock(0, "G-STEEL", Fix64.One);
        Assert.Equal(Fix64.Zero, locked.Economy.FixtureRunRecipe(0, "crucible_steel", Fix64.One, Fix64.FromInt(10)));

        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "KEY-5", "MET-10", "MET-11");
        iron.Economy.FixtureSetMetalFacilities(0, forges: 1, smelters: 0);
        iron.Economy.FixtureSetGoodStock(0, "G-STEEL", Fix64.One);
        iron.Economy.FixtureSetGoodStock(0, "G-CLAY", Fix64.One);
        iron.Economy.FixtureSetGoodStock(0, "G-CHAR", Fix64.One);
        Assert.Throws<System.ArgumentException>(() => iron.Economy.FixtureRunRecipe(0, "crucible_steel", Fix64.One, Fix64.FromInt(10)));
    }

    [Fact]
    public void Crucible_weapon_strength_is_fixed_and_training_is_a_separate_multiplier()
    {
        Fix64 weapon = F("3.6");
        Assert.Equal(Fix64.FromRatio(36, 10), weapon);
        Assert.True(Fix64.Abs(F("5.4") - weapon * Combat.ProfessionalTraining) < F("0.000001"));
        Assert.True(weapon > Combat.IronWeaponStrength(steelEdge: true));
    }
}
