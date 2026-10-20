using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Ai.Forecast;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;
using CivSim.Sim.Research;

namespace CivSim.Sim.Ai;

/// <summary>
/// The AI Spark (docs/MVP/14 §4): plans research with the forecast model. Each decision it projects the civilization
/// (<see cref="ResearchForecast"/>), models the Firsts race against a Council-like rival frontier built from public
/// knowledge (the Chronicle of Firsts and announced eras), and beam-searches the order of the next projects, starting from
/// the Council's prior. The best sequence becomes the slots (most valuable first, so Primary gets the time-critical
/// project) and the queue, written with the same commands a player has, so slots never idle and the Council only fills a
/// slot the plan leaves empty. Budgets are counted in rollouts, never in time (14 §12).
/// </summary>
public sealed class ResearchSpark : IResearchSpark
{
    private const ulong StreamPart = 0x535041524BUL; // "SPARK": the Settler's and Chieftain's prior picks
    private static readonly Fix64 Third = Fix64.FromRatio(1, 3);

    private readonly AiSpec _spec;
    private readonly DifficultyProfile _profile;
    private readonly KnowHowTracker _tracker = new();
    private int _lastDecision = -1;
    private readonly int[] _lastSlots = { -1, -1, -1 };
    /// <summary>Season each civilization was first seen in each era (civ × era), -1 if not yet: the public keystone record.</summary>
    private int[] _eraSeason = Array.Empty<int>();

    // Derived per catalog and rebuilt each decision; never part of the saved state.
    private ForecastCatalog? _fc;
    private ForecastStart _start = null!, _front = null!;
    private ResearchForecast _forecast = null!, _frontier = null!;
    private ForecastValuation _value = null!;
    private Fix64[] _prior = Array.Empty<Fix64>();
    private Fix64[] _rank = Array.Empty<Fix64>();
    private ProjectState[] _state = Array.Empty<ProjectState>();
    private string[] _lineageCode = Array.Empty<string>();
    private readonly List<int> _candidates = new();
    private readonly List<int> _plan = new();
    private readonly List<int> _current = new();
    private int[] _depth = Array.Empty<int>();
    private readonly List<int> _critical = new();

    public ResearchSpark(AiSpec spec, DifficultyProfile profile)
    {
        _spec = spec;
        _profile = profile;
    }

    /// <summary>Forecast rollouts run so far (the planner's cost, counted in iterations).</summary>
    public long Rollouts => _fc is null ? 0 : _forecast.Rollouts + _frontier.Rollouts;
    public int Decisions { get; private set; }
    /// <summary>The last plan written (slots, then queue), for tests and the debug overlay.</summary>
    public IReadOnlyList<int> LastPlan => _plan;
    /// <summary>The forecast of the last decision's chosen plan.</summary>
    public ForecastResult LastForecast { get; private set; }
    public ForecastResult LastBaseline { get; private set; }

    public void Decide(AiContext ctx)
    {
        Ensure(ctx.Catalog);
        ForecastCatalog fc = _fc!;
        AiResearchPort port = ctx.Research;
        var readout = new PortReadout(port);
        _tracker.Observe(readout, ctx.Season);
        KeepLeaning(ctx);

        _start.CaptureResearch(readout, ctx.Civ, ctx.Spec.Archetype);
        _start.Season = ctx.Season;
        _start.People = _start.EstimatePeople(ctx.Own.Population, ctx.Own.Adults, ctx.Own.Settlements.Count);
        _tracker.Gain.CopyTo(_start.KnowHowGen);
        for (int p = 0; p < fc.Count; p++) _state[p] = fc.IsNumeric[p] ? port.State(p) : ProjectState.Hidden;
        int horizon = Math.Max(SparkTuning.MinHorizonSeasons, _profile.HorizonEras * ForecastTuning.SeasonsPerEra);
        _forecast.Prepare(_start, horizon);

        BuildRivalFrontier(ctx, horizon);
        BuildValuation(ctx);
        BuildCandidates(port);
        Search(ctx);
        Write(ctx);
        _lastDecision = ctx.Season;
        Decisions++;
    }

    private void Ensure(ResearchCatalog catalog)
    {
        if (_fc is not null && ReferenceEquals(_fc.Catalog, catalog)) return;
        _fc = new ForecastCatalog(catalog);
        _start = new ForecastStart(_fc);
        _front = new ForecastStart(_fc);
        _forecast = new ResearchForecast(_fc, ForecastTuning.PlannerStepsPerSeason);
        _frontier = new ResearchForecast(_fc, ForecastTuning.PlannerStepsPerSeason);
        _value = new ForecastValuation(_fc.Count);
        _prior = new Fix64[_fc.Count];
        _rank = new Fix64[_fc.Count];
        _state = new ProjectState[_fc.Count];
        _depth = new int[_fc.Count];
        _lineageCode = new string[_fc.Count];
        for (int p = 0; p < _fc.Count; p++) _lineageCode[p] = _fc.Lineage[p] >= 0 ? catalog.LineageCodes[_fc.Lineage[p]] : "";
    }

    /// <summary>The archetype's Leaning (14 §9.1, the archetypes registry): style, and which Era Boon the civilization takes.</summary>
    private static void KeepLeaning(AiContext ctx)
    {
        (int craft, int kin, int reach) = MatchModel.Leaning(ctx.Catalog.Pack, ctx.Spec.Archetype);
        if (ctx.Research.Leaning(0) != craft) ctx.Research.SetLeaning(0, craft);
        if (ctx.Research.Leaning(1) != kin) ctx.Research.SetLeaning(1, kin);
        if (ctx.Research.Leaning(2) != reach) ctx.Research.SetLeaning(2, reach);
    }

    // ---- the Firsts race ------------------------------------------------------------------

    /// <summary>
    /// When the fastest rival is believed to complete each project (14 §4.4). Firsts are public, so a project nobody has
    /// claimed is undone everywhere. The rival is modeled as a Council civilization (the Council's ranking, a ready keystone
    /// pulled into Primary) starting from our own state, or, when a rival has announced a later era, from everything anyone
    /// has done first; it researches at the pace of the fastest rival, read from the public keystone announcements: how
    /// long it took to reach its era against how long we took (or are forecast to take). A Settler models no one: rivals as
    /// average, our state and our pace.
    /// </summary>
    private void BuildRivalFrontier(AiContext ctx, int horizon)
    {
        ForecastCatalog fc = _fc!;
        ForecastStart s = _start, f = _front;
        bool model = _profile.Opponent != OpponentModel.None;
        RecordEras(ctx);
        int rivalEra = s.Era;
        Fix64 pace = Fix64.One;
        if (model)
        {
            ForecastResult mine = _forecast.Run(ReadOnlySpan<int>.Empty, null, promoteKeystone: true);
            foreach (RivalBelief r in ctx.Belief.Rivals)
            {
                if (r.Civ == ctx.Civ) continue;
                int era = Math.Min(r.Era, ResearchCatalog.EraCount - 1);
                rivalEra = Math.Max(rivalEra, era);
                if (era < 1) continue;
                int theirs = EraSeason(r.Civ, era);
                int ours = EraSeason(ctx.Civ, era);
                if (ours < 0)
                {
                    int key = era == 1 ? mine.Key1 : era == 2 ? mine.Key2 : era == 3 ? mine.Key3 : -1;
                    ours = key >= 0 ? s.Season + key / _forecast.StepsPerSeason : s.Season + horizon;
                }
                if (theirs <= 0) continue;
                Fix64 ratio = Fix64.FromRatio(Math.Max(1, ours), theirs);
                if (ratio > pace) pace = ratio;
            }
            pace = Fix64.Clamp(pace, SparkTuning.RivalPaceMin, SparkTuning.RivalPaceMax);
        }
        bool union = model && rivalEra > s.Era;
        int frontEra = union ? rivalEra : s.Era;
        f.Civ = -1;
        f.Season = s.Season;
        f.Era = frontEra;
        Array.Clear(f.DoneByEra);
        for (int p = 0; p < fc.Count; p++)
        {
            bool done = s.Done[p] || (union && s.FirstBy[p] >= 0 && fc.IsNumeric[p]);
            f.Done[p] = done;
            f.FirstBy[p] = s.FirstBy[p];
            f.SeenBlocked[p] = s.SeenBlocked[p];
            f.Progress[p] = done ? Fix64.Zero : s.Progress[p];
            f.KnowHowApplied[p] = done ? Fix64.Zero : s.KnowHowApplied[p];
            Fix64 cost = s.Cost[p];
            if (!done && fc.IsNumeric[p] && frontEra > s.Era)
            {
                int extra = Math.Max(0, frontEra - fc.Era[p]) - Math.Max(0, s.Era - fc.Era[p]);
                if (extra > 0) cost = cost * FixMath.PowInt(fc.EraLag, extra);
            }
            f.Cost[p] = cost;
            if (done && fc.IsNumeric[p] && !fc.IsKeystone[p]) f.DoneByEra[fc.Era[p]]++;
        }
        Array.Copy(s.KnowHow, f.KnowHow, f.KnowHow.Length);
        for (int l = 0; l < f.KnowHowGen.Length; l++) f.KnowHowGen[l] = s.KnowHowGen[l] * pace;
        Array.Fill(f.Slots, -1);
        f.Queue.Clear();
        // Rivals are modeled as Balanced Council civilizations.
        f.Leaning[0] = 34; f.Leaning[1] = 33; f.Leaning[2] = 33;
        f.InsightPerYear = s.InsightPerYear * pace;
        f.CommonsPerYear = s.CommonsPerYear * pace;
        f.Scale = s.Scale * pace;
        f.LmCommons = s.LmCommons; f.LmElder = s.LmElder; f.RolesScale = s.RolesScale; f.People = s.People;
        RivalPace = pace;
        _frontier.Prepare(f, horizon);
        _frontier.Run(ReadOnlySpan<int>.Empty, null, null, keepLiveSlots: false, promoteKeystone: true);
        int[] eta = _value.RivalEta;
        for (int p = 0; p < fc.Count; p++) eta[p] = _frontier.DoneAt[p] >= 0 ? _frontier.DoneAt[p] : int.MaxValue;
    }

    /// <summary>The fastest rival's research pace relative to ours, as last estimated (1 when unknown).</summary>
    public Fix64 RivalPace { get; private set; } = Fix64.One;

    /// <summary>Notes the season each civilization was first seen in each era (public: keystones are announced).</summary>
    private void RecordEras(AiContext ctx)
    {
        int civs = ctx.Belief.Rivals.Length;
        if (_eraSeason.Length != civs * ResearchCatalog.EraCount)
        {
            _eraSeason = new int[civs * ResearchCatalog.EraCount];
            Array.Fill(_eraSeason, -1);
        }
        for (int c = 0; c < civs; c++)
        {
            int era = c == ctx.Civ ? ctx.Research.Era : ctx.Belief.Rivals[c].Era;
            for (int e = 1; e <= Math.Min(era, ResearchCatalog.EraCount - 1); e++)
                if (_eraSeason[c * ResearchCatalog.EraCount + e] < 0) _eraSeason[c * ResearchCatalog.EraCount + e] = ctx.Season;
        }
    }

    private int EraSeason(int civ, int era)
    {
        int i = civ * ResearchCatalog.EraCount + era;
        return i < _eraSeason.Length ? _eraSeason[i] : -1;
    }

    // ---- what a project is worth ----------------------------------------------------------

    private void BuildValuation(AiContext ctx)
    {
        ForecastCatalog fc = _fc!;
        StrategicIntent intent = ctx.Intent;
        bool[]? closure = fc.Catalog.ClosureOf(_start.Era);
        Fix64 defense = Fix64.Clamp(intent.DefenseUrgency, Fix64.Zero, Fix64.One) * SparkTuning.DefenseBonus;
        Fix64 attack = Fix64.Clamp(intent.AttackReadiness, Fix64.Zero, Fix64.One) * SparkTuning.AttackBonus;
        Fix64 frontier = Fix64.Clamp(intent.FrontierValue, Fix64.Zero, Fix64.One) * SparkTuning.FrontierBonus;
        _value.MarginSteps = SparkTuning.FirstMarginSeasons * _forecast.StepsPerSeason;
        _value.EtaUncertainty = SparkTuning.RivalEtaUncertainty;
        _value.KeystoneTempo = SparkTuning.KeystoneTempo;
        foreach (int p in fc.Numeric)
        {
            Theme theme = fc.Theme[p];
            _value.Weight[p] = theme == Theme.Keystone ? Fix64.One
                : Fix64.One + SparkTuning.StyleWeight * (Fix64.FromRatio(_start.Leaning[(int)theme], 100) - Third);
            Fix64 bonus = SparkTuning.HubBonus * fc.HubWeight[p];
            if (closure is not null && closure[p] && !_start.Done[p]) bonus += SparkTuning.ClosureBonus;
            string code = _lineageCode[p];
            if (code is "DEF" or "WAR") bonus += defense;
            if (code == "WAR") bonus += attack;
            if (code is "TER" or "WAY" or "SOC") bonus += frontier;
            if (IsSignature(_spec.Archetype, code)) bonus += SparkTuning.SignatureBonus;
            _value.Bonus[p] = bonus;
        }
    }

    /// <summary>The lineages that carry an archetype's signature (docs/MVP/14 §9.1).</summary>
    private static bool IsSignature(EconomyArchetype archetype, string code) => archetype switch
    {
        EconomyArchetype.Warlord => code is "WAR" or "DEF",
        EconomyArchetype.Sage => code is "LRN" or "BEL",
        EconomyArchetype.Builder => code is "STN" or "BLD",
        EconomyArchetype.Grower => code is "SOC" or "FOD",
        EconomyArchetype.Wanderer => code is "WAY" or "SEA",
        _ => false,
    };

    // ---- search ---------------------------------------------------------------------------

    /// <summary>Projects the plan may name now (Glimpsed or better: the queue refuses Hidden ones), ordered by the prior.</summary>
    private void BuildCandidates(AiResearchPort port)
    {
        ForecastCatalog fc = _fc!;
        port.CouncilPrior(_prior);
        Fix64 maxPrior = Fix64.Epsilon, maxHeuristic = Fix64.Epsilon;
        _candidates.Clear();
        foreach (int p in fc.Numeric)
        {
            _rank[p] = Fix64.Zero;
            if (_start.Done[p] || _state[p] < ProjectState.Glimpsed || _state[p] > ProjectState.Slotted) continue;
            _candidates.Add(p);
            Fix64 cost = Fix64.Max(_start.Cost[p], Fix64.One);
            Fix64 first = _start.FirstBy[p] < 0 ? fc.FirstPoints[p] : Fix64.Zero;
            Fix64 h = ((Fix64.FromInt(fc.Mastery[p]) + first) * _value.Weight[p] + _value.Bonus[p] + _forecast.InsightGain(p, _start) * 100) / cost;
            _rank[p] = h;
            if (h > maxHeuristic) maxHeuristic = h;
            if (_prior[p] > maxPrior) maxPrior = _prior[p];
        }
        foreach (int p in _candidates) _rank[p] = _rank[p] / maxHeuristic + SparkTuning.PriorWeight * _prior[p] / maxPrior;
        // Stable insertion sort, descending rank; ties keep ascending id.
        for (int a = 1; a < _candidates.Count; a++)
            for (int b = a; b > 0 && _rank[_candidates[b]] > _rank[_candidates[b - 1]]; b--)
                (_candidates[b], _candidates[b - 1]) = (_candidates[b - 1], _candidates[b]);
    }

    /// <summary>Whether a project can follow a partial plan: available now, or one step away with the missing step in it.</summary>
    private bool CanFollow(int p, int[] seq, int length)
    {
        ForecastCatalog fc = _fc!;
        for (int i = 0; i < length; i++) if (seq[i] == p) return false;
        if (_state[p] is ProjectState.Available or ProjectState.Slotted) return true;
        foreach (int r in fc.Requires[p]) if (!_start.Done[r] && !Contains(seq, length, r)) return false;
        if (fc.RequiresAny[p].Length > 0)
        {
            bool any = false;
            foreach (int r in fc.RequiresAny[p]) if (_start.Done[r] || Contains(seq, length, r)) { any = true; break; }
            if (!any) return false;
        }
        if (fc.IsKeystone[p])
        {
            int era = fc.BreadthEra[p], count = _start.DoneByEra[era];
            for (int i = 0; i < length; i++) if (fc.Era[seq[i]] == era && !fc.IsKeystone[seq[i]]) count++;
            if (count < fc.BreadthMin[p]) return false;
        }
        return !_start.SeenBlocked[p];
    }

    private static bool Contains(int[] seq, int length, int p)
    {
        for (int i = 0; i < length; i++) if (seq[i] == p) return true;
        return false;
    }

    private readonly struct Node
    {
        public Node(int[] seq, int length, Fix64 utility) { Seq = seq; Length = length; Utility = utility; }
        public int[] Seq { get; }
        public int Length { get; }
        public Fix64 Utility { get; }
    }

    /// <summary>
    /// Beam search over the order of the next <see cref="SparkTuning.SearchDepth"/> projects (14 §4.2), each partial plan
    /// valued by a rollout in which the Council fills whatever the plan leaves open. Width from the difficulty.
    /// </summary>
    private void Search(AiContext ctx)
    {
        ForecastCatalog fc = _fc!;
        int width = Math.Max(1, _profile.BeamWidth);
        int perNode = Math.Min(SparkTuning.MaxCandidates, width / 2 + SparkTuning.ExtraCandidates);
        LastBaseline = _forecast.Run(ReadOnlySpan<int>.Empty, _value, promoteKeystone: true);
        var best = new Node(Array.Empty<int>(), 0, LastBaseline.Utility);
        // Keystone routing (14 §4.2 step 4, §4.3): the most time-critical projects, the head of the next keystone's critical
        // path with its forced backfills, hold Primary and Secondary; the search orders everything after them.
        CriticalProjects();
        int firstDepth = 1;
        if (_critical.Count > 0)
        {
            int[] head = _critical.ToArray();
            best = new Node(head, head.Length, _forecast.Run(head, _value, promoteKeystone: true).Utility);
            firstDepth = head.Length + 1;
        }
        var beam = new List<Node> { best };
        var children = new List<Node>();
        for (int depth = firstDepth; depth <= SparkTuning.SearchDepth; depth++)
        {
            children.Clear();
            foreach (Node node in beam)
            {
                int tried = 0;
                foreach (int c in _candidates)
                {
                    if (tried == perNode) break;
                    if (!CanFollow(c, node.Seq, node.Length)) continue;
                    var seq = new int[depth];
                    Array.Copy(node.Seq, seq, node.Length);
                    seq[depth - 1] = c;
                    ForecastResult r = _forecast.Run(seq, _value, promoteKeystone: true);
                    children.Add(new Node(seq, depth, r.Utility));
                    tried++;
                }
            }
            if (children.Count == 0) break;
            // Stable: equal utilities keep generation order (beam order, then prior order).
            for (int a = 1; a < children.Count; a++)
                for (int b = a; b > 0 && children[b].Utility > children[b - 1].Utility; b--)
                    (children[b], children[b - 1]) = (children[b - 1], children[b]);
            beam.Clear();
            for (int i = 0; i < children.Count && i < width; i++) beam.Add(children[i]);
            if (beam[0].Utility > best.Utility) best = beam[0];
        }

        // Commitment (14 §3.3, against dithering): the plan in force is kept unless the search beats it by a margin. Swapping
        // costs no progress, but a plan reshuffled every season spreads Insight thin and finishes everything later.
        _current.Clear();
        for (int sl = 0; sl < CivResearch.SlotCount; sl++)
        {
            int q = ctx.Research.Slot(sl);
            if (q >= 0 && !_start.Done[q]) _current.Add(q);
        }
        foreach (int q in ctx.Research.Queue)
            if (!_start.Done[q] && !_current.Contains(q) && _state[q] >= ProjectState.Glimpsed) _current.Add(q);
        Fix64 currentUtility = Fix64.MinValue;
        if (_current.Count > 0)
            currentUtility = _forecast.Run(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_current), _value, promoteKeystone: true).Utility;
        Fix64 margin = Fix64.Max(SparkTuning.CommitmentFloor, Fix64.Abs(currentUtility == Fix64.MinValue ? Fix64.Zero : currentUtility) * SparkTuning.CommitmentShare);
        _plan.Clear();
        if (currentUtility != Fix64.MinValue && best.Utility < currentUtility + margin)
            foreach (int q in _current) _plan.Add(q);
        else
            for (int i = 0; i < best.Length; i++) _plan.Add(best.Seq[i]);
        for (int i = _critical.Count - 1; i >= 0; i--) { _plan.Remove(_critical[i]); _plan.Insert(0, _critical[i]); }
        ApplyPriorPicks(ctx);
        LastForecast = _forecast.Run(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_plan), _value, promoteKeystone: true);
        // The rest of the plan is what the best rollout researched next: the queue keeps the slots busy until the next decision.
        for (int i = 0; i < _forecast.CompletedCount && _plan.Count < SparkTuning.PlanLength; i++)
        {
            int p = _forecast.CompletionOrder[i];
            if (_plan.Contains(p) || _start.Done[p] || _state[p] < ProjectState.Glimpsed || _state[p] > ProjectState.Slotted) continue;
            _plan.Add(p);
        }
        // Keystone routing (14 §4.3, blunder "keystone stall"): a ready keystone always takes Primary.
        int key = fc.Catalog.KeystoneLeaving(_start.Era);
        if (key >= 0 && _state[key] is ProjectState.Available or ProjectState.Slotted)
        {
            _plan.Remove(key);
            _plan.Insert(0, key);
        }
    }

    /// <summary>
    /// The available projects with the longest chains still ahead of them inside the next keystone's closure (backfills
    /// included, 07 F8): delaying them delays the keystone one for one. The keystone itself once ready. Up to
    /// <see cref="SparkTuning.ClosureSlots"/>; none when the closure has nothing available (its next steps are already in
    /// the slots' hands, or only breadth is missing, which any project of the era serves).
    /// </summary>
    private void CriticalProjects()
    {
        _critical.Clear();
        ForecastCatalog fc = _fc!;
        int key = fc.Catalog.KeystoneLeaving(_start.Era);
        bool[]? closure = fc.Catalog.ClosureOf(_start.Era);
        if (key < 0 || closure is null || _start.Done[key]) return;
        if (_state[key] is ProjectState.Available or ProjectState.Slotted) { _critical.Add(key); return; }
        int[] depth = _depth;
        Array.Clear(depth);
        for (int p = 0; p < fc.Count; p++) if (closure[p] && !_start.Done[p]) depth[p] = 1;
        for (int pass = 0; pass < 40; pass++)
        {
            bool changed = false;
            for (int q = 0; q < fc.Count; q++)
            {
                if (depth[q] == 0) continue;
                foreach (int r in fc.Requires[q])
                    if (depth[r] > 0 && depth[r] < depth[q] + 1) { depth[r] = depth[q] + 1; changed = true; }
                foreach (int r in fc.RequiresAny[q])
                    if (closure[r] && depth[r] > 0 && depth[r] < depth[q] + 1) { depth[r] = depth[q] + 1; changed = true; }
            }
            if (!changed) break;
        }
        while (_critical.Count < SparkTuning.ClosureSlots)
        {
            int best = -1;
            for (int p = 0; p < fc.Count; p++)
            {
                if (depth[p] == 0 || p == key || _critical.Contains(p) || _state[p] is not (ProjectState.Available or ProjectState.Slotted)) continue;
                // Longest chain first; among equals the one with the most progress (keeps the work in hand), then the lowest id.
                if (best < 0 || depth[p] > depth[best] || (depth[p] == depth[best] && _start.Progress[p] > _start.Progress[best])) best = p;
            }
            if (best < 0) break;
            _critical.Add(best);
        }
    }

    /// <summary>
    /// The lower levels' deliberate imperfection (14 §9.2): a share of the explicitly ordered picks comes straight from the
    /// Council's prior. Drawn from the civilization's own deterministic stream, never from a clock.
    /// </summary>
    private void ApplyPriorPicks(AiContext ctx)
    {
        int percent = _profile.CouncilPriorPercent;
        if (percent <= 0 || _plan.Count == 0) return;
        // The civilization's own stream for this part and season (rule D3). AiContext.Stream would do, but its civ mixing
        // (civ × 0x9E3779B97F4A7C15 outside an unchecked block) overflows for civilization 2 and up; reported to the integrator.
        Pcg32 stream = Pcg32.ForStream(ctx.MatchSeed, StreamPart, (ulong)ctx.Civ, ctx.Season);
        for (int i = 0; i < _plan.Count; i++)
        {
            if (stream.NextBelow(100) >= (uint)percent) continue;
            int pick = -1;
            Fix64 bestPrior = Fix64.Zero;
            foreach (int p in _fc!.Numeric)
                if (_prior[p] > bestPrior && !_plan.Contains(p)) { bestPrior = _prior[p]; pick = p; }
            if (pick >= 0) _plan[i] = pick;
        }
    }

    // ---- orders ---------------------------------------------------------------------------

    private void Write(AiContext ctx)
    {
        AiResearchPort port = ctx.Research;
        Span<int> target = stackalloc int[CivResearch.SlotCount];
        target.Fill(-1);
        int filled = 0;
        foreach (int p in _plan)
        {
            if (filled == CivResearch.SlotCount) break;
            if (_state[p] is ProjectState.Available or ProjectState.Slotted) target[filled++] = p;
        }
        for (int s = 0; s < filled; s++)
            if (port.Slot(s) != target[s]) port.SetSlot(s, target[s]);
        port.ClearQueue();
        // The keystone waits at the head of the queue: the moment its last prerequisite lands, the cascade slots it, and the
        // next decision moves it to Primary (the Council pulls it at once; the Spark acts only at season ends).
        int keystone = _fc!.Catalog.KeystoneLeaving(_start.Era);
        if (keystone >= 0 && !_start.Done[keystone] && _state[keystone] == ProjectState.Glimpsed) port.Enqueue(keystone);
        foreach (int p in _plan)
        {
            if (port.Queue.Count >= 8) break;
            bool slotted = false;
            for (int s = 0; s < filled; s++) if (target[s] == p) slotted = true;
            if (!slotted) port.Enqueue(p);
        }
        for (int s = 0; s < CivResearch.SlotCount; s++)
        {
            int p = port.Slot(s);
            if (p == _lastSlots[s]) continue;
            _lastSlots[s] = p;
            if (p >= 0) ctx.Note("Spark", s == 0 ? "Primary" : s == 1 ? "Secondary" : "Tertiary", p, s, LastForecast.Utility - LastBaseline.Utility, Reason(p));
        }
    }

    /// <summary>The top reason a project was chosen (docs/MVP/14 §11).</summary>
    private string Reason(int p)
    {
        ForecastCatalog fc = _fc!;
        bool[]? closure = fc.Catalog.ClosureOf(_start.Era);
        if (fc.IsKeystone[p] || (closure is not null && closure[p])) return "keystone path";
        bool military = _lineageCode[p] is "DEF" or "WAR";
        if (military && _value.Bonus[p] > SparkTuning.HubBonus * fc.HubWeight[p] + SparkTuning.SignatureBonus + SparkTuning.ClosureBonus)
            return "threat: defense first";
        if (_forecast.InsightGain(p, _start).Raw > 0) return "insight gain";
        if (_start.FirstBy[p] < 0)
        {
            int mine = _forecast.DoneAt[p];
            int eta = _value.RivalEta[p];
            if (mine >= 0 && (eta == int.MaxValue || eta - mine >= _value.MarginSteps)) return "First within reach";
        }
        if (fc.Lineage[p] >= 0 && _start.KnowHow[fc.Lineage[p]] * 4 >= _start.Cost[p]) return "know-how";
        if (_start.Cost[p] < fc.Catalog.Projects[p].Cost) return "diffusion discount";
        return "best forecast";
    }

    // ---- saved state ----------------------------------------------------------------------

    public void Hash(ref StateHasher h)
    {
        h.Add(_lastDecision);
        h.Add(_lastSlots);
        h.Add(_eraSeason);
        _tracker.Hash(ref h);
    }

    public void Write(BinaryWriter w)
    {
        w.Write(_lastDecision);
        foreach (int s in _lastSlots) w.Write(s);
        w.Write(_eraSeason.Length);
        foreach (int e in _eraSeason) w.Write(e);
        _tracker.Write(w);
    }

    public void Read(BinaryReader r)
    {
        _lastDecision = r.ReadInt32();
        for (int i = 0; i < _lastSlots.Length; i++) _lastSlots[i] = r.ReadInt32();
        _eraSeason = new int[r.ReadInt32()];
        for (int i = 0; i < _eraSeason.Length; i++) _eraSeason[i] = r.ReadInt32();
        _tracker.Read(r);
    }
}
