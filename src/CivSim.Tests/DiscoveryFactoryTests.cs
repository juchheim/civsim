using System.IO;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>The Factory and local soot (M13 WP-E6-8 slice 1, docs/MVP/38 §5.7). Discovery chapter only.</summary>
public class DiscoveryFactoryTests
{
    private const ulong Seed = 20261019;
    private const string Factory = "factory";
    private static readonly string[] Kit = { "MET-08", "NRG-03", "MIN-04", "MIN-09", "NRG-06", "MEC-10" };

    private static BronzeFixture Town(int people = 150, params string[] projects)
    {
        BronzeFixture f = BronzeFixture.CreateDiscoveryWithCoal(TestPack.Pack, Seed, people, projects.Length == 0 ? Kit : projects);
        f.Economy.FixtureSetTier(0, 1);
        return f;
    }

    private static void Power(BronzeFixture f, int engines)
    {
        f.Economy.FixtureSetIronWork(0, "atmospheric_engine", engines);
        f.Economy.FixtureAddDiscoveryGood(0, "G-COAL", Fix64.FromInt(120));
        f.Economy.FixtureStaffAndBurnEngines(0, engineers: engines * 2);
    }

    [Fact]
    public void The_factory_gate_needs_MEC10_a_large_town_and_a_power_source_in_Discovery()
    {
        BronzeFixture ok = Town();
        ok.Economy.FixtureSetIronWork(0, "water_mill", 1);
        Assert.True(ok.Economy.FixtureIronGateOpen(0, Factory, includeAppetite: false));

        BronzeFixture noPower = Town();
        Assert.False(noPower.Economy.FixtureIronGateOpen(0, Factory, includeAppetite: false), "a factory with no power source is not raised");
        BronzeFixture small = Town(people: 60);
        small.Economy.FixtureSetIronWork(0, "water_mill", 1);
        Assert.False(small.Economy.FixtureIronGateOpen(0, Factory, includeAppetite: false));
        BronzeFixture noProject = Town(150, "MET-08", "NRG-03", "MIN-04", "MIN-09", "NRG-06");
        noProject.Economy.FixtureSetIronWork(0, "water_mill", 1);
        Assert.False(noProject.Economy.FixtureIronGateOpen(0, Factory, includeAppetite: false));

        BronzeFixture built = Town();
        built.Economy.FixtureSetIronWork(0, "water_mill", 1);
        built.Economy.FixtureSetIronWork(0, Factory, 1);
        Assert.False(built.Economy.FixtureIronGateOpen(0, Factory, includeAppetite: false), "one factory to a town");

        BronzeFixture fief = BronzeFixture.CreateFiefWithCoalDeposit(TestPack.Pack, Seed, 150, Kit);
        fief.Economy.FixtureSetTier(0, 1);
        fief.Economy.FixtureSetIronWork(0, "water_mill", 1);
        Assert.False(fief.Economy.FixtureIronGateOpen(0, Factory, includeAppetite: false), "no factory outside Discovery");
    }

    [Fact]
    public void A_staffed_powered_factory_cuts_recipe_labor_by_the_power_it_receives()
    {
        BronzeFixture f = Town();
        f.Economy.FixtureSetIronWork(0, Factory, 1);
        Assert.Equal(Fix64.One, f.Economy.FixtureFactoryLaborFactor(0));            // no crew, no power
        Power(f, engines: 1);
        f.Economy.FixtureStaffOperatives(0, 12);
        Fix64 half = f.Economy.FixtureFactoryCoverage(0);
        Assert.True(half > Fix64.Zero && half <= Fix64.One, "an engine's power reaches the factory");
        Assert.Equal(Fix64.One / (Fix64.One + half), f.Economy.FixtureFactoryLaborFactor(0));

        BronzeFixture full = Town();
        full.Economy.FixtureSetIronWork(0, Factory, 1);
        Power(full, engines: 2);
        full.Economy.FixtureStaffOperatives(0, 12);
        Assert.Equal(Fix64.One, full.Economy.FixtureFactoryCoverage(0));
        Assert.True(Fix64.Half == full.Economy.FixtureFactoryLaborFactor(0), "x2 output per worker at full power");

        BronzeFixture partCrew = Town();
        partCrew.Economy.FixtureSetIronWork(0, Factory, 1);
        Power(partCrew, engines: 2);
        partCrew.Economy.FixtureStaffOperatives(0, 6);
        Assert.True(Fix64.Half == partCrew.Economy.FixtureFactoryCoverage(0), "half the crew, half the effect");
    }

    [Fact]
    public void A_working_factory_soots_its_tile_and_the_ring_around_it_and_the_soot_fades()
    {
        BronzeFixture f = Town();
        f.Economy.FixtureSetIronWork(0, Factory, 1);
        Power(f, engines: 2);
        f.Economy.FixtureStaffOperatives(0, 12);
        int site = f.Economy.SettlementLite(0).SiteTile;
        Assert.Equal(Fix64.Zero, f.Economy.FixtureSootAt(site));
        f.Economy.FixtureEmitSoot(0);
        Assert.Equal(Fix64.One, f.Economy.FixtureSootAt(site));
        for (int i = 0; i < 30; i++) f.Economy.FixtureEmitSoot(0);
        Assert.True(Fix64.FromInt(10) == f.Economy.FixtureSootAt(site), "capped at ten");
        f.Economy.FixtureDecaySootEverywhere();
        Assert.Equal(Fix64.FromInt(9), f.Economy.FixtureSootAt(site));

        BronzeFixture idle = Town();
        idle.Economy.FixtureSetIronWork(0, Factory, 1);
        idle.Economy.FixtureEmitSoot(0); // no crew, no power
        Assert.Equal(Fix64.Zero, idle.Economy.FixtureSootAt(idle.Economy.SettlementLite(0).SiteTile));
    }

    [Fact]
    public void Soot_wears_on_the_towns_health_only_where_it_lies_and_never_outside_Discovery()
    {
        BronzeFixture f = Town();
        f.Economy.FixtureSetIronWork(0, Factory, 1);
        Power(f, engines: 2);
        f.Economy.FixtureStaffOperatives(0, 12);
        Assert.True(Fix64.One == f.Economy.FixtureSootFeverFactor(0), "clean air");
        for (int i = 0; i < 5; i++) f.Economy.FixtureEmitSoot(0);
        Assert.True(Fix64.One + Fix64.FromInt(5) / 50 == f.Economy.FixtureSootFeverFactor(0));
        for (int i = 0; i < 20; i++) f.Economy.FixtureEmitSoot(0);
        Assert.True(Fix64.One + Fix64.One / 5 == f.Economy.FixtureSootFeverFactor(0), "capped at a fifth more");

        BronzeFixture fief = BronzeFixture.CreateFiefWithCoalDeposit(TestPack.Pack, Seed, 150, Kit);
        Assert.True(Fix64.One == fief.Economy.FixtureSootFeverFactor(0));
    }

    [Fact]
    public void Soot_and_the_factory_survive_a_snapshot_restore_and_continuation()
    {
        BronzeFixture live = Town();
        live.Economy.FixtureSetIronWork(0, Factory, 1);
        Power(live, engines: 2);
        live.Economy.FixtureStaffOperatives(0, 12);
        live.RunSeasons(3);
        live.Economy.FixtureEmitSoot(0);
        byte[] saved = Snapshot(live.Economy);

        BronzeFixture resumed = Town();
        resumed.RunSeasons(3);
        Restore(resumed.Economy, saved);
        int site = live.Economy.SettlementLite(0).SiteTile;
        Assert.Equal(live.Economy.FixtureSootAt(site), resumed.Economy.FixtureSootAt(site));
        Assert.Equal(live.Economy.ComputeChecksum(), resumed.Economy.ComputeChecksum());

        live.RunSeasons(3);
        resumed.RunSeasons(3);
        Assert.Equal(Snapshot(live.Economy), Snapshot(resumed.Economy));
    }

    [Fact]
    public void Natural_play_raises_staffs_and_powers_a_factory_that_then_soots_the_town()
    {
        BronzeFixture f = Town(160);
        f.Economy.FixtureSetIronWork(0, "atmospheric_engine", 1);
        f.Economy.FixtureAddDiscoveryGood(0, "G-COAL", Fix64.FromInt(100));
        f.Economy.FixtureStock(0, "G-BLOCK", Fix64.FromInt(40));
        f.Economy.FixtureStock(0, "G-WOOD", Fix64.FromInt(60));
        f.Economy.FixtureStock(0, "G-IRON", Fix64.FromInt(10));
        int site = f.Economy.SettlementLite(0).SiteTile;
        bool powered = false;
        Fix64 mostSoot = Fix64.Zero;
        for (int season = 0; season < 12; season++)
        {
            f.RunSeasons(1);
            if (f.Economy.FixtureFactoryCoverage(0) > Fix64.Zero) powered = true;
            if (f.Economy.FixtureSootAt(site) > mostSoot) mostSoot = f.Economy.FixtureSootAt(site);
        }
        Assert.Equal(1, f.Economy.FixtureIronStructureCount(0, Factory));
        Assert.True(powered, "the planner staffed the crew and the engine's power reached the factory");
        Assert.True(mostSoot > Fix64.Zero, "a working factory soots its tile");
    }

    private static byte[] Snapshot(LivingEconomyModel m)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) m.WriteSnapshot(writer);
        return stream.ToArray();
    }

    private static void Restore(LivingEconomyModel m, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);
        m.ReadSnapshot(reader);
    }
}
