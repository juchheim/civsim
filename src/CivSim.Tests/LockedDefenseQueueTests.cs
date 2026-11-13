using System;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>A defense an AI queued ahead of its technology holds its labor but must not starve the foundation (docs/MVP/30).</summary>
public sealed class LockedDefenseQueueTests
{
    private const ulong Seed = 20261019;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));

    private static BronzeFixture Fixture() => BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true);

    [Fact]
    public void A_queued_palisade_that_is_not_yet_unlocked_steps_aside_for_a_paid_for_storage_pit()
    {
        BronzeFixture f = Fixture();
        LivingEconomyModel e = f.Economy;
        int pit = LivingEconomyModel.StructureIndex("storage_pit");
        int palisade = LivingEconomyModel.StructureIndex("palisade");
        e.FixtureSetStructureCount(0, "storage_pit", 0);
        e.FixtureSetStructureCount(0, "palisade", 0);
        e.FixtureForget(e.FixtureCivOf(0), "DEF-03"); // Palisades & Ditches: the palisade is queued ahead of its technology
        e.FixtureStock(0, "G-WOOD", Fix64.FromInt(20));
        e.FixtureSetBuildTarget(0, "palisade");
        e.FixtureSetBuildProgress(0, Fix64.FromInt(20));
        e.FixtureBuildIfReady(0);
        Assert.Equal(0, e.StructureCount(0, palisade));
        Assert.True(e.StructureCount(0, pit) > 0, "the pit is a foundation need and is paid for: it is built while the palisade waits");
    }

    [Fact]
    public void A_palisade_whose_technology_is_not_learned_is_never_built_however_much_labor_is_queued_for_it()
    {
        BronzeFixture f = Fixture();
        LivingEconomyModel e = f.Economy;
        int palisade = LivingEconomyModel.StructureIndex("palisade");
        e.FixtureForget(e.FixtureCivOf(0), "DEF-03");
        e.FixtureSetStructureCount(0, "palisade", 0);
        e.FixtureStock(0, "G-WOOD", Fix64.FromInt(50));
        e.FixtureSetBuildTarget(0, "palisade");
        e.FixtureSetBuildProgress(0, Fix64.FromInt(40));
        for (int i = 0; i < 5; i++) e.FixtureBuildIfReady(0);
        Assert.Equal(0, e.StructureCount(0, palisade));
    }
}
