using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using CivSim.Sim.World;
using Xunit;

namespace CivSim.Tests;

public sealed class FiefPortolanVisibilityTests
{
    [Fact]
    public void Way09_explores_adjacent_water_from_known_shore_without_revealing_more_land()
    {
        MatchModel match = MatchModel.Create(ViewKit.Seed, TestPack.WithNumeric("WAY-09"), ViewKit.World.Value,
            ViewKit.Balanced, default, rules: ChapterRules.Fief);
        var runner = new SimRunner(match);
        var builder = new FrameBuilder(match, 0);
        var frame = new MatchFrame();
        builder.Build(match, frame);

        Span<int> neighbors = stackalloc int[6];
        var coasts = new List<(int Shoreline, int Water)>();
        for (int tile = 0; tile < ViewKit.World.Value.TileCount; tile++)
        {
            if (frame.Visibility[tile] != (byte)TileVisibility.Unknown || !ViewKit.World.Value.TileAt(tile).IsLand) continue;
            int count = HexGrid.Neighbors(tile, ViewKit.World.Value.Width, ViewKit.World.Value.Height, neighbors);
            for (int i = 0; i < count; i++)
            {
                int candidate = neighbors[i];
                if (!ViewKit.World.Value.TileAt(candidate).IsLand && frame.Visibility[candidate] == (byte)TileVisibility.Unknown)
                    coasts.Add((tile, candidate));
            }
        }
        Assert.NotEmpty(coasts);
        bool[][] revealed = (bool[][])typeof(CivSim.Sim.Politics.PoliticsSystem)
            .GetField("_revealed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(match.Politics)!;
        revealed[0] ??= new bool[ViewKit.World.Value.TileCount];
        foreach ((int shoreline, _) in coasts) revealed[0][shoreline] = true;
        builder.Build(match, frame);
        Assert.All(coasts, coast => Assert.Equal((byte)TileVisibility.Unknown, frame.Visibility[coast.Water]));

        int key5 = match.Research.Catalog.IndexOf("KEY-5");
        runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, key5, 0, 0));
        runner.Advance(2);
        builder.Build(match, frame);
        int hiddenLand = Enumerable.Range(0, ViewKit.World.Value.TileCount)
            .First(t => ViewKit.World.Value.TileAt(t).IsLand && frame.Visibility[t] == (byte)TileVisibility.Unknown);
        int way09 = match.Research.Catalog.IndexOf("WAY-09");
        runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, way09, 0, 0));
        runner.Advance(2);
        builder.Build(match, frame);

        Assert.Contains(coasts, coast => frame.Visibility[coast.Water] == (byte)TileVisibility.Explored);
        Assert.Equal((byte)TileVisibility.Unknown, frame.Visibility[hiddenLand]);
        Assert.Contains(coasts, coast => frame.Visibility[coast.Shoreline] != (byte)TileVisibility.Unknown);
    }
}
