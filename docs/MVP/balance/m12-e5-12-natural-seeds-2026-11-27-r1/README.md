# WP-E5-12 fresh natural Fief cohort — 2026-11-27 r1

## Registration

This replaces the consumed 2026-11-16 whole-match block for acceptance. The earlier 80-match run is diagnostic because it used pre-correction candidate `0f7bdd2`; its seeds and outcomes have already been observed. This new block is reserved for a single clean candidate run.

- Seed generation: `random.Random(20261127).sample(range(47_000_000, 48_000_000), 80)`, sorted for execution.
- Manifest: [`m12-e5-12-natural-seeds-2026-11-27-r1.txt`](../m12-e5-12-natural-seeds-2026-11-27-r1.txt)
- Manifest SHA-256: `c69840b655fa6127ccf1565cabaecb774db7e4b0834913cc353152d814670af9`
- WP-E5-12 parallel runner SHA-256: `ee9e171fd31409363c5ec750792e16f65c798722ca16a880037f375cf0ce0725`
- Provenance helper SHA-256: `496f1064b4b02f3e28c8aaff33140a8b2e13d7f8e1a785a68a8cd32f0138a10a`
- Runner fixture SHA-256: `8e950e85f1744d6299c851f1a88975e5876716816526885c584d6badd1b13567`
- Collision audit: [seed-collision-audit.json](seed-collision-audit.json) records that all 80 exact seed tokens were absent from 5,398 seed manifests, run metadata, preflight records, and named seed ledgers scanned under `/Users/ejuchheim/Projects/CivSim` and `/private/tmp` on 2026-11-27. The 47M block also does not overlap the registered WP-E5-7 46M calibration/holdout block.
- Exposure policy: natural Fief, 130 simulated minutes, no geography/progress/outcome screening, and no `--grant-e5`.

## Acceptance protocol

Run the guarded `run_wp_e5_12_whole_match.py` once after the final source, numeric pack, adjacent Release Harness/Sim, runner, and manifest are frozen, with `--workers 4`. The runner uses separate per-seed outputs and synchronized metadata checkpoints while executing up to four matches concurrently. Its fixtures exercise the concurrent path. The observer set is described in [the protocol preflight](../m12-e5-12-whole-match-preflight-2026-11-16/README.md); the manifest pin was updated for this registration. Use a new absent output root. Preserve every match, raw log, replay, replay-index verification, censor status, and pre/post provenance record.

The cohort is also the combined natural whole-match measurement for compatible F-T1, F-T2, F-T5, F-T6, F-T7, F-T8, SEA-10, arms-production-lag, and WP-E5-7 denominator traces. Controlled F-T3, matched-state F-T4, and the paired GOV-11 treatment/control test remain separate protocols.

The pre-registered WP-E5-12 gates remain: all 80 matches/320 seats accounted for; at least 256/320 seats complete KEY-6; and the median own KEY-5-to-KEY-6 interval among seats completing both lies in 22.5–27.5 minutes. Report censor classes, match endings, and all replay/provenance failures. This whole-match cohort does not substitute for the separate human B5-12 comprehension study.

## Candidate anchor

The registration was prepared against the corrected runtime represented by `b075e1747c86153b03096db9b46f391040183726`, source aggregate `299b39031074567c110817f38fdb3180fa2d54c3f5662acdcf1240ecbf7207a6`, and numeric pack SHA-256 `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`. Before execution, pin the exact committed final freeze (including the full tree and current binaries/tools) in the runner metadata; abort if the runtime source aggregate or pack differs.
