using System;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;

namespace CivSim.Sim.Economy;

/// <summary>
/// Overseas colonies (TER-11, docs/MVP/38 §5.2, M13 WP-E6-6 slice 2). A colony is a member settlement the capital cannot reach on foot (it lies across water),
/// of a civilization that has TER-11, in the Discovery chapter. It pays the ordinary distance Overreach strain and a share of it again for the sea, a blockade
/// of its harbor strains it further, and while its Cohesion is low it rolls once a year for independence from a seeded stream (replay-stable, never by score).
/// Nothing is stored: the roll is recomputed from its key. Outside Discovery every method here answers "no colony".
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>A colony pays this share of its ordinary Overreach strain again, for the sea (provisional).</summary>
    private static readonly Fix64 ColonyStrainShare = Fix64.Half;
    /// <summary>A colony whose harbor is blockaded loses this many Cohesion points of target (provisional).</summary>
    private const int ColonyBlockadeStrain = 10;
    /// <summary>A colony rolls for independence at a year end while its Cohesion is below this (provisional).</summary>
    private const int ColonyIndependenceBelow = 45;
    /// <summary>Yearly independence odds in permille, and the extra when the colony is cut off by a blockade (provisional).</summary>
    private const int ColonyIndependencePermille = 60, ColonyCutOffPermille = 60;

    private static readonly ulong ColonyStream = Hash64.OfString("economy.colony");

    /// <summary>Whether this settlement is an overseas colony of its civilization (TER-11, Discovery only).</summary>
    private bool IsColony(SettlementState s)
    {
        if (!DiscoveryChain || s.Civic is { Seceded: true } || !Done(s.Civilization, "TER-11")) return false;
        int capital = CapitalOf(s.Civilization);
        if (capital < 0 || capital == s.Id) return false;
        int site = _settlements[capital].SiteTile;
        int[] cost = _walkFrom[site] ??= WalkingCosts(site);
        return cost[s.SiteTile] == int.MaxValue;
    }

    /// <summary>The extra Cohesion strain a colony carries: a share of its Overreach for the sea, and a blockade's.</summary>
    private Fix64 ColonyStrain(SettlementState s, Fix64 overreach)
    {
        if (!IsColony(s)) return Fix64.Zero;
        Fix64 strain = overreach * ColonyStrainShare;
        if (SiteAdvisor?.CutOffBySea(s.Id) == true) strain += Fix64.FromInt(ColonyBlockadeStrain);
        return strain;
    }

    /// <summary>A year-end independence roll for a colony in low Cohesion; true when it leaves.</summary>
    private bool ColonyBreaksAway(SettlementState s, int cohesion, int season)
    {
        if (cohesion >= ColonyIndependenceBelow || !IsColony(s)) return false;
        int odds = ColonyIndependencePermille + (SiteAdvisor?.CutOffBySea(s.Id) == true ? ColonyCutOffPermille : 0);
        Pcg32 roll = Pcg32.ForStream(_seed, ColonyStream, (ulong)s.Id, season);
        return roll.NextBelow(1000) < (uint)odds;
    }

    /// <summary>Whether a settlement is an overseas colony of its civilization right now (a founding Moment and the colony states read this).</summary>
    public bool IsOverseasColony(int settlementIndex) => (uint)settlementIndex < (uint)_settlements.Count && IsColony(_settlements[settlementIndex]);

    /// <summary>How many settlements are overseas colonies of their civilization (TER-11; a harness readout).</summary>
    public int ColonyCount()
    {
        int n = 0;
        foreach (SettlementState s in _settlements) if (IsColony(s)) n++;
        return n;
    }

    internal bool FixtureIsColony(int settlement) => IsColony(_settlements[settlement]);

    /// <summary>Tests only: the settlement secedes now, as a year-end roll would have made it.</summary>
    internal void FixtureSecede(int settlement) { SettlementState s = _settlements[settlement]; Secede(s, EnsureCivic(s)); }

    internal bool FixtureColonyBreaksAway(int settlement, int cohesion, int season) => ColonyBreaksAway(_settlements[settlement], cohesion, season);
}
