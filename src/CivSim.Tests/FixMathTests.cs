using System;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

public class FixMathTests
{
    private static void Near(double expected, Fix64 actual, double relative = 2e-7)
    {
        double a = actual.ToDoubleForDisplay();
        Assert.True(Math.Abs(a - expected) <= Math.Max(1e-8, Math.Abs(expected) * relative), $"expected {expected}, got {a}");
    }

    // Expected values from an independent Python float computation (log2, 2**x, x**0.85).
    [Theory]
    [InlineData(1.5, 0.5849625007211562)]
    [InlineData(2.0, 1.0)]
    [InlineData(8.26, 3.0461417816447205)]
    [InlineData(100.0, 6.643856189774724)]
    [InlineData(1234.5, 10.269711121142782)]
    [InlineData(0.5, -1.0)]
    [InlineData(100000.0, 16.609640474436812)]
    public void Log2_matches_the_reference(double x, double expected)
        => Near(expected, FixMath.Log2(Fix64.Parse(x.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));

    [Theory]
    [InlineData("-3.2", 0.1088188204120155)]
    [InlineData("-0.5", 0.7071067811865476)]
    [InlineData("0", 1.0)]
    [InlineData("0.3", 1.2311444133449163)]
    [InlineData("1", 2.0)]
    [InlineData("2.75", 6.727171322029716)]
    [InlineData("10.1", 1097.496025637164)]
    public void Exp2_matches_the_reference(string x, double expected) => Near(expected, FixMath.Exp2(Fix64.Parse(x)));

    [Theory]
    [InlineData("12", 8.26616479452201)]
    [InlineData("17.8", 11.557316373854109)]
    [InlineData("100", 50.11872336272722)]
    [InlineData("1234.5", 424.39223598182105)]
    [InlineData("0.5", 0.5547847360339225)]
    public void Pow_085_matches_the_reference(string x, double expected) => Near(expected, FixMath.Pow(Fix64.Parse(x), Fix64.Parse("0.85")));

    [Fact]
    public void Pow_edge_cases_and_exact_integer_powers()
    {
        Assert.Equal(Fix64.Zero, FixMath.Pow(Fix64.Zero, Fix64.Parse("0.85")));
        Assert.Equal(Fix64.One, FixMath.Pow(Fix64.FromInt(9), Fix64.Zero));
        Assert.Equal(Fix64.FromInt(8), FixMath.PowInt(Fix64.FromInt(2), 3));
        Assert.Equal(Fix64.One, FixMath.PowInt(Fix64.FromInt(5), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FixMath.Log2(Fix64.Zero));
    }

    [Fact]
    public void Exp2_and_Log2_are_inverse_and_monotone()
    {
        Fix64 previous = Fix64.MinValue;
        for (int i = 1; i < 200; i++)
        {
            Fix64 x = Fix64.FromRatio(i, 7);
            Fix64 log = FixMath.Log2(x);
            Assert.True(log > previous);
            previous = log;
            Near(x.ToDoubleForDisplay(), FixMath.Exp2(log), 1e-6);
        }
    }
}
