using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// Read-only numbers for the player's Stores and Overview views: every good in stock, storage capacity, the growth caps,
/// next season's food need and fuel draw, and the last completed season's food ledger. Nothing here is hashed,
/// snapshotted or read by a decision, so the golden checksums and replays do not depend on it.
/// </summary>
public sealed partial class LivingEconomyModel
{
    private const int FoodGoodCount = 9;

    /// <summary>Lines of <see cref="FoodLedgerLines"/>: start, gathered, eaten, spoiled, lost to full stores, end, need.</summary>
    public const int FoodLedgerSummaryLines = 7;
    public const int FoodLedgerLineCount = FoodLedgerSummaryLines + FoodGoodCount;

    /// <summary>Goods a settlement can hold: the early goods followed by the Bronze Age goods, one slot space.</summary>
    public static int StoreSlotCount => SlotCount;

    /// <summary>Registry id (G-WOOD, ...) of a store slot.</summary>
    public static string StoreSlotId(int slot)
        => slot < (int)Good.Count ? GoodId((Good)slot) : MetalGoodId((MetalGood)(slot - (int)Good.Count));

    /// <summary>How the Stores view groups a good.</summary>
    public enum StoreCategory : byte { Food, Material, Clothing, Tool, Advanced }

    public static StoreCategory StoreSlotCategory(int slot)
    {
        if (slot >= (int)Good.Count) return StoreCategory.Advanced;
        return (Good)slot switch
        {
            Good.Plant or Good.Meat or Good.Roots or Good.Nuts or Good.WildGrain or Good.Grain or Good.Fish or Good.DriedFood or Good.Fruit
                => StoreCategory.Food,
            Good.Wrap or Good.Tailored or Good.ClothGarment => StoreCategory.Clothing,
            Good.Stick or Good.Club or Good.Spear or Good.Basket or Good.Sickle or Good.Tool1 or Good.Tool2 or Good.Tool3 or Good.Tool4
                or Good.Tool5 or Good.Tool6 or Good.Tool7 or Good.SpearThrower or Good.Bow or Good.Harpoon or Good.FishingKit or Good.Net
                => StoreCategory.Tool,
            _ => StoreCategory.Material,
        };
    }

    /// <summary>The store slots of the food goods, in ledger order.</summary>
    public static int FoodGoodSlot(int foodIndex) => (int)FoodGoods[foodIndex];

    /// <summary>Stock of a good at a settlement, in the good's unit.</summary>
    public Fix64 StoreAt(int settlementIndex, int slot) => Stock(_settlements[settlementIndex], slot);

    /// <summary>Rations in store (one adult eats one ration a season).</summary>
    public Fix64 FoodStoresAt(int settlementIndex) => FoodStores(_settlements[settlementIndex]);

    /// <summary>The most food a settlement keeps: 12 rations, 15 more for a pit and 80 more for a granary.</summary>
    public Fix64 FoodStorageCapacity(int settlementIndex) => FoodStorageCapacity(_settlements[settlementIndex]);

    private static Fix64 FoodStorageCapacity(SettlementState settlement)
        => Fix64.FromInt(12 + 15 * settlement.Structures[(int)Structure.StoragePit] +
                         80 * settlement.Structures[(int)Structure.Granary]);

    /// <summary>Rations the settlement eats when the current season ends.</summary>
    public Fix64 FoodNeedThisSeason(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        bool winter = _seasonsCompleted % 4 == 3;
        Fix64 need = FoodNeed(s, winter, includeHarshWinter: false);
        return s.UnderInvestment ? need * Fix64.FromRatio(75, 100) : need;
    }

    /// <summary>Wood the settlement's fires and kilns burn in the current season.</summary>
    public Fix64 FuelThisSeason(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        bool winter = _seasonsCompleted % 4 == 3;
        Fix64 fuel = Fix64.Zero;
        if (s.Structures[(int)Structure.Hearth] > 0) fuel += winter ? Fix64.FromInt(3) : Fix64.FromRatio(3, 2);
        else if (s.FireLit && !HasProject(s.Civilization, "FIR-02")) fuel += Fix64.One;
        return fuel + E3FuelNeed(s, winter);
    }

    /// <summary>The growth limits of a settlement and the factors that slow its births (all as readouts, nothing is cached).</summary>
    public readonly record struct GrowthReadout(
        int Population, Fix64 FoodCap, Fix64 HousingCap, Fix64 GroupCap, Fix64 LeanCap,
        Fix64 FoodSecurity, Fix64 HealthFactor, Fix64 CrowdingFreeFactor);

    public GrowthReadout Growth(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        int population = Population(s.Id);
        Fix64 foodCap = FoodCap(s), housingCap = HousingCap(s);
        Fix64 cap = Fix64.Min(foodCap, housingCap);
        Fix64 density = cap <= Fix64.Zero ? Fix64.One : Fix64.Min(Fix64.One, Fix64.FromInt(population) / cap);
        Fix64 crowding = Fix64.One;
        for (int power = 0; power < 6; power++) crowding *= density;
        return new GrowthReadout(population, foodCap, housingCap, GroupCap(s), LeanFoodCapacity(s),
            s.FoodSecurity, FertilityHealthFactor(s), Fix64.One - crowding);
    }

    /// <summary>
    /// The last completed season's food ledger in rations: start, gathered, eaten, spoiled, lost to full stores, end and need,
    /// then what was gathered of each food good. Returns false before a season has ended at the settlement.
    /// </summary>
    public bool FoodLedgerLines(int settlementIndex, Span<Fix64> into)
    {
        if (into.Length < FoodLedgerLineCount) throw new ArgumentException("Too small.", nameof(into));
        into.Slice(0, FoodLedgerLineCount).Clear();
        SettlementState s = _settlements[settlementIndex];
        if (!s.LedgerValid) return false;
        into[0] = s.LedgerStart;
        into[1] = Fix64.Max(Fix64.Zero, s.LedgerBeforeEat - s.LedgerStart);
        into[2] = s.LedgerEaten;
        into[3] = Fix64.Max(Fix64.Zero, s.LedgerBeforeEat - s.LedgerEaten - s.LedgerAfterSpoil);
        into[4] = Fix64.Max(Fix64.Zero, s.LedgerAfterSpoil - s.LedgerEnd);
        into[5] = s.LedgerEnd;
        into[6] = s.LedgerNeed;
        for (int g = 0; g < FoodGoodCount; g++) into[FoodLedgerSummaryLines + g] = s.LedgerGathered[g];
        return true;
    }

    private void BeginFoodLedger(SettlementState s)
    {
        Array.Clear(s.LedgerAssigned); Array.Clear(s.LedgerApplied); Array.Clear(s.LedgerRequested);
        Array.Clear(s.LedgerHarvested); Array.Clear(s.LedgerOutput); Array.Clear(s.LedgerEatenByGood); Array.Clear(s.LedgerHazard);
        s.LedgerCapacity = s.LedgerPlannable = s.LedgerFoodWork = s.LedgerBuildWork = s.LedgerMaterialWork = s.LedgerHerdWork = s.LedgerCivicWork = Fix64.Zero;
        s.LedgerFoodShareRule = 0;
        for (int g = 0; g < FoodGoodCount; g++) s.LedgerStartStores[g] = s.Stores[(int)FoodGoods[g]];
        s.LedgerStart = FoodStores(s);
    }

    private void RecordFoodLedgerBeforeEating(SettlementState s, Fix64 available, Fix64 need)
    {
        s.LedgerBeforeEat = available;
        s.LedgerNeed = need;
        s.LedgerEaten = Fix64.Min(available, need);
        for (int g = 0; g < FoodGoodCount; g++)
        {
            Good good = FoodGoods[g];
            s.LedgerGathered[g] = (s.Stores[(int)good] - s.LedgerStartStores[g]) * FoodValue(good, s);
        }
    }
}
