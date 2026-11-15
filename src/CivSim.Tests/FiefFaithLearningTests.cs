using System;
using System.IO;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>First bounded WP-E5-6 slice: monastery construction, real monk staffing, and Insight inputs.</summary>
public sealed class FiefFaithLearningTests
{
    private const ulong Seed = 20261114;

    private static BronzeFixture Fief(int people = 100, params string[] projects)
        => BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, people, projects);

    private static BronzeFixture Iron(int people = 100, params string[] projects)
        => BronzeFixture.CreateIron(TestPack.Pack, Seed, people, projects);

    [Fact]
    public void Monastery_is_a_Fief_only_town_build_with_a_three_per_civilization_limit()
    {
        BronzeFixture ready = Fief(100, "KEY-5", "BEL-10");
        ready.Economy.FixtureSetTier(0, 1);
        Assert.True(ready.Economy.FixtureIronBuildWanted(0, "monastery"));

        BronzeFixture locked = Fief(100, "KEY-5");
        locked.Economy.FixtureSetTier(0, 1);
        Assert.DoesNotContain(locked.Economy.BuildOutlookFor(0).Items, item => item.Id == "monastery");

        BronzeFixture small = Fief(40, "KEY-5", "BEL-10");
        small.Economy.FixtureSetTier(0, 1);
        Assert.False(small.Economy.FixtureIronBuildWanted(0, "monastery"));

        BronzeFixture iron = Iron(100, "KEY-5", "BEL-10");
        iron.Economy.FixtureSetTier(0, 1);
        Assert.False(iron.Economy.FixtureIronBuildWanted(0, "monastery"));

        ready.Economy.FixtureSetIronWork(0, "monastery", 3);
        ready.Economy.FixtureSetCivilization(1, 0);
        ready.Economy.FixtureAddPeople(1, 60, 25);
        ready.Economy.FixtureSetTier(1, 1);
        Assert.False(ready.Economy.FixtureIronBuildWanted(1, "monastery"),
            "a fourth town with an open local slot should see the civilization-wide cap");
    }

    [Fact]
    public void Monasteries_take_four_real_adults_off_other_work_and_feed_the_monk_Insight_role()
    {
        BronzeFixture idle = Fief(100, "KEY-5", "BEL-10");
        idle.Economy.FixtureSetTier(0, 1);
        idle.RunSeasons(1);
        Assert.Equal(0, idle.Economy.MonkHeads(0));

        BronzeFixture staffed = Fief(100, "KEY-5", "BEL-10");
        staffed.Economy.FixtureSetTier(0, 1);
        staffed.Economy.FixtureBuildIronStructure(0, "monastery");
        staffed.RunSeasons(1);
        Assert.Equal(4, staffed.Economy.MonkHeads(0));

        ResearchCatalog catalog = TestPack.Catalog;
        int monk = catalog.RoleIndex("monk");
        Assert.True(monk >= 0);
        Assert.Equal(catalog.IndexOf("BEL-10"), catalog.Roles[monk].UnlockedBy);
        Assert.Equal(Fix64.FromInt(4), catalog.Roles[monk].Insight);

        GeneratedWorld generated = WorldGenerator.Generate(Seed);
        LivingEconomySource source = new(staffed.Economy, WorldEconomyInput.FromGeneratedWorld(generated), generated,
            catalog, ReadOnlySpan<EconomyArchetype>.Empty);
        InsightInputs inputs = source.GetInsightInputs(0);
        Assert.Equal(Fix64.FromInt(4), inputs.RoleHeads[monk]);
        Assert.Equal(Fix64.FromInt(16), inputs.RoleHeads[monk] * catalog.Roles[monk].Insight);

        BronzeFixture iron = Iron(100, "KEY-5", "BEL-10");
        iron.Economy.FixtureSetTier(0, 1);
        iron.Economy.FixtureBuildIronStructure(0, "monastery");
        iron.RunSeasons(1);
        Assert.Equal(0, iron.Economy.MonkHeads(0));
    }

    [Fact]
    public void A_snapshot_continues_with_the_same_staffed_monks_and_monastery_hash()
    {
        BronzeFixture live = Fief(100, "KEY-5", "BEL-10");
        live.Economy.FixtureSetTier(0, 1);
        live.Economy.FixtureBuildIronStructure(0, "monastery");
        live.RunSeasons(2);
        Assert.Equal(4, live.Economy.MonkHeads(0));

        byte[] saved = Snapshot(live.Economy);
        BronzeFixture resumed = Fief(100, "KEY-5", "BEL-10");
        resumed.Economy.FixtureSetTier(0, 1);
        resumed.Economy.FixtureBuildIronStructure(0, "monastery");
        resumed.RunSeasons(2); // Keep the fixture clock aligned with the snapshot's season.
        Restore(resumed.Economy, saved);
        Assert.Equal(4, resumed.Economy.MonkHeads(0));
        Assert.Equal(Hash(live.Economy), Hash(resumed.Economy));

        live.RunSeasons(3);
        resumed.RunSeasons(3);
        Assert.Equal(Snapshot(live.Economy), Snapshot(resumed.Economy));
        Assert.Equal(Hash(live.Economy), Hash(resumed.Economy));
    }

    private static byte[] Snapshot(LivingEconomyModel economy)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) economy.WriteSnapshot(writer);
        return stream.ToArray();
    }

    private static void Restore(LivingEconomyModel economy, byte[] snapshot)
    {
        using var stream = new MemoryStream(snapshot);
        using var reader = new BinaryReader(stream);
        economy.ReadSnapshot(reader);
        Assert.Equal(stream.Length, stream.Position);
    }

    private static ulong Hash(LivingEconomyModel economy)
        => economy.ComputeChecksum();
}
