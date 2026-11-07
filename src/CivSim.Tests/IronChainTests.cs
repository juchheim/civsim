using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.World;

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

        // 0.08 G-WROUGHT is 0.2 of a forge batch, which needs 0.1 G-IRON, which is 0.2 of a bloomery batch.
        Fix64 bloom = e.FixtureRunRecipe(0, "bloomery", F("0.2"), laborLeft);
        Fix64 forge = e.FixtureRunRecipe(0, "forge_wrought", F("0.2"), laborLeft);
        Fix64 kit = e.FixtureRunRecipe(0, "make_tool11", Fix64.One, laborLeft);
        Near(F("0.2"), bloom);
        Near(F("0.2"), forge);
        Near(Fix64.One, kit);

        Near(F("0.4"), oreStock - e.GoodStock(0, Ore));                  // 0.4 G-IRONORE
        Near(F("0.73"), charStock - e.GoodStock(0, Char));              // 0.73 G-CHAR = 4.38 G-WOOD at 6 per charcoal
        Near(F("4.38"), (charStock - e.GoodStock(0, Char)) * Fix64.FromInt(6));
        Near(Fix64.Zero, e.GoodStock(0, "G-IRON"));                     // no bloom left over: the bloomery charge is not counted twice
        Near(Fix64.Zero, e.GoodStock(0, "G-WROUGHT"));
        Near(Fix64.One, e.GoodStock(0, "G-TOOL11"));
        // Labor excluding charcoal burning: bloomery 0.16 + forging 0.08 + kit 0.2 = 0.44 ws (the doc's 0.732 adds 0.292 for the charcoal).
        Near(F("0.44"), F("0.8") * F("0.2") + F("0.4") * F("0.2") + F("0.2"));
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
        // Every litre of ore went through the bloomery (2 ore -> 0.5 bloom -> 0.4 wrought at the forge -> 0.08 wrought a kit).
        int held = 0;
        foreach (int t in e.FixtureAdultKitTiers(0)) if (t == 11) held++;
        Assert.True(held > 0, "kits made should reach hands");
        Fix64 allKits = kits + Fix64.FromInt(held);
        Fix64 bloomEquivalent = e.GoodStock(0, "G-IRON") + (e.GoodStock(0, "G-WROUGHT") + allKits * F("0.08")) * F("1.25");
        Near(ore - e.GoodStock(0, "G-IRONORE"), bloomEquivalent * Fix64.FromInt(4));
    }

    [Fact]
    public void Iron_mine_exhaustion_stops_new_extraction_and_forging_after_stores_are_cleared()
    {
        BronzeFixture f = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "MET-08", "MET-09");
        LivingEconomyModel e = f.Economy;
        e.IT6AccountingTraceEnabled = true;
        e.FixtureSetOreRemaining(f.IronTile, Fix64.FromInt(20));
        e.FixtureAddMiningCamp(0, f.IronTile, crew: 4, built: true, mineHead: true);
        e.FixtureStock(0, "G-CHAR", Fix64.FromInt(100));
        f.RunSeasons(1);

        IT6AccountingSnapshot mined = e.CaptureIT6Accounting(0);
        Assert.True(mined.IronOreMined > Fix64.Zero, "the controlled Iron deposit should supply the working camp");
        Assert.Equal(Fix64.FromInt(20) - mined.IronOreMined, e.FixtureOreRemaining(f.IronTile));
        e.FixtureExhaust(f.IronTile);
        foreach (string good in new[] { "G-IRONORE", "G-IRON", "G-WROUGHT", "G-STEEL", "G-TOOL11", "G-TOOL12", "G-CHAR" })
            e.FixtureStock(0, good, -e.GoodStock(0, good));
        f.RunSeasons(2);

        IT6AccountingSnapshot exhausted = e.CaptureIT6Accounting(0);
        Assert.Equal(mined.IronOreMined, exhausted.IronOreMined);
        Assert.Equal(mined.Kit11Produced, exhausted.Kit11Produced);
        Assert.Equal(mined.Kit12Produced, exhausted.Kit12Produced);
        Assert.Equal(Fix64.Zero, e.GoodStock(0, "G-IRONORE"));
        Assert.Equal(Fix64.Zero, e.GoodStock(0, "G-IRON"));
        Assert.Equal(Fix64.Zero, e.GoodStock(0, "G-WROUGHT"));
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
    public void A_captured_forge_keeps_its_inventory_and_wear_cohorts_and_can_resume_production()
    {
        BronzeFixture f = Iron("MET-08", "MET-09");
        LivingEconomyModel e = f.Economy;
        e.IT6AccountingTraceEnabled = true;
        e.FixtureComplete(1, "MET-08", "MET-09");
        e.FixtureStock(0, "G-WROUGHT", Fix64.FromInt(2));
        e.FixtureStock(0, "G-CHAR", Fix64.FromInt(4));
        e.FixtureStock(0, "G-TOOL11", Fix64.FromInt(3));
        e.FixtureStock(0, "G-TOOL12", Fix64.FromInt(2));
        int t11 = FirstAdult(e), t12 = -1;
        for (int i = t11 + 1; i < e.FigureCount; i++) if (e.Figure(i).Age >= 14) { t12 = i; break; }
        Assert.True(t12 >= 0);
        e.FixtureSetKit(t11, 11, Fix64.FromInt(17));
        e.FixtureSetKit(t12, 12, Fix64.FromInt(29));
        Fix64 before = e.GoodStock(0, "G-IRONORE") + e.GoodStock(0, "G-IRON") + e.GoodStock(0, "G-WROUGHT") +
            e.GoodStock(0, "G-STEEL") + e.GoodStock(0, "G-TOOL11") + e.GoodStock(0, "G-TOOL12") + Fix64.FromInt(2);
        int forges = e.FixtureMetalBuildState(0).Forges;

        e.TransferSettlement(0, 1);

        Fix64 after = e.GoodStock(0, "G-IRONORE") + e.GoodStock(0, "G-IRON") + e.GoodStock(0, "G-WROUGHT") +
            e.GoodStock(0, "G-STEEL") + e.GoodStock(0, "G-TOOL11") + e.GoodStock(0, "G-TOOL12") + Fix64.FromInt(2);
        Assert.Equal(before, after);
        Assert.Equal(forges, e.FixtureMetalBuildState(0).Forges);
        Assert.Equal(0, e.IronKitHolders(0).T11 + e.IronKitHolders(0).T12);
        Assert.Equal(2, e.IronKitHolders(1).T11 + e.IronKitHolders(1).T12);
        Assert.Equal(11, e.FigureKit(t11).Tier);
        Assert.Equal(12, e.FigureKit(t12).Tier);
        Assert.Equal(Fix64.One, e.FixtureRunRecipe(0, "make_tool11", Fix64.One, Fix64.FromInt(10)));
        Assert.Equal(Fix64.One, e.CaptureIT6Accounting(1).Kit11Produced);
    }

    [Fact]
    public void T12_lasts_exactly_twice_as_long_as_T11_and_neither_counts_as_a_Bronze_kit()
    {
        BronzeFixture f = Iron("MET-08", "MET-09", "MET-10");
        (Fix64 s11, Fix64 b11) = f.Economy.FixtureKitLife(11);
        (Fix64 s12, Fix64 b12) = f.Economy.FixtureKitLife(12);
        Assert.Equal(Fix64.FromInt(30), s11);
        Assert.Equal(Fix64.FromInt(60), s12);
        Near(s11 * Fix64.FromInt(2), s12);
        Near(b11 * Fix64.FromInt(2), b12); // the dormancy tail scales with the service life, so the whole curve is stretched exactly 2x
    }

    [Fact]
    public void Mixed_T11_and_T12_wear_breaks_at_their_independent_exact_lifetimes()
    {
        BronzeFixture f = Iron("MET-08", "MET-09", "MET-10");
        LivingEconomyModel e = f.Economy;
        e.IT6AccountingTraceEnabled = true;
        int t11 = FirstAdult(e), t12 = -1;
        for (int i = t11 + 1; i < e.FigureCount; i++) if (e.Figure(i).Age >= 14) { t12 = i; break; }
        Assert.True(t12 >= 0);
        (Fix64 s11, Fix64 b11) = e.FixtureKitLife(11);
        (Fix64 s12, Fix64 b12) = e.FixtureKitLife(12);
        e.FixtureSetKit(t11, 11, s11);
        e.FixtureSetKit(t12, 12, s12);
        Fix64 firstInterval = s11 - b11;
        e.FixtureWearKit(t11, firstInterval);
        e.FixtureWearKit(t12, firstInterval);
        Assert.Equal(0, e.FigureKit(t11).Tier);
        Assert.Equal(12, e.FigureKit(t12).Tier);
        e.FixtureWearKit(t12, (s12 - b12) - firstInterval);

        IT6AccountingSnapshot trace = e.CaptureIT6Accounting(0);
        Assert.Equal(0, e.FigureKit(t12).Tier);
        Assert.Equal(1, trace.Kit11Breaks);
        Assert.Equal(1, trace.Kit12Breaks);
        Assert.Equal(firstInterval, trace.Kit11ToolWork);
        Assert.Equal(firstInterval * Fix64.FromInt(2), trace.Kit12ToolWork);
    }

    [Fact]
    public void I_T3_live_wear_trace_bins_remaining_service_by_kit_tier_without_changing_state()
    {
        BronzeFixture f = Iron("MET-09", "MET-10");
        LivingEconomyModel e = f.Economy;
        int first = FirstAdult(e);
        int second = first + 1;
        int third = second + 1;
        e.FixtureSetKit(first, 11, F("-1"));
        e.FixtureSetKit(second, 11, Fix64.FromInt(6)); // 20% remaining is in the [20,40) bin.
        e.FixtureSetKit(third, 12, Fix64.FromInt(48)); // 80% remaining is in the >=80% bin.
        ulong before = e.ComputeChecksum();

        IT3WearDistribution wear = e.CaptureIT3WearDistribution(0);

        Assert.Equal(2, wear.T11Total);
        Assert.Equal(1, wear.T11BelowZero);
        Assert.Equal(1, wear.T11Under40);
        Assert.Equal(1, wear.T12Total);
        Assert.Equal(1, wear.T12AtLeast80);
        Assert.Equal(before, e.ComputeChecksum());
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
    public void A_Bronze_replacement_retires_an_Iron_kit_still_in_its_dormancy_tail()
    {
        BronzeFixture f = Iron("MET-09");
        LivingEconomyModel e = f.Economy;
        e.IT6AccountingTraceEnabled = true;
        int adult = FirstAdult(e);
        (_, Fix64 breakLeft) = e.FixtureKitLife(11);
        Assert.True(breakLeft < Fix64.Zero, "T11 has a negative-remaining-service dormancy tail");
        for (int i = 0; i < e.FigureCount; i++)
            if (i != adult && e.Figure(i).Age >= 14) e.FixtureSetKit(i, 10, Fix64.FromInt(20));
        e.FixtureStock(0, "G-TOOL11", -e.GoodStock(0, "G-TOOL11"));
        e.FixtureSetKit(adult, 11, Fix64.Zero);
        e.FixtureStock(0, "G-TOOL10", Fix64.One);
        Assert.Equal(Fix64.Zero, e.GoodStock(0, "G-TOOL11"));
        Assert.Equal(Fix64.One, e.GoodStock(0, "G-TOOL10"));
        e.FixtureWearAndIssueKits(0, 10);

        Assert.Equal(10, e.FigureKit(adult).Tier);
        IT6AccountingSnapshot trace = e.CaptureIT6Accounting(0);
        Assert.Equal(Fix64.One, trace.Kit11Retired);
        Assert.Equal(0, trace.Kit11Breaks);
    }

    [Fact]
    public void Iron_kits_go_to_current_tool_workers_before_other_adults()
    {
        BronzeFixture f = Iron("MET-09");
        LivingEconomyModel e = f.Economy;
        int firstAdult = -1, lastAdult = -1;
        for (int i = 0; i < e.FigureCount; i++)
        {
            if (e.Figure(i).Age < 14) continue;
            if (firstAdult < 0) firstAdult = i;
            lastAdult = i;
        }
        Assert.NotEqual(firstAdult, lastAdult);
        e.FixtureStock(0, "G-TOOL11", Fix64.One);

        // The legacy ID ordering would issue the only kit to firstAdult, who is not working with tools.
        e.FixtureWearAndIssueKits(0, tier: 0, lastAdult);

        Assert.Equal(0, e.FigureKit(firstAdult).Tier);
        Assert.Equal(11, e.FigureKit(lastAdult).Tier);
    }

    [Fact]
    public void An_idle_Iron_kit_moves_to_a_current_tool_worker_and_keeps_its_wear()
    {
        BronzeFixture f = Iron("MET-09");
        LivingEconomyModel e = f.Economy;
        int firstAdult = -1, lastAdult = -1;
        for (int i = 0; i < e.FigureCount; i++)
        {
            if (e.Figure(i).Age < 14) continue;
            if (firstAdult < 0) firstAdult = i;
            lastAdult = i;
        }
        Assert.NotEqual(firstAdult, lastAdult);
        (Fix64 life, _) = e.FixtureKitLife(11);
        Fix64 left = life - Fix64.FromInt(5);
        e.FixtureSetKit(firstAdult, 11, left);
        e.IT6AccountingTraceEnabled = true;

        e.FixtureWearAndIssueKits(0, tier: 0, lastAdult);

        Assert.Equal(0, e.FigureKit(firstAdult).Tier);
        Assert.Equal(11, e.FigureKit(lastAdult).Tier);
        Assert.Equal(left, e.FixtureKitLeft(lastAdult));
        Assert.Equal(1, e.CaptureIT6FuelSites()[0].Kit11Holders);
        Assert.Equal(1, e.CaptureIT6FuelSites()[0].T11ToolWorkers);
    }

    [Fact]
    public void An_early_upgrade_from_T11_to_T12_gives_no_metal_back_but_a_Bronze_kit_still_remelts()
    {
        foreach ((int oldTier, bool refunds) in new[] { (11, false), (10, true) })
        {
            BronzeFixture f = Iron("MET-08", "MET-09", "MET-10");
            LivingEconomyModel e = f.Economy;
            e.IT6AccountingTraceEnabled = true;
            int adult = FirstAdult(e);
            e.FixtureSetKit(adult, oldTier, Fix64.FromInt(20));
            e.FixtureStock(0, "G-TOOL12", Fix64.FromInt(100)); // enough for the kitless first, so the upgrade is served too
            Fix64 initialIronKits = e.GoodStock(0, "G-TOOL11") + e.GoodStock(0, "G-TOOL12") +
                Fix64.FromInt(e.IronKitHolders(0).T11 + e.IronKitHolders(0).T12);
            Fix64 remelted = e.MetalStats(0).RemeltCopper;
            f.RunSeasons(1);
            Assert.Equal(12, e.FigureKit(adult).Tier);
            Fix64 returned = e.MetalStats(0).RemeltCopper - remelted;
            if (refunds) Assert.True(returned > Fix64.Zero, "a Bronze kit retired early is remelted");
            else Assert.Equal(Fix64.Zero, returned);           // an Iron kit is never refunded
            if (oldTier == 11)
            {
                IT6AccountingSnapshot trace = e.CaptureIT6Accounting(0);
                Fix64 finalIronKits = e.GoodStock(0, "G-TOOL11") + e.GoodStock(0, "G-TOOL12") +
                    Fix64.FromInt(e.IronKitHolders(0).T11 + e.IronKitHolders(0).T12);
                Assert.Equal(1, trace.Kit11Upgrades);
                Assert.Equal(Fix64.Zero, initialIronKits + trace.Kit11Produced + trace.Kit12Produced -
                    Fix64.FromInt(trace.Kit11Breaks + trace.Kit12Breaks) - trace.Kit11Retired - trace.Kit12Retired - finalIronKits);
            }
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
        // The Iron fuel guard measures the whole charcoal catchment, so thin all of it (the fixture's own forest tiles are only part).
        f.Economy.FixtureThinCharcoalCatchment(BronzeFixture.Settlement, percent);
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
    public void Total_catchment_fuel_loss_stops_new_Iron_kit_production()
    {
        BronzeFixture f = BronzeFixture.CreateFuelScenario(TestPack.Pack, Seed, 60, ironChain: true, coppice: true, forestTiles: 13, "MET-08", "MET-09");
        LivingEconomyModel e = f.Economy;
        e.IT6AccountingTraceEnabled = true;
        f.RunSeasons(1); // record the productive forest baseline before the controlled loss
        IT6AccountingSnapshot before = e.CaptureIT6Accounting(0);
        ThinTheForest(f, 0);
        e.FixtureStock(0, "G-WOOD", -e.GoodStock(0, "G-WOOD"));
        e.FixtureStock(0, "G-CHAR", -e.GoodStock(0, "G-CHAR"));
        foreach (string good in new[] { "G-IRONORE", "G-IRON", "G-WROUGHT", "G-STEEL", "G-TOOL11", "G-TOOL12" })
            e.FixtureStock(0, good, -e.GoodStock(0, good));
        e.FixtureStock(0, "G-IRONORE", Fix64.FromInt(60));
        f.RunSeasons(4);

        IT6AccountingSnapshot trace = e.CaptureIT6Accounting(0);
        Assert.Equal(before.Kit11Produced, trace.Kit11Produced);
        Assert.Equal(before.Kit12Produced, trace.Kit12Produced);
        Assert.Equal(Fix64.FromInt(60), e.GoodStock(0, "G-IRONORE"));
        Assert.Equal(Fix64.Zero, e.GoodStock(0, "G-WROUGHT"));
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
            f.Economy.IT6AccountingTraceEnabled = true;
            f.Economy.FixtureStock(0, "G-TOOL11", Fix64.FromInt(40)); // far more than the parent's own adults lack
            Fix64 before = AllIronKits(f.Economy, 0, daughter);
            f.RunSeasons(1);
            Assert.Equal(before, AllIronKits(f.Economy, 0, daughter)); // moving kits creates and destroys none (no new smithing in a season with this stock)
            if (sharing)
            {
                int parentId = f.Economy.SettlementLite(0).Id, daughterId = f.Economy.SettlementLite(daughter).Id;
                IT6FuelSiteSnapshot source = Assert.Single(f.Economy.CaptureIT6FuelSites(), x => x.SettlementId == parentId);
                IT6FuelSiteSnapshot receiver = Assert.Single(f.Economy.CaptureIT6FuelSites(), x => x.SettlementId == daughterId);
                Assert.True(source.Kit11Sent >= Fix64.One);
                Assert.Equal(source.Kit11Sent, receiver.Kit11Received);
                Assert.Equal(Fix64.Zero, receiver.Kit12Received);
            }
            if (sharing) daughterHolders = IronKitHolders(f.Economy, daughter); else aloneHolders = IronKitHolders(f.Economy, daughter);
        }
        Assert.True(daughterHolders > 0, "the daughter's adults should hold kits the parent could spare");
        Assert.Equal(0, aloneHolders);
    }

    [Fact]
    public void Sister_kit_sharing_serves_current_tool_workers_before_other_adults()
    {
        (BronzeFixture f, int daughter) = WithDaughter(sharing: true);
        LivingEconomyModel e = f.Economy;
        int donorWorker = -1, receiverWorker = -1;
        for (int i = 0; i < e.FigureCount; i++)
        {
            if (e.Figure(i).Age < 14) continue;
            if (e.Figure(i).SettlementId == 0 && donorWorker < 0) donorWorker = i;
            if (e.Figure(i).SettlementId == daughter && receiverWorker < 0) receiverWorker = i;
        }
        Assert.True(donorWorker >= 0 && receiverWorker >= 0);
        e.FixtureSetKit(donorWorker, 11, Fix64.FromInt(20));
        e.FixtureSetLastToolWorker(donorWorker, worked: true);
        e.FixtureSetLastToolWorker(receiverWorker, worked: true);
        e.FixtureStock(0, "G-TOOL11", Fix64.One);
        e.IT6AccountingTraceEnabled = true;
        Fix64 before = AllIronKits(e, 0, daughter);

        e.FixtureShareIronGoods(0);

        Assert.Equal(before, AllIronKits(e, 0, daughter));
        Assert.Equal(Fix64.Zero, e.GoodStock(0, "G-TOOL11"));
        Assert.Equal(Fix64.One, e.GoodStock(daughter, "G-TOOL11"));
        IT6FuelSiteSnapshot source = Assert.Single(e.CaptureIT6FuelSites(), x => x.SettlementId == 0);
        IT6FuelSiteSnapshot receiver = Assert.Single(e.CaptureIT6FuelSites(), x => x.SettlementId == daughter);
        Assert.Equal(Fix64.One, source.Kit11Sent);
        Assert.Equal(Fix64.One, receiver.Kit11Received);
        IT6KitTransferSnapshot transfer = Assert.Single(e.CaptureIT6KitTransfers());
        Assert.Equal(0, transfer.Season);
        Assert.Equal(0, transfer.FromSettlementId);
        Assert.Equal(daughter, transfer.ToSettlementId);
        Assert.Equal(11, transfer.Tier);
        Assert.Equal(Fix64.One, transfer.Quantity);
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
    public void Key4_assigns_iron_kit_work_to_the_best_forest_catchment_and_biases_siting_toward_hauled_wood()
    {
        BronzeFixture siting = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "KEY-4");
        int preferredSite = siting.Economy.FixtureChooseFissionSite(0);
        Assert.True(preferredSite >= 0);
        Assert.True(siting.Economy.FixtureIronFuelCatchmentScoreAt(0, preferredSite) > Fix64.Zero,
            "post-KEY-4 fission should choose a productive-forest site when one is available");

        BronzeFixture f = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "KEY-4", "MET-08", "MET-09");
        foreach (int tile in f.ForestTiles) f.Economy.FixtureSetWood(tile, Fix64.FromInt(5));
        int parentTile = f.Economy.SettlementLite(0).SiteTile;
        int hubSite = -1, hauledForest = -1;
        Fix64 bestFuel = Fix64.Zero;
        int bestHaulDistance = int.MaxValue;
        for (int site = 0; site < f.Economy.FixtureWorldTileCount; site++)
        {
            int fromParent = HexGrid.Distance(parentTile, site, f.Economy.FixtureWorldWidth);
            if (!f.Economy.FixtureIsHabitableSite(site) || fromParent is < 7 or > 14 ||
                HexGrid.Distance(f.IronTile, site, f.Economy.FixtureWorldWidth) <= 8 ||
                f.Economy.FixtureHasLocalIronOreSourceAt(0, site)) continue;
            int forest = -1, forestDistance = int.MaxValue;
            foreach (int tile in f.ForestTiles)
            {
                int forestFromParent = HexGrid.Distance(parentTile, tile, f.Economy.FixtureWorldWidth);
                int fromSite = HexGrid.Distance(site, tile, f.Economy.FixtureWorldWidth);
                if (forestFromParent > 3 || fromSite is < 4 or > 8 || fromSite >= forestDistance) continue;
                forest = tile;
                forestDistance = fromSite;
            }
            if (forest < 0) continue;
            Fix64 score = f.Economy.FixtureIronFuelCatchmentScoreAt(0, site);
            if (forestDistance > bestHaulDistance || forestDistance == bestHaulDistance && score <= bestFuel) continue;
            bestFuel = score;
            bestHaulDistance = forestDistance;
            hubSite = site;
            hauledForest = forest;
        }
        Assert.True(hubSite >= 0, "the fixture should offer a sharing-range wooded hub site away from the iron mine");
        f.Economy.FixtureSetWood(hauledForest, Fix64.FromInt(10_000));
        int daughter = f.Economy.FoundByDecree(0, hubSite, Fix64.FromRatio(2, 5), minDaughter: 6, startCohesion: 60);
        Assert.True(daughter >= 0);
        f.RunSeasons(1); // register the daughter in the shared metal economy
        f.Economy.FixtureSetWood(hauledForest, Fix64.FromInt(10_000)); // restore productive catchment after the founding season
        f.Economy.FixtureSetMetalFacilities(daughter, forges: 0, smelters: 0);
        Assert.Equal(0, f.Economy.FixtureIronFuelHubAssignment(daughter));
        Assert.True(f.Economy.FixtureSisterKitDeficit(0) >= Fix64.FromInt(5), "the reachable producer must count the non-producing daughter's kit demand");
        f.Economy.FixtureBuild(daughter, "forge");
        f.Economy.FixtureBuild(daughter, "smelter");
        f.Economy.FixtureSetWood(hauledForest, Fix64.FromInt(10_000)); // refresh derived hub eligibility after finishing the facilities
        f.Economy.FixtureAddMiningCamp(0, f.IronTile, crew: 4, built: true);
        Assert.True(f.Economy.FixtureHasLocalIronOreSource(0));

        int daughterTile = f.Economy.SettlementLite(daughter).SiteTile;
        Fix64 daughterCatchment = f.Economy.FixtureIronFuelCatchmentScore(daughter);
        Fix64 parentCatchment = f.Economy.FixtureIronFuelCatchmentScore(0);
        int hub = daughterCatchment > parentCatchment ? daughter : 0; // parent wins exact ties by lower settlement id
        int nonHub = hub == daughter ? 0 : daughter;
        Assert.Equal(daughter, hub);
        Assert.False(f.Economy.FixtureHasLocalIronOreSource(hub), "the wooded daughter should rely on its sister mine");
        Assert.Equal(0, f.Economy.FixtureIronOreSupplierForHub(hub));
        Assert.True(f.Economy.FixtureIsIronFuelHub(hub));
        Assert.False(f.Economy.FixtureIsIronFuelHub(nonHub));
        Assert.True(f.Economy.FixtureIronLocalKitProducer(nonHub), "an already-forging sister with a full local wood factor can cover its own demand");
        int charcoalReach = f.Economy.FixtureIronFuelCharcoalReach(hub);
        int shareReach = f.Economy.FixtureSharing(0, hub, nonHub).Reach;
        Assert.True(charcoalReach >= 8 + shareReach + 8,
            "every Iron fuel consumer should combine the ordinary charcoal radius, existing domestic sharing reach, and the eight-tile hauling margin");
        Assert.Equal(charcoalReach, f.Economy.FixtureIronFuelCharcoalReach(nonHub));
        int hubTile = f.Economy.SettlementLite(hub).SiteTile;
        int nonHubTile = f.Economy.SettlementLite(nonHub).SiteTile;
        int hubOnlyForest = -1;
        for (int tile = 0; tile < f.Economy.FixtureWorldTileCount; tile++)
        {
            if (!f.Economy.FixtureIsForestTile(tile) || f.Economy.FixtureIsRivalTile(0, tile)) continue;
            int fromHub = HexGrid.Distance(hubTile, tile, f.Economy.FixtureWorldWidth);
            int fromMember = HexGrid.Distance(nonHubTile, tile, f.Economy.FixtureWorldWidth);
            if (fromHub <= 3 || fromHub > charcoalReach || fromMember <= charcoalReach) continue;
            hubOnlyForest = tile;
            break;
        }
        Assert.True(hubOnlyForest >= 0, "the fixture should have forest inside the hub catchment but beyond the member's direct haul range");
        f.Economy.FixtureSetWood(hubOnlyForest, Fix64.FromInt(500));
        Assert.Equal(hub, f.Economy.FixtureIronFuelHubAssignment(nonHub));
        Assert.True(f.Economy.FixtureIronFuelHaulDistanceToTile(nonHub, hubOnlyForest) < 0,
            "ordinary access must remain local before the hub-fuel candidate is enabled");
        f.Economy.IronHubFuelSharingEnabled = true;
        Assert.Equal(HexGrid.Distance(nonHubTile, hubTile, f.Economy.FixtureWorldWidth) +
            HexGrid.Distance(hubTile, hubOnlyForest, f.Economy.FixtureWorldWidth),
            f.Economy.FixtureIronFuelHaulDistanceToTile(nonHub, hubOnlyForest));
        Assert.True(f.Economy.FixtureSisterKitDeficit(hub) == Fix64.Zero,
            "the hub should not plan a qualified satellite's own kit demand twice");
        Assert.False(f.Economy.FixtureSisterKitDeficit(nonHub) > Fix64.Zero, "the non-hub should not duplicate the hub's target");
        Assert.True(f.Economy.FixtureIronFuelSitingMultiplier(0, f.ForestTiles[0]) > Fix64.One);

        foreach (string good in new[] { "G-IRON", "G-IRONORE", "G-WROUGHT", "G-STEEL", "G-TOOL11", "G-TOOL12", "G-CHAR" })
            f.Economy.FixtureStock(hub, good, -f.Economy.GoodStock(hub, good));
        f.Economy.FixtureStock(0, "G-IRONORE", Fix64.FromInt(20));
        Assert.True(f.Economy.FixtureIronFuelOreNeed(hub) > Fix64.Zero, "the test hub needs raw ore before its supplier is asked to send it");
        f.Economy.FixtureShareIronGoods(0);
        Assert.True(f.Economy.GoodStock(hub, "G-IRONORE") > Fix64.Zero,
            "the selected supplier should fill the hub's raw ore need before production consumes it");
        Fix64 bloomBeforeOreSupply = f.Economy.GoodStock(hub, "G-IRON");
        f.RunSeasons(1);
        Assert.True(f.Economy.GoodStock(hub, "G-IRONORE") > Fix64.Zero || f.Economy.GoodStock(hub, "G-IRON") > bloomBeforeOreSupply,
            $"the selected ore supplier should send raw ore to the wooded hub, where it may already have been smelted (supplier={f.Economy.FixtureIronOreSupplierForHub(hub)}, parent ore={f.Economy.GoodStock(0, "G-IRONORE")}, hub ore={f.Economy.GoodStock(hub, "G-IRONORE")}, hub bloom={f.Economy.GoodStock(hub, "G-IRON")}, hub charcoal={f.Economy.GoodStock(hub, "G-CHAR")})");
        f.Economy.FixtureStock(hub, "G-CHAR", Fix64.FromInt(3));
        Assert.True(f.Economy.FixtureRunRecipe(hub, "bloomery", Fix64.One, Fix64.FromInt(10)) > Fix64.Zero);
        Assert.True(f.Economy.GoodStock(hub, "G-IRON") > Fix64.Zero,
            "the hub should run the charcoal-consuming bloomery on the supplied ore");

        BronzeFixture reachFixture = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed + 1, 60, "KEY-4", "SOC-07", "MET-08", "MET-09");
        int reachSite = reachFixture.Economy.SettlementLite(0).SiteTile;
        int extendedReach = reachFixture.Economy.FixtureIronFuelCharcoalReach(0);
        int normalAndSharingReach = extendedReach - 8;
        int remoteForest = -1;
        for (int tile = 0; tile < reachFixture.Economy.FixtureWorldTileCount; tile++)
        {
            if (!reachFixture.Economy.FixtureIsForestTile(tile) || reachFixture.Economy.FixtureIsRivalTile(0, tile)) continue;
            int distance = HexGrid.Distance(reachSite, tile, reachFixture.Economy.FixtureWorldWidth);
            if (distance <= normalAndSharingReach || distance > extendedReach) continue;
            remoteForest = tile;
            break;
        }
        Assert.True(remoteForest >= 0, "the map should have productive forest in the eight-tile hauling margin beyond ordinary plus sharing reach");
        reachFixture.Economy.FixtureBuild(0, "forge");
        reachFixture.Economy.FixtureBuild(0, "smelter");
        reachFixture.Economy.FixtureAddMiningCamp(0, reachFixture.IronTile, crew: 4, built: true);
        for (int tile = 0; tile < reachFixture.Economy.FixtureWorldTileCount; tile++)
        {
            if (!reachFixture.Economy.FixtureIsForestTile(tile) ||
                HexGrid.Distance(reachSite, tile, reachFixture.Economy.FixtureWorldWidth) > extendedReach) continue;
            reachFixture.Economy.FixtureSetWood(tile, Fix64.FromInt(5));
        }
        reachFixture.Economy.FixtureSetWood(remoteForest, Fix64.FromInt(10_000));
        foreach (string good in new[] { "G-WOOD", "G-IRON", "G-IRONORE", "G-WROUGHT", "G-STEEL", "G-TOOL11", "G-TOOL12", "G-CHAR" })
            reachFixture.Economy.FixtureStock(0, good, -reachFixture.Economy.GoodStock(0, good));
        reachFixture.Economy.FixtureStock(0, "G-IRONORE", Fix64.FromInt(100));
        Fix64 remoteBefore = reachFixture.Economy.StandingWood(remoteForest);
        reachFixture.RunSeasons(1);
        Assert.True(reachFixture.Economy.StandingWood(remoteForest) < remoteBefore,
            "the hub should harvest forest just beyond ordinary sharing reach and pay the normal distance-based haul cost");
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

    [Fact]
    public void Public_grain_moves_only_a_donors_grain_above_its_winter_reserve_and_creates_none()
    {
        BronzeFixture f = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "FOD-09");
        int daughter = f.Economy.FoundByDecree(0, f.CampTile, Fix64.FromRatio(2, 5), minDaughter: 6, startCohesion: 60);
        Assert.True(daughter >= 0);
        f.Economy.FixtureSetTier(daughter, 1);
        f.Economy.FixtureStock(0, "G-GRAIN", Fix64.FromInt(300));
        Fix64 before = f.Economy.FixtureGrainUnits(0) + f.Economy.FixtureGrainUnits(daughter);
        Fix64 receiverBefore = f.Economy.FixtureGrainUnits(daughter);

        Fix64 moved = f.Economy.FixtureSharePublicGrain(0);

        Assert.True(moved > Fix64.Zero, "the town should have been sent grain");
        Assert.Equal(before, f.Economy.FixtureGrainUnits(0) + f.Economy.FixtureGrainUnits(daughter)); // none created or lost
        Assert.Equal(receiverBefore + moved, f.Economy.FixtureGrainUnits(daughter));
        // A donor with nothing above its winter reserve sends nothing.
        BronzeFixture poor = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "FOD-09");
        int poorDaughter = poor.Economy.FoundByDecree(0, poor.CampTile, Fix64.FromRatio(2, 5), minDaughter: 6, startCohesion: 60);
        poor.Economy.FixtureSetTier(poorDaughter, 1);
        poor.Economy.FixtureStock(0, "G-GRAIN", -poor.Economy.FixtureGrainUnits(0)); // the donor holds no grain at all
        Assert.Equal(Fix64.Zero, poor.Economy.FixtureSharePublicGrain(0));
        // And nothing moves without FOD-09 or in the Bronze chapter.
        BronzeFixture noResearch = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60);
        int d2 = noResearch.Economy.FoundByDecree(0, noResearch.CampTile, Fix64.FromRatio(2, 5), minDaughter: 6, startCohesion: 60);
        noResearch.Economy.FixtureSetTier(d2, 1);
        noResearch.Economy.FixtureStock(0, "G-GRAIN", Fix64.FromInt(300));
        Assert.Equal(Fix64.Zero, noResearch.Economy.FixtureSharePublicGrain(0));
    }

    [Fact]
    public void Construction_research_shortens_Iron_structures_one_factor_at_a_time_and_a_crane_halves_large_works()
    {
        var pack = TestPack.WithNumeric("BLD-11", "MEC-04", "NUM-07", "STN-12");
        BronzeFixture plain = BronzeFixture.CreateIronWithCamp(pack, Seed, 60);
        Assert.Equal(Fix64.FromInt(40), plain.Economy.FixtureIronLabor(0, "aqueduct"));
        Assert.Equal(Fix64.FromInt(20), plain.Economy.FixtureIronLabor(0, "bathhouse"));

        BronzeFixture general = BronzeFixture.CreateIronWithCamp(pack, Seed, 60, "BLD-11", "MEC-04");
        Fix64 expected = Fix64.FromInt(40) * F("0.76923") * F("0.869565");
        Assert.True(Fix64.Abs(general.Economy.FixtureIronLabor(0, "aqueduct") - expected) <= F("0.01"), "build_labor mods multiply");

        BronzeFixture stone = BronzeFixture.CreateIronWithCamp(pack, Seed, 60, "STN-12");
        Near(Fix64.FromInt(20) * F("0.6"), stone.Economy.FixtureIronLabor(0, "bathhouse"));          // stone work x0.6
        Near(Fix64.FromInt(8), stone.Economy.FixtureIronLabor(0, "terrace"));                       // a terrace is not stone work

        BronzeFixture survey = BronzeFixture.CreateIronWithCamp(pack, Seed, 60, "NUM-07");
        Assert.True(Fix64.Abs(survey.Economy.FixtureIronLabor(0, "aqueduct") - Fix64.FromInt(40) / F("1.15")) <= F("0.01"));
        Assert.True(Fix64.Abs(survey.Economy.FixtureIronLabor(0, "terrace") - Fix64.FromInt(8) / F("1.15")) <= F("0.01"));
        Assert.Equal(Fix64.FromInt(20), survey.Economy.FixtureIronLabor(0, "bathhouse"));           // surveying does not shorten baths

        BronzeFixture crane = BronzeFixture.CreateIronWithCamp(pack, Seed, 60);
        crane.Economy.FixtureBuildIronStructure(0, "crane");
        Assert.Equal(Fix64.FromInt(20), crane.Economy.FixtureIronLabor(0, "aqueduct"));              // large work halved
        Assert.Equal(Fix64.FromInt(20), crane.Economy.FixtureIronLabor(0, "bathhouse"));             // not large
    }

    [Fact]
    public void A_windlass_well_counts_as_a_fresh_water_source_for_the_water_cap_and_the_Bronze_economy_has_none_of_it()
    {
        BronzeFixture f = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "KEY-4");
        Fix64 before = f.Economy.FixtureWaterCapBase(0);
        f.Economy.FixtureBuildIronStructure(0, "windlass_well");
        Fix64 after = f.Economy.FixtureWaterCapBase(0);
        Assert.True(after == before + Fix64.FromInt(8) || after == Fix64.FromInt(150), $"{before} -> {after}");
    }

    [Fact]
    public void Grafting_doubles_the_fruit_of_the_plots_an_iron_blade_keeps_and_no_others()
    {
        BronzeFixture f = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "FOR-08");
        BronzeFixture noResearch = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60);
        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, Seed, 60, true, true, true, true, false, "FOR-08");
        int adult = FirstAdult(f.Economy);
        foreach (BronzeFixture x in new[] { f, noResearch, bronze }) x.Economy.FixtureSetKit(adult, 11, Fix64.FromInt(20));
        int blades = 0;
        foreach (int t in f.Economy.FixtureAdultKitTiers(0)) if (t >= 11) blades++;
        Assert.Equal(1, blades);
        Assert.Equal(Fix64.FromInt(2), f.Economy.FixtureGraftedFactor(0, 0));
        Assert.Equal(Fix64.FromInt(2), f.Economy.FixtureGraftedFactor(0, 1));
        Assert.Equal(Fix64.One, f.Economy.FixtureGraftedFactor(0, 2));                    // one blade keeps two plots
        Assert.Equal(Fix64.One, noResearch.Economy.FixtureGraftedFactor(0, 0));           // FOR-08 unknown
        Assert.Equal(Fix64.One, bronze.Economy.FixtureGraftedFactor(0, 0));               // not the Iron chapter
    }

    [Fact]
    public void Iron_chapter_cohesion_terms_apply_only_where_their_scope_holds()
    {
        BronzeFixture without = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60);
        BronzeFixture withTheatreResearch = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "ART-08", "BEL-08");
        int baseline = without.Economy.FixtureCohesionTarget(0);
        // The theatre's +15 and the integration +10 used to fall through to "everywhere"; with the research but no theatre and no conquered town, nothing changes.
        Assert.Equal(baseline, withTheatreResearch.Economy.FixtureCohesionTarget(0));
        withTheatreResearch.Economy.FixtureBuildIronStructure(0, "theatre_arena");
        int lifted = withTheatreResearch.Economy.FixtureCohesionTarget(0);
        Assert.True(lifted == Math.Min(100, baseline + 15), $"{baseline} -> {lifted}");
    }

    private static (BronzeFixture Fixture, int Far) WithFarProvince(params string[] projects)
    {
        BronzeFixture f = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, projects);
        int start = f.Economy.SettlementLite(0).SiteTile;
        var world = CivSim.Sim.Economy.WorldEconomyInput.FromGeneratedWorld(CivSim.Sim.World.WorldGenerator.Generate(Seed));
        for (int tile = 0; tile < world.TileCount; tile++)
        {
            int d = CivSim.Sim.World.HexGrid.Distance(start, tile, world.Width);
            if (d < 14 || d > 18) continue;
            int far = f.Economy.FoundByDecree(0, tile, Fix64.FromRatio(2, 5), minDaughter: 6, startCohesion: 60);
            if (far < 0) continue;
            f.Economy.FixtureSetTier(0, 0); // give both towns a civic state so Cohesion is live
            f.Economy.FixtureSetTier(far, 0);
            return (f, far);
        }
        throw new InvalidOperationException("no site for a far province");
    }

    [Fact]
    public void An_administrator_and_a_governor_are_staffed_roles_and_their_relief_depends_on_being_in_post()
    {
        string[] state = { "GOV-07", "TER-08", "KEY-4" };
        (BronzeFixture staffed, int far) = WithFarProvince(state);
        (BronzeFixture bare, int farBare) = WithFarProvince("KEY-4");
        for (int season = 0; season < 6; season++)
        {
            foreach (BronzeFixture f in new[] { staffed, bare }) { f.Economy.FixtureSetTier(0, 1); f.Economy.FixtureSetTier(far, 1); }
            staffed.RunSeasons(1);
            bare.RunSeasons(1);
        }
        Assert.Equal(1, staffed.Economy.FixtureRoleCount(0, "administrator"));
        Assert.Equal(1, staffed.Economy.FixtureRoleCount(far, "governor"));
        Assert.Equal(far, staffed.Economy.FixtureGovernorSeat(far));
        Assert.Equal(0, bare.Economy.FixtureRoleCount(0, "administrator"));
        (int overStaffed, _) = staffed.Economy.CohesionPressures(far);
        (int overBare, _) = bare.Economy.CohesionPressures(farBare);
        Assert.True(overBare > 0, "a far settlement should feel distance");
        Assert.True(overStaffed < overBare, $"staffed relief should lower the strain ({overStaffed} against {overBare})");
        // Vacate the governor and the province relief goes with it, while the census relief stays.
        staffed.Economy.FixtureClearRoles(far);
        Assert.Equal(-1, staffed.Economy.FixtureGovernorSeat(far));
    }

    [Fact]
    public void A_feudal_contract_removes_distance_and_span_strain_in_a_governed_province_and_nothing_else()
    {
        (BronzeFixture f, int far) = WithFarProvince("GOV-07", "TER-08", "GOV-09", "KEY-4");
        for (int season = 0; season < 6; season++) { f.Economy.FixtureSetTier(0, 1); f.Economy.FixtureSetTier(far, 1); f.RunSeasons(1); }
        Assert.Equal(far, f.Economy.FixtureGovernorSeat(far));
        (int overreach, int span) = f.Economy.CohesionPressures(far);
        Assert.Equal(0, overreach);
        Assert.Equal(0, span);
        // Without the contract the same province still feels both.
        (BronzeFixture g, int farG) = WithFarProvince("GOV-07", "TER-08", "KEY-4");
        for (int season = 0; season < 6; season++) { g.Economy.FixtureSetTier(0, 1); g.Economy.FixtureSetTier(farG, 1); g.RunSeasons(1); }
        Assert.True(g.Economy.CohesionPressures(farG).Overreach > 0);
    }

    [Fact]
    public void A_paved_link_shortens_the_distance_strain_and_courier_relays_add_their_relief_only_along_it()
    {
        (BronzeFixture plain, int far) = WithFarProvince("KEY-4", "TRN-07", "TRN-08", "COM-08");
        int rivers = plain.Economy.FixtureRiversOnLink(far);
        int unlinked = plain.Economy.CohesionPressures(far).Overreach;
        Assert.True(unlinked > 0);
        Assert.False(plain.Economy.FixtureRoadLinked(far));

        plain.Economy.FixtureBuildIronStructure(far, "road");
        // A road over a river is incomplete without a bridge; with no river it is linked at once.
        Assert.Equal(rivers == 0, plain.Economy.FixtureRoadLinked(far));
        if (rivers > 0) plain.Economy.FixtureBuildIronStructure(far, "bridge");
        Assert.True(plain.Economy.FixtureRoadLinked(far));
        int linked = plain.Economy.CohesionPressures(far).Overreach;
        Assert.True(linked < unlinked, $"the road should cut the strain ({linked} against {unlinked})");

        // Relays need a maintained road: the same research without the road gives no relief.
        (BronzeFixture noRoad, int farNoRoad) = WithFarProvince("KEY-4", "TRN-07", "COM-08");
        (BronzeFixture noRelay, int farNoRelay) = WithFarProvince("KEY-4", "TRN-07");
        Assert.Equal(unlinked, noRoad.Economy.CohesionPressures(farNoRoad).Overreach);
        noRoad.Economy.FixtureBuildIronStructure(farNoRoad, "road");
        noRelay.Economy.FixtureBuildIronStructure(farNoRelay, "road");
        if (noRoad.Economy.FixtureRiversOnLink(farNoRoad) > 0)
        {
            noRoad.Economy.FixtureBuildIronStructure(farNoRoad, "bridge");
            noRelay.Economy.FixtureBuildIronStructure(farNoRelay, "bridge");
        }
        Assert.True(noRoad.Economy.CohesionPressures(farNoRoad).Overreach <= noRelay.Economy.CohesionPressures(farNoRelay).Overreach);
        // And a Bronze town never has a road at all.
        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, Seed, 60, true, true, true, true, false, "KEY-4", "TRN-07");
        Assert.False(bronze.Economy.FixtureRoadLinked(0));
    }

    [Fact]
    public void Standing_soldiers_are_a_tenth_of_the_adults_a_fifth_with_iron_arms_and_only_while_fed()
    {
        BronzeFixture none = Iron("KEY-4");
        BronzeFixture some = Iron("KEY-4", "WAR-07");
        BronzeFixture more = Iron("KEY-4", "WAR-07", "WAR-08");
        BronzeFixture bronze = Bronze("KEY-4", "WAR-07");
        Assert.Equal(0, none.Economy.FixtureSoldierSlots(0));
        Assert.Equal(0, bronze.Economy.FixtureSoldierSlots(0));
        int tenth = some.Economy.FixtureSoldierSlots(0);
        Assert.True(tenth > 0, "WAR-07 should give a town standing soldier slots");
        int fifth = more.Economy.FixtureSoldierSlots(0); // floors apply to each share, so a fifth is twice a tenth to within one
        Assert.InRange(fifth, 2 * tenth, 2 * tenth + 1);
    }

    [Fact]
    public void Soldiers_are_staffed_with_real_adults_and_eat_a_quarter_ration_each()
    {
        BronzeFixture f = Iron("KEY-4", "WAR-07");
        f.Economy.FixtureSetTier(0, 1);
        f.RunSeasons(4);
        (int soldiers, int supplied) = f.Economy.FixtureSoldiers(0);
        Assert.True(soldiers > 0, "no one was drilled into the guard");
        Assert.True(soldiers <= f.Economy.FixtureSoldierSlots(0));
        Assert.Equal(soldiers, supplied);
        // A Bronze town and a town without WAR-07 never raise a guard.
        BronzeFixture without = Iron("KEY-4");
        without.Economy.FixtureSetTier(0, 1);
        without.RunSeasons(4);
        Assert.Equal(0, without.Economy.FixtureSoldiers(0).Soldiers);
        BronzeFixture bronze = Bronze("KEY-4", "WAR-07");
        bronze.RunSeasons(4);
        Assert.Equal(0, bronze.Economy.FixtureSoldiers(0).Soldiers);
    }

    [Fact]
    public void Iron_arms_are_made_for_the_soldiers_and_wear_out_by_age()
    {
        BronzeFixture f = Iron("KEY-4", "MET-09", "WAR-07", "WAR-08");
        f.Economy.FixtureSetTier(0, 1);
        f.RunSeasons(4);
        int soldiers = f.Economy.FixtureSoldiers(0).Soldiers;
        Assert.True(soldiers > 0);
        // With a forge and iron in store the standing guard is armed.
        f.Economy.FixtureStock(0, "G-ARMS3", Fix64.FromInt(10));
        Fix64 before = f.Economy.GoodStock(0, "G-ARMS3");
        f.RunSeasons(8);
        Fix64 after = f.Economy.GoodStock(0, "G-ARMS3");
        Assert.True(after < before, $"unused arms should wear: {before} to {after}");
        Assert.True(after > Fix64.Zero, "arms last longer than eight seasons");
    }

    [Fact]
    public void Iron_arms_and_professional_training_strengthen_a_fighter_by_exactly_the_documented_amounts()
    {
        Assert.Equal(Fix64.FromRatio(24, 10), Combat.IronWeaponStrength(steelEdge: false));
        Assert.Equal(Fix64.FromRatio(30, 10), Combat.IronWeaponStrength(steelEdge: true));
        // 2.4 x 1.25 = 3.0: the steel edge is the one bonus, not a second one.
        Near(Combat.IronWeaponStrength(false) * Fix64.FromRatio(125, 100), Combat.IronWeaponStrength(true));
        Assert.Equal(Fix64.FromRatio(15, 10), Combat.ProfessionalTraining);
        Assert.True(Combat.ProfessionalTraining > Fix64.FromRatio(13, 10), "a professional outfights a warrior");
        Assert.True(Combat.IronWeaponStrength(false) > Combat.E3WeaponStrength("tin"));
    }

    [Fact]
    public void The_soldier_count_survives_a_snapshot_round_trip()
    {
        BronzeFixture f = Iron("KEY-4", "WAR-07");
        f.Economy.FixtureSetTier(0, 1);
        f.RunSeasons(4);
        byte[] bytes = Snapshot(f.Economy);
        BronzeFixture g = Iron("KEY-4", "WAR-07");
        Restore(g.Economy, bytes);
        Assert.Equal(f.Economy.FixtureSoldiers(0), g.Economy.FixtureSoldiers(0));
        Assert.Equal(bytes, Snapshot(g.Economy));
    }

    [Fact]
    public void A_gatehouse_follows_a_wall_and_lifts_the_intact_wall_from_three_to_four_and_a_half()
    {
        BronzeFixture f = Iron("KEY-4", "DEF-06");
        f.Economy.FixtureSetTier(0, 1);
        // No wall, no gatehouse: the work has nothing to stand on.
        Assert.False(f.Economy.FixtureIronBuildWanted(0, "gatehouse"));
        f.Economy.FixtureBuildStructure(0, "stone_walls");
        Assert.True(f.Economy.FixtureHasStoneWalls(0));
        Assert.True(f.Economy.FixtureIronBuildWanted(0, "gatehouse"));
        Assert.False(f.Economy.HasGatehouse(0));
        f.Economy.FixtureBuildIronStructure(0, "gatehouse");
        Assert.True(f.Economy.HasGatehouse(0));
        Assert.False(f.Economy.FixtureIronBuildWanted(0, "gatehouse"));
        // The wall's multiplier: 3.0 bare, 4.5 with towers; only on a standing wall.
        var bare = new Defenses(false, false, false, false, false, false, false, false, false, false, StoneWalls: true, BronzeRules: true);
        var towers = bare with { Gatehouse = true };
        Assert.Equal(Fix64.FromRatio(11, 10) * Fix64.FromInt(3), bare.Multiplier());
        Assert.Equal(Fix64.FromRatio(11, 10) * Fix64.FromRatio(9, 2), towers.Multiplier());
        Assert.Equal(bare.Multiplier(), (bare with { Gatehouse = false }).Multiplier());
    }

    [Fact]
    public void Frontier_segments_face_only_a_close_rival_once_each_and_a_far_one_not_at_all()
    {
        BronzeFixture f = Iron("KEY-4", "DEF-06", "DEF-07");
        f.Economy.FixtureSetTier(0, 1);
        int rival = -1;
        for (int i = 1; i < f.Economy.FixtureSettlementCount; i++) if (f.Economy.FixtureCivOf(i) != f.Economy.FixtureCivOf(0)) { rival = i; break; }
        Assert.True(rival >= 0, "the fixture world has a foreign settlement");
        int rivalCiv = f.Economy.FixtureCivOf(rival);
        Assert.Equal(-1, f.Economy.FixtureNextFrontierFacing(0)); // the rival's start is far away
        Assert.False(f.Economy.FixtureIronBuildWanted(0, "frontier_wall_segment"));
        int site = f.Economy.SettlementLite(0).SiteTile;
        f.Economy.FixtureMoveSite(rival, site + 2);
        Assert.Equal(rivalCiv, f.Economy.FixtureNextFrontierFacing(0));
        Assert.True(f.Economy.FixtureIronBuildWanted(0, "frontier_wall_segment"));
        Assert.False(f.Economy.FrontierWallFacing(0, rivalCiv));
        f.Economy.FixtureFaceFrontier(0, rivalCiv);
        Assert.True(f.Economy.FrontierWallFacing(0, rivalCiv));
        Assert.False(f.Economy.FrontierWallFacing(0, rivalCiv + 1), "a segment faces one neighbour, not the whole realm");
        Assert.Equal(-1, f.Economy.FixtureNextFrontierFacing(0)); // the one neighbour is already faced
        // A Bronze economy never reports any of it.
        BronzeFixture bronze = Bronze("KEY-4");
        Assert.False(bronze.Economy.FrontierWallFacing(0, rivalCiv));
        Assert.False(bronze.Economy.HasGatehouse(0));
    }

    [Fact]
    public void A_walled_town_builds_its_gatehouse_before_the_other_Iron_works_it_wants()
    {
        BronzeFixture f = Iron("KEY-4", "DEF-06", "MEC-04");
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureAddPeople(0, 40, 25);
        f.Economy.FixtureBuildStructure(0, "stone_walls");
        // The gatehouse is later in the order than the windlass well; the town still takes it first.
        Assert.True(f.Economy.FixtureIronBuildWanted(0, "windlass_well"));
        Assert.True(f.Economy.FixtureIronBuildWanted(0, "gatehouse"));
        f.RunSeasons(2);
        Assert.Equal("gatehouse", LivingEconomyModel.IronWorkId(f.Economy.FixtureIronBuildKind(0)));
    }

    [Fact]
    public void A_castle_needs_walls_and_a_border_and_anchors_a_claim_only_while_garrisoned()
    {
        BronzeFixture f = Iron("KEY-4", "DEF-06", "DEF-08", "WAR-07");
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureAddPeople(0, 80, 25);
        Assert.False(f.Economy.FixtureIronBuildWanted(0, "castle")); // no wall, no neighbour
        f.Economy.FixtureBuildStructure(0, "stone_walls");
        Assert.False(f.Economy.FixtureIronBuildWanted(0, "castle")); // no border
        int rival = -1;
        for (int i = 1; i < f.Economy.FixtureSettlementCount; i++) if (f.Economy.FixtureCivOf(i) != f.Economy.FixtureCivOf(0)) { rival = i; break; }
        f.Economy.FixtureMoveSite(rival, f.Economy.SettlementLite(0).SiteTile + 4);
        Assert.True(f.Economy.FixtureIronBuildWanted(0, "castle"));
        // The border is where two claims meet: twelve tiles (two Iron claim radii and a gap of two) still counts, thirteen does not.
        f.Economy.FixtureMoveSite(rival, f.Economy.SettlementLite(0).SiteTile + 12);
        Assert.True(f.Economy.FixtureIronBuildWanted(0, "castle"));
        f.Economy.FixtureMoveSite(rival, f.Economy.SettlementLite(0).SiteTile + 13);
        Assert.False(f.Economy.FixtureIronBuildWanted(0, "castle"));
        f.Economy.FixtureMoveSite(rival, f.Economy.SettlementLite(0).SiteTile + 4);
        Fix64 before = f.Economy.ClaimMultiplier(0);
        f.Economy.FixtureBuildIronStructure(0, "castle");
        Assert.False(f.Economy.CastleGarrisoned(0)); // a keep with no soldiers holds nothing
        Assert.Equal(before, f.Economy.ClaimMultiplier(0));
        f.RunSeasons(4);
        if (f.Economy.CastleGarrisoned(0))
            Assert.True(f.Economy.ClaimMultiplier(0) > before, "a garrisoned castle is a stronger claim");
        BronzeFixture bronze = Bronze("KEY-4");
        Assert.False(bronze.Economy.CastleGarrisoned(0));
    }

    [Fact]
    public void Engine_fire_takes_two_seasons_for_one_engine_against_a_bare_wall_and_half_again_against_towers()
    {
        // One engine, bare wall: 1.0 -> 0.5 -> 0: breach on the second season.
        Fix64 integrity = Sieges.Batter(Fix64.One, 1, Fix64.FromInt(3));
        Assert.Equal(Fix64.Half, integrity);
        Assert.Equal(Fix64.Zero, Sieges.Batter(integrity, 1, Fix64.FromInt(3)));
        // Two engines breach it in one; no engine does nothing.
        Assert.Equal(Fix64.Zero, Sieges.Batter(Fix64.One, 2, Fix64.FromInt(3)));
        Assert.Equal(Fix64.One, Sieges.Batter(Fix64.One, 0, Fix64.FromInt(3)));
        // Towers (4.5) make the same engine take 3 seasons: 1 - 1/3 each.
        Fix64 stepped = Fix64.One;
        int seasons = 0;
        while (stepped > Fix64.Zero && seasons < 10) { stepped = Sieges.Batter(stepped, 1, Defenses.IntactTowerWall); seasons++; }
        Assert.Equal(3, seasons);
        // Never below zero.
        Assert.Equal(Fix64.Zero, Sieges.Batter(Fix64.FromRatio(1, 10), 3, Fix64.FromInt(3)));
    }

    [Fact]
    public void A_professional_army_keeps_a_siege_train_and_its_engines_are_taken_returned_and_fed()
    {
        BronzeFixture f = Iron("KEY-4", "WAR-07", "WAR-09", "MET-09");
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureStock(0, "G-ENGINE", Fix64.FromInt(3));
        Assert.Equal(2, f.Economy.TakeEngines(0, 2));
        Assert.Equal(Fix64.One, f.Economy.GoodStock(0, "G-ENGINE"));
        Assert.Equal(1, f.Economy.TakeEngines(0, 5)); // only whole engines in store leave
        f.Economy.ReturnEngines(0, 2);
        Assert.Equal(Fix64.FromInt(2), f.Economy.GoodStock(0, "G-ENGINE"));
        // Charcoal feeds them: 0.2 each a season; none in store, none run.
        f.Economy.FixtureStock(0, "G-CHAR", Fix64.Zero);
        Assert.Equal(0, f.Economy.PayEngineUpkeep(0, 2));
        f.Economy.FixtureStock(0, "G-CHAR", Fix64.FromRatio(3, 10));
        Assert.Equal(1, f.Economy.PayEngineUpkeep(0, 2));
        Assert.True(f.Economy.GoodStock(0, "G-CHAR") < Fix64.FromRatio(3, 10));
        // Bronze: no engines exist at all.
        BronzeFixture bronze = Bronze("KEY-4");
        Assert.Equal(0, bronze.Economy.TakeEngines(0, 3));
    }

    [Fact]
    public void The_view_sees_a_forge_idle_without_iron_and_at_work_with_iron_to_forge_for_adults_lacking_kits()
    {
        BronzeFixture f = Iron("KEY-4", "MET-09");
        f.Economy.FixtureBuild(0, "forge");
        f.Economy.FixtureStock(0, "G-WROUGHT", Fix64.Zero);
        Assert.Equal(1, f.Economy.ForgeStateAt(0)); // a forge, nothing to forge
        f.Economy.FixtureStock(0, "G-WROUGHT", Fix64.FromInt(3));
        Assert.Equal(2, f.Economy.ForgeStateAt(0)); // iron in store and adults without iron kits
        BronzeFixture bronze = Bronze("KEY-4", "MET-09");
        bronze.Economy.FixtureBuild(0, "forge");
        Assert.Equal(0, bronze.Economy.ForgeStateAt(0)); // a Bronze economy draws no Iron forge state
    }

    [Fact]
    public void The_view_gets_a_towns_terrace_tiles_and_the_civilizations_its_frontier_segments_face()
    {
        BronzeFixture iron = TerraceTown(ironChain: true), bronze = TerraceTown(ironChain: false);
        var none = new List<int>();
        iron.Economy.IronTerraceTilesForView(0, none);
        Assert.Empty(none);
        iron.RunSeasons(10);
        var tiles = new List<int>();
        iron.Economy.IronTerraceTilesForView(0, tiles);
        Assert.Equal(new[] { iron.TerraceTile }, tiles);
        var bronzeTiles = new List<int>();
        bronze.RunSeasons(10);
        bronze.Economy.IronTerraceTilesForView(0, bronzeTiles);
        Assert.Empty(bronzeTiles);

        Assert.Equal(0, iron.Economy.FrontierFacingMaskAt(0));
        iron.Economy.FixtureFaceFrontier(0, 2);
        iron.Economy.FixtureFaceFrontier(0, 5);
        Assert.Equal((1 << 2) | (1 << 5), iron.Economy.FrontierFacingMaskAt(0));
        Assert.Equal(0, bronze.Economy.FrontierFacingMaskAt(0));
    }

    [Fact]
    public void The_view_reads_a_towns_province_seat_the_way_the_rules_do_and_Bronze_has_none()
    {
        (BronzeFixture f, int far) = WithFarProvince("KEY-4", "GOV-07", "TER-08");
        Assert.Equal(f.Economy.FixtureGovernorSeat(far), f.Economy.ProvinceSeatAt(far));
        Assert.Equal(f.Economy.FixtureGovernorSeat(0), f.Economy.ProvinceSeatAt(0)); // the capital is governed directly
        BronzeFixture bronze = Bronze("KEY-4", "TER-08");
        Assert.Equal(-1, bronze.Economy.ProvinceSeatAt(0));
    }

    [Fact]
    public void The_view_gets_the_road_tiles_once_the_road_stands_with_or_without_its_bridge()
    {
        (BronzeFixture f, int far) = WithFarProvince("KEY-4", "TRN-07", "TRN-08");
        var tiles = new List<int>();
        Assert.False(f.Economy.RoadTilesForView(far, tiles, out _)); // no road yet
        f.Economy.FixtureBuildIronStructure(far, "road");
        Assert.True(f.Economy.RoadTilesForView(far, tiles, out bool bridged));
        Assert.False(bridged);
        Assert.Equal(f.Economy.SettlementLite(0).SiteTile, tiles[0]);
        Assert.Equal(f.Economy.SettlementLite(far).SiteTile, tiles[^1]);
        f.Economy.FixtureBuildIronStructure(far, "bridge");
        Assert.True(f.Economy.RoadTilesForView(far, new List<int>(), out bool nowBridged));
        Assert.True(nowBridged || f.Economy.FixtureRiversOnLink(far) == 0);
        Assert.False(f.Economy.RoadTilesForView(0, new List<int>(), out _)); // the capital has no road to itself
        BronzeFixture bronze = Bronze("KEY-4", "TRN-07");
        Assert.False(bronze.Economy.RoadTilesForView(0, new List<int>(), out _)); // Bronze draws no roads
    }

    [Fact]
    public void A_complete_paved_link_lends_its_town_claim_tiles_along_the_road_only_with_TRN_07()
    {
        (BronzeFixture f, int far) = WithFarProvince("KEY-4", "TRN-07", "TRN-08");
        var tiles = new List<int>();
        f.Economy.RoadClaimTiles(far, tiles);
        Assert.Empty(tiles); // no road yet
        f.Economy.FixtureBuildIronStructure(far, "road");
        f.Economy.FixtureBuildIronStructure(far, "bridge"); // a link over a river is incomplete without one
        f.Economy.RoadClaimTiles(far, tiles);
        Assert.NotEmpty(tiles);
        Assert.Equal(f.Economy.SettlementLite(0).SiteTile, tiles[0]);
        Assert.Equal(f.Economy.SettlementLite(far).SiteTile, tiles[^1]);
        Assert.Empty(RoadTilesWithoutTrn07());
        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, Seed, 60, true, true, true, true, false, "KEY-4", "TRN-07");
        var none = new List<int>();
        bronze.Economy.RoadClaimTiles(0, none);
        Assert.Empty(none);
    }

    private static List<int> RoadTilesWithoutTrn07()
    {
        // The same link in an economy that does not know the road research: no claim bonus.
        (BronzeFixture plain, int plainFar) = WithFarProvince("KEY-4", "TRN-08");
        plain.Economy.FixtureBuildIronStructure(plainFar, "road");
        plain.Economy.FixtureBuildIronStructure(plainFar, "bridge");
        var tiles = new List<int>();
        plain.Economy.RoadClaimTiles(plainFar, tiles);
        return tiles;
    }

    [Fact]
    public void Roads_and_relays_shorten_the_adoption_lag_of_a_linked_town_by_two_and_four()
    {
        (BronzeFixture none, int farNone) = WithFarProvince("KEY-4", "TRN-07", "COM-08");
        Assert.Equal(1, none.Economy.RoadAdoptionDivisor(farNone)); // no road built
        (BronzeFixture roads, int farRoads) = WithFarProvince("KEY-4", "TRN-07", "TRN-08");
        roads.Economy.FixtureBuildIronStructure(farRoads, "road");
        roads.Economy.FixtureBuildIronStructure(farRoads, "bridge");
        Assert.Equal(2, roads.Economy.RoadAdoptionDivisor(farRoads));
        (BronzeFixture relays, int farRelays) = WithFarProvince("KEY-4", "TRN-07", "TRN-08", "COM-08");
        relays.Economy.FixtureBuildIronStructure(farRelays, "road");
        relays.Economy.FixtureBuildIronStructure(farRelays, "bridge");
        Assert.Equal(4, relays.Economy.RoadAdoptionDivisor(farRelays));
        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, Seed, 60, true, true, true, true, false, "KEY-4", "TRN-07", "COM-08");
        Assert.Equal(1, bronze.Economy.RoadAdoptionDivisor(0));
    }

    [Fact]
    public void A_theatre_takes_a_tenth_off_the_distance_and_span_unrest_of_its_town()
    {
        (BronzeFixture plain, int farPlain) = WithFarProvince("KEY-4", "ART-08");
        (BronzeFixture venue, int farVenue) = WithFarProvince("KEY-4", "ART-08");
        venue.Economy.FixtureBuildIronStructure(farVenue, "theatre_arena");
        foreach ((BronzeFixture f, int far) in new[] { (plain, farPlain), (venue, farVenue) }) { f.Economy.FixtureSetTier(0, 1); f.Economy.FixtureSetTier(far, 1); }
        (int overPlain, int spanPlain) = plain.Economy.CohesionPressures(farPlain);
        (int overVenue, int spanVenue) = venue.Economy.CohesionPressures(farVenue);
        Assert.True(overPlain > 0);
        Assert.True(overVenue < overPlain && overVenue >= overPlain * 8 / 10 - 1, $"{overPlain} -> {overVenue}");
        Assert.True(spanVenue <= spanPlain);
    }

    [Fact]
    public void A_province_under_a_feudal_contract_defends_fifteen_percent_better()
    {
        var plain = new Defenses(false, false, false, false, false, false, false, false, false, false, StoneWalls: true, BronzeRules: true);
        Assert.Equal(plain.Multiplier() * Fix64.FromRatio(115, 100), (plain with { Feudal = true }).Multiplier());
        Assert.Equal(plain.Multiplier(), (plain with { Feudal = false }).Multiplier());
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
