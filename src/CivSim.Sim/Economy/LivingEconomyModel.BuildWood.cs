using System;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>Timber for Iron construction (docs/MVP/balance/m12-wp7-build-wood.md): the wider-reach felling that lets a town whose own range is cut out still get wood, and read-only diagnostics.</summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>Off, a town fells wood only within its own range (radius 3). Diagnostics and tests only; on by default in the Iron chapter.</summary>
    internal bool IronChopReachEnabled { get; set; } = true;

    /// <summary>The wider felling reach, in tiles: the same 8 a town already quarries stone over (docs/MVP/28 §9, <c>TakeReachStone</c>).</summary>
    private const int WoodReachTiles = 8;

    /// <summary>
    /// Fells up to <paramref name="amount"/> litres from the forest tiles beyond a town's own range and within <see cref="WoodReachTiles"/>, in proportion to each
    /// stand's standing stock (so no stand is stripped before its neighbours), never from a rival civilization's tile and never below the 5 L a tile keeps to stay
    /// forest (<see cref="StandingWoodAvailableToIron"/>). The wood moves from the forest ledger to the town's store at the full amount, so the forest and local-stock
    /// ledgers still close exactly; what a longer carry would cost is not modeled (the labor spent chopping is the same). Returns the litres felled.
    /// </summary>
    private Fix64 HarvestReachWood(SettlementState s, Fix64 amount)
    {
        if (amount <= Fix64.Zero) return Fix64.Zero;
        ReadOnlySpan<int> ring = ReachTiles(s.SiteTile, WoodReachTiles);
        Fix64 standing = Fix64.Zero;
        foreach (int tile in ring)
        {
            if (HexGrid.Distance(s.SiteTile, tile, _world.Width) <= Radius || !IsForestTile(tile) || IsRivalTile(s.Civilization, tile)) continue;
            standing += StandingWoodAvailableToIron(tile);
        }
        if (standing <= Fix64.Zero) return Fix64.Zero;
        Fix64 share = Fix64.Min(amount, standing) / standing, taken = Fix64.Zero;
        foreach (int tile in ring)
        {
            if (HexGrid.Distance(s.SiteTile, tile, _world.Width) <= Radius || !IsForestTile(tile) || IsRivalTile(s.Civilization, tile)) continue;
            Fix64 available = StandingWoodAvailableToIron(tile);
            Fix64 take = Fix64.Min(available, available * share);
            RecordB13StandingWoodRemoval(tile, take, "chop", _woodStock[tile]);
            _woodStock[tile] -= take;
            taken += take;
        }
        return taken;
    }

    /// <summary>
    /// Where a town's timber stands: its own wood store, the standing wood within its own range (radius 3), the standing wood within its Iron reach, and the wood
    /// held in sister towns' stores within sharing reach. Used by the harness to tell a town with no forest from a town the forest cannot reach.
    /// </summary>
    internal (Fix64 Store, Fix64 InRange, Fix64 InReach, Fix64 SisterStores) IronWoodContext(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        Fix64 sister = Fix64.Zero;
        int reach = Math.Max(0, ShareReach(s.Civilization));
        for (int d = 0; d < _settlements.Count; d++)
        {
            SettlementState other = _settlements[d];
            if (d == settlementIndex || other.Civilization != s.Civilization || IsFreeFolk(other)) continue;
            if (HexGrid.Distance(other.SiteTile, s.SiteTile, _world.Width) <= reach) sister += other.Stores[(int)Good.Wood];
        }
        return (s.Stores[(int)Good.Wood], StandingWoodInRange(s.SiteTile), IronForestWoodInReach(s), sister);
    }

    /// <summary>
    /// Where a town's stone stands: the quarry stone within its own range (radius 3) and within the wider quarry reach (8), and the blocks and ashlar held in sister
    /// towns' stores within sharing reach. Read-only, for the harness.
    /// </summary>
    internal (Fix64 InRange, Fix64 InReach, Fix64 SisterBlocks, Fix64 SisterAshlar) IronStoneContext(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        Fix64 inRange = Fix64.Zero, inReach = Fix64.Zero;
        foreach (int tile in RangeTiles(s.SiteTile)) inRange += _stoneStock[tile];
        foreach (int tile in ReachTiles(s.SiteTile, 8)) if (!IsRivalTile(s.Civilization, tile)) inReach += _stoneStock[tile];
        Fix64 blocks = Fix64.Zero, ashlar = Fix64.Zero;
        int reach = Math.Max(0, ShareReach(s.Civilization));
        for (int d = 0; d < _settlements.Count; d++)
        {
            SettlementState other = _settlements[d];
            if (d == settlementIndex || other.Civilization != s.Civilization || IsFreeFolk(other) || other.Metal is null) continue;
            if (HexGrid.Distance(other.SiteTile, s.SiteTile, _world.Width) > reach) continue;
            blocks += other.Metal.Goods[(int)MetalGood.Block];
            ashlar += other.Metal.Goods[(int)MetalGood.Ashlar];
        }
        return (inRange, inReach, blocks, ashlar);
    }

    /// <summary>Off, a town builds only from the blocks and ashlar it holds itself. Diagnostics and tests only; on by default in the Iron chapter.</summary>
    internal bool IronBuildStoneSharingEnabled { get; set; } = true;

    /// <summary>
    /// Sister towns lend the stone an Iron structure under construction lacks (docs/MVP/balance/m12-wp7-build-wood.md). In long Iron matches the towns stuck
    /// on "block" had no quarry stone left within 8 tiles while sister towns held 40 to 130 litres of dressed ashlar they were not using. A donor gives only what its own
    /// structure under construction does not need: first ashlar for ashlar, then blocks for blocks, then spare ashlar for any block still short (dressed stone is
    /// as good as a block, only dearer). Physical stone moves from one store to another, so none is created or lost; it is the same kind of transfer as public grain
    /// and Iron kits, within the same sharing reach (SOC-07 / GOV-04), and it runs only in the Iron chapter. Returns the litres moved.
    /// </summary>
    private Fix64 ShareBuildStone(int civ, int reach)
    {
        if (!IronChain || !IronBuildStoneSharingEnabled) return Fix64.Zero;
        Fix64 moved = Fix64.Zero;
        for (int r = 0; r < _settlements.Count; r++)
        {
            SettlementState receiver = _settlements[r];
            if (receiver.Civilization != civ || IsFreeFolk(receiver) || Population(receiver.Id) == 0 || receiver.Metal is not { IronBuildKind: >= 0 } rm) continue;
            IronSpec spec = IronSpecs[rm.IronBuildKind];
            Fix64 needAshlar = Fix64.Max(Fix64.Zero, Fix64.FromInt(spec.Ashlar) - rm.Goods[(int)MetalGood.Ashlar]);
            Fix64 needBlock = Fix64.Max(Fix64.Zero, Fix64.FromInt(spec.Block) - rm.Goods[(int)MetalGood.Block]);
            if (needAshlar <= Epsilon && needBlock <= Epsilon) continue;
            for (int pass = 0; pass < 3; pass++)
            {
                if ((pass == 0 && needAshlar <= Epsilon) || (pass >= 1 && needBlock <= Epsilon)) continue;
                MetalGood donated = pass == 1 ? MetalGood.Block : MetalGood.Ashlar; // passes 0 and 2 draw on ashlar, pass 1 on blocks
                for (int d = 0; d < _settlements.Count; d++)
                {
                    if ((pass == 0 ? needAshlar : needBlock) <= Epsilon) break;
                    SettlementState donor = _settlements[d];
                    if (d == r || donor.Civilization != civ || IsFreeFolk(donor) || donor.Metal is not { } dm ||
                        HexGrid.Distance(donor.SiteTile, receiver.SiteTile, _world.Width) > reach) continue;
                    Fix64 own = Fix64.Zero;
                    if (dm.IronBuildKind >= 0)
                    {
                        IronSpec ds = IronSpecs[dm.IronBuildKind];
                        own = donated == MetalGood.Ashlar ? Fix64.FromInt(ds.Ashlar) : Fix64.FromInt(ds.Block);
                    }
                    Fix64 surplus = dm.Goods[(int)donated] - own;
                    if (surplus <= Epsilon) continue;
                    Fix64 take = Fix64.Min(pass == 0 ? needAshlar : needBlock, surplus);
                    dm.Goods[(int)donated] -= take;
                    rm.Goods[(int)(pass == 0 ? MetalGood.Ashlar : MetalGood.Block)] += take;
                    RecordSupplyTransfer(donor.Id, receiver.Id, MetalGoodId(donated), take);
                    if (pass == 0) needAshlar -= take; else needBlock -= take;
                    moved += take;
                }
            }
        }
        return moved;
    }

    // ---- fixtures (tests only) ------------------------------------------------------------------------

    internal Fix64 FixtureShareBuildStone(int civ) => ShareBuildStone(civ, Math.Max(0, ShareReach(civ)));

    /// <summary>Starts an Iron structure (by its registry work id) on a town with a given share of its labor already done, as the planner would have selected it.</summary>
    internal void FixtureSetIronBuild(int settlementIndex, string kind, Fix64 progress)
    {
        SettlementState s = _settlements[settlementIndex];
        MetalState m = EnsureMetal(s);
        for (int k = 0; k < (int)IronStructure.Count; k++)
            if (IronWorkId(k) == kind) { m.IronBuildKind = k; m.IronBuildProgress = progress; return; }
        throw new ArgumentException($"No Iron structure '{kind}'.", nameof(kind));
    }

    internal Fix64 FixtureBlockStock(int settlementIndex, bool ashlar)
        => _settlements[settlementIndex].Metal?.Goods[(int)(ashlar ? MetalGood.Ashlar : MetalGood.Block)] ?? Fix64.Zero;

    /// <summary>A forest tile beyond a town's own range (more than 3) and within the 8-tile felling reach, or -1 when its reach holds none.</summary>
    internal int FixtureForestRingTile(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        foreach (int tile in ReachTiles(s.SiteTile, WoodReachTiles))
            if (HexGrid.Distance(s.SiteTile, tile, _world.Width) > Radius && IsForestTile(tile) && !IsRivalTile(s.Civilization, tile)) return tile;
        return -1;
    }

    /// <summary>Empties the wood in a town's own range, so the only standing wood it can reach lies in the ring.</summary>
    internal void FixtureClearOwnRangeWood(int settlementIndex)
    {
        foreach (int tile in RangeTiles(_settlements[settlementIndex].SiteTile)) _woodStock[tile] = Fix64.Zero;
        InvalidateIronFuelHubAssignments();
    }

    internal Fix64 FixtureHarvestWood(int settlementIndex, Fix64 amount) => HarvestWoodSpread(_settlements[settlementIndex], amount);
    internal Fix64 FixtureWoodStock(int tile) => _woodStock[tile];
    internal Fix64 FixtureStandingWoodTotal() { Fix64 t = Fix64.Zero; foreach (Fix64 w in _woodStock) t += w; return t; }
    internal Fix64 FixtureWoodStore(int settlementIndex) => _settlements[settlementIndex].Stores[(int)Good.Wood];
    internal Fix64 FixtureBuildWoodNeed(int settlementIndex) => IronBuildWoodProcurementNeed(_settlements[settlementIndex]);
}
