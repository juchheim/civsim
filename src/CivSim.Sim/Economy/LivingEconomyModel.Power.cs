using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

internal readonly record struct MillPowerLedgerSnapshot(
    int SettlementId, int Civilization, int Season, SettlementTier Tier, int Population,
    int WaterMillSources, int WindmillSources,
    Fix64 AvailablePowerHours,
    Fix64 GrindingDemand, Fix64 GrindingUsed,
    Fix64 SawingDemand, Fix64 SawingUsed,
    Fix64 CraftDemand, Fix64 CraftUsed,
    Fix64 UnusedPowerHours,
    Fix64 GrainMilledPeople, Fix64 GrainCoverage);

/// <summary>
/// Faith &amp; Fief mechanical power and the blast furnace (docs/MVP/27 §5.1 and §5.2; M12 WP-E5-5), built on the Iron chapter's water mill (docs/MVP/29): a mill town
/// shares one seasonal power-hour budget among grinding, sawing and powered crafts. Everything new is gated on <see cref="FiefChain"/>, so an Iron or Bronze
/// economy is byte-for-byte what it was. All numbers are provisional and come from the E5 oracle screens (e5_power_oracle.py).
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>The share of seasons a windmill turns (P, docs/MVP/27 §5.1); a deterministic factor, never a roll.</summary>
    private static readonly Fix64 WindAvailability = Fix64.FromRatio(8, 10);

    // One power hour serves one worker for one season. Job shares and multipliers are the provisional E5 oracle inputs.
    private static readonly Fix64 WaterMillPowerHours = Fix64.FromInt(5);
    private static readonly Fix64 WindmillPowerHours = Fix64.FromInt(4);
    private static readonly Fix64 GrindingWorkShare = Fix64.FromRatio(6, 100);
    private static readonly Fix64 SawingWorkShare = Fix64.FromRatio(2, 100);
    private static readonly Fix64 PoweredCraftWorkShare = Fix64.FromRatio(4, 100);
    private static readonly Fix64 GrindingMultiplier = Fix64.FromInt(5);
    private static readonly Fix64 SawingMultiplier = Fix64.FromInt(4);
    private static readonly Fix64 PoweredCraftMultiplier = Fix64.FromInt(2);

    /// <summary>Labor of a powered sawyer against a hand sawyer with a T12 saw: WOD-09 "Saw: Mill x4".</summary>
    private static readonly Fix64 SawmillLaborMult = Fix64.FromRatio(1, 4);

    private enum MillPowerJob : byte { Grinding, Sawing, Craft }

    private readonly record struct MillPowerAllocation(
        int Population, int WaterSources, int WindSources, Fix64 Available,
        Fix64 GrindingDemand, Fix64 GrindingUsed,
        Fix64 SawingDemand, Fix64 SawingUsed,
        Fix64 CraftDemand, Fix64 CraftUsed,
        Fix64 Unused, Fix64 GrainMilledPeople, Fix64 GrainCoverage)
    {
        public Fix64 Coverage(MillPowerJob job) => job switch
        {
            MillPowerJob.Grinding => JobCoverage(GrindingUsed, GrindingDemand),
            MillPowerJob.Sawing => JobCoverage(SawingUsed, SawingDemand),
            _ => JobCoverage(CraftUsed, CraftDemand),
        };

        private static Fix64 JobCoverage(Fix64 used, Fix64 demand)
            => demand > Fix64.Zero ? Fix64.Min(Fix64.One, used / demand) : Fix64.Zero;
    }

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

    /// <summary>
    /// Deterministic per-season allocation, in worker-season power hours. Demand is the oracle's modeled job mix; grain
    /// coverage is the resident share served, capped by MillPeople per source and by the grinding share actually funded.
    /// </summary>
    private MillPowerAllocation MillPower(SettlementState s, int extraWaterSources = 0, int extraWindSources = 0)
    {
        if (!FiefChain)
            return new MillPowerAllocation(Population(s.Id), 0, 0, Fix64.Zero,
                Fix64.Zero, Fix64.Zero, Fix64.Zero, Fix64.Zero, Fix64.Zero, Fix64.Zero,
                Fix64.Zero, Fix64.Zero, Fix64.Zero);

        int population = Population(s.Id);
        int waterSources = IronWorksCount(s, IronStructure.WaterMill) + extraWaterSources;
        int windSources = Done(s.Civilization, "NRG-05") ? IronWorksCount(s, IronStructure.Windmill) + extraWindSources : 0;
        Fix64 adults = Fix64.FromInt(population);
        Fix64 available = Fix64.FromInt(waterSources) * WaterMillPowerHours +
                          Fix64.FromInt(windSources) * WindmillPowerHours * WindAvailability;
        Fix64 grindingDemand = adults * GrindingWorkShare / GrindingMultiplier;
        bool hasPoweredSawmill = (waterSources > 0 || windSources > 0) &&
                                 IronWorksCount(s, IronStructure.Sawmill) > 0 && Done(s.Civilization, "WOD-09");
        Fix64 sawingDemand = hasPoweredSawmill ? adults * SawingWorkShare / SawingMultiplier : Fix64.Zero;
        Fix64 craftDemand = adults * PoweredCraftWorkShare / PoweredCraftMultiplier;

        Fix64 remaining = available;
        Fix64 grindingUsed = Fix64.Min(remaining, grindingDemand);
        remaining -= grindingUsed;
        Fix64 sawingUsed = Fix64.Min(remaining, sawingDemand);
        remaining -= sawingUsed;
        Fix64 craftUsed = Fix64.Min(remaining, craftDemand);
        remaining -= craftUsed;

        int sourceCount = waterSources + windSources;
        Fix64 sourceAvailability = sourceCount > 0
            ? (Fix64.FromInt(waterSources) + Fix64.FromInt(windSources) * WindAvailability) / Fix64.FromInt(sourceCount)
            : Fix64.Zero;
        Fix64 millCapacity = _civic.MillPeople * Fix64.FromInt(sourceCount);
        Fix64 eligiblePeople = Fix64.Min(adults, millCapacity);
        Fix64 grindingCoverage = grindingDemand > Fix64.Zero ? Fix64.Min(Fix64.One, grindingUsed / grindingDemand) : Fix64.Zero;
        // Preserve the first-pass mill-capacity rule: each source serves MillPeople, while an intermittent wind source
        // contributes only its availability share. The ledger can further reduce this by rationing grinding power.
        Fix64 grainMilledPeople = eligiblePeople * sourceAvailability * grindingCoverage;
        Fix64 grainCoverage = population > 0 ? grainMilledPeople / adults : Fix64.Zero;
        return new MillPowerAllocation(population, waterSources, windSources, available,
            grindingDemand, grindingUsed, sawingDemand, sawingUsed, craftDemand, craftUsed,
            remaining, grainMilledPeople, grainCoverage);
    }

    private Fix64 MillPoweredLaborFactor(SettlementState s, MillPowerJob job, Fix64 poweredLaborMult)
    {
        if (!FiefChain) return MillLaborFactor(s);
        Fix64 coverage = MillPower(s).Coverage(job);
        return Fix64.One - (Fix64.One - poweredLaborMult) * coverage;
    }

    /// <summary>Read-only power budget for every town/city, including source-free rows needed to close coverage windows.</summary>
    internal IReadOnlyList<MillPowerLedgerSnapshot> CaptureMillPowerSnapshots(int season)
    {
        if (!FiefChain) return Array.Empty<MillPowerLedgerSnapshot>();
        var snapshots = new List<MillPowerLedgerSnapshot>(_settlements.Count);
        foreach (SettlementState s in _settlements)
        {
            SettlementTier tier = Tier(s.Id);
            if (tier < SettlementTier.Town) continue;
            MillPowerAllocation p = MillPower(s);
            snapshots.Add(new MillPowerLedgerSnapshot(s.Id, s.Civilization, season, tier, p.Population,
                p.WaterSources, p.WindSources, p.Available, p.GrindingDemand, p.GrindingUsed,
                p.SawingDemand, p.SawingUsed, p.CraftDemand, p.CraftUsed, p.Unused,
                p.GrainMilledPeople, p.GrainCoverage));
        }
        return snapshots;
    }

    /// <summary>Off, a built blast furnace changes nothing. Diagnostics only.</summary>
    internal bool FurnaceEffectEnabled { get; set; } = true;

    /// <summary>A built blast furnace (MET-12) in a town with a mill makes each bloomery batch four times as large (docs/MVP/27 §5.2).</summary>
    private bool FurnaceActive(SettlementState s, CompiledRecipe r)
        => FiefChain && FurnaceEffectEnabled && r.Def.Id == "bloomery" && s.Metal is not null && IronWorksCount(s, IronStructure.BlastFurnace) > 0 &&
           Done(s.Civilization, "MET-12") && HasMill(s);

    /// <summary>Output factor interpolates from a hand bloomery to the catalog's x4 batch by the shared craft allocation.</summary>
    private Fix64 FurnaceOutputFactor(SettlementState s) => Fix64.One + Fix64.FromInt(3) * MillPower(s).Coverage(MillPowerJob.Craft);

    /// <summary>Fuel and labor interpolate to x2 only for the craft work actually covered by the shared power budget.</summary>
    private Fix64 FurnaceInputAndLaborFactor(SettlementState s) => Fix64.One + MillPower(s).Coverage(MillPowerJob.Craft);

    /// <summary>The Fief chapter's recipe-labor changes, applied on top of <see cref="RecipeLabor"/>: the sawmill, the spinning wheel and treadle loom, and fulling.</summary>
    private Fix64 FiefRecipeLabor(SettlementState s, CompiledRecipe r, Fix64 labor)
    {
        if (!FiefChain) return labor;
        int civ = s.Civilization;
        string id = r.Def.Id;
        if (id == "spin_yarn" && Done(civ, "FIB-11")) labor *= Fix64.FromRatio(1, 3);                      // spinning wheel x3
        if (id == "weave_cloth" && Done(civ, "FIB-10")) labor *= Fix64.Half;                               // treadle loom x2
        if (id == "weave_cloth" && Done(civ, "MEC-07") && HasMill(s))
            labor *= MillPoweredLaborFactor(s, MillPowerJob.Craft, _civic.MillLabor);                        // a fulling mill shares the craft budget
        if (FurnaceActive(s, r)) labor *= FurnaceInputAndLaborFactor(s);
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
    internal MillPowerLedgerSnapshot FixtureMillPowerLedger(int settlementIndex, int season = 0,
        int extraWaterSources = 0, int extraWindSources = 0)
    {
        if (extraWaterSources < 0) throw new ArgumentOutOfRangeException(nameof(extraWaterSources));
        if (extraWindSources < 0) throw new ArgumentOutOfRangeException(nameof(extraWindSources));
        SettlementState s = _settlements[settlementIndex];
        MillPowerAllocation p = MillPower(s, extraWaterSources, extraWindSources);
        return new MillPowerLedgerSnapshot(s.Id, s.Civilization, season, Tier(s.Id), p.Population,
            p.WaterSources, p.WindSources, p.Available, p.GrindingDemand, p.GrindingUsed,
            p.SawingDemand, p.SawingUsed, p.CraftDemand, p.CraftUsed, p.Unused,
            p.GrainMilledPeople, p.GrainCoverage);
    }
    internal bool FixtureIronGateOpen(int settlementIndex, string kind, bool includeAppetite = true)
    {
        for (int k = 0; k < (int)IronStructure.Count; k++)
            if (IronWorkId(k) == kind) return IronGate(_settlements[settlementIndex], (IronStructure)k, includeAppetite).Open;
        throw new ArgumentException($"No Iron structure '{kind}'.", nameof(kind));
    }
}
