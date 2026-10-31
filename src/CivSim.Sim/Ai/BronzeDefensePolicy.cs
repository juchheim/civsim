using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

/// <summary>Chooses an exposed own settlement for physical defense using only the AI's rival beliefs.</summary>
internal static class BronzeDefensePolicy
{
    internal readonly record struct PreparationTarget(int Settlement, bool Urgent);
    private readonly record struct SettlementCandidate(int Settlement, int Distance, int Defense);

    private sealed class RivalPreparationCandidates
    {
        internal RivalBelief Rival = null!;
        internal bool BelievesBronze;
        internal int NearestDistance;
        internal List<SettlementCandidate> Settlements = new();
    }

    internal readonly record struct PublicEraShadowTarget(int Settlement, int Rival, int Site, int SiteAge,
        int Distance, Fix64 ThreatProbability);

    /// <summary>Shadow-only target selection for contacted public-E3 rivals; does not issue a defense order.</summary>
    internal static bool TrySelectPublicEraShadowTarget(int civ, int season, int mapWidth, int ownReach,
        IReadOnlyList<OwnSettlement> ownSettlements, RivalBelief[] rivals, out PublicEraShadowTarget selected)
    {
        int bestSettlement = -1, bestRival = -1, bestSite = -1, bestAge = -1;
        int bestDistance = int.MaxValue, bestDefense = int.MaxValue;
        Fix64 bestThreat = Fix64.Zero;
        int defenseRange = StrategistTuning.RaidRangeReaches * Math.Max(6, ownReach);
        foreach (RivalBelief rival in rivals)
        {
            if (rival.Civ == civ || !rival.Contacted || rival.Era < 3) continue;
            foreach (BeliefSettlement site in rival.Settlements)
            {
                int age = season - site.LastSeenSeason;
                if (age is < 0 or > 8) continue;
                foreach (OwnSettlement own in ownSettlements)
                {
                    if (HasTimelyDefense(own.Defenses)) continue;
                    int distance = HexGrid.Distance(own.Tile, site.Tile, mapWidth);
                    int defense = DefenseStrength(own.Defenses);
                    if (distance > defenseRange || distance > bestDistance ||
                        distance == bestDistance && (defense > bestDefense || defense == bestDefense && own.Index >= bestSettlement)) continue;
                    bestSettlement = own.Index;
                    bestRival = rival.Civ;
                    bestSite = site.Settlement;
                    bestAge = age;
                    bestDistance = distance;
                    bestDefense = defense;
                    bestThreat = rival.ThreatProbability;
                }
            }
        }

        selected = bestSettlement < 0
            ? new PublicEraShadowTarget(-1, -1, -1, -1, int.MaxValue, Fix64.Zero)
            : new PublicEraShadowTarget(bestSettlement, bestRival, bestSite, bestAge, bestDistance, bestThreat);
        return bestSettlement >= 0;
    }

    public static int SelectSettlement(int civ, int season, int mapWidth, int ownReach,
        IReadOnlyList<OwnSettlement> ownSettlements, RivalBelief[] rivals, int met04, int met05,
        bool proactivePublicEra = false)
    {
        foreach (OwnSettlement own in ownSettlements)
            if (own.PhysicalDefenseQueued) return -1;

        int bestSettlement = -1;
        int bestDistance = int.MaxValue;
        int bestDefense = int.MaxValue;
        int defenseRange = StrategistTuning.RaidRangeReaches * Math.Max(6, ownReach);
        // Start the defense plan while a known Bronze opponent is still an extra reach away.
        // The normal raid selector cannot wait for its aggregate threat score here: it is
        // intentionally slow to rise when the attack is a long way from launch.
        int preparationRange = defenseRange + Math.Max(6, ownReach);
        foreach (RivalBelief rival in rivals)
        {
            bool publicBronze = proactivePublicEra && rival.Era >= 3;
            bool believesBronze = BelievesBronze(rival, met04, met05);
            if (rival.Civ == civ || !(publicBronze || believesBronze)) continue;

            foreach (BeliefSettlement site in rival.Settlements)
            {
                if (season - site.LastSeenSeason is < 0 or > 8) continue;
                foreach (OwnSettlement own in ownSettlements)
                {
                    if (HasTimelyDefense(own.Defenses) || own.PhysicalDefenseQueued) continue;
                    int distance = HexGrid.Distance(own.Tile, site.Tile, mapWidth);
                    int range = proactivePublicEra ? defenseRange : preparationRange;
                    if (distance > range) continue;
                    int defense = DefenseStrength(own.Defenses);
                    if (distance > bestDistance || distance == bestDistance &&
                        (defense > bestDefense || defense == bestDefense && own.Index >= bestSettlement)) continue;
                    bestDistance = distance;
                    bestDefense = defense;
                    bestSettlement = own.Index;
                }
            }
        }
        return bestSettlement;
    }

    /// <summary>
    /// Prepares every currently known exposed settlement. High-confidence rivals get one exposed
    /// settlement each before any rival receives a second target; early-era contacts are prepared
    /// as lower-priority hedges.
    /// </summary>
    public static PreparationTarget[] SelectPreparationTargets(int civ, int season, int mapWidth, int ownReach,
        IReadOnlyList<OwnSettlement> ownSettlements, RivalBelief[] rivals, int met04, int met05)
    {
        int preparationRange = StrategistTuning.RaidRangeReaches * Math.Max(6, ownReach) + Math.Max(6, ownReach);
        var highConfidence = new List<RivalPreparationCandidates>();
        var earlyHedges = new List<RivalPreparationCandidates>();
        foreach (RivalBelief rival in rivals)
        {
            if (rival.Civ == civ) continue;
            bool believesBronze = BelievesBronze(rival, met04, met05);
            bool publicEra3 = rival.Contacted && rival.Era >= 3;
            bool urgent = believesBronze || publicEra3;
            bool earlyHedge = !urgent && rival.Contacted && rival.Era is >= 1 and <= 2;
            if (!urgent && !earlyHedge) continue;

            var candidates = new List<SettlementCandidate>();
            foreach (OwnSettlement own in ownSettlements)
            {
                int nearestDistance = int.MaxValue;
                foreach (BeliefSettlement site in rival.Settlements)
                {
                    if (season - site.LastSeenSeason is < 0 or > 8) continue;
                    int distance = HexGrid.Distance(own.Tile, site.Tile, mapWidth);
                    if (distance <= preparationRange && distance < nearestDistance) nearestDistance = distance;
                }
                if (nearestDistance != int.MaxValue)
                    candidates.Add(new SettlementCandidate(own.Index, nearestDistance, DefenseStrength(own.Defenses)));
            }
            if (candidates.Count == 0) continue;
            candidates.Sort(CompareSettlementCandidates);
            var preparation = new RivalPreparationCandidates
            {
                Rival = rival,
                BelievesBronze = believesBronze,
                NearestDistance = candidates[0].Distance,
                Settlements = candidates,
            };
            (urgent ? highConfidence : earlyHedges).Add(preparation);
        }

        highConfidence.Sort(CompareHighConfidenceRivals);
        earlyHedges.Sort(CompareEarlyHedgeRivals);

        // Fill all exposed sites in rounds: cover each high-confidence rival once before
        // assigning a second site to any one rival. Defended/queued candidates stay in the
        // risk ordering but no longer consume a fixed two-site lifetime budget.
        var highTargets = new List<int>(ownSettlements.Count);
        bool added;
        do
        {
            added = false;
            foreach (RivalPreparationCandidates rival in highConfidence)
            {
                if (highTargets.Count >= ownSettlements.Count) break;
                if (!TryAddNextDistinctSettlement(rival, highTargets, out int settlement)) continue;
                highTargets.Add(settlement);
                added = true;
            }
        } while (added && highTargets.Count < ownSettlements.Count);

        var targets = new List<PreparationTarget>(ownSettlements.Count);
        foreach (int settlement in highTargets)
        {
            if (IsAlreadyDefendedOrQueued(ownSettlements, settlement)) continue;
            targets.Add(new PreparationTarget(settlement, Urgent: true));
        }

        // If Bronze is believed but no fresh enemy settlement is known (or only some own
        // sites fall inside the known envelope), the raid can still choose any settlement.
        // Queue the remaining sites as urgent preparation rather than leaving them unplanned.
        bool anyBelievedBronze = false;
        foreach (RivalBelief rival in rivals)
            if (rival.Civ != civ && BelievesBronze(rival, met04, met05))
            {
                anyBelievedBronze = true;
                break;
            }
        if (anyBelievedBronze)
        {
            var fallback = new List<SettlementCandidate>();
            foreach (OwnSettlement own in ownSettlements)
                if (!Contains(highTargets, own.Index))
                    fallback.Add(new SettlementCandidate(own.Index, 0, DefenseStrength(own.Defenses)));
            fallback.Sort(static (a, b) =>
            {
                int defense = a.Defense.CompareTo(b.Defense);
                return defense != 0 ? defense : a.Settlement.CompareTo(b.Settlement);
            });
            foreach (SettlementCandidate candidate in fallback)
            {
                if (IsAlreadyDefendedOrQueued(ownSettlements, candidate.Settlement)) continue;
                targets.Add(new PreparationTarget(candidate.Settlement, Urgent: true));
                highTargets.Add(candidate.Settlement);
            }
        }

        // Early public-era exposure is a weaker signal, but each known exposed settlement
        // can still be staged in parallel. Existing/queued defenses only suppress themselves.
        foreach (RivalPreparationCandidates rival in earlyHedges)
        {
            foreach (SettlementCandidate candidate in rival.Settlements)
            {
                if (Contains(highTargets, candidate.Settlement) ||
                    IsAlreadyDefendedOrQueued(ownSettlements, candidate.Settlement)) continue;
                targets.Add(new PreparationTarget(candidate.Settlement, Urgent: false));
                highTargets.Add(candidate.Settlement);
            }
        }
        return targets.ToArray();
    }

    private static int CompareSettlementCandidates(SettlementCandidate a, SettlementCandidate b)
    {
        int distance = a.Distance.CompareTo(b.Distance);
        if (distance != 0) return distance;
        int defense = a.Defense.CompareTo(b.Defense);
        return defense != 0 ? defense : a.Settlement.CompareTo(b.Settlement);
    }

    private static int CompareHighConfidenceRivals(RivalPreparationCandidates a, RivalPreparationCandidates b)
    {
        if (a.BelievesBronze != b.BelievesBronze) return a.BelievesBronze ? -1 : 1;
        if (a.Rival.ThreatProbability > b.Rival.ThreatProbability) return -1;
        if (a.Rival.ThreatProbability < b.Rival.ThreatProbability) return 1;
        int distance = a.NearestDistance.CompareTo(b.NearestDistance);
        return distance != 0 ? distance : a.Rival.Civ.CompareTo(b.Rival.Civ);
    }

    private static int CompareEarlyHedgeRivals(RivalPreparationCandidates a, RivalPreparationCandidates b)
    {
        int distance = a.NearestDistance.CompareTo(b.NearestDistance);
        if (distance != 0) return distance;
        if (a.Rival.ThreatProbability > b.Rival.ThreatProbability) return -1;
        if (a.Rival.ThreatProbability < b.Rival.ThreatProbability) return 1;
        return a.Rival.Civ.CompareTo(b.Rival.Civ);
    }

    private static bool TryAddNextDistinctSettlement(RivalPreparationCandidates rival, List<int> selected, out int settlement)
    {
        foreach (SettlementCandidate candidate in rival.Settlements)
        {
            if (Contains(selected, candidate.Settlement)) continue;
            settlement = candidate.Settlement;
            return true;
        }
        settlement = -1;
        return false;
    }

    private static bool IsAlreadyDefendedOrQueued(IReadOnlyList<OwnSettlement> ownSettlements, int settlement)
    {
        foreach (OwnSettlement own in ownSettlements)
            if (own.Index == settlement) return own.PhysicalDefenseQueued || HasTimelyDefense(own.Defenses);
        return false;
    }

    private static bool Contains(List<int> values, int value)
    {
        foreach (int current in values) if (current == value) return true;
        return false;
    }

    /// <summary>Default-off diagnostic hedge: order the exposed capital and newest frontier using fresh sightings.</summary>
    public static int[] SelectHedgeSettlements(int civ, int season, int mapWidth, int ownReach,
        IReadOnlyList<OwnSettlement> ownSettlements, RivalBelief[] rivals, int met04, int met05)
    {
        bool believedBronze = false;
        foreach (RivalBelief rival in rivals)
        {
            if (rival.Civ != civ && BelievesBronze(rival, met04, met05))
            {
                believedBronze = true;
                break;
            }
        }
        if (!believedBronze) return Array.Empty<int>();

        OwnSettlement? capital = null;
        OwnSettlement? frontier = null;
        foreach (OwnSettlement own in ownSettlements)
        {
            if (HasTimelyDefense(own.Defenses) || own.PhysicalDefenseQueued) continue;
            if (capital is null || own.Index < capital.Index) capital = own;
            if (frontier is null || own.Index > frontier.Index) frontier = own;
        }
        if (capital is null) return Array.Empty<int>();
        if (frontier is null || frontier.Index == capital.Index) return new[] { capital.Index };

        int defenseRange = StrategistTuning.RaidRangeReaches * Math.Max(6, ownReach);
        int capitalDistance = NearestFreshSiteDistance(civ, capital, rivals, season, mapWidth, defenseRange, met04, met05);
        int frontierDistance = NearestFreshSiteDistance(civ, frontier, rivals, season, mapWidth, defenseRange, met04, met05);
        return frontierDistance < capitalDistance
            ? new[] { frontier.Index, capital.Index }
            : new[] { capital.Index, frontier.Index };
    }

    private static int NearestFreshSiteDistance(int civ, OwnSettlement own, RivalBelief[] rivals, int season,
        int mapWidth, int defenseRange, int met04, int met05)
    {
        int nearest = int.MaxValue;
        foreach (RivalBelief rival in rivals)
        {
            if (rival.Civ == civ || !BelievesBronze(rival, met04, met05)) continue;
            foreach (BeliefSettlement site in rival.Settlements)
            {
                if (season - site.LastSeenSeason is < 0 or > 8) continue;
                int distance = HexGrid.Distance(own.Tile, site.Tile, mapWidth);
                if (distance <= defenseRange && distance < nearest) nearest = distance;
            }
        }
        return nearest;
    }

    internal static bool BelievesBronze(RivalBelief rival, int met04, int met05)
        => IsBelieved(rival, met04) || IsBelieved(rival, met05);

    private static bool IsBelieved(RivalBelief rival, int project)
        => (uint)project < (uint)rival.ProjectBelieved.Length && rival.ProjectBelieved[project];

    private static bool HasTimelyDefense(Defenses defenses)
        => defenses.Palisade || defenses.Hillfort || defenses.StoneWalls;

    private static int DefenseStrength(Defenses d)
        => (d.ThornBarrier ? 2 : 0) + (d.Watchers ? 1 : 0) + (d.Dogs ? 1 : 0) +
           (d.SignalFires ? 1 : 0) + (d.AlarmCalls ? 1 : 0) + (d.Archery ? 1 : 0) + (d.Night ? 1 : 0);
}
