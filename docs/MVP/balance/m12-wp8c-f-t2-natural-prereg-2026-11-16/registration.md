# F-T2 natural mill power ledger preregistration

**Prepared:** 2026-11-16

**Status:** Provisional seed block prepared; not launchable until the final candidate is frozen and all run pins are filled. No match or build was run to prepare this registration.

## Protocol

- **Worlds:** 20 natural Fief matches, 130 simulated minutes each. The sample size is provisional because the F-T2 runner accepts an explicit count but the existing protocol does not specify a fixed count. Older interrupted 16-seed attempts are documented as failed attempts, not as a sample-size requirement.
- **Seeds:** `seeds.txt` beside this registration; exact SHA-256: `c072ee375e8e1d65982e27229120dc454d79b041681af8587cf3b095ab38cb69`.
- **Sampling:** 20 unique seeds sampled without replacement using Python `secrets.SystemRandom` (OS-backed cryptographic randomness) from the 60,000,000–69,999,999 range. Before sampling, numeric identifiers already present in tracked `docs/MVP/balance` content were excluded; after sampling, all 1,446 tracked repository paths and their readable contents were scanned for exact collisions. The block has no exact seed collision in tracked files. Seed selection was not screened on maps, outcomes, or expected ledger behavior.
- **Run mode:** Natural Fief, power ledger enabled, 130-minute horizon, no `--grant-e5`. Execute every listed seed once, preserving all raw logs, exit codes, censored windows, errors, and hashes. Do not replace seeds after observing output.
- **Measurement:** Use the existing F-T2 runner/parser and its twelve-season inclusive town window. Grain coverage remains the agreed adult-equivalent capacity proxy; no flour inventory is modeled. The second windmill remains allowed. This registration adds no acceptance threshold or promotion rule.

## Collision audit

The scan covered all tracked files under `docs/MVP/balance` (277 files; 8,185 distinct 6–10 digit numeric identifiers in their contents), then checked the selected seeds against every tracked repository path and readable file content (1,446 paths). Exact collisions were zero in each check and in the named cohorts below:

- F-T1 calibration and final holdout seed files.
- WP7 calibration and holdout files.
- The F-T4 50-million-range paired roster in `m12-wp8d-f-t4-prereg-2026-11-16/manifest.json` and `seeds-focus.tsv`.
- Tracked WP8 and WP9 ledgers, including prior F-T2/F-T3 diagnostics and F-T5/F-T6/F-T7 seed lists.
- B5 manifests and seed lists, including the 1,000-seed B5-03 lists.

The manifest hash refers to the exact `seeds.txt` bytes, including its comment line and final newline. Recompute it before any later use and require an exact match.

## Pins and execution

All final source and binary pins are intentionally pending. Do not reuse the old diagnostic candidate tuple or binaries. Immediately before launch, freeze and record:

| Pin | Value |
|---|---|
| Git HEAD | pending final freeze |
| E5 source aggregate SHA-256 | pending final freeze |
| Numeric pack SHA-256 | pending final freeze |
| Release Harness DLL SHA-256 | pending adjacent Release build |
| Adjacent Release Sim DLL SHA-256 | pending adjacent Release build |
| F-T2 runner / provenance helper revision | pending final review |

Reserved fresh output root (checked absent when this registration was prepared):

`/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/ft2-natural-prereg20-2026-11-16`

After the freeze, invoke `docs/MVP/balance/tools/run_fief_ft2_natural_ledger.py` with `--expected-seed-count 20`, the manifest SHA above, the final five provenance values, exact pack and adjacent Release DLL paths, and this output root. The runner refuses a reused output directory and checks provenance before and after the cohort. Report the resulting ledger distributions descriptively; this registration establishes no pass/fail threshold.
