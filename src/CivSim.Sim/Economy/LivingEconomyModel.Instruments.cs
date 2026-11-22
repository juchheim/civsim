using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// The glass-and-instruments chain (M13 WP-E6-7 slice 3, docs/MVP/38 §5.3): alkali and acid (CHM-08), plate glass (CER-11), precision parts (MEC-09),
/// chronometers (WAY-10) and telescopes (INQ-07). The goods are Discovery goods and the recipes are the registry's, so the planner's ordinary
/// back-chaining makes them once something wants them; this file is that want and what the goods are for. Discovery only; nothing outside it changes.
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>A civilization's instrument of a kind is "held" when one stands in store in any of its towns (the realm's ships draw on its stores).</summary>
    private bool InstrumentHeld(int civ, DiscoveryGood good)
    {
        if (!DiscoveryChain) return false;
        int slot = DiscoverySlot(good);
        foreach (SettlementState s in _settlements)
            if (IsMember(s, civ) && Stock(s, slot) >= Fix64.One) return true;
        return false;
    }

    /// <summary>WAY-10: the civilization holds a marine chronometer (the navigator's staffing is not modelled; the instrument carries the benefit).</summary>
    public bool ChronometerHeld(int civ) => InstrumentHeld(civ, DiscoveryGood.Chronometer);

    /// <summary>INQ-07: the civilization holds a telescope.</summary>
    public bool TelescopeHeld(int civ) => InstrumentHeld(civ, DiscoveryGood.Telescope);

    /// <summary>
    /// CHM-08: while a town holds soda alkali its soap and glass come out at the row's multipliers (soap_output, glass_output: "soda replaces ash lye",
    /// "alkali flux in plate and blown glass"). The alkali is held, not used up; plate glass is what consumes it.
    /// </summary>
    private Fix64 AlkaliOutputFactor(SettlementState s, CompiledRecipe r)
    {
        if (!DiscoveryChain || r.Def.Outputs.Length == 0 || !Done(s.Civilization, "CHM-08")) return Fix64.One;
        string? stat = r.Def.Outputs[0].Good switch { "G-SOAP" => "soap_output", "G-GLASS" or "G-BLOWNGLASS" => "glass_output", _ => null };
        if (stat is null || Stock(s, DiscoverySlot(DiscoveryGood.Alkali)) < Fix64.One) return Fix64.One;
        Fix64 m = Modifier(s.Civilization, stat);
        return m > Fix64.One ? m : Fix64.One;
    }

    /// <summary>Plate glass standing in the stores of all the civilization's towns: the Palace's glass is carried to it by the realm's roads and ships.</summary>
    private Fix64 RealmPlateGlass(int civ)
    {
        if (!DiscoveryChain) return Fix64.Zero;
        int slot = DiscoverySlot(DiscoveryGood.PlateGlass);
        Fix64 total = Fix64.Zero;
        foreach (SettlementState o in _settlements) if (IsMember(o, civ)) total += Stock(o, slot);
        return total;
    }

    /// <summary>Takes plate glass for a Wonder: the building town's own first, then the realm's other towns in ascending id.</summary>
    private void PayRealmPlateGlass(SettlementState builder, int amount)
    {
        int slot = DiscoverySlot(DiscoveryGood.PlateGlass);
        Fix64 owed = Fix64.FromInt(amount);
        Fix64 own = Fix64.Min(owed, Stock(builder, slot));
        if (own > Fix64.Zero) { AddStock(builder, slot, -own); owed -= own; }
        foreach (SettlementState o in _settlements)
        {
            if (owed <= Fix64.Zero) break;
            if (o.Id == builder.Id || !IsMember(o, builder.Civilization)) continue;
            Fix64 take = Fix64.Min(owed, Stock(o, slot));
            if (take > Fix64.Zero) { AddStock(o, slot, -take); owed -= take; }
        }
    }

    private bool PalaceUnderConstruction(int civ)
    {
        foreach (SettlementState o in _settlements)
            if (o.Civilization == civ && o.Metal is { IronBuildKind: (int)IronStructure.RenaissancePalace }) return true;
        return false;
    }

    private void WantDiscoveryGood(MetalPlan plan, SettlementState s, DiscoveryGood good, Fix64 target)
    {
        int slot = DiscoverySlot(good);
        if (target > Stock(s, slot)) plan.Terminal[slot] = Fix64.Max(plan.Terminal[slot], target);
    }

    /// <summary>
    /// What the planner wants of the instruments chain (terminal wants; the recipes back-chain the rest): a little alkali in every town with CHM-08, and one
    /// chronometer or telescope at each harbor town once the civilization can make them. Acid has no consumer in the sim and is never wanted.
    /// </summary>
    private void DiscoveryInstrumentWants(SettlementState s, MetalPlan plan)
    {
        if (!DiscoveryChain) return;
        int civ = s.Civilization;
        if (Done(civ, "CHM-08") && Population(s.Id) >= 50) WantDiscoveryGood(plan, s, DiscoveryGood.Alkali, Fix64.FromInt(2));
        // Alkali needs salt and only some towns hold any, so the instruments are wanted in every town of size until the realm holds one: the towns that can make
        // one do, and the rest find the plan infeasible and spend nothing.
        // CER-11: a pane of plate glass glazes the windows, and with a staffed garden it is a greenhouse; the glass is held, not used up.
        if (Done(civ, "CER-11") && GardenStaffed(s)) WantDiscoveryGood(plan, s, DiscoveryGood.PlateGlass, Fix64.One);
        // The Renaissance Palace (BLD-13) draws its plate glass from the realm's stores: while one is being built, towns of size make it until the realm holds enough.
        if (Population(s.Id) >= 50 && PalaceUnderConstruction(civ) && RealmPlateGlass(civ) < Fix64.FromInt(IronSpecs[(int)IronStructure.RenaissancePalace].PlateGlass))
            WantDiscoveryGood(plan, s, DiscoveryGood.PlateGlass, Fix64.FromInt(IronSpecs[(int)IronStructure.RenaissancePalace].PlateGlass));
        if (!Done(civ, "MEC-09") || Population(s.Id) < 50) return;
        if (Done(civ, "WAY-10") && !ChronometerHeld(civ)) WantDiscoveryGood(plan, s, DiscoveryGood.Chronometer, Fix64.One);
        if (Done(civ, "INQ-07") && Done(civ, "CER-11") && !TelescopeHeld(civ)) WantDiscoveryGood(plan, s, DiscoveryGood.Telescope, Fix64.One);
    }

    /// <summary>Stock of a Discovery good (coal, coke or one of the instruments chain's) in a settlement, for diagnostics (zero outside Discovery).</summary>
    public Fix64 DiscoveryGoodStock(int settlementIndex, string goodId)
        => DiscoveryChain && (uint)settlementIndex < (uint)_settlements.Count ? Stock(_settlements[settlementIndex], SlotOf(goodId)) : Fix64.Zero;

    /// <summary>Stock of any registry good in a settlement, for diagnostics outside the simulation assembly.</summary>
    public Fix64 FiefStockPublic(int settlementIndex, string goodId)
        => (uint)settlementIndex < (uint)_settlements.Count && SlotOf(goodId) is var slot and >= 0 ? Stock(_settlements[settlementIndex], slot) : Fix64.Zero;

    internal bool FixtureChronometerHeld(int civ) => ChronometerHeld(civ);
    internal bool FixtureTelescopeHeld(int civ) => TelescopeHeld(civ);
    internal Fix64 FixtureAlkaliFactor(int settlementIndex, string recipeId) => AlkaliOutputFactor(_settlements[settlementIndex], FixtureRecipeById(recipeId));
    internal bool FixtureRecipeEnabled(string recipeId) => FixtureRecipeById(recipeId).Enabled;
    internal Fix64 FixtureDiscoveryStock(int settlementIndex, string goodId) => Stock(_settlements[settlementIndex], SlotOf(goodId));
}
