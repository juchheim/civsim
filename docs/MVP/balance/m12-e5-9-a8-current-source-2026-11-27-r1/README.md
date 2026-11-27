# Fief A8 whole-match current-source acceptance — 2026-11-27 r1

**Result: PASS.** The guarded whole-match observer run and independent validator both pass on the frozen E5 closeout candidate `2fce6aa29eab5d75e0cd6018c44d6ea6d5a65b78`.

## Frozen inputs

- Source tree: `de5be3c634f2b806c87f0b9ea49cde8352f0f4d5`
- `content/src/game` aggregate: `227902576ec15604a8399d395b170d55305ad0e50823e30fd49087d8f742342b`
- Numeric pack SHA-256: `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`
- Harness SHA-256: `5feac4a0162638143a9385632a32c841eef6ec8d122df7e60ec7045fc0607d14`
- Sim SHA-256: `4cb8c92ee84a7f5fa2d60a33ea15faefa10cd736ca00558fa9737f5b9d13e17a`
- Test assembly SHA-256: `6d21bdfaa425eb8c4821d2b5a64b3c103ef01199670a8b0fd46c5bcef4d9f073`
- Test source SHA-256: `af56ce81c521c773375056a3f07ddc902e7c1351ca2c12490b75d415718f41c2`
- Seed manifest SHA-256: `58e52b0d6a070fd5415a6a0554922f0bda113b62022ee5624abcffdfda5d0683`
- Eight fresh seeds: `83001001–83001008`; each ran 64 simulated minutes with observer checkpoints every 16 seasons.

## Acceptance results

- **2,304/2,304** observer checkpoints validated; zero clone-checksum mismatches.
- **640/640** unseen-state composite interventions applied; zero observer invariance failures, unapplied interventions, or ineligible checkpoints.
- All five hidden-state categories were exercised in all eight matches: deposits, powder/cannon, fleet/crew, horses, and people/stores/research.
- Visible controls detected all interventions: 128/128 each for deposits, powder stock, fleets, and horses; zero misses.
- Null controls: 512 applied, zero diffs. Tier-B composite controls: 512 applied, zero input or decision diffs.
- Independent validation: `status: pass`, zero composite, strict, null, or visible-control failures.
- Frozen Release test invocation passed **1/1** in 56m34s.

## Evidence files and hashes

The adjacent files preserve the preflight, input pins, full observer rows, summary, validator output, test log, and build log. SHA-256 values:

| File | SHA-256 |
|---|---|
| `a8-pins.json` | `a9a092960970145f51ff000cc271b8bda105e06f78824be4436234d7af4458e8` |
| `a8-rows.jsonl` | `9279662c8fb1a5c0c14122801486d23d1dec4d7420d5a30fa787f0611407df4c` |
| `a8-summary.json` | `eb5e4a67dca633b01d38886e3adb935d6d43cdab262f61205facc94270064820` |
| `validation.json` | `d3a9f2b8744ef911e699a35b670164afe33df33bf0b652a6db07522eeffd9710` |
| `run_metadata.json` | `7c0e52e80ffa0d7a3b740364be9e3caf4707810e549881952314c03472ffb70d` |
| `test.log` | `88c1ea6b962149f5f636352a7323bd88f98538a0ec5e4396e13b1ebaf5754f17` |

This closes the current-source whole-match A8 acceptance gate. It does not close A1–A7 or waive any other E5 requirement.
