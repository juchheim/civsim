using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>WP8a once-per-world and persisted state lifecycle contracts.</summary>
public sealed class WP8aLifecycleTests
{
    private const ulong Seed = 20261019;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    [Fact]
    public void Tin_Folk_spawn_once_at_the_first_Key3_and_survive_snapshot_resume()
    {
        MatchModel match = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        var runner = new SimRunner(match);
        for (int civ = 0; civ < 4; civ++)
        {
            match.Politics.SetDecreePolicy(civ, null);
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, TestPack.Catalog.IndexOf("KEY-3"), 0, 0));
        }
        runner.Advance(1);
        Assert.Empty(match.Politics.TinFolk);

        runner.Advance(149); // first Bronze season end runs the once-per-world spawn
        Assert.True(match.Politics.TinFolkSpawned);
        int spawned = match.Politics.TinFolk.Count;
        Assert.InRange(spawned, 1, 6);
        Assert.Equal(4 * spawned, match.Politics.Events.Count(e => e.Kind == PoliticsEventKind.TinFolkAppeared));
        Assert.All(match.Politics.TinFolk, folk => Assert.True(match.Politics.IsFreeFolk(folk.Settlement)));

        using var snapshot = new MemoryStream();
        using (var writer = new BinaryWriter(snapshot, System.Text.Encoding.UTF8, leaveOpen: true)) match.WriteSnapshot(writer);
        snapshot.Position = 0;
        MatchModel resumed = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        using (var reader = new BinaryReader(snapshot)) resumed.ReadSnapshot(reader);
        Assert.Equal(match.ComputeChecksum(), resumed.ComputeChecksum());
        Assert.Equal(spawned, resumed.Politics.TinFolk.Count);
        Assert.True(resumed.Politics.TinFolkSpawned);

        runner.Advance(150);
        var resumedRunner = new SimRunner(resumed, 50, runner.Tick - 150);
        resumedRunner.Advance(150);
        Assert.Equal(spawned, resumed.Politics.TinFolk.Count);
        Assert.Equal(match.ComputeChecksum(), resumed.ComputeChecksum());
    }

}
