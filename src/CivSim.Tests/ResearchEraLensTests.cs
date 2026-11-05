using System;
using System.Collections.Generic;
using CivSim.Game.Ui;

namespace CivSim.Tests;

/// <summary>The research Map's era lens (game/Scripts/Ui/ResearchEraLens.cs) over the real catalog: docs/MVP/research-tree-era-focus-design.md §5.</summary>
public class ResearchEraLensTests
{
    private static readonly ResearchRadialLayout Layout = ResearchRadialLayout.Build(TestPack.Catalog);

    private static IEnumerable<float> FocusGrid(ResearchEraLens lens, float step = 0.05f)
    {
        int steps = (int)Math.Round(lens.MaxFocus / step);
        for (int i = 0; i <= steps; i++) yield return Math.Min(lens.MaxFocus, i * step);
    }

    [Fact]
    public void Map_is_strictly_increasing_and_continuous()
    {
        var lens = new ResearchEraLens(Layout);
        float top = Layout.OuterRadius + 100;
        foreach (float f in FocusGrid(lens))
        {
            lens.SetFocus(f);
            float previous = lens.Map(0);
            for (float r = 1; r <= top; r += 1)
            {
                float m = lens.Map(r);
                Assert.True(m > previous, $"f={f:0.00}: Map({r}) = {m} is not above Map({r - 1}) = {previous}");
                // slope is at most 1 (full lanes and gaps are the layout's own spacing; the hub stretches by hub / CenterRadius)
                float slope = Math.Max(Math.Max(1f, lens.Hub / ResearchRadialLayout.CenterRadius), ResearchEraLens.FocusLane / ResearchEraLens.FullLane);
                Assert.True(m - previous <= slope + 0.01f, $"f={f:0.00}: Map jumps at {r}");
                previous = m;
            }
        }
    }

    [Fact]
    public void The_focused_ring_sits_at_FocusInner_at_full_thickness()
    {
        var lens = new ResearchEraLens(Layout);
        Assert.Equal(Layout.Rings.Length, lens.RingCount);
        for (int i = 0; i < lens.RingCount; i++)
        {
            lens.SetFocus(i);
            int era = Layout.Rings[i];
            Assert.Equal(1f, lens.Weight(era));
            // pinned at FocusInner, unless the older ages' bands already fill that radius and the hub sits at its floor (then the ring starts a little further out)
            if (lens.Hub > ResearchEraLens.HubMin + 0.01f) Assert.Equal(lens.FocusInner, lens.Inner(era), 3);
            else Assert.True(lens.Inner(era) >= lens.FocusInner - 0.01f);
            Assert.Equal(Layout.Lanes[era] * ResearchEraLens.FocusLane, lens.Outer(era) - lens.Inner(era), 3);
            Assert.Equal(lens.Inner(era), lens.Map(Layout.RingInner[era]), 2);
        }
    }

    [Fact]
    public void Lane_zero_neighbours_of_the_focused_ring_are_at_least_28_apart()
    {
        var lens = new ResearchEraLens(Layout);
        for (int i = 0; i < lens.RingCount; i++)
        {
            lens.SetFocus(i);
            float laneZero = lens.Inner(Layout.Rings[i]) + ResearchEraLens.FocusLane * 0.5f;
            float spacing = laneZero * Layout.SlotAngle;
            Assert.True(spacing >= 28f, $"{Layout.Rings[i]}: lane-0 neighbours are {spacing:0.0} apart");
        }
    }

    [Fact]
    public void A_small_change_in_focus_moves_no_project_far()
    {
        var lens = new ResearchEraLens(Layout);
        int n = Layout.Count;
        float[] x0 = new float[n], y0 = new float[n], x1 = new float[n], y1 = new float[n];
        float worst = 0;
        for (float f = 0; f + 0.01f <= lens.MaxFocus + 1e-4f; f += 0.01f)
        {
            lens.SetFocus(f);
            lens.Positions(x0, y0);
            lens.SetFocus(f + 0.01f);
            lens.Positions(x1, y1);
            for (int p = 0; p < n; p++)
            {
                if (!Layout.Placed[p]) continue;
                float dx = x1[p] - x0[p], dy = y1[p] - y0[p];
                worst = Math.Max(worst, MathF.Sqrt(dx * dx + dy * dy));
            }
        }
        Assert.True(worst <= 8f, $"a project moved {worst:0.00} units for a focus change of 0.01");
    }

    [Fact]
    public void Projects_stay_in_their_age_band_and_gates_in_their_gap()
    {
        var lens = new ResearchEraLens(Layout);
        foreach (float f in FocusGrid(lens))
        {
            lens.SetFocus(f);
            for (int p = 0; p < Layout.Count; p++)
            {
                if (!Layout.Placed[p]) continue;
                int era = Layout.Era[p];
                int ring = lens.RingOf(era);
                float r = lens.Map(Layout.Radius[p]);
                if (Layout.Gate[p])
                {
                    if (ring < 0) continue;
                    Assert.InRange(r, lens.RingEnd[ring], lens.GapEnd[ring]);
                }
                else Assert.InRange(r, lens.RingStart[ring], lens.RingEnd[ring]);
            }
        }
    }

    [Fact]
    public void Ten_ages_keep_the_hub_open_and_the_disc_bounded()
    {
        // today's five ages and the expected lanes of the five drafted ones (doc §1)
        var lens = new ResearchEraLens(new[] { 5, 5, 7, 6, 5, 3, 5, 4, 7, 7 }, Layout.SlotAngle);
        foreach (float f in FocusGrid(lens))
        {
            lens.SetFocus(f);
            Assert.True(lens.Hub >= ResearchEraLens.HubMin, $"f={f:0.00}: hub {lens.Hub:0}");
            Assert.True(lens.OuterRadius <= 1600f, $"f={f:0.00}: outer radius {lens.OuterRadius:0}");
        }
    }

    [Fact]
    public void Every_edge_still_points_outward_after_lensing()
    {
        var lens = new ResearchEraLens(Layout);
        foreach (float f in FocusGrid(lens, 0.25f))
        {
            lens.SetFocus(f);
            foreach (ResearchRadialLayout.Edge edge in Layout.Edges)
                Assert.True(lens.Map(Layout.Radius[edge.To]) > lens.Map(Layout.Radius[edge.From]),
                    $"f={f:0.00}: {TestPack.Catalog.Projects[edge.From].Id} -> {TestPack.Catalog.Projects[edge.To].Id} does not point outward");
        }
    }

    [Fact]
    public void Unmap_inverts_Map()
    {
        var lens = new ResearchEraLens(Layout);
        foreach (float f in FocusGrid(lens, 0.1f))
        {
            lens.SetFocus(f);
            for (float r = 0; r <= Layout.OuterRadius + 200; r += 3.7f)
            {
                float back = lens.Unmap(lens.Map(r));
                Assert.True(Math.Abs(back - r) < 0.02f, $"f={f:0.00}: Unmap(Map({r})) = {back}");
            }
        }
    }

    [Fact]
    public void Layout_ring_index_and_max_lanes_match_the_rings()
    {
        for (int i = 0; i < Layout.Rings.Length; i++) Assert.Equal(i, Layout.RingIndex[Layout.Rings[i]]);
        int max = 0;
        foreach (int era in Layout.Rings) max = Math.Max(max, Layout.Lanes[era]);
        Assert.Equal(max, Layout.MaxLanes);
    }
}
