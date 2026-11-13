using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>Faith &amp; Fief's remaining land and food rules (docs/MVP/27 §5.3).</summary>
public sealed class FiefLandSystemsTests
{
    private const ulong Seed = 20261019;

    private static BronzeFixture Fief(params string[] projects)
        => BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, projects);

    private static BronzeFixture Iron(params string[] projects)
        => BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 100, projects);

    private sealed class FixedOwnership(int owner) : ITileOwnership
    {
        public int OwnerOf(int tile) => owner;
    }

    [Fact]
    public void Selective_breeding_boosts_herd_yield_and_draft_work_only_in_Fief()
    {
        BronzeFixture plain = Fief("KEY-5", "AGR-05");
        BronzeFixture bred = Fief("KEY-5", "AGR-05", "HUS-10");
        foreach (BronzeFixture fixture in new[] { plain, bred })
        {
            fixture.Economy.FixtureSetTier(0, 1);
            fixture.Economy.FixtureFood(0, 6, Fix64.One, pastures: 0, cattle: Fix64.FromInt(2), sheep: Fix64.Zero, carts: 0);
        }

        Assert.Equal(Fix64.One, plain.Economy.FixtureFiefHerdYield(0));
        Assert.Equal(Fix64.FromRatio(3, 2), bred.Economy.FixtureFiefHerdYield(0));
        AssertFixClose(Fix64.FromRatio(6, 5), bred.Economy.FixtureFiefDraftStrength(0));
        Assert.True(bred.Economy.FixtureFieldLaborNeed(0) < plain.Economy.FixtureFieldLaborNeed(0));

        BronzeFixture ironPlain = Iron("KEY-5", "AGR-05");
        BronzeFixture ironBred = Iron("KEY-5", "AGR-05", "HUS-10");
        foreach (BronzeFixture fixture in new[] { ironPlain, ironBred })
        {
            fixture.Economy.FixtureSetTier(0, 1);
            fixture.Economy.FixtureFood(0, 6, Fix64.One, pastures: 0, cattle: Fix64.FromInt(2), sheep: Fix64.Zero, carts: 0);
        }
        Assert.Equal(Fix64.One, ironBred.Economy.FixtureFiefHerdYield(0));
        Assert.Equal(Fix64.One, ironBred.Economy.FixtureFiefDraftStrength(0));
        Assert.Equal(ironPlain.Economy.FixtureFieldLaborNeed(0), ironBred.Economy.FixtureFieldLaborNeed(0));
    }

    [Fact]
    public void Manorial_upkeep_moves_ten_percent_of_claimed_yield_and_TER10_reduces_it_to_seven()
    {
        BronzeFixture baseRate = Fief("KEY-5");
        BronzeFixture estate = Fief("KEY-5", "TER-10");
        foreach (BronzeFixture fixture in new[] { baseRate, estate })
        {
            fixture.Economy.TileOwnership = new FixedOwnership(0);
            fixture.Economy.FixtureSetTier(0, 1);
            fixture.Economy.FixtureSetCivilization(1, 0);
            fixture.Economy.FixtureSetTier(1, 1);
            fixture.Economy.FixtureStock(0, "G-GRAIN", Fix64.FromInt(100));
            fixture.Economy.FixtureStock(1, "G-GRAIN", Fix64.Zero);
        }

        Fix64 baseBefore = baseRate.Economy.FixtureGrainUnits(0);
        Fix64 estateBefore = estate.Economy.FixtureGrainUnits(0);
        AssertFixClose(Fix64.FromInt(10), baseRate.Economy.FixtureClaimUpkeep(0, Fix64.FromInt(100)));
        AssertFixClose(Fix64.FromInt(7), estate.Economy.FixtureClaimUpkeep(0, Fix64.FromInt(100)));
        AssertFixClose(baseBefore - Fix64.FromInt(10), baseRate.Economy.FixtureGrainUnits(0));
        AssertFixClose(estateBefore - Fix64.FromInt(7), estate.Economy.FixtureGrainUnits(0));
        AssertFixClose(Fix64.FromInt(10), baseRate.Economy.FixtureGrainUnits(1));
        AssertFixClose(Fix64.FromInt(7), estate.Economy.FixtureGrainUnits(1));

        BronzeFixture preKey = Fief("TER-10");
        BronzeFixture iron = Iron("KEY-5", "TER-10");
        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, Seed, 100, coppice: true, extraProjects: ["KEY-5", "TER-10"]);
        foreach (BronzeFixture fixture in new[] { preKey, iron, bronze })
        {
            fixture.Economy.TileOwnership = new FixedOwnership(0);
            fixture.Economy.FixtureSetTier(0, 1);
            fixture.Economy.FixtureStock(0, "G-GRAIN", Fix64.FromInt(100));
            Assert.Equal(Fix64.Zero, fixture.Economy.FixtureClaimUpkeep(0, Fix64.FromInt(100)));
        }
    }

    [Fact]
    public void Game_reserves_exclude_nonowners_preserve_half_capacity_and_raise_local_cohesion()
    {
        BronzeFixture owner = Fief("KEY-5", "HNT-11");
        owner.Economy.TileOwnership = new FixedOwnership(-1);
        int tile = owner.Economy.Settlement(0).SiteTile;
        Fix64 capacity = owner.Economy.FixtureWildHerdCarryingCapacity(0);
        owner.Economy.FixtureSetWildHerd(0, tile, capacity);
        int targetWithoutReserve = owner.Economy.FixtureCohesionTarget(0);
        owner.Economy.TileOwnership = new FixedOwnership(0);
        Assert.True(owner.Economy.FixtureSettlementHasReserve(0));
        Assert.Equal(targetWithoutReserve + 2, owner.Economy.FixtureCohesionTarget(0));

        // HNT-11 requires a falconer as well as a hunter: one assigned hunter cannot work the reserve.
        Assert.Equal(Fix64.Zero, owner.Economy.FixtureHuntWildHerd(0, Fix64.FromInt(1000), Fix64.One,
            enforceSustainableLimit: false, huntersAssigned: 1));
        Assert.Equal(capacity, owner.Economy.WildHerd(0).AnimalCount);

        Assert.True(owner.Economy.FixtureHuntWildHerd(0, Fix64.FromInt(1000), Fix64.One,
            enforceSustainableLimit: false, huntersAssigned: 2) > Fix64.Zero);
        AssertFixClose(capacity * Fix64.Half, owner.Economy.WildHerd(0).AnimalCount);

        BronzeFixture nonowner = Fief("KEY-5");
        nonowner.Economy.FixtureComplete(1, "KEY-5", "HNT-11");
        nonowner.Economy.TileOwnership = new FixedOwnership(1);
        int nonownerTile = nonowner.Economy.Settlement(0).SiteTile;
        Fix64 nonownerCapacity = nonowner.Economy.FixtureWildHerdCarryingCapacity(0);
        nonowner.Economy.FixtureSetWildHerd(0, nonownerTile, nonownerCapacity);
        Assert.Equal(Fix64.Zero, nonowner.Economy.FixtureHuntWildHerd(0, Fix64.FromInt(1000), Fix64.One,
            enforceSustainableLimit: false, huntersAssigned: 2));
        Assert.Equal(nonownerCapacity, nonowner.Economy.WildHerd(0).AnimalCount);

        BronzeFixture iron = Iron("KEY-5", "HNT-11");
        iron.Economy.TileOwnership = new FixedOwnership(0);
        iron.Economy.FixtureSetWildHerd(0, iron.Economy.Settlement(0).SiteTile, Fix64.FromInt(100));
        Assert.False(iron.Economy.FixtureSettlementHasReserve(0));
    }

    [Fact]
    public void Timber_framing_increases_Fief_hall_capacity_by_half_but_leaves_Iron_alone()
    {
        BronzeFixture plain = Fief("KEY-5");
        BronzeFixture framed = Fief("KEY-5", "WOD-10");
        foreach (BronzeFixture fixture in new[] { plain, framed })
        {
            fixture.Economy.FixtureSetTier(0, 1);
            fixture.Economy.FixtureCivic(0, "hall");
        }
        Assert.Equal(Fix64.FromInt(15), framed.Economy.FixtureHousingCap(0) - plain.Economy.FixtureHousingCap(0));

        BronzeFixture ironPlain = Iron("KEY-5");
        BronzeFixture ironFramed = Iron("KEY-5", "WOD-10");
        foreach (BronzeFixture fixture in new[] { ironPlain, ironFramed })
        {
            fixture.Economy.FixtureSetTier(0, 1);
            fixture.Economy.FixtureCivic(0, "hall");
        }
        Assert.Equal(ironPlain.Economy.FixtureHousingCap(0), ironFramed.Economy.FixtureHousingCap(0));
    }

    private static void AssertFixClose(Fix64 expected, Fix64 actual)
    {
        long tolerance = Fix64.FromRatio(1, 1_000_000).Raw;
        Assert.InRange(actual.Raw, expected.Raw - tolerance, expected.Raw + tolerance);
    }
}
