using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Tests;

public class ResearchDiffusionTests
{
    private static void Close(Fix64 expected, Fix64 actual)
        => Assert.True(Math.Abs(expected.Raw - actual.Raw) < 1000, $"expected {expected}, got {actual}"); // a few ulps of Q32.32

    [Fact]
    public void A_contact_that_knows_a_project_makes_it_ten_percent_cheaper_down_to_half()
    {
        var rig = new ResearchRig(civs: 7);
        int p = rig.P("FOR-01");
        Assert.Equal(Fix64.FromInt(22), rig.Research.EffectiveCost(0, p));
        for (int c = 1; c <= 6; c++) rig.Do(rig.Grant(c, "FOR-01"));
        Assert.Equal(Fix64.FromInt(22), rig.Research.EffectiveCost(0, p)); // knowing is not contact
        rig.Research.RecordContact(0, 1);
        Close(Fix64.FromInt(22) * Fix64.Parse("0.9"), rig.Research.EffectiveCost(0, p));
        rig.Research.RecordContact(0, 2);
        Close(Fix64.FromInt(22) * Fix64.Parse("0.8"), rig.Research.EffectiveCost(0, p));
        for (int c = 3; c <= 6; c++) rig.Research.RecordContact(0, c);
        Assert.Equal(Fix64.FromInt(22) * Fix64.Half, rig.Research.EffectiveCost(0, p)); // six knowers would be 0.4: the floor is 0.5
        Assert.Equal(Fix64.FromInt(22), rig.Research.EffectiveCost(1, p)); // civilization 1's only contact (0) does not know it
    }

    [Fact]
    public void Contact_glimpses_what_the_other_civilization_has_and_satisfies_its_Seen_gates()
    {
        var rig = new ResearchRig(civs: 2);
        // Civilization 1 finished FOR-02 and MET-01; civilization 0 can see neither yet.
        ResearchSystemTestsHelper.ReachEra(rig, 0);
        foreach (string id in new[] { "KEY-1", "KEY-2", "STN-08", "MET-01", "FOR-02" }) rig.Do(rig.Grant(1, id));
        foreach (string id in new[] { "KEY-1", "KEY-2", "STN-08" }) rig.Do(rig.Grant(0, id));
        Assert.Equal(ProjectState.Hidden, rig.Research.State(0, rig.P("MET-01")));
        Assert.Equal(ProjectState.Hidden, rig.Research.State(0, rig.P("FOR-02")));
        rig.Research.RecordContact(0, 1);
        Assert.Equal(ProjectState.Available, rig.Research.State(0, rig.P("MET-01"))); // (c) a contact has completed it
        Assert.Equal(ProjectState.Glimpsed, rig.Research.State(0, rig.P("FOR-02")));  // known to exist, prerequisites missing
        Assert.True(rig.Research.InContact(1, 0));
        Assert.Single(rig.Events(ResearchEventKind.ContactMade), e => e.Civ == 0);
    }

    [Fact]
    public void Backfills_cost_less_by_era_lag_and_a_cold_home_discounts_snow_gear()
    {
        var rig = new ResearchRig(configure: s => s.ClimateGate = TestPack.Catalog.GateTargetIndex("Cold"));
        Assert.Equal(Fix64.FromInt(22), rig.Research.EffectiveCost(0, rig.P("STN-01")));
        ResearchSystemTestsHelper.ReachEra(rig, 1);
        Close(Fix64.FromInt(22) * Fix64.Parse("0.7"), rig.Research.EffectiveCost(0, rig.P("STN-01")));
        // Snowshoes & Skis [Climate: Cold]: cost x0.7 in a cold home
        Close(Fix64.FromInt(50) * Fix64.Parse("0.7"), rig.Research.EffectiveCost(0, rig.P("TRN-03")));
        ResearchSystemTestsHelper.ReachEra(rig, 2);
        Close(Fix64.FromInt(22) * Fix64.Parse("0.7") * Fix64.Parse("0.7"), rig.Research.EffectiveCost(0, rig.P("STN-01")));
    }

    [Fact]
    public void The_first_civilization_to_finish_a_project_earns_Pioneer_and_Legacy()
    {
        var rig = new ResearchRig(civs: 2);
        rig.Do(rig.Grant(0, "FIR-03"));
        rig.Do(rig.Grant(1, "FIR-03"));
        Assert.Equal(0, rig.Research.FirstBy(rig.P("FIR-03")));
        Assert.True(rig.Research.IsPioneer(0, rig.P("FIR-03")));
        Assert.False(rig.Research.IsPioneer(1, rig.P("FIR-03")));
        Assert.Equal(Fix64.FromInt(11), rig.Research.FirstsLegacy(0)); // era base 22 x 0.5
        Assert.Equal(Fix64.Zero, rig.Research.FirstsLegacy(1));
        // Pioneer: +10% to the project's own modifiers, for that civilization only (Commons +10% becomes +11%)
        Close(Fix64.Parse("1.11"), rig.Research.ModifierMultiplier(0, "commons_insight"));
        Close(Fix64.Parse("1.1"), rig.Research.ModifierMultiplier(1, "commons_insight"));
        Assert.Single(rig.Events(ResearchEventKind.FirstClaimed, "FIR-03"));
        // Keystones are worth triple (docs/MVP/02 §14)
        var keys = new ResearchRig();
        ResearchInsightTests.GrantEraOne(keys);
        Assert.True(keys.Research.FirstsLegacy(0) >= Fix64.FromInt(33));
    }
}

public class ResearchAdoptionTests
{
    private static int SeasonsUntil(ResearchRig rig, string project, string fraction)
    {
        Fix64 target = Fix64.Parse(fraction);
        for (int seasons = 1; seasons <= 40; seasons++)
        {
            rig.Run(150);
            if (rig.Research.AdoptionFraction(0, rig.P(project)) >= target) return seasons;
        }
        return int.MaxValue;
    }

    [Fact]
    public void An_innovator_starts_alone_and_a_small_settlement_saturates_in_two_to_four_seasons()
    {
        var rig = new ResearchRig();
        rig.Do(rig.Grant(0, "STN-01"));
        int stn = rig.P("STN-01");
        Assert.Equal(Fix64.One / Fix64.FromInt(12), rig.Research.AdoptionFraction(0, stn)); // the innovator alone
        int seasons = SeasonsUntil(rig, "STN-01", "0.9");
        Assert.InRange(seasons, 2, 4);
        rig.Run(150 * 4);
        Assert.Equal(Fix64.One, rig.Research.AdoptionFraction(0, stn));
        Assert.Contains(rig.Events(ResearchEventKind.Adopted, "STN-01"), e => e.A == 50);
        Assert.Contains(rig.Events(ResearchEventKind.Adopted, "STN-01"), e => e.A == 100);
    }

    [Fact]
    public void Imitation_and_language_speed_adoption_and_a_big_settlement_slows_it()
    {
        int Seasons(Action<ResearchRig> setup, Fix64 people)
        {
            var rig = new ResearchRig(configure: s => s.PeoplePerSettlement = people);
            setup(rig);
            rig.Do(rig.Grant(0, "STN-01"));
            return SeasonsUntil(rig, "STN-01", "0.9");
        }
        int plain = Seasons(_ => { }, Fix64.FromInt(12));
        int taught = Seasons(r => r.GrantAll(0, "LRN-01", "COM-02"), Fix64.FromInt(12)); // p_teach x1.5 x2
        int crowded = Seasons(_ => { }, Fix64.FromInt(120));
        Assert.True(taught < plain, $"taught {taught} plain {plain}");
        Assert.True(crowded > plain, $"crowded {crowded} plain {plain}");
    }

    [Fact]
    public void Numeric_effects_of_a_skill_arrive_with_adoption_but_structures_land_at_once()
    {
        var rig = new ResearchRig(civs: 2);
        rig.Do(rig.Grant(1, "FOR-01")); // civilization 1 first, so no Pioneer bonus below
        rig.Do(rig.Grant(1, "BLD-01"));
        rig.Do(rig.Grant(0, "FOR-01")); // Plant Lore (a Discovery): hazard endemic x0.98 once people know it
        rig.Do(rig.Grant(0, "BLD-01")); // Lean-tos (a Structure): exposure x0.93 immediately
        Assert.Equal(Fix64.Parse("0.93"), rig.Research.HazardMultiplier(0, "exposure"));
        Fix64 early = rig.Research.HazardMultiplier(0, "endemic");
        Assert.True(early > Fix64.Parse("0.995"), early.ToString()); // 1/12 of people know it
        rig.Run(150 * 5);
        Fix64 late = rig.Research.HazardMultiplier(0, "endemic");
        Assert.InRange(late.ToDoubleForDisplay(), 0.97999, 0.98001); // full adoption gives the written x0.98
    }

    [Fact]
    public void A_daughter_band_carries_what_its_parent_knows_and_distant_camps_hear_later()
    {
        var rig = new ResearchRig(configure: s => s.Settlements = 2);
        rig.Do(rig.Grant(0, "STN-01"));
        rig.Run(150 * 7); // the parent saturates first; settlement 1 (a season's lag) is seeded and catches up
        Assert.True(rig.Research.AdoptionFraction(0, rig.P("STN-01")) > Fix64.Parse("0.9"));

        var fission = new ResearchRig(configure: s => s.Settlements = 1);
        fission.Do(fission.Grant(0, "STN-01"));
        fission.Run(150 * 3);
        fission.Source.Settlements = 2;
        fission.Source.Fissions.Add((0, 1));
        fission.Run(150);
        Assert.True(fission.Research.AdoptionFraction(0, fission.P("STN-01")) >= Fix64.Parse("0.99")); // the band left knowing it
    }
}

public class ResearchCouncilTests
{
    [Fact]
    public void A_manual_player_setup_keeps_empty_slots_available_for_the_opening_choice()
    {
        ResearchCatalog catalog = TestPack.Catalog;
        ResearchCivSetup player = ResearchCivSetup.Player(34, 33, 33, council: false);
        var research = new ResearchSystem(catalog, new FixedSource(catalog), 1, new[] { player });

        Assert.False(research.CouncilOn(0));
        for (int tick = 0; tick < 300; tick++) research.Step(tick, ReadOnlySpan<Command>.Empty);
        for (int slot = 0; slot < 3; slot++) Assert.Equal(-1, research.Slot(0, slot));
    }

    [Fact]
    public void An_AI_Council_fills_every_empty_slot_at_once_on_the_keystone_path()
    {
        var rig = new ResearchRig(council: true);
        rig.Run(10);
        bool[] closure = rig.Catalog.ClosureOf(0)!;
        for (int s = 0; s < 3; s++)
        {
            int p = rig.Research.Slot(0, s);
            Assert.True(p >= 0);
            Assert.True(closure[p], $"{rig.Catalog.Projects[p].Id} is not on KEY-1's path");
        }
        Assert.Equal(3, rig.Events(ResearchEventKind.CouncilPick).Count(e => e.B == (int)CouncilReason.KeystonePath));
    }

    [Fact]
    public void The_player_Council_waits_a_season_before_filling_an_empty_slot()
    {
        var rig = new ResearchRig(council: true, councilDelay: ResearchSystem.SeasonTicks);
        rig.Run(100);
        Assert.Equal(-1, rig.Research.Slot(0, 0));
        rig.Run(60);
        Assert.True(rig.Research.Slot(0, 0) >= 0);
        // A queued project is taken at once, with no wait
        var queued = new ResearchRig(council: true, councilDelay: ResearchSystem.SeasonTicks);
        queued.Run(1, new Command(0, 0, CommandKind.QueueProject, queued.P("WAY-01"), 0, 0));
        Assert.Equal(queued.P("WAY-01"), queued.Research.Slot(0, 0));
    }

    [Fact]
    public void Leaning_steers_the_Council_toward_a_theme()
    {
        foreach ((int craft, int kin, int reach, Theme theme) in new[] { (100, 1, 1, Theme.Craft), (1, 100, 1, Theme.Kin), (1, 1, 100, Theme.Reach) })
        {
            var rig = new ResearchRig(council: true, leaning: (craft, kin, reach));
            rig.Run(10);
            Assert.Equal(theme, rig.Catalog.Projects[rig.Research.Slot(0, 0)].Theme);
        }
        // Leaning can change in play
        var live = new ResearchRig(council: true, leaning: (100, 1, 1));
        live.Do(new Command(0, 0, CommandKind.SetLeaning, 2, 100, 0), new Command(0, 0, CommandKind.SetLeaning, 0, 1, 0));
        Assert.Equal(100, live.Research.Leaning(0, 2));
        Assert.Equal(1, live.Research.Leaning(0, 0));
    }

    [Fact]
    public void Famine_makes_the_Elders_suggest_food_lineages_once_the_keystone_path_is_done()
    {
        ResearchCatalog catalog = TestPack.Catalog;
        var rig = new ResearchRig(council: true, configure: s => s.Signals = new UrgencySignals(Fix64.One, Fix64.Zero, Fix64.Zero, Fix64.Zero));
        // Complete every project on KEY-1's path except the keystone; breadth is then the only goal and the Elders
        // choose among free picks, where famine urgency decides.
        bool[] closure = catalog.ClosureOf(0)!;
        foreach (ProjectDef p in catalog.Projects.Where(p => closure[p.Index] && !p.IsKeystone)) rig.Do(rig.Grant(0, p.Id));
        rig.Run(10);
        string[] food = { "FOR", "HNT", "FOD", "AGR", "HUS", "SEA" };
        int first = rig.Research.Slot(0, 0);
        Assert.Contains(catalog.LineageCodes[catalog.Projects[first].Lineage], food);
        Assert.Contains(rig.Events(ResearchEventKind.CouncilPick), e => e.B == (int)CouncilReason.FamineRisk || e.B == (int)CouncilReason.KeystonePath);
    }

    [Fact]
    public void Ties_between_equal_scores_resolve_by_project_order_so_two_runs_agree()
    {
        var a = new ResearchRig(council: true);
        var b = new ResearchRig(council: true);
        a.Run(3000);
        b.Run(3000);
        var hashA = new StateHasher();
        var hashB = new StateHasher();
        a.Research.Hash(ref hashA);
        b.Research.Hash(ref hashB);
        Assert.Equal(hashA.Finish(), hashB.Finish());
        Assert.True(a.Research.DoneCount(0) > 5, $"done {a.Research.DoneCount(0)}");
    }
}

public class ResearchCommandTests
{
    [Fact]
    public void Slot_commands_swap_replace_clear_and_reject_the_unavailable()
    {
        var rig = new ResearchRig();
        rig.Do(rig.Slot(0, 0, "FOR-01"), rig.Slot(0, 1, "STN-01"));
        rig.Do(rig.Slot(0, 0, "STN-01")); // dragging between slots re-ranks
        Assert.Equal(rig.P("STN-01"), rig.Research.Slot(0, 0));
        Assert.Equal(rig.P("FOR-01"), rig.Research.Slot(0, 1));
        rig.Do(rig.Slot(0, 2, "STN-02")); // glimpsed, not available
        rig.Do(rig.Slot(0, 2, "STN-05")); // hidden
        rig.Do(new Command(rig.Tick, 0, CommandKind.SetSlot, 7, rig.P("COM-01"), 0)); // no such slot
        rig.Do(new Command(rig.Tick, 0, CommandKind.SetSlot, 2, 100000, 0));
        Assert.Equal(-1, rig.Research.Slot(0, 2));
        rig.Do(rig.Slot(0, 1, "HNT-01")); // replacing returns FOR-01 to Available
        Assert.Equal(ProjectState.Available, rig.Research.State(0, rig.P("FOR-01")));
        rig.Do(rig.Slot(0, 0, ""));
        Assert.Equal(-1, rig.Research.Slot(0, 0));
        Assert.Equal(ProjectState.Available, rig.Research.State(0, rig.P("STN-01")));
    }

    [Fact]
    public void The_queue_takes_available_entries_in_order_and_waits_on_glimpsed_ones()
    {
        var rig = new ResearchRig();
        rig.Do(new Command(0, 0, CommandKind.QueueProject, rig.P("STN-05"), 0, 0)); // hidden: refused
        Assert.Empty(rig.Research.Queue(0));
        rig.Do(rig.Slot(0, 0, "FOR-01"), rig.Slot(0, 1, "STN-01"), rig.Slot(0, 2, "HNT-01"));
        int tick = (int)rig.Tick;
        rig.Do(new Command(tick, 0, CommandKind.QueueProject, rig.P("STN-02"), 0, 0), // glimpsed: waits for STN-01
               new Command(tick, 0, CommandKind.QueueProject, rig.P("WAY-01"), 0, 0),
               new Command(tick, 0, CommandKind.QueueProject, rig.P("WAY-01"), 0, 0)); // duplicate ignored
        Assert.Equal(new[] { rig.P("STN-02"), rig.P("WAY-01") }, rig.Research.Queue(0));
        rig.Do(rig.Grant(0, "FOR-01")); // frees slot 0: WAY-01 is Available, STN-02 is not yet, so WAY-01 is taken
        Assert.Equal(rig.P("WAY-01"), rig.Research.Slot(0, 0));
        Assert.Equal(new[] { rig.P("STN-02") }, rig.Research.Queue(0));
        rig.Do(rig.Grant(0, "STN-01"));
        Assert.Equal(rig.P("STN-02"), rig.Research.Slot(0, 1));
        rig.Do(new Command(rig.Tick, 0, CommandKind.DequeueProject, 5, 0, 0)); // out of range: ignored
    }

    [Fact]
    public void The_Council_toggle_and_the_Council_stay_out_when_off()
    {
        var rig = new ResearchRig(council: true);
        rig.Do(new Command(0, 0, CommandKind.SetCouncil, 0, 0, 0));
        Assert.False(rig.Research.CouncilOn(0));
        rig.Do(rig.Slot(0, 0, ""), rig.Slot(0, 1, ""), rig.Slot(0, 2, ""));
        rig.Run(300);
        Assert.Equal(-1, rig.Research.Slot(0, 1));
    }
}

public class ResearchDeterminismTests
{
    private static ulong Hash(ResearchSystem r)
    {
        var h = new StateHasher();
        r.Hash(ref h);
        return h.Finish();
    }

    private static ResearchRig Play(long ticks, bool snapshotMidway = false)
    {
        var rig = new ResearchRig(civs: 2, insightPerYear: 90, council: true, councilDelay: 150, configure: s => s.Settlements = 2);
        rig.Research.RecordContact(0, 1);
        rig.Run(ticks / 2, rig.Slot(0, 0, "FOR-01"), new Command(0, 0, CommandKind.QueueProject, rig.P("STN-01"), 0, 0));
        if (snapshotMidway)
        {
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) rig.Research.WriteSnapshot(w);
            stream.Position = 0;
            var resumed = new ResearchRig(civs: 2, insightPerYear: 90, council: true, councilDelay: 150, configure: s => s.Settlements = 2);
            using (var r = new BinaryReader(stream)) resumed.Research.ReadSnapshot(r);
            resumed.Tick = rig.Tick;
            Assert.Equal(Hash(rig.Research), Hash(resumed.Research));
            resumed.Run(ticks - ticks / 2);
            return resumed;
        }
        rig.Run(ticks - ticks / 2);
        return rig;
    }

    [Fact]
    public void The_same_inputs_give_the_same_state_and_a_snapshot_resumes_exactly()
    {
        ResearchRig straight = Play(6000), again = Play(6000), resumed = Play(6000, snapshotMidway: true);
        Assert.Equal(Hash(straight.Research), Hash(again.Research));
        Assert.Equal(Hash(straight.Research), Hash(resumed.Research));
        Assert.True(straight.Research.DoneCount(0) >= 20, $"done {straight.Research.DoneCount(0)}");
    }

    [Fact]
    public void A_different_command_changes_the_outcome()
    {
        var plain = new ResearchRig(council: true);
        var changed = new ResearchRig(council: true);
        plain.Run(2000);
        changed.Run(2000, new Command(0, 0, CommandKind.SetLeaning, 2, 100, 0));
        Assert.NotEqual(Hash(plain.Research), Hash(changed.Research));
    }
}

public class ResearchVisibleChangeTests
{
    [Fact]
    public void Every_numeric_project_announces_a_visible_change_when_it_completes()
    {
        var rig = new ResearchRig();
        ResearchCatalog catalog = rig.Catalog;
        var numeric = catalog.Projects.Where(p => p.Numeric).ToList();
        Assert.Equal(148, numeric.Count);
        foreach (ProjectDef p in numeric) rig.Do(rig.Grant(0, p.Id));

        var missing = new List<string>();
        foreach (ProjectDef p in numeric)
        {
            ResearchEvent[] mine = rig.Research.Events.Where(e => e.Project == p.Index).ToArray();
            ResearchEvent done = mine.Single(e => e.Kind == ResearchEventKind.Completed);
            bool moment = mine.Any(e => e.Kind == ResearchEventKind.Moment);
            bool visible = mine.Any(e => e.Kind == ResearchEventKind.VisibleChange && e.Tick - done.Tick <= ResearchSystem.SeasonTicks);
            if (!moment || !visible || p.Moment.Length == 0) missing.Add(p.Id);
        }
        Assert.True(missing.Count == 0, "No visible change for: " + string.Join(", ", missing));
    }
}

public class ResearchDormancyTests
{
    [Fact]
    public void A_completed_project_goes_Dormant_when_its_access_gate_is_lost_and_returns_with_access()
    {
        // No shipped E0 to E2 project has a [Has] gate yet, so give Fire Keeping one in a modified copy of the content.
        var files = ContentFixture.Files();
        int i = files.FindIndex(f => f.Path == "catalog/fir-fire-heat.yaml");
        string text = files[i].Text;
        const string anchor = "    requires: []\n    effects:\n      - {kind: unlock_action, action: keep_fire}";
        Assert.Contains(anchor, text);
        files[i] = (files[i].Path, text.Replace(anchor, "    requires: []\n    gates: [{kind: has, target: Tin}]\n    effects:\n      - {kind: unlock_action, action: keep_fire}"));
        (CivSim.Content.ContentSet set, CivSim.Content.Report report) = ContentFixture.Load(files);
        CivSim.Sim.Content.ContentPack? pack = CivSim.Content.PackBuilder.Build(set, report);
        Assert.True(pack is not null, ContentFixture.Describe(report));
        var catalog = new ResearchCatalog(pack!);

        var rig = new ResearchRig(catalog: catalog);
        int fire = rig.P("FIR-01");
        Assert.Equal(ProjectState.Available, rig.Research.State(0, fire)); // a [Has] gate blocks use, not research
        rig.Do(rig.Grant(0, "FIR-01"));
        Assert.Equal(ProjectState.Completed, rig.Research.State(0, fire));
        int tin = catalog.GateTargetIndex("Tin");
        rig.Research.SetAccess(0, tin, available: false);
        Assert.Equal(ProjectState.Dormant, rig.Research.State(0, fire));
        Assert.Contains(rig.Events(ResearchEventKind.DormantChanged, "FIR-01"), e => e.A == 1);
        rig.Research.SetAccess(0, tin, available: true);
        Assert.Equal(ProjectState.Completed, rig.Research.State(0, fire));
        Assert.Contains(rig.Events(ResearchEventKind.DormantChanged, "FIR-01"), e => e.A == 0);
    }
}
