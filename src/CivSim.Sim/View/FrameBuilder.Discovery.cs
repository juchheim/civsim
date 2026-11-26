using System;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Sim.View;

/// <summary>Builds the Discovery-only, privacy-filtered read model from existing simulation state (docs/MVP/38 §8).</summary>
public sealed partial class FrameBuilder
{
    private void FillDiscovery(MatchModel match, MatchFrame target)
    {
        DiscoveryFrame d = target.Discovery;
        LivingEconomyModel eco = match.Economy;
        d.Active = eco.DiscoveryChain;
        int n = target.SettlementCount;
        d.EnsureSettlements(n);
        Array.Clear(d.CoalLitres, 0, n); Array.Clear(d.FactoryCoverage, 0, n); Array.Clear(d.Soot, 0, n);
        Array.Clear(d.Engines, 0, n); Array.Clear(d.Ovens, 0, n); Array.Clear(d.Operatives, 0, n); Array.Clear(d.Engineers, 0, n);
        Array.Clear(d.Canal, 0, n); Array.Clear(d.Factory, 0, n); Array.Clear(d.Colony, 0, n); Array.Clear(d.GardenStaffed, 0, n);
        Array.Clear(d.Society, 0, n); Array.Clear(d.Bastion, 0, n); Array.Clear(d.GalleonCrewed, 0, n); Array.Clear(d.ExchangeHouse, 0, n); Array.Clear(d.Fleet, 0, n);
        d.OceanReach = d.ExchangeStaffed = d.JointStockOpen = d.InsuranceOpen = d.SocietyStaffed = false;
        d.ChronometerHeld = d.TelescopeHeld = false; d.EmbassyMask = 0;
        d.Colonies = 0; d.Key7Permille = 0;
        d.LaneCount = 0;
        d.CapitalTile = -1;
        if (!d.Active) return;

        // ocean lanes: the routes already in the frame that cross deep ocean (a spectator sees all of them, a player the routes it is party to)
        d.EnsureLanes(target.RouteCount);
        int lanes = 0;
        for (int r = 0; r < target.RouteCount; r++)
        {
            int civ = target.RouteSellerCiv[r], from = target.RouteFromTile[r], to = target.RouteToTile[r];
            if (civ < 0 || from < 0 || to < 0 || match.Politics.DeepSeaTilesOnRoute(civ, from, to) <= 0) continue;
            d.LaneFromTile[lanes] = from; d.LaneToTile[lanes] = to; d.LaneCiv[lanes] = (sbyte)civ; d.LaneVolume[lanes++] = target.RouteVolumePermille[r];
        }
        d.LaneCount = lanes;

        if (_viewer < 0) return; // a spectator (viewer -1) owns no civilization, so there is no realm block and no own town to read
        d.OceanReach = match.Politics.HasOceanReach(_viewer);
        d.ExchangeStaffed = eco.ExchangeStaffed(_viewer);
        d.JointStockOpen = eco.JointStockOpen(_viewer);
        d.InsuranceOpen = eco.InsuranceOpen(_viewer);
        d.SocietyStaffed = eco.SocietyStaffed(_viewer);
        d.ChronometerHeld = eco.ChronometerHeld(_viewer);
        d.TelescopeHeld = eco.TelescopeHeld(_viewer);
        int dip09 = match.Research.Catalog.IndexOf("DIP-09");
        if (dip09 >= 0 && match.Research.IsDone(_viewer, dip09))
            for (int o = 0; o < _civs && o < 31; o++)
                if (o != _viewer && match.Politics.ResidentEmbassy(_viewer, o)) d.EmbassyMask |= 1 << o;
        int key7 = match.Research.Catalog.IndexOf("KEY-7");
        if (key7 >= 0 && !match.Research.IsDone(_viewer, key7))
        {
            Fix64 cost = match.Research.EffectiveCost(_viewer, key7);
            if (cost > Fix64.Zero) d.Key7Permille = (short)Math.Clamp((match.Research.Progress(_viewer, key7) * Fix64.FromInt(1000) / cost).FloorToInt(), 0, 1000);
        }
        else if (key7 >= 0) d.Key7Permille = 1000;

        for (int w = 0; w < n; w++)
        {
            int settlement = target.SettlementIndex[w];
            SettlementViewFlags flags = (SettlementViewFlags)target.SettlementFlags[w];
            bool own = (flags & (SettlementViewFlags.Own | SettlementViewFlags.Remembered | SettlementViewFlags.FreeFolk)) == SettlementViewFlags.Own;
            if (!own || (uint)settlement >= (uint)_settlementCount) continue;
            DiscoveryTownReadout t = eco.DiscoveryTown(settlement);
            d.CoalLitres[w] = t.CoalLitres; d.FactoryCoverage[w] = t.FactoryCoverage; d.Soot[w] = t.Soot;
            d.Engines[w] = (byte)Math.Min(255, t.Engines); d.Ovens[w] = (byte)Math.Min(255, t.Ovens); d.Operatives[w] = (byte)Math.Min(255, t.Operatives); d.Engineers[w] = (byte)Math.Min(255, t.Engineers);
            d.Canal[w] = (byte)(t.Canal ? 1 : 0); d.Factory[w] = (byte)(t.Factory ? 1 : 0); d.Colony[w] = ColonyState(match, target, w, settlement, t.Colony);
            d.GardenStaffed[w] = (byte)(t.GardenStaffed ? 1 : 0); d.Society[w] = (byte)(t.SocietyHere ? 1 : 0);
            d.Bastion[w] = (byte)(t.Bastion ? 1 : 0); d.GalleonCrewed[w] = (byte)(t.GalleonCrewed ? 1 : 0); d.ExchangeHouse[w] = (byte)(t.ExchangeHere ? 1 : 0); d.Fleet[w] = (byte)(t.Fleet ? 1 : 0);
            if (t.Colony) d.Colonies++;
        }
        d.CapitalTile = eco.CapitalTile(_viewer);
    }

    /// <summary>A colony's state for the view (docs/MVP/39 §7.3): 0 not a colony, 1 content, 2 strained (its Cohesion is strained or fraying), 3 blockaded (its harbor is cut off by sea).</summary>
    private static byte ColonyState(MatchModel match, MatchFrame target, int row, int settlement, bool colony)
    {
        if (!colony) return 0;
        if (match.Politics.IsBlockaded(settlement)) return 3;
        return target.SettlementCohesion[row] >= (byte)CohesionBand.Strained ? (byte)2 : (byte)1;
    }
}
