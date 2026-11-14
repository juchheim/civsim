using System;
using System.IO;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Fief university staffing and scholar Insight follow-up for WP-E5-6.</summary>
public sealed class FiefUniversityTests
{
    private const ulong Seed = 20261114;
    private static BronzeFixture Fief(params string[] projects)
        => BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, projects);

    [Fact]
    public void University_adds_one_Fief_scholar_post_only_after_LRN_08()
    {
        BronzeFixture locked = Fief("KEY-5", "LRN-05");
        locked.Economy.FixtureSetTier(0, 1);
        Assert.DoesNotContain(locked.Economy.BuildOutlookFor(0).Items, item => item.Id == "university");

        BronzeFixture f = Fief("KEY-5", "LRN-05", "LRN-08");
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureBuildIronStructure(0, "academy");
        f.Economy.FixtureBuildIronStructure(0, "university");
        f.RunSeasons(1);
        Assert.Equal(2, f.Economy.ScholarHeads(0));

        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "KEY-5", "LRN-05", "LRN-08");
        iron.Economy.FixtureSetTier(0, 1);
        Assert.Contains(iron.Economy.BuildOutlookFor(0).Items, item => item.Id == "university" && item.Gate.Why == BuildWhy.NoSuchStructure);
    }

    [Fact]
    public void Only_scholars_at_a_Fief_university_get_the_extra_half_Insight_head()
    {
        BronzeFixture f = Fief("KEY-5", "LRN-05", "LRN-08");
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureBuildIronStructure(0, "academy");
        f.Economy.FixtureBuildIronStructure(0, "university");
        f.RunSeasons(1);
        GeneratedWorld generated = WorldGenerator.Generate(Seed);
        var source = new LivingEconomySource(f.Economy, WorldEconomyInput.FromGeneratedWorld(generated), generated,
            TestPack.Catalog, ReadOnlySpan<EconomyArchetype>.Empty);
        int scholar = TestPack.Catalog.RoleIndex("scholar");
        Assert.Equal(Fix64.FromInt(3), source.GetInsightInputs(0).RoleHeads[scholar]);

        BronzeFixture noUniversity = Fief("KEY-5", "LRN-05");
        noUniversity.Economy.FixtureSetTier(0, 1);
        noUniversity.Economy.FixtureBuildIronStructure(0, "academy");
        noUniversity.RunSeasons(1);
        var baseline = new LivingEconomySource(noUniversity.Economy, WorldEconomyInput.FromGeneratedWorld(generated), generated,
            TestPack.Catalog, ReadOnlySpan<EconomyArchetype>.Empty);
        Assert.Equal(Fix64.FromInt(1), baseline.GetInsightInputs(0).RoleHeads[scholar]);
    }

    [Fact]
    public void A_built_university_survives_Fief_snapshot_restore_and_continuation()
    {
        BronzeFixture live = Fief("KEY-5", "LRN-05", "LRN-08");
        live.Economy.FixtureSetTier(0, 1);
        live.Economy.FixtureBuildIronStructure(0, "academy");
        live.Economy.FixtureBuildIronStructure(0, "university");
        live.RunSeasons(2);
        Assert.Equal(2, live.Economy.ScholarHeads(0));
        byte[] saved = Snapshot(live.Economy);

        BronzeFixture resumed = Fief("KEY-5", "LRN-05", "LRN-08");
        resumed.Economy.FixtureSetTier(0, 1);
        resumed.Economy.FixtureBuildIronStructure(0, "academy");
        resumed.Economy.FixtureBuildIronStructure(0, "university");
        resumed.RunSeasons(2); // Align fixture time with the saved season before restoring.
        Restore(resumed.Economy, saved);

        Assert.Equal(2, resumed.Economy.ScholarHeads(0));
        Assert.Equal(live.Economy.ComputeChecksum(), resumed.Economy.ComputeChecksum());
        live.RunSeasons(3);
        resumed.RunSeasons(3);
        Assert.Equal(Snapshot(live.Economy), Snapshot(resumed.Economy));
        Assert.Equal(live.Economy.ComputeChecksum(), resumed.Economy.ComputeChecksum());
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
}
