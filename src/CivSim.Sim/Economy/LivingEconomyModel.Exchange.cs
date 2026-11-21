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

    internal Fix64 FixtureExchangeCropYield(int settlementIndex) => ExchangeCropYield(_settlements[settlementIndex]);

    internal Fix64 FixtureExchangeFeverFactor(int civ) => ExchangeFeverFactor(civ);

    internal bool FixtureInExchange(int civ) => InExchange(civ);
}
