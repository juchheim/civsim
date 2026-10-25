using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.World;
using Xunit;

namespace CivSim.Tests;

public sealed class StrategistDecreeCandidateTests
{
    private const ulong Seed = 20261023;
    private static readonly System.Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));

    [Theory]
    [InlineData(true, true, 0, true)]
    [InlineData(true, true, 1, false)]
    [InlineData(true, false, 0, false)]
    [InlineData(false, true, 0, false)]
    public void At_cap_useful_paid_decree_precedes_free_compact(bool atCap, bool positivePaidAlternative, int cost, bool expectedSkip)
    {
        Assert.Equal(expectedSkip, StrategistDecreePolicy.ShouldPreferMandateSpender(atCap, positivePaidAlternative, cost));
    }

    [Fact]
    public void Known_recent_mines_are_added_even_when_the_council_candidate_is_absent()
    {
        var rig = new StrategistRig(Seed, World.Value,
            new[] { CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3", "MET-05");
        rig.Seasons(12);
        Belief belief = rig.Match.Ai!.SeatOf(0)!.Belief;
        belief.Season = rig.Season;
        int tile = Enumerable.Range(0, World.Value.Tiles.Length).First(t => World.Value.Tiles[t].Deposit == DepositKind.Tin);
        belief.Map.MarkExplored(tile);
        belief.Map.RecordOwner(tile, -1, belief.Season);

        var candidates = new List<DecreeRequest>();
        StrategistDecreePolicy.AddBeliefKnownMineAlternatives(rig.Politics, 0, belief, candidates);

        Assert.Contains(candidates, r => r.Kind == DecreeKind.Mine && r.Target == tile);
        Assert.DoesNotContain(candidates, r => r.Kind == DecreeKind.Mine &&
            World.Value.Tiles[r.Target].Deposit is not (DepositKind.Tin or DepositKind.CopperOre or DepositKind.ArsenicalCopper));
        Assert.Equal(candidates.Count, candidates.Select(r => (r.Kind, r.Target, r.Extra)).Distinct().Count());
    }

    [Fact]
    public void Found_alternatives_are_not_added_after_the_council_age_limit()
    {
        var rig = new StrategistRig(Seed, World.Value,
            new[] { CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3");
        rig.Seasons(8);
        Belief belief = rig.Match.Ai!.SeatOf(0)!.Belief;
        belief.Season = rig.Season;
        int[] done = (int[])typeof(PoliticsSystem).GetField("_decreesDone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(rig.Politics)!;
        done[(int)DecreeKind.Found] = CouncilDecreePolicy.MaxFoundsPerAge;
        var candidates = new List<DecreeRequest>();

        StrategistDecreePolicy.AddBeliefKnownFoundAlternatives(rig.Politics, 0, belief, candidates);

        Assert.Empty(candidates);
    }

    [Fact]
    public void Secure_tin_levy_candidates_use_recent_belief_sightings_without_live_levy_selection()
    {
        var rig = new StrategistRig(Seed, World.Value,
            new[] { CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3");
        rig.Seasons(8);
        Belief belief = rig.Match.Ai!.SeatOf(0)!.Belief;
        belief.Season = rig.Season;
        int folk = 0; // Candidate generation is belief-only; legality is decided later by the forecaster.
        belief.TinFolkSeen.Clear();
        belief.TinFolkSeen.Add(new BeliefTinFolk { Settlement = folk, Tile = 0, LastSeenSeason = belief.Season });

        var candidates = new List<DecreeRequest>();
        StrategistDecreePolicy.AddBeliefKnownLevyAlternatives(rig.Politics, 0, belief, candidates);

        Assert.Contains(candidates, r => r.Kind == DecreeKind.Levy && r.Target == folk);
        belief.TinFolkSeen[0].LastSeenSeason = belief.Season - 3;
        candidates.Clear();
        StrategistDecreePolicy.AddBeliefKnownLevyAlternatives(rig.Politics, 0, belief, candidates);
        Assert.DoesNotContain(candidates, r => r.Kind == DecreeKind.Levy && r.Target == folk);
    }

    [Fact]
    public void Tin_folk_compact_candidates_keep_the_longer_known_route_evidence_window()
    {
        var rig = new StrategistRig(Seed, World.Value,
            new[] { CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3", "MET-05");
        Belief belief = rig.Match.Ai!.SeatOf(0)!.Belief;
        belief.Season = 12; // First season of the year, so compact proposals are eligible.
        belief.TinFolkSeen.Clear();
        belief.TinFolkSeen.Add(new BeliefTinFolk { Settlement = 0, Tile = 0, LastSeenSeason = 9 });
        var candidates = new List<DecreeRequest>();

        StrategistDecreePolicy.AddBeliefKnownTinCompactAlternatives(rig.Politics, 0, belief, candidates);

        int folkParty = DecreeArgs.FolkParty(0);
        int tinBuy = DecreeArgs.PackCompact(TradeGood.Tin, sell: false);
        Assert.Contains(candidates, r => r.Kind == DecreeKind.Compact && r.Target == folkParty && r.Extra == tinBuy);
        Assert.True(StrategistDecreePolicy.BeliefAllowsCandidate(belief, 0,
            new DecreeRequest(DecreeKind.Compact, folkParty, tinBuy)));

        belief.Season = 16;
        belief.TinFolkSeen[0].LastSeenSeason = 3;
        candidates.Clear();
        StrategistDecreePolicy.AddBeliefKnownTinCompactAlternatives(rig.Politics, 0, belief, candidates);
        Assert.DoesNotContain(candidates, r => r.Kind == DecreeKind.Compact && r.Target == folkParty && r.Extra == tinBuy);
    }

    [Fact]
    public void Found_candidates_are_filtered_by_recent_believed_rival_settlements()
    {
        var belief = new Belief(0, 4, TestPack.Catalog.Count,
            new MapKnowledge(World.Value.Tiles.ToArray(), World.Value.Width, World.Value.Height)) { Season = 20 };
        const int tile = 1000;
        belief.Map.MarkExplored(tile);
        belief.Rivals[1].Settlements.Add(new BeliefSettlement
        {
            Settlement = 1, Tile = tile, LastSeenSeason = 20, PopulationEstimate = 20,
        });
        var candidate = new DecreeRequest(DecreeKind.Found, tile, 0);

        Assert.False(StrategistDecreePolicy.BeliefAllowsCandidate(belief, 0, candidate));
        belief.Rivals[1].Settlements[0].LastSeenSeason = 17;
        Assert.True(StrategistDecreePolicy.BeliefAllowsCandidate(belief, 0, candidate));
    }
}
