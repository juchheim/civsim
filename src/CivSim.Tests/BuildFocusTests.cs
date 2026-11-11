using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.View;
using Xunit.Abstractions;

namespace CivSim.Tests;

/// <summary>The player's build focus (docs/MVP/30): the command, the planners' reading of it, and its limits.</summary>
public sealed class BuildFocusTests
{
    private const ulong Seed = 20261019;
    private readonly ITestOutputHelper _output;

    public BuildFocusTests(ITestOutputHelper output) => _output = output;

    private static BronzeFixture Bronze(params string[] extra)
        => BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, extra);

    private static int Kind(string id) => LivingEconomyModel.StructureIndex(id);

    private static int BasicCode(string id) => LivingEconomyModel.FocusCode.Basic(Kind(id));

    /// <summary>Builds the foundation structures and fields so the planner's next choice comes from the later rules.</summary>
    private static void Foundation(LivingEconomyModel e)
    {
        e.FixtureSetTier(0, 0); // gives the settlement its civic state, which the planner reads (no extra lean-to)
        foreach (string id in new[] { "storage_pit", "drying_rack", "hearth", "lean_to", "pit_house" })
            if (e.StructureCount(0, Kind(id)) == 0) e.FixtureBuildStructure(0, id);
        e.FixtureFillFields(0);
    }

    private static string PickName(LivingEconomyModel e)
    {
        int pick = e.FixtureSelectBuildCandidate(0);
        return pick == LivingEconomyModel.FixtureStructureCount ? "" : LivingEconomyModel.StructureName(pick);
    }

    [Fact]
    public void The_planner_goes_by_its_own_order_without_a_focus_and_by_the_focus_once_the_foundation_is_laid()
    {
        BronzeFixture f = Bronze();
        LivingEconomyModel e = f.Economy;
        Foundation(e);
        string natural = PickName(e);
        _output.WriteLine($"natural pick: {natural}");
        Assert.NotEqual("", natural);
        // The latest structure in the planner's order that is affordable: asking for it must put it first.
        BuildOutlook before = e.BuildOutlookFor(0);
        BuildItem choice = before.Items.Last(i => i.Track == BuildTrack.Basic && i.CanFocus && !i.AnyShort && i.Id != natural);
        _output.WriteLine($"asking for {choice.Id}");
        f.Apply(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, choice.Code));
        Assert.Equal(choice.Code, e.BuildFocusAt(0, out bool skip));
        Assert.False(skip);
        Assert.Equal(choice.Id, PickName(e));
        // Clearing it puts the planner back in charge.
        f.Apply(PlayerCommands.ClearBuildFocus(f.Tick, 0, 0));
        Assert.Equal(LivingEconomyModel.FocusCode.None, e.BuildFocusAt(0, out _));
        Assert.Equal(natural, PickName(e));
    }

    [Fact]
    public void A_focus_never_goes_ahead_of_shelter_warmth_storage_or_the_first_fields()
    {
        BronzeFixture f = Bronze();
        LivingEconomyModel e = f.Economy;
        Foundation(e);
        e.FixtureSetStructureCount(0, "hearth", 0); // the hearth is a foundation rule and stone is in store
        Assert.Equal("hearth", PickName(e));
        BuildOutlook o = e.BuildOutlookFor(0);
        BuildItem lookout = o.Items.First(i => i.Id == "lookout_post");
        f.Apply(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, lookout.Code));
        Assert.Equal(lookout.Code, e.BuildFocusAt(0, out _)); // accepted...
        Assert.Equal("hearth", PickName(e));                  // ...but the hearth still comes first
        e.FixtureBuildStructure(0, "hearth");
        Assert.Equal("lookout_post", PickName(e));
    }

    [Fact]
    public void A_focus_the_town_cannot_pay_for_leaves_the_planner_to_build_something_else()
    {
        BronzeFixture f = Bronze();
        LivingEconomyModel e = f.Economy;
        Foundation(e);
        string natural = PickName(e);
        BuildItem unaffordable = e.BuildOutlookFor(0).Items.First(i => i.Track == BuildTrack.Basic && i.CanFocus && i.AnyShort);
        f.Apply(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, unaffordable.Code));
        Assert.Equal(unaffordable.Code, e.BuildFocusAt(0, out _));
        Assert.Equal(natural, PickName(e)); // it holds neither materials nor labor
    }

    [Fact]
    public void Orders_the_rules_refuse_change_nothing()
    {
        BronzeFixture f = Bronze();
        LivingEconomyModel e = f.Economy;
        Foundation(e);
        int none = LivingEconomyModel.FocusCode.None;
        void Refused(Command c, string why)
        {
            f.Apply(c);
            Assert.True(e.BuildFocusAt(0, out _) == none, why);
        }
        Refused(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, 999), "no such structure");
        Refused(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, BasicCode("granary")), "the granary is a foundation structure the planner already puts first");
        Refused(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, BasicCode("kiln")), "the kiln stands already");
        Refused(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, BasicCode("potters_workshop")), "its project is not learned");
        Refused(PlayerCommands.SetBuildFocus(f.Tick, 1, 0, BasicCode("quern")), "another civilization's order");
        Refused(PlayerCommands.SetBuildFocus(f.Tick, 0, 9999, BasicCode("quern")), "no such settlement");
        Refused(PlayerCommands.SkipBuild(f.Tick, 0, 0, BasicCode("quern")), "the basic list is not skipped, only redirected");
        Refused(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, LivingEconomyModel.FocusCode.Civic(1)), "no civic building is learned");
        Refused(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, LivingEconomyModel.FocusCode.Iron(0)), "a Bronze economy has no Iron works");
        // The same order for something allowed is taken.
        f.Apply(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, BasicCode("quern")));
        Assert.Equal(BasicCode("quern"), e.BuildFocusAt(0, out _));
        // A refused order does not disturb the focus already set.
        f.Apply(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, BasicCode("kiln")));
        Assert.Equal(BasicCode("quern"), e.BuildFocusAt(0, out _));
    }

    [Fact]
    public void A_focus_ends_when_the_structure_stands()
    {
        BronzeFixture f = Bronze();
        LivingEconomyModel e = f.Economy;
        Foundation(e);
        f.Economy.FixtureStock(0, "G-WOOD", Fix64.FromInt(10));
        f.Apply(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, BasicCode("lookout_post")));
        Assert.Equal(0, e.StructureCount(0, Kind("lookout_post")));
        f.RunSeasons(6);
        Assert.Equal(1, e.StructureCount(0, Kind("lookout_post")));
        Assert.Equal(LivingEconomyModel.FocusCode.None, e.BuildFocusAt(0, out _));
    }

    [Fact]
    public void A_focus_lapses_after_its_time_even_if_nothing_was_built()
    {
        // A shrine needs four litres of stone, and this town has none in store or in reach, so it can never be paid for.
        BronzeFixture f = Bronze("BEL-05");
        LivingEconomyModel e = f.Economy;
        Foundation(e);
        e.FixtureStock(0, "G-STONE", -e.GoodStock(0, "G-STONE"));
        e.FixtureSetRangeStone(0, Fix64.Zero);
        e.FixtureSetReachStone(0, Fix64.Zero);
        f.Apply(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, BasicCode("shrine")));
        (int since, int until) = e.BuildFocusWindow(0);
        Assert.Equal(since + LivingEconomyModel.BuildFocusSeasons, until);
        f.RunSeasons(LivingEconomyModel.BuildFocusSeasons - 2);
        Assert.Equal(BasicCode("shrine"), e.BuildFocusAt(0, out _));
        f.RunSeasons(3);
        Assert.Equal(LivingEconomyModel.FocusCode.None, e.BuildFocusAt(0, out _));
        Assert.Equal(0, e.StructureCount(0, Kind("shrine")));
    }

    [Fact]
    public void A_focus_does_not_pass_to_a_new_owner_of_the_settlement()
    {
        BronzeFixture f = Bronze();
        LivingEconomyModel e = f.Economy;
        Foundation(e);
        f.Apply(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, BasicCode("quern")));
        Assert.Equal(BasicCode("quern"), e.BuildFocusAt(0, out _));
        e.FixtureSetCivilization(0, 1);
        Assert.Equal(LivingEconomyModel.FocusCode.None, e.BuildFocusAt(0, out _)); // another civilization's town shows none of it
        f.RunSeasons(1);
        e.FixtureSetCivilization(0, 0);
        Assert.Equal(LivingEconomyModel.FocusCode.None, e.BuildFocusAt(0, out _)); // and it was cleared, not merely hidden
    }

    [Fact]
    public void A_focus_for_timber_sends_woodcutters_for_it()
    {
        // Two identical towns that are short of wood for a pit house (4 L); one has asked for it.
        // Since 16b4c01 the town's own planner also sends woodcutters for whatever it wants next (so the old longhouse setup, 12 L,
        // saturated the town's chopping labor either way and the two woodpiles came out equal). The pit house is asked for because it
        // raises the timber request above the planner's own (4 L against 2 L), and the requests themselves are checked directly too.
        static BronzeFixture Short()
        {
            BronzeFixture f = Bronze();
            LivingEconomyModel e = f.Economy;
            Foundation(e);
            e.FixtureStock(0, "G-WOOD", -e.FixtureWood(0) + Fix64.FromInt(1));
            e.FixtureStock(0, "G-CLAY", Fix64.FromInt(8));
            return f;
        }
        BronzeFixture plain = Short(), asked = Short();
        Assert.Equal(Fix64.Zero, asked.Economy.FixtureBuildFocusWoodNeed(0));
        asked.Apply(PlayerCommands.SetBuildFocus(asked.Tick, 0, 0, BasicCode("pit_house")));
        plain.Apply(new Command(plain.Tick, 0, CommandKind.None, 0, 0, 0)); // keep the two on the same tick
        Fix64 ask = asked.Economy.FixtureBuildFocusWoodNeed(0);
        _output.WriteLine($"focus asks {ask.ToDoubleForDisplay():F1} L; planner alone asks {plain.Economy.FixturePlannerWoodNeed(0).ToDoubleForDisplay():F1} L");
        Assert.Equal(Fix64.FromInt(4), ask);
        Assert.Equal(Fix64.Zero, plain.Economy.FixtureBuildFocusWoodNeed(0));
        // The ask reaches the town's timber procurement and the woodcutters bring in more than the planner alone sent them for.
        Assert.Equal(ask, asked.Economy.FixturePlannerWoodNeed(0));
        plain.RunSeasons(2); asked.RunSeasons(2);
        _output.WriteLine($"wood plain {plain.Economy.FixtureWood(0).ToDoubleForDisplay():F1} asked {asked.Economy.FixtureWood(0).ToDoubleForDisplay():F1}");
        Assert.True(asked.Economy.FixtureWood(0) > plain.Economy.FixtureWood(0));
    }

    [Fact]
    public void An_order_in_a_replay_and_a_snapshot_give_the_same_town()
    {
        // The same orders run twice, and once with a save and restore in the middle, end on the same checksum.
        static ulong Play(bool saveAndRestore)
        {
            BronzeFixture f = Bronze();
            LivingEconomyModel e = f.Economy;
            Foundation(e);
            f.Economy.FixtureStock(0, "G-WOOD", Fix64.FromInt(10));
            f.Apply(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, BasicCode("thorn_barrier")));
            f.RunSeasons(1);
            if (saveAndRestore)
            {
                using var stream = new MemoryStream();
                using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) e.WriteSnapshot(w);
                stream.Position = 0;
                using var r = new BinaryReader(stream);
                e.ReadSnapshot(r);
            }
            f.Apply(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, BasicCode("quern")));
            f.RunSeasons(3);
            return e.ComputeChecksum();
        }
        ulong a = Play(false), b = Play(false), c = Play(true);
        Assert.Equal(a, b);
        Assert.Equal(a, c);
    }

    [Fact]
    public void Never_issuing_the_command_leaves_the_checksum_alone_and_clearing_it_restores_it()
    {
        // Reading the outlook and asking what could be built changes nothing; a focus that is set and cleared leaves no trace in the hash.
        BronzeFixture plain = Bronze(), watched = Bronze();
        Foundation(plain.Economy); Foundation(watched.Economy);
        ulong start = plain.Economy.ComputeChecksum();
        Assert.Equal(start, watched.Economy.ComputeChecksum());
        for (int i = 0; i < 3; i++)
        {
            _ = watched.Economy.BuildOutlookFor(0);
            Assert.Equal(start, watched.Economy.ComputeChecksum());
        }
        watched.Apply(PlayerCommands.SetBuildFocus(watched.Tick, 0, 0, BasicCode("quern")));
        plain.Apply(new Command(plain.Tick, 0, CommandKind.None, 0, 0, 0));
        Assert.NotEqual(plain.Economy.ComputeChecksum(), watched.Economy.ComputeChecksum()); // the focus is state
        watched.Apply(PlayerCommands.ClearBuildFocus(watched.Tick, 0, 0));
        plain.Apply(new Command(plain.Tick, 0, CommandKind.None, 0, 0, 0));
        Assert.Equal(plain.Economy.ComputeChecksum(), watched.Economy.ComputeChecksum());
    }

    // ---- Iron works and civic buildings ------------------------------------------------------------------

    private static BronzeFixture IronTown(params string[] more)
    {
        BronzeFixture f = BronzeFixture.CreateIron(TestPack.Pack, Seed, 60, new[] { "DEF-05", "DEF-06", "DEF-07", "DEF-08", "MEC-06", "ART-08", "MED-06",
            "MIN-03", "STN-11", "MET-09", "BEL-06", "TRD-04", "LRN-04", "FOD-08", "AGR-02" }.Concat(more).ToArray());
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureSetStructureCount(0, "stone_walls", 1);
        return f;
    }

    [Fact]
    public void The_Iron_slot_goes_to_the_works_the_player_asked_for_and_shelves_what_it_replaces()
    {
        BronzeFixture f = IronTown();
        LivingEconomyModel e = f.Economy;
        f.RunSeasons(2);
        int current = e.FixtureIronBuildKind(0);
        Assert.True(current >= 0, "the town starts something");
        BuildItem other = e.BuildOutlookFor(0).Items.First(i => i.Track == BuildTrack.Iron && i.CanFocus && i.Kind != current);
        _output.WriteLine($"current {LivingEconomyModel.IronWorkId(current)}, asking for {other.Id}");
        f.Apply(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, other.Code));
        f.RunSeasons(1);
        Assert.Equal(other.Kind, e.FixtureIronBuildKind(0));
        // Skipping the work it is on lets the slot go to the next wanted one, and the focus is the skip.
        f.Apply(PlayerCommands.SkipBuild(f.Tick, 0, 0, other.Code));
        f.RunSeasons(1);
        Assert.NotEqual(other.Kind, e.FixtureIronBuildKind(0));
        Assert.Equal(other.Code, e.BuildFocusAt(0, out bool skip));
        Assert.True(skip);
    }

    [Fact]
    public void The_civic_slot_switches_to_the_building_asked_for_and_a_Wonder_cannot_be_asked_for()
    {
        BronzeFixture f = IronTown();
        LivingEconomyModel e = f.Economy;
        e.FixtureSetTemples(0, 1); // the town already has the Temple, so no City path is open and the civic slot is free to go where the player asks
        f.RunSeasons(1);
        e.FixtureSetCivicBuildTarget(0, "market");
        BuildItem school = e.BuildOutlookFor(0).Items.First(i => i.Track == BuildTrack.Civic && i.Id == "school");
        Assert.True(school.CanFocus, school.FocusWhy.ToString());
        f.Apply(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, school.Code));
        f.RunSeasons(1);
        Assert.Equal(school.Kind, e.FixtureCivicBuildKind(0));
        BuildItem ziggurat = e.BuildOutlookFor(0).Items.FirstOrDefault(i => i.Track == BuildTrack.Civic && i.Id == "ziggurat")!;
        if (ziggurat is not null) Assert.False(ziggurat.CanFocus);
    }

    [Fact]
    public void The_Temple_that_makes_a_Town_a_City_comes_before_anything_the_player_asks_for()
    {
        BronzeFixture f = IronTown();
        LivingEconomyModel e = f.Economy;
        f.RunSeasons(1);
        BuildItem school = e.BuildOutlookFor(0).Items.First(i => i.Track == BuildTrack.Civic && i.Id == "school");
        Assert.False(school.CanFocus);
        Assert.Equal(BuildWhy.OnTheWayToCity, school.FocusWhy);
        f.Apply(PlayerCommands.SetBuildFocus(f.Tick, 0, 0, school.Code));
        Assert.Equal(LivingEconomyModel.FocusCode.None, e.BuildFocusAt(0, out _)); // refused
    }
}
