using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Research;

/// <summary>How one civilization is controlled and tuned at the start of a match.</summary>
/// <param name="Leaning">Craft, Kin and Reach weights in percent.</param>
/// <param name="Council">The Council of Elders fills empty slots (always for AI).</param>
/// <param name="CouncilDelayTicks">How long an empty slot waits for the Council: 0 for AI, 150 (one season) for the player.</param>
public readonly record struct ResearchCivSetup(int LeaningCraft, int LeaningKin, int LeaningReach, bool Council, int CouncilDelayTicks)
{
    public static ResearchCivSetup Ai(int craft, int kin, int reach) => new(craft, kin, reach, true, 0);
    public static ResearchCivSetup Player(int craft, int kin, int reach) => new(craft, kin, reach, true, ResearchSystem.SeasonTicks);
}

/// <summary>
/// The research rules (docs/MVP/02, 05 §16): Insight split 4 : 2 : 1 into three focus slots, queues, Know-how,
/// Glimpse and Hidden states, gates, diffusion, adoption, Keystones and eras, Firsts and the Council of
/// Elders. A pure function of its state, the tick, commands and what <see cref="IResearchSource"/> reports.
/// Completions are reported through <see cref="TakeCompletions"/> for the economy to apply.
/// </summary>
public sealed partial class ResearchSystem : IResearchView
{
    /// <summary>Insight, Know-how and slot progress advance every 10 ticks (docs/MVP/05 §3.2).</summary>
    public const int StepTicks = 10;
    public const int SeasonTicks = 150;
    private const int StepsPerYear = 60;
    private const int MaxQueue = 8;

    private static readonly Fix64 PTeachBase = Fix64.FromRatio(25, 100);
    private static readonly Fix64 PTeachMax = Fix64.FromRatio(95, 100);
    private static readonly Fix64 KnowHowCostShare = Fix64.Half;
    private static readonly Fix64 ClimateDiscount = Fix64.FromRatio(7, 10);
    private static readonly Fix64 PioneerBonus = Fix64.FromRatio(11, 10);
    private static readonly Fix64 LegacyFirstShare = Fix64.Half;
    private static readonly Fix64 KeystoneLegacyMult = Fix64.FromInt(3);
    private const int MixingNeighbours = 12;
    private const int MasteryPerEra = 1;

    private readonly ResearchCatalog _catalog;
    private readonly IResearchSource _source;
    private readonly ulong _seed;
    private readonly CivResearch[] _civs;
    private readonly bool[] _contact;
    private readonly int[] _firstBy;
    private readonly List<ResearchEvent> _events = new();
    private readonly List<(int Civ, int Project)> _completions = new();
    private readonly List<(int Parent, int Daughter)> _fissions = new();
    private readonly Fix64[] _practiceWork;
    private readonly Fix64[][] _people;

    private readonly Fix64 _commonsCoef, _commonsExp, _elderCommons, _knowHowOfCommons, _lowTies;
    private readonly Fix64 _eraLag, _diffusionStep, _diffusionFloor, _eraBoon, _calibration;
    private readonly int[] _slotWeight = new int[CivResearch.SlotCount];
    private readonly int _statCommons, _statElder, _roleElder;
    private readonly int _soc03;
    private long _tick;

    public ResearchSystem(ResearchCatalog catalog, IResearchSource source, ulong seed, ReadOnlySpan<ResearchCivSetup> setup)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(source);
        if (setup.Length != source.CivCount) throw new ArgumentException("One setup per civilization.", nameof(setup));
        _catalog = catalog;
        _source = source;
        _seed = seed;
        _commonsCoef = catalog.Constant("CommonsCoef");
        _commonsExp = catalog.Constant("CommonsExp");
        _elderCommons = catalog.Constant("ElderCommons");
        _knowHowOfCommons = catalog.Constant("KHOfCommons");
        _lowTies = catalog.Constant("LowTiesShare");
        _eraLag = catalog.Constant("EraLag");
        _diffusionStep = catalog.Constant("DiffusionStep");
        _diffusionFloor = catalog.Constant("DiffusionFloor");
        _eraBoon = catalog.Constant("EraBoonPct");
        _calibration = catalog.Constant("InsightCalibration");
        _slotWeight[0] = catalog.Constant("SlotPrimary").FloorToInt();
        _slotWeight[1] = catalog.Constant("SlotSecondary").FloorToInt();
        _slotWeight[2] = catalog.Constant("SlotTertiary").FloorToInt();
        _statCommons = catalog.StatIndex("commons_insight");
        _statElder = catalog.StatIndex("elder_insight");
        _roleElder = catalog.RoleIndex("elder");
        _soc03 = catalog.IndexOf("SOC-03");

        int civs = source.CivCount;
        _civs = new CivResearch[civs];
        _contact = new bool[civs * civs];
        _firstBy = new int[catalog.Count];
        Array.Fill(_firstBy, -1);
        _practiceWork = new Fix64[catalog.PracticeIds.Count];
        _people = new Fix64[civs][];
        for (int c = 0; c < civs; c++)
        {
            var civ = new CivResearch(c, catalog.Count, catalog.LineageCodes.Count, catalog.StatNames.Count)
            {
                Council = setup[c].Council,
                CouncilDelay = setup[c].CouncilDelayTicks,
                ClimateTarget = source.ClimateTarget(c),
            };
            civ.Leaning[0] = Math.Clamp(setup[c].LeaningCraft, 0, 100);
            civ.Leaning[1] = Math.Clamp(setup[c].LeaningKin, 0, 100);
            civ.Leaning[2] = Math.Clamp(setup[c].LeaningReach, 0, 100);
            _civs[c] = civ;
            _people[c] = new Fix64[CivResearch.MaxSettlements];
        }
        for (int c = 0; c < civs; c++)
        {
            CivResearch civ = _civs[c];
            civ.SeenMask = source.SeenMask(c, this);
            RefreshPeople(c);
            RebuildDerived(c);
            RefreshInsight(c);
            RefreshStates(c, announce: true);
        }
    }

    // ---- public surface -------------------------------------------------------------------

    public ResearchCatalog Catalog => _catalog;
    public int CivCount => _civs.Length;
    public long Tick => _tick;
    public IReadOnlyList<ResearchEvent> Events => _events;
    public void ClearEvents() => _events.Clear();

    public int Era(int civ) => _civs[civ].Era;
    public bool IsDone(int civ, int project) => _civs[civ].Done[project];
    public ProjectState State(int civ, int project) => _civs[civ].LastState[project];
    public int Slot(int civ, int slot) => _civs[civ].Slots[slot];
    public IReadOnlyList<int> Queue(int civ) => _civs[civ].Queue;
    public int DoneCount(int civ) { int n = 0; foreach (bool d in _civs[civ].Done) if (d) n++; return n; }
    public int DoneInEra(int civ, int era) => _civs[civ].DoneByEra[era];
    public Fix64 Progress(int civ, int project) => _civs[civ].Progress[project];
    public Fix64 KnowHow(int civ, int lineage) => _civs[civ].KnowHow[lineage];
    public Fix64 KnowHowApplied(int civ, int project) => _civs[civ].KnowHowApplied[project];
    public Fix64 InsightPerYear(int civ) => _civs[civ].InsightPerYear;
    public Fix64 CommonsPerYear(int civ) => _civs[civ].CommonsPerYear;
    public bool IsPioneer(int civ, int project) => _civs[civ].Pioneer[project];
    public int FirstBy(int project) => _firstBy[project];
    public long CompletedTick(int civ, int project) => _civs[civ].DoneTick[project];
    public Fix64 Legacy(int civ) => _civs[civ].MasteryLegacy + _civs[civ].FirstsLegacy;
    public Fix64 FirstsLegacy(int civ) => _civs[civ].FirstsLegacy;
    public Fix64 MasteryLegacy(int civ) => _civs[civ].MasteryLegacy;
    public int BeaconBoons(int civ) => _civs[civ].BeaconBoons;
    public bool CouncilOn(int civ) => _civs[civ].Council;
    public int Leaning(int civ, int theme) => _civs[civ].Leaning[theme];
    public bool InContact(int a, int b) => _contact[a * _civs.Length + b];
    public ulong SeenMask(int civ) => _civs[civ].SeenMask;

    /// <summary>The civilization-wide share of people who know a completed method (docs/MVP/02 §10).</summary>
    public Fix64 AdoptionFraction(int civ, int project)
    {
        CivResearch c = _civs[civ];
        if (!c.Done[project]) return Fix64.Zero;
        if (!_catalog.Projects[project].SpreadsAsSkill) return Fix64.One;
        Fix64[]? arr = c.Adoption[project];
        if (arr is null) return Fix64.One;
        Fix64 total = Fix64.Zero, know = Fix64.Zero;
        for (int s = 0; s < CivResearch.MaxSettlements; s++)
        {
            total += _people[civ][s];
            know += arr[s] * _people[civ][s];
        }
        return total.Raw == 0 ? Fix64.Zero : Fix64.Min(Fix64.One, know / total);
    }

    /// <summary>Product of the civilization's multipliers on a named stat (percent and multiplier mods), adoption-scaled.</summary>
    public Fix64 ModifierMultiplier(int civ, string stat)
    {
        int i = _catalog.StatIndex(stat);
        return i < 0 ? Fix64.One : _civs[civ].ModMult[i];
    }

    public Fix64 ModifierFlat(int civ, string stat)
    {
        int i = _catalog.StatIndex(stat);
        return i < 0 ? Fix64.Zero : _civs[civ].ModFlat[i];
    }

    /// <summary>The population-average multiplier on one cause of death from completed projects (docs/MVP/11 §1).</summary>
    public Fix64 HazardMultiplier(int civ, string cause)
    {
        for (int i = 0; i < ResearchCatalog.HazardCauses.Length; i++)
            if (ResearchCatalog.HazardCauses[i] == cause) return _civs[civ].HazardMult[i];
        throw new ArgumentException($"Unknown cause {cause}.", nameof(cause));
    }

    /// <summary>Effective research cost now: list cost × Diffusion × EraLag × gate discounts (docs/MVP/02 §8.3).</summary>
    public Fix64 EffectiveCost(int civ, int project)
    {
        CivResearch c = _civs[civ];
        ProjectDef def = _catalog.Projects[project];
        Fix64 cost = def.Cost;
        int knowers = 0;
        for (int o = 0; o < _civs.Length; o++)
            if (o != civ && InContact(civ, o) && _civs[o].Done[project]) knowers++;
        if (knowers > 0) cost = cost * Fix64.Max(_diffusionFloor, Fix64.One - _diffusionStep * knowers);
        int lag = c.Era - def.Era;
        if (lag > 0) cost = cost * FixMath.PowInt(_eraLag, lag);
        foreach (Gate g in def.Gates)
            if (g.Kind == GateKind.Climate && g.Target == c.ClimateTarget) cost = cost * ClimateDiscount;
        return cost;
    }

    /// <summary>Completions since the last call, for the economy to apply as <see cref="CommandKind.CompleteProject"/>.</summary>
    public void TakeCompletions(List<(int Civ, int Project)> into)
    {
        into.AddRange(_completions);
        _completions.Clear();
    }

    /// <summary>
    /// Records that two civilizations met: each now knows what the other has completed (Glimpsed, and it satisfies
    /// their [Seen] gates), and Diffusion discounts apply (docs/MVP/02 §14). M5 calls this from first contact.
    /// </summary>
    public void RecordContact(int a, int b)
    {
        if (a == b || InContact(a, b)) return;
        _contact[a * _civs.Length + b] = true;
        _contact[b * _civs.Length + a] = true;
        Emit((byte)a, ResearchEventKind.ContactMade, -1, b, 0);
        Emit((byte)b, ResearchEventKind.ContactMade, -1, a, 0);
        for (int p = 0; p < _catalog.Count; p++)
        {
            if (_civs[a].Done[p]) _civs[b].KnownByContact[p] = true;
            if (_civs[b].Done[p]) _civs[a].KnownByContact[p] = true;
        }
        RefreshStates(a, announce: true);
        RefreshStates(b, announce: true);
    }

    /// <summary>
    /// Marks gate targets as seen (bit i = target i): traded goods satisfy [Seen] gates for those goods (docs/MVP/13 §4.2).
    /// </summary>
    public void AddSeen(int civ, ulong mask)
    {
        CivResearch c = _civs[civ];
        ulong fresh = mask & ~c.SeenMask;
        if (fresh == 0) return;
        c.SeenMask |= fresh;
        for (int t = 0; t < 64; t++)
            if ((fresh & (1UL << t)) != 0) Emit((byte)civ, ResearchEventKind.SeenResource, -1, t, 0);
        RefreshStates(civ, announce: true);
    }

    /// <summary>Sets whether access gate target i is available (own territory or trade). Cutting it makes dependent projects Dormant.</summary>
    public void SetAccess(int civ, int gateTarget, bool available)
    {
        CivResearch c = _civs[civ];
        ulong bit = 1UL << gateTarget;
        c.HasMask = available ? c.HasMask | bit : c.HasMask & ~bit;
        RefreshStates(civ, announce: true);
    }

    // ---- stepping -------------------------------------------------------------------------

    public void Step(long tick, ReadOnlySpan<Command> commands)
    {
        _tick = tick;
        foreach (Command command in commands) Apply(command, tick);
        for (int c = 0; c < _civs.Length; c++) FillEmptySlots(c, tick);
        if ((tick + 1) % StepTicks == 0)
            for (int c = 0; c < _civs.Length; c++) ResearchStep(c, tick);
    }

    /// <summary>Season-end bookkeeping, after the economy's own season update (call <see cref="IResearchSource.BeginSeason"/> first).</summary>
    public void OnSeasonEnd(long tick)
    {
        _tick = tick;
        for (int c = 0; c < _civs.Length; c++)
        {
            CivResearch civ = _civs[c];
            RefreshPeople(c);
            GenerateKnowHow(c);
            _fissions.Clear();
            _source.DrainFissions(c, _fissions);
            foreach ((int parent, int daughter) in _fissions) InheritAdoption(civ, parent, daughter);
            SpreadAdoption(c, tick);
            ulong seen = _source.SeenMask(c, this);
            if (seen != civ.SeenMask)
            {
                ulong fresh = seen & ~civ.SeenMask;
                civ.SeenMask |= seen;
                for (int t = 0; t < 64; t++)
                    if ((fresh & (1UL << t)) != 0) Emit((byte)c, ResearchEventKind.SeenResource, -1, t, 0);
            }
            civ.ClimateTarget = _source.ClimateTarget(c);
            RebuildDerived(c);
            RefreshInsight(c);
            RefreshStates(c, announce: true);
            FillEmptySlots(c, tick);
        }
    }

    private void Apply(Command command, long tick)
    {
        if (command.Civ >= _civs.Length) return;
        int c = command.Civ;
        CivResearch civ = _civs[c];
        switch (command.Kind)
        {
            case CommandKind.SetSlot:
                SetSlot(c, command.A, command.B, tick);
                break;
            case CommandKind.QueueProject:
                if ((uint)command.A < (uint)_catalog.Count && civ.Queue.Count < MaxQueue &&
                    !civ.Queue.Contains(command.A) && !civ.Done[command.A] && civ.LastState[command.A] >= ProjectState.Glimpsed)
                    civ.Queue.Add(command.A);
                break;
            case CommandKind.DequeueProject:
                if ((uint)command.A < (uint)civ.Queue.Count) civ.Queue.RemoveAt(command.A);
                break;
            case CommandKind.SetLeaning:
                if ((uint)command.A < 3u) civ.Leaning[command.A] = Math.Clamp(command.B, 0, 100);
                break;
            case CommandKind.SetCouncil:
                civ.Council = command.A != 0;
                break;
            case CommandKind.CompleteProject:
                // A grant (fixtures, debugging): complete without spending Insight. The economy receives the
                // same command from its caller, so it is not forwarded again.
                if ((uint)command.A < (uint)_catalog.Count && !civ.Done[command.A] && _catalog.Projects[command.A].Numeric)
                    Complete(c, command.A, tick, forwardToEconomy: false);
                break;
        }
    }

    private void SetSlot(int c, int slot, int project, long tick)
    {
        CivResearch civ = _civs[c];
        if ((uint)slot >= CivResearch.SlotCount) return;
        if (project < 0)
        {
            if (civ.Slots[slot] >= 0)
            {
                civ.Slots[slot] = -1;
                civ.SlotEmptySince[slot] = tick;
                RefreshStates(c, announce: false);
            }
            return;
        }
        if ((uint)project >= (uint)_catalog.Count) return;
        ProjectState state = civ.LastState[project];
        if (state != ProjectState.Available && state != ProjectState.Slotted) return;
        int other = civ.SlotOf(project);
        if (other == slot) return;
        if (other >= 0)
        {
            // Dragging between slots re-ranks; progress is untouched.
            civ.Slots[other] = civ.Slots[slot];
            if (civ.Slots[other] < 0) civ.SlotEmptySince[other] = tick;
        }
        civ.Slots[slot] = project;
        civ.LastState[project] = ProjectState.Slotted;
        civ.Queue.Remove(project);
        Emit((byte)c, ResearchEventKind.SlotFilled, project, slot, 0);
        RefreshStates(c, announce: false);
    }

    private void ResearchStep(int c, long tick)
    {
        CivResearch civ = _civs[c];
        if (civ.InsightDirty) RefreshInsight(c);
        int totalWeight = 0;
        for (int s = 0; s < CivResearch.SlotCount; s++) if (civ.Slots[s] >= 0) totalWeight += _slotWeight[s];
        if (totalWeight == 0) return;
        Fix64 perStep = civ.InsightPerYear / StepsPerYear;
        // Decide every slot's share before anything completes, so a cascade mid-step cannot change who gets what.
        Span<int> project = stackalloc int[CivResearch.SlotCount];
        Span<Fix64> amount = stackalloc Fix64[CivResearch.SlotCount];
        for (int s = 0; s < CivResearch.SlotCount; s++)
        {
            project[s] = civ.Slots[s];
            if (project[s] < 0) continue;
            // The 4:2:1 split among occupied slots; an empty slot's share goes to the others.
            amount[s] = perStep * _slotWeight[s] / totalWeight + civ.SlotCarry[s];
            civ.SlotCarry[s] = Fix64.Zero;
        }
        for (int s = 0; s < CivResearch.SlotCount; s++)
            if (project[s] >= 0) AddProgress(c, project[s], amount[s], tick);
        if (civ.CouncilDelay == 0 && civ.Council) RankByCouncil(c);
    }

    /// <summary>Adds Insight to a project, with Know-how, and completes it, passing the overflow on (docs/MVP/02 §8.2).</summary>
    private void AddProgress(int c, int p, Fix64 amount, long tick)
    {
        CivResearch civ = _civs[c];
        while (true)
        {
            ProjectDef def = _catalog.Projects[p];
            Fix64 cost = EffectiveCost(c, p);
            Fix64 progress = civ.Progress[p] + amount;
            if (def.Lineage >= 0)
            {
                // Know-how drains into a slotted project of its lineage at up to the slot's Insight rate,
                // and may cover at most half of the project's cost (docs/MVP/02 §9).
                Fix64 room = Fix64.Max(Fix64.Zero, cost * KnowHowCostShare - civ.KnowHowApplied[p]);
                Fix64 kh = Fix64.Min(civ.KnowHow[def.Lineage], Fix64.Min(amount, room));
                if (kh.Raw > 0)
                {
                    civ.KnowHow[def.Lineage] -= kh;
                    civ.KnowHowApplied[p] += kh;
                    progress += kh;
                }
            }
            if (progress < cost) { civ.Progress[p] = progress; return; }
            amount = progress - cost;
            civ.Progress[p] = cost;
            int slot = civ.SlotOf(p);
            Complete(c, p, tick, forwardToEconomy: true);
            // Cascade: the others slide up, the queue or the Council fills the last slot (docs/MVP/08 §7). The overflow
            // goes to whichever project now holds the freed slot's place.
            Cascade(c, tick);
            int next = slot >= 0 ? civ.Slots[slot] : -1;
            if (next < 0)
            {
                if (slot >= 0) civ.SlotCarry[slot] += amount;
                return;
            }
            p = next;
        }
    }

    /// <summary>Slides slotted projects up to close gaps and fills what is left empty at the bottom.</summary>
    private void Cascade(int c, long tick)
    {
        CivResearch civ = _civs[c];
        int write = 0;
        for (int s = 0; s < CivResearch.SlotCount; s++)
            if (civ.Slots[s] >= 0) civ.Slots[write++] = civ.Slots[s];
        for (int s = write; s < CivResearch.SlotCount; s++) civ.Slots[s] = -1;
        if (write < CivResearch.SlotCount) civ.SlotEmptySince[write] = tick; // the slot that just opened at the bottom
        FillEmptySlots(c, tick);
    }

    /// <summary>Fills empty slots from the queue, then (after the grace period) from the Council.</summary>
    private void FillEmptySlots(int c, long tick)
    {
        CivResearch civ = _civs[c];
        for (int s = 0; s < CivResearch.SlotCount; s++)
            if (civ.Slots[s] < 0) PickForSlot(c, s, tick);
    }

    private int PickForSlot(int c, int slot, long tick)
    {
        CivResearch civ = _civs[c];
        for (int i = 0; i < civ.Queue.Count; i++)
        {
            int q = civ.Queue[i];
            if (civ.Done[q]) { civ.Queue.RemoveAt(i); i--; continue; }
            if (civ.LastState[q] != ProjectState.Available) continue;
            civ.Queue.RemoveAt(i);
            Place(c, slot, q, fromCouncil: false, CouncilReason.Cheap);
            return q;
        }
        if (!civ.Council || tick - civ.SlotEmptySince[slot] < civ.CouncilDelay) return -1;
        int pick = CouncilPick(c, out CouncilReason reason);
        if (pick < 0) return -1;
        Place(c, slot, pick, fromCouncil: true, reason);
        return pick;
    }

    private void Place(int c, int slot, int project, bool fromCouncil, CouncilReason reason)
    {
        CivResearch civ = _civs[c];
        civ.Slots[slot] = project;
        civ.LastState[project] = ProjectState.Slotted;
        Emit((byte)c, ResearchEventKind.SlotFilled, project, slot, fromCouncil ? 2 : 1);
        if (fromCouncil) Emit((byte)c, ResearchEventKind.CouncilPick, project, slot, (int)reason);
    }

    // ---- completion -----------------------------------------------------------------------

    private void Complete(int c, int p, long tick, bool forwardToEconomy)
    {
        CivResearch civ = _civs[c];
        ProjectDef def = _catalog.Projects[p];
        civ.Done[p] = true;
        civ.DoneTick[p] = tick;
        civ.Queue.Remove(p);
        int slot = civ.SlotOf(p);
        if (slot >= 0) { civ.Slots[slot] = -1; civ.SlotEmptySince[slot] = tick; }
        if (!def.IsKeystone) civ.DoneByEra[def.Era]++;

        int practice = def.Practice.Length > 0 ? _catalog.PracticeIndex(def.Practice[0].Action) : -1;
        long innovator = _source.ChooseInnovator(c, p, practice);
        bool inEra = def.Era >= civ.Era;
        Emit((byte)c, ResearchEventKind.Completed, p, (int)(innovator & 0x7fffffff), 0);
        // Backfills are cheap and numerous: they get no Moment, only a batched Chronicle line (docs/MVP/07 F8).
        Emit((byte)c, ResearchEventKind.Moment, p, inEra ? 1 : 0, 0);
        for (int e = 0; e < def.Effects.Length; e++)
        {
            VisibleChange visual = def.Effects[e].Visual;
            if (visual != VisibleChange.None) Emit((byte)c, ResearchEventKind.VisibleChange, p, (int)visual, e);
        }

        civ.MasteryLegacy += Fix64.FromInt((def.Era + 1) * MasteryPerEra);
        if (_firstBy[p] < 0)
        {
            _firstBy[p] = c;
            civ.Pioneer[p] = true;
            Fix64 points = _catalog.EraBase[def.Era] * LegacyFirstShare;
            if (def.IsKeystone || def.Type == ProjectType.Wonder) points = points * KeystoneLegacyMult;
            civ.FirstsLegacy += points;
            Emit((byte)c, ResearchEventKind.FirstClaimed, p, points.FloorToInt(), 0);
        }

        if (def.SpreadsAsSkill)
        {
            var arr = new Fix64[CivResearch.MaxSettlements];
            Fix64 people = _people[c][0];
            arr[0] = people > Fix64.One ? Fix64.One / people : Fix64.One;
            civ.Adoption[p] = arr;
            civ.AdoptionAge[p] = 0;
        }

        for (int o = 0; o < _civs.Length; o++)
        {
            if (o == c || !InContact(c, o)) continue;
            _civs[o].KnownByContact[p] = true;
            RefreshStates(o, announce: true);
        }

        if (def.IsKeystone) AdvanceEra(c, p, def);
        if (forwardToEconomy) _completions.Add((c, p));
        RebuildDerived(c);
        civ.InsightDirty = true;
        RefreshStates(c, announce: true);
    }

    private void AdvanceEra(int c, int keystone, ProjectDef def)
    {
        CivResearch civ = _civs[c];
        civ.Era = def.Enters;
        // Era Boon (docs/MVP/02 §13): +1 Beacon for a Reach-leaning civilization, otherwise +5% Insight.
        bool beacon = civ.Leaning[2] > civ.Leaning[0] && civ.Leaning[2] > civ.Leaning[1];
        if (beacon) civ.BeaconBoons++;
        else civ.EraBoonInsight = civ.EraBoonInsight * (Fix64.One + _eraBoon);
        Emit((byte)c, ResearchEventKind.EraAdvanced, keystone, def.Enters, beacon ? 1 : 0);
    }

    // ---- states, gates, glimpses ----------------------------------------------------------

    private bool SeenSatisfied(CivResearch civ, ProjectDef def)
    {
        foreach (Gate g in def.Gates)
        {
            if (g.Kind != GateKind.Seen) continue;
            if ((civ.SeenMask & (1UL << g.Target)) == 0 && !civ.KnownByContact[def.Index]) return false;
        }
        return true;
    }

    private bool AccessSatisfied(CivResearch civ, ProjectDef def)
    {
        foreach (Gate g in def.Gates)
            if (g.Kind == GateKind.Has && (civ.HasMask & (1UL << g.Target)) == 0) return false;
        return true;
    }

    private ProjectState ComputeState(CivResearch civ, ProjectDef def)
    {
        int p = def.Index;
        if (civ.Done[p]) return AccessSatisfied(civ, def) ? ProjectState.Completed : ProjectState.Dormant;
        if (!def.Numeric || def.Era > civ.Era) return ProjectState.Hidden;
        if (!SeenSatisfied(civ, def)) return ProjectState.Hidden;
        int missing = 0;
        foreach (int r in def.Requires) if (!civ.Done[r]) missing++;
        if (def.RequiresAny.Length > 0)
        {
            bool any = false;
            foreach (int r in def.RequiresAny) if (civ.Done[r]) { any = true; break; }
            if (!any) missing++;
        }
        if (def.IsKeystone && civ.DoneByEra[def.BreadthEra] < def.BreadthMin) missing++;
        if (missing == 0) return civ.SlotOf(p) >= 0 ? ProjectState.Slotted : ProjectState.Available;
        // A keystone is always on the horizon of its own era; others glimpse when one step away or known to a contact.
        if (missing == 1 || civ.KnownByContact[p] || def.IsKeystone) return ProjectState.Glimpsed;
        return ProjectState.Hidden;
    }

    private void RefreshStates(int c, bool announce)
    {
        CivResearch civ = _civs[c];
        for (int p = 0; p < _catalog.Count; p++)
        {
            ProjectState now = ComputeState(civ, _catalog.Projects[p]);
            ProjectState before = civ.LastState[p];
            if (now == before) continue;
            civ.LastState[p] = now;
            if (!announce) continue;
            if (now == ProjectState.Glimpsed && before == ProjectState.Hidden)
                Emit((byte)c, ResearchEventKind.Glimpsed, p, 0, 0);
            else if (now == ProjectState.Available && before is ProjectState.Hidden or ProjectState.Glimpsed)
                Emit((byte)c, ResearchEventKind.Unlocked, p, 0, 0);
            else if (now == ProjectState.Dormant) Emit((byte)c, ResearchEventKind.DormantChanged, p, 1, 0);
            else if (before == ProjectState.Dormant && now == ProjectState.Completed) Emit((byte)c, ResearchEventKind.DormantChanged, p, 0, 0);
        }
    }

    // ---- Insight and Know-how -------------------------------------------------------------

    private void RefreshPeople(int c)
    {
        int n = Math.Min(_source.SettlementCount(c), CivResearch.MaxSettlements);
        for (int s = 0; s < CivResearch.MaxSettlements; s++)
            _people[c][s] = s < n ? _source.SettlementPeople(c, s) : Fix64.Zero;
    }

    private void RefreshInsight(int c)
    {
        CivResearch civ = _civs[c];
        InsightInputs inputs = _source.GetInsightInputs(c);
        Fix64 lmCommons = civ.ModMult[_statCommons];
        // Hearth settlements only (FIR-03): scale the bonus by the share of Commons produced at hearths.
        lmCommons = Fix64.One + (lmCommons - Fix64.One) * inputs.HearthShare;
        Fix64 lmElder = civ.ModMult[_statElder];
        int settlements = Math.Max(1, inputs.Settlements);
        // Before SOC-03 daughter bands share Insight at half strength (docs/MVP/09 §7.4).
        Fix64 ties = civ.Done[_soc03] ? Fix64.One
            : (Fix64.One + _lowTies * (settlements - 1)) / settlements;
        Fix64 effective = (inputs.Adults + _elderCommons * lmElder * inputs.Elders) * ties;
        Fix64 commons = _commonsCoef * FixMath.Pow(effective, _commonsExp) * lmCommons;

        Fix64 roles = Fix64.Zero;
        for (int r = 0; r < _catalog.Roles.Count; r++)
        {
            RoleDef role = _catalog.Roles[r];
            if (role.Insight.Raw == 0 || role.UnlockedBy < 0 || !civ.Done[role.UnlockedBy]) continue;
            Fix64 heads = inputs.RoleHeads is { } h && r < h.Length ? h[r] : Fix64.Zero;
            Fix64 output = heads * role.Insight;
            if (r == _roleElder) output = output * lmElder;
            roles += output;
        }
        // InsightCalibration re-fits the whole formula to the live demography (docs/MVP/07c §7).
        Fix64 scale = inputs.InsightScale * _calibration * civ.EraBoonInsight;
        Fix64 year = (commons + roles * inputs.RolesScale + inputs.StructureOutput) * scale;
        civ.CommonsPerYear = commons * scale;
        civ.InsightPerYear = year;
        civ.InsightDirty = false;
    }

    private void GenerateKnowHow(int c)
    {
        CivResearch civ = _civs[c];
        Array.Clear(_practiceWork);
        _source.GetPractice(c, this, _practiceWork, out Fix64 practisingShare);
        Fix64 totalWork = Fix64.Zero;
        foreach (Fix64 w in _practiceWork) totalWork += w;
        if (totalWork.Raw == 0) return;
        InsightInputs inputs = _source.GetInsightInputs(c);
        // A practising figure generates Know-how worth 35% of its own Commons Insight (docs/MVP/07 F4), here the
        // season's Commons scaled by how much of the population is practising.
        Fix64 season = civ.CommonsPerYear / 4 * _knowHowOfCommons * practisingShare * inputs.KnowHowScale * civ.KnowHowGain;
        civ.KnowHowPerYear = season * 4;
        for (int a = 0; a < _practiceWork.Length; a++)
        {
            if (_practiceWork[a].Raw == 0) continue;
            Fix64 part = season * _practiceWork[a] / totalWork;
            foreach ((int lineage, int percent) in _catalog.PracticeSplit(a, civ.Era))
                civ.KnowHow[lineage] += part * percent / 100 * civ.KnowHowLineageGain[lineage];
        }
    }

    // ---- derived effects (modifier registry) ----------------------------------------------

    private void RebuildDerived(int c)
    {
        CivResearch civ = _civs[c];
        Array.Fill(civ.ModMult, Fix64.One);
        Array.Clear(civ.ModFlat);
        Array.Fill(civ.HazardMult, Fix64.One);
        Array.Fill(civ.CapMult, Fix64.One);
        Array.Fill(civ.KnowHowLineageGain, Fix64.One);
        civ.AdoptMult = Fix64.One;
        civ.KnowHowGain = Fix64.One;
        civ.GroupCapValue = Fix64.Zero;
        for (int p = 0; p < _catalog.Count; p++)
        {
            if (!civ.Done[p]) continue;
            ProjectDef def = _catalog.Projects[p];
            if (!def.Numeric) continue;
            // Skills spread person to person, so their numeric effects scale with adoption; structures,
            // roles, institutions and keystones land at once (docs/MVP/02 §10).
            Fix64 adopted = def.SpreadsAsSkill ? AdoptionFraction(c, p) : Fix64.One;
            Fix64 pioneer = civ.Pioneer[p] ? PioneerBonus : Fix64.One;
            foreach (EffectDef e in def.Effects)
            {
                switch (e.Kind)
                {
                    case EffectKind.Mod when e.Stat is not null && e.HasValue:
                        int s = _catalog.StatIndex(e.Stat);
                        switch (e.Op)
                        {
                            case "pct": civ.ModMult[s] = civ.ModMult[s] * (Fix64.One + e.Value * adopted * pioneer); break;
                            case "mult": civ.ModMult[s] = civ.ModMult[s] * (Fix64.One + (e.Value - Fix64.One) * adopted * pioneer); break;
                            case "flat": civ.ModFlat[s] += e.Value * adopted; break;
                        }
                        break;
                    case EffectKind.Hazard when e.Cause is not null:
                        int cause = Array.IndexOf(ResearchCatalog.HazardCauses, e.Cause);
                        if (cause >= 0) civ.HazardMult[cause] = civ.HazardMult[cause] * (Fix64.One + (e.Value - Fix64.One) * adopted);
                        break;
                    case EffectKind.Cap when e.Cap is { } cap && cap != CapKind.Group:
                        civ.CapMult[(int)cap] = civ.CapMult[(int)cap] * FixMath.Pow(e.Value, e.Take);
                        break;
                    case EffectKind.Cap:
                        civ.GroupCapValue = Fix64.Max(civ.GroupCapValue, e.Value);
                        break;
                    case EffectKind.Adopt:
                        civ.AdoptMult = civ.AdoptMult * e.Value;
                        break;
                    case EffectKind.KnowHow when e.Text is not null:
                        ApplyKnowHow(civ, e.Text);
                        break;
                }
            }
        }
        civ.InsightDirty = true;
    }

    /// <summary>Parses the know_how effect text: "+20%" (all lineages) or "HNT, +10%" (one lineage).</summary>
    private void ApplyKnowHow(CivResearch civ, string text)
    {
        int percent = 0;
        bool seenDigit = false;
        foreach (char ch in text)
        {
            if (ch is >= '0' and <= '9') { percent = checked(percent * 10 + (ch - '0')); seenDigit = true; }
            else if (seenDigit) break;
        }
        if (!seenDigit) throw new InvalidDataException($"Unreadable know_how effect '{text}'.");
        Fix64 gain = Fix64.One + Fix64.FromRatio(percent, 100);
        int comma = text.IndexOf(',', StringComparison.Ordinal);
        if (comma > 0)
        {
            int lineage = _catalog.LineageIndex(text[..comma].Trim());
            if (lineage < 0) throw new InvalidDataException($"Unknown lineage in know_how effect '{text}'.");
            civ.KnowHowLineageGain[lineage] = civ.KnowHowLineageGain[lineage] * gain;
        }
        else civ.KnowHowGain = civ.KnowHowGain * gain;
    }

    // ---- adoption -------------------------------------------------------------------------

    private void InheritAdoption(CivResearch civ, int parent, int daughter)
    {
        if ((uint)parent >= CivResearch.MaxSettlements || (uint)daughter >= CivResearch.MaxSettlements) return;
        for (int p = 0; p < _catalog.Count; p++)
            if (civ.Adoption[p] is { } a) a[daughter] = a[parent];
    }

    private void SpreadAdoption(int c, long tick)
    {
        CivResearch civ = _civs[c];
        int settlements = Math.Min(_source.SettlementCount(c), CivResearch.MaxSettlements);
        Fix64 pTeach = Fix64.Min(PTeachMax, PTeachBase * civ.AdoptMult);
        Fix64 keep = Fix64.One - pTeach;
        Fix64 logKeep = FixMath.Log2(keep);
        for (int p = 0; p < _catalog.Count; p++)
        {
            if (!civ.Done[p] || civ.Adoption[p] is not { } arr || civ.AdoptionMilestone[p] >= 100) continue;
            civ.AdoptionAge[p]++;
            // A distant settlement hears of it after its lag, once some other settlement has half its people trained.
            for (int s = 0; s < settlements; s++)
            {
                if (arr[s].Raw != 0 || _people[c][s].Raw == 0) continue;
                if (civ.AdoptionAge[p] < _source.AdoptionLagSeasons(c, s)) continue;
                for (int o = 0; o < settlements; o++)
                    if (o != s && arr[o] >= Fix64.Half) { arr[s] = Fix64.One / Fix64.Max(Fix64.One, _people[c][s]); break; }
            }
            for (int s = 0; s < settlements; s++)
            {
                Fix64 f = arr[s];
                if (f.Raw == 0 || f >= Fix64.One) continue;
                Fix64 people = _people[c][s];
                // Each non-knower within three tiles meets about f x (co-workers) teachers; each teaches with p_teach.
                Fix64 neighbours = Fix64.Min(Fix64.Max(Fix64.Zero, people - Fix64.One), Fix64.FromInt(MixingNeighbours));
                Fix64 learn = Fix64.One - FixMath.Exp2(f * neighbours * logKeep);
                f = f + (Fix64.One - f) * learn;
                if (f >= Fix64.FromRatio(995, 1000)) f = Fix64.One;
                arr[s] = f;
            }
            Fix64 civWide = AdoptionFraction(c, p);
            int milestone = civWide >= Fix64.FromRatio(999, 1000) ? 100 : civWide >= Fix64.FromRatio(9, 10) ? 90 : civWide >= Fix64.Half ? 50 : 0;
            if (milestone > civ.AdoptionMilestone[p])
            {
                civ.AdoptionMilestone[p] = (byte)milestone;
                Emit((byte)c, ResearchEventKind.Adopted, p, milestone, settlements);
            }
        }
        _ = tick;
    }

    // ---- events, hashing, snapshots -------------------------------------------------------

    private void Emit(byte civ, ResearchEventKind kind, int project, int a, int b)
        => _events.Add(new ResearchEvent(_tick, civ, kind, project, a, b));

    public void Hash(ref StateHasher h)
    {
        h.Add(_tick);
        foreach (CivResearch civ in _civs) civ.Hash(ref h);
        foreach (int f in _firstBy) h.Add(f);
        foreach (bool k in _contact) h.Add(k);
        _source.Hash(ref h);
    }

    public void WriteSnapshot(BinaryWriter w)
    {
        w.Write(_tick);
        w.Write(_civs.Length);
        w.Write(_catalog.Count);
        foreach (CivResearch civ in _civs) civ.Write(w);
        foreach (int f in _firstBy) w.Write(f);
        foreach (bool k in _contact) w.Write(k);
        _source.WriteSnapshot(w);
    }

    public void ReadSnapshot(BinaryReader r)
    {
        _tick = r.ReadInt64();
        if (r.ReadInt32() != _civs.Length || r.ReadInt32() != _catalog.Count)
            throw new InvalidDataException("Research snapshot does not match this match or content pack.");
        foreach (CivResearch civ in _civs) civ.Read(r);
        for (int i = 0; i < _firstBy.Length; i++) _firstBy[i] = r.ReadInt32();
        for (int i = 0; i < _contact.Length; i++) _contact[i] = r.ReadBoolean();
        _source.ReadSnapshot(r);
        _events.Clear();
        _completions.Clear();
        for (int c = 0; c < _civs.Length; c++)
        {
            RefreshPeople(c);
            RebuildDerived(c);
            RefreshInsight(c);
        }
    }
}
