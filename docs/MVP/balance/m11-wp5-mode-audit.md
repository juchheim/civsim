# M11 WP5 — Chapter-mode plumbing audit

Records what the Iron rules-mode work changed, what it audited and left alone, and what remains. Written against `codex/e4-iron` after WP5 slices 1–3. See [25 §2.2 and §3](../25-iron-age.md).

## What carries the mode

| Path | How | Bronze behavior |
|---|---|---|
| Research ceiling | `ResearchSystem.Rules.LastResearchEra` (`ComputeState`, Council chase, `CompleteProject` grants) | Ceiling 3, as before |
| Match end | `MatchEnd.Evaluate` reads the end keystone, cap year and countdown from the rules | KEY-4, Year 64, 12 seasons from the catalog |
| Economy ceiling | `LivingEconomyModel.MaxEra` (init), set from `rules.EconomyMaxEra` | 3; also 3 in Iron until WP6/WP7 (see below) |
| Forecast and Spark | `ForecastCatalog(catalog, ceilingEra)`; `ResearchSpark` passes the ceiling from `AiResearchPort.Rules`; `ForecastResult.Key5` | Ceiling 3; `Key5` is always -1 |
| State hash | Iron adds a mode term to the research hash | Nothing added |
| Match snapshot | Iron appends a trailer; each mode refuses the other's snapshot | Bytes unchanged |
| Match replay | Iron writes format 2 with a mode byte; verification uses the recorded mode | Format 1, bytes unchanged |
| Game save | Iron writes version 5 with a mode byte; `SimulationSession` builds the match from it | Version 4, bytes unchanged |
| Harness | `research --mode iron` (plain run only; goldens and M10 reports stay Bronze) | Default |
| Game text | End-screen title and rival-countdown toast say KEY-5 / Feudal Order when `SimulationSession.IsIron` | Unchanged |

## Audited and deliberately left unchanged

- **Strategist and belief E3-plus predicates** (`rival.Era >= 3`, `BronzeDefensePolicy`, `Strategist.SecureTin`, `OwnView` Bronze activation). They mean "has Bronze rules", which stays true in Iron.
- **`v.Own.Era <= 3` frontier priors** (`Strategist.cs:315`, `Strategist.Assess.cs:272`). These are tuning priors that drop after KEY-4 in Bronze. In Iron, era 4 is the main chapter, so the right value is a fit decision. Left for WP9 with fog tests; no data supports a value yet.
- **`ResearchSpark` uses of `ResearchSystem.LastResearchEra`** (Chieftain Bronze-closure protection). They test "this civilization is in era 3", not the ceiling, and remain correct in Iron.
- **Economy ceiling in Iron stays 3.** Raising it sends era 4 through the exact `era == 3` demography tables and the fission/camp policy (25 §2.2). WP6/WP7 raise it with their own health and fission paths.

## Not covered

- The game project does not currently compile on this branch because of `Game.Diplomacy.cs:465` (duplicate local `chosenDestination`), which predates this work. The WP5 game edits add no new compiler errors, but they have not been run, and `GameSave` has no automated test.
- Validator rule V21 keeps E4 rows `draft`. The research, forecast, grant, replay and save paths are mode-aware, but the view (research web layout) and economy effects are not, so promotion stays blocked.
- KEY-5 scoring: KEY-5 is declared as a normal keystone with the generic `legacy: Keystone` effect. No Iron-specific score was added; the oracle fit decides whether one is needed.
- The Year-96 cap and 12-season countdown are candidate values (25 §3), not approved thresholds.
