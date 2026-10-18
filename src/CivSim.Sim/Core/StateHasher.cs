using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;

namespace CivSim.Sim.Core;

/// <summary>
/// Order-sensitive 64-bit fingerprint of simulation state, used for desync detection and replay checks
/// (docs/MVP/15 rule D8). Not cryptographic. Feed state in a fixed order (ascending entity id).
/// </summary>
public struct StateHasher
{
    private ulong _hash;
    private bool _started;

    public void Add(ulong value)
    {
        if (!_started) { _hash = 0x243F6A8885A308D3UL; _started = true; }
        _hash = Hash64.Combine(_hash, value);
    }

    public void Add(long value) => Add(unchecked((ulong)value));
    public void Add(int value) => Add(unchecked((ulong)(long)value));
    public void Add(uint value) => Add((ulong)value);
    public void Add(byte value) => Add((ulong)value);
    public void Add(bool value) => Add(value ? 1UL : 0UL);
    public void Add(Fix64 value) => Add(value.Raw);

    public void Add(System.ReadOnlySpan<int> values)
    {
        Add(values.Length);
        foreach (int v in values) Add(v);
    }

    public void Add(System.ReadOnlySpan<Fix64> values)
    {
        Add(values.Length);
        foreach (Fix64 v in values) Add(v.Raw);
    }

    public readonly ulong Finish() => Hash64.Mix(_started ? _hash : 0UL);
}
