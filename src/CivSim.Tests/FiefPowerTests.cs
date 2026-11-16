using System.Linq;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>
/// Faith &amp; Fief mechanical power and the blast furnace (M12 WP-E5-5, docs/MVP/27 §5.1 and §5.2): a windmill is a (weaker) mill, a mill town's sawing, bellows and
/// hammer work costs less labor, a blast furnace makes each bloomery batch four times the iron from four times the ore, with fitted charcoal and twice the labor, and the spinning wheel and treadle
/// loom cut textile labor. Every effect exists only in the Fief chapter.
/// </summary>
public class FiefPowerTests
{
    private const ulong Seed = 20261019;

    private static BronzeFixture Fief(params string[] projects) => BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, projects);
    private static BronzeFixture Iron(params string[] projects) => BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 100, projects);

    [Fact]
    public void A_blast_furnace_makes_a_bloomery_batch_four_times_the_iron_for_four_times_the_ore_fitted_charcoal_and_twice_the_labor()
    {
        BronzeFixture f = Fief("MET-12", "NRG-03");
        var before = f.Economy.FixtureBatch(0, "bloomery");
        f.Economy.FixtureSetIronWork(0, "water_mill", 1);
        var withMill = f.Economy.FixtureBatch(0, "bloomery");
        f.Economy.FixtureSetIronWork(0, "blast_furnace", 1);
        var withFurnace = f.Economy.FixtureBatch(0, "bloomery");

        // The recipe's first input is the charcoal and its second the ore (the compiled order).
        Assert.Equal(withMill.Output * 4, withFurnace.Output);
        Assert.Equal(withMill.Input0, withFurnace.Input0);              // fitted x1 charcoal
        Assert.Equal(withMill.Input1 * 4, withFurnace.Input1);          // ore
        Assert.Equal(withMill.Labor, withFurnace.Labor);                // fitted x1 labor (F-T3)
        // One-quarter the charcoal and one-quarter the labor per litre of iron at this coverage.
        Assert.Equal(withMill.Input0 / withMill.Output / 4, withFurnace.Input0 / withFurnace.Output);
        Assert.Equal(withMill.Labor / withMill.Output / 4, withFurnace.Labor / withFurnace.Output);
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
        BronzeFixture water = Fief("NRG-05", "WOD-09");
        water.Economy.FixtureSetIronWork(0, "water_mill", 1);
        water.Economy.FixtureSetIronWork(0, "sawmill", 1);
        BronzeFixture wind = Fief("NRG-05", "WOD-09");
        wind.Economy.FixtureSetIronWork(0, "windmill", 1);
        wind.Economy.FixtureSetIronWork(0, "sawmill", 1);
        BronzeFixture none = Fief("NRG-05", "WOD-09");
        none.Economy.FixtureSetIronWork(0, "sawmill", 1);
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
        Assert.InRange((ledger.GrainCoverage - Fix64.FromRatio(13, 15)).Raw, -8L, 8L); // 260 of 300 residents have mill capacity
        Assert.Equal(Fix64.FromInt(260), ledger.GrainMilledPeople);

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
    public void A_town_reserves_sawing_hours_only_for_a_built_WOD09_sawmill()
    {
        BronzeFixture withoutSawmill = Fief("NRG-03", "WOD-09");
        withoutSawmill.Economy.FixtureAddPeople(0, 200, 25);
        withoutSawmill.Economy.FixtureSetTier(0, 1);
        withoutSawmill.Economy.FixtureSetIronWork(0, "water_mill", 1);
        var noSawmillLedger = withoutSawmill.Economy.FixtureMillPowerLedger(0);

        Assert.Equal(Fix64.Zero, noSawmillLedger.SawingDemand);
        Assert.Equal(Fix64.Zero, noSawmillLedger.SawingUsed);
        Assert.Equal(noSawmillLedger.AvailablePowerHours - noSawmillLedger.GrindingUsed,
            noSawmillLedger.CraftUsed);

        BronzeFixture unresearchedSawmill = Fief("NRG-03");
        unresearchedSawmill.Economy.FixtureAddPeople(0, 200, 25);
        unresearchedSawmill.Economy.FixtureSetTier(0, 1);
        unresearchedSawmill.Economy.FixtureSetIronWork(0, "water_mill", 1);
        unresearchedSawmill.Economy.FixtureSetIronWork(0, "sawmill", 1);
        var unresearchedLedger = unresearchedSawmill.Economy.FixtureMillPowerLedger(0);
        Assert.Equal(Fix64.Zero, unresearchedLedger.SawingDemand);
        Assert.Equal(Fix64.Zero, unresearchedLedger.SawingUsed);

        BronzeFixture withSawmill = Fief("NRG-03", "WOD-09");
        withSawmill.Economy.FixtureAddPeople(0, 200, 25);
        withSawmill.Economy.FixtureSetTier(0, 1);
        withSawmill.Economy.FixtureSetIronWork(0, "water_mill", 1);
        withSawmill.Economy.FixtureSetIronWork(0, "sawmill", 1);
        var sawmillLedger = withSawmill.Economy.FixtureMillPowerLedger(0);

        Assert.Equal(Fix64.FromInt(300) * Fix64.FromRatio(2, 100) / Fix64.FromInt(4),
            sawmillLedger.SawingDemand);
        Assert.Equal(sawmillLedger.AvailablePowerHours - sawmillLedger.GrindingUsed,
            sawmillLedger.SawingUsed);
        Assert.Equal(Fix64.Zero, sawmillLedger.CraftUsed);
    }

    [Fact]
    public void A_virtual_same_type_mill_sensitivity_changes_the_ledger_without_mutating_the_town()
    {
        BronzeFixture f = Fief("NRG-05");
        f.Economy.FixtureAddPeople(0, 500, 25);
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureSetIronWork(0, "windmill", 1);

        var baseline = f.Economy.FixtureMillPowerLedger(0);
        var addedWindmill = f.Economy.FixtureMillPowerLedger(0, extraWindSources: 1);
        var actualAfter = f.Economy.FixtureMillPowerLedger(0);

        Assert.Equal(1, baseline.WindmillSources);
        Assert.Equal(2, addedWindmill.WindmillSources);
        Assert.True(addedWindmill.GrainCoverage > baseline.GrainCoverage);
        Assert.Equal(baseline, actualAfter); // a sensitivity readout, not a changed simulation state
        Assert.Equal(addedWindmill.AvailablePowerHours,
            addedWindmill.GrindingUsed + addedWindmill.SawingUsed + addedWindmill.CraftUsed + addedWindmill.UnusedPowerHours);
    }

    [Fact]
    public void The_Fief_planner_allows_a_second_windmill_until_grain_capacity_reaches_sixty_percent()
    {
        // The source type present in all four remaining F-T2 misses. One existing source leaves this town
        // below the draft 60% capacity screen, so a second windmill is wanted; it is not wanted after coverage clears.
        BronzeFixture f = Fief("NRG-05");
        f.Economy.FixtureAddPeople(0, 300, 25); // 400 residents total, so two windmills clear the 60% screen
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureFillNaturalFields(0);
        Assert.True(f.Economy.FixtureIronGateOpen(0, "windmill"));
        f.Economy.FixtureSetIronWork(0, "windmill", 1);
        var oneMill = f.Economy.FixtureMillPowerLedger(0);
        Assert.True(oneMill.GrainCoverage < Fix64.FromRatio(60, 100));
        Assert.True(f.Economy.FixtureIronGateOpen(0, "windmill", includeAppetite: false));
        Assert.True(f.Economy.FixtureIronGateOpen(0, "windmill"));

        f.Economy.FixtureSetIronWork(0, "windmill", 2);
        var twoMills = f.Economy.FixtureMillPowerLedger(0);
        Assert.Equal(2, twoMills.WindmillSources);
        Assert.True(twoMills.GrainCoverage >= Fix64.FromRatio(60, 100), $"population={twoMills.Population}, coverage={twoMills.GrainCoverage}");
        Assert.False(f.Economy.FixtureIronGateOpen(0, "windmill"));

        f.Economy.FixtureSetIronWork(0, "windmill", 3);
        Assert.False(f.Economy.FixtureIronGateOpen(0, "windmill", includeAppetite: false));
    }

    [Fact]
    public void A_town_below_the_grain_screen_puts_its_second_windmill_ahead_of_another_build_in_progress()
    {
        BronzeFixture f = Fief("NRG-05", "BEL-10");
        f.Economy.FixtureAddPeople(0, 300, 25); // 400 residents: one windmill leaves coverage under 60%
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureFillNaturalFields(0);
        f.Economy.FixtureSetIronWork(0, "windmill", 1);
        f.Economy.FixtureStartIronBuild(0, "monastery");
        Assert.True(f.Economy.FixtureIronGateOpen(0, "windmill"));
        var chosen = f.Economy.FixtureRunIronBuildWants(0);
        Assert.Equal(f.Economy.FixtureIronKindOf("windmill"), chosen.Kind);
        // As the current build, its wheel is planned like any structure's; the post mill needs no dressed blocks.
        Assert.True(chosen.WheelWant >= Fix64.One);
        Assert.Equal(Fix64.Zero, chosen.BlockWant);
    }

    [Fact]
    public void The_windmill_does_not_jump_the_queue_once_coverage_is_met()
    {
        BronzeFixture met = Fief("NRG-05", "BEL-10");
        met.Economy.FixtureSetTier(0, 1);
        met.Economy.FixtureFillNaturalFields(0);
        met.Economy.FixtureSetIronWork(0, "windmill", 2);
        met.Economy.FixtureStartIronBuild(0, "monastery");
        met.Economy.FixtureSetGoodStock(0, "G-WHEEL", Fix64.FromInt(2));
        met.Economy.FixtureSetGoodStock(0, "G-BLOCK", Fix64.FromInt(10));
        Assert.NotEqual(met.Economy.FixtureIronKindOf("windmill"), met.Economy.FixtureRunIronBuildWants(0).Kind);
    }

    [Fact]
    public void A_second_Fief_windmill_build_remains_feasible_with_one_already_built_and_stops_at_two()
    {
        BronzeFixture f = Fief("NRG-05");
        f.Economy.FixtureAddPeople(0, 300, 25);
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureFillNaturalFields(0);
        f.Economy.FixtureSetIronWork(0, "windmill", 1);
        f.Economy.FixtureStartIronBuild(0, "windmill");

        Assert.True(f.Economy.FixtureIronBuildWanted(0, "windmill"));
        Assert.True(f.Economy.FixtureIronBuildFeasible(0, "windmill"));

        f.Economy.FixtureSetIronWork(0, "windmill", 2);
        Assert.False(f.Economy.FixtureIronBuildFeasible(0, "windmill"));
    }

    [Fact]
    public void A_river_town_can_build_a_water_mill_alongside_a_windmill_and_nonriver_sites_are_rejected()
    {
        GeneratedWorld world = WorldGenerator.Generate(Seed);
        int RiverSite(bool nearRiver)
        {
            for (int tile = 0; tile < world.TileCount; tile++)
            {
                if (world.TileAt(tile).Biome is Biome.Ocean or Biome.Coast or Biome.Peak or Biome.Ice) continue;
                bool hasNearbyRiver = false;
                for (int river = 0; river < world.TileCount; river++)
                    if ((world.TileAt(river).Flags & TileFlags.River) != 0 && HexGrid.Distance(tile, river, world.Width) <= 2)
                    { hasNearbyRiver = true; break; }
                if (hasNearbyRiver == nearRiver) return tile;
            }
            return -1;
        }

        int riverSite = RiverSite(nearRiver: true);
        int inlandSite = RiverSite(nearRiver: false);
        Assert.True(riverSite >= 0);
        Assert.True(inlandSite >= 0);

        BronzeFixture river = Fief("NRG-03", "NRG-05");
        river.Economy.FixtureMoveSite(0, riverSite);
        river.Economy.FixtureAddPeople(0, 500, 25);
        river.Economy.FixtureSetTier(0, 1);
        river.Economy.FixtureFillNaturalFields(0);
        river.Economy.FixtureSetIronWork(0, "windmill", 1);
        Assert.True(river.Economy.FixtureIronGateOpen(0, "water_mill"));
        river.Economy.FixtureSetIronWork(0, "water_mill", 1); // complete the gate-approved alternate source
        var mixedSource = river.Economy.FixtureMillPowerLedger(0);
        Assert.Equal(1, mixedSource.WaterMillSources);
        Assert.Equal(1, mixedSource.WindmillSources);
        Assert.InRange((mixedSource.AvailablePowerHours - Fix64.Parse("8.2")).Raw, -4L, 4L);
        Assert.InRange((mixedSource.GrainCoverage - Fix64.Parse("0.78")).Raw, -8L, 8L);
        Assert.Equal(mixedSource.AvailablePowerHours,
            mixedSource.GrindingUsed + mixedSource.SawingUsed + mixedSource.CraftUsed + mixedSource.UnusedPowerHours);

        BronzeFixture inland = Fief("NRG-03", "NRG-05");
        inland.Economy.FixtureMoveSite(0, inlandSite);
        inland.Economy.FixtureAddPeople(0, 500, 25);
        inland.Economy.FixtureSetTier(0, 1);
        inland.Economy.FixtureFillNaturalFields(0);
        inland.Economy.FixtureSetIronWork(0, "windmill", 1);
        Assert.False(inland.Economy.FixtureIronGateOpen(0, "water_mill"));
    }

    [Fact]
    public void A_windmill_can_complement_a_water_mill_only_below_the_coverage_target_and_stops_at_two_windmills()
    {
        GeneratedWorld world = WorldGenerator.Generate(Seed);
        int riverSite = Enumerable.Range(0, world.TileCount).First(tile =>
            (world.TileAt(tile).Flags & TileFlags.River) != 0 &&
            world.TileAt(tile).Biome is not (Biome.Ocean or Biome.Coast or Biome.Peak or Biome.Ice));

        BronzeFixture f = Fief("NRG-03", "NRG-05");
        f.Economy.FixtureMoveSite(0, riverSite);
        f.Economy.FixtureAddPeople(0, 700, 25); // 800 residents; water plus one windmill stays below 60%.
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureFillNaturalFields(0);
        Assert.True(f.Economy.FixtureIronGateOpen(0, "water_mill"));
        f.Economy.FixtureSetIronWork(0, "water_mill", 1);
        Assert.True(f.Economy.FixtureMillPowerLedger(0).GrainCoverage < Fix64.FromRatio(60, 100));
        Assert.True(f.Economy.FixtureIronGateOpen(0, "windmill"));

        f.Economy.FixtureSetIronWork(0, "windmill", 1);
        Assert.True(f.Economy.FixtureIronGateOpen(0, "windmill"));
        f.Economy.FixtureSetIronWork(0, "windmill", 2);
        Assert.True(f.Economy.FixtureMillPowerLedger(0).GrainCoverage >= Fix64.FromRatio(60, 100));
        Assert.False(f.Economy.FixtureIronGateOpen(0, "windmill"));
        f.Economy.FixtureSetIronWork(0, "windmill", 3);
        Assert.False(f.Economy.FixtureIronGateOpen(0, "windmill", includeAppetite: false));
    }

    [Fact]
    public void Two_windmills_with_the_260_person_capacity_fit_clear_the_sixty_percent_target()
    {
        BronzeFixture f = Fief("NRG-05");
        f.Economy.FixtureAddPeople(0, 508, 25);
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureFillNaturalFields(0);
        f.Economy.FixtureSetIronWork(0, "windmill", 2);

        var ledger = f.Economy.FixtureMillPowerLedger(0);
        Assert.Equal(2, ledger.WindmillSources);
        Assert.True(ledger.GrainCoverage >= Fix64.FromRatio(60, 100), $"population={ledger.Population}, coverage={ledger.GrainCoverage}");
        Assert.Equal(ledger.AvailablePowerHours,
            ledger.GrindingUsed + ledger.SawingUsed + ledger.CraftUsed + ledger.UnusedPowerHours);
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
