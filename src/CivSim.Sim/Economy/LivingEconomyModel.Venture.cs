using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// Joint-stock ventures and insurance (TRD-10 and TRD-11, docs/MVP/38 §5.2, decision D12: pooled goods only, no debt and no money; M13 WP-E6-6 slice 6). Both are
/// about <em>who stands behind a voyage</em>, so both need more than one purse: a civilization with fewer than two harbor towns has nobody to pool with or to spread a
/// loss across. Nothing here mints money or records a debt, and nothing is stored: the state is read from standing harbors, exchange houses and brokers. Discovery only.
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>Brokers an exchange house needs to be open (adults taken off other work; provisional).</summary>
    private const int BrokersPerExchange = 2;
    /// <summary>A town this small cannot spare brokers.</summary>
    private const int ExchangeMinPopulation = 60;

    /// <summary>Harbor towns of a civilization: the purses a joint-stock voyage ledger pools and an insurance pool spreads across.</summary>
    public int HarborTowns(int civ)
    {
        if (!DiscoveryChain) return 0;
        int n = 0;
        foreach (SettlementState s in _settlements)
            if (IsMember(s, civ) && IronWorksCount(s, IronStructure.Harbor) > 0) n++;
        return n;
    }

    private int BrokersWanted(SettlementState s)
        => DiscoveryChain && Done(s.Civilization, "TRD-11") && IronWorksCount(s, IronStructure.ExchangeHouse) > 0 && IronWorksCount(s, IronStructure.Harbor) > 0 ? BrokersPerExchange : 0;

    private int BrokersHeld(SettlementState s)
    {
        int n = 0;
        foreach (FigureState f in Residents(s.Id)) if (f.Role == Role.Broker) n++;
        return n;
    }

    /// <summary>Whether the civilization has an exchange house, in a harbor town, with its brokers at their desks (an unstaffed exchange has no effect).</summary>
    public bool ExchangeStaffed(int civ)
    {
        if (!DiscoveryChain || !Done(civ, "TRD-11")) return false;
        foreach (SettlementState s in _settlements)
            if (IsMember(s, civ) && IronWorksCount(s, IronStructure.ExchangeHouse) > 0 && IronWorksCount(s, IronStructure.Harbor) > 0 && BrokersHeld(s) >= BrokersPerExchange) return true;
        return false;
    }

    /// <summary>Whether TRD-10's pooled ledger is open to the civilization: the project and at least two harbor towns to pool.</summary>
    public bool JointStockOpen(int civ) => DiscoveryChain && Done(civ, "TRD-10") && HarborTowns(civ) >= 2;

    /// <summary>Whether TRD-11's insurance covers the civilization's voyages: a staffed exchange house and at least two harbor towns to spread a loss across.</summary>
    public bool InsuranceOpen(int civ) => ExchangeStaffed(civ) && HarborTowns(civ) >= 2;

    internal void FixtureStaffBrokers(int settlementIndex, int count)
    {
        int given = 0;
        foreach (FigureState f in Residents(_settlements[settlementIndex].Id))
        {
            if (f.Age is < 14 or >= 60 || f.Camp >= 0) continue;
            f.Role = given < count ? Role.Broker : Role.None;
            if (given < count) given++;
        }
    }
}
