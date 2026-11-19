using System;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

public sealed partial class LivingEconomyModel
{
    private static readonly string[] ShareableMilitaryGoods =
    {
        "G-ARMS5", "G-ARMS4", "G-ARMS3", "G-ARMS2T", "G-ARMS2A", "G-ARMS1",
        "G-CROSSBOW", "G-LONGBOW", "G-BOW", "G-CHARIOT", "G-ENGINE",
    };

    /// <summary>
    /// Move only whole finished arms and vehicles between living sister settlements within the ordinary supply reach.
    /// Each donor retains the stock needed for its own fighting-age population and professional posts. Powder and cannons
    /// remain in the Arsenal staging flow, where siege orders can reserve and return them explicitly.
    /// </summary>
    private void ShareFinishedMilitaryGoods(int season)
    {
        for (int civ = 0; civ < _completedProjects.Length; civ++)
        {
            if (!IronSharingEnabled) continue;
            int reach = ShareReach(civ);
            if (reach < 0) continue;
            foreach (string good in ShareableMilitaryGoods)
            {
                int slot = SlotOf(good);
                if (slot == NoSlot) continue;
                for (int r = 0; r < _settlements.Count; r++)
                {
                    SettlementState receiver = _settlements[r];
                    if (receiver.Civilization != civ || receiver.Metal is null || IsFreeFolk(receiver) || Population(receiver.Id) <= 0) continue;
                    int need = MilitaryGoodNeed(receiver, good);
                    if (need <= 0) continue;
                    for (int d = 0; d < _settlements.Count && need > 0; d++)
                    {
                        SettlementState donor = _settlements[d];
                        if (!IronSister(receiver, donor, reach)) continue;
                        int stock = Stock(donor, slot).FloorToInt();
                        int reserve = MilitaryGoodReserve(donor, good, stock);
                        int surplus = Math.Max(0, stock - reserve);
                        int moved = Math.Min(need, surplus);
                        if (moved <= 0) continue;
                        Fix64 quantity = Fix64.FromInt(moved);
                        AddStock(donor, slot, -quantity);
                        AddStock(receiver, slot, quantity);
                        RecordSupplyTransfer(donor.Id, receiver.Id, good, quantity);
                        int distance = HexGrid.Distance(donor.SiteTile, receiver.SiteTile, _world.Width);
                        Fix64 haulDivisor = Done(civ, "TRN-05") ? Fix64.FromInt(5) : Fix64.One;
                        AddMetalStat(civ, StatMetalLabor, quantity * HaulPerLitreTile * distance / haulDivisor * FiefHorseHaulFactor(donor));
                        need -= moved;
                    }
                }
            }
        }
    }

    private int MilitaryGoodNeed(SettlementState settlement, string good)
    {
        int civ = settlement.Civilization;
        int fighters = FightingAgeCount(settlement);
        Fix64 stock = Stock(settlement, SlotOf(good));
        if (good is "G-ARMS5" or "G-ARMS4" or "G-ARMS3")
        {
            bool best = good == "G-ARMS5" && FiefChain && Done(civ, "MET-11") ||
                        good == "G-ARMS4" && Done(civ, "MET-10") && !(FiefChain && Done(civ, "MET-11")) ||
                        good == "G-ARMS3" && Done(civ, "WAR-08") && !Done(civ, "MET-10");
            if (best)
            {
                int soldiers = settlement.Metal?.IronSoldiersSupplied ?? 0;
                int professionalNeed = Math.Max(0, soldiers + (soldiers > 0 ? 1 : 0) - stock.FloorToInt());
                int baselineNeed = Math.Max(0, fighters + _bronzeConstants.SpareArms - TotalIssuedWeaponStock(settlement));
                return Math.Max(professionalNeed, baselineNeed);
            }
        }
        if (good is "G-ARMS1" or "G-ARMS2A" or "G-ARMS2T" or "G-ARMS3" or "G-ARMS4" or "G-ARMS5")
        {
            int other = TotalIssuedWeaponStock(settlement) - stock.FloorToInt();
            return Math.Max(0, fighters + _bronzeConstants.SpareArms - other - stock.FloorToInt());
        }
        if (good is "G-CROSSBOW" or "G-LONGBOW")
        {
            if (!FiefChain || !Done(civ, "WAR-11")) return 0;
            int bowStock = Stock(settlement, SlotOf("G-CROSSBOW")).FloorToInt() + Stock(settlement, SlotOf("G-LONGBOW")).FloorToInt();
            return Math.Max(0, LevyBowTarget(settlement) - bowStock);
        }
        if (good == "G-BOW")
        {
            if (!Done(civ, "WAR-03") || (FiefChain && Done(civ, "WAR-11"))) return 0;
            return Math.Max(0, LevyBowTarget(settlement) - stock.FloorToInt());
        }
        if (good == "G-CHARIOT")
            return Done(civ, "WAR-06") ? Math.Max(0, checked((int)(Population(settlement.Id) / 50)) - stock.FloorToInt()) : 0;
        if (good == "G-ENGINE")
        {
            int soldiers = settlement.Metal?.IronSoldiersSupplied ?? 0;
            return Done(civ, "WAR-09") ? Math.Max(0, Math.Min(2, soldiers / 4) - stock.FloorToInt()) : 0;
        }
        return 0;
    }

    private int MilitaryGoodReserve(SettlementState settlement, string good, int stock)
    {
        if (stock <= 0) return 0;
        if (good is "G-ARMS5" or "G-ARMS4" or "G-ARMS3")
        {
            if (good == "G-ARMS5" && FiefChain && Done(settlement.Civilization, "MET-11") ||
                              good == "G-ARMS4" && Done(settlement.Civilization, "MET-10") && !(FiefChain && Done(settlement.Civilization, "MET-11")) ||
                              good == "G-ARMS3" && Done(settlement.Civilization, "WAR-08") && !Done(settlement.Civilization, "MET-10"))
                return Math.Min(stock, Math.Max((settlement.Metal?.IronSoldiersSupplied ?? 0) + ((settlement.Metal?.IronSoldiersSupplied ?? 0) > 0 ? 1 : 0),
                    Math.Max(0, FightingAgeCount(settlement) + _bronzeConstants.SpareArms - (TotalIssuedWeaponStock(settlement) - stock))));
        }
        int slot = SlotOf(good);
        if (good is "G-ARMS1" or "G-ARMS2A" or "G-ARMS2T" or "G-ARMS3" or "G-ARMS4" or "G-ARMS5")
            return Math.Min(stock, Math.Max(0, FightingAgeCount(settlement) + _bronzeConstants.SpareArms - (TotalIssuedWeaponStock(settlement) - stock)));
        if (good is "G-CROSSBOW" or "G-LONGBOW")
            return Math.Min(stock, Math.Max(0, LevyBowTarget(settlement) - Stock(settlement, SlotOf(good == "G-CROSSBOW" ? "G-LONGBOW" : "G-CROSSBOW")).FloorToInt()));
        if (good == "G-BOW") return Math.Min(stock, LevyBowTarget(settlement));
        if (good == "G-CHARIOT") return Math.Min(stock, Population(settlement.Id) / 50);
        if (good == "G-ENGINE") return Math.Min(stock, Math.Min(2, (settlement.Metal?.IronSoldiersSupplied ?? 0) / 4));
        _ = slot;
        return 0;
    }

    private int TotalIssuedWeaponStock(SettlementState settlement)
    {
        int total = 0;
        foreach (string good in ShareableMilitaryGoods)
        {
            if (good is "G-CHARIOT" or "G-ENGINE") continue;
            int civ = settlement.Civilization;
            if (good is "G-CROSSBOW" or "G-LONGBOW" && (!FiefChain || !Done(civ, "WAR-11"))) continue;
            if (good == "G-BOW" && !Done(civ, "HNT-06")) continue;
            int slot = SlotOf(good);
            if (slot != NoSlot) total += Stock(settlement, slot).FloorToInt();
        }
        return total;
    }

    private int FightingAgeCount(SettlementState settlement)
    {
        int count = 0;
        foreach (FigureState figure in Residents(settlement.Id)) if (figure.Age >= 14) count++;
        return count;
    }

    internal void FixtureShareFinishedMilitaryGoods(int civ) => ShareFinishedMilitaryGoods(checked((int)_seasonsCompleted));
}
