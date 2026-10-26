using System;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>
/// The Bronze Age metal and fuel economy (WP6, docs/MVP/22 §5, 23 §4–§7): recipes, deposits, camps, kits, dormancy,
/// forests, determinism and snapshot fidelity, and the B12/B13 economy fixtures.
/// </summary>
public class MetalEconomyTests
{
    private const ulong Seed = 20261019;
    private static Fix64 F(string text) => Fix64.Parse(text);

    /// <summary>Equal to a millionth (Q32.32 products round in the last bits).</summary>
    private static void Near(Fix64 expected, Fix64 actual)
        => Assert.True(Fix64.Abs(expected - actual) <= Fix64.FromRaw(4295), $"expected {expected}, got {actual}");
    private static BronzeFixture Fixture(int people = 60, bool coppice = false, bool tin = true, bool arsenic = true, bool shaft = true)
        => BronzeFixture.Create(TestPack.Pack, Seed, people, coppice, tin, arsenic, shaft, "WAR-05");

    // ---- recipes -------------------------------------------------------------------------------

    [Fact]
    public void Recipes_run_in_ascending_order_then_id()
    {
        var content = EconomyContent.FromPack(TestPack.Pack);
        ReadOnlySpan<EconomyRecipe> recipes = content.Recipes;
        Assert.True(recipes.Length >= 25);
        for (int i = 1; i < recipes.Length; i++)
        {
            EconomyRecipe a = recipes[i - 1], b = recipes[i];
            Assert.True(a.Order < b.Order || (a.Order == b.Order && string.CompareOrdinal(a.Id, b.Id) < 0), $"{a.Id} before {b.Id}");
        }
        Assert.Equal("burn_charcoal", recipes[0].Id);
    }

    [Fact]
    public void A_recipe_is_limited_by_its_scarcest_input_and_by_labor()
    {
        BronzeFixture f = Fixture();
        LivingEconomyModel e = f.Economy;
        e.FixtureStock(0, "G-CUORE", Fix64.FromInt(10));
        e.FixtureStock(0, "G-CHAR", Fix64.One);
        // Bellows at the smelter: 1.6 L of charcoal a batch, so one litre runs 0.625 batches whatever the ore.
        Fix64 made = e.FixtureRunRecipe(0, "smelt_copper", Fix64.FromInt(5), Fix64.FromInt(10));
        Near(F("0.625"), made);
        Near(F("0.4375"), e.GoodStock(0, "G-COPPER")); // 0.625 × 0.7 L
        Near(F("8.75"), e.GoodStock(0, "G-CUORE"));
        Near(Fix64.Zero, e.GoodStock(0, "G-CHAR"));
        // Labor binds next: 0.1 ws buys 0.2 batches at 0.5 ws each.
        e.FixtureStock(0, "G-CHAR", Fix64.FromInt(10));
        Near(F("0.2"), e.FixtureRunRecipe(0, "smelt_copper", Fix64.FromInt(5), F("0.1")));
    }

    [Fact]
    public void Tin_bronze_alloys_nine_parts_copper_to_one_of_tin()
    {
        BronzeFixture f = Fixture();
        LivingEconomyModel e = f.Economy;
        e.FixtureStock(0, "G-COPPER", F("0.9"));
        e.FixtureStock(0, "G-TIN", Fix64.One);
        e.FixtureStock(0, "G-CHAR", Fix64.One);
        Near(Fix64.One, e.FixtureRunRecipe(0, "alloy_bronze", Fix64.FromInt(3), Fix64.FromInt(3)));
        Near(Fix64.One, e.GoodStock(0, "G-BRONZE"));
        Near(F("0.9"), e.GoodStock(0, "G-TIN"));
        Near(Fix64.Zero, e.GoodStock(0, "G-COPPER"));
    }

    [Fact]
    public void No_smelting_without_charcoal()
    {
        BronzeFixture f = Fixture();
        f.Economy.FixtureStock(0, "G-CUORE", Fix64.FromInt(10));
        f.Economy.FixtureStock(0, "G-CHAR", -f.Economy.GoodStock(0, "G-CHAR"));
        Assert.Equal(Fix64.Zero, f.Economy.FixtureRunRecipe(0, "smelt_copper", Fix64.FromInt(5), Fix64.FromInt(10)));
        Assert.Equal(Fix64.Zero, f.Economy.FixtureRunRecipe(0, "smelt_tin", Fix64.FromInt(5), Fix64.FromInt(10)));
    }

    // ---- deposits and camps --------------------------------------------------------------------

    [Fact]
    public void Deposits_hold_litres_by_kind_tier_and_depth()
    {
        Assert.Equal(Fix64.FromInt(100), LivingEconomyModel.FixtureOreLitres(new WorldDeposit(DepositKind.Tin, 1, 1)));
        Assert.Equal(Fix64.FromInt(150), LivingEconomyModel.FixtureOreLitres(new WorldDeposit(DepositKind.Tin, 2, 1)));
        Assert.Equal(Fix64.FromInt(600), LivingEconomyModel.FixtureOreLitres(new WorldDeposit(DepositKind.Tin, 3, 2)));
        Assert.Equal(Fix64.FromInt(300), LivingEconomyModel.FixtureOreLitres(new WorldDeposit(DepositKind.CopperOre, 2, 1)));
        Assert.Equal(Fix64.FromInt(900), LivingEconomyModel.FixtureOreLitres(new WorldDeposit(DepositKind.CopperOre, 1, 2)));
        Assert.Equal(Fix64.FromInt(800), LivingEconomyModel.FixtureOreLitres(new WorldDeposit(DepositKind.ArsenicalCopper, 3, 2)));
    }

    [Fact]
    public void Depth_two_needs_shaft_mining()
    {
        BronzeFixture without = Fixture(shaft: false);
        Assert.False(without.Economy.FixtureWorkable(0, without.ArsenicTile));
        Assert.True(without.Economy.FixtureWorkable(0, without.CopperTile));
        BronzeFixture with = Fixture(shaft: true);
        Assert.True(with.Economy.FixtureWorkable(0, with.ArsenicTile));
    }

    [Fact]
    public void Mining_depletes_the_deposit_by_what_is_dug()
    {
        BronzeFixture f = Fixture();
        Fix64 before = f.Economy.OreRemaining(f.CopperTile);
        f.RunSeasons(8);
        MetalStatistics stats = f.Economy.MetalStats(0);
        Fix64 dug = before - f.Economy.OreRemaining(f.CopperTile);
        Assert.True(dug > Fix64.Zero);
        Assert.True(stats.OreMined >= dug);
    }

    [Fact]
    public void Last_season_gross_metal_output_rates_match_recipe_stats_and_survive_restore()
    {
        BronzeFixture f = Fixture();
        MetalStatistics before = f.Economy.MetalStats(0);
        f.RunSeasons(1);
        MetalStatistics after = f.Economy.MetalStats(0);
        MetalOutputRates rates = f.Economy.MetalOutputRate(0);
        Fix64 x4 = Fix64.FromInt(4);
        Assert.Equal((after.Copper - before.Copper) * x4, rates.Copper);
        Assert.Equal((after.Tin - before.Tin) * x4, rates.Tin);
        Assert.Equal((after.Bronze - before.Bronze) * x4, rates.Bronze);
        Assert.Equal((after.ArsenicalBronze - before.ArsenicalBronze) * x4, rates.ArsenicalBronze);
        Assert.Equal((after.Charcoal - before.Charcoal) * x4, rates.Charcoal);
        Assert.Equal((after.Kits8 - before.Kits8) * x4, rates.Kits8);
        Assert.Equal((after.Kits9 - before.Kits9) * x4, rates.Kits9);
        Assert.Equal((after.Kits10 - before.Kits10) * x4, rates.Kits10);
        Assert.True(rates.Charcoal > Fix64.Zero || rates.Copper > Fix64.Zero || rates.Tin > Fix64.Zero,
            "the fixture should exercise gross recipe production");

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) f.Economy.WriteSnapshot(writer);
        stream.Position = 0;
        BronzeFixture restored = Fixture();
        using (var reader = new BinaryReader(stream)) restored.Economy.ReadSnapshot(reader);
        Assert.Equal(rates, restored.Economy.MetalOutputRate(0));
        Assert.Equal(f.Economy.ComputeChecksum(), restored.Economy.ComputeChecksum());
        for (int i = 0; i < LivingEconomyModel.SeasonTicks; i++)
        {
            f.Economy.Step(f.Economy.Tick, ReadOnlySpan<Command>.Empty);
            restored.Economy.Step(restored.Economy.Tick, ReadOnlySpan<Command>.Empty);
        }
        Assert.Equal(f.Economy.MetalOutputRate(0), restored.Economy.MetalOutputRate(0));
        Assert.Equal(f.Economy.ComputeChecksum(), restored.Economy.ComputeChecksum());
    }

    [Fact]
    public void A_mining_camp_takes_its_crew_and_closes_when_the_deposit_is_exhausted()
    {
        BronzeFixture f = Fixture(people: 100);
        Assert.True(f.CampTile >= 0);
        LivingEconomyModel e = f.Economy;
        Assert.Equal(MiningCampResult.InRange, e.CheckOpenMiningCamp(0, f.CopperTile));
        Assert.Equal(MiningCampResult.Opened, e.CheckOpenMiningCamp(0, f.CampTile));
        Assert.Equal(MiningCampResult.InRange, e.OpenMiningCamp(0, f.CopperTile));
        Assert.Equal(MiningCampResult.Opened, e.OpenMiningCamp(0, f.CampTile));
        Assert.Equal(MiningCampResult.AlreadyOpen, e.CheckOpenMiningCamp(0, f.CampTile));
        Assert.Equal(MiningCampResult.AlreadyOpen, e.OpenMiningCamp(0, f.CampTile));
        Assert.Equal(1, e.MiningCampCount);
        MiningCampView camp = e.MiningCamp(0);
        Assert.Equal(6, camp.Crew); // 6 miners with MIN-04's mining_camp_crew
        Assert.Equal(1, camp.ClaimRadius);
        Assert.Equal(Fix64.Sqrt(Fix64.FromInt(6)) * F("0.75"), camp.ClaimEmission);
        f.RunSeasons(2);
        Assert.True(e.MiningCamp(0).Built);
        e.FixtureExhaust(f.CampTile);
        f.RunSeasons(1);
        Assert.Equal(0, e.MiningCampCount);
        for (int i = 0; i < e.FigureCount; i++) Assert.Equal(-1, e.FixtureCampOf(i));

        BronzeFixture small = Fixture(people: 100);
        small.Economy.FixtureAddMiningCamp(0, small.TinTile, crew: 100);
        Assert.Equal(MiningCampResult.NoCrew, small.Economy.CheckOpenMiningCamp(0, small.CampTile));
    }

    [Fact]
    public void A_camp_on_a_deep_deposit_needs_shaft_mining()
    {
        BronzeFixture f = Fixture(people: 100, shaft: false);
        // Without MIN-04 nothing is workable at depth 2; a surface deposit beyond range opens as usual.
        Assert.Equal(MiningCampResult.Opened, f.Economy.OpenMiningCamp(0, f.CampTile));
        Assert.Equal(MiningCampResult.InRange, f.Economy.OpenMiningCamp(0, f.ArsenicTile));
    }

    // ---- kits ----------------------------------------------------------------------------------

    [Fact]
    public void Kit_quality_falls_from_new_to_replacement_and_breaks_in_the_dormancy_tail()
    {
        BronzeFixture f = Fixture();
        LivingEconomyModel e = f.Economy;
        int adult = -1;
        for (int i = 0; i < e.FigureCount; i++) if (e.Figure(i).Age >= 14) { adult = i; break; }
        e.FixtureSetKit(adult, 10, Fix64.FromInt(24));
        Near(F("1.2"), e.FixtureKitQuality(adult));
        e.FixtureSetKit(adult, 10, Fix64.Zero);
        Near(F("0.7"), e.FixtureKitQuality(adult));
        // Worked on past replacement to quality 0.5: 0.4 of the service life more (×1.4 in all, 23 §4.1).
        Near(F("-9.6"), e.FixtureKitBreakLeft(10));
        Near(F("-4.8"), e.FixtureKitBreakLeft(8));
        e.FixtureSetKit(adult, 10, F("-9.6"));
        Near(F("0.5"), e.FixtureKitQuality(adult));
    }

    [Fact]
    public void An_early_upgrade_remelts_half_of_the_old_kit_as_copper()
    {
        BronzeFixture f = Fixture();
        LivingEconomyModel e = f.Economy;
        for (int i = 0; i < e.FigureCount; i++) if (e.Figure(i).Age >= 14) e.FixtureSetKit(i, 8, Fix64.FromInt(12));
        e.FixtureStock(0, "G-TOOL10", Fix64.FromInt(3));
        f.RunSeasons(1);
        Fix64 remelted = e.MetalStats(0).RemeltCopper;
        Assert.True(remelted >= F("0.18"), $"remelted {remelted}"); // at least the three stocked kits, 0.06 L each
        long kits = (remelted / F("0.06")).RoundToInt();
        Near(F("0.06") * (int)kits, remelted);
    }

    [Fact]
    public void Bronze_covers_most_tool_users_soon_after_tin_bronze_and_lasts()
    {
        // T18 (docs/MVP/22 §15): about 70% of tool users on rung 6 within 12 seasons where access holds (07d §5).
        BronzeFixture f = Fixture(people: 100);
        f.RunSeasons(12);
        Assert.True(f.BronzeShare() >= F("0.6"), $"rung 6 at 12 seasons: {f.BronzeShare()}");
        f.RunSeasons(4);
        Assert.True(f.BronzeShare() >= F("0.7"), $"rung 6 at 16 seasons: {f.BronzeShare()}");
        MetalReadout m = f.Economy.Metal(0);
        Assert.Equal(10, m.CastTier);
        Assert.Equal(6, f.Economy.MethodRung(0, EconomyJob.Chop));
    }

    [Fact]
    public void Losing_tin_is_a_slow_squeeze_with_a_no_tin_reason()
    {
        // B12 (07d §5): the reason shows at once, kits keep working, figures fall back on replacement, never a cliff.
        BronzeFixture f = Fixture(people: 100, arsenic: false);
        f.RunSeasons(24);
        int before = f.Economy.Metal(0).Kits10;
        Assert.True(before > 20);
        f.Economy.FixtureExhaust(f.TinTile);
        foreach (string g in new[] { "G-TIN", "G-TINORE", "G-BRONZE", "G-TOOL10" })
            f.Economy.FixtureStock(0, g, -f.Economy.GoodStock(0, g));
        f.RunSeasons(1);
        MetalReadout now = f.Economy.Metal(0);
        Assert.Equal(MethodFallbackReason.NoTin, now.Reason);
        Assert.Equal(8, now.CastTier);
        Assert.True(now.Kits10 * 10 >= before * 9, $"no cliff: {now.Kits10} of {before}");
        f.RunSeasons(12);
        MetalReadout twelveSeasons = f.Economy.Metal(0);
        Assert.Equal(MethodFallbackReason.NoTin, twelveSeasons.Reason);
        Assert.True(twelveSeasons.Kits10 * 10 <= twelveSeasons.Adults * 9,
            $"at least 10% of tool users should have fallen back by 12 seasons: {twelveSeasons.Kits10}/{twelveSeasons.Adults}");
        f.RunSeasons(23);
        MetalReadout later = f.Economy.Metal(0);
        Assert.True(later.Kits10 < before, "kits wear out");
        Assert.True(later.Kits8 > 0, "replacements are copper");
        Assert.True(later.Kits10 > 0, "the squeeze is slow");
        MethodReadout readout = f.Economy.MethodReadout(0, EconomyJob.Chop);
        Assert.Equal(6, readout.KnownRung);
    }

    // ---- forests -------------------------------------------------------------------------------

    [Fact]
    public void A_cut_forest_clears_turns_to_grass_and_regrows_doubled_with_coppicing()
    {
        BronzeFixture plain = Fixture(coppice: false), coppiced = Fixture(coppice: true);
        int tile = plain.ForestTiles[0];
        Assert.Equal(tile, coppiced.ForestTiles[0]);
        for (int s = 0; s < 4; s++)
        {
            plain.Economy.FixtureSetWood(tile, Fix64.Zero);
            plain.RunSeasons(1);
        }
        Assert.Equal(2, plain.Economy.ForestState(tile));
        Assert.Equal(0, plain.Economy.ForestBand(tile));
        plain.Economy.FixtureSetWood(tile, Fix64.FromInt(10));
        coppiced.Economy.FixtureSetWood(tile, Fix64.FromInt(10));
        plain.RunSeasons(1);
        coppiced.RunSeasons(1);
        Fix64 plainGrowth = plain.Economy.StandingWood(tile) - Fix64.FromInt(10);
        Fix64 coppicedGrowth = coppiced.Economy.StandingWood(tile) - Fix64.FromInt(10);
        // Woodcutters may take a little from the stand too; coppicing still regrows it at twice the rate.
        Assert.True(coppicedGrowth > plainGrowth, $"{coppicedGrowth} vs {plainGrowth}");
        plain.Economy.FixtureSetWood(tile, Fix64.FromInt(16));
        plain.RunSeasons(1);
        Assert.Equal(0, plain.Economy.ForestState(tile));
    }

    [Fact]
    public void Smelting_towns_draw_down_their_forest_and_coppicing_keeps_it()
    {
        // B13 / T19 on the fixture (07d §5, measured in docs/MVP/07d "WP6 live results").
        BronzeFixture plain = Fixture(people: 100, coppice: false), coppiced = Fixture(people: 100, coppice: true);
        Fix64 start = plain.StandingWood();
        plain.RunSeasons(52);
        coppiced.RunSeasons(52);
        Fix64 drawnPlain = Fix64.One - plain.StandingWood() / start;
        Fix64 drawnCoppiced = Fix64.One - coppiced.StandingWood() / start;
        Assert.True(drawnPlain > drawnCoppiced, $"{drawnPlain} vs {drawnCoppiced}");
        Assert.True(drawnCoppiced < F("0.05"), $"coppiced drawdown {drawnCoppiced}");
        Assert.True(drawnPlain > F("0.08"), $"uncoppiced drawdown {drawnPlain}");
    }

    // ---- determinism and snapshots --------------------------------------------------------------

    [Fact]
    public void Two_fixture_runs_are_identical_and_a_snapshot_round_trip_is_exact()
    {
        BronzeFixture a = Fixture(), b = Fixture();
        a.RunSeasons(10);
        b.RunSeasons(10);
        Assert.Equal(a.Economy.ComputeChecksum(), b.Economy.ComputeChecksum());
        foreach (int extra in new[] { 37, 150, 299 })
        {
            for (long t = 0; t < extra; t++) { a.Economy.Step(a.Economy.Tick, ReadOnlySpan<Command>.Empty); }
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) a.Economy.WriteSnapshot(w);
            stream.Position = 0;
            BronzeFixture restored = Fixture();
            using (var r = new BinaryReader(stream)) restored.Economy.ReadSnapshot(r);
            Assert.Equal(a.Economy.ComputeChecksum(), restored.Economy.ComputeChecksum());
            for (int t = 0; t < 300; t++)
            {
                a.Economy.Step(a.Economy.Tick, ReadOnlySpan<Command>.Empty);
                restored.Economy.Step(restored.Economy.Tick, ReadOnlySpan<Command>.Empty);
            }
            Assert.Equal(a.Economy.ComputeChecksum(), restored.Economy.ComputeChecksum());
        }
    }

    [Fact]
    public void A_restored_match_runs_on_exactly_as_the_live_one_before_and_after_bronze()
    {
        // The snapshot carries research's season caches (Insight, people, practice), so restoring mid-season after a
        // fission or a politics move no longer re-derives them from the economy as it is now (docs/MVP/15 D9).
        var archetypes = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        MatchModel live = MatchModel.Create(Seed, TestPack.Pack, FogFairnessHarness.World, archetypes);
        var runner = new SimRunner(live);
        foreach (long at in new[] { 4_537L, 9_001L, 27_000L + 37 })
        {
            runner.Advance(at - runner.Tick);
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) live.WriteSnapshot(w);
            stream.Position = 0;
            MatchModel copy = MatchModel.Create(Seed, TestPack.Pack, FogFairnessHarness.World, archetypes);
            using (var r = new BinaryReader(stream)) copy.ReadSnapshot(r);
            Assert.Equal(live.ComputeChecksum(), copy.ComputeChecksum());
            var copyRunner = new SimRunner(copy, SimRunner.DefaultChecksumInterval, at);
            for (int step = 0; step < 4; step++)
            {
                runner.Advance(150);
                copyRunner.Advance(150);
                Assert.Equal(live.ComputeChecksum(), copy.ComputeChecksum());
                Assert.Equal(live.Research.InsightPerYear(0), copy.Research.InsightPerYear(0));
            }
        }
        Assert.True(live.Economy.BronzeActive, "the last restore is in the Bronze Age");
    }
}
