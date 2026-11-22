using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Ocean lanes in the Discovery frame (docs/MVP/39 §7.2, M13 WP-E6-11): a visible trade route whose trip crosses deep ocean. Nothing is invented.</summary>
public sealed class DiscoveryLaneTests
{
    private const ulong Seed = 20261020;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack =
        new(() => TestPack.WithNumeric("WAY-08", "WAY-09", "KEY-6", "SEA-11", "SEA-12"));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static MatchFrame Frame(MatchModel match, int viewer)
    {
        var frame = new MatchFrame();
        new FrameBuilder(match, viewer).Build(match, frame);
        return frame;
    }

    private static void Grant(SimRunner runner, MatchModel match, int civ, params string[] projects)
    {
        foreach (string project in projects)
        {
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, match.Research.Catalog.IndexOf(project), 0, 0));
            runner.Advance(2);
        }
    }

    [Fact]
    public void A_route_that_crosses_deep_ocean_is_a_lane_only_for_a_civilization_with_ocean_reach()
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: ChapterRules.Discovery);
        var runner = new SimRunner(match);
        int Home(int civ) => Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == civ);
        Grant(runner, match, 0, "KEY-5", "SEA-04", "KEY-6", "SEA-12");
        Assert.True(match.Politics.HasOceanReach(0));

        int home = Home(0), partner = -1;
        for (int c = 1; c < 4 && partner < 0; c++)
            if (match.Politics.DeepSeaTilesOnRoute(0, match.Economy.SettlementLite(home).SiteTile, match.Economy.SettlementLite(Home(c)).SiteTile) > 0) partner = Home(c);
        Assert.True(partner >= 0, "seed 20261020 has a rival home across deep water from civ 0's");

        var compact = match.Politics.FixtureAddCompact(0, match.Economy.SettlementLite(partner).Civilization, CivSim.Sim.Politics.TradeGood.Tin);
        compact.LastSeller = home; compact.LastBuyer = partner;
        compact.LastVolume = CivSim.Sim.Numerics.Fix64.Half;

        DiscoveryFrame d = Frame(match, 0).Discovery;
        Assert.Equal(1, d.LaneCount);
        Assert.Equal(match.Economy.SettlementLite(home).SiteTile, d.LaneFromTile[0]);
        Assert.Equal(match.Economy.SettlementLite(partner).SiteTile, d.LaneToTile[0]);
        Assert.Equal(0, d.LaneCiv[0]);
        Assert.Equal(500, d.LaneVolume[0]);

        MatchModel plain = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: ChapterRules.Discovery);
        var plainCompact = plain.Politics.FixtureAddCompact(0, plain.Economy.SettlementLite(partner).Civilization, CivSim.Sim.Politics.TradeGood.Tin);
        plainCompact.LastSeller = home; plainCompact.LastBuyer = partner;
        Assert.Equal(0, Frame(plain, 0).Discovery.LaneCount); // no SEA-12, no ocean lane: the trip is not one this civilization can sail in the first place
    }
}
