using CivSim.Harness;
using CivSim.Sim.Ai;
using CivSim.Sim.Economy;
using HarnessCivOutcome = CivSim.Harness.CivOutcome;

namespace CivSim.Tests;

public sealed class B28TelemetrySummaryTests
{
    [Fact]
    public void B28_aggregates_only_first_believed_bronze_raid_cases_from_AI_defenders()
    {
        var match = new MatchOutcome
        {
            Seats = new[]
            {
                Player.Parse("warlord", EconomyArchetype.Balanced),
                Player.Parse("legend", EconomyArchetype.Balanced),
                Player.Parse("council", EconomyArchetype.Balanced),
                Player.Parse("warlord", EconomyArchetype.Balanced),
            },
            Civs = new[]
            {
                new HarnessCivOutcome { BelievedBronzeRaidMeasured = true, DefendedBeforeBelievedBronzeRaid = true },
                new HarnessCivOutcome { BelievedBronzeRaidMeasured = true },
                new HarnessCivOutcome { BelievedBronzeRaidMeasured = true, DefendedBeforeBelievedBronzeRaid = true },
                new HarnessCivOutcome(),
            },
            B28SettlementRaidLaunches = 8,
            B28AiSettlementRaidLaunches = 6,
            B28ActualBronzeRaidLaunches = 4,
            B28BelievedBronzeRaidLaunches = 3,
        };

        B28TelemetrySummary summary = B28TelemetrySummary.FromMatches(new[] { match });

        Assert.Equal(1, summary.Defended);
        Assert.Equal(2, summary.Cases);
        Assert.Equal(0.5, summary.DefenseRate);
        Assert.Equal(8, summary.SettlementRaids);
        Assert.Equal(6, summary.AiSettlementRaids);
        Assert.Equal(4, summary.ActualBronzeRaids);
        Assert.Equal(3, summary.BelievedBronzeRaids);
        Assert.False(summary.Pass);
    }

    [Fact]
    public void B28_fails_when_no_AI_has_a_qualifying_raid_opportunity()
    {
        var match = new MatchOutcome
        {
            Seats = new[] { Player.Parse("warlord", EconomyArchetype.Balanced) },
            Civs = new[] { new HarnessCivOutcome() },
        };

        B28TelemetrySummary summary = B28TelemetrySummary.FromMatches(new[] { match });

        Assert.Equal(0, summary.Cases);
        Assert.Equal(0, summary.SettlementRaids);
        Assert.True(double.IsNaN(summary.DefenseRate));
        Assert.False(summary.Pass);
    }
}
