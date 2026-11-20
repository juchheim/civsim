using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>The Arsenal (docs/MVP/37-arsenal.md): its gate, the retired powder mill, its supply reach and the conquest prospect that makes the planner want one.</summary>
public sealed class ArsenalTests
{
    private const ulong Seed = 20261024;

    [Fact]
    public void Arsenal_gate_needs_WAR12_a_forge_a_size_60_town_and_is_Fief_only_and_one_per_civilization()
    {
        // Saltpeter and sulfur supply is a readiness fact shown to the player, never a construction prerequisite (the Arsenal may be commissioned first).
        BronzeFixture f = BronzeFixture.CreateFiefWithPowderDeposits(TestPack.Pack, Seed, 100, "WAR-12", "CHM-07");
        Assert.False(f.Economy.FixtureIronGateOpen(0, "arsenal", includeAppetite: false), "a Fief town without a forge cannot build an Arsenal");
        f.Economy.FixtureSetMetalFacilities(0, 1, 1);
        f.Economy.FixtureSetTier(0, 1);
        Assert.True(f.Economy.FixtureIronGateOpen(0, "arsenal", includeAppetite: false));
        Assert.True(f.Economy.FixtureIronGateOpen(0, "arsenal", includeAppetite: true), "cannon and powder research are both done, so the planner prepares one");

        // With cannon research alone, the planner wants an Arsenal only when a conquest prospect creates urgency.
        BronzeFixture cannonOnly = BronzeFixture.CreateFiefWithPowderDeposits(TestPack.Pack, Seed, 100, "WAR-12");
        cannonOnly.Economy.FixtureSetMetalFacilities(0, 1, 1);
        cannonOnly.Economy.FixtureSetTier(0, 1);
        Assert.False(cannonOnly.Economy.FixtureIronGateOpen(0, "arsenal", includeAppetite: true), "no powder research and no conquest prospect");
        cannonOnly.Economy.ConquestProspectProbe = _ => true;
        Assert.True(cannonOnly.Economy.FixtureIronGateOpen(0, "arsenal", includeAppetite: true));
        cannonOnly.Economy.ConquestProspectProbe = _ => false;
        Assert.False(cannonOnly.Economy.FixtureIronGateOpen(0, "arsenal", includeAppetite: true));

        f.Economy.FixtureSetIronWork(0, "arsenal", 1);
        Assert.False(f.Economy.FixtureIronGateOpen(0, "arsenal", includeAppetite: false), "one per town and per civilization");

        BronzeFixture noWar12 = BronzeFixture.CreateFiefWithPowderDeposits(TestPack.Pack, Seed, 100, "CHM-07");
        noWar12.Economy.FixtureSetMetalFacilities(0, 1, 1);
        noWar12.Economy.FixtureSetTier(0, 1);
        Assert.False(noWar12.Economy.FixtureIronGateOpen(0, "arsenal", includeAppetite: false));

        BronzeFixture small = BronzeFixture.CreateFiefWithPowderDeposits(TestPack.Pack, Seed, 20, "WAR-12", "CHM-07");
        small.Economy.FixtureSetMetalFacilities(0, 1, 1);
        small.Economy.FixtureSetTier(0, 1);
        Assert.False(small.Economy.FixtureIronGateOpen(0, "arsenal", includeAppetite: false), "a town of fewer than 60 cannot hold one");

        BronzeFixture iron = BronzeFixture.CreateIronWithPowderDeposits(TestPack.Pack, Seed, 100, "WAR-12", "CHM-07");
        iron.Economy.FixtureSetMetalFacilities(0, 1, 1);
        iron.Economy.FixtureSetTier(0, 1);
        Assert.False(iron.Economy.FixtureIronGateOpen(0, "arsenal", includeAppetite: false), "Fief only");
    }

    [Fact]
    public void Sister_towns_carry_saltpeter_sulfur_and_spare_iron_to_the_Arsenal_without_creating_goods()
    {
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, "SOC-07", "GOV-04", "WAR-12", "CHM-07");
        LivingEconomyModel economy = fixture.Economy;
        economy.FixtureComplete(0, "SOC-07", "GOV-04", "WAR-12", "CHM-07");
        int daughter = economy.FoundByDecree(0, fixture.CampTile, Fix64.FromRatio(2, 5), minDaughter: 6, startCohesion: 60);
        Assert.True(daughter >= 0);
        economy.FixtureSetMetalFacilities(0, 1, 1);
        economy.FixtureSetMetalFacilities(daughter, 1, 1);
        economy.FixtureSetIronWork(0, "arsenal", 1);
        economy.FixtureStock(daughter, "G-SALTPETER", Fix64.FromInt(2));
        economy.FixtureStock(daughter, "G-SULFUR", Fix64.One);
        economy.FixtureStock(daughter, "G-IRON", Fix64.FromInt(30));
        Fix64 saltBefore = economy.GoodStock(0, "G-SALTPETER") + economy.GoodStock(daughter, "G-SALTPETER");
        Fix64 ironBefore = economy.GoodStock(0, "G-IRON") + economy.GoodStock(daughter, "G-IRON");

        economy.FixtureShipArsenalSupplies();

        Assert.Equal(Fix64.Zero, economy.GoodStock(daughter, "G-SALTPETER"));
        Assert.Equal(Fix64.Zero, economy.GoodStock(daughter, "G-SULFUR"));
        Assert.Equal(Fix64.FromInt(2), economy.GoodStock(0, "G-SALTPETER"));
        Assert.True(economy.GoodStock(0, "G-IRON") > Fix64.Zero, "spare iron is sent to the Arsenal");
        Assert.True(economy.GoodStock(0, "G-IRON") <= Fix64.FromInt(4), "up to the Arsenal's two-cannon iron target");
        Assert.Equal(saltBefore, economy.GoodStock(0, "G-SALTPETER") + economy.GoodStock(daughter, "G-SALTPETER"));
        Assert.Equal(ironBefore, economy.GoodStock(0, "G-IRON") + economy.GoodStock(daughter, "G-IRON"));
    }

    [Fact]
    public void Powder_mill_is_retired_and_gunpowder_is_milled_at_the_Arsenal()
    {
        BronzeFixture f = BronzeFixture.CreateFiefWithPowderDeposits(TestPack.Pack, Seed, 100, "CHM-07", "WAR-12");
        Assert.False(f.Economy.FixtureIronGateOpen(0, "powder_mill"), "the powder mill is retired");
        Assert.Equal(Fix64.Zero, f.Economy.FixtureRecipePlannedBatches(0, "make_powder", Fix64.FromInt(100)));
        f.Economy.FixtureSetIronWork(0, "arsenal", 1);
        Assert.True(f.Economy.FixtureRecipePlannedBatches(0, "make_powder", Fix64.FromInt(100)) > Fix64.Zero);
    }

    [Fact]
    public void Arsenal_supply_reach_grows_with_carts_roads_and_the_horseshoe()
    {
        Assert.Equal(8, BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, "WAR-12").Economy.ArsenalReach(0));
        Assert.Equal(12, BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, "WAR-12", "TRN-05").Economy.ArsenalReach(0));
        Assert.Equal(16, BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, "WAR-12", "TRN-05", "TRN-07").Economy.ArsenalReach(0));
        Assert.Equal(18, BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, "WAR-12", "TRN-05", "TRN-07", "TRN-09").Economy.ArsenalReach(0));
    }

    [Fact]
    public void Fief_siege_supply_reach_is_ten_and_Iron_stays_eight()
    {
        Assert.Equal(10, Sieges.SupplyReach(fief: true));
        Assert.Equal(8, Sieges.SupplyReach(fief: false));
    }

    [Fact]
    public void Conquest_prospect_needs_WAR12_hostility_a_walled_rival_in_reach_and_a_force_that_beats_it()
    {
        MatchModel match = MatchModel.Create(Seed, TestPack.WithNumeric("KEY-5", "WAR-12", "CHM-07", "DEF-05"), BuildNearbyStartWorld(), 
            new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced }, default, rules: ChapterRules.Fief);
        var runner = new SimRunner(match);
        void Grant(int civ, params string[] ids)
        {
            foreach (string id in ids)
            {
                runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, match.Research.Catalog.IndexOf(id), 0, 0));
                runner.Advance(1);
            }
        }
        Grant(0, "KEY-5", "CHM-07");
        Grant(1, "KEY-5", "DEF-05");
        runner.Advance(150);
        int home = Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == 0 && !match.Politics.IsFreeFolk(s));
        int target = Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == 1 && !match.Politics.IsFreeFolk(s));
        match.Economy.FixtureAddPeople(home, 240, 24);
        match.Economy.FixtureBuildStructure(target, "stone_walls");
        match.Politics.ForceContact(0, 1);

        match.Politics.Diplomacy.Set(0, 1, 0);
        Assert.False(match.Politics.ConquestProspect(0), "no WAR-12 yet");
        Grant(0, "WAR-12");
        match.Politics.Diplomacy.Set(0, 1, 0);
        Assert.False(match.Politics.ConquestProspect(0), "at peace with relations not hostile");
        match.Politics.SetWar(0, 1, true);
        Assert.True(match.Politics.ConquestProspect(0), "hostile, walled, in reach and outnumbered");
        match.Economy.FixtureBuildStructure(target, "palisade");
        Assert.False(match.Politics.ConquestProspect(2), "a civilization that is not hostile to anyone has no prospect");
        Assert.True(match.Economy.ArsenalWantedFor(0));
        Assert.False(match.Economy.ArsenalWantedFor(2));
    }

    private static GeneratedWorld BuildNearbyStartWorld()
    {
        GeneratedWorld source = WorldGenerator.Generate(Seed);
        WorldTile[] tiles = source.Tiles.ToArray();
        WorldStart[] starts = source.Starts.ToArray();
        bool[] passable = HexGrid.BuildWalkingPassability(tiles, source.Width, source.Height);
        int targetStart = starts.Single(s => s.Civilization == 1).Tile;
        int[] distance = HexGrid.WalkingDistancesFrom(source.Width, source.Height, passable, targetStart);
        var occupied = starts.Select(s => s.Tile).ToHashSet();
        int nearby = Enumerable.Range(0, tiles.Length)
            .Where(t => !occupied.Contains(t) && tiles[t].IsLand && tiles[t].Biome != Biome.Peak && distance[t] is >= 5 and <= 7)
            .OrderBy(t => distance[t]).ThenBy(t => t).First();
        int attacker = Array.FindIndex(starts, s => s.Civilization == 0);
        starts[attacker] = starts[attacker] with { Tile = nearby };
        return new GeneratedWorld(source.Seed, source.Settings, tiles, starts, source.Herds.ToArray(), source.Predators.ToArray(), source.Report);
    }
}
