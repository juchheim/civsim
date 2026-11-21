using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>HNT-12 deep-sea fishing fleets (M13 WP-E6-6 slice 4, docs/MVP/38 §5.2): a harbor town fishes open water at three times the rate. Discovery chapter only.</summary>
public sealed class DiscoveryFleetTests
{
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric("KEY-6", "SEA-07", "HNT-12"));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    /// <summary>A match with settlement 0 moved to a coast with open water two or three tiles out (the starts are inland), and one such water tile.</summary>
    private static (MatchModel Match, SimRunner Runner, int Settlement, int Tile)? Find(ChapterRules rules)
    {
        GeneratedWorld world = WorldGenerator.Generate(20261024);
        MatchModel match = MatchModel.Create(20261024, Pack.Value, world, Balanced, default, rules: rules);
        (int site, int water) = match.Economy.FixtureFindFleetSite();
        if (site < 0) return null;
        match.Economy.FixtureMoveSite(0, site);
        return (match, new SimRunner(match), 0, water);
    }

    private static void Grant(SimRunner runner, MatchModel match, int civ, params string[] projects)
    {
        foreach (string project in projects)
        {
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, match.Research.Catalog.IndexOf(project), 0, 0));
            runner.Advance(2);
        }
    }

    [Fact]
    public void A_harbor_town_with_HNT12_fishes_open_water_at_three_times_the_rate()
    {
        var found = Find(ChapterRules.Discovery);
        Assert.NotNull(found);
        (MatchModel match, SimRunner runner, int s, int tile) = found!.Value;
        LivingEconomyModel economy = match.Economy;
        Fix64 inshore = economy.FixtureFishTileRate(s, tile, Fix64.One);
        Assert.False(economy.FixtureOffshoreFleet(s));

        int civ = match.Economy.SettlementLite(s).Civilization;
        Grant(runner, match, civ, "KEY-5", "KEY-6", "SEA-07", "HNT-12");
        Assert.False(economy.FixtureOffshoreFleet(s), "no harbor, no fleet");
        economy.FixtureBuildIronStructure(s, "harbor");
        Assert.True(economy.FixtureOffshoreFleet(s));
        Assert.True(economy.FixtureCanFishTile(s, tile), "the fleet reaches open water the shore fishers cannot");
        Assert.Equal(Fix64.FromInt(3), economy.FixtureFishTileRate(s, tile, Fix64.One));
        Assert.Equal(Fix64.One, inshore); // before HNT-12 this water is out of reach and keeps the plain rate
    }

    [Fact]
    public void Offshore_banks_regrow_to_a_cap_only_in_Discovery()
    {
        var found = Find(ChapterRules.Discovery);
        Assert.NotNull(found);
        (MatchModel match, SimRunner runner, int s, _) = found!.Value;
        LivingEconomyModel economy = match.Economy;
        int bank = economy.FixtureRangeTiles(s).ToArray().First(t => economy.FixtureIsFishBank(t));
        Assert.Equal(Fix64.Zero, economy.FixtureFishStock(bank));
        runner.Advance(LivingEconomyModel.SeasonTicks * 8);
        Fix64 grown = economy.FixtureFishStock(bank);
        Assert.True(grown > Fix64.Zero, "an unfished bank fills");
        Assert.True(grown <= Fix64.Parse("7.2"), "up to its cap and no further");

        var fiefFound = Find(ChapterRules.Fief);
        Assert.NotNull(fiefFound);
        (MatchModel fief, SimRunner fiefRunner, int fs, _) = fiefFound!.Value;
        int open = fief.Economy.FixtureRangeTiles(fs).ToArray().First(t => fief.Economy.FixtureFleetWater(fs, t) && fief.Economy.FixtureFishStock(t) == Fix64.Zero);
        fiefRunner.Advance(LivingEconomyModel.SeasonTicks * 8);
        Assert.False(fief.Economy.FixtureIsFishBank(open), "no banks outside Discovery");
        Assert.Equal(Fix64.Zero, fief.Economy.FixtureFishStock(open));
    }

    [Fact]
    public void The_fleet_needs_the_project_and_does_nothing_in_Fief()
    {
        var found = Find(ChapterRules.Discovery);
        Assert.NotNull(found);
        (MatchModel match, SimRunner runner, int s, _) = found!.Value;
        Grant(runner, match, match.Economy.SettlementLite(s).Civilization, "KEY-5", "KEY-6", "SEA-07");
        match.Economy.FixtureBuildIronStructure(s, "harbor");
        Assert.False(match.Economy.FixtureOffshoreFleet(s), "a harbor alone sends no fleet");

        var fiefFound = Find(ChapterRules.Fief);
        Assert.NotNull(fiefFound);
        (MatchModel fief, SimRunner fiefRunner, int fs, _) = fiefFound!.Value;
        Grant(fiefRunner, fief, fief.Economy.SettlementLite(fs).Civilization, "KEY-5", "KEY-6", "SEA-07", "HNT-12"); // the ceiling refuses the era-six completion
        fief.Economy.FixtureBuildIronStructure(fs, "harbor");
        Assert.False(fief.Economy.FixtureOffshoreFleet(fs));
    }
}
