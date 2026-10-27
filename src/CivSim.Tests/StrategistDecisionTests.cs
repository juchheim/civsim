using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>
/// The Strategist's rules on hand-built views (docs/MVP/14 §6, §9.2, §10.4): what it decides depends only on the view
/// it is given (own state, belief, own research), so each rule can be checked exactly.
/// </summary>
public class StrategistDecisionTests
{
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(20261019));
    private const int W = 160;
    private static int T(int x, int y) => y * W + x;

    /// <summary>A view of civilization 0 with one settlement of 14 fighters at (40,40), contacted with civilization 1.</summary>
    private sealed class Scene
    {
        public readonly OwnView Own = new();
        public readonly Belief Belief;
        public readonly MapKnowledge Map;
        public readonly StrategicIntent Intent = new();
        public readonly AiLog Log = new();
        public readonly List<string> Done = new() { "WAR-01", "WAR-02", "HNT-02", "HNT-03" };
        public Difficulty Level = Difficulty.Warlord;
        public EconomyArchetype Archetype = EconomyArchetype.Balanced;
        public int Season = 97; // a raid two seasons out lands in autumn, when stores peak

        public Scene()
        {
            Map = new MapKnowledge(World.Value.Tiles.ToArray(), W, World.Value.Height);
            Belief = new Belief(0, 4, TestPack.Catalog.Count, Map);
            Own.Civ = 0; Own.Era = 1; Own.ReachTiles = 9; Own.Population = 40; Own.Adults = 16; Own.Posture = Posture.Balanced;
            Own.Settlements.Add(new OwnSettlement
            {
                Index = 0, Tile = T(40, 40), Population = 40, Adults = 16, Fighters = 14, DefendingStrength = Fix64.FromInt(14),
                AttackingStrength = Fix64.FromRatio(224, 10), FoodStores = Fix64.FromInt(27), FoodSecurity = Fix64.One,
                LateWinterForecast = Fix64.One, Cohesion = Fix64.One, Defenses = Defenses.None,
            });
            Own.Others = new PairView[4];
            for (int c = 0; c < 4; c++)
                Own.Others[c] = new PairView { Civ = c, LastGiftSeason = -1000, LastBarterSeason = -1000, LastRaidOnThemSeason = -1000, LastRaidOnUsSeason = -1000 };
            Contact(1, relations: 0);
        }

        public void Contact(int civ, int relations)
        {
            PairView p = Own.Others[civ];
            p.Aware = p.Contacted = true;
            p.Relations = relations;
            RivalBelief r = Belief.Rivals[civ];
            r.Aware = r.Contacted = true;
            r.Relations = relations;
            r.Era = 1;
            r.WeaponStrength = Fix64.FromRatio(13, 10);
        }

        public void ExploreAll()
        {
            for (int tile = 0; tile < Map.TileCount; tile++) Map.MarkExplored(tile);
        }

        public BeliefSettlement Site(int civ, int settlement, int tile, int fighters, bool palisade = false, bool watchers = false,
            int seenAgo = 0, bool stoneWalls = false, bool distress = false)
        {
            var b = new BeliefSettlement
            {
                Settlement = settlement, Tile = tile, LastSeenSeason = Season - seenAgo, PopulationEstimate = fighters * 3,
                FightersEstimate = fighters, DefendingStrengthEstimate = Fix64.FromInt(fighters) * Fix64.FromRatio(78, 100),
                PalisadeSeen = palisade, WatchersSeen = watchers, StoneWallsSeen = stoneWalls, DistressSeen = distress,
            };
            Belief.Rivals[civ].Settlements.Add(b);
            Belief.Rivals[civ].PopulationLow = Belief.Rivals[civ].PopulationHigh = Belief.Rivals[civ].Settlements.Sum(s => s.PopulationEstimate);
            return b;
        }

        /// <summary>A village seen only as a head count, as below the Full opponent model: no fighters, no strength, no defences.</summary>
        public BeliefSettlement BlindSite(int civ, int settlement, int tile, int population)
        {
            var b = new BeliefSettlement { Settlement = settlement, Tile = tile, LastSeenSeason = Season, PopulationEstimate = population };
            Belief.Rivals[civ].Settlements.Add(b);
            Belief.Rivals[civ].PopulationLow = Belief.Rivals[civ].PopulationHigh = Belief.Rivals[civ].Settlements.Sum(x => x.PopulationEstimate);
            return b;
        }

        public StrategistView View() => new()
        {
            Civ = 0, Season = Season, Tick = Season * 150L, MatchSeed = 7, Spec = new AiSpec(Level, Archetype),
            Profile = DifficultyProfile.From(TestPack.Pack, Level), Own = Own, Belief = Belief, Intent = Intent, Catalog = TestPack.Catalog,
            OwnDone = p => Done.Contains(TestPack.Catalog.Projects[p].Id), Log = Log,
        };

        public IEnumerable<AiDecision> Notes(string kind) => Log.Entries.Where(e => e.Kind == kind);
    }

    private static (Strategist, ActionRecorder) Decide(Scene scene, Strategist? mind = null)
    {
        mind ??= new Strategist(new AiSpec(scene.Level, scene.Archetype), DifficultyProfile.From(TestPack.Pack, scene.Level));
        var act = new ActionRecorder();
        mind.Decide(scene.View(), act);
        return (mind, act);
    }

    [Fact]
    public void Coalition_embargoes_only_tin_to_the_believed_leader_after_key3_once()
    {
        var scene = new Scene { Season = 120 };
        scene.Own.Era = 3;
        scene.Own.ClaimedTiles = 100;
        scene.Done.Add("KEY-3");
        scene.Contact(2, relations: 0);
        scene.Contact(3, relations: 0);
        // The belief shows civ 1 with 400 known tiles, civ 2 with 150, civ 0 with 100, and civ 3 with 80.
        for (int tile = 0; tile < scene.Map.TileCount; tile++)
        {
            scene.Map.MarkExplored(tile);
            int owner = tile < 400 ? 1 : tile < 550 ? 2 : tile < 650 ? 0 : tile < 730 ? 3 : 1;
            scene.Map.RecordOwner(tile, owner, scene.Season);
        }
        scene.Belief.Rivals[1].TinAccessSeen = true;
        scene.Own.Others[1].TinEmbargoed = false;

        (Strategist mind, ActionRecorder first) = Decide(scene);
        Assert.Equal(GrandPlan.CoalitionAgainstLeader, mind.Plan);
        Assert.Contains("embargo 1 Tin True", first.Orders);
        Assert.Contains(scene.Notes("Embargo"), n => n.Subject == 1 && n.Other == (int)TradeGood.Tin);

        // The own-state view records the existing embargo; another season does not reissue or relog it.
        scene.Own.Others[1].TinEmbargoed = true;
        (_, ActionRecorder second) = Decide(scene, mind);
        Assert.DoesNotContain(second.Orders, o => o.StartsWith("embargo", StringComparison.Ordinal));
    }

    [Fact]
    public void Coalition_tin_embargo_requires_belief_evidence_of_the_leaders_tin_route()
    {
        var scene = new Scene { Season = 120 };
        scene.Own.Era = 3;
        scene.Own.ClaimedTiles = 100;
        scene.Done.Add("KEY-3");
        scene.Contact(2, relations: 0);
        scene.Contact(3, relations: 0);
        for (int tile = 0; tile < scene.Map.TileCount; tile++)
        {
            scene.Map.MarkExplored(tile);
            int owner = tile < 400 ? 1 : tile < 550 ? 2 : tile < 650 ? 0 : tile < 730 ? 3 : 1;
            scene.Map.RecordOwner(tile, owner, scene.Season);
        }

        (Strategist mind, ActionRecorder unsupported) = Decide(scene);
        Assert.Equal(GrandPlan.CoalitionAgainstLeader, mind.Plan);
        Assert.DoesNotContain(unsupported.Orders, o => o.StartsWith("embargo", StringComparison.Ordinal));

        // A seen tin caravan or sighted tin mine camp makes the route evidence sufficient.
        scene.Belief.Rivals[1].SeenFlags |= SeenFlag.TinMineCamp;
        (_, ActionRecorder supported) = Decide(scene, mind);
        Assert.Contains("embargo 1 Tin True", supported.Orders);
    }

    [Fact]
    public void Coalition_does_not_embargo_before_key3_or_without_current_leader_target()
    {
        var scene = new Scene { Season = 120 };
        scene.Own.Era = 3;
        scene.Own.ClaimedTiles = 100;
        scene.Contact(2, relations: 0);
        scene.Contact(3, relations: 0);
        for (int tile = 0; tile < scene.Map.TileCount; tile++)
        {
            scene.Map.MarkExplored(tile);
            int owner = tile < 400 ? 1 : tile < 550 ? 2 : tile < 650 ? 0 : tile < 730 ? 3 : 1;
            scene.Map.RecordOwner(tile, owner, scene.Season);
        }

        (_, ActionRecorder locked) = Decide(scene);
        Assert.DoesNotContain(locked.Orders, o => o.StartsWith("embargo", StringComparison.Ordinal));
    }

    [Fact]
    public void Rejects_a_marginal_raid_after_comparing_it_with_a_palisaded_target()
    {
        var scene = new Scene();
        scene.Site(1, settlement: 5, tile: T(52, 40), fighters: 22, palisade: true, watchers: true);
        // Rich enough that the raid still pays on the second look (64 trials), not only on a lucky first one.
        scene.Site(1, settlement: 6, tile: T(28, 40), fighters: 7);
        (_, ActionRecorder act) = Decide(scene);
        Assert.DoesNotContain(act.Orders, o => o.StartsWith("raid", StringComparison.Ordinal));
        AiDecision note = Assert.Single(scene.Notes("RaidRejected"));
        Assert.Contains("below", note.Reason);

        // The palisaded village alone is not raided, and the log says why.
        var walled = new Scene();
        walled.Site(1, settlement: 5, tile: T(52, 40), fighters: 22, palisade: true, watchers: true);
        (_, ActionRecorder none) = Decide(walled);
        Assert.DoesNotContain(none.Orders, o => o.StartsWith("raid", StringComparison.Ordinal));
        AiDecision rejected = Assert.Single(walled.Notes("RaidRejected"));
        Assert.Contains("palisade seen", rejected.Reason);
        Assert.True(rejected.Score < Fix64.FromRatio(45, 100) || rejected.Reason.Contains("below the margin"), rejected.Reason);
    }

    [Fact]
    public void Raid_shortlist_keeps_near_targets_and_reserves_one_slot_for_the_weakest_belief()
    {
        var origin = new OwnSettlement();
        Strategist.RaidTargetOption Target(int id, int distance, int fighters, bool palisade = false, bool distress = false) =>
            new(new Site
            {
                Settlement = id, Fighters = fighters, PerDefend = Fix64.One,
                Defenses = new Defenses(false, palisade, false, false, false, false, false, false, false, false),
                Distress = distress,
            }, origin, distance);

        var candidates = new[]
        {
            Target(1, 2, 20, palisade: true),
            Target(2, 3, 18, palisade: true),
            Target(3, 4, 10),
            Target(4, 5, 5),
            Target(5, 6, 5, distress: true),
        };

        List<Strategist.RaidTargetOption> selected = Strategist.SelectRaidTargets(candidates, 3);
        List<Strategist.RaidTargetOption> reversed = Strategist.SelectRaidTargets(candidates.Reverse().ToArray(), 3);

        Assert.Equal(new[] { 1, 2, 5 }, selected.Select(x => x.Site.Settlement));
        Assert.Equal(selected.Select(x => x.Site.Settlement), reversed.Select(x => x.Site.Settlement));
        Assert.Equal(3, selected.Count);
    }

    [Fact]
    public void War_shortlist_ignores_stale_sites_and_breaks_equal_defense_by_existing_near_order()
    {
        Site Target(int id, int stale, int fighters) => new()
        {
            Settlement = id, Stale = stale, Fighters = fighters, PerDefend = Fix64.One,
        };
        Site[] nearestFirst =
        {
            Target(1, StrategistTuning.StaleSeasons + 1, 1),
            Target(2, 0, 20),
            Target(3, 0, 18),
            Target(4, 0, 5),
            Target(5, 0, 5),
        };

        List<Site> selected = Strategist.SelectWarTargets(nearestFirst, 3);

        Assert.Equal(new[] { 2, 3, 4 }, selected.Select(site => site.Settlement));
    }

    [Fact]
    public void Raid_decision_can_choose_a_weak_target_beyond_the_nearest_three_sightings()
    {
        var scene = new Scene();
        scene.Site(1, settlement: 5, tile: T(42, 40), fighters: 22, palisade: true, watchers: true);
        scene.Site(1, settlement: 6, tile: T(43, 40), fighters: 22, palisade: true, watchers: true);
        scene.Site(1, settlement: 7, tile: T(44, 40), fighters: 22, palisade: true, watchers: true);
        scene.Site(1, settlement: 8, tile: T(52, 40), fighters: 6);
        scene.Contact(1, relations: -60);

        (_, ActionRecorder act) = Decide(scene);

        Assert.True(act.Orders.Any(order => order.StartsWith("raid 8 ", StringComparison.Ordinal)),
            $"orders: {string.Join(";", act.Orders)}; rejected: {string.Join(" | ", scene.Notes("RaidRejected").Select(note => note.Reason))}");
        Assert.Contains(scene.Notes("Raid"), note => note.Subject == 8);
    }

    [Fact]
    public void Does_not_send_a_four_or_five_person_party_against_a_seen_palisade_and_watchers()
    {
        // B20 trace shape: a small home garrison can spare only four or five fighters, while the freshly seen target
        // has 22 defenders plus a palisade and watchers. This is a correctly impossible raid; increasing the party
        // by weakening the home reserve or relaxing the success margin would turn this into an unsafe launch.
        var scene = new Scene();
        OwnSettlement origin = scene.Own.Settlements[0];
        origin.Fighters = 9;
        origin.AttackingStrength = Fix64.FromRatio(144, 10);
        scene.Site(1, settlement: 5, tile: T(52, 40), fighters: 22, palisade: true, watchers: true);

        (_, ActionRecorder act) = Decide(scene);

        Assert.DoesNotContain(act.Orders, o => o.StartsWith("raid", StringComparison.Ordinal));
        Assert.DoesNotContain(act.Orders, o => o.StartsWith("war", StringComparison.Ordinal));
        Assert.DoesNotContain(act.Orders, o => o.StartsWith("assault", StringComparison.Ordinal));
        AiDecision rejected = Assert.Single(scene.Notes("RaidRejected"));
        Assert.True(rejected.Score < Fix64.FromRatio(5, 100), rejected.Reason);
        Assert.Contains("palisade seen", rejected.Reason);
        Assert.Contains("watchers seen", rejected.Reason);
        Assert.Contains("with 5", rejected.Reason);
    }

    [Fact]
    public void A_blind_Chieftain_estimates_a_villages_defenders_from_its_head_count()
    {
        // Seen only as a head count (no fighters, no strength), a village of 18 is modeled with six fighters at 0.6 of our
        // strength, with era-typical works. The confirmed calibrated probability and EV clear the standard raid margins.
        var medium = new Scene { Level = Difficulty.Chieftain };
        medium.BlindSite(1, settlement: 6, tile: T(28, 40), population: 18);
        (_, ActionRecorder act) = Decide(medium);
        Assert.Contains("raid 6 7", act.Orders);
        AiDecision launched = Assert.Single(medium.Notes("Raid"));
        Assert.True(launched.Score >= Fix64.FromRatio(45, 100), launched.Reason);
        Assert.True(launched.Score <= StrategistTuning.RaidSuccessCalibration, launched.Reason);

        // A tiny one does not pay for the risk...
        var tiny = new Scene { Level = Difficulty.Chieftain };
        tiny.BlindSite(1, settlement: 6, tile: T(28, 40), population: 9);
        Assert.DoesNotContain(Decide(tiny).Item2.Orders, o => o.StartsWith("raid", StringComparison.Ordinal));
        Assert.Contains(tiny.Notes("RaidRejected"), n => n.Reason.Contains("below the margin"));

        // ...and a big one cannot be beaten: a third of 60 people is 20 fighters against our five or seven.
        var big = new Scene { Level = Difficulty.Chieftain };
        big.BlindSite(1, settlement: 6, tile: T(28, 40), population: 60);
        Assert.DoesNotContain(Decide(big).Item2.Orders, o => o.StartsWith("raid", StringComparison.Ordinal));
        Assert.Contains(big.Notes("RaidRejected"), n => n.Reason.Contains("rejected"));
    }

    [Fact]
    public void Requests_a_levy_when_a_safe_host_makes_an_otherwise_unviable_raid_pay()
    {
        var scene = new Scene();
        scene.ExploreAll();
        scene.Contact(1, relations: 0);
        scene.Own.Settlements.Add(new OwnSettlement
        {
            Index = 4, Tile = T(42, 40), Population = 40, Adults = 16, Fighters = 14,
            AttackingStrength = Fix64.FromRatio(224, 10), DefendingStrength = Fix64.FromInt(14),
            FoodSecurity = Fix64.One, LateWinterForecast = Fix64.One,
        });
        scene.Site(1, settlement: 5, tile: T(52, 40), fighters: 20);
        var act = new ActionRecorder { Mandate = 1 };

        new Strategist(new AiSpec(scene.Level, scene.Archetype), DifficultyProfile.From(TestPack.Pack, scene.Level))
            .Decide(scene.View(), act);

        Assert.Contains("decree Levy 5 0", act.Orders);
        AiDecision request = Assert.Single(scene.Notes("RaidLevy"));
        Assert.Contains("with 14 levy host", request.Reason);
        Assert.DoesNotContain(act.Orders, order => order.StartsWith("raid 5 ", StringComparison.Ordinal));
    }

    [Fact]
    public void Raids_a_freshly_seen_mining_camp_when_its_open_ground_forecast_pays()
    {
        var scene = new Scene();
        scene.Belief.Rivals[1].MiningCampsSeen.Add(new BeliefMiningCamp
        {
            Id = 42, Tile = T(52, 40), LastSeenSeason = scene.Season, CrewEstimate = 2, Deposit = DepositKind.CopperOre,
        });

        (_, ActionRecorder act) = Decide(scene);

        Assert.Contains(act.Orders, order => order.StartsWith("raid-mine 42 ", StringComparison.Ordinal));
        Assert.Contains(scene.Notes("Raid"), note => note.Subject == 42 && note.Other == 1 && note.Reason.Contains("mining camp 42"));
    }

    [Fact]
    public void Does_not_raid_a_stale_mining_camp_sighting()
    {
        var scene = new Scene();
        scene.Belief.Rivals[1].MiningCampsSeen.Add(new BeliefMiningCamp
        {
            Id = 42, Tile = T(52, 40), LastSeenSeason = scene.Season - StrategistTuning.StaleSeasons - 1,
            CrewEstimate = 2, Deposit = DepositKind.CopperOre,
        });

        (_, ActionRecorder act) = Decide(scene);

        Assert.DoesNotContain(act.Orders, order => order.StartsWith("raid-mine ", StringComparison.Ordinal));
    }

    [Fact]
    public void Does_not_request_a_levy_when_posture_or_an_existing_raid_blocks_launch()
    {
        Scene MakeScene()
        {
            var scene = new Scene();
            scene.ExploreAll();
            scene.Own.Settlements.Add(new OwnSettlement
            {
                Index = 4, Tile = T(42, 40), Population = 40, Adults = 16, Fighters = 14,
                AttackingStrength = Fix64.FromRatio(224, 10), DefendingStrength = Fix64.FromInt(14),
                FoodSecurity = Fix64.One, LateWinterForecast = Fix64.One,
            });
            scene.Site(1, settlement: 5, tile: T(52, 40), fighters: 20);
            return scene;
        }

        Scene fortified = MakeScene();
        fortified.Own.Posture = Posture.Fortify;
        var fortifyActions = new ActionRecorder { Mandate = 1 };
        new Strategist(new AiSpec(fortified.Level, fortified.Archetype), DifficultyProfile.From(TestPack.Pack, fortified.Level))
            .Decide(fortified.View(), fortifyActions);
        Assert.DoesNotContain(fortifyActions.Orders, order => order.StartsWith("decree Levy", StringComparison.Ordinal));

        Scene busy = MakeScene();
        busy.Own.RaidsOnTheRoad = 1;
        var busyActions = new ActionRecorder { Mandate = 1 };
        new Strategist(new AiSpec(busy.Level, busy.Archetype), DifficultyProfile.From(TestPack.Pack, busy.Level))
            .Decide(busy.View(), busyActions);
        Assert.DoesNotContain(busyActions.Orders, order => order.StartsWith("decree Levy", StringComparison.Ordinal));
    }

    [Fact]
    public void Does_not_forecast_a_levy_across_an_unexplored_route()
    {
        var scene = new Scene();
        scene.Own.Settlements.Add(new OwnSettlement
        {
            Index = 4, Tile = T(42, 40), Population = 40, Adults = 16, Fighters = 14,
            AttackingStrength = Fix64.FromRatio(224, 10), DefendingStrength = Fix64.FromInt(14),
            FoodSecurity = Fix64.One, LateWinterForecast = Fix64.One,
        });
        scene.Site(1, settlement: 5, tile: T(52, 40), fighters: 20);
        scene.Map.MarkExplored(T(42, 40));
        scene.Map.MarkExplored(T(52, 40));
        var actions = new ActionRecorder { Mandate = 1 };

        Decide(scene).Item1.Decide(scene.View(), actions);

        Assert.DoesNotContain(actions.Orders, order => order.StartsWith("decree Levy", StringComparison.Ordinal));
    }

    [Fact]
    public void Fresh_sighting_overrides_the_rival_wide_palisade_technology_prior()
    {
        var scene = new Scene();
        scene.Site(1, settlement: 5, tile: T(52, 40), fighters: 20);
        scene.Belief.Rivals[1].ProjectBelieved[TestPack.Catalog.IndexOf("DEF-03")] = true;

        Decide(scene);

        Assert.Contains(scene.Notes("RaidRejected"), note => !note.Reason.Contains("palisade seen"));
    }

    [Fact]
    public void Uses_the_exact_active_levy_host_when_forecasting_a_matching_raid()
    {
        var scene = new Scene();
        scene.Site(1, settlement: 5, tile: T(52, 40), fighters: 20);
        var levy = new OwnLevy { TargetSettlement = 5 };
        for (int i = 0; i < 14; i++) levy.Members.Add(new Fighter(1_000 + i, Fix64.FromRatio(184, 100)));
        levy.Departures.Add(0, 10);
        scene.Own.Levies.Add(levy);
        var act = new ActionRecorder();

        new Strategist(new AiSpec(scene.Level, scene.Archetype), DifficultyProfile.From(TestPack.Pack, scene.Level))
            .Decide(scene.View(), act);

        Assert.Contains("raid 5 14", act.Orders);
        AiDecision request = Assert.Single(scene.Notes("Raid"));
        Assert.Contains("14 levy host", request.Reason);
    }

    [Fact]
    public void Does_not_forecast_a_normal_party_when_a_matching_levy_is_depleted()
    {
        var scene = new Scene();
        scene.Site(1, settlement: 5, tile: T(52, 40), fighters: 8);
        var levy = new OwnLevy { TargetSettlement = 5 };
        for (int i = 0; i < 3; i++) levy.Members.Add(new Fighter(1_000 + i, Fix64.FromRatio(184, 100)));
        scene.Own.Levies.Add(levy);
        var act = new ActionRecorder();

        Decide(scene).Item1.Decide(scene.View(), act);

        Assert.DoesNotContain(act.Orders, order => order.StartsWith("raid 5 ", StringComparison.Ordinal));
        Assert.DoesNotContain(act.Orders, order => order.StartsWith("decree Levy", StringComparison.Ordinal));
    }

    [Fact]
    public void Counterattack_forecast_counts_an_active_levy_departure_from_home()
    {
        var scene = new Scene();
        scene.Site(1, settlement: 5, tile: T(52, 40), fighters: 20);
        scene.Contact(2, relations: -60);
        scene.Own.Others[2].AtWar = true;
        scene.Belief.Rivals[2].Era = 2;
        scene.Site(2, settlement: 10, tile: T(41, 40), fighters: 50);
        var levy = new OwnLevy { TargetSettlement = 5 };
        for (int i = 0; i < 14; i++) levy.Members.Add(new Fighter(1_000 + i, Fix64.FromRatio(184, 100)));
        levy.Departures.Add(0, 10);
        scene.Own.Levies.Add(levy);
        var act = new ActionRecorder();

        Decide(scene).Item1.Decide(scene.View(), act);

        Assert.DoesNotContain(act.Orders, order => order.StartsWith("raid 5 ", StringComparison.Ordinal));
        Assert.Contains(scene.Notes("RaidRejected"), note => note.Reason.Contains("could take settlement"));
    }

    [Fact]
    public void A_blind_Chieftain_assumes_a_late_era_village_has_works_it_cannot_see()
    {
        // The rival's era is public. A village of 18 that is worth raiding in an early era is not once it is believed to have
        // a thorn hedge, lookouts and a palisade: the same head count, the same fighters, a lower success.
        var early = new Scene { Level = Difficulty.Chieftain };
        early.BlindSite(1, settlement: 6, tile: T(28, 40), population: 18);
        early.Belief.Rivals[1].Era = 1;
        Decide(early);
        var late = new Scene { Level = Difficulty.Chieftain };
        late.BlindSite(1, settlement: 6, tile: T(28, 40), population: 18);
        late.Belief.Rivals[1].Era = 3;
        (_, ActionRecorder act) = Decide(late);
        Assert.DoesNotContain(act.Orders, o => o.StartsWith("raid", StringComparison.Ordinal));
        Assert.Contains(late.Notes("RaidRejected"), n => n.Reason.Contains("rejected"));
        Fix64 earlyP = Assert.Single(early.Notes("Raid")).Score;
        Assert.True(late.Notes("RaidRejected").Max(n => n.Score) < earlyP);
    }

    [Fact]
    public void Raid_probability_estimates_use_enough_trials_for_candidate_selection()
    {
        foreach (Difficulty level in Enum.GetValues<Difficulty>())
        {
            DifficultyProfile profile = DifficultyProfile.From(TestPack.Pack, level);
            Assert.True(StrategistTuning.Trials(profile) >= 32, $"{level}: {StrategistTuning.Trials(profile)} trials");
        }

        // The larger fixed sample remains deterministic when the Strategist compares several raid candidates.
        var first = new Scene();
        first.Site(1, settlement: 5, tile: T(52, 40), fighters: 22, palisade: true, watchers: true);
        first.Site(1, settlement: 6, tile: T(28, 40), fighters: 6);
        var second = new Scene();
        second.Site(1, settlement: 5, tile: T(52, 40), fighters: 22, palisade: true, watchers: true);
        second.Site(1, settlement: 6, tile: T(28, 40), fighters: 6);
        (_, ActionRecorder firstAct) = Decide(first);
        (_, ActionRecorder secondAct) = Decide(second);
        Assert.Equal(firstAct.Orders, secondAct.Orders);
        Assert.Equal(first.Notes("RaidRejected").Single().Score, second.Notes("RaidRejected").Single().Score);
    }

    [Fact]
    public void Raid_probability_uses_sighted_E3_walls_and_the_bronze_barrier_rules()
    {
        var open = new Scene();
        open.Belief.Rivals[1].Era = 3;
        open.Site(1, settlement: 6, tile: T(28, 40), fighters: 6);
        (_, ActionRecorder openAct) = Decide(open);
        Assert.Contains(openAct.Orders, o => o.StartsWith("raid", StringComparison.Ordinal));
        AiDecision openEstimate = Assert.Single(open.Notes("Raid"));

        var walled = new Scene();
        walled.Belief.Rivals[1].Era = 3;
        walled.Site(1, settlement: 6, tile: T(28, 40), fighters: 6, stoneWalls: true);
        (_, ActionRecorder walledAct) = Decide(walled);
        Assert.DoesNotContain(walledAct.Orders, o => o.StartsWith("raid", StringComparison.Ordinal));
        AiDecision wallEstimate = Assert.Single(walled.Notes("RaidRejected"));
        Assert.True(wallEstimate.Score < openEstimate.Score, $"open {openEstimate.Score}; walls {wallEstimate.Score}");
    }

    [Fact]
    public void The_head_count_estimate_is_only_for_levels_that_cannot_see_fighters()
    {
        // At Full, a village believed to have no fighters stays undefended; its small stores do not justify the social cost.
        var scene = new Scene { Level = Difficulty.Warlord };
        scene.BlindSite(1, settlement: 6, tile: T(28, 40), population: 9);
        Assert.DoesNotContain(Decide(scene).Item2.Orders, o => o.StartsWith("raid", StringComparison.Ordinal));
        Assert.Contains(scene.Notes("RaidRejected"), n => n.Reason.Contains("expected value"));
    }

    [Fact]
    public void Never_raids_on_a_stale_sighting()
    {
        var scene = new Scene();
        scene.Site(1, settlement: 6, tile: T(28, 40), fighters: 6, seenAgo: 3);
        (_, ActionRecorder act) = Decide(scene);
        Assert.DoesNotContain(act.Orders, o => o.StartsWith("raid", StringComparison.Ordinal));
        Assert.Contains(scene.Notes("RaidRejected"), n => n.Reason.Contains("do not raid blind"));
    }

    [Fact]
    public void Strategist_intercepts_a_fresh_visible_caravan_using_its_legal_caravan_order()
    {
        var scene = new Scene();
        scene.Site(1, settlement: 5, tile: T(52, 40), fighters: 22, palisade: true, watchers: true);
        scene.Belief.Rivals[1].CaravansSeen.Add(new BeliefCaravan
        {
            Civ = 1, Id = 73, Tile = T(42, 40), LastSeenSeason = scene.Season, ArrivalSeason = scene.Season + 2,
            CrewEstimate = 2, Good = TradeGood.Tin, Cargo = Fix64.FromInt(8), CargoBarterValue = Fix64.FromInt(320),
        });

        (_, ActionRecorder act) = Decide(scene);

        Assert.Contains(act.Orders, o => o.StartsWith("raid-caravan 73 ", StringComparison.Ordinal));
        Assert.DoesNotContain(act.Orders, o => o.StartsWith("raid 5 ", StringComparison.Ordinal));
        AiDecision launched = Assert.Single(scene.Notes("CaravanRaid"));
        Assert.True(launched.Score >= Fix64.FromRatio(35, 100), launched.Reason);
        Assert.Contains("intercept", launched.Reason);
    }

    [Fact]
    public void Strategist_does_not_intercept_a_stale_caravan_or_one_outside_its_arrival_window()
    {
        var stale = new Scene();
        stale.Belief.Rivals[1].CaravansSeen.Add(new BeliefCaravan
        {
            Civ = 1, Id = 74, Tile = T(42, 40), LastSeenSeason = stale.Season - 1, ArrivalSeason = stale.Season + 2,
            CrewEstimate = 2, Good = TradeGood.Tin, CargoBarterValue = Fix64.FromInt(320),
        });
        Assert.DoesNotContain(Decide(stale).Item2.Orders, o => o.StartsWith("raid-caravan", StringComparison.Ordinal));

        var tooFar = new Scene();
        tooFar.Belief.Rivals[1].CaravansSeen.Add(new BeliefCaravan
        {
            Civ = 1, Id = 75, Tile = T(52, 40), LastSeenSeason = tooFar.Season, ArrivalSeason = tooFar.Season + 1,
            CrewEstimate = 2, Good = TradeGood.Tin, CargoBarterValue = Fix64.FromInt(320),
        });
        Assert.DoesNotContain(Decide(tooFar).Item2.Orders, o => o.StartsWith("raid-caravan", StringComparison.Ordinal));
    }

    [Fact]
    public void Never_raids_without_Raiding_or_under_Fortify()
    {
        var scene = new Scene();
        scene.Site(1, settlement: 6, tile: T(28, 40), fighters: 6);
        scene.Done.Remove("WAR-02");
        Assert.DoesNotContain(Decide(scene).Item2.Orders, o => o.StartsWith("raid", StringComparison.Ordinal));
        var calm = new Scene();
        calm.Site(1, settlement: 6, tile: T(28, 40), fighters: 6);
        calm.Own.Posture = Posture.Fortify;
        Assert.DoesNotContain(Decide(calm).Item2.Orders, o => o.StartsWith("raid", StringComparison.Ordinal));
        Assert.Contains(calm.Notes("RaidRejected"), n => n.Reason.Contains("Fortify"));
    }

    [Fact]
    public void Does_not_raid_into_a_winter_its_own_band_cannot_survive()
    {
        var scene = new Scene();
        scene.Site(1, settlement: 6, tile: T(28, 40), fighters: 6);
        scene.Own.Settlements[0].LateWinterForecast = Fix64.FromRatio(6, 10);
        (_, ActionRecorder act) = Decide(scene);
        Assert.DoesNotContain(act.Orders, o => o.StartsWith("raid", StringComparison.Ordinal));
        Assert.Contains(scene.Notes("RaidRejected"), n => n.Reason.Contains("late-winter food"));
    }

    /// <summary>A hostile neighbour on Raid posture with spears, 12 tiles away.</summary>
    private static void Raider(Scene scene, int civ = 1)
    {
        scene.Contact(civ, relations: -50);
        RivalBelief r = scene.Belief.Rivals[civ];
        r.ProjectBelieved[TestPack.Catalog.IndexOf("WAR-02")] = true;
        r.RaidPostureSeen = true;
        r.WeaponStrength = Fix64.FromRatio(16, 10);
        scene.Site(civ, settlement: 5, tile: T(52, 40), fighters: 20);
    }

    [Fact]
    public void A_raider_neighbour_raises_defense_urgency_and_Fortify()
    {
        var scene = new Scene();
        scene.Done.Add("GOV-02");
        scene.Own.Settlements[0].Fighters = 8;
        scene.Own.Settlements[0].DefendingStrength = Fix64.FromInt(6);
        Raider(scene);
        (_, ActionRecorder act) = Decide(scene);
        Assert.Contains("posture Fortify", act.Orders);
        Assert.True(scene.Intent.DefenseUrgency >= StrategistTuning.FortifyAt);
        Assert.Contains(scene.Notes("Threat"), n => n.Subject == 1);
        Assert.Contains(scene.Notes("Defense"), n => n.Subject == 1 && n.Reason.Contains("Fortify"));
    }

    [Fact]
    public void The_Settler_reacts_to_a_threat_two_seasons_late_and_the_Warlord_at_once()
    {
        foreach (Difficulty level in new[] { Difficulty.Settler, Difficulty.Warlord })
        {
            var scene = new Scene { Level = level };
            scene.Own.Settlements[0].Fighters = 8;
            scene.Own.Settlements[0].DefendingStrength = Fix64.FromInt(6);
            Strategist? mind = null;
            var urgency = new List<Fix64>();
            for (int i = 0; i < 4; i++)
            {
                if (i == 1) Raider(scene); // the threat appears in the second season
                (mind, _) = Decide(scene, mind);
                urgency.Add(scene.Intent.DefenseUrgency);
                scene.Season++;
            }
            Assert.Equal(Fix64.Zero, urgency[0]);
            if (level == Difficulty.Warlord) Assert.True(urgency[1] > Fix64.FromRatio(3, 10), $"warlord {urgency[1]}");
            else
            {
                Assert.Equal(Fix64.Zero, urgency[1]);
                Assert.Equal(Fix64.Zero, urgency[2]);
                Assert.True(urgency[3] > Fix64.FromRatio(3, 10), $"settler {urgency[3]}");
            }
        }
    }

    [Fact]
    public void Feasible_conquest_outranks_secure_tin_without_changing_combat_safety_gates()
    {
        var scene = new Scene { Archetype = EconomyArchetype.Sage };
        scene.Own.Era = 3;
        scene.Done.Add("MET-03");
        scene.Own.Settlements[0].Fighters = 40;
        scene.Own.Settlements[0].Adults = 40;
        scene.Own.Settlements[0].AttackingStrength = Fix64.FromRatio(108, 1);
        scene.Own.Settlements[0].DefendingStrength = Fix64.FromInt(40);
        scene.Own.Adults = 40;
        scene.Own.Others[1].Relations = scene.Belief.Rivals[1].Relations = -60;
        scene.Site(1, settlement: 6, tile: T(52, 40), fighters: 6);

        (Strategist mind, _) = Decide(scene);

        Assert.Equal(GrandPlan.Conquer, mind.Plan);
        Assert.Contains(scene.Notes("Plan"), note => note.Reason.Contains("secure tin 1.20") &&
            note.Reason.Contains("conquer 1.35"));
        Assert.Contains(scene.Notes("War"), note => note.Reason.Contains("value "));
    }

    [Fact]
    public void Declares_war_only_with_the_force_ratio_and_makes_peace_when_losing()
    {
        // Strong and hostile toward a small camp: war and an assault in the same season.
        var scene = new Scene { Archetype = EconomyArchetype.Warlord };
        scene.Own.Settlements[0].Fighters = 30;
        scene.Own.Settlements[0].Adults = 30;
        scene.Own.Settlements[0].AttackingStrength = Fix64.FromInt(30) * Fix64.FromRatio(27, 10);
        scene.Own.Others[1].Relations = scene.Belief.Rivals[1].Relations = -60;
        scene.Site(1, settlement: 6, tile: T(28, 40), fighters: 6);
        (_, ActionRecorder act) = Decide(scene);
        Assert.Contains("war 1", act.Orders);
        Assert.Contains("assault 6", act.Orders);
        AiDecision war = Assert.Single(scene.Notes("War"));
        Assert.True(war.Score >= StrategistTuning.WarRatio, war.Reason);
        Assert.Equal(GrandPlan.Conquer, scene.Intent.Plan);

        // Same hostility, equal strength: no war, and the refusal names the ratio.
        var even = new Scene { Archetype = EconomyArchetype.Warlord };
        even.Own.Others[1].Relations = even.Belief.Rivals[1].Relations = -60;
        even.Site(1, settlement: 6, tile: T(28, 40), fighters: 14);
        (_, ActionRecorder none) = Decide(even);
        Assert.DoesNotContain(none.Orders, o => o.StartsWith("war", StringComparison.Ordinal));
        Assert.Contains(even.Notes("WarRejected"), n => n.Reason.Contains("ratio"));

        // At war with a stronger rival: peace at once.
        var losing = new Scene();
        losing.Own.Others[1].AtWar = losing.Belief.Rivals[1].AtWar = true;
        losing.Own.Others[1].Relations = -40;
        losing.Site(1, settlement: 5, tile: T(52, 40), fighters: 30);
        (_, ActionRecorder peace) = Decide(losing);
        Assert.Contains("peace 1", peace.Orders);
        Assert.Contains(losing.Notes("Peace"), n => n.Reason.Contains("cannot win"));
    }

    [Fact]
    public void Campaign_gathers_a_support_settlement_within_the_shared_radius()
    {
        var scene = new Scene { Archetype = EconomyArchetype.Warlord };
        OwnSettlement origin = scene.Own.Settlements[0];
        origin.Fighters = 11;
        origin.Adults = 11;
        origin.AttackingStrength = Fix64.FromInt(11) * Fix64.FromRatio(27, 10);
        origin.DefendingStrength = Fix64.FromInt(11);
        scene.Own.Adults = 16;
        scene.Own.Population = 30;
        scene.Own.Settlements.Add(new OwnSettlement
        {
            Index = 1, Tile = T(51, 40), Population = 15, Adults = 5, Fighters = 5,
            AttackingStrength = Fix64.FromInt(5) * Fix64.FromRatio(27, 10),
            DefendingStrength = Fix64.FromInt(5), FoodSecurity = Fix64.One,
            LateWinterForecast = Fix64.One, Cohesion = Fix64.One, Defenses = Defenses.None,
        });
        scene.Own.Others[1].Relations = scene.Belief.Rivals[1].Relations = -60;
        scene.Site(1, settlement: 6, tile: T(28, 40), fighters: 2);

        (_, ActionRecorder act) = Decide(scene);

        Assert.Equal(11, HexGrid.Distance(origin.Tile, scene.Own.Settlements[1].Tile, W));
        Assert.True(StrategistTuning.ArmyGatherRange >= 11);
        Assert.Contains("war 1", act.Orders);
        Assert.Contains("assault 6", act.Orders);
        Assert.Contains(scene.Notes("Assault"), note => note.Reason.Contains("with 7"));
    }

    [Fact]
    public void War_threshold_distinguishes_inferred_palisades_from_observed_ones()
    {
        static Scene MakeBlind(int ownFighters)
        {
            var scene = new Scene { Level = Difficulty.Chieftain, Archetype = EconomyArchetype.Warlord };
            OwnSettlement own = scene.Own.Settlements[0];
            own.Fighters = ownFighters;
            own.Adults = ownFighters;
            own.AttackingStrength = Fix64.FromInt(ownFighters) * Fix64.FromRatio(27, 10);
            own.DefendingStrength = Fix64.FromInt(ownFighters);
            own.LateWinterForecast = Fix64.One;
            scene.Own.Adults = ownFighters;
            scene.Own.Population = ownFighters;
            scene.Own.Others[1].Relations = scene.Belief.Rivals[1].Relations = -60;
            scene.BlindSite(1, settlement: 6, tile: T(28, 40), population: 18);
            scene.Belief.Rivals[1].Era = 3;
            return scene;
        }

        // At 30 own fighters the same blind Era 3 site lands just below 1.8 after applying its inferred walls.
        Scene below = MakeBlind(30);
        (_, ActionRecorder belowAct) = Decide(below);
        Assert.DoesNotContain(belowAct.Orders, order => order.StartsWith("war ", StringComparison.Ordinal));
        AiDecision belowNote = Assert.Single(below.Notes("WarRejected"));
        Assert.Contains("ratio 1.79 (need 1.80, palisade)", belowNote.Reason);

        // At 32 own fighters the full inferred defense model is retained, but its 1.8 planning gate permits the campaign.
        Scene inferred = MakeBlind(32);
        (_, ActionRecorder inferredAct) = Decide(inferred);
        Assert.Contains("war 1", inferredAct.Orders);
        Assert.Contains("assault 6", inferredAct.Orders);
        AiDecision inferredWar = Assert.Single(inferred.Notes("War"));
        Assert.Contains("need 1.80, palisade", inferredWar.Reason);
        Assert.True(inferredWar.Score > StrategistTuning.WarRatioInferredPalisade);
        Assert.True(inferredWar.Score < StrategistTuning.WarRatioPalisade);

        // The same roughly 1.89 ratio remains below the 2.0 gate when a Full opponent has actually seen the palisade.
        Scene observed = new Scene { Level = Difficulty.Warlord, Archetype = EconomyArchetype.Warlord };
        OwnSettlement observedOwn = observed.Own.Settlements[0];
        observedOwn.Fighters = 32;
        observedOwn.Adults = 32;
        observedOwn.AttackingStrength = Fix64.FromInt(32) * Fix64.FromRatio(27, 10);
        observedOwn.DefendingStrength = Fix64.FromInt(32);
        observedOwn.LateWinterForecast = Fix64.One;
        observed.Own.Adults = observed.Own.Population = 32;
        observed.Own.Others[1].Relations = observed.Belief.Rivals[1].Relations = -60;
        BeliefSettlement seen = observed.Site(1, settlement: 6, tile: T(28, 40), fighters: 6, palisade: true);
        seen.DefendingStrengthEstimate = Fix64.FromInt(19) * Fix64.FromRatio(27, 10) * StrategistTuning.PartyPickBonus
            / Fix64.Parse("1.89") / Fix64.FromRatio(22, 10) * Fix64.FromRatio(6, 7);
        observed.Belief.Rivals[1].Era = 3;

        (_, ActionRecorder observedAct) = Decide(observed);
        Assert.DoesNotContain(observedAct.Orders, order => order.StartsWith("war ", StringComparison.Ordinal));
        AiDecision observedNote = Assert.Single(observed.Notes("WarRejected"));
        Assert.Contains("need 2.00, palisade", observedNote.Reason);
        Assert.True(observedNote.Score >= Fix64.FromRatio(188, 100) && observedNote.Score <= Fix64.FromRatio(190, 100),
            $"Observed-palisade ratio was {observedNote.Score}");
    }

    [Fact]
    public void War_search_reserves_one_evaluation_for_the_weakest_site_beyond_the_nearest_three()
    {
        var scene = new Scene { Archetype = EconomyArchetype.Warlord };
        OwnSettlement own = scene.Own.Settlements[0];
        own.Fighters = 30;
        own.Adults = 30;
        own.AttackingStrength = Fix64.FromInt(30) * Fix64.FromRatio(27, 10);
        own.DefendingStrength = Fix64.FromInt(80);
        scene.Own.Adults = 30;
        scene.Own.Others[1].Relations = scene.Belief.Rivals[1].Relations = -60;
        scene.Belief.Rivals[1].WeaponStrength = Fix64.FromRatio(1, 10); // weak field army; strong sites below remain costly to assault

        foreach ((int id, int x) in new[] { (5, 42), (6, 43), (7, 44) })
        {
            BeliefSettlement defended = scene.Site(1, settlement: id, tile: T(x, 40), fighters: 22);
            defended.DefendingStrengthEstimate = Fix64.FromInt(440);
        }
        scene.Site(1, settlement: 8, tile: T(45, 40), fighters: 6);

        (_, ActionRecorder act) = Decide(scene);

        Assert.Contains("war 1", act.Orders);
        Assert.Contains("assault 8", act.Orders);
        Assert.Contains(scene.Notes("Assault"), note => note.Subject == 8);
    }

    [Fact]
    public void Counterattack_forecast_does_not_pool_distant_rival_garrisons()
    {
        Scene MakeScene(int secondClusterX)
        {
            var scene = new Scene { Archetype = EconomyArchetype.Warlord };
            OwnSettlement own = scene.Own.Settlements[0];
            own.Fighters = 30;
            own.Adults = 30;
            own.AttackingStrength = Fix64.FromInt(81);
            own.DefendingStrength = Fix64.FromInt(30);
            scene.Own.Adults = 30;
            scene.Own.Others[1].Relations = scene.Belief.Rivals[1].Relations = -60;

            scene.Site(1, settlement: 6, tile: T(42, 40), fighters: 6);
            BeliefSettlement nearby = scene.Site(1, settlement: 7, tile: T(40, 42), fighters: 10);
            BeliefSettlement second = scene.Site(1, settlement: 8, tile: T(secondClusterX, 40), fighters: 10);
            // These sites contribute fighters to a counterattack, but are too strongly defended to become alternate war targets.
            nearby.DefendingStrengthEstimate = second.DefendingStrengthEstimate = Fix64.FromInt(100);
            nearby.PopulationEstimate = second.PopulationEstimate = 1;
            scene.Belief.Rivals[1].PopulationLow = scene.Belief.Rivals[1].PopulationHigh = 20;
            return scene;
        }

        Scene dispersed = MakeScene(secondClusterX: 53);
        (_, ActionRecorder canGatherOneCluster) = Decide(dispersed);
        Assert.Contains("war 1", canGatherOneCluster.Orders);
        Assert.Contains("assault 6", canGatherOneCluster.Orders);

        // The same two garrisons can veto the launch when they are close enough to join one campaign.
        Scene gathered = MakeScene(secondClusterX: 45);
        (_, ActionRecorder cannotGatherTwoClusters) = Decide(gathered);
        Assert.DoesNotContain(cannotGatherTwoClusters.Orders, order => order.StartsWith("war ", StringComparison.Ordinal));
        Assert.Contains(gathered.Notes("WarRejected"), note => note.Reason.Contains("could take our settlement 0"));
    }

    [Theory]
    [InlineData(35, 1, 2)]
    [InlineData(151, 2, 3)]
    public void War_capture_forecast_counts_arrival_season_relief_before_each_holding_season(
        int population, int holdingSeasons, int expectedReliefChecks)
    {
        Assert.Equal(holdingSeasons, Captures.OccupationSeasons(population));
        Assert.Equal(expectedReliefChecks, Strategist.OccupationReliefChecks(population));
    }

    [Fact]
    public void Plans_only_supplied_investments_against_seen_distressed_walls_with_margin()
    {
        var scene = new Scene { Archetype = EconomyArchetype.Warlord };
        scene.Own.Settlements[0].Fighters = 30;
        scene.Own.Settlements[0].Adults = 30;
        scene.Own.Settlements[0].AttackingStrength = Fix64.FromInt(30) * Fix64.FromRatio(27, 10);
        scene.Own.Others[1].Relations = scene.Belief.Rivals[1].Relations = -60;
        foreach (int tile in Enumerable.Range(0, scene.Map.TileCount)) scene.Map.MarkExplored(tile);
        BeliefSettlement target = scene.Site(1, settlement: 6, tile: T(34, 40), fighters: 6, stoneWalls: true, distress: true);

        (_, ActionRecorder act) = Decide(scene);
        Assert.Contains("assault 6", act.Orders); // The simulation maps this order to an investment at Stone Walls.
        AiDecision war = Assert.Single(scene.Notes("War"));
        Assert.Contains("Stone Walls: investment", war.Reason);
        Assert.Contains("supplied", war.Reason);
        Assert.Contains("stores are unobserved", war.Reason);
        Assert.True(war.Score >= Fix64.FromRatio(15, 10), war.Reason);
        Assert.True(target.StoneWallsSeen);

        var unsupplied = new Scene { Archetype = EconomyArchetype.Warlord };
        unsupplied.Own.Settlements[0].Fighters = 30;
        unsupplied.Own.Settlements[0].Adults = 30;
        unsupplied.Own.Settlements[0].AttackingStrength = Fix64.FromInt(30) * Fix64.FromRatio(27, 10);
        unsupplied.Own.Others[1].Relations = unsupplied.Belief.Rivals[1].Relations = -60;
        unsupplied.Site(1, settlement: 6, tile: T(34, 40), fighters: 6, stoneWalls: true, distress: true);
        (_, ActionRecorder none) = Decide(unsupplied);
        Assert.DoesNotContain(none.Orders, o => o.StartsWith("war", StringComparison.Ordinal));
        Assert.Contains(unsupplied.Notes("WarRejected"), n => n.Reason.Contains("no known supply path"));

        var noDistress = new Scene { Archetype = EconomyArchetype.Warlord };
        noDistress.Own.Settlements[0].Fighters = 30;
        noDistress.Own.Settlements[0].Adults = 30;
        noDistress.Own.Settlements[0].AttackingStrength = Fix64.FromInt(30) * Fix64.FromRatio(27, 10);
        noDistress.Own.Others[1].Relations = noDistress.Belief.Rivals[1].Relations = -60;
        foreach (int tile in Enumerable.Range(0, noDistress.Map.TileCount)) noDistress.Map.MarkExplored(tile);
        noDistress.Site(1, settlement: 6, tile: T(34, 40), fighters: 6, stoneWalls: true);
        (_, ActionRecorder rejected) = Decide(noDistress);
        Assert.DoesNotContain(rejected.Orders, o => o.StartsWith("war", StringComparison.Ordinal));
        Assert.Contains(noDistress.Notes("WarRejected"), n => n.Reason.Contains("no distress evidence"));
    }

    [Fact]
    public void Gifts_a_stronger_neighbour_at_first_contact_and_barters_when_both_gain()
    {
        var scene = new Scene();
        scene.Done.AddRange(new[] { "TRD-01", "TRD-02" });
        scene.Own.Settlements[0].FoodStores = Fix64.FromInt(45); // enough for the gift and a full barter cargo
        scene.Own.Others[1].Relations = scene.Belief.Rivals[1].Relations = 30;
        scene.Belief.Rivals[1].ProjectBelieved[TestPack.Catalog.IndexOf("TRD-02")] = true;
        scene.Site(1, settlement: 5, tile: T(52, 40), fighters: 25);
        (_, ActionRecorder act) = Decide(scene);
        Assert.Contains("gift 1", act.Orders);
        Assert.Contains("barter 1", act.Orders);
        Assert.Contains(scene.Notes("Gift"), n => n.Reason.Contains("first contact"));
    }

    [Fact]
    public void Allies_against_a_shared_threat_only()
    {
        Scene Build(bool threat)
        {
            var scene = new Scene();
            scene.Done.Add("DIP-03");
            scene.Own.Others[1].Relations = scene.Belief.Rivals[1].Relations = 50;
            scene.Belief.Rivals[1].ProjectBelieved[TestPack.Catalog.IndexOf("DIP-03")] = true;
            scene.Site(1, settlement: 5, tile: T(28, 40), fighters: 10);
            if (threat) { Raider(scene, civ: 2); scene.Own.Settlements[0].Fighters = 8; scene.Own.Settlements[0].DefendingStrength = Fix64.FromInt(6); }
            return scene;
        }
        var shared = Build(threat: true);
        Assert.Contains("ally 1", Decide(shared).Item2.Orders);
        Assert.Contains(shared.Notes("Alliance"), n => n.Reason.Contains("shared threat"));
        Assert.DoesNotContain("ally 1", Decide(Build(threat: false)).Item2.Orders);
    }

    [Fact]
    public void Places_a_Beacon_on_open_land_toward_the_rival()
    {
        var scene = new Scene();
        scene.Own.BeaconAllowance = 1;
        for (int t = 0; t < scene.Belief.Map.TileCount; t++) scene.Belief.Map.MarkExplored(t);
        int rival = T(66, 40);
        scene.Site(1, settlement: 5, tile: rival, fighters: 10);
        (_, ActionRecorder act) = Decide(scene);
        string order = Assert.Single(act.Orders, o => o.StartsWith("beacon", StringComparison.Ordinal));
        int tile = int.Parse(order.Split(' ')[1]);
        int own = scene.Own.Settlements[0].Tile;
        Assert.InRange(HexGrid.Distance(own, tile, W), StrategistTuning.BeaconMinDistance, StrategistTuning.BeaconMaxDistance);
        Assert.True(HexGrid.Distance(rival, tile, W) >= StrategistTuning.BeaconRivalKeepOff);
        // A useful deposit (tin on the contested midline, since E3) may pull the Beacon elsewhere; otherwise it leans toward the rival.
        bool onDeposit = World.Value.TileAt(tile).Deposit is DepositKind.Tin or DepositKind.NativeCopper or DepositKind.CopperOre
            or DepositKind.Coal or DepositKind.Salt or DepositKind.Flint;
        Assert.True(onDeposit || HexGrid.Distance(rival, tile, W) < HexGrid.Distance(rival, own, W), "the Beacon leans toward the rival");
        Assert.Single(scene.Notes("Beacon"));
    }

    [Fact]
    public void Strategist_prioritizes_a_known_tin_placer_until_the_tin_bronze_route_is_secured()
    {
        Assert.Equal(StrategistTuning.BeaconTinRouteBonus,
            Strategist.BronzeTinBeaconBonus(hasBronzeTools: true, hasTinBronze: false, deposit: DepositKind.Tin));
        Assert.Equal(Fix64.Zero,
            Strategist.BronzeTinBeaconBonus(hasBronzeTools: false, hasTinBronze: false, deposit: DepositKind.Tin));
        Assert.Equal(Fix64.Zero,
            Strategist.BronzeTinBeaconBonus(hasBronzeTools: true, hasTinBronze: true, deposit: DepositKind.Tin));
        Assert.Equal(Fix64.Zero,
            Strategist.BronzeTinBeaconBonus(hasBronzeTools: true, hasTinBronze: false, deposit: DepositKind.CopperOre));
    }

    [Fact]
    public void Decisions_are_a_function_of_the_view_and_the_state_saves_exactly()
    {
        var a = new Scene();
        a.Site(1, settlement: 6, tile: T(28, 40), fighters: 6);
        Raider(a, civ: 2);
        (Strategist first, ActionRecorder actA) = Decide(a);
        var b = new Scene();
        b.Site(1, settlement: 6, tile: T(28, 40), fighters: 6);
        Raider(b, civ: 2);
        (Strategist second, ActionRecorder actB) = Decide(b);
        Assert.Equal(actA.Orders, actB.Orders);
        Assert.Equal(a.Log.Entries.Select(e => e.Reason), b.Log.Entries.Select(e => e.Reason));

        // Round trip the state, then both minds decide the next season identically.
        var copy = new Strategist(new AiSpec(Difficulty.Warlord, EconomyArchetype.Balanced), DifficultyProfile.From(TestPack.Pack, Difficulty.Warlord));
        using (var ms = new MemoryStream())
        {
            using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) first.Write(w);
            ms.Position = 0;
            using var r = new BinaryReader(ms);
            copy.Read(r);
        }
        Assert.Equal(HashOf(first), HashOf(copy));
        a.Season++; b.Season++;
        (_, ActionRecorder nextA) = Decide(a, first);
        (_, ActionRecorder nextB) = Decide(b, copy);
        Assert.Equal(nextA.Orders, nextB.Orders);
        Assert.Equal(HashOf(first), HashOf(copy));
    }

    [Fact]
    public void The_plan_does_not_dither_when_the_threat_flickers()
    {
        var scene = new Scene();
        scene.Own.Settlements[0].Fighters = 8;
        scene.Own.Settlements[0].DefendingStrength = Fix64.FromInt(6);
        Strategist? mind = null;
        for (int i = 0; i < 24; i++)
        {
            // A rival that is hostile one season and friendly the next, for six years.
            scene.Belief.Rivals[1].Settlements.Clear();
            if (i % 2 == 0) Raider(scene); else scene.Contact(1, relations: 40);
            (mind, _) = Decide(scene, mind);
            scene.Season++;
        }
        StrategistAudit.Result audit = StrategistAudit.Run(0, scene.Log.Entries, Array.Empty<PoliticsEvent>());
        Assert.Equal(0, audit.Dithering);
    }

    [Fact]
    public void Fortify_posture_keeps_watch_and_changes_nothing_else()
    {
        var rig = new StrategistRig(20261019, World.Value, new[] { CivControl.Council, CivControl.Council, CivControl.Council, CivControl.Council });
        PoliticsSystem p = rig.Politics;
        Defenses open = Defenses.None;
        Assert.Equal(open, p.WithPostureDefenses(0, open));
        rig.Grant(0, "GOV-02");
        Assert.True(p.OrderPosture(0, Posture.Fortify));
        Defenses watched = p.WithPostureDefenses(0, open);
        Assert.True(watched.Watchers && watched.AlarmCalls);
        Assert.True(Raids.SurpriseChance(watched) < Raids.SurpriseChance(open));
        Assert.Equal(open, p.WithPostureDefenses(1, open));
    }

    private static ulong HashOf(Strategist s)
    {
        var h = new StateHasher();
        s.Hash(ref h);
        return h.Finish();
    }
}
