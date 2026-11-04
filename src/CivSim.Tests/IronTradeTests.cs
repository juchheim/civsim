using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>
/// Iron bloom as a Compact good in the Iron chapter (M11 WP6 slice 5): the good exists only in that chapter and cannot be embargoed, a
/// compact carries bloom from a seller's reserve to a buyer's forge, and the Council and the Strategist propose it to a civilization that has no iron.
/// World 39 gives civilization 1 iron of its own, a camp site on tile 5489 that civilization 0 can see, and civilization 0 none within reach.
/// </summary>
public class IronTradeTests
{
    private const ulong Seed = 39;
    private const int CampTile = 5489;
    /// <summary>MET-09 numeric, so research itself knows iron tools (the Council and Strategist read research, not the economy).</summary>
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric("MET-09"));
    private static readonly Lazy<ResearchCatalog> Catalog = new(() => new ResearchCatalog(Pack.Value));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private sealed class Rig
    {
        public readonly MatchModel Match;
        public readonly SimRunner Runner;
        public PoliticsSystem Politics => Match.Politics;

        public Rig(ChapterRules rules, bool councilPolicy = false)
        {
            Match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules);
            Runner = new SimRunner(Match);
            if (!councilPolicy) for (int c = 0; c < 4; c++) Politics.SetDecreePolicy(c, null);
            foreach (int civ in new[] { 0, 1 }) Grant(civ, "KEY-3", "MET-08", "MET-09");
            Politics.ForceContact(0, 1);
        }

        public void Grant(int civ, params string[] projects)
        {
            foreach (string p in projects)
                Runner.Submit(new Command(Runner.Tick, (byte)civ, CommandKind.CompleteProject, Catalog.Value.IndexOf(p), 0, 0));
            Runner.Advance(1);
        }

        public int HomeOf(int civ)
        {
            for (int s = 0; s < Politics.SettlementTotal; s++)
                if (Politics.SettlementCivOf(s) == civ && !Politics.IsFreeFolk(s)) return s;
            throw new InvalidOperationException("no settlement");
        }

        public void Seasons(int n) { for (int i = 0; i < n; i++) Runner.Advance(150); }
    }

    private static Belief NewBelief()
        => new(0, 4, Catalog.Value.Count, new MapKnowledge(World.Value.Tiles.ToArray(), World.Value.Width, World.Value.Height));

    [Fact]
    public void Iron_is_a_compact_good_only_in_the_Iron_chapter()
    {
        foreach ((ChapterRules rules, bool signed) in new[] { (ChapterRules.Bronze, false), (ChapterRules.Iron, true) })
        {
            var rig = new Rig(rules);
            rig.Match.Economy.PutGood(rig.HomeOf(1), "G-IRON", Fix64.FromInt(6));
            Assert.True(rig.Politics.IssueDecree(0, DecreeKind.Compact, 1, DecreeArgs.PackCompact(TradeGood.Iron, sell: false)));
            rig.Seasons(1);
            Assert.Equal(signed, rig.Politics.Compacts.Any(c => c.Good == TradeGood.Iron && c.Active));
        }
        Assert.Equal("G-IRON", DecreeArgs.GoodId(TradeGood.Iron));
    }

    [Fact]
    public void Iron_cannot_be_embargoed_and_no_embargo_bit_is_added()
    {
        var rig = new Rig(ChapterRules.Iron);
        Assert.False(rig.Politics.OrderEmbargo(1, 0, TradeGood.Iron, true));
        Assert.False(rig.Politics.Embargoes(1, 0, TradeGood.Iron));
        Assert.Equal(0, rig.Politics.EmbargoMask(1, 0));
        Assert.True(rig.Politics.OrderEmbargo(1, 0, TradeGood.Tin, true)); // the Bronze goods still can
        Assert.Equal(1 << (int)TradeGood.Tin, rig.Politics.EmbargoMask(1, 0));
    }

    [Fact]
    public void A_compact_carries_bloom_from_the_sellers_stock_to_the_buyers_forge()
    {
        var rig = new Rig(ChapterRules.Iron);
        int seller = rig.HomeOf(1), buyer = rig.HomeOf(0);
        rig.Match.Economy.PutGood(seller, "G-IRON", Fix64.FromInt(6));
        Fix64 sold = rig.Match.Economy.GoodStock(seller, "G-IRON");
        Assert.True(rig.Politics.IssueDecree(0, DecreeKind.Compact, 1, DecreeArgs.PackCompact(TradeGood.Iron, sell: false)));
        rig.Seasons(14);

        Compact compact = Assert.Single(rig.Politics.Compacts, c => c.Good == TradeGood.Iron);
        Assert.True(compact.Active);
        Assert.True(compact.Deliveries >= 1, "at least one caravan should have arrived");
        Assert.True(rig.Match.Economy.GoodStock(seller, "G-IRON") < sold, "the seller's bloom left in caravans");
        // What reached the buyer is bloom, wrought iron or kits it made from them.
        int held = 0;
        foreach (int t in rig.Match.Economy.FixtureAdultKitTiers(buyer)) if (t >= 11) held++;
        Fix64 arrived = rig.Match.Economy.GoodStock(buyer, "G-IRON") + rig.Match.Economy.GoodStock(buyer, "G-WROUGHT") +
            rig.Match.Economy.GoodStock(buyer, "G-TOOL11") + Fix64.FromInt(held);
        Assert.True(arrived > Fix64.Zero, "bloom, wrought iron or kits should be in the buyer's hands");
    }

    [Fact]
    public void The_Council_buys_iron_by_compact_from_a_civilization_it_has_seen_digging_it()
    {
        var rig = new Rig(ChapterRules.Iron, councilPolicy: true);
        Assert.Equal(MiningCampResult.Opened, rig.Match.Economy.OpenMiningCamp(1, CampTile));
        Assert.Equal(1, rig.Politics.IronSellerFor(0));
        Assert.False(rig.Politics.BuysIron(0));

        // Civilization 0 has no iron and no mine site it could open, so the Council buys.
        Assert.True(CouncilDecreePolicy.IronCouncilNeedsIron(rig.Politics.DecreeResearch, rig.Match.Economy, 0));
        Assert.Equal(-1, rig.Politics.NearestMineSite(0, DepositKind.Iron));
        var requests = new List<DecreeRequest>();
        new CouncilDecreePolicy().Propose(rig.Politics, 0, requests);
        Assert.Contains(requests, r => r.Kind == DecreeKind.Compact && r.Target == 1 && r.Extra == DecreeArgs.PackCompact(TradeGood.Iron, sell: false));
    }

    [Fact]
    public void The_Bronze_Council_never_asks_for_iron()
    {
        var rig = new Rig(ChapterRules.Bronze, councilPolicy: true);
        Assert.False(CouncilDecreePolicy.IronCouncilNeedsIron(rig.Politics.DecreeResearch, rig.Match.Economy, 0));
        var requests = new List<DecreeRequest>();
        new CouncilDecreePolicy().Propose(rig.Politics, 0, requests);
        Assert.DoesNotContain(requests, r => r.Kind == DecreeKind.Compact && DecreeArgs.Good(r.Extra) == TradeGood.Iron);
    }

    [Fact]
    public void The_Strategist_proposes_iron_compacts_only_to_rivals_believed_to_be_digging_iron()
    {
        var rig = new Rig(ChapterRules.Iron);
        var belief = NewBelief();
        belief.Season = 12; // first season of the year
        var candidates = new List<DecreeRequest>();
        int buyIron = DecreeArgs.PackCompact(TradeGood.Iron, sell: false);

        StrategistDecreePolicy.AddBeliefKnownIronCompactAlternatives(rig.Politics, 0, belief, candidates);
        Assert.Empty(candidates); // nobody contacted or seen digging

        belief.Rivals[1].Contacted = true;
        StrategistDecreePolicy.AddBeliefKnownIronCompactAlternatives(rig.Politics, 0, belief, candidates);
        Assert.Empty(candidates); // contacted but no iron camp seen

        belief.Rivals[1].SeenFlags |= SeenFlag.IronMineCamp;
        StrategistDecreePolicy.AddBeliefKnownIronCompactAlternatives(rig.Politics, 0, belief, candidates);
        Assert.Contains(candidates, r => r.Kind == DecreeKind.Compact && r.Target == 1 && r.Extra == buyIron);
        Assert.True(StrategistDecreePolicy.BeliefAllowsCandidate(belief, 0, new DecreeRequest(DecreeKind.Compact, 1, buyIron)));
        Assert.False(StrategistDecreePolicy.BeliefAllowsCandidate(belief, 0, new DecreeRequest(DecreeKind.Compact, 2, buyIron)));

        belief.Season = 13; // off the yearly compact season
        candidates.Clear();
        StrategistDecreePolicy.AddBeliefKnownIronCompactAlternatives(rig.Politics, 0, belief, candidates);
        Assert.Empty(candidates);
    }

    [Fact]
    public void A_Bronze_Strategist_has_no_iron_compact_candidates()
    {
        var rig = new Rig(ChapterRules.Bronze);
        var belief = NewBelief();
        belief.Season = 12;
        belief.Rivals[1].Contacted = true;
        belief.Rivals[1].SeenFlags |= SeenFlag.IronMineCamp;
        var candidates = new List<DecreeRequest>();
        StrategistDecreePolicy.AddBeliefKnownIronCompactAlternatives(rig.Politics, 0, belief, candidates);
        Assert.Empty(candidates);
    }
}
