using System;
using System.Collections.Generic;
using System.Linq;

namespace CivSim.Content;

/// <summary>
/// The prerequisite graph over projects and keystones, and the analyses built on it (docs/MVP/04 §2).
/// Dangling requirement ids are ignored here; check V2 reports them.
/// </summary>
public sealed class Graph
{
    private readonly ContentSet _set;
    private readonly Dictionary<string, HashSet<string>> _closure = new(StringComparer.Ordinal);

    public Graph(ContentSet set) => _set = set;

    private Project? Find(string id) => _set.ById.TryGetValue(id, out Project? p) ? p : null;

    /// <summary>Returns a cycle as a list of ids (first id repeated at the end), or null when the graph is acyclic.</summary>
    public List<string>? FindCycle()
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal); // 1 = visiting, 2 = done
        var stack = new List<string>();
        foreach (Project p in _set.Projects)
        {
            List<string>? cycle = Visit(p.Id, state, stack);
            if (cycle is not null) return cycle;
        }
        return null;
    }

    private List<string>? Visit(string id, Dictionary<string, int> state, List<string> stack)
    {
        if (state.TryGetValue(id, out int s))
        {
            if (s == 2) return null;
            int at = stack.IndexOf(id);
            var cycle = stack.GetRange(at, stack.Count - at);
            cycle.Add(id);
            return cycle;
        }
        Project? p = Find(id);
        if (p is null) return null;
        state[id] = 1;
        stack.Add(id);
        foreach (string r in p.AllRequirements())
        {
            List<string>? c = Visit(r, state, stack);
            if (c is not null) return c;
        }
        stack.RemoveAt(stack.Count - 1);
        state[id] = 2;
        return null;
    }

    /// <summary>
    /// Every project that must be completed before <paramref name="id"/> (the project itself excluded). Of a
    /// one-of group the alternative with the smaller closure is chosen, ties going to the first listed.
    /// Requires an acyclic graph.
    /// </summary>
    public HashSet<string> Closure(string id)
    {
        if (_closure.TryGetValue(id, out HashSet<string>? cached)) return cached;
        var result = new HashSet<string>(StringComparer.Ordinal);
        Project? p = Find(id);
        if (p is not null)
        {
            var chosen = new List<string>(p.Requires);
            if (p.RequiresAny.Count > 0)
            {
                string? best = null;
                int bestSize = int.MaxValue;
                foreach (string alt in p.RequiresAny)
                {
                    if (Find(alt) is null) continue;
                    int size = Closure(alt).Count;
                    if (size < bestSize) { best = alt; bestSize = size; }
                }
                if (best is not null) chosen.Add(best);
            }
            foreach (string r in chosen)
            {
                if (Find(r) is null) continue;
                result.Add(r);
                result.UnionWith(Closure(r));
            }
        }
        _closure[id] = result;
        return result;
    }

    /// <summary>The number of projects of the keystone's own era on its closure: the forced part of its breadth.</summary>
    public int InEraCritical(Project keystone)
    {
        int n = 0;
        foreach (string id in Closure(keystone.Id))
            if (Find(id) is { } p && p.Era == keystone.Era) n++;
        return n;
    }

    /// <summary>
    /// All projects reachable from the starters (those with no requirements). Gates are ignored unless some
    /// are given in <paramref name="blockedGates"/> (normalized lower-case 'seen' targets): a project carrying
    /// a blocked gate can never be completed.
    /// </summary>
    public HashSet<string> Reachable(ISet<string>? blockedGates = null)
    {
        var done = new HashSet<string>(StringComparer.Ordinal);
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (Project p in _set.Projects)
            {
                if (done.Contains(p.Id)) continue;
                if (blockedGates is not null && p.Gates.Any(g => g.Kind == "seen" && blockedGates.Contains(Normalize(g.Target)))) continue;
                if (!p.Requires.All(done.Contains)) continue;
                if (p.RequiresAny.Count > 0 && !p.RequiresAny.Any(done.Contains)) continue;
                done.Add(p.Id);
                changed = true;
            }
        }
        return done;
    }

    public static string Normalize(string target) => target.Trim().ToLowerInvariant();

    /// <summary>
    /// For every 'seen' gate target: the keystones that become unreachable if that target is never
    /// satisfied, and how many projects are lost. Targets that block no keystone are included with an empty list.
    /// </summary>
    public List<GateImpact> GateImpacts()
    {
        var targets = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Project p in _set.Projects)
            foreach (Gate g in p.Gates)
                if (g.Kind == "seen") targets.Add(Normalize(g.Target));
        int total = _set.Projects.Count;
        var result = new List<GateImpact>();
        foreach (string t in targets)
        {
            HashSet<string> reach = Reachable(new HashSet<string>(StringComparer.Ordinal) { t });
            var lost = new List<string>();
            foreach (Project k in _set.Keystones.OrderBy(k => k.Era)) if (!reach.Contains(k.Id)) lost.Add(k.Id); // earliest era first
            result.Add(new GateImpact(t, lost, total - reach.Count));
        }
        return result;
    }
}

public sealed record GateImpact(string Target, IReadOnlyList<string> BlockedKeystones, int ProjectsLost)
{
    public bool IsKeystoneCritical => BlockedKeystones.Count > 0;
}
