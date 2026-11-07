using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>The unlocks that did nothing (docs/MVP/29): totem, marker, shrine, signal post and potter's workshop.</summary>
public sealed class EarlyUnlockTests
{
    private const ulong Seed = 20261019;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static BronzeFixture Fixture(params string[] extra)
        => BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, extra);

    private static void Raise(LivingEconomyModel e, int settlement, string structure)
    {
        e.FixtureSetBuildTarget(settlement, structure);
        e.FixtureSetBuildProgress(settlement, Fix64.FromInt(20));
        e.FixtureBuildIfReady(settlement);
    }

    [Fact]
    public void A_totem_is_wanted_once_and_steadies_its_own_camp_when_raised()
    {
        BronzeFixture f = Fixture("BEL-02");
        LivingEconomyModel e = f.Economy;
        e.FixtureStock(0, "G-WOOD", Fix64.FromInt(5));
        int totem = LivingEconomyModel.StructureIndex("totem");
        Assert.True(e.FixtureEarlyWorkWanted(0, "totem"));
        e.FixtureSetStoredCohesion(0, Fix64.One);
        Fix64 before = e.FixtureStoredCohesion(0);
        Raise(e, 0, "totem");
        Assert.Equal(1, e.StructureCount(0, totem));
        Assert.Equal(before + Fix64.FromRatio(2, 100), e.FixtureStoredCohesion(0));
        Assert.False(e.FixtureEarlyWorkWanted(0, "totem")); // one to a camp
    }

    [Fact]
    public void A_settlement_raises_up_to_two_markers_from_stone_or_wood()
    {
        BronzeFixture f = Fixture("TER-03");
        LivingEconomyModel e = f.Economy;
        e.FixtureStock(0, "G-STONE", Fix64.FromInt(3));
        int marker = LivingEconomyModel.StructureIndex("marker");
        Raise(e, 0, "marker");
        Raise(e, 0, "marker");
        Assert.Equal(2, e.StructureCount(0, marker));
        Assert.False(e.FixtureEarlyWorkWanted(0, "marker"));
    }

    [Fact]
    public void A_shrine_keeps_six_tiles_from_the_next_and_extends_the_BEL05_cohesion_term()
    {
        BronzeFixture f = Fixture("BEL-05");
        LivingEconomyModel e = f.Economy;
        int rival = Enumerable.Range(1, e.FixtureSettlementCount - 1).First(i => e.FixtureCivOf(i) != e.FixtureCivOf(0));
        e.FixtureSetCivilization(rival, e.FixtureCivOf(0)); // a second settlement of the same civilization
        e.FixtureAddPeople(rival, 40, 25);
        e.FixtureAddPeople(0, 40, 25);
        e.FixtureStock(rival, "G-STONE", Fix64.FromInt(8));
        e.FixtureStock(0, "G-STONE", Fix64.FromInt(8));
        int site = e.SettlementLite(0).SiteTile;
        e.FixtureMoveSite(rival, site + 4);
        Raise(e, 0, "shrine");
        Assert.False(e.FixtureEarlyWorkWanted(rival, "shrine"), "four tiles from a shrine of the same civilization");
        e.FixtureMoveSite(rival, site + 7);
        Assert.True(e.FixtureEarlyWorkWanted(rival, "shrine"));
        // Far from the capital, a shrine brings the +3 of BEL-05 to the settlement beside it.
        e.FixtureMoveSite(rival, site + 25);
        e.FixtureSetTier(rival, 1);
        int far = e.FixtureCohesionTarget(rival);
        e.FixtureBuildStructure(rival, "shrine");
        Assert.Equal(far + 3, e.FixtureCohesionTarget(rival));
    }

    [Fact]
    public void A_potters_workshop_needs_a_kiln_and_cuts_stored_food_spoilage_by_a_fifth()
    {
        BronzeFixture f = Fixture("CER-04");
        LivingEconomyModel e = f.Economy;
        e.FixtureAddPeople(0, 40, 25);
        e.FixtureStock(0, "G-WOOD", Fix64.FromInt(5));
        e.FixtureStock(0, "G-CLAY", Fix64.FromInt(5));
        e.FixtureSetStructureCount(0, "kiln", 0);
        Assert.False(e.FixtureEarlyWorkWanted(0, "potters_workshop")); // no kiln
        e.FixtureBuildStructure(0, "kiln");
        Assert.True(e.FixtureEarlyWorkWanted(0, "potters_workshop"));
        Fix64 before = e.FixtureStorageFactor(0);
        Raise(e, 0, "potters_workshop");
        Assert.Equal(before * Fix64.FromRatio(8, 10), e.FixtureStorageFactor(0));
    }

    private static MatchModel Match(out SimRunner runner)
    {
        MatchModel match = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        runner = new SimRunner(match);
        return match;
    }

    private static void Grant(SimRunner runner, int civ, params string[] projects)
    {
        foreach (string id in projects)
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, TestPack.Catalog.IndexOf(id), 0, 0));
        runner.Advance(1);
    }

    [Fact]
    public void Signal_fires_need_a_post_and_lift_the_surprised_share_only_for_a_surprise()
    {
        MatchModel match = Match(out SimRunner runner);
        Grant(runner, 0, "COM-04");
        int home = Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == 0 && !match.Politics.IsFreeFolk(s));
        Assert.False(match.Politics.FixtureDefensesOf(home).SignalFires, "the research alone lights nothing");
        match.Economy.FixtureBuildStructure(home, "signal_post");
        Assert.True(match.Politics.FixtureDefensesOf(home).SignalFires);
        Assert.Equal(Fix64.FromRatio(484, 1000), Raids.MobilizedShare(surprised: true, signalFires: true));
        Assert.Equal(Raids.SurprisedShare, Raids.MobilizedShare(surprised: true, signalFires: false));
        Assert.Equal(Raids.AlertShare, Raids.MobilizedShare(surprised: false, signalFires: true)); // an alerted raid is unchanged
        // The post does not warn another civilization.
        int other = Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == 1 && !match.Politics.IsFreeFolk(s));
        Grant(runner, 1, "COM-04");
        Assert.False(match.Politics.FixtureDefensesOf(other).SignalFires);
    }

    [Fact]
    public void A_totem_and_a_shrine_each_add_a_claim_source_at_home()
    {
        MatchModel match = Match(out SimRunner runner);
        int home = Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == 0 && !match.Politics.IsFreeFolk(s));
        int before = match.Politics.FixtureSettlementClaimSourceCount();
        match.Economy.FixtureBuildStructure(home, "totem");
        Assert.Equal(before + 1, match.Politics.FixtureSettlementClaimSourceCount());
        match.Economy.FixtureBuildStructure(home, "shrine");
        Assert.Equal(before + 2, match.Politics.FixtureSettlementClaimSourceCount());
    }

    [Fact]
    public void Markers_make_the_first_camps_claim_thirty_percent_more_and_a_marker_without_camps_claims_from_home()
    {
        MatchModel match = Match(out SimRunner runner);
        Grant(runner, 0, "WAY-02", "TER-02");
        runner.Advance(600); // camps are placed; TER-03 is not known yet, so no town has raised a marker of its own
        int home = Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == 0 && !match.Politics.IsFreeFolk(s) && match.Politics.FixtureCampCountOf(s) > 0);
        var before = match.Politics.FixtureCampClaimSources();
        Assert.Equal(0, match.Economy.StructureCount(home, LivingEconomyModel.StructureIndex("marker")));
        match.Economy.FixtureBuildStructure(home, "marker");
        var after = match.Politics.FixtureCampClaimSources();
        Assert.Equal(before.Count, after.Count);
        int raised = 0;
        for (int i = 0; i < before.Count; i++)
            if (after[i].Emission != before[i].Emission) { raised++; Assert.Equal(before[i].Emission * Fix64.FromRatio(13, 10), after[i].Emission); }
        Assert.True(before.Count > 0, "camps exist");
        Assert.Equal(1, raised); // one marker lifts one camp
    }
}
