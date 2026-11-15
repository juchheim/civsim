using System;
using System.IO;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>Bounded WP-E5-6 hospital staffing and existing-outbreak mortality effect.</summary>
public sealed class FiefHospitalTests
{
    private const ulong Seed = 20261114;

    private static BronzeFixture Fief(params string[] projects)
        => BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, projects);

    [Fact]
    public void Hospital_build_is_MED_07_gated_and_Fief_only()
    {
        BronzeFixture locked = Fief("KEY-5");
        locked.Economy.FixtureSetTier(0, 1);
        Assert.DoesNotContain(locked.Economy.BuildOutlookFor(0).Items, item => item.Id == "hospital");

        BronzeFixture ready = Fief("KEY-5", "MED-07");
        ready.Economy.FixtureSetTier(0, 1);
        Assert.True(ready.Economy.FixtureIronBuildWanted(0, "hospital"));

        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "KEY-5", "MED-07");
        iron.Economy.FixtureSetTier(0, 1);
        Assert.Contains(iron.Economy.BuildOutlookFor(0).Items,
            item => item.Id == "hospital" && item.Gate.Why == BuildWhy.NoSuchStructure);
    }

    [Fact]
    public void Hospital_only_reduces_existing_outbreak_hazard_when_physician_and_two_attendants_are_staffed()
    {
        BronzeFixture baseline = Fief("KEY-5", "MED-07");
        baseline.Economy.FixtureSetTier(0, 1);
        baseline.RunSeasons(1);
        baseline.Economy.FixtureSetOutbreak(0, OutbreakClass.Plague);
        Fix64 baselineHazard = baseline.Economy.FixtureInfectedHazard(0);

        BronzeFixture hospital = Fief("KEY-5", "MED-07");
        hospital.Economy.FixtureSetTier(0, 1);
        hospital.Economy.FixtureBuildIronStructure(0, "hospital");
        hospital.RunSeasons(1);
        Assert.Equal(1, hospital.Economy.FixtureRoleCount(0, "physician"));
        Assert.Equal(2, hospital.Economy.FixtureRoleCount(0, "attendant"));
        Assert.True(hospital.Economy.FixtureHospitalStaffed(0));
        hospital.Economy.FixtureSetOutbreak(0, OutbreakClass.Plague);
        Assert.Equal(baselineHazard * Fix64.FromRatio(4, 5),
            hospital.Economy.FixtureInfectedHazard(0));

        hospital.Economy.FixtureClearRoles(0);
        Assert.False(hospital.Economy.FixtureHospitalStaffed(0));
        Assert.Equal(baselineHazard, hospital.Economy.FixtureInfectedHazard(0));
    }

    [Fact]
    public void Staffed_hospital_roles_and_structure_survive_Fief_snapshot_restore()
    {
        BronzeFixture live = Fief("KEY-5", "MED-07");
        live.Economy.FixtureSetTier(0, 1);
        live.Economy.FixtureBuildIronStructure(0, "hospital");
        live.RunSeasons(2);
        Assert.True(live.Economy.FixtureHospitalStaffed(0));
        byte[] snapshot = Snapshot(live.Economy);

        BronzeFixture resumed = Fief("KEY-5", "MED-07");
        resumed.Economy.FixtureSetTier(0, 1);
        resumed.Economy.FixtureBuildIronStructure(0, "hospital");
        resumed.RunSeasons(2);
        Restore(resumed.Economy, snapshot);
        Assert.True(resumed.Economy.FixtureHospitalStaffed(0));
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
