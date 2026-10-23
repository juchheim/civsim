using System;
using System.Collections.Generic;
using System.Globalization;
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

    private sealed class CivTrace
    {
        public double Key3 = double.NaN, Key4 = double.NaN, Met03 = double.NaN, Met04 = double.NaN, Met05 = double.NaN,
            Fir09 = double.NaN, Min04 = double.NaN, Wod04 = double.NaN;
        public long BronzeStart = -1;
        public MetalStatistics AtWindow, AtEnd;
        public double Coverage12 = double.NaN, CoverageAccess12 = double.NaN;
        public double CoverageEnd, People, Settlements, Camps, MineHeads;
        public string Summary = "";
    }

    private sealed class Result
    {
        public ulong Seed;
        public CivTrace[] Civs = Array.Empty<CivTrace>();
        public List<(int Pop, bool Coppiced, double Start, double Now, int Civ)> Forests = new();
        public List<double> SettlementCoverage12 = new();
        public long[] FirstBronzeSeason = new long[4096];
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
        text.AppendLine($"T18 share of adults on rung 6 twelve seasons after MET-04/05: civ-wide median {P(MedianOf(cover))}, " +
                        $"settlements with tin or arsenic access median {P(MedianOf(coverAccess))}; per settlement, 12 seasons after it could first cast bronze: " +
                        $"median {P(MedianOf(coverSettlements))} over {coverSettlements.Count}, {coverSettlements.FindAll(x => x >= 0.7).Count} at >= 70% (target >= 70% where access holds)");
        text.AppendLine();
        text.AppendLine("T19 standing wood on the forest tiles of each settlement's range, at the end against its Bronze start");
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
        Console.Write(text.ToString());
        return 0;
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
        var runner = new SimRunner(match);
        ResearchSystem r = match.Research;
        LivingEconomyModel eco = match.Economy;
        int civs = r.CivCount;
        var result = new Result { Seed = seed, Civs = new CivTrace[civs] };
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
            // T18 where access holds: from the season a settlement can first cast bronze (tier 9 or 10), its rung-6 share 12 seasons on.
            for (int s = 0; s < eco.SettlementCount && s < result.FirstBronzeSeason.Length; s++)
            {
                if (match.Politics.IsFreeFolk(s)) continue;
                MetalReadout mr = eco.Metal(s);
                if (result.FirstBronzeSeason[s] == 0 && mr.CastTier >= 9 && mr.Forges > 0) result.FirstBronzeSeason[s] = season;
                else if (result.FirstBronzeSeason[s] > 0 && season == result.FirstBronzeSeason[s] + 12 && mr.Adults > 0)
                    result.SettlementCoverage12.Add((double)(mr.Kits9 + mr.Kits10) / mr.Adults);
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
                MetalReadout m = eco.Metal(s);
                heads += m.MineHeads; k8 += m.Kits8; k9 += m.Kits9; k10 += m.Kits10;
                copperOre += m.CopperOre.ToDoubleForDisplay(); tinOre += m.TinOre.ToDoubleForDisplay();
                charcoal += m.Charcoal.ToDoubleForDisplay(); tin += m.Tin.ToDoubleForDisplay(); bronze += m.Bronze.ToDoubleForDisplay();
                if (m.Reason != MethodFallbackReason.None) reasons.Add(m.Reason.ToString());
                if (m.StandingWoodAtStart > Fix64.Zero)
                    result.Forests.Add((pop, r.IsDone(c, wod04), m.StandingWoodAtStart.ToDoubleForDisplay(), m.StandingWoodInRange.ToDoubleForDisplay(), c));
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
        return result;
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
}
