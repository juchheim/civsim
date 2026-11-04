using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>The Strategist's Iron-chapter levers on hand-built views: tribute, blockade, league (docs/MVP/25 §11). Decisions rest on own state and belief alone.</summary>
public class StrategistIronTests
{
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(20261019));
    private const int W = 160;
    private static int T(int x, int y) => y * W + x;

    private sealed class Scene
    {
        public readonly OwnView Own = new();
        public readonly Belief Belief;
        public readonly List<string> Done = new() { "WAR-01", "WAR-02", "HNT-02", "HNT-03" };
        public bool Iron = true;
        public int Season = 96;

        public Scene()
        {
            var map = new MapKnowledge(World.Value.Tiles.ToArray(), W, World.Value.Height);
            Belief = new Belief(0, 4, TestPack.Catalog.Count, map);
            Own.Civ = 0; Own.Era = 3; Own.ReachTiles = 15; Own.Population = 200; Own.Adults = 90; Own.Posture = Posture.Balanced;
            Own.Settlements.Add(new OwnSettlement
            {
                Index = 0, Tile = T(40, 40), Population = 200, Adults = 90, Fighters = 60, DefendingStrength = Fix64.FromInt(60),
                AttackingStrength = Fix64.FromInt(120), FoodStores = Fix64.FromInt(200), FoodSecurity = Fix64.One,
                LateWinterForecast = Fix64.One, Cohesion = Fix64.One, Defenses = Defenses.None,
            });
            Own.Others = new PairView[4];
            for (int c = 0; c < 4; c++)
                Own.Others[c] = new PairView { Civ = c, LastGiftSeason = -1000, LastBarterSeason = -1000, LastRaidOnThemSeason = -1000, LastRaidOnUsSeason = -1000 };
            Own.Others[1].Aware = Own.Others[1].Contacted = true;
            RivalBelief r = Belief.Rivals[1];
            r.Aware = r.Contacted = true;
            r.Era = 3;
            r.WeaponStrength = Fix64.FromRatio(13, 10);
            Belief.Rivals[1].Settlements.Add(new BeliefSettlement
            {
                Settlement = 5, Tile = T(48, 40), LastSeenSeason = Season, PopulationEstimate = 60, FightersEstimate = 14,
                DefendingStrengthEstimate = Fix64.FromInt(14),
            });
            r.PopulationLow = r.PopulationHigh = 60;
        }

        public void War(bool atWar) { Own.Others[1].AtWar = atWar; Belief.Rivals[1].AtWar = atWar; }
        public void Ally() { Own.Others[1].Allied = true; Belief.Rivals[1].Allied = true; }

        public StrategistView View() => new()
        {
            Civ = 0, Season = Season, Tick = Season * 150L, MatchSeed = 7, Spec = new AiSpec(Difficulty.Warlord, EconomyArchetype.Balanced),
            Profile = DifficultyProfile.From(TestPack.Pack, Difficulty.Warlord), Own = Own, Belief = Belief, Intent = new StrategicIntent(),
            Catalog = TestPack.Catalog, OwnDone = p => Done.Contains(TestPack.Catalog.Projects[p].Id), Log = new AiLog(), IronChapter = Iron,
        };
    }

    /// <summary>Runs consecutive seasons through one Strategist and returns every order it gave, in order.</summary>
    private static List<string> Seasons(Scene scene, int count)
    {
        var mind = new Strategist(new AiSpec(Difficulty.Warlord, EconomyArchetype.Balanced), DifficultyProfile.From(TestPack.Pack, Difficulty.Warlord));
        var all = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var act = new ActionRecorder { PeaceAccepted = false };
            mind.Decide(scene.View(), act);
            all.AddRange(act.Orders);
            scene.Season++;
        }
        return all;
    }

    [Fact]
    public void A_clear_lead_in_a_long_war_offers_tribute_once_in_a_while_and_only_with_DIP_06_in_the_Iron_chapter()
    {
        var scene = new Scene();
        scene.War(true);
        scene.Done.Add("DIP-06");
        List<string> orders = Seasons(scene, 20);
        Assert.Contains("tribute 1", orders);
        Assert.InRange(orders.Count(o => o == "tribute 1"), 1, 3); // a rhythm, not every season

        foreach (Action<Scene> change in new Action<Scene>[] { s => s.Iron = false, s => s.Done.Remove("DIP-06") })
        {
            var other = new Scene();
            other.War(true);
            other.Done.Add("DIP-06");
            change(other);
            Assert.DoesNotContain("tribute 1", Seasons(other, 20));
        }
        // No clear lead: the rival's believed strength is close to ours.
        var even = new Scene();
        even.War(true);
        even.Done.Add("DIP-06");
        even.Own.Settlements[0].AttackingStrength = Fix64.FromInt(24);
        Assert.DoesNotContain("tribute 1", Seasons(even, 20));
    }

    [Fact]
    public void A_galley_is_sent_only_against_a_rival_whose_harbor_has_been_seen()
    {
        var scene = new Scene();
        scene.War(true);
        scene.Done.AddRange(new[] { "SEA-09" });
        scene.Own.CrewedGalleyHomes.Add(0);
        Assert.DoesNotContain(Seasons(scene, 12), o => o.StartsWith("blockade"));
        scene.Belief.Rivals[1].SeenFlags |= SeenFlag.Harbor;
        List<string> orders = Seasons(scene, 12);
        Assert.Contains("blockade 5", orders);
        // No galley, no research, or not the Iron chapter: nothing.
        foreach (Action<Scene> change in new Action<Scene>[] { s => s.Own.CrewedGalleyHomes.Clear(), s => s.Done.Remove("SEA-09"), s => s.Iron = false })
        {
            var other = new Scene();
            other.War(true);
            other.Done.Add("SEA-09");
            other.Own.CrewedGalleyHomes.Add(0);
            other.Belief.Rivals[1].SeenFlags |= SeenFlag.Harbor;
            change(other);
            Assert.DoesNotContain(Seasons(other, 12), o => o.StartsWith("blockade"));
        }
    }

    [Fact]
    public void An_ally_is_offered_a_league_once_a_year_and_only_with_DIP_07()
    {
        var scene = new Scene();
        scene.Ally();
        Assert.DoesNotContain("league 1", Seasons(scene, 24));
        scene.Done.Add("DIP-07");
        List<string> orders = Seasons(scene, 24);
        Assert.InRange(orders.Count(o => o == "league 1"), 1, 2); // every twelfth season
        var bronze = new Scene { Iron = false };
        bronze.Ally();
        bronze.Done.Add("DIP-07");
        Assert.DoesNotContain("league 1", Seasons(bronze, 24));
    }
}
