using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using Xunit;

namespace CivSim.Tests;

public sealed class FiefFurnaceCharcoalFitTests
{
    [Fact]
    public void Opt_in_charcoal_factor_changes_only_Fief_bloomery_charcoal_input()
    {
        BronzeFixture fief = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, 20261114, 100, "MET-12", "NRG-03");
        fief.Economy.FixtureSetIronWork(0, "water_mill", 1);
        fief.Economy.FixtureSetIronWork(0, "blast_furnace", 1);
        (bool active, Fix64 coverage) = fief.Economy.CaptureFiefFurnaceDiagnostic(0);
        Assert.True(active);
        Assert.True(coverage > Fix64.Zero);

        var baseline = fief.Economy.FixtureBatch(0, "bloomery");
        fief.Economy.FurnaceCharcoalFactorOverride = Fix64.Parse("1.5");
        var candidate = fief.Economy.FixtureBatch(0, "bloomery");
        (bool activeAfter, Fix64 coverageAfter) = fief.Economy.CaptureFiefFurnaceDiagnostic(0);
        Fix64 expectedFactor = Fix64.One + Fix64.Half * coverage;
        Fix64 defaultFactor = Fix64.One;

        Assert.Equal(baseline.Output, candidate.Output);
        Assert.Equal(baseline.Input1, candidate.Input1); // ore
        Assert.Equal(baseline.Labor, candidate.Labor);
        Assert.True(activeAfter);
        Assert.Equal(coverage, coverageAfter);
        Assert.Equal(Fix64.One, defaultFactor);
        Assert.Equal(baseline.Input0 * expectedFactor, candidate.Input0); // charcoal only

        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.Pack, 20261114, 100, "MET-12", "NRG-03");
        iron.Economy.FixtureSetIronWork(0, "water_mill", 1);
        var ironBaseline = iron.Economy.FixtureBatch(0, "bloomery");
        iron.Economy.FixtureSetIronWork(0, "blast_furnace", 1);
        iron.Economy.FurnaceCharcoalFactorOverride = Fix64.Parse("1.5");
        Assert.Equal(ironBaseline, iron.Economy.FixtureBatch(0, "bloomery"));
    }

    [Fact]
    public void Fief_default_fuel_fit_keeps_fourfold_iron_and_ore_output_at_full_power()
    {
        const ulong seed = 20261114;
        BronzeFixture fief = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, seed, 100, "MET-12", "NRG-03");
        fief.Economy.FixtureSetIronWork(0, "water_mill", 1);
        fief.Economy.FixtureSetIronWork(0, "windmill", 1);
        fief.Economy.FixtureSetIronWork(0, "blast_furnace", 1);
        (bool active, Fix64 coverage) = fief.Economy.CaptureFiefFurnaceDiagnostic(0);
        Assert.True(active);
        Assert.Equal(Fix64.One, coverage);
        var powered = fief.Economy.FixtureBatch(0, "bloomery");

        BronzeFixture control = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, seed, 100, "MET-12", "NRG-03");
        control.Economy.FixtureSetIronWork(0, "water_mill", 1);
        control.Economy.FixtureSetIronWork(0, "windmill", 1);
        control.Economy.FixtureSetIronWork(0, "blast_furnace", 1);
        control.Economy.FurnaceEffectEnabled = false;
        var plain = control.Economy.FixtureBatch(0, "bloomery");

        Assert.Equal(plain.Output * Fix64.FromInt(4), powered.Output);
        Assert.Equal(plain.Input1 * Fix64.FromInt(4), powered.Input1); // ore remains x4
        Assert.Equal(plain.Input0, powered.Input0); // fitted charcoal input is x1
        Assert.Equal(plain.Labor * Fix64.FromInt(2), powered.Labor);
    }
}
