using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>
/// <c>e6-audit</c> (docs/MVP/38 §3.2, §7 D-T2 and D-T5, §11 D13; M13 WP-E6-3): a read-only audit of the existing world generator
/// for the three places Discovery leans on geography. (1) Coal: MIN-09 carries <c>[Seen: Coal]</c> and sits on the KEY-7 critical path, so
/// a start that can never "see" coal can never finish the age. (2) Sugarcane: FOD-10 carries <c>[Has: Sugarcane]</c>. (3) The sea: how far each
/// start is from a foreign start, whether the existing outrigger rule (OpenSeaReach 11, islands as stepping stones) already connects them, and
/// in how many legs. It generates worlds with <see cref="WorldGenerator"/> and never writes them: worldgen output, fingerprints and every
/// golden are untouched. Definitions are fixed here before any result is read.
/// <code>
/// civsim-harness e6-audit [--first N] [--seeds N] [--threads N] [--csv FILE]
/// </code>
/// The report is a function of the seed range alone (no clocks, no thread-order effects); <c>digest</c> hashes every per-start record.
/// <para>
/// How the sim evaluates <c>[Seen: Coal]</c> (read from the source, not assumed): <c>LivingEconomySource</c> builds the target tile list for "Coal" from
/// tiles whose <c>SurfaceEvidence</c> is <c>DarkSeam</c> (a regional surface sign that may sit on a barren tile), NOT from coal deposits; the gate
/// is satisfied once any DarkSeam tile lies within the civilization's era reach (35 at E6, times 1.5 with WAY-01) by hex distance of any of its
/// settlements, across water and borders alike. Two more paths exist: explorers' reveals mark the gate when they cross a coal DEPOSIT tile
/// (<c>PoliticsSystem.CommitReveal</c>), and barter or contact shares it (Coal is a traded good; a contact that completed MIN-09 makes it known).
/// </para>
/// </summary>
internal static class E6GeographyAudit
{
    // ---- fixed definitions ------------------------------------------------------------------------------

    /// <summary>content/registries/eras.yaml: E6 claim radius, E6 routine reach; WAY-01 multiplies reach by 3/2 (LivingEconomySource.ReachWithTrails).</summary>
    private const int ClaimRadius = 5, MiningBase = 8, MiningWater = 16, E5Reach = 25, E6Reach = 35, E6ReachTrails = 52;
    /// <summary>A daughter village sits at most 14 tiles (land) from its parent, so a realm that has fissioned once sees 14 further.</summary>
    private const int FissionHop = 14;
    private const int OpenSeaReach = 11, TilesPerSeason = 10;
    private const int Far = 999;

    private sealed class Res
    {
        public int OnMap;
        public int Nearest = Far, NearestSame = Far;
        public int N5, N8, N25, N35, N52, Unc5, Unc16, Unc35;
        public int Same35, Other35, Same52, Other52;
        public int WalkTiles = int.MaxValue, SailTiles = int.MaxValue;
        public int Step;
    }

    private sealed class Rec
    {
        public ulong Seed;
        public int Civ;
        public Res Seam = new(), Coal = new(), Sugar = new();
        public int CoalStock35;
        // coal deposits against the surface sign (world truth, same in every record of a world)
        public int CoalDeposits, CoalOnSeamTile, CoalOnOrganicBasin, SeamBarren;
        public int WorldAnySeam35, WorldAnySeam52, MateSeam35, MateSeam52;
        // sea
        public int MateHex = Far, MateWalk = int.MaxValue;
        public int FStartHex = Far, FStartWalk = int.MaxValue, FStartSail = int.MaxValue;
        public int FLandHex = Far, FLandSail = int.MaxValue, IslandHex = Far, IslandSail = int.MaxValue;
        public int Gap = Far, ReachDirect = Far, ReachIslands = Far, Legs11 = -1, Legs16 = -1, Legs22 = -1;
        public int OwnContinent;

        public bool SeamSees35 => Seam.N35 > 0;
        public bool SeamSees52 => Seam.N52 > 0;

        private List<(string Name, int Value, bool Local)>? _cols;

        public List<(string Name, int Value, bool Local)> Columns()
        {
            if (_cols is not null) return _cols;
            var c = new List<(string, int, bool)>();
            void A(string n, int v, bool local = true) => c.Add((n, v, local));
            void R(string p, Res r)
            {
                A(p + "OnMap", r.OnMap); A(p + "Nearest", r.Nearest); A(p + "NearestSame", r.NearestSame);
                A(p + "N5", r.N5); A(p + "N8", r.N8); A(p + "N25", r.N25); A(p + "N35", r.N35); A(p + "N52", r.N52);
                A(p + "Unc5", r.Unc5); A(p + "Unc16", r.Unc16); A(p + "Unc35", r.Unc35);
                A(p + "Same35", r.Same35); A(p + "Other35", r.Other35); A(p + "Same52", r.Same52); A(p + "Other52", r.Other52);
                A(p + "Walk", r.WalkTiles); A(p + "Sail", r.SailTiles); A(p + "Step", r.Step);
            }
            A("continent", OwnContinent, false);
            R("seam", Seam); R("coal", Coal); R("sugar", Sugar);
            A("coalStock35", CoalStock35); A("coalDeposits", CoalDeposits); A("coalOnSeamTile", CoalOnSeamTile);
            A("coalOnOrganicBasin", CoalOnOrganicBasin); A("seamBarren", SeamBarren);
            A("worldAnySeam35", WorldAnySeam35); A("worldAnySeam52", WorldAnySeam52); A("mateSeam35", MateSeam35); A("mateSeam52", MateSeam52);
            A("mateHex", MateHex); A("mateWalk", MateWalk);
            A("fStartHex", FStartHex); A("fStartWalk", FStartWalk); A("fStartSail", FStartSail);
            A("fLandHex", FLandHex); A("fLandSail", FLandSail); A("islandHex", IslandHex); A("islandSail", IslandSail);
            A("gap", Gap); A("reachDirect", ReachDirect); A("reachIslands", ReachIslands);
            A("legs11", Legs11); A("legs16", Legs16); A("legs22", Legs22);
            return _cols = c;
        }

        public string Line()
        {
            var sb = new StringBuilder();
            sb.Append(Seed).Append(',').Append(Civ);
            foreach ((string _, int v, bool _) in Columns()) sb.Append(',').Append(v.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>The record without its civilization and without route-shape columns: the rotated mirror start must equal it exactly.</summary>
        public string LocalKey()
        {
            var sb = new StringBuilder();
            foreach ((string _, int v, bool local) in Columns())
                if (local) sb.Append(v.ToString(CultureInfo.InvariantCulture)).Append(',');
            return sb.ToString();
        }

        public static string Header(Rec any)
        {
            var sb = new StringBuilder("seed,civ");
            foreach ((string n, int _, bool _) in any.Columns()) sb.Append(',').Append(n);
            return sb.ToString();
        }
    }

    // ---- driver -----------------------------------------------------------------------------------------

    public static int Run(string[] args)
    {
        ulong first = 1;
        int seeds = 10_000;
        int threads = Math.Max(1, Environment.ProcessorCount - 1);
        string? csv = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--first":
                case "--seed": first = ulong.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--seeds": seeds = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--threads": threads = Math.Max(1, int.Parse(Next(args, ref i), CultureInfo.InvariantCulture)); break;
                case "--csv": csv = Next(args, ref i); break;
                default:
                    Console.Error.WriteLine("usage: civsim-harness e6-audit [--first N] [--seeds N] [--threads N] [--csv FILE]");
                    return 2;
            }
        }
        Console.Write(AuditRange(first, seeds, threads, csv, n => Console.Error.WriteLine($"audited {n:N0}/{seeds:N0} seeds")));
        return 0;
    }

    /// <summary>The whole report for a seed range. A pure function of the range: thread count and timing cannot change a byte of it.</summary>
    internal static string AuditRange(ulong first, int seeds, int threads, string? csv = null, Action<int>? progress = null)
    {
        Rec[][] worlds = AuditWorlds(first, seeds, threads, progress);
        var all = new List<Rec>(seeds * 4);
        foreach (Rec[] world in worlds) all.AddRange(world);

        var digestInput = new StringBuilder();
        foreach (Rec r in all) digestInput.Append(r.Line()).Append('\n');
        using var sha = SHA256.Create();
        string digest = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(digestInput.ToString()))).ToLowerInvariant();
        if (csv is not null) File.WriteAllText(csv, Rec.Header(all[0]) + "\n" + digestInput);
        return Report(first, seeds, all, worlds, digest);
    }

    /// <summary>Per-start counters for a seed range (exposed so tests can check counter consistency without parsing the report).</summary>
    internal static IReadOnlyList<IReadOnlyList<(string Name, int Value)>> Counters(ulong first, int seeds, int threads)
    {
        var list = new List<IReadOnlyList<(string, int)>>();
        foreach (Rec[] world in AuditWorlds(first, seeds, threads, null))
            foreach (Rec r in world)
                list.Add(r.Columns().ConvertAll(c => (c.Name, c.Value)));
        return list;
    }

    private static Rec[][] AuditWorlds(ulong first, int seeds, int threads, Action<int>? progress)
    {
        if (seeds < 1) throw new ArgumentOutOfRangeException(nameof(seeds), "Audit count must be positive.");
        _ = checked(first + (ulong)(seeds - 1));
        var worlds = new Rec[seeds][];
        int done = 0;
        Parallel.For(0, seeds, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, threads) }, i =>
        {
            worlds[i] = AuditWorld(first + (ulong)i);
            int n = System.Threading.Interlocked.Increment(ref done);
            if (n % 250 == 0) progress?.Invoke(n);
        });
        return worlds;
    }

    private static string Next(string[] args, ref int i)
    {
        if (++i >= args.Length) throw new ArgumentException($"{args[i - 1]} needs a value.");
        return args[i];
    }

    // ---- per-world measurement --------------------------------------------------------------------------

    private static Rec[] AuditWorld(ulong seed)
    {
        GeneratedWorld world = WorldGenerator.Generate(seed);
        int w = world.Width, h = world.Height;
        WorldTile[] tiles = world.Tiles.ToArray();
        int[] starts = new int[world.Starts.Length];
        for (int c = 0; c < starts.Length; c++) starts[c] = world.Starts[c].Tile;

        var seam = new List<int>();
        var coal = new List<int>();
        var sugar = new List<int>();
        int coalOnSeam = 0, coalOnBasin = 0, seamBarren = 0;
        for (int t = 0; t < tiles.Length; t++)
        {
            WorldTile tile = tiles[t];
            if (!tile.IsLand) continue;
            if (tile.SurfaceEvidence == SurfaceEvidence.DarkSeam)
            {
                seam.Add(t);
                if (tile.Deposit == DepositKind.None) seamBarren++;
            }
            if (tile.Deposit == DepositKind.Coal)
            {
                coal.Add(t);
                if (tile.SurfaceEvidence == SurfaceEvidence.DarkSeam) coalOnSeam++;
                if ((tile.GeologyFlags & GeologyFlags.OrganicBasin) != 0) coalOnBasin++;
            }
            if (tile.Deposit == DepositKind.Sugarcane) sugar.Add(t);
        }

        bool[] passable = HexGrid.BuildWalkingPassability(tiles, w, h);
        int[] landDistance = HexGrid.LandDistances(tiles, w, h);
        (int gap, int reachDirect, int reachIslands, int[] legs) = ContinentGap(tiles, w, h, landDistance);

        var recs = new Rec[starts.Length];
        int[][] walk = new int[starts.Length][];
        int[][] cost = new int[starts.Length][];
        int[][] sail = new int[starts.Length][];
        for (int c = 0; c < starts.Length; c++)
        {
            walk[c] = HexGrid.WalkingDistancesFrom(w, h, passable, starts[c]);
            cost[c] = HexGrid.WalkingCostsFrom(tiles, w, h, starts[c]);
            sail[c] = HexGrid.WalkingCostsFrom(tiles, w, h, starts[c], int.MaxValue, landDistance, OpenSeaReach);
            var r = new Rec { Seed = seed, Civ = c, OwnContinent = tiles[starts[c]].Continent };
            Measure(r.Seam, starts[c], seam, starts, tiles, w, cost[c], sail[c], null);
            Measure(r.Coal, starts[c], coal, starts, tiles, w, cost[c], sail[c], null);
            Measure(r.Sugar, starts[c], sugar, starts, tiles, w, cost[c], sail[c], null);
            int stock = 0;
            foreach (int t in coal)
            {
                int d = HexGrid.Distance(starts[c], t, w);
                if (d <= E6Reach && tiles[t].Continent == tiles[starts[c]].Continent && Uncontested(starts[c], d, t, starts, w)) stock += tiles[t].DepositAmount;
            }
            r.CoalStock35 = stock;
            r.CoalDeposits = coal.Count;
            r.CoalOnSeamTile = coalOnSeam;
            r.CoalOnOrganicBasin = coalOnBasin;
            r.SeamBarren = seamBarren;
            r.Gap = gap; r.ReachDirect = reachDirect; r.ReachIslands = reachIslands; r.Legs11 = legs[0]; r.Legs16 = legs[1]; r.Legs22 = legs[2];
            recs[c] = r;
        }

        bool any35 = false, any52 = false;
        foreach (Rec r in recs) { any35 |= r.SeamSees35; any52 |= r.SeamSees52; }
        for (int c = 0; c < starts.Length; c++)
        {
            Rec r = recs[c];
            r.WorldAnySeam35 = any35 ? 1 : 0;
            r.WorldAnySeam52 = any52 ? 1 : 0;
            int mate = c ^ 1;
            r.MateSeam35 = r.SeamSees35 || recs[mate].SeamSees35 ? 1 : 0;
            r.MateSeam52 = r.SeamSees52 || recs[mate].SeamSees52 ? 1 : 0;
            r.MateHex = HexGrid.Distance(starts[c], starts[mate], w);
            r.MateWalk = walk[c][starts[mate]];
            Overseas(r, c, starts, tiles, w, h, walk[c], sail[c]);
        }
        return recs;
    }

    private static int SailTiles(int cost) => cost == int.MaxValue ? int.MaxValue : (cost + 9) / 10;

    private static void Measure(Res r, int s, List<int> list, int[] starts, WorldTile[] tiles, int w, int[] walkCost, int[] sailCost, object? unused)
    {
        _ = unused;
        r.OnMap = list.Count;
        int own = tiles[s].Continent;
        foreach (int t in list)
        {
            int d = HexGrid.Distance(s, t, w);
            bool same = tiles[t].Continent == own;
            r.Nearest = Math.Min(r.Nearest, d);
            if (same) r.NearestSame = Math.Min(r.NearestSame, d);
            if (d <= ClaimRadius) r.N5++;
            if (d <= MiningBase) r.N8++;
            if (d <= E5Reach) r.N25++;
            if (d <= E6Reach) { r.N35++; if (same) r.Same35++; else r.Other35++; }
            if (d <= E6ReachTrails) { r.N52++; if (same) r.Same52++; else r.Other52++; }
            if (same && Uncontested(s, d, t, starts, w))
            {
                if (d <= ClaimRadius) r.Unc5++;
                if (d <= MiningWater) r.Unc16++;
                if (d <= E6Reach) r.Unc35++;
            }
            if (walkCost[t] != int.MaxValue) r.WalkTiles = Math.Min(r.WalkTiles, SailTiles(walkCost[t]));
            if (sailCost[t] != int.MaxValue) r.SailTiles = Math.Min(r.SailTiles, SailTiles(sailCost[t]));
        }
        // Ladder (own-ground reading, same shape as the E5 audit's): 1 uncontested inside the claim radius 5 | 2 uncontested within 16 | 3 uncontested within the
        // E6 reach 35 | 4 same continent within 35 but only nearer a rival | 5 same continent, beyond 35 | 6 another continent only | 7 none on the map.
        r.Step = r.Unc5 > 0 ? 1 : r.Unc16 > 0 ? 2 : r.Unc35 > 0 ? 3 : r.Same35 > 0 ? 4 : r.NearestSame != Far ? 5 : r.OnMap > 0 ? 6 : 7;
    }

    /// <summary>True when the tile is strictly nearer <paramref name="own"/> than any other start (ties belong to no one).</summary>
    private static bool Uncontested(int own, int distance, int tile, int[] starts, int width)
    {
        foreach (int other in starts)
        {
            if (other == own) continue;
            if (HexGrid.Distance(other, tile, width) <= distance) return false;
        }
        return true;
    }

    private static void Overseas(Rec r, int c, int[] starts, WorldTile[] tiles, int w, int h, int[] walk, int[] sail)
    {
        int s = starts[c];
        int own = tiles[s].Continent;
        for (int o = 0; o < starts.Length; o++)
        {
            if (o == c || tiles[starts[o]].Continent == own) continue;
            int d = HexGrid.Distance(s, starts[o], w);
            if (d < r.FStartHex) r.FStartHex = d;
            r.FStartWalk = Math.Min(r.FStartWalk, walk[starts[o]]);
            r.FStartSail = Math.Min(r.FStartSail, SailTiles(sail[starts[o]]));
        }
        for (int t = 0; t < tiles.Length; t++)
        {
            if (!tiles[t].IsLand) continue;
            int cont = tiles[t].Continent;
            if (cont == own) continue;
            int d = HexGrid.Distance(s, t, w);
            if (cont is 0 or 1)
            {
                r.FLandHex = Math.Min(r.FLandHex, d);
                r.FLandSail = Math.Min(r.FLandSail, SailTiles(sail[t]));
            }
            else
            {
                r.IslandHex = Math.Min(r.IslandHex, d);
                r.IslandSail = Math.Min(r.IslandSail, SailTiles(sail[t]));
            }
        }
    }

    /// <summary>
    /// World-level sea geometry. <c>gap</c> is the least hex distance between a continent-0 and a continent-1 land tile (docs/MVP/21 puts it at 33).
    /// <c>direct</c> is the least open-sea reach (distance from land) that a single leg needs to link the two continents with islands unusable;
    /// <c>islands</c> is the same with islands as stepping stones (the existing rule). A route needs the maximum land-distance over the water tiles it enters.
    /// </summary>
    private static (int Gap, int Direct, int Islands, int[] Legs) ContinentGap(WorldTile[] tiles, int w, int h, int[] landDistance)
    {
        int gap = Far;
        // Multi-source hex distance from continent 0 land to everything, then the least value on continent 1 land.
        int[] dist = new int[tiles.Length];
        Array.Fill(dist, int.MaxValue);
        var queue = new int[tiles.Length];
        int head = 0, tail = 0;
        for (int t = 0; t < tiles.Length; t++)
            if (tiles[t].IsLand && tiles[t].Continent == 0) { dist[t] = 0; queue[tail++] = t; }
        Span<int> nb = stackalloc int[6];
        while (head < tail)
        {
            int t = queue[head++];
            int n = HexGrid.Neighbors(t, w, h, nb);
            for (int i = 0; i < n; i++)
            {
                int x = nb[i];
                if (dist[x] != int.MaxValue) continue;
                dist[x] = dist[t] + 1;
                queue[tail++] = x;
            }
        }
        for (int t = 0; t < tiles.Length; t++)
            if (tiles[t].IsLand && tiles[t].Continent == 1 && dist[t] < gap) gap = dist[t];
        // Direct crossing: distance from land measured to the two continents only, so islands cannot lend water tiles to the reach.
        int[] mainDistance = LandDistanceOf(tiles, w, h, t => tiles[t].IsLand && tiles[t].Continent is 0 or 1);
        return (gap, MinimaxReach(tiles, w, h, mainDistance, false), MinimaxReach(tiles, w, h, landDistance, true), HopLegs(tiles, w, h));
    }

    private static int[] LandDistanceOf(WorldTile[] tiles, int w, int h, Func<int, bool> source)
    {
        int[] dist = new int[tiles.Length];
        Array.Fill(dist, int.MaxValue);
        var queue = new int[tiles.Length];
        int head = 0, tail = 0;
        for (int t = 0; t < tiles.Length; t++)
            if (source(t)) { dist[t] = 0; queue[tail++] = t; }
        Span<int> nb = stackalloc int[6];
        while (head < tail)
        {
            int t = queue[head++];
            int n = HexGrid.Neighbors(t, w, h, nb);
            for (int i = 0; i < n; i++)
            {
                int x = nb[i];
                if (dist[x] != int.MaxValue) continue;
                dist[x] = dist[t] + 1;
                queue[tail++] = x;
            }
        }
        return dist;
    }

    /// <summary>
    /// Legs of the continent-0 to continent-1 crossing under a boat reach R. Landmasses are the two continents (each one node, even if a strait
    /// splits it) and every other connected piece of land (an island). A leg is one hop between two landmasses A and B in which every water tile
    /// entered lies within R tiles of A or of B (so a hop never leans on a third coast). Returns the fewest legs at R = 11 (the existing rule),
    /// 16 and 22, or -1 when the continents are not connected at that reach.
    /// </summary>
    private static int[] HopLegs(WorldTile[] tiles, int w, int h)
    {
        int[] node = new int[tiles.Length];
        Array.Fill(node, -1);
        var lists = new List<List<int>> { new(), new() };
        var stack = new Stack<int>();
        Span<int> nb = stackalloc int[6];
        for (int t = 0; t < tiles.Length; t++)
        {
            if (!tiles[t].IsLand) continue;
            int cont = tiles[t].Continent;
            if (cont is 0 or 1) { node[t] = cont; lists[cont].Add(t); continue; }
        }
        for (int t = 0; t < tiles.Length; t++)
        {
            if (!tiles[t].IsLand || node[t] >= 0) continue;
            int id = lists.Count;
            lists.Add(new List<int>());
            node[t] = id; stack.Push(t);
            while (stack.Count > 0)
            {
                int u = stack.Pop();
                lists[id].Add(u);
                int n = HexGrid.Neighbors(u, w, h, nb);
                for (int i = 0; i < n; i++)
                {
                    int x = nb[i];
                    if (tiles[x].IsLand && tiles[x].Continent is not (0 or 1) && node[x] < 0) { node[x] = id; stack.Push(x); }
                }
            }
        }
        int count = lists.Count;
        int[][] field = new int[count][];
        for (int a = 0; a < count; a++) { var members = lists[a]; field[a] = LandDistanceOf(tiles, w, h, t => node[t] == a); }
        // Least hop reach per pair of landmasses within 24 tiles (a hop above that is never useful here).
        const int Cap = 24;
        int[,] hopReach = new int[count, count];
        var seen = new int[tiles.Length];
        int stamp = 0;
        var queue = new Queue<int>();
        for (int a = 0; a < count; a++)
            for (int b = a + 1; b < count; b++)
            {
                hopReach[a, b] = hopReach[b, a] = Far;
                var small = lists[a].Count <= lists[b].Count ? a : b;
                int other = small == a ? b : a;
                int gap = int.MaxValue;
                foreach (int t in lists[small]) gap = Math.Min(gap, field[other][t]);
                if (gap > 2 * Cap + 1) continue;
                for (int reach = Math.Max(1, gap / 2); reach <= Cap; reach++)
                {
                    stamp++;
                    queue.Clear();
                    bool ok = false;
                    foreach (int t in lists[a])
                    {
                        int n = HexGrid.Neighbors(t, w, h, nb);
                        for (int i = 0; i < n; i++)
                        {
                            int x = nb[i];
                            if (tiles[x].IsLand || seen[x] == stamp || Math.Min(field[a][x], field[b][x]) > reach) continue;
                            seen[x] = stamp; queue.Enqueue(x);
                        }
                    }
                    while (queue.Count > 0 && !ok)
                    {
                        int u = queue.Dequeue();
                        int n = HexGrid.Neighbors(u, w, h, nb);
                        for (int i = 0; i < n; i++)
                        {
                            int x = nb[i];
                            if (tiles[x].IsLand) { if (node[x] == b) { ok = true; break; } continue; }
                            if (seen[x] == stamp || Math.Min(field[a][x], field[b][x]) > reach) continue;
                            seen[x] = stamp; queue.Enqueue(x);
                        }
                    }
                    if (ok) { hopReach[a, b] = hopReach[b, a] = reach; break; }
                }
            }
        // Fewest legs from continent 0 to continent 1 using only hops whose reach is within R.
        var result = new int[3];
        int[] limits = { OpenSeaReach, 16, 22 };
        for (int k = 0; k < 3; k++)
        {
            int[] legs = new int[count];
            Array.Fill(legs, -1);
            legs[0] = 0;
            var bfs = new Queue<int>();
            bfs.Enqueue(0);
            while (bfs.Count > 0)
            {
                int a = bfs.Dequeue();
                for (int b = 0; b < count; b++)
                    if (b != a && legs[b] < 0 && hopReach[a, b] <= limits[k]) { legs[b] = legs[a] + 1; bfs.Enqueue(b); }
            }
            result[k] = legs[1];
        }
        return result;
    }

    private static int MinimaxReach(WorldTile[] tiles, int w, int h, int[] landDistance, bool islandsAllowed)
    {
        int[] best = new int[tiles.Length];
        Array.Fill(best, int.MaxValue);
        var pq = new PriorityQueue<int, int>();
        for (int t = 0; t < tiles.Length; t++)
            if (tiles[t].IsLand && tiles[t].Continent == 0) { best[t] = 0; pq.Enqueue(t, 0); }
        Span<int> nb = stackalloc int[6];
        while (pq.TryDequeue(out int tile, out int b))
        {
            if (b != best[tile]) continue;
            if (tiles[tile].IsLand && tiles[tile].Continent == 1) return b;
            int n = HexGrid.Neighbors(tile, w, h, nb);
            for (int i = 0; i < n; i++)
            {
                int x = nb[i];
                int nbBest;
                if (tiles[x].IsLand)
                {
                    int cont = tiles[x].Continent;
                    if (cont is not (0 or 1) && !islandsAllowed) continue;
                    nbBest = b;
                }
                else nbBest = Math.Max(b, landDistance[x]);
                if (nbBest >= best[x]) continue;
                best[x] = nbBest;
                pq.Enqueue(x, nbBest);
            }
        }
        return Far;
    }

    // ---- report -----------------------------------------------------------------------------------------

    private static string Pct(int n, int d)
    {
        if (d == 0) return "n/a";
        long tenths = (n * 1000L + d / 2) / d;
        return $"{tenths / 10}.{tenths % 10}%";
    }

    private static T Percentile<T>(List<T> sorted, int percent) where T : IComparable<T>
        => sorted.Count == 0 ? default! : sorted[Math.Min(sorted.Count - 1, (int)((long)(sorted.Count - 1) * percent / 100))];

    /// <summary>Distribution of the finite values; values of 999 or more (and Int32.MaxValue) mean "none" and are counted separately.</summary>
    private static string Dist(IEnumerable<int> values)
    {
        var list = new List<int>();
        int none = 0;
        foreach (int v in values) if (v >= Far) none++; else list.Add(v);
        list.Sort();
        string tail = none > 0 ? $"; none {none:N0}" : "";
        if (list.Count == 0) return $"n/a{tail}";
        return $"min {list[0]} p5 {Percentile(list, 5)} p25 {Percentile(list, 25)} p50 {Percentile(list, 50)} " +
               $"p75 {Percentile(list, 75)} p95 {Percentile(list, 95)} p99 {Percentile(list, 99)} max {list[^1]} (n={list.Count:N0}{tail})";
    }

    private static IEnumerable<int> Each(Rec[][] worlds, Func<Rec[], int> pick)
    {
        foreach (Rec[] w in worlds) yield return pick(w);
    }

    private static string Cdf(List<Rec> group, Func<Rec, int> distance, params int[] thresholds)
    {
        var sb = new StringBuilder();
        foreach (int t in thresholds)
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append($"<= {t}: {Pct(group.FindAll(r => distance(r) <= t).Count, group.Count)}");
        }
        return sb.ToString();
    }

    private static void StepLine(StringBuilder o, string title, List<Rec> group, Func<Rec, int> step, string labels)
    {
        int n = group.Count;
        var counts = new int[8];
        foreach (Rec r in group) counts[step(r)]++;
        o.Append($"  {title} (n={n:N0}): ").Append(labels.Replace("{", "").Replace("}", "")).Append('\n');
        var sb = new StringBuilder("     ");
        for (int k = 1; k <= 7; k++) sb.Append($"{k}: {Pct(counts[k], n)}{(k < 7 ? " | " : "")}");
        o.Append(sb).Append('\n');
    }

    private static string Report(ulong first, int seeds, List<Rec> all, Rec[][] worlds, string digest)
    {
        var o = new StringBuilder();
        void L(string text = "") => o.Append(text).Append('\n');
        string I(int v) => v.ToString("N0", CultureInfo.InvariantCulture);
        string P(Func<Rec, bool> pick, List<Rec>? group = null) { var g = group ?? all; int k = g.FindAll(x => pick(x)).Count; return $"{Pct(k, g.Count)} ({I(k)}/{I(g.Count)})"; }
        const string StepLabels = "steps: 1 uncontested within claim radius 5 | 2 uncontested within 16 | 3 uncontested within the E6 reach 35 | 4 same continent within 35 but only nearer a rival | 5 same continent beyond 35 | 6 another continent only | 7 none on the map";

        L($"e6-audit seeds {first}..{first + (ulong)seeds - 1} ({I(seeds)} worlds, {I(all.Count)} starts)");
        L($"digest {digest}");
        int localMismatch = 0;
        foreach (Rec[] w in worlds)
            for (int c = 0; c < 2; c++)
                if (w[c].LocalKey() != w[c + 2].LocalKey()) localMismatch++;
        L($"mirror check: local columns of civ c and c+2 differ in {I(localMismatch)} of {I(seeds * 2)} pairs");
        L();

        var distinct = all.FindAll(r => r.Civ < 2);

        L("== 1. Denominators and definitions ==");
        L($"worlds {I(seeds)}; starts {I(all.Count)}; geometrically distinct starts {I(distinct.Count)} (civ 2 and 3 mirror civ 0 and 1).");
        L("Static starts only: each start is a lone settlement at its start tile; no expansion, no rivals' growth, no later daughter villages.");
        L($"seam = a land tile whose SurfaceEvidence is DarkSeam: this, and not a coal deposit, is what the sim's [Seen: Coal] reads (LivingEconomySource). Distances are hex distances ignoring terrain and water, as the sim measures them.");
        L($"coal deposit = a tile whose Deposit is Coal (what a mine, and the explorers' CommitReveal path, use). sugarcane = a tile whose Deposit is Sugarcane.");
        L($"reach: E6 routine reach {E6Reach} (eras.yaml); with WAY-01 trails x1.5 = {E6ReachTrails}; claim radius {ClaimRadius}; one fission hop adds {FissionHop}. uncontested = same continent and strictly nearer this start than any other start.");
        L("walking cost = least land cost in tiles (tenths/10 rounded up, biome costs of 12 §4; a one-tile strait is crossable). sail = the same with the existing outrigger rule (open water enterable within 11 tiles of land at 0.4 tile per tile).");
        L();

        L("== 2. World truth per world ==");
        L($"dark-seam tiles on the map: {Dist(Each(worlds, w => w[0].Seam.OnMap))}");
        L($"  of which barren (no deposit on the tile): {Dist(Each(worlds, w => w[0].SeamBarren))}");
        L($"coal deposit tiles on the map: {Dist(Each(worlds, w => w[0].CoalDeposits))}");
        L($"  coal deposits that carry a dark-seam sign: {Dist(Each(worlds, w => w[0].CoalOnSeamTile))}; that lie on an organic-basin tile: {Dist(Each(worlds, w => w[0].CoalOnOrganicBasin))}");
        L($"sugarcane tiles on the map: {Dist(Each(worlds, w => w[0].Sugar.OnMap))}");
        L($"worlds with no seam at all: {Pct(Each(worlds, w => w[0].Seam.OnMap).Where0(), seeds)}; with no coal deposit: {Pct(Each(worlds, w => w[0].CoalDeposits).Where0(), seeds)}; with no sugarcane: {Pct(Each(worlds, w => w[0].Sugar.OnMap).Where0(), seeds)}");
        L();

        L("== 3. COAL, the gate: can [Seen: Coal] (MIN-09, on the KEY-7 critical path) be satisfied? ==");
        L("The gate is met when a seam lies within the era reach of any settlement. At game start the only settlement is the start tile; the first line is the strict reading, the second adds WAY-01.");
        L($"nearest seam, any continent (hex): {Dist(all.ConvertAll(r => r.Seam.Nearest))}");
        L($"nearest seam, own continent (hex): {Dist(all.ConvertAll(r => r.Seam.NearestSame))}");
        L($"cumulative share of starts with a seam within: {Cdf(all, r => r.Seam.Nearest, 5, 8, 16, 25, 30, 35, 49, 52, 66, 80)}");
        L($"seam within {E6Reach} (E6 reach): {P(r => r.SeamSees35)};   within {E6ReachTrails} (with WAY-01): {P(r => r.SeamSees52)}");
        L($"  of those within {E6Reach}, the seam is on another continent only (seen across the sea): {P(r => r.Seam.N35 > 0 && r.Seam.Same35 == 0)}");
        L($"seam within {E6Reach} on the start's own continent: {P(r => r.Seam.Same35 > 0)}; within {E6ReachTrails}: {P(r => r.Seam.Same52 > 0)}");
        L($"** NO seam within {E6Reach} of the start tile (KEY-7 not reachable from the start alone, strict): {P(r => !r.SeamSees35)}");
        L($"** NO seam within {E6ReachTrails} of the start tile (with WAY-01): {P(r => !r.SeamSees52)}");
        L($"   among the starts with none within {E6Reach}: the mate (same continent, the other start of the pair) sees one within {E6Reach}: {P(r => r.MateSeam35 == 1, all.FindAll(x => !x.SeamSees35))}; any start in the world sees one within {E6Reach}: {P(r => r.WorldAnySeam35 == 1, all.FindAll(x => !x.SeamSees35))}");
        L($"   among the starts with none within {E6ReachTrails}: the mate sees one: {P(r => r.MateSeam52 == 1, all.FindAll(x => !x.SeamSees52))}; any start in the world sees one: {P(r => r.WorldAnySeam52 == 1, all.FindAll(x => !x.SeamSees52))}");
        L($"worlds in which NO start sees a seam within {E6Reach}: {Pct(Each(worlds, w => w[0].WorldAnySeam35 == 0 ? 1 : 0).Sum(), seeds)}; within {E6ReachTrails}: {Pct(Each(worlds, w => w[0].WorldAnySeam52 == 0 ? 1 : 0).Sum(), seeds)}");
        L($"distinct starts with none within {E6Reach}: {P(r => !r.SeamSees35, distinct)}; with none within {E6ReachTrails}: {P(r => !r.SeamSees52, distinct)}");
        L($"seam sign within {ClaimRadius} (inside own claim): {P(r => r.Seam.N5 > 0)}; within 25 (E5 reach): {P(r => r.Seam.N25 > 0)}");
        var seamWorlds = all.FindAll(r => r.Seam.OnMap > 0);
        L($"starts in worlds that have at least one seam (n={I(seamWorlds.Count)}): seam within {E6Reach}: {P(r => r.SeamSees35, seamWorlds)}; within {E6ReachTrails}: {P(r => r.SeamSees52, seamWorlds)}; none within {E6ReachTrails}: {P(r => !r.SeamSees52, seamWorlds)}");
        L($"starts in worlds with NO seam at all (n={I(all.Count - seamWorlds.Count)}): the [Seen: Coal] gate can never open from settlements there, whatever the reach.");
        L($"the explorers' path (Expedition decree, needs WAY-04, range 2 x reach = {2 * E6Reach}; it marks Coal when it crosses a coal DEPOSIT tile): a coal deposit lies within {2 * E6Reach} of {P(r => r.Coal.Nearest <= 2 * E6Reach)} of starts, and within {E6Reach} of {P(r => r.Coal.Nearest <= E6Reach)}.");
        L();

        L("== 4. COAL, the deposit: can a mine be opened and fed? (WP-E6-5; today's MineCheck does not yet list coal) ==");
        StepLine(o, "all starts", all, r => r.Coal.Step, StepLabels);
        StepLine(o, "distinct starts", distinct, r => r.Coal.Step, StepLabels);
        L($"nearest coal deposit, any continent (hex): {Dist(all.ConvertAll(r => r.Coal.Nearest))}");
        L($"nearest coal deposit on the start's own continent (hex): {Dist(all.ConvertAll(r => r.Coal.NearestSame))}");
        L($"nearest coal deposit by least walking cost, in tiles (land and straits only): {Dist(all.ConvertAll(r => r.Coal.WalkTiles == int.MaxValue ? Far : r.Coal.WalkTiles))}");
        L($"nearest coal deposit by least cost with the outrigger rule, in tiles: {Dist(all.ConvertAll(r => r.Coal.SailTiles == int.MaxValue ? Far : r.Coal.SailTiles))}");
        L($"cumulative share of starts with a coal deposit (any owner, any continent) within: {Cdf(all, r => r.Coal.Nearest, 5, 8, 16, 25, 35, 52)}");
        L($"cumulative share of starts with an UNCONTESTED own-continent coal deposit within: 5: {P(r => r.Coal.Unc5 > 0)} | 16: {P(r => r.Coal.Unc16 > 0)} | 35: {P(r => r.Coal.Unc35 > 0)}");
        L($"coal deposits within {E6Reach}, uncontested and on own continent: {Dist(all.ConvertAll(r => r.Coal.Unc35))}");
        L($"summed deposit quantity (1-3 per tile) of those: {Dist(all.ConvertAll(r => r.CoalStock35))}");
        L($"starts whose own continent has NO coal deposit at all: {P(r => r.Coal.NearestSame == Far)}");
        L($"starts whose nearest coal is on another continent only (needs a sea crossing): {P(r => r.Coal.NearestSame == Far && r.Coal.OnMap > 0)}");
        L($"starts with no coal deposit anywhere on the map: {P(r => r.Coal.OnMap == 0)}");
        L($"starts with no WALKING route to any coal deposit (land and straits): {P(r => r.Coal.WalkTiles == int.MaxValue)}; of those, one is reachable by the outrigger rule: {P(r => r.Coal.SailTiles != int.MaxValue, all.FindAll(x => x.Coal.WalkTiles == int.MaxValue))}");
        L($"starts with a seam within {E6Reach} but no uncontested own-continent coal within {E6Reach} (the gate opens, the mine is out of reach): {P(r => r.SeamSees35 && r.Coal.Unc35 == 0)}");
        L($"starts that see a seam within {E6Reach} while their nearest coal deposit is farther than {E6Reach}: {P(r => r.SeamSees35 && r.Coal.Nearest > E6Reach)}");
        L();

        L("== 5. What MIN-08 (E5 mine surveying, kind: reveal) would have to reveal ==");
        L("MIN-08's reveal effect is the prose line \"Ore veins within the civilization's own territory become known\" (kind: reveal, no radius, no typed value). The sim only classifies the kind; it reads no MIN-08 reveal, so today MIN-08 reveals nothing and is not wired to [Seen: Coal].");
        L($"If 'own territory' means the claim radius {ClaimRadius}: a coal deposit becomes known for {P(r => r.Coal.N5 > 0)} of starts; a seam for {P(r => r.Seam.N5 > 0)}.");
        L("Gate satisfied if a seam is within the era reach OR a MIN-08 reveal of radius R names a coal deposit (share of all starts):");
        foreach (int reveal in new[] { 5, 8, 16, 25, 35 })
            L($"  reveal radius {reveal}: seam within {E6Reach} or coal within {reveal}: {P(r => r.SeamSees35 || r.Coal.Nearest <= reveal)} | seam within {E6ReachTrails} or coal within {reveal}: {P(r => r.SeamSees52 || r.Coal.Nearest <= reveal)} | coal within {reveal} alone: {P(r => r.Coal.Nearest <= reveal)}");
        L("Radius a reveal would need so that this share of starts learns of a coal deposit (any continent, from the start tile): ");
        {
            var d = all.ConvertAll(r => r.Coal.Nearest);
            d.Sort();
            L($"  50% {Percentile(d, 50)} | 75% {Percentile(d, 75)} | 90% {Percentile(d, 90)} | 95% {Percentile(d, 95)} | 99% {Percentile(d, 99)} | 100% {d[^1]} (hex, nearest deposit on the map)");
            var s = all.ConvertAll(r => r.Coal.NearestSame);
            s.Sort();
            L($"  own continent only: 50% {Percentile(s, 50)} | 90% {Percentile(s, 90)} | 99% {Percentile(s, 99)} | 100% {s[^1]} ({I(all.FindAll(r => r.Coal.NearestSame == Far).Count)} starts have none)");
        }
        L();

        L("== 6. SUGARCANE (FOD-10 [Has: Sugarcane]; the sim currently decides only Tin and Arsenic ore as [Has] gates, so this one is open for every civilization) ==");
        StepLine(o, "all starts", all, r => r.Sugar.Step, StepLabels);
        L($"nearest sugarcane tile (hex): {Dist(all.ConvertAll(r => r.Sugar.Nearest))}");
        L($"cumulative share of starts with a sugarcane tile (any owner) within: {Cdf(all, r => r.Sugar.Nearest, 5, 8, 16, 25, 35, 52)}");
        L($"sugarcane in the claim radius {ClaimRadius}: {P(r => r.Sugar.N5 > 0)}; in E6 reach {E6Reach}: {P(r => r.Sugar.N35 > 0)} (own continent {P(r => r.Sugar.Same35 > 0)}; another continent only {P(r => r.Sugar.N35 > 0 && r.Sugar.Same35 == 0)})");
        L($"sugarcane on the start's own continent anywhere: {P(r => r.Sugar.NearestSame != Far)}; only overseas: {P(r => r.Sugar.NearestSame == Far && r.Sugar.OnMap > 0)}; none on the map: {P(r => r.Sugar.OnMap == 0)}");
        L($"uncontested own-continent sugarcane within {E6Reach}: {P(r => r.Sugar.Unc35 > 0)}");
        L($"nearest sugarcane by least walking cost, in tiles: {Dist(all.ConvertAll(r => r.Sugar.WalkTiles == int.MaxValue ? Far : r.Sugar.WalkTiles))}");
        L($"worlds in which no start has uncontested own-continent sugarcane within {E6Reach}: {Pct(Each(worlds, w => w.All(r => r.Sugar.Unc35 == 0) ? 1 : 0).Sum(), seeds)}");
        L();

        L("== 7. Overseas: foreign starts, foreign land and the existing outrigger rule (OpenSeaReach 11, islands as stepping stones) ==");
        L($"continent gap per world (least hex distance between continent 0 and 1 land): {Dist(Each(worlds, w => w[0].Gap))}");
        L($"least open-sea reach for ONE leg between the continents, islands unusable: {Dist(Each(worlds, w => w[0].ReachDirect))}");
        L($"least reach for a continuous sail with islands as stepping stones (the existing rule; needs <= {OpenSeaReach}): {Dist(Each(worlds, w => w[0].ReachIslands))}");
        L($"worlds crossable with the existing reach {OpenSeaReach}: {Pct(Each(worlds, w => w[0].ReachIslands <= OpenSeaReach ? 1 : 0).Sum(), seeds)}; with a one-leg reach of 16: {Pct(Each(worlds, w => w[0].ReachDirect <= 16 ? 1 : 0).Sum(), seeds)}; 20: {Pct(Each(worlds, w => w[0].ReachDirect <= 20 ? 1 : 0).Sum(), seeds)}; 22: {Pct(Each(worlds, w => w[0].ReachDirect <= 22 ? 1 : 0).Sum(), seeds)}");
        L($"mate start (same continent) hex distance: {Dist(all.ConvertAll(r => r.MateHex))}; walking steps: {Dist(all.ConvertAll(r => r.MateWalk == int.MaxValue ? Far : r.MateWalk))}");
        L($"nearest foreign start (other continent), hex: {Dist(all.ConvertAll(r => r.FStartHex))}");
        L($"nearest foreign start by walking steps: {P(r => r.FStartWalk != int.MaxValue)} of starts can walk to one (land and one-tile straits)");
        L($"nearest foreign start by outrigger cost, in tiles: {Dist(all.ConvertAll(r => r.FStartSail == int.MaxValue ? Far : r.FStartSail))}");
        L($"  seasons at {TilesPerSeason} tiles a season: {Dist(all.ConvertAll(r => r.FStartSail == int.MaxValue ? Far : (r.FStartSail + TilesPerSeason - 1) / TilesPerSeason))}");
        L($"starts for which a foreign start is reachable by the existing rule: {P(r => r.FStartSail != int.MaxValue)}");
        L($"legs from one continent to the other (a leg = a hop between two landmasses, every water tile within the reach of one of the two), fewest legs at reach {OpenSeaReach} (the existing rule): {LegHistogram(worlds, w => w[0].Legs11)}");
        L($"  at reach 16: {LegHistogram(worlds, w => w[0].Legs16)}; at reach 22: {LegHistogram(worlds, w => w[0].Legs22)}  (-1 legs = not connected at that reach)");
        L($"nearest tile of the other continent, hex: {Dist(all.ConvertAll(r => r.FLandHex))}; by outrigger cost in tiles: {Dist(all.ConvertAll(r => r.FLandSail == int.MaxValue ? Far : r.FLandSail))}");
        L($"nearest island tile, hex: {Dist(all.ConvertAll(r => r.IslandHex))}; by outrigger cost in tiles: {Dist(all.ConvertAll(r => r.IslandSail == int.MaxValue ? Far : r.IslandSail))}");
        L($"foreign land within the E6 reach {E6Reach} by hex distance: {P(r => r.FLandHex <= E6Reach)}; within {E6ReachTrails}: {P(r => r.FLandHex <= E6ReachTrails)}");
        L();

        L("== 8. Limits ==");
        L("Static starts: expansion adds settlements, and the sim measures [Seen] from every settlement, so real exposure is at least what is reported here and grows with the realm (one fission hop adds up to 14 tiles).");
        L("A seam is evidence, not a deposit: the generator places guaranteed seams on barren host-rock tiles near the start pair and never on coal, so a seen seam does not promise a coal tile nearby. Section 4 reports the deposit geography separately.");
        L("Era reach counts the era at which MIN-09 is researched (E6, 35). The Seen mask is cumulative, but E6 has the largest reach before it, so it dominates. WAY-01 (E0) is assumed done by E6, so the 52 row is the realistic one.");
        L("Contact and barter can also set the gate (Coal is a traded good; a contact that finished MIN-09 makes it known), and explorers set it when they cross a coal deposit tile; neither is modeled here beyond the mate and world rows of section 3.");
        L("All four starts per world are two geometric configurations (civ 2 and 3 mirror civ 0 and 1), so 'per world' figures rest on 20,000 distinct starts, not 40,000 independent ones.");
        return o.ToString();
    }

    private static string LegHistogram(Rec[][] worlds, Func<Rec[], int> legs)
    {
        var counts = new SortedDictionary<int, int>();
        foreach (Rec[] w in worlds) { int k = legs(w); counts[k] = counts.GetValueOrDefault(k) + 1; }
        var sb = new StringBuilder();
        foreach ((int n, int worldsWith) in counts)
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append($"{n} legs {Pct(worldsWith, worlds.Length)}");
        }
        return sb.Length == 0 ? "n/a" : sb.ToString();
    }
}
