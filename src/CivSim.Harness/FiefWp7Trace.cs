using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;

namespace CivSim.Harness;

/// <summary>Read-only season-boundary exposures used by the opt-in WP-E5-7 trace.</summary>
internal static class FiefWp7Trace
{
    internal readonly record struct Sample(int Civ, SettlementTier Tier, bool Live, bool Gov11, int Population);

    /// <summary>Read-only full-year endpoints and eligibility stability; never included in model state/checksums.</summary>
    public sealed class State
    {
        internal readonly Dictionary<int, Sample> YearStart = new();
        internal readonly Dictionary<int, string> Transition = new();
        internal int YearStartSeason;

        internal State(LivingEconomyModel economy, ResearchSystem research) => Reset(economy, research, 0);

        internal void Reset(LivingEconomyModel economy, ResearchSystem research, int nextSeason)
        {
            YearStart.Clear();
            Transition.Clear();
            YearStartSeason = nextSeason;
            for (int index = 0; index < economy.SettlementCount; index++)
            {
                (int id, int civ, _, _, _, _) = economy.SettlementLite(index);
                YearStart[id] = Read(economy, research, index, id, civ);
            }
        }
    }

    private sealed class PairedCity
    {
        public required int Id;
        public required Sample Start;
        public required Sample ControlStart;
        public Sample TreatmentEnd;
        public Sample ControlEnd;
        public bool TreatmentStable = true;
        public bool ControlStable = true;
        public string TreatmentChange = "none";
        public string ControlChange = "none";
    }

    /// <summary>A true same-snapshot treatment/control branch over one complete calendar year.</summary>
    public sealed class PairedWindow
    {
        private readonly Dictionary<int, PairedCity> _cities;
        public int StartSeason { get; }
        public int ObservedSeasons { get; private set; }
        public ulong StartingChecksum { get; }

        internal PairedWindow(MatchModel treatment, MatchModel control, int startSeason)
        {
            ulong treatmentChecksum = treatment.ComputeChecksum();
            ulong controlChecksum = control.ComputeChecksum();
            if (treatmentChecksum != controlChecksum)
                throw new InvalidDataException($"WP7 paired branch start checksum mismatch: treatment {treatmentChecksum:x16}, control {controlChecksum:x16}.");
            StartingChecksum = treatmentChecksum;
            StartSeason = startSeason;
            _cities = new Dictionary<int, PairedCity>();
            Dictionary<int, Sample> treatmentStart = Capture(treatment.Economy, treatment.Research);
            Dictionary<int, Sample> controlStart = Capture(control.Economy, control.Research);
            foreach ((int id, Sample sample) in treatmentStart)
            {
                if (!Eligible(sample) || !controlStart.TryGetValue(id, out Sample same) || same != sample)
                    continue;
                _cities[id] = new PairedCity { Id = id, Start = sample, ControlStart = same,
                    TreatmentEnd = sample, ControlEnd = same };
            }
            if (_cities.Count == 0) throw new InvalidOperationException("WP7 paired window requires an eligible live City with GOV-11 at its start.");
        }

        public void ObserveSeason(MatchModel treatment, MatchModel control)
        {
            Dictionary<int, Sample> treatmentNow = Capture(treatment.Economy, treatment.Research);
            Dictionary<int, Sample> controlNow = Capture(control.Economy, control.Research);
            foreach (PairedCity city in _cities.Values)
            {
                city.TreatmentEnd = treatmentNow.GetValueOrDefault(city.Id);
                city.ControlEnd = controlNow.GetValueOrDefault(city.Id);
                if (!EligibleFor(city.Start, city.TreatmentEnd))
                {
                    city.TreatmentStable = false;
                    if (city.TreatmentChange == "none") city.TreatmentChange = PairChangeReason(city.Start, city.TreatmentEnd);
                }
                if (!EligibleFor(city.ControlStart, city.ControlEnd))
                {
                    city.ControlStable = false;
                    if (city.ControlChange == "none") city.ControlChange = PairChangeReason(city.ControlStart, city.ControlEnd);
                }
            }
            ObservedSeasons++;
        }

        public void Append(StringBuilder output, ulong seed, MatchModel treatment, MatchModel control,
            int endSeason, bool fullYear, string pairEndReason)
        {
            Dictionary<int, Sample> treatmentNow = Capture(treatment.Economy, treatment.Research);
            Dictionary<int, Sample> controlNow = Capture(control.Economy, control.Research);
            foreach (PairedCity city in _cities.Values.OrderBy(item => item.Id))
            {
                city.TreatmentEnd = treatmentNow.GetValueOrDefault(city.Id);
                city.ControlEnd = controlNow.GetValueOrDefault(city.Id);
                bool stable = fullYear && city.TreatmentStable && city.ControlStable;
                string? exclusion = stable ? null : !fullYear ? "partial_year_or_match_end" :
                    city.TreatmentChange != "none" ? "treatment_" + city.TreatmentChange : "control_" + city.ControlChange;
                string rowName = !fullYear ? "wp7-gov11-city-year-pair-censored" :
                    exclusion is null ? "wp7-gov11-city-year-pair" : "wp7-gov11-city-year-pair-excluded";
                output.Append(rowName)
                    .Append(" seed=").Append(seed).Append(" year=").Append(StartSeason / 4)
                    .Append(" startSeason=").Append(StartSeason).Append(" endSeason=").Append(endSeason)
                    .Append(" observedSeasons=").Append(ObservedSeasons)
                    .Append(" civStart=").Append(city.Start.Civ).Append(" settlement=").Append(city.Id)
                    .Append(" treatmentStartChecksum=").Append(StartingChecksum.ToString("x16"))
                    .Append(" controlStartChecksum=").Append(StartingChecksum.ToString("x16"))
                    .Append(" sameStartingState=true")
                    .Append(" treatmentTierStart=").Append(city.Start.Tier)
                    .Append(" controlTierStart=").Append(city.ControlStart.Tier)
                    .Append(" treatmentGov11Start=").Append(city.Start.Gov11 ? "true" : "false")
                    .Append(" controlGov11Start=").Append(city.ControlStart.Gov11 ? "true" : "false")
                    .Append(" treatmentLiveStart=").Append(city.Start.Live ? "true" : "false")
                    .Append(" controlLiveStart=").Append(city.ControlStart.Live ? "true" : "false")
                    .Append(" treatmentEligibleAtStart=true controlEligibleAtStart=true")
                    .Append(" treatmentPopulationStart=").Append(city.Start.Population)
                    .Append(" controlPopulationStart=").Append(city.ControlStart.Population)
                    .Append(" treatmentPopulationEnd=").Append(city.TreatmentEnd.Population)
                    .Append(" controlPopulationEnd=").Append(city.ControlEnd.Population)
                    .Append(" treatmentNetPopulation=").Append(city.TreatmentEnd.Population - city.Start.Population)
                    .Append(" controlNetPopulation=").Append(city.ControlEnd.Population - city.ControlStart.Population)
                    .Append(" treatmentTierEnd=").Append(city.TreatmentEnd.Tier)
                    .Append(" controlTierEnd=").Append(city.ControlEnd.Tier)
                    .Append(" treatmentGov11End=").Append(city.TreatmentEnd.Gov11 ? "true" : "false")
                    .Append(" controlGov11End=").Append(city.ControlEnd.Gov11 ? "true" : "false")
                    .Append(" treatmentLiveEnd=").Append(city.TreatmentEnd.Live ? "true" : "false")
                    .Append(" controlLiveEnd=").Append(city.ControlEnd.Live ? "true" : "false")
                    .Append(" treatmentEligibleAllYear=").Append(city.TreatmentStable ? "true" : "false")
                    .Append(" controlEligibleAllYear=").Append(city.ControlStable ? "true" : "false")
                    .Append(" controlSuppression=gov11_city_growth_multiplier_only")
                    .Append(" pairEndReason=").Append(pairEndReason);
                if (exclusion is null)
                {
                    output.Append(" treatmentFactorPerMille=").Append((long)city.TreatmentEnd.Population * 1000 / city.Start.Population)
                        .Append(" controlFactorPerMille=").Append((long)city.ControlEnd.Population * 1000 / city.ControlStart.Population)
                        .Append(" pairedBasis=same_snapshot_four_season_branch");
                }
                else
                {
                    output.Append(" exclusionReason=").Append(exclusion)
                        .Append(" treatmentChange=").Append(city.TreatmentChange)
                        .Append(" controlChange=").Append(city.ControlChange);
                }
                output.Append('\n');
            }
        }
    }

    public static bool HasEligibleCity(LivingEconomyModel economy, ResearchSystem research)
        => Capture(economy, research).Values.Any(Eligible);

    public static PairedWindow BeginPairedWindow(MatchModel treatment, MatchModel control, int startSeason)
        => new(treatment, control, startSeason);

    public static State CaptureStart(LivingEconomyModel economy, ResearchSystem research) => new(economy, research);

    public static void AppendSeason(StringBuilder output, LivingEconomyModel economy, ResearchSystem research,
        State state, ulong seed, int season, string gov11Mode)
    {
        int gov11 = research.Catalog.IndexOf("GOV-11");
        Dictionary<int, Sample> current = Capture(economy, research);
        foreach ((int id, Sample start) in state.YearStart)
        {
            if (!current.TryGetValue(id, out Sample end))
                state.Transition.TryAdd(id, "settlement_missing");
            else if (!SameEligibility(start, end))
                state.Transition.TryAdd(id, TransitionReason(start, end));
        }
        foreach (int id in current.Keys)
            if (!state.YearStart.ContainsKey(id)) state.Transition.TryAdd(id, "not_present_at_year_start");

        if (season % 4 == 3)
        {
            var ids = new SortedSet<int>(state.YearStart.Keys);
            ids.UnionWith(current.Keys);
            foreach (int id in ids)
            {
                state.YearStart.TryGetValue(id, out Sample start);
                current.TryGetValue(id, out Sample end);
                if (start.Tier != SettlementTier.City && end.Tier != SettlementTier.City) continue;
                string? exclusion = Exclusion(state, id, start, end, current.ContainsKey(id));
                output.Append(exclusion is null ? "wp7-gov11-city-year" : "wp7-gov11-city-year-excluded")
                    .Append(" seed=").Append(seed).Append(" year=").Append(season / 4)
                    .Append(" startSeason=").Append(state.YearStartSeason).Append(" endSeason=").Append(season)
                    .Append(" civStart=").Append(start.Civ).Append(" civEnd=").Append(end.Civ)
                    .Append(" settlement=").Append(id)
                    .Append(" tierStart=").Append(start.Tier).Append(" tierEnd=").Append(end.Tier)
                    .Append(" gov11Start=").Append(start.Gov11 ? "true" : "false")
                    .Append(" gov11End=").Append(end.Gov11 ? "true" : "false")
                    .Append(" populationStart=").Append(start.Population)
                    .Append(" populationEnd=").Append(end.Population)
                    .Append(" netPopulation=").Append(end.Population - start.Population)
                    .Append(" liveStart=").Append(start.Live ? "true" : "false")
                    .Append(" liveEnd=").Append(end.Live ? "true" : "false")
                    .Append(" gov11Mode=").Append(gov11Mode);
                if (exclusion is null)
                {
                    long factorPerMille = (long)end.Population * 1000 / start.Population;
                    long growthPerMille = (long)(end.Population - start.Population) * 1000 / start.Population;
                    output.Append(" populationFactorPerMille=").Append(factorPerMille)
                        .Append(" growthPerMille=").Append(growthPerMille)
                        .Append(" pairedBasis=observed_full_city_year");
                }
                else
                {
                    output.Append(" exclusionReason=").Append(exclusion);
                }
                output.Append(" terminal=").Append(end.Live ? "live_at_boundary" : "settlement_lost").Append('\n');
            }
            state.Reset(economy, research, season + 1);
        }

        for (int index = 0; index < economy.SettlementCount; index++)
        {
            (int id, int civ, _, _, _, _) = economy.SettlementLite(index);
            if (civ < 0 || economy.Population(id) <= 0) continue;
            CivicReadout civic = economy.Civic(index);
            bool complete = gov11 >= 0 && research.IsDone(civ, gov11);
            bool city = civic.Tier == SettlementTier.City;
            bool charter = city && complete;
            long multiplier = charter ? 4_724_464_025L : 1L << 32;
            output.Append("wp7-gov11")
                .Append(" seed=").Append(seed).Append(" season=").Append(season)
                .Append(" civ=").Append(civ).Append(" settlement=").Append(id)
                .Append(" live=true tier=").Append(civic.Tier)
                .Append(" population=").Append(economy.Population(id))
                .Append(" gov11=").Append(complete ? "true" : "false")
                .Append(" charterEligible=").Append(city ? "true" : "false")
                .Append(" charter=").Append(charter ? "true" : "false")
                .Append(" growthMultiplierRaw=").Append(multiplier)
                .Append(" pairedControlMultiplierRaw=").Append(1L << 32)
                .Append(" pairedTreatmentRatioRaw=").Append(charter ? 4_724_464_025L : 1L << 32)
                .Append(" gov11Mode=").Append(gov11Mode)
                .Append(" pairedBasis=derived_same_state")
                .Append(" growthInput=unavailable growthOutcome=unavailable terminal=live_at_boundary\n");
        }
    }

    private static Dictionary<int, Sample> Capture(LivingEconomyModel economy, ResearchSystem research)
    {
        var rows = new Dictionary<int, Sample>(economy.SettlementCount);
        for (int index = 0; index < economy.SettlementCount; index++)
        {
            (int id, int civ, _, _, _, _) = economy.SettlementLite(index);
            rows[id] = Read(economy, research, index, id, civ);
        }
        return rows;
    }

    private static Sample Read(LivingEconomyModel economy, ResearchSystem research, int index, int id, int civ)
    {
        int gov11 = research.Catalog.IndexOf("GOV-11");
        return new Sample(civ, economy.Civic(index).Tier, civ >= 0 && economy.Population(id) > 0,
            civ >= 0 && gov11 >= 0 && research.IsDone(civ, gov11), economy.Population(id));
    }

    private static bool SameEligibility(Sample left, Sample right)
        => left.Civ == right.Civ && left.Tier == right.Tier && left.Live == right.Live && left.Gov11 == right.Gov11;

    private static bool Eligible(Sample sample)
        => sample.Civ >= 0 && sample.Live && sample.Tier == SettlementTier.City && sample.Gov11;

    private static bool EligibleFor(Sample start, Sample current)
        => Eligible(current) && current.Civ == start.Civ;

    private static string PairChangeReason(Sample start, Sample current)
    {
        if (!current.Live) return "settlement_lost";
        if (current.Civ != start.Civ) return "civilization_changed";
        if (current.Tier != SettlementTier.City) return "city_tier_changed";
        if (!current.Gov11) return "gov11_charter_lost";
        return "eligibility_changed";
    }

    private static string TransitionReason(Sample start, Sample end)
    {
        if (start.Civ != end.Civ) return "civilization_changed";
        if (start.Live != end.Live) return "live_status_changed";
        if (start.Tier != end.Tier) return "city_tier_changed";
        if (start.Gov11 != end.Gov11) return "gov11_status_changed";
        return "eligibility_changed";
    }

    private static string? Exclusion(State state, int id, Sample start, Sample end, bool hasEnd)
    {
        if (!state.YearStart.ContainsKey(id)) return "not_present_at_year_start";
        if (!hasEnd) return "settlement_missing";
        if (!start.Live || !end.Live) return "settlement_not_live_for_full_year";
        if (start.Civ != end.Civ) return "civilization_changed";
        if (start.Tier != SettlementTier.City || end.Tier != SettlementTier.City) return "not_city_for_full_year";
        if (start.Gov11 != end.Gov11) return "gov11_status_changed";
        return state.Transition.TryGetValue(id, out string? reason) ? reason : null;
    }

    public static void AppendTerminal(StringBuilder output, LivingEconomyModel economy, ulong seed, int season,
        bool chapterEnded, string endReason, bool horizonReached, State? state = null,
        ResearchSystem? research = null, string gov11Mode = "treatment")
    {
        string matchStatus = chapterEnded ? "chapter_end" : horizonReached ? "horizon_censored" : "run_stopped";
        output.Append("wp7-match-terminal")
            .Append(" seed=").Append(seed).Append(" season=").Append(season)
            .Append(" matchStatus=").Append(matchStatus)
            .Append(" endReason=").Append(endReason)
            .Append(" chapterEnded=").Append(chapterEnded ? "true" : "false")
            .Append(" horizonReached=").Append(horizonReached ? "true" : "false").Append('\n');

        if (state is not null && research is not null && season > state.YearStartSeason && season % 4 != 0)
        {
            Dictionary<int, Sample> current = Capture(economy, research);
            foreach ((int id, Sample start) in state.YearStart)
            {
                if (start.Tier != SettlementTier.City && (!current.TryGetValue(id, out Sample endSample) || endSample.Tier != SettlementTier.City)) continue;
                current.TryGetValue(id, out Sample end);
                output.Append("wp7-gov11-city-year-censored seed=").Append(seed)
                    .Append(" year=").Append(state.YearStartSeason / 4)
                    .Append(" startSeason=").Append(state.YearStartSeason)
                    .Append(" endSeason=").Append(season - 1)
                    .Append(" observedSeasons=").Append(season - state.YearStartSeason)
                    .Append(" civStart=").Append(start.Civ).Append(" civEnd=").Append(end.Civ)
                    .Append(" settlement=").Append(id)
                    .Append(" gov11Start=").Append(start.Gov11 ? "true" : "false")
                    .Append(" gov11End=").Append(end.Gov11 ? "true" : "false")
                    .Append(" populationStart=").Append(start.Population)
                    .Append(" populationEnd=").Append(end.Population)
                    .Append(" liveEnd=").Append(end.Live ? "true" : "false")
                    .Append(" gov11Mode=").Append(gov11Mode)
                    .Append(" censorReason=match_ended_before_four_seasons matchStatus=").Append(matchStatus).Append('\n');
            }
        }

        for (int index = 0; index < economy.SettlementCount; index++)
        {
            (int id, int civ, _, _, _, _) = economy.SettlementLite(index);
            int population = economy.Population(id);
            bool live = civ >= 0 && population > 0;
            output.Append("wp7-settlement-terminal")
                .Append(" seed=").Append(seed).Append(" season=").Append(season)
                .Append(" civ=").Append(civ).Append(" settlement=").Append(id)
                .Append(" population=").Append(population)
                .Append(" live=").Append(live ? "true" : "false")
                .Append(" settlementStatus=").Append(live ? "right_censored" : "settlement_lost")
                .Append(" matchStatus=").Append(matchStatus)
                .Append(" endReason=").Append(endReason).Append('\n');
        }
    }
}
