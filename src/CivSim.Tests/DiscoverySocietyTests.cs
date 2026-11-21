using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Tests;

/// <summary>LRN-09 learned societies (M13 WP-E6-7 slice 2, docs/MVP/38 §5.3). Discovery chapter only; the society works only with a scholar in it.</summary>
public class DiscoverySocietyTests
{
    private const ulong Seed = 20261019;
    private const string Society = "learned_society";
    private static readonly string[] Kit = { "KEY-5", "LRN-05", "LRN-09" };

    private static BronzeFixture Town(params string[] projects)
    {
        BronzeFixture f = BronzeFixture.CreateDiscoveryWithCoal(TestPack.Pack, Seed, 150, projects.Length == 0 ? Kit : projects);
        f.Economy.FixtureSetTier(0, 1);
        return f;
    }

    private static ResearchRig Rig(bool society, ChapterRules rules, params string[] granted)
    {
        var catalog = new ResearchCatalog(TestPack.WithNumeric("KEY-5", "KEY-6", "LRN-09", "COM-10", "MEC-05"));
        var rig = new ResearchRig(civs: 2, catalog: catalog, rules: rules, insightPerYear: 4000);
        rig.Source.Society = society;
        rig.GrantAll(0, granted);
        rig.Do(rig.Grant(1, "MEC-05"));
        rig.Research.RecordContact(0, 1);
        return rig;
    }

    [Fact]
    public void The_society_gate_needs_LRN09_is_wanted_where_scholars_gather_and_is_one_to_a_town_in_Discovery()
    {
        BronzeFixture f = Town();
        Assert.True(f.Economy.FixtureIronGateOpen(0, Society, includeAppetite: false));
        Assert.False(f.Economy.FixtureIronGateOpen(0, Society, includeAppetite: true), "no academy or university in the town");
        f.Economy.FixtureSetIronWork(0, "academy", 1);
        Assert.True(f.Economy.FixtureIronGateOpen(0, Society, includeAppetite: true));
        f.Economy.FixtureSetIronWork(0, Society, 1);
        Assert.False(f.Economy.FixtureIronGateOpen(0, Society, includeAppetite: false), "one society to a town");

        Assert.False(Town("KEY-5", "LRN-05").Economy.FixtureIronGateOpen(0, Society, includeAppetite: false), "no project");
        BronzeFixture fief = BronzeFixture.CreateFiefWithCoalDeposit(TestPack.Pack, Seed, 150, Kit);
        fief.Economy.FixtureSetTier(0, 1);
        Assert.False(fief.Economy.FixtureIronGateOpen(0, Society, includeAppetite: false), "no society outside Discovery");
    }

    [Fact]
    public void A_society_seats_scholars_and_works_only_while_one_sits_in_it()
    {
        BronzeFixture f = Town();
        f.Economy.FixtureSetIronWork(0, Society, 1);
        f.Economy.FixtureClearRoles(0);
        Assert.False(f.Economy.FixtureSocietyStaffed(0), "an empty society has no effect");
        f.RunSeasons(1);
        Assert.True(f.Economy.FixtureRoleCount(0, "scholar") >= 1, "the society seats scholars");
        Assert.True(f.Economy.FixtureSocietyStaffed(0));
        f.Economy.FixtureClearRoles(0);
        Assert.False(f.Economy.FixtureSocietyStaffed(0));
    }

    [Fact]
    public void LRN09_diffusion_bonus_applies_only_with_a_staffed_society_and_only_in_Discovery()
    {
        ResearchRig staffed = Rig(true, ChapterRules.Discovery, "KEY-5", "KEY-6", "COM-10", "LRN-09");
        ResearchRig empty = Rig(false, ChapterRules.Discovery, "KEY-5", "KEY-6", "COM-10", "LRN-09");
        ResearchRig without = Rig(true, ChapterRules.Discovery, "KEY-5", "KEY-6", "COM-10");
        int p = staffed.P("MEC-05");
        Fix64 withSociety = staffed.Research.EffectiveCost(0, p), noSociety = empty.Research.EffectiveCost(0, p), neither = without.Research.EffectiveCost(0, p);
        Assert.True(withSociety < noSociety, "a staffed society deepens the discount from a knowing neighbor");
        Assert.True(Fix64.Abs(noSociety - neither) < Fix64.FromRatio(1, 1000), "an empty society's bonus is taken back out (to within fixed-point rounding)");
    }

    [Fact]
    public void A_staffed_society_lets_a_tenth_of_other_lineages_Know_how_count_toward_a_project()
    {
        ResearchCatalog catalog = new(TestPack.WithNumeric("KEY-5", "KEY-6", "LRN-09", "COM-10", "MEC-05"));
        string target = catalog.Projects.First(d => d.Era <= 2 && d.Lineage >= 0 && d.Requires.Length == 0 && d.RequiresAny.Length == 0).Id;
        int targetLineage = catalog.Projects[catalog.IndexOf(target)].Lineage;
        int donor = Enumerable.Range(0, catalog.LineageCodes.Count).First(l => l != targetLineage);
        (Fix64 Applied, Fix64 Donor) Run(bool society)
        {
            ResearchRig rig = Rig(society, ChapterRules.Discovery, "KEY-5", "KEY-6", "COM-10", "LRN-09");
            rig.Research.FixtureAddKnowHow(0, donor, Fix64.FromInt(1000));
            rig.Do(rig.Slot(0, 0, target));
            rig.Run(ResearchSystem.SeasonTicks * 2);
            return (rig.Research.KnowHowApplied(0, rig.P(target)), rig.Research.KnowHow(0, donor));
        }
        (Fix64 staffedApplied, Fix64 staffedDonor) = Run(true);
        (Fix64 emptyApplied, Fix64 emptyDonor) = Run(false);
        Assert.True(staffedApplied > emptyApplied, "spillover adds Know-how to the project");
        Assert.True(staffedDonor < emptyDonor, "and the donor lineage pays for it");
        Assert.True(staffedApplied - emptyApplied <= (Fix64.FromInt(1000) - staffedDonor) / 10 + Fix64.FromRatio(1, 100), "the donor pays ten for each one given");
    }
}
