using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.View;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>
/// The reserved settlement core (docs/MVP/31 §3.3, WP3): no field, orchard, Iron terrace or mining camp stands within one hex of a living
/// settlement's site; plots a save from before the rule holds there are released at the next season; the planner never asks for one there.
/// </summary>
public sealed class SettlementCoreTests
{
    private const ulong Seed = 20261019;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly Lazy<WorldEconomyInput> Input = new(() => WorldEconomyInput.FromGeneratedWorld(World.Value));
    private static int Width => World.Value.Width;

    private static BronzeFixture Town() => BronzeFixture.CreateTerraceScenario(TestPack.Pack, Seed, 60, ironChain: true, "AGR-08", "MIN-03");

    private static int[] Ring(int site, int ring)
        => Enumerable.Range(0, Input.Value.TileCount).Where(t => HexGrid.Distance(site, t, Width) == ring).ToArray();

    private static bool Land(int tile) => (Biome)Input.Value.Tile(tile).BiomeId is not (Biome.Ocean or Biome.Coast or Biome.Ice or Biome.Peak);

    [Fact]
    public void The_core_constant_is_the_centre_hex_and_ring_one()
    {
        BronzeFixture f = Town();
        Assert.Equal(1, f.Economy.SettlementCoreRadius);
        int site = f.Economy.FixtureSiteTile(0);
        Assert.True(f.Economy.IsReservedCoreTile(site));
        foreach (int t in Ring(site, 1)) Assert.True(f.Economy.IsReservedCoreTile(t));
        foreach (int t in Ring(site, 2)) Assert.False(f.Economy.IsReservedCoreTile(t));
    }

    [Fact]
    public void ReleaseCorePlots_frees_every_field_orchard_and_terrace_a_save_holds_in_a_core_and_keeps_the_rest()
    {
        BronzeFixture live = Town();
        LivingEconomyModel e = live.Economy;
        int site = e.FixtureSiteTile(0);
        int[] ring1 = Ring(site, 1).Where(Land).ToArray();
        int[] ring2 = Ring(site, 2).Where(t => Land(t) && t != live.TerraceTile).ToArray();
        Assert.True(ring1.Length >= 3 && ring2.Length >= 2, "the fixture needs land in rings 1 and 2");
        int fields0 = e.FixtureFieldCount(0), fieldStructures0 = e.FixtureFieldStructures(0);
        int orchards0 = e.FixtureOrchardCount(0), orchardStructures0 = e.FixtureOrchardStructures(0);

        // As an old save could hold them: a field on the hearth, an orchard, a terrace and a field in ring 1; a field and an orchard in ring 2.
        e.FixtureForcePlot(0, site, kind: 0);
        e.FixtureForcePlot(0, ring1[0], kind: 1);
        e.FixtureForcePlot(0, ring1[1], kind: 2);
        e.FixtureForcePlot(0, ring1[2], kind: 0);
        e.FixtureForcePlot(0, ring2[0], kind: 0);
        e.FixtureForcePlot(0, ring2[1], kind: 1);

        // The snapshot format is unchanged, so the old state loads as it is.
        BronzeFixture loaded = Town();
        Restore(loaded.Economy, Snapshot(e));
        LivingEconomyModel l = loaded.Economy;
        Assert.Equal(e.ComputeChecksum(), l.ComputeChecksum());
        Assert.Equal(fields0 + 4, l.FixtureFieldCount(0));
        Assert.Equal(1, l.FixtureTerraceCount(0));

        l.FixtureReleaseCorePlots();
        Assert.Equal(fields0 + 1, l.FixtureFieldCount(0));
        Assert.Equal(fieldStructures0 + 1, l.FixtureFieldStructures(0));
        Assert.Equal(orchards0 + 1, l.FixtureOrchardCount(0));
        Assert.Equal(orchardStructures0 + 1, l.FixtureOrchardStructures(0));
        Assert.Equal(0, l.FixtureTerraceCount(0));
        Assert.Equal(0, l.FixtureTerraceWorks(0));
        foreach (int t in new[] { site, ring1[1], ring1[2] }) Assert.Equal(-1, l.FixtureFieldOwner(t));
        Assert.Equal(0, l.FixtureFieldOwner(ring2[0]));
        Assert.Equal(ring2[1], l.FixtureOrchardTile(0, l.FixtureOrchardCount(0) - 1));

        // A second release changes nothing, and the snapshot of the cleaned state is valid.
        ulong clean = l.ComputeChecksum();
        l.FixtureReleaseCorePlots();
        Assert.Equal(clean, l.ComputeChecksum());
        BronzeFixture reloaded = Town();
        Restore(reloaded.Economy, Snapshot(l));
        Assert.Equal(clean, reloaded.Economy.ComputeChecksum());

        // In play the release runs by itself at the next season.
        live.RunSeasons(1);
        AssertNoPlotInAnyCore(e);
    }

    [Fact]
    public void The_planner_never_places_a_field_orchard_or_terrace_on_a_core_hex()
    {
        BronzeFixture f = Town();
        LivingEconomyModel e = f.Economy;
        int site = e.FixtureSiteTile(0);
        e.FixtureFillNaturalFields(0);
        Assert.True(e.FixtureFieldCount(0) > 0);
        while (e.FixturePlaceOrchard(0)) { }
        Assert.True(e.FixtureOrchardCount(0) > 0);
        int terrace = e.FixtureNextTerraceTile(0);
        Assert.True(terrace < 0 || HexGrid.Distance(site, terrace, Width) > 1);
        AssertNoPlotInAnyCore(e);

        // Every free tile left is in the core: the planner neither wants a field nor sees ground for one or for an orchard.
        Assert.False(e.FixtureCanPlaceOrchard(0));
        Assert.False(e.FixtureFieldWanted(0));
        Assert.False(e.CanPlaceAnotherField(0));
        Assert.Contains(Ring(site, 1).Append(site), t => Land(t) && e.FixtureFieldOwner(t) < 0);

        // The season's own planner, with labour and materials to spare, keeps the core clear too.
        f.RunSeasons(8);
        AssertNoPlotInAnyCore(e);
    }

    [Fact]
    public void A_neighbours_core_is_never_plot_land_while_it_lives_and_is_free_once_it_is_empty()
    {
        BronzeFixture f = Town();
        LivingEconomyModel e = f.Economy;
        int site = e.FixtureSiteTile(0);
        // Settlement 1 (another civilization's start) moves three hexes away, so its core covers part of settlement 0's range.
        int neighbour = Ring(site, 3).First(t => Land(t) && Ring(t, 1).Count(n => HexGrid.Distance(site, n, Width) <= 3 && Land(n)) >= 3);
        e.FixtureMoveSite(1, neighbour);
        Assert.True(e.Population(e.SettlementLite(1).Id) > 0);
        int[] shared = Ring(neighbour, 1).Append(neighbour).Where(t => HexGrid.Distance(site, t, Width) <= 3).ToArray();
        foreach (int t in shared) Assert.True(e.IsReservedCoreTile(t));

        e.FixtureFillNaturalFields(0);
        while (e.FixturePlaceOrchard(0)) { }
        foreach (int t in shared) Assert.Equal(-1, e.FixtureFieldOwner(t));
        for (int i = 0; i < e.FixtureOrchardCount(0); i++) Assert.DoesNotContain(e.FixtureOrchardTile(0, i), shared);
        AssertNoPlotInAnyCore(e);

        // An emptied settlement reserves nothing.
        e.RazeSettlement(1);
        foreach (int t in shared.Where(t => HexGrid.Distance(site, t, Width) > 1)) Assert.False(e.IsReservedCoreTile(t));
    }

    [Fact]
    public void A_mining_camp_is_refused_on_a_living_settlements_core()
    {
        BronzeFixture f = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 60, "MET-08", "MET-09");
        LivingEconomyModel e = f.Economy;
        Assert.True(f.CampTile >= 0);
        int[] ring1 = Ring(f.CampTile, 1).Where(Land).ToArray();
        // A rival village whose hearth stands next to the deposit: the deposit is in its core.
        e.FixtureMoveSite(1, ring1[0]);
        Assert.True(e.IsReservedCoreTile(f.CampTile));
        Assert.Equal(MiningCampResult.InSettlementCore, e.CheckOpenMiningCamp(0, f.CampTile));
    }

    /// <summary>
    /// Iron rules, four Warlord-level AIs, seed by seed: after every season start no field, orchard or terrace lies within one hex of a
    /// living settlement, except on the core of a settlement founded during that very season (the next season start releases it).
    /// </summary>
    [Theory]
    [InlineData(1UL, 60)]
    public void In_live_Iron_matches_no_plot_stands_in_a_living_settlements_core(ulong seed, int minutes)
        => AssertLiveCoresClear(seed, minutes);

    /// <summary>The WP3 exit gate (docs/MVP/31 §9): 4 seeds × Iron 110 minutes. Slow; runs when CIVSIM_SLOW=1.</summary>
    [Fact]
    public void Slow_four_Iron_matches_of_110_minutes_keep_every_core_clear()
    {
        if (Environment.GetEnvironmentVariable("CIVSIM_SLOW") != "1") return;
        Parallel.ForEach(new ulong[] { 1, 2, 3, 4 }, seed => AssertLiveCoresClear(seed, 110));
    }

    // ---- helpers ------------------------------------------------------------------------------------------

    private static readonly EconomyArchetype[] Archetypes =
        { EconomyArchetype.Builder, EconomyArchetype.Grower, EconomyArchetype.Warlord, EconomyArchetype.Sage };

    internal static (MatchModel, SimRunner) LiveMatch(ulong seed)
    {
        GeneratedWorld g = seed == Seed ? World.Value : WorldGenerator.Generate(seed);
        var controls = Archetypes.Select(a => CivControl.Ai(Difficulty.Warlord, a)).ToArray();
        MatchModel match = MatchModel.Create(seed, TestPack.Pack, g, Archetypes, controls, rules: ChapterRules.Iron);
        return (match, new SimRunner(match));
    }

    private static void AssertLiveCoresClear(ulong seed, int minutes)
    {
        (MatchModel match, SimRunner runner) = LiveMatch(seed);
        LivingEconomyModel e = match.Economy;
        GeneratedWorld world = WorldGenerator.Generate(seed);
        int width = world.Width;
        int plotsSeen = 0, checkedSites = 0;
        while (runner.Tick < minutes * 600L)
        {
            int before = e.SettlementCount;
            runner.Advance(LivingEconomyModel.SeasonTicks);
            plotsSeen += CheckCores(e, width, newSince: before, $"seed {seed} tick {runner.Tick}");
            // WP4: every settlement, the starts and Tin Folk included, stands on a clean core (no grandfathered ones in a new game).
            for (int i = checkedSites; i < e.SettlementCount; i++)
                Assert.True(CleanCore(world, e.Settlement(i).SiteTile),
                    $"seed {seed} tick {runner.Tick}: settlement {i} was founded with a metal, salt or coal deposit in its core");
            checkedSites = e.SettlementCount;
        }
        Assert.True(plotsSeen > 0, $"seed {seed}: the match placed no plot in {minutes} minutes, so it tests nothing");
        Assert.True(checkedSites > 4, $"seed {seed}: no settlement was founded after the start, so siting is not tested");
    }

    internal static bool CleanCore(GeneratedWorld world, int site)
    {
        for (int t = 0; t < world.TileCount; t++)
            if (HexGrid.Distance(site, t, world.Width) <= SettlementCoreRules.CoreRadius &&
                SettlementCoreRules.IsValuableCoreDeposit(world.TileAt(t).Deposit)) return false;
        return true;
    }

    /// <summary>Checks every plot against every living settlement's core; returns the plots seen.</summary>
    private static int CheckCores(LivingEconomyModel e, int width, int newSince, string where)
    {
        int n = e.SettlementCount;
        var sites = new int[n];
        var living = new bool[n];
        for (int i = 0; i < n; i++) { SettlementView v = e.Settlement(i); sites[i] = v.SiteTile; living[i] = v.Population > 0; }
        int seen = 0;
        void Check(int owner, int tile, string kind)
        {
            seen++;
            for (int i = 0; i < newSince && i < n; i++)
                if (living[i] && HexGrid.Distance(sites[i], tile, width) <= 1)
                    Assert.Fail($"{where}: a {kind} of settlement {owner} on tile {tile} lies in the core of living settlement {i}");
        }
        var terraces = new List<int>();
        for (int s = 0; s < n; s++)
        {
            terraces.Clear();
            e.IronTerraceTilesForView(s, terraces);
            for (int j = 0; j < e.FieldCount(s); j++)
            {
                int t = e.FieldTileAt(s, j);
                Check(s, t, terraces.Contains(t) ? "terrace" : "field");
            }
            for (int j = 0; j < e.OrchardCount(s); j++) Check(s, e.OrchardTileAt(s, j), "orchard");
        }
        return seen;
    }

    private static void AssertNoPlotInAnyCore(LivingEconomyModel e)
    {
        int n = e.SettlementCount;
        for (int s = 0; s < n; s++)
        {
            for (int j = 0; j < e.FieldCount(s); j++)
                Assert.False(e.IsReservedCoreTile(e.FieldTileAt(s, j)), $"field of settlement {s} on core tile {e.FieldTileAt(s, j)}");
            for (int j = 0; j < e.OrchardCount(s); j++)
                Assert.False(e.IsReservedCoreTile(e.OrchardTileAt(s, j)), $"orchard of settlement {s} on core tile {e.OrchardTileAt(s, j)}");
        }
    }

    private static byte[] Snapshot(LivingEconomyModel e)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) e.WriteSnapshot(writer);
        return stream.ToArray();
    }

    internal static byte[] Snapshot(MatchModel m)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) m.WriteSnapshot(writer);
        return stream.ToArray();
    }

    private static void Restore(LivingEconomyModel e, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);
        e.ReadSnapshot(reader);
    }
}

/// <summary>Determinism with the core rule live (its own class so xUnit runs it beside the live core check).</summary>
public sealed class SettlementCoreDeterminismTests
{
    [Fact]
    public void The_same_Iron_seed_gives_the_same_checksums_twice_and_resumes_from_a_snapshot()
    {
        (MatchModel a, SimRunner ra) = SettlementCoreTests.LiveMatch(3);
        (MatchModel b, SimRunner rb) = SettlementCoreTests.LiveMatch(3);
        long ticks = 50L * 600;
        ra.Advance(ticks / 2);
        byte[] half = SettlementCoreTests.Snapshot(a);
        ra.Advance(ticks - ticks / 2);
        rb.Advance(ticks);
        Assert.Equal(ra.Checksums, rb.Checksums);
        Assert.Equal(ra.CurrentChecksum, rb.CurrentChecksum);

        (MatchModel c, _) = SettlementCoreTests.LiveMatch(3);
        using (var stream = new MemoryStream(half))
        using (var reader = new BinaryReader(stream)) c.ReadSnapshot(reader);
        var rc = new SimRunner(c, startTick: ticks / 2);
        rc.Advance(ticks - ticks / 2);
        Assert.Equal(ra.CurrentChecksum, rc.CurrentChecksum);
    }
}

/// <summary>Clean-core siting (docs/MVP/31 §3.3 siting rows, §7, WP4): starts, refusal text and the Found and Beacon checks.</summary>
public sealed class SettlementSitingTests
{
    private const ulong Seed = 20261019;

    private static void AssertStartCoresClean(int first, int count)
    {
        for (int seed = first; seed < first + count; seed++)
        {
            GeneratedWorld g = WorldGenerator.Generate((ulong)seed);
            foreach (WorldStart start in g.Starts)
                Assert.True(SettlementCoreTests.CleanCore(g, start.Tile), $"seed {seed}: a start on tile {start.Tile} has a valuable deposit in its core");
        }
    }

    [Fact]
    public void World_generation_keeps_every_start_core_clean() => AssertStartCoresClean(1, 24);

    /// <summary>The WP4 gate on 200 seeds (about 10 s). Slow; runs when CIVSIM_SLOW=1.</summary>
    [Fact]
    public void Slow_two_hundred_worlds_keep_every_start_core_clean()
    {
        if (Environment.GetEnvironmentVariable("CIVSIM_SLOW") != "1") return;
        AssertStartCoresClean(1, 200);
    }

    [Fact]
    public void Start_wild_grain_repairs_never_seed_the_core()
    {
        // The wild-grain repair now seeds ring 2 and 3, so a start whose core holds wild grain got it from the natural scatter only.
        for (int seed = 1; seed <= 24; seed++)
        {
            GeneratedWorld g = WorldGenerator.Generate((ulong)seed);
            foreach (WorldStart start in g.Starts)
            {
                int grain = 0;
                for (int t = 0; t < g.TileCount; t++)
                    if ((g.TileAt(t).Flags & TileFlags.WildGrain) != 0 && HexGrid.Distance(start.Tile, t, g.Width) <= 10) grain++;
                Assert.True(grain >= 4, $"seed {seed}: start {start.Tile} has {grain} wild-grain tiles within 10");
            }
        }
    }

    [Fact]
    public void Found_and_the_Beacon_refuse_a_site_over_a_deposit_and_the_text_names_it_only_when_known()
    {
        GeneratedWorld g = WorldGenerator.Generate(Seed);
        MatchModel match = MatchModel.Create(Seed, TestPack.Pack, g);
        LivingEconomyModel e = match.Economy;
        int site = -1;
        for (int t = 0; t < g.TileCount && site < 0; t++)
        {
            if (!e.IsHabitableSite(t) || e.IsCleanCoreSite(t)) continue;
            bool far = true;
            for (int s = 0; s < e.SettlementCount; s++) if (HexGrid.Distance(e.Settlement(s).SiteTile, t, g.Width) < 12) far = false;
            if (far && match.Politics.Territory.OwnerOf(t) < 0) site = t;
        }
        Assert.True(site >= 0, "the world needs a habitable tile over a deposit away from the starts");
        int deposit = e.CoreDepositTile(site);
        Assert.True(deposit >= 0 && HexGrid.Distance(site, deposit, g.Width) <= 1);

        Assert.Equal(OrderRefusalReason.CoreDeposit, match.Politics.FoundCheck(0, site, out _));
        Assert.False(match.Politics.Allows(0, site));
        Assert.False(e.PreviewBeaconSite(0, site).CanFound);

        WorldView view = WorldView.FromGenerated(g);
        var known = new byte[g.TileCount];
        string hidden = SiteText.CoreDepositRefusal(view, known, site);
        Assert.Contains("poor for a village", hidden);
        known[deposit] = 1;
        string named = SiteText.CoreDepositRefusal(view, known, site);
        Assert.Contains("known", named);
        string kind = g.TileAt(deposit).Deposit switch
        {
            DepositKind.NativeCopper => "native copper", DepositKind.CopperOre => "copper ore", DepositKind.ArsenicalCopper => "arsenical copper",
            DepositKind k => k.ToString().ToLowerInvariant(),
        };
        Assert.Contains(kind, named);
    }
}
