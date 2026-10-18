using System;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;

namespace CivSim.Tests;

public class Fix64Tests
{
    [Fact]
    public void Constants_have_expected_raw_values()
    {
        Assert.Equal(0L, Fix64.Zero.Raw);
        Assert.Equal(1L << 32, Fix64.One.Raw);
        Assert.Equal(1L << 31, Fix64.Half.Raw);
        Assert.Equal(1L, Fix64.Epsilon.Raw);
        Assert.Equal(7L << 32, Fix64.FromInt(7).Raw);
    }

    [Fact]
    public void Addition_and_subtraction_are_exact()
    {
        Fix64 a = Fix64.FromRatio(3, 2), b = Fix64.FromRatio(5, 4);
        Assert.Equal(Fix64.FromRatio(11, 4), a + b);
        Assert.Equal(Fix64.FromRatio(1, 4), a - b);
        Assert.Equal(Fix64.FromRatio(-3, 2), -a);
    }

    [Fact]
    public void Multiplication_and_division_truncate_toward_zero()
    {
        Fix64 third = Fix64.FromRatio(1, 3);
        Assert.Equal(1431655765L, third.Raw);
        Assert.Equal(Fix64.OneRaw - 1, (Fix64.FromInt(3) * third).Raw);          // 0.99999999976...
        Assert.Equal(third, Fix64.One / Fix64.FromInt(3));
        Assert.Equal(Fix64.FromRatio(-1, 3), Fix64.FromInt(-1) / Fix64.FromInt(3)); // toward zero, not floor
        Assert.Equal(Fix64.FromInt(6), Fix64.FromInt(2) * Fix64.FromInt(3));
        Assert.Equal(Fix64.FromRatio(3, 4), Fix64.FromRatio(3, 2) * Fix64.Half);
    }

    [Fact]
    public void Multiplication_is_sign_symmetric()
    {
        var rng = Pcg32.Seed(1, 1);
        for (int i = 0; i < 2000; i++)
        {
            var a = Fix64.FromRaw((long)rng.NextUInt() - (1L << 31));
            var b = Fix64.FromRaw((long)rng.NextUInt() - (1L << 31));
            Assert.Equal(-(a * b), (-a) * b);
            Assert.Equal(-(a * b), a * (-b));
        }
    }

    [Fact]
    public void Integer_operators_are_exact()
    {
        Fix64 x = Fix64.FromRatio(5, 2);
        Assert.Equal(Fix64.FromInt(10), x * 4);
        Assert.Equal(Fix64.FromInt(10), 4 * x);
        Assert.Equal(Fix64.FromRatio(5, 4), x / 2);
    }

    [Fact]
    public void Overflow_and_division_by_zero_throw()
    {
        Assert.Throws<OverflowException>(() => Fix64.MaxValue + Fix64.One);
        Assert.Throws<OverflowException>(() => Fix64.MinValue - Fix64.One);
        Assert.Throws<OverflowException>(() => -Fix64.MinValue);
        Assert.Throws<OverflowException>(() => Fix64.MaxValue * Fix64.FromInt(2));
        Assert.Throws<OverflowException>(() => Fix64.FromInt((long)int.MaxValue + 1));
        Assert.Throws<DivideByZeroException>(() => Fix64.One / Fix64.Zero);
        Assert.Throws<DivideByZeroException>(() => Fix64.FromRatio(1, 0));
    }

    [Fact]
    public void Comparison_and_helpers()
    {
        Fix64 one = Fix64.One, two = Fix64.FromInt(2);
        Assert.True(one < two);
        Assert.True(two >= one);
        Assert.True(one <= one + Fix64.Zero);
        Assert.Equal(one, Fix64.Min(one, two));
        Assert.Equal(two, Fix64.Max(one, two));
        Assert.Equal(Fix64.Half, Fix64.Abs(-Fix64.Half));
        Assert.Equal(one, Fix64.Clamp(Fix64.Zero, one, two));
        Assert.Equal(two, Fix64.Clamp(Fix64.FromInt(9), one, two));
        Assert.Equal(-1, (-one).Sign);
        Assert.Equal(0, Fix64.Zero.Sign);
        Assert.Throws<ArgumentException>(() => Fix64.Clamp(one, two, one));
    }

    [Theory]
    [InlineData(5, 2, 2, 2, 3)]      // 2.5: floor 2, trunc 2, round 3
    [InlineData(-5, 2, -3, -2, -3)]  // -2.5: floor -3, trunc -2, round -3 (away from zero)
    [InlineData(12, 5, 2, 2, 2)]     // 2.4
    [InlineData(-12, 5, -3, -2, -2)] // -2.4
    public void Rounding(long num, long den, int floor, int trunc, int round)
    {
        Fix64 v = Fix64.FromRatio(num, den);
        Assert.Equal(floor, v.FloorToInt());
        Assert.Equal(trunc, v.TruncateToInt());
        Assert.Equal(round, v.RoundToInt());
    }

    [Fact]
    public void Sqrt_of_perfect_squares_is_exact()
    {
        Assert.Equal(Fix64.Zero, Fix64.Sqrt(Fix64.Zero));
        Assert.Equal(Fix64.One, Fix64.Sqrt(Fix64.One));
        Assert.Equal(Fix64.FromInt(2), Fix64.Sqrt(Fix64.FromInt(4)));
        Assert.Equal(Fix64.FromInt(12), Fix64.Sqrt(Fix64.FromInt(144)));
        Assert.Equal(Fix64.Half, Fix64.Sqrt(Fix64.FromRatio(1, 4)));
    }

    [Fact]
    public void Sqrt_of_two_is_accurate_to_a_few_ulps()
    {
        // sqrt(2) = 1.41421356237309504880...; in Q32.32 raw = floor(sqrt(2) * 2^32) = 6074000999
        Assert.Equal(6074000999L, Fix64.Sqrt(Fix64.FromInt(2)).Raw);
    }

    [Fact]
    public void Sqrt_is_the_exact_floor_for_random_values()
    {
        var rng = Pcg32.Seed(2026, 10);
        for (int i = 0; i < 5000; i++)
        {
            long raw = (((long)rng.NextUInt() << 31) ^ rng.NextUInt()) & long.MaxValue;
            long r = Fix64.Sqrt(Fix64.FromRaw(raw)).Raw;
            UInt128 target = (UInt128)(ulong)raw << 32;
            Assert.True((UInt128)(ulong)r * (ulong)r <= target, $"sqrt too high for raw {raw}");
            Assert.True((UInt128)(ulong)(r + 1) * (ulong)(r + 1) > target, $"sqrt too low for raw {raw}");
        }
    }

    [Fact]
    public void Sqrt_of_negative_throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Fix64.Sqrt(Fix64.FromInt(-1)));

    [Fact]
    public void Sqrt_handles_the_largest_value()
    {
        long r = Fix64.Sqrt(Fix64.MaxValue).Raw;
        Assert.True(r > 0);
        Assert.True((UInt128)(ulong)r * (ulong)r <= (UInt128)(ulong)long.MaxValue << 32);
    }

    [Theory]
    [InlineData("1", 1L << 32)]
    [InlineData("1.5", 3L << 31)]
    [InlineData("-1.5", -(3L << 31))]
    [InlineData("+0.25", 1L << 30)]
    [InlineData(".5", 1L << 31)]
    [InlineData("0.1", 429496729L)]      // truncated, not rounded
    [InlineData("-0.1", -429496729L)]
    [InlineData("  2  ", 2L << 32)]
    public void Parse_is_exact_and_truncating(string text, long raw)
        => Assert.Equal(raw, Fix64.Parse(text).Raw);

    [Fact]
    public void Parse_matches_FromRatio()
    {
        Assert.Equal(Fix64.FromRatio(-12375, 1000), Fix64.Parse("-12.375"));
        Assert.Equal(Fix64.FromRatio(1265, 1000), Fix64.Parse("1.265"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData("1e5")]
    [InlineData("-")]
    [InlineData(".")]
    [InlineData("0.1234567890123456789")] // 19 fractional digits
    public void Parse_rejects_bad_input(string text)
        => Assert.ThrowsAny<Exception>(() => Fix64.Parse(text));

    [Fact]
    public void Parse_rejects_out_of_range()
        => Assert.Throws<OverflowException>(() => Fix64.Parse("3000000000"));

    [Fact]
    public void ToString_is_exact_decimal_text()
    {
        Assert.Equal("1.000000000", Fix64.One.ToString());
        Assert.Equal("0.500000000", Fix64.Half.ToString());
        Assert.Equal("-0.333333333", Fix64.FromRatio(-1, 3).ToString());
        Assert.Equal("12.375000000", Fix64.Parse("12.375").ToString());
        Assert.Equal("0.000000000", Fix64.Zero.ToString());
        Assert.Equal("-2147483648.000000000", Fix64.MinValue.ToString());
    }

    [Fact]
    public void Display_conversion_is_close()
        => Assert.Equal(1.5, Fix64.FromRatio(3, 2).ToDoubleForDisplay());
}
