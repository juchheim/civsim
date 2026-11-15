using System;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using Xunit.Abstractions;

namespace CivSim.Tests;

/// <summary>Faith &amp; Fief's two-field baseline and AGR-11's physical three-field rotation (docs/MVP/27 §5.3).</summary>
public sealed class FiefLandTests
{
    private const ulong Seed = 20261019;
    private readonly ITestOutputHelper _output;

    public FiefLandTests(ITestOutputHelper output) => _output = output;

    private static BronzeFixture Fief(params string[] projects)
        => BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, projects);

    private static BronzeFixture Iron(params string[] projects)
        => BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 100, projects);

    private static BronzeFixture Bronze(params string[] projects)
        => BronzeFixture.Create(TestPack.Pack, Seed, 100, coppice: true, tin: true, arsenic: true, extraProjects: projects);

    [Fact]
    public void AGR11_crops_two_fields_in_three_and_raises_the_physical_average_by_half()
    {
        BronzeFixture twoField = Fief("KEY-5");
        BronzeFixture threeField = Fief("KEY-5", "AGR-11");
        foreach (BronzeFixture fixture in new[] { twoField, threeField })
        {
            fixture.Economy.FixtureSetTier(0, 1);
            fixture.Economy.FixtureFillFields(0);
        }

        Assert.Equal(twoField.Economy.FixtureFieldCount(0), threeField.Economy.FixtureFieldCount(0));
        Assert.True(twoField.Economy.FixtureFieldCount(0) >= 3);
        int twoFieldCrops = 0, threeFieldCrops = 0;
        Fix64 twoFieldYield = Fix64.Zero, threeFieldYield = Fix64.Zero;
        for (int year = 0; year < 6; year++)
        {
            twoFieldCrops += twoField.Economy.FixtureCroppedFields(0, year);
            threeFieldCrops += threeField.Economy.FixtureCroppedFields(0, year);
            twoFieldYield += twoField.Economy.FixtureHarvestFieldYear(0, year);
            threeFieldYield += threeField.Economy.FixtureHarvestFieldYear(0, year);
        }

        // Across six years each strip is cropped three times in the two-field system and four times in the three-field system.
        Assert.Equal(twoFieldCrops * 4, threeFieldCrops * 3);
        AssertFixClose(Fix64.FromRatio(4, 3), threeField.Economy.FixtureFieldLaborNeed(0) / twoField.Economy.FixtureFieldLaborNeed(0));

        // (2/3 cropped × 1.125 legume yield) / (1/2 cropped) = the catalog's +50% productivity.
        Fix64 twoFieldShare = Fix64.Half;
        Fix64 threeFieldShare = Fix64.FromRatio(2, 3) * threeField.Economy.FixtureFiefFieldYield(0);
        AssertFixClose(Fix64.FromRatio(3, 2), threeFieldShare / twoFieldShare);
        Fix64 observedYieldGain = threeFieldYield / twoFieldYield;
        Assert.InRange(observedYieldGain.Raw, Fix64.FromRatio(135, 100).Raw, Fix64.FromRatio(165, 100).Raw);
    }

    [Fact]
    public void Fief_food_oracle_covers_five_geographies_and_conserves_harvested_grain()
    {
        _output.WriteLine("fixture    fields  farm-ws/y base/AGR11  grain-rations/y base/AGR11  grain-share  FoodCap ratio  support base/AGR11  grain-units/6y base/AGR11  ratio");
        (FiefLandGeography Geography, string GrainShare, string CapacityRatio, string HarvestRatio)[] cases =
        {
            (FiefLandGeography.NonRiver, "0.170", "1.085", "1.467"),
            (FiefLandGeography.River, "0.199", "1.100", "1.472"),
            (FiefLandGeography.Hill, "0.116", "1.058", "1.452"),
            (FiefLandGeography.Inland, "0.218", "1.109", "1.465"),
            (FiefLandGeography.Coastal, "0.139", "1.069", "1.460"),
        };
        foreach ((FiefLandGeography geography, string expectedGrainShare, string expectedCapRatioText, string expectedHarvestRatio) in cases)
        {
            BronzeFixture twoField = BronzeFixture.CreateFiefLandGeography(TestPack.Pack, Seed, 100, geography, "KEY-5");
            BronzeFixture threeField = BronzeFixture.CreateFiefLandGeography(TestPack.Pack, Seed, 100, geography, "KEY-5", "AGR-11");
            foreach (BronzeFixture fixture in new[] { twoField, threeField })
            {
                fixture.Economy.FixtureSetTier(0, 1);
                fixture.Economy.FixtureFillFields(0);
            }

            int fieldCount = twoField.Economy.FixtureFieldCount(0);
            Assert.True(fieldCount >= 3);
            Assert.Equal(fieldCount, threeField.Economy.FixtureFieldCount(0));

            FoodIncomeBreakdown baseline = twoField.Economy.SustainableFoodIncomeBreakdown(0);
            FoodIncomeBreakdown treatment = threeField.Economy.SustainableFoodIncomeBreakdown(0);
            AssertFixClose(Fix64.FromRatio(3, 2), treatment.Grain / baseline.Grain);
            Fix64 expectedCapacityRatio = Fix64.One + baseline.Grain / baseline.Total * Fix64.Half;
            Fix64 capacityRatio = threeField.Economy.SustainableFoodCapacity(0) / twoField.Economy.SustainableFoodCapacity(0);
            AssertFixClose(expectedCapacityRatio, capacityRatio);
            AssertFixClose(Fix64.Parse(expectedGrainShare), baseline.Grain / baseline.Total, Fix64.FromRatio(1, 1000));
            AssertFixClose(Fix64.Parse(expectedCapRatioText), capacityRatio, Fix64.FromRatio(1, 1000));
            Assert.InRange(capacityRatio.Raw, Fix64.One.Raw, Fix64.FromRatio(3, 2).Raw);
            AssertFixClose(Fix64.FromRatio(4, 3),
                threeField.Economy.FixtureFieldLaborNeed(0) / twoField.Economy.FixtureFieldLaborNeed(0));

            Fix64 baseBegin = twoField.Economy.FixtureGrainUnits(0);
            Fix64 treatmentBegin = threeField.Economy.FixtureGrainUnits(0);
            Assert.Equal(Fix64.Zero, baseBegin);
            Assert.Equal(Fix64.Zero, treatmentBegin);
            Fix64 baseHarvest = Fix64.Zero;
            Fix64 treatmentHarvest = Fix64.Zero;
            for (int year = 0; year < 6; year++)
            {
                baseHarvest += twoField.Economy.FixtureHarvestFieldYear(0, year);
                treatmentHarvest += threeField.Economy.FixtureHarvestFieldYear(0, year);
            }
            Assert.Equal(baseBegin + baseHarvest, twoField.Economy.FixtureGrainUnits(0));
            Assert.Equal(treatmentBegin + treatmentHarvest, threeField.Economy.FixtureGrainUnits(0));
            Fix64 physicalRatio = treatmentHarvest / baseHarvest;
            Assert.InRange(physicalRatio.Raw, Fix64.FromRatio(135, 100).Raw, Fix64.FromRatio(165, 100).Raw);
            AssertFixClose(Fix64.Parse(expectedHarvestRatio), physicalRatio, Fix64.FromRatio(1, 1000));

            _output.WriteLine($"{geography,-10} {twoField.Economy.FixtureFieldLaborNeed(0).ToDoubleForDisplay(),5:F1}/" +
                $"{threeField.Economy.FixtureFieldLaborNeed(0).ToDoubleForDisplay(),4:F1} {baseline.Grain.ToDoubleForDisplay(),9:F2}/"+
                $"{treatment.Grain.ToDoubleForDisplay(),7:F2} {(baseline.Grain / baseline.Total).ToDoubleForDisplay(),11:P1} " +
                $"{capacityRatio.ToDoubleForDisplay(),12:F3} " +
                $"{twoField.Economy.SustainableFoodCapacity(0).ToDoubleForDisplay(),6:F1}/"+
                $"{threeField.Economy.SustainableFoodCapacity(0).ToDoubleForDisplay(),5:F1} {baseHarvest.ToDoubleForDisplay(),9:F2}/"+
                $"{treatmentHarvest.ToDoubleForDisplay(),7:F2} {physicalRatio.ToDoubleForDisplay(),5:F3}");
        }
    }

    [Fact]
    public void Field_rotation_is_inert_before_KEY5_and_outside_Fief()
    {
        (BronzeFixture Treated, BronzeFixture Control)[] pairs =
        {
            (Fief("AGR-11"), Fief()),
            (Iron("KEY-5", "AGR-11"), Iron("KEY-5")),
            (Bronze("KEY-5", "AGR-11"), Bronze("KEY-5")),
        };
        foreach ((BronzeFixture treated, BronzeFixture control) in pairs)
        {
            foreach (BronzeFixture fixture in new[] { treated, control })
            {
                fixture.Economy.FixtureSetTier(0, 1);
                fixture.Economy.FixtureFillFields(0);
            }
            Assert.Equal(treated.Economy.FixtureFieldCount(0), treated.Economy.FixtureCroppedFields(0, 0));
            Assert.Equal(Fix64.One, treated.Economy.FixtureFiefFieldYield(0));
            Assert.Equal(control.Economy.SustainableFoodCapacity(0), treated.Economy.SustainableFoodCapacity(0));
        }
    }

    private static void AssertFixClose(Fix64 expected, Fix64 actual, Fix64? tolerance = null)
    {
        long allowed = (tolerance ?? Fix64.FromRatio(1, 1_000_000)).Raw;
        Assert.InRange(actual.Raw, expected.Raw - allowed, expected.Raw + allowed);
    }
}
