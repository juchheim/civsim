using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>TER-11 overseas colonies (M13 WP-E6-6 slice 2, docs/MVP/38 §5.2): sea strain, seeded independence, and the colony reach. Discovery chapter only.</summary>
public sealed class DiscoveryColonyTests
{
    private const ulong Seed = 20261020;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack =
        new(() => TestPack.WithNumeric("KEY-6", "SEA-11", "SEA-12", "TER-11"));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner, int Colony) Start(ChapterRules rules)
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules);
        var runner = new SimRunner(match);
        int Home(int civ) => Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == civ);
        int colony = Home(2);
        // Hand a rival's home village to civ 0: it now lies across the sea from civ 0's capital.
        match.Economy.FixtureSetCivilizationAndResidents(colony, 0);
        return (match, runner, colony);
    }

    private static void Grant(SimRunner runner, MatchModel match, int civ, params string[] projects)
    {
        foreach (string project in projects)
        {
            int index = match.Research.Catalog.IndexOf(project);
            Assert.True(index >= 0, $"{project} must exist in the test pack");
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, index, 0, 0));
            runner.Advance(2);
        }
    }

    [Fact]
    public void A_settlement_across_the_sea_is_a_colony_only_with_TER11_in_Discovery()
    {
        (MatchModel match, SimRunner runner, int colony) = Start(ChapterRules.Discovery);
        Grant(runner, match, 0, "KEY-5", "SEA-04", "KEY-6");
        Assert.False(match.Economy.FixtureIsColony(colony), "no TER-11, no colony");
        Grant(runner, match, 0, "TER-11");
        Assert.True(match.Economy.FixtureIsColony(colony));
        int capital = Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == 0 && s != colony);
        Assert.False(match.Economy.FixtureIsColony(capital), "the capital is not a colony");
    }

    [Fact]
    public void The_frame_shows_an_own_colony_as_content_with_the_capital_to_tether_to_and_nothing_for_a_rival()
    {
        (MatchModel match, SimRunner runner, int colony) = Start(ChapterRules.Discovery);
        Grant(runner, match, 0, "KEY-5", "SEA-04", "KEY-6", "TER-11");
        int capital = Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == 0 && s != colony);

        var own = new CivSim.Sim.View.MatchFrame();
        new CivSim.Sim.View.FrameBuilder(match, 0).Build(match, own);
        int row = Enumerable.Range(0, own.SettlementCount).First(w => own.SettlementIndex[w] == colony);
        Assert.Equal(1, own.Discovery.Colony[row]); // content: not strained, not blockaded
        Assert.Equal(match.Economy.SettlementLite(capital).SiteTile, own.Discovery.CapitalTile);

        var rival = new CivSim.Sim.View.MatchFrame();
        new CivSim.Sim.View.FrameBuilder(match, 1).Build(match, rival);
        for (int w = 0; w < rival.SettlementCount; w++)
            if (rival.SettlementIndex[w] == colony) Assert.Equal(0, rival.Discovery.Colony[w]); // another civilization's colony is not the viewer's to read
    }

    [Fact]
    public void TER11_does_nothing_in_Fief()
    {
        (MatchModel match, SimRunner runner, int colony) = Start(ChapterRules.Fief);
        Grant(runner, match, 0, "KEY-5", "SEA-04", "KEY-6", "TER-11"); // the ceiling refuses the era-six completion
        Assert.False(match.Economy.FixtureIsColony(colony));
        Assert.Equal(PoliticsSystem.SailSettleReach, match.Politics.SettleReach(0));
    }

    [Fact]
    public void A_colony_carries_more_strain_than_the_same_town_without_TER11()
    {
        (MatchModel match, SimRunner runner, int colony) = Start(ChapterRules.Discovery);
        Grant(runner, match, 0, "KEY-5", "SEA-04", "KEY-6");
        int without = match.Economy.FixtureCohesionTarget(colony);
        Grant(runner, match, 0, "TER-11");
        int with = match.Economy.FixtureCohesionTarget(colony);
        Assert.True(with < without, $"the sea adds strain to a colony ({with} against {without})");
    }

    [Fact]
    public void Independence_is_a_seeded_yearly_roll_that_needs_low_Cohesion()
    {
        (MatchModel match, SimRunner runner, int colony) = Start(ChapterRules.Discovery);
        Grant(runner, match, 0, "KEY-5", "SEA-04", "KEY-6", "TER-11");
        LivingEconomyModel economy = match.Economy;
        const int years = 4000;
        int low = 0;
        for (int season = 3; season < 4 * years; season += 4)
        {
            bool first = economy.FixtureColonyBreaksAway(colony, 30, season);
            Assert.Equal(first, economy.FixtureColonyBreaksAway(colony, 30, season));
            if (first) low++;
            Assert.False(economy.FixtureColonyBreaksAway(colony, 60, season), "a content colony never rolls");
        }
        Assert.InRange(low, years * 3 / 100, years * 10 / 100); // about 6 in 100 a year
    }

    [Fact]
    public void Colony_reach_crosses_the_channel_only_with_TER11_and_SEA12_and_a_blockade_flag_starts_clear()
    {
        (MatchModel match, SimRunner runner, int colony) = Start(ChapterRules.Discovery);
        Grant(runner, match, 0, "KEY-5", "SEA-04", "KEY-6");
        PoliticsSystem politics = match.Politics;
        Assert.Equal(PoliticsSystem.SailSettleReach, politics.SettleReach(0));
        Grant(runner, match, 0, "TER-11");
        Assert.Equal(PoliticsSystem.SailSettleReach, politics.SettleReach(0));
        Grant(runner, match, 0, "SEA-12");
        Assert.True(politics.HasColonyReach(0));
        Assert.Equal(PoliticsSystem.ColonySettleReach, politics.SettleReach(0));
        Assert.Equal(PoliticsSystem.ColonySettleReach, politics.FoundReach(0, bySea: true));
        Assert.False(politics.HasColonyReach(1));
        Assert.False(politics.CutOffBySea(colony));
    }

    [Fact]
    public void A_colony_that_secedes_shows_as_independent_to_its_old_owner_and_is_told_in_the_Chronicle()
    {
        (MatchModel match, SimRunner runner, int colony) = Start(ChapterRules.Discovery);
        Grant(runner, match, 0, "KEY-5", "SEA-04", "KEY-6", "TER-11");
        Assert.True(match.Economy.FixtureIsColony(colony));
        Assert.False(match.Economy.IsBreakawayColony(colony, 0), "a colony that still belongs is not independent");

        var builder = new CivSim.Sim.View.FrameBuilder(match, 0);
        var before = new CivSim.Sim.View.MatchFrame();
        builder.Build(match, before);
        Assert.Equal(0, before.Discovery.Independent);

        match.Economy.FixtureSecede(colony);
        runner.Advance(CivSim.Sim.Research.ResearchSystem.SeasonTicks + 5); // politics drains the secession at its next season step and makes it Free Folk
        Assert.False(match.Economy.FixtureIsColony(colony), "a seceded settlement is no longer a colony");
        Assert.True(match.Economy.IsBreakawayColony(colony, 0));
        Assert.False(match.Economy.IsBreakawayColony(colony, 1), "only its old owner sees it as a breakaway colony");

        var frame = new CivSim.Sim.View.MatchFrame();
        builder.Build(match, frame);
        int row = -1;
        for (int w = 0; w < frame.SettlementCount; w++) if (frame.SettlementIndex[w] == colony) row = w;
        Assert.True(row >= 0);
        Assert.NotEqual(0, frame.SettlementFlags[row] & (ushort)CivSim.Sim.View.SettlementViewFlags.FreeFolk);
        Assert.Equal(4, frame.Discovery.Colony[row]);
        Assert.Equal(1, frame.Discovery.Independent);
        Assert.Contains(frame.Events.Take(frame.EventCount), e => e.Kind == CivSim.Sim.View.ViewEventKind.ColonyIndependent && e.Settlement == colony);

        var rival = new CivSim.Sim.View.MatchFrame();
        new CivSim.Sim.View.FrameBuilder(match, 1).Build(match, rival);
        Assert.Equal(0, rival.Discovery.Independent);
    }

    [Fact]
    public void A_new_settlement_across_the_sea_is_told_as_a_colony_founding_once_and_a_neighbor_is_not()
    {
        (MatchModel match, SimRunner runner, int colony) = Start(ChapterRules.Discovery);
        Grant(runner, match, 0, "KEY-5", "SEA-04", "KEY-6", "TER-11");
        var builder = new CivSim.Sim.View.FrameBuilder(match, 0);
        var frame = new CivSim.Sim.View.MatchFrame();
        builder.Build(match, frame);
        builder.Build(match, frame); // primed: from here a new settlement is news
        int sea = match.Economy.SettlementLite(colony).SiteTile;
        var people = new List<long>();
        for (int i = 0; i < frame.FigureCount && people.Count < 6; i++)
            if (frame.FigureCiv[i] == 0 && frame.FigureAge[i] == (byte)CivSim.Sim.View.AgeBand.Adult) people.Add(frame.FigureId[i]);
        var land = World.Value;
        var taken = Enumerable.Range(0, match.Economy.SettlementCount).Select(i => match.Economy.SettlementLite(i).SiteTile).ToHashSet();
        int site = Enumerable.Range(0, land.TileCount).Where(t => land.TileAt(t).IsLand && !taken.Contains(t)
                && land.TileAt(t).Biome is Biome.GrasslandSteppe or Biome.OpenWoodland or Biome.TemperateForest or Biome.Savanna
                && HexGrid.Distance(t, sea, land.Width) is >= 1 and <= 4)
            .OrderBy(t => HexGrid.Distance(t, sea, land.Width)).First();
        int founded = match.Economy.FoundSettlement(0, site, people);
        Assert.True(match.Economy.IsOverseasColony(founded), "the new village is across the water from the capital");
        runner.Advance(5);
        builder.Build(match, frame);
        Assert.Single(frame.Events.Take(frame.EventCount), e => e.Kind == CivSim.Sim.View.ViewEventKind.ColonyFounded && e.Settlement == founded);
        runner.Advance(5);
        builder.Build(match, frame);
        Assert.Single(frame.Events.Take(frame.EventCount), e => e.Kind == CivSim.Sim.View.ViewEventKind.ColonyFounded && e.Settlement == founded); // the log keeps it; nothing told it twice

        var rival = new CivSim.Sim.View.MatchFrame();
        var rivalBuilder = new CivSim.Sim.View.FrameBuilder(match, 1);
        rivalBuilder.Build(match, rival);
        Assert.DoesNotContain(rival.Events.Take(rival.EventCount), e => e.Kind == CivSim.Sim.View.ViewEventKind.ColonyFounded);
    }

    [Fact]
    public void Independence_is_told_only_for_a_colony_not_for_an_ordinary_secession()
    {
        (MatchModel match, SimRunner runner, int colony) = Start(ChapterRules.Discovery);
        Grant(runner, match, 0, "KEY-5", "SEA-04", "KEY-6"); // no TER-11: the town across the sea is no colony
        var builder = new CivSim.Sim.View.FrameBuilder(match, 0);
        match.Economy.FixtureSecede(colony);
        runner.Advance(CivSim.Sim.Research.ResearchSystem.SeasonTicks + 5);
        var frame = new CivSim.Sim.View.MatchFrame();
        builder.Build(match, frame);
        Assert.DoesNotContain(frame.Events.Take(frame.EventCount), e => e.Kind == CivSim.Sim.View.ViewEventKind.ColonyIndependent);
        Assert.Equal(0, frame.Discovery.Independent);
    }
}
