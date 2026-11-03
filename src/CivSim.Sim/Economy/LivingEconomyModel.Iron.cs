using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// The Iron chapter's metal ledger (docs/MVP/25 §4, 26 §2.3). Everything here is additive and inert unless the match plays the Iron
/// chapter (<see cref="IronChain"/>): the Iron goods live in <see cref="MetalState.IronGoods"/>, outside the arrays a Bronze checksum and
/// snapshot cover, and the Iron recipes sit in their own list, so the Bronze planner never sees them.
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>Goods that exist only in the Iron chapter. Append only; the order is the slot order in snapshots.</summary>
    private enum IronGood : byte
    {
        Wrought, Steel, Kit11, Kit12, Arms3, Arms4,
        Count
    }

    private static string IronGoodId(IronGood good) => good switch
    {
        IronGood.Wrought => "G-WROUGHT", IronGood.Steel => "G-STEEL", IronGood.Kit11 => "G-TOOL11", IronGood.Kit12 => "G-TOOL12",
        IronGood.Arms3 => "G-ARMS3", IronGood.Arms4 => "G-ARMS4",
        _ => "",
    };

    /// <summary>Iron goods take slots after the E3 goods and the Bronze metal goods, so every existing slot keeps its number.</summary>
    private static int IronSlotBase => SlotCount;
    private static int IronSlot(IronGood good) => IronSlotBase + (int)good;

    private const ulong IronTag = 0x4952_4F4E_4348_4149; // "IRONCHAI"
    private const uint IronSnapshotMagic = 0x4E4F5249;   // "IRON" little-endian

    private CompiledRecipe[] _ironRecipes = Array.Empty<CompiledRecipe>();
    /// <summary>Every compiled recipe in content order (ascending order, then id): the Bronze list with the Iron recipes in place.</summary>
    private CompiledRecipe[] _allRecipes = Array.Empty<CompiledRecipe>();
    private bool _ironChain;

    /// <summary>Slots the planner tracks: the E3 and Bronze goods, plus the Iron goods in the Iron chapter.</summary>
    private int PlanSlots => SlotCount + (_ironChain ? (int)IronGood.Count : 0);

    /// <summary>
    /// True when the match plays the Iron chapter (<c>MatchModel</c> sets it from the chapter rules). Off, the Iron recipes cannot run, no Iron
    /// state is hashed or written, and E4 project completions are not recorded: the economy is byte-for-byte the Bronze economy.
    /// </summary>
    public bool IronChain
    {
        get => _ironChain;
        init
        {
            _ironChain = value;
            // The Iron recipes join the planner's list only in the Iron chapter, in content order so the craft step still runs them ascending.
            if (value) _compiledRecipes = _allRecipes;
        }
    }

    /// <summary>The last era whose project completions the economy records: one past <see cref="MaxEra"/> in the Iron chapter.</summary>
    private int ProjectCeiling => IronChain ? MaxEra + 1 : MaxEra;

    /// <summary>Stock of an Iron good in a settlement (zero before the chapter's first batch).</summary>
    private Fix64 IronStock(SettlementState s, IronGood good) => Stock(s, IronSlot(good));

    /// <summary>
    /// The Iron chapter's first claim on the chain (docs/MVP/25 §4): a T11 kit for every adult who does not hold one, wanted only by a settlement
    /// with a forge that knows MET-09. The want expands backwards through the same recipe graph as the Bronze kits (kit, wrought iron, bloom, ore,
    /// charcoal, wood) and is bounded by the same labor cap. Kits wait in store until kit use and wear arrive (a later WP6 slice).
    /// </summary>
    private void IronKitWant(SettlementState s, MetalState m, MetalPlan plan)
    {
        if (!IronChain || m.Forges <= 0 || !Done(s.Civilization, "MET-09")) return;
        // Adults who hold no iron kit, or whose iron kit is about to be replaced, less the kits already made and waiting.
        int needKits = 0;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState f = _figures[i];
            if (f.SettlementId != s.Id || f.Age < 14) continue;
            if (Rank(f.Kit) < Rank(11) || f.KitLeft <= ExpectedSeasonWear) needKits++;
        }
        // A thin forest asks for fewer new and replacement kits, but iron already in store (smelted earlier or imported by compact) is always worth forging
        // into kits: that costs a quarter of a litre of charcoal a kit, where smelting the ore costs six times more.
        Fix64 fromStore = (IronStock(s, IronGood.Wrought) + m.Goods[(int)MetalGood.Iron] * Fix64.FromRatio(4, 5)) / WroughtPerKit;
        Fix64 wanted = Fix64.Max(Fix64.FromInt(needKits) * IronWoodFactor(s, m), Fix64.Min(Fix64.FromInt(needKits), fromStore));
        plan.Terminal[IronSlot(IronGood.Kit11)] = Fix64.Max(Fix64.Zero, wanted - IronStock(s, IronGood.Kit11));
    }

    /// <summary>The standing-wood share (of what the settlement's range held when its metal economy began) above which the Iron chain asks for fuel in full.</summary>
    private static readonly Fix64 IronWoodFull = Fix64.FromRatio(75, 100);
    /// <summary>...and at or below which it asks for none: the forest is left to recover (docs/MVP/25 I-T6, "no mandatory clear-cut").</summary>
    private static readonly Fix64 IronWoodFloor = Fix64.FromRatio(55, 100);

    /// <summary>
    /// How much of the Iron chain's fuel demand the settlement's forest allows, from 0 to 1: the standing wood in range as a share of its
    /// start, scaled linearly between <see cref="IronWoodFloor"/> and <see cref="IronWoodFull"/>. It is stateless (recomputed from the forest each season) and
    /// governs only what the Iron chain *wants*: kits already made keep working, bloom and wrought iron in store are still forged, and the Bronze chain
    /// is untouched. A settlement with no forest baseline is not throttled (it has no wood to protect and the chain will find none to burn).
    /// </summary>
    private Fix64 IronWoodFactor(SettlementState s, MetalState m)
    {
        if (m.ForestAtStart <= Fix64.Zero) return Fix64.One;
        Fix64 share = StandingWoodInRange(s.SiteTile) / m.ForestAtStart;
        return Fix64.Clamp((share - IronWoodFloor) / (IronWoodFull - IronWoodFloor), Fix64.Zero, Fix64.One);
    }

    /// <summary>Wrought iron in one T11 kit (docs/MVP/26 §2.3).</summary>
    private static readonly Fix64 WroughtPerKit = Fix64.FromRatio(15, 100);

    /// <summary>Iron bloom in one T11 kit: 0.15 wrought, and a forge batch turns 0.5 bloom into 0.4 wrought (docs/MVP/26 §2.3).</summary>
    private static readonly Fix64 BloomPerKit = Fix64.FromRatio(1875, 10000);

    /// <summary>
    /// A settlement that sells iron under a compact keeps a bloom reserve of a litre per 20 people on top of its own chain's need, so a
    /// caravan has a surplus to carry. Only a settlement that can dig iron (or has a camp on it) makes one.
    /// </summary>
    private void IronExportWant(SettlementState s, MetalState m, MetalPlan plan, int pop)
    {
        if (!IronChain || IronTrade is not { } trade || !trade.SellsIron(s.Civilization)) return;
        if ((m.Access & SourceIron) == 0 && !CanDig(s, MetalGood.IronOre)) return;
        plan.Terminal[MetalSlot(MetalGood.Iron)] += Fix64.FromRatio(pop, 20) * IronWoodFactor(s, m);
    }

    /// <summary>
    /// What a settlement needs of iron bloom for its own T11 kits: the kits it lacks (adults with no iron kit or one about to wear out,
    /// less kits in store), less the bloom already in store as wrought iron. The trade valuation reads it as the "need".
    /// </summary>
    private Fix64 IronBloomNeed(SettlementState s)
    {
        if (!Done(s.Civilization, "MET-09")) return Fix64.Zero;
        int lacking = 0;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState f = _figures[i];
            if (f.SettlementId != s.Id || f.Age < 14) continue;
            if (Rank(f.Kit) < Rank(11) || f.KitLeft <= ExpectedSeasonWear) lacking++;
        }
        Fix64 kits = Fix64.Max(Fix64.Zero, Fix64.FromInt(lacking) - IronStock(s, IronGood.Kit11) - IronStock(s, IronGood.Kit12));
        Fix64 inStore = IronStock(s, IronGood.Wrought) * Fix64.FromRatio(5, 4);
        return Fix64.Max(Fix64.Zero, kits * BloomPerKit - inStore);
    }

    /// <summary>
    /// Hands the settlement's Iron kits to its adults (docs/MVP/26 §2.2): those with no kit first, then worn kits, then early upgrades, oldest
    /// first, each getting the best Iron tier in store that beats what they carry. An early upgrade of a Bronze kit remelts it as the E3 rule does;
    /// an Iron kit is never refunded on retirement. The kit moves from store to figure, so it exists once.
    /// </summary>
    private void IssueIronKits(SettlementState s, MetalState m)
    {
        if (m.IronGoods is null) return;
        int civ = s.Civilization;
        int top = Done(civ, "MET-10") ? 12 : Done(civ, "MET-09") ? 11 : 0;
        if (top == 0) return;
        var order = new List<int>();
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState f = _figures[i];
            if (f.SettlementId != s.Id || f.Age < 14) continue;
            if (f.Kit == 0 || f.KitLeft <= Fix64.Zero || Rank(f.Kit) < Rank(top)) order.Add(i);
        }
        order.Sort((a, b) =>
        {
            FigureState fa = _figures[a], fb = _figures[b];
            int pa = fa.Kit == 0 ? 0 : fa.KitLeft <= Fix64.Zero ? 1 : 2, pb = fb.Kit == 0 ? 0 : fb.KitLeft <= Fix64.Zero ? 1 : 2;
            if (pa != pb) return pa.CompareTo(pb);
            int byLeft = fa.KitLeft.CompareTo(fb.KitLeft);
            return byLeft != 0 ? byLeft : fa.Id.CompareTo(fb.Id);
        });
        foreach (int i in order)
        {
            FigureState f = _figures[i];
            int give = 0;
            for (int t = top; t >= 11; t--)
            {
                if (IronStock(s, t == 12 ? IronGood.Kit12 : IronGood.Kit11) < Fix64.One) continue;
                if (f.Kit != 0 && f.KitLeft > Fix64.Zero && Rank(t) <= Rank(f.Kit)) continue;
                give = t;
                break;
            }
            if (give == 0) continue;
            if (f.Kit is >= 8 and <= 10 && f.KitLeft > Fix64.Zero && _tierDefs[f.Kit] is { } old)
            {
                Fix64 copper = old.MetalPerKit * _bronzeConstants.RemeltShare;
                m.Goods[(int)MetalGood.Copper] += copper;
                AddMetalStat(civ, StatRemeltCopper, copper);
            }
            AddStock(s, IronSlot(give == 12 ? IronGood.Kit12 : IronGood.Kit11), -Fix64.One);
            f.Kit = (byte)give;
            f.KitLeft = ServiceOf(give);
        }
    }

    // ---- recipes --------------------------------------------------------------------------------

    /// <summary>
    /// Runs one Iron recipe as the craft step would (docs/MVP/26 §2.3): the same scarcest-input and labor limits as <see cref="RunRecipe"/>, inputs
    /// debited before the output is credited. Returns the batches made; zero when the chapter is off, the recipe is locked, or the structure is missing.
    /// </summary>
    private Fix64 RunIronRecipe(SettlementState s, string recipeId, Fix64 planned, ref Fix64 craftPool)
    {
        if (!IronChain || s.Metal is null) return Fix64.Zero;
        foreach (CompiledRecipe recipe in _ironRecipes)
        {
            if (recipe.Def.Id != recipeId) continue;
            if (!RecipeAvailable(s, recipe)) return Fix64.Zero;
            Fix64 metalPool = Fix64.Zero;
            return RunRecipe(s, recipe, planned, anyMetalKit: false, ref metalPool, ref craftPool);
        }
        return Fix64.Zero;
    }

    // ---- hashing and snapshots ------------------------------------------------------------------

    private void HashIron(ref StateHasher hash)
    {
        if (!IronChain) return;
        hash.Add(IronTag);
        for (int i = 0; i < _settlements.Count; i++)
        {
            if (_settlements[i].Metal?.IronGoods is not { } iron) continue;
            hash.Add(i);
            hash.Add(iron);
        }
    }

    private void WriteIron(BinaryWriter writer)
    {
        if (!IronChain) return;
        writer.Write(IronSnapshotMagic);
        int count = 0;
        for (int i = 0; i < _settlements.Count; i++) if (_settlements[i].Metal?.IronGoods is not null) count++;
        writer.Write(count);
        for (int i = 0; i < _settlements.Count; i++)
        {
            if (_settlements[i].Metal?.IronGoods is not { } iron) continue;
            writer.Write(i);
            WriteFixArray(writer, iron);
        }
    }

    private void ReadIron(BinaryReader reader)
    {
        if (!IronChain) return;
        if (reader.ReadUInt32() != IronSnapshotMagic) throw new InvalidDataException("The snapshot has no Iron-chapter economy section.");
        int count = reader.ReadInt32();
        if (count < 0 || count > _settlements.Count) throw new InvalidDataException("Invalid Iron-chapter settlement count.");
        int previous = -1;
        for (int n = 0; n < count; n++)
        {
            int index = reader.ReadInt32();
            if (index <= previous || index >= _settlements.Count || _settlements[index].Metal is not { } metal)
                throw new InvalidDataException("Invalid Iron-chapter settlement index.");
            previous = index;
            var iron = new Fix64[(int)IronGood.Count];
            ReadFixArray(reader, iron);
            foreach (Fix64 amount in iron) if (amount < Fix64.Zero) throw new InvalidDataException("Snapshot contains a negative Iron-good stock.");
            metal.IronGoods = iron;
        }
    }
}
