using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

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

    internal Fix64 FixtureFertilityMultiplier(int civ) => FertilityMultiplier(civ);
    internal int FixtureWorldWidth => _world.Width;
    internal int FixtureWorldTileCount => _world.TileCount;
    internal bool FixtureIsHabitableSite(int tile) => IsHabitableSite(tile);
    internal bool FixtureIsForestTile(int tile) => IsForestTile(tile);
    internal bool FixtureIsRivalTile(int civ, int tile) => IsRivalTile(civ, tile);

    internal int FixtureChooseFissionSite(int settlementIndex)
        => ChooseFissionSite(_settlements[settlementIndex], checked((int)_seasonsCompleted), recordDiagnostics: false);

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

    internal Fix64 FixtureKitLeft(int figureIndex) => _figures[figureIndex].KitLeft;

    /// <summary>Applies the same kit wear and break accounting used by a live work season.</summary>
    internal void FixtureWearKit(int figureIndex, Fix64 work) => ApplyKitWear(_figures[figureIndex], work);

    /// <summary>Runs live kit wear and issue with zero tool work, for lifecycle accounting fixtures.</summary>
    internal void FixtureWearAndIssueKits(int settlementIndex, int tier, params int[] toolWorkerFigureIndices)
    {
        SettlementState settlement = _settlements[settlementIndex];
        var toolWork = new Fix64[_figures.Count];
        foreach (int figureIndex in toolWorkerFigureIndices)
        {
            if ((uint)figureIndex >= (uint)_figures.Count) throw new ArgumentOutOfRangeException(nameof(toolWorkerFigureIndices));
            if (_figures[figureIndex].SettlementId == settlement.Id) toolWork[figureIndex] = Fix64.One;
        }
        WearAndIssueKits(settlement, EnsureMetal(settlement), tier, toolWork);
    }

    /// <summary>Empties a deposit (an exhausted, captured or cut-off mine).</summary>
    internal void FixtureExhaust(int tile) => _ore[tile] = Fix64.Zero;

    /// <summary>Sets the remaining ore for a deposit in a deterministic economy fixture.</summary>
    internal void FixtureSetOreRemaining(int tile, Fix64 amount) => _ore[tile] = Fix64.Max(Fix64.Zero, amount);
    internal Fix64 FixtureOreRemaining(int tile) => _ore[tile];

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
    internal void FixtureSetWood(int tile, Fix64 litres)
    {
        _woodStock[tile] = litres;
        InvalidateIronFuelHubAssignments();
    }

    /// <summary>
    /// Scales the standing wood of every forest tile in a settlement's charcoal catchment to a percentage of its starting stock. The Iron fuel
    /// guard judges the whole catchment (ef575c3), so a thin-forest fixture must thin all of it, not only the fixture's own forest tiles.
    /// </summary>
    internal void FixtureThinCharcoalCatchment(int settlementIndex, int percent)
    {
        SettlementState s = _settlements[settlementIndex];
        int reach = Done(s.Civilization, "TRN-05") ? _bronzeConstants.CharcoalReachCarts : _bronzeConstants.CharcoalReach;
        foreach (int tile in ReachTiles(s.SiteTile, reach))
            if (IsForestTile(tile)) _woodStock[tile] = _world.Tile(tile).InitialWood * percent / 100;
    }

    /// <summary>Sets the raw stone in a settlement's quarry range to a known total for civic masonry fixtures.</summary>
    internal void FixtureSetRangeStone(int settlementIndex, Fix64 litres)
    {
        ReadOnlySpan<int> range = RangeTiles(_settlements[settlementIndex].SiteTile);
        foreach (int tile in range) _stoneStock[tile] = Fix64.Zero;
        if (range.Length > 0) _stoneStock[range[0]] = Fix64.Max(Fix64.Zero, litres);
    }

    /// <summary>Sets the raw stone within 8 tiles of a settlement (what a wall ring may be raised from) to a known total.</summary>
    internal void FixtureSetReachStone(int settlementIndex, Fix64 litres)
    {
        SettlementState s = _settlements[settlementIndex];
        foreach (int tile in ReachTiles(s.SiteTile, 8)) _stoneStock[tile] = Fix64.Zero;
        _stoneStock[s.SiteTile] = Fix64.Max(Fix64.Zero, litres);
    }

    internal Fix64 FixtureReachStone(int settlementIndex)
    {
        Fix64 total = Fix64.Zero;
        foreach (int tile in ReachTiles(_settlements[settlementIndex].SiteTile, 8)) total += _stoneStock[tile];
        return total;
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

    internal bool FixtureEarlyWorkWanted(int settlementIndex, string structure)
    {
        int kind = StructureIndex(structure);
        if (kind < 0) throw new ArgumentException($"Unknown structure '{structure}'.", nameof(structure));
        SettlementState s = _settlements[settlementIndex];
        return EarlyWorkWanted(s, (Structure)kind, checked((int)_seasonsCompleted), Population(s.Id));
    }

    internal void FixtureSetCivilization(int settlementIndex, int civ) => _settlements[settlementIndex].Civilization = civ;
    internal void FixtureSetStoredCohesion(int settlementIndex, Fix64 value) => _settlements[settlementIndex].Cohesion = value;
    internal void FixtureSetStructureCount(int settlementIndex, string structure, int count)
    {
        int kind = StructureIndex(structure);
        if (kind < 0) throw new ArgumentException($"Unknown structure '{structure}'.", nameof(structure));
        _settlements[settlementIndex].Structures[kind] = count;
    }
    internal Fix64 FixtureStoredCohesion(int settlementIndex) => _settlements[settlementIndex].Cohesion;
    internal Fix64 FixtureStorageFactor(int settlementIndex) => StorageFactor(_settlements[settlementIndex]);
    internal int FixtureBuildTarget(int settlementIndex) => (int)_settlements[settlementIndex].BuildTarget;
    internal Fix64 FixtureBuildProgress(int settlementIndex) => _settlements[settlementIndex].BuildProgress;
    internal void FixtureSetBuildProgress(int settlementIndex, Fix64 progress) => _settlements[settlementIndex].BuildProgress = progress;
    internal void FixtureBuildIfReady(int settlementIndex)
        => BuildIfReady(_settlements[settlementIndex], checked((int)_seasonsCompleted));
    internal void FixtureSetBuildTarget(int settlementIndex, string structure)
    {
        int kind = StructureIndex(structure);
        if (kind < 0) throw new ArgumentException($"Unknown structure '{structure}'.", nameof(structure));
        _settlements[settlementIndex].BuildTarget = (Structure)kind;
    }

    internal void FixtureSetCivicBuildTarget(int settlementIndex, string structure)
    {
        CivicBuild kind = CivicOf(structure) ?? throw new ArgumentException($"Unknown civic structure '{structure}'.", nameof(structure));
        EnsureCivic(_settlements[settlementIndex]).BuildKind = kind;
    }

    internal void FixtureSetScoutPolicy(int settlementIndex, ScoutPolicy policy)
        => _settlements[settlementIndex].ScoutPolicy = policy;

    internal void FixtureShareDefenseWood(int season) => ShareDefenseWood(season);
    internal Fix64 FixtureDefenseWoodDonorReserve(int settlementIndex, int season)
        => DefenseWoodDonorReserve(_settlements[settlementIndex], season);

    internal int FixtureFindHabitableSiteFartherThan(int settlementIndex, int minimumDistance)
    {
        SettlementState source = _settlements[settlementIndex];
        for (int tile = 0; tile < _world.TileCount; tile++)
        {
            if (!IsHabitable(tile) || HexGrid.Distance(source.SiteTile, tile, _world.Width) <= minimumDistance) continue;
            bool occupied = false;
            foreach (SettlementState existing in _settlements)
                if (existing.SiteTile == tile) { occupied = true; break; }
            if (!occupied) return tile;
        }
        return -1;
    }

    internal bool FixtureHasBuildCandidate(int settlementIndex)
        => HasBuildCandidate(_settlements[settlementIndex], checked((int)_seasonsCompleted));

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

    internal void FixtureSetMetalFacilities(int settlementIndex, int forges, int smelters)
    {
        if (forges < 0) throw new ArgumentOutOfRangeException(nameof(forges));
        if (smelters < 0) throw new ArgumentOutOfRangeException(nameof(smelters));
        MetalState m = EnsureMetal(_settlements[settlementIndex]);
        m.Forges = forges;
        m.Smelters = smelters;
        InvalidateIronFuelHubAssignments();
    }

    internal void FixtureSetLastToolWorker(int figureIndex, bool worked)
    {
        FigureState figure = _figures[figureIndex];
        figure.Job = worked ? EconomyJob.Farm : EconomyJob.Rest;
        figure.WorkThisSeason = worked ? Fix64.One : Fix64.Zero;
        figure.Task = 0;
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
        foreach (CompiledRecipe recipe in _ironRecipes)
            if (recipe.Def.Id == recipeId) return RunIronRecipe(s, recipeId, planned, ref labor);
        throw new ArgumentException($"Unknown recipe '{recipeId}'.", nameof(recipeId));
    }

    /// <summary>The sharing reach of a civilization (-1 without SOC-07 or GOV-04) and the hex distance between two settlements, for sharing fixtures.</summary>
    internal (int Reach, int Distance) FixtureSharing(int civ, int settlementA, int settlementB)
        => (ShareReach(civ), HexGrid.Distance(_settlements[settlementA].SiteTile, _settlements[settlementB].SiteTile, _world.Width));

    /// <summary>The iron kits the forgeless sisters of a settlement still lack, which that settlement smiths for them; zero when sharing is off or out of reach.</summary>
    internal Fix64 FixtureSisterKitDeficit(int settlementIndex)
    {
        EnsureMetal(_settlements[settlementIndex]);
        return SisterKitDeficit(_settlements[settlementIndex]);
    }

    /// <summary>A figure's kit chop rate (work-seasons of wood per work-season), 0 without a usable metal kit, for saw and axe fixtures.</summary>
    internal Fix64 FixtureKitChopRate(int figureIndex) => KitChopRate(_figures[figureIndex]);

    /// <summary>Places fields until the settlement's allowance is used or no flat tile is left, so a terrace fixture starts with its natural fields full.</summary>
    internal void FixtureFillNaturalFields(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        while (s.FieldTiles.Count < FieldLimit(s))
        {
            int before = s.FieldTiles.Count;
            PlaceField(s);
            if (s.FieldTiles.Count == before) break;
        }
    }

    internal int FixtureFieldCount(int settlementIndex) => _settlements[settlementIndex].FieldTiles.Count;
    internal bool FixtureIsTerrace(int settlementIndex, int tile) => IsIronTerrace(_settlements[settlementIndex], tile);
    internal int FixtureTerraceCount(int settlementIndex) => IronTerraceCount(_settlements[settlementIndex]);
    internal string FixtureIronBuildState(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        return $"tier {(s.Civic?.Tier ?? 255)} kind {s.Metal?.IronBuildKind} progress {s.Metal?.IronBuildProgress.ToDoubleForDisplay():F2} block {(s.Metal?.Goods[(int)MetalGood.Block] ?? Fix64.Zero).ToDoubleForDisplay():F1} wood {s.Stores[(int)Good.Wood].ToDoubleForDisplay():F1} pop {Population(s.Id)}";
    }
    internal int FixtureIronBuildKind(int settlementIndex) => _settlements[settlementIndex].Metal?.IronBuildKind ?? -1;

    /// <summary>The extra effective farm work a figure's scythe gives for a stated amount of farm work (zero without AGR-09 and a T11 or T12 kit).</summary>
    internal Fix64 FixtureScytheBonus(int figureIndex, Fix64 work) => ScytheFarmBonus(_figures[figureIndex], work);

    /// <summary>A tile's field yield for a civilization with the plough rule applied, for plough fixtures.</summary>
    internal Fix64 FixtureFieldBaseYield(int civ, WorldEconomyTile tile) => FieldBaseYield(civ, tile);

    internal Fix64 FixtureWaterCap(int settlementIndex) => WaterCap(_settlements[settlementIndex]);
    internal Fix64 FixtureWaterCapBase(int settlementIndex) => WaterCapBase(_settlements[settlementIndex]);
    internal Fix64 FixtureIronSanitationFactor(int settlementIndex) => IronSanitationFactor(_settlements[settlementIndex]);
    internal Fix64 FixtureMedCare(int civ, Fix64 multiplier) => MedCare(civ, multiplier);

    /// <summary>Raises a settlement to a civic tier (0 village, 1 town, 2 city) without the works that normally earn it.</summary>
    internal void FixtureSetTier(int settlementIndex, int tier) => EnsureCivic(_settlements[settlementIndex]).Tier = (byte)tier;

    /// <summary>The first habitable land tile of the economy's own world that is (or is not) beside the sea, for harbor fixtures; -1 when none.</summary>
    internal int FixtureLandTile(bool coast)
    {
        for (int tile = 0; tile < _world.TileCount; tile++)
        {
            WorldEconomyTile t = _world.Tile(tile);
            if ((Biome)t.BiomeId is Biome.Ocean or Biome.Coast or Biome.Peak or Biome.Ice) continue;
            if (IsPortSite(tile) == coast) return tile;
        }
        return -1;
    }

    internal void FixtureSetSoldiersSupplied(int settlementIndex, int count) => EnsureMetal(_settlements[settlementIndex]).IronSoldiersSupplied = count;
    /// <summary>Runs the completion step of an Iron structure (counts it, earns a Wonder's Legacy, claims a terrace tile) as a finished build would.</summary>
    internal void FixtureCompleteIron(int settlementIndex, string name)
    {
        for (int k = 0; k < IronSpecs.Length; k++)
            if (IronSpecs[k].Name == name) { SettlementState s = _settlements[settlementIndex]; CompleteIronBuild(s, EnsureMetal(s), (IronStructure)k); return; }
        throw new ArgumentException($"Unknown Iron structure '{name}'.", nameof(name));
    }

    /// <summary>Starts an Iron structure as the settlement's current build, with no labor in yet.</summary>
    internal void FixtureStartIronBuild(int settlementIndex, string name)
    {
        for (int k = 0; k < IronSpecs.Length; k++)
            if (IronSpecs[k].Name == name) { MetalState m = EnsureMetal(_settlements[settlementIndex]); m.IronBuildKind = k; m.IronBuildProgress = Fix64.Zero; return; }
        throw new ArgumentException($"Unknown Iron structure '{name}'.", nameof(name));
    }

    internal Fix64 FixtureIronBuildProgress(int settlementIndex) => _settlements[settlementIndex].Metal?.IronBuildProgress ?? Fix64.Zero;

    internal int FixtureLuxuryItems(int settlementIndex) => LuxuryItems(_settlements[settlementIndex]);
    internal void FixtureSpoilLiquids(int settlementIndex) { if (_settlements[settlementIndex].Metal is { } m) SpoilIronLiquids(m); }

    /// <summary>The first habitable land tile whose biome is one of the named ones, for orchard fixtures; -1 when none.</summary>
    internal int FixtureTileOfBiomes(params Biome[] biomes)
    {
        for (int tile = 0; tile < _world.TileCount; tile++) if (Array.IndexOf(biomes, (Biome)_world.Tile(tile).BiomeId) >= 0) return tile;
        return -1;
    }

    /// <summary>Plants a mature orchard plot on a tile and runs one harvest of it, as the autumn step does.</summary>
    internal void FixtureHarvestOrchard(int settlementIndex, int tile)
    {
        SettlementState s = _settlements[settlementIndex];
        s.OrchardPlots.Clear();
        s.OrchardPlots.Add(new OrchardPlot { Tile = tile, AgeSeasons = 100 });
        HarvestGrafted(s, s.OrchardPlots[0], 0);
    }

    /// <summary>How many of a run of fire trials burn a structure, with fresh flammable structures each time (the draw depends on how many remain).</summary>
    internal int FixtureBurnTrials(int settlementIndex, int trials)
    {
        SettlementState s = _settlements[settlementIndex];
        int burned = 0;
        for (int i = 0; i < trials; i++)
        {
            s.Structures[(int)Structure.ThornBarrier] = i + 1;
            if (BurnStructure(settlementIndex)) burned++;
        }
        s.Structures[(int)Structure.ThornBarrier] = 0;
        return burned;
    }

    internal Fix64 FixtureGroupCap(int settlementIndex) => GroupCap(_settlements[settlementIndex]);
    internal int FixtureSettlementCount => _settlements.Count;
    internal int FixtureCivOf(int settlementIndex) => _settlements[settlementIndex].Civilization;

    /// <summary>Moves a settlement's site (a foreign neighbour for the frontier and castle fixtures).</summary>
    internal void FixtureMoveSite(int settlementIndex, int tile)
    {
        _settlements[settlementIndex].SiteTile = tile;
        InvalidateIronFuelHubAssignments();
    }

    internal bool FixtureHasStoneWalls(int settlementIndex) => HasStoneWalls(_settlements[settlementIndex]);
    internal int FixtureNextFrontierFacing(int settlementIndex) => NextFrontierFacing(_settlements[settlementIndex]);

    /// <summary>Gives a settlement a built frontier segment facing a civilization, outright.</summary>
    internal void FixtureFaceFrontier(int settlementIndex, int civ)
    {
        SettlementState s = _settlements[settlementIndex];
        MetalState m = EnsureMetal(s);
        (m.IronFacing ??= new List<int>()).Add(civ);
        (m.IronWorks ??= new int[(int)IronStructure.Count])[(int)IronStructure.FrontierWall]++;
    }

    /// <summary>Places an Iron-chapter structure outright (terrace excluded, since it needs a tile), for effect fixtures.</summary>
    internal void FixtureBuildIronStructure(int settlementIndex, string name)
    {
        for (int k = 0; k < IronSpecs.Length; k++)
            if (IronSpecs[k].Name == name)
            {
                SettlementState s = _settlements[settlementIndex];
                (EnsureMetal(s).IronWorks ??= new int[(int)IronStructure.Count])[k]++;
                return;
            }
        throw new ArgumentException($"Unknown Iron structure '{name}'.", nameof(name));
    }

    internal bool FixtureIronBuildWanted(int settlementIndex, string name)
    {
        for (int k = 0; k < IronSpecs.Length; k++)
            if (IronSpecs[k].Name == name) return IronBuildWanted(_settlements[settlementIndex], (IronStructure)k);
        throw new ArgumentException($"Unknown Iron structure '{name}'.", nameof(name));
    }

    /// <summary>Runs one public-grain transfer for a civilization and returns the grain units moved.</summary>
    internal Fix64 FixtureSharePublicGrain(int civ) => SharePublicGrain(civ, Math.Max(0, ShareReach(civ)));
    internal Fix64 FixtureGrainUnits(int settlementIndex) => _settlements[settlementIndex].Stores[(int)Good.Grain];

    internal Fix64 FixtureIronLabor(int settlementIndex, string name)
    {
        for (int k = 0; k < IronSpecs.Length; k++)
            if (IronSpecs[k].Name == name) return IronLaborOf(_settlements[settlementIndex], IronSpecs[k]);
        throw new ArgumentException($"Unknown Iron structure '{name}'.", nameof(name));
    }

    internal Fix64 FixtureGraftedFactor(int settlementIndex, int plot) => GraftedOrchardFactor(_settlements[settlementIndex], plot);

    /// <summary>How many adults of a settlement hold a state role ("administrator" or "governor").</summary>
    internal int FixtureRoleCount(int settlementIndex, string role)
    {
        byte id = role switch { "administrator" => Role.Administrator, "governor" => Role.Governor, _ => throw new ArgumentException(role, nameof(role)) };
        int n = 0;
        foreach (FigureState f in _figures) if (f.SettlementId == _settlements[settlementIndex].Id && f.Role == id) n++;
        return n;
    }

    /// <summary>How many adults hold the Soldier role, and how many of them the season's upkeep supplied.</summary>
    internal (int Soldiers, int Supplied) FixtureSoldiers(int settlementIndex)
    {
        int n = 0;
        foreach (FigureState f in _figures) if (f.SettlementId == _settlements[settlementIndex].Id && f.Role == Role.Soldier) n++;
        return (n, _settlements[settlementIndex].Metal?.IronSoldiersSupplied ?? 0);
    }

    internal int FixtureSoldierSlots(int settlementIndex) => SoldierSlots(_settlements[settlementIndex]);

    /// <summary>Takes every state role off a settlement's people, as if the post were vacant.</summary>
    internal void FixtureClearRoles(int settlementIndex)
    {
        foreach (FigureState f in _figures) if (f.SettlementId == _settlements[settlementIndex].Id) f.Role = Role.None;
    }

    internal int FixtureRiversOnLink(int settlementIndex) => RiversOnLink(_settlements[settlementIndex]);
    internal bool FixtureRoadLinked(int settlementIndex) => RoadLinked(_settlements[settlementIndex]);
    internal int FixtureGovernorSeat(int settlementIndex) => GovernorSeatOf(_settlements[settlementIndex]);

    /// <summary>The wood factor (0 to 1) that scales the Iron chain's fuel demand for a settlement now, for throttle fixtures.</summary>
    internal Fix64 FixtureIronWoodFactor(int settlementIndex)
        => _settlements[settlementIndex].Metal is { } m ? IronWoodFactor(_settlements[settlementIndex], m) : Fix64.One;

    /// <summary>The work-seasons a kit of a tier lasts, and the work left on it when it breaks (the dormancy tail), for wear fixtures.</summary>
    internal (Fix64 Service, Fix64 BreakLeft) FixtureKitLife(int tier) => (ServiceOf(tier), KitBreakLeft(tier));

    /// <summary>The kit tier each adult of a settlement carries, in figure order (0 for none).</summary>
    internal int[] FixtureAdultKitTiers(int settlementIndex)
    {
        int id = _settlements[settlementIndex].Id;
        var tiers = new List<int>();
        foreach (FigureState f in _figures) if (f.SettlementId == id && f.Age >= 14) tiers.Add(f.Kit);
        return tiers.ToArray();
    }

    /// <summary>The age and kit tier of each adult in figure order, for fixture-only mixed-age coverage diagnostics.</summary>
    internal (int Age, int Tier)[] FixtureAdultKitCohort(int settlementIndex)
    {
        int id = _settlements[settlementIndex].Id;
        var cohort = new List<(int Age, int Tier)>();
        foreach (FigureState f in _figures) if (f.SettlementId == id && f.Age >= 14) cohort.Add((f.Age, f.Kit));
        return cohort.ToArray();
    }

    /// <summary>A figure's kit quality (1.2 new, 0.7 at replacement, 0.5 when it breaks).</summary>
    internal Fix64 FixtureKitQuality(int figureIndex) => KitQuality(_figures[figureIndex]);

    /// <summary>The work left on a kit of a tier when it breaks (negative: the dormancy tail).</summary>
    internal Fix64 FixtureKitBreakLeft(int tier) => KitBreakLeft(tier);

    /// <summary>The litres a deposit held at the start (by kind, tier and depth).</summary>
    internal static Fix64 FixtureOreLitres(WorldDeposit deposit) => OreLitres(deposit);

    /// <summary>Whether a civilization can dig a deposit now (unlocked, and depth 2 needs Shaft Mining).</summary>
    internal bool FixtureWorkable(int civ, int tile) => DepositWorkable(civ, tile);

    /// <summary>Puts a settlement mid-way through a metal build: "forge", "kiln", "smelter" or "minehead", with site clay and stone already gathered.</summary>
    internal void FixtureSetMetalBuild(int settlementIndex, string kind, Fix64 progress, int siteClay, int siteStone, int smelters = 0)
    {
        MetalState m = _settlements[settlementIndex].Metal ?? throw new InvalidOperationException("Not a Bronze settlement.");
        m.BuildKind = kind.ToLowerInvariant() switch
        {
            "forge" => MetalBuild.Forge, "kiln" => MetalBuild.Kiln, "smelter" => MetalBuild.Smelter, "minehead" => MetalBuild.MineHead,
            _ => throw new ArgumentException($"Unknown metal build '{kind}'.", nameof(kind)),
        };
        m.BuildProgress = progress;
        m.SiteClay = Fix64.FromInt(siteClay);
        m.SiteStone = Fix64.FromInt(siteStone);
        m.Smelters = smelters;
    }

    /// <summary>Metal build state: kind, progress, Forges, Kilns, Smelters.</summary>
    internal (string Kind, Fix64 Progress, int Forges, int Kilns, int Smelters) FixtureMetalBuildState(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        MetalState m = s.Metal ?? throw new InvalidOperationException("Not a Bronze settlement.");
        return (m.BuildKind.ToString(), m.BuildProgress, m.Forges, s.Structures[(int)Structure.Kiln], m.Smelters);
    }

    /// <summary>Applies the completion-only civic-to-metal transfer to a hand-made plan; returns the labor moved and the plan's new BuildAssigned.</summary>
    internal (Fix64 Moved, Fix64 BuildAssigned) FixtureCivicToMetalCompletion(int settlementIndex, Fix64 buildNeed, Fix64 buildAssigned,
        Fix64 civicAssigned, Fix64 civicCanals, Fix64 civicHousing)
    {
        var plan = new MetalPlan(_compiledRecipes.Length, PlanSlots) { BuildNeed = buildNeed, BuildAssigned = buildAssigned };
        Fix64 moved = TransferCivicWorkToMetalCompletion(_settlements[settlementIndex], plan, civicAssigned, civicCanals, civicHousing);
        return (moved, plan.BuildAssigned);
    }

    /// <summary>Plans the metal chain for a settlement at its current capacity and returns the labor buckets (build, mine, charcoal, smelt).</summary>
    internal (Fix64 Build, Fix64 Mine, Fix64 Charcoal, Fix64 Smelt) FixturePlanMetalLabor(int settlementIndex, Fix64 totalWork)
    {
        SettlementState s = _settlements[settlementIndex];
        MetalPlan plan = PlanMetal(s, totalWork);
        return (plan.BuildNeed, plan.MineNeed, plan.CharcoalNeed, plan.SmeltNeed);
    }

    /// <summary>Whether the settlement can obtain an ore this season (stock, a positive-rate deposit, or an own-land camp site).</summary>
    internal bool FixtureCanObtainOre(int settlementIndex, MetalAccessKind kind)
    {
        SettlementState s = _settlements[settlementIndex];
        s.Metal!.Access = ComputeAccess(s);
        return CanObtainOre(s, kind switch { MetalAccessKind.Copper => MetalGood.CopperOre, MetalAccessKind.Arsenic => MetalGood.ArsenicalOre, _ => MetalGood.TinOre });
    }

    /// <summary>Removes every mine head of the settlement.</summary>
    internal void FixtureClearMineHeads(int settlementIndex)
        => (_settlements[settlementIndex].Metal ?? throw new InvalidOperationException("Not a Bronze settlement.")).MineHeads.Clear();

    /// <summary>Adds a mine head to a deposit tile for the settlement, as a finished MineHead build would.</summary>
    internal void FixtureAddMineHead(int settlementIndex, int tile)
    {
        MetalState m = _settlements[settlementIndex].Metal ?? throw new InvalidOperationException("Not a Bronze settlement.");
        if (!m.MineHeads.Contains(tile)) m.MineHeads.Add(tile);
    }

    /// <summary>Each of the settlement's figures' work this season, kit durability left, and job, in figure order.</summary>
    internal (Fix64[] Work, Fix64[] KitLeft, EconomyJob[] Jobs) FixtureFigureWork(int settlementIndex)
    {
        int id = _settlements[settlementIndex].Id;
        var work = new System.Collections.Generic.List<Fix64>();
        var kit = new System.Collections.Generic.List<Fix64>();
        var jobs = new System.Collections.Generic.List<EconomyJob>();
        foreach (FigureState f in _figures)
            if (f.SettlementId == id) { work.Add(f.WorkThisSeason); kit.Add(f.KitLeft); jobs.Add(f.Job); }
        return (work.ToArray(), kit.ToArray(), jobs.ToArray());
    }

    /// <summary>The settlement's dormancy reason and best castable tier as the planner sees them this season.</summary>
    internal (int Tier, MethodFallbackReason Reason) FixtureTier(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        if (s.Metal is not { } m) return (0, MethodFallbackReason.None);
        m.Access = ComputeAccess(s);
        int tier = BestCastTier(s);
        return (tier, DormancyReason(s, tier));
    }

    /// <summary>Tests cast-tier selection with explicit sister-access bits while retaining the fixture's local stocks and deposits.</summary>
    internal int FixtureTierWithAccess(int settlementIndex, bool copper, bool arsenic, bool tin)
    {
        SettlementState s = _settlements[settlementIndex];
        MetalState m = EnsureMetal(s);
        m.Access = (byte)((copper ? AccessCopper : 0) | (arsenic ? AccessArsenic : 0) | (tin ? AccessTin : 0));
        return BestCastTier(s);
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
        return ForecastWinterProduction(s, Fix64.Zero, FoodNeed(s, true), ScoutPlan.Empty);
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

    internal Fix64 FixtureHuntWildHerd(int settlementIndex, Fix64 work, Fix64 rate, bool enforceSustainableLimit = true)
        => HuntIndividualHerds(_settlements[settlementIndex], work, rate, (int)_seasonsCompleted,
            cooperativeDrive: false, enforceSustainableLimit: enforceSustainableLimit);

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

    /// <summary>Runs the production civic season-end path for a prepared civic fixture.</summary>
    internal void FixtureCivicSeasonEnd(int season) => CivicSeasonEnd(season);

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
            if (_figures[i].SettlementId == id)
            {
                RecordIT6IronKitRetired(_figures[i].Civilization, _figures[i].SettlementId, _figures[i].Kit, Fix64.One);
                _figures.RemoveAt(i); count--;
            }
    }

    internal Fix64 FixtureHousingTarget(int settlementIndex) => HousingTarget(_settlements[settlementIndex]);
    internal Fix64 FixtureHousingCap(int settlementIndex) => HousingCap(_settlements[settlementIndex]);
    internal int FixtureFieldLimit(int settlementIndex) => FieldLimit(_settlements[settlementIndex]);
    internal Fix64 FixtureFieldLaborNeed(int settlementIndex) => FieldLaborNeed(_settlements[settlementIndex]);
    internal Fix64 FixtureIrrigation(int settlementIndex, int tile) => IrrigationFactor(_settlements[settlementIndex], tile, out _);
    internal void FixtureSetNearRiver(int settlementIndex, bool near) => _nearRiver[_settlements[settlementIndex].SiteTile] = near;
    internal void FixtureSetTemples(int settlementIndex, int temples) => EnsureCivic(_settlements[settlementIndex]).Temples = temples;
    internal Fix64 FixtureGrainFoodMult(int settlementIndex) => SettlementFoodValueMult(_settlements[settlementIndex], Good.Grain);
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
