using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Tests;

public class ResearchCatalogTests
{
    [Fact]
    public void Catalog_compiles_the_pack_with_dense_indices()
    {
        ResearchCatalog catalog = TestPack.Catalog;
        Assert.Equal(519, catalog.Count);
        Assert.Equal(33, catalog.LineageCodes.Count);
        Assert.Equal(214, catalog.Projects.Count(p => p.Numeric)); // 148 (E0-E2, KEY-1..3) + 65 E3 projects + KEY-4
        for (int i = 0; i < catalog.Count; i++) Assert.Equal(i, catalog.Projects[i].Index);
        ProjectDef key1 = catalog.Get("KEY-1");
        Assert.True(key1.IsKeystone);
        Assert.Equal(13, key1.BreadthMin);
        Assert.Equal(1, key1.Enters);
        Assert.Equal(Fix64.FromInt(22), catalog.Get("STN-01").Cost);
        Assert.Equal(Fix64.FromInt(55), key1.Cost);
        Assert.Equal(Fix64.Parse("1.5"), catalog.Get("LRN-01").Effects[0].Value);
    }

    [Fact]
    public void Keystone_closures_cover_their_named_prerequisites_and_all_their_ancestors()
    {
        ResearchCatalog catalog = TestPack.Catalog;
        for (int era = 0; era < 3; era++)
        {
            bool[] closure = catalog.ClosureOf(era)!;
            ProjectDef key = catalog.Projects[catalog.KeystoneLeaving(era)];
            Assert.True(closure[key.Index]);
            foreach (ProjectDef p in catalog.Projects.Where(p => closure[p.Index]))
                foreach (int r in p.Requires) Assert.True(closure[r], $"{p.Id} requires {catalog.Projects[r].Id}, missing from the closure");
        }
        Assert.Equal(12, catalog.ClosureOf(0)!.Count(x => x));
    }

    [Fact]
    public void Insight_stats_roles_and_gates_are_indexed()
    {
        ResearchCatalog catalog = TestPack.Catalog;
        Assert.True(catalog.StatIndex("commons_insight") >= 0);
        Assert.True(catalog.StatIndex("elder_insight") >= 0);
        Assert.True(catalog.GateTargetIndex("Clay") >= 0 && catalog.GateTargetIndex("Native copper") >= 0);
        Assert.Equal(catalog.IndexOf("GOV-01"), catalog.Roles[catalog.RoleIndex("elder")].UnlockedBy);
        Assert.Equal(Fix64.FromInt(120), catalog.Roles[catalog.RoleIndex("researcher")].Insight);
    }
}

public class ResearchInsightTests
{
    private static void Near(double expected, Fix64 actual, double relative = 1e-4)
        => Assert.True(Math.Abs(actual.ToDoubleForDisplay() - expected) <= expected * relative, $"expected {expected}, got {actual.ToDoubleForDisplay()}");

    /// <summary>The live re-fit of the whole formula (docs/MVP/07c §7a).</summary>
    private static double Cal => TestPack.Catalog.Constant("InsightCalibration").ToDoubleForDisplay();

    [Fact]
    public void The_starting_band_makes_about_forty_Insight_a_year_before_calibration()
    {
        var rig = new ResearchRig();
        // 4.8 x (8 adults + 2 elders x 2)^0.85 = 39.7 (docs/MVP/02 §8.1), times the live calibration
        Near(4.8 * Math.Pow(12, 0.85) * Cal, rig.Research.InsightPerYear(0));
        Near(4.8 * Math.Pow(12, 0.85) * Cal, rig.Research.CommonsPerYear(0));
    }

    [Fact]
    public void Learning_multipliers_roles_and_ties_follow_02_section_8_1()
    {
        var rig = new ResearchRig(civs: 2, configure: s => s.Settlements = 3);
        // Civilization 1 completes everything first, so civilization 0 gets no Pioneer bonus on top of the plain numbers.
        foreach (string id in new[] { "SOC-03", "FIR-03", "GOV-01", "BEL-03", "LRN-02" }) rig.Do(rig.Grant(1, id));
        // Before SOC-03 daughter bands share at half strength: ties = (1 + 0.5 x 2) / 3.
        Near(4.8 * Math.Pow(12 * (2.0 / 3.0), 0.85) * Cal, rig.Research.InsightPerYear(0));

        rig.Do(rig.Grant(0, "SOC-03"));
        rig.Run(10);
        Near(4.8 * Math.Pow(12, 0.85) * Cal, rig.Research.InsightPerYear(0));

        rig.Do(rig.Grant(0, "FIR-03")); // Commons +10% at hearth settlements
        rig.Run(10);
        double commons = 4.8 * Math.Pow(12, 0.85) * 1.1 * Cal;
        Near(commons, rig.Research.InsightPerYear(0));

        rig.Do(rig.Grant(0, "GOV-01")); // Elder role: 8 Insight per holder per year
        rig.Run(10);
        Near(commons + 8 * Cal, rig.Research.InsightPerYear(0));

        rig.Do(rig.Grant(0, "BEL-03")); // Shaman: 12
        rig.Run(10);
        Near(commons + 20 * Cal, rig.Research.InsightPerYear(0));

        rig.Do(rig.Grant(0, "LRN-02")); // Elder Insight x1.5, on elders' Commons weight and the Elder role
        rig.Run(10);
        double withOral = (4.8 * Math.Pow(8 + 2 * 1.5 * 2, 0.85) * 1.1 + 8 * 1.5 + 12) * Cal;
        Near(withOral, rig.Research.InsightPerYear(0));
    }

    [Fact]
    public void Keystone_era_boon_adds_five_percent_Insight_or_a_beacon_by_Leaning()
    {
        var kin = new ResearchRig(leaning: (30, 40, 30));
        Fix64 before = kin.Research.InsightPerYear(0);
        GrantEraOne(kin);
        Assert.Equal(1, kin.Research.Era(0));
        Near(before.ToDoubleForDisplay() * 1.05, kin.Research.InsightPerYear(0));
        Assert.Equal(0, kin.Research.BeaconBoons(0));

        var reach = new ResearchRig(leaning: (20, 20, 60));
        Fix64 reachBefore = reach.Research.InsightPerYear(0);
        GrantEraOne(reach);
        Assert.Equal(1, reach.Research.BeaconBoons(0));
        Assert.Equal(reachBefore, reach.Research.InsightPerYear(0));
    }

    internal static void GrantEraOne(ResearchRig rig)
    {
        ResearchCatalog catalog = rig.Catalog;
        var named = new[] { "COM-02", "FIR-02", "STN-04", "SOC-01" };
        rig.GrantAll(0, named);
        foreach (ProjectDef p in catalog.Projects.Where(p => p.Era == 0 && p.Numeric && !p.IsKeystone && !named.Contains(p.Id)).Take(9))
            rig.Do(rig.Grant(0, p.Id));
        rig.Do(rig.Grant(0, "KEY-1"));
        rig.Run(10);
    }

    [Theory]
    [InlineData("ART-02", "knap", "STN", "1.2")]
    [InlineData("ART-03", "hunt", "HNT", "1.1")]
    public void Know_how_effects_scale_the_practice_that_feeds_a_lineage(string project, string practice, string lineage, string factor)
    {
        Action<FixedSource> work = s => { s.Work[practice] = Fix64.FromInt(3); s.PractisingShare = Fix64.One; };
        var plain = new ResearchRig(configure: work);
        var boosted = new ResearchRig(configure: work);
        boosted.Do(boosted.Grant(0, project));
        plain.Run(150);
        boosted.Run(150);
        int l = plain.Catalog.LineageIndex(lineage);
        double a = plain.Research.KnowHow(0, l).ToDoubleForDisplay(), b = boosted.Research.KnowHow(0, l).ToDoubleForDisplay();
        Assert.True(a > 0);
        Assert.InRange(b / a, double.Parse(factor) - 0.01, double.Parse(factor) + 0.01);
    }
}

public class ResearchSlotTests
{
    [Fact]
    public void Insight_splits_four_two_one_across_the_slots()
    {
        var rig = new ResearchRig();
        rig.Run(200, rig.Slot(0, 0, "FOR-01"), rig.Slot(0, 1, "STN-01"), rig.Slot(0, 2, "HNT-01"));
        long p0 = rig.Research.Progress(0, rig.P("FOR-01")).Raw, p1 = rig.Research.Progress(0, rig.P("STN-01")).Raw, p2 = rig.Research.Progress(0, rig.P("HNT-01")).Raw;
        Assert.True(Math.Abs(p0 - 2 * p1) < 200, $"{p0} {p1}");
        Assert.True(Math.Abs(p1 - 2 * p2) < 200, $"{p1} {p2}");
        // 20 steps of Insight/60 each, all of it spent.
        double expected = rig.Research.InsightPerYear(0).ToDoubleForDisplay() / 60 * 20;
        double spent = (p0 + p1 + p2) / 4294967296.0;
        Assert.InRange(spent, expected - 0.001, expected + 0.001);
    }

    [Fact]
    public void An_empty_slots_share_goes_to_the_others()
    {
        var two = new ResearchRig();
        two.Run(200, two.Slot(0, 0, "FOR-01"), two.Slot(0, 1, "STN-01"));
        long a = two.Research.Progress(0, two.P("FOR-01")).Raw, b = two.Research.Progress(0, two.P("STN-01")).Raw;
        Assert.True(Math.Abs(a - 2 * b) < 200); // 4 : 2 of 6
        double perStep = two.Research.InsightPerYear(0).ToDoubleForDisplay() / 60;
        Assert.InRange(a / 4294967296.0, perStep * 20 * 4 / 6 - 0.001, perStep * 20 * 4 / 6 + 0.001);

        var one = new ResearchRig();
        one.Run(200, one.Slot(0, 2, "FOR-01")); // even the tertiary slot gets everything when alone
        Assert.InRange(one.Research.Progress(0, one.P("FOR-01")).ToDoubleForDisplay(), perStep * 20 - 0.001, perStep * 20 + 0.001);
    }

    [Fact]
    public void A_completion_cascades_the_others_up_and_its_overflow_follows_the_freed_slot()
    {
        var rig = new ResearchRig(insightPerYear: 2400); // 40 Insight per step; Primary gets 4/7 = 22.86
        rig.Run(10, rig.Slot(0, 0, "FOR-01"), rig.Slot(0, 1, "STN-01"), rig.Slot(0, 2, "HNT-01"),
            new Command(0, 0, CommandKind.QueueProject, rig.P("WAY-01"), 0, 0));
        Assert.True(rig.Research.IsDone(0, rig.P("FOR-01")));
        // Secondary and Tertiary slide up (docs/MVP/08 §7) and the queue head drops into Tertiary.
        Assert.Equal(rig.P("STN-01"), rig.Research.Slot(0, 0));
        Assert.Equal(rig.P("HNT-01"), rig.Research.Slot(0, 1));
        Assert.Equal(rig.P("WAY-01"), rig.Research.Slot(0, 2));
        // The 0.857 left over from FOR-01 goes to the project that took over Primary, on top of its own 11.43.
        double expected = 40.0 * 2 / 7 + (40.0 * 4 / 7 - 22);
        Assert.InRange(rig.Research.Progress(0, rig.P("STN-01")).ToDoubleForDisplay(), expected - 0.01, expected + 0.01);
        Assert.Equal(Fix64.Zero, rig.Research.Progress(0, rig.P("WAY-01")));
    }

    [Fact]
    public void Progress_is_never_lost_when_a_project_leaves_its_slot()
    {
        var rig = new ResearchRig();
        rig.Run(100, rig.Slot(0, 0, "FOR-01"));
        Fix64 progress = rig.Research.Progress(0, rig.P("FOR-01"));
        Assert.True(progress > Fix64.Zero);
        rig.Do(rig.Slot(0, 0, ""));
        rig.Run(100);
        Assert.Equal(progress, rig.Research.Progress(0, rig.P("FOR-01")));
        Assert.Equal(ProjectState.Available, rig.Research.State(0, rig.P("FOR-01")));
        rig.Do(rig.Slot(0, 1, "FOR-01"));
        rig.Run(100);
        Assert.True(rig.Research.Progress(0, rig.P("FOR-01")) > progress);
    }

    [Fact]
    public void Know_how_drains_into_a_slotted_project_of_its_lineage_up_to_half_its_cost()
    {
        var rig = new ResearchRig(configure: s => { s.Work["knap"] = Fix64.FromInt(3); s.PractisingShare = Fix64.One; });
        rig.Run(1500); // ten seasons of knapping with nothing slotted: the pool grows past half of STN-01's cost
        int stn = rig.Catalog.LineageIndex("STN");
        Fix64 pool = rig.Research.KnowHow(0, stn);
        Assert.True(pool > Fix64.FromInt(11), pool.ToString());
        rig.Run(1, rig.Slot(0, 0, "STN-01"));
        double perStep = rig.Research.InsightPerYear(0).ToDoubleForDisplay() / 60;
        rig.Run(9);
        // One step: Know-how adds at most the slot's own Insight rate.
        Assert.InRange(rig.Research.KnowHowApplied(0, rig.P("STN-01")).ToDoubleForDisplay(), 0.1, perStep + 0.001);
        rig.Run(300);
        Assert.True(rig.Research.IsDone(0, rig.P("STN-01")));
        // Know-how covered half of the cost and no more; the rest was Insight.
        Assert.InRange(rig.Research.KnowHowApplied(0, rig.P("STN-01")).ToDoubleForDisplay(), 10.5, 11.001);
        Assert.True(rig.Research.KnowHow(0, stn) > Fix64.Zero);
    }

    [Fact]
    public void Know_how_flows_to_lineages_by_the_practice_registry_split()
    {
        var rig = new ResearchRig(configure: s => { s.Work["chop"] = Fix64.FromInt(2); s.PractisingShare = Fix64.One; });
        rig.Run(150);
        int stn = rig.Catalog.LineageIndex("STN"), wod = rig.Catalog.LineageIndex("WOD");
        double s = rig.Research.KnowHow(0, stn).ToDoubleForDisplay(), w = rig.Research.KnowHow(0, wod).ToDoubleForDisplay();
        Assert.InRange(w / s, 70.0 / 30 - 0.01, 70.0 / 30 + 0.01); // chop: WOD 70 / STN 30 before era 2
    }
}

public class ResearchStateTests
{
    [Fact]
    public void Seven_starter_projects_are_available_and_their_neighbours_glimpsed()
    {
        var rig = new ResearchRig();
        var available = rig.Catalog.Projects.Where(p => rig.Research.State(0, p.Index) == ProjectState.Available).Select(p => p.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "COM-01", "FIR-01", "FOR-01", "HNT-01", "SOC-01", "STN-01", "WAY-01" }, available);
        Assert.Equal(ProjectState.Glimpsed, rig.Research.State(0, rig.P("STN-02")));
        Assert.Equal(ProjectState.Hidden, rig.Research.State(0, rig.P("FOR-02")));  // two prerequisites missing
        Assert.Equal(ProjectState.Glimpsed, rig.Research.State(0, rig.P("KEY-1"))); // a keystone is always on the horizon of its era
        Assert.Equal(ProjectState.Hidden, rig.Research.State(0, rig.P("STN-05")));  // a future era
        Assert.All(rig.Research.Events.Where(e => e.Kind == ResearchEventKind.Unlocked), e => Assert.Equal(0, e.Tick));
    }

    [Fact]
    public void States_follow_slotting_completion_and_prerequisites()
    {
        var rig = new ResearchRig();
        rig.Do(rig.Slot(0, 0, "FOR-01"));
        Assert.Equal(ProjectState.Slotted, rig.Research.State(0, rig.P("FOR-01")));
        rig.Do(rig.Grant(0, "FOR-01"));
        Assert.Equal(ProjectState.Completed, rig.Research.State(0, rig.P("FOR-01")));
        Assert.Equal(-1, rig.Research.Slot(0, 0));
        Assert.Equal(ProjectState.Glimpsed, rig.Research.State(0, rig.P("FOR-02"))); // WOD-01 still missing
        rig.Do(rig.Grant(0, "WOD-01"));
        Assert.Equal(ProjectState.Available, rig.Research.State(0, rig.P("FOR-02")));
        Assert.Contains(rig.Events(ResearchEventKind.Glimpsed, "FOR-02"), e => e.Civ == 0);
        Assert.Contains(rig.Events(ResearchEventKind.Unlocked, "FOR-02"), e => e.Civ == 0);
    }

    [Fact]
    public void A_future_era_stays_hidden_until_its_keystone_and_a_keystone_needs_era_breadth()
    {
        var rig = new ResearchRig();
        rig.Do(rig.Grant(0, "STN-04"));
        Assert.Equal(ProjectState.Hidden, rig.Research.State(0, rig.P("STN-05"))); // prerequisites met, era not reached
        rig.GrantAll(0, "COM-02", "FIR-02", "SOC-01"); // the four named prerequisites
        Assert.Equal(4, rig.Research.DoneInEra(0, 0));
        Assert.Equal(ProjectState.Glimpsed, rig.Research.State(0, rig.P("KEY-1"))); // 13 era projects are needed too
        foreach (ProjectDef p in rig.Catalog.Projects.Where(p => p.Era == 0 && p.Numeric && !p.IsKeystone && !rig.Research.IsDone(0, p.Index)).Take(8))
            rig.Do(rig.Grant(0, p.Id));
        Assert.Equal(12, rig.Research.DoneInEra(0, 0));
        Assert.Equal(ProjectState.Glimpsed, rig.Research.State(0, rig.P("KEY-1")));
        rig.Do(rig.Grant(0, rig.Catalog.Projects.First(p => p.Era == 0 && p.Numeric && !p.IsKeystone && !rig.Research.IsDone(0, p.Index)).Id));
        Assert.Equal(ProjectState.Available, rig.Research.State(0, rig.P("KEY-1")));
        rig.Do(rig.Grant(0, "KEY-1"));
        Assert.Equal(1, rig.Research.Era(0));
        Assert.Equal(ProjectState.Available, rig.Research.State(0, rig.P("STN-05")));
        Assert.Single(rig.Events(ResearchEventKind.EraAdvanced, "KEY-1"));
    }

    [Fact]
    public void A_Seen_gate_hides_the_project_until_the_resource_is_seen()
    {
        var rig = new ResearchRig();
        rig.GrantAll(0, "STN-08"); // MET-01 needs STN-08 and a sight of native copper
        ResearchSystemTestsHelper.ReachEra(rig, 2);
        Assert.Equal(ProjectState.Hidden, rig.Research.State(0, rig.P("MET-01")));
        rig.Source.Seen |= 1UL << rig.Catalog.GateTargetIndex("Native copper");
        rig.Run(150);
        Assert.Equal(ProjectState.Available, rig.Research.State(0, rig.P("MET-01")));
        Assert.Single(rig.Events(ResearchEventKind.SeenResource));
    }

    [Fact]
    public void Draft_projects_are_never_researchable()
    {
        var rig = new ResearchRig();
        ResearchSystemTestsHelper.ReachEra(rig, 3);
        foreach (ProjectDef p in rig.Catalog.Projects.Where(p => !p.Numeric))
            Assert.Equal(ProjectState.Hidden, rig.Research.State(0, p.Index));
    }
}

internal static class ResearchSystemTestsHelper
{
    /// <summary>Grants every numeric project of the eras before <paramref name="era"/> so the civilization stands in it.</summary>
    public static void ReachEra(ResearchRig rig, int era)
    {
        foreach (ProjectDef p in rig.Catalog.Projects.Where(p => p.Numeric && p.Era < era && !rig.Research.IsDone(0, p.Index)))
            rig.Do(rig.Grant(0, p.Id));
        rig.Run(10);
        Assert.Equal(era, rig.Research.Era(0));
    }
}
