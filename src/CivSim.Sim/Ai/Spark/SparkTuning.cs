using CivSim.Sim.Numerics;

namespace CivSim.Sim.Ai;

/// <summary>
/// The research planner's weights (docs/MVP/14 §4, §14: AI parameters are data, tuned in the harness). Every value here was
/// set against the Spark bench (SparkBenchTests): AI civilizations against Council neighbours on mirrored seeds.
/// </summary>
public static class SparkTuning
{
    /// <summary>Projects the beam search orders explicitly (14 §4.2: "the next ~6 projects").</summary>
    public const int SearchDepth = 6;
    /// <summary>
    /// Slots forced onto the next keystone's critical path while its closure has available projects, ahead of the search.
    /// Zero: the search with keystone tempo already routes the keystone (the ready keystone itself is always forced into
    /// Primary), and forcing the closure costs Firsts. Harness duel against the Council (16 seeds, 32 matches, with tempo 25):
    /// 2 slots 75%, 1 slot 88%, 0 slots 97% wins on Legacy.
    /// </summary>
    public const int ClosureSlots = 0;
    /// <summary>Children tried per beam node: half the beam width plus this, capped (Settler 4, Chieftain 5, Warlord and Legend 6).</summary>
    public const int ExtraCandidates = 3;
    public const int MaxCandidates = 6;
    /// <summary>Slots plus the research queue: the plan written each decision, so slots never idle between decisions.</summary>
    public const int PlanLength = 11;
    /// <summary>The shortest horizon, in seasons (14 §4.1: "at least 2 years of sim time").</summary>
    public const int MinHorizonSeasons = 8;
    /// <summary>The Firsts race margin: a First is sure only a season ahead of the best rival (14 §4.4).</summary>
    public const int FirstMarginSeasons = 1;
    /// <summary>Bounds on the fastest rival's estimated pace relative to ours (from public keystone times).</summary>
    public static readonly Fix64 RivalPaceMin = Fix64.FromRatio(7, 10);
    public static readonly Fix64 RivalPaceMax = Fix64.FromInt(2);
    /// <summary>
    /// How well a rival's pace is known: the Firsts race is uncertain by this share of the time until the rival's ETA, so
    /// a project the rival reaches in ten minutes is contested over a six-minute window (±3), not a knife edge. 0.2 and 0.3
    /// bench alike; 0.3 is kept for the weaker seats, where the leader's pace is least certain.
    /// </summary>
    public static readonly Fix64 RivalEtaUncertainty = Fix64.FromRatio(3, 10);
    /// <summary>
    /// Keystone tempo in Legacy points per season earlier (14 §4.3): the era it opens brings cheaper backfills (EraLag 0.7),
    /// the next era's Firsts and the Era Boon sooner, more than the horizon's flat time weight credits. Duel (16 seeds, no
    /// closure slots): 10 → 94%, 25 → 97%, 100 → 84%.
    /// </summary>
    public static readonly Fix64 KeystoneTempo = Fix64.FromInt(25);
    /// <summary>A new plan replaces the one in force only if its forecast utility is higher by this share (or this floor).</summary>
    public static readonly Fix64 CommitmentShare = Fix64.FromRatio(2, 100);
    public static readonly Fix64 CommitmentFloor = Fix64.FromInt(3);
    /// <summary>A change this large in the Strategist's DefenseUrgency or AttackReadiness is a hard event that lifts the commitment.</summary>
    public static readonly Fix64 IntentShock = Fix64.FromRatio(1, 4);

    /// <summary>
    /// Archetype style (14 §9.1): a completion's Legacy is weighted by 1 + StyleWeight × (Leaning − 1/3) for its theme, so a
    /// Builder leans to Craft and a Wanderer to Reach without giving up competence. Balanced civilizations are unaffected.
    /// </summary>
    public static readonly Fix64 StyleWeight = Fix64.FromRatio(6, 10);
    /// <summary>Signature lineages inside the leaning (Warlord: WAR and DEF; Sage: LRN and BEL), a flat bonus per completion.</summary>
    public static readonly Fix64 SignatureBonus = Fix64.FromInt(3);
    /// <summary>
    /// The Strategist's readings turned into research value (14 §6.4: "DEF and WAR projects jump in priority"): Legacy-points
    /// equivalent added to a DEF/WAR project at full DefenseUrgency, to a WAR project at full AttackReadiness, and to a TER,
    /// WAY or SOC project at full FrontierValue.
    /// </summary>
    public static readonly Fix64 DefenseBonus = Fix64.FromInt(40);
    public static readonly Fix64 AttackBonus = Fix64.FromInt(30);
    public static readonly Fix64 FrontierBonus = Fix64.FromInt(8);
    /// <summary>Modest value for food and lean-season capabilities while settlements are food insecure.</summary>
    public static readonly Fix64 FoodNeedBonus = Fix64.FromInt(10);
    public static readonly Fix64 LeanNeedBonus = Fix64.FromInt(12);
    /// <summary>Modest value for cohesion effects while a settlement is strained.</summary>
    public static readonly Fix64 CohesionNeedBonus = Fix64.FromInt(8);
    /// <summary>Urgent projects receive this additional value only while the corresponding need is acute.</summary>
    public static readonly Fix64 NeedUrgencyBonus = Fix64.FromInt(8);
    /// <summary>The Strategist's readings are worth their full bonus only if answered at once, nothing after this many seasons.</summary>
    public const int UrgencySeasons = 8;
    /// <summary>Option value (14 §4.1) per project that transitively needs this one (hub emphasis, 03 §4.3), in Legacy points.</summary>
    public static readonly Fix64 HubBonus = Fix64.FromRatio(1, 10);
    /// <summary>Option value of a project on the next keystone's closure: forced, so done early is never wasted (07 F8).</summary>
    public static readonly Fix64 ClosureBonus = Fix64.FromInt(2);
    /// <summary>How the Council prior enters the search ordering, relative to the forecast heuristic (both normalized to 1).</summary>
    public static readonly Fix64 PriorWeight = Fix64.One;
}
