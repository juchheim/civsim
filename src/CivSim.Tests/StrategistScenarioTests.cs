using System;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>
/// The Strategist in real matches (docs/MVP/14 §6, §7, §8.4): Strategist AIs on civilizations 0 and 3, the Council on
/// 1 and 2, omniscient beliefs from <see cref="PerfectBeliefBuilder"/>. One shared run to minute 26 (contact comes near
/// minute 20); each scenario resumes from its snapshot and stages the situation with the test hooks.
/// </summary>
public class StrategistScenarioTests
{
    private const ulong Seed = 20261019;
    private const int BaseSeason = 104; // minute 26
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly CivControl Smart = CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced);
    private static readonly CivControl[] Controls = { Smart, CivControl.Council, CivControl.Council, Smart };

    private sealed class Base
    {
        public readonly StrategistRig Rig;
        public readonly byte[] Snapshot;
        public readonly ulong Checksum;
        public Base()
        {
            Rig = new StrategistRig(Seed, World.Value, Controls);
            Rig.Seasons(BaseSeason);
            Snapshot = Rig.Snapshot();
            Checksum = Rig.Match.ComputeChecksum();
        }
    }

    private static readonly Lazy<Base> Shared = new(() => new Base());

    private static StrategistRig Resume() => new(Seed, World.Value, Controls, snapshot: Shared.Value.Snapshot);

    private static readonly string[] Bows = { "WAR-01", "HNT-02", "HNT-03", "HNT-04", "HNT-05", "HNT-06", "WAR-02", "WAR-03", "SOC-05", "WAR-04" };

    [Fact]
    public void After_contact_it_gifts_and_barters_and_logs_its_reasons()
    {
        StrategistRig rig = Shared.Value.Rig;
        int contact = rig.Politics.Events.First(e => e.Kind == PoliticsEventKind.Contact && e.Civ == 0).Tick is long t ? (int)((t + 1) / 150) : -1;
        Assert.InRange(contact, 60, 100);
        Assert.Contains(rig.Notes(0, "Gift"), n => n.Subject == 1 && n.Season >= contact);
        Assert.Contains(rig.Politics.Events, e => e.Kind == PoliticsEventKind.Gift && e.Civ == 0 && e.Other == 1);
        Assert.Contains(rig.Notes(0, "Barter"), n => n.Subject == 1);
        // The trip takes seasons each way: it has come home with the goods, or is on the road.
        Assert.True(rig.Politics.Events.Any(e => e.Kind == PoliticsEventKind.Barter && e.Civ == 0) ||
                    rig.Politics.Expeditions.Any(e => e.Kind == ExpeditionKind.Barter && e.Civ == 0), "no barter trip of civ 0");
        Assert.True(rig.Politics.Diplomacy.Relations(0, 1) > 0);
        // Every decision carries its reasons (14 §11), and the plan starts as the frontier race (the E0-E3 default).
        Assert.All(rig.Log.Entries.Where(e => e.Layer == Strategist.Layer), e => Assert.False(string.IsNullOrWhiteSpace(e.Reason)));
        Assert.Contains(rig.Notes(0, "Plan"), n => n.Subject == (int)GrandPlan.FrontierRace);
        Assert.Equal(GrandPlan.FrontierRace, rig.Match.Ai!.SeatOf(0)!.Context.Intent.Plan);
        StrategistAudit.Result audit = StrategistAudit.Run(0, rig.Log.Entries, rig.Politics.Events);
        Assert.Equal((0, 0, 0, 0), (audit.SuicideRaids, audit.UndefendedLosses, audit.WinterAggression, audit.Dithering));
    }

    [Fact]
    public void The_match_is_deterministic_and_resumes_exactly()
    {
        var again = new StrategistRig(Seed, World.Value, Controls);
        again.Seasons(BaseSeason);
        Assert.Equal(Shared.Value.Checksum, again.Match.ComputeChecksum());
        Assert.Equal(Shared.Value.Rig.Log.Entries.Select(e => e.Reason), again.Log.Entries.Select(e => e.Reason));
        // A resumed match follows the straight one (the Strategist's plan, memories and cooldowns are saved).
        StrategistRig resumed = Resume();
        Assert.Equal(Shared.Value.Checksum, resumed.Match.ComputeChecksum());
        again.Seasons(8);
        resumed.Seasons(8);
        Assert.Equal(again.Match.ComputeChecksum(), resumed.Match.ComputeChecksum());
    }

    [Fact]
    public void It_fortifies_when_a_raider_neighbour_appears()
    {
        StrategistRig rig = Resume();
        rig.Grant(0, "GOV-02");
        rig.Grant(1, "WAR-01", "HNT-02", "HNT-03", "HNT-04", "WAR-02", "GOV-01", "SOC-05", "GOV-02");
        Assert.True(rig.Politics.OrderPosture(1, Posture.Raid));
        rig.Politics.Diplomacy.Set(0, 1, -50);
        rig.Seasons(2);
        Assert.Equal(Posture.Fortify, rig.Politics.PostureOf(0));
        Assert.Contains(rig.Notes(0, "Threat"), n => n.Subject == 1);
        Assert.Contains(rig.Notes(0, "Posture"), n => n.Subject == (int)Posture.Fortify);
        Assert.True(rig.Match.Ai!.SeatOf(0)!.Context.Intent.DefenseUrgency >= StrategistTuning.FortifyAt);
    }

    [Fact]
    public void It_declares_war_only_with_the_force_ratio()
    {
        // Bows and warriors against spears, Relations hostile: war, an assault, and the ratio in the log.
        StrategistRig strong = Resume();
        strong.Grant(0, Bows);
        // A sickness has halved the neighbour (its army could otherwise take one of our young daughter settlements in reply,
        // which the Strategist refuses to risk).
        foreach (int s in strong.SettlementsOf(1))
        {
            var people = new System.Collections.Generic.List<FigureView>();
            strong.Match.Economy.ListFigures(s, people);
            for (int i = 0; i < people.Count; i += 2) strong.Match.Economy.KillFigure(people[i].Id);
        }
        strong.Politics.Diplomacy.Set(0, 1, -60);
        strong.Seasons(8);
        Assert.True(strong.Notes(0, "War").Count() == 1, string.Join(" | ", strong.Log.Entries.Where(e => e.Civ == 0 && e.Season > BaseSeason && e.Kind.StartsWith("War")).Select(e => $"{e.Season} {e.Kind} {e.Reason}")));
        AiDecision war = strong.Notes(0, "War").Single();
        Assert.True(war.Score >= StrategistTuning.WarRatio, war.Reason);
        Assert.Contains(strong.Politics.Events, e => e.Kind == PoliticsEventKind.WarDeclared && e.Civ == 0 && e.Other == 1 && e.A == 0);
        Assert.Contains(strong.Notes(0, "Assault"), n => n.Other == 1);

        // The same hostility without the arms: no war, and the refusal says why.
        StrategistRig even = Resume();
        even.Politics.Diplomacy.Set(0, 1, -60);
        even.Seasons(4);
        Assert.Empty(even.Notes(0, "War"));
        Assert.False(even.Politics.Diplomacy.AtWar(0, 1));
        Assert.Contains(even.Notes(0, "WarRejected"), n => n.Other == 1);
    }

    [Fact]
    public void It_makes_peace_when_losing()
    {
        StrategistRig rig = Resume();
        rig.Grant(1, Bows);
        rig.Politics.SetWar(1, 0, true);
        rig.Seasons(2);
        Assert.False(rig.Politics.Diplomacy.AtWar(0, 1));
        Assert.True(rig.Politics.Diplomacy.InTruce(0, 1, rig.Politics.Season));
        Assert.Contains(rig.Notes(0, "Peace"), n => n.Subject == 1 && n.Reason.Contains("cannot win"));
        Assert.Contains(rig.Politics.Events, e => e.Kind == PoliticsEventKind.PeaceMade && e.Civ == 0);
    }

    [Fact]
    public void It_refuses_a_raid_on_a_palisade_and_raids_an_undefended_neighbour()
    {
        // Give the neighbor built defenses; DEF-03 only unlocks palisade construction.
        string[] spears = { "WAR-01", "HNT-02", "HNT-03", "HNT-04", "WAR-02" };
        StrategistRig walled = Resume();
        walled.Grant(0, spears);
        walled.Grant(1, "DEF-01", "DEF-02", "WOD-03", "DEF-03");
        foreach (int settlement in walled.SettlementsOf(1)) walled.Match.Economy.FixtureBuildStructure(settlement, "palisade");
        walled.Grant(1, Bows); // and warriors with bows behind it (13 §5.5: "palisade + warriors", 5% success)
        walled.Politics.Diplomacy.Set(0, 1, -40);
        walled.Seasons(8);
        Assert.True(!walled.Notes(0, "Raid").Any(), string.Join(" || ", walled.Log.Entries.Where(e => e.Civ == 0 && e.Season > BaseSeason && e.Kind.StartsWith("Raid")).Select(e => $"{e.Season} {e.Kind} {e.Reason}")) + " ## " + string.Join(" ", walled.Match.Ai!.SeatOf(0)!.Belief.Rivals[1].Settlements.Select(b => $"s{b.Settlement} seen{b.LastSeenSeason} pop{b.PopulationEstimate} f{b.FightersEstimate} d{b.DefendingStrengthEstimate} pal{b.PalisadeSeen} w{b.WatchersSeen}")));
        Assert.Contains(walled.Notes(0, "RaidRejected"), n => n.Other == 1 && n.Reason.Contains("palisade seen"));
        Assert.DoesNotContain(walled.Politics.Events, e => e.Kind == PoliticsEventKind.RaidLaunched && e.Civ == 0);

        // The same neighbour without walls is raided (expected value positive on the belief).
        StrategistRig open = Resume();
        open.Grant(0, spears);
        // Keep the counter-threat realistic: this seed has a young daughter settlement that an intact neighbour can
        // take after a 4–5 figure raid party leaves. The raid fixture needs the same weakened rival used by the war test
        // above, otherwise refusing the raid is the intended departure-safety behavior, not a raid-selection regression.
        foreach (int s in open.SettlementsOf(1))
        {
            var people = new System.Collections.Generic.List<FigureView>();
            open.Match.Economy.ListFigures(s, people);
            for (int i = 0; i < people.Count; i += 2) open.Match.Economy.KillFigure(people[i].Id);
        }
        open.Politics.Diplomacy.Set(0, 1, -40);
        open.Seasons(8);
        Assert.True(open.Notes(0, "Raid").Any(), string.Join(" | ", open.Log.Entries.Where(e => e.Civ == 0 && e.Season > 104).Select(e => $"{e.Season} {e.Kind} {e.Reason}")));
        AiDecision raid = open.Notes(0, "Raid").First();
        Assert.True(raid.Score >= Fix64.FromRatio(45, 100), raid.Reason);
        Assert.Contains(open.Politics.Events, e => e.Kind == PoliticsEventKind.RaidLaunched && e.Civ == 0 && e.Other == 1);
    }

    [Fact]
    public void It_places_a_Beacon_on_open_land_toward_the_frontier()
    {
        StrategistRig rig = Resume();
        rig.Grant(0, "ART-05");
        rig.Seasons(2);
        int beacon = Assert.Single(rig.Politics.BeaconsOf(0));
        AiDecision note = Assert.Single(rig.Notes(0, "Beacon"));
        Assert.Equal(beacon, note.Subject);
        Assert.NotEqual(1, rig.Politics.Territory.OwnerOf(beacon));
        int rival = rig.SettlementsOf(1).Select(s => rig.Politics.SettlementTileOf(s)).OrderBy(t => HexGrid.Distance(t, beacon, 160)).First();
        int home = rig.SettlementsOf(0).Select(s => rig.Politics.SettlementTileOf(s)).OrderBy(t => HexGrid.Distance(t, rival, 160)).First();
        Assert.True(HexGrid.Distance(beacon, rival, 160) >= StrategistTuning.BeaconRivalKeepOff);
        Assert.True(HexGrid.Distance(beacon, rival, 160) <= HexGrid.Distance(home, rival, 160) + 2, note.Reason);
    }

    [Fact]
    public void Against_a_Rusher_it_loses_no_settlement_and_commits_no_blunder()
    {
        var rig = new StrategistRig(Seed, World.Value, Controls);
        rig.Rushers.Add(new Rusher(1, 0));
        rig.Rushers.Add(new Rusher(2, 3));
        rig.Seasons(132);
        foreach (int civ in new[] { 0, 3 })
        {
            StrategistAudit.Result audit = StrategistAudit.Run(civ, rig.Log.Entries, rig.Politics.Events);
            Assert.Equal(0, audit.SettlementsLost);
            Assert.Equal((0, 0, 0, 0), (audit.SuicideRaids, audit.UndefendedLosses, audit.WinterAggression, audit.Dithering));
            Assert.True(audit.ThreatsNoticed > 0, $"civ {civ} never noticed the Rusher");
        }
    }

    [Fact]
    public void Without_beliefs_it_stays_safe_and_blind_to_targets()
    {
        // The placeholder belief builder knows only the diplomacy: no settlement is ever a target.
        var rig = new StrategistRig(Seed, World.Value, Controls) { Omniscient = false };
        rig.Seasons(BaseSeason);
        Assert.Empty(rig.Notes(0, "Raid"));
        Assert.Empty(rig.Notes(0, "War"));
        Assert.Empty(rig.Notes(0, "Beacon"));
        Assert.Contains(rig.Notes(0, "Gift"), n => n.Subject == 1);
    }
}
