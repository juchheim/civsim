using CivSim.Sim.Economy;
using CivSim.Sim.Politics;

namespace CivSim.Sim.View;

public sealed partial class FrameBuilder
{
    private void FillSupplyTransfers(LivingEconomyModel economy, PoliticsSystem politics, MatchFrame target)
    {
        target.SupplyTransferCount = 0;

        foreach (SettlementSupplyTransfer transfer in economy.SupplyTransfers)
        {
            if (!SupplyEndpointsKnown(transfer.FromSettlement, transfer.ToSettlement, target)) continue;
            Add(transfer.FromSettlement, transfer.ToSettlement, 0, transfer.GoodId, transfer.Quantity.Raw);
        }
        foreach (PoliticsSupplyTransfer transfer in politics.TributeTransfers)
        {
            if (!SupplyEndpointsKnown(transfer.FromSettlement, transfer.ToSettlement, target)) continue;
            Add(transfer.FromSettlement, transfer.ToSettlement, 1, transfer.GoodId, transfer.Quantity.Raw);
        }

        void Add(int from, int to, byte kind, string good, long amount)
        {
            target.EnsureSupplyTransfers(target.SupplyTransferCount + 1);
            int at = target.SupplyTransferCount++;
            target.SupplyTransferFrom[at] = from;
            target.SupplyTransferTo[at] = to;
            target.SupplyTransferKind[at] = kind;
            target.SupplyTransferGood[at] = good;
            target.SupplyTransferAmount[at] = amount;
        }
    }

    private bool SupplyEndpointsKnown(int from, int to, MatchFrame target)
    {
        if ((uint)from >= (uint)_settlementCount || (uint)to >= (uint)_settlementCount || from == to ||
            target.IndexOfSettlement(from) < 0 || target.IndexOfSettlement(to) < 0) return false;
        if (_omniscient) return true;
        bool ownFrom = _sCiv[from] == _viewer && !_sFree[from];
        bool ownTo = _sCiv[to] == _viewer && !_sFree[to];
        return (ownFrom || Visible(_sTile[from])) && (ownTo || Visible(_sTile[to]));
    }
}
