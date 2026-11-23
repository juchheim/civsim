using System;

namespace CivSim.Sim.Research;

/// <summary>Which chapter a match plays (docs/MVP/25 §3). One mode governs the whole match; advancement stays per civilization.</summary>
public enum ChapterMode : byte
{
    /// <summary>The Bronze chapter: E0 to E3, ended by KEY-4. The default everywhere, and the only mode before WP5.</summary>
    Bronze = 0,
    /// <summary>The opt-in Iron chapter: E4 opens after a civilization's own KEY-4 and KEY-5 ends the match.</summary>
    Iron = 1,
    /// <summary>
    /// The Faith &amp; Fief chapter (docs/MVP/27): E4 and E5 both play, E5 opens after a civilization's own KEY-5, and KEY-6 ends the match.
    /// It contains the Iron chapter, so every Iron system runs in it. Appended after Iron; never renumber.
    /// </summary>
    Fief = 2,
    /// <summary>
    /// The opt-in Discovery chapter (docs/MVP/38): E4, E5 and E6 all play, E6 opens after a civilization's own KEY-6, and KEY-7 ends the match.
    /// It contains the Faith &amp; Fief chapter (and so the Iron chapter), so every E4 and E5 system runs in it. Appended after Fief; never renumber.
    /// </summary>
    Discovery = 3,
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

    /// <summary>
    /// The Faith &amp; Fief chapter: research ceiling 5, KEY-6 ends it, and **no rival countdown** (user decision D11, docs/MVP/27 §6, 2026-11-13). The
    /// chapter ends only when the human completes KEY-6; a match nobody finishes is open (<see cref="MatchEnd.Open"/>) and every headless loop bounds
    /// itself by its own horizon, as for an Iron match nobody finishes (docs/MVP/28 §17).
    /// </summary>
    public static ChapterRules Fief { get; } = new(ChapterMode.Fief, ResearchSystem.LastResearchEra + 2, null, rivalCountdown: false);

    /// <summary>
    /// The Discovery chapter: research ceiling 6, KEY-7 ends it, and **no rival countdown** (carried from Fief, docs/MVP/38 §6, decision D11). The
    /// chapter ends only when the human completes KEY-7; a match nobody finishes is open (<see cref="MatchEnd.Open"/>) and every headless loop
    /// bounds itself by its own horizon.
    /// </summary>
    public static ChapterRules Discovery { get; } = new(ChapterMode.Discovery, ResearchSystem.LastResearchEra + 3, null, rivalCountdown: false);

    private readonly int? _countdownSeasons;

    private ChapterRules(ChapterMode mode, int lastResearchEra, int? countdownSeasons, bool rivalCountdown = true)
    {
        Mode = mode;
        LastResearchEra = lastResearchEra;
        _countdownSeasons = countdownSeasons;
        HasRivalCountdown = rivalCountdown;
    }

    public ChapterMode Mode { get; }

    /// <summary>The last era whose projects can be researched. Also the era whose leaving keystone ends the match.</summary>
    public int LastResearchEra { get; }

    /// <summary>
    /// The last era whose projects the economy applies. Equal to the Bronze value in both modes until WP6/WP7 add the E4 recipes, health
    /// path and fission policy: lifting it earlier would let era 4 fall through the exact-era-3 demography and fission tables (docs/MVP/25 §2.2).
    /// </summary>
    public int EconomyMaxEra => ResearchSystem.LastResearchEra;

    /// <summary>
    /// True when the Iron systems run: the Iron chapter and the Faith &amp; Fief chapter, which contains it (E4 is played in both). This is
    /// **not** the mode identity; use <see cref="HasMode"/> for what is written to saves, replays, snapshots and hashes, and <see cref="IsFief"/> for E5.
    /// </summary>
    public bool IsIron => Mode is ChapterMode.Iron or ChapterMode.Fief or ChapterMode.Discovery;

    /// <summary>
    /// True when the Faith &amp; Fief systems run: the Faith &amp; Fief chapter and the Discovery chapter, which contains it (E5 is played in both).
    /// Like <see cref="IsIron"/> this is **not** the mode identity: a call site that was missed treats Discovery as Fief (systems on), the safe
    /// failure. Use <see cref="IsDiscovery"/> for E6 and <see cref="HasMode"/> for what is written to saves, replays, snapshots and hashes.
    /// </summary>
    public bool IsFief => Mode is ChapterMode.Fief or ChapterMode.Discovery;

    /// <summary>True in the Discovery chapter: E6 opens after a civilization's own KEY-6.</summary>
    public bool IsDiscovery => Mode == ChapterMode.Discovery;

    /// <summary>
    /// True for any chapter other than the legacy Bronze one. Such a match writes its mode byte into replays, saves, snapshots and the research
    /// checksum, so Bronze bytes and hashes stay exactly as they were and no mode converts silently into another.
    /// </summary>
    public bool HasMode => Mode != ChapterMode.Bronze;

    /// <summary>False when a rival completing the last keystone starts no clock (Faith &amp; Fief and Discovery); the chapter then ends only on the human's completion.</summary>
    public bool HasRivalCountdown { get; }

    public int CountdownSeasons(ResearchCatalog catalog) => _countdownSeasons ?? catalog.ChapterCountdownSeasons;

    public static ChapterRules For(ChapterMode mode) => mode switch
    {
        ChapterMode.Bronze => Bronze,
        ChapterMode.Iron => Iron,
        ChapterMode.Fief => Fief,
        ChapterMode.Discovery => Discovery,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };
}
