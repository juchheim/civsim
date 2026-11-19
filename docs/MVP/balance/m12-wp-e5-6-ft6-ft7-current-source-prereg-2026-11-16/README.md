# WP-E5-6 F-T6/F-T7 current-source natural cohort preregistration — 2026-11-16

## State and pins

Preregistered, diagnostic, and not launchable. The calibration and held-out rosters each contain 20 distinct seeds. This follows the existing runner's fixed 20-seed manifest requirement and the two-stage F-T7 draft-hypothesis workflow: use calibration to assess a draft candidate, then use a disjoint held-out cohort only after the candidate and every source and binary pin are frozen. Held-out results must not be used to tune that candidate.

Final source and binary pins are explicitly `PENDING_FINAL_FREEZE` in [registration.json](registration.json). The authoritative source is actively changing, so no current pack or DLL is represented here as matching a final source. Do not start a build or cohort from these pending pins. Final freeze must include commit, source aggregate, numeric-status pack, Release Harness, adjacent Sim DLL, test assembly, F-T6/F-T7 fixture sources, runner, analyzer, test helper, and runtime version. Use the updated runner's per-output `provenance.json` as the full run record.

## Seed selection and collision audit

- Calibration: [calibration-seeds.txt](calibration-seeds.txt), 20 seeds sampled without replacement from 41,000,000–41,999,999 with `random.Random(20261121)`, then sorted for execution. SHA-256: `3e11fa10bd553d2bb1a349d5ccfcc737293b273e3dec69e6080dabdee93ab0a4`.
- Held-out: [heldout-seeds.txt](heldout-seeds.txt), 20 seeds sampled without replacement from 42,000,000–42,999,999 with `random.Random(20261122)`, then sorted for execution. SHA-256: `4985622a7b7ab7697588fd450eebf9a317293fedb3e88cd03b9eff66fe0c3daa`.
- No world, geography, KEY-5 timing, exposure, or outcome screening was used. The two pools are disjoint.
- The audit scanned UTF-8 text files throughout the authoritative checkout, excluding `.git`, build outputs (`bin`, `obj`, `.godot`), and this preregistration directory. It checked exact standalone eight-digit seed tokens, including historical seed outputs and manifests for F-T1 r2, F-T2, F-T3, F-T4, WP7–WP9, and B5. At preparation time: 1,594 files, 215,362 eight-digit tokens, and zero collisions. Full details and selected seeds are recorded in [registration.json](registration.json). Re-run the audit after any new seed ledgers are added and before launch.

## Fixed measurement protocol

Each seed runs one 130-minute natural `fief-match`, with no `--grant-e5`, and both `--fief-health-trace` and `--fief-institution-trace`. Keep every seed's output and every censor. Use the existing analyzer's 20,000-resample seed bootstrap with RNG seed `20261115`.

For F-T6, report working, dormant, queued, and unresolved counts, known staffed denominator, summary reconciliation, and typed blockers descriptively. **There is no approved numeric dormant-rate cutoff.**

For F-T7, report pooled expected epidemic-hazard share and its seed-bootstrap interval after filtering to seasons at or after each seat's own KEY-5. The existing proposed 10–14% band and interval-containing-12% screen remain an **unapproved draft hypothesis only**, not user-approved numeric acceptance bounds. Keep any fitted scale in draft status unless separately approved. Preserve exposure and censoring information. If no valid natural route-import attempts occur, MED-08 natural import attenuation remains inconclusive; focused keyed-gate fixture evidence is separate.

## Launch sequence after source freeze

1. Confirm the host is idle and the source has stabilized. Finalize and record all pins; rebuild the Release Harness, adjacent Sim, and Release test assembly from that exact source and run focused F-T6/F-T7 tests. Do not reuse binaries after any source change.
2. Create a new absent calibration output directory, fill `--expected-*` values from the final freeze, including `--test-assembly` and `--expected-test-assembly-sha256`, and pass this calibration manifest's SHA-256 as `--expected-manifest-sha256`. Run the existing runner with `--cohort-role calibration`.
3. Analyze calibration descriptively. If a draft candidate/source changes, rebuild and re-pin the complete tuple before holdout. Do not change F-T6 criteria or treat the draft F-T7 screen as approved.
4. Only after candidate and pins are frozen, create a new absent held-out output directory and run the disjoint held-out manifest with `--cohort-role held_out`. Preserve its outcomes unopened until then.
5. Analyze held-out data descriptively against the preregistered measurements. Do not promote numeric values or claim MED-08 natural import validation without valid attempts.

No output directories are reserved by this preparation. No build, test, catalog edit, or match has been run or authorized by this preregistration.
