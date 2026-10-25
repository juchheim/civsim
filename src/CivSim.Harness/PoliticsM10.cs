using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>Chapter-scale acceptance for M10 politics gates B17–B20 (docs/MVP/22 §16).</summary>
internal static class PoliticsM10
{
    internal readonly record struct B17ReportMetrics(double SettlementsPerCiv, double ClaimedLandShare, double PairedBorderTouchRate);

    internal static int ExpectedBorderPairs(int civCount) => civCount / 2;
    internal static double ElapsedMinutes(long tick) => tick / 600.0;

    internal static B17ReportMetrics SummarizeB17(double settlementTotal, double claimedLandShareTotal,
        int completedCivs, int touchingPairs, int sampledPairs)
        => new(
            settlementTotal / Math.Max(1, completedCivs),
            claimedLandShareTotal / Math.Max(1, completedCivs),
            touchingPairs / (double)Math.Max(1, sampledPairs));

    internal static bool IsE3ExposureTick(long tick, long key3Tick, long key4Tick)
        => key3Tick >= 0 && tick >= key3Tick && (key4Tick < 0 || tick <= key4Tick);

    private sealed class Sample
    {
        public ulong Seed;
        public double Minute, Key4LatestMinute;
        public int TownCivs, CityCivs, Civs, TouchingPairs, Pairs, Key4Completed;
        public int ChapterEndRaids, ChapterEndWars, ChapterEndCaptures, Secessions, CascadeMatches;
        public int E3Civs, E3Completed, E3Incomplete;
        public bool ChapterEndSampled;
        public AiDecision[] TraceDecisions = Array.Empty<AiDecision>();
        public string[] TraceEvents = Array.Empty<string>();
        public double Claimed, MeanSettlements;
        public double EndDominion, EndMastery, EndFirsts, EndWonders;
        public double CohesionSum;
        public int CohesionCount;
    }

    public static int Execute(string packPath, ulong firstSeed, int seeds, int minutes, ulong traceSeed = ulong.MaxValue)
    {
        if (seeds <= 0 || minutes <= 0) throw new ArgumentException("--seeds and --minutes must be positive.");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        bool fullSample = seeds >= 24 && minutes >= 64;
        int townCivs = 0, cityCivs = 0, touchingPairs = 0, pairs = 0, raidMatches = 0, warMatches = 0, captureMatches = 0;
        int civs = 0, cascadeMatches = 0, totalSecessions = 0, e3Civs = 0, e3Completed = 0, e3Incomplete = 0, cohesionCount = 0;
        double claimed = 0, settlements = 0, cohesionSum = 0;
        double endDominion = 0, endMastery = 0, endFirsts = 0, endWonders = 0;
        int chapterEndSamples = 0, chapterEndRaidTotal = 0;
        int key4Completed = 0;
        Console.WriteLine($"M10 politics: {seeds} seed(s) from {firstSeed}, up to {minutes} minutes; 4 Chieftain AI, Balanced archetype");
        for (int i = 0; i < seeds; i++)
        {
            ulong seed = firstSeed + (ulong)i;
            Sample r = Play(seed, pack, minutes, seed == traceSeed);
            townCivs += r.TownCivs; cityCivs += r.CityCivs; civs += r.Civs;
            touchingPairs += r.TouchingPairs; pairs += r.Pairs;
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
            Console.WriteLine($"seed {r.Seed}: KEY-4 {r.Key4Completed}/{r.Civs}, latest @ {r.Key4LatestMinute:F1}; run ended @ {r.Minute:F1}; claim {r.Claimed:P1}; chapter-end raids {r.ChapterEndRaids}, wars {r.ChapterEndWars}, captures {r.ChapterEndCaptures}, secessions in KEY-3→KEY-4 {r.Secessions}");
            if (seed == traceSeed) PrintTraceSummary(r);
        }
        double townRate = townCivs / (double)Math.Max(1, civs), cityRate = cityCivs / (double)Math.Max(1, civs);
        B17ReportMetrics b17Metrics = SummarizeB17(settlements, claimed, key4Completed, touchingPairs, pairs);
        double claimMean = b17Metrics.ClaimedLandShare, settlementMean = b17Metrics.SettlementsPerCiv;
        double warRate = warMatches / (double)seeds, captureRate = captureMatches / (double)seeds;
        double secessionsPerCivPerE3 = totalSecessions / (double)Math.Max(1, e3Completed);
        double cohesionMean = cohesionSum / Math.Max(1, cohesionCount);
        double endLegacy = endDominion + endMastery + endFirsts + endWonders;
        double endDomPct = 100 * endDominion / Math.Max(1e-9, endLegacy), endMasPct = 100 * endMastery / Math.Max(1e-9, endLegacy);
        double endFirstPct = 100 * endFirsts / Math.Max(1e-9, endLegacy), endWonderPct = 100 * endWonders / Math.Max(1e-9, endLegacy);
        Console.WriteLine($"B17 KEY-4: {settlementMean:F1} settlements/civ, claimed land {claimMean:P1}, paired borders {touchingPairs}/{pairs} sampled; KEY-4 completed {key4Completed}/{seeds * 4}");
        Console.WriteLine($"B17 chapter end: >=1 Town {townRate:P1}, >=1 City {cityRate:P1} ({chapterEndSamples}/{seeds} chapter-end samples)");
        Console.WriteLine($"B18 chapter-end Legacy split: Dominion {endDomPct:F1}%, Mastery {endMasPct:F1}%, Firsts {endFirstPct:F1}%, Wonders {endWonderPct:F1}%");
        Console.WriteLine($"B19 KEY-3→KEY-4 exposure: cohesion mean {cohesionMean:F1}, secessions/civ/E3 {secessionsPerCivPerE3:F2}, exposures complete {e3Completed}/{e3Civs} (incomplete {e3Incomplete}), cascades {cascadeMatches}/{seeds}");
        Console.WriteLine($"B20 chapter end: raids mean {chapterEndRaidTotal / (double)seeds:F1}, matches with war {warRate:P1}, with capture {captureRate:P1}");
        if (!fullSample)
        {
            Console.WriteLine("M10 politics DIAGNOSTIC ONLY: acceptance requires 24 seeds and a 64-minute chapter horizon.");
            return 2;
        }
        bool b17 = settlementMean >= 10 && settlementMean <= 13 && townRate >= .75 && cityRate >= .25 && claimMean >= .30 && claimMean <= .42 && b17Metrics.PairedBorderTouchRate >= .75 && key4Completed == seeds * 4 && chapterEndSamples == seeds;
        bool b18 = endDomPct >= 45 && endDomPct <= 55 && endMasPct >= 25 && endMasPct <= 35 && endFirstPct >= 12 && endFirstPct <= 20 && endWonderPct <= 8 && chapterEndSamples == seeds;
        bool b19 = e3Civs == seeds * 4 && e3Completed == seeds * 4 && e3Incomplete == 0 &&
            secessionsPerCivPerE3 >= .03 && secessionsPerCivPerE3 <= .08 && cascadeMatches / (double)seeds < .02 && cohesionMean >= 60 && cohesionMean <= 80;
        bool b20 = chapterEndRaidTotal / (double)seeds >= 3 && chapterEndRaidTotal / (double)seeds <= 12 && warRate >= .5 && captureRate >= .2 && captureRate <= .5 && chapterEndSamples == seeds;
        Console.WriteLine($"B17 {(b17 ? "PASS" : "FAIL")}  B18 {(b18 ? "PASS" : "FAIL")}  B19 {(b19 ? "PASS" : "FAIL")}  B20 {(b20 ? "PASS" : "FAIL")}");
        return b17 && b18 && b19 && b20 ? 0 : 1;
    }

    private static Sample Play(ulong seed, ContentPack pack, int minutes, bool trace)
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
        int season = 0;
        bool[] pairSampled = new bool[2];
        bool endSampled = false;
        while (runner.Tick < limit)
        {
            runner.Advance(150);
            int currentSeason = (int)(runner.Tick / ResearchSystem.SeasonTicks);
            if (currentSeason != season)
            {
                season = currentSeason;
                for (int c = 0; c < 4; c++) secessionBySeason[c, season % 8] = 0;
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
                    if (!pairSampled[pair] && key4Sampled[mate])
                    {
                        pairSampled[pair] = true;
                        result.Pairs++;
                        if (Touching(politics.Territory, pair * 2, pair * 2 + 1)) result.TouchingPairs++;
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
                for (int c = 0; c < 4; c++)
                {
                    bool hasTown = false, hasCity = false;
                    for (int s = 0; s < economy.SettlementCount; s++)
                    {
                        var lite = economy.SettlementLite(s);
                        if (lite.Civilization != c || politics.IsFreeFolk(s) || economy.Population(lite.Id) <= 0) continue;
                        SettlementTier tier = economy.Civic(s).Tier;
                        if (tier >= SettlementTier.Town) hasTown = true;
                        if (tier == SettlementTier.City) hasCity = true;
                    }
                    if (hasTown) result.TownCivs++;
                    if (hasCity) result.CityCivs++;
                }
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
        if (trace)
        {
            result.TraceDecisions = match.Ai?.Log.Entries.ToArray() ?? Array.Empty<AiDecision>();
            result.TraceEvents = politics.Events.Select(e => e.Kind.ToString()).ToArray();
        }
        result.CascadeMatches = Array.Exists(cascade, x => x) ? 1 : 0;
        return result;
    }

    private static void PrintTraceSummary(Sample r)
    {
        Console.WriteLine($"trace seed {r.Seed}: decisions Raid={r.TraceDecisions.Count(d => d.Kind == "Raid")}, War={r.TraceDecisions.Count(d => d.Kind == "War")}, Assault={r.TraceDecisions.Count(d => d.Kind == "Assault")}; launches Raid={r.TraceEvents.Count(k => k == PoliticsEventKind.RaidLaunched.ToString())}, War={r.TraceEvents.Count(k => k == PoliticsEventKind.WarDeclared.ToString())}, Assault={r.TraceEvents.Count(k => k == PoliticsEventKind.AssaultLaunched.ToString())}");
        var decrees = r.TraceDecisions.Where(d => d.Kind == "Decree")
            .GroupBy(d => (DecreeKind)d.Subject).OrderBy(g => g.Key).ToArray();
        Console.WriteLine("  chosen decrees: " + (decrees.Length == 0
            ? "none"
            : string.Join(", ", decrees.Select(g => $"{g.Key}={g.Count()}"))));
        var foundCandidates = r.TraceDecisions.Where(d => d.Kind == "DecreeCandidate" && d.Subject == (int)DecreeKind.Found).ToArray();
        AiDecision bestFound = foundCandidates.OrderByDescending(d => d.Score.ToDoubleForDisplay()).FirstOrDefault();
        Console.WriteLine($"  Found decree candidates: {foundCandidates.Length}; positive {foundCandidates.Count(d => d.Score.ToDoubleForDisplay() > 0)}; best {bestFound.Score.ToDoubleForDisplay():F1} ({(foundCandidates.Length == 0 ? "none" : bestFound.Reason)})");
        foreach (AiDecision d in r.TraceDecisions.Where(d => d.Kind is "Plan" or "Posture")
                     .OrderBy(d => d.Tick).ThenBy(d => d.Civ))
            Console.WriteLine($"  {d.Kind} civ{d.Civ} season {d.Season}: {d.Reason}");
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

}
