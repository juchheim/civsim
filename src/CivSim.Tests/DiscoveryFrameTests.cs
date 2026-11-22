using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>The Discovery view frame (M13 WP-E6-11 slice 1, docs/MVP/38 §8): read-only, own towns only, inactive outside a Discovery match.</summary>
public sealed class DiscoveryFrameTests
{
    private const ulong Seed = 20261024;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static MatchModel Match(ChapterRules rules) => MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced, default, rules: rules);

    private static MatchFrame Frame(MatchModel match, int viewer)
    {
        var frame = new MatchFrame();
        new FrameBuilder(match, viewer).Build(match, frame);
        return frame;
    }

    private static int Home(MatchModel match, int civ)
        => Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == civ && !match.Politics.IsFreeFolk(s));

    private static int Slot(MatchFrame frame, int settlement)
    {
        for (int w = 0; w < frame.SettlementCount; w++) if (frame.SettlementIndex[w] == settlement) return w;
        return -1;
    }

    [Fact]
    public void A_spectator_frame_builds_in_Discovery_with_no_realm_block_and_no_towns()
    {
        MatchModel match = Match(ChapterRules.Discovery);
        new SimRunner(match).Advance(5);
        MatchFrame frame = Frame(match, -1); // viewer -1: observing, owning nothing (it once threw an overflow in HasOceanReach)
        Assert.True(frame.Discovery.Active);
        Assert.False(frame.Discovery.OceanReach);
        Assert.Equal(0, frame.Discovery.Colonies);
        Assert.All(frame.Discovery.Engines, e => Assert.Equal(0, e));
    }

    [Fact]
    public void The_frame_is_inactive_outside_Discovery_and_shows_what_stands_in_the_viewers_own_towns_only()
    {
        MatchModel fief = Match(ChapterRules.Fief);
        new SimRunner(fief).Advance(5);
        Assert.False(Frame(fief, 0).Discovery.Active);

        MatchModel match = Match(ChapterRules.Discovery);
        var runner = new SimRunner(match);
        runner.Advance(5);
        int own = Home(match, 0), other = Home(match, 1);
        match.Economy.FixtureSetIronWork(own, "atmospheric_engine", 2);
        match.Economy.FixtureSetIronWork(own, "coke_oven", 1);
        match.Economy.FixtureSetIronWork(own, "navigation_canal", 1);
        match.Economy.FixtureSetIronWork(own, "factory", 1);
        match.Economy.FixtureSetIronWork(own, "bastion_fort", 1);
        match.Economy.FixtureSetIronWork(other, "atmospheric_engine", 3);
        match.Economy.FixtureAddDiscoveryGood(own, "G-COAL", Fix64.FromInt(40));

        MatchFrame frame = Frame(match, 0);
        DiscoveryFrame d = frame.Discovery;
        Assert.True(d.Active);
        int w = Slot(frame, own), v = Slot(frame, other);
        Assert.True(w >= 0);
        Assert.Equal(2, d.Engines[w]);
        Assert.Equal(1, d.Ovens[w]);
        Assert.Equal(1, d.Canal[w]);
        Assert.Equal(1, d.Factory[w]);
        Assert.Equal(1, d.Bastion[w]);
        Assert.True(d.CoalLitres[w] >= Fix64.FromInt(40).Raw);
        Assert.Equal(0, d.Colony[w]);
        if (v >= 0) { Assert.Equal(0, d.Engines[v]); Assert.Equal(0L, d.CoalLitres[v]); }
        Assert.False(d.OceanReach);
        Assert.Equal(0, d.Colonies);
        Assert.Equal(0, d.Key7Permille);
    }

    [Fact]
    public void Building_frames_never_changes_the_simulation()
    {
        MatchModel framed = Match(ChapterRules.Discovery), plain = Match(ChapterRules.Discovery);
        var a = new SimRunner(framed); var b = new SimRunner(plain);
        var frame = new MatchFrame();
        var builder = new FrameBuilder(framed, 0);
        for (int i = 0; i < 20; i++)
        {
            a.Advance(150); b.Advance(150);
            builder.Build(framed, frame);
        }
        Assert.Equal(plain.ComputeChecksum(), framed.ComputeChecksum());
    }
}
