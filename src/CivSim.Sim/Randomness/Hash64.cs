using System.Text;

namespace CivSim.Sim.Randomness;

/// <summary>
/// Fixed, platform-independent 64-bit hashing used to derive seeds and ids.
/// Never use <c>string.GetHashCode()</c> or default hash codes in simulation code: .NET randomizes them
/// per process (docs/MVP/15 rule D4).
/// </summary>
public static class Hash64
{
    /// <summary>The SplitMix64 finalizer: a strong, fast mix of one 64-bit value.</summary>
    public static ulong Mix(ulong x)
    {
        unchecked
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }
    }

    /// <summary>Combines a running hash with one more value. Order matters: Combine(a, b) != Combine(b, a).</summary>
    public static ulong Combine(ulong hash, ulong value)
    {
        unchecked { return Mix(hash + Mix(value)); }
    }

    /// <summary>FNV-1a 64-bit over the UTF-8 bytes of a string. Stable across processes and platforms.</summary>
    public static ulong OfString(string text)
    {
        unchecked
        {
            ulong h = 0xCBF29CE484222325UL;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                h ^= b;
                h *= 0x100000001B3UL;
            }
            return h;
        }
    }
}
