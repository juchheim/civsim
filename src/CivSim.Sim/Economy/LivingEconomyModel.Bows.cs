using System;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// The levy's bows (WAR-11, docs/MVP/36): crossbows and longbows made from surplus wood, cord and (for a crossbow) a little iron, held in a
/// settlement's stores as one weapon per levy fighter, and (from WAR-03) G-BOW kept for the levy as well as the hunt. Everything here is inert unless the
/// match plays the Faith &amp; Fief chapter (<see cref="FiefChain"/>): the WAR-11 goods live in <c>SettlementState.FiefGoods</c>, outside the arrays an Iron checksum and snapshot cover, and the
/// two recipes join the planner only in a Fief match. Arrows and bolts are folded into the weapon (36 §2); combat reads the stock through
/// <see cref="GoodStock"/>.
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>Goods that exist only in the Faith &amp; Fief chapter. Append only; the order is the slot order in snapshots.</summary>
    private enum FiefGood : byte
    {
        Crossbow, Longbow,
        Count
    }

    private static string FiefGoodId(FiefGood good) => good switch
    {
        FiefGood.Crossbow => "G-CROSSBOW", FiefGood.Longbow => "G-LONGBOW",
        _ => "",
    };

    /// <summary>Fief goods take slots after the Iron goods, so every Bronze and Iron slot keeps its number.</summary>
    private static int FiefSlotBase => SlotCount + (int)IronGood.Count;
    private static int FiefSlot(FiefGood good) => FiefSlotBase + (int)good;

    private const uint FiefGoodsSnapshotMagic = 0x53574F42; // "BOWS" little-endian
    private const ulong FiefGoodsTag = 0x4C45_5659_424F_5753; // "LEVYBOWS"

    /// <summary>Every compiled recipe in content order, the Fief recipes included: the planner's list in a Fief match.</summary>
    private CompiledRecipe[] _allFiefRecipes = Array.Empty<CompiledRecipe>();

    /// <summary>At most this many bows of both kinds a settlement holds for its levy (36 §4, provisional).</summary>
    internal const int LevyBowCap = 40;
    /// <summary>No new bows in a season the settlement is short of food (the same 0.8 line that halves the metal chain's cap).</summary>
    private static readonly Fix64 LevyBowFoodFloor = Fix64.FromRatio(8, 10);
    /// <summary>Share of the cut wood above the hearth-and-building reserve that bow-making may claim in one season.</summary>
    private static readonly Fix64 LevyBowWoodShare = Fix64.Half;

    /// <summary>
    /// The levy's standing want (36 §4): with WAR-11 known, one bow for every resident aged 14 or more who is not a professional soldier, up to
    /// <see cref="LevyBowCap"/>. It is a secondary want (after kits, iron arms and the E3 goods' push), built only in a season of enough food,
    /// and from no more than half the cut wood above the reserve. Crossbows are wanted first, as many as the forge and the iron already in store
    /// can make; the rest of the season's want is longbows, which need no iron. Bows already held of either kind count against the target.
    /// </summary>
    private void FiefBowWants(SettlementState s, MetalState m, MetalPlan plan)
    {
        if (!FiefChain || !Done(s.Civilization, "WAR-11") || s.FoodSecurity < LevyBowFoodFloor) return;
        int target = LevyBowTarget(s);
        Fix64 crossbows = Stock(s, FiefSlot(FiefGood.Crossbow)), longbows = Stock(s, FiefSlot(FiefGood.Longbow));
        Fix64 deficit = Fix64.FromInt(target) - crossbows - longbows;
        if (deficit <= Fix64.Zero) return;

        Fix64 spareWood = Fix64.Max(Fix64.Zero, s.Stores[(int)Good.Wood] - WoodReserve) * LevyBowWoodShare;
        CompiledRecipe? crossbowRecipe = ProducerOf(FiefSlot(FiefGood.Crossbow));
        CompiledRecipe? longbowRecipe = ProducerOf(FiefSlot(FiefGood.Longbow));

        Fix64 newCrossbows = Fix64.Zero;
        if (crossbowRecipe is not null && RecipeAvailable(s, crossbowRecipe))
        {
            Fix64 wood = InputOf(crossbowRecipe, (int)Good.Wood);
            Fix64 iron = InputOf(crossbowRecipe, IronSlot(IronGood.Wrought));
            Fix64 ironInStore = IronStock(s, IronGood.Wrought) + AvailableStock(s, MetalSlot(MetalGood.Iron));
            newCrossbows = deficit;
            if (wood > Fix64.Zero) newCrossbows = Fix64.Min(newCrossbows, spareWood / wood);
            if (iron > Fix64.Zero) newCrossbows = Fix64.Min(newCrossbows, ironInStore / iron);
            newCrossbows = Fix64.Max(Fix64.Zero, newCrossbows);
            if (newCrossbows > Fix64.Zero) plan.Terminal[FiefSlot(FiefGood.Crossbow)] += crossbows + newCrossbows;
            spareWood -= newCrossbows * wood;
        }

        Fix64 newLongbows = deficit - newCrossbows;
        if (newLongbows <= Fix64.Zero || longbowRecipe is null || !RecipeAvailable(s, longbowRecipe)) return;
        Fix64 longbowWood = InputOf(longbowRecipe, (int)Good.Wood);
        if (longbowWood > Fix64.Zero) newLongbows = Fix64.Min(newLongbows, Fix64.Max(Fix64.Zero, spareWood) / longbowWood);
        if (newLongbows > Fix64.Zero) plan.Terminal[FiefSlot(FiefGood.Longbow)] += longbows + newLongbows;
    }

    /// <summary>Bows a settlement wants for its levy: one per resident aged 14 or more who is not a professional soldier, capped.</summary>
    private int LevyBowTarget(SettlementState s)
    {
        int levy = 0;
        foreach (FigureState f in Residents(s.Id))
            if (f.Age >= 14 && f.Role != Role.Soldier) levy++;
        return Math.Min(levy, LevyBowCap);
    }

    /// <summary>The planner's recipe for an output slot, or null.</summary>
    private CompiledRecipe? ProducerOf(int slot)
    {
        foreach (CompiledRecipe r in _compiledRecipes) if (r.OutputSlot == slot) return r;
        return null;
    }

    /// <summary>The quantity a recipe takes of the input that may be met from <paramref name="slot"/>, zero if none.</summary>
    private static Fix64 InputOf(CompiledRecipe r, int slot)
    {
        for (int i = 0; i < r.InputSlots.Length; i++)
            if (Array.IndexOf(r.InputSlots[i], slot) >= 0) return r.InputQuantity[i];
        return Fix64.Zero;
    }

    /// <summary>Work to make one levy bow with its arrows after WAR-03 (36 §3, provisional): a stave and a sheaf, not the first hunting bow's apprenticeship.</summary>
    private static readonly Fix64 LevyBowWork = Fix64.FromRatio(4, 10);
    /// <summary>Wood for a stave and a sheaf of shafts: a split branch and a few straight shoots, so the bow costs very little wood (36 §3).</summary>
    internal static readonly Fix64 LevyBowWood = Fix64.FromRatio(5, 100);
    private static readonly Fix64 LevyBowFlint = Fix64.FromRatio(1, 10);
    /// <summary>A bow kept in store for the levy is useless after about six years: strings rot, staves lose their cast, arrows are spent or lost (36 §5).</summary>
    private const int LevyBowServiceSeasons = 24;

    /// <summary>The most war bows a settlement makes in one season (36 §4): about three, three times the old one-a-season pace.</summary>
    internal const int LevyBowsPerSeason = 3;

    /// <summary>
    /// Archery Warfare's bows (WAR-03, 36 §3), called in place of the hunting bow's craft in a Fief match. The bow becomes a routine craft (0.4 work, 0.3 wood,
    /// 0.1 flint for the bow and its arrows) instead of the E2 first bow's 10 work. Until WAR-11 the settlement keeps a G-BOW for every levy fighter, up to
    /// <see cref="LevyBowCap"/>, the same good the hunters use; after WAR-11 the levy turns to crossbows and longbows and only the hunting bow is kept.
    /// The first bow may take all the craft work, as the hunting bow did; the rest of the levy's take at most a quarter of what is left, only in a
    /// season of enough food and from no more than half the cut wood above the reserve.
    /// </summary>
    private void MakeLevyBows(SettlementState s, ref Fix64 remaining)
    {
        if (remaining <= Fix64.Zero) return;
        Fix64 start = s.Stores[(int)Good.Bow], have = start;
        if (have < Fix64.One)
            MakeGoods(s, ref remaining, Good.Bow, LevyBowWork, Good.Wood, LevyBowWood, Good.Flint, LevyBowFlint, Fix64.One);
        have = s.Stores[(int)Good.Bow];
        if (HasProject(s.Civilization, "WAR-11") || s.FoodSecurity < LevyBowFoodFloor || remaining <= Fix64.Zero) return;
        Fix64 spareWood = Fix64.Max(Fix64.Zero, s.Stores[(int)Good.Wood] - WoodReserve) * LevyBowWoodShare;
        // At most three bows in all this season, the first one included.
        Fix64 target = Fix64.Min(Fix64.FromInt(LevyBowTarget(s)), Fix64.Min(start + Fix64.FromInt(LevyBowsPerSeason), have + spareWood / LevyBowWood));
        if (target <= have) return;
        Fix64 budget = Fix64.Min(remaining, LevyBowWork * (Fix64.FromInt(LevyBowsPerSeason) - Fix64.Min(Fix64.FromInt(LevyBowsPerSeason), have - start)));
        Fix64 granted = budget;
        MakeGoods(s, ref budget, Good.Bow, LevyBowWork, Good.Wood, LevyBowWood, Good.Flint, LevyBowFlint, target);
        remaining -= granted - budget;
    }

    /// <summary>
    /// Age wear of the levy's arms in a Fief match (36 §5): crossbows and longbows by the registry's "40 seasons" and "24 seasons"; the bows in store
    /// beyond the one in use by <see cref="LevyBowServiceSeasons"/>. The bow in use still wears by hunting work as before.
    /// </summary>
    private void WearLevyArms(SettlementState s)
    {
        if (FiefChain && s.FiefGoods is { } fief)
            for (int g = 0; g < (int)FiefGood.Count; g++)
            {
                if (fief[g] <= Fix64.Zero) continue;
                int t = _content.TradeGoodIndex(FiefGoodId((FiefGood)g));
                int seasons = t >= 0 ? _content.TradeGoods[t].ServiceSeasons : 0;
                if (seasons > 0) fief[g] -= fief[g] / seasons;
            }
        Fix64 idle = s.Stores[(int)Good.Bow] - Fix64.One;
        if (idle > Fix64.Zero) RetireStoredTool(s, Good.Bow, idle / LevyBowServiceSeasons);
    }

    /// <summary>Removes untouched units of a durable tool from store first, then the one in use, keeping the work left within what the stock can hold.</summary>
    private void RetireStoredTool(SettlementState s, Good good, Fix64 quantity)
    {
        int index = (int)good;
        Fix64 stock = s.Stores[index];
        Fix64 gone = Fix64.Min(quantity, stock);
        if (gone <= Fix64.Zero) return;
        Fix64 durability = ToolDurability(s, good);
        Fix64 untouched = Fix64.Min(gone, Fix64.Max(Fix64.Zero, stock - Fix64.One));
        s.Stores[index] = stock - gone;
        s.ToolRemainingWork[index] = Fix64.Clamp(s.ToolRemainingWork[index] - untouched * durability, Fix64.Zero, s.Stores[index] * durability);
    }

    /// <summary>
    /// Removes up to <paramref name="quantity"/> of a stocked arms good from a settlement (weapons lost with fallen levies, 36 §5) and returns what
    /// was removed: G-BOW, G-CROSSBOW, G-LONGBOW, and anything else held in the metal stores (arms, chariots). Other E0–E2 tools cannot be lost this way.
    /// </summary>
    public Fix64 LoseArms(int settlementIndex, string goodId, Fix64 quantity)
    {
        int slot = SlotOf(goodId);
        if (slot == NoSlot || quantity <= Fix64.Zero) return Fix64.Zero;
        SettlementState s = _settlements[settlementIndex];
        if (slot == (int)Good.Bow)
        {
            Fix64 before = s.Stores[slot];
            RetireStoredTool(s, Good.Bow, quantity);
            return before - s.Stores[slot];
        }
        if (slot < (int)Good.Count) return Fix64.Zero;
        Fix64 lost = Fix64.Min(quantity, Stock(s, slot));
        if (lost <= Fix64.Zero) return Fix64.Zero;
        AddStock(s, slot, -lost);
        return lost;
    }

    // ---- hashing and snapshots ------------------------------------------------------------------

    private static bool HasFiefGoods(SettlementState s)
    {
        if (s.FiefGoods is not { } fief) return false;
        foreach (Fix64 amount in fief) if (amount != Fix64.Zero) return true;
        return false;
    }

    /// <summary>Hashed only while some settlement holds a bow, so a Fief match before WAR-11 keeps its checksum.</summary>
    private void HashFiefGoods(ref StateHasher hash)
    {
        if (!FiefChain) return;
        bool any = false;
        for (int i = 0; i < _settlements.Count; i++)
        {
            if (!HasFiefGoods(_settlements[i])) continue;
            if (!any) { hash.Add(FiefGoodsTag); any = true; }
            hash.Add(i);
            hash.Add(_settlements[i].FiefGoods!);
        }
    }

    private void WriteFiefGoods(BinaryWriter writer)
    {
        if (!FiefChain) return;
        writer.Write(FiefGoodsSnapshotMagic);
        int count = 0;
        for (int i = 0; i < _settlements.Count; i++) if (_settlements[i].FiefGoods is not null) count++;
        writer.Write(count);
        for (int i = 0; i < _settlements.Count; i++)
        {
            if (_settlements[i].FiefGoods is not { } fief) continue;
            writer.Write(i);
            WriteFixArray(writer, fief);
        }
    }

    private void ReadFiefGoods(BinaryReader reader)
    {
        if (!FiefChain) return;
        if (reader.ReadUInt32() != FiefGoodsSnapshotMagic) throw new InvalidDataException("The snapshot has no Fief levy-bow section.");
        int count = reader.ReadInt32();
        if (count < 0 || count > _settlements.Count) throw new InvalidDataException("Invalid Fief levy-bow settlement count.");
        int previous = -1;
        for (int n = 0; n < count; n++)
        {
            int index = reader.ReadInt32();
            if (index <= previous || index >= _settlements.Count)
                throw new InvalidDataException("Invalid Fief levy-bow settlement index.");
            previous = index;
            var fief = new Fix64[(int)FiefGood.Count];
            ReadFixArray(reader, fief);
            foreach (Fix64 amount in fief) if (amount < Fix64.Zero) throw new InvalidDataException("Snapshot contains a negative bow stock.");
            _settlements[index].FiefGoods = fief;
        }
    }
}
