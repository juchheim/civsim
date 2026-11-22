using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// CER-11 plate glass in a settlement (M13 WP-E6-7 slice 5, docs/MVP/38 §5.3): glazed windows (housing_comfort x1.05) and the greenhouse, which "adds winter
/// food only in a settlement with a staffed botanical garden (FOR-09) and plate glass". Discovery only. The glass is held, not used up.
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>Share of a staffed-garden town's winter food need its greenhouse covers (provisional: a few glazed beds, not a farm).</summary>
    internal static readonly Fix64 GreenhouseWinterShare = Fix64.FromRatio(6, 100);

    /// <summary>A town "holding plate glass" with CER-11 learned: it has glazed windows.</summary>
    private bool GlazedTown(SettlementState s)
        => DiscoveryChain && Done(s.Civilization, "CER-11") && Stock(s, DiscoverySlot(DiscoveryGood.PlateGlass)) >= Fix64.One;

    /// <summary>The greenhouse: glass and a staffed botanical garden together.</summary>
    private bool GreenhouseWorking(SettlementState s) => GlazedTown(s) && GardenStaffed(s);

    /// <summary>The factor on a town's winter food need (1 without a working greenhouse); the row's value is scaled by the civilization's adoption through <c>Modifier</c> only for comfort.</summary>
    private Fix64 GreenhouseNeedFactor(SettlementState s) => GreenhouseWorking(s) ? Fix64.One - GreenhouseWinterShare : Fix64.One;

    /// <summary>housing_comfort of a glazed town: its housing holds a little more (1 elsewhere).</summary>
    private Fix64 GlazedHousingFactor(SettlementState s)
    {
        if (!GlazedTown(s)) return Fix64.One;
        Fix64 m = Modifier(s.Civilization, "housing_comfort");
        return m > Fix64.One ? m : Fix64.One;
    }

    internal bool FixtureGreenhouseWorking(int settlementIndex) => GreenhouseWorking(_settlements[settlementIndex]);
    internal Fix64 FixtureGreenhouseNeedFactor(int settlementIndex) => GreenhouseNeedFactor(_settlements[settlementIndex]);
    internal Fix64 FixtureGlazedHousingFactor(int settlementIndex) => GlazedHousingFactor(_settlements[settlementIndex]);
    internal Fix64 FixtureHousingCapPublic(int settlementIndex) => HousingCap(_settlements[settlementIndex]);
}
