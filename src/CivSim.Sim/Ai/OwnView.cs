using System;
using System.Collections.Generic;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;

namespace CivSim.Sim.Ai;

/// <summary>One of the civilization's own settlements, exactly (docs/MVP/14 §3.1: "Self: exact").</summary>
public sealed class OwnSettlement
{
    public int Index, Tile, Population, Adults, Fighters, Warriors;
    public Fix64 DefendingStrength, AttackingStrength, FoodStores, FoodSecurity, LateWinterForecast, Cohesion;
    public Defenses Defenses;
}

/// <summary>What one civilization knows about its dealings with another: mutual knowledge, so exact.</summary>
public sealed class PairView
{
    public int Civ;
    public bool Aware, Contacted, AtWar, Allied, InTruce;
    public int Relations, Reputation;
    public int LastGiftSeason, LastBarterSeason, LastRaidOnThemSeason, LastRaidOnUsSeason;
}

/// <summary>The civilization's own state, exact, rebuilt every season before it decides (docs/MVP/14 §3.1).</summary>
public sealed class OwnView
{
    public int Civ, Era, Season;
    public long Tick;
    public int Population, Adults, ClaimedTiles, BeaconAllowance, ReachTiles;
    public bool RangedWeapon;
    public Fix64 InsightPerYear, Dominion, Legacy;
    public Posture Posture;
    public readonly List<OwnSettlement> Settlements = new();
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
        politics.WeaponOf(civ, out view.RangedWeapon);
        view.InsightPerYear = research.InsightPerYear(civ);
        view.Legacy = research.Legacy(civ) + politics.Territory.Dominion(civ);
        view.Dominion = politics.Territory.Dominion(civ);
        view.ClaimedTiles = politics.Territory.OwnedTiles(civ);
        view.Settlements.Clear();
        view.Beacons.Clear();
        foreach (int b in politics.BeaconsOf(civ)) view.Beacons.Add(b);
        view.Population = 0;
        view.Adults = 0;
        for (int s = 0; s < politics.SettlementTotal; s++)
        {
            if (politics.SettlementCivOf(s) != civ || politics.IsFreeFolk(s)) continue;
            var lite = economy.SettlementLite(s);
            SettlementMilitary m = politics.MilitaryOf(s);
            int pop = politics.SettlementPopulationOf(s);
            view.Settlements.Add(new OwnSettlement
            {
                Index = s, Tile = politics.SettlementTileOf(s), Population = pop, Adults = m.Adults, Fighters = m.Fighters,
                Warriors = m.Warriors, DefendingStrength = m.DefendingStrength, AttackingStrength = m.AttackingStrength,
                FoodStores = m.FoodStores, FoodSecurity = lite.LastFoodSecurity, LateWinterForecast = lite.LateWinterFoodForecast,
                Cohesion = economy.SettlementCohesion(s), Defenses = m.Defenses,
            });
            view.Population += pop;
            view.Adults += m.Adults;
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
