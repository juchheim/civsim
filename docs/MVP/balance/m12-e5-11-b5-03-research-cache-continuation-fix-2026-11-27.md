# B5-03 Fief continuation cache fix — 2026-11-27

## Failure and diagnosis

The frozen pre-fix candidate `0f7bdd2a182568425f5b1c8531a1442513ca6008` failed Fief continuation for seed `40000110` at tick `67550`. The uninterrupted and resumed whole-match hashes were `c471dc0d3cd79a83` and `380f360a46e27c57`. Economy, politics, guidance, AI, and Legends component checksums matched; only research differed.

At the preceding snapshot (tick `67500`), the original research cache for civ 3 had `InsightPerYear=113223195319451` and `CommonsPerYear=33553496887649`. Restoring that snapshot recomputed Insight as `113180746677185`, while the reported inputs and Commons output were identical. The next research step consumed the different cached Insight and diverged. `CivResearch.Write` did not store these caches or their dirty phase, and `ResearchSystem.ReadSnapshot` always recomputed them from restored state.

## Fix

Commit `2be9d593f92f2ad2aff784820a9ca9c51bee3f56` adds a versioned cache trailer to Fief and Discovery research snapshots. It preserves `InsightPerYear`, `CommonsPerYear`, `KnowHowPerYear`, and `InsightDirty`, then includes those future-affecting values in the corresponding state checksum. A Fief snapshot without this trailer follows the previous restore path; Bronze snapshot bytes and checksums are unchanged.

## Validation

- `dotnet test src/CivSim.Tests/CivSim.Tests.csproj -c Release --no-restore -v minimal` — **1,706 passed, 0 failed, 0 skipped**.
- `Fief_snapshot_preserves_cached_research_outputs_and_dirty_phase` verifies that all four cache values and the dirty phase survive snapshot restore and produce equal checksums.
- The original seed `40000110` was rerun for 130 simulated minutes with periodic continuation snapshots. All continuation interval checksums matched through tick `78000`; the final uninterrupted and resumed checksums were both `41e93d426a1b5afb`.

This closes the reproduced single-seed failure only. The 1,000-seed B5-03 Windows/macOS parity cohort remains required on a clean final freeze.
