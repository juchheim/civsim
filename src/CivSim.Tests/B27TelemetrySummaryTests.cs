using CivSim.Harness;
using System.Linq;
using HarnessCivOutcome = CivSim.Harness.CivOutcome;

namespace CivSim.Tests;

public sealed class B27TelemetrySummaryTests
{
    [Fact]
    public void B27_uses_tin_poor_seats_for_fallback_and_matches_for_dormant_rate()
    {
        MatchOutcome[] matches = Enumerable.Range(0, 5).Select(_ => new MatchOutcome
        {
            Seats = new[] { Player.Parse("warlord", CivSim.Sim.Economy.EconomyArchetype.Builder),
                Player.Parse("warlord", CivSim.Sim.Economy.EconomyArchetype.Builder),
                Player.Parse("council", CivSim.Sim.Economy.EconomyArchetype.Builder),
                Player.Parse("council", CivSim.Sim.Economy.EconomyArchetype.Builder) },
            Civs = new HarnessCivOutcome[4],
        }).ToArray();

        foreach (MatchOutcome match in matches)
            for (int c = 0; c < match.Civs.Length; c++)
                match.Civs[c] = new HarnessCivOutcome { Civ = c };

        // Four of ten Warlord seats reached the checkpoint, and all four found a fallback route.
        for (int c = 0; c < 4; c++)
        {
            matches[c / 2].Civs[c % 2].TinPoorEightSeasonsAfterKey3 = 1;
            matches[c / 2].Civs[c % 2].TinOrArsenicalRouteByChapterEnd = true;
        }

        // One dormant blunder is measured against five matches (0.20), not ten Warlord seats (0.10).
        matches[0].Civs[0].DormantBronzeBlunders = 1;

        B27TelemetrySummary summary = B27TelemetrySummary.FromMatches(matches);

        Assert.Equal(10, summary.WarlordSeats);
        Assert.Equal(4, summary.TinPoorSeats);
        Assert.Equal(4, summary.TinRouteSeats);
        Assert.Equal(1.0, summary.TinRouteRate);
        Assert.Equal(1, summary.DormantBlunders);
        Assert.Equal(5, summary.Matches);
        Assert.Equal(0.2, summary.DormantBlunderRate);
        Assert.False(summary.Pass);
    }
}
