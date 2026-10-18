using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CivSim.Tests;

/// <summary>
/// Enforces the determinism rules of docs/MVP/15 §6 on the simulation library's source (rules D1 to D6).
/// A line may opt out with a <c>determinism-allow: reason</c> comment; every use should be justified.
/// </summary>
public class SourceRulesTests
{
    private static readonly (string Rule, Regex Pattern)[] Banned =
    {
        ("D1 no floating point in simulation code", new Regex(@"\b(float|double|decimal|Single|Double|Decimal)\b")),
        ("D2 no float math library (only integer-safe Math members are allowed)", new Regex(@"\bMathF?\.(?!Min\b|Max\b|Abs\b|Clamp\b|BigMul\b|Sign\b|DivRem\b)")),
        ("D3 no System.Random", new Regex(@"\b(System\.Random|new Random\b|Random\.Shared)")),
        ("D4 no default or string hash codes", new Regex(@"GetHashCode\s*\(")),
        ("D4/D5 no hash-ordered collections", new Regex(@"\b(Dictionary|HashSet|SortedDictionary|ConcurrentDictionary|ConcurrentBag|ConditionalWeakTable|Hashtable)\s*<|\bHashtable\b")),
        ("D6 no wall-clock or timers", new Regex(@"\b(DateTime|DateTimeOffset|Stopwatch|TimeProvider|Environment\.TickCount|Environment\.TickCount64|Timer)\b")),
        ("D6 no threads or parallelism inside the simulation library", new Regex(@"\b(Thread|ThreadPool|Task\.Run|Parallel\.|PLINQ|AsParallel)\b")),
        ("D6 no file, console or environment access", new Regex(@"\b(File\.|Directory\.|Console\.|Environment\.GetEnvironmentVariable|Guid\.NewGuid)")),
    };

    private static readonly Regex Allow = new(@"determinism-allow");

    [Fact]
    public void Simulation_sources_follow_the_determinism_rules()
    {
        var violations = new List<string>();
        int filesScanned = 0;
        foreach (string path in Directory.EnumerateFiles(RepoPaths.SimSources, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            filesScanned++;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = StripComment(lines[i]);
                if (line.Length == 0) continue;
                bool allowed = Allow.IsMatch(lines[i]) || (i > 0 && Allow.IsMatch(lines[i - 1]));
                if (allowed) continue;
                foreach ((string rule, Regex pattern) in Banned)
                {
                    if (pattern.IsMatch(line))
                        violations.Add($"{Path.GetRelativePath(RepoPaths.Root, path)}:{i + 1}: {rule}: {lines[i].Trim()}");
                }
            }
        }
        Assert.True(filesScanned >= 8, $"expected to scan the sim sources, found {filesScanned} files");
        Assert.True(violations.Count == 0, "Determinism rule violations:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void The_simulation_library_does_not_reference_the_engine()
    {
        // Looks at real references only (not comments): no project, package or assembly reference may name an engine.
        var project = System.Xml.Linq.XDocument.Load(Path.Combine(RepoPaths.SimSources, "CivSim.Sim.csproj"));
        var references = new List<string>();
        foreach (System.Xml.Linq.XElement e in project.Descendants())
        {
            if (e.Name.LocalName is "ProjectReference" or "PackageReference" or "Reference" or "FrameworkReference")
                references.Add((string?)e.Attribute("Include") ?? "");
        }
        Assert.Empty(references); // the simulation library depends on the base class library only
    }

    [Fact]
    public void The_rule_checker_catches_violations()
    {
        // Guards the guard: make sure each pattern really matches what it is meant to catch.
        Assert.Matches(Banned[0].Pattern, "var x = 1.5f; double y;");
        Assert.Matches(Banned[1].Pattern, "var s = Math.Sqrt(2);");
        Assert.DoesNotMatch(Banned[1].Pattern, "var s = Math.Clamp(1, 2, 3);");
        Assert.Matches(Banned[2].Pattern, "var r = new Random(1);");
        Assert.Matches(Banned[3].Pattern, "return name.GetHashCode();");
        Assert.Matches(Banned[4].Pattern, "var d = new Dictionary<int, int>();");
        Assert.Matches(Banned[5].Pattern, "var t = DateTime.Now;");
        Assert.Matches(Banned[6].Pattern, "Task.Run(() => 1);");
        Assert.Matches(Banned[7].Pattern, "File.ReadAllText(p);");
    }

    private static string StripComment(string line)
    {
        string trimmed = line.TrimStart();
        if (trimmed.StartsWith("//") || trimmed.StartsWith("///") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) return "";
        int index = line.IndexOf("//", System.StringComparison.Ordinal);
        return index >= 0 ? line[..index] : line;
    }
}
