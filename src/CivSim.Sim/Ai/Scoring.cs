using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Sim.Ai;

/// <summary>A civilization's score at a moment: Dominion, Mastery, Firsts and, from Bronze, built Wonders (docs/MVP/22 §3.3).</summary>
public readonly record struct Standing(int Civ, Fix64 Dominion, Fix64 Mastery, Fix64 Firsts, Fix64 Wonders = default)
{
    public Fix64 Legacy => Dominion + Mastery + Firsts + Wonders;
}

public static class Scoring
{
    public static Standing Of(MatchModel match, int civ)
        => new(civ, match.Politics.Territory.Dominion(civ), match.Research.MasteryLegacy(civ), match.Research.FirstsLegacy(civ),
            match.Research.WondersLegacy(civ));

    /// <summary>The civilization with the most Legacy; ties go to the lowest id.</summary>
    public static int Leader(MatchModel match)
    {
        int best = 0;
        for (int c = 1; c < match.Research.CivCount; c++)
            if (Of(match, c).Legacy > Of(match, best).Legacy) best = c;
        return best;
    }
}
