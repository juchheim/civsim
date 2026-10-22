using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Content;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Research;

public enum ProjectType : byte { Discovery, Method, Structure, Role, Institution, Wonder, Keystone }

public enum Theme : byte { Craft = 0, Kin = 1, Reach = 2, Keystone = 3 }

public enum GateKind : byte { Seen, Has, Near, Climate }

/// <summary>An exposure or environment condition on a project (docs/MVP/02 §6). <see cref="Target"/> indexes <see cref="ResearchCatalog.GateTargets"/>.</summary>
public readonly record struct Gate(GateKind Kind, int Target);

/// <summary>The effect primitives of docs/MVP/11 §1 that numeric projects use.</summary>
public enum EffectKind : byte
{
    Method, Mod, Cap, Hazard, Good, Build, Wonder, Role, UnlockAction, Cohesion, Adopt, EraAdvance, Tier, Unlock, Prose,
    Rule, Claim, Reach, Reveal, Show, Planner, Risk, Gate, Diplo, Relations, Combat, Defense, Lever, KnowHow, Legacy,
    Presence, Need, Event, Highlight,
}

public enum CapKind : byte { Food = 0, Lean = 1, Housing = 2, Group = 3 }

/// <summary>What a player would see change when an effect lands (docs/MVP/11 §6 check C8, docs/MVP/08).</summary>
public enum VisibleChange : byte
{
    /// <summary>Figures start doing something new.</summary>
    NewAction,
    /// <summary>A new rung on a method ladder: a better tool sprite and a faster job.</summary>
    ToolOrMethod,
    /// <summary>A new tool, carried good or trade item appears in figures' hands.</summary>
    Good,
    /// <summary>A new building the planner may place.</summary>
    Structure,
    /// <summary>Figures with a distinct outfit.</summary>
    Role,
    /// <summary>A settlement tier banner.</summary>
    Tier,
    /// <summary>A map overlay or a lever: claims, reveals, beacons.</summary>
    Overlay,
    /// <summary>A measurable behaviour change shown as an efficiency badge, a hazard or cap readout, or a Chronicle line.</summary>
    Behavior,
    /// <summary>The era celebration.</summary>
    EraCelebration,
    /// <summary>The effect has no visual of its own (gates, risks, legacy bookkeeping).</summary>
    None,
}

/// <summary>One effect of a project, parsed from the content pack.</summary>
public sealed class EffectDef
{
    public EffectKind Kind { get; init; }
    public string? Stat { get; init; }
    /// <summary>pct, mult, flat or set (mods only).</summary>
    public string? Op { get; init; }
    /// <summary>The numeric payload: a mod value, a hazard or cap multiplier, a cap step, an adopt multiplier, a role output.</summary>
    public Fix64 Value { get; init; }
    public bool HasValue { get; init; }
    /// <summary>Probability that an average civilization takes the effect's project (caps).</summary>
    public Fix64 Take { get; init; } = Fix64.One;
    public CapKind? Cap { get; init; }
    public string? Id { get; init; }
    public string? Text { get; init; }
    public string? Cause { get; init; }
    public string? Action { get; init; }
    public string? Note { get; init; }
    public long Int { get; init; }

    public VisibleChange Visual => Kind switch
    {
        EffectKind.UnlockAction => VisibleChange.NewAction,
        EffectKind.Method => VisibleChange.ToolOrMethod,
        EffectKind.Good => VisibleChange.Good,
        EffectKind.Build or EffectKind.Wonder => VisibleChange.Structure,
        EffectKind.Role => VisibleChange.Role,
        EffectKind.Tier => VisibleChange.Tier,
        EffectKind.Claim or EffectKind.Reach or EffectKind.Reveal or EffectKind.Show or EffectKind.Highlight
            or EffectKind.Presence or EffectKind.Lever => VisibleChange.Overlay,
        EffectKind.EraAdvance => VisibleChange.EraCelebration,
        EffectKind.Mod or EffectKind.Cap or EffectKind.Hazard or EffectKind.Cohesion or EffectKind.Adopt
            or EffectKind.Unlock or EffectKind.Rule or EffectKind.Planner or EffectKind.Diplo or EffectKind.Relations
            or EffectKind.Combat or EffectKind.Defense or EffectKind.KnowHow or EffectKind.Event => VisibleChange.Behavior,
        EffectKind.Gate or EffectKind.Risk or EffectKind.Need or EffectKind.Legacy or EffectKind.Prose => VisibleChange.None,
        _ => VisibleChange.None,
    };
}

/// <summary>A project, compiled from the content pack into the form the research system uses.</summary>
public sealed class ProjectDef
{
    public int Index { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int Era { get; init; }
    public ProjectType Type { get; init; }
    public Theme Theme { get; init; }
    public int Lineage { get; init; }
    /// <summary>The list cost (EraBase × TypeMult, or an override). Effective cost adds Diffusion, EraLag and gate factors.</summary>
    public Fix64 Cost { get; init; }
    /// <summary>Only <c>numeric</c> projects can be researched; <c>draft</c> projects are present but gated off.</summary>
    public bool Numeric { get; init; }
    public required int[] Requires { get; init; }
    /// <summary>One-of prerequisites (empty when none).</summary>
    public required int[] RequiresAny { get; init; }
    public required Gate[] Gates { get; init; }
    public required EffectDef[] Effects { get; init; }
    /// <summary>Practice action ids with their share (percent) of the project's lineage practice.</summary>
    public required (string Action, int Share)[] Practice { get; init; }
    public string Moment { get; init; } = "";
    public string Flavor { get; init; } = "";
    /// <summary>Keystones: the era whose completed-project count is required, and how many.</summary>
    public int BreadthEra { get; init; } = -1;
    public int BreadthMin { get; init; }
    /// <summary>Keystones: the era this keystone enters.</summary>
    public int Enters { get; init; } = -1;
    public bool IsKeystone => Type == ProjectType.Keystone;
    /// <summary>Discoveries and methods are skills that spread person to person (docs/MVP/02 §10); everything else lands at once.</summary>
    public bool SpreadsAsSkill => Type is ProjectType.Discovery or ProjectType.Method;
}

/// <summary>One role from the roles registry.</summary>
public sealed class RoleDef
{
    public required string Id { get; init; }
    public Fix64 Insight { get; init; }
    public int UnlockedBy { get; init; } = -1;
}

/// <summary>The compiled research catalog: projects, lineages, eras, roles and practice maps, all with dense indices.</summary>
public sealed class ResearchCatalog
{
    public const int EraCount = 10;
    public const int CauseCount = 10;

    /// <summary>Hazard causes in index order (the order of EffectSpecs).</summary>
    public static readonly string[] HazardCauses =
        { "endemic", "epidemic", "starvation", "exposure", "injury", "predators", "violence", "child", "age0", "maternal" };

    public IReadOnlyList<ProjectDef> Projects { get; }
    public IReadOnlyList<string> LineageCodes { get; }
    public IReadOnlyList<RoleDef> Roles { get; }
    public IReadOnlyList<string> PracticeIds { get; }
    public IReadOnlyList<string> GateTargets { get; }
    public IReadOnlyList<string> StatNames { get; }
    public Fix64[] EraBase { get; } = new Fix64[EraCount];
    public int[] EraBreadth { get; } = new int[EraCount];
    public ContentPack Pack { get; }
    public int Count => Projects.Count;

    private readonly string[] _ids;
    private readonly int[] _keystoneByLeavingEra = new int[EraCount];
    private readonly bool[][] _closure;
    private readonly (int Lineage, int Percent)[][] _practiceLineages;
    private readonly (int Lineage, int Percent)[][] _practiceLineagesFromEra2;
    private readonly bool[] _practiceHasEra2;
    private readonly ContentValue _constants;

    public ResearchCatalog(ContentPack pack)
    {
        Pack = pack;
        ContentValue root = pack.Root;
        ContentValue registries = root.Get("registries") ?? throw new InvalidOperationException("Pack has no registries.");

        var lineageCodes = new List<string>();
        var lineageTheme = new List<Theme>();
        foreach (ContentValue l in pack.List("lineages"))
        {
            lineageCodes.Add(l.GetString("code")!);
            lineageTheme.Add(ParseTheme(l.GetString("theme")));
        }
        LineageCodes = lineageCodes;

        foreach (ContentValue era in Registry(registries, "eras"))
        {
            int id = (int)era.GetInt("id")!;
            EraBase[id] = era.GetFix("base_cost")!.Value;
            EraBreadth[id] = (int)era.GetInt("breadth")!;
        }

        var practiceIds = new List<string>();
        foreach (ContentValue p in Registry(registries, "practice")) practiceIds.Add(p.GetString("id")!);
        PracticeIds = practiceIds;
        _practiceLineages = new (int, int)[practiceIds.Count][];
        _practiceLineagesFromEra2 = new (int, int)[practiceIds.Count][];
        _practiceHasEra2 = new bool[practiceIds.Count];
        int pi = 0;
        foreach (ContentValue p in Registry(registries, "practice"))
        {
            _practiceLineages[pi] = LineageShares(p.Get("knowhow"));
            if (p.Get("knowhow_from_era2") is { } later)
            {
                _practiceLineagesFromEra2[pi] = LineageShares(later);
                _practiceHasEra2[pi] = true;
            }
            pi++;
        }

        var gateTargets = new SortedSet<string>(StringComparer.Ordinal);
        var stats = new SortedSet<string>(StringComparer.Ordinal);
        IReadOnlyList<ContentValue> rawProjects = pack.List("projects");
        foreach (ContentValue raw in rawProjects)
        {
            foreach (ContentValue g in ListOf(raw, "gates")) gateTargets.Add(g.GetString("target")!);
            foreach (ContentValue e in ListOf(raw, "effects"))
                if (e.GetString("kind") == "mod" && e.GetString("stat") is { } stat) stats.Add(stat);
        }
        GateTargets = new List<string>(gateTargets);
        StatNames = new List<string>(stats);

        _ids = new string[rawProjects.Count];
        for (int i = 0; i < rawProjects.Count; i++) _ids[i] = rawProjects[i].GetString("id")!;
        // Packs list projects in ordinal id order; the binary search below depends on it.
        for (int i = 1; i < _ids.Length; i++)
            if (string.CompareOrdinal(_ids[i - 1], _ids[i]) >= 0) throw new InvalidDataException("Pack projects are not in id order.");

        var projects = new List<ProjectDef>(rawProjects.Count);
        for (int i = 0; i < rawProjects.Count; i++) projects.Add(BuildProject(rawProjects[i], i, lineageCodes, lineageTheme, gateTargets));
        Projects = projects;

        Array.Fill(_keystoneByLeavingEra, -1);
        foreach (ProjectDef p in projects)
            if (p.IsKeystone) _keystoneByLeavingEra[p.Era] = p.Index;

        _closure = new bool[EraCount][];
        for (int era = 0; era < EraCount; era++)
            if (_keystoneByLeavingEra[era] >= 0) _closure[era] = ComputeClosure(_keystoneByLeavingEra[era]);

        var roles = new List<RoleDef>();
        foreach (ContentValue r in Registry(registries, "roles"))
        {
            string? unlocked = r.GetString("unlocked_by");
            roles.Add(new RoleDef
            {
                Id = r.GetString("id")!,
                Insight = r.GetFix("insight") ?? Fix64.Zero,
                UnlockedBy = unlocked is null ? -1 : IndexOf(unlocked),
            });
        }
        Roles = roles;

        _constants = root.Get("balance")?.Get("constants") ?? ContentValue.ListOf(Array.Empty<ContentValue>());
    }

    private static IReadOnlyList<ContentValue> Registry(ContentValue registries, string name)
        => registries.Get(name) is { Kind: ValueKind.List } v ? v.Items : Array.Empty<ContentValue>();

    private static IReadOnlyList<ContentValue> ListOf(ContentValue map, string key)
        => map.Get(key) is { Kind: ValueKind.List } v ? v.Items : Array.Empty<ContentValue>();

    private (int Lineage, int Percent)[] LineageShares(ContentValue? map)
    {
        var list = new List<(int, int)>();
        if (map is { Kind: ValueKind.Map })
            foreach (KeyValuePair<string, ContentValue> kv in map.Entries)
            {
                int lineage = LineageIndex(kv.Key);
                if (lineage >= 0) list.Add((lineage, (int)kv.Value.IntValue));
            }
        return list.ToArray();
    }

    private static Theme ParseTheme(string? text) => text switch
    {
        "craft" => Theme.Craft,
        "kin" => Theme.Kin,
        "reach" => Theme.Reach,
        "key" => Theme.Keystone,
        _ => throw new InvalidDataException($"Unknown theme '{text}'."),
    };

    private static ProjectType ParseType(string? text) => text switch
    {
        "D" => ProjectType.Discovery,
        "M" => ProjectType.Method,
        "S" => ProjectType.Structure,
        "R" => ProjectType.Role,
        "I" => ProjectType.Institution,
        "W" => ProjectType.Wonder,
        "K" => ProjectType.Keystone,
        _ => throw new InvalidDataException($"Unknown project type '{text}'."),
    };

    private static readonly (string Name, EffectKind Kind)[] EffectKinds =
    {
        ("adopt", EffectKind.Adopt), ("build", EffectKind.Build), ("cap", EffectKind.Cap), ("claim", EffectKind.Claim),
        ("cohesion", EffectKind.Cohesion), ("combat", EffectKind.Combat), ("defense", EffectKind.Defense),
        ("diplo", EffectKind.Diplo), ("era_advance", EffectKind.EraAdvance), ("event", EffectKind.Event),
        ("good", EffectKind.Good), ("gate", EffectKind.Gate), ("hazard", EffectKind.Hazard),
        ("highlight", EffectKind.Highlight), ("know_how", EffectKind.KnowHow), ("legacy", EffectKind.Legacy),
        ("lever", EffectKind.Lever), ("method", EffectKind.Method), ("mod", EffectKind.Mod), ("need", EffectKind.Need),
        ("planner", EffectKind.Planner), ("presence", EffectKind.Presence), ("prose", EffectKind.Prose),
        ("reach", EffectKind.Reach), ("relations", EffectKind.Relations), ("reveal", EffectKind.Reveal),
        ("risk", EffectKind.Risk), ("role", EffectKind.Role), ("rule", EffectKind.Rule), ("show", EffectKind.Show),
        ("tier", EffectKind.Tier), ("unlock", EffectKind.Unlock), ("unlock_action", EffectKind.UnlockAction),
        ("wonder", EffectKind.Wonder),
    };

    private static EffectKind ParseEffectKind(string? name)
    {
        foreach ((string n, EffectKind k) in EffectKinds)
            if (string.Equals(n, name, StringComparison.Ordinal)) return k;
        throw new InvalidDataException($"Unknown effect kind '{name}'.");
    }

    private static CapKind ParseCap(string? text) => text switch
    {
        "food" => CapKind.Food,
        "lean" => CapKind.Lean,
        "housing" => CapKind.Housing,
        "group" => CapKind.Group,
        _ => throw new InvalidDataException($"Unknown cap '{text}'."),
    };

    private ProjectDef BuildProject(ContentValue raw, int index, List<string> lineageCodes, List<Theme> lineageTheme,
        SortedSet<string> gateTargets)
    {
        string id = raw.GetString("id")!;
        string lineageCode = raw.GetString("lineage")!;
        int lineage = lineageCodes.IndexOf(lineageCode);
        ProjectType type = ParseType(raw.GetString("type"));
        Theme theme = lineage >= 0 ? lineageTheme[lineage] : Theme.Keystone;
        if (type == ProjectType.Keystone) theme = Theme.Keystone;

        var requires = new List<int>();
        foreach (string r in raw.GetStringList("requires")) requires.Add(IndexOfId(r));
        var requiresAny = new List<int>();
        foreach (string r in raw.GetStringList("requires_any")) requiresAny.Add(IndexOfId(r));

        var gates = new List<Gate>();
        var targetList = new List<string>(gateTargets);
        foreach (ContentValue g in ListOf(raw, "gates"))
        {
            GateKind kind = g.GetString("kind") switch
            {
                "seen" => GateKind.Seen,
                "has" => GateKind.Has,
                "near" => GateKind.Near,
                "climate" => GateKind.Climate,
                var other => throw new InvalidDataException($"Unknown gate kind '{other}' on {id}."),
            };
            gates.Add(new Gate(kind, targetList.IndexOf(g.GetString("target")!)));
        }

        var effects = new List<EffectDef>();
        foreach (ContentValue e in ListOf(raw, "effects")) effects.Add(BuildEffect(e));

        var practice = new List<(string, int)>();
        foreach (ContentValue p in ListOf(raw, "practice")) practice.Add((p.GetString("action")!, (int)p.GetInt("share")!));

        int breadthEra = -1, breadthMin = 0, enters = -1;
        if (raw.Get("breadth") is { } b)
        {
            breadthEra = (int)b.GetInt("era")!;
            breadthMin = (int)b.GetInt("min")!;
        }
        if (raw.GetInt("enters") is { } en) enters = (int)en;

        return new ProjectDef
        {
            Index = index,
            Id = id,
            Name = raw.GetString("name") ?? id,
            Era = (int)raw.GetInt("era")!,
            Type = type,
            Theme = theme,
            Lineage = lineage,
            Cost = raw.GetFix("cost") ?? Fix64.Zero,
            Numeric = raw.GetString("status") == "numeric",
            Requires = requires.ToArray(),
            RequiresAny = requiresAny.ToArray(),
            Gates = gates.ToArray(),
            Effects = effects.ToArray(),
            Practice = practice.ToArray(),
            Moment = raw.GetString("moment") ?? "",
            Flavor = raw.GetString("flavor") ?? "",
            BreadthEra = breadthEra,
            BreadthMin = breadthMin,
            Enters = enters,
        };
    }

    private int IndexOfId(string id)
    {
        int i = IndexOf(id);
        if (i < 0) throw new InvalidDataException($"Unknown project id '{id}'.");
        return i;
    }

    private EffectDef BuildEffect(ContentValue e)
    {
        EffectKind kind = ParseEffectKind(e.GetString("kind"));
        Fix64? value = null;
        switch (kind)
        {
            case EffectKind.Mod: value = e.GetFix("value"); break;
            case EffectKind.Hazard: value = e.GetFix("mult"); break;
            case EffectKind.Adopt: value = e.GetFix("mult"); break;
            case EffectKind.Method: value = e.GetFix("mult") ?? e.GetFix("rate"); break;
            case EffectKind.Cap: value = e.GetFix("mult") ?? e.GetFix("value"); break;
            case EffectKind.Role: value = e.GetFix("insight"); break;
            case EffectKind.Cohesion: value = e.GetFix("value"); break;
        }
        return new EffectDef
        {
            Kind = kind,
            Stat = e.GetString("stat"),
            Op = e.GetString("op"),
            Value = value ?? Fix64.Zero,
            HasValue = value is not null,
            Take = e.GetFix("take") ?? Fix64.One,
            Cap = kind == EffectKind.Cap ? ParseCap(e.GetString("cap")) : null,
            Id = e.GetString("id") ?? e.GetString("structure"),
            Text = e.GetString("text") ?? e.GetString("tier"),
            Cause = e.GetString("cause"),
            Action = e.GetString("action"),
            Note = e.GetString("note"),
            Int = e.GetInt("era") ?? e.GetInt("rung") ?? 0,
        };
    }

    // ---- lookups --------------------------------------------------------------------------

    /// <summary>The dense index of a project id, or -1.</summary>
    public int IndexOf(string id)
    {
        int i = Array.BinarySearch(_ids, id, StringComparer.Ordinal);
        return i >= 0 ? i : -1;
    }

    public ProjectDef Get(string id) => Projects[IndexOfId(id)];

    public int LineageIndex(string code)
    {
        for (int i = 0; i < LineageCodes.Count; i++)
            if (string.Equals(LineageCodes[i], code, StringComparison.Ordinal)) return i;
        return -1;
    }

    public int PracticeIndex(string id)
    {
        for (int i = 0; i < PracticeIds.Count; i++)
            if (string.Equals(PracticeIds[i], id, StringComparison.Ordinal)) return i;
        return -1;
    }

    public int GateTargetIndex(string target)
    {
        for (int i = 0; i < GateTargets.Count; i++)
            if (string.Equals(GateTargets[i], target, StringComparison.Ordinal)) return i;
        return -1;
    }

    public int StatIndex(string stat)
    {
        for (int i = 0; i < StatNames.Count; i++)
            if (string.Equals(StatNames[i], stat, StringComparison.Ordinal)) return i;
        return -1;
    }

    public int RoleIndex(string id)
    {
        for (int i = 0; i < Roles.Count; i++)
            if (string.Equals(Roles[i].Id, id, StringComparison.Ordinal)) return i;
        return -1;
    }

    /// <summary>The keystone that leaves <paramref name="era"/>, or -1.</summary>
    public int KeystoneLeaving(int era) => era is >= 0 and < EraCount ? _keystoneByLeavingEra[era] : -1;

    /// <summary>
    /// Every project the keystone leaving <paramref name="era"/> needs, transitively (docs/MVP/07 F8, 02 §15): the
    /// keystone's named prerequisites and everything they require, with one-of prerequisites resolved to their
    /// cheapest alternative. The keystone itself is included.
    /// </summary>
    public bool[]? ClosureOf(int era) => era is >= 0 and < EraCount ? _closure[era] : null;

    private bool[] ComputeClosure(int keystone)
    {
        var inClosure = new bool[Projects.Count];
        var stack = new Stack<int>();
        stack.Push(keystone);
        while (stack.Count > 0)
        {
            int p = stack.Pop();
            if (inClosure[p]) continue;
            inClosure[p] = true;
            ProjectDef def = Projects[p];
            foreach (int r in def.Requires) stack.Push(r);
            if (def.RequiresAny.Length > 0)
            {
                int best = def.RequiresAny[0];
                foreach (int alt in def.RequiresAny)
                    if (inClosure[alt]) { best = alt; break; }
                    else if (Projects[alt].Cost < Projects[best].Cost) best = alt;
                stack.Push(best);
            }
        }
        return inClosure;
    }

    /// <summary>Know-how lineage split (percent) for a practice action at a civilization's era.</summary>
    public ReadOnlySpan<(int Lineage, int Percent)> PracticeSplit(int practiceIndex, int era)
        => era >= 2 && _practiceHasEra2[practiceIndex] ? _practiceLineagesFromEra2[practiceIndex] : _practiceLineages[practiceIndex];

    /// <summary>A named balance constant as Fix64; throws if it is missing.</summary>
    public Fix64 Constant(string id)
    {
        foreach (ContentValue c in _constants.Items)
            if (c.GetString("id") == id) return c.GetFix("value") ?? throw new InvalidDataException($"Constant {id} is not a number.");
        throw new InvalidDataException($"Missing balance constant {id}.");
    }
}
