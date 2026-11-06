using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// Read-only goods-gained totals for the era report: for each civilization and store slot, the sum over seasons of how much the
/// stock across its settlements grew. This is net growth of the stores (what was left after eating, burning and trading),
/// not gross output. Nothing here is hashed, snapshotted or read by a decision, so the golden checksums and replays do not depend on it.
/// </summary>
public sealed partial class LivingEconomyModel
{
    private Fix64[]? _goodsGained;

    /// <summary>Called once a season, after the season's economy has resolved.</summary>
    private void TrackGoodsGained()
    {
        int slots = SlotCount;
        _goodsGained ??= new Fix64[CivilizationCount * slots];
        foreach (SettlementState s in _settlements)
        {
            if ((uint)s.Civilization >= (uint)CivilizationCount) continue;
            // A settlement's first reading is its baseline: stock it was founded with came from its parent, not from work.
            bool fresh = s.GainBaseline is null;
            Fix64[] baseline = s.GainBaseline ??= new Fix64[slots];
            for (int slot = 0; slot < slots; slot++)
            {
                Fix64 now = Stock(s, slot);
                Fix64 delta = now - baseline[slot];
                if (!fresh && delta > Fix64.Zero) _goodsGained[s.Civilization * slots + slot] += delta;
                baseline[slot] = now;
            }
        }
    }

    /// <summary>Net growth of a good in a civilization's stores since the match began (see the class remarks).</summary>
    public Fix64 GoodsGained(int civ, int slot)
        => _goodsGained is null || (uint)civ >= (uint)CivilizationCount || (uint)slot >= (uint)SlotCount
            ? Fix64.Zero
            : _goodsGained[civ * SlotCount + slot];
}
