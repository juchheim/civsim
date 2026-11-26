using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Legends;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Phase L1 of the Legends (docs/MVP/40): deed scores, the roll, names, the log and its frame. Nothing else in the match reads them yet.</summary>
public sealed class LegendTests
{
    private const ulong Seed = 20261127;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner) Start()
    {
        MatchModel match = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        return (match, new SimRunner(match));
    }

    private static void Grant(SimRunner runner, MatchModel match, params string[] projects)
    {
        foreach (string project in projects)
        {
            runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, match.Research.Catalog.IndexOf(project), 0, 0));
            runner.Advance(2);
        }
    }

    /// <summary>An adult of civilization 0 who is likely to live through the test.</summary>
    private static long AnAdult(MatchModel match, int skip = 0)
    {
        int seen = 0;
        for (int i = 0; i < match.Economy.FigureCount; i++)
        {
            FigureView f = match.Economy.Figure(i);
            if (f.Civilization == 0 && f.Age is >= 18 and <= 30 && seen++ >= skip) return f.Id;
        }
        throw new InvalidOperationException("no adult");
    }

    [Fact]
    public void Deeds_add_up_and_the_best_deed_is_remembered()
    {
        (MatchModel match, _) = Start();
        long id = AnAdult(match);
        match.Legends.NoteDeed(id, LegendDeed.Innovator, 10, 3);
        match.Legends.NoteDeed(id, LegendDeed.Founder, 60, 7);
        match.Legends.NoteDeed(id, LegendDeed.Innovator, 10, 4);
        Assert.Equal(80, match.Legends.ScoreOf(id));
        Assert.False(match.Legends.IsLegend(id));
        match.Legends.NoteDeed(-1, LegendDeed.Founder, 60); // not a person
        match.Legends.NoteDeed(id, LegendDeed.Founder, 0);   // nothing
        Assert.Equal(80, match.Legends.ScoreOf(id));
    }

    [Fact]
    public void A_figure_at_the_threshold_becomes_a_Legend_only_after_the_first_keystone()
    {
        (MatchModel match, SimRunner runner) = Start();
        long id = AnAdult(match);
        match.Legends.NoteDeed(id, LegendDeed.Founder, LegendSystem.Threshold, 5);
        runner.Advance(150 * 24); // two years: plenty of rolls at 35%
        Assert.False(match.Legends.IsLegend(id), "no Legends before KEY-1");

        Grant(runner, match, "KEY-1");
        for (int i = 0; i < 40 && !match.Legends.IsLegend(id); i++) runner.Advance(150);
        Assert.True(match.Legends.IsLegend(id), "a figure at the threshold becomes a Legend within a few seasons of KEY-1");
        LegendRecord record = match.Legends.Records.Single(r => r.FigureId == id);
        Assert.Equal(LegendDeed.Founder, record.Deed);
        Assert.Equal(0, record.Civilization);
        Assert.False(string.IsNullOrEmpty(LegendNames.Compose(record.NameCode)));
    }

    [Fact]
    public void A_settlement_holds_at_most_three_living_Legends()
    {
        (MatchModel match, SimRunner runner) = Start();
        Grant(runner, match, "KEY-1");
        var ids = new List<long>();
        int home = -1;
        for (int i = 0; i < match.Economy.FigureCount && ids.Count < 6; i++)
        {
            FigureView f = match.Economy.Figure(i);
            if (f.Civilization != 0 || f.Age is < 18 or > 30) continue;
            if (home < 0) home = f.SettlementId;
            if (f.SettlementId == home) ids.Add(f.Id);
        }
        Assert.True(ids.Count >= 4, "the home settlement has several adults");
        foreach (long id in ids) match.Legends.NoteDeed(id, LegendDeed.Founder, 200, 1);
        for (int i = 0; i < 60; i++) runner.Advance(150);
        Assert.True(match.Legends.LivingInSettlement(home) <= LegendSystem.PerSettlementCap);
        Assert.True(match.Legends.Records.Count(r => r.Settlement == home) >= 1);
    }

    [Fact]
    public void The_chance_is_35_percent_and_ART10_doubles_it()
    {
        (MatchModel match, _) = Start();
        Assert.Equal(3500, match.Legends.ChancePermyriad(0));
        var pack = new Lazy<CivSim.Sim.Content.ContentPack>(() => TestPack.WithNumeric("KEY-6", "ART-10"));
        EconomyArchetype[] a = Balanced;
        MatchModel patron = MatchModel.Create(Seed, pack.Value, World.Value, a, default, rules: ChapterRules.Discovery);
        var runner = new SimRunner(patron);
        Grant(runner, patron, "KEY-5", "KEY-6", "ART-10");
        int home = Enumerable.Range(0, patron.Economy.SettlementCount).First(i => patron.Economy.SettlementLite(i).Civilization == 0);
        patron.Economy.FixtureSetPalaces(home, 1);
        Assert.Equal(7000, patron.Legends.ChancePermyriad(0));
    }

    [Fact]
    public void Legends_are_the_same_in_two_runs_and_survive_a_round_trip()
    {
        List<(long, int, int)> Run(out ulong hash, out byte[] bytes)
        {
            (MatchModel match, SimRunner runner) = Start();
            Grant(runner, match, "KEY-1");
            for (int i = 0; i < 3; i++) match.Legends.NoteDeed(AnAdult(match, i), LegendDeed.First, 150, i);
            runner.Advance(150 * 40);
            var h = new StateHasher();
            match.Legends.Hash(ref h);
            hash = h.Finish();
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) match.Legends.Write(w);
            bytes = ms.ToArray();
            return match.Legends.Records.Select(r => (r.FigureId, r.BornSeason, r.NameCode)).ToList();
        }
        var first = Run(out ulong h1, out byte[] b1);
        var second = Run(out ulong h2, out byte[] b2);
        Assert.Equal(first, second);
        Assert.Equal(h1, h2);
        Assert.Equal(b1, b2);

        (MatchModel other, _) = Start();
        using (var r = new BinaryReader(new MemoryStream(b1))) other.Legends.Read(r);
        var hh = new StateHasher();
        other.Legends.Hash(ref hh);
        Assert.Equal(h1, hh.Finish());
    }

    [Fact]
    public void The_log_is_not_part_of_the_match_checksum_in_phase_L1()
    {
        (MatchModel a, SimRunner ra) = Start();
        (MatchModel b, SimRunner rb) = Start();
        b.Legends.NoteDeed(AnAdult(b), LegendDeed.First, 500, 1);
        ra.Advance(150 * 4);
        rb.Advance(150 * 4);
        Assert.Equal(a.ComputeChecksum(), b.ComputeChecksum());
    }

    [Fact]
    public void Names_are_stable_and_cover_the_tables()
    {
        Assert.Equal(LegendNames.Compose(0), LegendNames.Compose(0));
        Assert.True(LegendNames.Count > 1000);
        var seen = new HashSet<string>(Enumerable.Range(0, LegendNames.Count).Select(LegendNames.Compose));
        Assert.True(seen.Count > 200, "many distinct names");
        Assert.Equal(LegendNames.Compose(0), LegendNames.Compose(-5)); // a bad code falls back, never throws
    }

    [Fact]
    public void The_frame_shows_the_viewers_Legends_only()
    {
        (MatchModel match, SimRunner runner) = Start();
        Grant(runner, match, "KEY-1");
        match.Legends.NoteDeed(AnAdult(match), LegendDeed.Founder, 300, 2);
        for (int i = 0; i < 40 && match.Legends.Records.Count == 0; i++) runner.Advance(150);
        Assert.True(match.Legends.Records.Count >= 1);
        var frame = new MatchFrame();
        new FrameBuilder(match, 0).Build(match, frame);
        Assert.Equal(match.Legends.Records.Count(r => r.Civilization == 0), frame.LegendCount);
        var other = new MatchFrame();
        new FrameBuilder(match, 1).Build(match, other);
        Assert.Equal(0, other.LegendCount);
    }

    [Fact]
    public void A_match_snapshot_carries_the_Legends_and_a_match_with_none_writes_the_same_bytes_as_before()
    {
        (MatchModel empty, _) = Start();
        using (var plain = new MemoryStream())
        {
            using (var w = new BinaryWriter(plain, System.Text.Encoding.UTF8, leaveOpen: true)) empty.WriteSnapshot(w);
            using var again = new MemoryStream();
            using (var w = new BinaryWriter(again, System.Text.Encoding.UTF8, leaveOpen: true)) empty.Legends.WriteOptionalTrailer(w);
            Assert.Equal(0, again.Length); // nothing to keep: no trailer
        }

        (MatchModel match, SimRunner runner) = Start();
        Grant(runner, match, "KEY-1");
        match.Legends.NoteDeed(AnAdult(match), LegendDeed.First, 400, 3);
        runner.Advance(150 * 30);
        Assert.True(match.Legends.Records.Count >= 1);
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) match.WriteSnapshot(w);
        ms.Position = 0;
        MatchModel restored = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        using (var r = new BinaryReader(ms)) restored.ReadSnapshot(r);
        var a = new StateHasher(); match.Legends.Hash(ref a);
        var b = new StateHasher(); restored.Legends.Hash(ref b);
        Assert.Equal(a.Finish(), b.Finish());
        Assert.Equal(match.Legends.Records.Count, restored.Legends.Records.Count);
    }
}
