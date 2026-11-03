using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CivSim.Sim.Content;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Harness;

/// <summary>
/// The fuel check of docs/MVP/25 I-T6 on the live economy: "sustainable metal is not more than 15% of civilian work in steady state, and
/// no mandatory clear-cut", measured on one-settlement fixtures in the Iron chapter against the same settlement in the Bronze chapter,
/// with a low-wood start, a start without Coppicing, and the loss of the iron supply (the import-loss stress).
/// <para>
/// Read-only tooling: it builds fixtures and runs the economy, changes nothing in the simulation, and its output is a pure function of its
/// arguments. A fixture is one 60-person settlement (the live E3 age mix) with a forge, kiln, smelter and an iron camp site four tiles
/// out on own land, topped up with grain each season so food never binds. It is therefore a test of fuel and labor, not of a whole match.
/// </para>
/// </summary>
internal static class IronFuelCheck
{
    /// <summary>I-T6's ceiling on the share of civilian work the metal chain may take.</summary>
    private static readonly Fix64 ShareCeiling = Fix64.FromRatio(15, 100);
    /// <summary>Operational definition of a "mandatory clear-cut" (the doc gives none): standing wood in range ends below this share of its start.</summary>
    private static readonly Fix64 ClearCutRatio = Fix64.FromRatio(1, 2);
    /// <summary>...or the chain reports a fuel shortage in more than this share of the steady-state window.</summary>
    private static readonly Fix64 FuelShortCeiling = Fix64.FromRatio(1, 4);

    private sealed record Scenario(string Name, bool Iron, bool Coppice, int ForestTiles, bool LoseIronAtMidpoint);

    private static readonly Scenario[] Scenarios =
    {
        new("bronze-reference", false, true, 13, false),
        new("iron", true, true, 13, false),
        new("iron-low-wood", true, true, 4, false),
        new("iron-no-coppice", true, false, 13, false),
        new("bronze-no-coppice", false, false, 13, false),
        new("iron-import-loss", true, true, 13, true),
        new("bronze-low-wood", false, true, 4, false),
        new("iron-wood-rich", true, true, 30, false),
        new("bronze-wood-rich", false, true, 30, false),
    };

    private sealed class Row
    {
        public Fix64 Share, CharcoalWoodPerSeason, WoodStart, WoodMid, WoodEnd, WoodMin, IronKitShare, FuelShortShare, Bloom, Wrought;
        public int Adults;
    }

    public static int Run(string[] args)
    {
        string? packPath = null;
        var seeds = new List<ulong> { 20261019, 20261020, 20261021, 20261022, 20261023, 20261024, 20261025, 20261026 };
        int seasons = 40, people = 60;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pack": packPath = args[++i]; break;
                case "--seeds":
                    seeds.Clear();
                    foreach (string part in args[++i].Split(',')) seeds.Add(ulong.Parse(part, CultureInfo.InvariantCulture));
                    break;
                case "--seasons": seasons = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--people": people = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                default: throw new ArgumentException($"Unknown option {args[i]}.");
            }
        }
        if (packPath is null) throw new ArgumentException("iron-fuel needs --pack FILE (civsim build-pack --out FILE).");
        if (seasons < 8 || seasons % 2 != 0) throw new ArgumentException("--seasons must be an even number of at least 8.");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        Console.Write(Report(pack, seeds, seasons, people));
        return 0;
    }

    internal static string Report(ContentPack pack, IReadOnlyList<ulong> seeds, int seasons, int people, int scenarioLimit = int.MaxValue)
    {
        var sb = new StringBuilder();
        void L(string text = "") => sb.Append(text).Append('\n');
        int window = seasons / 2;
        L($"iron-fuel: {seeds.Count} seeds, {seasons} seasons ({window}-season steady-state window), {people}-person fixture settlement");
        L($"I-T6: metal chain <= {Pct(ShareCeiling)} of civilian work; clear-cut = standing wood < {Pct(ClearCutRatio)} of start or fuel short in > {Pct(FuelShortCeiling)} of the window");
        L();
        L("scenario            seeds  metal-share(mean/max)  charcoal-wood/season  wood end/start(mean/min)  end/mid  fuel-short  iron-kits  bloom+wrought");
        var verdicts = new List<string>();
        int shown = 0;
        foreach (Scenario sc in Scenarios)
        {
            if (shown++ >= scenarioLimit) break;
            var rows = new List<Row>();
            int skipped = 0;
            foreach (ulong seed in seeds)
            {
                try { rows.Add(RunOne(pack, seed, sc, seasons, people)); }
                catch (InvalidOperationException) { skipped++; } // the fixture needs 16 land tiles in range and land four tiles out
            }
            if (rows.Count == 0) { L($"{sc.Name,-19} no usable seeds"); continue; }
            Fix64 meanShare = Mean(rows, r => r.Share), maxShare = Max(rows, r => r.Share);
            Fix64 meanWood = Mean(rows, r => r.CharcoalWoodPerSeason);
            Fix64 meanRatio = Mean(rows, r => Ratio(r.WoodEnd, r.WoodStart)), minRatio = Min(rows, r => Ratio(r.WoodEnd, r.WoodStart));
            Fix64 meanSteady = Mean(rows, r => Ratio(r.WoodEnd, r.WoodMid));
            Fix64 meanShort = Mean(rows, r => r.FuelShortShare), maxShort = Max(rows, r => r.FuelShortShare);
            Fix64 meanKits = Mean(rows, r => r.IronKitShare);
            Fix64 meanBloom = Mean(rows, r => r.Bloom + r.Wrought);
            L($"{sc.Name,-19} {rows.Count + (skipped > 0 ? "/" + seeds.Count : ""),5}  {Pct(meanShare),7} / {Pct(maxShare),-7}       {meanWood.ToDoubleForDisplay(),8:F1} L            {meanRatio.ToDoubleForDisplay(),5:F2} / {minRatio.ToDoubleForDisplay(),-5:F2}        {meanSteady.ToDoubleForDisplay(),5:F2}     {Pct(meanShort),5}      {Pct(meanKits),5}      {meanBloom.ToDoubleForDisplay():F2}");
            bool shareOk = maxShare <= ShareCeiling;
            bool clearCut = minRatio < ClearCutRatio || maxShort > FuelShortCeiling;
            verdicts.Add($"{sc.Name}: work share {(shareOk ? "within" : "ABOVE")} {Pct(ShareCeiling)} on every seed (max {Pct(maxShare)}); {(clearCut ? "CLEAR-CUT SIGNAL" : "no clear-cut signal")} (lowest wood ratio {minRatio.ToDoubleForDisplay():F2}, worst fuel-short share {Pct(maxShort)})");
        }
        L();
        L("verdicts (per scenario, worst seed):");
        foreach (string v in verdicts) L("  " + v);
        L();
        L("end/mid is the steady-state slope of standing wood over the window (1.00 = flat; below 1 it is still being drawn down). end/start includes the one-time kit build-out.");
        L("Reading: the fixture settlement is fed with grain each season and has a forge, kiln and smelter from the start, so this measures fuel and labor, not a whole match.");
        L("The Bronze reference has the same iron site but makes no iron (the chain is Iron-chapter only), so the difference between it and 'iron' is the Iron chain's cost.");
        return sb.ToString();
    }

    private static Row RunOne(ContentPack pack, ulong seed, Scenario sc, int seasons, int people)
    {
        BronzeFixture f = BronzeFixture.CreateFuelScenario(pack, seed, people, sc.Iron, sc.Coppice, sc.ForestTiles, "MET-08", "MET-09");
        LivingEconomyModel e = f.Economy;
        int window = seasons / 2;
        var row = new Row { WoodStart = f.StandingWood(), WoodMin = f.StandingWood() };
        MetalStatistics mid = default;
        int fuelShort = 0;
        for (int s = 0; s < seasons; s++)
        {
            if (sc.LoseIronAtMidpoint && s == window && f.IronTile >= 0) e.FixtureExhaust(f.IronTile);
            f.RunSeasons(1);
            Fix64 wood = f.StandingWood();
            if (wood < row.WoodMin) row.WoodMin = wood;
            if (s == window - 1) { mid = e.MetalStats(0); row.WoodMid = wood; }
            if (s >= window && e.Metal(BronzeFixture.Settlement).Reason == MethodFallbackReason.NoFuel) fuelShort++;
        }
        MetalStatistics end = e.MetalStats(0);
        Fix64 labor = end.Labor - mid.Labor;
        row.Share = labor > Fix64.Zero ? (end.MetalLabor - mid.MetalLabor) / labor : Fix64.Zero;
        row.CharcoalWoodPerSeason = (end.CharcoalWood - mid.CharcoalWood + end.FireSettingWood - mid.FireSettingWood) / window;
        row.WoodEnd = f.StandingWood();
        row.FuelShortShare = Fix64.FromRatio(fuelShort, window);
        int adults = 0, iron = 0;
        foreach (int tier in e.FixtureAdultKitTiers(BronzeFixture.Settlement)) { adults++; if (tier >= 11) iron++; }
        row.Adults = adults;
        row.IronKitShare = adults == 0 ? Fix64.Zero : Fix64.FromRatio(iron, adults);
        row.Bloom = e.GoodStock(BronzeFixture.Settlement, "G-IRON");
        row.Wrought = e.GoodStock(BronzeFixture.Settlement, "G-WROUGHT");
        return row;
    }

    private static Fix64 Ratio(Fix64 end, Fix64 start) => start <= Fix64.Zero ? Fix64.One : end / start;
    private static string Pct(Fix64 v) => (v * 100).ToDoubleForDisplay().ToString("F1", CultureInfo.InvariantCulture) + "%";
    private static Fix64 Mean(List<Row> rows, Func<Row, Fix64> f) { Fix64 t = Fix64.Zero; foreach (Row r in rows) t += f(r); return t / rows.Count; }
    private static Fix64 Max(List<Row> rows, Func<Row, Fix64> f) { Fix64 m = f(rows[0]); foreach (Row r in rows) if (f(r) > m) m = f(r); return m; }
    private static Fix64 Min(List<Row> rows, Func<Row, Fix64> f) { Fix64 m = f(rows[0]); foreach (Row r in rows) if (f(r) < m) m = f(r); return m; }
}
