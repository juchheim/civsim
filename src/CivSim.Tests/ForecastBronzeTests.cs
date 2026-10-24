using CivSim.Sim.Ai;
using CivSim.Sim.Ai.Forecast;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using System.Linq;

namespace CivSim.Tests;

public sealed class ForecastBronzeTests
{
    [Fact]
    public void Own_view_captures_only_own_bronze_state_and_forecast_start_copies_it()
    {
        MatchModel match = ForecastTestKit.Match(20261019);
        var runner = new SimRunner(match);
        runner.Advance(ResearchSystem.SeasonTicks);
        var own = new OwnView();
        OwnViewBuilder.Build(own, 0, runner.Tick, match.Economy, match.Research, match.Politics);

        Assert.Equal(own.Settlements.Count, own.Bronze.SettlementCount);
        Assert.Equal(match.Research.Mandate(0), own.Bronze.Mandate);
        Assert.Equal(match.Research.AdminSpan(0), own.Bronze.AdminSpan);
        int rawRecharge = 0;
        for (int p = 0; p < match.Research.Catalog.Count; p++)
            if (match.Research.IsDone(0, p))
                foreach (EffectDef effect in match.Research.Catalog.Projects[p].Effects)
                    if (effect.Kind == EffectKind.Mandate) rawRecharge += effect.MandateRecharge;
        Assert.Equal(rawRecharge, own.Bronze.MandateRechargeRaw);
        Assert.Equal(match.Economy.CivHasMetalAccess(0, MetalAccessKind.Copper), own.Bronze.CopperAccess);
        Assert.Equal(match.Economy.CivHasMetalAccess(0, MetalAccessKind.Tin), own.Bronze.TinAccess);
        Assert.Equal(match.Economy.CivHasMetalAccess(0, MetalAccessKind.Arsenic), own.Bronze.ArsenicAccess);
        Assert.Equal(match.Politics.Compacts.Count(c => c.Active && c.Involves(0)), own.Bronze.Compacts.Count);
        Assert.All(own.Bronze.Settlements, s => Assert.InRange(s.Id, 0, match.Politics.SettlementTotal - 1));

        var start = new ForecastStart(new ForecastCatalog(TestPack.Catalog));
        start.CaptureBronze(own.Bronze);
        own.Bronze.SettlementCount++;
        Assert.NotEqual(start.Bronze.SettlementCount, own.Bronze.SettlementCount);
    }

    [Fact]
    public void Bronze_sidecar_applies_completed_mandate_and_admin_effects_then_recharges_deterministically()
    {
        ForecastCatalog catalog = new(TestPack.Catalog);
        int writing = catalog.Catalog.IndexOf("COM-05");
        Assert.True(writing >= 0);
        var start = new ForecastBronzeStart
        {
            Active = true, MandateUnlocked = true, Mandate = 0, MandateCap = 2, MandateRecharge = 8, MandateRechargeRaw = 8,
            MandateRechargeMin = catalog.Catalog.MandateRechargeMin,
            AdminSpan = 4, MeanOverreach = Fix64.FromInt(2), MeanSpanPenalty = Fix64.FromInt(3),
            SettlementCount = 8, SpanPenaltyPerExcess = Fix64.One,
        };
        var first = new ForecastBronzeRollout(start, catalog.Catalog);
        var second = new ForecastBronzeRollout(start.Copy(), catalog.Catalog);
        first.OnProjectCompleted(writing);
        second.OnProjectCompleted(writing);
        ForecastBronzeSeason afterCompletion = first.Snapshot(0);
        Assert.Equal(6, afterCompletion.AdminSpan);
        Assert.Equal(Fix64.FromRatio(1, 5), afterCompletion.AdminReduction);
        Assert.Equal(6, afterCompletion.MandateRecharge);
        Assert.Equal(Fix64.FromInt(2), afterCompletion.MeanSpanPenalty);
        for (int i = 0; i < 6; i++) { first.EndSeason(); second.EndSeason(); }
        Assert.Equal(first.Snapshot(6), second.Snapshot(6));
        Assert.Equal(1, first.Snapshot(6).Mandate);
    }

    [Fact]
    public void First_KEY3_mandate_effect_starts_from_zero_recharge_deltas()
    {
        ForecastCatalog catalog = new(TestPack.Catalog);
        int key3 = catalog.Catalog.IndexOf("KEY-3");
        var start = new ForecastBronzeStart
        {
            MandateUnlocked = false, MandateCap = 0, MandateRecharge = catalog.Catalog.MandateRechargeMin,
            MandateRechargeMin = catalog.Catalog.MandateRechargeMin,
        };
        var rollout = new ForecastBronzeRollout(start, catalog.Catalog);

        rollout.OnProjectCompleted(key3);

        ForecastBronzeSeason unlocked = rollout.Snapshot(0);
        Assert.Equal(8, unlocked.MandateRecharge);
        Assert.Equal(2, unlocked.MandateCap);
        Assert.Equal(1, unlocked.Mandate);
        for (int i = 0; i < 7; i++) rollout.EndSeason();
        Assert.Equal(1, rollout.Snapshot(7).Mandate);
        rollout.EndSeason();
        Assert.Equal(2, rollout.Snapshot(8).Mandate);
    }

    [Fact]
    public void Positive_mandate_recharge_delta_below_floor_uses_raw_sum()
    {
        ForecastCatalog catalog = new(TestPack.Catalog);
        int minimum = catalog.Catalog.MandateRechargeMin;
        var start = new ForecastBronzeStart
        {
            MandateUnlocked = true, MandateCap = 2, MandateRecharge = minimum, MandateRechargeRaw = minimum - 3,
            MandateRechargeMin = minimum,
        };
        var rollout = new ForecastBronzeRollout(start, catalog.Catalog);

        // Synthetic positive delta: adding it to the already floored value would incorrectly yield minimum + 1.
        rollout.ApplyMandateDelta(0, +1, 0);

        Assert.Equal(minimum, rollout.Snapshot(0).MandateRecharge);
    }
}
