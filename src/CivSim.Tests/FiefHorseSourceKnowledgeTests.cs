using System.IO;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

public sealed class FiefHorseSourceKnowledgeTests
{
    [Fact]
    public void Horse_sightings_respect_exploration_and_absence_requires_full_map_exploration()
    {
        const ulong seed = 20261054;
        GeneratedWorld generated = ForecastTestKit.World(seed);
        var match = MatchModel.Create(seed, TestPack.Pack, generated, ForecastTestKit.Balanced,
            new[] { CivControl.Council, CivControl.Council, CivControl.Council, CivControl.Council }, rules: ChapterRules.Fief);
        int horse = -1;
        for (int i = 0; i < match.Economy.WildHerdCount; i++)
        {
            WildHerdView herd = match.Economy.WildHerd(i);
            if (herd.Species == HerdSpecies.Horse && herd.AnimalCount > Fix64.Zero) { horse = i; break; }
        }
        Assert.True(horse >= 0, "fixture world needs one live horse herd");
        int horseTile = match.Economy.WildHerd(horse).CurrentTile;
        var map = new MapKnowledge(generated.Tiles.ToArray(), generated.Width, generated.Height);

        Assert.Equal(HorseSourceKnowledge.Unknown,
            BeliefBuilder.ObserveHorseSourceKnowledge(map, match.Economy, HorseSourceKnowledge.Unknown));
        map.MarkExplored(horseTile);
        Assert.Equal(HorseSourceKnowledge.Seen,
            BeliefBuilder.ObserveHorseSourceKnowledge(map, match.Economy, HorseSourceKnowledge.Unknown));

        for (int i = 0; i < match.Economy.WildHerdCount; i++)
        {
            WildHerdView herd = match.Economy.WildHerd(i);
            if (herd.Species == HerdSpecies.Horse)
                match.Economy.FixtureSetWildHerd(i, herd.CurrentTile, Fix64.Zero);
        }
        var unexplored = new MapKnowledge(generated.Tiles.ToArray(), generated.Width, generated.Height);
        Assert.Equal(HorseSourceKnowledge.Unknown,
            BeliefBuilder.ObserveHorseSourceKnowledge(unexplored, match.Economy, HorseSourceKnowledge.Unknown));
        for (int tile = 0; tile < unexplored.TileCount; tile++) unexplored.MarkExplored(tile);
        Assert.Equal(HorseSourceKnowledge.ConfirmedAbsent,
            BeliefBuilder.ObserveHorseSourceKnowledge(unexplored, match.Economy, HorseSourceKnowledge.Unknown));
    }

    [Fact]
    public void Horse_knowledge_is_a_Fief_only_hash_and_snapshot_tail()
    {
        GeneratedWorld generated = ForecastTestKit.World(20261054);
        MapKnowledge Map() => new(generated.Tiles.ToArray(), generated.Width, generated.Height);
        var iron = new Belief(0, 2, TestPack.Catalog.Count, Map());
        var ironChanged = new Belief(0, 2, TestPack.Catalog.Count, Map());
        ironChanged.HorseSource = HorseSourceKnowledge.Seen;
        Assert.Equal(Hash(iron), Hash(ironChanged));
        Assert.Equal(Snapshot(iron), Snapshot(ironChanged));
        var bronze = new Belief(0, 2, TestPack.Catalog.Count, Map());
        var bronzeChanged = new Belief(0, 2, TestPack.Catalog.Count, Map());
        bronzeChanged.HorseSource = HorseSourceKnowledge.ConfirmedAbsent;
        Assert.Equal(Hash(bronze), Hash(bronzeChanged));
        Assert.Equal(Snapshot(bronze), Snapshot(bronzeChanged));

        var fief = new Belief(0, 2, TestPack.Catalog.Count, Map(), fiefChapter: true)
            { HorseSource = HorseSourceKnowledge.ConfirmedAbsent };
        var fiefUnknown = new Belief(0, 2, TestPack.Catalog.Count, Map(), fiefChapter: true);
        Assert.NotEqual(Hash(fief), Hash(fiefUnknown));
        Assert.NotEqual(Snapshot(fief), Snapshot(fiefUnknown));
        var restored = new Belief(0, 2, TestPack.Catalog.Count, Map(), fiefChapter: true);
        using (var stream = new MemoryStream(Snapshot(fief)))
        using (var reader = new BinaryReader(stream)) restored.Read(reader);
        Assert.Equal(HorseSourceKnowledge.ConfirmedAbsent, restored.HorseSource);
        Assert.Equal(Hash(fief), Hash(restored));

        static ulong Hash(Belief belief)
        {
            var hasher = new StateHasher();
            belief.Hash(ref hasher);
            return hasher.Finish();
        }
        static byte[] Snapshot(Belief belief)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) belief.Write(writer);
            return stream.ToArray();
        }
    }

    [Fact]
    public void Fief_spark_blocks_horse_dependent_research_until_a_source_is_seen()
    {
        const ulong seed = 20261054;
        GeneratedWorld generated = ForecastTestKit.World(seed);
        var controls = new[] { CivControl.Council, CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Council, CivControl.Council };
        MatchModel match = MatchModel.Create(seed, TestPack.Pack, generated, ForecastTestKit.Balanced, controls, rules: ChapterRules.Fief);
        AiSeat seat = Assert.IsType<AiSeat>(match.Ai!.SeatOf(1));
        match.Ai.OnSeasonEnd(0);
        var spark = Assert.IsType<ResearchSpark>(seat.Spark);
        int hus07 = match.Research.Catalog.IndexOf("HUS-07"), trn09 = match.Research.Catalog.IndexOf("TRN-09"), war10 = match.Research.Catalog.IndexOf("WAR-10");
        foreach (HorseSourceKnowledge knowledge in new[] { HorseSourceKnowledge.Unknown, HorseSourceKnowledge.ConfirmedAbsent })
        {
            seat.Belief.HorseSource = knowledge;
            seat.Context.Season++;
            seat.Spark.Decide(seat.Context);
            Assert.True(spark.HorseBlockedForDiagnostics(hus07), $"HUS-07 should stay gated for {knowledge}");
            Assert.True(spark.HorseBlockedForDiagnostics(trn09), $"TRN-09 should stay gated for {knowledge}");
            Assert.True(spark.HorseBlockedForDiagnostics(war10), $"WAR-10 should stay gated for {knowledge}");
        }

        seat.Belief.HorseSource = HorseSourceKnowledge.Seen;
        seat.Context.Season++;
        seat.Spark.Decide(seat.Context);
        Assert.False(spark.HorseBlockedForDiagnostics(hus07));
        Assert.False(spark.HorseBlockedForDiagnostics(trn09));
        Assert.False(spark.HorseBlockedForDiagnostics(war10));
    }
}
