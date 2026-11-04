using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Coinage, route guides, long-distance routes and harbors in the Iron chapter (docs/MVP/25 §8).</summary>
public sealed class IronSeaTradeTests
{
    private const ulong Seed = 20261024;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric("KEY-4", "TRD-06", "WAY-06", "TRD-07", "HUS-09", "SEA-07", "SEA-09", "WAR-07"));
    private static readonly Lazy<ResearchCatalog> Catalog = new(() => new ResearchCatalog(Pack.Value));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner) Rig(ChapterRules rules)
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules);
        return (match, new SimRunner(match));
    }

    private static void Grant(SimRunner runner, int civ, params string[] projects)
    {
        foreach (string project in projects)
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, Catalog.Value.IndexOf(project), 0, 0));
        runner.Advance(1);
    }

    [Fact]
    public void Coinage_and_route_guides_cut_the_margin_a_trade_needs_and_only_in_the_Iron_chapter()
    {
        (MatchModel iron, SimRunner ironRunner) = Rig(ChapterRules.Iron);
        Fix64 plain = iron.Politics.FixtureGainFor(0);
        Grant(ironRunner, 0, "KEY-4", "TRD-06");
        Assert.Equal(plain * Fix64.FromRatio(6, 10), iron.Politics.FixtureGainFor(0));
        Grant(ironRunner, 0, "WAY-06");
        Near(plain * Fix64.FromRatio(6, 10) / Fix64.FromRatio(125, 100), iron.Politics.FixtureGainFor(0));
        Assert.Equal(plain, iron.Politics.FixtureGainFor(1)); // a civilization without them is unchanged

        (MatchModel bronze, SimRunner bronzeRunner) = Rig(ChapterRules.Bronze);
        Fix64 bronzePlain = bronze.Politics.FixtureGainFor(0);
        Grant(bronzeRunner, 0, "KEY-4", "TRD-06", "WAY-06");
        Assert.Equal(bronzePlain, bronze.Politics.FixtureGainFor(0));
    }

    [Fact]
    public void Long_routes_triple_a_caravans_range_only_in_a_mode_the_civilization_has()
    {
        (MatchModel match, SimRunner runner) = Rig(ChapterRules.Iron);
        int a = Settlement(match, 0), b = Settlement(match, 1);
        bool walkable = match.Politics.SeaTilesOnRoute(0, match.Economy.SettlementLite(a).SiteTile, match.Economy.SettlementLite(b).SiteTile) == 0;
        Assert.True(walkable, "the fixture world joins the two start towns by land");
        Assert.Equal(30, match.Politics.FixtureCaravanTiles(0, a, b, 30)); // nothing yet
        Grant(runner, 0, "KEY-4", "TRD-06");
        Assert.Equal(30, match.Politics.FixtureCaravanTiles(0, a, b, 30)); // coinage alone is not a longer route
        Grant(runner, 0, "TRD-07");
        Assert.Equal(30, match.Politics.FixtureCaravanTiles(0, a, b, 30)); // no pack animals, no harbors: no available mode
        Grant(runner, 0, "SEA-07");
        Assert.Equal(30, match.Politics.FixtureCaravanTiles(0, a, b, 30)); // a harbor mode does not help an overland route
        Grant(runner, 0, "HUS-09");
        Assert.Equal(10, match.Politics.FixtureCaravanTiles(0, a, b, 30)); // pack animals on a walked route: range x3
        Assert.Equal(11, match.Politics.FixtureCaravanTiles(0, a, b, 31)); // rounds up, never to zero
        Assert.Equal(1, match.Politics.FixtureCaravanTiles(0, a, b, 1));
        Assert.Equal(30, match.Politics.FixtureCaravanTiles(1, a, b, 30)); // another civilization's caravans are unchanged

        (MatchModel bronze, SimRunner bronzeRunner) = Rig(ChapterRules.Bronze);
        Grant(bronzeRunner, 0, "KEY-4", "TRD-07", "HUS-09");
        Assert.Equal(30, bronze.Politics.FixtureCaravanTiles(0, Settlement(bronze, 0), Settlement(bronze, 1), 30));
    }

    [Fact]
    public void A_harbor_is_for_a_coast_town_and_lifts_its_group_cap_by_a_fifth()
    {
        BronzeFixture f = BronzeFixture.CreateIron(TestPack.Pack, Seed, 60, "KEY-4", "SEA-07");
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureAddPeople(0, 30, 25);
        int inland = f.Economy.FixtureLandTile(coast: false), coast = f.Economy.FixtureLandTile(coast: true);
        Assert.True(inland >= 0 && coast >= 0);
        f.Economy.FixtureMoveSite(0, inland);
        Assert.False(f.Economy.FixtureIronBuildWanted(0, "harbor"));
        f.Economy.FixtureMoveSite(0, coast);
        Assert.True(f.Economy.FixtureIronBuildWanted(0, "harbor"));
        Fix64 before = f.Economy.FixtureGroupCap(0);
        Assert.False(f.Economy.HasHarbor(0));
        f.Economy.FixtureBuildIronStructure(0, "harbor");
        Assert.True(f.Economy.HasHarbor(0));
        Assert.False(f.Economy.FixtureIronBuildWanted(0, "harbor"));
        Near(before * Fix64.FromRatio(12, 10), f.Economy.FixtureGroupCap(0));

        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, Seed, 60, true, true, true, true, false, "KEY-4");
        Fix64 bronzeBefore = bronze.Economy.FixtureGroupCap(0);
        bronze.Economy.FixtureBuildIronStructure(0, "harbor");
        Assert.False(bronze.Economy.HasHarbor(0));
        Assert.Equal(bronzeBefore, bronze.Economy.FixtureGroupCap(0));
    }

    /// <summary>Civilization 0 with a harbor and a crewed galley, civilization 2 across the sea with a harbor, at war and in contact.</summary>
    private static (MatchModel Match, SimRunner Runner, int Home, int Target) BlockadeRig(bool galley = true, bool targetHarbor = true)
    {
        (MatchModel match, SimRunner runner) = Rig(ChapterRules.Iron);
        Grant(runner, 0, "KEY-4", "SEA-04", "SEA-07", "SEA-09", "WAR-07", "TRD-07");
        Grant(runner, 2, "KEY-4", "SEA-07");
        int home = Settlement(match, 0), target = Settlement(match, 2);
        match.Economy.FixtureBuildIronStructure(home, "harbor");
        if (galley) match.Economy.FixtureBuildIronStructure(home, "galley");
        match.Economy.FixtureSetSoldiersSupplied(home, 4);
        if (targetHarbor) match.Economy.FixtureBuildIronStructure(target, "harbor");
        match.Politics.SetExternalMind(0, true);
        match.Politics.SetExternalMind(2, true);
        match.Politics.ForceContact(0, 2);
        match.Politics.FixtureReveal(0, match.Economy.SettlementLite(target).SiteTile); // a ship has sighted the harbor
        match.Politics.SetWar(0, 2, true);
        return (match, runner, home, target);
    }

    [Fact]
    public void A_blockade_needs_a_crewed_galley_a_war_and_a_harbor_and_one_ship_holds_one_port()
    {
        (MatchModel noShip, _, _, int t1) = BlockadeRig(galley: false);
        Assert.False(noShip.Politics.OrderBlockade(0, t1));
        (MatchModel noHarbor, _, _, int t2) = BlockadeRig(targetHarbor: false);
        Assert.False(noHarbor.Politics.OrderBlockade(0, t2));
        (MatchModel peace, _, _, int t3) = BlockadeRig();
        peace.Politics.SetWar(0, 2, false);
        Assert.False(peace.Politics.OrderBlockade(0, t3));

        (MatchModel match, _, int home, int target) = BlockadeRig();
        Assert.True(match.Economy.CrewedGalley(home));
        Assert.Null(match.Politics.BlockadeRefusal(0, target, out _));
        Assert.True(match.Politics.OrderBlockade(0, target));
        Blockade b = Assert.Single(match.Politics.Blockades);
        Assert.Equal((0, 2, target, home), (b.Civ, b.TargetCiv, b.Harbor, b.Origin));
        Assert.Contains(match.Politics.Events, e => e.Kind == PoliticsEventKind.BlockadeBegan && e.Civ == 0 && e.A == target);
        Assert.False(match.Politics.OrderBlockade(0, target), "the harbor is already blockaded");
        // Bronze: no galleys, no blockade.
        Assert.False(Rig(ChapterRules.Bronze).Match.Politics.OrderBlockade(0, Settlement(Rig(ChapterRules.Bronze).Match, 2)));
    }

    [Fact]
    public void A_blockade_shuts_the_harbors_sea_routes_and_nothing_else()
    {
        (MatchModel match, _, int home, int target) = BlockadeRig();
        int overland = Settlement(match, 1);
        Assert.False(match.Politics.FixtureBlockadeCutsRoute(home, target));
        Assert.Equal(14, match.Politics.FixtureCaravanTiles(0, home, target, 41) is var open ? open : 0); // two harbors and long routes: range x3 by sea
        Assert.True(match.Politics.OrderBlockade(0, target));
        Assert.True(match.Politics.FixtureBlockadeCutsRoute(home, target));
        Assert.True(match.Politics.FixtureBlockadeCutsRoute(target, home));
        Assert.Equal(41, match.Politics.FixtureCaravanTiles(0, home, target, 41)); // no harbor mode into a blockaded port
        Assert.False(match.Politics.FixtureBlockadeCutsRoute(home, overland), "another town's route is not the blockaded harbor's");
    }

    [Fact]
    public void A_blockade_lapses_with_the_war_the_galley_or_the_crew_and_survives_a_snapshot()
    {
        (MatchModel match, SimRunner runner, int home, int target) = BlockadeRig();
        Assert.True(match.Politics.OrderBlockade(0, target));
        using var snapshot = new System.IO.MemoryStream();
        using (var writer = new System.IO.BinaryWriter(snapshot, System.Text.Encoding.UTF8, leaveOpen: true)) match.WriteSnapshot(writer);
        snapshot.Position = 0;
        MatchModel resumed = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: ChapterRules.Iron);
        using (var reader = new System.IO.BinaryReader(snapshot)) resumed.ReadSnapshot(reader);
        Assert.Equal(match.ComputeChecksum(), resumed.ComputeChecksum());
        Assert.Single(resumed.Politics.Blockades);

        match.Politics.SetWar(0, 2, false);
        runner.Advance(150);
        Assert.Empty(match.Politics.Blockades);
        Assert.Contains(match.Politics.Events, e => e.Kind == PoliticsEventKind.BlockadeEnded && e.Civ == 0 && e.B == 0);

        (MatchModel lost, SimRunner lostRunner, int lostHome, int lostTarget) = BlockadeRig();
        Assert.True(lost.Politics.OrderBlockade(0, lostTarget));
        lost.Economy.LoseGalley(lostHome);
        lostRunner.Advance(150);
        Assert.Empty(lost.Politics.Blockades);
    }

    [Fact]
    public void A_relief_galley_costs_the_blockade_one_ship_or_the_defender_one()
    {
        (MatchModel match, SimRunner runner, int home, int target) = BlockadeRig();
        match.Economy.FixtureBuildIronStructure(target, "galley");
        match.Economy.FixtureSetSoldiersSupplied(target, 4);
        Assert.True(match.Economy.CrewedGalley(target));
        Assert.True(match.Politics.OrderBlockade(0, target));
        int before = (match.Economy.CrewedGalley(home) ? 1 : 0) + (match.Economy.CrewedGalley(target) ? 1 : 0);
        Assert.Equal(2, before);
        for (int season = 0; season < 3 && match.Politics.Blockades.Count > 0; season++)
        {
            match.Economy.FixtureSetSoldiersSupplied(home, 4);
            match.Economy.FixtureSetSoldiersSupplied(target, 4);
            runner.Advance(150);
        }
        int after = (match.Economy.CrewedGalley(home) ? 1 : 0) + (match.Economy.CrewedGalley(target) ? 1 : 0);
        Assert.True(after < before, "a contested harbor costs one side a galley");
    }

    private static int Settlement(MatchModel match, int civ)
        => Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == civ && !match.Politics.IsFreeFolk(s));

    private static void Near(Fix64 expected, Fix64 actual)
        => Assert.True(Fix64.Abs(expected - actual) <= Fix64.FromRaw(43950), $"expected {expected}, got {actual}");
}
