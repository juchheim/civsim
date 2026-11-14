using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Fief WAY-08 halves existing open-sea ship-loss risk without changing route eligibility or land travel.</summary>
public sealed class FiefVoyageTests
{
    private const ulong Seed = 20261019;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric("WAY-08"));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner, int Home) Start(ChapterRules? rules = null)
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules ?? ChapterRules.Fief);
        var runner = new SimRunner(match);
        int home = Enumerable.Range(0, match.Economy.SettlementCount)
            .First(s => match.Economy.SettlementLite(s).Civilization == 0);
        return (match, runner, match.Economy.SettlementLite(home).SiteTile);
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
    public void Way08_halves_Fief_open_sea_risk_and_keeps_route_and_loss_rolls_deterministic()
    {
        (MatchModel match, SimRunner runner, int home) = Start();
        Grant(runner, match, 0, "KEY-5", "SEA-04");
        PoliticsSystem politics = match.Politics;
        int destination = Enumerable.Range(0, World.Value.TileCount)
            .First(t => World.Value.TileAt(t).IsLand && politics.SeaTilesOnRoute(0, home, t) > 0);
        int seaTiles = politics.SeaTilesOnRoute(0, home, destination);
        int baseRisk = Math.Min(PoliticsSystem.VoyageMaxRiskPermille,
            PoliticsSystem.VoyageBaseRiskPermille + PoliticsSystem.VoyageRiskPerSeaTile * seaTiles);
        Assert.Equal(baseRisk, politics.VoyageRiskPermille(0, home, destination));

        const int samples = 2000;
        var baselineLosses = new bool[samples];
        for (int season = 0; season < samples; season++)
        {
            baselineLosses[season] = politics.VoyageLost(0, home, destination, season);
            Assert.Equal(baselineLosses[season], politics.VoyageLost(0, home, destination, season));
        }

        // WAY-08 is a numeric draft-row fixture; this does not promote any content row.
        Grant(runner, match, 0, "WAY-08");
        int reducedRisk = politics.VoyageRiskPermille(0, home, destination);
        Assert.Equal(baseRisk / 2, reducedRisk);
        Assert.True(politics.CanReach(0, home, destination), "navigation changes losses, not the existing sailing gate");
        Assert.Equal(seaTiles, politics.SeaTilesOnRoute(0, home, destination));

        for (int season = 0; season < samples; season++)
        {
            bool after = politics.VoyageLost(0, home, destination, season);
            Assert.Equal(after, politics.VoyageLost(0, home, destination, season));
            Assert.False(after && !baselineLosses[season], "halving risk must only remove losses for the same deterministic roll");
        }

        int mainland = Enumerable.Range(0, World.Value.TileCount)
            .First(t => t != home && politics.SeaTilesOnRoute(0, home, t) == 0);
        Assert.Equal(0, politics.VoyageRiskPermille(0, home, mainland));
    }

    [Fact]
    public void Way08_stays_inert_in_Iron_even_with_a_numeric_E5_test_row()
    {
        (MatchModel match, SimRunner runner, int home) = Start(ChapterRules.Iron);
        Grant(runner, match, 0, "KEY-4", "SEA-04");
        PoliticsSystem politics = match.Politics;
        int destination = Enumerable.Range(0, World.Value.TileCount)
            .First(t => World.Value.TileAt(t).IsLand && politics.SeaTilesOnRoute(0, home, t) > 0);
        int risk = politics.VoyageRiskPermille(0, home, destination);

        Grant(runner, match, 0, "WAY-08"); // the Iron ceiling refuses this era-five completion

        Assert.False(match.Research.IsDone(0, match.Research.Catalog.IndexOf("WAY-08")));
        Assert.Equal(risk, politics.VoyageRiskPermille(0, home, destination));
    }
}
