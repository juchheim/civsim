using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CivSim.Sim.Ai;
using CivSim.Sim.Content;
using CivSim.Sim.Economy;

namespace CivSim.Harness;

/// <summary>
/// The AI harness (docs/MVP/14 §10): mirrored duels, free-for-alls, an Elo ladder and the M6 acceptance gate.
/// <code>
/// civsim-harness ai --pack FILE --duel warlord council [--seeds N] [--minutes N] [--archetype balanced]
/// civsim-harness ai --pack FILE --ladder council,rusher,chieftain,warlord [--seeds N]
/// civsim-harness ai --pack FILE --ffa warlord [--seeds N] [--first-seed N]
/// civsim-harness ai --pack FILE --check-m6 [--seeds N]
/// civsim-harness ai --pack FILE --exploit [--generations N] [--children N] [--seeds N]
/// </code>
/// A duel puts team A in seats 0 and 3 and team B in seats 1 and 2 (the world is point-symmetric), then swaps the teams,
/// and compares the teams' summed Legacy (Dominion + Mastery + Firsts).
/// </summary>
internal static class AiCommand
{
    public static int Run(string[] args)
    {
        string? packPath = null, duelA = null, duelB = null, ladder = null, ffa = null;
        bool check = false, exploit = false;
        int seeds = 8, minutes = 33, generations = 6, children = 12;
        ulong firstSeed = Tournament.BaseSeed;
        var archetype = EconomyArchetype.Balanced;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pack": packPath = args[++i]; break;
                case "--duel": duelA = args[++i]; duelB = args[++i]; break;
                case "--ladder": ladder = args[++i]; break;
                case "--ffa": ffa = args[++i]; break;
                case "--check-m6": check = true; break;
                case "--exploit": exploit = true; break;
                case "--generations": generations = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--children": children = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--seeds": seeds = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--first-seed": firstSeed = ulong.Parse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture); break;
                case "--minutes": minutes = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--archetype": archetype = Enum.Parse<EconomyArchetype>(args[++i], true); break;
                default: throw new ArgumentException($"Unknown option {args[i]}.");
            }
        }
        if (packPath is null) throw new ArgumentException("ai needs --pack FILE.");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        if (duelA is not null && duelB is not null) { PrintDuel(duelA, duelB, Tournament.Duel(pack, duelA, duelB, seeds, minutes, archetype, firstSeed)); return 0; }
        if (ladder is not null) return Ladder(pack, ladder.Split(','), seeds, minutes, archetype, firstSeed);
        if (ffa is not null) return FreeForAll(pack, ffa, seeds, minutes, firstSeed);
        if (check) return CheckM6(pack, seeds, minutes, archetype, firstSeed);
        if (exploit)
        {
            (BotPolicy best, double rate, int evaluations) = ExploitSearch.Search(pack, generations, children, seeds, minutes, firstSeed);
            Console.WriteLine($"Best fixed policy after {evaluations} evaluations: {rate * 100:F0}% against Warlord (confirmed on fresh seeds)\n  {best}");
            Console.WriteLine(rate <= 0.55 ? "EXPLOIT SEARCH PASSED (<= 55%)" : "EXPLOIT FOUND (> 55%): a bug in the AI");
            return rate <= 0.55 ? 0 : 1;
        }
        throw new ArgumentException("ai needs --duel A B, --ladder a,b,c, --ffa LEVEL or --check-m6.");
    }

    private static void PrintDuel(string a, string b, Tournament.DuelResult r)
    {
        Console.WriteLine($"{a} vs {b}: {r.Matches} matches, {a} wins {r.WinRateA * 100:F0}% (Legacy {r.LegacyA / r.Matches:F1} vs {r.LegacyB / r.Matches:F1})");
        Console.WriteLine($"  settlements lost per match: {a} {r.SettlementsLostA / r.Matches:F2} (in {r.MatchesWithLossA} matches), {b} {r.SettlementsLostB / r.Matches:F2} (in {r.MatchesWithLossB} matches)");
        Console.WriteLine($"  raids launched per match: {a} {(double)r.RaidsA / r.Matches:F1}, {b} {(double)r.RaidsB / r.Matches:F1}");
        Console.WriteLine($"  KEY-1 mean minute: {a} {r.MeanKey1A:F1}, {b} {r.MeanKey1B:F1}");
        Console.WriteLine($"  blunders ({a}): keystone stalls {r.KeystoneStallsA}, idle slot seasons {r.IdleSlotSeasonsA}, dithering matches {r.PlanDitherMatchesA}");
    }

    private static int Ladder(ContentPack pack, string[] names, int seeds, int minutes, EconomyArchetype archetype, ulong firstSeed)
    {
        int n = names.Length;
        var win = new double[n, n];
        var games = new int[n, n];
        for (int a = 0; a < n; a++)
            for (int b = a + 1; b < n; b++)
            {
                Tournament.DuelResult r = Tournament.Duel(pack, names[a], names[b], seeds, minutes, archetype, firstSeed);
                win[a, b] = r.WinRateA;
                games[a, b] = r.Matches;
                Console.WriteLine($"  {names[a],-10} v {names[b],-10} {r.WinRateA * 100,5:F0}% over {r.Matches} matches");
            }
        double[] elo = Tournament.Elo(n, win, games);
        Console.WriteLine("Elo ladder:");
        int[] order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        Array.Sort(order, (x, y) => elo[y].CompareTo(elo[x]));
        foreach (int i in order) Console.WriteLine($"  {names[i],-10} {elo[i],7:F0}");
        return 0;
    }

    private static int FreeForAll(ContentPack pack, string level, int seeds, int minutes, ulong firstSeed)
    {
        (double[] wins, int[] apps) = Tournament.FreeForAll(pack, level, seeds, minutes, out var civs, firstSeed);
        Console.WriteLine($"Free-for-all at {level}: win rate per appearance (25% is fair)");
        for (int i = 0; i < 5; i++) Console.WriteLine($"  {FfaNames[i],-9} {wins[i]:F0}/{apps[i]} = {100.0 * wins[i] / Math.Max(1, apps[i]),5:F1}%");
        PrintArchetypeMeans(civs);
        return 0;
    }

    private static readonly string[] FfaNames = { "Builder", "Grower", "Wanderer", "Warlord", "Sage" };

    /// <summary>Per-archetype means at the end of the match: where each style's Legacy comes from (docs/MVP/14 §17).</summary>
    private static void PrintArchetypeMeans(List<(EconomyArchetype Archetype, CivOutcome Outcome, bool Won)> civs)
    {
        Console.WriteLine("  archetype   Legacy Dominion Mastery  Firsts  Settl  Projs  KEY-1  KEY-2  Raids  Capt  Lost");
        foreach (string name in FfaNames)
        {
            var a = Enum.Parse<EconomyArchetype>(name);
            double n = 0, leg = 0, dom = 0, mas = 0, fir = 0, set = 0, proj = 0, k1 = 0, k2 = 0, raids = 0, capt = 0, lost = 0;
            int n1 = 0, n2 = 0;
            foreach (var (arch, o, _) in civs)
            {
                if (arch != a) continue;
                n++; leg += o.Legacy; dom += o.Dominion; mas += o.Mastery; fir += o.Firsts; set += o.Settlements; proj += o.Projects;
                raids += o.RaidsLaunched; capt += o.SettlementsCaptured; lost += o.SettlementsLost;
                if (!double.IsNaN(o.KeyMinutes[0])) { k1 += o.KeyMinutes[0]; n1++; }
                if (!double.IsNaN(o.KeyMinutes[1])) { k2 += o.KeyMinutes[1]; n2++; }
            }
            if (n == 0) continue;
            Console.WriteLine($"  {name,-9} {leg / n,8:F0} {dom / n,8:F0} {mas / n,7:F0} {fir / n,7:F0} {set / n,6:F1} {proj / n,6:F1} " +
                $"{(n1 > 0 ? k1 / n1 : double.NaN),6:F1} {(n2 > 0 ? k2 / n2 : double.NaN),6:F1} {raids / n,6:F1} {capt / n,5:F2} {lost / n,5:F2}");
        }
        Console.WriteLine("  combat by archetype: L=launches, R=resolved, P=in transit; mean forecast probability / observed rate");
        Console.WriteLine("  assault columns also show held assaults, broken/pending occupations, and captures (match-end censored)");
        foreach (string name in FfaNames)
        {
            EconomyArchetype a = Enum.Parse<EconomyArchetype>(name);
            int n = 0, raidL = 0, raidR = 0, raidPending = 0, raidW = 0, raidForecastCount = 0;
            int assaultL = 0, assaultR = 0, assaultPending = 0, assaultHeld = 0;
            int broken = 0, pending = 0, captured = 0, assaultForecastCount = 0;
            double expectedRaid = 0, expectedCapture = 0;
            foreach (var (arch, o, _) in civs)
            {
                if (arch != a) continue;
                n++;
                raidL += o.RaidsLaunched; raidR += o.RaidsResolved; raidPending += o.RaidExpeditionsPending; raidW += o.RaidsWon;
                expectedRaid += o.PredictedRaidSuccess; raidForecastCount += o.RaidPredictions;
                assaultL += o.AssaultsLaunched; assaultR += o.AssaultsResolved; assaultPending += o.AssaultExpeditionsPending; assaultHeld += o.AssaultsHeld;
                broken += o.OccupationsBroken; pending += o.OccupationsPending; captured += o.SettlementsCaptured;
                expectedCapture += o.PredictedCaptureSuccess; assaultForecastCount += o.AssaultPredictions;
            }
            double forecastRaid = raidForecastCount > 0 ? expectedRaid / raidForecastCount : double.NaN;
            double actualRaid = raidR > 0 ? (double)raidW / raidR : double.NaN;
            double forecastCapture = assaultForecastCount > 0 ? expectedCapture / assaultForecastCount : double.NaN;
            double actualCapture = assaultR > 0 ? (double)captured / assaultR : double.NaN;
            Console.WriteLine($"  {name,-9} {raidL,2}/{raidR,2}/{raidPending,2}/{raidW,2}  {forecastRaid,5:P0}/{actualRaid,5:P0}  " +
                              $"{assaultL,2}/{assaultR,2}/{assaultPending,2}/{assaultHeld,2}  {broken,2}/{pending,2}/{captured,2}  " +
                              $"{forecastCapture,5:P0}/{actualCapture,5:P0}  ({n} seats)");
        }
    }

    /// <summary>Free-for-all seeds of the A5 gate per --seeds (five rotations each).</summary>
    private const int FfaSeedsPerSeed = 3;

    /// <summary>The M6 acceptance gates A1 to A7 that need matches (docs/MVP/17 §4.3). A8 is the unit test FogFairnessTests.</summary>
    private static int CheckM6(ContentPack pack, int seeds, int minutes, EconomyArchetype archetype, ulong firstSeed)
    {
        bool pass = true;
        bool Gate(string name, bool ok, string detail)
        {
            Console.WriteLine($"  {name}: {detail} {(ok ? "PASS" : "FAIL")}");
            pass &= ok;
            return ok;
        }

        Console.WriteLine("== A1: Warlord AI vs the Council bot (mirrored seeds), >= 85% on Legacy");
        Tournament.DuelResult a1 = Tournament.Duel(pack, "warlord", "council", seeds, minutes, archetype, firstSeed);
        Gate("A1", a1.WinRateA >= 0.85, $"{a1.WinRateA * 100:F0}% over {a1.Matches} matches");

        Console.WriteLine("== A2: Warlord AI vs each opening script, >= 70%");
        foreach (string opening in new[] { "grower", "builder", "wanderer" })
        {
            Tournament.DuelResult r = Tournament.Duel(pack, "warlord", opening, seeds, minutes, archetype, firstSeed);
            Gate("A2 " + opening, r.WinRateA >= 0.70, $"{r.WinRateA * 100:F0}% over {r.Matches} matches");
        }

        Console.WriteLine("== A3: Warlord AI vs Rusher: loses no settlement in >= 80% of matches, wins >= 70%");
        Tournament.DuelResult a3 = Tournament.Duel(pack, "warlord", "rusher", seeds, minutes, archetype, firstSeed);
        double clean = 1.0 - (double)a3.MatchesWithLossA / a3.Matches;
        Gate("A3 survive", clean >= 0.80, $"{clean * 100:F0}% of matches without a lost settlement");
        Gate("A3 win", a3.WinRateA >= 0.70, $"{a3.WinRateA * 100:F0}%");

        Console.WriteLine("== A4: Chieftain vs Warlord, Chieftain wins 25-40%");
        Tournament.DuelResult a4 = Tournament.Duel(pack, "chieftain", "warlord", seeds, minutes, archetype, firstSeed);
        Gate("A4", a4.WinRateA >= 0.25 && a4.WinRateA <= 0.40, $"{a4.WinRateA * 100:F0}% over {a4.Matches} matches");

        // Three free-for-all seeds per duel seed (24 seeds, 120 matches at --seeds 8): at 25% the binomial error of a win
        // share is 11% over the 16 appearances of 4 seeds, so the 30% line would fail most balanced builds (14 §17).
        int ffaSeeds = Math.Max(2, seeds * FfaSeedsPerSeed);
        Console.WriteLine($"== A5: archetype balance in four-AI free-for-alls ({ffaSeeds} seeds), no archetype wins > 30%");
        (double[] wins, int[] apps) = Tournament.FreeForAll(pack, "warlord", ffaSeeds, minutes, out var civs, firstSeed);
        for (int i = 0; i < 5; i++) Gate("A5 " + FfaNames[i], wins[i] / Math.Max(1, apps[i]) <= 0.30, $"{100.0 * wins[i] / Math.Max(1, apps[i]):F1}% ({wins[i]:F0}/{apps[i]})");
        PrintArchetypeMeans(civs);

        Console.WriteLine("== A7: blunder detectors (Warlord vs Council)");
        Gate("A7 keystone stalls", a1.KeystoneStallsA == 0, $"{a1.KeystoneStallsA}");
        Gate("A7 dithering", a1.PlanDitherMatchesA == 0, $"{a1.PlanDitherMatchesA} matches");

        Console.WriteLine(pass ? "M6 CHECK PASSED" : "M6 CHECK FAILED");
        return pass ? 0 : 1;
    }
}
