using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivSim.Content;
using CivSim.Sim.Content;

namespace CivSim.Cli;

/// <summary>
/// <c>civsim</c>: content tooling (docs/MVP/16 §6.2).
/// <code>
/// civsim validate   [--content DIR] [--strict] [--quiet]   run the checks; exit 1 on errors (or warnings with --strict)
/// civsim build-pack [--content DIR] [--out FILE] [--check-golden F | --write-golden F]
///                                                          validate, compile, write the pack, print its hash
/// civsim stats      [--content DIR]                        counts by theme, era, lineage, type and status; hubs
/// civsim paths      [--content DIR]                        critical path and free picks per keystone
/// civsim gates      [--content DIR]                        which [Seen] gates block keystones, and their guarantees
/// civsim pack-info  FILE                                   load a pack and print its hash and size
/// </code>
/// Not yet implemented from the spec: diagram, docs (generated markdown) and replay; see docs/MVP/17 §3.1.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0) return Usage();
            string cmd = args[0];
            var rest = args.Skip(1).ToList();
            return cmd switch
            {
                "validate" => Validate(rest),
                "build-pack" => BuildPack(rest),
                "stats" => Stats(rest),
                "paths" => Paths(rest),
                "gates" => Gates(rest),
                "pack-info" => PackInfo(rest),
                _ => Usage(),
            };
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException)
        {
            Console.Error.WriteLine("error: " + e.Message);
            return 2;
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("usage: civsim <validate|build-pack|stats|paths|gates> [--content DIR] [options]\n       civsim pack-info FILE");
        return 2;
    }

    private static string? Option(List<string> args, string name)
    {
        int i = args.IndexOf(name);
        return i >= 0 && i + 1 < args.Count ? args[i + 1] : null;
    }

    private static string ContentDir(List<string> args)
    {
        if (Option(args, "--content") is { } given) return Path.GetFullPath(given);
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "CivSim.slnx"))) return Path.Combine(dir.FullName, "content");
                dir = dir.Parent;
            }
        }
        throw new ArgumentException("Could not find the repository root; pass --content DIR.");
    }

    private static (ContentSet Set, Report Report) Load(List<string> args)
    {
        var report = new Report();
        ContentSet set = ContentSet.Load(new DirectorySource(ContentDir(args)), report);
        return (set, report);
    }

    private static void PrintIssues(Report report, bool quiet)
    {
        foreach (Issue i in report.Issues.Where(i => i.Severity == Severity.Error)) Console.WriteLine(i);
        if (!quiet) foreach (Issue i in report.Issues.Where(i => i.Severity == Severity.Warning)) Console.WriteLine(i);
    }

    private static int Validate(List<string> args)
    {
        (ContentSet set, Report report) = Load(args);
        Validator.Run(set, report);
        PrintIssues(report, args.Contains("--quiet"));
        Analysis a = Analysis.Compute(set);
        Console.WriteLine($"{a.Projects} projects + {a.Keystones} keystones in {a.Lineages} lineages: " +
                          $"{string.Join(", ", a.ByStatus.Select(kv => $"{kv.Value} {kv.Key}"))}");
        Console.WriteLine($"{report.ErrorCount} error(s), {report.WarningCount} warning(s)");
        bool strict = args.Contains("--strict");
        return report.ErrorCount > 0 || (strict && report.WarningCount > 0) ? 1 : 0;
    }

    private static int BuildPack(List<string> args)
    {
        (ContentSet set, Report report) = Load(args);
        ContentPack? pack = PackBuilder.Build(set, report);
        if (pack is null)
        {
            PrintIssues(report, quiet: true);
            Console.Error.WriteLine($"not built: {report.ErrorCount} error(s)");
            return 1;
        }
        string outPath = Option(args, "--out") ?? Path.Combine(Directory.GetCurrentDirectory(), "civsim.pack");
        File.WriteAllBytes(outPath, pack.Bytes);
        Console.WriteLine($"wrote {outPath} ({pack.Bytes.Length:N0} bytes)");
        Console.WriteLine($"hash {pack.HashHex}");

        string summary = string.Join("\n", $"format={ContentPack.FormatName} version={ContentPack.FormatVersion}",
            $"projects={pack.List("projects").Count}", $"bytes={pack.Bytes.Length}", $"hash={pack.HashHex}");
        if (Option(args, "--write-golden") is { } write)
        {
            File.WriteAllText(write, summary + "\n");
            Console.WriteLine($"wrote golden {write}");
        }
        if (Option(args, "--check-golden") is { } check)
        {
            string expected = File.ReadAllText(check).Replace("\r\n", "\n").Trim();
            if (expected != summary)
            {
                Console.Error.WriteLine("CONTENT PACK CHECK FAILED\nexpected:\n" + expected + "\nactual:\n" + summary);
                return 1;
            }
            Console.WriteLine("content pack check passed");
        }
        return 0;
    }

    private static int PackInfo(List<string> args)
    {
        if (args.Count == 0) return Usage();
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(args[0]));
        Console.WriteLine($"{pack.Bytes.Length:N0} bytes, {pack.List("projects").Count} projects/keystones, hash {pack.HashHex}");
        return 0;
    }

    private static int Stats(List<string> args)
    {
        (ContentSet set, Report report) = Load(args);
        if (report.HasErrors) { PrintIssues(report, true); return 1; }
        Analysis a = Analysis.Compute(set);
        Console.WriteLine($"projects {a.Projects}, keystones {a.Keystones}, lineages {a.Lineages}");
        Console.WriteLine("by theme   : " + string.Join(" · ", a.ByTheme.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key} {kv.Value}")));
        Console.WriteLine("by era     : " + string.Join(" · ", a.ByEra.Select((n, e) => $"E{e} {n}")));
        Console.WriteLine("by type    : " + string.Join(" · ", a.ByType.Select(kv => $"{kv.Key} {kv.Value}")));
        Console.WriteLine("by status  : " + string.Join(" · ", a.ByStatus.Select(kv => $"{kv.Key} {kv.Value}")));
        Console.WriteLine("starters   : " + string.Join(", ", a.Starters));
        Console.WriteLine("by lineage : " + string.Join(" ", a.ByLineage.Select(kv => $"{kv.Key}={kv.Value}")));
        Console.WriteLine($"cross-theme: min {a.CrossThemeByLineage.Values.Min():P0} over {a.CrossThemeByLineage.Count} lineages");
        Console.WriteLine("hubs       : " + string.Join(", ", a.Hubs.Take(10).Select(h => $"{h.Id} ({h.Dependents})")));
        return 0;
    }

    private static int Paths(List<string> args)
    {
        (ContentSet set, Report report) = Load(args);
        if (report.HasErrors) { PrintIssues(report, true); return 1; }
        Analysis a = Analysis.Compute(set);
        Console.WriteLine("keystone  leaves  closure  in-era  breadth  free picks");
        foreach (KeystoneMeasure m in a.KeystoneMeasures)
            Console.WriteLine($"{m.Id,-8}  E{m.EraLeft,-5}  {m.Closure,7}  {m.InEraCritical,6}  {m.Breadth,7}  {m.FreePicks,10}");
        return 0;
    }

    private static int Gates(List<string> args)
    {
        (ContentSet set, Report report) = Load(args);
        if (report.HasErrors) { PrintIssues(report, true); return 1; }
        var guaranteed = set.Reg("guarantees").Records.Select(g => Graph.Normalize(g.GetString("gate") ?? "")).ToHashSet(StringComparer.Ordinal);
        Console.WriteLine("gate target                blocks     lost  guaranteed");
        foreach (GateImpact g in new Graph(set).GateImpacts())
            Console.WriteLine($"{g.Target,-26} {(g.IsKeystoneCritical ? g.BlockedKeystones[0] : "-"),-9} {g.ProjectsLost,5}  {(guaranteed.Contains(g.Target) ? "yes" : "no")}");
        return 0;
    }
}
