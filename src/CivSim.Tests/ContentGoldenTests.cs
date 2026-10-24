using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivSim.Content;
using CivSim.Sim.Content;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>The golden numbers of docs/MVP/16 §7, reproduced by the C# tooling from the YAML catalog.</summary>
public class ContentGoldenTests
{
    private static readonly Lazy<(ContentSet Set, Report Report)> Real = new(() =>
    {
        (ContentSet set, Report report) = ContentFixture.Load();
        Validator.Run(set, report);
        return (set, report);
    });

    private static ContentSet Set => Real.Value.Set;

    [Fact]
    public void Real_content_loads_and_validates_with_no_errors_and_no_warnings()
    {
        Report report = Real.Value.Report;
        Assert.True(report.ErrorCount == 0 && report.WarningCount == 0, ContentFixture.Describe(report));
    }

    [Fact]
    public void Counts_match_the_golden_numbers()
    {
        Analysis a = Analysis.Compute(Set);
        Assert.Equal(509, a.Projects);
        Assert.Equal(10, a.Keystones);
        Assert.Equal(33, a.Lineages);
        Assert.Equal(170, a.ByTheme["craft"]);
        Assert.Equal(161, a.ByTheme["kin"]);
        Assert.Equal(178, a.ByTheme["reach"]);
        Assert.Equal(new[] { 34, 59, 52, 65, 56, 49, 41, 43, 60, 50 }, a.ByEra);
        Assert.Equal(new[] { "COM-01", "FIR-01", "FOR-01", "HNT-01", "SOC-01", "STN-01", "WAY-01" }, a.Starters);
    }

    [Fact]
    public void The_first_four_eras_are_numeric_and_the_rest_are_draft()
    {
        foreach (Project p in Set.Projects)
        {
            bool early = p.Era <= 3; // E0-E2 and, since the Bronze specs (docs/MVP/23), E3 with KEY-4
            Assert.Equal(early ? "numeric" : "draft", p.Status);
        }
        Assert.Equal(214, Set.Projects.Count(p => p.Status == "numeric")); // 148 (E0-E2 and KEY-1..3) + 65 E3 projects + KEY-4
    }

    [Fact]
    public void Keystone_closures_match_the_reference_scripts()
    {
        Analysis a = Analysis.Compute(Set);
        Assert.Equal(new[] { 11, 28, 70, 108, 153, 174, 210, 227, 278, 338 }, a.KeystoneMeasures.Select(m => m.Closure));
        Assert.Equal(new[] { 11, 11, 17, 18, 17, 12, 10, 10, 14, 13 }, a.KeystoneMeasures.Select(m => m.InEraCritical));
        Assert.Equal(new[] { 13, 14, 20, 21, 21, 16, 14, 14, 18, 17 }, a.KeystoneMeasures.Select(m => m.Breadth));
    }

    [Fact]
    public void Every_lineage_meets_the_cross_theme_rule()
    {
        Analysis a = Analysis.Compute(Set);
        Assert.Equal(33, a.CrossThemeByLineage.Count);
        Assert.All(a.CrossThemeByLineage, kv => Assert.True(kv.Value >= Validator.CrossThemeMinimum, $"{kv.Key} {kv.Value:P0}"));
    }

    [Fact]
    public void Critical_gates_are_exactly_the_seven_the_design_expects()
    {
        var critical = new Graph(Set).GateImpacts().Where(g => g.IsKeystoneCritical)
            .ToDictionary(g => g.Target, g => g.BlockedKeystones[0]);
        Assert.Equal(new Dictionary<string, string>
        {
            ["clay"] = "KEY-3", ["native copper"] = "KEY-3", ["wolves"] = "KEY-4", ["wild goats or sheep"] = "KEY-4",
            ["gold or silver"] = "KEY-5", ["coal"] = "KEY-7", ["oil seep"] = "KEY-10",
        }, critical);
    }

    [Fact]
    public void The_graph_is_acyclic_and_everything_is_reachable()
    {
        var graph = new Graph(Set);
        Assert.Null(graph.FindCycle());
        Assert.Equal(519, graph.Reachable().Count);
    }

    [Fact]
    public void Costs_follow_era_base_times_type_multiplier()
    {
        Assert.Equal(Fix64.FromInt(22), Set.CostOf(Set.ById["STN-01"]));                 // E0, M
        Assert.Equal(Fix64.FromInt(22) * Set.TypeMult("S")!.Value, Set.CostOf(Set.ById["BLD-01"])); // E0, S (1.2)
        Assert.Equal(Fix64.FromInt(150) * Set.TypeMult("I")!.Value, Set.CostOf(Set.ById["SOC-06"])); // E2, I (1.5)
        Assert.Equal(Fix64.FromInt(55), Set.CostOf(Set.ById["KEY-1"]));                  // E0 keystone, 2.5 x 22
        Assert.Equal(Fix64.FromInt(375), Set.CostOf(Set.ById["KEY-3"]));                  // 2.5 x 150 = 375
    }

    [Fact]
    public void Method_ladders_are_collected_from_project_effects()
    {
        var ladders = Validator.Ladders(Set);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 6 }, ladders["chop"].Select(r => r.Rung)); // rung 6 by MET-04 or MET-05
        Assert.Equal(new[] { "STN-01", "STN-02", "STN-04", "STN-08", "MET-03", "MET-04", "MET-05" }, ladders["chop"].Select(r => r.Project.Id));
        Assert.Equal(new[] { 1, 2 }, ladders["mine"].Select(r => r.Rung));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, ladders["hunt"].Select(r => r.Rung));
        Assert.Equal(new[] { 1, 2, 3, 4 }, ladders["forage"].Select(r => r.Rung));
    }

    [Fact]
    public void The_pack_builds_with_a_stable_hash_and_round_trips()
    {
        var report = new Report();
        ContentPack? a = PackBuilder.Build(Set, report);
        ContentPack? b = PackBuilder.Build(ContentFixture.Load().Set, new Report());
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(a!.Bytes, b!.Bytes);
        Assert.Equal(a.HashHex, b.HashHex);

        ContentPack loaded = ContentPack.Load(a.Bytes);
        Assert.Equal(a.HashHex, loaded.HashHex);
        Assert.Equal(519, loaded.List("projects").Count);
        Assert.Equal(a.Bytes, CanonicalJson.Write(loaded.Root)); // read then write reproduces the bytes

        ContentValue stn01 = loaded.List("projects").First(p => p.GetString("id") == "STN-01");
        Assert.Equal(Fix64.FromInt(22), stn01.GetFix("cost"));
        Assert.Equal("craft", stn01.GetString("theme"));
    }

    [Fact]
    public void The_pack_matches_the_committed_golden_summary()
    {
        // The CI job runs the same comparison on macOS and Windows: identical content must give identical bytes.
        ContentPack pack = PackBuilder.Build(Set, new Report())!;
        string summary = string.Join("\n", $"format={ContentPack.FormatName} version={ContentPack.FormatVersion}",
            $"projects={pack.List("projects").Count}", $"bytes={pack.Bytes.Length}", $"hash={pack.HashHex}");
        string expected = File.ReadAllText(RepoPaths.Golden("content-pack.txt")).Replace("\r\n", "\n").Trim();
        Assert.Equal(expected, summary);
    }
}
