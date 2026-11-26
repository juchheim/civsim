using System;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// Coal and coke, the Discovery chapter's fuel goods (docs/MVP/38 §5.1, M13 WP-E6-5). Everything here is inert unless the match plays the Discovery chapter
/// (<see cref="DiscoveryChain"/>): the goods live in their own per-settlement array and their own snapshot tail, written and hashed only in a Discovery
/// match, so Bronze, Iron and Fief bytes, hashes and planner behavior are unchanged. The two goods take slots after the levy-bow goods; recipes that touch
/// them are disabled outside Discovery.
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>Discovery-only goods. Appended after <see cref="BowGood"/>; never renumber.</summary>
    private enum DiscoveryGood : byte
    {
        Coal, Coke,
        // The glass-and-instruments chain (docs/MVP/38 §5.3, M13 WP-E6-7 slice 3): appended after coal and coke, written to Discovery snapshots only.
        Alkali, Acid, PlateGlass, Parts, Chronometer, Telescope,
        // The musket rung of the levy's arms (docs/MVP/38 §5.6, M13 WP-E6-9 slice 2): appended after the instruments, Discovery snapshots only.
        Musket,
        Count
    }

    private static string DiscoveryGoodId(DiscoveryGood good) => good switch
    {
        DiscoveryGood.Coal => "G-COAL", DiscoveryGood.Coke => "G-COKE",
        DiscoveryGood.Alkali => "G-ALKALI", DiscoveryGood.Acid => "G-ACID", DiscoveryGood.PlateGlass => "G-PLATEGLASS",
        DiscoveryGood.Parts => "G-PARTS", DiscoveryGood.Chronometer => "G-CHRONOMETER", DiscoveryGood.Telescope => "G-TELESCOPE", DiscoveryGood.Musket => "G-MUSKET", _ => "",
    };

    private static int DiscoverySlotBase => BowSlotBase + (int)BowGood.Count;
    private static int DiscoverySlot(DiscoveryGood good) => DiscoverySlotBase + (int)good;

    private const uint DiscoveryGoodsSnapshotMagic = 0x4C41_4F43; // "COAL" little-endian
    private const ulong DiscoveryGoodsTag = 0x4449_5343_4F41_4C53; // "DISCOALS"

    /// <summary>Stock of a Discovery good in a settlement (zero outside Discovery or before the first batch).</summary>
    private Fix64 DiscoveryStock(SettlementState s, DiscoveryGood good) => Stock(s, DiscoverySlot(good));

    /// <summary>Litres of coal held by a settlement (zero outside a Discovery match).</summary>
    internal Fix64 CoalStock(int settlementIndex)
        => DiscoveryChain && (uint)settlementIndex < (uint)_settlements.Count ? DiscoveryStock(_settlements[settlementIndex], DiscoveryGood.Coal) : Fix64.Zero;

    /// <summary>Litres of coal held by a settlement, for diagnostics outside the simulation assembly (zero outside Discovery).</summary>
    public Fix64 CoalLitres(int settlementIndex) => CoalStock(settlementIndex);

    /// <summary>Litres of coke held by a settlement (zero outside a Discovery match).</summary>
    internal Fix64 CokeStock(int settlementIndex)
        => DiscoveryChain && (uint)settlementIndex < (uint)_settlements.Count ? DiscoveryStock(_settlements[settlementIndex], DiscoveryGood.Coke) : Fix64.Zero;

    private static bool HasDiscoveryGoods(SettlementState s)
    {
        if (s.DiscoveryGoods is not { } goods) return false;
        foreach (Fix64 amount in goods) if (amount != Fix64.Zero) return true;
        return false;
    }

    /// <summary>Hashed only in a Discovery match and only while a settlement holds coal or coke, so a match that never mines keeps its Fief checksum shape.</summary>
    private void HashDiscoveryGoods(ref StateHasher hash)
    {
        if (!DiscoveryChain) return;
        bool any = false;
        for (int i = 0; i < _settlements.Count; i++)
        {
            if (!HasDiscoveryGoods(_settlements[i])) continue;
            if (!any) { hash.Add(DiscoveryGoodsTag); any = true; }
            hash.Add(i);
            hash.Add(_settlements[i].DiscoveryGoods!);
        }
    }

    private void WriteDiscoveryGoods(BinaryWriter writer)
    {
        if (!DiscoveryChain) return;
        writer.Write(DiscoveryGoodsSnapshotMagic);
        int count = 0;
        for (int i = 0; i < _settlements.Count; i++) if (_settlements[i].DiscoveryGoods is not null) count++;
        writer.Write(count);
        for (int i = 0; i < _settlements.Count; i++)
        {
            if (_settlements[i].DiscoveryGoods is not { } goods) continue;
            writer.Write(i);
            WriteFixArray(writer, goods);
        }
    }

    private void ReadDiscoveryGoods(BinaryReader reader)
    {
        if (!DiscoveryChain) return;
        if (reader.ReadUInt32() != DiscoveryGoodsSnapshotMagic) throw new InvalidDataException("The snapshot has no Discovery goods section.");
        int count = reader.ReadInt32();
        if (count < 0 || count > _settlements.Count) throw new InvalidDataException("Invalid Discovery goods settlement count.");
        int previous = -1;
        for (int n = 0; n < count; n++)
        {
            int index = reader.ReadInt32();
            if (index <= previous || index >= _settlements.Count) throw new InvalidDataException("Invalid Discovery goods settlement index.");
            previous = index;
            var goods = new Fix64[(int)DiscoveryGood.Count];
            ReadFixArray(reader, goods);
            foreach (Fix64 amount in goods) if (amount < Fix64.Zero) throw new InvalidDataException("Snapshot contains a negative Discovery good stock.");
            _settlements[index].DiscoveryGoods = goods;
        }
    }

    /// <summary>Tests only: put coal or coke in a settlement's stores.</summary>
    internal void FixtureAddDiscoveryGood(int settlementIndex, string goodId, Fix64 amount)
    {
        if (!DiscoveryChain) throw new InvalidOperationException("Coal and coke exist only in a Discovery match.");
        for (int g = 0; g < (int)DiscoveryGood.Count; g++)
            if (DiscoveryGoodId((DiscoveryGood)g) == goodId) { AddStock(_settlements[settlementIndex], DiscoverySlot((DiscoveryGood)g), amount); return; }
        throw new ArgumentException($"No Discovery good '{goodId}'.", nameof(goodId));
    }

    // ---- coal mining (docs/MVP/38 §5.1) ---------------------------------------------------------------

    private static DiscoveryGood? DiscoveryOreGood(DepositKind kind) => kind == DepositKind.Coal ? DiscoveryGood.Coal : null;

    private Fix64 DigDiscoveryOre(SettlementState s, int tile, DiscoveryGood good, Fix64 rate, Fix64 productive, Fix64 want)
    {
        if (!DiscoveryChain || DiscoveryOreGood(_world.Deposit(tile).Kind) != good) return Fix64.Zero;
        Fix64 dug = Fix64.Min(want, Fix64.Min(_ore[tile], productive * rate));
        if (dug > Fix64.Zero)
        {
            _ore[tile] -= dug;
            AddStock(s, DiscoverySlot(good), dug);
        }
        return dug;
    }

    private bool HasDiscoveryOreCamp(SettlementState s)
    {
        if (!DiscoveryChain) return false;
        foreach (MiningCampState camp in _camps)
            if (camp.Parent == s.Id && DiscoveryOreGood(_world.Deposit(camp.Tile).Kind) is not null && DepositWorkable(s.Civilization, camp.Tile)) return true;
        return false;
    }

    private bool HasDiscoveryDepositInReach(SettlementState s)
    {
        if (!DiscoveryChain) return false;
        foreach (int tile in ReachTiles(s.SiteTile, FiefOreReach(s)))
            if (DiscoveryOreGood(_world.Deposit(tile).Kind) is not null && DepositWorkable(s.Civilization, tile) && IsOwnTile(s.Civilization, tile) && !IsRivalTile(s.Civilization, tile)) return true;
        return false;
    }

    /// <summary>Whether a settlement holds coal, digs it at a camp, or has an own coal deposit within its ore reach that the planner may open a camp on.</summary>
    private bool CoalObtainable(SettlementState s)
        => DiscoveryChain && (Stock(s, DiscoverySlot(DiscoveryGood.Coal)) > Fix64.Zero || HasDiscoveryOreCamp(s) || HasDiscoveryDepositInReach(s));

    /// <summary>The camp-opening rule for coal, the same as the Fief ores': a want beyond stock, no camp yet, an own deposit in reach.</summary>
    private void OpenCoalCampIfWanted(SettlementState s, MetalPlan plan, int season)
    {
        int slot = DiscoverySlot(DiscoveryGood.Coal);
        if (plan.Want[slot] <= Stock(s, slot) || HasDiscoveryOreCamp(s)) return;
        foreach (int tile in ReachTiles(s.SiteTile, FiefOreReach(s)))
        {
            if (DiscoveryOreGood(_world.Deposit(tile).Kind) is null || !IsOwnTile(s.Civilization, tile) || IsRivalTile(s.Civilization, tile)) continue;
            TryOpenCamp(s, tile, HexGrid.Distance(s.SiteTile, tile, _world.Width), decreed: false, season);
            break;
        }
    }

    // ---- coal firing, FIR-11 (docs/MVP/38 §5.1) -------------------------------------------------------

    /// <summary>Litres of loose wood one litre of coal replaces as heat (<c>WOOD_L_PER_COAL_L</c> in e6_power_oracle.py; provisional).</summary>
    private static readonly Fix64 WoodLitresPerCoalLitre = Fix64.FromInt(4);

    /// <summary>Wood the town does not burn this season because coal heats it instead: the FIR-11 share of its hearth and kiln wood, all or nothing on the coal being in store.</summary>
    private Fix64 CoalHeatRelief(SettlementState s, Fix64 heatWood)
    {
        if (heatWood <= Fix64.Zero || !Done(s.Civilization, "FIR-11")) return Fix64.Zero;
        Fix64 mult = Modifier(s.Civilization, "heat_wood_demand");
        if (mult >= Fix64.One) return Fix64.Zero;
        Fix64 relief = heatWood * (Fix64.One - mult);
        return Stock(s, DiscoverySlot(DiscoveryGood.Coal)) >= relief / WoodLitresPerCoalLitre ? relief : Fix64.Zero;
    }

    /// <summary>Coal a town would burn in a season to take its FIR-11 share off the forest (independent of what is in store, so it can be wanted).</summary>
    private Fix64 CoalHeatNeed(SettlementState s, bool winter)
    {
        if (!Done(s.Civilization, "FIR-11")) return Fix64.Zero;
        E3FuelParts(s, winter, out Fix64 heat, out _);
        Fix64 mult = Modifier(s.Civilization, "heat_wood_demand");
        return mult >= Fix64.One ? Fix64.Zero : heat * (Fix64.One - mult) / WoodLitresPerCoalLitre;
    }

    /// <summary>Season step: charges the coal that replaced wood. The wood side is already out of <c>E3FuelNeed</c>, which grants the relief only when this coal is in store.</summary>
    private void BurnCoalForHeat(SettlementState s, bool winter)
    {
        E3FuelParts(s, winter, out Fix64 heat, out _);
        Fix64 relief = CoalHeatRelief(s, heat);
        if (relief > Fix64.Zero) AddStock(s, DiscoverySlot(DiscoveryGood.Coal), -(relief / WoodLitresPerCoalLitre));
    }

    /// <summary>Tests only: the season's hearth, kiln and building wood demand (coal-firing relief included), and a direct run of the coal-for-heat charge.</summary>
    internal Fix64 FixtureFuelNeed(int settlementIndex, bool winter) => E3FuelNeed(_settlements[settlementIndex], winter);

    internal void FixtureBurnCoalForHeat(int settlementIndex, bool winter) => BurnCoalForHeat(_settlements[settlementIndex], winter);

    // ---- coke oven and navigation canal (docs/MVP/38 §5.1) --------------------------------------------

    /// <summary>A coke oven coks with 1.2 L of coal where the kiln takes 1.5 (the same 1 L of coke), in half the labor. Provisional.</summary>
    private static readonly Fix64 CokeOvenCoalFactor = Fix64.FromRatio(4, 5);
    private static readonly Fix64 CokeOvenLaborFactor = Fix64.Half;

    /// <summary>The shortest coal-camp distance a canal is worth building for (tiles).</summary>
    private const int CanalMinDistance = 3;

    /// <summary>Distance in tiles to the town's farthest coal camp, or 0 with none.</summary>
    private int FarthestCoalCamp(SettlementState s)
    {
        int farthest = 0;
        foreach (MiningCampState camp in _camps)
            if (camp.Parent == s.Id && _world.Deposit(camp.Tile).Kind == DepositKind.Coal && camp.Distance > farthest) farthest = camp.Distance;
        return farthest;
    }

    /// <summary>
    /// The divisor on a coal camp's haul labor: the cart's x5 (TRN-05) or none, replaced by the canal's x10 (TRN-11, "bulk haul inland x10") once the town has dug a navigation
    /// canal. Coal only; grain and every other good keep their own haul.
    /// </summary>
    private Fix64 CoalHaulDivisor(SettlementState s, Fix64 divisor)
        => IronWorksCount(s, IronStructure.NavigationCanal) > 0 && Done(s.Civilization, "TRN-11") ? Fix64.Max(divisor, Modifier(s.Civilization, "bulk_haul_inland")) : divisor;

    /// <summary>Tests only: a coal camp's haul divisor for a town given a starting divisor, and the cohesion walk of a town after its road effects.</summary>
    internal Fix64 FixtureCoalHaulDivisor(int settlementIndex, Fix64 divisor) => CoalHaulDivisor(_settlements[settlementIndex], divisor);

    internal int FixtureFarthestCoalCamp(int settlementIndex) => FarthestCoalCamp(_settlements[settlementIndex]);

    internal int FixtureRoadAdjustedWalk(int settlementIndex, int walk) => RoadAdjustedWalk(_settlements[settlementIndex], walk);

    // ---- coke smelting (docs/MVP/38 §5.1) -------------------------------------------------------------

    /// <summary>
    /// MET-13 coke smelting runs in a town with a blast furnace and a supply of coal or coke: then its iron comes from the coke-smelting recipe (no charcoal)
    /// and the bloomery recipe stands aside. With no coal in reach and no coke in store the town keeps the Fief furnace.
    /// </summary>
    private bool CokeSmeltingActive(SettlementState s)
        => DiscoveryChain && Done(s.Civilization, "MET-13") && s.Metal is not null && IronWorksCount(s, IronStructure.BlastFurnace) > 0 &&
           (Stock(s, DiscoverySlot(DiscoveryGood.Coke)) > Fix64.Zero || CoalObtainable(s));

    /// <summary>Tests only: the tiles of the mining camps a settlement runs, and the litres still in the ground at a tile.</summary>
    internal int[] FixtureCampTiles(int settlementIndex)
    {
        var tiles = new System.Collections.Generic.List<int>();
        int id = _settlements[settlementIndex].Id;
        foreach (MiningCampState camp in _camps) if (camp.Parent == id) tiles.Add(camp.Tile);
        return tiles.ToArray();
    }

    /// <summary>Tests only: the tile of the first coal camp a settlement runs, or -1.</summary>
    internal int FixtureCoalCampTile(int settlementIndex)
    {
        int id = _settlements[settlementIndex].Id;
        foreach (MiningCampState camp in _camps)
            if (camp.Parent == id && _world.Deposit(camp.Tile).Kind == DepositKind.Coal) return camp.Tile;
        return -1;
    }

    internal Fix64 FixtureOreLeft(int tile) => _ore[tile];

    internal bool FixtureCokeSmeltingActive(int settlementIndex) => CokeSmeltingActive(_settlements[settlementIndex]);

    internal bool FixtureRecipeAvailable(int settlementIndex, string recipeId)
        => RecipeAvailable(_settlements[settlementIndex], FixtureRecipeById(recipeId));

    private CompiledRecipe FixtureRecipeById(string id)
    {
        foreach (CompiledRecipe r in _allRecipes) if (r.Def.Id == id) return r;
        throw new ArgumentException($"Unknown recipe '{id}'.", nameof(id));
    }
}
