using System;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;

namespace CivSim.Sim.Ai;

/// <summary>
/// The observation rules of the belief state (docs/MVP/14 §3.1, 13 §2): how far a civilization sees, how noisy a head
/// count is and how fast certainty fades. Pure functions of their arguments, so they can be tested alone.
/// </summary>
public static class BeliefRules
{
    /// <summary>A figure sees three tiles around it (13 §2.1; terrain modifiers are left out, see BeliefBuilder).</summary>
    public const int SightRadius = 3;

    /// <summary>A head count of a seen settlement is off by up to a quarter either way (14 §3.1 "band ±25%").</summary>
    public static readonly Fix64 PopulationNoise = Fix64.FromRatio(1, 4);

    /// <summary>Confidence keeps 85% of itself for every season a rival goes unseen: half after four seasons, a fifth after ten.</summary>
    public static readonly Fix64 ConfidenceDecay = Fix64.FromRatio(85, 100);

    /// <summary>A rival seen once is never forgotten entirely.</summary>
    public static readonly Fix64 ConfidenceFloor = Fix64.FromRatio(5, 100);

    /// <summary>A settlement whose last season's food security was under 0.9 shows hungry people (13 §3.1 "hungry neighbours raid").</summary>
    public static readonly Fix64 DistressFoodSecurity = Fix64.FromRatio(9, 10);

    /// <summary>Tiles a civilization's people see from its settlements every season: its reach plus a figure's sight (13 §2.1 to §2.2).</summary>
    public static int RoutineSight(int reach) => reach + SightRadius;

    /// <summary>
    /// How far scouts range from a settlement once a rival is known (13 §2.3: "toward known rivals' last-seen positions"):
    /// the reach, half again with WAY-01's scouts and twice with WAY-03.
    /// </summary>
    public static int ScoutRange(int reach, bool way01, bool way03) => way03 ? 2 * reach : way01 ? reach * 3 / 2 : reach;

    /// <summary>
    /// Settlements this far apart have figures in each other's sight: both ranges plus a tenth for trails, plus sight, the
    /// rule awareness itself follows (13 §12.2 point 5).
    /// </summary>
    public static int FigureSight(int reachObserver, int reachRival) => (reachObserver + reachRival) * 11 / 10 + SightRadius;

    /// <summary>
    /// The factor a head count is off by when <paramref name="observer"/> looks at <paramref name="settlement"/> in
    /// <paramref name="season"/>: uniform in [0.75, 1.25] in steps of 0.0001, from a stream of its own (docs/MVP/15 D3).
    /// </summary>
    public static Fix64 NoiseFactor(ulong matchSeed, int observer, int settlement, int season)
    {
        ulong stream = unchecked(NoiseStream ^ (ulong)observer * 0x9E3779B97F4A7C15UL);
        Pcg32 rng = Pcg32.ForStream(matchSeed, stream, (ulong)settlement, season);
        uint step = rng.NextBelow(5001);
        return Fix64.FromRatio(7500 + step, 10000);
    }

    private static readonly ulong NoiseStream = Hash64.OfString("ai.belief.noise");

    /// <summary>A noisy count: the truth times the factor, rounded, never zero for a non-empty group.</summary>
    public static int Estimate(int truth, Fix64 factor)
    {
        if (truth <= 0) return 0;
        return Math.Max(1, (Fix64.FromInt(truth) * factor).RoundToInt());
    }

    /// <summary>One right after a sighting, times <see cref="ConfidenceDecay"/> per season since, never below the floor; zero if never seen.</summary>
    public static Fix64 Confidence(int seasonsSinceSeen)
    {
        if (seasonsSinceSeen < 0) return Fix64.Zero;
        Fix64 c = Fix64.One;
        for (int i = 0; i < Math.Min(seasonsSinceSeen, 40); i++) c *= ConfidenceDecay;
        return Fix64.Max(c, ConfidenceFloor);
    }
}
