using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>
/// Plays whole Discovery-chapter matches with the stock AI (docs/MVP/38, M13 WP-E6-12): the diagnostic that answers whether a match can reach KEY-6 and KEY-7 and what the E6 systems
/// do on the way. Nothing here changes the simulation. <c>--grant-e5</c> grants a civilization every E5 project, KEY-6 included, when it completes KEY-5 (E5 is still draft, and the stock AI researches only numeric rows; unlike fief-match, KEY-6 does not end this chapter);
/// <c>--grant-e6</c> additionally grants every E6 project except KEY-7 when KEY-6 is done (the E6 rows are numeric since WP-E6-12, so without it the AI researches them itself). Both are draft-row diagnostics; <c>--grant-e6</c> implies <c>--grant-e5</c>.
/// </summary>
internal static class DiscoveryMatchCommand
{
    private static readonly EconomyArchetype[] Archetypes = { EconomyArchetype.Builder, EconomyArchetype.Grower, EconomyArchetype.Warlord, EconomyArchetype.Sage };
    private static readonly string[] Structures =
        { "atmospheric_engine", "coke_oven", "navigation_canal", "galleon", "factory", "exchange_house", "bastion_fort", "harbor", "castle", "concentric_castle", "hospital", "botanical_garden" };

    public static int Run(string[] args)
    {
        string? packPath = null;
        ulong first = 20261019;
        int seeds = 1, minutes = 110;
        bool grantE5 = false, grantE6 = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pack": packPath = args[++i]; break;
                case "--seed": first = ulong.Parse(args[++i]); break;
                case "--seeds": seeds = int.Parse(args[++i]); break;
                case "--minutes": minutes = int.Parse(args[++i]); break;
                case "--grant-e5": grantE5 = true; break;
                case "--grant-e6": grantE6 = true; break;
                default: Console.Error.WriteLine($"Unknown option '{args[i]}'."); return 2;
            }
        }
        if (packPath is null) { Console.Error.WriteLine("discovery-match needs --pack FILE."); return 2; }
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        for (int n = 0; n < seeds; n++) Play(first + (ulong)n, pack, minutes, grantE5, grantE6);
        return 0;
    }

    private static void Play(ulong seed, ContentPack pack, int minutes, bool grantE5, bool grantE6)
    {
        GeneratedWorld world = WorldGenerator.Generate(seed);
        var controls = new CivControl[Archetypes.Length];
        for (int c = 0; c < controls.Length; c++) controls[c] = CivControl.Ai(Difficulty.Warlord, Archetypes[c]);
        MatchModel match = MatchModel.Create(seed, pack, world, Archetypes, controls, rules: ChapterRules.Discovery);
        match.Research.DiagnosticDraftProjectGrantsEnabled = grantE5 || grantE6;
        var runner = new SimRunner(match);
        ResearchSystem research = match.Research;
        int civs = Archetypes.Length;
        int key5 = research.Catalog.IndexOf("KEY-5"), key6 = research.Catalog.IndexOf("KEY-6"), key7 = research.Catalog.IndexOf("KEY-7");
        var grantedE5 = new bool[civs];
        var grantedE6 = new bool[civs];
        var seasonOf = new int[civs, 3];
        for (int c = 0; c < civs; c++) for (int k = 0; k < 3; k++) seasonOf[c, k] = -1;
        int[] keys = { key5, key6, key7 };
        long limit = minutes * 600L;
        MatchEnd end = match.EndOfMatch();
        while (runner.Tick < limit && !end.IsOver(runner.Tick))
        {
            if (grantE5 || grantE6)
                for (int c = 0; c < civs; c++)
                {
                    if (grantedE5[c] || !research.IsDone(c, key5)) continue;
                    grantedE5[c] = true;
                    foreach (ProjectDef p in research.Catalog.Projects)
                        if (p.Era == 5) runner.Submit(new Command(runner.Tick, (byte)c, CommandKind.CompleteProject, p.Index, 0, 0));
                }
            if (grantE6)
                for (int c = 0; c < civs; c++)
                {
                    if (grantedE6[c] || !research.IsDone(c, key6)) continue;
                    grantedE6[c] = true;
                    foreach (ProjectDef p in research.Catalog.Projects)
                        if (p.Era == 6 && p.Index != key7) runner.Submit(new Command(runner.Tick, (byte)c, CommandKind.CompleteProject, p.Index, 0, 0));
                }
            runner.Advance(150);
            int season = checked((int)(runner.Tick / 150L));
            for (int c = 0; c < civs; c++)
                for (int k = 0; k < 3; k++)
                    if (seasonOf[c, k] < 0 && keys[k] >= 0 && research.IsDone(c, keys[k])) seasonOf[c, k] = season;
            end = match.EndOfMatch();
        }
        Console.WriteLine($"seed={seed} ticks={runner.Tick} over={end.IsOver(runner.Tick)} reason={end.Reason} winner={end.Civ} final={match.ComputeChecksum():x16}");
        for (int c = 0; c < civs; c++)
            Console.WriteLine($"  civ{c} {Archetypes[c]}: KEY-5 s{seasonOf[c, 0]} KEY-6 s{seasonOf[c, 1]} KEY-7 s{seasonOf[c, 2]} projects={research.Catalog.Projects.Count(p => research.IsDone(c, p.Index))} era={research.Era(c)} insight/yr={research.InsightPerYear(c)} KEY-6 progress={research.Progress(c, key6)} of {research.EffectiveCost(c, key6)}");
        var kinds = Structures.Select(name => Array.FindIndex(Enumerable.Range(0, LivingEconomyModel.IronWorkKindCount).ToArray(), k => LivingEconomyModel.IronWorkId(k) == name)).ToArray();
        var totals = new int[Structures.Length];
        var perCiv = new int[civs, Structures.Length];
        int settlements = match.Economy.SettlementCount, coalSettlements = 0;
        for (int s = 0; s < settlements; s++)
        {
            int civ = match.Economy.SettlementLite(s).Civilization;
            for (int j = 0; j < Structures.Length; j++)
            {
                if (kinds[j] < 0) continue;
                int count = match.Economy.IronWorkAt(s, kinds[j]);
                totals[j] += count;
                if ((uint)civ < (uint)civs) perCiv[civ, j] += count;
            }
            if (match.Economy.CoalLitres(s) > Fix64.Zero) coalSettlements++;
        }
        Console.WriteLine("  built: " + string.Join(", ", Structures.Select((name, j) => $"{name}={totals[j]}")) + $"; settlements={settlements}, holding coal={coalSettlements}, colonies={match.Economy.ColonyCount()}");
    }
}
