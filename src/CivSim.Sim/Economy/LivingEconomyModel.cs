using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// Deterministic E0–E2 settlement simulation. M2 supplies the immutable generated map; this model owns
/// its changing resource stocks, people, stores, jobs, structures, seasonal hazards and fission.
/// </summary>
public sealed partial class LivingEconomyModel : ISimModel
{
    public const int SeasonTicks = 150;
    public const int SnapshotVersion = 24;
    private const int Radius = 3;
    /// <summary>The last era whose projects the economy applies (Bronze). A civilization past KEY-4 stays at era 3 here (docs/MVP/22 §3.1).</summary>
    private const int MaxEra = 3;

    private static readonly ulong ClimateStream = Hash64.OfString("economy.climate");
    private static readonly ulong BirthStream = Hash64.OfString("economy.birth");
    private static readonly ulong MortalityStream = Hash64.OfString("economy.mortality");
    private static readonly ulong DeathCauseStream = Hash64.OfString("economy.death-cause");
    private const int DeathCauseCount = (int)EconomyDeathCause.Senescence + 1;
    private static readonly ulong IllnessStream = Hash64.OfString("economy.illness");
    private static readonly ulong WoundStream = Hash64.OfString("economy.wound");
    private static readonly ulong HuntingWoundStream = Hash64.OfString("economy.hunting-wound");
    private static readonly ulong FissionStream = Hash64.OfString("economy.fission");
    private static readonly ulong DroughtStream = Hash64.OfString("economy.drought");
    private static readonly ulong FloodStream = Hash64.OfString("economy.flood");
    private static readonly ulong HerdCrashStream = Hash64.OfString("economy.herd-crash");
    private static readonly ulong HerdBehaviorStream = Hash64.OfString("economy.herd-behavior");
    private static readonly ulong WildfireStream = Hash64.OfString("economy.wildfire");
    private const int NaturalRegionSize = 8;
    private static readonly Fix64[] WinterMultipliers =
    {
        Fix64.FromInt(2), Fix64.Parse("1.7"), Fix64.Parse("1.4"), Fix64.Parse("1.25"),
        Fix64.Parse("1.2"), Fix64.Parse("1.15"), Fix64.Parse("1.1"), Fix64.Parse("1.08"),
        Fix64.Parse("1.05"), Fix64.Parse("1.05"),
    };
    private const int CauseCount = 8;
    // Provisional E0–E2 episode assumptions: two seasons untreated, 16% onset per available
    // season. Treated illness lasts one season (16% coverage gives MED-01's mean ×0.96);
    // two-season wounds reach about 28% coverage (MED-02's mean approximately ×0.92).
    // These are visible care episodes; the baseline hazards also include acute unobserved cases.
    private static readonly Fix64 HealthEpisodeChance = Fix64.Parse("0.16");
    private static readonly Fix64 CarePatientWork = Fix64.Parse("0.1");
    private const byte HealerRole = 1;
    private const byte MidwifeRole = 2;
    // §10.9 specifies the ten-tick loss condition, but no fuel or work rate. The
    // deterministic E0–E2 implementation uses 1 wood per season and 0.1 ws per keeper.
    private static readonly Fix64 FireFuelPerTick = Fix64.One / SeasonTicks;
    private static readonly Fix64 HearthFuelPerTick = Fix64.Parse("1.5") / SeasonTicks;
    private static readonly Fix64 WinterHearthFuelPerTick = Fix64.FromInt(3) / SeasonTicks;
    private static readonly Fix64 FireKeepingWork = Fix64.Parse("0.1");
    private static readonly Fix64 DaughterShare = Fix64.Parse("0.4");
    private static readonly Fix64 MinimumDaughterPopulation = Fix64.FromInt(6);
    private static readonly Fix64 SustainableFishTakeShare = Fix64.Parse("0.6");

    private enum Good : byte
    {
        Plant, Meat, Roots, Nuts, WildGrain, Grain, Fish, DriedFood,
        Wood, Stone, Flint, Clay, Fiber, Hide, Bone, Cord, Leather, Wrap, Tailored, ClothGarment,
        Stick, Club, Spear, Basket, Sickle, Tool1, Tool2, Tool3, Tool4, Tool5, Tool6, Tool7,
        Fruit, Straw, Sapling, Mudbrick, SpearThrower, Bow, Harpoon, FishingKit, Net,
        Count
    }

    private enum Structure : byte
    {
        Hearth, LeanTo, HideTent, PitHouse, WattleHouse, MudbrickHouse, Longhouse,
        StoragePit, DryingRack, Granary, Kiln, Quern, ThornBarrier, Lookout, Weir,
        Field, Pasture, Orchard, Hillfort, StoneWalls, Palisade, Count
    }

    private enum SkillFamily : byte { Forage, Hunt, Fish, Farm, Chop, Gather, Herd, Count }

    private const int MaxSkillLevel = 10;
    private static readonly Fix64 SkillWorkPerLevel = Fix64.FromInt(4);
    private static readonly Fix64 VeteranSkillWorkPerLevel = Fix64.FromInt(8);

    private readonly record struct HazardBreakdown(
        Fix64 Endemic, Fix64 Epidemic, Fix64 Starvation, Fix64 Exposure,
        Fix64 Injury, Fix64 Predators, Fix64 Violence, Fix64 Senescence, Fix64 WinterShortfall)
    {
        public Fix64 Total => Endemic + Epidemic + Starvation + WinterShortfall + Exposure + Injury + Predators + Violence + Senescence;
    }

    private readonly record struct ScoutPlan(int FirstFigure, int SecondFigure, Fix64 Work)
    {
        public static ScoutPlan Empty => new(-1, -1, Fix64.Zero);
        public bool Contains(int figureIndex) => figureIndex == FirstFigure || figureIndex == SecondFigure;
    }

    private sealed class FigureState
    {
        public long Id;
        public int Civilization;
        public int SettlementId;
        public int Age;
        public bool Female;
        public Fix64 Health = Fix64.One;
        public Fix64 Hunger;
        public Fix64 Warmth = Fix64.One;
        public Fix64 Rest = Fix64.One;
        public Fix64 Belonging = Fix64.FromRatio(7, 10);
        public byte PregnancySeasons;
        public EconomyJob Job;
        public Fix64 WorkThisSeason;
        public Fix64 HuntWorkThisSeason;
        public Fix64 HuntingInjuryHazard;
        public byte IllnessSeasons;
        public byte WoundSeasons;
        public bool DiseaseTreated;
        public bool WoundTreated;
        public bool MidwifeAttended;
        public byte CareRolesThisSeason;
        public Fix64 CareWorkThisSeason;
        public Fix64 FireWorkThisSeason;
        public byte[] SkillLevels = new byte[(int)SkillFamily.Count];
        public Fix64[] SkillPractice = new Fix64[(int)SkillFamily.Count];
        /// <summary>The metal tool kit in hand: 0 none (the settlement's stone tools), 8, 9 or 10 (docs/MVP/23 §4.1).</summary>
        public byte Kit;
        /// <summary>Work-seasons of tool work left before the kit is due for replacement; negative in the dormancy tail.</summary>
        public Fix64 KitLeft;
        /// <summary>The metal or craft task done this season (<see cref="MetalTask"/>), which research reads as practice.</summary>
        public byte Task;
        /// <summary>The mining camp the figure works at, or -1 (docs/MVP/22 §5.2).</summary>
        public int Camp = -1;
        /// <summary>The E3 role the figure holds (<see cref="Role"/>): a Priest or a Scribe works it all season (docs/MVP/23 §9).</summary>
        public byte Role;
        /// <summary>Caught this season's outbreak (docs/MVP/09 §10.3); set at the season start.</summary>
        public bool Infected;
    }

    private sealed class SettlementState
    {
        public int Id;
        public int Civilization;
        public int SiteTile;
        public readonly Fix64[] Stores = new Fix64[(int)Good.Count];
        // Remaining work capacity across each fractional inventory of tools.
        public readonly Fix64[] ToolRemainingWork = new Fix64[(int)Good.Count];
        public readonly int[] Structures = new int[(int)Structure.Count];
        public readonly List<int> FieldTiles = new();
        public readonly List<OrchardPlot> OrchardPlots = new();
        public Fix64 FoodSecurity = Fix64.One;
        // Diagnostic snapshot taken at the same point FoodSecurity is computed.
        // This does not participate in simulation decisions.
        public Fix64 LastSeasonFoodStoresBeforeConsumption;
        public Fix64 LateWinterForecast = Fix64.One;
        public Fix64 LateWinterFoodShare = Fix64.One;
        public Fix64 FarmWorkThisYear;
        public Fix64 BuildProgress;
        public Fix64 DryingMudbrick;
        public Structure BuildTarget;
        public int Fissions;
        public int LastFissionSeason = -1000;
        public Fix64 Cohesion = Fix64.One;
        public Fix64 DomesticSmallStock;
        public Fix64 DomesticCattle;
        public Fix64 DomesticPigs;
        public bool FissionPending;
        /// <summary>A walled settlement's field and pasture output is cut while it is under investment (22 §7.4).</summary>
        public bool UnderInvestment;
        public ScoutPolicy ScoutPolicy = ScoutPolicy.Auto;
        public bool FireLit;
        public long FireKeeperId = -1;
        public byte UnattendedFireTicks;
        /// <summary>The Bronze Age economy of the settlement (metal stores and structures), null before its civilization's KEY-3.</summary>
        public MetalState? Metal;
        /// <summary>The early state of the settlement (tier, civic structures, Cohesion terms, outbreaks), null before its civilization's KEY-3.</summary>
        public CivicState? Civic;
    }

    private sealed class WildHerdState
    {
        public int Id;
        public HerdSpecies Species;
        public int HomeTile;
        public int CurrentTile;
        public Fix64 CurrentAnimals;
        public Fix64 Fear;
        public int AwaySeasons;
        public int LastHuntCrashCivilization = -1;
        public int TamingYear;
        public Fix64 TamingLimitThisYear;
        public Fix64 TamedThisYear;
    }

    private sealed class OrchardPlot
    {
        public int Tile;
        public int AgeSeasons;
    }

    private readonly record struct ArchetypeProfile(
        Fix64 Fertility,
        Fix64 FoodCapacity,
        Fix64 AdultMortality,
        Fix64 LeanShareAdjustment);

    private readonly ulong _seed;
    private readonly EconomyContent _content;
    private readonly WorldEconomyInput _world;
    private readonly ulong _contentFingerprint;
    private readonly Fix64[] _forageStock;
    private readonly Fix64[] _herdFoodStock;
    // Derived from individual herd state in herd-id order alongside _herdFoodStock.
    private readonly Fix64[] _wildHerdFoodGrowthPerYear;
    private readonly Fix64[] _wildHerdFoodGrowthWithWildfirePerYear;
    private readonly Fix64[] _woodStock;
    private readonly Fix64[] _fishStock;
    private readonly Fix64[] _stoneStock;
    private readonly Fix64[] _clayStock;
    private readonly Fix64[] _flintStock;
    private readonly Fix64[] _soil;
    private readonly byte[] _wildfireSeasonsRemaining;
    private readonly WildHerdState[] _wildHerds;
    private readonly bool _hasIndividualHerds;
    private readonly int[][] _rangeTiles;
    private readonly int[][] _fortificationRangeTiles;
    private readonly Fix64[] _siteFoodScores;
    private readonly int[] _fieldOwner;
    private readonly bool[] _tendedStandYieldBonus;
    private readonly int[] _riverComponentByTile;
    // Derived from structure counts in settlement-id order; one river tile can host one weir.
    private readonly int[] _weirOwner;
    private readonly bool[][] _completedProjects;
    // Derived from completed projects and immutable content effects. Lazy so snapshots need not serialize it.
    private Fix64[]? _reachableForageShareCache;
    private bool[]? _reachableForageShareCacheValid;
    private readonly EconomyArchetype[] _archetypes;
    private readonly bool _gentleStart;
    private readonly List<SettlementState> _settlements = new();
    private readonly List<FigureState> _figures = new();
    // Scratch work matrix used by PlanAndWork. Values are cleared for the previous
    // settlement's rows before the matrix is reused; this is derived state and is
    // intentionally absent from snapshots.
    private Fix64[,] _actionWorkScratch = new Fix64[0, (int)SkillFamily.Count];
    private readonly List<int> _actionWorkTouchedRows = new();
    private readonly int[] _lastDeathsByCause = new int[DeathCauseCount];
    private readonly int[] _lastDeathsByCauseAndAgeBand = new int[DeathCauseCount * 3];
    private readonly Fix64[] _lastExpectedHazardsByCauseAndAgeBand = new Fix64[DeathCauseCount * 3];
    private readonly Fix64[] _lastExpectedHazardsByExactAgeAndCause = new Fix64[DeathCauseCount * 100];
    private readonly Fix64[] _lastExpectedWinterShortfallByExactAge = new Fix64[100];
    // A7 telemetry sampled after GOV-04 redistribution and before seasonal production/consumption.
    // This is diagnostic-only state and does not affect simulation decisions or outcomes.
    private Fix64[] _lastSeasonReachableFoodStores = Array.Empty<Fix64>();
    private Fix64[] _lastSeasonFoodNeed = Array.Empty<Fix64>();
    private readonly List<EconomyDeathEvent> _lastDeathEvents = new();
    private long _tick;
    private long _seasonsCompleted;
    // Derived within one season end: forest stands whose deadwood a settlement has burned this season.
    private bool[]? _deadwoodUsed;
    private long _nextFigureId;
    private bool _lastWinterWasHarsh;
    private Fix64 _winterForageMultiplier = Fix64.Parse("1.08");

    public LivingEconomyModel(ulong seed, EconomyContent content, WorldEconomyInput world)
        : this(seed, content, world, ReadOnlySpan<EconomyArchetype>.Empty)
    {
    }

    public LivingEconomyModel(ulong seed, EconomyContent content, WorldEconomyInput world,
        ReadOnlySpan<EconomyArchetype> archetypes, bool gentleStart = false)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(world);
        if (world.CivilizationCount != 4)
            throw new ArgumentException("The current M2 world and M3 economy support four civilizations.", nameof(world));
        if (!archetypes.IsEmpty && archetypes.Length != world.CivilizationCount)
            throw new ArgumentException("An archetype must be supplied for each civilization.", nameof(archetypes));

        _seed = seed;
        _gentleStart = gentleStart;
        _content = content;
        _world = world;
        _archetypes = new EconomyArchetype[world.CivilizationCount];
        if (archetypes.IsEmpty) Array.Fill(_archetypes, EconomyArchetype.Balanced);
        else archetypes.CopyTo(_archetypes);
        for (int i = 0; i < _archetypes.Length; i++)
            if (!Enum.IsDefined(_archetypes[i]))
                throw new ArgumentOutOfRangeException(nameof(archetypes), $"Unknown archetype value {(byte)_archetypes[i]}.");
        _contentFingerprint = FingerprintContent(content);
        _forageStock = new Fix64[world.TileCount];
        _herdFoodStock = new Fix64[world.TileCount];
        _wildHerdFoodGrowthPerYear = new Fix64[world.TileCount];
        _wildHerdFoodGrowthWithWildfirePerYear = new Fix64[world.TileCount];
        _woodStock = new Fix64[world.TileCount];
        _fishStock = new Fix64[world.TileCount];
        _stoneStock = new Fix64[world.TileCount];
        _clayStock = new Fix64[world.TileCount];
        _flintStock = new Fix64[world.TileCount];
        _soil = new Fix64[world.TileCount];
        _wildfireSeasonsRemaining = new byte[world.TileCount];
        _hasIndividualHerds = world.Herds.Length > 0;
        _wildHerds = new WildHerdState[world.Herds.Length];
        for (int i = 0; i < _wildHerds.Length; i++)
        {
            WorldHerd input = world.Herd(i);
            Fix64 animals = Fix64.FromInt(input.InitialAnimalCount);
            _wildHerds[i] = new WildHerdState
            {
                Id = input.Id, Species = input.Species, HomeTile = input.Tile, CurrentTile = input.Tile,
                CurrentAnimals = animals, TamingYear = 0,
                TamingLimitThisYear = animals * Fix64.Parse("0.2"),
            };
        }
        _fieldOwner = new int[world.TileCount];
        Array.Fill(_fieldOwner, -1);
        _tendedStandYieldBonus = new bool[world.TileCount];
        _riverComponentByTile = BuildRiverComponents(world);
        _weirOwner = new int[world.TileCount];
        Array.Fill(_weirOwner, -1);
        for (int tile = 0; tile < world.TileCount; tile++)
        {
            WorldEconomyTile input = world.Tile(tile);
            _forageStock[tile] = input.InitialForage / Fix64.Parse("0.4");
            _herdFoodStock[tile] = input.InitialHerdFood;
            _woodStock[tile] = input.InitialWood;
            _fishStock[tile] = input.InitialFish;
            _stoneStock[tile] = input.InitialStoneStock;
            _clayStock[tile] = input.InitialClayStock;
            _flintStock[tile] = input.InitialFlintStock;
            _soil[tile] = input.SoilFertility;
        }
        if (_hasIndividualHerds) RebuildWildHerdFoodStock();
        _rangeTiles = BuildRangeTiles(world, Radius);
        _fortificationRangeTiles = BuildRangeTiles(world, 4);
        _siteFoodScores = new Fix64[world.TileCount];
        for (int tile = 0; tile < world.TileCount; tile++) _siteFoodScores[tile] = SiteFoodScore(tile);

        int projectCount = ProjectArrayLength(content);
        _completedProjects = new bool[world.CivilizationCount][];
        for (int civ = 0; civ < _completedProjects.Length; civ++)
        {
            _completedProjects[civ] = new bool[projectCount];
            var settlement = NewSettlement(civ, world.StartingTile(civ));
            _settlements.Add(settlement);
            SeedPopulation(settlement);
            settlement.Stores[(int)Good.Plant] = Fix64.FromInt(3);
            settlement.Stores[(int)Good.Meat] = Fix64.FromInt(2);
            settlement.Stores[(int)Good.Nuts] = Fix64.FromInt(5);
        }
        InitializeBronze();
    }

    public long Tick => _tick;
    public long SeasonsCompleted => _seasonsCompleted;
    public int CivilizationCount => _completedProjects.Length;
    public int SettlementCount => _settlements.Count;
    public int FigureCount => _figures.Count;
    public int WildHerdCount => _wildHerds.Length;
    public bool LastWinterWasHarsh => _lastWinterWasHarsh;
    public ReadOnlySpan<int> LastDeathsByCause => _lastDeathsByCause;
    public int LastDeathEventCount => _lastDeathEvents.Count;
    public EconomyDeathEvent LastDeathEvent(int index) => _lastDeathEvents[index];

    /// <summary>
    /// Whether the deterministic cause-selection draw for a last-season death landed in the conditional Winter shortfall
    /// hazard, rather than the unconditional baseline starvation hazard. This replays the cause stream and does not
    /// change simulation state or the random sequence.
    /// </summary>
    public bool IsLastDeathAttributedToWinterShortfall(int index)
    {
        if ((uint)index >= (uint)_lastDeathEvents.Count || _seasonsCompleted <= 0) return false;
        EconomyDeathEvent death = _lastDeathEvents[index];
        if (death.Cause != EconomyDeathCause.Starvation || death.WinterShortfallHazard <= Fix64.Zero) return false;
        Pcg32 selection = Pcg32.ForStream(_seed, DeathCauseStream, (ulong)death.FigureId, _seasonsCompleted - 1);
        return IsWinterShortfallCauseDraw(death, selection.NextFix01() * death.TotalHazard);
    }

    internal static bool IsWinterShortfallCauseDraw(EconomyDeathEvent death, Fix64 target)
    {
        if (death.Cause != EconomyDeathCause.Starvation || death.WinterShortfallHazard <= Fix64.Zero) return false;
        target -= death.EndemicHazard;
        target -= death.EpidemicHazard;
        Fix64 baselineStarvation = death.StarvationHazard - death.WinterShortfallHazard;
        return target >= baselineStarvation && target < death.StarvationHazard;
    }

    /// <summary>Food inventory immediately before ration consumption in the last completed season.</summary>
    public Fix64 LastSeasonFoodStoresBeforeConsumption(int settlementIndex)
        => _settlements[settlementIndex].LastSeasonFoodStoresBeforeConsumption;

    /// <summary>Food stores after season-start redistribution, before work and consumption.</summary>
    public Fix64 LastSeasonReachableFoodStores(int settlementIndex)
        => (uint)settlementIndex < (uint)_lastSeasonReachableFoodStores.Length
            ? _lastSeasonReachableFoodStores[settlementIndex]
            : Fix64.Zero;

    /// <summary>Exact seasonal food need at season start, including the under-investment adjustment.</summary>
    public Fix64 LastSeasonFoodNeed(int settlementIndex)
        => (uint)settlementIndex < (uint)_lastSeasonFoodNeed.Length
            ? _lastSeasonFoodNeed[settlementIndex]
            : Fix64.Zero;
    public ReadOnlySpan<int> LastDeathsByCauseAndAgeBand(int ageBand)
    {
        if (ageBand < 0 || ageBand >= 3) throw new ArgumentOutOfRangeException(nameof(ageBand));
        return _lastDeathsByCauseAndAgeBand.AsSpan(ageBand * DeathCauseCount, DeathCauseCount);
    }

    /// <summary>Expected cause-specific seasonal hazard summed over figures at risk at the last season boundary.</summary>
    public ReadOnlySpan<Fix64> LastExpectedHazardsByCauseAndAgeBand(int ageBand)
    {
        if (ageBand < 0 || ageBand >= 3) throw new ArgumentOutOfRangeException(nameof(ageBand));
        return _lastExpectedHazardsByCauseAndAgeBand.AsSpan(ageBand * DeathCauseCount, DeathCauseCount);
    }

    /// <summary>Expected seasonal cause hazards summed over figures of an exact age at the last season boundary.</summary>
    public ReadOnlySpan<Fix64> LastExpectedHazardsByExactAgeAndCause(int age)
    {
        if (age < 0 || age >= 100) throw new ArgumentOutOfRangeException(nameof(age));
        return _lastExpectedHazardsByExactAgeAndCause.AsSpan(age * DeathCauseCount, DeathCauseCount);
    }

    /// <summary>Expected winter food-shortfall surcharge summed over figures of an exact age at the last season boundary.</summary>
    public Fix64 LastExpectedWinterShortfallByExactAge(int age)
    {
        if (age < 0 || age >= 100) throw new ArgumentOutOfRangeException(nameof(age));
        return _lastExpectedWinterShortfallByExactAge[age];
    }

    public int AgeBandPopulation(int settlementIndex, int ageBand)
    {
        if (ageBand < 0 || ageBand >= 3) throw new ArgumentOutOfRangeException(nameof(ageBand));
        int settlementId = _settlements[settlementIndex].Id;
        int count = 0;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState figure = _figures[i];
            if (figure.SettlementId != settlementId) continue;
            bool matches = ageBand == 0 ? figure.Age < 14 : ageBand == 1 ? figure.Age is >= 14 and < 45 : figure.Age >= 45;
            if (matches) count++;
        }
        return count;
    }

    public SettlementView Settlement(int index)
    {
        SettlementState s = _settlements[index];
        Fix64 foodCap = FoodCap(s);
        Fix64 housingCap = HousingCap(s);
        Fix64 groupCap = GroupCap(s);
        Fix64 leanFoodCapacity = LeanFoodCapacity(s);
        return new SettlementView(s.Id, s.Civilization, s.SiteTile, Population(s.Id), FoodStores(s),
            foodCap, housingCap, groupCap, Fix64.Min(foodCap, Fix64.Min(housingCap, groupCap)),
            s.FoodSecurity, s.LateWinterFoodShare, s.LateWinterForecast, C2LateWinterCapacityShare(s),
            leanFoodCapacity, leanFoodCapacity * Fix64.Clamp(s.LateWinterFoodShare, Fix64.Zero, Fix64.One), s.FissionPending, s.Fissions,
            s.Structures[(int)Structure.StoragePit], s.Structures[(int)Structure.DryingRack],
            s.FieldTiles.Count, WarmthTier(s));
    }

    /// <summary>The scouting policy stored for a settlement.</summary>
    public ScoutPolicy SettlementScoutPolicy(int index) => _settlements[index].ScoutPolicy;

    /// <summary>Hearths built at a settlement (FIR-03), for research's hearth Insight bonus.</summary>
    public int HearthCount(int settlementIndex) => _settlements[settlementIndex].Structures[(int)Structure.Hearth];

    public FigureView Figure(int index)
    {
        FigureState f = _figures[index];
        return new FigureView(f.Id, f.Civilization, f.SettlementId, f.Age, f.Female, f.Health,
            f.Hunger, f.Warmth, f.Rest, f.Belonging, f.PregnancySeasons, f.Job, f.WorkThisSeason, f.Task);
    }

    public WildHerdView WildHerd(int index)
    {
        WildHerdState herd = _wildHerds[index];
        return new WildHerdView(herd.Id, herd.Species, herd.HomeTile, herd.CurrentTile,
            herd.CurrentAnimals, herd.Fear, herd.AwaySeasons, herd.LastHuntCrashCivilization);
    }

    public DomesticHerdView DomesticHerd(int settlementIndex)
    {
        SettlementState settlement = _settlements[settlementIndex];
        return new DomesticHerdView(settlement.DomesticSmallStock, settlement.DomesticCattle, settlement.DomesticPigs);
    }

    public int Population(int settlementId)
    {
        int count = 0;
        for (int i = 0; i < _figures.Count; i++)
            if (_figures[i].SettlementId == settlementId) count++;
        return count;
    }

    public Fix64 FoodStores(int settlementIndex) => FoodStores(_settlements[settlementIndex]);

    public bool HasCompletedProject(int civilization, string projectId)
    {
        int index = ProjectIndex(projectId);
        return index >= 0 && _completedProjects[civilization][index];
    }

    internal void ConfigureC1ReferenceFixture(int civilization, IReadOnlyList<string> projectIds,
        ReadOnlySpan<int> fieldTiles, bool seedHarpoon)
    {
        if (_tick != 0 || _seasonsCompleted != 0)
            throw new InvalidOperationException("C1 reference fixture state must be configured before the first simulation step.");
        if ((uint)civilization >= (uint)_settlements.Count)
            throw new ArgumentOutOfRangeException(nameof(civilization));

        foreach (string projectId in projectIds)
        {
            int projectIndex = ProjectIndex(projectId);
            if (projectIndex < 0) throw new ArgumentException($"Unknown C1 reference project '{projectId}'.", nameof(projectIds));
            CompleteProject(civilization, projectIndex);
        }

        SettlementState settlement = _settlements[civilization];
        foreach (int tile in fieldTiles)
        {
            if (!IsWithinRange(settlement.SiteTile, tile) || _world.Tile(tile).FieldYieldPerYear <= Fix64.Zero ||
                _fieldOwner[tile] >= 0 || IsOrchardOccupied(tile))
                throw new ArgumentException($"Tile {tile} is not an eligible C1 reference field.", nameof(fieldTiles));
            _fieldOwner[tile] = settlement.Id;
            _tendedStandYieldBonus[tile] = HasProject(civilization, "FOR-06") && _world.Tile(tile).WildGrainStand;
            settlement.FieldTiles.Add(tile);
            settlement.Structures[(int)Structure.Field]++;
        }
        if (seedHarpoon && HasProject(civilization, "HNT-07"))
            settlement.Stores[(int)Good.Harpoon] = Fix64.One;
    }

    public Fix64 SustainableFoodCapacity(int settlementIndex)
    {
        return SustainableFoodCapacity(_settlements[settlementIndex]);
    }

    public FoodIncomeBreakdown SustainableFoodIncomeBreakdown(int settlementIndex)
    {
        SettlementState settlement = _settlements[settlementIndex];
        _ = SustainableFoodIncome(settlement, out _, out Fix64 plants, out Fix64 meat,
            out Fix64 grain, out Fix64 fish);
        Fix64 fruit = OrchardAnnualRations(settlement) * FoodValue(Good.Fruit, settlement);
        return new FoodIncomeBreakdown(plants, meat, fish, grain, fruit, AnnualFoodNeedPerPerson(settlement));
    }

    private Fix64 SustainableFoodCapacity(SettlementState settlement)
    {
        Fix64 annualNeedPerPerson = AnnualFoodNeedPerPerson(settlement);
        return annualNeedPerPerson <= Fix64.Zero
            ? Fix64.Zero
            : SustainableFoodIncome(settlement, out _, out _, out _, out _, out _) / annualNeedPerPerson;
    }

    public void Step(long tick, ReadOnlySpan<Command> commands)
    {
        if (tick != _tick) throw new InvalidOperationException($"Expected tick {_tick}, got {tick}.");
        if (_tick != 0 && _tick % SeasonTicks == 0)
            ResolvePendingFissions(checked((int)_seasonsCompleted));
        foreach (Command command in commands) Apply(command);
        AdvanceCampFires();
        _tick++;
        if (_tick % SeasonTicks == 0)
        {
            EndSeason(checked((int)_seasonsCompleted));
            _seasonsCompleted++;
        }
    }

    private void Apply(Command command)
    {
        if (command.Civ >= _completedProjects.Length) return;
        if (command.Kind == CommandKind.SetScoutPolicy)
        {
            if ((uint)command.A >= (uint)_settlements.Count || command.B < (int)ScoutPolicy.Auto || command.B > (int)ScoutPolicy.Two)
                return;
            SettlementState settlement = _settlements[command.A];
            if (settlement.Civilization == command.Civ &&
                (command.B != (int)ScoutPolicy.Two || HasProject(command.Civ, "WAY-01")))
                settlement.ScoutPolicy = (ScoutPolicy)command.B;
            return;
        }
        if (command.Kind != CommandKind.CompleteProject) return;
        int index = command.A;
        if (index < 0 || index >= _completedProjects[command.Civ].Length) return;
        EconomyProject project = _content.Projects[index];
        if (project.Era <= MaxEra) CompleteProject(command.Civ, index);
    }

    private void CompleteProject(int civ, int index)
    {
        if (_completedProjects[civ][index]) return;
        _completedProjects[civ][index] = true;
        ClearModifierCache(civ);
        InvalidateReachableForageShare(civ);
        if (index == _key3) _bronzeActive = true;
        BuildHousingPrototype(civ, index);
        if (_content.Projects[index].Id is "FIR-01" or "FIR-02")
        {
            // Discovering fire keeping supplies the initial embers; completing it again
            // cannot restore an extinguished fire because completed projects return above.
            foreach (SettlementState settlement in _settlements)
            {
                if (settlement.Civilization != civ) continue;
                settlement.FireLit = true;
                settlement.UnattendedFireTicks = 0;
                settlement.FireKeeperId = FindFireKeeper(settlement);
            }
        }
        // Cohesion effects of E0-E2 projects nudge the stored factor once. From E3 they are terms of a Cohesion target
        // with scopes (docs/MVP/23 §1.2), which a later work package owns; applying them as one-off nudges would be wrong.
        foreach (EconomyEffect effect in _content.Effects)
        {
            if (effect.ProjectIndex != index || effect.Kind != "cohesion" || effect.Value is not { } value) continue;
            if (_content.Projects[index].Era > 2) continue;
            Fix64 change = value / Fix64.FromInt(100);
            for (int i = 0; i < _settlements.Count; i++)
                if (_settlements[i].Civilization == civ)
                    _settlements[i].Cohesion = Fix64.Clamp(_settlements[i].Cohesion + change,
                        Fix64.Parse("0.6"), Fix64.Parse("1.2"));
        }
    }

    /// <summary>Research supplies one free prototype of each newly unlocked dwelling at the civilization's oldest inhabited settlement.</summary>
    private void BuildHousingPrototype(int civ, int projectIndex)
    {
        int oldest = OldestOf(civ);
        if (oldest < 0) return;

        SettlementState settlement = _settlements[oldest];
        EconomyProject project = _content.Projects[projectIndex];
        for (int i = project.EffectStart; i < project.EffectStart + project.EffectCount; i++)
        {
            EconomyEffect effect = _content.Effects[i];
            if (effect.Kind != "build") continue;

            int kind = StructureIndex(effect.Structure ?? string.Empty);
            if (kind >= 0 && IsDwelling((Structure)kind))
            {
                settlement.Structures[kind] = checked(settlement.Structures[kind] + 1);
                continue;
            }

            if (CivicOf(effect.Structure) == CivicBuild.Hall)
                AddBuilt(EnsureCivic(settlement), CivicBuild.Hall);
        }
    }

    private static bool IsDwelling(Structure structure)
        => structure is Structure.LeanTo or Structure.HideTent or Structure.PitHouse or Structure.WattleHouse or
            Structure.MudbrickHouse or Structure.Longhouse;

    private long FindFireKeeper(SettlementState settlement)
    {
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState figure = _figures[i];
            if (figure.SettlementId == settlement.Id && CanKeepFire(figure)) return figure.Id;
        }
        return -1;
    }

    private bool CanKeepFire(FigureState figure)
        => figure.Age >= 14 && figure.IllnessSeasons == 0 && figure.WoundSeasons == 0 &&
            figure.PregnancySeasons != 1 && WorkCapacity(figure) >= FireKeepingWork;

    private Fix64 AssignFireKeeper(SettlementState settlement)
    {
        if (!settlement.FireLit || HasProject(settlement.Civilization, "FIR-02") ||
            settlement.Structures[(int)Structure.Hearth] > 0)
        {
            settlement.FireKeeperId = -1;
            return Fix64.Zero;
        }

        FigureState? keeper = null;
        if (settlement.FireKeeperId >= 0)
        {
            for (int i = 0; i < _figures.Count; i++)
            {
                FigureState candidate = _figures[i];
                if (candidate.Id == settlement.FireKeeperId && candidate.SettlementId == settlement.Id && CanKeepFire(candidate))
                { keeper = candidate; break; }
            }
        }
        if (keeper is null)
        {
            settlement.FireKeeperId = FindFireKeeper(settlement);
            if (settlement.FireKeeperId < 0) return Fix64.Zero;
            for (int i = 0; i < _figures.Count; i++)
                if (_figures[i].Id == settlement.FireKeeperId) { keeper = _figures[i]; break; }
        }
        if (keeper is null) return Fix64.Zero;

        keeper.Job = EconomyJob.TendFire;
        keeper.WorkThisSeason += FireKeepingWork;
        keeper.FireWorkThisSeason = FireKeepingWork;
        return FireKeepingWork;
    }

    private void AdvanceCampFires()
    {
        bool winter = (_tick / SeasonTicks) % 4 == 3;
        for (int s = 0; s < _settlements.Count; s++)
        {
            SettlementState settlement = _settlements[s];
            bool hearth = settlement.Structures[(int)Structure.Hearth] > 0;
            bool fireMaking = HasProject(settlement.Civilization, "FIR-02");
            if (hearth)
            {
                Fix64 fuel = winter ? WinterHearthFuelPerTick : HearthFuelPerTick;
                if (settlement.Stores[(int)Good.Wood] >= fuel)
                {
                    settlement.Stores[(int)Good.Wood] -= fuel;
                    settlement.FireLit = true;
                    settlement.UnattendedFireTicks = 0;
                    settlement.FireKeeperId = -1;
                }
                else settlement.FireLit = false;
                continue;
            }
            if (fireMaking)
            {
                settlement.FireLit = true;
                settlement.UnattendedFireTicks = 0;
                settlement.FireKeeperId = -1;
                continue;
            }
            if (!settlement.FireLit) continue;

            bool keeperPresent = false;
            for (int i = 0; i < _figures.Count; i++)
            {
                FigureState figure = _figures[i];
                if (figure.Id == settlement.FireKeeperId && figure.SettlementId == settlement.Id && CanKeepFire(figure))
                { keeperPresent = true; break; }
            }
            if (keeperPresent)
            {
                settlement.UnattendedFireTicks = 0;
                continue;
            }
            if (settlement.Stores[(int)Good.Wood] > Fix64.Zero)
            {
                settlement.Stores[(int)Good.Wood] = Fix64.Max(Fix64.Zero,
                    settlement.Stores[(int)Good.Wood] - FireFuelPerTick);
                settlement.UnattendedFireTicks = 0;
                continue;
            }
            if (settlement.UnattendedFireTicks < 10) settlement.UnattendedFireTicks++;
            if (settlement.UnattendedFireTicks >= 10)
            {
                settlement.FireLit = false;
                settlement.FireKeeperId = -1;
            }
        }
    }

    private void EndSeason(int season)
    {
        EnsureSeasonFoodTelemetryCapacity();
        Array.Clear(_lastSeasonReachableFoodStores);
        Array.Clear(_lastSeasonFoodNeed);
        // Births are added after seasonal mortality. Keep this boundary so Winter's
        // age pass advances only people who existed before the season update.
        long firstFigureIdBornThisSeason = _nextFigureId;
        Array.Clear(_lastDeathsByCause);
        Array.Clear(_lastDeathsByCauseAndAgeBand);
        Array.Clear(_lastExpectedHazardsByCauseAndAgeBand);
        Array.Clear(_lastExpectedHazardsByExactAgeAndCause);
        Array.Clear(_lastExpectedWinterShortfallByExactAge);
        _lastDeathEvents.Clear();
        bool winter = season % 4 == 3;
        _lastWinterWasHarsh = false;
        if (winter)
        {
            Pcg32 climate = Pcg32.ForStream(_seed, ClimateStream, 0, season);
            uint roll = climate.NextBelow(100);
            bool protectedWinter = _gentleStart && season < 12;
            _lastWinterWasHarsh = !protectedWinter && roll < 30;
            _winterForageMultiplier = protectedWinter ? Fix64.Parse("1.08") : _lastWinterWasHarsh ? Fix64.Parse("0.6") :
                roll < 55 ? Fix64.Parse("1.15") : Fix64.Parse("1.08");
        }

        AdvanceWildfires(season);
        RegrowResources(season % 4);
        if (_hasIndividualHerds) AdvanceWildHerdBehavior(season);
        if (_bronzeActive)
        {
            BronzeSeasonStart(season);
            CivicSeasonStart(season);
            _deadwoodUsed = new bool[_world.TileCount];
        }
        // Keep A7's season-start stock snapshot available for all eras. Before E3,
        // no food redistribution runs, so the snapshot is the settlement's own stores.
        CaptureSeasonStartFoodAvailability(winter);
        for (int s = 0; s < _settlements.Count; s++)
        {
            SettlementState settlement = _settlements[s];
            AdvanceMudbrickDrying(settlement);
            AdvanceOrchards(settlement, season);
            AdvanceDomesticHerds(settlement, season);
            PlanAndWork(settlement, season);
            // §09 §3.2 conception uses the fed share from the completed prior season.
            Fix64 priorSeasonFoodSecurity = settlement.FoodSecurity;
            ConsumeFood(settlement, winter, season);
            SpoilFood(settlement);
            BuildIfReady(settlement);
            if (!_holdPopulation) UpdateNeedsAndPopulation(settlement, season, winter, priorSeasonFoodSecurity);
            if (season % 4 == 2) settlement.LateWinterForecast = ForecastWinterFood(settlement, ScoutPlan.Empty);
        }

        if (!_holdPopulation && season % 4 == 3) AgePopulationByOneYear(firstFigureIdBornThisSeason);
        if (_bronzeActive) CivicSeasonEnd(season);
        _deadwoodUsed = null;
        for (int s = 0; s < _settlements.Count; s++)
            TryFission(_settlements[s], season);
        RotateMetalOutputSeason();
    }

    private void EnsureSeasonFoodTelemetryCapacity()
    {
        if (_lastSeasonReachableFoodStores.Length == _settlements.Count) return;
        Array.Resize(ref _lastSeasonReachableFoodStores, _settlements.Count);
        Array.Resize(ref _lastSeasonFoodNeed, _settlements.Count);
    }

    private void CaptureSeasonStartFoodAvailability(bool winter)
    {
        EnsureSeasonFoodTelemetryCapacity();
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState settlement = _settlements[i];
            Fix64 need = FoodNeed(settlement, winter);
            if (settlement.UnderInvestment) need *= Fix64.FromRatio(75, 100);
            _lastSeasonReachableFoodStores[i] = FoodStores(settlement);
            _lastSeasonFoodNeed[i] = need;
        }
    }

    private void RegrowResources(int seasonOfYear)
    {
        int year = checked((int)(_seasonsCompleted / 4));
        if (_hasIndividualHerds && seasonOfYear == 0) AdvanceWildHerdGrowth(year);
        if (_bronzeActive) RefreshCoppicing();
        for (int tile = 0; tile < _world.TileCount; tile++)
        {
            WorldEconomyTile input = _world.Tile(tile);
            if (!_hasIndividualHerds && seasonOfYear == 0 && input.HerdFoodGrowthPerYear > Fix64.Zero && IsHerdCrash(tile, year))
                _herdFoodStock[tile] *= Fix64.Half;

            Fix64 forageRegrowth = SeasonalForageRegrowth(tile, seasonOfYear, year);
            if (seasonOfYear == 3) forageRegrowth *= _winterForageMultiplier;
            Fix64 forageCapacity = ForageStockCapacity(tile, seasonOfYear, year);
            _forageStock[tile] = Fix64.Min(_forageStock[tile] + forageRegrowth, forageCapacity);
            if (!_hasIndividualHerds)
            {
                Fix64 herdGrowth = input.HerdFoodGrowthPerYear;
                if (_wildfireSeasonsRemaining[tile] > 0) herdGrowth *= Fix64.Parse("1.3");
                _herdFoodStock[tile] = Fix64.Min(_herdFoodStock[tile] + herdGrowth / 4,
                    input.InitialHerdFood + herdGrowth);
            }
            _woodStock[tile] = Fix64.Min(_woodStock[tile] + WoodRegrowth(tile, input.WoodGrowthPerYear / 4), input.InitialWood);
            if (_bronzeActive) UpdateForestState(tile);
            _fishStock[tile] = Fix64.Min(_fishStock[tile] + SeasonalFishRegrowth(tile, seasonOfYear, year),
                input.InitialFish + input.TotalFishGrowthPerYear);
        }
        if (_hasIndividualHerds) RebuildWildHerdFoodStock();
    }

    private void AdvanceWildHerdGrowth(int year)
    {
        for (int i = 0; i < _wildHerds.Length; i++)
        {
            WildHerdState herd = _wildHerds[i];
            WorldHerd source = _world.Herd(i);
            if (IsIndividualHerdCrash(herd.Id, year)) herd.CurrentAnimals *= Fix64.Half;
            Fix64 growthRate = Fix64.FromRatio(source.AnnualGrowthPercent, 100);
            if (_wildfireSeasonsRemaining[herd.CurrentTile] > 0) growthRate *= Fix64.Parse("1.3");
            Fix64 carryingCapacity = Fix64.FromInt(source.InitialAnimalCount) * (Fix64.One + growthRate);
            herd.CurrentAnimals = Fix64.Min(carryingCapacity,
                herd.CurrentAnimals + herd.CurrentAnimals * growthRate);
            herd.TamingYear = year;
            herd.TamingLimitThisYear = herd.CurrentAnimals * Fix64.Parse("0.2");
            herd.TamedThisYear = Fix64.Zero;
        }
    }

    private void AdvanceWildHerdBehavior(int season)
    {
        for (int i = 0; i < _wildHerds.Length; i++)
        {
            WildHerdState herd = _wildHerds[i];
            // Fear gain and the displacement threshold are unspecified. Fear is measured in
            // normalized herd units: one kill adds 0.1, while a drive adds three times that.
            Fix64 decay = Fix64.Parse("0.25");
            if (HasHerdFollowingNear(herd)) decay *= 2;
            herd.Fear = Fix64.Max(Fix64.Zero, herd.Fear - decay);
            if (herd.AwaySeasons > 0)
            {
                herd.AwaySeasons--;
                if (herd.AwaySeasons == 0) herd.CurrentTile = herd.HomeTile;
            }
        }
        RebuildWildHerdFoodStock();
        _ = season;
    }

    private bool HasHerdFollowingNear(WildHerdState herd)
    {
        foreach (SettlementState settlement in _settlements)
            if (HasProject(settlement.Civilization, "HUS-02") &&
                HexGrid.Distance(settlement.SiteTile, herd.HomeTile, _world.Width) <= Radius)
                return true;
        return false;
    }

    private void DisplaceHerdIfAfraid(WildHerdState herd, bool cooperativeDrive, int season, SettlementState hunter)
    {
        if (herd.Fear < Fix64.One || herd.AwaySeasons > 0) return;
        bool following = HasProject(hunter.Civilization, "HUS-02");
        if (cooperativeDrive && following) return;
        Pcg32 displacement = Pcg32.ForStream(_seed, HerdBehaviorStream, (ulong)herd.Id, season);
        int distance = following ? 1 + (int)displacement.NextBelow((uint)Radius) :
            cooperativeDrive ? 4 + (int)displacement.NextBelow(3) : 3 + (int)displacement.NextBelow(4);
        var candidates = new List<int>();
        for (int tile = 0; tile < _world.TileCount; tile++)
            if (HexGrid.Distance(herd.HomeTile, tile, _world.Width) == distance && IsLandBiome(_world.Tile(tile).BiomeId))
                candidates.Add(tile);
        if (candidates.Count == 0) return;
        Pcg32 random = Pcg32.ForStream(_seed, HerdBehaviorStream, (ulong)herd.Id, season ^ 0x5A5A);
        herd.CurrentTile = candidates[(int)random.NextBelow((uint)candidates.Count)];
        // Behavior advances before the next seasonal hunt. Two countdowns keep the herd away
        // through one complete hunting season rather than returning immediately before it.
        herd.AwaySeasons = 2;
    }

    private void RebuildWildHerdFoodStock()
    {
        Array.Clear(_herdFoodStock);
        Array.Clear(_wildHerdFoodGrowthPerYear);
        Array.Clear(_wildHerdFoodGrowthWithWildfirePerYear);
        for (int i = 0; i < _wildHerds.Length; i++)
        {
            WildHerdState herd = _wildHerds[i];
            if (herd.CurrentAnimals <= Fix64.Zero) continue;
            _herdFoodStock[herd.CurrentTile] += herd.CurrentAnimals * MeatPerAnimal(herd.Species);
            Fix64 annualRate = Fix64.FromRatio(_world.Herd(i).AnnualGrowthPercent, 100);
            _wildHerdFoodGrowthPerYear[herd.CurrentTile] +=
                herd.CurrentAnimals * annualRate * MeatPerAnimal(herd.Species);
            Fix64 wildfireAnnualRate = annualRate * Fix64.Parse("1.3");
            _wildHerdFoodGrowthWithWildfirePerYear[herd.CurrentTile] +=
                herd.CurrentAnimals * wildfireAnnualRate * MeatPerAnimal(herd.Species);
        }
    }

    private static Fix64 MeatPerAnimal(HerdSpecies species) => species switch
    {
        HerdSpecies.Deer => Fix64.FromInt(4),
        HerdSpecies.Aurochs or HerdSpecies.Bison => Fix64.FromInt(15),
        HerdSpecies.WildBoar => Fix64.FromInt(5),
        HerdSpecies.Horse => Fix64.FromInt(10),
        HerdSpecies.Reindeer => Fix64.FromInt(5),
        HerdSpecies.WildGoatSheep or HerdSpecies.GazelleAntelope => Fix64.FromInt(3),
        HerdSpecies.CamelLlama => Fix64.FromInt(8),
        _ => Fix64.One,
    };

    private Fix64 HerdFoodGrowthPerYear(int tile, bool includeTransientEvents = true)
    {
        if (!_hasIndividualHerds) return _world.Tile(tile).HerdFoodGrowthPerYear;
        return includeTransientEvents && _wildfireSeasonsRemaining[tile] > 0
            ? _wildHerdFoodGrowthWithWildfirePerYear[tile]
            : _wildHerdFoodGrowthPerYear[tile];
    }

    private Fix64 HerdFoodStockCapacity(int tile)
    {
        WorldEconomyTile input = _world.Tile(tile);
        Fix64 growth = input.HerdFoodGrowthPerYear;
        if (_wildfireSeasonsRemaining[tile] > 0) growth *= Fix64.Parse("1.3");
        return _hasIndividualHerds
            ? _herdFoodStock[tile] + HerdFoodGrowthPerYear(tile)
            : input.InitialHerdFood + growth;
    }

    private Fix64 DomesticSmallStockCapacity(SettlementState settlement)
        => Fix64.FromInt(settlement.Structures[(int)Structure.Pasture] * 6);

    private Fix64 DomesticLargeAnimalCapacity(SettlementState settlement)
        => Fix64.FromInt(settlement.Structures[(int)Structure.Pasture] * 2);

    private static Fix64 DomesticHerdWorkNeed(SettlementState settlement)
        => settlement.DomesticSmallStock / Fix64.FromInt(10) * Fix64.Parse("0.1") +
            (settlement.DomesticCattle + settlement.DomesticPigs) / Fix64.FromInt(4) * Fix64.Parse("0.1");

    private void AdvanceDomesticHerds(SettlementState settlement, int season)
    {
        int seasonOfYear = season % 4;
        Fix64 smallCapacity = DomesticSmallStockCapacity(settlement);
        Fix64 largeCapacity = DomesticLargeAnimalCapacity(settlement);
        if (seasonOfYear == 0)
        {
            // Domestic herds grow 25% yearly, limited by the documented per-pasture capacity.
            settlement.DomesticSmallStock = Fix64.Min(smallCapacity, settlement.DomesticSmallStock * Fix64.Parse("1.25"));
            Fix64 largeTotal = settlement.DomesticCattle + settlement.DomesticPigs;
            if (largeTotal > Fix64.Zero)
            {
                Fix64 allowed = Fix64.Min(largeCapacity, largeTotal * Fix64.Parse("1.25"));
                Fix64 scale = allowed / largeTotal;
                settlement.DomesticCattle *= scale;
                settlement.DomesticPigs *= scale;
            }
        }
        _ = season;
    }

    private void ConsumeStoredFoodValue(SettlementState settlement, Fix64 amount)
    {
        Fix64 remaining = amount;
        for (int i = 0; i < FoodGoods.Length && remaining > Fix64.Zero; i++)
        {
            Good good = FoodGoods[i];
            Fix64 value = FoodValue(good, settlement);
            if (value <= Fix64.Zero) continue;
            Fix64 available = settlement.Stores[(int)good] * value;
            Fix64 consumed = Fix64.Min(remaining, available);
            settlement.Stores[(int)good] -= consumed / value;
            remaining -= consumed;
        }
    }

    private Fix64 SeasonalFishRegrowth(int tile, int season, int? year = null)
    {
        WorldEconomyTile input = _world.Tile(tile);
        Fix64 growth = input.FishGrowthPerYear / 4 + (season == 2 ? input.SalmonAutumnGrowth : Fix64.Zero);
        return IsDrought(tile, year ?? checked((int)(_seasonsCompleted / 4)))
            ? growth * Fix64.Parse("0.7")
            : growth;
    }

    // Resource stocks are shared world state. The first settlement range owns local improvements;
    // the same selection is used by live replenishment and capacity/forecast calculations.
    private Fix64 SeasonalForageRegrowth(int tile, int season, int? year = null,
        bool includeTransientEvents = true)
    {
        if (_fieldOwner[tile] >= 0) return Fix64.Zero;
        WorldEconomyTile input = _world.Tile(tile);
        Fix64 growth = season switch
        {
            0 => input.SpringForage,
            1 => input.SummerForage,
            2 => input.AutumnForage,
            _ => input.WinterForage,
        };
        // M2 inputs retain the original 40%-accessible representation; stocks here are total plants.
        growth /= Fix64.Parse("0.4");
        if (_forestState.Length > 0 && _forestState[tile] != 0) growth *= ClearedForageFactor(tile);
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState settlement = _settlements[i];
            if (HexGrid.Distance(tile, settlement.SiteTile, _world.Width) > Radius) continue;
            int civ = settlement.Civilization;
            if (season == 3) growth *= Modifier(civ, "winter_forage_stock");
            if (season == 2) growth *= Modifier(civ, "autumn_forage");
            Biome biome = (Biome)input.BiomeId;
            if ((season == 1 || season == 2) && HasProject(civ, "FOR-04") &&
                (biome == Biome.OpenWoodland || biome == Biome.GrasslandSteppe || biome == Biome.Savanna))
                growth *= Fix64.Parse("1.6");
            if (HasProject(civ, "FOR-06") && input.WildGrainStand)
                growth *= Fix64.FromInt(2);
            break;
        }
        if (includeTransientEvents)
        {
            if (IsDrought(tile, year ?? checked((int)(_seasonsCompleted / 4)))) growth *= Fix64.Parse("0.6");
            if (_wildfireSeasonsRemaining[tile] > 0) growth *= Fix64.Parse("1.4");
        }
        return growth;
    }

    private Fix64 ForageStockCapacity(int tile, int currentSeason, int year)
    {
        int nextSeason = (currentSeason + 1) % 4;
        Fix64 current = SeasonalForageRegrowth(tile, currentSeason, year);
        if (currentSeason == 3) current *= _winterForageMultiplier;
        Fix64 next = SeasonalForageRegrowth(tile, nextSeason, year);
        if (nextSeason == 3) next *= _winterForageMultiplier;
        return current + next;
    }

    private bool IsDrought(int tile, int year)
    {
        WorldEconomyTile input = _world.Tile(tile);
        ClimateBand climate = (ClimateBand)input.ClimateId;
        if (climate is not (ClimateBand.Warm or ClimateBand.Arid)) return false;

        HexGrid.Coordinates(tile, _world.Width, out int x, out int y);
        int regionX = x / NaturalRegionSize;
        int regionY = y / NaturalRegionSize;
        ulong region = Hash64.Combine((ulong)(uint)regionX, (ulong)(uint)regionY);
        Pcg32 rng = Pcg32.ForStream(_seed, DroughtStream, region, year);
        return rng.NextBelow(100) < 8;
    }

    private bool IsSpringFlood(int riverTile, int year)
    {
        if (!_world.Tile(riverTile).River) return false;
        int component = _riverComponentByTile[riverTile];
        if (component < 0) return false;
        Pcg32 rng = Pcg32.ForStream(_seed, FloodStream, (ulong)(uint)component, year);
        return rng.NextBelow(100) < 5;
    }

    private bool IsFieldFlooded(int fieldTile, int year)
    {
        if (_world.Tile(fieldTile).River && IsSpringFlood(fieldTile, year)) return true;
        Span<int> neighbors = stackalloc int[6];
        int count = HexGrid.Neighbors(fieldTile, _world.Width, _world.Height, neighbors);
        for (int i = 0; i < count; i++)
            if (IsSpringFlood(neighbors[i], year)) return true;
        return false;
    }

    private void AdvanceWildfires(int season)
    {
        for (int tile = 0; tile < _wildfireSeasonsRemaining.Length; tile++)
            if (_wildfireSeasonsRemaining[tile] > 0) _wildfireSeasonsRemaining[tile]--;

        int seasonOfYear = season % 4;
        if (seasonOfYear is not (1 or 2)) return;
        int year = checked((int)(_seasonsCompleted / 4));
        int regionRows = (_world.Height + NaturalRegionSize - 1) / NaturalRegionSize;
        int regionColumns = (_world.Width + NaturalRegionSize - 1) / NaturalRegionSize;
        for (int regionY = 0; regionY < regionRows; regionY++)
        for (int regionX = 0; regionX < regionColumns; regionX++)
        {
            var combustible = new List<int>();
            int yEnd = Math.Min(_world.Height, (regionY + 1) * NaturalRegionSize);
            int xEnd = Math.Min(_world.Width, (regionX + 1) * NaturalRegionSize);
            for (int y = regionY * NaturalRegionSize; y < yEnd; y++)
            for (int x = regionX * NaturalRegionSize; x < xEnd; x++)
            {
                int tile = y * _world.Width + x;
                if (IsWildfireBiome((Biome)_world.Tile(tile).BiomeId)) combustible.Add(tile);
            }
            if (combustible.Count == 0) continue;

            ulong region = Hash64.Combine((ulong)(uint)regionX, (ulong)(uint)regionY);
            Pcg32 random = Pcg32.ForStream(_seed, WildfireStream, region, season);
            int chance = IsDrought(combustible[0], year) ? 6 : 3;
            if (random.NextBelow(100) >= chance) continue;
            int start = combustible[(int)random.NextBelow((uint)combustible.Count)];
            int target = 5 + (int)random.NextBelow(26);
            BurnWildfireCluster(start, target, regionX, regionY);
        }
    }

    private void BurnWildfireCluster(int startTile, int target, int regionX, int regionY)
    {
        var burned = new List<int>(target);
        var queued = new bool[_world.TileCount];
        queued[startTile] = true;
        var frontier = new Queue<int>();
        frontier.Enqueue(startTile);
        Span<int> neighbors = stackalloc int[6];
        while (frontier.Count > 0 && burned.Count < target)
        {
            int tile = frontier.Dequeue();
            burned.Add(tile);
            int count = HexGrid.Neighbors(tile, _world.Width, _world.Height, neighbors);
            for (int i = 0; i < count; i++)
            {
                int next = neighbors[i];
                HexGrid.Coordinates(next, _world.Width, out int x, out int y);
                if (x / NaturalRegionSize != regionX || y / NaturalRegionSize != regionY ||
                    !IsWildfireBiome((Biome)_world.Tile(next).BiomeId) || queued[next]) continue;
                queued[next] = true;
                frontier.Enqueue(next);
            }
        }

        foreach (int tile in burned)
        {
            _wildfireSeasonsRemaining[tile] = 12;
            DamageUnprotectedStructure(tile);
            RelightNearbyFire(tile);
        }
    }

    private static bool IsWildfireBiome(Biome biome)
        => biome is Biome.OpenWoodland or Biome.GrasslandSteppe or Biome.Savanna;

    private void DamageUnprotectedStructure(int tile)
    {
        int ownerId = _fieldOwner[tile];
        if (ownerId >= 0)
        {
            SettlementState owner = _settlements[ownerId];
            owner.FieldTiles.Remove(tile);
            owner.Structures[(int)Structure.Field] = Math.Max(0, owner.Structures[(int)Structure.Field] - 1);
            _fieldOwner[tile] = -1;
            _tendedStandYieldBonus[tile] = false;
        }

        foreach (SettlementState settlement in _settlements)
            for (int plot = settlement.OrchardPlots.Count - 1; plot >= 0; plot--)
                if (settlement.OrchardPlots[plot].Tile == tile)
                {
                    settlement.OrchardPlots.RemoveAt(plot);
                    settlement.Structures[(int)Structure.Orchard] = Math.Max(0, settlement.Structures[(int)Structure.Orchard] - 1);
                }

        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState settlement = _settlements[i];
            if (settlement.SiteTile != tile) continue;

            Structure[] vulnerable =
            {
                Structure.LeanTo, Structure.HideTent, Structure.PitHouse, Structure.WattleHouse,
                Structure.Longhouse, Structure.MudbrickHouse, Structure.StoragePit, Structure.DryingRack,
                Structure.Granary, Structure.Kiln, Structure.Quern, Structure.ThornBarrier,
                Structure.Lookout, Structure.Hearth, Structure.Palisade,
            };
            foreach (Structure structure in vulnerable)
            {
                if (structure == Structure.MudbrickHouse && HasProject(settlement.Civilization, "BLD-05") ||
                    settlement.Structures[(int)structure] <= 0) continue;
                settlement.Structures[(int)structure]--;
                if (structure == Structure.Hearth && settlement.Structures[(int)structure] == 0)
                    settlement.FireLit = false;
                return;
            }
        }
    }

    private void RelightNearbyFire(int burnedTile)
    {
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState settlement = _settlements[i];
            if (settlement.FireLit || HasProject(settlement.Civilization, "FIR-02") ||
                settlement.Structures[(int)Structure.Hearth] > 0 ||
                HexGrid.Distance(settlement.SiteTile, burnedTile, _world.Width) > 1) continue;
            settlement.FireLit = true;
            settlement.UnattendedFireTicks = 0;
            settlement.FireKeeperId = FindFireKeeper(settlement);
        }
    }

    private bool IsHerdCrash(int tile, int year)
    {
        Pcg32 rng = Pcg32.ForStream(_seed, HerdCrashStream, (ulong)tile, year);
        return rng.NextBelow(100) < 2;
    }

    private bool IsIndividualHerdCrash(int herdId, int year)
    {
        Pcg32 rng = Pcg32.ForStream(_seed, HerdCrashStream, (ulong)herdId, year);
        return rng.NextBelow(100) < 2;
    }

    private Fix64 ReachableForageShare(int civ)
    {
        _reachableForageShareCache ??= new Fix64[_completedProjects.Length];
        _reachableForageShareCacheValid ??= new bool[_completedProjects.Length];
        if (_reachableForageShareCacheValid[civ]) return _reachableForageShareCache[civ];

        Fix64 share = Fix64.Parse("0.4");
        foreach (EconomyEffect effect in _content.Effects)
        {
            if (effect.Kind != "mod" || effect.Stat != "forage.reachable_stock" ||
                effect.Value is not { } value || !_completedProjects[civ][effect.ProjectIndex]) continue;
            share = effect.Operation switch
            {
                "set" => value,
                "pct" => share * (Fix64.One + value),
                "mult" => share * value,
                "flat" => share + value,
                _ => share,
            };
        }
        share = Fix64.Clamp(share, Fix64.Zero, Fix64.One);
        _reachableForageShareCache[civ] = share;
        _reachableForageShareCacheValid[civ] = true;
        return share;
    }

    private void InvalidateReachableForageShare(int civ)
    {
        if (_reachableForageShareCacheValid is not null) _reachableForageShareCacheValid[civ] = false;
    }

    private Fix64 SeasonalForageFoodValue(SettlementState settlement, int season)
    {
        Fix64 freshValue = FoodValue(Good.Plant, settlement);
        Fix64 value = freshValue;
        if (season == 3 && HasProject(settlement.Civilization, "FOR-02"))
            value += Fix64.Parse("0.3") * (FoodValue(Good.Roots, settlement) - freshValue);
        if (season == 2 && HasProject(settlement.Civilization, "FOR-03"))
            value += Fix64.Parse("0.4") * (FoodValue(Good.Nuts, settlement) - freshValue);
        if (season == 2 && HasProject(settlement.Civilization, "FOR-04"))
            value += Fix64.Parse("0.2") * (FoodValue(Good.WildGrain, settlement) - freshValue);
        return value;
    }

    private void PlanAndWork(SettlementState settlement, int season)
    {
        Fix64[,] actionWork = _actionWorkScratch;
        foreach (int row in _actionWorkTouchedRows)
            for (int column = 0; column < (int)SkillFamily.Count; column++)
                actionWork[row, column] = Fix64.Zero;
        _actionWorkTouchedRows.Clear();
        if (actionWork.GetLength(0) < _figures.Count)
        {
            actionWork = new Fix64[_figures.Count, (int)SkillFamily.Count];
            _actionWorkScratch = actionWork;
        }
        Fix64 totalWork = Fix64.Zero;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState figure = _figures[i];
            if (figure.SettlementId != settlement.Id) continue;
            _actionWorkTouchedRows.Add(i);
            figure.Job = EconomyJob.None;
            figure.WorkThisSeason = Fix64.Zero;
            figure.HuntWorkThisSeason = Fix64.Zero;
            figure.HuntingInjuryHazard = Fix64.Zero;
            figure.FireWorkThisSeason = Fix64.Zero;
            figure.Task = 0;
            BeginHealthSeason(figure, season);
            totalWork += WorkCapacity(figure);
        }

        totalWork -= AssignFireKeeper(settlement);
        totalWork -= AllocateCare(settlement);
        if (settlement.Civic is not null) totalWork -= StaffRoles(settlement);
        totalWork -= AssignScouts(settlement, season);
        Fix64 upkeepBudget = WeirUpkeepBudget(settlement, totalWork);
        totalWork -= upkeepBudget;
        totalWork -= OrchardUpkeepBudget(settlement, totalWork);
        // The Bronze Age metal chain reserves its labor first, at most MetalPushLaborCap of the work (docs/MVP/23 §7.3).
        MetalPlan? metalPlan = null;
        Fix64[]? toolWork = null;
        if (settlement.Metal is not null && CivInBronze(settlement.Civilization))
        {
            metalPlan = PlanMetal(settlement, totalWork);
            toolWork = new Fix64[_figures.Count];
            // Mining-camp crews dig first (docs/MVP/22 §5.2); what the face does not need of them works at home this season.
            metalPlan.CampWork = WorkCamps(settlement, metalPlan, toolWork, season);
            totalWork -= metalPlan.CampWork;
            totalWork -= metalPlan.Budget;
        }
        // Civic construction (docs/MVP/23 §8): canals, then Markets, Temples, the Palace and the rest, at most CivicLaborShare.
        Fix64 civicWork = Fix64.Zero, civicCanals = Fix64.Zero, civicHousing = Fix64.Zero;
        if (settlement.Civic is { } civicState && CohesionLive(settlement))
        {
            Fix64 canalUpkeep = Fix64.Min(totalWork, Fix64.FromInt(civicState.Canals.Count) / 40); // 0.1 ws a canal a year
            totalWork -= canalUpkeep;
            (civicWork, civicCanals, civicHousing) = PlanCivic(settlement, totalWork);
        }
        if (settlement.Civic is { } civicTraceRequest) civicTraceRequest.TraceCivicRequested = civicWork;
        Fix64 civicAssigned = Fix64.Zero;
        Fix64 upkeepRemaining = upkeepBudget;
        Fix64 need = FoodNeed(settlement, season % 4 == 3);
        if (settlement.UnderInvestment) need *= Fix64.FromRatio(75, 100);
        Fix64 stores = FoodStores(settlement);
        Fix64 foodShare = stores < need ? Fix64.Parse("0.85") : stores < need * 2 ? Fix64.Parse("0.70") : Fix64.Parse("0.48");
        if (settlement.FoodSecurity < Fix64.Parse("0.8")) foodShare = Fix64.Parse("0.9");
        if (ShouldPreserveCityTown(settlement)) foodShare = Fix64.Min(foodShare, Fix64.FromRatio(75, 100));
        if (settlement.Civic is { } civicTraceFood) civicTraceFood.TraceFoodShare = foodShare;
        Fix64 foodWork = totalWork * foodShare;
        // Civic labor comes out of what food leaves, never out of the food work (docs/MVP/23 §8 structures are built in plenty).
        civicWork = Fix64.Min(civicWork, Fix64.Max(Fix64.Zero, totalWork - foodWork));
        if (settlement.Civic is { } civicTraceAssigned) civicTraceAssigned.TraceCivicAssigned = civicWork;
        totalWork -= civicWork;
        foodWork = Fix64.Min(foodWork, totalWork);
        Fix64 buildBudget = Fix64.Max(Fix64.Zero, totalWork - foodWork);
        Fix64 civicRemaining = civicWork;
        // A Market town builds housing ahead, up to three dwellings' labor a season (07d R2); villages build one at a time.
        Fix64 buildCeiling = IsMarketTown(settlement) ? Fix64.FromInt(3) : Fix64.FromInt(1);
        Fix64 buildWork = HasBuildCandidate(settlement)
            ? Fix64.Min(buildBudget, Fix64.Min(buildCeiling, totalWork * Fix64.Parse("0.12")))
            : Fix64.Zero;
        Fix64 materialWork = Fix64.Min(totalWork * Fix64.Parse("0.18"), Fix64.Max(Fix64.Zero, totalWork - foodWork - buildWork));
        Fix64 herdMaintenanceWork = DomesticHerdWorkNeed(settlement);
        Fix64 herdWork = CanTameFromRange(settlement) ? materialWork * Fix64.Half : Fix64.Zero;
        herdWork = Fix64.Min(materialWork, Fix64.Max(herdWork, herdMaintenanceWork));
        materialWork -= herdWork;
        Fix64 farmWork = Fix64.Zero;
        Fix64 fishWork = Fix64.Zero;
        Fix64 forageWork = Fix64.Zero;
        Fix64 huntWork = Fix64.Zero;
        Fix64 chopWork = Fix64.Zero;
        Fix64 gatherWork = Fix64.Zero;
        Fix64 craftWork = Fix64.Zero;
        Fix64 preserveWork = Fix64.Zero;
        int huntPartyTarget = PlannedHuntPartySize(settlement);
        int huntersAssigned = 0;
        // Work-seasons of chopping this season's wood needs (Bronze only): the E3 fuel and a dwelling's timber beyond the stores,
        // at 8 L a work-season, at most half of the material work so clay, stone and the crafts keep their share.
        Fix64 woodShortWork = metalPlan is not null
            ? Fix64.Min(materialWork * Fix64.Half, Fix64.Max(Fix64.Zero, E3FuelNeed(settlement, season % 4 == 3) + Fix64.FromInt(6) - settlement.Stores[(int)Good.Wood]) / Fix64.FromInt(8))
            : Fix64.Zero;

        Fix64 foodRemaining = foodWork;
        Fix64 buildRemaining = buildWork;
        Fix64 materialRemaining = materialWork;
        Fix64 herdRemaining = herdWork;
        Fix64 assignedHerdWork = Fix64.Zero;
        Fix64 assignedBuildWork = Fix64.Zero;
        int foodOrdinal = 0;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState figure = _figures[i];
            if (figure.SettlementId != settlement.Id) continue;
            if (figure.Job == EconomyJob.Scout) continue;
            if (figure.Role != Role.None && figure.Task is MetalTask.Ritual or MetalTask.Records) continue;
            Fix64 available = WorkCapacity(figure) - figure.CareWorkThisSeason - figure.FireWorkThisSeason;
            if (figure.Camp >= 0 && toolWork is not null) available -= toolWork[i];
            if (available <= Fix64.Zero) { figure.Job = EconomyJob.Rest; continue; }
            if (upkeepRemaining > Fix64.Zero)
            {
                Fix64 upkeep = Fix64.Min(available, upkeepRemaining);
                Assign(figure, EconomyJob.Fish, upkeep);
                available -= upkeep;
                upkeepRemaining -= upkeep;
            }
            // Civic builders are adults, taken before the food work so the children's share goes to food (docs/MVP/23 §8).
            if (available > Fix64.Zero && civicRemaining > Fix64.Zero && figure.Age >= 14)
            {
                Fix64 amount = Fix64.Min(available, civicRemaining);
                Assign(figure, EconomyJob.Build, amount);
                if (figure.Task == 0) figure.Task = MetalTask.Build;
                civicAssigned += amount;
                if (toolWork is not null) toolWork[i] += amount;
                available -= amount;
                civicRemaining -= amount;
            }
            if (foodRemaining > Fix64.Zero && available > Fix64.Zero)
            {
                Fix64 amount = Fix64.Min(available, foodRemaining);
                EconomyJob job = PickFoodJob(settlement, season, foodOrdinal++, figure,
                    huntPartyTarget, ref huntersAssigned);
                Assign(figure, job, amount);
                if (job == EconomyJob.Hunt) figure.HuntWorkThisSeason += amount;
                SkillFamily? foodSkill = SkillForJob(job);
                if (foodSkill.HasValue) actionWork[i, (int)foodSkill.Value] += amount;
                AccumulateJob(job, amount, ref forageWork, ref huntWork, ref fishWork, ref farmWork, ref preserveWork);
                if (toolWork is not null && job == EconomyJob.Farm) toolWork[i] += amount;
                available -= amount;
                foodRemaining -= amount;
            }
            if (available > Fix64.Zero && buildRemaining > Fix64.Zero)
            {
                Fix64 amount = Fix64.Min(available, buildRemaining);
                Assign(figure, EconomyJob.Build, amount);
                assignedBuildWork += amount;
                if (toolWork is not null)
                {
                    toolWork[i] += amount;
                    assignedBuildWork += KitBuildBonus(settlement, figure, amount);
                }
                available -= amount;
                buildRemaining -= amount;
            }
            if (metalPlan is not null && toolWork is not null && available > Fix64.Zero && figure.Age >= 14)
            {
                Fix64 open = Unassigned(metalPlan);
                if (open > Fix64.Zero)
                {
                    Fix64 amount = Fix64.Min(available, open);
                    AssignMetalWork(figure, i, amount, metalPlan, toolWork);
                    available -= amount;
                }
            }
            if (available > Fix64.Zero && herdRemaining > Fix64.Zero && figure.Age >= 14 &&
                (herdMaintenanceWork > Fix64.Zero || HasTameableHerdInRange(settlement)))
            {
                Fix64 amount = Fix64.Min(available, herdRemaining);
                Assign(figure, EconomyJob.Herd, amount);
                actionWork[i, (int)SkillFamily.Herd] += amount;
                available -= amount;
                herdRemaining -= amount;
                assignedHerdWork += amount;
            }
            if (available > Fix64.Zero && materialRemaining > Fix64.Zero)
            {
                Fix64 amount = Fix64.Min(available, materialRemaining);
                // Keyed on the figure's own id, not its place in the global list, so deaths elsewhere cannot reshuffle this civilization's jobs (the fog-fairness rule, docs/MVP/15 D10).
                long rotation = figure.Id % 3;
                EconomyJob job = rotation == 0 ? EconomyJob.Craft : rotation == 1 ? EconomyJob.Chop : EconomyJob.Gather;
                // From KEY-3 woodcutters go first while the stores are short of the season's wood (hearths, kilns, building).
                if (woodShortWork > Fix64.Zero && job != EconomyJob.Chop) job = EconomyJob.Chop;
                if (job == EconomyJob.Chop) woodShortWork -= Fix64.Min(woodShortWork, amount);
                Assign(figure, job, amount);
                if (job == EconomyJob.Chop) chopWork += amount;
                else if (job == EconomyJob.Gather) gatherWork += amount;
                else craftWork += amount;
                if (job == EconomyJob.Chop) actionWork[i, (int)SkillFamily.Chop] += amount;
                else if (job == EconomyJob.Gather) actionWork[i, (int)SkillFamily.Gather] += amount;
                if (toolWork is not null) toolWork[i] += amount;
                available -= amount;
                materialRemaining -= amount;
            }
            if (figure.WorkThisSeason <= Fix64.Zero) figure.Job = EconomyJob.Rest;
        }

        // The baseline food mix assumes roughly 70% forage and 30% scavenging. Keep
        // scavenging work within that budget; assigning a fixed three-person hunting
        // party otherwise consumes 40–60% of early food labor at the lower rung-0 rate.
        Fix64 scavengingBudget = foodWork * Fix64.Parse("0.30");
        Fix64 excessScavengingWork = Fix64.Max(Fix64.Zero, huntWork - scavengingBudget);
        for (int i = _figures.Count - 1; i >= 0 && excessScavengingWork > Fix64.Zero; i--)
        {
            FigureState figure = _figures[i];
            if (figure.SettlementId != settlement.Id || figure.HuntWorkThisSeason <= Fix64.Zero) continue;
            Fix64 shifted = Fix64.Min(figure.HuntWorkThisSeason, excessScavengingWork);
            figure.HuntWorkThisSeason -= shifted;
            actionWork[i, (int)SkillFamily.Hunt] -= shifted;
            actionWork[i, (int)SkillFamily.Forage] += shifted;
            huntWork -= shifted;
            forageWork += shifted;
            excessScavengingWork -= shifted;
            if (figure.HuntWorkThisSeason <= Fix64.Zero && figure.Job == EconomyJob.Hunt)
                figure.Job = EconomyJob.Forage;
        }

        ReplaceWornTools(settlement);
        Fix64 forageMod = Modifier(settlement.Civilization, "forage.yield");
        Fix64 appliedForageWork = LimitActionWorkByTool(settlement, "forage", forageWork);
        ScaleSkillWork(actionWork, SkillFamily.Forage, forageWork, appliedForageWork);
        Fix64 productiveForageWork = SkillWeightedWork(actionWork, SkillFamily.Forage);
        Fix64 forageRate = ActionRate("forage", Fix64.Parse("3.2"), settlement, "forage",
            toolWork: appliedForageWork);
        Fix64 foraged = Harvest(settlement, EconomyJob.Forage, productiveForageWork, forageRate * forageMod, Good.Plant, _forageStock, 1);
        WearActionTool(settlement, "forage", appliedForageWork, forageRate * forageMod);
        PracticeSkill(actionWork, SkillFamily.Forage);
        ReclassifyForage(settlement, foraged, season % 4);

        Fix64 appliedHuntWork = LimitActionWorkByTool(settlement, "hunt", huntWork, huntersAssigned);
        Fix64 huntBase = ScavengeRate(settlement, huntersAssigned, appliedHuntWork);
        bool hasHunt = HasResourceInRange(settlement, _herdFoodStock) || CanBowHuntSmallGame(settlement, huntersAssigned);
        Fix64 rate = ActionRate("hunt", huntBase, settlement, "hunt", huntersAssigned, appliedHuntWork);
        ScaleHuntWork(settlement, huntWork, appliedHuntWork);
        ScaleSkillWork(actionWork, SkillFamily.Hunt, huntWork, appliedHuntWork);
        Fix64 productiveHuntWork = SkillWeightedWork(actionWork, SkillFamily.Hunt);
        Fix64 huntInjuryRate = HuntInjuryRate(settlement, huntersAssigned);
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState figure = _figures[i];
            if (figure.SettlementId == settlement.Id && figure.HuntWorkThisSeason > Fix64.Zero)
                figure.HuntingInjuryHazard = figure.HuntWorkThisSeason * huntInjuryRate;
        }
        if (hasHunt)
        {
            bool cooperativeDrive = huntersAssigned >= 6 && HasProject(settlement.Civilization, "HNT-04");
            Fix64 hunted = _hasIndividualHerds
                ? HuntIndividualHerds(settlement, productiveHuntWork, rate, season, cooperativeDrive)
                : Harvest(settlement, EconomyJob.Hunt, productiveHuntWork, rate, Good.Meat, _herdFoodStock, 2,
                    SustainableHuntLimit(settlement));
            if (!_hasIndividualHerds)
            {
                settlement.Stores[(int)Good.Hide] += hunted * Fix64.Parse("0.05");
                settlement.Stores[(int)Good.Bone] += hunted * Fix64.Parse("0.03");
            }
        }
        else if (huntWork > Fix64.Zero)
            Harvest(settlement, EconomyJob.Hunt, productiveHuntWork, rate, Good.Meat, _herdFoodStock, 2, Fix64.Zero);
        WearActionTool(settlement, "hunt", appliedHuntWork, rate, huntersAssigned);
        PracticeSkill(actionWork, SkillFamily.Hunt);

        WeirCatch(settlement, upkeepBudget - upkeepRemaining, season % 4, false);
        if (fishWork > Fix64.Zero)
        {
            Fix64 appliedFishWork = LimitActionWorkByTool(settlement, "fish", fishWork);
            ScaleSkillWork(actionWork, SkillFamily.Fish, fishWork, appliedFishWork);
            Fix64 fishRate = ActionRate("fish", Fix64.One, settlement, "fish", toolWork: appliedFishWork);
            HarvestFish(settlement, SkillWeightedWork(actionWork, SkillFamily.Fish), appliedFishWork, fishRate, season % 4);
            PracticeSkill(actionWork, SkillFamily.Fish);
        }

        if (farmWork > Fix64.Zero)
        {
            Fix64 productiveFarmWork = SkillWeightedWork(actionWork, SkillFamily.Farm);
            if (metalPlan is not null) productiveFarmWork += KitFarmBonus(settlement, actionWork);
            settlement.FarmWorkThisYear += productiveFarmWork;
            PracticeSkill(actionWork, SkillFamily.Farm);
            if (season % 4 == 2) HarvestFields(settlement, season / 4);
        }
        else if (season % 4 == 2) HarvestFields(settlement, season / 4);

        if (chopWork > Fix64.Zero && metalPlan is not null && AnyMetalChopper(settlement, actionWork))
            ChopWithKits(settlement, actionWork, chopWork, WoodChopLimit(settlement, metalPlan) + E3FuelNeed(settlement, season % 4 == 3));
        else if (chopWork > Fix64.Zero)
        {
            Fix64 appliedChopWork = LimitActionWorkByTool(settlement, "chop", chopWork);
            ScaleSkillWork(actionWork, SkillFamily.Chop, chopWork, appliedChopWork);
            Fix64 chopRate = ActionRate("chop", Fix64.FromInt(2), settlement, "chop",
                toolWork: appliedChopWork) * Modifier(settlement.Civilization, "wood_yield");
            if (metalPlan is not null)
                AddMetalStat(settlement.Civilization, StatChopWood, HarvestWoodSpread(settlement,
                    Fix64.Min(SkillWeightedWork(actionWork, SkillFamily.Chop) * chopRate, WoodChopLimit(settlement, metalPlan) + E3FuelNeed(settlement, season % 4 == 3))));
            else
                Harvest(settlement, EconomyJob.Chop, SkillWeightedWork(actionWork, SkillFamily.Chop), chopRate, Good.Wood, _woodStock, 4);
            WearActionTool(settlement, "chop", appliedChopWork, chopRate);
            PracticeSkill(actionWork, SkillFamily.Chop);
        }
        if (gatherWork > Fix64.Zero)
        {
            Fix64 gatherUsed = GatherMaterials(settlement, gatherWork, SkillWeightedWork(actionWork, SkillFamily.Gather));
            ScaleSkillWork(actionWork, SkillFamily.Gather, gatherWork, gatherUsed);
            PracticeSkill(actionWork, SkillFamily.Gather);
        }
        Fix64 craftLeft = Fix64.Zero;
        if (craftWork > Fix64.Zero) craftLeft = CraftTools(settlement, craftWork);
        if (preserveWork > Fix64.Zero) Preserve(settlement, preserveWork);
        if (assignedHerdWork > Fix64.Zero)
        {
            HerdAnimals(settlement, assignedHerdWork, SkillWeightedWork(actionWork, SkillFamily.Herd), season / 4);
            PracticeSkill(actionWork, SkillFamily.Herd);
        }
        if (assignedBuildWork > Fix64.Zero)
        {
            // A walls Corvée feeds the defensive construction queue (22 §4.3, §7.2); other targets
            // accelerate civic works in WorkCivic.
            if (CorveeAt(settlement.Id) == CorveeTarget.Walls) assignedBuildWork *= Fix64.FromRatio(3, 2);
            settlement.BuildProgress += assignedBuildWork;
        }
        if (metalPlan is not null && toolWork is not null) RunMetal(settlement, metalPlan, craftLeft, toolWork, season);
        if (civicAssigned > Fix64.Zero) WorkCivic(settlement, civicAssigned, civicCanals, civicHousing);
        TryCompleteCivic(settlement);
        if (_deadwoodUsed is not null && settlement.Civic is not null) BurnE3Fuel(settlement, season % 4 == 3, _deadwoodUsed);
    }

    private void BeginHealthSeason(FigureState figure, int season)
    {
        figure.DiseaseTreated = false;
        figure.WoundTreated = false;
        figure.MidwifeAttended = false;
        figure.CareRolesThisSeason = 0;
        figure.CareWorkThisSeason = Fix64.Zero;
        if (figure.IllnessSeasons == 0 &&
            Pcg32.ForStream(_seed, IllnessStream, (ulong)figure.Id, season).NextFix01() < HealthEpisodeChance)
            figure.IllnessSeasons = 2;
        if (figure.WoundSeasons == 0 &&
            Pcg32.ForStream(_seed, WoundStream, (ulong)figure.Id, season).NextFix01() < HealthEpisodeChance)
            figure.WoundSeasons = 2;
        UpdateFigureHealth(figure);
    }

    private static void UpdateFigureHealth(FigureState figure)
    {
        // 09 §3.2: a sick woman's fertility contribution is halved. The same health
        // multiplier limits work; wounds and accumulated hunger also reduce capacity.
        Fix64 episodeHealth = figure.IllnessSeasons > 0 ? Fix64.Half : Fix64.One;
        if (figure.WoundSeasons > 0) episodeHealth = Fix64.Min(episodeHealth, Fix64.Parse("0.75"));
        figure.Health = Fix64.Clamp(episodeHealth * (Fix64.One - figure.Hunger * Fix64.Half),
            Fix64.Parse("0.3"), Fix64.One);
    }

    private Fix64 AllocateCare(SettlementState settlement)
    {
        bool remedies = HasProject(settlement.Civilization, "MED-01");
        bool woundCare = HasProject(settlement.Civilization, "MED-02");
        bool midwives = HasProject(settlement.Civilization, "MED-03");
        Fix64 spent = Fix64.Zero;
        // Give time-sensitive births/infants priority, then tend illness and wounds.
        // Patient and provider ties are resolved in stable ascending figure-id order.
        for (int priority = 0; priority < 2; priority++)
        {
            for (int i = 0; i < _figures.Count; i++)
            {
                FigureState patient = _figures[i];
                if (patient.SettlementId != settlement.Id) continue;
                if (priority == 0)
                {
                    if (midwives && (patient.Age == 0 || patient.PregnancySeasons == 1) &&
                        TryAssignCare(patient, MidwifeRole))
                    {
                        patient.MidwifeAttended = true;
                        spent += CarePatientWork;
                    }
                    continue;
                }
                if (remedies && patient.IllnessSeasons > 0 && TryAssignCare(patient, HealerRole))
                {
                    patient.DiseaseTreated = true;
                    spent += CarePatientWork;
                }
                if (woundCare && patient.WoundSeasons > 0 && TryAssignCare(patient, HealerRole))
                {
                    patient.WoundTreated = true;
                    spent += CarePatientWork;
                }
            }
        }
        return spent;
    }

    /// <summary>Reserves eligible adults for scouting under the settlement's scouting policy.</summary>
    private Fix64 AssignScouts(SettlementState settlement, int season)
    {
        int unlockedCount = HasProject(settlement.Civilization, "WAY-01") ? 2 : 1;
        int count = settlement.ScoutPolicy switch
        {
            ScoutPolicy.Zero => 0,
            ScoutPolicy.One => Math.Min(1, unlockedCount),
            ScoutPolicy.Two => Math.Min(2, unlockedCount),
            _ => unlockedCount,
        };
        ScoutPlan plan = SelectScouts(settlement, count);
        if (settlement.ScoutPolicy == ScoutPolicy.Auto && count > 0)
        {
            bool winterShortage = season % 4 == 2 && ForecastWinterFood(settlement, plan) < Fix64.One;
            if (settlement.FoodSecurity < Fix64.One || winterShortage)
                plan = SelectScouts(settlement, --count);
        }
        if (plan.FirstFigure >= 0) AssignScout(plan.FirstFigure);
        if (plan.SecondFigure >= 0) AssignScout(plan.SecondFigure);
        return plan.Work;
    }

    private ScoutPlan SelectScouts(SettlementState settlement, int count)
    {
        int first = -1, second = -1;
        Fix64 work = Fix64.Zero;
        for (int slot = 0; slot < count; slot++)
        {
            int chosen = FindScoutCandidate(settlement, first, second);
            if (chosen < 0) break;
            FigureState figure = _figures[chosen];
            Fix64 available = WorkCapacity(figure) - figure.CareWorkThisSeason - figure.FireWorkThisSeason;
            if (available <= Fix64.Zero) break;
            if (first < 0) first = chosen;
            else second = chosen;
            work += available;
        }
        return new ScoutPlan(first, second, work);
    }

    private int FindScoutCandidate(SettlementState settlement, int excludedFirst, int excludedSecond)
    {
        FigureState? chosen = null;
        int chosenIndex = -1;
        for (int i = 0; i < _figures.Count; i++)
        {
            if (i == excludedFirst || i == excludedSecond) continue;
            FigureState figure = _figures[i];
            if (figure.SettlementId != settlement.Id || figure.Job != EconomyJob.None || figure.Age is < 14 or >= 45 ||
                figure.Role != Role.None || figure.Camp >= 0 || figure.IllnessSeasons > 0 || figure.WoundSeasons > 0 ||
                figure.PregnancySeasons > 0 || WorkCapacity(figure) <= figure.CareWorkThisSeason + figure.FireWorkThisSeason)
                continue;
            if (chosen is null || figure.Health > chosen.Health || figure.Health == chosen.Health && figure.Id < chosen.Id)
            {
                chosen = figure;
                chosenIndex = i;
            }
        }
        return chosenIndex;
    }

    private void AssignScout(int figureIndex)
    {
        FigureState figure = _figures[figureIndex];
        Fix64 available = WorkCapacity(figure) - figure.CareWorkThisSeason - figure.FireWorkThisSeason;
        if (available > Fix64.Zero) Assign(figure, EconomyJob.Scout, available);
    }

    private bool TryAssignCare(FigureState patient, byte role)
    {
        // MED-01 specifies 0.1 ws per patient-season. Wound care and midwifery use
        // that same provisional cost until their own work rates are specified.
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState provider = _figures[i];
            if (provider.SettlementId != patient.SettlementId || provider.Id == patient.Id ||
                provider.Age < 14 || provider.IllnessSeasons > 0 || provider.WoundSeasons > 0 ||
                provider.PregnancySeasons == 1 ||
                WorkCapacity(provider) - provider.CareWorkThisSeason - provider.FireWorkThisSeason < CarePatientWork) continue;
            provider.CareRolesThisSeason |= role;
            provider.CareWorkThisSeason += CarePatientWork;
            provider.WorkThisSeason += CarePatientWork;
            return true;
        }
        return false;
    }

    private EconomyJob PickFoodJob(SettlementState settlement, int season, int ordinal, FigureState figure,
        int huntPartyTarget, ref int huntersAssigned)
    {
        bool fishAvailable = HasAccessibleFishInRange(settlement);
        bool fieldAvailable = settlement.FieldTiles.Count > 0 && HasProject(settlement.Civilization, "AGR-01");
        bool hasHunt = HasResourceInRange(settlement, _herdFoodStock) || CanBowHuntSmallGame(settlement, huntPartyTarget);
        if (hasHunt && figure.Age >= 14 && figure.IllnessSeasons == 0 && figure.WoundSeasons == 0 &&
            WorkCapacity(figure) > Fix64.Zero &&
            huntersAssigned < huntPartyTarget)
        {
            huntersAssigned++;
            return EconomyJob.Hunt;
        }
        if (season % 4 <= 2 && fieldAvailable && ordinal % 4 == 0) return EconomyJob.Farm;
        if (fishAvailable && ordinal % 5 == 0) return EconomyJob.Fish;
        if (HasProject(settlement.Civilization, "FOD-02") && settlement.Structures[(int)Structure.DryingRack] > 0 &&
            settlement.Stores[(int)Good.Meat] > Fix64.FromInt(3) && ordinal % 7 == 0) return EconomyJob.Preserve;
        return EconomyJob.Forage;
    }

    private bool CanTameFromRange(SettlementState settlement)
        => settlement.Structures[(int)Structure.Pasture] > 0 && HasTameableHerdInRange(settlement);

    private bool HasTameableHerdInRange(SettlementState settlement)
    {
        bool smallEnabled = HasProject(settlement.Civilization, "HUS-03") &&
            settlement.DomesticSmallStock < DomesticSmallStockCapacity(settlement);
        bool largeEnabled = HasProject(settlement.Civilization, "HUS-04") &&
            settlement.DomesticCattle + settlement.DomesticPigs < DomesticLargeAnimalCapacity(settlement);
        if (!smallEnabled && !largeEnabled) return HasTameableHorses(settlement);
        int year = checked((int)(_seasonsCompleted / 4));
        foreach (WildHerdState herd in _wildHerds)
        {
            if (herd.CurrentAnimals <= Fix64.Zero || herd.TamingYear != year ||
                herd.TamedThisYear >= herd.TamingLimitThisYear ||
                !IsWithinRange(settlement.SiteTile, herd.CurrentTile)) continue;
            if (smallEnabled && herd.Species == HerdSpecies.WildGoatSheep) return true;
            if (largeEnabled && (herd.Species is HerdSpecies.Aurochs or HerdSpecies.Bison or HerdSpecies.WildBoar)) return true;
        }
        return HasTameableHorses(settlement);
    }

    private void HerdAnimals(SettlementState settlement, Fix64 work, Fix64 productiveWork, int year)
    {
        if (work <= Fix64.Zero || settlement.UnderInvestment || settlement.Structures[(int)Structure.Pasture] <= 0) return;
        Fix64 maintenanceNeed = DomesticHerdWorkNeed(settlement);
        Fix64 maintenanceWork = Fix64.Min(work, maintenanceNeed);
        Fix64 maintenanceShare = maintenanceNeed <= Fix64.Zero ? Fix64.Zero : maintenanceWork / maintenanceNeed;
        settlement.Stores[(int)Good.Meat] += settlement.DomesticSmallStock * Fix64.Parse("0.8") / 4 * maintenanceShare;
        settlement.Stores[(int)Good.Hide] += settlement.DomesticSmallStock * Fix64.Parse("0.2") / 4 * maintenanceShare;
        settlement.Stores[(int)Good.Meat] += settlement.DomesticCattle * Fix64.Parse("2.5") / 4 * maintenanceShare;
        settlement.Stores[(int)Good.Hide] += settlement.DomesticCattle * Fix64.Parse("0.3") / 4 * maintenanceShare;
        settlement.Stores[(int)Good.Meat] += settlement.DomesticPigs * Fix64.Parse("1.2") / 4 * maintenanceShare;
        if (settlement.Civic is not null) Milk(settlement, DomesticAnnualMeat(settlement) / 4 * maintenanceShare);
        // No separate waste inventory exists. Approximate pig waste as stored-food consumption.
        ConsumeStoredFoodValue(settlement,
            settlement.DomesticPigs * Fix64.Parse("0.5") / 4 * maintenanceShare);
        // Maintenance is a fixed physical-work obligation; skills increase the remaining
        // taming effort only after that obligation has been met.
        Fix64 tamingWork = Fix64.Max(Fix64.Zero, productiveWork - maintenanceWork);
        Fix64 animalsToTame = tamingWork / Fix64.Parse("0.5");
        if (HasProject(settlement.Civilization, "HUS-01")) animalsToTame *= Fix64.Parse("1.5");
        Fix64 smallRoom = Fix64.Max(Fix64.Zero, DomesticSmallStockCapacity(settlement) - settlement.DomesticSmallStock);
        Fix64 largeRoom = Fix64.Max(Fix64.Zero, DomesticLargeAnimalCapacity(settlement) - settlement.DomesticCattle - settlement.DomesticPigs);
        for (int i = 0; i < _wildHerds.Length && animalsToTame > Fix64.Zero; i++)
        {
            WildHerdState herd = _wildHerds[i];
            if (herd.CurrentAnimals <= Fix64.Zero || herd.TamingYear != year ||
                !IsWithinRange(settlement.SiteTile, herd.CurrentTile)) continue;
            bool small = herd.Species == HerdSpecies.WildGoatSheep && HasProject(settlement.Civilization, "HUS-03");
            bool cattle = (herd.Species is HerdSpecies.Aurochs or HerdSpecies.Bison) && HasProject(settlement.Civilization, "HUS-04");
            bool pig = herd.Species == HerdSpecies.WildBoar && HasProject(settlement.Civilization, "HUS-04");
            if ((!small && !cattle && !pig) || herd.TamedThisYear >= herd.TamingLimitThisYear) continue;
            Fix64 room = small ? smallRoom : largeRoom;
            Fix64 tame = Fix64.Min(animalsToTame,
                Fix64.Min(room, Fix64.Min(herd.CurrentAnimals, herd.TamingLimitThisYear - herd.TamedThisYear)));
            if (tame <= Fix64.Zero) continue;
            herd.CurrentAnimals -= tame;
            herd.TamedThisYear += tame;
            animalsToTame -= tame;
            if (small)
            {
                settlement.DomesticSmallStock += tame;
                smallRoom -= tame;
            }
            else if (cattle)
            {
                settlement.DomesticCattle += tame;
                largeRoom -= tame;
            }
            else
            {
                settlement.DomesticPigs += tame;
                largeRoom -= tame;
            }
        }
        if (settlement.Civic is not null)
            TameHorses(settlement, animalsToTame * Fix64.Half / (HasProject(settlement.Civilization, "HUS-01") ? Fix64.Parse("1.5") : Fix64.One), year);
        RebuildWildHerdFoodStock();
    }

    private static void AccumulateJob(EconomyJob job, Fix64 amount, ref Fix64 forage, ref Fix64 hunt,
        ref Fix64 fish, ref Fix64 farm, ref Fix64 preserve)
    {
        switch (job)
        {
            case EconomyJob.Forage: forage += amount; break;
            case EconomyJob.Hunt: hunt += amount; break;
            case EconomyJob.Fish: fish += amount; break;
            case EconomyJob.Farm: farm += amount; break;
            case EconomyJob.Preserve: preserve += amount; break;
        }
    }

    private static SkillFamily? SkillForJob(EconomyJob job) => job switch
    {
        EconomyJob.Forage => SkillFamily.Forage,
        EconomyJob.Hunt => SkillFamily.Hunt,
        EconomyJob.Fish => SkillFamily.Fish,
        EconomyJob.Farm => SkillFamily.Farm,
        _ => null,
    };

    private Fix64 SkillWeightedWork(Fix64[,] actionWork, SkillFamily family)
    {
        Fix64 work = Fix64.Zero;
        int column = (int)family;
        foreach (int i in _actionWorkTouchedRows)
        {
            Fix64 assignedWork = actionWork[i, column];
            if (assignedWork == Fix64.Zero) continue;
            work += assignedWork * SkillMultiplier(_figures[i], family);
        }
        return work;
    }

    private void ScaleSkillWork(Fix64[,] actionWork, SkillFamily family, Fix64 assignedWork, Fix64 appliedWork)
    {
        if (assignedWork <= Fix64.Zero || appliedWork >= assignedWork) return;
        Fix64 scale = appliedWork / assignedWork;
        int column = (int)family;
        foreach (int i in _actionWorkTouchedRows) actionWork[i, column] *= scale;
    }

    private void PracticeSkill(Fix64[,] actionWork, SkillFamily family)
    {
        int column = (int)family;
        foreach (int i in _actionWorkTouchedRows)
        {
            Fix64 work = actionWork[i, column];
            if (work <= Fix64.Zero) continue;
            FigureState figure = _figures[i];
            int level = figure.SkillLevels[column];
            if (level >= MaxSkillLevel) continue;
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
    }

    private static Fix64 SkillMultiplier(FigureState figure, SkillFamily family) =>
        Fix64.Parse("0.8") + Fix64.Parse("0.08") * Fix64.FromInt(figure.SkillLevels[(int)family]);

    private Fix64 Harvest(SettlementState settlement, EconomyJob job, Fix64 work, Fix64 rate, Good output,
        Fix64[] stock, int resourceKind, Fix64 maximum = default)
    {
        if (work <= Fix64.Zero || rate <= Fix64.Zero) return Fix64.Zero;
        Fix64 remaining = work * rate;
        if (maximum > Fix64.Zero) remaining = Fix64.Min(remaining, maximum);
        Fix64 harvested = Fix64.Zero;
        foreach (int tile in RangeTiles(settlement.SiteTile))
        {
            if (remaining <= Fix64.Zero) break;
            Fix64 available = stock[tile];
            if (resourceKind == 1) available *= ReachableForageShare(settlement.Civilization);
            if (resourceKind == 2 && available > Fix64.Zero)
            {
                Fix64 limit = _world.Tile(tile).HerdFoodGrowthPerYear * HuntShare(settlement.Civilization) / 4;
                available = Fix64.Min(available, limit);
            }
            Fix64 take = Fix64.Min(remaining, available);
            stock[tile] -= take;
            settlement.Stores[(int)output] += take;
            remaining -= take;
            harvested += take;
        }
        _ = job;
        return harvested;
    }

    private Fix64 HuntIndividualHerds(SettlementState settlement, Fix64 work, Fix64 rate, int season, bool cooperativeDrive,
        bool enforceSustainableLimit = true)
    {
        if (work <= Fix64.Zero || rate <= Fix64.Zero) return Fix64.Zero;
        Fix64 remainingWork = work;
        Fix64 remainingSustainableHarvest = enforceSustainableLimit ? SustainableHuntLimit(settlement) : Fix64.MaxValue;
        Fix64 meat = Fix64.Zero;
        for (int i = 0; i < _wildHerds.Length && remainingWork > Fix64.Zero; i++)
        {
            WildHerdState herd = _wildHerds[i];
            if (herd.CurrentAnimals <= Fix64.Zero || !IsWithinRange(settlement.SiteTile, herd.CurrentTile)) continue;
            bool largeGame = IsLargeGame(herd.Species);
            bool drive = cooperativeDrive && largeGame;
            Fix64 herdRate = cooperativeDrive && !largeGame ? rate * Fix64.Parse("0.6875") : rate;
            Fix64 perAnimal = MeatPerAnimal(herd.Species);
            Fix64 take = Fix64.Min(remainingWork * herdRate, herd.CurrentAnimals * perAnimal);
            if (enforceSustainableLimit)
            {
                take = Fix64.Min(take, remainingSustainableHarvest);
                take = Fix64.Min(take, SustainableHuntLimit(herd, settlement.Civilization));
            }
            Fix64 kills = take / perAnimal;
            herd.CurrentAnimals -= kills;
            if (herd.CurrentAnimals == Fix64.Zero) herd.LastHuntCrashCivilization = settlement.Civilization;
            Fix64 fearGain = kills * Fix64.Parse("0.1") * (drive ? Fix64.FromInt(3) : Fix64.One);
            herd.Fear = Fix64.Min(Fix64.FromInt(4), herd.Fear + fearGain);
            DisplaceHerdIfAfraid(herd, drive, season, settlement);
            settlement.Stores[(int)Good.Meat] += take;
            // Yield convention: hides for animals other than small game, plus 0.3 bone/antler per kill.
            if (herd.Species != HerdSpecies.SmallGame) settlement.Stores[(int)Good.Hide] += kills;
            settlement.Stores[(int)Good.Bone] += kills * Fix64.Parse("0.3");
            remainingWork -= take / herdRate;
            if (enforceSustainableLimit) remainingSustainableHarvest -= take;
            meat += take;
        }
        if (remainingWork > Fix64.Zero && CanBowHuntSmallGame(settlement, PlannedHuntPartySize(settlement)))
        {
            Fix64 available = Fix64.Zero;
            foreach (int tile in RangeTiles(settlement.SiteTile))
                if (IsLandBiome(_world.Tile(tile).BiomeId)) available += Fix64.One;
            Fix64 smallGameRate = rate;
            Fix64 take = Fix64.Min(remainingWork * smallGameRate, available);
            settlement.Stores[(int)Good.Meat] += take;
            settlement.Stores[(int)Good.Bone] += take * Fix64.Parse("0.3");
            meat += take;
        }
        RebuildWildHerdFoodStock();
        return meat;
    }

    private bool CanBowHuntSmallGame(SettlementState settlement, int huntPartySize)
        => BestActionRung(settlement, "hunt", huntPartySize) == 5;

    private static bool IsLargeGame(HerdSpecies species)
        => species is HerdSpecies.Aurochs or HerdSpecies.Bison or HerdSpecies.Horse or HerdSpecies.Reindeer;

    private Fix64 HarvestFish(SettlementState settlement, Fix64 productiveWork, Fix64 physicalWork, Fix64 rate, int season)
    {
        if (productiveWork <= Fix64.Zero || rate <= Fix64.Zero) return Fix64.Zero;
        Fix64 remainingWork = productiveWork;
        Fix64 harvested = Fix64.Zero;
        int passes = season == 2 ? 2 : 1;
        for (int pass = 0; pass < passes && remainingWork > Fix64.Zero; pass++)
        {
            bool salmonTile = season == 2 && pass == 0;
            foreach (int tile in RangeTiles(settlement.SiteTile))
            {
                WorldEconomyTile input = _world.Tile(tile);
                if (!CanFishTile(settlement, tile) ||
                    season == 2 && input.SalmonRun != salmonTile) continue;
                // Salmon season triples tile stock growth, not labor rate. Boats add
                // their separate 50% rate only on deeper water.
                Fix64 tileRate = FishTileRate(settlement, tile, rate);
                Fix64 take = Fix64.Min(remainingWork * tileRate, _fishStock[tile]);
                if (take <= Fix64.Zero) continue;
                _fishStock[tile] -= take;
                settlement.Stores[(int)Good.Fish] += take;
                harvested += take;
                remainingWork -= take / tileRate;
                if (remainingWork <= Fix64.Zero) break;
            }
        }
        WearActionTool(settlement, "fish", physicalWork, rate);
        return harvested;
    }

    private void ReclassifyForage(SettlementState settlement, Fix64 harvested, int seasonOfYear)
    {
        if (harvested <= Fix64.Zero) return;
        if (seasonOfYear == 3 && HasProject(settlement.Civilization, "FOR-02"))
        {
            Fix64 roots = harvested * Fix64.Parse("0.3");
            settlement.Stores[(int)Good.Plant] -= roots;
            settlement.Stores[(int)Good.Roots] += roots;
        }
        if (seasonOfYear == 2 && HasProject(settlement.Civilization, "FOR-03"))
        {
            Fix64 nuts = harvested * Fix64.Parse("0.4");
            settlement.Stores[(int)Good.Plant] -= nuts;
            settlement.Stores[(int)Good.Nuts] += nuts;
        }
        if (seasonOfYear == 2 && HasProject(settlement.Civilization, "FOR-04"))
        {
            Fix64 wildGrain = harvested * Fix64.Parse("0.2");
            settlement.Stores[(int)Good.Plant] -= wildGrain;
            settlement.Stores[(int)Good.WildGrain] += wildGrain;
        }
    }

    private void HarvestFields(SettlementState settlement, int year)
    {
        if (settlement.UnderInvestment || settlement.FieldTiles.Count == 0 || !HasProject(settlement.Civilization, "AGR-01")) return;
        Fix64 required = settlement.Civic is not null ? FieldLaborNeed(settlement) : Fix64.FromInt(settlement.FieldTiles.Count);
        Fix64 factor = required <= Fix64.Zero ? Fix64.One : Fix64.Min(Fix64.One, settlement.FarmWorkThisYear / required);
        Fix64 yield = Fix64.Zero;
        Fix64 soilLossMod = settlement.Civic is not null ? SoilLossModifier(settlement) : Modifier(settlement.Civilization, "soil_fertility_loss");
        foreach (int tile in settlement.FieldTiles)
        {
            WorldEconomyTile input = _world.Tile(tile);
            Fix64 soilRatio = input.SoilFertility <= Fix64.Zero
                ? Fix64.Parse("0.3")
                : Fix64.Clamp(_soil[tile] / input.SoilFertility, Fix64.Parse("0.3"), Fix64.One);
            Fix64 irrigation = Fix64.One;
            if (settlement.Civic is not null)
            {
                irrigation = IrrigationFactor(settlement, tile, out bool fullSoil);
                if (fullSoil) soilRatio = Fix64.One;
            }
            if (IsFieldFlooded(tile, year))
            {
                _soil[tile] = Fix64.Min(input.SoilFertility, _soil[tile] + Fix64.Parse("0.10"));
                // AGR-06's canals and NUM-06's calendar halve what a flood takes (E2: the whole harvest).
                if (settlement.Civic is not null) yield += input.FieldYieldPerYear * soilRatio * irrigation * (Fix64.One - FloodLoss(settlement));
            }
            else
            {
                Fix64 fieldYield = input.FieldYieldPerYear * soilRatio;
                if (irrigation != Fix64.One) fieldYield *= irrigation;
                if (_tendedStandYieldBonus[tile] && factor > Fix64.Zero)
                {
                    fieldYield *= Fix64.Parse("1.25");
                    _tendedStandYieldBonus[tile] = false;
                }
                yield += fieldYield;
                _soil[tile] = factor > Fix64.Zero
                    ? Fix64.Max(Fix64.Zero, _soil[tile] - factor * Fix64.Parse("0.06") * soilLossMod)
                    : Fix64.Min(input.SoilFertility, _soil[tile] + Fix64.Parse("0.10"));
            }
        }
        yield *= factor * Modifier(settlement.Civilization, "field_yield");
        if (HasProject(settlement.Civilization, "AGR-02")) yield *= Fix64.FromInt(2);
        settlement.Stores[(int)Good.Grain] += yield;
        if (HasProject(settlement.Civilization, "BLD-05"))
            settlement.Stores[(int)Good.Straw] += yield;
        settlement.FarmWorkThisYear = Fix64.Zero;
    }

    /// <summary>The E0–E2 craft step; returns the craft work it left unused, which the Bronze craft step may take.</summary>
    private Fix64 CraftTools(SettlementState settlement, Fix64 work)
    {
        if (work <= Fix64.Zero) return Fix64.Zero;
        Fix64 remaining = work;
        int civ = settlement.Civilization;
        if (HasProject(civ, "FOR-07"))
            MakeGoods(settlement, ref remaining, Good.Sapling, Fix64.Parse("0.1"), Good.Wood,
                Fix64.Parse("0.1"), targetStock: Fix64.One);
        if (HasProject(civ, "WOD-01"))
        {
            MakeGoods(settlement, ref remaining, Good.Stick, Fix64.Parse("0.03"), Good.Wood, Fix64.Parse("0.1"), targetStock: Fix64.FromInt(2));
            MakeGoods(settlement, ref remaining, Good.Club, Fix64.Parse("0.04"), Good.Wood, Fix64.Parse("0.2"), targetStock: Fix64.FromInt(3));
        }
        if (HasProject(civ, "HNT-02"))
            MakeGoods(settlement, ref remaining, Good.Spear, Fix64.Parse("0.08"), Good.Wood, Fix64.Parse("0.2"), Good.Stone, Fix64.Parse("0.1"), Fix64.FromInt(3));
        if (HasProject(civ, "HNT-05"))
            MakeGoods(settlement, ref remaining, Good.SpearThrower, Fix64.FromInt(8), Good.Wood,
                Fix64.Parse("0.5"), Good.Cord, Fix64.Parse("0.1"), Fix64.One);
        if (HasProject(civ, "HNT-06"))
            MakeGoods(settlement, ref remaining, Good.Bow, Fix64.FromInt(10), Good.Wood,
                Fix64.Parse("0.3"), Good.Flint, Fix64.Parse("0.1"), Fix64.One);
        if (HasProject(civ, "STN-07"))
            MakeGoods(settlement, ref remaining, Good.Harpoon, Fix64.Parse("0.1"), Good.Bone, Fix64.Parse("0.3"), targetStock: Fix64.One);
        if (HasProject(civ, "HNT-08"))
            MakeGoods(settlement, ref remaining, Good.FishingKit, Fix64.FromInt(8), Good.Bone,
                Fix64.Parse("0.1"), Good.Cord, Fix64.Parse("0.1"), Fix64.One);
        if (HasProject(civ, "FOR-04"))
            MakeGoods(settlement, ref remaining, Good.Sickle, Fix64.Parse("0.08"), Good.Stone, Fix64.Parse("0.2"), Good.Wood, Fix64.Parse("0.05"), targetStock: Fix64.One);
        if (HasProject(civ, "FIB-01"))
        {
            Fix64 cordBatches = Fix64.Min(remaining / Fix64.Parse("0.1"),
                Fix64.Min(settlement.Stores[(int)Good.Fiber] / Fix64.Half,
                    Fix64.Max(Fix64.Zero, Fix64.FromInt(20) - settlement.Stores[(int)Good.Cord]) / Fix64.FromInt(10)));
            settlement.Stores[(int)Good.Fiber] -= cordBatches * Fix64.Half;
            settlement.Stores[(int)Good.Cord] += cordBatches * Fix64.FromInt(10);
            remaining -= cordBatches * Fix64.Parse("0.1");
        }
        if (HasProject(civ, "FIB-05"))
        {
            MakeGoods(settlement, ref remaining, Good.Basket, Fix64.Parse("0.15"), Good.Fiber, Fix64.Half, targetStock: Fix64.FromInt(2));
            MakeGoods(settlement, ref remaining, Good.Net, Fix64.FromInt(12), Good.Cord, Fix64.FromInt(20), targetStock: Fix64.One);
        }
        if (HasProject(civ, "FIB-02"))
            MakeGoods(settlement, ref remaining, Good.Wrap, Fix64.Parse("0.1"), Good.Hide, Fix64.FromInt(2), targetStock: Fix64.FromInt(2));
        if (HasProject(civ, "FIB-03"))
            MakeGoods(settlement, ref remaining, Good.Leather, Fix64.Parse("0.2"), Good.Hide, Fix64.One, targetStock: Fix64.FromInt(6));
        if (HasProject(civ, "FIB-04"))
            MakeGoods(settlement, ref remaining, Good.Tailored, Fix64.Parse("0.3"), Good.Leather, Fix64.FromInt(3), Good.Bone, Fix64.Parse("0.3"), Fix64.One);

        if (HasProject(civ, "STN-08"))
            MakeGoods(settlement, ref remaining, Good.Tool7, Fix64.Parse("0.3"), Good.Stone, Fix64.Parse("0.4"), targetStock: Fix64.FromInt(2));
        if (HasProject(civ, "STN-06"))
            MakeGoods(settlement, ref remaining, Good.Tool6, Fix64.Parse("0.09"), Good.Flint, Fix64.Parse("0.2"), Good.Wood, Fix64.Parse("0.1"), Fix64.FromInt(2));
        if (HasProject(civ, "STN-05"))
            MakeGoods(settlement, ref remaining, Good.Tool5, Fix64.Parse("0.07"), Good.Flint, Fix64.Parse("0.25"), targetStock: Fix64.FromInt(2));
        if (HasProject(civ, "STN-04"))
            MakeGoods(settlement, ref remaining, Good.Tool4, Fix64.Parse("0.12"), Good.Stone, Fix64.Parse("0.3"), Good.Wood, Fix64.Parse("0.1"), Fix64.FromInt(2));
        if (HasProject(civ, "STN-03"))
            MakeGoods(settlement, ref remaining, Good.Tool3, Fix64.Parse("0.08"), Good.Stone, Fix64.Parse("0.28"), targetStock: Fix64.FromInt(2));
        if (HasProject(civ, "STN-02"))
            MakeGoods(settlement, ref remaining, Good.Tool2, Fix64.Parse("0.08"), Good.Stone, Fix64.Parse("0.4"), targetStock: Fix64.FromInt(2));
        if (HasProject(civ, "STN-01"))
            MakeGoods(settlement, ref remaining, Good.Tool1, Fix64.Parse("0.05"), Good.Stone, Fix64.Parse("0.3"), targetStock: Fix64.FromInt(2));

        if (HasProject(civ, "BLD-05") && remaining > Fix64.Zero)
        {
            // One recipe batch makes ten bricks from one clay and one straw; bricks dry for a season.
            Fix64 stockRoom = Fix64.Max(Fix64.Zero,
                (settlement.Civic is null ? Fix64.FromInt(200) : MudbrickTarget(settlement)) - settlement.Stores[(int)Good.Mudbrick] - settlement.DryingMudbrick);
            Fix64 batches = Fix64.Min(remaining / Fix64.Parse("0.3"),
                Fix64.Min(settlement.Stores[(int)Good.Clay],
                    Fix64.Min(settlement.Stores[(int)Good.Straw], stockRoom / Fix64.FromInt(10))));
            if (batches > Fix64.Zero)
            {
                settlement.Stores[(int)Good.Clay] -= batches;
                settlement.Stores[(int)Good.Straw] -= batches;
                settlement.DryingMudbrick += batches * Fix64.FromInt(10);
                remaining -= batches * Fix64.Parse("0.3");
            }
        }
        return Fix64.Max(Fix64.Zero, remaining);
    }

    private static void AdvanceMudbrickDrying(SettlementState settlement)
    {
        if (settlement.DryingMudbrick <= Fix64.Zero) return;
        settlement.Stores[(int)Good.Mudbrick] += settlement.DryingMudbrick;
        settlement.DryingMudbrick = Fix64.Zero;
    }

    private void AdvanceOrchards(SettlementState settlement, int season)
    {
        for (int i = 0; i < settlement.OrchardPlots.Count; i++)
        {
            OrchardPlot orchard = settlement.OrchardPlots[i];
            if (orchard.AgeSeasons < 224) orchard.AgeSeasons++;
            if (season % 4 == 2 && orchard.AgeSeasons is >= 24 and < 224)
                settlement.Stores[(int)Good.Fruit] += Fix64.FromInt(3);
        }
    }

    private void MakeGoods(SettlementState settlement, ref Fix64 work, Good output, Fix64 workPerUnit,
        Good inputA, Fix64 amountA, Good inputB = Good.Count, Fix64 amountB = default, Fix64 targetStock = default)
    {
        if (workPerUnit <= Fix64.Zero || amountA <= Fix64.Zero || work <= Fix64.Zero) return;
        Fix64 durability = ToolDurability(settlement, output);
        if (durability > Fix64.Zero && settlement.Stores[(int)output] > Fix64.Zero &&
            ToolQuality(settlement, output) <= Fix64.Parse("0.7")) targetStock += Fix64.One;
        Fix64 batch = Fix64.Min(work / workPerUnit,
            Fix64.Max(Fix64.Zero, targetStock - settlement.Stores[(int)output]));
        batch = Fix64.Min(batch, settlement.Stores[(int)inputA] / amountA);
        if (inputB != Good.Count && amountB > Fix64.Zero)
            batch = Fix64.Min(batch, settlement.Stores[(int)inputB] / amountB);
        if (batch <= Fix64.Zero) return;
        settlement.Stores[(int)inputA] -= batch * amountA;
        if (inputB != Good.Count && amountB > Fix64.Zero) settlement.Stores[(int)inputB] -= batch * amountB;
        settlement.Stores[(int)output] += batch;
        if (durability > Fix64.Zero) settlement.ToolRemainingWork[(int)output] += batch * durability;
        work -= batch * workPerUnit;
    }

    private Fix64 GatherMaterials(SettlementState settlement, Fix64 work, Fix64 productiveWork)
    {
        int civ = settlement.Civilization;
        Fix64 remaining = settlement.Stores[(int)Good.Tool1] >= Fix64.One
            ? Fix64.Min(work, AvailableToolWork(settlement, Good.Tool1))
            : work;
        Fix64 remainingProductive = work <= Fix64.Zero ? Fix64.Zero : productiveWork * remaining / work;
        Fix64 startingRemaining = remaining;
        if (HasProject(civ, "MIN-01"))
        {
            Fix64 flintWork = Fix64.Min(remaining * Fix64.Parse("0.4"), Fix64.Max(Fix64.Zero, Fix64.FromInt(6) - settlement.Stores[(int)Good.Flint]));
            if (flintWork > Fix64.Zero && HasResourceInRange(settlement, _flintStock))
            {
                Fix64 productive = remaining <= Fix64.Zero ? Fix64.Zero : remainingProductive * flintWork / remaining;
                Fix64 rate = GatherRate(settlement,
                    ActionRate("collect_flint_ochre", Fix64.One, settlement, "collect_flint_ochre"), flintWork);
                Fix64 gathered = Harvest(settlement, EconomyJob.Gather, productive,
                    rate, Good.Flint, _flintStock, 7);
                WearGatherTool(settlement, flintWork);
                remaining -= flintWork;
                remainingProductive -= productive;
            }
        }
        if (HasProject(civ, "CER-01") || HasProject(civ, "BLD-05"))
        {
            Fix64 clayWork = Fix64.Min(remaining * Fix64.Parse("0.4"), Fix64.Max(Fix64.Zero, Fix64.FromInt(10) - settlement.Stores[(int)Good.Clay]) / Fix64.FromInt(3));
            if (clayWork > Fix64.Zero && HasResourceInRange(settlement, _clayStock))
            {
                Fix64 productive = remaining <= Fix64.Zero ? Fix64.Zero : remainingProductive * clayWork / remaining;
                Fix64 rate = GatherRate(settlement,
                    ActionRate("gather_clay", Fix64.FromInt(3), settlement, "gather_clay"), clayWork);
                Fix64 gathered = Harvest(settlement, EconomyJob.Gather, productive,
                    rate, Good.Clay, _clayStock, 6);
                WearGatherTool(settlement, clayWork);
                remaining -= clayWork;
                remainingProductive -= productive;
            }
            else if (settlement.Civic is not null && CivInBronze(civ))
            {
                // From KEY-3 clay is dug where there is no deposit, as for the metal structures (WP6 decision 4): 1 L a
                // work-season at a riverbank, lake or wetland, 0.5 from subsoil; up to 20 L, the bricks of a town's buildings.
                Fix64 rate = ClayRate(settlement, out _);
                Fix64 digWork = Fix64.Min(remaining * Fix64.Parse("0.4"), Fix64.Max(Fix64.Zero, Fix64.FromInt(20) - settlement.Stores[(int)Good.Clay]) / rate);
                if (digWork > Fix64.Zero)
                {
                    Fix64 productive = remaining <= Fix64.Zero ? Fix64.Zero : remainingProductive * digWork / remaining;
                    settlement.Stores[(int)Good.Clay] += productive * rate;
                    remaining -= digWork;
                    remainingProductive -= productive;
                }
            }
        }
        if (HasProject(civ, "FIB-01"))
        {
            Fix64 fiberWork = Fix64.Min(remaining * Fix64.Parse("0.4"), Fix64.Max(Fix64.Zero, Fix64.FromInt(6) - settlement.Stores[(int)Good.Fiber]) / Fix64.Parse("0.5"));
            if (fiberWork > Fix64.Zero)
            {
                Fix64 productive = remaining <= Fix64.Zero ? Fix64.Zero : remainingProductive * fiberWork / remaining;
                Fix64 rate = GatherRate(settlement, Fix64.Parse("0.5"), fiberWork);
                Fix64 gathered = Harvest(settlement, EconomyJob.Gather, productive, rate, Good.Fiber, _forageStock, 1);
                WearGatherTool(settlement, fiberWork);
                remaining -= fiberWork;
                remainingProductive -= productive;
            }
        }
        if (remaining > Fix64.Zero)
        {
            Fix64 rate = GatherRate(settlement, Fix64.One, remaining);
            Fix64 gathered = Harvest(settlement, EconomyJob.Gather, remainingProductive, rate, Good.Stone, _stoneStock, 5,
                Fix64.Max(Fix64.Zero, Fix64.FromInt(20) - settlement.Stores[(int)Good.Stone]));
            WearGatherTool(settlement, remaining);
        }
        return startingRemaining;
    }

    private static void Preserve(SettlementState settlement, Fix64 work)
    {
        Fix64 capacity = work * Fix64.FromInt(80);
        Fix64 converted = Fix64.Min(capacity, settlement.Stores[(int)Good.Meat]);
        settlement.Stores[(int)Good.Meat] -= converted;
        settlement.Stores[(int)Good.DriedFood] += converted;
    }

    private void BuildIfReady(SettlementState settlement)
    {
        if (settlement.BuildProgress <= Fix64.Zero) return;
        Structure candidate = settlement.BuildTarget;
        if (candidate == Structure.Count || !CanBuild(settlement.Civilization, candidate))
        {
            candidate = SelectBuildCandidate(settlement);
            settlement.BuildTarget = candidate;
        }
        if (candidate == Structure.Count) return;
        if (candidate == Structure.Orchard && !CanPlaceOrchard(settlement))
        {
            settlement.BuildTarget = Structure.Count;
            return;
        }
        if (candidate == Structure.Weir && !CanPlaceWeir(settlement))
        {
            settlement.BuildTarget = Structure.Count;
            return;
        }
        Fix64 labor = StructureBuildLabor(settlement, candidate);
        if (settlement.BuildProgress < labor) return;
        if (!CanPayStructure(settlement, candidate)) return;
        PayStructure(settlement, candidate);
        settlement.Structures[(int)candidate]++;
        settlement.BuildProgress -= labor;
        settlement.BuildTarget = Structure.Count;
        if (candidate == Structure.Field) PlaceField(settlement);
        else if (candidate == Structure.Orchard) PlaceOrchard(settlement);
        else if (candidate == Structure.Weir) RebuildWeirOwnership();
    }

    private bool HasBuildCandidate(SettlementState settlement)
    {
        if (IsPriorityDefenseTarget(settlement)) return true;
        Structure candidate = SelectBuildCandidate(settlement);
        if (candidate == Structure.Count) return false;
        settlement.BuildTarget = candidate;
        return true;
    }

    /// <summary>
    /// Starts the next eligible physical defense at an exposed settlement identified from an AI's belief state.
    /// An existing construction job is allowed to finish before the priority is applied.
    /// </summary>
    internal bool PrioritizePhysicalDefenseBuild(int settlementIndex)
    {
        if ((uint)settlementIndex >= (uint)_settlements.Count) return false;
        SettlementState settlement = _settlements[settlementIndex];
        if (HasPhysicalDefense(settlement)) return false;
        if (settlement.BuildProgress > Fix64.Zero) return false;

        Structure candidate = SelectPhysicalDefenseCandidate(settlement);
        if (candidate == Structure.Count || settlement.BuildTarget == candidate) return false;
        settlement.BuildTarget = candidate;
        return true;
    }

    private bool IsPriorityDefenseTarget(SettlementState settlement)
        => !HasPhysicalDefense(settlement) && IsPhysicalDefense(settlement.BuildTarget) &&
           CanBuild(settlement.Civilization, settlement.BuildTarget) &&
           (settlement.BuildTarget != Structure.Hillfort || HasHillfortSite(settlement));

    private Structure SelectPhysicalDefenseCandidate(SettlementState settlement)
    {
        if (CanBuild(settlement.Civilization, Structure.Palisade) &&
            settlement.Structures[(int)Structure.Palisade] == 0 &&
            settlement.Stores[(int)Good.Wood] >= Fix64.FromInt(20 * ((int)Tier(settlement.Id) + 1)))
            return Structure.Palisade;
        if (CanBuild(settlement.Civilization, Structure.Hillfort) &&
            settlement.Structures[(int)Structure.Hillfort] == 0 && HasHillfortSite(settlement) &&
            settlement.Stores[(int)Good.Wood] >= Fix64.FromInt(10))
            return Structure.Hillfort;
        if (CanBuild(settlement.Civilization, Structure.StoneWalls) &&
            settlement.Structures[(int)Structure.StoneWalls] < (int)Tier(settlement.Id) + 1 &&
            CanPayStructure(settlement, Structure.StoneWalls))
            return Structure.StoneWalls;
        return Structure.Count;
    }

    private static bool IsPhysicalDefense(Structure structure)
        => structure is Structure.Palisade or Structure.Hillfort or Structure.StoneWalls;

    private static bool HasPhysicalDefense(SettlementState settlement)
        => settlement.Structures[(int)Structure.Palisade] > 0 || settlement.Structures[(int)Structure.Hillfort] > 0 ||
           settlement.Structures[(int)Structure.StoneWalls] > 0;

    private Structure SelectBuildCandidate(SettlementState settlement)
    {
        int population = Population(settlement.Id);
        bool needsHousing = HousingCap(settlement) < (ShouldBuildMarketHousingAhead(settlement)
            ? HousingTarget(settlement) : Fix64.FromInt(population + 8));
        bool hasWarmShelter = settlement.Structures[(int)Structure.PitHouse] > 0 ||
            settlement.Structures[(int)Structure.WattleHouse] > 0 || settlement.Structures[(int)Structure.MudbrickHouse] > 0 ||
            settlement.Structures[(int)Structure.Longhouse] > 0;
        bool hasBasicShelter = hasWarmShelter || settlement.Structures[(int)Structure.LeanTo] > 0 ||
            settlement.Structures[(int)Structure.HideTent] > 0;
        bool extraLeanToForMarketCandidate = settlement.Civic is null &&
            ShouldBuildMarketHousingAhead(settlement) && needsHousing;
        if (CanBuild(settlement.Civilization, Structure.StoragePit) && settlement.Structures[(int)Structure.StoragePit] == 0 &&
            FoodStores(settlement) >= Fix64.FromInt(5)) return Structure.StoragePit;
        if (CanBuild(settlement.Civilization, Structure.DryingRack) && settlement.Structures[(int)Structure.DryingRack] == 0 &&
            settlement.Stores[(int)Good.Wood] >= Fix64.Half && settlement.Stores[(int)Good.Meat] >= Fix64.FromInt(2)) return Structure.DryingRack;
        if (CanBuild(settlement.Civilization, Structure.Granary) && settlement.Structures[(int)Structure.Granary] == 0 &&
            settlement.Stores[(int)Good.Mudbrick] >= Fix64.FromInt(80)) return Structure.Granary;
        if (CanBuild(settlement.Civilization, Structure.PitHouse) && !hasWarmShelter && hasBasicShelter &&
            WarmthDeficit(settlement, true) > 0 && settlement.Stores[(int)Good.Wood] >= Fix64.FromInt(4)) return Structure.PitHouse;
        if (CanBuild(settlement.Civilization, Structure.WattleHouse) && !hasWarmShelter && hasBasicShelter &&
            WarmthDeficit(settlement, true) > 0 && settlement.Stores[(int)Good.Wood] >= Fix64.FromInt(3) &&
            settlement.Stores[(int)Good.Clay] >= Fix64.FromInt(2)) return Structure.WattleHouse;
        if (CanBuild(settlement.Civilization, Structure.LeanTo) && (!hasBasicShelter || extraLeanToForMarketCandidate) &&
            (needsHousing || WarmthDeficit(settlement, true) > 0) && settlement.Stores[(int)Good.Wood] >= Fix64.One)
            return Structure.LeanTo;
        if (CanBuild(settlement.Civilization, Structure.Hearth) && settlement.Structures[(int)Structure.Hearth] == 0 &&
            settlement.Stores[(int)Good.Stone] >= Fix64.Half) return Structure.Hearth;
        if (CanBuild(settlement.Civilization, Structure.Field) && FieldLimit(settlement) > settlement.FieldTiles.Count)
            return Structure.Field;
        if (CanBuild(settlement.Civilization, Structure.PitHouse) && settlement.Structures[(int)Structure.PitHouse] == 0 &&
            needsHousing && settlement.Stores[(int)Good.Wood] >= Fix64.FromInt(4)) return Structure.PitHouse;
        if (CanBuild(settlement.Civilization, Structure.HideTent) && settlement.Structures[(int)Structure.HideTent] == 0 &&
            settlement.Stores[(int)Good.Hide] >= Fix64.FromInt(3) && settlement.Stores[(int)Good.Wood] >= Fix64.One)
            return Structure.HideTent;
        if (CanBuild(settlement.Civilization, Structure.WattleHouse) && needsHousing && settlement.Stores[(int)Good.Wood] >= Fix64.FromInt(3) &&
            settlement.Stores[(int)Good.Clay] >= Fix64.FromInt(2)) return Structure.WattleHouse;
        if (CanBuild(settlement.Civilization, Structure.MudbrickHouse) && needsHousing &&
            settlement.Stores[(int)Good.Mudbrick] >= Fix64.FromInt(60)) return Structure.MudbrickHouse;
        if (CanBuild(settlement.Civilization, Structure.Longhouse) && needsHousing && settlement.Stores[(int)Good.Wood] >= Fix64.FromInt(12) &&
            settlement.Stores[(int)Good.Clay] >= Fix64.FromInt(4)) return Structure.Longhouse;
        if (CanBuild(settlement.Civilization, Structure.Kiln) && settlement.Structures[(int)Structure.Kiln] == 0 &&
            settlement.Stores[(int)Good.Clay] >= Fix64.FromInt(3) &&
            settlement.Stores[(int)Good.Stone] >= Fix64.FromInt(2)) return Structure.Kiln;
        if (CanBuild(settlement.Civilization, Structure.Quern) && settlement.Structures[(int)Structure.Quern] == 0 &&
            settlement.Stores[(int)Good.Stone] >= Fix64.One) return Structure.Quern;
        if (CanBuild(settlement.Civilization, Structure.ThornBarrier) && settlement.Structures[(int)Structure.ThornBarrier] == 0 &&
            settlement.Stores[(int)Good.Wood] >= Fix64.FromInt(2)) return Structure.ThornBarrier;
        if (CanBuild(settlement.Civilization, Structure.Lookout) && settlement.Structures[(int)Structure.Lookout] == 0 &&
            settlement.Stores[(int)Good.Wood] >= Fix64.One) return Structure.Lookout;
        if (CanBuild(settlement.Civilization, Structure.Hillfort) && settlement.Structures[(int)Structure.Hillfort] == 0 &&
            HasHillfortSite(settlement) && settlement.Stores[(int)Good.Wood] >= Fix64.FromInt(10)) return Structure.Hillfort;
        if (CanBuild(settlement.Civilization, Structure.Palisade) && settlement.Structures[(int)Structure.Palisade] == 0 &&
            settlement.Stores[(int)Good.Wood] >= Fix64.FromInt(20 * ((int)Tier(settlement.Id) + 1))) return Structure.Palisade;
        if (CanBuild(settlement.Civilization, Structure.StoneWalls) &&
            settlement.Structures[(int)Structure.StoneWalls] < (int)Tier(settlement.Id) + 1)
            return Structure.StoneWalls;
        if (CanBuild(settlement.Civilization, Structure.Weir) &&
            CanPlaceWeir(settlement) && settlement.Stores[(int)Good.Wood] >= Fix64.FromInt(3) &&
            settlement.Stores[(int)Good.Stone] >= Fix64.FromInt(2)) return Structure.Weir;
        if (CanBuild(settlement.Civilization, Structure.Pasture) && settlement.Structures[(int)Structure.Pasture] < PastureLimit(settlement) &&
            settlement.Stores[(int)Good.Wood] >= Fix64.One)
            return Structure.Pasture;
        if (CanBuild(settlement.Civilization, Structure.Orchard) && CanPlaceOrchard(settlement) &&
            settlement.Stores[(int)Good.Sapling] >= Fix64.One)
            return Structure.Orchard;
        return Structure.Count;
    }

    private bool CanBuild(int civ, Structure structure)
    {
        string id = StructureId(structure);
        for (int i = 0; i < _content.Effects.Length; i++)
        {
            EconomyEffect effect = _content.Effects[i];
            if (effect.Kind == "build" && effect.Structure == id && _completedProjects[civ][effect.ProjectIndex]) return true;
        }
        return false;
    }

    private static string StructureId(Structure structure) => structure switch
    {
        Structure.Hearth => "hearth", Structure.LeanTo => "lean_to", Structure.HideTent => "hide_tent",
        Structure.PitHouse => "pit_house", Structure.WattleHouse => "wattle_daub_house",
        Structure.MudbrickHouse => "mudbrick_house", Structure.Longhouse => "longhouse",
        Structure.StoragePit => "storage_pit", Structure.DryingRack => "drying_rack", Structure.Granary => "granary",
        Structure.Kiln => "kiln", Structure.Quern => "quern", Structure.ThornBarrier => "thorn_barrier",
        Structure.Lookout => "lookout_post", Structure.Weir => "weir", Structure.Field => "field",
        Structure.Pasture => "pasture", Structure.Orchard => "orchard", Structure.Hillfort => "hillfort",
        Structure.StoneWalls => "stone_walls", Structure.Palisade => "palisade", _ => "",
    };

    private static Fix64 BuildLabor(Structure structure) => structure switch
    {
        Structure.Hearth => Fix64.Parse("0.2"), Structure.LeanTo => Fix64.Parse("0.3"),
        Structure.HideTent => Fix64.Parse("0.4"), Structure.PitHouse => Fix64.FromInt(2),
        Structure.WattleHouse => Fix64.Parse("2.5"), Structure.MudbrickHouse => Fix64.FromInt(3),
        Structure.Longhouse => Fix64.FromInt(8), Structure.StoragePit => Fix64.Parse("0.5"),
        Structure.DryingRack => Fix64.Parse("0.2"), Structure.Granary => Fix64.FromInt(4),
        Structure.Kiln => Fix64.FromInt(2), Structure.Quern => Fix64.Parse("0.5"),
        Structure.ThornBarrier => Fix64.Parse("0.6"), Structure.Lookout => Fix64.Parse("0.5"),
        Structure.Weir => Fix64.Parse("1.5"), Structure.Pasture => Fix64.Parse("0.2"), Structure.Orchard => Fix64.One,
        Structure.Field => Fix64.One, _ => Fix64.One,
    };

    private Fix64 StructureBuildLabor(SettlementState settlement, Structure structure)
    {
        if (structure == Structure.StoneWalls) return Fix64.FromInt(30 * ((int)Tier(settlement.Id) + 1));
        if (structure == Structure.Palisade) return Fix64.FromInt(6 * ((int)Tier(settlement.Id) + 1));
        return BuildLabor(structure);
    }

    /// <summary>DEF-04: a settlement can raise one hillfort when a high, non-mountain tile lies within four hexes.</summary>
    private bool HasHillfortSite(SettlementState settlement)
    {
        foreach (int tile in _fortificationRangeTiles[settlement.SiteTile])
        {
            WorldEconomyTile candidate = _world.Tile(tile);
            if (candidate.Elevation >= 150 && (Biome)candidate.BiomeId is not (Biome.Mountain or Biome.Peak or Biome.Ocean or Biome.Coast))
                return true;
        }
        return false;
    }

    private bool CanPayStructure(SettlementState s, Structure structure) => structure switch
    {
        Structure.Hearth => s.Stores[(int)Good.Stone] >= Fix64.Half,
        Structure.LeanTo => s.Stores[(int)Good.Wood] >= Fix64.One,
        Structure.HideTent => s.Stores[(int)Good.Hide] >= Fix64.FromInt(3) && s.Stores[(int)Good.Wood] >= Fix64.One,
        Structure.PitHouse => s.Stores[(int)Good.Wood] >= Fix64.FromInt(4),
        Structure.WattleHouse => s.Stores[(int)Good.Wood] >= Fix64.FromInt(3) && s.Stores[(int)Good.Clay] >= Fix64.FromInt(2),
        Structure.Longhouse => s.Stores[(int)Good.Wood] >= Fix64.FromInt(12) && s.Stores[(int)Good.Clay] >= Fix64.FromInt(4),
        Structure.Kiln => s.Stores[(int)Good.Clay] >= Fix64.FromInt(3) && s.Stores[(int)Good.Stone] >= Fix64.FromInt(2),
        Structure.Quern => s.Stores[(int)Good.Stone] >= Fix64.One,
        Structure.ThornBarrier => s.Stores[(int)Good.Wood] >= Fix64.FromInt(2),
        Structure.Lookout => s.Stores[(int)Good.Wood] >= Fix64.One,
        Structure.Weir => s.Stores[(int)Good.Wood] >= Fix64.FromInt(3) && s.Stores[(int)Good.Stone] >= Fix64.FromInt(2),
        Structure.Pasture => s.Stores[(int)Good.Wood] >= Fix64.One,
        Structure.MudbrickHouse => s.Stores[(int)Good.Mudbrick] >= Fix64.FromInt(60),
        Structure.Granary => s.Stores[(int)Good.Mudbrick] >= Fix64.FromInt(80),
        Structure.Orchard => s.Stores[(int)Good.Sapling] >= Fix64.One,
        Structure.Hillfort => s.Stores[(int)Good.Wood] >= Fix64.FromInt(10),
        Structure.StoneWalls => CutStoneHeld(s, blocksToo: false) >= Fix64.FromInt(60 * ((int)Tier(s.Id) + 1)),
        Structure.Palisade => s.Stores[(int)Good.Wood] >= Fix64.FromInt(20 * ((int)Tier(s.Id) + 1)),
        Structure.StoragePit or Structure.Field => true,
        Structure.DryingRack => s.Stores[(int)Good.Wood] >= Fix64.Half,
        _ => false,
    };

    private void PayStructure(SettlementState s, Structure structure)
    {
        switch (structure)
        {
            case Structure.Hearth: s.Stores[(int)Good.Stone] -= Fix64.Half; break;
            case Structure.LeanTo: s.Stores[(int)Good.Wood] -= Fix64.One; break;
            case Structure.HideTent:
                s.Stores[(int)Good.Hide] -= Fix64.FromInt(3); s.Stores[(int)Good.Wood] -= Fix64.One; break;
            case Structure.PitHouse: s.Stores[(int)Good.Wood] -= Fix64.FromInt(4); break;
            case Structure.WattleHouse:
                s.Stores[(int)Good.Wood] -= Fix64.FromInt(3); s.Stores[(int)Good.Clay] -= Fix64.FromInt(2); break;
            case Structure.Longhouse:
                s.Stores[(int)Good.Wood] -= Fix64.FromInt(12); s.Stores[(int)Good.Clay] -= Fix64.FromInt(4); break;
            case Structure.Kiln:
                s.Stores[(int)Good.Clay] -= Fix64.FromInt(3); s.Stores[(int)Good.Stone] -= Fix64.FromInt(2); break;
            case Structure.Quern: s.Stores[(int)Good.Stone] -= Fix64.One; break;
            case Structure.ThornBarrier: s.Stores[(int)Good.Wood] -= Fix64.FromInt(2); break;
            case Structure.Lookout: s.Stores[(int)Good.Wood] -= Fix64.One; break;
            case Structure.Weir:
                s.Stores[(int)Good.Wood] -= Fix64.FromInt(3); s.Stores[(int)Good.Stone] -= Fix64.FromInt(2); break;
            case Structure.Pasture: s.Stores[(int)Good.Wood] -= Fix64.One; break;
            case Structure.MudbrickHouse: s.Stores[(int)Good.Mudbrick] -= Fix64.FromInt(60); break;
            case Structure.Granary: s.Stores[(int)Good.Mudbrick] -= Fix64.FromInt(80); break;
            case Structure.Orchard: s.Stores[(int)Good.Sapling] -= Fix64.One; break;
            case Structure.Hillfort: s.Stores[(int)Good.Wood] -= Fix64.FromInt(10); break;
            case Structure.Palisade: s.Stores[(int)Good.Wood] -= Fix64.FromInt(20 * ((int)Tier(s.Id) + 1)); break;
            case Structure.StoneWalls:
                if (s.Metal is { } metal)
                    metal.Goods[(int)MetalGood.Ashlar] -= Fix64.FromInt(60 * ((int)Tier(s.Id) + 1));
                break;
            case Structure.DryingRack: s.Stores[(int)Good.Wood] -= Fix64.Half; break;
        }
    }

    private bool CanPlaceOrchard(SettlementState settlement)
    {
        foreach (int tile in RangeTiles(settlement.SiteTile))
        {
            if (tile != settlement.SiteTile && _fieldOwner[tile] < 0 && !IsOrchardOccupied(tile) && IsOrchardGround(tile))
                return true;
        }
        return false;
    }

    private bool PlaceOrchard(SettlementState settlement)
    {
        int best = -1;
        Fix64 bestScore = Fix64.Zero;
        foreach (int tile in RangeTiles(settlement.SiteTile))
        {
            if (tile == settlement.SiteTile || _fieldOwner[tile] >= 0 || !IsOrchardGround(tile)) continue;
            if (IsOrchardOccupied(tile)) continue;
            WorldEconomyTile input = _world.Tile(tile);
            Fix64 score = input.SoilFertility + input.SpringForage + input.SummerForage + input.AutumnForage;
            if (best < 0 || score > bestScore || score == bestScore && tile < best)
            {
                best = tile;
                bestScore = score;
            }
        }
        if (best < 0) return false;
        settlement.OrchardPlots.Add(new OrchardPlot { Tile = best, AgeSeasons = 0 });
        return true;
    }

    private bool IsOrchardOccupied(int tile)
    {
        foreach (SettlementState settlement in _settlements)
            foreach (OrchardPlot orchard in settlement.OrchardPlots)
                if (orchard.Tile == tile) return true;
        return false;
    }

    private bool IsOrchardGround(int tile)
    {
        Biome biome = (Biome)_world.Tile(tile).BiomeId;
        return biome is not (Biome.Ocean or Biome.Coast or Biome.Ice or Biome.Peak);
    }

    private void PlaceField(SettlementState settlement)
    {
        int best = -1;
        Fix64 bestScore = Fix64.Zero;
        foreach (int tile in RangeTiles(settlement.SiteTile))
        {
            if (_fieldOwner[tile] >= 0 || IsOrchardOccupied(tile) || _world.Tile(tile).FieldYieldPerYear <= Fix64.Zero) continue;
            Fix64 score = _world.Tile(tile).FieldYieldPerYear + _soil[tile];
            if (score > bestScore || score == bestScore && tile < best)
            {
                best = tile;
                bestScore = score;
            }
        }
        if (best < 0) return;
        _fieldOwner[best] = settlement.Id;
        _tendedStandYieldBonus[best] = HasProject(settlement.Civilization, "FOR-06") &&
            _world.Tile(best).WildGrainStand;
        settlement.FieldTiles.Add(best);
    }

    private int FieldLimit(SettlementState settlement)
        // C1 limits fields to one quarter of the full range. Cultivability is checked
        // separately when a field is placed; barren/orchard tiles still count toward the limit.
        => E3FieldLimit(settlement, RangeTiles(settlement.SiteTile).Length / 4);

    private void ConsumeFood(SettlementState settlement, bool winter, int season)
    {
        Fix64 need = FoodNeed(settlement, winter);
        if (settlement.UnderInvestment) need *= Fix64.FromRatio(75, 100);
        Fix64 available = FoodStores(settlement);
        settlement.LastSeasonFoodStoresBeforeConsumption = available;
        Fix64 security = need <= Fix64.Zero ? Fix64.One : Fix64.Min(Fix64.One, available / need);
        ConsumeFoodStocks(settlement, Fix64.Min(available, need));
        settlement.FoodSecurity = security;
        if (winter) settlement.LateWinterFoodShare = security;

        Fix64 unmet = Fix64.One - security;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState figure = _figures[i];
            if (figure.SettlementId != settlement.Id) continue;
            figure.Hunger = Fix64.Clamp(figure.Hunger + unmet * Fix64.Parse("0.45") - security * Fix64.Parse("0.18"),
                Fix64.Zero, Fix64.One);
            figure.Rest = Fix64.Clamp(figure.Rest + Fix64.Parse("0.25") - figure.WorkThisSeason * Fix64.Parse("0.35"),
                Fix64.Zero, Fix64.One);
            figure.Belonging = Fix64.Clamp(figure.Belonging + Fix64.Parse("0.03") - unmet * Fix64.Parse("0.08"),
                Fix64.Zero, Fix64.One);
        }
        _ = season;
    }

    private void ConsumeFoodStocks(SettlementState s, Fix64 amount)
    {
        Good[] order = { Good.Fish, Good.Meat, Good.Plant, Good.Grain, Good.WildGrain, Good.DriedFood, Good.Roots, Good.Nuts };
        for (int i = 0; i < order.Length && amount > Fix64.Zero; i++)
        {
            int good = (int)order[i];
            Fix64 value = FoodValue(order[i], s);
            if (value <= Fix64.Zero) continue;
            Fix64 take = Fix64.Min(amount / value, s.Stores[good]);
            s.Stores[good] -= take;
            amount -= take * value;
        }
        if (amount > Fix64.Zero) ConsumeMetalFood(s, amount);
    }

    private void SpoilFood(SettlementState settlement)
    {
        Fix64 storageFactor = settlement.Structures[(int)Structure.Granary] > 0
            ? Fix64.Parse("0.25")
            : settlement.Structures[(int)Structure.StoragePit] > 0 ? Fix64.Half : Fix64.One;
        Fix64 modifier = Modifier(settlement.Civilization, "spoilage_of_stored_food");
        for (int i = 0; i < FoodGoods.Length; i++)
        {
            Good good = FoodGoods[i];
            Fix64 spoilage = SpoilageFor(good);
            if (good == Good.Plant || good == Good.Fruit || good == Good.Meat || good == Good.Fish || good == Good.Grain || good == Good.DriedFood)
                spoilage *= modifier;
            settlement.Stores[(int)good] *= Fix64.One - Fix64.Clamp(spoilage * storageFactor, Fix64.Zero, Fix64.One);
        }
        SpoilMetalFood(settlement, storageFactor);
        Fix64 capacity = Fix64.FromInt(12 + 15 * settlement.Structures[(int)Structure.StoragePit] +
                                     80 * settlement.Structures[(int)Structure.Granary]);
        TrimFoodToCapacity(settlement, capacity);
    }

    private static readonly Good[] FoodGoods =
    {
        Good.Plant, Good.Fruit, Good.Meat, Good.Roots, Good.Nuts, Good.WildGrain, Good.Grain, Good.Fish, Good.DriedFood
    };

    private Fix64 SpoilageFor(Good good)
    {
        string id = GoodId(good);
        for (int i = 0; i < _content.Goods.Length; i++)
            if (_content.Goods[i].Id == id) return _content.Goods[i].Spoilage;
        return good switch
        {
            Good.Meat => Fix64.Parse("0.6"), Good.Plant => Fix64.Parse("0.4"), Good.Roots => Fix64.Parse("0.15"),
            Good.Nuts => Fix64.Parse("0.05"), Good.WildGrain => Fix64.Parse("0.08"), Good.Grain => Fix64.Parse("0.05"),
            Good.Fish => Fix64.Parse("0.7"), Good.DriedFood => Fix64.Parse("0.1"), Good.Fruit => Fix64.Parse("0.4"), _ => Fix64.Zero,
        };
    }

    private void TrimFoodToCapacity(SettlementState settlement, Fix64 capacity)
    {
        Fix64 excess = FoodStores(settlement) - capacity;
        Good[] order = { Good.Plant, Good.Fruit, Good.Meat, Good.Fish, Good.WildGrain, Good.Grain, Good.Roots, Good.Nuts, Good.DriedFood };
        for (int i = 0; i < order.Length && excess > Fix64.Zero; i++)
        {
            int good = (int)order[i];
            Fix64 value = FoodValue(order[i], settlement);
            if (value <= Fix64.Zero) continue;
            Fix64 discard = Fix64.Min(excess / value, settlement.Stores[good]);
            settlement.Stores[good] -= discard;
            excess -= discard * value;
        }
    }

    private void UpdateNeedsAndPopulation(SettlementState settlement, int season, bool winter,
        Fix64 priorSeasonFoodSecurity)
    {
        int era = CurrentEra(settlement.Civilization);
        Fix64 winterMultiplier = winter ? WinterMultiplier(era) : Fix64.One;
        ClimateBand climate = (ClimateBand)_world.Tile(settlement.SiteTile).ClimateId;
        int populationAtStart = Population(settlement.Id);
        int requiredWarmth = WarmthRequirement(climate, winter);
        if (winter && _lastWinterWasHarsh && requiredWarmth > 0) requiredWarmth++;
        Fix64 fertilityHealth = FertilityHealthFactor(settlement);
        var deadIds = new List<long>();
        var birthMothers = new List<FigureState>();
        // The food and housing caps do not change during the pass over the settlement's figures, so work them out once,
        // when the first fertile woman needs them.
        Fix64 conceptionCap = Fix64.Zero;
        bool conceptionCapKnown = false;

        int warmthOrdinal = 0;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState figure = _figures[i];
            if (figure.SettlementId != settlement.Id) continue;
            if (figure.HuntingInjuryHazard > Fix64.Zero && figure.WoundSeasons == 0 &&
                Pcg32.ForStream(_seed, HuntingWoundStream, (ulong)figure.Id, season).NextFix01() < figure.HuntingInjuryHazard)
                figure.WoundSeasons = 2;
            int warmth = FigureWarmthTier(settlement, figure, populationAtStart, warmthOrdinal++);
            int warmthDeficit = Math.Max(0, requiredWarmth - warmth);
            Fix64 exposure = Fix64.Zero;
            if (winter && warmthDeficit > 0 && requiredWarmth > 0)
            {
                // Exposure is a cause in the Mort* annual hazard budget (§09 §4.1–4.3),
                // not an extra per-winter rate on top of that budget. Allocate its effective
                // annual share to the winter in proportion to the figure's warmth deficit.
                // Cold climates double the exposure load, as specified in §4.3. Fire, shelter,
                // and clothing reduce the active fraction by raising the figure's warmth tier.
                Fix64 exposureShare = CauseShare(IsChild(figure), "exposure", era);
                Fix64 effectiveAnnualMultiplier = (Fix64.FromInt(3) + WinterMultiplier(era)) / Fix64.FromInt(4);
                Fix64 deficitShare = Fix64.FromInt(warmthDeficit) / Fix64.FromInt(requiredWarmth);
                if (climate == ClimateBand.Cold) deficitShare = Fix64.Min(Fix64.FromInt(2), deficitShare * Fix64.FromInt(2));
                exposure = BaseHazard(figure, era) * exposureShare * CauseModifier(settlement.Civilization, "exposure") *
                    effectiveAnnualMultiplier * deficitShare;
            }
            figure.Warmth = requiredWarmth == 0 ? Fix64.One : Fix64.Min(Fix64.One,
                Fix64.FromRatio(Math.Max(0, warmth), requiredWarmth));
            // The extra shortfall surcharge applies when Winter ends underfed;
            // baseline starvation mortality remains in the seasonal hazard budget.
            Fix64 starvation = winter
                ? Fix64.Max(Fix64.Zero, Fix64.One - settlement.FoodSecurity)
                : Fix64.Zero;
            UpdateFigureHealth(figure);
            Fix64 starvationHazard = starvation * (IsChild(figure) || figure.Age >= 45 ? Fix64.Parse("0.25") : Fix64.Parse("0.1"));
            HazardBreakdown hazards = SeasonalHazards(settlement.Civilization, figure, era, winter,
                starvationHazard, exposure);
            RecordExpectedHazards(figure, hazards);

            Pcg32 mortality = Pcg32.ForStream(_seed, MortalityStream, (ulong)figure.Id, season);
            if (mortality.NextFix01() < Fix64.Clamp(hazards.Total, Fix64.Zero, Fix64.One))
            {
                RecordDeathCause(figure, hazards, season);
                deadIds.Add(figure.Id);
                settlement.Cohesion = Fix64.Max(Fix64.Parse("0.6"), settlement.Cohesion - Fix64.Parse("0.002"));
                continue;
            }

            if (figure.PregnancySeasons > 0)
            {
                figure.PregnancySeasons--;
                if (figure.PregnancySeasons == 0) birthMothers.Add(figure);
            }
            else if (figure.Female && figure.Age is >= 14 and < 45)
            {
                Fix64 targetFertility = Fix64.Parse("0.13") * FertilityMultiplier(settlement.Civilization);
                Fix64 conceptionChance = Fix64.One / (Fix64.One / targetFertility - Fix64.FromInt(3));
                if (!conceptionCapKnown)
                {
                    conceptionCap = Fix64.Min(FoodCap(settlement), HousingCap(settlement));
                    conceptionCapKnown = true;
                }
                Fix64 cap = conceptionCap;
                Fix64 density = cap <= Fix64.Zero ? Fix64.One : Fix64.Min(Fix64.One, Fix64.FromInt(populationAtStart) / cap);
                Fix64 crowding = Fix64.One;
                for (int power = 0; power < 6; power++) crowding *= density;
                conceptionChance *= priorSeasonFoodSecurity * fertilityHealth * (Fix64.One - crowding);
                Pcg32 birth = Pcg32.ForStream(_seed, BirthStream, (ulong)figure.Id, season);
                if (birth.NextFix01() < Fix64.Clamp(conceptionChance, Fix64.Zero, Fix64.One)) figure.PregnancySeasons = 3;
            }
            // A treated two-season illness recovers one season sooner; the bonus is
            // consumed by this episode, rather than permanently changing the figure.
            if (figure.IllnessSeasons > 0)
                figure.IllnessSeasons = (byte)Math.Max(0, figure.IllnessSeasons - (figure.DiseaseTreated ? 2 : 1));
            if (figure.WoundSeasons > 0) figure.WoundSeasons--;
            UpdateFigureHealth(figure);
        }

        int deathsHere = deadIds.Count;
        int read = 0, write = 0;
        for (; read < _figures.Count; read++)
        {
            if (deadIds.Count > 0 && deadIds[0] == _figures[read].Id)
            {
                deadIds.RemoveAt(0);
                continue;
            }
            _figures[write++] = _figures[read];
        }
        if (write < _figures.Count) _figures.RemoveRange(write, _figures.Count - write);
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState current = _settlements[i];
            if (current.FireKeeperId < 0) continue;
            bool keeperRemains = false;
            for (int j = 0; j < _figures.Count; j++)
                if (_figures[j].Id == current.FireKeeperId && _figures[j].SettlementId == current.Id)
                { keeperRemains = true; break; }
            if (!keeperRemains) current.FireKeeperId = -1;
        }
        foreach (FigureState mother in birthMothers) AddBirth(settlement, mother);
        if (settlement.Civic is { } civicDeaths) civicDeaths.DeathsThisYear += deathsHere;

        if (season % 4 == 3) RebuildCohortAgeState(settlement);
    }

    private Fix64 FertilityHealthFactor(SettlementState settlement)
    {
        int fertileWomen = 0;
        int sickFertileWomen = 0;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState figure = _figures[i];
            if (figure.SettlementId != settlement.Id || !figure.Female || figure.Age is < 14 or >= 45) continue;
            fertileWomen++;
            if (figure.IllnessSeasons > 0) sickFertileWomen++;
        }

        if (fertileWomen == 0) return Fix64.One;
        Fix64 sickShare = Fix64.FromInt(sickFertileWomen) / Fix64.FromInt(fertileWomen);
        return Fix64.One - Fix64.Half * sickShare;
    }

    private void AddBirth(SettlementState settlement, FigureState mother)
    {
        bool female = Pcg32.ForStream(_seed, BirthStream ^ 0x71UL, (ulong)mother.Id, _seasonsCompleted).NextBelow(2) == 0;
        AddFigure(settlement.Civilization, settlement.Id, 0, female);
    }

    private void AgePopulationByOneYear(long firstFigureIdBornThisSeason)
    {
        for (int i = 0; i < _figures.Count; i++)
            if (_figures[i].Id < firstFigureIdBornThisSeason && _figures[i].Age < 99)
                _figures[i].Age++;
    }

    private void RebuildCohortAgeState(SettlementState settlement)
    {
        // Figures are the exact 1:1 E0–E2 cohort representation; aging above advances every cohort member.
        _ = settlement;
    }

    private Fix64 BaseHazard(FigureState figure, int era)
    {
        Fix64 total;
        if (figure.Age >= 45)
        {
            // h45 by era (docs/MVP/09 §4.1): 0.023 E0, 0.021 E1–E2, 0.018 E3 (e45 17).
            Fix64 baseElder = era switch { 0 => Fix64.Parse("0.023"), 1 => Fix64.Parse("0.021"), 2 => Fix64.Parse("0.021"), _ => Fix64.Parse("0.018") };
            for (int age = 45; age < figure.Age; age++) baseElder *= Fix64.Parse("1.08872");
            total = Fix64.Min(Fix64.FromInt(4), baseElder);
        }
        else if (figure.Age == 0)
        {
            // Child and adult E0 hazards are the live baseline. E1/E2 mortality targets
            // are produced by completed-project mechanisms below; switching this base to
            // the target Mort* value as well would count the era shift twice.
            total = Fix64.Parse("0.036") * Fix64.Parse("2.333333333");
        }
        else if (figure.Age < 5)
        {
            total = Fix64.Parse("0.036") * Fix64.Parse("1.166666667");
        }
        else if (IsChild(figure))
        {
            total = Fix64.Parse("0.036") * Fix64.Parse("0.777777778");
        }
        else
        {
            total = Fix64.Parse("0.012");
        }
        return total;
    }

    private HazardBreakdown SeasonalHazards(int civ, FigureState figure, int era, bool winter,
        Fix64 starvationHazard, Fix64 exposureHazard)
    {
        Fix64 annual = BaseHazard(figure, era);
        Fix64 winterMultiplier = WinterMultiplier(era);
        // BaseHazard is the annual hazard outside winter. The population-health spec applies h/4
        // in each non-winter season and h*WinterMultiplier/4 in winter, so the effective annual
        // hazard is h*(3 + WinterMultiplier)/4.
        Fix64 seasonalScale = (winter ? winterMultiplier : Fix64.One) / Fix64.FromInt(4);
        bool child = IsChild(figure);
        Fix64 injuryShare = CauseShare(child, "injury", era);
        bool adult = figure.Age is >= 14 and < 45;
        // Childbirth is part of injury in 09 §4.2. Reserve the existing 6 percentage
        // points of adult mortality for actual deliveries, not every adult each season.
        if (adult) injuryShare -= Fix64.Parse("0.06");
        Fix64 injury = annual * injuryShare * CauseModifier(civ, "injury", adultExposure: !child) * seasonalScale;
        if (figure.WoundTreated) injury *= Fix64.Parse("0.7");
        // MET-04's arsenic (docs/MVP/23 §15): a per-figure risk for those who co-smelted arsenical bronze this season.
        if (figure.Task == MetalTask.SmeltArsenic) injury *= ArsenicInjury;
        if (adult && figure.Female && figure.PregnancySeasons == 1)
        {
            // Normalize to a 1:1 sex ratio and the uncrowded seasonal delivery rate.
            Fix64 deliveriesPerAdult = Fix64.Half * Fix64.Parse("0.13") * FertilityMultiplier(civ);
            Fix64 maternal = annual * Fix64.Parse("0.06") * seasonalScale / deliveriesPerAdult;
            maternal *= CauseModifier(civ, "maternal");
            if (figure.MidwifeAttended) maternal *= Fix64.Parse("0.6");
            injury += maternal;
        }
        Fix64 diseaseCare = figure.DiseaseTreated ? Fix64.Parse("0.75") : Fix64.One;
        // E3: epidemics come as discrete outbreaks (docs/MVP/09 §10); the continuous budget is then zero, so they do not count twice.
        bool discrete = DiscreteEpidemics(figure);
        // GOV-04: local famine deaths −50% (docs/MVP/23 §16), half of starvation being local famine (check_e3.py).
        Fix64 famine = discrete && HasProject(civ, "GOV-04") ? Fix64.FromRatio(3, 4) : Fix64.One;
        HazardBreakdown hazards = new(
            annual * CauseShare(child, "endemic", era) * CauseModifier(civ, "endemic") * seasonalScale * diseaseCare,
            discrete ? Fix64.Zero : annual * CauseShare(child, "epidemic", era) * CauseModifier(civ, "epidemic") * seasonalScale * diseaseCare,
            annual * CauseShare(child, "starvation", era) * CauseModifier(civ, "starvation") * seasonalScale * famine,
            Fix64.Zero,
            injury,
            annual * CauseShare(child, "predators", era) * CauseModifier(civ, "predators") * seasonalScale,
            annual * CauseShare(child, "violence", era) * CauseModifier(civ, "violence") * seasonalScale,
            Fix64.Zero,
            Fix64.Zero);

        if (figure.Age >= 45)
            hazards = Scale(hazards, Fix64.Parse("0.6")) with { Senescence = annual * Fix64.Parse("0.4") * seasonalScale };
        if (child)
        {
            hazards = Scale(hazards, CauseModifier(civ, "child"));
            if (figure.Age == 0) hazards = Scale(hazards, CauseModifier(civ, "age0"));
        }
        else if (adult) hazards = Scale(hazards, Profile(civ).AdultMortality);

        hazards = hazards with
        {
            Exposure = exposureHazard,
            WinterShortfall = famine == Fix64.One ? starvationHazard : starvationHazard * Fix64.Half,
        };
        if (discrete && figure.Infected) hazards = hazards with { Epidemic = hazards.Epidemic + InfectedHazard(figure) };
        return figure.Age == 0 && figure.MidwifeAttended ? Scale(hazards, Fix64.Parse("0.75")) : hazards;
    }

    private static HazardBreakdown Scale(HazardBreakdown hazards, Fix64 multiplier) => new(
        hazards.Endemic * multiplier, hazards.Epidemic * multiplier, hazards.Starvation * multiplier,
        hazards.Exposure * multiplier, hazards.Injury * multiplier, hazards.Predators * multiplier,
        hazards.Violence * multiplier, hazards.Senescence * multiplier, hazards.WinterShortfall * multiplier);

    private Fix64 CauseModifier(int civ, string cause, bool adultExposure = false)
    {
        int slot = CauseModifierSlot(cause, adultExposure);
        if (slot < 0) return ComputeCauseModifier(civ, cause, adultExposure);
        _causeModifierValues ??= new Fix64[_completedProjects.Length][];
        _causeModifierValid ??= new bool[_completedProjects.Length][];
        Fix64[] values = _causeModifierValues[civ] ??= new Fix64[20];
        bool[] valid = _causeModifierValid[civ] ??= new bool[20];
        if (valid[slot]) return values[slot];
        values[slot] = ComputeCauseModifier(civ, cause, adultExposure);
        valid[slot] = true;
        return values[slot];
    }

    private Fix64 ComputeCauseModifier(int civ, string cause, bool adultExposure)
    {
        Fix64 modifier = Fix64.One;
        for (int i = 0; i < _content.Effects.Length; i++)
        {
            EconomyEffect effect = _content.Effects[i];
            if (effect.Kind != "hazard" || effect.Cause != cause || effect.Mult is not { } mult ||
                !_completedProjects[civ][effect.ProjectIndex]) continue;
            // E0–E2 exposure Hazard values are population-average equivalents of warmth
            // mechanisms. The live exposure budget is allocated from each figure's warmth
            // deficit above, so multiplying it here would apply the same project twice.
            if (cause == "exposure") continue;
            // MED catalog hazards are population-average checks. Actual reductions require
            // an assigned provider and paid care for this patient in SeasonalHazards.
            string projectId = _content.Projects[effect.ProjectIndex].Id;
            if (projectId is "MED-01" or "MED-02" or "MED-03") continue;
            if (effect.Scope == "hunters")
            {
                // Catalog values are population averages. HNT injury effects cover the documented 60%
                // hunting share of adult injury; they do not affect child injury or other injury causes.
                if (cause != "injury" || !adultExposure) continue;
                mult = Fix64.One + Fix64.Parse("0.6") * (mult - Fix64.One);
            }
            modifier *= mult;
        }
        return modifier;
    }

    internal static Fix64 CauseShare(bool child, string cause, int era)
    {
        if (child)
        {
            if (era == 3)
            {
                return cause switch
                {
                    "endemic" => Fix64.Parse("0.50"), "epidemic" => Fix64.Parse("0.10"),
                    "starvation" => Fix64.Parse("0.15"), "exposure" => Fix64.Parse("0.10"),
                    "injury" => Fix64.Parse("0.10"), "predators" => Fix64.Zero,
                    "violence" => Fix64.Parse("0.05"), _ => Fix64.Zero,
                };
            }
            return cause switch
            {
                "endemic" => era >= 2 ? Fix64.Parse("0.42") : Fix64.Parse("0.45"),
                "epidemic" => era >= 2 ? Fix64.Parse("0.03") : Fix64.Zero,
                "starvation" => Fix64.Parse("0.20"), "exposure" => Fix64.Parse("0.20"),
                "injury" => Fix64.Parse("0.10"), "predators" => Fix64.Parse("0.05"), _ => Fix64.Zero,
            };
        }
        if (era == 3)
        {
            return cause switch
            {
                "endemic" => Fix64.Parse("0.35"), "epidemic" => Fix64.Parse("0.10"),
                "starvation" => Fix64.Parse("0.10"), "exposure" => Fix64.Parse("0.10"),
                "injury" => Fix64.Parse("0.20"), "predators" => Fix64.Zero,
                "violence" => Fix64.Parse("0.15"), _ => Fix64.Zero,
            };
        }
        return cause switch
        {
            "endemic" => era >= 2 ? Fix64.Parse("0.27") : Fix64.Parse("0.30"),
            "epidemic" => era >= 2 ? Fix64.Parse("0.03") : Fix64.Zero,
            "starvation" => Fix64.Parse("0.15"), "exposure" => Fix64.Parse("0.15"),
            "injury" => Fix64.Parse("0.25"), "predators" => Fix64.Parse("0.05"),
            "violence" => Fix64.Parse("0.10"), _ => Fix64.Zero,
        };
    }

    private void RecordDeathCause(FigureState figure, HazardBreakdown hazards, int season)
    {
        Fix64 total = hazards.Total;
        if (total <= Fix64.Zero)
        {
            RecordDeath(figure, EconomyDeathCause.Endemic, hazards);
            return;
        }
        Pcg32 selection = Pcg32.ForStream(_seed, DeathCauseStream, (ulong)figure.Id, season);
        Fix64 target = selection.NextFix01() * total;
        if ((target -= hazards.Endemic) < Fix64.Zero) { RecordDeath(figure, EconomyDeathCause.Endemic, hazards); return; }
        if ((target -= hazards.Epidemic) < Fix64.Zero) { RecordDeath(figure, EconomyDeathCause.Epidemic, hazards); return; }
        if ((target -= hazards.Starvation + hazards.WinterShortfall) < Fix64.Zero) { RecordDeath(figure, EconomyDeathCause.Starvation, hazards); return; }
        if ((target -= hazards.Exposure) < Fix64.Zero) { RecordDeath(figure, EconomyDeathCause.Exposure, hazards); return; }
        if ((target -= hazards.Injury) < Fix64.Zero) { RecordDeath(figure, EconomyDeathCause.Injury, hazards); return; }
        if ((target -= hazards.Predators) < Fix64.Zero) { RecordDeath(figure, EconomyDeathCause.Predators, hazards); return; }
        if ((target -= hazards.Violence) < Fix64.Zero) { RecordDeath(figure, EconomyDeathCause.Violence, hazards); return; }
        RecordDeath(figure, EconomyDeathCause.Senescence, hazards);
    }

    private void RecordExpectedHazards(FigureState figure, HazardBreakdown hazards)
    {
        int ageBand = figure.Age < 14 ? 0 : figure.Age < 45 ? 1 : 2;
        int offset = ageBand * DeathCauseCount;
        int exactAgeOffset = figure.Age * DeathCauseCount;
        _lastExpectedHazardsByCauseAndAgeBand[offset + (int)EconomyDeathCause.Endemic] += hazards.Endemic;
        _lastExpectedHazardsByCauseAndAgeBand[offset + (int)EconomyDeathCause.Epidemic] += hazards.Epidemic;
        _lastExpectedHazardsByCauseAndAgeBand[offset + (int)EconomyDeathCause.Starvation] += hazards.Starvation + hazards.WinterShortfall;
        _lastExpectedHazardsByCauseAndAgeBand[offset + (int)EconomyDeathCause.Exposure] += hazards.Exposure;
        _lastExpectedHazardsByCauseAndAgeBand[offset + (int)EconomyDeathCause.Injury] += hazards.Injury;
        _lastExpectedHazardsByCauseAndAgeBand[offset + (int)EconomyDeathCause.Predators] += hazards.Predators;
        _lastExpectedHazardsByCauseAndAgeBand[offset + (int)EconomyDeathCause.Violence] += hazards.Violence;
        _lastExpectedHazardsByCauseAndAgeBand[offset + (int)EconomyDeathCause.Senescence] += hazards.Senescence;
        _lastExpectedHazardsByExactAgeAndCause[exactAgeOffset + (int)EconomyDeathCause.Endemic] += hazards.Endemic;
        _lastExpectedHazardsByExactAgeAndCause[exactAgeOffset + (int)EconomyDeathCause.Epidemic] += hazards.Epidemic;
        _lastExpectedHazardsByExactAgeAndCause[exactAgeOffset + (int)EconomyDeathCause.Starvation] += hazards.Starvation + hazards.WinterShortfall;
        _lastExpectedWinterShortfallByExactAge[figure.Age] += hazards.WinterShortfall;
        _lastExpectedHazardsByExactAgeAndCause[exactAgeOffset + (int)EconomyDeathCause.Exposure] += hazards.Exposure;
        _lastExpectedHazardsByExactAgeAndCause[exactAgeOffset + (int)EconomyDeathCause.Injury] += hazards.Injury;
        _lastExpectedHazardsByExactAgeAndCause[exactAgeOffset + (int)EconomyDeathCause.Predators] += hazards.Predators;
        _lastExpectedHazardsByExactAgeAndCause[exactAgeOffset + (int)EconomyDeathCause.Violence] += hazards.Violence;
        _lastExpectedHazardsByExactAgeAndCause[exactAgeOffset + (int)EconomyDeathCause.Senescence] += hazards.Senescence;
    }

    private void RecordDeath(FigureState figure, EconomyDeathCause cause, HazardBreakdown hazards)
    {
        int band = figure.Age < 14 ? 0 : figure.Age < 45 ? 1 : 2;
        _lastDeathsByCause[(int)cause]++;
        _lastDeathsByCauseAndAgeBand[band * DeathCauseCount + (int)cause]++;
        _lastDeathEvents.Add(new EconomyDeathEvent(
            figure.Id, figure.Civilization, figure.SettlementId, figure.Age, figure.Female,
            figure.PregnancySeasons, cause, hazards.Total, hazards.Endemic, hazards.Epidemic,
            hazards.Starvation + hazards.WinterShortfall, hazards.Exposure, hazards.Injury, hazards.Predators,
            hazards.Violence, hazards.Senescence, hazards.WinterShortfall));
    }

    private Fix64 FertilityMultiplier(int civ)
    {
        int era = CurrentEra(civ);
        Fix64 eraMultiplier = era switch { 0 => Fix64.One, 1 => Fix64.Parse("1.15"), _ => Fix64.Parse("1.3") };
        return eraMultiplier * Profile(civ).Fertility;
    }

    // The Warlord's adult mortality is ×1.0, not 07's ×1.3: the balance model had no war and stood war deaths in with
    // mortality; since M5 raids and assaults kill fighters in the simulation itself (docs/MVP/14 §17).
    private ArchetypeProfile Profile(int civ) => _archetypes[civ] switch
    {
        EconomyArchetype.Grower => new ArchetypeProfile(Fix64.Parse("1.3"), Fix64.Parse("1.11"), Fix64.One, Fix64.Zero),
        EconomyArchetype.Builder => new ArchetypeProfile(Fix64.Parse("1.10"), Fix64.Parse("1.07"), Fix64.One, Fix64.Parse("-0.02")),
        EconomyArchetype.Wanderer => new ArchetypeProfile(Fix64.One, Fix64.Parse("0.95"), Fix64.Parse("1.05"), Fix64.Zero),
        EconomyArchetype.Warlord => new ArchetypeProfile(Fix64.One, Fix64.One, Fix64.One, Fix64.Zero),
        EconomyArchetype.Sage => new ArchetypeProfile(Fix64.Parse("0.95"), Fix64.Parse("0.95"), Fix64.Parse("0.95"), Fix64.Zero),
        _ => new ArchetypeProfile(Fix64.One, Fix64.One, Fix64.One, Fix64.Zero),
    };

    private static bool IsChild(FigureState figure) => figure.Age < 14;

    private int CurrentEra(int civ)
    {
        int era = 0;
        for (int i = 0; i < _content.Projects.Length; i++)
        {
            EconomyProject project = _content.Projects[i];
            if (_completedProjects[civ][project.Index] && project.EnterEra > era)
                era = project.EnterEra;
        }
        return Math.Min(era, MaxEra);
    }

    private static Fix64 WinterMultiplier(int era)
        => WinterMultipliers[Math.Clamp(era, 0, WinterMultipliers.Length - 1)];

    private void TryFission(SettlementState parent, int season)
    {
        if (_holdSettlements) return;
        if (ShouldPreserveCityTown(parent)) { parent.FissionPending = false; return; }
        if (parent.FissionPending || season - parent.LastFissionSeason < 4) return;
        // Free Folk (refugee camps, seceded towns, Tin Folk) grow to their cap but do not bud: a daughter would join the
        // civilization whose id the camp still carries (docs/MVP/22 §6.4).
        if (IsFreeFolk(parent)) return;
        int population = Population(parent.Id);
        Fix64 foodCap = FoodCap(parent);
        // A Market town is food-bound: it buds at 85% of min(FoodCap, GroupCap), not of its housing (07d R2).
        Fix64 effectiveCap = IsMarketTown(parent) ? Fix64.Min(foodCap, GroupCap(parent))
            : Fix64.Min(foodCap, Fix64.Min(HousingCap(parent), GroupCap(parent)));
        // Below C 35 volunteers leave sooner: the trigger drops to 75% (docs/MVP/22 §8.3).
        Fix64 trigger = CohesionLive(parent) && CohesionPoints(parent.Cohesion) < _civic.FissionBelow ? _civic.FissionTrigger : Fix64.Parse("0.85");
        bool capPressure = Fix64.FromInt(population) >= effectiveCap * trigger * FissionThresholdScale(parent.Civilization);
        // Live fission follows the settlement's current world-derived FoodCap. Lean-season
        // project effects set the share of that capacity available in winter, then actual
        // winter security scales the threshold as described in §09 §7.2.
        Fix64 leanFoodCapacity = foodCap * LeanFoodShare(parent.Civilization);
        bool lateWinterPressure = season % 4 == 3 &&
            Fix64.FromInt(population) >= leanFoodCapacity * Fix64.Clamp(parent.LateWinterFoodShare, Fix64.Zero, Fix64.One);
        if ((!capPressure && !lateWinterPressure) ||
            Fix64.FromInt(population) * DaughterShare < MinimumDaughterPopulation) return;
        parent.FissionPending = true;
    }

    /// <summary>Keep a viable Temple-bound Town intact until it can become a City (07d F4); hunger and low Cohesion still split it.</summary>
    private bool ShouldPreserveCityTown(SettlementState settlement)
    {
        if (settlement.Civic is not { } civic || civic.Tier != (byte)SettlementTier.Town ||
            civic.BuildKind != CivicBuild.Temple && civic.Temples == 0 && !ShouldPrepareCityTemple(settlement, civic)) return false;
        Fix64 cityPopulation = Fix64.FromInt(_civic.CityMinPop);
        return FoodCap(settlement) >= cityPopulation && HousingCap(settlement) >= cityPopulation &&
            settlement.FoodSecurity >= Fix64.Parse("0.8") &&
            CohesionLive(settlement) && CohesionPoints(settlement.Cohesion) >= _civic.FissionBelow;
    }

    /// <summary>
    /// Keep a Strategist's early frontier Found from splitting a leading City-growth settlement before it reaches Town.
    /// The rank and capacity checks are own-state only; they cover a healthy Market-bound village such as a future City site.
    /// </summary>
    internal bool ShouldProtectStrategistFoundDonor(int settlementIndex, Fix64 foundShare)
    {
        if (unchecked((uint)settlementIndex) >= unchecked((uint)_settlements.Count)) return false;
        SettlementState settlement = _settlements[settlementIndex];
        int population = Population(settlement.Id);
        int daughterPopulation = (Fix64.FromInt(population) * foundShare).RoundToInt();
        return ShouldProtectFoundingCityProspect(
            cityTrack: IsCityTrack(settlement),
            capacity: CityProspectCapacity(settlement),
            minimumCapacity: Fix64.FromRatio(_civic.CityMinPop, 2),
            healthy: settlement.FoodSecurity >= Fix64.FromRatio(8, 10) && CohesionLive(settlement) &&
                CohesionPoints(settlement.Cohesion) >= _civic.FissionBelow,
            capacityRank: CityProspectRank(settlement),
            remainingPopulation: population - daughterPopulation,
            cityMinPop: _civic.CityMinPop);
    }

    internal static bool ShouldProtectFoundingCityProspect(bool cityTrack, Fix64 capacity, Fix64 minimumCapacity,
        bool healthy, int capacityRank, int remainingPopulation, int cityMinPop)
        => cityTrack && capacity >= minimumCapacity && healthy && capacityRank < 3 && remainingPopulation < cityMinPop;

    private int CityProspectRank(SettlementState settlement)
    {
        Fix64 capacity = CityProspectCapacity(settlement);
        Fix64 minimumCapacity = Fix64.FromRatio(_civic.CityMinPop, 2);
        int higherRanked = 0;
        for (int i = 0; i < _settlements.Count; i++)
        {
            if (i == settlement.Id) continue;
            SettlementState other = _settlements[i];
            if (other.Civilization != settlement.Civilization || !IsCityTrack(other) ||
                other.FoodSecurity < Fix64.FromRatio(8, 10) || !CohesionLive(other) ||
                CohesionPoints(other.Cohesion) < _civic.FissionBelow || CityProspectCapacity(other) < minimumCapacity) continue;
            Fix64 otherCapacity = CityProspectCapacity(other);
            if (otherCapacity > capacity || otherCapacity == capacity && other.Id < settlement.Id) higherRanked++;
        }
        return higherRanked;
    }

    private bool IsCityTrack(SettlementState settlement)
    {
        if (settlement.Civic is not { } civic || Population(settlement.Id) <= 0) return false;
        return civic.Tier >= (byte)SettlementTier.Town || civic.Markets > 0 ||
            civic.BuildKind is CivicBuild.Market or CivicBuild.Temple || civic.Temples > 0;
    }

    private Fix64 CityProspectCapacity(SettlementState settlement)
        => Fix64.Min(FoodCap(settlement), HousingCap(settlement));

    private Fix64 LeanFoodCapacity(SettlementState settlement)
    {
        return FoodCap(settlement) * LeanFoodShare(settlement.Civilization);
    }

    private Fix64 LeanFoodShare(int civ)
    {
        Fix64 leanShare = Fix64.Parse("0.77") + Profile(civ).LeanShareAdjustment;
        foreach (EconomyCapCalibration effect in EconomyBalanceCalibration.E0ToE2FoodAndLeanCaps)
        {
            if (!HasProject(civ, effect.ProjectId)) continue;
            leanShare *= effect.LeanMultiplier;
        }
        return Fix64.Min(Fix64.One, leanShare);
    }

    private void ResolvePendingFissions(int season)
    {
        for (int s = 0; s < _settlements.Count; s++)
        {
            SettlementState parent = _settlements[s];
            if (!parent.FissionPending) continue;
            if (ShouldPreserveCityTown(parent)) { parent.FissionPending = false; continue; }
            if (season - parent.LastFissionSeason < 4) { parent.FissionPending = false; continue; }
            int population = Population(parent.Id);
            if (Fix64.FromInt(population) * DaughterShare < MinimumDaughterPopulation)
            { parent.FissionPending = false; continue; }
            int daughterCount = (Fix64.FromInt(population) * DaughterShare).RoundToInt();
            if (daughterCount >= population) daughterCount = population - 1;
            if (daughterCount < 6) { parent.FissionPending = false; continue; }
            int site = ChooseFissionSite(parent);
            if (site < 0) continue;
            if (SiteAdvisor is not null && SiteAdvisor.VoyageLost(parent.Civilization, parent.SiteTile, site, season)) { parent.FissionPending = false; continue; }
            if (SplitSettlement(parent, site, daughterCount, season, FissionStream) is null) parent.FissionPending = false;
        }
    }

    /// <summary>
    /// Splits <paramref name="daughterCount"/> people off <paramref name="parent"/> into a new settlement on <paramref name="site"/>
    /// (docs/MVP/09 §7.1): a cross-section drawn on <paramref name="stream"/> keyed by the parent and season, with a woman of
    /// childbearing age and an elder or skilled adult, 40% of the stores and herds. Records the parent's fission and returns the
    /// daughter, or null (nothing changed) when the cohort lacks the two representatives or six people.
    /// </summary>
    private SettlementState? SplitSettlement(SettlementState parent, int site, int daughterCount, int season, ulong stream)
    {
        var candidates = new List<int>();
        for (int i = 0; i < _figures.Count; i++)
            if (_figures[i].SettlementId == parent.Id) candidates.Add(i);
        candidates.Sort((left, right) => _figures[left].Id.CompareTo(_figures[right].Id));

        // A daughter is a 40% cross-section of its parent, subject to the two minimum
        // representatives in 09 §7.1. Rank the whole cohort with a fission-local stream,
        // then take an exact-size sample. This keeps any shortfall from Bernoulli
        // volunteers from biasing the remaining slots toward older figure IDs.
        Pcg32 fissionSample = Pcg32.ForStream(_seed, stream, (ulong)(uint)parent.Id, season);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = (int)fissionSample.NextBelow((uint)(i + 1));
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int womanIndex = -1;
        int elderOrSkilledAdultIndex = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            int candidateIndex = candidates[i];
            FigureState figure = _figures[candidateIndex];
            if (figure.Female && figure.Age is >= 14 and < 45)
            {
                womanIndex = candidateIndex;
                break;
            }
        }
        for (int i = 0; i < candidates.Count; i++)
        {
            int candidateIndex = candidates[i];
            FigureState figure = _figures[candidateIndex];
            if (candidateIndex != womanIndex &&
                (figure.Age >= 45 || figure.Age is >= 14 and < 45 && IsSkilled(figure)))
            {
                elderOrSkilledAdultIndex = candidateIndex;
                break;
            }
        }
        if (womanIndex < 0 || elderOrSkilledAdultIndex < 0) return null;

        bool[] selected = new bool[_figures.Count];
        selected[womanIndex] = true;
        selected[elderOrSkilledAdultIndex] = true;
        int moved = 2;
        for (int i = 0; i < candidates.Count && moved < daughterCount; i++)
            if (!selected[candidates[i]]) { selected[candidates[i]] = true; moved++; }
        if (moved < 6) return null;

        SettlementState daughter = NewSettlement(parent.Civilization, site);
        _settlements.Add(daughter);
        for (int i = 0; i < _figures.Count; i++)
            if (selected[i]) { _figures[i].SettlementId = daughter.Id; _figures[i].Camp = -1; _figures[i].Role = 0; }
        SplitMetal(parent, daughter);
        for (int good = 0; good < parent.Stores.Length; good++)
        {
            Good item = (Good)good;
            Fix64 quantity = parent.Stores[good];
            Fix64 transfer;
            if (ToolDurability(parent, item) > Fix64.Zero)
            {
                int wholeUnits = quantity.FloorToInt();
                // Durable tools move as whole units. A fractional recipe batch stays with
                // the parent until it can form a usable tool; each band keeps a whole unit.
                int transferableUnits = wholeUnits < 2 ? 0 : Math.Min(wholeUnits - 1, (wholeUnits * 2 + 2) / 5);
                transfer = Fix64.FromInt(transferableUnits);
            }
            else transfer = quantity * Fix64.FromRatio(2, 5);

            if (transfer <= Fix64.Zero) continue;
            parent.Stores[good] -= transfer;
            daughter.Stores[good] += transfer;
            Fix64 durability = ToolDurability(parent, item);
            Fix64 retainedCapacity = (quantity - transfer) * durability;
            Fix64 toolTransfer = Fix64.Clamp(parent.ToolRemainingWork[good] - retainedCapacity,
                Fix64.Zero, transfer * durability);
            parent.ToolRemainingWork[good] -= toolTransfer;
            daughter.ToolRemainingWork[good] += toolTransfer;
        }
        TransferDomesticHerds(parent, daughter);
        parent.Fissions++;
        parent.LastFissionSeason = season;
        parent.FissionPending = false;
        parent.Cohesion = Fix64.Max(Fix64.Parse("0.6"), parent.Cohesion - Fix64.Parse("0.05"));
        daughter.FireLit = parent.FireLit;
        parent.FireKeeperId = FindFireKeeper(parent);
        daughter.FireKeeperId = daughter.FireLit ? FindFireKeeper(daughter) : -1;
        RebuildWeirOwnership();
        return daughter;
    }

    private static bool IsSkilled(FigureState figure)
    {
        foreach (byte level in figure.SkillLevels)
            if (level > 0) return true;
        return false;
    }

    private static void TransferDomesticHerds(SettlementState parent, SettlementState daughter)
    {
        Fix64 small = parent.DomesticSmallStock * Fix64.FromRatio(2, 5);
        Fix64 cattle = parent.DomesticCattle * Fix64.FromRatio(2, 5);
        Fix64 pigs = parent.DomesticPigs * Fix64.FromRatio(2, 5);
        parent.DomesticSmallStock -= small; daughter.DomesticSmallStock += small;
        parent.DomesticCattle -= cattle; daughter.DomesticCattle += cattle;
        parent.DomesticPigs -= pigs; daughter.DomesticPigs += pigs;
        if (small > Fix64.Zero || cattle + pigs > Fix64.Zero)
        {
            int smallPastures = (small / Fix64.FromInt(6)).FloorToInt();
            if (Fix64.FromInt(smallPastures * 6) < small) smallPastures++;
            int largePastures = ((cattle + pigs) / Fix64.FromInt(2)).FloorToInt();
            if (Fix64.FromInt(largePastures * 2) < cattle + pigs) largePastures++;
            // Fission carries enough fenced capacity for its transferred animals; build assets are
            // otherwise not divided by the existing settlement-fission system.
            daughter.Structures[(int)Structure.Pasture] = Math.Max(1, Math.Max(smallPastures, largePastures));
        }
    }

    private void MoveFigure(long figureId, int settlementId)
    {
        for (int i = 0; i < _figures.Count; i++)
            if (_figures[i].Id == figureId) { _figures[i].SettlementId = settlementId; return; }
    }

    private int ChooseFissionSite(SettlementState parent)
    {
        int best = -1;
        Fix64 bestScore = Fix64.MinValue;
        int reach = Math.Max(14, SiteAdvisor?.SettleReach(parent.Civilization) ?? 14);
        for (int tile = 0; tile < _world.TileCount; tile++)
        {
            if (!IsHabitable(tile)) continue;
            int parentDistance = HexGrid.Distance(tile, parent.SiteTile, _world.Width);
            if (parentDistance < Radius * 2 + 1 || parentDistance > reach) continue;
            bool farEnough = true;
            for (int s = 0; s < _settlements.Count; s++)
                if (HexGrid.Distance(tile, _settlements[s].SiteTile, _world.Width) < Radius * 2 + 1)
                { farEnough = false; break; }
            if (!farEnough) continue;
            if (SiteAdvisor is not null && !SiteAdvisor.Reachable(parent.Civilization, parent.SiteTile, tile)) continue;
            if (SiteAdvisor is not null && !SiteAdvisor.Allows(parent.Civilization, tile)) continue;
            Fix64 score = _siteFoodScores[tile];
            if (SiteAdvisor is not null) score *= SiteAdvisor.Multiplier(parent.Civilization, tile);
            if (score > bestScore)
            {
                best = tile;
                bestScore = score;
            }
            else if (score == bestScore)
            {
                if (best < 0 || PreferFissionTieCandidate(parent.SiteTile, tile, best, _world.Width, _world.Height)) best = tile;
            }
        }
        return best;
    }

    // The generated map is point-symmetric. Resolve equal food scores in the same parent-relative direction,
    // so mirrored parents with mirrored candidate sets choose mirrored fission sites.
    internal static bool PreferFissionTieCandidate(int parentTile, int candidateTile, int currentBestTile, int width, int height)
    {
        int rotatedParent = HexGrid.Rotate180(parentTile, width, height);
        return parentTile <= rotatedParent ? candidateTile < currentBestTile : candidateTile > currentBestTile;
    }

    private Fix64 SiteFoodScore(int center)
    {
        Fix64 score = Fix64.Zero;
        foreach (int tile in RangeTiles(center))
        {
            WorldEconomyTile input = _world.Tile(tile);
            score += input.SpringForage + input.SummerForage + input.AutumnForage + input.WinterForage;
            score += HerdFoodGrowthPerYear(tile) * Fix64.FromRatio(2, 5);
            score += input.TotalFishGrowthPerYear;
        }
        return score;
    }

    private bool IsHabitable(int tile)
    {
        Biome biome = (Biome)_world.Tile(tile).BiomeId;
        return biome is not (Biome.Ocean or Biome.Coast or Biome.Ice or Biome.Peak);
    }

    private SettlementState NewSettlement(int civ, int site)
    {
        var settlement = new SettlementState
        {
            Id = _settlements.Count,
            Civilization = civ,
            SiteTile = site,
            BuildTarget = Structure.Count,
        };
        return settlement;
    }

    private void SeedPopulation(SettlementState settlement)
    {
        AddFigure(settlement.Civilization, settlement.Id, 6, true);
        AddFigure(settlement.Civilization, settlement.Id, 9, false);
        AddFigure(settlement.Civilization, settlement.Id, 18, true);
        AddFigure(settlement.Civilization, settlement.Id, 20, false);
        AddFigure(settlement.Civilization, settlement.Id, 22, true);
        AddFigure(settlement.Civilization, settlement.Id, 24, false);
        AddFigure(settlement.Civilization, settlement.Id, 26, true);
        AddFigure(settlement.Civilization, settlement.Id, 28, false);
        AddFigure(settlement.Civilization, settlement.Id, 30, true);
        AddFigure(settlement.Civilization, settlement.Id, 32, false);
        AddFigure(settlement.Civilization, settlement.Id, 50, true);
        AddFigure(settlement.Civilization, settlement.Id, 55, false);

    }

    private void AddFigure(int civ, int settlementId, int age, bool female)
    {
        _figures.Add(new FigureState { Id = _nextFigureId++, Civilization = civ, SettlementId = settlementId, Age = age, Female = female });
    }

    private int ProjectIndex(string projectId)
    {
        // Packs list projects in ordinal id order, so look the id up by bisection (every HasProject call comes here).
        int low = 0, high = _content.Projects.Length - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            int order = string.CompareOrdinal(_content.Projects[mid].Id, projectId);
            if (order == 0) return _content.Projects[mid].Index;
            if (order < 0) low = mid + 1; else high = mid - 1;
        }
        return -1;
    }

    private bool HasProject(int civ, string id)
    {
        int index = ProjectIndex(id);
        return index >= 0 && _completedProjects[civ][index];
    }

    private static int ProjectArrayLength(EconomyContent content)
    {
        int length = 0;
        for (int i = 0; i < content.Projects.Length; i++) length = Math.Max(length, content.Projects[i].Index + 1);
        return length;
    }

    private Fix64 FoodCap(SettlementState settlement)
    {
        Fix64 annual = SustainableFoodIncome(settlement, out _, out _, out _, out _, out _);
        Fix64 annualNeedPerPerson = AnnualFoodNeedPerPerson(settlement);
        if (annualNeedPerPerson <= Fix64.Zero) return Fix64.Zero;
        return (annual + FoodStores(settlement)) / annualNeedPerPerson;
    }

    private Fix64 AnnualFoodNeedPerPerson(SettlementState settlement)
    {
        int population = Population(settlement.Id);
        return population == 0
            ? Fix64.Parse("3.6")
            : (FoodNeed(settlement, false) * Fix64.FromInt(3) +
               FoodNeed(settlement, true, includeHarshWinter: false)) / Fix64.FromInt(population);
    }

    private Fix64 SustainableFoodIncome(SettlementState settlement, out Fix64 winterIncome,
        out Fix64 plantIncome, out Fix64 meatIncome, out Fix64 grainIncome, out Fix64 fishIncome)
    {
        int civ = settlement.Civilization;
        plantIncome = Fix64.Zero;
        winterIncome = Fix64.Zero;
        meatIncome = Fix64.Zero;
        grainIncome = Fix64.Zero;
        fishIncome = Fix64.Zero;
        Fix64 meatValue = FoodValue(Good.Meat, settlement);
        Fix64 fishValue = Fix64.Zero;
        bool hasFishValue = false;
        foreach (int tile in RangeTiles(settlement.SiteTile))
        {
            WorldEconomyTile input = _world.Tile(tile);
            for (int season = 0; season < 4; season++)
            {
                // FoodCap is based on normal renewable yield. Droughts and the short-lived
                // +40% wildfire pulse change this year's harvest, not a settlement's
                // sustainable population capacity.
                Fix64 plants = SeasonalForageRegrowth(tile, season, includeTransientEvents: false) *
                    ReachableForageShare(civ) * SeasonalForageFoodValue(settlement, season);
                plantIncome += plants;
                if (season == 3) winterIncome += plants;
            }

            Fix64 sustainableHerd = HerdFoodGrowthPerYear(tile, includeTransientEvents: false) * HuntShare(civ);
            meatIncome += sustainableHerd * meatValue;
            winterIncome += sustainableHerd / Fix64.FromInt(4) * meatValue;

            if (input.FishGrowthPerYear > Fix64.Zero)
            {
                if (!hasFishValue)
                {
                    fishValue = FoodValue(Good.Fish, settlement);
                    hasFishValue = true;
                }
                if (CanFishTile(settlement, tile))
                {
                    // HNT-07's sustainable take is 0.6 of regrowth. Reach is tile-based:
                    // CanFishTile admits shore tiles and the later techs open deeper water.
                    fishIncome += input.TotalFishGrowthPerYear * SustainableFishTakeShare * fishValue;
                    winterIncome += input.FishGrowthPerYear / Fix64.FromInt(4) * SustainableFishTakeShare * fishValue;
                }
                if (CanUseWeir(settlement) && IsServicedWeirTile(settlement, tile))
                {
                    // A serviced weir yields 2 R each season; Autumn salmon adds two
                    // extra yields on a run tile, matching WeirCatch's live production.
                    Fix64 annualWeirCatch = Fix64.FromInt(8);
                    if (input.SalmonRun) annualWeirCatch += Fix64.FromInt(4);
                    fishIncome += annualWeirCatch * fishValue;
                    winterIncome += Fix64.FromInt(2) * fishValue;
                }
            }
        }

        Fix64 domesticAnnualMeat = DomesticAnnualMeat(settlement);
        meatIncome += domesticAnnualMeat * meatValue;
        winterIncome += domesticAnnualMeat / Fix64.FromInt(4) * meatValue;
        if (settlement.Civic is not null)
        {
            Fix64 dairy = DairyIncome(settlement);
            meatIncome += dairy;
            winterIncome += dairy / 4;
        }

        if (HasProject(civ, "AGR-01"))
        {
            foreach (int tile in settlement.FieldTiles)
            {
                WorldEconomyTile input = _world.Tile(tile);
                Fix64 soilRatio = input.SoilFertility <= Fix64.Zero
                    ? Fix64.Parse("0.3")
                    : Fix64.Clamp(_soil[tile] / input.SoilFertility, Fix64.Parse("0.3"), Fix64.One);
                if (settlement.Civic is not null)
                {
                    Fix64 irrigation = IrrigationFactor(settlement, tile, out bool fullSoil);
                    if (fullSoil) soilRatio = Fix64.One;
                    soilRatio *= irrigation;
                }
                Fix64 yield = input.FieldYieldPerYear * soilRatio * Modifier(civ, "field_yield");
                if (HasProject(civ, "AGR-02")) yield *= Fix64.FromInt(2);
                grainIncome += yield * FoodValue(Good.Grain, settlement);
            }
        }

        return plantIncome + meatIncome + fishIncome + grainIncome +
            OrchardAnnualRations(settlement) * FoodValue(Good.Fruit, settlement);
    }

    private static Fix64 OrchardAnnualRations(SettlementState settlement)
    {
        int mature = 0;
        foreach (OrchardPlot orchard in settlement.OrchardPlots)
            if (orchard.AgeSeasons is >= 24 and < 224) mature++;
        return Fix64.FromInt(3 * mature);
    }

    private static Fix64 DomesticAnnualMeat(SettlementState settlement)
        => settlement.DomesticSmallStock * Fix64.Parse("0.8") +
            settlement.DomesticCattle * Fix64.Parse("2.5") +
            settlement.DomesticPigs * Fix64.Parse("1.2");

    // The C2 diagnostic is a sustainable late-winter share at the settlement's food capacity.
    // It is intentionally separate from LateWinterFoodShare, which records actual winter security
    // for starvation and fission decisions and therefore depends on the current population.
    private Fix64 C2LateWinterCapacityShare(SettlementState settlement)
    {
        Fix64 annual = SustainableFoodIncome(settlement, out Fix64 winterIncome,
            out _, out Fix64 meatIncome, out Fix64 grainIncome, out Fix64 fishIncome);
        if (annual <= Fix64.Zero) return Fix64.Zero;

        int population = Population(settlement.Id);
        Fix64 annualNeedPerPerson = population == 0
            ? Fix64.Parse("3.6")
            : (FoodNeed(settlement, false) * Fix64.FromInt(3) + FoodNeed(settlement, true, includeHarshWinter: false)) / Fix64.FromInt(population);
        Fix64 winterNeedPerPerson = population == 0
            ? Fix64.Parse("0.9")
            : FoodNeed(settlement, true, includeHarshWinter: false) / Fix64.FromInt(population);
        if (annualNeedPerPerson <= Fix64.Zero || winterNeedPerPerson <= Fix64.Zero) return Fix64.Zero;

        Fix64 foodCapacity = annual / annualNeedPerPerson;
        Fix64 winterNeedAtCapacity = foodCapacity * winterNeedPerPerson;
        if (winterNeedAtCapacity <= Fix64.Zero) return Fix64.Zero;
        Fix64 winterProductionShare = Fix64.Clamp(winterIncome / winterNeedAtCapacity, Fix64.Zero, Fix64.One);

        Fix64 storageFactor = settlement.Structures[(int)Structure.Granary] > 0
            ? Fix64.Parse("0.25")
            : settlement.Structures[(int)Structure.StoragePit] > 0 ? Fix64.Half : Fix64.One;
        Fix64 retainedFood = Fix64.Zero;
        Fix64 carryoverFood = Fix64.Zero;
        foreach (int tile in RangeTiles(settlement.SiteTile))
        {
            // Only Spring–Autumn production is carry-over surplus. Winter production is
            // already represented by w and must not be counted again as stored food.
            for (int season = 0; season < 3; season++)
            {
                Fix64 raw = SeasonalForageRegrowth(tile, season) * ReachableForageShare(settlement.Civilization);
                if (raw <= Fix64.Zero) continue;
                Fix64 nuts = season == 2 && HasProject(settlement.Civilization, "FOR-03")
                    ? raw * Fix64.Parse("0.4") : Fix64.Zero;
                Fix64 wildGrain = season == 2 && HasProject(settlement.Civilization, "FOR-04")
                    ? raw * Fix64.Parse("0.2") : Fix64.Zero;
                Fix64 fresh = raw - nuts - wildGrain;
                AddCarryover(ref carryoverFood, Good.Plant, fresh, settlement);
                AddCarryover(ref carryoverFood, Good.Nuts, nuts, settlement);
                AddCarryover(ref carryoverFood, Good.WildGrain, wildGrain, settlement);
                AddRetention(ref retainedFood, Good.Plant, fresh, settlement, storageFactor);
                AddRetention(ref retainedFood, Good.Nuts, nuts, settlement, storageFactor);
                AddRetention(ref retainedFood, Good.WildGrain, wildGrain, settlement, storageFactor);
            }
        }

        Fix64 meatValue = FoodValue(Good.Meat, settlement);
        Fix64 meatRations = meatValue <= Fix64.Zero ? Fix64.Zero : meatIncome / meatValue;
        bool canDry = HasProject(settlement.Civilization, "FOD-02") &&
            settlement.Structures[(int)Structure.DryingRack] > 0;
        Fix64 driedMeat = canDry ? meatRations * Fix64.Parse("0.075") : Fix64.Zero;
        Fix64 storedMeat = Fix64.Max(Fix64.Zero, meatRations * Fix64.Parse("0.75") - driedMeat);
        AddCarryover(ref carryoverFood, Good.Meat, storedMeat, settlement);
        AddCarryover(ref carryoverFood, Good.DriedFood, driedMeat, settlement);
        AddRetention(ref retainedFood, Good.Meat, storedMeat,
            settlement, storageFactor);
        // FOD-02 preserves 30% of Autumn's quarter, or 7.5% of annual meat.
        AddRetention(ref retainedFood, Good.DriedFood, driedMeat,
            settlement, storageFactor);
        Fix64 fishValue = FoodValue(Good.Fish, settlement);
        Fix64 carriedFishRations = Fix64.Zero;
        Fix64 autumnFishRations = Fix64.Zero;
        if (fishValue > Fix64.Zero)
        {
            Fix64 winterFishFood = Fix64.Zero;
            foreach (int tile in RangeTiles(settlement.SiteTile))
            {
                WorldEconomyTile input = _world.Tile(tile);
                if (input.FishGrowthPerYear <= Fix64.Zero ||
                    !(CanFishTile(settlement, tile) || CanUseWeir(settlement) && IsServicedWeirTile(settlement, tile))) continue;
                if (CanFishTile(settlement, tile))
                {
                    winterFishFood += input.FishGrowthPerYear / Fix64.FromInt(4) * SustainableFishTakeShare * fishValue;
                    autumnFishRations += (input.FishGrowthPerYear / Fix64.FromInt(4) + input.SalmonAutumnGrowth) * SustainableFishTakeShare;
                }
                if (CanUseWeir(settlement) && IsServicedWeirTile(settlement, tile))
                {
                    winterFishFood += Fix64.FromInt(2) * fishValue;
                    autumnFishRations += Fix64.FromInt(input.SalmonRun ? 6 : 2);
                }
            }
            carriedFishRations = Fix64.Max(Fix64.Zero, fishIncome - winterFishFood) / fishValue;
        }
        Fix64 driedFish = canDry
            ? Fix64.Min(carriedFishRations, autumnFishRations * Fix64.Parse("0.3"))
            : Fix64.Zero;
        Fix64 freshFish = Fix64.Max(Fix64.Zero, carriedFishRations - driedFish);
        AddCarryover(ref carryoverFood, Good.Fish, freshFish, settlement);
        AddCarryover(ref carryoverFood, Good.DriedFood, driedFish, settlement);
        AddRetention(ref retainedFood, Good.Fish, freshFish, settlement, storageFactor);
        AddRetention(ref retainedFood, Good.DriedFood, driedFish, settlement, storageFactor);
        Fix64 grainValue = FoodValue(Good.Grain, settlement);
        Fix64 grainRations = grainValue <= Fix64.Zero ? Fix64.Zero : grainIncome / grainValue;
        Fix64 fruitRations = OrchardAnnualRations(settlement);
        AddCarryover(ref carryoverFood, Good.Grain, grainRations, settlement);
        AddCarryover(ref carryoverFood, Good.Fruit, fruitRations, settlement);
        AddRetention(ref retainedFood, Good.Grain, grainRations, settlement, storageFactor);
        AddRetention(ref retainedFood, Good.Fruit, fruitRations, settlement, storageFactor);

        // C2 assumes carry-over is the proportional Spring–Autumn mix above; keep is the
        // survival ratio of that raw mix after 1.5 seasons, not a share of annual production.
        Fix64 keep = carryoverFood <= Fix64.Zero ? Fix64.Zero :
            Fix64.Clamp(retainedFood / carryoverFood, Fix64.Zero, Fix64.One);
        return Fix64.Clamp(winterProductionShare + (Fix64.One - winterProductionShare) * keep,
            Fix64.Zero, Fix64.One);
    }

    private void AddRetention(ref Fix64 retainedFood, Good good, Fix64 amount,
        SettlementState settlement, Fix64 storageFactor)
    {
        Fix64 food = amount * FoodValue(good, settlement);
        retainedFood += food * RetentionAfterOneAndHalfSeasons(good, storageFactor, settlement.Civilization);
    }

    private void AddCarryover(ref Fix64 carryoverFood, Good good, Fix64 amount, SettlementState settlement)
        => carryoverFood += amount * FoodValue(good, settlement);

    private Fix64 HousingCap(SettlementState settlement)
    {
        Fix64 capacity = Fix64.FromInt(40);
        capacity += Fix64.FromInt(4 * settlement.Structures[(int)Structure.LeanTo]);
        capacity += Fix64.FromInt(5 * settlement.Structures[(int)Structure.HideTent]);
        capacity += Fix64.FromInt(6 * settlement.Structures[(int)Structure.PitHouse]);
        capacity += Fix64.FromInt(8 * settlement.Structures[(int)Structure.WattleHouse]);
        capacity += Fix64.FromInt(8 * settlement.Structures[(int)Structure.MudbrickHouse]);
        capacity += Fix64.FromInt(25 * settlement.Structures[(int)Structure.Longhouse]);
        return settlement.Civic is null ? capacity : CivicHousing(settlement, capacity);
    }

    private Fix64 GroupCap(SettlementState settlement)
    {
        Fix64 capacity = Fix64.FromInt(25);
        for (int i = 0; i < _content.Effects.Length; i++)
        {
            EconomyEffect effect = _content.Effects[i];
            if (effect.Kind == "cap" && effect.Cap == "group" && effect.Value is not null &&
                _completedProjects[settlement.Civilization][effect.ProjectIndex])
                capacity = Fix64.Max(capacity, effect.Value.Value);
        }
        return capacity * Fix64.Clamp(settlement.Cohesion, Fix64.Parse("0.6"), Fix64.Parse("1.2"));
    }

    private Fix64 FoodStores(SettlementState settlement)
    {
        Fix64 total = Fix64.Zero;
        for (int i = 0; i < FoodGoods.Length; i++)
            total += settlement.Stores[(int)FoodGoods[i]] * FoodValue(FoodGoods[i], settlement);
        if (settlement.Metal is not null) total += MetalFoodStores(settlement);
        return total;
    }

    private Fix64 FoodValue(Good good, SettlementState settlement)
        => settlement.Civic is null ? FoodValue(good, settlement.Civilization, settlement.FireLit)
            : FoodValue(good, settlement.Civilization, settlement.FireLit) * SettlementFoodValueMult(settlement, good);

    private Fix64 FoodValue(Good good, int civ)
        => FoodValue(good, civ, hasFire: true);

    private Fix64 FoodValue(Good good, int civ, bool hasFire)
    {
        // Archetypes change usable food consistently in stores, consumption and derived income.
        Fix64 value = Modifier(civ, "food_value") * Profile(civ).FoodCapacity;
        if (good is Good.Meat or Good.Roots or Good.WildGrain or Good.Grain)
        {
            Fix64 cooking = Modifier(civ, "food_value_of_meat_roots_cereals");
            if (!hasFire && HasProject(civ, "FOD-01")) cooking /= Fix64.Parse("1.3");
            value *= cooking;
        }
        if (good is Good.Grain or Good.WildGrain) value *= Modifier(civ, "cereal_food_value");
        return Fix64.Max(Fix64.Zero, value);
    }

    private Fix64 FoodNeed(SettlementState settlement, bool winter)
        => FoodNeed(settlement, winter, includeHarshWinter: true);

    private Fix64 FoodNeed(SettlementState settlement, bool winter, bool includeHarshWinter)
    {
        int population = Population(settlement.Id);
        Fix64 total = Fix64.Zero;
        ClimateBand climate = (ClimateBand)_world.Tile(settlement.SiteTile).ClimateId;
        int requiredWarmth = WarmthRequirement(climate, winter);
        if (winter && includeHarshWinter && _lastWinterWasHarsh && requiredWarmth > 0) requiredWarmth++;
        int warmthOrdinal = 0;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState figure = _figures[i];
            if (figure.SettlementId != settlement.Id) continue;
            Fix64 need = figure.Age < 14 ? Fix64.Parse("0.6") : figure.Age >= 45 ? Fix64.Parse("0.8") : Fix64.One;
            if (requiredWarmth > FigureWarmthTier(settlement, figure, population, warmthOrdinal)) need *= Fix64.Parse("1.15");
            total += need;
            warmthOrdinal++;
        }
        return total;
    }

    private Fix64 ForecastWinterFood(SettlementState settlement, ScoutPlan plannedScouts)
    {
        Fix64 need = FoodNeed(settlement, true);
        if (need <= Fix64.Zero) return Fix64.One;
        Fix64 storageFactor = settlement.Structures[(int)Structure.Granary] > 0
            ? Fix64.Parse("0.25")
            : settlement.Structures[(int)Structure.StoragePit] > 0 ? Fix64.Half : Fix64.One;
        Fix64 retained = Fix64.Zero;
        foreach (Good good in FoodGoods)
            retained += settlement.Stores[(int)good] * FoodValue(good, settlement) *
                RetentionAfterOneAndHalfSeasons(good, storageFactor, settlement.Civilization);

        Fix64 winterProduction = ForecastWinterProduction(settlement, retained, need, plannedScouts);
        return Fix64.Clamp((retained + winterProduction) / need, Fix64.Zero, Fix64.One);
    }

    private Fix64 ForecastWinterProduction(SettlementState settlement, Fix64 retainedFood, Fix64 need, ScoutPlan plannedScouts)
    {
        int civ = settlement.Civilization;
        Fix64 plants = Fix64.Zero, meat = Fix64.Zero;
        // Expected value of the live winter climate draw: 30% harsh, 25% good, 45% mild.
        Fix64 winterClimate = Fix64.Parse("0.9535");
        foreach (int tile in RangeTiles(settlement.SiteTile))
        {
            WorldEconomyTile input = _world.Tile(tile);
            Fix64 winterRegrowth = SeasonalForageRegrowth(tile, 3) * winterClimate;
            Fix64 forageLimit = winterRegrowth + SeasonalForageRegrowth(tile, 0);
            plants += Fix64.Min(_forageStock[tile] + winterRegrowth, forageLimit) * ReachableForageShare(civ);
            Fix64 herdGrowth = HerdFoodGrowthPerYear(tile);
            Fix64 herdAvailable = Fix64.Min(_herdFoodStock[tile] + herdGrowth / 4, HerdFoodStockCapacity(tile));
            meat += Fix64.Min(herdAvailable, herdGrowth * HuntShare(civ) / 4);
        }

        Fix64 totalWork = Fix64.Zero;
        Fix64 scoutWork = plannedScouts.Work;
        foreach (FigureState figure in _figures)
        {
            if (figure.SettlementId != settlement.Id) continue;
            totalWork += WorkCapacity(figure);
            if (figure.Job == EconomyJob.Scout) scoutWork += figure.WorkThisSeason;
        }
        totalWork = Fix64.Max(Fix64.Zero, totalWork - scoutWork);
        Fix64 upkeep = WeirUpkeepBudget(settlement, totalWork);
        totalWork -= upkeep;
        totalWork -= OrchardUpkeepBudget(settlement, totalWork);
        Fix64 upkeepRemaining = upkeep;
        Fix64 weirFish = WeirCatch(settlement, upkeep, 3, true);
        int weirs = UsableWeirCount(settlement);
        Fix64 weirShare = weirs > 0
            ? Fix64.Min(Fix64.One, upkeep / (Fix64.FromInt(weirs) * Fix64.Parse("0.1")))
            : Fix64.Zero;
        bool fishAvailable = false;
        foreach (int tile in RangeTiles(settlement.SiteTile))
            if (CanFishTile(settlement, tile) && ForecastFishAfterWeir(settlement, tile, weirShare) > Fix64.Zero)
            { fishAvailable = true; break; }
        Fix64 foodShare = retainedFood < need ? Fix64.Parse("0.85") :
            retainedFood < need * 2 ? Fix64.Parse("0.70") : Fix64.Parse("0.48");
        if (settlement.FoodSecurity < Fix64.Parse("0.8")) foodShare = Fix64.Parse("0.9");
        Fix64 foodRemaining = totalWork * foodShare;
        Fix64 forageWork = Fix64.Zero, huntWork = Fix64.Zero, fishWork = Fix64.Zero;
        Fix64 forageSkillWork = Fix64.Zero, huntSkillWork = Fix64.Zero, fishSkillWork = Fix64.Zero;
        int ordinal = 0;
        for (int figureIndex = 0; figureIndex < _figures.Count; figureIndex++)
        {
            FigureState figure = _figures[figureIndex];
            if (figure.SettlementId != settlement.Id || figure.Job == EconomyJob.Scout ||
                plannedScouts.Contains(figureIndex) || foodRemaining <= Fix64.Zero) continue;
            Fix64 available = WorkCapacity(figure);
            Fix64 paidUpkeep = Fix64.Min(available, upkeepRemaining);
            available -= paidUpkeep;
            upkeepRemaining -= paidUpkeep;
            Fix64 amount = Fix64.Min(available, foodRemaining);
            if (amount <= Fix64.Zero) continue;
            if (CanFish(settlement) && fishAvailable && ordinal % 5 == 0)
            {
                fishWork += amount;
                fishSkillWork += amount * SkillMultiplier(figure, SkillFamily.Fish);
            }
            else if (meat > Fix64.Zero && ordinal % 3 == 0)
            {
                huntWork += amount;
                huntSkillWork += amount * SkillMultiplier(figure, SkillFamily.Hunt);
            }
            else if (!(HasProject(civ, "FOD-02") && settlement.Structures[(int)Structure.DryingRack] > 0 &&
                settlement.Stores[(int)Good.Meat] > Fix64.FromInt(3) && ordinal % 7 == 0))
            {
                forageWork += amount;
                forageSkillWork += amount * SkillMultiplier(figure, SkillFamily.Forage);
            }
            // Preserving transfers existing food, so it adds no newly produced rations here.
            foodRemaining -= amount;
            ordinal++;
        }

        Fix64 forageRate = ActionRate("forage", Fix64.Parse("3.2"), settlement, "forage") * Modifier(civ, "forage.yield");
        int huntParty = PlannedHuntPartySize(settlement);
        Fix64 scavengeRate = ScavengeRate(settlement, huntParty);
        Fix64 huntRate = ActionRate("hunt", scavengeRate, settlement, "hunt", huntParty);
        Fix64 fishRate = ActionRate("fish", Fix64.One, settlement, "fish");
        Fix64 appliedForageWork = LimitActionWorkByTool(settlement, "forage", forageWork);
        Fix64 appliedHuntWork = LimitActionWorkByTool(settlement, "hunt", huntWork, huntParty);
        Fix64 appliedFishWork = LimitActionWorkByTool(settlement, "fish", fishWork);
        Fix64 productiveForageWork = forageWork <= Fix64.Zero ? Fix64.Zero :
            forageSkillWork * appliedForageWork / forageWork;
        Fix64 productiveHuntWork = huntWork <= Fix64.Zero ? Fix64.Zero :
            huntSkillWork * appliedHuntWork / huntWork;
        Fix64 productiveFishWork = fishWork <= Fix64.Zero ? Fix64.Zero :
            fishSkillWork * appliedFishWork / fishWork;
        Fix64 produced = Fix64.Min(plants, productiveForageWork * forageRate) * SeasonalForageFoodValue(settlement, 3) +
            Fix64.Min(meat, productiveHuntWork * huntRate) * FoodValue(Good.Meat, settlement) +
            ForecastFishHarvest(settlement, productiveFishWork, fishRate, weirShare) * FoodValue(Good.Fish, settlement);
        produced += weirFish * FoodValue(Good.Fish, settlement);
        Fix64 maintenanceNeed = DomesticHerdWorkNeed(settlement);
        if (maintenanceNeed > Fix64.Zero)
        {
            Fix64 buildReserve = Fix64.Min(Fix64.One, totalWork * Fix64.Parse("0.12"));
            Fix64 materialBudget = Fix64.Min(totalWork * Fix64.Parse("0.18"),
                Fix64.Max(Fix64.Zero, totalWork * (Fix64.One - foodShare) - buildReserve));
            Fix64 herdBudget = CanTameFromRange(settlement) ? materialBudget * Fix64.Half : Fix64.Zero;
            herdBudget = Fix64.Min(materialBudget, Fix64.Max(herdBudget, maintenanceNeed));
            // Clamp before dividing. A newly tamed, tiny herd can have a positive maintenance
            // need close to one raw Q32.32 unit while the budget is ordinary-sized; computing
            // that ratio first can overflow even though the intended share is simply one.
            Fix64 maintenanceShare = herdBudget >= maintenanceNeed
                ? Fix64.One
                : herdBudget / maintenanceNeed;
            produced += DomesticAnnualMeat(settlement) / Fix64.FromInt(4) *
                FoodValue(Good.Meat, settlement) * maintenanceShare;
        }
        return produced;
    }

    private Fix64 ForecastFishAfterWeir(SettlementState settlement, int tile, Fix64 weirShare)
    {
        WorldEconomyTile input = _world.Tile(tile);
        Fix64 available = Fix64.Min(_fishStock[tile] + SeasonalFishRegrowth(tile, 3),
            input.InitialFish + input.TotalFishGrowthPerYear);
        if (_weirOwner[tile] == settlement.Id)
            available -= Fix64.Min(available, Fix64.FromInt(2) * weirShare);
        return available;
    }

    private Fix64 ForecastFishHarvest(SettlementState settlement, Fix64 work, Fix64 baseRate, Fix64 weirShare)
    {
        if (work <= Fix64.Zero || baseRate <= Fix64.Zero) return Fix64.Zero;
        Fix64 remainingWork = work;
        Fix64 caught = Fix64.Zero;
        foreach (int tile in RangeTiles(settlement.SiteTile))
        {
            if (remainingWork <= Fix64.Zero) break;
            if (!CanFishTile(settlement, tile)) continue;
            Fix64 rate = FishTileRate(settlement, tile, baseRate);
            Fix64 take = Fix64.Min(remainingWork * rate, ForecastFishAfterWeir(settlement, tile, weirShare));
            if (take <= Fix64.Zero) continue;
            caught += take;
            remainingWork -= take / rate;
        }
        return caught;
    }

    private Fix64 RetentionAfterOneAndHalfSeasons(Good good, Fix64 storageFactor, int civ)
    {
        Fix64 spoilage = SpoilageFor(good);
        if (good is Good.Plant or Good.Fruit or Good.Meat or Good.Fish or Good.Grain or Good.DriedFood)
            spoilage *= Modifier(civ, "spoilage_of_stored_food");
        spoilage = Fix64.Clamp(spoilage * storageFactor, Fix64.Zero, Fix64.One);
        Fix64 perSeasonKeep = Fix64.One - spoilage;
        return perSeasonKeep * Fix64.Sqrt(perSeasonKeep);
    }

    private int WarmthTier(SettlementState settlement)
    {
        int population = Population(settlement.Id);
        int total = 0;
        int warmthOrdinal = 0;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState figure = _figures[i];
            if (figure.SettlementId != settlement.Id) continue;
            total += FigureWarmthTier(settlement, figure, population, warmthOrdinal++);
        }
        return population == 0 ? 0 : total / population;
    }

    private int FigureWarmthTier(SettlementState settlement, FigureState figure, int population, int ordinal)
    {
        int warmShelterSeats = 6 * settlement.Structures[(int)Structure.PitHouse] +
            8 * settlement.Structures[(int)Structure.WattleHouse] +
            8 * settlement.Structures[(int)Structure.MudbrickHouse] +
            25 * settlement.Structures[(int)Structure.Longhouse];
        int basicShelterSeats = 4 * settlement.Structures[(int)Structure.LeanTo] +
            5 * settlement.Structures[(int)Structure.HideTent];
        int shelter = ordinal < warmShelterSeats ? 2 : ordinal < warmShelterSeats + basicShelterSeats ? 1 : 0;

        int tailoredSeats = Math.Min(population, Math.Max(0, settlement.Stores[(int)Good.Tailored].FloorToInt()));
        int basicClothingSeats = Math.Min(population,
            Math.Max(0, settlement.Stores[(int)Good.Wrap].FloorToInt()) +
            Math.Max(0, settlement.Stores[(int)Good.ClothGarment].FloorToInt()));
        int clothing = ordinal < tailoredSeats ? 2 : ordinal < tailoredSeats + basicClothingSeats ? 1 : 0;
        int fire = settlement.FireLit ? 1 : 0;
        return fire + shelter + clothing;
    }

    private static int WarmthRequirement(ClimateBand climate, bool winter) => climate switch
    {
        ClimateBand.Cold => winter ? 3 : 1,
        ClimateBand.Temperate => winter ? 2 : 0,
        ClimateBand.Warm or ClimateBand.Arid => winter ? 1 : 0,
        _ => 0,
    };

    private int WarmthDeficit(SettlementState settlement, bool winter, bool includeHarshWinter = true)
    {
        int population = Population(settlement.Id);
        ClimateBand climate = (ClimateBand)_world.Tile(settlement.SiteTile).ClimateId;
        int required = WarmthRequirement(climate, winter);
        if (includeHarshWinter && winter && _lastWinterWasHarsh && required > 0) required++;
        if (required == 0) return 0;
        int deficit = 0;
        int warmthOrdinal = 0;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState figure = _figures[i];
            if (figure.SettlementId == settlement.Id)
            {
                deficit += Math.Max(0, required - FigureWarmthTier(settlement, figure, population, warmthOrdinal));
                warmthOrdinal++;
            }
        }
        return deficit;
    }

    private int PlannedHuntPartySize(SettlementState settlement)
    {
        int hunters = 0;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState figure = _figures[i];
            if (figure.SettlementId == settlement.Id && figure.Age >= 14 &&
                figure.IllnessSeasons == 0 && figure.WoundSeasons == 0 && WorkCapacity(figure) > Fix64.Zero)
                hunters++;
        }
        if (hunters == 0) return 0;

        int civ = settlement.Civilization;
        if (HasProject(civ, "HNT-06") && HasGood(settlement, "G-BOW")) return Math.Min(2, hunters);
        if (HasProject(civ, "HNT-05") && HasGood(settlement, "G-ATLATL") && hunters >= 2) return Math.Min(4, hunters);
        if (HasProject(civ, "HNT-04") && HasGood(settlement, "G-TOOL4") && hunters >= 6) return 6;
        if (HasProject(civ, "HNT-03") && HasGood(settlement, "G-TOOL4") && hunters >= 2) return Math.Min(4, hunters);
        if (HasProject(civ, "HNT-02") && HasGood(settlement, "G-SPEAR") && hunters >= 2) return Math.Min(4, hunters);
        if (HasProject(civ, "HNT-01") && HasGood(settlement, "G-CLUB") && hunters >= 3) return 3;
        return Math.Min(3, hunters);
    }

    private bool HuntPartyMatches(string methodAction, int rung, int partySize)
    {
        if (methodAction == "scavenge" && rung == 1) return partySize >= 3;
        if (methodAction != "hunt") return true;
        return rung switch
        {
            1 or 2 => partySize is >= 2 and <= 4,
            3 => partySize >= 6,
            4 => partySize is >= 2 and <= 4,
            5 => partySize is >= 1 and <= 2,
            _ => true,
        };
    }

    private Fix64 HuntInjuryRate(SettlementState settlement, int partySize)
    {
        int rung = -1;
        foreach (EconomyEffect effect in _content.Effects)
        {
            if (effect.Kind != "method" || effect.Action != "hunt" || effect.Rung is null ||
                !_completedProjects[settlement.Civilization][effect.ProjectIndex] ||
                !HuntPartyMatches("hunt", effect.Rung.Value, partySize) ||
                !HasRequiredTool(settlement, "hunt", effect.Rung.Value) || effect.Rung.Value <= rung) continue;
            rung = effect.Rung.Value;
        }
        if (rung < 0 && HasProject(settlement.Civilization, "HNT-01") &&
            HuntPartyMatches("scavenge", 1, partySize) && HasRequiredTool(settlement, "scavenge", 1))
            return Fix64.Parse("0.015");
        return rung switch
        {
            1 => Fix64.Parse("0.02"),
            2 or 3 => Fix64.Parse("0.016"),
            4 => Fix64.Parse("0.011"),
            5 => Fix64.Parse("0.008"),
            _ => Fix64.Parse("0.01"),
        };
    }

    private Fix64 ActionRate(string baseAction, Fix64 fallback, SettlementState settlement, string methodAction,
        int huntPartySize = int.MaxValue, Fix64 toolWork = default)
    {
        int civ = settlement.Civilization;
        Fix64 baseRate = fallback;
        for (int i = 0; i < _content.Actions.Length; i++)
            if (_content.Actions[i].Id == baseAction && _content.Actions[i].BaseRate is { } rate) { baseRate = rate; break; }

        int bestRung = BestActionRung(settlement, methodAction, huntPartySize);
        Fix64? bestRate = null;
        for (int i = 0; i < _content.Effects.Length; i++)
        {
            EconomyEffect effect = _content.Effects[i];
            if (effect.Kind == "method" && effect.Action == methodAction && effect.Rung == bestRung &&
                _completedProjects[civ][effect.ProjectIndex])
            {
                bestRate = effect.Rate ?? (effect.Mult is { } mult ? baseRate * mult : baseRate);
                break;
            }
        }
        if (bestRung < 0) return baseRate;
        Fix64 quality = toolWork > Fix64.Zero
            ? AverageMethodToolQuality(settlement, methodAction, bestRung, toolWork)
            : MethodToolQuality(settlement, methodAction, bestRung);
        Fix64 result = (bestRate ?? baseRate) * quality;
        if (methodAction is "hunt" or "scavenge")
        {
            result *= HuntRateModifier(civ, huntPartySize);
            if (methodAction == "hunt" && bestRung == 1) result *= Modifier(civ, "hunt_r1");
        }
        return result;
    }

    private int BestActionRung(SettlementState settlement, string action, int huntPartySize)
    {
        int bestRung = -1;
        for (int i = 0; i < _content.Effects.Length; i++)
        {
            EconomyEffect effect = _content.Effects[i];
            if (effect.Kind != "method" || effect.Action != action || effect.Rung is not { } rung ||
                !_completedProjects[settlement.Civilization][effect.ProjectIndex] ||
                !HasRequiredTool(settlement, action, rung) || !HuntPartyMatches(action, rung, huntPartySize)) continue;
            if (rung > bestRung) bestRung = rung;
        }
        return bestRung;
    }

    private Fix64 HuntRateModifier(int civ, int partySize)
    {
        Fix64 modifier = Modifier(civ, "hunt");
        if (partySize < 2 && HasProject(civ, "COM-01"))
            modifier /= Fix64.Parse("1.1");
        return modifier;
    }

    private Fix64 ScavengeRate(SettlementState settlement, int partySize, Fix64 toolWork = default)
    {
        Fix64 rate = ActionRate("scavenge", Fix64.Parse("0.9"), settlement, "scavenge", partySize, toolWork);
        return HasProject(settlement.Civilization, "HUS-01") ? rate * Fix64.Parse("1.3") : rate;
    }

    private Fix64 LimitActionWorkByTool(SettlementState settlement, string action, Fix64 work,
        int huntPartySize = int.MaxValue)
    {
        if (work <= Fix64.Zero) return Fix64.Zero;
        int rung = BestActionRung(settlement, action, huntPartySize);
        if (rung < 0 && action == "hunt")
        {
            action = "scavenge";
            rung = BestActionRung(settlement, action, huntPartySize);
        }
        if (rung < 0) return work;
        Good tool = ToolForMethod(action, rung);
        if (tool == Good.Count) return work;
        Fix64 remaining = AvailableToolWork(settlement, tool);
        if (action == "fish" && rung == 2)
            remaining = Fix64.Min(remaining, AvailableToolWork(settlement, Good.Net));
        return Fix64.Min(work, remaining);
    }

    private Fix64 AvailableToolWork(SettlementState settlement, Good good)
    {
        Fix64 quantity = settlement.Stores[(int)good];
        if (quantity < Fix64.One) return Fix64.Zero;
        Fix64 durability = ToolDurability(settlement, good);
        int wholeUnits = quantity.FloorToInt();
        Fix64 fractionalReserve = (quantity - Fix64.FromInt(wholeUnits)) * durability;
        Fix64 usableRemainder = settlement.ToolRemainingWork[(int)good] - fractionalReserve;
        return Fix64.Clamp(usableRemainder, Fix64.Zero, Fix64.FromInt(wholeUnits) * durability);
    }

    private void ScaleHuntWork(SettlementState settlement, Fix64 assignedWork, Fix64 appliedWork)
    {
        if (assignedWork <= Fix64.Zero || appliedWork >= assignedWork) return;
        Fix64 scale = appliedWork / assignedWork;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState figure = _figures[i];
            if (figure.SettlementId == settlement.Id) figure.HuntWorkThisSeason *= scale;
        }
    }

    private Fix64 ToolDurability(SettlementState settlement, Good good)
    {
        Fix64 durability = good switch
        {
        Good.Stick or Good.Club => Fix64.FromInt(4),
        Good.Basket or Good.FishingKit or Good.SpearThrower => Fix64.FromInt(8),
        Good.Spear => Fix64.FromInt(5),
        Good.Sickle or Good.Harpoon => Fix64.FromInt(6),
        Good.Bow or Good.Tool7 => Fix64.FromInt(10),
        Good.Net => Fix64.FromInt(12),
        Good.Tool1 => Fix64.One,
        Good.Tool2 or Good.Tool3 => Fix64.FromInt(2),
        Good.Tool4 => Fix64.FromInt(3),
        Good.Tool5 => Fix64.Parse("4.5"),
        Good.Tool6 => Fix64.FromInt(4),
        _ => Fix64.Zero,
        };
        if (durability > Fix64.Zero && HasProject(settlement.Civilization, "FIR-04") &&
            good is Good.Stick or Good.Club or Good.Spear or Good.Tool1 or Good.Tool2 or Good.Tool3 or Good.Tool4)
            durability *= Modifier(settlement.Civilization, "wooden_tool_durability");
        if (durability > Fix64.Zero && good is Good.Tool4 or Good.Spear)
            durability *= Modifier(settlement.Civilization, "hafted_tool_durability");
        if (durability > Fix64.Zero && good == Good.Tool5)
            durability *= Modifier(settlement.Civilization, "tool_durability");
        return durability;
    }

    private Fix64 ToolQuality(SettlementState settlement, Good good)
    {
        Fix64 quantity = settlement.Stores[(int)good];
        Fix64 durability = ToolDurability(settlement, good);
        if (quantity <= Fix64.Zero || durability <= Fix64.Zero) return Fix64.One;
        Fix64 activeQuantity = Fix64.Min(Fix64.One, quantity);
        Fix64 capacity = activeQuantity * durability;
        Fix64 untouched = (quantity - activeQuantity) * durability;
        Fix64 remaining = Fix64.Clamp(settlement.ToolRemainingWork[(int)good] - untouched,
            Fix64.Zero, capacity);
        return Fix64.Half + Fix64.Parse("0.7") * remaining / capacity;
    }

    private Fix64 AverageMethodToolQuality(SettlementState settlement, string action, int rung, Fix64 work)
    {
        Good primary = ToolForMethod(action, rung);
        if (primary == Good.Count) return Fix64.One;
        Span<Good> tools = stackalloc Good[2];
        tools[0] = primary;
        int toolCount = 1;
        if (action == "fish" && rung == 2) tools[toolCount++] = Good.Net;

        Fix64 available = work;
        Fix64[] quantities = new Fix64[toolCount];
        Fix64[] remaining = new Fix64[toolCount];
        Fix64[] durability = new Fix64[toolCount];
        Fix64[] active = new Fix64[toolCount];
        for (int i = 0; i < toolCount; i++)
        {
            Good good = tools[i];
            quantities[i] = settlement.Stores[(int)good];
            remaining[i] = settlement.ToolRemainingWork[(int)good];
            durability[i] = ToolDurability(settlement, good);
            available = Fix64.Min(available, AvailableToolWork(settlement, good));
        }
        if (available <= Fix64.Zero) return Fix64.One;

        Fix64 qualityWork = Fix64.Zero;
        Fix64 workRemaining = available;
        while (workRemaining > Fix64.Zero)
        {
            Fix64 segment = workRemaining;
            for (int i = 0; i < toolCount; i++)
            {
                active[i] = ActiveToolRemaining(quantities[i], remaining[i], durability[i]);
                while (quantities[i] >= Fix64.One && active[i] <= Fix64.Zero)
                {
                    quantities[i] -= Fix64.One;
                    active[i] = ActiveToolRemaining(quantities[i], remaining[i], durability[i]);
                }
                if (active[i] <= Fix64.Zero) return Fix64.One;
                segment = Fix64.Min(segment, active[i]);
            }

            Fix64 startA = QualityFromRemaining(active[0], durability[0]);
            Fix64 endA = QualityFromRemaining(active[0] - segment, durability[0]);
            Fix64 start = startA;
            Fix64 end = endA;
            if (toolCount == 2)
            {
                Fix64 startB = QualityFromRemaining(active[1], durability[1]);
                Fix64 endB = QualityFromRemaining(active[1] - segment, durability[1]);
                start = startA <= startB ? startA : startB;
                end = endA <= endB ? endA : endB;
                Fix64 differenceStart = startA - startB;
                Fix64 differenceEnd = endA - endB;
                if ((differenceStart < Fix64.Zero && differenceEnd > Fix64.Zero) ||
                    (differenceStart > Fix64.Zero && differenceEnd < Fix64.Zero))
                {
                    Fix64 crossingFraction = Fix64.Abs(differenceStart) /
                        (Fix64.Abs(differenceStart) + Fix64.Abs(differenceEnd));
                    Fix64 crossing = startA + (endA - startA) * crossingFraction;
                    Fix64 firstLower = differenceStart < Fix64.Zero ? startA : startB;
                    Fix64 secondLower = differenceEnd < Fix64.Zero ? endA : endB;
                    qualityWork += segment * crossingFraction * (firstLower + crossing) / Fix64.FromInt(2);
                    qualityWork += segment * (Fix64.One - crossingFraction) * (crossing + secondLower) / Fix64.FromInt(2);
                }
                else qualityWork += segment * (start + end) / Fix64.FromInt(2);
            }
            else qualityWork += segment * (start + end) / Fix64.FromInt(2);

            workRemaining -= segment;
            for (int i = 0; i < toolCount; i++)
            {
                remaining[i] -= segment;
                active[i] -= segment;
                if (active[i] <= Fix64.Zero) quantities[i] -= Fix64.One;
            }
        }
        return qualityWork / available;
    }

    private static Fix64 ActiveToolRemaining(Fix64 quantity, Fix64 remainingWork, Fix64 durability)
    {
        if (quantity < Fix64.One || durability <= Fix64.Zero) return Fix64.Zero;
        Fix64 untouched = (quantity - Fix64.One) * durability;
        return Fix64.Clamp(remainingWork - untouched, Fix64.Zero, durability);
    }

    private static Fix64 QualityFromRemaining(Fix64 remainingWork, Fix64 durability)
        => Fix64.Half + Fix64.Parse("0.7") * Fix64.Clamp(remainingWork, Fix64.Zero, durability) / durability;

    private void WearTool(SettlementState settlement, Good good, Fix64 work)
    {
        Fix64 durability = ToolDurability(settlement, good);
        if (durability <= Fix64.Zero || work <= Fix64.Zero) return;
        int index = (int)good;
        while (work > Fix64.Zero && settlement.Stores[index] >= Fix64.One)
        {
            Fix64 quantity = settlement.Stores[index];
            Fix64 activeQuantity = Fix64.Min(Fix64.One, quantity);
            Fix64 untouched = (quantity - activeQuantity) * durability;
            Fix64 remaining = Fix64.Clamp(settlement.ToolRemainingWork[index] - untouched,
                Fix64.Zero, activeQuantity * durability);
            Fix64 used = Fix64.Min(work, remaining);
            settlement.ToolRemainingWork[index] -= used;
            work -= used;
            if (remaining > used) break;
            settlement.Stores[index] -= activeQuantity;
            settlement.ToolRemainingWork[index] = Fix64.Max(Fix64.Zero, settlement.ToolRemainingWork[index]);
        }
    }

    private void ReplaceWornTools(SettlementState settlement)
    {
        for (int i = 0; i < (int)Good.Count; i++)
        {
            Good good = (Good)i;
            Fix64 durability = ToolDurability(settlement, good);
            if (durability <= Fix64.Zero || settlement.Stores[i] < Fix64.FromInt(2) ||
                ToolQuality(settlement, good) > Fix64.Parse("0.7")) continue;
            Fix64 untouched = (settlement.Stores[i] - Fix64.One) * durability;
            Fix64 wornRemaining = Fix64.Max(Fix64.Zero, settlement.ToolRemainingWork[i] - untouched);
            settlement.Stores[i] -= Fix64.One;
            settlement.ToolRemainingWork[i] -= wornRemaining;
        }
    }

    private static Good ToolForMethod(string action, int rung) => action switch
    {
        "forage" when rung == 1 => Good.Stick,
        "forage" when rung == 2 => Good.Basket,
        "forage" when rung == 3 => Good.Sickle,
        "scavenge" when rung == 1 => Good.Club,
        "hunt" when rung == 1 => Good.Spear,
        "hunt" when rung is 2 or 3 => Good.Tool4,
        "hunt" when rung == 4 => Good.SpearThrower,
        "hunt" when rung == 5 => Good.Bow,
        "fish" when rung == 1 => Good.Harpoon,
        "fish" when rung == 2 => Good.FishingKit,
        "chop" when rung == 1 => Good.Tool1,
        "chop" when rung == 2 => Good.Tool2,
        "chop" when rung == 3 => Good.Tool4,
        "chop" when rung == 4 => Good.Tool7,
        _ => Good.Count,
    };

    private Fix64 MethodToolQuality(SettlementState settlement, string action, int rung)
    {
        Good tool = ToolForMethod(action, rung);
        if (tool == Good.Count) return Fix64.One;
        Fix64 quality = ToolQuality(settlement, tool);
        if (action == "fish" && rung == 2)
            quality = Fix64.Min(quality, ToolQuality(settlement, Good.Net));
        return quality;
    }

    private void WearActionTool(SettlementState settlement, string action, Fix64 actionWork,
        Fix64 rate, int huntPartySize = int.MaxValue)
    {
        if (actionWork <= Fix64.Zero || rate <= Fix64.Zero) return;
        int bestRung = BestActionRung(settlement, action, huntPartySize);
        if (bestRung < 0 && action == "hunt")
        {
            WearActionTool(settlement, "scavenge", actionWork, rate, huntPartySize);
            return;
        }
        Good tool = ToolForMethod(action, bestRung);
        if (tool != Good.Count) WearTool(settlement, tool, actionWork);
        if (action == "fish" && bestRung == 2) WearTool(settlement, Good.Net, actionWork);
    }

    private Fix64 GatherRate(SettlementState settlement, Fix64 baseRate, Fix64 toolWork = default)
        => settlement.Stores[(int)Good.Tool1] >= Fix64.One
            ? baseRate * (toolWork > Fix64.Zero
                ? AverageMethodToolQuality(settlement, "chop", 1, toolWork)
                : ToolQuality(settlement, Good.Tool1)) : baseRate;

    private void WearGatherTool(SettlementState settlement, Fix64 work)
    {
        if (work > Fix64.Zero && settlement.Stores[(int)Good.Tool1] >= Fix64.One)
            WearTool(settlement, Good.Tool1, work);
    }

    private bool HasRequiredTool(SettlementState settlement, string action, int rung)
    {
        // Chop rungs 5 and 6 (copper and bronze axes) are per-figure kits, never a settlement tool (docs/MVP/23 §1.3, §6.2).
        if (action == "chop" && rung >= 5) return false;
        if (action == "fish" && rung == 2)
            return HasGood(settlement, "G-FISHKIT") && HasGood(settlement, "G-NET");
        string? good = action switch
        {
            "forage" when rung == 1 => "G-STICK",
            "forage" when rung == 2 => "G-BASKET",
            "forage" when rung == 3 => "G-SICKLE",
            "scavenge" when rung == 1 => "G-CLUB",
            "hunt" when rung == 1 => "G-SPEAR",
            "hunt" when rung is 2 or 3 => "G-TOOL4",
            "hunt" when rung == 4 => "G-ATLATL",
            "hunt" when rung == 5 => "G-BOW",
            "fish" when rung == 1 => "G-HARPOON",
            "chop" when rung == 1 => "G-TOOL1",
            "chop" when rung == 2 => "G-TOOL2",
            "chop" when rung == 3 => "G-TOOL4",
            "chop" when rung == 4 => "G-TOOL7",
            _ => null,
        };
        if (good is null) return true;
        return HasGood(settlement, good);
    }

    private bool HasGood(SettlementState settlement, string id)
    {
        int index = GoodIndex(id);
        if (index < 0) return false;
        Fix64 minimum = ToolDurability(settlement, (Good)index) > Fix64.Zero ? Fix64.One : Fix64.Zero;
        return minimum > Fix64.Zero ? settlement.Stores[index] >= minimum : settlement.Stores[index] > Fix64.Zero;
    }

    private bool CanFish(SettlementState settlement)
        => HasProject(settlement.Civilization, "HNT-07") &&
           HasWaterAtSettlement(settlement) &&
           (HasRequiredTool(settlement, "fish", 1) ||
            HasProject(settlement.Civilization, "HNT-08") && HasRequiredTool(settlement, "fish", 2));

    private bool HasWaterAtSettlement(SettlementState settlement)
    {
        foreach (int tile in RangeTiles(settlement.SiteTile))
        {
            if (HexGrid.Distance(settlement.SiteTile, tile, _world.Width) > 1) continue;
            WorldEconomyTile input = _world.Tile(tile);
            if (input.River || input.Lake || input.Coast || input.FreshWater) return true;
        }
        return false;
    }

    private bool CanFishTile(SettlementState settlement, int tile)
    {
        if (!CanFish(settlement) || _world.Tile(tile).FishGrowthPerYear <= Fix64.Zero) return false;
        WorldEconomyTile input = _world.Tile(tile);
        int distance = HexGrid.Distance(settlement.SiteTile, tile, _world.Width);
        if (input.Lake) return HasProject(settlement.Civilization, "SEA-02") && distance <= 2;
        if (distance <= 1) return true;
        return distance == 2 && input.Coast && HasProject(settlement.Civilization, "SEA-03");
    }

    private Fix64 FishTileRate(SettlementState settlement, int tile, Fix64 baseRate)
        => CanFishTile(settlement, tile) && HexGrid.Distance(settlement.SiteTile, tile, _world.Width) > 1
            ? baseRate * Fix64.Parse("1.5")
            : baseRate;

    private bool HasAccessibleFishInRange(SettlementState settlement)
    {
        foreach (int tile in RangeTiles(settlement.SiteTile))
            if (_fishStock[tile] > Fix64.Zero && CanFishTile(settlement, tile)) return true;
        return false;
    }

    private static bool IsLandBiome(int biomeId)
        => biomeId != (int)Biome.Ocean && biomeId != (int)Biome.Coast;

    // Modifier(civ, stat) multiplies every completed project's effect on a stat. It is asked for thousands of times a
    // season and only changes when a project completes, so each civilization keeps the answers it has worked out.
    private string?[][]? _modifierKeys;
    private Fix64[][]? _modifierValues;
    private int[]? _modifierCounts;
    // Hazard modifiers are keyed by the fixed set of causes used in seasonal mortality.
    // Injury has a separate adult-exposure slot because hunter effects apply only there.
    private Fix64[][]? _causeModifierValues;
    private bool[][]? _causeModifierValid;

    private static int CauseModifierSlot(string cause, bool adultExposure)
    {
        int causeSlot = cause switch
        {
            "exposure" => 0,
            "injury" => 1,
            "maternal" => 2,
            "endemic" => 3,
            "epidemic" => 4,
            "starvation" => 5,
            "predators" => 6,
            "violence" => 7,
            "child" => 8,
            "age0" => 9,
            _ => -1,
        };
        return causeSlot < 0 ? -1 : causeSlot * 2 + (adultExposure ? 1 : 0);
    }

    private void ClearModifierCache(int civ)
    {
        if (_modifierCounts is not null) _modifierCounts[civ] = 0;
        if (_methodMultCache is not null) _methodMultCache[civ] = null;
        if (_causeModifierValid is not null && _causeModifierValid[civ] is { } causeValid) Array.Clear(causeValid);
    }

    private Fix64 Modifier(int civ, string stat)
    {
        _modifierKeys ??= new string?[_completedProjects.Length][];
        _modifierValues ??= new Fix64[_completedProjects.Length][];
        _modifierCounts ??= new int[_completedProjects.Length];
        string?[] keys = _modifierKeys[civ] ??= new string?[64];
        Fix64[] values = _modifierValues[civ] ??= new Fix64[64];
        int count = _modifierCounts[civ];
        int low = 0, high = count - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            int order = string.CompareOrdinal(keys[mid], stat);
            if (order == 0) return values[mid];
            if (order < 0) low = mid + 1; else high = mid - 1;
        }
        Fix64 computed = ComputeModifier(civ, stat);
        if (count == keys.Length)
        {
            Array.Resize(ref keys, count * 2);
            Array.Resize(ref values, count * 2);
            _modifierKeys[civ] = keys;
            _modifierValues[civ] = values;
        }
        for (int i = count; i > low; i--) { keys[i] = keys[i - 1]; values[i] = values[i - 1]; }
        keys[low] = stat;
        values[low] = computed;
        _modifierCounts[civ] = count + 1;
        return computed;
    }

    private Fix64 ComputeModifier(int civ, string stat)
    {
        Fix64 multiplier = Fix64.One;
        for (int i = 0; i < _content.Effects.Length; i++)
        {
            EconomyEffect effect = _content.Effects[i];
            if (effect.Kind != "mod" || effect.Stat != stat || effect.Value is null || !_completedProjects[civ][effect.ProjectIndex]) continue;
            Fix64 value = effect.Value.Value;
            multiplier *= effect.Operation switch
            {
                "pct" => Fix64.One + value,
                "mult" or "set" => value,
                _ => Fix64.One,
            };
        }
        return Fix64.Max(Fix64.Zero, multiplier);
    }

    private Fix64 SustainableHuntLimit(SettlementState settlement)
    {
        Fix64 annual = Fix64.Zero;
        foreach (int tile in RangeTiles(settlement.SiteTile)) annual += HerdFoodGrowthPerYear(tile);
        Fix64 share = HuntShare(settlement.Civilization);
        return annual * share / 4;
    }

    private Fix64 SustainableHuntLimit(WildHerdState herd, int civ)
    {
        Fix64 annualRate = Fix64.FromRatio(_world.Herd(herd.Id).AnnualGrowthPercent, 100);
        if (_wildfireSeasonsRemaining[herd.CurrentTile] > 0) annualRate *= Fix64.Parse("1.3");
        return herd.CurrentAnimals * annualRate * MeatPerAnimal(herd.Species) * HuntShare(civ) / 4;
    }

    private Fix64 HuntShare(int civ)
    {
        Fix64 share = HasProject(civ, "HNT-04") ? Fix64.Parse("0.48") :
            HasProject(civ, "HNT-03") ? Fix64.Parse("0.44") :
            HasProject(civ, "HNT-02") ? Fix64.Parse("0.42") : Fix64.FromRatio(2, 5);
        if (HasProject(civ, "HUS-01")) share *= Fix64.Parse("1.1");
        return Fix64.Min(Fix64.One, share);
    }

    private bool HasResourceInRange(SettlementState settlement, Fix64[] stock)
    {
        foreach (int tile in RangeTiles(settlement.SiteTile)) if (stock[tile] > Fix64.Zero) return true;
        return false;
    }

    private Fix64 WeirUpkeepBudget(SettlementState settlement, Fix64 totalWork)
        => CanUseWeir(settlement)
            ? Fix64.Min(totalWork, Fix64.FromInt(UsableWeirCount(settlement)) * Fix64.Parse("0.1"))
            : Fix64.Zero;

    private bool CanUseWeir(SettlementState settlement)
        => HasProject(settlement.Civilization, "HNT-09") && UsableWeirCount(settlement) > 0;

    private int UsableWeirCount(SettlementState settlement)
    {
        if (!HasProject(settlement.Civilization, "HNT-09")) return 0;
        return CountOwnedWeirs(settlement);
    }

    private bool HasUnownedRiverTileInRange(SettlementState settlement)
    {
        foreach (int tile in RangeTiles(settlement.SiteTile))
            if (_weirOwner[tile] < 0 && _world.Tile(tile).River &&
                _world.Tile(tile).FishGrowthPerYear > Fix64.Zero) return true;
        return false;
    }

    private bool CanPlaceWeir(SettlementState settlement)
    {
        if (!HasUnownedRiverTileInRange(settlement)) return false;
        // Rebuilds always assign tiles in settlement-id order. Check the resulting
        // global assignment before accepting a new weir, so no existing structure
        // loses its river tile when settlement ranges overlap.
        settlement.Structures[(int)Structure.Weir]++;
        RebuildWeirOwnership();
        bool allPlaced = true;
        foreach (SettlementState existing in _settlements)
            if (CountOwnedWeirs(existing) != existing.Structures[(int)Structure.Weir])
            { allPlaced = false; break; }
        settlement.Structures[(int)Structure.Weir]--;
        RebuildWeirOwnership();
        return allPlaced;
    }

    private int CountOwnedWeirs(SettlementState settlement)
    {
        int count = 0;
        foreach (int tile in RangeTiles(settlement.SiteTile))
            if (_weirOwner[tile] == settlement.Id) count++;
        return count;
    }

    private void RebuildWeirOwnership()
    {
        Array.Fill(_weirOwner, -1);
        foreach (SettlementState settlement in _settlements)
        {
            int remaining = settlement.Structures[(int)Structure.Weir];
            if (remaining <= 0) continue;
            foreach (int tile in RangeTiles(settlement.SiteTile))
            {
                WorldEconomyTile input = _world.Tile(tile);
                if (_weirOwner[tile] >= 0 || !input.River || input.FishGrowthPerYear <= Fix64.Zero) continue;
                _weirOwner[tile] = settlement.Id;
                if (--remaining == 0) break;
            }
        }
    }

    private bool IsServicedWeirTile(SettlementState settlement, int targetTile)
        => _weirOwner[targetTile] == settlement.Id;

    private static Fix64 OrchardUpkeepBudget(SettlementState settlement, Fix64 totalWork)
        => Fix64.Min(totalWork, Fix64.FromInt(settlement.OrchardPlots.Count) * Fix64.FromRatio(1, 40));

    private Fix64 WeirCatch(SettlementState settlement, Fix64 upkeepWork, int season, bool forecast)
    {
        int weirs = UsableWeirCount(settlement);
        if (weirs == 0 || upkeepWork <= Fix64.Zero) return Fix64.Zero;
        Fix64 upkeepShare = Fix64.Min(Fix64.One, upkeepWork / (Fix64.FromInt(weirs) * Fix64.Parse("0.1")));
        Fix64 caught = Fix64.Zero;
        foreach (int tile in RangeTiles(settlement.SiteTile))
        {
            WorldEconomyTile input = _world.Tile(tile);
            if (_weirOwner[tile] != settlement.Id) continue;
            Fix64 target = Fix64.FromInt(2) * upkeepShare;
            if (season == 2 && input.SalmonRun) target *= Fix64.FromInt(3);
            Fix64 available = forecast
                ? Fix64.Min(_fishStock[tile] + SeasonalFishRegrowth(tile, season), input.InitialFish + input.TotalFishGrowthPerYear)
                : _fishStock[tile];
            Fix64 take = Fix64.Min(target, available);
            if (!forecast) _fishStock[tile] -= take;
            caught += take;
            if (--weirs == 0) break;
        }
        if (!forecast) settlement.Stores[(int)Good.Fish] += caught;
        return caught;
    }

    private ReadOnlySpan<int> RangeTiles(int center) => _rangeTiles[center];

    private bool IsWithinRange(int center, int target)
    {
        foreach (int tile in RangeTiles(center))
            if (tile == target) return true;
        return false;
    }

    private static int[] BuildRiverComponents(WorldEconomyInput world)
    {
        int[] componentByTile = new int[world.TileCount];
        Array.Fill(componentByTile, -1);
        var queue = new Queue<int>();
        Span<int> neighbors = stackalloc int[6];
        for (int tile = 0; tile < world.TileCount; tile++)
        {
            if (!world.Tile(tile).River || componentByTile[tile] >= 0) continue;
            int component = tile; // ascending scan makes this the stable minimum tile id
            componentByTile[tile] = component;
            queue.Enqueue(tile);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int count = HexGrid.Neighbors(current, world.Width, world.Height, neighbors);
                for (int i = 0; i < count; i++)
                {
                    int next = neighbors[i];
                    if (!world.Tile(next).River || componentByTile[next] >= 0) continue;
                    componentByTile[next] = component;
                    queue.Enqueue(next);
                }
            }
        }
        return componentByTile;
    }

    private static int[][] BuildRangeTiles(WorldEconomyInput world, int radius)
    {
        var ranges = new int[world.TileCount][];
        var visited = new int[world.TileCount];
        int[] queue = new int[1 + 3 * radius * (radius + 1)];
        Span<int> neighbors = stackalloc int[6];
        for (int center = 0; center < world.TileCount; center++)
        {
            int marker = center + 1;
            int head = 0, tail = 1;
            queue[0] = center;
            visited[center] = marker;
            while (head < tail)
            {
                int tile = queue[head++];
                if (HexGrid.Distance(center, tile, world.Width) == radius) continue;
                int count = HexGrid.Neighbors(tile, world.Width, world.Height, neighbors);
                for (int i = 0; i < count; i++)
                {
                    int next = neighbors[i];
                    if (visited[next] == marker) continue;
                    visited[next] = marker;
                    queue[tail++] = next;
                }
            }
            Array.Sort(queue, 0, tail);
            ranges[center] = new int[tail];
            Array.Copy(queue, ranges[center], tail);
        }
        return ranges;
    }

    private Fix64 WorkCapacity(FigureState figure)
    {
        // A levy host stands and does no work (docs/MVP/22 §4.3).
        if (_idle.Count > 0 && IsIdle(figure.Id)) return Fix64.Zero;
        Fix64 work = BaseWorkCapacity(figure);
        // From KEY-3 Cohesion sets the pace: ×(1 + 0.15 (C − 65) ÷ 35), 0.85–1.15 (docs/MVP/22 §8.3); exactly 1 before it.
        if ((uint)figure.SettlementId < (uint)_settlements.Count && _settlements[figure.SettlementId].Civic is { } c && c.WorkMult != Fix64.One)
            work *= c.WorkMult;
        return work;
    }

    private static Fix64 BaseWorkCapacity(FigureState figure)
    {
        if (figure.Age < 10) return Fix64.Zero;
        if (figure.Age < 14) return Fix64.Parse("0.3") * figure.Health;
        if (figure.Age >= 45) return Fix64.Half * figure.Health;
        return figure.Health;
    }

    private static void Assign(FigureState figure, EconomyJob job, Fix64 work)
    {
        if (figure.Job == EconomyJob.None) figure.Job = job;
        figure.WorkThisSeason += work;
    }

    private static string GoodId(Good good) => good switch
    {
        Good.Plant => "G-PLANT", Good.Meat => "G-MEAT", Good.Roots => "G-ROOT", Good.Nuts => "G-NUTS",
        Good.WildGrain => "G-WGRAIN", Good.Grain => "G-GRAIN", Good.Fish => "G-FISH", Good.DriedFood => "G-DRIED",
        Good.Wood => "G-WOOD", Good.Stone => "G-STONE", Good.Flint => "G-FLINT", Good.Clay => "G-CLAY", Good.Fiber => "G-FIBER",
        Good.Hide => "G-HIDE", Good.Bone => "G-BONE", Good.Cord => "G-CORD", Good.Leather => "G-LEATHER",
        Good.Wrap => "G-WRAP", Good.Tailored => "G-TAILORED",
        Good.ClothGarment => "G-GARMENT",
        Good.Fruit => "G-FRUIT", Good.Straw => "G-STRAW", Good.Sapling => "G-SAPLING", Good.Mudbrick => "G-BRICK",
        Good.Stick => "G-STICK", Good.Club => "G-CLUB", Good.Spear => "G-SPEAR", Good.Basket => "G-BASKET",
        Good.Sickle => "G-SICKLE", Good.Tool1 => "G-TOOL1", Good.Tool2 => "G-TOOL2", Good.Tool3 => "G-TOOL3",
        Good.Tool4 => "G-TOOL4", Good.Tool5 => "G-TOOL5", Good.Tool6 => "G-TOOL6", Good.Tool7 => "G-TOOL7",
        Good.SpearThrower => "G-ATLATL", Good.Bow => "G-BOW", Good.Harpoon => "G-HARPOON",
        Good.FishingKit => "G-FISHKIT", Good.Net => "G-NET",
        _ => "",
    };

    private int GoodIndex(string id)
    {
        for (int i = 0; i < (int)Good.Count; i++) if (GoodId((Good)i) == id) return i;
        return -1;
    }

    public ulong ComputeChecksum()
    {
        var hash = new StateHasher();
        if (_gentleStart) hash.Add(0x47454E544C455354UL); // Keep the legacy default-false checksum stream unchanged.
        if (_holdSettlements) hash.Add(0x484F4C4446495854UL); // fixtures only
        if (_holdPopulation) hash.Add(0x484F4C44504F5055UL); // fixtures only
        hash.Add(_seed); hash.Add(_tick); hash.Add(_seasonsCompleted); hash.Add(_nextFigureId);
        hash.Add(_world.Fingerprint); hash.Add(_contentFingerprint); hash.Add(_lastWinterWasHarsh); hash.Add(_winterForageMultiplier);
        hash.Add(_archetypes.Length); foreach (EconomyArchetype archetype in _archetypes) hash.Add((byte)archetype);
        hash.Add(_forageStock); hash.Add(_herdFoodStock); hash.Add(_woodStock); hash.Add(_fishStock);
        hash.Add(_stoneStock); hash.Add(_clayStock); hash.Add(_flintStock); hash.Add(_soil);
        foreach (byte seasons in _wildfireSeasonsRemaining) hash.Add(seasons);
        hash.Add(_wildHerds.Length);
        foreach (WildHerdState herd in _wildHerds)
        {
            hash.Add(herd.Id); hash.Add((byte)herd.Species); hash.Add(herd.HomeTile); hash.Add(herd.CurrentTile);
            hash.Add(herd.CurrentAnimals); hash.Add(herd.Fear); hash.Add(herd.AwaySeasons);
            hash.Add(herd.LastHuntCrashCivilization);
            hash.Add(herd.TamingYear); hash.Add(herd.TamingLimitThisYear); hash.Add(herd.TamedThisYear);
        }
        foreach (int owner in _fieldOwner) hash.Add(owner);
        foreach (bool pending in _tendedStandYieldBonus) hash.Add(pending);
        for (int civ = 0; civ < _completedProjects.Length; civ++)
            for (int i = 0; i < _completedProjects[civ].Length; i++) hash.Add(_completedProjects[civ][i]);
        hash.Add(_settlements.Count);
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState s = _settlements[i];
            hash.Add(s.Id); hash.Add(s.Civilization); hash.Add(s.SiteTile); hash.Add(s.FoodSecurity);
            hash.Add(s.LateWinterForecast); hash.Add(s.LateWinterFoodShare); hash.Add(s.FarmWorkThisYear);
            // Keep the pre-Bronze checksum stream stable. Fortification slots extend the structure
            // array, and the appended enum members move Count (the old no-build sentinel).
            // They are unreachable until KEY-3, so hash them only once the Bronze simulation is live.
            byte buildTarget = !_bronzeActive && s.BuildTarget == Structure.Count
                ? (byte)Structure.Hillfort : (byte)s.BuildTarget;
            hash.Add(s.BuildProgress); hash.Add(s.DryingMudbrick); hash.Add(buildTarget); hash.Add(s.Fissions); hash.Add(s.LastFissionSeason);
            hash.Add(s.Cohesion); hash.Add(s.Stores);
            int hashedStructures = _bronzeActive ? s.Structures.Length : (int)Structure.Hillfort;
            for (int structure = 0; structure < hashedStructures; structure++) hash.Add(s.Structures[structure]);
            if (!_bronzeActive && s.Structures[(int)Structure.Palisade] > 0)
            { hash.Add((int)Structure.Palisade); hash.Add(s.Structures[(int)Structure.Palisade]); }
            hash.Add(s.DomesticSmallStock); hash.Add(s.DomesticCattle); hash.Add(s.DomesticPigs);
            hash.Add(s.ToolRemainingWork);
            hash.Add(s.FissionPending); hash.Add(s.FireLit); hash.Add(s.FireKeeperId); hash.Add(s.UnattendedFireTicks);
            if (_bronzeActive) hash.Add(s.UnderInvestment);
            hash.Add(s.FieldTiles.Count); foreach (int tile in s.FieldTiles) hash.Add(tile);
            hash.Add(s.OrchardPlots.Count);
            foreach (OrchardPlot orchard in s.OrchardPlots) { hash.Add(orchard.Tile); hash.Add(orchard.AgeSeasons); }
        }
        bool hasScoutPolicyOverride = false;
        foreach (SettlementState settlement in _settlements)
            if (settlement.ScoutPolicy != ScoutPolicy.Auto) { hasScoutPolicyOverride = true; break; }
        if (hasScoutPolicyOverride)
        {
            hash.Add(0x53434F5554504F4CUL); // "SCOUTPOL"
            foreach (SettlementState settlement in _settlements) hash.Add((byte)settlement.ScoutPolicy);
        }
        hash.Add(_figures.Count);
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState f = _figures[i];
            hash.Add(f.Id); hash.Add(f.Civilization); hash.Add(f.SettlementId); hash.Add(f.Age); hash.Add(f.Female);
            hash.Add(f.Health); hash.Add(f.Hunger); hash.Add(f.Warmth); hash.Add(f.Rest); hash.Add(f.Belonging);
            hash.Add(f.PregnancySeasons); hash.Add((byte)f.Job); hash.Add(f.WorkThisSeason);
            hash.Add(f.HuntWorkThisSeason); hash.Add(f.HuntingInjuryHazard);
            hash.Add(f.IllnessSeasons); hash.Add(f.WoundSeasons); hash.Add(f.DiseaseTreated);
            hash.Add(f.WoundTreated); hash.Add(f.MidwifeAttended); hash.Add(f.CareRolesThisSeason);
            hash.Add(f.CareWorkThisSeason); hash.Add(f.FireWorkThisSeason);
            hash.Add(f.SkillLevels.Length); foreach (byte level in f.SkillLevels) hash.Add(level);
            hash.Add(f.SkillPractice.Length); foreach (Fix64 practice in f.SkillPractice) hash.Add(practice);
        }
        foreach (int deaths in _lastDeathsByCause) hash.Add(deaths);
        hash.Add(_lastDeathsByCauseAndAgeBand);
        hash.Add(_lastExpectedHazardsByCauseAndAgeBand);
        hash.Add(_lastExpectedHazardsByExactAgeAndCause);
        hash.Add(_lastExpectedWinterShortfallByExactAge);
        HashBronze(ref hash);
        HashDecrees(ref hash);
        return hash.Finish();
    }

    public void WriteSnapshot(BinaryWriter writer)
    {
        writer.Write(0x45434F4E); writer.Write(SnapshotVersion); writer.Write(_seed);
        writer.Write(_world.Fingerprint); writer.Write(_contentFingerprint);
        writer.Write(_gentleStart);
        writer.Write(_archetypes.Length); foreach (EconomyArchetype archetype in _archetypes) writer.Write((byte)archetype);
        writer.Write(_tick);
        writer.Write(_seasonsCompleted); writer.Write(_nextFigureId); writer.Write(_lastWinterWasHarsh); writer.Write(_winterForageMultiplier.Raw);
        WriteFixArray(writer, _forageStock); WriteFixArray(writer, _herdFoodStock); WriteFixArray(writer, _woodStock);
        WriteFixArray(writer, _fishStock); WriteFixArray(writer, _stoneStock); WriteFixArray(writer, _clayStock);
        WriteFixArray(writer, _flintStock); WriteFixArray(writer, _soil);
        writer.Write(_wildfireSeasonsRemaining.Length);
        foreach (byte seasons in _wildfireSeasonsRemaining) writer.Write(seasons);
        writer.Write(_wildHerds.Length);
        foreach (WildHerdState herd in _wildHerds)
        {
            writer.Write(herd.Id); writer.Write((byte)herd.Species); writer.Write(herd.HomeTile); writer.Write(herd.CurrentTile);
            writer.Write(herd.CurrentAnimals.Raw); writer.Write(herd.Fear.Raw); writer.Write(herd.AwaySeasons);
            writer.Write(herd.LastHuntCrashCivilization);
            writer.Write(herd.TamingYear); writer.Write(herd.TamingLimitThisYear.Raw); writer.Write(herd.TamedThisYear.Raw);
        }
        writer.Write(_fieldOwner.Length); foreach (int owner in _fieldOwner) writer.Write(owner);
        writer.Write(_tendedStandYieldBonus.Length);
        foreach (bool pending in _tendedStandYieldBonus) writer.Write(pending);
        writer.Write(_completedProjects.Length);
        for (int civ = 0; civ < _completedProjects.Length; civ++)
        {
            writer.Write(_completedProjects[civ].Length);
            foreach (bool completed in _completedProjects[civ]) writer.Write(completed);
        }
        writer.Write(_settlements.Count);
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState s = _settlements[i];
            writer.Write(s.Id); writer.Write(s.Civilization); writer.Write(s.SiteTile); WriteFixArray(writer, s.Stores);
            WriteFixArray(writer, s.ToolRemainingWork);
            foreach (int count in s.Structures) writer.Write(count);
            writer.Write(s.FieldTiles.Count); foreach (int tile in s.FieldTiles) writer.Write(tile);
            writer.Write(s.OrchardPlots.Count);
            foreach (OrchardPlot orchard in s.OrchardPlots) { writer.Write(orchard.Tile); writer.Write(orchard.AgeSeasons); }
            writer.Write(s.FoodSecurity.Raw); writer.Write(s.LastSeasonFoodStoresBeforeConsumption.Raw);
            writer.Write(s.LateWinterForecast.Raw); writer.Write(s.LateWinterFoodShare.Raw);
            writer.Write(s.FarmWorkThisYear.Raw); writer.Write(s.BuildProgress.Raw); writer.Write(s.DryingMudbrick.Raw); writer.Write((byte)s.BuildTarget);
            writer.Write(s.Fissions); writer.Write(s.LastFissionSeason); writer.Write(s.Cohesion.Raw); writer.Write(s.FissionPending);
            writer.Write(s.DomesticSmallStock.Raw); writer.Write(s.DomesticCattle.Raw); writer.Write(s.DomesticPigs.Raw);
            writer.Write(s.FireLit); writer.Write(s.FireKeeperId); writer.Write(s.UnattendedFireTicks);
            writer.Write(s.UnderInvestment);
            writer.Write((byte)s.ScoutPolicy);
        }
        writer.Write(_figures.Count);
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState f = _figures[i];
            writer.Write(f.Id); writer.Write(f.Civilization); writer.Write(f.SettlementId); writer.Write(f.Age); writer.Write(f.Female);
            writer.Write(f.Health.Raw); writer.Write(f.Hunger.Raw); writer.Write(f.Warmth.Raw); writer.Write(f.Rest.Raw);
            writer.Write(f.Belonging.Raw); writer.Write(f.PregnancySeasons); writer.Write((byte)f.Job); writer.Write(f.WorkThisSeason.Raw);
            writer.Write(f.HuntWorkThisSeason.Raw); writer.Write(f.HuntingInjuryHazard.Raw);
            writer.Write(f.IllnessSeasons); writer.Write(f.WoundSeasons); writer.Write(f.DiseaseTreated);
            writer.Write(f.WoundTreated); writer.Write(f.MidwifeAttended); writer.Write(f.CareRolesThisSeason);
            writer.Write(f.CareWorkThisSeason.Raw); writer.Write(f.FireWorkThisSeason.Raw);
            writer.Write(f.SkillLevels.Length);
            for (int skill = 0; skill < f.SkillLevels.Length; skill++)
            {
                writer.Write(f.SkillLevels[skill]);
                writer.Write(f.SkillPractice[skill].Raw);
            }
        }
        foreach (int deaths in _lastDeathsByCause) writer.Write(deaths);
        foreach (int deaths in _lastDeathsByCauseAndAgeBand) writer.Write(deaths);
        foreach (Fix64 hazard in _lastExpectedHazardsByCauseAndAgeBand) writer.Write(hazard.Raw);
        foreach (Fix64 hazard in _lastExpectedHazardsByExactAgeAndCause) writer.Write(hazard.Raw);
        foreach (Fix64 hazard in _lastExpectedWinterShortfallByExactAge) writer.Write(hazard.Raw);
        // Last season's deaths, which research reads as the disease signal until the next season end: without them a
        // resumed match's Council picks differently for the rest of the season.
        writer.Write(_lastDeathEvents.Count);
        foreach (EconomyDeathEvent d in _lastDeathEvents)
        {
            writer.Write(d.FigureId); writer.Write(d.Civilization); writer.Write(d.SettlementId); writer.Write(d.Age);
            writer.Write(d.Female); writer.Write(d.PregnancySeasons); writer.Write((int)d.Cause);
            writer.Write(d.TotalHazard.Raw); writer.Write(d.EndemicHazard.Raw); writer.Write(d.EpidemicHazard.Raw);
            writer.Write(d.StarvationHazard.Raw); writer.Write(d.ExposureHazard.Raw); writer.Write(d.InjuryHazard.Raw);
            writer.Write(d.PredatorsHazard.Raw); writer.Write(d.ViolenceHazard.Raw); writer.Write(d.SenescenceHazard.Raw);
            writer.Write(d.WinterShortfallHazard.Raw);
        }
        WriteBronze(writer);
    }

    public void ReadSnapshot(BinaryReader reader)
    {
        // Parse and validate off to the side so malformed snapshots cannot leave this model half-mutated.
        var candidate = new LivingEconomyModel(_seed, _content, _world, _archetypes, _gentleStart);
        candidate.ReadSnapshotCore(reader);
        CopySnapshotStateFrom(candidate);
    }

    private void ReadSnapshotCore(BinaryReader reader)
    {
        if (reader.ReadInt32() != 0x45434F4E || reader.ReadInt32() != SnapshotVersion)
            throw new InvalidDataException("Unsupported living-economy snapshot.");
        if (reader.ReadUInt64() != _seed || reader.ReadUInt64() != _world.Fingerprint || reader.ReadUInt64() != _contentFingerprint)
            throw new InvalidDataException("Snapshot seed, world or content does not match this model.");
        if (reader.ReadBoolean() != _gentleStart) throw new InvalidDataException("Snapshot start settings differ.");
        if (reader.ReadInt32() != _archetypes.Length) throw new InvalidDataException("Snapshot archetype count differs.");
        for (int i = 0; i < _archetypes.Length; i++)
            if (reader.ReadByte() != (byte)_archetypes[i]) throw new InvalidDataException("Snapshot archetypes differ.");
        _tick = reader.ReadInt64(); _seasonsCompleted = reader.ReadInt64(); _nextFigureId = reader.ReadInt64();
        _lastWinterWasHarsh = reader.ReadBoolean(); _winterForageMultiplier = Fix64.FromRaw(reader.ReadInt64());
        ReadFixArray(reader, _forageStock); ReadFixArray(reader, _herdFoodStock); ReadFixArray(reader, _woodStock);
        ReadFixArray(reader, _fishStock); ReadFixArray(reader, _stoneStock); ReadFixArray(reader, _clayStock);
        ReadFixArray(reader, _flintStock); ReadFixArray(reader, _soil);
        if (reader.ReadInt32() != _wildfireSeasonsRemaining.Length)
            throw new InvalidDataException("Snapshot wildfire tile count differs.");
        for (int i = 0; i < _wildfireSeasonsRemaining.Length; i++)
        {
            byte seasons = reader.ReadByte();
            if (seasons > 12) throw new InvalidDataException("Snapshot contains an invalid burned-tile recovery timer.");
            _wildfireSeasonsRemaining[i] = seasons;
        }
        if (reader.ReadInt32() != _wildHerds.Length) throw new InvalidDataException("Snapshot wild-herd count differs.");
        foreach (WildHerdState herd in _wildHerds)
        {
            int id = reader.ReadInt32(); HerdSpecies species = (HerdSpecies)reader.ReadByte();
            int home = reader.ReadInt32(); int current = reader.ReadInt32();
            Fix64 animals = Fix64.FromRaw(reader.ReadInt64()); Fix64 fear = Fix64.FromRaw(reader.ReadInt64());
            int away = reader.ReadInt32(); int lastHuntCrashCivilization = reader.ReadInt32();
            int tamingYear = reader.ReadInt32();
            Fix64 tamingLimit = Fix64.FromRaw(reader.ReadInt64()); Fix64 tamed = Fix64.FromRaw(reader.ReadInt64());
            if (id != herd.Id || species != herd.Species || home != herd.HomeTile)
                throw new InvalidDataException("Snapshot wild-herd identity differs from world input.");
            herd.CurrentTile = current; herd.CurrentAnimals = animals; herd.Fear = fear;
            herd.AwaySeasons = away; herd.TamingYear = tamingYear;
            herd.LastHuntCrashCivilization = lastHuntCrashCivilization;
            herd.TamingLimitThisYear = tamingLimit; herd.TamedThisYear = tamed;
        }
        if (reader.ReadInt32() != _fieldOwner.Length) throw new InvalidDataException("Snapshot world tile count differs.");
        for (int i = 0; i < _fieldOwner.Length; i++) _fieldOwner[i] = reader.ReadInt32();
        if (reader.ReadInt32() != _tendedStandYieldBonus.Length)
            throw new InvalidDataException("Snapshot tended-stand tile count differs.");
        for (int i = 0; i < _tendedStandYieldBonus.Length; i++)
            _tendedStandYieldBonus[i] = reader.ReadBoolean();
        if (reader.ReadInt32() != _completedProjects.Length) throw new InvalidDataException("Snapshot civilization count differs.");
        for (int civ = 0; civ < _completedProjects.Length; civ++)
        {
            if (reader.ReadInt32() != _completedProjects[civ].Length) throw new InvalidDataException("Snapshot project count differs.");
            for (int i = 0; i < _completedProjects[civ].Length; i++) _completedProjects[civ][i] = reader.ReadBoolean();
            ClearModifierCache(civ);
            InvalidateReachableForageShare(civ);
        }
        _settlements.Clear();
        int settlementCount = ReadCount(reader, 100_000);
        for (int i = 0; i < settlementCount; i++)
        {
            var s = new SettlementState { Id = reader.ReadInt32(), Civilization = reader.ReadInt32(), SiteTile = reader.ReadInt32() };
            ReadFixArray(reader, s.Stores);
            ReadFixArray(reader, s.ToolRemainingWork);
            for (int j = 0; j < s.Structures.Length; j++) s.Structures[j] = reader.ReadInt32();
            int fields = ReadCount(reader, _world.TileCount);
            for (int j = 0; j < fields; j++) s.FieldTiles.Add(reader.ReadInt32());
            int orchards = ReadCount(reader, _world.TileCount);
            for (int j = 0; j < orchards; j++)
                s.OrchardPlots.Add(new OrchardPlot { Tile = reader.ReadInt32(), AgeSeasons = reader.ReadInt32() });
            s.FoodSecurity = Fix64.FromRaw(reader.ReadInt64());
            s.LastSeasonFoodStoresBeforeConsumption = Fix64.FromRaw(reader.ReadInt64());
            s.LateWinterForecast = Fix64.FromRaw(reader.ReadInt64());
            s.LateWinterFoodShare = Fix64.FromRaw(reader.ReadInt64()); s.FarmWorkThisYear = Fix64.FromRaw(reader.ReadInt64());
            s.BuildProgress = Fix64.FromRaw(reader.ReadInt64()); s.DryingMudbrick = Fix64.FromRaw(reader.ReadInt64());
            s.BuildTarget = (Structure)reader.ReadByte();
            s.Fissions = reader.ReadInt32(); s.LastFissionSeason = reader.ReadInt32(); s.Cohesion = Fix64.FromRaw(reader.ReadInt64());
            s.FissionPending = reader.ReadBoolean();
            s.DomesticSmallStock = Fix64.FromRaw(reader.ReadInt64());
            s.DomesticCattle = Fix64.FromRaw(reader.ReadInt64()); s.DomesticPigs = Fix64.FromRaw(reader.ReadInt64());
            s.FireLit = reader.ReadBoolean(); s.FireKeeperId = reader.ReadInt64(); s.UnattendedFireTicks = reader.ReadByte();
            s.UnderInvestment = reader.ReadBoolean();
            byte scoutPolicy = reader.ReadByte();
            if (scoutPolicy > (byte)ScoutPolicy.Two) throw new InvalidDataException("Snapshot contains an invalid settlement scout policy.");
            s.ScoutPolicy = (ScoutPolicy)scoutPolicy;
            _settlements.Add(s);
        }
        _figures.Clear();
        int figureCount = ReadCount(reader, 2_000_000);
        for (int i = 0; i < figureCount; i++)
        {
            var figure = new FigureState
            {
                Id = reader.ReadInt64(), Civilization = reader.ReadInt32(), SettlementId = reader.ReadInt32(),
                Age = reader.ReadInt32(), Female = reader.ReadBoolean(), Health = Fix64.FromRaw(reader.ReadInt64()),
                Hunger = Fix64.FromRaw(reader.ReadInt64()), Warmth = Fix64.FromRaw(reader.ReadInt64()),
                Rest = Fix64.FromRaw(reader.ReadInt64()), Belonging = Fix64.FromRaw(reader.ReadInt64()),
                PregnancySeasons = reader.ReadByte(), Job = (EconomyJob)reader.ReadByte(),
                WorkThisSeason = Fix64.FromRaw(reader.ReadInt64()),
                HuntWorkThisSeason = Fix64.FromRaw(reader.ReadInt64()), HuntingInjuryHazard = Fix64.FromRaw(reader.ReadInt64()),
                IllnessSeasons = reader.ReadByte(), WoundSeasons = reader.ReadByte(), DiseaseTreated = reader.ReadBoolean(),
                WoundTreated = reader.ReadBoolean(), MidwifeAttended = reader.ReadBoolean(), CareRolesThisSeason = reader.ReadByte(),
                CareWorkThisSeason = Fix64.FromRaw(reader.ReadInt64()), FireWorkThisSeason = Fix64.FromRaw(reader.ReadInt64()),
            };
            int skillCount = ReadCount(reader, (int)SkillFamily.Count);
            if (skillCount != (int)SkillFamily.Count) throw new InvalidDataException("Snapshot action-skill count differs.");
            for (int skill = 0; skill < skillCount; skill++)
            {
                figure.SkillLevels[skill] = reader.ReadByte();
                figure.SkillPractice[skill] = Fix64.FromRaw(reader.ReadInt64());
            }
            _figures.Add(figure);
        }
        for (int i = 0; i < _lastDeathsByCause.Length; i++) _lastDeathsByCause[i] = reader.ReadInt32();
        for (int i = 0; i < _lastDeathsByCauseAndAgeBand.Length; i++)
            _lastDeathsByCauseAndAgeBand[i] = reader.ReadInt32();
        for (int i = 0; i < _lastExpectedHazardsByCauseAndAgeBand.Length; i++)
            _lastExpectedHazardsByCauseAndAgeBand[i] = Fix64.FromRaw(reader.ReadInt64());
        for (int i = 0; i < _lastExpectedHazardsByExactAgeAndCause.Length; i++)
            _lastExpectedHazardsByExactAgeAndCause[i] = Fix64.FromRaw(reader.ReadInt64());
        for (int i = 0; i < _lastExpectedWinterShortfallByExactAge.Length; i++)
            _lastExpectedWinterShortfallByExactAge[i] = Fix64.FromRaw(reader.ReadInt64());
        _lastDeathEvents.Clear();
        int deathEvents = ReadCount(reader, 1_000_000);
        for (int i = 0; i < deathEvents; i++)
        {
            long id = reader.ReadInt64(); int civ = reader.ReadInt32(), settlement = reader.ReadInt32(), age = reader.ReadInt32();
            bool female = reader.ReadBoolean(); byte pregnancy = reader.ReadByte(); var cause = (EconomyDeathCause)reader.ReadInt32();
            Fix64 F() => Fix64.FromRaw(reader.ReadInt64());
            Fix64 total = F(), endemic = F(), epidemic = F(), starvation = F(), exposure = F(), injury = F(), predators = F(),
                violence = F(), senescence = F(), winterShortfall = F();
            _lastDeathEvents.Add(new EconomyDeathEvent(id, civ, settlement, age, female, pregnancy, cause, total, endemic, epidemic,
                starvation, exposure, injury, predators, violence, senescence, winterShortfall));
        }
        ReadBronze(reader);
        RefreshBronzeActive();
        RebuildWeirOwnership();
        ValidateSnapshotState();
        ValidateBronze();
    }

    private void ValidateSnapshotState()
    {
        if (_tick < 0 || _seasonsCompleted < 0 || _seasonsCompleted != _tick / SeasonTicks || _nextFigureId < 0)
            throw new InvalidDataException("Snapshot tick or season counters are inconsistent.");
        if (_settlements.Count < CivilizationCount)
            throw new InvalidDataException("Snapshot omits an initial civilization settlement.");

        ValidateNonnegative(_forageStock); ValidateNonnegative(_herdFoodStock); ValidateNonnegative(_woodStock);
        ValidateNonnegative(_fishStock); ValidateNonnegative(_stoneStock); ValidateNonnegative(_clayStock);
        ValidateNonnegative(_flintStock); ValidateNonnegative(_soil);
        for (int i = 0; i < _wildHerds.Length; i++)
        {
            WildHerdState herd = _wildHerds[i];
            WorldHerd inputHerd = _world.Herd(i);
            Fix64 carryingCapacity = Fix64.FromInt(inputHerd.InitialAnimalCount) *
                (Fix64.One + Fix64.FromRatio(inputHerd.AnnualGrowthPercent, 100) * Fix64.Parse("1.3"));
            if (herd.Id != _world.Herd(i).Id || herd.Species != _world.Herd(i).Species ||
                herd.HomeTile != _world.Herd(i).Tile || herd.CurrentTile < 0 || herd.CurrentTile >= _world.TileCount ||
                herd.CurrentAnimals < Fix64.Zero || herd.CurrentAnimals > carryingCapacity ||
                herd.Fear < Fix64.Zero || herd.Fear > Fix64.FromInt(4) ||
                herd.AwaySeasons is < 0 or > 2 || herd.TamingYear < 0 || herd.TamingYear > _seasonsCompleted / 4 ||
                herd.LastHuntCrashCivilization < -1 || herd.LastHuntCrashCivilization >= CivilizationCount ||
                herd.TamingLimitThisYear < Fix64.Zero || herd.TamedThisYear < Fix64.Zero ||
                herd.TamedThisYear > herd.TamingLimitThisYear)
                throw new InvalidDataException("Snapshot contains invalid wild-herd state.");
        }
        if (_hasIndividualHerds)
        {
            var expectedHerdFood = new Fix64[_world.TileCount];
            foreach (WildHerdState herd in _wildHerds)
                expectedHerdFood[herd.CurrentTile] += herd.CurrentAnimals * MeatPerAnimal(herd.Species);
            for (int tile = 0; tile < _world.TileCount; tile++)
                if (_herdFoodStock[tile] != expectedHerdFood[tile])
                    throw new InvalidDataException("Snapshot herd-food stocks disagree with wild-herd state.");
        }

        var fieldTileOwners = new int[_world.TileCount];
        Array.Fill(fieldTileOwners, -1);
        var orchardTileOwners = new int[_world.TileCount];
        Array.Fill(orchardTileOwners, -1);
        var settlementSites = new bool[_world.TileCount];
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState s = _settlements[i];
            if (s.Id != i || s.Civilization < 0 || s.Civilization >= CivilizationCount ||
                // A founding settlement may belong to another civilization: capitals can be captured (docs/MVP/13 §8.3).
                s.SiteTile < 0 || s.SiteTile >= _world.TileCount || settlementSites[s.SiteTile])
                throw new InvalidDataException("Snapshot contains an invalid settlement identity or reference.");
            settlementSites[s.SiteTile] = true;
            if (s.Fissions < 0 || s.BuildProgress < Fix64.Zero || s.DryingMudbrick < Fix64.Zero || s.FarmWorkThisYear < Fix64.Zero ||
                s.FoodSecurity < Fix64.Zero || s.FoodSecurity > Fix64.One ||
                s.LastSeasonFoodStoresBeforeConsumption < Fix64.Zero ||
                s.LateWinterForecast < Fix64.Zero || s.LateWinterForecast > Fix64.One ||
                s.LateWinterFoodShare < Fix64.Zero || s.LateWinterFoodShare > Fix64.One ||
                s.Cohesion < Fix64.Parse("0.6") || s.Cohesion > Fix64.Parse("1.2") ||
                (uint)s.BuildTarget > (uint)Structure.Count || s.FireKeeperId < -1 || s.UnattendedFireTicks > 10)
                throw new InvalidDataException("Snapshot contains invalid settlement state.");
            if (s.DomesticSmallStock < Fix64.Zero || s.DomesticSmallStock > DomesticSmallStockCapacity(s) ||
                s.DomesticCattle < Fix64.Zero || s.DomesticPigs < Fix64.Zero ||
                s.DomesticCattle + s.DomesticPigs > DomesticLargeAnimalCapacity(s))
                throw new InvalidDataException("Snapshot contains domestic herds beyond pasture capacity.");
            if (s.Structures[(int)Structure.Orchard] != s.OrchardPlots.Count)
                throw new InvalidDataException("Snapshot orchard count differs from its orchard plots.");
            for (int good = 0; good < s.Stores.Length; good++)
                if (s.Stores[good] < Fix64.Zero) throw new InvalidDataException("Snapshot contains a negative store.");
            for (int good = 0; good < s.ToolRemainingWork.Length; good++)
            {
                Fix64 durability = ToolDurability(s, (Good)good);
                if (s.ToolRemainingWork[good] < Fix64.Zero ||
                    s.ToolRemainingWork[good] > s.Stores[good] * durability ||
                    durability == Fix64.Zero && s.ToolRemainingWork[good] != Fix64.Zero)
                    throw new InvalidDataException("Snapshot contains invalid tool durability.");
            }
            for (int structure = 0; structure < s.Structures.Length; structure++)
                if (s.Structures[structure] < 0) throw new InvalidDataException("Snapshot contains a negative structure count.");
            if (s.Structures[(int)Structure.Weir] != CountOwnedWeirs(s))
                throw new InvalidDataException("Snapshot contains a weir without a unique river tile.");
            for (int j = 0; j < s.FieldTiles.Count; j++)
            {
                int tile = s.FieldTiles[j];
                if (tile < 0 || tile >= _world.TileCount || fieldTileOwners[tile] >= 0)
                    throw new InvalidDataException("Snapshot contains an invalid or duplicate field tile.");
                fieldTileOwners[tile] = s.Id;
            }
            for (int j = 0; j < s.OrchardPlots.Count; j++)
            {
                OrchardPlot orchard = s.OrchardPlots[j];
                if (orchard.Tile < 0 || orchard.Tile >= _world.TileCount || orchard.AgeSeasons < 0 || orchard.AgeSeasons > 224 ||
                    orchard.Tile == s.SiteTile || orchardTileOwners[orchard.Tile] >= 0 || !IsOrchardGround(orchard.Tile) ||
                    !IsWithinRange(s.SiteTile, orchard.Tile))
                    throw new InvalidDataException("Snapshot contains an invalid or duplicate orchard plot.");
                orchardTileOwners[orchard.Tile] = s.Id;
            }
        }
        for (int tile = 0; tile < _fieldOwner.Length; tile++)
        {
            if (fieldTileOwners[tile] >= 0 && orchardTileOwners[tile] >= 0)
                throw new InvalidDataException("Snapshot field and orchard plots overlap.");
            if (_fieldOwner[tile] < -1 || _fieldOwner[tile] >= _settlements.Count ||
                _fieldOwner[tile] != fieldTileOwners[tile])
                throw new InvalidDataException("Snapshot field ownership is inconsistent.");
            if (_tendedStandYieldBonus[tile] &&
                (fieldTileOwners[tile] < 0 || !_world.Tile(tile).WildGrainStand ||
                 !HasProject(_settlements[fieldTileOwners[tile]].Civilization, "FOR-06")))
                throw new InvalidDataException("Snapshot has a tended-stand yield bonus without its field and project.");
        }

        long previousFigureId = -1;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState f = _figures[i];
            if (f.Id <= previousFigureId || f.Id < 0 || f.Id >= _nextFigureId || f.Age < 0 || f.Age > 99 ||
                f.Civilization < 0 || f.Civilization >= CivilizationCount || f.SettlementId < 0 || f.SettlementId >= _settlements.Count ||
                _settlements[f.SettlementId].Civilization != f.Civilization || f.PregnancySeasons > 3 ||
                (uint)f.Job > (uint)EconomyJob.Scout || f.Health < Fix64.Zero || f.Health > Fix64.One ||
                f.Hunger < Fix64.Zero || f.Hunger > Fix64.One || f.Warmth < Fix64.Zero || f.Warmth > Fix64.One ||
                f.Rest < Fix64.Zero || f.Rest > Fix64.One || f.Belonging < Fix64.Zero || f.Belonging > Fix64.One ||
                f.WorkThisSeason < Fix64.Zero || f.HuntWorkThisSeason < Fix64.Zero ||
                f.HuntWorkThisSeason > f.WorkThisSeason || f.HuntingInjuryHazard < Fix64.Zero ||
                f.HuntingInjuryHazard > Fix64.Parse("0.03"))
                throw new InvalidDataException("Snapshot contains an invalid figure or settlement reference.");
            if (f.IllnessSeasons > 2 || f.WoundSeasons > 2 || f.CareRolesThisSeason > (HealerRole | MidwifeRole) ||
                // Cohesion paces work up to ×1.15 from KEY-3 (docs/MVP/22 §8.3), so a provider may give up to 1.15 work-seasons.
                f.CareWorkThisSeason < Fix64.Zero || f.CareWorkThisSeason > Fix64.FromRatio(115, 100) ||
                f.CareWorkThisSeason > f.WorkThisSeason ||
                f.FireWorkThisSeason < Fix64.Zero || f.FireWorkThisSeason > FireKeepingWork ||
                f.FireWorkThisSeason > f.WorkThisSeason ||
                ((f.Job == EconomyJob.TendFire) != (f.FireWorkThisSeason > Fix64.Zero)) ||
                ((f.CareRolesThisSeason == 0) != (f.CareWorkThisSeason == Fix64.Zero)) ||
                (f.CareRolesThisSeason != 0 && f.Age < 14) ||
                (f.DiseaseTreated && !HasProject(f.Civilization, "MED-01")) ||
                (f.WoundTreated && !HasProject(f.Civilization, "MED-02")) ||
                ((f.MidwifeAttended || (f.CareRolesThisSeason & MidwifeRole) != 0) && !HasProject(f.Civilization, "MED-03")) ||
                ((f.CareRolesThisSeason & HealerRole) != 0 && !HasProject(f.Civilization, "MED-01") &&
                    !HasProject(f.Civilization, "MED-02")))
                throw new InvalidDataException($"Snapshot contains invalid health or care state (figure {f.Id}: illness {f.IllnessSeasons}, wound {f.WoundSeasons}, care {f.CareRolesThisSeason}/{f.CareWorkThisSeason}, fire {f.FireWorkThisSeason}, job {f.Job}, work {f.WorkThisSeason}).");
            if (f.SkillLevels.Length != (int)SkillFamily.Count || f.SkillPractice.Length != (int)SkillFamily.Count)
                throw new InvalidDataException("Snapshot action-skill state has an invalid length.");
            for (int skill = 0; skill < (int)SkillFamily.Count; skill++)
            {
                int level = f.SkillLevels[skill];
                Fix64 practice = f.SkillPractice[skill];
                Fix64 threshold = level < 6 ? SkillWorkPerLevel : VeteranSkillWorkPerLevel;
                if (level > MaxSkillLevel || practice < Fix64.Zero ||
                    (level == MaxSkillLevel ? practice != Fix64.Zero : practice >= threshold))
                    throw new InvalidDataException("Snapshot contains invalid action-skill progress.");
            }
            previousFigureId = f.Id;
        }
        for (int i = 0; i < _settlements.Count; i++)
        {
            long keeperId = _settlements[i].FireKeeperId;
            if (keeperId < 0) continue;
            bool found = false;
            for (int j = 0; j < _figures.Count; j++)
                if (_figures[j].Id == keeperId && _figures[j].SettlementId == _settlements[i].Id) { found = true; break; }
            if (!found) throw new InvalidDataException("Snapshot firekeeper is missing from its settlement.");
        }
        for (int i = 0; i < _lastDeathsByCause.Length; i++)
            if (_lastDeathsByCause[i] < 0) throw new InvalidDataException("Snapshot contains a negative death count.");
        for (int i = 0; i < _lastDeathsByCauseAndAgeBand.Length; i++)
            if (_lastDeathsByCauseAndAgeBand[i] < 0) throw new InvalidDataException("Snapshot contains a negative age-band death count.");
        ValidateNonnegative(_lastExpectedHazardsByCauseAndAgeBand);
        ValidateNonnegative(_lastExpectedHazardsByExactAgeAndCause);
        ValidateNonnegative(_lastExpectedWinterShortfallByExactAge);
        for (int cause = 0; cause < _lastDeathsByCause.Length; cause++)
        {
            long byAge = 0;
            for (int ageBand = 0; ageBand < 3; ageBand++)
                byAge += _lastDeathsByCauseAndAgeBand[ageBand * _lastDeathsByCause.Length + cause];
            if (byAge != _lastDeathsByCause[cause])
                throw new InvalidDataException("Snapshot death-cause and age-band counts are inconsistent.");
        }
    }

    private void CopySnapshotStateFrom(LivingEconomyModel source)
    {
        _tick = source._tick;
        _seasonsCompleted = source._seasonsCompleted;
        _nextFigureId = source._nextFigureId;
        _lastWinterWasHarsh = source._lastWinterWasHarsh;
        _winterForageMultiplier = source._winterForageMultiplier;
        Array.Copy(source._forageStock, _forageStock, _forageStock.Length);
        Array.Copy(source._herdFoodStock, _herdFoodStock, _herdFoodStock.Length);
        Array.Copy(source._woodStock, _woodStock, _woodStock.Length);
        Array.Copy(source._fishStock, _fishStock, _fishStock.Length);
        Array.Copy(source._stoneStock, _stoneStock, _stoneStock.Length);
        Array.Copy(source._clayStock, _clayStock, _clayStock.Length);
        Array.Copy(source._flintStock, _flintStock, _flintStock.Length);
        Array.Copy(source._soil, _soil, _soil.Length);
        Array.Copy(source._wildfireSeasonsRemaining, _wildfireSeasonsRemaining, _wildfireSeasonsRemaining.Length);
        for (int i = 0; i < _wildHerds.Length; i++) CopyWildHerdState(source._wildHerds[i], _wildHerds[i]);
        if (_hasIndividualHerds) RebuildWildHerdFoodStock();
        Array.Copy(source._fieldOwner, _fieldOwner, _fieldOwner.Length);
        Array.Copy(source._tendedStandYieldBonus, _tendedStandYieldBonus, _tendedStandYieldBonus.Length);
        for (int civ = 0; civ < _completedProjects.Length; civ++)
        {
            Array.Copy(source._completedProjects[civ], _completedProjects[civ], _completedProjects[civ].Length);
            ClearModifierCache(civ);
            InvalidateReachableForageShare(civ);
        }
        _settlements.Clear();
        foreach (SettlementState settlement in source._settlements)
            _settlements.Add(CloneSettlement(settlement));
        RebuildWeirOwnership();
        _figures.Clear();
        foreach (FigureState figure in source._figures) _figures.Add(CloneFigure(figure));
        Array.Copy(source._lastDeathsByCause, _lastDeathsByCause, _lastDeathsByCause.Length);
        Array.Copy(source._lastDeathsByCauseAndAgeBand, _lastDeathsByCauseAndAgeBand, _lastDeathsByCauseAndAgeBand.Length);
        Array.Copy(source._lastExpectedHazardsByCauseAndAgeBand, _lastExpectedHazardsByCauseAndAgeBand,
            _lastExpectedHazardsByCauseAndAgeBand.Length);
        Array.Copy(source._lastExpectedHazardsByExactAgeAndCause, _lastExpectedHazardsByExactAgeAndCause,
            _lastExpectedHazardsByExactAgeAndCause.Length);
        Array.Copy(source._lastExpectedWinterShortfallByExactAge, _lastExpectedWinterShortfallByExactAge,
            _lastExpectedWinterShortfallByExactAge.Length);
        _lastDeathEvents.Clear();
        _lastDeathEvents.AddRange(source._lastDeathEvents);
        CopyBronzeFrom(source);
        CopyDecreesFrom(source);
    }

    private static FigureState CloneFigure(FigureState source) => new()
    {
        Id = source.Id, Civilization = source.Civilization, SettlementId = source.SettlementId,
        Age = source.Age, Female = source.Female, Health = source.Health, Hunger = source.Hunger,
        Warmth = source.Warmth, Rest = source.Rest, Belonging = source.Belonging,
        PregnancySeasons = source.PregnancySeasons, Job = source.Job, WorkThisSeason = source.WorkThisSeason,
        HuntWorkThisSeason = source.HuntWorkThisSeason, HuntingInjuryHazard = source.HuntingInjuryHazard,
        IllnessSeasons = source.IllnessSeasons, WoundSeasons = source.WoundSeasons,
        DiseaseTreated = source.DiseaseTreated, WoundTreated = source.WoundTreated,
        MidwifeAttended = source.MidwifeAttended, CareRolesThisSeason = source.CareRolesThisSeason,
        CareWorkThisSeason = source.CareWorkThisSeason, FireWorkThisSeason = source.FireWorkThisSeason,
        SkillLevels = (byte[])source.SkillLevels.Clone(), SkillPractice = (Fix64[])source.SkillPractice.Clone(),
        Kit = source.Kit, KitLeft = source.KitLeft, Task = source.Task, Camp = source.Camp, Role = source.Role, Infected = source.Infected,
    };

    private static void CopyWildHerdState(WildHerdState source, WildHerdState target)
    {
        target.Id = source.Id; target.Species = source.Species; target.HomeTile = source.HomeTile;
        target.CurrentTile = source.CurrentTile; target.CurrentAnimals = source.CurrentAnimals;
        target.Fear = source.Fear; target.AwaySeasons = source.AwaySeasons;
        target.LastHuntCrashCivilization = source.LastHuntCrashCivilization;
        target.TamingYear = source.TamingYear; target.TamingLimitThisYear = source.TamingLimitThisYear;
        target.TamedThisYear = source.TamedThisYear;
    }

    private static SettlementState CloneSettlement(SettlementState source)
    {
        var copy = new SettlementState
        {
            Id = source.Id,
            Civilization = source.Civilization,
            SiteTile = source.SiteTile,
            FoodSecurity = source.FoodSecurity,
            LastSeasonFoodStoresBeforeConsumption = source.LastSeasonFoodStoresBeforeConsumption,
            LateWinterForecast = source.LateWinterForecast,
            LateWinterFoodShare = source.LateWinterFoodShare,
            FarmWorkThisYear = source.FarmWorkThisYear,
            BuildProgress = source.BuildProgress,
            DryingMudbrick = source.DryingMudbrick,
            BuildTarget = source.BuildTarget,
            Fissions = source.Fissions,
            LastFissionSeason = source.LastFissionSeason,
            Cohesion = source.Cohesion,
            DomesticSmallStock = source.DomesticSmallStock,
            DomesticCattle = source.DomesticCattle,
            DomesticPigs = source.DomesticPigs,
            FissionPending = source.FissionPending,
            FireLit = source.FireLit,
            FireKeeperId = source.FireKeeperId,
            UnattendedFireTicks = source.UnattendedFireTicks,
            UnderInvestment = source.UnderInvestment,
            ScoutPolicy = source.ScoutPolicy,
            Metal = CloneMetal(source.Metal),
            Civic = CloneCivic(source.Civic),
        };
        Array.Copy(source.Stores, copy.Stores, source.Stores.Length);
        Array.Copy(source.ToolRemainingWork, copy.ToolRemainingWork, source.ToolRemainingWork.Length);
        Array.Copy(source.Structures, copy.Structures, source.Structures.Length);
        copy.FieldTiles.AddRange(source.FieldTiles);
        foreach (OrchardPlot orchard in source.OrchardPlots)
            copy.OrchardPlots.Add(new OrchardPlot { Tile = orchard.Tile, AgeSeasons = orchard.AgeSeasons });
        return copy;
    }

    private static void ValidateNonnegative(Fix64[] values)
    {
        for (int i = 0; i < values.Length; i++)
            if (values[i] < Fix64.Zero) throw new InvalidDataException("Snapshot contains a negative resource stock.");
    }

    private static int ReadCount(BinaryReader reader, int maximum)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > maximum) throw new InvalidDataException("Invalid snapshot collection length.");
        return count;
    }

    private static void WriteFixArray(BinaryWriter writer, Fix64[] values)
    {
        writer.Write(values.Length);
        for (int i = 0; i < values.Length; i++) writer.Write(values[i].Raw);
    }

    private static void ReadFixArray(BinaryReader reader, Fix64[] values)
    {
        if (reader.ReadInt32() != values.Length) throw new InvalidDataException("Snapshot array length differs.");
        for (int i = 0; i < values.Length; i++) values[i] = Fix64.FromRaw(reader.ReadInt64());
    }

    private static ulong FingerprintContent(EconomyContent content)
    {
        var hash = new StateHasher();
        hash.Add(content.Projects.Length);
        foreach (EconomyProject project in content.Projects)
        { hash.Add(project.Index); hash.Add(Hash64.OfString(project.Id)); hash.Add(project.Era); hash.Add(project.EnterEra); }
        hash.Add(content.Effects.Length);
        foreach (EconomyEffect effect in content.Effects)
        {
            hash.Add(effect.ProjectIndex); hash.Add(Hash64.OfString(effect.Kind));
            hash.Add(Hash64.OfString(effect.Action ?? "")); hash.Add(Hash64.OfString(effect.Stat ?? ""));
            hash.Add(Hash64.OfString(effect.Operation ?? "")); hash.Add(Hash64.OfString(effect.Cap ?? ""));
            hash.Add(Hash64.OfString(effect.Good ?? "")); hash.Add(Hash64.OfString(effect.Structure ?? ""));
            hash.Add(Hash64.OfString(effect.Cause ?? "")); hash.Add(Hash64.OfString(effect.Scope ?? ""));
            AddOptional(ref hash, effect.Rung); AddOptional(ref hash, effect.Value); AddOptional(ref hash, effect.Mult);
            AddOptional(ref hash, effect.Take); AddOptional(ref hash, effect.Rate); hash.Add(Hash64.OfString(effect.Unit ?? ""));
        }
        hash.Add(content.Actions.Length);
        foreach (EconomyAction action in content.Actions)
        {
            hash.Add(Hash64.OfString(action.Id)); AddOptional(ref hash, action.BaseRate);
            hash.Add(Hash64.OfString(action.Unit ?? ""));
        }
        hash.Add(content.Goods.Length);
        foreach (EconomyGood good in content.Goods)
        { hash.Add(Hash64.OfString(good.Id)); hash.Add(good.Spoilage); }
        return hash.Finish();
    }

    private static void AddOptional(ref StateHasher hash, int? value)
    {
        hash.Add(value.HasValue);
        if (value.HasValue) hash.Add(value.Value);
    }

    private static void AddOptional(ref StateHasher hash, Fix64? value)
    {
        hash.Add(value.HasValue);
        if (value.HasValue) hash.Add(value.Value);
    }
}
