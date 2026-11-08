using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>The three parallel build slots of a settlement: the basic list (homes, stores, early works, defenses), the Iron works and the civic buildings.</summary>
public enum BuildTrack : byte { Basic = 0, Iron = 1, Civic = 2 }

/// <summary>The class of reason something is not being built (docs/MVP/30 §2). <see cref="None"/> means the way is clear.</summary>
public enum BuildCause : byte
{
    None = 0,
    /// <summary>Wanted and allowed, but a good is short.</summary>
    Materials,
    /// <summary>Everything is in hand; the building work itself is still to do.</summary>
    Labor,
    /// <summary>Wanted, allowed and paid for, but another structure holds the slot first.</summary>
    Ahead,
    /// <summary>Allowed, but the town sees no need for it now.</summary>
    NotNeeded,
    /// <summary>The project that unlocks it is not learned.</summary>
    Locked,
    /// <summary>Another structure, project or condition has to come first.</summary>
    Requires,
    /// <summary>The ground or the neighbours rule it out here.</summary>
    Site,
    /// <summary>The town is too small, or the wrong tier.</summary>
    Size,
    /// <summary>It stands already, or the limit is reached.</summary>
    Cap,
    /// <summary>Someone else holds the only one, or it is reserved for another town.</summary>
    Taken,
}

/// <summary>The specific reason, which the text layer turns into plain words. The argument and project id travel in <see cref="BuildGate"/>.</summary>
public enum BuildWhy : byte
{
    None = 0,
    NotLearned, NotYourTown, SkipNotOffered, NoSuchStructure, AlreadyFirstInLine, WonderNotRedirectable, OnTheWayToCity,
    AlreadyBuilt, AtLimit, FieldsFull,
    NeedsPopulation, NeedsTown, NeedsCity,
    NeedsKiln, NeedsWalls, NeedsHarbor, NeedsRoad, NeedsTemple, NeedsMarket, NeedsRoutes, NeedsProject, NeedsSoldiers, NeedsGraftedOrchard, NeedsFields,
    NoFreeGround, NoRiver, NoHillSite, ShrineSpacing, NoTerraceTile, NoRivalNear, NotCoast, NotCapitalSite, NoLandRoute, IsCapital, NoRiverOnRoad,
    OnePerCivilization, WonderHeldElsewhere, AcademyLimit, MarketsGoToLargest, NotLargestTown, NotMember,
    HousingEnough, PlannerOnlyOne, WaterNotLimiting, CraneNoLargeWork, FlatFieldsRemain, CloseToCapital,
}

/// <summary>The first rule that stops a structure, with its number (people needed, tiles, routes) and the project that would lift it.</summary>
public readonly record struct BuildGate(BuildCause Cause, BuildWhy Why, int Arg = 0, string? Project = null)
{
    public static readonly BuildGate Pass = default;
    public bool Open => Cause == BuildCause.None;
}

/// <summary>A good a structure takes: its store slot (-1 for food), what the town holds and what it takes.</summary>
public readonly record struct BuildNeed(int Slot, Fix64 Have, Fix64 Need)
{
    public bool Short => Have < Need;
}

/// <summary>What stands between a town and a good it is short of.</summary>
public enum SupplyKind : byte
{
    /// <summary>The town already produces it; the shortage is a matter of time.</summary>
    Flowing = 0,
    /// <summary>A project has to be learned before anyone can make it (<see cref="GoodSupply.Project"/>).</summary>
    NeedsProject,
    /// <summary>There is none within the town's reach (the deposit, stand of timber or ore is not there or is used up).</summary>
    NoSource,
    /// <summary>Only a trade (compact) or a different town can bring it.</summary>
    NeedsOutside,
}

public readonly record struct GoodSupply(SupplyKind Kind, string? Project = null, int Reach = 0);

public sealed partial class LivingEconomyModel
{
    private static BuildGate Stop(BuildCause cause, BuildWhy why, int arg = 0, string? project = null) => new(cause, why, arg, project);

    /// <summary>The project whose completion unlocks the structure with this registry id ("" if none is listed).</summary>
    private string UnlockProjectOf(string structureId)
    {
        for (int i = 0; i < _content.Effects.Length; i++)
        {
            EconomyEffect effect = _content.Effects[i];
            if (effect.Kind == "build" && effect.Structure == structureId) return _content.Projects[effect.ProjectIndex].Id;
        }
        return "";
    }

    // ---- basic structures ---------------------------------------------------------------------------------

    /// <summary>
    /// The hard rules of a basic structure: learned, under its cap, big enough town, on suitable ground. The town's appetite (does it need the house now) is not
    /// part of this; <see cref="BasicRuleWanted"/> asks that. A gate that is open means the structure may be built here if the town (or the player) wants it.
    /// </summary>
    private BuildGate BasicGate(SettlementState s, Structure kind)
    {
        if (!CanBuild(s.Civilization, kind)) return Stop(BuildCause.Locked, BuildWhy.NotLearned, 0, UnlockProjectOf(StructureId(kind)));
        int pop = Population(s.Id);
        int built = s.Structures[(int)kind];
        BuildGate Built() => built > 0 ? Stop(BuildCause.Cap, BuildWhy.AlreadyBuilt) : BuildGate.Pass;
        BuildGate Size(int people) => pop < people ? Stop(BuildCause.Size, BuildWhy.NeedsPopulation, people) : BuildGate.Pass;
        switch (kind)
        {
            case Structure.StoragePit: case Structure.DryingRack: case Structure.Granary: case Structure.Hearth: case Structure.Kiln:
            case Structure.Quern: case Structure.ThornBarrier: case Structure.Lookout: case Structure.HideTent: case Structure.Palisade:
                return Built();
            case Structure.Totem: return Built() is { Open: false } t ? t : Size(8);
            case Structure.SignalPost: return Built() is { Open: false } sp ? sp : Size(12);
            case Structure.Marker:
                return built >= MarkersPerSettlement ? Stop(BuildCause.Cap, BuildWhy.AtLimit, MarkersPerSettlement) : Size(10);
            case Structure.Shrine:
                if (Built() is { Open: false } sh) return sh;
                if (Size(20) is { Open: false } shs) return shs;
                return OwnShrineWithin(s, ShrineSpacing) ? Stop(BuildCause.Site, BuildWhy.ShrineSpacing, ShrineSpacing) : BuildGate.Pass;
            case Structure.PottersWorkshop:
                if (Built() is { Open: false } pw) return pw;
                if (s.Structures[(int)Structure.Kiln] == 0) return Stop(BuildCause.Requires, BuildWhy.NeedsKiln);
                return Size(PottersMinPop);
            case Structure.Hillfort:
                if (Built() is { Open: false } hf) return hf;
                return HasHillfortSite(s) ? BuildGate.Pass : Stop(BuildCause.Site, BuildWhy.NoHillSite);
            case Structure.StoneWalls:
                return built >= (int)Tier(s.Id) + 1 ? Stop(BuildCause.Cap, BuildWhy.AtLimit, (int)Tier(s.Id) + 1) : BuildGate.Pass;
            case Structure.Weir:
                return CanPlaceWeir(s) ? BuildGate.Pass : Stop(BuildCause.Site, BuildWhy.NoRiver);
            case Structure.Pasture:
                return built >= PastureLimit(s) ? Stop(BuildCause.Cap, BuildWhy.AtLimit, PastureLimit(s)) : BuildGate.Pass;
            case Structure.Orchard:
                return CanPlaceOrchard(s) ? BuildGate.Pass : Stop(BuildCause.Site, BuildWhy.NoFreeGround);
            case Structure.Field:
                return FieldLimit(s) <= s.FieldTiles.Count ? Stop(BuildCause.Cap, BuildWhy.FieldsFull, FieldLimit(s)) : BuildGate.Pass;
            default: return BuildGate.Pass; // lean-to, pit house, wattle, mudbrick house, longhouse: as many as there is room and material for
        }
    }

    /// <summary>Whether any of the planner's rules for this structure calls for it now (the town's appetite, ignoring materials and whether it is learned).</summary>
    private bool BasicKindWanted(SettlementState s, Structure kind, ref BasicCtx ctx)
    {
        for (int r = 0; r < (int)BasicRule.Count; r++)
            if (BasicRuleKind(r) == kind && BasicRuleWanted(s, r, ref ctx)) return true;
        return false;
    }

    // ---- Iron works --------------------------------------------------------------------------------------

    /// <summary>
    /// The first rule that stops an Iron work here (not counting whether its project is learned), hard rules first. With <paramref name="includeAppetite"/> it also
    /// answers the planner's own question "does this town want one now"; <see cref="IronBuildWanted"/> is exactly "the gate is open with appetite included".
    /// </summary>
    private BuildGate IronGate(SettlementState s, IronStructure kind, bool includeAppetite)
    {
        int built = IronWorksCount(s, kind);
        bool town = s.Civic is { Tier: >= 1 };
        int pop = Population(s.Id);
        int civ = s.Civilization;
        BuildGate Built(int limit = 1) => built >= limit ? Stop(BuildCause.Cap, limit == 1 ? BuildWhy.AlreadyBuilt : BuildWhy.AtLimit, limit) : BuildGate.Pass;
        BuildGate Town() => town ? BuildGate.Pass : Stop(BuildCause.Size, BuildWhy.NeedsTown);
        BuildGate Size(int people) => pop < people ? Stop(BuildCause.Size, BuildWhy.NeedsPopulation, people) : BuildGate.Pass;
        bool WaterLimits() => WaterCapBase(s) <= Fix64.Min(FoodCap(s), Fix64.Min(HousingCap(s), GroupCap(s))) &&
                              Fix64.FromInt(pop) >= WaterCapBase(s) * Fix64.FromRatio(85, 100);
        BuildGate gate;
        switch (kind)
        {
            case IronStructure.Crane:
                if (!Built().Open) return Built();
                if (includeAppetite && !(IronGate(s, IronStructure.Aqueduct, true).Open || IronGate(s, IronStructure.Castle, true).Open))
                    return Stop(BuildCause.NotNeeded, BuildWhy.CraneNoLargeWork);
                return BuildGate.Pass;
            case IronStructure.WindlassWell:
                if (!Built(2).Open) return Built(2);
                if (!(gate = Town()).Open) return gate;
                if (includeAppetite && (!WaterLimits() || IronWorksCount(s, IronStructure.Aqueduct) != 0)) return Stop(BuildCause.NotNeeded, BuildWhy.WaterNotLimiting);
                return BuildGate.Pass;
            case IronStructure.Terrace:
                if (!(gate = Size(12)).Open) return gate;
                if (NextTerraceTile(s) < 0) return Stop(BuildCause.Site, BuildWhy.NoTerraceTile);
                if (includeAppetite && !NaturalFieldsFull(s)) return Stop(BuildCause.NotNeeded, BuildWhy.FlatFieldsRemain);
                return BuildGate.Pass;
            case IronStructure.Aqueduct:
                if (!(gate = Built()).Open) return gate;
                if (!(gate = Town()).Open) return gate;
                if (includeAppetite && !WaterLimits()) return Stop(BuildCause.NotNeeded, BuildWhy.WaterNotLimiting);
                return BuildGate.Pass;
            case IronStructure.Bathhouse:
                if (!(gate = Built()).Open) return gate;
                if (!(gate = Town()).Open) return gate;
                return Size(40);
            case IronStructure.Theatre:
                if (!(gate = Built()).Open) return gate;
                if (!(gate = Town()).Open) return gate;
                return Size(50);
            case IronStructure.Road:
            {
                if (!(gate = Built()).Open) return gate;
                if (!IsMember(s, civ)) return Stop(BuildCause.Taken, BuildWhy.NotMember);
                int capital = CapitalOf(civ);
                if (capital < 0 || capital == s.Id) return Stop(BuildCause.Site, BuildWhy.IsCapital);
                if (!LandConnected(_settlements[capital].SiteTile, s.SiteTile)) return Stop(BuildCause.Site, BuildWhy.NoLandRoute);
                if (includeAppetite && WalkingTiles(_settlements[capital].SiteTile, s.SiteTile) <= _civic.OverreachFree)
                    return Stop(BuildCause.NotNeeded, BuildWhy.CloseToCapital);
                return BuildGate.Pass;
            }
            case IronStructure.Bridge:
                if (!(gate = Built()).Open) return gate;
                if (IronWorksCount(s, IronStructure.Road) == 0) return Stop(BuildCause.Requires, BuildWhy.NeedsRoad);
                return RiversOnLink(s) > 0 ? BuildGate.Pass : Stop(BuildCause.Site, BuildWhy.NoRiverOnRoad);
            case IronStructure.Gatehouse:
                if (!(gate = Built()).Open) return gate;
                if (!(gate = Town()).Open) return gate;
                return HasStoneWalls(s) ? BuildGate.Pass : Stop(BuildCause.Requires, BuildWhy.NeedsWalls);
            case IronStructure.FrontierWall:
                if (!(gate = Built(2)).Open) return gate;
                if (!(gate = Town()).Open) return gate;
                return NextFrontierFacing(s) >= 0 ? BuildGate.Pass : Stop(BuildCause.Site, BuildWhy.NoRivalNear, FrontierReach);
            case IronStructure.Harbor:
                if (!(gate = Built()).Open) return gate;
                if (!(gate = Town()).Open) return gate;
                if (!IsPortSite(s.SiteTile)) return Stop(BuildCause.Site, BuildWhy.NotCoast);
                return Size(30);
            case IronStructure.Galley:
                if (!(gate = Built()).Open) return gate;
                if (!(gate = Town()).Open) return gate;
                if (IronWorksCount(s, IronStructure.Harbor) == 0) return Stop(BuildCause.Requires, BuildWhy.NeedsHarbor);
                if (!Done(civ, "WAR-07")) return Stop(BuildCause.Requires, BuildWhy.NeedsProject, 0, "WAR-07");
                return s.Metal is { } gm && gm.IronSoldiersSupplied >= GalleyCrew ? BuildGate.Pass : Stop(BuildCause.Requires, BuildWhy.NeedsSoldiers, GalleyCrew);
            case IronStructure.Academy:
                if (!(gate = Built()).Open) return gate;
                if (!(gate = Town()).Open) return gate;
                if (!(gate = Size(50)).Open) return gate;
                return CivWorksCount(civ, IronStructure.Academy) < MaxAcademies ? BuildGate.Pass : Stop(BuildCause.Taken, BuildWhy.AcademyLimit, MaxAcademies);
            case IronStructure.Press:
                if (!(gate = Built()).Open) return gate;
                if (!(gate = Town()).Open) return gate;
                if (!Done(civ, "FOR-08")) return Stop(BuildCause.Requires, BuildWhy.NeedsProject, 0, "FOR-08");
                return s.OrchardPlots.Count > 0 ? BuildGate.Pass : Stop(BuildCause.Requires, BuildWhy.NeedsGraftedOrchard);
            case IronStructure.WaterMill:
                if (!(gate = Built()).Open) return gate;
                if (!(gate = Town()).Open) return gate;
                if (!_nearRiver[s.SiteTile]) return Stop(BuildCause.Site, BuildWhy.NoRiver);
                return s.FieldTiles.Count >= MillFields ? BuildGate.Pass : Stop(BuildCause.Requires, BuildWhy.NeedsFields, MillFields);
            case IronStructure.Colonnade:
                if (!(gate = Built()).Open) return gate;
                if (!(gate = Town()).Open) return gate;
                return s.Civic is { Temples: > 0 } ? BuildGate.Pass : Stop(BuildCause.Requires, BuildWhy.NeedsTemple);
            case IronStructure.GreatLibrary:
            case IronStructure.GreatDome:
            case IronStructure.GreatLighthouse:
                if (!(gate = Built()).Open) return gate;
                if (!(gate = Town()).Open) return gate;
                if (WonderOwnerCiv(kind) >= 0) return Stop(BuildCause.Taken, BuildWhy.WonderHeldElsewhere);
                if (kind == IronStructure.GreatLighthouse && IronWorksCount(s, IronStructure.Harbor) == 0) return Stop(BuildCause.Requires, BuildWhy.NeedsHarbor);
                return WonderWanted(s, kind) ? BuildGate.Pass : Stop(BuildCause.Taken, BuildWhy.NotLargestTown);
            case IronStructure.Castle:
                if (!(gate = Built()).Open) return gate;
                if (!(gate = Town()).Open) return gate;
                if (!(gate = Size(60)).Open) return gate;
                if (!HasStoneWalls(s)) return Stop(BuildCause.Requires, BuildWhy.NeedsWalls);
                return NearestForeignDistance(s) <= CastleReach ? BuildGate.Pass : Stop(BuildCause.Site, BuildWhy.NoRivalNear, CastleReach);
            default:
                return Built();
        }
    }

    // ---- civic buildings ---------------------------------------------------------------------------------

    private static string CivicProject(CivicBuild b) => b switch
    {
        CivicBuild.Market => "TRD-04", CivicBuild.Temple => "BEL-06", CivicBuild.Palace => "GOV-05", CivicBuild.School => "LRN-04",
        CivicBuild.Bakery => "FOD-08", CivicBuild.Hall => "BLD-07", CivicBuild.Caravanserai => "TRD-05", CivicBuild.Ziggurat => "BLD-08", _ => "",
    };

    /// <summary>The structure id (registry) of a civic building.</summary>
    private static string CivicStructureId(CivicBuild b) => b switch
    {
        CivicBuild.Market => "market", CivicBuild.Temple => "temple", CivicBuild.Palace => "palace", CivicBuild.School => "school",
        CivicBuild.Bakery => "bakery", CivicBuild.Hall => "hall", CivicBuild.Caravanserai => "caravanserai", CivicBuild.Ziggurat => "ziggurat", _ => "",
    };

    /// <summary>The registry id of a civic building by its kind number ("" if none).</summary>
    public static string CivicStructureName(int kind) => CivicStructureId((CivicBuild)kind);

    /// <summary>The order the civic slot considers its candidates in (docs/MVP/23 §8).</summary>
    private static readonly CivicBuild[] CivicOrder =
    {
        CivicBuild.Market, CivicBuild.Palace, CivicBuild.Temple, CivicBuild.School, CivicBuild.Bakery, CivicBuild.Caravanserai, CivicBuild.Hall, CivicBuild.Ziggurat,
    };

    /// <summary>
    /// The first rule that stops a civic building here: its project, the tier, the one-per-town and one-per-civilization limits, and where the
    /// Market and Palace go. With <paramref name="includeAppetite"/> it also asks whether the Hall's housing is needed.
    /// </summary>
    private BuildGate CivicGate(SettlementState s, CivicState c, CivicBuild b, bool includeAppetite)
    {
        int civ = s.Civilization;
        string project = CivicProject(b);
        if (!Done(civ, project)) return Stop(BuildCause.Locked, BuildWhy.NotLearned, 0, project);
        bool townish = c.Tier >= (byte)SettlementTier.Town;
        BuildGate Town() => townish ? BuildGate.Pass : Stop(BuildCause.Size, BuildWhy.NeedsTown);
        BuildGate gate;
        switch (b)
        {
            case CivicBuild.Market:
                if (c.Markets > 0) return Stop(BuildCause.Cap, BuildWhy.AlreadyBuilt);
                return AmongLargest(s, 1 + (MemberCount(civ) - 1) / Math.Max(1, _civic.MarketEvery)) ? BuildGate.Pass : Stop(BuildCause.Taken, BuildWhy.MarketsGoToLargest);
            case CivicBuild.Palace:
                if (CivHasPalace(civ)) return Stop(BuildCause.Taken, BuildWhy.OnePerCivilization);
                return PalaceSite(civ) == s.Id ? BuildGate.Pass : Stop(BuildCause.Site, BuildWhy.NotCapitalSite);
            case CivicBuild.Temple:
                if (!(gate = Town()).Open) return gate;
                return c.Temples == 0 ? BuildGate.Pass : Stop(BuildCause.Cap, BuildWhy.AlreadyBuilt);
            case CivicBuild.School:
                if (!(gate = Town()).Open) return gate;
                return c.Schools == 0 ? BuildGate.Pass : Stop(BuildCause.Cap, BuildWhy.AlreadyBuilt);
            case CivicBuild.Bakery:
                if (!(gate = Town()).Open) return gate;
                if (c.Bakeries != 0) return Stop(BuildCause.Cap, BuildWhy.AlreadyBuilt);
                return HasProject(civ, "AGR-02") ? BuildGate.Pass : Stop(BuildCause.Requires, BuildWhy.NeedsProject, 0, "AGR-02");
            case CivicBuild.Caravanserai:
                if (!(gate = Town()).Open) return gate;
                if (c.Caravanserais != 0) return Stop(BuildCause.Cap, BuildWhy.AlreadyBuilt);
                return (TradeRoutes?.RoutesAt(s.Id) ?? 0) >= _civic.MarketTownRoutes ? BuildGate.Pass : Stop(BuildCause.Requires, BuildWhy.NeedsRoutes, _civic.MarketTownRoutes);
            case CivicBuild.Hall:
                if (c.Markets == 0) return Stop(BuildCause.Requires, BuildWhy.NeedsMarket);
                if (includeAppetite && HousingCap(s) >= HousingTarget(s)) return Stop(BuildCause.NotNeeded, BuildWhy.HousingEnough);
                return BuildGate.Pass;
            case CivicBuild.Ziggurat:
                if (c.Tier != (byte)SettlementTier.City) return Stop(BuildCause.Size, BuildWhy.NeedsCity);
                return CivHasZiggurat(civ) ? Stop(BuildCause.Taken, BuildWhy.OnePerCivilization) : BuildGate.Pass;
            default: return Stop(BuildCause.Locked, BuildWhy.NoSuchStructure);
        }
    }

    // ---- where a missing good comes from -----------------------------------------------------------------

    /// <summary>
    /// Whether the town can produce a good at all, from the same checks the gatherers make: the project that opens the work, and the deposit, forest or ore within reach.
    /// Read-only. The slot is a store slot (<see cref="StoreSlotId"/>), -1 for food.
    /// </summary>
    private GoodSupply SupplyOf(SettlementState s, int slot)
    {
        int civ = s.Civilization;
        if (slot < 0) return new GoodSupply(SupplyKind.Flowing);
        if (slot >= (int)Good.Count)
        {
            switch ((MetalGood)(slot - (int)Good.Count))
            {
                case MetalGood.Block:
                    if (!Done(civ, "MIN-03")) return new GoodSupply(SupplyKind.NeedsProject, "MIN-03");
                    return ReachStone(s) > Fix64.Zero ? new GoodSupply(SupplyKind.Flowing) : new GoodSupply(SupplyKind.NoSource, null, 8);
                case MetalGood.Ashlar:
                    if (!Done(civ, "MIN-03")) return new GoodSupply(SupplyKind.NeedsProject, "MIN-03");
                    if (!Done(civ, "STN-11")) return new GoodSupply(SupplyKind.NeedsProject, "STN-11");
                    return ReachStone(s) > Fix64.Zero || (s.Metal?.Goods[(int)MetalGood.Block] ?? Fix64.Zero) > Fix64.Zero
                        ? new GoodSupply(SupplyKind.Flowing) : new GoodSupply(SupplyKind.NoSource, null, 8);
                case MetalGood.Iron:
                    if (!Done(civ, "MET-09")) return new GoodSupply(SupplyKind.NeedsProject, "MET-09");
                    if (s.Metal is { } m && ((m.Access & (SourceIron | AccessIron)) != 0 || CanDig(s, MetalGood.IronOre))) return new GoodSupply(SupplyKind.Flowing);
                    return new GoodSupply(SupplyKind.NeedsOutside);
                case MetalGood.Wheel:
                    return new GoodSupply(SupplyKind.Flowing);
                default:
                    return new GoodSupply(SupplyKind.Flowing);
            }
        }
        switch ((Good)slot)
        {
            case Good.Wood:
            {
                Fix64 standing = Fix64.Zero;
                foreach (int tile in RangeTiles(s.SiteTile)) standing += _woodStock[tile];
                return standing > Fix64.Zero ? new GoodSupply(SupplyKind.Flowing) : new GoodSupply(SupplyKind.NoSource, null, 3);
            }
            case Good.Stone:
                return HasResourceInRange(s, _stoneStock) ? new GoodSupply(SupplyKind.Flowing) : new GoodSupply(SupplyKind.NoSource, null, 3);
            case Good.Clay:
                if (!HasProject(civ, "CER-01") && !HasProject(civ, "BLD-05")) return new GoodSupply(SupplyKind.NeedsProject, "CER-01");
                if (HasResourceInRange(s, _clayStock) || CivInBronze(civ)) return new GoodSupply(SupplyKind.Flowing);
                // No deposit in reach; from KEY-3 clay is dug from the ground anywhere, so that project is the way round it.
                return new GoodSupply(SupplyKind.NoSource, "KEY-3", 3);
            case Good.Mudbrick:
                if (!HasProject(civ, "BLD-05")) return new GoodSupply(SupplyKind.NeedsProject, "BLD-05");
                return SupplyOf(s, (int)Good.Clay);
            case Good.Sapling:
                return HasProject(civ, "FOR-07") ? new GoodSupply(SupplyKind.Flowing) : new GoodSupply(SupplyKind.NeedsProject, "FOR-07");
            default:
                return new GoodSupply(SupplyKind.Flowing);
        }
    }

    private Fix64 ReachStone(SettlementState s)
    {
        Fix64 total = Fix64.Zero;
        foreach (int tile in ReachTiles(s.SiteTile, 8))
            if (_stoneStock[tile] > Fix64.Zero && !IsRivalTile(s.Civilization, tile)) total += _stoneStock[tile];
        return total;
    }
}
