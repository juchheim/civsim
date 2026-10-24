using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CivSim.Sim.Content;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>
/// <c>research --c1-e3</c> (docs/MVP/22 §9, §16 B9, 23 §11.1; the live twin of <c>check_e3.py --fixed</c>): each civilization's start
/// settlement on a generated map, set up as check_e3's average settlement at the end of E2 (E0–E2 and KEY-3 done, fields on 25% of
/// the range at soil 0.70, no herds) and at the end of E3 (KEY-4's food closure and the take-weighted picks: the ard's 40% field
/// limit, canals on the fields within 2 tiles of a river, soil 0.79 with manuring, five pastures with cattle and sheep, carts),
/// its sustainable FoodCap read from the live income model at check_e3's need of 3.0 rations a person a year. It reports; it
/// never tunes.
/// </summary>
internal static class C1E3Report
{
    private static readonly string[] E3Food = { "AGR-05", "AGR-06", "AGR-07", "HUS-05", "TRN-05", "GOV-04", "TRD-05", "NUM-06" };

    public static int Execute(ContentPack pack, ulong seed)
    {
        var text = new StringBuilder();
        text.AppendLine($"C1-E3 live fixture (seed {seed}): FoodCap of each start settlement at need 3.0 (check_e3.py target 88 -> 190; E3 projects alone x1.74)");
        double[] e2 = Run(pack, seed, null, out int[] riverFields, out int[] fields2);
        double[] e3 = Run(pack, seed, E3Food, out _, out int[] fields3);
        double m2 = 0, m3 = 0;
        for (int c = 0; c < e2.Length; c++)
        {
            text.AppendLine($"  civ {c}: E2 end {e2[c],6:F1} ({fields2[c]} fields)  E3 end {e3[c],6:F1} ({fields3[c]} fields, {riverFields[c]} within 2 of a river)  x{e3[c] / e2[c]:F2}");
            m2 += e2[c]; m3 += e3[c];
        }
        m2 /= e2.Length; m3 /= e3.Length;
        text.AppendLine($"  mean: E2 end {m2:F1}, E3 end {m3:F1}, x{m3 / m2:F2} (check_e3 --fixed: 87.8 -> 177.3, x2.02 with the E2 backfills, x1.74 E3 alone by 23 §11.1)");
        text.AppendLine("  contribution at E3 end (FoodCap without the project -> ratio):");
        foreach (string id in E3Food)
        {
            var without = new List<string>(E3Food);
            without.Remove(id);
            double[] w = Run(pack, seed, without.ToArray(), out _, out _);
            double mw = 0;
            foreach (double v in w) mw += v;
            mw /= w.Length;
            text.AppendLine($"    {id}: x{m3 / mw:F3}");
        }
        Console.Write(text.ToString());
        return 0;
    }

    private static double[] Run(ContentPack pack, ulong seed, string[]? e3, out int[] riverFields, out int[] fieldCounts)
    {
        GeneratedWorld generated = WorldGenerator.Generate(seed);
        WorldEconomyInput world = WorldEconomyInput.FromGeneratedWorld(generated);
        EconomyContent content = EconomyContent.FromPack(pack);
        var economy = new LivingEconomyModel(seed, content, world) { FixtureHoldSettlements = true };
        var projects = new List<string>();
        foreach (EconomyProject p in content.Projects) if (p.Era <= 2) projects.Add(p.Id);
        projects.Add("KEY-3");
        if (e3 is not null) projects.AddRange(e3);
        int civs = economy.CivilizationCount;
        var result = new double[civs];
        riverFields = new int[civs];
        fieldCounts = new int[civs];
        for (int c = 0; c < civs; c++)
        {
            economy.FixtureComplete(c, projects.ToArray());
            economy.FixtureAddPeople(c, 48, 20);
        }
        // One season makes every settlement's Bronze state (the fixture changes nothing else that the income reads).
        for (int t = 0; t < LivingEconomyModel.SeasonTicks; t++) economy.Step(t, ReadOnlySpan<CivSim.Sim.Core.Command>.Empty);
        for (int c = 0; c < civs; c++)
        {
            int range = 37;
            bool end = e3 is not null;
            int fields = end && Array.IndexOf(e3!, "AGR-05") >= 0 ? range * 4 / 10 : range / 4;
            Fix64 soil = end && Array.IndexOf(e3!, "AGR-07") >= 0 ? Fix64.Parse("0.79") : Fix64.Parse("0.70");
            (int placed, _) = end
                ? economy.FixtureFood(c, fields, soil, 5, Fix64.FromInt(5), Fix64.FromInt(15), 3)
                : economy.FixtureFood(c, fields, soil, 0, Fix64.Zero, Fix64.Zero, 0);
            fieldCounts[c] = placed;
            riverFields[c] = economy.FixtureNearRiverFields(c);
            FoodIncomeBreakdown income = economy.SustainableFoodIncomeBreakdown(c);
            result[c] = income.Total.ToDoubleForDisplay() / 3.0;
            if (Environment.GetEnvironmentVariable("CIVSIM_TRACE_C1") == "1")
                Console.WriteLine($"    [{(e3 is null ? "E2" : "E3")} civ{c}] plants {income.Plants.ToDoubleForDisplay():F1} meat {income.Meat.ToDoubleForDisplay():F1} fish {income.Fish.ToDoubleForDisplay():F1} grain {income.Grain.ToDoubleForDisplay():F1} fruit {income.Fruit.ToDoubleForDisplay():F1}");
        }
        return result;
    }
}
