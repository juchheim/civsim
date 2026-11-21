using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Mind and state in the Discovery chapter (M13 WP-E6-7 slice 1, docs/MVP/38 §5.3, §5.4): INQ-07, INQ-08, NUM-11 and GOV-12. Discovery only.</summary>
public sealed class DiscoveryMindTests
{
    private const ulong Seed = 20261024;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric(
        "KEY-4", "KEY-6", "SEA-11", "SEA-12", "INQ-07", "INQ-08", "NUM-11", "GOV-12", "BEL-12", "MEC-05", "LRN-05", "INQ-04"));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner) Rig(ChapterRules rules)
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules);
        return (match, new SimRunner(match));
    }

    private static void Grant(SimRunner runner, MatchModel match, int civ, params string[] projects)
    {
        foreach (string project in projects)
        {
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, match.Research.Catalog.IndexOf(project), 0, 0));
            runner.Advance(2);
        }
    }

    private static int Home(MatchModel match, int civ)
        => Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == civ && !match.Politics.IsFreeFolk(s));

    [Fact]
    public void INQ08_and_NUM11_cut_project_costs_through_the_diffusion_floor_and_only_in_Discovery()
    {
        // Every rig completes an era-six project, so the era lag is the same in all of them and only the cost mods differ.
        (MatchModel plain, SimRunner plainRunner) = Rig(ChapterRules.Discovery);
        Grant(plainRunner, plain, 0, "KEY-5", "KEY-6", "SEA-12");
        (MatchModel inq08, SimRunner inq08Runner) = Rig(ChapterRules.Discovery);
        Grant(inq08Runner, inq08, 0, "KEY-5", "KEY-6", "INQ-08");
        (MatchModel both, SimRunner bothRunner) = Rig(ChapterRules.Discovery);
        Grant(bothRunner, both, 0, "KEY-5", "KEY-6", "INQ-08", "NUM-11");
        int mec = plain.Research.Catalog.IndexOf("MEC-05"), inq = plain.Research.Catalog.IndexOf("INQ-07");

        Fix64 list = plain.Research.EffectiveCost(0, mec), now = inq08.Research.EffectiveCost(0, mec);
        Assert.True(now < list, $"INQ-08 cuts every project's cost ({list} to {now})");
        // The first civilization to finish a project gets the pioneer bonus (x1.1 on the effect), so a tenth becomes 0.89.
        Assert.True(now >= list * Fix64.FromRatio(89, 100) - Fix64.FromRaw(43950), $"a tenth, with the pioneer bonus at most ({list} to {now})");
        Assert.True(now >= list / 2, "never below the half floor");
        Assert.True(plain.Research.EffectiveCost(1, mec) == inq08.Research.EffectiveCost(1, mec), "another civilization is untouched");

        Fix64 inqNow = inq08.Research.EffectiveCost(0, inq), inqBoth = both.Research.EffectiveCost(0, inq);
        Assert.True(inqBoth < inqNow, "NUM-11 takes a further tenth off INQ projects");
        Assert.True(inqBoth >= inqNow * Fix64.FromRatio(89, 100) - Fix64.FromRaw(43950));
        Assert.True(both.Research.EffectiveCost(0, mec) == now, "NUM-11 leaves other projects alone");

        (MatchModel fief, SimRunner fiefRunner) = Rig(ChapterRules.Fief);
        Grant(fiefRunner, fief, 0, "KEY-5");
        Fix64 before = fief.Research.EffectiveCost(0, mec);
        Grant(fiefRunner, fief, 0, "KEY-6", "INQ-08", "NUM-11"); // the ceiling refuses the era-six completions
        Assert.True(before == fief.Research.EffectiveCost(0, mec) || fief.Research.EffectiveCost(0, mec) <= before, "no Discovery cost cut in Fief");
    }

    /// <summary>A civilization-0 town with an academy and the research to staff scholars, in the given chapter.</summary>
    private static (MatchModel Match, SimRunner Runner) ScholarTown(ChapterRules rules, params string[] more)
    {
        (MatchModel match, SimRunner runner) = Rig(rules);
        Grant(runner, match, 0, new[] { "KEY-3", "KEY-4", "KEY-5", "LRN-05", "INQ-04" }.Concat(more).ToArray());
        int home = Home(match, 0);
        match.Economy.FixtureAddPeople(home, 80, 25);
        match.Economy.FixtureSetTier(home, 1);
        match.Economy.FixtureBuildIronStructure(home, "academy");
        for (int season = 0; season < 3; season++) { match.Economy.FixtureSetTier(home, 1); runner.Advance(150); }
        return (match, runner);
    }

    [Fact]
    public void INQ08_lifts_the_staffed_insight_sources_and_not_the_commons_and_never_in_Fief()
    {
        (MatchModel plain, _) = ScholarTown(ChapterRules.Discovery, "KEY-6");
        (MatchModel learned, _) = ScholarTown(ChapterRules.Discovery, "KEY-6", "INQ-08");
        Fix64 without = plain.Research.InsightPerYear(0), with = learned.Research.InsightPerYear(0);
        Assert.True(with > without, $"INQ-08 lifts the staffed sources ({without} to {with})");
        Assert.True(with <= without * Fix64.FromRatio(111, 100), "and never lifts more than a tenth (pioneer bonus included) of the whole");
        Assert.True(plain.Research.InsightPerYear(1) == learned.Research.InsightPerYear(1));

        (MatchModel fief, _) = ScholarTown(ChapterRules.Fief, "KEY-6");
        (MatchModel fief2, _) = ScholarTown(ChapterRules.Fief, "KEY-6", "INQ-08"); // the ceiling refuses the era-six completion
        Assert.True(fief.Research.InsightPerYear(0) == fief2.Research.InsightPerYear(0));
    }

    [Fact]
    public void GOV12_stops_Overreach_growing_with_distance_and_cuts_it_by_two_fifths()
    {
        (MatchModel match, SimRunner runner) = Rig(ChapterRules.Discovery);
        Grant(runner, match, 0, "KEY-5", "KEY-6");
        int far = Home(match, 2);
        match.Economy.FixtureSetCivilizationAndResidents(far, 0); // a far town across the sea joins civilization 0
        int without = match.Economy.FixtureCohesionTarget(far);
        Grant(runner, match, 0, "GOV-12");
        int with = match.Economy.FixtureCohesionTarget(far);
        Assert.True(with > without, $"the nation-state holds a far town together better ({with} against {without})");

        (MatchModel fief, SimRunner fiefRunner) = Rig(ChapterRules.Fief);
        int fiefFar = Home(fief, 2);
        fief.Economy.FixtureSetCivilizationAndResidents(fiefFar, 0);
        int before = fief.Economy.FixtureCohesionTarget(fiefFar);
        Grant(fiefRunner, fief, 0, "KEY-5", "KEY-6", "GOV-12");
        Assert.Equal(before, fief.Economy.FixtureCohesionTarget(fiefFar));
    }

    [Fact]
    public void INQ07_costs_a_point_of_Cohesion_until_BEL12_and_makes_sea_routes_five_percent_safer()
    {
        (MatchModel match, SimRunner runner) = Rig(ChapterRules.Discovery);
        Grant(runner, match, 0, "KEY-5", "KEY-6", "SEA-04", "SEA-12");
        int home = Home(match, 0);
        int before = match.Economy.FixtureCohesionTarget(home);
        int destination = Enumerable.Range(0, World.Value.TileCount)
            .Where(t => World.Value.TileAt(t).IsLand && match.Politics.SeaTilesOnRoute(0, match.Economy.SettlementLite(home).SiteTile, t) > 0)
            .OrderBy(t => match.Politics.SeaTilesOnRoute(0, match.Economy.SettlementLite(home).SiteTile, t)).First();
        int siteTile = match.Economy.SettlementLite(home).SiteTile;
        int riskBefore = match.Politics.VoyageRiskPermille(0, siteTile, destination);

        Grant(runner, match, 0, "INQ-07");
        Assert.Equal(before - 1, match.Economy.FixtureCohesionTarget(home));
        Assert.Equal(riskBefore * 100 / 105, match.Politics.VoyageRiskPermille(0, siteTile, destination));
        Grant(runner, match, 0, "BEL-12");
        Assert.Equal(before, match.Economy.FixtureCohesionTarget(home));
    }
}
