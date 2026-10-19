using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

public enum EconomyArchetype : byte
{
    Balanced, Grower, Builder, Wanderer, Warlord, Sage
}

public enum EconomyJob : byte
{
    None, Forage, Hunt, Fish, Farm, Chop, Gather, Craft, Build, Preserve, Rest, TendFire, Herd
}

public readonly record struct FigureView(
    long Id, int Civilization, int SettlementId, int Age, bool Female, Fix64 Health,
    Fix64 Hunger, Fix64 Warmth, Fix64 Rest, Fix64 Belonging, byte PregnancySeasons,
    EconomyJob Job, Fix64 WorkThisSeason);

public enum EconomyDeathCause : byte
{
    Endemic, Epidemic, Starvation, Exposure, Injury, Predators, Violence, Senescence
}

/// <summary>Cause and seasonal hazard inputs for a figure that died at the last season boundary.</summary>
public readonly record struct EconomyDeathEvent(
    long FigureId, int Civilization, int SettlementId, int Age, bool Female, byte PregnancySeasons,
    EconomyDeathCause Cause, Fix64 TotalHazard, Fix64 EndemicHazard, Fix64 EpidemicHazard,
    Fix64 StarvationHazard, Fix64 ExposureHazard, Fix64 InjuryHazard, Fix64 PredatorsHazard,
    Fix64 ViolenceHazard, Fix64 SenescenceHazard);

public readonly record struct SettlementView(
    int Id, int Civilization, int SiteTile, int Population, Fix64 FoodStores,
    Fix64 FoodCap, Fix64 HousingCap, Fix64 GroupCap, Fix64 EffectivePopulationCap,
    // Actual security is population-dependent; C2LateWinterCapacityShare is the sustainable
    // late-winter share at FoodCap used by the M3 balance check.
    Fix64 LastFoodSecurity, Fix64 LastLateWinterFoodShare, Fix64 LateWinterFoodForecast,
    Fix64 C2LateWinterCapacityShare, Fix64 LeanFoodCapacity,
    Fix64 LateWinterFissionThreshold, bool FissionPending, int Fissions,
    int StoragePits, int DryingRacks, int Fields, int Warmth);

public readonly record struct WildHerdView(
    int Id, CivSim.Sim.World.HerdSpecies Species, int HomeTile, int CurrentTile,
    Fix64 AnimalCount, Fix64 Fear, int AwaySeasons);

public readonly record struct DomesticHerdView(Fix64 SmallStock, Fix64 Cattle, Fix64 Pigs);

/// <summary>Annual sustainable food income by source, expressed in ration value.</summary>
public readonly record struct FoodIncomeBreakdown(
    Fix64 Plants, Fix64 Meat, Fix64 Fish, Fix64 Grain, Fix64 Fruit, Fix64 AnnualNeedPerPerson)
{
    public Fix64 Total => Plants + Meat + Fish + Grain + Fruit;
}
