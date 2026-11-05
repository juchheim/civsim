using System;
using System.Collections.Generic;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

internal readonly record struct B13WoodSnapshot(int SettlementId, int Civilization, int Season, int Population, bool IsTown,
    Fix64 LocalWood, Fix64 StandingForestWood, Fix64 ChopInflows, Fix64 CivicInflows, Fix64 OtherInflows,
    Fix64 HearthUse, Fix64 E3FuelUse, Fix64 MetalUse, Fix64 GoodsUse, Fix64 CivicUse, Fix64 StructureUse, Fix64 OtherUse,
    Fix64 Regrowth, Fix64 ChopRemoved, Fix64 MetalRemoved, Fix64 CivicRemoved, Fix64 OtherRemoved,
    Fix64 ClearThresholdRemoval, int ClearThresholdCrossings);

internal readonly record struct B13TileWoodSnapshot(int Tile, Fix64 Initial, Fix64 Standing, Fix64 Regrowth,
    Fix64 ChopRemoved, Fix64 MetalRemoved, Fix64 CivicRemoved, Fix64 OtherRemoved,
    Fix64 ClearThresholdRemoval, int ClearThresholdCrossings,
    Fix64 ChopClearThresholdRemoval, Fix64 MetalClearThresholdRemoval, Fix64 CivicClearThresholdRemoval, Fix64 OtherClearThresholdRemoval,
    int ChopClearThresholdCrossings, int MetalClearThresholdCrossings, int CivicClearThresholdCrossings, int OtherClearThresholdCrossings,
    Fix64 IronRemoved, Fix64 IronClearThresholdRemoval, int IronClearThresholdCrossings,
    int ForestState, int[] OverlappingSettlements);

internal readonly record struct B13OuterWoodSnapshot(int SettlementId, int Civilization, int Season, int Tile,
    Fix64 Standing, int Distance, bool IsForest, int[] Competitors);

public sealed partial class LivingEconomyModel
{
    private sealed class B13LocalWoodFlow
    {
        public Fix64 ChopInflows, CivicInflows, OtherInflows;
        public Fix64 HearthUse, E3FuelUse, MetalUse, GoodsUse, CivicUse, StructureUse, OtherUse;
    }

    private sealed class B13TileWoodFlow
    {
        public Fix64 Regrowth, ChopRemoved, MetalRemoved, CivicRemoved, OtherRemoved, IronRemoved, ClearThresholdRemoval;
        public Fix64 ChopClearThresholdRemoval, MetalClearThresholdRemoval, CivicClearThresholdRemoval, OtherClearThresholdRemoval;
        public Fix64 IronClearThresholdRemoval;
        public int ClearThresholdCrossings, ChopClearThresholdCrossings, MetalClearThresholdCrossings, CivicClearThresholdCrossings, OtherClearThresholdCrossings;
        public int IronClearThresholdCrossings;
    }

    private SortedList<int, B13LocalWoodFlow>? _b13LocalWoodFlows;
    private B13TileWoodFlow[]? _b13TileWoodFlows;
    internal bool B13WoodTraceEnabled { get; set; }

    internal IReadOnlyList<B13WoodSnapshot> CaptureB13WoodSnapshots(int season)
    {
        if (!B13WoodTraceEnabled) return Array.Empty<B13WoodSnapshot>();
        _b13TileWoodFlows ??= new B13TileWoodFlow[_woodStock.Length];
        var snapshots = new List<B13WoodSnapshot>(_settlements.Count);
        foreach (SettlementState s in _settlements)
        {
            B13LocalWoodFlow local = new();
            if (_b13LocalWoodFlows is not null && _b13LocalWoodFlows.TryGetValue(s.Id, out B13LocalWoodFlow? recorded))
                local = recorded;
            Fix64 standing = Fix64.Zero, regrowth = Fix64.Zero, chopRemoved = Fix64.Zero, metalRemoved = Fix64.Zero;
            Fix64 civicRemoved = Fix64.Zero, otherRemoved = Fix64.Zero, clearRemoved = Fix64.Zero;
            int clearCrossings = 0;
            foreach (int tile in RangeTiles(s.SiteTile))
            {
                if (_world.Tile(tile).InitialWood < Fix64.FromInt(15)) continue;
                standing += _woodStock[tile];
                B13TileWoodFlow? f = _b13TileWoodFlows[tile];
                if (f is null) continue;
                regrowth += f.Regrowth;
                chopRemoved += f.ChopRemoved;
                metalRemoved += f.MetalRemoved;
                civicRemoved += f.CivicRemoved;
                otherRemoved += f.OtherRemoved;
                clearRemoved += f.ClearThresholdRemoval;
                clearCrossings += f.ClearThresholdCrossings;
            }
            snapshots.Add(new B13WoodSnapshot(s.Id, s.Civilization, season, Population(s.Id), Tier(s.Id) == SettlementTier.Town,
                s.Stores[(int)Good.Wood], standing, local.ChopInflows, local.CivicInflows, local.OtherInflows,
                local.HearthUse, local.E3FuelUse, local.MetalUse, local.GoodsUse, local.CivicUse, local.StructureUse, local.OtherUse,
                regrowth, chopRemoved, metalRemoved, civicRemoved, otherRemoved, clearRemoved, clearCrossings));
        }
        return snapshots;
    }

    internal IReadOnlyList<B13TileWoodSnapshot> CaptureB13TileWoodSnapshots(int civilization)
    {
        if (!B13WoodTraceEnabled) return Array.Empty<B13TileWoodSnapshot>();
        _b13TileWoodFlows ??= new B13TileWoodFlow[_woodStock.Length];
        var tiles = new List<B13TileWoodSnapshot>();
        for (int tile = 0; tile < _woodStock.Length; tile++)
        {
            Fix64 initial = _world.Tile(tile).InitialWood;
            if (initial < Fix64.FromInt(15)) continue;
            var overlaps = new List<int>();
            foreach (SettlementState s in _settlements)
                if (s.Civilization == civilization && Population(s.Id) is >= 80 and <= 100 && Tier(s.Id) == SettlementTier.Town &&
                    Done(s.Civilization, "WOD-04") && HexGrid.Distance(s.SiteTile, tile, _world.Width) <= Radius)
                    overlaps.Add(s.Id);
            B13TileWoodFlow? f = _b13TileWoodFlows[tile];
            tiles.Add(new B13TileWoodSnapshot(tile, initial, _woodStock[tile], f?.Regrowth ?? Fix64.Zero,
                f?.ChopRemoved ?? Fix64.Zero, f?.MetalRemoved ?? Fix64.Zero, f?.CivicRemoved ?? Fix64.Zero,
                f?.OtherRemoved ?? Fix64.Zero, f?.ClearThresholdRemoval ?? Fix64.Zero,
                f?.ClearThresholdCrossings ?? 0,
                f?.ChopClearThresholdRemoval ?? Fix64.Zero, f?.MetalClearThresholdRemoval ?? Fix64.Zero,
                f?.CivicClearThresholdRemoval ?? Fix64.Zero, f?.OtherClearThresholdRemoval ?? Fix64.Zero,
                f?.ChopClearThresholdCrossings ?? 0, f?.MetalClearThresholdCrossings ?? 0,
                f?.CivicClearThresholdCrossings ?? 0, f?.OtherClearThresholdCrossings ?? 0,
                f?.IronRemoved ?? Fix64.Zero, f?.IronClearThresholdRemoval ?? Fix64.Zero, f?.IronClearThresholdCrossings ?? 0,
                _forestState[tile], overlaps.ToArray()));
        }
        return tiles;
    }

    internal IReadOnlyList<B13OuterWoodSnapshot> CaptureB13OuterWoodSnapshots(int civilization, int season, int reach = 8)
    {
        if (!B13WoodTraceEnabled) return Array.Empty<B13OuterWoodSnapshot>();
        var snapshots = new List<B13OuterWoodSnapshot>();
        foreach (SettlementState target in _settlements)
        {
            if (target.Civilization != civilization || Population(target.Id) <= 0) continue;
            var reachable = new SortedSet<int>();
            foreach (int tile in ReachTiles(target.SiteTile, reach))
            {
                int distance = HexGrid.Distance(target.SiteTile, tile, _world.Width);
                if (distance <= Radius || IsRivalTile(civilization, tile)) continue;
                if (_world.Tile(tile).InitialWood <= Fix64.Zero && _woodStock[tile] <= Fix64.Zero) continue;
                reachable.Add(tile);
            }

            foreach (int tile in reachable)
            {
                int distance = HexGrid.Distance(target.SiteTile, tile, _world.Width);
                var competitors = new List<int>();
                foreach (SettlementState other in _settlements)
                {
                    if (other.Id == target.Id || Population(other.Id) <= 0) continue;
                    int otherReach = Done(other.Civilization, "TRN-05") ? _bronzeConstants.CharcoalReachCarts : _bronzeConstants.CharcoalReach;
                    if (HexGrid.Distance(other.SiteTile, tile, _world.Width) > otherReach || IsRivalTile(other.Civilization, tile)) continue;
                    competitors.Add(other.Id);
                }
                snapshots.Add(new B13OuterWoodSnapshot(target.Id, civilization, season, tile, _woodStock[tile], distance,
                    IsForestTile(tile), competitors.ToArray()));
            }
        }
        return snapshots;
    }

    private void RecordB13LocalWoodUse(SettlementState s, Fix64 amount, string category)
    {
        if (!B13WoodTraceEnabled || amount <= Fix64.Zero) return;
        B13LocalWoodFlow flow = LocalFlow(s.Id);
        switch (category)
        {
            case "fire": flow.HearthUse += amount; break;
            case "fuel": flow.E3FuelUse += amount; break;
            case "metal": flow.MetalUse += amount; break;
            case "goods": flow.GoodsUse += amount; break;
            case "civic": flow.CivicUse += amount; break;
            case "structure": flow.StructureUse += amount; break;
            default: flow.OtherUse += amount; break;
        }
    }

    private void RecordB13LocalWoodInflow(SettlementState s, Fix64 amount, bool chop, bool civic)
    {
        if (!B13WoodTraceEnabled || amount <= Fix64.Zero) return;
        B13LocalWoodFlow flow = LocalFlow(s.Id);
        if (chop) flow.ChopInflows += amount;
        else if (civic) flow.CivicInflows += amount;
        else flow.OtherInflows += amount;
    }

    private B13LocalWoodFlow LocalFlow(int settlementId)
    {
        _b13LocalWoodFlows ??= new SortedList<int, B13LocalWoodFlow>();
        if (!_b13LocalWoodFlows.TryGetValue(settlementId, out B13LocalWoodFlow? flow))
            _b13LocalWoodFlows.Add(settlementId, flow = new B13LocalWoodFlow());
        return flow;
    }

    private string B13MetalWoodCause(SettlementState settlement)
        => IronChain && Done(settlement.Civilization, "MET-09") ? "iron" : "metal";

    private void RecordB13WoodRegrowth(int tile, Fix64 amount)
    {
        if (!B13WoodTraceEnabled || amount <= Fix64.Zero || !IsForestTile(tile)) return;
        _b13TileWoodFlows ??= new B13TileWoodFlow[_woodStock.Length];
        (_b13TileWoodFlows[tile] ??= new B13TileWoodFlow()).Regrowth += amount;
    }

    private void RecordB13StandingWoodRemoval(int tile, Fix64 amount, string category, Fix64 before)
    {
        if (!B13WoodTraceEnabled || amount <= Fix64.Zero || !IsForestTile(tile)) return;
        _b13TileWoodFlows ??= new B13TileWoodFlow[_woodStock.Length];
        B13TileWoodFlow flow = _b13TileWoodFlows[tile] ??= new B13TileWoodFlow();
        switch (category)
        {
            case "chop": flow.ChopRemoved += amount; break;
            case "iron": flow.MetalRemoved += amount; flow.IronRemoved += amount; break;
            case "metal": flow.MetalRemoved += amount; break;
            case "civic": flow.CivicRemoved += amount; break;
            default: flow.OtherRemoved += amount; break;
        }
        Fix64 after = Fix64.Max(Fix64.Zero, before - amount);
        if (before >= _bronzeConstants.ClearedBelow && after < _bronzeConstants.ClearedBelow)
        {
            flow.ClearThresholdCrossings++;
            flow.ClearThresholdRemoval += amount;
            switch (category)
            {
                case "chop": flow.ChopClearThresholdCrossings++; flow.ChopClearThresholdRemoval += amount; break;
                case "iron": flow.MetalClearThresholdCrossings++; flow.MetalClearThresholdRemoval += amount; flow.IronClearThresholdCrossings++; flow.IronClearThresholdRemoval += amount; break;
                case "metal": flow.MetalClearThresholdCrossings++; flow.MetalClearThresholdRemoval += amount; break;
                case "civic": flow.CivicClearThresholdCrossings++; flow.CivicClearThresholdRemoval += amount; break;
                default: flow.OtherClearThresholdCrossings++; flow.OtherClearThresholdRemoval += amount; break;
            }
        }
    }
}
