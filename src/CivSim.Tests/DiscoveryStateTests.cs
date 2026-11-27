using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>
/// The per-work state a Discovery town reports (docs/MVP/38 D-T8, docs/MVP/39 §8.1): every institution is working, queued, dormant or blocked with a reason, derived
/// from state the sim already keeps. Nothing here is stored; these tests pin what each reading means.
/// </summary>
public class DiscoveryStateTests
{
    private const ulong Seed = 20261019;
    private const string Engine = "atmospheric_engine";
    private static readonly string[] SteamKit = { "MET-08", "NRG-03", "MIN-04", "MIN-09", "NRG-06" };

    private static BronzeFixture Discovery(params string[] projects) => BronzeFixture.CreateDiscoveryWithCoal(TestPack.Pack, Seed, 100, projects);
    private static BronzeFixture DeepCoal(params string[] projects) => BronzeFixture.CreateDiscoveryWithDeepCoal(TestPack.Pack, Seed, 100, projects);

    private static BronzeFixture Town(BronzeFixture f)
    {
        f.Economy.FixtureSetTier(0, 1);
        return f;
    }

    private static DiscoveryWorks Works(BronzeFixture f) => f.Economy.DiscoveryTown(0).Works;

    [Fact]
    public void A_town_draws_crates_only_for_goods_it_holds()
    {
        BronzeFixture f = Town(Discovery(SteamKit));
        Assert.Equal(0, f.Economy.CargoMask(0));
        f.Economy.FixtureAddDiscoveryGood(0, "G-COAL", Fix64.FromInt(10));
        f.Economy.FixtureAddDiscoveryGood(0, "G-PLATEGLASS", Fix64.One);
        f.Economy.FixtureAddDiscoveryGood(0, "G-TELESCOPE", Fix64.One);
        Assert.Equal(CivSim.Sim.View.DiscoveryFrame.CargoCoal | CivSim.Sim.View.DiscoveryFrame.CargoGlass | CivSim.Sim.View.DiscoveryFrame.CargoInstruments, f.Economy.CargoMask(0));
        f.Economy.FixtureAddDiscoveryGood(0, "G-COAL", Fix64.FromInt(-10));
        Assert.Equal(CivSim.Sim.View.DiscoveryFrame.CargoGlass | CivSim.Sim.View.DiscoveryFrame.CargoInstruments, f.Economy.CargoMask(0));
        Assert.True(f.Economy.CargoMask(999) == 0, "no such town");
    }

    [Fact]
    public void A_town_with_no_works_reports_none_of_them()
    {
        DiscoveryWorks w = Works(Town(Discovery(SteamKit)));
        Assert.All(new[] { w.Engine, w.Oven, w.Factory, w.Garden, w.Society, w.Exchange, w.Galleon }, x => Assert.Equal(DiscoveryWorkState.None, x.State));
        Assert.False(w.Flooded);
        Assert.False(w.Pumping);
    }

    [Fact]
    public void An_engine_is_dormant_without_engineers_blocked_without_coal_and_working_with_both()
    {
        BronzeFixture f = Town(Discovery(SteamKit));
        f.Economy.FixtureSetIronWork(0, Engine, 1);
        Assert.Equal(new DiscoveryWork(DiscoveryWorkState.Dormant, DiscoveryWorkReason.NoEngineer), Works(f).Engine);

        f.Economy.FixtureStaffAndBurnEngines(0, engineers: 2); // a crew, but nothing to burn
        Assert.Equal(new DiscoveryWork(DiscoveryWorkState.Blocked, DiscoveryWorkReason.NoCoal), Works(f).Engine);

        f.Economy.FixtureAddDiscoveryGood(0, "G-COAL", Fix64.FromInt(50));
        f.Economy.FixtureStaffAndBurnEngines(0, engineers: 2);
        Assert.Equal(new DiscoveryWork(DiscoveryWorkState.Working, DiscoveryWorkReason.None), Works(f).Engine);
    }

    [Fact]
    public void A_deep_seam_reads_flooded_until_a_fired_engine_pumps_it()
    {
        BronzeFixture f = Town(DeepCoal(SteamKit));
        f.Economy.FixtureSetIronWork(0, Engine, 1);
        f.RunSeasons(8);
        Assert.True(f.Economy.FixtureCoalCampTile(0) >= 0);

        // the engine is taken away, so the seam floods
        f.Economy.FixtureSetIronWork(0, Engine, 0);
        f.Economy.FixtureStaffAndBurnEngines(0, engineers: 0);
        DiscoveryWorks flooded = Works(f);
        Assert.True(flooded.Flooded);
        Assert.Equal(new DiscoveryWork(DiscoveryWorkState.Blocked, DiscoveryWorkReason.Flooded), flooded.Mine);
        Assert.False(flooded.Pumping);

        // a fired engine drains it: pumping, and the mine is no longer blocked by water
        f.Economy.FixtureSetIronWork(0, Engine, 1);
        f.Economy.FixtureAddDiscoveryGood(0, "G-COAL", Fix64.FromInt(50));
        f.Economy.FixtureStaffAndBurnEngines(0, engineers: 2);
        DiscoveryWorks pumped = Works(f);
        Assert.True(pumped.Pumping);
        Assert.False(pumped.Flooded);
        Assert.NotEqual(DiscoveryWorkReason.Flooded, pumped.Mine.Reason);
    }

    [Fact]
    public void The_deep_pit_list_names_the_tile_and_whether_it_is_flooded_or_pumped()
    {
        BronzeFixture f = Town(DeepCoal(SteamKit));
        f.Economy.FixtureSetIronWork(0, Engine, 1);
        f.RunSeasons(8);
        f.Economy.FixtureSetIronWork(0, Engine, 0);
        f.Economy.FixtureStaffAndBurnEngines(0, engineers: 0);
        var tiles = new int[8]; var states = new byte[8];
        int n = f.Economy.DeepCoalPits(f.Economy.SettlementLite(0).Civilization, tiles, states);
        Assert.Equal(1, n);
        Assert.Equal(f.Economy.FixtureCoalCampTile(0), tiles[0]);
        Assert.Equal(1, states[0]);                                    // flooded

        f.Economy.FixtureSetIronWork(0, Engine, 1);
        f.Economy.FixtureAddDiscoveryGood(0, "G-COAL", Fix64.FromInt(50));
        f.Economy.FixtureStaffAndBurnEngines(0, engineers: 2);
        Assert.Equal(1, f.Economy.DeepCoalPits(f.Economy.SettlementLite(0).Civilization, tiles, states));
        Assert.Equal(2, states[0]);                                    // pumped
        Assert.Equal(0, f.Economy.DeepCoalPits(f.Economy.SettlementLite(0).Civilization + 1, tiles, states));   // another civilization's pits are not listed
    }

    [Fact]
    public void A_shallow_seam_never_reads_flooded()
    {
        BronzeFixture f = Town(Discovery(SteamKit));
        f.Economy.FixtureSetIronWork(0, Engine, 1);
        f.RunSeasons(8);
        DiscoveryWorks w = Works(f);
        Assert.False(w.Flooded);
        Assert.False(w.Pumping);
    }

    [Fact]
    public void A_garden_a_society_and_an_exchange_are_dormant_until_somebody_staffs_them()
    {
        BronzeFixture f = Town(Discovery(SteamKit));
        f.Economy.FixtureSetIronWork(0, "botanical_garden", 1);
        f.Economy.FixtureSetIronWork(0, "learned_society", 1);
        f.Economy.FixtureSetIronWork(0, "exchange_house", 1);
        DiscoveryWorks w = Works(f);
        Assert.Equal(new DiscoveryWork(DiscoveryWorkState.Dormant, DiscoveryWorkReason.NoBotanist), w.Garden);
        Assert.Equal(new DiscoveryWork(DiscoveryWorkState.Dormant, DiscoveryWorkReason.NoScholar), w.Society);
        Assert.Equal(new DiscoveryWork(DiscoveryWorkState.Blocked, DiscoveryWorkReason.NoHarbor), w.Exchange);   // an exchange house needs a harbor first

        f.Economy.FixtureSetIronWork(0, "harbor", 1);
        Assert.Equal(new DiscoveryWork(DiscoveryWorkState.Dormant, DiscoveryWorkReason.NoBroker), Works(f).Exchange);
    }

    [Fact]
    public void A_galleon_is_blocked_without_a_harbor_and_without_a_crew()
    {
        BronzeFixture f = Town(Discovery(SteamKit));
        f.Economy.FixtureSetIronWork(0, "galleon", 1);
        Assert.Equal(new DiscoveryWork(DiscoveryWorkState.Blocked, DiscoveryWorkReason.NoHarbor), Works(f).Galleon);
        f.Economy.FixtureSetIronWork(0, "harbor", 1);
        Assert.Equal(new DiscoveryWork(DiscoveryWorkState.Blocked, DiscoveryWorkReason.NoCrew), Works(f).Galleon);
    }

    [Fact]
    public void A_factory_is_dormant_without_operatives_and_a_coke_oven_blocked_without_coal()
    {
        BronzeFixture f = Town(Discovery(SteamKit));
        f.Economy.FixtureSetIronWork(0, "factory", 1);
        f.Economy.FixtureSetIronWork(0, "coke_oven", 1);
        DiscoveryWorks w = Works(f);
        Assert.Equal(DiscoveryWorkState.Dormant, w.Factory.State);
        Assert.Equal(DiscoveryWorkReason.NoOperatives, w.Factory.Reason);
        Assert.Equal(new DiscoveryWork(DiscoveryWorkState.Blocked, DiscoveryWorkReason.NoCoal), w.Oven);

        f.Economy.FixtureAddDiscoveryGood(0, "G-COAL", Fix64.FromInt(20));
        Assert.Equal(DiscoveryWorkState.Working, Works(f).Oven.State);
    }

    [Fact]
    public void The_packed_work_byte_round_trips_a_state_and_a_reason()
    {
        foreach (DiscoveryWorkState state in System.Enum.GetValues<DiscoveryWorkState>())
            foreach (DiscoveryWorkReason reason in System.Enum.GetValues<DiscoveryWorkReason>())
            {
                byte packed = CivSim.Sim.View.DiscoveryFrame.PackWork(new DiscoveryWork(state, reason));
                Assert.Equal(state, CivSim.Sim.View.DiscoveryFrame.WorkStateOf(packed));
                Assert.Equal(reason, CivSim.Sim.View.DiscoveryFrame.WorkReasonOf(packed));
            }
    }

    [Fact]
    public void Every_stopped_or_idle_work_has_a_reason_line_and_a_working_one_has_none()
    {
        foreach (DiscoveryWorkReason reason in System.Enum.GetValues<DiscoveryWorkReason>())
        {
            if (reason == DiscoveryWorkReason.None) continue;
            Assert.True(CivSim.Sim.View.DiscoveryWorkText.Reason(reason).Length > 0, $"{reason} has no words");
            foreach (DiscoveryWorkState state in new[] { DiscoveryWorkState.Dormant, DiscoveryWorkState.Blocked })
            {
                string line = CivSim.Sim.View.DiscoveryWorkText.Line(CivSim.Sim.View.DiscoveryFrame.WorkEngine, CivSim.Sim.View.DiscoveryFrame.PackWork(new DiscoveryWork(state, reason)));
                Assert.StartsWith("Atmospheric engine · " + (state == DiscoveryWorkState.Dormant ? "idle" : "stopped") + ": ", line);
            }
        }
        byte working = CivSim.Sim.View.DiscoveryFrame.PackWork(new DiscoveryWork(DiscoveryWorkState.Working, DiscoveryWorkReason.None));
        Assert.Equal("", CivSim.Sim.View.DiscoveryWorkText.Line(CivSim.Sim.View.DiscoveryFrame.WorkFactory, working));
        Assert.StartsWith("Coal pit · working", CivSim.Sim.View.DiscoveryWorkText.Line(CivSim.Sim.View.DiscoveryFrame.WorkMine, working, pumped: true));
    }

    [Fact]
    public void A_towns_reason_lines_follow_the_state_the_sim_reports()
    {
        BronzeFixture f = Town(Discovery(SteamKit));
        f.Economy.FixtureSetIronWork(0, Engine, 1);
        var d = new CivSim.Sim.View.DiscoveryFrame { Active = true, Work = new byte[CivSim.Sim.View.DiscoveryFrame.WorkKinds] };
        d.Work[CivSim.Sim.View.DiscoveryFrame.WorkEngine] = CivSim.Sim.View.DiscoveryFrame.PackWork(Works(f).Engine);
        Assert.Equal("Atmospheric engine · idle: no engineer to tend it", CivSim.Sim.View.DiscoveryWorkText.Lines(d, 0));
        Assert.Equal("", CivSim.Sim.View.DiscoveryWorkText.Lines(d, 1)); // no such row
    }
}
