using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>
/// The musket rung of the levy's arms (WAR-13, M13 WP-E6-9 slice 2, docs/MVP/38 §5.6; e6_combat_oracle.py Part 4): a good a forge makes, a want the planner
/// carries, a weapon the fighters draw in the ladder, and a share the frame shows. Discovery only.
/// </summary>
public sealed class DiscoveryMusketTests
{
    private const ulong Seed = 20261126;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric("KEY-6", "WAR-13", "GOV-12"));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner) Start(ChapterRules rules)
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules);
        return (match, new SimRunner(match));
    }

    private static void Grant(SimRunner runner, MatchModel match, params string[] projects)
    {
        foreach (string project in projects)
        {
            runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, match.Research.Catalog.IndexOf(project), 0, 0));
            runner.Advance(2);
        }
    }

    private static Fix64 Strength(MatchModel m) => m.Politics.OwnDefendersOf(0, 0).Aggregate(Fix64.Zero, (sum, f) => sum + f.Strength);

    private static BronzeFixture Forge(params string[] projects)
    {
        var all = new List<string> { "KEY-5" };
        all.AddRange(projects);
        BronzeFixture f = BronzeFixture.CreateDiscoveryWithCoal(TestPack.Pack, Seed, 150, all.ToArray());
        f.Economy.FixtureSetMetalFacilities(0, forges: 1, smelters: 0);
        f.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(120));
        f.Economy.FixtureSetGoodStock(0, "G-WROUGHT", Fix64.FromInt(10));
        f.Economy.FixtureSetGoodStock(0, "G-CORD", Fix64.FromInt(20));
        return f;
    }

    [Fact]
    public void A_musket_in_store_arms_a_levy_fighter_at_two_point_eight_and_the_drill_multiplies_it()
    {
        (MatchModel plain, SimRunner plainRunner) = Start(ChapterRules.Discovery);
        Grant(plainRunner, plain, "KEY-5", "KEY-6", "WAR-13", "GOV-12");
        (MatchModel armed, SimRunner armedRunner) = Start(ChapterRules.Discovery);
        Grant(armedRunner, armed, "KEY-5", "KEY-6", "WAR-13", "GOV-12");
        armed.Economy.FixtureAddDiscoveryGood(0, "G-MUSKET", Fix64.FromInt(6));
        Assert.Equal(6, armed.Economy.MusketsAt(0));
        Fix64 before = Strength(plain), after = Strength(armed);
        Assert.True(after > before, $"six muskets lift the defenders ({before} to {after})");

        // The same muskets without the drill are worth less than with it: the drill multiplies the levy's whole strength.
        (MatchModel undrilled, SimRunner undrilledRunner) = Start(ChapterRules.Discovery);
        Grant(undrilledRunner, undrilled, "KEY-5", "KEY-6", "WAR-13");
        undrilled.Economy.FixtureAddDiscoveryGood(0, "G-MUSKET", Fix64.FromInt(6));
        Assert.True(Strength(undrilled) < after, "muskets are stronger in a drilled levy's hands");
        (MatchModel bare, SimRunner bareRunner) = Start(ChapterRules.Discovery);
        Grant(bareRunner, bare, "KEY-5", "KEY-6", "WAR-13");
        Assert.True(Strength(undrilled) > Strength(bare), "and a musket still beats the bare levy without the drill");
    }

    [Fact]
    public void Muskets_do_nothing_without_WAR13_and_outside_Discovery()
    {
        (MatchModel noDrill, SimRunner runner) = Start(ChapterRules.Discovery);
        Grant(runner, noDrill, "KEY-5", "KEY-6");
        Fix64 before = Strength(noDrill);
        noDrill.Economy.FixtureAddDiscoveryGood(0, "G-MUSKET", Fix64.FromInt(6));
        Assert.Equal(0, noDrill.Economy.MusketsAt(0));
        Assert.Equal(before, Strength(noDrill));

        (MatchModel fief, _) = Start(ChapterRules.Fief);
        Assert.False(fief.Economy.MusketsKnown(0));
        Assert.Equal(0, fief.Economy.MusketsAt(0));
    }

    [Fact]
    public void The_planner_wants_up_to_three_muskets_a_season_at_a_forge_with_iron_and_nothing_without_WAR13_a_forge_or_iron()
    {
        BronzeFixture with = Forge("WAR-13");
        Fix64 want = with.Economy.FixtureMusketWant(0);
        Assert.True(want > Fix64.Zero && want <= Fix64.FromInt(LivingEconomyModel.MusketsPerSeason), $"wants {want}");

        Assert.Equal(Fix64.Zero, Forge().Economy.FixtureMusketWant(0));               // no WAR-13
        BronzeFixture noForge = Forge("WAR-13");
        noForge.Economy.FixtureSetMetalFacilities(0, forges: 0, smelters: 0);
        Assert.Equal(Fix64.Zero, noForge.Economy.FixtureMusketWant(0));               // no forge
        BronzeFixture noIron = Forge("WAR-13");
        noIron.Economy.FixtureSetGoodStock(0, "G-WROUGHT", Fix64.Zero);
        Assert.Equal(Fix64.Zero, noIron.Economy.FixtureMusketWant(0));                // no iron
    }

    [Fact]
    public void A_town_that_already_holds_a_musket_for_every_levy_fighter_wants_no_more_and_muskets_wear_out()
    {
        BronzeFixture f = Forge("WAR-13");
        f.Economy.FixtureSetGoodStock(0, "G-MUSKET", Fix64.FromInt(LivingEconomyModel.LevyBowCap));
        Assert.Equal(Fix64.Zero, f.Economy.FixtureMusketWant(0));

        Fix64 start = f.Economy.FixtureGoodStock(0, "G-MUSKET");
        for (int i = 0; i < 40; i++) f.Economy.FixtureWearMilitaryStockBeforePlanning(0);
        Assert.True(f.Economy.FixtureGoodStock(0, "G-MUSKET") < start, "a musket in store wears out");
    }

    [Fact]
    public void The_frame_shows_the_share_of_the_levy_holding_a_musket_in_Discovery_only()
    {
        (MatchModel match, SimRunner runner) = Start(ChapterRules.Discovery);
        Grant(runner, match, "KEY-5", "KEY-6", "WAR-13", "GOV-12");
        match.Economy.FixtureAddDiscoveryGood(0, "G-MUSKET", Fix64.FromInt(5));
        var arms = match.Politics.ArmsOf(0);
        Assert.True(arms.Musket > 0 && arms.Musket <= 5, $"{arms.Musket} fighters hold a musket");
        Assert.Equal(5, arms.StockMusket);
    }
}
