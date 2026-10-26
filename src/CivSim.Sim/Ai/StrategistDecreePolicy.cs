using System;
using System.Collections.Generic;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

/// <summary>
/// Deterministic first Strategist decree planner. The Council's feasibility rules generate only legal candidates; this layer
/// scores their expected strategic value, logs every positive candidate, and issues the highest-valued one with stable ties.
/// </summary>
internal sealed class StrategistDecreePolicy : IDecreePolicy
{
    // A compact remains legal while a village is known by map memory or lies within twice Reach. Keep its
    // belief evidence alive for the same horizon as tin-route research evidence; the 2-season levy window
    // does not apply to compact proposals.
    private const int TinFolkCompactEvidenceSeasons = 12;

    private readonly AiLog _log;
    private readonly Belief _belief;
    private readonly StrategicIntent _intent;
    private readonly CouncilDecreePolicy _feasibility = new();
    private readonly List<DecreeRequest> _candidates = new();
    private readonly List<DecreeForecast> _forecasts = new();
    private bool[] _foundFrontierSites = Array.Empty<bool>();
    private byte[] _foundDonorProtectionCache = Array.Empty<byte>();

    public StrategistDecreePolicy(AiLog log, Belief belief, StrategicIntent intent)
    { _log = log; _belief = belief; _intent = intent; }

    public void Propose(PoliticsSystem politics, int civ, List<DecreeRequest> into)
    {
        _candidates.Clear();
        _feasibility.Propose(politics, civ, _candidates);
        // The Council's expedition target uses live route passability. Replace it with candidates ranked from
        // explored-map knowledge and geometric range; an unknown route is accepted as uncertain.
        _candidates.RemoveAll(candidate => candidate.Kind == DecreeKind.Expedition);
        AddBeliefExpeditionAlternatives(politics, civ, _belief, _candidates);
        _candidates.RemoveAll(candidate => !BeliefAllowsCandidate(_belief, civ, candidate));
        ResearchViewForDecrees mandate = politics.DecreeResearch;
        bool frontierFoundOpportunity = false;
        byte[]? donorProtectionCache = null;
        if (mandate.MandateUnlocked(civ))
        {
            if (_foundFrontierSites.Length < _belief.Map.TileCount) _foundFrontierSites = new bool[_belief.Map.TileCount];
            if (_foundDonorProtectionCache.Length < politics.SettlementTotal)
                _foundDonorProtectionCache = new byte[politics.SettlementTotal];
            Array.Clear(_foundDonorProtectionCache, 0, politics.SettlementTotal);
            donorProtectionCache = _foundDonorProtectionCache;
            StrategistTuning.MarkRecentRivalFrontierSites(_belief, civ,
                politics.ClaimRadiusOfEra(mandate.Era(civ)), _foundFrontierSites);
            frontierFoundOpportunity = HasFrontierFoundOpportunity(politics, civ, _belief,
                _foundFrontierSites, donorProtectionCache);
        }
        AddBeliefKnownMineAlternatives(politics, civ, _belief, _candidates);
        AddBeliefKnownFoundAlternatives(politics, civ, _belief, _candidates,
            frontierFoundOpportunity ? _foundFrontierSites : null);
        AddWallCorveeAlternatives(politics, civ, _candidates);
        if (_intent.Plan == GrandPlan.SecureTin) AddBeliefKnownLevyAlternatives(politics, civ, _belief, _candidates);
        AddBeliefKnownTinCompactAlternatives(politics, civ, _belief, _candidates);
        if (_candidates.Count == 0) return;

        // Forecasting is pure for the duration of Propose: candidates are evaluated before any decree
        // is resolved, and neither scoring nor logging mutates PoliticsSystem. Cache each result because
        // the mandate-cap scan and final selection both need it, and the winner is logged a second time.
        _forecasts.Clear();
        bool hasFoundCandidate = false;
        foreach (DecreeRequest candidate in _candidates)
            if (candidate.Kind == DecreeKind.Found) { hasFoundCandidate = true; break; }
        int? knownOwnedWeight = hasFoundCandidate && mandate.MandateUnlocked(civ) &&
            mandate.Mandate(civ) >= politics.DecreeCost(civ, DecreeKind.Found)
            ? DecreeForecaster.KnownOwnedHabitableWeight(politics, civ, _belief)
            : null;
        foreach (DecreeRequest candidate in _candidates)
            _forecasts.Add(DecreeForecaster.Forecast(politics, civ, candidate.Kind, candidate.Target,
                candidate.Extra, _belief, knownOwnedWeight, donorProtectionCache));

        bool atMandateCap = mandate.Mandate(civ) >= mandate.MandateCap(civ);
        bool positivePaidAlternative = false;
        if (atMandateCap)
            for (int i = 0; i < _candidates.Count; i++)
            {
                DecreeRequest candidate = _candidates[i];
                if (politics.DecreeCost(civ, candidate.Kind) <= 0) continue;
                DecreeForecast forecast = _forecasts[i];
                Fix64 gross = GrossBenefit(politics, civ, candidate, forecast) +
                    DefenseCorveeBonus(politics, candidate, forecast, _intent.DefenseUrgency) + SecureTinBonus(candidate) +
                    FoundFrontierBonus(candidate);
                if (CandidateNetEv(forecast, gross, Fix64.One) > Fix64.Zero) { positivePaidAlternative = true; break; }
            }

        int bestIndex = -1;
        Fix64 bestValue = Fix64.Zero;
        bool reserveLogged = false;
        for (int i = 0; i < _candidates.Count; i++)
        {
            DecreeRequest candidate = _candidates[i];
            DecreeForecast forecast = _forecasts[i];
            Fix64 gross = GrossBenefit(politics, civ, candidate, forecast);
            Fix64 defenseBonus = DefenseCorveeBonus(politics, candidate, forecast, _intent.DefenseUrgency);
            gross += defenseBonus;
            Fix64 secureTinBonus = SecureTinBonus(candidate);
            gross += secureTinBonus;
            Fix64 foundFrontierBonus = FoundFrontierBonus(candidate);
            gross += foundFrontierBonus;
            Fix64 value = CandidateNetEv(forecast, gross, Fix64.One);
            _log.Add(new AiDecision((long)politics.Season * ResearchSystem.SeasonTicks, politics.Season, civ,
                "Strategist", "DecreeCandidate", (int)candidate.Kind, candidate.Target, value,
                Describe(candidate, forecast, gross, value, secureTinBonus, foundFrontierBonus)));
            // A zero-valued candidate should not consume Mandate or suppress another action.
            if (value <= Fix64.Zero) continue;
            if (ShouldReserveFrontierFoundMandate(candidate, politics.DecreeCost(civ, candidate.Kind),
                    mandate.Mandate(civ), politics.DecreeCost(civ, DecreeKind.Found), frontierFoundOpportunity,
                    urgentDefense: defenseBonus > Fix64.Zero))
            {
                if (!reserveLogged)
                {
                    reserveLogged = true;
                    _log.Add(new AiDecision((long)politics.Season * ResearchSystem.SeasonTicks, politics.Season, civ,
                        "Strategist", "DecreeReserve", (int)candidate.Kind, (int)DecreeKind.Found,
                        Fix64.FromInt(mandate.Mandate(civ)),
                        $"held {politics.DecreeCost(civ, DecreeKind.Found)} Mandate for a belief-legal frontier Found; " +
                        $"skipped {candidate.Kind} cost {politics.DecreeCost(civ, candidate.Kind)}"));
                }
                continue;
            }
            if (ShouldPreferMandateSpender(atMandateCap, positivePaidAlternative, politics.DecreeCost(civ, candidate.Kind))) continue;
            if (value > bestValue || value == bestValue && (bestIndex < 0 || Compare(candidate, _candidates[bestIndex]) < 0))
            {
                bestIndex = i;
                bestValue = value;
            }
        }

        if (bestIndex < 0) return;
        DecreeRequest chosen = _candidates[bestIndex];
        into.Add(chosen);
        DecreeForecast chosenForecast = _forecasts[bestIndex];
        Fix64 chosenGross = GrossBenefit(politics, civ, chosen, chosenForecast);
        chosenGross += DefenseCorveeBonus(politics, chosen, chosenForecast, _intent.DefenseUrgency);
        Fix64 chosenSecureTinBonus = SecureTinBonus(chosen);
        chosenGross += chosenSecureTinBonus;
        Fix64 chosenFoundFrontierBonus = FoundFrontierBonus(chosen);
        chosenGross += chosenFoundFrontierBonus;
        _log.Add(new AiDecision((long)politics.Season * ResearchSystem.SeasonTicks, politics.Season, civ,
            "Strategist", "Decree", (int)chosen.Kind, chosen.Target, bestValue,
            $"chosen; {Describe(chosen, chosenForecast, chosenGross, bestValue, chosenSecureTinBonus, chosenFoundFrontierBonus)}"));
    }

    internal static bool ShouldPreferMandateSpender(bool atMandateCap, bool positivePaidAlternative, int candidateCost)
        => atMandateCap && positivePaidAlternative && candidateCost == 0;

    internal static void AddBeliefExpeditionAlternatives(PoliticsSystem politics, int civ, Belief belief, List<DecreeRequest> candidates)
    {
        ResearchViewForDecrees research = politics.DecreeResearch;
        if (politics.DecreeCost(civ, DecreeKind.Expedition) > research.Mandate(civ)) return;
        int home = -1, population = 0;
        for (int s = 0; s < politics.SettlementTotal; s++)
            if (politics.SettlementCivOf(s) == civ && !politics.IsFreeFolk(s) &&
                politics.SettlementPopulationOf(s) > population)
            { home = s; population = politics.SettlementPopulationOf(s); }
        // A conservative donor-size floor; resolution still enforces the exact adult/crew requirement.
        if (home < 0 || population < 16) return;
        int origin = politics.SettlementTileOf(home), range = politics.ExpeditionRange(civ), radius = 5;
        HexGrid.Coordinates(origin, belief.Map.Width, out int cx, out int cy);
        for (int y = Math.Max(0, cy - range); y <= Math.Min(belief.Map.Height - 1, cy + range); y += 3)
            for (int x = Math.Max(0, cx - range); x <= Math.Min(belief.Map.Width - 1, cx + range); x += 3)
            {
                int tile = y * belief.Map.Width + x;
                if (HexGrid.Distance(origin, tile, belief.Map.Width) > range || belief.Map.IsExplored(tile)) continue;
                int unknown = 0;
                for (int yy = Math.Max(0, y - radius); yy <= Math.Min(belief.Map.Height - 1, y + radius); yy++)
                    for (int xx = Math.Max(0, x - radius - 1); xx <= Math.Min(belief.Map.Width - 1, x + radius + 1); xx++)
                    {
                        int t = yy * belief.Map.Width + xx;
                        if (HexGrid.Distance(tile, t, belief.Map.Width) <= radius && !belief.Map.IsExplored(t)) unknown++;
                    }
                if (unknown < 20) continue;
                var request = new DecreeRequest(DecreeKind.Expedition, tile, 0);
                if (!Contains(candidates, request)) candidates.Add(request);
            }
    }

    internal static void AddWallCorveeAlternatives(PoliticsSystem politics, int civ, List<DecreeRequest> candidates)
    {
        ResearchViewForDecrees research = politics.DecreeResearch;
        int mandate = research.Mandate(civ);
        if (mandate < research.MandateCap(civ) || politics.DecreeCost(civ, DecreeKind.Corvee) > mandate) return;
        for (int settlement = 0; settlement < politics.SettlementTotal; settlement++)
        {
            if (politics.SettlementCivOf(settlement) != civ || politics.IsFreeFolk(settlement) || politics.SettlementPopulationOf(settlement) <= 0 ||
                politics.Economy.CorveeAt(settlement) >= 0 || !politics.Economy.HasCivicWork(settlement, LivingEconomyModel.CorveeTarget.Walls)) continue;
            var request = new DecreeRequest(DecreeKind.Corvee, settlement, LivingEconomyModel.CorveeTarget.Walls);
            if (!Contains(candidates, request)) candidates.Add(request);
        }
    }

    internal static Fix64 DefenseCorveeBonus(PoliticsSystem politics, DecreeRequest candidate, DecreeForecast forecast, Fix64 defenseUrgency)
    {
        if (candidate.Kind != DecreeKind.Corvee || candidate.Extra != LivingEconomyModel.CorveeTarget.Walls || defenseUrgency <= Fix64.Zero ||
            unchecked((uint)candidate.Target) >= unchecked((uint)politics.SettlementTotal)) return Fix64.Zero;
        if (forecast.ThreatEtaSeasons < 0 || forecast.ThreatMarginSeasons <= Fix64.Zero) return Fix64.Zero;
        // The margin is how many seasons the forecasted wall completion precedes a threat grounded in recent belief.
        return Fix64.FromInt(politics.SettlementPopulationOf(candidate.Target)) * forecast.ThreatMarginSeasons * defenseUrgency / 10;
    }

    internal static void AddBeliefKnownMineAlternatives(PoliticsSystem politics, int civ, Belief belief, List<DecreeRequest> candidates)
    {
        ResearchViewForDecrees research = politics.DecreeResearch;
        if (politics.DecreeCost(civ, DecreeKind.Mine) > research.Mandate(civ)) return;
        LivingEconomyModel economy = politics.Economy;
        bool copperNeeded = (research.IsDone(civ, "MET-02") || research.IsDone(civ, "MET-03")) &&
            !economy.CivHasOreSource(civ, CivSim.Sim.World.DepositKind.CopperOre);
        // An ore source does not guarantee current metal access: its seasonal output can be consumed before the
        // access checkpoint. Keep a second known mine as a recovery option while Tin is actually unavailable.
        bool tinNeeded = research.IsDone(civ, "MET-05") &&
            !economy.CivHasMetalAccess(civ, MetalAccessKind.Tin);
        bool arsenicalNeeded = research.IsDone(civ, "MET-04") && research.IsDone(civ, "MIN-04") &&
            !economy.CivHasOreSource(civ, CivSim.Sim.World.DepositKind.ArsenicalCopper) &&
            !economy.CivHasMetalAccess(civ, MetalAccessKind.Tin);
        for (int tile = 0; tile < belief.Map.TileCount; tile++)
        {
            if (!belief.Map.TryTile(tile, out var known) || known.Deposit switch
                {
                    CivSim.Sim.World.DepositKind.CopperOre => !copperNeeded,
                    CivSim.Sim.World.DepositKind.Tin => !tinNeeded,
                    CivSim.Sim.World.DepositKind.ArsenicalCopper => !arsenicalNeeded,
                    _ => true,
                }) continue;
            var request = new DecreeRequest(DecreeKind.Mine, tile, 0);
            if (BeliefAllowsCandidate(belief, civ, request) && !Contains(candidates, request)) candidates.Add(request);
        }
    }

    internal static void AddBeliefKnownFoundAlternatives(PoliticsSystem politics, int civ, Belief belief, List<DecreeRequest> candidates)
        => AddBeliefKnownFoundAlternatives(politics, civ, belief, candidates, null);

    internal static void AddBeliefKnownFoundAlternatives(PoliticsSystem politics, int civ, Belief belief,
        List<DecreeRequest> candidates, bool[]? frontierSites)
    {
        ResearchViewForDecrees research = politics.DecreeResearch;
        int mandate = research.Mandate(civ);
        int foundCost = politics.DecreeCost(civ, DecreeKind.Found);
        int founded = politics.DecreesDone(civ, DecreeKind.Found);
        bool atMandateCap = mandate >= research.MandateCap(civ);
        if ((!atMandateCap && frontierSites is null) || foundCost > mandate || founded >= CouncilDecreePolicy.MaxFoundsPerAge ||
            politics.E3Foundings(civ) - founded < founded + 1) return;
        for (int tile = 0; tile < belief.Map.TileCount; tile++)
        {
            bool frontierMarked = unchecked((uint)tile) < (uint)(frontierSites?.Length ?? 0) && frontierSites![tile];
            if (!ShouldAddFoundAlternative(mandate, research.MandateCap(civ), foundCost, frontierMarked)) continue;
            var request = new DecreeRequest(DecreeKind.Found, tile, 0);
            if (BeliefAllowsCandidate(belief, civ, request) && !Contains(candidates, request)) candidates.Add(request);
        }
    }

    internal static bool ShouldAddFoundAlternative(int mandate, int mandateCap, int foundCost, bool frontierMarked)
        => mandate >= mandateCap || frontierMarked && mandate >= foundCost;

    internal static bool ShouldReserveFrontierFoundMandate(DecreeRequest candidate, int candidateCost,
        int currentMandate, int foundCost, bool frontierFoundOpportunity, bool urgentDefense)
    {
        // Protect accumulation toward Found, but once Found is affordable let the normal EV ranking decide.
        if (!frontierFoundOpportunity || candidateCost <= 0 || currentMandate - candidateCost >= foundCost) return false;
        return candidate.Kind == DecreeKind.Expedition ||
            candidate.Kind == DecreeKind.Corvee && !urgentDefense;
    }

    private static bool HasFrontierFoundOpportunity(PoliticsSystem politics, int civ, Belief belief,
        bool[] frontierSites, byte[]? donorProtectionCache)
    {
        int foundCost = politics.DecreeCost(civ, DecreeKind.Found);
        int founded = politics.DecreesDone(civ, DecreeKind.Found);
        if (foundCost <= 0 || founded >= CouncilDecreePolicy.MaxFoundsPerAge ||
            politics.E3Foundings(civ) - founded < founded + 1) return false;
        for (int tile = 0; tile < belief.Map.TileCount; tile++)
        {
            if (!frontierSites[tile]) continue;
            var request = new DecreeRequest(DecreeKind.Found, tile, 0);
            if (!BeliefAllowsCandidate(belief, civ, request)) continue;
            if (DecreeForecaster.FoundOwnCheck(politics, civ, tile, belief, donorProtectionCache, out _) == 0) return true;
        }
        return false;
    }

    internal static void AddBeliefKnownLevyAlternatives(PoliticsSystem politics, int civ, Belief belief, List<DecreeRequest> candidates)
    {
        if (politics.DecreeCost(civ, DecreeKind.Levy) > politics.DecreeResearch.Mandate(civ)) return;
        foreach (BeliefTinFolk folk in belief.TinFolkSeen)
        {
            if (belief.Season - folk.LastSeenSeason is < 0 or > 2) continue;
            var request = new DecreeRequest(DecreeKind.Levy, folk.Settlement, 0);
            if (!Contains(candidates, request)) candidates.Add(request);
        }
    }

    internal static void AddBeliefKnownTinCompactAlternatives(PoliticsSystem politics, int civ, Belief belief, List<DecreeRequest> candidates)
    {
        ResearchViewForDecrees research = politics.DecreeResearch;
        if (belief.Season % 4 != 0 || !research.IsDone(civ, "MET-05") ||
            politics.DecreeCost(civ, DecreeKind.Compact) > research.Mandate(civ) ||
            politics.Economy.CivHasMetalAccess(civ, MetalAccessKind.Tin) ||
            politics.BuysTin(civ)) return;
        int extra = DecreeArgs.PackCompact(TradeGood.Tin, sell: false);
        foreach (BeliefTinFolk folk in belief.TinFolkSeen)
        {
            if (!HasRecentTinFolkCompactEvidence(belief, folk)) continue;
            AddIfBelievedCompactRoute(belief, civ, DecreeArgs.FolkParty(folk.Settlement), extra, candidates);
        }
        for (int rival = 0; rival < belief.Rivals.Length; rival++)
        {
            if (rival == civ) continue;
            RivalBelief known = belief.Rivals[rival];
            if (!known.Contacted || !(known.TinAccessSeen || (known.SeenFlags & SeenFlag.TinMineCamp) != 0)) continue;
            AddIfBelievedCompactRoute(belief, civ, rival, extra, candidates);
        }
    }

    private static void AddIfBelievedCompactRoute(Belief belief, int civ, int target, int extra, List<DecreeRequest> candidates)
    {
        var request = new DecreeRequest(DecreeKind.Compact, target, extra);
        if (BeliefAllowsCandidate(belief, civ, request) && !Contains(candidates, request)) candidates.Add(request);
    }

    private static bool HasRecentTinFolkCompactEvidence(Belief belief, BeliefTinFolk folk)
        => belief.Season - folk.LastSeenSeason is >= 0 and <= TinFolkCompactEvidenceSeasons;

    private static bool Contains(List<DecreeRequest> candidates, DecreeRequest request)
    {
        foreach (DecreeRequest candidate in candidates)
            if (candidate.Kind == request.Kind && candidate.Target == request.Target && candidate.Extra == request.Extra) return true;
        return false;
    }

    internal static bool BeliefAllowsCandidate(Belief belief, int civ, DecreeRequest candidate)
    {
        if (candidate.Kind == DecreeKind.Found)
        {
            if (unchecked((uint)candidate.Target) >= unchecked((uint)belief.Map.TileCount) || !belief.Map.IsExplored(candidate.Target)) return false;
            foreach (RivalBelief knownRival in belief.Rivals)
            {
                if (knownRival.Civ == civ) continue;
                foreach (BeliefSettlement settlement in knownRival.Settlements)
                    if (belief.Season - settlement.LastSeenSeason is >= 0 and <= 2 &&
                        CivSim.Sim.World.HexGrid.Distance(settlement.Tile, candidate.Target, belief.Map.Width) < 9) return false;
            }
            foreach (BeliefTinFolk folk in belief.TinFolkSeen)
                if (belief.Season - folk.LastSeenSeason is >= 0 and <= 2 &&
                    CivSim.Sim.World.HexGrid.Distance(folk.Tile, candidate.Target, belief.Map.Width) < 9) return false;
            return true;
        }
        if (candidate.Kind == DecreeKind.Mine)
        {
            int tile = candidate.Target;
            if (unchecked((uint)tile) >= unchecked((uint)belief.Map.TileCount) || !belief.Map.TryTile(tile, out var known)) return false;
            if (known.Deposit is not (CivSim.Sim.World.DepositKind.CopperOre or CivSim.Sim.World.DepositKind.ArsenicalCopper or
                CivSim.Sim.World.DepositKind.Tin or CivSim.Sim.World.DepositKind.Gold or CivSim.Sim.World.DepositKind.Silver or
                CivSim.Sim.World.DepositKind.Salt or CivSim.Sim.World.DepositKind.Iron)) return false;
            (int owner, int seenSeason) = belief.Map.OwnerSeen(tile);
            return belief.Season - seenSeason is >= 0 and <= 2 && (owner == -1 || owner == civ);
        }
        if (candidate.Kind != DecreeKind.Compact || DecreeArgs.Variant(candidate.Extra) != CompactVariant.Goods ||
            DecreeArgs.Good(candidate.Extra) != TradeGood.Tin || DecreeArgs.Sell(candidate.Extra)) return true;

        int target = candidate.Target;
        if (DecreeArgs.IsFolkParty(target))
        {
            int settlement = DecreeArgs.FolkSettlement(target);
            foreach (BeliefTinFolk folk in belief.TinFolkSeen)
                if (folk.Settlement == settlement && HasRecentTinFolkCompactEvidence(belief, folk)) return true;
            return false;
        }
        if (unchecked((uint)target) >= unchecked((uint)belief.Rivals.Length) || target == civ) return false;
        RivalBelief rival = belief.Rivals[target];
        return rival.Contacted && (rival.TinAccessSeen || (rival.SeenFlags & SeenFlag.TinMineCamp) != 0);
    }

    internal static Fix64 GrossBenefit(PoliticsSystem politics, int civ, DecreeRequest candidate, DecreeForecast forecast)
    {
        return candidate.Kind switch
        {
            DecreeKind.Found or DecreeKind.Mine or DecreeKind.Expedition or DecreeKind.Levy => ForecastBenefit(candidate.Kind, forecast),
            DecreeKind.Compact => CompactBenefit(politics, civ, candidate, forecast) + Fix64.FromInt(forecast.RelationsDelta),
            DecreeKind.Corvee => CorveeApproximation(politics, candidate.Target, forecast),
            _ => Fix64.Zero,
        };
    }

    internal static Fix64 ForecastBenefit(DecreeKind kind, DecreeForecast forecast) => kind switch
    {
        DecreeKind.Found => Fix64.FromInt(forecast.People * 3) + forecast.LandWeight + forecast.Dominion,
        DecreeKind.Mine => forecast.MetalValue,
        DecreeKind.Expedition => forecast.Information * Fix64.FromInt(2),
        DecreeKind.Levy => Fix64.FromInt(forecast.People * 4),
        _ => Fix64.Zero,
    };

    internal static Fix64 CandidateNetEv(DecreeForecast forecast, Fix64 grossBenefit, Fix64 mandateValue)
    {
        if (!forecast.Allowed) return Fix64.MinValue;
        Fix64 success = Fix64.FromRatio(forecast.SuccessPermille, 1000);
        return grossBenefit * success - Fix64.FromInt(forecast.Cost) * mandateValue;
    }

    private Fix64 SecureTinBonus(DecreeRequest candidate)
    {
        if (_intent.Plan != GrandPlan.SecureTin) return Fix64.Zero;
        if (candidate.Kind == DecreeKind.Compact && DecreeArgs.Good(candidate.Extra) == TradeGood.Tin && !DecreeArgs.Sell(candidate.Extra))
            return StrategistTuning.SecureTinCompactDecreeBonus;
        if (candidate.Kind == DecreeKind.Levy)
            foreach (BeliefTinFolk folk in _belief.TinFolkSeen)
                if (folk.Settlement == candidate.Target && _belief.Season - folk.LastSeenSeason <= 2)
                    return StrategistTuning.SecureTinLevyDecreeBonus;
        if (candidate.Kind != DecreeKind.Mine || !_belief.Map.TryTile(candidate.Target, out CivSim.Sim.World.WorldTile known)) return Fix64.Zero;
        return known.Deposit switch
        {
            CivSim.Sim.World.DepositKind.Tin => StrategistTuning.SecureTinMineDecreeBonus,
            CivSim.Sim.World.DepositKind.ArsenicalCopper => StrategistTuning.SecureTinArsenicalDecreeBonus,
            _ => Fix64.Zero,
        };
    }

    private static Fix64 CompactBenefit(PoliticsSystem politics, int civ, DecreeRequest candidate, DecreeForecast forecast)
    {
        TradeGood good = DecreeArgs.Good(candidate.Extra);
        if (good is TradeGood.None or >= TradeGood.Count) return Fix64.Zero;
        Fix64 amount = forecast.Goods;
        Fix64 unitValue = Fix64.Zero;
        string goodId = DecreeArgs.GoodId(good);
        for (int s = 0; s < politics.SettlementTotal; s++)
        {
            if (politics.SettlementCivOf(s) != civ || politics.IsFreeFolk(s) || politics.SettlementPopulationOf(s) <= 0) continue;
            unitValue = Fix64.Max(unitValue, politics.UnitValue(s, good));
            if (!DecreeArgs.Sell(candidate.Extra))
            {
                Fix64 need = politics.Economy.TradeNeed(s, goodId, 4);
                Fix64 stock = good == TradeGood.Food ? politics.Economy.FoodStores(s) : politics.Economy.GoodStock(s, goodId);
                amount = Fix64.Max(amount, Fix64.Max(Fix64.Zero, need - stock));
            }
        }
        // Buying access is strategically valuable at the full scarcity price; selling returns a conservative tenth of load value.
        Fix64 tradeValue = amount * unitValue;
        return DecreeArgs.Sell(candidate.Extra) ? tradeValue * Fix64.FromRatio(1, 10) : tradeValue;
    }

    private static Fix64 CorveeApproximation(PoliticsSystem politics, int settlement, DecreeForecast forecast)
    {
        // Walls matter here only when the accelerated project is predicted to finish before a belief-supported threat.
        int people = (uint)settlement < (uint)politics.SettlementTotal ? politics.SettlementPopulationOf(settlement) : 0;
        if (people <= 0 || forecast.ThreatEtaSeasons < 0 || forecast.ThreatMarginSeasons <= Fix64.Zero) return Fix64.Zero;
        return Fix64.FromInt(people) * (Fix64.One + forecast.ThreatMarginSeasons) / 10;
    }

    private Fix64 FoundFrontierBonus(DecreeRequest candidate)
        => candidate.Kind == DecreeKind.Found && unchecked((uint)candidate.Target) < (uint)_foundFrontierSites.Length &&
            _foundFrontierSites[candidate.Target]
            ? StrategistTuning.FoundFrontierContactBonus : Fix64.Zero;

    private static string Describe(DecreeRequest candidate, DecreeForecast forecast, Fix64 gross, Fix64 net,
        Fix64 secureTinBonus, Fix64 foundFrontierBonus)
        => $"{candidate.Kind} target {candidate.Target}; legal {forecast.Allowed}; success {forecast.SuccessPermille}/1000; " +
           $"people {forecast.People}, tiles {forecast.Tiles}, land weight {forecast.LandWeight}, Dominion {forecast.Dominion}, " +
           $"information {forecast.Information}, metal value {forecast.MetalValue}, goods {forecast.Goods}, " +
           $"walls ETA {forecast.WallEtaSeasons}, threat ETA {forecast.ThreatEtaSeasons}, margin {forecast.ThreatMarginSeasons}, relations {forecast.RelationsDelta}, " +
           $"seasons {forecast.Seasons}; gross {gross}, SecureTin route bonus {secureTinBonus}, " +
           $"recent frontier contact bonus {foundFrontierBonus}, mandate cost {forecast.Cost}, net EV {net}";

    private static int Compare(DecreeRequest a, DecreeRequest b)
    {
        int kind = a.Kind.CompareTo(b.Kind);
        return kind != 0 ? kind : a.Target.CompareTo(b.Target);
    }
}
