using System.IO;
using CivSim.Sim.Content;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>Distinct Fief LRN-07 cathedral-school seats and the typed 1.5 scholar-cap effect.</summary>
public sealed class FiefCathedralSchoolTests
{
    private const ulong Seed = 20261115;
    private static BronzeFixture Fief(params string[] projects)
        => BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, projects);

    [Fact]
    public void LRN_07_unlocks_a_distinct_Fief_school_and_never_builds_in_Iron()
    {
        BronzeFixture locked = Fief("KEY-5", "LRN-05", "BEL-10");
        locked.Economy.FixtureSetTier(0, 1);
        Assert.DoesNotContain(locked.Economy.BuildOutlookFor(0).Items, item => item.Id == "cathedral_school");

        BronzeFixture ready = Fief("KEY-5", "LRN-05", "BEL-10", "LRN-07");
        ready.Economy.FixtureSetTier(0, 1);
        Assert.True(ready.Economy.FixtureIronBuildWanted(0, "cathedral_school"));
        ready.Economy.FixtureBuildIronStructure(0, "cathedral_school");
        Assert.False(ready.Economy.FixtureIronBuildWanted(0, "cathedral_school"));

        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "KEY-5", "LRN-05", "BEL-10", "LRN-07");
        iron.Economy.FixtureSetTier(0, 1);
        Assert.False(iron.Economy.FixtureIronBuildWanted(0, "cathedral_school"));
        Assert.DoesNotContain(iron.Economy.BuildOutlookFor(0).Items, item => item.Id == "cathedral_school" && item.Status != BuildStatus.Blocked);
    }

    [Fact]
    public void LRN_07_uses_nearest_integer_half_up_seats_only_after_a_school_or_monastery_is_staffed()
    {
        BronzeFixture empty = Fief("KEY-5", "LRN-05", "BEL-10", "LRN-07");
        empty.Economy.FixtureSetTier(0, 1);
        empty.RunSeasons(2);
        Assert.Equal(0, empty.Economy.ScholarHeads(0));

        BronzeFixture school = Fief("KEY-5", "LRN-05", "BEL-10", "LRN-07");
        school.Economy.FixtureSetTier(0, 1);
        school.Economy.FixtureBuildIronStructure(0, "cathedral_school");
        school.RunSeasons(1);
        Assert.Equal(1, school.Economy.ScholarHeads(0));
        school.RunSeasons(1);
        Assert.Equal(2, school.Economy.ScholarHeads(0)); // 1.5 seats rounds to 2 after a Scholar staffs the school.

        BronzeFixture monasteryAndSchool = Fief("KEY-5", "LRN-05", "BEL-10", "LRN-07");
        monasteryAndSchool.Economy.FixtureSetTier(0, 1);
        monasteryAndSchool.Economy.FixtureBuildIronStructure(0, "monastery");
        monasteryAndSchool.Economy.FixtureBuildIronStructure(0, "cathedral_school");
        monasteryAndSchool.RunSeasons(2);
        Assert.Equal(4, monasteryAndSchool.Economy.MonkHeads(0));
        Assert.Equal(3, monasteryAndSchool.Economy.ScholarHeads(0)); // two staffed institution seats × 1.5.
    }

    [Fact]
    public void Cathedral_school_scholar_roles_continue_deterministically_from_Fief_snapshot()
    {
        BronzeFixture live = Fief("KEY-5", "LRN-05", "BEL-10", "LRN-07");
        live.Economy.FixtureSetTier(0, 1);
        live.Economy.FixtureBuildIronStructure(0, "cathedral_school");
        live.RunSeasons(2);
        Assert.Equal(2, live.Economy.ScholarHeads(0));
        byte[] saved = Snapshot(live.Economy);

        BronzeFixture resumed = Fief("KEY-5", "LRN-05", "BEL-10", "LRN-07");
        resumed.Economy.FixtureSetTier(0, 1);
        resumed.Economy.FixtureBuildIronStructure(0, "cathedral_school");
        resumed.RunSeasons(2);
        Restore(resumed.Economy, saved);
        Assert.Equal(2, resumed.Economy.ScholarHeads(0));
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
