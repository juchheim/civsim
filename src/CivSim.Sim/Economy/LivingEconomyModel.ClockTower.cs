using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

public sealed partial class LivingEconomyModel
{
    private const int ClockTowerReach = 6;
    private static readonly Fix64 ClockTowerWorkMultiplier = Fix64.FromRatio(105, 100);

    private bool HasClockTowerWithin(SettlementState settlement)
    {
        if (!FiefChain) return false;
        foreach (SettlementState tower in _settlements)
            if (IronWorksCount(tower, IronStructure.ClockTower) > 0 &&
                HexGrid.Distance(settlement.SiteTile, tower.SiteTile, _world.Width) <= ClockTowerReach)
                return true;
        return false;
    }

    private Fix64 ClockTowerWorkFactor(SettlementState settlement)
        => HasClockTowerWithin(settlement) ? ClockTowerWorkMultiplier : Fix64.One;

    internal bool FixtureClockTowerWithin(int settlementIndex)
        => (uint)settlementIndex < (uint)_settlements.Count && HasClockTowerWithin(_settlements[settlementIndex]);
}
