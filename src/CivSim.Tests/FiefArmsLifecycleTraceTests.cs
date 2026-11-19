using System.Linq;
using System.Text;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;
using CivSim.Harness;

namespace CivSim.Tests;

public sealed class FiefArmsLifecycleTraceTests
{
    [Fact]
    public void FlagIsRecognizedOnlyWhenPresent()
    {
        Assert.True(FiefArmsLifecycleTrace.IsRequested(["--pack", "p", "--fief-arms-lifecycle-trace"]));
        Assert.False(FiefArmsLifecycleTrace.IsRequested(["--pack", "p"]));
    }

    [Fact]
    public void SeasonObservationIsReadOnlyAndMarksCrucibleTierUnsupported()
    {
        const ulong seed = 20261118;
        EconomyArchetype[] seats = Enumerable.Repeat(EconomyArchetype.Balanced, 4).ToArray();
        MatchModel match = MatchModel.Create(seed, TestPack.Pack, WorldGenerator.Generate(seed), seats, default, rules: ChapterRules.Fief);
        ulong before = match.ComputeChecksum();
        var rows = new StringBuilder();

        new FiefArmsLifecycleTrace().AppendSeason(rows, seed, 3, match.Economy, match.Politics, match.Research);

        Assert.Equal(before, match.ComputeChecksum());
        Assert.Contains("fief-arms-coverage", rows.ToString());
        Assert.Contains("good=G-ARMS5 stockable=true roster_supported=true", rows.ToString());
        Assert.Contains("families=copper,bronze,iron,steel,crucible,bow,crossbow,longbow,chariot,engine,cannon,powder", rows.ToString());
        Assert.Contains("researchFlags=MIN-03,", rows.ToString());
        Assert.Contains("siegeCannons=", rows.ToString());
        Assert.Contains("fief-arms-summary", rows.ToString());
        Assert.Contains("chariots=", rows.ToString());
        Assert.Contains("chariotCrew=", rows.ToString());
    }

    [Fact]
    public void Bow_lifecycle_separates_hunting_stock_carriers_and_levy_readiness()
    {
        const ulong seed = 20261119;
        ContentPack pack = TestPack.WithNumeric("HNT-06", "WAR-03");
        EconomyArchetype[] seats = Enumerable.Repeat(EconomyArchetype.Balanced, 4).ToArray();
        MatchModel match = MatchModel.Create(seed, pack, WorldGenerator.Generate(seed), seats, default, rules: ChapterRules.Fief);
        var runner = new SimRunner(match);
        var rows = new StringBuilder();
        var trace = new FiefArmsLifecycleTrace();
        ResearchCatalog catalog = new(pack);

        runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, catalog.IndexOf("HNT-06"), 0, 0));
        runner.Advance(1);
        match.Economy.FixtureSetGoodStock(0, "G-BOW", Fix64.One);
        trace.AppendSeason(rows, seed, 1, match.Economy, match.Politics, match.Research);
        Assert.Contains("kind=carrier civ=0 settlement=0 tier=warbow", rows.ToString());
        Assert.Contains("kind=whole-stock civ=0 settlement=0 tier=warbow", rows.ToString());
        Assert.DoesNotContain("kind=levy-bow-ready civ=0", rows.ToString());

        runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, catalog.IndexOf("WAR-03"), 0, 0));
        runner.Advance(1);
        trace.AppendSeason(rows, seed, 2, match.Economy, match.Politics, match.Research);
        Assert.Contains("kind=research civ=0 project=WAR-03", rows.ToString());
        Assert.Contains("kind=levy-bow-ready civ=0 season=2 requires=HNT-06+WAR-03", rows.ToString());
    }
}
