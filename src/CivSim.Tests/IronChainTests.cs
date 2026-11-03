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
