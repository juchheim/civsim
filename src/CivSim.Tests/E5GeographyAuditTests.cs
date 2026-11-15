using System;
using System.IO;
using CivSim.Harness;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>M12 WP-E5-3: the E5 geography audit is repeatable and leaves world generation exactly as it found it.</summary>
public sealed class E5GeographyAuditTests
{
    [Fact]
    public void Audit_report_is_identical_across_runs_and_thread_counts()
    {
        string one = E5GeographyAudit.AuditRange(1, 4, threads: 1);
        string many = E5GeographyAudit.AuditRange(1, 4, threads: 4);
        Assert.Equal(one, many);
        Assert.Contains("differ in 0 of 8 pairs", one);
    }

    [Fact]
    public void Audit_does_not_change_the_worldgen_fingerprint_golden()
    {
        string expected = File.ReadAllText(RepoPaths.Golden("worldgen-v1.txt")).Replace("\r\n", "\n").Trim();
        string Actual() => $"seed={WorldGenerator.Generate(42).Seed} fingerprint={WorldGenerator.Generate(42).Fingerprint():x16}";
        Assert.Equal(expected, Actual());
        _ = E5GeographyAudit.AuditRange(40, 2, threads: 2);
        Assert.Equal(expected, Actual());
    }
}
