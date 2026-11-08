using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// A player's build focus (docs/MVP/30; CommandKind.SetBuildFocus). One settlement may carry one entry: either "build this next" (prefer) or "leave this
/// alone for now" (skip). The planners read it and nothing else changes: it holds no materials and no labor, it never goes ahead of shelter, warmth,
/// food storage or the first fields, and it lapses when the structure stands, after <see cref="BuildFocusSeasons"/> seasons, or when the settlement
/// changes hands. The list is empty until a player issues the command, is hashed only while it is not, and the AI never issues it.
/// </summary>
public sealed partial class LivingEconomyModel
{
    private static readonly ulong BuildFocusTag = 0x4255494C44464F43UL; // "BUILDFOC"

    /// <summary>Seasons a focus or a skip lasts when nothing ends it first (6 years).</summary>
    public const int BuildFocusSeasons = 24;

    /// <summary>How a code names a structure: a basic structure by its index, an Iron work by 64 + kind, a civic structure by 128 + kind.</summary>
    public static class FocusCode
    {
        public const int None = -1, IronBase = 64, CivicBase = 128;
        public static int Basic(int structureKind) => structureKind;
        public static int Iron(int ironKind) => IronBase + ironKind;
        public static int Civic(int civicKind) => CivicBase + civicKind;
        public static BuildTrack TrackOf(int code) => code >= CivicBase ? BuildTrack.Civic : code >= IronBase ? BuildTrack.Iron : BuildTrack.Basic;
        public static int KindOf(int code) => code >= CivicBase ? code - CivicBase : code >= IronBase ? code - IronBase : code;
    }

    /// <summary>The state kept for one settlement; the list is sorted by settlement index.</summary>
    private sealed class BuildFocusEntry
    {
        public int Settlement, Civilization, Code, Since, Baseline;
        public bool Skip;
    }

    private readonly List<BuildFocusEntry> _buildFocus = new();

    private int FocusIndex(int settlement)
    {
        int low = 0, high = _buildFocus.Count - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            int v = _buildFocus[mid].Settlement;
            if (v == settlement) return mid;
            if (v < settlement) low = mid + 1; else high = mid - 1;
        }
        return ~low;
    }

    private BuildFocusEntry? FocusOf(SettlementState s)
    {
        if (_buildFocus.Count == 0) return null;
        int at = FocusIndex(s.Id);
        return at >= 0 && _buildFocus[at].Civilization == s.Civilization ? _buildFocus[at] : null;
    }

    /// <summary>The code the player chose for a settlement (<see cref="FocusCode.None"/> when there is none) and whether it is a skip.</summary>
    public int BuildFocusAt(int settlementIndex, out bool skip)
    {
        skip = false;
        if (unchecked((uint)settlementIndex) >= unchecked((uint)_settlements.Count)) return FocusCode.None;
        BuildFocusEntry? e = FocusOf(_settlements[settlementIndex]);
        if (e is null) return FocusCode.None;
        skip = e.Skip;
        return e.Code;
    }

    /// <summary>The season the settlement's focus was set and the season it lapses (both -1 without one).</summary>
    public (int Since, int Until) BuildFocusWindow(int settlementIndex)
    {
        if (unchecked((uint)settlementIndex) >= unchecked((uint)_settlements.Count) || FocusOf(_settlements[settlementIndex]) is not { } e) return (-1, -1);
        return (e.Since, e.Since + BuildFocusSeasons);
    }

    private bool BuildFocusPrefers(SettlementState s, BuildTrack track, out int kind)
    {
        kind = -1;
        if (_buildFocus.Count == 0 || FocusOf(s) is not { Skip: false } e || FocusCode.TrackOf(e.Code) != track) return false;
        kind = FocusCode.KindOf(e.Code);
        return true;
    }

    private bool BuildFocusSkips(SettlementState s, BuildTrack track, int kind)
        => _buildFocus.Count != 0 && FocusOf(s) is { Skip: true } e && FocusCode.TrackOf(e.Code) == track && FocusCode.KindOf(e.Code) == kind;

    /// <summary>The basic structure the player asked for at this settlement, or <c>Structure.Count</c>.</summary>
    private Structure BasicFocusKind(SettlementState s)
        => BuildFocusPrefers(s, BuildTrack.Basic, out int kind) ? (Structure)kind : Structure.Count;

    /// <summary>The focused structure is learned, allowed here and its materials are in store: the planner takes it ahead of everything after the foundation rules.</summary>
    private bool BasicFocusReady(SettlementState s, Structure kind, int season)
        => CanBuild(s.Civilization, kind) && BasicGate(s, kind).Cause == BuildCause.None && BasicNeedsMet(s, kind, season);

    /// <summary>Litres of wood a focused basic structure still lacks, which raises the settlement's wood target so woodcutters work for it (0 without a focus).</summary>
    private Fix64 BuildFocusWoodNeed(SettlementState s, int season)
    {
        if (_buildFocus.Count == 0 || BasicFocusKind(s) is var kind && kind == Structure.Count) return Fix64.Zero;
        if (!CanBuild(s.Civilization, kind) || BasicGate(s, kind).Cause != BuildCause.None) return Fix64.Zero;
        int n = BasicNeeds(s, kind, season, out BuildNeed a, out BuildNeed b);
        if (n >= 1 && a.Slot == (int)Good.Wood) return a.Need;
        return n >= 2 && b.Slot == (int)Good.Wood ? b.Need : Fix64.Zero;
    }

    // ---- eligibility -------------------------------------------------------------------------------------

    /// <summary>
    /// Whether the settlement may be asked to build (or skip) the structure: learned, site and size rules met, not at its cap, not a Wonder, and, for a prefer
    /// order on the basic list, not one of the foundation structures the planner already puts first. The town's own appetite (it does not need the house yet)
    /// does not count against it; that is the player's call.
    /// </summary>
    public bool CanFocusBuild(int settlementIndex, int code, bool skip, out BuildWhy why)
    {
        why = BuildWhy.None;
        if (unchecked((uint)settlementIndex) >= unchecked((uint)_settlements.Count)) { why = BuildWhy.NotYourTown; return false; }
        SettlementState s = _settlements[settlementIndex];
        if (IsFreeFolk(s) || Population(s.Id) == 0) { why = BuildWhy.NotYourTown; return false; }
        BuildTrack track = FocusCode.TrackOf(code);
        int kind = FocusCode.KindOf(code);
        switch (track)
        {
            case BuildTrack.Basic:
            {
                if (skip) { why = BuildWhy.SkipNotOffered; return false; }
                if ((uint)kind >= (uint)Structure.Count || !BasicInList((Structure)kind)) { why = BuildWhy.NoSuchStructure; return false; }
                var structure = (Structure)kind;
                if (IsFoundationStructure(structure)) { why = BuildWhy.AlreadyFirstInLine; return false; }
                BuildGate gate = BasicGate(s, structure);
                why = gate.Why;
                return gate.Cause == BuildCause.None;
            }
            case BuildTrack.Iron:
            {
                if (!IronChain || s.Metal is null || (uint)kind >= (uint)IronStructure.Count) { why = BuildWhy.NoSuchStructure; return false; }
                if (IsWonder((IronStructure)kind)) { why = BuildWhy.WonderNotRedirectable; return false; }
                if (!Done(s.Civilization, IronSpecs[kind].Unlock)) { why = BuildWhy.NotLearned; return false; }
                BuildGate gate = IronGate(s, (IronStructure)kind, includeAppetite: false);
                why = gate.Why;
                return gate.Cause == BuildCause.None;
            }
            case BuildTrack.Civic:
            {
                if (s.Civic is not { } c || !CohesionLive(s) || kind <= (int)CivicBuild.None || kind > (int)CivicBuild.Ziggurat) { why = BuildWhy.NoSuchStructure; return false; }
                var build = (CivicBuild)kind;
                if (build == CivicBuild.Ziggurat) { why = BuildWhy.WonderNotRedirectable; return false; }
                BuildGate gate = CivicGate(s, c, build, includeAppetite: false);
                if (gate.Cause != BuildCause.None) { why = gate.Why; return false; }
                // The Temple or Palace that makes the town a City comes first and cannot be set aside.
                CivicBuild enabler = CityEnabler(s, c);
                if (enabler != CivicBuild.None && (skip ? enabler == build : enabler != build)) { why = BuildWhy.OnTheWayToCity; return false; }
                return true;
            }
            default: return false;
        }
    }

    private static bool IsFoundationStructure(Structure k)
        => k is Structure.StoragePit or Structure.DryingRack or Structure.Granary or Structure.LeanTo or Structure.Hearth or Structure.Field;

    private static bool BasicInList(Structure k)
    {
        for (int r = 0; r < (int)BasicRule.Count; r++) if (BasicRuleKind(r) == k) return true;
        return false;
    }

    private int FocusBuiltCount(SettlementState s, int code)
    {
        int kind = FocusCode.KindOf(code);
        switch (FocusCode.TrackOf(code))
        {
            case BuildTrack.Iron: return IronWorksCount(s, (IronStructure)kind);
            case BuildTrack.Civic: return s.Civic is { } c ? Count(c, (CivicBuild)kind) : 0;
            default:
                return (Structure)kind == Structure.Field ? s.FieldTiles.Count
                    : (Structure)kind == Structure.Orchard ? s.OrchardPlots.Count : s.Structures[kind];
        }
    }

    // ---- applying the command ----------------------------------------------------------------------------

    /// <summary>
    /// <c>SetBuildFocus</c>: A = own settlement index, B = a <see cref="FocusCode"/> or -1 to clear, C = 1 to skip that structure instead of preferring it.
    /// Refused (nothing changes) for another civilization's settlement or a structure the settlement cannot be asked for.
    /// </summary>
    private void ApplyBuildFocus(Command command)
    {
        int index = command.A;
        if (unchecked((uint)index) >= unchecked((uint)_settlements.Count) || _settlements[index].Civilization != command.Civ) return;
        SettlementState s = _settlements[index];
        int at = FocusIndex(index);
        if (command.B < 0)
        {
            if (at >= 0) _buildFocus.RemoveAt(at);
            return;
        }
        bool skip = command.C == 1;
        if (!CanFocusBuild(index, command.B, skip, out _)) return;
        var entry = new BuildFocusEntry
        {
            Settlement = index, Civilization = s.Civilization, Code = command.B, Since = checked((int)_seasonsCompleted),
            Baseline = FocusBuiltCount(s, command.B), Skip = skip,
        };
        if (at >= 0) _buildFocus[at] = entry; else _buildFocus.Insert(~at, entry);
    }

    /// <summary>Once a season, before the settlement plans its work: ends a focus that has been built, has run its time out or belongs to a settlement that changed hands.</summary>
    private void AdvanceBuildFocus(SettlementState s, int season)
    {
        if (_buildFocus.Count == 0) return;
        int at = FocusIndex(s.Id);
        if (at < 0) return;
        BuildFocusEntry e = _buildFocus[at];
        bool done = e.Civilization != s.Civilization || IsFreeFolk(s) || season - e.Since >= BuildFocusSeasons ||
            (!e.Skip && FocusBuiltCount(s, e.Code) > e.Baseline);
        if (done) _buildFocus.RemoveAt(at);
    }

    // ---- state -------------------------------------------------------------------------------------------

    private void HashBuildFocus(ref StateHasher hash)
    {
        if (_buildFocus.Count == 0) return;
        hash.Add(BuildFocusTag);
        hash.Add(_buildFocus.Count);
        foreach (BuildFocusEntry e in _buildFocus)
        {
            hash.Add(e.Settlement); hash.Add(e.Civilization); hash.Add(e.Code); hash.Add(e.Since); hash.Add(e.Baseline); hash.Add(e.Skip);
        }
    }

    private void WriteBuildFocus(BinaryWriter w)
    {
        w.Write(_buildFocus.Count);
        foreach (BuildFocusEntry e in _buildFocus)
        {
            w.Write(e.Settlement); w.Write(e.Civilization); w.Write(e.Code); w.Write(e.Since); w.Write(e.Baseline); w.Write(e.Skip);
        }
    }

    private void ReadBuildFocus(BinaryReader r)
    {
        _buildFocus.Clear();
        int n = ReadCount(r, 100_000);
        int last = -1;
        for (int i = 0; i < n; i++)
        {
            var e = new BuildFocusEntry
            {
                Settlement = r.ReadInt32(), Civilization = r.ReadInt32(), Code = r.ReadInt32(), Since = r.ReadInt32(), Baseline = r.ReadInt32(),
                Skip = r.ReadBoolean(),
            };
            if (e.Settlement <= last || e.Settlement >= _settlements.Count || e.Civilization < 0 || e.Civilization >= CivilizationCount || e.Code < 0 || e.Code >= 255)
                throw new InvalidDataException("Snapshot contains an invalid build focus.");
            last = e.Settlement;
            _buildFocus.Add(e);
        }
    }

    private void CopyBuildFocusFrom(LivingEconomyModel source)
    {
        _buildFocus.Clear();
        foreach (BuildFocusEntry e in source._buildFocus)
            _buildFocus.Add(new BuildFocusEntry
            {
                Settlement = e.Settlement, Civilization = e.Civilization, Code = e.Code, Since = e.Since, Baseline = e.Baseline, Skip = e.Skip,
            });
    }
}
