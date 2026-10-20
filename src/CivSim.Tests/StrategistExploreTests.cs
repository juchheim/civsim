using System;
using System.Linq;
using System.Text;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;
using Xunit.Abstractions;

namespace CivSim.Tests;

/// <summary>A trace of one match for debugging the Strategist: <c>CIVSIM_EXPLORE=seed</c> (and <c>CIVSIM_MODE=rusher</c>); otherwise it returns at once.</summary>
public class StrategistExploreTests
{
    private readonly ITestOutputHelper _out;
    public StrategistExploreTests(ITestOutputHelper output) { _out = output; }

    [Fact]
    public void Explore()
    {
        if (Environment.GetEnvironmentVariable("CIVSIM_EXPLORE") is null) return;
        ulong seed = ulong.Parse(Environment.GetEnvironmentVariable("CIVSIM_EXPLORE")!);
        string mode = Environment.GetEnvironmentVariable("CIVSIM_MODE") ?? "duel";
        var world = WorldGenerator.Generate(seed);
        var ai = CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced);
        bool swap = Environment.GetEnvironmentVariable("CIVSIM_SWAP") is not null;
        var rig = swap
            ? new StrategistRig(seed, world, new[] { CivControl.Council, ai, ai, CivControl.Council }) { Omniscient = false }
            : new StrategistRig(seed, world, new[] { ai, CivControl.Council, CivControl.Council, ai }) { Omniscient = false };
        if (mode == "rusher") { rig.Rushers.Add(new Rusher(1, 0)); rig.Rushers.Add(new Rusher(2, 3)); }
        var sb = new StringBuilder();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        rig.Seasons(132);
        sb.AppendLine($"elapsed {sw.ElapsedMilliseconds} ms");
        var m = rig.Match;
        for (int c = 0; c < 4; c++)
            sb.AppendLine($"civ{c} dom {m.Politics.Territory.Dominion(c)} tiles {m.Politics.Territory.OwnedTiles(c)} settlements {rig.SettlementsOf(c).Count} posture {m.Politics.PostureOf(c)}");
        foreach (var d in rig.Log.Entries) sb.AppendLine($"{d.Season} civ{d.Civ} {d.Kind} s{d.Subject} o{d.Other} {d.Score.ToDoubleForDisplay():F2} {d.Reason}");
        foreach (var e in m.Politics.Events.Where(e => e.Kind is not (PoliticsEventKind.BeaconPlaced or PoliticsEventKind.Gift or PoliticsEventKind.Barter)))
            sb.AppendLine($"{(e.Tick + 1) / 150} civ{e.Civ} {e.Kind} other {e.Other} a {e.A} b {e.B}");
        sb.AppendLine(StrategistAudit.Run(0, rig.Log.Entries, m.Politics.Events).ToString());
        sb.AppendLine(StrategistAudit.Run(3, rig.Log.Entries, m.Politics.Events).ToString());
        _out.WriteLine(sb.ToString());
    }
}
