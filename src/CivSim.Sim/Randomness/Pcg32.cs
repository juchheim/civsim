using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Randomness;

/// <summary>
/// PCG32 (XSH-RR, 64-bit state, 32-bit output): small, fast and fully specified, so every platform
/// produces the same stream. Simulation code takes a fresh stream per (system, entity, tick) with
/// <see cref="ForStream"/> instead of sharing one generator, so results never depend on call order
/// (docs/MVP/15 rule D3).
/// </summary>
public struct Pcg32
{
    private const ulong Multiplier = 6364136223846793005UL;
    private ulong _state;
    private ulong _increment;

    /// <summary>The reference seeding procedure (pcg32_srandom). <c>Seed(42, 54)</c> is the published test stream.</summary>
    public static Pcg32 Seed(ulong initialState, ulong sequence)
    {
        unchecked
        {
            var rng = new Pcg32 { _state = 0, _increment = (sequence << 1) | 1UL };
            rng.NextUInt();
            rng._state += initialState;
            rng.NextUInt();
            return rng;
        }
    }

    /// <summary>
    /// The stream for one system acting on one entity at one tick. <paramref name="system"/> is a stable
    /// id such as <c>Hash64.OfString("walk")</c>; <paramref name="entity"/> is the entity id (or 0).
    /// </summary>
    public static Pcg32 ForStream(ulong matchSeed, ulong system, ulong entity, long tick)
    {
        unchecked
        {
            ulong h = Hash64.Combine(Hash64.Combine(Hash64.Combine(matchSeed, system), entity), (ulong)tick);
            return Seed(Hash64.Mix(h), Hash64.Mix(h ^ 0xA5A5A5A5A5A5A5A5UL));
        }
    }

    public uint NextUInt()
    {
        unchecked
        {
            ulong old = _state;
            _state = old * Multiplier + _increment;
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }
    }

    /// <summary>Uniform integer in [0, <paramref name="bound"/>), without modulo bias.</summary>
    public uint NextBelow(uint bound)
    {
        if (bound == 0) throw new ArgumentOutOfRangeException(nameof(bound));
        unchecked
        {
            uint threshold = (0u - bound) % bound;
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return r % bound;
            }
        }
    }

    /// <summary>Uniform integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return minInclusive + (int)NextBelow(checked((uint)(maxExclusive - minInclusive)));
    }

    /// <summary>Uniform value in [0, 1) with 32 bits of resolution.</summary>
    public Fix64 NextFix01() => Fix64.FromRaw(NextUInt());

    /// <summary>True with probability numerator / denominator (exact, integer arithmetic).</summary>
    public bool NextChance(uint numerator, uint denominator)
    {
        if (denominator == 0) throw new ArgumentOutOfRangeException(nameof(denominator));
        return NextBelow(denominator) < numerator;
    }
}
