using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Tests;

public sealed class BronzeDefenseBuildTests
{
    private const ulong Seed = 20261019;

    [Fact]
    public void Belief_priority_keeps_an_eligible_palisade_ahead_of_optional_builds()
    {
        BronzeFixture fixture = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, "DEF-03");
        LivingEconomyModel economy = fixture.Economy;
        economy.FixtureStock(0, "G-WOOD", Fix64.FromInt(100));

        Assert.True(economy.PrioritizePhysicalDefenseBuild(0));
        int palisade = LivingEconomyModel.StructureIndex("palisade");
        Assert.Equal(palisade, economy.FixtureBuildTarget(0));
        Assert.True(economy.FixtureHasBuildCandidate(0));
        Assert.Equal(palisade, economy.FixtureBuildTarget(0));
    }

    [Fact]
    public void Defense_priority_respects_unlocks_resources_and_existing_defenses()
    {
        GeneratedWorld world = WorldGenerator.Generate(Seed);
        var locked = new LivingEconomyModel(Seed, EconomyContent.FromPack(TestPack.Pack), WorldEconomyInput.FromGeneratedWorld(world));
        locked.FixtureComplete(0, "KEY-3");
        locked.FixtureStock(0, "G-WOOD", Fix64.FromInt(100));
        Assert.False(locked.PrioritizePhysicalDefenseBuild(0));

        BronzeFixture poor = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, "DEF-03");
        poor.Economy.FixtureStock(0, "G-WOOD", -poor.Economy.GoodStock(0, "G-WOOD"));
        Assert.False(poor.Economy.PrioritizePhysicalDefenseBuild(0));

        BronzeFixture fortified = BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, "DEF-03");
        fortified.Economy.FixtureStock(0, "G-WOOD", Fix64.FromInt(100));
        fortified.Economy.FixtureBuildStructure(0, "palisade");
        Assert.False(fortified.Economy.PrioritizePhysicalDefenseBuild(0));
    }
}
