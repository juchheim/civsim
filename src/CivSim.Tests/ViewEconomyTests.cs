using System;
using CivSim.Sim.Core;
using CivSim.Sim.Research;
using CivSim.Sim.Economy;
using CivSim.Sim.View;

namespace CivSim.Tests;

/// <summary>The economy block of the frame and the bottleneck rules behind the "Your people" sheet.</summary>
public class ViewEconomyTests
{
    private static int OwnRows(MatchFrame f, Action<int> each)
    {
        int rows = 0;
        for (int w = 0; w < f.SettlementCount; w++)
        {
            var flags = (SettlementViewFlags)f.SettlementFlags[w];
            if ((flags & (SettlementViewFlags.Own | SettlementViewFlags.Remembered | SettlementViewFlags.FreeFolk)) != SettlementViewFlags.Own) continue;
            rows++;
            each(w);
        }
        return rows;
    }

    [Fact]
    public void Own_settlements_carry_a_food_ledger_that_adds_up_and_other_settlements_carry_nothing()
    {
        MatchFrame f = ViewKit.Watched.Value.ViewerFrame;
        int ledgers = 0;
        int rows = OwnRows(f, w =>
        {
            int econ = w * EconField.Stride;
            Assert.Equal(1, f.SettlementEcon[econ + EconField.Valid]);
            Assert.True(f.SettlementEcon[econ + EconField.FoodCapacity] >= 12L << 32, "storage holds at least 12 rations");
            if (f.SettlementEcon[econ + EconField.LedgerValid] == 0) return;
            ledgers++;
            long start = f.SettlementEcon[econ + EconField.Ledger + EconField.LedgerStart];
            long gathered = f.SettlementEcon[econ + EconField.Ledger + EconField.LedgerGathered];
            long eaten = f.SettlementEcon[econ + EconField.Ledger + EconField.LedgerEaten];
            long spoiled = f.SettlementEcon[econ + EconField.Ledger + EconField.LedgerSpoiled];
            long lost = f.SettlementEcon[econ + EconField.Ledger + EconField.LedgerLostToCap];
            long end = f.SettlementEcon[econ + EconField.Ledger + EconField.LedgerEnd];
            // Start + gathered - eaten - spoiled - lost = end, to within what the clamps at zero can hide.
            Assert.InRange(Math.Abs(start + gathered - eaten - spoiled - lost - end), 0, 1L << 24);
            Assert.True(spoiled >= 0 && lost >= 0 && eaten >= 0);
        });
        Assert.True(rows > 0 && ledgers > 0, "the viewer has settlements with a completed season");
        for (int w = 0; w < f.SettlementCount; w++)
        {
            var flags = (SettlementViewFlags)f.SettlementFlags[w];
            if ((flags & SettlementViewFlags.Own) != 0 && (flags & SettlementViewFlags.Remembered) == 0 && (flags & SettlementViewFlags.FreeFolk) == 0) continue;
            Assert.Equal(0, f.SettlementEcon[w * EconField.Stride + EconField.Valid]);
            Assert.Equal(0, f.SettlementFoodStores[w]);
        }
    }

    [Fact]
    public void History_keeps_one_sample_a_season_up_to_sixteen_and_the_focus_civilization_has_its_own()
    {
        WatchedMatch watched = ViewKit.Watched.Value;
        MatchFrame f = watched.ViewerFrame;
        int series = MatchFrame.HistorySeriesCount;
        OwnRows(f, w =>
        {
            int count = f.SettlementHistoryCount[w];
            Assert.InRange(count, 1, MatchFrame.HistoryLength);
            // The newest sample was taken when the current season began, so it matches the settlement's population then.
            int pop = f.SettlementHistory[(w * MatchFrame.HistoryLength + count - 1) * series + HistorySeries.Population];
            Assert.InRange(pop, 1, 10_000);
        });
        Assert.InRange(f.FocusHistoryCount, 2, MatchFrame.HistoryLength);
        Assert.Equal(f.CivPopulation[f.FocusCiv], f.FocusHistory[(f.FocusHistoryCount - 1) * HistorySeries.CivSeries + HistorySeries.CivPopulation]);
    }

    [Fact]
    public void Learned_projects_report_the_season_they_were_learned()
    {
        MatchFrame f = ViewKit.Watched.Value.ViewerFrame;
        int learned = 0;
        for (int p = 0; p < f.FocusProjectCount; p++)
        {
            var state = (CivSim.Sim.Research.ProjectState)f.FocusProjectState[p];
            bool done = state is CivSim.Sim.Research.ProjectState.Completed or CivSim.Sim.Research.ProjectState.Dormant;
            Assert.Equal(done, f.FocusProjectDoneSeason[p] >= 0);
            if (done) { learned++; Assert.InRange(f.FocusProjectDoneSeason[p], 0, f.Season); }
        }
        Assert.True(learned > 0);
    }

    private static MatchFrame OneSettlement(int population, int securityPermille)
    {
        var f = new MatchFrame();
        f.Reserve(figures: 16, settlements: 4, herds: 4);
        f.Season = 8; f.SeasonOfYear = 0; f.ViewerCiv = 0; f.FocusCiv = 0; f.CivCount = 1;
        f.SettlementCount = 1;
        f.SettlementIndex[0] = 0; f.SettlementCiv[0] = 0; f.SettlementPopulation[0] = population;
        f.SettlementFlags[0] = (ushort)(SettlementViewFlags.Own | SettlementViewFlags.FireLit);
        int econ = 0;
        f.SettlementEcon[econ + EconField.Valid] = 1;
        f.SettlementEcon[econ + EconField.LedgerValid] = 1;
        f.SettlementEcon[econ + EconField.FoodSecurityPermille] = securityPermille;
        f.SettlementEcon[econ + EconField.ForecastPermille] = 1000;
        f.SettlementEcon[econ + EconField.FoodNeed] = 20L << 32;
        f.SettlementEcon[econ + EconField.FoodCapacity] = 27L << 32;
        f.SettlementEcon[econ + EconField.FoodCap] = 100L << 32;
        f.SettlementEcon[econ + EconField.HousingCap] = 100L << 32;
        f.SettlementEcon[econ + EconField.GroupCap] = 100L << 32;
        f.SettlementEcon[econ + EconField.HealthPermille] = 1000;
        f.SettlementEcon[econ + EconField.CrowdingFreePermille] = 1000;
        f.SettlementFoodStores[0] = 20L << 32;
        f.SettlementEcon[econ + EconField.BlockerStructure] = -1;
        f.FocusSlotProject[0] = 3; f.FocusSlotProject[1] = -1; f.FocusSlotProject[2] = -1;
        return f;
    }

    [Fact]
    public void Diagnosis_ranks_starvation_above_everything_and_stays_quiet_for_a_healthy_settlement()
    {
        var engine = new DiagnosisEngine();
        var into = new DiagnosisEntry[16];

        MatchFrame healthy = OneSettlement(20, 1000);
        Assert.Equal(0, engine.Evaluate(healthy, into));

        MatchFrame starving = OneSettlement(20, 600);
        starving.SettlementFlags[0] = (ushort)SettlementViewFlags.Own; // the fire is out as well
        starving.SettlementFoodStores[0] = 5L << 32;
        int n = engine.Evaluate(starving, into);
        Assert.True(n >= 2);
        Assert.Equal(DiagCode.Starving, into[0].Code);
        Assert.Equal(DiagSeverity.Critical, into[0].Severity);
        Assert.Equal(600, into[0].A);
        for (int i = 1; i < n; i++) Assert.True(into[i - 1].Score >= into[i].Score, "sorted by score");
        Assert.Contains(System.Linq.Enumerable.Take(into, n), e => e.Code == DiagCode.FireOut);
    }

    [Fact]
    public void Diagnosis_flags_full_stores_idle_research_and_a_nearly_full_land()
    {
        var engine = new DiagnosisEngine();
        var into = new DiagnosisEntry[16];
        MatchFrame f = OneSettlement(90, 1000);
        f.SettlementEcon[EconField.Ledger + EconField.LedgerLostToCap] = 9L << 32;
        f.SettlementEcon[EconField.Ledger + EconField.LedgerGathered] = 20L << 32;
        f.SettlementFoodStores[0] = 27L << 32;
        f.FocusSlotProject[0] = -1;
        int n = engine.Evaluate(f, into);
        var codes = new System.Collections.Generic.List<DiagCode>();
        for (int i = 0; i < n; i++) codes.Add(into[i].Code);
        Assert.Contains(DiagCode.StoresFull, codes);
        Assert.Contains(DiagCode.ResearchIdle, codes);
        Assert.Contains(DiagCode.CrowdingFood, codes);
        Assert.True(codes.IndexOf(DiagCode.ResearchIdle) < codes.IndexOf(DiagCode.StoresFull), "a critical finding outranks a warning");
    }
    [Fact]
    public void Action_and_labor_ledgers_are_consistent_and_payoffs_are_never_negative()
    {
        MatchFrame f = ViewKit.Watched.Value.ViewerFrame;
        int projects = f.PayoffProjectCount;
        Assert.True(projects > 10, "the content has measurable projects");
        var seen = new System.Collections.Generic.HashSet<int>();
        for (int m = 0; m < projects; m++) Assert.True(seen.Add(f.PayoffProject[m]), "each measurable project once");
        int paying = 0;
        OwnRows(f, w =>
        {
            int econ = w * EconField.Stride;
            for (int k = 0; k < LivingEconomyModel.ActionLedgerKinds; k++)
            {
                int line = econ + EconField.Actions + k * LivingEconomyModel.ActionLedgerFields;
                long assigned = f.SettlementEcon[line + LivingEconomyModel.ActionAssigned], applied = f.SettlementEcon[line + LivingEconomyModel.ActionApplied];
                Assert.InRange(applied, 0, assigned);
                Assert.True(f.SettlementEcon[line + LivingEconomyModel.ActionHarvested] >= 0 && f.SettlementEcon[line + LivingEconomyModel.ActionOutput] >= 0);
            }
            long capacity = f.SettlementEcon[econ + EconField.Labor + LivingEconomyModel.LaborCapacity];
            long used = 0;
            for (int i = LivingEconomyModel.LaborReserved; i <= LivingEconomyModel.LaborCivic; i++) used += f.SettlementEcon[econ + EconField.Labor + i];
            Assert.True(used <= capacity + (1L << 28), "labor lines do not exceed what was available");
            long rule = f.SettlementEcon[econ + EconField.Labor + LivingEconomyModel.LaborRule] >> 32;
            Assert.InRange(rule, 0, 5);
            for (int m = 0; m < projects; m++)
            {
                int o = (w * projects + m) * 3;
                Assert.True(f.SettlementPayoff[o] >= 0 && f.SettlementPayoff[o + 1] >= 0 && f.SettlementPayoff[o + 2] >= 0);
                if (f.SettlementPayoffInfo[o] == (byte)LivingEconomyModel.PayoffStatus.Paying && f.SettlementPayoff[o] > 0) paying++;
            }
        });
        Assert.True(paying > 0, "at least one learned project is counted as adding rations");
    }

    [Fact]
    public void Building_frames_never_changes_the_simulation()
    {
        MatchModel watched = ViewKit.NewMatch(), unwatched = ViewKit.NewMatch();
        var runnerA = new CivSim.Sim.Core.SimRunner(watched);
        var runnerB = new CivSim.Sim.Core.SimRunner(unwatched);
        var builder = new FrameBuilder(watched, 0);
        var frame = new MatchFrame();
        while (runnerA.Tick < 12 * 600)
        {
            runnerA.Advance(10);
            builder.Build(watched, frame);
        }
        runnerB.Advance(12 * 600);
        Assert.Equal(unwatched.ComputeChecksum(), watched.ComputeChecksum());
    }

    [Fact]
    public void Diagnosis_reports_unused_work_thin_land_and_builders_waiting()
    {
        var engine = new DiagnosisEngine();
        var into = new DiagnosisEntry[24];
        MatchFrame f = OneSettlement(20, 1000);
        int line = EconField.Actions + LivingEconomyModel.ActionForage * LivingEconomyModel.ActionLedgerFields;
        f.SettlementEcon[line + LivingEconomyModel.ActionAssigned] = 5L << 32;
        f.SettlementEcon[line + LivingEconomyModel.ActionApplied] = 3L << 32;
        int hunt = EconField.Actions + LivingEconomyModel.ActionHunt * LivingEconomyModel.ActionLedgerFields;
        f.SettlementEcon[hunt + LivingEconomyModel.ActionRequested] = 10L << 32;
        f.SettlementEcon[hunt + LivingEconomyModel.ActionHarvested] = 4L << 32;
        f.SettlementEcon[EconField.BlockerStructure] = 3; // a pit house
        f.SettlementEcon[EconField.BlockerSlot] = 8;
        f.SettlementEcon[EconField.BlockerHave] = 1L << 32;
        f.SettlementEcon[EconField.BlockerNeed] = 4L << 32;
        int n = engine.Evaluate(f, into);
        var found = new System.Collections.Generic.Dictionary<DiagCode, DiagnosisEntry>();
        for (int i = 0; i < n; i++) found[into[i].Code] = into[i];
        Assert.Equal(200, found[DiagCode.ToolLimitedWork].B);
        Assert.Equal(LivingEconomyModel.ActionForage, found[DiagCode.ToolLimitedWork].A);
        Assert.Equal(LivingEconomyModel.ActionHunt, found[DiagCode.LandLimited].A);
        Assert.Equal(400, found[DiagCode.LandLimited].B);
        Assert.Equal(3 | 9 << 8, found[DiagCode.BuildWaiting].A);
        Assert.Equal(100, found[DiagCode.BuildWaiting].B);
        Assert.Equal(400, found[DiagCode.BuildWaiting].C);
    }
}
