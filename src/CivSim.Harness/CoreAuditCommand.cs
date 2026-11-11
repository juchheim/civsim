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
/// <c>core-audit</c> (docs/MVP/31 §1.2, §6, §9 WP0): a read-only audit of what stands in each settlement's core (its centre hex and
/// ring 1) and of what a reserved core would cost. Two parts:
/// <list type="bullet">
/// <item><b>Static</b> (world generation only, seeds <c>first .. first+pack-seeds-1</c>): greedy packing of settlement sites at
/// spacing 7, spacing 9 and spacing 7 or 9 with no valuable deposit in the core, in tile order and in radius-3 forage order (as the
/// fission chooser picks best-first); valuable deposits in the starts' cores and ranges.</item>
/// <item><b>Live</b> (Iron rules, Builder/Grower/Warlord/Sage AIs at Warlord level, omniscient frame): plots by ring distance from
/// their settlement's centre (fields, orchards, terraces, mining camps; pastures are counts only), settlements with a plot on their own
/// centre, the share of plots within distance 0 and 1, deposits inside each living settlement's core, and best-k field yield over the
/// whole radius-3 range against the range without the 7 core hexes (base tile yield only: no depletion, irrigation or terraces).</item>
/// </list>
/// It changes nothing in the matches it plays and never writes the generated worlds. The report is a function of the arguments alone.
/// <code>
/// civsim-harness core-audit --pack FILE [--seeds N] [--first-seed N] [--minutes a,b,c] [--pack-seeds N] [--static-only | --live-only]
/// </code>
/// </summary>
internal static class CoreAuditCommand
{
    private const string UsageText =
        "usage: civsim-harness core-audit --pack FILE [--seeds N] [--first-seed N] [--minutes a,b,c] [--pack-seeds N] [--static-only | --live-only]";

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
        bool runStatic = true, runLive = true;
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
            Console.WriteLine();
            Console.WriteLine("== live: Iron rules, 4 Warlord-level AIs (Builder, Grower, Warlord, Sage), omniscient frame ==");
            for (int n = 0; n < seeds; n++)
                Console.Write(LiveReport(pack, (ulong)(first + n), minutes, totals));
            Console.WriteLine();
            Console.WriteLine("== best-k field yield over all live seeds (k = fields each settlement holds; base tile yield per year) ==");
            for (int m = 0; m < minutes.Length; m++) Console.WriteLine(totals[m].Describe(minutes[m], seeds));
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

    /// <summary>One live match on <paramref name="seed"/>, sampled at each minute (ascending).</summary>
    internal static string LiveReport(ContentPack pack, ulong seed, int[] minutes, YieldTotals[]? totals = null)
    {
        var sb = new StringBuilder();
        GeneratedWorld g = WorldGenerator.Generate(seed);
        WorldEconomyInput eco = WorldEconomyInput.FromGeneratedWorld(g);
        var controls = Archetypes.Select(a => CivControl.Ai(Difficulty.Warlord, a)).ToArray();
        MatchModel match = MatchModel.Create(seed, pack, g, Archetypes, controls, rules: ChapterRules.Iron);
        var runner = new SimRunner(match);
        var builder = new FrameBuilder(match, -1);
        var f = new MatchFrame();
        for (int m = 0; m < minutes.Length; m++)
        {
            long target = minutes[m] * 600L;
            if (target > runner.Tick) runner.Advance(target - runner.Tick);
            builder.Build(match, f);
            sb.Append(Inv($"seed {seed} minute {minutes[m]}: "));
            sb.Append(DescribeFrame(g, eco, f, totals?[m]));
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
