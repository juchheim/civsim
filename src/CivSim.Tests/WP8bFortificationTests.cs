using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Public E3 fortification and siege boundaries (docs/MVP/22 §7.4).</summary>
public sealed class WP8bFortificationTests
{
    private const ulong Seed = 20261023;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private sealed class Rig
    {
        public readonly MatchModel Match = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        public readonly SimRunner Runner;
        public Rig() => Runner = new SimRunner(Match);

        public void Grant(int civ, string project)
        {
            Runner.Submit(new Command(Runner.Tick, (byte)civ, CommandKind.CompleteProject,
                TestPack.Catalog.IndexOf(project), 0, 0));
            Runner.Advance(1);
        }

        public void Season() => Runner.Advance(150);
    }

    private static Fighter[] Group(int count, string strength, long firstId = 0)
        => Enumerable.Range(0, count).Select(i => new Fighter(firstId + i, Fix64.Parse(strength))).ToArray();

    [Fact]
    public void Fortification_projects_register_buildable_hillfort_and_wall_structures()
    {
        int hillfort = LivingEconomyModel.StructureIndex("hillfort");
        int walls = LivingEconomyModel.StructureIndex("stone_walls");

        Assert.InRange(hillfort, 0, LivingEconomyModel.StructureKindCount - 1);
        Assert.InRange(walls, 0, LivingEconomyModel.StructureKindCount - 1);
        Assert.Equal("hillfort", LivingEconomyModel.StructureName(hillfort));
        Assert.Equal("stone_walls", LivingEconomyModel.StructureName(walls));
        var effects = EconomyContent.FromPack(TestPack.Pack).Effects.ToArray();
        Assert.Contains(effects, e => e.Kind == "build" && e.Structure == "hillfort" &&
            TestPack.Catalog.Projects[e.ProjectIndex].Id == "DEF-04");
        Assert.Contains(effects, e => e.Kind == "build" && e.Structure == "stone_walls" &&
            TestPack.Catalog.Projects[e.ProjectIndex].Id == "DEF-05");
    }

    [Fact]
    public void Wall_corvee_is_refused_before_DEF05_and_accepted_after_the_E3_build_unlock()
    {
        var rig = new Rig();
        rig.Grant(0, "KEY-3");
        rig.Season(); // initializes the Bronze civic readout for living settlements
        int settlement = Enumerable.Range(0, rig.Match.Politics.SettlementTotal)
            .First(s => rig.Match.Politics.SettlementCivOf(s) == 0 && !rig.Match.Politics.IsFreeFolk(s));

        Assert.False(rig.Match.Economy.HasCivicWork(settlement, LivingEconomyModel.CorveeTarget.Walls));

        rig.Grant(0, "DEF-04");
        rig.Grant(0, "DEF-05");

        Assert.True(rig.Match.Economy.HasCivicWork(settlement, LivingEconomyModel.CorveeTarget.Walls));
        Assert.True(rig.Match.Economy.SetCorvee(settlement, checked((int)rig.Match.Economy.SeasonsCompleted + 3),
            LivingEconomyModel.CorveeTarget.Walls));
        Assert.Equal(LivingEconomyModel.CorveeTarget.Walls, rig.Match.Economy.CorveeAt(settlement));
    }

    [Fact]
    public void Stone_walls_hold_the_assault_line_and_shelter_raided_stores()
    {
        Defenses walls = Defenses.None with { StoneWalls = true };

        Assert.Equal(Fix64.FromInt(3) * Fix64.FromRatio(11, 10), walls.Multiplier());
        Assert.Equal(Fix64.FromRatio(1, 4), walls.LootMultiplier());
        Assert.False(walls.CanBeAssaulted());

        BattleResult assault = Captures.Assault(Group(40, "3.0"), Group(20, "1.0", 100), walls,
            childrenInside: true, Seed, 17);
        Assert.Equal(BattleWinner.StandOff, assault.Winner);
        Assert.Empty(assault.AttackerDead);
        Assert.Empty(assault.DefenderDead);

        RaidOutcome raid = Raids.Resolve(Group(100, "20.0"), Group(20, "0.1", 1000), walls,
            raiderRanged: false, Seed, 21);
        Assert.True(raid.Success);
        Assert.Equal(Raids.CarryPerRaider * raid.Battle.AttackerSurvivors * Fix64.FromRatio(1, 4), raid.LootCapacity);
    }
}
