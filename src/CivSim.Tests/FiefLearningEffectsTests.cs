using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>
/// The WP-E5-6/7 effects that were typed in the catalog but unread by the simulation: ART-09 manuscripts and monastery Insight, CER-10 spectacles and their holder gate,
/// NUM-08/TRD-08/GOV-10 trade volume, NUM-08 admin capacity and MED-07's elder hazard. Draft values, Fief only.
/// </summary>
public sealed class FiefLearningEffectsTests
{
    private const ulong Seed = 20261114;

    private static BronzeFixture Fief(string[] numeric, params string[] projects)
        => BronzeFixture.CreateFiefWithCamp(TestPack.WithNumeric(numeric), Seed, 100, projects);

    private static void AssertNear(Fix64 expected, Fix64 actual, long raw = 8)
        => Assert.InRange(actual.Raw, expected.Raw - raw, expected.Raw + raw);

    // ---- ART-09 -----------------------------------------------------------------------------------------------------

    [Fact]
    public void ART_09_copies_a_manuscript_only_at_a_staffed_monastery_and_consumes_paper_pigment_and_gold()
    {
        BronzeFixture f = Fief(new[] { "BEL-10", "ART-09" }, "KEY-5", "BEL-10", "ART-09");
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureSetGoodStock(0, "G-PAPER", Fix64.FromInt(5));
        f.Economy.FixtureSetGoodStock(0, "G-PIGMENT", Fix64.One);
        f.Economy.FixtureSetGoodStock(0, "G-GOLD", Fix64.One);
        Assert.Equal(Fix64.Zero, f.Economy.FixtureRunRecipe(0, "copy_manuscript", Fix64.One, Fix64.FromInt(6)));

        f.Economy.FixtureBuildIronStructure(0, "monastery");
        Assert.Equal(Fix64.Zero, f.Economy.FixtureRunRecipe(0, "copy_manuscript", Fix64.One, Fix64.FromInt(6)));
        f.RunSeasons(1);
        Assert.True(f.Economy.FixtureMonasteryStaffed(0));

        Fix64 paper = f.Economy.FixtureGoodStock(0, "G-PAPER"), pigment = f.Economy.FixtureGoodStock(0, "G-PIGMENT"), gold = f.Economy.FixtureGoodStock(0, "G-GOLD");
        Fix64 before = f.Economy.FixtureGoodStock(0, "G-MANUSCRIPT");
        f.Economy.FixtureSetGoodStock(0, "G-PAPER", Fix64.FromInt(5));
        f.Economy.FixtureSetGoodStock(0, "G-PIGMENT", Fix64.One);
        f.Economy.FixtureSetGoodStock(0, "G-GOLD", Fix64.One);
        Assert.Equal(Fix64.One, f.Economy.FixtureRunRecipe(0, "copy_manuscript", Fix64.One, Fix64.FromInt(6)));
        Assert.Equal(before + Fix64.One, f.Economy.FixtureGoodStock(0, "G-MANUSCRIPT"));
        // Four paper, 0.3 pigment and 0.01 gold, each plus the draft 5% Fief craft-loss allowance.
        AssertNear(Fix64.FromInt(5) - Fix64.Parse("4.2"), f.Economy.FixtureGoodStock(0, "G-PAPER"), 16);
        AssertNear(Fix64.One - Fix64.Parse("0.315"), f.Economy.FixtureGoodStock(0, "G-PIGMENT"), 16);
        AssertNear(Fix64.One - Fix64.Parse("0.0105"), f.Economy.FixtureGoodStock(0, "G-GOLD"), 16);
        Assert.True(paper >= Fix64.Zero && pigment >= Fix64.Zero && gold >= Fix64.Zero);
    }

    [Fact]
    public void A_manuscript_is_a_luxury_item_in_Fief_and_the_recipe_does_not_exist_in_Iron()
    {
        BronzeFixture f = Fief(new[] { "BEL-10", "ART-09" }, "KEY-5", "BEL-10", "ART-09");
        int before = f.Economy.FixtureLuxuryItems(0);
        f.Economy.FixtureSetGoodStock(0, "G-MANUSCRIPT", Fix64.FromInt(3));
        Assert.Equal(before + 3, f.Economy.FixtureLuxuryItems(0));

        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.WithNumeric("BEL-10", "ART-09"), Seed, 100, "KEY-5", "BEL-10", "ART-09");
        iron.Economy.FixtureSetTier(0, 1);
        iron.Economy.FixtureBuildIronStructure(0, "monastery");
        iron.RunSeasons(1);
        Assert.Equal(Fix64.Zero, iron.Economy.FixtureGoodStock(0, "G-MANUSCRIPT"));
        Assert.Throws<ArgumentException>(() => iron.Economy.FixtureRunRecipe(0, "copy_manuscript", Fix64.One, Fix64.FromInt(6)));
    }

    [Fact]
    public void A_staffed_monastery_with_the_inputs_makes_manuscripts_in_play_and_stops_at_its_target()
    {
        BronzeFixture f = Fief(new[] { "BEL-10", "ART-09", "COM-09" }, "KEY-5", "BEL-10", "ART-09", "COM-09", "MET-07");
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureBuildIronStructure(0, "monastery");
        f.RunSeasons(1);
        for (int season = 0; season < 6; season++)
        {
            f.Economy.FixtureSetGoodStock(0, "G-PAPER", Fix64.FromInt(20));
            f.Economy.FixtureSetGoodStock(0, "G-PIGMENT", Fix64.FromInt(5));
            f.Economy.FixtureSetGoodStock(0, "G-GOLD", Fix64.FromInt(2));
            f.RunSeasons(1);
        }
        Fix64 made = f.Economy.FixtureGoodStock(0, "G-MANUSCRIPT");
        Assert.True(made > Fix64.Zero, "a staffed monastery with paper, pigment and gold should copy a manuscript");
        Assert.True(made <= Fix64.FromInt(2), "copying stops at two manuscripts per monastery");
    }

    [Fact]
    public void ART_09_multiplies_only_monk_Insight_and_only_in_Fief()
    {
        ResearchCatalog catalog = new(TestPack.WithNumeric("BEL-10", "ART-09"));
        ResearchRig plain = new(catalog: catalog, rules: ChapterRules.Fief);
        ResearchRig copied = new(catalog: catalog, rules: ChapterRules.Fief);
        plain.GrantAll(0, "KEY-5", "BEL-10");
        copied.GrantAll(0, "KEY-5", "BEL-10", "ART-09");
        plain.Run(ResearchSystem.SeasonTicks);
        copied.Run(ResearchSystem.SeasonTicks);
        Assert.True(copied.Research.InsightPerYear(0) > plain.Research.InsightPerYear(0));

        ResearchRig ironPlain = new(catalog: catalog, rules: ChapterRules.Iron);
        ResearchRig ironCopied = new(catalog: catalog, rules: ChapterRules.Iron);
        ironPlain.GrantAll(0, "KEY-4", "BEL-10");
        ironCopied.GrantAll(0, "KEY-4", "BEL-10", "ART-09");
        ironPlain.Run(ResearchSystem.SeasonTicks);
        ironCopied.Run(ResearchSystem.SeasonTicks);
        Assert.Equal(ironPlain.Research.InsightPerYear(0), ironCopied.Research.InsightPerYear(0));
    }

    // ---- CER-10 -----------------------------------------------------------------------------------------------------

    [Fact]
    public void CER_10_grinds_spectacles_from_blown_glass_and_wood_and_wears_them_out_slowly()
    {
        BronzeFixture f = Fief(new[] { "CER-10" }, "KEY-5", "CER-10");
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureSetGoodStock(0, "G-BLOWNGLASS", Fix64.One);
        f.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(5));
        Fix64 made = f.Economy.FixtureRunRecipe(0, "grind_spectacles", Fix64.One, Fix64.One);
        Assert.Equal(Fix64.One, made);
        Assert.Equal(1, f.Economy.SpectacleStockAt(0));
        AssertNear(Fix64.One - Fix64.Parse("0.21"), f.Economy.FixtureGoodStock(0, "G-BLOWNGLASS"), 16); // 0.2 plus the draft 5% loss

        f.Economy.FixtureSetGoodStock(0, "G-SPECTACLES", Fix64.FromInt(40));
        f.RunSeasons(1);
        Fix64 after = f.Economy.FixtureGoodStock(0, "G-SPECTACLES");
        Assert.True(after < Fix64.FromInt(40) && after > Fix64.FromInt(38), $"one season of wear should take about 1/80 of the stock, left {after}");
    }

    [Fact]
    public void Spectacle_holders_are_capped_by_elders_and_the_Insight_bonus_follows_the_holders()
    {
        BronzeFixture f = Fief(new[] { "CER-10" }, "KEY-5", "CER-10");
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureAgeResidents(0, 3, 60);
        Assert.True(f.Economy.FixtureElderCount(0) >= 3);
        f.Economy.FixtureSetGoodStock(0, "G-SPECTACLES", Fix64.FromInt(2));
        GeneratedWorld generated = WorldGenerator.Generate(Seed);
        ResearchCatalog catalog = TestPack.Catalog;
        LivingEconomySource source = new(f.Economy, WorldEconomyInput.FromGeneratedWorld(generated), generated, catalog, ReadOnlySpan<EconomyArchetype>.Empty);
        InsightInputs held = source.GetInsightInputs(0);
        Assert.True(held.SpectacleHolders > Fix64.Zero);
        Assert.True(held.SpectacleHolders <= Fix64.FromInt(2) + Fix64.Zero && held.SpectacleHolders <= held.Elders);

        f.Economy.FixtureSetGoodStock(0, "G-SPECTACLES", Fix64.Zero);
        Assert.Equal(Fix64.Zero, source.GetInsightInputs(0).SpectacleHolders);
    }

    [Fact]
    public void CER_10_adds_no_elder_Insight_without_spectacles_and_all_of_it_with_one_pair_per_elder()
    {
        ResearchCatalog catalog = new(TestPack.WithNumeric("CER-10"));
        static ResearchRig Rig(ResearchCatalog c, ChapterRules rules, Fix64 holders, params string[] granted)
        {
            var rig = new ResearchRig(catalog: c, rules: rules);
            rig.Source.SpectacleHolders = holders;
            rig.GrantAll(0, granted);
            rig.Run(ResearchSystem.SeasonTicks);
            return rig;
        }
        Fix64 plain = Rig(catalog, ChapterRules.Fief, Fix64.Zero, "KEY-5").Research.InsightPerYear(0);
        Fix64 noGlasses = Rig(catalog, ChapterRules.Fief, Fix64.Zero, "KEY-5", "CER-10").Research.InsightPerYear(0);
        Fix64 half = Rig(catalog, ChapterRules.Fief, Fix64.One, "KEY-5", "CER-10").Research.InsightPerYear(0);
        Fix64 all = Rig(catalog, ChapterRules.Fief, Fix64.FromInt(2), "KEY-5", "CER-10").Research.InsightPerYear(0);
        Assert.Equal(plain, noGlasses);
        Assert.True(half > noGlasses && all > half, $"plain {plain}, glasses 0/1/2 of 2 elders: {noGlasses} {half} {all}");

        // Outside Fief an E5 grant stays inert (the same isolation the COM-09 test checks).
        Fix64 ironPlain = Rig(catalog, ChapterRules.Iron, Fix64.Zero, "KEY-4").Research.InsightPerYear(0);
        Fix64 ironLens = Rig(catalog, ChapterRules.Iron, Fix64.Zero, "KEY-4", "CER-10").Research.InsightPerYear(0);
        Assert.Equal(ironPlain, ironLens);
    }

    // ---- NUM-08 / TRD-08 / GOV-10 ------------------------------------------------------------------------------------

    [Fact]
    public void Trade_volume_and_contract_factors_multiply_in_Fief_and_are_one_elsewhere()
    {
        BronzeFixture none = Fief(new[] { "NUM-08", "TRD-08", "GOV-10" }, "KEY-5");
        Assert.Equal(Fix64.One, none.Economy.FiefTradeVolumeFactor(0));
        BronzeFixture num = Fief(new[] { "NUM-08", "TRD-08", "GOV-10" }, "KEY-5", "NUM-08");
        Assert.Equal(Fix64.Parse("1.15"), num.Economy.FiefTradeVolumeFactor(0));
        BronzeFixture all = Fief(new[] { "NUM-08", "TRD-08", "GOV-10" }, "KEY-5", "NUM-08", "TRD-08", "GOV-10");
        AssertNear(Fix64.Parse("1.15") * Fix64.Parse("1.5") * Fix64.Parse("1.05"), all.Economy.FiefTradeVolumeFactor(0), 16);

        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.WithNumeric("NUM-08", "TRD-08", "GOV-10"), Seed, 100, "KEY-5", "NUM-08", "TRD-08", "GOV-10");
        Assert.Equal(Fix64.One, iron.Economy.FiefTradeVolumeFactor(0));
    }

    [Fact]
    public void A_caravan_carries_more_of_the_buyers_need_with_trade_volume_but_never_more_than_the_sellers_surplus()
    {
        static Fix64 Load(bool research, int surplus)
        {
            var pack = TestPack.WithNumeric("KEY-5", "NUM-08", "TRD-08");
            MatchModel match = MatchModel.Create(Seed, pack, WorldGenerator.Generate(Seed),
                new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced }, default, rules: ChapterRules.Fief);
            var runner = new SimRunner(match);
            foreach (string project in research ? new[] { "KEY-5", "NUM-08", "TRD-08" } : new[] { "KEY-5" })
            {
                runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, match.Research.Catalog.IndexOf(project), 0, 0));
                runner.Advance(2);
            }
            int seller = Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == 0);
            int buyer = Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == 1);
            match.Economy.FixtureSetGoodStock(seller, "G-SALT", Fix64.FromInt(surplus));
            match.Economy.FixtureSetGoodStock(buyer, "G-SALT", Fix64.Zero);
            return match.Politics.FixtureCaravanLoad(0, 1, seller, buyer, TradeGood.Salt);
        }
        Fix64 plainCargo = Load(false, 1000), volumeCargo = Load(true, 1000);
        Assert.True(plainCargo > Fix64.Zero, "the buyer should state a need");
        AssertNear(plainCargo * Fix64.Parse("1.15") * Fix64.Parse("1.5"), volumeCargo, 64);
        Assert.True(Load(true, 2) <= Fix64.FromInt(2), "credit moves existing goods earlier; it never creates any");
    }

    // ---- NUM-08 admin capacity ----------------------------------------------------------------------------------------

    [Fact]
    public void NUM_08_widens_the_admin_span_in_Fief_by_its_multiplier_rounded_half_up()
    {
        BronzeFixture plain = Fief(new[] { "NUM-08" }, "KEY-5", "KEY-3");
        BronzeFixture wide = Fief(new[] { "NUM-08" }, "KEY-5", "KEY-3", "NUM-08");
        int baseSpan = plain.Economy.FixtureAdminSpanOf(0);
        Assert.True(baseSpan >= 4);
        Assert.Equal((int)Math.Floor(baseSpan * 1.15 + 0.5), wide.Economy.FixtureAdminSpanOf(0));

        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.WithNumeric("NUM-08"), Seed, 100, "KEY-5", "KEY-3", "NUM-08");
        BronzeFixture ironPlain = BronzeFixture.CreateIron(TestPack.WithNumeric("NUM-08"), Seed, 100, "KEY-5", "KEY-3");
        Assert.Equal(ironPlain.Economy.FixtureAdminSpanOf(0), iron.Economy.FixtureAdminSpanOf(0));
    }

    // ---- MED-07 elder hazard ------------------------------------------------------------------------------------------

    [Fact]
    public void A_staffed_hospital_lowers_the_elder_epidemic_hazard_by_the_typed_elder_factor_and_leaves_adults_at_the_hospital_factor()
    {
        static (BronzeFixture Fixture, Fix64 Adult, Fix64 Elder) Hazards(bool staffed)
        {
            BronzeFixture f = Fief(new[] { "MED-07" }, "KEY-5", "MED-07");
            f.Economy.FixtureSetTier(0, 1);
            f.Economy.FixtureBuildIronStructure(0, "hospital");
            f.RunSeasons(1);
            if (!staffed) f.Economy.FixtureClearRoles(0);
            f.Economy.FixtureSetOutbreak(0, OutbreakClass.Plague);
            f.Economy.FixtureSetFigureAgeAt(0, 30);
            Fix64 adult = f.Economy.FixtureInfectedHazard(0);
            f.Economy.FixtureSetFigureAgeAt(0, 60);
            Fix64 elder = f.Economy.FixtureInfectedHazard(0);
            return (f, adult, elder);
        }
        (_, Fix64 adultOpen, Fix64 elderOpen) = Hazards(false);
        (BronzeFixture staffedFixture, Fix64 adultStaffed, Fix64 elderStaffed) = Hazards(true);
        Assert.True(staffedFixture.Economy.FixtureHospitalStaffed(0));
        AssertNear(adultOpen * Fix64.FromRatio(4, 5), adultStaffed, 16);
        AssertNear(elderOpen * Fix64.FromRatio(4, 5) * Fix64.Parse("0.95"), elderStaffed, 32);
    }

    // ---- snapshots ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Manuscripts_and_spectacles_survive_a_snapshot_and_leave_a_match_without_them_unchanged()
    {
        BronzeFixture live = Fief(new[] { "BEL-10", "ART-09", "CER-10" }, "KEY-5", "BEL-10", "ART-09", "CER-10");
        BronzeFixture without = Fief(new[] { "BEL-10", "ART-09", "CER-10" }, "KEY-5", "BEL-10", "ART-09", "CER-10");
        Assert.Equal(without.Economy.ComputeChecksum(), live.Economy.ComputeChecksum());
        live.Economy.FixtureSetGoodStock(0, "G-MANUSCRIPT", Fix64.FromInt(2));
        live.Economy.FixtureSetGoodStock(0, "G-SPECTACLES", Fix64.FromInt(3));
        Assert.NotEqual(without.Economy.ComputeChecksum(), live.Economy.ComputeChecksum());

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) live.Economy.WriteSnapshot(writer);
        stream.Position = 0;
        BronzeFixture resumed = Fief(new[] { "BEL-10", "ART-09", "CER-10" }, "KEY-5", "BEL-10", "ART-09", "CER-10");
        using (var reader = new BinaryReader(stream)) resumed.Economy.ReadSnapshot(reader);
        Assert.Equal(Fix64.FromInt(2), resumed.Economy.FixtureGoodStock(0, "G-MANUSCRIPT"));
        Assert.Equal(Fix64.FromInt(3), resumed.Economy.FixtureGoodStock(0, "G-SPECTACLES"));
        Assert.Equal(live.Economy.ComputeChecksum(), resumed.Economy.ComputeChecksum());
    }
}
