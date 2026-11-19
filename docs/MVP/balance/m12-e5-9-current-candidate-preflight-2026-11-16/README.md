# WP-E5-9 current-candidate AI acceptance preflight — 2026-11-16

**Prepared only; no matches, search, build, or test was launched.** This is a preregistration for A1–A7 and the separate B5-10 Legend-vs-Warlord check. The pinned `5ee659bd` tuple is the current diagnostic/calibration candidate, not a final acceptance freeze. The F-T1 calibration is active. Do not execute any command below until F-T1 and other heavy cohorts have stopped, all WP-E5 source changes are settled, and the final candidate is re-frozen. If any source/content/game, pack, Harness, or Sim hash differs, make a new dated preflight with fresh manifests and roots rather than relabeling these files.

**Audit update (2026-11-16):** this bundle's pinned hashes are stale for current acceptance, and A8 is handled separately. See [the preflight audit](AUDIT-2026-11-16.md) before using any of the commands below.

## Candidate identity pinned for this preflight

- HEAD: `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`
- `content/src/game` aggregate: `5ee659bd7303b24190f59ad01299f4d7430e18ce3adb0441470cab96b1452733` (1,045 files)
- Numeric E5 diagnostic pack: `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/numeric-e5.pack`, SHA-256 `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`
- Release Harness: `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Harness.dll`, SHA-256 `26fd3451b52bf342c757118324fa2d6dd8b881d4dbda81435438e9285037d100`
- Adjacent Release Sim: `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Sim.dll`, SHA-256 `b347189f8d386a4b2a2756271298a8a05e41e7abb82009566ccb8dfa2f631dfe`

The values above identify the requested diagnostic candidate. At preparation time, a read-only hash_e5_workspace.py pass confirmed the pinned HEAD and aggregate above. At final execution, independently hash the candidate files and `content/src/game` workspace before and after each cohort. Require exact matches and `unchangedDuringRun: true`. A candidate mismatch means stop and re-freeze; do not use these as pass evidence for another candidate.

## Frozen seed manifests and output roots

All newly allocated world-seed ranges below are disjoint from each other and from the existing E5 records searched under `docs/MVP/balance` at preparation time. The manifests are unselected; no seed was screened or run. SHA-256 checksums for every frozen manifest are in [`MANIFEST-SHA256.txt`](MANIFEST-SHA256.txt).

| Acceptance leg | Frozen input | World seeds / planned matches | Fresh output root | Criterion |
|---|---|---:|---|---|
| A1 Warlord vs Council | [`a1_a7_council.tsv`](seeds/a1_a7_council.tsv) | 40 / 80 mirrored | [`outputs/a1-council/`](outputs/a1-council/) | Warlord win rate ≥80% |
| A2 Warlord vs Builder | [`a2_builder.tsv`](seeds/a2_builder.tsv) | 40 / 80 mirrored | [`outputs/a2-builder/`](outputs/a2-builder/) | Warlord win rate ≥70% |
| A2 Warlord vs Grower | [`a2_grower.tsv`](seeds/a2_grower.tsv) | 40 / 80 mirrored | [`outputs/a2-grower/`](outputs/a2-grower/) | Warlord win rate ≥70% |
| A2 Warlord vs Wanderer | [`a2_wanderer.tsv`](seeds/a2_wanderer.tsv) | 40 / 80 mirrored | [`outputs/a2-wanderer/`](outputs/a2-wanderer/) | Warlord win rate ≥70% |
| A3 Warlord vs Rusher | [`a3_rusher.tsv`](seeds/a3_rusher.tsv) | 40 / 80 mirrored | [`outputs/a3-rusher/`](outputs/a3-rusher/) | Warlord wins ≥70%; no settlement loss in ≥80% of matches |
| A4 Chieftain vs Warlord | [`a4_chieftain.tsv`](seeds/a4_chieftain.tsv) | 40 / 80 mirrored | [`outputs/a4-chieftain/`](outputs/a4-chieftain/) | Chieftain win rate 25–40%, inclusive |
| B5-10 Legend vs Warlord | [`b5_10_legend.tsv`](seeds/b5_10_legend.tsv) | 40 / 80 mirrored | [`outputs/b5-10-legend/`](outputs/b5-10-legend/) | Legend win rate ≥60% |
| A5 equal-difficulty FFA | [`a5_ffa_worlds.tsv`](seeds/a5_ffa_worlds.tsv) and [`a5_ffa_match_plan.tsv`](seeds/a5_ffa_match_plan.tsv) | 120 / 600 (all 5 rotations for every world) | [`outputs/a5-ffa/`](outputs/a5-ffa/) | No archetype wins >30% of its appearances |
| A6 exploit search | [`a6_screen_and_finalists.tsv`](seeds/a6_screen_and_finalists.tsv) | 10,009 distinct policy candidates screened; 16 finalists × 3 seeds × 2 mirrored seats = 96 validation matches | [`outputs/a6-exploit/`](outputs/a6-exploit/) | Every finalist ≤55% wins vs Warlord |
| A7 blunder telemetry | same 40-world schedule as A1 | 80 mirrored / 160 AI seats | [`outputs/a1-a7-council/`](outputs/a1-a7-council/) | Six harness counters pass per rules below |

A1 and A7 intentionally share a seed schedule so the gates describe the same Council/Warlord match cohort. A2 opponents each have a separate seed schedule, and all other legs are disjoint. A5's match plan has exactly 600 rows: each of 120 worlds appears once for each rotation 0–4. This corrects the prior A5 metadata mismatch, where a 40-seed manifest was attached to a 120-world run.

### A7 operational thresholds

Use the existing `--check-a7` report and preserve all raw traces. The gate requires: zero keystone stalls; zero plan-dithering matches; zero avoidable starvation deaths per 80 matches; undefended settlement captures below 0.1 per match; directly attributed herd crashes below 0.2 per match; and zero idle-labor seasons per match. The harness computes starvation, undefended-loss, herd-crash, and idle-labor rates over the 80 mirrored matches (and reports the 160 AI seats); these thresholds match current `AiCommand.ReportA7` logic and [14 §10.4](../../14-ai-opponents.md#104-blunder-detectors).

### A5 denominator and decision rule

The 600-match schedule includes four archetypes in each match. Each archetype sits out one of the five rotations, so each receives 480 appearances. Compute each archetype's win share as wins divided by its 480 appearances, not total matches; fail only if any share is greater than 30% (exactly 30% passes), matching [14 §17](../../14-ai-opponents.md#17-archetype-balance-a5-2026-10-20). Preserve per-match winner, world seed, rotation, seat assignment, and final Legacy so the denominator can be independently reconstructed.

### A6 screen and finalist evidence

The fixed search shape is `417 generations × 24 children + 1 initial policy = 10,009` candidates. Record the generated policy keys and unique count; duplicates do not count toward the 10,000 distinct-policy minimum. Preserve all screen fitness outcomes, finalist selection (8 top-fitness plus 8 diverse), and every validation result. Validate all 16 selected finalists on each of three fresh world seeds, both mirrored seatings, at the 64-minute horizon. A finalist's win rate is its wins divided by six validation matches; exactly 55% cannot occur at that denominator, so the rule is simply that no observed finalist may exceed 55%. If the harness uses a different denominator or ties convention in its printed rate, report wins, losses, ties, matches, and the exact calculation. A6 only makes a claim about this newly searched policy set against this pinned Warlord/candidate; do not generalize to all possible policies.

## Execution commands (not launched)

From `/Users/ejuchheim/Projects/CivSim/civsim-e5`, first validate the frozen source aggregate and binary/pack hashes. Use the already built pinned Release binaries; do not build while F-T1 or other heavy cohorts are active. Each output root is fresh and tracked by `.gitkeep`. Run one acceptance leg at a time, save full stdout/stderr and exit status as `raw-output.txt` plus `run_metadata.json`, and preserve per-match details. Metadata must include exact command, start/end times, pre/post source aggregate and HEAD/tree, pack/Harness/Sim SHA-256, manifest SHA-256 and expanded seed list, expected/actual match counts, ties, censors, errors, and raw-output SHA-256/byte size. Any nonzero exit, censor, missing match, identity drift, or incomplete A6 finalist set is not a pass.

```sh
ROOT=docs/MVP/balance/m12-e5-9-current-candidate-preflight-2026-11-16
HARNESS=/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Harness.dll
PACK=/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/numeric-e5.pack
```

A1:

```sh
dotnet "$HARNESS" ai --pack "$PACK" --mode fief \
  --duel warlord council --seeds 40 --first-seed 91000001 \
  --minutes 64 --match-details \
  > "$ROOT/outputs/a1-council/raw-output.txt" 2>&1
```

A7, same seed schedule:

```sh
dotnet "$HARNESS" ai --pack "$PACK" --mode fief \
  --check-a7 --seeds 40 --first-seed 91000001 --minutes 64 \
  > "$ROOT/outputs/a1-a7-council/raw-output.txt" 2>&1
```

A2 Builder, Grower, and Wanderer (run each as a separate cohort):

```sh
dotnet "$HARNESS" ai --pack "$PACK" --mode fief \
  --duel warlord builder --seeds 40 --first-seed 91001001 --minutes 64 --match-details \
  > "$ROOT/outputs/a2-builder/raw-output.txt" 2>&1

dotnet "$HARNESS" ai --pack "$PACK" --mode fief \
  --duel warlord grower --seeds 40 --first-seed 91002001 --minutes 64 --match-details \
  > "$ROOT/outputs/a2-grower/raw-output.txt" 2>&1

dotnet "$HARNESS" ai --pack "$PACK" --mode fief \
  --duel warlord wanderer --seeds 40 --first-seed 91003001 --minutes 64 --match-details \
  > "$ROOT/outputs/a2-wanderer/raw-output.txt" 2>&1
```

A3 and A4:

```sh
dotnet "$HARNESS" ai --pack "$PACK" --mode fief \
  --duel warlord rusher --seeds 40 --first-seed 91004001 --minutes 64 --match-details \
  > "$ROOT/outputs/a3-rusher/raw-output.txt" 2>&1

dotnet "$HARNESS" ai --pack "$PACK" --mode fief \
  --duel chieftain warlord --seeds 40 --first-seed 91005001 --minutes 64 --match-details \
  > "$ROOT/outputs/a4-chieftain/raw-output.txt" 2>&1
```

A5: make the complete world list explicit (`--seeds 120`) and all five supported rotations explicit.

```sh
dotnet "$HARNESS" ai --pack "$PACK" --mode fief \
  --ffa warlord --seeds 120 --first-seed 91010001 \
  --ffa-rotations 0,1,2,3,4 --minutes 64 --match-details \
  > "$ROOT/outputs/a5-ffa/raw-output.txt" 2>&1
```

A6: policy-generation RNG seed is `91020000`, screen world seed is `91020001`, and fresh validation world seeds are `91020002–91020004`. Keep the generated finalist/policy evidence and output separate from any earlier A6 run.

```sh
dotnet "$HARNESS" ai --pack "$PACK" --mode fief --exploit \
  --generations 417 --children 24 --seeds 1 --screen-minutes 1 --minutes 64 \
  --first-seed 91020000 \
  --exploit-screen-first-seed 91020001 \
  --exploit-validation-first-seed 91020002 \
  --exploit-output "$ROOT/outputs/a6-exploit/policy-search.txt" \
  > "$ROOT/outputs/a6-exploit/raw-output.txt" 2>&1
```

Legend vs Warlord (separate B5-10 check; not A4 and not the old E3 B31 45% criterion):

```sh
dotnet "$HARNESS" ai --pack "$PACK" --mode fief \
  --duel legend warlord --seeds 40 --first-seed 91006001 \
  --minutes 64 --match-details \
  > "$ROOT/outputs/b5-10-legend/raw-output.txt" 2>&1
```

## Role and interpretation boundaries

- **Calibration:** the pinned `5ee659bd` tuple and these seed schedules are diagnostic preparation. They do not promote numeric rows, select/tune a Warlord policy, or establish a final acceptance freeze.
- **Acceptance:** only a complete run whose source/content/game, pack, Release Harness, Sim, manifest, and output hashes are captured and unchanged can count. A1–A7 and B5-10 are independent acceptance rows; passing one does not close another.
- **Finalists:** A6 validation seeds are held out from policy search/screening and used only after the fresh search selects its finalists. Do not inspect them for tuning.
- **Human review:** this bundle contains no playtest or selector approval evidence.

## Metric-definition notes

The sourced acceptance bars are sufficiently explicit for these commands: A1 ≥80% (current update in [14](../../14-ai-opponents.md), superseding the older 85% table text), A2 ≥70% per opening script, A3 ≥70% wins plus no settlement loss in ≥80% of matches, A4 25–40%, A5 ≤30% per-archetype appearance share, A6 ≤55% for every finalist, A7 the six counter limits above, and Legend ≥60% under the E5 B5-10 requirement. A5's denominator is appearances, as specified above and used by prior FFA reports. The horizon is resolved by separating the general M10 baseline from the later-era chapter gate. [14 §17](../../14-ai-opponents.md#17-archetype-balance-a5-2026-10-20) defines the original minute-33 A5 measurement. The approved E3 chapter-horizon rerun uses 64 minutes ([28 §14](../../28-production-gap.md#14-a5-archetype-balance)), and the Fief acceptance readiness explicitly runs A1–A5/A7 in Fief mode at 64 minutes, with A5 measured on 120 worlds / 600 rotations and the same 30% per-archetype appearance ceiling ([Fief acceptance readiness](m12-e5-9-fief-ai-acceptance-readiness-2026-11-14.md)). WP-E5-9 keeps that chapter-horizon protocol. Therefore this preflight's 64-minute A5 command is the existing Fief/E5 gate; the minute-33 M10 baseline is not an additional E5 pass condition. No metric threshold or extra user-facing requirement is added. Preserve integer numerators and denominators in reports so borderline rates can be audited.

### Final no-go / go hash checks

The candidate's exact workspace aggregate and three binary inputs were verified at preparation time. Repeat these checks immediately before and after every acceptance leg; any assertion failure cancels the run and requires a newly frozen candidate and fresh manifests.

From the repository root, run:

    python3 docs/MVP/balance/tools/hash_e5_workspace.py --output /tmp/e5-ai-preflight.json
    python3 -c "import json; p=json.load(open('/tmp/e5-ai-preflight.json')); assert p['head']=='bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c', p['head']; assert p['aggregate_sha256']=='5ee659bd7303b24190f59ad01299f4d7430e18ce3adb0441470cab96b1452733', p['aggregate_sha256']"
    shasum -a 256 /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/numeric-e5.pack /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Harness.dll /Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Sim.dll

Require output hashes, in order, to be f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a, 26fd3451b52bf342c757118324fa2d6dd8b881d4dbda81435438e9285037d100, and b347189f8d386a4b2a2756271298a8a05e41e7abb82009566ccb8dfa2f631dfe. Save the JSON and shasum output under that leg's output root as preflight evidence.
