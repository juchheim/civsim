using System;
using System.Numerics;

namespace CivSim.Sim.Numerics;

/// <summary>
/// Deterministic logarithm, exponential and power for <see cref="Fix64"/>, built only from integer arithmetic
/// and <see cref="Fix64.Sqrt"/> so every platform produces the same bits. Used for the sublinear Commons
/// Insight term (population^0.85, docs/MVP/02 §8.1) and for logistic spread. Accuracy is about 1e-8 relative,
/// far inside what the balance model needs.
/// </summary>
public static class FixMath
{
    /// <summary>Roots[k] = 2^(2^-k) for k = 1..32, from repeated square roots of 2.</summary>
    private static readonly Fix64[] Roots = BuildRoots();

    private static Fix64[] BuildRoots()
    {
        var roots = new Fix64[Fix64.FractionalBits + 1];
        Fix64 current = Fix64.FromInt(2);
        for (int k = 1; k <= Fix64.FractionalBits; k++)
        {
            current = Fix64.Sqrt(current);
            roots[k] = current;
        }
        return roots;
    }

    /// <summary>Base-2 logarithm of a positive value.</summary>
    public static Fix64 Log2(Fix64 x)
    {
        if (x.Raw <= 0) throw new ArgumentOutOfRangeException(nameof(x), "Logarithm of a non-positive number.");
        long raw = x.Raw;
        int msb = 63 - BitOperations.LeadingZeroCount((ulong)raw);
        int integerPart = msb - Fix64.FractionalBits;
        // Normalise to [1, 2): the most significant bit lands on bit 32.
        long y = msb >= Fix64.FractionalBits ? raw >> (msb - Fix64.FractionalBits) : raw << (Fix64.FractionalBits - msb);
        Fix64 mantissa = Fix64.FromRaw(y);
        long fraction = 0;
        for (int i = 1; i <= Fix64.FractionalBits; i++)
        {
            mantissa = mantissa * mantissa;
            if (mantissa.Raw >= 2 * Fix64.OneRaw)
            {
                mantissa = Fix64.FromRaw(mantissa.Raw >> 1);
                fraction |= 1L << (Fix64.FractionalBits - i);
            }
        }
        return Fix64.FromRaw(((long)integerPart << Fix64.FractionalBits) + fraction);
    }

    /// <summary>2 raised to <paramref name="v"/>. Throws on overflow, like all Fix64 arithmetic.</summary>
    public static Fix64 Exp2(Fix64 v)
    {
        long integerPart = v.Raw >> Fix64.FractionalBits; // floor
        long fraction = v.Raw & (Fix64.OneRaw - 1);
        Fix64 result = Fix64.One;
        for (int k = 1; k <= Fix64.FractionalBits; k++)
            if (((fraction >> (Fix64.FractionalBits - k)) & 1) != 0) result = result * Roots[k];
        if (integerPart >= 0)
        {
            if (integerPart > 30) throw new OverflowException("Exp2 overflow.");
            return Fix64.FromRaw(checked(result.Raw << (int)integerPart));
        }
        return integerPart < -62 ? Fix64.Zero : Fix64.FromRaw(result.Raw >> (int)(-integerPart));
    }

    /// <summary><paramref name="x"/> raised to <paramref name="p"/>, for x &gt;= 0. Zero to any power is zero.</summary>
    public static Fix64 Pow(Fix64 x, Fix64 p)
    {
        if (x.Raw < 0) throw new ArgumentOutOfRangeException(nameof(x));
        if (x.Raw == 0) return Fix64.Zero;
        if (p.Raw == 0) return Fix64.One;
        return Exp2(p * Log2(x));
    }

    /// <summary><paramref name="x"/> raised to a small non-negative integer power by repeated multiplication (exact).</summary>
    public static Fix64 PowInt(Fix64 x, int n)
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
        Fix64 result = Fix64.One;
        for (int i = 0; i < n; i++) result = result * x;
        return result;
    }
}
