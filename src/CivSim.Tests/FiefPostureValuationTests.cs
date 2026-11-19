using System.Reflection;
using CivSim.Sim.Ai;
using CivSim.Sim.Ai.Forecast;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Tests;

public sealed class FiefPostureValuationTests
{
    [Theory]
    [InlineData(ChapterMode.Fief, 75, 10, 35, 3450)]
    [InlineData(ChapterMode.Fief, 75, 0, 0, 3450)]
    [InlineData(ChapterMode.Fief, 39, 0, 0, 0)]
    [InlineData(ChapterMode.Fief, 75, 45, 0, 0)]
    [InlineData(ChapterMode.Fief, 75, 0, 45, 0)]
    [InlineData(ChapterMode.Bronze, 75, 10, 35, 0)]
    [InlineData(ChapterMode.Iron, 75, 10, 35, 0)]
    [InlineData(ChapterMode.Bronze, 75, 0, 0, 3600)]
    [InlineData(ChapterMode.Iron, 75, 0, 0, 3600)]
    public void Spark_posture_value_uses_Fief_intent_units_and_preserves_conflict_guards_and_older_chapters(
        ChapterMode mode, int frontier, int defense, int attack, int expectedBonusHundredths)
    {
        const ulong seed = 20261046;
        var controls = new[] { CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced),
            CivControl.Council, CivControl.Council, CivControl.Council };
        MatchModel match = MatchModel.Create(seed, TestPack.Pack, ForecastTestKit.World(seed),
            ForecastTestKit.Balanced, controls, rules: ChapterRules.For(mode));
        match.Ai!.OnSeasonEnd(0);
        AiSeat seat = match.Ai.SeatOf(0)!;
        ResearchSpark spark = Assert.IsType<ResearchSpark>(seat.Spark);
        seat.Context.Intent.FrontierValue = Fix64.FromRatio(frontier, 100);
        seat.Context.Intent.DefenseUrgency = Fix64.FromRatio(defense, 100);
        seat.Context.Intent.AttackReadiness = Fix64.FromRatio(attack, 100);
        int gov02 = match.Research.Catalog.IndexOf("GOV-02");
        Assert.False(match.Research.IsDone(0, gov02));

        // Compare the actual valuation built by Decide with its bonus disabled. This exercises the caller's
        // conversion of intent units, which direct tests of PostureUnlockBonus alone cannot catch.
        spark.PostureUnlockBonusOverrideForDiagnostics = Fix64.Zero;
        seat.Context.Season++;
        spark.Decide(seat.Context);
        Fix64 withoutPosture = Valuation(spark).Bonus[gov02];
        spark.PostureUnlockBonusOverrideForDiagnostics = null;
        seat.Context.Season++;
        spark.Decide(seat.Context);
        Assert.Equal(Fix64.FromRatio(expectedBonusHundredths, 100), Valuation(spark).Bonus[gov02] - withoutPosture);
    }

    [Theory]
    [InlineData(Difficulty.Warlord, 3450)]
    [InlineData(Difficulty.Legend, 6000)]
    public void Fief_posture_value_steps_up_with_Warlord_and_Legend_difficulty(Difficulty difficulty, int expectedBonusHundredths)
    {
        const ulong seed = 20261046;
        var controls = new[] { CivControl.Ai(difficulty, EconomyArchetype.Balanced),
            CivControl.Council, CivControl.Council, CivControl.Council };
        MatchModel match = MatchModel.Create(seed, TestPack.Pack, ForecastTestKit.World(seed),
            ForecastTestKit.Balanced, controls, rules: ChapterRules.For(ChapterMode.Fief));
        match.Ai!.OnSeasonEnd(0);
        AiSeat seat = match.Ai.SeatOf(0)!;
        ResearchSpark spark = Assert.IsType<ResearchSpark>(seat.Spark);
        seat.Context.Intent.FrontierValue = Fix64.FromRatio(75, 100);
        seat.Context.Intent.DefenseUrgency = Fix64.Zero;
        seat.Context.Intent.AttackReadiness = Fix64.Zero;
        int gov02 = match.Research.Catalog.IndexOf("GOV-02");
        Assert.False(match.Research.IsDone(0, gov02));

        spark.PostureUnlockBonusOverrideForDiagnostics = Fix64.Zero;
        seat.Context.Season++;
        spark.Decide(seat.Context);
        Fix64 withoutPosture = Valuation(spark).Bonus[gov02];
        spark.PostureUnlockBonusOverrideForDiagnostics = null;
        seat.Context.Season++;
        spark.Decide(seat.Context);
        Assert.Equal(Fix64.FromRatio(expectedBonusHundredths, 100), Valuation(spark).Bonus[gov02] - withoutPosture);
    }

    private static ForecastValuation Valuation(ResearchSpark spark)
        => (ForecastValuation)typeof(ResearchSpark).GetField("_value", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(spark)!;
}
