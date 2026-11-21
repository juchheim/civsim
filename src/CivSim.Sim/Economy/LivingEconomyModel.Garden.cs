using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// FOR-09 botanical gardens and MED-09 anatomy (M13 WP-E6-8 slice 3, docs/MVP/38 §5.4). Discovery chapter only, and every term is neutral
/// (1) outside it. A garden works only while a botanist holds it; its settlement's medical effects are then stronger by the row's x1.15.
/// MED-09's surgery bonus lands on wound care in a settlement with a staffed hospital. Nothing here is stored: the bonuses read the structure
/// and the roles that already exist, so snapshots and hashes are unchanged.
/// </summary>
public sealed partial class LivingEconomyModel
{
    private int BotanistsWanted(SettlementState s)
        => DiscoveryChain && Done(s.Civilization, "FOR-09") && IronWorksCount(s, IronStructure.BotanicalGarden) > 0 ? 1 : 0;

    /// <summary>Whether the settlement has a garden with a botanist at work in it (an empty garden has no effect).</summary>
    private bool GardenStaffed(SettlementState s)
    {
        if (!DiscoveryChain || IronWorksCount(s, IronStructure.BotanicalGarden) == 0 || !Done(s.Civilization, "FOR-09")) return false;
        foreach (FigureState f in Residents(s.Id)) if (f.Role == Role.Botanist) return true;
        return false;
    }

    /// <summary>A care multiplier m (lower is better) for a figure: MED-05's strengthening, then the staffed garden's med_effects (m becomes 1 - (1 - m) x 1.15).</summary>
    private Fix64 CareFor(FigureState figure, Fix64 multiplier)
    {
        Fix64 m = MedCare(figure.Civilization, multiplier);
        if (!DiscoveryChain) return m;
        Fix64 effects = Modifier(figure.Civilization, "med_effects");
        if (effects > Fix64.One && GardenStaffed(_settlements[figure.SettlementId]))
            m = Fix64.Max(Fix64.Zero, Fix64.One - (Fix64.One - m) * effects);
        return m;
    }

    /// <summary>The injury multiplier of a treated wound: wound care, then anatomy's surgery bonus in a staffed hospital.</summary>
    private Fix64 WoundCareFactor(FigureState figure)
    {
        Fix64 m = CareFor(figure, Fix64.Parse("0.7"));
        if (!DiscoveryChain) return m;
        Fix64 surgery = Modifier(figure.Civilization, "surgery_success");
        if (surgery > Fix64.One && HospitalStaffed(_settlements[figure.SettlementId]))
            m = Fix64.Max(Fix64.Zero, Fix64.One - (Fix64.One - m) * surgery);
        return m;
    }

    internal bool FixtureGardenStaffed(int settlementIndex) => GardenStaffed(_settlements[settlementIndex]);
    internal Fix64 FixtureWoundCareFactor(int figureIndex) => WoundCareFactor(_figures[figureIndex]);
    internal Fix64 FixtureCareFor(int figureIndex, Fix64 multiplier) => CareFor(_figures[figureIndex], multiplier);
}
