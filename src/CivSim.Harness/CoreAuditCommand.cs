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
using CivSim.Sim.View;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>
/// <c>core-audit</c> (docs/MVP/31 §1.2, §6, §9 WP0; docs/MVP/31b §3.1): a read-only audit of settlement cores and their land cost.
/// Three report sections are available:
/// <list type="bullet">
/// <item><b>Static</b> (world generation only, seeds <c>first .. first+pack-seeds-1</c>): greedy packing of settlement sites at
/// spacing 7, spacing 9 and spacing 7 or 9 with no valuable deposit in the core, in tile order and in radius-3 forage order (as the
/// fission chooser picks best-first); valuable deposits in the starts' cores and ranges.</item>
/// <item><b>Live</b> (Iron rules, Builder/Grower/Warlord/Sage AIs at Warlord level, omniscient frame): plots by ring distance from
/// their settlement's centre (fields, orchards, terraces, mining camps; pastures are counts only), deposits inside each living
/// settlement's core, and best-k field yield over the whole radius-3 range against the range without the 7 core hexes.</item>
/// <item><b>Outer</b> (<c>--outer</c>): the enforced ring-two claims, moves and releases, invalid or overlapping claims, plots and seasonal
/// camps on claims, and best-k field yield with and without claimed hexes.</item>
/// </list>
/// It changes nothing in the matches it plays and never writes the generated worlds. The report is a function of the arguments alone.
/// <code>
/// civsim-harness core-audit --pack FILE [--seeds N] [--first-seed N] [--minutes a,b,c] [--pack-seeds N] [--static-only | --live-only] [--outer]
/// </code>
/// </summary>
internal static class CoreAuditCommand
{
    private const string UsageText =
        "usage: civsim-harness core-audit --pack FILE [--seeds N] [--first-seed N] [--minutes a,b,c] [--pack-seeds N] [--static-only | --live-only] [--outer]";

    /// <summary>Today's minimum distance between settlement sites, and the owner's non-overlapping radius-4 ring (docs/MVP/31 §6.2).</summary>
    public const int SpacingToday = 7, SpacingRing = 9;
    /// <summary>The core: the centre hex and ring 1 (7 hexes).</summary>
    public const int CoreRadius = 1;
    /// <summary>LivingEconomyModel.Radius: the working range every settlement uses.</summary>
    public const int RangeRadius = 3;
    private const int RingBuckets = 6; // 0..4 and 5+

    private static readonly EconomyArchetype[] Archetypes =
        { EconomyArchetype.Builder, EconomyArchetype.Grower, EconomyArchetype.Warlord, EconomyArchetype.Sage };

    public static int Run(string[] args)
    {
        string? packPath = null;
        int seeds = 3, first = 1, packSeeds = 12;
        int[] minutes = { 100, 120 };
        bool runStatic = true, runLive = true, includeOuter = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pack": packPath = Next(args, ref i); break;
                case "--seeds": seeds = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--first-seed": first = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--pack-seeds": packSeeds = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--minutes":
                    minutes = Next(args, ref i).Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(m => int.Parse(m, CultureInfo.InvariantCulture)).Distinct().OrderBy(m => m).ToArray();
                    break;
                case "--static-only": runLive = false; break;
                case "--live-only": runStatic = false; break;
                case "--outer": includeOuter = true; break;
                default:
                    Console.Error.WriteLine(UsageText);
                    return 2;
            }
        }
        if (seeds < 0 || packSeeds < 0 || first < 1 || minutes.Length == 0 || minutes[0] <= 0)
            throw new ArgumentException("core-audit needs positive seeds and minutes.");

        if (runStatic) Console.Write(StaticReport(first, packSeeds));
        if (runLive && seeds > 0)
        {
            if (packPath is null) throw new ArgumentException("core-audit needs --pack FILE for the live part (or pass --static-only).");
            ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
            var totals = new YieldTotals[minutes.Length];
            for (int m = 0; m < minutes.Length; m++) totals[m] = new YieldTotals();
            var outerTotals = new OuterTotals[minutes.Length];
            if (includeOuter) for (int m = 0; m < minutes.Length; m++) outerTotals[m] = new OuterTotals();
            Console.WriteLine();
            Console.WriteLine("== live: Iron rules, 4 Warlord-level AIs (Builder, Grower, Warlord, Sage), omniscient frame ==");
            for (int n = 0; n < seeds; n++)
                Console.Write(LiveReport(pack, (ulong)(first + n), minutes, totals, includeOuter ? outerTotals : null));
            Console.WriteLine();
            Console.WriteLine("== best-k field yield over all live seeds (k = fields each settlement holds; base tile yield per year) ==");
            for (int m = 0; m < minutes.Length; m++) Console.WriteLine(totals[m].Describe(minutes[m], seeds));
            if (includeOuter)
            {
                Console.WriteLine();
                Console.WriteLine("== enforced outer-core rule (docs/MVP/31b §5) ==");
                for (int m = 0; m < minutes.Length; m++) Console.WriteLine(outerTotals[m].Describe(minutes[m], seeds));
            }
        }
        return 0;
    }

    // ---- shared helpers ---------------------------------------------------------------------------------

    /// <summary>Land other than coast, ice and peaks: the rule of the economy's <c>IsHabitable</c>.</summary>
    public static bool Habitable(Biome b) => b is not (Biome.Ocean or Biome.Coast or Biome.Ice or Biome.Peak);

    /// <summary>Metal, salt and coal: the deposits the plan keeps out of a new settlement's core (docs/MVP/31 §3.1).</summary>
    public static bool Valuable(DepositKind k) => k is DepositKind.NativeCopper or DepositKind.CopperOre or DepositKind.ArsenicalCopper
        or DepositKind.Tin or DepositKind.Gold or DepositKind.Silver or DepositKind.Iron or DepositKind.Salt or DepositKind.Coal;

    public static bool Stoneworks(DepositKind k) => k is DepositKind.Stone or DepositKind.Flint or DepositKind.Clay;

    /// <summary>Tiles within <paramref name="r"/> of <paramref name="center"/>, ascending index.</summary>
    public static void Disk(int center, int r, int w, int h, List<int> into)
    {
        into.Clear();
        int x = center % w, y = center / w;
        for (int ny = Math.Max(0, y - r); ny <= Math.Min(h - 1, y + r); ny++)
            for (int nx = Math.Max(0, x - r - 1); nx <= Math.Min(w - 1, x + r + 1); nx++)
            {
                int t = ny * w + nx;
                if (HexGrid.Distance(center, t, w) <= r) into.Add(t);
            }
    }

    /// <summary>Greedy packing: walk <paramref name="order"/>, take every allowed tile not blocked, block everything nearer than <paramref name="spacing"/>.</summary>
    public static int Pack(int w, int h, int spacing, Func<int, bool> allowed, int[] order)
    {
        var blocked = new bool[w * h];
        var disk = new List<int>();
        int n = 0;
        foreach (int t in order)
        {
            if (blocked[t] || !allowed(t)) continue;
            n++;
            Disk(t, spacing - 1, w, h, disk);
            foreach (int b in disk) blocked[b] = true;
        }
        return n;
    }

    private static bool AnyValuable(WorldTile[] tiles, List<int> disk)
    {
        foreach (int r in disk) if (Valuable(tiles[r].Deposit)) return true;
        return false;
    }

    private static string Next(string[] args, ref int i)
    {
        if (i + 1 >= args.Length) throw new ArgumentException($"{args[i]} needs a value.");
        return args[++i];
    }

    private static string Pct(long part, long whole) => whole <= 0 ? "-" : (100.0 * part / whole).ToString("F1", CultureInfo.InvariantCulture) + "%";

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    // ---- static: world generation only ------------------------------------------------------------------

    /// <summary>Packing and start-core statistics for world-generation seeds <paramref name="first"/> .. first+count−1.</summary>
    public static string StaticReport(int first, int count)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Inv($"== static: greedy packing of settlement sites, seeds {first}..{first + count - 1} (docs/MVP/31 §6.2, §6.3) =="));
        sb.AppendLine("  tile order = ascending tile index; food order = radius-3 forage score, best first (as ChooseFissionSite); clean = no metal, salt or coal in the 7-hex core");
        sb.AppendLine(" seed  habitable | tile7 tile9 | food7 food9 | clean7 clean9 | starts: deposit-in-core in-range | habitable tiles whose core holds: valuable stone/flint/clay");
        long s7 = 0, s9 = 0, f7 = 0, f9 = 0, c7 = 0, c9 = 0;
        int done = 0, startsN = 0, startsCore = 0, startsRange = 0;
        double dirtyShare = 0, stoneShare = 0;
        var disk = new List<int>();
        for (int seed = first; seed < first + count; seed++)
        {
            GeneratedWorld g;
            try { g = WorldGenerator.Generate((ulong)seed); }
            catch (InvalidOperationException) { sb.AppendLine(Inv($"{seed,5}  world generation failed; skipped")); continue; }
            int w = g.Width, h = g.Height, n = g.TileCount;
            WorldTile[] tiles = g.Tiles.ToArray();
            bool Ok(int t) => Habitable(tiles[t].Biome);
            int hab = 0;
            for (int t = 0; t < n; t++) if (Ok(t)) hab++;

            var food = new int[n];
            var dirty = new bool[n];
            int dirtyN = 0, stoneyN = 0;
            for (int t = 0; t < n; t++)
            {
                if (!Ok(t)) continue;
                Disk(t, RangeRadius, w, h, disk);
                foreach (int r in disk) food[t] += tiles[r].ForageScore;
                Disk(t, CoreRadius, w, h, disk);
                bool v = false, s = false;
                foreach (int r in disk) { v |= Valuable(tiles[r].Deposit); s |= Stoneworks(tiles[r].Deposit); }
                dirty[t] = v;
                if (v) dirtyN++;
                if (s) stoneyN++;
            }
            int[] scan = Enumerable.Range(0, n).ToArray();
            int[] byFood = Enumerable.Range(0, n).OrderByDescending(t => food[t]).ThenBy(t => t).ToArray();
            bool Clean(int t) => Ok(t) && !dirty[t];

            int p7 = Pack(w, h, SpacingToday, Ok, scan), p9 = Pack(w, h, SpacingRing, Ok, scan);
            int p7f = Pack(w, h, SpacingToday, Ok, byFood), p9f = Pack(w, h, SpacingRing, Ok, byFood);
            int p7c = Pack(w, h, SpacingToday, Clean, byFood), p9c = Pack(w, h, SpacingRing, Clean, byFood);

            int vc = 0, vr = 0;
            foreach (var st in g.Starts)
            {
                startsN++;
                Disk(st.Tile, CoreRadius, w, h, disk);
                if (AnyValuable(tiles, disk)) vc++;
                Disk(st.Tile, RangeRadius, w, h, disk);
                if (AnyValuable(tiles, disk)) vr++;
            }
            int startCount = g.Starts.Length;
            string dirtyPct = Pct(dirtyN, hab), stoneyPct = Pct(stoneyN, hab);
            sb.AppendLine(Inv($"{seed,5} {hab,10} | {p7,5} {p9,5} | {p7f,5} {p9f,5} | {p7c,6} {p9c,6} | {vc,8}/{startCount} {vr,8}/{startCount} | {dirtyPct,8} {stoneyPct,8}"));
            s7 += p7; s9 += p9; f7 += p7f; f9 += p9f; c7 += p7c; c9 += p9c;
            startsCore += vc; startsRange += vr; done++;
            dirtyShare += 100.0 * dirtyN / hab; stoneShare += 100.0 * stoneyN / hab;
        }
        if (done == 0) return sb.AppendLine("no seed generated a world").ToString();
        double d = done;
        sb.AppendLine(Inv($"mean over {done} worlds:"));
        sb.AppendLine(Inv($"  tile order:  spacing 7 {s7 / d:F1}, spacing 9 {s9 / d:F1} ({100.0 * (s9 - s7) / s7:+0.0;-0.0}%)"));
        sb.AppendLine(Inv($"  food order:  spacing 7 {f7 / d:F1}, spacing 9 {f9 / d:F1} ({100.0 * (f9 - f7) / f7:+0.0;-0.0}%)"));
        sb.AppendLine(Inv($"  clean core:  spacing 7 {c7 / d:F1} ({100.0 * (c7 - f7) / f7:+0.0;-0.0}% vs food order), spacing 9 {c9 / d:F1}"));
        sb.AppendLine(Inv($"  starts with a valuable deposit in the core {startsCore} of {startsN}, in range {startsRange} of {startsN}"));
        sb.AppendLine(Inv($"  habitable tiles whose core holds a valuable deposit {dirtyShare / d:F1}%, a stone, flint or clay deposit {stoneShare / d:F1}%"));
        return sb.ToString();
    }

    // ---- live: Iron-rules matches -----------------------------------------------------------------------

    /// <summary>Sums of best-k field yield (raw Q32.32) across seeds for one minute.</summary>
    internal sealed class YieldTotals
    {
        public long Settlements, Fields;
        public decimal Range, RangeNoCore, Radius4NoCore;
        public decimal ForageAll, ForageCore, ForageRadius4NoCore;
        public long ForestAll, ForestCore;

        public string Describe(int minute, int seeds)
        {
            static string Rel(decimal a, decimal b) => b == 0 ? "-" : (100m * (a / b - 1m)).ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + "%";
            return Inv($"minute {minute}, {seeds} seeds, {Settlements} settlements, {Fields} fields: range r0-3 {Range / Scale:F0}; ") +
                Inv($"without the core r2-3 {RangeNoCore / Scale:F0} ({Rel(RangeNoCore, Range)}); r2-4 {Radius4NoCore / Scale:F0} ({Rel(Radius4NoCore, Range)})") +
                Inv($" | pooled forage+fish: core share of r0-3 {Pct(ForageCore, ForageAll)}, r2-4 vs r0-3 {Rel(ForageRadius4NoCore, ForageAll)}; forest tiles in the core {ForestCore} of {ForestAll} in range");
        }

        private static string Pct(decimal a, decimal b) => b == 0 ? "-" : (100m * a / b).ToString("F1", CultureInfo.InvariantCulture) + "%";
        private const decimal Scale = 4294967296m;
    }

    /// <summary>Aggregated validation counters for the enforced outer-core rule.</summary>
    internal sealed class OuterTotals
    {
        private static readonly string[] TierNames = { "Village/Band", "Town", "City" };
        private readonly long[] _settlements = new long[3], _growing = new long[3], _claims = new long[3], _homes = new long[3];
        private readonly long[] _unhoused = new long[3], _homeMoved = new long[3], _plots = new long[3], _shortGround = new long[3];
        private readonly long[,] _distribution = new long[3, 10];
        private readonly long[] _yieldFields = new long[3];
        private readonly decimal[] _yieldBefore = new decimal[3], _yieldAfter = new decimal[3];
        public long Added, Released, Moved, Duplicates, Invalid, InOtherCore, SeasonalCampsOnClaim;

        public void AddSettlement(int tier, int claimed, int homes, int unhoused, int movedHomes, int plots, int shortGround,
            decimal yieldBefore, decimal yieldAfter, int fields)
        {
            _settlements[tier]++;
            if (claimed > 0) _growing[tier]++;
            _claims[tier] += claimed;
            _homes[tier] += homes;
            _unhoused[tier] += unhoused;
            _homeMoved[tier] += movedHomes;
            _distribution[tier, Math.Min(claimed, 9)]++;
            _plots[tier] += plots;
            _shortGround[tier] += shortGround;
            _yieldFields[tier] += fields;
            _yieldBefore[tier] += yieldBefore;
            _yieldAfter[tier] += yieldAfter;
        }

        public void AddClaimCheck(bool duplicate, bool invalid, bool inOtherCore)
        {
            if (duplicate) Duplicates++;
            if (invalid) Invalid++;
            if (inOtherCore) InOtherCore++;
        }

        public void AddCampOnClaim() => SeasonalCampsOnClaim++;

        public string Describe(int minute, int seeds)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Inv($"minute {minute}, {seeds} seeds: settlements; growing; claims by 0..9; mean claims; homes; moved homes; unhoused; plots on claims; short ground"));
            decimal allBefore = 0, allAfter = 0;
            long allFields = 0;
            for (int tier = 0; tier < 3; tier++)
            {
                long settlements = _settlements[tier], claims = _claims[tier];
                sb.Append(Inv($"  {TierNames[tier],-13} {settlements,5} ; {_growing[tier],5} ; "));
                for (int n = 0; n < 10; n++) sb.Append(_distribution[tier, n].ToString(CultureInfo.InvariantCulture).PadLeft(5));
                sb.Append(Inv($" ; {(settlements == 0 ? 0.0 : (double)claims / settlements),5:F2} ; {_homes[tier],5} ; {_homeMoved[tier],5} ; {_unhoused[tier],5} ; {_plots[tier],5} ; {_shortGround[tier],5}"));
                sb.AppendLine();
                allBefore += _yieldBefore[tier]; allAfter += _yieldAfter[tier]; allFields += _yieldFields[tier];
            }
            sb.AppendLine(Inv($"  events since previous sample: claims added {Added}, released {Released}, moved {Moved}"));
            sb.AppendLine(Inv($"  rule checks: duplicate tiles {Duplicates}, statically unusable {Invalid}, inside another core {InOtherCore}, seasonal camps on claims {SeasonalCampsOnClaim}"));
            sb.AppendLine(Inv($"  best-k yield, all living settlements ({allFields} fields): without outer claims {allBefore / 4294967296m:F0}; with claims {allAfter / 4294967296m:F0}; change {Relative(allAfter, allBefore)}"));
            return sb.ToString();
        }

        private static string Relative(decimal a, decimal b)
            => b == 0 ? "-" : (100m * (a / b - 1m)).ToString("+0.00;-0.00", CultureInfo.InvariantCulture) + "%";
    }

    private sealed class OuterHistory
    {
        private int[][] _previous = Array.Empty<int[]>();
        private bool _primed;
        public long Added, Released, Moved;

        public void Observe(LivingEconomyModel economy)
        {
            int count = economy.SettlementCount;
            if (_previous.Length < count)
            {
                int old = _previous.Length;
                Array.Resize(ref _previous, count);
                for (int s = old; s < count; s++)
                {
                    _previous[s] = new int[(int)CoreDistrict.Count];
                    Array.Fill(_previous[s], -1);
                }
            }
            int[] tiles = new int[(int)CoreDistrict.Count];
            byte[] districts = new byte[(int)CoreDistrict.Count];
            byte[] positions = new byte[(int)CoreDistrict.Count];
            for (int settlement = 0; settlement < count; settlement++)
            {
                int[] current = new int[(int)CoreDistrict.Count];
                Array.Fill(current, -1);
                int rows = economy.OuterCoreHexes(settlement, tiles, districts, positions);
                for (int row = 0; row < rows; row++) if (tiles[row] >= 0) current[districts[row]] = tiles[row];
                if (_primed)
                    for (int district = 0; district < current.Length; district++)
                    {
                        int before = _previous[settlement][district], now = current[district];
                        if (before < 0 && now >= 0) Added++;
                        else if (before >= 0 && now < 0) Released++;
                        else if (before >= 0 && now >= 0 && before != now) Moved++;
                    }
                _previous[settlement] = current;
            }
            _primed = true;
        }

        public (long Added, long Released, long Moved) Drain()
        {
            var result = (Added, Released, Moved);
            Added = 0; Released = 0; Moved = 0;
            return result;
        }
    }

    /// <summary>One live match on <paramref name="seed"/>, sampled at each minute (ascending).</summary>
    internal static string LiveReport(ContentPack pack, ulong seed, int[] minutes, YieldTotals[]? totals = null, OuterTotals[]? outerTotals = null)
    {
        var sb = new StringBuilder();
        GeneratedWorld g = WorldGenerator.Generate(seed);
        WorldEconomyInput eco = WorldEconomyInput.FromGeneratedWorld(g);
        var controls = Archetypes.Select(a => CivControl.Ai(Difficulty.Warlord, a)).ToArray();
        MatchModel match = MatchModel.Create(seed, pack, g, Archetypes, controls, rules: ChapterRules.Iron);
        var runner = new SimRunner(match);
        var outerHistory = outerTotals is null ? null : new OuterHistory();
        outerHistory?.Observe(match.Economy);
        var builder = new FrameBuilder(match, -1);
        var f = new MatchFrame();
        for (int m = 0; m < minutes.Length; m++)
        {
            long target = minutes[m] * 600L;
            while (outerTotals is not null && runner.Tick < target)
            {
                long nextBoundary = (runner.Tick / LivingEconomyModel.SeasonTicks + 1) * LivingEconomyModel.SeasonTicks;
                long next = Math.Min(target, nextBoundary);
                runner.Advance(next - runner.Tick);
                if (runner.Tick == nextBoundary) outerHistory!.Observe(match.Economy);
            }
            if (target > runner.Tick) runner.Advance(target - runner.Tick);
            builder.Build(match, f);
            sb.Append(Inv($"seed {seed} minute {minutes[m]}: "));
            sb.Append(DescribeFrame(g, eco, f, totals?[m]));
            if (outerTotals is not null) AccumulateOuter(g, eco, match.Economy, f, outerTotals[m], outerHistory!.Drain());
        }
        return sb.ToString();
    }

    /// <summary>The §1.2 measurements on one omniscient frame. Pure function of the world, its economy input and the frame.</summary>
    internal static string DescribeFrame(GeneratedWorld g, WorldEconomyInput eco, MatchFrame f, YieldTotals? totals)
    {
        int w = g.Width, h = g.Height;
        int maxIndex = 0;
        for (int i = 0; i < f.SettlementCount; i++) maxIndex = Math.Max(maxIndex, f.SettlementIndex[i] + 1);
        var siteOf = new int[maxIndex];
        var rowOf = new int[maxIndex];
        Array.Fill(siteOf, -1);
        Array.Fill(rowOf, -1);
        for (int i = 0; i < f.SettlementCount; i++) { siteOf[f.SettlementIndex[i]] = f.SettlementTile[i]; rowOf[f.SettlementIndex[i]] = i; }
        int SiteOf(int settlementIndex) => (uint)settlementIndex < (uint)maxIndex ? siteOf[settlementIndex] : -1;
        int Ring(int site, int tile) => site < 0 ? RingBuckets - 1 : Math.Min(RingBuckets - 1, HexGrid.Distance(site, tile, w));

        var living = new bool[f.SettlementCount];
        int livingN = 0;
        for (int i = 0; i < f.SettlementCount; i++)
            if (f.SettlementPopulation[i] > 0) { living[i] = true; livingN++; }

        // Plots by ring, and which living settlements have one on their own centre hex.
        var fields = new int[RingBuckets]; var orchards = new int[RingBuckets]; var terraces = new int[RingBuckets]; var mines = new int[RingBuckets];
        var fieldOnCentre = new bool[f.SettlementCount];
        var plotOnCentre = new bool[f.SettlementCount];
        var fieldsOf = new int[f.SettlementCount];
        void Mark(int row, int ring, bool field)
        {
            if (row < 0 || !living[row] || ring != 0) return;
            plotOnCentre[row] = true;
            if (field) fieldOnCentre[row] = true;
        }
        for (int i = 0; i < f.PlotCount; i++)
        {
            int s = f.PlotSettlement[i];
            int row = (uint)s < (uint)maxIndex ? rowOf[s] : -1;
            int d = Ring(SiteOf(s), f.PlotTile[i]);
            bool field = f.PlotKind[i] == 0;
            (field ? fields : orchards)[d]++;
            if (field && row >= 0) fieldsOf[row]++;
            Mark(row, d, field);
        }
        IronFrame iron = f.Iron;
        for (int i = 0; i < iron.TerraceTileCount; i++)
        {
            int row = iron.TerraceSettlement[i]; // a frame row, not an economy index
            int d = Ring((uint)row < (uint)f.SettlementCount ? f.SettlementTile[row] : -1, iron.TerraceTiles[i]);
            terraces[d]++;
            Mark((uint)row < (uint)f.SettlementCount ? row : -1, d, false);
        }
        for (int i = 0; i < f.CampCount; i++)
        {
            if (f.CampKind[i] != (byte)CampKind.Mining) continue;
            int s = f.CampParent[i];
            int d = Ring(SiteOf(s), f.CampTile[i]);
            mines[d]++;
            Mark((uint)s < (uint)maxIndex ? rowOf[s] : -1, d, false);
        }
        int pastureKind = LivingEconomyModel.StructureIndex("pasture");
        long pastures = 0;
        if (pastureKind >= 0)
            for (int i = 0; i < f.SettlementCount; i++)
                if (living[i]) pastures += f.SettlementStructures[i * MatchFrame.StructureKinds + pastureKind];

        // Deposits inside each living settlement's core (world truth, whether or not discovered or exhausted).
        var disk = new List<int>();
        int settlementsValuable = 0, settlementsStone = 0, valuableTiles = 0, stoneTiles = 0, workedInCore = 0, minSpacing = int.MaxValue;
        var byKind = new int[Enum.GetValues<DepositKind>().Length];
        for (int i = 0; i < f.SettlementCount; i++)
        {
            if (!living[i]) continue;
            int a = f.SettlementTile[i];
            for (int j = i + 1; j < f.SettlementCount; j++)
                if (living[j]) minSpacing = Math.Min(minSpacing, HexGrid.Distance(a, f.SettlementTile[j], w));
            Disk(a, CoreRadius, w, h, disk);
            bool v = false, s = false;
            foreach (int r in disk)
            {
                DepositKind k = g.TileAt(r).Deposit;
                if (Valuable(k)) { v = true; valuableTiles++; byKind[(int)k]++; }
                if (Stoneworks(k)) { s = true; stoneTiles++; }
                if (Valuable(k) && r < f.DepositWorked.Length && f.DepositWorked[r] != 0) workedInCore++;
            }
            if (v) settlementsValuable++;
            if (s) settlementsStone++;
        }

        long fieldsTotal = fields.Sum(), orchardsTotal = orchards.Sum(), terracesTotal = terraces.Sum(), minesTotal = mines.Sum();
        long plotsTotal = fieldsTotal + orchardsTotal + terracesTotal;
        long plots0 = fields[0] + orchards[0] + terraces[0];
        long plots01 = plots0 + fields[1] + orchards[1] + terraces[1];
        var sb = new StringBuilder();
        sb.AppendLine(Inv($"{livingN} living settlements (closest pair {(minSpacing == int.MaxValue ? "-" : minSpacing.ToString(CultureInfo.InvariantCulture))} apart)"));
        sb.AppendLine("  by ring from the centre hex       0     1     2     3     4    5+");
        sb.AppendLine(Row("fields", fields));
        sb.AppendLine(Row("orchards", orchards));
        sb.AppendLine(Row("terraces", terraces));
        sb.AppendLine(Row("mining camps", mines));
        sb.AppendLine(Inv($"  pastures: {pastures} (counts only; the simulation gives them no tile)"));
        sb.AppendLine(Inv($"  settlements with a field on the centre hex {fieldOnCentre.Count(b => b)}, with any plot (field, orchard, terrace, mining camp) there {plotOnCentre.Count(b => b)} of {livingN}"));
        sb.AppendLine(Inv($"  fields in the core (d<=1) {fields[0] + fields[1]} of {fieldsTotal} = {Pct(fields[0] + fields[1], fieldsTotal)}; orchards in ring 1 {orchards[1]} of {orchardsTotal}"));
        sb.AppendLine(Inv($"  plots (fields, orchards, terraces) within d0 {plots0} = {Pct(plots0, plotsTotal)}, within d1 {plots01} = {Pct(plots01, plotsTotal)} of {plotsTotal}; mining camps within d1 {mines[0] + mines[1]} of {minesTotal}"));
        string kinds = string.Join(", ", Enum.GetValues<DepositKind>().Where(k => byKind[(int)k] > 0).Select(k => Inv($"{k} {byKind[(int)k]}")));
        sb.AppendLine(Inv($"  settlements with a metal, salt or coal deposit in the core {settlementsValuable} ({valuableTiles} deposit tiles: {(kinds.Length == 0 ? "none" : kinds)}; {workedInCore} worked)"));
        sb.AppendLine(Inv($"  settlements with a stone, flint or clay deposit in the core {settlementsStone} ({stoneTiles} tiles)"));

        // Best-k field yield: the k best cultivable tiles in the radius-3 range, with and without the core, and with a radius-4 range.
        if (totals is not null)
        {
            var all = new List<long>(); var noCore = new List<long>(); var r4NoCore = new List<long>();
            for (int i = 0; i < f.SettlementCount; i++)
            {
                if (!living[i]) continue;
                int a = f.SettlementTile[i];
                all.Clear(); noCore.Clear(); r4NoCore.Clear();
                Disk(a, RangeRadius + 1, w, h, disk);
                foreach (int r in disk)
                {
                    int d = HexGrid.Distance(a, r, w);
                    WorldEconomyTile t = eco.Tile(r);
                    long y = t.FieldYieldPerYear.Raw;
                    decimal forage = (decimal)(t.SpringForage + t.SummerForage + t.AutumnForage + t.WinterForage).Raw + t.TotalFishGrowthPerYear.Raw;
                    bool forest = g.TileAt(r).Biome is Biome.TemperateForest or Biome.Taiga or Biome.TropicalForest or Biome.OpenWoodland;
                    if (d <= RangeRadius) { totals.ForageAll += forage; if (y > 0) all.Add(y); if (forest) totals.ForestAll++; }
                    if (d <= CoreRadius) { totals.ForageCore += forage; if (forest) totals.ForestCore++; }
                    if (d > CoreRadius) totals.ForageRadius4NoCore += forage;
                    if (d > CoreRadius && d <= RangeRadius && y > 0) noCore.Add(y);
                    if (d > CoreRadius && y > 0) r4NoCore.Add(y);
                }
                totals.Settlements++;
                int k = fieldsOf[i];
                if (k <= 0) continue;
                totals.Fields += k;
                totals.Range += BestK(all, k);
                totals.RangeNoCore += BestK(noCore, k);
                totals.Radius4NoCore += BestK(r4NoCore, k);
            }
        }
        return sb.ToString();
    }

    private static void AccumulateOuter(GeneratedWorld generated, WorldEconomyInput world, LivingEconomyModel economy, MatchFrame frame,
        OuterTotals totals, (long Added, long Released, long Moved) events)
    {
        totals.Added += events.Added;
        totals.Released += events.Released;
        totals.Moved += events.Moved;
        int settlementCount = economy.SettlementCount;
        var claimedBy = new int[world.TileCount];
        Array.Fill(claimedBy, -1);
        var claimedCounts = new int[settlementCount];
        var homes = new int[settlementCount];
        var unhoused = new int[settlementCount];
        var movedHomes = new int[settlementCount];
        var plotCounts = new int[settlementCount];
        var shortGround = new int[settlementCount];
        var yieldBefore = new decimal[settlementCount];
        var yieldAfter = new decimal[settlementCount];
        var yieldFields = new int[settlementCount];
        int[] rowTiles = new int[(int)CoreDistrict.Count];
        byte[] rowDistricts = new byte[(int)CoreDistrict.Count];
        byte[] rowPositions = new byte[(int)CoreDistrict.Count];
        var sites = new int[settlementCount];
        var living = new bool[settlementCount];
        for (int s = 0; s < settlementCount; s++)
        {
            SettlementView view = economy.Settlement(s);
            sites[s] = view.SiteTile;
            living[s] = view.Population > 0;
        }

        for (int s = 0; s < settlementCount; s++)
        {
            int rows = economy.OuterCoreHexes(s, rowTiles, rowDistricts, rowPositions);
            for (int row = 0; row < rows; row++)
            {
                int tile = rowTiles[row];
                if (tile < 0) { unhoused[s]++; continue; }
                int district = rowDistricts[row];
                claimedCounts[s]++;
                if (district is (byte)CoreDistrict.HomesC or (byte)CoreDistrict.HomesD) homes[s]++;
                if (rowPositions[row] != SettlementCoreGrowth.Home[district]) movedHomes[s]++;
                bool validIndex = (uint)tile < (uint)world.TileCount;
                bool duplicate = validIndex && claimedBy[tile] >= 0;
                bool invalid = !SettlementCoreGrowth.IsUsableStatic(world, tile);
                bool inOtherCore = false;
                if (validIndex)
                    for (int other = 0; other < settlementCount; other++)
                        if (other != s && living[other] && HexGrid.Distance(sites[other], tile, generated.Width) <= CoreRadius)
                        { inOtherCore = true; break; }
                totals.AddClaimCheck(duplicate, invalid, inOtherCore);
                if (validIndex && !duplicate) claimedBy[tile] = s;
            }
        }

        for (int s = 0; s < settlementCount; s++)
        {
            for (int i = 0; i < economy.FieldCount(s); i++)
            {
                int tile = economy.FieldTileAt(s, i);
                if ((uint)tile < (uint)claimedBy.Length && claimedBy[tile] >= 0) plotCounts[claimedBy[tile]]++;
            }
            for (int i = 0; i < economy.OrchardCount(s); i++)
            {
                int tile = economy.OrchardTileAt(s, i);
                if ((uint)tile < (uint)claimedBy.Length && claimedBy[tile] >= 0) plotCounts[claimedBy[tile]]++;
            }
            var terraces = new List<int>();
            economy.IronTerraceTilesForView(s, terraces);
            foreach (int tile in terraces)
                if ((uint)tile < (uint)claimedBy.Length && claimedBy[tile] >= 0) plotCounts[claimedBy[tile]]++;
        }
        for (int camp = 0; camp < frame.CampCount; camp++)
            if (frame.CampKind[camp] == (byte)CampKind.Seasonal && (uint)frame.CampTile[camp] < (uint)claimedBy.Length && claimedBy[frame.CampTile[camp]] >= 0)
                totals.AddCampOnClaim();

        var disk = new List<int>();
        var beforeYield = new List<long>();
        var afterYield = new List<long>();
        for (int s = 0; s < settlementCount; s++)
        {
            if (!living[s]) continue;
            int fields = economy.FieldCount(s);
            if (fields <= 0) continue;
            beforeYield.Clear(); afterYield.Clear();
            Disk(sites[s], RangeRadius, generated.Width, generated.Height, disk);
            foreach (int tile in disk)
            {
                if (HexGrid.Distance(sites[s], tile, generated.Width) <= CoreRadius) continue;
                bool otherCore = false;
                for (int other = 0; other < settlementCount; other++)
                    if (other != s && living[other] && HexGrid.Distance(sites[other], tile, generated.Width) <= CoreRadius)
                    { otherCore = true; break; }
                if (otherCore) continue;
                long value = world.Tile(tile).FieldYieldPerYear.Raw;
                if (value <= 0) continue;
                beforeYield.Add(value);
                if (claimedBy[tile] < 0) afterYield.Add(value);
            }
            if (afterYield.Count < fields) shortGround[s]++;
            yieldFields[s] = fields;
            yieldBefore[s] = BestK(beforeYield, fields);
            yieldAfter[s] = BestK(afterYield, fields);
        }

        for (int s = 0; s < settlementCount; s++)
        {
            int tier = Math.Clamp((int)economy.Tier(s), 0, 2);
            totals.AddSettlement(tier, claimedCounts[s], homes[s], unhoused[s], movedHomes[s], plotCounts[s], shortGround[s],
                yieldBefore[s], yieldAfter[s], yieldFields[s]);
        }
    }

    private static decimal BestK(List<long> values, int k)
    {
        values.Sort();
        decimal sum = 0;
        for (int i = values.Count - 1, taken = 0; i >= 0 && taken < k; i--, taken++) sum += values[i];
        return sum;
    }

    private static string Row(string label, int[] counts)
        => "  " + label.PadRight(30) + string.Concat(counts.Select(c => c.ToString(CultureInfo.InvariantCulture).PadLeft(6)));
}
