using System.Collections.Generic;
using CivSim.Sim.Economy;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

/// <summary>
/// The Council of Elders' Decrees: the hands-off baseline (docs/MVP/22 §4.6, §17 "Council-only players still play"; WP8a). Without
/// it a civilization that never issues a Decree opens no mine beyond its own land and, on most maps, makes almost no bronze (07d
/// §14). It is deterministic and holds no state. Each season, in this order, while Mandate lasts:
/// <list type="number">
/// <item><b>Open a mine</b> on the nearest known, workable deposit of the first ore the civilization needs and has no source of:
/// copper ore once it knows MET-02 or MET-03, then tin once it knows MET-05, else arsenical copper once it knows MET-04 and MIN-04
/// (one camp a season).</item>
/// <item><b>Compact</b> (once a year, in the first season of the year): buy tin when it knows MET-05 but has no tin source, access or
/// tin compact, from the nearest Tin Folk village it has heard of, else from a contacted civilization it has seen working tin.</item>
/// <item><b>Found a village</b> with a full Mandate when a settlement is at 70% of its cap or more, before the 85% natural-fission
/// trigger: the best
/// site by the fission score (food × Beacon pull × rival pressure), ×1.5 beside an unclaimed metal deposit it can use; at most 2
/// per Bronze Age and never more Decree foundings than natural ones (T16's 25–50% share, 07d §7).</item>
/// <item><b>Corvée</b> with a full Mandate on the largest settlement raising a Temple or Palace, or a Market or School already under construction.</item>
/// <item><b>Expedition</b>, still at the cap, to the reachable spot within range where most tiles are unknown.</item>
/// </list>
/// It issues no levy, offers no pact and never embargoes: an engaged player (or the Strategist) does better, which is the point
/// (gate B7: the Settler proxy beats the Council in ≥ 65%).
/// </summary>
public sealed class CouncilDecreePolicy : IDecreePolicy
{
    /// <summary>The Council's early Found window, before the 85% natural fission trigger (docs/MVP/09 §7.1).</summary>
    public static readonly Numerics.Fix64 FoundFill = Numerics.Fix64.FromRatio(70, 100);
    public const int MaxFoundsPerAge = 2;

    internal static bool IsCouncilCorveeTarget(byte target, bool includeGrowthTargets)
        => target is LivingEconomyModel.CorveeTarget.Temple or LivingEconomyModel.CorveeTarget.Palace ||
            includeGrowthTargets && target is LivingEconomyModel.CorveeTarget.Market or LivingEconomyModel.CorveeTarget.School;

    public void Propose(PoliticsSystem p, int civ, List<DecreeRequest> into)
        => Propose(p, civ, into, FoundFill, includeGrowthCorvee: true);

    internal void Propose(PoliticsSystem p, int civ, List<DecreeRequest> into, Numerics.Fix64 foundFill,
        bool includeGrowthCorvee)
    {
        ResearchViewForDecrees r = p.DecreeResearch;
        int mandate = r.Mandate(civ);
        if (mandate <= 0) return;
        int cap = r.MandateCap(civ);
        LivingEconomyModel eco = p.Economy;

        // 1. The ore the metal chain needs.
        int mineCost = p.DecreeCost(civ, DecreeKind.Mine);
        if (mineCost <= mandate)
        {
            int site = -1;
            if ((r.IsDone(civ, "MET-02") || r.IsDone(civ, "MET-03")) && !eco.CivHasOreSource(civ, DepositKind.CopperOre))
                site = p.NearestMineSite(civ, DepositKind.CopperOre);
            if (site < 0 && r.IsDone(civ, "MET-05") && !eco.CivHasOreSource(civ, DepositKind.Tin) && !eco.CivHasMetalAccess(civ, MetalAccessKind.Tin))
                site = p.NearestMineSite(civ, DepositKind.Tin);
            if (site < 0 && r.IsDone(civ, "MET-04") && r.IsDone(civ, "MIN-04") && !eco.CivHasOreSource(civ, DepositKind.ArsenicalCopper) &&
                !eco.CivHasMetalAccess(civ, MetalAccessKind.Tin))
                site = p.NearestMineSite(civ, DepositKind.ArsenicalCopper);
            if (site >= 0)
            {
                into.Add(new DecreeRequest(DecreeKind.Mine, site, 0));
                mandate -= mineCost;
            }
        }

        // 2. Tin by compact.
        int compactCost = p.DecreeCost(civ, DecreeKind.Compact);
        if (compactCost <= mandate && p.Season % 4 == 0 && ShouldSeekTinCompact(r, eco, p, civ))
        {
            int seller = p.TinSellerFor(civ);
            if (seller != int.MinValue)
            {
                into.Add(new DecreeRequest(DecreeKind.Compact, seller, DecreeArgs.PackCompact(TradeGood.Tin, sell: false)));
                mandate -= compactCost;
            }
        }

        // 3. A placed founding with a full Mandate.
        int foundCost = p.DecreeCost(civ, DecreeKind.Found);
        int founded = p.DecreesDone(civ, DecreeKind.Found);
        int natural = p.E3Foundings(civ) - founded;
        bool proposedFound = false;
        if (mandate >= cap && foundCost <= mandate && founded < MaxFoundsPerAge && natural >= founded + 1)
        {
            int tile = p.ChooseFoundSite(civ, foundFill);
            if (tile >= 0)
            {
                into.Add(new DecreeRequest(DecreeKind.Found, tile, 0));
                mandate -= foundCost;
                proposedFound = true;
            }
        }

        // 4. Corvée on the public work under way, with a full Mandate.
        int corveeCost = p.DecreeCost(civ, DecreeKind.Corvee);
        if (!proposedFound && mandate >= cap && corveeCost <= mandate)
        {
            int best = -1, bestPop = 0;
            for (int s = 0; s < p.SettlementTotal; s++)
            {
                if (p.SettlementCivOf(s) != civ || p.IsFreeFolk(s) || eco.CorveeAt(s) >= 0) continue;
                byte target = eco.CivicBuildTarget(s);
                if (!IsCouncilCorveeTarget(target, includeGrowthCorvee)) continue;
                int pop = p.SettlementPopulationOf(s);
                if (pop > bestPop && eco.HasCivicWork(s, target)) { best = s; bestPop = pop; }
            }
            if (best >= 0)
            {
                into.Add(new DecreeRequest(DecreeKind.Corvee, best, eco.CivicBuildTarget(best)));
                mandate -= corveeCost;
            }
        }

        // 5. Rather than sit at the cap: an Expedition where the most is unknown (deposits, Tin Folk, rivals).
        int expeditionCost = p.DecreeCost(civ, DecreeKind.Expedition);
        if (!proposedFound && mandate >= cap && expeditionCost <= mandate)
        {
            int tile = p.ChooseExpeditionTarget(civ);
            if (tile >= 0) into.Add(new DecreeRequest(DecreeKind.Expedition, tile, 0));
        }
    }

    /// <summary>
    /// MET-05 is gated on having Tin, so the Council must seek a tin compact after MET-03 unlocks the Tin Bronze route,
    /// before waiting for MET-05 itself would create a circular dependency.
    /// </summary>
    internal static bool ShouldSeekTinCompact(ResearchViewForDecrees research, LivingEconomyModel economy, PoliticsSystem politics, int civ)
        => research.IsDone(civ, "MET-03") &&
           !economy.CivHasMetalAccess(civ, MetalAccessKind.Tin) &&
           !economy.CivHasOreSource(civ, DepositKind.Tin) &&
           !politics.BuysTin(civ);
}
