using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Harness;

/// <summary>
/// <code>
/// civsim-harness research --pack FILE [--seed N] [--minutes N] [--bot grower|builder|wanderer|none]
///                         [--archetypes a,b,c,d] [--record FILE] [--check-golden FILE | --write-golden FILE]
/// civsim-harness research --pack FILE --check-m10 [--seed N | --seeds a,b,c] [--minutes N] [--human-seat N]
/// civsim-harness research --pack FILE --report-metal [--decree-mines] [--seed N | --seeds a,b,c] [--minutes N]
/// civsim-harness research --pack FILE --report-e3 [--seed N | --seeds a,b,c] [--minutes N]
/// civsim-harness research --pack FILE --report-decrees [--seed N | --seeds a,b,c] [--minutes N]
/// </code>
/// Runs a four-civilization match (the living economy plus research). Civilization 0 plays the chosen scripted opening
/// (docs/MVP/04 §7) unless <c>--bot none</c>; the others are AI on the Council. Prints Keystone times for C5.
/// </summary>
internal static class ResearchCommand
{
    public const ulong GoldenSeed = 20261019;
    public const int GoldenMinutes = 32;

    public static int Run(string[] args)
    {
        string? packPath = null, record = null, checkGolden = null, writeGolden = null;
        bool trace = false;
        int traceCiv = 0;
        bool checkC5 = false, checkM10 = false, reportMetal = false, reportE3 = false, reportDecrees = false, c1e3 = false, decreeMines = false, metalFixture = false, minutesGiven = false, seedGiven = false;
        int humanSeat = -1;
        List<ulong>? seeds = null;
        string bot = "grower";
        ulong seed = GoldenSeed;
        int minutes = GoldenMinutes;
        var archetypes = new[] { EconomyArchetype.Grower, EconomyArchetype.Builder, EconomyArchetype.Sage, EconomyArchetype.Wanderer };
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pack": packPath = Next(args, ref i); break;
                case "--seed": seed = ulong.Parse(Next(args, ref i), CultureInfo.InvariantCulture); seedGiven = true; break;
                case "--seeds":
                    seeds = new List<ulong>();
                    foreach (string part in Next(args, ref i).Split(',')) seeds.Add(ulong.Parse(part, CultureInfo.InvariantCulture));
                    break;
                case "--minutes": minutes = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); minutesGiven = true; break;
                case "--check-m10": checkM10 = true; break;
                case "--report-metal": reportMetal = true; break;
                case "--report-e3": reportE3 = true; break;
                case "--report-decrees": reportDecrees = true; break;
                case "--c1-e3": c1e3 = true; break;
                case "--decree-mines": decreeMines = true; break;
                case "--metal-fixture": metalFixture = true; break;
                case "--human-seat": humanSeat = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--bot": bot = Next(args, ref i); break;
                case "--archetypes": archetypes = ParseArchetypes(Next(args, ref i)); break;
                case "--trace": trace = true; break;
                case "--trace-civ": traceCiv = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); trace = true; break;
                case "--check-c5": checkC5 = true; break;
                case "--record": record = Next(args, ref i); break;
                case "--check-golden": checkGolden = Next(args, ref i); break;
                case "--write-golden": writeGolden = Next(args, ref i); break;
                default: throw new ArgumentException($"Unknown option {args[i]}.");
            }
        }
        if (packPath is null) throw new ArgumentException("research needs --pack FILE (civsim build-pack --out FILE).");
        if (checkM10)
        {
            // B5 and B6 (docs/MVP/22 §16): play to the end of the Bronze chapter; --minutes only bounds a runaway match.
            ContentPack m10Pack = ContentPack.Load(File.ReadAllBytes(packPath));
            if (seedGiven && seeds is null) seeds = new List<ulong> { seed };
            return ResearchM10.Execute(m10Pack, seeds, minutesGiven ? minutes : 70, humanSeat);
        }
        if (reportDecrees)
        {
            // T11, T14-T16, T20, T26, T27 (docs/MVP/22 §15): Decrees, compacts and Tin Folk under the Council's Decree policy (WP8a).
            ContentPack decreePack = ContentPack.Load(File.ReadAllBytes(packPath));
            if (seedGiven && seeds is null) seeds = new List<ulong> { seed };
            return ResearchDecrees.Execute(decreePack, seeds, minutesGiven ? minutes : 64);
        }
        if (c1e3) return C1E3Report.Execute(ContentPack.Load(File.ReadAllBytes(packPath)), seed);
        if (reportE3)
        {
            // T7, T10-T16, T25 and C3-E3 (docs/MVP/22 §15, 07d §10-§11): the early state measured on four Balanced Council civilizations.
            ContentPack e3Pack = ContentPack.Load(File.ReadAllBytes(packPath));
            if (seedGiven && seeds is null) seeds = new List<ulong> { seed };
            return ResearchE3.Execute(e3Pack, seeds, minutesGiven ? minutes : 64);
        }
        if (metalFixture) return MetalFixtureReport.Execute(ContentPack.Load(File.ReadAllBytes(packPath)), seed);
        if (reportMetal)
        {
            // T17-T19 and B11 (docs/MVP/22 §15-§16): the metal and fuel economy measured on four Balanced Council civilizations.
            ContentPack metalPack = ContentPack.Load(File.ReadAllBytes(packPath));
            if (seedGiven && seeds is null) seeds = new List<ulong> { seed };
            return ResearchMetal.Execute(metalPack, seeds, minutesGiven ? minutes : 64, decreeMines);
        }
        if (checkC5)
        {
            // C5 measures the balance model's reference civilization: four Balanced civilizations on the Council,
            // long enough for KEY-3 (docs/MVP/11 §6, 07c §7a).
            bot = "none";
            archetypes = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
            minutes = Math.Max(minutes, 40);
        }
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        long ticks = minutes * 600L;

        var match = MatchModel.Create(seed, pack, archetypes);
        OpeningBot? opening = bot == "none" ? null : new OpeningBot(match.Research.Catalog, bot);
        var runner = new SimRunner(match);
        var commands = new List<Command>();
        if (opening is not null) commands.Add(new Command(0, 0, CommandKind.SetCouncil, 0, 0, 0));
        for (long t = 0; t < ticks; t++)
        {
            if (opening is not null && !opening.HandedOver && (t + 1) % ResearchSystem.StepTicks == 0)
            {
                var decided = new List<Command>();
                opening.Decide(match.Research, 0, t, decided);
                commands.AddRange(decided);
                foreach (Command c in decided) runner.Submit(c);
            }
            if (t == 0 && opening is not null) runner.Submit(commands[0]);
            runner.Advance(1);
            if (trace && (t + 1) % 600 == 0) Trace(match, (int)((t + 1) / 600), traceCiv);
        }

        string result = Describe(seed, ticks, bot, archetypes, runner, match);
        Console.WriteLine(result);
        if (checkC5 && !ReportC5(match.Research)) return 1;
        if (record is not null)
        {
            var world = CivSim.Sim.Economy.WorldEconomyInput.FromGeneratedWorld(CivSim.Sim.World.WorldGenerator.Generate(seed));
            MatchReplay replay = MatchReplay.FromRun(seed, pack, archetypes, runner, world.Fingerprint);
            using FileStream stream = File.Create(record);
            replay.Write(stream);
            Console.WriteLine($"wrote replay {record}");
        }
        if (writeGolden is not null)
        {
            File.WriteAllText(writeGolden, GoldenText(seed, ticks, bot, runner) + "\n");
            Console.WriteLine($"wrote golden {writeGolden}");
        }
        if (checkGolden is not null)
        {
            string expected = File.ReadAllText(checkGolden).Replace("\r\n", "\n").Trim();
            string actual = GoldenText(seed, ticks, bot, runner);
            if (expected != actual)
            {
                Console.Error.WriteLine("RESEARCH DETERMINISM CHECK FAILED");
                Console.Error.WriteLine("expected:\n" + expected);
                Console.Error.WriteLine("actual:\n" + actual);
                return 1;
            }
            Console.WriteLine("research determinism check passed");
        }
        return 0;
    }

    private static void Trace(MatchModel match, int minute, int civ)
    {
        ResearchSystem r = match.Research;
        ResearchCatalog catalog = r.Catalog;
        for (int c = civ; c <= civ; c++)
        {
            int people = 0, adults = 0, elders = 0;
            for (int f = 0; f < match.Economy.FigureCount; f++)
            {
                FigureView fig = match.Economy.Figure(f);
                if (fig.Civilization != c) continue;
                people++;
                if (fig.Age >= 45) elders++; else if (fig.Age >= 14) adults++;
            }
            double kh = 0;
            for (int l = 0; l < catalog.LineageCodes.Count; l++) kh += r.KnowHow(c, l).ToDoubleForDisplay();
            string slots = "";
            for (int s = 0; s < 3; s++)
            {
                int p = r.Slot(c, s);
                slots += p < 0 ? " -" : $" {catalog.Projects[p].Id}({r.Progress(c, p).ToDoubleForDisplay() / r.EffectiveCost(c, p).ToDoubleForDisplay():P0})";
            }
            Console.WriteLine($"min {minute,2} civ{c} people {people,3} (A{adults} E{elders}) settl {match.Source.SettlementCount(c)} insight/yr {r.InsightPerYear(c).ToDoubleForDisplay(),6:F1} commons {r.CommonsPerYear(c).ToDoubleForDisplay(),6:F1} KHpool {kh,5:F1} done {r.DoneCount(c),2} era {r.Era(c)} |{slots}");
        }
    }

    /// <summary>
    /// C5 (docs/MVP/11 §6): KEY-1, KEY-2 and KEY-3 at about 7.5, 17.5 and 29 minutes, Standard pace, ±25%. The median of the
    /// four civilizations must land in the window and none may stray beyond ±40% or miss the keystone altogether.
    /// </summary>
    private static bool ReportC5(ResearchSystem research)
    {
        double[] target = { 7.5, 17.5, 29.0 };
        bool pass = true;
        Console.WriteLine("C5 keystone timing (minutes, Standard pace):");
        for (int key = 1; key <= 3; key++)
        {
            int p = research.Catalog.IndexOf($"KEY-{key}");
            var times = new List<double>();
            bool missed = false;
            for (int c = 0; c < research.CivCount; c++)
            {
                if (research.IsDone(c, p)) times.Add(research.CompletedTick(c, p) / 600.0);
                else missed = true;
            }
            times.Sort();
            double median = times.Count == 0 ? double.NaN : times.Count % 2 == 1 ? times[times.Count / 2] : (times[times.Count / 2 - 1] + times[times.Count / 2]) / 2;
            double lo = target[key - 1] * 0.75, hi = target[key - 1] * 1.25;
            bool ok = !missed && median >= lo && median <= hi && times[0] >= target[key - 1] * 0.6 && times[^1] <= target[key - 1] * 1.4;
            pass &= ok;
            Console.WriteLine($"  KEY-{key}: median {median:F1} in [{lo:F1}, {hi:F1}], civs {string.Join(" ", times.ConvertAll(t => t.ToString("F1", CultureInfo.InvariantCulture)))}" +
                              (missed ? " (some civilizations did not finish)" : "") + (ok ? "  PASS" : "  FAIL"));
        }
        Console.WriteLine(pass ? "C5 PASS" : "C5 FAIL");
        return pass;
    }

    private static string GoldenText(ulong seed, long ticks, string bot, SimRunner runner)
        => string.Join('\n',
            $"seed={seed} ticks={ticks} bot={bot} commands={runner.AppliedCommands.Count}",
            $"final={runner.CurrentChecksum:x16}",
            $"digest={runner.Digest():x16}");

    private static string Describe(ulong seed, long ticks, string bot, EconomyArchetype[] archetypes, SimRunner runner, MatchModel match)
    {
        ResearchSystem r = match.Research;
        ResearchCatalog catalog = r.Catalog;
        var lines = new List<string>
        {
            GoldenText(seed, ticks, bot, runner),
            $"{"civ",-3}{"archetype",-9}{"projects",9}{"K1 min",8}{"K2 min",8}{"K3 min",8}{"era",5}{"Insight/yr",12}{"KH share",10}{"people",8}{"legacy",8}",
        };
        for (int c = 0; c < r.CivCount; c++)
        {
            string K(int key)
            {
                int p = catalog.IndexOf($"KEY-{key}");
                return r.IsDone(c, p) ? (r.CompletedTick(c, p) / 600.0).ToString("F1", CultureInfo.InvariantCulture) : "-";
            }
            Fix64 khApplied = Fix64.Zero, spent = Fix64.Zero;
            for (int p = 0; p < catalog.Count; p++)
            {
                khApplied += r.KnowHowApplied(c, p);
                spent += r.Progress(c, p);
            }
            double share = spent.Raw == 0 ? 0 : khApplied.ToDoubleForDisplay() / spent.ToDoubleForDisplay();
            int people = 0;
            for (int f = 0; f < match.Economy.FigureCount; f++) if (match.Economy.Figure(f).Civilization == c) people++;
            lines.Add($"{c,-3}{archetypes[c],-9}{r.DoneCount(c),9}{K(1),8}{K(2),8}{K(3),8}{r.Era(c),5}" +
                      $"{r.InsightPerYear(c).ToDoubleForDisplay(),12:F1}{share,10:P0}{people,8}{r.Legacy(c).ToDoubleForDisplay(),8:F0}");
        }
        return string.Join('\n', lines);
    }

    private static EconomyArchetype[] ParseArchetypes(string text)
    {
        string[] parts = text.Split(',');
        if (parts.Length != 4) throw new ArgumentException("--archetypes needs four comma-separated names.");
        var result = new EconomyArchetype[4];
        for (int i = 0; i < 4; i++) result[i] = Enum.Parse<EconomyArchetype>(parts[i], ignoreCase: true);
        return result;
    }

    private static string Next(string[] args, ref int i)
    {
        if (i + 1 >= args.Length) throw new ArgumentException($"{args[i]} needs a value.");
        return args[++i];
    }
}
