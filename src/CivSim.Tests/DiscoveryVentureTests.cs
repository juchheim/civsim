using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Joint-stock ventures and insurance, pooled goods only (M13 WP-E6-6 slice 6, decision D12, docs/MVP/38 §5.2). Discovery chapter only.</summary>
public sealed class DiscoveryVentureTests
{
    private const ulong Seed = 20261024;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric(
        "KEY-4", "KEY-6", "SEA-07", "SEA-11", "SEA-12", "TRD-10", "TRD-11", "WAY-09"));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static int Home(MatchModel match, int civ)
        => Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == civ && !match.Politics.IsFreeFolk(s));

    private static void Grant(SimRunner runner, MatchModel match, int civ, params string[] projects)
    {
        foreach (string project in projects)
        {
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, match.Research.Catalog.IndexOf(project), 0, 0));
            runner.Advance(2);
        }
    }

    /// <summary>Civilization 0 with a harbor town, and a second harbor town across the sea (civilization 2's home handed over), sailing with SEA-12.</summary>
    private static (MatchModel Match, SimRunner Runner, int Home, int Second) Rig(ChapterRules rules, bool secondHarbor = true)
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules);
        var runner = new SimRunner(match);
        Grant(runner, match, 0, "KEY-4", "KEY-5", "KEY-6", "SEA-04", "SEA-07", "SEA-12");
        int home = Home(match, 0), second = Home(match, 2);
        match.Economy.FixtureSetCivilizationAndResidents(second, 0);
        match.Economy.FixtureSetTier(home, 1);
        match.Economy.FixtureSetTier(second, 1);
        match.Economy.FixtureBuildIronStructure(home, "harbor");
        if (secondHarbor) match.Economy.FixtureBuildIronStructure(second, "harbor");
        return (match, runner, home, second);
    }

    [Fact]
    public void Joint_stock_needs_TRD10_and_two_harbor_towns_to_pool_and_speeds_only_sea_ventures()
    {
        (MatchModel match, SimRunner runner, int home, _) = Rig(ChapterRules.Discovery);
        Assert.Equal(2, match.Economy.HarborTowns(0));
        Assert.False(match.Economy.JointStockOpen(0), "no project");
        int from = match.Economy.SettlementLite(home).SiteTile;
        int[] land = Enumerable.Range(0, World.Value.TileCount).Where(t => World.Value.TileAt(t).IsLand).ToArray();
        int[] before = land.Select(t => match.Politics.TravelSeasons(0, from, t)).ToArray();
        Grant(runner, match, 0, "TRD-10");
        Assert.True(match.Economy.JointStockOpen(0));
        int[] after = land.Select(t => match.Politics.TravelSeasons(0, from, t)).ToArray();
        for (int i = 0; i < land.Length; i++) Assert.True(after[i] <= before[i], "a joint-stock venture is never slower");
        Assert.Contains(Enumerable.Range(0, land.Length), i => after[i] < before[i]);
        int walked = land.First(t => match.Politics.SeaTilesOnRoute(0, from, t) == 0 && t != from);
        Assert.Equal(match.Politics.TravelSeasons(1, from, walked), match.Politics.TravelSeasons(0, from, walked));
        Assert.False(match.Economy.JointStockOpen(1), "another civilization has no ledger");
    }

    [Fact]
    public void One_harbor_has_nobody_to_pool_with_and_Fief_has_no_joint_stock()
    {
        (MatchModel match, SimRunner runner, _, _) = Rig(ChapterRules.Discovery, secondHarbor: false);
        Grant(runner, match, 0, "TRD-10");
        Assert.Equal(1, match.Economy.HarborTowns(0));
        Assert.False(match.Economy.JointStockOpen(0));

        (MatchModel fief, SimRunner fiefRunner, _, _) = Rig(ChapterRules.Fief);
        Grant(fiefRunner, fief, 0, "TRD-10", "TRD-11"); // the ceiling refuses the era-six completions
        Assert.False(fief.Economy.JointStockOpen(0));
        Assert.Equal(0, fief.Economy.HarborTowns(0));
    }

    [Fact]
    public void An_exchange_house_needs_TRD11_a_harbor_town_and_is_one_to_a_civilization()
    {
        (MatchModel match, SimRunner runner, int home, int second) = Rig(ChapterRules.Discovery);
        match.Economy.FixtureAddPeople(home, 80, 25);
        Assert.False(match.Economy.FixtureIronGateOpen(home, "exchange_house", includeAppetite: false), "no project");
        Grant(runner, match, 0, "TRD-11");
        Assert.True(match.Economy.FixtureIronGateOpen(home, "exchange_house", includeAppetite: false), match.Economy.FixtureIronGate(home, "exchange_house", false).ToString());
        match.Economy.FixtureSetIronWork(home, "exchange_house", 1);
        Assert.False(match.Economy.FixtureIronGateOpen(home, "exchange_house", includeAppetite: false), "one to a town");
        Assert.False(match.Economy.FixtureIronGateOpen(second, "exchange_house", includeAppetite: false), "one to a civilization");
    }

    [Fact]
    public void A_staffed_exchange_house_halves_voyage_loss_and_an_empty_one_does_nothing()
    {
        (MatchModel match, SimRunner runner, int home, _) = Rig(ChapterRules.Discovery);
        Grant(runner, match, 0, "TRD-11");
        int from = match.Economy.SettlementLite(home).SiteTile;
        int destination = Enumerable.Range(0, World.Value.TileCount)
            .Where(t => World.Value.TileAt(t).IsLand && match.Politics.SeaTilesOnRoute(0, from, t) > 0).First();
        int plain = match.Politics.VoyageRiskPermille(0, from, destination);

        match.Economy.FixtureSetIronWork(home, "exchange_house", 1);
        Assert.False(match.Economy.ExchangeStaffed(0), "an unstaffed exchange has no effect");
        Assert.Equal(plain, match.Politics.VoyageRiskPermille(0, from, destination));
        match.Economy.FixtureStaffBrokers(home, 2);
        Assert.True(match.Economy.ExchangeStaffed(0));
        Assert.True(match.Economy.InsuranceOpen(0));
        Assert.Equal(plain / 2, match.Politics.VoyageRiskPermille(0, from, destination));
        Assert.False(match.Economy.InsuranceOpen(1));
    }
}
