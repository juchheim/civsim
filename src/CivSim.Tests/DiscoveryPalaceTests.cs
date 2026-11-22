using System.Linq;
using CivSim.Sim.Economy;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>BLD-13 the Renaissance Palace Wonder (M13 WP-E6-8, docs/MVP/38 §5.7). Discovery chapter only.</summary>
public class DiscoveryPalaceTests
{
    private const ulong Seed = 20261019;
    private static readonly string[] Projects = { "KEY-5", "BLD-11", "STN-13", "BLD-13" };

    private static BronzeFixture Town(params string[] projects)
    {
        BronzeFixture f = BronzeFixture.CreateDiscoveryWithCoal(TestPack.Pack, Seed, 150, projects);
        f.Economy.FixtureSetTier(0, 1);
        return f;
    }

    [Fact]
    public void The_palace_needs_BLD13_STN13_a_town_and_a_staffed_master_mason_and_exists_only_in_Discovery()
    {
        BronzeFixture f = Town(Projects);
        Assert.False(f.Economy.FixtureIronBuildWanted(0, "renaissance_palace"), "no master mason yet");
        f.RunSeasons(1);
        Assert.Equal(1, f.Economy.FixtureRoleCount(0, "master_mason"));
        Assert.True(f.Economy.FixtureIronBuildWanted(0, "renaissance_palace"));

        BronzeFixture noProject = Town("KEY-5", "BLD-11", "STN-13");
        noProject.RunSeasons(1);
        Assert.False(noProject.Economy.FixtureIronBuildWanted(0, "renaissance_palace"), "BLD-13 is the unlock");

        BronzeFixture fief = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, Projects);
        fief.Economy.FixtureSetTier(0, 1);
        fief.RunSeasons(1);
        Assert.False(fief.Economy.FixtureIronBuildWanted(0, "renaissance_palace"), "Fief has no Palace");
    }

    [Fact]
    public void Plate_glass_ashlar_and_wood_are_paid_once_and_conserved()
    {
        BronzeFixture f = Town(Projects);
        f.Economy.FixtureSetGoodStock(0, "G-ASHLAR", Fix64.FromInt(50));
        f.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(20));
        f.Economy.FixtureAddDiscoveryGood(0, "G-PLATEGLASS", Fix64.FromInt(5));
        Assert.False(f.Economy.FixtureTryPayIronBuild(0, "renaissance_palace"), "five plate glass is one short");
        Assert.Equal(Fix64.FromInt(50), f.Economy.FixtureGoodStock(0, "G-ASHLAR"));

        f.Economy.FixtureAddDiscoveryGood(0, "G-PLATEGLASS", Fix64.One);
        Assert.True(f.Economy.FixtureTryPayIronBuild(0, "renaissance_palace"));
        Assert.Equal(Fix64.Zero, f.Economy.FixtureGoodStock(0, "G-ASHLAR"));
        Assert.Equal(Fix64.Zero, f.Economy.FixtureGoodStock(0, "G-WOOD"));
        Assert.Equal(Fix64.Zero, f.Economy.DiscoveryGoodStock(0, "G-PLATEGLASS"));
    }

    [Fact]
    public void Plate_glass_is_taken_from_the_building_town_first_then_other_towns_of_the_realm()
    {
        MatchModel match = MatchModel.Create(20261020, TestPack.Pack, WorldGenerator.Generate(20261020),
            new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced }, default, rules: ChapterRules.Discovery);
        LivingEconomyModel eco = match.Economy;
        int Home(int civ) => Enumerable.Range(0, eco.SettlementCount).First(s => eco.SettlementLite(s).Civilization == civ);
        int builder = Home(0), other = Home(2), stranger = Home(3);
        eco.FixtureSetCivilizationAndResidents(other, 0); // a second town of civ 0's realm
        eco.FixtureSetGoodStock(builder, "G-ASHLAR", Fix64.FromInt(50));
        eco.FixtureSetGoodStock(builder, "G-WOOD", Fix64.FromInt(20));
        eco.FixtureAddDiscoveryGood(builder, "G-PLATEGLASS", Fix64.FromInt(2));
        eco.FixtureAddDiscoveryGood(stranger, "G-PLATEGLASS", Fix64.FromInt(9));
        Assert.False(eco.FixtureTryPayIronBuild(builder, "renaissance_palace"), "another realm's glass does not count");
        eco.FixtureAddDiscoveryGood(other, "G-PLATEGLASS", Fix64.FromInt(5));
        Assert.True(eco.FixtureTryPayIronBuild(builder, "renaissance_palace"), "two here and five in another town of the realm are enough");
        Assert.Equal(Fix64.Zero, eco.DiscoveryGoodStock(builder, "G-PLATEGLASS"));
        Assert.Equal(Fix64.One, eco.DiscoveryGoodStock(other, "G-PLATEGLASS"));
        Assert.Equal(Fix64.FromInt(9), eco.DiscoveryGoodStock(stranger, "G-PLATEGLASS"));
    }

    [Fact]
    public void The_palace_is_one_in_the_world_and_earns_the_Wonder_Legacy()
    {
        BronzeFixture f = Town(Projects);
        f.RunSeasons(1);
        f.Economy.FixtureCompleteIron(0, "renaissance_palace");
        Assert.Equal(1, f.Economy.FixtureIronStructureCount(0, "renaissance_palace"));
        Assert.True(f.Economy.HoldsWonder(0, "renaissance_palace"));
        Assert.Equal(0, f.Economy.WonderSettlement("renaissance_palace"));
        Assert.False(f.Economy.FixtureIronBuildWanted(0, "renaissance_palace"), "a held Wonder is not built twice");
        var built = new System.Collections.Generic.List<(int Civ, string Project)>();
        f.Economy.DrainWondersBuilt(built);
        Assert.Contains((0, "BLD-13"), built);
    }
}
