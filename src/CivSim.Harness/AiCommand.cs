using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Ai.Forecast;
using CivSim.Sim.Content;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Harness;

/// <summary>
/// The AI harness (docs/MVP/14 §10): mirrored duels, free-for-alls, an Elo ladder and the M6 acceptance gate.
/// <code>
/// civsim-harness ai --pack FILE --duel warlord council [--seeds N] [--minutes N] [--archetype balanced] [--match-details] [--warlord-rival-pace-override X] [--warlord-firsts-eta-pessimism-seasons N] [--warlord-nonconquer-raid-posture-streak N] [--warlord-max-campaign-origin-diagnostic] [--warlord-frontier-early-bonus-override X] [--warlord-e3-fission-threshold-override X] [--warlord-defense-research-bonus-override X] [--warlord-keystone-tempo-override X] [--warlord-e2-closure-slot-diagnostic] [--warlord-defense-urgency-shadow-diagnostic] [--warlord-found-funnel] [--warlord-settlement-trace] [--b28-trace]
/// civsim-harness ai --pack FILE --ladder council,rusher,chieftain,warlord [--seeds N]
/// civsim-harness ai --pack FILE --ffa warlord [--seeds N] [--first-seed N]
/// civsim-harness ai --pack FILE --check-m6 [--seeds N]
/// civsim-harness ai --pack FILE --check-a7 [--seeds N] [--minutes N]
/// civsim-harness ai --pack FILE --check-m10 [--seeds N] [--minutes N]
/// civsim-harness ai --pack FILE --check-b27 [--seeds N] [--minutes N] [--first-seed N]
/// civsim-harness ai --pack FILE --check-b28 [--seeds N] [--minutes N] [--first-seed N]
/// civsim-harness ai --pack FILE (--check-b7 | --check-b8) [--seeds N] [--minutes N] [--first-seed N]
/// Bare --check-m10, or either omitted sample dimension, defaults to a quick diagnostic (1 seed, 2 minutes).
/// Full-sample classification requires explicitly supplying both dimensions at acceptance size (at least 8 seeds, 64 minutes).
/// civsim-harness ai --pack FILE --exploit [--generations N] [--children N] [--seeds N] [--screen-minutes N] [--minutes N]
/// Bare --exploit defaults to a quick diagnostic (1 generation, 1 child, 1 seed, 2-minute screen); acceptance
/// requires 10,000 distinct candidates and 64-minute validation of 16 finalists on fresh mirrored seeds.
/// </code>
/// A duel puts team A in seats 0 and 3 and team B in seats 1 and 2 (the world is point-symmetric), then swaps the teams,
/// and compares the teams' summed Legacy (Dominion + Mastery + Firsts + Wonders).
/// </summary>
internal static class AiCommand
{
    public static int Run(string[] args)
    {
        string? packPath = null, duelA = null, duelB = null, ladder = null, ffa = null;
        bool check = false, checkM10 = false, checkB27 = false, checkB28 = false, checkA7 = false, checkB7 = false, checkB8 = false, exploit = false, minutesGiven = false;
        bool matchDetails = false;
        Fix64? rivalPaceOverride = null;
        int? firstsEtaPessimismSeasons = null;
        int? warlordNonConquerRaidPostureStreak = null;
        bool warlordMaxCampaignOrigin = false;
        Fix64? warlordFrontierEarlyBonusOverride = null;
        Fix64? warlordE3FissionThresholdOverride = null;
        Fix64? warlordDefenseResearchBonusOverride = null;
        Fix64? warlordKeystoneTempoOverride = null;
        bool warlordDefenseUrgencyShadowDiagnostic = false;
        bool warlordE2ClosureSlotDiagnostic = false;
        bool warlordFoundFunnel = false;
        bool warlordSettlementTrace = false;
        bool warlordB30Trace = false, warlordB28Trace = false;
        bool generationsGiven = false, childrenGiven = false, seedsGiven = false, screenMinutesGiven = false;
        int seeds = 8, minutes = 33, screenMinutes = ExploitSearch.ScreenMinutesDefault, generations = 6, children = 12;
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
                case "--check-m10": checkM10 = true; break;
                case "--check-b27": checkB27 = true; break;
                case "--check-b28": checkB28 = true; break;
                case "--check-a7": checkA7 = true; break;
                case "--check-b7": checkB7 = true; break;
                case "--check-b8": checkB8 = true; break;
                case "--exploit": exploit = true; break;
                case "--match-details": matchDetails = true; break;
                case "--warlord-rival-pace-override": rivalPaceOverride = Fix64.Parse(args[++i]); matchDetails = true; break;
                case "--warlord-firsts-eta-pessimism-seasons": firstsEtaPessimismSeasons = int.Parse(args[++i], CultureInfo.InvariantCulture); matchDetails = true; break;
                case "--warlord-nonconquer-raid-posture-streak":
                    warlordNonConquerRaidPostureStreak = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    if (warlordNonConquerRaidPostureStreak < 1) throw new ArgumentOutOfRangeException(args[i], "Streak threshold must be at least one season.");
                    matchDetails = true;
                    break;
                case "--warlord-max-campaign-origin-diagnostic": warlordMaxCampaignOrigin = true; matchDetails = true; break;
                case "--warlord-frontier-early-bonus-override": warlordFrontierEarlyBonusOverride = Fix64.Parse(args[++i]); matchDetails = true; break;
                case "--warlord-e3-fission-threshold-override": warlordE3FissionThresholdOverride = Fix64.Parse(args[++i]); matchDetails = true; break;
                case "--warlord-defense-research-bonus-override": warlordDefenseResearchBonusOverride = Fix64.Parse(args[++i]); matchDetails = true; break;
                case "--warlord-keystone-tempo-override": warlordKeystoneTempoOverride = Fix64.Parse(args[++i]); matchDetails = true; break;
                case "--warlord-e2-closure-slot-diagnostic": warlordE2ClosureSlotDiagnostic = true; matchDetails = true; break;
                case "--warlord-defense-urgency-shadow-diagnostic": warlordDefenseUrgencyShadowDiagnostic = true; matchDetails = true; break;
                case "--warlord-found-funnel": warlordFoundFunnel = true; matchDetails = true; break;
                case "--warlord-settlement-trace": warlordSettlementTrace = true; break;
                case "--warlord-b30-trace": warlordB30Trace = true; matchDetails = true; break;
                case "--b28-trace": warlordB28Trace = true; break;
                case "--generations": generations = int.Parse(args[++i], CultureInfo.InvariantCulture); generationsGiven = true; break;
                case "--children": children = int.Parse(args[++i], CultureInfo.InvariantCulture); childrenGiven = true; break;
                case "--seeds": seeds = int.Parse(args[++i], CultureInfo.InvariantCulture); seedsGiven = true; break;
                case "--first-seed": firstSeed = ulong.Parse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture); break;
                case "--minutes": minutes = int.Parse(args[++i], CultureInfo.InvariantCulture); minutesGiven = true; break;
                case "--screen-minutes": screenMinutes = int.Parse(args[++i], CultureInfo.InvariantCulture); screenMinutesGiven = true; break;
                case "--archetype": archetype = Enum.Parse<EconomyArchetype>(args[++i], true); break;
                default: throw new ArgumentException($"Unknown option {args[i]}.");
            }
        }
        if (packPath is null) throw new ArgumentException("ai needs --pack FILE.");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        if (duelA is not null && duelB is not null) { PrintDuel(duelA, duelB, Tournament.Duel(pack, duelA, duelB, seeds, minutes, archetype, firstSeed, matchDetails, rivalPaceOverride, firstsEtaPessimismSeasons, warlordNonConquerRaidPostureStreak, warlordMaxCampaignOrigin, warlordFrontierEarlyBonusOverride, warlordE3FissionThresholdOverride, warlordDefenseResearchBonusOverride, warlordDefenseUrgencyShadowDiagnostic, warlordFoundFunnel, warlordKeystoneTempoOverride, warlordE2ClosureSlotDiagnostic, warlordSettlementTrace, warlordB30Trace, warlordB28Trace), matchDetails); return 0; }
        if (ladder is not null) return Ladder(pack, ladder.Split(','), seeds, minutes, archetype, firstSeed);
        if (ffa is not null) return FreeForAll(pack, ffa, seeds, minutes, firstSeed);
        if (check) return CheckM6(pack, seeds, minutes, archetype, firstSeed, out _);
        if (checkA7) return CheckA7Only(pack, seeds, minutesGiven ? minutes : 64, archetype, firstSeed);
        if (checkB7 || checkB8) return BronzeAiGates.Run(pack, checkB7, checkB8,
            seedsGiven ? seeds : 1, minutesGiven ? minutes : 2, firstSeed);
        if (checkB27)
        {
            (int b27Seeds, int b27Minutes) = ResolveB27Sample(seedsGiven ? seeds : null, minutesGiven ? minutes : null);
            return CheckB27(pack, b27Seeds, b27Minutes, archetype, firstSeed);
        }
        if (checkB28)
        {
            (int b28Seeds, int b28Minutes) = ResolveM10Sample(seedsGiven ? seeds : null, minutesGiven ? minutes : null);
            return CheckB28(pack, b28Seeds, b28Minutes, firstSeed, warlordB28Trace);
        }
        if (checkM10)
        {
            (int m10Seeds, int m10Minutes) = ResolveM10Sample(seedsGiven ? seeds : null, minutesGiven ? minutes : null);
            return CheckM10(pack, m10Seeds, m10Minutes, archetype, firstSeed);
        }
        if (exploit)
        {
            ExploitSearch.Options options = ExploitSearch.ResolveOptions(
                generationsGiven ? generations : null,
                childrenGiven ? children : null,
                seedsGiven ? seeds : null,
                minutesGiven ? minutes : null,
                screenMinutesGiven ? screenMinutes : null);
            ExploitSearch.SearchResult search = ExploitSearch.Search(pack,
                options.Generations, options.Children, options.Seeds, options.ScreenMinutes, options.Minutes, firstSeed);
            Console.WriteLine($"A6 screen: {search.UniqueCandidates:N0} distinct candidates ({search.ScreenEvaluations:N0} evaluations), {search.ScreenMinutes}-minute horizon.");
            Console.WriteLine($"Finalists: {search.Finalists.Count}; full validation: {search.Validations.Count}/{search.Finalists.Count} at {search.ValidationMinutes} minutes, {ExploitSearch.ValidationSeedPairs} fresh mirrored seed pairs each.");
            Console.WriteLine($"Deterministic seed ranges: screen {Tournament.BaseSeed + 5000}–{Tournament.BaseSeed + 5000 + (ulong)Math.Max(0, options.Seeds - 1)}; validation {Tournament.BaseSeed + 9000}–{Tournament.BaseSeed + 9000 + (ulong)(ExploitSearch.ValidationSeedPairs - 1)}.");
            foreach (ExploitSearch.Validation validation in search.Validations)
                Console.WriteLine($"  finalist {validation.WinRate:P0} vs Warlord: {validation.Policy}");
            double highestConfirmed = search.Validations.Count == 0 ? double.NaN : search.Validations.Max(v => v.WinRate);
            ExploitSearch.GateResult result = ExploitSearch.Classify(search.UniqueCandidates, search.ScreenMinutes,
                search.ValidationMinutes, search.Finalists.Count, search.Validations.Count, highestConfirmed);
            if (result == ExploitSearch.GateResult.ExploitFound)
            {
                Console.WriteLine("EXPLOIT FOUND (> 55%): a bug in the AI");
                return 1;
            }
            if (result == ExploitSearch.GateResult.Passed)
            {
                Console.WriteLine("EXPLOIT SEARCH PASS: no validated finalist exceeded 55%; applies to this 10,000-policy searched candidate set, not all possible policies.");
                return 0;
            }
            if (result == ExploitSearch.GateResult.Diagnostic)
                Console.WriteLine($"EXPLOIT SEARCH DIAGNOSTIC: requires {ExploitSearch.MinimumAcceptanceEvaluations:N0} distinct candidates (got {search.UniqueCandidates:N0}); no acceptance conclusion.");
            else
                Console.WriteLine($"EXPLOIT SEARCH INCOMPLETE: requires {ExploitSearch.FinalistCount} finalists fully validated at {ExploitSearch.MinimumM10Minutes} minutes (got {search.Validations.Count}/{search.Finalists.Count} at {options.Minutes}); no acceptance conclusion.");
            return 2;
        }
        throw new ArgumentException("ai needs --duel A B, --ladder a,b,c, --ffa LEVEL, --check-m6, --check-a7, --check-b7, --check-b8, --check-b27, --check-b28, --check-m10 or --exploit.");
    }

    internal static (int Seeds, int Minutes) ResolveM10Sample(int? requestedSeeds, int? requestedMinutes) =>
        (requestedSeeds ?? 1, requestedMinutes ?? 2);

    internal static (int Seeds, int Minutes) ResolveB27Sample(int? requestedSeeds, int? requestedMinutes) =>
        (requestedSeeds ?? 1, requestedMinutes ?? M10AiAcceptanceStatus.MinimumMinutes);

    private static void PrintDuel(string a, string b, Tournament.DuelResult r, bool matchDetails = false)
    {
        foreach (MatchOutcome outcome in r.Outcomes)
        {
            foreach (string line in outcome.SettlementTimelineTraces) Console.WriteLine(line);
            foreach (string line in outcome.WarlordMandateTraces) Console.WriteLine(line);
        }
        MatchOutcome[] b30TraceMatches = r.Outcomes.Where(outcome => outcome.WarlordB30TraceEnabled).ToArray();
        if (b30TraceMatches.Length > 0)
        {
            int overflowEpisodes = b30TraceMatches.Sum(outcome => Enumerable.Range(0, outcome.Civs.Length)
                .Where(civ => string.Equals(outcome.Seats[civ].Name, "warlord", StringComparison.OrdinalIgnoreCase))
                .Sum(civ => outcome.Civs[civ].MandateOverflowBlunders));
            int atCapCandidateSeasons = b30TraceMatches.Sum(outcome => outcome.WarlordMandateTraces.Count);
            Console.WriteLine($"  B30 focused: unused positive-paid-decree overflow {overflowEpisodes}/{b30TraceMatches.Length} matches = {(double)overflowEpisodes / b30TraceMatches.Length:F3} per match; traced at-cap candidate seasons {atCapCandidateSeasons}.");
        }
        Console.WriteLine($"{a} vs {b}: {r.Matches} matches, {a} wins {r.WinRateA * 100:F0}% (Legacy {r.LegacyA / r.Matches:F1} vs {r.LegacyB / r.Matches:F1})");
        Console.WriteLine($"  settlements lost per match: {a} {r.SettlementsLostA / r.Matches:F2} (in {r.MatchesWithLossA} matches), {b} {r.SettlementsLostB / r.Matches:F2} (in {r.MatchesWithLossB} matches)");
        Console.WriteLine($"  raids launched per match: {a} {(double)r.RaidsA / r.Matches:F1}, {b} {(double)r.RaidsB / r.Matches:F1}");
        if (r.Outcomes.Count > 0 && r.Outcomes[0].WarlordFrontierEarlyBonusOverride is { } frontierBonus)
            Console.WriteLine($"  diagnostic Warlord FrontierRace early bonus override: {frontierBonus.ToDoubleForDisplay():F2} (production default {StrategistTuning.FrontierEarlyBonus.ToDoubleForDisplay():F2})");
        if (r.Outcomes.Count > 0 && r.Outcomes[0].WarlordE3FissionThresholdOverride is { } fissionThreshold)
            Console.WriteLine($"  diagnostic Warlord E3 Expand fission threshold override: {fissionThreshold.ToDoubleForDisplay():F2} (production default 0.83)");
        if (r.Outcomes.Count > 0 && r.Outcomes[0].WarlordDefenseResearchBonusOverride is { } defenseBonus)
            Console.WriteLine($"  diagnostic Warlord defense research bonus override: {defenseBonus.ToDoubleForDisplay():F2} (production default {SparkTuning.DefenseBonus.ToDoubleForDisplay():F2})");
        if (r.Outcomes.Count > 0 && r.Outcomes[0].WarlordDefenseUrgencyShadowDiagnostic)
        {
            DuelTotals shadow = SumTeam(r.Outcomes, string.Equals(a, "warlord", StringComparison.OrdinalIgnoreCase) ? a : b);
            Console.WriteLine($"  diagnostic Warlord defense urgency shadow: high-urgency decisions {shadow.DefenseUrgencyShadowHighDecisions}, slots changed {shadow.DefenseUrgencyShadowSlotsChanged}, DEF/WAR displaced by other projects {shadow.DefenseUrgencyShadowDisplacedDefense}; by era {FormatEraCounts(shadow.DefenseUrgencyShadowHighDecisionsByEra)}");
        }
        Console.WriteLine($"  owned land share per match: {a} {r.OwnedLandShareA / (r.Matches * 100.0):F1}%, {b} {r.OwnedLandShareB / (r.Matches * 100.0):F1}%");
        Console.WriteLine($"  below-six campaign rejections with alternate origin reaching six / still short: {a} {r.AlternateOriginCampaignRescuesA}/{r.AlternateOriginCampaignNoRescuesA}, {b} {r.AlternateOriginCampaignRescuesB}/{r.AlternateOriginCampaignNoRescuesB}");
        Console.WriteLine($"  KEY-1 mean minute: {a} {r.MeanKey1A:F1}, {b} {r.MeanKey1B:F1}");
        Console.WriteLine($"  blunders ({a}): keystone stalls {r.KeystoneStallsA}, idle slot seasons {r.IdleSlotSeasonsA}, dithering matches {r.PlanDitherMatchesA}");
        if (!matchDetails) return;

        double firstsA = 0, firstsB = 0;
        foreach (MatchOutcome match in r.Outcomes)
        {
            firstsA += SumTeam(match, a).Firsts;
            firstsB += SumTeam(match, b).Firsts;
        }
        Console.WriteLine($"  mean Firsts Legacy per match: {a} {firstsA / r.Matches:F1}, {b} {firstsB / r.Matches:F1}");

        Console.WriteLine("  match details: summed team Legacy components, settlement changes, conflict outcomes, and KEY-1/2 timing");
        foreach (MatchOutcome match in r.Outcomes)
        {
            DuelTotals teamA = SumTeam(match, a), teamB = SumTeam(match, b);
            string result = teamA.Legacy > teamB.Legacy ? a + " win" : teamA.Legacy < teamB.Legacy ? b + " win" : "tie";
            string streakLabel = match.WarlordNonConquerRaidPostureStreakOverride is { } streak
                ? $" [Warlord non-Conquer Raid streak={streak}]" : string.Empty;
            if (match.WarlordMaxCampaignOriginOverride) streakLabel += " [Warlord max-force campaign origin]";
            if (match.WarlordFrontierEarlyBonusOverride is { } bonus)
                streakLabel += $" [Warlord FrontierEarlyBonus={bonus.ToDoubleForDisplay():F2}]";
            if (match.WarlordE3FissionThresholdOverride is { } matchFissionThreshold)
                streakLabel += $" [Warlord E3 Expand fission threshold={matchFissionThreshold.ToDoubleForDisplay():F2}]";
            if (match.WarlordDefenseResearchBonusOverride is { } matchDefenseBonus)
                streakLabel += $" [Warlord defense research bonus={matchDefenseBonus.ToDoubleForDisplay():F2}]";
            if (match.WarlordKeystoneTempoOverride is { } matchKeystoneTempo)
                streakLabel += $" [Warlord keystone tempo={matchKeystoneTempo.ToDoubleForDisplay():F1}]";
            if (match.WarlordE2ClosureSlotDiagnostic)
                streakLabel += " [Warlord E2 closure slot diagnostic]";
            foreach (FoundFunnelDiagnostic funnel in match.FoundFunnels)
                Console.WriteLine($"      Warlord Found funnel civ {funnel.Civ}: observed seasons {funnel.SeasonsObserved}; CouncilOn {funnel.CouncilOnSeasons}, MandateUnlocked {funnel.MandateUnlockedSeasons}, both {funnel.CouncilAndMandateSeasons}; Found affordable {funnel.FoundAffordableSeasons}, at cap {funnel.AtMandateCapSeasons}, quota open {funnel.QuotaOpenSeasons}, natural headroom open {funnel.NaturalGateOpenSeasons}; active seasons without Found candidate {funnel.ActiveButNoFoundCandidateSeasons}; mean mandate/cap/Found cost {funnel.MeanMandate}/{funnel.MeanMandateCap}/{funnel.MeanFoundCost}; decree candidate seasons {funnel.CandidateSeasons}; Found candidate seasons {funnel.FoundCandidateSeasons} (positive EV {funnel.PositiveFoundCandidateSeasons}); Found chosen {funnel.FoundSelectedSeasons}; positive Found passed for another decree {funnel.PositiveFoundPassedSeasons}; final KEY-3 blockers [{funnel.Key3MissingPrerequisites}], prerequisite path [{funnel.Key3PrerequisitePath}], E2 breadth {funnel.E2BreadthDone}/20, KEY-3 {funnel.Key3State} progress {funnel.Key3Progress.ToDoubleForDisplay():F1}");
            foreach (WarlordResearchPlanDiagnostic trace in match.WarlordResearchTraces)
            {
                Console.WriteLine($"      Warlord research trace civ {trace.Civ}: {trace.Summary}");
                Console.WriteLine($"        checkpoints: {trace.Timeline}");
            }
            foreach (string trace in match.WarlordEconomyTraces)
                Console.WriteLine($"      {trace}");
            if (match.WarlordDefenseUrgencyShadowDiagnostic)
                streakLabel += " [Warlord high-urgency shadow plan enabled]";
            Console.WriteLine($"    seed {match.Seed}: {result}{streakLabel}; {a} L={teamA.Legacy:F0} (D/M/F/W {teamA.Dominion:F0}/{teamA.Mastery:F0}/{teamA.Firsts:F0}/{teamA.Wonders:F0}), settlements/founds/projects {teamA.Settlements}/{teamA.FoundDecrees}/{teamA.Projects}, splits/pending-seasons {teamA.FissionSplits}/{teamA.FissionPendingSeasons}, owned land {teamA.OwnedLandShareBasisPoints / 100.0:F1}%, posture B/E/P/F/R {FormatPostures(teamA.PostureSeasons)}, FrontierRace/Conquer plan seasons {teamA.FrontierRacePlanSeasons}/{teamA.ConquerPlanSeasons}, streak-posture changes/3-season opportunities {teamA.RaidStreakPostureTransitions}/{teamA.ThreeSeasonNonConquerRaidPostureOpportunities}, alternate-origin rescues/short {teamA.AlternateOriginCampaignRescues}/{teamA.AlternateOriginCampaignNoRescues}, GOV02-locked/frontier-open {teamA.PostureGateLockedSeasons}/{teamA.FrontierOpenSeasons}, lost/captured {teamA.SettlementsLost}/{teamA.SettlementsCaptured}, raids {teamA.RaidsWon}/{teamA.RaidsLaunched}, wars declared/rejected {teamA.WarsDeclared}/{teamA.WarRejections}, assaults launch/resolve/hold {teamA.AssaultsLaunched}/{teamA.AssaultsResolved}/{teamA.AssaultsHeld}, predicted capture Σp {teamA.PredictedCaptureSuccess:F2}/{teamA.AssaultPredictions}, K1/K2 {teamA.Key1Mean:F1}/{teamA.Key2Mean:F1}; " +
                $"{b} L={teamB.Legacy:F0} (D/M/F/W {teamB.Dominion:F0}/{teamB.Mastery:F0}/{teamB.Firsts:F0}/{teamB.Wonders:F0}), settlements/founds/projects {teamB.Settlements}/{teamB.FoundDecrees}/{teamB.Projects}, splits/pending-seasons {teamB.FissionSplits}/{teamB.FissionPendingSeasons}, owned land {teamB.OwnedLandShareBasisPoints / 100.0:F1}%, posture B/E/P/F/R {FormatPostures(teamB.PostureSeasons)}, FrontierRace/Conquer plan seasons {teamB.FrontierRacePlanSeasons}/{teamB.ConquerPlanSeasons}, streak-posture changes/3-season opportunities {teamB.RaidStreakPostureTransitions}/{teamB.ThreeSeasonNonConquerRaidPostureOpportunities}, alternate-origin rescues/short {teamB.AlternateOriginCampaignRescues}/{teamB.AlternateOriginCampaignNoRescues}, GOV02-locked/frontier-open {teamB.PostureGateLockedSeasons}/{teamB.FrontierOpenSeasons}, lost/captured {teamB.SettlementsLost}/{teamB.SettlementsCaptured}, raids {teamB.RaidsWon}/{teamB.RaidsLaunched}, wars declared/rejected {teamB.WarsDeclared}/{teamB.WarRejections}, assaults launch/resolve/hold {teamB.AssaultsLaunched}/{teamB.AssaultsResolved}/{teamB.AssaultsHeld}, predicted capture Σp {teamB.PredictedCaptureSuccess:F2}/{teamB.AssaultPredictions}, K1/K2 {teamB.Key1Mean:F1}/{teamB.Key2Mean:F1}");
            Console.WriteLine($"      Spark forecast rollouts/decisions: {a} {teamA.SparkRollouts}/{teamA.SparkDecisions} ({PerDecision(teamA.SparkRollouts, teamA.SparkDecisions):F1} per decision), " +
                $"{b} {teamB.SparkRollouts}/{teamB.SparkDecisions} ({PerDecision(teamB.SparkRollouts, teamB.SparkDecisions):F1} per decision)");
            if (match.WarlordDefenseUrgencyShadowDiagnostic)
            {
                DuelTotals shadow = string.Equals(a, "warlord", StringComparison.OrdinalIgnoreCase) ? teamA : teamB;
                Console.WriteLine($"      Warlord defense urgency shadow: high-urgency decisions {shadow.DefenseUrgencyShadowHighDecisions}, slots changed {shadow.DefenseUrgencyShadowSlotsChanged}, DEF/WAR displaced by other projects {shadow.DefenseUrgencyShadowDisplacedDefense}; by era {FormatEraCounts(shadow.DefenseUrgencyShadowHighDecisionsByEra)}");
            }
            Console.WriteLine($"      rejected wars ({a}): {FormatRejections(teamA.WarRejectionReasons)}");
            Console.WriteLine($"      rejected wars ({b}): {FormatRejections(teamB.WarRejectionReasons)}");
            Console.WriteLine($"      threat estimates ({a}): {FormatThreats(teamA.ThreatReasons)}");
            Console.WriteLine($"      threat estimates ({b}): {FormatThreats(teamB.ThreatReasons)}");
            if (string.Equals(a, "warlord", StringComparison.OrdinalIgnoreCase))
                Console.WriteLine($"      incoming Rusher settlement raids on {a}: {FormatIncomingRaids(teamA)}");
            if (string.Equals(b, "warlord", StringComparison.OrdinalIgnoreCase))
                Console.WriteLine($"      incoming Rusher settlement raids on {b}: {FormatIncomingRaids(teamB)}");
            if (teamA.WarlordUrgencySeasons > 0)
                Console.WriteLine($"      Warlord defense urgency {a}: {FormatUrgencyBands(teamA.WarlordUrgencyBands)}");
            if (teamB.WarlordUrgencySeasons > 0)
                Console.WriteLine($"      Warlord defense urgency {b}: {FormatUrgencyBands(teamB.WarlordUrgencyBands)}");
            PrintFirstRaceDiagnostics(match);
        }
    }

    private static void PrintFirstRaceDiagnostics(MatchOutcome match)
    {
        if (match.FirstRaceClaims.Count == 0) return;
        string paceOverride = match.RivalPaceOverride is { } pace
            ? $" (diagnostic Warlord pace override {pace.ToDoubleForDisplay():F2}×)" : string.Empty;
        string etaOverride = match.FirstsEtaPessimismOverride is { } seasons
            ? $" (diagnostic Warlord Firsts ETA pessimism {seasons} seasons)" : string.Empty;
        string raidStreakOverride = match.WarlordNonConquerRaidPostureStreakOverride is { } streak
            ? $" (diagnostic Warlord non-Conquer Raid posture streak {streak} seasons)" : string.Empty;
        foreach (int lookback in new[] { 4, 8, 16 })
        {
            var samples = new List<(int Era, string Lineage, int Error, double Pace, int EtaSeasons, int ElapsedSeasons)>();
            int outsideHorizon = 0, withoutLookback = 0;
            foreach (FirstRaceClaimDiagnostic claim in match.FirstRaceClaims)
            {
                int actualSeason = (int)(claim.Tick / ResearchSystem.SeasonTicks);
                foreach (IGrouping<int, FirstRaceEstimate> warlordHistory in claim.WarlordEstimates
                             .Where(estimate => estimate.ForecastSeason >= 0).GroupBy(estimate => estimate.WarlordCiv))
                {
                    FirstRaceEstimate[] eligible = warlordHistory
                        .Where(estimate => actualSeason - estimate.ForecastSeason >= lookback)
                        .OrderBy(estimate => actualSeason - estimate.ForecastSeason).ToArray();
                    if (eligible.Length == 0) { withoutLookback++; continue; }
                    FirstRaceEstimate estimate = eligible[0];
                    int etaSteps = estimate.RivalEtaSteps[claim.ProjectIndex];
                    if (etaSteps == int.MaxValue) { outsideHorizon++; continue; }
                    int etaSeasons = (etaSteps + ForecastTuning.PlannerStepsPerSeason - 1) / ForecastTuning.PlannerStepsPerSeason;
                    int elapsedSeasons = actualSeason - estimate.ForecastSeason;
                    int error = actualSeason - (estimate.ForecastSeason + etaSeasons);
                    samples.Add((claim.Era, claim.Lineage, error, estimate.RivalPace.ToDoubleForDisplay(), etaSeasons, elapsedSeasons));
                }
            }
            int beatEta = samples.Count(sample => sample.Error < 0);
            int[] errors = samples.Select(sample => sample.Error).OrderBy(error => error).ToArray();
            string medianError = errors.Length == 0 ? "n/a" : FormatSigned(errors[errors.Length / 2]);
            double[] requiredPace = samples.Where(sample => sample.Error < 0 && sample.ElapsedSeasons > 0 && sample.EtaSeasons > 0)
                .Select(sample => sample.Pace * sample.EtaSeasons / sample.ElapsedSeasons).OrderBy(value => value).ToArray();
            string paceSummary = requiredPace.Length == 0
                ? "pace proxy n/a"
                : $"rough linear pace needed {requiredPace[requiredPace.Length / 2]:F2}× " +
                  $"({requiredPace.Count(value => value > 2.0)}/{requiredPace.Length} above 2.00× cap)";
            Console.WriteLine($"      Rusher First ETA at {lookback}-season lookback (global rival frontier): " +
                $"before ETA {beatEta}/{samples.Count}, median actual-predicted {medianError} seasons; {paceSummary}; " +
                  $"{outsideHorizon} outside forecast horizon, {withoutLookback} without that much history{paceOverride}{etaOverride}{raidStreakOverride}.");
            if (lookback != 4) continue;
            foreach (var group in samples.GroupBy(sample => (sample.Era, sample.Lineage))
                         .Where(group => group.Count() >= 4).OrderByDescending(group => group.Count()).Take(10))
            {
                int[] groupErrors = group.Select(sample => sample.Error).OrderBy(error => error).ToArray();
                int groupBeatEta = groupErrors.Count(error => error < 0);
                double[] groupPaces = group.Where(sample => sample.Error < 0 && sample.ElapsedSeasons > 0 && sample.EtaSeasons > 0)
                    .Select(sample => sample.Pace * sample.EtaSeasons / sample.ElapsedSeasons).OrderBy(value => value).ToArray();
                string groupPace = groupPaces.Length == 0 ? "pace proxy n/a" :
                    $"rough pace {groupPaces[groupPaces.Length / 2]:F2}× ({groupPaces.Count(value => value > 2.0)}/{groupPaces.Length} above cap)";
                Console.WriteLine($"        E{group.Key.Era}/{group.Key.Lineage}: before ETA {groupBeatEta}/{groupErrors.Length}, " +
                    $"median actual-predicted {FormatSigned(groupErrors[groupErrors.Length / 2])}, {groupPace}");
            }
        }
        foreach (FirstRaceClaimDiagnostic claim in match.FirstRaceClaims.Take(8))
        {
            int actualSeason = (int)(claim.Tick / ResearchSystem.SeasonTicks);
            string estimates = string.Join("; ", claim.WarlordEstimates
                .Where(estimate => estimate.ForecastSeason >= 0 && actualSeason - estimate.ForecastSeason >= 4)
                .GroupBy(estimate => estimate.WarlordCiv)
                .Select(group => group.OrderBy(estimate => actualSeason - estimate.ForecastSeason).First())
                .Select(estimate => FormatFirstRaceEstimate(claim, estimate, actualSeason)));
            if (estimates.Length == 0) estimates = "no 4-season prior forecast";
            Console.WriteLine($"        E{claim.Era}/{claim.Lineage} {claim.Project} at season {actualSeason}: {estimates}");
        }
        if (match.FirstRaceClaims.Count > 8) Console.WriteLine($"        ... {match.FirstRaceClaims.Count - 8} more claims summarized above");
    }

    private static string FormatSigned(int value) => value > 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);

    private static string FormatFirstRaceEstimate(FirstRaceClaimDiagnostic claim, FirstRaceEstimate estimate, int actualSeason)
    {
        if (estimate.ForecastSeason < 0) return $"Warlord civ {estimate.WarlordCiv}: no prior forecast";
        int etaSteps = estimate.RivalEtaSteps[claim.ProjectIndex];
        if (etaSteps == int.MaxValue) return $"Warlord civ {estimate.WarlordCiv}: project beyond rival-forecast horizon from season {estimate.ForecastSeason}";
        int etaSeasons = (etaSteps + ForecastTuning.PlannerStepsPerSeason - 1) / ForecastTuning.PlannerStepsPerSeason;
        int predictedSeason = estimate.ForecastSeason + etaSeasons;
        int delta = actualSeason - predictedSeason;
        return $"Warlord civ {estimate.WarlordCiv} from season {estimate.ForecastSeason} pred {predictedSeason} " +
            $"(actual-predicted {FormatSigned(delta)}, pace {estimate.RivalPace.ToDoubleForDisplay():F2}×)";
    }

    private static string FormatPostures(int[] seasons)
        => string.Join("/", seasons);

    private static double PerDecision(long rollouts, int decisions)
        => decisions == 0 ? 0 : (double)rollouts / decisions;

    private static string FormatUrgencyBands(int[] bands)
        => $"<.45/.45-.50/.50-.60/.60-.70/>=.70 {string.Join('/', bands)}";

    private static string FormatEraCounts(int[] counts)
        => string.Join('/', counts.Select((count, era) => $"E{era}:{count}"));

    private static string FormatThreats(Dictionary<string, int> threats)
        => threats.Count == 0 ? "none" :
            string.Join(" | ", threats.OrderByDescending(x => x.Value).Take(4).Select(x => $"{x.Value}× {x.Key}"));

    private static string FormatIncomingRaids(DuelTotals team)
    {
        double successRate = team.IncomingRusherRaidsResolved == 0 ? double.NaN :
            (double)team.IncomingRusherRaidsSucceeded / team.IncomingRusherRaidsResolved;
        double meanParty = team.IncomingRusherRaidsLaunched == 0 ? double.NaN :
            (double)team.IncomingRusherRaidPartyFighters / team.IncomingRusherRaidsLaunched;
        return $"launched/resolved/pending {team.IncomingRusherRaidsLaunched}/{team.IncomingRusherRaidsResolved}/" +
            $"{team.IncomingRusherRaidsLaunched - team.IncomingRusherRaidsResolved}, succeeded {team.IncomingRusherRaidsSucceeded} " +
            $"({successRate:P0} of resolved), mean party {meanParty:F1}";
    }

    private static string FormatRejections(Dictionary<string, int>? reasons)
    {
        if (reasons is null || reasons.Count == 0) return "none";
        int radiusRescues = reasons.Where(x => x.Key.Contains("would clear holder floor", StringComparison.Ordinal)).Sum(x => x.Value);
        string top = string.Join(" | ", reasons.OrderByDescending(x => x.Value).Take(2).Select(x => $"{x.Value}× {x.Key}"));
        if (radiusRescues > 0) top = $"+4 radius could clear the holder floor in {radiusRescues} rejections | {top}";
        return top;
    }

    private static DuelTotals SumTeam(MatchOutcome match, string name)
    {
        var total = new DuelTotals { PostureSeasons = new int[5], WarlordUrgencyBands = new int[5],
            DefenseUrgencyShadowHighDecisionsByEra = new int[ResearchCatalog.EraCount], ThreatReasons = new Dictionary<string, int>(StringComparer.Ordinal) };
        for (int seat = 0; seat < match.Seats.Length; seat++)
        {
            if (!string.Equals(match.Seats[seat].Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            CivOutcome civ = match.Civs[seat];
            total.Dominion += civ.Dominion;
            total.Mastery += civ.Mastery;
            total.Firsts += civ.Firsts;
            total.Wonders += civ.Wonders;
            total.Settlements += civ.Settlements;
            total.Projects += civ.Projects;
            total.SparkRollouts += civ.SparkRollouts;
            total.SparkDecisions += civ.SparkDecisions;
            total.FoundDecrees += civ.FoundDecrees;
            total.FissionSplits += civ.FissionSplits;
            total.FissionPendingSeasons += civ.FissionPendingSeasons;
            total.OwnedLandShareBasisPoints += civ.OwnedLandShareBasisPoints;
            total.ConquerPlanSeasons += civ.ConquerPlanSeasons;
            total.FrontierRacePlanSeasons += civ.FrontierRacePlanSeasons;
            total.RaidStreakPostureTransitions += civ.RaidStreakPostureTransitions;
            total.ThreeSeasonNonConquerRaidPostureOpportunities += civ.ThreeSeasonNonConquerRaidPostureOpportunities;
            total.AlternateOriginCampaignRescues += civ.AlternateOriginCampaignRescues;
            total.AlternateOriginCampaignNoRescues += civ.AlternateOriginCampaignNoRescues;
            total.PostureGateLockedSeasons += civ.PostureGateLockedSeasons;
            total.FrontierOpenSeasons += civ.FrontierOpenSeasons;
            for (int posture = 0; posture < total.PostureSeasons.Length; posture++)
                total.PostureSeasons[posture] += civ.PostureSeasons[posture];
            for (int band = 0; band < total.WarlordUrgencyBands.Length; band++)
                total.WarlordUrgencyBands[band] += civ.WarlordUrgencyBands[band];
            total.WarlordUrgencySeasons += civ.WarlordUrgencySeasons;
            total.DefenseUrgencyShadowHighDecisions += civ.DefenseUrgencyShadowHighDecisions;
            total.DefenseUrgencyShadowSlotsChanged += civ.DefenseUrgencyShadowSlotsChanged;
            total.DefenseUrgencyShadowDisplacedDefense += civ.DefenseUrgencyShadowDisplacedDefense;
            for (int era = 0; era < total.DefenseUrgencyShadowHighDecisionsByEra.Length; era++)
                total.DefenseUrgencyShadowHighDecisionsByEra[era] += civ.DefenseUrgencyShadowHighDecisionsByEra[era];
            total.SettlementsLost += civ.SettlementsLost;
            total.SettlementsCaptured += civ.SettlementsCaptured;
            total.IncomingRusherRaidsLaunched += civ.IncomingRusherRaidsLaunched;
            total.IncomingRusherRaidsResolved += civ.IncomingRusherRaidsResolved;
            total.IncomingRusherRaidsSucceeded += civ.IncomingRusherRaidsSucceeded;
            total.IncomingRusherRaidPartyFighters += civ.IncomingRusherRaidPartyFighters;
            total.RaidsLaunched += civ.RaidsLaunched;
            total.RaidsWon += civ.RaidsWon;
            total.WarsDeclared += civ.WarsDeclared;
            total.WarRejections += civ.WarRejections;
            if (civ.WarRejectionReasons.Count > 0)
            {
                total.WarRejectionReasons ??= new Dictionary<string, int>(StringComparer.Ordinal);
                foreach ((string reason, int count) in civ.WarRejectionReasons)
                {
                    total.WarRejectionReasons.TryGetValue(reason, out int prior);
                    total.WarRejectionReasons[reason] = prior + count;
                }
            }
            foreach ((string threat, int count) in civ.ThreatReasons)
            {
                total.ThreatReasons.TryGetValue(threat, out int prior);
                total.ThreatReasons[threat] = prior + count;
            }
            total.AssaultsLaunched += civ.AssaultsLaunched;
            total.AssaultsResolved += civ.AssaultsResolved;
            total.AssaultsHeld += civ.AssaultsHeld;
            total.AssaultPredictions += civ.AssaultPredictions;
            total.PredictedCaptureSuccess += civ.PredictedCaptureSuccess;
            if (!double.IsNaN(civ.KeyMinutes[0])) { total.Key1Total += civ.KeyMinutes[0]; total.Key1Count++; }
            if (!double.IsNaN(civ.KeyMinutes[1])) { total.Key2Total += civ.KeyMinutes[1]; total.Key2Count++; }
        }
        return total;
    }

    private static DuelTotals SumTeam(IReadOnlyList<MatchOutcome> matches, string name)
    {
        var total = new DuelTotals { DefenseUrgencyShadowHighDecisionsByEra = new int[ResearchCatalog.EraCount] };
        foreach (MatchOutcome match in matches)
        {
            DuelTotals one = SumTeam(match, name);
            total.DefenseUrgencyShadowHighDecisions += one.DefenseUrgencyShadowHighDecisions;
            total.DefenseUrgencyShadowSlotsChanged += one.DefenseUrgencyShadowSlotsChanged;
            total.DefenseUrgencyShadowDisplacedDefense += one.DefenseUrgencyShadowDisplacedDefense;
            total.SparkRollouts += one.SparkRollouts;
            total.SparkDecisions += one.SparkDecisions;
            for (int era = 0; era < total.DefenseUrgencyShadowHighDecisionsByEra.Length; era++)
                total.DefenseUrgencyShadowHighDecisionsByEra[era] += one.DefenseUrgencyShadowHighDecisionsByEra[era];
        }
        return total;
    }

    private struct DuelTotals
    {
        public double Dominion, Mastery, Firsts, Wonders;
        public int Settlements, FoundDecrees, Projects;
        public long SparkRollouts;
        public int SparkDecisions;
        public int FissionSplits, FissionPendingSeasons;
        public int OwnedLandShareBasisPoints;
        public int ConquerPlanSeasons, FrontierRacePlanSeasons, RaidStreakPostureTransitions;
        public int ThreeSeasonNonConquerRaidPostureOpportunities;
        public int AlternateOriginCampaignRescues, AlternateOriginCampaignNoRescues;
        public int PostureGateLockedSeasons, FrontierOpenSeasons;
        public int[] PostureSeasons;
        public int[] WarlordUrgencyBands;
        public int WarlordUrgencySeasons;
        public int DefenseUrgencyShadowHighDecisions, DefenseUrgencyShadowSlotsChanged, DefenseUrgencyShadowDisplacedDefense;
        public int[] DefenseUrgencyShadowHighDecisionsByEra;
        public int SettlementsLost, SettlementsCaptured, RaidsLaunched, RaidsWon;
        public int WarsDeclared, WarRejections;
        public Dictionary<string, int>? WarRejectionReasons;
        public Dictionary<string, int> ThreatReasons;
        public int AssaultsLaunched, AssaultsResolved, AssaultsHeld;
        public int AssaultPredictions;
        public double PredictedCaptureSuccess;
        public int IncomingRusherRaidsLaunched, IncomingRusherRaidsResolved, IncomingRusherRaidsSucceeded;
        public long IncomingRusherRaidPartyFighters;
        public double Key1Total, Key2Total;
        public int Key1Count, Key2Count;
        public readonly double Legacy => Dominion + Mastery + Firsts + Wonders;
        public readonly double Key1Mean => Key1Count == 0 ? double.NaN : Key1Total / Key1Count;
        public readonly double Key2Mean => Key2Count == 0 ? double.NaN : Key2Total / Key2Count;
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

    private static int CheckA7Only(ContentPack pack, int seeds, int minutes, EconomyArchetype archetype, ulong firstSeed)
    {
        Console.WriteLine($"== A7-only: Warlord vs Council ({seeds} seeds, {minutes} minutes)");
        Tournament.DuelResult duel = Tournament.Duel(pack, "warlord", "council", seeds, minutes, archetype, firstSeed);
        bool pass = ReportA7(duel);
        Console.WriteLine(pass ? "A7 CHECK PASSED" : "A7 CHECK FAILED");
        return pass ? 0 : 1;
    }

    private static bool ReportA7(Tournament.DuelResult a1, bool diagnostic = false)
    {
        bool pass = true;
        bool Gate(string name, bool ok, string detail)
        {
            Console.WriteLine($"  {name}: {detail} {(diagnostic ? "DIAGNOSTIC" : ok ? "PASS" : "FAIL")}");
            pass &= ok;
            return ok;
        }

        Console.WriteLine("== A7: blunder detectors (Warlord vs Council)");
        Gate("A7 keystone stalls", a1.KeystoneStallsA == 0, $"{a1.KeystoneStallsA}");
        Gate("A7 dithering", a1.PlanDitherMatchesA == 0, $"{a1.PlanDitherMatchesA} matches");
        int a7Seats = 0, starvation = 0, undefended = 0, herdCrashes = 0, idleLabor = 0;
        foreach (MatchOutcome match in a1.Outcomes)
        {
            a7Seats += match.A7AiSeatMatches;
            starvation += match.AvoidableStarvationDeaths;
            undefended += match.UndefendedLosses;
            herdCrashes += match.AttributedHerdCrashes;
            idleLabor += match.IdleLaborSeasons;
        }
        int a7Matches = a1.Outcomes.Count;
        double starvationRate = (double)starvation / Math.Max(1, a7Matches);
        double undefendedRate = (double)undefended / Math.Max(1, a7Matches);
        double herdCrashRate = (double)herdCrashes / Math.Max(1, a7Matches);
        double idleLaborRate = (double)idleLabor / Math.Max(1, a7Matches);
        Gate("A7 avoidable starvation", starvationRate == 0,
            $"{starvation} deaths; {starvationRate:F3} per match ({a7Matches}; {a7Seats} AI seats)");
        Gate("A7 undefended loss", undefendedRate < 0.1,
            $"{undefended} captures; {undefendedRate:F3} per match ({a7Matches}; {a7Seats} AI seats)");
        Gate("A7 herd crash", herdCrashRate < 0.2,
            $"{herdCrashes} directly attributed; {herdCrashRate:F3} per match ({a7Matches}; {a7Seats} AI seats)");
        Gate("A7 idle labor", idleLaborRate == 0,
            $"{idleLabor} seasons; {idleLaborRate:F3} per match ({a7Matches}; {a7Seats} AI seats)");
        return pass;
    }

    /// <summary>Per-archetype means at the end of the match: where each style's Legacy comes from (docs/MVP/14 §17).</summary>
    private static void PrintArchetypeMeans(List<(EconomyArchetype Archetype, CivOutcome Outcome, bool Won)> civs)
    {
        Console.WriteLine("  archetype   Legacy Dominion Mastery  Firsts Wonders  Settl  Projs  KEY-1  KEY-2  Raids  Capt  Lost");
        foreach (string name in FfaNames)
        {
            var a = Enum.Parse<EconomyArchetype>(name);
            double n = 0, leg = 0, dom = 0, mas = 0, fir = 0, won = 0, set = 0, proj = 0, k1 = 0, k2 = 0, raids = 0, capt = 0, lost = 0;
            int n1 = 0, n2 = 0;
            foreach (var (arch, o, _) in civs)
            {
                if (arch != a) continue;
                n++; leg += o.Legacy; dom += o.Dominion; mas += o.Mastery; fir += o.Firsts; won += o.Wonders; set += o.Settlements; proj += o.Projects;
                raids += o.RaidsLaunched; capt += o.SettlementsCaptured; lost += o.SettlementsLost;
                if (!double.IsNaN(o.KeyMinutes[0])) { k1 += o.KeyMinutes[0]; n1++; }
                if (!double.IsNaN(o.KeyMinutes[1])) { k2 += o.KeyMinutes[1]; n2++; }
            }
            if (n == 0) continue;
            Console.WriteLine($"  {name,-9} {leg / n,8:F0} {dom / n,8:F0} {mas / n,7:F0} {fir / n,7:F0} {won / n,7:F0} {set / n,6:F1} {proj / n,6:F1} " +
                $"{(n1 > 0 ? k1 / n1 : double.NaN),6:F1} {(n2 > 0 ? k2 / n2 : double.NaN),6:F1} {raids / n,6:F1} {capt / n,5:F2} {lost / n,5:F2}");
        }
        Console.WriteLine("  raids: L=launched, R=resolved, P=in transit at match end, W=won; forecast averages all launches / win rate uses resolved raids");
        Console.WriteLine("  assaults: launched/resolved/in transit/held; then occupations broken/pending/captured; later outcomes are censored at match end");
        Console.WriteLine("  capture forecast averages all assault launches / capture rate uses resolved assaults");
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

    /// <summary>
    /// Runs the chapter-horizon AI duel gates, B29 forecast fidelity, B27/B28/B30 telemetry, and the M10
    /// Legend-vs-Warlord gate. A6's exploit search and A8's fog-fairness suite remain separate acceptance checks.
    /// </summary>
    private static int CheckM10(ContentPack pack, int seeds, int minutes, EconomyArchetype archetype, ulong firstSeed)
    {
        bool diagnostic = M10AiAcceptanceStatus.Classify(seeds, minutes) == M10AiAcceptanceStatus.Result.Diagnostic;
        Console.WriteLine("== M10 AI core: legacy A1-A5/A7 checks at the chapter horizon");
        int core = CheckM6(pack, seeds, minutes, archetype, firstSeed, out Tournament.DuelResult baseline, diagnostic,
            checkName: "M10 AI CORE");

        Console.WriteLine("== B29: KEY-4 forecast fidelity (AI forecast sampled at own KEY-3)");
        (List<B29ForecastDiagnostic.Observation> observations, int attempts, int missingForecasts, int censoredForecasts) =
            B29ForecastDiagnostic.Run(pack, seeds, minutes, archetype, firstSeed);
        B29ForecastDiagnostic.Summary b29Summary = B29ForecastDiagnostic.Summarize(observations, attempts,
            missingForecasts, censoredForecasts);
        bool b29 = b29Summary.Pass;
        Console.WriteLine($"  B29: {b29Summary.Within15Percent}/{b29Summary.Forecasts} forecasts within 15%; median {b29Summary.MedianError:P1}; " +
            $"sampled {b29Summary.Attempts} AI seats; missing at capture {b29Summary.MissingForecasts}; " +
            $"KEY-4 horizon-censored {b29Summary.CensoredForecasts} {M10AiAcceptanceStatus.FormatGateLabel(b29, seeds, minutes)}");

        Console.WriteLine("== B31: Legend vs Warlord, >= 60% (22 §16)");
        Tournament.DuelResult legend = Tournament.Duel(pack, "legend", "warlord", seeds, minutes, archetype, firstSeed);
        bool legendPass = legend.WinRateA >= 0.60;
        Console.WriteLine($"  B31: {legend.WinRateA * 100:F0}% over {legend.Matches} matches {M10AiAcceptanceStatus.FormatGateLabel(legendPass, seeds, minutes)}");

        B28TelemetrySummary b28Summary = B28TelemetrySummary.FromMatches(legend.Outcomes);
        int suicideRaids = 0, siegeBlunders = 0, mandateOverflowBlunders = 0, nakedCaravans = 0, frontierBlunders = 0;
        int mandateSaturationAiSeats = 0, aiMatches = 0;
        int raidLaunchedPredictions = 0, raidResolvedPredictions = 0, raidObservedSuccesses = 0;
        double raidCalibrationAbsoluteError = 0;
        double raidResolvedPredictedSuccess = 0;
        B27TelemetrySummary b27Summary = B27TelemetrySummary.FromMatches(baseline.Outcomes);
        foreach (MatchOutcome match in baseline.Outcomes)
            for (int c = 0; c < match.Civs.Length; c++)
            {
                if (match.Seats[c].Name != "warlord") continue;
                aiMatches++;
                CivOutcome o = match.Civs[c];
                frontierBlunders += o.UnwalledFrontierBlunders;
                suicideRaids += o.SuicideRaids;
                siegeBlunders += o.SiegeWithoutSupplyBlunders;
                raidLaunchedPredictions += o.RaidPredictionsLaunched;
                raidResolvedPredictions += o.RaidResolvedPredictions;
                raidObservedSuccesses += o.RaidObservedSuccesses;
                raidCalibrationAbsoluteError += o.RaidCalibrationAbsoluteError;
                raidResolvedPredictedSuccess += o.RaidResolvedPredictedSuccess;
                mandateOverflowBlunders += o.MandateOverflowBlunders;
                nakedCaravans += o.NakedCaravans;
                if (o.MandateSeasonsAtCap > 8) mandateSaturationAiSeats++;
            }
        int matches = baseline.Outcomes.Count;
        bool b27 = b27Summary.Pass;
        bool b28 = b28Summary.Pass;
        bool suicidePass = suicideRaids == 0;
        bool siegePass = siegeBlunders == 0;
        double mandateOverflowRate = matches == 0 ? double.NaN : (double)mandateOverflowBlunders / matches;
        double nakedCaravanRate = matches == 0 ? double.NaN : (double)nakedCaravans / matches;
        bool mandateOverflowPass = mandateOverflowRate < 0.30;
        bool nakedCaravanPass = nakedCaravanRate < 0.50;
        Console.WriteLine($"  B27 tin-poor fallback: {b27Summary.TinRouteSeats}/{b27Summary.TinPoorSeats} = {b27Summary.TinRouteRate:P0}; dormant bronze {b27Summary.DormantBlunders}/{b27Summary.Matches} = {b27Summary.DormantBlunderRate:F3} per match ({b27Summary.WarlordSeats} Warlord seats) {M10AiAcceptanceStatus.FormatGateLabel(b27, seeds, minutes)}");
        Console.WriteLine($"  B28 defenses before first believed-bronze raid: {b28Summary.Defended}/{b28Summary.Cases} = {b28Summary.DefenseRate:P0}; " +
            $"settlement raids {b28Summary.SettlementRaids}, AI-vs-AI {b28Summary.AiSettlementRaids}, believed-bronze {b28Summary.BelievedBronzeRaids}; " +
            $"actual-bronze {b28Summary.ActualBronzeRaids}; " +
            $"Legend-vs-Warlord matches {M10AiAcceptanceStatus.FormatGateLabel(b28, seeds, minutes)}");
        Console.WriteLine($"  B30 mandate overflow with positive decree EV: {mandateOverflowBlunders}/{matches} = {mandateOverflowRate:F3} per match {M10AiAcceptanceStatus.FormatGateLabel(mandateOverflowPass, seeds, minutes)}");
        Console.WriteLine($"  B30 measurable: suicide raids {suicideRaids} {M10AiAcceptanceStatus.FormatGateLabel(suicidePass, seeds, minutes)}; unsupplied siege desertion blunders {siegeBlunders} {M10AiAcceptanceStatus.FormatGateLabel(siegePass, seeds, minutes)}; naked caravans {nakedCaravans}/{matches} = {nakedCaravanRate:F3} per match {M10AiAcceptanceStatus.FormatGateLabel(nakedCaravanPass, seeds, minutes)}");
        double raidMae = raidResolvedPredictions == 0 ? double.NaN : raidCalibrationAbsoluteError / raidResolvedPredictions;
        double raidObservedRate = raidResolvedPredictions == 0 ? double.NaN : (double)raidObservedSuccesses / raidResolvedPredictions;
        Console.WriteLine($"  Raid forecast calibration (actual launched raids): {raidResolvedPredictions}/{raidLaunchedPredictions} resolved; predicted success { (raidResolvedPredictions == 0 ? double.NaN : raidResolvedPredictedSuccess / raidResolvedPredictions):P0}; observed success {raidObservedRate:P0}; mean absolute error {raidMae:F2}.");
        Console.WriteLine($"  §13.5 unwalled frontier diagnostic: {frontierBlunders} unanswered bronze-warrior threats after four seasons (no acceptance threshold specified).");
        Console.WriteLine($"  B30 mandate saturation proxy: {mandateSaturationAiSeats}/{aiMatches} AI seat-matches had >8 seasons at cap; this omits positive-EV availability and is not a gate.");
        Console.WriteLine("M10 AI check INCOMPLETE: A6 exploit search (`ai --exploit`) and A8 fog fairness tests remain separate acceptance gates.");
        bool measuredPass = core == 0 && legendPass && b27 && b28 && b29 && suicidePass && siegePass && mandateOverflowPass && nakedCaravanPass;
        Console.WriteLine(M10AiAcceptanceStatus.FormatSummary(measuredPass, seeds, minutes));
        return 1;
    }

    private static int CheckB28(ContentPack pack, int seeds, int minutes, ulong firstSeed, bool trace = false)
    {
        Console.WriteLine($"== B28: defense before the first believed-bronze raid in Legend-vs-Warlord AI matches ({seeds} seeds)");
        Tournament.DuelResult duel = Tournament.Duel(pack, "legend", "warlord", seeds, minutes, EconomyArchetype.Balanced, firstSeed,
            warlordB28Trace: trace);
        B28TelemetrySummary summary = B28TelemetrySummary.FromMatches(duel.Outcomes);
        Console.WriteLine($"  B28 defenses before first believed-bronze raid: {summary.Defended}/{summary.Cases} = {summary.DefenseRate:P0}; " +
            $"settlement raids {summary.SettlementRaids}, AI-vs-AI {summary.AiSettlementRaids}, believed-bronze {summary.BelievedBronzeRaids}; " +
            $"actual-bronze {summary.ActualBronzeRaids}; " +
            $"{duel.Matches} matches {M10AiAcceptanceStatus.FormatGateLabel(summary.Pass, seeds, minutes)}");
        foreach (MatchOutcome match in duel.Outcomes)
            foreach (string detail in match.B28Traces) Console.WriteLine($"  {detail}");
        if (M10AiAcceptanceStatus.Classify(seeds, minutes) == M10AiAcceptanceStatus.Result.Diagnostic)
            Console.WriteLine($"B28 DIAGNOSTIC ONLY: acceptance requires at least {M10AiAcceptanceStatus.MinimumSeeds} seeds and {M10AiAcceptanceStatus.MinimumMinutes}-minute chapter horizon.");
        else
            Console.WriteLine($"B28 {(summary.Pass ? "PASS" : "FAIL")}");
        return M10AiAcceptanceStatus.Classify(seeds, minutes) == M10AiAcceptanceStatus.Result.Diagnostic ? 2 : summary.Pass ? 0 : 1;
    }

    private static int CheckB27(ContentPack pack, int seeds, int minutes, EconomyArchetype archetype, ulong firstSeed)
    {
        Console.WriteLine($"== B27: tin-poor route recovery and dormant-route blunders ({seeds} seeds)");
        Tournament.DuelResult duel = Tournament.Duel(pack, "warlord", "council", seeds, minutes, archetype, firstSeed,
            warlordB27Trace: true);
        B27TelemetrySummary summary = B27TelemetrySummary.FromMatches(duel.Outcomes);
        bool diagnostic = M10AiAcceptanceStatus.Classify(seeds, minutes) == M10AiAcceptanceStatus.Result.Diagnostic;
        Console.WriteLine($"  Tin/arsenical recovery {summary.TinRouteSeats}/{summary.TinPoorSeats} = {summary.TinRouteRate:P0}; " +
            $"dormant-route blunders {summary.DormantBlunders}/{summary.Matches} = {summary.DormantBlunderRate:F3} per match; " +
            $"{summary.WarlordSeats} Warlord seats across {duel.Matches} matches " +
            $"{M10AiAcceptanceStatus.FormatGateLabel(summary.Pass, seeds, minutes)}");
        foreach (MatchOutcome match in duel.Outcomes)
            for (int c = 0; c < match.Civs.Length; c++)
            {
                CivOutcome outcome = match.Civs[c];
                if (match.Seats[c].Name != "warlord" || outcome.TinPoorEightSeasonsAfterKey3 == 0 ||
                    outcome.TinOrArsenicalRouteByChapterEnd) continue;
                Console.WriteLine($"  B27 unrecovered seat: seed={match.Seed} civ={c} KEY3={outcome.KeyMinutes[2]:F1} min " +
                    $"checkpoint season={outcome.TinPoorCheckpointSeason}; no route by chapter end");
                foreach (string trace in match.B27Traces.Where(trace => trace.Contains($" civ={c} ", StringComparison.Ordinal)))
                    Console.WriteLine($"    {trace}");
            }
        if (diagnostic)
            Console.WriteLine($"B27 DIAGNOSTIC ONLY: acceptance requires at least {M10AiAcceptanceStatus.MinimumSeeds} seeds and {M10AiAcceptanceStatus.MinimumMinutes}-minute chapter horizon.");
        return diagnostic ? 2 : summary.Pass ? 0 : 1;
    }

    /// <summary>The M6 acceptance gates A1 to A7 that need matches (docs/MVP/17 §4.3). A8 is the unit test FogFairnessTests.</summary>
    private static int CheckM6(ContentPack pack, int seeds, int minutes, EconomyArchetype archetype, ulong firstSeed,
        out Tournament.DuelResult a1, bool diagnostic = false, string checkName = "M6")
    {
        bool pass = true;
        bool Gate(string name, bool ok, string detail)
        {
            Console.WriteLine($"  {name}: {detail} {(diagnostic ? "DIAGNOSTIC" : ok ? "PASS" : "FAIL")}");
            pass &= ok;
            return ok;
        }

        Console.WriteLine("== A1: Warlord AI vs the Council bot (mirrored seeds), >= 85% on Legacy");
        a1 = Tournament.Duel(pack, "warlord", "council", seeds, minutes, archetype, firstSeed);
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

        pass &= ReportA7(a1, diagnostic);

        Console.WriteLine(FormatCoreCheckSummary(checkName, diagnostic, pass));
        return pass ? 0 : 1;
    }

    internal static string FormatCoreCheckSummary(string checkName, bool diagnostic, bool pass) =>
        $"{checkName} CHECK {(diagnostic ? "DIAGNOSTIC ONLY" : pass ? "PASSED" : "FAILED")}";
}
