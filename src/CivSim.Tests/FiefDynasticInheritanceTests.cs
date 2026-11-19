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
        => Grant(new SimRunner(match), match, civ, projects);

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

    private static (MatchModel Quiet, MatchModel Traced, int Folk) ConfigureTwoEligibleClaimants()
    {
        MatchModel quiet = Start();
        MatchModel traced = Start();
        (int source, int folk) = SourceAndFolk(quiet);
        int sourceTile = quiet.Economy.SettlementLite(source).SiteTile;
        int alliedHome = Enumerable.Range(0, quiet.Economy.SettlementCount)
            .First(s => quiet.Economy.SettlementLite(s).Civilization == 2);
        int alliedTile = TileAtDistance(sourceTile, 4);
        int folkTile = TileAtDistance(sourceTile, 2);

        foreach (MatchModel match in new[] { quiet, traced })
        {
            match.Economy.FixtureMoveSite(alliedHome, alliedTile);
            match.Politics.FixtureRefreshPilgrimageSettlements();
            match.Politics.FixtureSetFreeFolkSettlement(folk, folkTile);
            match.Politics.FixtureAlly(0, 2);
            var runner = new SimRunner(match);
            Grant(runner, match, 0, "KEY-5", "DIP-07", "GOV-09", "DIP-08");
            Grant(runner, match, 2, "KEY-5", "DIP-07", "GOV-09", "DIP-08");
            Assert.True(match.Politics.FixtureDynasticEligible(0, folk));
            Assert.True(match.Politics.FixtureDynasticEligible(2, folk));
        }
        return (quiet, traced, folk);
    }

    private static string[] DenominatorRows(System.Collections.Generic.IEnumerable<string> rows, int settlementId)
        => rows.Where(row => row.Contains("event=settlement_season", StringComparison.Ordinal) &&
                             row.Contains($"settlement={settlementId} ", StringComparison.Ordinal))
               .ToArray();

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

    [Fact]
    public void Trace_emits_one_settlement_season_denominator_with_multiple_eligible_claimants_and_is_checksum_neutral()
    {
        (MatchModel quiet, MatchModel traced, int folk) = ConfigureTwoEligibleClaimants();
        var rows = new System.Collections.Generic.List<string>();
        traced.Politics.FiefWp7TraceObserverForDiagnostics = rows.Add;
        Assert.Equal(quiet.ComputeChecksum(), traced.ComputeChecksum());

        quiet.Politics.FixtureAdvanceDynasticInheritance(40);
        traced.Politics.FixtureAdvanceDynasticInheritance(40);

        Assert.Equal(quiet.ComputeChecksum(), traced.ComputeChecksum());
        int folkId = traced.Economy.SettlementLite(folk).Id;
        string row = Assert.Single(DenominatorRows(rows, folkId));
        Assert.Contains($"settlementSeasonKey=40:{folkId}", row, StringComparison.Ordinal);
        Assert.Contains("eligibleClaimantCount=2", row, StringComparison.Ordinal);
        Assert.Contains("eligibleClaimantIds=0,2", row, StringComparison.Ordinal);
        Assert.Contains("classification=eligible_exposure", row, StringComparison.Ordinal);

        string[] allDenominators = rows.Where(r => r.Contains("event=settlement_season", StringComparison.Ordinal)).ToArray();
        string[] keys = allDenominators.Select(r => r.Split("settlementSeasonKey=", 2)[1].Split(' ')[0]).ToArray();
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Trace_emits_a_no_eligible_claimant_row_for_a_live_free_folk_settlement()
    {
        MatchModel match = Start();
        (int _, int folk) = SourceAndFolk(match);
        int sourceTile = match.Economy.SettlementLite(0).SiteTile;
        match.Politics.FixtureSetFreeFolkSettlement(folk, TileAtDistance(sourceTile, 4));
        var rows = new System.Collections.Generic.List<string>();
        match.Politics.FiefWp7TraceObserverForDiagnostics = rows.Add;

        match.Politics.FixtureAdvanceDynasticInheritance(9);

        int folkId = match.Economy.SettlementLite(folk).Id;
        string row = Assert.Single(DenominatorRows(rows, folkId));
        Assert.Contains("eligibleClaimantCount=0", row, StringComparison.Ordinal);
        Assert.Contains("eligibleClaimantIds=none", row, StringComparison.Ordinal);
        Assert.Contains("classification=no_eligible_claimant", row, StringComparison.Ordinal);
    }

    [Fact]
    public void Fief_snapshot_refreshes_settlement_count_before_reading_dynastic_state()
    {
        MatchModel match = Start();
        int site = Enumerable.Range(0, World.Value.TileCount)
            .First(tile => World.Value.TileAt(tile).IsLand &&
                           Enumerable.Range(0, match.Economy.SettlementCount)
                               .All(settlement => match.Economy.SettlementLite(settlement).SiteTile != tile));
        int added = match.Economy.FoundSettlement(1, site, Array.Empty<long>());
        match.Politics.FixtureSetFreeFolkSettlement(added, site);

        byte[] saved = Snapshot(match);
        MatchModel resumed = Start();
        using var reader = new BinaryReader(new MemoryStream(saved));
        resumed.ReadSnapshot(reader);

        Assert.Equal(match.Economy.SettlementCount, resumed.Economy.SettlementCount);
        Assert.Equal(match.ComputeChecksum(), resumed.ComputeChecksum());
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
