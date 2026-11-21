using System.IO;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>The atmospheric engine, pumping and the steam share of the power ledger (M13 WP-E6-5 slice 2, docs/MVP/38 §5.1). Discovery chapter only.</summary>
public class DiscoverySteamTests
{
    private const ulong Seed = 20261019;
    private const string Engine = "atmospheric_engine";
    private static readonly string[] SteamKit = { "MET-08", "NRG-03", "MIN-04", "MIN-09", "NRG-06" };

    private static BronzeFixture Discovery(params string[] projects) => BronzeFixture.CreateDiscoveryWithCoal(TestPack.Pack, Seed, 100, projects);
    private static BronzeFixture DeepCoal(params string[] projects) => BronzeFixture.CreateDiscoveryWithDeepCoal(TestPack.Pack, Seed, 100, projects);
    private static BronzeFixture NoCoal(params string[] projects) => BronzeFixture.CreateDiscoveryWithoutCoal(TestPack.Pack, Seed, 100, projects);
    private static BronzeFixture FiefCoal(params string[] projects) => BronzeFixture.CreateFiefWithCoalDeposit(TestPack.Pack, Seed, 100, projects);

    private static BronzeFixture Town(BronzeFixture f)
    {
        f.Economy.FixtureSetTier(0, 1);
        return f;
    }

    [Fact]
    public void The_engine_gate_needs_NRG06_a_town_coal_and_a_Discovery_match()
    {
        Assert.True(Town(Discovery(SteamKit)).Economy.FixtureIronGateOpen(0, Engine, includeAppetite: false));

        Assert.False(Town(Discovery("MET-08", "NRG-03", "MIN-09")).Economy.FixtureIronGateOpen(0, Engine, includeAppetite: false));   // NRG-06 not learned
        Assert.False(Discovery(SteamKit).Economy.FixtureIronGateOpen(0, Engine, includeAppetite: false));                              // a village is not a town
        Assert.False(Town(NoCoal(SteamKit)).Economy.FixtureIronGateOpen(0, Engine, includeAppetite: false));                           // no coal in store or reach

        BronzeFixture noSeam = Town(NoCoal(SteamKit));
        noSeam.Economy.FixtureAddDiscoveryGood(0, "G-COAL", Fix64.FromInt(20));
        Assert.True(noSeam.Economy.FixtureIronGateOpen(0, Engine, includeAppetite: false), "coal in store is enough");

        BronzeFixture fief = Town(FiefCoal(SteamKit));
        Assert.False(fief.Economy.FixtureIronGateOpen(0, Engine, includeAppetite: false), "a Fief match never builds an engine");

        BronzeFixture two = Town(Discovery(SteamKit));
        two.Economy.FixtureSetIronWork(0, Engine, 2);
        Assert.False(two.Economy.FixtureIronGateOpen(0, Engine, includeAppetite: false), "two to a town");
    }

    [Fact]
    public void A_staffed_engine_burns_one_litre_of_coal_per_unit_and_an_unstaffed_or_coalless_one_idles()
    {
        BronzeFixture f = Town(Discovery(SteamKit));
        f.Economy.FixtureSetIronWork(0, Engine, 1);
        f.Economy.FixtureAddDiscoveryGood(0, "G-COAL", Fix64.FromInt(50));

        f.Economy.FixtureStaffAndBurnEngines(0, engineers: 1);
        Assert.Equal(Fix64.Zero, f.Economy.SteamReadout(0).Units);                      // one engineer is not a crew
        Assert.Equal(Fix64.FromInt(50), f.Economy.CoalStock(0));

        f.Economy.FixtureStaffAndBurnEngines(0, engineers: 1);                           // the second engineer arrives
        var fired = f.Economy.SteamReadout(0);
        Assert.Equal((1, 1), (fired.Engines, fired.Staffed));
        Assert.Equal(Fix64.FromInt(10), fired.Units);
        Assert.Equal(Fix64.FromInt(40), f.Economy.CoalStock(0));                         // 10 units cost 10 L

        BronzeFixture dry = Town(Discovery(SteamKit));
        dry.Economy.FixtureSetIronWork(0, Engine, 1);
        dry.Economy.FixtureStaffAndBurnEngines(0, engineers: 2);
        Assert.Equal(Fix64.Zero, dry.Economy.SteamReadout(0).Units);                    // a crew and no coal
        Assert.Equal(Fix64.Zero, dry.Economy.CoalStock(0));

        BronzeFixture some = Town(Discovery(SteamKit));
        some.Economy.FixtureSetIronWork(0, Engine, 1);
        some.Economy.FixtureAddDiscoveryGood(0, "G-COAL", Fix64.FromInt(4));
        some.Economy.FixtureStaffAndBurnEngines(0, engineers: 2);
        Assert.Equal(Fix64.FromInt(4), some.Economy.SteamReadout(0).Units);             // a part-fed engine gives part power
        Assert.Equal(Fix64.Zero, some.Economy.CoalStock(0));
    }

    [Fact]
    public void Steam_is_a_source_in_the_shared_power_ledger_and_needs_no_river()
    {
        BronzeFixture none = Town(Discovery(SteamKit));
        Assert.Equal(Fix64.Zero, none.Economy.FixtureMillPowerLedger(0).AvailablePowerHours);

        BronzeFixture f = Town(Discovery(SteamKit));
        f.Economy.FixtureSetIronWork(0, Engine, 1);
        f.Economy.FixtureAddDiscoveryGood(0, "G-COAL", Fix64.FromInt(50));
        f.Economy.FixtureStaffAndBurnEngines(0, engineers: 2);
        Assert.Equal(Fix64.FromInt(10), f.Economy.FixtureMillPowerLedger(0).AvailablePowerHours);

        // With a water mill as well the sources add, one ledger, no stacking multiplier: 5 (wheel) + 10 (engine).
        f.Economy.FixtureSetIronWork(0, "water_mill", 1);
        Assert.Equal(Fix64.FromInt(15), f.Economy.FixtureMillPowerLedger(0).AvailablePowerHours);
    }

    [Fact]
    public void A_deep_seam_floods_to_half_yield_until_an_engine_pumps_it()
    {
        BronzeFixture f = Town(DeepCoal(SteamKit));
        f.Economy.FixtureSetIronWork(0, Engine, 1);
        f.RunSeasons(6);
        int tile = f.Economy.FixtureCoalCampTile(0);
        Assert.True(tile >= 0, "the engine's coal want opened a coal camp");
        Assert.Equal(1, f.Economy.SteamReadout(0).Staffed);                              // the season step staffed two engineers on its own

        // Take the engine away and the seam floods again: the factor is read from what the last season did.
        f.Economy.FixtureSetIronWork(0, Engine, 0);
        f.Economy.FixtureStaffAndBurnEngines(0, engineers: 0);
        f.Economy.FixtureSetIronWork(0, Engine, 1);
        Assert.False(f.Economy.SteamReadout(0).Pumping);
        Assert.Equal(Fix64.Half, f.Economy.FixtureFloodedSeamFactor(0, tile));

        // A fired engine pumps: the seam is dry again and the engine's units fall by the fixed pumping draw.
        f.Economy.FixtureAddDiscoveryGood(0, "G-COAL", Fix64.FromInt(50));
        f.Economy.FixtureStaffAndBurnEngines(0, engineers: 2);
        var pumped = f.Economy.SteamReadout(0);
        Assert.True(pumped.Pumping);
        Assert.Equal(Fix64.One, f.Economy.FixtureFloodedSeamFactor(0, tile));
        Assert.Equal(Fix64.FromInt(8), pumped.Units);

        // The shallower seam of the ordinary fixture never floods.
        BronzeFixture shallow = Town(Discovery(SteamKit));
        shallow.Economy.FixtureSetIronWork(0, Engine, 1);
        shallow.RunSeasons(6);
        int shallowTile = shallow.Economy.FixtureCoalCampTile(0);
        Assert.True(shallowTile >= 0);
        Assert.Equal(Fix64.One, shallow.Economy.FixtureFloodedSeamFactor(0, shallowTile));
    }

    [Fact]
    public void A_flooded_seam_asks_for_the_first_engine()
    {
        BronzeFixture f = Town(DeepCoal(SteamKit));
        Assert.False(f.Economy.FixtureEngineWanted(0), "no camp, no flooding, and a village-sized power budget with no shortfall to speak of");
        f.Economy.FixtureSetIronWork(0, Engine, 1);
        f.RunSeasons(6);
        Assert.True(f.Economy.FixtureCoalCampTile(0) >= 0);
        f.Economy.FixtureSetIronWork(0, Engine, 0);
        Assert.True(f.Economy.FixtureEngineWanted(0));
    }

    [Fact]
    public void Steam_state_survives_a_snapshot_restore_and_continuation()
    {
        BronzeFixture live = Town(Discovery(SteamKit));
        live.Economy.FixtureSetIronWork(0, Engine, 1);
        live.Economy.FixtureAddDiscoveryGood(0, "G-COAL", Fix64.FromInt(60));
        live.Economy.FixtureStaffAndBurnEngines(0, engineers: 2);
        live.RunSeasons(2);
        byte[] saved = Snapshot(live.Economy);

        BronzeFixture resumed = Town(Discovery(SteamKit));
        resumed.RunSeasons(2);
        Restore(resumed.Economy, saved);
        Assert.Equal(live.Economy.SteamReadout(0).Units, resumed.Economy.SteamReadout(0).Units);
        Assert.Equal(live.Economy.SteamReadout(0).Engines, resumed.Economy.SteamReadout(0).Engines);
        Assert.Equal(live.Economy.ComputeChecksum(), resumed.Economy.ComputeChecksum());

        live.RunSeasons(4);
        resumed.RunSeasons(4);
        Assert.Equal(Snapshot(live.Economy), Snapshot(resumed.Economy));
        Assert.Equal(live.Economy.ComputeChecksum(), resumed.Economy.ComputeChecksum());
    }

    [Fact]
    public void Steam_state_never_enters_a_Fief_economy()
    {
        BronzeFixture fief = Town(FiefCoal(SteamKit));
        BronzeFixture plain = Town(BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, SteamKit));
        fief.RunSeasons(5);
        plain.RunSeasons(5);
        Assert.Equal(Fix64.Zero, fief.Economy.SteamReadout(0).Units);
        Assert.Equal(0, fief.Economy.SteamReadout(0).Engines);
        Assert.Equal(plain.Economy.FixtureGoodStock(0, "G-IRON"), fief.Economy.FixtureGoodStock(0, "G-IRON"));
    }

    [Fact]
    public void Coal_firing_halves_the_heat_wood_only_where_coal_is_in_store_and_charges_a_quarter_litre_of_coal_per_litre_of_wood()
    {
        string[] fir = { "MET-08", "NRG-03", "MIN-04", "MIN-09", "NRG-06", "FIR-11" };
        Fix64 baseline = Town(Discovery(SteamKit)).Economy.FixtureFuelNeed(0, winter: true);
        Assert.True(baseline > Fix64.Zero, "the fixture town burns wood for its hearths");

        // FIR-11 without coal in store: no relief, and nothing is charged.
        BronzeFixture noCoal = Town(Discovery(fir));
        Assert.Equal(baseline, noCoal.Economy.FixtureFuelNeed(0, winter: true));

        // With coal in store the town burns less wood; the relief is part of the hearth and kiln wood, never the building wood.
        BronzeFixture f = Town(Discovery(fir));
        f.Economy.FixtureAddDiscoveryGood(0, "G-COAL", Fix64.FromInt(50));
        Fix64 eased = f.Economy.FixtureFuelNeed(0, winter: true);
        Assert.True(eased < baseline);
        Assert.True(eased > baseline / 2, "building wood is not replaced by coal");
        Fix64 relief = baseline - eased;

        // The coal charge is a quarter litre of coal for every litre of wood not burned.
        f.Economy.FixtureBurnCoalForHeat(0, winter: true);
        Assert.Equal(Fix64.FromInt(50) - relief / 4, f.Economy.CoalStock(0));

        // A town holding too little coal for the whole relief gets none (all or nothing, no half-burned seasons).
        BronzeFixture scant = Town(Discovery(fir));
        scant.Economy.FixtureAddDiscoveryGood(0, "G-COAL", relief / 8);
        Assert.Equal(baseline, scant.Economy.FixtureFuelNeed(0, winter: true));

        // A Fief match with the same research and a coal deposit never sees FIR-11.
        Assert.Equal(Town(FiefCoal(SteamKit)).Economy.FixtureFuelNeed(0, winter: true), Town(FiefCoal(fir)).Economy.FixtureFuelNeed(0, winter: true));
    }

    private static readonly string[] OvenKit = { "MET-08", "MET-12", "NRG-03", "MIN-04", "MIN-09", "FIR-12", "MET-13" };

    [Fact]
    public void A_coke_oven_cokes_with_less_coal_and_labor_and_is_wanted_only_where_a_furnace_smelts_on_coke()
    {
        BronzeFixture f = Town(Discovery(OvenKit));
        var kiln = f.Economy.FixtureBatch(0, "make_coke");
        Assert.True(kiln.Input0 >= Fix64.FromRatio(3, 2));         // 1.5 L of coal (plus the guild-craft waste)
        Assert.Equal(Fix64.One, kiln.Output);

        f.Economy.FixtureSetIronWork(0, "coke_oven", 1);
        var oven = f.Economy.FixtureBatch(0, "make_coke");
        Assert.Equal(kiln.Input0 * Fix64.FromRatio(4, 5), oven.Input0);   // 1.2 L of coal per litre of coke against the kiln's 1.5 (both carry the guild-craft waste)
        Assert.Equal(Fix64.One, oven.Output);
        Assert.Equal(kiln.Labor / 2, oven.Labor);                 // in half the labor

        // Gate: FIR-12, a town, coal, and (as a want) a furnace smelting on coke.
        BronzeFixture g = Town(Discovery(OvenKit));
        Assert.True(g.Economy.FixtureIronGateOpen(0, "coke_oven", includeAppetite: false));
        Assert.False(g.Economy.FixtureIronGateOpen(0, "coke_oven", includeAppetite: true), "no blast furnace, so no coke smelting yet");
        g.Economy.FixtureSetIronWork(0, "water_mill", 1);
        g.Economy.FixtureSetIronWork(0, "blast_furnace", 1);
        Assert.True(g.Economy.FixtureIronGateOpen(0, "coke_oven", includeAppetite: true));
        Assert.False(Town(Discovery("MET-08", "MET-12", "NRG-03", "MIN-04", "MIN-09")).Economy.FixtureIronGateOpen(0, "coke_oven", includeAppetite: false), "FIR-12 not learned");
        Assert.False(Town(FiefCoal(OvenKit)).Economy.FixtureIronGateOpen(0, "coke_oven", includeAppetite: false), "never in Fief");
    }

    [Fact]
    public void A_navigation_canal_needs_TRN11_and_a_far_coal_camp_and_hauls_coal_at_ten_times()
    {
        string[] kit = { "MET-08", "NRG-03", "MIN-04", "MIN-09", "NRG-06", "TRN-11" };
        BronzeFixture noCamp = Town(Discovery(kit));
        Assert.False(noCamp.Economy.FixtureIronGateOpen(0, "navigation_canal", includeAppetite: false), "no coal camp yet");

        BronzeFixture f = Town(Discovery(kit));
        f.Economy.FixtureSetIronWork(0, Engine, 1);
        f.RunSeasons(6);
        Assert.True(f.Economy.FixtureCoalCampTile(0) >= 0);
        Assert.True(f.Economy.FixtureFarthestCoalCamp(0) >= 3, $"camp distance {f.Economy.FixtureFarthestCoalCamp(0)}");
        f.Economy.FixtureSetTier(0, 1);                             // the seasons re-derive the tier from the town's size
        // The planner itself dug the canal while the seasons ran (a coal camp three tiles out and TRN-11): the gate is either open or answers "already built".
        var gate = f.Economy.FixtureIronGate(0, "navigation_canal", includeAppetite: false);
        Assert.True(gate.Open || gate.Why == BuildWhy.AlreadyBuilt, gate.ToString());
        f.Economy.FixtureSetIronWork(0, "navigation_canal", 0);
        Assert.True(f.Economy.FixtureIronGateOpen(0, "navigation_canal", includeAppetite: false));
        Assert.False(Town(Discovery("MET-08", "NRG-03", "MIN-04", "MIN-09", "NRG-06")).Economy.FixtureIronGateOpen(0, "navigation_canal", includeAppetite: false), "TRN-11 not learned");

        Assert.Equal(Fix64.One, f.Economy.FixtureCoalHaulDivisor(0, Fix64.One));
        Assert.Equal(Fix64.FromInt(5), f.Economy.FixtureCoalHaulDivisor(0, Fix64.FromInt(5)));
        f.Economy.FixtureSetIronWork(0, "navigation_canal", 1);
        Assert.Equal(Fix64.FromInt(10), f.Economy.FixtureCoalHaulDivisor(0, Fix64.One));
        Assert.Equal(Fix64.FromInt(10), f.Economy.FixtureCoalHaulDivisor(0, Fix64.FromInt(5)));   // replaces the cart, does not stack
        Assert.False(f.Economy.FixtureIronGateOpen(0, "navigation_canal", includeAppetite: false), "one canal to a town");
        Assert.False(Town(FiefCoal(kit)).Economy.FixtureIronGateOpen(0, "navigation_canal", includeAppetite: false), "never in Fief");
    }

    [Fact]
    public void Turnpikes_speed_a_road_linked_town_a_further_half_again_in_Discovery_only()
    {
        string[] basic = { "TRN-07" };
        BronzeFixture plain = Town(Discovery("TRN-07"));
        plain.Economy.FixtureSetIronWork(0, "road", 1);
        int roadOnly = plain.Economy.FixtureRoadAdjustedWalk(0, 9);
        Assert.Equal(6, roadOnly);                                 // TRN-07: x1.5

        BronzeFixture pike = Town(Discovery("TRN-07", "TRN-10"));
        pike.Economy.FixtureSetIronWork(0, "road", 1);
        Assert.Equal(4, pike.Economy.FixtureRoadAdjustedWalk(0, 9)); // TRN-10: x1.5 again

        BronzeFixture unlinked = Town(Discovery("TRN-07", "TRN-10"));
        Assert.Equal(9, unlinked.Economy.FixtureRoadAdjustedWalk(0, 9));

        BronzeFixture fief = Town(FiefCoal("TRN-07", "TRN-10"));
        fief.Economy.FixtureSetIronWork(0, "road", 1);
        Assert.Equal(6, fief.Economy.FixtureRoadAdjustedWalk(0, 9));
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
