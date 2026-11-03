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
        Wrought, Steel, Kit11, Kit12, Arms3, Arms4, Plank,
        Count
    }

    private static string IronGoodId(IronGood good) => good switch
    {
        IronGood.Wrought => "G-WROUGHT", IronGood.Steel => "G-STEEL", IronGood.Kit11 => "G-TOOL11", IronGood.Kit12 => "G-TOOL12",
        IronGood.Arms3 => "G-ARMS3", IronGood.Arms4 => "G-ARMS4", IronGood.Plank => "G-PLANK",
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
        => m.IronGoods is not null || m.IronWorks is not null || m.IronBuildKind >= 0 || m.IronTerraces is { Count: > 0 };

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
            for (int k = 0; k < (int)IronStructure.Count; k++) hash.Add(m.IronWorks is { } w ? w[k] : 0);
            hash.Add(m.IronTerraces?.Count ?? 0);
            if (m.IronTerraces is not null) foreach (int tile in m.IronTerraces) hash.Add(tile);
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
            for (int k = 0; k < (int)IronStructure.Count; k++) writer.Write(m.IronWorks is { } w ? w[k] : 0);
            writer.Write(m.IronTerraces?.Count ?? 0);
            if (m.IronTerraces is not null) foreach (int tile in m.IronTerraces) writer.Write(tile);
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

    // ---- construction ---------------------------------------------------------------------------------

    /// <summary>
    /// Structures only the Iron chapter builds (docs/MVP/26 §3.2). They are kept apart from the E2/E3 <c>Structure</c> array, which every Bronze hash
    /// covers. Append only; the order is the order a settlement considers them.
    /// </summary>
    private enum IronStructure : byte
    {
        Terrace,
        Count
    }

    /// <summary>What a structure needs: its unlocking project, labor in work-seasons, and materials paid at completion (docs/MVP/26 §3.2; all provisional).</summary>
    private readonly record struct IronSpec(string Name, string Unlock, int Labor, int Block, int Ashlar, int Wood, int Bloom);

    private static readonly IronSpec[] IronSpecs =
    {
        new("terrace", "AGR-08", 8, 4, 0, 2, 0),
    };

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
            IronStructure.Terrace => Population(s.Id) >= 12 && NextTerraceTile(s) >= 0 && NaturalFieldsFull(s),
            _ => false,
        };

    /// <summary>
    /// Chooses the Iron structure to build (first the settlement wants, in enum order) and asks the chain for the materials it lacks. Called from
    /// <c>PrimaryWants</c>, before the wants are expanded, so the stone and bloom it needs are planned like any other want.
    /// </summary>
    private void IronBuildWants(SettlementState s, MetalState m, MetalPlan plan)
    {
        if (!IronChain) return;
        int civ = s.Civilization;
        if (m.IronBuildKind >= 0 && (!Done(civ, IronSpecs[m.IronBuildKind].Unlock) || !IronBuildWanted(s, (IronStructure)m.IronBuildKind)))
        {
            m.IronBuildKind = -1; // nothing is paid before completion, so there is nothing to refund
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
    private Fix64 IronBuildLabor(MetalState m, Fix64 totalWork)
    {
        if (!IronChain || m.IronBuildKind < 0) return Fix64.Zero;
        Fix64 remaining = Fix64.Max(Fix64.Zero, Fix64.FromInt(IronSpecs[m.IronBuildKind].Labor) - m.IronBuildProgress);
        return Fix64.Min(remaining, totalWork * IronBuildShare);
    }

    /// <summary>
    /// Adds the season's assigned labor to the Iron structure under construction and completes it once its labor is in and its materials are in
    /// store. Returns labor beyond what the structure needed, which goes back to the metal pool.
    /// </summary>
    private Fix64 RunIronBuild(SettlementState s, MetalState m, MetalPlan plan)
    {
        if (!IronChain || m.IronBuildKind < 0) return Fix64.Zero;
        IronSpec spec = IronSpecs[m.IronBuildKind];
        Fix64 labor = Fix64.FromInt(spec.Labor), over = Fix64.Zero;
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
        AddStock(s, MetalSlot(MetalGood.Block), -Fix64.FromInt(spec.Block));
        AddStock(s, MetalSlot(MetalGood.Ashlar), -Fix64.FromInt(spec.Ashlar));
        AddStock(s, MetalSlot(MetalGood.Iron), -Fix64.FromInt(spec.Bloom));
        AddStock(s, (int)Good.Wood, -Fix64.FromInt(spec.Wood));
        return true;
    }

    private void CompleteIronBuild(SettlementState s, MetalState m, IronStructure kind)
    {
        (m.IronWorks ??= new int[(int)IronStructure.Count])[(int)kind]++;
        if (kind == IronStructure.Terrace)
        {
            int tile = NextTerraceTile(s);
            _fieldOwner[tile] = s.Id;
            s.FieldTiles.Add(tile);
            (m.IronTerraces ??= new List<int>()).Add(tile);
        }
    }
}
