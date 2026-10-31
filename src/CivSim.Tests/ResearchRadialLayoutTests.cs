using System;
using CivSim.Game.Ui;
using CivSim.Sim.Research;

namespace CivSim.Tests;

/// <summary>The research Map's radial layout (game/Scripts/Ui/ResearchRadialLayout.cs) over the real catalog.</summary>
public class ResearchRadialLayoutTests
{
    private static readonly ResearchRadialLayout Layout = ResearchRadialLayout.Build(TestPack.Catalog);

    [Fact]
    public void Every_numeric_project_is_placed_and_ages_grow_outward()
    {
        ResearchCatalog catalog = TestPack.Catalog;
        for (int p = 0; p < catalog.Count; p++)
            Assert.Equal(catalog.Projects[p].Numeric, Layout.Placed[p]);
        Assert.NotEmpty(Layout.Rings);
        Assert.Equal(0, Layout.Rings[0]);
        for (int i = 1; i < Layout.Rings.Length; i++)
            Assert.True(Layout.RingInner[Layout.Rings[i]] > Layout.RingOuter[Layout.Rings[i - 1]]);
        for (int p = 0; p < catalog.Count; p++)
        {
            if (!Layout.Placed[p] || Layout.Gate[p]) continue;
            int era = Layout.Era[p];
            Assert.InRange(Layout.Radius[p], Layout.RingInner[era], Layout.RingOuter[era]);
        }
    }

    [Fact]
    public void Every_prerequisite_edge_points_outward()
    {
        Assert.NotEmpty(Layout.Edges);
        foreach (ResearchRadialLayout.Edge edge in Layout.Edges)
            Assert.True(Layout.Radius[edge.To] > Layout.Radius[edge.From],
                $"{TestPack.Catalog.Projects[edge.From].Id} -> {TestPack.Catalog.Projects[edge.To].Id} does not point outward");
    }

    [Fact]
    public void Projects_sit_inside_their_track_sector_and_field_wedge()
    {
        ResearchCatalog catalog = TestPack.Catalog;
        for (int p = 0; p < catalog.Count; p++)
        {
            if (!Layout.Placed[p] || Layout.Gate[p]) continue;
            ProjectDef def = catalog.Projects[p];
            ResearchRadialLayout.Wedge wedge = Array.Find(Layout.Wedges, w => w.Lineage == def.Lineage);
            ResearchRadialLayout.Sector sector = Layout.Sectors[(int)def.Theme];
            double angle = ResearchRadialLayout.Unwrap(Layout.Angle[p], (wedge.Start + wedge.End) * 0.5);
            Assert.InRange(angle, wedge.Start, wedge.End);
            Assert.InRange(angle, sector.Start, sector.End);
            Assert.Equal((int)def.Theme, wedge.Theme);
        }
    }

    [Fact]
    public void No_two_projects_overlap()
    {
        float closest = float.MaxValue;
        for (int a = 0; a < Layout.Count; a++)
        {
            if (!Layout.Placed[a]) continue;
            for (int b = a + 1; b < Layout.Count; b++)
            {
                if (!Layout.Placed[b]) continue;
                float dx = Layout.X[a] - Layout.X[b], dy = Layout.Y[a] - Layout.Y[b];
                closest = Math.Min(closest, MathF.Sqrt(dx * dx + dy * dy));
            }
        }
        Assert.True(closest >= 20f, $"closest pair is {closest:0.0} units apart");
    }

    [Fact]
    public void Turning_points_sit_in_the_gap_after_their_age()
    {
        ResearchCatalog catalog = TestPack.Catalog;
        int gates = 0;
        for (int p = 0; p < catalog.Count; p++)
        {
            if (!Layout.Gate[p]) continue;
            gates++;
            int era = catalog.Projects[p].Era;
            Assert.True(Layout.Radius[p] > Layout.RingOuter[era]);
        }
        Assert.True(gates >= 3);
    }

    [Fact]
    public void The_layout_is_stable_between_builds()
    {
        ResearchRadialLayout again = ResearchRadialLayout.Build(TestPack.Catalog);
        Assert.Equal(Layout.X, again.X);
        Assert.Equal(Layout.Y, again.Y);
        Assert.Equal(Layout.Edges.Length, again.Edges.Length);
    }
}
