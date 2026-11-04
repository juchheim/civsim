using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Academies, scholars and the learning mods of the Iron chapter (docs/MVP/26 §2.3, §3.3).</summary>
public sealed class IronLearningTests
{
    private const ulong Seed = 20261024;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack =
        new(() => TestPack.WithNumeric("KEY-4", "LRN-05", "INQ-04", "BEL-08", "COM-07", "INQ-05", "MEC-05", "NRG-03"));
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

    /// <summary>A civilization-0 town of some size with an academy, the research to staff it and the seasons to do so.</summary>
    private static (MatchModel Match, SimRunner Runner, int Home) Academy(ChapterRules rules, bool academy = true, params string[] more)
    {
        (MatchModel match, SimRunner runner) = Rig(rules);
        Grant(runner, 0, new[] { "KEY-3", "KEY-4", "LRN-05" }.Concat(more).ToArray());
        int home = Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == 0);
        match.Economy.FixtureAddPeople(home, 80, 25);
        match.Economy.FixtureSetTier(home, 1);
        if (academy) match.Economy.FixtureBuildIronStructure(home, "academy");
        for (int season = 0; season < 3; season++) { match.Economy.FixtureSetTier(home, 1); runner.Advance(150); }
        return (match, runner, home);
    }

    [Fact]
    public void An_academy_is_wanted_by_a_town_that_knows_LRN_05_and_staffs_exactly_one_scholar()
    {
        BronzeFixture f = BronzeFixture.CreateIron(TestPack.Pack, Seed, 60, "KEY-4", "LRN-05");
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureAddPeople(0, 60, 25);
        Assert.True(f.Economy.FixtureIronBuildWanted(0, "academy"));
        f.Economy.FixtureBuildIronStructure(0, "academy");
        Assert.False(f.Economy.FixtureIronBuildWanted(0, "academy"));

        (MatchModel match, _, _) = Academy(ChapterRules.Iron);
        Assert.Equal(1, match.Economy.ScholarHeads(0));
        (MatchModel none, _, _) = Academy(ChapterRules.Iron, academy: false);
        Assert.Equal(0, none.Economy.ScholarHeads(0)); // knowing LRN-05 creates no scholar
        (MatchModel bronze, _, _) = Academy(ChapterRules.Bronze);
        Assert.Equal(0, bronze.Economy.ScholarHeads(0));
    }

    [Fact]
    public void A_scholar_adds_Insight_and_INQ_04_and_BEL_08_multiply_only_the_scholars_share()
    {
        (MatchModel withAcademy, _, _) = Academy(ChapterRules.Iron);
        (MatchModel without, _, _) = Academy(ChapterRules.Iron, academy: false);
        Fix64 scholarShare = withAcademy.Research.InsightPerYear(0) - without.Research.InsightPerYear(0);
        Assert.True(scholarShare > Fix64.Zero, "a staffed scholar adds Insight");

        (MatchModel philosophers, _, _) = Academy(ChapterRules.Iron, academy: true, "INQ-04");
        Fix64 boosted = philosophers.Research.InsightPerYear(0) - without.Research.InsightPerYear(0);
        Assert.True(boosted > scholarShare * Fix64.FromRatio(115, 100) && boosted < scholarShare * Fix64.FromRatio(125, 100),
            $"INQ-04 should lift the scholar's share by about 20%: {scholarShare} -> {boosted}");

        // Without an academy the same research changes nothing: the mod is on scholars, not on the Commons.
        (MatchModel idle, _, _) = Academy(ChapterRules.Iron, academy: false, "INQ-04", "BEL-08");
        Assert.Equal(without.Research.InsightPerYear(0), idle.Research.InsightPerYear(0));
    }

    [Fact]
    public void Mechanics_cuts_MEC_and_NRG_costs_toward_a_tenth_as_the_method_is_adopted()
    {
        (MatchModel plain, SimRunner plainRunner) = Rig(ChapterRules.Iron);
        Grant(plainRunner, 0, "KEY-3", "KEY-4");
        (MatchModel learned, SimRunner learnedRunner) = Rig(ChapterRules.Iron);
        Grant(learnedRunner, 0, "KEY-3", "KEY-4", "INQ-05");
        int mec = Catalog.Value.IndexOf("MEC-05"), nrg = Catalog.Value.IndexOf("NRG-03"), other = Catalog.Value.IndexOf("INQ-04");
        // INQ-05 is a Method, so like every Method mod it takes hold as the people adopt it: fully adopted it is x0.9, and it is never worse than that.
        foreach (int project in new[] { mec, nrg })
        {
            Fix64 list = plain.Research.EffectiveCost(0, project), now = learned.Research.EffectiveCost(0, project);
            Assert.True(now < list && now >= list * Fix64.FromRatio(9, 10), $"cost {list} -> {now}");
        }
        Assert.Equal(plain.Research.EffectiveCost(0, other), learned.Research.EffectiveCost(0, other)); // only MEC and NRG types
        // Another civilization is untouched.
        Assert.Equal(plain.Research.EffectiveCost(1, mec), learned.Research.EffectiveCost(1, mec));
        // The Bronze chapter cannot complete INQ-05, so nothing changes there.
        (MatchModel bronze, SimRunner bronzeRunner) = Rig(ChapterRules.Bronze);
        Fix64 before = bronze.Research.EffectiveCost(0, mec);
        Grant(bronzeRunner, 0, "KEY-3", "KEY-4", "INQ-05");
        Assert.Equal(before, bronze.Research.EffectiveCost(0, mec));
    }
}
