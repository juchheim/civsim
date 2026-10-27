using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// Economy fixtures for tests and the measurement harness (docs/MVP/22 §16 B12, B13): set up a Bronze Age settlement
/// directly instead of playing a match to it. Never called by a match; a model that used <see cref="FixtureHoldSettlements"/>
/// says so in its checksum.
/// </summary>
public sealed partial class LivingEconomyModel
{
    private bool _holdSettlements;
    private bool _holdPopulation;

    /// <summary>When set, settlements never split, so a fixture settlement keeps its size (the C1 fixture's rule for one settlement).</summary>
    internal bool FixtureHoldSettlements
    {
        get => _holdSettlements;
        set => _holdSettlements = value;
    }

    /// <summary>When set, a fixture keeps its starting figures and ages; never enabled in a match.</summary>
    internal bool FixtureHoldPopulation
    {
        get => _holdPopulation;
        set => _holdPopulation = value;
    }

    /// <summary>Completes projects for a civilization at any tick (the economy side of a research grant).</summary>
    internal void FixtureComplete(int civ, params string[] projectIds)
    {
        foreach (string id in projectIds)
        {
            int index = ProjectIndex(id);
            if (index < 0) throw new ArgumentException($"Unknown project '{id}'.", nameof(projectIds));
            CompleteProject(civ, index);
        }
    }

    /// <summary>Adds people of one age to a settlement (healthy, unskilled).</summary>
    internal void FixtureAddPeople(int settlementIndex, int count, int age)
    {
        SettlementState s = _settlements[settlementIndex];
        for (int i = 0; i < count; i++) AddFigure(s.Civilization, s.Id, age, i % 2 == 0);
    }

    /// <summary>Adds (or with a negative amount removes) a good in a settlement's stores.</summary>
    internal void FixtureStock(int settlementIndex, string goodId, Fix64 amount)
    {
        int slot = SlotOf(goodId);
        if (slot == NoSlot) throw new ArgumentException($"The economy does not stock '{goodId}'.", nameof(goodId));
        AddStock(_settlements[settlementIndex], slot, amount);
    }

    /// <summary>Gives a figure a kit of a tier with work-seasons left on it (0 removes the kit).</summary>
    internal void FixtureSetKit(int figureIndex, int tier, Fix64 left)
    {
        FigureState f = _figures[figureIndex];
        f.Kit = (byte)tier;
        f.KitLeft = tier == 0 ? Fix64.Zero : left;
    }

    /// <summary>Empties a deposit (an exhausted, captured or cut-off mine).</summary>
    internal void FixtureExhaust(int tile) => _ore[tile] = Fix64.Zero;

    /// <summary>Adds a populated mining camp at a chosen deposit for view and lifecycle fixtures.</summary>
    internal void FixtureAddMiningCamp(int parentSettlementIndex, int tile, int crew = 4, bool built = true, bool mineHead = false)
    {
        SettlementState parent = _settlements[parentSettlementIndex];
        var camp = new MiningCampState
        {
            Id = _nextCampId++, Parent = parentSettlementIndex, Tile = tile, Opened = checked((int)_seasonsCompleted),
            Target = Math.Max(2, crew), Built = built, MineHead = mineHead,
        };
        _camps.Add(camp);
        int assigned = 0;
        foreach (FigureState f in _figures)
        {
            if (assigned >= crew) break;
            if (f.SettlementId != parent.Id || f.Camp >= 0 || f.Age is < 14 or >= 45 || f.WoundSeasons > 0 || f.IllnessSeasons > 0) continue;
            f.Camp = camp.Id;
            assigned++;
        }
    }

    /// <summary>Sets a tile's standing wood (a forest burned off, or a clearing).</summary>
    internal void FixtureSetWood(int tile, Fix64 litres) => _woodStock[tile] = litres;

    /// <summary>Sets the raw stone in a settlement's quarry range to a known total for civic masonry fixtures.</summary>
    internal void FixtureSetRangeStone(int settlementIndex, Fix64 litres)
    {
        ReadOnlySpan<int> range = RangeTiles(_settlements[settlementIndex].SiteTile);
        foreach (int tile in range) _stoneStock[tile] = Fix64.Zero;
        if (range.Length > 0) _stoneStock[range[0]] = Fix64.Max(Fix64.Zero, litres);
    }

    internal Fix64 FixtureRangeStone(int settlementIndex)
    {
        Fix64 total = Fix64.Zero;
        foreach (int tile in RangeTiles(_settlements[settlementIndex].SiteTile)) total += _stoneStock[tile];
        return total;
    }

    internal Fix64 FixtureCivicBuildProgress(int settlementIndex)
        => _settlements[settlementIndex].Civic?.BuildProgress ?? Fix64.Zero;

    internal static bool FixtureBetterPalaceSite(bool candidateTown, Fix64 candidateCapacity, int candidatePop, int candidateId,
        bool currentTown, Fix64 currentCapacity, int currentPop, int currentId)
        => BetterPalaceSite(candidateTown, candidateCapacity, candidatePop, candidateId,
            currentTown, currentCapacity, currentPop, currentId);

    /// <summary>Builds one registered structure outright for a deterministic politics fixture.</summary>
    internal void FixtureBuildStructure(int settlementIndex, string structure)
    {
        int kind = StructureIndex(structure);
        if (kind < 0) throw new ArgumentException($"Unknown structure '{structure}'.", nameof(structure));
        _settlements[settlementIndex].Structures[kind]++;
    }

    internal int FixtureBuildTarget(int settlementIndex) => (int)_settlements[settlementIndex].BuildTarget;

    internal bool FixtureHasBuildCandidate(int settlementIndex) => HasBuildCandidate(_settlements[settlementIndex]);

    /// <summary>Builds a metal structure outright: "forge", "smelter", "kiln" or "mine_head" (at <paramref name="tile"/>).</summary>
    internal void FixtureBuild(int settlementIndex, string structure, int tile = -1)
    {
        SettlementState s = _settlements[settlementIndex];
        MetalState m = EnsureMetal(s);
        switch (structure)
        {
            case "forge": m.Forges++; break;
            case "smelter": m.Smelters++; break;
            case "kiln": s.Structures[(int)Structure.Kiln]++; break;
            case "mine_head": if (!m.MineHeads.Contains(tile)) m.MineHeads.Add(tile); break;
            default: throw new ArgumentException($"Unknown metal structure '{structure}'.", nameof(structure));
        }
    }

    /// <summary>Runs one recipe at a settlement for up to <paramref name="planned"/> batches with <paramref name="labor"/> work-seasons; returns the batches made.</summary>
    internal Fix64 FixtureRunRecipe(int settlementIndex, string recipeId, Fix64 planned, Fix64 labor)
    {
        SettlementState s = _settlements[settlementIndex];
        EnsureMetal(s);
        foreach (CompiledRecipe recipe in _compiledRecipes)
        {
            if (recipe.Def.Id != recipeId) continue;
            if (!RecipeAvailable(s, recipe)) return Fix64.Zero;
            Fix64 craft = Fix64.Zero;
            return RunRecipe(s, recipe, planned, anyMetalKit: false, ref labor, ref craft);
        }
        throw new ArgumentException($"Unknown recipe '{recipeId}'.", nameof(recipeId));
    }

    /// <summary>A figure's kit quality (1.2 new, 0.7 at replacement, 0.5 when it breaks).</summary>
    internal Fix64 FixtureKitQuality(int figureIndex) => KitQuality(_figures[figureIndex]);

    /// <summary>The work left on a kit of a tier when it breaks (negative: the dormancy tail).</summary>
    internal Fix64 FixtureKitBreakLeft(int tier) => KitBreakLeft(tier);

    /// <summary>The litres a deposit held at the start (by kind, tier and depth).</summary>
    internal static Fix64 FixtureOreLitres(WorldDeposit deposit) => OreLitres(deposit);

    /// <summary>Whether a civilization can dig a deposit now (unlocked, and depth 2 needs Shaft Mining).</summary>
    internal bool FixtureWorkable(int civ, int tile) => DepositWorkable(civ, tile);

    /// <summary>The settlement's dormancy reason and best castable tier as the planner sees them this season.</summary>
    internal (int Tier, MethodFallbackReason Reason) FixtureTier(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        if (s.Metal is not { } m) return (0, MethodFallbackReason.None);
        m.Access = ComputeAccess(s);
        int tier = BestCastTier(s);
        return (tier, DormancyReason(s, tier));
    }

    /// <summary>The camp a figure works at, or -1.</summary>
    internal int FixtureCampOf(int figureIndex) => _figures[figureIndex].Camp;

    /// <summary>
    /// The C1-E3 food fixture (docs/MVP/22 §9, 23 §11.1, check_e3.py): gives a settlement its best <paramref name="fields"/> field
    /// tiles at a soil share of <paramref name="soilShare"/>, canals on those within 2 tiles of a river (with AGR-06), pastures with
    /// their herds, and carts. Returns the fields placed and the canals dug.
    /// </summary>
    internal (int Fields, int Canals) FixtureFood(int settlementIndex, int fields, Fix64 soilShare, int pastures, Fix64 cattle, Fix64 sheep, int carts)
    {
        SettlementState s = _settlements[settlementIndex];
        while (s.FieldTiles.Count < fields)
        {
            int before = s.FieldTiles.Count;
            PlaceField(s);
            if (s.FieldTiles.Count == before) break;
            s.Structures[(int)Structure.Field]++;
        }
        foreach (int tile in s.FieldTiles) _soil[tile] = _world.Tile(tile).SoilFertility * soilShare;
        s.Structures[(int)Structure.Pasture] = pastures;
        s.DomesticCattle = cattle;
        s.DomesticSmallStock = sheep;
        int canals = 0;
        if (CivInBronze(s.Civilization))
        {
            CivicState c = EnsureCivic(s);
            if (HasProject(s.Civilization, "AGR-06"))
                foreach (int tile in s.FieldTiles)
                    if (_nearRiver[tile] && !c.Canals.Contains(tile)) { c.Canals.Add(tile); canals++; }
            if (carts > 0) EnsureMetal(s).Goods[(int)MetalGood.Cart] = Fix64.FromInt(carts);
            c.FoodValueMult = FoodValuePassThrough(s, c);
        }
        return (s.FieldTiles.Count, canals);
    }

    /// <summary>Runs the winter-production forecast for the selected settlement (regression coverage only).</summary>
    internal Fix64 FixtureForecastWinterProduction(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        return ForecastWinterProduction(s, Fix64.Zero, FoodNeed(s, true));
    }

    internal Fix64 FixtureDomesticHerdWorkNeed(int settlementIndex)
        => DomesticHerdWorkNeed(_settlements[settlementIndex]);

    internal bool FixtureCanTameFromRange(int settlementIndex) => CanTameFromRange(_settlements[settlementIndex]);

    internal void FixtureSetWildHerd(int herdIndex, int tile, Fix64 animals)
    {
        WildHerdState herd = _wildHerds[herdIndex];
        herd.CurrentTile = tile;
        herd.CurrentAnimals = animals;
        if (_hasIndividualHerds) RebuildWildHerdFoodStock();
    }

    internal Fix64 FixtureHuntWildHerd(int settlementIndex, Fix64 work, Fix64 rate)
        => HuntIndividualHerds(_settlements[settlementIndex], work, rate, (int)_seasonsCompleted, cooperativeDrive: false);

    internal void FixtureApplyNaturalHerdCrash(int herdIndex)
    {
        _wildHerds[herdIndex].CurrentAnimals *= Fix64.Half;
        if (_hasIndividualHerds) RebuildWildHerdFoodStock();
    }

    /// <summary>Raises a civic structure outright: "market", "temple", "palace", "school", "bakery", "hall" or "ziggurat".</summary>
    internal void FixtureCivic(int settlementIndex, string structure)
    {
        SettlementState s = _settlements[settlementIndex];
        CivicState c = EnsureCivic(s);
        AddBuilt(c, CivicOf(structure) ?? throw new ArgumentException($"Unknown civic structure '{structure}'.", nameof(structure)));
    }

    /// <summary>Sets a settlement's Cohesion (C, 0–100) and its conquered term (≤ 0).</summary>
    internal void FixtureCohesion(int settlementIndex, int cohesion, int conqueredTerm = 0)
    {
        SettlementState s = _settlements[settlementIndex];
        s.Cohesion = CohesionFactor(cohesion);
        EnsureCivic(s).ConqueredTerm = Math.Min(0, conqueredTerm);
    }

    /// <summary>Removes the youngest-added (highest id) people of a settlement.</summary>
    internal void FixtureRemovePeople(int settlementIndex, int count)
    {
        int id = _settlements[settlementIndex].Id;
        for (int i = _figures.Count - 1; i >= 0 && count > 0; i--)
            if (_figures[i].SettlementId == id) { _figures.RemoveAt(i); count--; }
    }

    internal Fix64 FixtureHousingTarget(int settlementIndex) => HousingTarget(_settlements[settlementIndex]);
    internal Fix64 FixtureHousingCap(int settlementIndex) => HousingCap(_settlements[settlementIndex]);
    internal int FixtureFieldLimit(int settlementIndex) => FieldLimit(_settlements[settlementIndex]);
    internal Fix64 FixtureFieldLaborNeed(int settlementIndex) => FieldLaborNeed(_settlements[settlementIndex]);
    internal Fix64 FixtureIrrigation(int settlementIndex, int tile) => IrrigationFactor(_settlements[settlementIndex], tile, out _);
    internal int FixtureCohesionTarget(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        return CohesionTarget(s, EnsureCivic(s), Population(s.Id));
    }
    internal bool FixtureDiscreteEpidemics(int figureIndex) => DiscreteEpidemics(_figures[figureIndex]);
    internal Fix64 FixtureWorkCapacity(int figureIndex) => WorkCapacity(_figures[figureIndex]);

    /// <summary>Field tiles of a settlement within 2 tiles of a river (where AGR-06 can dig canals).</summary>
    internal int FixtureNearRiverFields(int settlementIndex)
    {
        int n = 0;
        foreach (int tile in _settlements[settlementIndex].FieldTiles) if (_nearRiver[tile]) n++;
        return n;
    }
}
