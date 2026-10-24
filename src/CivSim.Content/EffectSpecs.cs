using System;
using System.Collections.Generic;
using CivSim.Sim.Content;
using CivSim.Sim.Numerics;

namespace CivSim.Content;

/// <summary>
/// The effect primitives (docs/MVP/11 §1) as typed specs, so effects can be checked (V10) and executed
/// later. Mechanical kinds carry numbers; behavioural kinds carry a text note that the simulation turns into
/// code. A <c>prose</c> effect is only allowed on draft projects.
/// </summary>
public static class EffectSpecs
{
    public const string Prose = "prose";

    private static readonly string[] HazardCauses =
        { "endemic", "epidemic", "starvation", "exposure", "injury", "predators", "violence", "child", "age0", "maternal" };

    private static readonly string[] TextKinds =
    {
        "rule", "claim", "reach", "reveal", "show", "planner", "risk", "gate", "diplo", "relations", "combat",
        "lever", "know_how", "legacy", "presence", "need", "event", "highlight",
    };

    /// <summary>The six Decrees a Mandate effect may price (docs/MVP/23 §1.2, 22 §4.3).</summary>
    public static readonly string[] Decrees = { "found", "expedition", "mine", "levy", "corvee", "compact" };

    public static readonly string[] CohesionScopes = { "all", "here", "radius", "capital" };

    private static readonly List<KeyValuePair<string, ObjType>> Specs = Build();

    private static List<KeyValuePair<string, ObjType>> Build()
    {
        var list = new List<KeyValuePair<string, ObjType>>();
        void Add(string kind, ObjType spec) => list.Add(new(kind, spec.Req("kind", S.Str())));

        Add("method", S.Obj().Req("action", S.Str()).Req("rung", S.Int(0, 30)).Req("name", S.Str())
            .Opt("mult", S.Num("1", "100")).Opt("rate", S.Num("0", "1000", minExclusive: true)).Opt("unit", S.Str()).Opt("note", S.Str()));
        Add("mod", S.Obj().Req("stat", S.Str()).Req("op", S.Enum("pct", "mult", "flat", "set")).Req("value", S.Num("-1000", "1000000"))
            .Opt("note", S.Str()));
        Add("cap", S.Obj().Req("cap", S.Enum("food", "lean", "housing", "group")).Opt("mult", S.Num("1", "5")).Opt("take", S.Num("0", "1", minExclusive: true))
            .Opt("value", S.Int(1, 1_000_000_000)).Opt("note", S.Str()));
        Add("hazard", S.Obj().Req("cause", S.Enum(HazardCauses)).Req("mult", S.Num("0", "2.5", minExclusive: true))
            .Opt("scope", S.Str()).Opt("note", S.Str()));
        Add("good", S.Obj().Req("id", S.Str()).Opt("note", S.Str()));
        Add("build", S.Obj().Req("structure", S.Str()).Opt("note", S.Str()));
        Add("wonder", S.Obj().Req("structure", S.Str()).Opt("note", S.Str()));
        Add("role", S.Obj().Req("id", S.Str()).Opt("insight", S.Int(0, 10_000)).Opt("note", S.Str()));
        Add("unlock_action", S.Obj().Req("action", S.Str()).Opt("note", S.Str()));
        Add("cohesion", S.Obj().Req("value", S.Int(-50, 50)).Opt("scope", S.Enum(CohesionScopes)).Opt("radius", S.Int(1, 50)).Opt("note", S.Str()));
        Add("adopt", S.Obj().Req("mult", S.Num("0", "20", minExclusive: true)));
        Add("era_advance", S.Obj().Req("era", S.Int(1, 9)));
        Add("tier", S.Obj().Req("tier", S.Str()).Opt("min_pop", S.Int(1, 1_000_000)).Opt("needs", S.List(S.Str())).Opt("radius", S.Int(1, 50))
            .Opt("claim_mult", S.Num("0", "10", minExclusive: true)).Opt("note", S.Str()));
        Add("admin", S.Obj().Opt("span", S.Int(0, 20)).Opt("overreach", S.Num("0", "1")).Opt("note", S.Str()));
        // decree is checked by the validator (V19) so an unknown Decree is named as such; a mandate effect carries deltas
        Add("mandate", S.Obj().Opt("start", S.Int(0, 10)).Opt("cap", S.Int(-5, 10)).Opt("recharge", S.Int(-10, 10))
            .Opt("decree", S.Str()).Opt("cost", S.Int(0, 5)).Opt("note", S.Str()));
        Add("sanitation", S.Obj().Req("value", S.Num("0", "1")).Opt("note", S.Str()));
        // text-only defense (WAR-03) stays valid; a typed one names its structure and multiplier
        Add("defense", S.Obj().Opt("structure", S.Str()).Opt("mult", S.Num("1", "10")).Opt("loot", S.Num("0", "1"))
            .Opt("refuge_radius", S.Int(1, 50)).Opt("site_within", S.Int(1, 50)).Opt("assault", S.Bool()).Opt("text", S.Str()));
        Add("unlock", S.Obj().Req("text", S.Str()).Opt("project", S.Str()));
        Add(Prose, S.Obj().Req("text", S.Str()));
        foreach (string kind in TextKinds) Add(kind, S.Obj().Req("text", S.Str()));
        return list;
    }

    public static IEnumerable<string> Kinds()
    {
        foreach (KeyValuePair<string, ObjType> kv in Specs) yield return kv.Key;
    }

    /// <summary>Checks one effect (check V10): known kind, typed fields in range, and the cross-field rules.</summary>
    public static void Validate(ContentValue effect, string projectStatus, string projectId, string file, Report report)
    {
        string where = $"{file}:{effect.Line}";
        string? kind = effect.GetString("kind");
        if (kind is null)
        {
            report.Error("V10", where, $"{projectId}: effect has no 'kind'");
            return;
        }
        ObjType? spec = null;
        foreach (KeyValuePair<string, ObjType> kv in Specs) if (kv.Key == kind) { spec = kv.Value; break; }
        if (spec is null)
        {
            report.Error("V10", where, $"{projectId}: unknown effect kind '{kind}'");
            return;
        }

        var sub = new Report();
        spec.Check(effect, "effect", new Ctx(file, sub));
        foreach (Issue i in sub.Issues) report.Error("V10", i.Location, $"{projectId}: {i.Message}");

        if (kind == Prose && projectStatus != "draft")
            report.Error("V10", where, $"{projectId}: a prose effect is only allowed on draft projects (status is {projectStatus})");

        switch (kind)
        {
            case "method" when effect.Has("mult") == effect.Has("rate"):
                report.Error("V10", where, $"{projectId}: a method needs exactly one of 'mult' or 'rate'");
                break;
            case "method" when effect.Has("rate") && !effect.Has("unit"):
                report.Error("V10", where, $"{projectId}: a method with a 'rate' needs a 'unit'");
                break;
            case "cohesion" when effect.GetString("scope") == "radius" && !effect.Has("radius"):
                report.Error("V10", where, $"{projectId}: a cohesion with scope 'radius' needs a 'radius'");
                break;
            case "cohesion" when effect.Has("radius") && effect.GetString("scope") != "radius":
                report.Error("V10", where, $"{projectId}: a cohesion 'radius' only applies with scope 'radius'");
                break;
            case "admin" when !effect.Has("span") && !effect.Has("overreach"):
                report.Error("V10", where, $"{projectId}: an admin effect needs 'span' or 'overreach'");
                break;
            case "mandate" when !(effect.Has("start") || effect.Has("cap") || effect.Has("recharge") || effect.Has("decree") || effect.Has("cost")):
                report.Error("V10", where, $"{projectId}: a mandate effect needs at least one of start, cap, recharge, decree or cost");
                break;
            case "mandate" when effect.Has("decree") != effect.Has("cost"):
                report.Error("V10", where, $"{projectId}: a mandate 'decree' and its 'cost' go together");
                break;
            case "defense" when !effect.Has("text") && !(effect.Has("structure") && effect.Has("mult")):
                report.Error("V10", where, $"{projectId}: a defense is either text-only or names a 'structure' and a 'mult'");
                break;
            case "defense" when effect.Has("structure") != effect.Has("mult")
                || (!effect.Has("structure") && (effect.Has("loot") || effect.Has("refuge_radius") || effect.Has("site_within") || effect.Has("assault"))):
                report.Error("V10", where, $"{projectId}: a typed defense names a 'structure' and a 'mult' together");
                break;
            case "cap":
                bool hasMult = effect.Has("mult"), hasValue = effect.Has("value");
                string? cap = effect.GetString("cap");
                if (hasMult == hasValue) report.Error("V10", where, $"{projectId}: a cap needs exactly one of 'mult' or 'value'");
                else if (cap == "group" && !hasValue) report.Error("V10", where, $"{projectId}: a group cap is a 'value' (a ladder step), not a multiplier");
                else if (cap != "group" && hasValue) report.Error("V10", where, $"{projectId}: a {cap} cap is a multiplier, not a 'value'");
                if (effect.Has("take") && !hasMult) report.Error("V10", where, $"{projectId}: 'take' only applies to multiplier caps");
                break;
        }
    }
}
