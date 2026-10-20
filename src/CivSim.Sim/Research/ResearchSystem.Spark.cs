using CivSim.Sim.Numerics;

namespace CivSim.Sim.Research;

public sealed partial class ResearchSystem
{
    /// <summary>
    /// The people and scales behind a civilization's Insight (docs/MVP/02 §8.1), exactly as research reads them: own state,
    /// for the AI forecast (docs/MVP/14 §3.2) and its validation. Read-only.
    /// </summary>
    public InsightInputs ForecastInsightInputs(int civ) => _source.GetInsightInputs(civ);

    /// <summary>The Era Boon Insight multiplier a civilization has collected (docs/MVP/02 §13). Read-only.</summary>
    public Fix64 ForecastEraBoon(int civ) => _civs[civ].EraBoonInsight;
}
