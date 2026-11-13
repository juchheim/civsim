using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>Faith &amp; Fief's two-field baseline and AGR-11's physical three-field rotation (docs/MVP/27 §5.3).</summary>
public sealed class FiefLandTests
{
    private const ulong Seed = 20261019;

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
    public void Field_rotation_is_inert_before_KEY5_and_outside_Fief()
    {
        BronzeFixture preKey = Fief("AGR-11");
        BronzeFixture iron = Iron("KEY-5", "AGR-11");
        BronzeFixture bronze = Bronze("KEY-5", "AGR-11");
        foreach (BronzeFixture fixture in new[] { preKey, iron, bronze })
        {
            fixture.Economy.FixtureSetTier(0, 1);
            fixture.Economy.FixtureFillFields(0);
            Assert.Equal(fixture.Economy.FixtureFieldCount(0), fixture.Economy.FixtureCroppedFields(0, 0));
            Assert.Equal(Fix64.One, fixture.Economy.FixtureFiefFieldYield(0));
        }
    }

    private static void AssertFixClose(Fix64 expected, Fix64 actual)
    {
        long tolerance = Fix64.FromRatio(1, 1_000_000).Raw;
        Assert.InRange(actual.Raw, expected.Raw - tolerance, expected.Raw + tolerance);
    }
}
