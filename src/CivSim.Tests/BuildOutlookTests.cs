using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using Xunit.Abstractions;

namespace CivSim.Tests;

/// <summary>The "what is this town waiting on" readout (docs/MVP/30): it reads the planner's own rules and changes nothing.</summary>
public sealed class BuildOutlookTests
{
    private const ulong Seed = 20261019;
    private readonly ITestOutputHelper _output;

    public BuildOutlookTests(ITestOutputHelper output) => _output = output;

    private static BronzeFixture Bronze(params string[] extra)
        => BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, extra);

    private static void Foundation(LivingEconomyModel e)
    {
        e.FixtureSetTier(0, 0);
        foreach (string id in new[] { "storage_pit", "drying_rack", "hearth", "lean_to", "pit_house" })
            if (e.StructureCount(0, LivingEconomyModel.StructureIndex(id)) == 0) e.FixtureBuildStructure(0, id);
        e.FixtureFillFields(0);
    }

    private static BuildItem Item(BuildOutlook o, BuildTrack track, string id) => o.Items.First(i => i.Track == track && i.Id == id);

    private static void Empty(LivingEconomyModel e, string good) => e.FixtureStock(0, good, -e.GoodStock(0, good));

    [Fact]
    public void A_short_good_is_named_with_what_the_town_holds_and_what_it_takes()
    {
        BronzeFixture f = Bronze();
        LivingEconomyModel e = f.Economy;
        Foundation(e);
        BuildItem granary = Item(e.BuildOutlookFor(0), BuildTrack.Basic, "granary");
        Assert.Equal(BuildStatus.Short, granary.Status);
        Assert.Equal(BuildCause.Materials, granary.Gate.Cause);
        BuildNeed brick = Assert.Single(granary.Needs);
        Assert.Equal("G-BRICK", LivingEconomyModel.StoreSlotId(brick.Slot));
        Assert.Equal(Fix64.FromInt(80), brick.Need);
        Assert.Equal(Fix64.Zero, brick.Have);
        Assert.False(granary.CanFocus); // a foundation structure: the planner already puts it first
        Assert.Equal(BuildWhy.AlreadyFirstInLine, granary.FocusWhy);
    }

    [Fact]
    public void The_basic_pick_is_the_planners_own_choice_in_every_stock_state()
    {
        BronzeFixture f = Bronze();
        LivingEconomyModel e = f.Economy;
        Foundation(e);
        string[] goods = { "G-WOOD", "G-STONE", "G-CLAY", "G-HIDE", "G-SAPLING", "G-BRICK" };
        for (int mask = 0; mask < 64; mask += 5)
        {
            for (int g = 0; g < goods.Length; g++)
            {
                Fix64 want = (mask >> g & 1) == 0 ? Fix64.Zero : Fix64.FromInt(30);
                e.FixtureStock(0, goods[g], want - e.GoodStock(0, goods[g]));
            }
            int pick = e.FixtureSelectBuildCandidate(0);
            BuildItem? current = e.BuildOutlookFor(0).CurrentOf(BuildTrack.Basic);
            if (pick == LivingEconomyModel.FixtureStructureCount) Assert.Null(current);
            else Assert.Equal(LivingEconomyModel.StructureName(pick), current!.Id);
        }
    }

    [Fact]
    public void A_town_that_can_pay_for_nothing_is_waiting_on_materials_and_the_old_readout_agrees()
    {
        BronzeFixture f = Bronze();
        LivingEconomyModel e = f.Economy;
        Foundation(e);
        foreach (string g in new[] { "G-WOOD", "G-STONE", "G-CLAY", "G-HIDE", "G-SAPLING", "G-BRICK", "G-MEAT" }) Empty(e, g);
        BuildOutlook o = e.BuildOutlookFor(0);
        Assert.Null(o.CurrentOf(BuildTrack.Basic));
        Assert.Equal(BuildHeadline.WaitingOnMaterials, o.Headline);
        LivingEconomyModel.BuildBlockerReadout blocker = e.BuildBlocker(0);
        Assert.True(blocker.Any);
        BuildItem first = o.Items.First(i => i.Track == BuildTrack.Basic && i.Status == BuildStatus.Short);
        Assert.Equal(LivingEconomyModel.StructureIndex(first.Id), blocker.Structure);
    }

    [Fact]
    public void A_good_the_ground_cannot_give_is_unresolvable_and_the_only_levers_are_new_ground()
    {
        BronzeFixture f = Bronze();
        LivingEconomyModel e = f.Economy;
        Foundation(e);
        e.FixtureSetStructureCount(0, "hearth", 0);
        Empty(e, "G-STONE");
        e.FixtureSetRangeStone(0, Fix64.Zero);
        BuildItem hearth = Item(e.BuildOutlookFor(0), BuildTrack.Basic, "hearth");
        Assert.Equal(BuildStatus.Short, hearth.Status);
        Assert.Equal(SupplyKind.NoSource, hearth.Supplies[0].Kind);
        Assert.Equal(BuildVerdict.Unresolvable, hearth.Verdict);
        Assert.Contains(hearth.Levers, l => l.Kind == LeverKind.FoundNearby);
    }

    [Fact]
    public void A_good_that_needs_a_project_offers_that_research()
    {
        // A Temple wants ten litres of cut stone, and dressing stone (STN-11) is a project this civilization has not learned.
        BronzeFixture f = BronzeFixture.CreateIron(TestPack.Pack, Seed, 60, "BEL-06");
        LivingEconomyModel e = f.Economy;
        e.FixtureSetTier(0, 1);
        e.FixtureSetCivicBuildTarget(0, "temple");
        BuildOutlook o = e.BuildOutlookFor(0);
        BuildItem temple = Item(o, BuildTrack.Civic, "temple");
        Assert.Equal(BuildStatus.Current, temple.Status);
        int cut = Array.FindIndex(temple.Needs, n => LivingEconomyModel.StoreSlotId(n.Slot) == "G-ASHLAR");
        Assert.True(cut >= 0 && temple.Needs[cut].Short);
        Assert.Equal(SupplyKind.NeedsProject, temple.Supplies[cut].Kind);
        Assert.Equal("STN-11", temple.Supplies[cut].Project);
        Assert.Contains(temple.Levers, l => l is { Kind: LeverKind.Research, Project: "STN-11" });
        Assert.Equal(BuildVerdict.Resolvable, temple.Verdict);
        Assert.Equal(BuildHeadline.WaitingOnMaterials, o.Headline);
        Assert.Contains(temple.Levers, l => l.Kind == LeverKind.SkipThis);
    }

    [Fact]
    public void A_rule_that_stops_a_structure_is_named_and_the_structure_it_needs_first_is_offered()
    {
        BronzeFixture f = Bronze("CER-04");
        LivingEconomyModel e = f.Economy;
        Foundation(e);
        e.FixtureSetStructureCount(0, "kiln", 0);
        BuildItem potters = Item(e.BuildOutlookFor(0), BuildTrack.Basic, "potters_workshop");
        Assert.Equal(BuildStatus.Blocked, potters.Status);
        Assert.Equal(BuildCause.Requires, potters.Gate.Cause);
        Assert.Equal(BuildWhy.NeedsKiln, potters.Gate.Why);
        int kiln = LivingEconomyModel.FocusCode.Basic(LivingEconomyModel.StructureIndex("kiln"));
        Assert.Contains(potters.Levers, l => l is { Kind: LeverKind.FocusPrerequisite } && l.Arg == kiln);
        Assert.Equal(BuildVerdict.Resolvable, potters.Verdict);
        // A small town cannot raise a shrine, and nothing the player holds changes that.
        BronzeFixture small = BronzeFixture.Create(TestPack.Pack, Seed, 15, false, true, true, true, "BEL-05");
        BuildItem shrine = Item(small.Economy.BuildOutlookFor(0), BuildTrack.Basic, "shrine");
        Assert.Equal(BuildStatus.Blocked, shrine.Status);
        Assert.Equal(BuildCause.Size, shrine.Gate.Cause);
        Assert.Equal(20, shrine.Gate.Arg);
        Assert.Equal(BuildVerdict.Unresolvable, shrine.Verdict);
    }

    [Fact]
    public void Reading_the_outlook_changes_nothing_and_draws_no_random_numbers()
    {
        BronzeFixture f = Bronze();
        LivingEconomyModel e = f.Economy;
        Foundation(e);
        ulong before = e.ComputeChecksum();
        for (int i = 0; i < e.FixtureSettlementCount; i++) _ = e.BuildOutlookFor(i);
        _ = e.BuildBlocker(0);
        Assert.Equal(before, e.ComputeChecksum());
        // The state after a season is the same whether or not the outlook was read in between.
        BronzeFixture g = Bronze();
        Foundation(g.Economy);
        f.RunSeasons(2);
        _ = g.Economy.BuildOutlookFor(0);
        g.RunSeasons(1);
        _ = g.Economy.BuildOutlookFor(0);
        g.RunSeasons(1);
        Assert.Equal(f.Economy.ComputeChecksum(), g.Economy.ComputeChecksum());
    }

    [Fact]
    public void The_Iron_gates_open_exactly_where_the_original_expression_wanted_a_work()
    {
        BronzeFixture f = BronzeFixture.CreateIron(TestPack.Pack, Seed, 60, "DEF-05", "DEF-06", "DEF-07", "DEF-08", "MEC-06", "ART-08", "MED-06", "BEL-06",
            "BLD-10", "TRN-07", "TRN-08", "SEA-07", "WAR-07", "LRN-05", "MEC-05", "FOR-08", "NRG-03", "STN-12", "AGR-08", "MEC-04");
        LivingEconomyModel e = f.Economy;
        int checkedKinds = 0, wanted = 0;
        for (int tier = 0; tier <= 2; tier++)
            for (int walls = 0; walls <= 1; walls++)
                for (int temples = 0; temples <= 1; temples++)
                    for (int people = 0; people <= 60; people += 30)
                    {
                        e.FixtureSetTier(0, tier);
                        e.FixtureSetStructureCount(0, "stone_walls", walls);
                        e.FixtureSetTemples(0, temples);
                        e.FixtureAddPeople(0, people, 25);
                        for (int k = 0; k < e.FixtureIronKindCount; k++)
                        {
                            bool legacy = e.FixtureIronBuildWantedLegacy(0, k), gate = e.FixtureIronBuildWantedGate(0, k);
                            Assert.True(legacy == gate, $"iron kind {k} ({LivingEconomyModel.IronWorkId(k)}) tier {tier} walls {walls} temples {temples}: original {legacy}, gate {gate}");
                            checkedKinds++;
                            if (gate) wanted++;
                        }
                    }
        _output.WriteLine($"{checkedKinds} comparisons, {wanted} wanted");
        Assert.True(wanted > 20, "the matrix has to reach the interesting cases");
    }
}
