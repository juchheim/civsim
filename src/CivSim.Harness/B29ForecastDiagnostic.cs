using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Ai.Forecast;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>KEY-4 forecast fidelity measurement for docs/MVP/22 §13.1.</summary>
internal static class B29ForecastDiagnostic
{
    internal enum Disposition
    {
        Pass,
        Fail,
        Incomplete,
    }

    internal readonly record struct Observation(ulong Seed, int Civ, double PredictedMinute, double ActualMinute)
    {
        public double RelativeError => Math.Abs(PredictedMinute - ActualMinute) / ActualMinute;
    }

    internal readonly record struct Summary(int Attempts, int Forecasts, int Within15Percent, double MedianError,
        int MissingForecasts, int CensoredForecasts)
    {
        public Disposition Result
        {
            get
            {
                if (Attempts <= 0) return Disposition.Incomplete;
                if (MissingForecasts > 0 || Within15Percent < Forecasts) return Disposition.Fail;
                if (Forecasts != Attempts || CensoredForecasts > 0) return Disposition.Incomplete;
                return MedianError <= 0.06 ? Disposition.Pass : Disposition.Fail;
            }
        }

        public bool Pass => Result == Disposition.Pass;
    }

    /// <summary>Pairs the first post-KEY-3 forecast with the later realized completion, if it occurs.</summary>
    internal sealed class SeatAttempt(ulong seed, int civ)
    {
        private double? _predictedMinute;
        private bool _completionRecorded;
        private bool _horizonReached;
        public bool Attempted { get; private set; }
        public bool MissingForecastAtCapture => Attempted && !_predictedMinute.HasValue;
        public bool CensoredAtHorizon => _horizonReached && Attempted && _predictedMinute.HasValue && !_completionRecorded;

        public void Capture(double? predictedMinute)
        {
            if (Attempted) throw new InvalidOperationException("KEY-3 forecast attempt was already captured.");
            Attempted = true;
            _predictedMinute = predictedMinute;
        }

        public Observation? Complete(double actualMinute)
        {
            if (!Attempted || !_predictedMinute.HasValue || _completionRecorded) return null;
            _completionRecorded = true;
            return new Observation(seed, civ, _predictedMinute.Value, actualMinute);
        }

        public void MarkHorizonReached() => _horizonReached = true;
    }

    public static (List<Observation> Observations, int Attempts, int MissingForecasts, int CensoredForecasts) Run(ContentPack pack, int seeds, int captureMinutes,
        EconomyArchetype archetype, ulong firstSeed, ICollection<string>? missingForecastDetails = null, int? observationMinutes = null)
    {
        int totalMinutes = observationMinutes ?? captureMinutes;
        if (captureMinutes < 0 || totalMinutes < captureMinutes)
            throw new ArgumentOutOfRangeException(nameof(observationMinutes), "Observation horizon must be at least the capture horizon.");
        var observations = new List<Observation>();
        int attempts = 0, missingForecasts = 0, censoredForecasts = 0;
        int key3, key4;
        // Every seed is mirrored to sample both point-symmetric AI seat pairs, as in the Harness duel.
        for (int i = 0; i < seeds; i++)
            foreach (bool swapped in new[] { false, true })
            {
                ulong seed = firstSeed + (ulong)i;
                GeneratedWorld world = WorldGenerator.Generate(seed);
                var controls = new CivControl[4];
                for (int c = 0; c < 4; c++)
                {
                    bool ai = swapped ? c == 1 || c == 2 : c == 0 || c == 3;
                    controls[c] = ai
                        ? CivControl.Ai(Difficulty.Warlord, archetype)
                        : CivControl.Council;
                }
                MatchModel match = MatchModel.Create(seed, pack, world,
                    new[] { archetype, archetype, archetype, archetype }, controls);
                var runner = new SimRunner(match);
                key3 = match.Research.Catalog.IndexOf("KEY-3");
                key4 = match.Research.Catalog.IndexOf("KEY-4");
                var attemptsBySeat = new SeatAttempt[4];
                for (int c = 0; c < 4; c++) attemptsBySeat[c] = new SeatAttempt(seed, c);
                for (long tick = 0; tick < totalMinutes * 600L; tick += ResearchSystem.SeasonTicks)
                {
                    runner.Advance(Math.Min(ResearchSystem.SeasonTicks, totalMinutes * 600L - tick));
                    for (int c = 0; c < 4; c++)
                    {
                        AiSeat? seat = match.Ai?.SeatOf(c);
                        SeatAttempt attempt = attemptsBySeat[c];
                        if (tick + ResearchSystem.SeasonTicks <= captureMinutes * 600L && !attempt.Attempted && seat is not null && seat.Control.Kind == ControlKind.Ai &&
                            seat.Context.Research.IsDone(key3))
                        {
                            attempts++;
                            double? predicted = null;
                            if (seat.Spark is ResearchSpark spark && TryGetKey4Forecast(
                                    spark.LastForecast, spark.LastB29ReportForecastForDiagnostics, out ForecastResult forecast))
                            {
                                // Save the absolute prediction at the first post-KEY-3 forecast. The only oracle
                                // read is the realized KEY-4 completion, paired when it occurs in a later season.
                                predicted = (seat.Context.Tick + 1L +
                                    forecast.Key4 * (ResearchSystem.SeasonTicks / (double)ForecastTuning.PlannerStepsPerSeason)) / 600.0;
                            }
                            else if (missingForecastDetails is not null && seat.Spark is ResearchSpark missingSpark)
                            {
                                int forecastHorizonSeasons = seat.Profile.HorizonEras * ForecastTuning.SeasonsPerEra;
                                missingForecastDetails.Add($"seed={seed} civ={c} KEY-3 season={seat.Context.Season} " +
                                    $"Spark decision season={missingSpark.LastDecisionSeasonForDiagnostics}; " +
                                    $"forecast horizon={forecastHorizonSeasons} seasons, rollout={missingSpark.LastForecast.Steps} steps, " +
                                    $"forecast era={missingSpark.LastForecast.Era}, KEY-4 steps={missingSpark.LastForecast.Key4}, " +
                                    $"report horizon={ResearchSpark.B29ReportHorizonSeasons} seasons, " +
                                    $"report KEY-4 steps={missingSpark.LastB29ReportForecastForDiagnostics?.Key4 ?? -1}; " +
                                    missingSpark.LastForecastKey4BlockersForDiagnostics());
                            }
                            attempt.Capture(predicted);
                        }
                        if (attempt.Attempted && match.Research.IsDone(c, key4))
                        {
                            double actual = match.Research.CompletedTick(c, key4) / 600.0;
                            Observation? observation = attempt.Complete(actual);
                            if (observation.HasValue) observations.Add(observation.Value);
                        }
                    }
                }
                foreach (SeatAttempt attempt in attemptsBySeat)
                {
                    attempt.MarkHorizonReached();
                    if (attempt.MissingForecastAtCapture) missingForecasts++;
                    else if (attempt.CensoredAtHorizon) censoredForecasts++;
                }
            }
        return (observations, attempts, missingForecasts, censoredForecasts);
    }

    internal static bool TryGetKey4Forecast(ForecastResult primary, ForecastResult? reportOnly, out ForecastResult selected)
    {
        if (primary.Key4 > 0)
        {
            selected = primary;
            return true;
        }
        if (reportOnly is { Key4: > 0 } extended)
        {
            selected = extended;
            return true;
        }
        selected = default;
        return false;
    }

    public static Summary Summarize(IReadOnlyCollection<Observation> observations, int attempts,
        int missingForecasts = 0, int censoredForecasts = 0)
    {
        double[] errors = observations.Select(o => o.RelativeError).OrderBy(e => e).ToArray();
        double median = errors.Length == 0 ? double.NaN : errors.Length % 2 == 1
            ? errors[errors.Length / 2]
            : (errors[errors.Length / 2 - 1] + errors[errors.Length / 2]) / 2.0;
        int within = errors.Count(e => e <= 0.15);
        return new Summary(attempts, errors.Length, within, median, missingForecasts, censoredForecasts);
    }
}
