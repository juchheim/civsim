# B5-11 Fief performance cohort preflight — 2026-11-15

> **Superseded command snapshot.** The direct `fief-perf` commands below predate the guarded B5-11 cohort runner and use stale hashes. Do not use them for acceptance. Use `m12-wp11-b5-11-current-candidate-preflight-2026-11-16.md` for the current runner contract and post-freeze invocation; no current final source/pack/binary tuple has been frozen.

This preflight freezes two fresh 20-seed cohorts for later sequential execution. It does not build, launch matches, or change any performance or figure-budget threshold. Do not launch while another simulation/performance cohort is using the host. At the current review, the F-T7 held-out health cohort is active.

## Current implementation snapshot

The harness hash `54e2c31e…f1ffa4` recorded in the original preflight is stale. The current F-T7 candidate freeze records source/content/game aggregate `e42e3605be2de8bd73a4cc049b0219cf92ed0a43994cf11064486958b2cb7144`, numeric-status pack `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`, and Release harness `aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f` ([candidate freeze ledger](m12-e5-9-f-t7-candidate-freeze-2026-11-15.json)). This is a provisional measurement snapshot, not the final integrated B5-11 freeze; recapture every input hash after WP-E5 source changes settle. The current-candidate no-build `FiefPerformanceCommandTests` filter passed 3/3. No multi-seed performance cohort has run.

## Frozen manifests

Each manifest uses the `FiefPerformanceCommand.ReadSeeds` format: UTF-8 text, one unsigned base-10 `ulong` seed per line; blank lines and `#` comments are accepted. The natural cohort is [m12-wp11-fief-natural-seeds-2026-11-15.txt](m12-wp11-fief-natural-seeds-2026-11-15.txt), SHA-256 `f787766662b46319f7f0807eacd27592aa8c2af4163f366d52ddec07a3e3a786`. The E5-granted stress cohort is [m12-wp11-fief-grant-e5-seeds-2026-11-15.txt](m12-wp11-fief-grant-e5-seeds-2026-11-15.txt), SHA-256 `b5d541d0f820ff3fc3979d2ff5158c15b9bc24ac61fa7e11af4196eec1f006b6`.

Selection used Python `random.Random(20261116).sample(range(31_000_000, 32_000_000), 40)`, without replacement. The first 20 draws were assigned to natural Fief and the next 20 to E5-granted stress; each cohort was sorted for execution. No geography, eligibility, progress, or outcome screening was performed. The two manifests are disjoint. Their 31,000,000–31,999,999 source range is disjoint from recorded Fief seed blocks/manifests: F-T1 `202610xx`; F-T4 `20270403–20279994`, `20291001–20296000`, `20300000–20399999`; F-T5 `20500000–20599999`; A6 screen/finalist seeds `20266019`, `20270019–20270021`, `21000001–21000003`, `21010001`; and the F-T6/F-T7 cohort in `20,700,000–20,799,999`. Existing WP5/Fief fit and validation manifests are also in the `20,4xx,xxx` range.

## Command and artifact capture

`fief-perf` defaults to 130 minutes with 5 warm-up minutes; these commands pin both values. Natural Fief omits `--grant-e5`; the stress cohort includes it. `FiefPerformanceCommand` writes `<prefix>-ticks.csv`, `<prefix>-tick-samples.csv`, `<prefix>-seats.csv`, and `<prefix>-provenance.txt`, and prints summaries to stdout. Per-seed rows include expected/observed timed ticks, terminal censor status, final tick, and checksum. Run each command sequentially in an otherwise idle process, after the host is free and the validated numeric scratch pack and Release DLL are frozen:

```sh
mkdir -p docs/MVP/balance/m12-wp11-fief-natural-output-2026-11-15 docs/MVP/balance/m12-wp11-fief-grant-e5-output-2026-11-15

dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll fief-perf \
  --pack /absolute/path/to/validated-e5-numeric-scratch.pack \
  --seeds-file docs/MVP/balance/m12-wp11-fief-natural-seeds-2026-11-15.txt \
  --minutes 130 --warmup-minutes 5 \
  --output-prefix docs/MVP/balance/m12-wp11-fief-natural-output-2026-11-15/perf \
  > docs/MVP/balance/m12-wp11-fief-natural-output-2026-11-15/console.txt 2>&1

dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll fief-perf \
  --pack /absolute/path/to/validated-e5-numeric-scratch.pack \
  --seeds-file docs/MVP/balance/m12-wp11-fief-grant-e5-seeds-2026-11-15.txt \
  --minutes 130 --warmup-minutes 5 --grant-e5 \
  --output-prefix docs/MVP/balance/m12-wp11-fief-grant-e5-output-2026-11-15/perf \
  > docs/MVP/balance/m12-wp11-fief-grant-e5-output-2026-11-15/console.txt 2>&1
```

Before execution, record SHA-256 for the exact validated scratch pack and the exact Release harness DLL next to the outputs; do not substitute an unvalidated pack or rebuild after recording. No scratch pack was present at preflight time, so its hash is intentionally pending identification of the validated numeric pack. The existing Release DLL at `src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll` currently hashes to `54e2c31e53764829c9db87d5d671fb1051ed18c749a35859b17bc19508f1ffa4`; verify this exact file is the one used at launch and preserve its hash with both cohorts. The command records manifest, pack, executable, runtime, OS, architecture, and process provenance in the sidecar. Capture all generated CSVs, the provenance sidecar, console output, both manifest hashes, pack hash, and DLL hash. The measurement gates and 600-figure diagnostic threshold remain as documented in `m12-wp11-performance.md`.
