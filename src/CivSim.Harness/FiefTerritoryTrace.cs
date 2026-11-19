using System;
using System.Globalization;
using System.Linq;
using System.Text;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;

namespace CivSim.Harness;

/// <summary>Opt-in descriptive F-T8 observations. This trace does not change simulation state or adjudicate a match end.</summary>
internal static class FiefTerritoryTrace
{
    internal const int SeasonsForDominionRule = 20;

    internal sealed class State
    {
        internal readonly Fix64[] LastAnnualDominion;
        internal readonly bool[] Key4Sampled;
        internal readonly bool[] Key5Sampled;
        internal int CandidateCiv = -1;
        internal int CandidateSeasons;

        internal State(Territory territory, int civilizations)
        {
            LastAnnualDominion = Enumerable.Range(0, civilizations).Select(territory.Dominion).ToArray();
            Key4Sampled = new bool[civilizations];
            Key5Sampled = new bool[civilizations];
        }
    }

    internal readonly record struct DominionCandidate(int Civilization, int LeaderOwnedWeight, int RunnerUpOwnedWeight,
        int TotalHabitableWeight)
    {
        internal bool Qualifies
            => 2L * LeaderOwnedWeight >= TotalHabitableWeight && LeaderOwnedWeight >= 2L * RunnerUpOwnedWeight;
    }

    /// <summary>Returns the unique top land-share civilization and its runner-up share, if the ≥50% test is reachable.</summary>
    internal static DominionCandidate? FindDominionCandidate(ReadOnlySpan<int> ownedWeights, int totalHabitableWeight)
    {
        if (ownedWeights.Length < 2 || totalHabitableWeight <= 0) return null;
        int leader = -1, leaderShare = -1, runnerUp = -1;
        bool tiedLeader = false;
        for (int civ = 0; civ < ownedWeights.Length; civ++)
        {
            int share = ownedWeights[civ];
            if (share > leaderShare)
            {
                runnerUp = leaderShare;
                leader = civ;
                leaderShare = share;
                tiedLeader = false;
            }
            else if (share == leaderShare)
            {
                runnerUp = share;
                tiedLeader = true;
            }
            else if (share > runnerUp)
            {
                runnerUp = share;
            }
        }
        return leader < 0 || tiedLeader ? null : new DominionCandidate(leader, leaderShare, Math.Max(0, runnerUp), totalHabitableWeight);
    }

    /// <summary>Advance an elapsed-season streak between samples; a qualifying first sample starts at zero elapsed seasons.</summary>
    internal static (int CandidateCiv, int ConsecutiveSeasons) AdvanceCandidateStreak(
        int priorCiv, int priorSeasons, int candidateCiv, bool qualifies)
    {
        if (!qualifies || candidateCiv < 0) return (-1, 0);
        return candidateCiv == priorCiv ? (candidateCiv, checked(priorSeasons + 1)) : (candidateCiv, 0);
    }

    internal static double? CitySharePercent(int cityPopulation, int totalPopulation)
        => totalPopulation <= 0 ? null : 100.0 * cityPopulation / totalPopulation;

    internal static double PercentOf(int part, int whole)
        => whole <= 0 ? 0 : 100.0 * part / whole;

    internal static Fix64 ExpectedGrossAnnualDominion(int ownedHabitableWeight, int totalOwnedHabitableWeight)
        => totalOwnedHabitableWeight <= 0
            ? Fix64.Zero
            : Fix64.FromRatio(100L * ownedHabitableWeight, totalOwnedHabitableWeight);

    internal static bool DominionRuleSatisfied(bool qualifies, int consecutiveSeasons)
        => qualifies && consecutiveSeasons >= SeasonsForDominionRule;

    internal static void Append(StringBuilder output, ulong seed, int completedSeason, MatchModel match, State state)
    {
        Territory territory = match.Politics.Territory;
        ResearchSystem research = match.Research;
        int key4 = research.Catalog.IndexOf("KEY-4"), key5 = research.Catalog.IndexOf("KEY-5");
        int civs = match.Economy.CivilizationCount;
        int[] population = new int[civs], cityPopulation = new int[civs], shares = new int[civs], ownedWeights = new int[civs];
        int allOwnedTiles = 0, totalOwnedWeight = 0;
        for (int civ = 0; civ < civs; civ++)
        {
            shares[civ] = territory.ShareBasisPoints(civ);
            ownedWeights[civ] = territory.OwnedWeight(civ);
            totalOwnedWeight += ownedWeights[civ];
            allOwnedTiles += territory.OwnedTiles(civ);
        }
        for (int settlement = 0; settlement < match.Economy.SettlementCount; settlement++)
        {
            var lite = match.Economy.SettlementLite(settlement);
            int civ = lite.Civilization;
            if ((uint)civ >= (uint)civs || match.Politics.IsFreeFolk(settlement)) continue;
            int people = match.Economy.Population(lite.Id);
            population[civ] += people;
            if (match.Economy.Tier(settlement) == SettlementTier.City) cityPopulation[civ] += people;
        }
        DominionCandidate? candidate = FindDominionCandidate(ownedWeights, territory.TotalHabitableWeight);
        bool qualifies = candidate is DominionCandidate value && value.Qualifies;
        (state.CandidateCiv, state.CandidateSeasons) = AdvanceCandidateStreak(
            state.CandidateCiv, state.CandidateSeasons, candidate?.Civilization ?? -1, qualifies);

        int claimedBasisPoints = territory.ClaimedBasisPoints();
        double worldTileFillPercent = PercentOf(allOwnedTiles, territory.TileCount);
        string candidateCiv = candidate?.Civilization.ToString(CultureInfo.InvariantCulture) ?? "none";
        int candidateShare = candidate is DominionCandidate leader ? shares[leader.Civilization] : 0;
        int runnerUpShare = candidate is DominionCandidate runnerUp && territory.TotalHabitableWeight > 0
            ? (int)(10_000L * runnerUp.RunnerUpOwnedWeight / territory.TotalHabitableWeight)
            : 0;
        bool ruleSatisfied = DominionRuleSatisfied(qualifies, state.CandidateSeasons);
        bool annualCheckpoint = completedSeason > 0 && completedSeason % 4 == 0;

        for (int civ = 0; civ < civs; civ++)
        {
            Fix64 dominion = territory.Dominion(civ);
            string annualGross = "na", annualNet = "na";
            if (annualCheckpoint)
            {
                Fix64 gross = ExpectedGrossAnnualDominion(territory.OwnedWeight(civ), totalOwnedWeight);
                annualGross = F(gross);
                annualNet = F(dominion - state.LastAnnualDominion[civ]);
                state.LastAnnualDominion[civ] = dominion;
            }
            double? cityShareValue = CitySharePercent(cityPopulation[civ], population[civ]);
            string cityShare = cityShareValue?.ToString("F2", CultureInfo.InvariantCulture) ?? "na";
            string mapShare = PercentOf(shares[civ], 10_000).ToString("F2", CultureInfo.InvariantCulture);
            bool key4Done = research.IsDone(civ, key4), key5Done = research.IsDone(civ, key5);
            long key4Tick = key4Done ? research.CompletedTick(civ, key4) : -1;
            long key5Tick = key5Done ? research.CompletedTick(civ, key5) : -1;
            bool key4Sample = key4Done && !state.Key4Sampled[civ];
            bool key5Sample = key5Done && !state.Key5Sampled[civ];
            state.Key4Sampled[civ] |= key4Done;
            state.Key5Sampled[civ] |= key5Done;
            string phase = !key4Done ? "pre-own-key4" : key5Done ? "post-own-key5" : "own-key4-to-key5";
            int key4Season = key4Tick < 0 ? -1 : checked((int)(key4Tick / 150L));
            int key5Season = key5Tick < 0 ? -1 : checked((int)(key5Tick / 150L));
            int offsetFromKey4 = key4Season < 0 ? -1 : completedSeason - key4Season;
            int offsetFromKey5 = key5Season < 0 ? -1 : completedSeason - key5Season;
            // This is a season-boundary observation. The completion ticks identify exact research events;
            // ownKey4Sample/ownKey5Sample mark the first report row at or after each event.
            output.AppendLine($"fief-territory seed={seed} season={completedSeason} sampleTick={completedSeason * 150L} civ={civ} samplePhase={phase} ownKey4Sample={key4Sample.ToString().ToLowerInvariant()} ownKey4CompletedTick={key4Tick} ownKey4Season={key4Season} seasonOffsetFromOwnKey4={offsetFromKey4} ownKey5Sample={key5Sample.ToString().ToLowerInvariant()} ownKey5CompletedTick={key5Tick} ownKey5Season={key5Season} seasonOffsetFromOwnKey5={offsetFromKey5} population={population[civ]} cityPopulation={cityPopulation[civ]} citySharePercent={cityShare} mapFillClaimedHabitablePercent={PercentOf(claimedBasisPoints, 10_000).ToString("F2", CultureInfo.InvariantCulture)} mapFillOwnedTilePercent={worldTileFillPercent.ToString("F2", CultureInfo.InvariantCulture)} civOwnedHabitableSharePercent={mapShare} dominionPoints={F(dominion)} dominionExpectedGrossAnnual={annualGross} dominionObservedNetAnnual={annualNet} dominionRuleLeaderCiv={candidateCiv} dominionRuleLeaderSharePercent={PercentOf(candidateShare, 10_000).ToString("F2", CultureInfo.InvariantCulture)} dominionRuleRunnerUpSharePercent={PercentOf(runnerUpShare, 10_000).ToString("F2", CultureInfo.InvariantCulture)} dominionRuleLeaderQualifies={qualifies.ToString().ToLowerInvariant()} dominionRuleConsecutiveElapsedSeasons={state.CandidateSeasons} dominionRuleSatisfied={ruleSatisfied.ToString().ToLowerInvariant()} dominionEndImplemented=false");
        }
    }

    private static string F(Fix64 value) => value.ToDoubleForDisplay().ToString("F4", CultureInfo.InvariantCulture);
}
