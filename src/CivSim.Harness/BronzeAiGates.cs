using System;
using System.Collections.Generic;
using System.Globalization;
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
        bool traceB8 = b8 && Environment.GetEnvironmentVariable("CIVSIM_TRACE_B8") == "1";
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
            LegacyMatchOutcome outcome = Tournament.PlayForLegacy(pack, seed, seats, archetypes, minutes,
                b8 ? match =>
                {
                    int[] offSeats = offTeamA ? Tournament.TeamASeats : Tournament.TeamBSeats;
                    foreach (int c in offSeats) match.Politics.SetDecreePolicy(c, null);
                } : null,
                traceB8 ? match => traces[j] = CaptureB8Trace(j, seed, swap, offTeamA, match) : null);
            bool teamAWon = outcome.TeamALegacy > outcome.TeamBLegacy;
            bool teamBWon = outcome.TeamBLegacy > outcome.TeamALegacy;
            bool tie = outcome.TeamALegacy == outcome.TeamBLegacy;
            scores[j] = (ScoresTeamA(b7, swap, offTeamA) ? teamAWon : teamBWon, tie);
        });

        // Fold in stable job order so completion timing cannot affect the reported rate.
        double wins = 0;
        foreach ((bool won, bool tie) in scores) wins += won ? 1 : tie ? .5 : 0;
        int matches = scores.Length;
        double rate = wins / matches;
        double threshold = b7 ? .65 : .60;
        Result result = Classify(seeds, minutes, rate, threshold);
        Console.WriteLine($"B{(b7 ? 7 : 8)}: {matches} mirrored matches ({seeds} seeds × 2, {minutes} minutes); " +
            $"{(b7 ? "Council vs Settler" : "Grower Decrees-on vs identical Grower Decrees-off")}; engaged/enabled win rate {rate.ToString("P1", CultureInfo.InvariantCulture)} (target ≥ {threshold:P0}).");
        Console.WriteLine(result switch
        {
            Result.Pass => "PASS",
            Result.Fail => "FAIL",
            _ => $"DIAGNOSTIC ONLY: acceptance requires at least {AcceptanceSeeds} seeds and {AcceptanceMinutes} minutes.",
        });
        if (traceB8)
            foreach (string trace in traces) Console.WriteLine(trace);
        return result switch { Result.Pass => 0, Result.Fail => 1, _ => 2 };
    }

    private static string CaptureB8Trace(int index, ulong seed, int swap, bool offTeamA, MatchModel match)
    {
        var parts = new List<string>();
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
                double legacy = standing.Dominion.ToDoubleForDisplay() + standing.Mastery.ToDoubleForDisplay() +
                    standing.Firsts.ToDoubleForDisplay();
                parts.Add($"{label}:civ{civ} legacy={legacy:F1} done(F/E/M/L/C/Cm)={counts[1]}/{counts[2]}/{counts[3]}/{counts[4]}/{counts[5]}/{counts[6]} refused={refused}");
            }
            foreach (PoliticsEvent e in match.Politics.Events)
                if (Array.IndexOf(civs, e.Civ) >= 0 && e.Kind is PoliticsEventKind.DecreeResolved or PoliticsEventKind.DecreeRefused)
                    parts.Add($"{label}:civ{e.Civ}@{e.Tick / 600.0:F1}:{e.Kind} kind={e.A} data={e.B}");
        }
        return $"B8 trace match {index} seed {seed} swap {swap} offTeamA {offTeamA}: " +
            (parts.Count == 0 ? "no decree events" : string.Join("; ", parts));
    }

    internal static bool ScoresTeamA(bool b7, int swap, bool offTeamA)
    {
        // In B7, Settler is the engaged team: Team B before the mirror, Team A after it.
        // In B8, the enabled policy is the team opposite the one whose policy was disabled.
        return b7 ? swap != 0 : !offTeamA;
    }
}
