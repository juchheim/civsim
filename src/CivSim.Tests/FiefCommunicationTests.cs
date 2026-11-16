using System.IO;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Tests;

/// <summary>COM-09 paper and COM-10 movable type runtime wiring (WP-E5-6).</summary>
public sealed class FiefCommunicationTests
{
    private const ulong Seed = 20261114;

    private static BronzeFixture Fief(params string[] projects)
        => BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, projects);

    [Fact]
    public void Paper_and_book_recipes_conserve_their_inputs_and_require_the_Fief_print_shop_staffing()
    {
        BronzeFixture f = Fief("KEY-5", "COM-05", "COM-09", "COM-10");
        f.Economy.FixtureSetTier(0, 1);

        f.Economy.FixtureSetGoodStock(0, "G-CLOTH", Fix64.One);
        Assert.Equal(Fix64.One, f.Economy.FixtureRunRecipe(0, "make_paper", Fix64.One, Fix64.One));
        Assert.InRange(f.Economy.FixtureGoodStock(0, "G-CLOTH").Raw,
            Fix64.Parse("0.475").Raw - 1, Fix64.Parse("0.475").Raw + 1); // 0.5 recipe input plus the draft 5% Fief loss allowance.
        Assert.Equal(Fix64.FromInt(2), f.Economy.FixtureGoodStock(0, "G-PAPER"));

        Assert.Contains(f.Economy.BuildOutlookFor(0).Items, item => item.Id == "print_shop");
        f.Economy.FixtureBuildIronStructure(0, "print_shop");
        f.Economy.FixtureSetGoodStock(0, "G-PAPER", Fix64.FromInt(4));
        f.Economy.FixtureSetGoodStock(0, "G-PIGMENT", Fix64.One);
        f.Economy.FixtureSetGoodStock(0, "G-BOOK", Fix64.Zero);
        Assert.Equal(Fix64.Zero, f.Economy.FixtureRunRecipe(0, "print_book", Fix64.One, Fix64.One));
        f.RunSeasons(1);
        Assert.True(f.Economy.RoleHeads(0).Scribes > 0);
        Assert.True(f.Economy.FixtureGoodStock(0, "G-BOOK") > Fix64.Zero,
            "The staffed print shop should leave book stock after seasonal household demand.");

        f.Economy.FixtureSetGoodStock(0, "G-PAPER", Fix64.Parse("4.2")); // Four recipe units plus the draft 5% loss allowance.
        f.Economy.FixtureSetGoodStock(0, "G-PIGMENT", Fix64.One);
        f.Economy.FixtureSetGoodStock(0, "G-BOOK", Fix64.Zero);
        Assert.Equal(Fix64.One, f.Economy.FixtureRunRecipe(0, "print_book", Fix64.One, Fix64.One));
        Assert.InRange(f.Economy.FixtureGoodStock(0, "G-PAPER").Raw, 0, 4); // Fixed-point 4.2 input may leave a few raw units.
        Assert.InRange(f.Economy.FixtureGoodStock(0, "G-PIGMENT").Raw,
            Fix64.Parse("0.895").Raw - 4, Fix64.Parse("0.895").Raw + 4); // 0.1 pigment plus the draft 5% loss allowance.
        Assert.Equal(Fix64.One, f.Economy.FixtureGoodStock(0, "G-BOOK"));
    }

    [Fact]
    public void Print_shop_requires_COM_10_and_is_not_buildable_in_Iron_or_Bronze()
    {
        BronzeFixture locked = Fief("KEY-5", "COM-09");
        locked.Economy.FixtureSetTier(0, 1);
        Assert.DoesNotContain(locked.Economy.BuildOutlookFor(0).Items, item => item.Id == "print_shop");

        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "KEY-5", "COM-10");
        iron.Economy.FixtureSetTier(0, 1);
        Assert.Contains(iron.Economy.BuildOutlookFor(0).Items,
            item => item.Id == "print_shop" && item.Gate.Why == BuildWhy.NoSuchStructure);

        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, Seed, 100, true, true, true, true, "COM-10");
        bronze.Economy.FixtureSetTier(0, 1);
        Assert.DoesNotContain(bronze.Economy.BuildOutlookFor(0).Items, item => item.Id == "print_shop");
    }

    [Fact]
    public void Fief_paper_book_and_print_shop_state_survive_snapshot_continuation()
    {
        BronzeFixture live = Fief("KEY-5", "COM-05", "COM-09", "COM-10");
        live.Economy.FixtureSetTier(0, 1);
        live.Economy.FixtureBuildIronStructure(0, "print_shop");
        live.Economy.FixtureSetGoodStock(0, "G-PAPER", Fix64.FromInt(3));
        live.Economy.FixtureSetGoodStock(0, "G-BOOK", Fix64.One);
        live.RunSeasons(2);
        byte[] saved = Snapshot(live.Economy);

        BronzeFixture resumed = Fief("KEY-5", "COM-05", "COM-09", "COM-10");
        resumed.Economy.FixtureSetTier(0, 1);
        resumed.Economy.FixtureBuildIronStructure(0, "print_shop");
        resumed.Economy.FixtureSetGoodStock(0, "G-PAPER", Fix64.FromInt(3));
        resumed.Economy.FixtureSetGoodStock(0, "G-BOOK", Fix64.One);
        resumed.RunSeasons(2);
        Restore(resumed.Economy, saved);
        live.RunSeasons(2);
        resumed.RunSeasons(2);
        Assert.Equal(Snapshot(live.Economy), Snapshot(resumed.Economy));
        Assert.Equal(live.Economy.ComputeChecksum(), resumed.Economy.ComputeChecksum());
        Assert.Equal(Fix64.FromInt(3), resumed.Economy.FixtureGoodStock(0, "G-PAPER"));
        Assert.Equal(Fix64.One, resumed.Economy.FixtureGoodStock(0, "G-BOOK"));
    }

    [Fact]
    public void Adding_Fief_good_slots_does_not_change_fresh_Iron_or_Bronze_snapshot_identity()
    {
        BronzeFixture ironA = BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "KEY-5");
        BronzeFixture ironB = BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "KEY-5");
        BronzeFixture bronzeA = BronzeFixture.Create(TestPack.Pack, Seed, 100, true, true, true, true);
        BronzeFixture bronzeB = BronzeFixture.Create(TestPack.Pack, Seed, 100, true, true, true, true);
        Assert.Equal(ironA.Economy.ComputeChecksum(), ironB.Economy.ComputeChecksum());
        Assert.Equal(Snapshot(ironA.Economy), Snapshot(ironB.Economy));
        Assert.Equal(bronzeA.Economy.ComputeChecksum(), bronzeB.Economy.ComputeChecksum());
        Assert.Equal(Snapshot(bronzeA.Economy), Snapshot(bronzeB.Economy));
    }

    [Fact]
    public void COM_09_scales_only_Fief_scribe_and_scholar_writing_output_by_1_3()
    {
        ResearchCatalog catalog = CommunicationCatalog();
        ResearchRig plain = new(catalog: catalog, rules: ChapterRules.Fief);
        ResearchRig written = new(catalog: catalog, rules: ChapterRules.Fief);
        plain.GrantAll(0, "COM-05", "LRN-05", "KEY-5");
        written.GrantAll(0, "COM-05", "LRN-05", "KEY-5", "COM-09");
        plain.Run(ResearchSystem.SeasonTicks);
        written.Run(ResearchSystem.SeasonTicks);
        Fix64 writingMultiplier = written.Research.ModifierMultiplier(0, "writing_role_output");
        Assert.True(writingMultiplier > Fix64.One && writingMultiplier < Fix64.FromRatio(13, 10));
        Assert.True(written.Research.InsightPerYear(0) > plain.Research.InsightPerYear(0));

        ResearchRig iron = new(catalog: catalog, rules: ChapterRules.Iron);
        iron.GrantAll(0, "COM-05", "LRN-05");
        iron.Run(ResearchSystem.SeasonTicks);
        ResearchRig ironWithDraftGrant = new(catalog: catalog, rules: ChapterRules.Iron);
        ironWithDraftGrant.GrantAll(0, "COM-05", "LRN-05", "COM-09");
        ironWithDraftGrant.Run(ResearchSystem.SeasonTicks);
        Assert.Equal(iron.Research.InsightPerYear(0), ironWithDraftGrant.Research.InsightPerYear(0));
    }

    [Fact]
    public void COM_10_multiplies_diffusion_reduction_by_1_5_and_keeps_the_half_cost_floor()
    {
        ResearchCatalog catalog = CommunicationCatalog();
        int target = catalog.IndexOf("COM-09");
        ResearchRig plain = new(civs: 2, catalog: catalog, rules: ChapterRules.Fief);
        ResearchRig faster = new(civs: 2, catalog: catalog, rules: ChapterRules.Fief);
        plain.GrantAll(0, "KEY-5");
        plain.GrantAll(1, "KEY-5", "COM-09");
        faster.GrantAll(0, "KEY-5", "COM-10");
        faster.GrantAll(1, "KEY-5", "COM-09");
        plain.Research.RecordContact(0, 1);
        faster.Research.RecordContact(0, 1);
        Fix64 step = catalog.Constant("DiffusionStep");
        Fix64 floorValue = catalog.Constant("DiffusionFloor");
        Fix64 plainCost = plain.Research.EffectiveCost(0, target);
        Fix64 fasterCost = faster.Research.EffectiveCost(0, target);
        Fix64 contributionMultiplier = faster.Research.ModifierMultiplier(0, "diffusion_discount");
        Assert.True(contributionMultiplier > Fix64.One && contributionMultiplier < Fix64.FromRatio(3, 2));
        Fix64 expectedFaster = plainCost * Fix64.Max(floorValue, Fix64.One - step * contributionMultiplier) /
            Fix64.Max(floorValue, Fix64.One - step);
        Assert.InRange(fasterCost.Raw, expectedFaster.Raw - 1, expectedFaster.Raw + 1);

        ResearchRig floor = new(civs: 6, catalog: catalog, rules: ChapterRules.Fief);
        ResearchRig noContact = new(catalog: catalog, rules: ChapterRules.Fief);
        floor.GrantAll(0, "KEY-5", "COM-10");
        noContact.GrantAll(0, "KEY-5", "COM-10");
        for (int civ = 1; civ < 6; civ++)
        {
            floor.GrantAll(civ, "KEY-5", "COM-09");
            floor.Research.RecordContact(0, civ);
        }
        Assert.InRange(floor.Research.EffectiveCost(0, target).Raw,
            (noContact.Research.EffectiveCost(0, target) * floorValue).Raw - 1,
            (noContact.Research.EffectiveCost(0, target) * floorValue).Raw + 1);

        ResearchRig ironPlain = new(civs: 2, catalog: catalog, rules: ChapterRules.Iron);
        ResearchRig ironDraft = new(civs: 2, catalog: catalog, rules: ChapterRules.Iron);
        ironPlain.GrantAll(1, "KEY-5", "MEC-05");
        ironDraft.GrantAll(1, "KEY-5", "MEC-05");
        ironPlain.GrantAll(0, "KEY-5");
        ironDraft.GrantAll(0, "KEY-5", "COM-10");
        ironPlain.Research.RecordContact(0, 1);
        ironDraft.Research.RecordContact(0, 1);
        Assert.Equal(ironPlain.Research.EffectiveCost(0, catalog.IndexOf("MEC-05")),
            ironDraft.Research.EffectiveCost(0, catalog.IndexOf("MEC-05")));
    }

    private static byte[] Snapshot(LivingEconomyModel economy)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) economy.WriteSnapshot(writer);
        return stream.ToArray();
    }

    private static void Restore(LivingEconomyModel economy, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);
        economy.ReadSnapshot(reader);
        Assert.Equal(stream.Length, stream.Position);
    }

    private static ResearchCatalog CommunicationCatalog()
        => new(TestPack.WithNumeric("COM-09", "COM-10"));
}
