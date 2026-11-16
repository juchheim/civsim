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

public sealed class FiefDynasticInheritanceTests
{
    private const ulong Seed = 20261114;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack =
        new(() => TestPack.WithNumeric("KEY-5", "DIP-07", "GOV-09", "DIP-08"));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static MatchModel Start(ulong seed = Seed, ChapterRules? rules = null)
        => MatchModel.Create(seed, Pack.Value, World.Value, Balanced, default, rules: rules ?? ChapterRules.Fief);

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

    private static (int Source, int Folk) SourceAndFolk(MatchModel match)
    {
        int source = Enumerable.Range(0, match.Economy.SettlementCount)
            .First(s => match.Economy.SettlementLite(s).Civilization == 0);
        int folk = Enumerable.Range(0, match.Economy.SettlementCount)
            .First(s => match.Economy.SettlementLite(s).Civilization == 1);
        return (source, folk);
    }

    private static int TileAtDistance(int origin, int distance)
        => Enumerable.Range(0, World.Value.TileCount)
            .Where(t => World.Value.TileAt(t).IsLand && HexGrid.Distance(origin, t, World.Value.Width) == distance)
            .First();

    private static byte[] Snapshot(MatchModel match)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) match.WriteSnapshot(writer);
        return stream.ToArray();
    }

    [Fact]
    public void Dynastic_inheritance_roll_is_keyed_and_measures_two_percent_over_seeded_keys()
    {
        const int samples = 10_000;
        int hits = 0;
        for (int seed = 1; seed <= samples; seed++)
        {
            bool draw = PoliticsSystem.RollDynasticInheritance((ulong)seed, 2, 17, 9);
            Assert.Equal(draw, PoliticsSystem.RollDynasticInheritance((ulong)seed, 2, 17, 9));
            hits += draw ? 1 : 0;
        }
        Assert.InRange(hits, 160, 240);
    }

    [Fact]
    public void Eligibility_requires_Dip08_an_alliance_and_an_own_or_ally_settlement_within_eight_tiles()
    {
        MatchModel match = Start();
        (int source, int folk) = SourceAndFolk(match);
        int home = match.Economy.SettlementLite(source).SiteTile;
        match.Politics.FixtureSetFreeFolkSettlement(folk, TileAtDistance(home, 8));

        Assert.False(match.Politics.FixtureDynasticEligible(0, folk));
        Grant(match, 0, "KEY-5", "DIP-07", "GOV-09", "DIP-08");
        Assert.False(match.Politics.FixtureDynasticEligible(0, folk), "a completed project without an alliance is insufficient");

        match.Politics.FixtureAlly(0, 1);
        Assert.True(match.Politics.FixtureDynasticEligible(0, folk), "eight hexes is included and an allied source qualifies");
        match.Politics.FixtureSetFreeFolkSettlement(folk, TileAtDistance(home, 9));
        Assert.False(match.Politics.FixtureDynasticEligible(0, folk), "nine hexes is outside the inheritance range");
    }

    [Fact]
    public void A_keyed_hit_waits_two_seasons_and_pending_confirmation_survives_snapshot()
    {
        ulong seed = Seed;
        MatchModel match = Start(seed);
        (int source, int folk) = SourceAndFolk(match);
        int sourceTile = match.Economy.SettlementLite(source).SiteTile;
        match.Politics.FixtureSetFreeFolkSettlement(folk, TileAtDistance(sourceTile, 4));
        match.Politics.FixtureAlly(0, 1);
        Grant(match, 0, "KEY-5", "DIP-07", "GOV-09", "DIP-08");
        int rollSeason = Enumerable.Range(1, 10_000)
            .First(season => PoliticsSystem.RollDynasticInheritance(seed, 0, folk, season));

        match.Politics.FixtureAdvanceDynasticInheritance(rollSeason);
        Assert.True(match.Politics.IsFreeFolk(folk));
        Assert.Equal((0, rollSeason + 2), match.Politics.FixtureDynasticPending(folk));

        byte[] saved = Snapshot(match);
        MatchModel resumed = Start(seed);
        using (var reader = new BinaryReader(new MemoryStream(saved))) resumed.ReadSnapshot(reader);
        Assert.Equal(match.ComputeChecksum(), resumed.ComputeChecksum());
        Assert.Equal((0, rollSeason + 2), resumed.Politics.FixtureDynasticPending(folk));
        resumed.Politics.FixtureAdvanceDynasticInheritance(rollSeason + 1);
        Assert.True(resumed.Politics.IsFreeFolk(folk), "pending inheritance is not an instant transfer");
        resumed.Politics.FixtureAdvanceDynasticInheritance(rollSeason + 2);
        Assert.False(resumed.Politics.IsFreeFolk(folk));
        Assert.Equal(0, resumed.Politics.SettlementCivOf(folk));
    }

    [Theory]
    [InlineData(ChapterMode.Bronze)]
    [InlineData(ChapterMode.Iron)]
    public void Inheritance_state_and_behavior_are_absent_in_Bronze_and_Iron(ChapterMode mode)
    {
        ChapterRules rules = mode == ChapterMode.Bronze ? ChapterRules.Bronze : ChapterRules.Iron;
        MatchModel match = Start(rules: rules);
        (int source, int folk) = SourceAndFolk(match);
        int sourceTile = match.Economy.SettlementLite(source).SiteTile;
        match.Politics.FixtureSetFreeFolkSettlement(folk, TileAtDistance(sourceTile, 4));
        match.Politics.FixtureAlly(0, 1);
        byte[] before = Snapshot(match);

        match.Politics.FixtureAdvanceDynasticInheritance(10);

        Assert.True(match.Politics.IsFreeFolk(folk));
        Assert.Equal(1, match.Politics.SettlementCivOf(folk));
        Assert.Equal(before, Snapshot(match));
        MatchModel restored = Start(rules: rules);
        using var reader = new BinaryReader(new MemoryStream(before));
        restored.ReadSnapshot(reader);
        Assert.Equal(match.ComputeChecksum(), restored.ComputeChecksum());
    }
}
