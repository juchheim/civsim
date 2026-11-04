using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Organized Faith in the Iron chapter (BEL-09): missionaries, route-bound conversion, the cap and the soft claim pressure.</summary>
public sealed class IronFaithTests
{
    private const ulong Seed = 20261024;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric("KEY-4", "BEL-06", "BEL-09", "SEA-07", "SEA-09", "WAR-07"));
    private static readonly Lazy<ResearchCatalog> Catalog = new(() => new ResearchCatalog(Pack.Value));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner) Rig(ChapterRules rules)
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules);
        return (match, new SimRunner(match));
    }

    private static void Grant(SimRunner runner, int civ, params string[] projects)
    {
        foreach (string project in projects)
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, Catalog.Value.IndexOf(project), 0, 0));
        runner.Advance(1);
    }

    private static int Settlement(MatchModel match, int civ)
        => Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == civ && !match.Politics.IsFreeFolk(s));

    /// <summary>Civilization 0 with a temple, BEL-09 and a missionary in post; civilization 1 beside it.</summary>
    private static (MatchModel Match, SimRunner Runner, int Home, int Foreign) Preachers(ChapterRules rules)
    {
        (MatchModel match, SimRunner runner) = Rig(rules);
        Grant(runner, 0, "KEY-3", "KEY-4", "BEL-06", "BEL-09");
        int home = Settlement(match, 0), foreign = Settlement(match, 1);
        match.Economy.FixtureAddPeople(home, 80, 25);
        match.Economy.FixtureCivic(home, "temple");
        match.Economy.FixtureSetTier(home, 1);
        runner.Advance(150);
        return (match, runner, home, foreign);
    }

    [Fact]
    public void A_temple_town_that_knows_Organized_Faith_staffs_one_missionary_and_a_Bronze_match_none()
    {
        (MatchModel match, _, int home, _) = Preachers(ChapterRules.Iron);
        Assert.Equal(1, match.Economy.Missionaries(home));
        (MatchModel bronze, _, int bronzeHome, _) = Preachers(ChapterRules.Bronze);
        Assert.Equal(0, bronze.Economy.Missionaries(bronzeHome));
    }

    [Fact]
    public void A_preacher_converts_one_percent_a_year_of_the_unconverted_to_a_quarter_cap_and_never_at_home()
    {
        (MatchModel match, _, int home, int foreign) = Preachers(ChapterRules.Iron);
        LivingEconomyModel economy = match.Economy;
        Assert.Equal(Fix64.Zero, economy.ConvertTo(home, 0, 1)); // no one is converted to their own civilization's faith
        Fix64 first = economy.ConvertTo(foreign, 0, 1);
        Assert.Equal(Fix64.FromRatio(1, 400), first);
        Fix64 second = economy.ConvertTo(foreign, 0, 2);
        Assert.Equal(Fix64.FromRatio(1, 400) * (Fix64.One - first), second); // 1% of those still unconverted
        for (int season = 3; season < 4000; season++) economy.ConvertTo(foreign, 0, season);
        Assert.Equal(Fix64.FromRatio(1, 4), economy.FaithShare(foreign, 0)); // the cap, exactly
        Assert.Equal(Fix64.Zero, economy.FaithShare(foreign, 2));
        (MatchModel bronze, _, int bronzeHome, int bronzeForeign) = Preachers(ChapterRules.Bronze);
        Assert.Equal(Fix64.Zero, bronze.Economy.ConvertTo(bronzeForeign, 0, 1));
    }

    [Fact]
    public void Soft_claim_pressure_is_five_percent_at_the_cap_and_ends_when_the_preaching_does()
    {
        (MatchModel match, _, _, int foreign) = Preachers(ChapterRules.Iron);
        LivingEconomyModel economy = match.Economy;
        Assert.Equal(Fix64.Zero, economy.FaithClaimShare(foreign, 0, 10));
        for (int season = 1; season < 4000; season++) economy.ConvertTo(foreign, 0, season);
        Assert.Equal(Fix64.FromRatio(5, 100), economy.FaithClaimShare(foreign, 0, 3999));
        Assert.Equal(Fix64.FromRatio(5, 100), economy.FaithClaimShare(foreign, 0, 4000)); // the last season still counts
        Assert.Equal(Fix64.Zero, economy.FaithClaimShare(foreign, 0, 4001)); // the preacher has stopped coming: converts stay, pressure goes
        Assert.Equal(Fix64.FromRatio(1, 4), economy.FaithShare(foreign, 0));
    }

    [Fact]
    public void Missionaries_follow_a_running_route_to_a_foreign_town_and_only_that()
    {
        (MatchModel match, SimRunner runner, int home, int foreign) = Preachers(ChapterRules.Iron);
        // No route: nothing is reached, nothing converted.
        Assert.Empty(match.Politics.FixtureFaithRoutes(home));
        runner.Advance(150);
        Assert.Equal(Fix64.Zero, match.Economy.FaithShare(foreign, 0));
        // A running route to a foreign hub.
        match.Politics.FixtureAddRoute(0, 1, home, foreign);
        Assert.Equal(new[] { foreign }, match.Politics.FixtureFaithRoutes(home));
        runner.Advance(150);
        Assert.True(match.Economy.FaithShare(foreign, 0) > Fix64.Zero, "the missionary reached the town on the route");
        Assert.Equal(Fix64.Zero, match.Economy.FaithShare(home, 0));
        Assert.Equal(1, match.Economy.SettlementLite(foreign).Civilization); // never a transfer of the town
        // A same-civilization route reaches no one.
        (MatchModel same, _, int sHome, _) = Preachers(ChapterRules.Iron);
        same.Politics.FixtureAddRoute(0, 0, sHome, sHome);
        Assert.Empty(same.Politics.FixtureFaithRoutes(sHome));
    }

    [Fact]
    public void A_blockade_cuts_the_faith_route_by_sea_and_the_faith_state_survives_a_snapshot()
    {
        (MatchModel match, SimRunner runner) = Rig(ChapterRules.Iron);
        Grant(runner, 0, "KEY-3", "KEY-4", "BEL-06", "BEL-09", "SEA-04", "SEA-07", "SEA-09", "WAR-07");
        Grant(runner, 2, "KEY-4", "SEA-07");
        int home = Settlement(match, 0), far = Settlement(match, 2);
        match.Economy.FixtureAddPeople(home, 80, 25);
        match.Economy.FixtureCivic(home, "temple");
        match.Economy.FixtureSetTier(home, 1);
        match.Economy.FixtureBuildIronStructure(home, "harbor");
        match.Economy.FixtureBuildIronStructure(home, "galley");
        match.Economy.FixtureSetSoldiersSupplied(home, 4);
        match.Economy.FixtureBuildIronStructure(far, "harbor");
        match.Politics.ForceContact(0, 2);
        match.Politics.FixtureReveal(0, match.Economy.SettlementLite(far).SiteTile);
        match.Politics.FixtureAddRoute(0, 2, home, far);
        Assert.Equal(new[] { far }, match.Politics.FixtureFaithRoutes(home));
        match.Politics.SetWar(0, 2, true);
        Assert.True(match.Politics.OrderBlockade(0, far));
        Assert.Empty(match.Politics.FixtureFaithRoutes(home)); // the sea route to that harbor is shut

        match.Politics.SetWar(0, 2, false);
        match.Economy.ConvertTo(far, 0, 5);
        using var stream = new System.IO.MemoryStream();
        using (var writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) match.WriteSnapshot(writer);
        stream.Position = 0;
        MatchModel resumed = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: ChapterRules.Iron);
        using (var reader = new System.IO.BinaryReader(stream)) resumed.ReadSnapshot(reader);
        Assert.Equal(match.ComputeChecksum(), resumed.ComputeChecksum());
        Assert.Equal(match.Economy.FaithShare(far, 0), resumed.Economy.FaithShare(far, 0));
        Assert.True(resumed.Economy.FaithShare(far, 0) > Fix64.Zero);
    }
}
