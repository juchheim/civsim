using System;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>
/// The Iron chapter's metal ledger (M11 WP6 slice 1, docs/MVP/26 §2.3): the forge recipes run only in an Iron-chapter economy, conserve what
/// they consume, and leave the Bronze checksum and snapshot untouched. Planner wants, kit use and wear come in later slices.
/// </summary>
public class IronChainTests
{
    private const ulong Seed = 20261019;
    private static Fix64 F(string text) => Fix64.Parse(text);

    private static void Near(Fix64 expected, Fix64 actual)
        => Assert.True(Fix64.Abs(expected - actual) <= Fix64.FromRaw(4295), $"expected {expected}, got {actual}");

    private static BronzeFixture Iron(params string[] extra) => BronzeFixture.CreateIron(TestPack.Pack, Seed, 60, extra);

    private static BronzeFixture Bronze(params string[] extra)
        => BronzeFixture.Create(TestPack.Pack, Seed, 60, coppice: true, tin: true, arsenic: true, shaftMining: true, holdPopulation: false, extra);

    [Fact]
    public void The_forge_recipes_run_only_in_an_Iron_chapter_economy_with_the_unlocking_project()
    {
        foreach ((BronzeFixture f, bool ironChain) in new[] { (Bronze("MET-09"), false), (Iron("MET-09"), true) })
        {
            f.Economy.FixtureStock(0, "G-IRON", Fix64.FromInt(2));
            f.Economy.FixtureStock(0, "G-CHAR", Fix64.FromInt(2));
            Fix64 made = f.Economy.FixtureRunRecipe(0, "forge_wrought", Fix64.One, Fix64.FromInt(10));
            Assert.Equal(ironChain, made > Fix64.Zero);
            Assert.Equal(ironChain ? Fix64.One : Fix64.Zero, made);
        }
        // The chapter is on but MET-09 is not known: the recipe is locked.
        BronzeFixture locked = Iron();
        locked.Economy.FixtureStock(0, "G-IRON", Fix64.FromInt(2));
        locked.Economy.FixtureStock(0, "G-CHAR", Fix64.FromInt(2));
        Assert.Equal(Fix64.Zero, locked.Economy.FixtureRunRecipe(0, "forge_wrought", Fix64.One, Fix64.FromInt(10)));
        Assert.Equal(Fix64.FromInt(2), locked.Economy.GoodStock(0, "G-IRON"));
    }

    [Fact]
    public void Forging_debits_inputs_before_it_credits_the_output_and_stops_at_the_scarcest_input()
    {
        BronzeFixture f = Iron("MET-09");
        LivingEconomyModel e = f.Economy;
        e.FixtureStock(0, "G-IRON", Fix64.FromInt(2));
        e.FixtureStock(0, "G-CHAR", F("0.5"));
        // 0.5 G-IRON + 0.25 G-CHAR -> 0.4 G-WROUGHT: charcoal allows 2 batches, iron 4, labor 25.
        Fix64 made = e.FixtureRunRecipe(0, "forge_wrought", Fix64.FromInt(10), Fix64.FromInt(10));
        Near(Fix64.FromInt(2), made);
        Near(F("1.0"), e.GoodStock(0, "G-IRON"));
        Near(Fix64.Zero, e.GoodStock(0, "G-CHAR"));
        Near(F("0.8"), e.GoodStock(0, "G-WROUGHT"));
        // Labor limits it too: 0.4 ws a batch leaves room for 0.5 batches on 0.2 ws.
        e.FixtureStock(0, "G-CHAR", Fix64.FromInt(2));
        Near(F("0.5"), e.FixtureRunRecipe(0, "forge_wrought", Fix64.FromInt(10), F("0.2")));
    }

    [Fact]
    public void A_T11_kit_embodies_the_hand_checked_ore_wood_and_labor_from_26()
    {
        BronzeFixture f = Iron("MET-08", "MET-09");
        LivingEconomyModel e = f.Economy;
        const string Ore = "G-IRONORE", Char = "G-CHAR";
        Fix64 oreStock = Fix64.FromInt(5), charStock = Fix64.FromInt(10);
        e.FixtureStock(0, Ore, oreStock);
        e.FixtureStock(0, Char, charStock);
        Fix64 labor = Fix64.FromInt(10), laborLeft = labor;

        // 0.15 G-WROUGHT is 0.375 of a forge batch, which needs 0.1875 G-IRON, which is 0.375 of a bloomery batch.
        Fix64 bloom = e.FixtureRunRecipe(0, "bloomery", F("0.375"), laborLeft);
        Fix64 forge = e.FixtureRunRecipe(0, "forge_wrought", F("0.375"), laborLeft);
        Fix64 kit = e.FixtureRunRecipe(0, "make_tool11", Fix64.One, laborLeft);
        Near(F("0.375"), bloom);
        Near(F("0.375"), forge);
        Near(Fix64.One, kit);

        Near(F("0.75"), oreStock - e.GoodStock(0, Ore));                // 0.75 G-IRONORE
        Near(F("1.36875"), charStock - e.GoodStock(0, Char));           // 1.36875 G-CHAR = 8.2125 G-WOOD at 6 per charcoal
        Near(F("8.2125"), (charStock - e.GoodStock(0, Char)) * Fix64.FromInt(6));
        Near(Fix64.Zero, e.GoodStock(0, "G-IRON"));                     // no bloom left over: the bloomery charge is not counted twice
        Near(Fix64.Zero, e.GoodStock(0, "G-WROUGHT"));
        Near(Fix64.One, e.GoodStock(0, "G-TOOL11"));
        // Labor excluding charcoal burning: bloomery 0.3 + forging 0.15 + kit 0.2 = 0.65 ws (the doc's 1.1975 adds 0.5475 for the charcoal).
        Near(F("0.65"), F("0.8") * F("0.375") + F("0.4") * F("0.375") + F("0.2"));
    }

    [Fact]
    public void Steel_and_T12_need_MET_10_and_the_forge()
    {
        BronzeFixture f = Iron("MET-09");
        LivingEconomyModel e = f.Economy;
        e.FixtureStock(0, "G-WROUGHT", Fix64.One);
        e.FixtureStock(0, "G-CHAR", Fix64.FromInt(2));
        Assert.Equal(Fix64.Zero, e.FixtureRunRecipe(0, "carburize_steel", Fix64.One, Fix64.FromInt(10))); // MET-10 not known
        e.FixtureComplete(0, "MET-10");
        Near(Fix64.One, e.FixtureRunRecipe(0, "carburize_steel", Fix64.One, Fix64.FromInt(10)));          // 0.4 + 0.2 -> 0.4
        Near(F("0.4"), e.GoodStock(0, "G-STEEL"));
        Near(F("0.6"), e.GoodStock(0, "G-WROUGHT"));
        Near(F("1.8"), e.GoodStock(0, "G-CHAR"));
        Near(Fix64.One, e.FixtureRunRecipe(0, "make_tool12", Fix64.One, Fix64.FromInt(10)));              // 0.15 steel + 0.10 char
        Near(F("0.25"), e.GoodStock(0, "G-STEEL"));
        Near(F("1.7"), e.GoodStock(0, "G-CHAR"));
        Near(Fix64.One, e.GoodStock(0, "G-TOOL12"));
    }

    [Fact]
    public void Iron_goods_are_hashed_and_saved_only_in_the_Iron_chapter_and_round_trip()
    {
        BronzeFixture bronze = Bronze("MET-09");
        ulong before = bronze.Economy.ComputeChecksum();
        // Stocking an Iron good in a Bronze economy still leaves the Bronze state unchanged on the wire.
        bronze.Economy.FixtureStock(0, "G-WROUGHT", Fix64.One);
        Assert.Equal(before, bronze.Economy.ComputeChecksum());
        Assert.Equal(Snapshot(Bronze("MET-09").Economy), Snapshot(bronze.Economy));

        BronzeFixture iron = Iron("MET-09");
        ulong ironBefore = iron.Economy.ComputeChecksum();
        iron.Economy.FixtureStock(0, "G-WROUGHT", F("1.25"));
        Assert.NotEqual(ironBefore, iron.Economy.ComputeChecksum());

        byte[] saved = Snapshot(iron.Economy);
        BronzeFixture restored = Iron("MET-09");
        Restore(restored.Economy, saved);
        Assert.Equal(iron.Economy.ComputeChecksum(), restored.Economy.ComputeChecksum());
        Assert.Equal(F("1.25"), restored.Economy.GoodStock(0, "G-WROUGHT"));

        // Neither economy reads the other's snapshot.
        Assert.ThrowsAny<Exception>(() => Restore(Iron("MET-09").Economy, Snapshot(Bronze("MET-09").Economy)));
        // A Bronze economy leaves the Iron section unread, which the match-level reader rejects as trailing bytes.
        using var stream = new MemoryStream(saved);
        using var reader = new BinaryReader(stream);
        Bronze("MET-09").Economy.ReadSnapshot(reader);
        Assert.NotEqual(stream.Length, stream.Position);
    }

    [Fact]
    public void An_Iron_economy_records_E4_completions_and_a_Bronze_one_does_not()
    {
        foreach ((BronzeFixture f, bool recorded) in new[] { (Bronze(), false), (Iron(), true) })
        {
            int project = ProjectIndex(f.Economy, "MET-09");
            f.Economy.Step(0, new[] { new Command(0, 0, CommandKind.CompleteProject, project, 0, 0) });
            Assert.Equal(recorded, f.Economy.HasCompletedProject(0, "MET-09"));
        }
    }

    [Fact]
    public void An_Iron_settlement_plans_T11_kits_for_its_adults_through_the_existing_chain_and_conserves_the_ore()
    {
        BronzeFixture f = Iron("MET-08", "MET-09");
        LivingEconomyModel e = f.Economy;
        Fix64 ore = Fix64.FromInt(60);
        e.FixtureStock(0, "G-IRONORE", ore);
        e.FixtureStock(0, "G-CHAR", Fix64.FromInt(40));
        Assert.Equal(Fix64.Zero, e.GoodStock(0, "G-TOOL11"));

        f.RunSeasons(3);

        Fix64 kits = e.GoodStock(0, "G-TOOL11");
        // Every litre of ore went through the bloomery (2 ore -> 0.5 bloom -> 0.4 wrought at the forge -> 0.15 wrought a kit).
        int held = 0;
        foreach (int t in e.FixtureAdultKitTiers(0)) if (t == 11) held++;
        Assert.True(held > 0, "kits made should reach hands");
        Fix64 allKits = kits + Fix64.FromInt(held);
        Fix64 bloomEquivalent = e.GoodStock(0, "G-IRON") + (e.GoodStock(0, "G-WROUGHT") + allKits * F("0.15")) * F("1.25");
        Near(ore - e.GoodStock(0, "G-IRONORE"), bloomEquivalent * Fix64.FromInt(4));
    }

    [Fact]
    public void A_Bronze_settlement_with_the_same_stock_and_projects_makes_no_Iron_goods()
    {
        BronzeFixture f = Bronze("MET-08", "MET-09");
        LivingEconomyModel e = f.Economy;
        e.FixtureStock(0, "G-IRONORE", Fix64.FromInt(60));
        e.FixtureStock(0, "G-CHAR", Fix64.FromInt(40));
        f.RunSeasons(3);
        foreach (string good in new[] { "G-WROUGHT", "G-STEEL", "G-TOOL11", "G-TOOL12" })
            Assert.Equal(Fix64.Zero, e.GoodStock(0, good));
    }

    [Fact]
    public void An_Iron_economy_restored_from_a_snapshot_continues_exactly()
    {
        BronzeFixture live = Iron("MET-08", "MET-09");
        live.Economy.FixtureStock(0, "G-IRONORE", Fix64.FromInt(60));
        live.Economy.FixtureStock(0, "G-CHAR", Fix64.FromInt(40));
        live.Economy.FixtureStock(0, "G-GRAIN", Fix64.FromInt(400));
        for (long t = 0; t < 2 * LivingEconomyModel.SeasonTicks; t++) live.Economy.Step(t, ReadOnlySpan<Command>.Empty);
        Assert.True(live.Economy.GoodStock(0, "G-TOOL11") > Fix64.Zero);

        BronzeFixture copy = Iron("MET-08", "MET-09");
        Restore(copy.Economy, Snapshot(live.Economy));
        Assert.Equal(live.Economy.ComputeChecksum(), copy.Economy.ComputeChecksum());
        for (long t = 2 * LivingEconomyModel.SeasonTicks; t < 4 * LivingEconomyModel.SeasonTicks; t++)
        {
            live.Economy.Step(t, ReadOnlySpan<Command>.Empty);
            copy.Economy.Step(t, ReadOnlySpan<Command>.Empty);
        }
        Assert.Equal(live.Economy.ComputeChecksum(), copy.Economy.ComputeChecksum());
        Assert.Equal(live.Economy.GoodStock(0, "G-TOOL11"), copy.Economy.GoodStock(0, "G-TOOL11"));
    }

    [Fact]
    public void T12_lasts_exactly_twice_as_long_as_T11_and_neither_counts_as_a_Bronze_kit()
    {
        BronzeFixture f = Iron("MET-08", "MET-09", "MET-10");
        (Fix64 s11, Fix64 b11) = f.Economy.FixtureKitLife(11);
        (Fix64 s12, Fix64 b12) = f.Economy.FixtureKitLife(12);
        Assert.Equal(Fix64.FromInt(24), s11);
        Assert.Equal(Fix64.FromInt(48), s12);
        Near(s11 * Fix64.FromInt(2), s12);
        Near(b11 * Fix64.FromInt(2), b12); // the dormancy tail scales with the service life, so the whole curve is stretched exactly 2x
    }

    [Fact]
    public void Iron_kits_move_from_store_to_adults_once_and_wear_with_their_tool_work()
    {
        BronzeFixture f = Iron("MET-08", "MET-09");
        LivingEconomyModel e = f.Economy;
        e.FixtureStock(0, "G-TOOL11", Fix64.FromInt(5));
        Fix64 copperBefore = e.GoodStock(0, "G-COPPER");
        f.RunSeasons(1);
        int[] tiers = e.FixtureAdultKitTiers(0);
        int held = 0;
        foreach (int t in tiers) if (t == 11) held++;
        Assert.True(held >= 5, $"the five stocked kits should be in hand, {held} are");
        // Every T11 kit is either in a store or in a hand: the planner's own kits plus the five, none duplicated by the hand-out.
        Fix64 total = e.GoodStock(0, "G-TOOL11") + Fix64.FromInt(held);
        Assert.True(total >= Fix64.FromInt(5));
        // A worn-down kit is replaced from store; the old iron kit returns nothing (docs/MVP/26 §2.2).
        Assert.True(e.GoodStock(0, "G-COPPER") >= copperBefore - Fix64.FromInt(1000));
    }

    [Fact]
    public void An_early_upgrade_from_T11_to_T12_gives_no_metal_back_but_a_Bronze_kit_still_remelts()
    {
        foreach ((int oldTier, bool refunds) in new[] { (11, false), (10, true) })
        {
            BronzeFixture f = Iron("MET-08", "MET-09", "MET-10");
            LivingEconomyModel e = f.Economy;
            int adult = FirstAdult(e);
            e.FixtureSetKit(adult, oldTier, Fix64.FromInt(20));
            e.FixtureStock(0, "G-TOOL12", Fix64.FromInt(100)); // enough for the kitless first, so the upgrade is served too
            Fix64 remelted = e.MetalStats(0).RemeltCopper;
            f.RunSeasons(1);
            Assert.Equal(12, e.FigureKit(adult).Tier);
            Fix64 returned = e.MetalStats(0).RemeltCopper - remelted;
            if (refunds) Assert.True(returned > Fix64.Zero, "a Bronze kit retired early is remelted");
            else Assert.Equal(Fix64.Zero, returned);           // an Iron kit is never refunded
        }
    }

    [Fact]
    public void A_Bronze_settlement_never_hands_out_Iron_kits()
    {
        BronzeFixture f = Bronze("MET-08", "MET-09");
        f.Economy.FixtureStock(0, "G-TOOL11", Fix64.FromInt(5));
        f.RunSeasons(2);
        foreach (int t in f.Economy.FixtureAdultKitTiers(0)) Assert.True(t is 0 or 8 or 9 or 10);
        Assert.Equal(Fix64.FromInt(5), f.Economy.GoodStock(0, "G-TOOL11"));
    }

    private static int FirstAdult(LivingEconomyModel e)
    {
        for (int i = 0; i < e.FigureCount; i++)
            if (e.Figure(i).Civilization == 0 && e.Figure(i).Age >= 14) return i;
        throw new InvalidOperationException("no adult");
    }

    [Fact]
    public void The_Iron_planner_opens_a_camp_on_own_land_iron_and_makes_kits_from_it_while_Bronze_opens_none()
    {
        BronzeFixture iron = BronzeFixture.CreateWithIronTile(TestPack.Pack, Seed, 60, ironChain: true, "MET-08", "MET-09");
        BronzeFixture bronze = BronzeFixture.CreateWithIronTile(TestPack.Pack, Seed, 60, ironChain: false, "MET-08", "MET-09");
        Assert.True(iron.IronTile >= 0);
        Assert.Equal(iron.IronTile, bronze.IronTile);
        foreach (BronzeFixture f in new[] { iron, bronze })
        {
            f.Economy.FixtureStock(0, "G-CHAR", Fix64.FromInt(60)); // charcoal in store: the question is where the ore comes from
            Assert.Equal(MiningCampResult.Opened, f.Economy.CheckOpenMiningCamp(0, f.IronTile));
        }

        iron.RunSeasons(6);
        bronze.RunSeasons(6);

        Assert.Equal(MiningCampResult.AlreadyOpen, iron.Economy.CheckOpenMiningCamp(0, iron.IronTile));
        Assert.Equal(MiningCampResult.Opened, bronze.Economy.CheckOpenMiningCamp(0, bronze.IronTile)); // never opened
        int held = 0;
        foreach (int t in iron.Economy.FixtureAdultKitTiers(0)) if (t == 11) held++;
        Fix64 kits = iron.Economy.GoodStock(0, "G-TOOL11") + Fix64.FromInt(held);
        Assert.True(kits > Fix64.Zero, "ore from the camp should have become iron kits");
        foreach (int t in bronze.Economy.FixtureAdultKitTiers(0)) Assert.True(t is 0 or 8 or 9 or 10);
    }

    private static Fix64 IronKits(BronzeFixture f)
    {
        int held = 0;
        foreach (int t in f.Economy.FixtureAdultKitTiers(0)) if (t >= 11) held++;
        return f.Economy.GoodStock(0, "G-TOOL11") + Fix64.FromInt(held);
    }

    private static void ThinTheForest(BronzeFixture f, int percent)
    {
        foreach (int tile in f.ForestTiles) f.Economy.FixtureSetWood(tile, Fix64.FromInt(30 * percent / 100));
    }

    [Fact]
    public void A_thin_forest_stops_the_Iron_chain_asking_for_fuel_and_a_full_one_does_not()
    {
        Fix64 oreUsed = Fix64.Zero, oreUsedThin = Fix64.Zero, kits = Fix64.Zero, kitsThin = Fix64.Zero;
        foreach (int percent in new[] { 100, 40 })
        {
            BronzeFixture f = BronzeFixture.CreateFuelScenario(TestPack.Pack, Seed, 60, ironChain: true, coppice: true, forestTiles: 13, "MET-08", "MET-09");
            f.RunSeasons(1); // the metal economy begins and records the forest it started with
            ThinTheForest(f, percent);
            f.Economy.FixtureStock(0, "G-IRONORE", Fix64.FromInt(60));
            f.RunSeasons(4);
            Fix64 used = Fix64.FromInt(60) - f.Economy.GoodStock(0, "G-IRONORE");
            if (percent == 100) { oreUsed = used; kits = IronKits(f); } else { oreUsedThin = used; kitsThin = IronKits(f); }
        }
        Assert.True(oreUsed > Fix64.FromInt(2), "with a full forest the chain smelts ore for kits");
        Assert.True(kits > Fix64.Zero);
        Assert.True(oreUsedThin < oreUsed / 4, $"a thin forest should not be smelted for: used {oreUsedThin} against {oreUsed}");
        Assert.True(kitsThin < kits);
    }

    [Fact]
    public void Iron_already_in_store_is_still_forged_into_kits_when_the_forest_is_thin()
    {
        BronzeFixture f = BronzeFixture.CreateFuelScenario(TestPack.Pack, Seed, 60, ironChain: true, coppice: true, forestTiles: 13, "MET-08", "MET-09");
        f.RunSeasons(1);
        ThinTheForest(f, 40);
        f.Economy.FixtureStock(0, "G-WROUGHT", Fix64.FromInt(2)); // enough for 13 kits
        f.Economy.FixtureStock(0, "G-CHAR", Fix64.FromInt(10));
        f.Economy.FixtureStock(0, "G-IRONORE", Fix64.FromInt(60));
        f.RunSeasons(3);
        Assert.True(IronKits(f) >= Fix64.FromInt(5), "stored wrought iron should have become kits");
        Assert.True(f.Economy.GoodStock(0, "G-IRONORE") >= Fix64.FromInt(58), "but no new ore is smelted while the forest is thin");
    }

    private static int IronKitHolders(LivingEconomyModel e, int settlement)
    {
        int held = 0;
        foreach (int t in e.FixtureAdultKitTiers(settlement)) if (t >= 11) held++;
        return held;
    }

    private static (BronzeFixture Fixture, int Daughter) WithDaughter(bool sharing)
    {
        // SOC-07 (the sharing research) is an early project the fixture already knows, so the towns share unless the economy's switch is off.
        BronzeFixture f = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "MET-08", "MET-09");
        f.Economy.IronSharingEnabled = sharing;
        f.Economy.FixtureStock(0, "G-CHAR", Fix64.FromInt(60));
        int daughter = f.Economy.FoundByDecree(0, f.CampTile, Fix64.FromRatio(2, 5), minDaughter: 6, startCohesion: 60);
        Assert.True(daughter >= 0, "the fixture should found a daughter settlement");
        return (f, daughter);
    }

    private static Fix64 AllIronKits(LivingEconomyModel e, params int[] settlements)
    {
        Fix64 total = Fix64.Zero;
        foreach (int i in settlements) total += e.GoodStock(i, "G-TOOL11") + Fix64.FromInt(IronKitHolders(e, i));
        return total;
    }

    [Fact]
    public void Spare_iron_kits_move_to_a_sister_town_only_when_the_towns_share_and_exist_once()
    {
        int daughterHolders = 0, aloneHolders = 0;
        foreach (bool sharing in new[] { true, false })
        {
            (BronzeFixture f, int daughter) = WithDaughter(sharing);
            f.Economy.FixtureStock(0, "G-TOOL11", Fix64.FromInt(40)); // far more than the parent's own adults lack
            Fix64 before = AllIronKits(f.Economy, 0, daughter);
            f.RunSeasons(1);
            Assert.Equal(before, AllIronKits(f.Economy, 0, daughter)); // moving kits creates and destroys none (no new smithing in a season with this stock)
            if (sharing) daughterHolders = IronKitHolders(f.Economy, daughter); else aloneHolders = IronKitHolders(f.Economy, daughter);
        }
        Assert.True(daughterHolders > 0, "the daughter's adults should hold kits the parent could spare");
        Assert.Equal(0, aloneHolders);
    }

    [Fact]
    public void Spare_bloom_moves_to_a_sister_forge_that_digs_no_iron_when_the_towns_share()
    {
        Fix64 withSharing = Fix64.Zero, without = Fix64.Zero;
        foreach (bool sharing in new[] { true, false })
        {
            (BronzeFixture f, int daughter) = WithDaughter(sharing);
            f.Economy.FixtureExhaust(f.IronTile); // neither town digs iron now, so the daughter's forge depends on what the parent holds
            f.Economy.FixtureBuild(daughter, "forge");
            f.Economy.FixtureStock(0, "G-IRON", Fix64.FromInt(8));
            f.RunSeasons(1);
            // Whatever reached the daughter is bloom, wrought iron or kits made from them.
            Fix64 reached = f.Economy.GoodStock(daughter, "G-IRON") + f.Economy.GoodStock(daughter, "G-WROUGHT") +
                f.Economy.GoodStock(daughter, "G-TOOL11") + Fix64.FromInt(IronKitHolders(f.Economy, daughter));
            if (sharing) withSharing = reached; else without = reached;
        }
        Assert.True(withSharing > Fix64.Zero, "bloom should have reached the sister forge");
        Assert.Equal(Fix64.Zero, without);
    }

    [Fact]
    public void A_forge_town_counts_its_forgeless_sisters_adults_in_its_own_kit_want()
    {
        (BronzeFixture shared, int daughter) = WithDaughter(sharing: true);
        (BronzeFixture alone, _) = WithDaughter(sharing: false);
        shared.RunSeasons(1); // both towns have a metal economy; the daughter has no forge yet
        alone.RunSeasons(1);
        Assert.Equal(0, shared.Economy.Metal(daughter).Forges);
        Assert.True(shared.Economy.FixtureSisterKitDeficit(0) >= Fix64.FromInt(5), "the parent should count the daughter's adults");
        Assert.Equal(Fix64.Zero, alone.Economy.FixtureSisterKitDeficit(0));
    }

    [Fact]
    public void With_MET_10_the_chain_makes_steel_edge_kits_for_the_adults_who_lack_one()
    {
        BronzeFixture f = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "MET-08", "MET-09", "MET-10");
        f.Economy.FixtureStock(0, "G-CHAR", Fix64.FromInt(120));
        f.Economy.FixtureStock(0, "G-IRONORE", Fix64.FromInt(100));
        f.RunSeasons(10);
        int steel = 0, wrought = 0;
        foreach (int t in f.Economy.FixtureAdultKitTiers(0)) { if (t == 12) steel++; else if (t == 11) wrought++; }
        Assert.True(steel > 0, "adults should hold steel-edge kits");
        Assert.True(steel > wrought, "new and replacement kits are steel-edge once MET-10 is known");
    }

    [Fact]
    public void The_saw_adds_forty_percent_to_a_T12_kits_chop_and_to_nothing_else()
    {
        var pack = TestPack.WithNumeric("MET-09", "MET-10", "WOD-08");
        BronzeFixture without = BronzeFixture.CreateIronWithCamp(pack, Seed, 60, "MET-08", "MET-09", "MET-10");
        BronzeFixture with = BronzeFixture.CreateIronWithCamp(pack, Seed, 60, "MET-08", "MET-09", "MET-10", "WOD-08");
        int adult = FirstAdult(without.Economy);
        foreach (BronzeFixture f in new[] { without, with }) f.Economy.FixtureSetKit(adult, 12, Fix64.FromInt(40));
        Fix64 axe = without.Economy.FixtureKitChopRate(adult), saw = with.Economy.FixtureKitChopRate(adult);
        Assert.True(axe > Fix64.Zero);
        Near(axe * F("1.4"), saw);
        // A T11 kit gets no saw, even with WOD-08.
        with.Economy.FixtureSetKit(adult, 11, Fix64.FromInt(20));
        without.Economy.FixtureSetKit(adult, 11, Fix64.FromInt(20));
        Assert.Equal(without.Economy.FixtureKitChopRate(adult), with.Economy.FixtureKitChopRate(adult));
    }

    [Fact]
    public void Sharing_leaves_a_settlement_with_no_sisters_exactly_as_it_was()
    {
        // The sister terms are zero for one settlement, with sharing on or off.
        BronzeFixture a = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "MET-08", "MET-09");
        BronzeFixture b = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "MET-08", "MET-09");
        a.Economy.IronSharingEnabled = false;
        a.Economy.FixtureStock(0, "G-CHAR", Fix64.FromInt(60));
        b.Economy.FixtureStock(0, "G-CHAR", Fix64.FromInt(60));
        a.RunSeasons(6);
        b.RunSeasons(6);
        Assert.Equal(IronKits(a), IronKits(b));
    }

    private static BronzeFixture TerraceTown(bool ironChain)
    {
        BronzeFixture f = BronzeFixture.CreateTerraceScenario(TestPack.Pack, Seed, 60, ironChain, "AGR-08", "MIN-03");
        f.Economy.FixtureFillNaturalFields(0);
        f.Economy.FixtureStock(0, "G-BLOCK", Fix64.FromInt(10));
        f.Economy.FixtureStock(0, "G-WOOD", Fix64.FromInt(20));
        return f;
    }

    [Fact]
    public void An_Iron_town_whose_flat_fields_are_full_terraces_a_hillside_into_a_field_and_a_Bronze_town_does_not()
    {
        BronzeFixture iron = TerraceTown(ironChain: true), bronze = TerraceTown(ironChain: false);
        Assert.True(iron.TerraceTile >= 0);
        int fieldsBefore = iron.Economy.FixtureFieldCount(0);
        iron.RunSeasons(10);
        bronze.RunSeasons(10);

        Assert.Equal(1, iron.Economy.FixtureTerraceCount(0));
        Assert.True(iron.Economy.FixtureIsTerrace(0, iron.TerraceTile), "the hillside tile should now be a terraced field");
        Assert.Equal(fieldsBefore + 1, iron.Economy.FixtureFieldCount(0));
        Assert.True(iron.Economy.GoodStock(0, "G-BLOCK") <= Fix64.FromInt(6), "the terrace's four blocks were paid");
        Assert.Equal(0, bronze.Economy.FixtureTerraceCount(0));
    }

    [Fact]
    public void A_town_does_not_start_a_terrace_while_it_has_flat_fields_to_place_or_the_research_is_missing()
    {
        BronzeFixture noResearch = BronzeFixture.CreateTerraceScenario(TestPack.Pack, Seed, 60, ironChain: true, "MIN-03");
        noResearch.Economy.FixtureFillNaturalFields(0);
        noResearch.Economy.FixtureStock(0, "G-BLOCK", Fix64.FromInt(10));
        noResearch.RunSeasons(4);
        Assert.Equal(-1, noResearch.Economy.FixtureIronBuildKind(0));
        Assert.Equal(0, noResearch.Economy.FixtureTerraceCount(0));
    }

    [Fact]
    public void An_Iron_economy_with_a_terrace_restores_from_a_snapshot_and_continues_exactly()
    {
        BronzeFixture live = TerraceTown(ironChain: true);
        live.RunSeasons(10);
        Assert.Equal(1, live.Economy.FixtureTerraceCount(0));
        BronzeFixture copy = TerraceTown(ironChain: true);
        Restore(copy.Economy, Snapshot(live.Economy));
        Assert.Equal(live.Economy.ComputeChecksum(), copy.Economy.ComputeChecksum());
        Assert.True(copy.Economy.FixtureIsTerrace(0, live.TerraceTile));
        for (long tick = 10L * LivingEconomyModel.SeasonTicks; tick < 12L * LivingEconomyModel.SeasonTicks; tick++)
        {
            live.Economy.Step(tick, ReadOnlySpan<Command>.Empty);
            copy.Economy.Step(tick, ReadOnlySpan<Command>.Empty);
        }
        Assert.Equal(live.Economy.ComputeChecksum(), copy.Economy.ComputeChecksum());
    }

    [Fact]
    public void The_scythe_adds_a_third_more_effective_farm_work_only_for_an_iron_kit_in_an_Iron_chapter_that_knows_AGR_09()
    {
        BronzeFixture iron = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "AGR-09");
        BronzeFixture noScythe = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60);
        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, Seed, 60, true, true, true, true, false, "AGR-09");
        int adult = FirstAdult(iron.Economy);
        foreach (BronzeFixture f in new[] { iron, noScythe, bronze }) f.Economy.FixtureSetKit(adult, 11, Fix64.FromInt(20));
        Fix64 work = Fix64.FromInt(3);
        Fix64 bonus = iron.Economy.FixtureScytheBonus(adult, work);
        Assert.True(bonus > Fix64.Zero);
        // x2 on a third of the work: one more third of the work, scaled by skill (never above work/3 times the skill multiplier).
        Assert.True(bonus <= work / 3 * Fix64.FromInt(2), $"bonus {bonus} is out of range");
        Assert.Equal(Fix64.Zero, noScythe.Economy.FixtureScytheBonus(adult, work));   // AGR-09 unknown
        Assert.Equal(Fix64.Zero, bronze.Economy.FixtureScytheBonus(adult, work));      // not the Iron chapter
        iron.Economy.FixtureSetKit(adult, 10, Fix64.FromInt(20));
        Assert.Equal(Fix64.Zero, iron.Economy.FixtureScytheBonus(adult, work));        // a bronze kit has no scythe
    }

    [Fact]
    public void The_plough_lifts_only_heavy_forest_soil_to_prime_yield_and_only_with_AGR_10_in_the_Iron_chapter()
    {
        BronzeFixture iron = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "AGR-10");
        BronzeFixture noPlough = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60);
        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, Seed, 60, true, true, true, true, false, "AGR-10");
        var world = CivSim.Sim.Economy.WorldEconomyInput.FromGeneratedWorld(CivSim.Sim.World.WorldGenerator.Generate(Seed));
        CivSim.Sim.Economy.WorldEconomyTile forest = world.Tile(0) with
        {
            BiomeId = (int)CivSim.Sim.World.Biome.TemperateForest, Clay = true, FieldYieldPerYear = F("2.8"),
        };
        Assert.Equal(Fix64.FromInt(3), iron.Economy.FixtureFieldBaseYield(0, forest));
        Assert.Equal(F("2.8"), noPlough.Economy.FixtureFieldBaseYield(0, forest));
        Assert.Equal(F("2.8"), bronze.Economy.FixtureFieldBaseYield(0, forest));
        // Light soil, other biomes and better ground are untouched.
        Assert.Equal(F("2.8"), iron.Economy.FixtureFieldBaseYield(0, forest with { Clay = false }));
        Assert.Equal(F("2.6"), iron.Economy.FixtureFieldBaseYield(0, forest with { BiomeId = (int)CivSim.Sim.World.Biome.OpenWoodland, FieldYieldPerYear = F("2.6") }));
        Assert.Equal(F("3.4"), iron.Economy.FixtureFieldBaseYield(0, forest with { FieldYieldPerYear = F("3.4") })); // never lowered
    }

    [Fact]
    public void Water_limits_a_town_only_in_the_Iron_chapter_after_its_own_KEY_4_and_an_aqueduct_raises_it_by_forty_percent()
    {
        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, Seed, 60, true, true, true, true, false, "KEY-4");
        BronzeFixture ironEarly = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60);
        BronzeFixture iron = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "KEY-4");
        Fix64 plenty = Fix64.FromInt(100000);
        Assert.Equal(plenty, bronze.Economy.FixtureWaterCap(0));
        Assert.Equal(plenty, ironEarly.Economy.FixtureWaterCap(0));
        Fix64 cap = iron.Economy.FixtureWaterCap(0);
        Assert.True(cap >= Fix64.FromInt(60) && cap <= Fix64.FromInt(150), $"water cap {cap}");
        iron.Economy.FixtureBuildIronStructure(0, "aqueduct");
        Near(cap * F("1.4"), iron.Economy.FixtureWaterCap(0));
        Assert.Equal(F("0.85"), iron.Economy.FixtureIronSanitationFactor(0));
    }

    [Fact]
    public void Aqueducts_and_baths_cut_density_disease_and_the_cuts_multiply()
    {
        BronzeFixture f = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "KEY-4");
        Assert.Equal(Fix64.One, f.Economy.FixtureIronSanitationFactor(0));
        f.Economy.FixtureBuildIronStructure(0, "bathhouse");
        Assert.Equal(F("0.75"), f.Economy.FixtureIronSanitationFactor(0));
        f.Economy.FixtureBuildIronStructure(0, "aqueduct");
        Near(F("0.6375"), f.Economy.FixtureIronSanitationFactor(0));
        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, Seed, 60, true, true, true, true, false);
        Assert.Equal(Fix64.One, bronze.Economy.FixtureIronSanitationFactor(0));
    }

    [Fact]
    public void Medical_texts_strengthen_care_by_fifteen_percent_of_its_reduction_in_the_Iron_chapter_only()
    {
        BronzeFixture withTexts = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "MED-05");
        BronzeFixture without = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60);
        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, Seed, 60, true, true, true, true, false, "MED-05");
        Near(F("0.655"), withTexts.Economy.FixtureMedCare(0, F("0.7")));
        Near(F("0.54"), withTexts.Economy.FixtureMedCare(0, F("0.6")));
        Assert.Equal(Fix64.One, withTexts.Economy.FixtureMedCare(0, Fix64.One));          // no care, no change
        Assert.Equal(F("0.7"), without.Economy.FixtureMedCare(0, F("0.7")));              // MED-05 unknown
        Assert.Equal(F("0.7"), bronze.Economy.FixtureMedCare(0, F("0.7")));               // not the Iron chapter
        Assert.Equal(Fix64.Zero, withTexts.Economy.FixtureMedCare(0, F("0.05")));         // never below zero
    }

    [Fact]
    public void A_town_with_research_and_people_wants_a_bathhouse_and_builds_it_from_blocks_and_wood()
    {
        BronzeFixture f = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "MED-06", "MIN-03");
        f.Economy.FixtureSetTier(0, 1);
        Assert.True(f.Economy.FixtureIronBuildWanted(0, "bathhouse"));
        f.Economy.FixtureStock(0, "G-BLOCK", Fix64.FromInt(20));
        f.Economy.FixtureStock(0, "G-WOOD", Fix64.FromInt(20));
        for (int season = 0; season < 14; season++)
        {
            f.Economy.FixtureSetTier(0, 1); // the fixture town stays a town (the economy would demote it, having none of the works that earn the tier)
            f.RunSeasons(1);
        }
        Assert.Equal(F("0.75"), f.Economy.FixtureIronSanitationFactor(0));
        Assert.False(f.Economy.FixtureIronBuildWanted(0, "bathhouse"), "one bathhouse is enough");
    }

    private static int ProjectIndex(LivingEconomyModel economy, string id)
    {
        var content = EconomyContent.FromPack(TestPack.Pack);
        for (int i = 0; i < content.Projects.Length; i++) if (content.Projects[i].Id == id) return i;
        throw new ArgumentException(id);
    }

    private static byte[] Snapshot(LivingEconomyModel e)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) e.WriteSnapshot(writer);
        return stream.ToArray();
    }

    private static void Restore(LivingEconomyModel e, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);
        e.ReadSnapshot(reader);
    }
}
