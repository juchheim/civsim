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
using Xunit;

namespace CivSim.Tests;

public class StrategistSecureTinTests
{
    private const int Season = 80;
    private const ulong MatchSeed = 20261023;
    private static readonly Lazy<GeneratedWorld> MatchWorld = new(() => WorldGenerator.Generate(MatchSeed));

    [Fact]
    public void Council_mine_and_tin_compact_candidates_require_belief_route_evidence()
    {
        var belief = new Belief(0, 4, TestPack.Catalog.Count,
            new MapKnowledge(MatchWorld.Value.Tiles.ToArray(), MatchWorld.Value.Width, MatchWorld.Value.Height)) { Season = 20 };
        int mine = Enumerable.Range(0, MatchWorld.Value.Tiles.Length).First(t => MatchWorld.Value.Tiles[t].Deposit == DepositKind.Tin);
        Assert.True(mine >= 0);
        var mineCandidate = new DecreeRequest(DecreeKind.Mine, mine, 0);
        Assert.False(StrategistDecreePolicy.BeliefAllowsCandidate(belief, 0, mineCandidate));
        belief.Map.MarkExplored(mine);
        belief.Map.RecordOwner(mine, -1, belief.Season);
        Assert.True(StrategistDecreePolicy.BeliefAllowsCandidate(belief, 0, mineCandidate));
        belief.Map.RecordOwner(mine, 1, belief.Season);
        Assert.False(StrategistDecreePolicy.BeliefAllowsCandidate(belief, 0, mineCandidate));
        belief.Map.RecordOwner(mine, -1, belief.Season - 3);
        Assert.False(StrategistDecreePolicy.BeliefAllowsCandidate(belief, 0, mineCandidate));

        var tinCompact = new DecreeRequest(DecreeKind.Compact, 1, DecreeArgs.PackCompact(TradeGood.Tin, sell: false));
        Assert.False(StrategistDecreePolicy.BeliefAllowsCandidate(belief, 0, tinCompact));
        belief.Rivals[1].Contacted = true;
        Assert.False(StrategistDecreePolicy.BeliefAllowsCandidate(belief, 0, tinCompact));
        belief.Rivals[1].TinAccessSeen = true;
        Assert.True(StrategistDecreePolicy.BeliefAllowsCandidate(belief, 0, tinCompact));
    }

    private static void PlaceSettlementInSight(PoliticsSystem politics, int settlement, int tile)
    {
        var field = typeof(PoliticsSystem).GetField("_sTile", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        ((int[])field.GetValue(politics)!)[settlement] = tile;
    }

    private sealed class Recorder : IStrategistActions
    {
        public readonly List<(DecreeKind Kind, int Target, int Extra)> Decrees = new();
        public int Mandate => 3;
        public int MandateCap => 3;
        public int DecreeCost(DecreeKind kind) => kind is DecreeKind.Mine or DecreeKind.Compact or DecreeKind.Levy ? 1 : int.MaxValue;
        public bool IssueDecree(DecreeKind kind, int target, int extra) { Decrees.Add((kind, target, extra)); return true; }
        public bool Gift(int toCiv) => false;
        public bool Barter(int withCiv) => false;
        public bool Raid(int targetSettlement, int partySize) => false;
        public bool Assault(int targetSettlement) => false;
        public bool DeclareWar(int civ) => false;
        public bool MakePeace(int civ) => false;
        public bool Ally(int civ) => false;
        public bool SetPosture(Posture posture) => false;
        public bool PlaceBeacon(int tile) => false;
        public void RemoveBeacon(int index) { }
    }

    private static (StrategistView View, int TinTile) BuildView(bool tinAccess = false, int era = 3)
    {
        GeneratedWorld world = WorldGenerator.Generate(20261023);
        const int civ = 0;
        const int ownTile = 40 * 160 + 80;
        int tinTile = Enumerable.Range(0, world.Tiles.Length)
            .Where(t => world.Tiles[t].Deposit == DepositKind.Tin)
            .OrderBy(t => HexGrid.Distance(ownTile, t, world.Width)).First();
        var map = new MapKnowledge(world.Tiles.ToArray(), world.Width, world.Height);
        map.MarkExplored(tinTile);
        map.RecordOwner(tinTile, -1, Season);
        var belief = new Belief(civ, 4, TestPack.Catalog.Count, map);
        var own = new OwnView
        {
            Civ = civ, Era = era, Season = Season, Tick = Season * 150L, Population = 80, Adults = 40,
            ReachTiles = 12, TinAccess = tinAccess,
            Posture = Posture.Balanced,
        };
        own.Settlements.Add(new OwnSettlement
        {
            Index = 0, Tile = ownTile, Population = 80, Adults = 40, Fighters = 20,
            DefendingStrength = Fix64.FromInt(20), AttackingStrength = Fix64.FromInt(20),
            FoodStores = Fix64.FromInt(100), FoodSecurity = Fix64.One, LateWinterForecast = Fix64.One,
            Cohesion = Fix64.One, Defenses = Defenses.None,
        });
        own.Others = Enumerable.Range(0, 4).Select(c => new PairView
        {
            Civ = c, LastGiftSeason = -1000, LastBarterSeason = -1000,
            LastRaidOnThemSeason = -1000, LastRaidOnUsSeason = -1000,
        }).ToArray();
        var done = new HashSet<string>(StringComparer.Ordinal) { "MET-03", "MET-05" };
        var view = new StrategistView
        {
            Civ = civ, Season = Season, Tick = Season * 150L, MatchSeed = 7,
            Spec = new AiSpec(Difficulty.Warlord, EconomyArchetype.Balanced),
            Profile = DifficultyProfile.From(TestPack.Pack, Difficulty.Warlord), Own = own, Belief = belief,
            Intent = new StrategicIntent(), Catalog = TestPack.Catalog,
            OwnDone = p => done.Contains(TestPack.Catalog.Projects[p].Id), Log = new AiLog(),
        };
        return (view, tinTile);
    }

    [Fact]
    public void Missing_tin_access_selects_secure_tin_without_directly_issuing_a_decree()
    {
        (StrategistView view, int tinTile) = BuildView();
        var strategist = new Strategist(view.Spec, view.Profile);
        var actions = new Recorder();

        strategist.Decide(view, actions);

        Assert.Equal(GrandPlan.SecureTin, view.Intent.Plan);
        Assert.Empty(actions.Decrees); // The one per-season decree arbiter owns Decree issuance.
        Assert.Contains(view.Log!.Entries, entry => entry.Kind == "Plan" && entry.Subject == (int)GrandPlan.SecureTin);
        Assert.True(view.Belief.Map.TryTile(tinTile, out WorldTile known) && known.Deposit == DepositKind.Tin);
    }

    [Fact]
    public void Tin_access_suppresses_the_secure_tin_plan()
    {
        (StrategistView view, _) = BuildView(tinAccess: true);
        var strategist = new Strategist(view.Spec, view.Profile);
        strategist.Decide(view, new Recorder());
        Assert.NotEqual(GrandPlan.SecureTin, view.Intent.Plan);
    }

    [Fact]
    public void Secure_tin_is_not_considered_before_E3()
    {
        (StrategistView view, _) = BuildView(era: 2);
        var strategist = new Strategist(view.Spec, view.Profile);
        strategist.Decide(view, new Recorder());
        Assert.NotEqual(GrandPlan.SecureTin, view.Intent.Plan);
        Assert.DoesNotContain(view.Log!.Entries, entry => entry.Kind == "Plan" && entry.Subject == (int)GrandPlan.SecureTin);
    }

    [Fact]
    public void E3_secure_tin_season_has_at_most_one_decree()
    {
        CivControl ai = CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced);
        var rig = new StrategistRig(MatchSeed, MatchWorld.Value,
            new[] { ai, CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3");
        rig.Grant(0, "MET-03");
        rig.Grant(0, "MET-05");
        Assert.Equal(3, rig.Match.Research.Era(0));

        rig.Seasons(1);

        Assert.Equal(GrandPlan.SecureTin, rig.Match.Ai!.SeatOf(0)!.Context.Intent.Plan);
        int season = rig.Season - 1;
        int resolved = rig.Politics.Events.Count(e => e.Civ == 0 && (e.Kind is PoliticsEventKind.DecreeResolved or PoliticsEventKind.DecreeRefused) &&
            (int)((e.Tick + 1) / 150) == season);
        Assert.InRange(resolved, 0, 1);
        Assert.InRange(rig.Notes(0, "Decree").Count(n => n.Season == season), 0, 1);
    }

    [Fact]
    public void Secure_tin_levy_requires_a_sighted_belief_record_and_has_positive_EV()
    {
        CivControl ai = CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced);
        var rig = new StrategistRig(MatchSeed, MatchWorld.Value,
            new[] { ai, CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Politics.SetDecreePolicy(0, null);
        rig.Grant(0, "KEY-3", "MET-03", "MET-05");
        rig.Seasons(12);

        AiSeat seat = rig.Match.Ai!.SeatOf(0)!;
        Belief belief = seat.Belief;
        int ownTile = rig.Politics.SettlementTileOf(rig.SettlementsOf(0)[0]);
        // Give the test host enough adults to exceed the forecaster's conservative fixed Tin Folk defense prior.
        // This keeps the test about belief-gated legal targeting and positive EV, not a zero-success levy.
        rig.Match.Economy.FixtureAddPeople(rig.SettlementsOf(0)[0], 60, 25);
        TinFolkVillage visibleFolk = rig.Politics.TinFolk[0];
        PlaceSettlementInSight(rig.Politics, visibleFolk.Settlement, ownTile);
        new BeliefBuilder().Update(belief, rig.Match.Ai.World, seat.Profile);
        var policy = new StrategistDecreePolicy(rig.Log, belief, seat.Context.Intent);
        seat.Context.Intent.Plan = GrandPlan.SecureTin;
        var requests = new List<DecreeRequest>();
        policy.Propose(rig.Politics, 0, requests);

        BeliefTinFolk known = Assert.Single(belief.TinFolkSeen, f => f.Settlement == visibleFolk.Settlement);
        Assert.True(rig.Politics.CanLevyKnownTinFolk(0, known.Settlement, known.LastSeenSeason));
        Assert.NotNull(known);
        AiDecision candidate = Assert.Single(rig.Log.Entries, e => e.Kind == "DecreeCandidate" &&
            e.Subject == (int)DecreeKind.Levy && e.Other == known.Settlement);
        Assert.True(candidate.Score > Fix64.Zero, candidate.Reason);
        Assert.InRange(requests.Count, 0, 1); // The common EV arbiter emits at most one decree this season.

        int unknown = rig.Politics.TinFolk.Select(f => f.Settlement)
            .FirstOrDefault(s => belief.TinFolkSeen.All(f => f.Settlement != s), -1);
        if (unknown >= 0)
        {
            Assert.False(rig.Politics.CanLevyKnownTinFolk(0, unknown, rig.Season));
            Assert.DoesNotContain(rig.Log.Entries, e => e.Kind == "DecreeCandidate" && e.Subject == (int)DecreeKind.Levy && e.Other == unknown);
        }
    }

    [Fact]
    public void Secure_tin_keeps_compact_recovery_available_when_an_ore_source_is_not_producing_access()
    {
        CivControl ai = CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced);
        var rig = new StrategistRig(MatchSeed, MatchWorld.Value,
            new[] { ai, CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3", "MET-03", "MET-05");
        int own = rig.SettlementsOf(0)[0];
        int tinTile = Enumerable.Range(0, MatchWorld.Value.Tiles.Length)
            .First(tile => MatchWorld.Value.Tiles[tile].Deposit == DepositKind.Tin);
        rig.Match.Economy.FixtureAddMiningCamp(own, tinTile);
        Assert.True(rig.Match.Economy.CivHasOreSource(0, DepositKind.Tin));
        Assert.False(rig.Match.Economy.CivHasMetalAccess(0, MetalAccessKind.Tin));

        var belief = new Belief(0, 4, TestPack.Catalog.Count,
            new MapKnowledge(MatchWorld.Value.Tiles.ToArray(), MatchWorld.Value.Width, MatchWorld.Value.Height))
        { Season = 4 };
        belief.Rivals[1].Contacted = true;
        belief.Rivals[1].TinAccessSeen = true;
        var candidates = new List<DecreeRequest>();

        int alternativeMine = rig.Politics.NearestMineSite(0, DepositKind.Tin);
        if (alternativeMine >= 0)
        {
            belief.Map.MarkExplored(alternativeMine);
            belief.Map.RecordOwner(alternativeMine, -1, belief.Season);
            StrategistDecreePolicy.AddBeliefKnownMineAlternatives(rig.Politics, 0, belief, candidates);
            Assert.Contains(candidates, c => c.Kind == DecreeKind.Mine && c.Target == alternativeMine);
        }

        candidates.Clear();
        StrategistDecreePolicy.AddBeliefKnownTinCompactAlternatives(rig.Politics, 0, belief, candidates);

        Assert.Contains(candidates, c => c.Kind == DecreeKind.Compact && c.Target == 1 &&
            DecreeArgs.Good(c.Extra) == TradeGood.Tin && !DecreeArgs.Sell(c.Extra));
    }

    [Fact]
    public void Levy_forecast_rejects_allied_targets()
    {
        CivControl ai = CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced);
        var rig = new StrategistRig(MatchSeed, MatchWorld.Value,
            new[] { ai, CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3", "DIP-03");
        rig.Seasons(1);
        int target = Enumerable.Range(0, rig.Politics.SettlementTotal).First(s => rig.Politics.SettlementCivOf(s) == 1);
        int ownTile = rig.Politics.SettlementTileOf(rig.SettlementsOf(0)[0]);
        PlaceSettlementInSight(rig.Politics, target, ownTile);
        var belief = new Belief(0, 4, TestPack.Catalog.Count,
            new MapKnowledge(MatchWorld.Value.Tiles.ToArray(), MatchWorld.Value.Width, MatchWorld.Value.Height));
        belief.Season = rig.Season;
        belief.Rivals[1].Settlements.Add(new BeliefSettlement
        {
            Settlement = target, Tile = rig.Politics.SettlementTileOf(target), LastSeenSeason = rig.Season,
            PopulationEstimate = 24, FightersEstimate = 8, DefendingStrengthEstimate = Fix64.FromInt(16),
        });

        DecreeForecast before = DecreeForecaster.Forecast(rig.Politics, 0, DecreeKind.Levy, target, 0, belief);
        rig.Politics.Diplomacy.Ally(0, 1);
        DecreeForecast allied = DecreeForecaster.Forecast(rig.Politics, 0, DecreeKind.Levy, target, 0, belief);

        Assert.True(before.Allowed, before.LikelyRefusal.ToString());
        Assert.False(allied.Allowed);
        Assert.Equal(OrderRefusalReason.Rejected, allied.LikelyRefusal);
    }
}
