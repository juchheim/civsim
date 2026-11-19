using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Bows from a settlement's store arm its fighters in a Faith &amp; Fief match (docs/MVP/36); other chapters keep the abstract ranged fallback.</summary>
public sealed class War11FightersTests
{
    private const ulong Seed = 20261115;
    /// <summary>WAR-11 is a draft row until the E5 rows are promoted together, so the test makes it numeric for research to know it.</summary>
    private static readonly System.Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric("WAR-11"));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner, int Home) Prepared(ChapterRules rules, params string[] grants)
    {
        var catalog = new ResearchCatalog(Pack.Value);
        var match = MatchModel.Create(Seed, Pack.Value, WorldGenerator.Generate(Seed), Balanced, default, rules: rules);
        var runner = new SimRunner(match);
        foreach (string project in grants)
        {
            int index = catalog.IndexOf(project);
            if (rules.IsFief && catalog.Projects[index].Era == rules.LastResearchEra)
            {
                int key5 = catalog.IndexOf("KEY-5");
                if (!match.Research.IsDone(0, key5))
                {
                    runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, key5, 0, 0));
                    runner.Advance(1);
                }
            }
            runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, index, 0, 0));
        }
        runner.Advance(1);
        int home = Enumerable.Range(0, match.Economy.SettlementCount)
            .First(s => match.Economy.SettlementLite(s).Civilization == 0 && !match.Politics.IsFreeFolk(s));
        match.Economy.FixtureAddPeople(home, 200, 20);
        match.Economy.FixtureStock(home, "G-GRAIN", Fix64.FromInt(1000));
        match.Economy.FixtureStock(home, "G-WOOD", Fix64.FromInt(150));
        match.Economy.FixtureStock(home, "G-CORD", Fix64.FromInt(30));
        return (match, runner, home);
    }

    private static Fix64 Defence(MatchModel match, int home) => match.Politics.MilitaryOf(home).DefendingStrength;

    [Fact]
    public void A_fief_town_with_longbows_in_store_defends_harder_than_one_without_the_tech()
    {
        string[] bowTech = { "KEY-3", "KEY-4", "HNT-06", "WAR-03" };
        var without = Prepared(ChapterRules.Fief, bowTech);
        var with = Prepared(ChapterRules.Fief, bowTech.Concat(new[] { "WAR-11" }).ToArray());
        for (int i = 0; i < 4; i++) { without.Runner.Advance(150); with.Runner.Advance(150); }

        int weapons = (with.Match.Economy.GoodStock(with.Home, "G-LONGBOW") + with.Match.Economy.GoodStock(with.Home, "G-CROSSBOW")).FloorToInt();
        Assert.True(weapons > 0, "the settlement should have built levy bows");
        Assert.Equal(0, (without.Match.Economy.GoodStock(without.Home, "G-LONGBOW") + without.Match.Economy.GoodStock(without.Home, "G-CROSSBOW")).FloorToInt());
        Assert.True(Defence(with.Match, with.Home) > Defence(without.Match, without.Home),
            $"with bows {Defence(with.Match, with.Home)} should exceed without {Defence(without.Match, without.Home)}");
    }

    [Fact]
    public void Every_chapter_draws_the_bow_from_stock_so_a_bow_town_is_stronger_than_a_bowless_one()
    {
        // The bow is a stocked arm in every chapter: with Archery Warfare and no bows a town gets no free 1.8 x 1.5, and each bow it holds arms a fighter.
        var iron = Prepared(ChapterRules.Iron, "KEY-3", "HNT-05", "HNT-06", "WAR-03");
        iron.Runner.Advance(1);
        Fix64 bare = Defence(iron.Match, iron.Home);
        iron.Match.Economy.FixtureStockTool(iron.Home, "G-BOW", 20);
        Assert.True(Defence(iron.Match, iron.Home) > bare, "twenty bows in store should arm twenty fighters");
    }


    [Fact]
    public void A_fighter_who_dies_takes_a_share_of_the_towns_bows_with_them_and_a_wounded_one_does_not()
    {
        var m = Prepared(ChapterRules.Fief, "KEY-3", "KEY-4", "HNT-06", "WAR-03", "WAR-11");
        for (int i = 0; i < 4; i++) m.Runner.Advance(150);
        Fix64 Bows() => m.Match.Economy.GoodStock(m.Home, "G-LONGBOW") + m.Match.Economy.GoodStock(m.Home, "G-CROSSBOW") + m.Match.Economy.GoodStock(m.Home, "G-BOW");
        var records = new System.Collections.Generic.List<LivingEconomyModel.FighterRecord>();
        m.Match.Economy.ListFightingAge(m.Home, records);
        Fix64 before = Bows();
        Assert.True(before >= Fix64.FromInt(10), $"the town should hold bows: {before}");

        // wounded fighters keep their bows
        var wounded = records.Where(r => !r.Wounded).Take(5).Select(r => r.Id).ToList();
        m.Match.Politics.ApplyCasualties(new System.Collections.Generic.List<long>(), wounded, "test");
        Assert.Equal(before, Bows());

        // ten who die lose some of them, never more than one each
        var fallen = records.Where(r => !r.Wounded && !wounded.Contains(r.Id)).Take(10).Select(r => r.Id).ToList();
        m.Match.Politics.ApplyCasualties(fallen, new System.Collections.Generic.List<long>(), "test");
        Fix64 lost = before - Bows();
        Assert.True(lost > Fix64.Zero, "dead fighters should have taken bows with them");
        Assert.True(lost <= Fix64.FromInt(10), $"at most one bow each: {lost}");
    }

    [Fact]
    public void The_frame_gives_the_share_of_a_towns_fighters_that_hold_each_bow()
    {
        var m = Prepared(ChapterRules.Fief, "KEY-3", "KEY-4", "HNT-06", "WAR-03", "WAR-11");
        for (int i = 0; i < 4; i++) m.Runner.Advance(150);
        var frame = new CivSim.Sim.View.MatchFrame();
        new CivSim.Sim.View.FrameBuilder(m.Match, 0).Build(m.Match, frame);
        int row = -1;
        for (int w = 0; w < frame.SettlementCount; w++) if (frame.SettlementIndex[w] == m.Home) row = w;
        Assert.True(row >= 0, "the home town is in the viewer's frame");
        int cb = frame.SettlementBowPct[row * 3], lb = frame.SettlementBowPct[row * 3 + 1], wb = frame.SettlementBowPct[row * 3 + 2];
        Assert.True(cb + lb + wb > 0, $"the town holds bows: {cb}/{lb}/{wb}");
        Assert.True(cb + lb + wb <= 100, $"never more than everyone: {cb}/{lb}/{wb}");

        // a civilization that does not know the weapons shows none, whatever sits in a store
        var bare = Prepared(ChapterRules.Fief, "KEY-3", "KEY-4");
        for (int i = 0; i < 4; i++) bare.Runner.Advance(150);
        var f2 = new CivSim.Sim.View.MatchFrame();
        new CivSim.Sim.View.FrameBuilder(bare.Match, 0).Build(bare.Match, f2);
        for (int w = 0; w < f2.SettlementCount * 3; w++) Assert.Equal(0, f2.SettlementBowPct[w]);
    }
}
