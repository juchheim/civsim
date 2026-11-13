using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// Faith &amp; Fief mechanical power and the blast furnace (docs/MVP/27 §5.1 and §5.2; M12 WP-E5-5), built on the Iron chapter's water mill (docs/MVP/29): a mill town
/// does its saw, bellows and forge-hammer work at <c>MillLaborMult</c> of the labor. Everything here is gated on <see cref="FiefChain"/>, so an Iron or Bronze economy is
/// byte-for-byte what it was. All numbers are provisional and come from the E5 oracle screens (e5_power_oracle.py).
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>The share of seasons a windmill turns (P, docs/MVP/27 §5.1); a deterministic factor, never a roll.</summary>
    private static readonly Fix64 WindAvailability = Fix64.FromRatio(8, 10);

    /// <summary>Labor of a powered sawyer against a hand sawyer with a T12 saw: WOD-09 "Saw: Mill x4".</summary>
    private static readonly Fix64 SawmillLaborMult = Fix64.FromRatio(1, 4);

    /// <summary>
    /// How much of a full water mill's power a town has: 1 with a water mill, the wind's availability with only a windmill (NRG-05), 0 with neither. Zero
    /// outside the Iron chapter; the windmill counts only in a Fief match.
    /// </summary>
    private Fix64 MillFactor(SettlementState s)
    {
        if (!IronChain) return Fix64.Zero;
        if (IronWorksCount(s, IronStructure.WaterMill) > 0) return Fix64.One;
        return FiefChain && IronWorksCount(s, IronStructure.Windmill) > 0 && Done(s.Civilization, "NRG-05") ? WindAvailability : Fix64.Zero;
    }

    /// <summary>Whether a town has any mill to drive a bellows, a saw or a hammer.</summary>
    private bool HasMill(SettlementState s) => MillFactor(s) > Fix64.Zero;

    /// <summary>
    /// The labor multiplier a mill town's powered work gets: <c>MillLaborMult</c> with a water mill, a share of that cut with a windmill (it turns four seasons in
    /// five), and exactly 1 with none. For a water mill it equals the Iron chapter's own multiplier.
    /// </summary>
    private Fix64 MillLaborFactor(SettlementState s)
    {
        Fix64 factor = MillFactor(s);
        return factor <= Fix64.Zero ? Fix64.One : Fix64.One - (Fix64.One - _civic.MillLabor) * factor;
    }

    /// <summary>Off, a built blast furnace changes nothing. Diagnostics only.</summary>
    internal bool FurnaceEffectEnabled { get; set; } = true;

    /// <summary>A built blast furnace (MET-12) in a town with a mill makes each bloomery batch four times as large (docs/MVP/27 §5.2).</summary>
    private bool FurnaceActive(SettlementState s, CompiledRecipe r)
        => FiefChain && FurnaceEffectEnabled && r.Def.Id == "bloomery" && s.Metal is not null && IronWorksCount(s, IronStructure.BlastFurnace) > 0 &&
           Done(s.Civilization, "MET-12") && HasMill(s);

    /// <summary>Iron out of a furnace batch against a bloomery batch (catalog: "Iron output x4").</summary>
    private static readonly Fix64 FurnaceOutputMult = Fix64.FromInt(4);

    /// <summary>Charcoal of a furnace batch against a bloomery batch: twice for four times the iron, so half the charcoal per litre (P; e5_power_oracle).</summary>
    private static readonly Fix64 FurnaceCharcoalMult = Fix64.FromInt(2);

    /// <summary>Labor of a furnace batch against a bloomery batch: twice for four times the iron, so half the labor per litre (P).</summary>
    private static readonly Fix64 FurnaceLaborMult = Fix64.FromInt(2);

    /// <summary>The Fief chapter's recipe-labor changes, applied on top of <see cref="RecipeLabor"/>: the sawmill, the spinning wheel and treadle loom, and fulling.</summary>
    private Fix64 FiefRecipeLabor(SettlementState s, CompiledRecipe r, Fix64 labor)
    {
        if (!FiefChain) return labor;
        int civ = s.Civilization;
        string id = r.Def.Id;
        if (id == "saw_planks" && IronWorksCount(s, IronStructure.Sawmill) > 0 && Done(civ, "WOD-09")) return r.Def.Labor * SawmillLaborMult;
        if (id == "spin_yarn" && Done(civ, "FIB-11")) labor *= Fix64.FromRatio(1, 3);                      // spinning wheel x3
        if (id == "weave_cloth" && Done(civ, "FIB-10")) labor *= Fix64.Half;                               // treadle loom x2
        if (id == "weave_cloth" && Done(civ, "MEC-07") && HasMill(s)) labor *= MillLaborFactor(s);          // a fulling mill in a mill town
        if (FurnaceActive(s, r)) labor *= FurnaceLaborMult;
        return labor;
    }

    // ---- fixtures (tests only) ------------------------------------------------------------------------

    internal void FixtureSetIronWork(int settlementIndex, string kind, int count)
    {
        MetalState m = EnsureMetal(_settlements[settlementIndex]);
        for (int k = 0; k < (int)IronStructure.Count; k++)
            if (IronWorkId(k) == kind) { (m.IronWorks ??= new int[(int)IronStructure.Count])[k] = count; return; }
        throw new ArgumentException($"No Iron structure '{kind}'.", nameof(kind));
    }

    private CompiledRecipe FixtureRecipe(string id)
    {
        foreach (CompiledRecipe r in _allRecipes) if (r.Def.Id == id) return r;
        throw new ArgumentException($"Unknown recipe '{id}'.", nameof(id));
    }

    /// <summary>One batch of a recipe at a town as the planner sees it now: output, first and second input, and labor.</summary>
    internal (Fix64 Output, Fix64 Input0, Fix64 Input1, Fix64 Labor) FixtureBatch(int settlementIndex, string recipeId)
    {
        SettlementState s = _settlements[settlementIndex];
        CompiledRecipe r = FixtureRecipe(recipeId);
        return (OutputQuantity(s, r), InputQuantity(s, r, 0), r.InputSlots.Length > 1 ? InputQuantity(s, r, 1) : Fix64.Zero, RecipeLabor(s, r, anyMetalKit: false));
    }

    internal Fix64 FixtureGrainValue(int settlementIndex) => FoodValue(Good.Grain, _settlements[settlementIndex]);
    internal bool FixtureIronGateOpen(int settlementIndex, string kind, bool includeAppetite = true)
    {
        for (int k = 0; k < (int)IronStructure.Count; k++)
            if (IronWorkId(k) == kind) return IronGate(_settlements[settlementIndex], (IronStructure)k, includeAppetite).Open;
        throw new ArgumentException($"No Iron structure '{kind}'.", nameof(kind));
    }
}
