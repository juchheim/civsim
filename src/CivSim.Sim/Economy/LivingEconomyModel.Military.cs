using System;
using System.Collections.Generic;
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

    private const int MilitaryInputBufferBatches = 3;

    private sealed class MilitaryInputDemand
    {
        public int[] Slots { get; init; } = Array.Empty<int>();
        public Fix64 Quantity { get; set; }
    }

    /// <summary>
    /// After finished arms are shared, send a small working buffer of the inputs the next season's weapon plan needs to
    /// reachable producers. This uses existing stocks only. It routes the metal and common materials that the Bronze
    /// store-sharing list does not cover (notably wood, leather/hide, cord, wrought iron, steel and crucible steel).
    /// </summary>
    private void ShareMilitaryProductionInputs(int season, int onlyCiv = -1)
    {
        if (!FiefChain || !IronSharingEnabled) return;

        for (int civ = 0; civ < _completedProjects.Length; civ++)
        {
            if (onlyCiv >= 0 && civ != onlyCiv) continue;
            int reach = ShareReach(civ);
            if (reach < 0) continue;

            var demandsBySettlement = new List<MilitaryInputDemand>[_settlements.Count];
            var reservesBySettlement = new Fix64[_settlements.Count][];
            var receivers = new List<SettlementState>();
            for (int i = 0; i < _settlements.Count; i++)
            {
                SettlementState settlement = _settlements[i];
                if (settlement.Civilization != civ || settlement.Metal is null || IsFreeFolk(settlement) || Population(settlement.Id) <= 0)
                    continue;

                List<MilitaryInputDemand> demands = MilitaryProductionInputDemands(settlement);
                if (demands.Count == 0) continue;
                demandsBySettlement[settlement.Id] = demands;
                reservesBySettlement[settlement.Id] = new Fix64[BowSlotBase + (int)BowGood.Count];
                AccumulateMilitaryInputReserve(settlement, demands, reservesBySettlement[settlement.Id]);
                receivers.Add(settlement);
            }

            // Put the least-equipped producer first; stable settlement id resolves equal coverage.
            receivers.Sort((a, b) =>
            {
                int aFighters = Math.Max(1, FightingAgeCount(a) + _bronzeConstants.SpareArms);
                int bFighters = Math.Max(1, FightingAgeCount(b) + _bronzeConstants.SpareArms);
                long aCovered = (long)TotalIssuedWeaponStock(a) * bFighters;
                long bCovered = (long)TotalIssuedWeaponStock(b) * aFighters;
                int coverage = aCovered.CompareTo(bCovered);
                return coverage != 0 ? coverage : a.Id.CompareTo(b.Id);
            });

            foreach (SettlementState receiver in receivers)
            {
                List<MilitaryInputDemand> demands = demandsBySettlement[receiver.Id]!;
                foreach (MilitaryInputDemand demand in demands)
                {
                    Fix64 held = Fix64.Zero;
                    foreach (int slot in demand.Slots) held += Stock(receiver, slot);
                    Fix64 need = Fix64.Max(Fix64.Zero, demand.Quantity - held);
                    while (need > Epsilon)
                    {
                        SettlementState? bestDonor = null;
                        int bestSlot = NoSlot;
                        Fix64 bestSurplus = Fix64.Zero;
                        int bestDistance = int.MaxValue;
                        for (int d = 0; d < _settlements.Count; d++)
                        {
                            SettlementState donor = _settlements[d];
                            if (donor == receiver || donor.Civilization != civ || donor.Metal is null || IsFreeFolk(donor) ||
                                Population(donor.Id) <= 0) continue;
                            int distance = HexGrid.Distance(donor.SiteTile, receiver.SiteTile, _world.Width);
                            if (distance > reach) continue;

                            foreach (int slot in demand.Slots)
                            {
                                Fix64 stock = Stock(donor, slot);
                                Fix64 plannedReserve = reservesBySettlement[donor.Id] is { } donorReserves && slot < donorReserves.Length
                                    ? donorReserves[slot] : Fix64.Zero;
                                Fix64 surplus = stock - MilitaryInputReserve(donor, slot, plannedReserve, season);
                                if (surplus <= Epsilon) continue;
                                if (surplus > bestSurplus || surplus == bestSurplus &&
                                    (distance < bestDistance || distance == bestDistance &&
                                     (bestDonor is null || donor.Id < bestDonor.Id || donor.Id == bestDonor.Id && slot < bestSlot)))
                                {
                                    bestDonor = donor;
                                    bestSlot = slot;
                                    bestSurplus = surplus;
                                    bestDistance = distance;
                                }
                            }
                        }
                        if (bestDonor is null || bestSlot == NoSlot) break;

                        Fix64 moved = Fix64.Min(need, bestSurplus);
                        AddStock(bestDonor, bestSlot, -moved);
                        AddStock(receiver, bestSlot, moved);
                        RecordSupplyTransfer(bestDonor.Id, receiver.Id, GoodIdForSlot(bestSlot), moved);
                        if (bestSlot == (int)Good.Wood)
                        {
                            RecordLocalWoodUse(bestDonor, moved, "other");
                            RecordLocalWoodInflow(receiver, moved, chop: false);
                        }
                        Fix64 haulDivisor = Done(civ, "TRN-05") ? Fix64.FromInt(5) : Fix64.One;
                        AddMetalStat(civ, StatMetalLabor, moved * HaulPerLitreTile * bestDistance / haulDivisor * FiefHorseHaulFactor(bestDonor));
                        need -= moved;
                    }
                }
            }
        }
    }

    private List<MilitaryInputDemand> MilitaryProductionInputDemands(SettlementState settlement)
    {
        var demands = new List<MilitaryInputDemand>();
        var byInputs = new Dictionary<string, MilitaryInputDemand>(StringComparer.Ordinal);

        int AddOutput(string goodId, int batchLimit)
        {
            if (batchLimit <= 0) return 0;
            CompiledRecipe? recipe = ProducerOf(SlotOf(goodId));
            if (recipe is null || !RecipeAvailable(settlement, recipe)) return 0;
            int batches = Math.Min(batchLimit, MilitaryGoodNeed(settlement, goodId));
            if (batches <= 0) return 0;

            for (int input = 0; input < recipe.InputSlots.Length; input++)
            {
                int[] slots = recipe.InputSlots[input];
                if (slots.Length == 0) continue;
                string key = string.Join(",", slots);
                if (!byInputs.TryGetValue(key, out MilitaryInputDemand? demand))
                {
                    demand = new MilitaryInputDemand { Slots = slots, Quantity = Fix64.Zero };
                    byInputs.Add(key, demand);
                    demands.Add(demand);
                }
                demand.Quantity += InputQuantity(settlement, recipe, input) * Fix64.FromInt(batches);
            }
            return batches;
        }

        if (settlement.Metal is { Forges: > 0 })
        {
            int armsBuffer = MilitaryInputBufferBatches;
            string? advancedArms = IronChain && Done(settlement.Civilization, "WAR-08")
                ? FiefChain && Done(settlement.Civilization, "MET-11") ? "G-ARMS5"
                    : Done(settlement.Civilization, "MET-10") ? "G-ARMS4" : "G-ARMS3"
                : null;
            if (advancedArms is not null)
            {
                int reserved = AddOutput(advancedArms, Math.Min(2, armsBuffer));
                armsBuffer -= reserved;
            }
            // Match the locally producible Bronze path instead of blindly preferring tin because its recipe happens
            // to precede arsenical in the registry. If Iron already has a professional upgrade target, retain one
            // buffer batch for ordinary arms so a constrained high-tier input cannot starve baseline coverage.
            string? baselineArms = null;
            int castTier = BestCastTier(settlement);
            if (Done(settlement.Civilization, "WAR-05"))
                baselineArms = castTier switch
                {
                    10 => "G-ARMS2T",
                    9 => "G-ARMS2A",
                    8 => "G-ARMS1",
                    _ when Done(settlement.Civilization, "MET-04") => "G-ARMS2A",
                    _ when Done(settlement.Civilization, "MET-05") => "G-ARMS2T",
                    _ when Done(settlement.Civilization, "MET-03") => "G-ARMS1",
                    _ => null,
                };
            else if (Done(settlement.Civilization, "MET-03"))
                baselineArms = "G-ARMS1";
            if (baselineArms is not null && armsBuffer > 0)
                armsBuffer -= AddOutput(baselineArms, advancedArms is null ? armsBuffer : Math.Min(1, armsBuffer));
            if (advancedArms is not null && armsBuffer > 0)
                AddOutput(advancedArms, armsBuffer);
            AddOutput("G-CHARIOT", 1);
        }

        if (FiefChain && Done(settlement.Civilization, "WAR-11"))
        {
            int bowNeed = LevyBowDeficit(settlement);
            int crossbows = Math.Min(MilitaryInputBufferBatches, bowNeed);
            AddOutput("G-CROSSBOW", crossbows);
            bowNeed -= crossbows;
            AddOutput("G-LONGBOW", Math.Min(MilitaryInputBufferBatches, bowNeed));
        }

        return demands;
    }

    private void AccumulateMilitaryInputReserve(SettlementState settlement, List<MilitaryInputDemand> demands, Fix64[] reserve)
    {
        foreach (MilitaryInputDemand demand in demands)
        {
            Fix64 left = demand.Quantity;
            // Reserve the actual alternative the settlement already has, so it keeps its own input buffer without
            // needlessly reserving leather when its stock is all hide (or vice versa).
            while (left > Epsilon)
            {
                int inputSlot = NoSlot;
                Fix64 available = Fix64.Zero;
                foreach (int candidate in demand.Slots)
                {
                    Fix64 candidateAvailable = Stock(settlement, candidate) - reserve[candidate];
                    if (candidateAvailable > available)
                    {
                        inputSlot = candidate;
                        available = candidateAvailable;
                    }
                }
                if (inputSlot == NoSlot || available <= Epsilon) break;
                Fix64 amount = Fix64.Min(left, available);
                reserve[inputSlot] += amount;
                left -= amount;
            }
        }
    }

    private Fix64 MilitaryInputReserve(SettlementState settlement, int slot, Fix64 plannedReserve, int season)
    {
        Fix64 reserve = plannedReserve;
        if (slot == (int)Good.Wood)
            reserve = Fix64.Max(reserve, DefenseWoodDonorReserve(settlement, season));
        else if (slot == (int)Good.Hide || slot == (int)Good.Leather || slot == (int)Good.Cord || slot == MetalSlot(MetalGood.Wheel))
            reserve = Fix64.Max(reserve, Fix64.FromInt(slot == MetalSlot(MetalGood.Wheel) ? 2 : 1));
        else if (slot == MetalSlot(MetalGood.Bronze) || slot == MetalSlot(MetalGood.ArsenicalBronze) ||
                 slot == IronSlot(IronGood.Wrought) || slot == IronSlot(IronGood.Steel) || slot == FiefSlot(FiefGood.CrucibleSteel))
            reserve = Fix64.Max(reserve, Fix64.Half);
        return Fix64.Min(Stock(settlement, slot), reserve);
    }

    private string GoodIdForSlot(int slot)
    {
        if (slot < (int)Good.Count) return GoodId((Good)slot);
        if (slot < IronSlotBase) return MetalGoodId((MetalGood)(slot - (int)Good.Count));
        if (slot < FiefSlotBase) return IronGoodId((IronGood)(slot - IronSlotBase));
        if (slot < BowSlotBase) return FiefGoodId((FiefGood)(slot - FiefSlotBase));
        return BowGoodId((BowGood)(slot - BowSlotBase));
    }

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
    internal void FixtureShareMilitaryProductionInputs(int civ) => ShareMilitaryProductionInputs(checked((int)_seasonsCompleted) + 1, civ);
    internal int FixtureMilitaryProductionInputDemandCount(int settlementIndex)
        => MilitaryProductionInputDemands(_settlements[settlementIndex]).Count;
}
