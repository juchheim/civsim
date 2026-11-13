using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>
/// Timber and stone for Iron construction (M12 WP-E5-0, docs/MVP/balance/m12-wp7-build-wood.md): an Iron structure asks for its timber, a town whose own range is
/// felled out cuts from the wider reach, and sister towns lend the stone a structure lacks. All three exist only in the Iron chapter.
/// </summary>
public class IronBuildMaterialsTests
{
    private const ulong Seed = 20261019;

    private static BronzeFixture Pair(out int daughter, params string[] projects)
    {
        BronzeFixture f = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, projects);
        daughter = f.Economy.FoundByDecree(0, f.CampTile, Fix64.FromRatio(2, 5), minDaughter: 6, startCohesion: 60);
        Assert.True(daughter >= 0);
        return f;
    }

    [Fact]
    public void A_structure_under_construction_asks_for_its_timber_in_the_Iron_chapter_only()
    {
        BronzeFixture iron = Pair(out _, "SOC-07", "LRN-05");
        iron.Economy.FixtureSetIronBuild(0, "academy", Fix64.Zero);   // academy: 2 L of timber
        Assert.Equal(Fix64.FromInt(2), iron.Economy.FixtureBuildWoodNeed(0));
        iron.Economy.FixtureSetIronBuild(0, "crane", Fix64.Zero);     // crane: 8 L
        Assert.Equal(Fix64.FromInt(8), iron.Economy.FixtureBuildWoodNeed(0));
        iron.Economy.IronBuildWoodProcurementEnabled = false;
        Assert.Equal(Fix64.Zero, iron.Economy.FixtureBuildWoodNeed(0));

        BronzeFixture noBuild = Pair(out _, "SOC-07");
        Assert.Equal(Fix64.Zero, noBuild.Economy.FixtureBuildWoodNeed(0)); // nothing under construction
    }

    [Fact]
    public void A_town_with_no_wood_in_its_own_range_fells_the_rest_from_the_reach_and_the_wood_moves_exactly()
    {
        BronzeFixture f = Pair(out _, "SOC-07");
        int ring = f.Economy.FixtureForestRingTile(0);
        Assert.True(ring >= 0, "the fixture town has forest within its 8-tile reach");
        f.Economy.FixtureClearOwnRangeWood(0);
        f.Economy.FixtureSetWood(ring, Fix64.FromInt(60));
        Fix64 storeBefore = f.Economy.FixtureWoodStore(0), forestBefore = f.Economy.FixtureStandingWoodTotal();

        Fix64 taken = f.Economy.FixtureHarvestWood(0, Fix64.FromInt(10));

        Assert.True(taken > Fix64.Zero, "a felled-out town cuts from the ring");
        Assert.Equal(storeBefore + taken, f.Economy.FixtureWoodStore(0));            // into the store at the full amount
        Assert.Equal(forestBefore - taken, f.Economy.FixtureStandingWoodTotal());               // out of the forest by the same amount
        Assert.True(f.Economy.FixtureWoodStock(ring) >= Fix64.FromInt(5));                      // the forest keeps the 5 L that stops a tile being cleared

        BronzeFixture off = Pair(out _, "SOC-07");
        int ring2 = off.Economy.FixtureForestRingTile(0);
        off.Economy.FixtureClearOwnRangeWood(0);
        off.Economy.FixtureSetWood(ring2, Fix64.FromInt(60));
        off.Economy.IronChopReachEnabled = false;
        Assert.Equal(Fix64.Zero, off.Economy.FixtureHarvestWood(0, Fix64.FromInt(10))); // the old behavior: nothing beyond the own range
    }

    [Fact]
    public void Sister_towns_lend_spare_stone_a_donors_own_structure_keeps_what_it_needs_and_none_is_created()
    {
        BronzeFixture f = Pair(out int daughter, "SOC-07", "MED-06", "BLD-10");
        f.Economy.FixtureSetIronBuild(daughter, "aqueduct", Fix64.Zero);   // needs 20 blocks and 8 ashlar
        f.Economy.FixtureSetIronBuild(0, "bathhouse", Fix64.Zero);         // the donor's own: needs 8 blocks
        f.Economy.FixtureStock(0, "G-ASHLAR", Fix64.FromInt(30));
        f.Economy.FixtureStock(0, "G-BLOCK", Fix64.FromInt(12));
        Fix64 stoneBefore = f.Economy.FixtureBlockStock(0, false) + f.Economy.FixtureBlockStock(0, true)
            + f.Economy.FixtureBlockStock(daughter, false) + f.Economy.FixtureBlockStock(daughter, true);

        Fix64 moved = f.Economy.FixtureShareBuildStone(0);

        Assert.True(moved > Fix64.Zero);
        Fix64 stoneAfter = f.Economy.FixtureBlockStock(0, false) + f.Economy.FixtureBlockStock(0, true)
            + f.Economy.FixtureBlockStock(daughter, false) + f.Economy.FixtureBlockStock(daughter, true);
        Assert.Equal(stoneBefore, stoneAfter);                                                   // none created or lost
        Assert.Equal(Fix64.FromInt(8), f.Economy.FixtureBlockStock(daughter, true));           // the aqueduct's ashlar is met from the donor's spare ashlar
        Assert.True(f.Economy.FixtureBlockStock(daughter, false) >= Fix64.FromInt(20));        // and its blocks, spare blocks first and then spare ashlar
        Assert.True(f.Economy.FixtureBlockStock(0, false) + f.Economy.FixtureBlockStock(0, true) > Fix64.Zero);
        Assert.True(f.Economy.FixtureBlockStock(0, false) >= Fix64.FromInt(8) || f.Economy.FixtureBlockStock(0, true) > Fix64.Zero); // the donor kept its own bathhouse's 8 blocks' worth
    }

    [Fact]
    public void Stone_sharing_can_be_switched_off_and_never_runs_in_the_Bronze_chapter()
    {
        BronzeFixture f = Pair(out int daughter, "SOC-07", "BLD-10");
        f.Economy.FixtureSetIronBuild(daughter, "aqueduct", Fix64.Zero);
        f.Economy.FixtureStock(0, "G-ASHLAR", Fix64.FromInt(40));
        f.Economy.IronBuildStoneSharingEnabled = false;
        Assert.Equal(Fix64.Zero, f.Economy.FixtureShareBuildStone(0));

        // In the Bronze chapter nothing is lent, whatever a town holds or lacks.
        BronzeFixture bronze = BronzeFixture.CreateWithIronTile(TestPack.Pack, Seed, 60, ironChain: false, "SOC-07", "BLD-10");
        int d2 = bronze.Economy.FoundByDecree(0, bronze.CampTile, Fix64.FromRatio(2, 5), minDaughter: 6, startCohesion: 60);
        Assert.True(d2 >= 0);
        bronze.Economy.FixtureSetIronBuild(d2, "aqueduct", Fix64.Zero);
        bronze.Economy.FixtureStock(0, "G-ASHLAR", Fix64.FromInt(40));
        Assert.Equal(Fix64.Zero, bronze.Economy.FixtureShareBuildStone(0));
    }
}
