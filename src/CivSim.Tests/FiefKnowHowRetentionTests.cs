using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

public sealed class FiefKnowHowRetentionTests
{
    private const ulong Seed = 20261119;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> FiefPack = new(() => TestPack.WithNumeric("BEL-10"));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static MatchModel Start(ulong seed = Seed, bool fief = true)
        => MatchModel.Create(seed, fief ? FiefPack.Value : TestPack.Pack, WorldGenerator.Generate(seed), Balanced,
            default, rules: fief ? ChapterRules.Fief : ChapterRules.Bronze);

    private static byte[] Snapshot(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) write(writer);
        return stream.ToArray();
    }

    private static Fix64 LocalBalance(MatchModel match, int civ, int lineage)
    {
        Fix64 total = match.Source.KnowHowReserve(civ, lineage);
        for (int i = 0; i < match.Economy.SettlementCount; i++)
            if (match.Economy.SettlementLite(i).Civilization == civ)
                total += match.Source.KnowHowBucket(i, lineage);
        return total;
    }

    private static Fix64 LocalBucketTotal(MatchModel match, int settlement)
    {
        Fix64 total = Fix64.Zero;
        for (int l = 0; l < match.Research.Catalog.LineageCodes.Count; l++)
            total += match.Source.KnowHowBucket(settlement, l);
        return total;
    }

    private static Fix64 PoolTotal(MatchModel match, int civ)
    {
        Fix64 total = Fix64.Zero;
        for (int l = 0; l < match.Research.Catalog.LineageCodes.Count; l++) total += match.Research.KnowHow(civ, l);
        return total;
    }

    private static void GenerateOneSeason(MatchModel match)
        => new SimRunner(match, startTick: match.Economy.Tick).Advance(LivingEconomyModel.SeasonTicks);

    private static void GrantBel10(MatchModel match)
    {
        match.Economy.FixtureComplete(0, "KEY-3", "KEY-4", "KEY-5", "BEL-10");
        match.Economy.FixtureSetTier(0, 1);
        match.Economy.FixtureAddPeople(0, 100, 24);
        var runner = new SimRunner(match);
        int key5 = match.Research.Catalog.IndexOf("KEY-5");
        Assert.True(key5 >= 0);
        runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, key5, 0, 0));
        runner.Advance(1);
        runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject,
            match.Research.Catalog.IndexOf("BEL-10"), 0, 0));
        runner.Advance(1);
        Assert.True(match.Research.IsDone(0, match.Research.Catalog.IndexOf("BEL-10")));
    }

    [Fact]
    public void Generated_and_spent_KnowHow_is_conserved_across_civ_pool_and_local_buckets()
    {
        MatchModel match = Start();
        var runner = new SimRunner(match);
        runner.Advance(LivingEconomyModel.SeasonTicks);
        int civ = 0;
        int project = -1;
        for (int season = 0; season < 12 && project < 0; season++)
        {
            project = Enumerable.Range(0, match.Research.Catalog.Count).FirstOrDefault(p =>
                match.Research.Catalog.Projects[p].Lineage >= 0 &&
                match.Research.KnowHow(civ, match.Research.Catalog.Projects[p].Lineage) > Fix64.Zero &&
                match.Research.State(civ, p) == ProjectState.Available, -1);
            if (project < 0) GenerateOneSeason(match);
        }
        Assert.True(project >= 0, "A generated lineage should eventually have an available project.");
        int lineage = match.Research.Catalog.Projects[project].Lineage;
        Assert.Equal(match.Research.KnowHow(civ, lineage), LocalBalance(match, civ, lineage));
        match.Research.ApplyOrder(new Command(match.Research.Tick, 0, CommandKind.SetSlot, 0, project, 0));
        Fix64 beforePool = match.Research.KnowHow(civ, lineage);
        Fix64 beforeTracked = LocalBalance(match, civ, lineage);
        new SimRunner(match, startTick: match.Economy.Tick).Advance(10);
        Assert.True(match.Research.KnowHow(civ, lineage) < beforePool);
        Assert.Equal(match.Research.KnowHow(civ, lineage) - beforePool, LocalBalance(match, civ, lineage) - beforeTracked);
        Assert.Equal(match.Research.KnowHow(civ, lineage), LocalBalance(match, civ, lineage));
    }

    [Fact]
    public void Unstaffed_capture_discards_only_local_KnowHow_and_keeps_completed_technology_civ_owned()
    {
        MatchModel match = Start();
        GrantBel10(match);
        GenerateOneSeason(match);
        Fix64 local = LocalBucketTotal(match, 0);
        Assert.True(local > Fix64.Zero);
        Fix64 before = PoolTotal(match, 0);
        Assert.True(match.Research.IsDone(0, match.Research.Catalog.IndexOf("BEL-10")));

        match.Economy.TransferSettlement(0, 1);

        Assert.Equal(Fix64.Zero, LocalBucketTotal(match, 0));
        Assert.Equal(before - local, PoolTotal(match, 0));
        Assert.True(match.Research.IsDone(0, match.Research.Catalog.IndexOf("BEL-10")));
        Assert.False(match.Research.IsDone(1, match.Research.Catalog.IndexOf("BEL-10")));
    }

    [Fact]
    public void Staffed_monastery_preserves_exact_unspent_bucket_in_former_civ_reserve_on_capture_and_collapse()
    {
        foreach (bool collapse in new[] { false, true })
        {
            MatchModel match = Start();
            GrantBel10(match);
            match.Economy.FixtureBuildIronStructure(0, "monastery");
            GenerateOneSeason(match);
            match.Economy.FixtureSetMonks(0, 4);
            Assert.True(match.Economy.MonksAt(0) >= 4,
                $"monks={match.Economy.MonksAt(0)}");
            Fix64 local = LocalBucketTotal(match, 0);
            Assert.True(local > Fix64.Zero);
            Fix64 before = PoolTotal(match, 0);
            Fix64 reserveBefore = Fix64.Zero;
            for (int l = 0; l < match.Research.Catalog.LineageCodes.Count; l++) reserveBefore += match.Source.KnowHowReserve(0, l);

            if (collapse) match.Economy.RazeSettlement(0);
            else match.Economy.TransferSettlement(0, 1);

            Fix64 reserveAfter = Fix64.Zero;
            for (int l = 0; l < match.Research.Catalog.LineageCodes.Count; l++) reserveAfter += match.Source.KnowHowReserve(0, l);
            Assert.Equal(Fix64.Zero, LocalBucketTotal(match, 0));
            Assert.Equal(local, reserveAfter - reserveBefore);
            Assert.Equal(before, PoolTotal(match, 0));
        }
    }

    [Fact]
    public void Unstaffed_collapse_discards_local_balance_without_removing_completed_technology()
    {
        MatchModel match = Start();
        GrantBel10(match);
        GenerateOneSeason(match);
        Fix64 local = LocalBucketTotal(match, 0);
        Fix64 before = PoolTotal(match, 0);
        Assert.True(local > Fix64.Zero);

        match.Economy.RazeSettlement(0);

        Assert.Equal(Fix64.Zero, LocalBucketTotal(match, 0));
        Assert.Equal(before - local, PoolTotal(match, 0));
        Assert.True(match.Research.IsDone(0, match.Research.Catalog.IndexOf("BEL-10")));
    }

    [Fact]
    public void Fief_retention_snapshot_continues_and_v40_migrates_pool_to_capital()
    {
        MatchModel live = Start();
        GenerateOneSeason(live);
        byte[] matchSnapshot = Snapshot(live.WriteSnapshot);
        MatchModel resumed = Start();
        using (var stream = new MemoryStream(matchSnapshot))
        using (var reader = new BinaryReader(stream)) resumed.ReadSnapshot(reader);
        Assert.Equal(live.ComputeChecksum(), resumed.ComputeChecksum());
        GenerateOneSeason(live);
        GenerateOneSeason(resumed);
        Assert.Equal(live.ComputeChecksum(), resumed.ComputeChecksum());

        byte[] economy = Snapshot(live.Economy.WriteSnapshot);
        byte[] research = Snapshot(live.Research.WriteSnapshot);
        byte[] legacyEconomy = AsVersion40(economy);
        MatchModel migrated = Start();
        using (var stream = new MemoryStream(legacyEconomy))
        using (var reader = new BinaryReader(stream)) migrated.Economy.ReadSnapshot(reader);
        using (var stream = new MemoryStream(research))
        using (var reader = new BinaryReader(stream)) migrated.Research.ReadSnapshot(reader);
        int capital = migrated.Economy.Capital(0);
        Assert.True(capital >= 0);
        for (int l = 0; l < migrated.Research.Catalog.LineageCodes.Count; l++)
        {
            Assert.Equal(migrated.Research.KnowHow(0, l), migrated.Source.KnowHowBucket(capital, l));
            Assert.Equal(Fix64.Zero, migrated.Source.KnowHowReserve(0, l));
        }
    }

    [Fact]
    public void Iron_and_Bronze_keep_their_non_Fief_snapshot_and_hash_paths()
    {
        foreach (bool iron in new[] { false, true })
        {
            BronzeFixture fixture = iron
                ? BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "KEY-5")
                : BronzeFixture.Create(TestPack.Pack, Seed, 100, true, tin: true, arsenic: true, "KEY-3");
            byte[] before = Snapshot(fixture.Economy.WriteSnapshot);
            Assert.Equal(iron ? LivingEconomyModel.IronSnapshotVersion : LivingEconomyModel.SnapshotVersion, BinaryPrimitives.ReadInt32LittleEndian(before.AsSpan(sizeof(int), sizeof(int))));
            var restored = iron
                ? BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "KEY-5")
                : BronzeFixture.Create(TestPack.Pack, Seed, 100, true, tin: true, arsenic: true, "KEY-3");
            using (var stream = new MemoryStream(before))
            using (var reader = new BinaryReader(stream)) restored.Economy.ReadSnapshot(reader);
            Assert.Equal(before, Snapshot(restored.Economy.WriteSnapshot));
            Assert.Equal(fixture.Economy.ComputeChecksum(), restored.Economy.ComputeChecksum());
        }
    }

    private static byte[] AsVersion40(byte[] version41)
    {
        int marker = -1;
        for (int i = version41.Length - sizeof(uint); i >= 0; i--)
            if (BinaryPrimitives.ReadUInt32LittleEndian(version41.AsSpan(i, sizeof(uint))) == 0x48574F4B)
            { marker = i; break; }
        Assert.True(marker >= 0, "Fief Know-how snapshot tail should be present.");
        byte[] legacy = version41[..marker];
        BinaryPrimitives.WriteInt32LittleEndian(legacy.AsSpan(sizeof(int), sizeof(int)), 40);
        return legacy;
    }
}
