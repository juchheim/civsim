# F-T8 measurement implementation and calibration note — 2026-11-16

**Status:** An opt-in Fief territory trace and deterministic metric fixtures are added to the working tree after isolated development. They have not yet been built or run. No F-T8 cohort was launched. This is instrumentation and a provisional calibration note only; F-T8 and all E5 numeric rows remain unpromoted.

## Governing wording and limits

MVP 27 §7 names F-T8 as “Map fill, Dominion and City share” and says it is diagnostic until re-anchored on the live E4 trajectory. §9 identifies WP-E5-11 promotion and WP-E5-12 whole-match validation; §10 has no F-T8 pass band or acceptance criterion. The prior I-T8 oracle likewise explicitly supplies no guessed E4 population, map-fill, Dominion, or City band (`m11-wp2-oracle-fit.md`). The matched-anchor protocol says to report within-pair City-share percentage-point changes, samples, and distributions by fixture, and not classify a pass/failure until the owner approves a numeric band (`m11-it4-matched-anchor-protocol.md`).

The E4 live-territory reference in `07b-territory-findings.md` reports 90% of habitable land claimed at 95–99.5 minutes on four seeds, at the start of E7, with seasonal camps. This is the only existing live calibration range for map fill; it is not F-T8 acceptance evidence. The already adopted Dominion rule remains direct ownership of at least 50% of all habitable weight and at least twice the runner-up, held for five consecutive years (MVP 05 §11.3; 07b T2/R7). Legacy accrual remains 100 points per year distributed by share of claimed land (07b R8; `Territory.AccrueDominion`).

## Measurement added

`fief-match --fief-territory-trace` emits four deterministic per-civilization rows for the initial season-0 state and after each completed season. It reports:

- Civilization population, population in City-tier settlements, and City share (`City population / civilization population`; `na` when population is zero).
- World map fill as both owned tiles / total map tiles and owned habitable weight / total habitable weight (the latter is the claimed-land measure used by the territory model).
- Each civilization's directly owned habitable share and current Dominion points.
- At completed annual checkpoints only (seasons 4, 8, …), expected gross Dominion accrual from current owned weight and observed net Dominion change after tribute transfers. Season 0 and non-annual rows use `na` for both annual flow fields. `dominionExpectedGrossAnnual` is a point-in-time annualized run-rate from checkpoint ownership; `dominionObservedNetAnnual` is the realized checkpoint-to-checkpoint change, including tribute transfers.
- The current direct-land-share Dominion-rule candidate, its leader and runner-up shares, `dominionRuleConsecutiveElapsedSeasons` (the elapsed seasonal intervals for which the candidate has qualified), and whether the diagnostic persistence predicate has remained true for 20 elapsed seasons. A tied or all-zero land leader is reported as `none` rather than assigning a civilization arbitrarily.

The trace is observational and opt-in. It neither changes state nor ends a match. The codebase has no Dominion match-ending implementation; `dominionRuleSatisfied` reports a report-only persistence diagnostic, not a game victory. The initial season-0 sample establishes a qualifying candidate with zero elapsed seasons. Twenty completed transitions thereafter require samples through season 20: 21 state samples span 20 elapsed seasons. If a candidate first qualifies at season 1, the 20-interval screen cannot pass before season 21. A failed sample or changed leader resets the elapsed-season counter.

Run shape after a candidate is frozen and the host is idle:

```sh
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll fief-match \
  --pack /absolute/path/to/frozen-natural-fief.pack \
  --seeds-file /absolute/path/to/frozen-ft8-seeds.txt \
  --minutes 130 --fief-territory-trace \
  > /absolute/path/to/new-ft8-run.txt 2>&1
```

The CLI does not run a new cohort as part of this instrumentation change. Preserve the exact source, pack and both adjacent Release DLL hashes with any later run. Do not reuse the D20 data as an F-T8 matched cohort. The shared no-overwrite preflight runner supports this trace with `--trace-kind territory`; it validates the canonical source manifest plus pack, adjacent Harness/Sim hashes, freezes the unique seed-list hash and seeds before launch, and refuses an existing output root. Example, only after the host is idle and inputs are frozen:

```sh
python3 tools/fief_trace_runner.py --trace-kind territory \
  --provenance /absolute/frozen/provenance.json \
  --source-root /absolute/frozen/source \
  --pack /absolute/frozen/CivSim.pack \
  --harness /absolute/frozen/bin/CivSim.Harness.dll \
  --sim /absolute/frozen/bin/CivSim.Sim.dll \
  --seeds /absolute/frozen/ft8-seeds.txt \
  --output /absolute/new/ft8-output --minutes 130
```

The provenance JSON must bind `source_tree_sha256`, the canonical `source_manifest` path and its SHA-256, plus `pack_sha256`, `harness_sha256`, and `sim_sha256`. The source manifest is the exact JSON from `hash_e5_workspace.py`; the runner verifies every listed file and its canonical aggregate before launch and rechecks inputs around each match. It creates each per-seed output and the run freeze with exclusive creation, never overwriting prior seeds. Append-only per-seed and run status journals record exit codes and terminal `completed`, `failed`, or `interrupted` states.

## Provisional calibration screen (not an acceptance band)

For an autonomous first calibration, use the existing four-player E4 live reference rather than inventing a new target: report the season/minute when habitable claimed land first reaches 90%, and compare the cohort distribution with the historical 95–99.5 minute E4 reference. A result outside that window is a re-anchoring/review signal only, not a F-T8 pass or fail. Report City-share change as matched percentage points and Dominion annual flow/victory-rule observations descriptively; there is no numeric City-share or Dominion-victory frequency threshold in §7, §9, or §10. Keep this provisional screen out of the acceptance status until an owner sets the gate.

## Deterministic fixture coverage and remaining work

`FiefTerritoryTraceTests` covers percent/City-share arithmetic, claimed-weight Dominion accrual, the exact 50%-and-2× rule, and elapsed-season persistence/reset behavior. The isolated follow-up patch adds an `Append` integration test that samples season 0 through season 4, verifies all four civilizations and annual-field labeling, and compares traced/untraced match checksums after every sample. It also changes the persistence counter to elapsed seasonal intervals and prevents season 0 from emitting a false annual net delta. The patch is not compiled or executed because active AI cohorts and test processes are consuming the host. A Release build and focused test run remain due when those workloads are idle. Release binaries were left untouched.

F-T8 still needs a dedicated unscreened cohort on the frozen current candidate, a live E4 reference or matched baseline with comparable seeds/starts/horizons, analysis by geography and horizon, censors, and review of whether observed City share/map fill/Dominion trajectories meet the design intent. A Dominion predicate trace cannot close a missing in-engine victory ending. No threshold was promoted and no simulation cohort was started.
