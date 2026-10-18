using System;
using System.Collections.Generic;

namespace CivSim.Sim.Core;

/// <summary>A checksum taken after <see cref="Tick"/> ticks have completed.</summary>
public readonly record struct ChecksumRecord(long Tick, ulong Hash);

/// <summary>
/// The fixed-step loop: schedules commands, steps the model one tick at a time, and records a checksum every
/// <see cref="ChecksumInterval"/> ticks (docs/MVP/15 §5.5 and rule D8). It has no notion of time: how fast
/// ticks are produced is the caller's business, which keeps the simulation deterministic at any speed.
/// </summary>
public sealed class SimRunner
{
    public const int DefaultChecksumInterval = 50;

    private readonly ISimModel _model;
    private readonly List<Command> _pending = new();
    private readonly List<Command> _applied = new();
    private readonly List<ChecksumRecord> _checksums = new();
    private readonly List<Command> _scratch = new();

    /// <param name="startTick">The tick the model is at: 0 for a new match, or the snapshot's tick when resuming.</param>
    public SimRunner(ISimModel model, int checksumInterval = DefaultChecksumInterval, long startTick = 0)
    {
        if (checksumInterval <= 0) throw new ArgumentOutOfRangeException(nameof(checksumInterval));
        if (startTick < 0) throw new ArgumentOutOfRangeException(nameof(startTick));
        _model = model;
        ChecksumInterval = checksumInterval;
        Tick = startTick;
    }

    public int ChecksumInterval { get; }

    /// <summary>The next tick to be simulated; equals the number of ticks completed.</summary>
    public long Tick { get; private set; }

    public IReadOnlyList<ChecksumRecord> Checksums => _checksums;

    /// <summary>Every command applied so far, in the order applied: the command log of a replay.</summary>
    public IReadOnlyList<Command> AppliedCommands => _applied;

    public ulong CurrentChecksum => _model.ComputeChecksum();

    /// <summary>
    /// One value folding every periodic checksum recorded so far, so a divergence anywhere in the run changes
    /// it, not only a divergence at the end.
    /// </summary>
    public ulong Digest()
    {
        ulong digest = 0;
        foreach (ChecksumRecord record in _checksums)
            digest = Randomness.Hash64.Combine(Randomness.Hash64.Combine(digest, (ulong)record.Tick), record.Hash);
        return digest;
    }

    /// <summary>
    /// Schedules a command for <see cref="Command.Tick"/>, which must not be in the past. Commands for the
    /// same tick are applied in submission order.
    /// </summary>
    public void Submit(Command command)
    {
        if (command.Tick < Tick)
            throw new ArgumentException($"Command for tick {command.Tick} submitted at tick {Tick}.", nameof(command));
        int index = _pending.Count;
        while (index > 0 && _pending[index - 1].Tick > command.Tick) index--; // stable: after equal ticks
        _pending.Insert(index, command);
    }

    public void Advance(long ticks)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
        for (long i = 0; i < ticks; i++) StepOnce();
    }

    private void StepOnce()
    {
        _scratch.Clear();
        int taken = 0;
        while (taken < _pending.Count && _pending[taken].Tick == Tick)
        {
            _scratch.Add(_pending[taken]);
            taken++;
        }
        if (taken > 0)
        {
            _pending.RemoveRange(0, taken);
            _applied.AddRange(_scratch);
        }
        _model.Step(Tick, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_scratch));
        Tick++;
        if (Tick % ChecksumInterval == 0) _checksums.Add(new ChecksumRecord(Tick, _model.ComputeChecksum()));
    }
}
