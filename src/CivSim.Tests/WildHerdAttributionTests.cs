using System.IO;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

public sealed class WildHerdAttributionTests
{
    private const ulong Seed = 20261019;

    private static BronzeFixture Fixture() => BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true);

    [Fact]
    public void Hunt_that_reduces_a_herd_to_zero_records_the_hunting_civilization()
    {
        BronzeFixture fixture = Fixture();
        LivingEconomyModel economy = fixture.Economy;
        int tile = economy.Settlement(0).SiteTile;
        economy.FixtureSetWildHerd(0, tile, Fix64.One);

        economy.FixtureHuntWildHerd(0, Fix64.FromInt(100), Fix64.One);

        WildHerdView herd = economy.WildHerd(0);
        Assert.Equal(Fix64.Zero, herd.AnimalCount);
        Assert.Equal(0, herd.LastHuntCrashCivilization);
    }

    [Fact]
    public void Natural_herd_crash_does_not_record_a_hunting_civilization()
    {
        BronzeFixture fixture = Fixture();
        LivingEconomyModel economy = fixture.Economy;
        WildHerdView before = economy.WildHerd(0);

        economy.FixtureApplyNaturalHerdCrash(0);

        WildHerdView after = economy.WildHerd(0);
        Assert.Equal(before.AnimalCount * Fix64.Half, after.AnimalCount);
        Assert.Equal(-1, after.LastHuntCrashCivilization);
    }

    [Fact]
    public void Hunt_attribution_survives_snapshot_resume_and_checksum()
    {
        BronzeFixture fixture = Fixture();
        LivingEconomyModel economy = fixture.Economy;
        economy.FixtureSetWildHerd(0, economy.Settlement(0).SiteTile, Fix64.One);
        economy.FixtureHuntWildHerd(0, Fix64.FromInt(100), Fix64.One);
        ulong checksum = economy.ComputeChecksum();

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) economy.WriteSnapshot(writer);
        stream.Position = 0;
        LivingEconomyModel restored = Fixture().Economy;
        using (var reader = new BinaryReader(stream)) restored.ReadSnapshot(reader);

        Assert.Equal(checksum, restored.ComputeChecksum());
        Assert.Equal(0, restored.WildHerd(0).LastHuntCrashCivilization);
    }
}
