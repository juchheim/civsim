using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>A settlement's tier at game scale (docs/MVP/22 §8.1): village (and smaller), Town, City.</summary>
public enum SettlementTier : byte { Village = 0, Town = 1, City = 2 }

/// <summary>Which settlements belong to no civilization (Free Folk, docs/MVP/13 §9). Politics implements it.</summary>
public interface IFreeFolkStatus
{
    bool IsFreeFolk(int settlementIndex);
}

/// <summary>Trade routes through a settlement (docs/MVP/22 §6, §8.5). WP8 implements it; without one a settlement has none.</summary>
public interface ITradeRouteSource
{
    /// <summary>Trade routes that pass through or end at a settlement this season.</summary>
    int RoutesAt(int settlementIndex);
    /// <summary>Traffic arriving at a settlement this season as a share of its people (0 to 1): the epidemic inflow of docs/MVP/09 §10.1.</summary>
    Fix64 InflowAt(int settlementIndex);
}

/// <summary>
/// The early state of a settlement as other systems see it (docs/MVP/22 §8, 23 §8–§9): tier, capital, civic structures,
/// the staffed roles, Cohesion (C = (F − 0.6) ÷ 0.006) with its target, and the claim it emits.
/// </summary>
public readonly record struct CivicReadout(
    SettlementTier Tier, bool Capital, bool CohesionLive, int Cohesion, int CohesionTarget, int Markets, int Temples, int Palaces,
    int Schools, int Bakeries, int Halls, int Caravanserais, int Ziggurats, int Canals, int Priests, int Scribes, int Smiths,
    int Traders, int Bakers, bool Seceded, int LowCohesionSeasons, int ConqueredTerm, int Outbreak, Fix64 Sanitation,
    int ClaimRadius, Fix64 ClaimMult, int Horses);

/// <summary>
/// The early state (WP7, docs/MVP/22 §8–§11, 23 §1.2, §8–§12, 07d §9–§10): settlement tiers by headcount and structure, the
/// capital, the Market town housing rule (07d R2), civic structures and the roles they staff, live Cohesion with Admin span and
/// Overreach, secession, and the captured-town rule (07d R7). Like the metal economy it exists only for settlements of a
/// civilization past KEY-3 and is hashed only once the Bronze Age has begun, so E0–E2 checksums are unchanged.
/// </summary>
public sealed partial class LivingEconomyModel
{
    private enum CivicBuild : byte { None, Market, Temple, Palace, School, Bakery, Hall, Caravanserai, Ziggurat }

    private static class Role
    {
        public const byte None = 0, Priest = 1, Scribe = 2;
    }

    private sealed class CivicState
    {
        public int Markets, Temples, Palaces, Schools, Bakeries, Halls, Caravanserais, Ziggurats;
        public CivicBuild BuildKind;
        public Fix64 BuildProgress;
        /// <summary>Field tiles with a canal (docs/MVP/23 §8), in the order dug.</summary>
        public readonly List<int> Canals = new();
        public Fix64 CanalProgress;
        /// <summary>The dwelling a Market town is building ahead (an E2 structure kind; Count when none) and its progress (07d R2).</summary>
        public byte Dwelling = (byte)Structure.Count;
        public Fix64 DwellingProgress;
        public byte Tier;
        public byte DemoteSeasons;
        /// <summary>The season's work multiplier from Cohesion (docs/MVP/22 §8.3), set at the season start.</summary>
        public Fix64 WorkMult = Fix64.One;
        /// <summary>The season's food-value multiplier (bakeries and the 09 pass-through steps), set at the season start.</summary>
        public Fix64 FoodValueMult = Fix64.One;
        public Fix64 BakeryShare;
        public byte LowSeasons;
        public bool Seceded;
        public int RaidedSeason = -1000;
        /// <summary>The conquered term of the Cohesion target (07d R7): −35 at capture, +1 a season back to 0.</summary>
        public int ConqueredTerm;
        public int DeathsThisYear, DeathsLastYear, PeopleLastYear;
        public int Target = -1;
        public int Horses;
        // Epidemics (docs/MVP/09 §10): the class running here (0 none), its season (1 or 2), the attack rate and immunity.
        public byte Outbreak, OutbreakSeason;
        public Fix64 AttackRate, CfrScale = Fix64.One;
        public readonly Fix64[] Immune = new Fix64[OutbreakClasses];
        public readonly byte[] ImmuneSeasons = new byte[OutbreakClasses];
        public byte ImportedOutbreak;
        public Fix64 Sanitation;
        /// <summary>Harness tracing only (never hashed or saved): the last season's civic plan and labor.</summary>
        public Fix64 TracePlan, TraceLabor, TraceHousingWant;
    }

    private readonly struct CivicConstants
    {
        public CivicConstants(EconomyContent c)
        {
            Fix64 K(string id, string fallback) => c.Constant(id, Fix64.Parse(fallback));
            Base = K("CohesionBase", "60").RoundToInt();
            Step = K("CohesionStep", "0.25");
            WorkMid = K("CohesionWorkMid", "65");
            WorkRange = K("CohesionWorkRange", "35");
            WorkSlope = K("CohesionWorkSlope", "0.15");
            FissionBelow = K("CohesionFissionBelow", "35").RoundToInt();
            FissionTrigger = K("CohesionFissionTrigger", "0.75");
            SecessionBelow = K("SecessionBelow", "15").RoundToInt();
            SecessionSeasons = K("SecessionSeasons", "8").RoundToInt();
            Raided = K("CohesionRaided", "-10").RoundToInt();
            RaidedBel01 = K("CohesionRaidedBEL01", "-5").RoundToInt();
            RaidYears = K("CohesionRaidYears", "2").RoundToInt();
            Famine = K("CohesionFamine", "-20");
            LossPer2Pct = K("CohesionLossPer2Pct", "-1").RoundToInt();
            Crowding = K("CohesionCrowding", "-10");
            OverreachFree = K("OverreachFreeTiles", "8").RoundToInt();
            OverreachDivisor = K("OverreachDivisor", "2");
            SpanPenalty = K("SpanPenalty", "3").RoundToInt();
            Conquered = K("ConqueredCohesion", "-35").RoundToInt();
            ConqueredRecovery = K("ConqueredRecovery", "1").RoundToInt();
            ConqueredStart = K("ConqueredStartCohesion", "25").RoundToInt();
            TownMinPop = K("TownMinPop", "60").RoundToInt();
            CityMinPop = K("CityMinPop", "90").RoundToInt();
            DemoteShare = K("TierDemoteShare", "0.85");
            DemoteSeasons = K("TierDemoteSeasons", "4").RoundToInt();
            HinterlandRadius = K("HinterlandRadius", "7").RoundToInt();
            HinterlandEmit = K("HinterlandEmit", "0.5");
            CapitalClaimMult = K("CapitalClaimMult", "1.25");
            SpecialistRate = K("SpecialistRateMult", "1.3");
            LaborShare = K("CivicLaborShare", "0.10");
            MarketEvery = K("MarketEvery", "4").RoundToInt();
            CanalRiverDist = K("CanalRiverDist", "2").RoundToInt();
            CanalLabor = K("CanalLabor", "1");
            ArdPerPair = K("ArdFieldsPerPair", "4").RoundToInt();
            ArdPerPairNrg02 = K("ArdFieldsPerPairNRG02", "6").RoundToInt();
            ManureCattle = K("ManureCattlePer4", "2");
            ManureSheep = K("ManureSheepPer4", "6");
            BakeryValue = K("BakeryGrainValue", "0.2");
            BakeryPeople = K("BakeryPeople", "40");
            HorseTameShare = K("HorseTameShare", "0.15");
            HorsePerPasture = K("HorsePerPasture", "2").RoundToInt();
            HorseLabor = K("HorseTameLabor", "1");
            MarketTownFoodCap = K("MarketTownFoodCap", "1.25");
            MarketTownRoutes = K("MarketTownRoutes", "2").RoundToInt();
            PlagueR0 = K("PlagueR0", "2.0");
            PlagueCfr = K("PlagueCFR", "0.35");
            Inflow = K("EpidemicInflowE3", "0.2");
            HearthPeople = K("HearthPeople", "35");
            KilnWood = K("KilnWoodPerPerson", "0.2");
            BuildWood = K("BuildWoodPerPerson", "0.15");
            Deadwood = K("DeadwoodPerTile", "2");
        }

        public readonly int Base, FissionBelow, SecessionBelow, SecessionSeasons, Raided, RaidedBel01, RaidYears, LossPer2Pct,
            OverreachFree, SpanPenalty, Conquered, ConqueredRecovery, ConqueredStart, TownMinPop, CityMinPop, DemoteSeasons,
            HinterlandRadius, MarketEvery, CanalRiverDist, ArdPerPair, ArdPerPairNrg02, HorsePerPasture, MarketTownRoutes;
        public readonly Fix64 Step, WorkMid, WorkRange, WorkSlope, FissionTrigger, Famine, Crowding, OverreachDivisor, DemoteShare,
            HinterlandEmit, CapitalClaimMult, SpecialistRate, LaborShare, CanalLabor, ManureCattle, ManureSheep, BakeryValue,
            BakeryPeople, HorseTameShare, HorseLabor, MarketTownFoodCap, PlagueR0, PlagueCfr, Inflow, HearthPeople, KilnWood,
            BuildWood, Deadwood;
    }

    /// <summary>An E3 project's `cohesion` effect as a term of the target (docs/MVP/23 §1.2).</summary>
    private readonly record struct CohesionTerm(int Project, int Value, string Scope, int Radius, CivicBuild Structure, bool Wonder);

    private CivicConstants _civic;
    private CohesionTerm[] _cohesionTerms = Array.Empty<CohesionTerm>();
    private int _townRadius = 5, _cityRadius = 6;
    private Fix64 _capitalClaimMult = Fix64.FromRatio(5, 4);
    private readonly List<int> _secessions = new();
    private readonly List<(int Civ, string Project)> _wondersBuilt = new();
    // Derived, never hashed: walking costs (tenths of a tile) from a capital's site, per site tile.
    private int[]?[] _walkFrom = Array.Empty<int[]?>();

    private static readonly Fix64 CohesionFloor = Fix64.FromRatio(6, 10);
    private static readonly Fix64 CohesionPerPoint = Fix64.FromRatio(6, 1000);

    /// <summary>Who is Free Folk (politics); without it no settlement is.</summary>
    public IFreeFolkStatus? FreeFolkStatus { get; set; }

    /// <summary>Trade routes (WP8); without it no settlement is on a route.</summary>
    public ITradeRouteSource? TradeRoutes { get; set; }

    private void InitializeCivic()
    {
        _civic = new CivicConstants(_content);
        _walkFrom = new int[]?[_world.TileCount];
        var terms = new List<CohesionTerm>();
        foreach (EconomyProject p in _content.Projects)
        {
            if (p.Era != 3) continue;
            CivicBuild structure = CivicBuild.None;
            bool wonder = false;
            for (int i = p.EffectStart; i < p.EffectStart + p.EffectCount; i++)
            {
                EconomyEffect e = _content.Effects[i];
                if (e.Kind is "build" or "wonder" && CivicOf(e.Structure) is { } k and not CivicBuild.None)
                {
                    structure = k;
                    wonder |= e.Kind == "wonder";
                }
            }
            for (int i = p.EffectStart; i < p.EffectStart + p.EffectCount; i++)
            {
                EconomyEffect e = _content.Effects[i];
                if (e.Kind == "cohesion" && e.Value is { } v)
                    terms.Add(new CohesionTerm(p.Index, v.RoundToInt(), e.Scope ?? "all", e.Radius ?? 0, structure, wonder));
                if (e.Kind == "tier")
                {
                    if (e.Tier == "town" && e.Radius is { } tr) _townRadius = tr;
                    if (e.Tier == "city" && e.Radius is { } cr) _cityRadius = cr;
                    if (e.Tier == "capital" && e.ClaimMult is { } cm) _capitalClaimMult = cm;
                }
            }
        }
        _cohesionTerms = terms.ToArray();
    }

    private static CivicBuild? CivicOf(string? structure) => structure switch
    {
        "market" => CivicBuild.Market, "temple" => CivicBuild.Temple, "palace" => CivicBuild.Palace, "school" => CivicBuild.School,
        "bakery" => CivicBuild.Bakery, "hall" => CivicBuild.Hall, "caravanserai" => CivicBuild.Caravanserai,
        "ziggurat" => CivicBuild.Ziggurat, _ => null,
    };

    private CivicState EnsureCivic(SettlementState s) => s.Civic ??= new CivicState();

    /// <summary>Cohesion is live for a settlement once its civilization has completed KEY-3 (docs/MVP/22 §8.3).</summary>
    private bool CohesionLive(SettlementState s) => s.Civic is { Seceded: false } && CivInBronze(s.Civilization) && !IsFreeFolk(s);

    /// <summary>The Cohesion inputs needed by Council urgency, without constructing a full civic readout.</summary>
    public (bool Live, int Points) CohesionStatus(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        return (CohesionLive(s), CohesionPoints(s.Cohesion));
    }

    private bool IsFreeFolk(SettlementState s) => FreeFolkStatus is { } f && f.IsFreeFolk(s.Id);

    private static int CohesionPoints(Fix64 f) => Math.Clamp(((f - CohesionFloor) / CohesionPerPoint).RoundToInt(), 0, 100);

    private static Fix64 CohesionFactor(int c) => CohesionFloor + CohesionPerPoint * Math.Clamp(c, 0, 100);

    private static int Count(CivicState c, CivicBuild b) => b switch
    {
        CivicBuild.Market => c.Markets, CivicBuild.Temple => c.Temples, CivicBuild.Palace => c.Palaces,
        CivicBuild.School => c.Schools, CivicBuild.Bakery => c.Bakeries, CivicBuild.Hall => c.Halls,
        CivicBuild.Caravanserai => c.Caravanserais, CivicBuild.Ziggurat => c.Ziggurats, _ => 0,
    };

    private static void AddBuilt(CivicState c, CivicBuild b)
    {
        switch (b)
        {
            case CivicBuild.Market: c.Markets++; break;
            case CivicBuild.Temple: c.Temples++; break;
            case CivicBuild.Palace: c.Palaces++; break;
            case CivicBuild.School: c.Schools++; break;
            case CivicBuild.Bakery: c.Bakeries++; break;
            case CivicBuild.Hall: c.Halls++; break;
            case CivicBuild.Caravanserai: c.Caravanserais++; break;
            case CivicBuild.Ziggurat: c.Ziggurats++; break;
        }
    }

    // ---- the polity: members, capital, Admin span ---------------------------------------------------------

    /// <summary>Whether a settlement counts as one of its civilization's own (people, not seceded, not Free Folk).</summary>
    private bool IsMember(SettlementState s, int civ)
        => s.Civilization == civ && !(s.Civic?.Seceded ?? false) && !IsFreeFolk(s) && Population(s.Id) > 0;

    /// <summary>
    /// The capital (docs/MVP/23 §9, C13): the settlement with the Palace, else the civilization's oldest settlement. -1 when the
    /// civilization holds none.
    /// </summary>
    private int CapitalOf(int civ)
    {
        int oldest = -1;
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState s = _settlements[i];
            if (!IsMember(s, civ)) continue;
            if (s.Civic is { Palaces: > 0 }) return i;
            if (oldest < 0) oldest = i;
        }
        return oldest;
    }

    private int MemberCount(int civ)
    {
        int n = 0;
        foreach (SettlementState s in _settlements) if (IsMember(s, civ)) n++;
        return n;
    }

    /// <summary>Admin span: settlements held without strain (docs/MVP/22 §8.2), the sum of the completed `admin` spans.</summary>
    internal int AdminSpanOf(int civ)
    {
        int span = 0;
        foreach (EconomyEffect e in _content.Effects)
            if (e.Kind == "admin" && e.Span is { } sp && _completedProjects[civ][e.ProjectIndex]) span += sp;
        return span;
    }

    /// <summary>AdminReduction = 1 − Π(1 − r) over completed `admin` effects (docs/MVP/22 §8.3).</summary>
    internal Fix64 AdminReductionOf(int civ)
    {
        Fix64 keep = Fix64.One;
        foreach (EconomyEffect e in _content.Effects)
            if (e.Kind == "admin" && e.Overreach is { } r && r > Fix64.Zero && _completedProjects[civ][e.ProjectIndex])
                keep *= Fix64.One - Fix64.Min(Fix64.One, r);
        return Fix64.One - keep;
    }

    /// <summary>Walking tiles between two sites (forest 1.5, wetland 2, mountain 4: the start-pair costs of docs/MVP/12 §9.3).</summary>
    private int WalkingTiles(int fromSite, int toSite)
    {
        int[] cost = _walkFrom[fromSite] ??= WalkingCosts(fromSite);
        int c = cost[toSite];
        // Across water (another landmass) the walk is the hex distance by boat at the open-ground rate.
        return c == int.MaxValue ? HexGrid.Distance(fromSite, toSite, _world.Width) : (c + 9) / 10;
    }

    private int[] WalkingCosts(int start)
    {
        int n = _world.TileCount;
        var distance = new int[n];
        Array.Fill(distance, int.MaxValue);
        distance[start] = 0;
        var queue = new PriorityQueue<int, long>();
        queue.Enqueue(start, 0);
        Span<int> neighbors = stackalloc int[6];
        while (queue.TryDequeue(out int tile, out long key))
        {
            int priority = (int)(key >> 32);
            if (priority != distance[tile]) continue;
            int count = HexGrid.Neighbors(tile, _world.Width, _world.Height, neighbors);
            for (int k = 0; k < count; k++)
            {
                int next = neighbors[k];
                int move = (Biome)_world.Tile(next).BiomeId switch
                {
                    Biome.Ocean or Biome.Peak => -1,
                    Biome.Coast => 10,
                    Biome.TemperateForest or Biome.Taiga or Biome.Desert => 15,
                    Biome.OpenWoodland => 12,
                    Biome.TropicalForest => 16,
                    Biome.Wetland => 20,
                    Biome.Tundra => 13,
                    Biome.Mountain => 40,
                    Biome.Ice => 30,
                    _ => 10,
                };
                if (move < 0) continue;
                int candidate = checked(distance[tile] + move);
                if (candidate >= distance[next]) continue;
                distance[next] = candidate;
                // Ties dequeue by tile id, so the order (not just the distances) is the same everywhere.
                queue.Enqueue(next, ((long)candidate << 32) | (uint)next);
            }
        }
        return distance;
    }

    // ---- the season start: civic state, roles, work and food multipliers ---------------------------------

    private void CivicSeasonStart(int season)
    {
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState s = _settlements[i];
            if (s.Civic is null && CivInBronze(s.Civilization) && Population(s.Id) > 0) EnsureCivic(s);
            if (s.Civic is not { } c) continue;
            c.WorkMult = Fix64.One;
            if (CohesionLive(s))
            {
                int points = CohesionPoints(s.Cohesion);
                Fix64 mult = Fix64.One + _civic.WorkSlope * (Fix64.FromInt(points) - _civic.WorkMid) / _civic.WorkRange;
                c.WorkMult = Fix64.Clamp(mult, Fix64.FromRatio(85, 100), Fix64.FromRatio(115, 100));
            }
            c.FoodValueMult = FoodValuePassThrough(s, c);
        }
        RedistributeFood();
        StartOutbreaks(season);
    }

    /// <summary>
    /// Priests and Scribes by structure (docs/MVP/23 §9): one Priest per Temple, one Scribe at the capital from COM-05 (the
    /// Palace's once it stands) and two per School. The holders work their role all season (ritual, records) and are kept from
    /// season to season; the least senior go first when a role is no longer needed.
    /// </summary>
    private Fix64 StaffRoles(SettlementState s)
    {
        if (s.Civic is not { } c || !CohesionLive(s)) return Fix64.Zero;
        int civ = s.Civilization;
        int priests = Done(civ, "BEL-06") ? c.Temples : 0;
        int scribes = 0;
        if (Done(civ, "COM-05"))
        {
            if (CapitalOf(civ) == s.Id) scribes++;
            if (Done(civ, "LRN-04")) scribes += 2 * c.Schools;
        }
        int havePriests = 0, haveScribes = 0;
        for (int i = _figures.Count - 1; i >= 0; i--)
        {
            FigureState f = _figures[i];
            if (f.SettlementId != s.Id || f.Role == Role.None) continue;
            bool keep = f.Age is >= 14 and < 60 && f.Camp < 0;
            if (keep && f.Role == Role.Priest && havePriests < priests) { havePriests++; continue; }
            if (keep && f.Role == Role.Scribe && haveScribes < scribes) { haveScribes++; continue; }
            f.Role = Role.None;
        }
        int adults = 0;
        foreach (FigureState f in _figures) if (f.SettlementId == s.Id && f.Age is >= 14 and < 60 && f.Camp < 0) adults++;
        for (int i = 0; i < _figures.Count && (havePriests < priests || haveScribes < scribes); i++)
        {
            // Keep at least four other working adults at home.
            if (havePriests + haveScribes + 4 >= adults) break;
            FigureState f = _figures[i];
            if (f.SettlementId != s.Id || f.Role != Role.None || f.Age is < 14 or >= 60 || f.Camp >= 0 || f.Id == s.FireKeeperId) continue;
            if (havePriests < priests) { f.Role = Role.Priest; havePriests++; }
            else { f.Role = Role.Scribe; haveScribes++; }
        }
        Fix64 spent = Fix64.Zero;
        foreach (FigureState f in _figures)
        {
            if (f.SettlementId != s.Id || f.Role == Role.None) continue;
            Fix64 amount = WorkCapacity(f) - f.CareWorkThisSeason - f.FireWorkThisSeason;
            if (amount <= Fix64.Zero) continue;
            Assign(f, EconomyJob.Craft, amount);
            f.Task = f.Role == Role.Priest ? MetalTask.Ritual : MetalTask.Records;
            spent += amount;
        }
        return spent;
    }

    // ---- civic construction --------------------------------------------------------------------------------

    /// <summary>
    /// The civic build plan (docs/MVP/23 §8): canals over the fields within 2 tiles of a river first, then the settlement's
    /// next structure. Returns the labor it wants this season, at most <c>CivicLaborShare</c> of the work (halved in hunger).
    /// </summary>
    private (Fix64 Labor, Fix64 CanalShare, Fix64 HousingShare) PlanCivic(SettlementState s, Fix64 totalWork)
    {
        if (s.Civic is not { } c || !CohesionLive(s)) return (Fix64.Zero, Fix64.Zero, Fix64.Zero);
        // A Market town builds its housing ahead (07d R2) with another share of the work on top of the civic structures.
        bool town = IsMarketTown(s);
        Fix64 cap = totalWork * _civic.LaborShare * (town ? 2 : 1);
        if (s.FoodSecurity < Fix64.FromRatio(8, 10)) cap *= Fix64.Half;
        Fix64 canals = Fix64.FromInt(CanalCandidates(s)) * _civic.CanalLabor;
        Fix64 housing = town ? HousingWant(s, c) : Fix64.Zero;
        if (c.BuildKind == CivicBuild.None || !CanStillBuild(s, c.BuildKind))
        {
            c.BuildKind = ChooseCivic(s, c);
            c.BuildProgress = Fix64.Zero;
        }
        Fix64 structure = Fix64.Zero;
        if (c.BuildKind != CivicBuild.None && CivicMaterialsReady(s, c.BuildKind, check: true))
            structure = Fix64.Max(Fix64.Zero, CivicLabor(s, c.BuildKind) - c.BuildProgress) + CivicGatherLabor(s, c.BuildKind);
        Fix64 want = canals + housing + structure;
        c.TracePlan = want; c.TraceHousingWant = housing;
        if (want <= Fix64.Zero) return (Fix64.Zero, Fix64.Zero, Fix64.Zero);
        // The structure under way comes first; canals and a Market town's dwellings share what is left in proportion.
        return (Fix64.Min(cap, want), canals, housing);
    }

    /// <summary>The dwelling a Market town builds ahead: a longhouse (25) where known, else wattle and daub (8), else a pit house (6).</summary>
    /// <summary>
    /// The dwelling a Market town builds ahead: a longhouse (25) where known and the timber is there, else mudbrick (8; no wood,
    /// for a range its villages have cleared), else wattle and daub (8), else a pit house (6).
    /// </summary>
    private Structure ChooseDwelling(SettlementState s)
    {
        int civ = s.Civilization;
        Fix64 wood = s.Stores[(int)Good.Wood];
        foreach (int tile in RangeTiles(s.SiteTile)) wood += _woodStock[tile];
        if (CanBuild(civ, Structure.Longhouse) && wood >= Fix64.FromInt(24)) return Structure.Longhouse;
        if (CanBuild(civ, Structure.MudbrickHouse)) return Structure.MudbrickHouse;
        if (CanBuild(civ, Structure.WattleHouse) && wood >= Fix64.FromInt(6)) return Structure.WattleHouse;
        if (CanBuild(civ, Structure.PitHouse) && wood >= Fix64.FromInt(8)) return Structure.PitHouse;
        return Structure.Count;
    }

    private static (int Wood, int Clay, int Bricks) DwellingMaterials(Structure kind) => kind switch
    {
        Structure.Longhouse => (12, 4, 0), Structure.WattleHouse => (3, 2, 0), Structure.PitHouse => (4, 0, 0),
        Structure.MudbrickHouse => (0, 0, 60), _ => (0, 0, 0),
    };

    private Fix64 DwellingLabor(SettlementState s, Structure kind)
    {
        Fix64 labor = BuildLabor(kind) * Modifier(s.Civilization, "construction_labor");
        Fix64 timber = Modifier(s.Civilization, "timber_building_labor");
        return timber > Fix64.Zero ? labor * timber : labor;
    }

    private Fix64 HousingWant(SettlementState s, CivicState c)
    {
        if (HousingCap(s) >= HousingTarget(s)) return Fix64.Zero;
        if (c.Dwelling == (byte)Structure.Count) c.Dwelling = (byte)ChooseDwelling(s);
        var kind = (Structure)c.Dwelling;
        if (kind == Structure.Count) return Fix64.Zero;
        (int wood, int clay, int bricks) = DwellingMaterials(kind);
        Fix64 labor = Fix64.Max(Fix64.Zero, DwellingLabor(s, kind) - c.DwellingProgress);
        labor += Fix64.Max(Fix64.Zero, Fix64.FromInt(wood) - s.Stores[(int)Good.Wood]) / CivicChopRate;
        Fix64 rate = ClayRate(s, out _);
        if (rate > Fix64.Zero) labor += Fix64.Max(Fix64.Zero, Fix64.FromInt(clay) - s.Stores[(int)Good.Clay]) / rate;
        Fix64 missingBricks = Fix64.Max(Fix64.Zero, Fix64.FromInt(bricks) - s.Stores[(int)Good.Mudbrick] - s.DryingMudbrick);
        if (rate > Fix64.Zero) labor += missingBricks / 10 * (Fix64.One / rate + Fix64.FromRatio(3, 10));
        return labor;
    }

    private Fix64 WorkHousing(SettlementState s, CivicState c, Fix64 labor)
    {
        var kind = (Structure)c.Dwelling;
        if (kind == Structure.Count || labor <= Fix64.Zero) return labor;
        (int wood, int clay, int bricks) = DwellingMaterials(kind);
        labor = MakeCivicBricks(s, bricks, labor);
        labor = GatherMaterials(s, wood, 0, clay, labor);
        Fix64 need = DwellingLabor(s, kind);
        Fix64 put = Fix64.Min(labor, Fix64.Max(Fix64.Zero, need - c.DwellingProgress));
        c.DwellingProgress += put;
        labor -= put;
        if (c.DwellingProgress < need || s.Stores[(int)Good.Wood] < Fix64.FromInt(wood) || s.Stores[(int)Good.Clay] < Fix64.FromInt(clay) ||
            s.Stores[(int)Good.Mudbrick] < Fix64.FromInt(bricks)) return labor;
        s.Stores[(int)Good.Wood] -= Fix64.FromInt(wood);
        s.Stores[(int)Good.Clay] -= Fix64.FromInt(clay);
        s.Stores[(int)Good.Mudbrick] -= Fix64.FromInt(bricks);
        s.Structures[(int)kind]++;
        c.DwellingProgress = Fix64.Zero;
        c.Dwelling = (byte)Structure.Count;
        return labor;
    }

    private CivicBuild ChooseCivic(SettlementState s, CivicState c)
    {
        int civ = s.Civilization;
        int pop = Population(s.Id);
        // The Market goes to the civilization's largest settlements (one more per MarketEvery); from the moment it is chosen the
        // settlement keeps its people (07d R2), and it opens once 40 live there (docs/MVP/23 §8).
        if (Done(civ, "TRD-04") && c.Markets == 0 && AmongLargest(s, 1 + (MemberCount(civ) - 1) / Math.Max(1, _civic.MarketEvery)))
            return CivicBuild.Market;
        bool townish = c.Tier >= (byte)SettlementTier.Town;
        if (Done(civ, "GOV-05") && !CivHasPalace(civ) && PalaceSite(civ) == s.Id) return CivicBuild.Palace;
        if (Done(civ, "BEL-06") && townish && c.Temples == 0) return CivicBuild.Temple;
        if (Done(civ, "LRN-04") && townish && c.Schools == 0) return CivicBuild.School;
        if (Done(civ, "FOD-08") && townish && c.Bakeries == 0 && HasProject(civ, "AGR-02")) return CivicBuild.Bakery;
        if (Done(civ, "TRD-05") && townish && c.Caravanserais == 0 && (TradeRoutes?.RoutesAt(s.Id) ?? 0) >= _civic.MarketTownRoutes)
            return CivicBuild.Caravanserai;
        if (Done(civ, "BLD-07") && c.Markets > 0 && HousingCap(s) < HousingTarget(s) && s.Stores[(int)Good.Mudbrick] >= Fix64.FromInt(120)) return CivicBuild.Hall;
        if (Done(civ, "BLD-08") && c.Tier == (byte)SettlementTier.City && !CivHasZiggurat(civ)) return CivicBuild.Ziggurat;
        return CivicBuild.None;
    }

    private bool CanStillBuild(SettlementState s, CivicBuild b)
    {
        CivicState c = s.Civic!;
        int civ = s.Civilization;
        return b switch
        {
            CivicBuild.Market => c.Markets == 0,
            CivicBuild.Palace => !CivHasPalace(civ),
            CivicBuild.Temple => c.Temples == 0 && c.Tier >= 1,
            CivicBuild.School => c.Schools == 0 && c.Tier >= 1,
            CivicBuild.Bakery => c.Bakeries == 0 && c.Tier >= 1,
            CivicBuild.Caravanserai => c.Caravanserais == 0,
            CivicBuild.Hall => true,
            CivicBuild.Ziggurat => !CivHasZiggurat(civ) && c.Tier == 2,
            _ => false,
        };
    }

    private bool AmongLargest(SettlementState s, int count)
    {
        int pop = Population(s.Id), larger = 0;
        foreach (SettlementState o in _settlements)
        {
            if (o == s || !IsMember(o, s.Civilization)) continue;
            int p = Population(o.Id);
            if (p > pop || (p == pop && o.Id < s.Id)) larger++;
        }
        return larger < count;
    }

    private bool CivHasPalace(int civ)
    {
        foreach (SettlementState s in _settlements) if (IsMember(s, civ) && s.Civic is { Palaces: > 0 }) return true;
        return false;
    }

    private bool CivHasZiggurat(int civ)
    {
        foreach (SettlementState s in _settlements) if (s.Civilization == civ && s.Civic is { Ziggurats: > 0 }) return true;
        return false;
    }

    /// <summary>Where the Palace goes (GOV-05): the civilization's largest Town, else its largest settlement.</summary>
    private int PalaceSite(int civ)
    {
        int best = -1, bestPop = -1;
        bool bestTown = false;
        foreach (SettlementState s in _settlements)
        {
            if (!IsMember(s, civ)) continue;
            bool town = s.Civic is { Tier: >= 1 };
            int pop = Population(s.Id);
            if (best < 0 || (town && !bestTown) || (town == bestTown && pop > bestPop)) { best = s.Id; bestPop = pop; bestTown = town; }
        }
        return best;
    }

    /// <summary>Labor of a civic structure (docs/MVP/23 §8) after NUM-04, STN-11, WOD-05 and the Wonder mods.</summary>
    private Fix64 CivicLabor(SettlementState s, CivicBuild b)
    {
        int civ = s.Civilization;
        Fix64 labor = b switch
        {
            CivicBuild.Market => Fix64.FromInt(6), CivicBuild.Temple => Fix64.FromInt(20), CivicBuild.Palace => Fix64.FromInt(30),
            CivicBuild.School => Fix64.FromInt(8), CivicBuild.Bakery => Fix64.FromInt(4), CivicBuild.Hall => Fix64.FromInt(10),
            CivicBuild.Caravanserai => Fix64.FromInt(8), CivicBuild.Ziggurat => Fix64.FromInt(200), _ => Fix64.Zero,
        };
        labor *= Modifier(civ, "construction_labor");
        if (b is CivicBuild.Temple or CivicBuild.Palace or CivicBuild.Hall or CivicBuild.Ziggurat) labor *= Modifier(civ, "stone_building_labor");
        if (b is CivicBuild.Market or CivicBuild.School or CivicBuild.Caravanserai) labor *= Modifier(civ, "timber_building_labor");
        if (b == CivicBuild.Ziggurat)
        {
            labor *= Modifier(civ, "wonder_build_labor");
            Fix64 rate = Modifier(civ, "wonder_build_rate");
            if (rate > Fix64.Zero) labor /= rate;
        }
        return labor;
    }

    /// <summary>Materials (docs/MVP/23 §8): (wood, stone, clay, mudbrick, cut stone or blocks).</summary>
    private static (int Wood, int Stone, int Clay, int Mudbrick, int Cut) CivicMaterials(CivicBuild b) => b switch
    {
        CivicBuild.Market => (8, 2, 0, 0, 0),
        CivicBuild.Temple => (0, 0, 0, 120, 10),
        CivicBuild.Palace => (0, 0, 0, 200, 20),
        CivicBuild.School => (6, 4, 4, 0, 0),
        CivicBuild.Bakery => (0, 4, 4, 0, 0),
        CivicBuild.Hall => (6, 0, 0, 120, 4),
        CivicBuild.Caravanserai => (6, 6, 0, 0, 0),
        CivicBuild.Ziggurat => (0, 0, 0, 400, 80),
        _ => (0, 0, 0, 0, 0),
    };

    /// <summary>Cut stone (or quarried blocks, for the Palace before STN-11) a civic structure under way still needs.</summary>
    private int CivicCutStoneWant(SettlementState s)
    {
        int civicNeed = s.Civic is { BuildKind: not CivicBuild.None } c ? CivicMaterials(c.BuildKind).Cut : 0;
        if (s.BuildTarget == Structure.StoneWalls)
            return Math.Max(civicNeed, 60 * ((int)Tier(s.Id) + 1));
        return civicNeed;
    }

    /// <summary>Mudbrick the settlement keeps in store: 200 (docs/MVP/10), or what the civic structure under way needs.</summary>
    private Fix64 MudbrickTarget(SettlementState s)
    {
        int need = s.Civic is { BuildKind: not CivicBuild.None } c ? CivicMaterials(c.BuildKind).Mudbrick : 0;
        return Fix64.FromInt(Math.Max(200, need));
    }

    private Fix64 CutStoneHeld(SettlementState s, bool blocksToo)
    {
        if (s.Metal is not { } m) return Fix64.Zero;
        return m.Goods[(int)MetalGood.Ashlar] + (blocksToo ? m.Goods[(int)MetalGood.Block] : Fix64.Zero);
    }

    private bool CivicMaterialsReady(SettlementState s, CivicBuild b, bool check)
    {
        (int wood, int stone, int clay, int brick, int cut) = CivicMaterials(b);
        bool blocks = b == CivicBuild.Palace;
        if (!check && b == CivicBuild.Market && Population(s.Id) < 40) return false;
        bool ok = s.Stores[(int)Good.Wood] >= Fix64.FromInt(wood) && s.Stores[(int)Good.Stone] >= Fix64.FromInt(stone) &&
                  s.Stores[(int)Good.Clay] >= Fix64.FromInt(clay) && s.Stores[(int)Good.Mudbrick] >= Fix64.FromInt(brick) &&
                  CutStoneHeld(s, blocks) >= Fix64.FromInt(cut);
        if (!check || ok) return ok;
        // Planning: the materials can be had (gathered, chopped, made) even if they are not in store yet.
        int civ = s.Civilization;
        bool cutOk = cut == 0 || HasProject(civ, "STN-11") || (blocks && HasProject(civ, "MIN-03"));
        bool clayOk = clay == 0 || HasProject(civ, "CER-01") || HasProject(civ, "BLD-05");
        return (brick == 0 || HasProject(civ, "BLD-05")) && cutOk && clayOk;
    }

    private void PayCivic(SettlementState s, CivicBuild b)
    {
        (int wood, int stone, int clay, int brick, int cut) = CivicMaterials(b);
        s.Stores[(int)Good.Wood] -= Fix64.FromInt(wood);
        s.Stores[(int)Good.Stone] -= Fix64.FromInt(stone);
        s.Stores[(int)Good.Clay] -= Fix64.FromInt(clay);
        s.Stores[(int)Good.Mudbrick] -= Fix64.FromInt(brick);
        if (cut > 0 && s.Metal is { } m)
        {
            Fix64 left = Fix64.FromInt(cut);
            Fix64 a = Fix64.Min(left, m.Goods[(int)MetalGood.Ashlar]);
            m.Goods[(int)MetalGood.Ashlar] -= a;
            left -= a;
            if (left > Fix64.Zero) m.Goods[(int)MetalGood.Block] -= Fix64.Min(left, m.Goods[(int)MetalGood.Block]);
        }
    }

    /// <summary>Spends the season's civic labor in the planned shares: canals (1 ws a field tile), a Market town's dwellings, the structure.</summary>
    private void WorkCivic(SettlementState s, Fix64 labor, Fix64 canalWant, Fix64 housingWant)
    {
        if (s.Civic is not { } c || labor <= Fix64.Zero) return;
        // A Corvée: building labor there counts ×1.5 for its two seasons (docs/MVP/22 §4.3; walls a third sooner, §7.2).
        Fix64 corvee = CorveeLaborMult(s);
        if (corvee != Fix64.One) labor *= corvee;
        c.TraceLabor = labor;
        Fix64 canalLabor = _civic.CanalLabor;
        // The Market first while it is being raised; then a Market town's dwellings (07d R2: it builds housing ahead), canals
        // (food) and the structure under way.
        Fix64 marketFirst = c.BuildKind == CivicBuild.Market
            ? Fix64.Min(labor, Fix64.Max(Fix64.Zero, CivicLabor(s, c.BuildKind) - c.BuildProgress) + CivicGatherLabor(s, c.BuildKind)) : Fix64.Zero;
        labor -= marketFirst;
        Fix64 housingPool = Fix64.Min(labor, housingWant);
        labor -= housingPool;
        Fix64 canalPool = Fix64.Min(labor, canalWant) + c.CanalProgress;
        labor -= Fix64.Min(labor, canalWant);
        while (canalPool >= canalLabor && canalLabor > Fix64.Zero && NextCanalTile(s) is int tile and >= 0)
        {
            c.Canals.Add(tile);
            canalPool -= canalLabor;
        }
        c.CanalProgress = NextCanalTile(s) >= 0 ? Fix64.Min(canalPool, canalLabor) : Fix64.Zero;
        if (NextCanalTile(s) < 0) labor += canalPool;
        labor += WorkHousing(s, c, housingPool) + marketFirst;
        if (c.BuildKind == CivicBuild.None) return;
        labor = GatherCivicMaterials(s, c.BuildKind, labor);
        c.BuildProgress = Fix64.Min(CivicLabor(s, c.BuildKind), c.BuildProgress + labor);
        if (c.BuildProgress < CivicLabor(s, c.BuildKind) || !CivicMaterialsReady(s, c.BuildKind, check: false)) return;
        PayCivic(s, c.BuildKind);
        AddBuilt(c, c.BuildKind);
        if (c.BuildKind == CivicBuild.Ziggurat) _wondersBuilt.Add((s.Civilization, "BLD-08"));
        c.BuildKind = CivicBuild.None;
        c.BuildProgress = Fix64.Zero;
    }

    /// <summary>Labor the builders spend fetching a structure's wood, stone and clay themselves (bricks and cut stone are made by the crafts).</summary>
    private Fix64 CivicGatherLabor(SettlementState s, CivicBuild b)
    {
        (int wood, int stone, int clay, int brick, _) = CivicMaterials(b);
        Fix64 bricksMissing = HasProject(s.Civilization, "BLD-05")
            ? Fix64.Max(Fix64.Zero, Fix64.FromInt(brick) - s.Stores[(int)Good.Mudbrick] - s.DryingMudbrick) : Fix64.Zero;
        Fix64 brickRate = ClayRate(s, out _);
        Fix64 labor = Fix64.Max(Fix64.Zero, Fix64.FromInt(wood) - s.Stores[(int)Good.Wood]) / CivicChopRate;
        labor += Fix64.Max(Fix64.Zero, Fix64.FromInt(stone) - s.Stores[(int)Good.Stone]);
        Fix64 clayRate = ClayRate(s, out _);
        if (clayRate > Fix64.Zero) labor += Fix64.Max(Fix64.Zero, Fix64.FromInt(clay) - s.Stores[(int)Good.Clay]) / clayRate;
        if (bricksMissing > Fix64.Zero && brickRate > Fix64.Zero) labor += bricksMissing / 10 * (Fix64.One / brickRate + Fix64.FromRatio(3, 10));
        return labor;
    }

    /// <summary>Litres of timber a builder fells a work-season for a civic structure (the polished-axe rate, docs/MVP/23 §6.2).</summary>
    private static readonly Fix64 CivicChopRate = Fix64.FromInt(8);

    /// <summary>
    /// The builders fetch what the structure still lacks of wood (felled in the range, as for a mine head), stone (from the range's
    /// stone) and clay (dug where it can be, WP6's rule); returns the labor left for building.
    /// </summary>
    private Fix64 GatherCivicMaterials(SettlementState s, CivicBuild b, Fix64 labor)
    {
        (int wood, int stone, int clay, int brick, _) = CivicMaterials(b);
        labor = MakeCivicBricks(s, brick, labor);
        return GatherMaterials(s, wood, stone, clay, labor);
    }

    /// <summary>
    /// The builders make the bricks a structure still lacks (BLD-05: 1 clay and 1 straw make 10 bricks in 0.3 ws; they dry a
    /// season), digging the clay where there is no store of it. Returns the labor left.
    /// </summary>
    private Fix64 MakeCivicBricks(SettlementState s, int bricks, Fix64 labor)
    {
        if (bricks <= 0 || labor <= Fix64.Zero || !HasProject(s.Civilization, "BLD-05")) return labor;
        Fix64 missing = Fix64.FromInt(bricks) - s.Stores[(int)Good.Mudbrick] - s.DryingMudbrick;
        if (missing <= Fix64.Zero) return labor;
        Fix64 rate = ClayRate(s, out bool deposit);
        Fix64 batches = Fix64.Min(missing / 10, s.Stores[(int)Good.Straw]);
        // Each batch costs its moulding (0.3 ws) and, beyond the clay in store, its digging.
        Fix64 clayHeld = s.Stores[(int)Good.Clay];
        Fix64 perBatchDig = rate > Fix64.Zero ? Fix64.One / rate : Fix64.Zero;
        Fix64 made = Fix64.Zero;
        Fix64 fromStore = Fix64.Min(batches, Fix64.Min(clayHeld, labor / Fix64.FromRatio(3, 10)));
        made += fromStore;
        s.Stores[(int)Good.Clay] -= fromStore;
        labor -= fromStore * Fix64.FromRatio(3, 10);
        if (perBatchDig > Fix64.Zero && labor > Fix64.Zero && made < batches)
        {
            Fix64 dug = Fix64.Min(batches - made, labor / (perBatchDig + Fix64.FromRatio(3, 10)));
            if (deposit)
            {
                Fix64 left = dug;
                foreach (int tile in RangeTiles(s.SiteTile))
                {
                    Fix64 take = Fix64.Min(left, _clayStock[tile]);
                    _clayStock[tile] -= take;
                    left -= take;
                    if (left <= Fix64.Zero) break;
                }
                dug -= left;
            }
            made += dug;
            labor -= dug * (perBatchDig + Fix64.FromRatio(3, 10));
        }
        s.Stores[(int)Good.Straw] -= made;
        s.DryingMudbrick += made * 10;
        return Fix64.Max(Fix64.Zero, labor);
    }

    private Fix64 GatherMaterials(SettlementState s, int wood, int stone, int clay, Fix64 labor)
    {
        Fix64 missingWood = Fix64.Max(Fix64.Zero, Fix64.FromInt(wood) - s.Stores[(int)Good.Wood]);
        if (missingWood > Fix64.Zero && labor > Fix64.Zero)
        {
            // Timber from the range's forest stands, then any wooded ground in range, then stands up to 8 tiles out, hauled home.
            Fix64 got = Fix64.Zero;
            for (int pass = 0; pass < 3 && got < missingWood && labor > Fix64.Zero; pass++)
                foreach (int tile in pass < 2 ? RangeTiles(s.SiteTile) : ReachTiles(s.SiteTile, 8))
                {
                    if (got >= missingWood || labor <= Fix64.Zero) break;
                    if (pass == 0 && !IsForestTile(tile) || pass == 1 && IsForestTile(tile) || IsRivalTile(s.Civilization, tile)) continue;
                    int d = HexGrid.Distance(s.SiteTile, tile, _world.Width);
                    if (pass == 2 && d <= Radius) continue;
                    Fix64 perLitre = Fix64.One / CivicChopRate + (pass == 2 ? HaulPerLitreTile * d : Fix64.Zero);
                    Fix64 take = Fix64.Min(Fix64.Min(missingWood - got, _woodStock[tile]), labor / perLitre);
                    if (take <= Fix64.Zero) continue;
                    _woodStock[tile] -= take;
                    got += take;
                    labor -= take * perLitre;
                }
            s.Stores[(int)Good.Wood] += got;
        }
        Fix64 missingStone = Fix64.Max(Fix64.Zero, Fix64.FromInt(stone) - s.Stores[(int)Good.Stone]);
        if (missingStone > Fix64.Zero && labor > Fix64.Zero)
        {
            // From the range first, then up to 8 tiles out, hauled home (0.1 ws per L per 4 tiles, docs/MVP/22 §5.2).
            Fix64 got = Fix64.Zero;
            foreach (int tile in ReachTiles(s.SiteTile, 8))
            {
                if (got >= missingStone || labor <= Fix64.Zero) break;
                if (_stoneStock[tile] <= Fix64.Zero || IsRivalTile(s.Civilization, tile)) continue;
                Fix64 perLitre = Fix64.One + HaulPerLitreTile * HexGrid.Distance(s.SiteTile, tile, _world.Width);
                Fix64 take = Fix64.Min(Fix64.Min(missingStone - got, _stoneStock[tile]), labor / perLitre);
                _stoneStock[tile] -= take;
                got += take;
                labor -= take * perLitre;
            }
            s.Stores[(int)Good.Stone] += got;
        }
        Fix64 missingClay = Fix64.Max(Fix64.Zero, Fix64.FromInt(clay) - s.Stores[(int)Good.Clay]);
        Fix64 rate = ClayRate(s, out bool deposit);
        if (missingClay > Fix64.Zero && labor > Fix64.Zero && rate > Fix64.Zero)
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
            s.Stores[(int)Good.Clay] += got;
            labor -= got / rate;
        }
        return Fix64.Max(Fix64.Zero, labor);
    }

    // ---- housing and the Market town rule (07d R2) ---------------------------------------------------------

    /// <summary>
    /// The housing the planner builds toward: just in time (people + 8) in villages; in a settlement with a Market, ahead to
    /// 0.85 × min(FoodCap, GroupCap) + 8 (07d R2: live villages are housing-bound; a Market town is food-bound).
    /// </summary>
    private Fix64 HousingTarget(SettlementState s)
    {
        Fix64 target = Fix64.FromInt(Population(s.Id) + 8);
        if (IsMarketTown(s))
            target = Fix64.Max(target, Fix64.FromRatio(85, 100) * Fix64.Min(FoodCap(s), GroupCap(s)) + Fix64.FromInt(8));
        return target;
    }

    /// <summary>A settlement with a Market, or raising one: from the day its Market is chosen it keeps its people (07d R2).</summary>
    private bool IsMarketTown(SettlementState s) => s.Civic is { } c && (c.Markets > 0 || c.BuildKind == CivicBuild.Market) && CohesionLive(s);

    /// <summary>Halls (30 each) and, in Towns and Cities with SOC-09, mudbrick houses and halls ×1.6 (dense dwellings).</summary>
    private Fix64 CivicHousing(SettlementState s, Fix64 e2Capacity)
    {
        if (s.Civic is not { } c || !CivInBronze(s.Civilization)) return e2Capacity;
        Fix64 halls = Fix64.FromInt(30 * c.Halls);
        if (c.Tier >= 1 && HasProject(s.Civilization, "SOC-09"))
        {
            Fix64 dense = Fix64.FromInt(8 * s.Structures[(int)Structure.MudbrickHouse]) + halls;
            return e2Capacity + halls + dense * Fix64.FromRatio(6, 10);
        }
        return e2Capacity + halls;
    }

    // ---- tiers, Cohesion and secession at the season end ---------------------------------------------------

    private void CivicSeasonEnd(int season)
    {
        bool yearEnd = season % 4 == 3;
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState s = _settlements[i];
            if (s.Civic is not { } c) continue;
            int pop = Population(s.Id);
            UpdateTier(s, c, pop);
            if (yearEnd)
            {
                c.DeathsLastYear = c.DeathsThisYear;
                c.PeopleLastYear = pop + c.DeathsThisYear;
                c.DeathsThisYear = 0;
            }
            if (!CohesionLive(s)) continue;
            int target = CohesionTarget(s, c, pop);
            c.Target = target;
            int now = CohesionPoints(s.Cohesion);
            int diff = target - now;
            // A quarter of the way each season, in whole points (docs/MVP/22 §8.3); at least one point while it differs.
            int step = (Fix64.FromInt(diff) * _civic.Step).FloorToInt();
            if (diff < 0) step = -((Fix64.FromInt(-diff) * _civic.Step).FloorToInt());
            if (step == 0 && diff != 0) step = Math.Sign(diff);
            now = Math.Clamp(now + step, 0, 100);
            s.Cohesion = CohesionFactor(now);
            if (c.ConqueredTerm < 0) c.ConqueredTerm = Math.Min(0, c.ConqueredTerm + _civic.ConqueredRecovery);
            c.LowSeasons = now < _civic.SecessionBelow ? (byte)Math.Min(255, c.LowSeasons + 1) : (byte)0;
            if (c.LowSeasons >= _civic.SecessionSeasons && CapitalOf(s.Civilization) != s.Id) Secede(s, c);
        }
    }

    private void UpdateTier(SettlementState s, CivicState c, int pop)
    {
        int civ = s.Civilization;
        bool town = Done(civ, "TRD-04") && c.Markets > 0;
        bool city = Done(civ, "SOC-09") && (c.Temples > 0 || c.Palaces > 0);
        byte earned = city && pop >= _civic.CityMinPop ? (byte)2 : town && pop >= _civic.TownMinPop ? (byte)1 : (byte)0;
        if (earned >= c.Tier) { c.Tier = earned; c.DemoteSeasons = 0; return; }
        // Hysteresis (docs/MVP/23 §1.2): the tier holds until 4 seasons in a row below 0.85 × min_pop or without the structure.
        int minPop = c.Tier == 2 ? _civic.CityMinPop : _civic.TownMinPop;
        bool structure = c.Tier == 2 ? city : town;
        bool holds = structure && Fix64.FromInt(pop) >= _civic.DemoteShare * minPop;
        if (holds) { c.DemoteSeasons = 0; return; }
        c.DemoteSeasons++;
        if (c.DemoteSeasons >= _civic.DemoteSeasons) { c.Tier--; c.DemoteSeasons = 0; }
    }

    /// <summary>
    /// The Cohesion target of docs/MVP/22 §8.3 with 07d R7's captured-town rule: base 60, the named E0–E2 terms (hearth,
    /// feast, shrine, GOV-03) unless conquered, luxuries, every E3 `cohesion` effect in its scope, the Megalith, and the
    /// strains: last winter's hunger, deaths, raids, crowding, Overreach and the Admin span.
    /// </summary>
    private int CohesionTarget(SettlementState s, CivicState c, int pop)
    {
        int civ = s.Civilization;
        bool conquered = c.ConqueredTerm < 0;
        int capital = CapitalOf(civ);
        SettlementState? cap = capital >= 0 ? _settlements[capital] : null;
        Fix64 t = Fix64.FromInt(_civic.Base);
        if (!conquered)
        {
            if (s.Structures[(int)Structure.Hearth] > 0) t += Fix64.FromInt(5);
            if (Done(civ, "SOC-05")) t += Fix64.FromInt(8);
            if (Done(civ, "BEL-05") && cap is not null && HexGrid.Distance(cap.SiteTile, s.SiteTile, _world.Width) <= 20) t += Fix64.FromInt(3);
            if (Done(civ, "GOV-03")) t += Fix64.FromInt(3);
        }
        t += Fix64.FromInt(Math.Min(5, LuxuryItems(s) * 10 / Math.Max(1, pop)));
        Fix64 wonderMult = Modifier(civ, "wonder_cohesion"), radiusMult = Modifier(civ, "wonder_radius");
        foreach (CohesionTerm term in _cohesionTerms)
        {
            if (!_completedProjects[civ][term.Project]) continue;
            if (term.Wonder && !CivHasZiggurat(civ)) continue;
            Fix64 value = Fix64.FromInt(term.Value) * (term.Wonder && term.Scope == "radius" ? wonderMult : Fix64.One);
            int radius = term.Wonder ? (Fix64.FromInt(term.Radius) * radiusMult).RoundToInt() : term.Radius;
            bool applies = term.Scope switch
            {
                "here" => Count(c, term.Structure) > 0,
                // A Wonder's ring includes its own settlement; a Temple's +2 is for its neighbours (it gives +6 "here").
                "radius" => term.Wonder ? Count(c, term.Structure) > 0 || WithinOfStructure(s, term.Structure, radius)
                                        : Count(c, term.Structure) == 0 && WithinOfStructure(s, term.Structure, radius),
                "capital" => capital == s.Id,
                _ => true,
            };
            if (applies) t += value;
        }
        // The Megalith (STN-10, an E2 Wonder named in the target): +10 within 8 walking tiles of the civilization's oldest settlement.
        if (Done(civ, "STN-10") && OldestOf(civ) is int oldest and >= 0)
        {
            int radius = (Fix64.FromInt(8) * radiusMult).RoundToInt();
            if (WalkingTiles(_settlements[oldest].SiteTile, s.SiteTile) <= radius) t += Fix64.FromInt(10) * wonderMult;
        }
        t += _civic.Famine * Fix64.Max(Fix64.Zero, Fix64.One - s.LateWinterFoodShare);
        if (c.PeopleLastYear > 0)
        {
            int lostPerMille = c.DeathsLastYear * 1000 / c.PeopleLastYear;
            if (lostPerMille > 30) t += Fix64.FromInt(_civic.LossPer2Pct * ((lostPerMille - 30) / 20));
        }
        if (checked((int)_seasonsCompleted) - c.RaidedSeason < 4 * _civic.RaidYears)
            t += Fix64.FromInt(Done(civ, "BEL-01") ? _civic.RaidedBel01 : _civic.Raided);
        Fix64 housing = HousingCap(s);
        if (housing > Fix64.Zero) t += _civic.Crowding * Fix64.Max(Fix64.Zero, Fix64.FromInt(pop) / housing - Fix64.One);
        if (cap is not null && capital != s.Id)
        {
            int walk = WalkingTiles(cap.SiteTile, s.SiteTile);
            Fix64 over = Fix64.FromInt(Math.Max(0, walk - _civic.OverreachFree)) / _civic.OverreachDivisor * (Fix64.One - AdminReductionOf(civ));
            if (Done(civ, "HUS-08") && MostlyOpenGround(s)) over *= Fix64.Half;
            t -= over;
            t -= Fix64.FromInt(_civic.SpanPenalty * Math.Max(0, MemberCount(civ) - AdminSpanOf(civ)));
        }
        t += Fix64.FromInt(c.ConqueredTerm);
        return Math.Clamp(t.RoundToInt(), 0, 100);
    }

    private int OldestOf(int civ)
    {
        for (int i = 0; i < _settlements.Count; i++) if (IsMember(_settlements[i], civ)) return i;
        return -1;
    }

    private bool WithinOfStructure(SettlementState s, CivicBuild b, int radius)
    {
        foreach (SettlementState o in _settlements)
            if (o != s && o.Civilization == s.Civilization && o.Civic is { } oc && Count(oc, b) > 0 &&
                WalkingTiles(o.SiteTile, s.SiteTile) <= radius) return true;
        return false;
    }

    private bool MostlyOpenGround(SettlementState s)
    {
        int open = 0, all = 0;
        foreach (int tile in RangeTiles(s.SiteTile))
        {
            all++;
            if ((Biome)_world.Tile(tile).BiomeId is Biome.GrasslandSteppe or Biome.Savanna) open++;
        }
        return open * 2 >= all;
    }

    /// <summary>Luxury items in store (docs/MVP/23 §3: 1 item per 0.1 L of gold, ×2 with MET-07's weight).</summary>
    private int LuxuryItems(SettlementState s)
    {
        if (s.Metal is not { } m) return 0;
        Fix64 items = m.Goods[(int)MetalGood.FineBronze] + m.Goods[(int)MetalGood.Faience] + m.Goods[(int)MetalGood.Glass] +
                      m.Goods[(int)MetalGood.DyedCloth] + m.Goods[(int)MetalGood.Silk];
        Fix64 goldWeight = HasProject(s.Civilization, "MET-07") ? Fix64.Max(Fix64.One, Modifier(s.Civilization, "gold_luxury_weight")) : Fix64.One;
        items += m.Goods[(int)MetalGood.Gold] * 10 * goldWeight;
        return items.FloorToInt();
    }

    /// <summary>
    /// Secession (docs/MVP/22 §8.3): the settlement leaves to become Free Folk, keeping its people, land and stores. The economy
    /// takes it out of its civilization's polity (span, capital, Cohesion); politics marks it Free Folk when it drains the hook.
    /// </summary>
    private void Secede(SettlementState s, CivicState c)
    {
        c.Seceded = true;
        c.LowSeasons = 0;
        c.Tier = 0;
        foreach (FigureState f in _figures) if (f.SettlementId == s.Id) { f.Role = Role.None; f.Camp = -1; }
        for (int i = _camps.Count - 1; i >= 0; i--) if (_camps[i].Parent == s.Id) _camps.RemoveAt(i);
        _secessions.Add(s.Id);
    }

    /// <summary>Settlements that seceded since the last call (the politics hook: WP8 makes them Free Folk with claims and relations).</summary>
    public void DrainSecessions(List<int> into)
    {
        into.AddRange(_secessions);
        _secessions.Clear();
    }

    /// <summary>Wonders finished since the last call, as (civilization, project id), for research's Wonder Legacy (docs/MVP/22 §3.3).</summary>
    public void DrainWondersBuilt(List<(int Civ, string Project)> into)
    {
        into.AddRange(_wondersBuilt);
        _wondersBuilt.Clear();
    }

    // ---- hooks for politics --------------------------------------------------------------------------------

    /// <summary>Whether a settlement's Cohesion is target-seeking (its civilization past KEY-3; not seceded or Free Folk).</summary>
    public bool IsCohesionLive(int settlementIndex) => CohesionLive(_settlements[settlementIndex]);

    /// <summary>A raid struck the settlement (docs/MVP/22 §8.3: −10 to the target for two years, −5 with BEL-01).</summary>
    public void NoteRaided(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        if (s.Civic is { } c) c.RaidedSeason = checked((int)_seasonsCompleted);
    }

    /// <summary>
    /// The settlement was captured by its present civilization (07d R7): when the captor's Cohesion is live it starts at C 25 and
    /// carries a conquered term of −35 that recovers 1 a season, without the hearth, feast, shrine and GOV-03 terms while it
    /// lasts. Returns false (and changes nothing) when Cohesion is not live; politics then applies the E2 debt (13 §8.3).
    /// </summary>
    public bool NoteCaptured(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        if (!CivInBronze(s.Civilization) || IsFreeFolk(s)) return false;
        CivicState c = EnsureCivic(s);
        c.Seceded = false;
        c.ConqueredTerm = _civic.Conquered;
        c.LowSeasons = 0;
        c.Tier = Math.Min(c.Tier, (byte)1);
        s.Cohesion = CohesionFactor(_civic.ConqueredStart);
        foreach (FigureState f in _figures) if (f.SettlementId == s.Id) f.Role = Role.None;
        return true;
    }

    /// <summary>The claim radius of a settlement (docs/MVP/22 §8.1, §11): Town 5, City 6; <paramref name="eraRadius"/> otherwise.</summary>
    public int ClaimRadius(int settlementIndex, int eraRadius)
    {
        SettlementState s = _settlements[settlementIndex];
        if (s.Civic is not { } c || !CohesionLive(s)) return eraRadius;
        return c.Tier switch { 2 => Math.Max(eraRadius, _cityRadius), 1 => Math.Max(eraRadius, _townRadius), _ => eraRadius };
    }

    /// <summary>The claim multiplier within the settlement's radius: ×1.25 at a capital with a Palace (GOV-05), else 1.</summary>
    public Fix64 ClaimMultiplier(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        if (s.Civic is not { Palaces: > 0 } || !CohesionLive(s) || !HasProject(s.Civilization, "GOV-05")) return Fix64.One;
        return _capitalClaimMult;
    }

    /// <summary>The hinterland ring of a City with TER-07 (radius 7 at 50% emission), as (radius, emission share); (0, 0) otherwise.</summary>
    public (int Radius, Fix64 Share) Hinterland(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        if (s.Civic is not { Tier: 2 } || !CohesionLive(s) || !HasProject(s.Civilization, "TER-07")) return (0, Fix64.Zero);
        return (_civic.HinterlandRadius, _civic.HinterlandEmit);
    }

    public SettlementTier Tier(int settlementIndex) => (SettlementTier)(_settlements[settlementIndex].Civic?.Tier ?? 0);

    /// <summary>The capital's settlement index of a civilization, or -1 (docs/MVP/23 §9 C13).</summary>
    public int Capital(int civ) => CapitalOf(civ);

    public int AdminSpan(int civ) => AdminSpanOf(civ);

    /// <summary>The Cohesion target penalties from distance to the capital and settlements beyond Admin span (docs/MVP/22 §8.3).</summary>
    public (int Overreach, int SpanPenalty) CohesionPressures(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        if (!CohesionLive(s)) return (0, 0);
        int civ = s.Civilization, capital = CapitalOf(s.Civilization);
        if (capital < 0 || capital == s.Id) return (0, 0);
        int walk = WalkingTiles(_settlements[capital].SiteTile, s.SiteTile);
        Fix64 overreach = Fix64.FromInt(Math.Max(0, walk - _civic.OverreachFree)) /
            _civic.OverreachDivisor * (Fix64.One - AdminReductionOf(civ));
        if (Done(civ, "HUS-08") && MostlyOpenGround(s)) overreach *= Fix64.Half;
        int spanPenalty = _civic.SpanPenalty * Math.Max(0, MemberCount(civ) - AdminSpanOf(civ));
        return (Math.Max(0, overreach.RoundToInt()), spanPenalty);
    }

    /// <summary>Priests and Scribes staffed at a settlement this season (research's E3 role Insight, docs/MVP/22 §8.4).</summary>
    public (int Priests, int Scribes) RoleHeads(int settlementIndex)
    {
        int id = _settlements[settlementIndex].Id, priests = 0, scribes = 0;
        foreach (FigureState f in _figures)
        {
            if (f.SettlementId != id) continue;
            if (f.Role == Role.Priest) priests++;
            else if (f.Role == Role.Scribe) scribes++;
        }
        return (priests, scribes);
    }

    /// <summary>A one-line description of a settlement's early state, for harness traces only.</summary>
    internal string DescribeCivic(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        var t = new System.Text.StringBuilder();
        int pop = Population(s.Id);
        t.Append($"s{settlementIndex} pop {pop} food {FoodCap(s).ToDoubleForDisplay():F0} house {HousingCap(s).ToDoubleForDisplay():F0} group {GroupCap(s).ToDoubleForDisplay():F0} C {CohesionPoints(s.Cohesion)}");
        t.Append($" fields {s.FieldTiles.Count}/{FieldLimit(s)} past {s.Structures[(int)Structure.Pasture]} cattle {s.DomesticCattle.ToDoubleForDisplay():F1} sheep {s.DomesticSmallStock.ToDoubleForDisplay():F1} brick {s.Stores[(int)Good.Mudbrick].ToDoubleForDisplay():F0} wood {s.Stores[(int)Good.Wood].ToDoubleForDisplay():F0} e2build {s.BuildTarget}:{s.BuildProgress.ToDoubleForDisplay():F1} clay {s.Stores[(int)Good.Clay].ToDoubleForDisplay():F1} straw {s.Stores[(int)Good.Straw].ToDoubleForDisplay():F0} stone {s.Stores[(int)Good.Stone].ToDoubleForDisplay():F0} bld05 {HasProject(s.Civilization, "BLD-05")} stn11 {HasProject(s.Civilization, "STN-11")} ashlar {(s.Metal?.Goods[(int)MetalGood.Ashlar] ?? Fix64.Zero).ToDoubleForDisplay():F1} wattle {s.Structures[(int)Structure.WattleHouse]} mud {s.Structures[(int)Structure.MudbrickHouse]} long {s.Structures[(int)Structure.Longhouse]}");
        Fix64 soil = Fix64.Zero;
        foreach (int tile in s.FieldTiles) if (_world.Tile(tile).SoilFertility > Fix64.Zero) soil += _soil[tile] / _world.Tile(tile).SoilFertility;
        if (s.FieldTiles.Count > 0) t.Append($" soil {(soil / s.FieldTiles.Count).ToDoubleForDisplay():F2}");
        if (s.Civic is { } c)
            t.Append($" | tier {c.Tier} mkt {c.Markets} tmp {c.Temples} pal {c.Palaces} sch {c.Schools} bak {c.Bakeries} hall {c.Halls} zig {c.Ziggurats} canals {c.Canals.Count} build {c.BuildKind}:{c.BuildProgress.ToDoubleForDisplay():F1} target {c.Target} plan {c.TracePlan.ToDoubleForDisplay():F1} housewant {c.TraceHousingWant.ToDoubleForDisplay():F1} labor {c.TraceLabor.ToDoubleForDisplay():F2} dwell {(Structure)c.Dwelling}:{c.DwellingProgress.ToDoubleForDisplay():F1} horses {c.Horses} outbreak {c.Outbreak} san {c.Sanitation.ToDoubleForDisplay():F2} fvm {c.FoodValueMult.ToDoubleForDisplay():F3}");
        return t.ToString();
    }

    public CivicReadout Civic(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        (int priests, int scribes) = RoleHeads(settlementIndex);
        int c0 = CohesionPoints(s.Cohesion);
        if (s.Civic is not { } c)
            return new CivicReadout(SettlementTier.Village, false, false, c0, -1, 0, 0, 0, 0, 0, 0, 0, 0, 0, priests, scribes, 0, 0, 0,
                false, 0, 0, 0, Fix64.Zero, 0, Fix64.One, 0);
        bool live = CohesionLive(s);
        return new CivicReadout((SettlementTier)c.Tier, live && CapitalOf(s.Civilization) == s.Id, live, c0, c.Target, c.Markets,
            c.Temples, c.Palaces, c.Schools, c.Bakeries, c.Halls, c.Caravanserais, c.Ziggurats, c.Canals.Count, priests, scribes,
            s.Metal?.Forges ?? 0, c.Markets, c.Bakeries, c.Seceded, c.LowSeasons, c.ConqueredTerm, c.Outbreak, c.Sanitation,
            ClaimRadius(settlementIndex, 4), ClaimMultiplier(settlementIndex), c.Horses);
    }

    // ---- hashing and snapshots -----------------------------------------------------------------------------

    private void HashCivic(ref StateHasher hash)
    {
        hash.Add(0x434956494353UL); // "CIVICS"
        hash.Add(_secessions.Count); foreach (int s in _secessions) hash.Add(s);
        hash.Add(_wondersBuilt.Count); foreach ((int civ, string p) in _wondersBuilt) { hash.Add(civ); hash.Add(Hash64.OfString(p)); }
        for (int i = 0; i < _settlements.Count; i++)
        {
            if (_settlements[i].Civic is not { } c) continue;
            hash.Add(i);
            hash.Add(c.Markets); hash.Add(c.Temples); hash.Add(c.Palaces); hash.Add(c.Schools); hash.Add(c.Bakeries);
            hash.Add(c.Halls); hash.Add(c.Caravanserais); hash.Add(c.Ziggurats); hash.Add((byte)c.BuildKind); hash.Add(c.BuildProgress);
            hash.Add(c.Canals.Count); foreach (int t in c.Canals) hash.Add(t);
            hash.Add(c.CanalProgress); hash.Add(c.Dwelling); hash.Add(c.DwellingProgress);
            hash.Add(c.Tier); hash.Add(c.DemoteSeasons); hash.Add(c.WorkMult); hash.Add(c.FoodValueMult); hash.Add(c.BakeryShare);
            hash.Add(c.LowSeasons); hash.Add(c.Seceded); hash.Add(c.RaidedSeason); hash.Add(c.ConqueredTerm);
            hash.Add(c.DeathsThisYear); hash.Add(c.DeathsLastYear); hash.Add(c.PeopleLastYear); hash.Add(c.Target); hash.Add(c.Horses);
            hash.Add(c.Outbreak); hash.Add(c.OutbreakSeason); hash.Add(c.AttackRate); hash.Add(c.CfrScale); hash.Add(c.Immune);
            foreach (byte b in c.ImmuneSeasons) hash.Add(b);
            hash.Add(c.ImportedOutbreak); hash.Add(c.Sanitation);
        }
    }

    private void WriteCivic(BinaryWriter w)
    {
        w.Write(_secessions.Count); foreach (int s in _secessions) w.Write(s);
        w.Write(_wondersBuilt.Count); foreach ((int civ, string p) in _wondersBuilt) { w.Write(civ); w.Write(p); }
        for (int i = 0; i < _settlements.Count; i++)
        {
            CivicState? c = _settlements[i].Civic;
            w.Write(c is not null);
            if (c is null) continue;
            w.Write(c.Markets); w.Write(c.Temples); w.Write(c.Palaces); w.Write(c.Schools); w.Write(c.Bakeries);
            w.Write(c.Halls); w.Write(c.Caravanserais); w.Write(c.Ziggurats); w.Write((byte)c.BuildKind); w.Write(c.BuildProgress.Raw);
            w.Write(c.Canals.Count); foreach (int t in c.Canals) w.Write(t);
            w.Write(c.CanalProgress.Raw); w.Write(c.Dwelling); w.Write(c.DwellingProgress.Raw);
            w.Write(c.Tier); w.Write(c.DemoteSeasons); w.Write(c.WorkMult.Raw); w.Write(c.FoodValueMult.Raw); w.Write(c.BakeryShare.Raw);
            w.Write(c.LowSeasons); w.Write(c.Seceded); w.Write(c.RaidedSeason); w.Write(c.ConqueredTerm);
            w.Write(c.DeathsThisYear); w.Write(c.DeathsLastYear); w.Write(c.PeopleLastYear); w.Write(c.Target); w.Write(c.Horses);
            w.Write(c.Outbreak); w.Write(c.OutbreakSeason); w.Write(c.AttackRate.Raw); w.Write(c.CfrScale.Raw);
            for (int k = 0; k < OutbreakClasses; k++) { w.Write(c.Immune[k].Raw); w.Write(c.ImmuneSeasons[k]); }
            w.Write(c.ImportedOutbreak); w.Write(c.Sanitation.Raw);
        }
    }

    private void ReadCivic(BinaryReader r)
    {
        _secessions.Clear();
        int n = ReadCount(r, 100_000);
        for (int i = 0; i < n; i++) _secessions.Add(r.ReadInt32());
        _wondersBuilt.Clear();
        n = ReadCount(r, 1_000);
        for (int i = 0; i < n; i++) _wondersBuilt.Add((r.ReadInt32(), r.ReadString()));
        for (int i = 0; i < _settlements.Count; i++)
        {
            if (!r.ReadBoolean()) { _settlements[i].Civic = null; continue; }
            var c = new CivicState
            {
                Markets = r.ReadInt32(), Temples = r.ReadInt32(), Palaces = r.ReadInt32(), Schools = r.ReadInt32(), Bakeries = r.ReadInt32(),
                Halls = r.ReadInt32(), Caravanserais = r.ReadInt32(), Ziggurats = r.ReadInt32(), BuildKind = (CivicBuild)r.ReadByte(),
                BuildProgress = Fix64.FromRaw(r.ReadInt64()),
            };
            int canals = ReadCount(r, _world.TileCount);
            for (int k = 0; k < canals; k++) c.Canals.Add(r.ReadInt32());
            c.CanalProgress = Fix64.FromRaw(r.ReadInt64()); c.Dwelling = r.ReadByte(); c.DwellingProgress = Fix64.FromRaw(r.ReadInt64());
            c.Tier = r.ReadByte(); c.DemoteSeasons = r.ReadByte(); c.WorkMult = Fix64.FromRaw(r.ReadInt64());
            c.FoodValueMult = Fix64.FromRaw(r.ReadInt64()); c.BakeryShare = Fix64.FromRaw(r.ReadInt64());
            c.LowSeasons = r.ReadByte(); c.Seceded = r.ReadBoolean(); c.RaidedSeason = r.ReadInt32(); c.ConqueredTerm = r.ReadInt32();
            c.DeathsThisYear = r.ReadInt32(); c.DeathsLastYear = r.ReadInt32(); c.PeopleLastYear = r.ReadInt32(); c.Target = r.ReadInt32();
            c.Horses = r.ReadInt32();
            c.Outbreak = r.ReadByte(); c.OutbreakSeason = r.ReadByte(); c.AttackRate = Fix64.FromRaw(r.ReadInt64()); c.CfrScale = Fix64.FromRaw(r.ReadInt64());
            for (int k = 0; k < OutbreakClasses; k++) { c.Immune[k] = Fix64.FromRaw(r.ReadInt64()); c.ImmuneSeasons[k] = r.ReadByte(); }
            c.ImportedOutbreak = r.ReadByte(); c.Sanitation = Fix64.FromRaw(r.ReadInt64());
            _settlements[i].Civic = c;
        }
    }

    private void ValidateCivic()
    {
        foreach (int s in _secessions)
            if ((uint)s >= (uint)_settlements.Count) throw new InvalidDataException("Snapshot contains an invalid secession.");
        foreach (SettlementState s in _settlements)
        {
            if (s.Civic is not { } c) continue;
            if (c.Markets < 0 || c.Temples < 0 || c.Palaces < 0 || c.Schools < 0 || c.Bakeries < 0 || c.Halls < 0 || c.Caravanserais < 0 ||
                c.Ziggurats < 0 || c.Tier > 2 || (uint)c.BuildKind > (uint)CivicBuild.Ziggurat || c.BuildProgress < Fix64.Zero ||
                c.ConqueredTerm > 0 || c.Outbreak >= OutbreakClasses || c.Horses < 0 || c.WorkMult <= Fix64.Zero)
                throw new InvalidDataException("Snapshot contains invalid civic settlement state.");
            foreach (int t in c.Canals)
                if ((uint)t >= (uint)_world.TileCount) throw new InvalidDataException("Snapshot contains an invalid canal tile.");
        }
    }

    private static CivicState? CloneCivic(CivicState? source)
    {
        if (source is null) return null;
        var c = new CivicState
        {
            Markets = source.Markets, Temples = source.Temples, Palaces = source.Palaces, Schools = source.Schools,
            Bakeries = source.Bakeries, Halls = source.Halls, Caravanserais = source.Caravanserais, Ziggurats = source.Ziggurats,
            BuildKind = source.BuildKind, BuildProgress = source.BuildProgress, Tier = source.Tier, DemoteSeasons = source.DemoteSeasons,
            WorkMult = source.WorkMult, FoodValueMult = source.FoodValueMult, BakeryShare = source.BakeryShare,
            LowSeasons = source.LowSeasons, Seceded = source.Seceded, RaidedSeason = source.RaidedSeason,
            ConqueredTerm = source.ConqueredTerm, DeathsThisYear = source.DeathsThisYear, DeathsLastYear = source.DeathsLastYear,
            PeopleLastYear = source.PeopleLastYear, Target = source.Target, Horses = source.Horses, Outbreak = source.Outbreak,
            OutbreakSeason = source.OutbreakSeason, AttackRate = source.AttackRate, CfrScale = source.CfrScale, ImportedOutbreak = source.ImportedOutbreak,
            Sanitation = source.Sanitation,
        };
        c.Canals.AddRange(source.Canals);
        c.CanalProgress = source.CanalProgress; c.Dwelling = source.Dwelling; c.DwellingProgress = source.DwellingProgress;
        Array.Copy(source.Immune, c.Immune, c.Immune.Length);
        Array.Copy(source.ImmuneSeasons, c.ImmuneSeasons, c.ImmuneSeasons.Length);
        return c;
    }

    private void CopyCivicFrom(LivingEconomyModel source)
    {
        _secessions.Clear(); _secessions.AddRange(source._secessions);
        _wondersBuilt.Clear(); _wondersBuilt.AddRange(source._wondersBuilt);
    }
}
