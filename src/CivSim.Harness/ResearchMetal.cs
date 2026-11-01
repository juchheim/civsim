using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;

namespace CivSim.Harness;

/// <summary>
/// <c>research --report-metal</c> (docs/MVP/22 §15 T17–T19, §16 B11–B13): four Balanced civilizations on the Council over
/// each seed, sampled every season. Reports metal demand per 100 people per year in steady state (T17), bronze coverage
/// twelve seasons after MET-04/05 (T18), standing wood drawn down by the end (T19) and chop/mining efficiency (B11).
/// It reports; it never tunes.
/// </summary>
internal static class ResearchMetal
{
    private static readonly ulong[] DefaultSeeds = { 20261019UL, 777UL };

    private sealed class T18CohortRow
    {
        public int Civilization, SettlementId, FirstSeason, Season, Adults, Rung6Adults;
        public string AccessPath = "";
        /// <summary>Whether the window opened with a workable deposit of the path's scarce input (tin or arsenical ore), not only stored stock.</summary>
        public bool SourceBacked;
        public List<T18WindowCheckpoint> Checkpoints = new();
    }

    private readonly record struct T18WindowCheckpoint(int Offset, int Season, string Detail);

    private sealed class T18CensoredWindow
    {
        public int Civilization, SettlementId, FirstSeason, CensoredSeason;
        public string AccessPath = "", Reason = "";
        public bool SourceBacked;
        public List<T18WindowCheckpoint> Checkpoints = new();

        public T18CensoredWindow(int civilization, int settlementId, int firstSeason, int censoredSeason, string accessPath, string reason)
        {
            Civilization = civilization; SettlementId = settlementId; FirstSeason = firstSeason; CensoredSeason = censoredSeason;
            AccessPath = accessPath; Reason = reason;
        }
    }

    private sealed class CivTrace
    {
        public double Key3 = double.NaN, Key4 = double.NaN, Met03 = double.NaN, Met04 = double.NaN, Met05 = double.NaN,
            Fir09 = double.NaN, Min04 = double.NaN, Wod04 = double.NaN;
        public long BronzeStart = -1;
        public MetalStatistics AtWindow, AtEnd;
        public double Coverage12 = double.NaN, CoverageAccess12 = double.NaN;
        public double CoverageEnd, People, Settlements, Camps, MineHeads;
        public int FoodSecurityBelow80, WinterForecastBelow80;
        public string Summary = "";
    }

    private sealed class Result
    {
        public ulong Seed;
        public List<KeyValuePair<string, int>> CampAttempts = new();
        public CivTrace[] Civs = Array.Empty<CivTrace>();
        public List<(int Pop, bool Coppiced, double Start, double Now, int Civ)> Forests = new();
        public List<double> SettlementCoverage12 = new();
        public List<T18CohortRow> T18EligibleRows = new();
        public List<T18CensoredWindow> T18CensoredWindows = new();
        public int T18Started, T18NoAdults, T18RightCensored;
        public List<T18CohortRow> T18CastableEligibleRows = new();
        public List<T18CensoredWindow> T18CastableCensoredWindows = new();
        public int T18CastableStarted, T18CastableNoAdults, T18CastableRightCensored;
        public List<double> T18CastableCoverage12 = new();
        public List<B12T18Snapshot> B12T18Snapshots = new();
        public List<B12T18PlanSnapshot> B12T18PlanSnapshots = new();
        public List<string> B12MiningWantTrace = new();
        public Dictionary<int, B13WoodSnapshot> B13AtKey3 = new();
        public Dictionary<int, B13WoodSnapshot> B13AtKey4 = new();
        public Dictionary<(int Civilization, int Tile), B13TileWoodSnapshot> B13TilesAtKey3 = new();
        public Dictionary<(int Civilization, int Tile), B13TileWoodSnapshot> B13TilesAtKey4 = new();
        public Dictionary<(int SettlementId, int Tile), B13OuterWoodSnapshot> B13OuterAtKey3 = new();
        public Dictionary<(int SettlementId, int Tile), B13OuterWoodSnapshot> B13OuterAtKey4 = new();
        public List<string> B12CampOpenTrace = new();
        public List<string> B12CopperShareTrace = new();
        public double B12CopperShareShortfallTotal;
        public bool[] ForestsAtKey4 = Array.Empty<bool>();
        public int[] FirstT18AccessSeason = Array.Empty<int>();
        public bool[] T18CohortClosed = Array.Empty<bool>();
        public string[] T18AccessPath = Array.Empty<string>();
        public bool[] T18SourceBacked = Array.Empty<bool>();
        public List<T18WindowCheckpoint>?[] T18WindowCheckpoints = Array.Empty<List<T18WindowCheckpoint>?>();
        public int[] FirstT18CastableSeason = Array.Empty<int>();
        public bool[] T18CastableCohortClosed = Array.Empty<bool>();
        public double ChopBronze, ChopStone;
        public int ChopSamples;
        public double MineBest, MineSurface;
    }

    public static int Execute(ContentPack pack, IReadOnlyList<ulong>? seeds, int minutes, bool decreeMines = false)
    {
        IReadOnlyList<ulong> list = seeds is { Count: > 0 } ? seeds : DefaultSeeds;
        var results = new Result[list.Count];
        Parallel.For(0, list.Count, i => results[i] = Play(list[i], pack, minutes, decreeMines));
        var text = new StringBuilder();
        text.AppendLine($"Metal report: {list.Count} seed(s), {minutes} minutes, four Balanced civilizations on the Council" +
                        (decreeMines ? "; each season every civilization opens a camp on its nearest copper, arsenical and tin deposit within mining reach (a stand-in for the Open a mine Decree, WP8/WP9)" : "; camps by the planner only (own land)"));
        var t17 = new List<double[]>();
        var cover = new List<double>();
        var coverAccess = new List<double>();
        var coverSettlements = new List<double>();
        foreach (Result r in results) coverSettlements.AddRange(r.SettlementCoverage12);
        foreach (Result r in results)
        {
            text.AppendLine($"seed {r.Seed}:");
            text.AppendLine($"  {"civ",-4}{"KEY-3",7}{"KEY-4",7}{"MET-03",8}{"MET-04",8}{"MET-05",8}{"FIR-09",8}{"MIN-04",8}{"WOD-04",8}{"cov12",7}{"covA12",7}{"covEnd",7}{"people",7}{"settl",6}{"camps",6}");
            for (int c = 0; c < r.Civs.Length; c++)
            {
                CivTrace t = r.Civs[c];
                text.AppendLine($"  {c,-4}{F(t.Key3),7}{F(t.Key4),7}{F(t.Met03),8}{F(t.Met04),8}{F(t.Met05),8}{F(t.Fir09),8}{F(t.Min04),8}{F(t.Wod04),8}" +
                                $"{P(t.Coverage12),7}{P(t.CoverageAccess12),7}{P(t.CoverageEnd),7}{t.People,7:F0}{t.Settlements,6:F0}{t.Camps,6:F0}");
                if (!double.IsNaN(t.Coverage12)) cover.Add(t.Coverage12);
                if (!double.IsNaN(t.CoverageAccess12)) coverAccess.Add(t.CoverageAccess12);
                double[]? d = Demand(t);
                if (d is not null) t17.Add(d);
                text.AppendLine("      " + t.Summary);
            }
        }
        text.AppendLine();
        text.AppendLine("T17 metal demand per 100 people per year (steady state: from 12 seasons after MET-04/05 to the end)");
        text.AppendLine("  target: bronze 0.8-1.0 L; tin 0.08 L; charcoal 2.3-4.5 L; wood 20-26 L (14-19 bellows); labor 3-4% (all +-20%)");
        foreach (double[] d in t17)
            text.AppendLine($"  bronze {d[0]:F2} L (tin bronze {d[1]:F2}, arsenical {d[2]:F2})  tin {d[3]:F3} L  charcoal {d[4]:F2} L  wood {d[5]:F1} L  labor {d[6] * 100:F1}%");
        if (t17.Count > 0)
            text.AppendLine($"  median: bronze {Median(t17, 0):F2} L, tin {Median(t17, 3):F3} L, charcoal {Median(t17, 4):F2} L, wood {Median(t17, 5):F1} L, labor {Median(t17, 6) * 100:F1}%");
        text.AppendLine();
        int t18Started = results.Sum(r => r.T18Started);
        int t18Lost = results.Sum(r => r.T18CensoredWindows.Count(x => x.Reason == "qualifying input access lost"));
        int t18NoAdults = results.Sum(r => r.T18NoAdults);
        int t18RightCensored = results.Sum(r => r.T18RightCensored);
        int t18Adults = results.Sum(r => r.T18EligibleRows.Sum(x => x.Adults));
        int t18Rung6Adults = results.Sum(r => r.T18EligibleRows.Sum(x => x.Rung6Adults));
        double t18AdultWeighted = t18Adults == 0 ? double.NaN : (double)t18Rung6Adults / t18Adults;
        int t18CastableAdults = results.Sum(r => r.T18CastableEligibleRows.Sum(x => x.Adults));
        int t18CastableRung6Adults = results.Sum(r => r.T18CastableEligibleRows.Sum(x => x.Rung6Adults));
        double t18CastableAdultWeighted = t18CastableAdults == 0 ? double.NaN : (double)t18CastableRung6Adults / t18CastableAdults;
        var castableCoverage = results.SelectMany(r => r.T18CastableCoverage12).ToList();
        int t18CastableLost = results.Sum(r => r.T18CastableCensoredWindows.Count(x => x.Reason == "qualifying input access lost"));
        if (results.Any(r => r.CampAttempts.Count > 0))
        {
            text.AppendLine("B12 camp-open attempts for unmet ore wants (ore/result: count, all seeds):");
            foreach (IGrouping<string, KeyValuePair<string, int>> g in results.SelectMany(r => r.CampAttempts).GroupBy(x => x.Key).OrderBy(g => g.Key, StringComparer.Ordinal))
                text.AppendLine($"  {g.Key}: {g.Sum(x => x.Value)}");
        }
        text.AppendLine($"T18 research-timed diagnostic (not conditioned on continuous settlement input access): civ-wide median {P(MedianOf(cover))}, " +
                        $"cast-tier-qualified settlements (CastTier >= 9) median {P(MedianOf(coverAccess))}.");
        text.AppendLine($"T18 live-input-access cohort (MET-04+arsenic, or MET-05+copper+tin; continuous through +12): adult-weighted {P(t18AdultWeighted)} ({t18Rung6Adults}/{t18Adults}), " +
                        $"per-settlement median {P(MedianOf(coverSettlements))} over {coverSettlements.Count} eligible rows, " +
                        $"{coverSettlements.FindAll(x => x >= 0.7).Count} at >= 70%; cohort starts {t18Started}, access lost before +12 {t18Lost}, still within +12 at horizon {t18RightCensored}, no adults at +12 {t18NoAdults} (target >= 70% where access holds)");
        foreach (bool backed in new[] { true, false })
        {
            var rows = results.SelectMany(r => r.T18EligibleRows).Where(x => x.SourceBacked == backed).ToList();
            var windows = results.SelectMany(r => r.T18CensoredWindows).Where(x => x.SourceBacked == backed).ToList();
            int adults = rows.Sum(x => x.Adults), rung6 = rows.Sum(x => x.Rung6Adults);
            int atTarget = rows.Count(x => x.Adults > 0 && (double)x.Rung6Adults / x.Adults >= 0.7);
            int lost = windows.Count(x => x.Reason == "qualifying input access lost");
            int open = windows.Count(x => x.Reason == "report horizon before +12");
            text.AppendLine($"T18 cohort by opening supply, {(backed ? "source-backed (workable tin/arsenical deposit at the start)" : "stock-only (stored input only)")}: " +
                            $"adult-weighted {P(adults == 0 ? double.NaN : (double)rung6 / adults)} ({rung6}/{adults}), {atTarget} of {rows.Count} eligible rows at >= 70%; " +
                            $"starts {rows.Count + windows.Count}, access lost before +12 {lost}, still within +12 at horizon {open}");
        }
        text.AppendLine($"T18 local-facility-chain diagnostic (raw input access plus local Forge and Kiln/Smelter; continuous through +12): " +
                        $"adult-weighted {P(t18CastableAdultWeighted)} ({t18CastableRung6Adults}/{t18CastableAdults}), " +
                        $"per-settlement median {P(MedianOf(castableCoverage))} over {castableCoverage.Count} eligible rows, " +
                        $"{castableCoverage.FindAll(x => x >= 0.7).Count} at >= 70%; cohort starts {results.Sum(r => r.T18CastableStarted)}, " +
                        $"access lost before +12 {t18CastableLost}, still within +12 at horizon {results.Sum(r => r.T18CastableRightCensored)}, " +
                        $"no adults at +12 {results.Sum(r => r.T18CastableNoAdults)} (diagnostic only; unchanged 70% target)");
        if (Environment.GetEnvironmentVariable("CIVSIM_TRACE_B12_CENSUS") == "1")
        {
            text.AppendLine("B12 report-horizon food safety (settlement-weighted):");
            foreach (Result r in results)
                for (int c = 0; c < r.Civs.Length; c++)
                {
                    CivTrace civ = r.Civs[c];
                    text.AppendLine($"  seed{r.Seed}/civ{c}: report-horizon food-security <0.8={civ.FoodSecurityBelow80}, winter-forecast <0.8={civ.WinterForecastBelow80} (settlement-weighted)");
                }
        }
        text.AppendLine();
        text.AppendLine("T19 standing wood on the forest tiles of each settlement's range, at KEY-4 against its KEY-3 baseline");
        var bands = new (int Low, int High)[] { (0, 39), (40, 79), (80, 100), (101, 1000) };
        foreach ((int low, int high) in bands)
            foreach (bool cop in new[] { false, true })
            {
                var drawn = new List<double>();
                foreach (Result r in results)
                    foreach (var f in r.Forests)
                        if (f.Pop >= low && f.Pop <= high && f.Coppiced == cop && f.Start > 0) drawn.Add(1 - f.Now / f.Start);
                if (drawn.Count == 0) continue;
                drawn.Sort();
                text.AppendLine($"  {low,3}-{high,-4} people, {(cop ? "WOD-04   " : "no WOD-04")}: {drawn.Count,3} settlements, drawn down median {P(MedianOf(drawn))} (p10 {P(drawn[drawn.Count / 10])}, p90 {P(drawn[drawn.Count * 9 / 10])})");
            }
        text.AppendLine("  target (07d R): towns of 80-100 people lose 25-70% without WOD-04, < 5% with it; villages none");
        text.AppendLine();
        double chopB = 0, chopS = 0, mineB = 0, mineS = 0;
        int n = 0, m = 0;
        foreach (Result r in results)
        {
            if (r.ChopSamples > 0) { chopB += r.ChopBronze / r.ChopSamples; chopS += r.ChopStone / r.ChopSamples; n++; }
            if (r.MineSurface > 0) { mineB += r.MineBest; mineS += r.MineSurface; m++; }
        }
        if (n > 0) text.AppendLine($"B11 chop: bronze kit {chopB / n:F2} L/ws against stone tools {chopS / n:F2} L/ws = x{chopB / Math.Max(1e-9, chopS):F2} (target x1.5)");
        if (m > 0) text.AppendLine($"B11 mining: shaft with bronze picks {mineB / m:F2} L/ws against surface digging {mineS / m:F2} = x{mineB / Math.Max(1e-9, mineS):F2} (target x1.5-2)");
        if (Environment.GetEnvironmentVariable("CIVSIM_TRACE_B12_T18") == "1")
            foreach (Result r in results) AppendB12T18Trace(text, r);
        if (Environment.GetEnvironmentVariable("CIVSIM_TRACE_B13_WOOD") == "1")
            foreach (Result r in results) AppendB13WoodTrace(text, r);
        foreach (Result r in results)
            if (r.B12CampOpenTrace.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("B12 failed civ0/s0 camp-opening decisions for own copper tile 7401, seasons 255-267:");
                foreach (string line in r.B12CampOpenTrace) text.AppendLine("  " + line);
            }
        foreach (Result r in results)
            foreach (string row in r.B12MiningWantTrace) text.AppendLine(row);
        foreach (Result r in results)
            if (r.B12CopperShareTrace.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("B12 s0/s11 copper sharing and camp delivery, seasons 255-267:");
                foreach (string line in r.B12CopperShareTrace) text.AppendLine("  " + line);
                double oreDeficit = r.B12CopperShareShortfallTotal;
                // Current recipes: 2 Cu ore -> 0.5 Cu; 0.9 Cu -> 1 tin bronze; 0.15 bronze -> 1 tier-10 kit.
                double copperOrePerKit = 2.0 / 0.5 * 0.9 * 0.15;
                double kitEquivalentUpperBound = oreDeficit / copperOrePerKit;
                text.AppendLine($"  generous incremental T10-kit ceiling from those deficits: {oreDeficit:F2} Cu ore / {copperOrePerKit:F2} Cu ore per kit = {kitEquivalentUpperBound:F2} kit-equivalents (at most {Math.Floor(kitEquivalentUpperBound)} complete kits); six needed; cannot reach six. This ignores tin/charcoal, recipes' labor, and the unchanged 10% cap, all of which can only reduce the bound.");
            }
        foreach (Result r in results)
            if (r.T18EligibleRows.Count > 0 || r.T18CensoredWindows.Count > 0)
            {
                text.AppendLine();
                text.AppendLine($"B12 T18 shared live-input-access cohort (seed {r.Seed}; valid +12 rows and censored windows):");
                Dictionary<(int SettlementId, int Season), B12T18PlanSnapshot> allocations = r.B12T18PlanSnapshots
                    .ToDictionary(x => (x.SettlementId, x.Season));
                foreach (T18CohortRow x in r.T18EligibleRows.OrderBy(x => x.Civilization).ThenBy(x => x.SettlementId))
                {
                    text.AppendLine($"  civ{x.Civilization}/s{x.SettlementId} start=season{x.FirstSeason} +12=season{x.Season} access={x.AccessPath} supply={(x.SourceBacked ? "source" : "stock")} coverage={x.Rung6Adults}/{x.Adults}({(double)x.Rung6Adults / x.Adults:P0})");
                    if (x.Rung6Adults * 100 < x.Adults * 70)
                        AppendCheckpoints(x.SettlementId, x.Checkpoints);
                }
                foreach (T18CensoredWindow x in r.T18CensoredWindows.OrderBy(x => x.Civilization).ThenBy(x => x.SettlementId))
                {
                    text.AppendLine($"  censored civ{x.Civilization}/s{x.SettlementId} start=season{x.FirstSeason} at=season{x.CensoredSeason} access={x.AccessPath} reason={x.Reason}");
                    AppendCheckpoints(x.SettlementId, x.Checkpoints);
                }

                void AppendCheckpoints(int settlementId, List<T18WindowCheckpoint> checkpoints)
                {
                    foreach (T18WindowCheckpoint checkpoint in checkpoints)
                    {
                        text.Append($"    +{checkpoint.Offset} season{checkpoint.Season}: {checkpoint.Detail}");
                        if (allocations.TryGetValue((settlementId, checkpoint.Season), out B12T18PlanSnapshot plan))
                            text.Append($" | normal-call plan before RunMetal: {DescribePlanAllocation(plan)}");
                        else
                            text.Append(" | plan allocation not sampled (no cast-tier plan in the normal call)");
                        text.AppendLine();
                    }
                }

                static string DescribePlanAllocation(B12T18PlanSnapshot plan)
                {
                    static string F(Fix64 value) => value.ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture);
                    return $"tier={plan.Tier} forge/smelter={plan.Forges}/{plan.Smelters} buildKind={plan.BuildKind} " +
                        $"rung6={plan.Rung6Adults}/{plan.Adults} work={F(plan.TotalWork)} cap={F(plan.PushCap)} foodSec/winter={F(plan.FoodSecurity)}/{F(plan.LateWinterForecast)} " +
                        $"chain={F(plan.PrimaryChain)}*{F(plan.PrimaryScale)}+{F(plan.SecondaryChain)}*{F(plan.SecondaryScale)} " +
                        $"build assigned/need={F(plan.BuildAssigned)}/{F(plan.BuildNeed)} " +
                        $"civic->metal-build={F(plan.CivicBuildTransfer)}{(plan.CivicTransferBlock.Length > 0 ? " blocked:" + plan.CivicTransferBlock : "")} " +
                        $"requested/cap(build)={F(plan.BuildRequested)}/{F(plan.BuildCap)}; " +
                        $"assigned/need(mine,charcoal,smelt)={F(plan.MineAssigned)}/{F(plan.MineNeed)} " +
                        $"{F(plan.CharcoalAssigned)}/{F(plan.CharcoalNeed)} " +
                        $"{F(plan.SmeltAssigned)}/{F(plan.SmeltNeed)} " +
                        $"stock/want(ore cu/as/tin,char)={F(plan.CopperOreStock)}/{F(plan.CopperOreWant)} " +
                        $"{F(plan.ArsenicalOreStock)}/{F(plan.ArsenicalOreWant)} {F(plan.TinOreStock)}/{F(plan.TinOreWant)} " +
                        $"{F(plan.CharcoalStock)}/{F(plan.CharcoalWant)} " +
                        $"jobs(fire,care,staff,scout,upkeep,orchard,food,civic,build,herd,material,unused)=" +
                        $"{F(plan.FireAssigned)}/{F(plan.CareAssigned)}/{F(plan.StaffAssigned)}/{F(plan.ScoutAssigned)}/" +
                        $"{F(plan.UpkeepAssigned)}/{F(plan.OrchardAssigned)}/{F(plan.FoodAssigned)}/{F(plan.CivicAssigned)}/" +
                        $"{F(plan.ConstructionAssigned)}/{F(plan.HerdAssigned)}/{F(plan.MaterialAssigned)}/" +
                        $"food-requested={F(plan.FoodRequested)} food-assigned={F(plan.FoodAssigned)} " +
                        $"idle(adult,child)={F(plan.AdultUnusedWork)},{F(plan.ChildUnusedWork)}";
                }
            }
        Console.Write(text.ToString());
        return 0;
    }

    private static string DescribeT18Checkpoint(LivingEconomyModel eco, int settlement)
    {
        MetalReadout readout = eco.Metal(settlement);
        static string F64(Fix64 value) => value.ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture);
        string read = $"metal castTier={readout.CastTier} access(cu/as/tin)={readout.CopperAccess}/{readout.ArsenicAccess}/{readout.TinAccess} " +
            $"forge/smelter={readout.Forges}/{readout.Smelters} adults={readout.Adults} kits8/9/10={readout.Kits8}/{readout.Kits9}/{readout.Kits10} " +
            $"stock(cuOre/asOre/tinOre/charcoal/copper/tin/bronze)={F64(readout.CopperOre)}/{F64(readout.ArsenicalOre)}/{F64(readout.TinOre)}/" +
            $"{F64(readout.Charcoal)}/{F64(readout.Copper)}/{F64(readout.Tin)}/{F64(readout.Bronze)}";
        return $"{read} | read-only state: {eco.DescribeMetalStateReadOnly(settlement)}; current plan not recomputed";
    }

    private static double[]? Demand(CivTrace t)
    {
        MetalStatistics a = t.AtWindow, b = t.AtEnd;
        double years100 = (b.PeopleSeasons - a.PeopleSeasons).ToDoubleForDisplay() / 4 / 100;
        if (t.BronzeStart < 0 || years100 <= 0.5) return null;
        double tin = (b.Bronze - a.Bronze).ToDoubleForDisplay();
        double ars = (b.ArsenicalBronze - a.ArsenicalBronze).ToDoubleForDisplay();
        double labor = (b.Labor - a.Labor).ToDoubleForDisplay();
        return new[]
        {
            (tin + ars) / years100, tin / years100, ars / years100,
            (b.Tin - a.Tin).ToDoubleForDisplay() / years100,
            (b.Charcoal - a.Charcoal).ToDoubleForDisplay() / years100,
            ((b.CharcoalWood - a.CharcoalWood) + (b.FireSettingWood - a.FireSettingWood)).ToDoubleForDisplay() / years100,
            labor <= 0 ? 0 : (b.MetalLabor - a.MetalLabor).ToDoubleForDisplay() / labor,
        };
    }

    private static Result Play(ulong seed, ContentPack pack, int minutes, bool decreeMines)
    {
        var archetypes = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        MatchModel match = MatchModel.Create(seed, pack, archetypes);
        bool traceB12T18 = seed == 20261019UL && Environment.GetEnvironmentVariable("CIVSIM_TRACE_B12_T18") == "1";
        bool traceB12MiningWant = Environment.GetEnvironmentVariable("CIVSIM_TRACE_B12_MINING") == "1";
        match.Economy.B12MiningWantTraceEnabled = traceB12MiningWant;
        match.Economy.B12T18TraceEnabled = traceB12T18;
        bool traceB12Census = Environment.GetEnvironmentVariable("CIVSIM_TRACE_B12_CENSUS") == "1";
        match.Economy.B12CensusTraceEnabled = traceB12Census;
        if (Environment.GetEnvironmentVariable("CIVSIM_B12_CHAIN_CAP_X") is { } capX) match.Economy.ChainCapMultiplier = Fix64.Parse(capX);
        match.Economy.BlockChainForUnobtainableOre = Environment.GetEnvironmentVariable("CIVSIM_B12_BLOCK_UNOBTAINABLE") == "1";
        if (Environment.GetEnvironmentVariable("CIVSIM_B12_MINEHEAD_WOOD") is { } headWood) match.Economy.MineHeadWood = Fix64.Parse(headWood);
        if (Environment.GetEnvironmentVariable("CIVSIM_B12_BUILD_CAP_X") is { } buildX) match.Economy.BuildCapMultiplier = Fix64.Parse(buildX);
        match.Economy.OpenOwnCampDespiteSister = Environment.GetEnvironmentVariable("CIVSIM_B12_OWN_CAMP") == "1";
        match.Economy.AdultsTakeMetalReserveFirst = Environment.GetEnvironmentVariable("CIVSIM_B12_ADULT_METAL_FIRST") == "1";
        string? civicRelease = Environment.GetEnvironmentVariable("CIVSIM_B12_CIVIC_RELEASE");
        match.Economy.CivicToMetalReleasesHousing = civicRelease is "housing" or "all";
        match.Economy.CivicToMetalReleasesCanals = civicRelease == "all";
        bool traceB13Wood = Environment.GetEnvironmentVariable("CIVSIM_TRACE_B13_WOOD") == "1" && (seed == 20261019UL || seed == 777UL);
        match.Economy.B13WoodTraceEnabled = traceB13Wood;
        bool traceB12CampOpen = seed == 20261019UL && Environment.GetEnvironmentVariable("CIVSIM_TRACE_B12_CAMP_OPEN") == "1";
        match.Economy.B12CampOpenTraceEnabled = traceB12CampOpen;
        bool traceB12CopperShare = seed == 20261019UL && Environment.GetEnvironmentVariable("CIVSIM_TRACE_B12_COPPER_SHARE") == "1";
        match.Economy.B12CopperShareTraceEnabled = traceB12CopperShare;
        var runner = new SimRunner(match);
        ResearchSystem r = match.Research;
        LivingEconomyModel eco = match.Economy;
        int civs = r.CivCount;
        var result = new Result { Seed = seed, Civs = new CivTrace[civs], ForestsAtKey4 = new bool[civs] };
        result.FirstT18AccessSeason = new int[eco.SettlementCount];
        Array.Fill(result.FirstT18AccessSeason, -1);
        result.T18CohortClosed = new bool[eco.SettlementCount];
        result.T18AccessPath = new string[eco.SettlementCount];
        result.T18SourceBacked = new bool[eco.SettlementCount];
        result.T18WindowCheckpoints = new List<T18WindowCheckpoint>?[eco.SettlementCount];
        result.FirstT18CastableSeason = new int[eco.SettlementCount];
        Array.Fill(result.FirstT18CastableSeason, -1);
        result.T18CastableCohortClosed = new bool[eco.SettlementCount];
        for (int c = 0; c < civs; c++) result.Civs[c] = new CivTrace();
        int Idx(string id) => r.Catalog.IndexOf(id);
        int key3 = Idx("KEY-3"), key4 = Idx("KEY-4"), met03 = Idx("MET-03"), met04 = Idx("MET-04"), met05 = Idx("MET-05");
        int fir09 = Idx("FIR-09"), min04 = Idx("MIN-04"), wod04 = Idx("WOD-04");
        long limit = minutes * 600L;
        while (runner.Tick < limit)
        {
            runner.Advance(1);
            if (runner.Tick % ResearchSystem.SeasonTicks != 0) continue;
            long season = runner.Tick / ResearchSystem.SeasonTicks;
            if (decreeMines) for (int c = 0; c < civs; c++) eco.EmulateMineDecrees(c);
            for (int c = 0; c < civs; c++)
            {
                CivTrace t = result.Civs[c];
                double T(int p) => r.IsDone(c, p) ? r.CompletedTick(c, p) / 600.0 : double.NaN;
                t.Key3 = T(key3); t.Key4 = T(key4); t.Met03 = T(met03); t.Met04 = T(met04); t.Met05 = T(met05);
                t.Fir09 = T(fir09); t.Min04 = T(min04); t.Wod04 = T(wod04);
                if (traceB13Wood && r.IsDone(c, key3) && !result.B13AtKey3.Values.Any(x => x.Civilization == c))
                {
                    foreach (B13WoodSnapshot snap in eco.CaptureB13WoodSnapshots((int)season).Where(x => x.Civilization == c)) result.B13AtKey3[snap.SettlementId] = snap;
                    foreach (B13TileWoodSnapshot tile in eco.CaptureB13TileWoodSnapshots(c)) result.B13TilesAtKey3[(c, tile.Tile)] = tile;
                    foreach (B13OuterWoodSnapshot tile in eco.CaptureB13OuterWoodSnapshots(c, (int)season)) result.B13OuterAtKey3[(tile.SettlementId, tile.Tile)] = tile;
                }
                if (traceB13Wood && r.IsDone(c, key4) && !result.B13AtKey4.Values.Any(x => x.Civilization == c))
                {
                    foreach (B13WoodSnapshot snap in eco.CaptureB13WoodSnapshots((int)season).Where(x => x.Civilization == c)) result.B13AtKey4[snap.SettlementId] = snap;
                    foreach (B13TileWoodSnapshot tile in eco.CaptureB13TileWoodSnapshots(c)) result.B13TilesAtKey4[(c, tile.Tile)] = tile;
                    foreach (B13OuterWoodSnapshot tile in eco.CaptureB13OuterWoodSnapshots(c, (int)season)) result.B13OuterAtKey4[(tile.SettlementId, tile.Tile)] = tile;
                }
                double first = Math.Min(double.IsNaN(t.Met04) ? double.MaxValue : t.Met04, double.IsNaN(t.Met05) ? double.MaxValue : t.Met05);
                if (first == double.MaxValue) continue;
                long firstSeason = (long)Math.Ceiling(first * 4);
                if (t.BronzeStart < 0) t.BronzeStart = firstSeason;
                if (season == firstSeason + 12)
                {
                    (double all, double access) = Coverage(eco, match.Politics, c);
                    t.Coverage12 = all;
                    t.CoverageAccess12 = access;
                    t.AtWindow = eco.MetalStats(c);
                }
            }
            if (Environment.GetEnvironmentVariable("CIVSIM_TRACE_SETTLEMENTS") is { } watch && season % 2 == 0)
                foreach (string part in watch.Split(','))
                    if (int.TryParse(part, out int ws) && ws < eco.SettlementCount)
                        Console.WriteLine($"[t{season / 4.0:F1}y m{runner.Tick / 600.0:F1}] {eco.DescribeMetal(ws)}");
            // T19 is measured at KEY-4 (07d §5), not at the arbitrary end of the harness run. Keep population and WOD-04
            // status on the same sample tick as forest stock so later growth, fission, or research cannot re-bin it.
            for (int c = 0; c < civs; c++)
            {
                CivTrace t = result.Civs[c];
                if (result.ForestsAtKey4[c] || !ReachedKey4(season, t.Key4)) continue;
                result.ForestsAtKey4[c] = true;
                bool coppiced = r.IsDone(c, wod04);
                for (int s = 0; s < eco.SettlementCount; s++)
                {
                    var v = eco.SettlementLite(s);
                    if (v.Civilization != c) continue;
                    int pop = eco.Population(v.Id);
                    MetalReadout m = eco.Metal(s);
                    var observation = ForestObservation(pop, match.Politics.IsFreeFolk(s), coppiced,
                        m.StandingWoodAtStart.ToDoubleForDisplay(), m.StandingWoodInRange.ToDoubleForDisplay());
                    if (observation is { } sample)
                        result.Forests.Add((sample.Pop, sample.Coppiced, sample.Start, sample.Now, c));
                }
            }
            // Settlements can be founded/fissioned after the report starts; preserve a tracker slot for every new settlement.
            if (eco.SettlementCount > result.FirstT18AccessSeason.Length)
            {
                int oldLength = result.FirstT18AccessSeason.Length;
                Array.Resize(ref result.FirstT18AccessSeason, eco.SettlementCount);
                Array.Fill(result.FirstT18AccessSeason, -1, oldLength, eco.SettlementCount - oldLength);
                Array.Resize(ref result.T18CohortClosed, eco.SettlementCount);
                Array.Resize(ref result.T18AccessPath, eco.SettlementCount);
                Array.Resize(ref result.T18SourceBacked, eco.SettlementCount);
                Array.Resize(ref result.T18WindowCheckpoints, eco.SettlementCount);
                Array.Resize(ref result.FirstT18CastableSeason, eco.SettlementCount);
                Array.Fill(result.FirstT18CastableSeason, -1, oldLength, eco.SettlementCount - oldLength);
                Array.Resize(ref result.T18CastableCohortClosed, eco.SettlementCount);
            }
            // T18 starts from live input access, never residual alloy stock, and requires the same access through +12.
            for (int s = 0; s < eco.SettlementCount && s < result.FirstT18AccessSeason.Length; s++)
            {
                if (match.Politics.IsFreeFolk(s))
                {
                    if (result.FirstT18AccessSeason[s] >= 0 && !result.T18CohortClosed[s])
                    {
                        result.T18CensoredWindows.Add(new T18CensoredWindow(eco.SettlementLite(s).Civilization, s,
                            result.FirstT18AccessSeason[s], (int)season, result.T18AccessPath[s], "became Free Folk")
                        { SourceBacked = result.T18SourceBacked[s], Checkpoints = result.T18WindowCheckpoints[s] ?? new List<T18WindowCheckpoint>() });
                        result.T18CohortClosed[s] = true;
                    }
                    continue;
                }
                int civ = eco.SettlementLite(s).Civilization;
                MetalReadout mr = eco.Metal(s);
                bool arsenic = r.IsDone(civ, met04) && eco.SettlementHasMetalInputAccess(s, MetalAccessKind.Arsenic);
                bool tin = r.IsDone(civ, met05) && eco.SettlementHasMetalInputAccess(s, MetalAccessKind.Copper) &&
                    eco.SettlementHasMetalInputAccess(s, MetalAccessKind.Tin);
                bool hasAccess = arsenic || tin;
                if (result.FirstT18AccessSeason[s] < 0)
                {
                    if (!hasAccess) continue;
                    result.FirstT18AccessSeason[s] = (int)season;
                    result.T18AccessPath[s] = arsenic ? "MET-04+arsenic" : "MET-05+copper+tin";
                    result.T18SourceBacked[s] = eco.SettlementHasMetalSource(s, arsenic ? MetalAccessKind.Arsenic : MetalAccessKind.Tin);
                    result.T18Started++;
                    if (traceB12Census)
                    {
                        result.T18WindowCheckpoints[s] = new List<T18WindowCheckpoint>
                        {
                            new(0, (int)season, DescribeT18Checkpoint(eco, s))
                        };
                    }
                    continue;
                }
                if (result.T18CohortClosed[s]) continue;
                int firstSeason = result.FirstT18AccessSeason[s];
                if (!hasAccess)
                {
                    result.T18CensoredWindows.Add(new T18CensoredWindow(civ, s, firstSeason, (int)season,
                        result.T18AccessPath[s], "qualifying input access lost")
                    { SourceBacked = result.T18SourceBacked[s], Checkpoints = result.T18WindowCheckpoints[s] ?? new List<T18WindowCheckpoint>() });
                    result.T18CohortClosed[s] = true;
                    continue;
                }
                int offset = (int)season - firstSeason;
                if (traceB12Census && (offset is 4 or 8 or 12) && result.T18WindowCheckpoints[s] is { } checkpoints)
                    checkpoints.Add(new T18WindowCheckpoint(offset, (int)season, DescribeT18Checkpoint(eco, s)));
                if (season != firstSeason + 12) continue;
                result.T18CohortClosed[s] = true;
                if (mr.Adults <= 0)
                {
                    result.T18NoAdults++;
                    result.T18CensoredWindows.Add(new T18CensoredWindow(civ, s, firstSeason, (int)season,
                        result.T18AccessPath[s], "no adults at +12")
                    { SourceBacked = result.T18SourceBacked[s], Checkpoints = result.T18WindowCheckpoints[s] ?? new List<T18WindowCheckpoint>() });
                    continue;
                }
                int rung6 = mr.Kits9 + mr.Kits10;
                result.SettlementCoverage12.Add((double)rung6 / mr.Adults);
                result.T18EligibleRows.Add(new T18CohortRow
                {
                    Civilization = civ, SettlementId = s, FirstSeason = firstSeason, Season = (int)season,
                    Adults = mr.Adults, Rung6Adults = rung6, AccessPath = result.T18AccessPath[s], SourceBacked = result.T18SourceBacked[s],
                    Checkpoints = result.T18WindowCheckpoints[s] ?? new List<T18WindowCheckpoint>()
                });
            }
            // This supplemental T18 clock starts only once local casting infrastructure is usable. It keeps the same
            // technology/input-access rule and +12 target, but separates facility rollout from the upgrade-push window.
            for (int s = 0; s < eco.SettlementCount; s++)
            {
                int civ = eco.SettlementLite(s).Civilization;
                if (match.Politics.IsFreeFolk(s))
                {
                    if (result.FirstT18CastableSeason[s] >= 0 && !result.T18CastableCohortClosed[s])
                    {
                        result.T18CastableCensoredWindows.Add(new T18CensoredWindow(civ, s,
                            result.FirstT18CastableSeason[s], (int)season, "local facility chain", "became Free Folk"));
                        result.T18CastableCohortClosed[s] = true;
                    }
                    continue;
                }
                MetalReadout castReadout = eco.Metal(s);
                bool arsenicAccess = r.IsDone(civ, met04) && eco.SettlementHasMetalInputAccess(s, MetalAccessKind.Arsenic);
                bool tinAccess = r.IsDone(civ, met05) && eco.SettlementHasMetalInputAccess(s, MetalAccessKind.Copper) &&
                    eco.SettlementHasMetalInputAccess(s, MetalAccessKind.Tin);
                bool hasAccess = arsenicAccess || tinAccess;
                bool hasLocalCastingPath = eco.HasLocalMetalCastingFacilities(s);
                if (result.FirstT18CastableSeason[s] < 0)
                {
                    if (!hasAccess || !hasLocalCastingPath) continue;
                    result.FirstT18CastableSeason[s] = (int)season;
                    result.T18CastableStarted++;
                    continue;
                }
                if (result.T18CastableCohortClosed[s]) continue;
                int firstCastableSeason = result.FirstT18CastableSeason[s];
                if (!hasAccess)
                {
                    result.T18CastableCensoredWindows.Add(new T18CensoredWindow(civ, s, firstCastableSeason,
                        (int)season, "local facility chain", "qualifying input access lost"));
                    result.T18CastableCohortClosed[s] = true;
                    continue;
                }
                if (!hasLocalCastingPath)
                {
                    result.T18CastableCensoredWindows.Add(new T18CensoredWindow(civ, s, firstCastableSeason,
                        (int)season, "local facility chain", "local facility unavailable"));
                    result.T18CastableCohortClosed[s] = true;
                    continue;
                }
                if ((int)season != firstCastableSeason + 12) continue;
                result.T18CastableCohortClosed[s] = true;
                if (castReadout.Adults <= 0)
                {
                    result.T18CastableNoAdults++;
                    result.T18CastableCensoredWindows.Add(new T18CensoredWindow(civ, s, firstCastableSeason,
                        (int)season, "local facility chain", "no adults at +12"));
                    continue;
                }
                int castRung6 = castReadout.Kits9 + castReadout.Kits10;
                double castCoverage = (double)castRung6 / castReadout.Adults;
                result.T18CastableCoverage12.Add(castCoverage);
                result.T18CastableEligibleRows.Add(new T18CohortRow
                {
                    Civilization = civ, SettlementId = s, FirstSeason = firstCastableSeason, Season = (int)season,
                    Adults = castReadout.Adults, Rung6Adults = castRung6, AccessPath = "local facility chain"
                });
            }
            if (season % 8 == 0)
                for (int s = 0; s < eco.SettlementCount; s++)
                {
                    (Fix64 bronze, Fix64 stone) = eco.ChopRates(s);
                    if (bronze <= Fix64.Zero) continue;
                    result.ChopBronze += bronze.ToDoubleForDisplay();
                    result.ChopStone += stone.ToDoubleForDisplay();
                    result.ChopSamples++;
                }
        }
        for (int c = 0; c < civs; c++)
        {
            CivTrace t = result.Civs[c];
            t.AtEnd = eco.MetalStats(c);
            t.CoverageEnd = Coverage(eco, match.Politics, c).All;
            int people = 0, settlements = 0, heads = 0, k8 = 0, k9 = 0, k10 = 0;
            double copperOre = 0, tinOre = 0, charcoal = 0, tin = 0, bronze = 0;
            var reasons = new SortedSet<string>(StringComparer.Ordinal);
            for (int s = 0; s < eco.SettlementCount; s++)
            {
                var v = eco.SettlementLite(s);
                if (v.Civilization != c || match.Politics.IsFreeFolk(s)) continue;
                int pop = eco.Population(v.Id);
                if (pop == 0) continue;
                people += pop;
                settlements++;
                if (v.LastFoodSecurity < Fix64.Parse("0.8")) t.FoodSecurityBelow80++;
                if (v.LateWinterFoodForecast < Fix64.Parse("0.8")) t.WinterForecastBelow80++;
                MetalReadout m = eco.Metal(s);
                heads += m.MineHeads; k8 += m.Kits8; k9 += m.Kits9; k10 += m.Kits10;
                copperOre += m.CopperOre.ToDoubleForDisplay(); tinOre += m.TinOre.ToDoubleForDisplay();
                charcoal += m.Charcoal.ToDoubleForDisplay(); tin += m.Tin.ToDoubleForDisplay(); bronze += m.Bronze.ToDoubleForDisplay();
                if (m.Reason != MethodFallbackReason.None) reasons.Add(m.Reason.ToString());
            }
            int camps = 0;
            for (int i = 0; i < eco.MiningCampCount; i++) if (eco.MiningCamp(i).Civilization == c) camps++;
            if (Environment.GetEnvironmentVariable("CIVSIM_TRACE_METAL") == "1")
                for (int s = 0; s < eco.SettlementCount; s++)
                    if (eco.SettlementLite(s).Civilization == c && eco.Population(eco.SettlementLite(s).Id) > 0)
                        Console.WriteLine($"[{seed}] {eco.DescribeMetal(s)}");
            t.People = people; t.Settlements = settlements; t.Camps = camps; t.MineHeads = heads;
            MetalStatistics st = t.AtEnd;
            t.Summary = $"kits T8 {k8} T9 {k9} T10 {k10}; mine heads {heads}; stores copper ore {copperOre:F1} tin ore {tinOre:F1} charcoal {charcoal:F1} tin {tin:F2} bronze {bronze:F2}; " +
                        $"made: bronze {st.Bronze.ToDoubleForDisplay():F1} arsenical {st.ArsenicalBronze.ToDoubleForDisplay():F1} copper {st.Copper.ToDoubleForDisplay():F1} " +
                        $"charcoal {st.Charcoal.ToDoubleForDisplay():F1} kits {st.Kits8.ToDoubleForDisplay():F0}/{st.Kits9.ToDoubleForDisplay():F0}/{st.Kits10.ToDoubleForDisplay():F0} arms {st.ArmsCast.ToDoubleForDisplay():F0}; " +
                        $"reasons {string.Join(",", reasons)}";
            (Fix64 best, Fix64 surface) = eco.MineRates(c);
            if (r.IsDone(c, min04)) { result.MineBest = best.ToDoubleForDisplay(); result.MineSurface = surface.ToDoubleForDisplay(); }
        }
        if ((traceB12T18 || traceB12Census) && eco.B12T18Snapshots is { } snapshots)
        {
            result.B12T18Snapshots.AddRange(snapshots);
        }
        if (traceB12Census && eco.B12T18PlanSnapshots is { } planSnapshots)
            result.B12T18PlanSnapshots.AddRange(planSnapshots);
        for (int s = 0; s < result.FirstT18AccessSeason.Length; s++)
            if (result.FirstT18AccessSeason[s] >= 0 && !result.T18CohortClosed[s])
            {
                result.T18RightCensored++;
                result.T18CensoredWindows.Add(new T18CensoredWindow(eco.SettlementLite(s).Civilization, s,
                    result.FirstT18AccessSeason[s], minutes * 4, result.T18AccessPath[s], "report horizon before +12")
                { SourceBacked = result.T18SourceBacked[s], Checkpoints = result.T18WindowCheckpoints[s] ?? new List<T18WindowCheckpoint>() });
            }
        for (int s = 0; s < result.FirstT18CastableSeason.Length; s++)
            if (result.FirstT18CastableSeason[s] >= 0 && !result.T18CastableCohortClosed[s])
            {
                result.T18CastableRightCensored++;
                result.T18CastableCensoredWindows.Add(new T18CensoredWindow(eco.SettlementLite(s).Civilization, s,
                    result.FirstT18CastableSeason[s], minutes * 4, "local facility chain", "report horizon before +12"));
            }
        if (traceB12MiningWant) result.B12MiningWantTrace.AddRange(eco.B12MiningWantTrace);
        if (traceB12MiningWant && eco.B12CampAttemptCounts is { } attempts) result.CampAttempts.AddRange(attempts);
        if (traceB12CampOpen) result.B12CampOpenTrace.AddRange(eco.B12CampOpenTrace);
        if (traceB12CopperShare)
        {
            result.B12CopperShareTrace.AddRange(eco.B12CopperShareTrace);
            result.B12CopperShareShortfallTotal = eco.B12CopperShareShortfallTotal.ToDoubleForDisplay();
        }
        return result;
    }

    private static void AppendB12T18Trace(StringBuilder text, Result result)
    {
        if (result.Seed != 20261019UL) return;
        var outcomes = result.B12T18Snapshots.Where(x => x.Offset == 12 && x.Adults > 0).ToArray();
        static double Coverage(B12T18Snapshot x) => (double)x.Rung6Adults / x.Adults;
        B12T18Snapshot? failed = outcomes.Where(x => x.Rung6Adults * 100 < x.Adults * 70).OrderByDescending(Coverage)
            .ThenBy(x => x.Civilization).ThenBy(x => x.SettlementId).Cast<B12T18Snapshot?>().FirstOrDefault();
        B12T18Snapshot? passed = outcomes.Where(x => x.Rung6Adults * 100 >= x.Adults * 70).OrderBy(Coverage)
            .ThenBy(x => x.Civilization).ThenBy(x => x.SettlementId).Cast<B12T18Snapshot?>().FirstOrDefault();
        text.AppendLine();
        text.AppendLine("B12 T18 aligned settlement trace (first cast access, then +4/+8/+12 seasons):");
        Append("failed", failed);
        Append("passed", passed);

        void Append(string label, B12T18Snapshot? outcome)
        {
            if (outcome is not { } selected)
            {
                text.AppendLine($"  {label}: no settlement with an observed +12-season outcome");
                return;
            }
            text.AppendLine($"  {label}: selected civ{selected.Civilization}/s{selected.SettlementId} T18={Coverage(selected):P0}");
            foreach (B12T18Snapshot snapshot in result.B12T18Snapshots
                         .Where(x => x.SettlementId == selected.SettlementId && x.Civilization == selected.Civilization)
                         .OrderBy(x => x.Offset))
            {
                text.AppendLine($"    +{snapshot.Offset} season={snapshot.Season} civ{snapshot.Civilization}/s{snapshot.SettlementId} " +
                    $"rung6={snapshot.Rung6Adults}/{snapshot.Adults} ({Coverage(snapshot):P0}) {snapshot.Detail}");
                if (snapshot.Offset == 12) text.AppendLine($"      bottleneck: {snapshot.Census}");
            }
        }
    }

    private static void AppendB13WoodTrace(StringBuilder text, Result result)
    {
        text.AppendLine();
        text.AppendLine($"B13 wood balance at KEY-3 to KEY-4 (seed {result.Seed}; WOD-04 towns, pop 80-100):");
        foreach (B13WoodSnapshot close in result.B13AtKey4.Values
                     .Where(x => x.IsTown && x.Population is >= 80 and <= 100 && !double.IsNaN(result.Civs[x.Civilization].Wod04))
                     .OrderBy(x => x.Civilization).ThenBy(x => x.SettlementId))
        {
            if (!result.B13AtKey3.TryGetValue(close.SettlementId, out B13WoodSnapshot open))
            {
                text.AppendLine($"  civ{close.Civilization}/s{close.SettlementId} pop={close.Population}: no KEY-3 opening sample");
                continue;
            }
            Fix64 dChopIn = close.ChopInflows - open.ChopInflows, dCivicIn = close.CivicInflows - open.CivicInflows;
            Fix64 dOtherIn = close.OtherInflows - open.OtherInflows;
            Fix64 dHearth = close.HearthUse - open.HearthUse, dFuel = close.E3FuelUse - open.E3FuelUse;
            Fix64 dMetalUse = close.MetalUse - open.MetalUse, dGoods = close.GoodsUse - open.GoodsUse;
            Fix64 dCivicUse = close.CivicUse - open.CivicUse, dStructure = close.StructureUse - open.StructureUse;
            Fix64 dOtherUse = close.OtherUse - open.OtherUse;
            Fix64 dRegrowth = close.Regrowth - open.Regrowth, dChop = close.ChopRemoved - open.ChopRemoved;
            Fix64 dMetal = close.MetalRemoved - open.MetalRemoved, dCivic = close.CivicRemoved - open.CivicRemoved;
            Fix64 dOther = close.OtherRemoved - open.OtherRemoved;
            Fix64 localResidual = open.LocalWood + dChopIn + dCivicIn + dOtherIn - dHearth - dFuel - dMetalUse - dGoods - dCivicUse - dStructure - dOtherUse - close.LocalWood;
            Fix64 forestResidual = open.StandingForestWood + dRegrowth - dChop - dMetal - dCivic - dOther - close.StandingForestWood;
            text.AppendLine($"  civ{close.Civilization}/s{close.SettlementId} pop={close.Population} KEY3=season{open.Season} local={F64(open.LocalWood)} forest={F64(open.StandingForestWood)} KEY4=season{close.Season} local={F64(close.LocalWood)} forest={F64(close.StandingForestWood)}");
            text.AppendLine($"    local inflow chop={F64(dChopIn)} civic/housing={F64(dCivicIn)} other={F64(dOtherIn)}; use hearth={F64(dHearth)} E3fire={F64(dFuel)} metal={F64(dMetalUse)} goods={F64(dGoods)} civic/housing={F64(dCivicUse)} structure={F64(dStructure)} other={F64(dOtherUse)} residual={F64(localResidual)}");
            text.AppendLine($"    forest regrowth={F64(dRegrowth)} removed chop/clear={F64(dChop)} metal={F64(dMetal)} civic={F64(dCivic)} other={F64(dOther)} clearThreshold={F64(close.ClearThresholdRemoval-open.ClearThresholdRemoval)} crossings={close.ClearThresholdCrossings-open.ClearThresholdCrossings} residual={F64(forestResidual)}");
        }
        var tileRows = result.B13TilesAtKey4
            .Where(pair => pair.Value.OverlappingSettlements.Length > 0 && result.B13TilesAtKey3.ContainsKey(pair.Key))
            .Select(pair => (Close: pair.Value, Open: result.B13TilesAtKey3[pair.Key]))
            .Select(x => (x.Close, x.Open, Loss: x.Open.Standing - x.Close.Standing))
            .OrderByDescending(x => x.Loss.ToDoubleForDisplay()).Take(12).ToArray();
        text.AppendLine("  worst forest tiles in selected town ranges (tile: stock KEY3→KEY4; net loss; removals chop/metal/civic/other; regrowth; threshold crossings/removal; overlapping towns):");
        foreach (var row in tileRows)
        {
            B13TileWoodSnapshot c = row.Close, o = row.Open;
            Fix64 d = c.ChopRemoved - o.ChopRemoved, m = c.MetalRemoved - o.MetalRemoved;
            Fix64 cv = c.CivicRemoved - o.CivicRemoved, ot = c.OtherRemoved - o.OtherRemoved, rg = c.Regrowth - o.Regrowth;
            text.AppendLine($"    tile{c.Tile}: {F64(o.Standing)}->{F64(c.Standing)} loss={F64(row.Loss)} removed={F64(d)}/{F64(m)}/{F64(cv)}/{F64(ot)} regrowth={F64(rg)} clearCrossings={c.ClearThresholdCrossings - o.ClearThresholdCrossings} clearRemoved={F64(c.ClearThresholdRemoval-o.ClearThresholdRemoval)} overlap=[{string.Join(",", c.OverlappingSettlements.Select(id => $"s{id}"))}]");
        }
        text.AppendLine("  outer wood candidates for GatherMaterials (d=4-8; excludes rival-owned tiles; per-town totals can overlap; potential stock, not observed harvest):");
        foreach (B13WoodSnapshot close in result.B13AtKey4.Values
                     .Where(x => x.IsTown && x.Population is >= 80 and <= 100 && !double.IsNaN(result.Civs[x.Civilization].Wod04))
                     .OrderBy(x => x.Civilization).ThenBy(x => x.SettlementId))
        {
            var open = result.B13OuterAtKey3.Values.Where(x => x.SettlementId == close.SettlementId).ToDictionary(x => x.Tile);
            var now = result.B13OuterAtKey4.Values.Where(x => x.SettlementId == close.SettlementId).ToDictionary(x => x.Tile);
            AppendOuterAvailability(text, $"KEY3 civ{close.Civilization}/s{close.SettlementId}", open.Values);
            AppendOuterAvailability(text, $"KEY4 civ{close.Civilization}/s{close.SettlementId}", now.Values);
            AppendOuterTileRows(text, "KEY3", open.Values);
            AppendOuterTileRows(text, "KEY4", now.Values);
        }
        static string F64(Fix64 x) => x.ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture);
    }

    private static void AppendOuterTileRows(StringBuilder text, string stage, IEnumerable<B13OuterWoodSnapshot> source)
    {
        B13OuterWoodSnapshot[] tiles = source.Where(x => x.Standing > Fix64.Zero)
            .OrderByDescending(x => x.Standing).ThenBy(x => x.Distance).ThenBy(x => x.Tile).ToArray();
        int sampleCount = Math.Min(8, tiles.Length);
        foreach (B13OuterWoodSnapshot tile in tiles.Take(sampleCount))
            text.AppendLine($"      {stage} tile{tile.Tile} {tile.Standing.ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture)}L d{tile.Distance} {(tile.IsForest ? "forest" : "nonforest")} potentialOtherSettlements=[{string.Join(",", tile.Competitors.Select(id => $"s{id}"))}]");
        if (tiles.Length > sampleCount)
            text.AppendLine($"      {stage} tile details: showing {sampleCount} of {tiles.Length} positive-stock candidates (highest standing stock)");
    }

    private static void AppendOuterAvailability(StringBuilder text, string label, IEnumerable<B13OuterWoodSnapshot> source)
    {
        B13OuterWoodSnapshot[] tiles = source.ToArray();
        Fix64 forest = Fix64.Zero, nonforest = Fix64.Zero, contested = Fix64.Zero, haul = Fix64.Zero;
        var byDistance = new Fix64[9];
        foreach (B13OuterWoodSnapshot tile in tiles)
        {
            if (tile.IsForest) forest += tile.Standing;
            else nonforest += tile.Standing;
            if (tile.Competitors.Length > 0) contested += tile.Standing;
            haul += tile.Standing * (Fix64.FromRatio(1, 8) + Fix64.FromRatio(tile.Distance, 40));
            byDistance[tile.Distance] += tile.Standing;
        }
        text.AppendLine($"    {label}: candidateTiles={tiles.Length} reachableStanding={F64(forest + nonforest)}L forest={F64(forest)}L nonforest={F64(nonforest)}L potentiallySharedStanding={F64(contested)}L grossHaulEstimate={F64(haul)}ws distanceStock=[d4:{F64(byDistance[4])},d5:{F64(byDistance[5])},d6:{F64(byDistance[6])},d7:{F64(byDistance[7])},d8:{F64(byDistance[8])}]");
        static string F64(Fix64 x) => x.ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture);
    }

    private static (double All, double Access) Coverage(LivingEconomyModel eco, PoliticsSystem pol, int civ)
    {
        int adults = 0, rung6 = 0, accessAdults = 0, accessRung6 = 0;
        for (int s = 0; s < eco.SettlementCount; s++)
        {
            var v = eco.SettlementLite(s);
            if (v.Civilization != civ || pol.IsFreeFolk(s)) continue;
            MetalReadout m = eco.Metal(s);
            int six = m.Kits9 + m.Kits10;
            adults += m.Adults;
            rung6 += six;
            if (m.CastTier >= 9) { accessAdults += m.Adults; accessRung6 += six; }
        }
        return (adults == 0 ? double.NaN : (double)rung6 / adults, accessAdults == 0 ? double.NaN : (double)accessRung6 / accessAdults);
    }

    private static double Median(List<double[]> rows, int column)
    {
        var values = new List<double>();
        foreach (double[] row in rows) values.Add(row[column]);
        return MedianOf(values);
    }

    private static double MedianOf(List<double> values)
    {
        if (values.Count == 0) return double.NaN;
        var sorted = new List<double>(values);
        sorted.Sort();
        int n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2;
    }

    private static string F(double v) => double.IsNaN(v) ? "-" : v.ToString("F1", CultureInfo.InvariantCulture);
    private static string P(double v) => double.IsNaN(v) ? "-" : (v * 100).ToString("F0", CultureInfo.InvariantCulture) + "%";

    internal static bool ReachedKey4(long season, double key4Minute)
        => !double.IsNaN(key4Minute) && season >= (int)Math.Ceiling(key4Minute * 4);

    internal static (int Pop, bool Coppiced, double Start, double Now)? ForestObservation(
        int pop, bool freeFolk, bool coppiced, double start, double now)
        => pop <= 0 || freeFolk || start <= 0 ? null : (pop, coppiced, start, now);
}
