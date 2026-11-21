using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// LRN-09 learned societies (M13 WP-E6-7 slice 2, docs/MVP/38 §5.3). Discovery chapter only. A society seats scholars like an academy; it works only
/// while a scholar sits in it (an empty society has no effect), and then the civilization's research gains the row's Know-how spillover and its
/// diffusion bonus (the research side reads <see cref="SocietyStaffed"/> through the research source). Nothing here is stored.
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>Most societies a civilization keeps (provisional): one for the capital's learning, one for a second seat.</summary>
    private const int MaxSocieties = 2;
    /// <summary>Scholar seats a society adds (provisional: an academy gives one).</summary>
    private const int SocietySeatsEach = 2;

    private int SocietySeats(SettlementState s)
        => DiscoveryChain && Done(s.Civilization, "LRN-09") ? SocietySeatsEach * IronWorksCount(s, IronStructure.LearnedSociety) : 0;

    /// <summary>Whether the civilization has a learned society with a scholar at work in its town.</summary>
    public bool SocietyStaffed(int civ)
    {
        if (!DiscoveryChain || !Done(civ, "LRN-09")) return false;
        foreach (SettlementState s in _settlements)
        {
            if (!IsMember(s, civ) || IronWorksCount(s, IronStructure.LearnedSociety) == 0) continue;
            foreach (FigureState f in Residents(s.Id)) if (f.Role == Role.Scholar) return true;
        }
        return false;
    }

    internal bool FixtureSocietyStaffed(int civ) => SocietyStaffed(civ);
}
