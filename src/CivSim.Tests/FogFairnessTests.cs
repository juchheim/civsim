using System.Collections.Generic;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using Xunit.Abstractions;

namespace CivSim.Tests;

/// <summary>
/// Acceptance A8 (docs/MVP/14 §10.4 "Cheating check", 15 rule D10): change what an AI cannot see and nothing it received
/// or did changes; change what it can see and its belief does.
/// </summary>
public class FogFairnessTests
{
    private const int Observer = 0;
    /// <summary>Tiles kept clear of every sight rule, enough for an era's reach and WAY-03's scouts to grow within the step.</summary>
    private const int Margin = 8;

    private readonly ITestOutputHelper _out;
    public FogFairnessTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Unseen_changes_leave_the_observers_belief_view_and_choices_unchanged_and_seen_ones_do_not()
    {
        CivControl[] controls = FogFairnessHarness.DefaultControls();
        MatchModel match = FogFairnessHarness.NewMatch(controls);
        var runner = new SimRunner(match);
        int contactSeason = -1, unseenChecks = 0, seenSettlementChecks = 0, seenWeaponChecks = 0;
        bool beforeContact = false, afterContact = false, midE2 = false;
        for (int season = 1; season <= 124; season++)
        {
            runner.Advance(ResearchSystem.SeasonTicks - 1);
            long tick = runner.Tick;
            bool contacted = false;
            for (int o = 1; o < 4; o++) contacted |= match.Politics.Diplomacy.Contacted(Observer, o);
            if (contacted && contactSeason < 0) contactSeason = season;

            string? label = null;
            if (season == 40) { label = "before contact"; beforeContact = true; }
            else if (contactSeason > 0 && season == contactSeason + 3) { label = "after contact"; afterContact = true; }
            else if (season == 120 && match.Research.Era(Observer) >= 2) { label = "mid E2"; midE2 = true; }
            if (label is null) { runner.Advance(1); continue; }

            // Seen: the belief must notice (copies of the match as it stands, before the pair below advances it).
            if (season > 40)
            {
                if (TrySeenSettlement(match, controls, tick)) seenSettlementChecks++;
                if (TrySeenWeapon(match, controls, tick)) seenWeaponChecks++;
            }

            // Unseen: identical minds.
            int changes = 0;
            (MatchModel a, MatchModel b) = FogFairnessHarness.RunPair(match, tick, controls,
                (copy, copyRunner) => changes = FogFairnessHarness.PerturbUnseen(copy, copyRunner, Observer, Margin));
            Assert.True(changes > 0, $"{label}: nothing out of sight to change");
            Assert.NotEqual(a.ComputeChecksum(), b.ComputeChecksum()); // the world did change
            List<string> diffs = FogFairnessHarness.CompareMinds(a, b, Observer, tick);
            Assert.True(diffs.Count == 0, $"{label} (season {season}, {changes} unseen changes): " + string.Join("; ", diffs));
            unseenChecks++;
            _out.WriteLine($"{label}: season {season}, {changes} unseen changes, belief hash {FogFairnessHarness.HashOf(a.Ai!.SeatOf(Observer)!.Belief):X}");

            runner = new SimRunner(match, SimRunner.DefaultChecksumInterval, tick + 1);
        }
        Assert.True(beforeContact && afterContact && midE2, $"checkpoints: {beforeContact} {afterContact} {midE2}, contact season {contactSeason}");
        Assert.Equal(3, unseenChecks);
        Assert.True(seenSettlementChecks + seenWeaponChecks >= 1, "no sanity check could be made");
        _out.WriteLine($"seen-settlement checks {seenSettlementChecks}, seen-weapon checks {seenWeaponChecks}");
    }

    /// <summary>Kills half the people of a rival settlement in plain sight in a copy: the head count believed must change.</summary>
    private static bool TrySeenSettlement(MatchModel match, CivControl[] controls, long tick)
    {
        TrueWorld world = match.Ai!.World;
        int target = -1;
        for (int s = 0; s < match.Politics.SettlementTotal && target < 0; s++)
        {
            int owner = match.Politics.SettlementCivOf(s);
            if (owner == Observer || match.Politics.IsFreeFolk(s) || !match.Politics.Diplomacy.Aware(Observer, owner)) continue;
            if (match.Politics.SettlementPopulationOf(s) >= 8 && BeliefBuilder.TilesOutOfSight(world, Observer, s) <= -2) target = s;
        }
        if (target < 0) return false;
        MatchModel a = FogFairnessHarness.Clone(match, controls), b = FogFairnessHarness.Clone(match, controls);
        var figures = new List<FigureView>();
        b.Economy.ListFigures(target, figures);
        for (int i = 0; i < figures.Count; i += 2) b.Economy.KillFigure(figures[i].Id);
        new SimRunner(a, SimRunner.DefaultChecksumInterval, tick).Advance(1);
        new SimRunner(b, SimRunner.DefaultChecksumInterval, tick).Advance(1);
        BeliefSettlement? x = Find(a.Ai!.SeatOf(Observer)!.Belief, target), y = Find(b.Ai!.SeatOf(Observer)!.Belief, target);
        Assert.NotNull(x);
        Assert.NotNull(y);
        Assert.NotEqual(x!.PopulationEstimate, y!.PopulationEstimate);
        Assert.NotEqual(FogFairnessHarness.HashOf(a.Ai.SeatOf(Observer)!.Belief), FogFairnessHarness.HashOf(b.Ai.SeatOf(Observer)!.Belief));
        return true;
    }

    /// <summary>Hands a rival whose figures are in sight a better weapon in a copy: the weapon believed must change.</summary>
    private static bool TrySeenWeapon(MatchModel match, CivControl[] controls, long tick)
    {
        TrueWorld world = match.Ai!.World;
        ResearchCatalog c = match.Research.Catalog;
        for (int o = 1; o < match.Research.CivCount; o++)
        {
            if (!match.Politics.Diplomacy.Aware(Observer, o) || BeliefBuilder.TilesOutOfFigureSight(world, Observer, o) > -2) continue;
            int bow = c.IndexOf("HNT-06"), archery = c.IndexOf("WAR-03");
            int grant = !match.Research.IsDone(o, bow) ? bow : !match.Research.IsDone(o, archery) ? archery : -1;
            if (grant < 0) continue;
            MatchModel a = FogFairnessHarness.Clone(match, controls), b = FogFairnessHarness.Clone(match, controls);
            var rb = new SimRunner(b, SimRunner.DefaultChecksumInterval, tick);
            rb.Submit(new Command(tick, (byte)o, CommandKind.CompleteProject, grant, 0, 0));
            new SimRunner(a, SimRunner.DefaultChecksumInterval, tick).Advance(1);
            rb.Advance(1);
            RivalBelief x = a.Ai!.SeatOf(Observer)!.Belief.Rivals[o], y = b.Ai!.SeatOf(Observer)!.Belief.Rivals[o];
            Assert.NotEqual(x.WeaponStrength, y.WeaponStrength);
            Assert.True(y.ProjectBelieved[grant], "the weapon seen proves its project");
            return true;
        }
        return false;
    }

    private static BeliefSettlement? Find(Belief belief, int settlement)
    {
        foreach (RivalBelief r in belief.Rivals)
            foreach (BeliefSettlement s in r.Settlements)
                if (s.Settlement == settlement) return s;
        return null;
    }
}
