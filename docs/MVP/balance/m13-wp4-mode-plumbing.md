# M13 WP-E6-4: Discovery chapter-mode plumbing (2026-11-20)

**Status:** built and tested on `codex/e6-discovery` (worktree `civsim-e6`, cut from `main` at `deba515`), uncommitted; plumbing only. No E6 system exists yet, no E6 row is numeric, and Bronze, Iron and Fief behavior, bytes, hashes and end rules are unchanged. Design: [38 §6](../38-discovery-age.md), following the Fief pattern of [m12-wp6-mode-plumbing](m12-wp6-mode-plumbing.md).

## What the mode is

`ChapterMode.Discovery = 3`, appended after Fief (never renumber). One mode governs a whole match; advancement stays per civilization.

| | Iron | Fief | **Discovery** |
|---|---|---|---|
| Research ceiling | E4 | E5 | **E6** |
| Ends on | the human's KEY-5, or 48 seasons after the first rival's | the human's KEY-6 only | **the human's KEY-7 only; a rival's KEY-7 starts no clock** (carried decision D11) |
| Iron systems run | yes | yes | **yes** |
| Fief systems run | no | yes | **yes (Discovery contains Fief)** |
| Economy ceiling | 3, records E4 completions | 3, records E5 | 3, records **E6** |
| Mode byte in replays, snapshots, research hash | 1 | 2 | **3** |

## What changed

| File | Change |
|---|---|
| `Research/ChapterRules.cs` | `Discovery` mode and rules; `IsIron` and `IsFief` now mean "those systems run" (so Discovery counts as both); new `IsDiscovery` |
| `Research/MatchModel.cs` | economy gets `DiscoveryChain` from the rules |
| `Research/MatchReplay.cs` | the existing mode format (version 2) accepts mode byte 3; any other unknown byte is still refused |
| `Economy/LivingEconomyModel.Discovery.cs` (new) | the `DiscoveryChain` flag; setting it also turns `FiefChain` (and so `IronChain`) on |
| `Economy/LivingEconomyModel.Iron.cs`, `LivingEconomyModel.cs` | `ProjectCeiling` gains the Discovery step; the constructor, the snapshot clone and the content fingerprint know the mode |
| `Ai/Forecast/ResearchForecast.cs` | `Key7` forecast field (always -1 outside a Discovery ceiling) |
| `Harness/ResearchCommand.cs`, `AiCommand.cs` | `--mode discovery` |
| `Tests/DiscoveryChapterTests.cs` (new), `Tests/FiefChapterTests.cs` | 8 new tests; the Fief "unknown replay mode" test now uses byte 4 because 3 is valid |

Design choices worth knowing:

1. **No new replay or save format numbers.** The mode-bearing format already carries a mode byte, so a new value is enough (the Fief choice again).
2. **`IsFief` was widened rather than every call site edited.** A call site that was missed then treats Discovery as Fief (E5 systems on), the safe failure, exactly as `IsIron` was widened for Fief. `ResearchSystem`'s E5 effects, the replay's Fief control block and the economy's Fief chain all keep working in Discovery.
3. **The content fingerprint treats E6 rows as one prose placeholder outside Discovery**, as it already does for E4 and E5 outside their chapters. Today every E6 row has exactly one prose effect, so this is byte-neutral; it is there so that typing the 41 E6 rows (WP-E6-2) cannot move a Bronze, Iron or Fief checksum. The registry era ceiling for goods and actions is 6 in Discovery.

## Tests (all pass)

Rules and ceilings; KEY-7 is the end keystone and an unfinished Discovery has no end; **a rival's KEY-7 starts no clock and the human's ends the chapter**, while KEY-6 ends Fief but not Discovery; E6 stays Hidden in Bronze, Iron and Fief (grants refused) and opens in Discovery only after the civilization's own KEY-6, never for another civilization, with E7 still Hidden; the forecast catalog bounds E6 by the ceiling; the four modes hash differently and Bronze adds nothing; a Discovery economy payload equals a Fief one so far while the match checksum differs; snapshots refuse every other mode's save in both directions; a Discovery replay round-trips, and the same run claimed as Fief fails to verify.

## Checks (this tree against a clean checkout of `main` at `deba515`)

- `civsim validate --strict`: 0 errors, 0 warnings. Toy golden passes.
- Full Release suite: **1,537 of 1,542** pass (1,533 before this change plus the 9 new tests). The five failures are the same five that fail on a clean `main`: `HideYieldTests` (3), `ViewDeterminismTests.The_politics_golden_still_passes_with_frames_built_every_10_ticks` and `MetalEconomyTests.Deposits_hold_litres_by_kind_tier_and_depth`. None is touched by this change.
- **The checked-in `research-v1` and `politics-v1` goldens do not match on a clean `main` either.** Both runs print identical `final` and `digest` values on `main` and on this tree (research 12 min grower: `final=208923cede594c56`; politics 24 min: `final=94fcdd6cfaff687a`), so the Discovery plumbing moves no simulation output. The golden files are stale relative to the Fief balance work already on `main`; refreshing them belongs to the E5 balancer, not to this branch.
- A plain `research --mode discovery` match runs and reports "chapter: Discovery (ceiling E6, no end until a keystone clause fires)".

## Audited and deliberately left alone

- **Raw research-era lookups** (reach, claim radius, fission and camp rules, mortality rows) are indexed by a civilization's raw research era. In Discovery a civilization at E6 reads the E6 row (reach 35, claim radius 5, mortality 0.025 / 0.0095 / 0.049) while the economy era clamp stays 3. This is the same intended behavior as Fief at E5; any E6 fit changes the registry row, not the code.
- **The economy snapshot version is unchanged** because no E6 state exists. Each E6 system will add its own state section, written only when `DiscoveryChain` is on.
- **`ResearchSpark` and Strategist `<= 3` frontier priors**: unchanged, as for Iron and Fief.

## Not done (the plumbing ends here)

1. **The game does not offer or label Discovery.** `GameSaveMode`, `SimulationSession` and the HUD know only Bronze, Iron and Fief; that is WP-E6-11 and touches game scripts other sessions are editing.
2. **No `discovery-match` harness command or `--grant-e6`** (WP-E6-12); `research --mode discovery` is the plain match only.
3. **`Key7` is not yet read by the Spark, Council or Strategist** (WP-E6-10).
4. **No E6 system, AI behavior or acceptance gate.** Next in order: WP-E6-2 (content typing) and WP-E6-3 (geography audit, coal visibility first), which can run in parallel.
