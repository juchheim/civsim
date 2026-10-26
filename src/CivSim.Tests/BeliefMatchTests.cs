using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;
using Xunit.Abstractions;

namespace CivSim.Tests;

/// <summary>
/// The belief builder in a real match (docs/MVP/14 §3.1, §5): one match run once to mid-E2 with a Warlord (full model) at
/// civilization 0, a Chieftain (archetype posterior) at 2 and a Settler (none) at 3. Tests only read it or copy it.
/// </summary>
public class BeliefMatchTests
{
    private const int Seasons = 124;
    private static readonly CivControl[] Controls =
    {
        CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Warlord), CivControl.Council,
        CivControl.Ai(Difficulty.Chieftain, EconomyArchetype.Grower), CivControl.Ai(Difficulty.Settler, EconomyArchetype.Sage),
    };

    private static readonly Lazy<MatchModel> Fixture = new(() =>
    {
        MatchModel m = FogFairnessHarness.NewMatch(Controls);
        new SimRunner(m).Advance(ResearchSystem.SeasonTicks * Seasons);
        return m;
    });

    private readonly ITestOutputHelper _out;
    public BeliefMatchTests(ITestOutputHelper output) => _out = output;

    private static Belief BeliefOf(MatchModel m, int civ) => m.Ai!.SeatOf(civ)!.Belief;

    [Fact]
    public void What_is_in_sight_is_seen_this_season_and_nothing_of_an_unknown_civilization_is()
    {
        MatchModel m = Fixture.Value;
        TrueWorld world = m.Ai!.World;
        Belief b = BeliefOf(m, 0);
        int season = m.Politics.Season, seen = 0;
        Assert.Equal(season, b.Season);
        for (int s = 0; s < m.Politics.SettlementTotal; s++)
        {
            int owner = m.Politics.SettlementCivOf(s);
            if (owner == 0 || m.Politics.IsFreeFolk(s) || m.Politics.SettlementPopulationOf(s) <= 0) continue;
            BeliefSettlement? record = b.Rivals[owner].Settlements.Find(x => x.Settlement == s);
            if (!b.Rivals[owner].Aware) { Assert.Null(record); continue; }
            if (BeliefBuilder.TilesOutOfSight(world, 0, s) <= 0)
            {
                Assert.NotNull(record);
                Assert.Equal(season, record!.LastSeenSeason);
                Assert.Equal(m.Politics.SettlementTileOf(s), record.Tile);
                seen++;
            }
        }
        Assert.True(seen >= 1, "a rival settlement should be in sight by mid-E2");
        // Stale records are of settlements out of sight now; they keep the season they were last true.
        foreach (RivalBelief r in b.Rivals)
            foreach (BeliefSettlement s in r.Settlements)
            {
                Assert.InRange(s.FirstSeenSeason, 0, s.LastSeenSeason);
                if (s.LastSeenSeason < season) Assert.True(BeliefBuilder.TilesOutOfSight(world, 0, s.Settlement) > 0);
            }
        foreach (RivalBelief r in b.Rivals)
        {
            if (r.Civ == 0) continue;
            if (!r.Aware) Assert.Empty(r.Settlements);
            Assert.Equal(m.Politics.Diplomacy.Relations(0, r.Civ), r.Relations);
            Assert.Equal(m.Research.Era(r.Civ), r.Era);
            Assert.Equal(BeliefRules.Confidence(r.LastSeenSeason < 0 ? -1 : season - r.LastSeenSeason), r.Confidence);
        }
        // The map: every tile within routine sight of an own settlement is explored with this season's owner.
        int routine = BeliefRules.RoutineSight(m.Politics.ReachOf(0));
        for (int s = 0; s < m.Politics.SettlementTotal; s++)
        {
            if (m.Politics.SettlementCivOf(s) != 0 || m.Politics.IsFreeFolk(s)) continue;
            for (int t = 0; t < b.Map.TileCount; t++)
            {
                if (HexGrid.Distance(m.Politics.SettlementTileOf(s), t, b.Map.Width) > routine) continue;
                Assert.True(b.Map.IsExplored(t));
                Assert.Equal((m.Politics.Territory.OwnerOf(t), season), b.Map.OwnerSeen(t));
            }
        }
    }

    [Fact]
    public void Head_counts_seen_this_season_are_within_a_quarter_of_the_truth()
    {
        MatchModel m = Fixture.Value;
        int checkedCount = 0;
        foreach (int civ in new[] { 0, 2 })
            foreach (RivalBelief r in BeliefOf(m, civ).Rivals)
                foreach (BeliefSettlement s in r.Settlements)
                {
                    if (s.LastSeenSeason != m.Politics.Season) continue;
                    int truth = m.Politics.SettlementPopulationOf(s.Settlement);
                    Assert.InRange(s.PopulationEstimate, truth * 3 / 4 - 1, truth * 5 / 4 + 1);
                    checkedCount++;
                }
        Assert.True(checkedCount >= 1);
    }

    [Fact]
    public void The_tech_estimate_is_never_wrong_and_covers_the_public_era()
    {
        MatchModel m = Fixture.Value;
        ResearchCatalog c = m.Research.Catalog;
        int believedTotal = 0;
        foreach (int civ in new[] { 0, 2 })
            foreach (RivalBelief r in BeliefOf(m, civ).Rivals)
            {
                if (r.Civ == civ) continue;
                for (int p = 0; p < c.Count; p++)
                    if (r.ProjectBelieved[p])
                    {
                        Assert.True(m.Research.IsDone(r.Civ, p), $"civ {civ} believes civ {r.Civ} has {c.Projects[p].Id}");
                        believedTotal++;
                    }
                for (int e = 0; e < r.Era; e++) Assert.True(r.ProjectBelieved[c.KeystoneLeaving(e)]);
            }
        Assert.True(believedTotal > 0);
        // The full model sees more than the public record: the Warlord's estimate of its seen neighbour beats the era closure.
        RivalBelief neighbour = BeliefOf(m, 0).Rivals[1];
        Assert.True(neighbour.LastSeenSeason >= 0);
        var publicOnly = new bool[c.Count];
        TechEstimate tech = ((BeliefBuilder)m.Ai!.SeatOf(0)!.Builder).Tech!;
        tech.AddEra(neighbour.Era, publicOnly);
        for (int p = 0; p < c.Count; p++) if (m.Research.FirstBy(p) == 1) tech.AddProject(p, publicOnly);
        Assert.True(Count(neighbour.ProjectBelieved) > Count(publicOnly), $"{Count(neighbour.ProjectBelieved)} vs {Count(publicOnly)}");
        Assert.True(neighbour.WeaponStrength > Fix64Of(6, 10));
    }

    [Fact]
    public void The_opponent_model_level_gates_what_is_kept()
    {
        MatchModel m = Fixture.Value;
        // Settler: diplomacy and the public record only.
        foreach (RivalBelief r in BeliefOf(m, 3).Rivals)
        {
            if (r.Civ == 3) continue;
            Assert.Empty(r.Settlements);
            Assert.DoesNotContain(true, r.ProjectBelieved);
            foreach (var p in r.ArchetypePosterior) InRangeFix(p, Fix64Of(199, 1000), Fix64Of(201, 1000)); // uniform
            Assert.Equal(Fix64Of(0, 1), r.Opportunity);
            Assert.Equal(m.Politics.Diplomacy.Relations(3, r.Civ), r.Relations);
        }
        Assert.True(BeliefOf(m, 3).Rivals[2].Contacted);
        // Chieftain: places, head counts and the posterior, no weapons, warriors, fighters or defenses.
        bool anySettlement = false;
        foreach (RivalBelief r in BeliefOf(m, 2).Rivals)
        {
            if (r.Civ == 2) continue;
            Assert.Equal(Fix64Of(6, 10), r.WeaponStrength);
            Assert.False(r.RangedSeen || r.WarriorsSeen || r.RaidPostureSeen);
            Assert.Equal(0, r.WarriorsEstimate);
            foreach (BeliefSettlement s in r.Settlements)
            {
                anySettlement = true;
                Assert.True(s.PopulationEstimate > 0);
                Assert.Equal(0, s.FightersEstimate);
                Assert.Equal(Fix64Of(0, 1), s.DefendingStrengthEstimate);
                Assert.False(s.ThornBarrierSeen || s.PalisadeSeen || s.WatchersSeen);
            }
        }
        Assert.True(anySettlement, "the Chieftain should have seen its neighbour by mid-E2");
        Assert.True(Sim.Numerics.Fix64.Abs(BeliefOf(m, 2).Rivals[3].ArchetypePosterior[0] - Fix64Of(1, 5)) > Fix64Of(1, 1000), "the Chieftain reads its neighbour");
    }

    [Fact]
    public void Chieftain_records_visible_rival_mining_camps_but_not_remote_ones()
    {
        MatchModel m = FogFairnessHarness.Clone(Fixture.Value, Controls);
        PoliticsSystem pol = m.Politics;
        int observer = 2, rivalCiv = 1;
        int ownSettlement = Enumerable.Range(0, pol.SettlementTotal)
            .First(s => pol.SettlementCivOf(s) == observer && !pol.IsFreeFolk(s));
        int rivalSettlement = Enumerable.Range(0, pol.SettlementTotal)
            .First(s => pol.SettlementCivOf(s) == rivalCiv && !pol.IsFreeFolk(s));
        int ownTile = pol.SettlementTileOf(ownSettlement);
        AiSeat seat = m.Ai!.SeatOf(observer)!;
        Belief belief = seat.Belief;
        int season = pol.Season;
        int reach = pol.ReachOf(observer);
        int routine = BeliefRules.RoutineSight(reach);
        int scout = BeliefRules.ScoutRange(reach,
            m.Research.IsDone(observer, m.Research.Catalog.IndexOf("WAY-01")),
            m.Research.IsDone(observer, m.Research.Catalog.IndexOf("WAY-03"))) + BeliefRules.SightRadius;
        int localTile = Enumerable.Range(0, m.Ai.World.Generated.Tiles.Length)
            .Where(t => m.Ai.World.Generated.TileAt(t).Deposit is DepositKind.CopperOre or DepositKind.ArsenicalCopper or DepositKind.Tin)
            .Where(t => HexGrid.Distance(ownTile, t, belief.Map.Width) <= routine)
            .OrderBy(t => HexGrid.Distance(ownTile, t, belief.Map.Width)).First();
        int remoteTile = Enumerable.Range(0, m.Ai.World.Generated.Tiles.Length)
            .Where(t => m.Ai.World.Generated.TileAt(t).Deposit is DepositKind.CopperOre or DepositKind.ArsenicalCopper or DepositKind.Tin)
            .Where(t => HexGrid.Distance(ownTile, t, belief.Map.Width) > Math.Max(routine, scout))
            .Where(t => belief.Map.OwnerSeen(t).Season != season)
            .OrderByDescending(t => HexGrid.Distance(ownTile, t, belief.Map.Width)).First();

        pol.ForceContact(observer, rivalCiv);
        m.Economy.FixtureAddMiningCamp(rivalSettlement, localTile);
        m.Economy.FixtureAddMiningCamp(rivalSettlement, remoteTile);
        int localId = Enumerable.Range(0, m.Economy.MiningCampCount).Select(m.Economy.MiningCamp)
            .Single(c => c.Tile == localTile && c.Civilization == rivalCiv).Id;
        int remoteId = Enumerable.Range(0, m.Economy.MiningCampCount).Select(m.Economy.MiningCamp)
            .Single(c => c.Tile == remoteTile && c.Civilization == rivalCiv).Id;

        new BeliefBuilder().Update(belief, m.Ai.World, seat.Profile);

        Assert.Equal(OpponentModel.ArchetypePosterior, seat.Profile.Opponent);
        Assert.Contains(belief.Rivals[rivalCiv].MiningCampsSeen, c => c.Id == localId && c.LastSeenSeason == season);
        Assert.DoesNotContain(belief.Rivals[rivalCiv].MiningCampsSeen, c => c.Id == remoteId);
        Assert.Equal(Fix64Of(6, 10), belief.Rivals[rivalCiv].WeaponStrength);
        Assert.False(belief.Rivals[rivalCiv].RangedSeen || belief.Rivals[rivalCiv].WarriorsSeen || belief.Rivals[rivalCiv].RaidPostureSeen);
    }

    [Fact]
    public void Threat_and_opportunity_are_filled_for_known_rivals_only()
    {
        MatchModel m = Fixture.Value;
        foreach (RivalBelief r in BeliefOf(m, 0).Rivals)
        {
            if (r.Civ == 0) continue;
            if (!r.Aware) { Assert.Equal(Fix64Of(0, 1), r.ThreatProbability); Assert.Equal(Fix64Of(0, 1), r.Opportunity); continue; }
            InRangeFix(r.ThreatProbability, Fix64Of(0, 1), ThreatModel.MaxProbability);
            InRangeFix(r.ThreatLoss, Fix64Of(0, 1), Fix64Of(1, 1));
            InRangeFix(r.Opportunity, Fix64Of(0, 1), Fix64Of(1, 1));
            Assert.True(r.ThreatLoss > Fix64Of(0, 1));
            _out.WriteLine($"civ 0 on civ {r.Civ}: threat {r.ThreatProbability} loss {r.ThreatLoss} opportunity {r.Opportunity} confidence {r.Confidence}");
        }
    }

    [Fact]
    public void A_match_with_AI_seats_saves_and_resumes_exactly()
    {
        MatchModel m = Fixture.Value;
        MatchModel copy = FogFairnessHarness.Clone(m, Controls);
        Assert.Equal(m.ComputeChecksum(), copy.ComputeChecksum());
        long tick = ResearchSystem.SeasonTicks * Seasons;
        MatchModel straight = FogFairnessHarness.Clone(m, Controls);
        new SimRunner(straight, SimRunner.DefaultChecksumInterval, tick).Advance(ResearchSystem.SeasonTicks * 4);
        new SimRunner(copy, SimRunner.DefaultChecksumInterval, tick).Advance(ResearchSystem.SeasonTicks * 2);
        MatchModel resumed = FogFairnessHarness.Clone(copy, Controls);
        Assert.Equal(copy.ComputeChecksum(), resumed.ComputeChecksum());
        new SimRunner(resumed, SimRunner.DefaultChecksumInterval, tick + ResearchSystem.SeasonTicks * 2).Advance(ResearchSystem.SeasonTicks * 2);
        Assert.Equal(straight.ComputeChecksum(), resumed.ComputeChecksum());
        Assert.Equal(FogFairnessHarness.HashOf(BeliefOf(straight, 0)), FogFairnessHarness.HashOf(BeliefOf(resumed, 0)));
    }

    [Fact]
    public void Belief_round_trips_through_its_own_serialization()
    {
        MatchModel m = Fixture.Value;
        Belief b = BeliefOf(m, 0);
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) b.Write(w);
        stream.Position = 0;
        GeneratedWorld g = FogFairnessHarness.World;
        var copy = new Belief(0, b.Rivals.Length, m.Research.Catalog.Count, new MapKnowledge(g.Tiles.ToArray(), g.Width, g.Height));
        using (var r = new BinaryReader(stream)) copy.Read(r);
        Assert.Equal(FogFairnessHarness.HashOf(b), FogFairnessHarness.HashOf(copy));
        var diffs = new System.Collections.Generic.List<string>();
        FogFairnessHarness.CompareBelief(b, copy, diffs);
        Assert.Empty(diffs);
    }

    [Fact]
    public void The_builder_is_cheap()
    {
        MatchModel m = Fixture.Value;
        TrueWorld world = m.Ai!.World;
        GeneratedWorld g = FogFairnessHarness.World;
        DifficultyProfile full = DifficultyProfile.From(TestPack.Pack, Difficulty.Warlord);
        var builders = new[] { new BeliefBuilder(), new BeliefBuilder() };
        var beliefs = new[]
        {
            new Belief(0, 4, m.Research.Catalog.Count, new MapKnowledge(g.Tiles.ToArray(), g.Width, g.Height)),
            new Belief(2, 4, m.Research.Catalog.Count, new MapKnowledge(g.Tiles.ToArray(), g.Width, g.Height)),
        };
        for (int i = 0; i < 5; i++) for (int k = 0; k < 2; k++) builders[k].Update(beliefs[k], world, full); // warm up; builds the tech tables
        const int runs = 200;
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++)
            for (int k = 0; k < 2; k++) builders[k].Update(beliefs[k], world, full);
        watch.Stop();
        double perCall = watch.Elapsed.TotalMilliseconds / (runs * 2);
        _out.WriteLine($"belief update: {perCall:0.000} ms per civilization per season");
        Assert.True(perCall < 2.0, $"{perCall} ms");
    }

    private static int Count(bool[] values) { int n = 0; foreach (bool v in values) if (v) n++; return n; }
    private static Sim.Numerics.Fix64 Fix64Of(int a, int b) => Sim.Numerics.Fix64.FromRatio(a, b);

    private static void InRangeFix(Sim.Numerics.Fix64 value, Sim.Numerics.Fix64 low, Sim.Numerics.Fix64 high)
        => Assert.True(value >= low && value <= high, $"{value} not in [{low}, {high}]");
}
