# WP-E5-12 runtime provenance audit — 2026-11-27

## Finding

The active 80-match WP-E5-12 cohort was launched from commit `042e49ad1930012c3dc1bf4fc9034d74e673f91f`, while the frozen closeout candidate is `2fce6aa29eab5d75e0cd6018c44d6ea6d5a65b78`. A byte-level source audit resolves the apparent source-aggregate mismatch: both snapshots contain the same **1,177 files** under `content/`, `src/`, and `game/`, with **zero path or file-hash differences**. The aggregate hashes differ because the provenance format includes the commit ID. The only commit-to-commit file changes are the WP-E5-12 runner, its fixture test, and its README; no product or build-configuration files changed.

The numeric pack is identical at SHA-256 `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`. The Release Harness and Sim DLL hashes differ (`d9fac40c…` / `5e695b5d…` on the cohort worktree; `650e2ac7…` / `52d0cbdc…` on the candidate). Their visible embedded differences are the assembly informational version (the respective commit ID), absolute PDB paths, and CodeView/PDB debug identifiers.

A reflection audit found identical type, field, and method signatures and attributes, and identical method IL, local-variable, and exception-handler fingerprints for both assemblies:

- Harness fingerprint: `FF07B5CE360D2F893E84CDCD3D91D80F98C3BD0B82B4457CC0CF6210F293E609` (17,044 fingerprint rows)
- Sim fingerprint: `913552E805A9ED45A980ECF692ECD15E5E2C4D5B0C0B5007830027ADECB159D9` (44,553 fingerprint rows)

The audit command is implemented in [`tools/assembly_code_fingerprint`](tools/assembly_code_fingerprint/). The source-file comparison used the checked-in `hash_e5_workspace.py` logic; its two captured file lists had 1,177 entries each and zero SHA-256 mismatches.

## Disposition

The cohort is source- and executable-code-equivalent to candidate `2fce6aa`; it does not need to be repeated solely because of the different commit-stamped aggregate or DLL hashes. The WP-E5-12 gate itself remains open until the guarded runner reaches terminal `complete`, all 80 matches and 320 seats are accounted for, replay-index verification succeeds, pre/post pins remain unchanged, and the preregistered KEY-6 completion and KEY-5→KEY-6 timing conditions are analyzed. The eight-person comprehension study remains separate and human-only.

This disposition does not treat partial logs as acceptance results. The active run's own commit, pack, DLL, manifest, runner, and provenance hashes remain its authoritative provenance record.
