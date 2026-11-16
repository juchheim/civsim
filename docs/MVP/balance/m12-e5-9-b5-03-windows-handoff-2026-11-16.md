# B5-03 Windows x64 handoff — 2026-11-16

**State: not ready to execute.** This prepares a future Windows x64 parity run after the final WP-E5-11 candidate is frozen. It does not report or imply Windows parity. The checkout is dirty, so the candidate hashes below are deliberately blank. Never fill them from the diagnostic `5ee659bd` tuple or another old build.

## Frozen input contract

Use the reviewed manifest [`m12-e5-9-b5-03-reviewed-seeds-2026-11-16.txt`](m12-e5-9-b5-03-reviewed-seeds-2026-11-16.txt), SHA-256 `12f313f9878c568798b32e723f1aa1783b2dbb3039bfae60749f7a279d1d9895`. It contains exactly 1,000 ascending seeds, **40,000,000–40,000,999**. The 2026-11-15 manifest (`32,000,000–32,000,999`, SHA-256 `1c05d1be…`) is superseded; the runner and comparator reject it. The manifest filename, seed block and hash in this handoff now match the runner's hard pins.

After WP-E5-11 and final promotion, freeze one clean committed source snapshot, final natural-Fief pack, Release Harness/Sim DLL pair and Release test assembly. Transfer byte-identical source, pack, binaries, test assembly, runner, merger, test source, comparator and manifest to both hosts. Record and recheck these SHA-256 values on each host:

| Artifact | Final pin |
|---|---|
| Source HEAD / tree / `content+src+game` aggregate | `<fill from final freeze record>` |
| Natural-Fief pack | `<sha256>` |
| Adjacent `CivSim.Harness.dll` / `CivSim.Sim.dll` | `<sha256>` / `<sha256>` |
| Release `CivSim.Tests.dll` | `<sha256>` |
| `tools/run_b5_03_determinism.py` | `<sha256>` |
| `tools/merge_b5_03_shards.py` | `<sha256>` |
| `tools/test_run_b5_03_determinism.py` | `<sha256>` |
| `tools/compare_replay_indexes.py` | `<sha256>` |
| Reviewed manifest | `12f313f9878c568798b32e723f1aa1783b2dbb3039bfae60749f7a279d1d9895` |

The runner's explicit test-source pin is `test_run_b5_03_determinism.py`; the test assembly must be built from the same final source and SDK on the freeze host, then copied byte-for-byte. Record the chosen .NET SDK/runtime on both hosts. Do not rebuild or modify inputs after freezing/hashing. Save the complete freeze record and file hashes with both host artifact roots.

## PowerShell run protocol

Use a fresh artifact root on a local NTFS volume. Fill the placeholders only from the final freeze record. The first call is preflight-only; it checks the host reports `Windows-x64`, clean source, manifest, source, binaries and test/tool hashes. It creates no output root. Do not add `--execute` until all checks pass.

```powershell
$RepoPath = 'C:\work\CivSim'
$PackPath = 'C:\frozen\final-natural-fief.pack'
$HarnessPath = 'C:\frozen\CivSim.Harness.dll'
$SimPath = 'C:\frozen\CivSim.Sim.dll'
$TestAssemblyPath = 'C:\frozen\CivSim.Tests.dll'
$ManifestPath = Join-Path $RepoPath 'docs\MVP\balance\m12-e5-9-b5-03-reviewed-seeds-2026-11-16.txt'
$RunnerPath = Join-Path $RepoPath 'docs\MVP\balance\tools\run_b5_03_determinism.py'
$MergerPath = Join-Path $RepoPath 'docs\MVP\balance\tools\merge_b5_03_shards.py'
$TestSourcePath = Join-Path $RepoPath 'docs\MVP\balance\tools\test_run_b5_03_determinism.py'
$ComparatorPath = Join-Path $RepoPath 'docs\MVP\balance\tools\compare_replay_indexes.py'
$ArtifactRoot = 'E:\CivSim-artifacts\B5-03\Windows-x64'

$Head = '<final-commit-sha>'
$Tree = '<final-git-tree-sha>'
$SourceAggregate = '<final-content-src-game-sha256>'
$PackSha = '<final-pack-sha256>'
$HarnessSha = '<final-harness-sha256>'
$SimSha = '<final-sim-sha256>'
$TestAssemblySha = '<final-test-assembly-sha256>'
$RunnerSha = '<final-runner-sha256>'
$MergerSha = '<final-merger-sha256>'
$TestSourceSha = '<final-test-source-sha256>'
$ComparatorSha = '<final-comparator-sha256>'
$ManifestSha = '12f313f9878c568798b32e723f1aa1783b2dbb3039bfae60749f7a279d1d9895'

# Record the execution environment alongside the final freeze record.
py -3 --version
dotnet --info
Get-ComputerInfo | Select-Object WindowsProductName, WindowsVersion, OsArchitecture

# Verify every transferred artifact before invoking Python.
$Expected = @(
  @($PackPath, $PackSha), @($HarnessPath, $HarnessSha), @($SimPath, $SimSha),
  @($TestAssemblyPath, $TestAssemblySha), @($RunnerPath, $RunnerSha),
  @($MergerPath, $MergerSha), @($TestSourcePath, $TestSourceSha),
  @($ComparatorPath, $ComparatorSha), @($ManifestPath, $ManifestSha)
)
foreach ($Pair in $Expected) {
  $Path = $Pair[0]
  $Actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
  if ($Actual -ne $Pair[1].ToLowerInvariant()) { throw "SHA-256 mismatch for $Path" }
}

$Common = @('--repo-root', $RepoPath, '--pack', $PackPath,
  '--harness-dll', $HarnessPath, '--sim-dll', $SimPath,
  '--manifest', $ManifestPath, '--expected-manifest-sha256', $ManifestSha,
  '--test-assembly', $TestAssemblyPath,
  '--expected-test-assembly-sha256', $TestAssemblySha,
  '--test-source', $TestSourcePath,
  '--expected-test-source-sha256', $TestSourceSha,
  '--expected-runner-sha256', $RunnerSha,
  '--expected-source-head', $Head, '--expected-source-tree', $Tree,
  '--expected-source-aggregate', $SourceAggregate,
  '--expected-pack-sha256', $PackSha, '--expected-harness-sha256', $HarnessSha,
  '--expected-sim-sha256', $SimSha, '--shard-count', '4')

# Preflight shard 0 only; no --execute and no matches.
& py -3 $RunnerPath @Common --output-root (Join-Path $ArtifactRoot 'shard-00') --shard-index 0
if ($LASTEXITCODE -ne 0) { throw 'B5-03 Windows preflight failed; do not run matches.' }

# Only after successful preflight, run four disjoint shards. Each holds 250
# consecutive seeds: 40,000,000–40,000,249; then the next three 250-seed ranges.
for ($ShardIndex = 0; $ShardIndex -lt 4; $ShardIndex++) {
  $ShardName = 'shard-{0:D2}' -f $ShardIndex
  $ShardRoot = Join-Path $ArtifactRoot $ShardName
  & py -3 $RunnerPath @Common --output-root $ShardRoot --shard-index $ShardIndex --execute
  if ($LASTEXITCODE -ne 0) { throw "B5-03 shard $ShardIndex failed; preserve partial artifacts and stop." }
}

# Merge only after all four shards have complete metadata and artifacts.
$MergeArgs = @('--manifest', $ManifestPath, '--expected-manifest-sha256', $ManifestSha,
  '--expected-test-assembly-sha256', $TestAssemblySha,
  '--expected-test-source-sha256', $TestSourceSha,
  '--expected-runner-sha256', $RunnerSha, '--expected-merger-sha256', $MergerSha,
  '--output-root', (Join-Path $ArtifactRoot 'merged'),
  '--expected-source-head', $Head, '--expected-source-tree', $Tree,
  '--expected-source-aggregate', $SourceAggregate, '--expected-pack-sha256', $PackSha,
  '--expected-harness-sha256', $HarnessSha, '--expected-sim-sha256', $SimSha)
for ($ShardIndex = 0; $ShardIndex -lt 4; $ShardIndex++) {
  $MergeArgs += @('--shard-root', (Join-Path $ArtifactRoot ('shard-{0:D2}' -f $ShardIndex)))
}
& py -3 $MergerPath @MergeArgs
if ($LASTEXITCODE -ne 0) { throw 'B5-03 Windows shard merge failed; preserve all artifacts.' }

# Compare the completed Windows merged index to the macOS arm64 merged index.
# Pin/record the comparator hash on both hosts; retain stdout, stderr and exit.
$MacIndex = 'C:\received\B5-03\macOS-arm64\merged\replay-index.json'
$WindowsIndex = Join-Path $ArtifactRoot 'merged\replay-index.json'
$MacIndexSha = '<sha256 from completed macOS merged run metadata>'
$MacIndexActual = (Get-FileHash -Algorithm SHA256 -LiteralPath $MacIndex).Hash.ToLowerInvariant()
if ($MacIndexActual -ne $MacIndexSha.ToLowerInvariant()) { throw 'macOS merged index hash mismatch.' }
$CompareLog = Join-Path $ArtifactRoot 'merged\cross-host-comparison.txt'
& py -3 $ComparatorPath $MacIndex $WindowsIndex --seeds-file $ManifestPath *> $CompareLog
$CompareExit = $LASTEXITCODE
$CompareLogSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $CompareLog).Hash.ToLowerInvariant()
if ($CompareExit -ne 0) { throw 'B5-03 cross-host replay-index comparison failed.' }
# Retain the comparator tool, both index hashes, command, output and exit code.
"comparatorSha256=$ComparatorSha`nmacOSIndexSha256=$MacIndexActual`nWindowsIndexSha256=$((Get-FileHash -Algorithm SHA256 -LiteralPath $WindowsIndex).Hash.ToLowerInvariant())`ncomparisonOutputSha256=$CompareLogSha`nexitCode=$CompareExit`ncommand=py -3 $ComparatorPath $MacIndex $WindowsIndex --seeds-file $ManifestPath" |
  Set-Content -Encoding ascii (Join-Path $ArtifactRoot 'merged\cross-host-comparison-metadata.txt')
```

## Shard accounting and parity criteria

The runner partitions the 1,000-row ordered manifest by integer boundaries into four contiguous non-overlapping shards of 250 seeds each. Expected ranges are `40,000,000–40,000,249`, `40,000,250–40,000,499`, `40,000,500–40,000,749`, and `40,000,750–40,000,999`. Each shard must complete 250 matches, replay verifications, and checkpoint continuations. Do not retry or replace a failed seed in place; preserve the failed attempt and use a separately labeled rerun protocol only if the gate owner approves it.

The merger requires all four unique shard indices, exact ordered partition/coverage, the same manifest/source/pack/binary/test/tool provenance, hashed stdout/stderr logs, all replay/index rows, continuation sidecars and snapshots. It writes a canonical 1,000-seed merged replay index. Compare the Windows merged index against the complete macOS arm64 merged index with the exact reviewed manifest. Pass requires 1,000/1,000 verified replays and continuation passes on both hosts; no missing seeds/checkpoints; same pack/mode/protocol; and identical ordered per-tick checksums and final checksums for every seed. Retain both index hashes, comparator command/output/exit code, and the byte-identical transferred input hashes. Any mismatch leaves B5-03 open for diagnosis. The complete gate description is in the [B5-03 determinism preflight](m12-e5-9-b5-03-determinism-preflight-2026-11-15.md).

No Windows run or parity result is recorded by this handoff.
