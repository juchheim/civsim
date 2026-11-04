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
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>
/// <c>iron-match</c>: one whole Iron-chapter match with four Warlord AI civilizations, played to the chapter's end, with a report of what the Iron systems actually did
/// (docs/MVP/25 §13). It is for reading, not a gate: it prints when each civilization reached KEY-4 and KEY-5, how the chapter ended, and for each civilization the iron
/// kits, works, staff, Wonders, wars and the events that the new systems raised.
/// <code>civsim-harness iron-match --pack FILE [--seed N] [--seeds N] [--minutes N]</code>
/// </summary>
internal static class IronMatchCommand
{
    private static readonly EconomyArchetype[] Archetypes = { EconomyArchetype.Builder, EconomyArchetype.Grower, EconomyArchetype.Warlord, EconomyArchetype.Sage };

    public static int Run(string[] args)
    {
        string? packPath = null;
        ulong first = 20261019;
        int seeds = 1, minutes = 110, capYears = ChapterRules.IronCapYear, countdown = ChapterRules.IronCountdownSeasons;
        bool grantE4 = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pack": packPath = args[++i]; break;
                case "--seed": first = ulong.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--seeds": seeds = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--minutes": minutes = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--cap-years": capYears = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--countdown": countdown = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--grant-e4": grantE4 = true; break;
                default:
                    Console.Error.WriteLine("usage: civsim-harness iron-match --pack FILE [--seed N] [--seeds N] [--minutes N] [--cap-years N] [--countdown SEASONS] [--grant-e4]");
                    return 2;
            }
        }
        if (packPath is null) throw new ArgumentException("iron-match needs --pack FILE.");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        for (int n = 0; n < seeds; n++) Console.Write(Play(first + (ulong)n, pack, minutes, capYears, countdown, grantE4));
        return 0;
    }

    private static string Play(ulong seed, ContentPack pack, int minutes, int capYears, int countdown, bool grantE4)
    {
        GeneratedWorld world = WorldGenerator.Generate(seed);
        var controls = new CivControl[Archetypes.Length];
        for (int c = 0; c < controls.Length; c++) controls[c] = CivControl.Ai(Difficulty.Warlord, Archetypes[c]);
        MatchModel match = MatchModel.Create(seed, pack, world, Archetypes, controls, rules: capYears == ChapterRules.IronCapYear && countdown == ChapterRules.IronCountdownSeasons ? ChapterRules.Iron : ChapterRules.IronWithLimits(capYears, countdown));
        var runner = new SimRunner(match);
        int civs = Archetypes.Length;
        ResearchSystem research = match.Research;
        int key4 = research.Catalog.IndexOf("KEY-4"), key5 = research.Catalog.IndexOf("KEY-5");
        var granted = new bool[civs];
        var idleSeasons = new int[civs];
        var peakSoldiers = new int[civs];
        long limit = minutes * 600L;
        MatchEnd end = match.EndOfMatch();
        while (runner.Tick < limit && !end.IsOver(runner.Tick))
        {
            // Diagnostic: take research out of the picture. Each civilization is given every E4 project except the keystone the moment it has KEY-4.
            if (grantE4)
                for (int c = 0; c < civs; c++)
                {
                    if (granted[c] || !research.IsDone(c, key4)) continue;
                    granted[c] = true;
                    foreach (ProjectDef p in research.Catalog.Projects)
                        if (p.Era == 4 && p.Index != key5) runner.Submit(new Command(runner.Tick, (byte)c, CommandKind.CompleteProject, p.Index, 0, 0));
                }
            runner.Advance(150);
            end = match.EndOfMatch();
            for (int c = 0; c < civs; c++)
            {
                if (match.Economy.IronIdleInstitutions(c) > 0) idleSeasons[c]++;
                int soldiers = 0;
                for (int s = 0; s < match.Economy.SettlementCount; s++) if (match.Economy.SettlementLite(s).Civilization == c) soldiers += match.Economy.SoldiersAt(s);
                peakSoldiers[c] = Math.Max(peakSoldiers[c], soldiers);
            }
        }

        var o = new StringBuilder();
        void L(string text = "") => o.Append(text).Append('\n');
        string Min(long tick) => tick < 0 ? "-" : (tick / 600.0).ToString("F1", CultureInfo.InvariantCulture);
        L($"== iron-match seed {seed}: {runner.Tick / 600.0:F1} minutes simulated, chapter end {end.Reason} at season {end.EndSeason} (ended: {end.IsOver(runner.Tick)})");
        L($"{"civ",-4}{"arch",-9}{"KEY-4",7}{"KEY-5",7}{"E4 done",8}{"settl",7}{"people",8}{"T11",6}{"T12",6}{"adults",8}{"soldiers",9}{"scholars",9}{"missions",9}{"idleS",7}");
        for (int c = 0; c < civs; c++)
        {
            int settlements = 0, people = 0, scholars = 0, missionaries = 0, soldiers = 0;
            for (int s = 0; s < match.Economy.SettlementCount; s++)
            {
                if (match.Economy.SettlementLite(s).Civilization != c || match.Politics.IsFreeFolk(s)) continue;
                settlements++; people += match.Economy.Population(match.Economy.SettlementLite(s).Id);
                scholars += match.Economy.ScholarsAt(s); missionaries += match.Economy.Missionaries(s); soldiers += match.Economy.SoldiersAt(s);
            }
            (int t11, int t12, int adults) = match.Economy.IronKitHolders(c);
            int e4 = research.Catalog.Projects.Count(p => p.Era == 4 && research.IsDone(c, p.Index));
            long k4 = research.IsDone(c, key4) ? research.CompletedTick(c, key4) : -1, k5 = research.IsDone(c, key5) ? research.CompletedTick(c, key5) : -1;
            L($"c{c,-3}{Archetypes[c],-9}{Min(k4),7}{Min(k5),7}{e4,8}{settlements,7}{people,8}{t11,6}{t12,6}{adults,8}{soldiers,9}{scholars,9}{missionaries,9}{idleSeasons[c],7}");
        }

        // Iron works by kind, per civilization.
        L("iron works built (all settlements):");
        for (int k = 0; k < LivingEconomyModel.IronWorkKindCount; k++)
        {
            var row = new int[civs];
            for (int s = 0; s < match.Economy.SettlementCount; s++)
            {
                int civ = match.Economy.SettlementLite(s).Civilization;
                if ((uint)civ < (uint)civs) row[civ] += match.Economy.IronWorkAt(s, k);
            }
            if (row.Sum() > 0) L($"  {LivingEconomyModel.IronWorkId(k),-24}{string.Join("  ", row.Select(v => v.ToString().PadLeft(3)))}");
        }
        L("iron build in progress (town count by structure, with the share of its labor done):");
        for (int c = 0; c < civs; c++)
        {
            var byKind = new SortedDictionary<string, (int Towns, int Permille)>();
            int villages = 0;
            for (int s = 0; s < match.Economy.SettlementCount; s++)
            {
                if (match.Economy.SettlementLite(s).Civilization != c || match.Politics.IsFreeFolk(s)) continue;
                if (match.Economy.Tier(s) == SettlementTier.Village) { villages++; continue; }
                (int kind, int permille) = match.Economy.IronBuildAt(s);
                string name = kind < 0 ? "(nothing wanted)" : LivingEconomyModel.IronWorkId(kind);
                byKind.TryGetValue(name, out (int Towns, int Permille) cur);
                byKind[name] = (cur.Towns + 1, cur.Permille + permille);
            }
            L($"  c{c}: villages {villages}; " + string.Join(", ", byKind.Select(kv => $"{kv.Key} x{kv.Value.Towns} ({kv.Value.Permille / Math.Max(1, kv.Value.Towns) / 10}%)")));
        }
        L("what the stuck towns lack (settlements whose structure is fully worked):");
        var lacks = new SortedDictionary<string, int>();
        for (int s = 0; s < match.Economy.SettlementCount; s++)
        {
            (int kind, int permille) = match.Economy.IronBuildAt(s);
            if (kind < 0 || permille < 1000) continue;
            string shortage = match.Economy.IronBuildShortages(s);
            foreach (string part in shortage.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where((_, i) => i % 2 == 0))
            {
                string key = $"{LivingEconomyModel.IronWorkId(kind)} lacks {part}";
                lacks[key] = lacks.GetValueOrDefault(key) + 1;
            }
            if (shortage.Length == 0) lacks[$"{LivingEconomyModel.IronWorkId(kind)} fully supplied"] = lacks.GetValueOrDefault($"{LivingEconomyModel.IronWorkId(kind)} fully supplied") + 1;
        }
        foreach (var kv in lacks) L($"  {kv.Key}: {kv.Value} towns");
        L("wonders held: " + string.Join(", ", new[] { "great_library", "great_lighthouse", "great_dome" }.Select(w =>
        {
            for (int c = 0; c < civs; c++) if (match.Economy.HoldsWonder(c, w)) return $"{w} c{c}";
            return $"{w} none";
        })));

        // What the politics events say the new systems did.
        var counts = match.Politics.Events.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.Count());
        int Count(PoliticsEventKind k) => counts.GetValueOrDefault(k);
        L($"events: wars {Count(PoliticsEventKind.WarDeclared)}  raids {Count(PoliticsEventKind.RaidResolved)}  assaults {Count(PoliticsEventKind.AssaultResolved)}  captured {Count(PoliticsEventKind.Captured)}  " +
          $"investments {Count(PoliticsEventKind.InvestmentBegan)}  blockades {Count(PoliticsEventKind.BlockadeBegan)}/{Count(PoliticsEventKind.BlockadeEnded)}  " +
          $"tributes {Count(PoliticsEventKind.TributeAgreed)}/{Count(PoliticsEventKind.TributeEnded)}  leagues {Count(PoliticsEventKind.LeagueJoined)}/{Count(PoliticsEventKind.LeagueEnded)}  compacts {Count(PoliticsEventKind.CompactSigned)}");
        L($"now: tributes {match.Politics.Tributes.Count}, leagues {match.Politics.Leagues.Count}, blockades {match.Politics.Blockades.Count}, investments {match.Politics.Investments.Count} " +
          $"(with engines {match.Politics.Investments.Count(i => i.Engines > 0)})");
        L($"peak soldiers per civ: {string.Join(", ", peakSoldiers.Select((v, c) => $"c{c} {v}"))}");
        L($"final legacy per civ: {string.Join(", ", Enumerable.Range(0, civs).Select(c => $"c{c} {(research.Legacy(c) + match.Politics.Territory.Dominion(c)).ToDoubleForDisplay():F0}"))}");
        L();
        return o.ToString();
    }
}
