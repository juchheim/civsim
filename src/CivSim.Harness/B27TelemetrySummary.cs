using System.Collections.Generic;

namespace CivSim.Harness;

/// <summary>Aggregates the B27 sample and match-level gate denominators for a Warlord duel.</summary>
internal readonly record struct B27TelemetrySummary(
    int WarlordSeats,
    int TinPoorSeats,
    int TinRouteSeats,
    int DormantBlunders,
    int Matches)
{
    public double TinRouteRate => TinPoorSeats == 0 ? double.NaN : (double)TinRouteSeats / TinPoorSeats;
    public double DormantBlunderRate => Matches == 0 ? double.NaN : (double)DormantBlunders / Matches;
    public bool Pass => TinPoorSeats > 0 && TinRouteRate >= 0.80 && DormantBlunderRate < 0.10;

    public static B27TelemetrySummary FromMatches(IReadOnlyCollection<MatchOutcome> matches)
    {
        int warlordSeats = 0, tinPoor = 0, tinRoute = 0, dormant = 0;
        foreach (MatchOutcome match in matches)
            for (int c = 0; c < match.Civs.Length; c++)
            {
                if (match.Seats[c].Name != "warlord") continue;
                warlordSeats++;
                CivOutcome outcome = match.Civs[c];
                if (outcome.TinPoorEightSeasonsAfterKey3 != 0)
                {
                    tinPoor++;
                    if (outcome.TinOrArsenicalRouteByChapterEnd) tinRoute++;
                }
                dormant += outcome.DormantBronzeBlunders;
            }

        return new B27TelemetrySummary(warlordSeats, tinPoor, tinRoute, dormant, matches.Count);
    }
}
