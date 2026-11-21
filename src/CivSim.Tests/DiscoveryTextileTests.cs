using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>FIB-12, the flying shuttle and the spinning jenny (M13 WP-E6-8 slice 2, docs/MVP/38 §5.7). Discovery chapter only.</summary>
public class DiscoveryTextileTests
{
    private const ulong Seed = 20261019;
    private static readonly string[] Loom = { "FIB-06", "FIB-07", "FIB-10", "FIB-11" };
    private static readonly string[] Shuttle = { "FIB-06", "FIB-07", "FIB-10", "FIB-11", "FIB-12" };

    private static BronzeFixture Town(params string[] projects)
    {
        BronzeFixture f = BronzeFixture.CreateDiscoveryWithCoal(TestPack.Pack, Seed, 100, projects);
        f.Economy.FixtureSetTier(0, 1);
        return f;
    }

    private static Fix64 Labor(BronzeFixture f, string recipe) => f.Economy.FixtureBatch(0, recipe).Labor;

    [Fact]
    public void The_jenny_spins_three_times_as_much_for_the_labor_and_leaves_weaving_to_the_shuttle()
    {
        BronzeFixture plain = Town(Loom), jenny = Town(Shuttle);
        Fix64 spin = Labor(plain, "spin_yarn"), spinNow = Labor(jenny, "spin_yarn");
        Assert.True(spinNow < spin);
        Assert.True(Fix64.Abs(spinNow * 3 - spin) <= Fix64.FromRaw(43950), $"x3 ({spin} to {spinNow})");
        Assert.True(Labor(plain, "weave_cloth") == Labor(jenny, "weave_cloth"), "no power, no gain for the shuttle");
        Assert.True(Labor(plain, "bloomery") == Labor(jenny, "bloomery"), "other recipes are untouched");
    }

    [Fact]
    public void The_shuttle_weaves_up_to_three_times_as_much_as_the_ledger_lends_it_power()
    {
        BronzeFixture plain = Town(Loom), powered = Town(Shuttle);
        powered.Economy.FixtureSetIronWork(0, "water_mill", 1);
        plain.Economy.FixtureSetIronWork(0, "water_mill", 1);
        Fix64 base1 = Labor(plain, "weave_cloth"), now = Labor(powered, "weave_cloth");
        Assert.True(now < base1, $"a shuttle with a mill beside it weaves faster ({base1} to {now})");
        Assert.True(now >= base1 / 3 - Fix64.FromRaw(43950), "never more than three times");
    }

    [Fact]
    public void FIB12_does_nothing_in_Fief()
    {
        BronzeFixture fief = BronzeFixture.CreateFiefWithCoalDeposit(TestPack.Pack, Seed, 100, Shuttle);
        BronzeFixture fiefLoom = BronzeFixture.CreateFiefWithCoalDeposit(TestPack.Pack, Seed, 100, Loom);
        Assert.True(Labor(fief, "spin_yarn") == Labor(fiefLoom, "spin_yarn"));
        Assert.True(Labor(fief, "weave_cloth") == Labor(fiefLoom, "weave_cloth"));
    }
}

/// <summary>AGR-12, the four-course rotation and enclosure (M13 WP-E6-8 slice 2, docs/MVP/38 §5.5). Discovery chapter only.</summary>
public class DiscoveryFourCourseTests
{
    private const ulong Seed = 20261019;
    private static readonly string[] ThreeField = { "KEY-5", "AGR-01", "AGR-11" };
    private static readonly string[] FourCourse = { "KEY-5", "AGR-01", "AGR-11", "AGR-12" };

    private static BronzeFixture Farm(bool discovery, params string[] projects)
    {
        BronzeFixture f = discovery
            ? BronzeFixture.CreateDiscoveryWithCoal(TestPack.Pack, Seed, 100, projects)
            : BronzeFixture.CreateFiefWithCoalDeposit(TestPack.Pack, Seed, 100, projects);
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureFillFields(0);
        return f;
    }

    [Fact]
    public void Four_course_crops_half_the_strips_at_nine_fifths_for_x1_2_over_the_three_field()
    {
        BronzeFixture three = Farm(true, ThreeField), four = Farm(true, FourCourse);
        (Fix64 shareThree, Fix64 yieldThree) = three.Economy.FixtureFieldPlan(0);
        (Fix64 shareFour, Fix64 yieldFour) = four.Economy.FixtureFieldPlan(0);
        Assert.True(shareThree > shareFour, "fewer strips bear grain: the rest are fodder and legume");
        Assert.True(Fix64.Abs(shareFour - Fix64.Half) <= Fix64.FromRaw(43950));
        Fix64 perStripThree = shareThree * yieldThree, perStripFour = shareFour * yieldFour;
        Fix64 gain = perStripFour / perStripThree;
        Assert.True(Fix64.Abs(gain - Fix64.FromRatio(12, 10)) <= Fix64.FromRatio(1, 100), $"x1.2 over the three-field ({gain})");
    }

    [Fact]
    public void Enclosure_cuts_the_hands_the_fields_need_to_85_percent_of_the_cropped_labor()
    {
        BronzeFixture three = Farm(true, ThreeField), four = Farm(true, FourCourse);
        Fix64 needThree = three.Economy.FixtureFieldLaborNeed(0), needFour = four.Economy.FixtureFieldLaborNeed(0);
        Assert.True(needFour < needThree);
        Fix64 expected = needThree * Fix64.FromRatio(3, 4) * Fix64.FromRatio(85, 100); // cropped share 1/2 against 2/3, then x0.85
        Assert.True(Fix64.Abs(needFour - expected) <= Fix64.FromRatio(1, 50), $"{needFour} against {expected}");
    }

    [Fact]
    public void A_strip_is_cropped_every_other_year_and_the_rotation_is_inert_in_Fief()
    {
        BronzeFixture four = Farm(true, FourCourse);
        int cropped0 = four.Economy.FixtureCroppedFields(0, 0), cropped1 = four.Economy.FixtureCroppedFields(0, 1);
        int fields = four.Economy.FixtureFieldCount(0);
        Assert.True(cropped0 + cropped1 == fields, "each strip bears grain in exactly one of two consecutive years");

        BronzeFixture fief = Farm(false, FourCourse), fiefThree = Farm(false, ThreeField);
        Assert.True(fief.Economy.FixtureFieldPlan(0) == fiefThree.Economy.FixtureFieldPlan(0));
        Assert.True(fief.Economy.FixtureFieldLaborNeed(0) == fiefThree.Economy.FixtureFieldLaborNeed(0));
    }
}
