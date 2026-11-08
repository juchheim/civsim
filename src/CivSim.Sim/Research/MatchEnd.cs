using System;

namespace CivSim.Sim.Research;

/// <summary>Which clause of the chapter rule ends the match (docs/MVP/22 §3.1; docs/MVP/25 §3 for the Iron chapter, where the keystone is KEY-5).</summary>
public enum MatchEndReason : byte
{
    /// <summary>
    /// No clause has fired: the chapter is still open and has no end season (<see cref="MatchEnd.NoEndSeason"/>). Since the year cap was
    /// removed (docs/MVP/28 §17) this is the state of every match until a keystone is completed. Never stored in saves or replays.
    /// </summary>
    None = 0,
    /// <summary>The human completed KEY-4: "The Iron Age dawns".</summary>
    HumanKeystone = 1,
    /// <summary>12 seasons passed since the first rival completed KEY-4.</summary>
    RivalCountdown = 2,
    /// <summary>
    /// OBSOLETE, never produced: the hard year cap was removed on 2026-11-08 (docs/MVP/28 §17). The member stays so the value 3 is
    /// not reused or renumbered; it may still appear in older saved data or recorded harness output.
    /// </summary>
    YearCap = 3,
}

/// <summary>
/// When the Bronze chapter ends (docs/MVP/22 §3.1, B-D1): at the end of the season in which the first of these happens
/// (1) the human completes the chapter's last keystone, (2) the chapter countdown after the first rival completes it. There is no
/// year cap (removed 2026-11-08, docs/MVP/28 §17): until a keystone is completed the match has no end, which is
/// <see cref="MatchEndReason.None"/> with <see cref="NoEndSeason"/>. A headless or AI-only match has no human seat and uses clause 2
/// only, so every headless loop must bound itself by its own horizon. A pure function of the research state, whose keystone
/// completion ticks are checksummed, so it is the same in every replay and every restore.
/// </summary>
/// <param name="Reason">The clause that closes the chapter as things stand, or <see cref="MatchEndReason.None"/> while the chapter is open; may be replaced by an earlier one later.</param>
/// <param name="EndSeason">The season (tick / 150) whose last tick closes the match, or <see cref="NoEndSeason"/> while the chapter is open.</param>
/// <param name="Civ">The civilization whose keystone triggers the clause, or -1 while the chapter is open.</param>
/// <param name="CountdownStartSeason">The season in which the first rival completed KEY-4, or -1 while none has.</param>
/// <param name="CountdownCiv">That rival, or -1.</param>
public readonly record struct MatchEnd(MatchEndReason Reason, int EndSeason, int Civ, int CountdownStartSeason, int CountdownCiv)
{
    /// <summary>Seasons per year (a year is 600 ticks).</summary>
    public const int SeasonsPerYear = 4;

    /// <summary><see cref="EndSeason"/> while no clause has fired: the chapter is open and never ends on its own.</summary>
    public const int NoEndSeason = int.MaxValue;

    /// <summary>The state of a chapter in which no keystone has been completed: no end season, never over.</summary>
    public static MatchEnd Open { get; } = new(MatchEndReason.None, NoEndSeason, -1, -1, -1);

    /// <summary>True once a clause has fired and the match has an end season.</summary>
    public bool HasEnd => EndSeason != NoEndSeason;

    /// <summary>
    /// The tick count at which the match is over: the end of <see cref="EndSeason"/>, or <see cref="long.MaxValue"/> while the chapter is
    /// open (so <c>tick &gt;= EndTick</c> is never true and nothing overflows).
    /// </summary>
    public long EndTick => HasEnd ? (EndSeason + 1L) * ResearchSystem.SeasonTicks : long.MaxValue;

    /// <summary>True once <paramref name="tick"/> ticks have been simulated; always false while the chapter is open.</summary>
    public bool IsOver(long tick) => HasEnd && tick >= EndTick;

    /// <summary>True from the season a rival completes KEY-4, while no earlier clause has already closed the chapter.</summary>
    public bool CountdownRunning => CountdownStartSeason >= 0 && Reason == MatchEndReason.RivalCountdown;

    /// <summary>
    /// Season ends still to come after the one in progress: 12 in the season a rival completes KEY-4 ("3 years left"), 0 in the
    /// final season and once the match is over, and -1 while the chapter is open (there is no end to count down to).
    /// </summary>
    public int SeasonsLeft(long tick)
        => !HasEnd ? -1 : IsOver(tick) ? 0 : Math.Max(0, EndSeason - (int)(tick / ResearchSystem.SeasonTicks));

    /// <summary>
    /// Evaluates the rule. <paramref name="humanSeat"/> is the human's civilization, or -1 for a headless or AI-only
    /// match (clause 1 off). A tie on the end season goes to the human clause, then the countdown.
    /// </summary>
    public static MatchEnd Evaluate(ResearchSystem research, int humanSeat = -1)
    {
        ArgumentNullException.ThrowIfNull(research);
        ResearchCatalog catalog = research.Catalog;
        ChapterRules rules = research.Rules;
        int keystone = catalog.KeystoneLeaving(rules.LastResearchEra);
        MatchEnd end = Open;
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
            int countdownEnd = rivalSeason + rules.CountdownSeasons(catalog);
            end = end with { CountdownStartSeason = rivalSeason, CountdownCiv = rival };
            end = end with { Reason = MatchEndReason.RivalCountdown, EndSeason = countdownEnd, Civ = rival };
        }
        if (humanSeason >= 0 && humanSeason <= end.EndSeason)
            end = end with { Reason = MatchEndReason.HumanKeystone, EndSeason = humanSeason, Civ = humanSeat };
        return end;
    }
}
