using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Research;

public sealed partial class ResearchSystem
{
    private const int GroupNone = 0, GroupFamine = 1, GroupThreat = 2, GroupDisease = 3, GroupCohesion = 4;
    private static readonly Fix64 UrgencyWeight = Fix64.FromInt(2);
    private static readonly Fix64 KeystonePathBoost = Fix64.FromInt(3);
    private static readonly Fix64 KeystoneReadyBoost = Fix64.FromInt(20);
    private static readonly Fix64 BreadthBoost = Fix64.FromInt(2);
    private static readonly Fix64 InsightGainWeight = Fix64.FromInt(16);
    private static readonly Fix64 MinLeaning = Fix64.FromRatio(1, 100);

    private static int UrgencyGroupOf(string lineageCode) => lineageCode switch
    {
        "FOR" or "HNT" or "FOD" or "AGR" or "HUS" or "SEA" => GroupFamine,
        "DEF" or "WAR" or "DIP" => GroupThreat,
        "MED" => GroupDisease,
        "BEL" or "GOV" or "SOC" or "ART" => GroupCohesion,
        _ => GroupNone,
    };

    /// <summary>What the Council needs to know about the civilization to score any project.</summary>
    private sealed class CouncilContext
    {
        public UrgencySignals Signals;
        public bool[]? Closure;
        public int Keystone = -1;
        public bool BreadthShort;
        public bool ClosureOpen;
        /// <summary>Remaining chain length through each unfinished closure project (the critical path).</summary>
        public int[] Depth = Array.Empty<int>();
        public InsightInputs Inputs;
    }

    private CouncilContext BuildCouncilContext(int c)
    {
        CivResearch civ = _civs[c];
        // Past the last researchable keystone (KEY-4's Iron threshold) there is no keystone path to chase: the Council
        // works through the leftovers on cost, Leaning and urgency alone.
        int keystone = _catalog.KeystoneLeaving(civ.Era);
        bool chase = keystone >= 0 && _catalog.Projects[keystone].Numeric && civ.Era <= LastResearchEra;
        var ctx = new CouncilContext
        {
            Signals = _source.GetSignals(c),
            Closure = chase ? _catalog.ClosureOf(civ.Era) : null,
            Keystone = chase ? keystone : -1,
            Inputs = _source.GetInsightInputs(c),
        };
        ctx.BreadthShort = ctx.Keystone >= 0 &&
            civ.DoneByEra[_catalog.Projects[ctx.Keystone].BreadthEra] < _catalog.Projects[ctx.Keystone].BreadthMin;
        if (ctx.Closure is null) return ctx;
        ctx.Depth = new int[_catalog.Count];
        for (int p = 0; p < ctx.Closure.Length; p++)
        {
            if (!ctx.Closure[p] || civ.Done[p]) continue;
            ctx.Depth[p] = 1;
            if (p != ctx.Keystone) ctx.ClosureOpen = true;
        }
        // Relax longest-chain lengths from the keystone down through unfinished prerequisites.
        for (int pass = 0; pass < 40; pass++)
        {
            bool changed = false;
            for (int q = 0; q < ctx.Closure.Length; q++)
            {
                if (ctx.Depth[q] == 0) continue;
                ProjectDef def = _catalog.Projects[q];
                foreach (int r in def.Requires)
                    if (ctx.Depth[r] > 0 && ctx.Depth[r] < ctx.Depth[q] + 1) { ctx.Depth[r] = ctx.Depth[q] + 1; changed = true; }
                if (def.RequiresAny.Length > 0)
                {
                    int chosen = def.RequiresAny[0];
                    foreach (int alt in def.RequiresAny)
                        if (ctx.Closure[alt]) { chosen = alt; break; }
                    if (ctx.Depth[chosen] > 0 && ctx.Depth[chosen] < ctx.Depth[q] + 1) { ctx.Depth[chosen] = ctx.Depth[q] + 1; changed = true; }
                }
            }
            if (!changed) break;
        }
        return ctx;
    }

    /// <summary>
    /// How much a project would raise this civilization's Insight, as a fraction of today's (an Opportunity the Elders
    /// weigh: wiser elders suggest what makes the band wiser). Roles count one holder per settlement.
    /// </summary>
    private Fix64 InsightGain(ProjectDef def, CivResearch civ, in InsightInputs inputs)
    {
        if (civ.InsightPerYear.Raw <= 0) return Fix64.Zero;
        Fix64 gain = Fix64.Zero;
        foreach (EffectDef e in def.Effects)
        {
            switch (e.Kind)
            {
                case EffectKind.Role when e.HasValue && e.Value.Raw > 0:
                    gain += e.Value * Math.Max(1, inputs.Settlements) / civ.InsightPerYear;
                    break;
                case EffectKind.Mod when e.Op == "pct" && e.Stat == "commons_insight":
                    gain += e.Value;
                    break;
                case EffectKind.Mod when e.Op == "mult" && e.Stat == "elder_insight":
                    gain += (e.Value - Fix64.One) / 3;
                    break;
            }
        }
        if (def.Index == _soc03 && !civ.Done[_soc03])
        {
            int n = Math.Max(1, inputs.Settlements);
            Fix64 ties = (Fix64.One + _lowTies * (n - 1)) / n;
            gain += (Fix64.One - ties) / ties;
        }
        return Fix64.Min(gain, Fix64.FromInt(2));
    }

    /// <summary>
    /// Score = Leaning × (1 + Urgency) × (1 + Opportunity) × KeystoneProgress ÷ Cost (docs/MVP/02 §15). KeystoneProgress
    /// covers the next keystone's whole closure, backfills included (docs/MVP/07 F8), and grows with how long the chain
    /// still ahead of a project is, so the critical path leads. Opportunity counts stored Know-how, what rivals know and
    /// how much a project would raise Insight.
    /// </summary>
    private Fix64 CouncilScore(int c, int p, CouncilContext ctx, out CouncilReason reason)
    {
        CivResearch civ = _civs[c];
        ProjectDef def = _catalog.Projects[p];
        Fix64 cost = EffectiveCost(c, p);
        if (cost.Raw <= 0) cost = Fix64.Epsilon;

        Fix64 leaning = def.Theme == Theme.Keystone ? Fix64.One : Fix64.Max(MinLeaning, Fix64.FromRatio(civ.Leaning[(int)def.Theme], 100));

        int group = def.Lineage >= 0 ? UrgencyGroupOf(_catalog.LineageCodes[def.Lineage]) : GroupNone;
        Fix64 signal = group switch
        {
            GroupFamine => ctx.Signals.Famine,
            GroupThreat => ctx.Signals.Threat,
            GroupDisease => ctx.Signals.Disease,
            GroupCohesion => ctx.Signals.Cohesion,
            _ => Fix64.Zero,
        };
        Fix64 urgency = signal * UrgencyWeight;

        Fix64 knowHow = def.Lineage >= 0 ? Fix64.Min(Fix64.One, civ.KnowHow[def.Lineage] / cost) : Fix64.Zero;
        Fix64 discount = Fix64.One - Fix64.Min(Fix64.One, cost / Fix64.Max(def.Cost, Fix64.Epsilon));
        bool rivalsKnow = false;
        for (int o = 0; o < _civs.Length; o++)
            if (o != c && InContact(c, o) && _civs[o].Done[p]) { rivalsKnow = true; break; }
        Fix64 insightGain = InsightGain(def, civ, ctx.Inputs);
        Fix64 opportunity = knowHow + (rivalsKnow ? discount : Fix64.Zero) + insightGain * InsightGainWeight;

        Fix64 keyFactor = Fix64.One;
        bool onPath = false;
        if (ctx.Closure is not null && ctx.Closure[p])
        {
            keyFactor = KeystonePathBoost + Fix64.FromInt(ctx.Depth[p]);
            if (p == ctx.Keystone) keyFactor += KeystoneReadyBoost;
            onPath = true;
        }
        else if (ctx.BreadthShort && !ctx.ClosureOpen && def.Era == civ.Era && def.Theme != Theme.Keystone)
        {
            // Free picks count toward the keystone's era breadth; earlier-era backfills do not.
            keyFactor = BreadthBoost;
            onPath = true;
        }

        // A mature E3 economy can have the City chain ready while BLD-07 waits behind other optional work.
        // Give only that available breadth project a modest nudge after the bronze housing, belief and stone paths land.
        // KEY-4 closure projects retain their larger critical-path factor.
        if (p == _bld07 && civ.Era == 3 && civ.LastState[_bld07] == ProjectState.Available &&
            civ.Done[_trd04] && civ.Done[_bel05] && civ.Done[_bld05])
            keyFactor *= BreadthBoost;

        reason = onPath ? CouncilReason.KeystonePath
            : urgency >= Fix64.One ? group switch
            {
                GroupFamine => CouncilReason.FamineRisk,
                GroupThreat => CouncilReason.Threat,
                GroupDisease => CouncilReason.Disease,
                _ => CouncilReason.CohesionCrisis,
            }
            : rivalsKnow ? CouncilReason.RivalsKnowIt
            : knowHow >= Fix64.Half ? CouncilReason.KnowHow
            : civ.Leaning[(int)(def.Theme == Theme.Keystone ? Theme.Craft : def.Theme)] > 40 ? CouncilReason.Leaning
            : CouncilReason.Cheap;
        return leaning * (Fix64.One + urgency) * (Fix64.One + opportunity) * keyFactor / cost;
    }

    /// <summary>
    /// The Council's score for every project (zero unless Available and not slotted): the prior the AI's search starts from
    /// (docs/MVP/14 §4.2) and the Settler's fallback picks.
    /// </summary>
    public void CouncilPrior(int civ, Span<Fix64> scores)
    {
        if (scores.Length < _catalog.Count) throw new ArgumentException("One score per project.", nameof(scores));
        scores.Clear();
        CivResearch c = _civs[civ];
        CouncilContext ctx = BuildCouncilContext(civ);
        for (int p = 0; p < _catalog.Count; p++)
            if (c.LastState[p] == ProjectState.Available) scores[p] = CouncilScore(civ, p, ctx, out _);
    }

    /// <summary>The Council of Elders' auto-pick among Available projects; ties go to the lowest index. Returns -1 when none.</summary>
    private int CouncilPick(int c, out CouncilReason reason)
    {
        CivResearch civ = _civs[c];
        CouncilContext ctx = BuildCouncilContext(c);
        int best = -1;
        Fix64 bestScore = Fix64.MinValue;
        reason = CouncilReason.Cheap;
        for (int p = 0; p < _catalog.Count; p++)
        {
            if (civ.LastState[p] != ProjectState.Available) continue;
            Fix64 score = CouncilScore(c, p, ctx, out CouncilReason why);
            if (score <= bestScore) continue;
            best = p;
            bestScore = score;
            reason = why;
        }
        return best;
    }

    /// <summary>
    /// An AI civilization keeps its best-scoring work in Primary, where the keystone chain gets 4/7 of its Insight
    /// (the "keystone phase", docs/MVP/07 §2). The player's slots are never reordered by the Council.
    /// Also pulls a ready keystone out of the pool into a slot.
    /// </summary>
    private void RankByCouncil(int c)
    {
        CivResearch civ = _civs[c];
        CouncilContext ctx = BuildCouncilContext(c);
        // A keystone that has become Available is taken straight into Primary, displacing the lowest slot's occupant.
        int key = ctx.Keystone;
        if (key >= 0 && civ.LastState[key] == ProjectState.Available && civ.SlotOf(key) < 0)
        {
            civ.Slots[CivResearch.SlotCount - 1] = key;
            civ.LastState[key] = ProjectState.Slotted;
            Emit((byte)c, ResearchEventKind.SlotFilled, key, CivResearch.SlotCount - 1, 2);
        }
        Span<Fix64> scores = stackalloc Fix64[CivResearch.SlotCount];
        for (int s = 0; s < CivResearch.SlotCount; s++)
            scores[s] = civ.Slots[s] >= 0 ? CouncilScore(c, civ.Slots[s], ctx, out _) : Fix64.MinValue;
        // Stable insertion sort, descending; carry stays with the slot index.
        for (int i = 1; i < CivResearch.SlotCount; i++)
            for (int j = i; j > 0 && scores[j] > scores[j - 1]; j--)
            {
                (scores[j], scores[j - 1]) = (scores[j - 1], scores[j]);
                (civ.Slots[j], civ.Slots[j - 1]) = (civ.Slots[j - 1], civ.Slots[j]);
            }
        RefreshStates(c, announce: false);
    }
}
