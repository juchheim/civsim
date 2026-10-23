using System;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Research;

/// <summary>Where a project stands for one civilization (docs/MVP/02 §7).</summary>
public enum ProjectState : byte
{
    /// <summary>Not shown: a future era, an unseen resource, or more than one prerequisite missing.</summary>
    Hidden = 0,
    /// <summary>Shown as a silhouette: all but one prerequisite done, or a contacted civilization has it.</summary>
    Glimpsed = 1,
    Available = 2,
    Slotted = 3,
    Completed = 4,
    /// <summary>Completed, but a needed input is missing so its methods fall back a rung.</summary>
    Dormant = 5,
}

public enum ResearchEventKind : byte
{
    /// <summary>A project moved from Hidden to Glimpsed.</summary>
    Glimpsed,
    /// <summary>A project became Available. Project = id.</summary>
    Unlocked,
    /// <summary>A project entered a focus slot. A = slot, B = 1 when the Council or queue chose it.</summary>
    SlotFilled,
    /// <summary>A project completed. A = innovator figure id, B = settlement index where it was first practised.</summary>
    Completed,
    /// <summary>The innovator Moment. A = 1 for an in-era project (a Moment fires), 0 for an earlier-era backfill (batched into the Chronicle, docs/MVP/07 F8).</summary>
    Moment,
    /// <summary>One visible change from an effect. A = <see cref="VisibleChange"/>, B = effect index.</summary>
    VisibleChange,
    /// <summary>Adoption milestone. A = percent of the civilization's people who know it (50, 90, 100).</summary>
    Adopted,
    /// <summary>A keystone completed. Project = keystone, A = new era, B = boon (0 Insight, 1 Beacon).</summary>
    EraAdvanced,
    /// <summary>The first civilization in the world completed it. Project = id, A = Legacy points gained.</summary>
    FirstClaimed,
    /// <summary>The Council chose a project. A = slot, B = <see cref="CouncilReason"/>.</summary>
    CouncilPick,
    /// <summary>A civilization saw a resource. A = gate target index.</summary>
    SeenResource,
    /// <summary>Two civilizations made contact. A = the other civilization.</summary>
    ContactMade,
    /// <summary>A completed project went Dormant (A = 1) or came back (A = 0).</summary>
    DormantChanged,
}

/// <summary>One thing that happened in the research system. The presentation layer reads these; they are never fed back.</summary>
public readonly record struct ResearchEvent(long Tick, byte Civ, ResearchEventKind Kind, int Project, int A, int B);

/// <summary>Why the Council of Elders suggested a project (docs/MVP/02 §15, 08 §8.6).</summary>
public enum CouncilReason : byte
{
    Cheap = 0,
    Leaning = 1,
    KeystonePath = 2,
    FamineRisk = 3,
    Threat = 4,
    Disease = 5,
    CohesionCrisis = 6,
    KnowHow = 7,
    RivalsKnowIt = 8,
}

/// <summary>Urgency signals from the simulation, each 0 to 1, that steer the Council (docs/MVP/02 §15).</summary>
public readonly record struct UrgencySignals(Fix64 Famine, Fix64 Threat, Fix64 Disease, Fix64 Cohesion);

/// <summary>What the economy tells research about a civilization's people (docs/MVP/02 §8.1).</summary>
public struct InsightInputs
{
    public Fix64 Adults;
    public Fix64 Elders;
    public int Settlements;
    /// <summary>Role holders by role index in <see cref="ResearchCatalog.Roles"/>; a role only produces once its project is done.</summary>
    public Fix64[] RoleHeads;
    /// <summary>Flat Insight per year from structures (Monasteries, Libraries). Zero in E0 to E2.</summary>
    public Fix64 StructureOutput;
    /// <summary>Share (0 to 1) of Commons produced at settlements with a hearth (FIR-03).</summary>
    public Fix64 HearthShare;
    /// <summary>Per-archetype scales (docs/MVP/07 sensitivity): Insight, Roles, Know-how.</summary>
    public Fix64 InsightScale, RolesScale, KnowHowScale;
}

/// <summary>Read access to a civilization's research state, for the economy and the planner.</summary>
public interface IResearchView
{
    int CivCount { get; }
    int Era(int civ);
    bool IsDone(int civ, int project);
    ResearchCatalog Catalog { get; }
}

/// <summary>
/// Everything research reads from the living economy (docs/MVP/05 §16): people, practice, signals and
/// exposure. <see cref="LivingEconomySource"/> adapts M3's <c>LivingEconomyModel</c>; tests use fixed sources.
/// Methods are called at season boundaries unless noted.
/// </summary>
public interface IResearchSource
{
    int CivCount { get; }
    /// <summary>Called once after the season's economy update, before research reads anything.</summary>
    void BeginSeason(long tick);
    InsightInputs GetInsightInputs(int civ);
    /// <summary>
    /// Work-seasons of practice done in the season that just ended, by practice-registry index
    /// (docs/MVP/05 §16.2), plus the work-weighted share of working people (the practising share of Commons).
    /// </summary>
    void GetPractice(int civ, IResearchView view, Span<Fix64> workByPracticeIndex, out Fix64 practisingShare);
    UrgencySignals GetSignals(int civ);
    int SettlementCount(int civ);
    Fix64 SettlementPeople(int civ, int settlement);
    /// <summary>Seasons before knowledge reaches a settlement from the others (distance lag).</summary>
    int AdoptionLagSeasons(int civ, int settlement);
    /// <summary>The figure who first tries a new method (docs/MVP/05 §16.3); an opaque id.</summary>
    long ChooseInnovator(int civ, int project, int primaryPractice);
    /// <summary>Bit i set when the civilization has seen gate target i (an index into <see cref="ResearchCatalog.GateTargets"/>). Reads completed projects through <paramref name="view"/>.</summary>
    ulong SeenMask(int civ, IResearchView view);
    /// <summary>
    /// Access gates the source decides this season (docs/MVP/22 §5.7): bits in <paramref name="decided"/> are set to their
    /// value in <paramref name="available"/>; other bits keep what <see cref="ResearchSystem.SetAccess"/> gave them. Returns false
    /// when the source decides nothing (the default).
    /// </summary>
    bool AccessMask(int civ, out ulong decided, out ulong available) { decided = 0; available = 0; return false; }
    /// <summary>Gate-target index of the civilization's home climate band, or -1.</summary>
    int ClimateTarget(int civ);
    /// <summary>Settlements founded by a split since the last call, as (parent, daughter) indices within the civilization.</summary>
    void DrainFissions(int civ, System.Collections.Generic.List<(int Parent, int Daughter)> into);
    void Hash(ref StateHasher hasher);
    void WriteSnapshot(BinaryWriter writer);
    void ReadSnapshot(BinaryReader reader);
}
