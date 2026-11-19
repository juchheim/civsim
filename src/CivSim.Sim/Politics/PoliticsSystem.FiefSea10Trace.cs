using System;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Politics;

/// <summary>Opt-in, report-only SEA-10 route observations. This observer is deliberately outside serialized/hash state.</summary>
public sealed partial class PoliticsSystem
{
    public Action<string>? FiefSea10TraceObserverForDiagnostics { get; set; }

    internal void FixtureTraceSea10Milestones() => TraceSea10Milestones();

    private void TraceSea10Milestones()
    {
        Action<string>? emit = FiefSea10TraceObserverForDiagnostics;
        if (emit is null || !_economy.FiefChain) return;
        string[] ids = { "KEY-5", "KEY-6", "SEA-04", "SEA-10" };
        foreach (string id in ids)
        {
            int project = _catalog.IndexOf(id);
            for (int civ = 0; civ < _civs; civ++)
            {
                bool done = project >= 0 && _research.IsDone(civ, project);
                long tick = done ? _research.CompletedTick(civ, project) : -1;
                emit($"sea10-trace kind=milestone seed={_seed} season={_season} civ={civ} project={id} complete={done.ToString().ToLowerInvariant()} completedSeason={(tick < 0 ? -1 : tick / LivingEconomyModel.SeasonTicks)} canSail={CanSail(civ).ToString().ToLowerInvariant()}");
            }
        }
    }

    private void TraceSea10Route(string kind, Compact? compact, Expedition? shipment, int seller, int buyer,
        string reason, Fix64 requested, Fix64 capped, Fix64 taken, Fix64 delivered = default,
        Fix64 paid = default, Fix64 returned = default, Fix64 lost = default)
    {
        Action<string>? emit = FiefSea10TraceObserverForDiagnostics;
        if (emit is null || !_economy.FiefChain) return;
        int mover = compact is null ? shipment?.Civ ?? -1 : compact.SellerCiv >= 0 ? compact.SellerCiv : compact.BuyerCiv;
        int from = (uint)seller < (uint)_sCount ? _sTile[seller] : -1;
        int to = (uint)buyer < (uint)_sCount ? _sTile[buyer] : -1;
        int walk = from >= 0 && to >= 0 ? WalkFrom(from)[to] : int.MaxValue;
        int physicalWater = from >= 0 && to >= 0 && walk == int.MaxValue ? PhysicalSeaTilesForDiagnostics(from, to) : 0;
        int eligibleWater = from >= 0 && to >= 0 ? SeaTilesOnRoute(mover, from, to) : -1;
        string route = walk != int.MaxValue ? "land" : physicalWater > 0 && eligibleWater < 0 ? "water_locked" :
            physicalWater > 0 && eligibleWater > 0 ? "water" : "unreachable";
        int key6 = _catalog.IndexOf("KEY-6"), sea10 = _catalog.IndexOf("SEA-10"), sea04 = _catalog.IndexOf("SEA-04");
        bool k6 = key6 >= 0 && mover >= 0 && _research.IsDone(mover, key6);
        bool s10 = sea10 >= 0 && mover >= 0 && _research.IsDone(mover, sea10);
        bool s04 = sea04 >= 0 && mover >= 0 && _research.IsDone(mover, sea04);
        emit($"sea10-trace kind={kind} seed={_seed} season={_season} shipment={(shipment?.Id ?? -1)} compact={(compact?.Id ?? shipment?.CompactId ?? -1)} priorDeliveries={compact?.Deliveries ?? 0} sellerCiv={(compact?.SellerCiv ?? -1)} buyerCiv={(compact?.BuyerCiv ?? -1)} seller={seller} buyer={buyer} good={(compact?.Good ?? shipment?.Good ?? TradeGood.None)} route={route} physicalWaterTiles={physicalWater} eligibleWaterTiles={eligibleWater} SEA04={s04.ToString().ToLowerInvariant()} KEY6={k6.ToString().ToLowerInvariant()} SEA10={s10.ToString().ToLowerInvariant()} reason={reason} requested={requested.Raw} capped={capped.Raw} taken={taken.Raw} carried={(shipment?.Cargo ?? Fix64.Zero).Raw} delivered={delivered.Raw} paid={paid.Raw} returned={returned.Raw} lost={lost.Raw} arrivalSeason={(shipment?.ArriveSeason ?? -1)}");
    }

    private int PhysicalSeaTilesForDiagnostics(int from, int to)
    {
        int[] costs = SailFrom(from);
        if (costs[to] == int.MaxValue) return 0;
        Span<int> neighbors = stackalloc int[6];
        int at = to, sea = 0;
        for (int guard = 0; guard < 4096 && at != from; guard++)
        {
            if (!_tileArray[at].IsLand && !_passable[at]) sea++;
            int count = CivSim.Sim.World.HexGrid.Neighbors(at, _width, _height, neighbors);
            int next = -1, best = costs[at];
            for (int i = 0; i < count; i++) if (costs[neighbors[i]] < best) { best = costs[neighbors[i]]; next = neighbors[i]; }
            if (next < 0) return 0;
            at = next;
        }
        return sea;
    }
}
