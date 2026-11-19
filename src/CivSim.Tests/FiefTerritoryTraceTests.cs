using System;
using System.Linq;
using System.Text;
using CivSim.Harness;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;
using Xunit;

namespace CivSim.Tests;

public sealed class FiefTerritoryTraceTests
{
    [Fact]
    public void Territory_trace_append_is_checksum_neutral_and_emits_all_civilizations()
    {
        const ulong seed = 20261116;
        GeneratedWorld world = WorldGenerator.Generate(seed);
        EconomyArchetype[] archetypes = Enumerable.Repeat(EconomyArchetype.Balanced, 4).ToArray();
        MatchModel plain = MatchModel.Create(seed, TestPack.Pack, world, archetypes, default, rules: ChapterRules.Fief);
        MatchModel traced = MatchModel.Create(seed, TestPack.Pack, world, archetypes, default, rules: ChapterRules.Fief);
        var plainRunner = new SimRunner(plain);
        var tracedRunner = new SimRunner(traced);
        var state = new FiefTerritoryTrace.State(traced.Politics.Territory, archetypes.Length);
        var rows = new StringBuilder();
        FiefTerritoryTrace.Append(rows, seed, 0, traced, state);
        Assert.Equal(plain.ComputeChecksum(), traced.ComputeChecksum());

        for (int season = 1; season <= 4; season++)
        {
            plainRunner.Advance(150);
            tracedRunner.Advance(150);
            FiefTerritoryTrace.Append(rows, seed, season, traced, state);
            Assert.Equal(plain.ComputeChecksum(), traced.ComputeChecksum());
        }

        string[] traceRows = rows.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(5 * archetypes.Length, traceRows.Length);
        Assert.Contains(traceRows, row => row.Contains("season=0", StringComparison.Ordinal) &&
            row.Contains("dominionExpectedGrossAnnual=na", StringComparison.Ordinal) && row.Contains("dominionObservedNetAnnual=na", StringComparison.Ordinal));
        Assert.Contains(traceRows, row => row.Contains("dominionExpectedGrossAnnual=", StringComparison.Ordinal) && !row.Contains("dominionExpectedGrossAnnual=na", StringComparison.Ordinal));
        Assert.Contains(traceRows, row => row.Contains("citySharePercent=", StringComparison.Ordinal) && row.Contains("dominionEndImplemented=false", StringComparison.Ordinal));
        Assert.All(traceRows, row =>
        {
            Assert.Contains("sampleTick=", row, StringComparison.Ordinal);
            Assert.Contains("samplePhase=", row, StringComparison.Ordinal);
            Assert.Contains("ownKey4CompletedTick=", row, StringComparison.Ordinal);
            Assert.Contains("ownKey5CompletedTick=", row, StringComparison.Ordinal);
            Assert.Contains("mapFillOwnedTilePercent=", row, StringComparison.Ordinal);
            Assert.Contains("civOwnedHabitableSharePercent=", row, StringComparison.Ordinal);
            Assert.Contains("dominionPoints=", row, StringComparison.Ordinal);
            Assert.Contains("citySharePercent=", row, StringComparison.Ordinal);
        });
        Assert.All(traceRows.Where(row => row.Contains("season=0", StringComparison.Ordinal)), row =>
        {
            Assert.Contains("samplePhase=pre-own-key4", row, StringComparison.Ordinal);
            Assert.Contains("ownKey4Sample=false", row, StringComparison.Ordinal);
            Assert.Contains("ownKey5Sample=false", row, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Fief_territory_observations_use_weighted_map_fill_population_city_share_and_claimed_land_dominion()
    {
        Assert.Equal(50.0, FiefTerritoryTrace.PercentOf(5_000, 10_000));
        Assert.Equal(25.0, FiefTerritoryTrace.CitySharePercent(25, 100));
        Assert.Null(FiefTerritoryTrace.CitySharePercent(0, 0));
        Assert.Equal(Fix64.FromInt(25), FiefTerritoryTrace.ExpectedGrossAnnualDominion(25, 100));
        Assert.Equal(Fix64.Zero, FiefTerritoryTrace.ExpectedGrossAnnualDominion(25, 0));
    }

    [Theory]
    [InlineData(5_000, 2_500, true)]
    [InlineData(5_000, 2_501, false)]
    [InlineData(4_999, 2_000, false)]
    [InlineData(6_000, 3_000, true)]
    public void Dominion_candidate_uses_exact_habitable_weights_and_the_documented_threshold(int leader, int runnerUp, bool expected)
    {
        var candidate = new FiefTerritoryTrace.DominionCandidate(2, leader, runnerUp, 10_000);
        Assert.Equal(expected, candidate.Qualifies);
    }

    [Fact]
    public void Dominion_candidate_selects_largest_direct_land_share_and_tracks_twenty_seasons()
    {
        int[] ownedWeights = [1_000, 2_500, 5_000, 1_500];
        FiefTerritoryTrace.DominionCandidate? candidate = FiefTerritoryTrace.FindDominionCandidate(ownedWeights, 10_000);
        Assert.NotNull(candidate);
        Assert.Equal(2, candidate.Value.Civilization);
        Assert.Equal(2_500, candidate.Value.RunnerUpOwnedWeight);
        Assert.True(candidate.Value.Qualifies);

        (int civ, int seasons) = (-1, 0);
        (civ, seasons) = FiefTerritoryTrace.AdvanceCandidateStreak(civ, seasons, candidate.Value.Civilization, qualifies: true);
        Assert.Equal(0, seasons); // first sample establishes the candidate but no full interval has elapsed
        Assert.False(FiefTerritoryTrace.DominionRuleSatisfied(qualifies: true, consecutiveSeasons: seasons));
        for (int elapsedSeason = 1; elapsedSeason <= FiefTerritoryTrace.SeasonsForDominionRule; elapsedSeason++)
        {
            (civ, seasons) = FiefTerritoryTrace.AdvanceCandidateStreak(civ, seasons, candidate.Value.Civilization, qualifies: true);
            Assert.Equal(elapsedSeason, seasons);
            Assert.Equal(elapsedSeason >= FiefTerritoryTrace.SeasonsForDominionRule,
                FiefTerritoryTrace.DominionRuleSatisfied(qualifies: true, consecutiveSeasons: seasons));
        }
        Assert.False(FiefTerritoryTrace.DominionRuleSatisfied(qualifies: false, consecutiveSeasons: seasons));
        (civ, seasons) = FiefTerritoryTrace.AdvanceCandidateStreak(civ, seasons, candidate.Value.Civilization, qualifies: false);
        Assert.Equal((-1, 0), (civ, seasons));
    }

    [Fact]
    public void Dominion_candidate_does_not_assign_an_arbitrary_leader_to_tied_or_unowned_land()
    {
        Assert.Null(FiefTerritoryTrace.FindDominionCandidate([0, 0, 0, 0], 10_000));
        Assert.Null(FiefTerritoryTrace.FindDominionCandidate([2_000, 5_000, 5_000, 0], 10_000));
    }

    [Fact]
    public void Dominion_streak_resets_when_the_qualifying_leader_changes()
    {
        Assert.Equal((1, 0), FiefTerritoryTrace.AdvanceCandidateStreak(0, 12, 1, qualifies: true));
        Assert.Equal((-1, 0), FiefTerritoryTrace.AdvanceCandidateStreak(0, 12, 1, qualifies: false));
    }
}
