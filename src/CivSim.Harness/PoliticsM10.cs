using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

/// <summary>Chapter-scale acceptance for M10 politics gates B17–B20 (docs/MVP/22 §16).</summary>
internal static class PoliticsM10
{
    private static readonly object ProgressOutputLock = new();

    internal readonly record struct B17ReportMetrics(double SettlementsPerCiv, double ClaimedLandShare, double OwnKey4BorderTouchRate);

    internal static int ExpectedBorderCivSamples(int civCount) => civCount;
    internal static double ElapsedMinutes(long tick) => tick / 600.0;

    internal static B17ReportMetrics SummarizeB17(double settlementTotal, double claimedLandShareTotal,
        int completedCivs, int touchingCivs, int sampledCivs)
        => new(
            settlementTotal / Math.Max(1, completedCivs),
            claimedLandShareTotal / Math.Max(1, completedCivs),
            touchingCivs / (double)Math.Max(1, sampledCivs));

    internal static bool TryRecordOwnKey4BorderSample(bool[] sampledCivs, int civ, bool touching, out bool countedTouch)
    {
        if (sampledCivs[civ])
        {
            countedTouch = false;
            return false;
        }

        sampledCivs[civ] = true;
        countedTouch = touching;
        return true;
    }

    internal static bool IsE3ExposureTick(long tick, long key3Tick, long key4Tick)
        => key3Tick >= 0 && tick >= key3Tick && (key4Tick < 0 || tick <= key4Tick);

    private sealed class Sample
    {
        public ulong Seed;
        public double Minute, Key4LatestMinute;
        public int TownCivs, CityCivs, Civs, TouchingBorderCivs, BorderCivSamples, Key4Completed;
        public int ChapterEndRaids, ChapterEndWars, ChapterEndCaptures, Secessions, CascadeMatches;
        public int E3Civs, E3Completed, E3Incomplete;
        public bool ChapterEndSampled;
        public AiDecision[] TraceDecisions = Array.Empty<AiDecision>();
        public string[] TraceEvents = Array.Empty<string>();
        public string[] TraceCivics = Array.Empty<string>();
        public string[] TraceChapterEndCivics = Array.Empty<string>();
        public string TraceChapterEndTiming = string.Empty;
        public string[] TraceTechs = Array.Empty<string>();
        public string[] TraceCityHistory = Array.Empty<string>();
        public string[] TraceLandClaims = Array.Empty<string>();
        public string[] TraceCampSightings = Array.Empty<string>();
        public string[] TraceB19Exposures = Array.Empty<string>();
        public string[] TraceB19History = Array.Empty<string>();
        public string[] TraceKey4Blockers = Array.Empty<string>();
        public string[] Key4IncompleteBlockers = Array.Empty<string>();
        public string[] TraceResearchSnapshots = Array.Empty<string>();
        public string[] TraceResearchMilestones = Array.Empty<string>();
        public string[] TraceLateCatchupPicks = Array.Empty<string>();
        public string[] TracePreCatchupResearchPicks = Array.Empty<string>();
        public string[] TraceDemography = Array.Empty<string>();
        public double Claimed, MeanSettlements;
        public double EndDominion, EndMastery, EndFirsts, EndWonders;
        public double CohesionSum;
        public int CohesionCount;
    }

    public static int Execute(string packPath, ulong firstSeed, int seeds, int minutes,
        ulong traceSeed = ulong.MaxValue, bool traceAllSeeds = false)
    {
        if (seeds <= 0 || minutes <= 0) throw new ArgumentException("--seeds and --minutes must be positive.");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        bool fullSample = seeds >= 24 && minutes >= 64;
        int townCivs = 0, cityCivs = 0, touchingBorderCivs = 0, borderCivSamples = 0, raidMatches = 0, warMatches = 0, captureMatches = 0;
        int civs = 0, cascadeMatches = 0, totalSecessions = 0, e3Civs = 0, e3Completed = 0, e3Incomplete = 0, cohesionCount = 0;
        double claimed = 0, settlements = 0, cohesionSum = 0;
        double endDominion = 0, endMastery = 0, endFirsts = 0, endWonders = 0;
        int chapterEndSamples = 0, chapterEndRaidTotal = 0;
        int key4Completed = 0;
        Console.WriteLine($"M10 politics: {seeds} seed(s) from {firstSeed}, up to {minutes} minutes; 4 Chieftain AI, Balanced archetype");
        var results = new Sample[seeds];
        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
        Parallel.For(0, seeds, options, i =>
        {
            ulong seed = firstSeed + (ulong)i;
            void Progress(string message)
            {
                lock (ProgressOutputLock)
                {
                    Console.Error.WriteLine(message);
                    Console.Error.Flush();
                }
            }

            Sample result = Play(seed, pack, minutes, traceAllSeeds || seed == traceSeed, Progress);
            results[i] = result;
            Progress($"[done] seed {result.Seed}: KEY-4 {result.Key4Completed}/{result.Civs}; own-KEY-4 border contact " +
                $"{result.TouchingBorderCivs}/{result.BorderCivSamples}; end {result.Minute:F1} min; " +
                $"Town {result.TownCivs}/{result.Civs}, City {result.CityCivs}/{result.Civs}");
        });
        for (int i = 0; i < seeds; i++)
        {
            ulong seed = firstSeed + (ulong)i;
            Sample r = results[i];
            townCivs += r.TownCivs; cityCivs += r.CityCivs; civs += r.Civs;
            touchingBorderCivs += r.TouchingBorderCivs; borderCivSamples += r.BorderCivSamples;
            key4Completed += r.Key4Completed;
            chapterEndRaidTotal += r.ChapterEndRaids; if (r.ChapterEndRaids > 0) raidMatches++;
            if (r.ChapterEndWars > 0) warMatches++;
            if (r.ChapterEndCaptures > 0) captureMatches++;
            claimed += r.Claimed;
            endDominion += r.EndDominion; endMastery += r.EndMastery; endFirsts += r.EndFirsts; endWonders += r.EndWonders;
            settlements += r.MeanSettlements;
            totalSecessions += r.Secessions; cascadeMatches += r.CascadeMatches;
            e3Civs += r.E3Civs; e3Completed += r.E3Completed; e3Incomplete += r.E3Incomplete;
            cohesionSum += r.CohesionSum; cohesionCount += r.CohesionCount;
            if (r.ChapterEndSampled) chapterEndSamples++;
            Console.WriteLine($"seed {r.Seed}: KEY-4 {r.Key4Completed}/{r.Civs}, latest @ {r.Key4LatestMinute:F1}; run ended @ {r.Minute:F1}; mean KEY-4 claim {r.Claimed / Math.Max(1, r.Key4Completed):P1}; chapter-end raids {r.ChapterEndRaids}, wars {r.ChapterEndWars}, captures {r.ChapterEndCaptures}, secessions in KEY-3→KEY-4 {r.Secessions}");
            foreach (string blocker in r.Key4IncompleteBlockers) Console.WriteLine($"  {blocker}");
            if (traceAllSeeds || seed == traceSeed) PrintTraceSummary(r);
        }
        double townRate = townCivs / (double)Math.Max(1, civs), cityRate = cityCivs / (double)Math.Max(1, civs);
        B17ReportMetrics b17Metrics = SummarizeB17(settlements, claimed, key4Completed, touchingBorderCivs, borderCivSamples);
        double claimMean = b17Metrics.ClaimedLandShare, settlementMean = b17Metrics.SettlementsPerCiv;
        double warRate = warMatches / (double)seeds, captureRate = captureMatches / (double)seeds;
        double secessionsPerCivPerE3 = totalSecessions / (double)Math.Max(1, e3Completed);
        double cohesionMean = cohesionSum / Math.Max(1, cohesionCount);
        double endLegacy = endDominion + endMastery + endFirsts + endWonders;
        double endDomPct = 100 * endDominion / Math.Max(1e-9, endLegacy), endMasPct = 100 * endMastery / Math.Max(1e-9, endLegacy);
        double endFirstPct = 100 * endFirsts / Math.Max(1e-9, endLegacy), endWonderPct = 100 * endWonders / Math.Max(1e-9, endLegacy);
        Console.WriteLine($"B17 KEY-4: {settlementMean:F1} settlements/civ, claimed land {claimMean:P1}, paired borders at each civ's own KEY-4 {touchingBorderCivs}/{borderCivSamples} civs sampled; KEY-4 completed {key4Completed}/{seeds * 4}");
        Console.WriteLine($"B17 chapter end: >=1 Town {townRate:P1}, >=1 City {cityRate:P1} ({chapterEndSamples}/{seeds} chapter-end samples)");
        Console.WriteLine($"B18 chapter-end Legacy split: Dominion {endDomPct:F1}%, Mastery {endMasPct:F1}%, Firsts {endFirstPct:F1}%, Wonders {endWonderPct:F1}%");
        Console.WriteLine($"B19 KEY-3→KEY-4 exposure: cohesion mean {cohesionMean:F1}, secessions/civ/E3 {secessionsPerCivPerE3:F2}, exposures complete {e3Completed}/{e3Civs} (incomplete {e3Incomplete}), cascades {cascadeMatches}/{seeds}");
        Console.WriteLine($"B20 chapter end: raids mean {chapterEndRaidTotal / (double)seeds:F1}, matches with war {warRate:P1}, with capture {captureRate:P1}");
        if (!fullSample)
        {
            Console.WriteLine("M10 politics DIAGNOSTIC ONLY: acceptance requires 24 seeds and a 64-minute chapter horizon.");
            return 2;
        }
        bool b17 = settlementMean >= 10 && settlementMean <= 13 && townRate >= .75 && cityRate >= .25 && claimMean >= .30 && claimMean <= .42 && b17Metrics.OwnKey4BorderTouchRate >= .75 && key4Completed == seeds * 4 && chapterEndSamples == seeds;
        bool b18 = endDomPct >= 45 && endDomPct <= 55 && endMasPct >= 25 && endMasPct <= 35 && endFirstPct >= 12 && endFirstPct <= 20 && endWonderPct <= 8 && chapterEndSamples == seeds;
        bool b19 = e3Civs == seeds * 4 && e3Completed == seeds * 4 && e3Incomplete == 0 &&
            secessionsPerCivPerE3 >= .03 && secessionsPerCivPerE3 <= .08 && cascadeMatches / (double)seeds < .02 && cohesionMean >= 60 && cohesionMean <= 80;
        bool b20 = chapterEndRaidTotal / (double)seeds >= 3 && chapterEndRaidTotal / (double)seeds <= 12 && warRate >= .5 && captureRate >= .2 && captureRate <= .5 && chapterEndSamples == seeds;
        Console.WriteLine($"B17 {(b17 ? "PASS" : "FAIL")}  B18 {(b18 ? "PASS" : "FAIL")}  B19 {(b19 ? "PASS" : "FAIL")}  B20 {(b20 ? "PASS" : "FAIL")}");
        return b17 && b18 && b19 && b20 ? 0 : 1;
    }

    private static Sample Play(ulong seed, ContentPack pack, int minutes, bool trace, Action<string>? progress = null)
    {
        var archetypes = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        var controls = new CivControl[4];
        for (int c = 0; c < controls.Length; c++) controls[c] = CivControl.Ai(Difficulty.Chieftain, EconomyArchetype.Balanced);
        GeneratedWorld world = WorldGenerator.Generate(seed);
        MatchModel match = MatchModel.Create(seed, pack, world, archetypes, controls, playerCouncil: true);
        var runner = new SimRunner(match);
        var result = new Sample { Seed = seed, Civs = 4 };
        ResearchSystem research = match.Research;
        LivingEconomyModel economy = match.Economy;
        PoliticsSystem politics = match.Politics;
        int key3 = research.Catalog.IndexOf("KEY-3"), key4 = research.Catalog.IndexOf("KEY-4");
        long limit = minutes * 600L;
        int eventCursor = 0;
        bool[] cascade = new bool[4];
        bool[] key4Sampled = new bool[4], key4TerritorySampled = new bool[4];
        long[] key3Tick = new long[4], key4Tick = new long[4];
        Array.Fill(key3Tick, -1); Array.Fill(key4Tick, -1);
        int[,] secessionBySeason = new int[4, 8];
        var cityHistory = trace ? new List<string>() : null;
        int lastCityHistorySeason = -1;
        int season = 0;
        bool[] pairTraceSampled = new bool[2];
        bool[] key4BorderSampled = new bool[4];
        bool endSampled = false;
        var landClaimTrace = trace ? new List<string>() : null;
        var capturedSettlements = trace ? new List<(int Civ, int Settlement, long Tick)>() : null;
        var b19History = trace ? new List<string>() : null;
        var researchSnapshots = trace ? new List<string>() : null;
        int lastB19HistorySeason = -1;
        int firstPublicRivalEra4Season = -1;
        int firstPublicRivalEra3Season = -1;
        int lastResearchSnapshotSeason = -1;
        int lastMandateTraceSeason = -1;
        bool gov6EligibilitySampled = false, gov6SlotSampled = false;
        bool gov6CompletionSampled = false;
        string[] milestoneProjectIds = { "KEY-2", "KEY-3", "MET-08", "TRD-05", "SOC-08", "SOC-09", "GOV-05", "GOV-06", "KEY-4" };
        int[] milestoneProjects = milestoneProjectIds.Select(research.Catalog.IndexOf).ToArray();
        int[,] firstAvailableSeason = new int[4, milestoneProjects.Length];
        int[,] firstSlotSeason = new int[4, milestoneProjects.Length];
        int[,] completionSeason = new int[4, milestoneProjects.Length];
        for (int c = 0; c < 4; c++)
            for (int i = 0; i < milestoneProjects.Length; i++)
                firstAvailableSeason[c, i] = firstSlotSeason[c, i] = completionSeason[c, i] = -1;
        bool key2ComparisonSampled = false;
        long foundProbeTick = -1;
        var demographyHistory = trace ? new List<string>() : null;
        int[] birthsSinceDemographySample = new int[4];
        int[,,] deathsSinceDemographySample = new int[4, 3, (int)EconomyDeathCause.Senescence + 1];
        long priorNextFigureId = economy.NextFigureId;
        var previousFissions = new Dictionary<int, int>();
        var previousFissionPending = new Dictionary<int, bool>();
        for (int s = 0; s < economy.SettlementCount; s++)
        {
            var lite = economy.SettlementLite(s);
            previousFissions[lite.Id] = lite.Fissions;
            previousFissionPending[lite.Id] = economy.SettlementFissionPending(s);
        }

        string DemographySnapshot(int atSeason)
        {
            string[] ageNames = { "child", "adult", "elder" };
            var lines = new List<string>();
            for (int c = 0; c < 4; c++)
            {
                int births = birthsSinceDemographySample[c];
                int totalPop = 0, children = 0, adults = 0, elders = 0;
                int totalDeaths = 0;
                var causesByAge = new List<string>();
                for (int ageBand = 0; ageBand < 3; ageBand++)
                for (int cause = 0; cause <= (int)EconomyDeathCause.Senescence; cause++)
                {
                    int count = deathsSinceDemographySample[c, ageBand, cause];
                    totalDeaths += count;
                    if (count > 0) causesByAge.Add($"{ageNames[ageBand]}-{(EconomyDeathCause)cause}:{count}");
                }
                var settlementLines = new List<string>();
                for (int s = 0; s < economy.SettlementCount; s++)
                {
                    var lite = economy.SettlementLite(s);
                    if (lite.Civilization != c || politics.IsFreeFolk(s)) continue;
                    SettlementView view = economy.Settlement(s);
                    int pop = economy.Population(lite.Id);
                    int childCount = economy.AgeBandPopulation(s, 0);
                    int adultCount = economy.AgeBandPopulation(s, 1);
                    int elderCount = economy.AgeBandPopulation(s, 2);
                    totalPop += pop; children += childCount; adults += adultCount; elders += elderCount;
                    CivicReadout civic = economy.Civic(s);
                    (bool cohesionLive, int cohesion) = economy.CohesionStatus(s);
                    Fix64 thresholdCap = civic.Markets > 0
                        ? Fix64.Min(view.FoodCap, view.GroupCap)
                        : view.EffectivePopulationCap;
                    Fix64 trigger = cohesionLive && cohesion < 35 ? Fix64.Parse("0.75") : Fix64.Parse("0.85");
                    Fix64 normalFissionThreshold = thresholdCap * trigger * economy.FissionScaleOf(c);
                    bool pressure = Fix64.FromInt(pop) >= normalFissionThreshold ||
                        Fix64.FromInt(pop) >= view.LateWinterFissionThreshold;
                    settlementLines.Add($"s{lite.Id} pop{pop} age{childCount}/{adultCount}/{elderCount} " +
                        $"cap(food/house/group)={view.FoodCap.ToDoubleForDisplay():F1}/{view.HousingCap.ToDoubleForDisplay():F1}/{view.GroupCap.ToDoubleForDisplay():F1} " +
                        $"foodSec={view.LastFoodSecurity.ToDoubleForDisplay():F2} winter={view.LateWinterFoodForecast.ToDoubleForDisplay():F2}/share{view.LastLateWinterFoodShare.ToDoubleForDisplay():F2} " +
                        $"fission(normal/winter)={normalFissionThreshold.ToDoubleForDisplay():F1}/{view.LateWinterFissionThreshold.ToDoubleForDisplay():F1} pressure={pressure} " +
                        $"pending={view.FissionPending} fissions={view.Fissions} since={economy.SeasonsSinceFission(s)} C={cohesion}{(cohesionLive ? "live" : "off")} " +
                        $"siteFood={economy.SiteFood(view.SiteTile).ToDoubleForDisplay():F1}");
                }
                lines.Add($"season{atSeason} civ{c} births={births} deaths={totalDeaths}[{string.Join(",", causesByAge)}] " +
                    $"pop={totalPop} age={children}/{adults}/{elders} " +
                    string.Join(" | ", settlementLines));
            }
            Array.Clear(birthsSinceDemographySample);
            Array.Clear(deathsSinceDemographySample);
            return string.Join(Environment.NewLine + "  demography ", lines);
        }

        if (trace)
        {
            var startingSites = new List<string>();
            for (int c = 0; c < 4; c++)
            {
                int tileIndex = world.Starts[c].Tile;
                WorldTile tile = world.TileAt(tileIndex);
                FoodIncomeBreakdown income = economy.SustainableFoodIncomeBreakdown(c);
                SettlementView view = economy.Settlement(c);
                startingSites.Add($"civ{c} tile{tileIndex} {tile.Climate}/{tile.Biome} rain{tile.Rainfall} soil{tile.SoilFertility} " +
                    $"forage{tile.ForageScore} habitable{tile.HabitableWeight} flags{tile.Flags} siteFood={economy.SiteFood(tileIndex).ToDoubleForDisplay():F1} " +
                    $"foodCap={view.FoodCap.ToDoubleForDisplay():F1} annualFood={income.Total.ToDoubleForDisplay():F1} " +
                    $"(plants{income.Plants.ToDoubleForDisplay():F1},meat{income.Meat.ToDoubleForDisplay():F1},fish{income.Fish.ToDoubleForDisplay():F1},grain{income.Grain.ToDoubleForDisplay():F1},fruit{income.Fruit.ToDoubleForDisplay():F1})");
            }
            demographyHistory!.Add("starting sites: " + string.Join("; ", startingSites));
        }

        string ResearchSnapshot(int atSeason, string reason)
        {
            int gov6 = research.Catalog.IndexOf("GOV-06");
            int key4 = research.Catalog.IndexOf("KEY-4");
            string ProjectStatus(int project)
            {
                if (project < 0) return "missing";
                Fix64 cost = research.EffectiveCost(3, project);
                Fix64 progress = research.Progress(3, project);
                string slot = string.Join(",", Enumerable.Range(0, CivResearch.SlotCount)
                    .Where(s => research.Slot(3, s) == project).Select(s => $"slot{s}"));
                if (slot.Length == 0) slot = "no-slot";
                return $"{research.Catalog.Projects[project].Id} {research.State(3, project)} " +
                    $"{progress.ToDoubleForDisplay():F1}/{cost.ToDoubleForDisplay():F1} ({(cost.Raw > 0 ? progress.ToDoubleForDisplay() / cost.ToDoubleForDisplay() : 0):P0}) {slot}";
            }

            bool[]? closure = research.Catalog.ClosureOf(ResearchSystem.LastResearchEra);
            string breadthOpen = string.Join(",", Enumerable.Range(0, research.Catalog.Count)
                .Where(p => research.Catalog.Projects[p].Numeric &&
                    research.Catalog.Projects[p].Era == ResearchSystem.LastResearchEra && !research.IsDone(3, p))
                .Select(p => research.Catalog.Projects[p].Id));
            string eligible = closure is null ? "none" : string.Join(",", Enumerable.Range(0, research.Catalog.Count)
                .Where(p => closure[p] && !research.IsDone(3, p) &&
                    (research.State(3, p) is ProjectState.Available or ProjectState.Slotted))
                .Select(ProjectStatus));
            string slots = string.Join(";", Enumerable.Range(0, CivResearch.SlotCount).Select(s =>
            {
                int p = research.Slot(3, s);
                return $"{s}:{(p < 0 ? "-" : research.Catalog.Projects[p].Id)}";
            }));
            string queue = string.Join(",", research.Queue(3).Select(p => research.Catalog.Projects[p].Id));
            string rivals = match.Ai?.SeatOf(3)?.Belief is { } belief
                ? string.Join(",", belief.Rivals.Where(r => r.Civ != 3).Select(r => $"c{r.Civ}=E{r.Era}"))
                : "unavailable";
            return $"{reason} season{atSeason}: civ3 E{research.Era(3)} breadth {research.DoneInEra(3, ResearchSystem.LastResearchEra)}/21 " +
                $"rivals[{rivals}] Insight/yr {research.InsightPerYear(3).ToDoubleForDisplay():F1}; " +
                $"breadth open[{breadthOpen}]; GOV-06 {ProjectStatus(gov6)}; KEY-4 {ProjectStatus(key4)} queued={research.Queue(3).Contains(key4)}; " +
                $"focus[{slots}] queue[{queue}]; eligible closure[{eligible}]";
        }

        string Key3Snapshot(int atSeason)
        {
            ProjectDef key3Def = research.Catalog.Projects[key3];
            string missing = string.Join(",", key3Def.Requires.Where(p => !research.IsDone(3, p))
                .Select(p => research.Catalog.Projects[p].Id));
            bool[]? closure = research.Catalog.ClosureOf(key3Def.BreadthEra);
            string eligible = closure is null ? "none" : string.Join(",", Enumerable.Range(0, research.Catalog.Count)
                .Where(p => closure[p] && !research.IsDone(3, p) &&
                    (research.State(3, p) is ProjectState.Available or ProjectState.Slotted))
                .Select(p => $"{research.Catalog.Projects[p].Id}:{research.State(3, p)}"));
            string slots = string.Join(";", Enumerable.Range(0, CivResearch.SlotCount).Select(s =>
            {
                int p = research.Slot(3, s);
                return $"{s}:{(p < 0 ? "-" : research.Catalog.Projects[p].Id)}";
            }));
            string rivals = match.Ai?.SeatOf(3)?.Belief is { } belief
                ? string.Join(",", belief.Rivals.Where(r => r.Civ != 3).Select(r => $"c{r.Civ}=E{r.Era}"))
                : "unavailable";
            return $"first rival Era-3 season{atSeason}: civ3 E{research.Era(3)} KEY-3 {research.State(3, key3)} " +
                $"breadth {research.DoneInEra(3, key3Def.BreadthEra)}/{key3Def.BreadthMin} missing[{missing}] " +
                $"rivals[{rivals}] slots[{slots}] available closure[{eligible}]";
        }

        string Key2PeerSnapshot(int atSeason, string reason)
        {
            int key2 = research.Catalog.IndexOf("KEY-2");
            int soc3 = research.Catalog.IndexOf("SOC-03");
            string peers = string.Join("; ", Enumerable.Range(0, 4).Select(c =>
            {
                InsightInputs inputs = research.ForecastInsightInputs(c);
                string completed = research.IsDone(c, key2)
                    ? $"done@season{research.CompletedTick(c, key2) / ResearchSystem.SeasonTicks}"
                    : research.State(c, key2).ToString();
                string roles = inputs.RoleHeads is null ? "none" : string.Join(",", Enumerable.Range(0, Math.Min(inputs.RoleHeads.Length, research.Catalog.Roles.Count))
                    .Where(i => inputs.RoleHeads[i] > Fix64.Zero)
                    .Select(i => $"{research.Catalog.Roles[i].Id}:{inputs.RoleHeads[i].ToDoubleForDisplay():F1}"));
                string engines = string.Join(",", new[] { "GOV-01", "BEL-03", "LRN-02" }
                    .Where(id => research.IsDone(c, research.Catalog.IndexOf(id))));
                string engineStatus = string.Join(",", new[] { "SOC-04", "BEL-03", "LRN-02" }.Select(id =>
                {
                    int project = research.Catalog.IndexOf(id);
                    Fix64 cost = research.EffectiveCost(c, project);
                    Fix64 progress = research.Progress(c, project);
                    string slot = string.Join("/", Enumerable.Range(0, CivResearch.SlotCount)
                        .Where(s => research.Slot(c, s) == project).Select(s => $"S{s}"));
                    return $"{id}:{research.State(c, project)} {progress.ToDoubleForDisplay():F1}/{cost.ToDoubleForDisplay():F1}" +
                        (slot.Length == 0 ? string.Empty : $" {slot}");
                }));
                string slots = string.Join(",", Enumerable.Range(0, CivResearch.SlotCount).Select(s =>
                {
                    int project = research.Slot(c, s);
                    return project < 0 ? $"S{s}:-" : $"S{s}:{research.Catalog.Projects[project].Id}";
                }));
                string settlements = string.Join(" / ", Enumerable.Range(0, economy.SettlementCount)
                    .Where(s => economy.SettlementLite(s).Civilization == c && !politics.IsFreeFolk(s))
                    .Select(s =>
                    {
                        var lite = economy.SettlementLite(s);
                        return $"s{s} pop{economy.Population(lite.Id)} age{economy.AgeBandPopulation(s, 0)}/{economy.AgeBandPopulation(s, 1)}/{economy.AgeBandPopulation(s, 2)} " +
                            $"food{lite.LastFoodSecurity.ToDoubleForDisplay():F2} winter{lite.LateWinterFoodForecast.ToDoubleForDisplay():F2} " +
                            economy.DescribeCivic(s);
                    }));
                return $"civ{c} E{research.Era(c)} KEY-2 {completed}, E2 breadth " +
                    $"{research.DoneInEra(c, ResearchSystem.LastResearchEra - 1)}/20, Insight/yr {research.InsightPerYear(c).ToDoubleForDisplay():F1}, " +
                    $"Commons/yr {research.CommonsPerYear(c).ToDoubleForDisplay():F1}, engine[{engines}] focus[{engineStatus}] slots[{slots}], " +
                    $"adults {inputs.Adults.ToDoubleForDisplay():F1}, elders {inputs.Elders.ToDoubleForDisplay():F1}, settlements {inputs.Settlements}, " +
                    $"hearth share {inputs.HearthShare.ToDoubleForDisplay():P0}, SOC-03 {research.IsDone(c, soc3)}, " +
                    $"era boon {research.ForecastEraBoon(c).ToDoubleForDisplay():F2}, roles[{roles}], settlements[{settlements}]";
            }));
            return $"{reason} season{atSeason}: {peers}";
        }

        while (runner.Tick < limit)
        {
            runner.Advance(150);
            if (progress is not null && runner.Tick % 3000 == 0)
            {
                int key3Count = 0, key4Count = 0;
                for (int c = 0; c < 4; c++)
                {
                    if (research.IsDone(c, key3)) key3Count++;
                    if (research.IsDone(c, key4)) key4Count++;
                }
                progress($"[progress] seed {seed} @ {runner.Tick / 600.0:F1} simulated min: KEY-3 {key3Count}/4, KEY-4 {key4Count}/4");
            }
            int historySeason = (int)(runner.Tick / ResearchSystem.SeasonTicks);
            if (trace && historySeason <= 96)
            {
                for (int figureIndex = 0; figureIndex < economy.FigureCount; figureIndex++)
                {
                    FigureLook figure = economy.LookFigure(figureIndex);
                    if (figure.Id >= priorNextFigureId && figure.Civilization < 4) birthsSinceDemographySample[figure.Civilization]++;
                }
                priorNextFigureId = economy.NextFigureId;
                for (int deathIndex = 0; deathIndex < economy.LastDeathEventCount; deathIndex++)
                {
                    EconomyDeathEvent death = economy.LastDeathEvent(deathIndex);
                    int ageBand = death.Age < 14 ? 0 : death.Age < 45 ? 1 : 2;
                    deathsSinceDemographySample[death.Civilization, ageBand, (int)death.Cause]++;
                    demographyHistory!.Add($"death season{historySeason - 1} civ{death.Civilization} age{death.Age} female={death.Female} " +
                        $"pregnancy={death.PregnancySeasons} cause={death.Cause} totalHazard={death.TotalHazard.ToDoubleForDisplay():F4} " +
                        $"(exposure={death.ExposureHazard.ToDoubleForDisplay():F4}, injury={death.InjuryHazard.ToDoubleForDisplay():F4}, " +
                        $"starvation={death.StarvationHazard.ToDoubleForDisplay():F4}, endemic={death.EndemicHazard.ToDoubleForDisplay():F4})");
                }
                for (int s = 0; s < economy.SettlementCount; s++)
                {
                    var lite = economy.SettlementLite(s);
                    bool pending = economy.SettlementFissionPending(s);
                    int priorFissions = previousFissions.TryGetValue(lite.Id, out int knownFissions) ? knownFissions : lite.Fissions;
                    bool wasPending = previousFissionPending.TryGetValue(lite.Id, out bool knownPending) && knownPending;
                    if (lite.Fissions > priorFissions)
                        demographyHistory!.Add($"fission success season{historySeason} civ{lite.Civilization} s{lite.Id}: {priorFissions}->{lite.Fissions}");
                    if (pending && !wasPending)
                    {
                        SettlementView view = economy.Settlement(s);
                        CivicReadout civic = economy.Civic(s);
                        (bool cohesionLive, int cohesion) = economy.CohesionStatus(s);
                        Fix64 thresholdCap = civic.Markets > 0
                            ? Fix64.Min(view.FoodCap, view.GroupCap)
                            : view.EffectivePopulationCap;
                        Fix64 trigger = cohesionLive && cohesion < 35 ? Fix64.Parse("0.75") : Fix64.Parse("0.85");
                        Fix64 normalThreshold = thresholdCap * trigger * economy.FissionScaleOf(lite.Civilization);
                        demographyHistory!.Add($"fission pending season{historySeason} civ{lite.Civilization} s{lite.Id}: pop{view.Population} " +
                            $"threshold normal/winter={normalThreshold.ToDoubleForDisplay():F1}/{view.LateWinterFissionThreshold.ToDoubleForDisplay():F1}");
                    }
                    if (wasPending && !pending && lite.Fissions == priorFissions)
                        demographyHistory!.Add($"fission pending cleared without daughter by season{historySeason} civ{lite.Civilization} s{lite.Id}");
                    previousFissions[lite.Id] = lite.Fissions;
                    previousFissionPending[lite.Id] = pending;
                }
                if (historySeason % 8 == 0 || historySeason == 96)
                {
                    demographyHistory!.Add(DemographySnapshot(historySeason));
                }
            }
            if (trace && economy.SettlementCount > 6 && historySeason % 4 == 0 && historySeason != lastCityHistorySeason)
            {
                lastCityHistorySeason = historySeason;
                double historyMinute = ElapsedMinutes(runner.Tick);
                if (historyMinute >= 48)
                    for (int s = 0; s < economy.SettlementCount; s++)
                    {
                        var lite = economy.SettlementLite(s);
                        if (politics.IsFreeFolk(s) || economy.Civic(s).Tier < SettlementTier.Town) continue;
                        cityHistory!.Add($"minute {historyMinute:F1} civ{lite.Civilization} fs{lite.LastFoodSecurity.ToDoubleForDisplay():F2} {economy.DescribeCivic(s)}");
                    }
            }
            int currentSeason = (int)(runner.Tick / ResearchSystem.SeasonTicks);
            if (trace && research.MandateUnlocked(3) && currentSeason != lastMandateTraceSeason && currentSeason <= 245)
            {
                lastMandateTraceSeason = currentSeason;
                Belief? belief = match.Ai?.SeatOf(3)?.Belief;
                int frontierSites = 0;
                if (belief is not null)
                {
                    var marked = new bool[belief.Map.TileCount];
                    StrategistTuning.MarkRecentRivalFrontierSites(belief, 3,
                        politics.ClaimRadiusOfEra(research.Era(3)), marked);
                    for (int tile = 0; tile < marked.Length; tile++) if (marked[tile]) frontierSites++;
                }
                AiDecision? chosen = match.Ai?.Log.Entries
                    .Where(d => d.Layer == "Strategist" && d.Civ == 3 && d.Season == currentSeason && d.Kind == "Decree")
                    .Select(d => (AiDecision?)d).FirstOrDefault();
                string chosenText = chosen is { } decree ? $"chosen {(DecreeKind)decree.Subject} cost {politics.DecreeCost(3, (DecreeKind)decree.Subject)}" : "no decree";
                landClaimTrace!.Add($"civ3 Mandate season{currentSeason}: {research.Mandate(3)}/{research.MandateCap(3)}, " +
                    $"earned {research.MandateEarned(3)} spent {research.MandateSpent(3)}, next {research.MandateSeasonsToNext(3)} " +
                    $"(every {research.MandateRechargeSeasons(3)}), frontier marks {frontierSites}; {chosenText}");
            }
            if (trace)
            {
                for (int c = 0; c < 4; c++)
                for (int i = 0; i < milestoneProjects.Length; i++)
                {
                    int project = milestoneProjects[i];
                    ProjectState state = research.State(c, project);
                    if (firstAvailableSeason[c, i] < 0 && (state is ProjectState.Available or ProjectState.Slotted))
                        firstAvailableSeason[c, i] = currentSeason;
                    if (firstSlotSeason[c, i] < 0 && Enumerable.Range(0, CivResearch.SlotCount).Any(s => research.Slot(c, s) == project))
                        firstSlotSeason[c, i] = currentSeason;
                    if (completionSeason[c, i] < 0 && research.IsDone(c, project))
                        completionSeason[c, i] = (int)(research.CompletedTick(c, project) / ResearchSystem.SeasonTicks);
                }
            }
            if (trace && firstPublicRivalEra4Season < 0 && match.Ai?.SeatOf(3)?.Belief is { } researchBelief &&
                researchBelief.Rivals.Any(r => r.Civ != 3 && r.Era >= ResearchSystem.LastResearchEra + 1))
            {
                firstPublicRivalEra4Season = currentSeason;
                researchSnapshots!.Add(ResearchSnapshot(currentSeason, "first public rival Era-4"));
            }
            if (trace && firstPublicRivalEra3Season < 0 && match.Ai?.SeatOf(3)?.Belief is { } key3Belief &&
                key3Belief.Rivals.Any(r => r.Civ != 3 && r.Era >= ResearchSystem.LastResearchEra))
            {
                firstPublicRivalEra3Season = currentSeason;
                researchSnapshots!.Add(Key3Snapshot(currentSeason));
                researchSnapshots.Add(Key2PeerSnapshot(currentSeason, "KEY-2 peer comparison at first rival Era-3"));
            }
            int key2Project = research.Catalog.IndexOf("KEY-2");
            if (trace && !key2ComparisonSampled && research.IsDone(3, key2Project))
            {
                key2ComparisonSampled = true;
                researchSnapshots!.Add(Key2PeerSnapshot(currentSeason, "KEY-2 peer comparison at civ3 completion"));
            }
            if (trace && currentSeason == 256 && lastResearchSnapshotSeason != currentSeason)
            {
                lastResearchSnapshotSeason = currentSeason;
                researchSnapshots!.Add(ResearchSnapshot(currentSeason, "deadline probe"));
            }
            int gov6Project = research.Catalog.IndexOf("GOV-06");
            if (trace && !gov6EligibilitySampled &&
                (research.State(3, gov6Project) is ProjectState.Available or ProjectState.Slotted))
            {
                gov6EligibilitySampled = true;
                researchSnapshots!.Add(ResearchSnapshot(currentSeason, "GOV-06 first eligible"));
            }
            if (trace && !gov6SlotSampled && research.State(3, gov6Project) == ProjectState.Slotted)
            {
                gov6SlotSampled = true;
                researchSnapshots!.Add(ResearchSnapshot(currentSeason, "GOV-06 first slotted"));
            }
            if (trace && !gov6CompletionSampled && research.IsDone(3, gov6Project))
            {
                gov6CompletionSampled = true;
                researchSnapshots!.Add(ResearchSnapshot(currentSeason, "GOV-06 completion"));
            }
            if (trace && currentSeason is >= 136 and <= 144)
            {
                int key3Project = research.Catalog.IndexOf("KEY-3");
                int trade3Project = research.Catalog.IndexOf("TRD-03");
                int trade4Project = research.Catalog.IndexOf("TRD-04");
                string key3State = key3Project >= 0 && research.IsDone(3, key3Project)
                    ? $"done@{research.CompletedTick(3, key3Project)}" : "open";
                string trade3State = trade3Project >= 0 && research.IsDone(3, trade3Project)
                    ? $"done@{research.CompletedTick(3, trade3Project)}" : "open";
                string trade4State = trade4Project >= 0 && research.IsDone(3, trade4Project)
                    ? $"done@{research.CompletedTick(3, trade4Project)}" : "open";
                for (int s = 0; s < economy.SettlementCount; s++)
                {
                    var lite = economy.SettlementLite(s);
                    if (lite.Civilization != 3 || politics.IsFreeFolk(s) || economy.Population(s) <= 0) continue;
                    cityHistory?.Add($"early season {currentSeason} tick {runner.Tick} civ3 KEY-3 {key3State} TRD-03 {trade3State} TRD-04 {trade4State} {economy.DescribeCivic(s)}");
                }
            }
            if (trace && currentSeason is 175 or 228)
            {
                int[] probeCivs = currentSeason == 175 ? new[] { 3 } : new[] { 2, 3 };
                foreach (int civ in probeCivs)
                {
                    AiDecision[] foundCandidates = (match.Ai?.Log.Entries ?? Array.Empty<AiDecision>())
                        .Where(d => d.Layer == "Strategist" && d.Civ == civ && d.Season == currentSeason &&
                            d.Kind == "DecreeCandidate" && d.Subject == (int)DecreeKind.Found &&
                            d.Reason.Contains("legal True", StringComparison.Ordinal))
                        .OrderByDescending(d => d.Score.Raw).ThenBy(d => d.Other).Take(10).ToArray();
                    foreach (AiDecision candidate in foundCandidates)
                    {
                        if (politics.FoundCheck(civ, candidate.Other, out int donor) != 0 || donor < 0) continue;
                        var donorLite = economy.SettlementLite(donor);
                        landClaimTrace!.Add($"Found donor probe civ{civ} season{currentSeason} target{candidate.Other} EV {candidate.Score.ToDoubleForDisplay():F1}: " +
                            $"donor s{donor} pop {economy.Population(donorLite.Id)} tier {economy.Civic(donor).Tier} " +
                            $"{economy.DescribeCivic(donor)}");
                    }
                }
            }
            if (currentSeason != season)
            {
                season = currentSeason;
                for (int c = 0; c < 4; c++) secessionBySeason[c, season % 8] = 0;
            }
            if (trace && currentSeason == 226 && foundProbeTick < 0)
            {
                const int expectedUpdatesToPairSample = 4;
                foundProbeTick = runner.Tick;
                var historicalFoundAudit = politics.ProjectedFoundSiteFrontier(2, 3,
                    (civ, tile) => match.Ai?.SeatOf(civ)?.Belief is { } belief &&
                        StrategistBeliefAllowsFoundCandidate(politics, belief, civ, tile),
                    expectedUpdatesToPairSample);
                AiDecision[] legalFoundCandidates = (match.Ai?.Log.Entries ?? Array.Empty<AiDecision>())
                    .Where(d => d.Layer == "Strategist" && d.Civ == 3 && d.Season == currentSeason &&
                        d.Kind == "DecreeCandidate" && d.Subject == (int)DecreeKind.Found &&
                        d.Reason.Contains("legal True", StringComparison.Ordinal))
                    .OrderByDescending(d => d.Score.ToDoubleForDisplay()).ThenBy(d => d.Other).ToArray();
                Belief historicalBelief = match.Ai?.SeatOf(3)?.Belief ??
                    throw new InvalidOperationException("Missing civ3 belief at the historical Found probe.");
                bool[] recentFrontierSignal = new bool[historicalBelief.Map.TileCount];
                StrategistTuning.MarkRecentRivalFrontierSites(historicalBelief, 3,
                    politics.ClaimRadiusOfEra(politics.DecreeResearch.Era(3)), recentFrontierSignal);
                bool targetHasBeliefSignal = recentFrontierSignal[6356];
                AiDecision? target6356Decision = legalFoundCandidates.Where(d => d.Other == 6356)
                    .Select(d => (AiDecision?)d).FirstOrDefault();
                bool target6356GotBonus = target6356Decision is { } targetDecision && !string.IsNullOrEmpty(targetDecision.Reason) &&
                    targetDecision.Reason.Contains($"recent frontier contact bonus {StrategistTuning.FoundFrontierContactBonus}", StringComparison.Ordinal);
                landClaimTrace!.Add($"pair 2/3 historical Found signal check: tile6356 recent-owner signal {targetHasBeliefSignal}; " +
                    $"Strategist logged +{StrategistTuning.FoundFrontierContactBonus} bonus {target6356GotBonus}");
                if (!targetHasBeliefSignal || !target6356GotBonus)
                    landClaimTrace!.Add("historical tile6356 bonus check is diagnostic only; policy candidates or remembered frontier may have changed");
                AiDecision? chosenFoundWindowDecree = (match.Ai?.Log.Entries ?? Array.Empty<AiDecision>())
                    .FirstOrDefault(d => d.Layer == "Strategist" && d.Civ == 3 && d.Season == currentSeason && d.Kind == "Decree");
                bool hasChosenFoundWindowDecree = (match.Ai?.Log.Entries ?? Array.Empty<AiDecision>())
                    .Any(d => d.Layer == "Strategist" && d.Civ == 3 && d.Season == currentSeason && d.Kind == "Decree");
                var contactSites = historicalFoundAudit.Candidates
                    .Where(candidate => candidate.Civ == 3 && candidate.PairOwnersTouching)
                    .Select(candidate => (Candidate: candidate,
                        Decision: legalFoundCandidates.FirstOrDefault(d => d.Other == candidate.Tile)))
                    .Where(result => legalFoundCandidates.Any(d => d.Other == result.Candidate.Tile))
                    .OrderByDescending(result => result.Decision.Score.ToDoubleForDisplay())
                    .ThenBy(result => result.Candidate.Tile).ToArray();
                string bestFoundText = legalFoundCandidates.Length == 0
                    ? "none forecast-legal"
                    : $"tile{legalFoundCandidates[0].Other} EV {legalFoundCandidates[0].Score.ToDoubleForDisplay():F1}";
                string contactText = contactSites.Length == 0
                    ? "none among projected frontier-relevant sites"
                    : $"tile{contactSites[0].Candidate.Tile} EV {contactSites[0].Decision.Score.ToDoubleForDisplay():F1}, " +
                        $"Found rank {Array.FindIndex(legalFoundCandidates, d => d.Other == contactSites[0].Candidate.Tile) + 1}/{legalFoundCandidates.Length}, " +
                        $"land weight {contactSites[0].Candidate.ClaimedWeight}, under-42% {contactSites[0].Candidate.Under42PercentCap}, " +
                        $"contact after {expectedUpdatesToPairSample} ownership updates";
                string winnerText = hasChosenFoundWindowDecree && chosenFoundWindowDecree is { } chosen
                    ? $"{(DecreeKind)chosen.Subject}/target{chosen.Other} EV {chosen.Score.ToDoubleForDisplay():F1}"
                    : "no decree chosen";
                string scoreGapText = contactSites.Length > 0 && hasChosenFoundWindowDecree && chosenFoundWindowDecree is { } selected
                    ? $"; hypothetical contact Found score gap vs chosen decree " +
                        $"{contactSites[0].Decision.Score.ToDoubleForDisplay() - selected.Score.ToDoubleForDisplay():F1}"
                    : string.Empty;
                landClaimTrace!.Add($"pair 2/3 historical Found audit at season {currentSeason}: baseline contact after " +
                    $"{expectedUpdatesToPairSample} fixed-source ownership updates {historicalFoundAudit.BaselinePairOwnersTouching}; " +
                    $"forecast-legal Found candidates {legalFoundCandidates.Length}, best {bestFoundText}; " +
                    $"best shortlisted contact site {contactText}; chosen {winnerText}{scoreGapText}");
            }
            for (int c = 0; c < 4; c++)
            {
                if (research.IsDone(c, key3) && key3Tick[c] < 0)
                {
                    key3Tick[c] = research.CompletedTick(c, key3);
                    result.E3Civs++;
                }
                if (research.IsDone(c, key4) && key4Tick[c] < 0)
                {
                    key4Tick[c] = research.CompletedTick(c, key4);
                    key4Sampled[c] = true;
                    result.Key4Completed++;
                    result.E3Completed++;
                    result.Key4LatestMinute = Math.Max(result.Key4LatestMinute, key4Tick[c] / 600.0);
                }
            }
            IReadOnlyList<PoliticsEvent> events = politics.Events;
            for (; eventCursor < events.Count; eventCursor++)
            {
                PoliticsEvent e = events[eventCursor];
                if (trace && e.Kind == PoliticsEventKind.Captured && e.Civ < 4 && e.B >= 0 &&
                    IsE3ExposureTick(e.Tick, key3Tick[e.Civ], key4Tick[e.Civ]))
                    capturedSettlements!.Add((e.Civ, e.A, e.Tick));
                if (e.Kind == PoliticsEventKind.Seceded && e.Civ < 4)
                {
                    int c = e.Civ;
                    if (IsE3ExposureTick(e.Tick, key3Tick[c], key4Tick[c]))
                    {
                        result.Secessions++;
                        secessionBySeason[c, currentSeason % 8]++;
                        int recent = 0;
                        for (int k = 0; k < 8; k++) recent += secessionBySeason[c, k];
                        if (recent > 2) cascade[c] = true;
                    }
                }
            }
            if (trace && currentSeason != lastB19HistorySeason)
            {
                lastB19HistorySeason = currentSeason;
                foreach ((int civ, int settlement, long captureTick) in capturedSettlements!)
                {
                    if (!IsE3ExposureTick(runner.Tick, key3Tick[civ], key4Tick[civ])) continue;
                    var read = economy.Civic(settlement);
                    (int overreach, int spanPenalty) = economy.CohesionPressures(settlement);
                    b19History!.Add($"season{currentSeason} captured-civ{civ} s{settlement} captured@{captureTick / ResearchSystem.SeasonTicks}: " +
                        $"C{read.Cohesion} target{read.CohesionTarget} overreach{overreach} span{spanPenalty} " +
                        $"conquered{read.ConqueredTerm} low{read.LowCohesionSeasons}");
                }
            }
            double minute = runner.Tick / 600.0;
            for (int c = 0; c < 4; c++)
            {
                if (key4Tick[c] >= 0 && key4Tick[c] <= runner.Tick && !key4TerritorySampled[c])
                {
                    key4TerritorySampled[c] = true;
                    int living = 0;
                    for (int s = 0; s < economy.SettlementCount; s++)
                    {
                        var lite = economy.SettlementLite(s);
                        if (lite.Civilization != c || politics.IsFreeFolk(s) || economy.Population(lite.Id) <= 0) continue;
                        living++;
                    }
                    result.MeanSettlements += living;
                    result.Claimed += politics.Territory.ClaimedBasisPoints() / 10000.0;
                    int pair = c / 2;
                    int mate = c ^ 1;
                    bool pairTouchesAtOwnKey4 = Touching(politics.Territory, c, mate);
                    if (TryRecordOwnKey4BorderSample(key4BorderSampled, c, pairTouchesAtOwnKey4, out bool countedTouch))
                    {
                        result.BorderCivSamples++;
                        if (countedTouch) result.TouchingBorderCivs++;
                        if (trace)
                            landClaimTrace!.Add($"civ{c} own KEY-4 sample at season {currentSeason}: paired borders touch {countedTouch}");
                    }
                    if (!pairTraceSampled[pair] && key4Sampled[mate])
                    {
                        pairTraceSampled[pair] = true;
                        if (trace)
                        {
                            landClaimTrace!.Add($"pair {pair * 2}/{pair * 2 + 1} later-civ KEY-4 diagnostic at season {currentSeason}: " +
                                $"owners touching {pairTouchesAtOwnKey4}, live claimed {politics.Territory.ClaimedBasisPoints() / 10000.0:P1}");
                            if (pair == 1 && foundProbeTick >= 0)
                                landClaimTrace!.Add($"pair 2/3 historical Found audit timing: pair sampled at season {currentSeason}, tick {runner.Tick}; " +
                                    $"{(runner.Tick - foundProbeTick) / ResearchSystem.SeasonTicks} ownership updates since season-226 probe");
                            var projected = politics.ProjectedClaimedLandWeights();
                            int totalWeight = Math.Max(1, politics.Territory.TotalHabitableWeight);
                            string Share(int weight) => $"{weight / (double)totalWeight:P1}";
                            string PathCost(int weight) => weight < 0 ? "disconnected" : $"{weight} weight ({Share(weight)})";
                            int capWeight = (int)Math.Floor(totalWeight * 0.42);
                            string CapMargin(int weight) => weight <= capWeight
                                ? $"under 42% cap by {capWeight - weight} weight"
                                : $"over 42% cap by {weight - capWeight} weight";
                            var settlementFrontier = politics.ProjectedSettlementOnlyFrontier(pair * 2, pair * 2 + 1);
                            int settlementPathWeight = settlementFrontier.Weight;
                            int landBudgetWeight = Math.Max(0, (int)Math.Floor(totalWeight * 0.42) - projected.SettlementsOnly);
                            int firstCamps = 0, secondCamps = 0;
                            for (int i = 0; i < politics.CampCount; i++)
                            {
                                int civ = politics.Camp(i).Civ;
                                if (civ == pair * 2) firstCamps++;
                                else if (civ == pair * 2 + 1) secondCamps++;
                            }
                            landClaimTrace!.Add($"pair {pair * 2}/{pair * 2 + 1}: live claimed {politics.Territory.ClaimedBasisPoints() / 10000.0:P1}; " +
                                $"instant projection settlements {Share(projected.SettlementsOnly)}, +seasonal camps {Share(projected.WithSeasonalCamps)}, " +
                                $"+mining camps {Share(projected.WithMiningCamps)}; seasonal camps {firstCamps}/{secondCamps}; " +
                                $"settlement-only minimum land path cost {PathCost(settlementPathWeight)} vs {landBudgetWeight} weight available below 42%; " +
                                NearestCampToFrontier(politics, settlementFrontier.Path, pair * 2, pair * 2 + 1));
                            foreach (MiningCampClaimRadiusProjection miningProjection in Enum.GetValues<MiningCampClaimRadiusProjection>())
                            {
                                var trail = politics.ProjectedSeasonalCampTrail(8, pair * 2, pair * 2 + 1, miningProjection);
                                landClaimTrace.Add($"pair {pair * 2}/{pair * 2 + 1}: radius-8 camp trail, mining claims {miningProjection} => " +
                                    $"projected claimed land {Share(trail.ClaimedWeight)} ({CapMargin(trail.ClaimedWeight)}), minimum land path cost {PathCost(trail.FrontierConnectionWeight)}, " +
                                    $"supported/unsupported route tiles {trail.SupportedTiles}/{trail.UnsupportedTiles}, corridor weight {trail.CorridorWeight}");
                            }
                            var compactPlacement = politics.ProjectedCompactCampPlacement(pair * 2, pair * 2 + 1);
                            landClaimTrace.Add($"pair {pair * 2}/{pair * 2 + 1}: compact legal camp-site counterfactual => " +
                                $"projected claimed land {Share(compactPlacement.BaselineClaimedWeight)} current to {Share(compactPlacement.CompactClaimedWeight)} compact " +
                                $"({CapMargin(compactPlacement.CompactClaimedWeight)}), frontier path cost " +
                                $"{PathCost(compactPlacement.BaselineFrontierConnectionWeight)} current to {PathCost(compactPlacement.CompactFrontierConnectionWeight)} compact, " +
                                $"camps {compactPlacement.BaselineCampCount}/{compactPlacement.CompactCampCount}, changed sites {compactPlacement.ChangedSites}");
                            landClaimTrace.Add($"pair {pair * 2}/{pair * 2 + 1}: bounded ownership-screened frontier-preserving search => " +
                                $"projected claimed land {Share(compactPlacement.SearchClaimedWeight)} ({CapMargin(compactPlacement.SearchClaimedWeight)}), " +
                                $"frontier path cost {PathCost(compactPlacement.SearchFrontierConnectionWeight)}; " +
                                $"changed sites {compactPlacement.SearchChangedSites}, route-support slots {compactPlacement.SearchSupportSlots}, " +
                                $"pair projections {compactPlacement.SearchSupportPairEvaluations}, accepted moves {compactPlacement.SearchOptimizedMoves}, " +
                                $"coupled-pair projections {compactPlacement.SearchCoupledPairEvaluations}, accepted pairs {compactPlacement.SearchOptimizedPairs}, " +
                                $"whole-map projections {compactPlacement.SearchEvaluations}/200, meets both targets {compactPlacement.SearchMeetsTargets}; " +
                                $"snapshot-legal current sites {compactPlacement.SnapshotLegalOriginalSites}/{compactPlacement.BaselineCampCount}");
                            landClaimTrace.Add($"pair {pair * 2}/{pair * 2 + 1}: compact placement preserving route-support camps => " +
                                $"projected claimed land {Share(compactPlacement.ProtectedClaimedWeight)} ({CapMargin(compactPlacement.ProtectedClaimedWeight)}), " +
                                $"frontier path cost {PathCost(compactPlacement.ProtectedFrontierConnectionWeight)}, " +
                                $"preserved legal route-support camps {compactPlacement.ProtectedCampSites}");
                            landClaimTrace.Add($"pair {pair * 2}/{pair * 2 + 1}: all-slot route-camp counterfactual => " +
                                $"candidate slots {compactPlacement.RouteCandidateSlots}, one/two-camp projections " +
                                $"{compactPlacement.RouteSingleEvaluations}/{compactPlacement.RoutePairEvaluations}, best claimed land " +
                                $"{Share(compactPlacement.RouteBestClaimedWeight)} ({CapMargin(compactPlacement.RouteBestClaimedWeight)}), " +
                                $"frontier path cost {PathCost(compactPlacement.RouteBestFrontierConnectionWeight)}, " +
                                $"meets contact + 42% cap {compactPlacement.RouteMeetsContactAndLandCap}");
                            var foundAudit = politics.ProjectedFoundSiteFrontier(pair * 2, pair * 2 + 1,
                                (civ, tile) => match.Ai?.SeatOf(civ)?.Belief is { } belief &&
                                    StrategistBeliefAllowsFoundCandidate(politics, belief, civ, tile));
                            landClaimTrace.Add($"pair {pair * 2}/{pair * 2 + 1}: legal Found-site counterfactual baseline => " +
                                $"projected claimed land {Share(foundAudit.BaselineClaimedWeight)} ({CapMargin(foundAudit.BaselineClaimedWeight)}), " +
                                $"frontier path cost {PathCost(foundAudit.BaselineFrontierConnectionWeight)}, " +
                                $"projected owner contact after {foundAudit.OwnershipUpdatesProjected} fixed-source ownership updates " +
                                $"{foundAudit.BaselinePairOwnersTouching}");
                            foreach (FoundFrontierCivSummary civ in foundAudit.Civilizations)
                                landClaimTrace.Add($"  Found options civ{civ.Civ}: live execution-legal sites {civ.LegalSiteCount}; " +
                                    $"Strategist belief-filtered sites {civ.StrategistCandidateCount}; " +
                                    $"within radius+1 of frontier {civ.RelevantSiteCount}; projected {civ.ProjectedSiteCount}; " +
                                    $"AI Found gate open {civ.AiFoundGateOpen}; Mandate {civ.Mandate}/{civ.MandateCap}; " +
                                    $"Found decrees {civ.FoundDecrees}, natural E3 foundings {civ.NaturalFoundings}; " +
                                    $"Council 85% preferred tile {civ.CouncilPreferredTile}");
                            foreach (FoundFrontierCandidateProjection candidate in foundAudit.Candidates)
                                landClaimTrace.Add($"  Found projection civ{candidate.Civ} tile{candidate.Tile} donor s{candidate.DonorSettlement} " +
                                    $"pop {candidate.DonorPopulation}->{candidate.DaughterPopulation}, fill {candidate.DonorFill.ToDoubleForDisplay():F2}, " +
                                    $"path distance {candidate.DistanceToFrontier}, Council preferred {candidate.CouncilPreferred} => " +
                                    $"claimed land {Share(candidate.ClaimedWeight)} ({CapMargin(candidate.ClaimedWeight)}), " +
                                    $"frontier path cost {PathCost(candidate.FrontierConnectionWeight)}, " +
                                    $"projected owner contact after {foundAudit.OwnershipUpdatesProjected} fixed-source ownership updates " +
                                    $"{candidate.PairOwnersTouching}");
                            foreach (int civ in new[] { pair * 2, pair * 2 + 1 })
                            {
                                AiDecision[] decisions = (match.Ai?.Log.Entries ?? Array.Empty<AiDecision>())
                                    .Where(d => d.Layer == "Strategist" && d.Civ == civ && d.Season <= currentSeason)
                                    .ToArray();
                                var foundWindows = decisions
                                    .Where(d => d.Kind == "DecreeCandidate" && d.Subject == (int)DecreeKind.Found)
                                    .GroupBy(d => d.Season)
                                    .OrderByDescending(group => group.Key)
                                    .Take(3)
                                    .OrderBy(group => group.Key)
                                    .ToArray();
                                if (foundWindows.Length == 0)
                                {
                                    landClaimTrace.Add($"  Strategist full-Mandate Found windows civ{civ}: none through season {currentSeason}");
                                    continue;
                                }
                                foreach (var window in foundWindows)
                                {
                                    AiDecision[] seasonCandidates = decisions
                                        .Where(d => d.Kind == "DecreeCandidate" && d.Season == window.Key &&
                                            d.Reason.Contains("legal True", StringComparison.Ordinal))
                                        .OrderByDescending(d => d.Score.ToDoubleForDisplay())
                                        .ThenBy(d => d.Subject).ThenBy(d => d.Other)
                                        .ToArray();
                                    AiDecision[] topFound = window.Where(d => d.Reason.Contains("legal True", StringComparison.Ordinal))
                                        .OrderByDescending(d => d.Score.ToDoubleForDisplay())
                                        .ThenBy(d => d.Other).Take(3).ToArray();
                                    AiDecision? winner = decisions.FirstOrDefault(d => d.Kind == "Decree" && d.Season == window.Key);
                                    bool hasWinner = decisions.Any(d => d.Kind == "Decree" && d.Season == window.Key);
                                    string foundScores = string.Join(", ", topFound.Select(d => $"tile{d.Other} EV {d.Score.ToDoubleForDisplay():F1}"));
                                    string scoredRank = string.Join(", ", seasonCandidates.Take(3)
                                        .Select(d => $"{(DecreeKind)d.Subject}/target{d.Other} EV {d.Score.ToDoubleForDisplay():F1}"));
                                    string winnerText = hasWinner && winner is { } chosen
                                        ? $"chosen {(DecreeKind)chosen.Subject}/target{chosen.Other} EV {chosen.Score.ToDoubleForDisplay():F1}; " +
                                            (topFound.Length == 0
                                                ? "no forecast-legal Found candidate"
                                                : $"best legal Found minus chosen {topFound[0].Score.ToDoubleForDisplay() - chosen.Score.ToDoubleForDisplay():F1}")
                                        : "no decree chosen";
                                    if (topFound.Length == 0) foundScores = $"none legal ({window.Count()} belief-filtered candidate(s))";
                                    landClaimTrace.Add($"  Strategist full-Mandate Found window civ{civ} season {window.Key}: " +
                                        $"top legal Found [{foundScores}]; top legal scored decrees [{scoredRank}]; {winnerText}");
                                }
                            }
                        }
                    }
                }
                for (int s = 0; s < economy.SettlementCount; s++)
                {
                    var lite = economy.SettlementLite(s);
                    if (lite.Civilization != c || politics.IsFreeFolk(s) || economy.Population(lite.Id) <= 0) continue;
                    if (IsE3ExposureTick(runner.Tick, key3Tick[c], key4Tick[c]) && economy.Civic(s).CohesionLive)
                    { result.CohesionSum += economy.Civic(s).Cohesion; result.CohesionCount++; }
                }
            }
            MatchEnd end = match.EndOfMatch();
            if (end.IsOver(runner.Tick) && !endSampled)
            {
                endSampled = true;
                result.ChapterEndSampled = true;
                var chapterEndCivics = trace ? new List<string>() : null;
                if (trace)
                    result.TraceChapterEndTiming = $"chapter closes {end.Reason} at season {end.EndSeason} / {ElapsedMinutes(end.EndTick):F1} min; sampled at tick {runner.Tick} / {ElapsedMinutes(runner.Tick):F1} min";
                for (int c = 0; c < 4; c++)
                {
                    bool hasTown = false, hasCity = false;
                    var livingSettlements = trace ? new List<string>() : null;
                    for (int s = 0; s < economy.SettlementCount; s++)
                    {
                        var lite = economy.SettlementLite(s);
                        if (lite.Civilization != c || politics.IsFreeFolk(s) || economy.Population(lite.Id) <= 0) continue;
                        SettlementTier tier = economy.Civic(s).Tier;
                        if (tier >= SettlementTier.Town) hasTown = true;
                        if (tier == SettlementTier.City) hasCity = true;
                        if (trace)
                            livingSettlements!.Add($"s{lite.Id} pop{economy.Population(lite.Id)} {tier} temple{economy.Civic(s).Temples}");
                    }
                    if (hasTown) result.TownCivs++;
                    if (hasCity) result.CityCivs++;
                    if (trace)
                        chapterEndCivics!.Add($"civ{c}: Town={hasTown} City={hasCity}; " +
                            (livingSettlements!.Count == 0 ? "no living settlements" : string.Join("; ", livingSettlements)));
                }
                if (trace) result.TraceChapterEndCivics = chapterEndCivics!.ToArray();
                result.ChapterEndRaids = politics.RaidCount;
                result.ChapterEndWars = politics.Wars;
                result.ChapterEndCaptures = politics.CaptureCount;
                for (int c = 0; c < 4; c++)
                {
                    result.EndDominion += politics.Territory.Dominion(c).ToDoubleForDisplay();
                    result.EndMastery += research.MasteryLegacy(c).ToDoubleForDisplay();
                    result.EndFirsts += research.FirstsLegacy(c).ToDoubleForDisplay();
                    result.EndWonders += research.WondersLegacy(c).ToDoubleForDisplay();
                }
            }
            bool allKey4 = true;
            for (int c = 0; c < 4; c++) allKey4 &= key4Tick[c] >= 0;
            if (end.IsOver(runner.Tick) && allKey4) { result.Minute = minute; break; }
        }
        for (int c = 0; c < 4; c++) if (key3Tick[c] >= 0 && key4Tick[c] < 0) result.E3Incomplete++;
        if (!result.ChapterEndSampled)
        {
            result.ChapterEndRaids = politics.RaidCount;
            result.ChapterEndWars = politics.Wars;
            result.ChapterEndCaptures = politics.CaptureCount;
        }
        // Keep the actual run horizon even when the chapter-end sample was already taken but one
        // or more civilizations never reached KEY-4. The sample is still valid at chapter end;
        // KEY-4 completeness is reported independently in the gate totals.
        result.Minute = ElapsedMinutes(runner.Tick);
        int finalKey4 = research.Catalog.KeystoneLeaving(ResearchSystem.LastResearchEra);
        bool[]? finalClosure = research.Catalog.ClosureOf(ResearchSystem.LastResearchEra);
        result.Key4IncompleteBlockers = Enumerable.Range(0, 4).Where(c => !research.IsDone(c, finalKey4)).Select(c =>
        {
            string missingDirect = string.Join(",", research.Catalog.Projects[finalKey4].Requires
                .Where(p => !research.IsDone(c, p)).Select(p => $"{research.Catalog.Projects[p].Id}:{research.State(c, p)}"));
            int[] missingClosure = finalClosure is null ? Array.Empty<int>() : Enumerable.Range(0, research.Catalog.Count)
                .Where(p => finalClosure[p] && !research.IsDone(c, p)).ToArray();
            string closurePreview = string.Join(",", missingClosure.Take(5).Select(p => $"{research.Catalog.Projects[p].Id}:{research.State(c, p)}"));
            return $"KEY-4 blockers civ{c}: state {research.State(c, finalKey4)}, E3 breadth {research.DoneInEra(c, ResearchSystem.LastResearchEra)}/21, " +
                $"direct prerequisites {(missingDirect.Length == 0 ? "complete" : missingDirect)}, closure gaps {missingClosure.Length}" +
                (closurePreview.Length == 0 ? string.Empty : $" [{closurePreview}{(missingClosure.Length > 5 ? ",…" : string.Empty)}]");
        }).ToArray();
        if (trace)
        {
            result.TraceDecisions = match.Ai?.Log.Entries.ToArray() ?? Array.Empty<AiDecision>();
            result.TraceResearchSnapshots = researchSnapshots!.ToArray();
            result.TraceResearchMilestones = Enumerable.Range(0, 4).SelectMany(c => Enumerable.Range(0, milestoneProjects.Length).Select(i =>
            {
                string At(int seasonNumber) => seasonNumber < 0 ? "not reached" : $"season{seasonNumber}";
                return $"civ{c} {milestoneProjectIds[i]} first eligible {At(firstAvailableSeason[c, i])}, first slotted {At(firstSlotSeason[c, i])}, completed {At(completionSeason[c, i])}";
            })).ToArray();
            result.TraceLateCatchupPicks = result.TraceDecisions
                .Where(d => d.Layer == "Spark" && d.Reason == "late KEY-4 catch-up")
                .OrderBy(d => d.Tick).ThenBy(d => d.Civ)
                .Select(d => $"civ{d.Civ} season {d.Season}: {((uint)d.Subject < (uint)research.Catalog.Count ? $"{research.Catalog.Projects[d.Subject].Id} (Era {research.Catalog.Projects[d.Subject].Era})" : d.Subject.ToString(CultureInfo.InvariantCulture))}")
                .ToArray();
            int key3Season = key3Tick[3] < 0 ? int.MaxValue : (int)(key3Tick[3] / ResearchSystem.SeasonTicks);
            result.TracePreCatchupResearchPicks = result.TraceDecisions
                .Where(d => d.Layer == "Spark" && d.Civ == 3 && d.Season >= key3Season &&
                    d.Season < firstPublicRivalEra4Season && (d.Kind is "Primary" or "Secondary" or "Tertiary"))
                .OrderBy(d => d.Tick).ThenBy(d => d.Kind, StringComparer.Ordinal)
                .Select(d => $"season {d.Season} {d.Kind}: {((uint)d.Subject < (uint)research.Catalog.Count ? research.Catalog.Projects[d.Subject].Id : d.Subject.ToString(CultureInfo.InvariantCulture))}; {d.Reason}")
                .ToArray();
            result.TraceEvents = politics.Events.Select(e => e.Kind.ToString()).ToArray();
            result.TraceKey4Blockers = Enumerable.Range(0, 4).Select(c =>
            {
                string missingClosure = finalClosure is null ? "none" : string.Join(",", Enumerable.Range(0, research.Catalog.Count)
                    .Where(p => finalClosure[p] && !research.IsDone(c, p))
                    .Select(p => $"{research.Catalog.Projects[p].Id}:{research.State(c, p)}"));
                string missingDirect = string.Join(",", research.Catalog.Projects[finalKey4].Requires
                    .Where(p => !research.IsDone(c, p)).Select(p => $"{research.Catalog.Projects[p].Id}:{research.State(c, p)}"));
                return $"civ{c}: KEY-4 {(research.IsDone(c, finalKey4) ? "done" : "open")}, E3 breadth {research.DoneInEra(c, ResearchSystem.LastResearchEra)}/21, " +
                    $"direct prerequisites {(missingDirect.Length == 0 ? "complete" : missingDirect)}, closure gap {(missingClosure.Length == 0 ? "none" : missingClosure)}";
            }).ToArray();
            result.TraceB19Exposures = Enumerable.Range(0, 4).Select(c =>
            {
                string key3 = key3Tick[c] >= 0 ? $"{ElapsedMinutes(key3Tick[c]):F1} min" : "not reached";
                string key4 = key4Tick[c] >= 0 ? $"{ElapsedMinutes(key4Tick[c]):F1} min" : "not reached";
                int captures = politics.Events.Count(e => e.Kind == PoliticsEventKind.Captured && e.Civ == c && e.B >= 0 &&
                    IsE3ExposureTick(e.Tick, key3Tick[c], key4Tick[c]));
                int secessions = politics.Events.Count(e => e.Kind == PoliticsEventKind.Seceded && e.Civ == c &&
                    IsE3ExposureTick(e.Tick, key3Tick[c], key4Tick[c]));
                string captureDetails = string.Join("; ", politics.Events
                    .Where(e => e.Kind == PoliticsEventKind.Captured && e.Civ == c && e.B >= 0 &&
                        IsE3ExposureTick(e.Tick, key3Tick[c], key4Tick[c]))
                    .Select(e =>
                    {
                        var captured = economy.SettlementLite(e.A);
                        var civic = economy.Civic(e.A);
                        int capital = economy.Capital(c);
                        int distance = capital >= 0
                            ? HexGrid.Distance(captured.SiteTile, economy.SettlementLite(capital).SiteTile, world.Width)
                            : -1;
                        return $"season{e.Tick / ResearchSystem.SeasonTicks} took s{e.A} from civ{e.Other} ({e.B} stayed), " +
                            $"now owner civ{captured.Civilization}/capital distance {distance}/C{civic.Cohesion}/target{civic.CohesionTarget}/" +
                            $"low{civic.LowCohesionSeasons}/conquered{civic.ConqueredTerm}";
                    }));
                return $"civ{c}: KEY-3 {key3}, KEY-4 {key4}, captures owned during exposure {captures}, secessions {secessions}" +
                    (captureDetails.Length == 0 ? string.Empty : $"; {captureDetails}");
            }).ToArray();
            result.TraceB19History = b19History?.ToArray() ?? Array.Empty<string>();
            var civicTrace = new List<string>();
            string[] cityPath = { "SOC-09", "GOV-05", "BEL-05", "BEL-06", "STN-11", "BLD-05", "BLD-07", "TRD-03", "TRD-04" };
            var cityProjectIndices = cityPath.Select(code => research.Catalog.IndexOf(code)).Where(p => p >= 0).ToArray();
            result.TraceTechs = research.Events
                .Where(e => e.Kind == ResearchEventKind.Completed && Array.IndexOf(cityProjectIndices, e.Project) >= 0)
                .OrderBy(e => e.Tick)
                .Select(e => $"civ{e.Civ} {research.Catalog.Projects[e.Project].Id}@{ElapsedMinutes(e.Tick):F1}")
                .ToArray();
            for (int c = 0; c < 4; c++)
            {
                int capital = economy.Capital(c);
                string projects = string.Join(" ", cityPath.Select(code =>
                {
                    int project = research.Catalog.IndexOf(code);
                    return $"{code}={(project >= 0 && research.IsDone(c, project) ? "done" : "open")}";
                }));
                int seasonalCamps = 0;
                for (int i = 0; i < politics.CampCount; i++) if (politics.Camp(i).Civ == c) seasonalCamps++;
                int miningCamps = 0;
                for (int i = 0; i < economy.MiningCampCount; i++) if (economy.MiningCamp(i).Civilization == c) miningCamps++;
                var civSettlements = new List<(int Id, int Population)>();
                for (int i = 0; i < economy.SettlementCount; i++)
                {
                    var lite = economy.SettlementLite(i);
                    if (lite.Civilization == c && !politics.IsFreeFolk(i) && economy.Population(lite.Id) > 0)
                        civSettlements.Add((lite.Id, economy.Population(lite.Id)));
                }
                civicTrace.Add($"run-end City trace civ{c}: settlements {civSettlements.Count}, camps seasonal/mining {seasonalCamps}/{miningCamps}; {projects}");
                foreach ((int settlement, int population) in civSettlements.OrderByDescending(x => x.Population).ThenBy(x => x.Id).Take(3))
                    civicTrace.Add($"    {(settlement == capital ? "capital " : "")}{economy.DescribeCivic(settlement)}");
            }
            result.TraceCivics = civicTrace.ToArray();
            result.TraceDemography = demographyHistory?.ToArray() ?? Array.Empty<string>();
            result.TraceCityHistory = cityHistory?.ToArray() ?? Array.Empty<string>();
            result.TraceLandClaims = landClaimTrace?.ToArray() ?? Array.Empty<string>();
            var campSightings = new List<string>();
            for (int c = 0; c < 4; c++)
            {
                if (match.Ai?.SeatOf(c) is not { } seat || seat.Builder is not BeliefBuilder builder) continue;
                int remembered = 0;
                foreach (RivalBelief rival in seat.Belief.Rivals) remembered += rival.MiningCampsSeen.Count;
                campSightings.Add($"civ{c} season {seat.Belief.Season}: last update live/aware/range/owner-seen/recorded " +
                    $"{builder.LastUpdateLiveRivalCamps}/{builder.LastUpdateAwareMiningCamps}/{builder.LastUpdateRangeMiningCamps}/" +
                    $"{builder.LastUpdateOwnerSeenMiningCamps}/{builder.LastUpdateRecordedMiningCamps}, remembered {remembered}");
            }
            result.TraceCampSightings = campSightings.ToArray();
        }
        result.CascadeMatches = Array.Exists(cascade, x => x) ? 1 : 0;
        return result;
    }

    private static void PrintTraceSummary(Sample r)
    {
        Console.WriteLine($"trace seed {r.Seed}: decisions Raid={r.TraceDecisions.Count(d => d.Kind == "Raid")}, War={r.TraceDecisions.Count(d => d.Kind == "War")}, Assault={r.TraceDecisions.Count(d => d.Kind == "Assault")}; launches Raid={r.TraceEvents.Count(k => k is nameof(PoliticsEventKind.RaidLaunched) or nameof(PoliticsEventKind.MiningCampRaidLaunched))} (camps {r.TraceEvents.Count(k => k == nameof(PoliticsEventKind.MiningCampRaidLaunched))}), War={r.TraceEvents.Count(k => k == PoliticsEventKind.WarDeclared.ToString())}, Assault={r.TraceEvents.Count(k => k == PoliticsEventKind.AssaultLaunched.ToString())}");
        if (r.TraceChapterEndTiming.Length > 0) Console.WriteLine($"  B17 sample timing: {r.TraceChapterEndTiming}; run-end state at {r.Minute:F1} min");
        foreach (string line in r.TraceChapterEndCivics) Console.WriteLine($"  B17 chapter-end civic {line}");
        foreach (string line in r.TraceTechs) Console.WriteLine($"  City tech {line}");
        foreach (string line in r.TraceCivics) Console.WriteLine($"  {line}");
        foreach (string line in r.TraceB19Exposures) Console.WriteLine($"  B19 exposure {line}");
        foreach (string line in r.TraceB19History) Console.WriteLine($"  B19 history {line}");
        foreach (string line in r.TraceKey4Blockers) Console.WriteLine($"  KEY-4 blockers {line}");
        foreach (string line in r.TraceResearchSnapshots) Console.WriteLine($"  research snapshot {line}");
        foreach (string line in r.TraceResearchMilestones) Console.WriteLine($"  research milestone {line}");
        foreach (string line in r.TraceDemography) Console.WriteLine($"  demography {line}");
        foreach (string line in r.TracePreCatchupResearchPicks) Console.WriteLine($"  pre-catch-up Spark pick {line}");
        foreach (string line in r.TraceLandClaims) Console.WriteLine($"  B17 land diagnostic {line}");
        foreach (AiDecision d in r.TraceDecisions.Where(d => d.Kind == "DecreeReserve")
                     .OrderBy(d => d.Tick).ThenBy(d => d.Civ))
            Console.WriteLine($"  Frontier Found reserve civ{d.Civ} season{d.Season}: {d.Reason}");
        foreach (string line in r.TraceCampSightings) Console.WriteLine($"  B20 camp sighting diagnostic {line}");
        foreach (string line in r.TraceCityHistory) Console.WriteLine($"  City history {line}");
        var decrees = r.TraceDecisions.Where(d => d.Kind == "Decree")
            .GroupBy(d => (DecreeKind)d.Subject).OrderBy(g => g.Key).ToArray();
        Console.WriteLine("  chosen decrees: " + (decrees.Length == 0
            ? "none"
            : string.Join(", ", decrees.Select(g => $"{g.Key}={g.Count()}"))));
        foreach (AiDecision d in r.TraceDecisions.Where(d => d.Kind == "Decree" && d.Subject == (int)DecreeKind.Found)
                     .OrderBy(d => d.Tick).ThenBy(d => d.Civ))
            Console.WriteLine($"  Found decree civ{d.Civ} season{d.Season} target{d.Other} EV {d.Score.ToDoubleForDisplay():F1}: {d.Reason}");
        var foundCandidates = r.TraceDecisions.Where(d => d.Kind == "DecreeCandidate" && d.Subject == (int)DecreeKind.Found).ToArray();
        AiDecision bestFound = foundCandidates.OrderByDescending(d => d.Score.ToDoubleForDisplay()).FirstOrDefault();
        Console.WriteLine($"  Found decree candidates: {foundCandidates.Length}; positive {foundCandidates.Count(d => d.Score.ToDoubleForDisplay() > 0)}; best {bestFound.Score.ToDoubleForDisplay():F1} ({(foundCandidates.Length == 0 ? "none" : bestFound.Reason)})");
        foreach (AiDecision d in r.TraceDecisions.Where(d => d.Kind is "Plan" or "Posture")
                     .OrderBy(d => d.Tick).ThenBy(d => d.Civ))
            Console.WriteLine($"  {d.Kind} civ{d.Civ} season {d.Season}: {d.Reason}");
        foreach (string line in r.TraceLateCatchupPicks) Console.WriteLine($"  late KEY-4 catch-up {line}");
        AiDecision[] campScans = r.TraceDecisions.Where(d => d.Kind == "CampRaidScan").ToArray();
        Console.WriteLine($"  Camp raid scans: {campScans.Length}; with a sighting {campScans.Count(d => d.Subject > 0)}; evaluated candidates {campScans.Sum(d => d.Other)}");
        foreach (AiDecision d in campScans.Where(d => d.Subject > 0 || d.Other > 0).TakeLast(12))
            Console.WriteLine($"  Camp raid scan civ{d.Civ} season {d.Season}: {d.Reason}");
        foreach (string kind in new[] { "RaidRejected", "WarRejected" })
        {
            var top = r.TraceDecisions.Where(d => d.Kind == kind).GroupBy(d => d.Reason)
                .Select(g => (Reason: g.Key, Count: g.Count())).OrderByDescending(g => g.Count).ThenBy(g => g.Reason, StringComparer.Ordinal).Take(3).ToArray();
            Console.WriteLine($"  {kind}: " + (top.Length == 0 ? "none" : string.Join("; ", top.Select(x => $"{x.Count}x {x.Reason}"))));
        }
    }

    private static bool Touching(Territory t, int a, int b)
    {
        Span<int> n = stackalloc int[6];
        for (int tile = 0; tile < t.TileCount; tile++)
        {
            if (t.OwnerOf(tile) != a) continue;
            int count = HexGrid.Neighbors(tile, t.Width, t.Height, n);
            for (int k = 0; k < count; k++) if (t.OwnerOf(n[k]) == b) return true;
        }
        return false;
    }

    internal static bool StrategistBeliefAllowsFoundCandidate(PoliticsSystem politics, Belief belief, int civ, int tile)
    {
        if (unchecked((uint)tile) >= unchecked((uint)belief.Map.TileCount) ||
            !belief.Map.TryTile(tile, out WorldTile knownTile) || !knownTile.IsLand) return false;
        int width = belief.Map.Width;
        int ownMinimumDistance = politics.ClaimRadiusOfEra(politics.DecreeResearch.Era(civ)) + 1;
        int reach = politics.FoundReach(civ, bySea: false);
        bool inReach = false;
        int bestDonor = -1;
        Fix64 bestFill = Fix64.MinValue;
        for (int settlement = 0; settlement < politics.SettlementTotal; settlement++)
        {
            int distance = HexGrid.Distance(politics.SettlementTileOf(settlement), tile, width);
            bool own = politics.SettlementCivOf(settlement) == civ && !politics.IsFreeFolk(settlement);
            if (!own) continue;
            if (politics.SettlementPopulationOf(settlement) <= 0) continue;
            if (distance < ownMinimumDistance) return false;
            if (distance > reach) continue;
            inReach = true;
            if (politics.SettlementPopulationOf(settlement) < 20 ||
                politics.Economy.SeasonsSinceFission(settlement) < 4) continue;
            Fix64 fill = politics.Economy.PopulationFill(settlement);
            if (fill >= Fix64.FromRatio(6, 10) && fill > bestFill)
            {
                bestFill = fill;
                bestDonor = settlement;
            }
        }
        if (!inReach || bestDonor < 0) return false;
        int daughterPopulation = (politics.SettlementPopulationOf(bestDonor) * 2 + 2) / 5;
        if (daughterPopulation < 8) return false;

        foreach (RivalBelief rival in belief.Rivals)
        {
            if (rival.Civ == civ) continue;
            foreach (BeliefSettlement settlement in rival.Settlements)
                if (HexGrid.Distance(settlement.Tile, tile, width) < 9) return false;
        }
        foreach (BeliefTinFolk folk in belief.TinFolkSeen)
            if (belief.Season - folk.LastSeenSeason is >= 0 and <= 2 &&
                HexGrid.Distance(folk.Tile, tile, width) < 9) return false;
        return true;
    }

    private static string NearestCampToFrontier(PoliticsSystem politics, int[] path, int civA, int civB)
    {
        if (path.Length == 0) return "no land-only path";
        int bestTile = -1, bestCiv = -1, bestPathDistance = int.MaxValue, bestParentDistance = int.MaxValue,
            bestRivalDistance = int.MaxValue;
        for (int i = 0; i < politics.CampCount; i++)
        {
            (int tile, int settlement, int civ) = politics.Camp(i);
            if (civ != civA && civ != civB) continue;
            int pathDistance = int.MaxValue;
            foreach (int pathTile in path)
                pathDistance = Math.Min(pathDistance, HexGrid.Distance(tile, pathTile, politics.Territory.Width));
            if (pathDistance >= bestPathDistance) continue;
            int parentDistance = HexGrid.Distance(tile, politics.SettlementTileOf(settlement), politics.Territory.Width);
            int rivalDistance = int.MaxValue;
            for (int s = 0; s < politics.SettlementTotal; s++)
            {
                if (politics.IsFreeFolk(s) || politics.SettlementCivOf(s) == civ) continue;
                rivalDistance = Math.Min(rivalDistance,
                    HexGrid.Distance(tile, politics.SettlementTileOf(s), politics.Territory.Width));
            }
            bestTile = tile;
            bestCiv = civ;
            bestPathDistance = pathDistance;
            bestParentDistance = parentDistance;
            bestRivalDistance = rivalDistance;
        }
        return bestTile < 0 ? "no paired seasonal camp" :
            $"nearest seasonal camp civ{bestCiv} tile {bestTile}: path {bestPathDistance}, parent {bestParentDistance}, rival settlement {bestRivalDistance} tiles";
    }

}
