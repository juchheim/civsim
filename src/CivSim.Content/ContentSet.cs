using System;
using System.Collections.Generic;
using CivSim.Sim.Content;
using CivSim.Sim.Numerics;

namespace CivSim.Content;

public sealed record Gate(string Kind, string Target);

public sealed record PracticeShare(string Action, int Share);

/// <summary>One effect of a project: its kind and its field map.</summary>
public sealed class Effect
{
    public Effect(ContentValue map, string file)
    {
        Map = map;
        File = file;
        Kind = map.GetString("kind") ?? "";
    }

    public ContentValue Map { get; }
    public string File { get; }
    public string Kind { get; }
    public int Line => Map.Line;
    public string? Str(string field) => Map.GetString(field);
    public long? Int(string field) => Map.GetInt(field);
    public Fix64? Num(string field) => Map.GetFix(field);
    public bool Has(string field) => Map.Has(field);
}

/// <summary>A project or an era keystone, as authored.</summary>
public sealed class Project
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Lineage { get; init; }   // lineage code, or "KEY" for keystones
    public required string Theme { get; init; }     // craft | kin | reach | key
    public required int Era { get; init; }           // for a keystone, the era being left
    public required string Type { get; init; }
    public required string Status { get; init; }
    public required IReadOnlyList<string> Requires { get; init; }
    public required IReadOnlyList<string> RequiresAny { get; init; }
    public required IReadOnlyList<Gate> Gates { get; init; }
    public Fix64? CostOverride { get; init; }
    public string? CostReason { get; init; }
    public required IReadOnlyList<Effect> Effects { get; init; }
    public required IReadOnlyList<PracticeShare> Practice { get; init; }
    public string? Flavor { get; init; }
    public string? Moment { get; init; }
    /// <summary>Warning codes the author has accepted for this project, with <see cref="AckReason"/>.</summary>
    public IReadOnlyList<string> Ack { get; init; } = Array.Empty<string>();
    public string? AckReason { get; init; }
    public bool IsKeystone { get; init; }
    public int Enters { get; init; }
    public int BreadthEra { get; init; }
    public int BreadthMin { get; init; }
    public required string File { get; init; }
    public required ContentValue Tree { get; init; }
    public int Line => Tree.Line;
    public IEnumerable<string> AllRequirements()
    {
        foreach (string r in Requires) yield return r;
        foreach (string r in RequiresAny) yield return r;
    }
}

public sealed record Lineage(string Code, string Theme, string Name, string File, ContentValue Tree);

/// <summary>A registry file: a named list of records, each with a unique id.</summary>
public sealed class Registry
{
    public Registry(string name, string file, IReadOnlyList<ContentValue> records)
    {
        Name = name;
        File = file;
        Records = records;
    }

    public string Name { get; }
    public string File { get; }
    public IReadOnlyList<ContentValue> Records { get; }

    public ContentValue? Find(string id)
    {
        foreach (ContentValue r in Records)
            if ((r.GetString("id") ?? (r.Get("id") is { Kind: ValueKind.Int } i ? i.ToString() : null)) == id) return r;
        return null;
    }

    public HashSet<string> Ids()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (ContentValue r in Records)
        {
            ContentValue? id = r.Get("id");
            if (id is not null && !id.IsNull) set.Add(id.ToString());
        }
        return set;
    }
}

/// <summary>
/// All content loaded from a source: lineages, projects, keystones, registries and constants, each already
/// checked against its schema (V1). Rule checks (V2 onward) are in <see cref="Validator"/>.
/// </summary>
public sealed class ContentSet
{
    public static readonly string[] RegistryNames =
    {
        "eras", "actions", "practice", "goods", "structures", "tools", "roles", "biomes", "fauna", "deposits",
        "archetypes", "difficulty", "guarantees", "openings", "recipes",
    };

    public List<Lineage> Lineages { get; } = new();
    public List<Project> Projects { get; } = new();
    public Dictionary<string, Registry> Registries { get; } = new(StringComparer.Ordinal);
    public ContentValue? Balance { get; private set; }
    public string? BalanceFile { get; private set; }

    public Dictionary<string, Project> ById { get; } = new(StringComparer.Ordinal);

    public IEnumerable<Project> Keystones { get { foreach (Project p in Projects) if (p.IsKeystone) yield return p; } }
    public IEnumerable<Project> Regular { get { foreach (Project p in Projects) if (!p.IsKeystone) yield return p; } }

    public Registry Reg(string name)
        => Registries.TryGetValue(name, out Registry? r) ? r : new Registry(name, "(missing)", Array.Empty<ContentValue>());

    public static ContentSet Load(IContentSource source, Report report)
    {
        var set = new ContentSet();
        foreach ((string path, string text) in source.Files())
        {
            ContentValue? tree = YamlLoader.Parse(path, text, report);
            if (tree is null) continue;
            var ctx = new Ctx(path, report);
            if (tree.GetInt("schema") != 1) report.Error("V1", $"{path}:{tree.Line}", "missing or unsupported 'schema: 1'");

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (path == "catalog/keystones.yaml") set.LoadKeystones(path, tree, ctx);
            else if (path.StartsWith("catalog/", StringComparison.Ordinal)) set.LoadLineage(path, tree, ctx);
            else if (path.StartsWith("registries/", StringComparison.Ordinal) && Array.IndexOf(RegistryNames, name) >= 0) set.LoadRegistry(name, path, tree, ctx);
            else if (path == "constants/balance.yaml") set.LoadBalance(path, tree, ctx);
            else report.Warning("V1", path, "file is not part of the content layout and was ignored");
        }
        foreach (string required in RegistryNames)
            if (!set.Registries.ContainsKey(required)) report.Error("V1", "registries/" + required + ".yaml", "required registry file is missing");
        if (set.Balance is null) report.Error("V1", "constants/balance.yaml", "required constants file is missing");
        return set;
    }

    // ---- lineages and projects ---------------------------------------------------------------------
    private static ObjType ProjectSchema() => S.Obj()
        .Req("id", S.Str()).Req("name", S.Str()).Req("era", S.Int(0, 9)).Req("type", S.Enum("D", "M", "S", "R", "I", "W"))
        .Req("status", S.Enum("draft", "numeric", "tuned")).Req("requires", S.List(S.Str())).Opt("requires_any", S.List(S.Str()))
        .Opt("gates", S.List(S.Obj().Req("kind", S.Enum("seen", "has", "near", "climate")).Req("target", S.Str())))
        .Opt("cost", S.Num("0", null, minExclusive: true)).Opt("cost_reason", S.Str())
        .Req("effects", S.List(S.Obj(allowExtra: true)))
        .Opt("practice", S.List(S.Obj().Req("action", S.Str()).Req("share", S.Int(1, 100))))
        .Opt("flavor", S.Str()).Opt("moment", S.Str()).Opt("anchor", S.Str())
        .Opt("ack", S.List(S.Str())).Opt("ack_reason", S.Str());

    private static ObjType KeystoneSchema() => S.Obj()
        .Req("id", S.Str()).Req("name", S.Str()).Req("era", S.Int(0, 9)).Req("enters", S.Int(1, 10)).Req("type", S.Enum("K"))
        .Req("status", S.Enum("draft", "numeric", "tuned")).Req("requires", S.List(S.Str()))
        .Req("breadth", S.Obj().Req("era", S.Int(0, 9)).Req("min", S.Int(1, 200)))
        .Opt("cost", S.Num("0", null, minExclusive: true)).Opt("cost_reason", S.Str())
        .Req("effects", S.List(S.Obj(allowExtra: true))).Opt("flavor", S.Str()).Opt("moment", S.Str())
        .Opt("ack", S.List(S.Str())).Opt("ack_reason", S.Str());

    private void LoadLineage(string path, ContentValue tree, Ctx ctx)
    {
        new ObjType().Req("schema", S.Int(1, 1))
            .Req("lineage", S.Obj().Req("code", S.Str()).Req("theme", S.Enum("craft", "kin", "reach")).Req("name", S.Str())
                .Opt("purpose", S.Str()).Opt("anchors", S.Str()))
            .Req("projects", S.List(S.Any())).Check(tree, "", ctx);
        ContentValue? lin = tree.Get("lineage");
        if (lin is null) return;
        string code = lin.GetString("code") ?? "???", theme = lin.GetString("theme") ?? "?";
        Lineages.Add(new Lineage(code, theme, lin.GetString("name") ?? code, path, lin));
        ObjType schema = ProjectSchema();
        ContentValue? list = tree.Get("projects");
        if (list is null) return;
        foreach (ContentValue p in list.Items)
        {
            schema.Check(p, p.GetString("id") ?? "project", ctx);
            AddProject(p, path, code, theme, isKeystone: false, ctx.Report);
        }
    }

    private void LoadKeystones(string path, ContentValue tree, Ctx ctx)
    {
        new ObjType().Req("schema", S.Int(1, 1)).Req("keystones", S.List(S.Any())).Check(tree, "", ctx);
        ObjType schema = KeystoneSchema();
        ContentValue? list = tree.Get("keystones");
        if (list is null) return;
        foreach (ContentValue k in list.Items)
        {
            schema.Check(k, k.GetString("id") ?? "keystone", ctx);
            AddProject(k, path, "KEY", "key", isKeystone: true, ctx.Report);
        }
    }

    private void AddProject(ContentValue v, string file, string lineage, string theme, bool isKeystone, Report report)
    {
        string id = v.GetString("id") ?? "";
        if (id.Length == 0) return;
        var gates = new List<Gate>();
        if (v.Get("gates") is { Kind: ValueKind.List } g)
            foreach (ContentValue item in g.Items)
                gates.Add(new Gate(item.GetString("kind") ?? "", item.GetString("target") ?? ""));
        var practice = new List<PracticeShare>();
        if (v.Get("practice") is { Kind: ValueKind.List } pr)
            foreach (ContentValue item in pr.Items)
                practice.Add(new PracticeShare(item.GetString("action") ?? "", (int)(item.GetInt("share") ?? 0)));
        var effects = new List<Effect>();
        if (v.Get("effects") is { Kind: ValueKind.List } ef)
            foreach (ContentValue item in ef.Items) effects.Add(new Effect(item, file));

        var project = new Project
        {
            Id = id,
            Name = v.GetString("name") ?? id,
            Lineage = lineage,
            Theme = theme,
            Era = (int)(v.GetInt("era") ?? 0),
            Type = isKeystone ? "K" : v.GetString("type") ?? "?",
            Status = v.GetString("status") ?? "draft",
            Requires = v.GetStringList("requires"),
            RequiresAny = v.GetStringList("requires_any"),
            Gates = gates,
            CostOverride = v.GetFix("cost"),
            CostReason = v.GetString("cost_reason"),
            Effects = effects,
            Practice = practice,
            Flavor = v.GetString("flavor"),
            Moment = v.GetString("moment"),
            Ack = v.GetStringList("ack"),
            AckReason = v.GetString("ack_reason"),
            IsKeystone = isKeystone,
            Enters = (int)(v.GetInt("enters") ?? 0),
            BreadthEra = (int)(v.Get("breadth")?.GetInt("era") ?? 0),
            BreadthMin = (int)(v.Get("breadth")?.GetInt("min") ?? 0),
            File = file,
            Tree = v,
        };
        if (ById.ContainsKey(id))
        {
            report.Error("V2", $"{file}:{v.Line}", $"duplicate project id {id} (also in {ById[id].File}:{ById[id].Line})");
            return;
        }
        ById[id] = project;
        Projects.Add(project);
    }

    // ---- registries and constants ------------------------------------------------------------------
    private static ObjType Rec() => S.Obj().Req("id", S.Str()).Req("name", S.Str());

    private static FieldType RegistrySchema(string name) => name switch
    {
        "eras" => S.Obj().Req("id", S.Int(0, 9)).Req("name", S.Str()).Req("base_cost", S.Int(1)).Req("breadth", S.Int(1))
            .Req("rep_scale", S.Int(1, 10000)).Req("claim_radius", S.Int(1, 50)).Req("reach", S.Int(1, 1000)).Req("sea_level", S.Int(0, 2))
            .Req("claim_mult", S.Num("0", "10", true)).Req("winter_mult", S.Num("1", "10")).Req("fert_mult", S.Num("0", "10"))
            .Req("mort_child", S.Num("0", "1")).Req("mort_adult", S.Num("0", "1")).Req("mort_elder", S.Num("0", "1"))
            .Req("cost_mod", S.Num("0", "10", true)),
        "actions" => Rec().Req("class", S.Enum("family", "activity")).Opt("base_rate", S.Num("0", null, true)).Opt("unit", S.Str()),
        "practice" => S.Obj().Req("id", S.Str()).Req("knowhow", S.Map(S.Int(1, 100))).Opt("knowhow_from_era2", S.Map(S.Int(1, 100))).Opt("note", S.Str()),
        "goods" => Rec().Req("category", S.Enum("food", "food_feast", "material", "processed", "fuel", "metal", "tool", "container", "clothing", "luxury", "shelter", "animal", "ore", "arms"))
            .Req("first_from", S.List(S.Str())).Opt("spoilage", S.Num("0", "1")).Opt("durability", S.Str()).Req("load", S.Num("0", "100")).Opt("per", S.Int(1, 1000))
            .Req("barter", S.Num("0", "10000")),
        "structures" => Rec().Req("class", S.Enum("structure", "wonder")).Req("unlocked_by", S.Str()).Opt("labor", S.Num("0")).Opt("labor_note", S.Str())
            .Opt("materials", S.Str()).Opt("effect", S.Str()).Opt("upkeep", S.Str()),
        "tools" => Rec().Req("unlocked_by", S.List(S.Str())).Opt("make_time", S.Num("0")).Opt("inputs", S.Str()).Opt("durability", S.Str()).Opt("enables", S.Str())
            .Opt("metal", S.Str()).Opt("metal_per_kit", S.Num("0", "10", true)).Opt("service_ws", S.Num("0", "1000", true))
            .Opt("mods", S.Map(S.Num("0", "100", true))).Opt("strength", S.Num("0", "100", true))
            .Opt("casualties_taken", S.Num("0", "1", true)).Opt("open_terrain_mult", S.Num("1", "10")).Opt("crew", S.Int(1, 100)).Opt("horses", S.Int(0, 100)),
        "roles" => Rec().Req("insight", S.Int(0, 100000)).Req("unlocked_by", S.Str()).Opt("staffing", S.Str()),
        "recipes" => Rec().Req("unlocked_by", S.List(S.Str())).Opt("structure", S.List(S.Str()))
            .Req("inputs", S.Map(S.Num("0", "1000", true))).Req("outputs", S.Map(S.Num("0", "1000", true)))
            .Req("labor", S.Num("0", "100", true)).Req("order", S.Int(0, 100)).Req("practice", S.Str()).Opt("bellows", S.Bool()).Opt("note", S.Str()),
        "guarantees" => S.Obj().Req("id", S.Str()).Req("scope", S.Enum("start", "continental")).Opt("radius", S.Int(1, 1000)).Req("gate", S.Str()),
        "archetypes" => Rec().Req("leaning", S.Map(S.Num("0", "1"))).Req("raid_margin", S.Str()).Req("expansion", S.Str()).Req("diplomacy", S.Str()).Req("signature", S.Str()),
        "difficulty" => Rec().Req("horizon_eras", S.Int(1, 10)).Req("beam_width", S.Int(1, 1000)).Req("opponent_model", S.Str()).Req("replan", S.Str())
            .Req("council_prior_pct", S.Int(0, 100)).Opt("note", S.Str()),
        "openings" => Rec().Req("steps", S.List(S.Str())),
        _ => S.Obj(allowExtra: true).Req("id", S.Str()).Req("name", S.Str()), // biomes, fauna, deposits: world-generation tables
    };

    private void LoadRegistry(string name, string path, ContentValue tree, Ctx ctx)
    {
        new ObjType().Req("schema", S.Int(1, 1)).Req(name, S.List(S.Any())).Check(tree, "", ctx);
        ContentValue? list = tree.Get(name);
        if (list is null || list.Kind != ValueKind.List) return;
        FieldType schema = RegistrySchema(name);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (ContentValue rec in list.Items)
        {
            schema.Check(rec, name + "." + (rec.Get("id")?.ToString() ?? "?"), ctx);
            string id = rec.Get("id")?.ToString() ?? "";
            if (id.Length > 0 && !seen.Add(id)) ctx.Report.Error("V2", $"{path}:{rec.Line}", $"duplicate id '{id}' in registry {name}");
        }
        Registries[name] = new Registry(name, path, list.Items);
    }

    private void LoadBalance(string path, ContentValue tree, Ctx ctx)
    {
        new ObjType().Req("schema", S.Int(1, 1))
            .Req("type_mult", S.Map(S.Num("0", "10", true)))
            .Req("constants", S.List(S.Obj().Req("id", S.Str()).Req("value", S.Num("-2000000000", "2000000000")).Req("unit", S.Str()).Req("source", S.Str())))
            .Check(tree, "", ctx);
        Balance = tree;
        BalanceFile = path;
    }

    // ---- costs (docs/MVP/02 §8.3) ------------------------------------------------------------------
    public Fix64? EraBase(int era)
    {
        ContentValue? rec = Reg("eras").Find(era.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return rec?.GetInt("base_cost") is { } c ? Fix64.FromInt(c) : null;
    }

    public Fix64? TypeMult(string type) => Balance?.Get("type_mult")?.GetFix(type);

    /// <summary>EraBase times the type multiplier, or the project's own override.</summary>
    public Fix64? CostOf(Project p)
    {
        if (p.CostOverride is { } o) return o;
        return EraBase(p.Era) is { } b && TypeMult(p.Type) is { } m ? b * m : null;
    }
}
