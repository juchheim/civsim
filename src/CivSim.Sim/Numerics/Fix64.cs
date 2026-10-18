using System;
using System.Globalization;

namespace CivSim.Sim.Numerics;

/// <summary>
/// Signed fixed-point number in Q32.32 format on a 64-bit integer: 32 integer bits, 32 fractional bits.
/// Range about ±2.1 billion, resolution 2^-32 (about 2.3e-10).
/// <para>
/// This is the only number type simulation state may use for fractional values (docs/MVP/15 rule D1).
/// Every operation is integer arithmetic, so results are identical on every CPU and platform.
/// </para>
/// <para>
/// Semantics (fixed forever; replays depend on them):
/// multiplication and division use a 128-bit intermediate and <b>truncate toward zero</b>;
/// any result outside the range throws <see cref="OverflowException"/> (a deterministic failure, never a wrap).
/// </para>
/// </summary>
public readonly struct Fix64 : IEquatable<Fix64>, IComparable<Fix64>
{
    public const int FractionalBits = 32;
    public const long OneRaw = 1L << FractionalBits;

    public static readonly Fix64 Zero = default;
    public static readonly Fix64 One = new(OneRaw);
    public static readonly Fix64 Half = new(OneRaw >> 1);
    public static readonly Fix64 MaxValue = new(long.MaxValue);
    public static readonly Fix64 MinValue = new(long.MinValue);
    /// <summary>The smallest positive value, 2^-32.</summary>
    public static readonly Fix64 Epsilon = new(1);

    /// <summary>The raw Q32.32 value. Exposed for hashing, serialization and tests.</summary>
    public readonly long Raw;

    private Fix64(long raw) => Raw = raw;

    // ---- construction -------------------------------------------------------------------

    public static Fix64 FromRaw(long raw) => new(raw);

    public static Fix64 FromInt(int value) => new((long)value << FractionalBits);

    public static Fix64 FromInt(long value)
    {
        if (value > int.MaxValue || value < int.MinValue)
            throw new OverflowException("Fix64 integer part must fit in 32 bits.");
        return new(value << FractionalBits);
    }

    /// <summary>The ratio <paramref name="numerator"/> / <paramref name="denominator"/>, truncated toward zero.</summary>
    public static Fix64 FromRatio(long numerator, long denominator)
    {
        if (denominator == 0) throw new DivideByZeroException();
        Int128 q = ((Int128)numerator << FractionalBits) / denominator;
        return new(checked((long)q));
    }

    /// <summary>
    /// Parses an invariant decimal such as "-12.375" exactly: no floating point is involved.
    /// Digits beyond what Q32.32 can hold are truncated toward zero. At most 18 fractional digits.
    /// </summary>
    public static Fix64 Parse(string text)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        ReadOnlySpan<char> s = text.AsSpan().Trim();
        if (s.IsEmpty) throw new FormatException("Empty Fix64 literal.");
        bool negative = false;
        int i = 0;
        if (s[0] == '-') { negative = true; i = 1; }
        else if (s[0] == '+') { i = 1; }

        long whole = 0;
        int wholeDigits = 0;
        for (; i < s.Length && s[i] >= '0' && s[i] <= '9'; i++, wholeDigits++)
        {
            whole = checked(whole * 10 + (s[i] - '0'));
            if (whole > int.MaxValue + 1L) throw new OverflowException("Fix64 literal out of range.");
        }

        long fraction = 0;
        long denominator = 1;
        int fractionDigits = 0;
        if (i < s.Length && s[i] == '.')
        {
            i++;
            for (; i < s.Length && s[i] >= '0' && s[i] <= '9'; i++, fractionDigits++)
            {
                if (fractionDigits >= 18) throw new FormatException("At most 18 fractional digits.");
                fraction = fraction * 10 + (s[i] - '0');
                denominator *= 10;
            }
        }
        if (i != s.Length || (wholeDigits == 0 && fractionDigits == 0))
            throw new FormatException($"Not a Fix64 literal: '{text}'.");

        Int128 magnitude = ((Int128)whole << FractionalBits) + (((Int128)fraction << FractionalBits) / denominator);
        Int128 signed = negative ? -magnitude : magnitude;
        return new(checked((long)signed));
    }

    // ---- arithmetic ---------------------------------------------------------------------

    public static Fix64 operator +(Fix64 a, Fix64 b) => new(checked(a.Raw + b.Raw));
    public static Fix64 operator -(Fix64 a, Fix64 b) => new(checked(a.Raw - b.Raw));
    public static Fix64 operator -(Fix64 a) => new(checked(-a.Raw));

    public static Fix64 operator *(Fix64 a, Fix64 b)
    {
        Int128 product = (Int128)a.Raw * b.Raw;
        return new(checked((long)ShiftTowardZero(product)));
    }

    public static Fix64 operator /(Fix64 a, Fix64 b)
    {
        if (b.Raw == 0) throw new DivideByZeroException();
        Int128 q = ((Int128)a.Raw << FractionalBits) / b.Raw;
        return new(checked((long)q));
    }

    /// <summary>Multiplies by a plain integer; exact, with no rounding.</summary>
    public static Fix64 operator *(Fix64 a, int n) => new(checked(a.Raw * n));
    public static Fix64 operator *(int n, Fix64 a) => new(checked(a.Raw * n));
    /// <summary>Divides by a plain integer, truncating toward zero.</summary>
    public static Fix64 operator /(Fix64 a, int n)
    {
        if (n == 0) throw new DivideByZeroException();
        return new(a.Raw / n);
    }

    private static Int128 ShiftTowardZero(Int128 value)
        => value >= 0 ? value >> FractionalBits : -((-value) >> FractionalBits);

    // ---- comparison ---------------------------------------------------------------------

    public static bool operator ==(Fix64 a, Fix64 b) => a.Raw == b.Raw;
    public static bool operator !=(Fix64 a, Fix64 b) => a.Raw != b.Raw;
    public static bool operator <(Fix64 a, Fix64 b) => a.Raw < b.Raw;
    public static bool operator >(Fix64 a, Fix64 b) => a.Raw > b.Raw;
    public static bool operator <=(Fix64 a, Fix64 b) => a.Raw <= b.Raw;
    public static bool operator >=(Fix64 a, Fix64 b) => a.Raw >= b.Raw;

    public bool Equals(Fix64 other) => Raw == other.Raw;
    public override bool Equals(object? obj) => obj is Fix64 other && Equals(other);
    public int CompareTo(Fix64 other) => Raw.CompareTo(other.Raw);
    // determinism-allow: value-based hash of a long, not a randomized string hash
    public override int GetHashCode() => unchecked((int)Raw ^ (int)(Raw >> 32));

    // ---- helpers ------------------------------------------------------------------------

    public int Sign => Raw > 0 ? 1 : Raw < 0 ? -1 : 0;
    public static Fix64 Abs(Fix64 a) => a.Raw < 0 ? -a : a;
    public static Fix64 Min(Fix64 a, Fix64 b) => a.Raw <= b.Raw ? a : b;
    public static Fix64 Max(Fix64 a, Fix64 b) => a.Raw >= b.Raw ? a : b;

    public static Fix64 Clamp(Fix64 value, Fix64 min, Fix64 max)
    {
        if (min.Raw > max.Raw) throw new ArgumentException("min must not exceed max.");
        return value.Raw < min.Raw ? min : value.Raw > max.Raw ? max : value;
    }

    /// <summary>Largest integer not above the value, as an integer.</summary>
    public int FloorToInt() => checked((int)(Raw >> FractionalBits));

    /// <summary>Integer part, truncated toward zero.</summary>
    public int TruncateToInt() => checked((int)(Raw / OneRaw));

    /// <summary>Nearest integer; halves round away from zero.</summary>
    public int RoundToInt()
        => Raw >= 0
            ? checked((int)((Raw + (OneRaw >> 1)) >> FractionalBits))
            : checked(-(int)((-Raw + (OneRaw >> 1)) >> FractionalBits));

    /// <summary>Square root, rounded down to the nearest representable value. The value must not be negative.</summary>
    public static Fix64 Sqrt(Fix64 value)
    {
        if (value.Raw < 0) throw new ArgumentOutOfRangeException(nameof(value), "Square root of a negative number.");
        // sqrt(raw / 2^32) * 2^32 = sqrt(raw * 2^32)
        return new(checked((long)IntegerSqrt((UInt128)(ulong)value.Raw << FractionalBits)));
    }

    /// <summary>Floor of the square root of <paramref name="n"/>, by integer Newton iteration.</summary>
    public static ulong IntegerSqrt(UInt128 n)
    {
        if (n == UInt128.Zero) return 0;
        int bits = 128 - (int)UInt128.LeadingZeroCount(n);
        UInt128 x = UInt128.One << ((bits + 1) / 2); // always >= floor(sqrt(n))
        while (true)
        {
            UInt128 y = (x + n / x) >> 1;
            if (y >= x) return checked((ulong)x);
            x = y;
        }
    }

    /// <summary>Exact decimal text with 9 fractional digits (truncated). Integer arithmetic only.</summary>
    public override string ToString()
    {
        bool negative = Raw < 0;
        ulong magnitude = negative ? unchecked((ulong)(-(Raw + 1)) + 1UL) : (ulong)Raw;
        ulong whole = magnitude >> FractionalBits;
        ulong fraction = magnitude & 0xFFFFFFFFUL;
        ulong nanos = (fraction * 1_000_000_000UL) >> FractionalBits;
        return (negative ? "-" : "") + whole.ToString(CultureInfo.InvariantCulture) + "."
               + nanos.ToString("D9", CultureInfo.InvariantCulture);
    }

    /// <summary>For display and tests only. Never feed the result back into simulation state.</summary>
    // determinism-allow: presentation-only conversion
    public double ToDoubleForDisplay() => Raw / 4294967296.0;
}
