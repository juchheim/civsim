using System.Collections.Generic;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>Connected Fief pilgrimage paths over the existing completed road-to-capital links.</summary>
public sealed partial class LivingEconomyModel
{
    public Fix64 PilgrimageRouteTradeFactor(int civ)
        => FiefChain && (uint)civ < (uint)CivilizationCount ? Modifier(civ, "route_trade") : Fix64.One;

    /// <summary>
    /// Builds the deterministic settlement and tile path for a pilgrimage. Current roads form completed spokes from each town to its capital;
    /// a route between two non-capital towns therefore travels through that capital. Returns false unless each needed spoke is road- and bridge-complete.
    /// </summary>
    public bool TryPilgrimageRoadPath(int civ, int fromSettlement, int toSettlement, List<int> settlementPath, List<int> tilePath)
    {
        settlementPath.Clear();
        tilePath.Clear();
        if (!FiefChain || (uint)civ >= (uint)CivilizationCount ||
            (uint)fromSettlement >= (uint)_settlements.Count || (uint)toSettlement >= (uint)_settlements.Count ||
            fromSettlement == toSettlement || !Done(civ, "BEL-10") || !Done(civ, "BEL-11") || !Done(civ, "TRN-07")) return false;
        SettlementState from = _settlements[fromSettlement], to = _settlements[toSettlement];
        if (from.Civilization != civ || to.Civilization != civ || !IsMember(from, civ) || !IsMember(to, civ)) return false;
        int capital = CapitalOf(civ);
        if (capital < 0 || fromSettlement != capital && !RoadLinked(from) || toSettlement != capital && !RoadLinked(to)) return false;

        settlementPath.Add(fromSettlement);
        if (fromSettlement != capital) settlementPath.Add(capital);
        if (toSettlement != capital) settlementPath.Add(toSettlement);

        if (fromSettlement != capital)
        {
            var spoke = new List<int>();
            if (!RoadTilesForView(fromSettlement, spoke, out _)) return false;
            for (int i = spoke.Count - 1; i >= 0; i--) tilePath.Add(spoke[i]);
        }
        if (toSettlement != capital)
        {
            var spoke = new List<int>();
            if (!RoadTilesForView(toSettlement, spoke, out _)) { settlementPath.Clear(); tilePath.Clear(); return false; }
            for (int i = tilePath.Count == 0 ? 0 : 1; i < spoke.Count; i++) tilePath.Add(spoke[i]);
        }
        return tilePath.Count >= 2;
    }
}
