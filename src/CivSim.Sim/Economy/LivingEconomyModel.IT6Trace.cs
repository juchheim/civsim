using System;
using System.Collections.Generic;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>Read-only whole-match I-T6 counters; diagnostic only and deliberately excluded from simulation state.</summary>
internal readonly record struct IT6AccountingSnapshot(
    Fix64 Kit11Produced, Fix64 Kit12Produced, Fix64 Kit11Issued, Fix64 Kit12Issued,
    Fix64 Kit11ToolWork, Fix64 Kit12ToolWork, int Kit11Breaks, int Kit12Breaks, int Kit11Upgrades,
    Fix64 KitRecipeLabor, Fix64 MetalFacilityBuildLabor, Fix64 IronRecipeCharcoal,
    Fix64 IronOreMined, Fix64 IronFireSettingWood, Fix64 CharcoalHaulLabor, Fix64 OreHaulLabor, Fix64 IronOreHaulLabor,
    int MetalFuelShortSeasons, int IronEraFuelShortSeasons,
    int IronEraFuelDemandSeasons, Fix64 IronEraCharcoalPlan, Fix64 IronEraCharcoalUnmet);

internal readonly record struct IT6FuelSiteSnapshot(
    int SettlementId, int Civilization, int Population, int DemandSeasons, int ShortSeasons,
    Fix64 Planned, Fix64 Unmet, Fix64 ShortfallReachWood, Fix64 ShortfallHarvestableWood,
    Fix64 ShortfallInRangeWood, Fix64 ShortfallOuterReachWood, Fix64 ShortfallLocalWood,
    Fix64 ShortfallRemainingWork, Fix64 ShortfallWoodFactor, Fix64 ForestBaseline,
    Fix64 ReachWoodNow, Fix64 HarvestableWoodNow, int HarvestableForestTiles,
    Fix64 BeyondReachRawWood, Fix64 BeyondReachHarvestableWood, int BeyondReachForestTiles, int NearestBeyondReachForestDistance,
    Fix64 LocalWoodNow, Fix64 CharcoalStock, int OwnSettlementsInReach, int OtherSettlementsInReach);

public sealed partial class LivingEconomyModel
{
    private sealed class IT6CivFlow
    {
        public Fix64 Kit11Produced, Kit12Produced, Kit11Issued, Kit12Issued;
        public Fix64 Kit11ToolWork, Kit12ToolWork;
        public int Kit11Breaks, Kit12Breaks, Kit11Upgrades;
        public Fix64 KitRecipeLabor, MetalFacilityBuildLabor, IronRecipeCharcoal;
        public Fix64 IronOreMined, IronFireSettingWood, CharcoalHaulLabor, OreHaulLabor, IronOreHaulLabor;
        public int MetalFuelShortSeasons, IronEraFuelShortSeasons, IronEraFuelDemandSeasons;
        public Fix64 IronEraCharcoalPlan, IronEraCharcoalUnmet;
    }

    private sealed class IT6FuelSiteFlow
    {
        public int DemandSeasons, ShortSeasons;
        public Fix64 Planned, Unmet, ShortfallReachWood, ShortfallHarvestableWood, ShortfallInRangeWood,
            ShortfallOuterReachWood, ShortfallLocalWood, ShortfallRemainingWork, ShortfallWoodFactor;
    }

    private IT6CivFlow[]? _it6CivFlows;
    private Dictionary<int, IT6FuelSiteFlow>? _it6FuelSiteFlows;
    internal bool IT6AccountingTraceEnabled { get; set; }

    internal IT6AccountingSnapshot CaptureIT6Accounting(int civilization)
    {
        if (!IT6AccountingTraceEnabled || (uint)civilization >= (uint)_completedProjects.Length ||
            _it6CivFlows is null || _it6CivFlows[civilization] is not { } f)
            return default;
        return new IT6AccountingSnapshot(f.Kit11Produced, f.Kit12Produced, f.Kit11Issued, f.Kit12Issued,
            f.Kit11ToolWork, f.Kit12ToolWork, f.Kit11Breaks, f.Kit12Breaks, f.Kit11Upgrades,
            f.KitRecipeLabor, f.MetalFacilityBuildLabor, f.IronRecipeCharcoal, f.IronOreMined,
            f.IronFireSettingWood, f.CharcoalHaulLabor, f.OreHaulLabor, f.IronOreHaulLabor,
            f.MetalFuelShortSeasons, f.IronEraFuelShortSeasons,
            f.IronEraFuelDemandSeasons, f.IronEraCharcoalPlan, f.IronEraCharcoalUnmet);
    }

    internal IReadOnlyList<IT6FuelSiteSnapshot> CaptureIT6FuelSites()
    {
        if (!IT6AccountingTraceEnabled || _it6FuelSiteFlows is null) return Array.Empty<IT6FuelSiteSnapshot>();
        var result = new List<IT6FuelSiteSnapshot>(_it6FuelSiteFlows.Count);
        foreach ((int settlementId, IT6FuelSiteFlow flow) in _it6FuelSiteFlows)
        {
            int settlementIndex = -1;
            for (int i = 0; i < _settlements.Count; i++)
                if (_settlements[i].Id == settlementId) { settlementIndex = i; break; }
            if (settlementIndex < 0) continue;
            SettlementState s = _settlements[settlementIndex];
            if (s.Metal is not { } m) continue;
            (Fix64 raw, Fix64 available, int availableTiles, _, _) = IT6ReachWood(s);
            int reach = IronFuelCharcoalReach(s);
            (Fix64 beyondRaw, Fix64 beyondAvailable, int beyondTiles, int nearestBeyond) = IT6WoodInAnnulus(s, reach, reach + 10);
            int ownInReach = 0, otherInReach = 0;
            foreach (SettlementState other in _settlements)
            {
                if (other.Id == s.Id || Population(other.Id) <= 0 ||
                    HexGrid.Distance(s.SiteTile, other.SiteTile, _world.Width) > reach) continue;
                if (other.Civilization == s.Civilization) ownInReach++;
                else otherInReach++;
            }
            result.Add(new IT6FuelSiteSnapshot(settlementId, s.Civilization, Population(s.Id), flow.DemandSeasons, flow.ShortSeasons,
                flow.Planned, flow.Unmet, flow.ShortfallReachWood, flow.ShortfallHarvestableWood,
                flow.ShortfallInRangeWood, flow.ShortfallOuterReachWood, flow.ShortfallLocalWood,
                flow.ShortfallRemainingWork, flow.ShortfallWoodFactor, m.ForestAtStart, raw, available, availableTiles,
                beyondRaw, beyondAvailable, beyondTiles, nearestBeyond,
                Fix64.Max(Fix64.Zero, s.Stores[(int)Good.Wood] - WoodReserve), m.Goods[(int)MetalGood.Charcoal],
                ownInReach, otherInReach));
        }
        return result;
    }

    private (Fix64 Raw, Fix64 Harvestable, int Tiles, Fix64 InRange, Fix64 OuterReach) IT6ReachWood(SettlementState s)
    {
        int reach = IronFuelCharcoalReach(s);
        Fix64 raw = Fix64.Zero, harvestable = Fix64.Zero, inRange = Fix64.Zero, outerReach = Fix64.Zero;
        int tiles = 0;
        foreach (int tile in ReachTiles(s.SiteTile, reach))
        {
            if (!IsForestTile(tile) || IsRivalTile(s.Civilization, tile)) continue;
            Fix64 stock = _woodStock[tile];
            raw += stock;
            Fix64 available = StandingWoodAvailableToIron(tile);
            if (available <= Fix64.Zero) continue;
            harvestable += available;
            if (HexGrid.Distance(s.SiteTile, tile, _world.Width) <= Radius) inRange += available;
            else outerReach += available;
            tiles++;
        }
        return (raw, harvestable, tiles, inRange, outerReach);
    }

    private (Fix64 Raw, Fix64 Harvestable, int Tiles, int Nearest) IT6WoodInAnnulus(SettlementState s, int innerReach, int outerReach)
    {
        Fix64 raw = Fix64.Zero, harvestable = Fix64.Zero;
        int tiles = 0, nearest = int.MaxValue;
        foreach (int tile in ReachTiles(s.SiteTile, outerReach))
        {
            int distance = HexGrid.Distance(s.SiteTile, tile, _world.Width);
            if (distance <= innerReach || !IsForestTile(tile) || IsRivalTile(s.Civilization, tile)) continue;
            raw += _woodStock[tile];
            Fix64 available = StandingWoodAvailableToIron(tile);
            if (available <= Fix64.Zero) continue;
            harvestable += available;
            tiles++;
            nearest = Math.Min(nearest, distance);
        }
        return (raw, harvestable, tiles, nearest == int.MaxValue ? -1 : nearest);
    }

    private IT6CivFlow IT6Flow(int civilization)
    {
        _it6CivFlows ??= new IT6CivFlow[_completedProjects.Length];
        return _it6CivFlows[civilization] ??= new IT6CivFlow();
    }

    private void RecordIT6Recipe(SettlementState settlement, CompiledRecipe recipe, Fix64 batches)
    {
        if (!IT6AccountingTraceEnabled || !IronChain || batches <= Fix64.Zero) return;
        IT6CivFlow flow = IT6Flow(settlement.Civilization);
        if (recipe.OutputSlot == IronSlot(IronGood.Kit11)) flow.Kit11Produced += batches * OutputQuantity(settlement, recipe);
        if (recipe.OutputSlot == IronSlot(IronGood.Kit12)) flow.Kit12Produced += batches * OutputQuantity(settlement, recipe);
        if (recipe.Def.Id is "make_tool11" or "make_tool12")
            flow.KitRecipeLabor += batches * RecipeLabor(settlement, recipe, anyMetalKit: true);

        if (recipe.Def.Id is "bloomery" or "forge_wrought" or "carburize_steel" or "make_tool11" or "make_tool12")
            for (int i = 0; i < recipe.InputSlots.Length; i++)
                if (Array.IndexOf(recipe.InputSlots[i], MetalSlot(MetalGood.Charcoal)) >= 0)
                    flow.IronRecipeCharcoal += batches * InputQuantity(settlement, recipe, i);
    }

    private void RecordIT6IronKitIssued(int civilization, int tier, int oldTier, Fix64 oldLeft)
    {
        if (!IT6AccountingTraceEnabled || !IronChain) return;
        IT6CivFlow flow = IT6Flow(civilization);
        if (tier == 11) flow.Kit11Issued += Fix64.One;
        else if (tier == 12) flow.Kit12Issued += Fix64.One;
        if (oldTier == 11 && tier == 12 && oldLeft > Fix64.Zero) flow.Kit11Upgrades++;
    }

    private void RecordIT6IronKitWork(int civilization, int tier, Fix64 work)
    {
        if (!IT6AccountingTraceEnabled || !IronChain || work <= Fix64.Zero) return;
        IT6CivFlow flow = IT6Flow(civilization);
        if (tier == 11) flow.Kit11ToolWork += work;
        else if (tier == 12) flow.Kit12ToolWork += work;
    }

    private void RecordIT6IronKitBreak(int civilization, int tier)
    {
        if (!IT6AccountingTraceEnabled || !IronChain) return;
        IT6CivFlow flow = IT6Flow(civilization);
        if (tier == 11) flow.Kit11Breaks++;
        else if (tier == 12) flow.Kit12Breaks++;
    }

    private void RecordIT6MetalFacilityBuild(int civilization, Fix64 labor)
    {
        if (!IT6AccountingTraceEnabled || !IronChain || labor <= Fix64.Zero) return;
        IT6Flow(civilization).MetalFacilityBuildLabor += labor;
    }

    private void RecordIT6IronOre(int civilization, Fix64 ore, Fix64 fireWood)
    {
        if (!IT6AccountingTraceEnabled || !IronChain || (ore <= Fix64.Zero && fireWood <= Fix64.Zero)) return;
        IT6CivFlow flow = IT6Flow(civilization);
        if (ore > Fix64.Zero) flow.IronOreMined += ore;
        flow.IronFireSettingWood += fireWood;
    }

    private void RecordIT6CharcoalHaul(int civilization, Fix64 labor)
    {
        if (!IT6AccountingTraceEnabled || !IronChain || labor <= Fix64.Zero) return;
        IT6Flow(civilization).CharcoalHaulLabor += labor;
    }

    private void RecordIT6OreHaul(int civilization, Fix64 labor, bool ironOre)
    {
        if (!IT6AccountingTraceEnabled || !IronChain || labor <= Fix64.Zero) return;
        IT6CivFlow flow = IT6Flow(civilization);
        flow.OreHaulLabor += labor;
        if (ironOre) flow.IronOreHaulLabor += labor;
    }

    private void RecordIT6FuelShort(int civilization, bool shortfall, bool ironEra)
    {
        if (!IT6AccountingTraceEnabled || !IronChain || !shortfall) return;
        IT6CivFlow flow = IT6Flow(civilization);
        flow.MetalFuelShortSeasons++;
        if (ironEra) flow.IronEraFuelShortSeasons++;
    }

    private void RecordIT6CharcoalPlan(SettlementState settlement, Fix64 planned, Fix64 unmet, bool ironEra,
        bool fuelShort, Fix64 reachableWood, Fix64 harvestableWood, Fix64 inRangeWood, Fix64 outerReachWood,
        Fix64 localWood, Fix64 remainingWork, Fix64 woodFactor)
    {
        if (!IT6AccountingTraceEnabled || !IronChain || !ironEra || planned <= Fix64.Zero) return;
        IT6CivFlow flow = IT6Flow(settlement.Civilization);
        flow.IronEraFuelDemandSeasons++;
        flow.IronEraCharcoalPlan += planned;
        flow.IronEraCharcoalUnmet += Fix64.Min(planned, Fix64.Max(Fix64.Zero, unmet));
        _it6FuelSiteFlows ??= new Dictionary<int, IT6FuelSiteFlow>();
        if (!_it6FuelSiteFlows.TryGetValue(settlement.Id, out IT6FuelSiteFlow? site))
            _it6FuelSiteFlows.Add(settlement.Id, site = new IT6FuelSiteFlow());
        site.DemandSeasons++;
        site.Planned += planned;
        site.Unmet += Fix64.Min(planned, Fix64.Max(Fix64.Zero, unmet));
        if (!fuelShort) return;
        site.ShortSeasons++;
        site.ShortfallReachWood += reachableWood;
        site.ShortfallHarvestableWood += harvestableWood;
        site.ShortfallInRangeWood += inRangeWood;
        site.ShortfallOuterReachWood += outerReachWood;
        site.ShortfallLocalWood += localWood;
        site.ShortfallRemainingWork += remainingWork;
        site.ShortfallWoodFactor += woodFactor;
    }
}
