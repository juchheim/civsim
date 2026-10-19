using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Content;
using CivSim.Sim.Numerics;

namespace CivSim.Content;

/// <summary>
/// The rule checks of docs/MVP/16 §6.1 (V2 to V18; V1 is the loader and schema). Each finding is an error or a
/// warning in the <see cref="Report"/>.
/// </summary>
public static class Validator
{
    public const double CrossThemeMinimum = 0.35;

    public static Report Run(ContentSet set)
    {
        var report = new Report();
        return Run(set, report);
    }

    public static Report Run(ContentSet set, Report report)
    {
        CheckEras(set, report);
        CheckReferences(set, report);

        var graph = new Graph(set);
        List<string>? cycle = graph.FindCycle();
        if (cycle is not null)
            report.Error("V3", "catalog", "prerequisite cycle: " + string.Join(" -> ", cycle));

        CheckEraOrder(set, report);
        CheckCrossTheme(set, report);
        CheckEffects(set, report);
        CheckLadders(set, report);
        CheckRegistryCompleteness(set, report);
        CheckCosts(set, report);
        CheckPractice(set, report);
        CheckText(set, report);
        CheckCapTargets(set, report);
        CheckHazardCoverage(set, report);

        if (cycle is null && !report.HasErrorsOf("V2"))
        {
            CheckReachability(set, graph, report);
            CheckGates(set, graph, report);
            CheckBreadth(set, graph, report);
            CheckOpenings(set, report);
        }
        return report;
    }

    private static string At(Project p) => $"{p.File}:{p.Line}";

    /// <summary>A warning on a project, unless its author accepted that code (with a reason).</summary>
    private static void Warn(Report report, Project p, string code, string message)
    {
        if (p.Ack.Contains(code)) return;
        report.Warning(code, At(p), message);
    }

    // ---- V15 cap targets -------------------------------------------------------------------------
    // These are numeric projects with catalog era E0-E2 in 09 §5.2, including effects
    // forced back into later research eras. Group caps are direct ladder values, not multipliers.
    private static readonly (string Project, string Cap, double Mult, double Take)[] CapTargets =
    {
        ("FOR-01", "food", 1.10, 1.0), ("HNT-01", "food", 1.12, 1.0), ("FOD-01", "food", 1.10, 1.0),
        ("HNT-02", "food", 1.04, 1.0), ("FOR-03", "food", 1.08, 1.0), ("HNT-03", "food", 1.04, 1.0),
        ("WAY-02", "food", 1.05, 1.0), ("HNT-07", "food", 1.10, 0.6), ("FOR-04", "food", 1.10, 1.0),
        ("HNT-04", "food", 1.10, 0.7), ("FOD-04", "food", 1.05, 1.0), ("FOR-05", "food", 1.04, 1.0),
        ("FOR-06", "food", 1.06, 1.0), ("AGR-01", "food", 1.30, 1.0), ("AGR-02", "food", 1.22, 1.0),
        ("FOD-05", "food", 1.05, 1.0), ("AGR-03", "food", 1.08, 0.5), ("HNT-09", "food", 1.08, 0.4),
        ("FOR-07", "food", 1.08, 0.4), ("HUS-02", "food", 1.04, 1.0),
        ("FOD-06", "food", 1.05, 1.0), ("HNT-06", "food", 1.03, 1.0),
        ("FOR-03", "lean", 1.25, 1.0), ("FOR-02", "lean", 1.10, 1.0), ("FOD-02", "lean", 1.15, 1.0),
        ("FOD-03", "lean", 1.30, 1.0),
        ("BLD-01", "housing", 1.25, 1.0), ("BLD-02", "housing", 1.20, 1.0),
        ("BLD-03", "housing", 1.50, 1.0), ("BLD-05", "housing", 1.50, 1.0),
        ("SOC-04", "housing", 1.30, 1.0),
        ("BLD-04", "housing", 1.50, 1.0), ("SOC-06", "housing", 1.50, 1.0),
        ("BLD-06", "housing", 1.50, 1.0),
    };

    private static void CheckCapTargets(ContentSet set, Report report)
    {
        const double tolerance = 0.10;
        var expected = CapTargets.ToDictionary(x => (x.Project, x.Cap));
        var actual = new Dictionary<(string Project, string Cap), (double Mult, double Take, Project Owner)>();
        foreach (Project p in set.Projects.Where(p => p.Status == "numeric" && p.Era <= 2))
        foreach (Effect e in p.Effects.Where(e => e.Kind == "cap" && e.Str("cap") is "food" or "lean" or "housing"))
        {
            if (e.Num("mult") is not { } mult || e.Str("cap") is not { } cap) continue;
            actual[(p.Id, cap)] = (mult.ToDoubleForDisplay(), e.Num("take")?.ToDoubleForDisplay() ?? 1.0, p);
        }

        foreach (var target in CapTargets)
        {
            double expectedMult = Math.Pow(target.Mult, target.Take);
            if (!actual.TryGetValue((target.Project, target.Cap), out var found))
            {
                string location = set.ById.TryGetValue(target.Project, out Project? p) ? At(p) : "catalog";
                report.Warning("V15", location,
                    $"{target.Project}: missing numeric {target.Cap} cap multiplier; target is ×{expectedMult:F3} (09 §5.2)");
                continue;
            }
            double actualMult = Math.Pow(found.Mult, found.Take);
            double delta = (actualMult - expectedMult) / expectedMult;
            if (Math.Abs(delta) > tolerance)
                Warn(report, found.Owner, "V15",
                    $"{target.Project}: {target.Cap} cap multiplier is ×{actualMult:F3}, target ×{expectedMult:F3} ({delta:+0%;-0%;0%}; tolerance ±10%, 09 §5.2)");
        }

        foreach (var item in actual)
            if (!expected.ContainsKey(item.Key))
                Warn(report, item.Value.Owner, "V15",
                    $"{item.Key.Project}: unlisted numeric {item.Key.Cap} cap multiplier ×{Math.Pow(item.Value.Mult, item.Value.Take):F3}; add its target to V15 if intentional (09 §5.2)");
    }

    // ---- V16 hazard coverage ---------------------------------------------------------------------
    // 07c §5 defines the E0 cause shares and the age-band target ratios. Project hazard values are
    // already population-average effects and are applied directly (11 §1). The E0 mix is
    // the reference; E1 and E2 add that era's numeric projects cumulatively. This is a catalog-level
    // warning proxy for the live C3 check, not a replacement for measuring deaths in the simulation.
    private static void CheckHazardCoverage(ContentSet set, Report report)
    {
        const double tolerance = 0.10;
        var childShares = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["endemic"] = .45, ["epidemic"] = 0, ["starvation"] = .20, ["exposure"] = .20,
            ["injury"] = .10, ["predators"] = .05, ["violence"] = 0,
        };
        var adultShares = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["endemic"] = .30, ["epidemic"] = 0, ["starvation"] = .15, ["exposure"] = .15,
            ["injury"] = .25, ["predators"] = .05, ["violence"] = .10,
        };
        var targets = new Dictionary<(int Era, string Band), double>
        {
            [(1, "child")] = .917, [(1, "adult")] = .917, [(1, "elder")] = 1.000,
            [(2, "child")] = .972, [(2, "adult")] = 1.000, [(2, "elder")] = 1.075,
        };
        var effects = new Dictionary<int, List<(string Cause, double Mult, string? Scope)>>();
        foreach (Project p in set.Projects.Where(p => p.Status == "numeric" && p.Era <= 2))
        foreach (Effect e in p.Effects.Where(e => e.Kind == "hazard" && e.Str("cause") is not null))
        {
            if (e.Num("mult") is not { } raw || e.Str("cause") is not { } cause) continue;
            int effectiveEra = p.IsKeystone ? p.Enters : p.Era;
            if (effectiveEra > 2) continue;
            if (!effects.TryGetValue(effectiveEra, out List<(string Cause, double Mult, string? Scope)>? eraEffects))
                effects[effectiveEra] = eraEffects = new List<(string Cause, double Mult, string? Scope)>();
            eraEffects.Add((cause, raw.ToDoubleForDisplay(), e.Str("scope")));
        }

        double BandHazard(string band, int throughEra)
        {
            double total = 0;
            IReadOnlyDictionary<string, double> shares = band == "child" ? childShares : adultShares;
            foreach (var cause in shares)
            {
                double factor = 1;
                for (int era = 0; era <= throughEra; era++)
                    if (effects.TryGetValue(era, out var causeEffects))
                        foreach (var effect in causeEffects)
                            if (effect.Cause == cause.Key)
                            {
                                double mult = effect.Mult;
                                if (effect.Scope == "hunters" && cause.Key == "injury")
                                {
                                    // 07c §5 assumes 60% of adult injury occurs while hunting;
                                    // hunter-scoped modifiers affect that share, not all injury.
                                    if (band == "child") continue;
                                    mult = 1 + .60 * (mult - 1);
                                }
                                factor *= mult;
                            }
                total += cause.Value * factor;
            }

            // Age-band hazards apply to all child mortality. Age-0 and maternal effects are weighted
            // using the assumptions in 07c §5: infants contribute 1/6 of child deaths; childbirth 6% of adults.
            if (band == "child")
            {
                double childFactor = 1, infantFactor = 1;
                for (int era = 0; era <= throughEra; era++)
                    if (effects.TryGetValue(era, out var ageBandEffects))
                        foreach (var effect in ageBandEffects)
                        {
                            if (effect.Cause == "child") childFactor *= effect.Mult;
                            if (effect.Cause == "age0") infantFactor *= effect.Mult;
                        }
                total *= childFactor * (5.0 / 6.0 + infantFactor / 6.0);
            }
            else if (band == "adult")
            {
                double maternalFactor = 1;
                for (int era = 0; era <= throughEra; era++)
                    if (effects.TryGetValue(era, out var maternalEffects))
                        foreach (var effect in maternalEffects)
                            if (effect.Cause == "maternal") maternalFactor *= effect.Mult;
                total *= .94 + .06 * maternalFactor;
            }
            else // Elders: 40% senescence is unaffected; remaining 60% follows adult causes.
                total = .40 + .60 * total;
            return total;
        }

        foreach (var target in targets)
        {
            double baseline = BandHazard(target.Key.Band, 0);
            double measured = BandHazard(target.Key.Band, target.Key.Era) / baseline;
            double delta = (measured - target.Value) / target.Value;
            if (Math.Abs(delta) > tolerance)
                report.Warning("V16", "catalog",
                    $"E{target.Key.Era} {target.Key.Band} mortality ratio vs E0 is {measured:F3}; target {target.Value:F3} ({delta:+0%;-0%;0%}, tolerance ±10%; 09 §4.1, 07c §5)");
        }
    }

    // ---- eras and keystone chain ------------------------------------------------------------------
    private static void CheckEras(ContentSet set, Report report)
    {
        Registry eras = set.Reg("eras");
        for (int i = 0; i < 10; i++)
            if (eras.Find(i.ToString()) is null) report.Error("V2", eras.File, $"era {i} is missing (eras must be 0..9)");
        foreach (Project k in set.Keystones)
        {
            if (k.Enters != k.Era + 1) report.Error("V5", At(k), $"{k.Id}: enters era {k.Enters} but leaves era {k.Era}");
            if (k.BreadthEra != k.Era) report.Error("V5", At(k), $"{k.Id}: breadth counts era {k.BreadthEra} but the keystone leaves era {k.Era}");
        }
        int[] expectedEras = Enumerable.Range(0, 10).ToArray();
        int[] actual = set.Keystones.Select(k => k.Era).OrderBy(e => e).ToArray();
        if (!actual.SequenceEqual(expectedEras))
            report.Error("V5", "catalog/keystones.yaml", $"expected one keystone leaving each era 0..9, found eras [{string.Join(",", actual)}]");
    }

    // ---- V2 references -----------------------------------------------------------------------------
    private static void CheckReferences(ContentSet set, Report report)
    {
        foreach (Project p in set.Projects)
        {
            foreach (string r in p.AllRequirements())
                if (!set.ById.ContainsKey(r)) report.Error("V2", At(p), $"{p.Id} requires unknown project {r}");
            if (p.Requires.Contains(p.Id) || p.RequiresAny.Contains(p.Id)) report.Error("V3", At(p), $"{p.Id} requires itself");
            if (p.RequiresAny.Count == 1) Warn(report, p, "V2", $"{p.Id}: a one-of group with a single alternative is just a requirement");
        }

        void Ref(string registry, string field, string location, string? id, bool allowStart = false)
        {
            if (id is null || (allowStart && id == "start")) return;
            if (!set.ById.ContainsKey(id)) report.Error("V2", location, $"{registry}: {field} refers to unknown project {id}");
        }
        foreach (ContentValue g in set.Reg("goods").Records)
            foreach (string id in g.GetStringList("first_from")) Ref("goods", g.GetString("id") + ".first_from", $"{set.Reg("goods").File}:{g.Line}", id, true);
        foreach (ContentValue s in set.Reg("structures").Records) Ref("structures", s.GetString("id") + ".unlocked_by", $"{set.Reg("structures").File}:{s.Line}", s.GetString("unlocked_by"));
        foreach (ContentValue t in set.Reg("tools").Records)
            foreach (string id in t.GetStringList("unlocked_by")) Ref("tools", t.GetString("id") + ".unlocked_by", $"{set.Reg("tools").File}:{t.Line}", id, true);
        foreach (ContentValue r in set.Reg("roles").Records) Ref("roles", r.GetString("id") + ".unlocked_by", $"{set.Reg("roles").File}:{r.Line}", r.GetString("unlocked_by"));
        foreach (ContentValue o in set.Reg("openings").Records)
            foreach (string id in o.GetStringList("steps")) Ref("openings", o.GetString("id") + ".steps", $"{set.Reg("openings").File}:{o.Line}", id);
    }

    // ---- V4 and V5 era order ----------------------------------------------------------------------
    private static void CheckEraOrder(ContentSet set, Report report)
    {
        foreach (Project p in set.Projects)
        {
            foreach (string r in p.AllRequirements())
            {
                if (!set.ById.TryGetValue(r, out Project? req)) continue;
                if (req.Era > p.Era) report.Error("V4", At(p), $"{p.Id} (era {p.Era}) requires {r} from later era {req.Era}");
                if (p.IsKeystone && req.Era != p.Era)
                    report.Error("V5", At(p), $"keystone {p.Id} leaves era {p.Era} but its prerequisite {r} is in era {req.Era}");
            }
        }
    }

    // ---- V6 cross-theme ---------------------------------------------------------------------------
    private static void CheckCrossTheme(ContentSet set, Report report)
    {
        foreach (Lineage lin in set.Lineages)
        {
            var late = set.Regular.Where(p => p.Lineage == lin.Code && p.Era >= 1).ToList();
            if (late.Count == 0) continue;
            int cross = late.Count(p => p.AllRequirements().Any(r => set.ById.TryGetValue(r, out Project? q) && q.Theme != p.Theme));
            double ratio = (double)cross / late.Count;
            if (ratio < CrossThemeMinimum)
                report.Error("V6", lin.File, $"lineage {lin.Code}: only {cross}/{late.Count} ({ratio:P0}) of E1+ projects have a cross-theme prerequisite (minimum {CrossThemeMinimum:P0})");
        }
    }

    // ---- V7 reachability, V8 gates, V9 breadth ----------------------------------------------------
    private static void CheckReachability(ContentSet set, Graph graph, Report report)
    {
        HashSet<string> reach = graph.Reachable();
        foreach (Project p in set.Projects)
            if (!reach.Contains(p.Id)) report.Error("V7", At(p), $"{p.Id} cannot be reached from the starter projects");
    }

    private static void CheckGates(ContentSet set, Graph graph, Report report)
    {
        var guaranteed = new HashSet<string>(StringComparer.Ordinal);
        foreach (ContentValue g in set.Reg("guarantees").Records)
            if (g.GetString("gate") is { } gate) guaranteed.Add(Graph.Normalize(gate));
        foreach (GateImpact impact in graph.GateImpacts())
            if (impact.IsKeystoneCritical && !guaranteed.Contains(impact.Target))
                report.Error("V8", "registries/guarantees.yaml",
                    $"the gate [Seen: {impact.Target}] blocks {impact.BlockedKeystones[0]} if never satisfied, but world generation has no guarantee for it");
    }

    private static void CheckBreadth(ContentSet set, Graph graph, Report report)
    {
        foreach (Project k in set.Keystones)
        {
            int critical = graph.InEraCritical(k);
            int free = k.BreadthMin - critical;
            if (free < 0)
                report.Error("V9", At(k), $"{k.Id}: breadth {k.BreadthMin} is below its own era's critical path ({critical})");
            else if (free < 3)
                Warn(report, k, "V9", $"{k.Id}: only {free} free picks beyond the critical path (breadth {k.BreadthMin}, critical {critical}); target is 3 or more");
        }
    }

    // ---- V10 effects -------------------------------------------------------------------------------
    private static void CheckEffects(ContentSet set, Report report)
    {
        foreach (Project p in set.Projects)
        {
            if (p.Ack.Count > 0 && string.IsNullOrWhiteSpace(p.AckReason)) report.Error("V13", At(p), $"{p.Id} accepts warnings [{string.Join(", ", p.Ack)}] but gives no ack_reason");
            if (p.Effects.Count == 0) report.Error("V10", At(p), $"{p.Id} has no effects");
            foreach (Effect e in p.Effects) EffectSpecs.Validate(e.Map, p.Status, p.Id, p.File, report);
        }
    }

    // ---- V11 method ladders -----------------------------------------------------------------------
    public static Dictionary<string, List<(int Rung, Project Project, Effect Effect)>> Ladders(ContentSet set)
    {
        var map = new Dictionary<string, List<(int, Project, Effect)>>(StringComparer.Ordinal);
        foreach (Project p in set.Projects)
            foreach (Effect e in p.Effects)
            {
                if (e.Kind != "method" || e.Str("action") is not { } action || e.Int("rung") is not { } rung) continue;
                if (!map.TryGetValue(action, out var list)) map[action] = list = new();
                list.Add(((int)rung, p, e));
            }
        foreach (var list in map.Values) list.Sort((a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : string.CompareOrdinal(a.Item2.Id, b.Item2.Id));
        return map;
    }

    private static void CheckLadders(ContentSet set, Report report)
    {
        Registry actions = set.Reg("actions");
        foreach (var (action, rungs) in Ladders(set))
        {
            ContentValue? def = actions.Find(action);
            if (def is null) continue; // unknown action: reported as a reference error in CheckRegistryCompleteness
            if (def.GetString("class") != "family")
                report.Error("V11", $"{actions.File}:{def.Line}", $"'{action}' has method rungs but is declared an activity, not a family");

            for (int i = 1; i < rungs.Count; i++)
                if (rungs[i].Rung == rungs[i - 1].Rung)
                    report.Error("V11", At(rungs[i].Project), $"ladder '{action}': rung {rungs[i].Rung} is defined by both {rungs[i - 1].Project.Id} and {rungs[i].Project.Id}");
            int first = rungs[0].Rung;
            if (first > 1) report.Error("V11", At(rungs[0].Project), $"ladder '{action}' starts at rung {first}; it must start at 0 or 1");
            var distinct = rungs.Select(r => r.Rung).Distinct().ToList();
            for (int i = 1; i < distinct.Count; i++)
                if (distinct[i] != distinct[i - 1] + 1)
                    report.Error("V11", At(rungs.First(r => r.Rung == distinct[i]).Project), $"ladder '{action}' skips rung {distinct[i - 1] + 1}");

            Fix64? prev = null;
            string? prevMeasure = null;
            foreach (var (rung, project, effect) in rungs)
            {
                string measure = effect.Has("rate") ? "rate" : "mult";
                Fix64 value = (measure == "rate" ? effect.Num("rate") : effect.Num("mult")) ?? Fix64.Zero;
                if (prev is { } pv && prevMeasure == measure && value < pv)
                    Warn(report, project, "V11", $"ladder '{action}': rung {rung} ({project.Id}) is slower than the previous rung ({value} < {pv}); fine only if it trades speed for something else");
                prev = value;
                prevMeasure = measure;
            }
        }
    }

    // ---- V12 registry completeness ----------------------------------------------------------------
    private static void CheckRegistryCompleteness(ContentSet set, Report report)
    {
        HashSet<string> goods = set.Reg("goods").Ids(), structures = set.Reg("structures").Ids(), roles = set.Reg("roles").Ids(), actions = set.Reg("actions").Ids();
        var usedGoods = new HashSet<string>(StringComparer.Ordinal);
        var usedStructures = new HashSet<string>(StringComparer.Ordinal);
        var usedRoles = new HashSet<string>(StringComparer.Ordinal);
        var usedActions = new HashSet<string>(StringComparer.Ordinal);

        foreach (Project p in set.Projects)
            foreach (Effect e in p.Effects)
            {
                string at = $"{p.File}:{e.Line}";
                void Need(HashSet<string> registry, HashSet<string> used, string registryName, string? id)
                {
                    if (id is null) return;
                    used.Add(id);
                    if (!registry.Contains(id)) report.Error("V12", at, $"{p.Id}: {e.Kind} refers to {registryName} '{id}', which is not in the registry");
                }
                switch (e.Kind)
                {
                    case "good": Need(goods, usedGoods, "good", e.Str("id")); break;
                    case "build": case "wonder": Need(structures, usedStructures, "structure", e.Str("structure")); break;
                    case "role": Need(roles, usedRoles, "role", e.Str("id")); break;
                    case "unlock_action": case "method": Need(actions, usedActions, "action", e.Str("action")); break;
                    case "unlock":
                        if (e.Str("project") is { } pid && !set.ById.ContainsKey(pid)) report.Error("V2", at, $"{p.Id}: unlock refers to unknown project {pid}");
                        break;
                }
            }

        void Unused(string registry, HashSet<string> used, Func<ContentValue, bool> relevant)
        {
            foreach (ContentValue r in set.Reg(registry).Records)
            {
                string id = r.GetString("id") ?? "";
                if (!used.Contains(id) && relevant(r))
                    report.Warning("V12", $"{set.Reg(registry).File}:{r.Line}", $"{registry} entry '{id}' is not used by any numeric project");
            }
        }
        bool NumericOwner(string? projectId) => projectId is not null && set.ById.TryGetValue(projectId, out Project? p) && p.Status != "draft";
        Unused("goods", usedGoods, r => r.GetStringList("first_from").Any(NumericOwner));
        Unused("structures", usedStructures, r => NumericOwner(r.GetString("unlocked_by")));
        Unused("roles", usedRoles, r => NumericOwner(r.GetString("unlocked_by")));
        Unused("actions", usedActions, r => r.GetString("class") == "activity");

        // a structure's unlocking project should actually build or wonder it
        foreach (ContentValue s in set.Reg("structures").Records)
        {
            string id = s.GetString("id") ?? "";
            if (set.ById.TryGetValue(s.GetString("unlocked_by") ?? "", out Project? owner) && owner.Status != "draft"
                && !owner.Effects.Any(e => (e.Kind is "build" or "wonder") && e.Str("structure") == id))
                report.Warning("V12", $"{set.Reg("structures").File}:{s.Line}", $"structure '{id}' says {owner.Id} unlocks it, but {owner.Id} has no build effect for it");
        }
    }

    // ---- V13 costs --------------------------------------------------------------------------------
    private static void CheckCosts(ContentSet set, Report report)
    {
        foreach (Project p in set.Projects)
        {
            if (p.CostOverride is not null && string.IsNullOrWhiteSpace(p.CostReason))
                report.Error("V13", At(p), $"{p.Id} overrides its cost but gives no cost_reason");
            if (set.CostOf(p) is null)
                report.Error("V13", At(p), $"{p.Id}: cannot compute a cost (missing era {p.Era} base cost or type multiplier {p.Type})");
        }
    }

    // ---- V14 practice -----------------------------------------------------------------------------
    private static void CheckPractice(ContentSet set, Report report)
    {
        Registry practice = set.Reg("practice");
        HashSet<string> lineageCodes = set.Lineages.Select(l => l.Code).ToHashSet(StringComparer.Ordinal);
        foreach (ContentValue a in practice.Records)
        {
            foreach (string field in new[] { "knowhow", "knowhow_from_era2" })
            {
                if (a.Get(field) is not { Kind: ValueKind.Map } map) continue;
                long sum = 0;
                foreach (KeyValuePair<string, ContentValue> e in map.Entries)
                {
                    sum += e.Value.IntValue;
                    if (!lineageCodes.Contains(e.Key)) report.Error("V14", $"{practice.File}:{a.Line}", $"practice '{a.GetString("id")}' {field} names unknown lineage {e.Key}");
                }
                if (sum != 100) report.Error("V14", $"{practice.File}:{a.Line}", $"practice '{a.GetString("id")}' {field} sums to {sum}, not 100");
            }
        }

        HashSet<string> actions = practice.Ids();
        foreach (Project p in set.Projects)
        {
            if (p.Status == "draft" || p.IsKeystone) continue;
            if (p.Practice.Count == 0) { report.Error("V14", At(p), $"{p.Id} ({p.Status}) has no practice"); continue; }
            int sum = p.Practice.Sum(x => x.Share);
            if (sum != 100) report.Error("V14", At(p), $"{p.Id}: practice shares sum to {sum}, not 100");
            bool feeds = false;
            foreach (PracticeShare s in p.Practice)
            {
                if (!actions.Contains(s.Action)) { report.Error("V14", At(p), $"{p.Id}: practice action '{s.Action}' is not in the practice registry"); continue; }
                if (practice.Find(s.Action)?.Get("knowhow")?.Get(p.Lineage) is not null) feeds = true;
            }
            if (!feeds) Warn(report, p, "V14", $"{p.Id}: none of its practice actions feeds its own lineage {p.Lineage}");
        }
    }

    // ---- V17 text ---------------------------------------------------------------------------------
    private static void CheckText(ContentSet set, Report report)
    {
        foreach (Project p in set.Projects)
        {
            if (p.Status == "draft") continue;
            if (string.IsNullOrWhiteSpace(p.Flavor)) report.Error("V17", At(p), $"{p.Id} ({p.Status}) has no flavor line");
            if (p.Era <= 4 && string.IsNullOrWhiteSpace(p.Moment)) report.Error("V17", At(p), $"{p.Id} (era {p.Era}) has no moment text");
        }
    }

    // ---- V18 openings -----------------------------------------------------------------------------
    private static void CheckOpenings(ContentSet set, Report report)
    {
        Registry reg = set.Reg("openings");
        foreach (ContentValue o in reg.Records)
        {
            string name = o.GetString("id") ?? "?";
            string at = $"{reg.File}:{o.Line}";
            var done = new HashSet<string>(StringComparer.Ordinal);
            int era = 0;
            foreach (string step in o.GetStringList("steps"))
            {
                if (!set.ById.TryGetValue(step, out Project? p)) continue; // V2 already reported it
                if (!done.Add(step)) { report.Error("V18", at, $"opening '{name}': {step} appears twice"); continue; }
                if (p.IsKeystone)
                {
                    if (p.Era != era) { report.Error("V18", at, $"opening '{name}': {step} leaves era {p.Era} but the opening is in era {era}"); break; }
                    int inEra = done.Count(id => set.ById[id] is { IsKeystone: false } q && q.Era == era);
                    if (inEra < p.BreadthMin) report.Error("V18", at, $"opening '{name}': {step} needs {p.BreadthMin} era-{era} projects but only {inEra} are done");
                    era++;
                }
                else if (p.Era > era)
                {
                    report.Error("V18", at, $"opening '{name}': {step} is an era-{p.Era} project in era {era}");
                }
                foreach (string r in p.Requires)
                    if (!done.Contains(r)) report.Error("V18", at, $"opening '{name}': {step} needs {r} first");
                if (p.RequiresAny.Count > 0 && !p.RequiresAny.Any(done.Contains))
                    report.Error("V18", at, $"opening '{name}': {step} needs one of [{string.Join(", ", p.RequiresAny)}] first");
            }
        }
    }
}

public static class ReportExtensions
{
    public static bool HasErrorsOf(this Report report, string code) => report.Count(code, Severity.Error) > 0;
}
