# B5-03 Fief determinism gate preflight — 2026-11-15

This preflight prepares the S1/B5-03 1,000-match determinism gate. It does not build, run matches, or alter simulation behavior. The acceptance target is the existing S1 criterion: 100% replay agreement over 1,000 matches, plus identical checksums on macOS arm64 and Windows x64 (MVP 17 §4.2; MVP 15 D9).

For the Windows x64 PowerShell handoff and current diagnostic-candidate warning, see [the 2026-11-16 Windows handoff](m12-e5-9-b5-03-windows-handoff-2026-11-16.md).

## Frozen cohort

Use the reviewed contiguous unsigned seed block **40,000,000–40,000,999**, exactly 1,000 matches. This replaces the earlier 32,000,000 block; the B5-03 runner pins this newer reviewed range. It is disjoint from the recorded E5/Fief seed ranges, including F-T1 `202610xx`, F-T4 `20,270,403–20,279,994`, `20,291,001–20,296,000`, and `20,300,000–20,399,999`, WP5 cohorts in `20,4xx,xxx`, F-T5 `20,500,000–20,599,999`, AI/A6 ranges, F-T6/T7 `20,700,000–20,799,999`, WP11's `31,000,000` block, and the WP-E5-12 blocks in the `33,000,000–36,000,000` range. Seed provenance is ascending and contiguous, with no sampling or outcome/geography screening.

The frozen manifest is [m12-e5-9-b5-03-reviewed-seeds-2026-11-16.txt](m12-e5-9-b5-03-reviewed-seeds-2026-11-16.txt), SHA-256 `12f313f9878c568798b32e723f1aa1783b2dbb3039bfae60749f7a279d1d9895`. It contains one unsigned decimal seed per line, no comments. The earlier 2026-11-15 file is superseded and rejected by the current runner and comparator. Do not edit the reviewed file after its hash is recorded.

## Match and pack protocol

Use the **natural Fief** rules, standard four Warlord archetypes and controls exactly as `fief-match` currently constructs them; do not pass `--grant-e5`. Horizon is **130 minutes (78,000 ticks)**, pinned explicitly. Before running, freeze the exact final natural Fief compiled pack and record its SHA-256; freeze the Release harness DLL and record its SHA-256. Record source commit, SDK/runtime version, OS/architecture and `ChapterRules.Fief` selection with the outputs. Do not rebuild between recording and replay verification. Pack content hash, regenerated-world fingerprint, replay format/mode, RNG-dependent applied command sequence and every `SimRunner` checksum are required.

## Current CLI and remaining command seam

The existing match replay codec and verifier are suitable: `MatchReplay.FromRun` captures the pack hash, world fingerprint, Fief mode, archetypes, AI controls, command log, interval checksums and final checksum; `replay verify FILE --pack FILE` verifies them. `research --mode fief --record` uses a no-control match and optional opening script, so it is **not equivalent to the standard AI-driven `fief-match` protocol**. The Harness adds `fief-match --record-dir NEW_DIR`; it records one `<seed>.replay` per run using the Fief control replay format. The directory must not already exist. Verify each replay with `replay verify FILE --pack FILE`.

`fief-match --seeds-file FILE` consumes one unsigned decimal seed per line, in file order, and rejects blank/invalid lines and duplicate seeds. `--seeds-file` cannot be combined with `--seed` or `--seeds`. `fief-match --checkpoint-continuation NEW_DIR` writes each seed's snapshot payloads and a `<seed>.continuation.csv` sidecar. It captures tick 0 and every 750 ticks, restores each snapshot into a fresh Fief match, checks the immediate state checksum, and advances the live and restored match together while comparing every 50-tick checksum through the configured horizon or earlier chapter end. The option rejects behavior and diagnostic overrides so the restored model matches the standard Fief configuration. The sidecar records snapshot SHA-256, checkpoint checksums, every compared interval checksum, and the final state checksum. Each output directory must not already exist.

The Harness now has a deterministic batch index command for recorded `.replay` files. After recording the cohort, run `replay index` with the same frozen seed manifest and pack on each host. It verifies every replay, emits entries in ascending seed order with pack hash, mode, ticks, command/checksum counts, final checksum, the complete ordered `(tick, checksum)` sequence, and per-seed verification outcome. Missing files and duplicate replay files become failed seed entries; malformed or unrequested replay filenames/seeds are rejected. Do not substitute the existing `acceptance --s15`: it uses different archetype/control coverage and only in-memory checkpoints.

```sh
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll replay index \
  --replay-dir docs/MVP/balance/m12-e5-9-b5-03-output-2026-11-15/<host>/replays \
  --pack /absolute/path/to/final-natural-fief.pack \
  --seeds-file docs/MVP/balance/m12-e5-9-b5-03-reviewed-seeds-2026-11-16.txt \
  --output docs/MVP/balance/m12-e5-9-b5-03-output-2026-11-15/<host>/replay-index.json
python3 docs/MVP/balance/tools/compare_replay_indexes.py \
  docs/MVP/balance/m12-e5-9-b5-03-output-2026-11-15/macOS-arm64/replay-index.json \
  docs/MVP/balance/m12-e5-9-b5-03-output-2026-11-15/Windows-x64/replay-index.json \
  --seeds-file docs/MVP/balance/m12-e5-9-b5-03-reviewed-seeds-2026-11-16.txt
```

The comparator verifies the exact frozen manifest bytes and requires both indexes to contain the full ordered manifest seed sequence, then checks verification status, pack/mode, per-seed run counts, final checksums, and every checksum record by sequence position and tick. It does not normalize or rewrite either input artifact; source, pack-file, Harness, and Sim provenance remains independently pinned by the runner/merger metadata.

## Frozen runner and shard merge

`tools/run_b5_03_determinism.py` is the frozen runner for this gate. It refuses dirty checkouts or any mismatch in source HEAD/tree, canonical content/src/game source aggregate and per-file manifest, pack, Release harness, or simulation DLL. It writes and hashes the source file manifest, records input SHA-256 values, and confirms source and binary hashes remain unchanged at run completion. By default it is preflight-only. `--execute` runs only the selected shard; with the default `--shard-count 1`, that is all 1,000 seeds. For an accelerated macOS arm64 run, use four balanced contiguous 250-seed shards with indices 0 through 3 and distinct fresh artifact roots. Run the same four shards independently on Windows x64 against byte-identical source, pack, harness, simulation DLL and manifest inputs. Do not pass `--compare-index` on individual shards.

After all four shards complete on each host, `tools/merge_b5_03_shards.py` verifies the exact four-way partition and complete 1,000-seed coverage; requires matching source/runtime/protocol/pack/binary hashes; revalidates every local stdout/stderr log hash, replay, checksum-index row, continuation sidecar and snapshot; then emits a canonical ascending-seed index. Compare the two host-level merged `replay-index.json` files with `tools/compare_replay_indexes.py`. Never merge evidence from different source or binary snapshots. The shard partition, canonical coverage and raw-log integrity rules have focused unit tests in `test_run_b5_03_determinism.py`.

Example for one host after source, pack, binaries and hashes are frozen (run once per index, each with a new output root):

```sh
python3 docs/MVP/balance/tools/run_b5_03_determinism.py \
  --repo-root "$REPO" --pack "$PACK" --harness-dll "$HARNESS" --sim-dll "$SIM" \
  --output-root "$OUT/$HOST/shard-00" --expected-source-head "$HEAD" \
  --expected-source-tree "$TREE" --expected-source-aggregate "$SOURCE_AGGREGATE" \
  --expected-pack-sha256 "$PACK_SHA" \
  --expected-harness-sha256 "$HARNESS_SHA" --expected-sim-sha256 "$SIM_SHA" \
  --shard-count 4 --shard-index 0 --execute
```

Repeat with `--shard-index 1`, `2` and `3` and output roots `shard-01`, `shard-02`, and `shard-03`. Once all four are complete, merge them:

```sh
python3 docs/MVP/balance/tools/merge_b5_03_shards.py \
  --shard-root "$OUT/$HOST/shard-00" --shard-root "$OUT/$HOST/shard-01" \
  --shard-root "$OUT/$HOST/shard-02" --shard-root "$OUT/$HOST/shard-03" \
  --output-root "$OUT/$HOST/merged" --expected-source-head "$HEAD" \
  --expected-source-tree "$TREE" --expected-source-aggregate "$SOURCE_AGGREGATE" \
  --expected-pack-sha256 "$PACK_SHA" \
  --expected-harness-sha256 "$HARNESS_SHA" --expected-sim-sha256 "$SIM_SHA"
```

The merger rejects missing/duplicate shard indices, reordered or overlapping seed subsets, incomplete manifest coverage, failed replay verification, incomplete tick checksums, or divergent inputs. A shard run is not itself B5-03 acceptance; only the merged complete index on each host plus successful cross-host comparison closes the technical cohort.

## Golden owner and cross-host comparison

For the frozen seed manifest and the exact frozen source, compiled pack, Release binaries, and 130-minute natural-Fief protocol above, the **macOS arm64 `replay-index.json` is the canonical `fief-v1` golden**. Record its SHA-256 and retain the complete index, including every seed's ordered per-tick checksum list. The Windows x64 run is an independent replay-verification and parity check against that golden: verify every replay on Windows and compare the Windows index to the macOS index across all 1,000 seeds, every tick, final checksums, mode, pack, and run parameters. Windows output does not replace or silently regenerate the golden.

Each host must use a fresh, previously nonexistent artifact root (`.../macOS-arm64/` and `.../Windows-x64/`); do not reuse, merge, or overwrite the other host's directory. For identical inputs, preserve command-log and replay-file hashes and compare replay bytes where possible. Byte-identical replay files are the preferred result; any byte difference must be reported and investigated, while the required semantic acceptance remains full replay verification plus exact per-seed/per-tick checksum-index parity. Never normalize or rewrite artifacts to make hashes match. A changed source, pack, manifest, harness, simulation DLL, or command protocol defines a different run and cannot reuse this golden.

After choosing an exact natural Fief pack and freezing the source, run the following shape on macOS arm64 and Windows x64, separately, preserving one artifact directory per host:

```sh
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll fief-match \
  --pack /absolute/path/to/final-natural-fief.pack \
  --seeds-file docs/MVP/balance/m12-e5-9-b5-03-reviewed-seeds-2026-11-16.txt \
  --minutes 130 \
  --record-dir docs/MVP/balance/m12-e5-9-b5-03-output-2026-11-15/<host>/replays \
  --checkpoint-continuation docs/MVP/balance/m12-e5-9-b5-03-output-2026-11-15/<host>/continuations
```

Batch verification remains external: invoke `replay verify` once per `.replay` file and collect exit codes and hashes alongside the continuation sidecars.

## Required artifact set, metadata schema, and acceptance

Store one `run_metadata.json` in each fresh host root. It must use this schema shape (the example values are placeholders; `seeds` contains one record for every manifest seed):

```json
{
  "schema": "civsim.fief-determinism-run/v1",
  "source": { "head": "<commit>", "tree": "<tree>", "dirty": false, "dirtyDiffSha256": null },
  "runtime": { "sdk": "<dotnet SDK>", "runtime": "<dotnet runtime>", "os": "<OS/version>", "architecture": "<arm64|x64>" },
  "protocol": { "mode": "Fief", "minutes": 130, "command": "fief-match", "argv": ["<exact argv>"] },
  "manifest": { "path": "<path>", "sha256": "<sha256>", "seedCount": 1000 },
  "pack": { "path": "<path>", "sha256": "<sha256>", "contentHash": "<hash>" },
  "binaries": {
    "harnessDll": { "path": "<path>", "sha256": "<sha256>" },
    "simDll": { "path": "<path>", "sha256": "<sha256>" }
  },
  "commands": [
    { "name": "fief-match", "exitCode": 0, "stdout": "<path>", "stdoutSha256": "<sha256>", "stderr": "<path>", "stderrSha256": "<sha256>" },
    { "name": "replay-verify", "seed": 40000000, "argv": ["replay", "verify", "<replay-path>", "--pack", "<pack-path>"], "exitCode": 0, "stdout": "<path>", "stdoutSha256": "<sha256>", "stderr": "<path>", "stderrSha256": "<sha256>" },
    { "name": "replay-index", "exitCode": 0, "output": "<path>", "outputSha256": "<sha256>" },
    { "name": "checkpoint-continuation", "exitCode": 0, "stdout": "<path>", "stdoutSha256": "<sha256>", "stderr": "<path>", "stderrSha256": "<sha256>" }
  ],
  "artifacts": { "replayIndex": "<path>", "replayIndexSha256": "<sha256>", "rawLogs": ["<path and sha256 for each log>"] },
  "seeds": [
    {
      "seed": 40000000,
      "matchExitCode": 0,
      "ticks": 78000,
      "commandCount": "<from index>",
      "checksumInterval": "<from index>",
      "checksumCount": "<from index>",
      "finalChecksum": "<from index>",
      "orderedTickChecksumRecords": "<full ordered sequence is in the hashed index>",
      "replay": { "path": "<path>", "sha256": "<sha256>", "verifyExitCode": 0 },
      "continuation": {
        "sidecar": "<path>", "sidecarSha256": "<sha256>",
        "snapshots": [{ "tick": 0, "path": "<path>", "sha256": "<sha256>", "expectedChecksum": "<hash>", "restoredChecksum": "<hash>", "resumedChecksum": "<hash>" }]
      }
    }
  ]
}
```

In addition to this manifest, preserve/hash every replay, continuation sidecar and snapshot, both indexes, every raw stdout/stderr log, and the index-comparator output. Record one `replay-verify` command entry per seed. For each seed, record match and replay-verification exit codes, ticks, command count, checksum interval/count, final checksum, replay hash, checkpoint-sidecar hash, and every checkpoint snapshot hash plus expected/restored/resumed checksums. The index itself carries the complete ordered `(tick, checksum)` sequence for each seed; do not replace it with only a summary hash.

On both hosts require 1,000/1,000 replay verifications and continuation passes, no missing seeds/checkpoints, and exact per-seed/per-tick checksum-index parity. The macOS arm64 index is the canonical `fief-v1` golden for these exact inputs; the Windows x64 index must compare equal under the comparator. Snapshot bytes and replay bytes must be retained and compared; snapshot hashes may differ only if the format is not platform-canonical, while semantic checksums immediately after restore and at every continued interval must match. Any mismatch fails the gate and both raw records remain available for diagnosis. B5-03 remains open until both runs and comparison complete successfully.

No code, builds, match runs, or commits were made for this preflight.
