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
/// <c>research --report-e3</c> (WP7; docs/MVP/22 §15 T7, T10–T16, T25, 07d §10–§11): four Balanced civilizations on the Council
/// over each seed, sampled every season. Reports people, settlements, Towns and Cities, FoodCap, claimed land and border contact
/// at each civilization's KEY-3 and KEY-4; E3 hazards against E0 (C3-E3), e45 and the epidemic share of deaths; Cohesion in E3
/// and secessions; E3 role Insight; and the E3 timing. It reports; it never tunes.
/// </summary>
internal static class ResearchE3
{
    private static readonly ulong[] DefaultSeeds = { 20261019UL, 777UL };

    private sealed class At
    {
        public double Minute = double.NaN, People, Settlements, Towns, Cities, FoodCap, Sustainable, Claimed, OwnShare, Largest,
            RolesShare, Insight, Fissions;
    }

    private sealed class CivTrace
    {
        public At K3 = new(), K4 = new(), End = new();
        // Deaths and person-seasons by era (0 and 3) and band (child, adult, elder); E3 elders by five-year bin from 45.
        public double[,] Deaths = new double[4, 3], Exposure = new double[4, 3];
        public double[] ElderDeaths = new double[12], ElderExposure = new double[12], E2ElderDeaths = new double[12], E2ElderExposure = new double[12];
        public double E3Deaths, E3Epidemic, E3Starvation;
        public double[,] CauseByEra = new double[4, 8], ElderCauseByEra = new double[4, 8];
        public double[] WinterSecurity = new double[4], WinterPeople = new double[4], Hungry = new double[4];
        public int FoundingsE3;
        public double FirstMarket = double.NaN, FirstTown = double.NaN, FirstCity = double.NaN, FirstPalace = double.NaN, FirstTemple = double.NaN;
        public List<int> Cohesion = new();
    }

    private sealed class Result
    {
        public ulong Seed;
        public CivTrace[] Civs = Array.Empty<CivTrace>();
        public double[,] Touch = new double[4, 4];
        public int Secessions, Captures, Raids;
        public double FinalMinute;
        // Expected seasonal hazard by exact age, summed over figures, in seasons when all four civilizations were in E2 or all in E3.
        public double[,] ExpHazard = new double[4, 100], ExpCount = new double[4, 100];
    }

    public static int Execute(ContentPack pack, IReadOnlyList<ulong>? seeds, int minutes)
    {
        IReadOnlyList<ulong> list = seeds is { Count: > 0 } ? seeds : DefaultSeeds;
        var results = new Result[list.Count];
        Parallel.For(0, list.Count, i => results[i] = Play(list[i], pack, minutes));
        var text = new StringBuilder();
        text.AppendLine($"E3 report: {list.Count} seed(s), {minutes} minutes, four Balanced civilizations on the Council");
        var k3 = new List<double>(); var k4 = new List<double>(); var e3 = new List<double>();
        var people = new List<double>(); var setts = new List<double>(); var foodRatio = new List<double>(); var food3 = new List<double>(); var food4 = new List<double>();
        var claimed = new List<double>(); var roles = new List<double>(); var towns = new List<double>(); var cities = new List<double>();
        var townsEnd = new List<double>(); var citiesEnd = new List<double>(); var endMin = new List<double>(); var peopleEnd = new List<double>();
        var settsEnd = new List<double>(); var bigEnd = new List<double>(); var claimedEnd = new List<double>();
        var coh = new List<int>(); var found = new List<double>(); var touchPaired = 0; var pairs = 0;
        double[,] deaths = new double[4, 3], exposure = new double[4, 3];
        double e3d = 0, e3epi = 0, e3starv = 0;
        double[] ed = new double[12], ee = new double[12], ed2 = new double[12], ee2 = new double[12];
        int secessions = 0, captures = 0, civCount = 0;
        foreach (Result r in results)
        {
            text.AppendLine($"seed {r.Seed} (captures {r.Captures}, raids {r.Raids}, secessions {r.Secessions}):");
            text.AppendLine($"  {"civ",-4}{"KEY-3",7}{"KEY-4",7}{"E3",6}{"ppl3",6}{"ppl4",6}{"set3",5}{"set4",5}{"town",5}{"city",5}{"big",5}{"food3",7}{"food4",7}{"x",6}{"clm4",6}{"own4",6}{"roles",7}{"ins4",6}{"found",6}{"C mean",7}{"C p10",6}{"C p90",6}");
            for (int c = 0; c < r.Civs.Length; c++)
            {
                CivTrace t = r.Civs[c];
                civCount++;
                double ratio = t.K4.FoodCap / Math.Max(1e-9, t.K3.FoodCap);
                var cs = new List<int>(t.Cohesion); cs.Sort();
                text.AppendLine($"  {c,-4}{F(t.K3.Minute),7}{F(t.K4.Minute),7}{F(t.K4.Minute - t.K3.Minute),6}{t.K3.People,6:F0}{t.K4.People,6:F0}{t.K3.Settlements,5:F0}{t.K4.Settlements,5:F0}" +
                                $"{t.K4.Towns,5:F0}{t.K4.Cities,5:F0}{t.K4.Largest,5:F0}{t.K3.FoodCap,7:F0}{t.K4.FoodCap,7:F0}{ratio,6:F2}{t.K4.Claimed * 100,6:F1}{t.K4.OwnShare * 100,6:F1}" +
                                $"{t.K4.RolesShare * 100,6:F1}%{t.K4.Insight,6:F0}{t.FoundingsE3,6}{(cs.Count == 0 ? double.NaN : Mean(cs)),7:F1}{Pct(cs, 0.1),6}{Pct(cs, 0.9),6}");
                if (!double.IsNaN(t.K3.Minute)) k3.Add(t.K3.Minute);
                if (!double.IsNaN(t.K4.Minute)) { k4.Add(t.K4.Minute); e3.Add(t.K4.Minute - t.K3.Minute); people.Add(t.K4.People); setts.Add(t.K4.Settlements);
                    foodRatio.Add(ratio); food3.Add(t.K3.FoodCap); food4.Add(t.K4.FoodCap); claimed.Add(t.K4.Claimed); roles.Add(t.K4.RolesShare);
                    towns.Add(t.K4.Towns > 0 ? 1 : 0); cities.Add(t.K4.Cities > 0 ? 1 : 0); found.Add(t.FoundingsE3); }
                coh.AddRange(t.Cohesion);
                if (!double.IsNaN(t.End.Minute))
                {
                    townsEnd.Add(t.End.Towns > 0 ? 1 : 0); citiesEnd.Add(t.End.Cities > 0 ? 1 : 0); endMin.Add(t.End.Minute);
                    peopleEnd.Add(t.End.People); settsEnd.Add(t.End.Settlements); bigEnd.Add(t.End.Largest); claimedEnd.Add(t.End.Claimed);
                }
                for (int e = 0; e < 4; e++) for (int b = 0; b < 3; b++) { deaths[e, b] += t.Deaths[e, b]; exposure[e, b] += t.Exposure[e, b]; }
                for (int k = 0; k < 12; k++) { ed[k] += t.ElderDeaths[k]; ee[k] += t.ElderExposure[k]; ed2[k] += t.E2ElderDeaths[k]; ee2[k] += t.E2ElderExposure[k]; }
                e3d += t.E3Deaths; e3epi += t.E3Epidemic; e3starv += t.E3Starvation;
            }
            secessions += r.Secessions; captures += r.Captures;
            for (int a = 0; a < 4; a += 2) { pairs++; if (r.Touch[a, a + 1] > 0) touchPaired++; }
        }
        text.AppendLine();
        text.AppendLine($"Timing: KEY-3 median {F(Median(k3))}, KEY-4 median {F(Median(k4))} (current projection 61.25; fresh acceptance pending), E3 duration median {F(Median(e3))} (current projection 19.75)");
        text.AppendLine($"T10 people per civ at KEY-4: median {F(Median(people))} (range {F(Min(people))}-{F(Max(people))}; historical target 170, 120-230; rebaseline at current cadence)");
        text.AppendLine($"T11 settlements per civ at KEY-4: median {F(Median(setts))} (range {F(Min(setts))}-{F(Max(setts))}; historical target 7, 5-10; rebaseline at current cadence)");
        text.AppendLine($"B17 Town/City rates at KEY-4 {P(MeanOf(towns))}/{P(MeanOf(cities))}; at chapter end {P(MeanOf(townsEnd))}/{P(MeanOf(citiesEnd))} (historical thresholds; rebaseline at current cadence)");
        text.AppendLine($"  at the chapter end (minute {F(Median(endMin))}): people per civ median {F(Median(peopleEnd))}, settlements {F(Median(settsEnd))}, largest {F(Median(bigEnd))}, claimed {P(Median(claimedEnd))}");
        text.AppendLine($"T12 mean FoodCap per settlement: KEY-3 {F(Median(food3))}, KEY-4 {F(Median(food4))}, ratio median {Median(foodRatio):F2} (C1-E3 target x2.16 on the average settlement, x1.74 from E3 projects alone)");
        double ann(int e, int b) => deaths[e, b] / Math.Max(1e-9, exposure[e, b] / 4);
        text.AppendLine($"T13 annual death rate E0 / E3: children {ann(0, 0) * 1000:F1} / {ann(3, 0) * 1000:F1} per mille -> ratio {ann(3, 0) / ann(0, 0):F3} (target 0.803 +-10%); " +
                        $"adults {ann(0, 1) * 1000:F1} / {ann(3, 1) * 1000:F1} -> {ann(3, 1) / ann(0, 1):F3} (target 0.779 +-10%)");
        text.AppendLine($"T13 e45 in E3 from the elder life table: {E45(ed, ee):F1} (target 17 +-10%; the same estimator gives {E45(ed2, ee2):F1} in E2, 09 anchor 16); E3 epidemic share of deaths {P(e3epi / Math.Max(1, e3d))} (B10 7-13%), starvation {P(e3starv / Math.Max(1, e3d))}");
        var cz = new double[4, 8]; var ecz = new double[4, 8];
        foreach (Result rr in results) foreach (CivTrace t in rr.Civs) for (int e = 0; e < 4; e++) for (int k = 0; k < 8; k++) { cz[e, k] += t.CauseByEra[e, k]; ecz[e, k] += t.ElderCauseByEra[e, k]; }
        for (int e = 0; e < 4; e++)
        {
            double tot = 0, etot = 0; for (int k = 0; k < 8; k++) { tot += cz[e, k]; etot += ecz[e, k]; }
            var line = new StringBuilder($"  deaths by cause in E{e} (all / elders):");
            for (int k = 0; k < 8; k++) line.Append($" {(EconomyDeathCause)k} {100 * cz[e, k] / Math.Max(1, tot):F0}/{100 * ecz[e, k] / Math.Max(1, etot):F0}%");
            text.AppendLine(line.ToString());
        }
        double[] ws = new double[4], wp = new double[4], hg = new double[4];
        foreach (Result rr in results) foreach (CivTrace t in rr.Civs) for (int e = 0; e < 4; e++) { ws[e] += t.WinterSecurity[e]; wp[e] += t.WinterPeople[e]; hg[e] += t.Hungry[e]; }
        text.AppendLine($"  winter food security (people-weighted) E0..E3: {ws[0] / Math.Max(1, wp[0]):F3} {ws[1] / Math.Max(1, wp[1]):F3} {ws[2] / Math.Max(1, wp[2]):F3} {ws[3] / Math.Max(1, wp[3]):F3}; people in a short winter {P(hg[0] / Math.Max(1, wp[0]))} {P(hg[1] / Math.Max(1, wp[1]))} {P(hg[2] / Math.Max(1, wp[2]))} {P(hg[3] / Math.Max(1, wp[3]))}");
        var eh = new double[4, 100]; var ec = new double[4, 100];
        foreach (Result rr in results) for (int e = 0; e < 4; e++) for (int a = 0; a < 100; a++) { eh[e, a] += rr.ExpHazard[e, a]; ec[e, a] += rr.ExpCount[e, a]; }
        double ExpE45(int e)
        {
            double surv = 1, years = 0;
            for (int a = 45; a < 100; a++)
            {
                double m = ec[e, a] > 0 ? 4 * eh[e, a] / ec[e, a] : 4.0;
                double q = 1 - Math.Exp(-m);
                years += surv * (1 - q / 2);
                surv *= 1 - q;
            }
            return years;
        }
        double ExpBand(int e, int lo, int hi)
        {
            double h = 0, n = 0;
            for (int a = lo; a < hi; a++) { h += eh[e, a]; n += ec[e, a]; }
            return n > 0 ? 4 * h / n : double.NaN;
        }
        text.AppendLine($"T13 from expected hazards (seasons with all four civs in the era): child annual E0 {ExpBand(0, 0, 14) * 1000:F1} E2 {ExpBand(2, 0, 14) * 1000:F1} E3 {ExpBand(3, 0, 14) * 1000:F1} per mille (09 E3 anchor 36.1); " +
                        $"adult E0 {ExpBand(0, 14, 45) * 1000:F1} E2 {ExpBand(2, 14, 45) * 1000:F1} E3 {ExpBand(3, 14, 45) * 1000:F1} (anchor 11.7); ratios E3/E0 child {ExpBand(3, 0, 14) / ExpBand(0, 0, 14):F3} (target 0.803) adult {ExpBand(3, 14, 45) / ExpBand(0, 14, 45):F3} (target 0.779); " +
                        $"e45 E2 {ExpE45(2):F1} E3 {ExpE45(3):F1} (target 17 +-10%)");
        text.AppendLine($"T14 claimed land at each civ's KEY-4: median {P(Median(claimed))} (target 30-42%)");
        text.AppendLine($"T15 paired neighbours' borders touching by the end: {touchPaired}/{pairs} (target >= 75% by KEY-4)");
        text.AppendLine($"T16 E3 foundings per civ (fissions KEY-3 -> KEY-4): median {F(Median(found))}");
        var cohSorted = new List<int>(coh); cohSorted.Sort();
        text.AppendLine($"T25 Cohesion over live settlement-seasons in E3: mean {(coh.Count == 0 ? double.NaN : Mean(coh)):F1}, p10 {Pct(cohSorted, 0.1)}, p50 {Pct(cohSorted, 0.5)}, p90 {Pct(cohSorted, 0.9)} (target typical 60-80); " +
                        $"secessions {secessions} in {civCount} civs = {secessions / (double)Math.Max(1, civCount):F3} per civ (07d: 0.03-0.08), captures {captures}");
        text.AppendLine($"T7 E3 roles' share of Insight at KEY-4: median {P(Median(roles))} (target 12-20%)");
        Console.Write(text.ToString());
        return 0;
    }

    private static Result Play(ulong seed, ContentPack pack, int minutes)
    {
        var archetypes = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        MatchModel match = MatchModel.Create(seed, pack, archetypes);
        var runner = new SimRunner(match);
        ResearchSystem r = match.Research;
        LivingEconomyModel eco = match.Economy;
        PoliticsSystem pol = match.Politics;
        int civs = r.CivCount;
        var result = new Result { Seed = seed, Civs = new CivTrace[civs] };
        for (int c = 0; c < civs; c++) result.Civs[c] = new CivTrace();
        int key3 = r.Catalog.IndexOf("KEY-3"), key4 = r.Catalog.IndexOf("KEY-4");
        long limit = minutes * 600L;
        var lastFissions = new int[4096];
        while (runner.Tick < limit)
        {
            runner.Advance(1);
            if (runner.Tick % ResearchSystem.SeasonTicks != 0) continue;
            double minute = runner.Tick / 600.0;
            int allEra = r.Era(0);
            for (int c = 1; c < civs; c++) if (r.Era(c) != allEra) allEra = -1;
            if (allEra is >= 0 and < 4)
            {
                var alive = new int[100];
                // Expected hazards were recorded before seasonal mortality and aging. Survivors are now
                // one year older; newborns have age 0 and were not exposed to this season's hazards.
                for (int f = 0; f < eco.FigureCount; f++)
                {
                    int age = PreMortalityAgeOfSurvivor(eco.Figure(f).Age);
                    if (age >= 0) alive[age]++;
                }
                for (int i = 0; i < eco.LastDeathEventCount; i++) alive[Math.Min(99, eco.LastDeathEvent(i).Age)]++;
                for (int a = 0; a < 100; a++)
                {
                    double h = 0;
                    foreach (Fix64 x in eco.LastExpectedHazardsByExactAgeAndCause(a)) h += x.ToDoubleForDisplay();
                    result.ExpHazard[allEra, a] += h;
                    result.ExpCount[allEra, a] += alive[a];
                }
            }
            // deaths of the season just ended, by the civilization's era
            for (int i = 0; i < eco.LastDeathEventCount; i++)
            {
                EconomyDeathEvent d = eco.LastDeathEvent(i);
                int era = r.Era(d.Civilization);
                CivTrace t = result.Civs[d.Civilization];
                if (era == 2 && d.Age >= 45) t.E2ElderDeaths[Math.Min(11, (d.Age - 45) / 5)]++;
                if (era < 4) t.CauseByEra[era, (int)d.Cause]++;
                if (era < 4 && d.Age >= 45) t.ElderCauseByEra[era, (int)d.Cause]++;
                if (era is not (0 or 3)) continue;
                t.Deaths[era, d.Age < 14 ? 0 : d.Age < 45 ? 1 : 2]++;
                if (era == 3)
                {
                    t.E3Deaths++;
                    if (d.Cause == EconomyDeathCause.Epidemic) t.E3Epidemic++;
                    if (d.Cause == EconomyDeathCause.Starvation) t.E3Starvation++;
                    if (d.Age >= 45) t.ElderDeaths[Math.Min(11, (d.Age - 45) / 5)]++;
                }
            }
            for (int f = 0; f < eco.FigureCount; f++)
            {
                FigureView v = eco.Figure(f);
                int era = r.Era(v.Civilization);
                CivTrace t = result.Civs[v.Civilization];
                if (era == 2 && v.Age >= 45) t.E2ElderExposure[Math.Min(11, (v.Age - 45) / 5)] += 0.25;
                if (era is not (0 or 3)) continue;
                t.Exposure[era, v.Age < 14 ? 0 : v.Age < 45 ? 1 : 2]++;
                if (era == 3 && v.Age >= 45) t.ElderExposure[Math.Min(11, (v.Age - 45) / 5)] += 0.25;
            }
            for (int s = 0; s < eco.SettlementCount && s < lastFissions.Length; s++)
            {
                var lite = eco.SettlementLite(s);
                if (lite.Fissions > lastFissions[s] && r.Era(lite.Civilization) == 3) result.Civs[lite.Civilization].FoundingsE3 += lite.Fissions - lastFissions[s];
                lastFissions[s] = lite.Fissions;
                int sera = r.Era(lite.Civilization);
                if (sera < 4 && runner.Tick % 600 == 0)
                {
                    int sp = eco.Population(lite.Id);
                    CivTrace st = result.Civs[lite.Civilization];
                    st.WinterSecurity[sera] += lite.LastFoodSecurity.ToDoubleForDisplay() * sp;
                    st.WinterPeople[sera] += sp;
                    if (lite.LastFoodSecurity < Fix64.One) st.Hungry[sera] += sp;
                }
                if (r.Era(lite.Civilization) == 3 && !pol.IsFreeFolk(s))
                {
                    CivicReadout cr = eco.Civic(s);
                    CivTrace ct = result.Civs[lite.Civilization];
                    if (double.IsNaN(ct.FirstMarket) && cr.Markets > 0) ct.FirstMarket = minute;
                    if (double.IsNaN(ct.FirstTown) && cr.Tier >= SettlementTier.Town) ct.FirstTown = minute;
                    if (double.IsNaN(ct.FirstCity) && cr.Tier == SettlementTier.City) ct.FirstCity = minute;
                    if (double.IsNaN(ct.FirstPalace) && cr.Palaces > 0) ct.FirstPalace = minute;
                    if (double.IsNaN(ct.FirstTemple) && cr.Temples > 0) ct.FirstTemple = minute;
                    if (cr.CohesionLive && eco.Population(lite.Id) > 0) result.Civs[lite.Civilization].Cohesion.Add(cr.Cohesion);
                }
            }
            for (int c = 0; c < civs; c++)
            {
                CivTrace t = result.Civs[c];
                if (double.IsNaN(t.K3.Minute) && r.IsDone(c, key3)) Sample(t.K3, c, minute, match);
                if (double.IsNaN(t.K4.Minute) && r.IsDone(c, key4)) Sample(t.K4, c, minute, match);
            }
            if (Environment.GetEnvironmentVariable("CIVSIM_TRACE_CIV") is { } tc && runner.Tick % 600 == 0 && r.Era(int.Parse(tc, CultureInfo.InvariantCulture)) >= 3)
            {
                int tcv = int.Parse(tc, CultureInfo.InvariantCulture), best = -1, bestPop = -1;
                for (int s = 0; s < eco.SettlementCount; s++)
                    if (eco.SettlementLite(s).Civilization == tcv && eco.Population(eco.SettlementLite(s).Id) > bestPop) { best = s; bestPop = eco.Population(eco.SettlementLite(s).Id); }
                if (best >= 0) Console.WriteLine($"[t{minute:F0}] {eco.DescribeCivic(best)}");
            }
            MatchEnd chapter = match.EndOfMatch();
            bool ended = chapter.Reason != MatchEndReason.YearCap && chapter.IsOver(runner.Tick);
            if (ended && double.IsNaN(result.Civs[0].End.Minute))
                for (int c = 0; c < civs; c++) Sample(result.Civs[c].End, c, minute, match);
            bool all = ended;
            for (int c = 0; c < civs; c++) all &= r.IsDone(c, key4);
            if (all) break;
        }
        result.FinalMinute = runner.Tick / 600.0;
        if (Environment.GetEnvironmentVariable("CIVSIM_TRACE_E3") is not null)
            for (int c = 0; c < civs; c++)
            {
                CivTrace tt = result.Civs[c];
                var sb = new StringBuilder($"[{seed} civ{c} done] market@{F(tt.FirstMarket)} town@{F(tt.FirstTown)} palace@{F(tt.FirstPalace)} temple@{F(tt.FirstTemple)} city@{F(tt.FirstCity)}");
                foreach (string id in new[] { "KEY-3", "TRD-04", "AGR-05", "AGR-06", "AGR-07", "HUS-05", "COM-05", "SOC-08", "SOC-09", "BEL-06", "GOV-05", "GOV-06", "LRN-04", "BLD-07", "STN-11", "FOD-08", "TRN-05", "KEY-4" })
                {
                    int p = r.Catalog.IndexOf(id);
                    sb.Append($" {id}@{(r.IsDone(c, p) ? (r.CompletedTick(c, p) / 600.0).ToString("F1", CultureInfo.InvariantCulture) : "-")}");
                }
                Console.WriteLine(sb.ToString());
            }
        Territory terr = pol.Territory;
        for (int a = 0; a < civs; a++)
            for (int b = 0; b < civs; b++)
                if (a != b && Touching(terr, a, b)) result.Touch[a, b] = 1;
        result.Secessions = pol.Secessions;
        result.Captures = pol.CaptureCount;
        result.Raids = pol.RaidCount;
        return result;
    }

    internal static int PreMortalityAgeOfSurvivor(int postSeasonAge)
        => postSeasonAge <= 0 ? -1 : Math.Min(99, postSeasonAge - 1);

    private static void Sample(At at, int civ, double minute, MatchModel match)
    {
        LivingEconomyModel eco = match.Economy;
        at.Minute = minute;
        double foodSum = 0, sustSum = 0;
        int n = 0;
        int priests = 0, scribes = 0, cities = 0;
        for (int s = 0; s < eco.SettlementCount; s++)
        {
            var lite = eco.SettlementLite(s);
            if (lite.Civilization != civ || match.Politics.IsFreeFolk(s)) continue;
            int pop = eco.Population(lite.Id);
            if (pop == 0) continue;
            n++;
            at.People += pop;
            at.Largest = Math.Max(at.Largest, pop);
            SettlementView v = eco.Settlement(s);
            foodSum += v.FoodCap.ToDoubleForDisplay();
            sustSum += eco.SustainableFoodCapacity(s).ToDoubleForDisplay();
            CivicReadout c = eco.Civic(s);
            if (c.Tier >= SettlementTier.Town) at.Towns++;
            if (c.Tier == SettlementTier.City) { at.Cities++; cities++; }
            priests += c.Priests; scribes += c.Scribes;
            if (Environment.GetEnvironmentVariable("CIVSIM_TRACE_E3") == "1")
                Console.WriteLine($"[m{minute:F1} civ{civ}] {eco.DescribeCivic(s)}");
            at.Fissions += lite.Fissions;
        }
        at.Settlements = n;
        at.FoodCap = n == 0 ? 0 : foodSum / n;
        at.Sustainable = n == 0 ? 0 : sustSum / n;
        Territory t = match.Politics.Territory;
        at.Claimed = t.ClaimedBasisPoints() / 10000.0;
        at.OwnShare = t.ShareBasisPoints(civ) / 10000.0;
        ResearchSystem r = match.Research;
        at.Insight = r.InsightPerYear(civ).ToDoubleForDisplay();
        // E3 roles: (Priests 16 (+4 NUM-06) + Scribes 24 (×1.5 COM-06)) × 1.1 with SOC-08, + 8 a City, × InsightCalibration and the Era Boon.
        bool num06 = r.IsDone(civ, r.Catalog.IndexOf("NUM-06")), com06 = r.IsDone(civ, r.Catalog.IndexOf("COM-06")), soc08 = r.IsDone(civ, r.Catalog.IndexOf("SOC-08"));
        double raw = (priests * (16 + (num06 ? 4 : 0)) + scribes * 24 * (com06 ? 1.5 : 1.0)) * (soc08 ? 1.1 : 1.0) + 8 * cities;
        at.RolesShare = at.Insight <= 0 ? 0 : raw * 1.6 * r.ForecastEraBoon(civ).ToDoubleForDisplay() / at.Insight;
    }

    private static bool Touching(Territory t, int a, int b)
    {
        int w = t.Width, h = t.Height;
        Span<int> n = stackalloc int[6];
        for (int tile = 0; tile < t.TileCount; tile++)
        {
            if (t.OwnerOf(tile) != a) continue;
            int count = CivSim.Sim.World.HexGrid.Neighbors(tile, w, h, n);
            for (int k = 0; k < count; k++) if (t.OwnerOf(n[k]) == b) return true;
        }
        return false;
    }

    private static double E45(double[] deaths, double[] exposure)
    {
        double survival = 1, years = 0;
        for (int k = 0; k < 12; k++)
        {
            double m = exposure[k] > 0 ? deaths[k] / exposure[k] : 1.0;
            if (k == 11) { years += survival / Math.Max(1e-6, m); break; }
            double q = 1 - Math.Exp(-5 * m);
            years += survival * 5 * (1 - q / 2);
            survival *= 1 - q;
        }
        return years;
    }

    private static double Mean(List<int> v) { double s = 0; foreach (int x in v) s += x; return s / v.Count; }
    private static string Pct(List<int> sorted, double p) => sorted.Count == 0 ? "-" : sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * p))].ToString(CultureInfo.InvariantCulture);
    private static double MeanOf(List<double> v) { if (v.Count == 0) return double.NaN; double s = 0; foreach (double x in v) s += x; return s / v.Count; }
    private static double Min(List<double> v) { double m = double.MaxValue; foreach (double x in v) m = Math.Min(m, x); return v.Count == 0 ? double.NaN : m; }
    private static double Max(List<double> v) { double m = double.MinValue; foreach (double x in v) m = Math.Max(m, x); return v.Count == 0 ? double.NaN : m; }
    private static double Median(List<double> values)
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
