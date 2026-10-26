using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.World;

namespace CivSim.Tests;

public class TerritoryTests
{
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(20261019));
    private static GeneratedWorld W => World.Value;
    private static readonly List<ClaimSource> None = new();

    private static Territory NewTerritory() => new(W, 4);
    private static int LandNeighbor(int tile)
    {
        Span<int> n = stackalloc int[6];
        int count = HexGrid.Neighbors(tile, W.Width, W.Height, n);
        for (int i = 0; i < count; i++) if (W.TileAt(n[i]).IsLand) return n[i];
        throw new InvalidOperationException("No land neighbour.");
    }

    private static int Start(int civ) => W.Starts[civ].Tile;

    private static List<ClaimSource> One(int civ, int tile, string emission, int radius = 3)
        => new() { new ClaimSource(civ, tile, radius, Fix64.Parse(emission)) };

    private static void Run(Territory t, List<ClaimSource> settlements, List<ClaimSource>? camps = null, int seasons = 2)
    {
        for (int i = 0; i < seasons; i++) t.Update(settlements, camps ?? None);
    }

    [Fact]
    public void A_tile_changes_hands_only_after_its_claimant_leads_for_a_full_season()
    {
        Territory t = NewTerritory();
        List<ClaimSource> a = One(0, Start(0), "6");
        t.Update(a, None);
        Assert.Equal(-1, t.OwnerOf(Start(0)));          // first season: pending
        Assert.True(t.ClaimOf(0, Start(0)) >= Territory.OwnThresholdMilli);
        t.Update(a, None);
        Assert.Equal(0, t.OwnerOf(Start(0)));           // second consecutive season: owned
        Assert.True(t.OwnedTiles(0) > 10);
    }

    [Fact]
    public void Claim_falls_off_linearly_to_the_edge_of_the_radius_and_needs_seven_tenths()
    {
        Territory t = NewTerritory();
        // Emission 1.0 radius 3: claim at distance d is 1 x (1 - d/4); only the centre (1.0) and d=1 (0.75) pass T = 0.7.
        Run(t, One(0, Start(0), "1"));
        int center = Start(0);
        Assert.Equal(1000, t.ClaimOf(0, center));
        int owned = 0;
        for (int tile = 0; tile < t.TileCount; tile++)
        {
            int d = HexGrid.Distance(center, tile, t.Width);
            if (d == 1) Assert.Equal(750, t.ClaimOf(0, tile));
            if (d == 2) Assert.Equal(500, t.ClaimOf(0, tile));
            if (d == 3) Assert.Equal(250, t.ClaimOf(0, tile));
            if (d > 3) Assert.Equal(0, t.ClaimOf(0, tile));
            if (t.OwnerOf(tile) == 0) { owned++; Assert.True(d <= 1); }
        }
        Assert.InRange(owned, 1, 7);
        // A weak claim of 0.6 owns nothing.
        Territory weak = NewTerritory();
        Run(weak, One(0, Start(0), "0.6"));
        Assert.Equal(0, weak.OwnedTiles(0));
    }

    [Fact]
    public void A_rival_needs_a_lead_of_twenty_percent_to_own_a_contested_tile()
    {
        int tile = Start(0);
        // Find a neighbouring land tile to hold the rival's source.
        int other = LandNeighbor(tile);

        // At the first source's own tile civilization 0 claims 4.0 and civilization 1 (one tile away) 4.5 x 0.75 = 3.4: a 1.18 lead.
        var equal = new List<ClaimSource> { new(0, tile, 3, Fix64.FromInt(4)), new(1, other, 3, Fix64.Parse("4.5")) };
        Territory contested = NewTerritory();
        Run(contested, equal, seasons: 4);
        Assert.Equal(-1, contested.OwnerOf(tile));      // both claim well over T, neither leads by 20%

        var clear = new List<ClaimSource> { new(0, tile, 3, Fix64.FromInt(5)), new(1, other, 3, Fix64.FromInt(3)) };
        Territory decided = NewTerritory();
        Run(decided, clear, seasons: 3);
        Assert.Equal(0, decided.OwnerOf(tile));
    }

    [Fact]
    public void Borders_do_not_flicker_a_stronger_challenger_waits_a_season_and_the_incumbent_keeps_the_ground_meanwhile()
    {
        int tile = Start(0);
        int other = LandNeighbor(tile);
        Territory t = NewTerritory();
        Run(t, new List<ClaimSource> { new(0, tile, 3, Fix64.FromInt(4)) }, seasons: 3);
        Assert.Equal(0, t.OwnerOf(tile));
        var challenger = new List<ClaimSource> { new(0, tile, 3, Fix64.FromInt(2)), new(1, other, 3, Fix64.FromInt(6)) };
        t.Update(challenger, None);
        Assert.Equal(0, t.OwnerOf(tile));               // one season of leading is not enough
        t.Update(challenger, None);
        Assert.Equal(1, t.OwnerOf(tile));               // the second flips it
        // A challenger that gives up after one season changes nothing.
        Territory flicker = NewTerritory();
        Run(flicker, new List<ClaimSource> { new(0, tile, 3, Fix64.FromInt(4)) }, seasons: 3);
        flicker.Update(challenger, None);
        flicker.Update(new List<ClaimSource> { new(0, tile, 3, Fix64.FromInt(4)) }, None);
        flicker.Update(challenger, None);
        Assert.Equal(0, flicker.OwnerOf(tile));
    }

    [Fact]
    public void A_camp_on_rival_land_claims_nothing_and_a_camp_on_free_land_does()
    {
        Territory t = NewTerritory();
        var settlements = new List<ClaimSource> { new(0, Start(0), 3, Fix64.FromInt(6)) };
        Run(t, settlements);
        int center = Start(0);
        Assert.Equal(0, t.OwnerOf(center));
        var intruder = new List<ClaimSource> { new(1, center, Territory.CampRadius, Fix64.FromInt(3)) };
        t.Update(settlements, intruder);
        Assert.Equal(0, t.ClaimOf(1, center));          // the camp sits on civ 0's land and is ignored
        // Elsewhere the same camp works: put one far from everybody.
        int far = Start(3);
        t.Update(settlements, new List<ClaimSource> { new(1, far, Territory.CampRadius, Fix64.FromInt(3)) });
        Assert.True(t.ClaimOf(1, far) > 0);
    }

    [Fact]
    public void Claim_projection_adds_camp_sources_without_mutating_live_ownership()
    {
        int home = Start(0);
        int campTile = Enumerable.Range(0, W.Tiles.Length)
            .First(tile => HexGrid.Distance(home, tile, W.Width) == 5 && W.TileAt(tile).HabitableWeight > 0);
        var settlements = One(0, home, "6");
        var camps = new List<ClaimSource> { new(0, campTile, 2, Fix64.FromInt(8)) };
        Territory territory = NewTerritory();
        territory.Update(settlements, None);
        int liveClaim = territory.ClaimOf(0, home);
        int liveOwner = territory.OwnerOf(home);
        int liveClaimedWeight = territory.ClaimedBasisPoints();

        int settlementsOnly = territory.ProjectedClaimedWeight(settlements, None);
        int withCamp = territory.ProjectedClaimedWeight(settlements, camps);

        Assert.True(withCamp > settlementsOnly);
        Assert.Equal(liveClaim, territory.ClaimOf(0, home));
        Assert.Equal(liveOwner, territory.OwnerOf(home));
        Assert.Equal(liveClaimedWeight, territory.ClaimedBasisPoints());
    }

    [Fact]
    public void Claim_projection_reports_land_and_frontier_cost_from_the_same_stateless_projection()
    {
        int first = Start(0);
        int second = LandNeighbor(first);
        var settlements = new List<ClaimSource>
        {
            new(0, first, 0, Fix64.FromInt(6)),
            new(1, second, 0, Fix64.FromInt(6)),
        };
        Territory territory = NewTerritory();

        var projection = territory.ProjectedClaimedWeightAndFrontierCost(settlements, None, 0, 1);

        Assert.True(projection.ClaimedWeight > 0);
        Assert.Equal(0, projection.FrontierConnectionWeight);
        Assert.Empty(projection.FrontierUnownedPath);
        Assert.Equal(projection.ClaimedWeight, territory.ProjectedClaimedWeight(settlements, None));
        Assert.Equal(0, territory.ClaimedBasisPoints());
        Assert.Equal(-1, territory.OwnerOf(first));
    }

    [Fact]
    public void Claim_projection_returns_the_unowned_tiles_that_make_up_its_frontier_cost()
    {
        int start = Start(0), end = -1;
        var distance = new int[W.Tiles.Length];
        Array.Fill(distance, -1);
        var queue = new Queue<int>();
        distance[start] = 0;
        queue.Enqueue(start);
        Span<int> neighbors = stackalloc int[6];
        while (queue.Count > 0 && end < 0)
        {
            int tile = queue.Dequeue();
            if (distance[tile] >= 2) { end = tile; break; }
            int count = HexGrid.Neighbors(tile, W.Width, W.Height, neighbors);
            for (int i = 0; i < count; i++)
            {
                int next = neighbors[i];
                if (!W.TileAt(next).IsLand || distance[next] >= 0) continue;
                distance[next] = distance[tile] + 1;
                queue.Enqueue(next);
            }
        }
        Assert.True(end >= 0, "Expected a land path at least two tiles long from the start.");
        var settlements = new List<ClaimSource>
        {
            new(0, start, 0, Fix64.FromInt(6)),
            new(1, end, 0, Fix64.FromInt(6)),
        };

        var projection = NewTerritory().ProjectedClaimedWeightAndFrontierCost(settlements, None, 0, 1);

        Assert.True(projection.FrontierConnectionWeight > 0);
        Assert.NotEmpty(projection.FrontierUnownedPath);
        Assert.Equal(projection.FrontierConnectionWeight,
            projection.FrontierUnownedPath.Sum(tile => W.TileAt(tile).HabitableWeight));
    }

    [Fact]
    public void Dominion_hands_out_a_hundred_points_a_year_by_share_of_the_claimed_land()
    {
        Territory t = NewTerritory();
        var sources = new List<ClaimSource> { new(0, Start(0), 3, Fix64.FromInt(8)), new(1, Start(1), 3, Fix64.FromInt(4)) };
        Run(t, sources, seasons: 3);
        Assert.True(t.OwnedWeight(0) > 0 && t.OwnedWeight(1) > 0);
        t.AccrueDominion();
        double total = 0;
        for (int c = 0; c < 4; c++) total += t.Dominion(c).ToDoubleForDisplay();
        Assert.InRange(total, 99.99, 100.01);
        // Points follow owned habitable weight.
        double ratio = t.Dominion(0).ToDoubleForDisplay() / t.Dominion(1).ToDoubleForDisplay();
        Assert.InRange(ratio, (double)t.OwnedWeight(0) / t.OwnedWeight(1) * 0.999, (double)t.OwnedWeight(0) / t.OwnedWeight(1) * 1.001);
        Assert.Equal(Fix64.Zero, t.Dominion(2));
        int bp = t.ClaimedBasisPoints();
        Assert.InRange(bp, 1, 10_000);
        Assert.InRange(t.ShareBasisPoints(0) + t.ShareBasisPoints(1) + t.ShareBasisPoints(2) + t.ShareBasisPoints(3), bp - 2, bp + 2);
    }

    [Fact]
    public void Camp_sites_lie_one_to_three_tiles_beyond_the_claim_radius_on_the_same_landmass()
    {
        Territory t = NewTerritory();
        int home = Start(0);
        var own = new List<ClaimSource> { new(0, home, 3, Fix64.FromInt(6)) };
        List<int> sites = t.ChooseCampSites(0, home, 3, own, None, None, 2);
        Assert.Equal(2, sites.Count);
        foreach (int site in sites)
        {
            int d = HexGrid.Distance(home, site, W.Width);
            Assert.InRange(d, 4, 6);
            Assert.Equal(W.TileAt(home).Continent, W.TileAt(site).Continent);
            Assert.True(W.TileAt(site).HabitableWeight > 0);
        }
        Assert.NotEqual(sites[0], sites[1]);
        // The same inputs give the same sites.
        Assert.Equal(sites, t.ChooseCampSites(0, home, 3, own, None, None, 2));
        // Sites near a rival settlement are refused.
        var rival = new List<ClaimSource> { new(1, home, 3, Fix64.One) };
        foreach (int site in t.ChooseCampSites(0, home, 3, own, None, rival, 2))
            Assert.True(HexGrid.Distance(site, home, W.Width) >= 6);
    }

    [Fact]
    public void Compact_camp_counterfactual_keeps_legal_sites_distinct_and_fills_zero_gain_slots()
    {
        Territory t = NewTerritory();
        int home = Start(0);
        // The broad owned disk covers every eligible camp disk, so all legal sites have zero marginal gain.
        var own = new List<ClaimSource> { new(0, home, 9, Fix64.FromInt(6)) };
        int claimedBefore = t.ClaimedBasisPoints();

        List<int> sites = t.ChooseCompactCampSites(0, home, 3, own, None, None, 2);

        Assert.Equal(2, sites.Count);
        Assert.NotEqual(sites[0], sites[1]);
        Assert.All(sites, site =>
        {
            Assert.True(W.TileAt(site).HabitableWeight > 0);
            Assert.Equal(W.TileAt(home).Continent, W.TileAt(site).Continent);
            Assert.InRange(HexGrid.Distance(home, site, W.Width), 4, 6);
            Assert.Equal(0, t.CampGain(site, 0, CoveredBy(own, t)));
        });
        Assert.Equal(sites, t.ChooseCompactCampSites(0, home, 3, own, None, None, 2));
        Assert.Equal(claimedBefore, t.ClaimedBasisPoints());
    }

    [Fact]
    public void Camp_path_support_reports_only_a_camp_needed_to_keep_a_pair_owned_tile()
    {
        Territory t = NewTerritory();
        int tile = Start(0);
        var camps = new List<ClaimSource>
        {
            new(0, tile, Territory.CampRadius, Fix64.One),
            new(1, Start(1), Territory.CampRadius, Fix64.One),
        };
        int claimedBefore = t.ClaimedBasisPoints();

        bool[] support = t.ProjectedCampSupportsPairPath(None, camps, new[] { tile }, 0, 1);

        Assert.Equal(new[] { true, false }, support);
        Assert.Equal(claimedBefore, t.ClaimedBasisPoints());
    }

    [Fact]
    public void Camp_move_local_projection_delta_matches_whole_map_projection()
    {
        Territory t = NewTerritory();
        int home = Start(0);
        var settlements = new List<ClaimSource>
        {
            new(0, home, 3, Fix64.FromInt(2)),
            new(1, Start(1), 3, Fix64.FromInt(2)),
        };
        int[] sites = Enumerable.Range(0, t.TileCount)
            .Where(tile => t.IsCampSiteLegal(0, home, tile, 3, None))
            .Take(2)
            .ToArray();
        Assert.Equal(2, sites.Length);
        var camps = new List<ClaimSource>
        {
            new(0, sites[0], Territory.CampRadius, Fix64.One),
            new(1, Start(1), Territory.CampRadius, Fix64.One),
        };
        int claimedBefore = t.ProjectedClaimedWeight(settlements, camps);
        var movedCamps = new List<ClaimSource>(camps) { [0] = camps[0] with { Tile = sites[1] } };
        int claimedAfter = t.ProjectedClaimedWeight(settlements, movedCamps);
        int liveClaimedBefore = t.ClaimedBasisPoints();

        int delta = t.ProjectedCampMoveClaimedWeightDelta(settlements, camps, 0, sites[1]);

        Assert.Equal(claimedAfter - claimedBefore, delta);
        Assert.Equal(liveClaimedBefore, t.ClaimedBasisPoints());
    }

    [Fact]
    public void Paired_camp_move_local_delta_matches_whole_map_projection()
    {
        Territory t = NewTerritory();
        var settlements = new List<ClaimSource>
        {
            new(0, Start(0), 3, Fix64.FromInt(2)),
            new(1, Start(1), 3, Fix64.FromInt(2)),
        };
        var camps = new List<ClaimSource>
        {
            new(0, Start(0), Territory.CampRadius, Fix64.One),
            new(1, Start(1), Territory.CampRadius, Fix64.One),
        };
        int firstNew = LandNeighbor(Start(0));
        int secondNew = LandNeighbor(Start(1));
        int claimedBefore = t.ProjectedClaimedWeight(settlements, camps);
        var movedCamps = new List<ClaimSource>(camps)
        {
            [0] = camps[0] with { Tile = firstNew },
            [1] = camps[1] with { Tile = secondNew },
        };
        int claimedAfter = t.ProjectedClaimedWeight(settlements, movedCamps);

        int delta = t.ProjectedCampMovesClaimedWeightDelta(settlements, camps, 0, firstNew, 1, secondNew);

        Assert.Equal(claimedAfter - claimedBefore, delta);
    }

    private static bool[] CoveredBy(IReadOnlyList<ClaimSource> sources, Territory territory)
    {
        var covered = new bool[territory.TileCount];
        foreach (ClaimSource source in sources) territory.MarkDisk(covered, source.Tile, source.Radius);
        return covered;
    }

}
