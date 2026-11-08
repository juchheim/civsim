using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>Where a structure stands for one settlement.</summary>
public enum BuildStatus : byte
{
    /// <summary>It is what the track is building (or, for the basic list, what the planner will build next).</summary>
    Current = 0,
    /// <summary>Wanted and allowed, but a good is short.</summary>
    Short,
    /// <summary>Wanted, allowed and paid for, but another structure has the slot.</summary>
    Waiting,
    /// <summary>Allowed, but the town sees no need for it now. The player may still ask for it.</summary>
    NotNeeded,
    /// <summary>A rule stops it here: not enough people, wrong ground, a prerequisite, someone else holds it.</summary>
    Blocked,
    /// <summary>It stands, or the limit is reached.</summary>
    Done,
}

/// <summary>What the player can do about a stopped structure.</summary>
public enum LeverKind : byte
{
    /// <summary>Learn this project (<see cref="BuildLever.Project"/>).</summary>
    Research = 0,
    /// <summary>Ask the town to build this structure next; for a wood shortage it also sends woodcutters.</summary>
    FocusThis,
    /// <summary>Build the structure this one needs first: <see cref="BuildLever.Arg"/> is its focus code.</summary>
    FocusPrerequisite,
    /// <summary>A Corvée Decree at the town (<see cref="BuildLever.Arg"/> is the <see cref="CorveeTarget"/>): its builders work half again as fast for two seasons.</summary>
    Corvee,
    /// <summary>Buy it by compact (<see cref="BuildLever.Arg"/> is the Politics TradeGood number).</summary>
    Compact,
    /// <summary>Found a village within reach of the missing deposit (Found a village Decree).</summary>
    FoundNearby,
    /// <summary>Open a mine on an ore deposit (Open a mine Decree).</summary>
    OpenMine,
    /// <summary>Set the structure aside so the slot turns to the next wanted one.</summary>
    SkipThis,
}

/// <summary>One thing the player can do (<see cref="Kind"/>) with its number and project id.</summary>
public readonly record struct BuildLever(LeverKind Kind, int Arg = 0, string? Project = null);

/// <summary>Whether the stop can be got round.</summary>
public enum BuildVerdict : byte
{
    /// <summary>Nothing is in the way.</summary>
    Clear = 0,
    /// <summary>The town is already doing what it takes; it is a matter of seasons.</summary>
    Time,
    /// <summary>The player has a lever that works.</summary>
    Resolvable,
    /// <summary>No lever the game gives the player gets round this here and now.</summary>
    Unresolvable,
}

/// <summary>One structure a settlement could build, as the planner and the rules see it this season.</summary>
public sealed record BuildItem(
    BuildTrack Track, int Kind, string Id, BuildStatus Status, BuildGate Gate, BuildNeed[] Needs, GoodSupply[] Supplies,
    Fix64 Labor, Fix64 Progress, bool CanFocus, BuildWhy FocusWhy, bool IsFocus, bool IsSkipped, BuildVerdict Verdict, BuildLever[] Levers)
{
    /// <summary>The code the SetBuildFocus command takes for this structure.</summary>
    public int Code => Track switch
    {
        BuildTrack.Iron => LivingEconomyModel.FocusCode.Iron(Kind),
        BuildTrack.Civic => LivingEconomyModel.FocusCode.Civic(Kind),
        _ => LivingEconomyModel.FocusCode.Basic(Kind),
    };

    public bool AnyShort
    {
        get { foreach (BuildNeed n in Needs) if (n.Short) return true; return false; }
    }
}

/// <summary>The headline for a settlement.</summary>
public enum BuildHeadline : byte
{
    /// <summary>Something is under way and has what it needs.</summary>
    Building = 0,
    /// <summary>The target (or the first thing the town wants) is short of a good.</summary>
    WaitingOnMaterials,
    /// <summary>Nothing the town wants is allowed or paid for... or nothing is wanted at all.</summary>
    NothingWanted,
}

/// <summary>
/// What a settlement is building and what holds it back (docs/MVP/30): one <see cref="BuildItem"/> for every structure its civilization has learned
/// that this town could ever raise, across the basic list, the Iron works and the civic buildings, each with its state, what it takes,
/// the first rule or shortage in its way, and what the player can do. Computed from the same rules the planners use; it changes nothing and draws no random numbers.
/// </summary>
public sealed record BuildOutlook(
    int Settlement, int Season, BuildHeadline Headline, BuildItem[] Items, int FocusCode, bool FocusIsSkip, int FocusSince, int FocusUntil,
    bool NeedsHousing, bool Cold, int FoodSecurityPermille, Fix64 BuildWorkLastSeason, Fix64 CivicWorkLastSeason, int Population)
{
    public static readonly BuildOutlook Empty = new(-1, 0, BuildHeadline.NothingWanted, Array.Empty<BuildItem>(), LivingEconomyModel.FocusCode.None, false, -1, -1,
        false, false, 1000, Fix64.Zero, Fix64.Zero, 0);

    /// <summary>The item the track is building now, or null.</summary>
    public BuildItem? CurrentOf(BuildTrack track)
    {
        foreach (BuildItem item in Items) if (item.Track == track && item.Status == BuildStatus.Current) return item;
        return null;
    }
}

public sealed partial class LivingEconomyModel
{
    private const int BasicTrackKinds = (int)Structure.Count;

    /// <summary>The most fields the settlement may keep (the planner builds a field while it has fewer plots than this and ground is free).</summary>
    public int FieldAllowance(int settlementIndex) => FieldLimit(_settlements[settlementIndex]);

    /// <summary>The project whose completion unlocks the structure with this registry id ("" if none): for reports on researched-but-unbuilt works.</summary>
    public string UnlockProjectOfStructure(string structureId) => UnlockProjectOf(structureId);

    /// <summary>Whether a free plot of ground exists where the settlement could still lay a field (the planner can place one).</summary>
    public bool CanPlaceAnotherField(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        foreach (int tile in RangeTiles(s.SiteTile))
            if (_fieldOwner[tile] < 0 && !IsOrchardOccupied(tile) && _world.Tile(tile).FieldYieldPerYear > Fix64.Zero) return true;
        return false;
    }

    /// <summary>
    /// The outlook of one settlement. Items are listed track by track (basic in the planner's order, then Iron, then civic); only structures whose project
    /// is learned appear. A Free Folk village or an empty settlement has none.
    /// </summary>
    public BuildOutlook BuildOutlookFor(int settlementIndex)
    {
        if (unchecked((uint)settlementIndex) >= unchecked((uint)_settlements.Count)) return BuildOutlook.Empty;
        SettlementState s = _settlements[settlementIndex];
        int season = checked((int)_seasonsCompleted);
        int pop = Population(s.Id);
        if (pop == 0 || IsFreeFolk(s)) return BuildOutlook.Empty with { Settlement = settlementIndex, Season = season };

        int focusCode = BuildFocusAt(settlementIndex, out bool focusSkip);
        (int since, int until) = BuildFocusWindow(settlementIndex);
        BasicCtx ctx = MakeBasicCtx(s, season);
        bool cold = BasicCold(s, ref ctx);
        var items = new List<BuildItem>();
        AddBasicItems(s, season, ref ctx, focusCode, focusSkip, items);
        if (IronChain && s.Metal is { } metal) AddIronItems(s, metal, focusCode, focusSkip, items);
        if (s.Civic is { } civic && CohesionLive(s)) AddCivicItems(s, civic, focusCode, focusSkip, items);

        BuildHeadline headline = BuildHeadline.NothingWanted;
        foreach (BuildItem item in items)
        {
            if (item.Status == BuildStatus.Current)
            {
                if (item.AnyShort) { headline = BuildHeadline.WaitingOnMaterials; break; }
                headline = BuildHeadline.Building;
            }
        }
        if (headline == BuildHeadline.NothingWanted)
            foreach (BuildItem item in items)
                if (item.Status == BuildStatus.Short) { headline = BuildHeadline.WaitingOnMaterials; break; }
        return new BuildOutlook(settlementIndex, season, headline, items.ToArray(), focusCode, focusSkip, since, until,
            ctx.NeedsHousing, cold, (Fix64.Clamp(s.FoodSecurity, Fix64.Zero, Fix64.FromInt(4)) * 1000).FloorToInt(),
            s.LedgerBuildWork, s.LedgerCivicWork, pop);
    }

    // ---- assembling items ---------------------------------------------------------------------------------

    private static BuildNeed[] NeedArray(int n, BuildNeed a, BuildNeed b) => n == 0 ? Array.Empty<BuildNeed>() : n == 1 ? new[] { a } : new[] { a, b };

    private void AddBasicItems(SettlementState s, int season, ref BasicCtx ctx, int focusCode, bool focusSkip, List<BuildItem> items)
    {
        Structure pick = SelectBuildCandidate(s, season);
        var seen = new bool[BasicTrackKinds];
        for (int rule = 0; rule < (int)BasicRule.Count; rule++)
        {
            Structure kind = BasicRuleKind(rule);
            if (kind == Structure.Count || seen[(int)kind]) continue;
            seen[(int)kind] = true;
            BuildGate hard = BasicGate(s, kind);
            if (hard.Cause == BuildCause.Locked) continue;
            int n = BasicNeeds(s, kind, season, out BuildNeed a, out BuildNeed b);
            BuildNeed[] needs = NeedArray(n, a, b);
            bool wanted = BasicKindWanted(s, kind, ref ctx);
            BuildStatus status;
            BuildGate gate = hard;
            if (!hard.Open) status = hard.Cause == BuildCause.Cap ? BuildStatus.Done : BuildStatus.Blocked;
            else if (kind == pick) status = BuildStatus.Current;
            else if (!wanted) { status = BuildStatus.NotNeeded; gate = Stop(BuildCause.NotNeeded, BasicNotNeededWhy(kind, ctx.NeedsHousing, s)); }
            else if (AnyShort(needs)) { status = BuildStatus.Short; gate = Stop(BuildCause.Materials, BuildWhy.None); }
            else { status = BuildStatus.Waiting; gate = Stop(BuildCause.Ahead, BuildWhy.None); }
            Fix64 labor = StructureBuildLabor(s, kind);
            Fix64 progress = status == BuildStatus.Current ? Fix64.Min(s.BuildProgress, labor) : Fix64.Zero;
            items.Add(MakeItem(s, BuildTrack.Basic, (int)kind, StructureId(kind), status, gate, needs, labor, progress, focusCode, focusSkip, season));
        }
    }

    private static BuildWhy BasicNotNeededWhy(Structure kind, bool needsHousing, SettlementState s) => kind switch
    {
        // The planner raises one pit house for room; when the town already has it, further room comes from the other dwellings.
        Structure.PitHouse when needsHousing && s.Structures[(int)Structure.PitHouse] > 0 => BuildWhy.PlannerOnlyOne,
        Structure.PitHouse or Structure.WattleHouse or Structure.LeanTo or Structure.MudbrickHouse or Structure.Longhouse => BuildWhy.HousingEnough,
        _ => BuildWhy.None,
    };

    private static bool AnyShort(BuildNeed[] needs)
    {
        foreach (BuildNeed n in needs) if (n.Short) return true;
        return false;
    }

    private void AddIronItems(SettlementState s, MetalState m, int focusCode, bool focusSkip, List<BuildItem> items)
    {
        for (int k = 0; k < (int)IronStructure.Count; k++)
        {
            IronSpec spec = IronSpecs[k];
            if (!Done(s.Civilization, spec.Unlock)) continue;
            var kind = (IronStructure)k;
            BuildGate hard = IronGate(s, kind, includeAppetite: false);
            BuildGate soft = hard.Open ? IronGate(s, kind, includeAppetite: true) : hard;
            var needList = new List<BuildNeed>(5);
            void Need(MetalGood good, int amount)
            {
                if (amount > 0) needList.Add(new BuildNeed(MetalSlot(good), Stock(s, MetalSlot(good)), Fix64.FromInt(amount)));
            }
            Need(MetalGood.Block, spec.Block); Need(MetalGood.Ashlar, spec.Ashlar); Need(MetalGood.Iron, spec.Bloom); Need(MetalGood.Wheel, spec.Wheel);
            if (spec.Wood > 0) needList.Add(new BuildNeed((int)Good.Wood, s.Stores[(int)Good.Wood], Fix64.FromInt(spec.Wood)));
            BuildNeed[] needs = needList.ToArray();
            Fix64 labor = IronLaborOf(s, spec);
            bool current = m.IronBuildKind == k;
            BuildStatus status;
            BuildGate gate = soft;
            if (!hard.Open) status = hard.Cause == BuildCause.Cap ? BuildStatus.Done : BuildStatus.Blocked;
            else if (current) { status = BuildStatus.Current; gate = BuildGate.Pass; }
            else if (!soft.Open) status = BuildStatus.NotNeeded;
            else if (m.IronBuildKind >= 0) { status = BuildStatus.Waiting; gate = Stop(BuildCause.Ahead, BuildWhy.None); }
            else { status = BuildStatus.Short; gate = Stop(BuildCause.Materials, BuildWhy.None); }
            Fix64 shelved = m.IronShelf is { } shelf ? shelf[k] : Fix64.Zero;
            Fix64 progress = current ? Fix64.Min(m.IronBuildProgress, labor) : Fix64.Min(shelved, labor);
            items.Add(MakeItem(s, BuildTrack.Iron, k, IronWorkId(k), status, gate, needs, labor, progress, focusCode, focusSkip, checked((int)_seasonsCompleted)));
        }
    }

    private void AddCivicItems(SettlementState s, CivicState c, int focusCode, bool focusSkip, List<BuildItem> items)
    {
        CivicBuild enabler = CityEnabler(s, c);
        foreach (CivicBuild b in CivicOrder)
        {
            BuildGate hard = CivicGate(s, c, b, includeAppetite: false);
            if (hard.Cause == BuildCause.Locked) continue;
            BuildGate soft = hard.Open ? CivicGate(s, c, b, includeAppetite: true) : hard;
            (int wood, int stone, int clay, int brick, int cut) = CivicMaterials(b);
            var needList = new List<BuildNeed>(5);
            void Need(int slot, int amount, Fix64 have)
            {
                if (amount > 0) needList.Add(new BuildNeed(slot, have, Fix64.FromInt(amount)));
            }
            Need((int)Good.Wood, wood, s.Stores[(int)Good.Wood]); Need((int)Good.Stone, stone, s.Stores[(int)Good.Stone]);
            Need((int)Good.Clay, clay, s.Stores[(int)Good.Clay]); Need((int)Good.Mudbrick, brick, s.Stores[(int)Good.Mudbrick]);
            Need(MetalSlot(MetalGood.Ashlar), cut, CutStoneHeld(s, blocksToo: b == CivicBuild.Palace));
            BuildNeed[] needs = needList.ToArray();
            Fix64 labor = CivicLabor(s, b);
            bool current = c.BuildKind == b;
            BuildStatus status;
            BuildGate gate = soft;
            if (!hard.Open) status = hard.Cause == BuildCause.Cap ? BuildStatus.Done : BuildStatus.Blocked;
            else if (current) { status = BuildStatus.Current; gate = BuildGate.Pass; }
            else if (enabler != CivicBuild.None && enabler != b) { status = BuildStatus.Waiting; gate = Stop(BuildCause.Ahead, BuildWhy.OnTheWayToCity); }
            else if (!soft.Open) status = BuildStatus.NotNeeded;
            else if (c.BuildKind != CivicBuild.None) { status = BuildStatus.Waiting; gate = Stop(BuildCause.Ahead, BuildWhy.None); }
            else { status = BuildStatus.Short; gate = Stop(BuildCause.Materials, BuildWhy.None); }
            Fix64 progress = current ? Fix64.Min(c.BuildProgress, labor) : Fix64.Zero;
            items.Add(MakeItem(s, BuildTrack.Civic, (int)b, CivicStructureId(b), status, gate, needs, labor, progress, focusCode, focusSkip, checked((int)_seasonsCompleted)));
        }
    }

    /// <summary>Fills in the focus state, the supplies behind any shortage, the levers and the verdict.</summary>
    private BuildItem MakeItem(SettlementState s, BuildTrack track, int kind, string id, BuildStatus status, BuildGate gate, BuildNeed[] needs,
        Fix64 labor, Fix64 progress, int focusCode, bool focusSkip, int season)
    {
        int code = track switch { BuildTrack.Iron => FocusCode.Iron(kind), BuildTrack.Civic => FocusCode.Civic(kind), _ => FocusCode.Basic(kind) };
        BuildWhy why = BuildWhy.None;
        bool canFocus = status != BuildStatus.Done && CanFocusBuild(s.Id, code, skip: false, out why);
        var supplies = new GoodSupply[needs.Length];
        var levers = new List<BuildLever>(4);
        bool anyShort = false, anyTime = false, anyDead = false;
        bool active = status is BuildStatus.Current or BuildStatus.Short or BuildStatus.Waiting;
        for (int i = 0; i < needs.Length; i++)
        {
            supplies[i] = active && needs[i].Short ? SupplyOf(s, needs[i].Slot) : new GoodSupply(SupplyKind.Flowing);
            if (!active || !needs[i].Short) continue;
            anyShort = true;
            switch (supplies[i].Kind)
            {
                case SupplyKind.NeedsProject: AddLever(levers, new BuildLever(LeverKind.Research, 0, supplies[i].Project)); break;
                case SupplyKind.NoSource:
                    if (supplies[i].Project is { Length: > 0 } way) AddLever(levers, new BuildLever(LeverKind.Research, 0, way));
                    else anyDead = true;
                    AddLever(levers, new BuildLever(LeverKind.FoundNearby));
                    break;
                case SupplyKind.NeedsOutside:
                    AddLever(levers, new BuildLever(LeverKind.Compact, (int)CivSim.Sim.Politics.TradeGood.Iron));
                    AddLever(levers, new BuildLever(LeverKind.OpenMine)); break;
                default: anyTime = true; break;
            }
        }
        bool shortOfWood = false;
        foreach (BuildNeed n in needs) if (active && n.Short && n.Slot == (int)Good.Wood) shortOfWood = true;
        // Asking for a basic structure also sends woodcutters for its wood; the civic and Iron slots fetch their own materials.
        if (track == BuildTrack.Basic && shortOfWood && canFocus && focusCode != code) AddLever(levers, new BuildLever(LeverKind.FocusThis));
        bool buildingOrShort = status is BuildStatus.Current or BuildStatus.Short;
        if (buildingOrShort || status == BuildStatus.Waiting)
        {
            int corvee = CorveeFor(track, kind);
            if (corvee >= 0 && (track == BuildTrack.Civic || kind == (int)Structure.StoneWalls) && HasCivicWork(s.Id, (byte)corvee))
                AddLever(levers, new BuildLever(LeverKind.Corvee, corvee));
        }
        if (status == BuildStatus.Current && track != BuildTrack.Basic && !(focusSkip && focusCode == code))
            AddLever(levers, new BuildLever(LeverKind.SkipThis));
        if (status == BuildStatus.Blocked || status == BuildStatus.Short && gate.Cause == BuildCause.Requires)
        {
            if (gate.Project is { Length: > 0 }) AddLever(levers, new BuildLever(LeverKind.Research, 0, gate.Project));
            int pre = PrerequisiteCode(s, gate.Why);
            if (pre >= 0) AddLever(levers, new BuildLever(LeverKind.FocusPrerequisite, pre));
        }
        BuildVerdict verdict;
        if (status is BuildStatus.Done or BuildStatus.NotNeeded) verdict = BuildVerdict.Clear;
        else if (status == BuildStatus.Blocked)
            verdict = levers.Exists(l => l.Kind is LeverKind.Research or LeverKind.FocusPrerequisite) ? BuildVerdict.Resolvable : BuildVerdict.Unresolvable;
        else if (!anyShort) verdict = status == BuildStatus.Waiting ? BuildVerdict.Time : BuildVerdict.Clear;
        else if (levers.Exists(l => l.Kind is LeverKind.Research or LeverKind.Compact or LeverKind.FocusThis)) verdict = BuildVerdict.Resolvable;
        else if (anyDead && !anyTime) verdict = BuildVerdict.Unresolvable;
        else verdict = BuildVerdict.Time;
        // Everything is in hand and the work itself is what is left.
        if (status == BuildStatus.Current && !anyShort && progress < labor) gate = Stop(BuildCause.Labor, BuildWhy.None);
        else if (status == BuildStatus.Current && anyShort) gate = Stop(BuildCause.Materials, BuildWhy.None);
        return new BuildItem(track, kind, id, status, gate, needs, supplies, labor, progress, canFocus, why, focusCode == code && !focusSkip,
            focusCode == code && focusSkip, verdict, levers.ToArray());
    }

    private static void AddLever(List<BuildLever> levers, BuildLever lever)
    {
        if (!levers.Contains(lever)) levers.Add(lever);
    }

    /// <summary>The Corvée target that drives this structure, or -1.</summary>
    private static int CorveeFor(BuildTrack track, int kind)
    {
        if (track == BuildTrack.Basic) return kind == (int)Structure.StoneWalls ? CorveeTarget.Walls : -1;
        if (track != BuildTrack.Civic) return -1;
        return (CivicBuild)kind switch
        {
            CivicBuild.Market => CorveeTarget.Market, CivicBuild.Temple => CorveeTarget.Temple, CivicBuild.Palace => CorveeTarget.Palace,
            CivicBuild.School => CorveeTarget.School, _ => CorveeTarget.TopPublicWork,
        };
    }

    /// <summary>The focus code of the structure a "needs X first" rule points at, when the town may be asked for it; -1 otherwise.</summary>
    private int PrerequisiteCode(SettlementState s, BuildWhy why)
    {
        int code = why switch
        {
            BuildWhy.NeedsKiln => FocusCode.Basic((int)Structure.Kiln),
            BuildWhy.NeedsWalls => FocusCode.Basic((int)Structure.StoneWalls),
            BuildWhy.NeedsHarbor => FocusCode.Iron((int)IronStructure.Harbor),
            BuildWhy.NeedsRoad => FocusCode.Iron((int)IronStructure.Road),
            BuildWhy.NeedsTemple => FocusCode.Civic((int)CivicBuild.Temple),
            BuildWhy.NeedsMarket => FocusCode.Civic((int)CivicBuild.Market),
            _ => -1,
        };
        return code >= 0 && CanFocusBuild(s.Id, code, skip: false, out _) ? code : -1;
    }
}
