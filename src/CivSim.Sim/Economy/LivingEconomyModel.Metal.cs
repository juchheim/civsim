using System;
using System.Collections.Generic;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// The Bronze Age economy's season (docs/MVP/22 §5, 23 §4–§7): the planner decides how many kits, arms and crafted goods
/// it wants, expands that backwards through the recipes into ore, charcoal and labor, reserves at most
/// <c>MetalPushLaborCap</c> of the settlement's work for it, and the craft step runs the recipes in ascending order, each
/// limited by its scarcest input. Kits are per figure; their tier's typed <c>mods</c> apply to the figure that carries one.
/// </summary>
public sealed partial class LivingEconomyModel
{
    private const int NoSlot = int.MinValue;
    /// <summary>Recipes from this order on are crafts: they run on craft labor, not on the metal chain's reserved labor.</summary>
    private const int CraftOrder = 45;
    private static int MetalSlot(MetalGood good) => (int)Good.Count + (int)good;
    private static int SlotCount => (int)Good.Count + (int)MetalGood.Count;

    private sealed class CompiledRecipe
    {
        public required EconomyRecipe Def;
        public required int[][] InputSlots;
        public required Fix64[] InputQuantity;
        public int OutputSlot;
        public Fix64 OutputQuantity;
        public byte Task;
        public bool IsCharcoal;
        public bool Enabled;
    }

    private sealed class MetalPlan
    {
        public MetalPlan(int recipes)
        {
            Batches = new Fix64[recipes];
            Want = new Fix64[SlotCount];
            Terminal = new Fix64[SlotCount];
            SisterTerminal = new Fix64[SlotCount];
        }

        /// <summary>The part of <see cref="Terminal"/> made or dug for sister settlements.</summary>
        public readonly Fix64[] SisterTerminal;

        public int Tier;
        public readonly Fix64[] Batches;
        public readonly Fix64[] Want;
        public readonly Fix64[] Terminal;
        public Fix64 Budget;
        // Labor buckets, in the order figures are assigned to them.
        public Fix64 BuildNeed, MineNeed, CharcoalNeed, SmeltNeed;
        public Fix64 BuildAssigned, MineAssigned, CharcoalAssigned, SmeltAssigned;
        public Fix64 MineProductive;
        public Fix64 CampWork;
        public Fix64 MeanChopRate = Fix64.FromInt(8);
        public bool AnyMetalKit;
        public int Adults;
    }

    private Fix64[] _metalBarter = Array.Empty<Fix64>();
    /// <summary>Harness tracing of the craft step; null (and free) in matches.</summary>
    internal System.Text.StringBuilder? MetalDebug;
    private readonly EconomyToolTier?[] _tierDefs = new EconomyToolTier?[11];

    private void CompileRecipes()
    {
        _metalBarter = new Fix64[(int)MetalGood.Count];
        for (int g = 0; g < (int)MetalGood.Count; g++) _metalBarter[g] = _content.BarterValue(MetalGoodId((MetalGood)g));
        foreach (EconomyToolTier tier in _content.ToolTiers)
            if (tier.Tier is >= 8 and <= 10) _tierDefs[tier.Tier] = tier;

        var compiled = new List<CompiledRecipe>();
        foreach (EconomyRecipe r in _content.Recipes)
        {
            bool enabled = r.Outputs.Length > 0;
            var slots = new int[r.Inputs.Length][];
            var quantities = new Fix64[r.Inputs.Length];
            for (int i = 0; i < r.Inputs.Length; i++)
            {
                var alternatives = new List<int>();
                foreach (string id in r.Inputs[i].Goods)
                {
                    int slot = SlotOf(id);
                    if (slot != NoSlot) alternatives.Add(slot);
                }
                if (alternatives.Count == 0) enabled = false;
                slots[i] = alternatives.ToArray();
                quantities[i] = r.Inputs[i].Quantity;
            }
            int output = r.Outputs.Length > 0 ? SlotOf(r.Outputs[0].Good) : NoSlot;
            if (output == NoSlot || output < (int)Good.Count) enabled = false; // the craft step stocks E3 goods only
            compiled.Add(new CompiledRecipe
            {
                Def = r, InputSlots = slots, InputQuantity = quantities, OutputSlot = output,
                OutputQuantity = r.Outputs.Length > 0 ? r.Outputs[0].Quantity : Fix64.Zero,
                Task = TaskOfPractice(r.Practice), IsCharcoal = output == MetalSlot(MetalGood.Charcoal),
                Enabled = enabled,
            });
        }
        _compiledRecipes = compiled.ToArray();
    }

    private int SlotOf(string goodId)
    {
        int e2 = GoodIndex(goodId);
        if (e2 >= 0) return e2;
        for (int g = 0; g < (int)MetalGood.Count; g++)
            if (MetalGoodId((MetalGood)g) == goodId) return MetalSlot((MetalGood)g);
        return NoSlot;
    }

    private static Fix64 Stock(SettlementState s, int slot)
        => slot < (int)Good.Count ? s.Stores[slot] : s.Metal is { } m ? m.Goods[slot - (int)Good.Count] : Fix64.Zero;

    private void AddStock(SettlementState s, int slot, Fix64 amount)
    {
        if (slot < (int)Good.Count) s.Stores[slot] = Fix64.Max(Fix64.Zero, s.Stores[slot] + amount);
        else
        {
            MetalState m = EnsureMetal(s);
            m.Goods[slot - (int)Good.Count] = Fix64.Max(Fix64.Zero, m.Goods[slot - (int)Good.Count] + amount);
        }
    }

    private MetalState EnsureMetal(SettlementState s)
    {
        if (s.Metal is { } existing) return existing;
        var m = new MetalState { ForestAtStart = StandingWoodInRange(s.SiteTile) };
        s.Metal = m;
        return m;
    }

    private bool Done(int civ, string projectId) => HasProject(civ, projectId);

    private bool AnyDone(int civ, int[] projects)
    {
        foreach (int p in projects) if ((uint)p < (uint)_completedProjects[civ].Length && _completedProjects[civ][p]) return true;
        return false;
    }

    private bool TierKnown(int civ, int tier) => _tierDefs[tier] is { } def && AnyDone(civ, def.UnlockedBy);

    private static int Rank(int tier) => tier switch { 8 => 1, 9 => 2, 10 => 3, _ => 0 };

    private static MetalGood KitGood(int tier) => tier switch { 8 => MetalGood.Kit8, 9 => MetalGood.Kit9, _ => MetalGood.Kit10 };

    // ---- kits -----------------------------------------------------------------------------------

    private Fix64 ServiceOf(int tier) => _tierDefs[tier]?.ServiceWs ?? Fix64.FromInt(12);

    /// <summary>Kit quality falls linearly from 1.2 new to 0.7 at replacement and on to 0.5, where it breaks (docs/MVP/23 §4.1).</summary>
    private Fix64 KitQuality(FigureState f)
    {
        if (f.Kit == 0) return Fix64.One;
        Fix64 service = ServiceOf(f.Kit);
        Fix64 slope = (KitNewQuality - _bronzeConstants.ReplaceQuality) / service;
        return Fix64.Clamp(_bronzeConstants.ReplaceQuality + slope * f.KitLeft, _bronzeConstants.BreakQuality, KitNewQuality);
    }

    /// <summary>The work left on a kit when it breaks: below replacement by the dormancy tail (×1.4 the service life).</summary>
    private Fix64 KitBreakLeft(int tier)
    {
        Fix64 service = ServiceOf(tier);
        Fix64 slope = (KitNewQuality - _bronzeConstants.ReplaceQuality) / service;
        return (_bronzeConstants.BreakQuality - _bronzeConstants.ReplaceQuality) / slope;
    }

    /// <summary>The figure's kit multiplier for a stat, applied once whatever the civilization knows (docs/MVP/23 §1.3).</summary>
    private Fix64 KitMod(FigureState f, string stat)
        => f.Kit != 0 && _tierDefs[f.Kit] is { } def ? def.Mod(stat) : Fix64.One;

    /// <summary>The Chop rung a figure's kit gives it (5 copper, 6 bronze of either alloy), or -1 when it has no usable metal kit.</summary>
    private int KitChopRung(FigureState f)
    {
        if (f.Kit == 0) return -1;
        int rung = f.Kit == 8 ? 5 : 6;
        return ChopRungMult(f.Civilization, rung) > Fix64.Zero ? rung : -1;
    }

    private Fix64 ChopRungMult(int civ, int rung) => MethodMult(civ, "chop", rung);

    // Method multipliers by (action, rung) per civilization: asked for every figure every season, changed only by completions.
    private Fix64[]?[]? _methodMultCache;
    private static readonly Fix64 Uncomputed = Fix64.FromInt(-1);
    /// <summary>A new miner's skill multiplier (0.8, docs/MVP/05 skills): the plan budgets mining for the unskilled.</summary>
    private static readonly Fix64 UnskilledMiner = Fix64.FromRatio(4, 5);
    /// <summary>Quantities below this (a millionth) are rounding dust and end a loop.</summary>
    private static readonly Fix64 Epsilon = Fix64.FromRaw(4295);
    private static readonly Fix64 ArsenicInjury = Fix64.FromRatio(13, 10);
    private static readonly Fix64 CattleMilk = Fix64.FromRatio(5, 2);
    private static readonly Fix64 SmallStockMilk = Fix64.FromRatio(4, 5);

    /// <summary>The researched method multiplier of a rung of chop or mine, 0 when the civilization lacks it.</summary>
    private Fix64 MethodMult(int civ, string action, int rung)
    {
        int slot = (action == "chop" ? 0 : 16) + rung;
        if (rung is < 0 or >= 16 || action is not ("chop" or "mine")) return ComputeMethodMult(civ, action, rung);
        _methodMultCache ??= new Fix64[]?[_completedProjects.Length];
        Fix64[] cache = _methodMultCache[civ] ??= NewMethodCache();
        if (cache[slot] == Uncomputed) cache[slot] = ComputeMethodMult(civ, action, rung);
        return cache[slot];
    }

    private static Fix64[] NewMethodCache()
    {
        var cache = new Fix64[32];
        Array.Fill(cache, Uncomputed);
        return cache;
    }

    private Fix64 ComputeMethodMult(int civ, string action, int rung)
    {
        ReadOnlySpan<EconomyEffect> effects = _content.Effects;
        for (int i = 0; i < effects.Length; i++)
        {
            EconomyEffect e = effects[i];
            if (e.Kind == "method" && e.Action == action && e.Rung == rung && _completedProjects[civ][e.ProjectIndex])
                return e.Mult ?? Fix64.One;
        }
        return Fix64.Zero;
    }

    /// <summary>A figure's chop output per work-season with its metal kit (base 2 L × the rung × kit quality), before skill and wood modifiers.</summary>
    private Fix64 KitChopRate(FigureState f)
    {
        int rung = KitChopRung(f);
        if (rung < 0) return Fix64.Zero;
        return BaseRate("chop", Fix64.FromInt(2)) * ChopRungMult(f.Civilization, rung) * KitQuality(f);
    }

    private Fix64 BaseRate(string action, Fix64 fallback)
    {
        foreach (EconomyAction a in _content.Actions)
            if (a.Id == action && a.BaseRate is { } rate) return rate;
        return fallback;
    }

    private void WearAndIssueKits(SettlementState s, MetalState m, int tier, Fix64[] toolWork)
    {
        int civ = s.Civilization;
        // Wear by the season's tool work; a kit that cannot be replaced is worked on to its break point (the dormancy tail).
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState f = _figures[i];
            if (f.SettlementId != s.Id || f.Kit == 0) continue;
            f.KitLeft -= toolWork[i];
            if (f.KitLeft <= KitBreakLeft(f.Kit)) { f.Kit = 0; f.KitLeft = Fix64.Zero; }
        }
        // Hand out the kits in stock: those without a kit first, then worn kits, then upgrades, oldest kits first.
        var order = new List<int>();
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState f = _figures[i];
            if (f.SettlementId != s.Id || f.Age < 14) continue;
            if (f.Kit == 0 || f.KitLeft <= Fix64.Zero || (tier != 0 && Rank(f.Kit) < Rank(tier))) order.Add(i);
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
            if (tier != 0 && m.Goods[(int)KitGood(tier)] >= Fix64.One) give = tier;
            else
                for (int t = 10; t >= 8; t--)
                {
                    if (m.Goods[(int)KitGood(t)] < Fix64.One) continue;
                    if (f.Kit != 0 && f.KitLeft > Fix64.Zero && Rank(t) <= Rank(f.Kit)) continue;
                    give = t;
                    break;
                }
            if (give == 0) continue;
            if (f.Kit != 0 && f.KitLeft > Fix64.Zero && _tierDefs[f.Kit] is { } old)
            {
                // An early upgrade remelts the old kit: half its metal comes back as copper (arsenic burns off, 23 §4.1).
                Fix64 copper = old.MetalPerKit * _bronzeConstants.RemeltShare;
                m.Goods[(int)MetalGood.Copper] += copper;
                AddMetalStat(civ, StatRemeltCopper, copper);
            }
            m.Goods[(int)KitGood(give)] -= Fix64.One;
            f.Kit = (byte)give;
            f.KitLeft = ServiceOf(give);
        }
    }

    // ---- access, sharing and the best tier ---------------------------------------------------------

    private bool OreUnlocked(int civ, DepositKind kind) => kind switch
    {
        DepositKind.CopperOre => CivInBronze(civ),
        DepositKind.ArsenicalCopper => Done(civ, "MET-04"),
        DepositKind.Tin => Done(civ, "MET-05"),
        DepositKind.Iron => Done(civ, "MET-08"),
        DepositKind.Gold or DepositKind.Silver => Done(civ, "MET-07"),
        DepositKind.Salt => Done(civ, "MIN-06"),
        _ => false,
    };

    private static MetalGood? OreGood(DepositKind kind) => kind switch
    {
        DepositKind.CopperOre => MetalGood.CopperOre,
        DepositKind.ArsenicalCopper => MetalGood.ArsenicalOre,
        DepositKind.Tin => MetalGood.TinOre,
        DepositKind.Iron => MetalGood.IronOre,
        DepositKind.Gold or DepositKind.Silver => MetalGood.Gold,
        DepositKind.Salt => MetalGood.Salt,
        _ => null,
    };

    /// <summary>Whether a civilization can dig a deposit at all: the ore is unlocked and depth 2 needs Shaft Mining (docs/MVP/23 §7.1).</summary>
    private bool DepositWorkable(int civ, int tile)
    {
        WorldDeposit d = _world.Deposit(tile);
        if (_ore[tile] <= Fix64.Zero || OreGood(d.Kind) is null || !OreUnlocked(civ, d.Kind)) return false;
        return d.Depth < 2 || Done(civ, "MIN-04");
    }

    private bool HasMineHead(SettlementState s, int tile) => s.Metal is { } m && m.MineHeads.Contains(tile);

    /// <summary>Litres of ore per productive work-season at a deposit: base × deposit factor × the mine rung (docs/MVP/23 §6.2, §7.1).</summary>
    private Fix64 MineRate(int civ, int tile, bool mineHead, out bool fireSetting)
    {
        fireSetting = false;
        WorldDeposit d = _world.Deposit(tile);
        if (d.Kind is DepositKind.Gold or DepositKind.Silver) return BaseRate("pan_gold", Fix64.FromRatio(1, 10));
        if (d.Kind == DepositKind.Salt) return BaseRate("salt", Fix64.One);
        Fix64 factor = d.Kind == DepositKind.Tin ? Fix64.Half : Fix64.One;
        Fix64 rate = BaseRate("mine", Fix64.One) * factor;
        if (mineHead && Done(civ, "MIN-04"))
        {
            Fix64 fire = MethodMult(civ, "mine", 2);
            Fix64 shaft = MethodMult(civ, "mine", 1);
            if (fire > Fix64.Zero) { fireSetting = true; return rate * fire; }
            if (shaft > Fix64.Zero) return rate * shaft;
        }
        return d.Depth >= 2 ? Fix64.Zero : rate;
    }

    private bool IsOwnTile(int civ, int tile)
    {
        if (TileOwnership is { } ownership) return ownership.OwnerOf(tile) == civ;
        foreach (SettlementState s in _settlements)
            if (s.Civilization == civ && HexGrid.Distance(s.SiteTile, tile, _world.Width) <= 4 && Population(s.Id) > 0) return true;
        return false;
    }

    private bool IsRivalTile(int civ, int tile)
        => TileOwnership is { } ownership && ownership.OwnerOf(tile) is int owner && owner >= 0 && owner != civ;

    private int ShareReach(int civ)
        => Done(civ, "GOV-04") ? _bronzeConstants.ShareReachGov04 : Done(civ, "SOC-07") ? _bronzeConstants.ShareReach : -1;

    /// <summary>
    /// Season start (before any settlement works): Bronze state for every settlement of a civilization past KEY-3, mining
    /// camp crews and closures, strategic goods shared between sister settlements, and each settlement's access.
    /// </summary>
    private void BronzeSeasonStart(int season)
    {
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState s = _settlements[i];
            if (s.Metal is null && CivInBronze(s.Civilization) && Population(s.Id) > 0) EnsureMetal(s);
        }
        MaintainCamps(season);
        ShareMetals();
        for (int i = 0; i < _settlements.Count; i++)
            if (_settlements[i].Metal is { } m) m.Access = ComputeAccess(_settlements[i]);
    }

    private void ShareMetals()
    {
        for (int civ = 0; civ < _completedProjects.Length; civ++)
        {
            int reach = ShareReach(civ);
            if (reach < 0) continue;
            for (int k = 0; k < SharedGoods.Length; k++)
            {
                if (SharedGoods[k] is MetalGood.Kit8 or MetalGood.Kit9 or MetalGood.Kit10) continue;
                int g = (int)SharedGoods[k];
                for (int r = 0; r < _settlements.Count; r++)
                {
                    SettlementState receiver = _settlements[r];
                    if (receiver.Civilization != civ || receiver.Metal is not { } rm || IsFreeFolk(receiver)) continue;
                    Fix64 need = rm.ShareWant[k] - rm.Goods[g];
                    for (int d = 0; d < _settlements.Count && need > Fix64.Zero; d++)
                    {
                        if (d == r) continue;
                        SettlementState donor = _settlements[d];
                        if (donor.Civilization != civ || donor.Metal is not { } dm || IsFreeFolk(donor) ||
                            HexGrid.Distance(donor.SiteTile, receiver.SiteTile, _world.Width) > reach) continue;
                        Fix64 surplus = dm.Goods[g] - dm.ShareWant[k];
                        if (surplus <= Fix64.Zero) continue;
                        Fix64 move = Fix64.Min(need, surplus);
                        dm.Goods[g] -= move;
                        rm.Goods[g] += move;
                        need -= move;
                    }
                }
            }
            // Cast kits travel as whole kits, any tier, best first: a settlement without a forge takes what its smiths send.
            for (int r = 0; r < _settlements.Count; r++)
            {
                SettlementState receiver = _settlements[r];
                if (receiver.Civilization != civ || receiver.Metal is not { } rm || IsFreeFolk(receiver)) continue;
                Fix64 need = KitWant(rm) - KitStock(rm);
                for (int d = 0; d < _settlements.Count && need >= Fix64.One; d++)
                {
                    if (d == r) continue;
                    SettlementState donor = _settlements[d];
                    if (donor.Civilization != civ || donor.Metal is not { } dm || IsFreeFolk(donor) ||
                        HexGrid.Distance(donor.SiteTile, receiver.SiteTile, _world.Width) > reach) continue;
                    Fix64 surplus = KitStock(dm) - KitWant(dm);
                    for (int t = 10; t >= 8 && surplus >= Fix64.One && need >= Fix64.One; t--)
                    {
                        int g = (int)KitGood(t);
                        Fix64 move = Fix64.FromInt(Fix64.Min(Fix64.Min(need, surplus), dm.Goods[g]).FloorToInt());
                        if (move <= Fix64.Zero) continue;
                        dm.Goods[g] -= move;
                        rm.Goods[g] += move;
                        need -= move;
                        surplus -= move;
                    }
                }
            }
        }
    }

    private static Fix64 KitStock(MetalState m)
        => m.Goods[(int)MetalGood.Kit8] + m.Goods[(int)MetalGood.Kit9] + m.Goods[(int)MetalGood.Kit10];

    private static Fix64 KitWant(MetalState m)
    {
        Fix64 want = Fix64.Zero;
        for (int k = 0; k < SharedGoods.Length; k++)
            if (SharedGoods[k] is MetalGood.Kit8 or MetalGood.Kit9 or MetalGood.Kit10) want += m.ShareWant[k];
        return want;
    }

    private byte ComputeAccess(SettlementState s)
    {
        int civ = s.Civilization;
        Fix64 min = _bronzeConstants.AccessMin;
        int reach = ShareReach(civ);
        byte access = 0;
        bool Holds(SettlementState x, MetalGood a, MetalGood b)
            => x.Metal is { } xm && xm.Goods[(int)a] + xm.Goods[(int)b] >= min;
        bool Has(MetalGood a, MetalGood b)
        {
            if (Holds(s, a, b)) return true;
            if (reach < 0) return false;
            foreach (SettlementState other in _settlements)
                if (other != s && other.Civilization == civ && !IsFreeFolk(other) && !IsFreeFolk(s) && HexGrid.Distance(other.SiteTile, s.SiteTile, _world.Width) <= reach &&
                    Holds(other, a, b)) return true;
            return false;
        }
        if (Has(MetalGood.CopperOre, MetalGood.Copper)) access |= AccessCopper;
        if (Has(MetalGood.ArsenicalOre, MetalGood.ArsenicalBronze)) access |= AccessArsenic;
        if (Has(MetalGood.TinOre, MetalGood.Tin)) access |= AccessTin;
        foreach (int tile in RangeTiles(s.SiteTile))
            access |= SourceBit(civ, tile);
        foreach (MiningCampState camp in _camps)
            if (camp.Parent == s.Id) access |= SourceBit(civ, camp.Tile);
        // A workable deposit on own land within mining reach is a source too: the planner opens a camp there when it wants the ore.
        if ((access & (SourceCopper | SourceArsenic | SourceTin)) != (SourceCopper | SourceArsenic | SourceTin))
            foreach (int tile in ReachTiles(s.SiteTile, MiningReach(civ, s.SiteTile)))
            {
                byte bit = SourceBit(civ, tile);
                if (bit != 0 && (access & bit) == 0 && IsOwnTile(civ, tile)) access |= bit;
            }
        return access;
    }

    private byte SourceBit(int civ, int tile)
    {
        if (!DepositWorkable(civ, tile)) return 0;
        return _world.Deposit(tile).Kind switch
        {
            DepositKind.CopperOre => SourceCopper,
            DepositKind.ArsenicalCopper => SourceArsenic,
            DepositKind.Tin => SourceTin,
            _ => 0,
        };
    }

    /// <summary>
    /// The best kit tier the settlement can make now (docs/MVP/22 §5.4, 23 §7.3): tin bronze needs tin and copper,
    /// arsenical bronze needs arsenical ore, copper needs copper; each from stores, a sister settlement or a workable
    /// deposit. 0 when none (stone tools only).
    /// </summary>
    private int BestCastTier(SettlementState s)
    {
        if (s.Metal is not { } m) return 0;
        int civ = s.Civilization;
        bool copper = (m.Access & (AccessCopper | SourceCopper)) != 0 || m.Goods[(int)MetalGood.Copper] >= Fix64.FromRatio(12, 100);
        bool tin = (m.Access & (AccessTin | SourceTin)) != 0;
        bool arsenic = (m.Access & (AccessArsenic | SourceArsenic)) != 0;
        Fix64 kitMetal = Fix64.FromRatio(15, 100);
        if (TierKnown(civ, 10) && (m.Goods[(int)MetalGood.Bronze] >= kitMetal || (tin && copper))) return 10;
        if (TierKnown(civ, 9) && (m.Goods[(int)MetalGood.ArsenicalBronze] >= kitMetal || arsenic)) return 9;
        if (TierKnown(civ, 8) && copper) return 8;
        return 0;
    }

    private MethodFallbackReason DormancyReason(SettlementState s, int tier)
    {
        if (s.Metal is not { } m) return MethodFallbackReason.None;
        int civ = s.Civilization;
        if (TierKnown(civ, 10) && tier < 10 && (m.Access & (AccessTin | SourceTin)) == 0) return MethodFallbackReason.NoTin;
        if (TierKnown(civ, 9) && tier < 9 && (m.Access & (AccessArsenic | SourceArsenic)) == 0) return MethodFallbackReason.NoArsenic;
        if (TierKnown(civ, 8) && tier == 0) return MethodFallbackReason.NoOre;
        if (m.FuelShortSeasons > 0) return MethodFallbackReason.NoFuel;
        return MethodFallbackReason.None;
    }

    // ---- planning --------------------------------------------------------------------------------

    private bool RecipeAvailable(SettlementState s, CompiledRecipe r)
    {
        if (!r.Enabled || !AnyDone(s.Civilization, r.Def.UnlockedBy)) return false;
        if (r.Def.Structures.Length == 0) return true;
        MetalState? m = s.Metal;
        foreach (string id in r.Def.Structures)
        {
            if (id == "kiln" && s.Structures[(int)Structure.Kiln] > 0) return true;
            if (id == "smelter" && m is not null && m.Smelters > 0) return true;
            if (id == "forge" && m is not null && m.Forges > 0) return true;
        }
        return false;
    }

    private bool BellowsActive(SettlementState s, CompiledRecipe r)
        => r.Def.Bellows && s.Metal is { Smelters: > 0 } && Done(s.Civilization, "FIR-09");

    private Fix64 OutputQuantity(SettlementState s, CompiledRecipe r)
        => BellowsActive(s, r) ? r.OutputQuantity * Modifier(s.Civilization, "smelt_yield") : r.OutputQuantity;

    private Fix64 InputQuantity(SettlementState s, CompiledRecipe r, int input)
    {
        Fix64 q = r.InputQuantity[input];
        if (BellowsActive(s, r) && Array.IndexOf(r.InputSlots[input], MetalSlot(MetalGood.Charcoal)) >= 0)
            q *= Modifier(s.Civilization, "charcoal_per_smelt");
        return q;
    }

    private Fix64 RecipeLabor(SettlementState s, CompiledRecipe r, bool anyMetalKit)
    {
        Fix64 labor = r.Def.Labor;
        string id = r.Def.Id;
        if (id is "make_wheel" or "build_cart" or "build_chariot") labor *= Modifier(s.Civilization, "wheel_and_cart_labor");
        // Dressing stone takes twice as long without a T8+ kit (docs/MVP/23 §5).
        if (id == "dress_stone" && !anyMetalKit) labor *= 2;
        return labor;
    }

    private MetalPlan PlanMetal(SettlementState s, Fix64 totalWork)
    {
        MetalState m = EnsureMetal(s);
        int civ = s.Civilization;
        var plan = new MetalPlan(_compiledRecipes.Length);
        plan.Tier = BestCastTier(s);
        m.Reason = DormancyReason(s, plan.Tier);

        // Who carries what: the kit summary feeds charcoal labor and the craft labor of dressing stone.
        int adults = 0, needKits = 0;
        Fix64 chopSum = Fix64.Zero;
        Fix64 e2Chop = ActionRate("chop", Fix64.FromInt(2), s, "chop") * Modifier(civ, "wood_yield");
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState f = _figures[i];
            if (f.SettlementId != s.Id || f.Age < 14) continue;
            adults++;
            Fix64 kitRate = KitChopRate(f);
            if (kitRate > Fix64.Zero) { chopSum = Fix64.Max(chopSum, kitRate * Modifier(civ, "wood_yield")); plan.AnyMetalKit = true; }
            else chopSum = Fix64.Max(chopSum, e2Chop);
            if (plan.Tier != 0 && (f.Kit == 0 || f.KitLeft <= ExpectedSeasonWear || Rank(f.Kit) < Rank(plan.Tier))) needKits++;
        }
        plan.Adults = adults;
        // The colliers fell their wood with the best axe in the settlement (as metal_sim.py does).
        if (adults > 0) plan.MeanChopRate = Fix64.Max(Fix64.One, chopSum);
        int pop = Population(s.Id);

        // Kits and arms come first; the other goods get what is left of the push cap (docs/MVP/23 §7.3).
        PrimaryWants(s, m, plan, pop, needKits);
        ExpandWants(s, plan);
        var primaryBatches = (Fix64[])plan.Batches.Clone();
        var primaryWant = (Fix64[])plan.Want.Clone();
        (Fix64 mineA, Fix64 charA, Fix64 smeltA) = ChainLabor(s, plan);
        SecondaryWants(s, m, plan, pop);
        ExpandWants(s, plan);
        (Fix64 mineAll, Fix64 charAll, Fix64 smeltAll) = ChainLabor(s, plan);

        plan.BuildNeed = PlanMetalBuild(s, m, plan);
        Fix64 cap = totalWork * _bronzeConstants.PushCap;
        if (s.FoodSecurity < Fix64.FromRatio(8, 10)) cap *= Fix64.Half;
        // Building the forge, furnaces and mine heads is construction, not the push: it takes up to another tenth of the
        // work (MetalPushLaborCap bounds the chain itself, docs/MVP/23 §7.3), so a slow build cannot starve the kits.
        Fix64 chainA = mineA + charA + smeltA;
        plan.BuildNeed = Fix64.Min(plan.BuildNeed, totalWork * _bronzeConstants.PushCap);
        Fix64 chainCap = cap;
        Fix64 laborB = Fix64.Max(Fix64.Zero, mineAll + charAll + smeltAll - chainA);
        Fix64 scaleA = chainA <= chainCap || chainA <= Fix64.Zero ? Fix64.One : chainCap / chainA;
        Fix64 left = Fix64.Max(Fix64.Zero, chainCap - chainA * scaleA);
        Fix64 scaleB = laborB <= Fix64.Zero ? Fix64.Zero : laborB <= left ? Fix64.One : left / laborB;
        for (int r = 0; r < plan.Batches.Length; r++)
        {
            if (_compiledRecipes[r].Def.Order >= CraftOrder) continue; // finishing crafts run on craft labor, unscaled
            Fix64 extra = Fix64.Max(Fix64.Zero, plan.Batches[r] - primaryBatches[r]);
            plan.Batches[r] = primaryBatches[r] * scaleA + extra * scaleB;
        }
        for (int k = 0; k < plan.Want.Length; k++)
        {
            Fix64 extra = Fix64.Max(Fix64.Zero, plan.Want[k] - primaryWant[k]);
            plan.Want[k] = primaryWant[k] * scaleA + extra * scaleB;
        }
        // What this settlement itself needs of each shared good (sister settlements send toward it next season).
        for (int k = 0; k < SharedGoods.Length; k++)
        {
            int slot = MetalSlot(SharedGoods[k]);
            m.ShareWant[k] = Fix64.Max(Fix64.Zero, plan.Want[slot] - plan.SisterTerminal[slot]);
        }
        (plan.MineNeed, plan.CharcoalNeed, plan.SmeltNeed) = ChainLabor(s, plan);
        plan.Budget = plan.BuildNeed + plan.MineNeed + plan.CharcoalNeed + plan.SmeltNeed;
        return plan;
    }

    /// <summary>Labor of the metal chain for the plan's batches and wants: in-range mining, charcoal (with cutting) and the recipes of orders 20–40.</summary>
    private (Fix64 Mine, Fix64 Charcoal, Fix64 Smelt) ChainLabor(SettlementState s, MetalPlan plan)
    {
        Fix64 mine = MiningLaborNeed(s, plan), charcoal = Fix64.Zero, smelt = Fix64.Zero;
        for (int r = 0; r < _compiledRecipes.Length; r++)
        {
            CompiledRecipe recipe = _compiledRecipes[r];
            if (plan.Batches[r] <= Fix64.Zero || recipe.Def.Order >= CraftOrder) continue;
            Fix64 labor = plan.Batches[r] * RecipeLabor(s, recipe, plan.AnyMetalKit);
            if (recipe.IsCharcoal)
            {
                // Cutting the wood is part of burning charcoal: 6 L of standing wood per litre at the settlement's chop rate.
                Fix64 storedWood = Fix64.Max(Fix64.Zero, s.Stores[(int)Good.Wood] - WoodReserve);
                Fix64 cut = Fix64.Max(Fix64.Zero, plan.Batches[r] * recipe.InputQuantity[0] - storedWood);
                charcoal += labor + cut / plan.MeanChopRate;
            }
            else smelt += labor;
        }
        return (mine, charcoal, smelt);
    }

    /// <summary>
    /// The first claim on metal labor (docs/MVP/23 §4): a kit of the best castable tier for every adult who needs one, the
    /// kits and ores that sister settlements without a forge or a deposit lack, and arms to four per hundred people plus
    /// the spares (23 §4.2).
    /// </summary>
    private void PrimaryWants(SettlementState s, MetalState m, MetalPlan plan, int pop, int needKits)
    {
        int civ = s.Civilization;
        if (plan.Tier != 0)
            plan.Terminal[MetalSlot(KitGood(plan.Tier))] = Fix64.Max(Fix64.Zero, Fix64.FromInt(needKits) - m.Goods[(int)KitGood(plan.Tier)]);
        int reach = ShareReach(civ);
        if (reach >= 0)
        {
            // A settlement with a forge casts for sister settlements within the sharing reach that have none (docs/MVP/22
            // §5.7's shared stores): their want of kits, of any tier, arrives here as kits of this settlement's tier.
            if (m.Forges > 0 && plan.Tier != 0)
                foreach (SettlementState sister in _settlements)
                {
                    if (sister == s || sister.Civilization != civ || sister.Metal is not { } sm || sm.Forges > 0 || IsFreeFolk(sister) ||
                        HexGrid.Distance(sister.SiteTile, s.SiteTile, _world.Width) > reach) continue;
                    Fix64 deficit = Fix64.Zero;
                    for (int t = 8; t <= 10; t++)
                    {
                        int k = Array.IndexOf(SharedGoods, KitGood(t));
                        deficit += sm.ShareWant[k] - sm.Goods[(int)KitGood(t)];
                    }
                    if (deficit <= Fix64.Zero) continue;
                    plan.Terminal[MetalSlot(KitGood(plan.Tier))] += deficit;
                    plan.SisterTerminal[MetalSlot(KitGood(plan.Tier))] += deficit;
                }
            // A settlement that can dig an ore also digs what its sister settlements lack and cannot dig.
            for (int k = 0; k < SharedGoods.Length; k++)
            {
                MetalGood ore = SharedGoods[k];
                byte bit = ore switch { MetalGood.CopperOre => SourceCopper, MetalGood.ArsenicalOre => SourceArsenic, MetalGood.TinOre => SourceTin, _ => (byte)0 };
                if (bit == 0 || (m.Access & bit) == 0) continue;
                foreach (SettlementState sister in _settlements)
                {
                    if (sister == s || sister.Civilization != civ || sister.Metal is not { } sm || (sm.Access & bit) != 0 || IsFreeFolk(sister) ||
                        HexGrid.Distance(sister.SiteTile, s.SiteTile, _world.Width) > reach) continue;
                    Fix64 deficit = sm.ShareWant[k] - sm.Goods[(int)ore];
                    if (deficit <= Fix64.Zero) continue;
                    plan.Terminal[MetalSlot(ore)] += deficit;
                    plan.SisterTerminal[MetalSlot(ore)] += deficit;
                }
            }
        }

        // Arms (docs/MVP/23 §4.2): one kit per warrior plus the spares; warriors are about four per hundred people (22 §5.6).
        if (TierKnown(civ, 8) && plan.Tier != 0)
        {
            Fix64 target = Fix64.FromInt(_bronzeConstants.SpareArms + pop * 4 / 100);
            Fix64 have = m.Goods[(int)MetalGood.Arms1] + m.Goods[(int)MetalGood.Arms2A] + m.Goods[(int)MetalGood.Arms2T];
            if (target > have)
            {
                bool war05 = Done(civ, "WAR-05");
                MetalGood arms = war05 && plan.Tier == 10 ? MetalGood.Arms2T
                    : war05 && (plan.Tier == 9 || m.Goods[(int)MetalGood.ArsenicalBronze] >= Fix64.FromRatio(3, 10)) ? MetalGood.Arms2A
                    : MetalGood.Arms1;
                plan.Terminal[MetalSlot(arms)] += target - have;
            }
        }
    }

    /// <summary>
    /// Modest stocks of the other E3 goods (docs/MVP/23 §3, §5), wanted after kits and arms. Coarse where the goods' sinks
    /// (Cohesion, sanitation, walls, caravans) belong to later work packages: one luxury of each kind per 20 people.
    /// </summary>
    private void SecondaryWants(SettlementState s, MetalState m, MetalPlan plan, int pop)
    {
        int civ = s.Civilization;
        void Want(MetalGood good, Fix64 target)
        {
            Fix64 have = m.Goods[(int)good];
            if (target > have) plan.Terminal[MetalSlot(good)] += target - have;
        }
        Fix64 perTwenty = Fix64.FromInt((pop + 19) / 20);
        if (Done(civ, "MET-06")) Want(MetalGood.FineBronze, perTwenty);
        if (Done(civ, "WOD-06")) Want(MetalGood.Turned, perTwenty);
        if (Done(civ, "CER-05")) Want(MetalGood.Faience, perTwenty);
        if (Done(civ, "CER-06")) Want(MetalGood.Glass, perTwenty);
        if (Done(civ, "FIB-08")) Want(MetalGood.DyedCloth, perTwenty);
        if (Done(civ, "MEC-03")) Want(MetalGood.Wheel, Fix64.FromInt(2));
        if (Done(civ, "TRN-05")) Want(MetalGood.Cart, Fix64.FromInt((pop + 24) / 25));
        if (Done(civ, "WAR-06")) Want(MetalGood.Chariot, Fix64.FromInt(pop / 50));
        if (Done(civ, "STN-11")) Want(MetalGood.Ashlar, Fix64.FromInt(Math.Max(10, CivicCutStoneWant(s))));
        else if (Done(civ, "MIN-03") && CivicCutStoneWant(s) > 0) Want(MetalGood.Block, Fix64.FromInt(CivicCutStoneWant(s)));
        if (Done(civ, "CHM-05")) Want(MetalGood.Soap, perTwenty);
        if (Done(civ, "CHM-04")) Want(MetalGood.Dye, Fix64.One);
        if (Done(civ, "HUS-05")) plan.Terminal[MetalSlot(MetalGood.Cheese)] += m.Goods[(int)MetalGood.Milk] * 4 / 6;
        if (Done(civ, "FOD-07")) Want(MetalGood.Salted, Fix64.FromInt(pop) / 2);
        if (Done(civ, "HUS-06")) Want(MetalGood.WoolGarment, Fix64.FromInt((pop + 9) / 10));
        if (Done(civ, "MET-07")) Want(MetalGood.Gold, perTwenty / 10);
        if (Done(civ, "MET-08")) Want(MetalGood.Iron, Fix64.One);
        if (Done(civ, "MIN-06")) Want(MetalGood.Salt, Fix64.One);
    }

    /// <summary>
    /// Expands the season's wants backwards through the recipes into gross wants of every input and the batches each
    /// recipe should run (a fixed point over the recipe graph, so same-order chains such as wheels into carts resolve).
    /// </summary>
    private void ExpandWants(SettlementState s, MetalPlan plan)
    {
        int n = _compiledRecipes.Length;
        var available = new bool[n];
        for (int r = 0; r < n; r++) available[r] = RecipeAvailable(s, _compiledRecipes[r]);
        Fix64[] maxBatches = FeasibleBatches(s, available);
        Array.Copy(plan.Terminal, plan.Want, plan.Want.Length);
        var next = new Fix64[plan.Want.Length];
        for (int pass = 0; pass < 8; pass++)
        {
            Array.Copy(plan.Terminal, next, next.Length);
            Array.Clear(plan.Batches);
            for (int r = n - 1; r >= 0; r--)
            {
                if (!available[r]) continue;
                CompiledRecipe recipe = _compiledRecipes[r];
                Fix64 net = plan.Want[recipe.OutputSlot] - Stock(s, recipe.OutputSlot);
                Fix64 output = OutputQuantity(s, recipe);
                if (net <= Fix64.Zero || output <= Fix64.Zero) continue;
                // A good with several producers is planned on the first that is available; the others top it up next season.
                if (FirstProducer(s, recipe.OutputSlot, available) != r) continue;
                Fix64 batches = Fix64.Min(net / output, maxBatches[r]);
                if (batches <= Fix64.Zero) continue;
                plan.Batches[r] = batches;
                for (int i = 0; i < recipe.InputSlots.Length; i++)
                    next[PreferredSlot(s, recipe.InputSlots[i], batches * InputQuantity(s, recipe, i))] += batches * InputQuantity(s, recipe, i);
            }
            bool same = true;
            for (int k = 0; k < next.Length; k++) if (next[k] != plan.Want[k]) { same = false; break; }
            Array.Copy(next, plan.Want, next.Length);
            if (same) break;
        }
    }

    private static readonly Fix64 Plenty = Fix64.FromInt(100_000);

    /// <summary>
    /// The most batches each recipe could run this season (a forward pass in run order): its inputs are what is in stock,
    /// what earlier recipes could make, and, without limit, ores the settlement can dig, standing wood for charcoal, ochre
    /// and quarried blocks. The plan never reserves labor for a recipe its inputs cannot feed.
    /// </summary>
    private Fix64[] FeasibleBatches(SettlementState s, bool[] available)
    {
        var potential = new Fix64[SlotCount];
        for (int slot = 0; slot < SlotCount; slot++) potential[slot] = Stock(s, slot);
        MetalState m = s.Metal!;
        int civ = s.Civilization;
        foreach (MetalGood ore in new[] { MetalGood.CopperOre, MetalGood.ArsenicalOre, MetalGood.TinOre, MetalGood.IronOre, MetalGood.Gold, MetalGood.Salt })
            if (CanDig(s, ore)) potential[MetalSlot(ore)] = Plenty;
        // A deposit the planner may open a camp on, or a sister that digs it, counts too: the want is what opens the camp.
        if ((m.Access & (SourceCopper | AccessCopper)) != 0) potential[MetalSlot(MetalGood.CopperOre)] = Plenty;
        if ((m.Access & (SourceArsenic | AccessArsenic)) != 0) potential[MetalSlot(MetalGood.ArsenicalOre)] = Plenty;
        if ((m.Access & (SourceTin | AccessTin)) != 0) potential[MetalSlot(MetalGood.TinOre)] = Plenty;
        if (Done(civ, "MIN-01")) potential[MetalSlot(MetalGood.Ochre)] = Plenty;
        if (Done(civ, "MIN-03")) potential[MetalSlot(MetalGood.Block)] = Plenty;
        var result = new Fix64[_compiledRecipes.Length];
        for (int r = 0; r < _compiledRecipes.Length; r++)
        {
            if (!available[r]) continue;
            CompiledRecipe recipe = _compiledRecipes[r];
            Fix64 batches = Plenty;
            for (int i = 0; i < recipe.InputSlots.Length; i++)
            {
                Fix64 have = Fix64.Zero;
                foreach (int slot in recipe.InputSlots[i])
                    have += recipe.IsCharcoal && slot == (int)Good.Wood ? Plenty : potential[slot];
                Fix64 q = InputQuantity(s, recipe, i);
                if (q > Fix64.Zero) batches = Fix64.Min(batches, Fix64.Min(Plenty, have) / q);
            }
            result[r] = batches;
            potential[recipe.OutputSlot] = Fix64.Min(Plenty, potential[recipe.OutputSlot] + batches * OutputQuantity(s, recipe));
        }
        _ = m;
        return result;
    }

    /// <summary>Whether the settlement can dig an ore this season: a workable deposit in range or one of its camps on it.</summary>
    private bool CanDig(SettlementState s, MetalGood ore)
    {
        if (BestRangeDeposit(s, ore, out _) >= 0) return true;
        foreach (MiningCampState camp in _camps)
            if (camp.Parent == s.Id && OreGood(_world.Deposit(camp.Tile).Kind) == ore && _ore[camp.Tile] > Fix64.Zero) return true;
        return false;
    }

    private int FirstProducer(SettlementState s, int slot, bool[] available)
    {
        for (int r = 0; r < _compiledRecipes.Length; r++)
            if (available[r] && _compiledRecipes[r].OutputSlot == slot) return r;
        return -1;
    }

    /// <summary>An "A|B" input is planned on the first alternative that covers it, else on the first.</summary>
    private static int PreferredSlot(SettlementState s, int[] slots, Fix64 need)
    {
        foreach (int slot in slots) if (Stock(s, slot) >= need) return slot;
        return slots[0];
    }

    private Fix64 MiningLaborNeed(SettlementState s, MetalPlan plan)
    {
        Fix64 labor = Fix64.Zero;
        int civ = s.Civilization;
        for (int g = 0; g < (int)MetalGood.Count; g++)
        {
            Fix64 net = plan.Want[MetalSlot((MetalGood)g)] - s.Metal!.Goods[g];
            if (net <= Fix64.Zero) continue;
            int tile = BestRangeDeposit(s, (MetalGood)g, out Fix64 rate);
            if (tile < 0 || rate <= Fix64.Zero) continue;
            labor += Fix64.Min(net, _ore[tile]) / (rate * UnskilledMiner);
        }
        _ = civ;
        return labor;
    }

    private int BestRangeDeposit(SettlementState s, MetalGood good, out Fix64 rate)
    {
        rate = Fix64.Zero;
        int best = -1;
        int civ = s.Civilization;
        foreach (int tile in RangeTiles(s.SiteTile))
        {
            WorldDeposit d = _world.Deposit(tile);
            if (OreGood(d.Kind) != good || !DepositWorkable(civ, tile) || IsRivalTile(civ, tile)) continue;
            Fix64 r = MineRate(civ, tile, HasMineHead(s, tile), out _);
            if (r <= Fix64.Zero) continue;
            if (best < 0 || r > rate) { best = tile; rate = r; }
        }
        return best;
    }

    /// <summary>Chooses the metal structure to build next (forge, kiln, smelter, mine head) and returns its remaining labor.</summary>
    private Fix64 PlanMetalBuild(SettlementState s, MetalState m, MetalPlan plan)
    {
        int civ = s.Civilization;
        if (m.BuildKind != MetalBuild.None && !MaterialsObtainable(s, m.BuildKind))
        {
            m.BuildKind = MetalBuild.None; m.BuildTile = -1; m.BuildProgress = Fix64.Zero;
            s.Stores[(int)Good.Clay] += m.SiteClay; s.Stores[(int)Good.Stone] += m.SiteStone;
            m.SiteClay = Fix64.Zero; m.SiteStone = Fix64.Zero;
        }
        if (m.BuildKind == MetalBuild.None)
        {
            bool smelting = plan.Want[MetalSlot(MetalGood.CopperOre)] > Fix64.Zero || plan.Want[MetalSlot(MetalGood.ArsenicalOre)] > Fix64.Zero ||
                            plan.Want[MetalSlot(MetalGood.TinOre)] > Fix64.Zero || plan.Want[MetalSlot(MetalGood.Copper)] > Fix64.Zero ||
                            plan.Want[MetalSlot(MetalGood.Bronze)] > Fix64.Zero || plan.Want[MetalSlot(MetalGood.ArsenicalBronze)] > Fix64.Zero ||
                            plan.Tier != 0; // kits need smelted metal even before the furnace exists to plan it
            bool casting = plan.Tier != 0 || plan.Terminal[MetalSlot(MetalGood.Arms1)] > Fix64.Zero;
            // Every settlement that can cast builds its own forge, then a furnace (docs/MVP/23 §7.3: the chain is per settlement).
            if (Done(civ, "MET-03") && m.Forges == 0 && casting && MaterialsObtainable(s, MetalBuild.Forge)) m.BuildKind = MetalBuild.Forge;
            else if (smelting && m.Forges > 0 && s.Structures[(int)Structure.Kiln] == 0 && m.Smelters == 0 && CanBuild(civ, Structure.Kiln) &&
                     MaterialsObtainable(s, MetalBuild.Kiln)) m.BuildKind = MetalBuild.Kiln;
            else if (smelting && m.Forges > 0 && Done(civ, "FIR-09") && m.Smelters == 0 && MaterialsObtainable(s, MetalBuild.Smelter)) m.BuildKind = MetalBuild.Smelter;
            else if (Done(civ, "MIN-04"))
            {
                for (int g = 0; g < (int)MetalGood.Count && m.BuildKind == MetalBuild.None; g++)
                {
                    if (plan.Want[MetalSlot((MetalGood)g)] <= m.Goods[g]) continue;
                    foreach (int tile in RangeTiles(s.SiteTile))
                    {
                        WorldDeposit d = _world.Deposit(tile);
                        if (OreGood(d.Kind) != (MetalGood)g || m.MineHeads.Contains(tile) || !DepositWorkable(civ, tile) || IsRivalTile(civ, tile)) continue;
                        m.BuildKind = MetalBuild.MineHead;
                        m.BuildTile = tile;
                        break;
                    }
                }
            }
        }
        if (m.BuildKind == MetalBuild.None) return Fix64.Zero;
        return Fix64.Max(Fix64.Zero, MetalBuildLabor(m.BuildKind) - m.BuildProgress) + MaterialLabor(s, m.BuildKind);
    }

    private bool SisterHasForge(SettlementState s)
    {
        int reach = ShareReach(s.Civilization);
        if (reach < 0) return false;
        foreach (SettlementState o in _settlements)
            if (o != s && o.Civilization == s.Civilization && !IsFreeFolk(o) && !IsFreeFolk(s) && o.Metal is { } om && (om.Forges > 0 || om.BuildKind == MetalBuild.Forge) &&
                Population(o.Id) > 0 && HexGrid.Distance(o.SiteTile, s.SiteTile, _world.Width) <= reach) return true;
        return false;
    }

    private static (int Clay, int Stone) MetalBuildMaterials(MetalBuild kind) => kind switch
    {
        MetalBuild.Forge => (2, 2),
        MetalBuild.Kiln => (3, 2),
        MetalBuild.Smelter => (4, 3),
        _ => (0, 0),
    };

    /// <summary>Clay per work-season: from a clay deposit in range (3 L, docs/MVP/10 §5.3), else at a riverbank, lake shore or wetland edge (1 L), else subsoil (0.5 L).</summary>
    private Fix64 ClayRate(SettlementState s, out bool deposit)
    {
        deposit = false;
        foreach (int tile in RangeTiles(s.SiteTile))
            if (_clayStock[tile] > Fix64.Zero) { deposit = true; return Fix64.FromInt(3); }
        foreach (int tile in RangeTiles(s.SiteTile))
        {
            WorldEconomyTile t = _world.Tile(tile);
            if (t.River || t.Lake || (Biome)t.BiomeId == Biome.Wetland) return Fix64.One;
        }
        // Anywhere else a little clay for moulds and furnace linings can be dug from subsoil, slowly (a WP6 decision:
        // without it most E3 settlements could never build a forge, docs/MVP/07d "WP6 live results").
        return Fix64.Half;
    }

    /// <summary>Whether a metal structure's clay and stone can be had: in stores, dug in range, or sent by a sister settlement.</summary>
    private bool MaterialsObtainable(SettlementState s, MetalBuild kind)
    {
        (int clay, int stone) = MetalBuildMaterials(kind);
        if (kind == MetalBuild.MineHead) return TakeWood(s, s.SiteTile, Fix64.FromInt(6), commit: false) >= Fix64.FromInt(6);
        Fix64 stoneInRange = s.Stores[(int)Good.Stone];
        foreach (int tile in RangeTiles(s.SiteTile)) stoneInRange += _stoneStock[tile];
        if (stoneInRange + s.Metal!.SiteStone < Fix64.FromInt(stone)) return false;
        if (kind == MetalBuild.Smelter && s.Stores[(int)Good.Leather] + s.Stores[(int)Good.Hide] < Fix64.FromInt(2)) return false;
        return s.Stores[(int)Good.Clay] + s.Metal!.SiteClay >= Fix64.FromInt(clay) || ClayRate(s, out _) > Fix64.Zero || SisterClay(s, Fix64.FromInt(clay)) >= 0;
    }

    /// <summary>The index of a sister settlement within the sharing reach that can spare <paramref name="need"/> clay, or -1.</summary>
    private int SisterClay(SettlementState s, Fix64 need)
    {
        int reach = ShareReach(s.Civilization);
        if (reach < 0) return -1;
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState o = _settlements[i];
            if (o == s || o.Civilization != s.Civilization || IsFreeFolk(o) || IsFreeFolk(s) || HexGrid.Distance(o.SiteTile, s.SiteTile, _world.Width) > reach) continue;
            if (o.Stores[(int)Good.Clay] >= need + Fix64.FromInt(3)) return i;
        }
        return -1;
    }

    private Fix64 MaterialLabor(SettlementState s, MetalBuild kind)
    {
        (int clay, int stone) = MetalBuildMaterials(kind);
        MetalState m = s.Metal!;
        Fix64 labor = Fix64.Zero;
        Fix64 missingClay = Fix64.Max(Fix64.Zero, Fix64.FromInt(clay) - m.SiteClay - s.Stores[(int)Good.Clay]);
        Fix64 missingStone = Fix64.Max(Fix64.Zero, Fix64.FromInt(stone) - m.SiteStone - s.Stores[(int)Good.Stone]);
        Fix64 clayRate = ClayRate(s, out _);
        if (missingClay > Fix64.Zero && clayRate > Fix64.Zero) labor += missingClay / clayRate;
        labor += missingStone;
        return labor;
    }

    /// <summary>
    /// Brings a metal structure's clay and stone to the site (from the stores, a sister settlement, or dug in range with the
    /// builders' labor); returns the labor left for building. Site materials are out of the stores, so mudbrick-making
    /// cannot eat them.
    /// </summary>
    private Fix64 GatherForBuild(SettlementState s, MetalBuild kind, Fix64 labor)
    {
        MetalState m = s.Metal!;
        (int clay, int stone) = MetalBuildMaterials(kind);
        Fix64 missingClay = Fix64.Max(Fix64.Zero, Fix64.FromInt(clay) - m.SiteClay);
        Fix64 fromStores = Fix64.Min(missingClay, s.Stores[(int)Good.Clay]);
        s.Stores[(int)Good.Clay] -= fromStores; m.SiteClay += fromStores; missingClay -= fromStores;
        if (missingClay > Fix64.Zero && SisterClay(s, missingClay) is int sister and >= 0)
        {
            _settlements[sister].Stores[(int)Good.Clay] -= missingClay;
            m.SiteClay += missingClay;
            missingClay = Fix64.Zero;
        }
        if (missingClay > Fix64.Zero && labor > Fix64.Zero)
        {
            Fix64 rate = ClayRate(s, out bool deposit);
            if (rate > Fix64.Zero)
            {
                Fix64 got = Fix64.Min(missingClay, labor * rate);
                if (deposit)
                {
                    Fix64 left = got;
                    foreach (int tile in RangeTiles(s.SiteTile))
                    {
                        Fix64 take = Fix64.Min(left, _clayStock[tile]);
                        _clayStock[tile] -= take;
                        left -= take;
                        if (left <= Fix64.Zero) break;
                    }
                    got -= left;
                }
                m.SiteClay += got;
                labor -= got / rate;
            }
        }
        Fix64 missingStone = Fix64.Max(Fix64.Zero, Fix64.FromInt(stone) - m.SiteStone);
        Fix64 stoneStores = Fix64.Min(missingStone, s.Stores[(int)Good.Stone]);
        s.Stores[(int)Good.Stone] -= stoneStores; m.SiteStone += stoneStores; missingStone -= stoneStores;
        if (missingStone > Fix64.Zero && labor > Fix64.Zero)
        {
            Fix64 want = Fix64.Min(missingStone, labor);
            Fix64 left = want;
            foreach (int tile in RangeTiles(s.SiteTile))
            {
                Fix64 take = Fix64.Min(left, _stoneStock[tile]);
                _stoneStock[tile] -= take;
                left -= take;
                if (left <= Fix64.Zero) break;
            }
            m.SiteStone += want - left;
            labor -= want - left;
        }
        return Fix64.Max(Fix64.Zero, labor);
    }

    private static Fix64 MetalBuildLabor(MetalBuild kind) => kind switch
    {
        MetalBuild.Forge => Fix64.FromInt(2),
        MetalBuild.Kiln => Fix64.FromInt(2),
        MetalBuild.Smelter => Fix64.FromInt(3),
        MetalBuild.MineHead => Fix64.FromInt(4),
        _ => Fix64.Zero,
    };

    private bool TryPayMetalBuild(SettlementState s, MetalBuild kind)
    {
        MetalState m = s.Metal!;
        Fix64[] st = s.Stores;
        (int clay, int stone) = MetalBuildMaterials(kind);
        if (m.SiteClay < Fix64.FromInt(clay) || m.SiteStone < Fix64.FromInt(stone)) return false;
        int leather = (int)Good.Leather, hide = (int)Good.Hide;
        if (kind == MetalBuild.Smelter)
        {
            if (st[leather] + st[hide] < Fix64.FromInt(2)) return false;
            Fix64 fromLeather = Fix64.Min(st[leather], Fix64.FromInt(2));
            st[leather] -= fromLeather; st[hide] -= Fix64.FromInt(2) - fromLeather;
        }
        if (kind == MetalBuild.MineHead && TakeWood(s, s.SiteTile, Fix64.FromInt(6), commit: false) < Fix64.FromInt(6)) return false;
        if (kind == MetalBuild.MineHead) TakeWood(s, s.SiteTile, Fix64.FromInt(6), commit: true);
        // Leftover site materials go back to the stores.
        st[(int)Good.Clay] += m.SiteClay - Fix64.FromInt(clay);
        st[(int)Good.Stone] += m.SiteStone - Fix64.FromInt(stone);
        m.SiteClay = Fix64.Zero;
        m.SiteStone = Fix64.Zero;
        return true;
    }

    // ---- assignment ---------------------------------------------------------------------------------

    /// <summary>Gives a figure metal work in the plan's bucket order: building, mining, charcoal, smelting and casting.</summary>
    private void AssignMetalWork(FigureState figure, int index, Fix64 amount, MetalPlan plan, Fix64[] toolWork)
    {
        while (amount > Fix64.Zero)
        {
            Fix64 part;
            if (plan.BuildAssigned < plan.BuildNeed)
            {
                part = Fix64.Min(amount, plan.BuildNeed - plan.BuildAssigned);
                Assign(figure, EconomyJob.Build, part);
                if (figure.Task == 0) figure.Task = MetalTask.Build;
                plan.BuildAssigned += part;
            }
            else if (plan.MineAssigned < plan.MineNeed)
            {
                part = Fix64.Min(amount, plan.MineNeed - plan.MineAssigned);
                Assign(figure, EconomyJob.Gather, part);
                if (figure.Task == 0) figure.Task = MetalTask.Mine;
                plan.MineAssigned += part;
                plan.MineProductive += part * SkillMultiplier(figure, SkillFamily.Gather) * KitMod(figure, "mine");
                PracticeFigure(figure, SkillFamily.Gather, part);
            }
            else if (plan.CharcoalAssigned < plan.CharcoalNeed)
            {
                part = Fix64.Min(amount, plan.CharcoalNeed - plan.CharcoalAssigned);
                Assign(figure, EconomyJob.Chop, part);
                if (figure.Task == 0) figure.Task = MetalTask.Charcoal;
                plan.CharcoalAssigned += part;
            }
            else if (plan.SmeltAssigned < plan.SmeltNeed)
            {
                part = Fix64.Min(amount, plan.SmeltNeed - plan.SmeltAssigned);
                Assign(figure, EconomyJob.Craft, part);
                if (figure.Task == 0) figure.Task = MetalTask.Smelt;
                plan.SmeltAssigned += part;
            }
            else break;
            toolWork[index] += part;
            amount -= part;
        }
    }

    private static Fix64 Unassigned(MetalPlan plan)
        => plan.BuildNeed - plan.BuildAssigned + plan.MineNeed - plan.MineAssigned + plan.CharcoalNeed - plan.CharcoalAssigned +
           plan.SmeltNeed - plan.SmeltAssigned;

    private void PracticeFigure(FigureState figure, SkillFamily family, Fix64 work)
    {
        int column = (int)family;
        int level = figure.SkillLevels[column];
        if (level >= MaxSkillLevel || work <= Fix64.Zero) return;
        Fix64 progress = figure.SkillPractice[column] + work;
        while (level < MaxSkillLevel)
        {
            Fix64 threshold = level < 6 ? SkillWorkPerLevel : VeteranSkillWorkPerLevel;
            if (progress < threshold) break;
            progress -= threshold;
            level++;
        }
        figure.SkillLevels[column] = (byte)level;
        figure.SkillPractice[column] = level == MaxSkillLevel ? Fix64.Zero : progress;
    }

    // ---- the season's work ---------------------------------------------------------------------------

    private void RunMetal(SettlementState s, MetalPlan plan, Fix64 craftLeft, Fix64[] toolWork, int season)
    {
        MetalState m = s.Metal!;
        int civ = s.Civilization;
        SupplyFromHerds(s, m);

        // 1. The metal structure under construction.
        Fix64 spare = Fix64.Zero;
        if (m.BuildKind != MetalBuild.None)
        {
            m.BuildProgress += GatherForBuild(s, m.BuildKind, plan.BuildAssigned);
            if (m.BuildProgress >= MetalBuildLabor(m.BuildKind) && TryPayMetalBuild(s, m.BuildKind))
            {
                spare += m.BuildProgress - MetalBuildLabor(m.BuildKind);
                switch (m.BuildKind)
                {
                    case MetalBuild.Forge: m.Forges++; break;
                    case MetalBuild.Smelter: m.Smelters++; break;
                    case MetalBuild.Kiln: s.Structures[(int)Structure.Kiln]++; break;
                    case MetalBuild.MineHead: if (m.BuildTile >= 0 && !m.MineHeads.Contains(m.BuildTile)) m.MineHeads.Add(m.BuildTile); break;
                }
                m.BuildKind = MetalBuild.None;
                m.BuildTile = -1;
                m.BuildProgress = Fix64.Zero;
            }
        }

        // SOC-08: full-time Miners, Smiths and Potters in Towns and Cities work 30% faster (docs/MVP/22 §8.5).
        Fix64 specialist = s.Civic is { Tier: >= 1 } && Done(civ, "SOC-08") ? _civic.SpecialistRate : Fix64.One;
        // 2. Mining in range (a settlement job): the miners' work is shared among the wanted ores in proportion to the
        // labor each needs, then what one ore leaves goes to the next in the fixed good order.
        Fix64 productive = plan.MineProductive * specialist;
        var oreNeed = new Fix64[(int)MetalGood.Count];
        Fix64 needSum = Fix64.Zero;
        for (int g = 0; g < (int)MetalGood.Count; g++)
        {
            Fix64 net = plan.Want[MetalSlot((MetalGood)g)] - m.Goods[g];
            if (net <= Fix64.Zero || BestRangeDeposit(s, (MetalGood)g, out Fix64 rate) < 0 || rate <= Fix64.Zero) continue;
            oreNeed[g] = net / rate;
            needSum += oreNeed[g];
        }
        for (int round = 0; round < 2 && productive > Epsilon && needSum > Fix64.Zero; round++)
        {
            Fix64 pool = productive;
            for (int g = 0; g < (int)MetalGood.Count && productive > Epsilon; g++)
            {
                if (oreNeed[g] <= Fix64.Zero) continue;
                Fix64 share = round == 0 ? Fix64.Min(productive, pool * oreNeed[g] / needSum) : productive;
                Fix64 net = plan.Want[MetalSlot((MetalGood)g)] - m.Goods[g];
                // Each pass empties a deposit, meets the want or spends the work; a few passes cover several deposits of one ore.
                for (int pass = 0; pass < 4 && net > Epsilon && share > Epsilon; pass++)
                {
                    int tile = BestRangeDeposit(s, (MetalGood)g, out Fix64 rate);
                    if (tile < 0) break;
                    Fix64 dug = Dig(s, tile, rate, share, net, HasMineHead(s, tile));
                    if (dug <= Epsilon) break;
                    Fix64 used = Fix64.Min(share, dug / rate);
                    share -= used;
                    productive = Fix64.Max(Fix64.Zero, productive - used);
                    net -= dug;
                }
            }
        }
        if (plan.MineAssigned > Fix64.Zero && plan.MineProductive > Fix64.Zero)
            spare += plan.MineAssigned * productive / (plan.MineProductive * specialist);


        // 4. The craft step: every recipe in ascending order, then id, each limited by its scarcest input.
        Fix64 metalPool = (plan.CharcoalAssigned + plan.SmeltAssigned) * specialist + spare;
        bool arsenic = false;
        bool fuelShort = false;
        for (int r = 0; r < _compiledRecipes.Length; r++)
        {
            CompiledRecipe recipe = _compiledRecipes[r];
            if (plan.Batches[r] <= Fix64.Zero || !RecipeAvailable(s, recipe)) continue;
            if (recipe.OutputSlot == MetalSlot(MetalGood.Pigment)) GatherOchre(s, m, plan, ref craftLeft);
            if (recipe.OutputSlot == MetalSlot(MetalGood.Ashlar)) QuarryBlocks(s, m, plan, ref craftLeft);
            if (recipe.OutputSlot == MetalSlot(MetalGood.Salted)) PanSalt(s, m, plan, season, ref craftLeft);
            Fix64 made = recipe.IsCharcoal
                ? BurnCharcoal(s, m, recipe, plan, Fix64.Min(plan.Batches[r], CharcoalNeededNow(s, plan, r)), ref metalPool, ref craftLeft, out fuelShort)
                : RunRecipe(s, recipe, plan.Batches[r], plan.AnyMetalKit, ref metalPool, ref craftLeft);
            MetalDebug?.Append($" {recipe.Def.Id}:{plan.Batches[r].ToDoubleForDisplay():F2}->{made.ToDoubleForDisplay():F2}(pool {metalPool.ToDoubleForDisplay():F2}/{craftLeft.ToDoubleForDisplay():F2})");
            if (made <= Fix64.Zero) continue;
            RecordRecipe(civ, recipe, made, OutputQuantity(s, recipe));
            if (recipe.Def.Id == "cosmelt_arsenical") arsenic = true;
        }
        m.FuelShortSeasons = fuelShort ? m.FuelShortSeasons + 1 : 0;
        if (arsenic && Done(civ, "MET-04"))
            foreach (FigureState f in _figures)
                if (f.SettlementId == s.Id && f.Task == MetalTask.Smelt) f.Task = MetalTask.SmeltArsenic;

        // 5. Kits: wear by this season's tool work, then hand out what was cast.
        WearAndIssueKits(s, m, plan.Tier, toolWork);

        // 6. Goods that wear by age (arms, carts, chariots, wool garments: "24 seasons" and the like).
        for (int g = 0; g < (int)MetalGood.Count; g++)
        {
            if (m.Goods[g] <= Fix64.Zero) continue;
            int t = _content.TradeGoodIndex(MetalGoodId((MetalGood)g));
            int seasons = t >= 0 ? _content.TradeGoods[t].ServiceSeasons : 0;
            if (seasons > 0) m.Goods[g] -= m.Goods[g] / seasons;
        }

        // 7. Statistics for the harness (T17): labor, people.
        Fix64 work = Fix64.Zero;
        foreach (FigureState f in _figures) if (f.SettlementId == s.Id) work += f.WorkThisSeason;
        AddMetalStat(civ, StatLabor, work);
        // Metal labor is what the chain used: the reserved labor less what the recipes left idle, plus the camp crews' work.
        Fix64 idleMetal = Fix64.Min(metalPool, plan.CharcoalAssigned + plan.SmeltAssigned + spare);
        // T17 counts mining, charcoal and smelting (22 §5.6); building the forge and furnaces is construction.
        AddMetalStat(civ, StatMetalLabor, plan.MineAssigned + plan.CharcoalAssigned + plan.SmeltAssigned - idleMetal + plan.CampWork);
        AddMetalStat(civ, StatPeopleSeasons, Fix64.FromInt(Population(s.Id)));
        OpenCampsIfNeeded(s, plan, season);
    }

    private void RecordRecipe(int civ, CompiledRecipe recipe, Fix64 batches, Fix64 outputPerBatch)
    {
        Fix64 made = batches * outputPerBatch;
        switch (recipe.Def.Id)
        {
            case "alloy_bronze": AddMetalStat(civ, StatBronze, made); break;
            case "cosmelt_arsenical": AddMetalStat(civ, StatArsenicalBronze, made); break;
            case "smelt_copper": AddMetalStat(civ, StatCopper, made); break;
            case "smelt_tin": AddMetalStat(civ, StatTin, made); break;
            case "burn_charcoal": AddMetalStat(civ, StatCharcoal, made); break;
            case "cast_t8": AddMetalStat(civ, StatKits8, made); break;
            case "cast_t9": AddMetalStat(civ, StatKits9, made); break;
            case "cast_t10": AddMetalStat(civ, StatKits10, made); break;
            case "cast_arms1": AddMetalStat(civ, StatArmsCast, made); break;
            case "cast_arms2a": case "cast_arms2t": AddMetalStat(civ, StatArmsCast, made); AddMetalStat(civ, StatBronzeArms, made); break;
        }
    }

    /// <summary>
    /// Charcoal the later recipes can actually burn this season: each planned recipe that takes charcoal, limited by its
    /// other inputs as they stand now (ore just dug, metal in stock), less the charcoal in store. Charcoal is burned to use.
    /// </summary>
    private Fix64 CharcoalNeededNow(SettlementState s, MetalPlan plan, int charcoalRecipe)
    {
        int charcoal = MetalSlot(MetalGood.Charcoal);
        var stock = new Fix64[SlotCount];
        for (int slot = 0; slot < SlotCount; slot++) stock[slot] = Stock(s, slot);
        Fix64 need = Fix64.Zero;
        for (int r = charcoalRecipe + 1; r < _compiledRecipes.Length; r++)
        {
            CompiledRecipe recipe = _compiledRecipes[r];
            if (plan.Batches[r] <= Fix64.Zero || !RecipeAvailable(s, recipe)) continue;
            Fix64 batches = plan.Batches[r];
            Fix64 charPer = Fix64.Zero;
            for (int i = 0; i < recipe.InputSlots.Length; i++)
            {
                Fix64 q = InputQuantity(s, recipe, i);
                if (Array.IndexOf(recipe.InputSlots[i], charcoal) >= 0) { charPer += q; continue; }
                Fix64 have = Fix64.Zero;
                foreach (int slot in recipe.InputSlots[i]) have += stock[slot];
                if (q > Fix64.Zero) batches = Fix64.Min(batches, have / q);
            }
            if (charPer <= Fix64.Zero || batches <= Fix64.Zero) continue;
            need += batches * charPer;
            // What this recipe makes feeds the next ones (copper into bronze into kits).
            stock[recipe.OutputSlot] += batches * OutputQuantity(s, recipe);
        }
        Fix64 net = need - Stock(s, charcoal);
        return net <= Fix64.Zero ? Fix64.Zero : net / _compiledRecipes[charcoalRecipe].OutputQuantity;
    }

    /// <summary>Runs one recipe for up to <paramref name="planned"/> batches; returns the batches made.</summary>
    private Fix64 RunRecipe(SettlementState s, CompiledRecipe recipe, Fix64 planned, bool anyMetalKit, ref Fix64 metalPool, ref Fix64 craftPool)
    {
        Fix64 labor = RecipeLabor(s, recipe, anyMetalKit);
        Fix64 pool = metalPool + craftPool;
        Fix64 batches = planned;
        if (labor > Fix64.Zero) batches = Fix64.Min(batches, pool / labor);
        for (int i = 0; i < recipe.InputSlots.Length; i++)
        {
            Fix64 have = Fix64.Zero;
            foreach (int slot in recipe.InputSlots[i]) have += Stock(s, slot);
            Fix64 q = InputQuantity(s, recipe, i);
            if (q > Fix64.Zero) batches = Fix64.Min(batches, have / q);
        }
        if (batches <= Fix64.Zero) return Fix64.Zero;
        for (int i = 0; i < recipe.InputSlots.Length; i++)
        {
            Fix64 need = batches * InputQuantity(s, recipe, i);
            foreach (int slot in recipe.InputSlots[i])
            {
                Fix64 take = Fix64.Min(need, Stock(s, slot));
                AddStock(s, slot, -take);
                need -= take;
                if (need <= Fix64.Zero) break;
            }
        }
        AddStock(s, recipe.OutputSlot, batches * OutputQuantity(s, recipe));
        SpendLabor(batches * labor, recipe.Def.Order < CraftOrder, ref metalPool, ref craftPool);
        return batches;
    }

    private static void SpendLabor(Fix64 labor, bool metalFirst, ref Fix64 metalPool, ref Fix64 craftPool)
    {
        if (metalFirst)
        {
            Fix64 a = Fix64.Min(labor, metalPool); metalPool -= a; labor -= a;
            craftPool = Fix64.Max(Fix64.Zero, craftPool - labor);
        }
        else
        {
            Fix64 a = Fix64.Min(labor, craftPool); craftPool -= a; labor -= a;
            metalPool = Fix64.Max(Fix64.Zero, metalPool - labor);
        }
    }

    /// <summary>
    /// Charcoal (docs/MVP/23 §7.4): wood comes from the settlement's cut wood above a hearth-and-building reserve, then from
    /// the standing stock of forest tiles, nearest first, in range and then out to the clamp reach (8 tiles, 12 with
    /// carts) at the haul cost. Never from deadwood. Returns the batches (litres of charcoal) made.
    /// </summary>
    private Fix64 BurnCharcoal(SettlementState s, MetalState m, CompiledRecipe recipe, MetalPlan plan, Fix64 planned,
        ref Fix64 metalPool, ref Fix64 craftPool, out bool fuelShort)
    {
        fuelShort = false;
        Fix64 woodPer = recipe.InputQuantity[0];
        Fix64 burn = RecipeLabor(s, recipe, plan.AnyMetalKit);
        Fix64 made = Fix64.Zero;
        Fix64 woodUsed = Fix64.Zero;
        // From cut wood first.
        Fix64 stored = Fix64.Max(Fix64.Zero, s.Stores[(int)Good.Wood] - WoodReserve);
        if (stored > Fix64.Zero && planned > Fix64.Zero)
        {
            Fix64 batches = Fix64.Min(planned, stored / woodPer);
            if (burn > Fix64.Zero) batches = Fix64.Min(batches, (metalPool + craftPool) / burn);
            if (batches > Fix64.Zero)
            {
                s.Stores[(int)Good.Wood] -= batches * woodPer;
                SpendLabor(batches * burn, true, ref metalPool, ref craftPool);
                made += batches;
                woodUsed += batches * woodPer;
                planned -= batches;
            }
        }
        // Then the range's standing stock, cut across its forest tiles in proportion to what each holds (clamps move through
        // the woods, as metal_sim.py draws), so coppiced stands regrow everywhere; then beyond the range, nearest first, hauled.
        int reach = Done(s.Civilization, "TRN-05") ? _bronzeConstants.CharcoalReachCarts : _bronzeConstants.CharcoalReach;
        int[] tiles = ReachTiles(s.SiteTile, reach);
        Fix64 haulDivisor = Done(s.Civilization, "TRN-05") ? Fix64.FromInt(5) : Fix64.One;
        Fix64 inRange = Fix64.Zero;
        foreach (int tile in RangeTiles(s.SiteTile))
            if (IsForestTile(tile) && !IsRivalTile(s.Civilization, tile)) inRange += _woodStock[tile];
        if (planned > Fix64.Zero && inRange > Fix64.Zero)
        {
            Fix64 perBatch = burn + woodPer / plan.MeanChopRate;
            Fix64 batches = Fix64.Min(planned, inRange / woodPer);
            if (perBatch > Fix64.Zero) batches = Fix64.Min(batches, (metalPool + craftPool) / perBatch);
            if (batches > Fix64.Zero)
            {
                Fix64 wood = batches * woodPer, share = wood / inRange, taken = Fix64.Zero;
                int most = -1;
                foreach (int tile in RangeTiles(s.SiteTile))
                {
                    if (!IsForestTile(tile) || IsRivalTile(s.Civilization, tile) || _woodStock[tile] <= Fix64.Zero) continue;
                    Fix64 take = Fix64.Min(_woodStock[tile], _woodStock[tile] * share);
                    if (most < 0 || _woodStock[tile] > _woodStock[most]) most = tile;
                    _woodStock[tile] -= take;
                    taken += take;
                }
                if (most >= 0 && taken < wood) { Fix64 rest = Fix64.Min(wood - taken, _woodStock[most]); _woodStock[most] -= rest; taken += rest; }
                Fix64 done = taken / woodPer;
                SpendLabor(done * perBatch, true, ref metalPool, ref craftPool);
                made += done;
                woodUsed += taken;
                planned -= done;
                m.ClampTile = most;
            }
        }
        foreach (int tile in tiles)
        {
            if (planned <= Fix64.Zero || metalPool + craftPool <= Fix64.Zero) break;
            int distance = HexGrid.Distance(s.SiteTile, tile, _world.Width);
            if (distance <= Radius) continue;
            if (!IsForestTile(tile) || _woodStock[tile] <= Fix64.Zero || IsRivalTile(s.Civilization, tile)) continue;
            Fix64 haul = HaulPerLitreTile * distance / haulDivisor;
            Fix64 perBatch = burn + woodPer / plan.MeanChopRate + haul;
            Fix64 batches = Fix64.Min(planned, _woodStock[tile] / woodPer);
            if (perBatch > Fix64.Zero) batches = Fix64.Min(batches, (metalPool + craftPool) / perBatch);
            if (batches <= Fix64.Zero) continue;
            _woodStock[tile] -= batches * woodPer;
            SpendLabor(batches * perBatch, true, ref metalPool, ref craftPool);
            made += batches;
            woodUsed += batches * woodPer;
            planned -= batches;
            m.ClampTile = tile;
        }
        if (planned > Fix64.FromRatio(1, 100) && metalPool + craftPool > Fix64.FromRatio(1, 100)) fuelShort = true;
        if (made > Fix64.Zero) m.Goods[(int)MetalGood.Charcoal] += made * recipe.OutputQuantity;
        AddMetalStat(s.Civilization, StatCharcoalWood, woodUsed);
        return made;
    }

    /// <summary>Tiles within <paramref name="reach"/> of a site, nearest first, then by id (derived and cached; never state).</summary>
    private int[] ReachTiles(int site, int reach)
    {
        int[]? cached = _reachTiles[site];
        if (cached is not null && cached.Length > 0 && HexGrid.Distance(site, cached[^1], _world.Width) >= Math.Min(reach, 12)) return Filter(cached, site, reach);
        var list = new List<int>();
        int max = Math.Max(reach, 12);
        int width = _world.Width, height = _world.Height;
        HexGrid.Coordinates(site, width, out int x, out int y);
        for (int ny = Math.Max(0, y - max); ny <= Math.Min(height - 1, y + max); ny++)
        for (int nx = Math.Max(0, x - max - 1); nx <= Math.Min(width - 1, x + max + 1); nx++)
        {
            int tile = ny * width + nx;
            if (HexGrid.Distance(site, tile, width) <= max) list.Add(tile);
        }
        list.Sort((a, b) =>
        {
            int da = HexGrid.Distance(site, a, width), db = HexGrid.Distance(site, b, width);
            return da != db ? da.CompareTo(db) : a.CompareTo(b);
        });
        _reachTiles[site] = list.ToArray();
        return Filter(_reachTiles[site]!, site, reach);
    }

    private int[] Filter(int[] tiles, int site, int reach)
    {
        int count = 0;
        while (count < tiles.Length && HexGrid.Distance(site, tiles[count], _world.Width) <= reach) count++;
        if (count == tiles.Length) return tiles;
        var result = new int[count];
        Array.Copy(tiles, result, count);
        return result;
    }

    /// <summary>Ochre for pigment (MIN-01: 0.5 L a work-season from surface outcrops, docs/MVP/10 §5.4), from craft labor.</summary>
    private void GatherOchre(SettlementState s, MetalState m, MetalPlan plan, ref Fix64 craftPool)
    {
        if (!Done(s.Civilization, "MIN-01")) return;
        Fix64 net = plan.Want[MetalSlot(MetalGood.Ochre)] - m.Goods[(int)MetalGood.Ochre];
        if (net <= Fix64.Zero || craftPool <= Fix64.Zero) return;
        Fix64 litres = Fix64.Min(net, craftPool * Fix64.Half);
        m.Goods[(int)MetalGood.Ochre] += litres;
        craftPool -= litres * 2;
    }

    /// <summary>
    /// Quarried blocks for dressing (MIN-03: 2 L a work-season, docs/MVP/10 §5.4; KEY-3 books them as their own good), from
    /// the stone of the range at the quarry rate of the best kit in the settlement (×1.1 copper, ×1.3 bronze).
    /// </summary>
    private void QuarryBlocks(SettlementState s, MetalState m, MetalPlan plan, ref Fix64 craftPool)
    {
        if (!Done(s.Civilization, "MIN-03")) return;
        Fix64 net = plan.Want[MetalSlot(MetalGood.Block)] - m.Goods[(int)MetalGood.Block];
        if (net <= Fix64.Zero || craftPool <= Fix64.Zero) return;
        Fix64 best = Fix64.One;
        foreach (FigureState f in _figures)
            if (f.SettlementId == s.Id && f.Kit != 0) best = Fix64.Max(best, KitMod(f, "quarry"));
        Fix64 rate = BaseRate("quarry", Fix64.FromInt(2)) * best;
        Fix64 want = Fix64.Min(net, craftPool * rate);
        Fix64 left = want;
        foreach (int tile in RangeTiles(s.SiteTile))
        {
            Fix64 take = Fix64.Min(left, _stoneStock[tile]);
            _stoneStock[tile] -= take;
            left -= take;
            if (left <= Fix64.Zero) break;
        }
        Fix64 got = want - left;
        m.Goods[(int)MetalGood.Block] += got;
        craftPool -= got / rate;
    }

    /// <summary>Wool from sheep (HUS-06: 0.5 L a year each) and milk from the herds (HUS-05), the raw goods of wool garments and cheese.</summary>
    private void SupplyFromHerds(SettlementState s, MetalState m)
    {
        int civ = s.Civilization;
        if (Done(civ, "HUS-06")) m.Goods[(int)MetalGood.Wool] += s.DomesticSmallStock * Fix64.Half / 4;
        // Milk comes with the herd work (HUS-05 dairying, LivingEconomyModel.Food: half the herd's food again, WP7).
    }

    /// <summary>Digs ore at a tile with <paramref name="productive"/> skill- and kit-weighted work-seasons; returns litres dug.</summary>
    private Fix64 Dig(SettlementState s, int tile, Fix64 rate, Fix64 productive, Fix64 want, bool mineHead)
    {
        MetalState m = s.Metal!;
        MetalGood good = OreGood(_world.Deposit(tile).Kind)!.Value;
        Fix64 litres = Fix64.Min(want, Fix64.Min(_ore[tile], productive * rate));
        if (litres <= Fix64.Zero) return Fix64.Zero;
        MineRate(s.Civilization, tile, mineHead, out bool fireSetting);
        if (fireSetting)
        {
            // Fire-setting burns half a litre of wood per litre of ore, from cut wood, else the nearest standing stock.
            Fix64 wood = litres * _bronzeConstants.FireSettingWood;
            Fix64 fromStore = Fix64.Min(wood, Fix64.Max(Fix64.Zero, s.Stores[(int)Good.Wood] - WoodReserve));
            s.Stores[(int)Good.Wood] -= fromStore;
            Fix64 rest = wood - fromStore;
            foreach (int t in RangeTiles(s.SiteTile))
            {
                if (rest <= Fix64.Zero) break;
                if (!IsForestTile(t)) continue;
                Fix64 take = Fix64.Min(rest, _woodStock[t]);
                _woodStock[t] -= take;
                rest -= take;
            }
            if (rest > Fix64.Zero)
            {
                // Not enough wood: the shortfall is dug at the Shaft rate instead.
                Fix64 shaft = MethodMult(s.Civilization, "mine", 1);
                Fix64 fire = MethodMult(s.Civilization, "mine", 2);
                if (fire > Fix64.Zero && shaft > Fix64.Zero) litres -= rest / _bronzeConstants.FireSettingWood * (Fix64.One - shaft / fire);
            }
            AddMetalStat(s.Civilization, StatFireSettingWood, wood - rest);
        }
        litres = Fix64.Max(Fix64.Zero, Fix64.Min(litres, _ore[tile]));
        _ore[tile] -= litres;
        m.Goods[(int)good] += litres;
        AddMetalStat(s.Civilization, StatOreMined, litres);
        return litres;
    }

    // ---- mining camps ---------------------------------------------------------------------------------

    private int CampCrewCount(MiningCampState camp)
    {
        int n = 0;
        foreach (FigureState f in _figures) if (f.Camp == camp.Id) n++;
        return n;
    }

    private int CampCrewCap(int civ)
    {
        Fix64 mod = Modifier(civ, "mining_camp_crew");
        return mod != Fix64.One && mod > Fix64.Zero ? mod.FloorToInt() : _bronzeConstants.CampCrew;
    }

    /// <summary>
    /// Keeps every camp crewed from its parent (4, or 6 with MIN-04's mining_camp_crew) and closes camps whose deposit is
    /// exhausted, whose parent is gone or which cannot keep two miners (docs/MVP/22 §5.2).
    /// </summary>
    private void MaintainCamps(int season)
    {
        for (int c = _camps.Count - 1; c >= 0; c--)
        {
            MiningCampState camp = _camps[c];
            SettlementState parent = _settlements[camp.Parent];
            foreach (FigureState f in _figures)
                if (f.Camp == camp.Id && f.SettlementId != parent.Id) f.Camp = -1;
            int cap = Math.Clamp(camp.Target, _bronzeConstants.CampMinCrew, CampCrewCap(parent.Civilization));
            int crew = CampCrewCount(camp);
            for (int i = _figures.Count - 1; i >= 0 && crew > cap; i--)
                if (_figures[i].Camp == camp.Id) { _figures[i].Camp = -1; crew--; }
            if (crew < cap)
            {
                for (int i = 0; i < _figures.Count && crew < cap; i++)
                {
                    FigureState f = _figures[i];
                    if (f.SettlementId != parent.Id || f.Camp >= 0 || f.Age is < 14 or >= 45 || f.WoundSeasons > 0 || f.IllnessSeasons > 0) continue;
                    if (f.Id == parent.FireKeeperId) continue;
                    f.Camp = camp.Id;
                    crew++;
                }
            }
            bool exhausted = _ore[camp.Tile] <= Fix64.Zero;
            if (exhausted || crew < _bronzeConstants.CampMinCrew || Population(parent.Id) == 0 || !CivInBronze(parent.Civilization))
            {
                foreach (FigureState f in _figures) if (f.Camp == camp.Id) f.Camp = -1;
                _camps.RemoveAt(c);
            }
        }
        _ = season;
    }

    /// <summary>The camps' season: build, then dig and haul home what the parent wants; returns the crews' work.</summary>
    private Fix64 WorkCamps(SettlementState s, MetalPlan plan, Fix64[] toolWork, int season)
    {
        MetalState m = s.Metal!;
        int civ = s.Civilization;
        Fix64 total = Fix64.Zero;
        foreach (MiningCampState camp in _camps)
        {
            if (camp.Parent != s.Id) continue;
            MetalGood good = OreGood(_world.Deposit(camp.Tile).Kind)!.Value;
            Fix64 want = plan.Want[MetalSlot(good)];
            Fix64 net = want * 2 - m.Goods[(int)good];
            Fix64 haulDivisor = Done(civ, "TRN-05") ? Fix64.FromInt(5) : Fix64.One;
            Fix64 haul = HaulPerLitreTile * camp.Distance / haulDivisor;
            Fix64 idle = Fix64.Zero;
            int crewCount = 0;
            for (int i = 0; i < _figures.Count; i++)
            {
                FigureState f = _figures[i];
                if (f.Camp != camp.Id || f.SettlementId != s.Id) continue;
                Fix64 available = WorkCapacity(f) - f.CareWorkThisSeason - f.FireWorkThisSeason;
                if (available <= Fix64.Zero) continue;
                Fix64 used = Fix64.Zero;
                if (!camp.Built)
                {
                    Fix64 part = Fix64.Min(available, Fix64.FromInt(2) - camp.Progress);
                    camp.Progress += part;
                    used += part;
                    if (camp.Progress >= Fix64.FromInt(2)) camp.Built = true;
                }
                else if (!camp.MineHead && Done(civ, "MIN-04") && TakeWood(s, camp.Tile, Fix64.FromInt(6), commit: false) >= Fix64.FromInt(6))
                {
                    Fix64 part = Fix64.Min(available, Fix64.FromInt(4) - camp.HeadProgress);
                    camp.HeadProgress += part;
                    used += part;
                    if (camp.HeadProgress >= Fix64.FromInt(4)) { camp.MineHead = true; TakeWood(s, camp.Tile, Fix64.FromInt(6), commit: true); }
                }
                Fix64 left = available - used;
                if (camp.Built && left > Fix64.Zero && net > Fix64.Zero && _ore[camp.Tile] > Fix64.Zero)
                {
                    Fix64 rate = MineRate(civ, camp.Tile, camp.MineHead, out _);
                    Fix64 effective = rate * SkillMultiplier(f, SkillFamily.Gather) * KitMod(f, "mine");
                    if (effective > Fix64.Zero)
                    {
                        // Each litre costs its digging plus its haul home (0.1 ws per L per 4 walking tiles, ÷5 by cart).
                        Fix64 perLitre = Fix64.One / effective + haul;
                        Fix64 litres = Fix64.Min(net, left / perLitre);
                        Fix64 dug = Dig(s, camp.Tile, effective, litres / effective, litres, camp.MineHead);
                        Fix64 spent = dug * perLitre;
                        used += spent;
                        net -= dug;
                        PracticeFigure(f, SkillFamily.Gather, spent);
                    }
                }
                crewCount++;
                idle += Fix64.Max(Fix64.Zero, available - used);
                if (used <= Fix64.Zero) continue;
                Assign(f, EconomyJob.Gather, used);
                if (f.Task == 0) f.Task = MetalTask.Mine;
                toolWork[i] += used;
                total += used;
            }
            // The crew follows the need: one more miner while ore falls short, one fewer while a miner's work goes unused.
            if (camp.Built && net > Fix64.FromRatio(1, 10)) camp.Target = Math.Min(CampCrewCap(civ), Math.Max(camp.Target, crewCount) + 1);
            else if (idle >= Fix64.One) camp.Target = Math.Max(_bronzeConstants.CampMinCrew, Math.Min(camp.Target, crewCount) - 1);
        }
        _ = season;
        return total;
    }

    /// <summary>
    /// The planner opens a camp (docs/MVP/23 §7.2) when it wants an ore no deposit in range supplies: on a workable deposit
    /// of that ore on its own land within mining reach, nearest first.
    /// </summary>
    private void OpenCampsIfNeeded(SettlementState s, MetalPlan plan, int season)
    {
        MetalState m = s.Metal!;
        int civ = s.Civilization;
        for (int g = 0; g < (int)MetalGood.Count; g++)
        {
            MetalGood good = (MetalGood)g;
            if (good is not (MetalGood.CopperOre or MetalGood.ArsenicalOre or MetalGood.TinOre)) continue;
            bool tierNeeds = plan.Tier switch
            {
                8 => good == MetalGood.CopperOre,
                9 => good == MetalGood.ArsenicalOre,
                10 => good is MetalGood.CopperOre or MetalGood.TinOre,
                _ => false,
            };
            if (plan.Want[MetalSlot(good)] <= m.Goods[g] && !(tierNeeds && m.Goods[g] < Fix64.FromInt(2))) continue;
            if (BestRangeDeposit(s, good, out _) >= 0) continue;
            bool have = false;
            foreach (MiningCampState camp in _camps)
                if (camp.Parent == s.Id && OreGood(_world.Deposit(camp.Tile).Kind) == good) { have = true; break; }
            if (have) continue;
            // A sister within the sharing reach that digs this ore already supplies it (docs/MVP/22 §5.7).
            int share = ShareReach(civ);
            if (share >= 0)
                foreach (MiningCampState camp in _camps)
                {
                    SettlementState p = _settlements[camp.Parent];
                    if (p != s && p.Civilization == civ && !IsFreeFolk(p) && OreGood(_world.Deposit(camp.Tile).Kind) == good &&
                        HexGrid.Distance(p.SiteTile, s.SiteTile, _world.Width) <= share) { have = true; break; }
                }
            if (have) continue;
            int reach = MiningReach(civ, s.SiteTile);
            foreach (int tile in ReachTiles(s.SiteTile, reach))
            {
                if (OreGood(_world.Deposit(tile).Kind) != good || !IsOwnTile(civ, tile)) continue;
                if (TryOpenCamp(s, tile, HexGrid.Distance(s.SiteTile, tile, _world.Width), decreed: false, season) == MiningCampResult.Opened) break;
            }
        }
    }

    private int MiningReach(int civ, int site)
    {
        int reach = Done(civ, "TRN-05") ? _bronzeConstants.MineReachCarts : _bronzeConstants.MineReach;
        WorldEconomyTile t = _world.Tile(site);
        if (Done(civ, "SEA-05") && (t.River || t.Coast || t.Lake)) reach = Math.Max(reach, _bronzeConstants.MineReachWater);
        return reach;
    }

    private MiningCampResult TryOpenCamp(SettlementState parent, int tile, int distance, bool decreed, int season)
    {
        int civ = parent.Civilization;
        WorldDeposit d = _world.Deposit(tile);
        if (OreGood(d.Kind) is null || _ore[tile] <= Fix64.Zero) return MiningCampResult.NoDeposit;
        if (!OreUnlocked(civ, d.Kind)) return MiningCampResult.NotUnlocked;
        if (d.Depth >= 2 && !Done(civ, "MIN-04")) return MiningCampResult.NeedsShaftMining;
        if (IsRivalTile(civ, tile)) return MiningCampResult.RivalOwned;
        foreach (MiningCampState other in _camps) if (other.Tile == tile) return MiningCampResult.AlreadyOpen;
        foreach (SettlementState s in _settlements) if (s.SiteTile == tile) return MiningCampResult.NoDeposit;
        if (TakeWood(parent, tile, Fix64.FromInt(2), commit: false) < Fix64.FromInt(2)) return MiningCampResult.NoCrew;
        int free = 0;
        foreach (FigureState f in _figures)
            if (f.SettlementId == parent.Id && f.Camp < 0 && f.Age is >= 14 and < 45 && f.WoundSeasons == 0 && f.IllnessSeasons == 0) free++;
        // A camp takes its crew from the parent; leave at least half the parent's working adults at home.
        if (free < _bronzeConstants.CampMinCrew * 2) return MiningCampResult.NoCrew;
        TakeWood(parent, tile, Fix64.FromInt(2), commit: true);
        int cap = Math.Min(CampCrewCap(civ), free / 2);
        var camp = new MiningCampState
        {
            Id = _nextCampId++, Parent = parent.Id, Tile = tile, Opened = season, Distance = distance, Decreed = decreed, Target = cap,
        };
        _camps.Add(camp);
        int crew = 0;
        foreach (FigureState f in _figures)
        {
            if (crew >= cap) break;
            if (f.SettlementId != parent.Id || f.Camp >= 0 || f.Age is < 14 or >= 45 || f.WoundSeasons > 0 || f.IllnessSeasons > 0) continue;
            if (f.Id == parent.FireKeeperId) continue;
            f.Camp = camp.Id;
            crew++;
        }
        EnsureMetal(parent);
        return MiningCampResult.Opened;
    }

    /// <summary>
    /// Opens a mining camp on a deposit for the <c>Open a mine</c> Decree (docs/MVP/22 §4.3; WP8 executes the Decree): the
    /// civilization's settlement nearest the tile (within mining reach: 8 tiles, 12 with TRN-05, 16 by water with SEA-05)
    /// sends 4 miners, 6 with MIN-04. Unlike the planner, the Decree may open on unclaimed land, never on a rival's.
    /// <paramref name="walkingTiles"/> overrides the hex distance when the caller has measured the walk.
    /// </summary>
    public MiningCampResult OpenMiningCamp(int civ, int tile, int walkingTiles = -1)
    {
        if (!_bronzeActive || !CivInBronze(civ)) return MiningCampResult.NotBronze;
        if ((uint)tile >= (uint)_world.TileCount) return MiningCampResult.NoDeposit;
        SettlementState? parent = null;
        int best = int.MaxValue;
        foreach (SettlementState s in _settlements)
        {
            if (s.Civilization != civ || Population(s.Id) == 0 || IsFreeFolk(s)) continue;
            int d = HexGrid.Distance(s.SiteTile, tile, _world.Width);
            if (d < best) { best = d; parent = s; }
        }
        if (parent is null) return MiningCampResult.NoParent;
        int distance = walkingTiles >= 0 ? walkingTiles : best;
        if (distance <= Radius) return MiningCampResult.InRange;
        if (distance > MiningReach(civ, parent.SiteTile)) return MiningCampResult.OutOfReach;
        return TryOpenCamp(parent, tile, distance, decreed: true, checked((int)_seasonsCompleted));
    }

    /// <summary>
    /// A stand-in for the Strategist's <c>Open a mine</c> Decree (WP8/WP9 own the real one), used by the measurement
    /// harness only: for each of copper ore, arsenical copper and tin that the civilization can dig and has no camp on, it
    /// opens a camp on the nearest workable deposit within mining reach of one of its settlements. Returns camps opened.
    /// </summary>
    internal int EmulateMineDecrees(int civ)
    {
        if (!_bronzeActive || !CivInBronze(civ)) return 0;
        int opened = 0;
        foreach (DepositKind kind in new[] { DepositKind.CopperOre, DepositKind.ArsenicalCopper, DepositKind.Tin })
        {
            if (!OreUnlocked(civ, kind)) continue;
            bool have = false;
            foreach (MiningCampState camp in _camps)
                if (_settlements[camp.Parent].Civilization == civ && _world.Deposit(camp.Tile).Kind == kind) { have = true; break; }
            if (have) continue;
            int best = -1, bestDistance = int.MaxValue;
            foreach (SettlementState s in _settlements)
            {
                if (s.Civilization != civ || Population(s.Id) == 0) continue;
                bool inRange = false;
                foreach (int tile in RangeTiles(s.SiteTile))
                    if (_world.Deposit(tile).Kind == kind && DepositWorkable(civ, tile)) { inRange = true; break; }
                if (inRange) { best = -1; bestDistance = -1; break; }
                foreach (int tile in ReachTiles(s.SiteTile, MiningReach(civ, s.SiteTile)))
                {
                    if (_world.Deposit(tile).Kind != kind || !DepositWorkable(civ, tile) || IsRivalTile(civ, tile)) continue;
                    int d = HexGrid.Distance(s.SiteTile, tile, _world.Width);
                    if (d <= Radius || d >= bestDistance) continue;
                    best = tile; bestDistance = d;
                }
            }
            if (best >= 0 && OpenMiningCamp(civ, best) == MiningCampResult.Opened) opened++;
        }
        return opened;
    }

    /// <summary>
    /// Timber for a mine head or camp: the settlement's cut wood first, then standing wood cut on the forest tiles nearest
    /// <paramref name="center"/> within 3 (the builders fell it themselves). With <paramref name="commit"/> false only counts.
    /// </summary>
    private Fix64 TakeWood(SettlementState s, int center, Fix64 amount, bool commit)
    {
        Fix64 got = Fix64.Min(amount, s.Stores[(int)Good.Wood]);
        if (commit) s.Stores[(int)Good.Wood] -= got;
        foreach (int tile in ReachTiles(center, Radius))
        {
            if (got >= amount) break;
            if (!IsForestTile(tile)) continue;
            Fix64 take = Fix64.Min(amount - got, _woodStock[tile]);
            if (commit) _woodStock[tile] -= take;
            got += take;
        }
        return got;
    }

    // ---- chop with metal kits (docs/MVP/23 §6.2: rungs 5 and 6) ------------------------------------------

    private bool AnyMetalChopper(SettlementState s, Fix64[,] actionWork)
    {
        if (s.Metal is null) return false;
        foreach (int i in _actionWorkTouchedRows)
            if (actionWork[i, (int)SkillFamily.Chop] > Fix64.Zero && KitChopRung(_figures[i]) >= 0) return true;
        return false;
    }

    /// <summary>
    /// Chop for a settlement where some choppers carry metal kits: each kit holder works at its own rung (copper axe 4.5,
    /// bronze axe 6.0) and kit quality; the rest share the settlement's stone tools as before.
    /// </summary>
    /// <summary>
    /// From KEY-3 the settlement chops to a stock, not without end (a WP6 decision, docs/MVP/07d "WP6 live results"): a year
    /// of hearth fuel and building wood (24 L) plus this season's charcoal wood. The E0–E2 economy, which never needed
    /// standing forest to last, chops whatever its woodcutters can; left so, a town hoards hundreds of litres and
    /// strips its range whether or not it smelts. Returns the most to cut this season (at least a hundredth of a litre).
    /// </summary>
    private Fix64 WoodChopLimit(SettlementState s, MetalPlan plan)
    {
        Fix64 charcoal = Fix64.Zero;
        for (int r = 0; r < _compiledRecipes.Length; r++)
            if (_compiledRecipes[r].IsCharcoal) charcoal += plan.Batches[r] * _compiledRecipes[r].InputQuantity[0];
        Fix64 target = Fix64.FromInt(24) + charcoal;
        return Fix64.Max(Fix64.FromRatio(1, 100), target - s.Stores[(int)Good.Wood]);
    }

    /// <summary>
    /// Bronze Age woodcutting (WP6): fells <paramref name="amount"/> litres across the range's wooded tiles in proportion to
    /// their standing stock, not tile by tile in id order as E0–E2 do, so regrowth (×2 coppiced) works on every stand.
    /// </summary>
    private Fix64 HarvestWoodSpread(SettlementState s, Fix64 amount)
    {
        if (amount <= Fix64.Zero) return Fix64.Zero;
        // Timber is felled in the forest stands (tiles of 15 L or more at the start); scrub and grass only where there are none.
        Fix64 standing = Fix64.Zero;
        bool forest = false;
        foreach (int tile in RangeTiles(s.SiteTile))
            if (IsForestTile(tile)) { standing += _woodStock[tile]; forest = true; }
        if (!forest) foreach (int tile in RangeTiles(s.SiteTile)) standing += _woodStock[tile];
        if (standing <= Fix64.Zero) return Fix64.Zero;
        Fix64 want = Fix64.Min(amount, standing), share = want / standing, taken = Fix64.Zero;
        foreach (int tile in RangeTiles(s.SiteTile))
        {
            if (forest && !IsForestTile(tile)) continue;
            Fix64 take = Fix64.Min(_woodStock[tile], _woodStock[tile] * share);
            _woodStock[tile] -= take;
            taken += take;
        }
        s.Stores[(int)Good.Wood] += taken;
        return taken;
    }

    private void ChopWithKits(SettlementState s, Fix64[,] actionWork, Fix64 chopWork, Fix64 limit)
    {
        int civ = s.Civilization;
        int column = (int)SkillFamily.Chop;
        Fix64 kitWork = Fix64.Zero;
        foreach (int i in _actionWorkTouchedRows)
            if (KitChopRung(_figures[i]) >= 0) kitWork += actionWork[i, column];
        Fix64 otherWork = Fix64.Max(Fix64.Zero, chopWork - kitWork);
        Fix64 appliedOther = LimitActionWorkByTool(s, "chop", otherWork);
        if (otherWork > Fix64.Zero && appliedOther < otherWork)
        {
            Fix64 scale = appliedOther / otherWork;
            foreach (int i in _actionWorkTouchedRows)
                if (KitChopRung(_figures[i]) < 0) actionWork[i, column] *= scale;
        }
        Fix64 woodYield = Modifier(civ, "wood_yield");
        Fix64 e2Rate = ActionRate("chop", Fix64.FromInt(2), s, "chop", toolWork: appliedOther) * woodYield;
        Fix64 wood = Fix64.Zero;
        foreach (int i in _actionWorkTouchedRows)
        {
            FigureState f = _figures[i];
            Fix64 work = actionWork[i, column];
            if (work <= Fix64.Zero) continue;
            Fix64 rate = KitChopRung(f) >= 0 ? KitChopRate(f) * woodYield : e2Rate;
            wood += work * SkillMultiplier(f, SkillFamily.Chop) * rate;
        }
        AddMetalStat(civ, StatChopWood, HarvestWoodSpread(s, Fix64.Min(wood, limit)));
        WearActionTool(s, "chop", appliedOther, e2Rate);
        PracticeSkill(actionWork, SkillFamily.Chop);
    }

    /// <summary>Farm work by figures whose kit lightens harvest labor (×0.8 for bronze) counts for more (docs/MVP/23 §4.1).</summary>
    private Fix64 KitFarmBonus(SettlementState s, Fix64[,] actionWork)
    {
        if (s.Metal is null) return Fix64.Zero;
        Fix64 bonus = Fix64.Zero;
        int column = (int)SkillFamily.Farm;
        foreach (int i in _actionWorkTouchedRows)
        {
            FigureState f = _figures[i];
            Fix64 work = actionWork[i, column];
            if (work <= Fix64.Zero || f.Kit == 0) continue;
            Fix64 mod = KitMod(f, "harvest_labor");
            if (mod > Fix64.Zero && mod < Fix64.One) bonus += work * SkillMultiplier(f, SkillFamily.Farm) * (Fix64.One / mod - Fix64.One);
        }
        return bonus;
    }

    /// <summary>Building work by a kit holder on a timber structure goes further (×0.95 copper, ×0.85 bronze labor).</summary>
    private Fix64 KitBuildBonus(SettlementState s, FigureState f, Fix64 work)
    {
        if (s.Metal is null || f.Kit == 0 || !IsTimberStructure(s.BuildTarget)) return Fix64.Zero;
        Fix64 mod = KitMod(f, "timber_build_labor");
        return mod > Fix64.Zero && mod < Fix64.One ? work * (Fix64.One / mod - Fix64.One) : Fix64.Zero;
    }

    private static bool IsTimberStructure(Structure structure) => structure is Structure.LeanTo or Structure.PitHouse or
        Structure.WattleHouse or Structure.Longhouse or Structure.DryingRack or Structure.ThornBarrier or Structure.Lookout or
        Structure.Weir or Structure.Pasture;

    // ---- forests (docs/MVP/22 §5.5, 23 §7.4) -----------------------------------------------------------

    /// <summary>Coppicing (WOD-04) doubles regrowth on tiles within a settlement's radius; derived each season from settlements.</summary>
    private void RefreshCoppicing()
    {
        Array.Clear(_coppiced);
        foreach (SettlementState s in _settlements)
        {
            if (!CivInBronze(s.Civilization) || !Done(s.Civilization, "WOD-04") || Population(s.Id) == 0) continue;
            foreach (int tile in RangeTiles(s.SiteTile)) _coppiced[tile] = true;
        }
    }

    private Fix64 WoodRegrowth(int tile, Fix64 seasonal)
    {
        if (!_bronzeActive || !_coppiced[tile]) return seasonal;
        // The first settlement in id order whose range covers the tile applies its civilization's wood_regrowth modifier.
        foreach (SettlementState s in _settlements)
        {
            if (!CivInBronze(s.Civilization) || !Done(s.Civilization, "WOD-04") || HexGrid.Distance(s.SiteTile, tile, _world.Width) > Radius) continue;
            return seasonal * Modifier(s.Civilization, "wood_regrowth");
        }
        return seasonal;
    }

    /// <summary>Cleared below 5 L, grassland after 4 seasons below 1 L, forest again from 15 L (docs/MVP/23 §7.4).</summary>
    private void UpdateForestState(int tile)
    {
        if (!IsForestTile(tile)) return;
        Fix64 stock = _woodStock[tile];
        if (stock >= _bronzeConstants.RestoreAt) { _forestState[tile] = 0; _forestLowSeasons[tile] = 0; return; }
        if (stock < _bronzeConstants.ClearedBelow && _forestState[tile] == 0) _forestState[tile] = 1;
        if (stock < _bronzeConstants.GrassBelow)
        {
            if (_forestLowSeasons[tile] < byte.MaxValue) _forestLowSeasons[tile]++;
            if (_forestLowSeasons[tile] >= _bronzeConstants.GrassSeasons) _forestState[tile] = 2;
        }
        else _forestLowSeasons[tile] = 0;
    }

    /// <summary>A cleared forest tile forages as open woodland, then as grassland.</summary>
    private Fix64 ClearedForageFactor(int tile)
    {
        if (_forestState.Length == 0 || _forestState[tile] == 0) return Fix64.One;
        Biome biome = (Biome)_world.Tile(tile).BiomeId;
        Fix64 own = WorldEconomyInput.ForageMultiplierOf(biome);
        if (own <= Fix64.Zero) return Fix64.One;
        Biome now = _forestState[tile] == 1 ? Biome.OpenWoodland : Biome.GrasslandSteppe;
        return WorldEconomyInput.ForageMultiplierOf(now) / own;
    }

    // ---- preserved foods (cheese, salted) count as food -----------------------------------------------

    private Fix64 MetalFoodStores(SettlementState s)
    {
        if (s.Metal is not { } m) return Fix64.Zero;
        Fix64 food = m.Goods[(int)MetalGood.Cheese] + m.Goods[(int)MetalGood.Salted] + m.Goods[(int)MetalGood.Milk];
        return food <= Fix64.Zero ? Fix64.Zero : food * FoodValue(Good.DriedFood, s);
    }

    private Fix64 ConsumeMetalFood(SettlementState s, Fix64 amount)
    {
        if (s.Metal is not { } m || amount <= Fix64.Zero) return amount;
        Fix64 value = FoodValue(Good.DriedFood, s);
        if (value <= Fix64.Zero) return amount;
        foreach (MetalGood good in new[] { MetalGood.Milk, MetalGood.Salted, MetalGood.Cheese })
        {
            Fix64 take = Fix64.Min(amount / value, m.Goods[(int)good]);
            m.Goods[(int)good] -= take;
            amount -= take * value;
            if (amount <= Fix64.Zero) break;
        }
        return amount;
    }

    private void SpoilMetalFood(SettlementState s, Fix64 storageFactor)
    {
        if (s.Metal is not { } m) return;
        foreach (MetalGood good in new[] { MetalGood.Milk, MetalGood.Cheese, MetalGood.Salted })
        {
            int t = _content.TradeGoodIndex(MetalGoodId(good));
            Fix64 spoilage = Fix64.Zero;
            for (int i = 0; i < _content.Goods.Length; i++)
                if (_content.Goods[i].Id == MetalGoodId(good)) { spoilage = _content.Goods[i].Spoilage; break; }
            _ = t;
            Fix64 factor = good == MetalGood.Milk ? Fix64.One : storageFactor;
            m.Goods[(int)good] *= Fix64.One - Fix64.Clamp(spoilage * factor, Fix64.Zero, Fix64.One);
        }
    }

    /// <summary>A daughter band takes two fifths of the parent's metal stores (cast kits stay; figures carry their own).</summary>
    private void SplitMetal(SettlementState parent, SettlementState daughter)
    {
        if (parent.Metal is not { } pm) return;
        MetalState dm = EnsureMetal(daughter);
        for (int g = 0; g < (int)MetalGood.Count; g++)
        {
            Fix64 move = pm.Goods[g] * Fix64.FromRatio(2, 5);
            if (move <= Fix64.Zero) continue;
            pm.Goods[g] -= move;
            dm.Goods[g] += move;
        }
    }
}
