using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// MIN-10 black-powder blasting (M13 WP-E6-12 follow-up to docs/MVP/38 §5.1; the row: mine and quarry output x2, "blasting consumes gunpowder, 0.02 per litre
/// mined"). Discovery only. A town with powder in store digs and quarries at the row's multiplier and pays powder for what it dug; with the store empty it
/// works at the ordinary rate, so mines compete with the Arsenal for saltpeter and sulfur through the one powder good. Nothing new is stored.
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>Powder a litre of blasted ore or stone costs (the row's provisional 0.02).</summary>
    private static readonly Fix64 BlastingPowderPerLitre = Fix64.FromRatio(2, 100);
    /// <summary>A town blasts only while at least this much powder is in store (five litres' worth at the ore rate is a season's camp work).</summary>
    private static readonly Fix64 BlastingMinPowder = Fix64.FromRatio(1, 10);

    /// <summary>The mine and quarry multiplier a town gets this season: the row's x2 while powder is in store, else 1.</summary>
    private Fix64 BlastingOutput(SettlementState s)
    {
        if (!DiscoveryChain || s.Metal is null || !Done(s.Civilization, "MIN-10")) return Fix64.One;
        if (Stock(s, FiefSlot(FiefGood.Powder)) < BlastingMinPowder) return Fix64.One;
        Fix64 m = Modifier(s.Civilization, "mine_quarry_output");
        return m > Fix64.One ? m : Fix64.One;
    }

    /// <summary>Pays the powder for litres dug or quarried under blasting (all there is, if the store runs short mid-season).</summary>
    private void PayBlastingPowder(SettlementState s, Fix64 litres)
    {
        if (litres <= Fix64.Zero) return;
        int slot = FiefSlot(FiefGood.Powder);
        Fix64 pay = Fix64.Min(Stock(s, slot), litres * BlastingPowderPerLitre);
        if (pay > Fix64.Zero) AddStock(s, slot, -pay);
    }

    internal Fix64 FixtureBlastingOutput(int settlementIndex) => BlastingOutput(_settlements[settlementIndex]);
    internal void FixtureAddPowder(int settlementIndex, Fix64 amount) => AddStock(_settlements[settlementIndex], FiefSlot(FiefGood.Powder), amount);
    internal Fix64 FixtureQuarryRate(int settlementIndex) => QuarryRate(_settlements[settlementIndex]);
    internal Fix64 FixturePowder(int settlementIndex) => Stock(_settlements[settlementIndex], FiefSlot(FiefGood.Powder));
    internal void FixturePayBlasting(int settlementIndex, Fix64 litres) => PayBlastingPowder(_settlements[settlementIndex], litres);
}
