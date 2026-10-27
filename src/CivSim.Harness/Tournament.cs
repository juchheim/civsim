using System;
using System.Collections.Generic;
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
    public int FissionSplits, FissionPendingSeasons;
    public int OwnedLandShareBasisPoints;
    public int ConquerPlanSeasons, RaidStreakPostureTransitions;
    public int ThreeSeasonNonConquerRaidPostureOpportunities;
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
    public bool TinOrArsenicalRouteByChapterEnd;
    public int DormantBronzeBlunders;
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

internal readonly record struct LegacyMatchOutcome(double TeamALegacy, double TeamBLegacy, long FinalTick, int SeasonsProcessed, ulong Checksum);

/// <summary>Runs matches headless and scores them. The harness may use doubles and threads; the simulation does not.</summary>
internal static class Tournament
{
    public const ulong BaseSeed = 20261019;
    public static readonly int[] TeamASeats = { 0, 3 };
    public static readonly int[] TeamBSeats = { 1, 2 };

    public static MatchOutcome Play(ContentPack pack, ulong seed, Player[] seats, EconomyArchetype[] archetypes, int minutes,
        bool captureFirstRaceDiagnostics = false, Fix64? rivalPaceOverride = null, int? firstsEtaPessimismSeasons = null,
        int? warlordNonConquerRaidPostureStreak = null)
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
        GeneratedWorld world = WorldGenerator.Generate(seed);
        var controls = new CivControl[seats.Length];
        for (int i = 0; i < seats.Length; i++) controls[i] = seats[i].Control;
        MatchModel match = MatchModel.Create(seed, pack, world, archetypes, controls);
        if (rivalPaceOverride is not null || firstsEtaPessimismSeasons is not null || warlordNonConquerRaidPostureStreak is not null)
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
                }
        // Tournament scoring and telemetry use the final state, not the periodic hash history.
        var runner = new SimRunner(match, int.MaxValue);
        int civs = seats.Length;
        var outcome = new MatchOutcome
        {
            Seed = seed, RivalPaceOverride = rivalPaceOverride, FirstsEtaPessimismOverride = firstsEtaPessimismSeasons,
            WarlordNonConquerRaidPostureStreakOverride = warlordNonConquerRaidPostureStreak,
            Seats = seats, Civs = new CivOutcome[civs]
        };
        var firstRaceHistory = new List<FirstRaceEstimate[]>();
        for (int c = 0; c < civs; c++) outcome.Civs[c] = new CivOutcome { Civ = c };
        ResearchCatalog catalog = match.Research.Catalog;
        int[] keys = { catalog.IndexOf("KEY-1"), catalog.IndexOf("KEY-2"), catalog.IndexOf("KEY-3") };
        int gov02 = catalog.IndexOf("GOV-02");
        int met03 = catalog.IndexOf("MET-03"), met04 = catalog.IndexOf("MET-04"), met05 = catalog.IndexOf("MET-05");
        int min04 = catalog.IndexOf("MIN-04");
        var stalled = new bool[civs * 3];
        var key3Season = new int[civs]; Array.Fill(key3Season, -1);
        var bronzePoorMeasured = new bool[civs];
        var dormantOptionSeasons = new int[civs];
        var dormantReported = new bool[civs];
        var fallbackRouteAfterTinPoor = new bool[civs];
        var bronzeRaidBelieved = new bool[civs][];
        for (int c = 0; c < civs; c++) bronzeRaidBelieved[c] = new bool[civs];
        var siegeBlunderTracker = new SiegeBlunderTracker();
        var pendingRaidPredictions = new Dictionary<(int Civ, int Target), Queue<Fix64>>();
        var nakedTinCaravans = new NakedTinCaravanTracker[civs];
        for (int c = 0; c < civs; c++) nakedTinCaravans[c] = new NakedTinCaravanTracker();
        var mandateOverflowStreak = new int[civs];
        var mandateOverflowReported = new bool[civs];
        var fissionsSeen = new int[match.Economy.SettlementCount];
        for (int s = 0; s < fissionsSeen.Length; s++) fissionsSeen[s] = match.Economy.Settlement(s).Fissions;
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
        for (long t = 0; t < ticks; t += 150, season++)
        {
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
                    bronzeRaidBelieved[c][other] = M10TelemetryRules.BelievesBronze(rivals[other], met04, met05);
                }
            }
            int politicsEventsBefore = match.Politics.Events.Count;
            int aiLogEntriesBefore = match.Ai?.Log.Entries.Count ?? 0;
            var foodStoresBeforeAdvance = new Fix64[match.Economy.SettlementCount];
            var settlementOwnersBeforeAdvance = new int[match.Economy.SettlementCount];
            var settlementTilesBeforeAdvance = new int[match.Economy.SettlementCount];
            var settlementIdsBeforeAdvance = new int[match.Economy.SettlementCount];
            for (int s = 0; s < foodStoresBeforeAdvance.Length; s++)
            {
                SettlementView view = match.Economy.Settlement(s);
                foodStoresBeforeAdvance[s] = view.FoodStores;
                settlementOwnersBeforeAdvance[s] = match.Politics.SettlementCivOf(s);
                settlementTilesBeforeAdvance[s] = view.SiteTile;
                settlementIdsBeforeAdvance[s] = view.Id;
            }
            var defensesBeforeAdvance = new Defenses[match.Politics.SettlementTotal];
            for (int s = 0; s < defensesBeforeAdvance.Length; s++)
                defensesBeforeAdvance[s] = match.Politics.MilitaryOf(s).Defenses;
            FirstRaceEstimate[] firstRaceEstimates = captureFirstRaceDiagnostics
                ? CaptureFirstRaceEstimates(match, seats, catalog.Count)
                : Array.Empty<FirstRaceEstimate>();
            if (captureFirstRaceDiagnostics)
            {
                firstRaceHistory.Add(firstRaceEstimates);
                if (firstRaceHistory.Count > 20) firstRaceHistory.RemoveAt(0);
            }
            int researchEventsBeforeAdvance = match.Research.Events.Count;
            runner.Advance(Math.Min(150, ticks - t));

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
                        if (aiSeat.Strategist is Strategist strategist && strategist.Plan == GrandPlan.Conquer)
                            outcome.Civs[c].ConquerPlanSeasons++;
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

            // A7 starvation uses stores held at the start of the season, before consumption and deaths.
            // A sister is operationally reachable within 15 hexes (the default inter-settlement sharing
            // radius), or 20 when GOV-04 has extended food redistribution for that civilization.
            for (int d = 0; d < match.Economy.LastDeathEventCount; d++)
            {
                EconomyDeathEvent death = match.Economy.LastDeathEvent(d);
                if (death.Cause != EconomyDeathCause.Starvation || (uint)death.Civilization >= (uint)civs ||
                    seats[death.Civilization].Control.Kind != ControlKind.Ai) continue;
                int own = -1;
                for (int s = 0; s < foodStoresBeforeAdvance.Length; s++)
                    if (settlementIdsBeforeAdvance[s] == death.SettlementId) { own = s; break; }
                bool ownFood = own >= 0 && foodStoresBeforeAdvance[own] > Fix64.Zero;
                bool sisterFood = false;
                int reach = match.Research.IsDone(death.Civilization, catalog.IndexOf("GOV-04")) ? 20 : 15;
                if (own >= 0)
                    for (int s = 0; s < foodStoresBeforeAdvance.Length; s++)
                        if (s != own && M10TelemetryRules.IsReachableSisterFood(
                            settlementOwnersBeforeAdvance[s] == death.Civilization,
                            foodStoresBeforeAdvance[s] > Fix64.Zero,
                            HexGrid.Distance(settlementTilesBeforeAdvance[s], settlementTilesBeforeAdvance[own], world.Width), reach))
                        { sisterFood = true; break; }
                if (M10TelemetryRules.HasReachableFood(ownFood, sisterFood))
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
            var recoveryTinOrder = new bool[civs];
            var recoveryArsenicOrder = new bool[civs];
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
                if (!M10TelemetryRules.IsSettlementRaidLaunch(ev) || ev.Other < 0 || ev.Other >= civs || ev.Civ >= civs ||
                    seats[ev.Other].Control.Kind != ControlKind.Ai || seats[ev.Civ].Control.Kind != ControlKind.Ai ||
                    !M10TelemetryRules.TryMeasureBelievedBronzeRaid(
                        outcome.Civs[ev.Other].BelievedBronzeRaidMeasured,
                        bronzeRaidBelieved[ev.Other][ev.Civ], ev.B, defensesBeforeAdvance,
                        out bool defendedBeforeRaid)) continue;
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

                    o.MandateSeasonsAtCap = match.Politics.MandateSeasonsAtCap(c);
                    mandateOverflowStreak[c] = M10B30Rules.AdvanceMandateOverflowStreak(mandateOverflowStreak[c],
                        positiveDecreeCandidate[c], match.Research.Mandate(c), match.Research.MandateCap(c));
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
                    int adultWorkers = 0, idleAdults = 0;
                    for (int f = 0; f < match.Economy.FigureCount; f++)
                    {
                        FigureView figure = match.Economy.Figure(f);
                        if (figure.Civilization != c || figure.Age < 14 || figure.Age >= 45) continue;
                        adultWorkers++;
                        if (figure.WorkThisSeason <= Fix64.Zero) idleAdults++;
                    }
                    bool deficit = false;
                    for (int s = 0; s < match.Economy.SettlementCount; s++)
                    {
                        SettlementView settlement = match.Economy.Settlement(s);
                        if (settlement.Civilization == c && settlement.LastFoodSecurity < Fix64.One)
                        { deficit = true; break; }
                    }
                    if (M10TelemetryRules.IsIdleLaborBlunder(idleAdults, adultWorkers, deficit)) o.IdleLaborSeasons++;
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
            for (int c = 0; c < civs; c++)
                if (seats[c].Control.Kind == ControlKind.Ai)
                    outcome.Civs[c].PlanSwitchesInThreeYears = M10TelemetryRules.MaxOrdinaryPlanSwitchesInThreeYears(ai.Log.Entries, c);

            foreach (AiDecision d in ai.Log.Entries)
            {
                if ((uint)d.Civ >= (uint)civs || seats[d.Civ].Control.Kind != ControlKind.Ai) continue;
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
        return outcome;
    }

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
        public int RaidsA, RaidsB;
        public int KeystoneStallsA, IdleSlotSeasonsA, MaxPlanSwitchesA;
        public int PlanDitherMatchesA;
        public double MeanKey1A = double.NaN, MeanKey1B = double.NaN;
        public readonly List<MatchOutcome> Outcomes = new();
        public double WinRateA => Matches == 0 ? 0 : WinsA / Matches;
    }

    public static DuelResult Duel(ContentPack pack, string teamA, string teamB, int seeds, int minutes, EconomyArchetype archetype,
        ulong firstSeed = BaseSeed, bool captureFirstRaceDiagnostics = false, Fix64? rivalPaceOverride = null,
        int? firstsEtaPessimismSeasons = null, int? warlordNonConquerRaidPostureStreak = null)
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
                firstsEtaPessimismSeasons, warlordNonConquerRaidPostureStreak), swapped);
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
    public static (double[] Wins, int[] Appearances) FreeForAll(ContentPack pack, string level, int seeds, int minutes, ulong firstSeed = BaseSeed)
        => FreeForAll(pack, level, seeds, minutes, out _, firstSeed);

    /// <summary>The free-for-all, also returning every civilization's outcome with its archetype for the per-archetype report.</summary>
    public static (double[] Wins, int[] Appearances) FreeForAll(ContentPack pack, string level, int seeds, int minutes,
        out List<(EconomyArchetype Archetype, CivOutcome Outcome, bool Won)> civs, ulong firstSeed = BaseSeed)
    {
        EconomyArchetype[] all = { EconomyArchetype.Builder, EconomyArchetype.Grower, EconomyArchetype.Wanderer, EconomyArchetype.Warlord, EconomyArchetype.Sage };
        var jobs = new List<(ulong Seed, int Rotation)>();
        for (int i = 0; i < seeds; i++) for (int rot = 0; rot < 5; rot++) jobs.Add((firstSeed + (ulong)i, rot));
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
            outcomes[j] = (Play(pack, seed, seats, seating, minutes), seating);
        });
        var wins = new double[5];
        var apps = new int[5];
        civs = new List<(EconomyArchetype, CivOutcome, bool)>();
        foreach ((MatchOutcome m, EconomyArchetype[] seating) in outcomes)
        {
            double best = double.MinValue; int bestSeat = 0;
            for (int s = 0; s < 4; s++) if (m.Civs[s].Legacy > best) { best = m.Civs[s].Legacy; bestSeat = s; }
            for (int s = 0; s < 4; s++) apps[Array.IndexOf(all, seating[s])]++;
            wins[Array.IndexOf(all, seating[bestSeat])]++;
            for (int s = 0; s < 4; s++) civs.Add((seating[s], m.Civs[s], s == bestSeat));
        }
        return (wins, apps);
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
}
