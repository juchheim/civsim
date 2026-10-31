using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;
using Xunit.Abstractions;

namespace CivSim.Tests;

/// <summary>End-to-end wall investment lifecycle checks for docs/MVP/22 §7.4 and 07d §6.</summary>
public sealed class WP8bSiegeLifecycleTests
{
    private readonly ITestOutputHelper _output;
    private const ulong Seed = 20261024;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    public WP8bSiegeLifecycleTests(ITestOutputHelper output) => _output = output;

    private sealed class Rig
    {
        public readonly MatchModel Match;
        public readonly SimRunner Runner;
        public Rig() : this(World.Value, Seed) { }
        public Rig(GeneratedWorld world) : this(world, Seed) { }
        public Rig(GeneratedWorld world, ulong seed)
        {
            Match = MatchModel.Create(seed, TestPack.Pack, world, Balanced);
            Runner = new SimRunner(Match);
        }
        public void Grant(int civ, params string[] projects)
        {
            foreach (string project in projects)
                Runner.Submit(new Command(Runner.Tick, (byte)civ, CommandKind.CompleteProject,
                    TestPack.Catalog.IndexOf(project), 0, 0));
            Runner.Advance(1);
        }
        public int SettlementOf(int civ) => Enumerable.Range(0, Match.Economy.SettlementCount)
            .First(s => Match.Economy.SettlementLite(s).Civilization == civ && !Match.Politics.IsFreeFolk(s));
        public int SettlementTargetedBy(int civ) => Enumerable.Range(0, Match.Economy.SettlementCount)
            .First(s => Match.Economy.SettlementLite(s).Civilization == civ && !Match.Politics.IsFreeFolk(s));
        public void Season() => Runner.Advance(150);
    }

    private static (Rig Rig, int Home, int Target) Prepared()
    {
        var rig = new Rig();
        rig.Grant(0, "KEY-3");
        rig.Grant(1, "KEY-3", "DEF-05");
        rig.Season();
        int home = rig.SettlementOf(0), target = rig.SettlementTargetedBy(1);
        rig.Match.Economy.FixtureAddPeople(home, 240, 24);
        rig.Match.Economy.FixtureStock(home, "G-GRAIN", Fix64.FromInt(1000));
        rig.Match.Economy.FixtureStock(target, "G-GRAIN", Fix64.FromInt(1000));
        rig.Match.Economy.FixtureBuildStructure(target, "stone_walls");
        rig.Match.Politics.SetExternalMind(0, true);
        rig.Match.Politics.SetExternalMind(1, true);
        rig.Match.Politics.ForceContact(0, 1);
        rig.Match.Politics.SetWar(0, 1, true);
        return (rig, home, target);
    }

    private static GeneratedWorld WorldWithNearbyWalkingStart()
    {
        GeneratedWorld source = World.Value;
        WorldTile[] tiles = source.Tiles.ToArray();
        WorldStart[] starts = source.Starts.ToArray();
        bool[] passable = HexGrid.BuildWalkingPassability(tiles, source.Width, source.Height);
        int targetStart = starts.Single(s => s.Civilization == 1).Tile;
        int[] distance = HexGrid.WalkingDistancesFrom(source.Width, source.Height, passable, targetStart);
        HashSet<int> occupiedStarts = starts.Select(s => s.Tile).ToHashSet();
        int nearby = Enumerable.Range(0, tiles.Length)
            .Where(tile => !occupiedStarts.Contains(tile) && tiles[tile].IsLand && tiles[tile].Biome != Biome.Peak &&
                           distance[tile] is >= 5 and <= 7)
            .OrderBy(tile => distance[tile]).ThenBy(tile => tile)
            .First();
        int attackerIndex = Array.FindIndex(starts, s => s.Civilization == 0);
        starts[attackerIndex] = starts[attackerIndex] with { Tile = nearby };
        return new GeneratedWorld(source.Seed, source.Settings, tiles, starts, source.Herds.ToArray(),
            source.Predators.ToArray(), source.Report);
    }

    private static void SetStoredFood(LivingEconomyModel economy, int settlement, Fix64 foodValue)
    {
        string[] foodGoods = { "G-PLANT", "G-FRUIT", "G-MEAT", "G-ROOT", "G-NUTS", "G-WGRAIN", "G-GRAIN", "G-FISH", "G-DRIED",
            "G-MILK", "G-CHEESE", "G-SALTED" };
        foreach (string good in foodGoods)
        {
            Fix64 stock = economy.GoodStock(settlement, good);
            if (stock > Fix64.Zero) economy.FixtureStock(settlement, good, -stock);
        }

        Fix64 before = economy.FoodStores(settlement);
        economy.FixtureStock(settlement, "G-GRAIN", Fix64.One);
        Fix64 grainValue = economy.FoodStores(settlement) - before;
        economy.FixtureStock(settlement, "G-GRAIN", -Fix64.One);
        if (grainValue <= Fix64.Zero) throw new InvalidOperationException("Grain must provide positive food value in the siege fixture.");
        economy.FixtureStock(settlement, "G-GRAIN", foodValue / grainValue);
    }

    [Fact]
    public void Assault_against_walls_becomes_persistent_investment_and_survives_snapshot_resume()
    {
        (Rig rig, _, int target) = Prepared();
        Assert.True(rig.Match.Research.IsDone(1, TestPack.Catalog.IndexOf("DEF-05")));
        Assert.Equal(1, rig.Match.Economy.StructureCount(target, LivingEconomyModel.StructureIndex("stone_walls")));

        Assert.True(rig.Match.Politics.OrderAssault(0, target));
        Assert.Contains(rig.Match.Politics.Expeditions, e => e.Kind == ExpeditionKind.Assault && e.ToSettlement == target);
        int arrival = rig.Match.Politics.Expeditions.Single(e => e.Kind == ExpeditionKind.Assault).ArriveSeason;
        rig.Runner.Advance(150 * (arrival - rig.Match.Politics.Season));

        Assert.True(rig.Match.Politics.Events.Any(e => e.Kind == PoliticsEventKind.InvestmentBegan),
            string.Join(" | ", rig.Match.Politics.Events.Where(e => e.Kind is PoliticsEventKind.AssaultResolved or PoliticsEventKind.VoyageLost).Select(e => e.ToString())));
        Investment investment = Assert.Single(rig.Match.Politics.Investments);
        Assert.Equal(target, investment.Settlement);
        Assert.Equal(1, investment.State.BesiegerFoodSeasons);
        Assert.True(rig.Match.Economy.IsUnderInvestment(target));
        Assert.Contains(rig.Match.Politics.Events, e => e.Kind == PoliticsEventKind.InvestmentBegan && e.Civ == 0);

        using var snapshot = new MemoryStream();
        using (var writer = new BinaryWriter(snapshot, System.Text.Encoding.UTF8, leaveOpen: true)) rig.Match.WriteSnapshot(writer);
        snapshot.Position = 0;
        MatchModel resumed = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        using (var reader = new BinaryReader(snapshot)) resumed.ReadSnapshot(reader);
        Assert.Equal(rig.Match.ComputeChecksum(), resumed.ComputeChecksum());
        Assert.Single(resumed.Politics.Investments);
        Assert.True(resumed.Economy.IsUnderInvestment(target));

        rig.Season();
        new SimRunner(resumed, 50, rig.Runner.Tick - 150).Advance(150);
        Assert.Equal(rig.Match.ComputeChecksum(), resumed.ComputeChecksum());
    }

    [Fact]
    public void Owner_relief_transition_fights_in_open_field_and_can_lift_the_investment()
    {
        (Rig rig, _, int target) = Prepared();
        Assert.True(rig.Match.Politics.OrderAssault(0, target));
        int arrival = rig.Match.Politics.Expeditions.Single(e => e.Kind == ExpeditionKind.Assault).ArriveSeason;
        rig.Runner.Advance(150 * (arrival - rig.Match.Politics.Season));
        Assert.NotNull(rig.Match.Politics.InvestmentAt(target));

        // Model a relief force already arrived at the settlement, without adding a new command surface.
        rig.Match.Economy.FixtureAddPeople(target, 240, 24);
        rig.Match.Economy.FixtureStock(target, "G-GRAIN", Fix64.FromInt(1000));
        var records = new List<LivingEconomyModel.FighterRecord>();
        rig.Match.Economy.ListFightingAge(target, records);
        long[] relief = records.Select(f => f.Id).ToArray();
        Assert.True(rig.Match.Politics.ResolveInvestmentRelief(1, target, relief));

        Assert.Null(rig.Match.Politics.InvestmentAt(target));
        Assert.False(rig.Match.Economy.IsUnderInvestment(target));
        Assert.Contains(rig.Match.Politics.Events, e => e.Kind == PoliticsEventKind.InvestmentRelieved && e.Civ == 1);
    }

    [Fact]
    public void Supplied_live_economy_investment_rations_a_walled_target_without_direct_assault_capture()
    {
        GeneratedWorld world = WorldWithNearbyWalkingStart();
        var rig = new Rig(world);
        rig.Grant(0, "KEY-3");
        rig.Grant(1, "KEY-3", "DEF-05");
        rig.Season();
        int home = rig.SettlementOf(0), target = rig.SettlementTargetedBy(1);
        rig.Match.Economy.FixtureAddPeople(home, 240, 24);
        rig.Match.Economy.FixtureStock(home, "G-GRAIN", Fix64.FromInt(1000));
        rig.Match.Economy.FixtureStock(target, "G-GRAIN", Fix64.FromInt(1000));
        rig.Match.Economy.FixtureBuildStructure(target, "stone_walls");
        rig.Match.Economy.FixtureBuildStructure(target, "granary");
        rig.Match.Politics.SetExternalMind(0, true);
        rig.Match.Politics.SetExternalMind(1, true);
        rig.Match.Politics.ForceContact(0, 1);
        rig.Match.Politics.SetWar(0, 1, true);

        WorldTile[] tiles = world.Tiles.ToArray();
        bool[] passable = HexGrid.BuildWalkingPassability(tiles, world.Width, world.Height);
        int homeTile = rig.Match.Economy.SettlementLite(home).SiteTile;
        int targetTile = rig.Match.Economy.SettlementLite(target).SiteTile;
        int[] walkDistances = HexGrid.WalkingDistancesFrom(world.Width, world.Height, passable, targetTile);
        Assert.True(passable[homeTile] && passable[targetTile]);
        Assert.InRange(walkDistances[homeTile], 1, Sieges.SupplyReachTiles);

        Assert.True(rig.Match.Politics.OrderAssault(0, target));
        int arrival = rig.Match.Politics.Expeditions.Single(e => e.Kind == ExpeditionKind.Assault && e.ToSettlement == target).ArriveSeason;
        int ticksToArrival = LivingEconomyModel.SeasonTicks * (arrival - rig.Match.Politics.Season);
        Assert.True(ticksToArrival > 0);
        rig.Runner.Advance(ticksToArrival - 1);

        bool arrivalWinter = arrival % 4 == 3;
        Fix64 need = rig.Match.Economy.FoodNeedForSeason(target, arrivalWinter);
        SetStoredFood(rig.Match.Economy, target, need * Fix64.FromRatio(3, 1));
        rig.Runner.Advance(1);

        Investment investment = Assert.Single(rig.Match.Politics.Investments);
        Assert.Equal(target, investment.Settlement);
        Assert.Equal(1, rig.Match.Economy.SettlementLite(target).Civilization);
        Assert.Contains(rig.Match.Politics.Events, e => e.Kind == PoliticsEventKind.InvestmentBegan && e.Civ == 0);
        Assert.DoesNotContain(rig.Match.Politics.Events,
            e => e.Kind == PoliticsEventKind.AssaultResolved && e.Civ == 0 && e.B == target);
        bool firstSiegeSeasonWinter = (rig.Match.Politics.Season + 1) % 4 == 3;
        Fix64 firstSiegeSeasonNeed = rig.Match.Economy.FoodNeedForSeason(target, firstSiegeSeasonWinter);
        Fix64 startingStores = firstSiegeSeasonNeed * Fix64.FromRatio(5, 2);
        SetStoredFood(rig.Match.Economy, target, startingStores);
        Assert.InRange((rig.Match.Economy.FoodStores(target) / firstSiegeSeasonNeed).ToDoubleForDisplay(), 2.0, 3.0);
        int suppliedRations = investment.State.BesiegerFoodSeasons;
        int besiegerCount = investment.Members.Count;
        int startSeason = investment.BeganSeason;

        for (int elapsed = 1; elapsed <= 6 && rig.Match.Politics.InvestmentAt(target) is not null; elapsed++)
        {
            rig.Season();
            Investment? active = rig.Match.Politics.InvestmentAt(target);
            if (active is not null)
            {
                Assert.Equal(1, rig.Match.Economy.SettlementLite(target).Civilization);
                Assert.Equal(suppliedRations, active.State.BesiegerFoodSeasons);
                Assert.Equal(besiegerCount, active.Members.Count);
            }
            Assert.DoesNotContain(rig.Match.Politics.Events,
                e => e.Kind is PoliticsEventKind.InvestmentSally or PoliticsEventKind.InvestmentDeserted);
        }

        Assert.NotNull(rig.Match.Politics.InvestmentAt(target));
        Assert.Equal(1, rig.Match.Economy.SettlementLite(target).Civilization);
        Assert.DoesNotContain(rig.Match.Politics.Events, e =>
            e.Kind == PoliticsEventKind.InvestmentCapitulated && e.Civ == 0 && e.A == target);
        Assert.Equal(startSeason + 6, rig.Match.Politics.Season);
    }

    [Fact]
    public void Controlled_live_investment_records_capitulation_timing_with_real_assault_and_supply()
    {
        // Controlled-food integration fixture, not ordinary-economy acceptance: the real match, assault route,
        // wall conversion, supply check and siege lifecycle run; target stores are reset before each season's
        // final tick so local food production cannot hide the siege collapse window.
        GeneratedWorld world = WorldWithNearbyWalkingStart();
        var rig = new Rig(world);
        rig.Grant(0, "KEY-3");
        rig.Grant(1, "KEY-3", "DEF-05");
        rig.Season();
        int home = rig.SettlementOf(0), target = rig.SettlementTargetedBy(1);
        rig.Match.Economy.FixtureAddPeople(home, 240, 24);
        rig.Match.Economy.FixtureStock(home, "G-GRAIN", Fix64.FromInt(1000));
        rig.Match.Economy.FixtureStock(target, "G-GRAIN", Fix64.FromInt(1000));
        rig.Match.Economy.FixtureBuildStructure(target, "stone_walls");
        rig.Match.Economy.FixtureBuildStructure(target, "granary");
        rig.Match.Politics.SetExternalMind(0, true);
        rig.Match.Politics.SetExternalMind(1, true);
        rig.Match.Politics.ForceContact(0, 1);
        rig.Match.Politics.SetWar(0, 1, true);

        Assert.True(rig.Match.Politics.OrderAssault(0, target));
        int arrival = rig.Match.Politics.Expeditions.Single(e => e.Kind == ExpeditionKind.Assault && e.ToSettlement == target).ArriveSeason;
        rig.Runner.Advance(LivingEconomyModel.SeasonTicks * (arrival - rig.Match.Politics.Season) - 1);
        bool arrivalWinter = arrival % 4 == 3;
        Fix64 arrivalNeed = rig.Match.Economy.FoodNeedForSeason(target, arrivalWinter);
        SetStoredFood(rig.Match.Economy, target, arrivalNeed * Fix64.FromRatio(5, 2));
        rig.Runner.Advance(1);
        Assert.NotNull(rig.Match.Politics.InvestmentAt(target));
        Assert.Equal(1, rig.Match.Economy.SettlementLite(target).Civilization);

        WorldTile[] routeTiles = world.Tiles.ToArray();
        bool[] passable = HexGrid.BuildWalkingPassability(routeTiles, world.Width, world.Height);
        int homeTile = rig.Match.Economy.SettlementLite(home).SiteTile;
        int targetTile = rig.Match.Economy.SettlementLite(target).SiteTile;
        int routeDistance = HexGrid.WalkingDistancesFrom(world.Width, world.Height, passable, targetTile)[homeTile];
        Assert.InRange(routeDistance, 1, Sieges.SupplyReachTiles);

        Investment siege = rig.Match.Politics.InvestmentAt(target)!;
        bool firstSiegeWinter = (rig.Match.Politics.Season + 1) % 4 == 3;
        Fix64 firstSiegeNeed = rig.Match.Economy.FoodNeedForSeason(target, firstSiegeWinter);
        Fix64 controlledStart = firstSiegeNeed * Fix64.FromRatio(5, 2);
        SetStoredFood(rig.Match.Economy, target, controlledStart);
        siege.State = siege.State with { StoresSeasons = Fix64.FromRatio(5, 2) };
        Assert.InRange(siege.State.StoresSeasons.ToDoubleForDisplay(), 2.0, 3.0);
        int carriedFood = siege.State.BesiegerFoodSeasons;
        int beginning = siege.BeganSeason;
        var trace = new List<string>();
        trace.Add($"begin stores={siege.State.StoresSeasons.ToDoubleForDisplay():F2}, supplyRoute={routeDistance} tiles");
        for (int elapsed = 1; elapsed <= 6 && rig.Match.Politics.InvestmentAt(target) is not null; elapsed++)
        {
            // Keep a valid supply route and explicitly control the reconstructed siege-state trajectory.
            // AdvanceInvestments reads post-economy stores / need + 0.75 before Sieges.Advance subtracts
            // its ration. Set the pre-final-tick stock to (desiredState - 0.75) * need so the live
            // economy's ration and the helper's reconstruction exercise the same season accounting.
            rig.Runner.Advance(LivingEconomyModel.SeasonTicks - 1);
            bool winter = (rig.Match.Politics.Season + 1) % 4 == 3;
            Fix64 need = rig.Match.Economy.FoodNeedForSeason(target, winter);
            Fix64 desiredState = elapsed switch
            {
                1 => Fix64.FromInt(2),
                2 => Fix64.FromRatio(3, 2),
                3 => Sieges.RationNeed,
                _ => Sieges.RationNeed,
            };
            SetStoredFood(rig.Match.Economy, target, Fix64.Max(Fix64.Zero, desiredState - Sieges.RationNeed) * need);
            rig.Runner.Advance(1);
            Investment? active = rig.Match.Politics.InvestmentAt(target);
            bool supplied = routeDistance <= Sieges.SupplyReachTiles;
            if (active is not null) Assert.Equal(carriedFood, active.State.BesiegerFoodSeasons);
            Fix64 stores = rig.Match.Economy.FoodStores(target);
            Fix64 currentNeed = rig.Match.Economy.FoodNeedForSeason(target, (rig.Match.Politics.Season + 1) % 4 == 3);
            string outcomes = string.Join(',', rig.Match.Politics.Events
                .Where(e => e.Kind is PoliticsEventKind.InvestmentSally or PoliticsEventKind.InvestmentDeserted or PoliticsEventKind.InvestmentCapitulated)
                .Select(e => e.Kind.ToString()).Distinct());
            trace.Add($"season {elapsed}: stores={(currentNeed > Fix64.Zero ? stores / currentNeed : Fix64.Zero).ToDoubleForDisplay():F2}, supply={supplied}, events={outcomes}");
        }

        PoliticsEvent? capitulation = rig.Match.Politics.Events
            .Where(e => e.Kind == PoliticsEventKind.InvestmentCapitulated && e.Civ == 0 && e.A == target)
            .Select(e => (PoliticsEvent?)e).LastOrDefault();
        foreach (string row in trace) _output.WriteLine(row);
        Assert.NotNull(capitulation);
        Assert.InRange(capitulation.Value.B, 3, 6);
        Assert.True(rig.Match.Politics.Season >= beginning + 3);
        Assert.DoesNotContain(rig.Match.Politics.Events,
            e => e.Kind is PoliticsEventKind.InvestmentSally or PoliticsEventKind.InvestmentDeserted);
    }

    [Fact]
    public void Controlled_live_siege_timing_reaches_target_rate_across_fixed_match_seeds()
    {
        // Short controlled integration sample, not ordinary-economy acceptance. Reuse one generated route/world
        // while varying only the deterministic match seed so the siege outcome stream changes reproducibly.
        const int sampleCount = 16;
        GeneratedWorld world = WorldWithNearbyWalkingStart();
        int captures = 0;
        var timings = new List<string>(sampleCount);

        for (ulong offset = 0; offset < sampleCount; offset++)
        {
            ulong seed = 20261024 + offset;
            var rig = new Rig(world, seed);
            rig.Grant(0, "KEY-3");
            rig.Grant(1, "KEY-3", "DEF-05");
            rig.Season();
            int home = rig.SettlementOf(0), target = rig.SettlementTargetedBy(1);
            rig.Match.Economy.FixtureAddPeople(home, 240, 24);
            rig.Match.Economy.FixtureStock(home, "G-GRAIN", Fix64.FromInt(1000));
            rig.Match.Economy.FixtureStock(target, "G-GRAIN", Fix64.FromInt(1000));
            rig.Match.Economy.FixtureBuildStructure(target, "stone_walls");
            rig.Match.Economy.FixtureBuildStructure(target, "granary");
            rig.Match.Politics.SetExternalMind(0, true);
            rig.Match.Politics.SetExternalMind(1, true);
            rig.Match.Politics.ForceContact(0, 1);
            rig.Match.Politics.SetWar(0, 1, true);

            Assert.True(rig.Match.Politics.OrderAssault(0, target));
            int arrival = rig.Match.Politics.Expeditions.Single(e => e.Kind == ExpeditionKind.Assault && e.ToSettlement == target).ArriveSeason;
            rig.Runner.Advance(LivingEconomyModel.SeasonTicks * (arrival - rig.Match.Politics.Season) - 1);
            bool arrivalWinter = arrival % 4 == 3;
            Fix64 arrivalNeed = rig.Match.Economy.FoodNeedForSeason(target, arrivalWinter);
            SetStoredFood(rig.Match.Economy, target, arrivalNeed * Fix64.FromRatio(5, 2));
            rig.Runner.Advance(1);

            Investment siege = Assert.IsType<Investment>(rig.Match.Politics.InvestmentAt(target));
            Assert.Equal(1, rig.Match.Economy.SettlementLite(target).Civilization); // no wall-assault bypass
            WorldTile[] routeTiles = world.Tiles.ToArray();
            bool[] passable = HexGrid.BuildWalkingPassability(routeTiles, world.Width, world.Height);
            int homeTile = rig.Match.Economy.SettlementLite(home).SiteTile;
            int targetTile = rig.Match.Economy.SettlementLite(target).SiteTile;
            int routeDistance = HexGrid.WalkingDistancesFrom(world.Width, world.Height, passable, targetTile)[homeTile];
            Assert.InRange(routeDistance, 1, Sieges.SupplyReachTiles);

            bool firstWinter = (rig.Match.Politics.Season + 1) % 4 == 3;
            Fix64 firstNeed = rig.Match.Economy.FoodNeedForSeason(target, firstWinter);
            SetStoredFood(rig.Match.Economy, target, firstNeed * Fix64.FromRatio(5, 2));
            siege.State = siege.State with { StoresSeasons = Fix64.FromRatio(5, 2) };
            Assert.InRange(siege.State.StoresSeasons.ToDoubleForDisplay(), 2.0, 3.0);
            int carriedFood = siege.State.BesiegerFoodSeasons;
            int began = siege.BeganSeason;

            for (int elapsed = 1; elapsed <= 6 && rig.Match.Politics.InvestmentAt(target) is not null; elapsed++)
            {
                rig.Runner.Advance(LivingEconomyModel.SeasonTicks - 1);
                bool winter = (rig.Match.Politics.Season + 1) % 4 == 3;
                Fix64 need = rig.Match.Economy.FoodNeedForSeason(target, winter);
                Fix64 desiredState = elapsed switch
                {
                    // Keep post-ration stores above one full need for the first two seasons;
                    // lower stores enter the helper's 35% low-stores capitulation window early.
                    1 or 2 => Fix64.FromInt(3),
                    _ => Sieges.RationNeed,
                };
                SetStoredFood(rig.Match.Economy, target,
                    Fix64.Max(Fix64.Zero, desiredState - Sieges.RationNeed) * need);
                rig.Runner.Advance(1);

                Investment? active = rig.Match.Politics.InvestmentAt(target);
                if (active is null) break;
                Assert.Equal(carriedFood, active.State.BesiegerFoodSeasons);
                Assert.Equal(1, rig.Match.Economy.SettlementLite(target).Civilization);
                Assert.DoesNotContain(rig.Match.Politics.Events,
                    e => e.Kind is PoliticsEventKind.InvestmentSally or PoliticsEventKind.InvestmentDeserted ||
                         e.Kind == PoliticsEventKind.AssaultResolved && e.Civ == 0 && e.B == target);
            }

            PoliticsEvent? capitulation = rig.Match.Politics.Events
                .Where(e => e.Kind == PoliticsEventKind.InvestmentCapitulated && e.Civ == 0 && e.A == target)
                .Select(e => (PoliticsEvent?)e).LastOrDefault();
            if (capitulation is not null)
            {
                captures++;
                Assert.InRange(capitulation.Value.B, 3, 6);
                timings.Add($"{seed}:capitulated@{capitulation.Value.B}");
            }
            else
            {
                timings.Add($"{seed}:not-capitulated-by-6");
                Assert.NotNull(rig.Match.Politics.InvestmentAt(target));
            }
        }

        foreach (string timing in timings) _output.WriteLine(timing);
        _output.WriteLine($"controlled sample captures={captures}/{sampleCount}");
        Assert.True(captures >= sampleCount / 2, $"Expected ≥50% capitulation in seasons 3–6; {captures}/{sampleCount}. {string.Join(", ", timings)}");
    }

    [Fact]
    public void Siege_helper_uses_the_documented_investment_lift_and_store_gates_deterministically()
    {
        Assert.True(Sieges.CanInvest(Fix64.FromRatio(132, 10), Fix64.FromInt(10)));
        Assert.False(Sieges.CanInvest(Fix64.FromRatio(119, 10), Fix64.FromInt(10)));
        var safe = new SiegeState(Fix64.FromInt(20), Fix64.FromInt(10), Fix64.FromInt(30), Fix64.FromInt(2), 1, 0);
        Assert.Equal(SiegeResult.Ongoing, Sieges.Advance(ref safe, supplied: true, Seed, 19, settlement: 7));

        int lowCapitulations = 0, emptyCapitulations = 0;
        for (ulong seed = 1; seed <= 64; seed++)
        {
            var low = new SiegeState(Fix64.FromInt(20), Fix64.FromInt(10), Fix64.FromInt(30), Fix64.One, 1, 0);
            SiegeResult lowResult = Sieges.Advance(ref low, supplied: true, seed, 19, settlement: 7);
            var replay = new SiegeState(Fix64.FromInt(20), Fix64.FromInt(10), Fix64.FromInt(30), Fix64.One, 1, 0);
            Assert.Equal(lowResult, Sieges.Advance(ref replay, supplied: true, seed, 19, settlement: 7));
            if (lowResult == SiegeResult.Capitulated) lowCapitulations++;

            var empty = new SiegeState(Fix64.FromInt(20), Fix64.FromInt(10), Fix64.FromInt(30),
                Sieges.RationNeed, 1, 0);
            if (Sieges.Advance(ref empty, supplied: true, seed, 19, settlement: 7) == SiegeResult.Capitulated)
                emptyCapitulations++;
        }
        Assert.InRange(lowCapitulations, 1, 40); // 35% deterministic keyed stream.
        Assert.True(emptyCapitulations > lowCapitulations); // 70% at zero stores.

        var carried = new SiegeState(Fix64.FromInt(20), Fix64.FromInt(10), Fix64.FromInt(30),
            Fix64.FromInt(4), 1, 0);
        Assert.Equal(SiegeResult.Ongoing, Sieges.Advance(ref carried, supplied: false, Seed, 19, settlement: 7));
        Assert.Equal(Fix64.FromInt(20), carried.BesiegerStrength);
        Assert.Equal(0, carried.BesiegerFoodSeasons);
        Assert.Equal(SiegeResult.Ongoing, Sieges.Advance(ref carried, supplied: false, Seed, 19, settlement: 7));
        Assert.True(carried.BesiegerStrength >= Fix64.FromRatio(17999, 1000) &&
            carried.BesiegerStrength <= Fix64.FromRatio(18001, 1000));
    }

    [Fact]
    public void Supplied_investments_match_the_T22_three_to_six_season_collapse_rates()
    {
        const int trials = 1_000;
        var cases = new[]
        {
            (Stores: Fix64.FromInt(2), Expected: 0.651, Tolerance: 0.04),
            (Stores: Fix64.FromRatio(5, 2), Expected: 0.980, Tolerance: 0.03),
            (Stores: Fix64.FromInt(3), Expected: 0.980, Tolerance: 0.03),
        };

        foreach (var testCase in cases)
        {
            int capitulationsInWindow = 0;
            for (ulong seed = 1; seed <= trials; seed++)
            {
                var state = new SiegeState(Fix64.FromInt(20), Fix64.FromInt(10), Fix64.FromInt(30),
                    testCase.Stores, 1, 0);
                SiegeResult result = SiegeResult.Ongoing;
                for (int season = 1; season <= 6; season++)
                {
                    result = Sieges.Advance(ref state, supplied: true, seed, 19, settlement: 7);
                    if (result != SiegeResult.Ongoing) break;
                }

                Assert.True(result is SiegeResult.Ongoing or SiegeResult.Capitulated,
                    $"Supplied 2x-strength investment unexpectedly ended as {result} at season {state.Season}.");
                if (result == SiegeResult.Capitulated && state.Season is >= 3 and <= 6)
                    capitulationsInWindow++;
            }

            double rate = (double)capitulationsInWindow / trials;
            Assert.InRange(rate, testCase.Expected - testCase.Tolerance, testCase.Expected + testCase.Tolerance);
        }
    }
}
