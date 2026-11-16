using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.IO;
using CivSim.Harness;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

public sealed class FiefWp7TraceTests
{
    private const ulong Seed = 20261116;
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner) Start(ulong seed, CivSim.Sim.Content.ContentPack pack)
    {
        GeneratedWorld world = WorldGenerator.Generate(seed);
        MatchModel match = MatchModel.Create(seed, pack, world, Balanced, default, rules: ChapterRules.Fief);
        return (match, new SimRunner(match));
    }

    private static void Grant(SimRunner runner, MatchModel match, int civ, params string[] projects)
    {
        foreach (string project in projects)
        {
            int index = match.Research.Catalog.IndexOf(project);
            Assert.True(index >= 0);
            if (match.Research.Catalog.Projects[index].Era == ChapterRules.Fief.LastResearchEra)
            {
                int key5 = match.Research.Catalog.IndexOf("KEY-5");
                if (!match.Research.IsDone(civ, key5))
                {
                    runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, key5, 0, 0));
                    runner.Advance(2);
                }
            }
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, index, 0, 0));
            runner.Advance(2);
        }
    }

    [Fact]
    public void GOV11_paired_conception_trace_shares_keyed_roll_and_is_checksum_neutral()
    {
        var pack = TestPack.WithNumeric("GOV-11");
        (MatchModel quiet, SimRunner quietRunner) = Start(Seed, pack);
        (MatchModel traced, SimRunner tracedRunner) = Start(Seed, pack);
        const int settlement = 0;
        quiet.Economy.FixtureSetTier(settlement, (int)SettlementTier.City);
        traced.Economy.FixtureSetTier(settlement, (int)SettlementTier.City);
        quiet.Economy.FixtureAddPeople(settlement, 2, 20);
        traced.Economy.FixtureAddPeople(settlement, 2, 20);
        Grant(quietRunner, quiet, 0, "GOV-11");
        Grant(tracedRunner, traced, 0, "GOV-11");
        var rows = new List<string>();
        traced.Economy.FiefWp7TraceObserverForDiagnostics = rows.Add;
        Assert.Equal(quiet.ComputeChecksum(), traced.ComputeChecksum());

        quietRunner.Advance(150);
        tracedRunner.Advance(150);

        Assert.Equal(quiet.ComputeChecksum(), traced.ComputeChecksum());
        Assert.Contains(rows, row => row.Contains("wp7-gov11-growth", System.StringComparison.Ordinal));
        string paired = rows.First(row => row.Contains("wp7-gov11-growth", System.StringComparison.Ordinal));
        Assert.Contains("controlConceptionChance=", paired, System.StringComparison.Ordinal);
        Assert.Contains("treatmentConceptionChance=", paired, System.StringComparison.Ordinal);
        Assert.Contains("keyedRoll=", paired, System.StringComparison.Ordinal);
        Assert.Contains("controlOutcome=", paired, System.StringComparison.Ordinal);
        Assert.Contains("treatmentOutcome=", paired, System.StringComparison.Ordinal);
        Assert.Contains("pairedBasis=same_figure_same_state_same_keyed_roll", paired, System.StringComparison.Ordinal);
        Assert.Contains("actualMode=treatment", paired, System.StringComparison.Ordinal);
        Assert.Contains("actualConceptionChance=", paired, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Gov11_suppression_is_checksum_neutral_and_removes_only_the_city_growth_multiplier()
    {
        var pack = TestPack.WithNumeric("GOV-11");
        (MatchModel treatment, SimRunner treatmentRunner) = Start(Seed, pack);
        (MatchModel control, SimRunner controlRunner) = Start(Seed, pack);
        treatment.Economy.FixtureSetTier(0, (int)SettlementTier.City);
        control.Economy.FixtureSetTier(0, (int)SettlementTier.City);
        treatment.Economy.FixtureAddPeople(0, 2, 20);
        control.Economy.FixtureAddPeople(0, 2, 20);
        Grant(treatmentRunner, treatment, 0, "GOV-11");
        Grant(controlRunner, control, 0, "GOV-11");
        Assert.Equal(treatment.ComputeChecksum(), control.ComputeChecksum());

        ulong checksum = control.ComputeChecksum();
        Assert.Equal(Fix64.FromRatio(11, 10), control.Economy.FixtureFiefCityGrowthMultiplier(0));
        control.Economy.FiefWp7SuppressGov11GrowthForDiagnostics = true;
        Assert.Equal(checksum, control.ComputeChecksum());
        Assert.Equal(Fix64.One, control.Economy.FixtureFiefCityGrowthMultiplier(0));
        Assert.Equal(Fix64.FromRatio(11, 10), treatment.Economy.FixtureFiefCityGrowthMultiplier(0));
    }

    [Fact]
    public void Full_city_year_trace_emits_population_endpoints_and_excludes_charter_transition()
    {
        var pack = TestPack.WithNumeric("GOV-11");
        (MatchModel match, SimRunner runner) = Start(Seed, pack);
        match.Economy.FixtureSetTier(0, (int)SettlementTier.City);
        match.Economy.FixtureAddPeople(0, 2, 20);
        Grant(runner, match, 0, "GOV-11");
        var output = new StringBuilder();
        FiefWp7Trace.State state = FiefWp7Trace.CaptureStart(match.Economy, match.Research);
        int startPopulation = match.Economy.Population(0);
        match.Economy.FixtureAddPeople(0, 1, 1);
        for (int season = 0; season < 4; season++)
            FiefWp7Trace.AppendSeason(output, match.Economy, match.Research, state, Seed, season, "treatment");
        string row = output.ToString().Split('\n').Single(line => line.StartsWith("wp7-gov11-city-year ", System.StringComparison.Ordinal));
        Assert.Contains("populationStart=" + startPopulation, row, System.StringComparison.Ordinal);
        Assert.Contains("populationEnd=" + match.Economy.Population(0), row, System.StringComparison.Ordinal);
        Assert.Contains("netPopulation=1", row, System.StringComparison.Ordinal);
        Assert.Contains("growthPerMille=", row, System.StringComparison.Ordinal);
        Assert.Contains("gov11Mode=treatment", row, System.StringComparison.Ordinal);

        (MatchModel transitioned, SimRunner transitionedRunner) = Start(Seed + 1, pack);
        transitioned.Economy.FixtureSetTier(0, (int)SettlementTier.City);
        transitioned.Economy.FixtureAddPeople(0, 2, 20);
        FiefWp7Trace.State transitionState = FiefWp7Trace.CaptureStart(transitioned.Economy, transitioned.Research);
        Grant(transitionedRunner, transitioned, 0, "GOV-11");
        var transitionOutput = new StringBuilder();
        for (int season = 0; season < 4; season++)
            FiefWp7Trace.AppendSeason(transitionOutput, transitioned.Economy, transitioned.Research, transitionState, Seed + 1, season, "treatment");
        Assert.Contains(transitionOutput.ToString().Split('\n'), line =>
            line.StartsWith("wp7-gov11-city-year-excluded ", System.StringComparison.Ordinal) &&
            line.Contains("exclusionReason=gov11_status_changed", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Partial_city_year_is_explicitly_right_censored_at_match_end()
    {
        (MatchModel match, _) = Start(Seed, TestPack.WithNumeric("GOV-11"));
        match.Economy.FixtureSetTier(0, (int)SettlementTier.City);
        match.Economy.FixtureAddPeople(0, 2, 20);
        FiefWp7Trace.State state = FiefWp7Trace.CaptureStart(match.Economy, match.Research);
        var rows = new StringBuilder();
        FiefWp7Trace.AppendSeason(rows, match.Economy, match.Research, state, Seed, 0, "treatment");
        FiefWp7Trace.AppendSeason(rows, match.Economy, match.Research, state, Seed, 1, "treatment");
        FiefWp7Trace.AppendTerminal(rows, match.Economy, Seed, season: 2,
            chapterEnded: false, endReason: "None", horizonReached: true, state, match.Research);
        Assert.Contains(rows.ToString().Split('\n'), line =>
            line.StartsWith("wp7-gov11-city-year-censored ", System.StringComparison.Ordinal) &&
            line.Contains("observedSeasons=2", System.StringComparison.Ordinal) &&
            line.Contains("censorReason=match_ended_before_four_seasons", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Same_snapshot_pair_emits_equal_start_checksums_and_four_season_population_outcomes()
    {
        var pack = TestPack.WithNumeric("GOV-11");
        (MatchModel treatment, SimRunner runner) = Start(Seed, pack);
        treatment.Economy.FixtureSetTier(0, (int)SettlementTier.City);
        treatment.Economy.FixtureAddPeople(0, 2, 20);
        Grant(runner, treatment, 0, "GOV-11");

        MatchModel control = MatchModel.Create(Seed, pack, WorldGenerator.Generate(Seed), Balanced, default,
            rules: ChapterRules.Fief);
        using (var snapshot = new MemoryStream())
        {
            using (var writer = new BinaryWriter(snapshot, System.Text.Encoding.UTF8, leaveOpen: true))
                treatment.WriteSnapshot(writer);
            snapshot.Position = 0;
            using var reader = new BinaryReader(snapshot, System.Text.Encoding.UTF8, leaveOpen: true);
            control.ReadSnapshot(reader);
        }
        Assert.Equal(treatment.ComputeChecksum(), control.ComputeChecksum());
        control.Economy.FiefWp7SuppressGov11GrowthForDiagnostics = true;
        Assert.Equal(treatment.ComputeChecksum(), control.ComputeChecksum());

        FiefWp7Trace.PairedWindow pair = FiefWp7Trace.BeginPairedWindow(treatment, control, startSeason: 0);
        int startPopulation = treatment.Economy.Population(0);
        treatment.Economy.FixtureAddPeople(0, 1, 1);
        for (int season = 0; season < 4; season++) pair.ObserveSeason(treatment, control);
        var output = new StringBuilder();
        pair.Append(output, Seed, treatment, control, endSeason: 3, fullYear: true,
            pairEndReason: "four_seasons_complete");
        string row = output.ToString().Split('\n').Single(line => line.StartsWith("wp7-gov11-city-year-pair ", System.StringComparison.Ordinal));
        Assert.Contains("sameStartingState=true", row, System.StringComparison.Ordinal);
        Assert.Contains("treatmentStartChecksum=", row, System.StringComparison.Ordinal);
        Assert.Contains("controlStartChecksum=", row, System.StringComparison.Ordinal);
        Assert.Contains("treatmentPopulationStart=" + startPopulation, row, System.StringComparison.Ordinal);
        Assert.Contains("controlPopulationStart=" + startPopulation, row, System.StringComparison.Ordinal);
        Assert.Contains("treatmentPopulationEnd=" + (startPopulation + 1), row, System.StringComparison.Ordinal);
        Assert.Contains("controlPopulationEnd=" + startPopulation, row, System.StringComparison.Ordinal);
        Assert.Contains("pairedBasis=same_snapshot_four_season_branch", row, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Terminal_trace_marks_live_settlements_as_censored_at_match_end()
    {
        (MatchModel match, _) = Start(Seed, TestPack.WithNumeric("GOV-11"));
        var rows = new StringBuilder();

        FiefWp7Trace.AppendTerminal(rows, match.Economy, Seed, season: 40,
            chapterEnded: false, endReason: "None", horizonReached: true);

        string[] lines = rows.ToString().Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(lines, line => line.Contains("wp7-match-terminal", System.StringComparison.Ordinal) &&
            line.Contains("matchStatus=horizon_censored", System.StringComparison.Ordinal));
        Assert.Equal(match.Economy.SettlementCount,
            lines.Count(line => line.Contains("wp7-settlement-terminal", System.StringComparison.Ordinal)));
        Assert.Contains(lines, line => line.Contains("wp7-settlement-terminal", System.StringComparison.Ordinal) &&
            line.Contains("live=true", System.StringComparison.Ordinal) &&
            line.Contains("settlementStatus=right_censored", System.StringComparison.Ordinal));

        rows.Clear();
        FiefWp7Trace.AppendTerminal(rows, match.Economy, Seed, season: 12,
            chapterEnded: true, endReason: "RivalCountdown", horizonReached: false);
        Assert.Contains(rows.ToString().Split('\n'), line => line.Contains("matchStatus=chapter_end", System.StringComparison.Ordinal) &&
            line.Contains("endReason=RivalCountdown", System.StringComparison.Ordinal));
    }
}
