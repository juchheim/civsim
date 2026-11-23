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
        { "atmospheric_engine", "coke_oven", "navigation_canal", "galleon", "factory", "exchange_house", "bastion_fort", "harbor", "castle", "concentric_castle", "hospital", "botanical_garden", "learned_society", "renaissance_palace", "academy", "university" };

    public static int Run(string[] args)
    {
        string? packPath = null;
        ulong first = 20261019;
        int seeds = 1, minutes = 110;
        bool grantE5 = false, grantE6 = false;
        int resumeAt = 0;
        ChapterRules rules = ChapterRules.Discovery;
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
                case "--mode": rules = args[++i] == "fief" ? ChapterRules.Fief : ChapterRules.Discovery; break;
                case "--resume-at": resumeAt = int.Parse(args[++i]); break;
                default: Console.Error.WriteLine($"Unknown option '{args[i]}'."); return 2;
            }
        }
        if (packPath is null) { Console.Error.WriteLine("discovery-match needs --pack FILE."); return 2; }
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        for (int n = 0; n < seeds; n++) Play(first + (ulong)n, pack, minutes, grantE5, grantE6, resumeAt, rules);
        return 0;
    }

    /// <summary>Diagnostic: the first tiles whose forage stock differs, with the per-tile state that regrowth reads (read through reflection; nothing is written).</summary>
    private static void TileDiff(LivingEconomyModel a, LivingEconomyModel b)
    {
        const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        object? Get(LivingEconomyModel m, string name) => typeof(LivingEconomyModel).GetField(name, F)?.GetValue(m);
        var fa = (Fix64[])Get(a, "_forageStock")!; var fb = (Fix64[])Get(b, "_forageStock")!;
        int shown = 0, total = 0;
        for (int t = 0; t < fa.Length; t++)
        {
            if (fa[t] == fb[t]) continue;
            total++;
            if (shown++ >= 6) continue;
            string Cell(string name) => Get(a, name) is Array xa && Get(b, name) is Array xb && t < xa.Length ? $"{name}={xa.GetValue(t)}|{xb.GetValue(t)}" : $"{name}=?";
            Console.WriteLine($"    tile {t}: forage {fa[t].Raw} vs {fb[t].Raw} (diff {fa[t].Raw - fb[t].Raw}); {Cell("_fieldOwner")} {Cell("_wildfireSeasonsRemaining")} {Cell("_forestState")} {Cell("_forestLowSeasons")}");
        }
        Console.WriteLine($"    forage tiles differing: {total}");
        for (int t = 0; t < fa.Length; t++)
        {
            if (fa[t] == fb[t]) continue;
            object? Call(LivingEconomyModel m, string name, params object[] args) => typeof(LivingEconomyModel).GetMethod(name, F)!.Invoke(m, args);
            int ownerA = (int)Call(a, "FirstSettlementForageOwner", t)!, ownerB = (int)Call(b, "FirstSettlementForageOwner", t)!;
            Console.WriteLine($"    tile {t}: owner {ownerA} vs {ownerB}");
            foreach (var m in new[] { a, b })
            {
                int owner = (int)Call(m, "FirstSettlementForageOwner", t)!;
                if (owner < 0) continue;
                var settlements = (System.Collections.IList)Get(m, "_settlements")!;
                int civ = (int)(settlements[owner]!.GetType().GetField("Civilization")?.GetValue(settlements[owner]) ?? settlements[owner]!.GetType().GetProperty("Civilization")!.GetValue(settlements[owner])!);
                Console.WriteLine($"      civ {civ}: winter_forage_stock {Call(m, "Modifier", civ, "winter_forage_stock")}, autumn_forage {Call(m, "Modifier", civ, "autumn_forage")}, winterMult {Get(m, "_winterForageMultiplier")}, seasons {Get(m, "_seasonsCompleted")}");
            }
            break;
        }
    }

    /// <summary>Diagnostic: lists the fields (by path) that differ between two economies, comparing live objects with reflection; derived caches show up too, so read the names.</summary>
    private static void DeepDiff(object? a, object? b, string path, int depth, List<string> found, HashSet<object> seen)
    {
        if (found.Count >= 4000 || depth > 6) return;
        if (a is null || b is null) return; // a lazily built cache on one side only: not state
        Type t = a.GetType();
        if (t != b.GetType()) { found.Add($"{path}: type"); return; }
        if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal)) { if (!a.Equals(b)) found.Add($"{path}: {a} vs {b}"); return; }
        if (a is Array xa)
        {
            var xb = (Array)b;
            if (xa.Rank != 1)
            {
                if (xa.Length != xb.Length) { found.Add($"{path}: length {xa.Length} vs {xb.Length}"); return; }
                var ea = xa.GetEnumerator(); var eb = xb.GetEnumerator(); int n = 0;
                while (ea.MoveNext() && eb.MoveNext()) { if (!Equals(ea.Current, eb.Current)) { found.Add($"{path}[flat {n}]: {ea.Current} vs {eb.Current}"); break; } n++; }
                return;
            }
            if (xa.Length != xb.Length) { found.Add($"{path}: length {xa.Length} vs {xb.Length}"); return; }
            if (t.GetElementType()!.IsPrimitive) { for (int i = 0; i < xa.Length; i++) if (!Equals(xa.GetValue(i), xb.GetValue(i))) { found.Add($"{path}[{i}]: {xa.GetValue(i)} vs {xb.GetValue(i)}"); break; } return; }
            for (int i = 0; i < xa.Length && found.Count < 4000; i++) DeepDiff(xa.GetValue(i), xb.GetValue(i), $"{path}[{i}]", depth + 1, found, seen);
            return;
        }
        if (a is System.Collections.IList la)
        {
            var lb = (System.Collections.IList)b;
            if (la.Count != lb.Count) { found.Add($"{path}: count {la.Count} vs {lb.Count}"); return; }
            for (int i = 0; i < la.Count && found.Count < 4000; i++) DeepDiff(la[i], lb[i], $"{path}[{i}]", depth + 1, found, seen);
            return;
        }
        if (t.Namespace is null || !t.Namespace.StartsWith("CivSim", StringComparison.Ordinal)) return;
        if (!t.IsValueType && !t.Namespace.StartsWith("CivSim.Sim.Economy", StringComparison.Ordinal)) return; // peer systems hold their own state
        if (!t.IsValueType && !seen.Add(a)) return;
        foreach (var f in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
            if (!f.FieldType.IsPointer && !typeof(Delegate).IsAssignableFrom(f.FieldType) && !(f.Name.Contains("ecipes") || f.Name is "_content" or "_world" or "_catalog" or "_pack" or "_coppiced" || f.Name.Contains("TileOwnership"))) DeepDiff(f.GetValue(a), f.GetValue(b), $"{path}.{f.Name}", depth + 1, found, seen);
    }

    private static byte[] EconomyBytes(MatchModel m)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) m.Economy.WriteSnapshot(writer);
        return stream.ToArray();
    }

    private static string Parts(MatchModel m)
    {
        var r = new StateHasher(); m.Research.Hash(ref r);
        var p = new StateHasher(); m.Politics.Hash(ref p);
        var a = new StateHasher(); m.Ai?.Hash(ref a);
        return $"[economy {m.Economy.ComputeChecksum():x8} research {r.Finish():x8} politics {p.Finish():x8} ai {a.Finish():x8}]";
    }

    private static void Play(ulong seed, ContentPack pack, int minutes, bool grantE5, bool grantE6, int resumeAt, ChapterRules rules)
    {
        GeneratedWorld world = WorldGenerator.Generate(seed);
        var controls = new CivControl[Archetypes.Length];
        for (int c = 0; c < controls.Length; c++) controls[c] = CivControl.Ai(Difficulty.Warlord, Archetypes[c]);
        MatchModel match = MatchModel.Create(seed, pack, world, Archetypes, controls, rules: rules);
        match.Research.DiagnosticDraftProjectGrantsEnabled = grantE5 || grantE6;
        var runner = new SimRunner(match);
        MatchModel? twin = null;
        SimRunner? twinRunner = null;
        MatchModel? twin2 = null;
        SimRunner? twin2Runner = null;
        long resumeTick = resumeAt * 600L;
        bool diverged = false;
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
                        if (p.Era == 5) { runner.Submit(new Command(runner.Tick, (byte)c, CommandKind.CompleteProject, p.Index, 0, 0)); twinRunner?.Submit(new Command(runner.Tick, (byte)c, CommandKind.CompleteProject, p.Index, 0, 0)); }
                }
            if (grantE6)
                for (int c = 0; c < civs; c++)
                {
                    if (grantedE6[c] || !research.IsDone(c, key6)) continue;
                    grantedE6[c] = true;
                    foreach (ProjectDef p in research.Catalog.Projects)
                        if (p.Era == 6 && p.Index != key7) { runner.Submit(new Command(runner.Tick, (byte)c, CommandKind.CompleteProject, p.Index, 0, 0)); twinRunner?.Submit(new Command(runner.Tick, (byte)c, CommandKind.CompleteProject, p.Index, 0, 0)); }
                }
            if (twinRunner is not null && !diverged)
            {
                for (int t = 0; t < 150 && !diverged; t++)
                {
                    runner.Advance(1); twinRunner.Advance(1); twin2Runner?.Advance(1);
                    if (twin!.Economy.ComputeChecksum() != match.Economy.ComputeChecksum()) { diverged = true; Console.WriteLine($"  economy diverged at tick {runner.Tick} (season offset {t})");
                        if (twin2 is not null) Console.WriteLine($"  second restore: equals first twin {twin2.Economy.ComputeChecksum() == twin.Economy.ComputeChecksum()}, equals live {twin2.Economy.ComputeChecksum() == match.Economy.ComputeChecksum()}");
                        byte[] a = EconomyBytes(match), b = EconomyBytes(twin);
                        int first = -1, count = 0;
                        for (int k = 0; k < Math.Min(a.Length, b.Length); k++) if (a[k] != b[k]) { if (first < 0) first = k; count++; }
                        Console.WriteLine($"  economy snapshot: {a.Length:N0} vs {b.Length:N0} bytes, first difference at byte {first:N0}, {count:N0} bytes differ");
                        TileDiff(match.Economy, twin.Economy);
                        int clusters = 0, startAt = -1, last = -1;
                        for (int k = 0; k <= Math.Min(a.Length, b.Length) && clusters < 14; k++)
                        {
                            bool d = k < Math.Min(a.Length, b.Length) && a[k] != b[k];
                            if (d && startAt < 0) startAt = k;
                            if (d) last = k;
                            if (startAt >= 0 && (!d && k - last > 64)) { Console.WriteLine($"    diff run {startAt:N0}..{last:N0} (~{(startAt - 100) / 128004.0:F2} arrays in)"); clusters++; startAt = -1; }
                        } }
                }
            }
            else { runner.Advance(150); twinRunner?.Advance(150); }
            if (twin is not null && !diverged && twin.ComputeChecksum() != match.ComputeChecksum()) { diverged = true; Console.WriteLine($"  first divergence after tick {runner.Tick}: {Parts(match)} vs {Parts(twin)}"); }
            if (resumeAt > 0 && twin is null && runner.Tick >= resumeTick)
            {
                // Resume check: snapshot the live match, restore it into a fresh model and run both on with the same commands.
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) match.WriteSnapshot(writer);
                long snapshotBytes = stream.Length;
                stream.Position = 0;
                twin = MatchModel.Create(seed, pack, world, Archetypes, controls, rules: rules);
                twin.Research.DiagnosticDraftProjectGrantsEnabled = grantE5 || grantE6;
                byte[] snapshotCopy = stream.ToArray();
                using (var reader = new BinaryReader(new MemoryStream(snapshotCopy))) twin.ReadSnapshot(reader);
                twin2 = MatchModel.Create(seed, pack, world, Archetypes, controls, rules: rules);
                twin2.Research.DiagnosticDraftProjectGrantsEnabled = grantE5 || grantE6;
                using (var reader2 = new BinaryReader(new MemoryStream(snapshotCopy))) twin2.ReadSnapshot(reader2);
                twin2Runner = new SimRunner(twin2, startTick: runner.Tick);
                twinRunner = new SimRunner(twin, startTick: runner.Tick);
                if (Environment.GetEnvironmentVariable("CIVSIM_DEEPDIFF") == "1")
                {
                    var found = new List<string>();
                    DeepDiff(match.Economy, twin.Economy, "economy", 0, found, new HashSet<object>(ReferenceEqualityComparer.Instance));
                    foreach (string line in found) if (!(line.Contains("Ledger") || line.Contains("Scratch") || line.Contains("Stamp") || line.Contains("Generation") || line.Contains("B28") || line.Contains("Telemetry") || line.Contains("Trace"))) Console.WriteLine("    deepdiff " + line);
                }
                Console.WriteLine($"  resume: snapshot at tick {runner.Tick} ({snapshotBytes:N0} bytes), checksums {match.ComputeChecksum():x16} vs {twin.ComputeChecksum():x16}");
            }
            int season = checked((int)(runner.Tick / 150L));
            for (int c = 0; c < civs; c++)
                for (int k = 0; k < 3; k++)
                    if (seasonOf[c, k] < 0 && keys[k] >= 0 && research.IsDone(c, keys[k])) seasonOf[c, k] = season;
            end = match.EndOfMatch();
        }
        if (twin is not null) Console.WriteLine($"  resume check: {(twin.ComputeChecksum() == match.ComputeChecksum() ? "IDENTICAL" : "DIVERGED")} at tick {runner.Tick}: {match.ComputeChecksum():x16} vs {twin.ComputeChecksum():x16}");
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
        var goodTotals = new System.Text.StringBuilder();
        foreach (string good in new[] { "G-ALKALI", "G-PLATEGLASS", "G-PARTS", "G-CHRONOMETER", "G-TELESCOPE" })
        {
            Fix64 total = Fix64.Zero;
            for (int s = 0; s < settlements; s++) total += match.Economy.DiscoveryGoodStock(s, good);
            goodTotals.Append($"{good}={total.ToDoubleForDisplay():F1} ");
        }
        Console.WriteLine("  goods: " + goodTotals.ToString().TrimEnd());
        if (Environment.GetEnvironmentVariable("CIVSIM_CHAINDIAG") == "1")
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var avail = typeof(LivingEconomyModel).GetMethod("FixtureRecipeAvailable", F)!;
            string[] recipes = { "make_alkali", "make_plate_glass", "make_parts", "make_chronometer", "make_telescope" };
            string[] inputs = { "G-GLASS", "G-ALKALI", "G-STEEL", "G-WROUGHT", "G-PLATEGLASS", "G-PARTS", "G-SALT", "G-LIME", "G-COAL", "G-CHAR" };
            for (int c = 0; c < civs; c++)
            {
                var can = new int[recipes.Length]; var have = new int[inputs.Length]; int towns = 0;
                for (int s = 0; s < settlements; s++)
                {
                    if (match.Economy.SettlementLite(s).Civilization != c) continue;
                    towns++;
                    for (int r = 0; r < recipes.Length; r++) if ((bool)avail.Invoke(match.Economy, new object[] { s, recipes[r] })!) can[r]++;
                    for (int g = 0; g < inputs.Length; g++) if (match.Economy.DiscoveryGoodStock(s, inputs[g]) > Fix64.Zero || match.Economy.FiefStockPublic(s, inputs[g]) > Fix64.Zero) have[g]++;
                }
                int pb = match.Economy.WonderUnderConstruction(c);
                if (pb >= 0)
                    Console.WriteLine($"    palace town {pb}: can-run " + string.Join(" ", recipes.Select(n => $"{n}={(bool)avail.Invoke(match.Economy, new object[] { pb, n })!}")) + " | stock " +
                        string.Join(" ", inputs.Select(n => $"{n}={(match.Economy.DiscoveryGoodStock(pb, n) + match.Economy.FiefStockPublic(pb, n)).ToDoubleForDisplay():F1}")) + $" pop={match.Economy.Population(pb)}");
                Console.WriteLine($"  chain civ{c}: towns={towns} can-run " + string.Join(" ", recipes.Select((n, r) => $"{n}={can[r]}")) + " | towns holding " + string.Join(" ", inputs.Select((n, g) => $"{n}={have[g]}")));
            }
        }
        Console.WriteLine("  instruments held at a harbor: " + string.Join(", ", Enumerable.Range(0, civs).Select(c => $"civ{c} chronometer={match.Economy.ChronometerHeld(c)} telescope={match.Economy.TelescopeHeld(c)}")));
        Console.WriteLine("  palace: " + string.Join(", ", Enumerable.Range(0, civs).Select(c =>
        {
            if (match.Economy.HoldsWonder(c, "renaissance_palace")) return $"civ{c} HELD";
            int builder = match.Economy.WonderUnderConstruction(c);
            return builder < 0 ? $"civ{c} not started" : $"civ{c} building {LivingEconomyModel.IronWorkId(match.Economy.IronBuildAt(builder).Kind)} [{match.Economy.IronBuildShortages(builder)}]";
        })));
        {
            var laneFrame = new CivSim.Sim.View.MatchFrame();
            new CivSim.Sim.View.FrameBuilder(match, -1).Build(match, laneFrame); // a spectator's frame lists every route, so every deep-ocean lane
            Console.WriteLine($"  ocean lanes (deep-ocean trade routes in the world): {laneFrame.Discovery.LaneCount} of {laneFrame.RouteCount} routes");
        }
        Console.WriteLine("  built: " + string.Join(", ", Structures.Select((name, j) => $"{name}={totals[j]}")) + $"; settlements={settlements}, holding coal={coalSettlements}, colonies={match.Economy.ColonyCount()}");
    }
}
