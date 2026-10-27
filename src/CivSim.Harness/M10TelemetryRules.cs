using CivSim.Sim.Ai;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using System;
using System.Collections.Generic;

namespace CivSim.Harness;

/// <summary>Pure predicates for the measured M10 AI gates, kept separate so the detector boundaries are testable.</summary>
internal static class M10TelemetryRules
{
    /// <summary>Maximum ordinary Strategist plan changes in any 12-season window; hard-event changes do not count.</summary>
    public static int MaxOrdinaryPlanSwitchesInThreeYears(IReadOnlyList<AiDecision> decisions, int civ)
    {
        var seasons = new List<int>();
        int max = 0;
        foreach (AiDecision decision in decisions)
        {
            if (decision.Civ != civ || decision.Layer != Strategist.Layer || decision.Kind != "Plan" || decision.Other < 0 ||
                !decision.Reason.StartsWith("hard=0 ", StringComparison.Ordinal)) continue;

            int first = 0;
            while (first < seasons.Count && decision.Season - seasons[first] >= 12) first++;
            if (first > 0) seasons.RemoveRange(0, first);
            seasons.Add(decision.Season);
            max = Math.Max(max, seasons.Count);
        }
        return max;
    }

    public static bool BelievesBronze(RivalBelief rival, int met04, int met05)
        => IsBelieved(rival, met04) || IsBelieved(rival, met05);

    public static bool IsProjectBelieved(RivalBelief rival, int project) => IsBelieved(rival, project);

    public static bool HasTimelyDefense(Defenses defenses)
        => defenses.Palisade || defenses.Hillfort || defenses.StoneWalls;

    public static bool IsSettlementRaidLaunch(PoliticsEvent ev)
        => ev.Kind == PoliticsEventKind.RaidLaunched && ev.B >= 0;

    public static bool LatchFallbackRoute(bool tinPoorObserved, bool alreadyLatched, bool tinAccess, bool arsenicAccess)
        => alreadyLatched || tinPoorObserved && (tinAccess || arsenicAccess);

    /// <summary>
    /// Records whether a qualifying raid target was defended using the immutable snapshot taken before
    /// the season advance that emitted the raid event. A target created during that advance is unknown
    /// to the snapshot and is conservatively treated as undefended.
    /// </summary>
    public static bool TryMeasureBelievedBronzeRaid(bool alreadyMeasured, bool believedBronze, int targetSettlement,
        Defenses[] defensesBeforeAdvance, out bool defendedBeforeRaid)
    {
        defendedBeforeRaid = false;
        if (alreadyMeasured || !believedBronze) return false;
        if ((uint)targetSettlement < (uint)defensesBeforeAdvance.Length)
            defendedBeforeRaid = HasTimelyDefense(defensesBeforeAdvance[targetSettlement]);
        return true;
    }

    /// <summary>A sighted bronze-armed warrior force within four hexes is a frontier threat.</summary>
    public static bool IsUnwalledFrontierThreat(bool bronzeBelieved, bool warriorsSeen, bool withinReach)
        => bronzeBelieved && warriorsSeen && withinReach;

    /// <summary>Count only after four full seasons without a defensive response to the observed threat.</summary>
    public static bool IsUnwalledFrontierBlunder(bool threatObserved, bool defensiveResponse, int seasonsSinceObserved)
        => threatObserved && !defensiveResponse && seasonsSinceObserved >= 4;

    /// <summary>Food is reachable from a sister settlement within the operational food-sharing radius.</summary>
    public static bool IsReachableSisterFood(bool sameCiv, bool hasStores, int distance, int reach)
        => sameCiv && hasStores && distance <= reach;

    public static bool HasReachableFood(bool ownStores, bool reachableSisterStores) => ownStores || reachableSisterStores;

    public static bool IsAvoidableStarvationDeath(EconomyDeathEvent death, bool shortfallAttributed, bool reachableFood)
        => death.Cause == EconomyDeathCause.Starvation && shortfallAttributed && reachableFood;

    /// <summary>Count a capture only when an observed threat predates it by at least two seasons and no defense appeared.</summary>
    public static bool IsUndefendedLoss(int threatSeason, int currentSeason, bool defensiveResponse)
        => threatSeason >= 0 && currentSeason - threatSeason >= 2 && !defensiveResponse;

    /// <summary>Captured events identify the defender's settlement, so A7 observation history uses that same key.</summary>
    public static bool IsUndefendedLossForSettlement(int[,] observedThreatSeasons, bool[,] defenseResponses,
        int civ, int settlement, int currentSeason)
        => (uint)civ < (uint)observedThreatSeasons.GetLength(0) && (uint)civ < (uint)defenseResponses.GetLength(0) &&
           (uint)settlement < (uint)observedThreatSeasons.GetLength(1) && (uint)settlement < (uint)defenseResponses.GetLength(1) &&
           IsUndefendedLoss(observedThreatSeasons[civ, settlement], currentSeason, defenseResponses[civ, settlement]);

    /// <summary>Idle labor is a strict adult share threshold; seasons without a food deficit are excluded.</summary>
    public static bool IsIdleLaborBlunder(int idleAdults, int adultWorkers, bool foodDeficit)
        => foodDeficit && adultWorkers > 0 && idleAdults * 100 > adultWorkers * 15;

    public static bool IsAttributedHerdCrash(int huntedToExtinctionByCiv, int civ, int civilizationCount)
        => (uint)civ < (uint)civilizationCount && huntedToExtinctionByCiv == civ;

    /// <summary>
    /// A dormant route is a blunder only when a known recovery option remains available for more than eight
    /// consecutive seasons and the civilization has not taken a recovery action or issued a recovery order.
    /// The caller must reset <paramref name="optionSeasons"/> when dormancy or the matching option ends.
    /// </summary>
    public static bool IsDormantRouteBlunder(ProjectState met04, bool arsenicOption, ProjectState met05, bool tinOption,
        bool hasAnyMetalAccess, int optionSeasons, bool recoveryActionOccurred)
        => !hasAnyMetalAccess && !recoveryActionOccurred && optionSeasons > 8 &&
            (met05 == ProjectState.Dormant && tinOption || met04 == ProjectState.Dormant && arsenicOption);

    /// <summary>Formal B30 criterion applies only to a recorded launch, not a candidate forecast.</summary>
    public static bool IsSuicideRaidLaunch(PoliticsEvent launch, AiDecision decision)
        => IsSettlementRaidLaunch(launch) && decision.Kind == "Raid" && decision.Civ == launch.Civ &&
           decision.Other == launch.Other && decision.Subject == launch.B &&
           decision.Score < Fix64.FromRatio(3, 10);

    public static bool IsUnsuppliedSiegeBlunder(int initialMembers, int remainingMembers, int foodSeasons)
        => initialMembers > 0 && foodSeasons == 0 && remainingMembers * 10 < initialMembers * 7;

    private static bool IsBelieved(RivalBelief rival, int project)
        => (uint)project < (uint)rival.ProjectBelieved.Length && rival.ProjectBelieved[project];
}
