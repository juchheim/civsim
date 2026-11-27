using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>The Columbian Exchange, FOR-10 (M13 WP-E6-6 slice 5, docs/MVP/38 §5.2): new crops after contact across an ocean, and the fevers that come with them. Discovery only.</summary>
public sealed class DiscoveryExchangeTests
{
    private const ulong Seed = 20261024;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric("KEY-6", "SEA-11", "SEA-12", "FOR-10"));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner) Rig(ChapterRules rules)
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules);
        return (match, new SimRunner(match));
    }

    private static void Grant(SimRunner runner, MatchModel match, int civ, params string[] projects)
    {
        foreach (string project in projects)
        {
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, match.Research.Catalog.IndexOf(project), 0, 0));
            runner.Advance(2);
        }
    }

    private static int Home(MatchModel match, int civ)
        => Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == civ && !match.Politics.IsFreeFolk(s));

    [Fact]
    public void The_exchange_needs_FOR10_and_contact_across_an_ocean_and_not_a_neighbor_on_the_same_land()
    {
        (MatchModel match, SimRunner runner) = Rig(ChapterRules.Discovery);
        Grant(runner, match, 0, "KEY-5", "KEY-6", "SEA-04", "SEA-12");
        match.Politics.ForceContact(0, 1);
        Assert.False(match.Economy.FixtureInExchange(0), "a neighbor on the same land is not an ocean");
        Grant(runner, match, 0, "FOR-10");
        Assert.False(match.Economy.FixtureInExchange(0));
        Assert.False(match.Politics.OverseasExchange(0));
        match.Politics.ForceContact(0, 2);
        runner.Advance(LivingEconomyModel.SeasonTicks); // the answer is worked out once a season
        Assert.True(match.Politics.OverseasExchange(0), "civilization 2 lives across the sea");
        Assert.True(match.Economy.FixtureInExchange(0));
        Assert.False(match.Economy.FixtureInExchange(3), "a civilization that met no one across the sea and lacks FOR-10");
    }

    [Fact]
    public void Contact_alone_without_FOR10_changes_nothing()
    {
        (MatchModel match, SimRunner runner) = Rig(ChapterRules.Discovery);
        Grant(runner, match, 0, "KEY-5", "KEY-6", "SEA-04", "SEA-12");
        match.Politics.ForceContact(0, 2);
        runner.Advance(LivingEconomyModel.SeasonTicks);
        Assert.True(match.Politics.OverseasExchange(0));
        Assert.False(match.Economy.FixtureInExchange(0));
        Assert.Equal(Fix64.One, match.Economy.FixtureExchangeFeverFactor(0));
    }

    [Fact]
    public void An_imported_staple_lifts_fields_only_where_it_grows_and_the_fevers_come_with_it()
    {
        (MatchModel match, SimRunner runner) = Rig(ChapterRules.Discovery);
        int home = Home(match, 0);
        int grass = Enumerable.Range(0, World.Value.TileCount).First(t => World.Value.TileAt(t).Biome == Biome.GrasslandSteppe);
        int desert = Enumerable.Range(0, World.Value.TileCount).First(t => World.Value.TileAt(t).Biome is Biome.Desert or Biome.Wetland or Biome.Ice or Biome.Peak or Biome.Ocean);
        Grant(runner, match, 0, "KEY-5", "KEY-6", "SEA-04", "SEA-12", "FOR-10");
        match.Politics.ForceContact(0, 2);
        runner.Advance(LivingEconomyModel.SeasonTicks);
        Assert.True(match.Economy.FixtureInExchange(0));

        match.Economy.FixtureMoveSite(home, grass);
        Assert.Equal(Fix64.FromRatio(12, 10), match.Economy.FixtureExchangeCropYield(home));
        match.Economy.FixtureMoveSite(home, desert);
        Assert.Equal(Fix64.One, match.Economy.FixtureExchangeCropYield(home));
        Assert.Equal(Fix64.FromRatio(115, 100), match.Economy.FixtureExchangeFeverFactor(0));
        Assert.True(Fix64.One == match.Economy.FixtureExchangeFeverFactor(1), "a civilization outside the exchange has no new fevers");
    }

    [Fact]
    public void The_imported_crop_drawn_is_the_one_the_biome_pays_for_and_none_outside_the_exchange()
    {
        (MatchModel match, SimRunner runner) = Rig(ChapterRules.Discovery);
        int home = Home(match, 0);
        int grass = Enumerable.Range(0, World.Value.TileCount).First(t => World.Value.TileAt(t).Biome == Biome.GrasslandSteppe);
        Grant(runner, match, 0, "KEY-5", "KEY-6", "SEA-04", "SEA-12", "FOR-10");
        match.Economy.FixtureMoveSite(home, grass);
        Assert.Equal(0, match.Economy.ExchangeCropAt(home)); // knowledge alone, no contact across the sea
        match.Politics.ForceContact(0, 2);
        runner.Advance(LivingEconomyModel.SeasonTicks);
        match.Economy.FixtureMoveSite(home, grass);
        Assert.Equal(1, match.Economy.ExchangeCropAt(home));
        int upland = Enumerable.Range(0, World.Value.TileCount).First(t => World.Value.TileAt(t).Biome is Biome.Taiga or Biome.Tundra or Biome.Mountain);
        match.Economy.FixtureMoveSite(home, upland);
        Assert.Equal(2, match.Economy.ExchangeCropAt(home));
        int desert = Enumerable.Range(0, World.Value.TileCount).First(t => World.Value.TileAt(t).Biome is Biome.Desert or Biome.Wetland or Biome.Ice or Biome.Peak or Biome.Ocean);
        match.Economy.FixtureMoveSite(home, desert);
        Assert.Equal(0, match.Economy.ExchangeCropAt(home));
        Assert.True(match.Economy.ExchangeCropAt(Home(match, 1)) == 0, "a civilization outside the exchange grows no imported crop");
    }

    [Fact]
    public void The_exchange_does_nothing_in_Fief()
    {
        (MatchModel match, SimRunner runner) = Rig(ChapterRules.Fief);
        Grant(runner, match, 0, "KEY-5", "KEY-6", "SEA-04", "SEA-12", "FOR-10"); // the ceiling refuses the era-six completions
        match.Politics.ForceContact(0, 2);
        runner.Advance(LivingEconomyModel.SeasonTicks);
        Assert.False(match.Politics.OverseasExchange(0));
        Assert.False(match.Economy.FixtureInExchange(0));
        Assert.Equal(Fix64.One, match.Economy.FixtureExchangeCropYield(Home(match, 0)));
        Assert.Equal(Fix64.One, match.Economy.FixtureExchangeFeverFactor(0));
    }
}
