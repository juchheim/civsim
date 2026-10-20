using CivSim.Sim.Numerics;

namespace CivSim.Sim.Ai.Forecast;

/// <summary>
/// The forecast's demographic constants (docs/MVP/14 §14: AI parameters are data). The live economy ages people exactly:
/// children become adults at 14 (56 seasons) and adults elders at 45 (docs/MVP/07c §7a), so the forecast carries children
/// in a 56-season pipeline. Values were fitted to 20 live Council civilizations (five seeds): the
/// effective adults (adults + 2 × elders) 3 to 32 minutes ahead come within about 16% RMS from any start, which is the
/// spread between the live civilizations themselves.
/// </summary>
public static class ForecastTuning
{
    /// <summary>Seasons from birth to adulthood (14 years).</summary>
    public const int ChildSeasons = 56;
    /// <summary>Births per adult per season, net of crowding (fitted).</summary>
    public static readonly Fix64 BirthsPerAdult = Fix64.FromRatio(60, 1000);
    /// <summary>Child deaths per child per season (fitted).</summary>
    public static readonly Fix64 ChildMortality = Fix64.FromRatio(8, 1000);
    /// <summary>Adult losses per adult per season (fitted).</summary>
    public static readonly Fix64 AdultMortality = Fix64.FromRatio(3, 1000);
    /// <summary>Seasons from 14 to 45 (31 years): adults are carried in a pipeline too and become elders at its end.</summary>
    public const int AdultSeasons = 124;
    /// <summary>Adults alive at the start are spread over their first 72 seasons of adulthood (14 to 32: the founders are 18 to 32).</summary>
    public const int YoungAdultSeasons = 72;
    /// <summary>Elder deaths per elder per season (fitted).</summary>
    public static readonly Fix64 ElderMortality = Fix64.FromRatio(2, 100);
    /// <summary>Unknown children's ages taper by this factor per season of age: a growing people is young (fitted, 0.98).</summary>
    public static readonly Fix64 ChildAgeTaper = Fix64.FromRatio(98, 100);
    /// <summary>A band splits about once its people pass 25 per settlement (live: two settlements by 35 people, three by 60 to 80).</summary>
    public const int PeoplePerSettlement = 25;
    public const int SettlementOffset = 5;
    /// <summary>A settlement keeps a Shaman once it has six adults (docs/MVP/02 §8.1).</summary>
    public const int AdultsPerShaman = 6;
    /// <summary>Research steps the forecast takes per season (the live system takes 15; 5 is three live steps each).</summary>
    public const int StepsPerSeason = 5;
    /// <summary>Steps per season in the planner's rollouts: coarser (75 ticks each) so hundreds fit a decision's budget.</summary>
    public const int PlannerStepsPerSeason = 2;
    /// <summary>
    /// Seasons of the planner's horizon per difficulty era (Settler 1 era = 6 minutes, Warlord 3 = 18, Legend 4 = 24; live E0
    /// to E2 run 6, 13 and 13 minutes). Harness duel, Warlord v Council, 16 seeds: 20 → 94%, 24 → 97%, 30 → 97% at a
    /// quarter more cost; 24 keeps four Warlord Sparks inside the 40-minute budget.
    /// </summary>
    public const int SeasonsPerEra = 24;
    /// <summary>The fudge that maps the model's Insight now onto the live Insight now is kept inside this band.</summary>
    public static readonly Fix64 FudgeMin = Fix64.FromRatio(7, 10);
    public static readonly Fix64 FudgeMax = Fix64.FromRatio(13, 10);
}
