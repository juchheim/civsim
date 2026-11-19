# WP-E5-9 A8 whole-match preflight — stale 5ee record (2026-11-16)

## Follow-up implementation — 2026-11-17

The canonical whole-match probe now advances the live Fief match normally, clones the resulting checkpoint for baseline and perturbation variants, and refreshes only the target seat with the same `AiSystem` season-end update used in production. This holds whole-match history fixed while preventing other seats from reacting to a hidden perturbation before the target mind is measured. Hidden research perturbations complete the research and economy sides directly at the current tick, without another `SimRunner` step.

Raw rows and summaries use schema version 2. Each row has an `eligible` flag and `observerHasSettlement` evidence; an eliminated seat remains in the report as `eligible: false`, `observerHasSettlement: false`, `applied: false`, with the exact reason `notApplied: "observer has no settlement"`. Such rows are accounted for separately and do not count as missed controls. Eligible controls still must apply and pass, and each frozen seed must contain eligible examples of every visible-control kind and applied unseen exposure for every category. The focused validator contract checks these fields and per-seed exposure totals against raw JSONL. No acceptance threshold or balance parameter changed. Focused fast tests cover the seat-isolated hidden/visible fixture and schema validator rules; the full A8 campaign remains unrun.

**Status: stale historical preflight; do not run its pinned commands.** This preflight does not close A8. The 5ee values below are preserved as diagnostics only. The `d3908981…` source aggregate was a prior intermediate snapshot after the corrected test source was added; it is no longer the current working-tree aggregate. At this audit, HEAD remains `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c` and the dirty `content/src/game` snapshot hashes to `fd22df0fc0ca563a3e85c38538012aa05200e5e18ff6f04c77afdea7b07fbd48` across 1,046 files. This working-tree hash identifies the current source only; it is not a clean final freeze and has no verified final pack or Release binary tuple. A fresh final-tuple preflight is required after WP-E5-11's freeze, including new source, pack, Harness, Sim, test-source, test-assembly, runner, validator, and hash-helper hashes.

## Validator follow-up — 2026-11-16

The raw-row validator now independently checks each row's `pass` bit against its serialized `diffs`: null and unseen rows pass only with no diffs, while an applied visible positive control counts as detected only with at least one diff. This includes `unseen-horse`, the whole-match `AddHorseTriple` mutation path that the earlier partial triage did not exercise. Focused regression fixtures reproduce both former false-accept cases: an unseen-horse row with a nonempty diff and stale `pass: true`, and a visible-horse row with no diff but stale `pass: true`; both are rejected even when row artifact hashes are refreshed. The run3 whole-run output remains historical evidence only. This change has not been built or exercised against whole matches, and it does not explain or clear the historical unseen-state failures. A fresh frozen whole-match cohort is still required after the candidate freeze; no 8-seed cohort was run for this change.

## Historical 5ee pins (diagnostic record only)

- HEAD: `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`
- `content/src/game` aggregate: `5ee659bd7303b24190f59ad01299f4d7430e18ce3adb0441470cab96b1452733` (1,045 files)
- Numeric-E5 diagnostic pack: `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/numeric-e5.pack`, SHA-256 `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`
- Release Harness: `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Harness.dll`, SHA-256 `26fd3451b52bf342c757118324fa2d6dd8b881d4dbda81435438e9285037d100`
- Adjacent Release Sim: `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Sim.dll`, SHA-256 `b347189f8d386a4b2a2756271298a8a05e41e7abb82009566ccb8dfa2f631dfe`
- A8 seed file: [`seeds.txt`](seeds.txt), 8 unscreened seeds `23000001–23000008`, SHA-256 `98015f9ec04a35762ea1ede1d84c18b9fce576e80ecb5c41cec3edcd045dbed6`. A search of recorded seed/manifest/metadata files under `docs/MVP/balance` and the candidate run folder found no collision for these seeds. The old output path `/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/a8-current-2026-11-16/` is retained as historical metadata and must not be used for the fresh run.

## Existing A8 protocol and prior result

The whole-match protocol plays four Fief AI seats for 64 minutes, samples every 16 seasons, compares null clones, strict unseen-state perturbations (deposits, rival powder/cannon, fleets/crew, horses, hidden people/stores/research), Tier-B composite unseen perturbations, and positive controls for visible deposits, own stock, own fleets, and visible horses. The acceptance condition is zero observer-input/decision diffs from all applicable unseen perturbations, zero null-clone diffs, and detected positive controls. Retain every row and report every non-applied perturbation/control; do not turn lack of exposure into a pass.

The preserved 8-seed run3 used the older `7decf87…` aggregate, Harness `aa7b830c…`, and Sim `a8cb7666…`, so it is not current-candidate evidence. It recorded four strict unseen failures and 27 Tier-B composite failures. It also had only 100/128 visible-fleet controls applied. The test summary counted only *applied* positive controls when calculating `visibleControlMisses`, so its zero-miss field concealed 28 unapplied cases. It calculated `tierBComposite.withInputOrDecisionDiffs` but did not assert that count was zero. The corrected canonical test source now addresses both fail-closed defects; historical run3 results remain diagnostics only.

The exact run3 test source is unrecoverable: the closest preserved source has SHA-256 `7d60fbd36ba52e8491cbd36be58e9bf5e5de2f49e4ff04b55620f319e858241f`, while run3 metadata records `8454418f…`. The corrected whole-match test source is now in the canonical `src/CivSim.Tests` project. Its source SHA-256 is `ca736d5f8ca54ee577814e1e3c23eb672dd2104bf5316c4f07a16d784a22b7f3`; it compiled in a Release build but has not been executed. The historical `run_gate.py` hard-codes the old 7dec/aa7b/a8cb tuple and must not be reused. Any eventual run requires a fresh final-tuple preflight and recorded test-source/test-assembly hashes.

The exact `8454418f…` test source was not found in the preserved checkout, run folders, or searched scratch copies. The run3 preservation folder contains raw outputs, JSONL, summary and metadata, but no test source or `CivSim.Tests.dll`; its checksum list covers only those output artifacts. Metadata records `testBinaries.CivSim.Tests.dll=3855925a…`, while the top-level `testAssemblySha256` is null. The closest preserved source has SHA-256 `7d60fbd36ba52e8491cbd36be58e9bf5e5de2f49e4ff04b55620f319e858241f`, not the recorded `8454418f…`. The earlier failed attempt has source SHA `54e381721507e76e151f3f76a717f73558b68a67344a7ecdd015ac72cef5a617` and assembly SHA `3749702e7921315802b7dc6fdad1a40ecabb1632b2cbc90c31d65358e608b67e`, also distinct. Thus neither the run3 source bytes nor its assembly bytes are recoverable from the preserved artifacts; recorded hashes identify the missing artifacts but cannot recreate or authenticate them. Treat all run3 results as historical diagnostics tied to that incomplete tuple.

The canonical test source incorporates the two fail-closed changes: unapplied or undetected visible controls count as misses, and nonzero Tier-B composite failures fail the test (as do unapplied unseen perturbations). A Release build verifies compilation only; the eight-seed whole-match test and full A8 acceptance remain unrun.

The reusable runner is [`run_fief_a8_acceptance.py`](../tools/run_fief_a8_acceptance.py). It requires pinned HEAD, Git tree, source aggregate, pack/Harness/Sim/test-source/test-assembly hashes, runner/validator/source-hash-helper hashes, a hashed seed manifest, at least one hashed exclusion manifest for disjointness, protocol settings, and an absent output root. By default it verifies those pins and builds the Release test assembly, then exits without running matches. Only explicit `--execute` runs the named A8 test; the runner captures build/test logs, writes pre/post metadata and artifact hashes, refuses changed inputs, and calls `validate_fief_a8_result.py`. The A8 JSONL writer now sorts rows by seed/checkpoint/observer/category so parallel match completion order cannot reorder evidence. These are test/runner changes only; no AI policy behavior or acceptance thresholds changed. Fixture coverage passes, but no final tuple or whole-match acceptance run has been executed.

To obtain the pinned test-assembly hash, after the final source and pack are frozen, run one Release test-project build and hash `src/CivSim.Tests/bin/Release/net10.0/CivSim.Tests.dll`. The wrapper rebuilds and requires the resulting test assembly, Harness and Sim hashes to match those preflight pins. If the repeated build does not reproduce the exact test assembly bytes, stop and resolve the toolchain/build reproducibility before any matches. Freeze the runner, validator and hash helper hashes too; they are now explicit required CLI pins and are checked before and after execution.

Implemented test-source gate conditions:

1. Make any unapplied visible positive-control row count as a missed control (or otherwise fail the gate); include the unapplied count in `a8-summary.json`.
2. Assert `tierBBad == 0`; the canonical corrected source now fails on nonzero composite failures.
3. Keep all row-level `Applied`/`NotApplied`, perturbation counts, inputs and decision diffs, clone checksums, and visible-control outcomes in the raw JSONL. Do not drop no-exposure rows from the report.

## Stale 5ee preflight commands — do not run

The following commands record the old 5ee pin checks and command shape for historical reference only. They are stale and intentionally fail against the current source aggregate. Do not use their output paths or run command for acceptance. After WP7/source and numeric-value changes settle, prepare a fresh preflight for the final tuple.

```sh
python3 docs/MVP/balance/tools/hash_e5_workspace.py --output /tmp/a8-source-preflight.json
python3 - <<'PY'
import hashlib, json
from pathlib import Path
m = json.loads(Path('/tmp/a8-source-preflight.json').read_text())
assert m['head'] == 'bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c', m['head']
assert m['aggregate_sha256'] == '5ee659bd7303b24190f59ad01299f4d7430e18ce3adb0441470cab96b1452733', m['aggregate_sha256']
pack = Path('/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/numeric-e5.pack')
harness = Path('/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/harness-release/CivSim.Harness.dll')
sim = harness.with_name('CivSim.Sim.dll')
expected = {
    pack: 'f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a',
    harness: '26fd3451b52bf342c757118324fa2d6dd8b881d4dbda81435438e9285037d100',
    sim: 'b347189f8d386a4b2a2756271298a8a05e41e7abb82009566ccb8dfa2f631dfe',
}
for p, want in expected.items():
    got = hashlib.sha256(p.read_bytes()).hexdigest()
    assert got == want, (str(p), got, want)
assert Path('/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/a8-current-2026-11-16').exists() is False
seeds = [int(s) for s in Path('docs/MVP/balance/m12-e5-9-a8-current-preflight-2026-11-16/seeds.txt').read_text().splitlines() if s and not s.startswith('#')]
assert seeds == list(range(23000001, 23000009)), seeds
print('A8 input pins and unused output root verified')
PY
```

Historical test command shape (stale; do not run):

```sh
export CIVSIM_A8_PACK=/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/numeric-e5.pack
export CIVSIM_A8_OUT=/Users/ejuchheim/Projects/CivSim/civsim-e5-runs/candidate-5ee659bd/a8-current-2026-11-16
export CIVSIM_A8_SEEDS=23000001,23000002,23000003,23000004,23000005,23000006,23000007,23000008
export CIVSIM_A8_INTERVAL=16
export CIVSIM_A8_MINUTES=64
export CIVSIM_A8_DOP=4
export CIVSIM_A8_TIERB=1
dotnet build src/CivSim.Tests/CivSim.Tests.csproj -c Release
dotnet test src/CivSim.Tests/CivSim.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~FiefWholeMatchFogFairnessTests.Whole_fief_matches_are_invariant_to_unseen_state_and_respond_to_visible_state'
```

This command is not runnable for the current candidate. The corrected test is now in the canonical tree, but the 5ee pack/binaries and the recorded source tuple are stale. No A8 run or whole-match test has been launched with the corrected source.

## Post-run result validation

The reusable runner writes `a8-pins.json` before execution and records pre/post input pins, `unchangedDuringRun`, process exit status, the exact seed list and protocol, plus SHA-256 and byte size for both raw artifacts in `run_metadata.json`. It invokes the validator after the run. The validator can also be run manually for an audit:

Validate the resulting artifacts with:

```sh
python3 docs/MVP/balance/tools/validate_fief_a8_result.py \
  /path/to/new/a8-run \
  --pins /path/to/a8-pins.json
```

The validator recomputes strict and composite failures from every raw row, reconciles summary counts, requires exposure in each unseen category, requires null controls to pass, and counts every unapplied or undetected visible positive control as a miss. It rejects absent seed/category coverage, stale test/source/binary pins, changed inputs, incomplete artifact hashes, and any missing Tier-B summary/assertion data. Its focused fixtures are in `test_validate_fief_a8_result.py`. The old run3 artifacts intentionally fail provenance/exit validation and cannot be rehabilitated as acceptance evidence.
