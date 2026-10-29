using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// Diagnostic ledgers of what each action produced last season, how the labor was split and what research is worth
/// (the "Your people" sheet). The ledgers are written while the season runs but never read by a decision, never hashed or
/// snapshotted; the payoff numbers are read-only counterfactuals ("what last season would have lost without this project
/// alone"), so the golden checksums and replays do not depend on any of it.
/// </summary>
public sealed partial class LivingEconomyModel
{
    public const int ActionLedgerKinds = 5;
    public const int ActionForage = 0, ActionHunt = 1, ActionFish = 2, ActionFarm = 3, ActionChop = 4;
    /// <summary>Per action: work assigned, work applied after tools, output asked for and taken (units), rations or wood added.</summary>
    public const int ActionLedgerFields = 5;
    public const int ActionLedgerLineCount = ActionLedgerKinds * ActionLedgerFields;
    public const int ActionAssigned = 0, ActionApplied = 1, ActionRequested = 2, ActionHarvested = 3, ActionOutput = 4;

    /// <summary>Labor lines: capacity, reserved before planning (fire, care, roles, scouts, upkeep, metal, civic canals), food, building, materials, herding, civic work, the food-share rule.</summary>
    public const int LaborLineCount = 8;
    public const int LaborCapacity = 0, LaborReserved = 1, LaborFood = 2, LaborBuild = 3, LaborMaterial = 4, LaborHerd = 5, LaborCivic = 6, LaborRule = 7;

    private static int FoodIndexOf(Good good)
    {
        for (int i = 0; i < FoodGoods.Length; i++) if (FoodGoods[i] == good) return i;
        return 0;
    }

    private static void RecordAction(SettlementState s, int kind, Fix64 assigned, Fix64 applied, Fix64 requested, Fix64 harvested, Fix64 output)
    {
        s.LedgerAssigned[kind] = assigned;
        s.LedgerApplied[kind] = Fix64.Min(assigned, applied);
        s.LedgerRequested[kind] = requested;
        s.LedgerHarvested[kind] = harvested;
        s.LedgerOutput[kind] = output;
    }

    private static void RecordSettlementHazards(SettlementState s, HazardBreakdown hazards)
    {
        s.LedgerHazard[(int)EconomyDeathCause.Endemic] += hazards.Endemic;
        s.LedgerHazard[(int)EconomyDeathCause.Epidemic] += hazards.Epidemic;
        s.LedgerHazard[(int)EconomyDeathCause.Starvation] += hazards.Starvation + hazards.WinterShortfall;
        s.LedgerHazard[(int)EconomyDeathCause.Exposure] += hazards.Exposure;
        s.LedgerHazard[(int)EconomyDeathCause.Injury] += hazards.Injury;
        s.LedgerHazard[(int)EconomyDeathCause.Predators] += hazards.Predators;
        s.LedgerHazard[(int)EconomyDeathCause.Violence] += hazards.Violence;
        s.LedgerHazard[(int)EconomyDeathCause.Senescence] += hazards.Senescence;
    }

    /// <summary>Last season's work and output per action: <see cref="ActionLedgerFields"/> numbers for each of the five actions.</summary>
    public void ActionLedgerLines(int settlementIndex, Span<Fix64> into)
    {
        if (into.Length < ActionLedgerLineCount) throw new ArgumentException("Too small.", nameof(into));
        SettlementState s = _settlements[settlementIndex];
        for (int k = 0; k < ActionLedgerKinds; k++)
        {
            into[k * ActionLedgerFields + ActionAssigned] = s.LedgerAssigned[k];
            into[k * ActionLedgerFields + ActionApplied] = s.LedgerApplied[k];
            into[k * ActionLedgerFields + ActionRequested] = s.LedgerRequested[k];
            into[k * ActionLedgerFields + ActionHarvested] = s.LedgerHarvested[k];
            into[k * ActionLedgerFields + ActionOutput] = s.LedgerOutput[k];
        }
    }

    /// <summary>Last season's labor split in work-seasons, and the food-share rule that fired (1 hunger, 2 low stores, 3 thin stores, 4 comfortable, 5 town cap).</summary>
    public void LaborLines(int settlementIndex, Span<Fix64> into)
    {
        if (into.Length < LaborLineCount) throw new ArgumentException("Too small.", nameof(into));
        SettlementState s = _settlements[settlementIndex];
        into[LaborCapacity] = s.LedgerCapacity;
        into[LaborReserved] = Fix64.Max(Fix64.Zero, s.LedgerCapacity - s.LedgerPlannable);
        into[LaborFood] = s.LedgerFoodWork;
        into[LaborBuild] = s.LedgerBuildWork;
        into[LaborMaterial] = s.LedgerMaterialWork;
        into[LaborHerd] = s.LedgerHerdWork;
        into[LaborCivic] = s.LedgerCivicWork;
        into[LaborRule] = Fix64.FromInt(s.LedgerFoodShareRule);
    }

    // ---- what the builders are waiting for ----------------------------------------------------

    /// <summary>A dwelling or store the planner wants but cannot pay for: <see cref="Structure"/> kind, the good's slot (-1 food) and what is in store against what it takes.</summary>
    public readonly record struct BuildBlockerReadout(int Structure, int Slot, Fix64 Have, Fix64 Need)
    {
        public bool Any => Structure >= 0;
    }

    /// <summary>
    /// The first dwelling, shelter or store the settlement wants whose materials are short, when none of the wanted ones can be
    /// paid for. Mirrors <see cref="SelectBuildCandidate"/> for housing, warmth and storage and changes nothing.
    /// </summary>
    public BuildBlockerReadout BuildBlocker(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        int population = Population(s.Id);
        bool needsHousing = HousingCap(s) < (ShouldBuildMarketHousingAhead(s) ? HousingTarget(s) : Fix64.FromInt(population + 8));
        bool hasWarmShelter = s.Structures[(int)Structure.PitHouse] > 0 || s.Structures[(int)Structure.WattleHouse] > 0 ||
            s.Structures[(int)Structure.MudbrickHouse] > 0 || s.Structures[(int)Structure.Longhouse] > 0;
        bool hasBasicShelter = hasWarmShelter || s.Structures[(int)Structure.LeanTo] > 0 || s.Structures[(int)Structure.HideTent] > 0;
        bool extraLeanTo = s.Civic is null && ShouldBuildMarketHousingAhead(s) && needsHousing;
        bool cold = WarmthDeficit(s, true) > 0;
        int wood = (int)Good.Wood, clay = (int)Good.Clay, brick = (int)Good.Mudbrick, stone = (int)Good.Stone;

        BuildBlockerReadout first = default;
        bool any = false;
        // Each wanted item: its structure, then what it takes (good slot, amount) in order; null slot ends the list.
        bool Want(Structure structure, bool wanted, ReadOnlySpan<(int Slot, int Amount, int Hundredths)> needs)
        {
            if (!wanted || !CanBuild(s.Civilization, structure)) return false;
            for (int i = 0; i < needs.Length; i++)
            {
                Fix64 need = needs[i].Hundredths > 0 ? Fix64.FromRatio(needs[i].Hundredths, 100) : Fix64.FromInt(needs[i].Amount);
                Fix64 have = needs[i].Slot < 0 ? FoodStores(s) : s.Stores[needs[i].Slot];
                if (have >= need) continue;
                if (!any) { first = new BuildBlockerReadout((int)structure, needs[i].Slot, have, need); any = true; }
                return true;
            }
            return false;
        }

        bool affordable = false;
        bool Check(Structure structure, bool wanted, ReadOnlySpan<(int Slot, int Amount, int Hundredths)> needs)
        {
            if (!wanted || !CanBuild(s.Civilization, structure)) return false;
            bool short_ = Want(structure, true, needs);
            if (!short_) affordable = true;
            return short_;
        }

        Check(Structure.StoragePit, s.Structures[(int)Structure.StoragePit] == 0, stackalloc[] { (-1, 5, 0) });
        Check(Structure.Granary, s.Structures[(int)Structure.Granary] == 0, stackalloc[] { (brick, 80, 0) });
        Check(Structure.PitHouse, !hasWarmShelter && hasBasicShelter && cold, stackalloc[] { (wood, 4, 0) });
        Check(Structure.WattleHouse, !hasWarmShelter && hasBasicShelter && cold, stackalloc[] { (wood, 3, 0), (clay, 2, 0) });
        Check(Structure.LeanTo, (!hasBasicShelter || extraLeanTo) && (needsHousing || cold), stackalloc[] { (wood, 1, 0) });
        Check(Structure.Hearth, s.Structures[(int)Structure.Hearth] == 0, stackalloc[] { (stone, 0, 50) });
        Check(Structure.PitHouse, s.Structures[(int)Structure.PitHouse] == 0 && needsHousing, stackalloc[] { (wood, 4, 0) });
        Check(Structure.WattleHouse, needsHousing, stackalloc[] { (wood, 3, 0), (clay, 2, 0) });
        Check(Structure.MudbrickHouse, needsHousing, stackalloc[] { (brick, 60, 0) });
        Check(Structure.Longhouse, needsHousing, stackalloc[] { (wood, 12, 0), (clay, 4, 0) });
        return any && !affordable ? first : new BuildBlockerReadout(-1, -1, Fix64.Zero, Fix64.Zero);
    }

    // ---- research payoff ------------------------------------------------------------------------

    /// <summary>What a project's measurable effects are doing in the settlements.</summary>
    public enum PayoffStatus : byte
    {
        /// <summary>No effect of the project can be counted as a flow.</summary>
        NotMeasured = 0,
        /// <summary>It added to last season's output, or saved lives.</summary>
        Paying = 1,
        /// <summary>A better method replaced it.</summary>
        Superseded = 2,
        /// <summary>A method it gave is blocked by a tool, crew or metal.</summary>
        Blocked = 3,
        /// <summary>Its flow was zero last season (nobody did the work, nothing spoiled).</summary>
        Idle = 4,
    }

    /// <summary>Rations, wood and lives per season (Q32.32), with the status and, when blocked, why and by which tool.</summary>
    public readonly record struct PayoffValue(Fix64 Rations, Fix64 Wood, Fix64 Lives, PayoffStatus Status,
        MethodFallbackReason Reason, MethodToolId Tool);

    private int[]? _payoffProjects;
    private int[][]? _payoffEffects;

    /// <summary>Dense project indices whose effects can be measured (a method, a yield or food modifier, a hazard); constant for the content.</summary>
    public ReadOnlySpan<int> PayoffProjects
    {
        get { EnsurePayoffIndex(); return _payoffProjects; }
    }

    private static int PayoffAction(string? action) => action switch
    {
        "forage" => ActionForage, "hunt" => ActionHunt, "fish" => ActionFish, "chop" => ActionChop, _ => -1,
    };

    private static EconomyJob PayoffJob(int kind) => kind switch
    {
        ActionForage => EconomyJob.Forage, ActionHunt => EconomyJob.Hunt, ActionFish => EconomyJob.Fish, _ => EconomyJob.Chop,
    };

    private static string PayoffActionName(int kind) => kind switch
    {
        ActionForage => "forage", ActionHunt => "hunt", ActionFish => "fish", _ => "chop",
    };

    private enum PayoffFlow { None, Forage, Hunt, Chop, Farm, Eaten, EatenAnimalsAndGrain, EatenCereal, Spoiled }

    private static PayoffFlow FlowOfStat(string? stat) => stat switch
    {
        "forage.yield" or "autumn_forage" => PayoffFlow.Forage,
        "hunt" => PayoffFlow.Hunt,
        "wood_yield" => PayoffFlow.Chop,
        "field_yield" or "irrigated_yield" => PayoffFlow.Farm,
        "food_value" => PayoffFlow.Eaten,
        "food_value_of_meat_roots_cereals" => PayoffFlow.EatenAnimalsAndGrain,
        "cereal_food_value" => PayoffFlow.EatenCereal,
        "spoilage_of_stored_food" => PayoffFlow.Spoiled,
        _ => PayoffFlow.None,
    };

    private static int HazardCauseOf(string? cause) => cause switch
    {
        "endemic" => (int)EconomyDeathCause.Endemic, "epidemic" => (int)EconomyDeathCause.Epidemic,
        "injury" => (int)EconomyDeathCause.Injury, "predators" => (int)EconomyDeathCause.Predators,
        "violence" => (int)EconomyDeathCause.Violence, "starvation" => (int)EconomyDeathCause.Starvation, _ => -1,
    };

    private bool IsMeasurable(in EconomyEffect e)
    {
        switch (e.Kind)
        {
            case "method": return PayoffAction(e.Action) >= 0 && e.Rung is not null;
            case "mod": return FlowOfStat(e.Stat) != PayoffFlow.None && e.Value is not null && e.Operation is "pct" or "mult";
            case "hazard":
                // Exposure is allocated from warmth deficits and MED hazards need a provider; the simulation does not
                // apply either as a plain multiplier, so they are not counted here.
                return HazardCauseOf(e.Cause) >= 0 && e.Mult is not null &&
                    _content.Projects[e.ProjectIndex].Id is not ("MED-01" or "MED-02" or "MED-03");
            default: return false;
        }
    }

    private void EnsurePayoffIndex()
    {
        if (_payoffProjects is not null) return;
        int projects = _content.Projects.Length;
        var lists = new List<int>[projects];
        for (int i = 0; i < _content.Effects.Length; i++)
        {
            EconomyEffect e = _content.Effects[i];
            if ((uint)e.ProjectIndex >= (uint)projects || !IsMeasurable(e)) continue;
            (lists[e.ProjectIndex] ??= new List<int>()).Add(i);
        }
        var found = new List<int>();
        var effects = new int[projects][];
        for (int p = 0; p < projects; p++)
        {
            effects[p] = lists[p]?.ToArray() ?? Array.Empty<int>();
            if (effects[p].Length > 0) found.Add(_content.Projects[p].Index);
        }
        _payoffEffects = effects;
        _payoffProjects = found.ToArray();
    }

    private int ProjectSlot(int project)
    {
        for (int p = 0; p < _content.Projects.Length; p++) if (_content.Projects[p].Index == project) return p;
        return -1;
    }

    private static Fix64 EffectMultiplier(in EconomyEffect e)
    {
        if (e.Value is not { } value) return Fix64.One;
        return e.Operation == "pct" ? Fix64.One + value : value;
    }

    /// <summary>Last season's flow a modifier acts on, at a settlement.</summary>
    private Fix64 FlowOf(SettlementState s, PayoffFlow flow)
    {
        switch (flow)
        {
            case PayoffFlow.Forage: return s.LedgerOutput[ActionForage];
            case PayoffFlow.Hunt: return s.LedgerOutput[ActionHunt];
            case PayoffFlow.Chop: return s.LedgerOutput[ActionChop];
            case PayoffFlow.Farm: return s.LedgerOutput[ActionFarm];
            case PayoffFlow.Eaten:
            {
                Fix64 total = Fix64.Zero;
                for (int g = 0; g < FoodGoodCount; g++) total += s.LedgerEatenByGood[g];
                return total;
            }
            case PayoffFlow.EatenAnimalsAndGrain:
                return s.LedgerEatenByGood[FoodIndexOf(Good.Meat)] + s.LedgerEatenByGood[FoodIndexOf(Good.Roots)] +
                       s.LedgerEatenByGood[FoodIndexOf(Good.WildGrain)] + s.LedgerEatenByGood[FoodIndexOf(Good.Grain)];
            case PayoffFlow.EatenCereal:
                return s.LedgerEatenByGood[FoodIndexOf(Good.WildGrain)] + s.LedgerEatenByGood[FoodIndexOf(Good.Grain)];
            case PayoffFlow.Spoiled:
                return Fix64.Max(Fix64.Zero, s.LedgerBeforeEat - s.LedgerEaten - s.LedgerAfterSpoil);
            default: return Fix64.Zero;
        }
    }

    private static bool WoodFlow(PayoffFlow flow) => flow == PayoffFlow.Chop;

    /// <summary>The highest method rung of an action below <paramref name="rung"/> that the civilization knows, or -1.</summary>
    private int LowerKnownRung(int civ, string action, int rung)
    {
        int best = -1;
        for (int i = 0; i < _content.Effects.Length; i++)
        {
            EconomyEffect e = _content.Effects[i];
            if (e.Kind != "method" || e.Action != action || e.Rung is not { } r || r >= rung || r <= best ||
                !_completedProjects[civ][e.ProjectIndex]) continue;
            best = r;
        }
        return best;
    }

    /// <summary>
    /// What a learned project is worth at a settlement, from last season's ledger: for each measurable effect, what the
    /// season would have lost without that project alone (a method's output times one minus the lower rung's rate over its
    /// own, a modifier's flow times one minus one over its multiplier, expected deaths times one over the hazard multiplier
    /// minus one). Shares of several projects do not add up to the total change, because the multipliers stack.
    /// </summary>
    public PayoffValue Payoff(int settlementIndex, int project)
    {
        EnsurePayoffIndex();
        int slot = ProjectSlot(project);
        if (slot < 0 || _payoffEffects![slot].Length == 0) return new PayoffValue(Fix64.Zero, Fix64.Zero, Fix64.Zero, PayoffStatus.NotMeasured, MethodFallbackReason.None, MethodToolId.None);
        SettlementState s = _settlements[settlementIndex];
        int civ = s.Civilization;
        Fix64 rations = Fix64.Zero, wood = Fix64.Zero, lives = Fix64.Zero;
        bool paying = false, blocked = false, superseded = false;
        MethodFallbackReason reason = MethodFallbackReason.None;
        MethodToolId tool = MethodToolId.None;
        bool autumn = (_seasonsCompleted - 1) % 4 == 2;
        foreach (int index in _payoffEffects[slot])
        {
            EconomyEffect e = _content.Effects[index];
            switch (e.Kind)
            {
                case "method":
                {
                    int kind = PayoffAction(e.Action);
                    int rung = e.Rung!.Value;
                    EconomyJob job = PayoffJob(kind);
                    int effective = MethodRung(settlementIndex, job);
                    if (effective > rung) { superseded = true; break; }
                    if (effective < rung)
                    {
                        blocked = true;
                        MethodReadout readout = MethodReadout(settlementIndex, job);
                        if (readout.KnownRung == rung && readout.Reason != MethodFallbackReason.None) { reason = readout.Reason; tool = readout.RequiredTool; }
                        else if (reason == MethodFallbackReason.None) reason = MethodFallbackReason.MissingTool;
                        break;
                    }
                    Fix64 baseline = JobBaseRate(job);
                    Fix64 thisRate = MethodRate(civ, PayoffActionName(kind), rung, baseline);
                    Fix64 lowerRate = MethodRate(civ, PayoffActionName(kind), LowerKnownRung(civ, PayoffActionName(kind), rung), baseline);
                    if (thisRate <= Fix64.Zero) break;
                    Fix64 value = s.LedgerOutput[kind] * Fix64.Max(Fix64.Zero, Fix64.One - lowerRate / thisRate);
                    if (value > Fix64.Zero) paying = true;
                    if (kind == ActionChop) wood += value; else rations += value;
                    break;
                }
                case "mod":
                {
                    PayoffFlow flow = FlowOfStat(e.Stat);
                    if (e.Stat == "autumn_forage" && !autumn) break;
                    Fix64 m = EffectMultiplier(e);
                    if (m <= Fix64.Zero) break;
                    Fix64 flowValue = FlowOf(s, flow);
                    Fix64 value = flow == PayoffFlow.Spoiled
                        ? (m < Fix64.One ? flowValue * (Fix64.One / m - Fix64.One) : Fix64.Zero)
                        : (m > Fix64.One ? flowValue * (Fix64.One - Fix64.One / m) : Fix64.Zero);
                    if (value > Fix64.Zero) paying = true;
                    if (WoodFlow(flow)) wood += value; else rations += value;
                    break;
                }
                case "hazard":
                {
                    Fix64 m = e.Mult!.Value;
                    if (e.Scope == "hunters") m = Fix64.One + Fix64.Parse("0.6") * (m - Fix64.One);
                    if (m <= Fix64.Zero || m >= Fix64.One) break;
                    Fix64 value = s.LedgerHazard[HazardCauseOf(e.Cause)] * (Fix64.One / m - Fix64.One);
                    if (value > Fix64.Zero) paying = true;
                    lives += value;
                    break;
                }
            }
        }
        PayoffStatus status = paying ? PayoffStatus.Paying : blocked ? PayoffStatus.Blocked : superseded ? PayoffStatus.Superseded : PayoffStatus.Idle;
        return new PayoffValue(rations, wood, lives, status, reason, tool);
    }

    /// <summary>
    /// What a project in a research slot would add at today's flows, summed over the civilization's settlements: the same
    /// counterfactual run forward. A method no settlement can use yet reports the tool it needs instead.
    /// </summary>
    public PayoffValue Projection(int civ, int project)
    {
        EnsurePayoffIndex();
        int slot = ProjectSlot(project);
        if (slot < 0 || _payoffEffects![slot].Length == 0) return new PayoffValue(Fix64.Zero, Fix64.Zero, Fix64.Zero, PayoffStatus.NotMeasured, MethodFallbackReason.None, MethodToolId.None);
        bool ledger = false;
        for (int i = 0; i < _settlements.Count && !ledger; i++) ledger = _settlements[i].Civilization == civ && _settlements[i].LedgerValid;
        // After a load the ledgers are empty until a season ends: say nothing rather than "no gain".
        if (!ledger) return new PayoffValue(Fix64.Zero, Fix64.Zero, Fix64.Zero, PayoffStatus.NotMeasured, MethodFallbackReason.None, MethodToolId.None);
        Fix64 rations = Fix64.Zero, wood = Fix64.Zero, lives = Fix64.Zero;
        bool gain = false, needsTool = false;
        MethodToolId tool = MethodToolId.None;
        MethodFallbackReason reason = MethodFallbackReason.None;
        bool autumn = (_seasonsCompleted - 1) % 4 == 2;
        for (int si = 0; si < _settlements.Count; si++)
        {
            SettlementState s = _settlements[si];
            if (s.Civilization != civ) continue;
            foreach (int index in _payoffEffects[slot])
            {
                EconomyEffect e = _content.Effects[index];
                switch (e.Kind)
                {
                    case "method":
                    {
                        int kind = PayoffAction(e.Action);
                        int rung = e.Rung!.Value;
                        EconomyJob job = PayoffJob(kind);
                        string action = PayoffActionName(kind);
                        int effective = MethodRung(si, job);
                        if (effective >= rung) break;
                        bool usable = HuntPartyMatches(action, rung, 3) && !(action == "chop" && rung >= 5) && HasRequiredTool(s, action, rung);
                        if (!usable)
                        {
                            needsTool = true;
                            if (!HuntPartyMatches(action, rung, 3)) reason = MethodFallbackReason.HuntParty;
                            else { reason = MethodFallbackReason.MissingTool; if (tool == MethodToolId.None) tool = MissingMethodTool(s, action, rung); }
                            break;
                        }
                        Fix64 baseline = JobBaseRate(job);
                        Fix64 thisRate = e.Rate ?? (e.Mult is { } mult ? baseline * mult : baseline);
                        Fix64 currentRate = MethodRate(civ, action, effective, baseline);
                        if (currentRate <= Fix64.Zero) break;
                        Fix64 value = s.LedgerOutput[kind] * Fix64.Max(Fix64.Zero, thisRate / currentRate - Fix64.One);
                        if (value > Fix64.Zero) gain = true;
                        if (kind == ActionChop) wood += value; else rations += value;
                        break;
                    }
                    case "mod":
                    {
                        PayoffFlow flow = FlowOfStat(e.Stat);
                        if (e.Stat == "autumn_forage" && !autumn) break;
                        Fix64 m = EffectMultiplier(e);
                        if (m <= Fix64.Zero) break;
                        Fix64 flowValue = FlowOf(s, flow);
                        // The modifier is not in last season's flow yet: spoilage would shrink by (1 - m), a yield would grow by (m - 1).
                        Fix64 value = flow == PayoffFlow.Spoiled
                            ? (m < Fix64.One ? flowValue * (Fix64.One - m) : Fix64.Zero)
                            : (m > Fix64.One ? flowValue * (m - Fix64.One) : Fix64.Zero);
                        if (value > Fix64.Zero) gain = true;
                        if (WoodFlow(flow)) wood += value; else rations += value;
                        break;
                    }
                    case "hazard":
                    {
                        Fix64 m = e.Mult!.Value;
                        if (e.Scope == "hunters") m = Fix64.One + Fix64.Parse("0.6") * (m - Fix64.One);
                        if (m <= Fix64.Zero || m >= Fix64.One) break;
                        Fix64 value = s.LedgerHazard[HazardCauseOf(e.Cause)] * (Fix64.One - m);
                        if (value > Fix64.Zero) gain = true;
                        lives += value;
                        break;
                    }
                }
            }
        }
        PayoffStatus status = gain ? PayoffStatus.Paying : needsTool ? PayoffStatus.Blocked : PayoffStatus.Idle;
        return new PayoffValue(rations, wood, lives, status, reason, tool);
    }
}
