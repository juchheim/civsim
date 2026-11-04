using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

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
        Wrought, Steel, Kit11, Kit12, Arms3, Arms4, Plank, Engine, BlownGlass, FiredBrick, Tile, Barrel, Grape, Olive, Wine, OilPlant,
        Count
    }

    private static string IronGoodId(IronGood good) => good switch
    {
        IronGood.Wrought => "G-WROUGHT", IronGood.Steel => "G-STEEL", IronGood.Kit11 => "G-TOOL11", IronGood.Kit12 => "G-TOOL12",
        IronGood.Arms3 => "G-ARMS3", IronGood.Arms4 => "G-ARMS4", IronGood.Plank => "G-PLANK", IronGood.Engine => "G-ENGINE",
        IronGood.BlownGlass => "G-BLOWNGLASS", IronGood.FiredBrick => "G-FBRICK", IronGood.Tile => "G-TILE", IronGood.Barrel => "G-BARREL",
        IronGood.Grape => "G-GRAPE", IronGood.Olive => "G-OLIVE", IronGood.Wine => "G-WINE", IronGood.OilPlant => "G-OILPLANT",
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
        // Adults who hold no iron kit, or whose iron kit is about to be replaced, here and in the forgeless sister settlements this one smiths for,
        // less the kits already made and waiting (the sisters' own stores are netted in their deficit).
        Fix64 needKits = Fix64.FromInt(IronKitsLacking(s)) + SisterKitDeficit(s);
        // A thin forest asks for fewer new and replacement kits, but iron already in store (smelted earlier or imported by compact) is always worth forging
        // into kits: that costs a quarter of a litre of charcoal a kit, where smelting the ore costs six times more.
        // With MET-10 the kits wanted are steel-edge T12 (replacing a worn or missing kit, never an early upgrade of a working one); steel and wrought
        // iron in store both count, since carburizing stored wrought iron is cheap (0.2 charcoal for 0.4 steel).
        bool steel = Done(s.Civilization, "MET-10");
        Fix64 iron = IronStock(s, IronGood.Wrought) + m.Goods[(int)MetalGood.Iron] * Fix64.FromRatio(4, 5) + (steel ? IronStock(s, IronGood.Steel) : Fix64.Zero);
        Fix64 fromStore = iron / WroughtPerKit;
        Fix64 wanted = Fix64.Max(needKits * IronWoodFactor(s, m), Fix64.Min(needKits, fromStore));
        plan.Terminal[IronSlot(steel ? IronGood.Kit12 : IronGood.Kit11)] = Fix64.Max(Fix64.Zero, wanted - IronKitsInStore(s));
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

    /// <summary>Screen and test switch: turn the Iron chapter's sharing between sister settlements off. On in production.</summary>
    internal bool IronSharingEnabled { get; set; } = true;

    /// <summary>The distance within which sister settlements share Iron goods: the E3 sharing reach, or none when sharing is switched off.</summary>
    private int IronShareReach(int civ) => IronSharingEnabled ? ShareReach(civ) : -1;

    /// <summary>Adults of a settlement with no iron kit, or whose iron kit is about to wear out.</summary>
    private int IronKitsLacking(SettlementState s)
    {
        int lacking = 0;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState f = _figures[i];
            if (f.SettlementId != s.Id || f.Age < 14) continue;
            if (Rank(f.Kit) < Rank(11) || f.KitLeft <= ExpectedSeasonWear) lacking++;
        }
        return lacking;
    }

    private Fix64 IronKitsInStore(SettlementState s) => IronStock(s, IronGood.Kit11) + IronStock(s, IronGood.Kit12);

    /// <summary>A sister settlement this one can send goods to or take them from: same civilization, a living metal economy, within the sharing reach.</summary>
    private bool IronSister(SettlementState s, SettlementState other, int reach)
        => other != s && other.Civilization == s.Civilization && other.Metal is not null && !IsFreeFolk(other) && Population(other.Id) > 0 &&
           HexGrid.Distance(other.SiteTile, s.SiteTile, _world.Width) <= reach;

    /// <summary>
    /// The iron kits the forgeless sister settlements within the sharing reach still lack (their adults without an iron kit, less kits they
    /// already hold). A forge settlement smiths for them, as it casts Bronze kits for sisters (docs/MVP/22 §5.7). Zero without the sharing
    /// research (SOC-07 or GOV-04).
    /// </summary>
    private Fix64 SisterKitDeficit(SettlementState s)
    {
        int reach = IronShareReach(s.Civilization);
        if (reach < 0) return Fix64.Zero;
        Fix64 deficit = Fix64.Zero;
        foreach (SettlementState sister in _settlements)
        {
            if (!IronSister(s, sister, reach) || sister.Metal!.Forges > 0) continue;
            deficit += Fix64.Max(Fix64.Zero, Fix64.FromInt(IronKitsLacking(sister)) - IronKitsInStore(sister));
        }
        return deficit;
    }

    /// <summary>
    /// The bloom the forge sisters not digging iron themselves still lack, which a settlement that can dig iron smelts for them (stateless, from
    /// the sisters' kits and stores), scaled by this settlement's forest: the smelting fuel is its own.
    /// </summary>
    private Fix64 SisterBloomDeficit(SettlementState s, MetalState m)
    {
        int reach = IronShareReach(s.Civilization);
        if (reach < 0 || ((m.Access & SourceIron) == 0 && !CanDig(s, MetalGood.IronOre))) return Fix64.Zero;
        Fix64 deficit = Fix64.Zero;
        foreach (SettlementState sister in _settlements)
        {
            if (!IronSister(s, sister, reach) || sister.Metal!.Forges <= 0) continue;
            if (CanDig(sister, MetalGood.IronOre)) continue; // it digs iron now; a deposit it merely could open a camp on does not count
            deficit += Fix64.Max(Fix64.Zero, IronBloomNeed(sister) - sister.Metal.Goods[(int)MetalGood.Iron]);
        }
        return deficit * IronWoodFactor(s, m);
    }

    /// <summary>
    /// Moves Iron kits and bloom between sister settlements of a civilization (called from <c>ShareMetals</c> in the Iron chapter, after the Bronze goods):
    /// whole kits, best tier first, from settlements holding more than their own adults lack to those short of them; bloom from settlements holding
    /// more than their own kits need to forge settlements without iron. Nothing is created: each move debits the donor as it credits the receiver.
    /// </summary>
    private void ShareIronGoods(int civ, int reach)
    {
        for (int r = 0; r < _settlements.Count; r++)
        {
            SettlementState receiver = _settlements[r];
            if (receiver.Civilization != civ || receiver.Metal is not { } rm || IsFreeFolk(receiver) || Population(receiver.Id) == 0) continue;
            Fix64 need = Fix64.FromInt(IronKitsLacking(receiver)) - IronKitsInStore(receiver);
            for (int d = 0; d < _settlements.Count && need >= Fix64.One; d++)
            {
                if (d == r || !IronSister(receiver, _settlements[d], reach)) continue;
                SettlementState donor = _settlements[d];
                Fix64 surplus = IronKitsInStore(donor) - Fix64.FromInt(IronKitsLacking(donor));
                for (int t = 12; t >= 11 && surplus >= Fix64.One && need >= Fix64.One; t--)
                {
                    IronGood kit = t == 12 ? IronGood.Kit12 : IronGood.Kit11;
                    Fix64 move = Fix64.FromInt(Fix64.Min(Fix64.Min(need, surplus), IronStock(donor, kit)).FloorToInt());
                    if (move <= Fix64.Zero) continue;
                    AddStock(donor, IronSlot(kit), -move);
                    AddStock(receiver, IronSlot(kit), move);
                    need -= move;
                    surplus -= move;
                }
            }
        }
        for (int r = 0; r < _settlements.Count; r++)
        {
            SettlementState receiver = _settlements[r];
            if (receiver.Civilization != civ || receiver.Metal is not { Forges: > 0 } rm || IsFreeFolk(receiver) || Population(receiver.Id) == 0) continue;
            if (CanDig(receiver, MetalGood.IronOre)) continue;
            Fix64 need = IronBloomNeed(receiver) - rm.Goods[(int)MetalGood.Iron];
            for (int d = 0; d < _settlements.Count && need > Epsilon; d++)
            {
                if (d == r || !IronSister(receiver, _settlements[d], reach)) continue;
                SettlementState donor = _settlements[d];
                Fix64 surplus = donor.Metal!.Goods[(int)MetalGood.Iron] - IronBloomNeed(donor);
                Fix64 move = Fix64.Min(need, surplus);
                if (move <= Epsilon) continue;
                donor.Metal.Goods[(int)MetalGood.Iron] -= move;
                rm.Goods[(int)MetalGood.Iron] += move;
                need -= move;
            }
        }
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
        if (!IronChain) return;
        // Bloom for forge sisters that have no iron of their own, smelted here.
        plan.Terminal[MetalSlot(MetalGood.Iron)] += SisterBloomDeficit(s, m);
        if (IronTrade is not { } trade || !trade.SellsIron(s.Civilization)) return;
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
        Fix64 kits = Fix64.Max(Fix64.Zero, Fix64.FromInt(IronKitsLacking(s)) - IronStock(s, IronGood.Kit11) - IronStock(s, IronGood.Kit12));
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

    private static bool HasIronState(MetalState m)
        => m.IronGoods is not null || m.IronWorks is not null || m.IronBuildKind >= 0 || m.IronTerraces is { Count: > 0 } || m.IronSoldiersSupplied > 0 || m.IronFacing is { Count: > 0 } || m.IronFaith is not null || m.IronPledgeTo != 0;

    private void HashIron(ref StateHasher hash)
    {
        if (!IronChain) return;
        hash.Add(IronTag);
        for (int i = 0; i < _settlements.Count; i++)
        {
            if (_settlements[i].Metal is not { } m || !HasIronState(m)) continue;
            hash.Add(i);
            hash.Add(m.IronGoods ?? new Fix64[(int)IronGood.Count]);
            hash.Add(m.IronBuildKind);
            hash.Add(m.IronBuildProgress);
            hash.Add(m.IronSoldiersSupplied);
            for (int k = 0; k < (int)IronStructure.Count; k++) hash.Add(m.IronWorks is { } w ? w[k] : 0);
            hash.Add(m.IronTerraces?.Count ?? 0);
            if (m.IronTerraces is not null) foreach (int tile in m.IronTerraces) hash.Add(tile);
            hash.Add(m.IronFacing?.Count ?? 0);
            if (m.IronFacing is not null) foreach (int civ in m.IronFacing) hash.Add(civ);
            hash.Add(m.IronPledgeTo);
            hash.Add(m.IronFaith?.Length ?? 0);
            if (m.IronFaith is not null) for (int k = 0; k < m.IronFaith.Length; k++) { hash.Add(m.IronFaith[k]); hash.Add(m.IronFaithSeason![k]); }
        }
    }

    private void WriteIron(BinaryWriter writer)
    {
        if (!IronChain) return;
        writer.Write(IronSnapshotMagic);
        int count = 0;
        for (int i = 0; i < _settlements.Count; i++) if (_settlements[i].Metal is { } m && HasIronState(m)) count++;
        writer.Write(count);
        for (int i = 0; i < _settlements.Count; i++)
        {
            if (_settlements[i].Metal is not { } m || !HasIronState(m)) continue;
            writer.Write(i);
            WriteFixArray(writer, m.IronGoods ?? new Fix64[(int)IronGood.Count]);
            writer.Write(m.IronBuildKind);
            writer.Write(m.IronBuildProgress.Raw);
            writer.Write(m.IronSoldiersSupplied);
            for (int k = 0; k < (int)IronStructure.Count; k++) writer.Write(m.IronWorks is { } w ? w[k] : 0);
            writer.Write(m.IronTerraces?.Count ?? 0);
            if (m.IronTerraces is not null) foreach (int tile in m.IronTerraces) writer.Write(tile);
            writer.Write(m.IronFacing?.Count ?? 0);
            if (m.IronFacing is not null) foreach (int civ in m.IronFacing) writer.Write(civ);
            writer.Write(m.IronPledgeTo);
            writer.Write(m.IronFaith?.Length ?? 0);
            if (m.IronFaith is not null) for (int k = 0; k < m.IronFaith.Length; k++) { writer.Write(m.IronFaith[k].Raw); writer.Write(m.IronFaithSeason![k]); }
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
            metal.IronBuildKind = reader.ReadInt32();
            metal.IronBuildProgress = Fix64.FromRaw(reader.ReadInt64());
            metal.IronSoldiersSupplied = reader.ReadInt32();
            if (metal.IronSoldiersSupplied < 0) throw new InvalidDataException("Negative Iron-chapter soldier count.");
            if (metal.IronBuildKind < -1 || metal.IronBuildKind >= (int)IronStructure.Count || metal.IronBuildProgress < Fix64.Zero)
                throw new InvalidDataException("Invalid Iron-chapter build state.");
            var works = new int[(int)IronStructure.Count];
            bool any = false;
            for (int k = 0; k < works.Length; k++)
            {
                works[k] = reader.ReadInt32();
                if (works[k] < 0) throw new InvalidDataException("Negative Iron-chapter structure count.");
                any |= works[k] > 0;
            }
            metal.IronWorks = any ? works : null;
            int terraces = reader.ReadInt32();
            if (terraces < 0 || terraces > _world.TileCount) throw new InvalidDataException("Invalid Iron-chapter terrace count.");
            metal.IronTerraces = terraces == 0 ? null : new List<int>(terraces);
            for (int t = 0; t < terraces; t++)
            {
                int tile = reader.ReadInt32();
                if ((uint)tile >= (uint)_world.TileCount) throw new InvalidDataException("Invalid Iron-chapter terrace tile.");
                metal.IronTerraces!.Add(tile);
            }
            int facings = reader.ReadInt32();
            if (facings < 0 || facings > 8) throw new InvalidDataException("Invalid Iron-chapter frontier count.");
            metal.IronFacing = facings == 0 ? null : new List<int>(facings);
            for (int f = 0; f < facings; f++)
            {
                int civ = reader.ReadInt32();
                if (civ < 0) throw new InvalidDataException("Invalid Iron-chapter frontier civilization.");
                metal.IronFacing!.Add(civ);
            }
            metal.IronPledgeTo = reader.ReadInt32();
            if ((uint)metal.IronPledgeTo > (uint)_settlements.Count) throw new InvalidDataException("Invalid Iron-chapter pledge.");
            int faiths = reader.ReadInt32();
            if (faiths < 0 || faiths > CivilizationCount) throw new InvalidDataException("Invalid Iron-chapter faith count.");
            metal.IronFaith = faiths == 0 ? null : new Fix64[faiths];
            metal.IronFaithSeason = faiths == 0 ? null : new int[faiths];
            for (int k = 0; k < faiths; k++)
            {
                metal.IronFaith![k] = Fix64.FromRaw(reader.ReadInt64());
                metal.IronFaithSeason![k] = reader.ReadInt32();
                if (metal.IronFaith[k] < Fix64.Zero || metal.IronFaith[k] > Fix64.One) throw new InvalidDataException("Invalid Iron-chapter faith share.");
            }
        }
    }

    // ---- farming ----------------------------------------------------------------------------------------

    /// <summary>Share of a field's labor that is harvesting, the stage the AGR-09 scythe speeds (provisional; the catalog gives no split).</summary>
    private static readonly Fix64 ScytheHarvestShare = Fix64.FromRatio(1, 3);

    /// <summary>The scythe's harvest-stage output per work (AGR-09: x2).</summary>
    private static readonly Fix64 ScytheMult = Fix64.FromInt(2);

    /// <summary>The best base field yield in the world (grassland); the mouldboard plough lifts heavy forest soil to it (AGR-10).</summary>
    private static readonly Fix64 PrimeFieldYield = Fix64.FromInt(3);

    /// <summary>
    /// Extra effective farm work from an iron scythe: a farmer with a T11 or T12 kit in a civilization that knows AGR-09 gets harvest-stage output x2 on the
    /// harvest share of the work. It is extra *work*, so it only raises a harvest where the settlement was short of farm labor (its fields are filled
    /// by work, never by hand), and the kit's own harvest-labor saving is a separate, existing bonus. No population-food grant.
    /// </summary>
    private Fix64 ScytheFarmBonus(FigureState f, Fix64 work)
        => IronChain && f.Kit >= 11 && Done(f.Civilization, "AGR-09")
            ? work * SkillMultiplier(f, SkillFamily.Farm) * ScytheHarvestShare * (ScytheMult - Fix64.One)
            : Fix64.Zero;

    /// <summary>
    /// A tile's base field yield for a civilization: the mouldboard plough (AGR-10, Iron chapter) makes a heavy-soil temperate-forest plot (the forest biome
    /// with a clay deposit) prime farmland, the world's best base yield, and changes nothing else (no universal bonus).
    /// </summary>
    private Fix64 FieldBaseYield(int civ, in WorldEconomyTile tile)
        => IronChain && tile.Clay && (Biome)tile.BiomeId == Biome.TemperateForest && Done(civ, "AGR-10")
            ? Fix64.Max(tile.FieldYieldPerYear, PrimeFieldYield)
            : tile.FieldYieldPerYear;

    // ---- the state layer: administrators, provinces and the feudal contract ------------------------------------

    /// <summary>How far, in walking tiles, a province seat's governor reaches (docs/MVP/26 §3.3; provisional).</summary>
    private const int ProvinceRadius = 12;

    /// <summary>The projects whose Overreach relief the Iron chapter scopes (staffed administrator, governed province, maintained roads) instead of applying civilization-wide.</summary>
    private bool IsScopedAdminProject(int projectIndex)
    {
        string id = _content.Projects[projectIndex].Id;
        return id is "GOV-07" or "TER-08" or "COM-08";
    }

    /// <summary>The Overreach relief a scoped project's `admin` effect gives, from the content (zero when it has none).</summary>
    private Fix64 ScopedOverreach(string projectId)
    {
        foreach (EconomyEffect e in _content.Effects)
            if (e.Kind == "admin" && e.Overreach is { } r && _content.Projects[e.ProjectIndex].Id == projectId) return r;
        return Fix64.Zero;
    }

    private bool LandConnected(int siteA, int siteB) => (_walkFrom[siteA] ??= WalkingCosts(siteA))[siteB] != int.MaxValue;

    private bool HasStaffedRole(SettlementState s, byte role)
    {
        foreach (FigureState f in _figures) if (f.SettlementId == s.Id && f.Role == role && f.Age is >= 14 and < 60) return true;
        return false;
    }

    /// <summary>GOV-07: the census office works only with an administrator at the capital; a vacant office gives no relief (docs/MVP/26 §3.3).</summary>
    private bool HasStaffedAdministrator(int civ)
        => CapitalOf(civ) is int capital and >= 0 && HasStaffedRole(_settlements[capital], Role.Administrator);

    /// <summary>
    /// Whether a settlement should keep a governor (TER-08): a town other than the capital, joined to it by land, that is itself beyond the free Overreach
    /// distance or has a member settlement within the province radius that is. Few seats result: a governor takes an adult from the fields full time.
    /// </summary>
    private bool WantsGovernor(SettlementState s)
    {
        if (!IronChain || !Done(s.Civilization, "TER-08") || s.Civic is not { Tier: >= 1 } || !IsMember(s, s.Civilization)) return false;
        int capital = CapitalOf(s.Civilization);
        if (capital < 0 || capital == s.Id || !LandConnected(_settlements[capital].SiteTile, s.SiteTile)) return false;
        if (WalkingTiles(_settlements[capital].SiteTile, s.SiteTile) > _civic.OverreachFree) return true;
        foreach (SettlementState o in _settlements)
            if (o != s && IsMember(o, s.Civilization) && WalkingTiles(s.SiteTile, o.SiteTile) <= ProvinceRadius &&
                WalkingTiles(_settlements[capital].SiteTile, o.SiteTile) > _civic.OverreachFree) return true;
        return false;
    }

    /// <summary>
    /// The index of the staffed province seat that governs this settlement, or -1: the nearest settlement (by walking tiles, then lowest index) within the
    /// province radius, joined by land, whose governor is in post. The capital is governed directly. A seat whose governor has gone, or that has been cut off
    /// or captured, projects nothing, and the assignment is read from the staffed roles each time, so it is rebuilt every season without being stored.
    /// </summary>
    private int GovernorSeatOf(SettlementState s)
    {
        int civ = s.Civilization;
        if (!IronChain || !Done(civ, "TER-08")) return -1;
        int capital = CapitalOf(civ);
        if (capital < 0 || capital == s.Id) return -1;
        int best = -1, bestWalk = int.MaxValue;
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState seat = _settlements[i];
            if (!IsMember(seat, civ) || i == capital || !HasStaffedRole(seat, Role.Governor) || !LandConnected(seat.SiteTile, s.SiteTile) ||
                !LandConnected(_settlements[capital].SiteTile, seat.SiteTile)) continue;
            int walk = seat.Id == s.Id ? 0 : WalkingTiles(seat.SiteTile, s.SiteTile);
            if (walk > ProvinceRadius || walk >= bestWalk) continue;
            best = i;
            bestWalk = walk;
        }
        return best;
    }

    /// <summary>
    /// The settlement's Overreach relief: the civilization's unscoped relief, plus the census (GOV-07, only with a staffed administrator at the capital) and
    /// its province (TER-08, only in a province whose governor is in post). They combine as 1 - the product of (1 - r), as every `admin` relief does.
    /// </summary>
    private Fix64 AdminReductionAt(SettlementState s)
    {
        int civ = s.Civilization;
        Fix64 keep = Fix64.One - AdminReductionOf(civ);
        if (!IronChain) return Fix64.One - keep;
        if (Done(civ, "GOV-07") && HasStaffedAdministrator(civ)) keep *= Fix64.One - Fix64.Min(Fix64.One, ScopedOverreach("GOV-07"));
        if (GovernorSeatOf(s) >= 0) keep *= Fix64.One - Fix64.Min(Fix64.One, ScopedOverreach("TER-08"));
        // Courier relays (COM-08) work along maintained roads: the relief reaches only a road-linked settlement.
        if (Done(civ, "COM-08") && RoadLinked(s)) keep *= Fix64.One - Fix64.Min(Fix64.One, ScopedOverreach("COM-08"));
        return Fix64.One - keep;
    }

    /// <summary>True while a settlement is in a governed province and the civilization holds GOV-09: the feudal contract (docs/MVP/25 §6; Q4 resolved).</summary>
    private bool UnderFeudalContract(SettlementState s) => IronChain && Done(s.Civilization, "GOV-09") && GovernorSeatOf(s) >= 0;

    /// <summary>
    /// What is left of the distance-from-the-capital strain on Cohesion: none under a feudal contract (Q4: distance and span pressure only), half with
    /// citizenship (SOC-10), otherwise all of it. Famine, defeat and the other strains are untouched.
    /// </summary>
    private Fix64 DistanceStrainFactor(SettlementState s)
        => UnderFeudalContract(s) ? Fix64.Zero : IronChain ? Modifier(s.Civilization, "distance_contribution_to_cohesion_decay") : Fix64.One;

    /// <summary>What is left of the Admin-span strain: none under a feudal contract, otherwise all of it.</summary>
    private Fix64 SpanStrainFactor(SettlementState s) => UnderFeudalContract(s) ? Fix64.Zero : Fix64.One;

    // ---- soldiers and iron arms (WAR-07, WAR-08) ---------------------------------------------------------

    /// <summary>Upkeep of a standing soldier, in rations a season (docs/MVP/25 Q8 candidate; provisional).</summary>
    private static readonly Fix64 SoldierUpkeep = Fix64.FromRatio(1, 4);

    /// <summary>
    /// Standing soldier slots (WAR-07): a tenth of the settlement's working-age adults, a fifth once WAR-08 is known. Capacity is a ceiling, not a free army:
    /// the slots are staffed with real adults who drill all season instead of working, and are suspended while food is short (security below 0.8).
    /// </summary>
    private int SoldierSlots(SettlementState s)
    {
        if (!IronChain || !Done(s.Civilization, "WAR-07") || s.FoodSecurity < Fix64.FromRatio(8, 10)) return 0;
        int adults = 0;
        foreach (FigureState f in _figures) if (f.SettlementId == s.Id && f.Age is >= 14 and < 60 && f.Camp < 0) adults++;
        Fix64 share = Done(s.Civilization, "WAR-08") ? Fix64.FromRatio(1, 5) : Fix64.FromRatio(1, 10);
        return (Fix64.FromInt(adults) * share).FloorToInt();
    }

    /// <summary>Pays the season's soldier upkeep from food in store; soldiers whose upkeep cannot be paid do not fight as professionals this season.</summary>
    private void PaySoldierUpkeep(SettlementState s, int soldiers)
    {
        MetalState? m = s.Metal;
        if (!IronChain || m is null) return;
        Fix64 cost = SoldierUpkeep * soldiers;
        if (soldiers > 0 && FoodStores(s) >= cost) { ConsumeFoodStocks(s, cost); m.IronSoldiersSupplied = soldiers; }
        else m.IronSoldiersSupplied = 0;
    }

    /// <summary>
    /// Iron arms for the standing soldiers (WAR-08): a set for each soldier in post and one spare, of the best tier the civilization can make (steel-edge
    /// arms with MET-10). They are wanted only by a settlement with a forge; the want expands through wrought iron or steel, wood and leather or hide.
    /// </summary>
    private void IronArmsWant(SettlementState s, MetalState m, MetalPlan plan)
    {
        if (!IronChain || m.Forges <= 0 || !Done(s.Civilization, "WAR-08")) return;
        int soldiers = 0;
        foreach (FigureState f in _figures) if (f.SettlementId == s.Id && f.Role == Role.Soldier) soldiers++;
        if (soldiers == 0) return;
        Fix64 have = IronStock(s, IronGood.Arms3) + IronStock(s, IronGood.Arms4);
        IronGood tier = Done(s.Civilization, "MET-10") ? IronGood.Arms4 : IronGood.Arms3;
        plan.Terminal[IronSlot(tier)] += Fix64.Max(Fix64.Zero, Fix64.FromInt(soldiers + 1) - have);
    }

    /// <summary>
    /// The Iron chapter's craft goods are wanted in modest standing amounts, one for each twenty people, like the E3 luxuries (docs/MVP/23 §3): blown glass (CER-07),
    /// fired brick for the roofs of a town (CER-08), pressed wine and oil (FOR-08 and a press), and enough barrels for the liquids in store (WOD-07). What there is
    /// no producer for stays unmet: grapes and olives come only from grafted orchards, and nothing is created to fill a want.
    /// </summary>
    private void IronCraftWants(SettlementState s, MetalState m, MetalPlan plan)
    {
        if (!IronChain) return;
        int civ = s.Civilization;
        Fix64 perTwenty = Fix64.FromInt((Population(s.Id) + 19) / 20);
        void Want(IronGood good, Fix64 target)
        {
            Fix64 have = IronStock(s, good);
            if (target > have) plan.Terminal[IronSlot(good)] += target - have;
        }
        if (Done(civ, "CER-07") && m.Smelters > 0) Want(IronGood.BlownGlass, perTwenty); // blow_glass runs in a smelter
        if (Done(civ, "CER-08") && s.Structures[(int)Structure.Kiln] > 0) Want(IronGood.FiredBrick, perTwenty);
        if (Done(civ, "FOR-08") && Done(civ, "MEC-05") && IronWorksCount(s, IronStructure.Press) > 0)
        {
            Want(IronGood.Wine, perTwenty);
            Want(IronGood.OilPlant, perTwenty);
        }
        if (Done(civ, "WOD-07"))
        {
            Fix64 liquids = IronStock(s, IronGood.Grape) + IronStock(s, IronGood.Olive) + IronStock(s, IronGood.Wine) + IronStock(s, IronGood.OilPlant);
            Want(IronGood.Barrel, Fix64.FromInt((liquids / BarrelLitres + Fix64.FromRatio(99, 100)).FloorToInt())); // one barrel to ten litres, rounded up
        }
    }

    /// <summary>Litres of wine or oil a barrel holds (provisional).</summary>
    private static readonly Fix64 BarrelLitres = Fix64.FromInt(10);

    /// <summary>
    /// Seasonal spoilage of grapes, olives, wine and oil (registry <c>goods.yaml</c>: 20%, 10%, 20%, 10% a season), cut by 40% for the share of them that sits in
    /// barrels (WOD-07, "liquid storage loss x0.60", one barrel to ten litres). The barrel itself wears out like any good (40 seasons). Barrels in transit are not
    /// modelled: no compact carries wine or oil.
    /// </summary>
    private void SpoilIronLiquids(MetalState m)
    {
        if (m.IronGoods is not { } iron) return;
        Fix64 liquids = iron[(int)IronGood.Grape] + iron[(int)IronGood.Olive] + iron[(int)IronGood.Wine] + iron[(int)IronGood.OilPlant];
        if (liquids <= Fix64.Zero) return;
        Fix64 covered = Fix64.Min(Fix64.One, iron[(int)IronGood.Barrel] * BarrelLitres / liquids);
        Fix64 keep = Fix64.One - Fix64.FromRatio(4, 10) * covered; // share of the usual loss that still happens
        iron[(int)IronGood.Grape] -= iron[(int)IronGood.Grape] * Fix64.FromRatio(2, 10) * keep;
        iron[(int)IronGood.Olive] -= iron[(int)IronGood.Olive] * Fix64.FromRatio(1, 10) * keep;
        iron[(int)IronGood.Wine] -= iron[(int)IronGood.Wine] * Fix64.FromRatio(2, 10) * keep;
        iron[(int)IronGood.OilPlant] -= iron[(int)IronGood.OilPlant] * Fix64.FromRatio(1, 10) * keep;
    }

    /// <summary>Iron arms and siege engines wear out by age like the Bronze arms: a share of the stock each season, one over the service life (24 seasons iron, 48 steel-edge, 12 for an engine).</summary>
    private void WearIronGoods(MetalState m)
    {
        if (m.IronGoods is not { } iron) return;
        SpoilIronLiquids(m);
        foreach (IronGood g in new[] { IronGood.Arms3, IronGood.Arms4, IronGood.Engine, IronGood.Barrel })
        {
            int t = _content.TradeGoodIndex(IronGoodId(g));
            int seasons = t >= 0 ? _content.TradeGoods[t].ServiceSeasons : 0;
            if (seasons > 0 && iron[(int)g] > Fix64.Zero) iron[(int)g] -= iron[(int)g] / seasons;
        }
    }

    // ---- Wonders and leagues (LRN-06, SEA-08, BLD-11, DIP-07) -------------------------------------------

    private static bool IsWonder(IronStructure kind) => kind is IronStructure.GreatLibrary or IronStructure.GreatLighthouse or IronStructure.GreatDome;

    /// <summary>The research project a Wonder completes, which earns its Legacy.</summary>
    private static string WonderProject(IronStructure kind) => kind switch
    {
        IronStructure.GreatLibrary => "LRN-06", IronStructure.GreatLighthouse => "SEA-08", _ => "BLD-11",
    };

    /// <summary>The civilization that holds a Wonder, or -1: there is one of each in the world, and it belongs to whoever holds the town it stands in.</summary>
    private int WonderOwnerCiv(IronStructure kind)
    {
        foreach (SettlementState s in _settlements) if (IronWorksCount(s, kind) > 0) return s.Civilization;
        return -1;
    }

    /// <summary>True when the civilization holds the Great Lighthouse (SEA-08), the Great Library (LRN-06) or the Great Dome (BLD-11), named by its structure id.</summary>
    public bool HoldsWonder(int civ, string structure)
    {
        if (!IronChain) return false;
        for (int k = 0; k < IronSpecs.Length; k++)
            if (IronSpecs[k].Name == structure && IsWonder((IronStructure)k)) return WonderOwnerCiv((IronStructure)k) == civ;
        return false;
    }

    /// <summary>
    /// A civilization builds a Wonder in its most populous town of tier 1 or more (a harbor town for the Lighthouse), one at a time, when nobody holds it yet.
    /// </summary>
    private bool WonderWanted(SettlementState s, IronStructure kind)
    {
        if (s.Civic is not { Tier: >= 1 } || IronWorksCount(s, kind) > 0 || WonderOwnerCiv(kind) >= 0) return false;
        bool harbor = kind == IronStructure.GreatLighthouse;
        SettlementState? best = null;
        foreach (SettlementState o in _settlements)
        {
            if (o.Civilization != s.Civilization || o.Civic is not { Tier: >= 1 } || harbor && IronWorksCount(o, IronStructure.Harbor) == 0) continue;
            if (best is null || Population(o.Id) > Population(best.Id)) best = o;
        }
        return best?.Id == s.Id;
    }

    /// <summary>The settlement a league member's labor is pledged to, if that settlement is still building a Wonder.</summary>
    private SettlementState? PledgedWonder(MetalState m)
    {
        if (m.IronPledgeTo <= 0 || m.IronPledgeTo > _settlements.Count) return null;
        SettlementState target = _settlements[m.IronPledgeTo - 1];
        return target.Metal is { IronBuildKind: >= 0 } tm && IsWonder((IronStructure)tm.IronBuildKind) ? target : null;
    }

    /// <summary>The settlement of a civilization that is building a Wonder now, or -1 (what its league partners may pledge to).</summary>
    public int WonderUnderConstruction(int civ)
    {
        foreach (SettlementState s in _settlements)
            if (s.Civilization == civ && s.Metal is { IronBuildKind: >= 0 } m && IsWonder((IronStructure)m.IronBuildKind)) return s.Id;
        return -1;
    }

    /// <summary>The civilization's most populous town of tier 1 or more: the one it can spare to a league partner's Wonder; -1 when none.</summary>
    public int LeagueContributor(int civ)
    {
        int best = -1;
        foreach (SettlementState s in _settlements)
        {
            if (s.Civilization != civ || s.Civic is not { Tier: >= 1 } || IsFreeFolk(s)) continue;
            if (best < 0 || Population(s.Id) > Population(best)) best = s.Id;
        }
        return best;
    }

    /// <summary>Points a settlement's Iron build labor at another settlement's Wonder (-1 stops it). Set each season by politics for league members.</summary>
    public void SetWonderPledge(int settlementIndex, int builderSettlement)
    {
        if (!IronChain) return;
        SettlementState s = _settlements[settlementIndex];
        if (builderSettlement < 0 && s.Metal is null) return;
        EnsureMetal(s).IronPledgeTo = builderSettlement < 0 ? 0 : builderSettlement + 1;
    }

    /// <summary>
    /// How much faster knowledge reaches a road-linked town (TRN-07 adopt x2, COM-08 adopt x2): the distance lag of its adoption is divided by 2 with paved roads and by
    /// 4 once courier relays run on them. 1 for a town with no complete road, and always 1 outside the Iron chapter.
    /// </summary>
    public int RoadAdoptionDivisor(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        if (!IronChain || !RoadLinked(s)) return 1;
        return (Done(s.Civilization, "TRN-07") ? 2 : 1) * (Done(s.Civilization, "COM-08") ? 2 : 1);
    }

    // ---- read-outs for the view layer (all read-only; nothing here changes state) ------------------------

    /// <summary>The number of Iron works kinds, the stride of the frame's per-settlement works counts.</summary>
    public static int IronWorkKindCount => (int)IronStructure.Count;

    /// <summary>The registry id of an Iron works kind (for the front end's string table), or "" when out of range.</summary>
    public static string IronWorkId(int kind) => (uint)kind < (uint)IronSpecs.Length ? IronSpecs[kind].Name : "";

    /// <summary>How many of an Iron works kind a settlement holds.</summary>
    public int IronWorkAt(int settlementIndex, int kind)
        => IronChain && (uint)kind < (uint)IronStructure.Count ? IronWorksCount(_settlements[settlementIndex], (IronStructure)kind) : 0;

    /// <summary>The Iron structure under construction in a settlement (kind -1 when none) and its progress in permille of its labor.</summary>
    public (int Kind, int Permille) IronBuildAt(int settlementIndex)
    {
        if (!IronChain || _settlements[settlementIndex] is not { Metal: { IronBuildKind: >= 0 } m } s) return (-1, 0);
        Fix64 labor = IronLaborOf(s, IronSpecs[m.IronBuildKind]);
        return (m.IronBuildKind, labor <= Fix64.Zero ? 1000 : (int)Math.Clamp((m.IronBuildProgress * 1000 / labor).FloorToInt(), 0, 1000));
    }

    /// <summary>Soldiers in post and fed this season at a settlement (the supplied count the army uses).</summary>
    public int SoldiersAt(int settlementIndex) => IronChain ? _settlements[settlementIndex].Metal?.IronSoldiersSupplied ?? 0 : 0;

    public int ScholarsAt(int settlementIndex)
    {
        if (!IronChain) return 0;
        int n = 0;
        foreach (FigureState f in _figures) if (f.SettlementId == _settlements[settlementIndex].Id && f.Role == Role.Scholar) n++;
        return n;
    }

    /// <summary>The settlement a town's Iron build labor is pledged to this season (a league contribution), or -1.</summary>
    public int PledgeOf(int settlementIndex) => IronChain ? (_settlements[settlementIndex].Metal?.IronPledgeTo ?? 0) - 1 : -1;

    /// <summary>The settlement holding a Wonder (named by its structure id), or -1 when nobody holds it.</summary>
    public int WonderSettlement(string structure)
    {
        if (!IronChain) return -1;
        for (int k = 0; k < IronSpecs.Length; k++)
            if (IronSpecs[k].Name == structure && IsWonder((IronStructure)k))
                foreach (SettlementState s in _settlements) if (IronWorksCount(s, (IronStructure)k) > 0) return s.Id;
        return -1;
    }

    /// <summary>
    /// How many of a civilization's staffed institutions stand idle (docs/MVP/25 §11 "idle staffed institutions"): an academy with no scholar, a castle with no
    /// garrison, a galley with no crew, a temple with no missionary once BEL-09 is known. Telemetry for the AI blunder counters; it changes nothing.
    /// </summary>
    public int IronIdleInstitutions(int civ)
    {
        if (!IronChain) return 0;
        int idle = 0;
        foreach (SettlementState s in _settlements)
        {
            if (s.Civilization != civ || IsFreeFolk(s)) continue;
            int scholars = 0, missionaries = 0;
            foreach (FigureState f in _figures)
            {
                if (f.SettlementId != s.Id) continue;
                if (f.Role == Role.Scholar) scholars++;
                else if (f.Role == Role.Missionary) missionaries++;
            }
            if (Done(civ, "LRN-05") && IronWorksCount(s, IronStructure.Academy) > scholars) idle++;
            if (IronWorksCount(s, IronStructure.Castle) > 0 && !CastleGarrisoned(s.Id)) idle++;
            if (IronWorksCount(s, IronStructure.Galley) > 0 && !CrewedGalley(s.Id)) idle++;
            if (Done(civ, "BEL-09") && s.Civic is { Temples: > 0 } && missionaries == 0) idle++;
        }
        return idle;
    }

    /// <summary>The settlement is a province seat or member under a feudal contract (GOV-09): its local defense is stronger.</summary>
    public bool UnderFeudalContractAt(int settlementIndex) => UnderFeudalContract(_settlements[settlementIndex]);

    // ---- walls, castles and siege engines (DEF-06 to DEF-08, WAR-09) -------------------------------------

    /// <summary>A land tile with a sea (ocean or coast) tile beside it: a place a ship can tie up. Lakes and rivers are not harbors.</summary>
    private bool IsPortSite(int tile)
    {
        Span<int> neighbors = stackalloc int[6];
        int count = HexGrid.Neighbors(tile, _world.Width, _world.Height, neighbors);
        for (int k = 0; k < count; k++)
            if ((Biome)_world.Tile(neighbors[k]).BiomeId is Biome.Ocean or Biome.Coast) return true;
        return false;
    }

    /// <summary>Academies one civilization keeps (provisional): scholars are the largest single Insight role, so they are bounded by buildings, not by demand.</summary>
    private const int MaxAcademies = 3;

    private int CivWorksCount(int civ, IronStructure kind)
    {
        int n = 0;
        foreach (SettlementState o in _settlements) if (o.Civilization == civ) n += IronWorksCount(o, kind);
        return n;
    }

    /// <summary>One scholar for each academy, once the civilization knows LRN-05 (the role is staffed from the town's adults, as a priest is; it does not create a person).</summary>
    private int ScholarSlots(SettlementState s)
        => IronChain && Done(s.Civilization, "LRN-05") ? IronWorksCount(s, IronStructure.Academy) : 0;

    /// <summary>Scholars in post across a civilization, for the research system's Insight (48 a year each in the registry).</summary>
    public int ScholarHeads(int civ)
    {
        if (!IronChain) return 0;
        int n = 0;
        foreach (FigureState f in _figures)
            if (f.Role == Role.Scholar && f.Civilization == civ) n++;
        return n;
    }

    /// <summary>Soldiers who crew a war galley (docs/MVP/26 §4 asks for a crew; four, like an engine, provisional).</summary>
    private const int GalleyCrew = 4;

    /// <summary>A seaport's group cap multiplier (SEA-07 port_city_cap_support).</summary>
    private static readonly Fix64 PortGroupCapMult = Fix64.FromRatio(12, 10);

    /// <summary>How far a frontier segment reaches: a rival settlement this close is a neighbour worth a wall (hex tiles; provisional).</summary>
    private const int FrontierReach = 6;
    /// <summary>A castle is built on a border town: a rival settlement within this many tiles (provisional).</summary>
    private const int CastleReach = 8;
    /// <summary>Soldiers needed in post for a castle to hold its region (the catalog says "garrison required"; the Captures minimum is four).</summary>
    private const int CastleGarrison = 4;
    /// <summary>Claim strength of a garrisoned castle's settlement (docs/MVP/26 §3.2 "strong claim anchor"; radius is already 5 in the Iron era; provisional).</summary>
    private static readonly Fix64 CastleClaimMult = Fix64.FromRatio(12, 10);
    /// <summary>Charcoal an engine in the field burns each season (docs/MVP/26 §3.2; provisional).</summary>
    private static readonly Fix64 EngineUpkeep = Fix64.FromRatio(1, 5);

    private bool HasStoneWalls(SettlementState s) => s.Structures[(int)Structure.StoneWalls] > 0;

    /// <summary>Hex distance to the nearest settlement of another civilization (Free Folk excluded), or a large number when none.</summary>
    private int NearestForeignDistance(SettlementState s)
    {
        int best = int.MaxValue;
        foreach (SettlementState o in _settlements)
        {
            if (o.Civilization == s.Civilization || o.Civilization < 0 || IsFreeFolk(o)) continue;
            best = Math.Min(best, HexGrid.Distance(o.SiteTile, s.SiteTile, _world.Width));
        }
        return best;
    }

    private bool IsFaced(SettlementState s, int civ) => s.Metal?.IronFacing?.Contains(civ) == true;

    /// <summary>The lowest-numbered rival civilization with a settlement within <see cref="FrontierReach"/> that none of this settlement's segments face yet; -1 when none.</summary>
    private int NextFrontierFacing(SettlementState s)
    {
        int best = -1;
        foreach (SettlementState o in _settlements)
        {
            if (o.Civilization == s.Civilization || o.Civilization < 0 || IsFreeFolk(o) || IsFaced(s, o.Civilization)) continue;
            if (HexGrid.Distance(o.SiteTile, s.SiteTile, _world.Width) > FrontierReach) continue;
            if (best < 0 || o.Civilization < best) best = o.Civilization;
        }
        return best;
    }

    /// <summary>
    /// A harbor stands in the settlement (SEA-07). Its port city holds 20% more people in its group cap, so a seaport can grow past an inland town of the same food,
    /// housing and water; those three still bind separately, so it never creates food, water or houses. It is also the end of a harbor-to-harbor trade route.
    /// </summary>
    public bool HasHarbor(int settlementIndex)
        => IronChain && IronWorksCount(_settlements[settlementIndex], IronStructure.Harbor) > 0;

    /// <summary>A war galley lies at this harbor with a crew of four supplied soldiers in post (SEA-09): the only ship that can blockade.</summary>
    public bool CrewedGalley(int settlementIndex)
        => IronChain && _settlements[settlementIndex] is { } s && IronWorksCount(s, IronStructure.Harbor) > 0 && IronWorksCount(s, IronStructure.Galley) > 0 &&
           s.Metal is { } m && m.IronSoldiersSupplied >= GalleyCrew;

    /// <summary>The galley is wrecked or beaten and is gone.</summary>
    public void LoseGalley(int settlementIndex)
    {
        if (IronChain && _settlements[settlementIndex].Metal?.IronWorks is { } works && works[(int)IronStructure.Galley] > 0) works[(int)IronStructure.Galley]--;
    }

    /// <summary>A gatehouse stands on the settlement's wall (DEF-06): the wall's intact multiplier rises from 3.0 to 4.5.</summary>
    public bool HasGatehouse(int settlementIndex)
        => IronChain && _settlements[settlementIndex] is { } s && HasStoneWalls(s) && IronWorksCount(s, IronStructure.Gatehouse) > 0;

    /// <summary>A built frontier segment of this settlement faces the civilization (DEF-07): crossing it is harder, and only it.</summary>
    public bool FrontierWallFacing(int settlementIndex, int civ)
        => IronChain && _settlements[settlementIndex] is { } s && IronWorksCount(s, IronStructure.FrontierWall) > 0 && IsFaced(s, civ);

    /// <summary>A castle stands here and its garrison is in post and fed (DEF-08, WAR-07): the settlement is a strongpoint and a strong claim anchor.</summary>
    public bool CastleGarrisoned(int settlementIndex)
        => IronChain && _settlements[settlementIndex] is { } s && IronWorksCount(s, IronStructure.Castle) > 0 && s.Metal is { } m && m.IronSoldiersSupplied >= CastleGarrison;

    /// <summary>
    /// A professional army keeps a siege train (WAR-09): a forge town with soldiers wants one engine for every four of them, at most two. An engine is
    /// forged from wood, blocks and wrought iron (<c>make_siege_engine</c>); the want expands through the same graph as the arms.
    /// </summary>
    private void IronEngineWant(SettlementState s, MetalState m, MetalPlan plan)
    {
        if (!IronChain || m.Forges <= 0 || !Done(s.Civilization, "WAR-09")) return;
        int soldiers = m.IronSoldiersSupplied;
        int wanted = Math.Min(2, soldiers / 4);
        if (wanted <= 0) return;
        plan.Terminal[IronSlot(IronGood.Engine)] += Fix64.Max(Fix64.Zero, Fix64.FromInt(wanted) - IronStock(s, IronGood.Engine));
    }

    /// <summary>Takes up to <paramref name="max"/> whole engines from a settlement's store for a campaign train; returns how many left.</summary>
    public int TakeEngines(int settlementIndex, int max)
    {
        if (!IronChain || max <= 0) return 0;
        SettlementState s = _settlements[settlementIndex];
        int n = Math.Min(max, IronStock(s, IronGood.Engine).FloorToInt());
        if (n > 0) AddStock(s, IronSlot(IronGood.Engine), -Fix64.FromInt(n));
        return n;
    }

    /// <summary>Returns engines to a settlement's store when a siege ends (they came home with the host).</summary>
    public void ReturnEngines(int settlementIndex, int count)
    {
        if (!IronChain || count <= 0) return;
        AddStock(_settlements[settlementIndex], IronSlot(IronGood.Engine), Fix64.FromInt(count));
    }

    /// <summary>
    /// Pays one season of engine upkeep from the home settlement's charcoal (0.2 each) and returns how many engines ran; an engine whose charcoal is not in
    /// store stands idle that season (docs/MVP/25 §7: the train must be supplied).
    /// </summary>
    public int PayEngineUpkeep(int homeSettlement, int engines)
    {
        if (!IronChain || engines <= 0 || (uint)homeSettlement >= (uint)_settlements.Count) return 0;
        SettlementState s = _settlements[homeSettlement];
        int slot = MetalSlot(MetalGood.Charcoal);
        int running = Math.Min(engines, (Stock(s, slot) / EngineUpkeep).FloorToInt());
        if (running > 0) AddStock(s, slot, -EngineUpkeep * running);
        return running;
    }

    // ---- organized faith (BEL-09) ----------------------------------------------------------------------

    /// <summary>One year of a missionary's conversion: 1% of the eligible non-adherents (docs/MVP/25 §6); a season is a quarter of it.</summary>
    private static readonly Fix64 ConversionPerSeason = Fix64.FromRatio(1, 400);
    /// <summary>No settlement holds more than a quarter of its people in one foreign faith.</summary>
    private static readonly Fix64 ConversionCap = Fix64.FromRatio(1, 4);
    /// <summary>Extra claim pressure a foreign faith lends its civilization in a settlement where it is spreading, at the cap (BEL-09 soft_claim_pressure +5%).</summary>
    private static readonly Fix64 FaithClaimPressure = Fix64.FromRatio(5, 100);

    /// <summary>One missionary for each Temple, once the civilization knows Organized Faith (a role staffed by an adult who preaches all season).</summary>
    private int MissionarySlots(SettlementState s)
        => IronChain && Done(s.Civilization, "BEL-09") && s.Civic is { Temples: > 0 } c ? c.Temples : 0;

    /// <summary>How many missionaries are in post at a settlement.</summary>
    public int Missionaries(int settlementIndex)
    {
        if (!IronChain) return 0;
        int n = 0;
        foreach (FigureState f in _figures) if (f.SettlementId == _settlements[settlementIndex].Id && f.Role == Role.Missionary) n++;
        return n;
    }

    /// <summary>The share of a settlement's people who follow a civilization's faith (0 until a missionary has come).</summary>
    public Fix64 FaithShare(int settlementIndex, int faithCiv)
        => IronChain && _settlements[settlementIndex].Metal?.IronFaith is { } faith && (uint)faithCiv < (uint)faith.Length ? faith[faithCiv] : Fix64.Zero;

    /// <summary>
    /// One missionary-season of preaching (BEL-09): converts 1% a year of the people who do not yet follow the faith, never past a quarter of the settlement, never in
    /// the faith's own civilization. Ownership never changes. Returns the share converted this season.
    /// </summary>
    public Fix64 ConvertTo(int settlementIndex, int faithCiv, int season)
    {
        SettlementState s = _settlements[settlementIndex];
        if (!IronChain || faithCiv == s.Civilization || (uint)faithCiv >= (uint)CivilizationCount) return Fix64.Zero;
        MetalState m = EnsureMetal(s);
        if (m.IronFaith is null)
        {
            m.IronFaith = new Fix64[CivilizationCount];
            m.IronFaithSeason = new int[CivilizationCount];
            Array.Fill(m.IronFaithSeason, -1);
        }
        Fix64 now = m.IronFaith[faithCiv];
        Fix64 gain = Fix64.Min(ConversionCap - now, ConversionPerSeason * (Fix64.One - now));
        if (gain > Fix64.Zero) m.IronFaith[faithCiv] = now + gain;
        m.IronFaithSeason![faithCiv] = season; // the preacher came, even if the settlement is at its cap
        return Fix64.Max(Fix64.Zero, gain);
    }

    /// <summary>
    /// The claim emission a faith lends its civilization at a settlement, as a share of the settlement's own emission: up to 5% at a quarter converted, and only while a
    /// missionary keeps coming (this season or the last). It adds to the faith civilization's claim around the town and never moves the town itself.
    /// </summary>
    public Fix64 FaithClaimShare(int settlementIndex, int faithCiv, int season)
    {
        if (!IronChain || _settlements[settlementIndex].Metal is not { IronFaith: { } faith, IronFaithSeason: { } last } || (uint)faithCiv >= (uint)faith.Length) return Fix64.Zero;
        return faith[faithCiv] > Fix64.Zero && season - last[faithCiv] <= 1 ? faith[faithCiv] / ConversionCap * FaithClaimPressure : Fix64.Zero;
    }

    // ---- roads, bridges and relays (TRN-07, TRN-08, COM-08) ------------------------------------------------

    /// <summary>River tiles on the straight line from the capital to a settlement: a road over them is incomplete without a bridge (TRN-08). Zero for the capital.</summary>
    private int RiversOnLink(SettlementState s)
    {
        int capital = CapitalOf(s.Civilization);
        if (capital < 0 || capital == s.Id) return 0;
        HexGrid.Coordinates(_settlements[capital].SiteTile, _world.Width, out int fx, out int fy);
        HexGrid.Coordinates(s.SiteTile, _world.Width, out int tx, out int ty);
        int steps = Math.Max(1, HexGrid.Distance(_settlements[capital].SiteTile, s.SiteTile, _world.Width));
        int rivers = 0;
        for (int k = 0; k <= steps; k++)
            if (_world.Tile(HexGrid.Index(fx + (tx - fx) * k / steps, fy + (ty - fy) * k / steps, _world.Width, _world.Height)).River) rivers++;
        return rivers;
    }

    /// <summary>
    /// The tiles of a settlement's paved link to its capital, from the capital to the settlement along the same straight line the bridge and Cohesion rules use, when the
    /// link is complete (a road, and a bridge where it crosses a river) and the civilization knows TRN-07. Empty otherwise. Politics adds the road's claim bonus on them.
    /// </summary>
    public void RoadClaimTiles(int settlementIndex, List<int> into)
    {
        SettlementState s = _settlements[settlementIndex];
        if (!IronChain || !Done(s.Civilization, "TRN-07") || !RoadLinked(s)) return;
        int capital = CapitalOf(s.Civilization);
        if (capital < 0 || capital == s.Id) return;
        HexGrid.Coordinates(_settlements[capital].SiteTile, _world.Width, out int fx, out int fy);
        HexGrid.Coordinates(s.SiteTile, _world.Width, out int tx, out int ty);
        int steps = Math.Max(1, HexGrid.Distance(_settlements[capital].SiteTile, s.SiteTile, _world.Width));
        for (int k = 0; k <= steps; k++) into.Add(HexGrid.Index(fx + (tx - fx) * k / steps, fy + (ty - fy) * k / steps, _world.Width, _world.Height));
    }

    private bool RoadWanted(SettlementState s)
    {
        int civ = s.Civilization;
        if (IronWorksCount(s, IronStructure.Road) > 0 || !IsMember(s, civ)) return false;
        int capital = CapitalOf(civ);
        return capital >= 0 && capital != s.Id && LandConnected(_settlements[capital].SiteTile, s.SiteTile) &&
            WalkingTiles(_settlements[capital].SiteTile, s.SiteTile) > _civic.OverreachFree;
    }

    /// <summary>A settlement is road-linked to its capital when it has a paved link and, if that link crosses a river, a bridge too.</summary>
    private bool RoadLinked(SettlementState s)
        => IronChain && IronWorksCount(s, IronStructure.Road) > 0 && (RiversOnLink(s) == 0 || IronWorksCount(s, IronStructure.Bridge) > 0);

    /// <summary>
    /// The walking distance to the capital as the Cohesion strain sees it: a road-linked settlement moves x1.5 faster along its link (TRN-07), so the
    /// distance counts as two thirds, rounded up. Unlinked settlements, and every Bronze settlement, keep the plain walk.
    /// </summary>
    private int RoadAdjustedWalk(SettlementState s, int walk)
        => RoadLinked(s) && Done(s.Civilization, "TRN-07") ? (walk * 2 + 2) / 3 : walk;

    // ---- orchards (FOR-08) ---------------------------------------------------------------------------------

    /// <summary>Orchard plots one iron-knife holder can graft and keep grafted (provisional).</summary>
    private const int GraftedPlotsPerBlade = 2;

    /// <summary>
    /// FOR-08 grafting: a mature orchard plot gives twice the fruit when it is grafted, and a plot is grafted when a T11 or T12 kit holder of the settlement has
    /// it among the (two) plots he or she keeps (plots in order). Without an iron blade no plot is grafted; a Bronze economy never grafts. Fruit is the only
    /// output here: wine and oil need the press chain, which is not built yet.
    /// </summary>
    private Fix64 GraftedOrchardFactor(SettlementState s, int plotIndex)
    {
        if (!IronChain || !Done(s.Civilization, "FOR-08")) return Fix64.One;
        int blades = 0;
        foreach (FigureState f in _figures) if (f.SettlementId == s.Id && f.Age >= 14 && f.Kit >= 11) blades++;
        return plotIndex < blades * GraftedPlotsPerBlade ? Fix64.FromInt(2) : Fix64.One;
    }

    /// <summary>
    /// A grafted orchard plot also gives a litre of grapes or olives a year (docs/MVP/26 §2.1: 1 L per orchard work-season of the grafted plot): olives on dry ground
    /// (grassland, savanna, open woodland), grapes on wet or wooded ground (forest, wetland), none elsewhere. They are goods for pressing and trade, not food in store,
    /// so they feed no one and create no calories.
    /// </summary>
    private void HarvestGrafted(SettlementState s, OrchardPlot plot, int plotIndex)
    {
        if (!IronChain || GraftedOrchardFactor(s, plotIndex) <= Fix64.One) return;
        IronGood crop = (Biome)_world.Tile(plot.Tile).BiomeId switch
        {
            Biome.GrasslandSteppe or Biome.Savanna or Biome.OpenWoodland => IronGood.Olive,
            Biome.TemperateForest or Biome.TropicalForest or Biome.Wetland => IronGood.Grape,
            _ => IronGood.Count,
        };
        if (crop != IronGood.Count) AddStock(s, IronSlot(crop), Fix64.One);
    }

    // ---- public grain (FOD-09) ----------------------------------------------------------------------------

    /// <summary>The most a town can take in as public grain in a season, as a share of a ration per person (the transfer's capacity limit; provisional).</summary>
    private static readonly Fix64 PublicGrainRate = Fix64.FromRatio(1, 4);

    /// <summary>
    /// Public grain supply (docs/MVP/26 §5.2, FOD-09): a town or city holds a reserve target of one ration per person, and grain moves to it from sister
    /// settlements in sharing reach, but only the grain a donor holds above its own one-season winter need, and at most a quarter-ration per person per
    /// season. Physical grain moves from one store to another, so none is created; the catalog's "+25% cap" is the outcome the oracle looks for, not a multiplier
    /// applied here. Returns the grain units moved, for tests and screens.
    /// </summary>
    private Fix64 SharePublicGrain(int civ, int reach)
    {
        if (!Done(civ, "FOD-09")) return Fix64.Zero;
        Fix64 movedTotal = Fix64.Zero;
        for (int r = 0; r < _settlements.Count; r++)
        {
            SettlementState receiver = _settlements[r];
            int pop = Population(receiver.Id);
            if (receiver.Civilization != civ || IsFreeFolk(receiver) || pop == 0 || receiver.Civic is not { Tier: >= 1 }) continue;
            Fix64 need = Fix64.Min(Fix64.FromInt(pop) - FoodStores(receiver), Fix64.FromInt(pop) * PublicGrainRate);
            Fix64 receiverValue = FoodValue(Good.Grain, receiver);
            for (int d = 0; d < _settlements.Count && need > Epsilon && receiverValue > Fix64.Zero; d++)
            {
                SettlementState donor = _settlements[d];
                if (d == r || donor.Civilization != civ || IsFreeFolk(donor) || Population(donor.Id) == 0 ||
                    HexGrid.Distance(donor.SiteTile, receiver.SiteTile, _world.Width) > reach) continue;
                Fix64 surplus = FoodStores(donor) - FoodNeed(donor, true); // above its one-season winter reserve
                Fix64 donorValue = FoodValue(Good.Grain, donor);
                if (surplus <= Epsilon || donorValue <= Fix64.Zero) continue;
                Fix64 units = Fix64.Min(Fix64.Min(need / receiverValue, surplus / donorValue), donor.Stores[(int)Good.Grain]);
                if (units <= Epsilon) continue;
                donor.Stores[(int)Good.Grain] -= units;
                receiver.Stores[(int)Good.Grain] += units;
                need -= units * receiverValue;
                movedTotal += units;
            }
        }
        return movedTotal;
    }

    // ---- water and health -------------------------------------------------------------------------------

    /// <summary>A town's water-limited size before any aqueduct: 60 people plus 8 for each fresh-water tile in its range, at most 150 (provisional; the oracle screen used 80).</summary>
    private Fix64 WaterCapBase(SettlementState s)
    {
        int fresh = 0;
        foreach (int tile in RangeTiles(s.SiteTile))
        {
            WorldEconomyTile t = _world.Tile(tile);
            if (t.River || t.Lake || t.FreshWater) fresh++;
        }
        // A windlass well (MEC-04) reaches a deep source and counts as a fresh-water tile; it adds no new source to the map.
        return Fix64.FromInt(Math.Min(150, 60 + 8 * (fresh + IronWorksCount(s, IronStructure.WindlassWell))));
    }

    /// <summary>
    /// The water-limited size of a town (docs/MVP/25 §5, 26 §3.2): an aqueduct raises it by 40%, and it creates neither food nor housing. It binds only in the
    /// Iron chapter once a civilization has its own KEY-4 (E4 is hidden before then); otherwise there is no limit. It is one more term in the cap that
    /// makes a town bud, so a town short of water splits instead of crowding.
    /// </summary>
    private Fix64 WaterCap(SettlementState s)
    {
        if (!IronChain || !Done(s.Civilization, "KEY-4")) return Plenty;
        Fix64 cap = WaterCapBase(s);
        return IronWorksCount(s, IronStructure.Aqueduct) > 0 ? cap * Fix64.FromRatio(14, 10) : cap;
    }

    /// <summary>Density-disease pressure multiplier from an aqueduct (x0.85) and a bathhouse (x0.75) in the Iron chapter; they multiply when both stand.</summary>
    private Fix64 IronSanitationFactor(SettlementState s)
    {
        if (!IronChain) return Fix64.One;
        Fix64 factor = Fix64.One;
        if (IronWorksCount(s, IronStructure.Aqueduct) > 0) factor *= Fix64.FromRatio(85, 100);
        if (IronWorksCount(s, IronStructure.Bathhouse) > 0) factor *= Fix64.FromRatio(75, 100);
        return factor;
    }

    /// <summary>
    /// MED-05 (medical texts) strengthens the care effects already in play by 15% of their reduction, without compounding on itself: a care
    /// multiplier m becomes 1 - (1 - m) x 1.15 (0.70 becomes 0.655). It applies in the Iron chapter to remedies, wound care, midwives and sickness
    /// care; the physician role is a separate piece.
    /// </summary>
    private Fix64 MedCare(int civ, Fix64 multiplier)
        => IronChain && Done(civ, "MED-05") ? Fix64.Max(Fix64.Zero, Fix64.One - (Fix64.One - multiplier) * Fix64.FromRatio(115, 100)) : multiplier;

    // ---- construction ---------------------------------------------------------------------------------

    /// <summary>
    /// Structures only the Iron chapter builds (docs/MVP/26 §3.2). They are kept apart from the E2/E3 <c>Structure</c> array, which every Bronze hash
    /// covers. Append only; the order is the order a settlement considers them.
    /// </summary>
    private enum IronStructure : byte
    {
        Crane, WindlassWell, Terrace, Aqueduct, Bathhouse, Theatre, Road, Bridge, Gatehouse, FrontierWall, Castle, Harbor, Galley, Academy, GreatLibrary, GreatLighthouse, GreatDome, Press,
        Count
    }

    /// <summary>What a structure needs: its unlocking project, labor in work-seasons, and materials paid at completion (docs/MVP/26 §3.2; all provisional).</summary>
    private readonly record struct IronSpec(string Name, string Unlock, int Labor, int Block, int Ashlar, int Wood, int Bloom,
        bool Large = false, bool Stone = false, bool Infrastructure = false);

    // Large: a treadwheel crane halves the labor (MEC-06). Stone: STN-12 shortens stone work. Infrastructure: surveying (NUM-07) shortens fields, roads and aqueducts.
    private static readonly IronSpec[] IronSpecs =
    {
        new("crane", "MEC-06", 16, 0, 0, 8, 2),
        new("windlass_well", "MEC-04", 8, 0, 0, 4, 0),
        new("terrace", "AGR-08", 8, 4, 0, 2, 0, Infrastructure: true),
        new("aqueduct", "BLD-10", 40, 20, 8, 5, 0, Large: true, Stone: true, Infrastructure: true),
        new("bathhouse", "MED-06", 20, 8, 0, 4, 0, Stone: true),
        new("theatre_arena", "ART-08", 24, 12, 0, 4, 0, Stone: true),
        // A paved link from a settlement to its capital (provisional: the catalog gives no road cost). Surveying shortens it; a bridge is the 26 §3.2 row.
        new("road", "TRN-07", 12, 6, 0, 0, 0, Infrastructure: true),
        new("bridge", "TRN-08", 16, 6, 2, 2, 0, Stone: true),
        // Defense works (docs/MVP/26 §3.2): a gatehouse on a wall, a frontier segment facing one neighbour, a castle on the border.
        new("gatehouse", "DEF-06", 30, 12, 0, 6, 0, Stone: true),
        new("frontier_wall_segment", "DEF-07", 12, 4, 0, 2, 0, Stone: true),
        new("castle", "DEF-08", 60, 0, 20, 12, 2, Large: true, Stone: true),
        // A harbor on a coast site (docs/MVP/26 §3.2): piers of block and timber.
        new("harbor", "SEA-07", 24, 8, 0, 12, 0, Stone: true),
        // A war galley (docs/MVP/25 §7): timber and iron fittings, laid down at a harbor.
        new("galley", "SEA-09", 20, 0, 0, 14, 1),
        // An academy: a hall of block and timber (docs/MVP/26 §3.2).
        new("academy", "LRN-05", 16, 8, 0, 2, 0, Stone: true),
        // The three Iron Wonders (docs/MVP/25 Q7: buildable as normal Wonders; the catalog gives no cost, so these are provisional and sized like the E3 ziggurat's 200 work-seasons).
        new("great_library", "LRN-06", 150, 60, 40, 30, 4, Large: true, Stone: true),
        new("great_lighthouse", "SEA-08", 120, 40, 30, 40, 4, Large: true, Stone: true),
        new("great_dome", "BLD-11", 180, 80, 60, 20, 6, Large: true, Stone: true),
        // A screw press for wine and oil (docs/MVP/26 §3.2): timber and two litres of bloom for the screw.
        new("press", "MEC-05", 8, 0, 0, 4, 2),
    };

    /// <summary>
    /// The labor a structure costs this settlement: its listed work-seasons, shortened by the civilization's general construction research (BLD-11, MEC-04:
    /// stat <c>build_labor</c>), stone-work research (STN-12), surveying for fields, roads and aqueducts (NUM-07, an efficiency of 1.15 meaning labor /1.15), and
    /// a crane for large works (x0.5). Each factor is exactly 1 until its project is complete, so the Bronze economy is unaffected.
    /// </summary>
    private Fix64 IronLaborOf(SettlementState s, IronSpec spec)
    {
        int civ = s.Civilization;
        Fix64 labor = Fix64.FromInt(spec.Labor) * Modifier(civ, "build_labor");
        if (spec.Stone) labor *= Modifier(civ, "stone_build_labor");
        if (spec.Infrastructure)
        {
            Fix64 efficiency = Modifier(civ, "infrastructure_efficiency");
            if (efficiency > Fix64.Zero) labor /= efficiency;
        }
        if (spec.Large && IronWorksCount(s, IronStructure.Crane) > 0) labor *= Fix64.Half;
        return labor;
    }

    /// <summary>The share of a settlement's work the Iron construction bucket may take in a season, beside the metal chain and civic works (provisional).</summary>
    private static readonly Fix64 IronBuildShare = Fix64.FromRatio(1, 10);

    /// <summary>Crop output of a terraced hillside plot, in the same unit as a tile's field yield (provisional; a flat tile yields 0.6 to 3.0 by biome).</summary>
    private static readonly Fix64 TerraceYield = Fix64.FromInt(2);

    private int IronWorksCount(SettlementState s, IronStructure kind) => s.Metal?.IronWorks is { } w ? w[(int)kind] : 0;

    private int IronTerraceCount(SettlementState s) => s.Metal?.IronTerraces?.Count ?? 0;

    private bool IsIronTerrace(SettlementState s, int tile) => s.Metal?.IronTerraces?.Contains(tile) == true;

    /// <summary>The next hillside tile a settlement could terrace: a Mountain tile in range that is no one's field or orchard, lowest index first; -1 when none.</summary>
    private int NextTerraceTile(SettlementState s)
    {
        int best = -1;
        foreach (int tile in RangeTiles(s.SiteTile))
        {
            if ((Biome)_world.Tile(tile).BiomeId != Biome.Mountain || _fieldOwner[tile] >= 0 || IsOrchardOccupied(tile)) continue;
            if (best < 0 || tile < best) best = tile;
        }
        return best;
    }

    /// <summary>True when every flat tile the settlement can field is already a field, or its field allowance is used: the case for terracing a hillside.</summary>
    private bool NaturalFieldsFull(SettlementState s)
    {
        int natural = s.FieldTiles.Count - IronTerraceCount(s);
        if (natural >= FieldLimit(s) - IronTerraceCount(s)) return true;
        foreach (int tile in RangeTiles(s.SiteTile))
            if (_fieldOwner[tile] < 0 && !IsOrchardOccupied(tile) && _world.Tile(tile).FieldYieldPerYear > Fix64.Zero) return false;
        return true;
    }

    private bool IronBuildWanted(SettlementState s, IronStructure kind)
        => kind switch
        {
            // A crane is worth building when a large work is wanted and none stands yet (it halves the labor).
            IronStructure.Crane => IronWorksCount(s, IronStructure.Crane) == 0 && (IronBuildWanted(s, IronStructure.Aqueduct) || IronBuildWanted(s, IronStructure.Castle)),
            // A windlass well adds a deep water source where water, not food or housing, holds a town back (at most two).
            IronStructure.WindlassWell => IronWorksCount(s, IronStructure.WindlassWell) < 2 && s.Civic is { Tier: >= 1 } &&
                WaterCapBase(s) <= Fix64.Min(FoodCap(s), Fix64.Min(HousingCap(s), GroupCap(s))) &&
                Fix64.FromInt(Population(s.Id)) >= WaterCapBase(s) * Fix64.FromRatio(85, 100) && IronWorksCount(s, IronStructure.Aqueduct) == 0,
            IronStructure.Terrace => Population(s.Id) >= 12 && NextTerraceTile(s) >= 0 && NaturalFieldsFull(s),
            // A town on the way to its water limit builds an aqueduct only if water is what holds it back (not food, housing or its group).
            IronStructure.Aqueduct => s.Civic is { Tier: >= 1 } && IronWorksCount(s, IronStructure.Aqueduct) == 0 &&
                WaterCapBase(s) <= Fix64.Min(FoodCap(s), Fix64.Min(HousingCap(s), GroupCap(s))) &&
                Fix64.FromInt(Population(s.Id)) >= WaterCapBase(s) * Fix64.FromRatio(85, 100),
            // Baths pay off where people are dense enough for outbreaks to bite.
            IronStructure.Bathhouse => s.Civic is { Tier: >= 1 } && IronWorksCount(s, IronStructure.Bathhouse) == 0 && Population(s.Id) >= 40,
            // One venue per city: a town of fifty or more that has none builds one (it lifts that city's Cohesion target).
            // A paved link to the capital pays where there is distance strain to relieve; a bridge where that link crosses a river.
            IronStructure.Road => RoadWanted(s),
            IronStructure.Bridge => IronWorksCount(s, IronStructure.Road) > 0 && IronWorksCount(s, IronStructure.Bridge) == 0 && RiversOnLink(s) > 0,
            IronStructure.Theatre => s.Civic is { Tier: >= 1 } && IronWorksCount(s, IronStructure.Theatre) == 0 && Population(s.Id) >= 50,
            // Defense works follow the wall and the neighbour: a gatehouse on a walled town, a segment toward each close rival, a castle on a walled border town.
            IronStructure.Gatehouse => s.Civic is { Tier: >= 1 } && IronWorksCount(s, IronStructure.Gatehouse) == 0 && HasStoneWalls(s),
            IronStructure.FrontierWall => s.Civic is { Tier: >= 1 } && IronWorksCount(s, IronStructure.FrontierWall) < 2 && NextFrontierFacing(s) >= 0,
            // A coast town of some size builds one harbor: it is the port of every route that leaves by water.
            IronStructure.Harbor => s.Civic is { Tier: >= 1 } && IronWorksCount(s, IronStructure.Harbor) == 0 && IsPortSite(s.SiteTile) && Population(s.Id) >= 30,
            // A harbor town with a standing guard keeps one galley: the ship is built once and lost only to a wreck or a defeat.
            IronStructure.Galley => s.Civic is { Tier: >= 1 } && IronWorksCount(s, IronStructure.Harbor) > 0 && IronWorksCount(s, IronStructure.Galley) == 0 &&
                Done(s.Civilization, "WAR-07") && s.Metal is { } gm && gm.IronSoldiersSupplied >= GalleyCrew,
            // A town of some size builds an academy; a civilization keeps at most three (provisional): each staffed scholar is worth 48 Insight a year.
            IronStructure.Academy => s.Civic is { Tier: >= 1 } && IronWorksCount(s, IronStructure.Academy) == 0 && Population(s.Id) >= 50 &&
                CivWorksCount(s.Civilization, IronStructure.Academy) < MaxAcademies,
            // A press stands where grafted orchards give grapes or olives to press.
            IronStructure.Press => s.Civic is { Tier: >= 1 } && IronWorksCount(s, IronStructure.Press) == 0 && Done(s.Civilization, "FOR-08") && s.OrchardPlots.Count > 0,
            IronStructure.GreatLibrary or IronStructure.GreatDome => WonderWanted(s, kind),
            IronStructure.GreatLighthouse => IronWorksCount(s, IronStructure.Harbor) > 0 && WonderWanted(s, kind),
            IronStructure.Castle => s.Civic is { Tier: >= 1 } && IronWorksCount(s, IronStructure.Castle) == 0 && Population(s.Id) >= 60 && HasStoneWalls(s) &&
                NearestForeignDistance(s) <= CastleReach,
            _ => false,
        };

    /// <summary>Whether a started structure can still be finished: a transient change in why it was wanted (a town slipping a tier for a season) does not throw away labor already spent.</summary>
    private bool IronBuildFeasible(SettlementState s, IronStructure kind)
        => kind switch
        {
            IronStructure.Terrace => NextTerraceTile(s) >= 0,
            IronStructure.WindlassWell => IronWorksCount(s, kind) < 2,
            IronStructure.Gatehouse => IronWorksCount(s, kind) == 0 && HasStoneWalls(s),
            IronStructure.FrontierWall => IronWorksCount(s, kind) < 2 && NextFrontierFacing(s) >= 0,
            IronStructure.GreatLibrary or IronStructure.GreatLighthouse or IronStructure.GreatDome => IronWorksCount(s, kind) == 0 && WonderOwnerCiv(kind) < 0,
            _ => IronWorksCount(s, kind) == 0,
        };

    /// <summary>
    /// Chooses the Iron structure to build (first the settlement wants, in enum order) and asks the chain for the materials it lacks. Called from
    /// <c>PrimaryWants</c>, before the wants are expanded, so the stone and bloom it needs are planned like any other want.
    /// </summary>
    private void IronBuildWants(SettlementState s, MetalState m, MetalPlan plan)
    {
        if (!IronChain) return;
        int civ = s.Civilization;
        if (m.IronBuildKind >= 0 && (!Done(civ, IronSpecs[m.IronBuildKind].Unlock) || !IronBuildFeasible(s, (IronStructure)m.IronBuildKind)))
        {
            m.IronBuildKind = -1; // nothing is paid before completion, so there is nothing to refund (labor already spent is lost)
            m.IronBuildProgress = Fix64.Zero;
        }
        if (m.IronBuildKind < 0)
            for (int k = 0; k < (int)IronStructure.Count; k++)
                if (Done(civ, IronSpecs[k].Unlock) && IronBuildWanted(s, (IronStructure)k)) { m.IronBuildKind = k; m.IronBuildProgress = Fix64.Zero; break; }
        if (m.IronBuildKind < 0) return;
        IronSpec spec = IronSpecs[m.IronBuildKind];
        if (spec.Block > 0) plan.Terminal[MetalSlot(MetalGood.Block)] += Fix64.Max(Fix64.Zero, Fix64.FromInt(spec.Block) - m.Goods[(int)MetalGood.Block]);
        if (spec.Ashlar > 0) plan.Terminal[MetalSlot(MetalGood.Ashlar)] += Fix64.Max(Fix64.Zero, Fix64.FromInt(spec.Ashlar) - m.Goods[(int)MetalGood.Ashlar]);
        if (spec.Bloom > 0) plan.Terminal[MetalSlot(MetalGood.Iron)] += Fix64.Max(Fix64.Zero, Fix64.FromInt(spec.Bloom) - m.Goods[(int)MetalGood.Iron]);
    }

    /// <summary>The labor the Iron structure under construction can take this season: what it still needs, within its share of the settlement's work.</summary>
    private Fix64 IronBuildLabor(SettlementState s, MetalState m, Fix64 totalWork)
    {
        if (!IronChain) return Fix64.Zero;
        if (PledgedWonder(m) is { } wonder)
            return Fix64.Min(Fix64.Max(Fix64.Zero, IronLaborOf(wonder, IronSpecs[wonder.Metal!.IronBuildKind]) - wonder.Metal.IronBuildProgress), totalWork * IronBuildShare);
        if (m.IronBuildKind < 0) return Fix64.Zero;
        Fix64 remaining = Fix64.Max(Fix64.Zero, IronLaborOf(s, IronSpecs[m.IronBuildKind]) - m.IronBuildProgress);
        return Fix64.Min(remaining, totalWork * IronBuildShare);
    }

    /// <summary>
    /// Adds the season's assigned labor to the Iron structure under construction and completes it once its labor is in and its materials are in
    /// store. Returns labor beyond what the structure needed, which goes back to the metal pool.
    /// </summary>
    private Fix64 RunIronBuild(SettlementState s, MetalState m, MetalPlan plan)
    {
        if (!IronChain) return Fix64.Zero;
        if (PledgedWonder(m) is { } pledgedTo)
        {
            // A league member's labor goes into the builder's Wonder instead of its own works: delivered work, never multiplied (DIP-07).
            Fix64 wonderLabor = IronLaborOf(pledgedTo, IronSpecs[pledgedTo.Metal!.IronBuildKind]);
            Fix64 room = Fix64.Max(Fix64.Zero, wonderLabor - pledgedTo.Metal.IronBuildProgress);
            Fix64 given = Fix64.Min(room, plan.IronBuildAssigned);
            pledgedTo.Metal.IronBuildProgress += given;
            return plan.IronBuildAssigned - given;
        }
        if (m.IronBuildKind < 0) return Fix64.Zero;
        IronSpec spec = IronSpecs[m.IronBuildKind];
        Fix64 labor = IronLaborOf(s, spec), over = Fix64.Zero;
        m.IronBuildProgress += plan.IronBuildAssigned;
        if (m.IronBuildProgress > labor) { over = m.IronBuildProgress - labor; m.IronBuildProgress = labor; }
        if (m.IronBuildProgress < labor || !TryPayIronBuild(s, m, spec)) return over;
        CompleteIronBuild(s, m, (IronStructure)m.IronBuildKind);
        m.IronBuildKind = -1;
        m.IronBuildProgress = Fix64.Zero;
        return over;
    }

    private bool TryPayIronBuild(SettlementState s, MetalState m, IronSpec spec)
    {
        if (m.Goods[(int)MetalGood.Block] < Fix64.FromInt(spec.Block) || m.Goods[(int)MetalGood.Ashlar] < Fix64.FromInt(spec.Ashlar) ||
            m.Goods[(int)MetalGood.Iron] < Fix64.FromInt(spec.Bloom) || s.Stores[(int)Good.Wood] < Fix64.FromInt(spec.Wood)) return false;
        if (m.IronBuildKind == (int)IronStructure.Terrace && NextTerraceTile(s) < 0) return false;
        if (m.IronBuildKind == (int)IronStructure.FrontierWall && NextFrontierFacing(s) < 0) return false;
        if (IsWonder((IronStructure)m.IronBuildKind) && WonderOwnerCiv((IronStructure)m.IronBuildKind) >= 0) return false; // someone else finished it first: there is one of each
        AddStock(s, MetalSlot(MetalGood.Block), -Fix64.FromInt(spec.Block));
        AddStock(s, MetalSlot(MetalGood.Ashlar), -Fix64.FromInt(spec.Ashlar));
        AddStock(s, MetalSlot(MetalGood.Iron), -Fix64.FromInt(spec.Bloom));
        AddStock(s, (int)Good.Wood, -Fix64.FromInt(spec.Wood));
        return true;
    }

    private void CompleteIronBuild(SettlementState s, MetalState m, IronStructure kind)
    {
        if (kind == IronStructure.FrontierWall) (m.IronFacing ??= new List<int>()).Add(NextFrontierFacing(s)); // before the count rises: the facing is the civ this segment answers
        (m.IronWorks ??= new int[(int)IronStructure.Count])[(int)kind]++;
        if (IsWonder(kind)) _wondersBuilt.Add((s.Civilization, WonderProject(kind))); // the Wonder's Legacy (docs/MVP/22 §3.3)
        if (kind == IronStructure.Terrace)
        {
            int tile = NextTerraceTile(s);
            _fieldOwner[tile] = s.Id;
            s.FieldTiles.Add(tile);
            (m.IronTerraces ??= new List<int>()).Add(tile);
        }
    }
}
