using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Sim.Ai;

/// <summary>A civilization's score at a moment: the three Legacy parts that exist in E0 to E2 (docs/MVP/01 §7.1).</summary>
public readonly record struct Standing(int Civ, Fix64 Dominion, Fix64 Mastery, Fix64 Firsts)
{
    public Fix64 Legacy => Dominion + Mastery + Firsts;
}

public static class Scoring
{
    public static Standing Of(MatchModel match, int civ)
        => new(civ, match.Politics.Territory.Dominion(civ), match.Research.MasteryLegacy(civ), match.Research.FirstsLegacy(civ));

    /// <summary>The civilization with the most Legacy; ties go to the lowest id.</summary>
    public static int Leader(MatchModel match)
    {
        int best = 0;
        for (int c = 1; c < match.Research.CivCount; c++)
            if (Of(match, c).Legacy > Of(match, best).Legacy) best = c;
        return best;
    }
}
