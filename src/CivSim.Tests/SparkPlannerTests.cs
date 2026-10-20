using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;

namespace CivSim.Tests;

/// <summary>The AI Spark's planning rules (docs/MVP/14 §4): keystone routing, plans written like a player's, determinism, saves.</summary>
public class SparkPlannerTests
{
    private static readonly CivControl Warlord = CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced);

    [Fact]
    public void A_ready_keystone_is_in_Primary_after_every_decision_and_slots_never_idle()
    {
        // Blunder detector "keystone stall" (14 §10.4): breadth met and prerequisites done means the keystone holds Primary
        // after the next decision. The plan also fills all three slots and queues more, so the Council never has to.
        MatchModel match = ForecastTestKit.Match(20261019, Warlord, CivControl.Council, CivControl.Council, Warlord);
        var runner = new SimRunner(match);
        ResearchSystem r = match.Research;
        ResearchCatalog cat = r.Catalog;
        int checkedKeystones = 0;
        for (int season = 1; season <= 88; season++)
        {
            runner.Advance(ResearchSystem.SeasonTicks);
            foreach (int civ in new[] { 0, 3 })
            {
                int key = cat.KeystoneLeaving(r.Era(civ));
                if (key >= 0 && r.State(civ, key) is ProjectState.Available or ProjectState.Slotted)
                {
                    Assert.Equal(key, r.Slot(civ, 0));
                    checkedKeystones++;
                }
                for (int s = 0; s < CivResearch.SlotCount; s++) Assert.True(r.Slot(civ, s) >= 0, $"civ {civ} slot {s} idle in season {season}");
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
        // 14 §6.4: when the Strategist reads a threat, DEF and WAR projects jump in priority.
        int DefenseDone(bool threatened)
        {
            MatchModel match = ForecastTestKit.Match(777, Warlord, CivControl.Council, CivControl.Council, CivControl.Council);
            if (threatened) match.Ai!.SeatOf(0)!.Context.Intent.DefenseUrgency = Sim.Numerics.Fix64.One;
            new SimRunner(match).Advance(9 * ForecastTestKit.TicksPerMinute);
            ResearchCatalog cat = match.Research.Catalog;
            return cat.Projects.Count(p => p.Lineage >= 0 && cat.LineageCodes[p.Lineage] is "DEF" or "WAR" && match.Research.IsDone(0, p.Index));
        }
        int calm = DefenseDone(false), threatened = DefenseDone(true);
        Assert.True(threatened > calm, $"DEF/WAR projects done by minute 9: threatened {threatened}, calm {calm}");
    }

    [Fact]
    public void Thinking_budgets_grow_with_difficulty_and_stay_inside_the_Warlord_budget()
    {
        // 14 §9.2, §12: deeper search at higher levels, counted in rollouts. Warlord about 150 a decision (a few ms), Legend
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
        Assert.InRange(perDecision[2], 50, 220);
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
