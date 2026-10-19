using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CivSim.Sim.Content;
using CivSim.Sim.Economy;

namespace CivSim.Harness;

/// <summary>
/// The AI harness (docs/MVP/14 §10): mirrored duels, free-for-alls, an Elo ladder and the M6 acceptance gate.
/// <code>
/// civsim-harness ai --pack FILE --duel warlord council [--seeds N] [--minutes N] [--archetype balanced]
/// civsim-harness ai --pack FILE --ladder council,rusher,chieftain,warlord [--seeds N]
/// civsim-harness ai --pack FILE --ffa warlord [--seeds N]
/// civsim-harness ai --pack FILE --check-m6 [--seeds N]
/// </code>
/// A duel puts team A in seats 0 and 3 and team B in seats 1 and 2 (the world is point-symmetric), then swaps the teams,
/// and compares the teams' summed Legacy (Dominion + Mastery + Firsts).
/// </summary>
internal static class AiCommand
{
    public static int Run(string[] args)
    {
        string? packPath = null, duelA = null, duelB = null, ladder = null, ffa = null;
        bool check = false;
        int seeds = 8, minutes = 33;
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
                case "--seeds": seeds = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--minutes": minutes = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--archetype": archetype = Enum.Parse<EconomyArchetype>(args[++i], true); break;
                default: throw new ArgumentException($"Unknown option {args[i]}.");
            }
        }
        if (packPath is null) throw new ArgumentException("ai needs --pack FILE.");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        if (duelA is not null && duelB is not null) { PrintDuel(duelA, duelB, Tournament.Duel(pack, duelA, duelB, seeds, minutes, archetype)); return 0; }
        if (ladder is not null) return Ladder(pack, ladder.Split(','), seeds, minutes, archetype);
        if (ffa is not null) return FreeForAll(pack, ffa, seeds, minutes);
        if (check) return CheckM6(pack, seeds, minutes, archetype);
        throw new ArgumentException("ai needs --duel A B, --ladder a,b,c, --ffa LEVEL or --check-m6.");
    }

    private static void PrintDuel(string a, string b, Tournament.DuelResult r)
    {
        Console.WriteLine($"{a} vs {b}: {r.Matches} matches, {a} wins {r.WinRateA * 100:F0}% (Legacy {r.LegacyA / r.Matches:F1} vs {r.LegacyB / r.Matches:F1})");
        Console.WriteLine($"  settlements lost per match: {a} {r.SettlementsLostA / r.Matches:F2} (in {r.MatchesWithLossA} matches), {b} {r.SettlementsLostB / r.Matches:F2} (in {r.MatchesWithLossB} matches)");
        Console.WriteLine($"  KEY-1 mean minute: {a} {r.MeanKey1A:F1}, {b} {r.MeanKey1B:F1}");
        Console.WriteLine($"  blunders ({a}): keystone stalls {r.KeystoneStallsA}, idle slot seasons {r.IdleSlotSeasonsA}, dithering matches {r.PlanDitherMatchesA}");
    }

    private static int Ladder(ContentPack pack, string[] names, int seeds, int minutes, EconomyArchetype archetype)
    {
        int n = names.Length;
        var win = new double[n, n];
        var games = new int[n, n];
        for (int a = 0; a < n; a++)
            for (int b = a + 1; b < n; b++)
            {
                Tournament.DuelResult r = Tournament.Duel(pack, names[a], names[b], seeds, minutes, archetype);
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

    private static int FreeForAll(ContentPack pack, string level, int seeds, int minutes)
    {
        string[] names = { "Builder", "Grower", "Wanderer", "Warlord", "Sage" };
        (double[] wins, int[] apps) = Tournament.FreeForAll(pack, level, seeds, minutes);
        Console.WriteLine($"Free-for-all at {level}: win rate per appearance (25% is fair)");
        for (int i = 0; i < 5; i++) Console.WriteLine($"  {names[i],-9} {wins[i]:F0}/{apps[i]} = {100.0 * wins[i] / Math.Max(1, apps[i]),5:F1}%");
        return 0;
    }

    /// <summary>The M6 acceptance gates A1 to A7 that need matches (docs/MVP/17 §4.3). A8 is the unit test FogFairnessTests.</summary>
    private static int CheckM6(ContentPack pack, int seeds, int minutes, EconomyArchetype archetype)
    {
        bool pass = true;
        bool Gate(string name, bool ok, string detail)
        {
            Console.WriteLine($"  {name}: {detail} {(ok ? "PASS" : "FAIL")}");
            pass &= ok;
            return ok;
        }

        Console.WriteLine("== A1: Warlord AI vs the Council bot (mirrored seeds), >= 85% on Legacy");
        Tournament.DuelResult a1 = Tournament.Duel(pack, "warlord", "council", seeds, minutes, archetype);
        Gate("A1", a1.WinRateA >= 0.85, $"{a1.WinRateA * 100:F0}% over {a1.Matches} matches");

        Console.WriteLine("== A2: Warlord AI vs each opening script, >= 70%");
        foreach (string opening in new[] { "grower", "builder", "wanderer" })
        {
            Tournament.DuelResult r = Tournament.Duel(pack, "warlord", opening, seeds, minutes, archetype);
            Gate("A2 " + opening, r.WinRateA >= 0.70, $"{r.WinRateA * 100:F0}% over {r.Matches} matches");
        }

        Console.WriteLine("== A3: Warlord AI vs Rusher: loses no settlement in >= 80% of matches, wins >= 70%");
        Tournament.DuelResult a3 = Tournament.Duel(pack, "warlord", "rusher", seeds, minutes, archetype);
        double clean = 1.0 - (double)a3.MatchesWithLossA / a3.Matches;
        Gate("A3 survive", clean >= 0.80, $"{clean * 100:F0}% of matches without a lost settlement");
        Gate("A3 win", a3.WinRateA >= 0.70, $"{a3.WinRateA * 100:F0}%");

        Console.WriteLine("== A4: Chieftain vs Warlord, Chieftain wins 25-40%");
        Tournament.DuelResult a4 = Tournament.Duel(pack, "chieftain", "warlord", seeds, minutes, archetype);
        Gate("A4", a4.WinRateA >= 0.25 && a4.WinRateA <= 0.40, $"{a4.WinRateA * 100:F0}% over {a4.Matches} matches");

        Console.WriteLine("== A5: archetype balance in four-AI free-for-alls, no archetype wins > 30%");
        (double[] wins, int[] apps) = Tournament.FreeForAll(pack, "warlord", Math.Max(2, seeds / 2), minutes);
        string[] names = { "Builder", "Grower", "Wanderer", "Warlord", "Sage" };
        for (int i = 0; i < 5; i++) Gate("A5 " + names[i], wins[i] / Math.Max(1, apps[i]) <= 0.30, $"{100.0 * wins[i] / Math.Max(1, apps[i]):F1}% ({wins[i]:F0}/{apps[i]})");

        Console.WriteLine("== A7: blunder detectors (Warlord vs Council)");
        Gate("A7 keystone stalls", a1.KeystoneStallsA == 0, $"{a1.KeystoneStallsA}");
        Gate("A7 dithering", a1.PlanDitherMatchesA == 0, $"{a1.PlanDitherMatchesA} matches");

        Console.WriteLine(pass ? "M6 CHECK PASSED" : "M6 CHECK FAILED");
        return pass ? 0 : 1;
    }
}
