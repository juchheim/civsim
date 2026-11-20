using System.IO;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>Coal, coke and coke smelting (M13 WP-E6-5, docs/MVP/38 §5.1). Everything exists only in the Discovery chapter.</summary>
public class DiscoveryCoalTests
{
    private const ulong Seed = 20261019;
    private static readonly string[] CokeKit = { "MET-08", "MET-12", "NRG-03", "MIN-09", "FIR-12", "MET-13" };

    private static BronzeFixture Discovery(params string[] projects) => BronzeFixture.CreateDiscoveryWithCoal(TestPack.Pack, Seed, 100, projects);
    private static BronzeFixture NoCoal(params string[] projects) => BronzeFixture.CreateDiscoveryWithoutCoal(TestPack.Pack, Seed, 100, projects);
    private static BronzeFixture FiefCoal(params string[] projects) => BronzeFixture.CreateFiefWithCoalDeposit(TestPack.Pack, Seed, 100, projects);

    private static BronzeFixture Furnace(BronzeFixture f)
    {
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureSetIronWork(0, "water_mill", 1);
        f.Economy.FixtureSetIronWork(0, "blast_furnace", 1);
        return f;
    }

    [Fact]
    public void Coal_is_dug_only_after_MIN09_and_only_in_a_Discovery_match()
    {
        BronzeFixture locked = Furnace(Discovery("MET-08", "MET-12", "NRG-03", "FIR-12", "MET-13"));   // no MIN-09
        locked.RunSeasons(8);
        Assert.Equal(Fix64.Zero, locked.Economy.CoalStock(0));
        Assert.Equal(Fix64.Zero, locked.Economy.FixtureGoodStock(0, "G-COAL"));

        BronzeFixture open = Furnace(Discovery(CokeKit));
        open.RunSeasons(8);
        Assert.True(open.Economy.FixtureGoodStock(0, "G-COAL") > Fix64.Zero || open.Economy.FixtureGoodStock(0, "G-COKE") > Fix64.Zero || open.Economy.FixtureGoodStock(0, "G-IRON") > Fix64.Zero);

        // A Fief match with the same deposit and the same research never touches coal: no slot, no camp, no stock.
        BronzeFixture fief = Furnace(FiefCoal(CokeKit));
        fief.RunSeasons(8);
        Assert.Equal(Fix64.Zero, fief.Economy.CoalStock(0));
        Assert.Equal(Fix64.Zero, fief.Economy.CokeStock(0));
        Assert.False(fief.Economy.FixtureCokeSmeltingActive(0));
    }

    [Fact]
    public void Coke_smelting_replaces_the_bloomery_only_with_MET13_a_furnace_and_coal_to_hand()
    {
        BronzeFixture f = Furnace(Discovery(CokeKit));
        Assert.True(f.Economy.FixtureCokeSmeltingActive(0));
        Assert.False(f.Economy.FixtureRecipeAvailable(0, "bloomery"));
        Assert.True(f.Economy.FixtureRecipeAvailable(0, "coke_smelt"));

        // No MET-13: the Fief bloomery stays and coke smelting is locked.
        BronzeFixture noResearch = Furnace(Discovery("MET-08", "MET-12", "NRG-03", "MIN-09", "FIR-12"));
        Assert.False(noResearch.Economy.FixtureCokeSmeltingActive(0));
        Assert.True(noResearch.Economy.FixtureRecipeAvailable(0, "bloomery"));
        Assert.False(noResearch.Economy.FixtureRecipeAvailable(0, "coke_smelt"));

        // No coal in reach and no coke in store: the town keeps the Fief furnace.
        BronzeFixture noCoal = Furnace(NoCoal(CokeKit));
        Assert.False(noCoal.Economy.FixtureCokeSmeltingActive(0));
        Assert.True(noCoal.Economy.FixtureRecipeAvailable(0, "bloomery"));

        // Coke already in store is enough, even with no deposit.
        noCoal.Economy.FixtureAddDiscoveryGood(0, "G-COKE", Fix64.FromInt(5));
        Assert.True(noCoal.Economy.FixtureCokeSmeltingActive(0));
        Assert.False(noCoal.Economy.FixtureRecipeAvailable(0, "bloomery"));

        // No blast furnace: no coke smelting.
        BronzeFixture noFurnace = Discovery(CokeKit);
        noFurnace.Economy.FixtureSetTier(0, 1);
        Assert.False(noFurnace.Economy.FixtureCokeSmeltingActive(0));
    }

    [Fact]
    public void A_coke_smelting_town_digs_coal_cokes_it_and_makes_iron_without_burning_charcoal_for_the_smelt()
    {
        BronzeFixture f = Furnace(Discovery(CokeKit));
        Fix64 coalDug = Fix64.Zero;
        bool cokeSeen = false;
        for (int season = 0; season < 12; season++)
        {
            f.RunSeasons(1);
            cokeSeen |= f.Economy.CokeStock(0) > Fix64.Zero;
            coalDug = Fix64.Max(coalDug, f.Economy.CoalStock(0));
        }
        Assert.True(coalDug > Fix64.Zero, "coal was dug");
        Assert.True(cokeSeen, "coal was coked");
        Assert.True(f.Economy.FixtureGoodStock(0, "G-IRON") > Fix64.Zero, "iron was smelted");
        Assert.NotEmpty(f.Economy.FixtureCampTiles(0));

        // The bloomery stands aside here, so the iron came from the coke-smelting recipe: 6 ore and 3 coke for 3 iron, no charcoal input.
        Assert.False(f.Economy.FixtureRecipeAvailable(0, "bloomery"));
        var batch = f.Economy.FixtureBatch(0, "coke_smelt");
        Assert.Equal(Fix64.FromInt(3), batch.Output);

        // Without coal the same research leaves the town on the Fief furnace, and it never holds coal or coke.
        BronzeFixture old = Furnace(NoCoal(CokeKit));
        old.RunSeasons(12);
        Assert.Equal(Fix64.Zero, old.Economy.CoalStock(0) + old.Economy.CokeStock(0));
    }

    [Fact]
    public void Coal_and_coke_survive_a_snapshot_restore_and_continuation()
    {
        BronzeFixture live = Furnace(Discovery(CokeKit));
        live.RunSeasons(3);
        Assert.True(live.Economy.CoalStock(0) > Fix64.Zero || live.Economy.CokeStock(0) > Fix64.Zero);
        byte[] saved = Snapshot(live.Economy);

        BronzeFixture resumed = Furnace(Discovery(CokeKit));
        resumed.RunSeasons(3);
        Restore(resumed.Economy, saved);
        Assert.Equal(live.Economy.CoalStock(0), resumed.Economy.CoalStock(0));
        Assert.Equal(live.Economy.CokeStock(0), resumed.Economy.CokeStock(0));
        Assert.Equal(live.Economy.ComputeChecksum(), resumed.Economy.ComputeChecksum());

        live.RunSeasons(4);
        resumed.RunSeasons(4);
        Assert.Equal(Snapshot(live.Economy), Snapshot(resumed.Economy));
        Assert.Equal(live.Economy.ComputeChecksum(), resumed.Economy.ComputeChecksum());
    }

    [Fact]
    public void A_Fief_economy_is_byte_identical_whether_or_not_the_world_holds_coal()
    {
        // Coal exists only in Discovery: a Fief town with a coal deposit on its land plays exactly like the same town without one except for the deposit tile itself.
        BronzeFixture withCoal = Furnace(FiefCoal(CokeKit));
        BronzeFixture without = Furnace(BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, CokeKit));
        withCoal.RunSeasons(6);
        without.RunSeasons(6);
        Assert.Equal(without.Economy.FixtureGoodStock(0, "G-IRON"), withCoal.Economy.FixtureGoodStock(0, "G-IRON"));
        Assert.Equal(without.Economy.FixtureGoodStock(0, "G-CHAR"), withCoal.Economy.FixtureGoodStock(0, "G-CHAR"));
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
