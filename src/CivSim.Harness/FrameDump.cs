using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CivSim.Sim.View;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>Writes one frame and its terrain as JSON so a script (the placeholder-art mock) can draw a real match.</summary>
internal static class FrameDump
{
    public static void Write(string path, ulong seed, MatchFrame f, long tick)
    {
        WorldView w = WorldView.FromGenerated(WorldGenerator.Generate(seed));
        var figures = new List<int[]>();
        for (int i = 0; i < f.FigureCount; i++)
            figures.Add(new[] { f.FigureX[i], f.FigureY[i], f.FigureCiv[i], f.FigureAge[i], f.FigureActivity[i], f.FigureFlags[i], f.FigureToolRung[i], f.FigureSettlement[i] });
        var herds = new List<int[]>();
        for (int i = 0; i < f.HerdCount; i++) herds.Add(new[] { f.HerdX[i], f.HerdY[i], f.HerdSpecies[i], f.HerdAnimals[i] });
        var settlements = new List<object>();
        int kinds = MatchFrame.StructureKinds;
        for (int i = 0; i < f.SettlementCount; i++)
        {
            var structures = new int[kinds];
            for (int k = 0; k < kinds; k++) structures[k] = f.SettlementStructures[i * kinds + k];
            settlements.Add(new { tile = f.SettlementTile[i], civ = (int)f.SettlementCiv[i], population = f.SettlementPopulation[i], food = (int)f.SettlementFood[i], structures, ziggurats = (int)f.SettlementZiggurats[i], zigguratPermille = (int)f.SettlementZigguratPermille[i] });
        }
        var plots = new List<int[]>();
        for (int i = 0; i < f.PlotCount; i++) plots.Add(new[] { f.PlotTile[i], f.PlotKind[i] });
        var doc = new
        {
            width = f.Width, height = f.Height, tick, year = f.Year, seasonOfYear = f.SeasonOfYear,
            biome = w.Biome, elevation = w.Elevation, flags = w.Flags, owner = f.Owner, claim = f.Claim,
            forestBand = Slice(f.ForestBand, f.ForestBand.Length), depositWorked = Slice(f.DepositWorked, f.DepositWorked.Length),
            miningReach = Slice(f.MiningReach, f.MiningReach.Length),
            camps = Slice(f.CampTile, f.CampCount), campCiv = Slice(f.CampCiv, f.CampCount),
            campKind = Slice(f.CampKind, f.CampCount), campParent = Slice(f.CampParent, f.CampCount),
            campDeposit = Slice(f.CampDeposit, f.CampCount), campCrew = Slice(f.CampCrew, f.CampCount),
            campBuilt = Slice(f.CampBuilt, f.CampCount), campMineHead = Slice(f.CampMineHead, f.CampCount),
            beacons = Slice(f.BeaconTile, f.BeaconCount),
            figures, herds, settlements, plots,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(doc));
        System.Console.WriteLine($"wrote frame {path} ({f.FigureCount} figures, {f.SettlementCount} settlements, {f.HerdCount} herds)");
    }

    private static T[] Slice<T>(T[] a, int n) { var r = new T[n]; System.Array.Copy(a, r, n); return r; }
}
