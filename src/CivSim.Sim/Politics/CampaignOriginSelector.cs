using System.Collections.Generic;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

/// <summary>A settlement's current campaign force at the point an assault is planned or launched.</summary>
public readonly record struct CampaignOriginCandidate(int Settlement, int Tile, int Adults, int Fighters);

/// <summary>Shared rally-origin rule for the campaign planner and runtime.</summary>
public static class CampaignOriginSelector
{
    public static int AvailableAttackers(int fighters)
        => System.Math.Max(0, System.Math.Min(fighters - PoliticsSystem.CampaignHomeReserve, fighters * 6 / 10));

    /// <summary>
    /// Selects the eligible origin that gathers the most attackers inside the existing radius; ties go to the nearest
    /// settlement to the target and then the first settlement in the caller's stable order. If none has eight adults,
    /// retain the existing fullest-settlement fallback.
    /// </summary>
    public static int BestOrigin(IReadOnlyList<CampaignOriginCandidate> settlements, int targetTile, int width)
    {
        int best = -1, bestAttackers = -1, bestDistance = int.MaxValue;
        int fullest = -1, mostAdults = -1;
        foreach (CampaignOriginCandidate candidate in settlements)
        {
            if (candidate.Adults > mostAdults)
            {
                mostAdults = candidate.Adults;
                fullest = candidate.Settlement;
            }
            if (candidate.Adults < 8) continue;

            int attackers = 0;
            foreach (CampaignOriginCandidate contributor in settlements)
                if (HexGrid.Distance(contributor.Tile, candidate.Tile, width) <= PoliticsSystem.CampaignGatherRange)
                    attackers += AvailableAttackers(contributor.Fighters);

            int distance = HexGrid.Distance(candidate.Tile, targetTile, width);
            if (attackers > bestAttackers || attackers == bestAttackers && distance < bestDistance)
            {
                best = candidate.Settlement;
                bestAttackers = attackers;
                bestDistance = distance;
            }
        }
        return best >= 0 ? best : fullest;
    }
}
