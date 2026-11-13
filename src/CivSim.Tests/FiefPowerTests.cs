using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>
/// Faith &amp; Fief mechanical power and the blast furnace (M12 WP-E5-5, docs/MVP/27 §5.1 and §5.2): a windmill is a (weaker) mill, a mill town's sawing, bellows and
/// hammer work costs less labor, a blast furnace makes each bloomery batch four times the iron for twice the charcoal and labor, and the spinning wheel and treadle
/// loom cut textile labor. Every effect exists only in the Fief chapter.
/// </summary>
public class FiefPowerTests
{
    private const ulong Seed = 20261019;

    private static BronzeFixture Fief(params string[] projects) => BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, projects);
    private static BronzeFixture Iron(params string[] projects) => BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 100, projects);

    [Fact]
    public void A_blast_furnace_makes_a_bloomery_batch_four_times_the_iron_for_four_times_the_ore_twice_the_charcoal_and_twice_the_labor()
    {
        BronzeFixture f = Fief("MET-12", "NRG-03");
        var before = f.Economy.FixtureBatch(0, "bloomery");
        f.Economy.FixtureSetIronWork(0, "water_mill", 1);
        var withMill = f.Economy.FixtureBatch(0, "bloomery");
        f.Economy.FixtureSetIronWork(0, "blast_furnace", 1);
        var withFurnace = f.Economy.FixtureBatch(0, "bloomery");

        // The recipe's first input is the charcoal and its second the ore (the compiled order).
        Assert.Equal(withMill.Output * 4, withFurnace.Output);
        Assert.Equal(withMill.Input0 * 2, withFurnace.Input0);          // charcoal
        Assert.Equal(withMill.Input1 * 4, withFurnace.Input1);          // ore
        Assert.Equal(withMill.Labor * 2, withFurnace.Labor);
        // Half the charcoal and half the labor per litre of iron: the point of the furnace.
        Assert.Equal(withMill.Input0 / withMill.Output / 2, withFurnace.Input0 / withFurnace.Output);
        Assert.Equal(withMill.Labor / withMill.Output / 2, withFurnace.Labor / withFurnace.Output);
        Assert.Equal(before, withMill);                                  // the mill alone changes nothing about a bloomery batch (the bloomery is not a hammer or saw job)
    }

    [Fact]
    public void The_furnace_needs_MET12_a_mill_and_the_Fief_chapter()
    {
        BronzeFixture noResearch = Fief("NRG-03");
        noResearch.Economy.FixtureSetIronWork(0, "water_mill", 1);
        var baseline = noResearch.Economy.FixtureBatch(0, "bloomery");
        noResearch.Economy.FixtureSetIronWork(0, "blast_furnace", 1);
        Assert.Equal(baseline, noResearch.Economy.FixtureBatch(0, "bloomery"));          // no MET-12: nothing

        BronzeFixture noMill = Fief("MET-12");
        var plain = noMill.Economy.FixtureBatch(0, "bloomery");
        noMill.Economy.FixtureSetIronWork(0, "blast_furnace", 1);
        Assert.Equal(plain, noMill.Economy.FixtureBatch(0, "bloomery"));                 // no mill to drive the bellows

        BronzeFixture iron = Iron("MET-12", "NRG-03");
        iron.Economy.FixtureSetIronWork(0, "water_mill", 1);
        var ironBase = iron.Economy.FixtureBatch(0, "bloomery");
        iron.Economy.FixtureSetIronWork(0, "blast_furnace", 1);
        Assert.Equal(ironBase, iron.Economy.FixtureBatch(0, "bloomery"));                // an Iron chapter ignores the furnace
    }

    [Fact]
    public void A_windmill_is_a_mill_that_turns_four_seasons_in_five_and_only_in_a_Fief_match()
    {
        BronzeFixture water = Fief("NRG-05");
        water.Economy.FixtureSetIronWork(0, "water_mill", 1);
        BronzeFixture wind = Fief("NRG-05");
        wind.Economy.FixtureSetIronWork(0, "windmill", 1);
        BronzeFixture none = Fief("NRG-05");
        foreach (BronzeFixture t in new[] { water, wind, none }) t.Economy.FixtureSetTier(0, 1);   // grain value needs a civic town

        Fix64 plain = none.Economy.FixtureBatch(0, "forge_wrought").Labor;
        Fix64 waterLabor = water.Economy.FixtureBatch(0, "forge_wrought").Labor;
        Fix64 windLabor = wind.Economy.FixtureBatch(0, "forge_wrought").Labor;
        Assert.Equal(plain / 2, waterLabor);                                              // the Iron chapter's water-mill rule, unchanged
        Assert.True(windLabor < plain && windLabor > waterLabor, "a windmill helps, less than a water wheel");
        Assert.InRange((windLabor - plain * Fix64.FromRatio(5, 8)).Raw, -8L, 8L);        // grind and saw take priority; 3/4 of craft demand is powered
        Assert.True(wind.Economy.FixtureGrainValue(0) > none.Economy.FixtureGrainValue(0));
        Assert.True(wind.Economy.FixtureGrainValue(0) < water.Economy.FixtureGrainValue(0));
        var windLedger = wind.Economy.FixtureMillPowerLedger(0);
        Assert.InRange((windLedger.AvailablePowerHours - Fix64.Parse("3.2")).Raw, -4L, 4L); // 4 units at 80% availability
        Assert.Equal(Fix64.FromRatio(4, 5), windLedger.GrainCoverage);                    // existing 200-person mill cap

        BronzeFixture iron = Iron("NRG-05");
        Fix64 ironPlain = iron.Economy.FixtureBatch(0, "forge_wrought").Labor;
        iron.Economy.FixtureSetIronWork(0, "windmill", 1);
        Assert.Equal(ironPlain, iron.Economy.FixtureBatch(0, "forge_wrought").Labor);    // an Iron match ignores a windmill
    }

    [Fact]
    public void A_town_spends_one_shared_power_budget_in_grinding_sawing_craft_priority_and_reports_grain_coverage()
    {
        BronzeFixture powered = Fief("NRG-03", "WOD-09");
        powered.Economy.FixtureAddPeople(0, 200, 25);
        powered.Economy.FixtureSetTier(0, 1);
        powered.Economy.FixtureSetIronWork(0, "water_mill", 1);
        powered.Economy.FixtureSetIronWork(0, "sawmill", 1);

        var ledger = powered.Economy.FixtureMillPowerLedger(0, season: 7);
        Assert.Equal(1, ledger.WaterMillSources);
        Assert.Equal(0, ledger.WindmillSources);
        Assert.Equal(Fix64.FromInt(5), ledger.AvailablePowerHours);
        Assert.Equal(Fix64.FromInt(300) * Fix64.FromRatio(6, 100) / Fix64.FromInt(5), ledger.GrindingDemand);
        Assert.Equal(ledger.GrindingDemand, ledger.GrindingUsed);                       // food has first call on power
        Assert.Equal(Fix64.FromInt(300) * Fix64.FromRatio(2, 100) / Fix64.FromInt(4), ledger.SawingDemand);
        Assert.Equal(ledger.AvailablePowerHours - ledger.GrindingUsed, ledger.SawingUsed); // the remaining 1.4 hours
        Assert.Equal(Fix64.Zero, ledger.CraftUsed);                                     // no budget remains for hammer/bellows
        Assert.Equal(Fix64.Zero, ledger.UnusedPowerHours);
        Assert.Equal(ledger.AvailablePowerHours,
            ledger.GrindingUsed + ledger.SawingUsed + ledger.CraftUsed + ledger.UnusedPowerHours);
        Assert.Equal(Fix64.FromRatio(2, 3), ledger.GrainCoverage);                       // 200 of 300 residents have mill capacity
        Assert.Equal(Fix64.FromInt(200), ledger.GrainMilledPeople);

        BronzeFixture handPowered = Fief("NRG-03", "WOD-09");
        handPowered.Economy.FixtureAddPeople(0, 200, 25);
        handPowered.Economy.FixtureSetTier(0, 1);
        handPowered.Economy.FixtureSetIronWork(0, "sawmill", 1);
        Fix64 handLabor = handPowered.Economy.FixtureBatch(0, "saw_planks").Labor;
        Fix64 sharedLabor = powered.Economy.FixtureBatch(0, "saw_planks").Labor;
        Assert.InRange((sharedLabor - handLabor * Fix64.FromRatio(3, 10)).Raw, -16L, 16L); // only 14/15 of the saw job is powered

        var snapshots = powered.Economy.CaptureMillPowerSnapshots(7);
        Assert.Contains(snapshots, row => row.SettlementId == ledger.SettlementId && row.Season == 7 && row.GrainCoverage == ledger.GrainCoverage);
    }

    [Fact]
    public void A_sawmill_saws_planks_at_a_quarter_of_the_labor_and_the_textile_tools_cut_spinning_and_weaving()
    {
        BronzeFixture f = Fief("WOD-08", "WOD-09", "FIB-06", "FIB-07", "FIB-10", "FIB-11");
        Fix64 hand = f.Economy.FixtureBatch(0, "saw_planks").Labor;
        f.Economy.FixtureSetIronWork(0, "water_mill", 1);
        f.Economy.FixtureSetIronWork(0, "sawmill", 1);
        Assert.Equal(hand / 4, f.Economy.FixtureBatch(0, "saw_planks").Labor);

        BronzeFixture plain = Fief("FIB-06", "FIB-07");
        BronzeFixture tools = Fief("FIB-06", "FIB-07", "FIB-10", "FIB-11");
        Assert.Equal(plain.Economy.FixtureBatch(0, "spin_yarn").Labor / 3, tools.Economy.FixtureBatch(0, "spin_yarn").Labor);
        Assert.Equal(plain.Economy.FixtureBatch(0, "weave_cloth").Labor / 2, tools.Economy.FixtureBatch(0, "weave_cloth").Labor);

        BronzeFixture iron = Iron("FIB-06", "FIB-07", "FIB-10", "FIB-11", "WOD-08", "WOD-09");
        Fix64 ironSpin = iron.Economy.FixtureBatch(0, "spin_yarn").Labor;
        Assert.Equal(plain.Economy.FixtureBatch(0, "spin_yarn").Labor, ironSpin);        // no change outside Fief
    }

    [Fact]
    public void The_three_new_structures_are_wanted_only_in_a_Fief_town_that_meets_their_rules()
    {
        BronzeFixture f = Fief("NRG-05", "WOD-09", "MET-12", "NRG-03");
        f.Economy.FixtureSetTier(0, 1);
        // A blast furnace needs a mill and a forge and a smelter in the town.
        Assert.False(f.Economy.FixtureIronGateOpen(0, "blast_furnace"));
        f.Economy.FixtureSetIronWork(0, "water_mill", 1);
        f.Economy.FixtureSetMetalFacilities(0, 1, 1);
        Assert.True(f.Economy.FixtureIronGateOpen(0, "blast_furnace", includeAppetite: false) || f.Economy.FixtureIronGateOpen(0, "blast_furnace"));
        f.Economy.FixtureSetIronWork(0, "blast_furnace", 1);
        Assert.False(f.Economy.FixtureIronGateOpen(0, "blast_furnace"));                   // one is enough
        // A windmill is not wanted where a water mill already grinds.
        Assert.False(f.Economy.FixtureIronGateOpen(0, "windmill"));

        BronzeFixture iron = Iron("NRG-05", "WOD-09", "MET-12", "NRG-03");
        iron.Economy.FixtureSetTier(0, 1);
        iron.Economy.FixtureSetMetalFacilities(0, 1, 1);
        iron.Economy.FixtureSetIronWork(0, "water_mill", 1);
        Assert.False(iron.Economy.FixtureIronGateOpen(0, "blast_furnace"));                // never in an Iron match
        Assert.False(iron.Economy.FixtureIronGateOpen(0, "sawmill"));
        Assert.False(iron.Economy.FixtureIronGateOpen(0, "windmill"));
    }
}
