using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Content;
using CivSim.Sim.Numerics;

namespace CivSim.Content;

/// <summary>
/// Compiles a validated <see cref="ContentSet"/> into a <see cref="ContentPack"/> (docs/MVP/16 §5): projects and
/// keystones get dense indices in sorted-id order and an exact compiled cost, method ladders are collected per
/// action, and registries are sorted by id. Authoring order, file order and comments never reach the pack.
/// </summary>
public static class PackBuilder
{
    /// <summary>Returns the pack, or null (with errors in <paramref name="report"/>) if the content does not validate.</summary>
    public static ContentPack? Build(ContentSet set, Report report)
    {
        Validator.Run(set, report);
        if (report.HasErrors) return null;
        return ContentPack.FromTree(Compile(set));
    }

    public static ContentValue Compile(ContentSet set)
    {
        var root = new List<KeyValuePair<string, ContentValue>>
        {
            new("format", ContentValue.Of(ContentPack.FormatName)),
            new("version", ContentValue.Of((long)ContentPack.FormatVersion)),
        };

        var lineages = set.Lineages.OrderBy(l => l.Code, StringComparer.Ordinal).Select(l => l.Tree);
        root.Add(new("lineages", ContentValue.ListOf(lineages)));

        var ordered = set.Projects.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
        var projects = new List<ContentValue>();
        for (int i = 0; i < ordered.Count; i++)
        {
            Project p = ordered[i];
            var entries = new List<KeyValuePair<string, ContentValue>>(p.Tree.Entries)
            {
                new("idx", ContentValue.Of((long)i)),
                new("lineage", ContentValue.Of(p.Lineage)),
                new("theme", ContentValue.Of(p.Theme)),
                new("cost", ContentValue.Of(set.CostOf(p) ?? Fix64.Zero)),
            };
            projects.Add(ContentValue.MapOf(entries));
        }
        root.Add(new("projects", ContentValue.ListOf(projects)));

        var ladders = new List<KeyValuePair<string, ContentValue>>();
        foreach (var (action, rungs) in Validator.Ladders(set).OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var list = rungs.Select(r =>
            {
                var e = new List<KeyValuePair<string, ContentValue>>
                {
                    new("rung", ContentValue.Of((long)r.Rung)),
                    new("project", ContentValue.Of(r.Project.Id)),
                    new("name", ContentValue.Of(r.Effect.Str("name") ?? "")),
                };
                if (r.Effect.Num("mult") is { } m) e.Add(new("mult", ContentValue.Of(m)));
                if (r.Effect.Num("rate") is { } rate) e.Add(new("rate", ContentValue.Of(rate)));
                if (r.Effect.Str("unit") is { } unit) e.Add(new("unit", ContentValue.Of(unit)));
                return ContentValue.MapOf(e);
            });
            ladders.Add(new(action, ContentValue.ListOf(list)));
        }
        root.Add(new("ladders", ContentValue.MapOf(ladders)));

        var registries = new List<KeyValuePair<string, ContentValue>>();
        foreach (string name in ContentSet.RegistryNames)
        {
            var records = set.Reg(name).Records.OrderBy(r => r.Get("id"), IdComparer.Instance);
            registries.Add(new(name, ContentValue.ListOf(records)));
        }
        root.Add(new("registries", ContentValue.MapOf(registries)));

        if (set.Balance is { } b)
        {
            var entries = b.Entries.Where(e => e.Key != "schema").ToList();
            root.Add(new("balance", ContentValue.MapOf(entries)));
        }
        return ContentValue.MapOf(root);
    }

    private sealed class IdComparer : IComparer<ContentValue?>
    {
        public static readonly IdComparer Instance = new();

        public int Compare(ContentValue? a, ContentValue? b)
        {
            if (a is null || b is null) return (a is null ? 0 : 1) - (b is null ? 0 : 1);
            if (a.Kind == ValueKind.Int && b.Kind == ValueKind.Int) return a.IntValue.CompareTo(b.IntValue);
            return string.CompareOrdinal(a.ToString(), b.ToString());
        }
    }
}
