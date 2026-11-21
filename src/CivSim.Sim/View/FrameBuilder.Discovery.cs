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
        Array.Clear(d.Engines, 0, n); Array.Clear(d.Ovens, 0, n); Array.Clear(d.Operatives, 0, n);
        Array.Clear(d.Canal, 0, n); Array.Clear(d.Factory, 0, n); Array.Clear(d.Colony, 0, n); Array.Clear(d.GardenStaffed, 0, n);
        Array.Clear(d.Society, 0, n); Array.Clear(d.Bastion, 0, n); Array.Clear(d.GalleonCrewed, 0, n); Array.Clear(d.ExchangeHouse, 0, n);
        d.OceanReach = d.ExchangeStaffed = d.JointStockOpen = d.InsuranceOpen = d.SocietyStaffed = false;
        d.Colonies = 0; d.Key7Permille = 0;
        if (!d.Active) return;

        d.OceanReach = match.Politics.HasOceanReach(_viewer);
        d.ExchangeStaffed = eco.ExchangeStaffed(_viewer);
        d.JointStockOpen = eco.JointStockOpen(_viewer);
        d.InsuranceOpen = eco.InsuranceOpen(_viewer);
        d.SocietyStaffed = eco.SocietyStaffed(_viewer);
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
            d.Engines[w] = (byte)Math.Min(255, t.Engines); d.Ovens[w] = (byte)Math.Min(255, t.Ovens); d.Operatives[w] = (byte)Math.Min(255, t.Operatives);
            d.Canal[w] = (byte)(t.Canal ? 1 : 0); d.Factory[w] = (byte)(t.Factory ? 1 : 0); d.Colony[w] = (byte)(t.Colony ? 1 : 0);
            d.GardenStaffed[w] = (byte)(t.GardenStaffed ? 1 : 0); d.Society[w] = (byte)(t.SocietyHere ? 1 : 0);
            d.Bastion[w] = (byte)(t.Bastion ? 1 : 0); d.GalleonCrewed[w] = (byte)(t.GalleonCrewed ? 1 : 0); d.ExchangeHouse[w] = (byte)(t.ExchangeHere ? 1 : 0);
            if (t.Colony) d.Colonies++;
        }
    }
}
