using CivSim.Sim.Numerics;
using System.Linq;

namespace CivSim.Sim.Economy;

public sealed partial class LivingEconomyModel
{
    internal Fix64 FixtureFiefWildernessFarmYield(int settlementIndex)
        => FiefWildernessFarmYield(_settlements[settlementIndex]);

    internal int[] FixtureFiefWildernessPlots(int settlementIndex)
        => FiefWildernessPlots(_settlements[settlementIndex]).ToArray();

    internal bool FixtureIsInsideAnySettlementFootprint(int tile) => IsInsideAnySettlementFootprint(tile);
    internal bool FixtureIsAnySettledField(int tile) => IsAnySettledField(tile);
    internal bool FixtureIsSettlementSite(int tile) => IsSettlementSite(tile);
    internal Fix64 FixtureWildernessPlotYield(int settlementIndex, int tile)
        => FieldBaseYield(_settlements[settlementIndex].Civilization, _world.Tile(tile)) * WildernessPlotYieldShare;
}
