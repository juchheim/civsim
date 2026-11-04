using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>The Iron Wonders and leagues (LRN-06, SEA-08, BLD-11, DIP-07): one of each in the world, effects while it stands, labor pledged but never multiplied.</summary>
public sealed class IronWonderTests
{
    private const ulong Seed = 20261024;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() =>
        TestPack.WithNumeric("KEY-4", "LRN-06", "SEA-08", "BLD-11", "DIP-07", "SEA-07", "TRD-07", "INQ-04"));
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

    private static int Settlement(MatchModel match, int civ)
        => Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == civ && !match.Politics.IsFreeFolk(s));

    private static (MatchModel Match, SimRunner Runner, int Home0, int Home1) Pair(params string[] projects)
    {
        (MatchModel match, SimRunner runner) = Rig(ChapterRules.Iron);
        Grant(runner, 0, new[] { "KEY-3", "KEY-4" }.Concat(projects).ToArray());
        Grant(runner, 1, new[] { "KEY-3", "KEY-4" }.Concat(projects).ToArray());
        int a = Settlement(match, 0), b = Settlement(match, 1);
        foreach (int s in new[] { a, b }) { match.Economy.FixtureAddPeople(s, 60, 25); match.Economy.FixtureSetTier(s, 1); }
        return (match, runner, a, b);
    }

    [Fact]
    public void A_Wonder_is_wanted_in_the_biggest_town_until_anyone_holds_it_and_there_is_only_one()
    {
        (MatchModel match, _, int a, int b) = Pair("LRN-06", "BLD-11");
        Assert.True(match.Economy.FixtureIronBuildWanted(a, "great_library"));
        Assert.True(match.Economy.FixtureIronBuildWanted(b, "great_library"));
        Assert.True(match.Economy.FixtureIronBuildWanted(a, "great_dome"));
        Assert.False(match.Economy.HoldsWonder(0, "great_library"));
        match.Economy.FixtureCompleteIron(a, "great_library");
        Assert.True(match.Economy.HoldsWonder(0, "great_library"));
        Assert.False(match.Economy.HoldsWonder(1, "great_library"));
        Assert.False(match.Economy.FixtureIronBuildWanted(a, "great_library"));
        Assert.False(match.Economy.FixtureIronBuildWanted(b, "great_library"), "nobody else builds a second Great Library");
        Assert.True(match.Economy.FixtureIronBuildWanted(b, "great_dome"), "another Wonder is a separate matter");
        // The Lighthouse needs a harbor in the town.
        (MatchModel sea, _, int c, _) = Pair("SEA-08", "SEA-07");
        Assert.False(sea.Economy.FixtureIronBuildWanted(c, "great_lighthouse"));
        sea.Economy.FixtureBuildIronStructure(c, "harbor");
        Assert.True(sea.Economy.FixtureIronBuildWanted(c, "great_lighthouse"));
    }

    [Fact]
    public void A_finished_Wonder_earns_its_Legacy_for_its_builder_alone()
    {
        (MatchModel match, SimRunner runner, int a, _) = Pair("LRN-06");
        int library = Catalog.Value.IndexOf("LRN-06");
        match.Economy.FixtureCompleteIron(a, "great_library");
        runner.Advance(150);
        Assert.True(match.Research.WonderBuilt(0, library));
        Assert.False(match.Research.WonderBuilt(1, library));
    }

    [Fact]
    public void The_Great_Library_doubles_the_diffusion_discount_and_never_passes_the_floor()
    {
        (MatchModel plain, SimRunner plainRunner, _, _) = Pair();
        (MatchModel library, SimRunner libraryRunner, int a, _) = Pair("LRN-06");
        foreach ((MatchModel m, SimRunner r) in new[] { (plain, plainRunner), (library, libraryRunner) })
        {
            m.Politics.ForceContact(0, 1);
            m.Research.RecordContact(0, 1);
            Grant(r, 1, "INQ-04"); // civilization 1 knows a project civilization 0 does not
        }
        int target = Catalog.Value.IndexOf("INQ-04");
        library.Economy.FixtureCompleteIron(a, "great_library");
        libraryRunner.Advance(150);
        Fix64 list = Catalog.Value.Projects[target].Cost;
        Near(list * Fix64.FromRatio(9, 10), plain.Research.EffectiveCost(0, target));
        Near(list * Fix64.FromRatio(8, 10), library.Research.EffectiveCost(0, target));
    }

    [Fact]
    public void The_Great_Lighthouse_halves_shipwreck_and_speeds_harbor_routes_only_for_its_holder()
    {
        (MatchModel match, SimRunner runner, int a, int b) = Pair("SEA-08", "SEA-07", "TRD-07");
        Grant(runner, 0, "SEA-04");
        int Lost(int civ) { int n = 0; for (ulong entity = 1; entity <= 2000; entity++) if (match.Politics.FixtureRollVoyageLost(300, entity, 5, civ)) n++; return n; }
        int before = Lost(0);
        match.Economy.FixtureBuildIronStructure(a, "harbor");
        match.Economy.FixtureCompleteIron(a, "great_lighthouse");
        int after = Lost(0), others = Lost(1);
        Assert.InRange(before, 500, 700);                 // about 30%
        Assert.InRange(after, 250, 350);                  // about 15%
        Assert.InRange(others, 500, 700);                 // another civilization's ships are as before
    }

    [Fact]
    public void League_rules_need_DIP_07_an_alliance_and_no_war_and_members_who_fall_out_leave()
    {
        (MatchModel none, _, _, _) = Pair();
        Assert.False(none.Politics.OrderLeague(0, 1));
        (MatchModel match, _, _, _) = Pair("DIP-07");
        match.Politics.ForceContact(0, 1);
        Assert.False(match.Politics.OrderLeague(0, 1), "a league is built on an alliance");
        match.Politics.FixtureAlly(0, 1);
        Assert.True(match.Politics.OrderLeague(0, 1));
        League league = Assert.Single(match.Politics.Leagues);
        Assert.Equal(new[] { 0, 1 }, league.Members);
        Assert.Equal(0, match.Politics.LeagueOf(0));
        Assert.False(match.Politics.OrderLeague(0, 1), "already partners");
        match.Politics.SetWar(0, 1, true);
        match.Politics.FixtureAdvanceLeagues();
        Assert.Empty(match.Politics.Leagues);
        Assert.Contains(match.Politics.Events, e => e.Kind == PoliticsEventKind.LeagueEnded);
    }

    [Fact]
    public void A_pledged_towns_build_labor_goes_into_the_builders_Wonder_and_is_never_multiplied()
    {
        // Two towns of one civilization stand in for a league: one builds, one pledges.
        (BronzeFixture pledged, int helperA) = IronChainWonderFarm();
        (BronzeFixture alone, int helperB) = IronChainWonderFarm();
        pledged.Economy.FixtureStartIronBuild(0, "great_library");
        alone.Economy.FixtureStartIronBuild(0, "great_library");
        pledged.Economy.SetWonderPledge(helperA, 0);
        Assert.Equal(0, pledged.Economy.WonderUnderConstruction(0) is var site ? site : -1);
        for (int season = 0; season < 6; season++)
        {
            foreach ((BronzeFixture f, int helper) in new[] { (pledged, helperA), (alone, helperB) })
            {
                f.Economy.FixtureSetTier(0, 1); f.Economy.FixtureSetTier(helper, 1);
                f.RunSeasons(1);
            }
        }
        Fix64 withHelp = pledged.Economy.FixtureIronBuildProgress(0), without = alone.Economy.FixtureIronBuildProgress(0);
        Assert.True(withHelp > without, $"a pledged town adds delivered labor: {without} -> {withHelp}");
        // The pledging town's own works stand still while its labor is lent; the unpledged town carries on building its own.
        Assert.Equal(Fix64.Zero, pledged.Economy.FixtureIronBuildProgress(helperA));
        Assert.True(alone.Economy.FixtureIronBuildProgress(helperB) > Fix64.Zero);
    }

    private static (BronzeFixture Fixture, int Far) IronChainWonderFarm()
    {
        BronzeFixture f = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "KEY-4", "LRN-06", "MIN-03");
        int start = f.Economy.SettlementLite(0).SiteTile;
        var world = CivSim.Sim.Economy.WorldEconomyInput.FromGeneratedWorld(CivSim.Sim.World.WorldGenerator.Generate(Seed));
        for (int tile = 0; tile < world.TileCount; tile++)
        {
            int d = CivSim.Sim.World.HexGrid.Distance(start, tile, world.Width);
            if (d < 14 || d > 18) continue;
            int far = f.Economy.FoundByDecree(0, tile, Fix64.FromRatio(2, 5), minDaughter: 6, startCohesion: 60);
            if (far < 0) continue;
            f.Economy.FixtureAddPeople(far, 60, 25); // a town with hands to spare, fed, so its work is not all farming
            f.Economy.FixtureStock(far, "G-GRAIN", Fix64.FromInt(4000));
            f.Economy.FixtureSetTier(0, 1);
            f.Economy.FixtureSetTier(far, 1);
            return (f, far);
        }
        throw new InvalidOperationException("no site for a second town");
    }

    private static void Near(Fix64 expected, Fix64 actual)
        => Assert.True(Fix64.Abs(expected - actual) <= Fix64.FromRatio(1, 100), $"expected {expected}, got {actual}");
}
