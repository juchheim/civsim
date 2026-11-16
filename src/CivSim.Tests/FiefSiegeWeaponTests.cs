using System;
using System.Linq;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using Xunit.Abstractions;

namespace CivSim.Tests;

public class FiefSiegeWeaponTests
{
    private readonly ITestOutputHelper _output;
    public FiefSiegeWeaponTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Fief_cannon_body_is_cast_from_cast_iron_in_a_blast_furnace()
    {
        EconomyRecipe recipe = EconomyContent.FromPack(TestPack.Pack).Recipes.ToArray()
            .Single(row => row.Id == "cast_cannon");

        Assert.Contains("blast_furnace", recipe.Structures);
        Assert.Equal(2, recipe.Inputs.Length);
        EconomyRecipeInput castIron = Assert.Single(recipe.Inputs, input => input.Goods.Contains("G-CAST"));
        Assert.Equal(Fix64.FromInt(2), castIron.Quantity);
        EconomyRecipeInput timber = Assert.Single(recipe.Inputs, input => input.Goods.Contains("G-WOOD"));
        Assert.Equal(Fix64.FromInt(6), timber.Quantity);
        EconomyRecipeOutput cannon = Assert.Single(recipe.Outputs);
        Assert.Equal("G-CANNON", cannon.Good);
        Assert.Equal(Fix64.One, cannon.Quantity);
    }

    [Fact]
    public void Cannon_shots_consume_one_quarter_powder_and_return_only_paid_shots()
    {
        BronzeFixture f = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, 20261114, 100, "WAR-12");
        f.Economy.FixtureSetGoodStock(0, "G-POWDER", Fix64.FromRatio(3, 4));

        Assert.Equal(3, f.Economy.PayCannonPowder(0, 5));
        Assert.Equal(0, f.Economy.PayCannonPowder(0, 1));
        Assert.Equal(Fix64.Zero, f.Economy.FixtureGoodStock(0, "G-POWDER"));
    }

    [Fact]
    public void Fief_powder_needs_workable_saltpeter_and_sulfur_deposits_and_mines_them_with_labor()
    {
        BronzeFixture sourced = BronzeFixture.CreateFiefWithPowderDeposits(TestPack.Pack, 20261114, 100, "CHM-07", "WAR-12");
        sourced.Economy.FixtureSetIronWork(0, "blast_furnace", 1);
        sourced.Economy.FixtureSetIronWork(0, "powder_mill", 1);
        Assert.True(sourced.Economy.FixtureWorkable(0, sourced.SaltpeterTile));
        Assert.True(sourced.Economy.FixtureWorkable(0, sourced.SulfurTile));
        Assert.True(sourced.Economy.FixtureRecipePlannedBatches(0, "make_powder", Fix64.FromInt(100)) > Fix64.Zero);
        sourced.RunSeasons(8);
        Assert.True(sourced.Economy.FixtureGoodStock(0, "G-SALTPETER") > Fix64.Zero);
        Assert.True(sourced.Economy.FixtureGoodStock(0, "G-SULFUR") > Fix64.Zero);

        BronzeFixture unsourced = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, 20261114, 100, "CHM-07", "WAR-12");
        unsourced.Economy.FixtureSetIronWork(0, "blast_furnace", 1);
        unsourced.Economy.FixtureSetIronWork(0, "powder_mill", 1);
        Assert.Equal(Fix64.Zero, unsourced.Economy.FixtureRecipePlannedBatches(0, "make_powder", Fix64.FromInt(100)));
        unsourced.RunSeasons(8);
        Assert.Equal(Fix64.Zero, unsourced.Economy.FixtureGoodStock(0, "G-POWDER"));
    }

    [Fact]
    public void Fief_chemistry_camp_uses_the_existing_owned_land_camp_and_haul_path()
    {
        BronzeFixture f = BronzeFixture.CreateFiefWithPowderCamp(TestPack.Pack, 20261114, 100, "CHM-07", "WAR-12");
        f.Economy.FixtureSetIronWork(0, "blast_furnace", 1);
        f.Economy.FixtureSetIronWork(0, "powder_mill", 1);
        f.RunSeasons(12);
        Assert.True(f.Economy.FixtureGoodStock(0, "G-SALTPETER") > Fix64.Zero);
        Assert.True(f.Economy.FixtureGoodStock(0, "G-SULFUR") > Fix64.Zero);
    }

    [Fact]
    public void Saltpeter_and_sulfur_paths_stay_unavailable_in_Bronze_and_Iron()
    {
        BronzeFixture bronze = BronzeFixture.CreateBronzeWithPowderDeposits(TestPack.Pack, 20261114, 100, "CHM-07", "WAR-12");
        BronzeFixture iron = BronzeFixture.CreateIronWithPowderDeposits(TestPack.Pack, 20261114, 100, "CHM-07", "WAR-12");
        foreach (BronzeFixture fixture in new[] { bronze, iron })
        {
            Assert.False(fixture.Economy.FixtureWorkable(0, fixture.SaltpeterTile));
            Assert.False(fixture.Economy.FixtureWorkable(0, fixture.SulfurTile));
            fixture.RunSeasons(4);
            Assert.Equal(Fix64.Zero, fixture.Economy.FixtureGoodStock(0, "G-SALTPETER"));
            Assert.Equal(Fix64.Zero, fixture.Economy.FixtureGoodStock(0, "G-SULFUR"));
            Assert.Equal(Fix64.Zero, fixture.Economy.FixtureGoodStock(0, "G-POWDER"));
        }
    }

    [Fact]
    public void Powder_breach_reduces_only_wall_barrier_and_allows_the_storm()
    {
        var intact = new Defenses(false, false, false, false, false, false, false, false, false, false,
            StoneWalls: true, Castle: true, ConcentricCastle: true);
        Defenses breached = intact with { PowderBreach = true };

        Assert.False(intact.CanBeAssaulted());
        Assert.True(breached.CanBeAssaulted());
        Assert.Equal(Fix64.Zero, Sieges.Batter(Fix64.One, 2, Fix64.FromInt(3))); // two ordinary engine hits still breach without the powder modifier
        Assert.Equal(Fix64.Zero, Sieges.Batter(Fix64.One, Sieges.CannonWallDamage, Fix64.FromInt(3)));
        Assert.InRange((breached.Multiplier() - intact.Multiplier() * Fix64.FromRatio(3, 10)).Raw, -4L, 4L);
        Assert.True((intact with { StoneWalls = false, Gatehouse = false }).CanBeAssaulted()); // ordinary engine breach remains stormable

        var hill = intact with { Hillfort = true, PowderBreach = true };
        Assert.Equal(Fix64.FromRatio(11, 10) * Fix64.FromRatio(5, 2) * Defenses.CastleStrongpoint * Defenses.ConcentricCastleMultiplier,
            hill.Multiplier());
    }

    [Fact]
    public void Draft_crew_attrition_is_seeded_per_adult_and_DEF09_multiplies_the_chance_by_125_percent()
    {
        const ulong seed = 20261114;
        int baseline = 0, def09 = 0;
        const int exposures = 100_000;
        for (long figure = 1; figure <= exposures; figure++)
        {
            bool baseLoss = Sieges.RollCrewAttrition(seed, 7, 12, figure, def09Concentric: false);
            Assert.Equal(baseLoss, Sieges.RollCrewAttrition(seed, 7, 12, figure, def09Concentric: false));
            baseline += baseLoss ? 1 : 0;
            def09 += Sieges.RollCrewAttrition(seed, 7, 12, figure, def09Concentric: true) ? 1 : 0;
        }
        static (double Low, double High) Wilson(int count, int n)
        {
            const double z = 1.96;
            double p = (double)count / n;
            double d = 1 + z * z / n;
            double center = (p + z * z / (2 * n)) / d;
            double radius = z * Math.Sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / d;
            return (center - radius, center + radius);
        }
        var baseCi = Wilson(baseline, exposures);
        var defCi = Wilson(def09, exposures);
        _output.WriteLine($"Seed {seed}; assigned-person-turn exposures={exposures} per case; observed base losses={baseline} ({100.0 * baseline / exposures:F3}%, 95% Wilson {100 * baseCi.Low:F3}–{100 * baseCi.High:F3}%), DEF-09 losses={def09} ({100.0 * def09 / exposures:F3}%, 95% Wilson {100 * defCi.Low:F3}–{100 * defCi.High:F3}%). Draft expected losses: 1,000 at 1.00%; 1,250 at 1.25%.");
        Assert.InRange(baseline, 900, 1100); // 1% per assigned adult
        Assert.InRange(def09, 1150, 1350);   // 1.25% per assigned adult
    }
}
