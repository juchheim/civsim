using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>
/// <c>iron-audit</c> (docs/MVP/25 §4, §14 B4-10; M11 WP4): a read-only audit of the existing world generator. For every start of every
/// audited seed it measures what world truth places near the start, what E3 rules let a civilization know and reach, and which
/// partners a trade route could geographically connect it to. It generates worlds with <see cref="WorldGenerator"/> and never writes
/// them: the worldgen output, fingerprints and golden are untouched. Definitions are fixed here before any result is read.
/// <code>
/// civsim-harness iron-audit [--seed N] [--seeds N] [--threads N] [--csv FILE]
/// </code>
/// The report is a function of the seed range alone (no clocks, no thread-order effects); <c>digest</c> hashes every per-start record.
/// </summary>
internal static class IronGeographyAudit
{
    // ---- fixed definitions ------------------------------------------------------------------------------

    /// <summary>MET-08 reveals iron deposits within 20 tiles of every own settlement (content/catalog/met-metallurgy.yaml).</summary>
    private const int KnownRadius = 20;
    /// <summary>MineReach 8; TRN-05 carts 12; SEA-05 16 along rivers, coasts and lakes (BronzeConstants, MiningReach).</summary>
    private const int ReachBase = 8, ReachCarts = 12, ReachWater = 16;
    /// <summary>CharcoalReach 8; 12 with TRN-05.</summary>
    private const int CharcoalReach = 8, CharcoalReachCarts = 12;
    /// <summary>The economy's own forest test: a tile holding at least 15 L of standing wood.</summary>
    private static readonly Fix64 ForestWood = Fix64.FromInt(15);
    private const int CoppiceRadius = 3;
    /// <summary>The E3 claim radius (content/registries/eras.yaml): the least land a settlement owns around itself.</summary>
    private const int ClaimFloor = 4;
    /// <summary>LivingEconomyModel.Radius: ore a settlement digs without a camp lies within 3 tiles of its site.</summary>
    private const int SettlementRange = 3;

    // Reference demand per adult from e4_metal_oracle.py replacement(): T11 2.0357 L ore, 22.291 L wood per 100 adults per season;
    // T12 half the kits. Per adult-year (four seasons): wood 0.89164 L (T11), 0.45396 L (T12); ore 0.081429 L (T11).
    private const double WoodPerAdultYearT11 = 22.29107142857142 * 4 / 100;
    private const double WoodPerAdultYearT12 = 11.34910714285714 * 4 / 100;
    private const double OrePerAdultSeasonT11 = 2.035714285714285 / 100;
    private const double OreLitresPerDeposit = 1000;
    private const int HorizonSeasons = 68; // 17 simulated minutes at Standard 1x (docs/MVP/25 §3)
    private const double HaulWsPerLitreTile = 1.0 / 40;
    private const double AdultWsPerSeason = 38.0 / 35;
    private const int LowWoodAdults = 100, VeryLowWoodAdults = 50;
    private const int OpenSeaReach = 11, TilesPerSeason = 10;

    private enum IronStep
    {
        /// <summary>Uncontested depth-1 deposit within the base mining reach of 8.</summary>
        StrictLocal = 1,
        /// <summary>Uncontested depth-1 deposit within 12 (TRN-05) or 16 (SEA-05 on a river, coast or lake site).</summary>
        CartsOrWater = 2,
        /// <summary>Uncontested depth-2 deposit within the extended reach; needs MIN-04 Shaft Mining.</summary>
        NeedsShaft = 3,
        /// <summary>Only deposits nearer a rival start within the extended reach: ownership decides.</summary>
        Contested = 4,
        /// <summary>Known (within 20) but beyond every mining reach: needs a nearer settlement.</summary>
        BeyondReach = 5,
        /// <summary>No iron within 20 tiles.</summary>
        NoneKnown = 6,
    }

    private sealed class Rec
    {
        public ulong Seed;
        public int Civ;
        public int IronOnMap, IronOnContinent;
        public int NearestAny = -1, NearestDepth1 = -1, NearestUncontestedDepth1 = -1;
        public int Known, KnownDepth1;
        public int D1Unc3, AnyUnc3, D1Unc4, D1Unc8, D1Unc12, D1UncStar, AnyUnc8, AnyUncStar, AnyStar;
        public int IronDepth2OnMap;
        public bool WetSite;
        public int Star;
        public IronStep Step;
        public int SiteHex = -1, SiteDepth, SiteWalkTenths = int.MaxValue;
        public int Forest8, Forest12, Wooded8, Wooded12, Forest3;
        public double ForestStock8;
        public int CoastDist, WaterDist;
        // trade geography
        public int MateWalk = int.MaxValue, MateSail = int.MaxValue, OtherWalk = int.MaxValue, OtherSail = int.MaxValue;
        public bool MateCapable, OtherCapable;
        public bool SameContinentMate;

        public double Supportable(int forestTiles, bool coppice, double perAdultYear)
            => (2.0 * (forestTiles + (coppice ? Forest3 : 0))) / perAdultYear;

        public bool LowWood => Supportable(Forest8, false, WoodPerAdultYearT11) < LowWoodAdults;
        public bool VeryLowWood => Supportable(Forest8, false, WoodPerAdultYearT11) < VeryLowWoodAdults;

        public string Line()
        {
            var sb = new StringBuilder();
            sb.Append(Seed).Append(',').Append(Civ);
            foreach (int v in new[]
            {
                IronOnMap, IronOnContinent, NearestAny, NearestDepth1, NearestUncontestedDepth1, Known, KnownDepth1,
                D1Unc3, AnyUnc3, D1Unc4, D1Unc8, D1Unc12, D1UncStar, AnyUnc8, AnyUncStar, AnyStar, IronDepth2OnMap, WetSite ? 1 : 0, Star, (int)Step, SiteHex, SiteDepth,
                SiteWalkTenths, Forest8, Forest12, Wooded8, Wooded12, Forest3, CoastDist, WaterDist, MateWalk, MateSail,
                OtherWalk, OtherSail, MateCapable ? 1 : 0, OtherCapable ? 1 : 0, SameContinentMate ? 1 : 0,
            })
                sb.Append(',').Append(v.ToString(CultureInfo.InvariantCulture));
            sb.Append(',').Append(ForestStock8.ToString("F3", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>The record without its civilization: the rotated mirror start must equal this exactly.</summary>
        public string MirrorKey()
        {
            string line = Line();
            int first = line.IndexOf(',');
            int second = line.IndexOf(',', first + 1);
            return line[(second + 1)..];
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
                    Console.Error.WriteLine("usage: civsim-harness iron-audit [--seed N] [--seeds N] [--threads N] [--csv FILE]");
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

        using var sha = SHA256.Create();
        var digestInput = new StringBuilder();
        foreach (Rec r in all) digestInput.Append(r.Line()).Append('\n');
        string digest = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(digestInput.ToString()))).ToLowerInvariant();

        if (csv is not null)
        {
            File.WriteAllText(csv, "seed,civ,ironOnMap,ironOnContinent,nearestAny,nearestDepth1,nearestUncDepth1,known,knownDepth1," +
                "d1Unc3,anyUnc3,d1Unc4,d1Unc8,d1Unc12,d1UncStar,anyUnc8,anyUncStar,anyStar,ironDepth2OnMap,wetSite,star,step,siteHex,siteDepth,siteWalkTenths," +
                "forest8,forest12,wooded8,wooded12,forest3,coastDist,waterDist,mateWalk,mateSail,otherWalk,otherSail," +
                "mateCapable,otherCapable,sameContinentMate,forestStock8\n" + digestInput);
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

        var iron = new List<int>();
        for (int t = 0; t < tiles.Length; t++)
            if (econ.Deposit(t).Kind == DepositKind.Iron) iron.Add(t);

        bool[] passable = HexGrid.BuildWalkingPassability(tiles, w, h);
        int[] landDistance = HexGrid.LandDistances(tiles, w, h);

        var recs = new Rec[starts.Length];
        int[][] walk = new int[starts.Length][];
        int[][] sail = new int[starts.Length][];
        for (int c = 0; c < starts.Length; c++)
        {
            recs[c] = Local(seed, c, starts, tiles, econ, iron, w, h);
            walk[c] = HexGrid.WalkingDistancesFrom(w, h, passable, starts[c]);
            sail[c] = HexGrid.WalkingCostsFrom(tiles, w, h, starts[c], int.MaxValue, landDistance, OpenSeaReach);
        }

        for (int c = 0; c < starts.Length; c++)
        {
            Rec r = recs[c];
            int mate = c ^ 1;
            r.SameContinentMate = tiles[starts[c]].Continent == tiles[starts[mate]].Continent;
            // A partner can export if its own start has a workable, uncontested depth-1 deposit on E3 rules alone (no MIN-04).
            r.MateCapable = recs[mate].Step <= IronStep.CartsOrWater;
            r.MateWalk = walk[c][starts[mate]];
            r.MateSail = SailTiles(sail[c][starts[mate]]);
            for (int o = 0; o < starts.Length; o++)
            {
                if (o == c || o == mate) continue;
                if (recs[o].Step > IronStep.CartsOrWater) continue;
                r.OtherCapable = true;
                r.OtherWalk = Math.Min(r.OtherWalk, walk[c][starts[o]]);
                r.OtherSail = Math.Min(r.OtherSail, SailTiles(sail[c][starts[o]]));
            }
        }
        return recs;
    }

    private static int SailTiles(int cost) => cost == int.MaxValue ? int.MaxValue : (cost + 9) / 10;

    private static Rec Local(ulong seed, int civ, int[] starts, WorldTile[] tiles, WorldEconomyInput econ, List<int> iron, int w, int h)
    {
        int s = starts[civ];
        var r = new Rec { Seed = seed, Civ = civ };
        WorldEconomyTile site = econ.Tile(s);
        r.WetSite = site.River || site.Coast || site.Lake;
        r.Star = r.WetSite ? ReachWater : ReachCarts;
        byte continent = tiles[s].Continent;
        r.IronOnMap = iron.Count;

        int bestUncDepth1Hex = int.MaxValue, bestUncAnyHex = int.MaxValue, bestUncAnyDepth = 0, bestContestedHex = int.MaxValue;
        var eligibleSites = new List<int>();
        foreach (int t in iron)
        {
            int d = HexGrid.Distance(s, t, w);
            int depth = econ.Deposit(t).Depth;
            if (depth >= 2) r.IronDepth2OnMap++;
            if (tiles[t].Continent == continent) r.IronOnContinent++;
            if (r.NearestAny < 0 || d < r.NearestAny) r.NearestAny = d;
            if (depth < 2 && (r.NearestDepth1 < 0 || d < r.NearestDepth1)) r.NearestDepth1 = d;
            bool uncontested = Uncontested(s, d, t, starts, w);
            if (uncontested && depth < 2 && (r.NearestUncontestedDepth1 < 0 || d < r.NearestUncontestedDepth1)) r.NearestUncontestedDepth1 = d;
            if (d <= KnownRadius) { r.Known++; if (depth < 2) r.KnownDepth1++; }
            if (d > r.Star) continue;
            r.AnyStar++;
            if (!uncontested) { bestContestedHex = Math.Min(bestContestedHex, d); continue; }
            r.AnyUncStar++;
            if (d <= SettlementRange) r.AnyUnc3++;
            if (d <= ReachBase) r.AnyUnc8++;
            if (depth < 2)
            {
                r.D1UncStar++;
                if (d <= ReachCarts) r.D1Unc12++;
                if (d <= ReachBase) r.D1Unc8++;
                if (d <= ClaimFloor) r.D1Unc4++;
                if (d <= SettlementRange) r.D1Unc3++;
                eligibleSites.Add(t);
                if (d < bestUncDepth1Hex) bestUncDepth1Hex = d;
            }
            if (d < bestUncAnyHex) { bestUncAnyHex = d; bestUncAnyDepth = depth; }
        }
        if (r.D1Unc8 > 0) { r.Step = IronStep.StrictLocal; r.SiteHex = bestUncDepth1Hex; r.SiteDepth = 1; }
        else if (r.D1UncStar > 0) { r.Step = IronStep.CartsOrWater; r.SiteHex = bestUncDepth1Hex; r.SiteDepth = 1; }
        else if (r.AnyUncStar > 0) { r.Step = IronStep.NeedsShaft; r.SiteHex = bestUncAnyHex; r.SiteDepth = bestUncAnyDepth; }
        else if (r.AnyStar > 0) { r.Step = IronStep.Contested; r.SiteHex = bestContestedHex; r.SiteDepth = 0; }
        else if (r.Known > 0) r.Step = IronStep.BeyondReach;
        else r.Step = IronStep.NoneKnown;

        if (r.Step <= IronStep.CartsOrWater && eligibleSites.Count > 0)
        {
            // The cheapest walking route among all eligible sites, so equal-distance ties cannot matter.
            int[] costs = HexGrid.WalkingCostsFrom(tiles, w, h, s);
            foreach (int candidate in eligibleSites) r.SiteWalkTenths = Math.Min(r.SiteWalkTenths, costs[candidate]);
        }

        // Wood: tiles nearer this start than any other, so a neighbor's woods are not double counted.
        int coast = int.MaxValue, water = int.MaxValue;
        HexGrid.Coordinates(s, w, out int sx, out int sy);
        int span = 24;
        for (int ny = Math.Max(0, sy - span); ny <= Math.Min(h - 1, sy + span); ny++)
        for (int nx = Math.Max(0, sx - span - 1); nx <= Math.Min(w - 1, sx + span + 1); nx++)
        {
            int t = ny * w + nx;
            int d = HexGrid.Distance(s, t, w);
            if (d > 25) continue;
            if ((tiles[t].Flags & TileFlags.CoastAdjacent) != 0) coast = Math.Min(coast, d);
            if ((tiles[t].Flags & (TileFlags.River | TileFlags.Lake)) != 0) water = Math.Min(water, d);
            if (d > CharcoalReachCarts) continue;
            if (!Uncontested(s, d, t, starts, w)) continue;
            WorldEconomyTile e = econ.Tile(t);
            bool forest = e.InitialWood >= ForestWood;
            bool wooded = e.InitialWood > Fix64.Zero;
            if (d <= CharcoalReach)
            {
                if (forest) { r.Forest8++; r.ForestStock8 += e.InitialWood.ToDoubleForDisplay(); }
                if (wooded) r.Wooded8++;
            }
            if (forest) r.Forest12++;
            if (wooded) r.Wooded12++;
            if (forest && d <= CoppiceRadius) r.Forest3++;
        }
        r.CoastDist = coast == int.MaxValue ? 99 : coast;
        r.WaterDist = water == int.MaxValue ? 99 : water;
        return r;
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
        return $"min {list[0]} p1 {Percentile(list, 1)} p5 {Percentile(list, 5)} p25 {Percentile(list, 25)} p50 {Percentile(list, 50)} " +
               $"p75 {Percentile(list, 75)} p95 {Percentile(list, 95)} p99 {Percentile(list, 99)} max {list[^1]} (n={list.Count:N0})";
    }

    private static string Report(ulong first, int seeds, List<Rec> all, Rec[][] worlds, string digest)
    {
        var o = new StringBuilder();
        void L(string text = "") => o.Append(text).Append('\n');
        string I(int v) => v.ToString("N0", CultureInfo.InvariantCulture);

        L($"iron-audit seeds {first}..{first + (ulong)seeds - 1} ({I(seeds)} worlds, {I(all.Count)} starts)");
        L($"digest {digest}");

        // Mirror check: civ 2 is the 180-degree rotation of civ 0, civ 3 of civ 1.
        int mirrorMismatch = 0;
        foreach (Rec[] w in worlds)
            for (int c = 0; c < 2; c++)
                if (w[c].MirrorKey() != w[c + 2].MirrorKey()) mirrorMismatch++;
        // The local columns alone, as a second view of the same symmetry (the full record also includes the partner routes).
        int localMismatch = 0;
        foreach (Rec[] w in worlds)
            for (int c = 0; c < 2; c++)
                if (LocalKey(w[c]) != LocalKey(w[c + 2])) localMismatch++;
        L($"mirror check: local columns of civ c and c+2 differ in {I(localMismatch)} of {I(seeds * 2)} pairs (full-record differences: {I(mirrorMismatch)})");
        L();

        var distinct = all.FindAll(r => r.Civ < 2);
        L("== 1. Denominators ==");
        L($"worlds {I(seeds)}; starts {I(all.Count)}; geometrically distinct starts {I(distinct.Count)} (civ 2 and 3 mirror civ 0 and 1).");
        L("All outcome tables count all starts, the mirror starts included; the distinct-start column repeats the headline.");
        L();

        L("== 2. World truth vs knowledge ==");
        L($"iron deposits on the map per world: {Dist(Each(worlds, w => w[0].IronOnMap))}");
        L($"iron deposits on the start's continent: {Dist(all.ConvertAll(r => r.IronOnContinent))}");
        L($"nearest iron (hex, any depth): {Dist(all.ConvertAll(r => r.NearestAny))}");
        L($"nearest depth-1 iron (hex): {Dist(all.ConvertAll(r => r.NearestDepth1))}");
        L($"nearest uncontested depth-1 iron (hex; -1 = none anywhere): {Dist(all.ConvertAll(r => r.NearestUncontestedDepth1))}");
        L($"known at MET-08 (iron within {KnownRadius} of the start): {Dist(all.ConvertAll(r => r.Known))}");
        L($"starts with iron within {KnownRadius}: {Pct(all.FindAll(r => r.Known > 0).Count, all.Count)} ({I(all.FindAll(r => r.Known > 0).Count)}/{I(all.Count)})");
        L($"starts with depth-1 iron within {KnownRadius}: {Pct(all.FindAll(r => r.KnownDepth1 > 0).Count, all.Count)}");
        L();

        L("== 3. Local iron route (first step satisfied, uncontested = strictly nearer this start than any other start) ==");
        L("step 1 depth-1 within 8 | step 2 depth-1 within 12 (TRN-05) or 16 (SEA-05, river/coast/lake site) | step 3 depth-2 within the extended reach (MIN-04)");
        L("step 4 only deposits nearer a rival start | step 5 known but beyond reach | step 6 none within 20");
        StepTable(o, "all starts", all);
        StepTable(o, "distinct starts (civ 0, 1)", distinct);
        int step2 = all.FindAll(r => r.Step == IronStep.CartsOrWater).Count;
        int onlyWater = all.FindAll(r => r.Step == IronStep.CartsOrWater && r.D1Unc12 == 0).Count;
        L($"  of the {I(step2)} step-2 starts, {I(onlyWater)} need SEA-05 (a depth-1 site only 13-16 away on a wet start site); the rest need only TRN-05, which TRD-05 makes a KEY-4 prerequisite");
        L($"  steps 1-2 on E3 rules: {I(all.FindAll(r => r.Step <= IronStep.CartsOrWater).Count)} ({Pct(all.FindAll(r => r.Step <= IronStep.CartsOrWater).Count, all.Count)}); without SEA-05: {I(all.FindAll(r => r.Step == IronStep.StrictLocal || r.Step == IronStep.CartsOrWater && r.D1Unc12 > 0).Count)} ({Pct(all.FindAll(r => r.Step == IronStep.StrictLocal || r.Step == IronStep.CartsOrWater && r.D1Unc12 > 0).Count, all.Count)})");
        L($"  steps 1-3: {I(all.FindAll(r => r.Step <= IronStep.NeedsShaft).Count)} ({Pct(all.FindAll(r => r.Step <= IronStep.NeedsShaft).Count, all.Count)}) (step 3 also needs MIN-04, which KEY-4 does not require)");
        L();

        L("== 4. Mineability and camp capacity ==");
        L($"depth-1 uncontested sites within 8 per start: {Dist(all.ConvertAll(r => r.D1Unc8))}");
        L($"depth-1 uncontested sites within the extended reach per start: {Dist(all.ConvertAll(r => r.D1UncStar))}");
        L($"any-depth uncontested sites within the extended reach per start: {Dist(all.ConvertAll(r => r.AnyUncStar))}");
        L($"starts whose nearest eligible site is depth 2: {Pct(all.FindAll(r => r.Step == IronStep.NeedsShaft).Count, all.Count)}");
        double reference = OrePerAdultSeasonT11 * HorizonSeasons;
        L($"reference ore need: {OrePerAdultSeasonT11 * 100:F3} L per 100 adults per season (T11 replacement, e4_metal_oracle) = {reference * 100:F1} L over {HorizonSeasons} seasons for 100 adults;");
        L($"one deposit holds {OreLitresPerDeposit:F0} L, enough for {OreLitresPerDeposit / reference:F0} adults over the horizon; one minimum camp (2 miners, 1 L per work-season) mines 2 L/season,");
        L($"about {2 / OrePerAdultSeasonT11:F0} adults' T11 need at base rate; a full 4-miner camp {4 / OrePerAdultSeasonT11:F0} adults.");
        var sites = all.FindAll(r => r.Step <= IronStep.CartsOrWater);
        L($"adults whose {HorizonSeasons}-season T11 ore need the start's depth-1 uncontested sites could cover (sites x 1000 L / need): {Dist(sites.ConvertAll(r => (int)(r.D1UncStar * OreLitresPerDeposit / reference)))}");
        var haul = sites.ConvertAll(r => (int)Math.Round(r.SiteHex * OrePerAdultSeasonT11 * 100 * HaulWsPerLitreTile / (100 * AdultWsPerSeason) * 10_000));
        L($"haul labor to the nearest eligible site, 100 adults at T11, basis points of adult work: {Dist(haul)}");
        var walkable = sites.FindAll(r => r.SiteWalkTenths != int.MaxValue);
        L($"walking tiles to the cheapest eligible site (Dijkstra tenths / 10, where a route exists): {Dist(walkable.ConvertAll(r => r.SiteWalkTenths / 10))}");
        int unreachable = sites.Count - walkable.Count;
        L($"starts whose every eligible site has no walking route at all (steps 1-2 only): {I(unreachable)}; with a route longer than 40 tiles: {I(walkable.FindAll(r => r.SiteWalkTenths > 400).Count)}");
        L($"starts with depth-1 uncontested iron inside the E3 claim floor of {ClaimFloor}: {I(all.FindAll(r => r.D1Unc4 > 0).Count)} ({Pct(all.FindAll(r => r.D1Unc4 > 0).Count, all.Count)}); wet start sites {Pct(all.FindAll(r => r.WetSite).Count, all.Count)}");
        L($"live-planner caveat: MaintainCamps opens camps only for copper, arsenical copper and tin (LivingEconomyModel.Metal.cs, MaintainCamps); iron is dug without a Mine Decree only within {SettlementRange} of a settlement site.");
        L($"starts with depth-1 uncontested iron within {SettlementRange} of the start site: {I(all.FindAll(r => r.D1Unc3 > 0).Count)} ({Pct(all.FindAll(r => r.D1Unc3 > 0).Count, all.Count)}); any depth: {I(all.FindAll(r => r.AnyUnc3 > 0).Count)} ({Pct(all.FindAll(r => r.AnyUnc3 > 0).Count, all.Count)})");
        L($"iron deposits at depth 2 per world: {Dist(Each(worlds, w => w[0].IronDepth2OnMap))}");
        L();

        L("== 5. Wood regeneration (charcoal reach 8, 12 with TRN-05; flat 2 L/yr regrowth on every tile that holds wood) ==");
        L($"forest tiles (>= 15 L) within 8, uncontested: {Dist(all.ConvertAll(r => r.Forest8))}");
        L($"forest tiles within 12, uncontested: {Dist(all.ConvertAll(r => r.Forest12))}");
        L($"wooded tiles (> 0 L) within 8: {Dist(all.ConvertAll(r => r.Wooded8))}");
        L($"adults T11 replacement wood regrowth can sustain (forest only, reach 8): {Dist(all.ConvertAll(r => (int)r.Supportable(r.Forest8, false, WoodPerAdultYearT11)))}");
        L($"adults T11 sustained, reach 12: {Dist(all.ConvertAll(r => (int)r.Supportable(r.Forest12, false, WoodPerAdultYearT11)))}");
        L($"adults T12 sustained, reach 8: {Dist(all.ConvertAll(r => (int)r.Supportable(r.Forest8, false, WoodPerAdultYearT12)))}");
        L($"adults T11 sustained, reach 8 with WOD-04 (doubles regrowth within {CoppiceRadius}): {Dist(all.ConvertAll(r => (int)r.Supportable(r.Forest8, true, WoodPerAdultYearT11)))}");
        L($"standing forest stock within 8 (L): {Dist(all.ConvertAll(r => (int)r.ForestStock8))}");
        L($"low-wood start (regrowth sustains < {LowWoodAdults} adults of T11 replacement, reach 8, forest only): {I(all.FindAll(r => r.LowWood).Count)} ({Pct(all.FindAll(r => r.LowWood).Count, all.Count)})");
        L($"very low wood (< {VeryLowWoodAdults} adults): {I(all.FindAll(r => r.VeryLowWood).Count)} ({Pct(all.FindAll(r => r.VeryLowWood).Count, all.Count)})");
        L("This counts only the iron-tool charcoal draw. E3 bronze, hearths and building draw on the same woods; the real margin is smaller.");
        var adultsT11 = all.ConvertAll(r => r.Supportable(r.Forest8, false, WoodPerAdultYearT11));
        foreach (int cut in new[] { 25, 50, 100, 200, 400 })
        {
            int below = adultsT11.FindAll(a => a < cut).Count, belowHalf = adultsT11.FindAll(a => a * 0.5 < cut).Count;
            L($"  regrowth sustains fewer than {cut} adults (T11, reach 8): {I(below)} ({Pct(below, all.Count)}); if half the regrowth is already committed to other uses: {I(belowHalf)} ({Pct(belowHalf, all.Count)})");
        }
        var sortedForest = all.ConvertAll(r => r.Forest8);
        sortedForest.Sort();
        int decile = Percentile(sortedForest, 10);
        L($"relative stratum: bottom decile by forest tiles within 8 (<= {decile} tiles; set after the 40-seed pilot, see report): {I(all.FindAll(r => r.Forest8 <= decile).Count)}");
        L();

        L("== 6. Inland and low-wood strata ==");
        L($"nearest coast-adjacent tile (hex): {Dist(all.ConvertAll(r => r.CoastDist))}");
        L($"nearest river or lake tile (hex): {Dist(all.ConvertAll(r => r.WaterDist))}");
        var coastal = all.FindAll(r => r.CoastDist <= 8);
        var inland = all.FindAll(r => r.CoastDist > 8 && r.CoastDist <= 20);
        var landlocked = all.FindAll(r => r.CoastDist > 20);
        var deepInland = all.FindAll(r => r.CoastDist >= 15);
        L($"coastal (<= 8): {I(coastal.Count)}; inland (9-20): {I(inland.Count)}; landlocked (> 20, no sea access inside the 20-tile start guarantee): {I(landlocked.Count)}");
        StepTable(o, "coastal starts", coastal);
        StepTable(o, "inland starts", inland);
        StepTable(o, "landlocked starts", landlocked);
        StepTable(o, "deep inland starts (coast 15-20 away)", deepInland);
        StepTable(o, "low-wood starts", all.FindAll(r => r.LowWood));
        StepTable(o, "very-low-wood starts", all.FindAll(r => r.VeryLowWood));
        StepTable(o, "inland and low-wood starts", all.FindAll(r => r.CoastDist > 8 && r.LowWood));
        StepTable(o, "bottom-decile forest starts", all.FindAll(r => r.Forest8 <= decile));
        StepTable(o, "zero forest within 8", all.FindAll(r => r.Forest8 == 0));
        StepTable(o, "inland and bottom-decile forest", all.FindAll(r => r.CoastDist > 8 && r.Forest8 <= decile));
        L("  joint iron route (step 1-2) by wood status:");
        foreach ((string name, Func<Rec, bool> pick) in new (string, Func<Rec, bool>)[]
                 { ("reach-8 regrowth >= 100 adults", r => !r.LowWood), ("50-99 adults", r => r.LowWood && !r.VeryLowWood), ("< 50 adults", r => r.VeryLowWood) })
        {
            var g = all.FindAll(r => pick(r));
            int ok = g.FindAll(r => r.Step <= IronStep.CartsOrWater).Count;
            int okShaft = g.FindAll(r => r.Step <= IronStep.NeedsShaft).Count;
            L($"    {name}: n={I(g.Count)}; step 1-2 {I(ok)} ({Pct(ok, g.Count)}); step 1-3 {I(okShaft)} ({Pct(okShaft, g.Count)})");
        }
        L();

        L("== 7. Import alternatives (geography only; no contact, relations or iron TradeGood is modelled here) ==");
        L("capable partner = its own start reaches step 1 or 2 on E3 rules. Route = unit-cost walk (Travel) else sail with SEA-04 (11 tiles from land), tiles / 10 = seasons.");
        L($"mate on the same continent: {Pct(all.FindAll(r => r.SameContinentMate).Count, all.Count)}");
        L($"mate capable of exporting: {Pct(all.FindAll(r => r.MateCapable).Count, all.Count)}; walkable to mate: {Pct(all.FindAll(r => r.MateWalk != int.MaxValue).Count, all.Count)}");
        L($"mate walk tiles: {Dist(all.FindAll(r => r.MateWalk != int.MaxValue).ConvertAll(r => r.MateWalk))}");
        L($"mate sail tiles (if walking is impossible): {Dist(all.FindAll(r => r.MateWalk == int.MaxValue && r.MateSail != int.MaxValue).ConvertAll(r => r.MateSail))}");
        L($"other-continent partner capable: {Pct(all.FindAll(r => r.OtherCapable).Count, all.Count)}; walkable: {Pct(all.FindAll(r => r.OtherWalk != int.MaxValue).Count, all.Count)}; sailable: {Pct(all.FindAll(r => r.OtherSail != int.MaxValue).Count, all.Count)}");
        L($"other-continent sail tiles: {Dist(all.FindAll(r => r.OtherSail != int.MaxValue).ConvertAll(r => r.OtherSail))}");
        L($"other-continent sail seasons: {Dist(all.FindAll(r => r.OtherSail != int.MaxValue).ConvertAll(r => (r.OtherSail + TilesPerSeason - 1) / TilesPerSeason))}");
        L();

        L("== 8. Route outcome (geography; first applicable) ==");
        L("local = steps 1-3 on E3 technology | ownership-dependent = step 4 | needs a nearer settlement = step 5 | trade only = no local route but a capable partner is");
        L("walkable (mate) or sailable (other continent) | none = no local route and no geographic trade route");
        RouteTable(o, "all starts", all);
        RouteTable(o, "distinct starts (civ 0, 1)", distinct);
        RouteTable(o, "landlocked starts", landlocked);
        RouteTable(o, "deep inland starts (coast 15-20 away)", deepInland);
        RouteTable(o, "inland starts", inland);
        RouteTable(o, "low-wood starts", all.FindAll(r => r.LowWood));
        L();

        int worldsAffected = 0, worldsNoRoute = 0;
        foreach (Rec[] w in worlds)
        {
            bool any = false, none = false;
            foreach (Rec r in w)
            {
                if (r.Step > IronStep.NeedsShaft) any = true;
                if (r.Step > IronStep.NeedsShaft && !TradeFeasible(r)) none = true;
            }
            if (any) worldsAffected++;
            if (none) worldsNoRoute++;
        }
        L($"worlds with at least one start outside steps 1-3: {I(worldsAffected)} of {I(seeds)}; with at least one such start that also has no geographic trade route: {I(worldsNoRoute)}");
        var failing = all.FindAll(r => r.Step > IronStep.CartsOrWater);
        L("== 9. Starts without a step 1-2 local route (first 25 by seed) ==");
        int shown = 0;
        foreach (Rec r in failing)
        {
            if (shown++ >= 25) break;
            L($"seed {r.Seed} civ {r.Civ}: step {(int)r.Step} {r.Step}, known {r.Known}, nearest uncontested depth-1 {r.NearestUncontestedDepth1}, wet site {r.WetSite}, mate walk {Tiles(r.MateWalk)}");
        }
        if (failing.Count == 0) L("none");
        return o.ToString();
    }

    private static string LocalKey(Rec r)
        => string.Join(',', r.IronOnMap, r.IronOnContinent, r.NearestAny, r.NearestDepth1, r.NearestUncontestedDepth1, r.Known, r.KnownDepth1,
            r.D1Unc3, r.AnyUnc3, r.D1Unc4, r.D1Unc8, r.D1Unc12, r.D1UncStar, r.AnyUnc8, r.AnyUncStar, r.AnyStar, r.IronDepth2OnMap, r.WetSite, r.Star, (int)r.Step, r.SiteHex, r.SiteDepth,
            r.SiteWalkTenths, r.Forest8, r.Forest12, r.Wooded8, r.Wooded12, r.Forest3, r.CoastDist, r.WaterDist);

    private static string Tiles(int tiles) => tiles == int.MaxValue ? "none" : tiles.ToString(CultureInfo.InvariantCulture);

    private static IEnumerable<int> Each(Rec[][] worlds, Func<Rec[], int> pick)
    {
        foreach (Rec[] w in worlds) yield return pick(w);
    }

    private static void StepTable(StringBuilder o, string label, List<Rec> group)
    {
        o.Append($"  {label} (n={group.Count:N0}):");
        if (group.Count == 0) { o.Append(" empty\n"); return; }
        for (IronStep s = IronStep.StrictLocal; s <= IronStep.NoneKnown; s++)
        {
            int n = group.FindAll(r => r.Step == s).Count;
            o.Append($" s{(int)s} {n:N0} ({Pct(n, group.Count)})");
        }
        o.Append('\n');
    }

    private static bool TradeFeasible(Rec r)
        => r.MateCapable && (r.MateWalk != int.MaxValue || r.MateSail != int.MaxValue) ||
           r.OtherCapable && (r.OtherWalk != int.MaxValue || r.OtherSail != int.MaxValue);

    private static void RouteTable(StringBuilder o, string label, List<Rec> group)
    {
        o.Append($"  {label} (n={group.Count:N0}):");
        if (group.Count == 0) { o.Append(" empty\n"); return; }
        int local = group.FindAll(r => r.Step <= IronStep.NeedsShaft).Count;
        int ownership = group.FindAll(r => r.Step == IronStep.Contested).Count;
        int expand = group.FindAll(r => r.Step >= IronStep.BeyondReach && r.Step != IronStep.NoneKnown).Count;
        int none = group.FindAll(r => r.Step == IronStep.NoneKnown).Count;
        int tradeOnly = group.FindAll(r => r.Step >= IronStep.Contested && TradeFeasible(r)).Count;
        int noRoute = group.FindAll(r => r.Step >= IronStep.Contested && !TradeFeasible(r)).Count;
        o.Append($" local {local:N0} ({Pct(local, group.Count)}); ownership-dependent {ownership:N0}; needs nearer settlement {expand:N0}; no iron known {none:N0};");
        o.Append($" of the {ownership + expand + none:N0} non-local starts, trade-feasible {tradeOnly:N0} and no route {noRoute:N0}\n");
        int viaMate = group.FindAll(r => r.Step >= IronStep.Contested && r.MateCapable && (r.MateWalk != int.MaxValue || r.MateSail != int.MaxValue)).Count;
        int viaOtherOnly = group.FindAll(r => r.Step >= IronStep.Contested && !(r.MateCapable && (r.MateWalk != int.MaxValue || r.MateSail != int.MaxValue)) &&
            r.OtherCapable && (r.OtherWalk != int.MaxValue || r.OtherSail != int.MaxValue)).Count;
        o.Append($"    non-local starts whose capable partner is the walkable mate: {viaMate:N0}; reachable only by sea from the other continent: {viaOtherOnly:N0}\n");
    }

    // ---- live probe (own-era exposure and actual contacts) ------------------------------------------------

    private sealed class LiveSample
    {
        public ulong Seed;
        public int Civ;
        public bool ReachedKey4;
        public double Met08Minute = double.NaN, Key3Minute = double.NaN, Key4Minute = double.NaN, SampleMinute;
        public int Settlements, KnownIron, OwnedIron, OwnedIronWorked, NearIronWorked;
        public double OreMinedOwned, OreMinedNear, BloomStock;
        public bool MateContacted, MateTouching, OtherContacted;
        public bool Min04, Trn05, Sea05, Wod04;
        public int IronCamps, IronCampsBuilt;
        public int Mate;
    }

    /// <summary>
    /// <c>iron-live</c>: the same planner-only stand-in as the E3 metal report (four Balanced civilizations on the Council), sampled for
    /// each civilization once at its own KEY-4 (or at the minute limit if it never gets there, reported as incomplete). It reads iron
    /// that was actually mined and the contacts and borders that actually existed. It is not an AI tournament and not a human game.
    /// <code>civsim-harness iron-live --pack FILE [--seed N] [--seeds N] [--minutes N]</code>
    /// </summary>
    public static int RunLive(string[] args)
    {
        string? packPath = null;
        ulong first = 20261019;
        int seeds = 24, minutes = 80;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pack": packPath = Next(args, ref i); break;
                case "--seed": first = ulong.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--seeds": seeds = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--minutes": minutes = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                default:
                    Console.Error.WriteLine("usage: civsim-harness iron-live --pack FILE [--seed N] [--seeds N] [--minutes N]");
                    return 2;
            }
        }
        if (packPath is null) throw new ArgumentException("iron-live needs --pack FILE.");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));

        var perSeed = new List<LiveSample>[seeds];
        Parallel.For(0, seeds, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, i =>
        {
            perSeed[i] = PlayLive(first + (ulong)i, pack, minutes);
            Console.Error.WriteLine($"[done] seed {first + (ulong)i}");
        });

        var all = new List<LiveSample>();
        foreach (List<LiveSample> list in perSeed) all.AddRange(list);
        var o = new StringBuilder();
        void L(string text = "") => o.Append(text).Append('\n');
        string Q(List<double> v, int pc) { v.Sort(); return v.Count == 0 ? "n/a" : v[Math.Min(v.Count - 1, (int)((long)(v.Count - 1) * pc / 100))].ToString("F1", CultureInfo.InvariantCulture); }
        string Qi(List<int> v, int pc) { v.Sort(); return v.Count == 0 ? "n/a" : v[Math.Min(v.Count - 1, (int)((long)(v.Count - 1) * pc / 100))].ToString(CultureInfo.InvariantCulture); }
        string Spread(List<double> v) => $"p10 {Q(v, 10)} p50 {Q(v, 50)} p90 {Q(v, 90)} max {Q(v, 100)} (n={v.Count})";
        string SpreadI(List<int> v) => $"p10 {Qi(v, 10)} p50 {Qi(v, 50)} p90 {Qi(v, 90)} max {Qi(v, 100)} (n={v.Count})";

        var done = all.FindAll(x => x.ReachedKey4);
        L($"iron-live seeds {first}..{first + (ulong)seeds - 1} ({seeds} matches, {all.Count} civilizations), minute limit {minutes}, planner-only Balanced stand-in");
        L($"own KEY-4 reached: {done.Count}/{all.Count}; incomplete exposures (no KEY-4 inside the limit): {all.Count - done.Count}. Incomplete civilizations are excluded from the KEY-4 rows below, never counted as zero.");
        L($"MET-08 minute: {Spread(done.ConvertAll(x => x.Met08Minute))}");
        L($"KEY-3 minute: {Spread(done.ConvertAll(x => x.Key3Minute))}");
        L($"KEY-4 minute: {Spread(done.ConvertAll(x => x.Key4Minute))}");
        L($"own E3 exposure (KEY-3 to KEY-4, minutes): {Spread(done.ConvertAll(x => x.Key4Minute - x.Key3Minute))}");
        L($"MET-08 to KEY-4 (minutes): {Spread(done.ConvertAll(x => x.Key4Minute - x.Met08Minute))}");
        L($"settlements at own KEY-4: {SpreadI(done.ConvertAll(x => x.Settlements))}");
        L($"iron deposits known (within 20 of an own settlement) at own KEY-4: {SpreadI(done.ConvertAll(x => x.KnownIron))}");
        L($"iron deposits inside own territory at own KEY-4: {SpreadI(done.ConvertAll(x => x.OwnedIron))}");
        int worked = done.FindAll(x => x.OwnedIronWorked > 0).Count, workedNear = done.FindAll(x => x.NearIronWorked > 0).Count;
        L($"civilizations that had worked an owned iron deposit by their own KEY-4: {worked}/{done.Count}; any deposit within 20: {workedNear}/{done.Count}");
        L($"civilizations with an iron mining camp open at own KEY-4: {done.FindAll(x => x.IronCamps > 0).Count}/{done.Count} (built {done.FindAll(x => x.IronCampsBuilt > 0).Count}/{done.Count}); camps opened by the planner never target iron, so any seen here came from a decree");
        L($"litres mined from owned iron by own KEY-4: {Spread(done.ConvertAll(x => x.OreMinedOwned))}");
        L($"litres mined from iron within 20 of an own settlement: {Spread(done.ConvertAll(x => x.OreMinedNear))}");
        L($"iron bloom in stock at own KEY-4 (L, all settlements): {Spread(done.ConvertAll(x => x.BloomStock))}");
        int mateContact = done.FindAll(x => x.MateContacted).Count, mateTouch = done.FindAll(x => x.MateTouching).Count, other = done.FindAll(x => x.OtherContacted).Count;
        L($"at own KEY-4: mate contacted {mateContact}/{done.Count}; mate borders touching {mateTouch}/{done.Count}; contact with a civilization on the other continent {other}/{done.Count}");
        L($"E3 projects done by own KEY-4 that the static steps lean on: TRN-05 {done.FindAll(x => x.Trn05).Count}/{done.Count}, MIN-04 {done.FindAll(x => x.Min04).Count}/{done.Count}, SEA-05 {done.FindAll(x => x.Sea05).Count}/{done.Count}, WOD-04 {done.FindAll(x => x.Wod04).Count}/{done.Count}");
        int noContact = done.FindAll(x => !x.MateContacted && !x.OtherContacted).Count;
        L($"at own KEY-4 with no contact of any kind: {noContact}/{done.Count}");
        L("per civilization (seed civ key4-minute settlements owned-iron worked mined-L bloom mate-contact mate-touch other-contact):");
        foreach (LiveSample x in all)
            L($"  {x.Seed} c{x.Civ} {(x.ReachedKey4 ? x.Key4Minute.ToString("F1", CultureInfo.InvariantCulture) : "incomplete@" + x.SampleMinute.ToString("F1", CultureInfo.InvariantCulture))} " +
              $"{x.Settlements} {x.OwnedIron} {x.OwnedIronWorked} {x.OreMinedOwned.ToString("F0", CultureInfo.InvariantCulture)} {x.BloomStock.ToString("F1", CultureInfo.InvariantCulture)} " +
              $"{(x.MateContacted ? 1 : 0)} {(x.MateTouching ? 1 : 0)} {(x.OtherContacted ? 1 : 0)}");
        Console.Write(o.ToString());
        return 0;
    }

    private static List<LiveSample> PlayLive(ulong seed, ContentPack pack, int minutes)
    {
        var archetypes = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        GeneratedWorld world = WorldGenerator.Generate(seed);
        MatchModel match = MatchModel.Create(seed, pack, world, archetypes);
        WorldEconomyInput econ = WorldEconomyInput.FromGeneratedWorld(world);
        var iron = new List<int>();
        for (int t = 0; t < world.TileCount; t++) if (econ.Deposit(t).Kind == DepositKind.Iron) iron.Add(t);
        var runner = new SimRunner(match);
        ResearchSystem r = match.Research;
        LivingEconomyModel eco = match.Economy;
        PoliticsSystem pol = match.Politics;
        int civs = r.CivCount;
        int key3 = r.Catalog.IndexOf("KEY-3"), key4 = r.Catalog.IndexOf("KEY-4"), met08 = r.Catalog.IndexOf("MET-08");
        var samples = new LiveSample[civs];
        long limit = minutes * 600L;
        while (runner.Tick < limit)
        {
            runner.Advance(1);
            if (runner.Tick % ResearchSystem.SeasonTicks != 0) continue;
            bool all = true;
            for (int c = 0; c < civs; c++)
            {
                if (samples[c] is not null) continue;
                if (!r.IsDone(c, key4)) { all = false; continue; }
                samples[c] = Sample(seed, c, true, runner.Tick, r, eco, pol, world, iron, econ, key3, key4, met08);
            }
            if (all) break;
        }
        for (int c = 0; c < civs; c++)
            samples[c] ??= Sample(seed, c, false, runner.Tick, r, eco, pol, world, iron, econ, key3, key4, met08);
        return new List<LiveSample>(samples);
    }

    private static LiveSample Sample(ulong seed, int c, bool reached, long tick, ResearchSystem r, LivingEconomyModel eco, PoliticsSystem pol,
        GeneratedWorld world, List<int> iron, WorldEconomyInput econ, int key3, int key4, int met08)
    {
        double Minute(int project) => project >= 0 && r.IsDone(c, project) ? r.CompletedTick(c, project) / 600.0 : double.NaN;
        var x = new LiveSample
        {
            Seed = seed, Civ = c, ReachedKey4 = reached, Mate = c ^ 1, SampleMinute = tick / 600.0,
            Met08Minute = Minute(met08), Key3Minute = Minute(key3), Key4Minute = Minute(key4),
        };
        var sites = new List<int>();
        for (int s = 0; s < eco.SettlementCount; s++)
        {
            var lite = eco.SettlementLite(s);
            if (lite.Civilization != c || pol.IsFreeFolk(s) || eco.Population(lite.Id) <= 0) continue;
            sites.Add(lite.SiteTile);
            x.BloomStock += eco.GoodStock(s, "G-IRON").ToDoubleForDisplay();
        }
        x.Settlements = sites.Count;
        foreach (int t in iron)
        {
            bool known = false;
            foreach (int site in sites)
                if (HexGrid.Distance(site, t, world.Width) <= KnownRadius) { known = true; break; }
            double mined = OreLitresPerDeposit - eco.OreRemaining(t).ToDoubleForDisplay();
            bool owned = pol.Territory.OwnerOf(t) == c;
            if (known) { x.KnownIron++; x.OreMinedNear += mined; if (eco.DepositWorked(t)) x.NearIronWorked++; }
            if (owned) { x.OwnedIron++; x.OreMinedOwned += mined; if (eco.DepositWorked(t)) x.OwnedIronWorked++; }
        }
        for (int k = 0; k < eco.MiningCampCount; k++)
        {
            MiningCampView camp = eco.MiningCamp(k);
            if (camp.Civilization != c || camp.Deposit != DepositKind.Iron) continue;
            x.IronCamps++;
            if (camp.Built) x.IronCampsBuilt++;
        }
        x.Min04 = r.IsDone(c, r.Catalog.IndexOf("MIN-04"));
        x.Trn05 = r.IsDone(c, r.Catalog.IndexOf("TRN-05"));
        x.Sea05 = r.IsDone(c, r.Catalog.IndexOf("SEA-05"));
        x.Wod04 = r.IsDone(c, r.Catalog.IndexOf("WOD-04"));
        x.MateContacted = pol.Diplomacy.Contacted(c, x.Mate);
        x.MateTouching = TouchingOwners(pol.Territory, c, x.Mate);
        for (int o = 0; o < r.CivCount; o++)
            if (o != c && o != x.Mate && pol.Diplomacy.Contacted(c, o)) x.OtherContacted = true;
        return x;
    }

    private static bool TouchingOwners(Territory t, int a, int b)
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
}
