using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>DEF-10 bastion forts and WAR-13 pike and musket drill (M13 WP-E6-9 slice 1, docs/MVP/38 §5.6; e6_combat_oracle.py). Discovery chapter only.</summary>
public sealed class DiscoveryWarTests
{
    private const ulong Seed = 20261114;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric("KEY-6", "DEF-08", "DEF-09", "DEF-10", "WAR-13", "GOV-12"));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner) Start(ChapterRules rules)
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules);
        return (match, new SimRunner(match));
    }

    private static void Grant(SimRunner runner, MatchModel match, params string[] projects)
    {
        foreach (string project in projects)
        {
            runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, match.Research.Catalog.IndexOf(project), 0, 0));
            runner.Advance(2);
        }
    }

    [Fact]
    public void A_bastion_fort_is_a_garrisoned_castles_alternative_strongpoint_that_stops_where_the_concentric_step_would()
    {
        (MatchModel match, SimRunner runner) = Start(ChapterRules.Discovery);
        LivingEconomyModel economy = match.Economy;
        Grant(runner, match, "KEY-5", "KEY-6", "DEF-08", "DEF-09", "DEF-10");
        economy.FixtureBuildIronStructure(0, "castle");
        economy.FixtureSetSoldiersSupplied(0, 4);
        Defenses castle = match.Politics.FixtureDefensesOf(0);
        Assert.True(castle.Castle && !castle.BastionFort);

        economy.FixtureBuildIronStructure(0, "bastion_fort");
        Defenses bastion = match.Politics.FixtureDefensesOf(0);
        Assert.True(bastion.BastionFort);
        Assert.True(castle.Multiplier() * Defenses.BastionStormMultiplier == bastion.Multiplier());
        Assert.True(Defenses.CastleStrongpoint * Defenses.BastionStormMultiplier == Fix64.FromRatio(15, 8), "1.875, below the 1.9 storm crossover");
        Assert.True(Defenses.BastionWall == Defenses.IntactTowerWall * Fix64.FromRatio(3, 2), "1.5 times a gatehouse wall");

        economy.FixtureSetSoldiersSupplied(0, 0);
        Assert.False(match.Politics.FixtureDefensesOf(0).BastionFort, "an ungarrisoned fort is empty stone");
        economy.FixtureSetSoldiersSupplied(0, 4);
        economy.FixtureBuildIronStructure(0, "concentric_castle");
        Defenses both = match.Politics.FixtureDefensesOf(0);
        Assert.True(both.ConcentricCastle && !both.BastionFort, "the two never stack");
    }

    [Fact]
    public void The_bastion_gate_needs_DEF10_a_garrisoned_castle_and_no_concentric_step()
    {
        (MatchModel match, SimRunner runner) = Start(ChapterRules.Discovery);
        LivingEconomyModel economy = match.Economy;
        Grant(runner, match, "KEY-5", "KEY-6", "DEF-08", "DEF-09");
        economy.FixtureSetTier(0, 1);
        Assert.False(economy.FixtureIronGateOpen(0, "bastion_fort", includeAppetite: false), "no project");
        Grant(runner, match, "DEF-10");
        Assert.False(economy.FixtureIronGateOpen(0, "bastion_fort", includeAppetite: false), "no castle");
        economy.FixtureBuildIronStructure(0, "castle");
        Assert.False(economy.FixtureIronGateOpen(0, "bastion_fort", includeAppetite: false), "no garrison");
        economy.FixtureSetSoldiersSupplied(0, 4);
        Assert.True(economy.FixtureIronGateOpen(0, "bastion_fort", includeAppetite: false));
        economy.FixtureBuildIronStructure(0, "bastion_fort");
        Assert.False(economy.FixtureIronGateOpen(0, "concentric_castle", includeAppetite: false), "the concentric step is the alternative");
        Assert.False(economy.FixtureIronGateOpen(0, "bastion_fort", includeAppetite: false), "one to a town");
    }

    [Fact]
    public void A_bastion_wall_holds_a_three_cannon_train_longer_on_the_integrity_track()
    {
        int Seasons(Fix64 wall)
        {
            Fix64 integrity = Fix64.One;
            int seasons = 0;
            while (integrity > Fix64.Zero && seasons < 20) { integrity = Sieges.Batter(integrity, Fix64.FromInt(6), wall); seasons++; }
            return seasons;
        }
        Assert.Equal(1, Seasons(Defenses.IntactTowerWall));
        Assert.Equal(1, Seasons(Defenses.BastionWall)); // three cannons (six engine-equivalents) still breach in a season
        int Cannon(Fix64 wall) { Fix64 i = Fix64.One; int n = 0; while (i > Fix64.Zero && n < 20) { i = Sieges.Batter(i, Fix64.FromInt(2), wall); n++; } return n; }
        Assert.True(Cannon(Defenses.BastionWall) > Cannon(Defenses.IntactTowerWall), "one cannon needs longer against a bastion than a tower wall");
        Assert.Equal(3, Cannon(Defenses.BastionWall));
    }

    [Fact]
    public void The_bastion_is_Discovery_only()
    {
        (MatchModel fief, SimRunner runner) = Start(ChapterRules.Fief);
        Grant(runner, fief, "KEY-5", "DEF-08", "DEF-10"); // the Fief ceiling refuses the era-six completion
        fief.Economy.FixtureBuildIronStructure(0, "castle");
        fief.Economy.FixtureSetSoldiersSupplied(0, 4);
        Assert.False(fief.Economy.BastionFortBuilt(0));
        Assert.False(fief.Politics.FixtureDefensesOf(0).BastionFort);
    }

    [Fact]
    public void A_drilled_levy_fights_at_one_and_a_half_times_and_needs_WAR13_and_GOV12_in_Discovery()
    {
        (MatchModel plain, SimRunner plainRunner) = Start(ChapterRules.Discovery);
        Grant(plainRunner, plain, "KEY-5", "KEY-6");
        (MatchModel drilled, SimRunner drilledRunner) = Start(ChapterRules.Discovery);
        Grant(drilledRunner, drilled, "KEY-5", "KEY-6", "WAR-13", "GOV-12");
        (MatchModel half, SimRunner halfRunner) = Start(ChapterRules.Discovery);
        Grant(halfRunner, half, "KEY-5", "KEY-6", "WAR-13");

        Fix64 Strength(MatchModel m) => m.Politics.OwnDefendersOf(0, 0).Aggregate(Fix64.Zero, (sum, f) => sum + f.Strength);
        Fix64 before = Strength(plain), after = Strength(drilled);
        Assert.True(after > before, $"drill lifts the levy ({before} to {after})");
        Assert.True(after <= before * Fix64.FromRatio(3, 2) + Fix64.FromRaw(43950), "at most x1.5 of the whole");
        Assert.True(Strength(half) == before, "WAR-13 needs the state (GOV-12)");

        (MatchModel fief, SimRunner fiefRunner) = Start(ChapterRules.Fief);
        Fix64 fiefBefore = Strength(fief);
        Grant(fiefRunner, fief, "KEY-5", "WAR-13", "GOV-12"); // refused by the ceiling
        Assert.True(Strength(fief) == fiefBefore);
    }
}
