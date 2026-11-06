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
/// The per-match chapter rules (docs/MVP/25 §3): the research ceiling, the keystone that ends the match, and its cap and
/// countdown. <see cref="Bronze"/> reproduces the constants that were hard-coded before M11 (<see cref="ResearchSystem.LastResearchEra"/>
/// and the catalog's chapter constants), so a match built without rules is byte-identical to one built before this type existed.
/// <para>The Iron cap year and countdown are candidate values awaiting the oracle fit (docs/MVP/25 §3), not approved thresholds.</para>
/// </summary>
public sealed class ChapterRules
{
    /// <summary>Candidate Iron hard cap in years (docs/MVP/25 §3).</summary>
    public const int IronCapYear = 96;

    /// <summary>Candidate Iron countdown after the first rival's KEY-5, in seasons (docs/MVP/25 §3).</summary>
    public const int IronCountdownSeasons = 24;

    /// <summary>The Bronze chapter exactly as before M11: research ceiling 3, KEY-4 ends it, cap and countdown from the catalog.</summary>
    public static ChapterRules Bronze { get; } = new(ChapterMode.Bronze, ResearchSystem.LastResearchEra, null, null);

    /// <summary>The Iron chapter: research ceiling 4, KEY-5 ends it.</summary>
    public static ChapterRules Iron { get; } = new(ChapterMode.Iron, ResearchSystem.LastResearchEra + 1, IronCapYear, IronCountdownSeasons);

    /// <summary>Harness and diagnostics only: the Iron chapter with another cap year and countdown, to watch its systems run longer than the candidate chapter allows.</summary>
    public static ChapterRules IronWithLimits(int capYear, int countdownSeasons)
        => new(ChapterMode.Iron, ResearchSystem.LastResearchEra + 1, capYear, countdownSeasons);

    private readonly int? _capYear;
    private readonly int? _countdownSeasons;

    private ChapterRules(ChapterMode mode, int lastResearchEra, int? capYear, int? countdownSeasons)
    {
        Mode = mode;
        LastResearchEra = lastResearchEra;
        _capYear = capYear;
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

    public int CapYear(ResearchCatalog catalog) => _capYear ?? catalog.ChapterCapYear;

    public int CountdownSeasons(ResearchCatalog catalog) => _countdownSeasons ?? catalog.ChapterCountdownSeasons;

    public static ChapterRules For(ChapterMode mode) => mode switch
    {
        ChapterMode.Bronze => Bronze,
        ChapterMode.Iron => Iron,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };
}
