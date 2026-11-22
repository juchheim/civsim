using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>A read-only snapshot of a Discovery town for the view: what stands, what works and what it gives (raw Q32.32 where a quantity).</summary>
public readonly record struct DiscoveryTownReadout(
    long CoalLitres, int Engines, int Ovens, bool Canal, bool Factory, long FactoryCoverage, int Operatives, long Soot,
    bool Colony, bool GardenStaffed, bool SocietyHere, bool Bastion, bool GalleonCrewed, bool ExchangeHere);

public sealed partial class LivingEconomyModel
{
    /// <summary>
    /// The Discovery readout of a town (docs/MVP/38 §8; M13 WP-E6-11): pure reads of existing state, nothing stored, empty outside a Discovery match.
    /// The view frame calls it for the viewer's own towns only.
    /// </summary>
    public DiscoveryTownReadout DiscoveryTown(int settlementIndex)
    {
        if (!DiscoveryChain || (uint)settlementIndex >= (uint)_settlements.Count) return default;
        SettlementState s = _settlements[settlementIndex];
        return new DiscoveryTownReadout(
            CoalStock(settlementIndex).Raw, EngineCount(s), IronWorksCount(s, IronStructure.CokeOven), IronWorksCount(s, IronStructure.NavigationCanal) > 0,
            FactoryCount(s) > 0, FactoryCoverage(s).Raw, OperativesHeld(s), SootAt(s.SiteTile).Raw,
            IsColony(s), GardenStaffed(s), IronWorksCount(s, IronStructure.LearnedSociety) > 0, BastionFortBuilt(settlementIndex),
            CrewedGalleon(settlementIndex), IronWorksCount(s, IronStructure.ExchangeHouse) > 0);
    }

    /// <summary>The site tile of a civilization's capital (where its colonies' tethers lead), or -1 with none; Discovery only.</summary>
    public int CapitalTile(int civ)
    {
        if (!DiscoveryChain || civ < 0) return -1;
        int capital = CapitalOf(civ);
        return capital < 0 ? -1 : _settlements[capital].SiteTile;
    }
}
