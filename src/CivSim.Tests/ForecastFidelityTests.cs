using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Ai.Forecast;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.World;
using Xunit.Abstractions;

namespace CivSim.Tests;

/// <summary>
/// The forecast model against the live simulation (docs/MVP/14 §3.2): a real 45-minute match of four Council
/// civilizations; each forecast starts from the civilization's captured state at minute 0 or minute 10, replays its
/// actual completion order, and rolls out to the 64-minute Bronze chapter horizon. KEY-1, KEY-2 and KEY-3 must land within 25% of the live
/// times, and the median error within 12%.
/// </summary>
public class ForecastFidelityTests
{
    private readonly ITestOutputHelper _out;
    public ForecastFidelityTests(ITestOutputHelper output) => _out = output;

    private sealed record Start(int Civ, int Minute, ForecastStart State, long Tick);

    private static (MatchModel Match, List<Start> Starts) LiveRun(ulong seed, ForecastCatalog fc)
    {
        MatchModel match = ForecastTestKit.Match(seed);
        var runner = new SimRunner(match);
        var trackers = Enumerable.Range(0, 4).Select(_ => new KnowHowTracker()).ToArray();
        var starts = new List<Start>();
        for (int season = 1; season <= 180; season++)
        {
            runner.Advance(ResearchSystem.SeasonTicks);
            for (int c = 0; c < 4; c++)
            {
                // The tracker learns Know-how gains from every season-end look, as the Spark's does.
                trackers[c].Observe(new SystemReadout(match.Research, c), match.Politics.Season);
                if (season != 1 && season != 40) continue;
                var start = new ForecastStart(fc);
                ForecastCapture.FromSystem(start, match.Research, c, EconomyArchetype.Balanced, trackers[c], match.Politics.Season);
                ForecastCapture.SetPopulation(start, ForecastTestKit.Population(match, c));
                starts.Add(new Start(c, season == 1 ? 0 : 10, start, runner.Tick));
            }
        }
        return (match, starts);
    }

    [Fact]
    public void Keystone_times_forecast_from_the_live_plan_land_within_25_percent_of_the_live_match()
    {
        var fc = new ForecastCatalog(TestPack.Catalog);
        (MatchModel match, List<Start> starts) = LiveRun(20261019, fc);
        var errors = new List<double>();
        foreach (Start s in starts)
        {
            var forecast = new ResearchForecast(fc);
            forecast.Prepare(s.State, 64 * 4);
            int[] order = ForecastTestKit.CompletionOrder(match.Research, s.Civ, s.Tick);
            // A Council civilization pulls a ready keystone into Primary at once; the forecast plays that rule too.
            ForecastResult r = forecast.Run(order, promoteKeystone: true);
            int[] keys = { r.Key1, r.Key2, r.Key3 };
            var line = new List<string>();
            for (int k = 1; k <= 3; k++)
            {
                double live = ForecastTestKit.KeyMinute(match.Research, s.Civ, k);
                if (double.IsNaN(live) || live * ForecastTestKit.TicksPerMinute <= s.Tick) continue;
                Assert.True(keys[k - 1] >= 0, $"civ {s.Civ} from minute {s.Minute}: KEY-{k} not forecast within 64 minutes (live {live:0.0})");
                double predicted = ForecastTestKit.StepMinute(keys[k - 1], s.Tick);
                double error = Math.Abs(predicted - live) / live;
                errors.Add(error);
                line.Add($"KEY-{k} live {live:0.0} forecast {predicted:0.0} ({error:P0})");
                Assert.True(error <= 0.25, $"civ {s.Civ} from minute {s.Minute}: KEY-{k} live {live:0.0}, forecast {predicted:0.0}");
            }
            _out.WriteLine($"civ {s.Civ} from minute {s.Minute}: " + string.Join(", ", line));
        }
        Assert.True(errors.Count >= 16, $"expected keystones to compare, got {errors.Count}");
        double median = ForecastTestKit.Median(errors);
        _out.WriteLine($"median error {median:P1}");
        Assert.True(median <= 0.12, $"median keystone error {median:P1}");
    }
}

/// <summary>Shared helpers for the forecast and Spark tests: matches, captures, live keystone times.</summary>
internal static class ForecastTestKit
{
    public const int TicksPerMinute = 600;
    public static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static readonly object WorldLock = new();
    private static readonly List<(ulong Seed, GeneratedWorld World)> Worlds = new();

    public static GeneratedWorld World(ulong seed)
    {
        lock (WorldLock)
        {
            foreach ((ulong s, GeneratedWorld w) in Worlds) if (s == seed) return w;
            GeneratedWorld world = WorldGenerator.Generate(seed);
            Worlds.Add((seed, world));
            return world;
        }
    }

    public static MatchModel Match(ulong seed, params CivControl[] controls)
        => controls.Length == 0
            ? MatchModel.Create(seed, TestPack.Pack, World(seed), Balanced)
            : MatchModel.Create(seed, TestPack.Pack, World(seed), Balanced, controls);

    public static int Population(MatchModel match, int civ)
    {
        int n = 0;
        for (int f = 0; f < match.Economy.FigureCount; f++) if (match.Economy.Figure(f).Civilization == civ) n++;
        return n;
    }

    public static ForecastStart Capture(MatchModel match, int civ, KnowHowTracker tracker, ForecastCatalog catalog)
    {
        var start = new ForecastStart(catalog);
        ForecastCapture.FromSystem(start, match.Research, civ, EconomyArchetype.Balanced, tracker, match.Politics.Season);
        ForecastCapture.SetPopulation(start, Population(match, civ));
        return start;
    }

    /// <summary>Minutes at which the civilization completed a keystone (NaN if not).</summary>
    public static double KeyMinute(ResearchSystem r, int civ, int key)
    {
        int p = r.Catalog.IndexOf($"KEY-{key}");
        return r.IsDone(civ, p) ? r.CompletedTick(civ, p) / (double)TicksPerMinute : double.NaN;
    }

    /// <summary>The projects a civilization completed after <paramref name="afterTick"/>, in completion order.</summary>
    public static int[] CompletionOrder(ResearchSystem r, int civ, long afterTick)
        => Enumerable.Range(0, r.Catalog.Count).Where(p => r.IsDone(civ, p) && r.CompletedTick(civ, p) > afterTick)
            .OrderBy(p => r.CompletedTick(civ, p)).ThenBy(p => p).ToArray();

    /// <summary>A forecast step expressed in match minutes, from a start at <paramref name="startTick"/>.</summary>
    public static double StepMinute(int step, long startTick)
        => (startTick + step * (double)ResearchSystem.SeasonTicks / ForecastTuning.StepsPerSeason) / TicksPerMinute;

    public static double Median(IList<double> xs)
    {
        var s = xs.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
        if (s.Count == 0) return double.NaN;
        return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2;
    }
}
