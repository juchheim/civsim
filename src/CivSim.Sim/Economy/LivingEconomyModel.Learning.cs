using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// Fief-only learning goods and the trade, admin and health modifiers that were typed in the E5 catalog but had no counterpart in the simulation (WP-E5-6/7 audit,
/// docs/MVP/balance/m12-e5-6-7-per-project-audit-2026-11-15.md). Every quantity here is a draft hypothesis, nothing is promoted, and everything is inert outside a Fief match.
/// <list type="bullet">
/// <item>ART-09: a staffed monastery copies illuminated manuscripts from paper, pigment and gold (the registry recipe <c>copy_manuscript</c>); each manuscript is a luxury
/// item for Cohesion; <c>monastery_insight</c> multiplies the Insight of staffed monks (see ResearchSystem).</item>
/// <item>CER-10: spectacles are ground from blown glass and wood; an elder holding one adds the CER-10 Elder Insight bonus, an elder without one does not (the research system
/// reads <see cref="SpectacleHolders"/>); a pair lasts about 80 seasons.</item>
/// <item>NUM-08, TRD-08, GOV-10: <c>trade_volume</c> and <c>contract_trade</c> widen how much of the buyer's stated need one caravan may carry (never more than the seller's
/// surplus, so credit moves existing goods earlier and creates none); <c>admin_capacity</c> widens the span of settlements held without strain.</item>
/// <item>MED-07: <c>elder_hazard</c> lowers the epidemic hazard of infected elders in a settlement with a staffed hospital.</item>
/// </list>
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>Spectacles a settlement may grind in one season (draft).</summary>
    private const int SpectaclesPerSeason = 4;
    /// <summary>Manuscripts a staffed monastery keeps in store (draft); copying stops once the stock reaches it.</summary>
    private const int ManuscriptsPerMonastery = 2;
    /// <summary>Fallback service life if the registry's "80 seasons" cannot be read.</summary>
    private const int SpectacleServiceSeasonsFallback = 80;

    /// <summary>A monastery with its four Monk posts filled (the same test the Know-how copy uses).</summary>
    private bool MonasteryStaffed(SettlementState s)
        => FiefChain && IronWorksCount(s, IronStructure.Monastery) > 0 && MonksAt(s.Id) >= 4;

    private int ElderCount(SettlementState s)
    {
        int n = 0;
        foreach (FigureState f in Residents(s.Id)) if (f.Age >= 45) n++;
        return n;
    }

    /// <summary>
    /// SOC-11 extends age-based service life only for registry goods that both have a numeric season lifetime and are
    /// outputs of a compiled recipe. This excludes indefinite "durable" labels, raw/extracted stock, and the G-TOOL
    /// working-season kits (especially T12), whose dedicated tool-life model is unchanged.
    /// </summary>
    private Fix64 FiefCraftServiceLifeMultiplier(SettlementState settlement, string goodId, int serviceSeasons)
    {
        if (!FiefChain || serviceSeasons <= 0 || goodId.StartsWith("G-TOOL", StringComparison.Ordinal)) return Fix64.One;
        int slot = SlotOf(goodId);
        if (slot == NoSlot) return Fix64.One;
        bool recipeOutput = false;
        foreach (CompiledRecipe recipe in _allRecipes)
        {
            if (recipe.OutputSlot != slot || recipe.OutputQuantity <= Fix64.Zero) continue;
            recipeOutput = true;
            break;
        }
        return recipeOutput ? Modifier(settlement.Civilization, "craft_quality") : Fix64.One;
    }

    /// <summary>One season of stock wear for an age-wearing recipe good; SOC-11 stretches only eligible Fief service life.</summary>
    private Fix64 AgeCraftedDurableGood(SettlementState settlement, string goodId, Fix64 stock, int serviceSeasons)
    {
        if (stock <= Fix64.Zero || serviceSeasons <= 0) return stock;
        Fix64 life = Fix64.FromInt(serviceSeasons) * FiefCraftServiceLifeMultiplier(settlement, goodId, serviceSeasons);
        return life <= Fix64.Zero ? stock : Fix64.Max(Fix64.Zero, stock - stock / life);
    }

    internal Fix64 FixtureCraftServiceLifeMultiplier(int settlementIndex, string goodId, int serviceSeasons)
        => FiefCraftServiceLifeMultiplier(_settlements[settlementIndex], goodId, serviceSeasons);

    /// <summary>CER-10 and ART-09 wants (secondary, like the levy's bows): one pair of spectacles per elder, up to four a season, and two manuscripts per staffed monastery.</summary>
    private void FiefLearningWants(SettlementState s, MetalPlan plan)
    {
        if (!FiefChain) return;
        if (Done(s.Civilization, "CER-10"))
        {
            Fix64 held = Stock(s, BowSlot(BowGood.Spectacles));
            Fix64 deficit = Fix64.FromInt(ElderCount(s)) - held;
            if (deficit > Fix64.Zero)
                plan.Terminal[BowSlot(BowGood.Spectacles)] += held + Fix64.Min(deficit, Fix64.FromInt(SpectaclesPerSeason));
        }
        if (Done(s.Civilization, "ART-09") && MonasteryStaffed(s))
        {
            Fix64 held = Stock(s, BowSlot(BowGood.Manuscript));
            Fix64 target = Fix64.FromInt(ManuscriptsPerMonastery * IronWorksCount(s, IronStructure.Monastery));
            if (held < target) plan.Terminal[BowSlot(BowGood.Manuscript)] += Fix64.Min(target, held + Fix64.One);
        }
    }

    /// <summary>Spectacles wear out over their registry service life; SOC-11 extends this non-kit crafted Fief good.</summary>
    private void WearSpectacles(SettlementState s)
    {
        if (!FiefChain) return;
        int slot = BowSlot(BowGood.Spectacles);
        Fix64 stock = Stock(s, slot);
        if (stock <= Fix64.Zero) return;
        int t = _content.TradeGoodIndex(BowGoodId(BowGood.Spectacles));
        int seasons = t >= 0 && _content.TradeGoods[t].ServiceSeasons > 0 ? _content.TradeGoods[t].ServiceSeasons : SpectacleServiceSeasonsFallback;
        AddStock(s, slot, AgeCraftedDurableGood(s, BowGoodId(BowGood.Spectacles), stock, seasons) - stock);
    }

    /// <summary>Whole pairs of spectacles in a settlement's stores (zero outside Fief).</summary>
    internal int SpectacleStockAt(int settlementIndex)
        => FiefChain && (uint)settlementIndex < (uint)_settlements.Count ? Stock(_settlements[settlementIndex], BowSlot(BowGood.Spectacles)).FloorToInt() : 0;

    /// <summary>Manuscripts in a settlement's stores, for the view and tests.</summary>
    internal Fix64 ManuscriptStockAt(int settlementIndex)
        => FiefChain && (uint)settlementIndex < (uint)_settlements.Count ? Stock(_settlements[settlementIndex], BowSlot(BowGood.Manuscript)) : Fix64.Zero;

    /// <summary>Manuscripts count as luxury items for Cohesion (draft: one each), and only in a Fief match.</summary>
    private Fix64 LearnedLuxuryItems(SettlementState s)
        => FiefChain ? Stock(s, BowSlot(BowGood.Manuscript)) : Fix64.Zero;

    /// <summary>How much of the buyer's stated need one caravan may carry, from NUM-08 and TRD-08 (<c>trade_volume</c>) and GOV-10 (<c>contract_trade</c>); one outside Fief.</summary>
    public Fix64 FiefTradeVolumeFactor(int civ)
        => FiefChain && (uint)civ < (uint)CivilizationCount ? Modifier(civ, "trade_volume") * Modifier(civ, "contract_trade") : Fix64.One;

    /// <summary>Settlement span widened by NUM-08's <c>admin_capacity</c>: the sum of spans times the multiplier, rounded half up (Fief only).</summary>
    private int FiefAdminCapacity(int civ, int span)
    {
        if (!FiefChain || span <= 0) return span;
        Fix64 mult = Modifier(civ, "admin_capacity");
        return mult == Fix64.One ? span : (Fix64.FromInt(span) * mult + Fix64.Half).FloorToInt();
    }

    // ---- fixtures ---------------------------------------------------------------------------------------------------

    internal int FixtureElderCount(int settlementIndex) => ElderCount(_settlements[settlementIndex]);

    /// <summary>Sets the age of the figure at <paramref name="figureIndex"/> in the figure list (tests only).</summary>
    internal void FixtureSetFigureAgeAt(int figureIndex, int age) => _figures[figureIndex].Age = age;

    /// <summary>Makes the first <paramref name="count"/> residents of a settlement exactly <paramref name="age"/> years old (tests only).</summary>
    internal void FixtureAgeResidents(int settlementIndex, int count, int age)
    {
        foreach (FigureState f in Residents(_settlements[settlementIndex].Id))
        {
            if (count-- <= 0) break;
            f.Age = age;
        }
    }

    internal bool FixtureMonasteryStaffed(int settlementIndex) => MonasteryStaffed(_settlements[settlementIndex]);
    internal int FixtureAdminSpanOf(int civ) => AdminSpanOf(civ);
}
