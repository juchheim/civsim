using System.IO;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>MEC-08 Clock Tower's local work and cohesion effects (WP-E5-5).</summary>
public sealed class FiefClockTowerTests
{
    private const ulong Seed = 20261114;
    private static BronzeFixture Fief(params string[] projects)
        => BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, projects);

    [Fact]
    public void Clock_tower_is_a_Fief_build_unlocked_by_MEC_08_only()
    {
        BronzeFixture locked = Fief("KEY-5", "MEC-07", "NUM-06");
        locked.Economy.FixtureSetTier(0, 1);
        Assert.DoesNotContain(locked.Economy.BuildOutlookFor(0).Items, item => item.Id == "clock_tower");

        BronzeFixture fief = Fief("KEY-5", "MEC-08");
        fief.Economy.FixtureSetTier(0, 1);
        Assert.Contains(fief.Economy.BuildOutlookFor(0).Items, item => item.Id == "clock_tower");
        Assert.True(fief.Economy.FixtureIronBuildWanted(0, "clock_tower"));

        BronzeFixture iron = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 100, "KEY-5", "MEC-08");
        Assert.Contains(iron.Economy.BuildOutlookFor(0).Items,
            item => item.Id == "clock_tower" && item.Gate.Why == BuildWhy.NoSuchStructure);
        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, Seed, 100, true, true, true, true, "MEC-08");
        Assert.DoesNotContain(bronze.Economy.BuildOutlookFor(0).Items, item => item.Id == "clock_tower");
    }

    [Fact]
    public void A_built_clock_tower_adds_five_percent_work_and_two_cohesion_only_through_six_tiles()
    {
        BronzeFixture f = Fief("KEY-5", "MEC-08");
        int adult = FirstAdult(f.Economy);
        Assert.Equal(Fix64.One, f.Economy.FixtureWorkCapacity(adult));

        (BronzeFixture near, int nearSettlement) = FiefWithProvinceAtDistance(6);
        (BronzeFixture far, int farSettlement) = FiefWithProvinceAtDistance(7);
        near.Economy.FixtureSetTier(nearSettlement, 0);
        far.Economy.FixtureSetTier(farSettlement, 0);
        int nearBefore = near.Economy.FixtureCohesionTarget(nearSettlement);
        int farBefore = far.Economy.FixtureCohesionTarget(farSettlement);
        Assert.False(near.Economy.FixtureClockTowerWithin(nearSettlement));

        f.Economy.FixtureBuildIronStructure(0, "clock_tower");
        Assert.Equal(Fix64.FromRatio(105, 100), f.Economy.FixtureWorkCapacity(adult));
        near.Economy.FixtureBuildIronStructure(0, "clock_tower");
        far.Economy.FixtureBuildIronStructure(0, "clock_tower");
        Assert.True(near.Economy.FixtureClockTowerWithin(nearSettlement));
        Assert.False(far.Economy.FixtureClockTowerWithin(farSettlement));
        Assert.Equal(nearBefore + 2, near.Economy.FixtureCohesionTarget(nearSettlement));
        Assert.Equal(farBefore, far.Economy.FixtureCohesionTarget(farSettlement));
    }

    [Fact]
    public void Clock_tower_state_survives_Fief_snapshot_and_has_no_Iron_or_Bronze_effect()
    {
        BronzeFixture live = Fief("KEY-5", "MEC-08");
        live.Economy.FixtureBuildIronStructure(0, "clock_tower");
        live.RunSeasons(2);
        byte[] saved = Snapshot(live.Economy);

        BronzeFixture resumed = Fief("KEY-5", "MEC-08");
        resumed.RunSeasons(2);
        Restore(resumed.Economy, saved);
        Assert.True(resumed.Economy.FixtureClockTowerWithin(0));
        Assert.Equal(live.Economy.ComputeChecksum(), resumed.Economy.ComputeChecksum());
        live.RunSeasons(2);
        resumed.RunSeasons(2);
        Assert.Equal(Snapshot(live.Economy), Snapshot(resumed.Economy));

        BronzeFixture ironA = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 100, "KEY-5", "MEC-08");
        BronzeFixture ironB = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 100, "KEY-5", "MEC-08");
        ironA.Economy.FixtureSetTier(0, 1);
        ironB.Economy.FixtureSetTier(0, 1);
        int ironWork = FirstAdult(ironA.Economy);
        Fix64 ironBefore = ironA.Economy.FixtureWorkCapacity(ironWork);
        Assert.False(ironA.Economy.FixtureClockTowerWithin(0));
        Assert.Equal(ironBefore, ironB.Economy.FixtureWorkCapacity(FirstAdult(ironB.Economy)));
        Assert.Equal(ironA.Economy.FixtureCohesionTarget(0), ironB.Economy.FixtureCohesionTarget(0));
        Assert.Contains(ironA.Economy.BuildOutlookFor(0).Items,
            item => item.Id == "clock_tower" && item.Gate.Why == BuildWhy.NoSuchStructure);
        ironA.RunSeasons(2);
        ironB.RunSeasons(2);
        Assert.Equal(Snapshot(ironA.Economy), Snapshot(ironB.Economy));
        Assert.Equal(ironA.Economy.ComputeChecksum(), ironB.Economy.ComputeChecksum());

        BronzeFixture bronzeA = BronzeFixture.Create(TestPack.Pack, Seed, 100, true, true, true, true, "MEC-08");
        BronzeFixture bronzeB = BronzeFixture.Create(TestPack.Pack, Seed, 100, true, true, true, true, "MEC-08");
        Assert.DoesNotContain(bronzeA.Economy.BuildOutlookFor(0).Items, item => item.Id == "clock_tower");
        bronzeA.RunSeasons(2);
        bronzeB.RunSeasons(2);
        Assert.Equal(Snapshot(bronzeA.Economy), Snapshot(bronzeB.Economy));
        Assert.Equal(bronzeA.Economy.ComputeChecksum(), bronzeB.Economy.ComputeChecksum());
    }

    private static int FirstAdult(LivingEconomyModel economy)
    {
        for (int i = 0; i < economy.FigureCount; i++)
            if (economy.Figure(i).Age is >= 14 and < 45 && economy.Figure(i).Health == Fix64.One) return i;
        throw new InvalidDataException("Fixture has no healthy adult.");
    }

    private static (BronzeFixture Fixture, int Settlement) FiefWithProvinceAtDistance(int distance)
    {
        BronzeFixture f = Fief("KEY-5", "MEC-08");
        int origin = f.Economy.SettlementLite(0).SiteTile;
        for (int tile = 0; tile < f.Economy.FixtureWorldTileCount; tile++)
        {
            if (HexGrid.Distance(origin, tile, f.Economy.FixtureWorldWidth) != distance) continue;
            int settlement = f.Economy.FoundByDecree(0, tile, Fix64.FromRatio(2, 5), minDaughter: 6, startCohesion: 60);
            if (settlement < 0) continue;
            f.Economy.FixtureSetTier(0, 0);
            f.Economy.FixtureSetTier(settlement, 0);
            return (f, settlement);
        }
        throw new InvalidDataException($"No valid settlement site at distance {distance}.");
    }

    private static byte[] Snapshot(LivingEconomyModel economy)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) economy.WriteSnapshot(writer);
        return stream.ToArray();
    }

    private static void Restore(LivingEconomyModel economy, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        economy.ReadSnapshot(reader);
        Assert.Equal(stream.Length, stream.Position);
    }
}
