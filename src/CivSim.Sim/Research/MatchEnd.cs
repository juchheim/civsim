using System;

namespace CivSim.Sim.Research;

/// <summary>Which clause of the Bronze chapter rule ends the match (docs/MVP/22 §3.1).</summary>
public enum MatchEndReason : byte
{
    /// <summary>The human completed KEY-4: "The Iron Age dawns".</summary>
    HumanKeystone = 1,
    /// <summary>12 seasons passed since the first rival completed KEY-4.</summary>
    RivalCountdown = 2,
    /// <summary>The hard cap, Year 64.</summary>
    YearCap = 3,
}

/// <summary>
/// When the Bronze chapter ends (docs/MVP/22 §3.1, B-D1): at the end of the season in which the first of these happens
/// (1) the human completes KEY-4, (2) 12 seasons after the first rival completes KEY-4, (3) Year 64. A headless or AI-only
/// match has no human seat and uses clauses 2 and 3. A pure function of the research state, whose KEY-4 completion
/// ticks are checksummed, so it is the same in every replay and every restore.
/// </summary>
/// <param name="Reason">The clause that closes the chapter as things stand; may be replaced by an earlier one later.</param>
/// <param name="EndSeason">The season (tick / 150) whose last tick closes the match.</param>
/// <param name="Civ">The civilization whose KEY-4 triggers the clause, or -1 for the Year cap.</param>
/// <param name="CountdownStartSeason">The season in which the first rival completed KEY-4, or -1 while none has.</param>
/// <param name="CountdownCiv">That rival, or -1.</param>
public readonly record struct MatchEnd(MatchEndReason Reason, int EndSeason, int Civ, int CountdownStartSeason, int CountdownCiv)
{
    /// <summary>Seasons per year (a year is 600 ticks).</summary>
    public const int SeasonsPerYear = 4;

    /// <summary>The tick count at which the match is over: the end of <see cref="EndSeason"/>.</summary>
    public long EndTick => (EndSeason + 1L) * ResearchSystem.SeasonTicks;

    /// <summary>True once <paramref name="tick"/> ticks have been simulated.</summary>
    public bool IsOver(long tick) => tick >= EndTick;

    /// <summary>True from the season a rival completes KEY-4, while no earlier clause has already closed the chapter.</summary>
    public bool CountdownRunning => CountdownStartSeason >= 0 && Reason == MatchEndReason.RivalCountdown;

    /// <summary>
    /// Season ends still to come after the one in progress: 12 in the season a rival completes KEY-4 ("3 years left"), 0 in the
    /// final season and once the match is over.
    /// </summary>
    public int SeasonsLeft(long tick)
        => IsOver(tick) ? 0 : Math.Max(0, EndSeason - (int)(tick / ResearchSystem.SeasonTicks));

    /// <summary>
    /// Evaluates the rule. <paramref name="humanSeat"/> is the human's civilization, or -1 for a headless or AI-only
    /// match (clause 1 off). A tie on the end season goes to the human clause, then the countdown, then the cap.
    /// </summary>
    public static MatchEnd Evaluate(ResearchSystem research, int humanSeat = -1)
    {
        ArgumentNullException.ThrowIfNull(research);
        ResearchCatalog catalog = research.Catalog;
        int keystone = catalog.KeystoneLeaving(ResearchSystem.LastResearchEra);
        int cap = catalog.ChapterCapYear * SeasonsPerYear - 1;
        var end = new MatchEnd(MatchEndReason.YearCap, cap, -1, -1, -1);
        if (keystone < 0) return end;

        int humanSeason = -1, rivalSeason = -1, rival = -1;
        long rivalTick = long.MaxValue;
        for (int c = 0; c < research.CivCount; c++)
        {
            if (!research.IsDone(c, keystone)) continue;
            long tick = research.CompletedTick(c, keystone);
            int season = (int)(tick / ResearchSystem.SeasonTicks);
            if (c == humanSeat) humanSeason = season;
            else if (tick < rivalTick) { rivalTick = tick; rivalSeason = season; rival = c; }
        }
        if (rival >= 0)
        {
            int countdownEnd = rivalSeason + catalog.ChapterCountdownSeasons;
            end = end with { CountdownStartSeason = rivalSeason, CountdownCiv = rival };
            if (countdownEnd <= end.EndSeason) end = end with { Reason = MatchEndReason.RivalCountdown, EndSeason = countdownEnd, Civ = rival };
        }
        if (humanSeason >= 0 && humanSeason <= end.EndSeason)
            end = end with { Reason = MatchEndReason.HumanKeystone, EndSeason = humanSeason, Civ = humanSeat };
        return end;
    }
}
