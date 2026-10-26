using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;

namespace CivSim.Harness;

/// <summary>
/// <code>
/// civsim-harness politics --report-combat [--trials N]     the raid table, raid value and capture odds (docs/MVP/13 F3 to F5)
/// civsim-harness politics --report-combat-e3 [--trials N]  Bronze raid and field tables (docs/MVP/22 §7.5)
/// civsim-harness politics --pack FILE --check-m10 [--seed N] [--seeds N] [--minutes N] [--trace-seed N] [--trace-all-seeds]  Bronze politics gates B17–B20 (docs/MVP/22 §16)
/// </code>
/// </summary>
internal static class PoliticsCommand
{
    public static int Run(string[] args)
    {
        int trials = 4000;
        bool combat = false;
        bool combatE3 = false;
        string? packPath = null, checkGolden = null, writeGolden = null;
        bool checkM5 = false;
        bool checkM10 = false;
        bool traceAllSeeds = false;
        int auditSeeds = 0, seedCount = 1;
        ulong seed = ResearchCommand.GoldenSeed;
        int minutes = 30;
        bool minutesSpecified = false;
        var archetypes = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--report-combat": combat = true; break;
                case "--report-combat-e3": combatE3 = true; break;
                case "--pack": packPath = args[++i]; break;
                case "--check-m5": checkM5 = true; break;
                case "--check-m10": checkM10 = true; break;
                case "--check-golden": checkGolden = args[++i]; break;
                case "--write-golden": writeGolden = args[++i]; break;
                case "--audit-contact": auditSeeds = 1; break;
                case "--trace-seed": traceSeed = ulong.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--trace-all-seeds": traceAllSeeds = true; break;
                case "--seeds": seedCount = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--seed": seed = ulong.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--minutes": minutes = int.Parse(args[++i], CultureInfo.InvariantCulture); minutesSpecified = true; break;
                case "--archetypes": archetypes = Array.ConvertAll(args[++i].Split(','), t => Enum.Parse<EconomyArchetype>(t, true)); break;
                case "--trials": trials = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                default: throw new ArgumentException($"Unknown option {args[i]}.");
            }
        }
        if (combat) return ReportCombat(trials);
        if (combatE3) return ReportCombatE3(trials);
        if (packPath is null) throw new ArgumentException("politics needs --report-combat or --pack FILE.");
        if (checkM5) return CheckM5(packPath);
        if (checkM10) return PoliticsM10.Execute(packPath, seed, seedCount, minutesSpecified ? minutes : 64, traceSeed, traceAllSeeds);
        if (auditSeeds > 0) return AuditContact(packPath, seed, seedCount, archetypes);
        return ReportMatch(packPath, seed, minutes, archetypes, checkGolden, writeGolden);
    }

    private static double D(Fix64 value) => value.ToDoubleForDisplay();

    private static int ReportCombat(int trials)
    {
        Console.WriteLine($"Raid table (warband of {CombatTables.RaiderCount} vs {CombatTables.DefenderAdults} adults), {trials} raids per row:");
        Console.WriteLine($"{"defenses",-34}{"success %",10}{"raider hurt %",15}{"defender hurt %",17}{"(dead %)",14}{"net value",11}");
        foreach (RaidScenario s in CombatTables.Scenarios)
        {
            RaidStats r = CombatTables.Run(s, trials, 20261019);
            Console.WriteLine($"{r.Name,-34}{D(r.SuccessPercent),10:F1}{D(r.RaiderCasualtyPercent),15:F1}{D(r.DefenderCasualtyPercent),17:F1}{D(r.RaiderDeathPercent),7:F1}/{D(r.DefenderDeathPercent),5:F1}{D(r.MeanNetValue),11:F1}");
        }
        Console.WriteLine("Capture of a village of 40 adults within one season:");
        foreach ((string name, Defenses d) in new[] { ("thorn barrier", new Defenses(true, false, false, false, false, false, false, false, false, false)),
                                                      ("palisade", new Defenses(false, true, true, false, false, false, false, false, false, false)) })
            foreach (string ratio in new[] { "1.0", "1.5", "2.0", "3.0" })
                Console.WriteLine($"  {name,-14} attacker strength x{ratio}: {D(CombatTables.CaptureSuccessPercent(Fix64.Parse(ratio), d, trials / 2, 20261019)):F1}% held");
        return 0;
    }

    private static int ReportCombatE3(int trials)
    {
        if (trials <= 0) throw new ArgumentOutOfRangeException(nameof(trials), "Trials must be positive.");
        bool pass = true;
        Console.WriteLine($"E3 raid tables, {trials} trials per row, seed {CombatE3Tables.Seed}; target bands are ±10 points (net: ±10 rations).");
        Console.WriteLine($"{"raid scenario",-48}{"success / target",19}{"raider / target",19}{"defender / target",20}{"net / target",18}{"gate",8}");
        foreach (E3RaidRow row in CombatE3Tables.Raids)
        {
            E3RaidResult r = CombatE3Tables.Run(row, trials);
            bool ok = CombatE3Tables.Pass(r, row); pass &= ok;
            Console.WriteLine($"{r.Name,-48}{r.Success,7:F1}/{row.SuccessTarget,5:F1}{r.RaiderHurt,7:F1}/{row.RaiderHurtTarget,5:F1}{r.DefenderHurt,7:F1}/{row.DefenderHurtTarget,6:F1}{r.Net,7:F1}/{row.NetTarget,6:F1}{(ok ? "PASS" : "FAIL"),8}");
        }
        Console.WriteLine("E3 field battles (open ground, 40% break thresholds; rows without an oracle target are diagnostic):");
        Console.WriteLine($"{"field scenario",-48}{"attacker win / target",24}{"gate",8}");
        foreach (E3FieldRow row in CombatE3Tables.Fields)
        {
            E3FieldResult r = CombatE3Tables.Run(row, trials);
            bool ok = CombatE3Tables.Pass(r); pass &= ok;
            string target = r.WinTarget is double t ? $"{t:F0}%" : "—";
            Console.WriteLine($"{r.Name,-48}{r.WinPercent,10:F1}% / {target,-10}{(ok ? "PASS" : "FAIL"),8}");
        }
        Console.WriteLine(pass ? "E3 combat report PASS" : "E3 combat report FAIL");
        return pass ? 0 : 1;
    }

    /// <summary>Plays a match on the Council and reports contact, relations, territory and conflict (docs/MVP/17 M5 gates).</summary>
    private static int ReportMatch(string packPath, ulong seed, int minutes, EconomyArchetype[] archetypes, string? checkGolden, string? writeGolden)
    {
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        var match = MatchModel.Create(seed, pack, archetypes);
        var runner = new SimRunner(match);
        long ticks = minutes * 600L;
        int printed = 0;
        for (long t = 0; t < ticks; t += 150)
        {
            runner.Advance(Math.Min(150, ticks - t));
            PoliticsSystem pol = match.Politics;
            foreach (PoliticsEvent e in pol.Events)
            {
                if (e.Civ > e.Other && e.Other >= 0 && e.Kind is PoliticsEventKind.Aware or PoliticsEventKind.Contact or PoliticsEventKind.Skirmish) continue;
                Console.WriteLine($"  min {e.Tick / 600.0,5:F1}  civ{e.Civ} {e.Kind} other={e.Other} a={e.A} b={e.B}");
                printed++;
            }
            pol.ClearEvents();
            if ((t + 150) % 3000 == 0)
            {
                int[] settlements = new int[match.Research.CivCount];
                for (int i = 0; i < match.Economy.SettlementCount; i++) settlements[match.Economy.SettlementLite(i).Civilization]++;
                Console.WriteLine($"  [min {(t + 150) / 600.0:F0}] claimed {pol.Territory.ClaimedBasisPoints() / 100.0:F1}% settlements {string.Join('/', settlements)} era {match.Research.Era(0)}{match.Research.Era(1)}{match.Research.Era(2)}{match.Research.Era(3)}");
            }
        }
        PoliticsSystem p = match.Politics;
        Console.WriteLine($"After {minutes} min: claimed {p.Territory.ClaimedBasisPoints() / 100.0:F1}% of habitable land; raids {p.RaidCount} (ok {p.RaidsSucceeded}), gifts {p.Gifts}, barter {p.BarterTrips}, wars {p.Wars}, captures {p.CaptureCount}, skirmishes {p.Skirmishes}");
        for (int c = 0; c < match.Research.CivCount; c++)
            Console.WriteLine($"  civ{c}: owns {p.Territory.OwnedTiles(c)} tiles ({p.Territory.ShareBasisPoints(c) / 100.0:F1}%), dominion {p.Territory.Dominion(c).ToDoubleForDisplay():F1}, reach {p.ReachOf(c)}, era {match.Research.Era(c)}");
        for (int a = 0; a < match.Research.CivCount; a++)
            for (int b = a + 1; b < match.Research.CivCount; b++)
                if (p.Diplomacy.Contacted(a, b))
                    Console.WriteLine($"  civ{a}-civ{b}: relations {p.Diplomacy.Relations(a, b)}{(p.Diplomacy.AtWar(a, b) ? " WAR" : "")}{(p.Diplomacy.Allied(a, b) ? " ALLIED" : "")}");
        string golden = string.Join('\n',
            $"seed={seed} ticks={ticks} archetypes={string.Join(',', archetypes)}",
            $"final={runner.CurrentChecksum:x16}",
            $"digest={runner.Digest():x16}");
        Console.WriteLine(golden);
        if (writeGolden is not null) { File.WriteAllText(writeGolden, golden + "\n"); Console.WriteLine($"wrote golden {writeGolden}"); }
        if (checkGolden is not null)
        {
            string expected = File.ReadAllText(checkGolden).Replace("\r\n", "\n").Trim();
            if (expected != golden)
            {
                Console.Error.WriteLine("POLITICS DETERMINISM CHECK FAILED\nexpected:\n" + expected + "\nactual:\n" + golden);
                return 1;
            }
            Console.WriteLine("politics determinism check passed");
        }
        return 0;
    }

    /// <summary>
    /// The M5 exit gate (docs/MVP/17): F1 first contact in minutes 15 to 25 on at least 90% of maps; F3 to F5 from the combat
    /// tables; claimed land at the end of E2 near 07b's 12 to 13%. F2 is a unit test of the roll.
    /// </summary>
    private static int CheckM5(string packPath)
    {
        bool pass = true;
        Console.WriteLine("== F3 to F5: combat tables");
        foreach (RaidScenario scenario in CombatTables.Scenarios)
        {
            RaidStats r = CombatTables.Run(scenario, 4000, 20261019);
            Console.WriteLine($"  {r.Name,-34} success {D(r.SuccessPercent),5:F1}%  raiders hurt {D(r.RaiderCasualtyPercent),5:F1}%  defenders hurt {D(r.DefenderCasualtyPercent),5:F1}%  net value {D(r.MeanNetValue),6:F1}");
        }
        double[] success = { 75, 40, 15, 5 };
        for (int i = 0; i < success.Length; i++)
        {
            double got = D(CombatTables.Run(CombatTables.Scenarios[i], 4000, 20261019).SuccessPercent);
            bool ok = Math.Abs(got - success[i]) <= 10;
            pass &= ok;
            Console.WriteLine($"  F3 row {i + 1}: {got:F1}% vs {success[i]}% (+/-10 points) {(ok ? "PASS" : "FAIL")}");
        }
        double net0 = D(CombatTables.Run(CombatTables.Scenarios[0], 4000, 20261019).MeanNetValue);
        double net2 = D(CombatTables.Run(CombatTables.Scenarios[2], 4000, 20261019).MeanNetValue);
        bool f4 = net0 > 0 && net2 < 0;
        pass &= f4;
        Console.WriteLine($"  F4: net value undefended {net0:F1} (> 0), palisaded {net2:F1} (< 0) {(f4 ? "PASS" : "FAIL")}");
        double capture = D(CombatTables.CaptureSuccessPercent(Fix64.Parse("1.5"), new Defenses(false, true, true, false, false, false, false, false, false, false), 1000, 20261019));
        bool f5 = capture >= 60;
        pass &= f5;
        Console.WriteLine($"  F5: village taken within a season at 1.5x strength: {capture:F0}% (>= 60) {(f5 ? "PASS" : "FAIL")}");

        Console.WriteLine("== F1: first contact");
        pass &= AuditContact(packPath, 20261019, 24, new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced }) == 0;

        Console.WriteLine("== Territory: claimed land at minute 30 (07b: 12.4-13.1% at the end of E2)");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        var shares = new List<double>();
        for (ulong seed = 20261019; seed < 20261025; seed++)
        {
            var match = MatchModel.Create(seed, pack, new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced });
            new SimRunner(match).Advance(30 * 600);
            double share = match.Politics.Territory.ClaimedBasisPoints() / 100.0;
            shares.Add(share);
            Console.WriteLine($"  seed {seed}: {share:F1}%");
        }
        double mean = 0;
        foreach (double v in shares) mean += v / shares.Count;
        bool eachOk = shares.TrueForAll(v => v >= 9.6 && v <= 17.0);
        bool meanOk = mean >= 11.0 && mean <= 14.5;
        pass &= eachOk && meanOk;
        Console.WriteLine($"  mean {mean:F1}% (11.0-14.5), each in 9.6-17.0% {(eachOk && meanOk ? "PASS" : "FAIL")}");
        Console.WriteLine(pass ? "M5 PASS" : "M5 FAIL");
        return pass ? 0 : 1;
    }

    /// <summary>
    /// F1 (docs/MVP/13 §11): first contact between a paired start falls between minute 15 and 25 on at least 90% of
    /// maps. Plays each seed on the Council until both pairs have met or 30 minutes have passed.
    /// </summary>
    private static ulong traceSeed = ulong.MaxValue;

    private static int AuditContact(string packPath, ulong firstSeed, int seeds, EconomyArchetype[] archetypes)
    {
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        int inWindow = 0;
        for (int i = 0; i < seeds; i++)
        {
            ulong seed = firstSeed + (ulong)i;
            var match = MatchModel.Create(seed, pack, archetypes);
            var runner = new SimRunner(match);
            if (traceSeed == seed) match.Politics.Trace = Console.WriteLine;
            double first = double.NaN;
            for (long t = 0; t < 30 * 600 && double.IsNaN(first); t += 150)
            {
                runner.Advance(150);
                foreach (PoliticsEvent e in match.Politics.Events)
                    if (e.Kind == PoliticsEventKind.Contact) { first = (e.Tick + 1) / 600.0; break; }
            }
            bool ok = first >= 15 && first <= 25;
            if (ok) inWindow++;
            Console.WriteLine($"seed {seed}: first contact at minute {(double.IsNaN(first) ? "none by 30" : first.ToString("F1", CultureInfo.InvariantCulture))}  {(ok ? "ok" : "OUT")}");
        }
        double share = 100.0 * inWindow / seeds;
        Console.WriteLine($"F1: {inWindow}/{seeds} seeds in minutes 15-25 ({share:F0}%, need 90%) {(share >= 90 ? "PASS" : "FAIL")}");
        return share >= 90 ? 0 : 1;
    }
}
