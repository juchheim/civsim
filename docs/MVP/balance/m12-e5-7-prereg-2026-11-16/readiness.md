# WP-E5-5/6 current-source audit and WP-E5-7 preregistration

Read-only audit snapshot: 2026-11-16. This folder is isolated under `/private/tmp`; no repo files, source, pack, binaries, or simulation outputs were changed. No tests, builds, or cohorts were run.

## Candidate identity and applicability

The E5 checkout is `/Users/ejuchheim/Projects/CivSim/civsim-e5`, HEAD `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`, current content/src/game aggregate `2fae166f4f3078097d44b5ed4642b758abb2793a3e78c0903cfb4fc30384f34e` (1,043 scoped files; 116 modified and 243 untracked). The prior candidate aggregate was `7decf87…`; therefore no earlier candidate-sensitive run is automatically current-source evidence.

Observed available artifacts: the numeric scratch pack hash is `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`; the adjacent Release Harness and Sim hashes are `aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f` and `a8cb7666042cf6651a6b9558b3d6db7b1f00566b5cc30041077e67b86540ad50`. These binaries were previously paired with aggregate `7decf87…`, not proven built from `2fae166f…`. The checkout's `civsim.pack` is a different artifact (`bf208c3…`); it is not the numeric scratch pack. Do not treat the observed binary tuple as a final freeze or launch with it until rebuilt/attested against the final source.

## WP-E5-5/6 evidence disposition

| Slice | Existing evidence | Status for current `2fae166f…` | Remaining action |
|---|---|---|---|
| F-T1 | 6000/8600/9500 and resume attempts use wrong evidence-copy Harness `8cfe679…` / Sim `dcbe7d…`; all terminal, children `-15`, no completed candidate seeds. Frozen 20-seed calibration manifest SHA `ed96e046…`; separate 80-world holdout SHA `c2176556…`. | Nonqualifying; no calibration fit. | After final source/binary freeze and host idle, use guarded runner for 20 calibration seeds, analyze/freeze candidate, then run the untouched 80-world holdout only if fit is accepted. A refit consumes that holdout and requires a new block. |
| F-T2 natural ledger | Initial and resume attempts use wrong Harness/Sim; 16-seed manifest `1b5bcf7…`; terminal/interrupted shards have no valid completed rows. | Nonqualifying. | Exact-binary 16-seed natural ledger after freeze, preserve all 16 seeds, exit/censor/error counts and hashes. |
| F-T4 | Existing data is a targeted/diagnostic cohort, not a current frozen natural acceptance block. | Diagnostic only. | Keep diagnostic; do not promote or infer gate closure from it. |
| F-T6 | Historical natural and calibration/holdout outputs were produced on older aggregate/binaries; no current-source Sim attestation. Descriptive dormant rate 1.46%; broad second-windmill exposure not established. | Historical, not current-candidate evidence. | Current-source guarded calibration/holdout after freeze. No approved dormancy/queue cutoff exists; keep numeric rows unpromoted pending an explicit acceptance decision. |
| F-T7 | Historical holdout reports expected epidemic share 12.04%, seed bootstrap 95% CI `[11.345648%,12.716459%]`, 20/20 exposed; import attempts/blocks/successes `0/0/0`. Source/Sim tuple is older. | Historical, not current-candidate evidence; import mechanic not tested. | Current-source guarded calibration/holdout. Preserve the documented 10–14% and CI-includes-12% rule, but no natural import exposure means a keyed MED-08 import fixture remains separate. |

Confirmed user choices retained: adult-equivalent capacity proxy and allowance for a second windmill. Numeric draft rows remain unpromoted. F-T4 remains diagnostic. The guarded T6/T7 runner requires full expected source/pack/Harness/Sim tuple and pinned manifest SHA, but current frozen binaries have not been verified against current source.

## WP-E5-7 separate preregistration

Seed blocks are in `wp7-calibration-seeds.txt` and `wp7-holdout-seeds.txt`. Calibration has 20 seeds `34000000–34000019`; untouched holdout has 80 seeds `34000020–34000099`; both use the 130-minute horizon established in the trace protocol. Seed-list SHA-256 values:

- Calibration: `443e0c4de1d6f63e23ae4d86c18dfbec9e75d773421e3bac1eb13df4eb48b021`
- Holdout: `80e638045a3457057db028b63206288e233cb23ae3eacaa9f17e7ef6901c0793`

The blocks are mutually disjoint, avoid reserved WP-E5-12 seeds `33000000–33000079`, and the exact 34000000–34000099 range did not occur in the E5 checkout and recognized WP7/E5 scratch trees scanned. Registration JSON SHA-256: `8eb5969be59165dc418c9577e1210991b3dd503fda0ef9a0ae09cb6568ecafe3`.

This is a **seed-block preregistration, not an executable runner manifest**. Its candidate anchor is HEAD `bd0f4b8…` / aggregate `2fae166f…`, pack `f360a3d…`, Harness `aa7b830c…`, Sim `a8cb7666…`; current source-to-binary correspondence is unproven. Convert to the guarded runner JSON only after final source freeze, clean/frozen workspace status, exact numeric pack selection, adjacent Release binary rebuild/verification, and a fresh full-tuple preflight. Capture Sim hash and pre/post source identity in every run record. No seed may be moved into or out of either block after calibration outcomes are viewed; any calibration refit requires a fresh holdout.

Carry the autonomous provisional criteria in `m12-e5-7-measurement-protocol-audit-2026-11-15.md` unchanged and retain their explicit label “not separately user-approved.” They include: GOV-10 predictive interval plus clustered 90% observed/expected ratio CI within 0.80–1.20 and exact 0.006 cohesion decrement; GOV-11 exact charter derivation and paired city-growth multiplier 1.08–1.12 with interval containing 1.10; TRD-08 exact per-claim conservation and ≥20 matured lifecycles across ≥10 worlds for coverage; TRD-09 fitted draft factors with one-minor-unit accounting and transaction-neutral trace controls; DIP-08 predictive interval/clustered ratio CI 0.75–1.25 plus exact eligibility/delay/succession mechanics; SOC-11 exact recipe-stratified fixed-point loss and frozen quality/training effects with trace neutrality. Sparse exposure, insufficient precision, or absent mechanic exposure is inconclusive. No catalog promotion follows from these runs; WP-E5-11's all-50-row release gate and WP-E5-12 human review remain separate.

## Blockers before any qualifying cohort

1. Final clean source freeze and exact current numeric pack identity.
2. Release Harness and adjacent Sim rebuilt/verified against that exact source; current `aa7b/a8cb` binaries are not proven current-source.
3. Host/process capacity must be idle; A5 Warlord FFA was still live at last check (PID 15753).
4. F-T6 needs an explicit dormancy/queue acceptance cutoff if a numeric gate is required; do not invent one.
5. WP7 provisional tolerances remain autonomous working criteria, not separately user-approved. Preserve that status; do not change thresholds after viewing holdout.
6. Instrumentation limits in the protocol remain: paired whole-city GOV-11 outcomes, DIP-08 denominator treatment, paired TRD-09 transaction controls, SOC-11 durability/T12-life effects, and full natural exposure need closure or explicit inconclusive disposition.

At initial preregistration, no build, test, simulation, or source edit occurred. Subsequent trace work is limited to instrumentation and focused tests; it does not qualify as cohort evidence.

## Current-source trace-field check (2026-11-16)

The present dirty checkout contains the opt-in `--fief-wp7-trace` output path. A source inspection confirms these emitted fields and event families:

- `wp7-gov10`: settlement/civ and season identity, eligible check, pre-check disorder, GOV-10 status, keyed hit, event/consequence and cohesion values (from the observer in `PoliticsSystem.FiefLaw.cs`).
- `wp7-gov11`: seed, season, civ, settlement, live marker, current tier/population, GOV-11 completion, derived city eligibility/charter, raw treatment/control multipliers, and a same-state derived ratio. The economy observer now also emits paired control/treatment fertility inputs and conception chances, the same keyed roll, and each counterfactual conception result for each eligible figure attempt (`pairedBasis=same_figure_same_state_same_keyed_roll`). These are paired conception outcomes, not whole City-year population growth outcomes. The boundary row's `growthInput`/`growthOutcome` remain `unavailable`; the ratio is a rule value, not an observed paired growth estimate.
- `wp7-trd08`: claim origination, claim-season food-reserve eligibility, payment with opening/paid/closing values and route-independence, and write-off/expiry lifecycle events, where those events occur.
- `wp7-trd09-admin`, `wp7-trd09-margin`, and `wp7-trd09-transaction`: assigned/actual/released administration work, delivered dispatch-frozen seller value/quantity/payment/margin, and per-good stock deltas. There is no corresponding paired trace-on/off state/offer/terms record.
- `wp7-dip08`: one keyed eligible-claimant check row per candidate, pending start, confirmation/reversal, and leader succession/alliance state. There is no unique settlement-season denominator row; multiple eligible allies can create multiple keyed checks.
- `wp7-soc11` and `wp7-soc11-training`: recipe/input/loss/output/quality/role-work rows and non-recipe role-season assignment/training exposure. Neither paired tool-durability nor T12-life outcomes are emitted.

The harness calls the season trace after each 150-tick advance and prints its buffered rows with the match result. The WP7 path now appends one match terminal row (`chapter_end`, `horizon_censored`, or `run_stopped`) and one final settlement row with population, live status, settlement loss/right-censor status, match status, and end reason. A living settlement is right-censored at either the chapter end or horizon because later trajectory is unobserved; population-zero or ownerless settlements are classified as `settlement_lost`. The trace still does not emit paired whole City-year population outcomes or identify the exact season/cause of a loss between sampled boundaries. No measurement field should be inferred from `unavailable` or from the synthetic same-state ratio.

Remaining cohort prerequisites are: paired same-state GOV-11 whole City-year population outcomes (paired conception outcomes alone do not measure realized city growth) and season-specific loss/terminal labels; a unique DIP-08 eligible settlement-season denominator with claimant-check multiplicity retained separately; paired trace-on/off TRD-09 transaction controls for offers, terms, cargo, payments, and stocks; SOC-11 paired durability and T12-life effects; trajectory-level terminal/right-censor/no-exposure classifications for claims and other event paths; exact frozen source/pack/Harness/Sim provenance; and disjoint calibration/holdout manifests. Both the calibration and untouched holdout cohorts remain open and unrun. Even when the fields close, those cohorts must still meet their pre-registered exposure and precision requirements. No cohort was launched in this trace follow-up.


## Superseding candidate/readiness snapshot — 2026-11-16

The earlier 2fae/7dec candidate identity above is historical. The current external diagnostic candidate is `candidate-5ee659bd`: HEAD `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`, content/src/game aggregate `5ee659bd7303b24190f59ad01299f4d7430e18ce3adb0441470cab96b1452733` over 1,045 files, pack `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`, Release Harness `26fd3451b52bf342c757118324fa2d6dd8b881d4dbda81435438e9285037d100`, Sim `b347189f8d386a4b2a2756271298a8a05e41e7abb82009566ccb8dfa2f631dfe`. The tuple is suitable for the described diagnostics/preflights only; it is not the final WP-E5-11 freeze. F-T1 calibration was active at check time: metadata had 3/20 successful exits and four raw seed files; record only 3/20 complete, with no result estimate. A5's stale 7dec artifacts are preserved, and its 120-seed metadata mismatch is documented. WP7, F-T5, and F-T2/F-T3 artifacts are preflight evidence only. WP-E5-11 remains open: no promotion, all 50 catalog rows draft, ordinary/Release Fief selection closed. WP-E5-12 remains not started: reserved holdout unused; comprehension study and Windows parity outstanding. F-T4 remains diagnostic. No current-source acceptance claim follows from this candidate snapshot.

## Superseding paired-measurement implementation addendum — 2026-11-16

The preceding GOV-11 trace-field limitation is superseded. The dirty target checkout now contains the exact-state paired path: at each natural calendar-year boundary with a live City and GOV-11, the harness copies the complete `MatchModel`, confirms equal 64-bit start checksums, and advances treatment and a restored control branch for four seasons in lockstep. The control suppresses only the Fief City growth multiplier. Complete City-year rows record both population endpoints and eligibility; loss or eligibility changes are emitted as excluded rows, and an incomplete year is censored. The four-season pair tests pass 6/6.

The fail-closed analyzer requires nonempty, valid 16-digit hexadecimal start checksums that match; at least 20 distinct seed clusters with eligible pairs; and, when given the frozen manifest, exactly one terminal row for every expected seed with no unexpected terminal or pair rows. No-exposure seeds remain in cohort accounting. Six analyzer fixtures pass, including empty/malformed checksum and missing-terminal cases. The 20-seed calibration manifest therefore needs exposure in all 20 seeds to clear this minimum; otherwise report inconclusive and preregister a fresh calibration block rather than changing the manifest after results are seen.

No WP7 calibration or holdout match has run. The old candidate pins above remain historical; rebuild and attest source, numeric pack, Release Harness, and Sim from one clean frozen candidate, then preflight the exact seed manifest and use a fresh output root. The D20 acceptance values remain autonomous working criteria, not separately approved, and no catalog row is promoted by this instrumentation or its fixtures.
