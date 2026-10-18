using System;
using System.Collections.Generic;
using System.Linq;

namespace CivSim.Content;

/// <summary>Counts and graph measures over a loaded catalog: what <c>civsim stats</c> prints and the golden test checks.</summary>
public sealed class Analysis
{
    public required int Projects { get; init; }          // excludes keystones
    public required int Keystones { get; init; }
    public required int Lineages { get; init; }
    public required IReadOnlyDictionary<string, int> ByTheme { get; init; }
    public required IReadOnlyList<int> ByEra { get; init; }            // regular projects, eras 0..9
    public required IReadOnlyDictionary<string, int> ByLineage { get; init; }
    public required IReadOnlyDictionary<string, int> ByType { get; init; }
    public required IReadOnlyDictionary<string, int> ByStatus { get; init; }
    public required IReadOnlyList<string> Starters { get; init; }
    public required IReadOnlyList<KeystoneMeasure> KeystoneMeasures { get; init; }
    public required IReadOnlyList<(string Id, int Dependents)> Hubs { get; init; }
    public required IReadOnlyDictionary<string, double> CrossThemeByLineage { get; init; }

    public static Analysis Compute(ContentSet set)
    {
        var graph = new Graph(set);
        var regular = set.Regular.ToList();
        var byTheme = regular.GroupBy(p => p.Theme).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var byEra = Enumerable.Range(0, 10).Select(e => regular.Count(p => p.Era == e)).ToList();
        var byLineage = regular.GroupBy(p => p.Lineage).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var byType = set.Projects.GroupBy(p => p.Type).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var byStatus = set.Projects.GroupBy(p => p.Status).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var starters = regular.Where(p => p.Requires.Count == 0 && p.RequiresAny.Count == 0).Select(p => p.Id).OrderBy(i => i, StringComparer.Ordinal).ToList();

        var measures = new List<KeystoneMeasure>();
        bool acyclic = graph.FindCycle() is null;
        foreach (Project k in set.Keystones.OrderBy(k => k.Enters))
        {
            int closure = acyclic ? graph.Closure(k.Id).Count : -1;
            int critical = acyclic ? graph.InEraCritical(k) : -1;
            measures.Add(new KeystoneMeasure(k.Id, k.Era, k.BreadthMin, closure, critical));
        }

        var dependents = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Project p in set.Projects)
            foreach (string r in p.AllRequirements())
                dependents[r] = dependents.GetValueOrDefault(r) + 1;
        var hubs = dependents.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(15).Select(kv => (kv.Key, kv.Value)).ToList();

        var cross = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (Lineage lin in set.Lineages)
        {
            var late = regular.Where(p => p.Lineage == lin.Code && p.Era >= 1).ToList();
            if (late.Count == 0) continue;
            int n = late.Count(p => p.AllRequirements().Any(r => set.ById.TryGetValue(r, out Project? q) && q.Theme != p.Theme));
            cross[lin.Code] = (double)n / late.Count;
        }

        return new Analysis
        {
            Projects = regular.Count,
            Keystones = set.Keystones.Count(),
            Lineages = set.Lineages.Count,
            ByTheme = byTheme,
            ByEra = byEra,
            ByLineage = byLineage,
            ByType = byType,
            ByStatus = byStatus,
            Starters = starters,
            KeystoneMeasures = measures,
            Hubs = hubs,
            CrossThemeByLineage = cross,
        };
    }
}

public sealed record KeystoneMeasure(string Id, int EraLeft, int Breadth, int Closure, int InEraCritical)
{
    public int FreePicks => Breadth - InEraCritical;
}
