using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

/// <summary>Chooses an exposed own settlement for physical defense using only the AI's rival beliefs.</summary>
internal static class BronzeDefensePolicy
{
    private static readonly Fix64 MinimumThreatProbability = Fix64.FromRatio(1, 10);

    public static int SelectSettlement(int civ, int season, int mapWidth, int ownReach,
        IReadOnlyList<OwnSettlement> ownSettlements, RivalBelief[] rivals, int met04, int met05)
    {
        int bestSettlement = -1;
        int bestDistance = int.MaxValue;
        int defenseRange = StrategistTuning.RaidRangeReaches * Math.Max(6, ownReach);
        foreach (RivalBelief rival in rivals)
        {
            if (rival.Civ == civ || !BelievesBronze(rival, met04, met05) ||
                !(rival.AtWar || rival.ThreatProbability >= MinimumThreatProbability ||
                  season - rival.LastRaidOnUsSeason is >= 0 and <= ThreatModel.RecentRaidSeasons)) continue;

            foreach (BeliefSettlement site in rival.Settlements)
            {
                if (season - site.LastSeenSeason is < 0 or > 8) continue;
                foreach (OwnSettlement own in ownSettlements)
                {
                    if (HasTimelyDefense(own.Defenses)) continue;
                    int distance = HexGrid.Distance(own.Tile, site.Tile, mapWidth);
                    if (distance > defenseRange || distance >= bestDistance) continue;
                    bestDistance = distance;
                    bestSettlement = own.Index;
                }
            }
        }
        return bestSettlement;
    }

    internal static bool BelievesBronze(RivalBelief rival, int met04, int met05)
        => IsBelieved(rival, met04) || IsBelieved(rival, met05);

    private static bool IsBelieved(RivalBelief rival, int project)
        => (uint)project < (uint)rival.ProjectBelieved.Length && rival.ProjectBelieved[project];

    private static bool HasTimelyDefense(Defenses defenses)
        => defenses.Palisade || defenses.Hillfort || defenses.StoneWalls;
}
