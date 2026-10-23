using System;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// E3 food and fuel (WP7, docs/MVP/22 §9, 23 §6.2, §8, §11, 07d R4): the ard's 40% field limit and halved field labor with ox
/// pairs, canals over fields within 2 tiles of a river, fallow and manuring by the herds, dairying, horses, salt pans, bakeries,
/// the pass-through steps 09 gives carts, redistribution and market towns, GOV-04's redistribution of stores, and the
/// households' hearths, kilns and building upkeep that burn wood (with deadwood first). All of it applies only to settlements of
/// civilizations past KEY-3, so E0–E2 behavior is unchanged.
/// </summary>
public sealed partial class LivingEconomyModel
{
    private bool[] _nearRiver = Array.Empty<bool>();

    private void InitializeFood()
    {
        _nearRiver = new bool[_world.TileCount];
        int reach = _civic.CanalRiverDist;
        for (int tile = 0; tile < _world.TileCount; tile++)
            foreach (int t in RangeTiles(tile))
                if (_world.Tile(t).River && HexGrid.Distance(tile, t, _world.Width) <= reach) { _nearRiver[tile] = true; break; }
    }

    private bool E3Food(SettlementState s) => s.Civic is not null && CivInBronze(s.Civilization);

    /// <summary>Fields a settlement may keep: a quarter of the range in E2, 40% with the ard (AGR-05, 07d R4).</summary>
    private int E3FieldLimit(SettlementState s, int e2Limit)
    {
        if (!E3Food(s) || !HasProject(s.Civilization, "AGR-05")) return e2Limit;
        return RangeTiles(s.SiteTile).Length * 4 / 10;
    }

    private int CanalCandidates(SettlementState s)
    {
        if (s.Civic is not { } c || !HasProject(s.Civilization, "AGR-06")) return 0;
        int n = 0;
        foreach (int tile in s.FieldTiles) if (_nearRiver[tile] && !c.Canals.Contains(tile)) n++;
        return n;
    }

    private int NextCanalTile(SettlementState s)
    {
        if (s.Civic is not { } c || !HasProject(s.Civilization, "AGR-06")) return -1;
        foreach (int tile in s.FieldTiles) if (_nearRiver[tile] && !c.Canals.Contains(tile)) return tile;
        return -1;
    }

    private bool HasCanal(SettlementState s, int tile) => s.Civic is { } c && c.Canals.Contains(tile);

    /// <summary>A canal's yield (AGR-06): ×1.5, ×2 on Arid or Desert ground where soil then counts in full.</summary>
    private Fix64 IrrigationFactor(SettlementState s, int tile, out bool fullSoil)
    {
        fullSoil = false;
        if (!E3Food(s) || !HasCanal(s, tile)) return Fix64.One;
        WorldEconomyTile t = _world.Tile(tile);
        if ((ClimateBand)t.ClimateId == ClimateBand.Arid || (Biome)t.BiomeId == Biome.Desert)
        {
            fullSoil = true;
            return Fix64.FromInt(2);
        }
        Fix64 m = Modifier(s.Civilization, "irrigated_yield");
        return m > Fix64.Zero ? m : Fix64.One;
    }

    /// <summary>Work-seasons a year the fields need: 1 each, half where an ox pair ploughs (one pair per 4 fields, 6 with NRG-02).</summary>
    private Fix64 FieldLaborNeed(SettlementState s)
    {
        int fields = s.FieldTiles.Count;
        Fix64 need = Fix64.FromInt(fields);
        if (!E3Food(s) || !HasProject(s.Civilization, "AGR-05")) return need;
        int pairs = (s.DomesticCattle / 2).FloorToInt();
        int perPair = HasProject(s.Civilization, "NRG-02") ? _civic.ArdPerPairNrg02 : _civic.ArdPerPair;
        int ploughed = Math.Min(fields, pairs * perPair);
        return need - Fix64.FromInt(ploughed) * Fix64.Half;
    }

    /// <summary>
    /// Soil loss per harvest: the E2 modifiers, with AGR-07's ×0.4 only on the share of fields the herds manure (2 cattle or 6 sheep
    /// per 4 fields, docs/MVP/22 §9).
    /// </summary>
    private Fix64 SoilLossModifier(SettlementState s)
    {
        Fix64 mod = Modifier(s.Civilization, "soil_fertility_loss");
        if (!HasProject(s.Civilization, "AGR-07")) return mod;
        Fix64 agr07 = Fix64.One;
        foreach (EconomyEffect e in _content.Effects)
            if (e.Kind == "mod" && e.Stat == "soil_fertility_loss" && e.Value is { } v && _content.Projects[e.ProjectIndex].Id == "AGR-07") agr07 = v;
        if (agr07 <= Fix64.Zero || agr07 == Fix64.One) return mod;
        int fields = Math.Max(1, s.FieldTiles.Count);
        Fix64 cattle = s.DomesticCattle * 4 / (_civic.ManureCattle * fields);
        Fix64 sheep = s.DomesticSmallStock * 4 / (_civic.ManureSheep * fields);
        Fix64 share = E3Food(s) ? Fix64.Min(Fix64.One, Fix64.Max(cattle, sheep)) : Fix64.One;
        return mod / agr07 * (Fix64.One + share * (agr07 - Fix64.One));
    }

    /// <summary>How much of a flooded field's harvest is lost: all of it in E2; AGR-06 and NUM-06 halve the loss each.</summary>
    private Fix64 FloodLoss(SettlementState s)
    {
        if (!E3Food(s)) return Fix64.One;
        return Fix64.Clamp(Modifier(s.Civilization, "flood_loss"), Fix64.Zero, Fix64.One);
    }

    /// <summary>Pastures a settlement keeps: 2 in E2; in E3 up to 15% of its range, the share the herds need for the ard and manuring.</summary>
    private int PastureLimit(SettlementState s)
        => E3Food(s) && (HasProject(s.Civilization, "HUS-03") || HasProject(s.Civilization, "HUS-04"))
            ? Math.Max(2, RangeTiles(s.SiteTile).Length * 15 / 100) : 2;

    /// <summary>Dairying (HUS-05): half the herd's food again as milk, without slaughter.</summary>
    private Fix64 DairyIncome(SettlementState s)
    {
        if (!E3Food(s) || !HasProject(s.Civilization, "HUS-05")) return Fix64.Zero;
        Fix64 mult = Modifier(s.Civilization, "herd_food_without_slaughter");
        if (mult <= Fix64.One) return Fix64.Zero;
        return DomesticAnnualMeat(s) * (mult - Fix64.One) * FoodValue(Good.DriedFood, s);
    }

    private void Milk(SettlementState s, Fix64 meatThisSeason)
    {
        if (!E3Food(s) || !HasProject(s.Civilization, "HUS-05") || meatThisSeason <= Fix64.Zero) return;
        Fix64 mult = Modifier(s.Civilization, "herd_food_without_slaughter");
        if (mult <= Fix64.One) return;
        EnsureMetal(s).Goods[(int)MetalGood.Milk] += meatThisSeason * (mult - Fix64.One);
    }

    /// <summary>Horse taming (HUS-07): 1 ws an animal, at most 15% of a wild horse herd in range a year, 2 to a pasture.</summary>
    private Fix64 TameHorses(SettlementState s, Fix64 work, int year)
    {
        if (!E3Food(s) || !HasProject(s.Civilization, "HUS-07") || work <= Fix64.Zero) return work;
        CivicState c = s.Civic!;
        int room = _civic.HorsePerPasture * s.Structures[(int)Structure.Pasture] - c.Horses;
        if (room <= 0) return work;
        for (int i = 0; i < _wildHerds.Length && room > 0 && work >= _civic.HorseLabor; i++)
        {
            WildHerdState herd = _wildHerds[i];
            if (herd.Species != HerdSpecies.Horse || herd.CurrentAnimals < Fix64.One || herd.TamingYear != year ||
                !IsWithinRange(s.SiteTile, herd.CurrentTile)) continue;
            Fix64 allowed = herd.CurrentAnimals * _civic.HorseTameShare - herd.TamedThisYear;
            int tame = Math.Min(room, Math.Min((work / _civic.HorseLabor).FloorToInt(), allowed.FloorToInt()));
            if (tame <= 0) continue;
            herd.CurrentAnimals -= Fix64.FromInt(tame);
            herd.TamedThisYear += Fix64.FromInt(tame);
            c.Horses += tame;
            room -= tame;
            work -= _civic.HorseLabor * tame;
        }
        return work;
    }

    private bool HasTameableHorses(SettlementState s)
    {
        if (!E3Food(s) || !HasProject(s.Civilization, "HUS-07") || s.Structures[(int)Structure.Pasture] <= 0) return false;
        if (_civic.HorsePerPasture * s.Structures[(int)Structure.Pasture] <= s.Civic!.Horses) return false;
        int year = checked((int)(_seasonsCompleted / 4));
        foreach (WildHerdState herd in _wildHerds)
            if (herd.Species == HerdSpecies.Horse && herd.CurrentAnimals >= Fix64.One && herd.TamingYear == year &&
                herd.TamedThisYear < herd.CurrentAnimals * _civic.HorseTameShare && IsWithinRange(s.SiteTile, herd.CurrentTile)) return true;
        return false;
    }

    /// <summary>
    /// The settlement's food-value multiplier this season: Bakeries (FOD-08: grain +20% for 40 people each) apply to grain only and
    /// are kept apart; the rest are 09's cap steps that 10/23 give no stock mechanism (check_e3 passes them through): carts (TRN-05
    /// ×1.06, with a cart per 25 people), redistribution (GOV-04 ×1.04), market towns (TRD-05 ×1.05, and
    /// ×1.25 net in a Town on two or more routes).
    /// </summary>
    private Fix64 FoodValuePassThrough(SettlementState s, CivicState c)
    {
        int civ = s.Civilization;
        int pop = Math.Max(1, Population(s.Id));
        c.BakeryShare = HasProject(civ, "FOD-08") && c.Bakeries > 0
            ? Fix64.Min(Fix64.One, _civic.BakeryPeople * c.Bakeries / pop) : Fix64.Zero;
        if (!CohesionLive(s)) return Fix64.One;
        Fix64 mult = Fix64.One;
        if (HasProject(civ, "TRN-05") && s.Metal is { } m)
        {
            Fix64 carts = Fix64.Min(Fix64.One, m.Goods[(int)MetalGood.Cart] * 25 / pop);
            mult *= Fix64.One + Fix64.FromRatio(6, 100) * carts;
        }
        if (HasProject(civ, "GOV-04")) mult *= Fix64.FromRatio(104, 100);
        if (HasProject(civ, "TRD-05"))
        {
            mult *= Fix64.FromRatio(105, 100);
            if (c.Tier >= 1 && (TradeRoutes?.RoutesAt(s.Id) ?? 0) >= _civic.MarketTownRoutes) mult *= _civic.MarketTownFoodCap / Fix64.FromRatio(105, 100);
        }
        return mult;
    }

    /// <summary>The bakery and pass-through multipliers on a good's food value at a settlement (1 before KEY-3).</summary>
    private Fix64 SettlementFoodValueMult(SettlementState s, Good good)
    {
        if (s.Civic is not { } c) return Fix64.One;
        Fix64 mult = c.FoodValueMult;
        if (good == Good.Grain && c.BakeryShare > Fix64.Zero) mult *= Fix64.One + _civic.BakeryValue * c.BakeryShare;
        return mult;
    }

    /// <summary>
    /// GOV-04 Tribute & Redistribution (docs/MVP/23 §16): each season stores rebalance between own settlements within 20 tiles: a
    /// settlement with more than two seasons of food sends toward sisters with less than one.
    /// </summary>
    private void RedistributeFood()
    {
        for (int civ = 0; civ < _completedProjects.Length; civ++)
        {
            if (!CivInBronze(civ) || !HasProject(civ, "GOV-04")) continue;
            int reach = ShareReach(civ);
            for (int r = 0; r < _settlements.Count; r++)
            {
                SettlementState receiver = _settlements[r];
                if (!IsMember(receiver, civ) || receiver.Civic is null) continue;
                Fix64 need = FoodNeed(receiver, false);
                Fix64 deficit = need - FoodStores(receiver);
                for (int d = 0; d < _settlements.Count && deficit > Fix64.Zero; d++)
                {
                    SettlementState donor = _settlements[d];
                    if (d == r || !IsMember(donor, civ) || donor.Civic is null ||
                        HexGrid.Distance(donor.SiteTile, receiver.SiteTile, _world.Width) > reach) continue;
                    Fix64 surplus = FoodStores(donor) - FoodNeed(donor, false) * 2;
                    if (surplus <= Fix64.Zero) continue;
                    Fix64 move = Fix64.Min(deficit, surplus);
                    ConsumeStoredFoodValue(donor, move);
                    GiveGoods(r, move);
                    deficit -= move;
                }
            }
        }
    }

    /// <summary>Salt from coastal pans (MIN-06): 1 L a work-season on a Warm, Arid or Temperate coast in Summer, from craft labor.</summary>
    private void PanSalt(SettlementState s, MetalState m, MetalPlan plan, int season, ref Fix64 craftPool)
    {
        if (!Done(s.Civilization, "MIN-06") || season % 4 != 1 || craftPool <= Fix64.Zero) return;
        Fix64 net = plan.Want[MetalSlot(MetalGood.Salt)] - m.Goods[(int)MetalGood.Salt];
        if (net <= Fix64.Zero || BestRangeDeposit(s, MetalGood.Salt, out _) >= 0) return;
        bool pans = false;
        foreach (int tile in RangeTiles(s.SiteTile))
        {
            WorldEconomyTile t = _world.Tile(tile);
            if ((Biome)t.BiomeId == Biome.Coast && (ClimateBand)t.ClimateId is ClimateBand.Warm or ClimateBand.Arid or ClimateBand.Temperate) { pans = true; break; }
        }
        if (!pans) return;
        Fix64 rate = BaseRate("salt", Fix64.One);
        Fix64 litres = Fix64.Min(net, craftPool * rate);
        m.Goods[(int)MetalGood.Salt] += litres;
        craftPool -= litres / rate;
    }

    // ---- wood for hearths, kilns and building (the fuel demand WP6 found missing, 07d §14) ----------------

    /// <summary>
    /// Wood a settlement burns or builds with this season beyond its one E2 hearth (metal_sim.py's non-metal demand, docs/MVP/10 §5):
    /// a hearth (1.5 L a season, 3 in Winter) for each 35 people beyond the first, kiln firing (pottery and lime, 0.2 L a person a
    /// year where a kiln stands), building and repairs (0.15 L a person a year) and a Bakery's 2 L a season.
    /// </summary>
    private Fix64 E3FuelNeed(SettlementState s, bool winter)
    {
        if (!E3Food(s)) return Fix64.Zero;
        int pop = Population(s.Id);
        if (pop == 0) return Fix64.Zero;
        Fix64 households = Fix64.Max(Fix64.One, Fix64.FromInt(pop) / _civic.HearthPeople);
        Fix64 hearth = (households - Fix64.One) * (winter ? Fix64.FromInt(3) : Fix64.FromRatio(3, 2));
        Fix64 kiln = s.Structures[(int)Structure.Kiln] > 0 ? Fix64.FromInt(pop) * _civic.KilnWood / 4 : Fix64.Zero;
        Fix64 build = Fix64.FromInt(pop) * _civic.BuildWood / 4;
        Fix64 bakery = Fix64.FromInt(2 * (s.Civic?.Bakeries ?? 0));
        return hearth + kiln + build + bakery;
    }

    /// <summary>
    /// Burns the season's E3 fuel: deadwood of the range's forest stands first (0.5 L a tile a season, docs/MVP/23 §7.4, never
    /// stocked; a stand's deadwood serves one settlement a season), then cut wood from the stores.
    /// </summary>
    private void BurnE3Fuel(SettlementState s, bool winter, bool[] deadwoodUsed)
    {
        Fix64 need = E3FuelNeed(s, winter);
        if (need <= Fix64.Zero) return;
        Fix64 perTile = _civic.Deadwood / 4;
        foreach (int tile in RangeTiles(s.SiteTile))
        {
            if (need <= Fix64.Zero) break;
            if (!IsForestTile(tile) || deadwoodUsed[tile] || _woodStock[tile] < _bronzeConstants.ClearedBelow) continue;
            deadwoodUsed[tile] = true;
            need -= Fix64.Min(need, perTile);
        }
        Fix64 fromStores = Fix64.Min(need, Fix64.Max(Fix64.Zero, s.Stores[(int)Good.Wood]));
        s.Stores[(int)Good.Wood] -= fromStores;
        AddMetalStat(s.Civilization, StatOtherWood, fromStores);
    }
}
