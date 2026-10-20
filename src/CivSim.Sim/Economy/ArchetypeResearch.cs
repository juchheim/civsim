using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// The archetypes' research multipliers: on Insight, on specialist (role) Insight and on Know-how. The one table the
/// living economy's research source and the AI's forecast both read; Balanced is exactly one.
/// <para>
/// Started from docs/MVP/07 params.ARCHETYPES and re-fitted for the Legacy race (docs/MVP/14 §17): Firsts go to the first
/// civilization to finish a project, so any edge in research pace compounds into most of a match's Legacy, and the
/// archetypes' other differences must be paid for here. A Kin Leaning steers the planner onto the research engine (LRN,
/// SOC and the food lineages are Kin), Grower fertility buys more people, and a Reach Leaning takes its Era Boons as
/// Beacons instead of +5% Insight; the Territory multipliers the balance model set against Wanderer and Warlord costs do
/// not exist in the live sim (claims are bounded by the era's claim radius). Each value below is the one that brings that
/// archetype's mean Legacy at minute 33 within a few percent of the others in four-AI free-for-alls (24 seeds, 120 matches).
/// </para>
/// </summary>
public static class ArchetypeResearch
{
    public readonly record struct Scales(Fix64 Insight, Fix64 Roles, Fix64 KnowHow);

    public static readonly Scales Balanced = new(Fix64.One, Fix64.One, Fix64.One);
    /// <summary>
    /// Big families, little schooling: ×0.93 Insight, fewer specialists (×0.9) and less practice per head (×0.95). Fertility
    /// ×1.3 already gives the Grower 5 settlements to the others' 3 and its Kin Leaning favours the research engine.
    /// </summary>
    public static readonly Scales Grower = new(Fix64.FromRatio(93, 100), Fix64.FromRatio(90, 100), Fix64.FromRatio(95, 100));
    /// <summary>Efficient core: practice turns into Know-how 30% faster (unchanged from 07).</summary>
    public static readonly Scales Builder = new(Fix64.One, Fix64.One, Fix64.FromRatio(130, 100));
    /// <summary>
    /// Travellers who learn from everything they meet: ×1.15 Insight and Know-how ×1.1. Pays for the Era Boons taken as
    /// Beacons and for a Reach Leaning that keeps the planner off the Kin research engine (07 had ×0.95 against a Territory
    /// ×1.4 the live sim never had).
    /// </summary>
    public static readonly Scales Wanderer = new(Fix64.FromRatio(115, 100), Fix64.One, Fix64.FromRatio(110, 100));
    /// <summary>
    /// ×1.15 Insight, for the same Reach Leaning and Beacon Boons as the Wanderer (07 had ×0.9 against a Territory ×1.3 the
    /// live sim never had).
    /// </summary>
    public static readonly Scales Warlord = new(Fix64.FromRatio(115, 100), Fix64.One, Fix64.One);
    /// <summary>
    /// Institutions: specialists (Elder, Shaman and later roles) give 10% more Insight, commons ×0.97. 07's ×1.1 and ×1.4
    /// stacked on a Kin Leaning with the LRN/BEL signature won 78% of free-for-alls.
    /// </summary>
    public static readonly Scales Sage = new(Fix64.FromRatio(97, 100), Fix64.FromRatio(110, 100), Fix64.One);

    public static Scales Of(EconomyArchetype archetype) => archetype switch
    {
        EconomyArchetype.Grower => Grower,
        EconomyArchetype.Builder => Builder,
        EconomyArchetype.Wanderer => Wanderer,
        EconomyArchetype.Warlord => Warlord,
        EconomyArchetype.Sage => Sage,
        _ => Balanced,
    };
}
