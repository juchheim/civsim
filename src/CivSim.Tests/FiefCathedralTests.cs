using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>Fief-only BLD-12 Gothic Cathedral mechanics and snapshot tail.</summary>
public sealed class FiefCathedralTests
{
    private const ulong Seed = 20261114;
    private static BronzeFixture Fief(params string[] projects)
        => BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, projects);

    [Fact]
    public void Cathedral_requires_Fief_prerequisites_town_and_a_staffed_master_mason()
    {
        BronzeFixture f = Fief("KEY-5", "BLD-11", "STN-13", "BLD-12");
        f.Economy.FixtureSetTier(0, 1);
        Fix64 unsupervisedLabor = f.Economy.FixtureIronLabor(0, "gothic_cathedral");
        Assert.False(f.Economy.FixtureIronBuildWanted(0, "gothic_cathedral"));
        f.RunSeasons(1);
        Assert.Equal(1, f.Economy.FixtureRoleCount(0, "master_mason"));
        Assert.Equal(Fix64.FromRatio(2, 3), f.Economy.FixtureIronLabor(0, "gothic_cathedral") / unsupervisedLabor);
        Assert.True(f.Economy.FixtureIronBuildWanted(0, "gothic_cathedral"));

        BronzeFixture village = Fief("KEY-5", "BLD-11", "STN-13", "BLD-12");
        Assert.False(village.Economy.FixtureIronBuildWanted(0, "gothic_cathedral"));

        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "KEY-5", "BLD-11", "STN-13", "BLD-12");
        iron.Economy.FixtureSetTier(0, 1);
        Assert.False(iron.Economy.FixtureIronBuildWanted(0, "gothic_cathedral"));
    }

    [Fact]
    public void Cathedral_glass_ashlar_and_wood_are_paid_once_and_conserved()
    {
        BronzeFixture f = Fief("KEY-5", "BLD-11", "STN-13", "BLD-12");
        f.Economy.FixtureSetGoodStock(0, "G-ASHLAR", Fix64.FromInt(39));
        f.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(20));
        f.Economy.FixtureSetGoodStock(0, "G-GLASS", Fix64.FromInt(4));
        Assert.False(f.Economy.FixtureTryPayIronBuild(0, "gothic_cathedral"));
        Assert.Equal(Fix64.FromInt(39), f.Economy.FixtureGoodStock(0, "G-ASHLAR"));

        f.Economy.FixtureSetGoodStock(0, "G-ASHLAR", Fix64.FromInt(40));
        Assert.True(f.Economy.FixtureTryPayIronBuild(0, "gothic_cathedral"));
        Assert.Equal(Fix64.Zero, f.Economy.FixtureGoodStock(0, "G-ASHLAR"));
        Assert.Equal(Fix64.Zero, f.Economy.FixtureGoodStock(0, "G-WOOD"));
        Assert.Equal(Fix64.Zero, f.Economy.FixtureGoodStock(0, "G-GLASS"));
    }

    [Fact]
    public void Cathedral_is_a_world_capped_wonder_and_grants_realm_cohesion_and_endpoint_trade_only_in_Fief()
    {
        BronzeFixture f = Fief("KEY-5", "BLD-11", "STN-13", "BLD-12");
        f.Economy.FixtureSetTier(0, 1);
        int before = f.Economy.FixtureCohesionTarget(0);
        f.Economy.FixtureCompleteIron(0, "gothic_cathedral");
        Assert.Equal(3, f.Economy.FixtureCohesionTarget(0) - before);
        Assert.Equal(Fix64.FromRatio(11, 10), f.Economy.PilgrimageTradeMultiplier(0));
        Assert.False(f.Economy.FixtureIronBuildWanted(0, "gothic_cathedral"));

        BronzeFixture noCathedral = Fief("KEY-5", "BLD-11", "STN-13", "BLD-12");
        Assert.Equal(Fix64.One, noCathedral.Economy.PilgrimageTradeMultiplier(0));

        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "KEY-5", "BLD-11", "STN-13", "BLD-12");
        iron.Economy.FixtureSetTier(0, 1);
        int ironCohesion = iron.Economy.FixtureCohesionTarget(0);
        iron.Economy.FixtureCompleteIron(0, "gothic_cathedral");
        Assert.Equal(ironCohesion, iron.Economy.FixtureCohesionTarget(0));
        Assert.Equal(Fix64.One, iron.Economy.PilgrimageTradeMultiplier(0));
        Assert.Equal(1, iron.Economy.FixtureIronStructureCount(0, "gothic_cathedral"));
    }

    [Fact]
    public void Cathedral_world_cap_and_role_state_continue_from_Fief_snapshot()
    {
        BronzeFixture live = Fief("KEY-5", "BLD-11", "STN-13", "BLD-12");
        live.Economy.FixtureSetTier(0, 1);
        live.RunSeasons(2);
        Assert.Equal(1, live.Economy.FixtureRoleCount(0, "master_mason"));
        live.Economy.FixtureCompleteIron(0, "gothic_cathedral");
        live.RunSeasons(1);
        byte[] saved = Snapshot(live.Economy);

        BronzeFixture resumed = Fief("KEY-5", "BLD-11", "STN-13", "BLD-12");
        resumed.Economy.FixtureSetTier(0, 1);
        resumed.RunSeasons(3);
        Restore(resumed.Economy, saved);
        Assert.Equal(live.Economy.ComputeChecksum(), resumed.Economy.ComputeChecksum());
        Assert.Equal(Fix64.FromRatio(11, 10), resumed.Economy.PilgrimageTradeMultiplier(0));
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
