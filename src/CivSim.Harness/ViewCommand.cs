using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.View;

namespace CivSim.Harness;

/// <summary>
/// <code>
/// civsim-harness view --pack FILE [--seed N] [--minutes N] [--viewer CIV|-1] [--every TICKS] [--compare]
/// </code>
/// Plays a match on the Council, builds a frame for the viewer every few ticks the way the front end's sim thread would
/// (through a <see cref="FrameStore"/>), and prints frame contents, build times and allocations (docs/MVP/18 §9). With
/// --compare it plays the same match again without frames and requires the same checksum digest.
/// </summary>
internal static class ViewCommand
{
    public static int Run(string[] args)
    {
        string? packPath = null;
        ulong seed = ResearchCommand.GoldenSeed;
        int minutes = 40, viewer = 0, every = 10;
        bool compare = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pack": packPath = args[++i]; break;
                case "--seed": seed = ulong.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--minutes": minutes = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--viewer": viewer = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--every": every = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--compare": compare = true; break;
                default: throw new ArgumentException($"Unknown option {args[i]}.");
            }
        }
        if (packPath is null) throw new ArgumentException("view needs --pack FILE.");
        if (every <= 0 || every > 150) throw new ArgumentException("--every must be 1 to 150 ticks (at least one frame a season).");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        var archetypes = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

        var match = MatchModel.Create(seed, pack, archetypes);
        var runner = new SimRunner(match);
        var builder = new FrameBuilder(match, viewer);
        var store = new FrameStore();
        // The MVP budget (2,400 figures) with headroom, so frames never grow mid-match.
        store.Reserve(figures: 4096, settlements: 128, herds: match.Economy.WildHerdCount);
        long ticks = minutes * 600L;
        long frames = 0, totalTicks = 0, maxTicks = 0, lastMinuteTicks = 0, lastMinuteFrames = 0, measuredBytes = 0, measuredFrames = 0;
        long maxFigures = 0;
        var tierCounts = new int[5];
        long shownSequence = 0;
        var watch = new Stopwatch();
        while (runner.Tick < ticks)
        {
            runner.Advance(Math.Min(every, ticks - runner.Tick));
            long before = GC.GetAllocatedBytesForCurrentThread();
            watch.Restart();
            store.BuildAndPublish(builder, match);
            watch.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            frames++;
            long elapsed = watch.ElapsedTicks;
            totalTicks += elapsed;
            maxTicks = Math.Max(maxTicks, elapsed);
            if (runner.Tick > ticks - 600) { lastMinuteTicks += elapsed; lastMinuteFrames++; }
            // Allocation is measured once the buffers have grown: after the first minute.
            if (runner.Tick > 600) { measuredBytes += allocated; measuredFrames++; }
            // The render side: acquire, count new events by tier.
            if (store.TryAcquire())
            {
                MatchFrame frame = store.Current!;
                maxFigures = Math.Max(maxFigures, frame.FigureCount);
                for (int i = 0; i < frame.EventCount; i++)
                {
                    ViewEvent e = frame.Events[i];
                    if (e.Sequence <= shownSequence) continue;
                    tierCounts[(int)e.Tier]++;
                    shownSequence = e.Sequence;
                }
            }
            if (runner.Tick % 6000 == 0) Report(store.Current!, runner.Tick);
        }
        double ms(long t) => t * 1000.0 / Stopwatch.Frequency;
        MatchFrame last = store.Current!;
        Console.WriteLine($"seed={seed} minutes={minutes} viewer={viewer} every={every} frames={frames}");
        Console.WriteLine($"figures: {last.FigureCount} in frame of {last.TotalFigures} alive (max in frame {maxFigures}); settlements {last.SettlementCount}, herds {last.HerdCount}, parties {last.PartyCount}, occupations {last.OccupationCount}");
        Console.WriteLine($"build time: mean {ms(totalTicks) / frames:F3} ms, last minute mean {ms(lastMinuteTicks) / Math.Max(1, lastMinuteFrames):F3} ms, max {ms(maxTicks):F3} ms");
        Console.WriteLine($"allocated after the first minute: {measuredBytes} bytes over {measuredFrames} frames ({(double)measuredBytes / Math.Max(1, measuredFrames):F1} per frame)");
        Console.WriteLine($"events: {builder.LatestEventSequence} logged; shown by tier ambient {tierCounts[0]}, info {tierCounts[1]}, milestone {tierCounts[2]}, moment {tierCounts[3]}, critical {tierCounts[4]}");
        Console.WriteLine($"digest={runner.Digest():x16} final={runner.CurrentChecksum:x16}");

        // The same frames for the omniscient viewer, once, for the stats line.
        var omni = new FrameBuilder(match, -1);
        var all = new MatchFrame();
        watch.Restart();
        omni.Build(match, all);
        watch.Stop();
        Console.WriteLine($"omniscient frame: {all.FigureCount} figures, {all.SettlementCount} settlements, {all.HerdCount} herds, first build {ms(watch.ElapsedTicks):F3} ms");

        if (compare)
        {
            var plain = MatchModel.Create(seed, pack, archetypes);
            var plainRunner = new SimRunner(plain);
            plainRunner.Advance(ticks);
            bool same = plainRunner.Digest() == runner.Digest() && plainRunner.CurrentChecksum == runner.CurrentChecksum;
            Console.WriteLine(same ? "view determinism check passed (same digest without frames)" : $"VIEW DETERMINISM CHECK FAILED: without frames digest={plainRunner.Digest():x16}");
            if (!same) return 1;
        }
        return 0;
    }

    private static void Report(MatchFrame f, long tick)
    {
        int visible = 0, explored = 0;
        for (int t = 0; t < f.Visibility.Length; t++)
        {
            if (f.Visibility[t] == (byte)TileVisibility.Visible) visible++;
            else if (f.Visibility[t] == (byte)TileVisibility.Explored) explored++;
        }
        int walking = 0, away = 0;
        for (int i = 0; i < f.FigureCount; i++)
        {
            var flags = (FigureFlags)f.FigureFlags[i];
            if ((flags & FigureFlags.Walking) != 0) walking++;
            if ((flags & FigureFlags.Away) != 0) away++;
        }
        Console.WriteLine($"  [min {tick / 600}] year {f.Year} season {f.SeasonOfYear}: figures {f.FigureCount}/{f.TotalFigures} (walking {walking}, away {away}), settlements {f.SettlementCount}, herds {f.HerdCount}, parties {f.PartyCount}, tiles visible {visible} explored {explored}, era {f.CivEra[Math.Max(0, f.ViewerCiv)]}");
    }
}
