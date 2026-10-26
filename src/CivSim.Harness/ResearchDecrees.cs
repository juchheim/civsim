using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>
/// <c>research --report-decrees</c> (WP8a; docs/MVP/22 §15 T11, T14–T16, T20, T26, T27): four Balanced civilizations on the Council,
/// whose policy issues the Decrees (<see cref="CouncilDecreePolicy"/>). Per civilization at its KEY-4: settlements, claimed land,
/// borders, tin access, Mandate earned and Decrees issued in its Bronze Age, seasons at the cap, foundings by Decree; per match: Tin
/// Folk and compacts. It reports; it never tunes.
/// </summary>
internal static class ResearchDecrees
{
    private static readonly ulong[] DefaultSeeds = { 20261019UL, 777UL };
    // B21 acceptance uses a 24-match sample with deterministic consecutive seeds. The existing two-seed
    // report remains a quick diagnostic; 64 minutes allows the Council time to establish Tin Folk compacts.
    internal const int B21AcceptanceMatches = 24;
    private static readonly ulong[] B21Seeds = MakeB21Seeds();

    private static ulong[] MakeB21Seeds()
    {
        var seeds = new ulong[B21AcceptanceMatches];
        for (int i = 0; i < seeds.Length; i++) seeds[i] = 20261019UL + (ulong)i;
        return seeds;
    }

    private sealed class CivTrace
    {
        public double K3 = double.NaN, K4 = double.NaN;
        public int Settlements, E3Foundings, DecreeFounds, MandateEarned, Decrees, SeasonsUnlocked, SeasonsAtCap;
        public int EarnedAtK3;
        public int[] ByKind = new int[7];
        public double Claimed, Own;
        public bool Tin, Bronze, TinByCompact, TinByFolk;
        public bool Done;
    }

    private sealed class Result
    {
        public ulong Seed;
        public CivTrace[] Civs = Array.Empty<CivTrace>();
        public bool[] Touch = new bool[4];
        public int TinFolk, Compacts, FolkCompacts, Deliveries, CaravansRaided, Pacts, Expeditions;
        public int[] Refusals = new int[16];
        public double FinalMinute;
        public string Log = "";
    }

    public static int Execute(ContentPack pack, IReadOnlyList<ulong>? seeds, int minutes, bool acceptance = false)
    {
        IReadOnlyList<ulong> list = seeds is { Count: > 0 } ? seeds : acceptance ? B21Seeds : DefaultSeeds;
        var results = new Result[list.Count];
        Parallel.For(0, list.Count, i => results[i] = Play(list[i], pack, minutes));
        var text = new StringBuilder();
        text.AppendLine($"Decree report: {list.Count} seed(s), up to {minutes} minutes, four Balanced civilizations on the Council (Council Decree policy)");
        var setts = new List<double>(); var claimed = new List<double>(); var share = new List<double>(); var earned = new List<double>();
        var decrees = new List<double>(); var atCap = new List<double>(); var k4 = new List<double>(); var e3 = new List<double>();
        int tin = 0, bronze = 0, civs = 0, touch = 0, borderSamples = 0, folkMatches = 0;
        foreach (Result r in results)
        {
            text.AppendLine($"seed {r.Seed} (ended minute {r.FinalMinute:F1}): Tin Folk {r.TinFolk}, compacts {r.Compacts} ({r.FolkCompacts} with Tin Folk), deliveries {r.Deliveries}, caravans raided {r.CaravansRaided}, pacts {r.Pacts}");
            text.AppendLine($"  refusals by reason: NoMandate {r.Refusals[4]}, NoDonor {r.Refusals[5]}, NoSite {r.Refusals[6]}, OutOfReach {r.Refusals[7]}, Rejected {r.Refusals[8]}, invalid {r.Refusals[1]}");
            text.AppendLine($"  {"civ",-4}{"KEY-3",7}{"KEY-4",7}{"E3",6}{"setl",6}{"clm%",6}{"own%",6}{"found",7}{"byDec",6}{"earned",7}{"decr",6}{"capS",6}{"unlS",6}  Found/Exp/Mine/Levy/Corv/Comp  tin  bronze");
            for (int c = 0; c < r.Civs.Length; c++)
            {
                CivTrace t = r.Civs[c];
                text.AppendLine($"  {c,-4}{F(t.K3),7}{F(t.K4),7}{F(t.K4 - t.K3),6}{t.Settlements,6}{t.Claimed * 100,6:F1}{t.Own * 100,6:F1}{t.E3Foundings,7}{t.DecreeFounds,6}" +
                                $"{t.MandateEarned,7}{t.Decrees,6}{t.SeasonsAtCap,6}{t.SeasonsUnlocked,6}  {t.ByKind[1]}/{t.ByKind[2]}/{t.ByKind[3]}/{t.ByKind[4]}/{t.ByKind[5]}/{t.ByKind[6]}" +
                                $"  {(t.Tin ? (t.TinByFolk ? "folk" : t.TinByCompact ? "compact" : "field") : "no"),-7} {(t.Bronze ? "yes" : "no")}");
                if (!t.Done) continue;
                civs++;
                k4.Add(t.K4); e3.Add(t.K4 - t.K3);
                setts.Add(t.Settlements); claimed.Add(t.Claimed); earned.Add(t.MandateEarned); decrees.Add(t.Decrees);
                if (t.SeasonsUnlocked > 0) atCap.Add((double)t.SeasonsAtCap / t.SeasonsUnlocked);
                if (t.E3Foundings > 0) share.Add((double)t.DecreeFounds / t.E3Foundings);
                if (t.Tin) tin++;
                if (t.Bronze) bronze++;
                borderSamples++;
                if (r.Touch[c]) touch++;
            }
            if (r.FolkCompacts > 0) folkMatches++;
            text.Append(r.Log);
        }
        text.AppendLine();
        text.AppendLine($"Timing: KEY-4 median {F(Median(k4))} (current projection 61.25; fresh acceptance pending), E3 duration median {F(Median(e3))} (current projection 19.75)");
        text.AppendLine($"T11 settlements per civ at KEY-4: median {F(Median(setts))} (historical target 7, 5-10; rebaseline at current cadence)");
        text.AppendLine($"T14 claimed land at KEY-4: median {P(Median(claimed))} (historical target 30-42%; rebaseline at current cadence)");
        text.AppendLine($"T15 paired borders touching at each civ's own KEY-4: {touch}/{borderSamples} civs (target >= 75%)");
        text.AppendLine($"T16 E3 foundings by Decree: median share {P(Median(share))} over {share.Count} civs (historical target 25-50%; review with the longer Mandate window)");
        text.AppendLine($"T20 civs with tin by KEY-4: {tin}/{civs} (historical target >= 50%); with some bronze (tin or arsenic): {bronze}/{civs} (>= 90%; rebaseline at current cadence)");
        int folk = 0; foreach (Result r in results) folk += r.TinFolk;
        bool tinFolkCountPass = results.All(r => r.TinFolk is >= 3 and <= 6);
        bool folkCompactPass = folkMatches * 2 >= results.Length;
        bool fullSample = acceptance && list.Count >= B21AcceptanceMatches && minutes >= 64;
        string gateStatus = acceptance
            ? $"{(fullSample ? tinFolkCountPass ? "PASS" : "FAIL" : "DIAGNOSTIC ONLY")} (sample {list.Count}/{B21AcceptanceMatches} matches, horizon {minutes}/64 min)"
            : "report only";
        string compactStatus = acceptance
            ? $"{(fullSample ? folkCompactPass ? "PASS" : "FAIL" : "DIAGNOSTIC ONLY")} (sample {list.Count}/{B21AcceptanceMatches} matches, horizon {minutes}/64 min)"
            : "report only";
        text.AppendLine($"T26 Tin Folk per map: {string.Join(", ", Array.ConvertAll(results, r => r.TinFolk))} (target 3-6); {gateStatus}");
        text.AppendLine($"T26 matches with a Tin Folk compact {folkMatches}/{results.Length} (target >= 50%); {compactStatus}");
        text.AppendLine($"T27 Mandate earned per E3 median {F(Median(earned))} (historical 7-9); Decrees median {F(Median(decrees))} (historical 5-8); current budget needs review for a ~20-minute E3");
        Console.Write(text.ToString());
        return acceptance && fullSample && (!tinFolkCountPass || !folkCompactPass) ? 1 : 0;
    }

    private static Result Play(ulong seed, ContentPack pack, int minutes)
    {
        var archetypes = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        MatchModel match = MatchModel.Create(seed, pack, archetypes);
        var runner = new SimRunner(match);
        ResearchSystem r = match.Research;
        PoliticsSystem pol = match.Politics;
        LivingEconomyModel eco = match.Economy;
        int civs = r.CivCount;
        var result = new Result { Seed = seed, Civs = new CivTrace[civs] };
        for (int c = 0; c < civs; c++) result.Civs[c] = new CivTrace();
        int key3 = r.Catalog.IndexOf("KEY-3"), key4 = r.Catalog.IndexOf("KEY-4");
        long limit = minutes * 600L;
        int cursor = 0;
        var log = new StringBuilder();
        bool verbose = Environment.GetEnvironmentVariable("CIVSIM_TRACE_DECREES") == "1";
        while (runner.Tick < limit)
        {
            runner.Advance(1);
            if (runner.Tick % ResearchSystem.SeasonTicks != 0) continue;
            IReadOnlyList<PoliticsEvent> events = pol.Events;
            for (; cursor < events.Count; cursor++)
            {
                PoliticsEvent e = events[cursor];
                if (e.Kind == PoliticsEventKind.DecreeRefused && e.B < result.Refusals.Length) result.Refusals[e.B]++;
                if (e.Kind == PoliticsEventKind.CaravanRaided && e.B >= 0 && e.Other >= 0) result.CaravansRaided++;
                if (verbose && e.Kind is PoliticsEventKind.DecreeResolved or PoliticsEventKind.DecreeRefused or PoliticsEventKind.CompactSigned
                        or PoliticsEventKind.CompactEnded or PoliticsEventKind.TinFolkAppeared or PoliticsEventKind.ExpeditionReturned)
                    log.AppendLine($"    m{runner.Tick / 600.0:F1} civ{e.Civ} {e.Kind} other {e.Other} A {e.A} B {e.B}");
            }
            for (int c = 0; c < civs; c++)
            {
                CivTrace t = result.Civs[c];
                if (double.IsNaN(t.K3) && r.IsDone(c, key3)) { t.K3 = r.CompletedTick(c, key3) / 600.0; t.EarnedAtK3 = r.MandateEarned(c); }
                if (t.Done || !r.IsDone(c, key4)) continue;
                t.Done = true;
                t.K4 = r.CompletedTick(c, key4) / 600.0;
                Snapshot(t, c, pol, eco, r);
                if (c < 4) result.Touch[c] = Touching(pol.Territory, c, c ^ 1);
            }
            bool all = true;
            foreach (CivTrace t in result.Civs) if (!t.Done) all = false;
            if (all && runner.Tick % 600 == 0) break;
        }
        for (int c = 0; c < civs; c++) if (!result.Civs[c].Done) Snapshot(result.Civs[c], c, pol, eco, r);
        result.FinalMinute = runner.Tick / 600.0;
        result.TinFolk = pol.TinFolk.Count;
        foreach (Compact c in pol.Compacts)
        {
            result.Compacts++;
            result.Deliveries += c.Deliveries;
            if (c.SellerFolk >= 0 || c.BuyerFolk >= 0) result.FolkCompacts++;
        }
        result.Pacts = pol.Pacts.Count;
        for (int c = 0; c < civs; c++) result.Expeditions += pol.DecreesDone(c, DecreeKind.Expedition);
        result.Log = log.ToString();
        return result;
    }

    /// <summary>The civilization at its KEY-4 (or at the end of the run if it never got there).</summary>
    private static void Snapshot(CivTrace t, int c, PoliticsSystem pol, LivingEconomyModel eco, ResearchSystem r)
    {
        int settlements = 0;
        for (int s = 0; s < pol.SettlementTotal; s++)
            if (pol.SettlementCivOf(s) == c && !pol.IsFreeFolk(s) && pol.SettlementPopulationOf(s) > 0) settlements++;
        t.Settlements = settlements;
        t.Claimed = pol.Territory.ClaimedBasisPoints() / 10000.0;
        t.Own = pol.Territory.ShareBasisPoints(c) / 10000.0;
        t.E3Foundings = pol.E3Foundings(c);
        t.DecreeFounds = pol.DecreesDone(c, DecreeKind.Found);
        t.MandateEarned = r.MandateEarned(c) - t.EarnedAtK3 + (r.MandateUnlocked(c) ? 1 : 0);
        t.Decrees = pol.DecreesDone(c);
        for (int k = 1; k < 7; k++) t.ByKind[k] = pol.DecreesDone(c, (DecreeKind)k);
        t.SeasonsUnlocked = pol.MandateSeasons(c);
        t.SeasonsAtCap = pol.MandateSeasonsAtCap(c);
        bool field = eco.CivHasOreSource(c, DepositKind.Tin);
        t.TinByCompact = false;
        t.TinByFolk = false;
        foreach (Compact k in pol.Compacts)
            if (k.BuyerCiv == c && k.Good == TradeGood.Tin && k.Deliveries > 0)
            {
                t.TinByCompact = true;
                if (k.SellerFolk >= 0) t.TinByFolk = true;
            }
        t.Tin = field || t.TinByCompact || eco.CivHasMetalAccess(c, MetalAccessKind.Tin);
        t.Bronze = t.Tin || eco.CivHasMetalAccess(c, MetalAccessKind.Arsenic);
    }

    /// <summary>Borders touch for a civ and its paired start at that civ's own KEY-4.</summary>
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
