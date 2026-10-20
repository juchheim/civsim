using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Research;
using CivSim.Sim.View;

namespace CivSim.Tests;

/// <summary>Building frames only reads the match (docs/MVP/18 §8): checksums are unchanged, and a resumed save shows the same frames.</summary>
public class ViewDeterminismTests
{
    [Fact]
    public void The_politics_golden_still_passes_with_frames_built_every_10_ticks()
    {
        MatchModel match = ViewKit.NewMatch();
        var runner = new SimRunner(match);
        var store = new FrameStore();
        var viewer = new FrameBuilder(match, 0);
        var omniscient = new FrameBuilder(match, -1);
        var spare = new MatchFrame();
        long ticks = 24 * 600;
        while (runner.Tick < ticks)
        {
            runner.Advance(10);
            store.BuildAndPublish(viewer, match);
            store.TryAcquire();
            if (runner.Tick % 150 == 0) omniscient.Build(match, spare);
        }
        string[] golden = File.ReadAllText(RepoPaths.Golden("politics-v1.txt")).Replace("\r\n", "\n").Trim().Split('\n');
        Assert.Equal(golden[1], $"final={runner.CurrentChecksum:x16}");
        Assert.Equal(golden[2], $"digest={runner.Digest():x16}");
    }

    [Fact]
    public void A_match_with_a_view_attached_has_the_same_checksums_as_one_without()
    {
        MatchModel watched = ViewKit.NewMatch(), plain = ViewKit.NewMatch();
        var watchedRunner = new SimRunner(watched);
        var plainRunner = new SimRunner(plain);
        var builder = new FrameBuilder(watched, 2);
        var frame = new MatchFrame();
        for (int i = 0; i < 300; i++)
        {
            watchedRunner.Advance(10);
            builder.Build(watched, frame);
            if (i % 7 == 0) builder.Build(watched, frame); // twice in a tick is harmless too
        }
        plainRunner.Advance(3000);
        Assert.Equal(plainRunner.Digest(), watchedRunner.Digest());
        Assert.Equal(plain.ComputeChecksum(), watched.ComputeChecksum());
    }

    [Fact]
    public void A_resumed_save_shows_the_same_frames_as_the_straight_run()
    {
        const long resumeAt = 20 * 600, end = resumeAt + 1500;
        // Straight run.
        MatchModel straight = ViewKit.NewMatch();
        var straightRunner = new SimRunner(straight);
        var straightBuilder = new FrameBuilder(straight, 0);
        var frame = new MatchFrame();
        var straightHashes = new List<ulong>();
        while (straightRunner.Tick < end)
        {
            straightRunner.Advance(10);
            straightBuilder.Build(straight, frame);
            if (straightRunner.Tick > resumeAt) straightHashes.Add(ViewKit.Hash(frame));
        }

        // Played to the save point with the same builder cadence, saved (match and view), loaded into new objects.
        MatchModel first = ViewKit.NewMatch();
        var firstRunner = new SimRunner(first);
        var firstBuilder = new FrameBuilder(first, 0);
        var firstFrame = new MatchFrame();
        while (firstRunner.Tick < resumeAt)
        {
            firstRunner.Advance(10);
            firstBuilder.Build(first, firstFrame);
        }
        byte[] snapshot = ViewKit.Snapshot(first);
        using var viewState = new MemoryStream();
        using (var writer = new BinaryWriter(viewState, System.Text.Encoding.UTF8, leaveOpen: true)) firstBuilder.WriteState(writer);
        viewState.Position = 0;

        MatchModel resumed = ViewKit.Restore(snapshot);
        var resumedBuilder = new FrameBuilder(resumed, 0);
        using (var reader = new BinaryReader(viewState)) resumedBuilder.ReadState(reader);
        var resumedRunner = new SimRunner(resumed, SimRunner.DefaultChecksumInterval, startTick: resumeAt);
        var resumedHashes = new List<ulong>();
        var resumedFrame = new MatchFrame();
        while (resumedRunner.Tick < end)
        {
            resumedRunner.Advance(10);
            resumedBuilder.Build(resumed, resumedFrame);
            resumedHashes.Add(ViewKit.Hash(resumedFrame));
        }
        Assert.Equal(straight.ComputeChecksum(), resumed.ComputeChecksum());
        Assert.Equal(straightHashes.Count, resumedHashes.Count);
        for (int i = 0; i < straightHashes.Count; i++)
            Assert.True(straightHashes[i] == resumedHashes[i], $"frame {i} after the resume differs");
        Assert.Equal(ViewKit.Hash(frame), ViewKit.Hash(resumedFrame));
    }
}
