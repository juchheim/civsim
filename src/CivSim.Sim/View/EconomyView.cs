namespace CivSim.Sim.View;

/// <summary>
/// Layout of <see cref="MatchFrame.SettlementEcon"/>: <see cref="Stride"/> longs per settlement row, filled for the viewer's
/// own settlements and refreshed once a season (docs/MVP "Your people" sheet). Quantities are raw Q32.32 values (rations,
/// people or goods); shares are permille.
/// </summary>
public static class EconField
{
    public const int Stride = 72;

    /// <summary>1 when the row was filled (own, visible settlement).</summary>
    public const int Valid = 0;
    /// <summary>Rations the stores hold at most.</summary>
    public const int FoodCapacity = 1;
    /// <summary>Rations eaten when the current season ends.</summary>
    public const int FoodNeed = 2;
    /// <summary>Share of last season's need that was met, permille (1000 when fully fed).</summary>
    public const int FoodSecurityPermille = 3;
    /// <summary>Late-winter forecast share, permille; only updated each autumn.</summary>
    public const int ForecastPermille = 4;
    /// <summary>People the land feeds, people the housing holds, people a band holds together, and the lean-year food capacity.</summary>
    public const int FoodCap = 5, HousingCap = 6, GroupCap = 7, LeanCap = 8;
    /// <summary>The factors that slow births, permille: health of fertile women and room left before crowding.</summary>
    public const int HealthPermille = 9, CrowdingFreePermille = 10;
    /// <summary>Wood the fires burn this season.</summary>
    public const int FuelPerSeason = 11;
    /// <summary>1 once a season has ended at the settlement, so the ledger lines hold real numbers.</summary>
    public const int LedgerValid = 12;
    /// <summary>First of <see cref="Economy.LivingEconomyModel.FoodLedgerLineCount"/> ledger lines.</summary>
    public const int Ledger = 13;

    // Ledger line offsets from Ledger.
    public const int LedgerStart = 0, LedgerGathered = 1, LedgerEaten = 2, LedgerSpoiled = 3, LedgerLostToCap = 4, LedgerEnd = 5, LedgerNeed = 6;
    public const int LedgerGatheredByFood = 7;

    /// <summary>Last season's work and output per action: <see cref="Economy.LivingEconomyModel.ActionLedgerFields"/> numbers for each of forage, hunt, fish, farm and chop.</summary>
    public const int Actions = Ledger + Economy.LivingEconomyModel.FoodLedgerLineCount;
    /// <summary>Last season's labor split (<see cref="Economy.LivingEconomyModel.LaborLineCount"/> lines: work-seasons, then the food-share rule).</summary>
    public const int Labor = Actions + Economy.LivingEconomyModel.ActionLedgerLineCount;
    /// <summary>What the builders wait for: structure kind (-1 none), good slot (-1 food), then in store and needed (raw Q32.32).</summary>
    public const int BlockerStructure = Labor + Economy.LivingEconomyModel.LaborLineCount;
    public const int BlockerSlot = BlockerStructure + 1, BlockerHave = BlockerStructure + 2, BlockerNeed = BlockerStructure + 3;
    public const int End = BlockerNeed + 1;
}

/// <summary>Series of <see cref="MatchFrame.SettlementHistory"/>; per-good stock series follow <see cref="StockBase"/>.</summary>
public static class HistorySeries
{
    public const int Population = 0, FoodStores = 1, FoodSecurityPermille = 2, Gathered = 3, Eaten = 4, Spoiled = 5, LostToCap = 6;
    public const int StockBase = 7;
    public const int CivPopulation = 0, CivInsight = 1, CivProjects = 2, CivSeries = 3;
}
