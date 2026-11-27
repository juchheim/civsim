using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>Minimal Fief charter interpretation (GOV-11): a City-tier settlement is chartered once its civilization completes GOV-11; the charter multiplies conception probability by 1.1.</summary>
public sealed partial class LivingEconomyModel
{
    private Fix64 FiefCityGrowthMultiplier(SettlementState settlement)
        => FiefWp7SuppressGov11GrowthForDiagnostics ? Fix64.One : FiefCityGrowthMultiplierTreatment(settlement);

    private Fix64 FiefCityGrowthMultiplierTreatment(SettlementState settlement)
        => FiefChain && settlement.Civic is { Tier: (byte)SettlementTier.City } &&
           HasProject(settlement.Civilization, "GOV-11")
            ? Fix64.FromRatio(11, 10)
            : Fix64.One;

    internal Fix64 FixtureFiefCityGrowthMultiplier(int settlementIndex)
        => FiefCityGrowthMultiplier(_settlements[settlementIndex]);
}
