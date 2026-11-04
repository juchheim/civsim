using System;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>The Iron chapter's craft goods: blown glass, fired brick roofs, the press, wine and oil, barrels (docs/MVP/26 §2.1, §3.2).</summary>
public sealed class IronCraftTests
{
    private const ulong Seed = 20261024;
    private static Fix64 F(string text) => Fix64.Parse(text);

    private static void Near(Fix64 expected, Fix64 actual)
        => Assert.True(Fix64.Abs(expected - actual) <= Fix64.FromRaw(43950), $"expected {expected}, got {actual}");

    [Fact]
    public void Blown_glass_wine_and_oil_are_luxuries_in_the_Iron_chapter_only()
    {
        BronzeFixture iron = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "KEY-4", "CER-07");
        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, Seed, 60, true, true, true, true, false, "KEY-4", "CER-07");
        int before = iron.Economy.FixtureLuxuryItems(0), bronzeBefore = bronze.Economy.FixtureLuxuryItems(0);
        iron.Economy.FixtureStock(0, "G-BLOWNGLASS", F("4"));
        iron.Economy.FixtureStock(0, "G-WINE", F("2"));
        iron.Economy.FixtureStock(0, "G-OILPLANT", F("1"));
        Assert.Equal(before + 7, iron.Economy.FixtureLuxuryItems(0));
        bronze.Economy.FixtureStock(0, "G-BLOWNGLASS", F("4")); // even if some were put there, a Bronze economy counts none of it
        Assert.Equal(bronzeBefore, bronze.Economy.FixtureLuxuryItems(0));
    }

    [Fact]
    public void Grafted_orchards_give_olives_on_dry_ground_and_grapes_on_wooded_ground_and_nothing_ungrafted()
    {
        BronzeFixture f = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "KEY-4", "FOR-08");
        f.Economy.FixtureSetKit(FirstAdult(f.Economy), 11, Fix64.FromInt(20));
        int dry = f.Economy.FixtureTileOfBiomes(Biome.GrasslandSteppe, Biome.Savanna, Biome.OpenWoodland);
        int wooded = f.Economy.FixtureTileOfBiomes(Biome.TemperateForest, Biome.TropicalForest, Biome.Wetland);
        int cold = f.Economy.FixtureTileOfBiomes(Biome.Taiga, Biome.Tundra);
        Assert.True(dry >= 0 && wooded >= 0 && cold >= 0);
        f.Economy.FixtureHarvestOrchard(0, dry);
        Assert.Equal(Fix64.One, f.Economy.GoodStock(0, "G-OLIVE"));
        Assert.Equal(Fix64.Zero, f.Economy.GoodStock(0, "G-GRAPE"));
        f.Economy.FixtureHarvestOrchard(0, wooded);
        Assert.Equal(Fix64.One, f.Economy.GoodStock(0, "G-GRAPE"));
        f.Economy.FixtureHarvestOrchard(0, cold);
        Assert.Equal(Fix64.One, f.Economy.GoodStock(0, "G-GRAPE")); // nothing grows there
        Assert.Equal(Fix64.One, f.Economy.GoodStock(0, "G-OLIVE"));
        // No iron blade, no grafting, no crop.
        BronzeFixture ungrafted = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "KEY-4", "FOR-08");
        ungrafted.Economy.FixtureHarvestOrchard(0, dry);
        Assert.Equal(Fix64.Zero, ungrafted.Economy.GoodStock(0, "G-OLIVE"));
    }

    [Fact]
    public void A_press_turns_two_litres_of_grapes_into_one_of_wine_and_only_with_a_press()
    {
        BronzeFixture f = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "KEY-4", "FOR-08", "MEC-05");
        f.Economy.FixtureStock(0, "G-GRAPE", F("4"));
        Assert.Equal(Fix64.Zero, f.Economy.FixtureRunRecipe(0, "press_wine", Fix64.One, Fix64.FromInt(10)));
        f.Economy.FixtureBuildIronStructure(0, "press");
        Assert.Equal(Fix64.One, f.Economy.FixtureRunRecipe(0, "press_wine", Fix64.One, Fix64.FromInt(10)));
        Assert.Equal(Fix64.One, f.Economy.GoodStock(0, "G-WINE"));
        Assert.Equal(F("2"), f.Economy.GoodStock(0, "G-GRAPE"));
    }

    [Fact]
    public void Liquids_spoil_a_fifth_a_season_and_barrels_take_a_share_of_that_loss_off()
    {
        BronzeFixture open = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "KEY-4", "WOD-07");
        BronzeFixture barrelled = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "KEY-4", "WOD-07");
        foreach (BronzeFixture f in new[] { open, barrelled })
        {
            f.Economy.FixtureStock(0, "G-WINE", F("10"));
            f.Economy.FixtureStock(0, "G-OILPLANT", F("10"));
        }
        barrelled.Economy.FixtureStock(0, "G-BARREL", F("2")); // two barrels hold the twenty litres
        open.Economy.FixtureSpoilLiquids(0);
        barrelled.Economy.FixtureSpoilLiquids(0);
        Near(F("8"), open.Economy.GoodStock(0, "G-WINE"));                 // 20% lost
        Near(F("9"), open.Economy.GoodStock(0, "G-OILPLANT"));             // 10% lost
        Near(F("8.8"), barrelled.Economy.GoodStock(0, "G-WINE"));          // 20% x 0.6 = 12% lost
        Near(F("9.4"), barrelled.Economy.GoodStock(0, "G-OILPLANT"));      // 10% x 0.6 = 6% lost
    }

    [Fact]
    public void A_roofed_town_loses_a_structure_to_fire_half_as_often()
    {
        BronzeFixture roofed = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "KEY-4", "CER-08");
        BronzeFixture bare = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "KEY-4", "CER-08");
        roofed.Economy.FixtureStock(0, "G-FBRICK", F("50")); // far more than one for every twenty people
        int roofedBurns = roofed.Economy.FixtureBurnTrials(0, 400), bareBurns = bare.Economy.FixtureBurnTrials(0, 400);
        Assert.Equal(400, bareBurns);
        Assert.InRange(roofedBurns, 150, 250);
        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, Seed, 60, true, true, true, true, false, "KEY-4", "CER-08");
        Assert.Equal(400, bronze.Economy.FixtureBurnTrials(0, 400));
    }

    private static int FirstAdult(LivingEconomyModel e)
    {
        for (int i = 0; i < e.FigureCount; i++)
            if (e.Figure(i).Civilization == 0 && e.Figure(i).Age >= 14) return i;
        throw new InvalidOperationException("no adult");
    }
}
