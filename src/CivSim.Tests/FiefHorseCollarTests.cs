using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>Horse-collar draft power applies to ploughed Fief fields only when the settlement actually holds tame horses.</summary>
public sealed class FiefHorseCollarTests
{
    private const ulong Seed = 20261019;

    private static BronzeFixture Fief(params string[] projects)
        => BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, projects);

    private static BronzeFixture Iron(params string[] projects)
        => BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 100, projects);

    [Fact]
    public void Horse_collar_reduces_only_the_ploughing_share_and_needs_horses_in_Fief()
    {
        BronzeFixture withoutCollar = Fief("KEY-5", "AGR-11", "NRG-02", "HUS-07");
        BronzeFixture collarWithoutHorses = Fief("KEY-5", "AGR-11", "NRG-02", "HUS-07", "NRG-04");
        BronzeFixture collarWithHorses = Fief("KEY-5", "AGR-11", "NRG-02", "HUS-07", "NRG-04");
        Fix64 baseNeed = Prepare(withoutCollar, 2);
        Fix64 noHorsesNeed = Prepare(collarWithoutHorses, 0);
        Fix64 horseNeed = Prepare(collarWithHorses, 1);

        Assert.Equal(baseNeed, noHorsesNeed); // researched but no G-HORSE held: no draft benefit
        // Twenty fields give 13 1/3 cropped strips after AGR-11. One collared horse ploughs eight of those strips;
        // halving that ploughing labor saves four work-seasons out of the 13 1/3 baseline.
        AssertFixClose(Fix64.FromRatio(7, 10), horseNeed / baseNeed);

        BronzeFixture ironWithoutCollar = Iron("KEY-5", "AGR-11", "NRG-02", "HUS-07");
        BronzeFixture ironWithCollar = Iron("KEY-5", "AGR-11", "NRG-02", "HUS-07", "NRG-04");
        Assert.Equal(Prepare(ironWithoutCollar, 2), Prepare(ironWithCollar, 2)); // Iron mode ignores the E5 draft rule

        BronzeFixture bronzeWithCollar = BronzeFixture.Create(TestPack.Pack, Seed, 100, coppice: true,
            extraProjects: ["KEY-5", "AGR-11", "NRG-02", "HUS-07", "NRG-04"]);
        BronzeFixture bronzeWithoutHorses = BronzeFixture.Create(TestPack.Pack, Seed, 100, coppice: true,
            extraProjects: ["KEY-5", "AGR-11", "NRG-02", "HUS-07", "NRG-04"]);
        Assert.Equal(Prepare(bronzeWithCollar, 2), Prepare(bronzeWithoutHorses, 0));
    }

    [Fact]
    public void Horse_collar_reduces_modeled_haul_labor_but_not_woodcutting_labor()
    {
        BronzeFixture withoutHorses = Fief("KEY-5", "NRG-04");
        BronzeFixture withHorses = Fief("KEY-5", "NRG-04");

        Fix64 withoutHorsesLabor = GatherOuterWood(withoutHorses, 0);
        Fix64 withHorsesLabor = GatherOuterWood(withHorses, 1);

        Assert.True(withHorsesLabor < withoutHorsesLabor);
        Assert.Equal(Fix64.FromInt(10), withHorses.Economy.FixtureWoodStore(0));
        Assert.Equal(Fix64.FromInt(10), withoutHorses.Economy.FixtureWoodStore(0));

        BronzeFixture bronzeWithoutHorses = BronzeFixture.CreateWithIronTile(TestPack.Pack, Seed, 100, ironChain: false, "KEY-5", "NRG-04");
        BronzeFixture bronzeWithHorses = BronzeFixture.CreateWithIronTile(TestPack.Pack, Seed, 100, ironChain: false, "KEY-5", "NRG-04");
        Assert.Equal(GatherOuterWood(bronzeWithoutHorses, 0), GatherOuterWood(bronzeWithHorses, 1));
    }

    private static Fix64 Prepare(BronzeFixture fixture, int horses)
    {
        fixture.Economy.FixtureSetTier(0, 1);
        var fields = fixture.Economy.FixtureFood(0, 20, Fix64.One, pastures: 0, cattle: Fix64.Zero, sheep: Fix64.Zero, carts: 0);
        Assert.Equal(20, fields.Fields);
        fixture.Economy.FixtureSetTameHorses(0, horses);
        return fixture.Economy.FixtureFieldLaborNeed(0);
    }

    private static Fix64 GatherOuterWood(BronzeFixture fixture, int horses)
    {
        fixture.Economy.FixtureSetTameHorses(0, horses);
        int ring = fixture.Economy.FixtureForestRingTile(0);
        Assert.True(ring >= 0);
        fixture.Economy.FixtureClearOwnRangeWood(0);
        fixture.Economy.FixtureSetWood(ring, Fix64.FromInt(60));
        fixture.Economy.FixtureStock(0, "G-WOOD", -fixture.Economy.FixtureWoodStore(0));

        Fix64 availableLabor = Fix64.FromInt(20);
        Fix64 remainingLabor = fixture.Economy.FixtureGatherMaterials(0, wood: 10, stone: 0, clay: 0, labor: availableLabor);
        return availableLabor - remainingLabor;
    }

    private static void AssertFixClose(Fix64 expected, Fix64 actual)
    {
        long tolerance = Fix64.FromRatio(1, 1_000_000).Raw;
        Assert.InRange(actual.Raw, expected.Raw - tolerance, expected.Raw + tolerance);
    }
}
