using CivSim.Sim.Core;
using CivSim.Sim.Research;
using CivSim.Sim.View;

namespace CivSim.Tests;

/// <summary>The megalith (STN-10) goes out as a monument: where, whose and when, for the front end to play the raising from.</summary>
public class ViewMonumentTests
{
    [Fact]
    public void A_learned_megalith_shows_in_the_frame_at_a_fixed_place_with_its_learning_tick()
    {
        MatchModel match = ViewKit.NewMatch();
        var runner = new SimRunner(match);
        runner.Advance(10);
        var builder = new FrameBuilder(match, 0);
        var frame = new MatchFrame();
        builder.Build(match, frame);
        Assert.Equal(0, frame.MonumentCount);

        int project = match.Research.Catalog.IndexOf("STN-10");
        runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, project, 0, 0));
        runner.Advance(20);
        builder.Build(match, frame);
        Assert.Equal(1, frame.MonumentCount);
        Assert.Equal(0, frame.MonumentCiv[0]);
        Assert.Equal(match.Research.CompletedTick(0, project), frame.MonumentTick[0]);
        int tile = frame.MonumentTile[0];
        Assert.InRange(tile, 0, frame.Width * frame.Height - 1);
        Assert.True(frame.MonumentPopulation[0] > 0);

        // It stays where it was raised, and the number does not depend on who builds the frame.
        runner.Advance(3000);
        builder.Build(match, frame);
        Assert.Equal(1, frame.MonumentCount);
        Assert.Equal(tile, frame.MonumentTile[0]);
    }

    [Fact]
    public void A_rival_megalith_is_hidden_until_its_tile_is_seen()
    {
        MatchModel match = ViewKit.NewMatch();
        var runner = new SimRunner(match);
        runner.Advance(10);
        int project = match.Research.Catalog.IndexOf("STN-10");
        runner.Submit(new Command(runner.Tick, 3, CommandKind.CompleteProject, project, 0, 0));
        runner.Advance(20);
        var frame = new MatchFrame();
        new FrameBuilder(match, 0).Build(match, frame);
        for (int i = 0; i < frame.MonumentCount; i++) Assert.NotEqual(3, (int)frame.MonumentCiv[i]);
        var omniscient = new MatchFrame();
        new FrameBuilder(match, -1).Build(match, omniscient);
        Assert.Equal(1, omniscient.MonumentCount);
        Assert.Equal(3, omniscient.MonumentCiv[0]);
    }

    [Fact]
    public void Each_civilization_reports_the_best_boat_its_people_know()
    {
        MatchModel match = ViewKit.NewMatch();
        var runner = new SimRunner(match);
        runner.Advance(10);
        var builder = new FrameBuilder(match, -1);
        var frame = new MatchFrame();
        builder.Build(match, frame);
        for (int c = 0; c < frame.CivSeaTier.Length; c++) Assert.Equal(0, frame.CivSeaTier[c]);

        runner.Submit(new Command(runner.Tick, 1, CommandKind.CompleteProject, match.Research.Catalog.IndexOf("SEA-01"), 0, 0));
        runner.Submit(new Command(runner.Tick, 1, CommandKind.CompleteProject, match.Research.Catalog.IndexOf("SEA-03"), 0, 0));
        runner.Advance(20);
        builder.Build(match, frame);
        Assert.Equal(3, frame.CivSeaTier[1]);
        Assert.Equal(0, frame.CivSeaTier[0]);
    }
}
