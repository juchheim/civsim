using System.Collections.Generic;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;

namespace CivSim.Harness;

/// <summary>Aggregates defense timing from AI-versus-AI matches that can produce natural bronze raids.</summary>
internal readonly record struct B28TelemetrySummary(int Defended, int Cases,
    int SettlementRaids, int AiSettlementRaids, int ActualBronzeRaids, int BelievedBronzeRaids)
{
    internal enum Disposition
    {
        Pass,
        Fail,
        Incomplete,
    }

    public double DefenseRate => Cases == 0 ? double.NaN : (double)Defended / Cases;
    public Disposition Result => Cases == 0
        ? Disposition.Incomplete
        : DefenseRate >= 0.80 ? Disposition.Pass : Disposition.Fail;
    public bool Pass => Result == Disposition.Pass;

    public static B28TelemetrySummary FromMatches(IReadOnlyCollection<MatchOutcome> matches)
    {
        int defended = 0, cases = 0, settlementRaids = 0, aiSettlementRaids = 0, actualBronzeRaids = 0, believedBronzeRaids = 0;
        foreach (MatchOutcome match in matches)
        {
            settlementRaids += match.B28SettlementRaidLaunches;
            aiSettlementRaids += match.B28AiSettlementRaidLaunches;
            actualBronzeRaids += match.B28ActualBronzeRaidLaunches;
            believedBronzeRaids += match.B28BelievedBronzeRaidLaunches;
            for (int c = 0; c < match.Civs.Length; c++)
            {
                if (match.Seats[c].Control.Kind != ControlKind.Ai || !match.Civs[c].BelievedBronzeRaidMeasured) continue;
                cases++;
                if (match.Civs[c].DefendedBeforeBelievedBronzeRaid) defended++;
            }
        }

        return new B28TelemetrySummary(defended, cases, settlementRaids, aiSettlementRaids, actualBronzeRaids, believedBronzeRaids);
    }
}
