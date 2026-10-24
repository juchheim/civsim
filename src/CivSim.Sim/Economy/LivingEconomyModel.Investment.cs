using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>Economy effects of an active E3 investment (docs/MVP/22 §7.4).</summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>Whether the settlement is currently under a Bronze Age investment.</summary>
    public bool IsUnderInvestment(int settlementIndex)
        => unchecked((uint)settlementIndex) < unchecked((uint)_settlements.Count) && _settlements[settlementIndex].UnderInvestment;

    /// <summary>Sets the next-season investment effects. Politics owns and snapshots the investment itself.</summary>
    public void SetUnderInvestment(int settlementIndex, bool invested)
    {
        if (unchecked((uint)settlementIndex) >= unchecked((uint)_settlements.Count)) return;
        _settlements[settlementIndex].UnderInvestment = invested && _bronzeActive && CivInBronze(_settlements[settlementIndex].Civilization);
    }

    /// <summary>Food need at this settlement for the given seasonal conditions, before siege rationing.</summary>
    public Fix64 FoodNeedForSeason(int settlementIndex, bool winter)
    {
        if (unchecked((uint)settlementIndex) >= unchecked((uint)_settlements.Count)) return Fix64.Zero;
        return FoodNeed(_settlements[settlementIndex], winter);
    }
}
