using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CivSim.Harness;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>M13 WP-E6-3: the E6 geography audit is repeatable, internally consistent, and leaves world generation exactly as it found it.</summary>
public sealed class E6GeographyAuditTests
{
    private static int V(IReadOnlyList<(string Name, int Value)> row, string name) => row.First(c => c.Name == name).Value;

    [Fact]
    public void Audit_report_is_identical_across_runs_and_thread_counts()
    {
        string one = E6GeographyAudit.AuditRange(1, 4, threads: 1);
        string many = E6GeographyAudit.AuditRange(1, 4, threads: 4);
        Assert.Equal(one, many);
        Assert.Contains("differ in 0 of 8 pairs", one);
    }

    [Fact]
    public void A_fixed_seed_range_gives_a_fixed_digest()
    {
        string report = E6GeographyAudit.AuditRange(1, 4, threads: 2);
        string digest = report.Split('\n').First(l => l.StartsWith("digest ", StringComparison.Ordinal));
        // Pinned from the first run. If world generation changes on purpose, the audit digest moves with it: refresh this value and the report.
        Assert.Equal("digest " + ExpectedDigest(1, 4), digest);
    }

    private static string ExpectedDigest(ulong first, int seeds)
    {
        Assert.Equal((ulong)1, first);
        Assert.Equal(4, seeds);
        return "6aca8fa180e35a7645f04fe661e757073ff9140019a42272e6db11b1c6aa9856";
    }

    [Fact]
    public void Every_printed_share_is_between_zero_and_one_hundred_percent()
    {
        string report = E6GeographyAudit.AuditRange(1, 6, threads: 2);
        int found = 0;
        foreach (Match m in Regex.Matches(report, @"(\d+)\.(\d)%"))
        {
            found++;
            double pct = double.Parse(m.Groups[1].Value + "." + m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(pct, 0.0, 100.0);
        }
        Assert.True(found > 40, "the report should print many shares");
    }

    [Fact]
    public void Counters_are_consistent_for_every_start()
    {
        var rows = E6GeographyAudit.Counters(1, 8, threads: 2);
        Assert.Equal(32, rows.Count);
        foreach (var row in rows)
        {
            foreach (string p in new[] { "seam", "coal", "sugar" })
            {
                int onMap = V(row, p + "OnMap");
                Assert.True(V(row, p + "N5") <= V(row, p + "N8"));
                Assert.True(V(row, p + "N8") <= V(row, p + "N25"));
                Assert.True(V(row, p + "N25") <= V(row, p + "N35"));
                Assert.True(V(row, p + "N35") <= V(row, p + "N52"));
                Assert.True(V(row, p + "N52") <= onMap);
                Assert.Equal(V(row, p + "N35"), V(row, p + "Same35") + V(row, p + "Other35"));
                Assert.Equal(V(row, p + "N52"), V(row, p + "Same52") + V(row, p + "Other52"));
                Assert.True(V(row, p + "Unc5") <= V(row, p + "Unc16") && V(row, p + "Unc16") <= V(row, p + "Unc35"));
                Assert.True(V(row, p + "Unc35") <= V(row, p + "Same35"));
                Assert.True(V(row, p + "Nearest") <= V(row, p + "NearestSame"));
                Assert.InRange(V(row, p + "Step"), 1, 7);
                Assert.Equal(onMap == 0, V(row, p + "Step") == 7);
                // a tile counted within a radius implies the nearest one is within that radius
                Assert.Equal(V(row, p + "N35") > 0, V(row, p + "Nearest") <= 35);
            }
            Assert.True(V(row, "coalDeposits") > 0, "every generated world has coal deposits");
            Assert.True(V(row, "reachIslands") <= V(row, "reachDirect"), "islands as stepping stones never need more reach than a direct hop");
            Assert.True(V(row, "gap") >= 2 * V(row, "reachDirect"), "a one-leg crossing of a gap needs about half the gap");
            Assert.True(V(row, "legs22") <= V(row, "legs16") || V(row, "legs16") == -1);
            Assert.True(V(row, "legs16") <= V(row, "legs11") || V(row, "legs11") == -1);
            Assert.True(V(row, "legs11") >= 1 || V(row, "legs11") == -1, "the continents are never joined by land");
        }
        // World-level truths agree across the four starts of a world.
        for (int w = 0; w < 8; w++)
            foreach (string name in new[] { "coalDeposits", "seamOnMap", "sugarOnMap", "gap", "reachDirect", "reachIslands", "legs11", "worldAnySeam35" })
            {
                int first = V(rows[w * 4], name);
                for (int c = 1; c < 4; c++) Assert.Equal(first, V(rows[w * 4 + c], name));
            }
    }

    [Fact]
    public void Audit_does_not_change_the_worldgen_fingerprint_golden()
    {
        string expected = File.ReadAllText(RepoPaths.Golden("worldgen-v1.txt")).Replace("\r\n", "\n").Trim();
        string Actual() => $"seed={WorldGenerator.Generate(42).Seed} fingerprint={WorldGenerator.Generate(42).Fingerprint():x16}";
        Assert.Equal(expected, Actual());
        _ = E6GeographyAudit.AuditRange(40, 2, threads: 2);
        Assert.Equal(expected, Actual());
    }
}
