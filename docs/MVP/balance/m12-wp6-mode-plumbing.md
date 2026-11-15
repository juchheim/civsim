# M12 WP-E5-4: Faith & Fief chapter-mode plumbing (2026-11-13)

**Status:** built and tested on `codex/e5-fief`; plumbing only. No E5 system exists yet, no E5 row is numeric, and Bronze and Iron behavior, bytes, hashes, goldens and end rules are unchanged. Design: [27 §6](../27-faith-and-fief-age.md), following the Iron pattern of [m11-wp5-mode-audit](m11-wp5-mode-audit.md).

## What the mode is

`ChapterMode.Fief = 2`, appended after Iron (never renumber). One mode governs a whole match; advancement stays per civilization.

| | Bronze | Iron | **Fief** |
|---|---|---|---|
| Research ceiling | E3 | E4 | **E5** |
| Ends on | the human's KEY-4, or the countdown after the first rival's | the human's KEY-5, or 48 seasons after the first rival's | **the human's KEY-6 only; a rival's KEY-6 starts no clock** (user decision D11) |
| Iron systems run | no | yes | **yes (Fief contains Iron)** |
| Economy ceiling | 3 | 3, records E4 completions | 3, records **E5** completions |
| Mode byte in replays, snapshots, research hash | none (legacy bytes) | 1 | **2** |

A Fief match nobody finishes is open (`MatchEnd.Open`), so every headless loop bounds itself by its own `--minutes` horizon, exactly as an Iron match nobody finishes does ([28 §17](../28-production-gap.md)).

## What changed

| File | Change |
|---|---|
| `Research/ChapterRules.cs` | `Fief` mode and rules; `IsIron` now means "Iron systems run" (Iron and Fief); new `IsFief`, `HasMode` (anything but Bronze) and `HasRivalCountdown` |
| `Research/MatchEnd.cs` | the rival-countdown clause runs only when `HasRivalCountdown` |
| `Research/ResearchSystem.cs` | the mode byte joins the research hash when `HasMode`, so Iron (1) and Fief (2) hash differently and Bronze adds nothing |
| `Research/MatchReplay.cs` | the existing mode format (version 2) carries the Fief byte; an unknown mode byte is refused |
| `Research/MatchModel.cs` | economy gets `FiefChain`; the snapshot trailer is written and checked for any mode, so no mode reads another's save |
| `Economy/LivingEconomyModel.Fief.cs` (new) | the `FiefChain` flag; setting it also turns `IronChain` on |
| `Economy/LivingEconomyModel.Iron.cs`, `LivingEconomyModel.cs` | `ProjectCeiling` gains the Fief step; the economy clone copies the flag |
| `Ai/Forecast/ResearchForecast.cs` | `Key6` forecast field (always -1 outside a Fief ceiling) |
| `Harness/ResearchCommand.cs`, `AiCommand.cs` | `--mode fief`; the chapter line names the mode |
| `Tests/FiefChapterTests.cs` (new) | 9 tests, below |

Two design choices worth knowing. First, **no new replay or save format numbers**: the plan said Fief would append the next versions, but the mode-bearing format (replay version 2, snapshot trailer version 1, save version 6) already carries a mode byte, so a new mode value is the cleaner, smaller change. Second, `IsIron` was widened rather than every call site edited: a call site that was missed then treats Fief as Iron (systems on), the safe failure, while the places that mean "which mode" (hash, replay, snapshot) were changed to `HasMode` by hand.

## Tests (all pass)

Fief rules and ceilings; the end keystone is KEY-6 and an unfinished Fief has no end; **a rival's KEY-6 starts no clock and the human's ends the chapter** (and a headless match has no end); E5 projects stay Hidden in Bronze and Iron (grants refused) and open in Fief only after the civilization's own KEY-5, never for another civilization, with E6 still Hidden; the forecast catalog bounds E5 by the ceiling and reports KEY-6 only inside Fief; the three modes hash differently and Bronze adds nothing; **a Fief economy's bytes equal an Iron economy's** after three simulated seasons (nothing of its own yet); snapshots carry the mode and no mode accepts another's save; a Fief replay records mode byte 2, round-trips, does not verify as Iron, and a bad mode byte is refused. The E5-visibility tests run on a pack where KEY-6 and AGR-11 are numeric (compiled without validation, as the E4 mode-bound test does), because V22 forbids a half-promoted E5.

## Checks

- `civsim validate --strict` clean; toy, research (12 min, grower) and politics (24 min) determinism checks pass, so no simulation golden moved.
- Full Release suite: **1,194 of 1,194** pass.
- A plain `research --mode fief` match (seed 20261019, 40 minutes) runs and reports "chapter: Fief (ceiling E5, no end until a keystone clause fires)".

## Audited and deliberately left alone

- **Raw research-era lookups.** The registry rows for reach, claim radius and the fission and camp rules are read by a civilization's raw research era (`LivingEconomySource`, `PoliticsSystem`, `Strategist`). In a Fief match a civilization at E5 reads the E5 row (reach 25, claim radius 5), and the `era == 3` and `era > 3` fission and camp rules fall through exactly as they do for Iron at E4. The arrays are sized for all ten eras, so nothing truncates. This is intended; any E5 fit changes the registry row, not code.
- **The economy era clamp stays 3** in all three modes (so E4 and E5 use the E3 demography tables). The per-chapter economy profile (decision D3) is not needed until an E5 system reads era-specific policy.
- **`ResearchSpark` uses of `ResearchSystem.LastResearchEra`** test "this civilization is in era 3" (Bronze-closure protection), not the ceiling, and stay correct.
- **Strategist `<= 3` frontier priors** and the Bronze defense predicates: unchanged, as for Iron.

## Not done (the plumbing ends here)

1. **The game does not offer or label Fief.** `GameSave` still rejects any saved mode but Iron and Bronze, `SimulationSession.IsIron` text says "Feudal Order" for any Iron-systems match, and there is no mode choice. That is WP-E5-10 work, and it touches the game scripts other sessions are editing.
2. **No `fief-match` harness command** yet (WP-E5-11); `research --mode fief` is the plain match only.
3. **Fief needs its own full-content fingerprint** before any E5 effect runs, as Iron's comment already says; the legacy fingerprint still represents E4 and E5 drafts as one prose placeholder.
4. **No E5 system, AI behavior or acceptance gate** (B5-04 across all three end clauses runs with the systems, later).
