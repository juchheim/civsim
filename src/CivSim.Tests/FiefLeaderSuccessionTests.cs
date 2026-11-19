using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;
using Xunit;

namespace CivSim.Tests;

public sealed class FiefLeaderSuccessionTests
{
    private const ulong Seed = 20261115;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack =
        new(() => TestPack.WithNumeric("KEY-5", "DIP-07", "GOV-09", "DIP-08"));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static MatchModel Start(ChapterRules? rules = null)
        => MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules ?? ChapterRules.Fief);

    private static void Grant(MatchModel match, int civ, params string[] projects)
    {
        var runner = new SimRunner(match);
        foreach (string project in projects)
        {
            int index = match.Research.Catalog.IndexOf(project);
            Assert.True(index >= 0, $"{project} must exist in the test pack");
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, index, 0, 0));
            runner.Advance(2);
        }
    }

    private static long SeniorLivingAdult(MatchModel match, int civ, long excluding = -1)
        => Enumerable.Range(0, match.Economy.FigureCount).Select(match.Economy.LookFigure)
            .Where(f => f.Civilization == civ && f.Age >= 14 && f.Id != excluding)
            .OrderByDescending(f => f.Age).ThenBy(f => f.Id).Select(f => f.Id).FirstOrDefault(-1);

    private static byte[] Snapshot(MatchModel match)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) match.WriteSnapshot(writer);
        return stream.ToArray();
    }

    [Fact]
    public void Initial_leader_and_successor_use_oldest_living_adult_then_lowest_figure_id_and_roundtrip()
    {
        MatchModel match = Start();
        Assert.Equal(SeniorLivingAdult(match, 0), match.Politics.FixtureFiefLeaderId(0));
        Grant(match, 0, "KEY-5", "DIP-07", "GOV-09", "DIP-08");
        match.Politics.FixtureAlly(0, 1);
        long oldLeader = match.Politics.FixtureFiefLeaderId(0);
        long successor = SeniorLivingAdult(match, 0, oldLeader);
        Assert.True(successor >= 0);

        match.Economy.FixtureKillFigureForSuccession(oldLeader);
        match.Politics.FixtureAdvanceFiefLeadership();

        Assert.Equal(successor, match.Politics.FixtureFiefLeaderId(0));
        Assert.True(match.Politics.Diplomacy.Allied(0, 1), "DIP-08 retains the active alliance when its leader dies");
        byte[] saved = Snapshot(match);
        MatchModel restored = Start();
        using (var reader = new BinaryReader(new MemoryStream(saved))) restored.ReadSnapshot(reader);
        Assert.Equal(match.ComputeChecksum(), restored.ComputeChecksum());
        Assert.Equal(successor, restored.Politics.FixtureFiefLeaderId(0));
    }

    [Fact]
    public void Without_DIP08_a_leader_death_lapses_all_active_alliances()
    {
        MatchModel match = Start();
        match.Politics.FixtureAlly(0, 1);
        long leader = match.Politics.FixtureFiefLeaderId(0);
        Assert.True(leader >= 0);

        match.Economy.FixtureKillFigureForSuccession(leader);
        match.Politics.FixtureAdvanceFiefLeadership();

        Assert.False(match.Politics.Diplomacy.Allied(0, 1));
        Assert.Equal(Diplomacy.AllianceFloor, match.Politics.Diplomacy.Relations(0, 1));
        Assert.Equal(SeniorLivingAdult(match, 0, leader), match.Politics.FixtureFiefLeaderId(0));
    }

    [Fact]
    public void Succession_records_no_leader_when_no_living_adult_is_eligible()
    {
        MatchModel match = Start();
        Grant(match, 0, "KEY-5", "DIP-07", "GOV-09", "DIP-08");
        match.Politics.FixtureAlly(0, 1);
        long leader = match.Politics.FixtureFiefLeaderId(0);
        for (int i = 0; i < match.Economy.FigureCount; i++)
        {
            FigureLook figure = match.Economy.LookFigure(i);
            if (figure.Civilization == 0 && figure.Id != leader) match.Economy.FixtureSetFigureAge(figure.Id, 13);
        }

        match.Economy.FixtureKillFigureForSuccession(leader);
        match.Politics.FixtureAdvanceFiefLeadership();

        Assert.Equal(-1, match.Politics.FixtureFiefLeaderId(0));
        Assert.True(match.Politics.Diplomacy.Allied(0, 1));
    }

    [Theory]
    [InlineData(ChapterMode.Bronze)]
    [InlineData(ChapterMode.Iron)]
    public void Leader_lifecycle_is_absent_in_Bronze_and_Iron(ChapterMode mode)
    {
        ChapterRules rules = mode == ChapterMode.Bronze ? ChapterRules.Bronze : ChapterRules.Iron;
        MatchModel match = Start(rules);
        Assert.Equal(-1, match.Politics.FixtureFiefLeaderId(0));
        match.Politics.FixtureAlly(0, 1);
        long figure = Enumerable.Range(0, match.Economy.FigureCount).Select(match.Economy.LookFigure)
            .First(f => f.Civilization == 0).Id;
        match.Economy.FixtureKillFigureForSuccession(figure);
        match.Politics.FixtureAdvanceFiefLeadership();
        Assert.True(match.Politics.Diplomacy.Allied(0, 1));
        Assert.Equal(-1, match.Politics.FixtureFiefLeaderId(0));
    }
}
