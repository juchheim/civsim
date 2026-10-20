using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Research driving the real living economy (M4 on M3). Each match tick is costly, so these runs are short.</summary>
public class MatchModelTests
{
    private const ulong Seed = 20261019;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Archetypes =
        { EconomyArchetype.Grower, EconomyArchetype.Builder, EconomyArchetype.Sage, EconomyArchetype.Wanderer };

    private static MatchModel NewMatch() => MatchModel.Create(Seed, TestPack.Pack, World.Value, Archetypes);

    [Fact]
    public void Research_completions_are_applied_to_the_economy()
    {
        MatchModel match = NewMatch();
        var runner = new SimRunner(match);
        runner.Submit(new Command(0, 0, CommandKind.SetSlot, 0, match.Research.Catalog.IndexOf("FIR-01"), 0));
        runner.Advance(2400);
        ResearchCatalog catalog = match.Research.Catalog;
        for (int civ = 0; civ < 4; civ++)
        {
            Assert.True(match.Research.DoneCount(civ) >= 3, $"civ {civ} finished {match.Research.DoneCount(civ)}");
            foreach (ProjectDef p in catalog.Projects.Where(p => p.Numeric))
                Assert.Equal(match.Research.IsDone(civ, p.Index), match.Economy.HasCompletedProject(civ, p.Id));
        }
        // The player asked for fire first and the Council filled the rest after a season of grace.
        Assert.True(match.Research.IsDone(0, catalog.IndexOf("FIR-01")));
    }

    [Fact]
    public void The_match_is_deterministic_and_its_replay_verifies()
    {
        const long ticks = 1500;
        var commands = new[]
        {
            new Command(0, 0, CommandKind.SetSlot, 0, TestPack.Catalog.IndexOf("FOR-01"), 0),
            new Command(40, 0, CommandKind.QueueProject, TestPack.Catalog.IndexOf("SOC-01"), 0, 0),
            new Command(300, 1, CommandKind.SetLeaning, 2, 90, 0),
            new Command(600, 0, CommandKind.SetCouncil, 0, 0, 0),
        };
        Replay replay = Replay.Record(NewMatch(), Seed, ticks, commands, 50, TestPack.Pack.Hash);
        Assert.Equal(commands.Length, replay.Commands.Count);
        ReplayVerification verified = ReplayVerifier.Verify(replay, _ => NewMatch(), TestPack.Pack.Hash);
        Assert.True(verified.Ok, verified.Message);

        var tampered = new Replay
        {
            MatchSeed = replay.MatchSeed, ContentHash = replay.ContentHash, ChecksumInterval = replay.ChecksumInterval,
            TotalTicks = replay.TotalTicks, Checksums = replay.Checksums,
            Commands = replay.Commands.Where(c => c.Kind != CommandKind.SetLeaning).ToList(),
        };
        Assert.False(ReplayVerifier.Verify(tampered, _ => NewMatch(), TestPack.Pack.Hash).Ok);
    }

    [Fact]
    public void A_snapshot_resumes_to_the_same_checksum()
    {
        MatchModel straight = NewMatch();
        var straightRunner = new SimRunner(straight);
        straightRunner.Advance(1200);

        MatchModel first = NewMatch();
        var firstRunner = new SimRunner(first);
        firstRunner.Advance(600);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) first.WriteSnapshot(writer);
        stream.Position = 0;
        MatchModel resumed = NewMatch();
        using (var reader = new BinaryReader(stream)) resumed.ReadSnapshot(reader);
        Assert.Equal(first.ComputeChecksum(), resumed.ComputeChecksum());
        var resumedRunner = new SimRunner(resumed, 50, startTick: 600);
        resumedRunner.Advance(600);
        Assert.Equal(straight.ComputeChecksum(), resumed.ComputeChecksum());
    }

    [Fact]
    public void Gentle_start_protects_three_winters_and_defers_player_contact_until_KEY1()
    {
        var balanced = new[]
        {
            EconomyArchetype.Balanced, EconomyArchetype.Balanced,
            EconomyArchetype.Balanced, EconomyArchetype.Balanced,
        };
        var councils = new[]
        {
            CivControl.Council, CivControl.Council, CivControl.Council, CivControl.Council,
        };
        MatchModel match = MatchModel.Create(Seed, TestPack.Pack, World.Value, balanced, councils,
            playerCouncil: true, gentleStart: true);
        var runner = new SimRunner(match);
        for (int season = 0; season < 12; season++)
        {
            runner.Advance(ResearchSystem.SeasonTicks);
            if (season % 4 == 3) Assert.False(match.Economy.LastWinterWasHarsh, $"winter in season {season} was harsh");
        }

        int key1 = match.Research.Catalog.IndexOf("KEY-1");
        while (!match.Research.IsDone(0, key1) && runner.Tick < 5_700)
            runner.Advance(ResearchSystem.SeasonTicks);
        Assert.True(match.Research.IsDone(0, key1));
        long key1Tick = match.Research.CompletedTick(0, key1);
        Assert.DoesNotContain(match.Politics.Events, e => e.Kind == CivSim.Sim.Politics.PoliticsEventKind.Contact &&
            (e.Civ == 0 || e.Other == 0) && e.Tick < key1Tick);
    }

    [Fact]
    public void KEY_1_lands_inside_the_C5_window_for_every_Balanced_civilization_and_every_completion_is_visible()
    {
        // C5 (docs/MVP/11 §6): KEY-1 at about 7.5 minutes, +/-25%. KEY-2 and KEY-3 are checked by the harness
        // (`research --check-c5`, docs/MVP/07c §7a), which needs a 40 minute run.
        var balanced = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        MatchModel match = MatchModel.Create(Seed, TestPack.Pack, World.Value, balanced);
        var runner = new SimRunner(match);
        runner.Advance((long)(9.5 * 600));
        int key1 = match.Research.Catalog.IndexOf("KEY-1");
        for (int civ = 0; civ < 4; civ++)
        {
            Assert.True(match.Research.IsDone(civ, key1), $"civ {civ} had not reached KEY-1 by minute 9.5");
            double minutes = match.Research.CompletedTick(civ, key1) / 600.0;
            Assert.InRange(minutes, 7.5 * 0.75, 7.5 * 1.25);
            Assert.Equal(1, match.Research.Era(civ));
        }
        // C8 in the event stream: each completion is announced with its Moment and a visible change in the same tick.
        foreach (ResearchEvent done in match.Research.Events.Where(e => e.Kind == ResearchEventKind.Completed))
        {
            Assert.Contains(match.Research.Events, e => e.Kind == ResearchEventKind.Moment && e.Civ == done.Civ && e.Project == done.Project && e.Tick == done.Tick);
            Assert.Contains(match.Research.Events, e => e.Kind == ResearchEventKind.VisibleChange && e.Civ == done.Civ && e.Project == done.Project && e.Tick == done.Tick);
        }
        Assert.True(match.Research.Events.Count(e => e.Kind == ResearchEventKind.Completed) >= 40);
    }
}
