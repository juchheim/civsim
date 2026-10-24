using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>Which strategic metal a settlement has access to (docs/MVP/22 §5.7, 23 §7.3).</summary>
public enum MetalAccessKind : byte { Copper, Arsenic, Tin }

/// <summary>Why the Decree or the planner could not open a mining camp (docs/MVP/22 §4.3, 23 §7.2).</summary>
public enum MiningCampResult : byte
{
    Opened, NotBronze, NoDeposit, NotUnlocked, NeedsShaftMining, RivalOwned, OutOfReach, NoParent, NoCrew, AlreadyOpen, InRange
}

/// <summary>Who owns a tile, for the planner's rule that it opens camps only on own land (docs/MVP/23 §7.2). Politics implements it.</summary>
public interface ITileOwnership
{
    /// <summary>The owning civilization of a tile, or -1.</summary>
    int OwnerOf(int tile);
}

/// <summary>A mining camp as other systems see it (docs/MVP/22 §5.2): a satellite of its parent, a claim source and a raid target.</summary>
public readonly record struct MiningCampView(
    int Id, int Civilization, int ParentSettlementIndex, int Tile, int Crew, bool Built, bool MineHead, DepositKind Deposit,
    Fix64 OreLeft, int ClaimRadius, Fix64 ClaimEmission);

/// <summary>A settlement's metal economy at a glance, for the harness, the view and the AI (read-only).</summary>
public readonly record struct MetalReadout(
    int CastTier, bool CopperAccess, bool ArsenicAccess, bool TinAccess, MethodFallbackReason Reason,
    int Forges, int Smelters, int MineHeads, int Adults, int Kits8, int Kits9, int Kits10,
    Fix64 CopperOre, Fix64 TinOre, Fix64 ArsenicalOre, Fix64 Charcoal, Fix64 Copper, Fix64 Tin, Fix64 Bronze, Fix64 ArsenicalBronze,
    Fix64 StandingWoodInRange, Fix64 StandingWoodAtStart, int ClampTile);

/// <summary>Cumulative metal-economy totals of one civilization since its Bronze Age began (docs/MVP/22 §15 T17).</summary>
public readonly record struct MetalStatistics(
    Fix64 Labor, Fix64 MetalLabor, Fix64 PeopleSeasons, Fix64 Bronze, Fix64 ArsenicalBronze, Fix64 Copper, Fix64 Tin,
    Fix64 Charcoal, Fix64 CharcoalWood, Fix64 FireSettingWood, Fix64 OreMined, Fix64 Kits8, Fix64 Kits9, Fix64 Kits10,
    Fix64 ArmsCast, Fix64 BronzeArms, Fix64 RemeltCopper, Fix64 ChopWood, Fix64 OtherWood = default);

/// <summary>
/// The Bronze Age economy's state (docs/MVP/22 §5, 23 §3–§8): deposits as ore stocks, mining camps, forests that recede and
/// regrow, per-settlement metal stores and structures, per-figure tool kits. Everything here is dormant, unhashed and
/// unchanged until the first civilization completes KEY-3, so E0–E2 checksums are those of the economy without it.
/// </summary>
public sealed partial class LivingEconomyModel
{
    private enum MetalGood : byte
    {
        CopperOre, ArsenicalOre, TinOre, IronOre,
        Charcoal, Copper, ArsenicalBronze, Tin, Bronze, Iron, Gold,
        Block, Ashlar, Salt, Lime, Ochre, Pigment, Wool, Yarn, Cloth, Milk, Oil,
        Kit8, Kit9, Kit10, Arms1, Arms2A, Arms2T, Chariot, Wheel, Cart,
        Cheese, Salted, Dye, Soap, WoolGarment,
        Turned, Faience, Glass, DyedCloth, Silk, FineBronze,
        Count
    }

    private static string MetalGoodId(MetalGood good) => good switch
    {
        MetalGood.CopperOre => "G-CUORE", MetalGood.ArsenicalOre => "G-ASORE", MetalGood.TinOre => "G-TINORE",
        MetalGood.IronOre => "G-IRONORE", MetalGood.Charcoal => "G-CHAR", MetalGood.Copper => "G-COPPER",
        MetalGood.ArsenicalBronze => "G-ABRONZE", MetalGood.Tin => "G-TIN", MetalGood.Bronze => "G-BRONZE",
        MetalGood.Iron => "G-IRON", MetalGood.Gold => "G-GOLD", MetalGood.Block => "G-BLOCK", MetalGood.Ashlar => "G-ASHLAR",
        MetalGood.Salt => "G-SALT", MetalGood.Lime => "G-LIME", MetalGood.Ochre => "G-OCHRE", MetalGood.Pigment => "G-PIGMENT",
        MetalGood.Wool => "G-WOOL", MetalGood.Yarn => "G-YARN", MetalGood.Cloth => "G-CLOTH", MetalGood.Milk => "G-MILK",
        MetalGood.Oil => "G-OIL", MetalGood.Kit8 => "G-TOOL8", MetalGood.Kit9 => "G-TOOL9", MetalGood.Kit10 => "G-TOOL10",
        MetalGood.Arms1 => "G-ARMS1", MetalGood.Arms2A => "G-ARMS2A", MetalGood.Arms2T => "G-ARMS2T",
        MetalGood.Chariot => "G-CHARIOT", MetalGood.Wheel => "G-WHEEL", MetalGood.Cart => "G-CART",
        MetalGood.Cheese => "G-CHEESE", MetalGood.Salted => "G-SALTED", MetalGood.Dye => "G-DYE", MetalGood.Soap => "G-SOAP",
        MetalGood.WoolGarment => "G-WOOLGARMENT", MetalGood.Turned => "G-TURNED", MetalGood.Faience => "G-FAIENCE",
        MetalGood.Glass => "G-GLASS", MetalGood.DyedCloth => "G-DYEDCLOTH", MetalGood.Silk => "G-SILK",
        MetalGood.FineBronze => "G-FINEBRONZE",
        _ => "",
    };

    /// <summary>
    /// What a figure did for the metal and craft economy this season, beyond its displayed job; research reads it as the
    /// practice (docs/MVP/23 §10). Values are stable (snapshots and checksums store them).
    /// </summary>
    private static class MetalTask
    {
        public const byte None = 0, Mine = 1, Smelt = 2, SmeltArsenic = 3, Charcoal = 4, Craft = 5, Knap = 6, Clay = 7,
            Fiber = 8, Preserve = 9, Build = 10, Ritual = 11, Records = 12, Count = 13;
    }

    private static readonly string[] TaskPracticeIds =
        { "", "mine", "smelt", "smelt", "burn_charcoal", "craft", "knap", "clay", "fiber", "preserve", "build", "ritual", "scribe" };

    /// <summary>The practice id a figure's metal task generates, or null for none (research maps it to a practice index).</summary>
    public static string? TaskPractice(byte task) => task is > 0 and < MetalTask.Count ? TaskPracticeIds[task] : null;

    private static byte TaskOfPractice(string practice) => practice switch
    {
        "mine" => MetalTask.Mine, "smelt" => MetalTask.Smelt, "burn_charcoal" => MetalTask.Charcoal, "knap" => MetalTask.Knap,
        "clay" => MetalTask.Clay, "fiber" => MetalTask.Fiber, "preserve" => MetalTask.Preserve, "build" => MetalTask.Build,
        _ => MetalTask.Craft,
    };

    private enum MetalBuild : byte { None, Forge, Smelter, Kiln, MineHead }

    private const int StatLabor = 0, StatMetalLabor = 1, StatPeopleSeasons = 2, StatBronze = 3, StatArsenicalBronze = 4,
        StatCopper = 5, StatTin = 6, StatCharcoal = 7, StatCharcoalWood = 8, StatFireSettingWood = 9, StatOreMined = 10,
        StatKits8 = 11, StatKits9 = 12, StatKits10 = 13, StatArmsCast = 14, StatBronzeArms = 15, StatRemeltCopper = 16,
        StatChopWood = 17, StatOtherWood = 18, StatCount = 19;

    // Access bits (stores or a sister settlement within the sharing reach hold the metal) and source bits (a workable deposit
    // in range or at an own camp, so the planner may count on mining it).
    private const byte AccessCopper = 1, AccessArsenic = 2, AccessTin = 4, SourceCopper = 8, SourceArsenic = 16, SourceTin = 32;

    /// <summary>Strategic goods that sister settlements share (docs/MVP/22 §5.7), in a fixed order.</summary>
    private static readonly MetalGood[] SharedGoods =
    {
        MetalGood.CopperOre, MetalGood.ArsenicalOre, MetalGood.TinOre, MetalGood.Copper, MetalGood.ArsenicalBronze,
        MetalGood.Tin, MetalGood.Bronze, MetalGood.Kit8, MetalGood.Kit9, MetalGood.Kit10,
    };

    private sealed class MetalState
    {
        public readonly Fix64[] Goods = new Fix64[(int)MetalGood.Count];
        /// <summary>Last season's gross want of each shared good, what sister settlements send toward.</summary>
        public readonly Fix64[] ShareWant = new Fix64[SharedGoods.Length];
        public int Forges;
        public int Smelters;
        /// <summary>Deposit tiles in range where this settlement has built a mine head, in the order built.</summary>
        public readonly List<int> MineHeads = new();
        public MetalBuild BuildKind;
        public int BuildTile = -1;
        public Fix64 BuildProgress;
        /// <summary>Clay and stone gathered for the structure under construction, kept at the site (out of the stores).</summary>
        public Fix64 SiteClay, SiteStone;
        public byte Access;
        public MethodFallbackReason Reason;
        /// <summary>The forest tile whose standing stock the charcoal burners cut last (a view marker), or -1.</summary>
        public int ClampTile = -1;
        /// <summary>Standing forest stock in range when the settlement's Bronze state began (T19's reference).</summary>
        public Fix64 ForestAtStart;
        /// <summary>Seasons in a row in which charcoal fell short of the plan for want of wood.</summary>
        public int FuelShortSeasons;
    }

    private sealed class MiningCampState
    {
        public int Id;
        public int Parent;
        public int Tile;
        public int Opened;
        public bool Built;
        public Fix64 Progress;
        public bool MineHead;
        public Fix64 HeadProgress;
        public int Distance;
        public bool Decreed;
        /// <summary>The crew the camp keeps: starts at the full crew and follows the parent's ore need (never below the minimum).</summary>
        public int Target;
    }

    private readonly struct BronzeConstants
    {
        public BronzeConstants(EconomyContent c)
        {
            Fix64 K(string id, string fallback) => c.Constant(id, Fix64.Parse(fallback));
            CampCrew = K("MiningCampCrew", "4").FloorToInt();
            CampCrewShaft = K("MiningCampCrewShaft", "6").FloorToInt();
            CampMinCrew = K("MiningCampMinCrew", "2").FloorToInt();
            CampBand = K("MiningCampBand", "6");
            CampEmit = K("MiningCampEmit", "0.75");
            CampRadius = K("MiningCampRadius", "1").FloorToInt();
            HaulWaterDiv = K("HaulWaterDiv", "3");
            AccessMin = K("MetalAccessMin", "0.05");
            ShareReach = K("AccessShareReach", "15").FloorToInt();
            ShareReachGov04 = K("AccessShareReachGOV04", "20").FloorToInt();
            ReplaceQuality = K("KitReplaceQuality", "0.7");
            BreakQuality = K("KitBreakQuality", "0.5");
            RemeltShare = K("UpgradeRemeltShare", "0.5");
            PushCap = K("MetalPushLaborCap", "0.10");
            SpareArms = K("WarriorSpareKits", "2").FloorToInt();
            FireSettingWood = K("FireSettingWoodPerL", "0.5");
            ClearedBelow = K("ForestClearedBelow", "5");
            GrassBelow = K("ForestGrassBelow", "1");
            GrassSeasons = K("ForestGrassSeasons", "4").FloorToInt();
            RestoreAt = K("ForestRestoreAt", "15");
            CharcoalReach = K("CharcoalReach", "8").FloorToInt();
            CharcoalReachCarts = K("CharcoalReachCarts", "12").FloorToInt();
            MineReach = K("MineReach", "8").FloorToInt();
            MineReachCarts = K("MineReachCarts", "12").FloorToInt();
            MineReachWater = K("MineReachWater", "16").FloorToInt();
        }

        public readonly int CampCrew, CampCrewShaft, CampMinCrew, CampRadius, ShareReach, ShareReachGov04, SpareArms, GrassSeasons;
        public readonly int CharcoalReach, CharcoalReachCarts, MineReach, MineReachCarts, MineReachWater;
        public readonly Fix64 CampBand, CampEmit, HaulWaterDiv, AccessMin, ReplaceQuality, BreakQuality, RemeltShare, PushCap;
        public readonly Fix64 FireSettingWood, ClearedBelow, GrassBelow, RestoreAt;
    }

    private static readonly ulong BronzeTag = 0x42524F4E5A452033UL; // "BRONZE 3"
    private static readonly Fix64 KitNewQuality = Fix64.FromRatio(12, 10);
    private static readonly Fix64 HaulPerLitreTile = Fix64.FromRatio(1, 40); // 0.1 ws per L per 4 tiles (docs/MVP/22 §5.2)
    private static readonly Fix64 WoodReserve = Fix64.FromInt(12);
    private static readonly Fix64 CharcoalWoodPerL = Fix64.FromInt(6);
    private static readonly Fix64 ExpectedSeasonWear = Fix64.FromRatio(7, 10);

    private BronzeConstants _bronzeConstants;
    private bool _bronzeActive;
    private Fix64[] _ore = Array.Empty<Fix64>();
    private byte[] _forestState = Array.Empty<byte>();
    private byte[] _forestLowSeasons = Array.Empty<byte>();
    private readonly List<MiningCampState> _camps = new();
    private int _nextCampId;
    private Fix64[] _metalStats = Array.Empty<Fix64>();
    private int _key3 = -1;

    // Derived, never hashed: recipe slots resolved against the store layout, and per-site tile lists for charcoal reach.
    private CompiledRecipe[] _compiledRecipes = Array.Empty<CompiledRecipe>();
    private int[]?[] _reachTiles = Array.Empty<int[]?>();
    private bool[] _coppiced = Array.Empty<bool>();

    /// <summary>Who owns tiles (politics); without it the planner treats tiles within four of its own settlements as its own.</summary>
    public ITileOwnership? TileOwnership { get; set; }

    /// <summary>True from the season the first civilization completes KEY-3: deposits, camps, forests and kits are live.</summary>
    public bool BronzeActive => _bronzeActive;

    private void InitializeBronze()
    {
        _bronzeConstants = new BronzeConstants(_content);
        _key3 = ProjectIndex("KEY-3");
        int tiles = _world.TileCount;
        _ore = new Fix64[tiles];
        _forestState = new byte[tiles];
        _forestLowSeasons = new byte[tiles];
        _coppiced = new bool[tiles];
        _reachTiles = new int[]?[tiles];
        for (int tile = 0; tile < tiles; tile++)
        {
            WorldDeposit d = _world.Deposit(tile);
            _ore[tile] = OreLitres(d);
        }
        _metalStats = new Fix64[_completedProjects.Length * StatCount];
        CompileRecipes();
        InitializeCivic();
        InitializeFood();
    }

    /// <summary>Litres of ore a deposit holds (docs/MVP/22 §5.1; tin by WP4's quantity tiers 1 = 100, 2 = 150, 3 = 600 L).</summary>
    private static Fix64 OreLitres(WorldDeposit d) => d.Kind switch
    {
        DepositKind.Tin => Fix64.FromInt(d.Tier switch { 3 => 600, 2 => 150, _ => 100 }),
        DepositKind.CopperOre => Fix64.FromInt(d.Depth >= 2 ? 900 : 300),
        DepositKind.ArsenicalCopper => Fix64.FromInt(800),
        DepositKind.Iron => Fix64.FromInt(1000),
        DepositKind.Gold or DepositKind.Silver => Fix64.FromInt(40),
        DepositKind.Salt => Fix64.FromInt(400),
        _ => Fix64.Zero,
    };

    private void RefreshBronzeActive()
    {
        _bronzeActive = false;
        if (_key3 < 0) return;
        for (int civ = 0; civ < _completedProjects.Length; civ++)
            if (_completedProjects[civ][_key3]) { _bronzeActive = true; return; }
    }

    private bool CivInBronze(int civ) => _key3 >= 0 && _completedProjects[civ][_key3];

    private void AddMetalStat(int civ, int stat, Fix64 amount)
    {
        if (amount == Fix64.Zero) return;
        _metalStats[civ * StatCount + stat] += amount;
    }

    // ---- public read-only surfaces ---------------------------------------------------------

    /// <summary>Ore left in a tile's deposit, in litres.</summary>
    public Fix64 OreRemaining(int tile) => _ore[tile];

    /// <summary>The view's forest band of a tile (docs/MVP/23 §7.4): 0 under 5 L, 1 for 5–14, 2 for 15–24, 3 from 25 L.</summary>
    public int ForestBand(int tile)
    {
        Fix64 stock = _woodStock[tile];
        return stock < Fix64.FromInt(5) ? 0 : stock < Fix64.FromInt(15) ? 1 : stock < Fix64.FromInt(25) ? 2 : 3;
    }

    /// <summary>A forest tile's state: 0 standing forest, 1 cleared woodland, 2 grassland (docs/MVP/23 §7.4).</summary>
    public int ForestState(int tile) => _forestState.Length == 0 ? 0 : _forestState[tile];

    /// <summary>Standing wood on a tile, in litres.</summary>
    public Fix64 StandingWood(int tile) => _woodStock[tile];

    public int MiningCampCount => _camps.Count;

    public MiningCampView MiningCamp(int index)
    {
        MiningCampState camp = _camps[index];
        SettlementState parent = _settlements[camp.Parent];
        int crew = CampCrewCount(camp);
        int radius = _bronzeConstants.CampRadius + (HasProject(parent.Civilization, "TER-06") ? 1 : 0);
        Fix64 emission = Fix64.Sqrt(_bronzeConstants.CampBand) * _bronzeConstants.CampEmit;
        return new MiningCampView(camp.Id, parent.Civilization, camp.Parent, camp.Tile, crew, camp.Built, camp.MineHead,
            _world.Deposit(camp.Tile).Kind, _ore[camp.Tile], radius, emission);
    }

    /// <summary>The tool kit a figure carries: tier 0 (none, stone tools of the settlement), 8, 9 or 10, and its quality in permille.</summary>
    public (int Tier, int QualityPermille) FigureKit(int index)
    {
        FigureState f = _figures[index];
        if (f.Kit == 0) return (0, 0);
        return (f.Kit, (KitQuality(f) * Fix64.FromInt(1000)).FloorToInt());
    }

    /// <summary>Whether any settlement of the civilization has the metal in hand (docs/MVP/23 §7.3), for research's Dormant state.</summary>
    public bool CivHasMetalAccess(int civ, MetalAccessKind kind)
    {
        byte bit = kind switch { MetalAccessKind.Copper => AccessCopper, MetalAccessKind.Arsenic => AccessArsenic, _ => AccessTin };
        foreach (SettlementState s in _settlements)
            if (s.Civilization == civ && s.Metal is { } m && (m.Access & bit) != 0 && !IsFreeFolk(s)) return true;
        return false;
    }

    public MetalStatistics MetalStats(int civ)
    {
        int o = civ * StatCount;
        Fix64 S(int k) => _metalStats.Length == 0 ? Fix64.Zero : _metalStats[o + k];
        return new MetalStatistics(S(StatLabor), S(StatMetalLabor), S(StatPeopleSeasons), S(StatBronze), S(StatArsenicalBronze),
            S(StatCopper), S(StatTin), S(StatCharcoal), S(StatCharcoalWood), S(StatFireSettingWood), S(StatOreMined),
            S(StatKits8), S(StatKits9), S(StatKits10), S(StatArmsCast), S(StatBronzeArms), S(StatRemeltCopper), S(StatChopWood), S(StatOtherWood));
    }

    public MetalReadout Metal(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        int adults = 0, k8 = 0, k9 = 0, k10 = 0;
        foreach (FigureState f in _figures)
        {
            if (f.SettlementId != s.Id || f.Age < 14) continue;
            adults++;
            if (f.Kit == 8) k8++; else if (f.Kit == 9) k9++; else if (f.Kit == 10) k10++;
        }
        if (s.Metal is not { } m)
            return new MetalReadout(0, false, false, false, MethodFallbackReason.None, 0, 0, 0, adults, k8, k9, k10,
                Fix64.Zero, Fix64.Zero, Fix64.Zero, Fix64.Zero, Fix64.Zero, Fix64.Zero, Fix64.Zero, Fix64.Zero,
                StandingWoodInRange(s.SiteTile), Fix64.Zero, -1);
        return new MetalReadout(BestCastTier(s), (m.Access & AccessCopper) != 0, (m.Access & AccessArsenic) != 0,
            (m.Access & AccessTin) != 0, m.Reason, m.Forges, m.Smelters, m.MineHeads.Count, adults, k8, k9, k10,
            m.Goods[(int)MetalGood.CopperOre], m.Goods[(int)MetalGood.TinOre], m.Goods[(int)MetalGood.ArsenicalOre],
            m.Goods[(int)MetalGood.Charcoal], m.Goods[(int)MetalGood.Copper], m.Goods[(int)MetalGood.Tin],
            m.Goods[(int)MetalGood.Bronze], m.Goods[(int)MetalGood.ArsenicalBronze], StandingWoodInRange(s.SiteTile),
            m.ForestAtStart, m.ClampTile);
    }

    /// <summary>Standing wood on the forest tiles of a radius-3 range (docs/MVP/22 §5.5).</summary>
    public Fix64 StandingWoodInRange(int siteTile)
    {
        Fix64 total = Fix64.Zero;
        foreach (int tile in RangeTiles(siteTile))
            if (IsForestTile(tile)) total += _woodStock[tile];
        return total;
    }

    private bool IsForestTile(int tile) => _world.Tile(tile).InitialWood >= Fix64.FromInt(15);

    /// <summary>
    /// Mean chop output per work-season of a settlement's bronze-kit holders (rung 6) and of its stone tools (the E2 rung
    /// with current tool quality), both before skill (docs/MVP/22 §16 B11). Zero where nobody holds a bronze kit.
    /// </summary>
    internal (Fix64 Bronze, Fix64 Stone) ChopRates(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        Fix64 sum = Fix64.Zero;
        int n = 0;
        foreach (FigureState f in _figures)
        {
            if (f.SettlementId != s.Id || f.Kit is not (9 or 10) || KitChopRung(f) != 6) continue;
            sum += KitChopRate(f);
            n++;
        }
        // The stone reference is the polished axe (rung 4) at the kits' mean quality, the E2 end of the ladder (docs/MVP/23 §6.3).
        Fix64 quality = Fix64.Zero;
        int q = 0;
        foreach (FigureState f in _figures)
            if (f.SettlementId == s.Id && f.Kit is 9 or 10) { quality += KitQuality(f); q++; }
        Fix64 stone = BaseRate("chop", Fix64.FromInt(2)) * MethodMult(s.Civilization, "chop", 4) * (q == 0 ? Fix64.One : quality / q);
        return (n == 0 ? Fix64.Zero : sum / n, stone);
    }

    /// <summary>Mining output per work-season at a deposit with a mine head for a bronze-kit holder, against surface digging by hand (B11).</summary>
    internal (Fix64 Best, Fix64 Surface) MineRates(int civ)
    {
        Fix64 surface = BaseRate("mine", Fix64.One);
        Fix64 shaft = MethodMult(civ, "mine", 1);
        Fix64 picks = _tierDefs[10]?.Mod("mine") ?? Fix64.One;
        return (surface * (shaft > Fix64.Zero ? shaft : Fix64.One) * picks, surface);
    }

    /// <summary>A one-line description of a settlement's metal economy, for harness traces only.</summary>
    internal string DescribeMetal(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        var text = new System.Text.StringBuilder();
        text.Append($"s{settlementIndex} civ{s.Civilization} pop {Population(s.Id)} kiln {s.Structures[(int)Structure.Kiln]} wood {s.Stores[(int)Good.Wood].FloorToInt()} stone {s.Stores[(int)Good.Stone].FloorToInt()} clay {s.Stores[(int)Good.Clay].FloorToInt()}");
        if (s.Metal is { } m)
        {
            text.Append($" tier {BestCastTier(s)} access {m.Access} forge {m.Forges} smelter {m.Smelters} build {m.BuildKind}:{m.BuildProgress.FloorToInt()} reason {m.Reason}");
            for (int g = 0; g < (int)MetalGood.Count; g++)
                if (m.Goods[g] > Fix64.Zero) text.Append($" {MetalGoodId((MetalGood)g)}={m.Goods[g].ToDoubleForDisplay():F2}");
        }
        int nearCu = int.MaxValue, nearAs = int.MaxValue, nearTin = int.MaxValue;
        for (int tile = 0; tile < _world.TileCount; tile++)
        {
            WorldDeposit d = _world.Deposit(tile);
            int dist = HexGrid.Distance(s.SiteTile, tile, _world.Width);
            if (d.Kind == DepositKind.CopperOre) nearCu = Math.Min(nearCu, dist);
            if (d.Kind == DepositKind.ArsenicalCopper) nearAs = Math.Min(nearAs, dist);
            if (d.Kind == DepositKind.Tin) nearTin = Math.Min(nearTin, dist);
        }
        text.Append($" | nearest copper {nearCu} arsenic {nearAs} tin {nearTin}");
        foreach (MiningCampState c in _camps) if (c.Parent == s.Id) text.Append($" camp@{c.Tile}({_world.Deposit(c.Tile).Kind},crew {CampCrewCount(c)},built {c.Built})");
        if (s.Metal is not null && CivInBronze(s.Civilization))
        {
            Fix64 total = Fix64.Zero;
            foreach (FigureState f in _figures) if (f.SettlementId == s.Id && f.Camp < 0) total += WorkCapacity(f);
            MetalPlan plan = PlanMetal(s, total);
            text.Append($" || plan tier {plan.Tier} budget {plan.Budget.ToDoubleForDisplay():F2} of {total.ToDoubleForDisplay():F1} build {plan.BuildNeed.ToDoubleForDisplay():F2} mine {plan.MineNeed.ToDoubleForDisplay():F2} char {plan.CharcoalNeed.ToDoubleForDisplay():F2} smelt {plan.SmeltNeed.ToDoubleForDisplay():F2}");
            for (int r = 0; r < _compiledRecipes.Length; r++)
                if (plan.Batches[r] > Fix64.Zero) text.Append($" {_compiledRecipes[r].Def.Id}x{plan.Batches[r].ToDoubleForDisplay():F2}");
        }
        return text.ToString();
    }

    // ---- goods by value (docs/MVP/22 §6.1, 23 §3): hooks for barter, compacts and loot ---------------------

    /// <summary>How much of a good a settlement holds, in the good's unit; 0 for goods the economy does not stock.</summary>
    public Fix64 GoodStock(int settlementIndex, string goodId)
    {
        SettlementState s = _settlements[settlementIndex];
        int slot = SlotOf(goodId);
        return slot == NoSlot ? Fix64.Zero : Stock(s, slot);
    }

    /// <summary>The registry's barter value of one unit of a good (docs/MVP/23 §3), 0 when unknown.</summary>
    public Fix64 GoodBarterValue(string goodId) => _content.BarterValue(goodId);

    /// <summary>
    /// The barter value of everything a settlement could trade beyond food and tools in hand: metals, ores, luxuries,
    /// processed goods and cast kits in stock, each at its registry value. Politics' barter weighs offers with it.
    /// </summary>
    public Fix64 TradeGoodsValue(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        if (s.Metal is not { } m) return Fix64.Zero;
        Fix64 total = Fix64.Zero;
        for (int g = 0; g < (int)MetalGood.Count; g++)
        {
            if (m.Goods[g] <= Fix64.Zero) continue;
            total += m.Goods[g] * _metalBarter[g];
        }
        return total;
    }

    /// <summary>
    /// Moves up to <paramref name="quantity"/> of a good from one settlement's stores to another's (a barter trip, a compact
    /// delivery or loot) and returns what moved. Only stocked goods move; kits in hand and buildings never do.
    /// </summary>
    public Fix64 TransferGood(int fromSettlement, int toSettlement, string goodId, Fix64 quantity)
    {
        int slot = SlotOf(goodId);
        if (slot == NoSlot || quantity <= Fix64.Zero) return Fix64.Zero;
        SettlementState from = _settlements[fromSettlement], to = _settlements[toSettlement];
        if (slot >= (int)Good.Count) EnsureMetal(to);
        Fix64 moved = Fix64.Min(quantity, Stock(from, slot));
        if (moved <= Fix64.Zero) return Fix64.Zero;
        if (slot < (int)Good.Count && ToolDurability(from, (Good)slot) > Fix64.Zero) return Fix64.Zero;
        AddStock(from, slot, -moved);
        AddStock(to, slot, moved);
        return moved;
    }

    // ---- hashing and snapshots ---------------------------------------------------------------

    private void HashBronze(ref StateHasher hash)
    {
        if (!_bronzeActive) return;
        hash.Add(BronzeTag);
        hash.Add(_world.DepositFingerprint);
        hash.Add(_content.BronzeFingerprint);
        hash.Add(_ore);
        foreach (byte state in _forestState) hash.Add(state);
        foreach (byte low in _forestLowSeasons) hash.Add(low);
        hash.Add(_nextCampId);
        hash.Add(_camps.Count);
        foreach (MiningCampState c in _camps)
        {
            hash.Add(c.Id); hash.Add(c.Parent); hash.Add(c.Tile); hash.Add(c.Opened); hash.Add(c.Built); hash.Add(c.Progress);
            hash.Add(c.MineHead); hash.Add(c.HeadProgress); hash.Add(c.Distance); hash.Add(c.Decreed); hash.Add(c.Target);
        }
        hash.Add(_metalStats);
        for (int i = 0; i < _settlements.Count; i++)
        {
            if (_settlements[i].Metal is not { } m) continue;
            hash.Add(i);
            hash.Add(m.Goods); hash.Add(m.ShareWant); hash.Add(m.Forges); hash.Add(m.Smelters);
            hash.Add(m.MineHeads.Count); foreach (int t in m.MineHeads) hash.Add(t);
            hash.Add((byte)m.BuildKind); hash.Add(m.BuildTile); hash.Add(m.BuildProgress); hash.Add(m.SiteClay); hash.Add(m.SiteStone); hash.Add(m.Access);
            hash.Add((byte)m.Reason); hash.Add(m.ClampTile); hash.Add(m.ForestAtStart); hash.Add(m.FuelShortSeasons);
        }
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState f = _figures[i];
            if (f.Kit == 0 && f.Task == 0 && f.Camp < 0 && f.Role == 0 && !f.Infected) continue;
            hash.Add(f.Id); hash.Add(f.Kit); hash.Add(f.KitLeft); hash.Add(f.Task); hash.Add(f.Camp); hash.Add(f.Role); hash.Add(f.Infected);
        }
        HashCivic(ref hash);
    }

    private void WriteBronze(BinaryWriter writer)
    {
        WriteFixArray(writer, _ore);
        writer.Write(_forestState.Length);
        writer.Write(_forestState);
        writer.Write(_forestLowSeasons);
        writer.Write(_nextCampId);
        writer.Write(_camps.Count);
        foreach (MiningCampState c in _camps)
        {
            writer.Write(c.Id); writer.Write(c.Parent); writer.Write(c.Tile); writer.Write(c.Opened); writer.Write(c.Built);
            writer.Write(c.Progress.Raw); writer.Write(c.MineHead); writer.Write(c.HeadProgress.Raw); writer.Write(c.Distance);
            writer.Write(c.Decreed); writer.Write(c.Target);
        }
        WriteFixArray(writer, _metalStats);
        for (int i = 0; i < _settlements.Count; i++)
        {
            MetalState? m = _settlements[i].Metal;
            writer.Write(m is not null);
            if (m is null) continue;
            WriteFixArray(writer, m.Goods); WriteFixArray(writer, m.ShareWant);
            writer.Write(m.Forges); writer.Write(m.Smelters);
            writer.Write(m.MineHeads.Count); foreach (int t in m.MineHeads) writer.Write(t);
            writer.Write((byte)m.BuildKind); writer.Write(m.BuildTile); writer.Write(m.BuildProgress.Raw);
            writer.Write(m.SiteClay.Raw); writer.Write(m.SiteStone.Raw); writer.Write(m.Access);
            writer.Write((byte)m.Reason); writer.Write(m.ClampTile); writer.Write(m.ForestAtStart.Raw); writer.Write(m.FuelShortSeasons);
        }
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState f = _figures[i];
            writer.Write(f.Kit); writer.Write(f.KitLeft.Raw); writer.Write(f.Task); writer.Write(f.Camp); writer.Write(f.Role); writer.Write(f.Infected);
        }
        WriteCivic(writer);
        WriteDecrees(writer);
    }

    private void ReadBronze(BinaryReader reader)
    {
        ReadFixArray(reader, _ore);
        if (reader.ReadInt32() != _forestState.Length) throw new InvalidDataException("Snapshot forest tile count differs.");
        byte[] states = reader.ReadBytes(_forestState.Length);
        byte[] lows = reader.ReadBytes(_forestLowSeasons.Length);
        if (states.Length != _forestState.Length || lows.Length != _forestLowSeasons.Length)
            throw new InvalidDataException("Snapshot forest state is truncated.");
        Array.Copy(states, _forestState, states.Length);
        Array.Copy(lows, _forestLowSeasons, lows.Length);
        _nextCampId = reader.ReadInt32();
        _camps.Clear();
        int camps = ReadCount(reader, 100_000);
        for (int i = 0; i < camps; i++)
        {
            _camps.Add(new MiningCampState
            {
                Id = reader.ReadInt32(), Parent = reader.ReadInt32(), Tile = reader.ReadInt32(), Opened = reader.ReadInt32(),
                Built = reader.ReadBoolean(), Progress = Fix64.FromRaw(reader.ReadInt64()), MineHead = reader.ReadBoolean(),
                HeadProgress = Fix64.FromRaw(reader.ReadInt64()), Distance = reader.ReadInt32(), Decreed = reader.ReadBoolean(),
                Target = reader.ReadInt32(),
            });
        }
        ReadFixArray(reader, _metalStats);
        for (int i = 0; i < _settlements.Count; i++)
        {
            if (!reader.ReadBoolean()) { _settlements[i].Metal = null; continue; }
            var m = new MetalState();
            ReadFixArray(reader, m.Goods); ReadFixArray(reader, m.ShareWant);
            m.Forges = reader.ReadInt32(); m.Smelters = reader.ReadInt32();
            int heads = ReadCount(reader, _world.TileCount);
            for (int h = 0; h < heads; h++) m.MineHeads.Add(reader.ReadInt32());
            m.BuildKind = (MetalBuild)reader.ReadByte(); m.BuildTile = reader.ReadInt32(); m.BuildProgress = Fix64.FromRaw(reader.ReadInt64());
            m.SiteClay = Fix64.FromRaw(reader.ReadInt64()); m.SiteStone = Fix64.FromRaw(reader.ReadInt64());
            m.Access = reader.ReadByte(); m.Reason = (MethodFallbackReason)reader.ReadByte(); m.ClampTile = reader.ReadInt32();
            m.ForestAtStart = Fix64.FromRaw(reader.ReadInt64()); m.FuelShortSeasons = reader.ReadInt32();
            _settlements[i].Metal = m;
        }
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState f = _figures[i];
            f.Kit = reader.ReadByte(); f.KitLeft = Fix64.FromRaw(reader.ReadInt64()); f.Task = reader.ReadByte(); f.Camp = reader.ReadInt32();
            f.Role = reader.ReadByte(); f.Infected = reader.ReadBoolean();
        }
        ReadCivic(reader);
        ReadDecrees(reader);
    }

    private void ValidateBronze()
    {
        ValidateNonnegative(_ore);
        for (int tile = 0; tile < _forestState.Length; tile++)
            if (_forestState[tile] > 2) throw new InvalidDataException("Snapshot contains an invalid forest state.");
        var campIds = new List<int>();
        foreach (MiningCampState c in _camps)
        {
            if (c.Id < 0 || c.Id >= _nextCampId || c.Parent < 0 || c.Parent >= _settlements.Count || c.Tile < 0 ||
                c.Tile >= _world.TileCount || c.Progress < Fix64.Zero || c.HeadProgress < Fix64.Zero || campIds.Contains(c.Id))
                throw new InvalidDataException("Snapshot contains an invalid mining camp.");
            campIds.Add(c.Id);
        }
        foreach (SettlementState s in _settlements)
        {
            if (s.Metal is not { } m) continue;
            for (int g = 0; g < m.Goods.Length; g++)
                if (m.Goods[g] < Fix64.Zero) throw new InvalidDataException("Snapshot contains a negative metal store.");
            if (m.Forges < 0 || m.Smelters < 0 || m.BuildProgress < Fix64.Zero || m.SiteClay < Fix64.Zero || m.SiteStone < Fix64.Zero || (uint)m.BuildKind > (uint)MetalBuild.MineHead ||
                (uint)m.Reason > (uint)MethodFallbackReason.NoFuel)
                throw new InvalidDataException("Snapshot contains invalid metal settlement state.");
        }
        foreach (FigureState f in _figures)
        {
            if (f.Kit is not (0 or 8 or 9 or 10) || f.Task >= MetalTask.Count || (f.Camp >= 0 && !campIds.Contains(f.Camp)) || f.Camp < -1 ||
                f.Role > Role.Scribe)
                throw new InvalidDataException("Snapshot contains an invalid figure kit or task.");
        }
        ValidateCivic();
    }

    private void CopyBronzeFrom(LivingEconomyModel source)
    {
        Array.Copy(source._ore, _ore, _ore.Length);
        Array.Copy(source._forestState, _forestState, _forestState.Length);
        Array.Copy(source._forestLowSeasons, _forestLowSeasons, _forestLowSeasons.Length);
        _nextCampId = source._nextCampId;
        _camps.Clear();
        foreach (MiningCampState c in source._camps)
            _camps.Add(new MiningCampState
            {
                Id = c.Id, Parent = c.Parent, Tile = c.Tile, Opened = c.Opened, Built = c.Built, Progress = c.Progress,
                MineHead = c.MineHead, HeadProgress = c.HeadProgress, Distance = c.Distance, Decreed = c.Decreed, Target = c.Target,
            });
        Array.Copy(source._metalStats, _metalStats, _metalStats.Length);
        CopyCivicFrom(source);
        RefreshBronzeActive();
    }

    private static MetalState? CloneMetal(MetalState? source)
    {
        if (source is null) return null;
        var m = new MetalState
        {
            Forges = source.Forges, Smelters = source.Smelters, BuildKind = source.BuildKind, BuildTile = source.BuildTile,
            BuildProgress = source.BuildProgress, SiteClay = source.SiteClay, SiteStone = source.SiteStone, Access = source.Access, Reason = source.Reason, ClampTile = source.ClampTile,
            ForestAtStart = source.ForestAtStart, FuelShortSeasons = source.FuelShortSeasons,
        };
        Array.Copy(source.Goods, m.Goods, m.Goods.Length);
        Array.Copy(source.ShareWant, m.ShareWant, m.ShareWant.Length);
        m.MineHeads.AddRange(source.MineHeads);
        return m;
    }
}
