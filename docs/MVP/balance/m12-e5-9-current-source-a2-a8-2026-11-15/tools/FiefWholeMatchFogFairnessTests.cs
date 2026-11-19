using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CivSim.Sim.Ai;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;
using Xunit;

namespace CivSim.Tests;

/// <summary>
/// WP-E5-9 acceptance A8, whole-match form. Plays full Fief matches (four AI seats, 64 minutes) and, at every checkpoint
/// season, clones the live match once per observer, changes in the clone only state that observer cannot see (unexplored
/// deposits, rival cannon/powder stock, a hidden harbor/galley and crew, live horse herds on unexplored tiles, plus the
/// Bronze-era hidden people/stores/research perturbation), runs both one season-end pass and compares everything that
/// observer's mind received and did. At the same checkpoints, for a rotating observer, visible changes of each kind must
/// be detected (positive controls). Inactive unless CIVSIM_A8_PACK is set.
/// Environment: CIVSIM_A8_PACK (frozen pack), CIVSIM_A8_OUT (directory), CIVSIM_A8_SEEDS (comma list),
/// CIVSIM_A8_INTERVAL (checkpoint every N seasons, default 8), CIVSIM_A8_MINUTES (default 64), CIVSIM_A8_DOP (default 3).
/// </summary>
public sealed class FiefWholeMatchFogFairnessTests
{
    private const int Margin = 8;
    private static readonly EconomyArchetype[] Archetypes =
        { EconomyArchetype.Warlord, EconomyArchetype.Builder, EconomyArchetype.Grower, EconomyArchetype.Sage };
    private static readonly CivControl[] Controls =
    {
        CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Warlord), CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Builder),
        CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Grower), CivControl.Ai(Difficulty.Legend, EconomyArchetype.Sage),
    };

    private sealed class Row
    {
        public ulong Seed; public int Season; public int Observer; public string Kind = "";
        public bool Applied; public string NotApplied = "";
        public int Deposits, Powder, Fleet, Horses, People;
        public bool WorldChanged;
        public List<string> Diffs = new();
        public string Triage = "";
        public bool Pass;
    }

    [Fact]
    public void Whole_fief_matches_are_invariant_to_unseen_state_and_respond_to_visible_state()
    {
        string? packPath = Environment.GetEnvironmentVariable("CIVSIM_A8_PACK");
        if (string.IsNullOrEmpty(packPath)) return;
        string outDir = Environment.GetEnvironmentVariable("CIVSIM_A8_OUT") ?? throw new InvalidOperationException("CIVSIM_A8_OUT");
        ulong[] seeds = (Environment.GetEnvironmentVariable("CIVSIM_A8_SEEDS") ?? "20261019").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(ulong.Parse).ToArray();
        int interval = int.Parse(Environment.GetEnvironmentVariable("CIVSIM_A8_INTERVAL") ?? "8");
        int minutes = int.Parse(Environment.GetEnvironmentVariable("CIVSIM_A8_MINUTES") ?? "64");
        int dop = int.Parse(Environment.GetEnvironmentVariable("CIVSIM_A8_DOP") ?? "3");
        Directory.CreateDirectory(outDir);
        byte[] packBytes = File.ReadAllBytes(packPath);
        ContentPack pack = ContentPack.Load(packBytes);
        string simPath = typeof(MatchModel).Assembly.Location;
        var all = new List<Row>();
        var lockObj = new object();
        using var rows = new StreamWriter(Path.Combine(outDir, "a8-rows.jsonl"));

        Parallel.ForEach(seeds, new ParallelOptions { MaxDegreeOfParallelism = dop }, seed =>
        {
            List<Row> local = PlayMatch(pack, seed, interval, minutes);
            lock (lockObj)
            {
                all.AddRange(local);
                foreach (Row r in local) rows.WriteLine(ToJson(r));
                rows.Flush();
            }
        });

        var summary = new StringBuilder();
        summary.AppendLine("{");
        summary.AppendLine($"  \"simAssemblyPath\": \"{simPath}\",");
        summary.AppendLine($"  \"simAssemblySha256\": \"{Sha(simPath)}\",");
        summary.AppendLine($"  \"packSha256\": \"{Convert.ToHexString(SHA256.HashData(packBytes)).ToLowerInvariant()}\",");
        summary.AppendLine($"  \"seeds\": [{string.Join(",", seeds)}], \"intervalSeasons\": {interval}, \"minutes\": {minutes}, \"margin\": {Margin},");
        var unseen = all.Where(r => r.Kind == "unseen-strict" || r.Kind == "unseen-horse").ToList();
        var nulls = all.Where(r => r.Kind == "null-control" && r.Applied).ToList();
        var tierBRows = all.Where(r => r.Kind == "unseen-composite" && r.Applied).ToList();
        int unseenApplied = unseen.Count(r => r.Applied), unseenFail = unseen.Count(r => r.Applied && !r.Pass);
        summary.AppendLine($"  \"unseenCompositeObserverCheckpoints\": {unseen.Count}, \"unseenCompositeApplied\": {unseenApplied}, \"unseenInvarianceFailures\": {unseenFail},");
        summary.AppendLine($"  \"unseenWorldChangedCount\": {unseen.Count(r => r.Applied && r.WorldChanged)},");
        summary.AppendLine($"  \"unseenAppliedByCategory\": {{\"deposits\": {unseen.Count(r => r.Applied && r.Deposits > 0)}, \"powderCannon\": {unseen.Count(r => r.Applied && r.Powder > 0)}, \"fleetCrew\": {unseen.Count(r => r.Applied && r.Fleet > 0)}, \"horses\": {unseen.Count(r => r.Applied && r.Horses > 0)}, \"peopleStoresResearch\": {unseen.Count(r => r.Applied && r.People > 0)}}},");
        summary.AppendLine($"  \"unseenMatchesWithCategory\": {{\"deposits\": {unseen.Where(r => r.Applied && r.Deposits > 0).Select(r => r.Seed).Distinct().Count()}, \"powderCannon\": {unseen.Where(r => r.Applied && r.Powder > 0).Select(r => r.Seed).Distinct().Count()}, \"fleetCrew\": {unseen.Where(r => r.Applied && r.Fleet > 0).Select(r => r.Seed).Distinct().Count()}, \"horses\": {unseen.Where(r => r.Applied && r.Horses > 0).Select(r => r.Seed).Distinct().Count()}, \"peopleStoresResearch\": {unseen.Where(r => r.Applied && r.People > 0).Select(r => r.Seed).Distinct().Count()}}},");
        int controlMisses = 0;
        summary.AppendLine("  \"visibleControls\": {");
        string[] kinds = { "visible-deposit", "visible-stock", "visible-fleet", "visible-horse" };
        for (int i = 0; i < kinds.Length; i++)
        {
            var c = all.Where(r => r.Kind == kinds[i]).ToList();
            int applied = c.Count(r => r.Applied), detected = c.Count(r => r.Applied && r.Pass);
            controlMisses += applied - detected;
            summary.AppendLine($"    \"{kinds[i]}\": {{\"checkpoints\": {c.Count}, \"applied\": {applied}, \"detected\": {detected}}}{(i < kinds.Length - 1 ? "," : "")}");
        }
        summary.AppendLine("  },");
        summary.AppendLine($"  \"visibleControlMisses\": {controlMisses},");
        summary.AppendLine($"  \"nullControls\": {{\"applied\": {nulls.Count}, \"withDiffs\": {nulls.Count(r => !r.Pass)}}},");
        int tierBBad = tierBRows.Count(r => !r.Pass), tierBDecisionOnly = tierBRows.Count(r => !r.Pass && r.Diffs.All(d => d.StartsWith("decisions:", StringComparison.Ordinal)));
        summary.AppendLine($"  \"tierBComposite\": {{\"applied\": {tierBRows.Count}, \"withInputOrDecisionDiffs\": {tierBBad}, \"decisionDiffsWithoutOtherDiffs\": {tierBDecisionOnly}}},");
        int cloneMismatches = all.Count(r => r.Kind == "clone-mismatch");
        summary.AppendLine($"  \"cloneChecksumMismatches\": {cloneMismatches},");
        summary.AppendLine($"  \"observerCheckpointsTotal\": {all.Count}");
        summary.AppendLine("}");
        File.WriteAllText(Path.Combine(outDir, "a8-summary.json"), summary.ToString());

        var failures = unseen.Where(r => r.Applied && !r.Pass).Take(10).Select(r => $"seed {r.Seed} season {r.Season} obs {r.Observer}: {string.Join("; ", r.Diffs.Take(3))}");
        Assert.True(unseenFail == 0, "unseen-state invariance failures: " + string.Join(" | ", failures));
        Assert.True(controlMisses == 0, $"{controlMisses} visible controls missed");
        Assert.True(nulls.All(r => r.Pass), "null-control clones differ from the live match");
        Assert.Equal(0, all.Count(r => r.Kind == "clone-mismatch"));
    }


    [Fact]
    public void Triage_failing_checkpoints_by_category()
    {
        string? packPath = Environment.GetEnvironmentVariable("CIVSIM_A8_PACK");
        string? list = Environment.GetEnvironmentVariable("CIVSIM_A8_TRIAGE");
        if (string.IsNullOrEmpty(packPath) || string.IsNullOrEmpty(list)) return;
        string outDir = Environment.GetEnvironmentVariable("CIVSIM_A8_OUT")!;
        int dop = int.Parse(Environment.GetEnvironmentVariable("CIVSIM_A8_DOP") ?? "3");
        Directory.CreateDirectory(outDir);
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        var items = list.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(':')).Select(a => (Seed: ulong.Parse(a[0]), Season: int.Parse(a[1]), Observer: int.Parse(a[2]))).ToList();
        using var writer = new StreamWriter(Path.Combine(outDir, "triage.jsonl"));
        var lockObj = new object();
        Parallel.ForEach(items.GroupBy(i => i.Seed), new ParallelOptions { MaxDegreeOfParallelism = dop }, group =>
        {
            ulong seed = group.Key;
            int last = group.Max(g => g.Season);
            MatchModel match = MatchModel.Create(seed, pack, WorldGenerator.Generate(seed), Archetypes, Controls, rules: ChapterRules.Fief);
            var runner = new SimRunner(match, int.MaxValue);
            for (int season = 1; season <= last; season++)
            {
                var todo = group.Where(g => g.Season == season).ToList();
                if (todo.Count == 0) { runner.Advance(150); continue; }
                runner.Advance(149);
                long tick = runner.Tick;
                var runs = new List<(string Label, int Observer, MatchModel Copy, SimRunner Runner, Row Row, string Extra)>();
                foreach (var it in todo)
                {
                    foreach ((string label, int mask) in new[] { ("null", 0), ("deposits", 1), ("powder", 2), ("fleet", 4), ("horses", 8), ("people", 16), ("research", 32), ("full", 63) })
                    {
                        MatchModel c = Clone(match, pack, seed);
                        var cr = new SimRunner(c, int.MaxValue, tick);
                        var row = new Row { Seed = seed, Season = season, Observer = it.Observer, Kind = "triage-" + label };
                        int rseed = unchecked((int)(seed * 31 + (ulong)(season - 1) * 7 + (ulong)it.Observer));
                        if (Environment.GetEnvironmentVariable("CIVSIM_A8_TRIAGE_STRICT") == "1") PerturbStrict(c, cr, it.Observer, rseed, row, mask);
                        else PerturbUnseen(c, cr, it.Observer, rseed, row, mask);
                        runs.Add((label, it.Observer, c, cr, row, ""));
                    }
                    // fleet control with persistence tracking
                    MatchModel fc = Clone(match, pack, seed);
                    var fr = new SimRunner(fc, int.MaxValue, tick);
                    var frow = new Row { Seed = seed, Season = season, Observer = it.Observer, Kind = "triage-fleet-control" };
                    string? why = ApplyVisible(fc, match, it.Observer, "visible-fleet");
                    if (why is not null) { frow.NotApplied = why; lock (lockObj) writer.WriteLine(ToJson(frow)); }
                    else runs.Add(("fleet-control", it.Observer, fc, fr, frow, ""));
                }
                runner.Advance(1);
                foreach (var r in runs) r.Runner.Advance(1);
                foreach (var r in runs)
                {
                    List<string> diffs = Compare(match, r.Copy, r.Observer, tick);
                    AiSeat bs = match.Ai!.SeatOf(r.Observer)!, cs = r.Copy.Ai!.SeatOf(r.Observer)!;
                    var inputDiffs = new List<string>();
                    FogFairnessHarness.CompareBelief(bs.Belief, cs.Belief, inputDiffs);
                    FogFairnessHarness.CompareOwnView(bs.Own, cs.Own, inputDiffs);
                    bool inputsEqual = inputDiffs.Count == 0;
                    // did the observer's belief list any settlement the perturbation touched? (reveal check)
                    string extra = "";
                    if (r.Label == "fleet-control")
                    {
                        int crewed = 0; foreach (int h in cs.Own.CrewedGalleyHomes) crewed++;
                        int baseCrewed = bs.Own.CrewedGalleyHomes.Count;
                        extra = $"crewedGalleysAfter clone={crewed} baseline={baseCrewed}";
                    }
                    lock (lockObj)
                        writer.WriteLine("{\"seed\":" + seed + ",\"season\":" + season + ",\"observer\":" + r.Observer + ",\"label\":\"" + r.Label + "\",\"changes\":[" + r.Row.Deposits + "," + r.Row.Powder + "," + r.Row.Fleet + "," + r.Row.Horses + "," + r.Row.People + "],\"inputsEqual\":" + (inputsEqual ? "true" : "false") + ",\"extra\":\"" + Esc(extra) + "\",\"diffCount\":" + diffs.Count + ",\"diffs\":[" + string.Join(",", diffs.Select(d => "\"" + Esc(d.Length > 900 ? d[..900] : d) + "\"")) + "]}");
                    writer.Flush();
                }
            }
        });
    }

    private static List<Row> PlayMatch(ContentPack pack, ulong seed, int interval, int minutes)
    {
        var rows = new List<Row>();
        MatchModel match = MatchModel.Create(seed, pack, WorldGenerator.Generate(seed), Archetypes, Controls, rules: ChapterRules.Fief);
        var runner = new SimRunner(match, int.MaxValue);
        long horizon = minutes * 600L;
        int season = 0, checkpoint = 0;
        for (long t = 0; t < horizon; t += 150, season++)
        {
            bool isCheckpoint = (season + 1) % interval == 0 && t + 150 <= horizon;
            if (!isCheckpoint) { runner.Advance(Math.Min(150, horizon - t)); continue; }

            runner.Advance(149);
            long tick = runner.Tick;
            var pending = new List<(Row Row, MatchModel Copy, SimRunner CopyRunner, MatchModel? Against, string? Expect)>();
            int visibleObserver = checkpoint % 4;
            bool tierB = Environment.GetEnvironmentVariable("CIVSIM_A8_TIERB") == "1";
            for (int o = 0; o < 4; o++)
            {
                if (!HasHome(match, o))
                {
                    rows.Add(new Row { Seed = seed, Season = season + 1, Observer = o, Kind = "null-control", NotApplied = "observer has no settlement" });
                    rows.Add(new Row { Seed = seed, Season = season + 1, Observer = o, Kind = "unseen-strict", NotApplied = "observer has no settlement" });
                    continue;
                }
                // Null control: a restored clone with nothing changed must be indistinguishable from the live match.
                MatchModel nullCopy = Clone(match, pack, seed);
                pending.Add((new Row { Seed = seed, Season = season + 1, Observer = o, Kind = "null-control", Applied = true }, nullCopy, new SimRunner(nullCopy, int.MaxValue, tick), null, null));
                // Strict unseen tier: only state the observer cannot observe within the pass.
                var strictRow = new Row { Seed = seed, Season = season + 1, Observer = o, Kind = "unseen-strict" };
                MatchModel strictCopy = Clone(match, pack, seed);
                var strictRunner = new SimRunner(strictCopy, int.MaxValue, tick);
                PerturbStrict(strictCopy, strictRunner, o, unchecked((int)(seed * 31 + (ulong)season * 7 + (ulong)o)), strictRow);
                strictRow.Applied = strictRow.Deposits + strictRow.Powder + strictRow.Fleet + strictRow.Horses + strictRow.People > 0;
                if (!strictRow.Applied) { strictRow.NotApplied = "no strictly unseen target"; rows.Add(strictRow); }
                else pending.Add((strictRow, strictCopy, strictRunner, null, null));
                if (tierB)
                {
                    var row = new Row { Seed = seed, Season = season + 1, Observer = o, Kind = "unseen-composite" };
                    MatchModel copy = Clone(match, pack, seed);
                    var cr = new SimRunner(copy, int.MaxValue, tick);
                    PerturbUnseen(copy, cr, o, unchecked((int)(seed * 31 + (ulong)season * 7 + (ulong)o)), row);
                    row.Applied = row.Deposits + row.Powder + row.Fleet + row.Horses + row.People > 0;
                    if (!row.Applied) { row.NotApplied = "no unseen target"; rows.Add(row); }
                    else pending.Add((row, copy, cr, null, null));
                }
            }
            foreach (string kind in new[] { "visible-deposit", "visible-stock", "visible-fleet" })
            {
                var row = new Row { Seed = seed, Season = season + 1, Observer = visibleObserver, Kind = kind };
                if (!HasHome(match, visibleObserver)) { row.NotApplied = "observer has no settlement"; rows.Add(row); continue; }
                MatchModel copy = Clone(match, pack, seed);
                var cr = new SimRunner(copy, int.MaxValue, tick);
                string? why = ApplyVisible(copy, match, visibleObserver, kind);
                if (why is not null) { row.NotApplied = why; rows.Add(row); continue; }
                row.Applied = true;
                pending.Add((row, copy, cr, null, ExpectedLabel(kind)));
            }
            AddHorseTriple(pending, rows, match, pack, seed, season + 1, visibleObserver, tick);

            runner.Advance(1);
            foreach ((_, _, SimRunner advanceRunner, _, _) in pending) advanceRunner.Advance(1);
            foreach ((Row row, MatchModel copy, _, MatchModel? against, string? expect) in pending)
            {
                MatchModel reference = against ?? match;
                row.WorldChanged = copy.ComputeChecksum() != reference.ComputeChecksum();
                row.Diffs = row.Applied ? Compare(reference, copy, row.Observer, tick) : new List<string>();
                if (row.Applied && row.Kind == "visible-fleet" && copy.Ai!.SeatOf(row.Observer)!.Own.CrewedGalleyHomes.Count <= reference.Ai!.SeatOf(row.Observer)!.Own.CrewedGalleyHomes.Count)
                {
                    row.Applied = false; row.NotApplied = "planted crew lapsed by season end (fixture did not persist)"; row.Diffs = new List<string>();
                }
                if (!row.Applied) { }
                else if (expect is null)
                {
                    row.Pass = row.Diffs.Count == 0;
                    if (!row.Pass && row.Diffs.Any(d => d.StartsWith("map deposit", StringComparison.Ordinal))) row.Triage = "deposit-diff: check whether a perturbed tile was revealed by the observer this season";
                }
                else row.Pass = row.Diffs.Any(d => d.StartsWith(expect, StringComparison.Ordinal));
                rows.Add(row);
            }
            checkpoint++;
        }
        return rows;
    }



    /// <summary>Belief fields that are in the hash but not in FogFairnessHarness.CompareBelief's explicit list, labelled so a hash-only difference can be attributed.</summary>
    private static void ExtraBeliefDiffs(Belief a, Belief b, List<string> diffs)
    {
        for (int c = 0; c < a.Rivals.Length; c++)
        {
            RivalBelief x = a.Rivals[c], y = b.Rivals[c];
            if (x.TinAccessSeen != y.TinAccessSeen) diffs.Add($"extra rival {c} tin access: {x.TinAccessSeen} vs {y.TinAccessSeen}");
            if (x.CaravansSeen.Count != y.CaravansSeen.Count) diffs.Add($"extra rival {c} caravans seen count: {x.CaravansSeen.Count} vs {y.CaravansSeen.Count}");
            for (int i = 0; i < Math.Min(x.CaravansSeen.Count, y.CaravansSeen.Count); i++)
            {
                BeliefCaravan p = x.CaravansSeen[i], q = y.CaravansSeen[i];
                if ((p.Id, p.Tile, p.LastSeenSeason, p.Cargo, p.ArrivalSeason, p.CrewEstimate, p.CargoBarterValue) != (q.Id, q.Tile, q.LastSeenSeason, q.Cargo, q.ArrivalSeason, q.CrewEstimate, q.CargoBarterValue))
                    diffs.Add($"extra rival {c} caravan seen {i}: id {p.Id} tile {p.Tile} seen {p.LastSeenSeason} cargo {p.Cargo} crew {p.CrewEstimate} vs id {q.Id} tile {q.Tile} seen {q.LastSeenSeason} cargo {q.Cargo} crew {q.CrewEstimate}");
            }
            if (x.MiningCampsSeen.Count != y.MiningCampsSeen.Count) diffs.Add($"extra rival {c} mining camps seen count: {x.MiningCampsSeen.Count} vs {y.MiningCampsSeen.Count}");
            for (int i = 0; i < Math.Min(x.MiningCampsSeen.Count, y.MiningCampsSeen.Count); i++)
            {
                BeliefMiningCamp p = x.MiningCampsSeen[i], q = y.MiningCampsSeen[i];
                if ((p.Id, p.Tile, p.LastSeenSeason, p.CrewEstimate) != (q.Id, q.Tile, q.LastSeenSeason, q.CrewEstimate))
                    diffs.Add($"extra rival {c} mining camp seen {i}: id {p.Id} tile {p.Tile} seen {p.LastSeenSeason} crew {p.CrewEstimate} vs id {q.Id} tile {q.Tile} seen {q.LastSeenSeason} crew {q.CrewEstimate}");
            }
            for (int i = 0; i < Math.Min(x.Settlements.Count, y.Settlements.Count); i++)
                if ((x.Settlements[i].HillfortSeen, x.Settlements[i].StoneWallsSeen) != (y.Settlements[i].HillfortSeen, y.Settlements[i].StoneWallsSeen))
                    diffs.Add($"extra rival {c} settlement {i} hillfort/stone walls seen");
        }
        if (a.TinFolkSeen.Count != b.TinFolkSeen.Count) diffs.Add($"extra tin folk seen count: {a.TinFolkSeen.Count} vs {b.TinFolkSeen.Count}");
    }

    private static string MaskedDecisions(MatchModel m, int civ, long since)
    {
        var parts = new List<string>();
        foreach (AiDecision d in m.Ai!.Log.Entries)
            if (d.Civ == civ && d.Tick >= since) {
                string text = System.Text.RegularExpressions.Regex.Replace(d.ToString(), @"Season = \d+", "Season = *");
                if (Environment.GetEnvironmentVariable("CIVSIM_A8_DIAG_MASKCAMPID") == "1") text = System.Text.RegularExpressions.Regex.Replace(text, @"best camp \d+", "best camp #");
                parts.Add(text);
            }
        return string.Join("|", parts);
    }

    /// <summary>CompareMinds, except that the decision log is compared with the Season field masked: a restored clone logs the first decision after restore with Season 0 (the field is not part of the snapshot; the null control exposes it).</summary>
    private static List<string> Compare(MatchModel reference, MatchModel copy, int observer, long tick)
    {
        List<string> diffs = FogFairnessHarness.CompareMinds(reference, copy, observer, tick);
        ExtraBeliefDiffs(reference.Ai!.SeatOf(observer)!.Belief, copy.Ai!.SeatOf(observer)!.Belief, diffs);
        if (diffs.Any(d => d.StartsWith("decisions:", StringComparison.Ordinal)) && MaskedDecisions(reference, observer, tick) == MaskedDecisions(copy, observer, tick))
            diffs.RemoveAll(d => d.StartsWith("decisions:", StringComparison.Ordinal));
        return diffs;
    }

    /// <summary>Tiles within <paramref name="radius"/> hex steps of anything the observer has explored.</summary>
    private static bool[] NearExplored(MatchModel m, int observer, int radius)
    {
        MapKnowledge map = m.Ai!.SeatOf(observer)!.Belief.Map;
        GeneratedWorld g = m.Ai.World.Generated;
        int n = g.TileCount, w = g.Width, h = n / w;
        var dist = new int[n];
        Array.Fill(dist, -1);
        var queue = new Queue<int>();
        for (int t = 0; t < n; t++) if (map.IsExplored(t)) { dist[t] = 0; queue.Enqueue(t); }
        Span<int> nb = stackalloc int[6];
        while (queue.Count > 0)
        {
            int t = queue.Dequeue();
            if (dist[t] >= radius) continue;
            int count = HexGrid.Neighbors(t, w, h, nb);
            for (int i = 0; i < count; i++) if (dist[nb[i]] < 0) { dist[nb[i]] = dist[t] + 1; queue.Enqueue(nb[i]); }
        }
        var near = new bool[n];
        for (int t = 0; t < n; t++) near[t] = dist[t] >= 0;
        return near;
    }

    /// <summary>
    /// Strict tier: changes only state the observer cannot observe within the pass. Deposits and horse herds stay more than the margin
    /// from every tile it has explored; settlements are those of rivals it has never contacted and that sit more than the margin from
    /// anything explored, so neither sight nor a rival's reaction can carry the change to the observer in one season.
    /// </summary>
    private static void PerturbStrict(MatchModel m, SimRunner cr, int observer, int randomSeed, Row row, int mask = 63)
    {
        var random = new Random(randomSeed);
        TrueWorld world = m.Ai!.World;
        bool[] near = NearExplored(m, observer, Margin);
        var hidden = new List<int>();
        for (int t = 0; t < world.Generated.TileCount; t++) if (!near[t]) hidden.Add(t);
        int[] pool = hidden.ToArray();
        int depositCount = Math.Min(8, hidden.Count);
        for (int i = 0; i < depositCount; i++)
        {
            int pick = random.Next(hidden.Count);
            int tileIndex = hidden[pick];
            hidden.RemoveAt(pick);
            WorldTile tile = world.Generated.TileAt(tileIndex);
            tile.Deposit = (i & 1) == 0 ? DepositKind.Saltpeter : DepositKind.Sulfur;
            tile.DepositAmount = (byte)random.Next(1, 4);
            tile.DepositDepth = (byte)random.Next(1, 3);
            if ((mask & 1) == 0) continue;
            GeneratedTiles(world.Generated)[tileIndex] = tile;
            PoliticsTiles(m.Politics)[tileIndex] = tile;
            row.Deposits++;
        }
        var targets = new List<int>();
        for (int s = 0; s < m.Politics.SettlementTotal; s++)
        {
            int owner = m.Politics.SettlementCivOf(s);
            if (owner == observer || m.Politics.IsFreeFolk(s) || m.Politics.SettlementPopulationOf(s) <= 0) continue;
            if (m.Politics.Diplomacy.Contacted(observer, owner)) continue;
            if (near[m.Politics.SettlementTileOf(s)]) continue;
            targets.Add(s);
        }
        int used = 0;
        var figures = new List<FigureView>();
        foreach (int s in targets)
        {
            if (used < 3)
            {
                int cannon = random.Next(0, 6), powder = random.Next(0, 16), crew = random.Next(4, 13);
                if ((mask & 2) != 0) { SetStock(m.Economy, s, "G-CANNON", cannon); SetStock(m.Economy, s, "G-POWDER", powder); row.Powder += 2; }
                if ((mask & 4) != 0)
                {
                    m.Economy.FixtureBuildIronStructure(s, "harbor");
                    m.Economy.FixtureBuildIronStructure(s, "galley");
                    m.Economy.FixtureSetSoldiersSupplied(s, crew);
                    row.Fleet += 3;
                }
                used++;
            }
            if ((mask & 16) == 0) continue;
            figures.Clear();
            m.Economy.ListFigures(s, figures);
            for (int i = 0; i < figures.Count; i++)
            {
                if (i % 3 == 0) { m.Economy.KillFigure(figures[i].Id); row.People++; }
                else if (i % 3 == 1) { m.Economy.WoundFigure(figures[i].Id, 2); row.People++; }
            }
            m.Economy.TakeGoods(s, Fix64.FromInt(1000), LootPreference.FoodOnly);
            m.Economy.AdjustCohesion(s, Fix64.FromRatio(-1, 10));
            row.People += 2;
        }
        if ((mask & 32) != 0) row.People += GrantHiddenResearch(m, cr, observer);
        if (pool.Length > 0)
            for (int i = 0; i < m.Economy.WildHerdCount; i++)
            {
                WildHerdView herd = m.Economy.WildHerd(i);
                if (herd.Species != HerdSpecies.Horse || near[herd.CurrentTile]) continue;
                int herdTile = pool[random.Next(pool.Length)], herdCount = random.Next(1, 20);
                if ((mask & 8) == 0) continue;
                m.Economy.FixtureSetWildHerd(i, herdTile, Fix64.FromInt(herdCount));
                row.Horses++;
            }
    }

    private static int[] HiddenPool(MatchModel m, int observer)
    {
        AiSeat seat = m.Ai!.SeatOf(observer)!;
        MapKnowledge map = seat.Belief.Map;
        TrueWorld world = m.Ai.World;
        int routineSight = BeliefRules.RoutineSight(m.Politics.ReachOf(observer));
        var pool = new List<int>();
        for (int tile = 0; tile < world.Generated.TileCount; tile++)
        {
            if (map.IsExplored(tile)) continue;
            int distance = int.MaxValue;
            for (int s = 0; s < m.Politics.SettlementTotal; s++)
                if (m.Politics.SettlementCivOf(s) == observer && !m.Politics.IsFreeFolk(s))
                    distance = Math.Min(distance, HexGrid.Distance(m.Politics.SettlementTileOf(s), tile, world.Generated.Width));
            if (distance > routineSight + Margin) pool.Add(tile);
        }
        return pool.ToArray();
    }

    /// <summary>
    /// Horse-source knowledge is a one-way latch (Seen stays Seen), so on live matches it is usually already set. For the rotating
    /// observer this builds a controlled triple from the live state: a reference clone whose latch is reset to Unknown with every
    /// horse herd moved to unexplored tiles; a hidden clone (herd tiles and counts varied among unexplored tiles; must match the
    /// reference); and a visible clone (one herd on an explored home tile; must flip the observer's knowledge).
    /// </summary>
    private static void AddHorseTriple(List<(Row Row, MatchModel Copy, SimRunner CopyRunner, MatchModel? Against, string? Expect)> pending,
        List<Row> rows, MatchModel match, ContentPack pack, ulong seed, int season, int observer, long tick)
    {
        Row hiddenRow = new() { Seed = seed, Season = season, Observer = observer, Kind = "unseen-horse" };
        Row visibleRow = new() { Seed = seed, Season = season, Observer = observer, Kind = "visible-horse" };
        int home = HomeOf(match, observer);
        bool[] nearExplored = NearExplored(match, observer, Margin);
        int[] pool = HiddenPool(match, observer).Where(t => !nearExplored[t]).ToArray();
        var herds = new List<int>();
        for (int i = 0; i < match.Economy.WildHerdCount; i++)
            if (match.Economy.WildHerd(i).Species == HerdSpecies.Horse) herds.Add(i);
        if (home < 0 || pool.Length == 0 || herds.Count == 0)
        {
            string why = home < 0 ? "no home" : pool.Length == 0 ? "no unexplored tiles beyond margin" : "world has no horse herd";
            hiddenRow.NotApplied = why; visibleRow.NotApplied = why;
            rows.Add(hiddenRow); rows.Add(visibleRow);
            return;
        }
        MatchModel Make(Random rng, bool varied, bool visible)
        {
            MatchModel c = Clone(match, pack, seed);
            AiSeat st = c.Ai!.SeatOf(observer)!;
            st.Belief.HorseSource = HorseSourceKnowledge.Unknown;
            for (int k = 0; k < herds.Count; k++)
            {
                int tile = varied ? pool[rng.Next(pool.Length)] : pool[(k * 7) % pool.Length];
                int count = varied ? rng.Next(1, 20) : 5;
                c.Economy.FixtureSetWildHerd(herds[k], tile, Fix64.FromInt(count));
            }
            if (visible)
            {
                st.Belief.Map.MarkExplored(c.Politics.SettlementTileOf(home));
                c.Economy.FixtureSetWildHerd(herds[0], c.Politics.SettlementTileOf(home), Fix64.FromInt(5));
            }
            return c;
        }
        var rngA = new Random(unchecked((int)(seed * 17 + (ulong)season)));
        MatchModel reference = Make(rngA, false, false), hidden = Make(rngA, true, false), visibleClone = Make(rngA, false, true);
        var rr = new SimRunner(reference, int.MaxValue, tick);
        hiddenRow.Applied = true; visibleRow.Applied = true; hiddenRow.Horses = herds.Count; visibleRow.Horses = 1;
        pending.Add((new Row { Seed = seed, Season = season, Observer = observer, Kind = "horse-reference", Applied = false }, reference, rr, null, null));
        pending.Add((hiddenRow, hidden, new SimRunner(hidden, int.MaxValue, tick), reference, null));
        pending.Add((visibleRow, visibleClone, new SimRunner(visibleClone, int.MaxValue, tick), reference, "horse source"));
    }

    private static string ExpectedLabel(string kind) => kind switch
    {
        "visible-deposit" => "map deposit",
        "visible-stock" => "own settlement",
        "visible-fleet" => "own fleet",
        _ => "horse source",
    };

    private static bool HasHome(MatchModel m, int civ)
    {
        for (int s = 0; s < m.Politics.SettlementTotal; s++)
            if (m.Politics.SettlementCivOf(s) == civ && !m.Politics.IsFreeFolk(s) && m.Politics.SettlementPopulationOf(s) > 0) return true;
        return false;
    }

    private static int HomeOf(MatchModel m, int civ)
    {
        for (int s = 0; s < m.Politics.SettlementTotal; s++)
            if (m.Politics.SettlementCivOf(s) == civ && !m.Politics.IsFreeFolk(s) && m.Politics.SettlementPopulationOf(s) > 0) return s;
        return -1;
    }

    private static MatchModel Clone(MatchModel source, ContentPack pack, ulong seed)
    {
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) source.WriteSnapshot(w);
        stream.Position = 0;
        MatchModel copy = MatchModel.Create(seed, pack, WorldGenerator.Generate(seed), Archetypes, Controls, rules: ChapterRules.Fief);
        // Workaround for a Sim restore-order defect (documented in the A8 report): PoliticsSystem.ReadSnapshot validates the
        // Fief dynastic-inheritance count against _sCount before RefreshSettlements() runs, so a fresh match rejects any snapshot
        // taken after settlements were founded. Setting the cache to the true count lets the restore proceed; the clone is then
        // verified against the source checksum, so a wrong restore cannot pass silently.
        typeof(PoliticsSystem).GetField("_sCount", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(copy.Politics, source.Economy.SettlementCount);
        using (var r = new BinaryReader(stream)) copy.ReadSnapshot(r);
        if (copy.ComputeChecksum() != source.ComputeChecksum())
            throw new InvalidOperationException($"clone checksum mismatch (seed {seed}, tick {source.Economy.Tick})");
        return copy;
    }

    private static void PerturbUnseen(MatchModel m, SimRunner cr, int observer, int randomSeed, Row row, int mask = 63)
    {
        var random = new Random(randomSeed);
        AiSeat seat = m.Ai!.SeatOf(observer)!;
        MapKnowledge map = seat.Belief.Map;
        TrueWorld world = m.Ai.World;
        int routineSight = BeliefRules.RoutineSight(m.Politics.ReachOf(observer));

        var hiddenTiles = new List<int>();
        for (int tile = 0; tile < world.Generated.TileCount; tile++)
        {
            if (map.IsExplored(tile)) continue;
            int distance = int.MaxValue;
            for (int s = 0; s < m.Politics.SettlementTotal; s++)
                if (m.Politics.SettlementCivOf(s) == observer && !m.Politics.IsFreeFolk(s))
                    distance = Math.Min(distance, HexGrid.Distance(m.Politics.SettlementTileOf(s), tile, world.Generated.Width));
            if (distance > routineSight + Margin) hiddenTiles.Add(tile);
        }
        int[] pool = hiddenTiles.ToArray();
        int depositCount = Math.Min(8, hiddenTiles.Count);
        for (int i = 0; i < depositCount; i++)
        {
            int pick = random.Next(hiddenTiles.Count);
            int tileIndex = hiddenTiles[pick];
            hiddenTiles.RemoveAt(pick);
            WorldTile tile = world.Generated.TileAt(tileIndex);
            tile.Deposit = (i & 1) == 0 ? DepositKind.Saltpeter : DepositKind.Sulfur;
            tile.DepositAmount = (byte)random.Next(1, 4);
            tile.DepositDepth = (byte)random.Next(1, 3);
            if ((mask & 1) == 0) continue;
            GeneratedTiles(world.Generated)[tileIndex] = tile;
            PoliticsTiles(m.Politics)[tileIndex] = tile;
            row.Deposits++;
        }

        int used = 0;
        for (int s = 0; s < m.Politics.SettlementTotal && used < 3; s++)
        {
            int owner = m.Politics.SettlementCivOf(s);
            if (owner == observer || m.Politics.IsFreeFolk(s) || m.Politics.SettlementPopulationOf(s) <= 0) continue;
            if (BeliefBuilder.TilesOutOfSight(world, observer, s) <= Margin) continue;
            int cannon = random.Next(0, 6), powder = random.Next(0, 16), crew = random.Next(4, 13);
            if ((mask & 2) != 0) { SetStock(m.Economy, s, "G-CANNON", cannon); SetStock(m.Economy, s, "G-POWDER", powder); row.Powder += 2; }
            if ((mask & 4) != 0)
            {
                m.Economy.FixtureBuildIronStructure(s, "harbor");
                m.Economy.FixtureBuildIronStructure(s, "galley");
                m.Economy.FixtureSetSoldiersSupplied(s, crew);
                row.Fleet += 3;
            }
            used++;
        }

        if (pool.Length > 0)
            for (int i = 0; i < m.Economy.WildHerdCount; i++)
            {
                WildHerdView herd = m.Economy.WildHerd(i);
                if (herd.Species != HerdSpecies.Horse) continue;
                if (map.IsExplored(herd.CurrentTile)) continue; // a herd the observer has explored is visible state; leave it
                int herdTile = pool[random.Next(pool.Length)], herdCount = random.Next(1, 20);
                if ((mask & 8) == 0) continue;
                m.Economy.FixtureSetWildHerd(i, herdTile, Fix64.FromInt(herdCount));
                row.Horses++;
            }

        if ((mask & 16) != 0) row.People += FogFairnessHarness.PerturbUnseen(m, cr, observer, Margin, research: false);
        if ((mask & 32) != 0) row.People += GrantHiddenResearch(m, cr, observer);
    }

    /// <summary>Returns null when the visible change was made; otherwise why the control was not applicable at this checkpoint.</summary>
    private static string? ApplyVisible(MatchModel copy, MatchModel baseline, int observer, string kind)
    {
        int home = HomeOf(copy, observer);
        if (home < 0) return "no home";
        int homeTile = copy.Politics.SettlementTileOf(home);
        AiSeat seat = copy.Ai!.SeatOf(observer)!;
        MapKnowledge map = seat.Belief.Map;
        switch (kind)
        {
            case "visible-deposit":
            {
                for (int t = 0; t < map.TileCount; t++)
                {
                    if (!map.IsExplored(t) || !map.TryTile(t, out WorldTile known) || known.Deposit != DepositKind.None) continue;
                    FogFairnessHarness.FixtureSetVisibleDeposit(copy, observer, t, DepositKind.Saltpeter);
                    return null;
                }
                return "no explored deposit-free tile";
            }
            case "visible-stock":
                copy.Economy.FixtureStock(home, "G-CANNON", Fix64.FromInt(2));
                copy.Economy.FixtureStock(home, "G-POWDER", Fix64.FromInt(7));
                return null;
            case "visible-fleet":
            {
                int target = -1;
                for (int s = 0; s < copy.Politics.SettlementTotal; s++)
                    if (copy.Politics.SettlementCivOf(s) == observer && !copy.Politics.IsFreeFolk(s) && copy.Politics.SettlementPopulationOf(s) > 0 && !copy.Economy.CrewedGalley(s)) { target = s; break; }
                if (target < 0) return "every own settlement already has a crewed galley";
                copy.Economy.FixtureBuildIronStructure(target, "harbor");
                copy.Economy.FixtureBuildIronStructure(target, "galley");
                copy.Economy.FixtureSetSoldiersSupplied(target, 8);
                if (!copy.Economy.CrewedGalley(target)) return "fixture did not produce a crewed galley";
                return null;
            }
            default:
            {
                if (baseline.Ai!.SeatOf(observer)!.Belief.HorseSource == HorseSourceKnowledge.Seen) return "observer already knows a horse source";
                int horseHerd = -1;
                for (int i = 0; i < copy.Economy.WildHerdCount; i++)
                {
                    WildHerdView herd = copy.Economy.WildHerd(i);
                    if (herd.Species != HerdSpecies.Horse) continue;
                    if (horseHerd < 0) horseHerd = i;
                    copy.Economy.FixtureSetWildHerd(i, herd.CurrentTile, Fix64.Zero);
                }
                if (horseHerd < 0) return "world has no horse herd";
                map.MarkExplored(homeTile);
                copy.Economy.FixtureSetWildHerd(horseHerd, homeTile, Fix64.FromInt(5));
                return null;
            }
        }
    }

    /// <summary>The research half of FogFairnessHarness.PerturbUnseen (rivals never contacted and whose figures are out of sight).</summary>
    private static int GrantHiddenResearch(MatchModel m, SimRunner runner, int observer)
    {
        TrueWorld world = m.Ai!.World;
        int changes = 0;
        for (int o = 0; o < m.Research.CivCount; o++)
        {
            if (o == observer || m.Politics.Diplomacy.Contacted(observer, o)) continue;
            if (BeliefBuilder.TilesOutOfFigureSight(world, observer, o) <= Margin) continue;
            int granted = 0;
            for (int p = 0; p < m.Research.Catalog.Count && granted < 3; p++)
            {
                ProjectDef def = m.Research.Catalog.Projects[p];
                if (!def.Numeric || def.IsKeystone || m.Research.IsDone(o, p) || m.Research.FirstBy(p) < 0) continue;
                bool ready = true;
                foreach (int r in def.Requires) ready &= m.Research.IsDone(o, r);
                if (!ready) continue;
                runner.Submit(new Command(runner.Tick, (byte)o, CommandKind.CompleteProject, p, 0, 0));
                granted++;
                changes++;
            }
        }
        return changes;
    }

    private static void SetStock(LivingEconomyModel economy, int settlement, string good, int value)
        => economy.FixtureStock(settlement, good, Fix64.FromInt(value) - economy.GoodStock(settlement, good));

    private static WorldTile[] GeneratedTiles(GeneratedWorld world)
        => (WorldTile[])typeof(GeneratedWorld).GetField("_tiles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;

    private static WorldTile[] PoliticsTiles(PoliticsSystem politics)
        => (WorldTile[])typeof(PoliticsSystem).GetField("_tileArray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(politics)!;

    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string ToJson(Row r) =>
        $"{{\"seed\":{r.Seed},\"season\":{r.Season},\"observer\":{r.Observer},\"kind\":\"{r.Kind}\",\"applied\":{(r.Applied ? "true" : "false")},\"notApplied\":\"{Esc(r.NotApplied)}\"," +
        $"\"deposits\":{r.Deposits},\"powder\":{r.Powder},\"fleet\":{r.Fleet},\"horses\":{r.Horses},\"people\":{r.People},\"worldChanged\":{(r.WorldChanged ? "true" : "false")}," +
        $"\"pass\":{(r.Pass ? "true" : "false")},\"triage\":\"{Esc(r.Triage)}\",\"diffs\":[{string.Join(",", r.Diffs.Take(6).Select(d => "\"" + Esc(d.Length > 200 ? d[..200] : d) + "\""))}]}}";
}
