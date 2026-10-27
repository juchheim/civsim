using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Sim.Ai.Forecast;

/// <summary>One season of a forecast (docs/MVP/14 §3.2 "outputs per season").</summary>
public readonly record struct ForecastSeason(int Season, Fix64 InsightPerYear, Fix64 Adults, int Completed, int Era, Fix64 Mastery, Fix64 Firsts, Fix64 Utility,
    ForecastBronzeSeason Bronze);

/// <summary>The summary of one rollout.</summary>
public struct ForecastResult
{
    /// <summary>U of docs/MVP/14 §4.1: time-weighted Legacy (Mastery + expected Firsts) plus the caller's bonuses.</summary>
    public Fix64 Utility;
    public Fix64 Mastery;
    /// <summary>Expected Firsts Legacy gained (each First weighted by the chance of beating every rival to it).</summary>
    public Fix64 Firsts;
    public int Completed;
    public int Era;
    /// <summary>Forecast steps (<see cref="ForecastTuning.StepsPerSeason"/> a season) at which KEY-1 through KEY-4 complete; -1 if not within the horizon or already done.</summary>
    public int Key1, Key2, Key3, Key4;
    public int Steps;
    /// <summary>The last season's coarse own-only E3 state, including deterministic Mandate and Admin progression.</summary>
    public ForecastBronzeSeason Bronze;
}

/// <summary>What a completion is worth to the caller beyond Mastery: the Firsts race and per-project bonuses.</summary>
public sealed class ForecastValuation
{
    public ForecastValuation(int projects)
    {
        RivalEta = new int[projects];
        Array.Fill(RivalEta, int.MaxValue);
        Bonus = new Fix64[projects];
        Urgent = new Fix64[projects];
        Weight = new Fix64[projects];
        Array.Fill(Weight, Fix64.One);
    }

    /// <summary>The step at which the fastest rival is believed to complete each project; int.MaxValue for never within the horizon.</summary>
    public int[] RivalEta { get; }
    /// <summary>Flat value added on completion (Strategist urgency, archetype signature), time-weighted like Legacy.</summary>
    public Fix64[] Bonus { get; }
    /// <summary>Multiplier on a completion's Legacy (archetype Leaning, 14 §9.1).</summary>
    public Fix64[] Weight { get; }
    /// <summary>Half-width of the Firsts race in steps: a First is sure when we finish this much before the rival (14 §4.4: one season).</summary>
    public int MarginSteps { get; set; } = ForecastTuning.StepsPerSeason;
    /// <summary>Relative uncertainty of a rival's ETA: the race margin is at least this share of the time until it.</summary>
    public Fix64 EtaUncertainty { get; set; } = Fix64.FromRatio(1, 5);
    /// <summary>Value of a keystone per season it lands before the horizon, on top of its Legacy.</summary>
    public Fix64 KeystoneTempo { get; set; }
    /// <summary>
    /// Value that is only worth having soon (a threat to answer, 14 §6.4): full at once, falling to nothing at
    /// <see cref="UrgencySteps"/>. A flat bonus would be collected later just the same.
    /// </summary>
    public Fix64[] Urgent { get; }
    public int UrgencySteps { get; set; } = 1;
}

/// <summary>
/// The forecast model (docs/MVP/14 §3.2): a fast, deterministic mean-field projection of one civilization's research,
/// season by season, in Fix64 only. It replays the research rules of docs/MVP/02 (4 : 2 : 1 slots with cascade and
/// overflow, Know-how drain capped at half a project's cost and at the slot's rate, EraLag, keystone breadth, the era
/// gate, Era Boons, Insight from Commons, roles, Commons and elder mods and SOC-03 ties) on a coarse demography: adults
/// fed by a 56-season child pipeline (07c §7a), elders, settlements by population. People do not depend on the plan, so
/// they are projected once per <see cref="Prepare"/>; each <see cref="Run"/> costs only the research steps.
/// </summary>
public sealed class ResearchForecast
{
    private static readonly Fix64 PioneerBonus = Fix64.FromRatio(1, 10);
    private static readonly Fix64 InsightGainWeight = Fix64.FromInt(16);
    private static readonly Fix64 KeystonePathBoost = Fix64.FromInt(3);
    private static readonly Fix64 KeystoneReadyBoost = Fix64.FromInt(20);
    private static readonly Fix64 BreadthBoost = Fix64.FromInt(2);
    private static readonly Fix64 MinLeaning = Fix64.FromRatio(1, 100);
    private static readonly Fix64 TerminalShare = Fix64.Half;
    private readonly ForecastCatalog _c;
    private readonly int _sps;
    private readonly int _n;
    private ForecastStart _start = null!;
    private int _horizon;           // seasons
    private int _steps;             // horizon in steps

    // People, by season 0.._horizon.
    private Fix64[] _adults = Array.Empty<Fix64>(), _elders = Array.Empty<Fix64>();
    private int[] _settlements = Array.Empty<int>(), _elderHeads = Array.Empty<int>(), _shamanHeads = Array.Empty<int>();
    // Pow((adults + 2 lmE elders) ties, 0.85) by season, cached per (lmE, SOC-03) pair.
    private readonly long[] _powKey = new long[4];
    private readonly Fix64[][] _pow = new Fix64[4][];
    private int _powKeys;
    private Fix64 _fudge = Fix64.One;
    private Fix64 _commons0 = Fix64.One, _invCommons0 = Fix64.One;
    private Fix64 _gainAll0 = Fix64.One;
    // Indexed by era advancement from the start, through every era in the catalog.
    private readonly Fix64[][] _costByEra = new Fix64[ResearchCatalog.EraCount][];
    private readonly int[][] _fallback = new int[ResearchCatalog.EraCount][];
    private readonly int[][] _rankPos = new int[ResearchCatalog.EraCount][];
    // Available, unslotted projects as bits over the current era's fallback order: the Council's pick is the lowest bit.
    private ulong[] _avail = Array.Empty<ulong>();
    private int _order;
    private int[] _blockers0 = Array.Empty<int>();
    private bool[] _anySat0 = Array.Empty<bool>();
    private Fix64[] _timeWeight = Array.Empty<Fix64>();
    private readonly Fix64[,] _slotShare = new Fix64[8, CivResearch.SlotCount];
    private int[] _genLineages = Array.Empty<int>();
    private Fix64 _work0 = Fix64.One, _gen0 = Fix64.Zero;
    private Fix64 _extraWork;
    private Fix64[] _extraLineage = Array.Empty<Fix64>();
    private Fix64[] _genBase = Array.Empty<Fix64>();
    private int[] _genList = Array.Empty<int>();
    private int _genCount;
    private bool _genDirty;
    private int[] _lineageLeft0 = Array.Empty<int>(), _lineageLeft = Array.Empty<int>();
    // Slot amounts per step for each occupancy mask, valid until Insight changes.
    private readonly Fix64[] _amountByMask = new Fix64[8 * CivResearch.SlotCount];
    private int _amountMasks;
    private Fix64 _perStep;
    private readonly int[] _stepProject = new int[CivResearch.SlotCount];
    private readonly Fix64[] _stepAmount = new Fix64[CivResearch.SlotCount];
    private Fix64[] _lineageGain0 = Array.Empty<Fix64>();

    // Rollout state.
    private readonly bool[] _done;
    private readonly Fix64[] _progress, _khApplied;
    private readonly int[] _blockers;
    private readonly bool[] _anySat;
    private readonly Fix64[] _kh, _lineageGain;
    private readonly int[] _doneByEra = new int[ResearchCatalog.EraCount];
    private readonly int[] _slots = new int[CivResearch.SlotCount];
    private readonly Fix64[] _carry = new Fix64[CivResearch.SlotCount];
    private int[] _plan = Array.Empty<int>();
    private bool[] _planUsed = Array.Empty<bool>();
    private int _planLength, _planHead;
    private int _era, _step;
    private Fix64 _lmC, _lmE, _boon, _gainAll;
    private bool _soc03;
    private ulong _roles;
    private Fix64 _utility, _mastery, _firsts;
    private int _completed;
    private ForecastValuation? _value;
    private bool _insightDirty;
    private bool _seenOpen;
    private bool _promote;
    private Fix64 _khUsed;
    private ForecastBronzeRollout? _bronze;
    /// <summary>Know-how drained into projects during the last run.</summary>
    public Fix64 KnowHowUsed => _khUsed;

    public ResearchForecast(ForecastCatalog catalog, int stepsPerSeason = ForecastTuning.StepsPerSeason)
    {
        _c = catalog;
        _sps = Math.Max(1, stepsPerSeason);
        _n = catalog.Count;
        _done = new bool[_n];
        _progress = new Fix64[_n];
        _khApplied = new Fix64[_n];
        _blockers = new int[_n];
        _anySat = new bool[_n];
        _kh = new Fix64[catalog.LineageCount];
        _lineageGain = new Fix64[catalog.LineageCount];
        DoneAt = new int[_n];
        CompletionOrder = new int[_n];
        for (int mask = 1; mask < 8; mask++)
        {
            int total = 0;
            for (int s = 0; s < CivResearch.SlotCount; s++) if ((mask & (1 << s)) != 0) total += catalog.SlotWeight[s];
            for (int s = 0; s < CivResearch.SlotCount; s++)
                if ((mask & (1 << s)) != 0) _slotShare[mask, s] = Fix64.FromRatio(catalog.SlotWeight[s], total);
        }
    }

    public ForecastCatalog Catalog => _c;
    /// <summary>Research steps per season in this forecast (steps are the unit of <see cref="DoneAt"/> and the ETAs).</summary>
    public int StepsPerSeason => _sps;
    public ForecastStart Start => _start;
    public int HorizonSeasons => _horizon;
    public int HorizonSteps => _steps;
    /// <summary>Step of completion of each project in the last run (-1 if not completed in it).</summary>
    public int[] DoneAt { get; }
    /// <summary>Projects in the order the last run completed them; the first <see cref="CompletedCount"/> are valid.</summary>
    public int[] CompletionOrder { get; }
    public int CompletedCount => _completed;
    /// <summary>Rollouts run since construction: the planner's budget is counted in these, never in time (14 §12).</summary>
    public long Rollouts { get; private set; }
    public Fix64 Adults(int season) => _adults[Math.Min(season, _horizon)];
    public Fix64 Elders(int season) => _elders[Math.Min(season, _horizon)];
    public int Settlements(int season) => _settlements[Math.Min(season, _horizon)];

    /// <summary>Projects people and prepares costs and the fallback order for rollouts from <paramref name="start"/>.</summary>
    public void Prepare(ForecastStart start, int horizonSeasons)
    {
        _start = start;
        _horizon = Math.Max(1, horizonSeasons);
        _steps = _horizon * _sps;
        ProjectPeople(start.People, start.Season);
        _powKeys = 0;

        // Costs by era: research charges EraLag^(era − project era) on top of the list cost.
        for (int k = 0; k < _costByEra.Length; k++)
        {
            Fix64[] cost = _costByEra[k] ??= new Fix64[_n];
            int era = Math.Min(start.Era + k, ResearchCatalog.EraCount - 1);
            foreach (int p in _c.Numeric)
            {
                int lagNow = Math.Max(0, start.Era - _c.Era[p]);
                int lagThen = Math.Max(0, era - _c.Era[p]);
                cost[p] = lagThen > lagNow ? start.Cost[p] * FixMath.PowInt(_c.EraLag, lagThen - lagNow) : start.Cost[p];
            }
        }

        // Blockers: missing prerequisites, an unmet one-of group, the era gate, keystone breadth, unseen gates.
        if (_blockers0.Length != _n) { _blockers0 = new int[_n]; _anySat0 = new bool[_n]; }
        Array.Fill(_blockers0, 1); // draft projects can never be researched
        foreach (int p in _c.Numeric)
        {
            int b = 0;
            foreach (int r in _c.Requires[p]) if (!start.Done[r]) b++;
            bool any = _c.RequiresAny[p].Length == 0;
            foreach (int r in _c.RequiresAny[p]) if (start.Done[r]) any = true;
            _anySat0[p] = any;
            if (!any) b++;
            if (_c.Era[p] > start.Era) b++;
            if (_c.IsKeystone[p] && start.DoneByEra[_c.BreadthEra[p]] < _c.BreadthMin[p]) b++;
            if (start.SeenBlocked[p]) b++; // lifted when reach grows (an era) or a reveal project lands
            _blockers0[p] = b;
        }

        // Insight now, by the model, against the live figure: the ratio absorbs what the model leaves out.
        _lmC = start.LmCommons; _lmE = start.LmElder; _soc03 = start.Done[_c.Soc03]; _boon = Fix64.One;
        _roles = RolesOf(start.Done);
        _fudge = Fix64.One;
        Fix64 model = InsightAt(0, out Fix64 commons);
        _fudge = model.Raw > 0 && start.InsightPerYear.Raw > 0
            ? Fix64.Clamp(start.InsightPerYear / model, ForecastTuning.FudgeMin, ForecastTuning.FudgeMax) : Fix64.One;
        _commons0 = Fix64.Max(Fix64.Epsilon, commons * _fudge);
        // Q32.32 cannot represent 1 / epsilon (or 1 / 2 raw units). An empty civ has no baseline Commons to
        // normalize against, so it contributes no projected Know-how until it has a representable baseline.
        _invCommons0 = _commons0.Raw > 2 ? Fix64.One / _commons0 : Fix64.Zero;
        _gainAll0 = Fix64.One;
        if (_lineageGain0.Length != _c.LineageCount) _lineageGain0 = new Fix64[_c.LineageCount];
        Array.Fill(_lineageGain0, Fix64.One);
        foreach (int p in _c.Numeric)
        {
            if (!start.Done[p]) continue;
            _gainAll0 = _gainAll0 * _c.KnowHowGainAll[p];
            if (_c.KnowHowGainLineage[p] >= 0) _lineageGain0[_c.KnowHowGainLineage[p]] = _lineageGain0[_c.KnowHowGainLineage[p]] * _c.KnowHowGainLineageMult[p];
        }
        var gen = new List<int>();
        for (int l = 0; l < _c.LineageCount; l++) if (start.KnowHowGen[l].Raw > 0) gen.Add(l);
        _genLineages = gen.ToArray();
        _work0 = _c.BaseWork;
        _gen0 = Fix64.Zero;
        foreach (int p in _c.Numeric) if (start.Done[p]) _work0 += _c.AmbientWork[p];
        foreach (int l in _genLineages) _gen0 += start.KnowHowGen[l];
        if (_extraLineage.Length != _c.LineageCount)
        {
            _extraLineage = new Fix64[_c.LineageCount];
            _genBase = new Fix64[_c.LineageCount];
            _genList = new int[_c.LineageCount];
            _lineageLeft0 = new int[_c.LineageCount];
            _lineageLeft = new int[_c.LineageCount];
        }
        Array.Clear(_lineageLeft0);
        foreach (int p in _c.Numeric) if (!start.Done[p] && _c.Lineage[p] >= 0) _lineageLeft0[_c.Lineage[p]]++;

        // Time weight: a completion at the horizon is worth half of one now (Legacy earlier is safer and compounds).
        if (_timeWeight.Length != _steps + 1) _timeWeight = new Fix64[_steps + 1];
        for (int s = 0; s <= _steps; s++) _timeWeight[s] = Fix64.One - Fix64.FromRatio(s, 2L * _steps);

        BuildFallbackOrders(start);
    }

    /// <summary>
    /// The Council of Elders' ranking (docs/MVP/02 §15) frozen at the start, one order per era the civilization may reach:
    /// what fills a slot the plan leaves empty, as it does live, and how a Council rival is modeled.
    /// </summary>
    private void BuildFallbackOrders(ForecastStart start)
    {
        var scores = new Fix64[_n];
        var depth = new int[_n];
        for (int k = 0; k < _fallback.Length; k++)
        {
            int era = Math.Min(start.Era + k, ResearchCatalog.EraCount - 1);
            Fix64[] cost = _costByEra[k];
            int key = _c.Catalog.KeystoneLeaving(era);
            bool[]? closure = _c.Catalog.ClosureOf(era);
            Array.Clear(depth);
            if (closure is not null)
            {
                for (int p = 0; p < _n; p++) if (closure[p] && !start.Done[p]) depth[p] = 1;
                for (int pass = 0; pass < 40; pass++)
                {
                    bool changed = false;
                    for (int q = 0; q < _n; q++)
                    {
                        if (depth[q] == 0) continue;
                        foreach (int r in _c.Requires[q])
                            if (depth[r] > 0 && depth[r] < depth[q] + 1) { depth[r] = depth[q] + 1; changed = true; }
                        if (_c.RequiresAny[q].Length > 0)
                        {
                            int chosen = _c.RequiresAny[q][0];
                            foreach (int alt in _c.RequiresAny[q]) if (closure[alt]) { chosen = alt; break; }
                            if (depth[chosen] > 0 && depth[chosen] < depth[q] + 1) { depth[chosen] = depth[q] + 1; changed = true; }
                        }
                    }
                    if (!changed) break;
                }
            }
            int count = 0;
            foreach (int p in _c.Numeric)
            {
                if (start.Done[p] || _c.Era[p] > era) { scores[p] = Fix64.MinValue; continue; }
                count++;
                Fix64 c = Fix64.Max(cost[p], Fix64.Epsilon);
                if (c.Raw <= 2)
                {
                    // The exact inverse cost is outside Q32.32. Treat malformed/near-zero costs as free
                    // projects for ranking rather than overflowing either ratio below.
                    scores[p] = Fix64.MaxValue;
                    continue;
                }
                Fix64 leaning = _c.Theme[p] == Theme.Keystone ? Fix64.One : Fix64.Max(MinLeaning, Fix64.FromRatio(start.Leaning[(int)_c.Theme[p]], 100));
                Fix64 kh = _c.Lineage[p] >= 0 ? Fix64.Min(Fix64.One, start.KnowHow[_c.Lineage[p]] / c) : Fix64.Zero;
                Fix64 opportunity = kh + InsightGain(p, start) * InsightGainWeight;
                Fix64 keyFactor = Fix64.One;
                if (depth[p] > 0)
                {
                    keyFactor = KeystonePathBoost + Fix64.FromInt(depth[p]);
                    if (p == key) keyFactor += KeystoneReadyBoost;
                }
                else if (_c.Era[p] == era && !_c.IsKeystone[p]) keyFactor = BreadthBoost;
                scores[p] = leaning * (Fix64.One + opportunity) * keyFactor / c;
            }
            int[] order = _fallback[k] is { } existing && existing.Length == count ? existing : new int[count];
            int i = 0;
            foreach (int p in _c.Numeric) if (scores[p] != Fix64.MinValue) order[i++] = p;
            // Stable insertion sort, descending score; ties keep ascending id.
            for (int a = 1; a < order.Length; a++)
                for (int b = a; b > 0 && scores[order[b]] > scores[order[b - 1]]; b--)
                    (order[b], order[b - 1]) = (order[b - 1], order[b]);
            _fallback[k] = order;
            int[] pos = _rankPos[k] is { } rp && rp.Length == _n ? rp : new int[_n];
            Array.Fill(pos, -1);
            for (int j = 0; j < order.Length; j++) pos[order[j]] = j;
            _rankPos[k] = pos;
        }
    }

    /// <summary>A project's Insight gain as a fraction of today's (the Council's Opportunity term, 02 §15).</summary>
    public Fix64 InsightGain(int p, ForecastStart start)
    {
        if (start.InsightPerYear.Raw <= 0) return Fix64.Zero;
        Fix64 gain = _c.CommonsPct[p];
        if (_c.RoleOf[p] >= 0) gain += _c.RoleInsight[_c.RoleOf[p]] * Math.Max(1, start.People.Settlements) * _c.Calibration / start.InsightPerYear;
        if (_c.ElderMult[p] != Fix64.One) gain += (_c.ElderMult[p] - Fix64.One) / 3;
        if (p == _c.Soc03 && !start.Done[p])
        {
            Fix64 ties = _c.Ties(Math.Max(2, start.People.Settlements), false);
            gain += (Fix64.One - ties) / ties;
        }
        return Fix64.Min(gain, Fix64.FromInt(2));
    }

    private ulong RolesOf(bool[] done)
    {
        ulong roles = 0;
        foreach (int p in _c.Numeric) if (done[p] && _c.RoleOf[p] >= 0) roles |= 1UL << _c.RoleOf[p];
        return roles;
    }

    // ---- people ---------------------------------------------------------------------------

    private void ProjectPeople(ForecastPeople people, int season)
    {
        int h = _horizon;
        if (_adults.Length != h + 1)
        {
            _adults = new Fix64[h + 1]; _elders = new Fix64[h + 1]; _settlements = new int[h + 1];
            _elderHeads = new int[h + 1]; _shamanHeads = new int[h + 1];
        }
        var kids = new Fix64[ForecastTuning.ChildSeasons];
        // Children's ages are not known: a growing people is young, so each older year holds a little fewer (index 0 oldest).
        Fix64 weight = Fix64.One, total = Fix64.Zero;
        for (int i = kids.Length - 1; i >= 0; i--) { kids[i] = weight; total += weight; weight = weight * ForecastTuning.ChildAgeTaper; }
        for (int i = 0; i < kids.Length; i++) kids[i] = people.Children * kids[i] / total;
        int oldest = 0; // ring index of the oldest bucket
        // Adults by seasons since they turned 14. The ages of those alive now are not known: they are spread evenly over the
        // first 72 seasons of adulthood at the start (the founders are 18 to 32, 05 §20), a span that widens as the match ages.
        var grown = new Fix64[ForecastTuning.AdultSeasons];
        int spread = Math.Min(ForecastTuning.YoungAdultSeasons + Math.Max(0, season), grown.Length);
        for (int i = 0; i < spread; i++) grown[grown.Length - 1 - i] = people.Adults / spread;
        int eldest = 0; // ring index of the adults closest to 45
        Fix64 a = people.Adults, e = people.Elders;
        Fix64 keepChild = Fix64.One - ForecastTuning.ChildMortality;
        Fix64 keepAdult = Fix64.One - ForecastTuning.AdultMortality;
        Fix64 keepElder = Fix64.One - ForecastTuning.ElderMortality;
        int floorSettlements = Math.Max(1, people.Settlements);
        for (int t = 0; t <= h; t++)
        {
            Fix64 children = Fix64.Zero;
            foreach (Fix64 k in kids) children += k;
            int pop = (a + e + children).FloorToInt();
            int n = Math.Max(floorSettlements, 1 + Math.Max(0, pop - ForecastTuning.SettlementOffset) / ForecastTuning.PeoplePerSettlement);
            floorSettlements = n;
            _adults[t] = a; _elders[t] = e; _settlements[t] = n;
            _elderHeads[t] = Math.Min(n, (e + Fix64.Half).FloorToInt());
            int shamans = a.FloorToInt() / ForecastTuning.AdultsPerShaman;
            _shamanHeads[t] = Math.Min(n, shamans);
            if (t == h) break;
            Fix64 matured = kids[oldest] * keepChild;
            Fix64 births = a * ForecastTuning.BirthsPerAdult;
            for (int i = 0; i < kids.Length; i++) kids[i] = kids[i] * keepChild;
            kids[oldest] = births;                 // the oldest bucket leaves; the newborns take its place
            oldest = (oldest + 1) % kids.Length;   // and the next-oldest becomes the oldest
            Fix64 aged = grown[eldest] * keepAdult;
            a = Fix64.Zero;
            for (int i = 0; i < grown.Length; i++) { grown[i] = grown[i] * keepAdult; a += grown[i]; }
            a = a - grown[eldest] + matured;
            grown[eldest] = matured;
            eldest = (eldest + 1) % grown.Length;
            e = e * keepElder + aged;
        }
    }

    // ---- Insight --------------------------------------------------------------------------

    private Fix64 PowFor(int season)
    {
        long key = (_lmE.Raw << 1) | (_soc03 ? 1L : 0L);
        int slot = -1;
        for (int i = 0; i < _powKeys; i++) if (_powKey[i] == key) { slot = i; break; }
        if (slot < 0)
        {
            if (_powKeys == _powKey.Length) return ComputePow(season);
            slot = _powKeys++;
            _powKey[slot] = key;
            Fix64[] arr = _pow[slot] is { } existing && existing.Length == _horizon + 1 ? existing : new Fix64[_horizon + 1];
            Array.Fill(arr, Fix64.MinValue);
            _pow[slot] = arr;
        }
        Fix64[] cache = _pow[slot];
        if (cache[season] == Fix64.MinValue) cache[season] = ComputePow(season);
        return cache[season];
    }

    private Fix64 ComputePow(int season)
    {
        Fix64 w = (_adults[season] + _c.ElderCommons * _lmE * _elders[season]) * _c.Ties(_settlements[season], _soc03);
        return w.Raw <= 0 ? Fix64.Zero : FixMath.Pow(w, _c.CommonsExp);
    }

    /// <summary>Insight a year in a season with the rollout's current mods (docs/MVP/02 §8.1, with the live calibration).</summary>
    private Fix64 InsightAt(int season, out Fix64 commons)
    {
        Fix64 scale = _start.Scale * _boon * _fudge;
        commons = _c.CommonsCoef * PowFor(season) * _lmC;
        Fix64 roles = Fix64.Zero;
        if (_roles != 0)
        {
            if (_c.ElderRole >= 0 && (_roles & (1UL << _c.ElderRole)) != 0)
                roles += _c.RoleInsight[_c.ElderRole] * _elderHeads[season] * _lmE;
            if (_c.ShamanRole >= 0 && (_roles & (1UL << _c.ShamanRole)) != 0)
                roles += _c.RoleInsight[_c.ShamanRole] * _shamanHeads[season];
        }
        Fix64 year = (commons + roles * _start.RolesScale) * scale;
        commons = commons * scale;
        return year;
    }

    // ---- rollout --------------------------------------------------------------------------

    /// <summary>
    /// Runs the research forward from the prepared start. With a plan, the slots start empty and fill from the plan in order
    /// (what the Spark writes: slots, then the queue); the fallback (the Council) fills a slot the plan cannot. Without a
    /// plan the live slots and queue are kept. <paramref name="promoteKeystone"/> plays the Council's habit of pulling a ready
    /// keystone straight into Primary (docs/MVP/02 §15), for modeling Council civilizations.
    /// </summary>
    public ForecastResult Run(ReadOnlySpan<int> plan, ForecastValuation? value = null, List<ForecastSeason>? trace = null, bool keepLiveSlots = false,
        bool promoteKeystone = false)
    {
        Rollouts++;
        ForecastStart s = _start;
        _value = value;
        Array.Copy(s.Done, _done, _n);
        Array.Copy(s.Progress, _progress, _n);
        Array.Copy(s.KnowHowApplied, _khApplied, _n);
        Array.Copy(_blockers0, _blockers, _n);
        Array.Copy(_anySat0, _anySat, _n);
        Array.Copy(s.KnowHow, _kh, _kh.Length);
        Array.Copy(_lineageGain0, _lineageGain, _lineageGain.Length);
        Array.Copy(s.DoneByEra, _doneByEra, _doneByEra.Length);
        Array.Fill(DoneAt, -1);
        _era = s.Era;
        _lmC = s.LmCommons; _lmE = s.LmElder; _soc03 = s.Done[_c.Soc03]; _boon = Fix64.One; _gainAll = _gainAll0;
        _roles = RolesOf(s.Done);
        _utility = Fix64.Zero; _mastery = Fix64.Zero; _firsts = Fix64.Zero; _completed = 0;
        _step = 0;
        _seenOpen = false;
        _khUsed = Fix64.Zero;
        _bronze = new ForecastBronzeRollout(_start.Bronze, _c.Catalog);
        _extraWork = Fix64.Zero;
        Array.Clear(_extraLineage);
        _genDirty = true;
        Array.Copy(_lineageLeft0, _lineageLeft, _lineageLeft.Length);
        Array.Clear(_carry);

        int length = plan.Length + (keepLiveSlots ? s.Queue.Count : 0);
        if (_plan.Length < length) { _plan = new int[Math.Max(length, 16)]; _planUsed = new bool[_plan.Length]; }
        _planLength = 0;
        _planHead = 0;
        if (keepLiveSlots)
        {
            for (int i = 0; i < CivResearch.SlotCount; i++) _slots[i] = s.Slots[i];
            foreach (int q in s.Queue) _plan[_planLength++] = q;
        }
        else Array.Fill(_slots, -1);
        foreach (int p in plan) _plan[_planLength++] = p;
        Array.Clear(_planUsed, 0, _planLength);
        _promote = promoteKeystone;
        _order = 0;
        RebuildAvailable();
        Fill();

        var result = new ForecastResult { Key1 = -1, Key2 = -1, Key3 = -1, Key4 = -1 };
        Fix64 perStep = Fix64.Zero;
        _insightDirty = true;
        for (int season = 0; season < _horizon; season++)
        {
            Fix64 commons = Fix64.Zero;
            for (int k = 0; k < _sps; k++)
            {
                if (_insightDirty)
                {
                    perStep = InsightAt(season, out commons) / (4 * _sps);
                    _insightDirty = false;
                    _perStep = perStep;
                    _amountMasks = 0;
                }
                Step();
                _step++;
            }
            if (_insightDirty) InsightAt(season, out commons); // a completion in the last step changed it
            _insightDirty = true;                              // the next season brings new people
            GenerateKnowHow(commons);
            _bronze.EndSeason();
            if (trace is not null)
            {
                ForecastBronzeSeason bronzeSeason = _bronze.Snapshot(season + 1);
                trace.Add(new ForecastSeason(season + 1, InsightAt(Math.Min(season + 1, _horizon), out _), _adults[Math.Min(season + 1, _horizon)],
                    _completed, _era, _mastery, _firsts, _utility, bronzeSeason));
            }
            if (_slots[0] < 0 && _slots[1] < 0 && _slots[2] < 0) break; // nothing left that can be researched
        }

        _utility += TerminalValue();
        result.Utility = _utility;
        result.Mastery = _mastery;
        result.Firsts = _firsts;
        result.Completed = _completed;
        result.Era = _era;
        result.Steps = _step;
        result.Bronze = _bronze.Snapshot(Math.Min(_horizon, Math.Max(1, (_step + _sps - 1) / _sps)));
        int k1 = _c.Catalog.KeystoneLeaving(0), k2 = _c.Catalog.KeystoneLeaving(1), k3 = _c.Catalog.KeystoneLeaving(2), k4 = _c.Catalog.KeystoneLeaving(3);
        if (k1 >= 0) result.Key1 = DoneAt[k1];
        if (k2 >= 0) result.Key2 = DoneAt[k2];
        if (k3 >= 0) result.Key3 = DoneAt[k3];
        if (k4 >= 0) result.Key4 = DoneAt[k4];
        return result;
    }

    /// <summary>Half credit for work in progress at the horizon, so a long project started now is not worth nothing.</summary>
    private Fix64 TerminalValue()
    {
        Fix64 v = Fix64.Zero;
        for (int i = 0; i < CivResearch.SlotCount; i++)
        {
            int p = _slots[i];
            if (p < 0) continue;
            Fix64 cost = Cost(p);
            if (cost.Raw <= 0) continue;
            Fix64 share = Fix64.Min(Fix64.One, _progress[p] / cost);
            v += share * Worth(p, _steps) * TerminalShare * _timeWeight[_steps];
        }
        return v;
    }

    private Fix64 Cost(int p) => _costByEra[Math.Min(_era - _start.Era, _costByEra.Length - 1)][p];

    private void Step()
    {
        int mask = 0;
        for (int s = 0; s < CivResearch.SlotCount; s++) if (_slots[s] >= 0) mask |= 1 << s;
        if (mask == 0) return;
        if ((_amountMasks & (1 << mask)) == 0)
        {
            for (int s = 0; s < CivResearch.SlotCount; s++) _amountByMask[mask * CivResearch.SlotCount + s] = _perStep * _slotShare[mask, s];
            _amountMasks |= 1 << mask;
        }
        // Every slot's share is decided before anything completes, as live (docs/MVP/02 §8.2).
        for (int s = 0; s < CivResearch.SlotCount; s++)
        {
            _stepProject[s] = _slots[s];
            if (_stepProject[s] < 0) continue;
            _stepAmount[s] = _amountByMask[mask * CivResearch.SlotCount + s] + _carry[s];
            _carry[s] = Fix64.Zero;
        }
        for (int s = 0; s < CivResearch.SlotCount; s++)
            if (_stepProject[s] >= 0) AddProgress(_stepProject[s], _stepAmount[s]);
    }

    private void AddProgress(int p, Fix64 amount)
    {
        while (true)
        {
            if (_done[p]) return;
            Fix64 cost = Cost(p);
            Fix64 progress = _progress[p] + amount;
            int lineage = _c.Lineage[p];
            if (lineage >= 0 && _kh[lineage].Raw > 0)
            {
                Fix64 room = Fix64.FromRaw(cost.Raw >> 1) - _khApplied[p]; // Know-how covers at most half the cost
                if (room.Raw > 0)
                {
                    Fix64 kh = Fix64.Min(_kh[lineage], Fix64.Min(amount, room));
                    if (kh.Raw > 0)
                    {
                        _kh[lineage] -= kh;
                        _khApplied[p] += kh;
                        _khUsed += kh;
                        progress += kh;
                    }
                }
            }
            if (progress < cost) { _progress[p] = progress; return; }
            amount = progress - cost;
            _progress[p] = cost;
            int slot = SlotOf(p);
            Complete(p);
            Cascade();
            int next = slot >= 0 ? _slots[slot] : -1;
            if (next < 0)
            {
                if (slot >= 0) _carry[slot] += amount;
                return;
            }
            p = next;
        }
    }

    private int SlotOf(int p)
    {
        for (int s = 0; s < CivResearch.SlotCount; s++) if (_slots[s] == p) return s;
        return -1;
    }

    private void Cascade()
    {
        int write = 0;
        for (int s = 0; s < CivResearch.SlotCount; s++) if (_slots[s] >= 0) _slots[write++] = _slots[s];
        for (int s = write; s < CivResearch.SlotCount; s++) _slots[s] = -1;
        Fill();
    }

    private bool Available(int p) => !_done[p] && _blockers[p] == 0;

    private void Fill()
    {
        FillSlots();
        if (!_promote) return;
        int key = _c.Catalog.KeystoneLeaving(_era);
        if (key < 0 || !Available(key) || SlotOf(key) >= 0) return;
        int displaced = _slots[CivResearch.SlotCount - 1];
        for (int s = CivResearch.SlotCount - 1; s > 0; s--) _slots[s] = _slots[s - 1];
        _slots[0] = key;
        Unmark(key);
        if (displaced >= 0) MarkAvailable(displaced);
        for (int i = 0; i < _planLength && displaced >= 0; i++)
            if (_plan[i] == displaced) { _planUsed[i] = false; _planHead = Math.Min(_planHead, i); break; }
    }

    private void FillSlots()
    {
        for (int s = 0; s < CivResearch.SlotCount; s++)
        {
            if (_slots[s] >= 0) continue;
            int pick = PickFromPlan();
            if (pick < 0) pick = PickFallback();
            if (pick < 0) return;
            _slots[s] = pick;
        }
    }

    private int PickFromPlan()
    {
        while (_planHead < _planLength && (_planUsed[_planHead] || _done[_plan[_planHead]])) _planHead++;
        for (int i = _planHead; i < _planLength; i++)
        {
            if (_planUsed[i]) continue;
            int q = _plan[i];
            if (_done[q]) { _planUsed[i] = true; continue; }
            if (!Available(q) || SlotOf(q) >= 0) continue;
            _planUsed[i] = true;
            Unmark(q);
            return q;
        }
        return -1;
    }

    private int PickFallback()
    {
        for (int w = 0; w < _avail.Length; w++)
        {
            ulong bits = _avail[w];
            if (bits == 0) continue;
            int pos = w * 64 + System.Numerics.BitOperations.TrailingZeroCount(bits);
            _avail[w] = bits & (bits - 1);
            return _fallback[_order][pos];
        }
        return -1;
    }

    private void RebuildAvailable()
    {
        int[] order = _fallback[_order];
        int words = (order.Length + 63) / 64;
        if (_avail.Length != words) _avail = new ulong[words];
        else Array.Clear(_avail);
        for (int j = 0; j < order.Length; j++)
        {
            int p = order[j];
            if (Available(p) && SlotOf(p) < 0) _avail[j >> 6] |= 1UL << (j & 63);
        }
    }

    private void MarkAvailable(int p)
    {
        int j = _rankPos[_order][p];
        if (j >= 0 && Available(p) && SlotOf(p) < 0) _avail[j >> 6] |= 1UL << (j & 63);
    }

    private void Unmark(int p)
    {
        int j = _rankPos[_order][p];
        if (j >= 0) _avail[j >> 6] &= ~(1UL << (j & 63));
    }

    /// <summary>What completing a project at a step is worth: Mastery plus the expected First, weighted.</summary>
    private Fix64 Worth(int p, int step) => Worth(p, step, ExpectedFirst(p, step));

    private Fix64 Worth(int p, int step, Fix64 first)
    {
        Fix64 legacy = Fix64.FromInt(_c.Mastery[p]) + first;
        if (_value is null) return legacy;
        Fix64 worth = legacy * _value.Weight[p] + _value.Bonus[p];
        // Tempo: every season a keystone lands before the horizon is a season of cheaper backfills, the next era's Firsts
        // in reach and the Era Boon (docs/MVP/02 §8.3, §13).
        if (_c.IsKeystone[p] && _value.KeystoneTempo.Raw != 0) worth += _value.KeystoneTempo * (_steps - step) / _sps;
        if (_value.Urgent[p].Raw != 0 && step < _value.UrgencySteps) worth += _value.Urgent[p] * (_value.UrgencySteps - step) / _value.UrgencySteps;
        return worth;
    }

    /// <summary>
    /// The First's Legacy times the chance we beat every rival to it: sure a margin ahead of the rival's believed ETA, lost a
    /// margin behind, linear between. The margin is at least the caller's (a season) and grows with how far ahead the
    /// rival's ETA lies, since a rival's pace is only known to about <see cref="ForecastValuation.EtaUncertainty"/>.
    /// </summary>
    private Fix64 ExpectedFirst(int p, int step)
    {
        if (_start.FirstBy[p] >= 0) return Fix64.Zero;
        if (_value is null) return _c.FirstPoints[p];
        int eta = _value.RivalEta[p];
        if (eta == int.MaxValue) return _c.FirstPoints[p];
        int m = Math.Max(Math.Max(1, _value.MarginSteps), (_value.EtaUncertainty * eta).FloorToInt());
        int d = eta - step;
        if (d >= m) return _c.FirstPoints[p];
        if (d <= -m) return Fix64.Zero;
        // points × (d + m) / 2m on the raw value: exact enough and no 128-bit division in the inner loop.
        return Fix64.FromRaw(checked(_c.FirstPoints[p].Raw * (d + m)) / (2L * m));
    }

    private void Complete(int p)
    {
        _bronze?.OnProjectCompleted(p);
        _done[p] = true;
        DoneAt[p] = _step;
        CompletionOrder[_completed++] = p;
        int slot = SlotOf(p);
        if (slot >= 0) _slots[slot] = -1;
        Unmark(p);
        if (_c.Lineage[p] >= 0 && --_lineageLeft[_c.Lineage[p]] == 0) _genDirty = true;
        int step = Math.Min(_step, _steps);
        _mastery += Fix64.FromInt(_c.Mastery[p]);
        Fix64 first = ExpectedFirst(p, step);
        _firsts += first;
        _utility += Worth(p, step, first) * _timeWeight[step];

        bool pioneer = _start.FirstBy[p] < 0;
        if (_c.CommonsPct[p].Raw != 0)
        {
            Fix64 pct = _c.CommonsPct[p];
            if (pioneer) pct = pct + pct * PioneerBonus;
            _lmC = _lmC * (Fix64.One + pct);
            _insightDirty = true;
        }
        if (_c.ElderMult[p] != Fix64.One)
        {
            Fix64 extra = _c.ElderMult[p] - Fix64.One;
            if (pioneer) extra = extra + extra * PioneerBonus;
            _lmE = _lmE * (Fix64.One + extra);
            _insightDirty = true;
        }
        if (_c.RoleOf[p] >= 0) { _roles |= 1UL << _c.RoleOf[p]; _insightDirty = true; }
        if (p == _c.Soc03) { _soc03 = true; _insightDirty = true; }
        _gainAll = _gainAll * _c.KnowHowGainAll[p];
        if (_c.AmbientLineageWork[p] is { } ambient)
        {
            _extraWork += _c.AmbientWork[p];
            for (int l = 0; l < ambient.Length; l++) _extraLineage[l] += ambient[l];
            _genDirty = true;
        }
        if (_c.KnowHowGainLineage[p] >= 0)
        {
            _lineageGain[_c.KnowHowGainLineage[p]] = _lineageGain[_c.KnowHowGainLineage[p]] * _c.KnowHowGainLineageMult[p];
            _genDirty = true;
        }

        if (!_seenOpen && (_c.IsKeystone[p] || _c.Reveals[p]))
        {
            // Resources beyond today's sight come into view as routine reach grows with the era (6 / 9 / 12 tiles), with
            // WAY-01's trails, or with INQ-03's ore lore (docs/MVP/07c §7a): the mean-field assumption is that the gate opens.
            _seenOpen = true;
            foreach (int q in _c.Numeric)
                if (_start.SeenBlocked[q] && --_blockers[q] == 0) MarkAvailable(q);
        }
        foreach (int d in _c.Dependents[p])
            if (--_blockers[d] == 0) MarkAvailable(d);
        foreach (int d in _c.AnyDependents[p])
            if (!_anySat[d]) { _anySat[d] = true; if (--_blockers[d] == 0) MarkAvailable(d); }
        if (_c.IsKeystone[p])
        {
            int enters = _c.Enters[p];
            for (int e = _era + 1; e <= enters && e < ResearchCatalog.EraCount; e++)
                foreach (int q in _c.ByEra[e]) _blockers[q]--;
            _era = Math.Max(_era, enters);
            if (!_start.BoonIsBeacon) { _boon = _boon * (Fix64.One + _c.EraBoon); _insightDirty = true; }
            _order = Math.Min(_era - _start.Era, _fallback.Length - 1);
            RebuildAvailable();
        }
        else
        {
            int era = _c.Era[p];
            _doneByEra[era]++;
            int key = _c.KeystoneByBreadthEra[era];
            if (key >= 0 && !_done[key] && _doneByEra[era] == _c.BreadthMin[key] && --_blockers[key] == 0) MarkAvailable(key);
        }
    }

    /// <summary>Season-end Know-how: each lineage's observed gain, scaled by Commons and Know-how gain since (docs/MVP/02 §9).</summary>
    private void GenerateKnowHow(Fix64 commons)
    {
        if (_gen0.Raw == 0) return;
        if (_genDirty) RebuildGeneration();
        Fix64 ratio = commons * _invCommons0;
        if (_gainAll != _gainAll0) ratio = ratio * _gainAll / _gainAll0;
        for (int i = 0; i < _genCount; i++)
        {
            int l = _genList[i];
            _kh[l] += _genBase[l] * ratio;
        }
    }

    /// <summary>
    /// Know-how per season per lineage at today's Commons: the observed gain, with any standing practice started since
    /// taking its share of the people's work, and of the Know-how, into its own lineages.
    /// </summary>
    private void RebuildGeneration()
    {
        _genDirty = false;
        _genCount = 0;
        Fix64 keep = Fix64.One, perWork = Fix64.Zero;
        if (_extraWork.Raw != 0)
        {
            keep = _work0 / (_work0 + _extraWork);
            perWork = _gen0 / (_work0 + _extraWork);
        }
        for (int l = 0; l < _kh.Length; l++)
        {
            if (_lineageLeft[l] == 0) { _genBase[l] = Fix64.Zero; continue; } // nothing left for it to feed
            Fix64 g = _extraWork.Raw == 0 ? _start.KnowHowGen[l] : _start.KnowHowGen[l] * keep + _extraLineage[l] * perWork;
            if (g.Raw != 0 && _lineageGain[l] != _lineageGain0[l]) g = g * _lineageGain[l] / _lineageGain0[l];
            _genBase[l] = g;
            if (g.Raw != 0) _genList[_genCount++] = l;
        }
    }

    /// <summary>True when the project could be put in a slot now in the last run's end state.</summary>
    public bool IsAvailableAtEnd(int p) => Available(p);
}
