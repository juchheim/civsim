using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>
/// B12 completion-only transfer (docs/MVP/07d "B12 civic-to-metal completion"): a fully funded first Forge or Kiln that the
/// normal assignments leave a sliver short of finishing takes exactly that sliver from discretionary civic construction.
/// </summary>
public class CivicMetalCompletionTests
{
    private const ulong Seed = 20261019;
    private static Fix64 F(string text) => Fix64.Parse(text);

    /// <summary>The Kiln's remaining labor at 1.98 of 2.00, exactly as the planner computes BuildNeed.</summary>
    private static readonly Fix64 Need = Fix64.FromInt(2) - F("1.98");

    private static void Near(Fix64 expected, Fix64 actual)
        => Assert.True(Fix64.Abs(expected - actual) <= Fix64.FromRaw(4295), $"expected {expected}, got {actual}");

    private static BronzeFixture Fixture(string build, string progress, string? civic = "temple", int smelters = 0)
    {
        BronzeFixture f = BronzeFixture.Create(TestPack.Pack, Seed, 60, coppice: false, tin: true, arsenic: true, shaftMining: true, "WAR-05");
        f.Economy.FixtureSetMetalBuild(0, build, F(progress), siteClay: 4, siteStone: 3, smelters);
        if (civic is not null) f.Economy.FixtureSetCivicBuildTarget(0, civic);
        return f;
    }

    [Fact]
    public void A_funded_kiln_stalled_at_198_of_200_takes_exactly_the_missing_labor_and_completes_once()
    {
        BronzeFixture f = Fixture("kiln", "1.98");
        (Fix64 moved, Fix64 assigned) = f.Economy.FixtureCivicToMetalCompletion(0, buildNeed: Fix64.FromInt(2) - F("1.98"), buildAssigned: Fix64.Zero,
            civicAssigned: Fix64.One, civicCanals: Fix64.Zero, civicHousing: Fix64.Zero);
        Near(F("0.02"), moved);
        Assert.Equal(moved, assigned);
        // Progress plus the transferred work lands exactly on the build's labor, so RunMetal finishes it this season.
        Assert.Equal(Fix64.FromInt(2), F("1.98") + moved);
    }

    [Fact]
    public void A_stalled_forge_takes_only_what_the_normal_assignment_left_unfinished()
    {
        BronzeFixture f = Fixture("forge", "1.36");
        (Fix64 moved, Fix64 assigned) = f.Economy.FixtureCivicToMetalCompletion(0, buildNeed: Fix64.FromInt(2) - F("1.36"), buildAssigned: F("0.10"),
            civicAssigned: Fix64.One, civicCanals: Fix64.Zero, civicHousing: Fix64.Zero);
        Near(F("0.54"), moved);
        Near(F("0.64"), assigned);
    }

    [Fact]
    public void Nothing_moves_when_materials_civic_surplus_or_the_build_allowance_cannot_cover_completion()
    {
        BronzeFixture f = Fixture("kiln", "1.98");
        LivingEconomyModel e = f.Economy;
        // The existing BuildNeed does not cover the remaining labor.
        Assert.Equal(Fix64.Zero, e.FixtureCivicToMetalCompletion(0, F("0.01"), Fix64.Zero, Fix64.One, Fix64.Zero, Fix64.Zero).Moved);
        // Civic surplus is smaller than the remaining labor, so a partial transfer (which would still stall) is refused.
        Assert.Equal(Fix64.Zero, e.FixtureCivicToMetalCompletion(0, Need, Fix64.Zero, F("0.01"), Fix64.Zero, Fix64.Zero).Moved);
        // Already complete this season: nothing left to move.
        Assert.Equal(Fix64.Zero, e.FixtureCivicToMetalCompletion(0, Need, Need, Fix64.One, Fix64.Zero, Fix64.Zero).Moved);

        // Materials not secured: no clay anywhere and none at the site.
        BronzeFixture unfunded = BronzeFixture.Create(TestPack.Pack, Seed, 60, coppice: false, tin: true, arsenic: true, shaftMining: true, "WAR-05");
        unfunded.Economy.FixtureSetMetalBuild(0, "kiln", F("1.98"), siteClay: 0, siteStone: 2);
        unfunded.Economy.FixtureStock(0, "G-CLAY", -unfunded.Economy.GoodStock(0, "G-CLAY"));
        unfunded.Economy.FixtureSetCivicBuildTarget(0, "temple");
        Assert.Equal(Fix64.Zero, unfunded.Economy.FixtureCivicToMetalCompletion(0, Need, Fix64.Zero, Fix64.One, Fix64.Zero, Fix64.Zero).Moved);
    }

    [Fact]
    public void Housing_canal_market_and_bakery_labor_is_never_diverted()
    {
        BronzeFixture f = Fixture("kiln", "1.98");
        LivingEconomyModel e = f.Economy;
        // 0.50 civic labor, 0.30 housing and 0.19 canals leave 0.01: not enough for the 0.02 needed.
        Assert.Equal(Fix64.Zero, e.FixtureCivicToMetalCompletion(0, Need, Fix64.Zero, F("0.50"), F("0.19"), F("0.30")).Moved);
        // 0.52 leaves exactly 0.02 of discretionary labor.
        Near(F("0.02"), e.FixtureCivicToMetalCompletion(0, Need, Fix64.Zero, F("0.52"), F("0.19"), F("0.30")).Moved);

        foreach (string protectedBuild in new[] { "market", "bakery" })
        {
            BronzeFixture g = Fixture("kiln", "1.98", protectedBuild);
            Assert.Equal(Fix64.Zero, g.Economy.FixtureCivicToMetalCompletion(0, Need, Fix64.Zero, Fix64.One, Fix64.Zero, Fix64.Zero).Moved);
        }
    }

    [Fact]
    public void Releasing_housing_lets_the_final_sliver_come_from_housing_but_never_from_canals_or_a_market()
    {
        BronzeFixture f = Fixture("kiln", "1.98");
        LivingEconomyModel e = f.Economy;
        // All 0.30 of civic labor is housing demand: protected by default.
        Assert.Equal(Fix64.Zero, e.FixtureCivicToMetalCompletion(0, Need, Fix64.Zero, F("0.30"), Fix64.Zero, F("0.30")).Moved);
        e.CivicToMetalReleasesHousing = true;
        (Fix64 moved, Fix64 assigned) = e.FixtureCivicToMetalCompletion(0, Need, Fix64.Zero, F("0.30"), Fix64.Zero, F("0.30"));
        Assert.Equal(Need, moved);
        Assert.Equal(Need, assigned);
        // Canal labor stays protected when only housing is released.
        Assert.Equal(Fix64.Zero, e.FixtureCivicToMetalCompletion(0, Need, Fix64.Zero, F("0.30"), F("0.30"), Fix64.Zero).Moved);
        // The sliver must still fit: a civic assignment smaller than it moves nothing.
        Assert.Equal(Fix64.Zero, e.FixtureCivicToMetalCompletion(0, Need, Fix64.Zero, F("0.01"), Fix64.Zero, F("0.01")).Moved);
        // A Market under way is never touched.
        BronzeFixture market = Fixture("kiln", "1.98", "market");
        market.Economy.CivicToMetalReleasesHousing = true;
        Assert.Equal(Fix64.Zero, market.Economy.FixtureCivicToMetalCompletion(0, Need, Fix64.Zero, F("0.30"), Fix64.Zero, F("0.30")).Moved);
    }

    [Fact]
    public void Only_the_first_forge_or_kiln_qualifies()
    {
        foreach (string kind in new[] { "smelter", "minehead" })
        {
            BronzeFixture f = Fixture(kind, "1.98");
            Assert.Equal(Fix64.Zero, f.Economy.FixtureCivicToMetalCompletion(0, Need, Fix64.Zero, Fix64.One, Fix64.Zero, Fix64.Zero).Moved);
        }
        // A Kiln beside an existing Smelter is not the first casting furnace.
        BronzeFixture withSmelter = Fixture("kiln", "1.98", smelters: 1);
        Assert.Equal(Fix64.Zero, withSmelter.Economy.FixtureCivicToMetalCompletion(0, Need, Fix64.Zero, Fix64.One, Fix64.Zero, Fix64.Zero).Moved);
    }

    [Fact]
    public void A_build_the_normal_assignment_finishes_is_untouched_and_never_repeats_after_completion()
    {
        // This fixture has labor to spare, so the Kiln completes with or without the rule: the rule must then be inert, leaving
        // every figure's work, kit wear and job, the food stores and the facility counts identical, and never fire again once
        // the build is done.
        (Fix64[] Work, Fix64[] KitLeft, EconomyJob[] Jobs, Fix64 Food, string State, Fix64 Moved)[] runs = new
            (Fix64[], Fix64[], EconomyJob[], Fix64, string, Fix64)[2];
        for (int variant = 0; variant < 2; variant++)
        {
            BronzeFixture f = Fixture("kiln", "1.98");
            f.Economy.CivicToMetalCompletionEnabled = variant == 1;
            (_, _, _, int kilnsBefore, _) = f.Economy.FixtureMetalBuildState(0);
            f.RunSeasons(1);
            (_, _, _, int kilnsAfter, _) = f.Economy.FixtureMetalBuildState(0);
            Assert.Equal(kilnsBefore + 1, kilnsAfter);
            Fix64 movedAfterCompletion = f.Economy.CivicToMetalTransferred;
            f.RunSeasons(2);
            (_, _, _, int kilnsLater, _) = f.Economy.FixtureMetalBuildState(0);
            Assert.Equal(kilnsAfter, kilnsLater);
            Assert.Equal(movedAfterCompletion, f.Economy.CivicToMetalTransferred);
            (Fix64[] work, Fix64[] kit, EconomyJob[] jobs) = f.Economy.FixtureFigureWork(0);
            runs[variant] = (work, kit, jobs, f.Economy.FoodStores(0), f.Economy.FixtureMetalBuildState(0).ToString(), f.Economy.CivicToMetalTransferred);
        }
        Assert.Equal(Fix64.Zero, runs[1].Moved);
        Assert.Equal(runs[0].Work, runs[1].Work);
        Assert.Equal(runs[0].KitLeft, runs[1].KitLeft);
        Assert.Equal(runs[0].Jobs, runs[1].Jobs);
        Assert.Equal(runs[0].Food, runs[1].Food);
        Assert.Equal(runs[0].State, runs[1].State);
    }
}
