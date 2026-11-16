using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// The Faith &amp; Fief chapter's switch on the economy (docs/MVP/27, M12 WP-E5-4). Like <see cref="IronChain"/> it is set once from the chapter
/// rules; off, the economy is byte-for-byte the Iron or Bronze economy. It contains the Iron chain: turning it on turns the Iron chain on.
/// <para>
/// **Plumbing only.** No E5 system reads it yet, so a Fief economy plays exactly like an Iron one except that E5 project completions are
/// recorded (<see cref="ProjectCeiling"/>). Each E5 system added later (power ledger, blast furnace, monasteries, powder and the rest) gates on
/// this flag and keeps its state in its own snapshot and hash section, written only when the flag is on, as the Iron state is. The raw-era
/// lookups that index the registry by research era (reach, claim radius, mortality rows read through <c>LivingEconomySource</c> and
/// <c>PoliticsSystem</c>) will read the E5 row in a Fief match; that is intended and is audited in docs/MVP/balance/m12-wp6-mode-plumbing.md.
/// </para>
/// </summary>
public sealed partial class LivingEconomyModel
{
    private bool _fiefChain;

    /// <summary>True when the match plays the Faith &amp; Fief chapter. Setting it also turns the Iron chain on.</summary>
    public bool FiefChain
    {
        get => _fiefChain;
        init
        {
            _fiefChain = value;
            if (value)
            {
                IronChain = true;
                _compiledRecipes = _allRecipes;
            }
        }
    }

    private static readonly Fix64 PowderPerCannonShot = Fix64.FromRatio(1, 4);
    private static readonly Fix64 BaseFiefRecipeWaste = Fix64.FromRatio(1, 20);

    /// <summary>
    /// The SOC-11 craft-loss allowance is a loss of "non-kit crafted goods" (catalog note). Tool kits keep their service-life rule, and ore reduction and metal refining
    /// (smelting, alloying, bloomery, wrought iron, steel) make intermediate metal, not crafted goods, so they carry no craft loss: their fuel and ore are already
    /// accounted by the I-T6 fuel ledger, and a flat 5% on them put the Fief ramp-up window over the F-T3 15% work-share rule (docs/MVP/balance/m12-e5-ft3-fit-2026-11-16.md).
    /// </summary>
    private static bool FiefCraftLossApplies(CompiledRecipe r)
    {
        string id = r.Def.Id;
        if (id.StartsWith("make_tool", StringComparison.Ordinal) || id.StartsWith("cast_t", StringComparison.Ordinal)) return false;
        if (id.StartsWith("smelt_", StringComparison.Ordinal) || id.StartsWith("cosmelt_", StringComparison.Ordinal)) return false;
        return id is not ("alloy_bronze" or "bloomery" or "forge_wrought" or "carburize_steel");
    }

    private Fix64 FiefRecipeWasteRate(int civilization)
        => Done(civilization, "SOC-11") ? BaseFiefRecipeWaste / Fix64.Parse("1.2") : BaseFiefRecipeWaste;

    /// <summary>Fief craft-role training exposure in work-seasons; guild apprenticeship speed affects only this quantity.</summary>
    internal Fix64 FiefCraftRoleTraining(int settlementIndex)
        => FiefChain && _settlements[settlementIndex].Civic is { } civic ? civic.CraftRoleTraining : Fix64.Zero;

    /// <summary>Cumulative modeled recipe-input loss, including the guild reduction, for focused diagnostics.</summary>
    internal Fix64 FiefRecipeWaste(int settlementIndex)
        => FiefChain && _settlements[settlementIndex].Civic is { } civic ? civic.GuildRecipeWaste : Fix64.Zero;

    /// <summary>Accumulated output-independent craft quality units (one per batch; 1.2 with SOC-11).</summary>
    internal Fix64 FiefCraftQuality(int settlementIndex)
        => FiefChain && _settlements[settlementIndex].Civic is { } civic ? civic.CraftQualityUnits : Fix64.Zero;

    /// <summary>Read-only Fief weapon stock for the owning civilization's AI view.</summary>
    internal Fix64 FiefStock(int settlementIndex, string goodId)
    {
        if (!FiefChain) return Fix64.Zero;
        int slot = SlotOf(goodId);
        return slot < 0 ? Fix64.Zero : Stock(_settlements[settlementIndex], slot);
    }

    /// <summary>Draws Fief cannons from a settlement's existing stock for an active siege.</summary>
    public int TakeCannons(int settlementIndex, int max)
    {
        if (!FiefChain || max <= 0) return 0;
        SettlementState settlement = _settlements[settlementIndex];
        int count = Math.Min(max, Stock(settlement, FiefSlot(FiefGood.Cannon)).FloorToInt());
        if (count > 0) AddStock(settlement, FiefSlot(FiefGood.Cannon), Fix64.FromInt(-count));
        return count;
    }

    /// <summary>Returns unused siege cannons to their home settlement.</summary>
    public void ReturnCannons(int settlementIndex, int count)
    {
        if (!FiefChain || count <= 0) return;
        AddStock(_settlements[settlementIndex], FiefSlot(FiefGood.Cannon), Fix64.FromInt(count));
    }

    /// <summary>Consumes a quarter unit of existing powder for each cannon that fires this season; returns full shots paid.</summary>
    public int PayCannonPowder(int settlementIndex, int requestedShots)
    {
        if (!FiefChain || requestedShots <= 0) return 0;
        SettlementState settlement = _settlements[settlementIndex];
        int shots = Math.Min(requestedShots, (Stock(settlement, FiefSlot(FiefGood.Powder)) / PowderPerCannonShot).FloorToInt());
        if (shots > 0) AddStock(settlement, FiefSlot(FiefGood.Powder), -PowderPerCannonShot * shots);
        return shots;
    }

    private void FiefWeaponWants(SettlementState settlement, MetalPlan plan)
    {
        FiefCommunicationWants(settlement, plan);
        if (!FiefChain || !Done(settlement.Civilization, "WAR-12") ||
            !Done(settlement.Civilization, "CHM-07") ||
            IronWorksCount(settlement, IronStructure.BlastFurnace) == 0 ||
            IronWorksCount(settlement, IronStructure.PowderMill) == 0 ||
            Stock(settlement, FiefSlot(FiefGood.Saltpeter)) <= Fix64.Zero && !HasFiefDepositInReach(settlement, FiefGood.Saltpeter) ||
            Stock(settlement, FiefSlot(FiefGood.Sulfur)) <= Fix64.Zero && !HasFiefDepositInReach(settlement, FiefGood.Sulfur)) return;
        plan.Terminal[FiefSlot(FiefGood.Cannon)] = Fix64.One;
        plan.Terminal[FiefSlot(FiefGood.Powder)] = PowderPerCannonShot;
    }

    internal void FixtureSetGoodStock(int settlementIndex, string goodId, Fix64 amount)
    {
        int slot = SlotOf(goodId);
        if (slot == NoSlot) throw new ArgumentException($"Unknown good '{goodId}'.", nameof(goodId));
        AddStock(_settlements[settlementIndex], slot, amount - Stock(_settlements[settlementIndex], slot));
    }

    internal Fix64 FixtureGoodStock(int settlementIndex, string goodId)
    {
        int slot = SlotOf(goodId);
        if (slot == NoSlot) throw new ArgumentException($"Unknown good '{goodId}'.", nameof(goodId));
        return Stock(_settlements[settlementIndex], slot);
    }
}
