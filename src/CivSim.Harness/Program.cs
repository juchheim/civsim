using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;
using CivSim.Sim.Research;
using CivSim.Sim.Toy;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>
/// Headless runner (docs/MVP/15 A9) for the toy determinism canary, generated worlds, and the E0–E2 economy.
/// Full AI tournaments and regression suites are later work.
/// <code>
/// civsim-harness toy [--seed N] [--ticks N] [--checksum-every N]      run and print the result
/// civsim-harness toy --check-golden FILE                              compare against a golden file (CI)
/// civsim-harness toy --write-golden FILE                              (re)write the golden file
/// civsim-harness toy --record FILE                                    write a replay file
/// civsim-harness worldgen [--seed N] [--audit-seeds N] [--check-fingerprint FILE]
///                                                                      generate/report, sweep seeds, or verify a golden fingerprint
/// civsim-harness replay verify FILE                                   re-run a replay and verify checksums
/// civsim-harness economy --pack FILE [--seed N] [--ticks N] [--record FILE]
/// civsim-harness economy --pack FILE --report-c1-reference
/// civsim-harness economy --pack FILE --report-balance-reference --trace-demography
/// civsim-harness replay verify FILE --pack FILE                       verify an economy replay
/// </code>
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0) return Usage();
            return args[0] switch
            {
                "toy" => RunToy(args[1..]),
                "economy" => RunEconomy(args[1..]),
                "research" => ResearchCommand.Run(args[1..]),
                "worldgen" => RunWorldGen(args[1..]),
                "replay" when args.Length >= 3 && args[1] == "verify" => VerifyReplay(args[2..]),
                _ => Usage(),
            };
        }
        catch (Exception e) when (e is IOException or InvalidDataException or FormatException or ArgumentException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 2;
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("usage: civsim-harness toy [--seed N] [--ticks N] [--checksum-every N] [--check-golden FILE | --write-golden FILE | --record FILE]");
        Console.Error.WriteLine("       civsim-harness worldgen [--seed N] [--audit-seeds N] [--check-fingerprint FILE]");
        Console.Error.WriteLine("       civsim-harness replay verify FILE");
        Console.Error.WriteLine("       civsim-harness economy --pack FILE [--seed N] [--ticks N] [--checksum-every N] [--archetypes Balanced,Grower,Builder,Wanderer] [--complete TICK:CIV:PROJECT] [--record FILE] [--check-golden FILE | --write-golden FILE] [--report-economy] [--report-balance-reference] [--report-c1-reference] [--report-c3-midpoints] [--trace-demography]");
        Console.Error.WriteLine("       civsim-harness research --pack FILE [--seed N] [--minutes N] [--bot grower|builder|wanderer|none] [--archetypes a,b,c,d] [--trace] [--record FILE] [--check-golden FILE | --write-golden FILE] [--check-c5]");
        Console.Error.WriteLine("       civsim-harness replay verify FILE --pack FILE");
        return 2;
    }

    private static int RunToy(string[] args)
    {
        ulong seed = ToyScenario.GoldenSeed;
        long ticks = ToyScenario.GoldenTicks;
        int interval = SimRunner.DefaultChecksumInterval;
        string? checkGolden = null, writeGolden = null, record = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seed": seed = ulong.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--ticks": ticks = long.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--checksum-every": interval = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--check-golden": checkGolden = Next(args, ref i); break;
                case "--write-golden": writeGolden = Next(args, ref i); break;
                case "--record": record = Next(args, ref i); break;
                default: return Usage();
            }
        }

        var runner = new SimRunner(new ToyModel(seed), interval);
        foreach (Command c in ToyScenario.Commands(seed, ticks)) runner.Submit(c);
        runner.Advance(ticks);

        string result = Describe(seed, ticks, interval, runner);
        Console.WriteLine(result);

        if (record is not null)
        {
            Replay replay = Replay.Record(new ToyModel(seed), seed, ticks, ToyScenario.Commands(seed, ticks), interval);
            using FileStream stream = File.Create(record);
            replay.Write(stream);
            Console.WriteLine($"wrote replay {record}");
        }
        if (writeGolden is not null)
        {
            File.WriteAllText(writeGolden, result + "\n");
            Console.WriteLine($"wrote golden {writeGolden}");
        }
        if (checkGolden is not null)
        {
            string expected = File.ReadAllText(checkGolden).Replace("\r\n", "\n").Trim();
            if (expected != result.Trim())
            {
                Console.Error.WriteLine("DETERMINISM CHECK FAILED");
                Console.Error.WriteLine("expected:");
                Console.Error.WriteLine(expected);
                Console.Error.WriteLine("actual:");
                Console.Error.WriteLine(result);
                return 1;
            }
            Console.WriteLine("determinism check passed");
        }
        return 0;
    }

    private static int RunEconomy(string[] args)
    {
        ulong seed = 42;
        long ticks = 18_000;
        int interval = SimRunner.DefaultChecksumInterval;
        string? packPath = null, recordPath = null, checkGolden = null, writeGolden = null;
        bool reportEconomy = false, reportBalanceReference = false, reportC1Reference = false,
            reportC3Midpoints = false, traceDemography = false;
        var archetypes = new EconomyArchetype[4];
        var completions = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pack": packPath = Next(args, ref i); break;
                case "--seed": seed = ulong.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--ticks": ticks = long.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--checksum-every": interval = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--record": recordPath = Next(args, ref i); break;
                case "--check-golden": checkGolden = Next(args, ref i); break;
                case "--write-golden": writeGolden = Next(args, ref i); break;
                case "--complete": completions.Add(Next(args, ref i)); break;
                case "--report-economy": reportEconomy = true; break;
                case "--report-balance-reference": reportBalanceReference = true; break;
                case "--report-c1-reference": reportC1Reference = true; break;
                case "--report-c3-midpoints": reportC3Midpoints = true; break;
                case "--trace-demography": traceDemography = true; break;
                case "--archetypes":
                    string[] names = Next(args, ref i).Split(',', StringSplitOptions.TrimEntries);
                    if (names.Length != 4) throw new ArgumentException("Supply four comma-separated archetypes.");
                    for (int civ = 0; civ < names.Length; civ++)
                        if (!Enum.TryParse(names[civ], ignoreCase: true, out archetypes[civ]) || !Enum.IsDefined(archetypes[civ]))
                            throw new ArgumentException($"Unknown archetype '{names[civ]}'.");
                    break;
                default: return Usage();
            }
        }
        if (packPath is null) throw new ArgumentException("Economy runs require --pack with a compiled content pack.");
        if (checkGolden is not null && writeGolden is not null)
            throw new ArgumentException("Choose either --check-golden or --write-golden.");
        if (traceDemography && !reportBalanceReference)
            throw new ArgumentException("--trace-demography requires --report-balance-reference.");
        if (reportC3Midpoints && !reportBalanceReference)
            throw new ArgumentException("--report-c3-midpoints requires --report-balance-reference and a staged E0–E2 schedule.");
        if (reportC3Midpoints && ticks < C3MidpointWindows[^1].LastSampleTick)
            throw new ArgumentException($"--report-c3-midpoints requires at least {C3MidpointWindows[^1].LastSampleTick} ticks to include the E2 window.");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        EconomyContent content = EconomyContent.FromPack(pack);
        if (reportC1Reference) return ReportC1Reference(seed, content);

        var commands = new List<Command>();
        foreach (string completion in completions)
        {
            string[] parts = completion.Split(':');
            if (parts.Length != 3) throw new ArgumentException("--complete requires TICK:CIV:PROJECT, for example 150:0:WOD-01.");
            long tick = long.Parse(parts[0], CultureInfo.InvariantCulture);
            byte civ = byte.Parse(parts[1], CultureInfo.InvariantCulture);
            if (civ >= 4) throw new ArgumentException("Civilization must be 0, 1, 2 or 3.");
            int index = -1;
            foreach (EconomyProject project in content.Projects)
                if (project.Id == parts[2] && project.Era <= 2) { index = project.Index; break; }
            if (index < 0) throw new ArgumentException($"Unknown E0–E2 project '{parts[2]}'.");
            commands.Add(new Command(tick, civ, CommandKind.CompleteProject, index, 0, 0));
        }
        EconomyReplay recording = EconomyReplay.Record(seed, pack, ticks, commands, archetypes, interval);
        ulong digest = 0;
        foreach (ChecksumRecord checksum in recording.Replay.Checksums)
            digest = Hash64.Combine(Hash64.Combine(digest, (ulong)checksum.Tick), checksum.Hash);
        string result = string.Join('\n',
            $"seed={seed} ticks={ticks} checksumInterval={interval} commands={recording.Replay.Commands.Count}",
            $"content={pack.HashHex} world={recording.WorldFingerprint:x16}",
            $"archetypes={string.Join(",", archetypes)}",
            $"final={recording.FinalChecksum:x16}", $"digest={digest:x16}");
        Console.WriteLine(result);
        if (recordPath is not null)
        {
            using FileStream stream = File.Create(recordPath);
            recording.Write(stream);
            Console.WriteLine($"wrote economy replay {recordPath}");
        }
        if (writeGolden is not null)
        {
            File.WriteAllText(writeGolden, result + "\n");
            Console.WriteLine($"wrote golden {writeGolden}");
        }
        if (checkGolden is not null)
        {
            string expected = File.ReadAllText(checkGolden).Replace("\r\n", "\n").Trim();
            if (expected != result)
            {
                Console.Error.WriteLine("ECONOMY DETERMINISM CHECK FAILED");
                Console.Error.WriteLine($"expected:\n{expected}\nactual:\n{result}");
                return 1;
            }
            Console.WriteLine("economy determinism check passed");
        }
        if (reportEconomy)
            ReportEconomy(seed, content, ticks, commands, archetypes, interval,
                WorldEconomyInput.FromGeneratedWorld(WorldGenerator.Generate(seed)), "generated-world", false,
                reportC3Midpoints: reportC3Midpoints);
        if (reportBalanceReference)
            ReportEconomy(seed, content, ticks, commands, archetypes, interval,
                CreateCanonicalBalanceWorld(), "canonical-balance-reference", true, traceDemography,
                reportC3Midpoints);
        return 0;
    }

    private readonly record struct C1ReferenceCheckpoint(string Name, double Position, double Need, double Target);

    private readonly record struct C1ReferenceIncome(double Plants, double Game, double Fish, double Fields,
        double FieldTiles)
    {
        public double Total => Plants + Game + Fish + Fields;
    }

    private readonly record struct C3MidpointWindow(string Name, int Era, long MidpointTick,
        long FirstSampleTick, long LastSampleTick, Fix64 WinterMultiplier,
        Fix64 ChildTarget, Fix64 AdultTarget, Fix64 ElderTarget);

    private static readonly C3MidpointWindow[] C3MidpointWindows =
    [
        // Sample the four season ends after midpoint projects complete, before later-position projects.
        new("E0", 0, 2_325, 2_400, 2_850, Fix64.FromInt(2), Fix64.Parse("0.036"), Fix64.Parse("0.012"), Fix64.Parse("0.053")),
        new("E1", 1, 7_650, 7_800, 8_250, Fix64.Parse("1.7"), Fix64.Parse("0.033"), Fix64.Parse("0.011"), Fix64.Parse("0.053")),
        new("E2", 2, 14_175, 14_250, 14_700, Fix64.Parse("1.4"), Fix64.Parse("0.035"), Fix64.Parse("0.012"), Fix64.Parse("0.057")),
    ];

    private static int C3WindowForSampleTick(long tick)
    {
        if (tick % LivingEconomyModel.SeasonTicks != 0) return -1;
        for (int i = 0; i < C3MidpointWindows.Length; i++)
        {
            C3MidpointWindow window = C3MidpointWindows[i];
            if (tick >= window.FirstSampleTick && tick <= window.LastSampleTick) return i;
        }
        return -1;
    }

    private static int SeasonIndexForEndingTick(long tick)
    {
        if (tick <= 0 || tick % LivingEconomyModel.SeasonTicks != 0)
            throw new ArgumentOutOfRangeException(nameof(tick), "Expected a positive season-ending tick.");
        return (int)(((tick / LivingEconomyModel.SeasonTicks) - 1) % 4);
    }

    private static void WriteC3MidpointWindow(string environment, C3MidpointWindow window,
        string[] ageNames, string[] causeNames, long[,] personSeasons,
        Fix64[,,] expectedHazards, long[,] deaths, Fix64[,] annualHazards,
        long[,,] exactAgePersonSeasons, Fix64[,,,] exactAgeExpectedHazards,
        Fix64[,,] exactAgeWinterShortfall, int windowIndex)
    {
        int sampleCount = checked((int)((window.LastSampleTick - window.FirstSampleTick) /
            LivingEconomyModel.SeasonTicks + 1));
        Fix64 winterAnnualization = (Fix64.FromInt(3) + window.WinterMultiplier) / Fix64.FromInt(4);
        Console.WriteLine($"economy-diagnostic c3-midpoint environment={environment} era={window.Name} nominalTick={window.MidpointTick} fromTick={window.FirstSampleTick} toTick={window.LastSampleTick} samplesAfterNominalTicks={window.FirstSampleTick - window.MidpointTick} seasonalSamples={sampleCount} winterMultiplier={window.WinterMultiplier} effectiveAnnualization={winterAnnualization} targetInterpretation=E1-E0-and-E2-E0-annual-effective-ratios scope=all-civilizations ageWeights=person-time midpointProjects=complete-laterProjects=held");

        for (int age = 0; age < ageNames.Length; age++)
        {
            long exposure = personSeasons[windowIndex, age];
            long observedDeaths = deaths[windowIndex, age];
            Fix64 baseTarget = age switch
            {
                0 => window.ChildTarget,
                1 => window.AdultTarget,
                _ => window.ElderTarget,
            };
            Fix64 target = baseTarget * winterAnnualization;
            if (exposure == 0)
            {
                Console.WriteLine($"economy-diagnostic c3-age-band environment={environment} era={window.Name} age={ageNames[age]} personSeasons=0 personYears=0 expectedAnnualHazard=n/a outsideWinterBaseTarget={baseTarget} annualEffectiveTargetReference={target} targetRole=diagnostic crudeAnnualHazard=n/a deaths=0 causes=[]");
                continue;
            }

            Fix64 expectedTotal = Fix64.Zero;
            string[] causeRates = new string[causeNames.Length];
            for (int cause = 0; cause < causeNames.Length; cause++)
            {
                Fix64 causeHazard = expectedHazards[windowIndex, age, cause] * Fix64.FromInt(4) /
                    Fix64.FromInt(exposure);
                expectedTotal += causeHazard;
                causeRates[cause] = $"{causeNames[cause]}={causeHazard}";
            }

            annualHazards[window.Era, age] = expectedTotal;
            Fix64 crudeAnnualHazard = Fix64.FromInt(observedDeaths) * Fix64.FromInt(4) /
                Fix64.FromInt(exposure);
            string targetRole = age < 2 && window.Era == 0 ? "e0-reference" : age < 2 ? "ratio-reference" : "elder-diagnostic-only";
            Console.WriteLine($"economy-diagnostic c3-age-band environment={environment} era={window.Name} age={ageNames[age]} personSeasons={exposure} personYears={(exposure / 4.0).ToString("F2", CultureInfo.InvariantCulture)} expectedAnnualHazard={expectedTotal} outsideWinterBaseTarget={baseTarget} annualEffectiveTargetReference={target} targetRole={targetRole} deltaPercent=n/a within10=n/a crudeAnnualHazard={crudeAnnualHazard} deaths={observedDeaths} causes=[{string.Join(',', causeRates)}]");
        }

        if (window.Era > 0)
        {
            for (int age = 0; age < 2; age++)
            {
                Fix64 baseline = annualHazards[0, age];
                Fix64 actualRatio = baseline > Fix64.Zero ? annualHazards[window.Era, age] / baseline : Fix64.Zero;
                Fix64 baselineTarget = age == 0 ? C3MidpointWindows[0].ChildTarget : C3MidpointWindows[0].AdultTarget;
                Fix64 eraTarget = age == 0 ? window.ChildTarget : window.AdultTarget;
                Fix64 baselineAnnualization = (Fix64.FromInt(3) + C3MidpointWindows[0].WinterMultiplier) / Fix64.FromInt(4);
                Fix64 targetRatio = (eraTarget / baselineTarget) * (winterAnnualization / baselineAnnualization);
                Fix64 deltaPercent = targetRatio > Fix64.Zero
                    ? (actualRatio / targetRatio - Fix64.One) * Fix64.FromInt(100)
                    : Fix64.Zero;
                bool withinTolerance = Fix64.Abs(deltaPercent) <= Fix64.FromInt(10);
                Console.WriteLine($"economy-diagnostic c3-ratio environment={environment} age={ageNames[age]} era={window.Name} baselineEra=E0 actualAnnualEffectiveRatio={actualRatio} targetAnnualEffectiveRatio={targetRatio} deltaPercent={deltaPercent} within10={withinTolerance}");
            }
        }

        for (int age = 45; age < 100; age++)
        {
            for (int season = 0; season < 4; season++)
            {
                long ageExposure = exactAgePersonSeasons[windowIndex, age, season];
                if (ageExposure == 0) continue;
                string[] causeRates = new string[causeNames.Length];
                Fix64 totalSeasonHazard = Fix64.Zero;
                for (int cause = 0; cause < causeNames.Length; cause++)
                {
                    Fix64 causeRate = exactAgeExpectedHazards[windowIndex, age, season, cause] /
                        Fix64.FromInt(ageExposure);
                    totalSeasonHazard += causeRate;
                    causeRates[cause] = $"{causeNames[cause]}={causeRate}";
                }
                Fix64 winterShortfall = exactAgeWinterShortfall[windowIndex, age, season] / Fix64.FromInt(ageExposure);
                Console.WriteLine($"economy-diagnostic c3-exact-age-season environment={environment} era={window.Name} age={age} season={season} personSeasons={ageExposure} expectedSeasonHazard={totalSeasonHazard} winterShortfallHazard={winterShortfall} causes=[{string.Join(',', causeRates)}]");
            }
        }
    }

    private static int ReportC1Reference(ulong seed, EconomyContent content)
    {
        // This is the fixed, mean-field C1 calculation from check_e0e2.py --fixed. It is
        // isolated from ReportEconomy: no model ticks, AI research choices, deaths, fission,
        // inventory, or harvested stock changes enter these target comparisons.
        C1ReferenceCheckpoint[] checkpoints =
        {
            new("E0-start", 0.0, 3.60, 20.0),
            new("E0-end", 0.999, 3.1334495164663534, 27.0),
            new("E1-end", 1.999, 3.0325188573113, 41.0),
            new("E2-end", 2.999, 3.004678324932085, 88.0),
        };

        WorldEconomyInput world = CreateCanonicalBalanceWorld();
        int[][] seededFields = CreateC1ReferenceFields(world);
        Console.WriteLine("c1-reference mode=analytical-fixed noSimulationSteps=true target=20/27/41/88 tolerance=10%");
        Console.WriteLine("c1-reference assumptions=37-tile-radius-3; temperate-forest reference yields; 15% grass-stand share; mean herd density 0.04/tile; renewable income only; fixed needs from reference age shares (3.60/3.13/3.03/3.00 when rounded)");
        Console.WriteLine("c1-reference projectState=check_e0e2.py fixed positions; optional project probabilities are not sampled; E2 analytical ramp is 9.236 field-equivalents and fixture seeds 9 physical fields; HNT-09 expected weir share is analytical only (fixture seeds no physical weirs)");
        Console.WriteLine($"c1-reference seededE2FieldTiles_perCiv={string.Join(";", seededFields.Select((tiles, civ) => $"civ{civ}:{string.Join(',', tiles)}"))}");

        foreach (C1ReferenceCheckpoint checkpoint in checkpoints)
        {
            C1ReferenceIncome income = C1ReferenceAt(checkpoint.Position);
            double cap = income.Total / checkpoint.Need;
            double delta = 100.0 * (cap / checkpoint.Target - 1.0);
            string projects = string.Join(',', C1ReferenceProjectPositions
                .Where(project => project.Value <= checkpoint.Position)
                .Select(project => project.Key));
            string integerFieldCheck = string.Empty;
            if (checkpoint.Name == "E2-end")
            {
                C1ReferenceIncome seeded = C1ReferenceAt(checkpoint.Position, seededFields[0].Length);
                double seededCap = seeded.Total / checkpoint.Need;
                double seededDelta = 100.0 * (seededCap / checkpoint.Target - 1.0);
                integerFieldCheck = $" seededNineFieldIncome={seeded.Total.ToString("F2", CultureInfo.InvariantCulture)} seededNineFieldCap={seededCap.ToString("F2", CultureInfo.InvariantCulture)} seededNineFieldDeltaPct={seededDelta.ToString("F1", CultureInfo.InvariantCulture)} seededNineFieldPass={Math.Abs(seededDelta) <= 10.0}";
            }
            Console.WriteLine($"c1-reference checkpoint={checkpoint.Name} researchPosition={checkpoint.Position.ToString("F3", CultureInfo.InvariantCulture)} projects={projects} need={checkpoint.Need.ToString("F4", CultureInfo.InvariantCulture)} plants={income.Plants.ToString("F2", CultureInfo.InvariantCulture)} game={income.Game.ToString("F2", CultureInfo.InvariantCulture)} fish={income.Fish.ToString("F2", CultureInfo.InvariantCulture)} fields={income.Fields.ToString("F2", CultureInfo.InvariantCulture)} analyticalFieldTiles={income.FieldTiles.ToString("F3", CultureInfo.InvariantCulture)} sustainableIncome={income.Total.ToString("F2", CultureInfo.InvariantCulture)} analyticalFoodCap={cap.ToString("F2", CultureInfo.InvariantCulture)} target={checkpoint.Target.ToString("F0", CultureInfo.InvariantCulture)} deltaPct={delta.ToString("F1", CultureInfo.InvariantCulture)} pass={Math.Abs(delta) <= 10.0}{integerFieldCheck}");

            string[] projectIds = C1ReferenceProjectPositions
                .Where(project => project.Value <= checkpoint.Position)
                .Select(project => project.Key).ToArray();
            ReadOnlySpan<int> fieldTiles = checkpoint.Name == "E2-end" ? seededFields[0] : ReadOnlySpan<int>.Empty;
            var referenceArchetypes = new EconomyArchetype[world.CivilizationCount];
            Array.Fill(referenceArchetypes, EconomyArchetype.Balanced);
            var fixture = new LivingEconomyModel(seed, content, world, referenceArchetypes);
            fixture.ConfigureC1ReferenceFixture(0, projectIds, fieldTiles, seedHarpoon: true);
            FoodIncomeBreakdown liveIncome = fixture.SustainableFoodIncomeBreakdown(0);
            double liveCapAtReferenceNeed = liveIncome.Total.ToDoubleForDisplay() / checkpoint.Need;
            double liveDelta = 100.0 * (liveCapAtReferenceNeed / checkpoint.Target - 1.0);
            Console.WriteLine($"c1-reference-runtime-fixture checkpoint={checkpoint.Name} steps=0 projects={string.Join(',', projectIds)} fieldTiles={fieldTiles.Length} harpoonSeeded={projectIds.Contains("HNT-07")} plants={liveIncome.Plants} game={liveIncome.Meat} fish={liveIncome.Fish} fields={liveIncome.Grain} renewableIncome={liveIncome.Total} fixedReferenceNeed={checkpoint.Need.ToString("F4", CultureInfo.InvariantCulture)} modelIncomeCap={liveCapAtReferenceNeed.ToString("F2", CultureInfo.InvariantCulture)} target={checkpoint.Target.ToString("F0", CultureInfo.InvariantCulture)} deltaPct={liveDelta.ToString("F1", CultureInfo.InvariantCulture)} withinTolerance={Math.Abs(liveDelta) <= 10.0} gameplayNeed={liveIncome.AnnualNeedPerPerson} gameplaySustainableCapacity={fixture.SustainableFoodCapacity(0)}");
        }

        // Keep the actual game metric visible, but separate: it includes stores and uses
        // the live population need. This zero-step snapshot is not compared with C1 targets.
        var balanced = new EconomyArchetype[world.CivilizationCount];
        Array.Fill(balanced, EconomyArchetype.Balanced);
        var runtime = new LivingEconomyModel(seed, content, world, balanced);
        SettlementView initial = runtime.Settlement(0);
        FoodIncomeBreakdown initialIncome = runtime.SustainableFoodIncomeBreakdown(0);
        Console.WriteLine($"c1-reference runtime=actual-model checkpoint=initial steps=0 archetype=Balanced FoodCap_includesStores={initial.FoodCap} stores={initial.FoodStores} sustainableFoodCapacity_liveNeed={runtime.SustainableFoodCapacity(0)} sustainableIncome={initialIncome.Total} liveNeed={initialIncome.AnnualNeedPerPerson} fields={initial.Fields}");
        Console.WriteLine("c1-reference note=runtime FoodCap is the gameplay value (renewable income plus stores divided by live need); compare fixed targets only to analyticalFoodCap above");
        return 0;
    }

    // Positions are the fixed C1 placements from check_e0e2.py. Its C1 done(pid)
    // rule treats a project as present once its position is reached, even if optional.
    private static readonly IReadOnlyDictionary<string, double> C1ReferenceProjectPositions =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["FOR-01"] = 0.10, ["HNT-01"] = 0.25, ["FOD-01"] = 0.45, ["FIR-01"] = 0.50,
            ["HNT-02"] = 1.20, ["FOR-02"] = 1.40, ["HNT-03"] = 1.45,
            ["HNT-07"] = 1.50, ["HUS-01"] = 1.50, ["FOR-04"] = 1.60, ["HNT-04"] = 1.85,
            ["FOR-05"] = 2.10, ["FOD-04"] = 2.10, ["FOR-06"] = 2.15,
            ["AGR-01"] = 2.30, ["AGR-02"] = 2.50, ["FOD-05"] = 2.65,
            ["AGR-03"] = 2.70, ["HNT-09"] = 2.70,
        };

    private static C1ReferenceIncome C1ReferenceAt(double position, int? seededFieldCount = null)
    {
        const double rangeTiles = 37.0;
        const double annualForestForage = 0.6 + 1.0 + 0.9 + 0.15;
        const double fertility = 0.70;
        const double standShare = 0.15;
        const double meanHerdDensity = 0.04;
        const double herdYield = (22 * 0.30 * 4 + 30 * 0.18 * 15 + 15 * 0.40 * 5) / 3.0;

        bool Done(string id) => C1ReferenceProjectPositions[id] <= position;
        double cook = Done("FOD-01") ? 1.3 : 1.0;
        double boil = Done("FOD-04") ? 1.2 : 1.0;
        double grind = Done("FOR-05") ? 1.25 : 1.0;
        double ferment = Done("FOD-05") ? 1.15 : 1.0;

        double reach = Done("FOR-01") ? 0.52 : 0.40;
        bool roots = Done("FOR-02");
        if (roots) reach *= 1.25;
        double annualForage = annualForestForage + (roots ? 0.15 : 0.0);
        double analyticalFieldTiles = Done("AGR-01")
            ? rangeTiles * 0.25 * Math.Min(1.0, (position - C1ReferenceProjectPositions["AGR-01"]) / 0.7)
            : 0.0;
        double fieldTiles = seededFieldCount ?? analyticalFieldTiles;

        double stand = 1.0;
        if (Done("FOR-04")) stand += standShare * 0.60 * (1.0 + 0.9) / annualForage;
        if (Done("FOR-06")) stand += standShare;
        double plants = (rangeTiles - fieldTiles) * annualForage * reach * stand;
        double rootShare = roots ? (1.25 - 1.0) / 1.25 : 0.0;
        double grainShare = (stand - 1.0) / stand + (Done("FOR-04") ? standShare * 0.4 : 0.0);
        plants *= 1.0 + rootShare * (cook * boil - 1.0) +
            grainShare * (cook * boil * grind - 1.0);

        double access = Done("HNT-04") ? 0.48 : Done("HNT-03") ? 0.44 :
            Done("HNT-02") ? 0.42 : 0.40;
        if (Done("HUS-01")) access *= 1.10;
        double game = rangeTiles * meanHerdDensity * herdYield * access * cook * boil;

        // Mirrors check_e0e2.py: 60% take × two rivers × 1.2 R/season × four
        // seasons × 50% reach; FOD-01 cooking excludes fish, while FOD-04's
        // general food-value effect applies. HNT-09 adds a one-weir 40% share.
        double fish = Done("HNT-07") ? 0.6 * 2.0 * 1.2 * 4.0 * 0.5 * boil : 0.0;
        if (Done("HNT-09")) fish += 0.4 * 2.0 * 4.0 * boil;

        double fields = 0.0;
        if (fieldTiles > 0.0)
        {
            double yield = Done("AGR-02") ? 8.0 : 4.0;
            if (Done("AGR-03")) yield *= Math.Sqrt(1.08);
            fields = fieldTiles * yield * fertility * cook * boil * grind * ferment;
        }
        return new C1ReferenceIncome(plants, game, fish, fields, analyticalFieldTiles);
    }

    private static int[][] CreateC1ReferenceFields(WorldEconomyInput world)
    {
        var fields = new int[world.CivilizationCount][];
        for (int civ = 0; civ < fields.Length; civ++)
        {
            int start = world.StartingTile(civ);
            fields[civ] = Enumerable.Range(0, world.TileCount)
                .Where(tile => HexGrid.Distance(start, tile, world.Width) <= 3 &&
                    world.Tile(tile).FieldYieldPerYear > Fix64.Zero && !world.Tile(tile).WildGrainStand &&
                    !world.Tile(tile).River)
                .OrderBy(tile => HexGrid.Distance(start, tile, world.Width))
                .ThenBy(static tile => tile)
                .Take(9)
                .ToArray();
            if (fields[civ].Length != 9)
                throw new InvalidDataException($"C1 reference range {civ} has only {fields[civ].Length} eligible field tiles; expected nine.");
        }
        return fields;
    }

    private static void ReportEconomy(ulong seed, EconomyContent content, long ticks,
        IReadOnlyList<Command> commands, EconomyArchetype[] archetypes, int interval,
        WorldEconomyInput world, string environment, bool balanceReference, bool traceDemography = false,
        bool reportC3Midpoints = false)
    {
        // Diagnostics use a separate run of the same inputs, preserving the normal recording and
        // golden-output path. Every seasonal death counter is sampled exactly once before it resets.
        var model = new LivingEconomyModel(seed, content, world, archetypes);
        var runner = new SimRunner(model, interval);
        foreach (Command command in commands) runner.Submit(command);
        var firstFissionTicks = new long[model.CivilizationCount];
        Array.Fill(firstFissionTicks, -1);
        var firstPendingTicks = new long[model.CivilizationCount];
        Array.Fill(firstPendingTicks, -1);
        string[] firstPendingTriggers = Enumerable.Repeat("not-observed", model.CivilizationCount).ToArray();
        var lastWinterSecurity = new Fix64?[model.CivilizationCount];
        int knownSettlements = model.SettlementCount;
        string[] causeNames = { "endemic", "epidemic", "starvation", "exposure", "injury", "predators", "violence", "senescence" };
        string[] ageNames = { "child", "adult", "elder" };
        var periodDeaths = new long[ageNames.Length, causeNames.Length];
        var periodExpectedHazards = new Fix64[ageNames.Length, causeNames.Length];
        var periodPersonSeasons = new long[ageNames.Length];
        var periodBirths = new long[model.CivilizationCount];
        var periodConceptions = new long[model.CivilizationCount];
        var periodFertileWomanPersonSeasons = new long[model.CivilizationCount];
        var periodConceptionOpportunityPersonSeasons = new long[model.CivilizationCount];
        var periodPopulationPersonSeasons = new long[model.CivilizationCount];
        var periodFoodSecurityPersonSeasons = new Fix64[model.CivilizationCount];
        var periodDeathsByCivAndCause = new long[model.CivilizationCount, causeNames.Length];
        var c3PersonSeasons = new long[C3MidpointWindows.Length, ageNames.Length];
        var c3ExpectedHazards = new Fix64[C3MidpointWindows.Length, ageNames.Length, causeNames.Length];
        var c3Deaths = new long[C3MidpointWindows.Length, ageNames.Length];
        var c3AnnualHazards = new Fix64[C3MidpointWindows.Length, ageNames.Length];
        var c3ExactAgePersonSeasons = new long[C3MidpointWindows.Length, 100, 4];
        var c3ExactAgeExpectedHazards = new Fix64[C3MidpointWindows.Length, 100, 4, causeNames.Length];
        var c3ExactAgeWinterShortfall = new Fix64[C3MidpointWindows.Length, 100, 4];
        long periodStart = 0;
        const int yearTicks = LivingEconomyModel.SeasonTicks * 4;

        Console.WriteLine(balanceReference
            ? "economy-diagnostic canonical 37-tile mixed forest/woodland and river reference ranges on a roomy map; C1 milestones at 0/4650/10650/17700 ticks; year=600 ticks at Standard pace"
            : "economy-diagnostic generated-world integration metrics; generated starts are resource-enriched; year=600 ticks at Standard pace");
        Console.WriteLine("economy-diagnostic FoodCap and C2 means are per settlement; actual late-winter security is population-weighted; deaths, expected hazards and person-time cover all civilizations; personSeasons samples figures immediately before each seasonal mortality pass (4 seasons = 1 person-year); expected hazards sum the per-figure cause inputs before stochastic sampling; firstFissionTick is the zero-based simulation step that founded a daughter settlement");
        if (reportC3Midpoints)
            Console.WriteLine("economy-diagnostic C3 windows use expected mechanism hazards before stochastic sampling; child/adult acceptance compares annual-effective E1/E0 and E2/E0 ratios; exact-age elder season rates support age-standardized e45; person-time is sampled immediately before seasonal mortality");
        WriteEconomyCheckpoint(model, archetypes, firstFissionTicks, firstPendingTriggers,
            firstPendingTicks, lastWinterSecurity, environment, "initial");
        int balanceMilestone = 0;
        long[] balanceMilestoneTicks = { 4650, 10650, 17700 };
        while (runner.Tick < ticks)
        {
            bool seasonEnd = (runner.Tick + 1) % LivingEconomyModel.SeasonTicks == 0;
            int c3WindowIndex = reportC3Midpoints ? C3WindowForSampleTick(runner.Tick + 1) : -1;
            int c3SeasonIndex = c3WindowIndex >= 0 ? SeasonIndexForEndingTick(runner.Tick + 1) : -1;
            Dictionary<long, FigureView>? demographyBefore = null;
            FigureView[] traceBefore = traceDemography && seasonEnd && runner.Tick + 1 <= 15 * yearTicks
                ? FiguresForSettlement(model, 1)
                : Array.Empty<FigureView>();
            FigureView[] civilizationTraceBefore = traceBefore.Length == 0
                ? Array.Empty<FigureView>()
                : FiguresForCivilization(model, 1);
            SettlementView? traceSettlementBefore = traceBefore.Length == 0
                ? null
                : InitialSettlementForCivilization(model, 1);
            if (seasonEnd)
            {
                demographyBefore = new Dictionary<long, FigureView>(model.FigureCount);
                for (int figureIndex = 0; figureIndex < model.FigureCount; figureIndex++)
                {
                    FigureView figure = model.Figure(figureIndex);
                    demographyBefore.Add(figure.Id, figure);
                    if (c3WindowIndex >= 0 && figure.Age >= 45)
                        c3ExactAgePersonSeasons[c3WindowIndex, figure.Age, c3SeasonIndex]++;
                    if (figure.Female && figure.Age is >= 14 and < 45)
                    {
                        periodFertileWomanPersonSeasons[figure.Civilization]++;
                        if (figure.PregnancySeasons == 0)
                            periodConceptionOpportunityPersonSeasons[figure.Civilization]++;
                    }
                }
                for (int settlement = 0; settlement < model.SettlementCount; settlement++)
                {
                    SettlementView view = model.Settlement(settlement);
                    periodPopulationPersonSeasons[view.Civilization] += view.Population;
                    periodFoodSecurityPersonSeasons[view.Civilization] +=
                        Fix64.FromInt(view.Population) * view.LastFoodSecurity;
                    for (int age = 0; age < ageNames.Length; age++)
                    {
                        periodPersonSeasons[age] += model.AgeBandPopulation(settlement, age);
                        if (c3WindowIndex >= 0)
                            c3PersonSeasons[c3WindowIndex, age] += model.AgeBandPopulation(settlement, age);
                    }
                }
            }
            runner.Advance(1);
            if (demographyBefore is not null)
            {
                for (int deathIndex = 0; deathIndex < model.LastDeathEventCount; deathIndex++)
                {
                    EconomyDeathEvent death = model.LastDeathEvent(deathIndex);
                    periodDeathsByCivAndCause[death.Civilization, (int)death.Cause]++;
                }
                for (int figureIndex = 0; figureIndex < model.FigureCount; figureIndex++)
                {
                    FigureView figure = model.Figure(figureIndex);
                    if (!demographyBefore.TryGetValue(figure.Id, out FigureView beforeFigure))
                    {
                        periodBirths[figure.Civilization]++;
                        continue;
                    }
                    if (beforeFigure.Female && beforeFigure.Age is >= 14 and < 45 &&
                        beforeFigure.PregnancySeasons == 0 && figure.PregnancySeasons == 3)
                        periodConceptions[figure.Civilization]++;
                }
            }
            if (traceBefore.Length > 0 && traceSettlementBefore is { } before)
                WriteDemographyTrace(model, runner.Tick, archetypes[1], before, traceBefore, civilizationTraceBefore);
            if (runner.Tick % LivingEconomyModel.SeasonTicks == 0)
            {
                for (int settlement = 0; settlement < knownSettlements; settlement++)
                {
                    SettlementView view = model.Settlement(settlement);
                    int civ = view.Civilization;
                    if (!view.FissionPending || firstPendingTicks[civ] >= 0) continue;
                    bool capPressure = Fix64.FromInt(view.Population) >= view.EffectivePopulationCap * Fix64.Parse("0.85");
                    bool winterPressure = model.SeasonsCompleted % 4 == 0 &&
                        Fix64.FromInt(view.Population) >= view.LateWinterFissionThreshold;
                    firstPendingTicks[civ] = runner.Tick;
                    firstPendingTriggers[civ] = capPressure && winterPressure ? "cap+late-winter" :
                        capPressure ? "cap" : winterPressure ? "late-winter" : "pending-unclassified";
                    Console.WriteLine($"economy-diagnostic fission-pending environment={environment} tick={runner.Tick} civ={civ} archetype={archetypes[civ]} settlement={view.Id} population={view.Population} capThreshold={(view.EffectivePopulationCap * Fix64.Parse("0.85"))} leanThreshold={view.LateWinterFissionThreshold} actualWinterSecurity={view.LastLateWinterFoodShare} triggerCandidate={firstPendingTriggers[civ]}");
                }
            }
            for (int settlement = knownSettlements; settlement < model.SettlementCount; settlement++)
            {
                int civ = model.Settlement(settlement).Civilization;
                if (firstFissionTicks[civ] < 0) firstFissionTicks[civ] = runner.Tick - 1;
            }
            knownSettlements = model.SettlementCount;
            if (runner.Tick % LivingEconomyModel.SeasonTicks == 0)
            {
                if (c3WindowIndex >= 0)
                {
                    int seasonIndex = SeasonIndexForEndingTick(runner.Tick);
                    for (int exactAge = 45; exactAge < 100; exactAge++)
                    {
                        ReadOnlySpan<Fix64> exactAgeHazards = model.LastExpectedHazardsByExactAgeAndCause(exactAge);
                        for (int cause = 0; cause < exactAgeHazards.Length; cause++)
                            c3ExactAgeExpectedHazards[c3WindowIndex, exactAge, seasonIndex, cause] += exactAgeHazards[cause];
                        c3ExactAgeWinterShortfall[c3WindowIndex, exactAge, seasonIndex] +=
                            model.LastExpectedWinterShortfallByExactAge(exactAge);
                    }
                }
                for (int age = 0; age < ageNames.Length; age++)
                {
                    ReadOnlySpan<int> deaths = model.LastDeathsByCauseAndAgeBand(age);
                    ReadOnlySpan<Fix64> expectedHazards = model.LastExpectedHazardsByCauseAndAgeBand(age);
                    if (deaths.Length != causeNames.Length)
                        throw new InvalidDataException("Economy diagnostic death-cause labels differ from model counters.");
                    if (expectedHazards.Length != causeNames.Length)
                        throw new InvalidDataException("Economy diagnostic expected-hazard labels differ from model counters.");
                    for (int cause = 0; cause < deaths.Length; cause++) periodDeaths[age, cause] += deaths[cause];
                    for (int cause = 0; cause < expectedHazards.Length; cause++)
                        periodExpectedHazards[age, cause] += expectedHazards[cause];
                    if (c3WindowIndex >= 0)
                    {
                        for (int cause = 0; cause < deaths.Length; cause++)
                        {
                            c3Deaths[c3WindowIndex, age] += deaths[cause];
                            c3ExpectedHazards[c3WindowIndex, age, cause] += expectedHazards[cause];
                        }
                    }
                }
                if (c3WindowIndex >= 0 && runner.Tick == C3MidpointWindows[c3WindowIndex].LastSampleTick)
                    WriteC3MidpointWindow(environment, C3MidpointWindows[c3WindowIndex], ageNames,
                        causeNames, c3PersonSeasons, c3ExpectedHazards, c3Deaths, c3AnnualHazards,
                        c3ExactAgePersonSeasons, c3ExactAgeExpectedHazards,
                        c3ExactAgeWinterShortfall, c3WindowIndex);
            }
            bool annual = runner.Tick % yearTicks == 0;
            bool atBalanceMilestone = balanceReference && balanceMilestone < balanceMilestoneTicks.Length &&
                runner.Tick == balanceMilestoneTicks[balanceMilestone];
            if (annual || runner.Tick == ticks || atBalanceMilestone)
            {
                string checkpoint = atBalanceMilestone ? $"C1-E{balanceMilestone}-end" :
                    annual ? "annual" : "final-partial";
                WriteEconomyCheckpoint(model, archetypes, firstFissionTicks, firstPendingTriggers,
                    firstPendingTicks, lastWinterSecurity, environment, checkpoint);
                for (int age = 0; age < ageNames.Length; age++)
                {
                    var counts = new string[causeNames.Length];
                    long ageDeaths = 0;
                    Fix64 expectedTotal = Fix64.Zero;
                    for (int cause = 0; cause < causeNames.Length; cause++)
                    {
                        counts[cause] = $"{causeNames[cause]}={periodDeaths[age, cause]}";
                        ageDeaths += periodDeaths[age, cause];
                        periodDeaths[age, cause] = 0;
                    }
                    var expectedByCause = new string[causeNames.Length];
                    Fix64 annualizer = periodPersonSeasons[age] > 0
                        ? Fix64.FromRatio(4, periodPersonSeasons[age])
                        : Fix64.Zero;
                    for (int cause = 0; cause < causeNames.Length; cause++)
                    {
                        expectedTotal += periodExpectedHazards[age, cause];
                        Fix64 expectedAnnual = periodExpectedHazards[age, cause] * annualizer;
                        expectedByCause[cause] = $"expected_{causeNames[cause]}={expectedAnnual}";
                        periodExpectedHazards[age, cause] = Fix64.Zero;
                    }
                    double personYears = periodPersonSeasons[age] / 4.0;
                    string annualizedHazard = personYears > 0
                        ? (ageDeaths / personYears).ToString("F6", CultureInfo.InvariantCulture)
                        : "n/a";
                    Fix64 expectedAnnualHazard = expectedTotal * annualizer;
                    Console.WriteLine($"economy-diagnostic deaths environment={environment} scope=all-civilizations checkpoint={checkpoint} fromTick={periodStart} toTick={runner.Tick} ageBand={ageNames[age]} deaths={ageDeaths} personSeasons={periodPersonSeasons[age]} personYears={personYears.ToString("F2", CultureInfo.InvariantCulture)} crudeAnnualHazard={annualizedHazard} expectedAnnualHazard={expectedAnnualHazard} {string.Join(" ", counts)} {string.Join(" ", expectedByCause)}");
                    periodPersonSeasons[age] = 0;
                }
                for (int civ = 0; civ < model.CivilizationCount; civ++)
                {
                    double fertileWomanYears = periodFertileWomanPersonSeasons[civ] / 4.0;
                    double conceptionOpportunityYears = periodConceptionOpportunityPersonSeasons[civ] / 4.0;
                    double populationPersonYears = periodPopulationPersonSeasons[civ] / 4.0;
                    double foodSecurityPersonYears = periodFoodSecurityPersonSeasons[civ].ToDoubleForDisplay() / 4.0;
                    string meanFoodSecurity = periodPopulationPersonSeasons[civ] == 0
                        ? "n/a"
                        : (periodFoodSecurityPersonSeasons[civ] / Fix64.FromInt(periodPopulationPersonSeasons[civ])).ToString();
                    string birthsPerFertileWomanYear = fertileWomanYears <= 0
                        ? "n/a"
                        : (periodBirths[civ] / fertileWomanYears).ToString("F3", CultureInfo.InvariantCulture);
                    var deaths = new string[causeNames.Length];
                    for (int cause = 0; cause < causeNames.Length; cause++)
                    {
                        deaths[cause] = $"{causeNames[cause]}={periodDeathsByCivAndCause[civ, cause]}";
                        periodDeathsByCivAndCause[civ, cause] = 0;
                    }
                    Console.WriteLine($"economy-diagnostic demography environment={environment} scope=civilization checkpoint={checkpoint} civ={civ} archetype={archetypes[civ]} fromTick={periodStart} toTick={runner.Tick} births={periodBirths[civ]} conceptions={periodConceptions[civ]} fertileWomanPersonSeasons={periodFertileWomanPersonSeasons[civ]} fertileWomanPersonYears={fertileWomanYears.ToString("F2", CultureInfo.InvariantCulture)} conceptionOpportunityPersonSeasons={periodConceptionOpportunityPersonSeasons[civ]} conceptionOpportunityPersonYears={conceptionOpportunityYears.ToString("F2", CultureInfo.InvariantCulture)} birthsPerFertileWomanYear={birthsPerFertileWomanYear} populationPersonSeasons={periodPopulationPersonSeasons[civ]} populationPersonYears={populationPersonYears.ToString("F2", CultureInfo.InvariantCulture)} foodSecurityPersonSeasons={periodFoodSecurityPersonSeasons[civ]} foodSecurityPersonYears={foodSecurityPersonYears.ToString("F2", CultureInfo.InvariantCulture)} meanPriorSeasonFoodSecurity={meanFoodSecurity} deaths=[{string.Join(',', deaths)}]");
                    periodBirths[civ] = 0;
                    periodConceptions[civ] = 0;
                    periodFertileWomanPersonSeasons[civ] = 0;
                    periodConceptionOpportunityPersonSeasons[civ] = 0;
                    periodPopulationPersonSeasons[civ] = 0;
                    periodFoodSecurityPersonSeasons[civ] = Fix64.Zero;
                }
                periodStart = runner.Tick;
                if (atBalanceMilestone) balanceMilestone++;
            }
        }
    }

    private static FigureView[] FiguresForCivilization(LivingEconomyModel model, int civ)
    {
        var figures = new List<FigureView>();
        for (int i = 0; i < model.FigureCount; i++)
        {
            FigureView figure = model.Figure(i);
            if (figure.Civilization == civ) figures.Add(figure);
        }
        return figures.ToArray();
    }

    private static FigureView[] FiguresForSettlement(LivingEconomyModel model, int settlementId)
    {
        var figures = new List<FigureView>();
        for (int i = 0; i < model.FigureCount; i++)
        {
            FigureView figure = model.Figure(i);
            if (figure.SettlementId == settlementId) figures.Add(figure);
        }
        return figures.ToArray();
    }

    private static SettlementView InitialSettlementForCivilization(LivingEconomyModel model, int civ)
    {
        for (int i = 0; i < model.SettlementCount; i++)
        {
            SettlementView settlement = model.Settlement(i);
            if (settlement.Id == civ) return settlement;
        }
        throw new InvalidDataException($"Initial settlement for civilization {civ} is missing.");
    }

    private static void WriteDemographyTrace(LivingEconomyModel model, long tick,
        EconomyArchetype archetype, SettlementView before, FigureView[] figuresBefore,
        FigureView[] civilizationFiguresBefore)
    {
        FigureView[] figuresAfter = FiguresForSettlement(model, before.Id);
        FigureView[] civilizationAfter = FiguresForCivilization(model, before.Civilization);
        var beforeIds = figuresBefore.Select(static f => f.Id).ToHashSet();
        var civilizationBeforeIds = civilizationFiguresBefore.Select(static f => f.Id).ToHashSet();
        var civilizationAfterIds = civilizationAfter.Select(static f => f.Id).ToHashSet();
        int births = civilizationAfter.Count(f => !civilizationBeforeIds.Contains(f.Id));
        int[] deathsByAge = new int[3];
        foreach (FigureView figure in civilizationFiguresBefore)
            if (!civilizationAfterIds.Contains(figure.Id)) deathsByAge[figure.Age < 14 ? 0 : figure.Age < 45 ? 1 : 2]++;

        SettlementView after = InitialSettlementForCivilization(model, before.Civilization);
        int fertileWomen = figuresAfter.Count(static f => f.Female && f.Age is >= 14 and < 45);
        int pregnantWomen = figuresAfter.Count(static f => f.Female && f.Age is >= 14 and < 45 && f.PregnancySeasons > 0);
        int newConceptions = figuresAfter.Count(f => f.Female && f.Age is >= 14 and < 45 && f.PregnancySeasons == 3 &&
            figuresBefore.Any(previous => previous.Id == f.Id && previous.PregnancySeasons == 0));
        string exactAges = string.Join(',', figuresAfter
            .GroupBy(static f => (f.Female, f.Age))
            .OrderBy(static group => group.Key.Female ? 0 : 1)
            .ThenBy(static group => group.Key.Age)
            .Select(static group => $"{(group.Key.Female ? 'F' : 'M')}{group.Key.Age}:{group.Count()}"));
        string deathEvents = string.Join(';', Enumerable.Range(0, model.LastDeathEventCount)
            .Select(model.LastDeathEvent)
            .Where(death => death.Civilization == before.Civilization)
            .Select(static death => $"id={death.FigureId},age={death.Age},sex={(death.Female ? 'F' : 'M')},pregnancy={death.PregnancySeasons},cause={death.Cause},hazard={death.TotalHazard},endemic={death.EndemicHazard},epidemic={death.EpidemicHazard},starvation={death.StarvationHazard},exposure={death.ExposureHazard},injury={death.InjuryHazard},predators={death.PredatorsHazard},violence={death.ViolenceHazard},senescence={death.SenescenceHazard}"));
        double meanHunger = figuresAfter.Length == 0
            ? 0.0
            : figuresAfter.Average(static f => f.Hunger.ToDoubleForDisplay());
        string season = ((tick / LivingEconomyModel.SeasonTicks - 1) % 4) switch
        {
            0 => "spring", 1 => "summer", 2 => "autumn", _ => "winter",
        };
        double year = (double)tick / (LivingEconomyModel.SeasonTicks * 4);
        string capThreshold = (after.EffectivePopulationCap * Fix64.Parse("0.85")).ToString();
        Console.WriteLine($"economy-demography tick={tick} year={year.ToString("F2", CultureInfo.InvariantCulture)} season={season} civ={before.Civilization} archetype={archetype} scope=initial-settlement+dynamics-civilization population_initialSettlement={after.Population} population_civilization={civilizationAfter.Length} births_civilization={births} conceptions_initialSettlement={newConceptions} fertileWomen_initialSettlement={fertileWomen} pregnantWomen_initialSettlement={pregnantWomen} deaths_child_civilization={deathsByAge[0]} deaths_adult_civilization={deathsByAge[1]} deaths_elder_civilization={deathsByAge[2]} deathEvents_civilization=[{deathEvents}] FoodSecurity_prev_initialSettlement={before.LastFoodSecurity} FoodSecurity_current_initialSettlement={after.LastFoodSecurity} hunger_mean_initialSettlement={meanHunger.ToString("F3", CultureInfo.InvariantCulture)} FoodStores_initialSettlement={after.FoodStores} cap85_initialSettlement={capThreshold} LeanFoodCapacity_initialSettlement={after.LeanFoodCapacity} winterThreshold_initialSettlement={after.LateWinterFissionThreshold} lateWinterSecurity_initialSettlement={after.LastLateWinterFoodShare} fields_initialSettlement={after.Fields} fissionPending_initialSettlement={after.FissionPending} agesSex_initialSettlement={exactAges}");
    }

    private static void WriteEconomyCheckpoint(LivingEconomyModel model, EconomyArchetype[] archetypes,
        long[] firstFissionTicks, string[] firstPendingTriggers, long[] firstPendingTicks,
        Fix64?[] lastWinterSecurity, string environment, string checkpoint)
    {
        var populations = new int[model.CivilizationCount];
        var settlements = new int[model.CivilizationCount];
        var fieldTiles = new int[model.CivilizationCount];
        var ageBandPopulation = new int[model.CivilizationCount, 3];
        var foodCaps = new Fix64[model.CivilizationCount];
        var housingCaps = new Fix64[model.CivilizationCount];
        var groupCaps = new Fix64[model.CivilizationCount];
        var effectiveCaps = new Fix64[model.CivilizationCount];
        var leanFoodCapacities = new Fix64[model.CivilizationCount];
        var lateWinterFissionThresholds = new Fix64[model.CivilizationCount];
        var pendingFissions = new int[model.CivilizationCount];
        var sustainableFoodCaps = new Fix64[model.CivilizationCount];
        var sustainablePlants = new Fix64[model.CivilizationCount];
        var sustainableMeat = new Fix64[model.CivilizationCount];
        var sustainableFish = new Fix64[model.CivilizationCount];
        var sustainableGrain = new Fix64[model.CivilizationCount];
        var sustainableFruit = new Fix64[model.CivilizationCount];
        var c2Shares = new Fix64[model.CivilizationCount];
        var winterSecurity = new Fix64[model.CivilizationCount];
        for (int i = 0; i < model.SettlementCount; i++)
        {
            SettlementView settlement = model.Settlement(i);
            int civ = settlement.Civilization;
            populations[civ] += settlement.Population;
            settlements[civ]++;
            fieldTiles[civ] += settlement.Fields;
            foodCaps[civ] += settlement.FoodCap;
            housingCaps[civ] += settlement.HousingCap;
            groupCaps[civ] += settlement.GroupCap;
            effectiveCaps[civ] += settlement.EffectivePopulationCap;
            leanFoodCapacities[civ] += settlement.LeanFoodCapacity;
            lateWinterFissionThresholds[civ] += settlement.LateWinterFissionThreshold;
            if (settlement.FissionPending) pendingFissions[civ]++;
            sustainableFoodCaps[civ] += model.SustainableFoodCapacity(i);
            FoodIncomeBreakdown income = model.SustainableFoodIncomeBreakdown(i);
            sustainablePlants[civ] += income.Plants;
            sustainableMeat[civ] += income.Meat;
            sustainableFish[civ] += income.Fish;
            sustainableGrain[civ] += income.Grain;
            sustainableFruit[civ] += income.Fruit;
            c2Shares[civ] += settlement.C2LateWinterCapacityShare;
            winterSecurity[civ] += settlement.LastLateWinterFoodShare * Fix64.FromInt(settlement.Population);
            for (int age = 0; age < 3; age++)
                ageBandPopulation[civ, age] += model.AgeBandPopulation(i, age);
        }
        for (int civ = 0; civ < model.CivilizationCount; civ++)
        {
            if (checkpoint == "annual")
                lastWinterSecurity[civ] = populations[civ] == 0 ? null : winterSecurity[civ] / populations[civ];
            string foodMean = settlements[civ] == 0 ? "n/a" : (foodCaps[civ] / settlements[civ]).ToString();
            string housingMean = settlements[civ] == 0 ? "n/a" : (housingCaps[civ] / settlements[civ]).ToString();
            string groupMean = settlements[civ] == 0 ? "n/a" : (groupCaps[civ] / settlements[civ]).ToString();
            string effectiveMean = settlements[civ] == 0 ? "n/a" : (effectiveCaps[civ] / settlements[civ]).ToString();
            string capTriggerMean = settlements[civ] == 0 ? "n/a" : (effectiveCaps[civ] / settlements[civ] * Fix64.Parse("0.85")).ToString();
            string leanCapacityMean = settlements[civ] == 0 ? "n/a" : (leanFoodCapacities[civ] / settlements[civ]).ToString();
            string leanThresholdMean = settlements[civ] == 0 ? "n/a" : (lateWinterFissionThresholds[civ] / settlements[civ]).ToString();
            string sustainableFoodMean = settlements[civ] == 0 ? "n/a" : (sustainableFoodCaps[civ] / settlements[civ]).ToString();
            string plantsMean = settlements[civ] == 0 ? "n/a" : (sustainablePlants[civ] / settlements[civ]).ToString();
            string meatMean = settlements[civ] == 0 ? "n/a" : (sustainableMeat[civ] / settlements[civ]).ToString();
            string fishMean = settlements[civ] == 0 ? "n/a" : (sustainableFish[civ] / settlements[civ]).ToString();
            string grainMean = settlements[civ] == 0 ? "n/a" : (sustainableGrain[civ] / settlements[civ]).ToString();
            string fruitMean = settlements[civ] == 0 ? "n/a" : (sustainableFruit[civ] / settlements[civ]).ToString();
            string c2Mean = settlements[civ] == 0 ? "n/a" : (c2Shares[civ] / settlements[civ]).ToString();
            string security = model.SeasonsCompleted < 4 ? "not-observed" : lastWinterSecurity[civ]?.ToString() ?? "n/a";
            long winterTick = model.SeasonsCompleted / 4 * (LivingEconomyModel.SeasonTicks * 4);
            string firstFission = firstFissionTicks[civ] < 0 ? "not-observed" : firstFissionTicks[civ].ToString(CultureInfo.InvariantCulture);
            string firstPendingTick = firstPendingTicks[civ] < 0 ? "not-observed" : firstPendingTicks[civ].ToString(CultureInfo.InvariantCulture);
            string ageBands = $"child:{ageBandPopulation[civ, 0]},adult:{ageBandPopulation[civ, 1]},elder:{ageBandPopulation[civ, 2]}";
            string fieldMean = settlements[civ] == 0 ? "n/a" : ((double)fieldTiles[civ] / settlements[civ]).ToString("F2", CultureInfo.InvariantCulture);
            Console.WriteLine($"economy-diagnostic environment={environment} checkpoint={checkpoint} tick={model.Tick} year={model.Tick / (LivingEconomyModel.SeasonTicks * 4)} civ={civ} archetype={archetypes[civ]} settlements={settlements[civ]} population={populations[civ]} ageBands={ageBands} fields_total={fieldTiles[civ]} fields_mean={fieldMean} FoodCap_total={foodCaps[civ]} FoodCap_mean={foodMean} HousingCap_mean={housingMean} GroupCap_mean={groupMean} EffectivePopCap_mean={effectiveMean} CapTriggerPopulation_mean={capTriggerMean} LeanFoodCapacity_mean={leanCapacityMean} LateWinterFissionThreshold_mean={leanThresholdMean} FissionPending_settlements={pendingFissions[civ]} C1SustainableFoodCap_mean={sustainableFoodMean} sustainableIncome_plants={plantsMean} sustainableIncome_meat={meatMean} sustainableIncome_fish={fishMean} sustainableIncome_grain={grainMean} sustainableIncome_fruit={fruitMean} C2LateWinterCapacityShare_mean={c2Mean} actualLateWinterSecurity_populationWeighted={security} actualLateWinterSecurity_tick={winterTick} firstPendingTrigger={firstPendingTriggers[civ]} firstPendingTick={firstPendingTick} firstFissionTick={firstFission}");
        }
    }

    private static WorldEconomyInput CreateCanonicalBalanceWorld()
    {
        const int width = 128;
        const int height = 48;
        var forest = new WorldEconomyTile(
            (int)Biome.TemperateForest, (int)ClimateBand.Temperate, Fix64.Parse("0.70"),
            Fix64.Parse("2.12"), Fix64.Parse("0.24"), Fix64.Parse("0.40"),
            Fix64.Parse("0.36"), Fix64.Parse("0.06"), Fix64.Parse("5.496"),
            Fix64.Parse("1.832"), Fix64.FromInt(30), Fix64.FromInt(2),
            Fix64.Zero, Fix64.Zero, Fix64.Parse("2.8"),
            false, false, false, false, Fix64.Zero, Fix64.Zero, Fix64.Zero);
        var openWoodland = forest with
        {
            BiomeId = (int)Biome.OpenWoodland,
            SoilFertility = Fix64.Parse("0.65"),
            InitialForage = Fix64.Parse("2.332"),
            SpringForage = Fix64.Parse("0.264"),
            SummerForage = Fix64.Parse("0.44"),
            AutumnForage = Fix64.Parse("0.396"),
            WinterForage = Fix64.Parse("0.066"),
            FieldYieldPerYear = Fix64.Parse("2.6"),
            WildGrainStand = true,
        };
        var tiles = new WorldEconomyTile[width * height];
        Array.Fill(tiles, forest);
        int[] starts =
        {
            HexGrid.Index(12, 24, width, height), HexGrid.Index(42, 24, width, height),
            HexGrid.Index(72, 24, width, height), HexGrid.Index(102, 24, width, height),
        };
        Span<int> neighbors = stackalloc int[6];

        for (int civ = 0; civ < starts.Length; civ++)
        {
            var range = new List<int>();
            for (int tile = 0; tile < tiles.Length; tile++)
                if (HexGrid.Distance(starts[civ], tile, width) <= 3) range.Add(tile);
            if (range.Count != 37) throw new InvalidDataException($"Reference range {civ} has {range.Count} tiles; expected 37.");

            int standCount = civ is 0 or 2 ? 5 : 6;
            foreach (int tile in range.OrderByDescending(tile => HexGrid.Distance(starts[civ], tile, width))
                         .ThenBy(static tile => tile).Take(standCount))
                tiles[tile] = openWoodland;

            int nearRiver = range.Where(tile => HexGrid.Distance(starts[civ], tile, width) == 1)
                .OrderBy(static tile => tile).First();
            int neighborCount = HexGrid.Neighbors(nearRiver, width, height, neighbors);
            int farRiver = -1;
            for (int i = 0; i < neighborCount; i++)
            {
                int candidate = neighbors[i];
                if (HexGrid.Distance(starts[civ], candidate, width) == 2) { farRiver = candidate; break; }
            }
            if (farRiver < 0) throw new InvalidDataException($"Reference range {civ} could not place two connected river tiles.");
            foreach (int river in new[] { nearRiver, farRiver })
                tiles[river] = tiles[river] with
                {
                    FreshWater = true,
                    River = true,
                    InitialFish = Fix64.Parse("2.4"),
                    FishGrowthPerYear = Fix64.Parse("4.8"),
                };
        }
        return new WorldEconomyInput(width, height, tiles, starts);
    }

    private static int RunWorldGen(string[] args)
    {
        ulong seed = 1;
        int auditSeeds = 1;
        string? checkFingerprint = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seed": seed = ulong.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--audit-seeds": auditSeeds = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--check-fingerprint": checkFingerprint = Next(args, ref i); break;
                default: return Usage();
            }
        }
        if (auditSeeds < 1) throw new ArgumentOutOfRangeException(nameof(auditSeeds), "Audit count must be positive.");
        if (checkFingerprint is not null && auditSeeds != 1)
            throw new ArgumentException("Fingerprint checks require exactly one seed.");

        int failures = 0, riverStarts = 0, coastalStarts = 0, accepted = 0;
        int minLand = int.MaxValue, maxLand = 0, maxGenerationMs = 0;
        int minShare = int.MaxValue, maxShare = 0;
        GeneratedWorld? firstWorld = null;
        var startBiomeCounts = new int[Enum.GetValues<Biome>().Length];
        var windowStartBiomeCounts = new int[Enum.GetValues<Biome>().Length];
        bool varietyWindowFailed = false;
        var all = Stopwatch.StartNew();
        int progressInterval = auditSeeds >= 10 ? 10 : 0;
        for (int offset = 0; offset < auditSeeds; offset++)
        {
            ulong currentSeed = checked(seed + (ulong)offset);
            var generationTimer = Stopwatch.StartNew();
            GeneratedWorld world;
            try { world = WorldGenerator.Generate(currentSeed); }
            catch (InvalidOperationException e)
            {
                generationTimer.Stop();
                int failedGenerationMs = checked((int)generationTimer.ElapsedMilliseconds);
                maxGenerationMs = Math.Max(maxGenerationMs, failedGenerationMs);
                failures++;
                string error = $"seed {currentSeed}: generation rejected: {e.Message}";
                Console.WriteLine("FAIL " + error);
                if (progressInterval > 0 && (offset + 1) % progressInterval == 0)
                    Console.WriteLine($"audited {offset + 1:N0}/{auditSeeds:N0} seeds; accepted={accepted} failures={failures}");
                continue;
            }
            if (offset == 0) firstWorld = world;
            generationTimer.Stop();
            int generationMs = checked((int)generationTimer.ElapsedMilliseconds);
            if (generationMs > maxGenerationMs) maxGenerationMs = generationMs;
            WorldAcceptanceResult result = WorldAcceptance.Validate(world);
            if (result.Passed) accepted++;
            else
            {
                failures++;
                foreach (string error in result.Failures)
                    Console.WriteLine($"FAIL seed {currentSeed}: {error}");
            }
            riverStarts += result.RiverStarts;
            coastalStarts += result.CoastalStarts;
            minLand = Math.Min(minLand, world.Report.LandShareBasisPoints);
            maxLand = Math.Max(maxLand, world.Report.LandShareBasisPoints);
            foreach (int share in world.Report.E7SharesBasisPoints.Span)
            {
                minShare = Math.Min(minShare, share);
                maxShare = Math.Max(maxShare, share);
            }
            foreach (WorldStart startAt in world.Starts)
            {
                int biome = (int)world.TileAt(startAt.Tile).Biome;
                startBiomeCounts[biome]++;
                windowStartBiomeCounts[biome]++;
            }
            if (auditSeeds >= 1_000 && (offset + 1) % 1_000 == 0)
            {
                Biome[] windowMissing = RequiredStartBiomes()
                    .Where(b => windowStartBiomeCounts[(int)b] == 0).ToArray();
                ulong firstSeed = checked(seed + (ulong)(offset + 1 - 1_000));
                ulong lastSeed = currentSeed;
                if (windowMissing.Length == 0)
                    Console.WriteLine($"biome variety passed for seeds {firstSeed:N0}–{lastSeed:N0}");
                else
                {
                    varietyWindowFailed = true;
                    Console.WriteLine($"FAIL biome variety for seeds {firstSeed:N0}–{lastSeed:N0}: " +
                        string.Join(", ", windowMissing));
                }
                Array.Clear(windowStartBiomeCounts);
            }
            if (progressInterval > 0 && (offset + 1) % progressInterval == 0)
                Console.WriteLine($"audited {offset + 1:N0}/{auditSeeds:N0} seeds; accepted={accepted} failures={failures}");
        }
        all.Stop();

        int totalStarts = auditSeeds * 4;
        Console.WriteLine($"seeds={auditSeeds} accepted={accepted} failures={failures}");
        Console.WriteLine($"landShareBp={minLand}..{maxLand} riverStarts={riverStarts}/{totalStarts} coastalStarts={coastalStarts}/{totalStarts}");
        Console.WriteLine($"E7ShareBp={minShare}..{maxShare} maxGenerateMs={maxGenerationMs} totalMs={all.ElapsedMilliseconds}");
        Console.WriteLine("startBiomes=" + string.Join(", ", Enum.GetValues<Biome>().Where(b => startBiomeCounts[(int)b] > 0)
            .Select(b => $"{b}:{startBiomeCounts[(int)b]}")));
        if (auditSeeds == 1 && accepted == 1)
        {
            GeneratedWorld world = firstWorld!;
            Console.WriteLine($"fairnessAttempts={world.Report.FairnessAttempts} blockedSeasons={world.Report.BlockedSeasons} contactSeason={world.Report.FirstContactSeason}");
            var startText = new string[world.Starts.Length];
            for (int i = 0; i < world.Starts.Length; i++)
            {
                WorldStart startAt = world.Starts[i];
                startText[i] = $"{startAt.Civilization}:{startAt.Tile % world.Width},{startAt.Tile / world.Width}";
            }
            Console.WriteLine("starts=" + string.Join(", ", startText));
            Console.WriteLine("E7SharesBp=" + string.Join(",", world.Report.E7SharesBasisPoints.ToArray()));
            string fingerprintLine = $"seed={seed} fingerprint={world.Fingerprint():x16}";
            Console.WriteLine(fingerprintLine);
            if (checkFingerprint is not null)
            {
                string expected = File.ReadAllText(checkFingerprint).Replace("\r\n", "\n").Trim();
                if (expected != fingerprintLine)
                {
                    Console.Error.WriteLine("WORLD GENERATION FINGERPRINT CHECK FAILED");
                    Console.Error.WriteLine($"expected: {expected}");
                    Console.Error.WriteLine($"actual:   {fingerprintLine}");
                    return 1;
                }
                Console.WriteLine("world generation fingerprint check passed");
            }
        }
        bool hasRequiredBiomes = RequiredStartBiomes().All(b => startBiomeCounts[(int)b] > 0);
        bool varied = auditSeeds < 1000 || (!varietyWindowFailed && hasRequiredBiomes);
        bool hasRivers = riverStarts * 100 >= totalStarts * 60;
        bool hasCoastal = coastalStarts * 100 >= totalStarts * 30;
        bool fast = maxGenerationMs < 5_000;
        if (auditSeeds >= 1000 && !hasRequiredBiomes)
            Console.WriteLine("FAIL missing start biomes in the aggregate: " +
                string.Join(", ", RequiredStartBiomes().Where(b => startBiomeCounts[(int)b] == 0)));
        if (auditSeeds >= 1000 && !hasRivers) Console.WriteLine("FAIL fewer than 60% of starts have nearby rivers");
        if (auditSeeds >= 1000 && !hasCoastal) Console.WriteLine("FAIL fewer than 30% of starts have nearby coast");
        if (!fast) Console.WriteLine("FAIL a world exceeded the 5-second generation budget");
        return failures == 0 && varied && fast && (auditSeeds < 1000 || (hasRivers && hasCoastal)) ? 0 : 1;
    }

    // Desert's forage is below the §9.1 90%-of-map-median start threshold, so it is not a required type.
    private static Biome[] RequiredStartBiomes() =>
    [
        Biome.TemperateForest, Biome.OpenWoodland, Biome.GrasslandSteppe,
        Biome.Savanna, Biome.TropicalForest, Biome.Wetland
    ];

    private static string Describe(ulong seed, long ticks, int interval, SimRunner runner)
    {
        ulong digest = runner.Digest();
        return string.Join('\n',
            $"seed={seed} ticks={ticks} checksumInterval={interval} commands={runner.AppliedCommands.Count}",
            $"final={runner.CurrentChecksum:x16}",
            $"digest={digest:x16}");
    }

    private static int VerifyReplay(string[] args)
    {
        string? packPath = null;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--pack") packPath = Next(args, ref i);
            else return Usage();
        }
        using FileStream stream = File.OpenRead(args[0]);
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        uint magic = reader.ReadUInt32();
        stream.Position = 0;
        if (magic == EconomyReplay.Magic)
        {
            if (packPath is null) throw new ArgumentException("Economy replay verification requires --pack.");
            EconomyReplay economy = EconomyReplay.Read(stream);
            ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
            ReplayVerification verification = economy.Verify(pack);
            Console.WriteLine(verification.Ok
                ? $"economy replay OK ({economy.Replay.Checksums.Count} checksums, {economy.Replay.Commands.Count} commands, final={economy.FinalChecksum:x16})"
                : verification.Message);
            return verification.Ok ? 0 : 1;
        }
        if (magic == MatchReplay.Magic)
        {
            if (packPath is null) throw new ArgumentException("Match replay verification requires --pack.");
            MatchReplay match = MatchReplay.Read(stream);
            ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
            ReplayVerification verification = match.Verify(pack);
            Console.WriteLine(verification.Ok
                ? $"match replay OK ({match.Replay.Checksums.Count} checksums, {match.Replay.Commands.Count} commands, final={match.FinalChecksum:x16})"
                : verification.Message);
            return verification.Ok ? 0 : 1;
        }
        if (packPath is not null) throw new ArgumentException("--pack is only used for economy and match replay files.");
        Replay replay = Replay.Read(stream);
        ReplayVerification result = ReplayVerifier.Verify(replay, seed => new ToyModel(seed));
        Console.WriteLine(result.Ok ? $"replay OK ({replay.Checksums.Count} checksums, {replay.Commands.Count} commands)" : result.Message);
        return result.Ok ? 0 : 1;
    }

    private static string Next(string[] args, ref int i)
    {
        if (i + 1 >= args.Length) throw new ArgumentException($"{args[i]} needs a value.");
        return args[++i];
    }
}
