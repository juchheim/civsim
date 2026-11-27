using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// The Columbian Exchange (FOR-10, docs/MVP/38 §5.2, M13 WP-E6-6 slice 5). A civilization with FOR-10 that has actually met a civilization across an ocean (the
/// politics side answers <see cref="IFissionSiteAdvisor.OverseasExchange"/>) gets the new crops, and the old world's fevers come with them. Discovery chapter
/// only; elsewhere every method answers "no change" with an exact one, so other chapters' bytes do not move. Nothing is stored.
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>The field yield of a settlement that can grow an imported staple (the row's x1.2; provisional).</summary>
    private static readonly Fix64 ExchangeYield = Fix64.FromRatio(12, 10);
    /// <summary>The endemic death hazard of a civilization in the exchange, for the fevers that cross with the ships (provisional).</summary>
    private static readonly Fix64 ExchangeFever = Fix64.FromRatio(115, 100);

    private bool InExchange(int civ)
        => DiscoveryChain && HasProject(civ, "FOR-10") && SiteAdvisor?.OverseasExchange(civ) == true;

    /// <summary>
    /// Whether an imported staple grows on the settlement's own biome: maize on the warm and temperate lowlands, the potato on the cool uplands. Desert, ice,
    /// wetland, peaks and open water take neither.
    /// </summary>
    private bool CanGrowImportedStaple(SettlementState s)
        => (Biome)_world.Tile(s.SiteTile).BiomeId is Biome.GrasslandSteppe or Biome.Savanna or Biome.TropicalForest or Biome.OpenWoodland or Biome.TemperateForest
            or Biome.Taiga or Biome.Tundra or Biome.Mountain;

    private Fix64 ExchangeCropYield(SettlementState s)
        => DiscoveryChain && InExchange(s.Civilization) && CanGrowImportedStaple(s) ? ExchangeYield : Fix64.One;

    private Fix64 ExchangeFeverFactor(int civ) => DiscoveryChain && InExchange(civ) ? ExchangeFever : Fix64.One;

    /// <summary>
    /// The imported crop a settlement's fields show for the picture (docs/MVP/39 §7.4): 0 none (outside the exchange, or a biome that takes neither), 1 maize on the
    /// temperate lowlands, 2 the potato on the cool uplands, 3 tobacco on the hot lowlands (savanna and tropical forest). All three are the same x1.2 imported-staple
    /// bonus and the same biome test as the yield, so nothing is drawn that the sim does not pay; tobacco is only the hot lowlands' picture of it, not a fourth good.
    /// </summary>
    public int ExchangeCropAt(int settlementIndex)
    {
        if (!DiscoveryChain || (uint)settlementIndex >= (uint)_settlements.Count) return 0;
        SettlementState s = _settlements[settlementIndex];
        if (!InExchange(s.Civilization) || !CanGrowImportedStaple(s)) return 0;
        Biome biome = (Biome)_world.Tile(s.SiteTile).BiomeId;
        return biome is Biome.Taiga or Biome.Tundra or Biome.Mountain ? 2 : biome is Biome.Savanna or Biome.TropicalForest ? 3 : 1;
    }

    internal Fix64 FixtureExchangeCropYield(int settlementIndex) => ExchangeCropYield(_settlements[settlementIndex]);

    internal Fix64 FixtureExchangeFeverFactor(int civ) => ExchangeFeverFactor(civ);

    internal bool FixtureInExchange(int civ) => InExchange(civ);
}
