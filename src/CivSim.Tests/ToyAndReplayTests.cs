using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Toy;

namespace CivSim.Tests;

public class ToyAndReplayTests
{
    private static SimRunner RunToy(ulong seed, long ticks, int interval = 50)
    {
        var runner = new SimRunner(new ToyModel(seed), interval);
        foreach (Command c in ToyScenario.Commands(seed, ticks)) runner.Submit(c);
        runner.Advance(ticks);
        return runner;
    }

    [Fact]
    public void Same_seed_and_commands_give_identical_checksums()
    {
        SimRunner a = RunToy(42, 2000), b = RunToy(42, 2000);
        Assert.Equal(a.Checksums, b.Checksums);
        Assert.Equal(a.CurrentChecksum, b.CurrentChecksum);
        Assert.Equal(a.Digest(), b.Digest());
    }

    [Fact]
    public void A_different_seed_diverges()
    {
        Assert.NotEqual(RunToy(1, 1000).Digest(), RunToy(2, 1000).Digest());
    }

    [Fact]
    public void A_different_command_changes_the_outcome()
    {
        SimRunner plain = RunToy(5, 1500);
        var changed = new SimRunner(new ToyModel(5));
        foreach (Command c in ToyScenario.Commands(5, 1500)) changed.Submit(c);
        changed.Submit(new Command(10, 0, CommandKind.SetPosture, (int)Posture.Expand, 0, 0));
        changed.Advance(1500);
        Assert.NotEqual(plain.CurrentChecksum, changed.CurrentChecksum);
    }

    [Fact]
    public void The_model_actually_does_something()
    {
        var model = new ToyModel(11);
        var runner = new SimRunner(model);
        runner.Advance(1500);
        for (int civ = 0; civ < ToyModel.Civs; civ++)
        {
            Assert.True(model.Population(civ).Raw > 0);
            Assert.True(model.OwnedTiles(civ) > 0, $"civ {civ} owns no tiles");
        }
    }

    [Fact]
    public void Golden_checksum_is_unchanged()
    {
        // The same file is checked by CI on macOS and Windows, which is the cross-platform determinism proof.
        string expected = File.ReadAllText(RepoPaths.Golden("toy-v1.txt")).Replace("\r\n", "\n").Trim();
        SimRunner runner = RunToy(ToyScenario.GoldenSeed, ToyScenario.GoldenTicks);
        string actual = string.Join('\n',
            $"seed={ToyScenario.GoldenSeed} ticks={ToyScenario.GoldenTicks} checksumInterval={SimRunner.DefaultChecksumInterval} commands={runner.AppliedCommands.Count}",
            $"final={runner.CurrentChecksum:x16}",
            $"digest={runner.Digest():x16}");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Resuming_from_a_snapshot_matches_an_uninterrupted_run()
    {
        const ulong seed = 99;
        const long half = 1500, total = 3000;
        List<Command> script = ToyScenario.Commands(seed, total);

        var straightModel = new ToyModel(seed);
        var straight = new SimRunner(straightModel);
        foreach (Command c in script) straight.Submit(c);
        straight.Advance(total);

        var firstModel = new ToyModel(seed);
        var first = new SimRunner(firstModel);
        foreach (Command c in script) first.Submit(c);
        first.Advance(half);

        byte[] snapshot;
        using (var ms = new MemoryStream())
        {
            using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) firstModel.WriteSnapshot(w);
            snapshot = ms.ToArray();
        }

        var resumedModel = new ToyModel(seed);
        using (var ms = new MemoryStream(snapshot))
        using (var r = new BinaryReader(ms)) resumedModel.ReadSnapshot(r);
        Assert.Equal(firstModel.ComputeChecksum(), resumedModel.ComputeChecksum());

        var resumed = new SimRunner(resumedModel, SimRunner.DefaultChecksumInterval, startTick: half);
        foreach (Command c in script) if (c.Tick >= half) resumed.Submit(c);
        resumed.Advance(total - half);

        Assert.Equal(straight.CurrentChecksum, resumed.CurrentChecksum);
        Assert.Equal(straightModel.Population(0), resumedModel.Population(0));
    }

    [Fact]
    public void A_snapshot_from_another_seed_is_rejected()
    {
        var model = new ToyModel(1);
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) model.WriteSnapshot(w);
        ms.Position = 0;
        using var r = new BinaryReader(ms);
        Assert.Throws<InvalidDataException>(() => new ToyModel(2).ReadSnapshot(r));
    }

    [Fact]
    public void Replay_round_trips_and_verifies()
    {
        Replay replay = Replay.Record(new ToyModel(7), 7, 1200, ToyScenario.Commands(7, 1200));
        using var ms = new MemoryStream();
        replay.Write(ms);
        ms.Position = 0;
        Replay loaded = Replay.Read(ms);

        Assert.Equal(replay.MatchSeed, loaded.MatchSeed);
        Assert.Equal(replay.TotalTicks, loaded.TotalTicks);
        Assert.Equal(replay.Commands, loaded.Commands);
        Assert.Equal(replay.Checksums, loaded.Checksums);

        ReplayVerification result = ReplayVerifier.Verify(loaded, seed => new ToyModel(seed));
        Assert.True(result.Ok, result.Message);
    }

    [Fact]
    public void Writing_the_same_replay_twice_gives_identical_bytes()
    {
        byte[] Bytes()
        {
            Replay replay = Replay.Record(new ToyModel(3), 3, 600, ToyScenario.Commands(3, 600));
            using var ms = new MemoryStream();
            replay.Write(ms);
            return ms.ToArray();
        }
        Assert.Equal(Bytes(), Bytes());
    }

    [Fact]
    public void A_tampered_replay_fails_verification_at_the_right_place()
    {
        Replay good = Replay.Record(new ToyModel(7), 7, 1200, ToyScenario.Commands(7, 1200));
        var commands = new List<Command>(good.Commands);
        int index = commands.FindIndex(c => c.Kind == CommandKind.PlaceBeacon && c.Tick > 100);
        Command original = commands[index];
        commands[index] = original with { A = (original.A + 7) % ToyModel.Width };
        var bad = new Replay
        {
            MatchSeed = good.MatchSeed,
            ChecksumInterval = good.ChecksumInterval,
            TotalTicks = good.TotalTicks,
            Commands = commands,
            Checksums = good.Checksums,
        };
        ReplayVerification result = ReplayVerifier.Verify(bad, seed => new ToyModel(seed));
        Assert.False(result.Ok);
        Assert.True(result.FirstMismatchTick > original.Tick, "divergence should appear at or after the tampered command");
        Assert.True(result.FirstMismatchTick <= good.TotalTicks);
    }

    [Fact]
    public void Corrupt_replay_files_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() => Replay.Read(new MemoryStream(new byte[64])));
        Replay replay = Replay.Record(new ToyModel(1), 1, 100, ToyScenario.Commands(1, 100));
        using var ms = new MemoryStream();
        replay.Write(ms);
        byte[] truncated = ms.ToArray()[..60];
        Assert.ThrowsAny<IOException>(() => Replay.Read(new MemoryStream(truncated)));
    }
}
