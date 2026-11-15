using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>The arms readout the Diplomacy drawer shows (docs/MVP/13 §7.1): base weapon from research, kits only as far as fighters can carry them, own settlements only.</summary>
public sealed class ArmsViewTests
{
    private const ulong Seed = 20261024;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric("KEY-4", "WAR-01", "WAR-04", "HNT-06"));
    private static readonly Lazy<ResearchCatalog> Catalog = new(() => new ResearchCatalog(Pack.Value));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static MatchFrame Frame(MatchModel match, int viewer)
    {
        var frame = new MatchFrame();
        new FrameBuilder(match, viewer).Build(match, frame);
        return frame;
    }

    [Fact]
    public void Research_changes_the_base_weapon_and_only_own_settlements_report_fighters()
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default);
        var runner = new SimRunner(match);
        runner.Advance(5);
        MatchFrame before = Frame(match, 0);
        Assert.Equal(0, before.Arms.BaseTier);
        Assert.Equal(60, before.Arms.BaseHundredths);

        foreach (string project in new[] { "WAR-01", "HNT-06" })
            runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, Catalog.Value.IndexOf(project), 0, 0));
        runner.Advance(2);
        MatchFrame after = Frame(match, 0);
        Assert.Equal(5, after.Arms.BaseTier);
        Assert.Equal(180, after.Arms.BaseHundredths);
        Assert.NotEqual(0, after.Arms.Research & ArmsFrame.HasBow);

        int fighters = 0;
        for (int w = 0; w < after.SettlementCount; w++)
        {
            bool own = after.SettlementCiv[w] == 0 && (after.SettlementFlags[w] & (ushort)SettlementViewFlags.Own) != 0;
            if (!own) Assert.Equal(0, after.Arms.Rows[w].Fighters);
            fighters += after.Arms.Rows[w].Fighters;
        }
        Assert.Equal(fighters, after.Arms.Total.Fighters);
        Assert.True(after.Arms.Total.Fighters > 0);
        // No metal arms exist in this chapter, so nothing is carried.
        Assert.Equal(0, after.Arms.Total.StockCopper + after.Arms.Total.Copper);
    }
}
