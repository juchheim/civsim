using System;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;

namespace CivSim.Sim.Toy;

/// <summary>
/// A miniature stand-in for the real world, kept permanently as a <b>determinism canary</b>: if its golden
/// checksum changes, the engine-level plumbing (Fix64, RNG streams, ordering, snapshots, replays) changed,
/// independent of any game-system work. It exercises the same patterns the real simulation will use:
/// per-entity RNG streams keyed by (seed, system, entity, tick), ascending-id iteration, Fix64 arithmetic
/// including square roots, a claim-field-and-ownership rule like docs/MVP/05 §11, commands, and snapshots.
/// <para>
/// World: four civilizations, each with 64 walking figures on a 64 x 40 grid. Figures leave claim marks;
/// each season the marks decay, tiles are owned by the strongest clear leader, and population grows with
/// the owned land (and suffers random harsh winters).
/// </para>
/// </summary>
public sealed class ToyModel : ISimModel
{
    public const int Width = 64;
    public const int Height = 40;
    public const int Civs = 4;
    public const int FiguresPerCiv = 64;
    public const int TileCount = Width * Height;
    public const int SeasonTicks = 150;

    private static readonly ulong WalkStream = Hash64.OfString("toy.walk");
    private static readonly ulong WinterStream = Hash64.OfString("toy.winter");

    private static readonly Fix64 OwnThreshold = Fix64.FromRatio(7, 10);   // like T = 0.7 (docs/MVP/07b)
    private static readonly Fix64 LeadMargin = Fix64.FromRatio(12, 10);    // owner leads runner-up by 20%
    private static readonly Fix64 Decay = Fix64.FromRatio(9, 10);
    private static readonly Fix64 PopulationCap = Fix64.FromInt(1000);

    private readonly ulong _seed;
    private long _tick;
    private readonly int[] _figureX = new int[Civs * FiguresPerCiv];
    private readonly int[] _figureY = new int[Civs * FiguresPerCiv];
    private readonly Fix64[] _claim = new Fix64[Civs * TileCount];
    private readonly Fix64[] _population = new Fix64[Civs];
    private readonly byte[] _posture = new byte[Civs];
    private readonly bool[] _beaconOn = new bool[Civs];
    private readonly int[] _beaconX = new int[Civs];
    private readonly int[] _beaconY = new int[Civs];
    private readonly int[] _owned = new int[Civs];

    public ToyModel(ulong seed)
    {
        _seed = seed;
        for (int civ = 0; civ < Civs; civ++)
        {
            int startX = 8 + (civ % 2) * 48;
            int startY = 8 + (civ / 2) * 24;
            for (int f = 0; f < FiguresPerCiv; f++)
            {
                _figureX[civ * FiguresPerCiv + f] = startX;
                _figureY[civ * FiguresPerCiv + f] = startY;
            }
            _population[civ] = Fix64.FromInt(12);
        }
    }

    public long Tick => _tick;
    public Fix64 Population(int civ) => _population[civ];
    public int OwnedTiles(int civ) => _owned[civ];

    public void Step(long tick, ReadOnlySpan<Command> commands)
    {
        if (tick != _tick) throw new InvalidOperationException($"Expected tick {_tick}, got {tick}.");

        foreach (Command c in commands) Apply(c);

        for (int i = 0; i < _figureX.Length; i++) // ascending entity id (rule D5)
        {
            int civ = i / FiguresPerCiv;
            Pcg32 rng = Pcg32.ForStream(_seed, WalkStream, (ulong)i, tick);
            int steps = _posture[civ] == (byte)Posture.Expand ? 2 : 1;
            for (int s = 0; s < steps; s++) MoveFigure(i, civ, ref rng);
            int tile = _figureY[i] * Width + _figureX[i];
            _claim[civ * TileCount + tile] += Fix64.One;
        }

        _tick++;
        if (_tick % SeasonTicks == 0) EndSeason();
    }

    private void Apply(Command c)
    {
        if (c.Civ >= Civs) return; // ignored, deterministically
        switch (c.Kind)
        {
            case CommandKind.PlaceBeacon:
                _beaconOn[c.Civ] = true;
                _beaconX[c.Civ] = Math.Clamp(c.A, 0, Width - 1);
                _beaconY[c.Civ] = Math.Clamp(c.B, 0, Height - 1);
                break;
            case CommandKind.RemoveBeacon:
                _beaconOn[c.Civ] = false;
                break;
            case CommandKind.SetPosture:
                if (c.A >= 0 && c.A <= (int)Posture.Raid) _posture[c.Civ] = (byte)c.A;
                break;
        }
    }

    private void MoveFigure(int i, int civ, ref Pcg32 rng)
    {
        int dx = 0, dy = 0;
        if (_beaconOn[civ] && rng.NextBelow(2) == 0)
        {
            if (_figureX[i] != _beaconX[civ]) dx = Math.Sign(_beaconX[civ] - _figureX[i]);
            else if (_figureY[i] != _beaconY[civ]) dy = Math.Sign(_beaconY[civ] - _figureY[i]);
        }
        else
        {
            switch (rng.NextBelow(4))
            {
                case 0: dx = 1; break;
                case 1: dx = -1; break;
                case 2: dy = 1; break;
                default: dy = -1; break;
            }
        }
        _figureX[i] = Math.Clamp(_figureX[i] + dx, 0, Width - 1);
        _figureY[i] = Math.Clamp(_figureY[i] + dy, 0, Height - 1);
    }

    private void EndSeason()
    {
        for (int i = 0; i < _claim.Length; i++) _claim[i] *= Decay;

        for (int civ = 0; civ < Civs; civ++) _owned[civ] = 0;
        for (int tile = 0; tile < TileCount; tile++)
        {
            int best = -1;
            Fix64 top = Fix64.Zero, second = Fix64.Zero;
            for (int civ = 0; civ < Civs; civ++)
            {
                Fix64 value = _claim[civ * TileCount + tile];
                if (value > top) { second = top; top = value; best = civ; }
                else if (value > second) { second = value; }
            }
            if (best >= 0 && top >= OwnThreshold && top >= second * LeadMargin) _owned[best]++;
        }

        long season = _tick / SeasonTicks;
        for (int civ = 0; civ < Civs; civ++)
        {
            Pcg32 rng = Pcg32.ForStream(_seed, WinterStream, (ulong)civ, season);
            Fix64 pop = _population[civ];
            if (rng.NextChance(3, 10))
            {
                pop *= Fix64.FromRatio(8, 10); // harsh winter
            }
            else
            {
                Fix64 growth = _posture[civ] == (byte)Posture.Expand ? Fix64.FromRatio(12, 100) : Fix64.FromRatio(8, 100);
                pop += pop * growth;
            }
            pop += Fix64.Sqrt(Fix64.FromInt(_owned[civ]));
            _population[civ] = Fix64.Min(pop, PopulationCap);
        }
    }

    public ulong ComputeChecksum()
    {
        var h = new StateHasher();
        h.Add(_tick);
        h.Add(_figureX);
        h.Add(_figureY);
        h.Add(_claim);
        h.Add(_population);
        for (int civ = 0; civ < Civs; civ++)
        {
            h.Add(_posture[civ]);
            h.Add(_beaconOn[civ]);
            h.Add(_beaconX[civ]);
            h.Add(_beaconY[civ]);
            h.Add(_owned[civ]);
        }
        return h.Finish();
    }

    public void WriteSnapshot(BinaryWriter writer)
    {
        writer.Write(_seed);
        writer.Write(_tick);
        foreach (int v in _figureX) writer.Write(v);
        foreach (int v in _figureY) writer.Write(v);
        foreach (Fix64 v in _claim) writer.Write(v.Raw);
        foreach (Fix64 v in _population) writer.Write(v.Raw);
        writer.Write(_posture);
        foreach (bool v in _beaconOn) writer.Write(v);
        foreach (int v in _beaconX) writer.Write(v);
        foreach (int v in _beaconY) writer.Write(v);
        foreach (int v in _owned) writer.Write(v);
    }

    public void ReadSnapshot(BinaryReader reader)
    {
        if (reader.ReadUInt64() != _seed) throw new InvalidDataException("Snapshot was taken with a different seed.");
        _tick = reader.ReadInt64();
        for (int i = 0; i < _figureX.Length; i++) _figureX[i] = reader.ReadInt32();
        for (int i = 0; i < _figureY.Length; i++) _figureY[i] = reader.ReadInt32();
        for (int i = 0; i < _claim.Length; i++) _claim[i] = Fix64.FromRaw(reader.ReadInt64());
        for (int i = 0; i < _population.Length; i++) _population[i] = Fix64.FromRaw(reader.ReadInt64());
        byte[] posture = reader.ReadBytes(Civs);
        if (posture.Length != Civs) throw new InvalidDataException("Truncated snapshot.");
        Array.Copy(posture, _posture, Civs);
        for (int i = 0; i < Civs; i++) _beaconOn[i] = reader.ReadBoolean();
        for (int i = 0; i < Civs; i++) _beaconX[i] = reader.ReadInt32();
        for (int i = 0; i < Civs; i++) _beaconY[i] = reader.ReadInt32();
        for (int i = 0; i < Civs; i++) _owned[i] = reader.ReadInt32();
    }
}
