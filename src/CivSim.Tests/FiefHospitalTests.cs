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
    public void CHM_06_spirits_replace_the_hospital_reduction_only_in_staffed_Fief_hospitals()
    {
        BronzeFixture baseline = Fief("KEY-5", "MED-07", "CHM-06");
        baseline.Economy.FixtureSetTier(0, 1);
        baseline.RunSeasons(1);
        baseline.Economy.FixtureSetOutbreak(0, OutbreakClass.Plague);
        Fix64 baselineHazard = baseline.Economy.FixtureInfectedHazard(0);

        BronzeFixture hospital = Fief("KEY-5", "MED-07", "CHM-06");
        hospital.Economy.FixtureSetTier(0, 1);
        hospital.Economy.FixtureBuildIronStructure(0, "hospital");
        hospital.RunSeasons(1);
        hospital.Economy.FixtureSetOutbreak(0, OutbreakClass.Plague);
        Assert.Equal(baselineHazard * Fix64.FromRatio(4, 5), hospital.Economy.FixtureInfectedHazard(0));

        hospital.Economy.FixtureStock(0, "G-SPIRITS", Fix64.One);
        Assert.Equal(baselineHazard * Fix64.FromRatio(3, 4), hospital.Economy.FixtureInfectedHazard(0));

        hospital.Economy.FixtureClearRoles(0);
        Assert.Equal(baselineHazard, hospital.Economy.FixtureInfectedHazard(0));

        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "KEY-5", "MED-07", "CHM-06");
        iron.Economy.FixtureSetOutbreak(0, OutbreakClass.Plague);
        Fix64 ironHazard = iron.Economy.FixtureInfectedHazard(0);
        iron.Economy.FixtureStock(0, "G-SPIRITS", Fix64.One);
        Assert.Equal(ironHazard, iron.Economy.FixtureInfectedHazard(0));
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

    [Fact]
    public void Version_45_snapshot_migrates_legacy_Fief_goods_and_defaults_spirits_to_zero()
    {
        BronzeFixture live = Fief("KEY-5", "CHM-06");
        live.Economy.FixtureStock(0, "G-PAPER", Fix64.Parse("2.25"));
        live.Economy.FixtureStock(0, "G-BOOK", Fix64.Parse("3.75"));
        Assert.Equal(Fix64.Parse("2.25"), live.Economy.GoodStock(0, "G-PAPER"));
        Assert.Equal(Fix64.Parse("3.75"), live.Economy.GoodStock(0, "G-BOOK"));
        BronzeFixture currentRoundTrip = Fief("KEY-5", "CHM-06");
        Restore(currentRoundTrip.Economy, Snapshot(live.Economy));
        Assert.Equal(Fix64.Parse("2.25"), currentRoundTrip.Economy.GoodStock(0, "G-PAPER"));
        live.Economy.FixtureLegacyIronStructureCount = live.Economy.FixtureIronKindCount - 1; // pre-Arsenal layout
        byte[] legacySnapshot = AsVersion45Snapshot(Snapshot(live.Economy));

        BronzeFixture resumed = Fief("KEY-5", "CHM-06");
        Restore(resumed.Economy, legacySnapshot);

        Assert.Equal(45, resumed.Economy.FiefSnapshotVersionForRestore);
        Assert.Equal(Fix64.Parse("2.25"), resumed.Economy.GoodStock(0, "G-PAPER"));
        Assert.Equal(Fix64.Parse("3.75"), resumed.Economy.GoodStock(0, "G-BOOK"));
        Assert.Equal(Fix64.Zero, resumed.Economy.GoodStock(0, "G-SPIRITS"));
        Assert.Equal(live.Economy.ComputeChecksum(), resumed.Economy.ComputeChecksum());
    }

    private static byte[] AsVersion45Snapshot(byte[] current)
    {
        current = WithoutFiefFissionScaleTail(current);
        long paper = Fix64.Parse("2.25").Raw, book = Fix64.Parse("3.75").Raw;
        using var anchorStream = new MemoryStream();
        using (var writer = new BinaryWriter(anchorStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Fix64.Parse("2.25").Raw);
            writer.Write(Fix64.Parse("3.75").Raw);
            writer.Write(Fix64.Zero.Raw);
        }
        byte[] anchor = anchorStream.ToArray();
        int valuesOffset = FindBytes(current, anchor);
        if (valuesOffset < sizeof(int) + 5 * sizeof(long)) throw new InvalidDataException("Could not locate Fief paper and book stocks.");
        int offset = valuesOffset - (sizeof(int) + 5 * sizeof(long));
        if (BitConverter.ToInt32(current, offset) != 10) throw new InvalidDataException("Located Fief-good values without their array header.");

        byte[] legacy = new byte[current.Length - 3 * sizeof(long)];
        Array.Copy(current, 0, legacy, 0, offset);
        BitConverter.GetBytes(7).CopyTo(legacy, offset);
        Array.Copy(current, offset + sizeof(int), legacy, offset + sizeof(int), 7 * sizeof(long));
        Array.Copy(current, offset + sizeof(int) + 10 * sizeof(long), legacy, offset + sizeof(int) + 7 * sizeof(long),
            current.Length - offset - sizeof(int) - 10 * sizeof(long));
        BitConverter.GetBytes(45).CopyTo(legacy, sizeof(int));
        if (BitConverter.ToInt32(legacy, offset) != 7 ||
            BitConverter.ToInt64(legacy, offset + sizeof(int) + 5 * sizeof(long)) != paper ||
            BitConverter.ToInt64(legacy, offset + sizeof(int) + 6 * sizeof(long)) != book)
            throw new InvalidDataException("The generated v45 array does not contain its legacy Paper and Book stocks.");
        return legacy;
    }

    [Fact]
    public void Version_46_snapshot_migrates_eight_Fief_goods_and_defaults_crucible_goods_to_zero()
    {
        BronzeFixture live = Fief("KEY-5", "CHM-06");
        live.Economy.FixtureSetGoodStock(0, "G-PAPER", Fix64.Parse("2.25"));
        live.Economy.FixtureSetGoodStock(0, "G-BOOK", Fix64.Parse("3.75"));
        live.Economy.FixtureSetGoodStock(0, "G-SPIRITS", Fix64.Parse("1.5"));
        live.Economy.FixtureLegacyIronStructureCount = live.Economy.FixtureIronKindCount - 1; // pre-Arsenal layout
        byte[] legacySnapshot = AsVersion46Snapshot(Snapshot(live.Economy));

        BronzeFixture resumed = Fief("KEY-5", "CHM-06");
        Restore(resumed.Economy, legacySnapshot);

        Assert.Equal(46, resumed.Economy.FiefSnapshotVersionForRestore);
        Assert.Equal(Fix64.Parse("2.25"), resumed.Economy.GoodStock(0, "G-PAPER"));
        Assert.Equal(Fix64.Parse("3.75"), resumed.Economy.GoodStock(0, "G-BOOK"));
        Assert.Equal(Fix64.Parse("1.5"), resumed.Economy.GoodStock(0, "G-SPIRITS"));
        Assert.Equal(Fix64.Zero, resumed.Economy.GoodStock(0, "G-CSTEEL"));
        Assert.Equal(Fix64.Zero, resumed.Economy.GoodStock(0, "G-ARMS5"));
    }

    private static byte[] AsVersion46Snapshot(byte[] current)
    {
        current = WithoutFiefFissionScaleTail(current);
        long paper = Fix64.Parse("2.25").Raw, book = Fix64.Parse("3.75").Raw;
        using var anchorStream = new MemoryStream();
        using (var writer = new BinaryWriter(anchorStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Fix64.Parse("2.25").Raw);
            writer.Write(Fix64.Parse("3.75").Raw);
            writer.Write(Fix64.Parse("1.5").Raw);
        }
        byte[] anchor = anchorStream.ToArray();
        int valuesOffset = FindBytes(current, anchor);
        int offset = valuesOffset - (sizeof(int) + 5 * sizeof(long));
        if (valuesOffset < 0 || BitConverter.ToInt32(current, offset) != 10) throw new InvalidDataException("Could not locate current Fief-good array.");
        byte[] legacy = new byte[current.Length - 2 * sizeof(long)];
        Array.Copy(current, 0, legacy, 0, offset);
        BitConverter.GetBytes(8).CopyTo(legacy, offset);
        Array.Copy(current, offset + sizeof(int), legacy, offset + sizeof(int), 8 * sizeof(long));
        Array.Copy(current, offset + sizeof(int) + 10 * sizeof(long), legacy, offset + sizeof(int) + 8 * sizeof(long),
            current.Length - offset - sizeof(int) - 10 * sizeof(long));
        BitConverter.GetBytes(46).CopyTo(legacy, sizeof(int));
        if (BitConverter.ToInt64(legacy, offset + sizeof(int) + 7 * sizeof(long)) != Fix64.Parse("1.5").Raw ||
            BitConverter.ToInt64(legacy, offset + sizeof(int) + 5 * sizeof(long)) != paper ||
            BitConverter.ToInt64(legacy, offset + sizeof(int) + 6 * sizeof(long)) != book)
            throw new InvalidDataException("Generated v46 array has invalid legacy Fief stocks.");
        return legacy;
    }

    private static byte[] WithoutFiefFissionScaleTail(byte[] current)
    {
        const int tailBytes = sizeof(int) + sizeof(int) + 8 * sizeof(long);
        int offset = current.Length - tailBytes;
        if (offset < 0 || BitConverter.ToInt32(current, offset) != 0x53495346 ||
            BitConverter.ToInt32(current, offset + sizeof(int)) != 8)
            throw new InvalidDataException("Could not locate the Fief fission-scale snapshot tail.");
        return current[..offset];
    }

    private static int FindBytes(byte[] source, byte[] value)
    {
        for (int i = 0; i <= source.Length - value.Length; i++)
        {
            int j = 0;
            while (j < value.Length && source[i + j] == value[j]) j++;
            if (j == value.Length) return i;
        }
        return -1;
    }

    [Fact]
    public void Snapshot_accepts_prior_care_work_after_provider_turns_45_and_rejects_work_above_global_capacity()
    {
        BronzeFixture live = Fief("KEY-5", "MED-01", "MEC-08");
        live.Economy.FixtureBuildIronStructure(0, "clock_tower");
        live.Economy.FixtureSetCivicWorkMult(0, Fix64.FromRatio(115, 100));
        int provider = FirstHealthyAdult(live.Economy);
        long providerId = live.Economy.Figure(provider).Id;
        live.Economy.FixtureSetFigureAge(providerId, 44);
        Fix64 effectiveCapacity = live.Economy.FixtureWorkCapacity(provider);
        Assert.Equal(Fix64.FromRatio(115, 100) * Fix64.FromRatio(105, 100), effectiveCapacity);
        live.Economy.FixtureSetCareWork(provider, roles: 1, careWork: Fix64.Parse("1.2"), totalWork: effectiveCapacity);
        live.Economy.FixtureSetFigureAge(providerId, 45);
        Assert.Equal(Fix64.Half * Fix64.FromRatio(115, 100) * Fix64.FromRatio(105, 100),
            live.Economy.FixtureWorkCapacity(provider));
        byte[] snapshot = Snapshot(live.Economy);

        BronzeFixture resumed = Fief("KEY-5", "MED-01", "MEC-08");
        resumed.Economy.FixtureBuildIronStructure(0, "clock_tower");
        resumed.Economy.FixtureSetCivicWorkMult(0, Fix64.FromRatio(115, 100));
        Restore(resumed.Economy, snapshot);
        Assert.Equal(snapshot, Snapshot(resumed.Economy));

        live.Economy.FixtureSetCareWork(provider, roles: 1, careWork: Fix64.Parse("1.21"), totalWork: Fix64.Parse("1.21"));
        byte[] impossible = Snapshot(live.Economy);
        BronzeFixture invalid = Fief("KEY-5", "MED-01", "MEC-08");
        invalid.Economy.FixtureBuildIronStructure(0, "clock_tower");
        invalid.Economy.FixtureSetCivicWorkMult(0, Fix64.FromRatio(115, 100));
        Assert.Throws<InvalidDataException>(() => Restore(invalid.Economy, impossible));
    }

    private static int FirstHealthyAdult(LivingEconomyModel economy)
    {
        for (int i = 0; i < economy.FigureCount; i++)
            if (economy.Figure(i).Age is >= 14 and < 45 && economy.Figure(i).Health == Fix64.One) return i;
        throw new InvalidDataException("Fixture has no healthy adult.");
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
