using System;
using System.Collections.Generic;
using CivSim.Sim.Core;
using CivSim.Sim.Ai.Forecast;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

/// <summary>One of the civilization's own settlements, exactly (docs/MVP/14 §3.1: "Self: exact").</summary>
public sealed class OwnSettlement
{
    public int Index, Tile, Population, Adults, Fighters, Warriors, IdleFighters;
    public Fix64 DefendingStrength, AttackingStrength, FoodStores, FoodSecurity, LateWinterForecast, Cohesion;
    public Defenses Defenses;
}

/// <summary>What one civilization knows about its dealings with another: mutual knowledge, so exact.</summary>
public sealed class PairView
{
    public int Civ;
    public bool Aware, Contacted, AtWar, Allied, InTruce, NonAggressionPact, TinEmbargoed;
    public int Relations, Reputation;
    public int LastGiftSeason, LastBarterSeason, LastRaidOnThemSeason, LastRaidOnUsSeason;
}

/// <summary>A currently gathered levy host for one target, visible exactly to its owner (docs/MVP/22 §4.3).</summary>
public sealed class OwnLevy
{
    public int TargetSettlement;
    public readonly List<Fighter> Members = new();
    public readonly Dictionary<int, int> Departures = new();
}

/// <summary>The civilization's own state, exact, rebuilt every season before it decides (docs/MVP/14 §3.1).</summary>
public sealed class OwnView
{
    public int Civ, Era, Season;
    public long Tick;
    public int Population, Adults, ClaimedTiles, BeaconAllowance, ReachTiles;
    /// <summary>Exact E3 inputs for the forecast; contains only this civilization's own state.</summary>
    public ForecastBronzeStart Bronze { get; } = new();
    /// <summary>Exact access held by this civilization; own state is not fogged (docs/MVP/14 §3.1).</summary>
    public bool TinAccess, ArsenicAccess;
    public bool RangedWeapon;
    public Fix64 InsightPerYear, Dominion, Legacy;
    public Posture Posture;
    public readonly List<OwnSettlement> Settlements = new();
    public readonly List<OwnLevy> Levies = new();
    public readonly List<int> Beacons = new();
    public PairView[] Others = Array.Empty<PairView>();
    /// <summary>Own parties on the road: raids, assaults, gifts and barter, by kind.</summary>
    public int RaidsOnTheRoad, AssaultsOnTheRoad, GiftsOnTheRoad, Occupations;

    public OwnSettlement? SettlementAt(int index)
    {
        foreach (OwnSettlement s in Settlements) if (s.Index == index) return s;
        return null;
    }
}

/// <summary>Builds the own-state view from the true state of one civilization only.</summary>
internal static class OwnViewBuilder
{
    public static void Build(OwnView view, int civ, long tick, LivingEconomyModel economy, ResearchSystem research, PoliticsSystem politics)
    {
        view.Civ = civ;
        view.Tick = tick;
        view.Season = politics.Season;
        view.Era = research.Era(civ);
        view.Posture = politics.PostureOf(civ);
        view.BeaconAllowance = politics.BeaconAllowance(civ);
        view.ReachTiles = politics.ReachOf(civ);
        view.TinAccess = economy.CivHasMetalAccess(civ, MetalAccessKind.Tin);
        view.ArsenicAccess = economy.CivHasMetalAccess(civ, MetalAccessKind.Arsenic);
        politics.WeaponOf(civ, out view.RangedWeapon);
        view.InsightPerYear = research.InsightPerYear(civ);
        view.Legacy = research.Legacy(civ) + politics.Territory.Dominion(civ);
        view.Dominion = politics.Territory.Dominion(civ);
        view.ClaimedTiles = politics.Territory.OwnedTiles(civ);
        view.Settlements.Clear();
        view.Levies.Clear();
        view.Beacons.Clear();
        foreach (int b in politics.BeaconsOf(civ)) view.Beacons.Add(b);
        var ownSettlementById = new Dictionary<int, int>();
        for (int settlement = 0; settlement < politics.SettlementTotal; settlement++)
            if (politics.SettlementCivOf(settlement) == civ && !politics.IsFreeFolk(settlement))
                ownSettlementById[economy.SettlementLite(settlement).Id] = settlement;
        foreach (Levy levy in politics.Levies)
        {
            if (levy.Civ != civ || levy.UntilSeason <= politics.Season) continue;
            var ownLevy = new OwnLevy { TargetSettlement = levy.TargetSettlement };
            for (int i = 0; i < levy.Members.Count; i++)
            {
                long id = levy.Members[i];
                if (!economy.TryFigure(id, out FigureView figure) || economy.IsWounded(id)) continue;
                ownLevy.Members.Add(new Fighter(id, levy.Strengths[i]));
                if (ownSettlementById.TryGetValue(figure.SettlementId, out int settlement))
                    ownLevy.Departures[settlement] = ownLevy.Departures.GetValueOrDefault(settlement) + 1;
            }
            view.Levies.Add(ownLevy);
        }
        view.Population = 0;
        view.Adults = 0;
        ForecastBronzeStart bronze = view.Bronze;
        bronze.Active = research.Era(civ) >= 3;
        bronze.CopperAccess = economy.CivHasMetalAccess(civ, MetalAccessKind.Copper);
        bronze.TinAccess = economy.CivHasMetalAccess(civ, MetalAccessKind.Tin);
        bronze.ArsenicAccess = economy.CivHasMetalAccess(civ, MetalAccessKind.Arsenic);
        bronze.CopperSource = economy.CivHasOreSource(civ, DepositKind.CopperOre);
        bronze.TinSource = economy.CivHasOreSource(civ, DepositKind.Tin);
        bronze.ArsenicSource = economy.CivHasOreSource(civ, DepositKind.ArsenicalCopper);
        bronze.BestCastTier = bronze.Kit8 = bronze.Kit9 = bronze.Kit10 = 0;
        bronze.WalledSettlements = bronze.HillfortSettlements = 0;
        bronze.MeanCohesion = bronze.MeanOverreach = bronze.MeanSpanPenalty = Fix64.Zero;
        bronze.SettlementCount = 0;
        bronze.SpanPenaltyPerExcess = Fix64.Zero;
        bronze.Settlements.Clear();
        bronze.Compacts.Clear();
        bronze.Mandate = research.Mandate(civ);
        bronze.MandateUnlocked = research.MandateUnlocked(civ);
        bronze.MandateCap = research.MandateCap(civ);
        bronze.MandateRecharge = research.MandateRechargeSeasons(civ);
        bronze.MandateRechargeRaw = 0;
        for (int p = 0; p < research.Catalog.Count; p++)
        {
            if (!research.IsDone(civ, p)) continue;
            foreach (EffectDef effect in research.Catalog.Projects[p].Effects)
                if (effect.Kind == EffectKind.Mandate) bronze.MandateRechargeRaw += effect.MandateRecharge;
        }
        bronze.MandateCounter = bronze.Mandate < bronze.MandateCap
            ? Math.Max(0, bronze.MandateRecharge - research.MandateSeasonsToNext(civ)) : 0;
        bronze.MandateRechargeMin = research.Catalog.MandateRechargeMin;
        bronze.MetalOutputRates = economy.MetalOutputRate(civ);
        bronze.AdminSpan = research.AdminSpan(civ);
        bronze.AdminReduction = research.AdminReduction(civ);
        int totalOverreach = 0, totalSpanPenalty = 0, totalKits8 = 0, totalKits9 = 0, totalKits10 = 0;
        for (int s = 0; s < politics.SettlementTotal; s++)
        {
            if (politics.SettlementCivOf(s) != civ || politics.IsFreeFolk(s)) continue;
            var lite = economy.SettlementLite(s);
            SettlementMilitary m = politics.MilitaryOf(s);
            int pop = politics.SettlementPopulationOf(s);
            MetalReadout metal = economy.Metal(s);
            var fightingAge = new List<LivingEconomyModel.FighterRecord>();
            economy.ListFightingAge(s, fightingAge);
            int idleFighters = 0;
            foreach (LivingEconomyModel.FighterRecord fighter in fightingAge)
                if (!fighter.Wounded && economy.IsIdle(fighter.Id)) idleFighters++;
            bronze.BestCastTier = Math.Max(bronze.BestCastTier, metal.CastTier);
            totalKits8 += metal.Kits8; totalKits9 += metal.Kits9; totalKits10 += metal.Kits10;
            (int overreach, int spanPenalty) = economy.CohesionPressures(s);
            bronze.MeanCohesion += economy.SettlementCohesion(s);
            totalOverreach += overreach; totalSpanPenalty += spanPenalty;
            SettlementTier tier = economy.Tier(s);
            bronze.Settlements.Add(new ForecastBronzeSettlement(s, politics.SettlementTileOf(s), (int)tier,
                economy.SettlementCohesion(s), overreach, spanPenalty, m.Defenses.Hillfort, m.Defenses.StoneWalls));
            if (m.Defenses.Hillfort) bronze.HillfortSettlements++;
            if (m.Defenses.StoneWalls) bronze.WalledSettlements++;
            bronze.SettlementCount++;
            view.Settlements.Add(new OwnSettlement
            {
                Index = s, Tile = politics.SettlementTileOf(s), Population = pop, Adults = m.Adults, Fighters = m.Fighters, IdleFighters = idleFighters,
                Warriors = m.Warriors, DefendingStrength = m.DefendingStrength, AttackingStrength = m.AttackingStrength,
                FoodStores = m.FoodStores, FoodSecurity = lite.LastFoodSecurity, LateWinterForecast = lite.LateWinterFoodForecast,
                Cohesion = economy.SettlementCohesion(s), Defenses = m.Defenses,
            });
            view.Population += pop;
            view.Adults += m.Adults;
        }
        bronze.Kit8 = totalKits8; bronze.Kit9 = totalKits9; bronze.Kit10 = totalKits10;
        if (bronze.SettlementCount > 0)
        {
            bronze.MeanCohesion /= bronze.SettlementCount;
            bronze.MeanOverreach = Fix64.FromRatio(totalOverreach, bronze.SettlementCount);
            bronze.MeanSpanPenalty = Fix64.FromRatio(totalSpanPenalty, bronze.SettlementCount);
        }
        bronze.SpanPenaltyPerExcess = bronze.SettlementCount > research.AdminSpan(civ)
            ? Fix64.FromRatio(totalSpanPenalty, bronze.SettlementCount) : Fix64.Zero;
        foreach (Compact compact in politics.Compacts)
        {
            if (!compact.Active || !compact.Involves(civ)) continue;
            int partner = compact.SellerCiv == civ ? compact.BuyerCiv : compact.SellerCiv;
            if (partner < 0) partner = compact.SellerFolk >= 0 ? compact.SellerFolk : compact.BuyerFolk;
            bronze.Compacts.Add(new ForecastBronzeCompact(compact.Id, partner, compact.Good,
                compact.BuyerCiv == civ, Math.Max(0, politics.Season - compact.SinceSeason), compact.LastVolume, compact.Delivered));
        }
        int civs = research.CivCount;
        if (view.Others.Length != civs) view.Others = new PairView[civs];
        Diplomacy d = politics.Diplomacy;
        for (int o = 0; o < civs; o++)
        {
            PairView p = view.Others[o] ??= new PairView();
            p.Civ = o;
            if (o == civ) continue;
            p.Aware = d.Aware(civ, o);
            p.Contacted = d.Contacted(civ, o);
            p.AtWar = d.AtWar(civ, o);
            p.Allied = d.Allied(civ, o);
            p.InTruce = d.InTruce(civ, o, view.Season);
            p.NonAggressionPact = politics.HasPact(civ, o);
            p.TinEmbargoed = politics.Embargoes(civ, o, TradeGood.Tin);
            p.Relations = d.Relations(civ, o);
            p.Reputation = d.Reputation(o);
            p.LastGiftSeason = d.LastGift(civ, o);
            p.LastBarterSeason = d.LastBarter(civ, o);
            p.LastRaidOnThemSeason = d.LastRaidBy(civ, o);
            p.LastRaidOnUsSeason = d.LastRaidBy(o, civ);
        }
        view.RaidsOnTheRoad = view.AssaultsOnTheRoad = view.GiftsOnTheRoad = view.Occupations = 0;
        foreach (Expedition e in politics.Expeditions)
        {
            if (e.Civ != civ) continue;
            if (e.Kind == ExpeditionKind.Raid) view.RaidsOnTheRoad++;
            else if (e.Kind == ExpeditionKind.Assault) view.AssaultsOnTheRoad++;
            else if (e.Kind == ExpeditionKind.Gift) view.GiftsOnTheRoad++;
        }
        foreach (Occupation o in politics.Occupations) if (o.Civ == civ) view.Occupations++;
    }
}
