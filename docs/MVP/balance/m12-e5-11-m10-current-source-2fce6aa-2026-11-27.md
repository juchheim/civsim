# M10 current-source AI closeout — 2026-11-27

## Disposition

**A1 passes the E5 release criterion by explicit owner approval:** the current-source run reports 76% over 80 mirrored matches, meeting the owner-approved minimum of 74%. The captured output still marks A1 as failing the nominal 80% benchmark, and `M10 AI CORE CHECK FAILED` / `M10 measurable gates FAIL` therefore remain faithful descriptions of the raw harness result. Preserve both facts; the owner-approved E5 criterion governs the release decision for A1.

All other measured M10 lines in this run pass. This report does not turn the harness's raw M10 output into a clean overall PASS: the harness still uses the nominal A1 threshold, reports the integrated M10 check as incomplete, and the applicable release disposition combines this owner decision with the separately evidenced A6 and A8 gates.

## Frozen run inputs

- Candidate commit: `2fce6aa29eab5d75e0cd6018c44d6ea6d5a65b78`
- Candidate tree: `de5be3c634f2b806c87f0b9ea49cde8352f0f4d5`
- `content/src/game` aggregate: `227902576ec15604a8399d395b170d55305ad0e50823e30fd49087d8f742342b`
- Numeric pack SHA-256: `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`
- Release Harness SHA-256: `650e2ac7775d8842b15c1383bd4f5e08174762a78e74afa1acdda71b68fa8cba`
- Release Sim SHA-256: `52d0cbdc39886119652f4231c383c97dc01623926df99e00876fd043eda37416`
- Mode and horizon: Fief, 64 minutes; 40 mirrored seeds / 80 matches. The first seed defaults to `20261019`.
- Raw output: [captured log](m12-e5-11-m10-current-source-2fce6aa-2026-11-27/raw-output.txt) (SHA-256 `44e09b507840c3aa62a39031262255c87da6122b36c52b113ddb636aa5c59630`)
- Reconstructed input pins and run parameters: [run-pins.json](m12-e5-11-m10-current-source-2fce6aa-2026-11-27/run-pins.json)
- Raw log completed at `2026-11-27T23:27:34.818006Z`.

The M10 run was launched directly rather than through the guarded run-metadata wrapper. Consequently, no original serialized run metadata or process exit code was captured. The input pins and run parameters were reconstructed from the completed invocation and independently verified against the frozen candidate artifacts. Do not infer a clean process exit from the report.

## Results

| Gate | Result | E5 disposition |
|---|---:|---|
| A1 Warlord vs Council | 76% / 80 matches; raw FAIL against 80% | **PASS under owner-approved ≥74% E5 bar** |
| A2 Grower / Builder / Wanderer | 74% / 82% / 82% | Pass |
| A3 survival / wins | 100% survival / 79% wins | Pass |
| A4 Chieftain vs Warlord | 36% / 80 matches | Pass |
| A5 five archetypes | Builder 30.0%, Grower 25.6%, Wanderer 21.9%, Warlord 23.1%, Sage 24.4% | Pass; Builder is exactly at the ceiling |
| A7 blunder counters | All six counters zero | Pass |
| B29 forecast fidelity | 158/158 within 15%; median error 3.0%; zero censored | Pass |
| B31 Legend vs Warlord | 46% / 80 matches | Pass against ≥45% |
| B27 tin-poor fallback | 141/154 = 92%; dormant bronze 0.050 per match | Pass |
| B28 defenses before first believed-bronze raid | 17/17 = 100% | Pass |
| B30 mandate overflow | 0.212 per match | Pass |
| B30 suicide raids / unsupplied siege desertion | 0 / 0 | Pass |
| B30 naked caravans | 0.138 per match | Pass |

The run's raid calibration MAE is 0.28; the unwalled-frontier count is zero (diagnostic only), and 86/160 AI seat-matches exceeded eight seasons at mandate cap (proxy only, not a gate).

## Separate gates

- A6 current-source acceptance: [report](m12-e5-9-a6-current-source-2026-11-27-r1/README.md).
- A8 current-source acceptance: [report](m12-e5-9-a8-current-source-2026-11-27-r1/README.md).
- The whole-match WP-E5-12 holdout, current-source F-T cohorts, B5-03 determinism/parity, B5-11 performance, and B5-13 final technical review remain independently tracked in the E5 closeout status. A1 approval closes A1 only.
