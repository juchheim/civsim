using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CivSim.Sim.Ai;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;

namespace CivSim.Harness;

/// <summary>Automated Bronze engagement and Decree value gates (docs/MVP/22 §16 B7–B8).</summary>
internal static class BronzeAiGates
{
    public const int AcceptanceSeeds = 32;
    public const int AcceptanceMinutes = 64;

    internal readonly record struct B8FirstClaim(string ProjectId, int Era, bool IsKeystone, int Points,
        int ClaimantCiv, string ClaimantTeam, int CompletionSeason, int OpposingEarliestSeason);

    internal sealed record B8SampleDelta(ulong Seed, int Swap, double Dominion, double Mastery, double Firsts, double Wonders,
        double Legacy, double Settlements, double Villages, double Towns, double Cities, double OwnedWeight,
        double NaturalFoundings, double DecreeFoundings, double[] DecreesDone, double[] DecreesRefused,
        double[] FirstPointsByProject, string[] ProjectIds, double OrientationLegacyMargin);

    internal sealed record B8SeedDelta(ulong Seed, double Dominion, double Mastery, double Firsts, double Wonders,
        double Legacy, double Settlements, double Villages, double Towns, double Cities, double OwnedWeight,
        double NaturalFoundings, double DecreeFoundings, double[] DecreesDone, double[] DecreesRefused,
        double[] FirstPointsByProject);

    private sealed record B8FoodSeason(int Season, bool Enabled, int Settlements, int CurrentLow, int WinterLow,
        double MinCurrent, double MinWinter);

    internal static string FormatB8FirstLedger(IReadOnlyList<B8FirstClaim> claims)
    {
        var projects = new List<string>(claims.Count);
        var totals = new int[2, ResearchCatalog.EraCount, 2]; // team × era × ordinary/keystone
        foreach (B8FirstClaim claim in claims)
        {
            projects.Add($"{claim.ProjectId}:pts={claim.Points}:claim=civ{claim.ClaimantCiv}/{claim.ClaimantTeam}:s{claim.CompletionSeason}:opp=" +
                (claim.OpposingEarliestSeason < 0 ? "never" : $"s{claim.OpposingEarliestSeason}"));
            int team = claim.ClaimantTeam == "enabled" ? 0 : 1;
            int era = Math.Clamp(claim.Era, 0, ResearchCatalog.EraCount - 1);
            totals[team, era, claim.IsKeystone ? 1 : 0] += claim.Points;
        }

        var aggregates = new List<string>(2);
        for (int team = 0; team < 2; team++)
        {
            string label = team == 0 ? "enabled" : "disabled";
            var eras = new List<string>(ResearchCatalog.EraCount);
            for (int era = 0; era < ResearchCatalog.EraCount; era++)
                eras.Add($"E{era}(ordinary={totals[team, era, 0]},keystone={totals[team, era, 1]})");
            aggregates.Add($"{label}[{string.Join(',', eras)}]");
        }
        return $"projects[{string.Join(';', projects)}] aggregates[{string.Join(';', aggregates)}]";
    }

    internal static string FormatB8CohortSummary(IReadOnlyList<B8SeedDelta> seeds, IReadOnlyList<string> projectIds,
        IReadOnlyList<B8SampleDelta>? orientations = null)
    {
        static double[] MeanMedian(IReadOnlyList<B8SeedDelta> rows, Func<B8SeedDelta, double> value)
        {
            if (rows.Count == 0) return new[] { double.NaN, double.NaN };
            var values = new double[rows.Count];
            double sum = 0;
            for (int i = 0; i < rows.Count; i++) { values[i] = value(rows[i]); sum += values[i]; }
            Array.Sort(values);
            double median = values.Length % 2 == 1 ? values[values.Length / 2] : (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2;
            return new[] { sum / rows.Count, median };
        }

        static string Pair(double[] stats) => $"{stats[0]:+0.0;-0.0;0.0}/{stats[1]:+0.0;-0.0;0.0}";
        string FormatVector(Func<B8SeedDelta, double> value) => Pair(MeanMedian(seeds, value));
        string[] decreeNames = { "Found", "Expedition", "Mine", "Levy", "Corvee", "Compact" };
        string DecreeStats(Func<B8SeedDelta, double[]> values)
        {
            var items = new string[decreeNames.Length];
            for (int i = 0; i < items.Length; i++)
            {
                int index = i + 1; // DecreeKind.None occupies slot zero.
                double[] stats = MeanMedian(seeds, row => index < values(row).Length ? values(row)[index] : 0);
                items[i] = $"{decreeNames[i]}={Pair(stats)}";
            }
            return string.Join(',', items);
        }

        var lines = new List<string>(seeds.Count + 4);
        lines.Add($"B8 cohort summary seeds={seeds.Count} paired mean/median deltas (D/M/Firsts/W/Legacy)=" +
            $"{FormatVector(x => x.Dominion)}/{FormatVector(x => x.Mastery)}/{FormatVector(x => x.Firsts)}/" +
            $"{FormatVector(x => x.Wonders)}/{FormatVector(x => x.Legacy)}; " +
            $"settlements={FormatVector(x => x.Settlements)} tiers(V/T/C)={FormatVector(x => x.Villages)}/" +
            $"{FormatVector(x => x.Towns)}/{FormatVector(x => x.Cities)} ownedWeight={FormatVector(x => x.OwnedWeight)}; " +
            $"foundings(natural/decree)={FormatVector(x => x.NaturalFoundings)}/{FormatVector(x => x.DecreeFoundings)}; " +
            $"decreeDone(mean/median)=[{DecreeStats(x => x.DecreesDone)}] decreeRefused(mean/median)=[{DecreeStats(x => x.DecreesRefused)}]");

        for (int i = 0; i < seeds.Count; i++)
        {
            B8SeedDelta row = seeds[i];
            var projectParts = new List<string>();
            for (int p = 0; p < row.FirstPointsByProject.Length && p < projectIds.Count; p++)
                if (row.FirstPointsByProject[p] != 0) projectParts.Add($"{projectIds[p]}={row.FirstPointsByProject[p]:+0.0;-0.0;0.0}");
            lines.Add($"B8 paired seed={row.Seed} delta(D/M/Firsts/W/Legacy)=" +
                $"{row.Dominion:F1}/{row.Mastery:F1}/{row.Firsts:F1}/{row.Wonders:F1}/{row.Legacy:F1} " +
                $"settlements={row.Settlements:F1} tiers(V/T/C)={row.Villages:F1}/{row.Towns:F1}/{row.Cities:F1} " +
                $"ownedWeight={row.OwnedWeight:F1} foundings(natural/decree)={row.NaturalFoundings:F1}/{row.DecreeFoundings:F1} " +
                $"done=[{FormatDecreeVector(row.DecreesDone)}] refused=[{FormatDecreeVector(row.DecreesRefused)}] " +
                $"firstPoints=[{string.Join(',', projectParts)}]");
        }

        var projectScores = new List<(string Id, double Mean)>();
        for (int p = 0; p < projectIds.Count; p++)
        {
            double total = 0;
            foreach (B8SeedDelta row in seeds) if (p < row.FirstPointsByProject.Length) total += row.FirstPointsByProject[p];
            projectScores.Add((projectIds[p], seeds.Count == 0 ? 0 : total / seeds.Count));
        }
        lines.Add("B8 top paired Firsts projects by absolute mean delta=" +
            string.Join(',', projectScores.OrderByDescending(x => Math.Abs(x.Mean)).ThenBy(x => x.Id, StringComparer.Ordinal)
                .Take(5).Select(x => $"{x.Id}={x.Mean:+0.0;-0.0;0.0}")));
        lines.Add("B8 worst paired seeds by Legacy delta=" +
            string.Join(',', seeds.OrderBy(x => x.Legacy).ThenBy(x => x.Seed).Take(5)
                .Select(x => $"{x.Seed}={x.Legacy:+0.0;-0.0;0.0}")));
        if (orientations is not null)
        {
            int splitPairs = 0;
            for (int i = 0; i + 1 < orientations.Count; i += 2)
                if (Outcome(orientations[i].OrientationLegacyMargin) != Outcome(orientations[i + 1].OrientationLegacyMargin)) splitPairs++;
            lines.Add($"B8 mirrored orientation split-pairs={splitPairs}/{orientations.Count / 2}");
            foreach (B8SampleDelta row in orientations)
                lines.Add($"B8 orientation seed={row.Seed} swap={row.Swap} enabledLegacyMargin={row.OrientationLegacyMargin:+0.0;-0.0;0.0} result={Outcome(row.OrientationLegacyMargin)}");
            lines.Add("B8 five closest losing orientation margins to zero=" +
                string.Join(',', orientations.Where(x => x.OrientationLegacyMargin < 0)
                    .OrderByDescending(x => x.OrientationLegacyMargin).ThenBy(x => x.Seed).ThenBy(x => x.Swap).Take(5)
                    .Select(x => $"{x.Seed}/swap{x.Swap}={x.OrientationLegacyMargin:+0.0;-0.0;0.0}")));
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string Outcome(double margin) => margin > 0 ? "win" : margin < 0 ? "loss" : "tie";

    private static string FormatDecreeVector(double[] values)
    {
        string[] names = { "Found", "Expedition", "Mine", "Levy", "Corvee", "Compact" };
        var parts = new string[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            int index = i + 1; // DecreeKind.None occupies slot zero.
            parts[i] = $"{names[i]}={(index < values.Length ? values[index] : 0):+0.0;-0.0;0.0}";
        }
        return string.Join(',', parts);
    }

    internal enum Result { Diagnostic, Pass, Fail }

    internal static Result Classify(int seeds, int minutes, double winRate, double threshold)
    {
        if (seeds < AcceptanceSeeds || minutes < AcceptanceMinutes) return Result.Diagnostic;
        return winRate >= threshold ? Result.Pass : Result.Fail;
    }

    public static int Run(ContentPack pack, bool b7, bool b8, int seeds, int minutes, ulong firstSeed)
    {
        if (b7 == b8) throw new ArgumentException("Choose exactly one of --check-b7 or --check-b8.");
        var jobs = new (ulong Seed, int Swap)[seeds * 2];
        for (int i = 0; i < seeds; i++)
        {
            jobs[i * 2] = (firstSeed + (ulong)i, 0);
            jobs[i * 2 + 1] = (firstSeed + (ulong)i, 1);
        }
        var scores = new (bool Won, bool Tie)[jobs.Length];
        var traces = new string[jobs.Length];
        bool summaryB8 = b8 && Environment.GetEnvironmentVariable("CIVSIM_SUMMARY_B8") == "1";
        bool traceB8 = b8 && !summaryB8 && Environment.GetEnvironmentVariable("CIVSIM_TRACE_B8") == "1";
        var b8LegacyMargins = b8 ? new double[jobs.Length] : null;
        var summaries = summaryB8 ? new B8SampleDelta[jobs.Length] : null;
        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
        Parallel.For(0, jobs.Length, options, j =>
        {
            (ulong seed, int swap) = jobs[j];
            var seats = new Player[4];
            var archetypes = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced,
                EconomyArchetype.Balanced, EconomyArchetype.Balanced };
            if (b7)
            {
                Player council = Player.Parse("council", EconomyArchetype.Balanced);
                Player settler = Player.Parse("settler", EconomyArchetype.Balanced);
                foreach (int s in Tournament.TeamASeats) seats[s] = swap == 0 ? council : settler;
                foreach (int s in Tournament.TeamBSeats) seats[s] = swap == 0 ? settler : council;
            }
            else
            {
                // Both sides use the exact same scripted Grower. The only intervention is that one
                // side's Council Decree policy is removed; seat placement is mirrored per seed.
                Player grower = Player.Parse("grower", EconomyArchetype.Balanced);
                foreach (int s in Tournament.TeamASeats) seats[s] = grower;
                foreach (int s in Tournament.TeamBSeats) seats[s] = grower;
            }

            bool offTeamA = b8 && swap == 1;
            var b8FoodSeasons = traceB8 ? new List<B8FoodSeason>() : null;
            LegacyMatchOutcome outcome = Tournament.PlayForLegacy(pack, seed, seats, archetypes, minutes,
                b8 ? match =>
                {
                    match.Politics.B8FoundTraceEnabled = traceB8;
                    int[] offSeats = offTeamA ? Tournament.TeamASeats : Tournament.TeamBSeats;
                    foreach (int c in offSeats) match.Politics.SetDecreePolicy(c, null);
                } : null,
                traceB8 || summaryB8 ? match =>
                {
                    if (traceB8) traces[j] = CaptureB8Trace(j, seed, swap, offTeamA, match) +
                        Environment.NewLine + FormatB8FoodTrace(seed, swap, offTeamA, match, b8FoodSeasons!);
                    if (summaryB8) summaries![j] = CaptureB8SampleDelta(seed, swap, offTeamA, match);
                } : null,
                traceB8 ? (match, season) => CaptureB8FoodSeason(match, season, offTeamA, b8FoodSeasons!) : null);
            bool teamAWon = outcome.TeamALegacy > outcome.TeamBLegacy;
            bool teamBWon = outcome.TeamBLegacy > outcome.TeamALegacy;
            bool tie = outcome.TeamALegacy == outcome.TeamBLegacy;
            scores[j] = (ScoresTeamA(b7, swap, offTeamA) ? teamAWon : teamBWon, tie);
            if (b8)
                b8LegacyMargins![j] = offTeamA
                    ? outcome.TeamBLegacy - outcome.TeamALegacy
                    : outcome.TeamALegacy - outcome.TeamBLegacy;
        });

        // Fold in stable job order so completion timing cannot affect the reported rate.
        double wins = 0;
        foreach ((bool won, bool tie) in scores) wins += won ? 1 : tie ? .5 : 0;
        int matches = scores.Length;
        double rate = wins / matches;
        double threshold = b7 ? .65 : .60;
        B8PairedLegacySummary b8Effect = b8 ? SummarizeB8PairedLegacyMargins(b8LegacyMargins!) : default;
        Result result = b8
            ? ClassifyB8(seeds, minutes, b8Effect)
            : Classify(seeds, minutes, rate, threshold);
        if (b7)
            Console.WriteLine($"B7: {matches} mirrored matches ({seeds} seeds × 2, {minutes} minutes); Council vs Settler; " +
                $"engaged win rate {rate.ToString("P1", CultureInfo.InvariantCulture)} (target ≥ {threshold:P0}).");
        else
            Console.WriteLine($"B8: {matches} mirrored matches ({seeds} seeds × 2, {minutes} minutes); " +
                $"Grower Decrees-on vs identical Grower Decrees-off; paired Legacy advantage " +
                $"{b8Effect.PositiveSeedPairs}/{b8Effect.SeedPairs} seeds " +
                $"({(double)b8Effect.PositiveSeedPairs / b8Effect.SeedPairs:P1}), mean {b8Effect.MeanDelta:+0.0;-0.0;0.0}, " +
                $"median {b8Effect.MedianDelta:+0.0;-0.0;0.0} (target ≥ 60% of pairs and positive mean); " +
                $"raw enabled match win rate {rate.ToString("P1", CultureInfo.InvariantCulture)} (diagnostic).");
        Console.WriteLine(result switch
        {
            Result.Pass => "PASS",
            Result.Fail => "FAIL",
            _ => $"DIAGNOSTIC ONLY: acceptance requires at least {AcceptanceSeeds} seeds and {AcceptanceMinutes} minutes.",
        });
        if (traceB8)
            foreach (string trace in traces) Console.WriteLine(trace);
        if (summaryB8)
            Console.WriteLine(FormatB8CohortSummary(PairB8Samples(summaries!), summaries![0].ProjectIds, summaries));
        return result switch { Result.Pass => 0, Result.Fail => 1, _ => 2 };
    }

    internal readonly record struct B8PairedLegacySummary(int PositiveSeedPairs, int SeedPairs, double MeanDelta, double MedianDelta);

    internal static B8PairedLegacySummary SummarizeB8PairedLegacyMargins(IReadOnlyList<double> orientationMargins)
    {
        if (orientationMargins.Count % 2 != 0)
            throw new ArgumentException("B8 needs two mirrored Legacy margins per seed.", nameof(orientationMargins));
        int seedPairs = orientationMargins.Count / 2;
        var deltas = new double[seedPairs];
        int positive = 0;
        double total = 0;
        for (int i = 0; i < seedPairs; i++)
        {
            double delta = (orientationMargins[i * 2] + orientationMargins[i * 2 + 1]) / 2;
            deltas[i] = delta;
            if (delta > 0) positive++;
            total += delta;
        }
        Array.Sort(deltas);
        double median = seedPairs == 0 ? 0 : seedPairs % 2 == 1
            ? deltas[seedPairs / 2]
            : (deltas[seedPairs / 2 - 1] + deltas[seedPairs / 2]) / 2;
        return new B8PairedLegacySummary(positive, seedPairs, seedPairs == 0 ? 0 : total / seedPairs, median);
    }

    internal static Result ClassifyB8(int seeds, int minutes, B8PairedLegacySummary effect)
    {
        if (seeds < AcceptanceSeeds || minutes < AcceptanceMinutes || effect.SeedPairs != seeds)
            return Result.Diagnostic;
        return effect.PositiveSeedPairs / (double)seeds >= .60 && effect.MeanDelta > 0
            ? Result.Pass : Result.Fail;
    }

    private static B8SampleDelta CaptureB8SampleDelta(ulong seed, int swap, bool offTeamA, MatchModel match)
    {
        int[] enabled = offTeamA ? Tournament.TeamBSeats : Tournament.TeamASeats;
        int[] disabled = offTeamA ? Tournament.TeamASeats : Tournament.TeamBSeats;
        double[] metrics = new double[5]; // D/M/F/W/Legacy
        double settlements = 0, villages = 0, towns = 0, cities = 0, ownedWeight = 0;
        double natural = 0, decreeFoundings = 0;
        var done = new double[7];
        var refused = new double[7];
        var firstPoints = new double[match.Research.Catalog.Projects.Count];
        foreach ((int[] civs, double sign) in new[] { (enabled, 1.0), (disabled, -1.0) })
        foreach (int civ in civs)
        {
            var standing = Scoring.Of(match, civ);
            metrics[0] += sign * standing.Dominion.ToDoubleForDisplay();
            metrics[1] += sign * standing.Mastery.ToDoubleForDisplay();
            metrics[2] += sign * standing.Thriving.ToDoubleForDisplay();
            metrics[3] += sign * standing.Wonders.ToDoubleForDisplay();
            metrics[4] += sign * standing.Legacy.ToDoubleForDisplay();
            ownedWeight += sign * match.Politics.Territory.OwnedWeight(civ);
            for (int s = 0; s < match.Economy.SettlementCount; s++)
            {
                var settlement = match.Economy.Settlement(s);
                if (settlement.Civilization != civ) continue;
                settlements += sign;
                switch (match.Economy.Tier(s))
                {
                    case SettlementTier.Village: villages += sign; break;
                    case SettlementTier.Town: towns += sign; break;
                    case SettlementTier.City: cities += sign; break;
                }
            }
            for (int k = 1; k < 7; k++)
            {
                done[k] += sign * match.Politics.DecreesDone(civ, (DecreeKind)k);
                refused[k] += sign * match.Politics.DecreesRefused(civ, (DecreeKind)k);
            }
            int foundedByDecree = match.Politics.DecreesDone(civ, DecreeKind.Found);
            decreeFoundings += sign * foundedByDecree;
            natural += sign * (match.Politics.E3Foundings(civ) - foundedByDecree);
        }
        for (int p = 0; p < firstPoints.Length; p++)
        {
            int claimant = match.Research.FirstBy(p);
            if (claimant < 0) continue;
            firstPoints[p] = Array.IndexOf(enabled, claimant) >= 0
                ? match.Research.Catalog.FirstUnits(match.Research.Catalog.Projects[p]).ToDoubleForDisplay()
                : -match.Research.Catalog.FirstUnits(match.Research.Catalog.Projects[p]).ToDoubleForDisplay();
        }
        return new B8SampleDelta(seed, swap, metrics[0], metrics[1], metrics[2], metrics[3], metrics[4],
            settlements, villages, towns, cities, ownedWeight, natural, decreeFoundings, done, refused,
            firstPoints, match.Research.Catalog.Projects.Select(x => x.Id).ToArray(), metrics[4]);
    }

    private static void CaptureB8FoodSeason(MatchModel match, int season, bool offTeamA, List<B8FoodSeason> into)
    {
        for (int side = 0; side < 2; side++)
        {
            bool enabled = side == 0;
            int[] civs = enabled
                ? offTeamA ? Tournament.TeamBSeats : Tournament.TeamASeats
                : offTeamA ? Tournament.TeamASeats : Tournament.TeamBSeats;
            int settlements = 0, currentLow = 0, winterLow = 0;
            double minCurrent = double.PositiveInfinity, minWinter = double.PositiveInfinity;
            foreach (int civ in civs)
                for (int s = 0; s < match.Economy.SettlementCount; s++)
                {
                    var lite = match.Economy.SettlementLite(s);
                    if (lite.Civilization != civ || match.Politics.IsFreeFolk(s)) continue;
                    settlements++;
                    double food = lite.LastFoodSecurity.ToDoubleForDisplay();
                    double winter = lite.LateWinterFoodForecast.ToDoubleForDisplay();
                    minCurrent = Math.Min(minCurrent, food);
                    minWinter = Math.Min(minWinter, winter);
                    if (food < 0.8) currentLow++;
                    if (winter < 0.8) winterLow++;
                }
            into.Add(new B8FoodSeason(season, enabled, settlements, currentLow, winterLow,
                double.IsPositiveInfinity(minCurrent) ? 0 : minCurrent,
                double.IsPositiveInfinity(minWinter) ? 0 : minWinter));
        }
    }

    private static string FormatB8FoodTrace(ulong seed, int swap, bool offTeamA, MatchModel match,
        IReadOnlyList<B8FoodSeason> snapshots)
    {
        var expeditions = new List<(int Civ, int Season, int Target)>();
        foreach (PoliticsEvent e in match.Politics.Events)
            if (e.Kind == PoliticsEventKind.DecreeResolved && e.A == (int)DecreeKind.Expedition)
                expeditions.Add((e.Civ, checked((int)(e.Tick / ResearchSystem.SeasonTicks)), e.B));

        var lines = new List<string>();
        foreach (B8FoodSeason row in snapshots)
        {
            var sideCivs = row.Enabled
                ? offTeamA ? Tournament.TeamBSeats : Tournament.TeamASeats
                : offTeamA ? Tournament.TeamASeats : Tournament.TeamBSeats;
            var nearby = new List<string>();
            foreach ((int civ, int expeditionSeason, int target) in expeditions)
                if (Array.IndexOf(sideCivs, civ) >= 0 && Math.Abs(row.Season - expeditionSeason) <= 1)
                    nearby.Add($"c{civ}@s{expeditionSeason}/t{target}");
            bool foodRisk = row.CurrentLow > 0 || row.WinterLow > 0;
            if (!foodRisk && nearby.Count == 0) continue;
            lines.Add($"B8 food seed={seed} swap={swap} side={(row.Enabled ? "enabled" : "disabled")} season={row.Season} " +
                $"settlements={row.Settlements} currentLow(<0.8)={row.CurrentLow} minCurrent={row.MinCurrent:F2} " +
                $"winterLow(<0.8)={row.WinterLow} minWinter={row.MinWinter:F2} " +
                $"expeditionWindow={(nearby.Count == 0 ? "none" : string.Join(',', nearby))}");
        }
        return string.Join(Environment.NewLine, lines);
    }

    internal static IReadOnlyList<B8SeedDelta> PairB8Samples(IReadOnlyList<B8SampleDelta> samples)
    {
        if (samples.Count % 2 != 0) throw new ArgumentException("B8 samples must contain mirrored pairs.", nameof(samples));
        var paired = new List<B8SeedDelta>(samples.Count / 2);
        for (int i = 0; i < samples.Count; i += 2)
        {
            B8SampleDelta a = samples[i], b = samples[i + 1];
            if (a.Seed != b.Seed || a.Swap == b.Swap) throw new ArgumentException("B8 samples are not in mirrored seed order.", nameof(samples));
            static double Avg(double x, double y) => (x + y) / 2;
            static double[] AvgArray(double[] x, double[] y)
            {
                var result = new double[Math.Max(x.Length, y.Length)];
                for (int j = 0; j < result.Length; j++) result[j] = Avg(j < x.Length ? x[j] : 0, j < y.Length ? y[j] : 0);
                return result;
            }
            paired.Add(new B8SeedDelta(a.Seed, Avg(a.Dominion, b.Dominion), Avg(a.Mastery, b.Mastery),
                Avg(a.Firsts, b.Firsts), Avg(a.Wonders, b.Wonders), Avg(a.Legacy, b.Legacy),
                Avg(a.Settlements, b.Settlements), Avg(a.Villages, b.Villages), Avg(a.Towns, b.Towns),
                Avg(a.Cities, b.Cities), Avg(a.OwnedWeight, b.OwnedWeight), Avg(a.NaturalFoundings, b.NaturalFoundings),
                Avg(a.DecreeFoundings, b.DecreeFoundings), AvgArray(a.DecreesDone, b.DecreesDone),
                AvgArray(a.DecreesRefused, b.DecreesRefused), AvgArray(a.FirstPointsByProject, b.FirstPointsByProject)));
        }
        return paired;
    }

    private static string CaptureB8Trace(int index, ulong seed, int swap, bool offTeamA, MatchModel match)
    {
        var parts = new List<string>();
        int[] enabledCivs = offTeamA ? Tournament.TeamBSeats : Tournament.TeamASeats;
        int[] disabledCivs = offTeamA ? Tournament.TeamASeats : Tournament.TeamBSeats;
        for (int side = 0; side < 2; side++)
        {
            int[] civs = side == 0 ? (offTeamA ? Tournament.TeamBSeats : Tournament.TeamASeats)
                                  : (offTeamA ? Tournament.TeamASeats : Tournament.TeamBSeats);
            string label = side == 0 ? "enabled" : "disabled";
            foreach (int civ in civs)
            {
                var counts = new int[7];
                int refused = 0;
                for (int kind = 1; kind < counts.Length; kind++)
                {
                    counts[kind] = match.Politics.DecreesDone(civ, (DecreeKind)kind);
                    refused += match.Politics.DecreesRefused(civ, (DecreeKind)kind);
                }
                var standing = Scoring.Of(match, civ);
                double legacy = standing.Legacy.ToDoubleForDisplay();
                int totalE3Foundings = match.Politics.E3Foundings(civ);
                int decreeFoundings = counts[(int)DecreeKind.Found];
                int naturalFoundings = totalE3Foundings - decreeFoundings;
                int ownedTiles = match.Politics.Territory.OwnedTiles(civ);
                int ownedWeight = match.Politics.Territory.OwnedWeight(civ);
                parts.Add($"{label}:civ{civ} legacy={legacy:F1} parts(D/M/F/W)=" +
                    $"{standing.Dominion.ToDoubleForDisplay():F1}/{standing.Mastery.ToDoubleForDisplay():F1}/" +
                    $"{standing.Thriving.ToDoubleForDisplay():F1}/{standing.Wonders.ToDoubleForDisplay():F1} " +
                    $"E3foundings(total/natural/decree)={totalE3Foundings}/{naturalFoundings}/{decreeFoundings} " +
                    $"owned(tiles/weight)={ownedTiles}/{ownedWeight} " +
                    $"done(F/E/M/L/C/Cm)={counts[1]}/{counts[2]}/{counts[3]}/{counts[4]}/{counts[5]}/{counts[6]} refused={refused}");
                var settlements = new List<string>();
                for (int s = 0; s < match.Economy.SettlementCount; s++)
                {
                    var settlement = match.Economy.Settlement(s);
                    if (settlement.Civilization != civ) continue;
                    byte build = match.Economy.CivicBuildTarget(s);
                    if (build is not (LivingEconomyModel.CorveeTarget.Market or LivingEconomyModel.CorveeTarget.School or
                        LivingEconomyModel.CorveeTarget.Temple or LivingEconomyModel.CorveeTarget.Palace)) continue;
                    settlements.Add($"s{s}:pop={settlement.Population}:build={CorveeTargetName(build)}:corvee={CorveeTargetName(match.Economy.CorveeAt(s))}:cohesion={match.Economy.SettlementCohesion(s).ToDoubleForDisplay():F2}");
                }
                parts.Add($"{label}:civ{civ} civic=" + (settlements.Count == 0 ? "none" : string.Join(',', settlements)));
            }
            foreach (PoliticsEvent e in match.Politics.Events)
                if (Array.IndexOf(civs, e.Civ) >= 0 && e.Kind is PoliticsEventKind.DecreeResolved or PoliticsEventKind.DecreeRefused)
                    parts.Add($"{label}:civ{e.Civ}@{e.Tick / 600.0:F1}:{e.Kind} kind={e.A} data={e.B}");
        }
        foreach (string found in match.Politics.B8FoundTrace)
        {
            // Report whether the donor whose City prospect was at risk eventually became a City.
            int donorStart = found.IndexOf("donor=s", StringComparison.Ordinal);
            int donorEnd = donorStart < 0 ? -1 : found.IndexOf(' ', donorStart);
            int donor = -1;
            if (donorEnd > donorStart) int.TryParse(found.Substring(donorStart + 7, donorEnd - donorStart - 7), out donor);
            string city = donor >= 0 && donor < match.Economy.SettlementCount
                ? match.Economy.Civic(donor).Tier.ToString()
                : "unknown";
            parts.Add($"foundTrace:{found} completedCity={city}");
        }
        var firstClaims = new List<B8FirstClaim>();
        for (int project = 0; project < match.Research.Catalog.Projects.Count; project++)
        {
            int claimant = match.Research.FirstBy(project);
            if (claimant < 0) continue;
            bool enabled = Array.IndexOf(enabledCivs, claimant) >= 0;
            int[] opposingCivs = enabled ? disabledCivs : enabledCivs;
            int opposingSeason = int.MaxValue;
            foreach (int civ in opposingCivs)
            {
                if (!match.Research.IsDone(civ, project)) continue;
                long tick = match.Research.CompletedTick(civ, project);
                if (tick >= 0) opposingSeason = Math.Min(opposingSeason, checked((int)(tick / ResearchSystem.SeasonTicks)));
            }
            var definition = match.Research.Catalog.Projects[project];
            int claimTick = checked((int)(match.Research.CompletedTick(claimant, project) / ResearchSystem.SeasonTicks));
            firstClaims.Add(new B8FirstClaim(definition.Id, definition.Era, definition.IsKeystone,
                match.Research.Catalog.FirstUnits(definition).FloorToInt(), claimant, enabled ? "enabled" : "disabled",
                claimTick, opposingSeason == int.MaxValue ? -1 : opposingSeason));
        }
        parts.Add("firstsLedger=" + FormatB8FirstLedger(firstClaims));
        return $"B8 trace match {index} seed {seed} swap {swap} offTeamA {offTeamA}: " +
            (parts.Count == 0 ? "no decree events" : string.Join("; ", parts));
    }

    private static string CorveeTargetName(int target) => target switch
    {
        LivingEconomyModel.CorveeTarget.TopPublicWork => "public",
        LivingEconomyModel.CorveeTarget.Walls => "walls",
        LivingEconomyModel.CorveeTarget.Temple => "temple",
        LivingEconomyModel.CorveeTarget.Palace => "palace",
        LivingEconomyModel.CorveeTarget.Canal => "canal",
        LivingEconomyModel.CorveeTarget.School => "school",
        LivingEconomyModel.CorveeTarget.Market => "market",
        LivingEconomyModel.CorveeTarget.Wonder => "wonder",
        _ => "none",
    };

    internal static bool ScoresTeamA(bool b7, int swap, bool offTeamA)
    {
        // In B7, Settler is the engaged team: Team B before the mirror, Team A after it.
        // In B8, the enabled policy is the team opposite the one whose policy was disabled.
        return b7 ? swap != 0 : !offTeamA;
    }
}
