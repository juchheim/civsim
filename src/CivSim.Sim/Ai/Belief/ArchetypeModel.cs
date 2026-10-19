using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Ai;

/// <summary>Behaviour one civilization can legitimately observe of another, as evidence about its archetype (docs/MVP/14 §5.3).</summary>
public enum BeliefEvidence : byte
{
    /// <summary>A settlement of theirs not seen before appeared (after the first sighting): fission pace.</summary>
    NewSettlement,
    /// <summary>Their claimed tiles in our sight grew by five or more past the most ever counted: claims reaching out.</summary>
    ClaimsAdvance,
    /// <summary>A structure kind from a project after the Dawn was seen for the first time: tool and craft tier.</summary>
    NewCraftStructure,
    /// <summary>More warriors (WAR-04's painted figures) than ever seen before.</summary>
    WarriorsSeen,
    /// <summary>Warpaint outfits, the Raid posture signal (14 §3.1), seen after a season without.</summary>
    RaidPosture,
    /// <summary>They raided one of our settlements.</summary>
    RaidOnUs,
    /// <summary>They are at war with us since this season.</summary>
    WarOnUs,
    /// <summary>First contact left Relations below zero.</summary>
    HostileContact,
    /// <summary>First contact left Relations above zero.</summary>
    FriendlyContact,
    /// <summary>A gift party of theirs reached us.</summary>
    GiftFromThem,
    /// <summary>A First was announced for them (public Chronicle).</summary>
    FirstAnnounced,
    /// <summary>A Keystone was announced that puts them in an era ahead of ours.</summary>
    EraAhead,
    /// <summary>A thorn barrier or palisade of theirs was seen for the first time.</summary>
    DefenseSeen,
    /// <summary>A ranged weapon (atlatl, bow) was seen in their hands for the first time.</summary>
    RangedWeapon,
    Count,
}

/// <summary>
/// The archetype posterior (docs/MVP/14 §5.3): a Bayesian estimate over Builder, Grower, Wanderer, Warlord and Sage, in
/// that order, updated once per piece of evidence with the likelihood table below and renormalised. A floor keeps every
/// archetype possible, so a Grower that turns to raiding is read as a Warlord within a few raids.
/// </summary>
public static class ArchetypeModel
{
    public const int Count = 5;
    public const int Builder = 0, Grower = 1, Wanderer = 2, Warlord = 3, Sage = 4;

    /// <summary>No archetype's share falls below about 2% (the floor before the last renormalisation).</summary>
    public static readonly Fix64 Floor = Fix64.FromRatio(2, 100);

    /// <summary>
    /// P(evidence | archetype) relative to an average civilization, in percent. Rows follow <see cref="BeliefEvidence"/>,
    /// columns Builder, Grower, Wanderer, Warlord, Sage. Read from the archetype table of 14 §9.1: Growers fission fast,
    /// Wanderers claim widest and give gifts, Warlords paint warriors and raid (low raid margin), Builders raise tool tiers
    /// and walls, Sages win Firsts and race through the eras.
    /// </summary>
    private static readonly int[] LikelihoodPercent =
    {
        //  B    G    W    Wl   S
        90, 160, 120, 90, 80,     // NewSettlement
        90, 120, 160, 100, 80,    // ClaimsAdvance
        150, 100, 90, 90, 110,    // NewCraftStructure
        80, 80, 100, 200, 80,     // WarriorsSeen
        60, 60, 90, 250, 60,      // RaidPosture
        70, 70, 100, 220, 70,     // RaidOnUs
        80, 70, 90, 200, 70,      // WarOnUs
        90, 90, 90, 140, 90,      // HostileContact
        100, 110, 120, 80, 110,   // FriendlyContact
        110, 110, 140, 70, 120,   // GiftFromThem
        110, 95, 100, 90, 125,    // FirstAnnounced (one update a season at most: a leader wins Firsts whatever its style)
        120, 90, 90, 90, 150,     // EraAhead
        120, 90, 80, 120, 100,    // DefenseSeen
        90, 90, 120, 130, 90,     // RangedWeapon
    };

    public static Fix64 Likelihood(BeliefEvidence evidence, int archetype)
        => Fix64.FromRatio(LikelihoodPercent[(int)evidence * Count + archetype], 100);

    /// <summary>The prior: every archetype equally likely.</summary>
    public static void Reset(Fix64[] posterior)
    {
        for (int a = 0; a < Count; a++) posterior[a] = Fix64.FromRatio(1, Count);
        FixSum(posterior);
    }

    /// <summary>True if the posterior has never been set (all zero).</summary>
    public static bool IsUnset(Fix64[] posterior)
    {
        foreach (Fix64 p in posterior) if (p != Fix64.Zero) return false;
        return true;
    }

    /// <summary>Bayes' rule for one piece of evidence: multiply by the likelihood, renormalise, apply the floor.</summary>
    public static void Update(Fix64[] posterior, BeliefEvidence evidence)
    {
        if (posterior.Length != Count) throw new ArgumentException("Five archetypes.", nameof(posterior));
        if (IsUnset(posterior)) Reset(posterior);
        for (int a = 0; a < Count; a++) posterior[a] *= Likelihood(evidence, a);
        Normalise(posterior);
    }

    /// <summary>Scales to a sum of one, lifts every share to the floor, and scales again; the sum is exactly one.</summary>
    public static void Normalise(Fix64[] posterior)
    {
        Scale(posterior);
        for (int a = 0; a < Count; a++) posterior[a] = Fix64.Max(posterior[a], Floor);
        Scale(posterior);
    }

    private static void Scale(Fix64[] posterior)
    {
        Fix64 sum = Fix64.Zero;
        foreach (Fix64 p in posterior) sum += p;
        if (sum <= Fix64.Zero) { Reset(posterior); return; }
        for (int a = 0; a < Count; a++) posterior[a] = posterior[a] / sum;
        FixSum(posterior);
    }

    /// <summary>Puts the rounding remainder on the largest share so the shares sum to exactly one.</summary>
    private static void FixSum(Fix64[] posterior)
    {
        Fix64 sum = Fix64.Zero;
        int largest = 0;
        for (int a = 0; a < Count; a++)
        {
            sum += posterior[a];
            if (posterior[a] > posterior[largest]) largest = a;
        }
        posterior[largest] += Fix64.One - sum;
    }

    /// <summary>The most likely archetype; ties go to the lowest index.</summary>
    public static int Mode(Fix64[] posterior)
    {
        int best = 0;
        for (int a = 1; a < posterior.Length; a++) if (posterior[a] > posterior[best]) best = a;
        return best;
    }
}
