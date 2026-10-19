using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Ai;

/// <summary>What the threat estimate reads, all of it from the belief state or our own exact state.</summary>
public readonly record struct ThreatInputs(
    int Relations, bool AtWar, bool Allied, bool InTruce,
    // 0 cannot raid yet (Dawn, no WAR-02 believed), 1 could (E1 or later), 2 can (WAR-02 believed).
    int RaidCapability,
    int WarriorsSeen, bool RaidPostureSeen, bool DistressSeen, bool RaidedUsRecently,
    // Tiles from their nearest believed settlement to our nearest; int.MaxValue when none is known.
    int DistanceTiles, int RivalReach);

/// <summary>What the opportunity estimate reads.</summary>
public readonly record struct OpportunityInputs(
    bool Contacted, bool Allied, bool InTruce, bool AtWar,
    // Strength of the raid party our best-placed settlement can send (13 §7.1 units).
    Fix64 OwnParty,
    // Believed strength of the target's defenders times the defenses seen.
    Fix64 TargetDefense,
    bool DistressSeen, bool WarriorsAwaySeen, bool GapClosing, Fix64 Confidence);

/// <summary>
/// Threat and opportunity (docs/MVP/14 §5.4). Products of simple named factors, each monotone in one observation, so a
/// log line can say which factor decided. The constants are first guesses for the harness to tune (14 §14).
/// </summary>
public static class ThreatModel
{
    /// <summary>Hostility at Relations 0; each Relations point below zero adds half a percent (−100 gives 0.6).</summary>
    public static readonly Fix64 BaseHostility = Fix64.FromRatio(10, 100);
    public static readonly Fix64 HostilityMin = Fix64.FromRatio(2, 100);
    public static readonly Fix64 HostilityMax = Fix64.FromRatio(60, 100);
    /// <summary>At war, an attack within the horizon is all but certain.</summary>
    public static readonly Fix64 WarHostility = Fix64.FromRatio(80, 100);
    /// <summary>Allies rarely turn (14 §7.2: smart AIs are mostly trustworthy); a truce costs reputation to break.</summary>
    public static readonly Fix64 AllyFactor = Fix64.FromRatio(1, 10), TruceFactor = Fix64.FromRatio(3, 10);
    /// <summary>
    /// How readily each archetype raids (Builder, Grower, Wanderer, Warlord, Sage), from the raid margins of 14 §9.1:
    /// high margins raid rarely, the Warlord's low margin twice as often as average.
    /// </summary>
    public static readonly Fix64[] RaidPropensity =
    {
        Fix64.FromRatio(6, 10), Fix64.FromRatio(6, 10), Fix64.FromRatio(9, 10), Fix64.FromInt(2), Fix64.FromRatio(6, 10),
    };
    /// <summary>Raiding needs WAR-02 (E1): believed, possible, or impossible.</summary>
    public static readonly Fix64[] CapabilityFactor = { Fix64.FromRatio(1, 10), Fix64.FromRatio(6, 10), Fix64.One };
    /// <summary>Warriors seen raise the threat up to twofold at twenty.</summary>
    public const int WarriorsForDoubleThreat = 20;
    public static readonly Fix64 RaidPostureFactor = Fix64.FromRatio(15, 10);
    /// <summary>Hungry neighbours raid (13 §3.1).</summary>
    public static readonly Fix64 DistressFactor = Fix64.FromRatio(13, 10);
    /// <summary>Whoever raided us in the last two years is likely to again.</summary>
    public static readonly Fix64 HistoryFactor = Fix64.FromRatio(15, 10);
    public const int RecentRaidSeasons = 8;
    /// <summary>Distance unknown: half weight.</summary>
    public static readonly Fix64 UnknownDistanceFactor = Fix64.Half;
    /// <summary>Never quite certain.</summary>
    public static readonly Fix64 MaxProbability = Fix64.FromRatio(95, 100);

    /// <summary>A raid party is three tenths of a settlement's fighters, at least four (13 §5.2 under Raid posture).</summary>
    public static readonly Fix64 RaidPartyShare = Fix64.FromRatio(3, 10);
    public const int MinimumParty = 4;
    /// <summary>A warrior fights at ×1.3 (13 §7.1).</summary>
    public static readonly Fix64 WarriorTraining = Fix64.FromRatio(13, 10);
    /// <summary>Settlement of unknown make-up: half its people fight, at the non-warrior defender's 0.6.</summary>
    public static readonly Fix64 UnknownFighterShare = Fix64.Half, DefenderBase = Fix64.FromRatio(6, 10);

    /// <summary>Opportunity multipliers: famine seen, their warriors away, a tech gap in our favour that is closing, war already on.</summary>
    public static readonly Fix64 OpportunityDistress = Fix64.FromRatio(13, 10), OpportunityAway = Fix64.FromRatio(125, 100),
        OpportunityGap = Fix64.FromRatio(12, 10), OpportunityWar = Fix64.FromRatio(12, 10), OpportunityTruce = Fix64.FromRatio(3, 10);

    public static Fix64 Hostility(int relations, bool atWar, bool allied, bool inTruce)
    {
        Fix64 h = Fix64.Clamp(BaseHostility - Fix64.FromRatio(relations, 200), HostilityMin, HostilityMax);
        if (atWar) h = Fix64.Max(h, WarHostility);
        if (allied) h *= AllyFactor;
        if (inTruce) h *= TruceFactor;
        return h;
    }

    /// <summary>The posterior-weighted raid propensity: 0.94 for the uniform prior, 2.0 for a sure Warlord.</summary>
    public static Fix64 Propensity(ReadOnlySpan<Fix64> posterior)
    {
        Fix64 sum = Fix64.Zero;
        for (int a = 0; a < Math.Min(posterior.Length, RaidPropensity.Length); a++) sum += posterior[a] * RaidPropensity[a];
        return sum;
    }

    /// <summary>One at or within their reach, falling to zero at three reaches, beyond which raids are not sent (13 §12.3).</summary>
    public static Fix64 DistanceFactor(int distance, int reach)
    {
        if (distance == int.MaxValue) return UnknownDistanceFactor;
        reach = Math.Max(1, reach);
        if (distance <= reach) return Fix64.One;
        return Fix64.Max(Fix64.Zero, Fix64.One - Fix64.FromRatio(distance - reach, 2 * reach));
    }

    /// <summary>P(they attack within the horizon) (14 §5.4).</summary>
    public static Fix64 Probability(in ThreatInputs t, ReadOnlySpan<Fix64> posterior)
    {
        Fix64 p = Hostility(t.Relations, t.AtWar, t.Allied, t.InTruce)
            * Propensity(posterior)
            * CapabilityFactor[Math.Clamp(t.RaidCapability, 0, 2)]
            * (Fix64.One + Fix64.FromRatio(Math.Min(Math.Max(0, t.WarriorsSeen), WarriorsForDoubleThreat), WarriorsForDoubleThreat))
            * DistanceFactor(t.DistanceTiles, t.RivalReach);
        if (t.RaidPostureSeen) p *= RaidPostureFactor;
        if (t.DistressSeen) p *= DistressFactor;
        if (t.RaidedUsRecently) p *= HistoryFactor;
        return Fix64.Clamp(p, Fix64.Zero, MaxProbability);
    }

    /// <summary>
    /// The share of what is at stake in our exposed settlement that an attack takes: their party over party plus our
    /// defense (a party as strong as our defenders takes half).
    /// </summary>
    public static Fix64 Loss(Fix64 partyStrength, Fix64 ownDefense)
    {
        Fix64 total = partyStrength + ownDefense;
        return total <= Fix64.Zero ? Fix64.Zero : partyStrength / total;
    }

    /// <summary>A raid party's strength: size from the fighters, warriors first, everyone with the weapon seen.</summary>
    public static Fix64 PartyStrength(int fighters, int warriors, Fix64 weapon)
    {
        int size = Math.Max(MinimumParty, (Fix64.FromInt(Math.Max(0, fighters)) * RaidPartyShare).RoundToInt());
        int trained = Math.Min(Math.Max(0, warriors), size);
        return weapon * size + weapon * (WarriorTraining - Fix64.One) * trained;
    }

    /// <summary>How inviting a strike on them is now (14 §5.4), from zero to one.</summary>
    public static Fix64 Opportunity(in OpportunityInputs o)
    {
        if (!o.Contacted || o.Allied || o.OwnParty <= Fix64.Zero) return Fix64.Zero;
        Fix64 v = o.OwnParty / (o.OwnParty + Fix64.Max(Fix64.Zero, o.TargetDefense));
        if (o.DistressSeen) v *= OpportunityDistress;
        if (o.WarriorsAwaySeen) v *= OpportunityAway;
        if (o.GapClosing) v *= OpportunityGap;
        if (o.AtWar) v *= OpportunityWar;
        if (o.InTruce) v *= OpportunityTruce;
        // A stale reading is worth less: half weight at no confidence.
        v *= Fix64.Half + Fix64.Half * Fix64.Clamp(o.Confidence, Fix64.Zero, Fix64.One);
        return Fix64.Clamp(v, Fix64.Zero, Fix64.One);
    }
}
