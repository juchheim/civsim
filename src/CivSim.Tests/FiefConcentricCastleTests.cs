using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>DEF-09's provisional concentric-castle strongpoint multiplier is local, garrisoned, and Fief-only.</summary>
public sealed class FiefConcentricCastleTests
{
    private const ulong Seed = 20261114;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric("DEF-09"));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner) Start(ChapterRules rules)
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules);
        return (match, new SimRunner(match));
    }

    private static BronzeFixture FiefFixture(params string[] projects)
        => BronzeFixture.CreateFiefWithCamp(Pack.Value, Seed, 100, projects);

    private static void Grant(SimRunner runner, MatchModel match, params string[] projects)
    {
        foreach (string project in projects)
        {
            int index = match.Research.Catalog.IndexOf(project);
            Assert.True(index >= 0, $"{project} must exist in the test pack");
            if (match.Research.Catalog.Projects[index].Era == ChapterRules.Fief.LastResearchEra &&
                !match.Research.IsDone(0, match.Research.Catalog.IndexOf("KEY-5")))
            {
                int key5 = match.Research.Catalog.IndexOf("KEY-5");
                Assert.True(key5 >= 0, "KEY-5 must exist in the Fief test pack");
                runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, key5, 0, 0));
                runner.Advance(2);
            }
            runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, index, 0, 0));
            runner.Advance(2);
        }
    }

    [Fact]
    public void Def09_applies_only_to_a_built_concentric_castle_at_a_garrisoned_castle_settlement()
    {
        (MatchModel match, SimRunner runner) = Start(ChapterRules.Fief);
        LivingEconomyModel economy = match.Economy;
        economy.FixtureBuildIronStructure(0, "castle");
        economy.FixtureSetSoldiersSupplied(0, 4);
        Grant(runner, match, "DEF-08");

        Defenses castle = match.Politics.FixtureDefensesOf(0);
        Assert.True(castle.Castle);
        Assert.False(castle.ConcentricCastle);
        Assert.Equal((castle with { Castle = false }).Multiplier() * Defenses.CastleStrongpoint, castle.Multiplier());

        Grant(runner, match, "DEF-09");
        Assert.False(match.Politics.FixtureDefensesOf(0).ConcentricCastle);
        economy.FixtureBuildIronStructure(0, "concentric_castle");
        Defenses upgraded = match.Politics.FixtureDefensesOf(0);
        Assert.True(upgraded.ConcentricCastle);
        economy.FixtureSetSoldiersSupplied(0, 0);
        Defenses uncrewed = match.Politics.FixtureDefensesOf(0);
        Assert.False(uncrewed.Castle);
        Assert.False(uncrewed.ConcentricCastle);

        economy.FixtureSetSoldiersSupplied(0, 4);
        upgraded = match.Politics.FixtureDefensesOf(0);
        Assert.True(upgraded.Castle);
        Assert.True(upgraded.ConcentricCastle);
        Assert.Equal(castle.Multiplier() * Defenses.ConcentricCastleMultiplier, upgraded.Multiplier());
        Assert.Equal(Fix64.FromRatio(15, 8), Defenses.CastleStrongpoint * Defenses.ConcentricCastleMultiplier);

        // The production raid resolver consumes the same settlement defense multiplier.
        var raiders = new[] { new Fighter(1, Fix64.One) };
        var defenders = new[] { new Fighter(2, Fix64.One) };
        RaidOutcome upgradedRaid = Raids.Resolve(raiders, defenders, upgraded, false, Seed, 17);
        RaidOutcome castleRaid = Raids.Resolve(raiders, defenders, castle, false, Seed, 17);
        Assert.NotNull(upgradedRaid.Battle);
        Assert.NotNull(castleRaid.Battle);
    }

    [Fact]
    public void Concentric_castle_effect_is_inert_outside_Fief()
    {
        (MatchModel match, SimRunner runner) = Start(ChapterRules.Iron);
        match.Economy.FixtureBuildIronStructure(0, "castle");
        match.Economy.FixtureBuildIronStructure(0, "concentric_castle");
        match.Economy.FixtureSetSoldiersSupplied(0, 4);
        Grant(runner, match, "DEF-08", "DEF-09");

        Defenses defenses = match.Politics.FixtureDefensesOf(0);
        Assert.True(defenses.Castle);
        Assert.False(defenses.ConcentricCastle);
        Assert.Equal((defenses with { Castle = false }).Multiplier() * Defenses.CastleStrongpoint, defenses.Multiplier());
        Assert.False(match.Research.IsDone(0, match.Research.Catalog.IndexOf("DEF-09")));
    }

    [Fact]
    public void A_concentric_structure_without_a_castle_is_not_a_strongpoint()
    {
        (MatchModel match, SimRunner runner) = Start(ChapterRules.Fief);
        Grant(runner, match, "DEF-08", "DEF-09");
        match.Economy.FixtureBuildIronStructure(0, "concentric_castle");
        match.Economy.FixtureSetSoldiersSupplied(0, 4);

        Defenses defenses = match.Politics.FixtureDefensesOf(0);
        Assert.False(defenses.Castle);
        Assert.False(defenses.ConcentricCastle);
        Assert.Equal((defenses with { Castle = false }).Multiplier(), defenses.Multiplier());
    }

    [Fact]
    public void Planner_builds_concentric_castles_only_after_DEF09_at_an_existing_garrisoned_castle()
    {
        BronzeFixture unresearched = FiefFixture("DEF-08");
        unresearched.Economy.FixtureSetTier(0, 1);
        unresearched.Economy.FixtureBuildIronStructure(0, "castle");
        unresearched.Economy.FixtureSetSoldiersSupplied(0, 4);
        Assert.False(unresearched.Economy.FixtureIronBuildWanted(0, "concentric_castle"));

        BronzeFixture noCastle = FiefFixture("DEF-08", "DEF-09");
        noCastle.Economy.FixtureSetTier(0, 1);
        noCastle.Economy.FixtureSetSoldiersSupplied(0, 4);
        Assert.False(noCastle.Economy.FixtureIronGateOpen(0, "concentric_castle"));

        BronzeFixture uncrewed = FiefFixture("DEF-08", "DEF-09");
        uncrewed.Economy.FixtureSetTier(0, 1);
        uncrewed.Economy.FixtureBuildIronStructure(0, "castle");
        Assert.False(uncrewed.Economy.FixtureIronGateOpen(0, "concentric_castle"));

        BronzeFixture ready = FiefFixture("DEF-08", "DEF-09");
        ready.Economy.FixtureSetTier(0, 1);
        ready.Economy.FixtureBuildIronStructure(0, "castle");
        ready.Economy.FixtureSetSoldiersSupplied(0, 4);
        Assert.True(ready.Economy.FixtureIronGateOpen(0, "concentric_castle"));
        Assert.True(ready.Economy.FixtureIronBuildWanted(0, "concentric_castle"));
        Assert.Equal(Fix64.FromInt(80), ready.Economy.FixtureIronLabor(0, "concentric_castle"));
        BuildItem upgrade = Assert.Single(ready.Economy.BuildOutlookFor(0).Items, item => item.Id == "concentric_castle");
        Assert.Equal(Fix64.FromInt(80), upgrade.Labor);
        Assert.Equal(new[] { Fix64.FromInt(2), Fix64.FromInt(12), Fix64.FromInt(24) },
            upgrade.Needs.Select(need => need.Need).OrderBy(need => need));

        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "DEF-08");
        iron.Economy.FixtureSetTier(0, 1);
        iron.Economy.FixtureBuildIronStructure(0, "castle");
        iron.Economy.FixtureSetSoldiersSupplied(0, 4);
        Assert.False(iron.Economy.FixtureIronGateOpen(0, "concentric_castle"));
    }

    [Fact]
    public void Built_concentric_castle_round_trips_in_the_Fief_snapshot_slot()
    {
        BronzeFixture live = FiefFixture("DEF-08", "DEF-09");
        live.Economy.FixtureSetTier(0, 1);
        live.Economy.FixtureBuildIronStructure(0, "castle");
        live.Economy.FixtureBuildIronStructure(0, "concentric_castle");
        live.Economy.FixtureSetSoldiersSupplied(0, 4);

        BronzeFixture resumed = FiefFixture("DEF-08", "DEF-09");
        resumed.Economy.FixtureSetTier(0, 1);
        resumed.Economy.FixtureBuildIronStructure(0, "castle");
        resumed.Economy.FixtureSetSoldiersSupplied(0, 4);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) live.Economy.WriteSnapshot(writer);
        stream.Position = 0;
        using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true)) resumed.Economy.ReadSnapshot(reader);

        Assert.True(resumed.Economy.ConcentricCastleBuilt(0));
        Assert.True(resumed.Economy.CastleGarrisoned(0));
        Assert.Equal(live.Economy.ComputeChecksum(), resumed.Economy.ComputeChecksum());
    }
}
