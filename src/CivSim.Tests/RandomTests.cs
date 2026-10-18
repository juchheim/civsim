using System;
using CivSim.Sim.Randomness;

namespace CivSim.Tests;

public class RandomTests
{
    [Fact]
    public void Pcg32_matches_the_published_reference_stream()
    {
        // pcg32_srandom(42, 54): first outputs of the reference demo program.
        var rng = Pcg32.Seed(42, 54);
        uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };
        foreach (uint e in expected) Assert.Equal(e, rng.NextUInt());
    }

    [Fact]
    public void Hash_mix_matches_splitmix64_reference()
    {
        Assert.Equal(0xe220a8397b1dcdafUL, Hash64.Mix(0));
        Assert.Equal(0x910a2dec89025cc1UL, Hash64.Mix(1));
    }

    [Fact]
    public void String_hash_is_fnv1a_and_stable()
    {
        Assert.Equal(0xcbf29ce484222325UL, Hash64.OfString(""));
        Assert.Equal(0xaf63dc4c8601ec8cUL, Hash64.OfString("a"));
        Assert.Equal(0x76b4a5b2e1675cc4UL, Hash64.OfString("civsim"));
    }

    [Fact]
    public void Combine_is_order_sensitive_with_fixed_values()
    {
        Assert.Equal(0xe06dd043328bd285UL, Hash64.Combine(1, 2));
        Assert.Equal(0xbcd9dbb49673066bUL, Hash64.Combine(2, 1));
    }

    [Fact]
    public void Streams_are_reproducible_and_independent()
    {
        ulong system = Hash64.OfString("walk");
        uint First(ulong seed, ulong sys, ulong entity, long tick) => Pcg32.ForStream(seed, sys, entity, tick).NextUInt();

        Assert.Equal(First(1, system, 5, 10), First(1, system, 5, 10));
        Assert.NotEqual(First(1, system, 5, 10), First(2, system, 5, 10));
        Assert.NotEqual(First(1, system, 5, 10), First(1, Hash64.OfString("other"), 5, 10));
        Assert.NotEqual(First(1, system, 5, 10), First(1, system, 6, 10));
        Assert.NotEqual(First(1, system, 5, 10), First(1, system, 5, 11));
    }

    [Fact]
    public void Stream_does_not_depend_on_how_many_other_streams_were_drawn()
    {
        ulong system = Hash64.OfString("walk");
        uint alone = Pcg32.ForStream(9, system, 3, 100).NextUInt();
        for (ulong e = 0; e < 50; e++) Pcg32.ForStream(9, system, e, 100).NextUInt();
        Assert.Equal(alone, Pcg32.ForStream(9, system, 3, 100).NextUInt());
    }

    [Fact]
    public void NextBelow_stays_in_range_and_is_roughly_uniform()
    {
        var rng = Pcg32.Seed(123, 456);
        var counts = new int[10];
        for (int i = 0; i < 100_000; i++)
        {
            uint v = rng.NextBelow(10);
            Assert.True(v < 10);
            counts[v]++;
        }
        foreach (int c in counts) Assert.InRange(c, 9_000, 11_000); // expected 10,000, sigma about 95
    }

    [Fact]
    public void NextBelow_handles_bounds_that_do_not_divide_the_range()
    {
        var rng = Pcg32.Seed(5, 5);
        for (int i = 0; i < 1000; i++) Assert.True(rng.NextBelow(uint.MaxValue - 1) < uint.MaxValue - 1);
        Assert.Equal(0u, rng.NextBelow(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextBelow(0));
    }

    [Fact]
    public void NextInt_covers_the_requested_range()
    {
        var rng = Pcg32.Seed(77, 1);
        int min = int.MaxValue, max = int.MinValue;
        for (int i = 0; i < 5000; i++)
        {
            int v = rng.NextInt(-3, 4);
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        Assert.Equal(-3, min);
        Assert.Equal(3, max);
    }

    [Fact]
    public void NextFix01_is_in_the_unit_interval()
    {
        var rng = Pcg32.Seed(8, 8);
        for (int i = 0; i < 5000; i++)
        {
            var v = rng.NextFix01();
            Assert.True(v.Raw >= 0 && v.Raw < CivSim.Sim.Numerics.Fix64.OneRaw);
        }
    }

    [Fact]
    public void NextChance_matches_the_requested_probability()
    {
        var rng = Pcg32.Seed(31, 41);
        int hits = 0;
        for (int i = 0; i < 100_000; i++) if (rng.NextChance(3, 10)) hits++;
        Assert.InRange(hits, 29_000, 31_000);
    }
}
