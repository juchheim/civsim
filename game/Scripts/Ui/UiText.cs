using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CivSim.Sim.Content;
using CivSim.Sim.Research;

namespace CivSim.Game.Ui;

/// <summary>Plain-English names and phrasing for everything the HUD says (docs/MVP/20 §5). IDs never appear here.</summary>
public static class UiText
{
    public static readonly string[] CivEmblemNames = { "Sun", "Wave", "Leaf", "Star", "Mountain", "Flame", "Moon", "Antler" };
    public static readonly string[] SlotNames = { "Primary", "Secondary", "Tertiary" };
    public static readonly string[] SlotShare = { "4 of 7", "2 of 7", "1 of 7" };
    public static readonly int[] SlotWeight = { 4, 2, 1 };
    public static readonly string[] ThemeNames = { "Craft", "Kin", "Reach", "Turning point" };
    public static readonly string[] ThemePurpose =
    {
        "Tools, fire, materials and building", "Food, family, belief and health", "Travel, contact, claims and knowledge", "Moves your people into a new age",
    };
    public static readonly string[] TypeNames = { "Discovery", "Method", "Structure", "Role", "Institution", "Wonder", "Turning point" };
    public static readonly string[] SeasonNames = { "Spring", "Summer", "Autumn", "Winter" };

    private static string[] _ageNames = { "Dawn", "Hunters", "Roots", "Bronze", "Iron", "Faith & Fief", "Discovery", "Industry", "Modern", "Information" };
    private static readonly Dictionary<string, string> FieldFull = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> GoodNames = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> StructureNames = new(StringComparer.Ordinal);
    private static string?[][] _methodNames = Array.Empty<string?[]>();

    private static readonly Dictionary<string, string> FieldShort = new(StringComparer.Ordinal)
    {
        ["BLD"] = "Building", ["CER"] = "Ceramics", ["CHM"] = "Chemistry", ["FIB"] = "Fiber & Hide", ["FIR"] = "Fire", ["MEC"] = "Machines",
        ["MET"] = "Metallurgy", ["MIN"] = "Mining", ["NRG"] = "Energy", ["STN"] = "Stoneworking", ["WOD"] = "Woodworking",
        ["AGR"] = "Agriculture", ["ART"] = "Arts", ["BEL"] = "Belief", ["FOD"] = "Food Storage", ["FOR"] = "Foraging", ["GOV"] = "Governance",
        ["HNT"] = "Hunting", ["HUS"] = "Herding", ["LRN"] = "Learning", ["MED"] = "Medicine", ["SOC"] = "Kinship",
        ["COM"] = "Language", ["DEF"] = "Defense", ["DIP"] = "Diplomacy", ["INQ"] = "Science", ["NUM"] = "Numbers", ["SEA"] = "Seafaring",
        ["TER"] = "Land Claims", ["TRD"] = "Trade", ["TRN"] = "Transport", ["WAR"] = "Warfare", ["WAY"] = "Wayfinding",
    };

    public static void Init(ContentPack pack, ResearchCatalog catalog)
    {
        try
        {
            ContentValue? eras = pack.Root.Get("registries")?.Get("eras");
            if (eras is { Kind: ValueKind.List })
            {
                var names = new string[eras.Items.Count];
                for (int i = 0; i < names.Length; i++) names[i] = eras.Items[i].GetString("name") ?? _ageNames[Math.Min(i, _ageNames.Length - 1)];
                _ageNames = names;
            }
            foreach (ContentValue lineage in pack.List("lineages"))
                if (lineage.GetString("code") is { } code && lineage.GetString("name") is { } name) FieldFull[code] = name;

            ContentValue? registries = pack.Root.Get("registries");
            LoadNames(registries?.Get("goods"), GoodNames);
            LoadNames(registries?.Get("structures"), StructureNames);

            IReadOnlyList<ContentValue> raw = pack.List("projects");
            _methodNames = new string?[raw.Count][];
            for (int p = 0; p < raw.Count; p++)
            {
                ContentValue? effects = raw[p].Get("effects");
                if (effects is not { Kind: ValueKind.List }) continue;
                var row = new string?[effects.Items.Count];
                for (int e = 0; e < row.Length; e++) row[e] = effects.Items[e].GetString("name");
                _methodNames[p] = row;
            }
        }
        catch (Exception ex) { Godot.GD.PushWarning($"UiText init fell back to defaults: {ex.Message}"); }
    }

    private static void LoadNames(ContentValue? records, Dictionary<string, string> into)
    {
        into.Clear();
        if (records is not { Kind: ValueKind.List }) return;
        foreach (ContentValue record in records.Items)
            if (record.GetString("id") is { } id && record.GetString("name") is { } name)
                into[id] = name;
    }

    /// <summary>Player-facing name of a goods registry id (G-WOOD), falling back to the id's own words.</summary>
    public static string GoodName(string id)
        => GoodNames.TryGetValue(id, out string? name) ? name : Sentence(id.StartsWith("G-", StringComparison.Ordinal) ? id[2..].Replace('_', ' ').ToLowerInvariant() : id);

    /// <summary>Player-facing name of a structure registry id.</summary>
    public static string StructureName(string id)
        => StructureNames.TryGetValue(id, out string? name) ? name : Sentence(id.Replace('_', ' ').ToLowerInvariant());

    public static string AgeName(int era) => (uint)era < (uint)_ageNames.Length ? _ageNames[era] : $"Age {era + 1}";

    public static string CivName(int civ) => (uint)civ < (uint)CivEmblemNames.Length ? CivEmblemNames[civ] + " Folk" : "A distant people";

    public static string FieldName(ResearchCatalog catalog, int lineage, bool shortName)
    {
        if (lineage < 0 || lineage >= catalog.LineageCodes.Count) return "Turning points";
        string code = catalog.LineageCodes[lineage];
        if (shortName && FieldShort.TryGetValue(code, out string? s)) return s;
        return FieldFull.TryGetValue(code, out string? f) ? f : code;
    }

    public static string FieldFullName(ResearchCatalog catalog, int lineage) => FieldName(catalog, lineage, false);

    public static string Percent(double value) => $"{Math.Round(value):0}%";

    public static string FormatInsight(long raw)
    {
        double v = raw / 4294967296.0;
        return v >= 1000 ? $"{v / 1000.0:0.#}k" : v >= 10 ? $"{v:0}" : $"{v:0.#}";
    }

    public static string Minutes(double seconds)
    {
        if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return "—";
        int total = (int)Math.Round(seconds);
        if (total < 600) return $"{total / 60}:{total % 60:00}";
        return $"{Math.Round(total / 60.0)} min";
    }

    private static readonly Regex DesignRef = new(@"\s*[,;(]?\s*\d{1,2}[.\s]*§\s*[\d.]+\)?", RegexOptions.Compiled);

    /// <summary>Content notes sometimes cite design documents ("fire can die, 10.§9"); players should never see those.</summary>
    public static string Clean(string? text) => text is null ? "" : DesignRef.Replace(text, "").Trim().TrimEnd(',', ';');

    public static string Sentence(string text)
        => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string Humanize(string? stat)
    {
        if (string.IsNullOrEmpty(stat)) return "Something";
        string s = stat.Replace('.', ' ').Replace('_', ' ').Trim();
        return Sentence(s);
    }

    private static string ActionWord(string? action) => action switch
    {
        "forage" => "Foraging", "hunt" => "Hunting", "fish" => "Fishing", "farm" => "Farming", "chop" => "Chopping", "gather" => "Gathering",
        "craft" => "Crafting", "build" => "Building", "preserve" => "Preserving food", "tend_fire" => "Tending fire", "herd" => "Herding",
        "knap" => "Toolmaking", null or "" => "Work", _ => Sentence(action.Replace('_', ' ')),
    };

    private static string CauseWord(string? cause) => cause switch
    {
        "endemic" => "endemic illness", "epidemic" => "epidemics", "starvation" => "hunger", "exposure" => "cold", "injury" => "injuries",
        "predators" => "predators", "violence" => "violence", "child" => "childhood illness", "age0" => "infant deaths", "maternal" => "childbirth",
        _ => cause ?? "danger",
    };

    /// <summary>One plain line for an effect, or null when it has nothing worth saying.</summary>
    public static string? DescribeEffect(ProjectDef def, int effectIndex)
    {
        EffectDef e = def.Effects[effectIndex];
        double value = e.HasValue ? e.Value.ToDoubleForDisplay() : 0;
        string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        switch (e.Kind)
        {
            case EffectKind.Method:
                {
                    string? name = def.Index < _methodNames.Length && _methodNames[def.Index] is { } names && effectIndex < names.Length ? names[effectIndex] : null;
                    return name is null ? $"{ActionWord(e.Action)}: better tools ×{F(value)}" : $"{ActionWord(e.Action)}: {name.ToLowerInvariant()} ×{F(value)}";
                }
            case EffectKind.Mod:
                {
                    string label = Humanize(e.Stat ?? e.Id ?? e.Action);
                    return e.Op switch
                    {
                        "pct" => $"{label} {(value >= 0 ? "+" : "−")}{F(Math.Abs(value) * 100)}%",
                        "mult" => $"{label} ×{F(value)}",
                        "flat" => $"{label} {(value >= 0 ? "+" : "−")}{F(Math.Abs(value))}",
                        "set" => $"{label} set to {F(value)}",
                        _ => Clean(e.Note),
                    };
                }
            case EffectKind.Hazard:
                {
                    string cause = CauseWord(e.Cause ?? e.Id);
                    return value < 1 ? $"Fewer deaths from {cause} (−{F((1 - value) * 100)}%)" : $"More deaths from {cause} (+{F((value - 1) * 100)}%)";
                }
            case EffectKind.Cap:
                return e.Cap switch
                {
                    CapKind.Food => "The land feeds more people",
                    CapKind.Lean => "Lean seasons hurt less",
                    CapKind.Housing => "More room in each settlement",
                    CapKind.Group => "Groups can grow larger before splitting",
                    _ => e.Note ?? "Raises a limit",
                };
            case EffectKind.Cohesion:
                return "People hold together better";
            case EffectKind.Adopt:
                return $"Skill spread speed ×{F(value)}";
            case EffectKind.KnowHow:
                {
                    string knowHowText = e.Text?.Trim() ?? "";
                    int comma = knowHowText.IndexOf(',');
                    if (comma > 0)
                    {
                        string code = knowHowText[..comma].Trim();
                        string lineage = FieldShort.TryGetValue(code, out string? shortName) ? shortName : FieldFull.GetValueOrDefault(code, code);
                        return $"{lineage} know-how {knowHowText[(comma + 1)..].Trim()}";
                    }
                    return knowHowText.Length > 0 ? $"Know-how from practice {knowHowText}" : "More know-how from practice";
                }
            case EffectKind.Tier:
                return e.Text is { Length: > 0 } tier ? $"Settlement tier: {Sentence(tier)}" : null;
            case EffectKind.UnlockAction:
                return $"People start to {(e.Action ?? e.Id ?? "do something new").Replace('_', ' ')}";
            case EffectKind.Good:
                {
                    string? name = e.Id is not null && GoodNames.TryGetValue(e.Id, out string? goodName) ? goodName : null;
                    string? detail = e.Note is { Length: > 0 } goodNote ? Clean(goodNote) : null;
                    if (name is not null) return detail is null ? $"Unlocks {name}" : $"Unlocks {name} — {detail}";
                    return detail is not null ? $"Unlocks {detail}" : null;
                }
            case EffectKind.Build:
            case EffectKind.Wonder:
                {
                    string? name = e.Id is not null && StructureNames.TryGetValue(e.Id, out string? structureName) ? structureName : null;
                    string? detail = e.Note is { Length: > 0 } buildNote ? Clean(buildNote) : null;
                    if (name is not null) return detail is null ? $"Can build: {name}" : $"Can build: {name} — {detail}";
                    return detail is not null ? $"Can build: {detail}" : "Unlocks a new building";
                }
            case EffectKind.Role:
                return $"A new role: {Humanize(e.Id ?? e.Text)}";
            case EffectKind.Risk:
                return e.Text is { Length: > 0 } risk ? $"Risk: {Clean(risk)}" : "Comes with a risk";
            case EffectKind.Unlock:
            case EffectKind.Reveal:
            case EffectKind.Show:
            case EffectKind.Reach:
            case EffectKind.Lever:
            case EffectKind.Claim:
                return e.Text is { Length: > 0 } text ? Sentence(Clean(text)) : e.Note is { Length: > 0 } note ? Sentence(Clean(note)) : null;
            case EffectKind.Prose:
                return null;
            default:
                return e.Note is { Length: > 0 } fallback ? Sentence(Clean(fallback)) : e.Text is { Length: > 0 } t2 ? Sentence(Clean(t2)) : null;
        }
    }

    public static List<string> DescribeEffects(ProjectDef def, int max)
    {
        var lines = new List<string>(max);
        for (int i = 0; i < def.Effects.Length && lines.Count < max; i++)
        {
            string? line = DescribeEffect(def, i);
            if (!string.IsNullOrEmpty(line)) lines.Add(line);
        }
        return lines;
    }

    public static string FirstChangeLine(ProjectDef def)
    {
        for (int i = 0; i < def.Effects.Length; i++)
        {
            string? line = DescribeEffect(def, i);
            if (!string.IsNullOrEmpty(line)) return line;
        }
        return def.Flavor.Length > 0 ? def.Flavor : "A new way of living";
    }

    public static string GateText(ResearchCatalog catalog, Gate gate)
    {
        string target = (uint)gate.Target < (uint)catalog.GateTargets.Count ? catalog.GateTargets[gate.Target].Replace('_', ' ') : "something";
        return gate.Kind switch
        {
            GateKind.Seen => $"Your people have seen {target}",
            GateKind.Has => $"Needs {target} at a settlement",
            GateKind.Near => $"Needs to live near {target}",
            GateKind.Climate => $"Needs a {target} climate",
            _ => $"Needs {target}",
        };
    }

    public static string CouncilReason(int reason) => reason switch
    {
        0 => "it's quick",
        1 => "it fits your leaning",
        2 => "it leads to the next age",
        3 => "food is short",
        4 => "danger is near",
        5 => "sickness is spreading",
        6 => "your people are drifting apart",
        7 => "your people already practise it",
        8 => "neighbours already know it",
        _ => "it looked wise",
    };

    public static string OrderNoun(int command) => command switch
    {
        (int)CivSim.Sim.Core.CommandKind.SetSlot => "Your research choice",
        (int)CivSim.Sim.Core.CommandKind.QueueProject => "Queueing",
        (int)CivSim.Sim.Core.CommandKind.SendGift => "Your gift",
        (int)CivSim.Sim.Core.CommandKind.SendBarter => "Your barter trip",
        (int)CivSim.Sim.Core.CommandKind.LaunchRaid or (int)CivSim.Sim.Core.CommandKind.LaunchRaidAgainst => "Your raid",
        (int)CivSim.Sim.Core.CommandKind.LaunchAssault => "Your assault",
        (int)CivSim.Sim.Core.CommandKind.DeclareWar => "War",
        (int)CivSim.Sim.Core.CommandKind.MakePeace => "Peace",
        (int)CivSim.Sim.Core.CommandKind.FormAlliance => "The alliance",
        (int)CivSim.Sim.Core.CommandKind.RecallParty => "The recall",
        (int)CivSim.Sim.Core.CommandKind.PlaceBeacon => "The Beacon",
        _ => "Your order",
    };

    public static string RefusalReason(int reason) => reason switch
    {
        1 => "that target isn't valid any more",
        2 => "something it needs is missing",
        3 => "it isn't possible right now",
        _ => "it was refused",
    };
}
