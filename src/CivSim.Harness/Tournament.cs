using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CivSim.Sim.Ai;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>Who plays a seat: the Council, the smart AI at a level, or a scripted bot (docs/MVP/14 §10.2).</summary>
internal readonly record struct Player(string Name, CivControl Control)
{
    public static readonly string[] Names =
        { "council", "random", "grower", "builder", "wanderer", "rusher", "turtle", "settler", "chieftain", "warlord", "legend" };

    public static Player Parse(string name, EconomyArchetype archetype) => name.ToLowerInvariant() switch
    {
        "council" => new Player("council", CivControl.Council),
        "random" => new Player("random", CivControl.Scripted(BotKind.Random)),
        "grower" => new Player("grower", CivControl.Scripted(BotKind.OpeningGrower)),
        "builder" => new Player("builder", CivControl.Scripted(BotKind.OpeningBuilder)),
        "wanderer" => new Player("wanderer", CivControl.Scripted(BotKind.OpeningWanderer)),
        "rusher" => new Player("rusher", CivControl.Scripted(BotKind.Rusher)),
        "turtle" => new Player("turtle", CivControl.Scripted(BotKind.Turtle)),
        "settler" => new Player("settler", CivControl.Ai(Difficulty.Settler, archetype)),
        "chieftain" => new Player("chieftain", CivControl.Ai(Difficulty.Chieftain, archetype)),
        "warlord" => new Player("warlord", CivControl.Ai(Difficulty.Warlord, archetype)),
        "legend" => new Player("legend", CivControl.Ai(Difficulty.Legend, archetype)),
        _ => throw new ArgumentException($"Unknown player '{name}' (one of {string.Join(", ", Names)})."),
    };
}

/// <summary>One civilization's result and the blunders its mind committed (docs/MVP/14 §10.4).</summary>
internal sealed class CivOutcome
{
    public int Civ;
    public double Dominion, Mastery, Firsts, Wonders;
    public int Settlements, SettlementsLost, SettlementsCaptured, Projects;
    public int FoundDecrees;
    public readonly int[] PostureSeasons = new int[5];
    public readonly int[] WarlordUrgencyBands = new int[5];
    public int WarlordUrgencySeasons;
    public int DefenseUrgencyShadowHighDecisions, DefenseUrgencyShadowSlotsChanged, DefenseUrgencyShadowDisplacedDefense;
    public int[] DefenseUrgencyShadowHighDecisionsByEra = new int[ResearchCatalog.EraCount];
    public int FissionSplits, FissionPendingSeasons;
    public int OwnedLandShareBasisPoints;
    public int ConquerPlanSeasons, FrontierRacePlanSeasons, RaidStreakPostureTransitions;
    public int ThreeSeasonNonConquerRaidPostureOpportunities;
    public int AlternateOriginCampaignRescues, AlternateOriginCampaignNoRescues;
    public int PostureGateLockedSeasons, FrontierOpenSeasons;
    public readonly double[] KeyMinutes = { double.NaN, double.NaN, double.NaN };
    public int RaidsLaunched, RaidsWon;
    public int RaidsResolved, RaidExpeditionsPending, RaidPredictions;
    public int WarsDeclared, WarRejections;
    public readonly Dictionary<string, int> WarRejectionReasons = new(StringComparer.Ordinal);
    public readonly Dictionary<string, int> ThreatReasons = new(StringComparer.Ordinal);
    public int IncomingRusherRaidsLaunched, IncomingRusherRaidsResolved, IncomingRusherRaidsSucceeded;
    public long IncomingRusherRaidPartyFighters;
    public int AssaultsLaunched, AssaultsResolved, AssaultsHeld, AssaultExpeditionsPending;
    public int OccupationsBroken, OccupationsPending, AssaultPredictions;
    public double PredictedRaidSuccess, PredictedCaptureSuccess;
    public int RaidPredictionsLaunched, RaidResolvedPredictions, RaidObservedSuccesses;
    public double RaidCalibrationAbsoluteError, RaidResolvedPredictedSuccess;
    // Blunders sampled once a season.
    public int KeystoneStalls, IdleSlotSeasons, PlanSwitchesInThreeYears;
    public int AvoidableStarvationDeaths, UndefendedLosses, AttributedHerdCrashes, IdleLaborSeasons;
    public int TinPoorEightSeasonsAfterKey3;
    public int TinPoorCheckpointSeason = -1;
    public bool TinOrArsenicalRouteByChapterEnd;
    public int DormantBronzeBlunders;
    public long SparkRollouts;
    public int SparkDecisions;
    public int UnwalledFrontierBlunders;
    public bool BelievedBronzeRaidMeasured, DefendedBeforeBelievedBronzeRaid;
    public int SuicideRaids;
    public int MandateSeasonsAtCap;
    public int MandateOverflowBlunders;
    public int SiegeWithoutSupplyBlunders;
    public int NakedCaravans;
    public double Legacy => Dominion + Mastery + Firsts + Wonders;
}

internal sealed class MatchOutcome
{
    public ulong Seed;
    public Fix64? RivalPaceOverride;
    public int? FirstsEtaPessimismOverride;
    public int? WarlordNonConquerRaidPostureStreakOverride;
    public bool WarlordMaxCampaignOriginOverride;
    public bool WarlordSeatAllianceDiagnostic;
    public Fix64? WarlordFrontierEarlyBonusOverride;
    public Fix64? WarlordE3FissionThresholdOverride;
    public Fix64? WarlordDefenseResearchBonusOverride;
    public Fix64? WarlordPostureUnlockBonusOverride;
    public bool WarlordFrontierConquestPlanFloorDiagnostic;
    public Fix64? WarlordKeystoneTempoOverride;
    public bool WarlordE2ClosureSlotDiagnostic;
    public bool WarlordDefenseUrgencyShadowDiagnostic;
    public bool WarlordB30TraceEnabled;
    public bool WarlordB28TraceEnabled;
    public bool WarlordFissionSiteDiagnosticEnabled;
    public bool A2LeadCheckpointDiagnosticEnabled;
    public bool WarlordCampaignOpportunityTraceEnabled;
    public readonly List<string> B28Traces = new();
    public readonly List<string> A7Traces = new();
    public readonly List<string> B27Traces = new();
    public int B28SettlementRaidLaunches, B28AiSettlementRaidLaunches, B28ActualBronzeRaidLaunches, B28BelievedBronzeRaidLaunches;
    public readonly List<FoundFunnelDiagnostic> FoundFunnels = new();
    public readonly List<FoundFunnelSeasonSnapshot> FoundFunnelSnapshots = new();
    public readonly List<WarlordResearchPlanDiagnostic> WarlordResearchTraces = new();
    public readonly List<WarlordResearchSeasonSnapshot> WarlordResearchSnapshots = new();
    public readonly List<string> WarlordEconomyTraces = new();
    public readonly List<string> WarlordDemographyTraces = new();
    public readonly List<string> WarlordFoodLedgerTraces = new();
    public readonly List<string> WarlordFoodWorkAttributionTraces = new();
    public readonly List<string> FfaFoodDemographyTraces = new();
    public readonly List<string> FfaCaseControlTraces = new();
    public readonly List<string> WarlordFissionSiteDiagnostics = new();
    public readonly List<string> A2LeadCheckpointTraces = new();
    public readonly List<string> WarlordCampaignOpportunityTraces = new();
    public readonly List<FfaFirstClaimDiagnostic> FfaFirstClaims = new();
    public readonly List<string> FfaResearchTimeline = new();
    public readonly List<string> WarlordMandateTraces = new();
    public readonly List<string> WarlordCombatTraces = new();
    public readonly List<string> SettlementTimelineTraces = new();
    public Player[] Seats = Array.Empty<Player>();
    public CivOutcome[] Civs = Array.Empty<CivOutcome>();
    public readonly List<FirstRaceClaimDiagnostic> FirstRaceClaims = new();
    public long FinalTick;
    public int SeasonsProcessed;
    public ulong Checksum;
    public int A7AiSeatMatches, AvoidableStarvationDeaths, UndefendedLosses, AttributedHerdCrashes, IdleLaborSeasons;
}

internal readonly record struct FirstRaceEstimate(int WarlordCiv, int ForecastSeason, Fix64 RivalPace, int[] RivalEtaSteps);
internal readonly record struct FirstRaceClaimDiagnostic(int ProjectIndex, string Project, int Era, string Lineage,
    int ClaimantCiv, long Tick, FirstRaceEstimate[] WarlordEstimates);
internal readonly record struct FoundFunnelDiagnostic(int Civ, int CandidateSeasons, int FoundCandidateSeasons,
    int PositiveFoundCandidateSeasons, int FoundSelectedSeasons, int PositiveFoundPassedSeasons,
    int SeasonsObserved, int CouncilOnSeasons, int MandateUnlockedSeasons, int CouncilAndMandateSeasons,
    int FoundAffordableSeasons, int AtMandateCapSeasons, int QuotaOpenSeasons, int NaturalGateOpenSeasons,
    int ActiveButNoFoundCandidateSeasons, int MeanMandate, int MeanMandateCap, int MeanFoundCost,
    string Key3MissingPrerequisites, string Key3PrerequisitePath, int E2BreadthDone, string Key3State, Fix64 Key3Progress);
internal readonly record struct FoundFunnelSeasonSnapshot(int Civ, int Season, bool CouncilOn, bool MandateUnlocked,
    int Mandate, int MandateCap, int FoundCost, int Founded, int E3Foundings,
    int FoundQuotaRemaining, int NaturalHeadroom);
internal readonly record struct FfaFirstClaimDiagnostic(int Civ, int Project, string ProjectId, int Era, int Points);
internal readonly record struct WarlordResearchSeasonSnapshot(int Civ, int Season, ProjectState Key3State,
    Fix64 Key3Progress, Fix64 Key3EffectiveCost, bool Key3Done, int Era, ProjectState Key2State,
    Fix64 Key2Progress, bool Key2Done, Fix64 InsightPerYear, int ProjectsDone, int E2BreadthDone,
    int[] Slots, int[] Queue, int[] Plan, (int Project, ProjectState State, Fix64 Progress)[] Prerequisites,
    int[] AvailableClosureNotPlanned);
internal readonly record struct WarlordResearchPlanDiagnostic(int Civ, string Summary, string Timeline);

internal readonly record struct LegacyMatchOutcome(double TeamALegacy, double TeamBLegacy, long FinalTick, int SeasonsProcessed, ulong Checksum);

internal sealed class FfaFoodWindow
{
    public bool HasOpening;
    public Fix64 OpeningStores, ClosingStores, FoodNeed, FoodSecurity, BaseCapacity, EffectiveCapacity;
    public Fix64 FoodRequested, FoodAssigned, ForageWork, HuntWork, FishWork, FarmWork, PreserveWork;
    public Fix64 ForageYield, MeatYield, FishYield, WeirFishYield, GrainYield, OrchardFruitYield;
    public int Seasons, Births;
    public readonly int[] DeathsByCause = new int[(int)EconomyDeathCause.Senescence + 1];
}

/// <summary>Runs matches headless and scores them. The harness may use doubles and threads; the simulation does not.</summary>
internal static class Tournament
{
    public const ulong BaseSeed = 20261019;
    public static readonly int[] TeamASeats = { 0, 3 };
    public static readonly int[] TeamBSeats = { 1, 2 };

    public static MatchOutcome Play(ContentPack pack, ulong seed, Player[] seats, EconomyArchetype[] archetypes, int minutes,
        bool captureFirstRaceDiagnostics = false, Fix64? rivalPaceOverride = null, int? firstsEtaPessimismSeasons = null,
        int? warlordNonConquerRaidPostureStreak = null, bool warlordMaxCampaignOrigin = false,
        Fix64? warlordFrontierEarlyBonusOverride = null, Fix64? warlordE3FissionThresholdOverride = null,
        Fix64? warlordDefenseResearchBonusOverride = null, bool warlordDefenseUrgencyShadowDiagnostic = false,
        bool captureFoundFunnelDiagnostics = false, Fix64? warlordKeystoneTempoOverride = null,
        bool warlordE2ClosureSlotDiagnostic = false, bool warlordSettlementTrace = false,
        bool warlordSettlementTraceSwapped = false, bool warlordB30Trace = false, bool warlordB28Trace = false,
        bool warlordB27Trace = false, bool warlordSeatAllianceDiagnostic = false,
        Fix64? warlordPostureUnlockBonusOverride = null, bool warlordFissionSiteDiagnostic = false,
        bool foodShortageSkipScoutsForDiagnostics = false, bool foodShortageSkipScoutsForChieftainsForDiagnostics = false,
        bool captureFfaFirstsDiagnostic = false,
        bool captureFfaResearchTimeline = false, int ffaRotation = -1,
        bool captureFfaFoodDemographyTimeline = false,
        int? deferFissionSettlementUntilSeasonForDiagnostics = null,
        bool captureFfaCaseControlTimeline = false,
        bool warlordCampaignOpportunityTrace = false,
        bool warlordFrontierConquestPlanFloorDiagnostic = false,
        bool a2LeadCheckpointDiagnostic = false,
        int? deferWarlordS2FissionUntilSeasonForDiagnostics = null)
    {
        if (captureFirstRaceDiagnostics)
        {
            bool rusher = false, warlord = false;
            foreach (Player seat in seats)
            {
                rusher |= string.Equals(seat.Name, "rusher", StringComparison.OrdinalIgnoreCase);
                warlord |= string.Equals(seat.Name, "warlord", StringComparison.OrdinalIgnoreCase);
            }
            captureFirstRaceDiagnostics = rusher && warlord;
        }
        if (captureFoundFunnelDiagnostics)
        {
            bool hasWarlord = false;
            foreach (Player seat in seats) hasWarlord |= string.Equals(seat.Name, "warlord", StringComparison.OrdinalIgnoreCase);
            captureFoundFunnelDiagnostics = hasWarlord;
        }
        if (warlordCampaignOpportunityTrace)
        {
            bool warlord = false, grower = false;
            foreach (Player seat in seats)
            {
                warlord |= string.Equals(seat.Name, "warlord", StringComparison.OrdinalIgnoreCase);
                grower |= string.Equals(seat.Name, "grower", StringComparison.OrdinalIgnoreCase);
            }
            warlordCampaignOpportunityTrace = seed == 20261024 && warlord && grower;
        }
        GeneratedWorld world = WorldGenerator.Generate(seed);
        var controls = new CivControl[seats.Length];
        for (int i = 0; i < seats.Length; i++) controls[i] = seats[i].Control;
        MatchModel match = MatchModel.Create(seed, pack, world, archetypes, controls);
        if (captureFfaResearchTimeline || captureFfaCaseControlTimeline) match.Research.EnableDiagnosticGenerationCounters();
        match.Economy.FoodWorkAttributionTraceEnabled = warlordSettlementTrace || captureFfaFoodDemographyTimeline;
        if (foodShortageSkipScoutsForDiagnostics)
        {
            var skipScoutsForCiv = new bool[seats.Length];
            bool anyWarlordSeat = false;
            for (int civ = 0; civ < seats.Length; civ++)
            {
                skipScoutsForCiv[civ] = FoodShortageScoutSkipAppliesForDiagnostics(seats[civ].Name, foodShortageSkipScoutsForChieftainsForDiagnostics);
                anyWarlordSeat |= skipScoutsForCiv[civ];
            }
            if (anyWarlordSeat) match.Economy.SkipScoutsWhenFoodShortForCivsForDiagnostics = skipScoutsForCiv;
        }
        if (deferFissionSettlementUntilSeasonForDiagnostics is { } fissionFirstAllowedSeason)
            match.Economy.DeferFissionForDiagnostics(civ: 1, settlementId: 1, fissionFirstAllowedSeason);
        if (deferWarlordS2FissionUntilSeasonForDiagnostics is { } warlordS2FirstAllowedSeason &&
            seed == 20261024 && warlordSettlementTraceSwapped && seats.Length > 2 &&
            string.Equals(seats[2].Name, "warlord", StringComparison.OrdinalIgnoreCase))
            match.Economy.DeferFissionForDiagnostics(civ: 2, settlementId: 2, warlordS2FirstAllowedSeason);
        FissionSiteDiagnostics? fissionSiteDiagnostics = warlordFissionSiteDiagnostic && seed == 20261024
            ? new FissionSiteDiagnostics() : null;
        bool a2LeadCheckpointEnabled = a2LeadCheckpointDiagnostic && seed == 20261025 &&
            seats.Any(seat => string.Equals(seat.Name, "warlord", StringComparison.OrdinalIgnoreCase)) &&
            seats.Any(seat => string.Equals(seat.Name, "grower", StringComparison.OrdinalIgnoreCase));
        if (fissionSiteDiagnostics is not null) match.Economy.FissionSiteDiagnosticsForDiagnostics = fissionSiteDiagnostics;
        int[] fissionTrackedSettlement = Enumerable.Repeat(-1, seats.Length).ToArray();
        int[] fissionFirstPendingSeason = Enumerable.Repeat(-1, seats.Length).ToArray();
        int[] fissionFirstSplitSeason = Enumerable.Repeat(-1, seats.Length).ToArray();
        int[] fissionPriorSplits = new int[seats.Length];
        int[] fissionBirthsWindow = new int[seats.Length];
        int[,] fissionDeathsWindow = new int[seats.Length, Enum.GetValues<EconomyDeathCause>().Length];
        int[] fissionCapPressureWindow = new int[seats.Length];
        int[] fissionWinterPressureWindow = new int[seats.Length];
        long fissionPriorNextFigureId = match.Economy.NextFigureId;
        int[] leadBirthsWindow = new int[seats.Length];
        int[] leadStarvationDeathsWindow = new int[seats.Length];
        long leadPriorNextFigureId = match.Economy.NextFigureId;
        if (fissionSiteDiagnostics is not null)
            for (int c = 0; c < seats.Length; c++)
            {
                if (!string.Equals(seats[c].Name, "warlord", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(seats[c].Name, "grower", StringComparison.OrdinalIgnoreCase)) continue;
                for (int s = 0; s < match.Economy.SettlementCount; s++)
                    if (match.Politics.SettlementCivOf(s) == c)
                    { fissionTrackedSettlement[c] = match.Economy.Settlement(s).Id; break; }
            }
        int initialSettlementTotal = match.Politics.SettlementTotal;
        if (warlordB28Trace && match.Ai is { } b28TraceAi)
            for (int c = 0; c < seats.Length; c++)
                if (b28TraceAi.SeatOf(c)?.Strategist is Strategist strategist)
                    strategist.TraceBronzeDefenseForDiagnostics = true;
        bool warlordSeatAllianceApplied = false;
        if (warlordSeatAllianceDiagnostic)
        {
            int firstWarlord = -1, secondWarlord = -1;
            for (int c = 0; c < seats.Length; c++)
            {
                if (!string.Equals(seats[c].Name, "warlord", StringComparison.OrdinalIgnoreCase)) continue;
                if (firstWarlord < 0) firstWarlord = c;
                else if (secondWarlord < 0) secondWarlord = c;
            }
            if (secondWarlord >= 0)
            {
                match.Politics.Diplomacy.MarkContacted(firstWarlord, secondWarlord);
                match.Politics.Diplomacy.Ally(firstWarlord, secondWarlord);
                warlordSeatAllianceApplied = true;
            }
        }
        bool warlordPostureUnlockBonusOverrideApplied = false;
        bool warlordFrontierConquestPlanFloorApplied = false;
        if (rivalPaceOverride is not null || firstsEtaPessimismSeasons is not null || warlordNonConquerRaidPostureStreak is not null || warlordMaxCampaignOrigin || warlordFrontierEarlyBonusOverride is not null || warlordE3FissionThresholdOverride is not null || warlordDefenseResearchBonusOverride is not null || warlordPostureUnlockBonusOverride is not null || warlordDefenseUrgencyShadowDiagnostic || warlordKeystoneTempoOverride is not null || warlordE2ClosureSlotDiagnostic || warlordFrontierConquestPlanFloorDiagnostic)
            for (int c = 0; c < seats.Length; c++)
                if (string.Equals(seats[c].Name, "warlord", StringComparison.OrdinalIgnoreCase) && match.Ai?.SeatOf(c) is { } warlordSeat)
                {
                    if (warlordSeat.Spark is ResearchSpark spark)
                    {
                        if (rivalPaceOverride is { } overridePace) spark.RivalPaceOverrideForDiagnostics = overridePace;
                        if (firstsEtaPessimismSeasons is { } pessimism) spark.FirstsEtaPessimismSeasonsForDiagnostics = pessimism;
                    }
                    if (warlordSeat.Strategist is Strategist strategist && warlordNonConquerRaidPostureStreak is { } streak)
                        strategist.NonConquerRaidPostureStreakForDiagnostics = streak;
                    if (warlordSeat.Strategist is Strategist frontierStrategist && warlordFrontierEarlyBonusOverride is { } frontierBonus)
                        frontierStrategist.FrontierEarlyBonusForDiagnostics = frontierBonus;
                    if (warlordSeat.Strategist is Strategist planFloorStrategist && warlordFrontierConquestPlanFloorDiagnostic)
                    {
                        planFloorStrategist.FrontierConquestPlanFloorForDiagnostics = true;
                        warlordFrontierConquestPlanFloorApplied = true;
                    }
                    if (warlordSeat.Spark is ResearchSpark warlordSpark && warlordDefenseResearchBonusOverride is { } defenseBonus)
                        warlordSpark.DefenseResearchBonusOverrideForDiagnostics = defenseBonus;
                    if (warlordSeat.Spark is ResearchSpark postureSpark && warlordPostureUnlockBonusOverride is { } postureBonus)
                    {
                        postureSpark.PostureUnlockBonusOverrideForDiagnostics = postureBonus;
                        warlordPostureUnlockBonusOverrideApplied = true;
                    }
                    if (warlordSeat.Spark is ResearchSpark tempoSpark && warlordKeystoneTempoOverride is { } tempo)
                        tempoSpark.KeystoneTempoOverrideForDiagnostics = tempo;
                    if (warlordSeat.Spark is ResearchSpark closureSpark && warlordE2ClosureSlotDiagnostic)
                        closureSpark.E2ClosureSlotOverrideForDiagnostics = true;
                    if (warlordSeat.Spark is ResearchSpark shadowSpark && warlordDefenseUrgencyShadowDiagnostic)
                        shadowSpark.DefenseUrgencyShadowDiagnosticForDiagnostics = true;
                    if (warlordE3FissionThresholdOverride is { } fissionThreshold)
                        match.Politics.SetWarlordE3ExpandFissionThresholdOverrideForDiagnostics(c, fissionThreshold);
                    if (warlordMaxCampaignOrigin)
                    {
                        if (warlordSeat.Strategist is Strategist campaignStrategist) campaignStrategist.UseMaxCampaignOriginForDiagnostics = true;
                        match.Politics.UseMaxCampaignOriginForDiagnostics(c, enabled: true);
                    }
                }
        // Tournament scoring and telemetry use the final state, not the periodic hash history.
        var runner = new SimRunner(match, int.MaxValue);
        int civs = seats.Length;
        var outcome = new MatchOutcome
        {
            Seed = seed, RivalPaceOverride = rivalPaceOverride, FirstsEtaPessimismOverride = firstsEtaPessimismSeasons,
            WarlordNonConquerRaidPostureStreakOverride = warlordNonConquerRaidPostureStreak,
            WarlordMaxCampaignOriginOverride = warlordMaxCampaignOrigin,
            WarlordSeatAllianceDiagnostic = warlordSeatAllianceApplied,
            WarlordFrontierEarlyBonusOverride = warlordFrontierEarlyBonusOverride,
            WarlordE3FissionThresholdOverride = warlordE3FissionThresholdOverride,
            WarlordDefenseResearchBonusOverride = warlordDefenseResearchBonusOverride,
            WarlordPostureUnlockBonusOverride = warlordPostureUnlockBonusOverrideApplied ? warlordPostureUnlockBonusOverride : null,
            WarlordFrontierConquestPlanFloorDiagnostic = warlordFrontierConquestPlanFloorApplied,
            WarlordKeystoneTempoOverride = warlordKeystoneTempoOverride,
            WarlordE2ClosureSlotDiagnostic = warlordE2ClosureSlotDiagnostic,
            WarlordDefenseUrgencyShadowDiagnostic = warlordDefenseUrgencyShadowDiagnostic,
            WarlordB30TraceEnabled = warlordB30Trace,
            WarlordB28TraceEnabled = warlordB28Trace,
            WarlordFissionSiteDiagnosticEnabled = fissionSiteDiagnostics is not null,
            A2LeadCheckpointDiagnosticEnabled = a2LeadCheckpointEnabled,
            WarlordCampaignOpportunityTraceEnabled = warlordCampaignOpportunityTrace,
            Seats = seats, Civs = new CivOutcome[civs]
        };
        var firstRaceHistory = new List<FirstRaceEstimate[]>();
        var pendingCampaignEvaluations = new List<CampaignEvaluationDiagnostic>();
        if (warlordCampaignOpportunityTrace && !warlordSettlementTraceSwapped && match.Ai is { } campaignAi)
            for (int c = 0; c < seats.Length; c++)
            {
                if (!string.Equals(seats[c].Name, "warlord", StringComparison.OrdinalIgnoreCase) ||
                    campaignAi.SeatOf(c)?.Strategist is not Strategist strategist) continue;
                strategist.CampaignEvaluationObserverForDiagnostics = evaluation =>
                {
                    if (evaluation.Season < 128 || (uint)evaluation.Rival >= (uint)seats.Length ||
                        !string.Equals(seats[evaluation.Rival].Name, "grower", StringComparison.OrdinalIgnoreCase)) return;
                    pendingCampaignEvaluations.Add(evaluation);
                };
                strategist.CampaignPlanObserverForDiagnostics = (civ, planSeason, plan, target) =>
                {
                    for (int i = pendingCampaignEvaluations.Count - 1; i >= 0; i--)
                    {
                        CampaignEvaluationDiagnostic evaluation = pendingCampaignEvaluations[i];
                        if (evaluation.Civ != civ || evaluation.Season != planSeason) continue;
                        pendingCampaignEvaluations.RemoveAt(i);
                        outcome.WarlordCampaignOpportunityTraces.Add(
                            $"A2 campaign-candidate seed={seed} mirror=0 warlord=civ{evaluation.Civ} grower=civ{evaluation.Rival} " +
                            $"s{evaluation.Season} plan={plan} planTarget={(target < 0 ? "none" : $"civ{target}")} " +
                            $"target={(evaluation.Settlement < 0 ? "none" : $"s{evaluation.Settlement}@t{evaluation.Tile}")} " +
                            $"selected={evaluation.Selected} origin={(evaluation.OriginSettlement < 0 ? "none" : $"s{evaluation.OriginSettlement}")} " +
                            $"attackers={evaluation.Attackers} alternateOriginAttackers={evaluation.AlternateOriginAttackers} " +
                            $"ratio={evaluation.Ratio.ToDoubleForDisplay():F3}/{evaluation.Required.ToDoubleForDisplay():F3} " +
                            $"predictedCapture={evaluation.CaptureChance.ToDoubleForDisplay():F3} EV={evaluation.Value.ToDoubleForDisplay():F2} " +
                            $"tactical={evaluation.TacticallyViable} launchFeasible={evaluation.Feasible} reason={evaluation.Reason}");
                    }
                };
            }
        int[] lastFoundFunnelSeason = new int[civs];
        Array.Fill(lastFoundFunnelSeason, -1);
        for (int c = 0; c < civs; c++) outcome.Civs[c] = new CivOutcome { Civ = c };
        ResearchCatalog catalog = match.Research.Catalog;
        int[] keys = { catalog.IndexOf("KEY-1"), catalog.IndexOf("KEY-2"), catalog.IndexOf("KEY-3") };
        int[] ffaKey1Season = new int[civs], ffaKey2Season = new int[civs];
        Array.Fill(ffaKey1Season, -1);
        Array.Fill(ffaKey2Season, -1);
        int ffaTraceEventStart = 0;
        int a2LeadFirstClaimEventStart = match.Research.Events.Count;
        var ffaPriorInsight = new Fix64[civs];
        var ffaPriorKnowHow = new Fix64[civs, catalog.LineageCodes.Count];
        int gov02 = catalog.IndexOf("GOV-02");
        int met03 = catalog.IndexOf("MET-03"), met04 = catalog.IndexOf("MET-04"), met05 = catalog.IndexOf("MET-05");
        int min04 = catalog.IndexOf("MIN-04");
        var stalled = new bool[civs * 3];
        var key3Season = new int[civs]; Array.Fill(key3Season, -1);
        var bronzePoorMeasured = new bool[civs];
        var dormantOptionSeasons = new int[civs];
        var dormantReported = new bool[civs];
        var fallbackRouteAfterTinPoor = new bool[civs];
        var lastB27TraceState = new string[civs];
        var campaignOpportunityPrior = new Dictionary<(int Warlord, int Rival), string>();
        var campaignDecisionSignatures = new Dictionary<string, string>(StringComparer.Ordinal);
        var campaignFirstKnownSeason = new Dictionary<(int Warlord, int Rival), int>();
        var campaignFirstReachSeason = new Dictionary<(int Warlord, int Rival), int>();
        var campaignMilestoneEmitted = new HashSet<(int Warlord, int Rival)>();
        var bronzeRaidBelieved = new bool[civs][];
        var bronzeRaidBelievedMet04 = new bool[civs][];
        var bronzeRaidBelievedMet05 = new bool[civs][];
        var bronzeRivalContacted = new bool[civs][];
        var bronzeRivalFirstContactSeason = new int[civs][];
        var bronzeRivalLastSeenSeason = new int[civs][];
        var bronzeRivalSeenFlags = new int[civs][];
        for (int c = 0; c < civs; c++)
        {
            bronzeRaidBelieved[c] = new bool[civs];
            bronzeRaidBelievedMet04[c] = new bool[civs];
            bronzeRaidBelievedMet05[c] = new bool[civs];
            bronzeRivalContacted[c] = new bool[civs];
            bronzeRivalFirstContactSeason[c] = new int[civs];
            bronzeRivalLastSeenSeason[c] = new int[civs];
            bronzeRivalSeenFlags[c] = new int[civs];
            Array.Fill(bronzeRivalFirstContactSeason[c], -1);
            Array.Fill(bronzeRivalLastSeenSeason[c], -1);
        }
        var b28Timeline = new Dictionary<(int Defender, int Attacker), List<string>>();
        int b28WoodLedgerRowsEmitted = 0;
        var b28FirstBeliefEvidenceEmitted = new bool[civs, civs];
        var b28FirstBeliefSeason = new int[civs, civs];
        for (int c = 0; c < civs; c++)
            for (int other = 0; other < civs; other++) b28FirstBeliefSeason[c, other] = -1;
        int s35FirstAppearanceSeason = initialSettlementTotal > 35 ? 0 : -1;
        bool s35FirstAppearanceTraceEmitted = false;
        var siegeBlunderTracker = new SiegeBlunderTracker();
        var pendingRaidPredictions = new Dictionary<(int Civ, int Target), Queue<Fix64>>();
        var nakedTinCaravans = new NakedTinCaravanTracker[civs];
        for (int c = 0; c < civs; c++) nakedTinCaravans[c] = new NakedTinCaravanTracker();
        var mandateOverflowStreak = new int[civs];
        var mandateOverflowReported = new bool[civs];
        var totalDeaths = new int[civs];
        var starvationDeaths = new int[civs];
        bool checkpointDemographyTrace = warlordSettlementTrace &&
            seats.Any(static seat => string.Equals(seat.Name, "warlord", StringComparison.OrdinalIgnoreCase)) &&
            seats.Any(static seat => string.Equals(seat.Name, "rusher", StringComparison.OrdinalIgnoreCase));
        var checkpointDeathCauses = new int[civs, (int)EconomyDeathCause.Senescence + 1];
        var checkpointBirths = new int[civs];
        var checkpointBirthFigureIds = new HashSet<long>();
        long checkpointPriorNextFigureId = match.Economy.NextFigureId;
        var foodRaidLaunches = new Dictionary<(int Attacker, int Defender), Queue<(int Target, int Season)>>();
        string[] foodGoodIds = { "G-PLANT", "G-MEAT", "G-ROOT", "G-NUT", "G-WGRAIN", "G-GRAIN", "G-FISH", "G-DRIED", "G-FRUIT" };
        var fissionsSeen = new int[match.Economy.SettlementCount];
        for (int s = 0; s < fissionsSeen.Length; s++) fissionsSeen[s] = match.Economy.Settlement(s).Fissions;
        var ffaFoodWindows = captureFfaFoodDemographyTimeline ? new Dictionary<int, FfaFoodWindow>() : null;
        var ffaFoodFirstPendingSeason = captureFfaFoodDemographyTimeline ? new Dictionary<int, int>() : null;
        var ffaFoodFirstSplitSeason = captureFfaFoodDemographyTimeline ? new Dictionary<int, int>() : null;
        var ffaCaseControlPriorFissions = captureFfaCaseControlTimeline ? new Dictionary<int, int>() : null;
        var ffaCaseControlFirstSplitIds = captureFfaCaseControlTimeline ? new HashSet<int>() : null;
        var frontierThreatSeason = new int[civs, world.TileCount];
        var a7ThreatObservedSeason = new int[civs, world.TileCount];
        var a7DefenseResponseSeen = new bool[civs, world.TileCount];
        var frontierResponseSeen = new bool[civs, world.TileCount];
        var frontierThreatOwnSettlement = new int[civs, world.TileCount];
        var undefendedLossReported = new bool[civs, world.TileCount];
        var herdCrashReported = new bool[match.Economy.WildHerdCount];
        for (int c = 0; c < civs; c++)
            for (int s = 0; s < world.TileCount; s++)
            {
                frontierThreatSeason[c, s] = -1;
                a7ThreatObservedSeason[c, s] = -1;
                frontierThreatOwnSettlement[c, s] = -1;
            }
        long ticks = minutes * 600L;
        int season = 0;
        if (checkpointDemographyTrace)
            CaptureDemographyCheckpoint(match, seats, catalog, 0,
                checkpointBirths, checkpointDeathCauses, outcome.WarlordDemographyTraces);
        for (long t = 0; t < ticks; t += 150, season++)
        {
            Dictionary<long, int>? foodTraceAgesBefore = null;
            if (warlordSettlementTrace && season + 1 is >= 28 and <= 64)
            {
                foodTraceAgesBefore = new Dictionary<long, int>();
                for (int f = 0; f < match.Economy.FigureCount; f++)
                {
                    FigureView figure = match.Economy.Figure(f);
                    if (figure.SettlementId is 1 or 2 or 3 or 4) foodTraceAgesBefore[figure.Id] = figure.Age;
                }
            }
            if (warlordB28Trace && s35FirstAppearanceSeason < 0 && match.Politics.SettlementTotal > 35)
                s35FirstAppearanceSeason = season;
            if (warlordB28Trace && !s35FirstAppearanceTraceEmitted && s35FirstAppearanceSeason >= 0 &&
                match.Politics.SettlementTotal > 35)
            {
                s35FirstAppearanceTraceEmitted = true;
                outcome.B28Traces.Add($"B28 shadow s35-first-appearance seed={seed} season={s35FirstAppearanceSeason} " +
                    FormatB28ShadowSettlement(35, 0, s35FirstAppearanceSeason, world.Width, match));
            }
            bool traceSettlementTimeline = warlordSettlementTrace && season + 1 >= 16 && season + 1 <= 128 &&
                seats.Any(static seat => string.Equals(seat.Name, "warlord", StringComparison.OrdinalIgnoreCase));
            bool traceFoodLedger = warlordSettlementTrace && season + 1 >= 24 && season + 1 <= 80 &&
                seats.Any(static seat => string.Equals(seat.Name, "warlord", StringComparison.OrdinalIgnoreCase));
            // The belief snapshot precedes this season's political step: only evidence observed by the AI before
            // the incoming raid can qualify for B28.
            for (int c = 0; c < civs; c++)
            {
                AiSeat? seat = match.Ai?.SeatOf(c);
                if (seat?.Control.Kind != ControlKind.Ai) continue;
                RivalBelief[] rivals = seat.Belief.Rivals;
                for (int other = 0; other < civs; other++)
                {
                    if (other == c) continue;
                    RivalBelief rival = rivals[other];
                    bronzeRaidBelievedMet04[c][other] = M10TelemetryRules.IsProjectBelieved(rival, met04);
                    bronzeRaidBelievedMet05[c][other] = M10TelemetryRules.IsProjectBelieved(rival, met05);
                    bronzeRaidBelieved[c][other] = bronzeRaidBelievedMet04[c][other] || bronzeRaidBelievedMet05[c][other];
                    bronzeRivalContacted[c][other] = rival.Contacted;
                    bronzeRivalFirstContactSeason[c][other] = rival.FirstContactSeason;
                    bronzeRivalLastSeenSeason[c][other] = rival.LastSeenSeason;
                    bronzeRivalSeenFlags[c][other] = rival.SeenFlags;
                    if (!warlordB28Trace || !bronzeRaidBelieved[c][other]) continue;
                    if (!b28FirstBeliefEvidenceEmitted[c, other])
                    {
                        b28FirstBeliefEvidenceEmitted[c, other] = true;
                        b28FirstBeliefSeason[c, other] = season;
                        string rememberedSites = rival.Settlements.Count == 0 ? "none" : string.Join(",",
                            rival.Settlements.Select(site => $"s{site.Settlement}@t{site.Tile}/age{season - site.LastSeenSeason}"));
                        bool hasS35 = match.Politics.SettlementTotal > 35;
                        int s35Owner = hasS35 ? match.Politics.SettlementCivOf(35) : -1;
                        int s35Tile = hasS35 ? match.Politics.SettlementTileOf(35) : -1;
                        MapKnowledge beliefMap = seat.Belief.Map;
                        int ownerTileCount = 0, nearestOwnerTile = -1, nearestOwnerDistance = int.MaxValue, nearestOwnerAge = -1;
                        if (hasS35)
                        {
                            for (int tile = 0; tile < beliefMap.TileCount; tile++)
                            {
                                (int owner, int seenSeason) = beliefMap.OwnerSeen(tile);
                                if (owner != other) continue;
                                ownerTileCount++;
                                int distance = HexGrid.Distance(s35Tile, tile, world.Width);
                                if (distance < nearestOwnerDistance)
                                {
                                    nearestOwnerTile = tile;
                                    nearestOwnerDistance = distance;
                                    nearestOwnerAge = seenSeason < 0 ? -1 : season - seenSeason;
                                }
                            }
                        }
                        string nearestOwner = nearestOwnerTile < 0 ? "none" : $"t{nearestOwnerTile}/dist{nearestOwnerDistance}/age{nearestOwnerAge}";
                        outcome.B28Traces.Add($"B28 first-belief-evidence seed={seed} defender=civ{c} attacker=civ{other} season={season} " +
                            $"initialSettlements={initialSettlementTotal} currentSettlements={match.Politics.SettlementTotal} " +
                            $"s35Exists={hasS35} s35DefenderOwned={hasS35 && s35Owner == c} s35Owner={(s35Owner < 0 ? "none" : $"civ{s35Owner}")} s35Tile={(s35Tile < 0 ? "none" : $"t{s35Tile}")} " +
                            $"rememberedRivalSites=[{rememberedSites}] knownOwnerTilesCiv{other}={ownerTileCount} nearestToS35={nearestOwner} " +
                            FormatB28ShadowSettlement(35, c, season, world.Width, match, s35FirstAppearanceSeason));
                    }
                    var key = (c, other);
                    if (!b28Timeline.TryGetValue(key, out List<string>? rows))
                        b28Timeline[key] = rows = new List<string>();
                    // Retain a compact per-season causal timeline until this pair's first qualifying raid.
                    if (rows.Count >= 64) continue;
                    bool recentRaid = season - rival.LastRaidOnUsSeason is >= 0 and <= 4;
                    bool support = rival.AtWar || rival.ThreatProbability >= Fix64.FromRatio(1, 10) || recentRaid;
                    int reach = match.Politics.ReachOf(c);
                    int defenseRange = StrategistTuning.RaidRangeReaches * Math.Max(6, reach);
                    int nearestSite = -1, nearestDistance = int.MaxValue, siteAge = -1, exposed = -1;
                    foreach (BeliefSettlement site in rival.Settlements)
                    {
                        int age = season - site.LastSeenSeason;
                        if (age is < 0 or > 8) continue;
                        for (int ownSettlement = 0; ownSettlement < match.Politics.SettlementTotal; ownSettlement++)
                        {
                            if (match.Politics.SettlementCivOf(ownSettlement) != c || match.Politics.IsFreeFolk(ownSettlement)) continue;
                            Defenses defenses = match.Politics.MilitaryOf(ownSettlement).Defenses;
                            if (defenses.Palisade || defenses.Hillfort || defenses.StoneWalls) continue;
                            int d = HexGrid.Distance(match.Politics.SettlementTileOf(ownSettlement), site.Tile, world.Width);
                            if (d < nearestDistance) { nearestDistance = d; nearestSite = site.Tile; siteAge = age; exposed = ownSettlement; }
                        }
                    }
                    int distanceGate = nearestDistance <= defenseRange ? 1 : 0;
                    string targetState = exposed < 0 ? "none" : FormatB28Settlement(exposed, match);
                    var policyRows = match.Ai!.Log.Entries.Where(d => d.Civ == c && d.Kind == "DefenseBuild" && d.Season <= season)
                        .TakeLast(1).Select(d => $"{d.Season}:DefenseBuild:s{d.Subject}");
                    string candidate = support && distanceGate != 0 && exposed >= 0 ? $"s{exposed}" : "none";
                    rows.Add($"s{season} belief={bronzeRaidBelievedMet04[c][other]}/{bronzeRaidBelievedMet05[c][other]} war={rival.AtWar} " +
                        $"threat={rival.ThreatProbability.ToDoubleForDisplay():F3} support={support} recentRaid={recentRaid} " +
                        $"rivalSite={nearestSite}@age{siteAge}/dist{nearestDistance}/inRange={distanceGate} candidate={candidate} state={targetState} " +
                        $"DEF-03(actual trace context)={match.Research.IsDone(c, catalog.IndexOf("DEF-03"))} " +
                        $"DefenseBuild/policy=[{string.Join(',', policyRows)}]");
                }
            }
            int politicsEventsBefore = match.Politics.Events.Count;
            int aiLogEntriesBefore = match.Ai?.Log.Entries.Count ?? 0;
            var mandateBeforeAdvance = new int[civs];
            var mandateCapBeforeAdvance = new int[civs];
            for (int c = 0; c < civs; c++)
            {
                mandateBeforeAdvance[c] = match.Research.Mandate(c);
                mandateCapBeforeAdvance[c] = match.Research.MandateCap(c);
            }
            var foodStoresBeforeAdvance = new Fix64[match.Economy.SettlementCount];
            var foodGoodStocksBeforeAdvance = traceFoodLedger
                ? new Fix64[match.Economy.SettlementCount, foodGoodIds.Length]
                : null;
            var settlementOwnersBeforeAdvance = new int[match.Economy.SettlementCount];
            var settlementIdsBeforeAdvance = new int[match.Economy.SettlementCount];
            var settlementFissionsBeforeAdvance = new int[match.Economy.SettlementCount];
            var ffaCaseControlPreSettlements = captureFfaCaseControlTimeline
                ? new Dictionary<int, (int Owner, int Population, int Children, int Adults, int Elders, Fix64 Stores, Fix64 FoodSecurity)>()
                : null;
            var ffaFoodPriorFissions = captureFfaFoodDemographyTimeline ? new Dictionary<int, int>() : null;
            var settlementFieldsBeforeAdvance = traceSettlementTimeline ? new int[match.Economy.SettlementCount] : null;
            var settlementOrchardsBeforeAdvance = traceSettlementTimeline ? new int[match.Economy.SettlementCount] : null;
            var settlementHerdsBeforeAdvance = traceSettlementTimeline
                ? new (int SmallStock, int Cattle, int Pigs)[match.Economy.SettlementCount]
                : null;
            for (int s = 0; s < foodStoresBeforeAdvance.Length; s++)
            {
                SettlementView view = match.Economy.Settlement(s);
                foodStoresBeforeAdvance[s] = view.FoodStores;
                if (foodGoodStocksBeforeAdvance is not null && view.Id is 1 or 2 or 3 or 4)
                    for (int good = 0; good < foodGoodIds.Length; good++)
                        foodGoodStocksBeforeAdvance[s, good] = match.Economy.GoodStock(s, foodGoodIds[good]);
                settlementOwnersBeforeAdvance[s] = match.Politics.SettlementCivOf(s);
                settlementIdsBeforeAdvance[s] = view.Id;
                settlementFissionsBeforeAdvance[s] = view.Fissions;
                if (ffaCaseControlPriorFissions is not null)
                {
                    ffaCaseControlPriorFissions[view.Id] = view.Fissions;
                    ffaCaseControlPreSettlements![view.Id] = (match.Politics.SettlementCivOf(s), view.Population,
                        match.Economy.AgeBandPopulation(s, 0), match.Economy.AgeBandPopulation(s, 1),
                        match.Economy.AgeBandPopulation(s, 2), view.FoodStores, view.LastFoodSecurity);
                }
                if (ffaFoodPriorFissions is not null)
                {
                    ffaFoodPriorFissions[view.Id] = view.Fissions;
                    if (view.FissionPending && !ffaFoodFirstPendingSeason!.ContainsKey(view.Id))
                        ffaFoodFirstPendingSeason[view.Id] = season;
                }
                if (traceSettlementTimeline)
                {
                    settlementFieldsBeforeAdvance![s] = match.Economy.FieldCount(s);
                    settlementOrchardsBeforeAdvance![s] = match.Economy.OrchardCount(s);
                    settlementHerdsBeforeAdvance![s] = match.Economy.DomesticStock(s);
                }
            }
            var defensesBeforeAdvance = new Defenses[match.Politics.SettlementTotal];
            for (int s = 0; s < defensesBeforeAdvance.Length; s++)
                defensesBeforeAdvance[s] = match.Politics.MilitaryOf(s).Defenses;
            // Actual research is sampled alongside the pre-advance defense snapshot for trace context only.
            var actualMet04BeforeAdvance = new bool[civs];
            var actualMet05BeforeAdvance = new bool[civs];
            for (int c = 0; c < civs; c++)
            {
                actualMet04BeforeAdvance[c] = match.Research.IsDone(c, met04);
                actualMet05BeforeAdvance[c] = match.Research.IsDone(c, met05);
            }
            FirstRaceEstimate[] firstRaceEstimates = captureFirstRaceDiagnostics
                ? CaptureFirstRaceEstimates(match, seats, catalog.Count)
                : Array.Empty<FirstRaceEstimate>();
            if (captureFirstRaceDiagnostics)
            {
                firstRaceHistory.Add(firstRaceEstimates);
                if (firstRaceHistory.Count > 20) firstRaceHistory.RemoveAt(0);
            }
            if (captureFoundFunnelDiagnostics)
                for (int c = 0; c < civs; c++)
                {
                    if (!string.Equals(seats[c].Name, "warlord", StringComparison.OrdinalIgnoreCase)) continue;
                    int funnelSeason = match.Politics.Season;
                    if (lastFoundFunnelSeason[c] == funnelSeason) continue;
                    lastFoundFunnelSeason[c] = funnelSeason;
                    int founded = match.Politics.DecreesDone(c, DecreeKind.Found);
                    int natural = match.Politics.E3Foundings(c) - founded;
                    int foundCost = match.Politics.DecreeCost(c, DecreeKind.Found);
                    int mandate = match.Research.Mandate(c);
                    int mandateCap = match.Research.MandateCap(c);
                    outcome.FoundFunnelSnapshots.Add(new FoundFunnelSeasonSnapshot(c, funnelSeason,
                        match.Research.CouncilOn(c), match.Research.MandateUnlocked(c), mandate, mandateCap,
                        foundCost, founded, match.Politics.E3Foundings(c),
                        Math.Max(0, CouncilDecreePolicy.MaxFoundsPerAge - founded), natural - (founded + 1)));
                    if (funnelSeason % 32 == 0)
                        outcome.WarlordEconomyTraces.Add(FormatWarlordEconomySnapshot(match, c,
                            totalDeaths[c], starvationDeaths[c]));

                }
            int researchEventsBeforeAdvance = match.Research.Events.Count;
            long ffaFoodPriorNextFigureId = captureFfaFoodDemographyTimeline ? match.Economy.NextFigureId : 0;
            // Levy and siege hosts are intentionally removed from work capacity. Snapshot their IDs on both
            // sides of the advance so a host that expires at the season boundary is not called idle labor.
            var intentionallyIdleFigureIds = new HashSet<long>();
            for (int f = 0; f < match.Economy.FigureCount; f++)
            {
                FigureView figure = match.Economy.Figure(f);
                if (match.Economy.IsIdle(figure.Id)) intentionallyIdleFigureIds.Add(figure.Id);
            }
            long nextFigureIdBeforeAdvance = warlordSettlementTrace ? match.Economy.NextFigureId : -1;
            runner.Advance(Math.Min(150, ticks - t));
            if (warlordCampaignOpportunityTrace)
                CaptureWarlordCampaignOpportunityTrace(match, seats, outcome, season + 1,
                    aiLogEntriesBefore, politicsEventsBefore, campaignOpportunityPrior,
                    campaignDecisionSignatures, campaignFirstKnownSeason, campaignFirstReachSeason,
                    campaignMilestoneEmitted, swapped: warlordSettlementTraceSwapped);
            if (traceFoodLedger)
            {
                for (int e = politicsEventsBefore; e < match.Politics.Events.Count; e++)
                {
                    PoliticsEvent ev = match.Politics.Events[e];
                    int eventSeason = checked((int)(ev.Tick / LivingEconomyModel.SeasonTicks));
                    if (ev.Kind == PoliticsEventKind.RaidLaunched && (ev.B == 1 || ev.B == 3))
                    {
                        var key = (ev.Civ, ev.Other);
                        if (!foodRaidLaunches.TryGetValue(key, out Queue<(int Target, int Season)>? queue))
                            foodRaidLaunches[key] = queue = new Queue<(int Target, int Season)>();
                        queue.Enqueue((ev.B, eventSeason));
                        outcome.WarlordFoodLedgerTraces.Add($"A3 food-raid seed={seed} mirror={(warlordSettlementTraceSwapped ? 1 : 0)} event=launch season={eventSeason} attacker=civ{ev.Civ} defender=civ{ev.Other} target=s{ev.B}");
                    }
                    else if (ev.Kind == PoliticsEventKind.RaidResolved && ev.A is 2 or 3)
                    {
                        var key = (ev.Other, ev.Civ);
                        if (foodRaidLaunches.TryGetValue(key, out Queue<(int Target, int Season)>? queue) && queue.Count > 0)
                        {
                            (int target, int launchSeason) = queue.Dequeue();
                            if (target == 1 || target == 3)
                                outcome.WarlordFoodLedgerTraces.Add($"A3 food-raid seed={seed} mirror={(warlordSettlementTraceSwapped ? 1 : 0)} event=resolved season={eventSeason} launchSeason={launchSeason} attacker=civ{ev.Other} defender=civ{ev.Civ} target=s{target} success={ev.A == 2} reportedLoot={ev.B}");
                        }
                    }
                }
                int foodTraceSeason = season + 1;
                for (int settlementId = 1; settlementId <= 4; settlementId++)
                {
                    int current = -1, prior = -1;
                    for (int s = 0; s < match.Economy.SettlementCount; s++)
                        if (match.Economy.Settlement(s).Id == settlementId) { current = s; break; }
                    for (int s = 0; s < settlementIdsBeforeAdvance.Length; s++)
                        if (settlementIdsBeforeAdvance[s] == settlementId) { prior = s; break; }
                    if (current < 0 || prior < 0 || foodGoodStocksBeforeAdvance is null) continue;
                    SettlementView after = match.Economy.Settlement(current);
                    Fix64 preEat = match.Economy.LastSeasonFoodStoresBeforeConsumption(current);
                    Fix64 need = match.Economy.LastSeasonFoodNeed(current);
                    Fix64 foodWork = Fix64.Zero, totalWork = Fix64.Zero, baselineCapacity = Fix64.Zero;
                    int adults = 0;
                    for (int f = 0; f < match.Economy.FigureCount; f++)
                    {
                        FigureView figure = match.Economy.Figure(f);
                        if (figure.SettlementId != settlementId) continue;
                        totalWork += figure.WorkThisSeason;
                        if (figure.Job is EconomyJob.Forage or EconomyJob.Hunt or EconomyJob.Fish or EconomyJob.Farm or EconomyJob.Preserve)
                            foodWork += figure.WorkThisSeason;
                        if (figure.Age is >= 14 and < 45) { adults++; baselineCapacity += figure.Health; }
                        else if (figure.Age >= 45) baselineCapacity += figure.Health * Fix64.Half;
                    }
                    var sourceDeltas = new List<string>();
                    for (int good = 0; good < foodGoodIds.Length; good++)
                    {
                        Fix64 delta = match.Economy.GoodStock(current, foodGoodIds[good]) -
                            foodGoodStocksBeforeAdvance[prior, good];
                        if (delta != Fix64.Zero) sourceDeltas.Add($"{foodGoodIds[good]}:{delta.ToDoubleForDisplay():F1}");
                    }
                    int owner = match.Politics.SettlementCivOf(current);
                    outcome.WarlordFoodLedgerTraces.Add($"A3 food-ledger seed={seed} mirror={(warlordSettlementTraceSwapped ? 1 : 0)} season={foodTraceSeason} settlement=s{settlementId} civ={owner}/{seats[owner].Name} " +
                        $"opening={foodStoresBeforeAdvance[prior].ToDoubleForDisplay():F1} preEat={preEat.ToDoubleForDisplay():F1} need={need.ToDoubleForDisplay():F1} short={(preEat < need)} closing={after.FoodStores.ToDoubleForDisplay():F1} " +
                        $"foodJobWork={foodWork.ToDoubleForDisplay():F2} totalAssignedWork={totalWork.ToDoubleForDisplay():F2} adults={adults} baselineAdultCapacity={baselineCapacity.ToDoubleForDisplay():F2} " +
                        $"foodGoodNetDeltas=[{string.Join(',', sourceDeltas)}]");
                }
            }
            if (foodTraceAgesBefore is not null)
            {
                int traceSeason = season + 1;
                for (int settlementId = 1; settlementId <= 4; settlementId++)
                {
                    int index = -1;
                    for (int s = 0; s < match.Economy.SettlementCount; s++)
                        if (match.Economy.Settlement(s).Id == settlementId) { index = s; break; }
                    if (index < 0 || !match.Economy.TryGetFoodWorkAttribution(index, out FoodWorkAttribution work)) continue;
                    int owner = match.Politics.SettlementCivOf(index);
                    var deaths = new List<string>();
                    for (int d = 0; d < match.Economy.LastDeathEventCount; d++)
                    {
                        EconomyDeathEvent death = match.Economy.LastDeathEvent(d);
                        if (death.SettlementId == settlementId)
                            deaths.Add($"{death.Cause}:age{death.Age}");
                    }
                    int aged = 0;
                    int births = 0;
                    for (int f = 0; f < match.Economy.FigureCount; f++)
                    {
                        FigureView figure = match.Economy.Figure(f);
                        if (figure.SettlementId == settlementId && figure.Id >= nextFigureIdBeforeAdvance && figure.Age == 0)
                            births++;
                        if (figure.SettlementId == settlementId && foodTraceAgesBefore.TryGetValue(figure.Id, out int oldAge) && figure.Age > oldAge)
                            aged++;
                    }
                    SettlementView view = match.Economy.Settlement(index);
                    outcome.WarlordFoodWorkAttributionTraces.Add(
                        $"A3 work-attribution seed={seed} mirror={(warlordSettlementTraceSwapped ? 1 : 0)} season={traceSeason} settlement=s{settlementId} civ={owner}/{seats[owner].Name} " +
                        $"openingFood/need={work.OpeningFoodStores.ToDoubleForDisplay():F2}/{work.FoodNeed.ToDoubleForDisplay():F2} foodShort={work.OpeningFoodStores < work.FoodNeed} " +
                        $"ages(child/adult/elder)={match.Economy.AgeBandPopulation(index, 0)}/{match.Economy.AgeBandPopulation(index, 1)}/{match.Economy.AgeBandPopulation(index, 2)} " +
                        $"capacity(base/effective)={work.BaseCapacity.ToDoubleForDisplay():F2}/{work.EffectiveCapacity.ToDoubleForDisplay():F2} " +
                        $"reserved(fire/care/staff/scout/weirBudget/weirAssigned/orchard)={work.FireWork.ToDoubleForDisplay():F2}/{work.CareWork.ToDoubleForDisplay():F2}/{work.StaffWork.ToDoubleForDisplay():F2}/{work.ScoutWork.ToDoubleForDisplay():F2}/{work.WeirUpkeepBudget.ToDoubleForDisplay():F2}/{work.WeirUpkeepAssigned.ToDoubleForDisplay():F2}/{work.OrchardUpkeep.ToDoubleForDisplay():F2} scoutWork={work.ScoutWork.ToDoubleForDisplay():F2} " +
                        $"foodWork(requested/assigned)={work.FoodWorkRequested.ToDoubleForDisplay():F2}/{work.FoodWorkAssigned.ToDoubleForDisplay():F2} " +
                        $"jobs(forage/hunt/fish/farm/preserve)={work.ForageWork.ToDoubleForDisplay():F2}/{work.HuntWork.ToDoubleForDisplay():F2}/{work.FishWork.ToDoubleForDisplay():F2}/{work.FarmWork.ToDoubleForDisplay():F2}/{work.PreserveWork.ToDoubleForDisplay():F2} " +
                        $"yield(forage/meat/fish/weir/grain/orchardFruit)={work.ForageYield.ToDoubleForDisplay():F2}/{work.MeatYield.ToDoubleForDisplay():F2}/{work.FishYield.ToDoubleForDisplay():F2}/{work.WeirFishYield.ToDoubleForDisplay():F2}/{work.GrainYield.ToDoubleForDisplay():F2}/{work.OrchardFruitYield.ToDoubleForDisplay():F2} " +
                        $"pop={view.Population} births={births} deaths=[{string.Join(',', deaths)}] figuresAged={aged}");
                }
            }
            if (checkpointDemographyTrace)
                for (int f = 0; f < match.Economy.FigureCount; f++)
                {
                    FigureView figure = match.Economy.Figure(f);
                    if (figure.Id >= checkpointPriorNextFigureId && checkpointBirthFigureIds.Add(figure.Id))
                        checkpointBirths[figure.Civilization]++;
                }
            if (warlordB28Trace)
                while (b28WoodLedgerRowsEmitted < match.Economy.B28LocalWoodLedgerRows.Count)
                    outcome.B28Traces.Add($"B28 s35 wood-ledger seed={seed} {match.Economy.B28LocalWoodLedgerRows[b28WoodLedgerRowsEmitted++]}");
            if (warlordB28Trace && match.Economy.SettlementCount > 35 &&
                match.Economy.SettlementHasQueuedPhysicalDefenseBuild(35) &&
                match.Economy.B28DefenseWorkTrace is { } defenseWorkTrace)
                outcome.B28Traces.Add($"B28 s35 work seed={seed} {defenseWorkTrace}");
            if (warlordB28Trace && match.Ai is { } b28AiAfterAdvance)
                for (int i = aiLogEntriesBefore; i < b28AiAfterAdvance.Log.Entries.Count; i++)
                {
                    AiDecision decision = b28AiAfterAdvance.Log.Entries[i];
                    if (decision.Kind != "B28DefenseAttempt") continue;
                    outcome.B28Traces.Add($"B28 selector/order seed {seed} season={decision.Season} civ={decision.Civ} " +
                        $"selected={(decision.Subject < 0 ? "none" : $"s{decision.Subject}")} orderAccepted={decision.Other == 1} {decision.Reason}");
                }
            for (int f = 0; f < match.Economy.FigureCount; f++)
            {
                FigureView figure = match.Economy.Figure(f);
                if (match.Economy.IsIdle(figure.Id)) intentionallyIdleFigureIds.Add(figure.Id);
            }
            for (int d = 0; d < match.Economy.LastDeathEventCount; d++)
            {
                EconomyDeathEvent death = match.Economy.LastDeathEvent(d);
                if ((uint)death.Civilization >= (uint)civs) continue;
                totalDeaths[death.Civilization]++;
                if (death.Cause == EconomyDeathCause.Starvation)
                {
                    starvationDeaths[death.Civilization]++;
                    if (fissionSiteDiagnostics is not null) leadStarvationDeathsWindow[death.Civilization]++;
                }
                if (checkpointDemographyTrace && (uint)death.Cause <= (uint)EconomyDeathCause.Senescence)
                    checkpointDeathCauses[death.Civilization, (int)death.Cause]++;
                if (checkpointDemographyTrace && death.FigureId >= checkpointPriorNextFigureId &&
                    checkpointBirthFigureIds.Add(death.FigureId))
                    checkpointBirths[death.Civilization]++;
                if (fissionSiteDiagnostics is not null && death.SettlementId == fissionTrackedSettlement[death.Civilization])
                    fissionDeathsWindow[death.Civilization, (int)death.Cause]++;
            }
            int completedSeason = season + 1;
            if (captureFfaFoodDemographyTimeline && completedSeason <= 80)
            {
                if (completedSeason > 16)
                {
                    for (int d = 0; d < match.Economy.LastDeathEventCount; d++)
                    {
                        EconomyDeathEvent death = match.Economy.LastDeathEvent(d);
                        if (death.SettlementId >= 0 && (uint)death.Cause <= (uint)EconomyDeathCause.Senescence)
                            GetFfaFoodWindow(ffaFoodWindows!, death.SettlementId).DeathsByCause[(int)death.Cause]++;
                    }
                    for (int f = 0; f < match.Economy.FigureCount; f++)
                    {
                        FigureView figure = match.Economy.Figure(f);
                        if (figure.Id >= ffaFoodPriorNextFigureId && figure.SettlementId >= 0)
                            GetFfaFoodWindow(ffaFoodWindows!, figure.SettlementId).Births++;
                    }
                }
                var checkpointSettlements = new List<(int Id, int Owner, SettlementView View)>();
                for (int s = 0; s < match.Economy.SettlementCount; s++)
                {
                    SettlementView view = match.Economy.Settlement(s);
                    int owner = match.Politics.SettlementCivOf(s);
                    if (owner < 0 || owner >= civs || match.Politics.IsFreeFolk(s)) continue;
                    FfaFoodWindow window = GetFfaFoodWindow(ffaFoodWindows!, view.Id);
                    int previousFissions = ffaFoodPriorFissions?.GetValueOrDefault(view.Id, view.Fissions) ?? view.Fissions;
                    if (view.FissionPending && !ffaFoodFirstPendingSeason!.ContainsKey(view.Id))
                        ffaFoodFirstPendingSeason[view.Id] = completedSeason;
                    if (view.Fissions > previousFissions && !ffaFoodFirstSplitSeason!.ContainsKey(view.Id))
                        ffaFoodFirstSplitSeason[view.Id] = completedSeason;
                    int beforeIndex = Array.IndexOf(settlementIdsBeforeAdvance, view.Id);
                    if (completedSeason > 16 && !window.HasOpening)
                    {
                        window.OpeningStores = beforeIndex >= 0 ? foodStoresBeforeAdvance[beforeIndex] : view.FoodStores;
                        window.HasOpening = true;
                    }
                    if (completedSeason > 16)
                    {
                        window.ClosingStores = view.FoodStores;
                        window.FoodSecurity += view.LastFoodSecurity;
                        window.FoodNeed += match.Economy.LastSeasonFoodNeed(s);
                        window.Seasons++;
                        if (match.Economy.TryGetFoodWorkAttribution(s, out FoodWorkAttribution work))
                        {
                            window.BaseCapacity += work.BaseCapacity;
                            window.EffectiveCapacity += work.EffectiveCapacity;
                            window.FoodRequested += work.FoodWorkRequested;
                            window.FoodAssigned += work.FoodWorkAssigned;
                            window.ForageWork += work.ForageWork; window.HuntWork += work.HuntWork;
                            window.FishWork += work.FishWork; window.FarmWork += work.FarmWork;
                            window.PreserveWork += work.PreserveWork;
                            window.ForageYield += work.ForageYield; window.MeatYield += work.MeatYield;
                            window.FishYield += work.FishYield; window.WeirFishYield += work.WeirFishYield;
                            window.GrainYield += work.GrainYield; window.OrchardFruitYield += work.OrchardFruitYield;
                        }
                    }
                    checkpointSettlements.Add((view.Id, owner, view));
                }
                if (completedSeason >= 24 && completedSeason % 8 == 0)
                {
                    foreach ((int id, int owner, SettlementView view) in checkpointSettlements)
                    {
                        FfaFoodWindow window = GetFfaFoodWindow(ffaFoodWindows!, id);
                        int currentIndex = FindSettlementIndex(match, id);
                        string causes = string.Join(',', Enum.GetValues<EconomyDeathCause>()
                            .Where(cause => window.DeathsByCause[(int)cause] > 0)
                            .Select(cause => $"{cause}:{window.DeathsByCause[(int)cause]}"));
                        string pendingSeason = ffaFoodFirstPendingSeason!.TryGetValue(id, out int pendingAt) ? $"s{pendingAt}" : "never";
                        string splitSeason = ffaFoodFirstSplitSeason!.TryGetValue(id, out int splitAt) ? $"s{splitAt}" : "never";
                        double divisor = Math.Max(1, window.Seasons);
                        outcome.FfaFoodDemographyTraces.Add($"A5_FOOD_DEMO seed={seed} rotation={ffaRotation} season={completedSeason} " +
                            $"seat={archetypes[owner]}/c{owner} settlement=s{id} pop={view.Population} ages(child/adult/elder)=" +
                            $"{match.Economy.AgeBandPopulation(currentIndex, 0)}/{match.Economy.AgeBandPopulation(currentIndex, 1)}/{match.Economy.AgeBandPopulation(currentIndex, 2)} " +
                            $"births={window.Births} deaths=[{causes}] foodSecurityAvg={(window.FoodSecurity.ToDoubleForDisplay() / divisor):F2} " +
                            $"stores={window.OpeningStores.ToDoubleForDisplay():F1}->{window.ClosingStores.ToDoubleForDisplay():F1} foodNeed={window.FoodNeed.ToDoubleForDisplay():F1} " +
                            $"workAvg(base/effective)={(window.BaseCapacity.ToDoubleForDisplay() / divisor):F2}/{(window.EffectiveCapacity.ToDoubleForDisplay() / divisor):F2} " +
                            $"foodWork(requested/assigned)={window.FoodRequested.ToDoubleForDisplay():F2}/{window.FoodAssigned.ToDoubleForDisplay():F2} " +
                            $"jobs(forage/hunt/fish/farm/preserve)={window.ForageWork.ToDoubleForDisplay():F2}/{window.HuntWork.ToDoubleForDisplay():F2}/{window.FishWork.ToDoubleForDisplay():F2}/{window.FarmWork.ToDoubleForDisplay():F2}/{window.PreserveWork.ToDoubleForDisplay():F2} " +
                            $"yield(forage/meat/fish/weir/grain/orchard)={window.ForageYield.ToDoubleForDisplay():F1}/{window.MeatYield.ToDoubleForDisplay():F1}/{window.FishYield.ToDoubleForDisplay():F1}/{window.WeirFishYield.ToDoubleForDisplay():F1}/{window.GrainYield.ToDoubleForDisplay():F1}/{window.OrchardFruitYield.ToDoubleForDisplay():F1} " +
                            $"caps(food/housing/group/effective)={view.FoodCap.ToDoubleForDisplay():F1}/{view.HousingCap.ToDoubleForDisplay():F1}/{view.GroupCap.ToDoubleForDisplay():F1}/{view.EffectivePopulationCap.ToDoubleForDisplay():F1} " +
                            $"fission(pending/firstPending/firstSplit/splits)={view.FissionPending}/{pendingSeason}/{splitSeason}/{view.Fissions}");
                        ffaFoodWindows![id] = new FfaFoodWindow();
                    }
                }
            }
            if (captureFfaCaseControlTimeline)
            {
                for (int s = 0; s < match.Economy.SettlementCount; s++)
                {
                    SettlementView parent = match.Economy.Settlement(s);
                    int priorFissions = ffaCaseControlPriorFissions!.GetValueOrDefault(parent.Id, parent.Fissions);
                    if (parent.Fissions <= priorFissions || !ffaCaseControlFirstSplitIds!.Add(parent.Id) ||
                        !ffaCaseControlPreSettlements!.TryGetValue(parent.Id, out var pre)) continue;
                    int owner = match.Politics.SettlementCivOf(s);
                    int daughterId = -1, daughterIndex = -1;
                    for (int n = 0; n < match.Economy.SettlementCount; n++)
                    {
                        SettlementView candidate = match.Economy.Settlement(n);
                        if (match.Politics.SettlementCivOf(n) != owner || ffaCaseControlPreSettlements.ContainsKey(candidate.Id)) continue;
                        daughterId = candidate.Id; daughterIndex = n; break;
                    }
                    int adultHeads = 0;
                    Fix64 adultWork = Fix64.Zero;
                    for (int f = 0; f < match.Economy.FigureCount; f++)
                    {
                        FigureView figure = match.Economy.Figure(f);
                        if (figure.SettlementId != parent.Id && figure.SettlementId != daughterId) continue;
                        if (figure.Age is >= 14 and < 45) { adultHeads++; adultWork += figure.WorkThisSeason; }
                    }
                    string daughter = daughterIndex < 0 ? "none" : $"s{daughterId}";
                    string daughterPopulation = daughterIndex < 0 ? "0/0/0/0" :
                        $"{match.Economy.Settlement(daughterIndex).Population}/" +
                        $"{match.Economy.AgeBandPopulation(daughterIndex, 0)}/" +
                        $"{match.Economy.AgeBandPopulation(daughterIndex, 1)}/" +
                        $"{match.Economy.AgeBandPopulation(daughterIndex, 2)}";
                    string daughterStores = daughterIndex < 0 ? "0.0" :
                        match.Economy.Settlement(daughterIndex).FoodStores.ToDoubleForDisplay().ToString("F1", CultureInfo.InvariantCulture);
                    string daughterSecurity = daughterIndex < 0 ? "0.00" :
                        match.Economy.Settlement(daughterIndex).LastFoodSecurity.ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture);
                    outcome.FfaCaseControlTraces.Add($"A5_CASE_SPLIT seed={seed} rotation={ffaRotation} season={completedSeason} " +
                        $"seat={archetypes[owner]}/c{owner} parent=s{parent.Id} daughter={daughter} " +
                        $"donorPre(pop/child/adult/elder)={pre.Population}/{pre.Children}/{pre.Adults}/{pre.Elders} " +
                        $"donorStores(open/close)={pre.Stores.ToDoubleForDisplay():F1}/{parent.FoodStores.ToDoubleForDisplay():F1} " +
                        $"donorFoodSecurityBefore={pre.FoodSecurity.ToDoubleForDisplay():F2} " +
                        $"daughterPost(pop/child/adult/elder)={daughterPopulation} daughterStores={daughterStores} " +
                        $"foodSecurity(parent/daughter)={parent.LastFoodSecurity.ToDoubleForDisplay():F2}/{daughterSecurity} " +
                        $"adultHeadsPost(parent+daughter)={adultHeads} adultWorkThisSeason={adultWork.ToDoubleForDisplay():F2}");
                }
                bool terminalCheckpoint = IsFfaCaseControlTerminal(
                    match.EndOfMatch(humanSeat: -1).IsOver(runner.Tick), t + 150 >= ticks);
                if (completedSeason is 64 or 80 or 128 or 192 || terminalCheckpoint)
                {
                    IReadOnlyList<ResearchEvent> events = match.Research.Events;
                    int leaderAtCheckpoint = 0;
                    double leaderLegacy = double.MinValue;
                    for (int c = 0; c < civs; c++)
                    {
                        Standing standing = Scoring.Of(match, c);
                        double legacy = standing.Dominion.ToDoubleForDisplay() + standing.Mastery.ToDoubleForDisplay() +
                            standing.Firsts.ToDoubleForDisplay() + standing.Wonders.ToDoubleForDisplay();
                        if (legacy > leaderLegacy) { leaderLegacy = legacy; leaderAtCheckpoint = c; }
                    }
                    for (int c = 0; c < civs; c++)
                    {
                        if (terminalCheckpoint && c != leaderAtCheckpoint && archetypes[c] != EconomyArchetype.Builder) continue;
                        int population = 0, settlements = 0;
                        int fissions = 0;
                        Fix64 foodSecurity = Fix64.Zero;
                        for (int s = 0; s < match.Economy.SettlementCount; s++)
                        {
                            if (match.Politics.SettlementCivOf(s) != c || match.Politics.IsFreeFolk(s)) continue;
                            SettlementView view = match.Economy.Settlement(s);
                            population += view.Population;
                            fissions += view.Fissions;
                            foodSecurity += view.LastFoodSecurity;
                            settlements++;
                        }
                        int firsts = 0;
                        for (int e = 0; e < events.Count; e++)
                            if (events[e].Kind == ResearchEventKind.FirstClaimed && events[e].Civ == c)
                                firsts += events[e].A;
                        Standing score = Scoring.Of(match, c);
                        Fix64 insight = match.Research.DiagnosticInsightGenerated(c);
                        outcome.FfaCaseControlTraces.Add($"A5_CASE_CHECKPOINT seed={seed} rotation={ffaRotation} " +
                            $"season={(terminalCheckpoint ? $"{completedSeason}:terminal" : completedSeason.ToString(CultureInfo.InvariantCulture))} " +
                            $"seat={archetypes[c]}/c{c} leaderAtCheckpoint={archetypes[leaderAtCheckpoint]}/c{leaderAtCheckpoint} " +
                            $"{(terminalCheckpoint ? $"terminalWinner={archetypes[leaderAtCheckpoint]}/c{leaderAtCheckpoint} " : string.Empty)}" +
                            $"population={population} settlements={settlements} " +
                            $"fissions={fissions} meanFoodSecurity={(settlements == 0 ? 0 : foodSecurity.ToDoubleForDisplay() / settlements):F2} " +
                            $"insightGenerated={insight.ToDoubleForDisplay():F2} completedProjects={match.Research.DoneCount(c)} " +
                            $"cumulativeFirstClaimPoints={firsts} D={score.Dominion.ToDoubleForDisplay():F1} " +
                            $"M={score.Mastery.ToDoubleForDisplay():F1} F={score.Firsts.ToDoubleForDisplay():F1} " +
                            $"Legacy={(score.Dominion + score.Mastery + score.Firsts + score.Wonders).ToDoubleForDisplay():F1}");
                    }
                }
            }
            if (captureFfaResearchTimeline && completedSeason <= 128 && completedSeason % 8 == 0)
            {
                IReadOnlyList<ResearchEvent> researchEvents = match.Research.Events;
                var intervalCompleted = new List<string>[civs];
                var intervalFirsts = new List<string>[civs];
                for (int c = 0; c < civs; c++) { intervalCompleted[c] = new(); intervalFirsts[c] = new(); }
                for (int e = ffaTraceEventStart; e < researchEvents.Count; e++)
                {
                    ResearchEvent ev = researchEvents[e];
                    if (ev.Civ >= civs || (uint)ev.Project >= (uint)catalog.Projects.Count) continue;
                    ProjectDef project = catalog.Projects[ev.Project];
                    int eventSeason = checked((int)(ev.Tick / LivingEconomyModel.SeasonTicks));
                    if (ev.Kind == ResearchEventKind.Completed)
                    {
                        intervalCompleted[ev.Civ].Add($"{project.Id}@s{eventSeason}");
                        if (project.Id == "KEY-1" && ffaKey1Season[ev.Civ] < 0) ffaKey1Season[ev.Civ] = eventSeason;
                        if (project.Id == "KEY-2" && ffaKey2Season[ev.Civ] < 0) ffaKey2Season[ev.Civ] = eventSeason;
                    }
                    else if (ev.Kind == ResearchEventKind.FirstClaimed)
                        intervalFirsts[ev.Civ].Add($"E{project.Era}:{project.Id}={ev.A}");
                }
                for (int c = 0; c < civs; c++)
                {
                    int population = 0, settlementCount = 0;
                    for (int s = 0; s < match.Economy.SettlementCount; s++)
                    {
                        if (match.Politics.SettlementCivOf(s) != c || match.Politics.IsFreeFolk(s)) continue;
                        population += match.Economy.Settlement(s).Population;
                        settlementCount++;
                    }
                    Fix64 insight = match.Research.DiagnosticInsightGenerated(c);
                    string insightDelta = (insight - ffaPriorInsight[c]).ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture);
                    ffaPriorInsight[c] = insight;
                    var lineageRows = new List<string>();
                    for (int lineage = 0; lineage < catalog.LineageCodes.Count; lineage++)
                    {
                        Fix64 total = match.Research.DiagnosticKnowHowGenerated(c, lineage);
                        Fix64 delta = total - ffaPriorKnowHow[c, lineage];
                        ffaPriorKnowHow[c, lineage] = total;
                        if (delta.Raw != 0) lineageRows.Add($"{catalog.LineageCodes[lineage]}={delta.ToDoubleForDisplay():F2}");
                    }
                    outcome.FfaResearchTimeline.Add($"A5_RESEARCH_TRACE seed={seed} rotation={ffaRotation} season={completedSeason} seat={archetypes[c]}/c{c} " +
                        $"pop={population} settlements={settlementCount} era={match.Research.Era(c)} insightGenerated={insightDelta} " +
                        $"knowHowGenerated=[{string.Join(',', lineageRows)}] KEY-1={(ffaKey1Season[c] < 0 ? "never" : $"s{ffaKey1Season[c]}")} " +
                        $"KEY-2={(ffaKey2Season[c] < 0 ? "never" : $"s{ffaKey2Season[c]}")} " +
                        $"completed=[{string.Join(',', intervalCompleted[c])}] firsts=[{string.Join(',', intervalFirsts[c])}]");
                }
                ffaTraceEventStart = researchEvents.Count;
            }
            if (fissionSiteDiagnostics is not null)
            {
                for (int f = 0; f < match.Economy.FigureCount; f++)
                {
                    FigureView figure = match.Economy.Figure(f);
                    if (figure.Id >= leadPriorNextFigureId && figure.Civilization < seats.Length)
                        leadBirthsWindow[figure.Civilization]++;
                    if (figure.Id >= fissionPriorNextFigureId && figure.Civilization < seats.Length &&
                        figure.SettlementId == fissionTrackedSettlement[figure.Civilization])
                        fissionBirthsWindow[figure.Civilization]++;
                }
                leadPriorNextFigureId = match.Economy.NextFigureId;
                fissionPriorNextFigureId = match.Economy.NextFigureId;
                for (int c = 0; c < seats.Length; c++)
                {
                    int settlementId = fissionTrackedSettlement[c];
                    if (settlementId < 0) continue;
                    int index = -1;
                    for (int s = 0; s < match.Economy.SettlementCount; s++)
                        if (match.Economy.Settlement(s).Id == settlementId) { index = s; break; }
                    if (index < 0) continue;
                    SettlementView view = match.Economy.Settlement(index);
                    var cohesion = match.Economy.CohesionStatus(index);
                    Fix64 capTrigger = cohesion.Live && cohesion.Points < 35 ? Fix64.Parse("0.75") : Fix64.Parse("0.85");
                    bool capPressure = Fix64.FromInt(view.Population) >= view.EffectivePopulationCap * capTrigger * match.Economy.FissionScaleOf(c);
                    bool winterPressure = season % 4 == 3 && Fix64.FromInt(view.Population) >= view.LateWinterFissionThreshold;
                    if (capPressure) fissionCapPressureWindow[c]++;
                    if (winterPressure) fissionWinterPressureWindow[c]++;
                    if (fissionFirstPendingSeason[c] < 0 && view.FissionPending) fissionFirstPendingSeason[c] = season;
                    if (view.Fissions > fissionPriorSplits[c] && fissionFirstSplitSeason[c] < 0) fissionFirstSplitSeason[c] = season;
                    fissionPriorSplits[c] = view.Fissions;
                    if (completedSeason % 4 != 0 && completedSeason != 128) continue;
                    string deathCounts = string.Join(',', Enum.GetValues<EconomyDeathCause>()
                        .Where(cause => fissionDeathsWindow[c, (int)cause] > 0)
                        .Select(cause => $"{cause}:{fissionDeathsWindow[c, (int)cause]}"));
                    fissionSiteDiagnostics.RecordTriggerTimeline(c,
                        $"FISSION_TRIGGER_TRACE seed={seed} mirror={(warlordSettlementTraceSwapped ? 1 : 0)} season={completedSeason} civ={c}/{seats[c].Name} parent=s{settlementId} " +
                        $"pop={view.Population} births4={fissionBirthsWindow[c]} deaths4=[{deathCounts}] ageBands={match.Economy.AgeBandPopulation(index, 0)}/{match.Economy.AgeBandPopulation(index, 1)}/{match.Economy.AgeBandPopulation(index, 2)} " +
                        $"foodSecurity={view.LastFoodSecurity.ToDoubleForDisplay():F2} caps(food/housing/group/effective)={view.FoodCap.ToDoubleForDisplay():F1}/{view.HousingCap.ToDoubleForDisplay():F1}/{view.GroupCap.ToDoubleForDisplay():F1}/{view.EffectivePopulationCap.ToDoubleForDisplay():F1} " +
                        $"thresholdScale={match.Economy.FissionScaleOf(c).ToDoubleForDisplay():F2} capPressureNow={capPressure} capPressureSeasons4={fissionCapPressureWindow[c]} " +
                        $"leanWinterPressureNow={winterPressure} leanWinterPressureSeasons4={fissionWinterPressureWindow[c]} pending={view.FissionPending} firstPendingSeason={(fissionFirstPendingSeason[c] < 0 ? "never" : fissionFirstPendingSeason[c].ToString())} " +
                        $"firstSplitSeason={(fissionFirstSplitSeason[c] < 0 ? "never" : fissionFirstSplitSeason[c].ToString())}");
                    fissionBirthsWindow[c] = fissionCapPressureWindow[c] = fissionWinterPressureWindow[c] = 0;
                    for (int cause = 0; cause < fissionDeathsWindow.GetLength(1); cause++) fissionDeathsWindow[c, cause] = 0;
                }
                if (completedSeason is 128 or 192 or 256 or 320 or 384 or 448 or 512)
                    CaptureFissionLeadCheckpoint(match, seats, catalog, completedSeason,
                        leadBirthsWindow, leadStarvationDeathsWindow, warlordSettlementTraceSwapped,
                        fissionSiteDiagnostics);
            }
            if (a2LeadCheckpointEnabled && (completedSeason % 64 == 0 || t + 150 >= ticks))
            {
                CaptureA2LeadCheckpoint(match, seats, catalog, completedSeason, t + 150 >= ticks,
                    warlordSettlementTraceSwapped, a2LeadFirstClaimEventStart, outcome.A2LeadCheckpointTraces);
                a2LeadFirstClaimEventStart = match.Research.Events.Count;
            }
            if (checkpointDemographyTrace && (completedSeason % 64 == 0 || t + 150 >= ticks))
                CaptureDemographyCheckpoint(match, seats, catalog, completedSeason,
                    checkpointBirths, checkpointDeathCauses, outcome.WarlordDemographyTraces);
            if (checkpointDemographyTrace && (completedSeason % 64 == 0 || t + 150 >= ticks))
            {
                checkpointPriorNextFigureId = match.Economy.NextFigureId;
                checkpointBirthFigureIds.Clear();
                Array.Clear(checkpointBirths);
            }

            if (traceSettlementTimeline)
            {
                int traceSeason = season + 1;
                var starvationBySettlement = new Dictionary<int, int>();
                var starvationDetailsBySettlement = new Dictionary<int, List<string>>();
                for (int d = 0; d < match.Economy.LastDeathEventCount; d++)
                {
                    EconomyDeathEvent death = match.Economy.LastDeathEvent(d);
                    if (death.Cause == EconomyDeathCause.Starvation)
                    {
                        starvationBySettlement[death.SettlementId] = starvationBySettlement.GetValueOrDefault(death.SettlementId) + 1;
                        if (!starvationDetailsBySettlement.TryGetValue(death.SettlementId, out List<string>? details))
                            starvationDetailsBySettlement[death.SettlementId] = details = new List<string>();
                        details.Add($"f{death.FigureId}/age{death.Age}/h{death.StarvationHazard.ToDoubleForDisplay():F3}/winter{death.WinterShortfallHazard.ToDoubleForDisplay():F3}/total{death.TotalHazard.ToDoubleForDisplay():F3}");
                    }
                }
                var parents = new List<string>();
                int priorCount = settlementIdsBeforeAdvance.Length;
                var newSettlementIds = new Queue<int>();
                for (int n = priorCount; n < match.Economy.SettlementCount; n++)
                    newSettlementIds.Enqueue(match.Economy.Settlement(n).Id);
                for (int s = 0; s < Math.Min(priorCount, match.Economy.SettlementCount); s++)
                {
                    SettlementView before = match.Economy.Settlement(s);
                    int added = before.Fissions - settlementFissionsBeforeAdvance[s];
                    for (int n = 0; n < added; n++)
                    {
                        if (newSettlementIds.Count > 0)
                        {
                            int daughterId = newSettlementIds.Dequeue();
                            (int smallStock, int cattle, int pigs) = settlementHerdsBeforeAdvance![s];
                            parents.Add($"inferred:{before.Id}>{daughterId}(preStores={foodStoresBeforeAdvance[s].ToDoubleForDisplay():F1}," +
                                $"prePlots={settlementFieldsBeforeAdvance![s]}/{settlementOrchardsBeforeAdvance![s]}," +
                                $"preHerd={smallStock}/{cattle}/{pigs})");
                        }
                    }
                }
                var ownershipEvents = new List<string>();
                for (int s = 0; s < priorCount; s++)
                {
                    int id = settlementIdsBeforeAdvance[s];
                    int oldOwner = settlementOwnersBeforeAdvance[s];
                    int currentOwner = -1;
                    for (int n = 0; n < match.Economy.SettlementCount; n++)
                        if (match.Economy.Settlement(n).Id == id) { currentOwner = match.Politics.SettlementCivOf(n); break; }
                    if (currentOwner >= 0 && currentOwner != oldOwner)
                        ownershipEvents.Add($"capture:s{id}:c{oldOwner}>c{currentOwner}");
                    else if (currentOwner < 0) ownershipEvents.Add($"lost:s{id}:c{oldOwner}");
                }
                var records = new List<string>();
                for (int s = 0; s < match.Economy.SettlementCount; s++)
                {
                    SettlementView settlement = match.Economy.Settlement(s);
                    int civ = match.Politics.SettlementCivOf(s);
                    if (match.Politics.IsFreeFolk(s)) continue;
                    int deaths = starvationBySettlement.GetValueOrDefault(settlement.Id);
                    string starvationDetails = starvationDetailsBySettlement.TryGetValue(settlement.Id, out List<string>? details)
                        ? string.Join(',', details)
                        : string.Empty;
                    FoodIncomeBreakdown income = match.Economy.SustainableFoodIncomeBreakdown(s);
                    (int smallStock, int cattle, int pigs) = match.Economy.DomesticStock(s);
                    records.Add($"s{settlement.Id}/c{civ}:{seats[civ].Name}/tile={settlement.SiteTile}/p{settlement.Population}" +
                        $"/foodsec={settlement.LastFoodSecurity.ToDoubleForDisplay():F2}/stores={settlement.FoodStores.ToDoubleForDisplay():F1}" +
                        $"/preEat={match.Economy.LastSeasonFoodStoresBeforeConsumption(s).ToDoubleForDisplay():F1}" +
                        $"/cap={settlement.FoodCap.ToDoubleForDisplay():F1}/sustain={match.Economy.SustainableFoodCapacity(s).ToDoubleForDisplay():F1}" +
                        $"/income={income.Total.ToDoubleForDisplay():F1}[{income.Plants.ToDoubleForDisplay():F1},{income.Meat.ToDoubleForDisplay():F1},{income.Fish.ToDoubleForDisplay():F1},{income.Grain.ToDoubleForDisplay():F1},{income.Fruit.ToDoubleForDisplay():F1}]" +
                        $"/plots={match.Economy.FieldCount(s)}/{match.Economy.OrchardCount(s)}/herd={smallStock}/{cattle}/{pigs}/starve={deaths}[{starvationDetails}]");
                }
                outcome.SettlementTimelineTraces.Add($"SETTLE_TRACE seed={seed} mirror={(warlordSettlementTraceSwapped ? 1 : 0)} season={traceSeason} settlements=[{string.Join(';', records)}] fission=[{string.Join(',', parents)}] events=[{string.Join(',', ownershipEvents)}]");
            }

            if (captureFoundFunnelDiagnostics)
                for (int c = 0; c < civs; c++)
                {
                    if (!string.Equals(seats[c].Name, "warlord", StringComparison.OrdinalIgnoreCase)) continue;
                    int key2 = catalog.IndexOf("KEY-2");
                    int key3 = catalog.IndexOf("KEY-3");
                    ProjectDef key3Def = catalog.Projects[key3];
                    int[] slots = new int[CivResearch.SlotCount];
                    for (int slot = 0; slot < slots.Length; slot++) slots[slot] = match.Research.Slot(c, slot);
                    int[] queue = match.Research.Queue(c).ToArray();
                    int[] plan = match.Ai?.SeatOf(c)?.Spark is ResearchSpark spark ? spark.LastPlan.ToArray() : Array.Empty<int>();
                    var planned = new HashSet<int>(plan);
                    bool[]? closure = catalog.ClosureOf(2);
                    int[] availableClosureNotPlanned = closure is null
                        ? Array.Empty<int>()
                        : Enumerable.Range(0, closure.Length).Where(project => closure[project] &&
                            match.Research.State(c, project) == ProjectState.Available && !planned.Contains(project)).ToArray();
                    var prerequisiteStates = key3Def.Requires.Select(project =>
                        (project, match.Research.State(c, project), match.Research.Progress(c, project))).ToArray();
                    outcome.WarlordResearchSnapshots.Add(new WarlordResearchSeasonSnapshot(c, match.Politics.Season,
                        match.Research.State(c, key3), match.Research.Progress(c, key3), match.Research.EffectiveCost(c, key3),
                        match.Research.IsDone(c, key3), match.Research.Era(c), match.Research.State(c, key2),
                        match.Research.Progress(c, key2), match.Research.IsDone(c, key2), match.Research.InsightPerYear(c),
                        match.Research.DoneCount(c), match.Research.DoneInEra(c, key3Def.BreadthEra),
                        slots, queue, plan, prerequisiteStates, availableClosureNotPlanned));
                }

            if (captureFirstRaceDiagnostics)
                for (int e = researchEventsBeforeAdvance; e < match.Research.Events.Count; e++)
                {
                    ResearchEvent researchEvent = match.Research.Events[e];
                    if (researchEvent.Kind != ResearchEventKind.FirstClaimed || (uint)researchEvent.Civ >= (uint)civs ||
                        !string.Equals(seats[researchEvent.Civ].Name, "rusher", StringComparison.OrdinalIgnoreCase)) continue;
                    ProjectDef project = catalog.Projects[researchEvent.Project];
                    string lineage = project.Lineage >= 0 && project.Lineage < catalog.LineageCodes.Count
                        ? catalog.LineageCodes[project.Lineage]
                        : "none";
                    outcome.FirstRaceClaims.Add(new FirstRaceClaimDiagnostic(researchEvent.Project, project.Id, project.Era,
                        lineage, researchEvent.Civ, researchEvent.Tick, firstRaceHistory.SelectMany(static x => x).ToArray()));
                }

            for (int c = 0; c < civs; c++)
            {
                outcome.Civs[c].PostureSeasons[(int)match.Politics.PostureOf(c)]++;
                if (seats[c].Control.Kind == ControlKind.Ai && match.Ai?.SeatOf(c) is { } aiSeat)
                {
                    if (!match.Research.IsDone(c, gov02)) outcome.Civs[c].PostureGateLockedSeasons++;
                    if (aiSeat.Context.Intent.FrontierValue >= StrategistTuning.FrontierOpen)
                        outcome.Civs[c].FrontierOpenSeasons++;
                    if (aiSeat.Control.Spec.Level == Difficulty.Warlord)
                    {
                        outcome.Civs[c].WarlordUrgencyBands[WarlordUrgencyBand(aiSeat.Context.Intent.DefenseUrgency)]++;
                        outcome.Civs[c].WarlordUrgencySeasons++;
                        if (aiSeat.Strategist is Strategist strategist)
                        {
                            if (strategist.Plan == GrandPlan.Conquer) outcome.Civs[c].ConquerPlanSeasons++;
                            if (strategist.Plan == GrandPlan.FrontierRace) outcome.Civs[c].FrontierRacePlanSeasons++;
                        }
                    }
                }
            }
            if (match.Economy.SettlementCount > fissionsSeen.Length)
                Array.Resize(ref fissionsSeen, match.Economy.SettlementCount);
            for (int s = 0; s < match.Economy.SettlementCount; s++)
            {
                SettlementView settlement = match.Economy.Settlement(s);
                int owner = match.Politics.SettlementCivOf(s);
                if ((uint)owner < (uint)civs && !match.Politics.IsFreeFolk(s))
                {
                    if (settlement.FissionPending) outcome.Civs[owner].FissionPendingSeasons++;
                    if (settlement.Fissions > fissionsSeen[s])
                        outcome.Civs[owner].FissionSplits += settlement.Fissions - fissionsSeen[s];
                }
                fissionsSeen[s] = settlement.Fissions;
            }

            // A7 counts only deaths attributed to the conditional winter food-shortfall hazard, not the unconditional
            // baseline starvation hazard. Food sufficiency is sampled after the actual season-start redistribution
            // and before seasonal production or consumption; nearby stores without an eligible GOV-04 transfer do not count.
            for (int d = 0; d < match.Economy.LastDeathEventCount; d++)
            {
                EconomyDeathEvent death = match.Economy.LastDeathEvent(d);
                if (death.Cause != EconomyDeathCause.Starvation || (uint)death.Civilization >= (uint)civs ||
                    seats[death.Civilization].Control.Kind != ControlKind.Ai) continue;
                bool shortfallAttributed = match.Economy.IsLastDeathAttributedToWinterShortfall(d);
                if (!shortfallAttributed) continue;
                int own = -1;
                for (int s = 0; s < settlementIdsBeforeAdvance.Length; s++)
                    if (settlementIdsBeforeAdvance[s] == death.SettlementId) { own = s; break; }
                bool sufficientFood = own >= 0 && M10TelemetryRules.HasSufficientSeasonFood(
                    match.Economy.LastSeasonReachableFoodStores(own), match.Economy.LastSeasonFoodNeed(own));
                if (M10TelemetryRules.IsAvoidableStarvationDeath(death, shortfallAttributed,
                        sufficientFood))
                    outcome.Civs[death.Civilization].AvoidableStarvationDeaths++;
            }

            // Herd crashes count only with the simulation's direct hunt-to-zero attribution marker.
            for (int h = 0; h < match.Economy.WildHerdCount; h++)
            {
                int hunter = match.Economy.WildHerd(h).HuntedToExtinctionByCiv;
                if (!herdCrashReported[h] && M10TelemetryRules.IsAttributedHerdCrash(hunter, hunter, civs) &&
                    seats[hunter].Control.Kind == ControlKind.Ai)
                {
                    outcome.Civs[hunter].AttributedHerdCrashes++;
                    herdCrashReported[h] = true;
                }
            }

            // Siege events preserve the launch baseline even when desertion ends the investment in this advance.
            for (int e = politicsEventsBefore; e < match.Politics.Events.Count; e++)
            {
                PoliticsEvent ev = match.Politics.Events[e];
                if (ev.Kind == PoliticsEventKind.InvestmentTelemetryStarted && ev.Civ < civs &&
                    seats[ev.Civ].Control.Kind == ControlKind.Ai)
                    siegeBlunderTracker.BeginFromEvent(ev.Civ, ev.A, ev.B);
                else if (ev.Kind == PoliticsEventKind.InvestmentDeserted && ev.Civ < civs &&
                         seats[ev.Civ].Control.Kind == ControlKind.Ai &&
                         siegeBlunderTracker.ObserveDesertionEvent(ev.Civ, ev.A, ev.B))
                    outcome.Civs[ev.Civ].SiegeWithoutSupplyBlunders++;
            }

            // §14 §10.4 unwalled frontier: trigger solely from the AI's current belief snapshot. A threat is
            // a rival whose bronze route and warriors were sighted within four hexes of one of our settlements.
            // Once triggered, four seasons without a fortification response counts once for that rival site.
            if (match.Ai is { } frontierAi)
                for (int c = 0; c < civs; c++)
                {
                    AiSeat? frontierSeat = frontierAi.SeatOf(c);
                    if (frontierSeat?.Control.Kind != ControlKind.Ai) continue;
                    foreach (RivalBelief rival in frontierSeat.Belief.Rivals)
                    {
                        bool bronzeKnown = M10TelemetryRules.BelievesBronze(rival, met04, met05);
                        foreach (BeliefSettlement sighting in rival.Settlements)
                        {
                            if ((uint)sighting.Settlement >= (uint)world.TileCount) continue;
                            int site = sighting.Settlement;
                            int nearestOwn = -1, nearestDistance = int.MaxValue;
                            for (int own = 0; own < match.Economy.SettlementCount; own++)
                            {
                                if (match.Politics.SettlementCivOf(own) != c) continue;
                                int distance = HexGrid.Distance(sighting.Tile, match.Politics.SettlementTileOf(own), world.Width);
                                if (distance < nearestDistance) { nearestDistance = distance; nearestOwn = own; }
                            }
                            bool withinReach = nearestOwn >= 0 && nearestDistance <= 4;
                            bool freshlySeen = sighting.LastSeenSeason == match.Politics.Season;
                            bool threat = freshlySeen && M10TelemetryRules.IsUnwalledFrontierThreat(
                                bronzeKnown, sighting.WarriorsSeen > 0, withinReach);
                            if (frontierThreatSeason[c, site] == -1 && threat)
                            {
                                Defenses defenses = match.Politics.MilitaryOf(nearestOwn).Defenses;
                                if (!M10TelemetryRules.HasTimelyDefense(defenses))
                                {
                                    frontierThreatSeason[c, site] = match.Politics.Season;
                                    if (a7ThreatObservedSeason[c, nearestOwn] < 0)
                                        a7ThreatObservedSeason[c, nearestOwn] = match.Politics.Season;
                                    frontierThreatOwnSettlement[c, site] = nearestOwn;
                                }
                            }
                            int triggered = frontierThreatSeason[c, site];
                            if (triggered < 0 || frontierResponseSeen[c, site]) continue;
                            int target = frontierThreatOwnSettlement[c, site];
                            if (target >= 0 && target < match.Politics.SettlementTotal &&
                                match.Politics.SettlementCivOf(target) == c &&
                                M10TelemetryRules.HasTimelyDefense(match.Politics.MilitaryOf(target).Defenses))
                            {
                                frontierResponseSeen[c, site] = true;
                                a7DefenseResponseSeen[c, target] = true;
                            }
                            if (M10TelemetryRules.IsUnwalledFrontierBlunder(true,
                                    frontierResponseSeen[c, site], match.Politics.Season - triggered))
                            {
                                outcome.Civs[c].UnwalledFrontierBlunders++;
                                frontierThreatSeason[c, site] = -2; // recorded
                            }
                        }
                    }
                }

            // A7 counts one captured settlement after a known, unanswered threat at least two seasons old.
            for (int e = politicsEventsBefore; e < match.Politics.Events.Count; e++)
            {
                PoliticsEvent ev = match.Politics.Events[e];
                if (ev.Kind != PoliticsEventKind.Captured || (uint)ev.Civ >= (uint)civs ||
                    seats[ev.Civ].Control.Kind != ControlKind.Ai || (uint)ev.A >= (uint)world.TileCount ||
                    match.Politics.SettlementCivOf(ev.A) == ev.Civ || undefendedLossReported[ev.Civ, ev.A]) continue;
                if (M10TelemetryRules.IsUndefendedLossForSettlement(
                    a7ThreatObservedSeason, a7DefenseResponseSeen, ev.Civ, ev.A, match.Politics.Season))
                {
                    outcome.Civs[ev.Civ].UndefendedLosses++;
                    undefendedLossReported[ev.Civ, ev.A] = true;
                }
            }

            var positiveDecreeCandidate = new bool[civs];
            var paidDecreeSelected = new bool[civs];
            var paidDecreeResolved = new bool[civs];
            var recoveryTinOrder = new bool[civs];
            var recoveryArsenicOrder = new bool[civs];
            for (int e = politicsEventsBefore; e < match.Politics.Events.Count; e++)
            {
                PoliticsEvent ev = match.Politics.Events[e];
                if (ev.Kind != PoliticsEventKind.DecreeResolved || (uint)ev.Civ >= (uint)civs ||
                    ev.A is < (int)DecreeKind.Found or > (int)DecreeKind.Compact) continue;
                paidDecreeResolved[ev.Civ] |= match.Politics.DecreeCost(ev.Civ, (DecreeKind)ev.A) > 0;
            }
            if (match.Ai is { } aiSystem)
                for (int i = aiLogEntriesBefore; i < aiSystem.Log.Entries.Count; i++)
                {
                    AiDecision decision = aiSystem.Log.Entries[i];
                    if (decision.Kind == "DecreeCandidate" && (uint)decision.Civ < (uint)civs &&
                        M10B30Rules.IsPositivePaidDecreeCandidate(decision.Score > Fix64.Zero,
                            match.Politics.DecreeCost(decision.Civ, (DecreeKind)decision.Subject)))
                        positiveDecreeCandidate[decision.Civ] = true;
                    if (decision.Kind != "Decree" || (uint)decision.Civ >= (uint)civs) continue;
                    DecreeKind kind = (DecreeKind)decision.Subject;
                    if (match.Politics.DecreeCost(decision.Civ, kind) > 0)
                        paidDecreeSelected[decision.Civ] = true;
                    if (kind == DecreeKind.Compact) recoveryTinOrder[decision.Civ] = true; // the current policy's compact is for Tin.
                    if (kind == DecreeKind.Mine && (uint)decision.Other < (uint)world.TileCount)
                    {
                        DepositKind deposit = world.TileAt(decision.Other).Deposit;
                        if (deposit == DepositKind.Tin) recoveryTinOrder[decision.Civ] = true;
                        if (deposit == DepositKind.ArsenicalCopper) recoveryArsenicOrder[decision.Civ] = true;
                    }
                }

            // B28: first incoming raid by a rival the defender already believed had a bronze route.
            for (int e = politicsEventsBefore; e < match.Politics.Events.Count; e++)
            {
                PoliticsEvent ev = match.Politics.Events[e];
                if (!M10TelemetryRules.IsSettlementRaidLaunch(ev)) continue;
                outcome.B28SettlementRaidLaunches++;
                if (ev.Other < 0 || ev.Other >= civs || ev.Civ >= civs ||
                    seats[ev.Other].Control.Kind != ControlKind.Ai || seats[ev.Civ].Control.Kind != ControlKind.Ai) continue;
                outcome.B28AiSettlementRaidLaunches++;
                if (match.Research.IsDone(ev.Civ, met04) || match.Research.IsDone(ev.Civ, met05))
                    outcome.B28ActualBronzeRaidLaunches++;
                if (bronzeRaidBelieved[ev.Other][ev.Civ]) outcome.B28BelievedBronzeRaidLaunches++;
                bool attackerHadActualBronze = actualMet04BeforeAdvance[ev.Civ] || actualMet05BeforeAdvance[ev.Civ];
                bool firstQualifyingRaid = !outcome.Civs[ev.Other].BelievedBronzeRaidMeasured && bronzeRaidBelieved[ev.Other][ev.Civ];
                if (warlordB28Trace && firstQualifyingRaid && ev.Other == 0 && ev.Civ == 2 && ev.B == 35)
                {
                    int raidSeason = checked((int)(ev.Tick / LivingEconomyModel.SeasonTicks));
                    int firstDefenseQueueSeason = match.Ai!.Log.Entries
                        .Where(d => d.Civ == ev.Other && d.Kind == "DefenseBuild" && d.Subject == ev.B)
                        .Select(d => d.Season).DefaultIfEmpty(-1).Min();
                    int beliefSeason = b28FirstBeliefSeason[ev.Other, ev.Civ];
                    int beliefLead = beliefSeason < 0 ? -1 : raidSeason - beliefSeason;
                    int queuedLead = firstDefenseQueueSeason < 0 ? -1 : raidSeason - firstDefenseQueueSeason;
                    outcome.B28Traces.Add($"B28 shadow s35-raid-lead seed={seed} defender=civ{ev.Other} attacker=civ{ev.Civ} " +
                        $"raidSeason={raidSeason} firstBeliefSeason={beliefSeason} firstAppearanceSeason={s35FirstAppearanceSeason} " +
                        $"hypotheticalQueueAtFirstBeliefLead={beliefLead} actualDefenseQueueSeason={firstDefenseQueueSeason} " +
                        $"actualQueueLead={queuedLead} {FormatB28ShadowSettlement(35, ev.Other, raidSeason, world.Width, match, s35FirstAppearanceSeason)}");
                }
                if (warlordB28Trace && firstQualifyingRaid && b28Timeline.TryGetValue((ev.Other, ev.Civ), out List<string>? timeline))
                {
                    foreach (string row in timeline)
                        outcome.B28Traces.Add($"B28 timeline seed {seed} defender=civ{ev.Other} attacker=civ{ev.Civ} {row}");
                    b28Timeline.Remove((ev.Other, ev.Civ));
                }
                if (warlordB28Trace && attackerHadActualBronze)
                {
                    string preLaunchDefense = (uint)ev.B < (uint)defensesBeforeAdvance.Length
                        ? defensesBeforeAdvance[ev.B].ToString()
                        : "unknown";
                    int lastSeenSeason = bronzeRivalLastSeenSeason[ev.Other][ev.Civ];
                    int firstContactSeason = bronzeRivalFirstContactSeason[ev.Other][ev.Civ];
                    int lastSeenAge = lastSeenSeason < 0 ? -1 : season - lastSeenSeason;
                    int contactAge = firstContactSeason < 0 ? -1 : season - firstContactSeason;
                    int seenFlags = bronzeRivalSeenFlags[ev.Other][ev.Civ];
                    var bronzeEvidence = new List<string>();
                    if ((seenFlags & SeenFlag.CopperKit) != 0) bronzeEvidence.Add("copper-kit");
                    if ((seenFlags & SeenFlag.ArsenicalKit) != 0) bronzeEvidence.Add("arsenical-kit");
                    if ((seenFlags & SeenFlag.TinBronzeKit) != 0) bronzeEvidence.Add("tin-bronze-kit");
                    if ((seenFlags & SeenFlag.ArsenicalMineCamp) != 0) bronzeEvidence.Add("arsenical-mine-camp");
                    if ((seenFlags & SeenFlag.TinMineCamp) != 0) bronzeEvidence.Add("tin-mine-camp");
                    outcome.B28Traces.Add($"B28 actual-bronze-attack seed {seed} attacker={seats[ev.Civ].Name}/civ{ev.Civ} " +
                        $"defender={seats[ev.Other].Name}/civ{ev.Other} target=s{ev.B} " +
                        $"attacker MET-04/05={actualMet04BeforeAdvance[ev.Civ]}/{actualMet05BeforeAdvance[ev.Civ]} " +
                        $"preLaunchBelief MET-04/05={bronzeRaidBelievedMet04[ev.Other][ev.Civ]}/{bronzeRaidBelievedMet05[ev.Other][ev.Civ]} " +
                        $"everSeenBronzeEvidence={(bronzeEvidence.Count == 0 ? "none" : string.Join(',', bronzeEvidence))}; " +
                        $"lastVisualSeenSeason={lastSeenSeason} age={lastSeenAge}; " +
                        $"contacted={bronzeRivalContacted[ev.Other][ev.Civ]} firstContactSeason={firstContactSeason} age={contactAge}; " +
                        $"preLaunchDefense={preLaunchDefense} firstQualifyingRaid={firstQualifyingRaid}");
                }
                if (!M10TelemetryRules.TryMeasureBelievedBronzeRaid(
                        outcome.Civs[ev.Other].BelievedBronzeRaidMeasured,
                        bronzeRaidBelieved[ev.Other][ev.Civ], ev.B, defensesBeforeAdvance,
                        out bool defendedBeforeRaid)) continue;
                if (warlordB28Trace)
                {
                    var decisions = match.Ai!.Log.Entries
                        .Where(d => d.Civ == ev.Other && d.Tick <= ev.Tick && d.Kind is "Plan" or "Defense" or "Project")
                        .TakeLast(5)
                        .Select(d => $"{d.Season}:{d.Kind}:{d.Reason}");
                    int def03 = catalog.IndexOf("DEF-03"), def04 = catalog.IndexOf("DEF-04"), def05 = catalog.IndexOf("DEF-05");
                    string defensesBefore = (uint)ev.B < (uint)defensesBeforeAdvance.Length
                        ? defensesBeforeAdvance[ev.B].ToString()
                        : "unknown";
                    outcome.B28Traces.Add($"B28 seed {seed} {seats[ev.Other].Name} civ{ev.Other} vs {seats[ev.Civ].Name} civ{ev.Civ} " +
                        $"season {season} tick {ev.Tick} settlement {ev.B}: believed MET-04/05=" +
                        $"{bronzeRaidBelievedMet04[ev.Other][ev.Civ]}/{bronzeRaidBelievedMet05[ev.Other][ev.Civ]}, " +
                        $"attacker completed MET-04/05={match.Research.IsDone(ev.Civ, met04)}/{match.Research.IsDone(ev.Civ, met05)}, " +
                        $"DEF-03/04/05={match.Research.IsDone(ev.Other, def03)}/{match.Research.IsDone(ev.Other, def04)}/{match.Research.IsDone(ev.Other, def05)}, " +
                        $"before={defensesBefore}, after={match.Politics.MilitaryOf(ev.B).Defenses}; " +
                        $"recent AI decisions [{string.Join(" | ", decisions)}]");
                }
                CivOutcome defender = outcome.Civs[ev.Other];
                defender.BelievedBronzeRaidMeasured = true;
                defender.DefendedBeforeBelievedBronzeRaid = defendedBeforeRaid;
            }

            // Pair only actual settlement launches with the AI's launch decision, then score those forecasts
            // when the corresponding raid resolves. A threshold violation is therefore launch-based, while
            // calibration is measured independently against the realized result.
            for (int e = politicsEventsBefore; e < match.Politics.Events.Count; e++)
            {
                PoliticsEvent ev = match.Politics.Events[e];
                if (M10TelemetryRules.IsSettlementRaidLaunch(ev) && ev.Civ < civs &&
                    seats[ev.Civ].Control.Kind == ControlKind.Ai && match.Ai is { } launchAi)
                {
                    for (int i = aiLogEntriesBefore; i < launchAi.Log.Entries.Count; i++)
                    {
                        AiDecision decision = launchAi.Log.Entries[i];
                        if (decision.Kind != "Raid" || decision.Civ != ev.Civ || decision.Other != ev.Other ||
                            decision.Subject != ev.B) continue;
                        if (M10TelemetryRules.IsSuicideRaidLaunch(ev, decision)) outcome.Civs[ev.Civ].SuicideRaids++;
                        var key = (ev.Civ, ev.Other);
                        if (!pendingRaidPredictions.TryGetValue(key, out Queue<Fix64>? queue))
                            pendingRaidPredictions.Add(key, queue = new Queue<Fix64>());
                        queue.Enqueue(decision.Score);
                        CivOutcome attacker = outcome.Civs[ev.Civ];
                        attacker.RaidPredictionsLaunched++;
                        attacker.PredictedRaidSuccess += decision.Score.ToDoubleForDisplay();
                        break;
                    }
                }
                if (ev.Kind != PoliticsEventKind.RaidResolved || ev.A is not (0 or 1) || ev.Civ >= civs ||
                    seats[ev.Civ].Control.Kind != ControlKind.Ai) continue;
                var resolvedKey = (ev.Civ, ev.Other);
                if (!pendingRaidPredictions.TryGetValue(resolvedKey, out Queue<Fix64>? predictions) || predictions.Count == 0) continue;
                Fix64 predicted = predictions.Dequeue();
                CivOutcome resolved = outcome.Civs[ev.Civ];
                resolved.RaidResolvedPredictions++;
                resolved.RaidResolvedPredictedSuccess += predicted.ToDoubleForDisplay();
                if (ev.A == 1) resolved.RaidObservedSuccesses++;
                Fix64 observed = ev.A == 1 ? Fix64.One : Fix64.Zero;
                resolved.RaidCalibrationAbsoluteError += Fix64.Abs(predicted - observed).ToDoubleForDisplay();
            }

            // B30: at first observation, record whether each tin caravan is hostile and has fewer than two escorts.
            foreach (Expedition caravan in match.Politics.Caravans())
            {
                if ((uint)caravan.Civ >= (uint)civs || seats[caravan.Civ].Control.Kind != ControlKind.Ai) continue;
                if (nakedTinCaravans[caravan.Civ].Observe(caravan.Id, caravan.Good,
                        match.Politics.CaravanCrossesHostileTerritory(caravan), caravan.EscortCount))
                    outcome.Civs[caravan.Civ].NakedCaravans++;
            }

            for (int c = 0; c < civs; c++)
            {
                if (seats[c].Control.Kind == ControlKind.Council) continue;
                CivOutcome o = outcome.Civs[c];
                if (seats[c].Control.Kind == ControlKind.Ai)
                {
                    if (match.Research.IsDone(c, keys[2]) && key3Season[c] < 0)
                        key3Season[c] = (int)(match.Research.CompletedTick(c, keys[2]) / 150);
                    if (key3Season[c] >= 0 && !bronzePoorMeasured[c] && match.Politics.Season >= key3Season[c] + 8)
                    {
                        bronzePoorMeasured[c] = true;
                        o.TinPoorEightSeasonsAfterKey3 = !match.Economy.CivHasMetalAccess(c, MetalAccessKind.Tin) ? 1 : 0;
                        if (o.TinPoorEightSeasonsAfterKey3 != 0) o.TinPoorCheckpointSeason = match.Politics.Season;
                    }
                    fallbackRouteAfterTinPoor[c] = M10TelemetryRules.LatchFallbackRoute(bronzePoorMeasured[c],
                        fallbackRouteAfterTinPoor[c], match.Economy.CivHasMetalAccess(c, MetalAccessKind.Tin),
                        match.Economy.CivHasMetalAccess(c, MetalAccessKind.Arsenic));

                    bool tinOption = match.Politics.NearestMineSite(c, DepositKind.Tin) >= 0 || match.Politics.TinSellerFor(c) != int.MinValue;
                    bool arsenicOption = match.Research.IsDone(c, min04) && match.Politics.NearestMineSite(c, DepositKind.ArsenicalCopper) >= 0;
                    ProjectState met04State = match.Research.State(c, met04);
                    ProjectState met05State = match.Research.State(c, met05);
                    bool tinAccess = match.Economy.CivHasMetalAccess(c, MetalAccessKind.Tin);
                    bool arsenicAccess = match.Economy.CivHasMetalAccess(c, MetalAccessKind.Arsenic);
                    bool hasAnyMetalAccess = tinAccess || arsenicAccess;
                    bool dormantOption = !hasAnyMetalAccess &&
                        (met05State == ProjectState.Dormant && tinOption || met04State == ProjectState.Dormant && arsenicOption);
                    bool recoveryAction = dormantOption &&
                        (tinOption && recoveryTinOrder[c] || arsenicOption && recoveryArsenicOrder[c]);
                    dormantOptionSeasons[c] = dormantOption && !recoveryAction ? dormantOptionSeasons[c] + 1 : 0;
                    if (M10TelemetryRules.IsDormantRouteBlunder(met04State, arsenicOption, met05State, tinOption,
                            hasAnyMetalAccess, dormantOptionSeasons[c], recoveryAction) && !dormantReported[c])
                    {
                        o.DormantBronzeBlunders++;
                        dormantReported[c] = true;
                    }

                    if (warlordB27Trace && o.TinPoorEightSeasonsAfterKey3 != 0 &&
                        string.Equals(seats[c].Name, "warlord", StringComparison.OrdinalIgnoreCase))
                    {
                        AiDecision[] decisions = match.Ai?.Log.Entries
                            .Skip(aiLogEntriesBefore)
                            .Where(d => d.Civ == c && d.Layer == "Strategist" &&
                                (d.Kind == "Plan" || d.Kind == "Decree" || d.Kind == "DecreeCandidate"))
                            .ToArray() ?? Array.Empty<AiDecision>();
                        string plan = decisions.Where(d => d.Kind == "Plan")
                            .Select(d => d.Reason).LastOrDefault() ?? string.Empty;
                        string selected = string.Join(";", decisions.Where(d => d.Kind == "Decree")
                            .Select(d => $"{(DecreeKind)d.Subject}@{d.Other} EV={d.Score.ToDoubleForDisplay():F2}"));
                        string CandidateLabel(AiDecision decision)
                        {
                            DecreeKind kind = (DecreeKind)decision.Subject;
                            string deposit = kind == DecreeKind.Mine && (uint)decision.Other < (uint)world.TileCount
                                ? $"/{world.TileAt(decision.Other).Deposit}" : string.Empty;
                            return $"{kind}{deposit}@{decision.Other} EV={decision.Score.ToDoubleForDisplay():F2} {decision.Reason}";
                        }
                        string candidates = string.Join(";", decisions
                            .Where(d => d.Kind == "DecreeCandidate" &&
                                (DecreeKind)d.Subject is DecreeKind.Mine or DecreeKind.Levy or DecreeKind.Compact)
                            .OrderByDescending(d => d.Score)
                            .Take(3)
                            .Select(CandidateLabel));
                        string traceState = $"{tinAccess}/{arsenicAccess}/{tinOption}/{arsenicOption}/{met04State}/{met05State}";
                        bool traceStateChanged = !string.Equals(lastB27TraceState[c], traceState, StringComparison.Ordinal);
                        lastB27TraceState[c] = traceState;
                        if (match.Politics.Season == o.TinPoorCheckpointSeason || traceStateChanged ||
                            plan.Length > 0 || selected.Length > 0 || candidates.Length > 0)
                            outcome.B27Traces.Add($"B27_TRACE seed={seed} civ={c} season={match.Politics.Season} " +
                                $"checkpoint={o.TinPoorCheckpointSeason} metal(tin={tinAccess},ars={arsenicAccess}) " +
                                $"option(tin={tinOption},ars={arsenicOption}) MET04={met04State} MET05={met05State} " +
                                $"dormantStreak={dormantOptionSeasons[c]} recoveryAction={recoveryAction} " +
                                $"plan=[{plan}] selected=[{selected}] topRouteCandidates=[{candidates}]");
                    }

                    o.MandateSeasonsAtCap = match.Politics.MandateSeasonsAtCap(c);
                    // Measure true overflow at the start of the season. A paid Decree can lower Mandate before
                    // this season's recharge, so an end-of-season cap reading alone would count useful spending.
                    mandateOverflowStreak[c] = M10B30Rules.AdvanceMandateOverflowStreak(mandateOverflowStreak[c],
                        positiveDecreeCandidate[c], paidDecreeResolved[c], mandateBeforeAdvance[c], mandateCapBeforeAdvance[c]);
                    if (warlordB30Trace && string.Equals(seats[c].Name, "warlord", StringComparison.OrdinalIgnoreCase) &&
                        positiveDecreeCandidate[c] && mandateCapBeforeAdvance[c] > 0 &&
                        mandateBeforeAdvance[c] >= mandateCapBeforeAdvance[c])
                    {
                        AiDecision[] seasonDecisions = match.Ai!.Log.Entries
                            .Skip(aiLogEntriesBefore)
                            .Where(decision => decision.Civ == c)
                            .ToArray();
                        AiDecision[] paidCandidateDecisions = seasonDecisions
                            .Where(decision => decision.Kind == "DecreeCandidate" && decision.Score > Fix64.Zero &&
                                match.Politics.DecreeCost(c, (DecreeKind)decision.Subject) > 0)
                            .ToArray();
                        string[] paidCandidates = paidCandidateDecisions
                            .OrderByDescending(decision => decision.Score)
                            .Take(5)
                            .Select(decision => $"{(DecreeKind)decision.Subject}@{decision.Other}:cost={match.Politics.DecreeCost(c, (DecreeKind)decision.Subject)},ev={decision.Score.ToDoubleForDisplay():F2}")
                            .ToArray();
                        AiDecision[] positiveFoundDecisions = seasonDecisions
                            .Where(decision => decision.Kind == "DecreeCandidate" && decision.Subject == (int)DecreeKind.Found &&
                                decision.Score > Fix64.Zero)
                            .OrderByDescending(decision => decision.Score)
                            .Take(3)
                            .ToArray();
                        string[] foundCandidates = positiveFoundDecisions
                            .Select(decision => $"target={decision.Other},ev={decision.Score.ToDoubleForDisplay():F2}")
                            .ToArray();
                        string[] reserves = seasonDecisions
                            .Where(decision => decision.Kind == "DecreeReserve")
                            .Select(decision => $"{(DecreeKind)decision.Subject}: {decision.Reason}")
                            .ToArray();
                        string[] selected = seasonDecisions
                            .Where(decision => decision.Kind == "Decree")
                            .Select(decision => $"{(DecreeKind)decision.Subject}@{decision.Other}:cost={match.Politics.DecreeCost(c, (DecreeKind)decision.Subject)},ev={decision.Score.ToDoubleForDisplay():F2}")
                            .ToArray();
                        string[] decreeEvents = match.Politics.Events
                            .Skip(politicsEventsBefore)
                            .Where(ev => ev.Civ == c && ev.Kind is PoliticsEventKind.DecreeResolved or PoliticsEventKind.DecreeRefused)
                            .Select(ev => ev.Kind == PoliticsEventKind.DecreeResolved
                                ? $"resolved:{(DecreeKind)ev.A}:subject={ev.B}"
                                : $"refused:{(DecreeKind)ev.A}:{(OrderRefusalReason)ev.B}")
                            .ToArray();
                        outcome.WarlordMandateTraces.Add(
                            $"B30_TRACE seed={seed} mirror={(warlordSettlementTraceSwapped ? 1 : 0)} civ={c} season={season + 1} " +
                            $"streak={mandateOverflowStreak[c]} mandate={mandateBeforeAdvance[c]}->{match.Research.Mandate(c)}/cap={mandateCapBeforeAdvance[c]} " +
                            $"paid={paidCandidateDecisions.Length}[{string.Join(';', paidCandidates)}] paidSelected={paidDecreeSelected[c]} paidResolved={paidDecreeResolved[c]} " +
                            $"positiveFound={positiveFoundDecisions.Length}[{string.Join(';', foundCandidates)}] " +
                            $"reserved=[{string.Join(';', reserves)}] selected=[{string.Join(';', selected)}] events=[{string.Join(';', decreeEvents)}]");
                    }
                    if (mandateOverflowStreak[c] > 8 && !mandateOverflowReported[c])
                    {
                        o.MandateOverflowBlunders++;
                        mandateOverflowReported[c] = true;
                    }
                }
                // Keystone stall: breadth met and the keystone Available, but unslotted two seasons running.
                for (int k = 0; k < 3; k++)
                {
                    ProjectDef def = catalog.Projects[keys[k]];
                    bool waiting = !match.Research.IsDone(c, keys[k]) && match.Research.State(c, keys[k]) == ProjectState.Available;
                    if (waiting && stalled[c * 3 + k]) { o.KeystoneStalls++; stalled[c * 3 + k] = false; }
                    else stalled[c * 3 + k] = waiting;
                }
                // Idle research slot while a project could take it.
                bool anyEmpty = false;
                for (int s = 0; s < CivResearch.SlotCount; s++) if (match.Research.Slot(c, s) < 0) anyEmpty = true;
                if (anyEmpty && HasAvailable(match.Research, catalog, c)) o.IdleSlotSeasons++;

                if (seats[c].Control.Kind == ControlKind.Ai)
                {
                    int settlementCount = match.Economy.SettlementCount;
                    var adultWorkers = new int[settlementCount];
                    var idleAdults = new int[settlementCount];
                    for (int f = 0; f < match.Economy.FigureCount; f++)
                    {
                        FigureView figure = match.Economy.Figure(f);
                        if (figure.Civilization != c || figure.Age < 14 || figure.Age >= 45 ||
                            (uint)figure.SettlementId >= (uint)settlementCount ||
                            intentionallyIdleFigureIds.Contains(figure.Id)) continue;
                        adultWorkers[figure.SettlementId]++;
                        if (figure.WorkThisSeason <= Fix64.Zero) idleAdults[figure.SettlementId]++;
                    }
                    bool idleLaborBlunder = false;
                    for (int settlementId = 0; settlementId < settlementCount; settlementId++)
                    {
                        SettlementView settlement = match.Economy.Settlement(settlementId);
                        bool localFoodDeficit = settlement.Civilization == c && settlement.LastFoodSecurity < Fix64.One;
                        if (!M10TelemetryRules.IsIdleLaborBlunder(
                                idleAdults[settlementId], adultWorkers[settlementId], localFoodDeficit)) continue;
                        if (!idleLaborBlunder)
                        {
                            o.IdleLaborSeasons++;
                            idleLaborBlunder = true;
                        }
                        var idlePeople = new List<string>();
                        for (int f = 0; f < match.Economy.FigureCount; f++)
                        {
                            FigureView figure = match.Economy.Figure(f);
                            if (figure.Civilization != c || figure.SettlementId != settlementId ||
                                figure.Age < 14 || figure.Age >= 45 || figure.WorkThisSeason > Fix64.Zero ||
                                intentionallyIdleFigureIds.Contains(figure.Id)) continue;
                            idlePeople.Add($"id{figure.Id}/s{figure.SettlementId}/age{figure.Age}/health{figure.Health}/hunger{figure.Hunger}/warmth{figure.Warmth}/rest{figure.Rest}/job{figure.Job}");
                        }
                        outcome.A7Traces.Add($"A7 idle-labor seed={seed} season={season + 1} civ={c}: " +
                            $"settlement s{settlementId} idle {idleAdults[settlementId]}/{adultWorkers[settlementId]}, " +
                            $"food security {settlement.LastFoodSecurity}, " +
                            $"zero-work figures [{string.Join(';', idlePeople)}]");
                    }
                }
            }

            // Stop at the Bronze chapter horizon after recording the closing season's telemetry.
            outcome.SeasonsProcessed++;
            if (match.EndOfMatch(humanSeat: -1).IsOver(runner.Tick)) break;
        }

        PoliticsSystem pol = match.Politics;
        for (int c = 0; c < civs; c++)
            if (match.Ai?.SeatOf(c)?.Strategist is Strategist strategist)
                outcome.Civs[c].ThreeSeasonNonConquerRaidPostureOpportunities =
                    strategist.ThreeSeasonNonConquerRaidPostureOpportunitiesForDiagnostics;
        foreach (PoliticsEvent e in pol.Events)
        {
            if (warlordSettlementTrace && (uint)e.Civ < (uint)civs &&
                string.Equals(seats[e.Civ].Name, "warlord", StringComparison.OrdinalIgnoreCase) &&
                e.Kind is PoliticsEventKind.WarDeclared or PoliticsEventKind.AssaultResolved or
                    PoliticsEventKind.OccupationBroken or PoliticsEventKind.Captured)
                outcome.WarlordCombatTraces.Add($"WARLORD_COMBAT_EVENT seed={seed} mirror={(warlordSettlementTraceSwapped ? 1 : 0)} " +
                    $"season={e.Tick / ResearchSystem.SeasonTicks} civ={e.Civ} {e.Kind} other={e.Other} A={e.A} B={e.B}");
            if (e.Kind == PoliticsEventKind.RaidLaunched && e.B >= 0 &&
                (uint)e.Civ < (uint)civs && (uint)e.Other < (uint)civs &&
                string.Equals(seats[e.Civ].Name, "rusher", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(seats[e.Other].Name, "warlord", StringComparison.OrdinalIgnoreCase))
            {
                CivOutcome target = outcome.Civs[e.Other];
                target.IncomingRusherRaidsLaunched++;
                target.IncomingRusherRaidPartyFighters += e.A;
            }
            else if (e.Kind == PoliticsEventKind.RaidResolved && e.A is 2 or 3 &&
                (uint)e.Civ < (uint)civs && (uint)e.Other < (uint)civs &&
                string.Equals(seats[e.Civ].Name, "warlord", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(seats[e.Other].Name, "rusher", StringComparison.OrdinalIgnoreCase))
            {
                CivOutcome target = outcome.Civs[e.Civ];
                target.IncomingRusherRaidsResolved++;
                if (e.A == 2) target.IncomingRusherRaidsSucceeded++;
            }

            if (e.Kind == PoliticsEventKind.DecreeResolved && e.A == (int)DecreeKind.Found && e.Civ < civs)
                outcome.Civs[e.Civ].FoundDecrees++;
            else if (e.Kind == PoliticsEventKind.Captured)
            {
                // Each capture is logged twice: the captor's event has B >= 0, the loser's B <= 0 (both 0 if nobody stayed, so
                // the final owner decides that case).
                bool captor = e.B > 0 || e.B == 0 && pol.SettlementCivOf(e.A) == e.Civ;
                if (captor && e.Other >= 0 && e.Other < civs && e.Other != e.Civ) { outcome.Civs[e.Other].SettlementsLost++; outcome.Civs[e.Civ].SettlementsCaptured++; }
            }
            // The politics event kind is also used for assault launches; AI decision notes distinguish them below.
            else if (M10TelemetryRules.IsSettlementRaidLaunch(e) && seats[e.Civ].Control.Kind != ControlKind.Ai)
                outcome.Civs[e.Civ].RaidsLaunched++;
            else if (e.Kind == PoliticsEventKind.RaidResolved && (e.A == 0 || e.A == 1))
            {
                outcome.Civs[e.Civ].RaidsResolved++;
                if (e.A == 1) outcome.Civs[e.Civ].RaidsWon++;
            }
            else if (e.Kind == PoliticsEventKind.AssaultResolved && (e.A == 0 || e.A == 1))
            {
                CivOutcome c = outcome.Civs[e.Civ];
                c.AssaultsResolved++;
                if (e.A == 1) c.AssaultsHeld++;
            }
            else if (e.Kind == PoliticsEventKind.OccupationBroken) outcome.Civs[e.Civ].OccupationsBroken++;
        }

        for (int c = 0; c < civs; c++)
            outcome.Civs[c].OwnedLandShareBasisPoints = pol.Territory.ShareBasisPoints(c);
        if (match.Ai is { } ai)
        {
            if (captureFoundFunnelDiagnostics)
                for (int c = 0; c < civs; c++)
                    if (string.Equals(seats[c].Name, "warlord", StringComparison.OrdinalIgnoreCase))
                    {
                        outcome.FoundFunnels.Add(SummarizeFoundFunnel(c, ai.Log.Entries, outcome.FoundFunnelSnapshots, match.Research));
                        outcome.WarlordResearchTraces.Add(SummarizeWarlordResearch(c, outcome.WarlordResearchSnapshots,
                            match.Research.Catalog));
                        if (outcome.WarlordEconomyTraces.Count == 0 ||
                            !outcome.WarlordEconomyTraces[^1].Contains($"civ {c} s{match.Politics.Season}:", StringComparison.Ordinal))
                            outcome.WarlordEconomyTraces.Add(FormatWarlordEconomySnapshot(match, c,
                                totalDeaths[c], starvationDeaths[c]));
                        int other = c == 0 ? 1 : 0;
                        FoundFrontierAudit audit = match.Politics.ProjectedFoundSiteFrontier(c, other, (candidateCiv, tile) =>
                        {
                            if (candidateCiv != c) return true;
                            Belief? belief = match.Ai?.SeatOf(candidateCiv)?.Belief;
                            return belief is not null && PoliticsM10.StrategistBeliefAllowsFoundCandidate(
                                match.Politics, belief, candidateCiv, tile);
                        });
                        foreach (FoundFrontierCivSummary civSummary in audit.Civilizations)
                            outcome.WarlordEconomyTraces.Add($"Found-site audit civ {civSummary.Civ}: execution-legal {civSummary.LegalSiteCount}, " +
                                $"Strategist candidates {civSummary.StrategistCandidateCount}, frontier-near {civSummary.RelevantSiteCount}, " +
                                $"projected {civSummary.ProjectedSiteCount}; gate {civSummary.AiFoundGateOpen}, " +
                                $"Mandate {civSummary.Mandate}/{civSummary.MandateCap}, natural E3 foundings {civSummary.NaturalFoundings}, " +
                                $"Found decrees {civSummary.FoundDecrees}");
                    }
            for (int c = 0; c < civs; c++)
                if (seats[c].Control.Kind == ControlKind.Ai)
                    outcome.Civs[c].PlanSwitchesInThreeYears = M10TelemetryRules.MaxOrdinaryPlanSwitchesInThreeYears(ai.Log.Entries, c);

            foreach (AiDecision d in ai.Log.Entries)
            {
                if ((uint)d.Civ >= (uint)civs || seats[d.Civ].Control.Kind != ControlKind.Ai) continue;
                if (warlordSettlementTrace && string.Equals(seats[d.Civ].Name, "warlord", StringComparison.OrdinalIgnoreCase) &&
                    d.Kind is "War" or "WarRejected" or "Assault" or "Captured")
                    outcome.WarlordCombatTraces.Add($"WARLORD_COMBAT_DECISION seed={seed} mirror={(warlordSettlementTraceSwapped ? 1 : 0)} " +
                        $"season={d.Season} civ={d.Civ} {d.Kind} target={d.Subject} other={d.Other} score={d.Score} {d.Reason}");
                CivOutcome c = outcome.Civs[d.Civ];
                if (d.Kind == "Raid")
                {
                    c.RaidsLaunched++;
                    c.RaidPredictions++;
                }
                else if (d.Kind == "Assault") { c.AssaultPredictions++; c.PredictedCaptureSuccess += d.Score.ToDoubleForDisplay(); c.AssaultsLaunched++; }
                else if (d.Kind == "War") c.WarsDeclared++;
                else if (d.Kind == "WarRejected")
                {
                    c.WarRejections++;
                    c.WarRejectionReasons.TryGetValue(d.Reason, out int count);
                    c.WarRejectionReasons[d.Reason] = count + 1;
                    if (d.Reason.Contains("too small to launch a campaign", StringComparison.Ordinal) &&
                        TryReadAlternateOriginAttackers(d.Reason, out int alternateAttackers))
                    {
                        if (alternateAttackers >= PoliticsSystem.CampaignMinimumAttackers) c.AlternateOriginCampaignRescues++;
                        else c.AlternateOriginCampaignNoRescues++;
                    }
                }
                else if (d.Kind == "Threat")
                {
                    string threat = $"civ {d.Civ} rival {d.Subject}: {d.Reason}";
                    c.ThreatReasons.TryGetValue(threat, out int count);
                    c.ThreatReasons[threat] = count + 1;
                }
                else if (d.Kind == "Posture" && d.Reason.Contains("raids have paid in expectation", StringComparison.Ordinal))
                    c.RaidStreakPostureTransitions++;
            }
        }
        foreach (Expedition expedition in pol.Expeditions)
        {
            if ((uint)expedition.Civ >= (uint)civs) continue;
            if (expedition.Kind == ExpeditionKind.Raid) outcome.Civs[expedition.Civ].RaidExpeditionsPending++;
            else if (expedition.Kind == ExpeditionKind.Assault) outcome.Civs[expedition.Civ].AssaultExpeditionsPending++;
        }
        foreach (Occupation occupation in pol.Occupations)
            if ((uint)occupation.Civ < (uint)civs) outcome.Civs[occupation.Civ].OccupationsPending++;
        for (int i = 0; i < match.Economy.SettlementCount; i++)
        {
            int owner = pol.SettlementCivOf(i);
            if (owner >= 0 && owner < civs && !pol.IsFreeFolk(i)) outcome.Civs[owner].Settlements++;
        }
        for (int c = 0; c < civs; c++)
        {
            CivOutcome o = outcome.Civs[c];
            Standing s = Scoring.Of(match, c);
            o.Dominion = s.Dominion.ToDoubleForDisplay();
            o.Mastery = s.Mastery.ToDoubleForDisplay();
            o.Firsts = s.Firsts.ToDoubleForDisplay();
            o.Wonders = s.Wonders.ToDoubleForDisplay();
            o.Projects = match.Research.DoneCount(c);
            if (match.Ai?.SeatOf(c)?.Spark is ResearchSpark spark)
            {
                o.SparkRollouts = spark.Rollouts;
                o.SparkDecisions = spark.Decisions;
                o.DefenseUrgencyShadowHighDecisions = spark.DefenseUrgencyShadowHighDecisionsForDiagnostics;
                o.DefenseUrgencyShadowSlotsChanged = spark.DefenseUrgencyShadowSlotsChangedForDiagnostics;
                o.DefenseUrgencyShadowDisplacedDefense = spark.DefenseUrgencyShadowDisplacedDefenseForDiagnostics;
                Array.Copy(spark.DefenseUrgencyShadowHighDecisionsByEraForDiagnostics, o.DefenseUrgencyShadowHighDecisionsByEra,
                    Math.Min(spark.DefenseUrgencyShadowHighDecisionsByEraForDiagnostics.Length, o.DefenseUrgencyShadowHighDecisionsByEra.Length));
            }
            if (seats[c].Control.Kind == ControlKind.Ai)
            {
                outcome.A7AiSeatMatches++;
                outcome.AvoidableStarvationDeaths += o.AvoidableStarvationDeaths;
                outcome.UndefendedLosses += o.UndefendedLosses;
                outcome.AttributedHerdCrashes += o.AttributedHerdCrashes;
                outcome.IdleLaborSeasons += o.IdleLaborSeasons;
                o.TinOrArsenicalRouteByChapterEnd = o.TinPoorEightSeasonsAfterKey3 != 0 && fallbackRouteAfterTinPoor[c];
            }
            for (int k = 0; k < 3; k++)
                if (match.Research.IsDone(c, keys[k])) o.KeyMinutes[k] = match.Research.CompletedTick(c, keys[k]) / 600.0;
        }
        outcome.FinalTick = runner.Tick;
        outcome.Checksum = runner.CurrentChecksum;
        if (captureFfaFirstsDiagnostic)
            foreach (ResearchEvent researchEvent in match.Research.Events)
                if (researchEvent.Kind == ResearchEventKind.FirstClaimed &&
                    (uint)researchEvent.Project < (uint)catalog.Projects.Count)
                {
                    ProjectDef project = catalog.Projects[researchEvent.Project];
                    outcome.FfaFirstClaims.Add(new FfaFirstClaimDiagnostic(researchEvent.Civ, researchEvent.Project,
                        project.Id, project.Era, researchEvent.A));
                }
        if (captureFfaResearchTimeline && ffaRotation >= 0)
            outcome.FfaResearchTimeline.Add($"A5_RESEARCH_RESULT {FormatFfaMatchDetail(seed, ffaRotation, archetypes, outcome.Civs)}");
        if (captureFfaFoodDemographyTimeline && ffaRotation >= 0)
            outcome.FfaFoodDemographyTraces.Add($"A5_FOOD_DEMO_RESULT {FormatFfaMatchDetail(seed, ffaRotation, archetypes, outcome.Civs)}");
        if (fissionSiteDiagnostics is not null)
        {
            string growerGov02Timing = string.Join(',', Enumerable.Range(0, seats.Length)
                .Where(c => string.Equals(seats[c].Name, "grower", StringComparison.OrdinalIgnoreCase))
                .Select(c => match.Research.IsDone(c, catalog.IndexOf("GOV-02"))
                    ? $"c{c}:s{match.Research.CompletedTick(c, catalog.IndexOf("GOV-02")) / LivingEconomyModel.SeasonTicks}"
                    : $"c{c}:never"));
            foreach (FissionSiteSelectionAttempt attempt in fissionSiteDiagnostics.Attempts)
            {
                if ((uint)attempt.Civ >= (uint)seats.Length ||
                    !string.Equals(seats[attempt.Civ].Name, "warlord", StringComparison.OrdinalIgnoreCase)) continue;
                outcome.WarlordFissionSiteDiagnostics.Add(FissionSiteDiagnostics.Format(attempt, seats[attempt.Civ].Name));
            }
            foreach (FissionResolutionAttempt attempt in fissionSiteDiagnostics.Resolutions)
            {
                if ((uint)attempt.Civ >= (uint)seats.Length ||
                    !string.Equals(seats[attempt.Civ].Name, "warlord", StringComparison.OrdinalIgnoreCase)) continue;
                outcome.WarlordFissionSiteDiagnostics.Add($"{attempt.Trace} growerGOV02=[{growerGov02Timing}]");
            }
            foreach (FissionTriggerTimelineRow row in fissionSiteDiagnostics.TriggerTimeline)
                if ((uint)row.Civ < (uint)seats.Length)
                    outcome.WarlordFissionSiteDiagnostics.Add($"{row.Trace} growerGOV02=[{growerGov02Timing}]");
            foreach (FissionLeadCheckpointRow row in fissionSiteDiagnostics.LeadCheckpoints)
                outcome.WarlordFissionSiteDiagnostics.Add(row.Trace);
        }
        return outcome;
    }

    internal static bool FoodShortageScoutSkipAppliesForDiagnostics(string seatName, bool chieftainOptIn)
        => string.Equals(seatName, "warlord", StringComparison.OrdinalIgnoreCase) ||
           (chieftainOptIn && string.Equals(seatName, "chieftain", StringComparison.OrdinalIgnoreCase));

    private static void CaptureFissionLeadCheckpoint(MatchModel match, Player[] seats, ResearchCatalog catalog,
        int season, int[] birthsByCiv, int[] starvationDeathsByCiv, bool mirror,
        FissionSiteDiagnostics diagnostics)
    {
        int gov02 = catalog.IndexOf("GOV-02");
        var teams = new[] { "warlord", "grower" };
        var metrics = new (double D, double M, double F, double W, int Settlements, int Population, int Fissions,
            int Births, int Starvation, double Insight, int Projects, string Gov02)[teams.Length];
        for (int teamIndex = 0; teamIndex < teams.Length; teamIndex++)
        {
            double dominion = 0, mastery = 0, firsts = 0, wonders = 0, insight = 0;
            int settlements = 0, population = 0, fissions = 0, births = 0, starvation = 0, projects = 0;
            var govStates = new List<string>();
            for (int civ = 0; civ < seats.Length; civ++)
            {
                if (!string.Equals(seats[civ].Name, teams[teamIndex], StringComparison.OrdinalIgnoreCase)) continue;
                Standing standing = Scoring.Of(match, civ);
                dominion += standing.Dominion.ToDoubleForDisplay();
                mastery += standing.Mastery.ToDoubleForDisplay();
                firsts += standing.Firsts.ToDoubleForDisplay();
                wonders += standing.Wonders.ToDoubleForDisplay();
                insight += match.Research.InsightPerYear(civ).ToDoubleForDisplay();
                projects += match.Research.DoneCount(civ);
                births += birthsByCiv[civ];
                starvation += starvationDeathsByCiv[civ];
                string completed = match.Research.IsDone(civ, gov02)
                    ? $"s{match.Research.CompletedTick(civ, gov02) / LivingEconomyModel.SeasonTicks}"
                    : "never";
                govStates.Add($"c{civ}:{match.Research.State(civ, gov02)}/{match.Research.Progress(civ, gov02).ToDoubleForDisplay():F1}/done{completed}");
                for (int s = 0; s < match.Economy.SettlementCount; s++)
                {
                    if (match.Politics.SettlementCivOf(s) != civ || match.Politics.IsFreeFolk(s)) continue;
                    SettlementView view = match.Economy.Settlement(s);
                    settlements++;
                    population += view.Population;
                    fissions += view.Fissions;
                }
            }
            metrics[teamIndex] = (dominion, mastery, firsts, wonders, settlements, population, fissions,
                births, starvation, insight, projects, string.Join(',', govStates));
        }
        double warlordLegacy = metrics[0].D + metrics[0].M + metrics[0].F + metrics[0].W;
        double growerLegacy = metrics[1].D + metrics[1].M + metrics[1].F + metrics[1].W;
        double legacyDelta = warlordLegacy - growerLegacy;
        for (int i = 0; i < teams.Length; i++)
        {
            var m = metrics[i];
            diagnostics.RecordLeadCheckpoint(-1,
                $"A2_LEAD_CHECKPOINT seed=20261024 mirror={(mirror ? 1 : 0)} season={season} team={teams[i]} " +
                $"score(D/M/F/W/L)={m.D:F1}/{m.M:F1}/{m.F:F1}/{m.W:F1}/{m.D + m.M + m.F + m.W:F1} " +
                $"warlordMinusGrowerL={legacyDelta:F1} settlements={m.Settlements} population={m.Population} fissions={m.Fissions} " +
                $"birthsSinceStartOrCheckpoint={m.Births} starvationSinceStartOrCheckpoint={m.Starvation} insightPerYear={m.Insight:F1} projects={m.Projects} GOV02=[{m.Gov02}]");
        }
        Array.Clear(birthsByCiv);
        Array.Clear(starvationDeathsByCiv);
    }

    private static void CaptureA2LeadCheckpoint(MatchModel match, Player[] seats, ResearchCatalog catalog,
        int season, bool terminal, bool mirror, int firstClaimEventStart, List<string> output)
    {
        var teams = new[] { "warlord", "grower" };
        var rows = new string[teams.Length];
        var populations = new int[teams.Length];
        var settlementCounts = new int[teams.Length];
        var works = new double[teams.Length];
        var insights = new double[teams.Length];
        var projects = new int[teams.Length];
        var masteries = new double[teams.Length];
        var firsts = new double[teams.Length];
        var claimPoints = new int[teams.Length];

        for (int teamIndex = 0; teamIndex < teams.Length; teamIndex++)
        {
            var eras = new List<string>();
            var slots = new List<string>();
            var claims = new List<string>();
            double insightPerYear = 0, mastery = 0, firstsScore = 0;
            int doneProjects = 0, claimDelta = 0;
            for (int civ = 0; civ < seats.Length; civ++)
            {
                if (!string.Equals(seats[civ].Name, teams[teamIndex], StringComparison.OrdinalIgnoreCase)) continue;
                eras.Add($"c{civ}:E{match.Research.Era(civ)}");
                var civSlots = new List<string>();
                for (int slot = 0; slot < CivResearch.SlotCount; slot++)
                {
                    int project = match.Research.Slot(civ, slot);
                    civSlots.Add(project < 0 ? "-" : catalog.Projects[project].Id);
                }
                slots.Add($"c{civ}:[{string.Join(',', civSlots)}]");
                int civPopulation = 0;
                Fix64 civWork = Fix64.Zero;
                for (int s = 0; s < match.Economy.SettlementCount; s++)
                {
                    if (match.Politics.SettlementCivOf(s) != civ || match.Politics.IsFreeFolk(s)) continue;
                    SettlementView view = match.Economy.Settlement(s);
                    civPopulation += view.Population;
                    civWork += match.Economy.DiagnosticEffectiveWorkCapacity(s);
                    settlementCounts[teamIndex]++;
                }
                populations[teamIndex] += civPopulation;
                works[teamIndex] += civWork.ToDoubleForDisplay();
                insightPerYear += match.Research.InsightPerYear(civ).ToDoubleForDisplay();
                doneProjects += match.Research.DoneCount(civ);
                mastery += Scoring.Of(match, civ).Mastery.ToDoubleForDisplay();
                firstsScore += Scoring.Of(match, civ).Firsts.ToDoubleForDisplay();
            }
            IReadOnlyList<ResearchEvent> events = match.Research.Events;
            for (int e = firstClaimEventStart; e < events.Count; e++)
            {
                ResearchEvent ev = events[e];
                if (ev.Kind != ResearchEventKind.FirstClaimed || ev.Project < 0 || ev.Project >= catalog.Projects.Count ||
                    ev.Civ < 0 || ev.Civ >= seats.Length ||
                    !string.Equals(seats[ev.Civ].Name, teams[teamIndex], StringComparison.OrdinalIgnoreCase)) continue;
                ProjectDef project = catalog.Projects[ev.Project];
                claimDelta += ev.A;
                claims.Add($"E{project.Era}:{project.Id}={ev.A}");
            }
            insights[teamIndex] = insightPerYear;
            projects[teamIndex] = doneProjects;
            masteries[teamIndex] = mastery;
            firsts[teamIndex] = firstsScore;
            claimPoints[teamIndex] = claimDelta;
            rows[teamIndex] = $"{teams[teamIndex]}{{pop={populations[teamIndex]} settlements={settlementCounts[teamIndex]} " +
                $"effectiveWork={works[teamIndex]:F1} InsightPerYear={insightPerYear:F1} era=[{string.Join(',', eras)}] " +
                $"slots=[{string.Join(';', slots)}] projects={doneProjects} mastery={mastery:F1} firsts={firstsScore:F1} " +
                $"FirstClaimDelta={claimDelta} claims=[{string.Join(',', claims)}]}}";
        }
        output.Add($"A2_LEAD_CHECKPOINT seed=20261025 mirror={(mirror ? 1 : 0)} season={season}{(terminal ? ":terminal" : string.Empty)} " +
            $"{rows[0]} {rows[1]} delta(warlord-grower)[pop={populations[0] - populations[1]} settlements={settlementCounts[0] - settlementCounts[1]} " +
            $"effectiveWork={works[0] - works[1]:F1} InsightPerYear={insights[0] - insights[1]:F1} projects={projects[0] - projects[1]} " +
            $"mastery={masteries[0] - masteries[1]:F1} firsts={firsts[0] - firsts[1]:F1} FirstClaimDelta={claimPoints[0] - claimPoints[1]}]");
    }

    private static FoundFunnelDiagnostic SummarizeFoundFunnel(int civ, IReadOnlyList<AiDecision> decisions,
        IReadOnlyList<FoundFunnelSeasonSnapshot> snapshots, ResearchSystem research)
    {
        var candidateSeasons = new HashSet<int>();
        var foundCandidateSeasons = new HashSet<int>();
        var positiveFoundCandidateSeasons = new HashSet<int>();
        var foundSelectedSeasons = new HashSet<int>();
        var otherSelectedSeasons = new HashSet<int>();
        foreach (AiDecision decision in decisions)
        {
            if (decision.Civ != civ || decision.Layer != "Strategist") continue;
            if (decision.Kind == "DecreeCandidate")
            {
                candidateSeasons.Add(decision.Season);
                if ((DecreeKind)decision.Subject == DecreeKind.Found)
                {
                    foundCandidateSeasons.Add(decision.Season);
                    if (decision.Score > Fix64.Zero) positiveFoundCandidateSeasons.Add(decision.Season);
                }
            }
            else if (decision.Kind == "Decree")
            {
                if ((DecreeKind)decision.Subject == DecreeKind.Found) foundSelectedSeasons.Add(decision.Season);
                else otherSelectedSeasons.Add(decision.Season);
            }
        }
        int positiveFoundPassedSeasons = positiveFoundCandidateSeasons.Count(otherSelectedSeasons.Contains);
        FoundFunnelSeasonSnapshot[] states = snapshots.Where(snapshot => snapshot.Civ == civ).ToArray();
        int councilOn = states.Count(snapshot => snapshot.CouncilOn);
        int mandateUnlocked = states.Count(snapshot => snapshot.MandateUnlocked);
        int both = states.Count(snapshot => snapshot.CouncilOn && snapshot.MandateUnlocked);
        int activeNoFound = states.Count(snapshot => snapshot.CouncilOn && snapshot.MandateUnlocked &&
            !foundCandidateSeasons.Contains(snapshot.Season));
        int key3 = research.Catalog.IndexOf("KEY-3");
        ProjectDef key3Def = research.Catalog.Projects[key3];
        string missingKey3 = string.Join(",", key3Def.Requires.Where(project => !research.IsDone(civ, project))
            .Select(project => research.Catalog.Projects[project].Id));
        string key3Path = string.Join(",", key3Def.Requires.Select(project =>
            $"{research.Catalog.Projects[project].Id}:{research.State(civ, project)}:{research.Progress(civ, project).ToDoubleForDisplay():F1}"));
        return new FoundFunnelDiagnostic(civ, candidateSeasons.Count, foundCandidateSeasons.Count,
            positiveFoundCandidateSeasons.Count, foundSelectedSeasons.Count, positiveFoundPassedSeasons,
            states.Length, councilOn, mandateUnlocked, both,
            states.Count(snapshot => snapshot.MandateUnlocked && snapshot.Mandate >= snapshot.FoundCost),
            states.Count(snapshot => snapshot.MandateUnlocked && snapshot.Mandate >= snapshot.MandateCap),
            states.Count(snapshot => snapshot.FoundQuotaRemaining > 0),
            states.Count(snapshot => snapshot.NaturalHeadroom >= 0), activeNoFound,
            states.Length == 0 ? 0 : (int)Math.Round(states.Average(snapshot => snapshot.Mandate)),
            states.Length == 0 ? 0 : (int)Math.Round(states.Average(snapshot => snapshot.MandateCap)),
            states.Length == 0 ? 0 : (int)Math.Round(states.Average(snapshot => snapshot.FoundCost)),
            missingKey3, key3Path, research.DoneInEra(civ, key3Def.BreadthEra), research.State(civ, key3).ToString(), research.Progress(civ, key3));
    }

    private static string FormatWarlordEconomySnapshot(MatchModel match, int civ, int deaths, int starvationDeaths)
    {
        int settlements = 0, population = 0, adults = 0, pending = 0, fissions = 0;
        Fix64 fill = Fix64.Zero, foodSecurity = Fix64.Zero, lateWinter = Fix64.Zero, cohesion = Fix64.Zero;
        for (int s = 0; s < match.Economy.SettlementCount; s++)
        {
            if (match.Politics.SettlementCivOf(s) != civ || match.Politics.IsFreeFolk(s)) continue;
            SettlementView view = match.Economy.Settlement(s);
            if (view.Population <= 0) continue;
            settlements++;
            population += view.Population;
            adults += match.Economy.AgeBandPopulation(s, 1);
            if (view.FissionPending) pending++;
            fissions += view.Fissions;
            fill += match.Economy.PopulationFill(s);
            foodSecurity += view.LastFoodSecurity;
            lateWinter += view.LateWinterFoodForecast;
            cohesion += match.Economy.SettlementCohesion(s);
        }
        Fix64 divisor = Fix64.FromInt(Math.Max(1, settlements));
        return $"Warlord economy trace civ {civ} s{match.Politics.Season}: settlements {settlements}, population/adults {population}/{adults}, " +
            $"mean fill/food security/late-winter/cohesion {(fill / divisor).ToDoubleForDisplay():F2}/" +
            $"{(foodSecurity / divisor).ToDoubleForDisplay():F2}/{(lateWinter / divisor).ToDoubleForDisplay():F2}/" +
            $"{(cohesion / divisor).ToDoubleForDisplay():F2}, fission pending {pending}, cumulative fissions {fissions}, " +
            $"natural E3 foundings {match.Politics.E3Foundings(civ)}, deaths/starvation {deaths}/{starvationDeaths}";
    }

    private static void CaptureDemographyCheckpoint(MatchModel match, Player[] seats, ResearchCatalog catalog,
        int checkpoint, int[] birthsByCiv, int[,] deathsByCause, List<string> output)
    {
        int gov02 = catalog.IndexOf("GOV-02");
        for (int civ = 0; civ < seats.Length; civ++)
        {
            string name = seats[civ].Name;
            if (!string.Equals(name, "warlord", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(name, "rusher", StringComparison.OrdinalIgnoreCase)) continue;
            int settlements = 0, population = 0, pending = 0, completed = 0, pressureCount = 0;
            Fix64 foodCap = Fix64.Zero, housingCap = Fix64.Zero, groupCap = Fix64.Zero;
            Fix64 foodStores = Fix64.Zero, foodSecurity = Fix64.Zero;
            var settlementRows = new List<string>();
            for (int s = 0; s < match.Economy.SettlementCount; s++)
            {
                if (match.Politics.SettlementCivOf(s) != civ || match.Politics.IsFreeFolk(s)) continue;
                SettlementView view = match.Economy.Settlement(s);
                settlements++;
                population += view.Population;
                foodCap += view.FoodCap;
                housingCap += view.HousingCap;
                groupCap += view.GroupCap;
                foodStores += view.FoodStores;
                foodSecurity += view.LastFoodSecurity;
                if (view.FissionPending) pending++;
                completed += view.Fissions;
                CivicReadout civic = match.Economy.Civic(s);
                (bool cohesionLive, int cohesion) = match.Economy.CohesionStatus(s);
                Fix64 thresholdCap = civic.Markets > 0 ? Fix64.Min(view.FoodCap, view.GroupCap) :
                    Fix64.Min(view.FoodCap, Fix64.Min(view.HousingCap, view.GroupCap));
                Fix64 trigger = cohesionLive && cohesion < 35 ? Fix64.Parse("0.75") : Fix64.Parse("0.85");
                Fix64 normalThreshold = thresholdCap * trigger * match.Economy.FissionScaleOf(civ);
                bool pressure = Fix64.FromInt(view.Population) >= normalThreshold ||
                    checkpoint % 4 == 3 && Fix64.FromInt(view.Population) >= view.LateWinterFissionThreshold;
                bool candidate = pressure && Fix64.FromInt(view.Population) * Fix64.Parse("0.4") >= Fix64.FromInt(6) &&
                    !view.FissionPending && match.Economy.SeasonsSinceFission(s) >= 4 &&
                    !match.Politics.IsFreeFolk(s);
                if (pressure && !view.FissionPending) pressureCount++;
                settlementRows.Add($"s{view.Id}:pop{view.Population}/caps{view.FoodCap.ToDoubleForDisplay():F0},{view.HousingCap.ToDoubleForDisplay():F0},{view.GroupCap.ToDoubleForDisplay():F0}" +
                    $"/food{view.FoodStores.ToDoubleForDisplay():F1}/sec{view.LastFoodSecurity.ToDoubleForDisplay():F2}" +
                    $"/fissionPressure{pressure}/candidate{candidate}/pending{view.FissionPending}/done{view.Fissions}");
            }
            int deaths = 0;
            string causeCounts = string.Join(',', Enum.GetValues<EconomyDeathCause>().Select(cause =>
            {
                int count = (int)cause <= (int)EconomyDeathCause.Senescence ? deathsByCause[civ, (int)cause] : 0;
                deaths += count;
                return $"{cause}:{count}";
            }));
            string govPrereqs = string.Join(',', catalog.Projects[gov02].Requires.Select(p =>
                $"{catalog.Projects[p].Id}:{match.Research.State(civ, p)}"));
            output.Add($"A3 demographic checkpoint civ={civ} seat={name} season={checkpoint} settlements={settlements} population={population} " +
                $"births={birthsByCiv[civ]} deaths={deaths}[{causeCounts}] caps(food/housing/group)={foodCap.ToDoubleForDisplay():F1}/{housingCap.ToDoubleForDisplay():F1}/{groupCap.ToDoubleForDisplay():F1} " +
                $"meanFoodSecurity={(foodSecurity / Fix64.FromInt(Math.Max(1, settlements))).ToDoubleForDisplay():F2} foodStores={foodStores.ToDoubleForDisplay():F1} " +
                $"fissionPressureSites={pressureCount} pending={pending} completed={completed} InsightPerYear={match.Research.InsightPerYear(civ).ToDoubleForDisplay():F1} " +
                $"GOV-02={match.Research.State(civ, gov02)} prereqs=[{govPrereqs}] sites=[{string.Join(';', settlementRows)}]");
            for (int cause = 0; cause <= (int)EconomyDeathCause.Senescence; cause++) deathsByCause[civ, cause] = 0;
        }
    }

    private static WarlordResearchPlanDiagnostic SummarizeWarlordResearch(int civ,
        IReadOnlyList<WarlordResearchSeasonSnapshot> snapshots, ResearchCatalog catalog)
    {
        WarlordResearchSeasonSnapshot[] states = snapshots.Where(snapshot => snapshot.Civ == civ).ToArray();
        if (states.Length == 0) return new WarlordResearchPlanDiagnostic(civ, "no season snapshots", "");

        int key3 = catalog.IndexOf("KEY-3");
        WarlordResearchSeasonSnapshot first = states[0], last = states[^1];
        Fix64 maxKeyProgress = states.Max(static snapshot => snapshot.Key3Progress);
        int key3SlotSeasons = states.Count(snapshot => snapshot.Slots.Contains(key3));
        int key3QueueSeasons = states.Count(snapshot => snapshot.Queue.Contains(key3));
        int key3PlanSeasons = states.Count(snapshot => snapshot.Plan.Contains(key3));
        int? key2DoneSeason = states.FirstOrDefault(static snapshot => snapshot.Key2Done).Season;
        if (key2DoneSeason == 0 && !states.Any(static snapshot => snapshot.Key2Done)) key2DoneSeason = null;
        int? firstE2CompletionSeason = states.FirstOrDefault(static snapshot => snapshot.E2BreadthDone > 0).Season;
        if (firstE2CompletionSeason == 0 && !states.Any(static snapshot => snapshot.E2BreadthDone > 0)) firstE2CompletionSeason = null;
        int? breadth14Season = FirstSeasonAtBreadth(states, 14);
        int? breadth17Season = FirstSeasonAtBreadth(states, 17);
        int? breadth20Season = FirstSeasonAtBreadth(states, catalog.Projects[key3].BreadthMin);
        string[] prerequisiteSummary = catalog.Projects[key3].Requires.Select(project =>
        {
            WarlordResearchSeasonSnapshot[] withProject = states.Where(snapshot =>
                snapshot.Prerequisites.Any(prerequisite => prerequisite.Project == project)).ToArray();
            var final = withProject[^1].Prerequisites.Single(prerequisite => prerequisite.Project == project);
            int slotSeasons = withProject.Count(snapshot => snapshot.Slots.Contains(project));
            int queueSeasons = withProject.Count(snapshot => snapshot.Queue.Contains(project));
            int planSeasons = withProject.Count(snapshot => snapshot.Plan.Contains(project));
            int? firstAvailable = withProject.FirstOrDefault(snapshot =>
                snapshot.Prerequisites.Single(prerequisite => prerequisite.Project == project).State is
                    ProjectState.Available or ProjectState.Slotted or ProjectState.Completed).Season;
            if (firstAvailable == 0 && !withProject.Any(snapshot =>
                snapshot.Prerequisites.Single(prerequisite => prerequisite.Project == project).State is
                    ProjectState.Available or ProjectState.Slotted or ProjectState.Completed)) firstAvailable = null;
            int? completed = withProject.FirstOrDefault(snapshot =>
                snapshot.Prerequisites.Single(prerequisite => prerequisite.Project == project).State == ProjectState.Completed).Season;
            if (completed == 0 && !withProject.Any(snapshot =>
                snapshot.Prerequisites.Single(prerequisite => prerequisite.Project == project).State == ProjectState.Completed)) completed = null;
            Fix64 firstProgress = withProject[0].Prerequisites.Single(prerequisite => prerequisite.Project == project).Progress;
            Fix64 maxProgress = withProject.Max(snapshot =>
                snapshot.Prerequisites.Single(prerequisite => prerequisite.Project == project).Progress);
            int availableOffPlan = withProject.Count(snapshot =>
                snapshot.Prerequisites.Single(prerequisite => prerequisite.Project == project).State == ProjectState.Available &&
                !snapshot.Plan.Contains(project));
            return $"{catalog.Projects[project].Id} {firstAvailable?.ToString() ?? "n/a"}→{completed?.ToString() ?? "n/a"} s; progress {firstProgress.ToDoubleForDisplay():F1}→{final.Progress.ToDoubleForDisplay():F1} (max {maxProgress.ToDoubleForDisplay():F1}); slot {slotSeasons}, queue {queueSeasons}, plan {planSeasons}, available off-plan {availableOffPlan}; final {final.State}";
        }).ToArray();

        var omittedClosureCounts = new Dictionary<int, int>();
        foreach (WarlordResearchSeasonSnapshot snapshot in states)
            foreach (int project in snapshot.AvailableClosureNotPlanned)
            {
                omittedClosureCounts.TryGetValue(project, out int count);
                omittedClosureCounts[project] = count + 1;
            }
        string omittedClosure = omittedClosureCounts.Count == 0
            ? "none"
            : string.Join(", ", omittedClosureCounts.OrderByDescending(static pair => pair.Value)
                .ThenBy(pair => catalog.Projects[pair.Key].Id, StringComparer.Ordinal).Take(8)
                .Select(pair => $"{catalog.Projects[pair.Key].Id}:{pair.Value}"));
        string summary = $"Era {first.Era}→{last.Era}, KEY-2 done s{key2DoneSeason?.ToString() ?? "n/a"}; E2 breadth {first.E2BreadthDone}→{last.E2BreadthDone}, first E2 completion s{firstE2CompletionSeason?.ToString() ?? "n/a"}, 14/17/20 at {breadth14Season?.ToString() ?? "n/a"}/{breadth17Season?.ToString() ?? "n/a"}/{breadth20Season?.ToString() ?? "n/a"}; Insight/year {first.InsightPerYear.ToDoubleForDisplay():F1}→{last.InsightPerYear.ToDoubleForDisplay():F1}, projects {first.ProjectsDone}→{last.ProjectsDone}; KEY-3 {first.Key3State}/{first.Key3Progress.ToDoubleForDisplay():F1}→{last.Key3State}/{last.Key3Progress.ToDoubleForDisplay():F1} of cost {last.Key3EffectiveCost.ToDoubleForDisplay():F1} (max {maxKeyProgress.ToDoubleForDisplay():F1}; slot {key3SlotSeasons}, queue {key3QueueSeasons}, plan {key3PlanSeasons}, done {last.Key3Done}); prerequisites [{string.Join("; ", prerequisiteSummary)}]; available closure projects off-plan [{omittedClosure}]";

        var timeline = new List<string>();
        ProjectState previousKey3State = first.Key3State;
        string previousPrerequisiteStates = string.Join(",", first.Prerequisites.Select(static prerequisite => prerequisite.State));
        int previousE2 = first.E2BreadthDone;
        bool breadthGateWasMet = first.E2BreadthDone >= catalog.Projects[key3].BreadthMin;
        foreach (WarlordResearchSeasonSnapshot snapshot in states)
        {
            string plan = $"slots {FormatPlanProjects(snapshot.Slots, catalog)} | q {FormatPlanProjects(snapshot.Queue.Take(3), catalog)} | plan {FormatPlanProjects(snapshot.Plan.Take(5), catalog)}";
            string prerequisiteStates = string.Join(",", snapshot.Prerequisites.Select(static prerequisite => prerequisite.State));
            bool breadthGateMet = snapshot.E2BreadthDone >= catalog.Projects[key3].BreadthMin;
            bool milestone = snapshot.Key3State != previousKey3State || prerequisiteStates != previousPrerequisiteStates ||
                breadthGateMet != breadthGateWasMet || CrossedBreadth(previousE2, snapshot.E2BreadthDone, 14) ||
                CrossedBreadth(previousE2, snapshot.E2BreadthDone, 17) || CrossedBreadth(previousE2, snapshot.E2BreadthDone, catalog.Projects[key3].BreadthMin);
            if (milestone || snapshot.Season % 16 == 0)
                timeline.Add($"s{snapshot.Season} era={snapshot.Era} K2={snapshot.Key2State} K3={snapshot.Key3State}/{snapshot.Key3Progress.ToDoubleForDisplay():F1} E2={snapshot.E2BreadthDone} I/y={snapshot.InsightPerYear.ToDoubleForDisplay():F0} done={snapshot.ProjectsDone} [{plan}]");
            previousKey3State = snapshot.Key3State;
            previousPrerequisiteStates = prerequisiteStates;
            previousE2 = snapshot.E2BreadthDone;
            breadthGateWasMet = breadthGateMet;
        }
        return new WarlordResearchPlanDiagnostic(civ, summary, string.Join("; ", timeline));
    }

    private static int? FirstSeasonAtBreadth(IReadOnlyList<WarlordResearchSeasonSnapshot> states, int breadth)
    {
        foreach (WarlordResearchSeasonSnapshot snapshot in states)
            if (snapshot.E2BreadthDone >= breadth) return snapshot.Season;
        return null;
    }

    private static bool CrossedBreadth(int before, int after, int threshold) => before < threshold && after >= threshold;

    private static string FormatPlanProjects(IEnumerable<int> projects, ResearchCatalog catalog) =>
        string.Join(",", projects.Select(project => project < 0 ? "-" : catalog.Projects[project].Id));

    private static int WarlordUrgencyBand(Fix64 urgency)
    {
        if (urgency < Fix64.FromRatio(45, 100)) return 0;
        if (urgency < Fix64.FromRatio(50, 100)) return 1;
        if (urgency < Fix64.FromRatio(60, 100)) return 2;
        if (urgency < Fix64.FromRatio(70, 100)) return 3;
        return 4;
    }

    private static FirstRaceEstimate[] CaptureFirstRaceEstimates(MatchModel match, Player[] seats, int projects)
    {
        var estimates = new List<FirstRaceEstimate>(2);
        for (int c = 0; c < seats.Length; c++)
        {
            if (!string.Equals(seats[c].Name, "warlord", StringComparison.OrdinalIgnoreCase) ||
                match.Ai?.SeatOf(c)?.Spark is not ResearchSpark spark) continue;
            var rivalEta = new int[projects];
            for (int p = 0; p < projects; p++) rivalEta[p] = spark.RivalEtaStepsForDiagnostics(p);
            estimates.Add(new FirstRaceEstimate(c, spark.LastDecisionSeasonForDiagnostics, spark.RivalPace, rivalEta));
        }
        return estimates.ToArray();
    }

    /// <summary>Runs a match while collecting only the team Legacy scores consumed by A6.</summary>
    public static LegacyMatchOutcome PlayForLegacy(ContentPack pack, ulong seed, Player[] seats, EconomyArchetype[] archetypes, int minutes,
        Action<MatchModel>? configure = null, Action<MatchModel>? completed = null)
    {
        GeneratedWorld world = WorldGenerator.Generate(seed);
        return PlayForLegacy(pack, seed, world, seats, archetypes, minutes, configure, completed);
    }

    /// <summary>Runs a legacy-only match on a caller-provided generated world.</summary>
    internal static LegacyMatchOutcome PlayForLegacy(ContentPack pack, ulong seed, GeneratedWorld world, Player[] seats,
        EconomyArchetype[] archetypes, int minutes, Action<MatchModel>? configure = null, Action<MatchModel>? completed = null)
    {
        var controls = new CivControl[seats.Length];
        for (int i = 0; i < seats.Length; i++) controls[i] = seats[i].Control;
        MatchModel match = MatchModel.Create(seed, pack, world, archetypes, controls);
        configure?.Invoke(match);
        var runner = new SimRunner(match, int.MaxValue);
        long ticks = minutes * 600L;
        int seasons = 0;
        for (long t = 0; t < ticks; t += 150)
        {
            runner.Advance(Math.Min(150, ticks - t));
            seasons++;
            if (match.EndOfMatch(humanSeat: -1).IsOver(runner.Tick)) break;
        }

        var legacy = new double[seats.Length];
        for (int c = 0; c < seats.Length; c++)
        {
            Standing standing = Scoring.Of(match, c);
            legacy[c] = standing.Legacy.ToDoubleForDisplay();
        }
        completed?.Invoke(match);
        double teamA = 0, teamB = 0;
        foreach (int c in TeamASeats) teamA += legacy[c];
        foreach (int c in TeamBSeats) teamB += legacy[c];
        return new LegacyMatchOutcome(teamA, teamB, runner.Tick, seasons, runner.CurrentChecksum);
    }

    private static bool HasAvailable(ResearchSystem research, ResearchCatalog catalog, int civ)
    {
        for (int p = 0; p < catalog.Count; p++)
            if (catalog.Projects[p].Numeric && research.State(civ, p) == ProjectState.Available) return true;
        return false;
    }

    private static bool TryReadAlternateOriginAttackers(string reason, out int attackers)
    {
        const string marker = "best alternate rally origin yields ";
        int start = reason.LastIndexOf(marker, StringComparison.Ordinal);
        if (start < 0) { attackers = 0; return false; }
        start += marker.Length;
        int end = reason.IndexOf(' ', start);
        if (end < 0) end = reason.Length;
        return int.TryParse(reason.AsSpan(start, end - start), NumberStyles.None, CultureInfo.InvariantCulture, out attackers);
    }

    /// <summary>
    /// A mirrored duel: team A (seats 0 and 3) against team B (seats 1 and 2) on each seed, then again with the teams
    /// swapped, so neither side gets the better half of the map. Returns one result per match from team A's view.
    /// </summary>
    public sealed class DuelResult
    {
        public int Matches;
        public double WinsA;
        public double SettlementsLostA, SettlementsLostB;
        public int MatchesWithLossA, MatchesWithLossB;
        public double LegacyA, LegacyB;
        public int OwnedLandShareA, OwnedLandShareB;
        public int AlternateOriginCampaignRescuesA, AlternateOriginCampaignRescuesB;
        public int AlternateOriginCampaignNoRescuesA, AlternateOriginCampaignNoRescuesB;
        public int RaidsA, RaidsB;
        public int KeystoneStallsA, IdleSlotSeasonsA, MaxPlanSwitchesA;
        public int PlanDitherMatchesA;
        public double MeanKey1A = double.NaN, MeanKey1B = double.NaN;
        public readonly List<MatchOutcome> Outcomes = new();
        public double WinRateA => Matches == 0 ? 0 : WinsA / Matches;
    }

    public static DuelResult Duel(ContentPack pack, string teamA, string teamB, int seeds, int minutes, EconomyArchetype archetype,
        ulong firstSeed = BaseSeed, bool captureFirstRaceDiagnostics = false, Fix64? rivalPaceOverride = null,
        int? firstsEtaPessimismSeasons = null, int? warlordNonConquerRaidPostureStreak = null,
        bool warlordMaxCampaignOrigin = false, Fix64? warlordFrontierEarlyBonusOverride = null,
        Fix64? warlordE3FissionThresholdOverride = null, Fix64? warlordDefenseResearchBonusOverride = null,
        bool warlordDefenseUrgencyShadowDiagnostic = false, bool captureFoundFunnelDiagnostics = false,
        Fix64? warlordKeystoneTempoOverride = null, bool warlordE2ClosureSlotDiagnostic = false,
        bool warlordSettlementTrace = false, bool warlordB30Trace = false, bool warlordB28Trace = false,
        bool warlordB27Trace = false, bool warlordSeatAllianceDiagnostic = false,
        Fix64? warlordPostureUnlockBonusOverride = null, bool warlordFissionSiteDiagnostic = false,
        bool foodShortageSkipScoutsForDiagnostics = false, bool foodShortageSkipScoutsForChieftainsForDiagnostics = false,
        bool warlordCampaignOpportunityTrace = false,
        bool warlordFrontierConquestPlanFloorDiagnostic = false,
        bool a2LeadCheckpointDiagnostic = false,
        int? deferWarlordS2FissionUntilSeasonForDiagnostics = null)
    {
        var jobs = new List<(ulong Seed, bool Swapped)>();
        for (int i = 0; i < seeds; i++) { jobs.Add((firstSeed + (ulong)i, false)); jobs.Add((firstSeed + (ulong)i, true)); }
        var results = new (MatchOutcome Outcome, bool Swapped)[jobs.Count];
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, j =>
        {
            (ulong seed, bool swapped) = jobs[j];
            Player a = Player.Parse(teamA, archetype), b = Player.Parse(teamB, archetype);
            var seats = new Player[4];
            foreach (int s in TeamASeats) seats[s] = swapped ? b : a;
            foreach (int s in TeamBSeats) seats[s] = swapped ? a : b;
            var archetypes = new[] { archetype, archetype, archetype, archetype };
            results[j] = (Play(pack, seed, seats, archetypes, minutes, captureFirstRaceDiagnostics, rivalPaceOverride,
                firstsEtaPessimismSeasons, warlordNonConquerRaidPostureStreak, warlordMaxCampaignOrigin,
                warlordFrontierEarlyBonusOverride, warlordE3FissionThresholdOverride, warlordDefenseResearchBonusOverride,
                warlordDefenseUrgencyShadowDiagnostic, captureFoundFunnelDiagnostics, warlordKeystoneTempoOverride,
                warlordE2ClosureSlotDiagnostic, warlordSettlementTrace, swapped, warlordB30Trace, warlordB28Trace,
                warlordB27Trace, warlordSeatAllianceDiagnostic, warlordPostureUnlockBonusOverride,
                warlordFissionSiteDiagnostic, foodShortageSkipScoutsForDiagnostics, foodShortageSkipScoutsForChieftainsForDiagnostics,
                warlordCampaignOpportunityTrace: warlordCampaignOpportunityTrace,
                warlordFrontierConquestPlanFloorDiagnostic: warlordFrontierConquestPlanFloorDiagnostic,
                a2LeadCheckpointDiagnostic: a2LeadCheckpointDiagnostic,
                deferWarlordS2FissionUntilSeasonForDiagnostics: deferWarlordS2FissionUntilSeasonForDiagnostics), swapped);
        });
        var r = new DuelResult();
        double key1A = 0, key1B = 0; int nA = 0, nB = 0;
        foreach ((MatchOutcome m, bool swapped) in results)
        {
            int[] aSeats = swapped ? TeamBSeats : TeamASeats, bSeats = swapped ? TeamASeats : TeamBSeats;
            double la = 0, lb = 0; int lostA = 0, lostB = 0;
            foreach (int s in aSeats)
            {
                CivOutcome c = m.Civs[s];
                la += c.Legacy; lostA += c.SettlementsLost; r.RaidsA += c.RaidsLaunched;
                r.KeystoneStallsA += c.KeystoneStalls; r.IdleSlotSeasonsA += c.IdleSlotSeasons;
                r.MaxPlanSwitchesA = Math.Max(r.MaxPlanSwitchesA, c.PlanSwitchesInThreeYears);
                if (c.PlanSwitchesInThreeYears > 2) r.PlanDitherMatchesA++;
                if (!double.IsNaN(c.KeyMinutes[0])) { key1A += c.KeyMinutes[0]; nA++; }
            }
            foreach (int s in bSeats)
            {
                CivOutcome c = m.Civs[s];
                lb += c.Legacy; lostB += c.SettlementsLost; r.RaidsB += c.RaidsLaunched;
                if (!double.IsNaN(c.KeyMinutes[0])) { key1B += c.KeyMinutes[0]; nB++; }
            }
            r.Matches++;
            r.WinsA += la > lb ? 1 : la == lb ? 0.5 : 0;
            r.LegacyA += la; r.LegacyB += lb;
            r.OwnedLandShareA += (int)aSeats.Sum(s => (long)m.Civs[s].OwnedLandShareBasisPoints);
            r.OwnedLandShareB += (int)bSeats.Sum(s => (long)m.Civs[s].OwnedLandShareBasisPoints);
            r.AlternateOriginCampaignRescuesA += aSeats.Sum(s => m.Civs[s].AlternateOriginCampaignRescues);
            r.AlternateOriginCampaignRescuesB += bSeats.Sum(s => m.Civs[s].AlternateOriginCampaignRescues);
            r.AlternateOriginCampaignNoRescuesA += aSeats.Sum(s => m.Civs[s].AlternateOriginCampaignNoRescues);
            r.AlternateOriginCampaignNoRescuesB += bSeats.Sum(s => m.Civs[s].AlternateOriginCampaignNoRescues);
            r.SettlementsLostA += lostA; r.SettlementsLostB += lostB;
            if (lostA > 0) r.MatchesWithLossA++;
            if (lostB > 0) r.MatchesWithLossB++;
            r.Outcomes.Add(m);
        }
        if (nA > 0) r.MeanKey1A = key1A / nA;
        if (nB > 0) r.MeanKey1B = key1B / nB;
        return r;
    }

    /// <summary>
    /// A free-for-all: four AIs of one level with different archetypes, rotated through the seats so position does
    /// not decide. Returns wins and appearances per archetype (docs/MVP/14 §10.3 archetype balance).
    /// </summary>
    public static (double[] Wins, int[] Appearances) FreeForAll(ContentPack pack, string level, int seeds, int minutes,
        ulong firstSeed = BaseSeed, Fix64? rivalPaceOverride = null, bool warlordMaxCampaignOrigin = false,
        List<string>? matchDiagnostics = null, List<string>? firstsDiagnostics = null, bool captureFirstsAttribution = false,
        List<string>? researchDiagnostics = null, bool captureResearchTimeline = false, int[]? selectedRotations = null,
        List<string>? foodDiagnostics = null, bool captureFoodTimeline = false,
        bool deferA5BuilderS1FissionUntil64 = false, List<string>? caseControlDiagnostics = null,
        bool captureCaseControlTimeline = false)
        => FreeForAll(pack, level, seeds, minutes, out _, firstSeed, rivalPaceOverride, warlordMaxCampaignOrigin,
            matchDiagnostics, firstsDiagnostics, captureFirstsAttribution, researchDiagnostics, captureResearchTimeline,
            selectedRotations, foodDiagnostics, captureFoodTimeline, deferA5BuilderS1FissionUntil64,
            caseControlDiagnostics, captureCaseControlTimeline);

    /// <summary>The free-for-all, also returning every civilization's outcome with its archetype for the per-archetype report.</summary>
    public static (double[] Wins, int[] Appearances) FreeForAll(ContentPack pack, string level, int seeds, int minutes,
        out List<(EconomyArchetype Archetype, CivOutcome Outcome, bool Won)> civs, ulong firstSeed = BaseSeed,
        Fix64? rivalPaceOverride = null, bool warlordMaxCampaignOrigin = false, List<string>? matchDiagnostics = null,
        List<string>? firstsDiagnostics = null, bool captureFirstsAttribution = false,
        List<string>? researchDiagnostics = null, bool captureResearchTimeline = false, int[]? selectedRotations = null,
        List<string>? foodDiagnostics = null, bool captureFoodTimeline = false,
        bool deferA5BuilderS1FissionUntil64 = false, List<string>? caseControlDiagnostics = null,
        bool captureCaseControlTimeline = false)
    {
        EconomyArchetype[] all = { EconomyArchetype.Builder, EconomyArchetype.Grower, EconomyArchetype.Wanderer, EconomyArchetype.Warlord, EconomyArchetype.Sage };
        var jobs = new List<(ulong Seed, int Rotation)>();
        int[] rotations = selectedRotations ?? new[] { 0, 1, 2, 3, 4 };
        if (rotations.Any(static rotation => rotation is < 0 or > 4) || rotations.Distinct().Count() != rotations.Length)
            throw new ArgumentOutOfRangeException(nameof(selectedRotations), "FFA rotations must be unique values from 0 through 4.");
        for (int i = 0; i < seeds; i++) foreach (int rot in rotations) jobs.Add((firstSeed + (ulong)i, rot));
        var outcomes = new (MatchOutcome Outcome, EconomyArchetype[] Seating)[jobs.Count];
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, j =>
        {
            (ulong seed, int rot) = jobs[j];
            // The rotation drops one archetype; the shift moves the others round the table so position does not decide.
            var seating = new EconomyArchetype[4];
            int shift = (int)(seed % 4);
            for (int k = 0; k < 4; k++) seating[(k + shift) % 4] = all[(rot + 1 + k) % 5];
            var seats = new Player[4];
            for (int s = 0; s < 4; s++) seats[s] = Player.Parse(level, seating[s]);
            outcomes[j] = (Play(pack, seed, seats, seating, minutes, rivalPaceOverride: rivalPaceOverride,
                warlordMaxCampaignOrigin: warlordMaxCampaignOrigin,
                captureFfaFirstsDiagnostic: captureFirstsAttribution,
                captureFfaResearchTimeline: captureResearchTimeline, ffaRotation: rot,
                captureFfaFoodDemographyTimeline: captureFoodTimeline,
                deferFissionSettlementUntilSeasonForDiagnostics: deferA5BuilderS1FissionUntil64 &&
                    seed == 20261025 && rot == 4 && seating[1] == EconomyArchetype.Builder ? 64 : null,
                captureFfaCaseControlTimeline: captureCaseControlTimeline), seating);
        });
        var wins = new double[5];
        var apps = new int[5];
        civs = new List<(EconomyArchetype, CivOutcome, bool)>();
        matchDiagnostics?.Clear();
        firstsDiagnostics?.Clear();
        researchDiagnostics?.Clear();
        foodDiagnostics?.Clear();
        caseControlDiagnostics?.Clear();
        for (int jobIndex = 0; jobIndex < outcomes.Length; jobIndex++)
        {
            (MatchOutcome m, EconomyArchetype[] seating) = outcomes[jobIndex];
            double best = double.MinValue; int bestSeat = 0;
            for (int s = 0; s < 4; s++) if (m.Civs[s].Legacy > best) { best = m.Civs[s].Legacy; bestSeat = s; }
            for (int s = 0; s < 4; s++) apps[Array.IndexOf(all, seating[s])]++;
            wins[Array.IndexOf(all, seating[bestSeat])]++;
            for (int s = 0; s < 4; s++) civs.Add((seating[s], m.Civs[s], s == bestSeat));
            matchDiagnostics?.Add(FormatFfaMatchDetail(jobs[jobIndex].Seed, jobs[jobIndex].Rotation, seating, m.Civs));
            firstsDiagnostics?.Add(FormatFfaFirstsAttribution(jobs[jobIndex].Seed, jobs[jobIndex].Rotation,
                seating, m.Civs, m.FfaFirstClaims));
            if (researchDiagnostics is not null)
                researchDiagnostics.AddRange(m.FfaResearchTimeline);
            if (foodDiagnostics is not null)
                foodDiagnostics.AddRange(m.FfaFoodDemographyTraces);
            if (caseControlDiagnostics is not null)
                caseControlDiagnostics.AddRange(m.FfaCaseControlTraces);
        }
        return (wins, apps);
    }

    internal static bool IsFfaCaseControlTerminal(bool matchEnded, bool horizonReached) => matchEnded || horizonReached;

    internal static string FormatFfaMatchDetail(ulong seed, int rotation, EconomyArchetype[] seating, CivOutcome[] civs)
    {
        int winner = 0;
        for (int s = 1; s < civs.Length; s++)
            if (civs[s].Legacy > civs[winner].Legacy) winner = s;
        int runnerUp = winner == 0 ? 1 : 0;
        for (int s = 0; s < civs.Length; s++)
            if (s != winner && (runnerUp == winner || civs[s].Legacy > civs[runnerUp].Legacy)) runnerUp = s;
        static string Score(EconomyArchetype archetype, int civ, CivOutcome outcome) =>
            $"{archetype}/c{civ}:L{outcome.Legacy:F1}(D{outcome.Dominion:F1},M{outcome.Mastery:F1},F{outcome.Firsts:F1},W{outcome.Wonders:F1})";
        string scores = string.Join(';', Enumerable.Range(0, civs.Length).Select(s => Score(seating[s], s, civs[s])));
        return $"A5_FFA_MATCH seed={seed} rotation={rotation} winner={seating[winner]}/c{winner} L={civs[winner].Legacy:F1} " +
            $"runnerUp={seating[runnerUp]}/c{runnerUp} L={civs[runnerUp].Legacy:F1} margin={civs[winner].Legacy - civs[runnerUp].Legacy:F1} scores=[{scores}]";
    }


    private static int FindSettlementIndex(MatchModel match, int settlementId)
    {
        for (int s = 0; s < match.Economy.SettlementCount; s++)
            if (match.Economy.Settlement(s).Id == settlementId) return s;
        return -1;
    }

    private static FfaFoodWindow GetFfaFoodWindow(Dictionary<int, FfaFoodWindow> windows, int settlementId)
    {
        if (!windows.TryGetValue(settlementId, out FfaFoodWindow? window))
            windows[settlementId] = window = new FfaFoodWindow();
        return window;
    }

    internal static string FormatFfaFirstsAttribution(ulong seed, int rotation, EconomyArchetype[] seating,
        CivOutcome[] civs, IReadOnlyList<FfaFirstClaimDiagnostic> firstClaims)
    {
        int winner = 0;
        for (int s = 1; s < civs.Length; s++)
            if (civs[s].Legacy > civs[winner].Legacy) winner = s;
        int runnerUp = winner == 0 ? 1 : 0;
        for (int s = 0; s < civs.Length; s++)
            if (s != winner && civs[s].Legacy > civs[runnerUp].Legacy) runnerUp = s;
        int builder = Array.IndexOf(seating, EconomyArchetype.Builder);
        string builderReport = builder < 0 ? "absent" : FormatClaimReport(builder, civs[builder].Firsts, firstClaims);
        string runnerReport = FormatClaimReport(runnerUp, civs[runnerUp].Firsts, firstClaims);
        return $"A5_FFA_FIRSTS seed={seed} rotation={rotation} winner={seating[winner]}/c{winner} L={civs[winner].Legacy:F1} " +
            $"runnerUp={seating[runnerUp]}/c{runnerUp} L={civs[runnerUp].Legacy:F1} margin={civs[winner].Legacy - civs[runnerUp].Legacy:F1} " +
            $"builderFirsts={builderReport} runnerUpFirsts={runnerReport}";
    }

    private static string FormatClaimReport(int civ, double scorePoints, IReadOnlyList<FfaFirstClaimDiagnostic> claims)
    {
        FfaFirstClaimDiagnostic[] owned = claims.Where(claim => claim.Civ == civ)
            .OrderBy(claim => claim.Era).ThenBy(claim => claim.ProjectId, StringComparer.Ordinal).ToArray();
        int attributedPoints = owned.Sum(claim => claim.Points);
        string byEra = string.Join(';', owned.GroupBy(claim => claim.Era)
            .Select(era => $"E{era.Key}:[{string.Join(',', era.Select(claim => $"{claim.ProjectId}={claim.Points}"))}]"));
        bool reconciled = Math.Abs(scorePoints - attributedPoints) < 0.01;
        return $"score={scorePoints:F1}/events={attributedPoints}/reconciled={reconciled}[{byEra}]";
    }

    /// <summary>Elo ratings from pairwise win rates by repeated updates against the aggregate (starts at 1000).</summary>
    public static double[] Elo(int players, double[,] winRate, int[,] games)
    {
        var rating = new double[players];
        for (int i = 0; i < players; i++) rating[i] = 1000;
        for (int pass = 0; pass < 400; pass++)
        {
            for (int a = 0; a < players; a++)
                for (int b = a + 1; b < players; b++)
                {
                    if (games[a, b] == 0) continue;
                    double expected = 1.0 / (1.0 + Math.Pow(10, (rating[b] - rating[a]) / 400.0));
                    double actual = winRate[a, b];
                    double delta = 4.0 * (actual - expected);
                    rating[a] += delta; rating[b] -= delta;
                }
        }
        return rating;
    }

    private static void CaptureWarlordCampaignOpportunityTrace(MatchModel match, Player[] seats, MatchOutcome outcome,
        int season, int aiLogStart, int politicsEventStart,
        Dictionary<(int Warlord, int Rival), string> prior,
        Dictionary<string, string> emittedDecisions,
        Dictionary<(int Warlord, int Rival), int> firstKnownSeason,
        Dictionary<(int Warlord, int Rival), int> firstReachSeason,
        HashSet<(int Warlord, int Rival)> milestoneEmitted,
        bool swapped)
    {
        if (match.Ai is not { } ai) return;
        for (int warlord = 0; warlord < seats.Length; warlord++)
        {
            if (!string.Equals(seats[warlord].Name, "warlord", StringComparison.OrdinalIgnoreCase) ||
                ai.SeatOf(warlord) is not { } seat) continue;
            for (int rivalCiv = 0; rivalCiv < seats.Length; rivalCiv++)
            {
                if (!string.Equals(seats[rivalCiv].Name, "grower", StringComparison.OrdinalIgnoreCase)) continue;
                RivalBelief rival = seat.Belief.Rivals[rivalCiv];
                OwnView own = seat.Own;
                int width = seat.Belief.Map.Width;
                var key = (warlord, rivalCiv);
                int publicReach = match.Politics.ReachOfEra(rival.Era);
                int reachLimit = StrategistTuning.RaidRangeReaches * Math.Max(6, publicReach);
                BeliefSettlement? target = null;
                int targetDistance = int.MaxValue;
                OwnSettlement? selectedOrigin = null;
                bool selectedFresh = false;
                bool anyRangeEligible = false;
                foreach (BeliefSettlement known in rival.Settlements)
                {
                    OwnSettlement? origin = CampaignTraceOrigin(own, known.Tile, width);
                    int distance = origin is null ? int.MaxValue : HexGrid.Distance(origin.Tile, known.Tile, width);
                    bool siteFresh = season - known.LastSeenSeason <= StrategistTuning.StaleSeasons;
                    anyRangeEligible |= siteFresh && distance <= reachLimit;
                    if ((siteFresh && !selectedFresh) || (siteFresh == selectedFresh &&
                        (distance < targetDistance || (distance == targetDistance && (target is null || known.Settlement < target.Settlement)))))
                    {
                        target = known;
                        targetDistance = distance;
                        selectedOrigin = origin;
                        selectedFresh = siteFresh;
                    }
                }
                if (rival.Settlements.Count > 0 && !firstKnownSeason.ContainsKey(key))
                    firstKnownSeason[key] = rival.Settlements.Min(static site => site.FirstSeenSeason);
                bool rangeEligible = target is not null && selectedFresh && targetDistance <= reachLimit;
                if (anyRangeEligible && !firstReachSeason.ContainsKey(key)) firstReachSeason[key] = season;

                int eligibleAttackers = 0;
                Fix64 estimatedForce = Fix64.Zero;
                if (target is not null && selectedOrigin is not null)
                    foreach (OwnSettlement contributor in own.Settlements)
                    {
                        if (HexGrid.Distance(contributor.Tile, selectedOrigin.Tile, width) > StrategistTuning.ArmyGatherRange ||
                            contributor.Fighters <= 0) continue;
                        int available = Math.Max(0, Math.Min(contributor.Fighters - PoliticsSystem.CampaignHomeReserve,
                            contributor.Fighters * 6 / 10));
                        eligibleAttackers += available;
                        estimatedForce += contributor.AttackingStrength / contributor.Fighters * StrategistTuning.PartyPickBonus * available;
                    }
                string targetLabel = target is null ? "none" : $"s{target.Settlement}@t{target.Tile}";
                string originLabel = selectedOrigin is null ? "none" : $"s{selectedOrigin.Index}@t{selectedOrigin.Tile}";
                int relation = own.Others[rivalCiv].Relations;
                bool gov02Done = match.Research.IsDone(warlord, match.Research.Catalog.IndexOf("GOV-02"));
                bool declarationAllowed = relation < Diplomacy.HostileBelow || own.Posture == Posture.Raid || gov02Done;
                string blocker = target is null ? "no-known-site" : !selectedFresh ? "site-stale" : targetDistance > reachLimit
                    ? "outside-geometric-range" : eligibleAttackers < PoliticsSystem.CampaignMinimumAttackers
                        ? "estimated-force-below-minimum" : "no-force-block-observed";
                string snapshot = $"target={targetLabel};age={(target is null ? -1 : season - target.LastSeenSeason)};" +
                    $"dist={targetDistance};rangeEligible={rangeEligible};rangeTiles={reachLimit};origin={originLabel};" +
                    $"estimatedAvailableAttackers={eligibleAttackers};estimatedForce={estimatedForce.ToDoubleForDisplay():F2};" +
                    $"posture={own.Posture};atWar={rival.AtWar};relations={relation};gov02={gov02Done};" +
                    $"declarationGate={(rival.AtWar ? "already-at-war" : declarationAllowed ? "declaration-allowed" : "declaration-locked")};" +
                    $"assaultBlock={blocker};remainingCapture/strategy-gates=not-evaluated";
                string stateSignature = $"target={target?.Settlement ?? -1};tile={target?.Tile ?? -1};fresh={selectedFresh};" +
                    $"range={rangeEligible};origin={selectedOrigin?.Index ?? -1};forceMin={eligibleAttackers >= PoliticsSystem.CampaignMinimumAttackers};" +
                    $"posture={own.Posture};war={rival.AtWar};relationGate={declarationAllowed};gov02={gov02Done};block={blocker}";
                if (season >= 128)
                {
                    bool firstTraceRow = !milestoneEmitted.Contains(key);
                    if (!milestoneEmitted.Contains(key))
                    {
                        milestoneEmitted.Add(key);
                        outcome.WarlordCampaignOpportunityTraces.Add(
                            $"A2 campaign-milestone seed={outcome.Seed} mirror={(swapped ? 1 : 0)} warlord=civ{warlord} grower=civ{rivalCiv} " +
                            $"firstKnown={(firstKnownSeason.TryGetValue(key, out int knownAt) ? $"s{knownAt}" : "never")} " +
                            $"firstRangeEligible={(firstReachSeason.TryGetValue(key, out int reachAt) ? $"s{reachAt}" : "never")} " +
                            "reachDefinition=belief-site-fresh+Strategist-geometric-range(no-pathfinding) " +
                            $"observedAt=s{season}");
                    }
                    if (firstTraceRow || !prior.TryGetValue(key, out string? oldSnapshot) || oldSnapshot != stateSignature)
                        outcome.WarlordCampaignOpportunityTraces.Add(
                            $"A2 campaign-state seed={outcome.Seed} mirror={(swapped ? 1 : 0)} warlord=civ{warlord} grower=civ{rivalCiv} s{season} {snapshot}");
                }
                prior[key] = stateSignature;
            }
        }

        if (season < 128) return;
        for (int i = aiLogStart; i < ai.Log.Entries.Count; i++)
        {
            AiDecision decision = ai.Log.Entries[i];
            if ((uint)decision.Civ >= (uint)seats.Length ||
                !string.Equals(seats[decision.Civ].Name, "warlord", StringComparison.OrdinalIgnoreCase) ||
                decision.Kind is not ("War" or "WarRejected" or "Assault" or "Raid" or "RaidRejected" or "Posture" or "Peace" or "Captured")) continue;
            string key = $"{decision.Civ}|{decision.Kind}|{decision.Subject}|{decision.Other}";
            string signature = $"{decision.Score.Raw}|{decision.Reason}";
            if (emittedDecisions.TryGetValue(key, out string? previous) && previous == signature) continue;
            emittedDecisions[key] = signature;
            outcome.WarlordCampaignOpportunityTraces.Add(
                $"A2 campaign-decision seed={outcome.Seed} mirror={(swapped ? 1 : 0)} warlord=civ{decision.Civ} s{decision.Season} " +
                $"kind={decision.Kind} target={decision.Subject} other={decision.Other} score={decision.Score.ToDoubleForDisplay():F3} reason={decision.Reason}");
        }
        for (int i = politicsEventStart; i < match.Politics.Events.Count; i++)
        {
            PoliticsEvent ev = match.Politics.Events[i];
            if ((uint)ev.Civ >= (uint)seats.Length ||
                !string.Equals(seats[ev.Civ].Name, "warlord", StringComparison.OrdinalIgnoreCase) ||
                ev.Kind is not (PoliticsEventKind.RaidLaunched or PoliticsEventKind.RaidResolved or
                    PoliticsEventKind.AssaultResolved or PoliticsEventKind.Captured or PoliticsEventKind.Occupied or
                    PoliticsEventKind.OccupationBroken or PoliticsEventKind.VoyageLost)) continue;
            int eventSeason = checked((int)(ev.Tick / LivingEconomyModel.SeasonTicks));
            outcome.WarlordCampaignOpportunityTraces.Add(
                $"A2 campaign-event seed={outcome.Seed} mirror={(swapped ? 1 : 0)} warlord=civ{ev.Civ} s{eventSeason} " +
                $"kind={ev.Kind} other={ev.Other} a={ev.A} b={ev.B}");
        }
    }

    private static OwnSettlement? CampaignTraceOrigin(OwnView own, int targetTile, int width)
    {
        OwnSettlement? nearestAdultOrigin = null, fullest = null;
        int nearestDistance = int.MaxValue, mostAdults = -1;
        foreach (OwnSettlement settlement in own.Settlements)
        {
            if (settlement.Adults > mostAdults) { mostAdults = settlement.Adults; fullest = settlement; }
            int distance = HexGrid.Distance(settlement.Tile, targetTile, width);
            if (settlement.Adults >= 8 && distance < nearestDistance)
            { nearestDistance = distance; nearestAdultOrigin = settlement; }
        }
        return nearestAdultOrigin ?? fullest;
    }

    private static string FormatB28ShadowSettlement(int settlement, int defender, int season, int worldWidth, MatchModel match,
        int firstAppearanceSeason = -1)
    {
        if ((uint)settlement >= (uint)match.Politics.SettlementTotal) return $"s{settlement}=absent-at-s{season}";
        int owner = match.Politics.SettlementCivOf(settlement);
        var defenses = match.Politics.MilitaryOf(settlement).Defenses;
        (int buildKind, Fix64 buildProgress) = match.Economy.SettlementBuild(settlement);
        string buildTarget = buildKind < 0 ? "none" : LivingEconomyModel.StructureName(buildKind);
        string foundSeason = firstAppearanceSeason == 0 ? "initial" : firstAppearanceSeason < 0 ? "unknown" : $"s{firstAppearanceSeason}";
        string frontier = "not-member";
        if (owner == defender)
        {
            int ownCount = 0, rank = 0, targetScore = 0, targetNeighbor = -1;
            var scores = new List<(int Settlement, int Score, int Nearest)>();
            for (int own = 0; own < match.Politics.SettlementTotal; own++)
            {
                if (match.Politics.SettlementCivOf(own) != defender || match.Politics.IsFreeFolk(own)) continue;
                ownCount++;
                int nearestDistance = int.MaxValue, nearestSettlement = -1;
                int ownTile = match.Politics.SettlementTileOf(own);
                for (int other = 0; other < match.Politics.SettlementTotal; other++)
                {
                    if (other == own || match.Politics.SettlementCivOf(other) != defender || match.Politics.IsFreeFolk(other)) continue;
                    int distance = HexGrid.Distance(ownTile, match.Politics.SettlementTileOf(other), worldWidth);
                    if (distance < nearestDistance || (distance == nearestDistance && other < nearestSettlement))
                    { nearestDistance = distance; nearestSettlement = other; }
                }
                int score = nearestDistance == int.MaxValue ? 0 : nearestDistance;
                scores.Add((own, score, nearestSettlement));
            }
            scores.Sort(static (a, b) => b.Score != a.Score ? b.Score.CompareTo(a.Score) : a.Settlement.CompareTo(b.Settlement));
            for (int i = 0; i < scores.Count; i++)
            {
                if (scores[i].Settlement != settlement) continue;
                rank = i + 1;
                targetScore = scores[i].Score;
                targetNeighbor = scores[i].Nearest;
                break;
            }
            string neighbor = targetNeighbor < 0 ? "none" : $"s{targetNeighbor}";
            frontier = $"rank={rank}/{ownCount},nearestSameCiv={neighbor},score={targetScore}(distance-to-nearest;score-desc/id-asc)";
        }
        return $"s{settlement} owner=civ{owner} tile=t{match.Politics.SettlementTileOf(settlement)} currentSeason=s{season} " +
            $"foundingSeasonObserved={foundSeason} pop={match.Politics.SettlementPopulationOf(settlement)} defenses={defenses} " +
            $"wood={match.Economy.GoodStock(settlement, "G-WOOD").ToDoubleForDisplay():F1} " +
            $"buildTarget={buildTarget} buildProgress={buildProgress.ToDoubleForDisplay():P0} frontier[{frontier}]";
    }

    private static string FormatB28Settlement(int settlement, MatchModel match)
    {
        var defense = match.Politics.MilitaryOf(settlement).Defenses;
        (int kind, Fix64 share) = match.Economy.SettlementBuild(settlement);
        string build = kind switch
        {
            18 => "Hillfort",
            19 => "StoneWalls",
            20 => "Palisade",
            _ => kind < 0 ? "none" : $"structure-{kind}"
        };
        string activeDefenseProgress = kind is 18 or 19 or 20
            ? $"{build}:{share.ToDoubleForDisplay():P0} (completion ETA unavailable)"
            : "none";
        string wood = match.Economy.GoodStock(settlement, "G-WOOD").ToDoubleForDisplay().ToString("F1", CultureInfo.InvariantCulture);
        string stone = match.Economy.GoodStock(settlement, "G-STONE").ToDoubleForDisplay().ToString("F1", CultureInfo.InvariantCulture);
        string clay = match.Economy.GoodStock(settlement, "G-CLAY").ToDoubleForDisplay().ToString("F1", CultureInfo.InvariantCulture);
        return $"s{settlement}@{match.Politics.SettlementTileOf(settlement)} pop={match.Politics.SettlementPopulationOf(settlement)} " +
            $"defense={defense.Palisade}/{defense.Hillfort}/{defense.StoneWalls} build={build}:{share.ToDoubleForDisplay():P0} " +
            $"activeDefense={activeDefenseProgress} stores[wood/stone/clay]={wood}/{stone}/{clay}";
    }
}
