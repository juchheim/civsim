using System;

namespace CivSim.Sim.Research;

/// <summary>Which chapter a match plays (docs/MVP/25 §3). One mode governs the whole match; advancement stays per civilization.</summary>
public enum ChapterMode : byte
{
    /// <summary>The Bronze chapter: E0 to E3, ended by KEY-4. The default everywhere, and the only mode before WP5.</summary>
    Bronze = 0,
    /// <summary>The opt-in Iron chapter: E4 opens after a civilization's own KEY-4 and KEY-5 ends the match.</summary>
    Iron = 1,
}

/// <summary>
/// The per-match chapter rules (docs/MVP/25 §3): the research ceiling, the keystone that ends the match, and its
/// countdown. There is no year cap (removed 2026-11-08, docs/MVP/28 §17).
/// <see cref="Bronze"/> reproduces the constants that were hard-coded before M11 (<see cref="ResearchSystem.LastResearchEra"/>
/// and the catalog's chapter constants), so a match built without rules is byte-identical to one built before this type existed.
/// <para>The Iron countdown is a candidate value awaiting the oracle fit (docs/MVP/25 §3), not an approved threshold.</para>
/// </summary>
public sealed class ChapterRules
{
    /// <summary>
    /// Iron countdown after the first rival's KEY-5, in seasons: 12 years. Was 24 (docs/MVP/25 §3). Most Iron buildings unlock in the last
    /// minutes of research, and at 24 the match ended before a town could build them. 72 gave the time but let almost every type unlock, so
    /// it is 48 together with a doubled cost for the late Iron projects (docs/MVP/28 §6 and §7).
    /// </summary>
    public const int IronCountdownSeasons = 48;

    /// <summary>The Bronze chapter exactly as before M11: research ceiling 3, KEY-4 ends it, countdown from the catalog.</summary>
    public static ChapterRules Bronze { get; } = new(ChapterMode.Bronze, ResearchSystem.LastResearchEra, null);

    /// <summary>The Iron chapter: research ceiling 4, KEY-5 ends it.</summary>
    public static ChapterRules Iron { get; } = new(ChapterMode.Iron, ResearchSystem.LastResearchEra + 1, IronCountdownSeasons);

    /// <summary>Harness and diagnostics only: the Iron chapter with another countdown, to watch its systems run longer (or shorter) than the candidate chapter allows.</summary>
    public static ChapterRules IronWithCountdown(int countdownSeasons)
        => new(ChapterMode.Iron, ResearchSystem.LastResearchEra + 1, countdownSeasons);

    private readonly int? _countdownSeasons;

    private ChapterRules(ChapterMode mode, int lastResearchEra, int? countdownSeasons)
    {
        Mode = mode;
        LastResearchEra = lastResearchEra;
        _countdownSeasons = countdownSeasons;
    }

    public ChapterMode Mode { get; }

    /// <summary>The last era whose projects can be researched. Also the era whose leaving keystone ends the match.</summary>
    public int LastResearchEra { get; }

    /// <summary>
    /// The last era whose projects the economy applies. Equal to the Bronze value in both modes until WP6/WP7 add the E4 recipes, health
    /// path and fission policy: lifting it earlier would let era 4 fall through the exact-era-3 demography and fission tables (docs/MVP/25 §2.2).
    /// </summary>
    public int EconomyMaxEra => ResearchSystem.LastResearchEra;

    public bool IsIron => Mode == ChapterMode.Iron;

    public int CountdownSeasons(ResearchCatalog catalog) => _countdownSeasons ?? catalog.ChapterCountdownSeasons;

    public static ChapterRules For(ChapterMode mode) => mode switch
    {
        ChapterMode.Bronze => Bronze,
        ChapterMode.Iron => Iron,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };
}
