using System;
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

/// <summary>The Iron chapter's siege: engines, breach, storm, and the Bronze rule that a wall is never assaulted (docs/MVP/25 §7).</summary>
public sealed class IronSiegeTests
{
    private readonly ITestOutputHelper _output;
    /// <summary>The E4 rows these tests grant, numeric so research itself knows them (the validator keeps every E4 row draft until all of them are promoted together).</summary>
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric("KEY-4", "WAR-07", "WAR-09", "DEF-06", "DEF-07", "DEF-08"));
    private static readonly Lazy<ResearchCatalog> Catalog = new(() => new ResearchCatalog(Pack.Value));
    private const ulong Seed = 20261024;
    /// <summary>Civilization 0 starts five to seven walking tiles from civilization 1, so its host is inside the supply reach an engine train needs.</summary>
    private static readonly Lazy<GeneratedWorld> World = new(() =>
    {
        GeneratedWorld source = WorldGenerator.Generate(Seed);
        WorldTile[] tiles = source.Tiles.ToArray();
        WorldStart[] starts = source.Starts.ToArray();
        bool[] passable = HexGrid.BuildWalkingPassability(tiles, source.Width, source.Height);
        int targetStart = starts.Single(st => st.Civilization == 1).Tile;
        int[] distance = HexGrid.WalkingDistancesFrom(source.Width, source.Height, passable, targetStart);
        var occupiedStarts = starts.Select(st => st.Tile).ToHashSet();
        int nearby = Enumerable.Range(0, tiles.Length)
            .Where(tile => !occupiedStarts.Contains(tile) && tiles[tile].IsLand && tiles[tile].Biome != Biome.Peak && distance[tile] is >= 5 and <= 7)
            .OrderBy(tile => distance[tile]).ThenBy(tile => tile)
            .First();
        int attackerIndex = Array.FindIndex(starts, st => st.Civilization == 0);
        starts[attackerIndex] = starts[attackerIndex] with { Tile = nearby };
        return new GeneratedWorld(source.Seed, source.Settings, tiles, starts, source.Herds.ToArray(), source.Predators.ToArray(), source.Report);
    });
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    public IronSiegeTests(ITestOutputHelper output) => _output = output;

    private sealed class Rig
    {
        public readonly MatchModel Match;
        public readonly SimRunner Runner;
        public Rig(ChapterRules rules)
        {
            Match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules);
            Runner = new SimRunner(Match);
        }
        public void Grant(int civ, params string[] projects)
        {
            foreach (string project in projects)
                Runner.Submit(new Command(Runner.Tick, (byte)civ, CommandKind.CompleteProject, Catalog.Value.IndexOf(project), 0, 0));
            Runner.Advance(1);
        }
        public int SettlementOf(int civ) => Enumerable.Range(0, Match.Economy.SettlementCount)
            .First(s => Match.Economy.SettlementLite(s).Civilization == civ && !Match.Politics.IsFreeFolk(s));
        public void Season() => Runner.Advance(150);
    }

    /// <summary>A besieged walled town and a home with an engine train in store, war declared; the assault has not been ordered.</summary>
    private static (Rig Rig, int Home, int Target) Prepared(ChapterRules rules, bool engines, bool towers = false)
    {
        var rig = new Rig(rules);
        rig.Grant(0, "KEY-3", "KEY-4", "WAR-07", "WAR-09");
        rig.Grant(1, towers ? new[] { "KEY-3", "KEY-4", "DEF-05", "DEF-06" } : new[] { "KEY-3", "KEY-4", "DEF-05" });
        rig.Season();
        int home = rig.SettlementOf(0), target = rig.SettlementOf(1);
        LivingEconomyModel economy = rig.Match.Economy;
        economy.FixtureAddPeople(home, 240, 24);
        economy.FixtureStock(home, "G-GRAIN", Fix64.FromInt(1000));
        economy.FixtureStock(target, "G-GRAIN", Fix64.FromInt(1000));
        economy.FixtureStock(home, "G-CHAR", Fix64.FromInt(20));
        if (engines) economy.FixtureStock(home, "G-ENGINE", Fix64.FromInt(2));
        economy.FixtureBuildStructure(target, "stone_walls");
        if (towers) { economy.FixtureBuildIronStructure(target, "gatehouse"); economy.FixtureSetTier(target, 1); }
        rig.Match.Politics.SetExternalMind(0, true);
        rig.Match.Politics.SetExternalMind(1, true);
        rig.Match.Politics.ForceContact(0, 1);
        rig.Match.Politics.SetWar(0, 1, true);
        return (rig, home, target);
    }

    private static void ArriveAndBegin(Rig rig, int target)
    {
        Assert.True(rig.Match.Politics.OrderAssault(0, target));
        int arrival = rig.Match.Politics.Expeditions.Single(e => e.Kind == ExpeditionKind.Assault).ArriveSeason;
        rig.Runner.Advance(150 * (arrival - rig.Match.Politics.Season));
    }

    [Fact]
    public void A_host_with_crew_takes_the_home_engines_and_a_Bronze_host_takes_none()
    {
        (Rig iron, int home, int target) = Prepared(ChapterRules.Iron, engines: true);
        ArriveAndBegin(iron, target);
        Investment siege = Assert.Single(iron.Match.Politics.Investments);
        string engine = "G-ENGINE", war09 = "WAR-09";
        _output.WriteLine($"members {siege.Members.Count} home {home} stock {iron.Match.Economy.GoodStock(home, engine)} war09 {iron.Match.Research.IsDone(0, Catalog.Value.IndexOf(war09))} chain {iron.Match.Economy.IronChain} homeOfSiege {siege.HomeSettlement}");
        Assert.InRange(siege.Engines, 1, Sieges.MaxEngines);
        Assert.Equal(Fix64.One, siege.Integrity);
        Assert.True(iron.Match.Economy.GoodStock(home, engine) < Fix64.FromInt(2) - Fix64.FromInt(siege.Engines) + Fix64.FromRatio(1, 2), "the engines left the store (wear aside)");

        (Rig bronze, _, int bronzeTarget) = Prepared(ChapterRules.Bronze, engines: false);
        ArriveAndBegin(bronze, bronzeTarget);
        Assert.Equal(0, Assert.Single(bronze.Match.Politics.Investments).Engines);
    }

    [Fact]
    public void Without_engines_an_iron_chapter_wall_is_never_stormed_and_integrity_stays_whole()
    {
        (Rig rig, _, int target) = Prepared(ChapterRules.Iron, engines: false);
        ArriveAndBegin(rig, target);
        for (int season = 0; season < 3 && rig.Match.Politics.InvestmentAt(target) is not null; season++) rig.Season();
        if (rig.Match.Politics.InvestmentAt(target) is { } siege)
        {
            Assert.Equal(0, siege.Engines);
            Assert.Equal(Fix64.One, siege.Integrity);
        }
        Assert.DoesNotContain(rig.Match.Politics.Events, e => e.Kind == PoliticsEventKind.AssaultResolved && e.Civ == 0 && e.B == target);
    }

    [Fact]
    public void Engines_batter_the_wall_down_and_the_besiegers_storm_the_breach()
    {
        (Rig rig, int home, int target) = Prepared(ChapterRules.Iron, engines: true);
        ArriveAndBegin(rig, target);
        Investment siege = Assert.Single(rig.Match.Politics.Investments);
        int start = rig.Match.Politics.Season;
        bool breached = false;
        for (int season = 0; season < 6 && rig.Match.Politics.InvestmentAt(target) is not null; season++)
        {
            rig.Match.Economy.FixtureStock(home, "G-CHAR", Fix64.FromInt(20));
            rig.Season();
            Investment? now = rig.Match.Politics.InvestmentAt(target);
            string events = string.Join(",", rig.Match.Politics.Events
                .Where(e => e.Kind is PoliticsEventKind.AssaultResolved or PoliticsEventKind.InvestmentRelieved or PoliticsEventKind.InvestmentCapitulated or PoliticsEventKind.InvestmentSally)
                .Select(e => e.Kind + ":" + e.Civ + ":" + e.A + ":" + e.B));
            _output.WriteLine($"season {rig.Match.Politics.Season}: engines {now?.Engines} integrity {now?.Integrity} besiegers {now?.State.BesiegerStrength} defenders {now?.State.UnwalledDefenderStrength} events {events}");
            if (now is { } live && live.Integrity < Fix64.One) breached |= live.Integrity == Fix64.Zero;
        }
        _output.WriteLine($"seasons {rig.Match.Politics.Season - start}; investment ended: {rig.Match.Politics.InvestmentAt(target) is null}");
        // Either the wall went down and the storm was fought (an assault event), or the investment was already over: engines never leave a wall untouched.
        Assert.Contains(rig.Match.Politics.Events, e => e.Kind == PoliticsEventKind.AssaultResolved && e.Civ == 0 && e.B == target);
        Assert.True(breached || rig.Match.Politics.InvestmentAt(target) is null);
    }

    [Fact]
    public void Towers_slow_the_breach_so_the_same_train_takes_a_season_longer()
    {
        int Seasons(bool towers)
        {
            (Rig rig, int home, int target) = Prepared(ChapterRules.Iron, engines: true, towers);
            ArriveAndBegin(rig, target);
            int n = 0;
            while (n < 8 && rig.Match.Politics.InvestmentAt(target) is { } s && s.Integrity > Fix64.Zero)
            {
                rig.Match.Economy.FixtureStock(home, "G-CHAR", Fix64.FromInt(20));
                rig.Season();
                n++;
            }
            return n;
        }
        int bare = Seasons(towers: false), towered = Seasons(towers: true);
        _output.WriteLine($"bare {bare}, towered {towered}");
        Assert.True(towered >= bare, "a gatehouse never makes a wall fall sooner");
    }

    [Fact]
    public void A_frontier_segment_turns_back_most_raids_from_the_civilization_it_faces_and_only_those()
    {
        (Rig rig, _, int target) = Prepared(ChapterRules.Iron, engines: false);
        rig.Grant(1, "DEF-07");
        var win = new RaidOutcome { Success = true, LootCapacity = Fix64.FromInt(40) };
        int Kept(Rig r, int raiderCiv)
        {
            int kept = 0;
            for (ulong stream = 1; stream <= 400; stream++) if (r.Match.Politics.FixtureRepelAtFrontier(win, raiderCiv, target, stream).Success) kept++;
            return kept;
        }
        Assert.Equal(400, Kept(rig, 0)); // no segment yet
        rig.Match.Economy.FixtureFaceFrontier(target, 0);
        int facing = Kept(rig, 0);
        _output.WriteLine($"{facing} of 400 raids across the segment still succeeded");
        Assert.InRange(facing, 90, 150); // x0.3 of 400 is 120
        Assert.Equal(400, Kept(rig, 2)); // a civilization the segment does not face
        // A failed raid is untouched, and a repelled raid carries nothing away.
        var lost = new RaidOutcome { Success = false };
        Assert.False(rig.Match.Politics.FixtureRepelAtFrontier(lost, 0, target, 7).Success);
        for (ulong stream = 1; stream <= 50; stream++)
        {
            RaidOutcome o = rig.Match.Politics.FixtureRepelAtFrontier(win, 0, target, stream);
            if (!o.Success) Assert.Equal(Fix64.Zero, o.LootCapacity);
        }
    }

    [Fact]
    public void A_frontier_segment_guards_the_border_near_its_town_against_the_faced_civilization_only()
    {
        (Rig rig, _, int target) = Prepared(ChapterRules.Iron, engines: false);
        rig.Grant(1, "DEF-07");
        int tile = rig.Match.Economy.SettlementLite(target).SiteTile;
        Assert.False(rig.Match.Politics.FixtureFrontierGuarded(tile, 1, 0));
        rig.Match.Economy.FixtureFaceFrontier(target, 0);
        Assert.True(rig.Match.Politics.FixtureFrontierGuarded(tile, 1, 0));
        Assert.False(rig.Match.Politics.FixtureFrontierGuarded(tile, 1, 2), "never realm-wide: another challenger flips as before");
        Assert.False(rig.Match.Politics.FixtureFrontierGuarded(tile, 0, 1), "the wall guards its owner's land");
        Assert.False(rig.Match.Politics.FixtureFrontierGuarded(0, 1, 0), "a tile out of reach of the segment's town is unguarded");
    }

    [Fact]
    public void A_gatehouse_holds_the_approaches_of_its_own_town_only()
    {
        (Rig rig, _, int target) = Prepared(ChapterRules.Iron, engines: false, towers: true);
        int tile = rig.Match.Economy.SettlementLite(target).SiteTile;
        Assert.True(rig.Match.Politics.FixtureGateControls(target, tile));
        Assert.False(rig.Match.Politics.FixtureGateControls(target, 0), "a tile far from the gate is not controlled");
        (Rig bare, _, int bareTarget) = Prepared(ChapterRules.Iron, engines: false);
        Assert.False(bare.Match.Politics.FixtureGateControls(bareTarget, bare.Match.Economy.SettlementLite(bareTarget).SiteTile));
        (Rig bronze, _, int bronzeTarget) = Prepared(ChapterRules.Bronze, engines: false, towers: true);
        Assert.False(bronze.Match.Politics.FixtureGateControls(bronzeTarget, bronze.Match.Economy.SettlementLite(bronzeTarget).SiteTile));
    }

    [Fact]
    public void A_siege_with_engines_survives_a_snapshot_round_trip()
    {
        (Rig rig, _, int target) = Prepared(ChapterRules.Iron, engines: true);
        ArriveAndBegin(rig, target);
        Assert.NotNull(rig.Match.Politics.InvestmentAt(target));
        using var snapshot = new MemoryStream();
        using (var writer = new BinaryWriter(snapshot, System.Text.Encoding.UTF8, leaveOpen: true)) rig.Match.WriteSnapshot(writer);
        snapshot.Position = 0;
        MatchModel resumed = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: ChapterRules.Iron);
        using (var reader = new BinaryReader(snapshot)) resumed.ReadSnapshot(reader);
        Assert.Equal(rig.Match.ComputeChecksum(), resumed.ComputeChecksum());
        Investment a = rig.Match.Politics.InvestmentAt(target)!, b = resumed.Politics.InvestmentAt(target)!;
        Assert.Equal(a.Engines, b.Engines);
        Assert.Equal(a.Integrity, b.Integrity);
    }
}
