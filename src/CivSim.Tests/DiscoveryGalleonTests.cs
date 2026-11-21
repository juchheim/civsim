using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>SEA-13 galleons (M13 WP-E6-6 slice 3, docs/MVP/38 §5.2): a crewed war and convoy ship that blockades, escorts and takes its crew out of the defense pool. Discovery only.</summary>
public sealed class DiscoveryGalleonTests
{
    private const ulong Seed = 20261024;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric(
        "KEY-4", "KEY-6", "TRD-06", "WAY-06", "TRD-07", "HUS-09", "SEA-07", "SEA-09", "WAR-07", "SEA-11", "SEA-12", "SEA-13"));
    private static readonly Lazy<ResearchCatalog> Catalog = new(() => new ResearchCatalog(Pack.Value));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static int Settlement(MatchModel match, int civ)
        => Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == civ && !match.Politics.IsFreeFolk(s));

    private static void Grant(SimRunner runner, int civ, params string[] projects)
    {
        foreach (string project in projects)
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, Catalog.Value.IndexOf(project), 0, 0));
        runner.Advance(1);
    }

    /// <summary>Civilization 0 with a harbor in a town, civilization 2 across the sea with a harbor, at war and in contact; no ship yet.</summary>
    private static (MatchModel Match, SimRunner Runner, int Home, int Target) Rig(ChapterRules rules)
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules);
        var runner = new SimRunner(match);
        Grant(runner, 0, "KEY-4", "SEA-04", "KEY-5", "KEY-6", "SEA-07", "SEA-09", "WAR-07", "TRD-07", "SEA-12", "SEA-13");
        Grant(runner, 2, "KEY-4", "SEA-07");
        int home = Settlement(match, 0), target = Settlement(match, 2);
        match.Economy.FixtureSetTier(home, 1);
        match.Economy.FixtureBuildIronStructure(home, "harbor");
        match.Economy.FixtureBuildIronStructure(target, "harbor");
        match.Politics.SetExternalMind(0, true);
        match.Politics.SetExternalMind(2, true);
        match.Politics.ForceContact(0, 2);
        match.Politics.FixtureReveal(0, match.Economy.SettlementLite(target).SiteTile);
        match.Politics.SetWar(0, 2, true);
        return (match, runner, home, target);
    }

    [Fact]
    public void The_galleon_gate_needs_SEA13_a_harbor_a_crew_and_a_Discovery_match()
    {
        (MatchModel match, _, int home, _) = Rig(ChapterRules.Discovery);
        Assert.False(match.Economy.FixtureIronGateOpen(home, "galleon", includeAppetite: false), "no crew yet");
        match.Economy.FixtureSetSoldiersSupplied(home, LivingEconomyModel.GalleonCrew - 1);
        Assert.False(match.Economy.FixtureIronGateOpen(home, "galleon", includeAppetite: false), "five soldiers are not a crew");
        match.Economy.FixtureSetSoldiersSupplied(home, LivingEconomyModel.GalleonCrew);
        Assert.True(match.Economy.FixtureIronGateOpen(home, "galleon", includeAppetite: false));
        match.Economy.FixtureBuildIronStructure(home, "galleon");
        Assert.False(match.Economy.FixtureIronGateOpen(home, "galleon", includeAppetite: false), "one galleon to a harbor");

        (MatchModel noProject, SimRunner _, int h2, _) = Rig(ChapterRules.Discovery);
        noProject.Economy.FixtureSetSoldiersSupplied(h2, LivingEconomyModel.GalleonCrew);
        Assert.True(noProject.Economy.FixtureIronGateOpen(h2, "galleon", includeAppetite: false));

        (MatchModel fief, _, int fiefHome, _) = Rig(ChapterRules.Fief);
        fief.Economy.FixtureSetSoldiersSupplied(fiefHome, LivingEconomyModel.GalleonCrew);
        Assert.False(fief.Economy.FixtureIronGateOpen(fiefHome, "galleon", includeAppetite: false), "no galleon outside Discovery");
    }

    [Fact]
    public void A_crewed_galleon_blockades_and_is_the_ship_that_is_lost()
    {
        (MatchModel match, SimRunner runner, int home, int target) = Rig(ChapterRules.Discovery);
        match.Economy.FixtureBuildIronStructure(home, "galleon");
        Assert.False(match.Economy.CrewedGalleon(home), "no crew, no ship");
        Assert.False(match.Politics.OrderBlockade(0, target));
        match.Economy.FixtureSetSoldiersSupplied(home, LivingEconomyModel.GalleonCrew);
        Assert.True(match.Economy.CrewedGalleon(home));
        Assert.True(match.Economy.CrewedGalley(home), "a crewed galleon holds a station like a galley");
        Assert.True(match.Politics.OrderBlockade(0, target));
        match.Economy.LoseGalley(home);
        Assert.False(match.Economy.CrewedGalleon(home));
        Assert.False(match.Economy.CrewedGalley(home));
        runner.Advance(150);
        Assert.Empty(match.Politics.Blockades);
    }

    [Fact]
    public void A_galleon_on_station_takes_its_crew_out_of_the_defense_pool_until_the_blockade_ends()
    {
        (MatchModel match, SimRunner runner, int home, int target) = Rig(ChapterRules.Discovery);
        match.Economy.FixtureBuildIronStructure(home, "galleon");
        match.Economy.FixtureSetSoldiersSupplied(home, LivingEconomyModel.GalleonCrew);
        match.Economy.FixtureSetSoldierRoles(home, 8);
        int before = match.Politics.OwnDefendersOf(0, home).Count;
        Assert.True(match.Politics.OrderBlockade(0, target));
        int onStation = match.Politics.OwnDefendersOf(0, home).Count;
        Assert.Equal(before - LivingEconomyModel.GalleonCrew, onStation);
        // The crew leaves the ship (here, the supply lapses): the ship no longer holds the station, and the soldiers are back in the pool.
        match.Economy.FixtureSetSoldiersSupplied(home, 0);
        Assert.Equal(before, match.Politics.OwnDefendersOf(0, home).Count);
        match.Economy.FixtureSetSoldiersSupplied(home, LivingEconomyModel.GalleonCrew);
        match.Politics.SetWar(0, 2, false);
        runner.Advance(150);
        Assert.Empty(match.Politics.Blockades);
    }

    [Fact]
    public void A_galley_on_station_keeps_its_crew_in_the_pool_as_before()
    {
        (MatchModel match, _, int home, int target) = Rig(ChapterRules.Discovery);
        match.Economy.FixtureBuildIronStructure(home, "galley");
        match.Economy.FixtureSetSoldiersSupplied(home, 4);
        match.Economy.FixtureSetSoldierRoles(home, 8);
        int before = match.Politics.OwnDefendersOf(0, home).Count;
        Assert.True(match.Politics.OrderBlockade(0, target));
        Assert.Equal(before, match.Politics.OwnDefendersOf(0, home).Count);
    }

    [Fact]
    public void An_escorted_trade_fleet_keeps_six_tenths_of_its_loss_and_only_in_Discovery()
    {
        Assert.Equal(6, PoliticsSystem.ConvoyRiskTenths);
        (MatchModel match, _, int home, _) = Rig(ChapterRules.Discovery);
        Assert.False(match.Economy.CrewedGalleon(home));
        Assert.Equal(300, match.Politics.FixtureConvoyRisk(300, home));
        match.Economy.FixtureBuildIronStructure(home, "galleon");
        match.Economy.FixtureSetSoldiersSupplied(home, LivingEconomyModel.GalleonCrew);
        Assert.True(match.Economy.CrewedGalleon(home));
        Assert.Equal(180, match.Politics.FixtureConvoyRisk(300, home));
        (MatchModel fief, _, int fiefHome, _) = Rig(ChapterRules.Fief);
        fief.Economy.FixtureBuildIronStructure(fiefHome, "galleon");
        fief.Economy.FixtureSetSoldiersSupplied(fiefHome, LivingEconomyModel.GalleonCrew);
        Assert.False(fief.Economy.CrewedGalleon(fiefHome), "no escort outside Discovery");
        Assert.Equal(300, fief.Politics.FixtureConvoyRisk(300, fiefHome));
    }
}
