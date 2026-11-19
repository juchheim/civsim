# WP-E5-8 current-source readiness — 2026-11-16

**Status:** Implementation and trace tooling are present, but neither F-T5 nor SEA-10 has a qualifying current-source whole-match result. Do not launch a cohort from this note. At preparation time the checkout was on `codex/e5-fief` at `ceaadef`; WP-E5-7/source edits and the final candidate freeze were still pending. I also clarified the draft WAR-12 catalog text from “per firing season” to “per shot” to match the user's confirmed package; this is wording only and does not change simulation behavior. No current pack, Release Harness, adjacent Sim DLL, or source-manifest tuple is pinned here.

The previously documented `5ee659bd` and `f888640` artifacts are stale for this source. Their old output locations are not run targets. The host was occupied by externally owned controlled siege matches during preparation; wait for an idle host before any new match, and never stop or overlap those processes.

## User-confirmed package and implementation check

- CHM-07 gunpowder uses saltpeter, sulfur, and charcoal. The current draft recipe is 0.75 saltpeter, 0.10 sulfur, and 0.15 charcoal per powder unit. The cast-cannon recipe uses cast iron plus wood.
- A paid cannon shot consumes 0.25 powder. The siege trace records cannon shots and powder paid separately.
- Cannon fire sets the powder-breach state only when a cannon is supplied with powder. Against stone walls, that state applies a 0.3 wall-defense multiplier (70% reduction) for the following storm; assault against standing stone walls remains blocked until breach.
- Each attacking engine or cannon is assigned four adults. Baseline siege attrition is 1% per assigned adult per siege turn. DEF-09 multiplies that chance by 1.25 only when the target has a completed DEF-09, a built concentric castle, and a garrison. The garrisoned concentric-castle defense multiplier is 1.5 on top of the existing castle multiplier.
- Fief ocean crossings remain disabled after SEA-04 until the civilization completes KEY-6. SEA-10 doubles the plank-boat shipment limit from 12 L to 24 L; a natural trace must observe an actual water-route shipment to measure its throughput.

These rules are implemented in the current source. Their numeric recipe and balance values remain draft. Focused fixtures establish mechanics only; they do not establish natural whole-match exposure or acceptance.

## Reusable manifests and exact tools

Re-use the existing frozen seed files; do not generate duplicate rosters. Recheck collision/disjointness against all WP5/WP7/WP8/WP9/D20/WP-E5-12 ledgers after the final source freeze.

| Gate | Manifest | Seeds | SHA-256 | Trace and analyzer |
|---|---|---:|---|---|
| SEA-10 | [m12-e5-8-sea10-natural-seeds-2026-11-16.txt](m12-e5-8-sea10-natural-seeds-2026-11-16.txt) | 20 | `1f63acfdc5330fede8d4875799aff0b4cd907708fe538cc4a8abed7e932e84dc` | `--trace-kind sea10`; [analyze_fief_sea10_trace.py](tools/analyze_fief_sea10_trace.py) |
| F-T5 | [m12-e5-8-f-t5-natural-seeds-2026-11-16.txt](m12-e5-8-f-t5-natural-seeds-2026-11-16.txt) | 20 | `d30825f653a1a39c851a7a5f1d64a640ce0c69fee8da70c95c619d0a51edc29b` | `--trace-kind siege`; [analyze_fief_siege_trace.py](tools/analyze_fief_siege_trace.py) |

The guarded runner is [`tools/fief_trace_runner.py`](../../../tools/fief_trace_runner.py), SHA-256 `0d5e2b75161fdcc47617e22d4877f264393e7f9390bd43d98bc0665d40a2cb9d`. Both traces use 20 natural `fief-match` runs at exactly 130 minutes, without `--grant-e5`. Runner inputs are `--provenance`, `--source-root`, `--pack`, `--harness`, adjacent `--sim`, `--seeds`, a new absent `--output`, `--minutes 130`, and the gate's `--trace-kind`. Provenance must include `source_tree_sha256`, `source_manifest`, `source_manifest_sha256`, `pack_sha256`, `harness_sha256`, and `sim_sha256`. The runner refuses incomplete or mismatched provenance and existing output paths.

Analyzer hashes at this audit: SEA-10 `fb9343a0161b65052aab2edb6b50a0ef03541ff3fc7aeb7765b9810b09c756a6`; F-T5 `6caafe60957066226ebebb9c8b8a03e8261e59be644d7514f9f996371b40eb7f`.

Do not use the legacy `run_fief_ft5_cohort.py`; it is hard-pinned to the older 2026-11-15 F-T5 manifest. The current F-T5 analyzer is pinned to the 2026-11-16 manifest hash above. The SEA-10 analyzer validates the manifest and frozen run tuple supplied with the run.

## Evidence still needed

- **SEA-10:** A complete current-source trace and analyzer report. Count water-route dispatches separately from delivery, payment, return, loss, and censoring. Locked/unreachable routes and land trades are not water-throughput exposure. The prior natural evidence does not establish positive current-source actual-water-route throughput.
- **F-T5:** A complete current-source trace and analyzer report retaining every no-exposure and censored seed. Report engine/cannon investment, paid shots, powder conservation, breach, base and DEF-09 assigned crew-seasons/deaths, relief/capture/sally, and terminal/censored outcomes. The prior 20-seed natural sample had no persistent siege investment, shots, breaches, or crew-season exposure, so it was inconclusive.
- Neither trace report alone is a pass/fail balance gate or promotion evidence. Keep all E5 rows draft until WP-E5-11 evaluates the full required evidence.

## Launch sequence after WP-E5-7/source freeze

1. Confirm WP-E5-7 edits are settled and freeze one exact source revision. Build the numeric-status pack and Release Harness/Sim from it; keep the Sim DLL adjacent to the Harness.
2. Generate the canonical source manifest and complete provenance file; verify each source, pack, Harness, Sim, runner, analyzer, and seed-manifest hash. Re-run seed collision checks against the complete current inventory.
3. Confirm the host has been idle for at least two minutes. Use a new, absent output path under the candidate directory. Do not reuse the historical `5ee659bd` or `f888640` output names.
4. Run SEA-10 and F-T5 serially with the exact 130-minute runner contract above. Analyze each completed run against its frozen manifest and provenance. Preserve all outcomes, including no exposure, failures, interruptions, returns, losses, and right-censoring.

No source/pack/binary pins or output directories are reserved by this readiness note.

## Why prior natural samples had no qualifying exposure — 2026-11-16

A read-only source/trace review found no telemetry defect. In the prior F-T5 cohort, wars, assaults, and captures did not imply a persistent siege investment; the trace observed zero investments, shots, powder use, breaches, assigned crews, or DEF-09 exposure. The Warlord's walled-target investment path requires a viable supplied army and either target distress or at least one cannon plus 0.25 powder at the origin. The party must then arrive with enough holders to begin the investment; each engine or cannon needs four adults. Keep these prerequisite and no-exposure rows in the next report rather than counting generic war events as siege evidence.

For SEA-10, land trade and a locked water route do not measure the doubled cargo cap. A qualifying observation requires sailing unlocked after KEY-6, a physically viable water crossing, and a caravan dispatch on that route; the trace distinguishes `land`, `water_locked`, `water`, and `unreachable` and records dispatch separately from cargo delivery, payment, return, and loss. The current 20-seed manifests remain diagnostic inputs only until refreshed provenance is built from the final source freeze. Run siege and SEA-10 traces separately at the frozen 130-minute horizon, keep all zero-exposure/censored/error seeds, and treat zero exposure as inconclusive.
