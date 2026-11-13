using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.View;

/// <summary>Display names for the ids a <see cref="BuildOutlook"/> carries: structures and goods (registry ids) and projects (catalog ids).</summary>
public sealed record BuildNames(Func<string, string> Structure, Func<string, string> Good, Func<string, string> Project)
{
    /// <summary>Plain forms of the ids themselves, for tests and headless reports.</summary>
    public static readonly BuildNames Ids = new(s => s, g => g, p => p);
}

/// <summary>
/// The plain-English wording of the "what is this town waiting on" panel (docs/MVP/30). It is a pure function of a <see cref="BuildOutlook"/>, so the front end only
/// lays the sentences out, and the wording is tested headless.
/// </summary>
public static class BuildText
{
    /// <summary>A quantity for display: whole when it is within a twentieth of a whole number, else one decimal ("80", "2.5").</summary>
    private static string Num(Fix64 value)
    {
        int tenths = (value * 10).RoundToInt();
        if (tenths % 10 == 0) return (tenths / 10).ToString(CultureInfo.InvariantCulture);
        string sign = tenths < 0 ? "-" : "";
        int abs = Math.Abs(tenths);
        return $"{sign}{abs / 10}.{abs % 10}";
    }

    public static string Track(BuildTrack track) => track switch
    {
        BuildTrack.Iron => "Iron works", BuildTrack.Civic => "Civic buildings", _ => "Homes, stores and defenses",
    };

    /// <summary>The name of a structure from its focus code.</summary>
    public static string NameOfCode(int code, BuildNames names)
    {
        int kind = LivingEconomyModel.FocusCode.KindOf(code);
        return LivingEconomyModel.FocusCode.TrackOf(code) switch
        {
            BuildTrack.Iron => names.Structure(LivingEconomyModel.IronWorkId(kind)),
            BuildTrack.Civic => names.Structure(LivingEconomyModel.CivicStructureName(kind)),
            _ => names.Structure(LivingEconomyModel.StructureName(kind)),
        };
    }

    /// <summary>"wood 4, clay 2": what a structure takes.</summary>
    public static string Cost(BuildItem item, BuildNames names)
    {
        var sb = new StringBuilder();
        foreach (BuildNeed n in item.Needs)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(GoodName(n.Slot, names).ToLowerInvariant()).Append(' ').Append(Num(n.Need));
        }
        return sb.Length == 0 ? "no materials" : sb.ToString();
    }

    /// <summary>The good's name for a sentence: "Wood / fuel" in the Stores tab reads "Wood" here, where only its use as timber matters.</summary>
    public static string GoodName(int slot, BuildNames names)
    {
        if (slot < 0) return "Food";
        string name = names.Good(LivingEconomyModel.StoreSlotId(slot));
        int slash = name.IndexOf(" / ", StringComparison.Ordinal);
        return slash > 0 ? name[..slash] : name;
    }

    /// <summary>The headline for the town.</summary>
    public static string Headline(BuildOutlook o, BuildNames names)
    {
        BuildItem? shortCurrent = null;
        foreach (BuildItem i in o.Items) if (i.Status == BuildStatus.Current && i.AnyShort) { shortCurrent = i; break; }
        switch (o.Headline)
        {
            case BuildHeadline.Building:
                return "Building: the town has what it needs for what it is working on.";
            case BuildHeadline.WaitingOnMaterials:
                if (shortCurrent is not null)
                    return $"Waiting on materials for the {names.Structure(shortCurrent.Id).ToLowerInvariant()}.";
                return "Waiting on materials: the town wants to build, but cannot pay for anything it wants.";
            default:
                return "Nothing is wanted right now: everything the town would build is built, not needed, or ruled out here.";
        }
    }

    /// <summary>One line for an item in a list: the good short, or the rule that stops it.</summary>
    public static string Short(BuildItem item, BuildNames names)
    {
        switch (item.Status)
        {
            case BuildStatus.Current:
                return item.Labor > Fix64.Zero ? $"{Num(item.Progress)} of {Num(item.Labor)} work-seasons done" : "under way";
            case BuildStatus.Short:
                foreach (BuildNeed n in item.Needs)
                    if (n.Short) return $"{GoodName(n.Slot, names)} {Num(n.Have)} of {Num(n.Need)}";
                return "short of materials";
            case BuildStatus.Waiting: return "ready, waiting its turn";
            case BuildStatus.NotNeeded: return "not needed now";
            case BuildStatus.Done: return item.Gate.Why == BuildWhy.AtLimit ? "at its limit" : "built";
            default: return Why(item.Gate, names);
        }
    }

    /// <summary>The rule that stops a structure, as a sentence.</summary>
    public static string Why(BuildGate g, BuildNames names)
    {
        string project = g.Project is { Length: > 0 } p ? names.Project(p) : "";
        return g.Why switch
        {
            BuildWhy.NotLearned => project.Length > 0 ? $"Your people have not learned {project} yet." : "Your people have not learned how to build this yet.",
            BuildWhy.AlreadyBuilt => "One stands already.",
            BuildWhy.AtLimit => g.Arg > 0 ? $"The limit of {g.Arg} here is reached." : "The limit here is reached.",
            BuildWhy.FieldsFull => $"Every plot this town can work is laid ({g.Arg}).",
            BuildWhy.NeedsPopulation => $"The town needs {g.Arg} people first.",
            BuildWhy.NeedsTown => "Only a Town or a City can raise this; this settlement is still a village.",
            BuildWhy.NeedsCity => "Only a City can raise this.",
            BuildWhy.NeedsKiln => "It needs a kiln in the town first.",
            BuildWhy.NeedsWalls => "It needs stone walls around the town first.",
            BuildWhy.NeedsHarbor => "It needs a harbor first.",
            BuildWhy.NeedsRoad => "It needs the paved road to the capital first.",
            BuildWhy.NeedsTemple => "It needs a Temple in the town first.",
            BuildWhy.NeedsMarket => "It needs a Market in the town first.",
            BuildWhy.NeedsRoutes => $"The town needs {g.Arg} trade routes first.",
            BuildWhy.NeedsProject => project.Length > 0 ? $"It needs {project}, which your people have not learned." : "It needs a project your people have not learned.",
            BuildWhy.NeedsSoldiers => $"It needs {g.Arg} fed soldiers in post here.",
            BuildWhy.NeedsGraftedOrchard => "It needs an orchard here to press.",
            BuildWhy.NeedsFields => $"It needs {g.Arg} worked fields here.",
            BuildWhy.NoFreeGround => "There is no free ground within reach.",
            BuildWhy.NoRiver => "It needs a river (or one with fish to dam) within reach, and this site has none free.",
            BuildWhy.NoHillSite => "There is no high ground within four tiles to raise a hillfort on.",
            BuildWhy.ShrineSpacing => $"Another of your shrines stands within {g.Arg} tiles. Shrines must keep that apart.",
            BuildWhy.NoTerraceTile => "There is no free hillside within reach to terrace.",
            BuildWhy.NoRivalNear => $"No rival settlement lies within {g.Arg} tiles, so there is nothing to wall or fort against.",
            BuildWhy.NotCoast => "It needs a site on the sea coast.",
            BuildWhy.NotCapitalSite => "The Palace goes to the town the game picks for the capital, and this is not it.",
            BuildWhy.NoLandRoute => "There is no road route over land to the capital.",
            BuildWhy.IsCapital => "This is the capital; roads lead to it.",
            BuildWhy.NoRiverOnRoad => "The road to the capital crosses no river, so no bridge is needed.",
            BuildWhy.OnePerCivilization => "Your civilization may hold only one, and it is built (or being built) elsewhere.",
            BuildWhy.WonderHeldElsewhere => "Only one of each Wonder exists, and someone holds it.",
            BuildWhy.AcademyLimit => $"Your civilization keeps at most {g.Arg} academies.",
            BuildWhy.MarketsGoToLargest => "Markets go to your civilization's largest towns, and this is not among them.",
            BuildWhy.NotLargestTown => "A Wonder is raised in your civilization's largest town, and this is not it.",
            BuildWhy.NotMember => "This settlement does not belong to your realm.",
            BuildWhy.HousingEnough => "The town has room for its people now.",
            BuildWhy.PlannerOnlyOne => "The town raises only one of these by itself; further room comes from other homes.",
            BuildWhy.WaterNotLimiting => "Water is not what holds this town back.",
            BuildWhy.CraneNoLargeWork => "A crane pays off only with a large work (aqueduct or castle) to build.",
            BuildWhy.FlatFieldsRemain => "Flat ground is still free for fields, so terraces are not wanted yet.",
            BuildWhy.CloseToCapital => "The town is close to the capital already.",
            _ => "",
        };
    }

    /// <summary>Why a focus cannot be set on a structure.</summary>
    public static string FocusRefusal(BuildWhy why, BuildNames names) => why switch
    {
        BuildWhy.AlreadyFirstInLine => "The town already builds this before anything else on the list.",
        BuildWhy.WonderNotRedirectable => "Wonders are raised in the largest town and cannot be redirected.",
        BuildWhy.OnTheWayToCity => "The town is raising the Temple or Palace that makes it a City. That comes first.",
        BuildWhy.SkipNotOffered => "Only Iron works and civic buildings can be set aside.",
        BuildWhy.NotYourTown => "Not one of your towns.",
        BuildWhy.NoSuchStructure => "The town cannot build this.",
        BuildWhy.None => "",
        _ => Why(new BuildGate(BuildCause.None, why), names),
    };

    /// <summary>Where a short good would come from, as a sentence.</summary>
    public static string Supply(BuildNeed need, GoodSupply supply, BuildNames names)
    {
        string good = GoodName(need.Slot, names).ToLowerInvariant();
        switch (supply.Kind)
        {
            case SupplyKind.NeedsProject:
                return $"No one here can make {good} yet: your people have not learned {names.Project(supply.Project ?? "")}.";
            case SupplyKind.NoSource:
                string id = need.Slot >= 0 ? LivingEconomyModel.StoreSlotId(need.Slot) : "";
                return id switch
                {
                    "G-WOOD" => "There is no standing timber left within three tiles of this town.",
                    "G-STONE" => "There is no stone left within three tiles of this town, and stone does not grow back.",
                    "G-CLAY" or "G-BRICK" => $"There is no clay deposit within three tiles. Clay can be dug from the ground anywhere once your civilization reaches the Bronze Age ({names.Project(supply.Project ?? "KEY-3")}).",
                    "G-BLOCK" or "G-ASHLAR" => $"There is no stone left within {supply.Reach} tiles that no rival holds.",
                    _ => $"There is none to be had within {supply.Reach} tiles of this town.",
                };
            case SupplyKind.NeedsOutside:
                return "No iron ore is within this town's reach, so the bloom it needs can come only from a mine elsewhere or from trade.";
            default:
                return $"The town is already gathering {good}, but nothing you can do speeds it directly.";
        }
    }

    /// <summary>What the player can do, as a sentence.</summary>
    public static string Lever(BuildLever lever, BuildNames names) => lever.Kind switch
    {
        LeverKind.Research => $"Learn {names.Project(lever.Project ?? "")}.",
        LeverKind.FocusThis => "Ask the town to build this next: its material workers then cut timber for it first.",
        LeverKind.FocusPrerequisite => $"Ask the town to build the {NameOfCode(lever.Arg, names).ToLowerInvariant()} first.",
        LeverKind.Corvee => "A Corvée decree here: builders work half again as fast for two seasons (costs Mandate).",
        LeverKind.Compact => "Buy iron from a neighbour by compact (a Compact decree).",
        LeverKind.FoundNearby => "Found a village within reach of a deposit (Found a village decree).",
        LeverKind.OpenMine => "Open a mine on iron ore (Open a mine decree).",
        LeverKind.SkipThis => "Set it aside for now: the slot goes to the next work the town wants.",
        _ => "",
    };

    /// <summary>
    /// The honest answer to "can I do anything about this?": a lever that works, nothing but time, or nothing at all, with the exact reason.
    /// </summary>
    public static string Verdict(BuildItem item, BuildNames names)
    {
        switch (item.Verdict)
        {
            case BuildVerdict.Resolvable: return "You can fix this:";
            case BuildVerdict.Time: return "Nothing you can order speeds this up; it is a matter of seasons.";
            case BuildVerdict.Unresolvable:
                if (item.Status == BuildStatus.Short)
                    for (int i = 0; i < item.Needs.Length; i++)
                        if (item.Needs[i].Short && item.Supplies[i].Kind == SupplyKind.NoSource)
                            return Supply(item.Needs[i], item.Supplies[i], names) + " Nothing you can order changes that here.";
                return Why(item.Gate, names) + " Nothing you can order changes that here.";
            default: return "";
        }
    }

    /// <summary>The verdict when the cause has already been given above it: just the conclusion.</summary>
    public static string VerdictAfterCause(BuildItem item, BuildNames names)
        => item.Verdict == BuildVerdict.Unresolvable ? "Nothing you can order changes that here." : Verdict(item, names);

    /// <summary>The whole explanation of a stopped item in plain sentences, one per line (the tooltip and the headless report).</summary>
    public static IReadOnlyList<string> Explain(BuildItem item, BuildNames names)
    {
        var lines = new List<string>();
        string name = names.Structure(item.Id);
        switch (item.Status)
        {
            case BuildStatus.Current:
                lines.Add($"{name}: {Short(item, names)}.");
                break;
            case BuildStatus.Short:
                lines.Add($"{name}: wanted, but the town is short of {ShortGoods(item, names)}.");
                break;
            case BuildStatus.Waiting:
                lines.Add($"{name}: the town can pay for it, and it waits its turn.");
                break;
            case BuildStatus.NotNeeded:
                lines.Add($"{name}: the town sees no need for it. {Why(item.Gate, names)}".TrimEnd());
                break;
            case BuildStatus.Blocked:
                lines.Add($"{name}: {Why(item.Gate, names)}");
                break;
            default:
                lines.Add($"{name}: {Short(item, names)}.");
                break;
        }
        for (int i = 0; i < item.Needs.Length; i++)
            if (item.Needs[i].Short && item.Status is BuildStatus.Short or BuildStatus.Current)
                lines.Add(Supply(item.Needs[i], item.Supplies[i], names));
        string verdict = item.Status == BuildStatus.Blocked ? Verdict(item, names) : VerdictAfterCause(item, names);
        if (verdict.Length > 0) lines.Add(verdict);
        foreach (BuildLever lever in item.Levers) lines.Add(Lever(lever, names));
        return lines;
    }

    private static string ShortGoods(BuildItem item, BuildNames names)
    {
        var parts = new List<string>();
        foreach (BuildNeed n in item.Needs)
            if (n.Short) parts.Add($"{GoodName(n.Slot, names).ToLowerInvariant()} ({Num(n.Have)} of {Num(n.Need)})");
        return string.Join(" and ", parts);
    }

    /// <summary>A structure your people have learned that no town of yours has built, with what holds it back across the towns.</summary>
    public sealed record NotBuilt(BuildTrack Track, string Id, int Towns, int ShortTowns, int BlockedTowns, int WaitingTowns, int NotNeededTowns, string Reason);

    /// <summary>
    /// The learned structures that stand in none of the given towns (docs/MVP/30 §5: "researched but never seen"), most widely held back first, each with the
    /// commonest reason in plain words. Towns without an outlook are skipped; foundation structures the town always builds first are included like any other.
    /// </summary>
    public static IReadOnlyList<NotBuilt> NotBuiltAnywhere(IEnumerable<BuildOutlook?> towns, BuildNames names)
    {
        // Items are grouped by (track, id) in first-seen order; the towns arrive in ascending settlement order, so the grouping is deterministic.
        var keys = new List<(BuildTrack Track, string Id)>();
        var groups = new List<List<BuildItem>>();
        foreach (BuildOutlook? town in towns)
        {
            if (town is null) continue;
            foreach (BuildItem item in town.Items)
            {
                int at = keys.FindIndex(k => k.Track == item.Track && k.Id == item.Id);
                if (at < 0) { keys.Add((item.Track, item.Id)); groups.Add(new List<BuildItem>()); at = keys.Count - 1; }
                groups[at].Add(item);
            }
        }
        var result = new List<NotBuilt>();
        for (int g = 0; g < keys.Count; g++)
        {
            (BuildTrack track, string id) = keys[g];
            List<BuildItem> items = groups[g];
            bool built = false;
            int shorts = 0, blocked = 0, waiting = 0, notNeeded = 0;
            var shortGoods = new List<(string Key, int Count)>();
            var blockWhy = new List<(string Key, int Count)>();
            foreach (BuildItem item in items)
            {
                if (item.BuiltCount > 0) built = true;
                switch (item.Status)
                {
                    case BuildStatus.Short or BuildStatus.Current when item.AnyShort:
                        shorts++;
                        foreach (BuildNeed n in item.Needs)
                            if (n.Short) { Bump(shortGoods, GoodName(n.Slot, names).ToLowerInvariant()); break; }
                        break;
                    case BuildStatus.Blocked:
                        blocked++;
                        Bump(blockWhy, Why(item.Gate, names));
                        break;
                    case BuildStatus.Waiting: waiting++; break;
                    case BuildStatus.NotNeeded: notNeeded++; break;
                }
            }
            if (built) continue;
            var parts = new List<string>();
            if (shorts > 0) parts.Add($"short of {Top(shortGoods)} in {shorts} {(shorts == 1 ? "town" : "towns")}");
            if (waiting > 0) parts.Add($"affordable but waiting its turn in {waiting}");
            if (blocked > 0) parts.Add($"ruled out in {blocked}: {Top(blockWhy).TrimEnd('.')}");
            if (notNeeded > 0 && parts.Count == 0) parts.Add($"no town needs one yet");
            result.Add(new NotBuilt(track, id, items.Count, shorts, blocked, waiting, notNeeded, string.Join("; ", parts)));
        }
        result.Sort((a, b) =>
        {
            int c = (b.ShortTowns + b.BlockedTowns + b.WaitingTowns).CompareTo(a.ShortTowns + a.BlockedTowns + a.WaitingTowns);
            return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
        });
        return result;
    }

    private static void Bump(List<(string Key, int Count)> counts, string key)
    {
        int at = counts.FindIndex(c => c.Key == key);
        if (at < 0) counts.Add((key, 1)); else counts[at] = (key, counts[at].Count + 1);
    }

    /// <summary>The commonest key; ties go to the one seen first.</summary>
    private static string Top(List<(string Key, int Count)> counts)
    {
        string best = ""; int most = 0;
        foreach ((string k, int v) in counts) if (v > most) { best = k; most = v; }
        return best;
    }
}
