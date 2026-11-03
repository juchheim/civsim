using System;
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
        plan.Terminal[IronSlot(IronGood.Kit11)] = Fix64.Max(Fix64.Zero, Fix64.FromInt(plan.Adults) - IronStock(s, IronGood.Kit11));
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
