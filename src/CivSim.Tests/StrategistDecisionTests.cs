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
        scene.Site(1, settlement: 6, tile: T(28, 40), fighters: 6);
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
    public void A_blind_Chieftain_estimates_a_villages_defenders_from_its_head_count()
    {
        // Seen only as a head count (no fighters, no strength), a village of 18 is modeled with six fighters believed as strong
        // as ours (times 1.3) against our seven. The calibrated success probability lowers expected loot below the EV margin.
        var medium = new Scene { Level = Difficulty.Chieftain };
        medium.BlindSite(1, settlement: 6, tile: T(28, 40), population: 18);
        (_, ActionRecorder act) = Decide(medium);
        Assert.DoesNotContain(act.Orders, o => o.StartsWith("raid 6 ", StringComparison.Ordinal));
        AiDecision note = Assert.Single(medium.Notes("RaidRejected"));
        Assert.Contains("expected value", note.Reason);
        Assert.True(note.Score < Fix64.FromRatio(75, 100), note.Reason);

        // A tiny one does not pay for the risk...
        var tiny = new Scene { Level = Difficulty.Chieftain };
        tiny.BlindSite(1, settlement: 6, tile: T(28, 40), population: 9);
        Assert.DoesNotContain(Decide(tiny).Item2.Orders, o => o.StartsWith("raid", StringComparison.Ordinal));
        Assert.Contains(tiny.Notes("RaidRejected"), n => n.Reason.Contains("below the margin"));

        // ...and a big one cannot be beaten: a third of 60 people is 20 fighters against our five or seven.
        var big = new Scene { Level = Difficulty.Chieftain };
        big.BlindSite(1, settlement: 6, tile: T(28, 40), population: 60);
        Assert.DoesNotContain(Decide(big).Item2.Orders, o => o.StartsWith("raid", StringComparison.Ordinal));
        Assert.Contains(big.Notes("RaidRejected"), n => n.Reason.Contains("believed success"));
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
        // At Full, a village believed to have no fighters is believed to have none: the old reading is unchanged there.
        var scene = new Scene { Level = Difficulty.Warlord };
        scene.BlindSite(1, settlement: 6, tile: T(28, 40), population: 9);
        Assert.DoesNotContain(Decide(scene).Item2.Orders, o => o.StartsWith("raid", StringComparison.Ordinal));
        Assert.Contains(scene.Notes("RaidRejected"), n => n.Reason.Contains("success 0.00"));
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
        scene.Own.Others[1].Relations = scene.Belief.Rivals[1].Relations = 10;
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
