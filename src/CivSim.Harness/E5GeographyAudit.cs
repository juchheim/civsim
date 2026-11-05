using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CivSim.Sim.Economy;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>
/// <c>e5-audit</c> (docs/MVP/27 §7, WP-E5-3; M12): a read-only audit of the existing world generator for the three places Faith &amp; Fief
/// leans on geography: saltpeter and sulfur for gunpowder (CHM-07 has a [Has: Saltpeter] gate), river sites for water wheels and sawmills
/// (NRG-03, WOD-09), and wild horses for the horse collar, horseshoes and heavy cavalry (NRG-04, TRN-09, WAR-10 via HUS-07's [Seen: Wild horses]).
/// It generates worlds with <see cref="WorldGenerator"/> and never writes them: the worldgen output, fingerprints and golden are untouched.
/// Definitions are fixed here before any result is read.
/// <code>
/// civsim-harness e5-audit [--seed N] [--seeds N] [--threads N] [--csv FILE]
/// </code>
/// The report is a function of the seed range alone (no clocks, no thread-order effects); <c>digest</c> hashes every per-start record.
/// </summary>
internal static class E5GeographyAudit
{
    // ---- fixed definitions ------------------------------------------------------------------------------

    /// <summary>Mining reach: 8 base, 12 with TRN-05 carts, 16 with SEA-05 on a river, coast or lake site (same as the iron audit).</summary>
    private const int ReachBase = 8, ReachCarts = 12, ReachWater = 16;
    /// <summary>E5 routine reach (content/registries/eras.yaml): how far people range from a settlement, and so how far a camp or a scout sees.</summary>
    private const int E5Reach = 25;
    /// <summary>LivingEconomyModel.Radius (a settlement's own range) and the E5 claim radius.</summary>
    private const int SettlementRange = 3, ClaimRadius = 5;
    /// <summary>Horse reach tiers: E3 reach 15, E4 reach 20, E5 reach 25.</summary>
    private const int HorseNear = 15, HorseMid = 20;
    /// <summary>The economy's forest test (a tile holding at least 15 L of standing wood) and the wood radius a sawmill's town draws on.</summary>
    private const int SawmillWoodReach = 8;
    private const int OpenSeaReach = 11, TilesPerSeason = 10;

    private enum Step
    {
        /// <summary>Uncontested depth-1 deposit within the base mining reach of 8.</summary>
        StrictLocal = 1,
        /// <summary>Uncontested depth-1 deposit within 12 (TRN-05) or 16 (SEA-05 on a wet start).</summary>
        CartsOrWater = 2,
        /// <summary>Uncontested depth-2 deposit within the extended reach (MIN-04 and MIN-07 are known by E5).</summary>
        Deep = 3,
        /// <summary>Only deposits nearer a rival start within the extended reach.</summary>
        Contested = 4,
        /// <summary>Within the E5 routine reach of 25 but beyond every mining reach.</summary>
        BeyondReach = 5,
        /// <summary>Nothing within 25.</summary>
        None = 6,
    }

    private sealed class Rec
    {
        public ulong Seed;
        public int Civ;
        public bool WetSite;
        public int Star;
        // saltpeter and sulfur
        public int SaltpeterOnMap, SulfurOnMap;
        public int SaltpeterNearest = -1, SulfurNearest = -1;
        public int SaltpeterUnc5, SaltpeterUnc8, SaltpeterUncStar, SaltpeterUnc25;
        public int SulfurUncStar;
        public Step SaltpeterStep, SulfurStep;
        public int SaltpeterStock;
        // rivers and lakes
        public int RiverNearest = 99, RiverTiles3, RiverTiles5, RiverTiles8, ForestNearRiver;
        public bool SiteOnRiver;
        // horses
        public int HorseHerdsOnMap, HorseNearest = 99, HorseHerdsUnc15, HorseHerdsUnc20, HorseHerdsUnc25, HorseAnimalsUnc25, HorseHerdsAny25;
        public int CoastDist = 99;
        // trade geometry (saltpeter, horses)
        public bool SameContinentMate;
        public bool MateHasSaltpeter, OtherHasSaltpeter, MateHasHorses, OtherHasHorses;
        public int SaltMateWalk = int.MaxValue, SaltMateSail = int.MaxValue, SaltOtherWalk = int.MaxValue, SaltOtherSail = int.MaxValue;
        public int HorseMateWalk = int.MaxValue, HorseMateSail = int.MaxValue, HorseOtherWalk = int.MaxValue, HorseOtherSail = int.MaxValue;

        public bool PowderLocal => SaltpeterStep <= Step.Deep && SulfurStep <= Step.Deep;
        public bool SaltpeterLocal => SaltpeterStep <= Step.Deep;
        public bool RiverSite => RiverTiles5 > 0;
        public bool HorsesLocal => HorseHerdsUnc25 > 0;
        public int KitCount => (SaltpeterLocal ? 1 : 0) + (RiverSite ? 1 : 0) + (HorsesLocal ? 1 : 0);

        public string Line()
        {
            var sb = new StringBuilder();
            sb.Append(Seed).Append(',').Append(Civ);
            foreach (int v in new[]
            {
                WetSite ? 1 : 0, Star, SaltpeterOnMap, SulfurOnMap, SaltpeterNearest, SulfurNearest, SaltpeterUnc5, SaltpeterUnc8, SaltpeterUncStar,
                SaltpeterUnc25, SulfurUncStar, (int)SaltpeterStep, (int)SulfurStep, SaltpeterStock, RiverNearest, RiverTiles3, RiverTiles5, RiverTiles8,
                ForestNearRiver, SiteOnRiver ? 1 : 0, HorseHerdsOnMap, HorseNearest, HorseHerdsUnc15, HorseHerdsUnc20, HorseHerdsUnc25, HorseAnimalsUnc25,
                HorseHerdsAny25, CoastDist, SameContinentMate ? 1 : 0, MateHasSaltpeter ? 1 : 0, OtherHasSaltpeter ? 1 : 0, MateHasHorses ? 1 : 0,
                OtherHasHorses ? 1 : 0, SaltMateWalk, SaltMateSail, SaltOtherWalk, SaltOtherSail, HorseMateWalk, HorseMateSail, HorseOtherWalk, HorseOtherSail,
            })
                sb.Append(',').Append(v.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>The record without its civilization: the rotated mirror start must equal its local columns exactly.</summary>
        public string LocalKey()
        {
            string line = Line();
            int first = line.IndexOf(',');
            int second = line.IndexOf(',', first + 1);
            string rest = line[(second + 1)..];
            // keep the local columns only: everything up to and including SameContinentMate is local (trade columns follow it)
            int cut = IndexOfComma(rest, 28);
            return rest[..cut];
        }

        private static int IndexOfComma(string s, int n)
        {
            int idx = -1;
            for (int i = 0; i < n; i++) idx = s.IndexOf(',', idx + 1);
            return idx;
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
                case "--seed": first = ulong.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--seeds": seeds = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--threads": threads = Math.Max(1, int.Parse(Next(args, ref i), CultureInfo.InvariantCulture)); break;
                case "--csv": csv = Next(args, ref i); break;
                default:
                    Console.Error.WriteLine("usage: civsim-harness e5-audit [--seed N] [--seeds N] [--threads N] [--csv FILE]");
                    return 2;
            }
        }
        Console.Write(AuditRange(first, seeds, threads, csv, n => Console.Error.WriteLine($"audited {n:N0}/{seeds:N0} seeds")));
        return 0;
    }

    /// <summary>The whole report for a seed range. A pure function of the range: thread count and timing cannot change a byte of it.</summary>
    internal static string AuditRange(ulong first, int seeds, int threads, string? csv = null, Action<int>? progress = null)
    {
        if (seeds < 1) throw new ArgumentOutOfRangeException(nameof(seeds), "Audit count must be positive.");
        _ = checked(first + (ulong)(seeds - 1));

        var worlds = new Rec[seeds][];
        int done = 0;
        Parallel.For(0, seeds, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, threads) }, i =>
        {
            worlds[i] = AuditWorld(first + (ulong)i);
            int n = System.Threading.Interlocked.Increment(ref done);
            if (n % 500 == 0) progress?.Invoke(n);
        });

        var all = new List<Rec>(seeds * 4);
        foreach (Rec[] world in worlds) all.AddRange(world);

        var digestInput = new StringBuilder();
        foreach (Rec r in all) digestInput.Append(r.Line()).Append('\n');
        using var sha = SHA256.Create();
        string digest = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(digestInput.ToString()))).ToLowerInvariant();

        if (csv is not null)
        {
            File.WriteAllText(csv, "seed,civ,wetSite,star,saltpeterOnMap,sulfurOnMap,saltpeterNearest,sulfurNearest,saltpeterUnc5,saltpeterUnc8,saltpeterUncStar," +
                "saltpeterUnc25,sulfurUncStar,saltpeterStep,sulfurStep,saltpeterStock,riverNearest,riverTiles3,riverTiles5,riverTiles8,forestNearRiver,siteOnRiver," +
                "horseHerdsOnMap,horseNearest,horseHerdsUnc15,horseHerdsUnc20,horseHerdsUnc25,horseAnimalsUnc25,horseHerdsAny25,coastDist,sameContinentMate," +
                "mateHasSaltpeter,otherHasSaltpeter,mateHasHorses,otherHasHorses,saltMateWalk,saltMateSail,saltOtherWalk,saltOtherSail," +
                "horseMateWalk,horseMateSail,horseOtherWalk,horseOtherSail\n" + digestInput);
        }

        return Report(first, seeds, all, worlds, digest);
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
        WorldEconomyInput econ = WorldEconomyInput.FromGeneratedWorld(world);
        int w = world.Width, h = world.Height;
        WorldTile[] tiles = world.Tiles.ToArray();
        int[] starts = new int[world.Starts.Length];
        for (int c = 0; c < starts.Length; c++) starts[c] = world.Starts[c].Tile;

        var saltpeter = new List<int>();
        var sulfur = new List<int>();
        for (int t = 0; t < tiles.Length; t++)
        {
            DepositKind kind = econ.Deposit(t).Kind;
            if (kind == DepositKind.Saltpeter) saltpeter.Add(t);
            else if (kind == DepositKind.Sulfur) sulfur.Add(t);
        }
        var horses = new List<(int Tile, int Size)>();
        foreach (Herd herd in world.Herds)
            if (herd.Species == HerdSpecies.Horse) horses.Add((herd.Tile, herd.Size));

        bool[] passable = HexGrid.BuildWalkingPassability(tiles, w, h);
        int[] landDistance = HexGrid.LandDistances(tiles, w, h);

        var recs = new Rec[starts.Length];
        int[][] walk = new int[starts.Length][];
        int[][] sail = new int[starts.Length][];
        for (int c = 0; c < starts.Length; c++)
        {
            recs[c] = Local(seed, c, starts, tiles, econ, saltpeter, sulfur, horses, w, h);
            walk[c] = HexGrid.WalkingDistancesFrom(w, h, passable, starts[c]);
            sail[c] = HexGrid.WalkingCostsFrom(tiles, w, h, starts[c], int.MaxValue, landDistance, OpenSeaReach);
        }

        for (int c = 0; c < starts.Length; c++)
        {
            Rec r = recs[c];
            int mate = c ^ 1;
            r.SameContinentMate = tiles[starts[c]].Continent == tiles[starts[mate]].Continent;
            // A partner can export saltpeter if its own start has a local saltpeter route on E5 rules; horses if it has a horse herd of its own.
            r.MateHasSaltpeter = recs[mate].SaltpeterLocal;
            r.MateHasHorses = recs[mate].HorsesLocal;
            r.SaltMateWalk = r.MateHasSaltpeter ? walk[c][starts[mate]] : int.MaxValue;
            r.SaltMateSail = r.MateHasSaltpeter ? SailTiles(sail[c][starts[mate]]) : int.MaxValue;
            r.HorseMateWalk = r.MateHasHorses ? walk[c][starts[mate]] : int.MaxValue;
            r.HorseMateSail = r.MateHasHorses ? SailTiles(sail[c][starts[mate]]) : int.MaxValue;
            for (int o = 0; o < starts.Length; o++)
            {
                if (o == c || o == mate) continue;
                if (recs[o].SaltpeterLocal)
                {
                    r.OtherHasSaltpeter = true;
                    r.SaltOtherWalk = Math.Min(r.SaltOtherWalk, walk[c][starts[o]]);
                    r.SaltOtherSail = Math.Min(r.SaltOtherSail, SailTiles(sail[c][starts[o]]));
                }
                if (recs[o].HorsesLocal)
                {
                    r.OtherHasHorses = true;
                    r.HorseOtherWalk = Math.Min(r.HorseOtherWalk, walk[c][starts[o]]);
                    r.HorseOtherSail = Math.Min(r.HorseOtherSail, SailTiles(sail[c][starts[o]]));
                }
            }
        }
        return recs;
    }

    private static int SailTiles(int cost) => cost == int.MaxValue ? int.MaxValue : (cost + 9) / 10;

    private static Rec Local(ulong seed, int civ, int[] starts, WorldTile[] tiles, WorldEconomyInput econ,
        List<int> saltpeter, List<int> sulfur, List<(int Tile, int Size)> horses, int w, int h)
    {
        int s = starts[civ];
        var r = new Rec { Seed = seed, Civ = civ };
        WorldEconomyTile site = econ.Tile(s);
        r.WetSite = site.River || site.Coast || site.Lake;
        r.Star = r.WetSite ? ReachWater : ReachCarts;
        r.SiteOnRiver = site.River || site.Lake;

        r.SaltpeterOnMap = saltpeter.Count;
        r.SulfurOnMap = sulfur.Count;
        r.SaltpeterStep = Ladder(s, saltpeter, starts, econ, w, r.Star, out r.SaltpeterNearest, out r.SaltpeterUnc5, out r.SaltpeterUnc8,
            out r.SaltpeterUncStar, out r.SaltpeterUnc25, out r.SaltpeterStock);
        r.SulfurStep = Ladder(s, sulfur, starts, econ, w, r.Star, out r.SulfurNearest, out _, out _, out r.SulfurUncStar, out _, out _);

        // Rivers: the Near: River gate is read at the settlement, so count river and lake tiles near the site and the nearest forest for planks.
        HexGrid.Coordinates(s, w, out int sx, out int sy);
        int span = 26;
        int coast = int.MaxValue;
        for (int ny = Math.Max(0, sy - span); ny <= Math.Min(h - 1, sy + span); ny++)
        for (int nx = Math.Max(0, sx - span - 1); nx <= Math.Min(w - 1, sx + span + 1); nx++)
        {
            int t = ny * w + nx;
            int d = HexGrid.Distance(s, t, w);
            if (d > 26) continue;
            if ((tiles[t].Flags & TileFlags.CoastAdjacent) != 0) coast = Math.Min(coast, d);
            if ((tiles[t].Flags & (TileFlags.River | TileFlags.Lake)) != 0 && Uncontested(s, d, t, starts, w))
            {
                r.RiverNearest = Math.Min(r.RiverNearest, d);
                if (d <= SettlementRange) r.RiverTiles3++;
                if (d <= ClaimRadius) r.RiverTiles5++;
                if (d <= ReachBase) r.RiverTiles8++;
            }
        }
        r.CoastDist = coast == int.MaxValue ? 99 : coast;
        if (r.RiverTiles5 > 0)
        {
            // A sawmill needs a river site and planks: standing forest (>= 15 L) within reach 8 of the start, nearer this start than any other.
            for (int ny = Math.Max(0, sy - SawmillWoodReach); ny <= Math.Min(h - 1, sy + SawmillWoodReach); ny++)
            for (int nx = Math.Max(0, sx - SawmillWoodReach - 1); nx <= Math.Min(w - 1, sx + SawmillWoodReach + 1); nx++)
            {
                int t = ny * w + nx;
                int d = HexGrid.Distance(s, t, w);
                if (d > SawmillWoodReach || !Uncontested(s, d, t, starts, w)) continue;
                if (econ.Tile(t).InitialWood >= CivSim.Sim.Numerics.Fix64.FromInt(15)) r.ForestNearRiver++;
            }
        }

        // Horses: wild herds on the map at the start of the game (herds migrate seasonally, so these are start positions only).
        r.HorseHerdsOnMap = horses.Count;
        foreach ((int tile, int size) in horses)
        {
            int d = HexGrid.Distance(s, tile, w);
            r.HorseNearest = Math.Min(r.HorseNearest, d);
            if (d <= E5Reach) r.HorseHerdsAny25++;
            if (d > E5Reach || !Uncontested(s, d, tile, starts, w)) continue;
            r.HorseHerdsUnc25++;
            r.HorseAnimalsUnc25 += size;
            if (d <= HorseMid) r.HorseHerdsUnc20++;
            if (d <= HorseNear) r.HorseHerdsUnc15++;
        }
        return r;
    }

    private static Step Ladder(int s, List<int> deposits, int[] starts, WorldEconomyInput econ, int w, int star,
        out int nearest, out int unc5, out int unc8, out int uncStar, out int unc25, out int stock)
    {
        nearest = -1; unc5 = unc8 = uncStar = unc25 = 0; stock = 0;
        int d1Unc8 = 0, d1UncStar = 0, anyUncStar = 0, anyStar = 0, any25 = 0;
        foreach (int t in deposits)
        {
            int d = HexGrid.Distance(s, t, w);
            if (nearest < 0 || d < nearest) nearest = d;
            if (d <= E5Reach) any25++;
            if (d > star) continue;
            anyStar++;
            if (!Uncontested(s, d, t, starts, w)) continue;
            anyUncStar++;
            int depth = econ.Deposit(t).Depth;
            if (d <= ClaimRadius) unc5++;
            if (d <= ReachBase) unc8++;
            uncStar++;
            stock += econ.Deposit(t).Tier;
            if (depth < 2)
            {
                d1UncStar++;
                if (d <= ReachBase) d1Unc8++;
            }
        }
        foreach (int t in deposits)
        {
            int d = HexGrid.Distance(s, t, w);
            if (d <= E5Reach && Uncontested(s, d, t, starts, w)) unc25++;
        }
        if (d1Unc8 > 0) return Step.StrictLocal;
        if (d1UncStar > 0) return Step.CartsOrWater;
        if (anyUncStar > 0) return Step.Deep;
        if (anyStar > 0) return Step.Contested;
        if (any25 > 0) return Step.BeyondReach;
        return Step.None;
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

    // ---- report -----------------------------------------------------------------------------------------

    private static string Pct(int n, int d)
    {
        if (d == 0) return "n/a";
        long tenths = (n * 1000L + d / 2) / d;
        return $"{tenths / 10}.{tenths % 10}%";
    }

    private static T Percentile<T>(List<T> sorted, int percent) where T : IComparable<T>
        => sorted.Count == 0 ? default! : sorted[Math.Min(sorted.Count - 1, (int)((long)(sorted.Count - 1) * percent / 100))];

    private static string Dist(IEnumerable<int> values)
    {
        var list = new List<int>(values);
        list.Sort();
        if (list.Count == 0) return "n/a";
        return $"min {list[0]} p5 {Percentile(list, 5)} p25 {Percentile(list, 25)} p50 {Percentile(list, 50)} " +
               $"p75 {Percentile(list, 75)} p95 {Percentile(list, 95)} max {list[^1]} (n={list.Count:N0})";
    }

    private static IEnumerable<int> Each(Rec[][] worlds, Func<Rec[], int> pick)
    {
        foreach (Rec[] w in worlds) yield return pick(w);
    }

    private static void Ladder(StringBuilder o, string title, List<Rec> group, Func<Rec, Step> step)
    {
        int n = group.Count;
        var counts = new int[7];
        foreach (Rec r in group) counts[(int)step(r)]++;
        string I(int v) => v.ToString("N0", CultureInfo.InvariantCulture);
        o.Append($"  {title} (n={I(n)}): ");
        o.Append($"1 local-8 {Pct(counts[1], n)} | 2 carts/water {Pct(counts[2], n)} | 3 deep {Pct(counts[3], n)} | 4 contested {Pct(counts[4], n)} | 5 beyond reach {Pct(counts[5], n)} | 6 none {Pct(counts[6], n)}");
        int local = counts[1] + counts[2] + counts[3];
        o.Append($" || own supply (1-3) {Pct(local, n)}\n");
    }

    private static string Report(ulong first, int seeds, List<Rec> all, Rec[][] worlds, string digest)
    {
        var o = new StringBuilder();
        void L(string text = "") => o.Append(text).Append('\n');
        string I(int v) => v.ToString("N0", CultureInfo.InvariantCulture);
        string P(Func<Rec, bool> pick, List<Rec>? group = null) { var g = group ?? all; int k = g.FindAll(x => pick(x)).Count; return $"{Pct(k, g.Count)} ({I(k)}/{I(g.Count)})"; }

        L($"e5-audit seeds {first}..{first + (ulong)seeds - 1} ({I(seeds)} worlds, {I(all.Count)} starts)");
        L($"digest {digest}");
        int localMismatch = 0;
        foreach (Rec[] w in worlds)
            for (int c = 0; c < 2; c++)
                if (w[c].LocalKey() != w[c + 2].LocalKey()) localMismatch++;
        L($"mirror check: local columns of civ c and c+2 differ in {I(localMismatch)} of {I(seeds * 2)} pairs");
        L();

        var distinct = all.FindAll(r => r.Civ < 2);
        var coastal = all.FindAll(r => r.CoastDist <= 8);
        var inland = all.FindAll(r => r.CoastDist > 8 && r.CoastDist <= 20);
        var deepInland = all.FindAll(r => r.CoastDist > 20);

        L("== 1. Denominators ==");
        L($"worlds {I(seeds)}; starts {I(all.Count)}; geometrically distinct starts {I(distinct.Count)} (civ 2 and 3 mirror civ 0 and 1).");
        L($"strata by nearest coast-adjacent tile: coastal (<= 8) {I(coastal.Count)}; inland (9-20) {I(inland.Count)}; deep inland (> 20) {I(deepInland.Count)}.");
        L("uncontested = strictly nearer this start than any other start (a static stand-in for ownership; live claims differ).");
        L();

        L("== 2. World truth per world ==");
        L($"saltpeter deposits on the map: {Dist(Each(worlds, w => w[0].SaltpeterOnMap))}");
        L($"sulfur deposits on the map: {Dist(Each(worlds, w => w[0].SulfurOnMap))}");
        L($"wild horse herds on the map: {Dist(Each(worlds, w => w[0].HorseHerdsOnMap))}");
        L($"worlds with no saltpeter at all: {Pct(Each(worlds, w => w[0].SaltpeterOnMap).Where0(), seeds)}; with no sulfur: {Pct(Each(worlds, w => w[0].SulfurOnMap).Where0(), seeds)}; with no horse herd: {Pct(Each(worlds, w => w[0].HorseHerdsOnMap).Where0(), seeds)}");
        L();

        L("== 3. Saltpeter (CHM-07 [Has: Saltpeter]) ==");
        L("step 1 uncontested depth-1 within 8 | 2 within 12 (TRN-05) or 16 (SEA-05, wet site) | 3 depth-2 within that reach | 4 only nearer a rival | 5 within 25 but beyond reach | 6 none within 25");
        Ladder(o, "all starts", all, r => r.SaltpeterStep);
        Ladder(o, "distinct starts", distinct, r => r.SaltpeterStep);
        Ladder(o, "coastal", coastal, r => r.SaltpeterStep);
        Ladder(o, "inland", inland, r => r.SaltpeterStep);
        Ladder(o, "deep inland", deepInland, r => r.SaltpeterStep);
        L($"nearest saltpeter (hex, any): {Dist(all.ConvertAll(r => r.SaltpeterNearest))}");
        L($"uncontested saltpeter sites within the claim radius {ClaimRadius}: {Dist(all.ConvertAll(r => r.SaltpeterUnc5))}");
        L($"uncontested saltpeter sites within the extended reach: {Dist(all.ConvertAll(r => r.SaltpeterUncStar))}");
        L($"uncontested saltpeter sites within the E5 reach {E5Reach} (where an expanded realm would hold them): {Dist(all.ConvertAll(r => r.SaltpeterUnc25))}");
        L($"starts with a saltpeter site inside the claim radius {ClaimRadius}: {P(r => r.SaltpeterUnc5 > 0)}");
        L($"starts with a saltpeter site anywhere in the E5 reach, uncontested: {P(r => r.SaltpeterUnc25 > 0)}");
        L($"saltpeter + sulfur both on own ground (steps 1-3 for each): {P(r => r.PowderLocal)}");
        Ladder(o, "sulfur, all starts", all, r => r.SulfurStep);
        L();

        L("== 4. Rivers and lakes (NRG-03 water mill and WOD-09 sawmill [Near: River]; wind needs none) ==");
        L($"start site itself on a river or lake tile: {P(r => r.SiteOnRiver)}");
        L($"nearest uncontested river or lake tile (hex): {Dist(all.ConvertAll(r => r.RiverNearest))}");
        L($"river or lake tiles within the settlement range {SettlementRange}: {Dist(all.ConvertAll(r => r.RiverTiles3))}");
        L($"starts with a river or lake tile within {SettlementRange}: {P(r => r.RiverTiles3 > 0)}; within the claim radius {ClaimRadius}: {P(r => r.RiverTiles5 > 0)}; within 8: {P(r => r.RiverTiles8 > 0)}");
        L($"water-mill site (river within 5) and planks (forest within {SawmillWoodReach}) for a sawmill: {P(r => r.RiverTiles5 > 0 && r.ForestNearRiver > 0)}");
        L($"wind-only starts (no river or lake within 5, so a windmill is the only mill): {P(r => r.RiverTiles5 == 0)}; coastal {P(r => r.RiverTiles5 == 0, coastal)}; inland {P(r => r.RiverTiles5 == 0, inland)}; deep inland {P(r => r.RiverTiles5 == 0, deepInland)}");
        L();

        L("== 5. Wild horses (HUS-07 [Seen: Wild horses]; NRG-04 horse collar, TRN-09 horseshoes, WAR-10 heavy cavalry need G-HORSE) ==");
        L($"nearest horse herd (hex, any owner; 99 = none): {Dist(all.ConvertAll(r => r.HorseNearest))}");
        L($"uncontested horse herds within 15 / 20 / {E5Reach}: {P(r => r.HorseHerdsUnc15 > 0)} / {P(r => r.HorseHerdsUnc20 > 0)} / {P(r => r.HorseHerdsUnc25 > 0)}");
        L($"horse herds within {E5Reach}, uncontested: {Dist(all.ConvertAll(r => r.HorseHerdsUnc25))}");
        L($"wild horses within {E5Reach}, uncontested (animals at the start of the game): {Dist(all.ConvertAll(r => r.HorseAnimalsUnc25))}");
        L("taming (HUS-07): at most 15% of a wild herd a year, so a 20-50 herd yields 3-7 animals a year; a pasture carries 2.");
        L($"starts that could be 'Seen: Wild horses' at all (any herd within {E5Reach}, contested or not): {P(r => r.HorseHerdsAny25 > 0)}");
        L($"strata, uncontested herd within {E5Reach}: coastal {P(r => r.HorsesLocal, coastal)}; inland {P(r => r.HorsesLocal, inland)}; deep inland {P(r => r.HorsesLocal, deepInland)}");
        L("Herds migrate seasonally (20 tiles) so these are start-of-game positions; a start with none may still see horses later.");
        L();

        L("== 6. All three together (own saltpeter route, a river within 5, an own horse herd within 25) ==");
        for (int k = 3; k >= 0; k--)
            L($"  {k} of 3 local: {P(r => r.KitCount == k)}");
        L($"  powder chain whole (saltpeter and sulfur) plus river plus horses: {P(r => r.PowderLocal && r.RiverSite && r.HorsesLocal)}");
        L($"  no saltpeter and no horses on own ground: {P(r => !r.SaltpeterLocal && !r.HorsesLocal)}");
        L();

        L("== 7. Trade geometry for what a start lacks (geography only; no contact, relations, TradeGood or barter-party carrying is modeled) ==");
        L("capable partner = its own start has the resource on its own ground. Route = unit-cost walk else sail with SEA-04 (11 tiles from land); seasons = tiles / 10.");
        foreach ((string name, Func<Rec, bool> lacks, Func<Rec, bool> mateHas, Func<Rec, int> mateWalk, Func<Rec, int> mateSail, Func<Rec, bool> otherHas, Func<Rec, int> otherWalk, Func<Rec, int> otherSail) in new (string, Func<Rec, bool>, Func<Rec, bool>, Func<Rec, int>, Func<Rec, int>, Func<Rec, bool>, Func<Rec, int>, Func<Rec, int>)[]
        {
            ("saltpeter", r => !r.SaltpeterLocal, r => r.MateHasSaltpeter, r => r.SaltMateWalk, r => r.SaltMateSail, r => r.OtherHasSaltpeter, r => r.SaltOtherWalk, r => r.SaltOtherSail),
            ("horses", r => !r.HorsesLocal, r => r.MateHasHorses, r => r.HorseMateWalk, r => r.HorseMateSail, r => r.OtherHasHorses, r => r.HorseOtherWalk, r => r.HorseOtherSail),
        })
        {
            var lack = all.FindAll(r => lacks(r));
            L($"-- {name}: starts lacking it on own ground: {I(lack.Count)} ({Pct(lack.Count, all.Count)})");
            L($"   mate has it: {Pct(lack.FindAll(r => mateHas(r)).Count, lack.Count)}; walkable to mate: {Pct(lack.FindAll(r => mateHas(r) && mateWalk(r) != int.MaxValue).Count, lack.Count)}; mate walk tiles: {Dist(lack.FindAll(r => mateHas(r) && mateWalk(r) != int.MaxValue).ConvertAll(r => mateWalk(r)))}");
            var sailOnly = lack.FindAll(r => !(mateHas(r) && mateWalk(r) != int.MaxValue));
            L($"   no walkable mate: {I(sailOnly.Count)}; of those a mate sailable {Pct(sailOnly.FindAll(r => mateHas(r) && mateSail(r) != int.MaxValue).Count, sailOnly.Count)}; another civilization walkable {Pct(sailOnly.FindAll(r => otherHas(r) && otherWalk(r) != int.MaxValue).Count, sailOnly.Count)}; sailable {Pct(sailOnly.FindAll(r => otherHas(r) && otherSail(r) != int.MaxValue).Count, sailOnly.Count)}");
            int none = lack.FindAll(r => !(mateHas(r) && (mateWalk(r) != int.MaxValue || mateSail(r) != int.MaxValue)) && !(otherHas(r) && (otherWalk(r) != int.MaxValue || otherSail(r) != int.MaxValue))).Count;
            L($"   no geographic route to any capable start: {I(none)} ({Pct(none, all.Count)} of all starts)");
        }
        L();

        int worldsNoSaltAnywhere = 0;
        foreach (Rec[] w in worlds) if (w[0].SaltpeterOnMap == 0) worldsNoSaltAnywhere++;
        L("== 8. Limits ==");
        L($"worlds with no saltpeter anywhere ({I(worldsNoSaltAnywhere)}) leave every start in them without powder unless the tile gate is relaxed; they are counted above, never dropped.");
        L("Deposits are generator truth; whether a civilization knows them at CHM-07 is a discovery question (MIN-08 reveals veins inside own territory; the geology-discovery branch changes how early). Static starts only: no expansion, no rivals' growth.");
        return o.ToString();
    }
}

internal static class AuditExtensions
{
    /// <summary>Counts the zeros in a sequence (used for "worlds with none").</summary>
    public static int Where0(this IEnumerable<int> values)
    {
        int n = 0;
        foreach (int v in values) if (v == 0) n++;
        return n;
    }
}
