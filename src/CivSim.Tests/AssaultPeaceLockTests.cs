using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Peace must never end a war while one of its assaults is still on the road, besieging or holding a settlement.</summary>
public class AssaultPeaceLockTests
{
    private const ulong Seed = 20261019;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel match, SimRunner runner) Rig()
    {
        var match = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        var runner = new SimRunner(match);
        runner.Advance((long)(24 * 600) - runner.Tick);
        foreach (string id in new[] { "HNT-02", "HNT-03", "WAR-01", "WAR-02", "WAR-04" })
            runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, TestPack.Catalog.IndexOf(id), 0, 0));
        runner.Advance(1);
        match.Politics.ForceContact(0, 1);
        match.Politics.SetWar(0, 1, true);
        return (match, runner);
    }

    [Fact]
    public void Peace_is_refused_while_an_assault_is_on_the_road()
    {
        var (match, _) = Rig();
        PoliticsSystem p = match.Politics;
        Assert.True(p.LaunchAssault(0, 1, requireOdds: false));
        Assert.False(p.OrderPeace(0, 1), "the attacker may not make peace with its own army on the road");
        Assert.False(p.OrderPeace(1, 0), "the defender may not make peace while the army is on the road");
        Assert.True(p.Diplomacy.AtWar(0, 1));
    }

    [Fact]
    public void Peace_is_allowed_again_once_the_assault_has_resolved()
    {
        var (match, runner) = Rig();
        PoliticsSystem p = match.Politics;
        Assert.True(p.LaunchAssault(0, 1, requireOdds: false));
        runner.Advance(150L * 40);
        if (p.Diplomacy.AtWar(0, 1)) Assert.True(p.OrderPeace(0, 1), "nothing is left on the road, so peace may be asked");
    }

    [Fact]
    public void Only_the_guarded_door_may_end_a_war()
    {
        // Every call to Diplomacy.MakePeace in the simulation must be the guarded TryEndWar, the SetWar scenario hook or the test fixture.
        string[] allowed = { "TryEndWar", "SetWar", "FixtureMakePeace" };
        var offenders = new System.Collections.Generic.List<string>();
        foreach (string path in Directory.EnumerateFiles(RepoPaths.SimSources, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            string[] lines = File.ReadAllLines(path);
            string method = "";
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                foreach (string name in allowed) if (line.Contains(name + "(") && !line.Contains(".MakePeace(")) method = name;
                if (line.Contains("_dip.MakePeace(") && !line.TrimStart().StartsWith("//") && !allowed.Contains(method))
                    offenders.Add($"{Path.GetFileName(path)}:{i + 1}");
            }
        }
        Assert.True(offenders.Count == 0, "war must end only through TryEndWar (it enforces the assault lock): " + string.Join(", ", offenders));
    }
}
