using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// Draft Fief-only monastery wilderness work (WP-E5-6). These are derived wilderness plots, never settlement field tiles:
/// assignments are rebuilt deterministically from the map and monastery locations, so no Bronze/Iron or snapshot state changes.
/// </summary>
public sealed partial class LivingEconomyModel
{
    // Draft yield hypothesis: a tended wilderness plot supplies one quarter of a normal field's gross annual grain.
    private static readonly Fix64 WildernessPlotYieldShare = Fix64.FromRatio(1, 4);
    private const int WildernessPlotsPerMonastery = 6;

    private Fix64 FiefWildernessFarmYield(SettlementState settlement)
    {
        Fix64 yield = Fix64.Zero;
        foreach (int tile in FiefWildernessPlots(settlement))
            yield += FieldBaseYield(settlement.Civilization, _world.Tile(tile)) * WildernessPlotYieldShare;
        return yield;
    }

    private IReadOnlyList<int> FiefWildernessPlots(SettlementState settlement)
    {
        if (!FiefChain || !HasProject(settlement.Civilization, "BEL-10")) return Array.Empty<int>();
        int monasteries = IronWorksCount(settlement, IronStructure.Monastery);
        int monks = MonksAt(settlement.Id);
        if (monasteries == 0 || monks == 0) return Array.Empty<int>();

        int plotLimit = Math.Min(WildernessPlotsPerMonastery * monasteries, monks);
        var assignedTiles = new List<int>(plotLimit);
        foreach (int tile in ReachTiles(settlement.SiteTile, 8))
        {
            if (assignedTiles.Count >= plotLimit) break;
            if (IsInsideAnySettlementFootprint(tile) || IsAnySettledField(tile) || IsSettlementSite(tile) ||
                WildernessAssignedElsewhere(settlement, tile)) continue;
            Biome biome = (Biome)_world.Tile(tile).BiomeId;
            if (biome is Biome.Ocean or Biome.Coast or Biome.Ice or Biome.Peak or Biome.Desert) continue;
            assignedTiles.Add(tile);
        }
        return assignedTiles;
    }

    private bool IsInsideAnySettlementFootprint(int tile)
    {
        foreach (SettlementState other in _settlements)
            if (HexGrid.Distance(other.SiteTile, tile, _world.Width) <= Radius) return true;
        return false;
    }

    private bool IsAnySettledField(int tile)
    {
        foreach (SettlementState other in _settlements)
            if (other.FieldTiles.Contains(tile)) return true;
        return false;
    }

    private bool IsSettlementSite(int tile)
    {
        foreach (SettlementState other in _settlements)
            if (other.SiteTile == tile) return true;
        return false;
    }

    private bool WildernessAssignedElsewhere(SettlementState settlement, int tile)
    {
        int distance = HexGrid.Distance(settlement.SiteTile, tile, _world.Width);
        foreach (SettlementState other in _settlements)
        {
            if (other.Id == settlement.Id || IronWorksCount(other, IronStructure.Monastery) == 0 || MonksAt(other.Id) == 0) continue;
            int otherDistance = HexGrid.Distance(other.SiteTile, tile, _world.Width);
            if (otherDistance < distance || (otherDistance == distance && other.Id < settlement.Id)) return true;
        }
        return false;
    }
}
