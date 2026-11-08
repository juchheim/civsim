using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CivSim.Sim.Ai;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>
/// <c>build-census</c>: plays whole AI matches and reads every town's <see cref="BuildOutlook"/> once every few seasons, to show what towns are waiting on and which
/// researched structures are not being built, and why (docs/MVP/30 §1). It is for reading, not a gate, and changes nothing in the match.
/// <code>civsim-harness build-census --pack FILE [--seed N] [--seeds N] [--minutes N] [--mode iron|bronze] [--sample SEASONS] [--top N]</code>
/// </summary>
internal static class BuildCensusCommand
{
    private static readonly EconomyArchetype[] Archetypes = { EconomyArchetype.Builder, EconomyArchetype.Grower, EconomyArchetype.Warlord, EconomyArchetype.Sage };

    private sealed class KindStats
    {
        public readonly Dictionary<BuildStatus, int> Status = new();
        public int Samples;
        public readonly HashSet<int> UnlockedCivs = new();
        public readonly Dictionary<int, int> UnlockSeason = new(), FirstBuiltSeason = new();
        public int TownsDoneAtEnd, TownsAtEnd;
    }

    public static int Run(string[] args)
    {
        string? packPath = null;
        ulong first = 20264001;
        int seeds = 1, minutes = 90, sample = 4, top = 25;
        bool iron = true;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pack": packPath = args[++i]; break;
                case "--seed": first = ulong.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--seeds": seeds = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--minutes": minutes = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--sample": sample = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--top": top = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--mode": iron = args[++i] != "bronze"; break;
                default:
                    Console.Error.WriteLine("usage: civsim-harness build-census --pack FILE [--seed N] [--seeds N] [--minutes N] [--mode iron|bronze] [--sample SEASONS] [--top N]");
                    return 2;
            }
        }
        if (packPath is null) throw new ArgumentException("build-census needs --pack FILE.");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));

        var kinds = new SortedDictionary<string, KindStats>(StringComparer.Ordinal);
        var blockers = new Dictionary<string, int>(StringComparer.Ordinal);
        var shortGoods = new Dictionary<string, int>(StringComparer.Ordinal);
        var headlines = new Dictionary<BuildHeadline, int>();
        var trapped = new HashSet<(ulong, int)>();
        var townsSeen = new HashSet<(ulong, int)>();
        long fieldPickSamples = 0, trapSamples = 0, townSamples = 0, nothingWithShortSamples = 0;
        long idleBuildSamples = 0, queuedBehindCapSamples = 0, basicPickSamples = 0;

        for (int n = 0; n < seeds; n++)
        {
            ulong seed = first + (ulong)n;
            GeneratedWorld world = WorldGenerator.Generate(seed);
            var controls = new CivControl[Archetypes.Length];
            for (int c = 0; c < controls.Length; c++) controls[c] = CivControl.Ai(Difficulty.Warlord, Archetypes[c]);
            ChapterRules rules = iron ? ChapterRules.Iron : ChapterRules.Bronze;
            MatchModel match = MatchModel.Create(seed, pack, world, Archetypes, controls, rules: rules);
            var runner = new SimRunner(match);
            LivingEconomyModel eco = match.Economy;
            var lastFields = new Dictionary<int, int>();
            long limit = minutes * 600L;
            MatchEnd end = match.EndOfMatch();
            int seasonsRun = 0;
            while (runner.Tick < limit && !end.IsOver(runner.Tick))
            {
                runner.Advance(150);
                end = match.EndOfMatch();
                seasonsRun++;
                if (seasonsRun % sample != 0) continue;
                int season = (int)(runner.Tick / 150L);
                for (int s = 0; s < eco.SettlementCount; s++)
                {
                    if (match.Politics.IsFreeFolk(s) || eco.Population(eco.SettlementLite(s).Id) == 0) continue;
                    int civ = eco.SettlementLite(s).Civilization;
                    BuildOutlook o = eco.BuildOutlookFor(s);
                    townSamples++;
                    townsSeen.Add((seed, s));
                    headlines[o.Headline] = headlines.GetValueOrDefault(o.Headline) + 1;
                    bool anyCurrent = false;
                    foreach (BuildItem item in o.Items)
                    {
                        string key = $"{item.Track}/{item.Id}";
                        if (!kinds.TryGetValue(key, out KindStats? ks)) kinds[key] = ks = new KindStats();
                        ks.Samples++;
                        ks.Status[item.Status] = ks.Status.GetValueOrDefault(item.Status) + 1;
                        ks.UnlockedCivs.Add(civ);
                        int ck = civ + 100 * (int)(seed - first);
                        if (!ks.UnlockSeason.ContainsKey(ck)) ks.UnlockSeason[ck] = season;
                        if (item.Status == BuildStatus.Done && !ks.FirstBuiltSeason.ContainsKey(ck)) ks.FirstBuiltSeason[ck] = season;
                        if (item.Status == BuildStatus.Current) anyCurrent = true;
                        if (item.Status is BuildStatus.Short or BuildStatus.Blocked)
                        {
                            string why = item.Status == BuildStatus.Short ? "short" : $"{item.Gate.Cause}/{item.Gate.Why}{(item.Gate.Project is { Length: > 0 } p ? "(" + p + ")" : "")}";
                            string bk = $"{key} {item.Status} {why}";
                            blockers[bk] = blockers.GetValueOrDefault(bk) + 1;
                            if (item.Status == BuildStatus.Short)
                                for (int i = 0; i < item.Needs.Length; i++)
                                {
                                    if (!item.Needs[i].Short) continue;
                                    string gk = $"{key} lacks {(item.Needs[i].Slot < 0 ? "food" : LivingEconomyModel.StoreSlotId(item.Needs[i].Slot))} [{item.Supplies[i].Kind}{(item.Supplies[i].Project is { } sp ? " " + sp : "")}]";
                                    shortGoods[gk] = shortGoods.GetValueOrDefault(gk) + 1;
                                    break;
                                }
                        }
                    }
                    if (!anyCurrent)
                    {
                        idleBuildSamples++;
                        if (o.Headline == BuildHeadline.WaitingOnMaterials) nothingWithShortSamples++;
                    }
                    // Labor already enough for the next basic structure at the end of a season: the one-build-a-season limit, not materials, holds the rest.
                    BuildItem? basicPick = o.CurrentOf(BuildTrack.Basic);
                    if (basicPick is not null)
                    {
                        basicPickSamples++;
                        if (basicPick.Progress >= basicPick.Labor && basicPick.Labor > CivSim.Sim.Numerics.Fix64.Zero) queuedBehindCapSamples++;
                    }
                    // The field trap: the planner keeps choosing a field that the ground cannot take.
                    BuildItem? field = o.Items.FirstOrDefault(i => i.Track == BuildTrack.Basic && i.Id == "field");
                    if (field is { Status: BuildStatus.Current })
                    {
                        fieldPickSamples++;
                        int count = eco.FieldCount(s);
                        if (!eco.CanPlaceAnotherField(s) && count < eco.FieldAllowance(s))
                        {
                            trapSamples++;
                            trapped.Add((seed, s));
                        }
                    }
                    lastFields[s] = eco.FieldCount(s);
                }
            }
            // End of match: how many towns of each civilization hold each structure.
            for (int s = 0; s < eco.SettlementCount; s++)
            {
                if (match.Politics.IsFreeFolk(s) || eco.Population(eco.SettlementLite(s).Id) == 0) continue;
                BuildOutlook o = eco.BuildOutlookFor(s);
                foreach (BuildItem item in o.Items)
                {
                    KindStats ks = kinds[$"{item.Track}/{item.Id}"];
                    ks.TownsAtEnd++;
                    if (item.Status == BuildStatus.Done) ks.TownsDoneAtEnd++;
                }
            }
        }

        var o2 = new StringBuilder();
        void L(string text = "") => o2.Append(text).Append('\n');
        L($"== build-census: {seeds} match(es) from seed {first}, {minutes} min, {(iron ? "Iron" : "Bronze")} chapter, sampled every {sample} seasons; {townSamples} town samples over {townsSeen.Count} towns");
        L();
        L("Headline of each sampled town:");
        foreach (BuildHeadline h in Enum.GetValues<BuildHeadline>())
            L($"  {h,-20} {headlines.GetValueOrDefault(h),7}  {100.0 * headlines.GetValueOrDefault(h) / Math.Max(1, townSamples):F1}%");
        L($"  towns with no structure under way: {idleBuildSamples} samples ({100.0 * idleBuildSamples / Math.Max(1, townSamples):F1}%), of which waiting on a short good: {nothingWithShortSamples}");
        L();
        L("Structures each civilization has learned, by what the town-samples say (status shares), and how many towns hold one at the end:");
        L($"  {"structure",-34}{"samples",8}{"built",8}{"short",8}{"waiting",8}{"blocked",8}{"notneed",8}  {"held at end",12}  unlock->first built (seasons, median)");
        foreach ((string key, KindStats ks) in kinds)
        {
            int Pct(BuildStatus st) => (int)Math.Round(100.0 * ks.Status.GetValueOrDefault(st) / Math.Max(1, ks.Samples));
            var lags = ks.UnlockSeason.Keys.Where(k => ks.FirstBuiltSeason.ContainsKey(k)).Select(k => ks.FirstBuiltSeason[k] - ks.UnlockSeason[k]).OrderBy(x => x).ToList();
            int never = ks.UnlockSeason.Count - lags.Count;
            string lag = lags.Count == 0 ? "never built" : $"{lags[lags.Count / 2]}" + (never > 0 ? $" ({never} of {ks.UnlockSeason.Count} civs never)" : "");
            L($"  {key,-34}{ks.Samples,8}{Pct(BuildStatus.Done),7}%{Pct(BuildStatus.Short),7}%{Pct(BuildStatus.Waiting),7}%{Pct(BuildStatus.Blocked),7}%{Pct(BuildStatus.NotNeeded),7}%  {ks.TownsDoneAtEnd,5}/{ks.TownsAtEnd,-6}  {lag}");
        }
        L();
        L($"Most common stops (Short = wanted and allowed but a good is short; Blocked = a rule), top {top}:");
        foreach ((string k, int v) in blockers.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Take(top))
            L($"  {v,7}  {k}");
        L();
        L($"Most common missing goods (first short good of a Short structure) and where it would come from, top {top}:");
        foreach ((string k, int v) in shortGoods.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Take(top))
            L($"  {v,7}  {k}");
        L();
        L($"Basic pick already fully worked at the end of a season (labor was enough for one more, the one-a-season limit held it): {queuedBehindCapSamples} of {basicPickSamples} samples that had a pick.");
        L($"Field trap: the planner picked a field in {fieldPickSamples} samples; in {trapSamples} of them the town had fewer plots than its allowance but no free ground to lay one ({trapped.Count} of {townsSeen.Count} towns ever).");
        Console.Write(o2.ToString());
        return 0;
    }
}
