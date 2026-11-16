using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

public sealed partial class LivingEconomyModel
{
    /// <summary>Harness-only Fief furnace bloomery-charcoal factor at full craft coverage; null preserves the exact live default.</summary>
    internal Fix64? FurnaceCharcoalFactorOverride { get; set; }

    /// <summary>Blend an opt-in candidate from x1 at zero powered coverage to its requested factor at full coverage.</summary>
    private Fix64 FurnaceCharcoalInputFactor(SettlementState settlement)
    {
        if (!FiefChain)
            return FurnaceInputAndLaborFactor(settlement);
        Fix64 candidate = FurnaceCharcoalFactorOverride ?? Fix64.One;
        if (candidate < Fix64.One || candidate > Fix64.FromInt(2))
            throw new InvalidOperationException("The Fief furnace charcoal factor must be between 1 and 2.");
        Fix64 coverage = MillPower(settlement).Coverage(MillPowerJob.Craft);
        return Fix64.One + (candidate - Fix64.One) * coverage;
    }
}
