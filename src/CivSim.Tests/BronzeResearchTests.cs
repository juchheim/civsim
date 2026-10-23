using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivSim.Sim.Ai.Forecast;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.View;

namespace CivSim.Tests;

/// <summary>
/// Work package WP5 of the Bronze milestone (docs/MVP/22 §3, §4.2, §20): E3 research and the Iron threshold, Legacy units per
/// era, Mandate and Admin state, and the chapter end rule.
/// </summary>
public class BronzeLegacyTests
{
    private static ProjectDef First(ResearchCatalog c, int era, Func<ProjectDef, bool> where)
        => c.Projects.First(p => p.Numeric && p.Era == era && where(p));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Mastery_and_Firsts_scale_with_the_era_not_the_cost(int era)
    {
        var rig = new ResearchRig(civs: 2);
        ProjectDef plain = First(rig.Catalog, era, p => !p.IsKeystone && p.Type != ProjectType.Wonder);
        rig.Do(rig.Grant(0, plain.Id));
        rig.Do(rig.Grant(1, plain.Id));
        Assert.Equal(Fix64.FromInt(2 * (era + 1)), rig.Research.MasteryLegacy(0));
        Assert.Equal(Fix64.FromInt(2 * (era + 1)), rig.Research.MasteryLegacy(1));
        Assert.Equal(Fix64.FromInt(3 * (era + 1)), rig.Research.FirstsLegacy(0));
        Assert.Equal(Fix64.Zero, rig.Research.FirstsLegacy(1)); // second to finish: Mastery only
        Assert.Equal(Fix64.FromInt(2 * (era + 1)) + Fix64.FromInt(3 * (era + 1)), rig.Research.Legacy(0));
    }

    [Theory]
    [InlineData(0, "KEY-1")]
    [InlineData(1, "KEY-2")]
    [InlineData(2, "KEY-3")]
    [InlineData(3, "KEY-4")]
    public void A_Keystone_First_is_four_times_a_First(int era, string key)
    {
        var rig = new ResearchRig();
        rig.Do(rig.Grant(0, key));
        Assert.Equal(Fix64.FromInt(12 * (era + 1)), rig.Research.FirstsLegacy(0)); // 12, 24, 36, 48
        Assert.Equal(Fix64.FromInt(2 * (era + 1)), rig.Research.MasteryLegacy(0));
    }

    [Fact]
    public void The_Wonder_First_is_four_times_a_First_and_a_built_Wonder_adds_25_per_era()
    {
        var rig = new ResearchRig(civs: 2);
        ProjectDef ziggurat = rig.Catalog.Get("BLD-08");
        Assert.Equal(ProjectType.Wonder, ziggurat.Type);
        Assert.False(rig.Research.AwardWonderBuilt(0, ziggurat.Index)); // not researched yet
        rig.Do(rig.Grant(0, "BLD-08"));
        Assert.Equal(Fix64.FromInt(48), rig.Research.FirstsLegacy(0));
        Assert.Equal(Fix64.Zero, rig.Research.WondersLegacy(0));
        Assert.True(rig.Research.AwardWonderBuilt(0, ziggurat.Index));
        Assert.False(rig.Research.AwardWonderBuilt(0, ziggurat.Index)); // once per civilization
        Assert.Equal(Fix64.FromInt(100), rig.Research.WondersLegacy(0));
        Assert.Equal(Fix64.FromInt(8 + 48 + 100), rig.Research.Legacy(0));
        Assert.True(rig.Research.WonderBuilt(0, ziggurat.Index));
        Assert.False(rig.Research.WonderBuilt(1, ziggurat.Index));
        // Only Wonders can be built.
        rig.Do(rig.Grant(0, "STN-11"));
        Assert.False(rig.Research.AwardWonderBuilt(0, rig.P("STN-11")));
    }

    [Fact]
    public void The_Wonder_of_the_second_era_is_worth_75_built()
    {
        var rig = new ResearchRig();
        ProjectDef megalith = rig.Catalog.Projects.First(p => p.Numeric && p.Type == ProjectType.Wonder && p.Era == 2);
        rig.Do(rig.Grant(0, megalith.Id));
        Assert.Equal(Fix64.FromInt(36), rig.Research.FirstsLegacy(0));
        Assert.True(rig.Research.AwardWonderBuilt(0, megalith.Index));
        Assert.Equal(Fix64.FromInt(75), rig.Research.WondersLegacy(0));
    }

    [Fact]
    public void The_forecast_the_AI_plans_with_uses_the_same_units()
    {
        ResearchCatalog catalog = TestPack.Catalog;
        var fc = new ForecastCatalog(catalog);
        foreach (ProjectDef p in catalog.Projects.Where(p => p.Numeric))
        {
            Assert.Equal(catalog.MasteryUnits(p).FloorToInt(), fc.Mastery[p.Index]);
            Assert.Equal(catalog.FirstUnits(p), fc.FirstPoints[p.Index]);
        }
        Assert.Equal(Fix64.FromInt(48), fc.FirstPoints[catalog.IndexOf("KEY-4")]);
        Assert.Equal(8, fc.Mastery[catalog.IndexOf("MET-03")]);
    }

    [Fact]
    public void The_units_come_from_the_pack_and_the_old_ones_are_gone()
    {
        ResearchCatalog c = TestPack.Catalog;
        Assert.Equal((2, 3, 4, 25), (c.LegacyMasteryPerEra, c.LegacyFirstPerEra, c.LegacyFirstKeystoneMult, c.LegacyWonderBuiltPerEra));
        Assert.Equal((12, 64, 4), (c.ChapterCountdownSeasons, c.ChapterCapYear, c.MandateRechargeMin));
        // A First no longer grows with EraBase: E3's EraBase is 150, its First 12 for a regular project.
        Assert.Equal(Fix64.FromInt(12), c.FirstUnits(c.Get("MET-03")));
    }
}

public class BronzeMandateTests
{
    private static ResearchRig Rig(int civs = 1) => new(civs);
    private const int Season = ResearchSystem.SeasonTicks;

    [Fact]
    public void There_is_no_Mandate_before_KEY3()
    {
        ResearchRig rig = Rig();
        rig.Run(20 * Season);
        Assert.False(rig.Research.MandateUnlocked(0));
        Assert.Equal((0, 0, 0), (rig.Research.Mandate(0), rig.Research.MandateCap(0), rig.Research.MandateSeasonsToNext(0)));
        Assert.Equal(0, rig.Research.AdminSpan(0));
    }

    [Fact]
    public void KEY3_grants_start_1_cap_2_recharge_8_and_Admin_span_4()
    {
        ResearchRig rig = Rig();
        rig.Do(rig.Grant(0, "KEY-3"));
        ResearchSystem r = rig.Research;
        Assert.True(r.MandateUnlocked(0));
        Assert.Equal((1, 2, 8), (r.Mandate(0), r.MandateCap(0), r.MandateRechargeSeasons(0)));
        Assert.Equal(4, r.AdminSpan(0));
        Assert.Equal(Fix64.Zero, r.AdminReduction(0));
        Assert.Equal(8, r.MandateSeasonsToNext(0));
    }

    [Fact]
    public void Mandate_recharges_one_point_every_R_seasons_and_a_full_Mandate_does_not_accumulate()
    {
        ResearchRig rig = Rig();
        rig.Do(rig.Grant(0, "KEY-3"));
        ResearchSystem r = rig.Research;
        // The grant lands during the first season; seasons end at tick 149, 299, ... so 8 season ends make the next point.
        rig.Run(7 * Season);
        Assert.Equal(1, r.Mandate(0));
        Assert.Equal(1, r.MandateSeasonsToNext(0));
        rig.Run(Season);
        Assert.Equal(2, r.Mandate(0)); // cap 2: full
        Assert.Equal(0, r.MandateSeasonsToNext(0));
        rig.Run(20 * Season);
        Assert.Equal(2, r.Mandate(0));
        Assert.Equal(1, r.MandateEarned(0));
        // Spending one point starts a fresh count: the 20 idle seasons banked nothing.
        Assert.True(r.TrySpendMandate(0, 1));
        Assert.Equal(1, r.Mandate(0));
        Assert.Equal(8, r.MandateSeasonsToNext(0));
        rig.Run(7 * Season);
        Assert.Equal(1, r.Mandate(0));
        rig.Run(Season);
        Assert.Equal(2, r.Mandate(0));
        Assert.Equal(1, r.MandateSpent(0));
    }

    [Fact]
    public void Spending_more_than_is_held_changes_nothing()
    {
        ResearchRig rig = Rig();
        rig.Do(rig.Grant(0, "KEY-3"));
        Assert.False(rig.Research.TrySpendMandate(0, 2));
        Assert.False(rig.Research.TrySpendMandate(0, -1));
        Assert.Equal(1, rig.Research.Mandate(0));
        Assert.True(rig.Research.TrySpendMandate(0, 0));
        Assert.Equal(1, rig.Research.Mandate(0));
        Assert.Equal(0, rig.Research.MandateSpent(0));
    }

    [Fact]
    public void Kingship_and_the_rule_of_law_raise_the_cap_to_4()
    {
        ResearchRig rig = Rig();
        rig.GrantAll(0, "KEY-3", "GOV-05");
        Assert.Equal(3, rig.Research.MandateCap(0));
        Assert.Equal(1, rig.Research.Mandate(0)); // a higher cap grants nothing by itself
        rig.GrantAll(0, "GOV-06");
        Assert.Equal(4, rig.Research.MandateCap(0));
        Assert.Equal(1, rig.Research.Mandate(0));
    }

    [Fact]
    public void Writing_shortens_the_recharge_down_to_the_floor_of_4()
    {
        ResearchRig rig = Rig();
        rig.GrantAll(0, "KEY-3");
        Assert.Equal(8, rig.Research.MandateRechargeSeasons(0));
        rig.GrantAll(0, "COM-05");
        Assert.Equal(6, rig.Research.MandateRechargeSeasons(0));
        rig.GrantAll(0, "COM-06");
        Assert.Equal(5, rig.Research.MandateRechargeSeasons(0));
        rig.GrantAll(0, "BEL-07");
        Assert.Equal(4, rig.Research.MandateRechargeSeasons(0)); // 8 - 2 - 1 - 1, the floor (23 §20.7)
        // Every `mandate` effect in the pack, summed, can not go under the floor either.
        ResearchCatalog c = rig.Catalog;
        int sum = c.Projects.Where(p => p.Numeric).SelectMany(p => p.Effects).Where(e => e.Kind == EffectKind.Mandate).Sum(e => e.MandateRecharge);
        Assert.True(sum >= c.MandateRechargeMin);
        // The recharge takes the new period at once: 4 seasons to a point.
        rig.GrantAll(0, "GOV-05", "GOV-06");
        Assert.True(rig.Research.TrySpendMandate(0, 1));
        rig.Run(4 * Season);
        Assert.Equal(1, rig.Research.Mandate(0));
    }

    [Fact]
    public void Decree_costs_follow_the_constants_and_DIP04_makes_a_Compact_free()
    {
        ResearchRig rig = Rig();
        ResearchSystem r = rig.Research;
        Assert.Equal(2, r.DecreeCost(0, "found"));
        foreach (string d in new[] { "expedition", "mine", "levy", "corvee", "compact" }) Assert.Equal(1, r.DecreeCost(0, d));
        rig.GrantAll(0, "DIP-04");
        Assert.Equal(0, r.DecreeCost(0, "compact"));
        Assert.Equal(2, r.DecreeCost(0, "found"));
        Assert.Throws<ArgumentException>(() => r.DecreeCost(0, "tax"));
    }

    [Fact]
    public void Admin_span_and_Overreach_reduction_are_summed_and_combined_as_in_23_20_7()
    {
        ResearchRig rig = Rig();
        rig.GrantAll(0, "KEY-3", "GOV-04", "COM-05", "GOV-06", "TRN-05");
        ResearchSystem r = rig.Research;
        Assert.Equal(4 + 2 + 2 + 2, r.AdminSpan(0));
        // 1 - 0.9 x 0.8 x 0.85 x 0.9 = 0.449 (GOV-04 0.10, COM-05 0.20, GOV-06 0.15, TRN-05 0.10)
        Assert.InRange(r.AdminReduction(0).ToDoubleForDisplay(), 0.448, 0.450);
        // Every project: 15 span in total, 0.711 reduction (the nine `admin` effects).
        ResearchRig all = Rig();
        foreach (ProjectDef p in all.Catalog.Projects.Where(p => p.Numeric && p.Effects.Any(e => e.Kind == EffectKind.Admin))) all.Do(all.Grant(0, p.Id));
        Assert.Equal(15, all.Research.AdminSpan(0));
        Assert.InRange(all.Research.AdminReduction(0).ToDoubleForDisplay(), 0.710, 0.712);
    }

    [Fact]
    public void Mandate_state_is_checksummed_only_once_it_exists_and_survives_a_snapshot()
    {
        ResearchRig early = Rig();
        ResearchRig plain = Rig();
        early.Run(3 * Season);
        plain.Run(3 * Season);
        Assert.Equal(Hash(early), Hash(plain)); // no Mandate yet: nothing in the checksum differs

        ResearchRig rig = Rig();
        rig.GrantAll(0, "KEY-3", "GOV-05", "COM-05");
        rig.Run(5 * Season);
        Assert.True(rig.Research.TrySpendMandate(0, 1));
        rig.Run(2 * Season);
        ulong before = Hash(rig);
        Assert.NotEqual(Hash(Rig()), before);

        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) rig.Research.WriteSnapshot(w);
        stream.Position = 0;
        ResearchRig copy = Rig();
        using (var rd = new BinaryReader(stream)) copy.Research.ReadSnapshot(rd);
        Assert.Equal(before, Hash(copy));
        ResearchSystem a = rig.Research, b = copy.Research;
        Assert.Equal((a.Mandate(0), a.MandateCap(0), a.MandateRechargeSeasons(0), a.MandateSeasonsToNext(0), a.MandateEarned(0), a.MandateSpent(0), a.AdminSpan(0)),
            (b.Mandate(0), b.MandateCap(0), b.MandateRechargeSeasons(0), b.MandateSeasonsToNext(0), b.MandateEarned(0), b.MandateSpent(0), b.AdminSpan(0)));
        // And they go on alike.
        copy.Tick = rig.Tick;
        rig.Run(10 * Season);
        copy.Run(10 * Season);
        Assert.Equal(Hash(rig), Hash(copy));
        Assert.Equal(a.Mandate(0), b.Mandate(0));
    }

    [Fact]
    public void Built_Wonders_survive_a_snapshot_and_the_checksum()
    {
        var rig = new ResearchRig();
        ulong before = Hash(rig);
        rig.Do(rig.Grant(0, "BLD-08"));
        ulong granted = Hash(rig);
        rig.Research.AwardWonderBuilt(0, rig.P("BLD-08"));
        Assert.NotEqual(granted, Hash(rig));
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) rig.Research.WriteSnapshot(w);
        stream.Position = 0;
        var copy = new ResearchRig();
        using (var rd = new BinaryReader(stream)) copy.Research.ReadSnapshot(rd);
        Assert.Equal(Fix64.FromInt(100), copy.Research.WondersLegacy(0));
        Assert.True(copy.Research.WonderBuilt(0, rig.P("BLD-08")));
        Assert.False(copy.Research.AwardWonderBuilt(0, rig.P("BLD-08")));
        Assert.Equal(Hash(rig), Hash(copy));
        Assert.NotEqual(before, Hash(rig));
    }

    private static ulong Hash(ResearchRig rig)
    {
        var h = new StateHasher();
        rig.Research.Hash(ref h);
        return h.Finish();
    }

    [Fact]
    public void The_frame_shows_the_viewers_Mandate_and_hides_a_rivals()
    {
        MatchModel match = MatchModel.Create(20261019, TestPack.Pack);
        var runner = new SimRunner(match);
        runner.Submit(new Command(0, 0, CommandKind.CompleteProject, match.Research.Catalog.IndexOf("KEY-3"), 0, 0));
        runner.Submit(new Command(0, 1, CommandKind.CompleteProject, match.Research.Catalog.IndexOf("KEY-3"), 0, 0));
        runner.Advance(2 * ResearchSystem.SeasonTicks);
        var frame = new MatchFrame();
        new FrameBuilder(match, 0).Build(match, frame);
        Assert.Equal((1, 2), (frame.CivMandate[0], frame.CivMandateCap[0]));
        Assert.True(frame.CivMandateNext[0] is > 0 and <= 8);
        Assert.Equal((0, 0, 0), (frame.CivMandate[1], frame.CivMandateCap[1], frame.CivMandateNext[1]));
        var omniscient = new MatchFrame();
        new FrameBuilder(match, -1).Build(match, omniscient);
        Assert.Equal(1, omniscient.CivMandate[0]);
    }
}

public class BronzeEraTests
{
    private const int Season = ResearchSystem.SeasonTicks;

    [Fact]
    public void E3_projects_are_hidden_until_KEY3_then_appear_as_Glimpse_and_Available()
    {
        var rig = new ResearchRig();
        int met03 = rig.P("MET-03");
        ResearchSystemTestsHelper.ReachEra(rig, 2);
        Assert.Equal(ProjectState.Hidden, rig.Research.State(0, met03)); // E3 is not this era yet
        foreach (ProjectDef p in rig.Catalog.Projects.Where(p => p.Numeric && p.Era == 2 && !p.IsKeystone && !rig.Research.IsDone(0, p.Index)))
            rig.Do(rig.Grant(0, p.Id));
        Assert.Equal(2, rig.Research.Era(0));
        Assert.Equal(ProjectState.Hidden, rig.Research.State(0, met03)); // still Hidden with every E2 project done: only KEY-3 opens the age
        rig.Do(rig.Grant(0, "KEY-3"));
        rig.Run(10);
        Assert.Equal(3, rig.Research.Era(0));
        Assert.Equal(ProjectState.Available, rig.Research.State(0, met03)); // MET-02 and CER-03 are done
        // One prerequisite away is a Glimpse; two or more stay Hidden.
        Assert.Equal(ProjectState.Glimpsed, rig.Research.State(0, rig.P("MET-04"))); // needs MET-03 only ([Has] is for use, not research)
        Assert.Equal(ProjectState.Hidden, rig.Research.State(0, rig.P("MET-06")));
    }

    [Fact]
    public void KEY4_needs_breadth_21_over_E3_and_its_four_prerequisites()
    {
        var rig = new ResearchRig();
        ResearchSystemTestsHelper.ReachEra(rig, 3);
        ProjectDef key4 = rig.Catalog.Get("KEY-4");
        Assert.Equal((3, 21), (key4.BreadthEra, key4.BreadthMin));
        Assert.Equal(ProjectState.Glimpsed, rig.Research.State(0, key4.Index)); // a keystone is always on the horizon
        rig.GrantAll(0, "MET-08", "COM-05", "GOV-06", "TRD-05");
        Assert.Equal(ProjectState.Glimpsed, rig.Research.State(0, key4.Index)); // prerequisites are not breadth
        var others = rig.Catalog.Projects.Where(p => p.Numeric && p.Era == 3 && !p.IsKeystone && !rig.Research.IsDone(0, p.Index)).ToList();
        for (int i = 0; i < 16; i++) rig.Do(rig.Grant(0, others[i].Id));
        Assert.Equal(20, rig.Research.DoneInEra(0, 3));
        Assert.Equal(ProjectState.Glimpsed, rig.Research.State(0, key4.Index));
        rig.Do(rig.Grant(0, others[16].Id));
        Assert.Equal(21, rig.Research.DoneInEra(0, 3));
        Assert.Equal(ProjectState.Available, rig.Research.State(0, key4.Index));
    }

    [Fact]
    public void Completing_KEY4_enters_the_Iron_threshold_and_E4_stays_hidden()
    {
        var rig = new ResearchRig(civs: 1, insightPerYear: 30000);
        ResearchSystemTestsHelper.ReachEra(rig, 3);
        rig.GrantAll(0, "MET-08", "COM-05", "GOV-06", "TRD-05");
        foreach (ProjectDef p in rig.Catalog.Projects.Where(p => p.Numeric && p.Era == 3 && !p.IsKeystone && !rig.Research.IsDone(0, p.Index)).Take(17))
            rig.Do(rig.Grant(0, p.Id));
        int key4 = rig.P("KEY-4");
        Assert.Equal(ProjectState.Available, rig.Research.State(0, key4));
        int leftover = rig.Catalog.Projects.First(p => p.Numeric && p.Era == 3 && !p.IsKeystone && !rig.Research.IsDone(0, p.Index)).Index;
        Fix64 listCost = rig.Catalog.Projects[leftover].Cost;
        Assert.Equal(listCost, rig.Research.EffectiveCost(0, leftover)); // in-era: no lag

        Fix64 firstsBefore = rig.Research.FirstsLegacy(0);
        rig.Do(rig.Slot(0, 0, "KEY-4"));
        rig.Run(2 * Season);
        Assert.True(rig.Research.IsDone(0, key4));
        Assert.Equal(4, rig.Research.Era(0));
        Assert.Single(rig.Events(ResearchEventKind.EraAdvanced, "KEY-4"), e => e.A == 4);
        // E3 leftovers carry on at EraLag 0.7, E0-E2 leftovers at 0.7 per era behind.
        Assert.Equal(listCost * rig.Catalog.Constant("EraLag"), rig.Research.EffectiveCost(0, leftover));
        // Nothing of E4 or later can be researched or even glimpsed, and no keystone can be chased.
        foreach (ProjectDef p in rig.Catalog.Projects.Where(p => p.Era >= 4))
            Assert.Equal(ProjectState.Hidden, rig.Research.State(0, p.Index));
        Assert.Equal(Fix64.FromInt(48), rig.Research.FirstsLegacy(0) - firstsBefore); // KEY-4's First
    }

    [Fact]
    public void The_Council_keeps_picking_E3_projects_and_stops_chasing_a_keystone_after_KEY4()
    {
        var rig = new ResearchRig(civs: 1, insightPerYear: 300, council: true);
        ResearchSystemTestsHelper.ReachEra(rig, 3);
        rig.Run(3 * Season);
        var picks = rig.Events(ResearchEventKind.CouncilPick).Select(e => rig.Catalog.Projects[e.Project]).ToList();
        Assert.Contains(picks, p => p.Era == 3);
        // Past KEY-4 the Council still fills every slot from the leftovers.
        foreach (ProjectDef p in rig.Catalog.Projects.Where(p => p.Numeric && p.Era == 3 && !p.IsKeystone && !rig.Research.IsDone(0, p.Index)).Take(24))
            rig.Do(rig.Grant(0, p.Id));
        rig.Do(rig.Grant(0, "KEY-4"));
        Assert.Equal(4, rig.Research.Era(0));
        rig.Run(2 * Season);
        for (int s = 0; s < CivResearch.SlotCount; s++) Assert.True(rig.Research.Slot(0, s) >= 0, "a leftover should fill the slot");
        Span<Fix64> prior = stackalloc Fix64[rig.Catalog.Count];
        rig.Research.CouncilPrior(0, prior);
        Assert.Contains(prior.ToArray(), x => x.Raw > 0);
    }

    [Fact]
    public void Leaning_steers_the_Council_among_E3_projects()
    {
        Fix64 Score((int Craft, int Kin, int Reach) leaning, string project)
        {
            var rig = new ResearchRig(civs: 1, leaning: leaning);
            ResearchSystemTestsHelper.ReachEra(rig, 3);
            var scores = new Fix64[rig.Catalog.Count];
            rig.Research.CouncilPrior(0, scores);
            Assert.True(scores[rig.P(project)].Raw > 0, $"{project} should be Available and scored");
            return scores[rig.P(project)];
        }
        // CHM-04 is Craft, DEF-04 is Reach: both are Available at the start of the age.
        Assert.Equal(Theme.Craft, TestPack.Catalog.Get("CHM-04").Theme);
        Assert.Equal(Theme.Reach, TestPack.Catalog.Get("DEF-04").Theme);
        Assert.True(Score((80, 10, 10), "CHM-04") > Score((10, 10, 80), "CHM-04"));
        Assert.True(Score((10, 10, 80), "DEF-04") > Score((80, 10, 10), "DEF-04"));
    }

    [Fact]
    public void Diffusion_applies_to_E3_projects_known_by_a_contact()
    {
        var rig = new ResearchRig(civs: 2);
        ResearchSystemTestsHelper.ReachEra(rig, 3);
        int met03 = rig.P("MET-03");
        Fix64 cost = rig.Research.EffectiveCost(0, met03);
        rig.Research.RecordContact(0, 1);
        rig.Do(rig.Grant(1, "MET-03"));
        Fix64 step = rig.Catalog.Constant("DiffusionStep"), floor = rig.Catalog.Constant("DiffusionFloor");
        Assert.Equal(cost * Fix64.Max(floor, Fix64.One - step), rig.Research.EffectiveCost(0, met03));
        Assert.True(rig.Research.EffectiveCost(0, met03) < cost);
    }

    [Fact]
    public void Dormant_applies_to_a_completed_E3_project_that_loses_its_access_and_recovers()
    {
        var rig = new ResearchRig();
        ResearchSystemTestsHelper.ReachEra(rig, 3);
        rig.GrantAll(0, "MET-03", "MET-05");
        int tin = rig.Catalog.GateTargetIndex("Tin");
        Assert.Equal(ProjectState.Completed, rig.Research.State(0, rig.P("MET-05")));
        rig.Research.SetAccess(0, tin, false);
        Assert.Equal(ProjectState.Dormant, rig.Research.State(0, rig.P("MET-05")));
        Assert.Single(rig.Events(ResearchEventKind.DormantChanged, "MET-05"), e => e.A == 1);
        rig.Research.SetAccess(0, tin, true);
        Assert.Equal(ProjectState.Completed, rig.Research.State(0, rig.P("MET-05")));
    }

    [Fact]
    public void Know_how_flows_into_E3_projects_and_methods_spread_by_adoption()
    {
        var rig = new ResearchRig(civs: 1, insightPerYear: 200, configure: s => { s.Work["smelt"] = Fix64.FromInt(3); s.PractisingShare = Fix64.One; });
        ResearchSystemTestsHelper.ReachEra(rig, 3);
        rig.Run(2 * Season);
        int met = rig.Catalog.LineageIndex("MET");
        Assert.True(rig.Research.KnowHow(0, met) > Fix64.Zero, "smelting practice should fill the MET pool");
        rig.Do(rig.Slot(0, 0, "MET-03"));
        rig.Run(Season);
        Assert.True(rig.Research.KnowHowApplied(0, rig.P("MET-03")) > Fix64.Zero || rig.Research.IsDone(0, rig.P("MET-03")));
        rig.GrantAll(0, "WOD-05");
        Assert.True(rig.Research.AdoptionFraction(0, rig.P("WOD-05")) < Fix64.One, "a new method starts with its innovator");
        rig.Run(12 * Season);
        Assert.Equal(Fix64.One, rig.Research.AdoptionFraction(0, rig.P("WOD-05")));
    }

    [Fact]
    public void A_snapshot_of_a_civilization_in_the_Iron_threshold_restores_exactly()
    {
        var rig = new ResearchRig(civs: 1, council: true, insightPerYear: 300);
        ResearchSystemTestsHelper.ReachEra(rig, 3);
        foreach (ProjectDef p in rig.Catalog.Projects.Where(p => p.Numeric && p.Era == 3 && !p.IsKeystone && !rig.Research.IsDone(0, p.Index)).Take(24))
            rig.Do(rig.Grant(0, p.Id));
        rig.Do(rig.Grant(0, "KEY-4"));
        rig.Run(3 * Season);
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) rig.Research.WriteSnapshot(w);
        stream.Position = 0;
        var copy = new ResearchRig(civs: 1, council: true, insightPerYear: 300);
        using (var rd = new BinaryReader(stream)) copy.Research.ReadSnapshot(rd);
        var h1 = new StateHasher(); rig.Research.Hash(ref h1);
        var h2 = new StateHasher(); copy.Research.Hash(ref h2);
        Assert.Equal(h1.Finish(), h2.Finish());
        Assert.Equal(4, copy.Research.Era(0));
        Assert.Equal(rig.Research.Mandate(0), copy.Research.Mandate(0));
    }
}

public class BronzeEventStreamTests
{
    private static readonly string[] E3Ids = TestPack.Catalog.Projects.Where(p => p.Numeric && (p.Era == 3 || p.Id == "KEY-4")).Select(p => p.Id).ToArray();

    [Fact]
    public void B4_all_66_E3_nodes_emit_Completed_Moment_and_a_VisibleChange()
    {
        Assert.Equal(66, E3Ids.Length);
        var rig = new ResearchRig();
        foreach (string id in E3Ids) rig.Do(rig.Grant(0, id));
        var missing = new List<string>();
        foreach (string id in E3Ids)
        {
            bool completed = rig.Events(ResearchEventKind.Completed, id).Any();
            bool moment = rig.Events(ResearchEventKind.Moment, id).Any();
            bool visible = rig.Events(ResearchEventKind.VisibleChange, id).Any();
            if (!(completed && moment && visible)) missing.Add($"{id} (completed {completed}, moment {moment}, visible {visible})");
        }
        Assert.True(missing.Count == 0, "no full event set: " + string.Join("; ", missing));
        // Kinds the economy does not know yet (mandate, admin, defense, sanitation, tier ...) are events, not errors.
        Assert.Contains(rig.Events(ResearchEventKind.VisibleChange, "GOV-05"), e => e.A == (int)VisibleChange.Overlay);
    }

    [Fact]
    public void Every_E3_completion_applies_to_the_living_economy_without_changing_its_rules_of_play()
    {
        MatchModel match = MatchModel.Create(777, TestPack.Pack);
        var runner = new SimRunner(match);
        ResearchCatalog catalog = match.Research.Catalog;
        foreach (ProjectDef p in catalog.Projects.Where(p => p.Numeric && p.Era <= 3 && !p.IsKeystone))
            runner.Submit(new Command(0, 2, CommandKind.CompleteProject, p.Index, 0, 0));
        foreach (string key in new[] { "KEY-1", "KEY-2", "KEY-3", "KEY-4" })
            runner.Submit(new Command(0, 2, CommandKind.CompleteProject, catalog.IndexOf(key), 0, 0));
        runner.Advance(4 * ResearchSystem.SeasonTicks);
        Assert.Equal(4, match.Research.Era(2));
        Assert.True(match.Research.IsDone(2, catalog.IndexOf("KEY-4")));
        // The civilization still lives and its people still work.
        int people = 0;
        for (int f = 0; f < match.Economy.FigureCount; f++) if (match.Economy.Figure(f).Civilization == 2) people++;
        Assert.True(people > 0);
    }
}

public class BronzeChapterTests
{
    private const int Season = ResearchSystem.SeasonTicks;

    private static ResearchRig FourCivs() => new(civs: 4);

    private static void Key4At(ResearchRig rig, int civ, int season, int intoSeason = 5)
    {
        rig.Tick = season * (long)Season + intoSeason;
        rig.Do(rig.Grant(civ, "KEY-4"));
    }

    [Fact]
    public void With_no_KEY4_the_chapter_ends_at_Year_64()
    {
        MatchEnd end = MatchEnd.Evaluate(FourCivs().Research, humanSeat: 0);
        Assert.Equal(MatchEndReason.YearCap, end.Reason);
        Assert.Equal(64 * 4 - 1, end.EndSeason);
        Assert.Equal(64 * 600L, end.EndTick);
        Assert.False(end.IsOver(64 * 600L - 1));
        Assert.True(end.IsOver(64 * 600L));
        Assert.False(end.CountdownRunning);
        Assert.Equal(-1, end.CountdownCiv);
    }

    [Fact]
    public void Clause_two_twelve_seasons_after_the_first_rival_completes_KEY4()
    {
        ResearchRig rig = FourCivs();
        Key4At(rig, 2, season: 6);
        Key4At(rig, 3, season: 9); // later rivals do not move it
        foreach (int human in new[] { -1, 0 })
        {
            MatchEnd end = MatchEnd.Evaluate(rig.Research, human);
            Assert.Equal(MatchEndReason.RivalCountdown, end.Reason);
            Assert.Equal(6 + 12, end.EndSeason);
            Assert.Equal(2, end.Civ);
            Assert.Equal((6, 2), (end.CountdownStartSeason, end.CountdownCiv));
            Assert.True(end.CountdownRunning);
            Assert.Equal((18 + 1) * (long)Season, end.EndTick);
            Assert.False(end.IsOver(19 * Season - 1));
            Assert.True(end.IsOver(19 * Season));
            Assert.Equal(12, end.SeasonsLeft(6 * Season + 5)); // "3 years left" as the countdown appears
            Assert.Equal(1, end.SeasonsLeft(17 * Season));
            Assert.Equal(0, end.SeasonsLeft(18 * Season));      // the final season
            Assert.Equal(0, end.SeasonsLeft(19 * Season));      // over
        }
    }

    [Fact]
    public void Clause_one_the_human_completing_KEY4_ends_the_season_it_happens_in_if_that_is_sooner()
    {
        ResearchRig rig = FourCivs();
        Key4At(rig, 2, season: 6);
        Key4At(rig, 0, season: 13, intoSeason: 149); // the last tick of season 13
        MatchEnd end = MatchEnd.Evaluate(rig.Research, humanSeat: 0);
        Assert.Equal(MatchEndReason.HumanKeystone, end.Reason);
        Assert.Equal(13, end.EndSeason);
        Assert.Equal(0, end.Civ);
        Assert.Equal(14 * (long)Season, end.EndTick);
        Assert.False(end.CountdownRunning); // the human's clause closes the chapter first
        Assert.Equal(6, end.CountdownStartSeason);
        // Without a human seat the same history is the countdown only.
        Assert.Equal(MatchEndReason.RivalCountdown, MatchEnd.Evaluate(rig.Research, humanSeat: -1).Reason);
        // Another seat as the human: civilization 0 is just a rival, and it is not the first one.
        MatchEnd other = MatchEnd.Evaluate(rig.Research, humanSeat: 1);
        Assert.Equal((MatchEndReason.RivalCountdown, 18, 2), (other.Reason, other.EndSeason, other.Civ));
    }

    [Fact]
    public void A_human_who_is_too_late_does_not_change_the_countdown_and_a_tie_goes_to_the_human()
    {
        ResearchRig late = FourCivs();
        Key4At(late, 2, season: 6);
        Key4At(late, 0, season: 25);
        MatchEnd end = MatchEnd.Evaluate(late.Research, 0);
        Assert.Equal((MatchEndReason.RivalCountdown, 18), (end.Reason, end.EndSeason));

        ResearchRig tie = FourCivs();
        Key4At(tie, 2, season: 6);
        Key4At(tie, 0, season: 18);
        Assert.Equal((MatchEndReason.HumanKeystone, 18), (MatchEnd.Evaluate(tie.Research, 0).Reason, MatchEnd.Evaluate(tie.Research, 0).EndSeason));
    }

    [Fact]
    public void The_human_finishing_first_with_no_rival_ends_at_once()
    {
        ResearchRig rig = FourCivs();
        Key4At(rig, 0, season: 40);
        MatchEnd end = MatchEnd.Evaluate(rig.Research, 0);
        Assert.Equal((MatchEndReason.HumanKeystone, 40), (end.Reason, end.EndSeason));
        Assert.Equal(-1, end.CountdownStartSeason);
        // A headless match has no human clause: the same history leaves Year 64 or the (human-as-rival) countdown.
        MatchEnd headless = MatchEnd.Evaluate(rig.Research, -1);
        Assert.Equal((MatchEndReason.RivalCountdown, 52), (headless.Reason, headless.EndSeason));
    }

    [Fact]
    public void Clause_three_the_year_cap_wins_when_the_countdown_would_run_past_it()
    {
        ResearchRig rig = FourCivs();
        Key4At(rig, 1, season: 250);
        MatchEnd end = MatchEnd.Evaluate(rig.Research, -1);
        Assert.Equal((MatchEndReason.YearCap, 255), (end.Reason, end.EndSeason));
        Assert.Equal(250, end.CountdownStartSeason); // the countdown is real, the cap just comes first
        // A countdown ending exactly on the cap is still the countdown.
        ResearchRig exact = FourCivs();
        Key4At(exact, 1, season: 243);
        Assert.Equal((MatchEndReason.RivalCountdown, 255), (MatchEnd.Evaluate(exact.Research, -1).Reason, MatchEnd.Evaluate(exact.Research, -1).EndSeason));
    }

    [Fact]
    public void The_first_rival_is_the_earliest_completion_and_the_lowest_civilization_breaks_a_tie()
    {
        ResearchRig rig = FourCivs();
        rig.Tick = 10 * Season + 20;
        rig.Do(rig.Grant(3, "KEY-4"));
        rig.Tick = 10 * Season + 20;
        rig.Do(rig.Grant(1, "KEY-4"));
        MatchEnd end = MatchEnd.Evaluate(rig.Research, -1);
        Assert.Equal((1, 10), (end.CountdownCiv, end.CountdownStartSeason));
    }

    [Fact]
    public void The_end_is_the_same_in_a_rerun_a_restore_and_a_replay()
    {
        MatchEnd Play(out ulong checksum, out MatchModel model)
        {
            MatchModel match = MatchModel.Create(20261019, TestPack.Pack);
            var runner = new SimRunner(match);
            int key4 = match.Research.Catalog.IndexOf("KEY-4");
            runner.Submit(new Command(2 * Season + 40, 1, CommandKind.CompleteProject, key4, 0, 0));
            runner.Submit(new Command(5 * Season + 7, 0, CommandKind.CompleteProject, key4, 0, 0));
            runner.Advance(8 * Season);
            checksum = runner.CurrentChecksum;
            model = match;
            return match.EndOfMatch(humanSeat: 0);
        }

        MatchEnd first = Play(out ulong c1, out MatchModel m1);
        MatchEnd second = Play(out ulong c2, out _);
        Assert.Equal(first, second);
        Assert.Equal(c1, c2);
        Assert.Equal((MatchEndReason.HumanKeystone, 5), (first.Reason, first.EndSeason)); // season 5 beats 2 + 12
        Assert.Equal(6L * Season, first.EndTick);

        // A snapshot restore reads the same completion ticks.
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) m1.WriteSnapshot(w);
        stream.Position = 0;
        MatchModel restored = MatchModel.Create(20261019, TestPack.Pack);
        using (var rd = new BinaryReader(stream)) restored.ReadSnapshot(rd);
        Assert.Equal(first, restored.EndOfMatch(0));
        Assert.Equal(m1.EndOfMatch(-1), restored.EndOfMatch(-1));
    }

    [Fact]
    public void A_Council_match_reaches_KEY4_through_research_and_the_chapter_ends_by_the_rule()
    {
        // The whole path in the living economy: E3 projects research, complete and apply; KEY-4 completes; the countdown runs.
        var balanced = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        MatchModel match = MatchModel.Create(777, TestPack.Pack, balanced);
        var runner = new SimRunner(match);
        int key4 = match.Research.Catalog.IndexOf("KEY-4");
        while (runner.Tick < 55 * 600L && !match.EndOfMatch(-1).CountdownRunning) runner.Advance(Season);
        Assert.True(match.EndOfMatch(-1).CountdownRunning, "someone should have completed KEY-4 within 55 minutes");
        MatchEnd end = match.EndOfMatch(-1);
        Assert.Equal(MatchEndReason.RivalCountdown, end.Reason);
        Assert.True(match.Research.IsDone(end.CountdownCiv, key4));
        Assert.Equal(4, match.Research.Era(end.CountdownCiv));
        Assert.Equal(end.CountdownStartSeason + 12, end.EndSeason);
        // E4 stays hidden for everyone, and Mandate has been recharging since KEY-3.
        for (int c = 0; c < 4; c++)
        {
            Assert.True(match.Research.MandateUnlocked(c));
            foreach (ProjectDef p in match.Research.Catalog.Projects.Where(p => p.Era >= 4))
                Assert.Equal(ProjectState.Hidden, match.Research.State(c, p.Index));
        }
    }
}
