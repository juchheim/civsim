using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>The AI Spark's planning rules (docs/MVP/14 §4): keystone routing, plans written like a player's, determinism, saves.</summary>
public class SparkPlannerTests
{
    private static readonly CivControl Warlord = CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced);

    [Fact]
    public void Posture_unlock_value_favors_GOV_02_only_for_open_frontiers_without_urgent_conflict()
    {
        Assert.Equal(Fix64.FromInt(27), ResearchSpark.PostureUnlockBonus(Fix64.FromRatio(75, 100), Fix64.Zero, Fix64.Zero));
        Assert.Equal(Fix64.Zero, ResearchSpark.PostureUnlockBonus(Fix64.FromRatio(39, 100), Fix64.Zero, Fix64.Zero));
        Assert.Equal(Fix64.Zero, ResearchSpark.PostureUnlockBonus(Fix64.FromRatio(75, 100), StrategistTuning.FortifyAt, Fix64.Zero));
        Assert.Equal(Fix64.Zero, ResearchSpark.PostureUnlockBonus(Fix64.FromRatio(75, 100), Fix64.Zero, StrategistTuning.FortifyAt));
    }

    [Fact]
    public void Posture_unlock_override_preserves_default_and_frontier_conflict_guards()
    {
        Fix64 openFrontier = Fix64.FromRatio(75, 100);
        Assert.Equal(Fix64.FromInt(27), ResearchSpark.PostureUnlockBonus(openFrontier, Fix64.Zero, Fix64.Zero));
        Assert.Equal(Fix64.FromRatio(81, 2), ResearchSpark.PostureUnlockBonus(openFrontier, Fix64.Zero, Fix64.Zero,
            Fix64.FromInt(54)));
        Assert.Equal(Fix64.Zero, ResearchSpark.PostureUnlockBonus(openFrontier, StrategistTuning.FortifyAt,
            Fix64.Zero, Fix64.FromInt(54)));
        Assert.Equal(Fix64.Zero, ResearchSpark.PostureUnlockBonus(Fix64.FromRatio(39, 100), Fix64.Zero,
            Fix64.Zero, Fix64.FromInt(54)));
    }

    [Fact]
    public void Bronze_fork_uses_only_feedable_routes_and_breaks_a_two_route_tie_toward_tin()
    {
        ResearchCatalog catalog = TestPack.Catalog;
        int met04 = catalog.IndexOf("MET-04"), met05 = catalog.IndexOf("MET-05");

        Assert.Equal(Fix64.Zero, ResearchSpark.BronzeForkBonus(met05, met04, met05, met03Done: false,
            min04Done: true, tinAccess: true, arsenicAccess: true));
        Assert.Equal(Fix64.Zero, ResearchSpark.BronzeForkBonus(met04, met04, met05, met03Done: true,
            min04Done: false, tinAccess: false, arsenicAccess: true));

        Fix64 tinOnly = ResearchSpark.BronzeForkBonus(met05, met04, met05, met03Done: true,
            min04Done: false, tinAccess: true, arsenicAccess: false);
        Fix64 arsenicOnly = ResearchSpark.BronzeForkBonus(met04, met04, met05, met03Done: true,
            min04Done: true, tinAccess: false, arsenicAccess: true);
        Assert.Equal(SparkTuning.BronzeRouteBonus, tinOnly);
        Assert.Equal(SparkTuning.BronzeRouteBonus, arsenicOnly);

        Fix64 bothArsenic = ResearchSpark.BronzeForkBonus(met04, met04, met05, met03Done: true,
            min04Done: true, tinAccess: true, arsenicAccess: true);
        Fix64 bothTin = ResearchSpark.BronzeForkBonus(met05, met04, met05, met03Done: true,
            min04Done: true, tinAccess: true, arsenicAccess: true);
        Assert.Equal(SparkTuning.TinRouteTieBreak, bothTin - bothArsenic);
    }

    [Fact]
    public void Bronze_fork_values_believed_tin_sources_and_keeps_arsenic_behind_MIN_04()
    {
        ResearchCatalog catalog = TestPack.Catalog;
        int met04 = catalog.IndexOf("MET-04"), met05 = catalog.IndexOf("MET-05");
        var tiles = new WorldTile[100];
        var map = new MapKnowledge(tiles, 10, 10);
        var ctx = new AiContext
        {
            Civ = 0,
            Season = 10,
            Own = new OwnView { Civ = 0, Season = 10, Others = new PairView[2] },
            Belief = new Belief(0, 2, catalog.Count, map) { Season = 10 },
        };
        ctx.Own.Settlements.Add(new OwnSettlement { Tile = 0 });

        // Direct sight of a Tin Folk village in reach supplies a plausible tin route.
        ctx.Belief.TinFolkSeen.Add(new BeliefTinFolk { Settlement = 5, Tile = 1, LastSeenSeason = 10 });
        Fix64 tinChance = ResearchSpark.TinAccessProbability(ctx, cartReach: false, waterReach: false);
        Assert.Equal(Fix64.FromRatio(1, 2), tinChance);
        Assert.True(ResearchSpark.BronzeForkBonus(met05, met04, met05, met03Done: true,
            min04Done: false, tinChance, arsenicAccess: false) > Fix64.Zero);
        Assert.Equal(Fix64.Zero, ResearchSpark.BronzeForkBonus(met04, met04, met05, met03Done: true,
            min04Done: false, tinChance, arsenicAccess: true));
    }

    [Fact]
    public void Spark_tin_access_probability_does_not_read_unseen_deposits()
    {
        var tiles = new WorldTile[100];
        var map = new MapKnowledge(tiles, 10, 10);
        var ctx = new AiContext
        {
            Civ = 0,
            Season = 10,
            Own = new OwnView { Civ = 0, Season = 10, Others = new PairView[2] },
            Belief = new Belief(0, 2, TestPack.Catalog.Count, map) { Season = 10 },
        };
        ctx.Own.Settlements.Add(new OwnSettlement { Tile = 0 });
        Fix64 before = ResearchSpark.TinAccessProbability(ctx, cartReach: false, waterReach: false);

        // Mutate only the underlying truth at a tile the map has never revealed.
        tiles[55].Deposit = DepositKind.Tin;
        Fix64 after = ResearchSpark.TinAccessProbability(ctx, cartReach: false, waterReach: false);

        Assert.Equal(Fix64.Zero, before);
        Assert.Equal(before, after);
    }

    [Fact]
    public void A_ready_keystone_is_in_Primary_after_every_decision_and_available_projects_are_slotted()
    {
        // Blunder detector "keystone stall" (14 §10.4): breadth met and prerequisites done means the keystone holds Primary
        // after the next decision. The plan also fills all three slots and queues more, so the Council never has to.
        MatchModel match = ForecastTestKit.Match(20261019, Warlord, CivControl.Council, CivControl.Council, Warlord);
        var runner = new SimRunner(match);
        ResearchSystem r = match.Research;
        ResearchCatalog cat = r.Catalog;
        int checkedKeystones = 0;
        bool[] completedDuringGrace = new bool[4];
        for (int season = 1; season <= 88; season++)
        {
            runner.Advance(ResearchSystem.SeasonTicks);
            // Completions may open slots late in the season; allow the configured AI selection grace before
            // asserting that an Available project was left idle. A completion during that grace window starts a
            // fresh timer, so defer that civilization's sample to its next decision.
            long graceStart = runner.Tick;
            runner.Advance(30);
            completedDuringGrace[0] = completedDuringGrace[3] = false;
            foreach (ResearchEvent e in r.Events)
                if ((e.Civ is 0 or 3) && e.Kind == ResearchEventKind.Completed && e.Tick >= graceStart)
                    completedDuringGrace[e.Civ] = true;
            foreach (int civ in new[] { 0, 3 })
            {
                int key = cat.KeystoneLeaving(r.Era(civ));
                if (key >= 0 && r.State(civ, key) is ProjectState.Available or ProjectState.Slotted)
                {
                    Assert.Equal(key, r.Slot(civ, 0));
                    checkedKeystones++;
                }
                bool hasEmptySlot = false;
                for (int s = 0; s < CivResearch.SlotCount; s++)
                    if (r.Slot(civ, s) < 0) { hasEmptySlot = true; break; }
                if (hasEmptySlot && !completedDuringGrace[civ])
                {
                    bool hasUnslottedAvailableProject = false;
                    for (int p = 0; p < cat.Count; p++)
                    {
                        if (!cat.Projects[p].Numeric || r.State(civ, p) != ProjectState.Available) continue;
                        bool slotted = false;
                        for (int s = 0; s < CivResearch.SlotCount; s++)
                            if (r.Slot(civ, s) == p) { slotted = true; break; }
                        if (!slotted) { hasUnslottedAvailableProject = true; break; }
                    }
                    Assert.False(hasUnslottedAvailableProject,
                        $"civ {civ} left an Available project unslotted in season {season}");
                }
                Assert.NotEmpty(r.Queue(civ));
            }
        }
        Assert.True(checkedKeystones >= 4, $"keystones seen ready: {checkedKeystones}");
        // Forced backfills were scheduled: both AI civilizations reached Sedentism, whose closure reaches back into E0.
        foreach (int civ in new[] { 0, 3 })
        {
            Assert.True(r.IsDone(civ, cat.IndexOf("KEY-2")), $"civ {civ} has KEY-2 by minute 22");
            Assert.All(Enumerable.Range(0, cat.Count).Where(p => cat.ClosureOf(1)![p]), p => Assert.True(r.IsDone(civ, p)));
        }
        // Every slot the Spark filled carries a reason (14 §11).
        Assert.All(match.Ai!.Log.Entries.Where(e => e.Layer == "Spark"), e => Assert.False(string.IsNullOrEmpty(e.Reason)));
        Assert.Contains(match.Ai.Log.Entries, e => e.Layer == "Spark" && e.Reason == "keystone path");
    }

    [Fact]
    public void The_Strategists_threat_reading_moves_defense_research_forward()
    {
        // 14 §6.4: when the Strategist reads a threat, DEF and WAR projects jump in priority. Two identical matches reach the
        // same season end (the real Strategist and Spark have decided); the Spark then re-plans once with the threat reading
        // the Strategist would hand it, calm in one copy and urgent in the other.
        (int First, bool Reasoned, string Plan) Planned(Sim.Numerics.Fix64 urgency)
        {
            MatchModel match = ForecastTestKit.Match(777, Warlord, CivControl.Council, CivControl.Council, CivControl.Council);
            new SimRunner(match).Advance(8 * ForecastTestKit.TicksPerMinute);
            AiSeat seat = match.Ai!.SeatOf(0)!;
            long tick = match.Research.Tick;
            seat.Context.Intent.DefenseUrgency = urgency;
            seat.Spark.Decide(seat.Context);
            ResearchCatalog cat = match.Research.Catalog;
            bool Military(int p) => cat.Projects[p].Lineage >= 0 && cat.LineageCodes[cat.Projects[p].Lineage] is "DEF" or "WAR";
            bool DependsOn(int project, int prerequisite)
            {
                foreach (int required in cat.Projects[project].Requires)
                    if (required == prerequisite || DependsOn(required, prerequisite)) return true;
                foreach (int required in cat.Projects[project].RequiresAny)
                    if (required == prerequisite || DependsOn(required, prerequisite)) return true;
                return false;
            }
            bool MilitaryPath(int p)
            {
                int era = match.Research.Era(0);
                if (match.Research.IsDone(0, p) || cat.Projects[p].Era > era) return false;
                if (Military(p)) return true;
                foreach (int target in Enumerable.Range(0, cat.Count))
                    if (cat.Projects[target].Era <= era && !match.Research.IsDone(0, target) && Military(target) && DependsOn(target, p)) return true;
                return false;
            }
            var plan = ((ResearchSpark)seat.Spark).LastPlan;
            int first = -1;
            for (int i = 0; i < plan.Count && first < 0; i++) if (MilitaryPath(plan[i])) first = i;
            // Reasons are logged for slots a decision changed (14 §11), including when a military-path pick is also part of a keystone path.
            var pathChanges = match.Ai.Log.Entries.Where(e => e.Civ == 0 && e.Tick >= tick && e.Layer == "Spark" && MilitaryPath(e.Subject)).ToArray();
            bool reasoned = pathChanges.Length == 0 || pathChanges.All(e => !string.IsNullOrWhiteSpace(e.Reason));
            return (first, reasoned, string.Join(" ", plan.Select(q => cat.Projects[q].Id)));
        }
        var calm = Planned(Sim.Numerics.Fix64.Zero);
        var threatened = Planned(Sim.Numerics.Fix64.One);
        // Urgent: a DEF/WAR project or its prerequisite path among the first three, before the calm plan reaches it.
        Assert.True(threatened.First is >= 0 and <= 2, $"threatened: {threatened.Plan} | calm: {calm.Plan}");
        Assert.True(calm.First < 0 || calm.First > threatened.First, $"threatened: {threatened.Plan} | calm: {calm.Plan}");
        Assert.True(threatened.Reasoned, $"a slotted defense pick logs its reason: {threatened.Plan}");
    }

    [Fact]
    public void Thinking_budgets_grow_with_difficulty_and_stay_inside_the_Warlord_budget()
    {
        // 14 §9.2, §12: deeper search at higher levels, counted in rollouts. Warlord about 220 a decision (about 4 ms), Legend
        // at most twice that.
        MatchModel match = ForecastTestKit.Match(4242,
            CivControl.Ai(Difficulty.Settler, EconomyArchetype.Balanced), CivControl.Ai(Difficulty.Chieftain, EconomyArchetype.Balanced),
            CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Ai(Difficulty.Legend, EconomyArchetype.Balanced));
        new SimRunner(match).Advance(8 * ForecastTestKit.TicksPerMinute);
        double[] perDecision = Enumerable.Range(0, 4).Select(c =>
        {
            var spark = (ResearchSpark)match.Ai!.SeatOf(c)!.Spark;
            return (double)spark.Rollouts / spark.Decisions;
        }).ToArray();
        Assert.True(perDecision[0] < perDecision[1] && perDecision[1] < perDecision[2] && perDecision[2] < perDecision[3],
            "rollouts per decision: " + string.Join(", ", perDecision.Select(x => x.ToString("0"))));
        Assert.InRange(perDecision[2], 50, 260);
        Assert.True(perDecision[3] <= 2 * perDecision[2] + 1, $"Legend {perDecision[3]:0} vs Warlord {perDecision[2]:0}");
        // The Settler thinks once a year (AiSystem's cadence), the others every season.
        Assert.Equal(8, ((ResearchSpark)match.Ai!.SeatOf(0)!.Spark).Decisions);
        Assert.Equal(32, ((ResearchSpark)match.Ai.SeatOf(2)!.Spark).Decisions);
    }

    [Fact]
    public void Plans_are_deterministic_and_resume_exactly_from_a_snapshot()
    {
        CivControl[] controls = { Warlord, CivControl.Council, CivControl.Council, CivControl.Ai(Difficulty.Settler, EconomyArchetype.Builder) };
        MatchModel straight = ForecastTestKit.Match(31, controls);
        new SimRunner(straight).Advance(150 * 40);
        MatchModel twin = ForecastTestKit.Match(31, controls);
        new SimRunner(twin).Advance(150 * 40);
        Assert.Equal(straight.ComputeChecksum(), twin.ComputeChecksum());
        Assert.Equal(straight.Ai!.Log.Entries.ToArray(), twin.Ai!.Log.Entries.ToArray());

        MatchModel first = ForecastTestKit.Match(31, controls);
        new SimRunner(first).Advance(150 * 21);
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) first.WriteSnapshot(w);
        stream.Position = 0;
        MatchModel resumed = ForecastTestKit.Match(31, controls);
        using (var r = new BinaryReader(stream)) resumed.ReadSnapshot(r);
        Assert.Equal(first.ComputeChecksum(), resumed.ComputeChecksum());
        new SimRunner(resumed, 50, 150 * 21).Advance(150 * 19);
        Assert.Equal(straight.ComputeChecksum(), resumed.ComputeChecksum());
    }
}
