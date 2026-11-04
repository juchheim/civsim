using System.Collections.Generic;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Politics;

/// <summary>
/// Organized Faith (BEL-09, docs/MVP/25 §6): a missionary travels only along a trade route that is actually running, converts a small share of a foreign
/// settlement's people each year to a cap, and lends its civilization a little claim pressure there while the preaching continues. Faith never exposes fog,
/// never transfers a settlement, and stops where the route is cut.
/// </summary>
public sealed partial class PoliticsSystem
{
    /// <summary>
    /// The foreign settlements a settlement's missionaries can reach this season, ascending: the other hub of every running civilization-to-civilization compact that has
    /// delivered at least once (so a route exists), unless the sea route is blockaded or the town is under siege.
    /// </summary>
    private List<int> FaithRoutes(int home)
    {
        var reached = new List<int>();
        foreach (Compact c in _compacts)
        {
            if (!c.Active || c.Deliveries <= 0 || c.SellerCiv < 0 || c.BuyerCiv < 0) continue;
            int other = c.LastSeller == home ? c.LastBuyer : c.LastBuyer == home ? c.LastSeller : -1;
            if (other < 0 || other >= _sCount || _free[other] || _sCiv[other] == _sCiv[home] || BlockadeCutsRoute(home, other) || UnderInvestment(other)) continue;
            if (!reached.Contains(other)) reached.Add(other);
        }
        reached.Sort();
        return reached;
    }

    /// <summary>One season of preaching: each missionary works one reached settlement, taking them in turn so several preachers spread across several towns.</summary>
    private void AdvanceFaith()
    {
        if (!_economy.IronChain) return;
        for (int home = 0; home < _sCount; home++)
        {
            if (_free[home]) continue;
            int missionaries = _economy.Missionaries(home);
            if (missionaries <= 0) continue;
            List<int> reached = FaithRoutes(home);
            if (reached.Count == 0) continue;
            for (int k = 0; k < missionaries; k++) _economy.ConvertTo(reached[k % reached.Count], _sCiv[home], _season);
        }
    }

    internal List<int> FixtureFaithRoutes(int home) => FaithRoutes(home);

    /// <summary>A running compact whose last caravan has already run between two hubs, so a route exists for a missionary to follow.</summary>
    internal void FixtureAddRoute(int sellerCiv, int buyerCiv, int sellerHub, int buyerHub)
        => _compacts.Add(new Compact { Id = _nextCompactId++, SellerCiv = sellerCiv, BuyerCiv = buyerCiv, Proposer = sellerCiv, Good = TradeGood.Food,
            SinceSeason = _season, Deliveries = 1, LastSeller = sellerHub, LastBuyer = buyerHub });
}
