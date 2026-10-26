using System;
using System.Globalization;
using System.Text;
using CivSim.Sim.Content;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Harness;

/// <summary>
/// <c>research --metal-fixture</c> (docs/MVP/22 §16 B12, B13): the metal_sim.py settlement run on the live economy. One
/// settlement held at a size, deposits and 13 forest tiles in range, the metal projects done at season 0.
/// </summary>
internal static class MetalFixtureReport
{
    public static int Execute(ContentPack pack, ulong seed)
    {
        var text = new StringBuilder();
        text.AppendLine($"Metal fixture (seed {seed}): one settlement, copper ore, a tin placer and deep arsenical ore in range, 13 forest tiles x 30 L");
        int[] sizes = Environment.GetEnvironmentVariable("CIVSIM_FIXTURE_SIZE") is { } only ? new[] { int.Parse(only, CultureInfo.InvariantCulture) } : new[] { 40, 60, 80, 100 };
        foreach (int people in sizes)
            foreach (bool coppice in new[] { false, true })
            {
                BronzeFixture f = BronzeFixture.Create(pack, seed, people, coppice, true, true, "WAR-05");
                var shares = new StringBuilder();
                MetalStatistics at12 = default;
                int reach70 = -1;
                for (int season = 1; season <= 52; season++)
                {
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    if (Environment.GetEnvironmentVariable("CIVSIM_TRACE_FIXTURE") == "1") f.Economy.MetalDebug = new StringBuilder();
                    f.RunSeasons(1);
                    if (f.Economy.MetalDebug is { } log)
                    {
                        var st = new StringBuilder();
                        for (int k = 0; k < LivingEconomyModel.StructureKindCount; k++)
                            if (f.Economy.StructureCount(0, k) > 0) st.Append($" {LivingEconomyModel.StructureName(k)}={f.Economy.StructureCount(0, k)}");
                        Console.WriteLine($"   wood {f.Economy.GoodStock(0, "G-WOOD").ToDoubleForDisplay():F1} chopped total {f.Economy.MetalStats(0).ChopWood.ToDoubleForDisplay():F1} standing {f.StandingWood().ToDoubleForDisplay():F0} |{st}");
                        Console.WriteLine("   recipes:" + log); f.Economy.MetalDebug = null;
                    }
                    if (Environment.GetEnvironmentVariable("CIVSIM_TRACE_FIXTURE") == "1")
                        Console.WriteLine($"season {season} {clock.ElapsedMilliseconds} ms pop {f.Economy.Population(0)} | {f.Economy.DescribeMetal(0)}");
                    double share = f.BronzeShare().ToDoubleForDisplay();
                    if (reach70 < 0 && share >= 0.7) reach70 = season;
                    if (season is 4 or 8 or 12 or 24 or 52) shares.Append($" s{season} {share * 100:F0}%");
                    if (season == 24) at12 = f.Economy.MetalStats(0);
                }
                MetalStatistics end = f.Economy.MetalStats(0);
                double years100 = (end.PeopleSeasons - at12.PeopleSeasons).ToDoubleForDisplay() / 4 / 100;
                double bronze = (end.Bronze - at12.Bronze + end.ArsenicalBronze - at12.ArsenicalBronze).ToDoubleForDisplay() / years100;
                double tin = (end.Tin - at12.Tin).ToDoubleForDisplay() / years100;
                double charcoal = (end.Charcoal - at12.Charcoal).ToDoubleForDisplay() / years100;
                double wood = (end.CharcoalWood - at12.CharcoalWood + end.FireSettingWood - at12.FireSettingWood).ToDoubleForDisplay() / years100;
                double chop = (end.ChopWood - at12.ChopWood).ToDoubleForDisplay() / years100;
                double labor = (end.MetalLabor - at12.MetalLabor).ToDoubleForDisplay() / Math.Max(1e-9, (end.Labor - at12.Labor).ToDoubleForDisplay());
                MetalReadout m = f.Economy.Metal(0);
                text.AppendLine($"  {people,3} people {(coppice ? "WOD-04   " : "no WOD-04")}: rung 6{shares} (70% at season {reach70}); kits T8 {m.Kits8} T9 {m.Kits9} T10 {m.Kits10} of {m.Adults} adults");
                text.AppendLine($"      steady state (seasons 25-52) per 100 people a year: bronze {bronze:F2} L, tin {tin:F3} L, charcoal {charcoal:F2} L, wood for metal {wood:F1} L, metal labor {labor * 100:F1}%; chopped (hearths, building, crafts) {chop:F0} L;" +
                                " population follows natural births, deaths and aging");
            }

        // B13/T19 compares equal-size, fixed-demography Towns so births and deaths do not change wood demand mid-run.
        foreach (int people in new[] { 80, 100 })
            foreach (bool coppice in new[] { false, true })
            {
                BronzeFixture f = BronzeFixture.Create(pack, seed, people, coppice, true, true,
                    shaftMining: true, holdPopulation: true, extraProjects: new[] { "WAR-05" });
                Fix64 wood0 = f.StandingWood();
                f.RunSeasons(52);
                double drawn = (Fix64.One - f.StandingWood() / wood0).ToDoubleForDisplay();
                int population = f.Economy.Population(f.Economy.SettlementLite(0).Id);
                text.AppendLine($"  T19 fixed {people}-person population {(coppice ? "with" : "without")} WOD-04: {drawn * 100:F0}% standing-wood drawdown (population {population})");
            }

        // B12: tin cut after 52 seasons of tin bronze, with and without arsenical ore to fall back on.
        foreach (bool arsenic in new[] { true, false })
        {
            BronzeFixture d = BronzeFixture.Create(pack, seed, 100, true, true, arsenic, "WAR-05");
            d.RunSeasons(52);
            d.Economy.FixtureExhaust(d.TinTile);
            foreach (string g in new[] { "G-TIN", "G-TINORE", "G-BRONZE", "G-TOOL10" })
                d.Economy.FixtureStock(0, g, -d.Economy.GoodStock(0, g));
            MetalReadout start = d.Economy.Metal(0);
            var dorm = new StringBuilder();
            int halfCoverage = -1;
            for (int season = 1; season <= 52; season++)
            {
                d.RunSeasons(1);
                MetalReadout m = d.Economy.Metal(0);
                if (halfCoverage < 0 && m.Kits10 * start.Adults * 2 <= start.Kits10 * m.Adults) halfCoverage = season;
                if (season is 1 or 4 or 8 or 12 or 24 or 36 or 48 or 52)
                    dorm.Append($" +{season}: T10 {m.Kits10} T9 {m.Kits9} T8 {m.Kits8} of {m.Adults} ({m.Reason});");
            }
            text.AppendLine($"B12 tin cut at season 52, {(arsenic ? "arsenical ore in range" : "no arsenical ore")} (T10 {start.Kits10} of {start.Adults}):{dorm} half the initial rung-6 adult coverage gone after {halfCoverage} seasons");
        }

        // B12: no smelting without charcoal (the recipe is limited by its scarcest input).
        BronzeFixture n = BronzeFixture.Create(pack, seed, 60, coppice: false);
        n.Economy.FixtureStock(0, "G-CUORE", Fix64.FromInt(10));
        Fix64 without = n.Economy.FixtureRunRecipe(0, "smelt_copper", Fix64.FromInt(5), Fix64.FromInt(10));
        n.Economy.FixtureStock(0, "G-CHAR", Fix64.FromInt(2));
        Fix64 with = n.Economy.FixtureRunRecipe(0, "smelt_copper", Fix64.FromInt(5), Fix64.FromInt(10));
        text.AppendLine($"B12 no smelting without charcoal: 10 L copper ore and no charcoal smelt {without.ToDoubleForDisplay():F2} batches; with 2 L charcoal (bellows 1.6 L a batch) {with.ToDoubleForDisplay():F2}");
        Console.Write(text.ToString());
        _ = CultureInfo.InvariantCulture;
        return 0;
    }
}
