using System;
using System.Reflection;
using CivSim.Sim.Core;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Worn trails (docs/MVP/19): routes between villages, drawn from tiles the viewer has explored and nothing else.</summary>
public class ViewTrailTests
{
    [Fact]
    public void Trail_path_with_identical_endpoints_is_rejected()
    {
        MatchModel match = ViewKit.NewMatch();
        var builder = new FrameBuilder(match, -1);
        Type linkType = typeof(FrameBuilder).GetNestedType("TrailLink", BindingFlags.NonPublic)!;
        object link = Activator.CreateInstance(linkType, nonPublic: true)!;
        MethodInfo tracePath = typeof(FrameBuilder).GetMethod("TracePath", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int tile = match.Economy.SettlementLite(0).SiteTile;

        Assert.False((bool)tracePath.Invoke(builder, new[] { (object)tile, tile, link })!);
        Assert.Equal(0, (int)linkType.GetField("TileCount")!.GetValue(link)!);
    }

    [Fact]
    public void Trails_follow_the_map_and_never_reach_unexplored_ground()
    {
        WatchedMatch watched = ViewKit.Watched.Value;
        MatchFrame omni = watched.OmniFrame, mine = watched.ViewerFrame;
        Assert.True(omni.TrailCount > 0, "a 30-minute match should have at least one village-to-village trail");
        int width = omni.Width, height = omni.Height;
        Span<int> neighbors = stackalloc int[6];
        for (int i = 0; i < omni.TrailCount; i++)
        {
            Assert.InRange((int)omni.TrailLevel[i], 1, 3);
            int previous = -1;
            for (int j = 0; j < omni.TrailLength[i]; j++)
            {
                int tile = omni.TrailTiles[omni.TrailStart[i] + j];
                if (tile >= 0 && previous >= 0)
                {
                    int count = HexGrid.Neighbors(previous, width, height, neighbors);
                    bool adjacent = false;
                    for (int n = 0; n < count; n++) adjacent |= neighbors[n] == tile;
                    Assert.True(adjacent, $"trail {i}: tiles {previous} and {tile} are not neighbours");
                }
                previous = tile;
            }
        }
        for (int i = 0; i < mine.TrailCount; i++)
            for (int j = 0; j < mine.TrailLength[i]; j++)
            {
                int tile = mine.TrailTiles[mine.TrailStart[i] + j];
                if (tile >= 0) Assert.True(mine.Visibility[tile] > 0, $"trail {i} runs over unexplored tile {tile}");
            }
    }
}
