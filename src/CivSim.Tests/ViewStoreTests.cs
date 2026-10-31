using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CivSim.Sim.Core;
using CivSim.Sim.Research;
using CivSim.Sim.View;

namespace CivSim.Tests;

// This class contains a wall-clock performance gate. Keep it off other concurrent test collections so
// unrelated simulation tests cannot consume the same cores and turn scheduler noise into a failure.
[CollectionDefinition("Frame timing", DisableParallelization = true)]
public sealed class FrameTimingCollection { }

/// <summary>Handing frames to the render thread (docs/MVP/18 §5): matching by id, the lock-free swap, no allocation, the time budget.</summary>
[Collection("Frame timing")]
public class ViewStoreTests
{
    [Fact]
    public void Consecutive_frames_match_figures_by_id_and_report_births_and_deaths()
    {
        MatchModel match = ViewKit.NewMatch();
        var runner = new SimRunner(match);
        var store = new FrameStore();
        var builder = new FrameBuilder(match, -1);
        runner.Advance(15 * 600 - 30);
        int spawned = 0, removed = 0, matched = 0, moved = 0;
        for (int step = 0; step < 120; step++) // crosses eight season boundaries
        {
            runner.Advance(10);
            store.BuildAndPublish(builder, match);
            Assert.True(store.TryAcquire());
            Assert.False(store.TryAcquire(), "nothing new was published");
            MatchFrame? prev = store.Previous, cur = store.Current;
            if (prev is null) continue;
            Assert.True(cur!.Tick == prev.Tick + 10);
            IdLink link = store.Figures;
            Assert.Equal(cur.FigureCount, link.Count);
            var previousIds = new HashSet<long>(prev.FigureId.Take(prev.FigureCount));
            var currentIds = new HashSet<long>(cur.FigureId.Take(cur.FigureCount));
            for (int i = 0; i < cur.FigureCount; i++)
            {
                int p = link.PreviousIndex(i);
                if (p < 0) { Assert.DoesNotContain(cur.FigureId[i], previousIds); continue; }
                Assert.Equal(cur.FigureId[i], prev.FigureId[p]);
                matched++;
                if (cur.FigureX[i] != prev.FigureX[p] || cur.FigureY[i] != prev.FigureY[p]) moved++;
                // Interpolated positions stay on the map.
                long half = prev.FigureX[p] + (cur.FigureX[i] - prev.FigureX[p]) / 2;
                Assert.InRange(half, 0, (cur.Width + 1) * HexLayout.Unit);
            }
            for (int r = 0; r < link.RemovedCount; r++) Assert.DoesNotContain(prev.FigureId[link.Removed(r)], currentIds);
            Assert.Equal(cur.FigureCount - link.SpawnedCount, prev.FigureCount - link.RemovedCount);
            spawned += link.SpawnedCount;
            removed += link.RemovedCount;
            Assert.Equal(cur.HerdCount, store.Herds.Count);
        }
        Assert.True(matched > 1000);
        Assert.True(moved > matched / 10, "figures walk between frames");
        Assert.True(spawned > 0, "births at a season boundary are spawned");
        Assert.True(removed > 0, "deaths at a season boundary are removed");
        Assert.Equal(0, store.Alpha(store.Previous!.Tick * 100, 100));
        Assert.Equal(100, store.Alpha(store.Current!.Tick * 100, 100));
        Assert.Equal(50, store.Alpha((store.Previous.Tick + 5) * 100, 100));
    }

    [Fact]
    public void Frames_cross_threads_whole_and_in_order()
    {
        var store = new FrameStore();
        store.Reserve(figures: 64, settlements: 4, herds: 4);
        const int published = 20_000;
        var writer = new Thread(() =>
        {
            for (int i = 1; i <= published; i++)
            {
                MatchFrame f = store.Back;
                f.Tick = i;
                f.FigureCount = i % 50;
                for (int k = 0; k < f.FigureCount; k++) f.FigureId[k] = (long)i * 1000 + k;
                store.Publish();
                if (i % 1000 == 0) Thread.Yield();
            }
        });
        writer.Start();
        long last = 0, acquired = 0, torn = 0;
        while (last < published)
        {
            if (!store.TryAcquire()) { Thread.SpinWait(20); continue; }
            MatchFrame f = store.Current!;
            Assert.True(f.Tick > last, "frames arrive newest last");
            last = f.Tick;
            acquired++;
            if (f.FigureCount != f.Tick % 50) torn++;
            for (int k = 0; k < f.FigureCount; k++) if (f.FigureId[k] != f.Tick * 1000 + k) torn++;
            if (store.Previous is { } p) Assert.True(p.Tick < f.Tick);
        }
        writer.Join();
        Assert.Equal(0, torn);
        Assert.True(acquired > 0);
    }

    [Fact]
    public void Building_frames_allocates_nothing_once_warm_and_stays_well_under_a_millisecond()
    {
        MatchModel match = ViewKit.NewMatch();
        var runner = new SimRunner(match);
        var store = new FrameStore();
        store.Reserve(figures: 4096, settlements: 128, herds: match.Economy.WildHerdCount);
        var viewer = new FrameBuilder(match, 0);
        var omniscient = new FrameBuilder(match, -1);
        var omniFrame = new MatchFrame();
        omniFrame.Reserve(4096, 128, match.Economy.WildHerdCount);
        while (runner.Tick < 18 * 600)
        {
            runner.Advance(10);
            store.BuildAndPublish(viewer, match);
            store.TryAcquire();
            if (runner.Tick % 50 == 0) omniscient.Build(match, omniFrame);
        }

        // Prime trail links, event scratch buffers and the arrays a first mining camp grows (it opens at about minute 18.8 now).
        for (int i = 0; i < 60; i++)
        {
            runner.Advance(10);
            store.BuildAndPublish(viewer, match);
            store.TryAcquire();
            omniscient.Build(match, omniFrame);
        }

        long allocated = 0;
        for (int i = 0; i < 60; i++) // 600 ticks: four season boundaries
        {
            runner.Advance(10);
            long before = GC.GetAllocatedBytesForCurrentThread();
            store.BuildAndPublish(viewer, match);
            store.TryAcquire();
            omniscient.Build(match, omniFrame);
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Assert.Equal(0, allocated);

        // Time: the whole map, every figure (omniscient), rebuilt from the same state, averaged.
        var watch = Stopwatch.StartNew();
        const int builds = 200;
        for (int i = 0; i < builds; i++) omniscient.Build(match, omniFrame);
        watch.Stop();
        double meanMs = watch.Elapsed.TotalMilliseconds / builds;
        Assert.True(meanMs < 1.0, $"omniscient frame of {omniFrame.FigureCount} figures took {meanMs:F3} ms");
    }
}
