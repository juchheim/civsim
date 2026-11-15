using System.IO;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>The levy's bows (docs/MVP/36): WAR-03 bows, WAR-11 crossbows and longbows as stocked, worn and lost arms in a Fief match.</summary>
public sealed class FiefLevyBowTests
{
    private const ulong Seed = 20261115;

    private static BronzeFixture Fief(params string[] projects)
        => BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, projects);

    private static void Supply(BronzeFixture f, bool iron)
    {
        f.Economy.FixtureStock(0, "G-WOOD", Fix64.FromInt(120));
        f.Economy.FixtureStock(0, "G-CORD", Fix64.FromInt(20));
        if (iron) f.Economy.FixtureStock(0, "G-WROUGHT", Fix64.FromInt(4));
    }

    private static Fix64 Crossbows(BronzeFixture f) => f.Economy.GoodStock(0, "G-CROSSBOW");
    private static Fix64 Longbows(BronzeFixture f) => f.Economy.GoodStock(0, "G-LONGBOW");

    [Fact]
    public void War11_settlement_with_wood_cord_and_iron_builds_crossbows()
    {
        BronzeFixture f = Fief("KEY-4", "KEY-5", "MET-08", "MET-09", "WAR-08", "WAR-11");
        Supply(f, iron: true);
        f.RunSeasons(4);
        Assert.True(Crossbows(f) > Fix64.Zero, "crossbows were made");
        Assert.True(Crossbows(f) + Longbows(f) <= Fix64.FromInt(LivingEconomyModel.LevyBowCap));
    }

    [Fact]
    public void War11_settlement_without_iron_builds_longbows_only()
    {
        BronzeFixture f = Fief("KEY-4", "KEY-5", "WAR-11");
        Supply(f, iron: false);
        f.RunSeasons(4);
        Assert.Equal(Fix64.Zero, Crossbows(f));
        Assert.True(Longbows(f) > Fix64.Zero, "longbows were made");
    }

    [Fact]
    public void Without_War11_no_crossbows_or_longbows_appear()
    {
        BronzeFixture f = Fief("KEY-4", "KEY-5", "MET-08", "MET-09", "WAR-08");
        Supply(f, iron: true);
        f.RunSeasons(4);
        Assert.Equal(Fix64.Zero, Crossbows(f));
        Assert.Equal(Fix64.Zero, Longbows(f));
    }

    [Fact]
    public void Iron_chapter_with_War11_named_builds_no_bows()
    {
        // WAR-11 is an E5 project: an Iron match never records it, and its recipes are not in the Iron planner.
        BronzeFixture f = BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "KEY-4", "MET-08", "MET-09", "WAR-08", "WAR-11");
        Supply(f, iron: true);
        f.RunSeasons(4);
        Assert.Equal(Fix64.Zero, Crossbows(f));
        Assert.Equal(Fix64.Zero, Longbows(f));
    }

    [Fact]
    public void Bow_stock_is_deterministic_and_survives_snapshot_restore()
    {
        BronzeFixture a = Fief("KEY-4", "KEY-5", "MET-08", "MET-09", "WAR-08", "WAR-11");
        BronzeFixture b = Fief("KEY-4", "KEY-5", "MET-08", "MET-09", "WAR-08", "WAR-11");
        Supply(a, iron: true);
        Supply(b, iron: true);
        a.RunSeasons(3);
        b.RunSeasons(3);
        Assert.True(Crossbows(a) + Longbows(a) > Fix64.Zero);
        Assert.Equal(Crossbows(a), Crossbows(b));
        Assert.Equal(Longbows(a), Longbows(b));
        Assert.Equal(a.Economy.ComputeChecksum(), b.Economy.ComputeChecksum());

        byte[] snapshot = Snapshot(a.Economy);
        Restore(b.Economy, snapshot);
        Assert.Equal(a.Economy.ComputeChecksum(), b.Economy.ComputeChecksum());
        a.RunSeasons(2);
        b.RunSeasons(2);
        Assert.Equal(Snapshot(a.Economy), Snapshot(b.Economy));
    }

    [Fact]
    public void Bows_wear_out_without_a_maker_and_can_be_lost_in_battle()
    {
        BronzeFixture f = Fief("KEY-4", "KEY-5");
        f.Economy.FixtureStock(0, "G-CROSSBOW", Fix64.FromInt(10));
        f.RunSeasons(4);
        Fix64 left = Crossbows(f);
        Assert.True(left < Fix64.FromInt(10) && left > Fix64.FromInt(8), $"40-season wear leaves about 9.0, got {left}");
        Assert.Equal(Fix64.FromInt(3), f.Economy.LoseArms(0, "G-CROSSBOW", Fix64.FromInt(3)));
        Assert.Equal(left - Fix64.FromInt(3), Crossbows(f));
    }

    [Fact]
    public void War03_levy_keeps_bows_in_every_chapter_at_up_to_three_a_season()
    {
        // Every fixture knows the E0-E2 projects, HNT-06 and WAR-03 among them.
        BronzeFixture fief = Fief("KEY-4");
        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "KEY-4");
        foreach (BronzeFixture f in new[] { fief, iron })
        {
            f.Economy.FixtureStock(0, "G-WOOD", Fix64.FromInt(120));
            f.Economy.FixtureStock(0, "G-FLINT", Fix64.FromInt(10));
            Fix64 held = f.Economy.GoodStock(0, "G-BOW");
            for (int season = 1; season <= 4; season++)
            {
                f.RunSeasons(1);
                Fix64 now = f.Economy.GoodStock(0, "G-BOW");
                // wear takes a little back each season, so the gain never exceeds the cap
                Assert.True(now - held <= Fix64.FromInt(CivSim.Sim.Economy.LivingEconomyModel.LevyBowsPerSeason), $"season {season}: {held} -> {now}");
                held = now;
            }
            Assert.True(f.Economy.GoodStock(0, "G-BOW") >= Fix64.FromInt(5), $"levy bows after four seasons: {f.Economy.GoodStock(0, "G-BOW")}");
        }
        Fix64 before = fief.Economy.GoodStock(0, "G-BOW");
        Assert.Equal(Fix64.FromInt(2), fief.Economy.LoseArms(0, "G-BOW", Fix64.FromInt(2)));
        Assert.Equal(before - Fix64.FromInt(2), fief.Economy.GoodStock(0, "G-BOW"));
    }

    [Fact]
    public void A_levy_bow_needs_almost_no_wood_and_a_town_makes_three_in_its_first_season()
    {
        // A stave and a few shafts: a twentieth of a litre, so three bows cost less than the hearth burns in a day.
        Assert.True(CivSim.Sim.Economy.LivingEconomyModel.LevyBowWood <= Fix64.FromRatio(1, 10));
        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "KEY-4");
        iron.Economy.FixtureStock(0, "G-WOOD", Fix64.FromInt(120));
        iron.Economy.FixtureStock(0, "G-FLINT", Fix64.FromInt(10));
        Fix64 before = iron.Economy.GoodStock(0, "G-BOW");
        iron.RunSeasons(1);
        Assert.Equal(Fix64.FromInt(3), iron.Economy.GoodStock(0, "G-BOW") - before);
    }

    private static byte[] Snapshot(LivingEconomyModel economy)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) economy.WriteSnapshot(writer);
        return stream.ToArray();
    }

    private static void Restore(LivingEconomyModel economy, byte[] snapshot)
    {
        using var stream = new MemoryStream(snapshot);
        using var reader = new BinaryReader(stream);
        economy.ReadSnapshot(reader);
        Assert.Equal(stream.Length, stream.Position);
    }
}
