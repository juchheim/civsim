using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>Fief-only runtime demand for COM-09/10 paper and printed books (WP-E5-6).</summary>
public sealed partial class LivingEconomyModel
{
    private void FiefCommunicationWants(SettlementState settlement, MetalPlan plan)
    {
        if (!FiefChain || !Done(settlement.Civilization, "COM-10") ||
            IronWorksCount(settlement, IronStructure.PrintShop) == 0 ||
            RoleHeads(settlement.Id).Scribes == 0 ||
            Stock(settlement, FiefSlot(FiefGood.Book)) > Fix64.Zero) return;

        // Keep one book in the staffed shop's local stock. Recipe expansion supplies paper from cloth as needed.
        plan.Terminal[FiefSlot(FiefGood.Book)] = Fix64.One;
    }
}
