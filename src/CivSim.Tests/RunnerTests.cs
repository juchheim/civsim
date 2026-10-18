using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;

namespace CivSim.Tests;

public class RunnerTests
{
    private sealed class RecordingModel : ISimModel
    {
        public readonly List<(long Tick, Command[] Commands)> Steps = new();
        public void Step(long tick, ReadOnlySpan<Command> commands) => Steps.Add((tick, commands.ToArray()));
        public ulong ComputeChecksum() => (ulong)Steps.Count;
        public void WriteSnapshot(BinaryWriter writer) { }
        public void ReadSnapshot(BinaryReader reader) { }
    }

    private static Command Cmd(long tick, int a = 0, byte civ = 0) => new(tick, civ, CommandKind.PlaceBeacon, a, 0, 0);

    [Fact]
    public void Steps_every_tick_in_order_and_advances_the_counter()
    {
        var model = new RecordingModel();
        var runner = new SimRunner(model);
        runner.Advance(5);
        Assert.Equal(5, runner.Tick);
        Assert.Equal(new long[] { 0, 1, 2, 3, 4 }, model.Steps.ConvertAll(s => s.Tick));
    }

    [Fact]
    public void Commands_arrive_on_their_tick_in_submission_order()
    {
        var model = new RecordingModel();
        var runner = new SimRunner(model);
        runner.Submit(Cmd(3, a: 1));
        runner.Submit(Cmd(1, a: 2));
        runner.Submit(Cmd(3, a: 3)); // same tick as the first: must come after it
        runner.Submit(Cmd(1, a: 4));
        runner.Advance(5);

        Assert.Empty(model.Steps[0].Commands);
        Assert.Equal(new[] { 2, 4 }, Array.ConvertAll(model.Steps[1].Commands, c => c.A));
        Assert.Empty(model.Steps[2].Commands);
        Assert.Equal(new[] { 1, 3 }, Array.ConvertAll(model.Steps[3].Commands, c => c.A));
        Assert.Equal(4, runner.AppliedCommands.Count);
    }

    [Fact]
    public void Submitting_for_a_past_tick_is_rejected()
    {
        var runner = new SimRunner(new RecordingModel());
        runner.Advance(10);
        Assert.Throws<ArgumentException>(() => runner.Submit(Cmd(9)));
        runner.Submit(Cmd(10)); // the next tick is fine
    }

    [Fact]
    public void Checksums_are_taken_on_the_interval()
    {
        var runner = new SimRunner(new RecordingModel(), checksumInterval: 50);
        runner.Advance(120);
        Assert.Equal(new long[] { 50, 100 }, new List<ChecksumRecord>(runner.Checksums).ConvertAll(c => c.Tick));
    }

    [Fact]
    public void A_resumed_runner_continues_the_tick_count_and_cadence()
    {
        var model = new RecordingModel();
        var runner = new SimRunner(model, checksumInterval: 50, startTick: 90);
        runner.Advance(20);
        Assert.Equal(110, runner.Tick);
        Assert.Equal(90L, model.Steps[0].Tick);
        Assert.Equal(100L, Assert.Single(runner.Checksums).Tick);
    }

    [Fact]
    public void Invalid_arguments_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimRunner(new RecordingModel(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimRunner(new RecordingModel(), 50, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimRunner(new RecordingModel()).Advance(-1));
    }
}
