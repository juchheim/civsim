# WP-E5-8 closure readiness (2026-11-15)

## Status

WP-E5-8 is implemented as a set of draft mechanics, with focused regression evidence for siege plumbing, ship cargo limits, sailing speed, and Bronze/Iron isolation. It is **not closed**: existing natural siege evidence has no sustained siege investment, SEA-10 whole-match throughput has not been demonstrated, and no natural whole-match balance cohort establishes acquisition, use, attrition, capture, or relief rates. All E5 numeric rows remain draft.

## Current candidate identity

Identity was rechecked read-only while preparing this report. The working tree remains based on HEAD `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`; the canonical `hash_e5_workspace.py` calculation over `content/`, `src/`, and `game/` yields aggregate SHA-256 `7decf87ac45f924efeedb8fb28cd87c8f8d0476b93b438344c931c07fa4947d6` (1,040 files). The numeric scratch pack is `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`; Release Harness DLL is `aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f`; adjacent Release Sim DLL is `a8cb7666042cf6651a6b9558b3d6db7b1f00566b5cc30041077e67b86540ad50`. The Tests output copy of `CivSim.Sim.dll` has the same `a8cb…` hash.

These are the current candidate fingerprints, not the provenance of the older WP-E5-8 cohort below. The source-aggregate JSON snapshot `m12-e5-9-fief-ai-source-aggregate-2026-11-15.json` currently records a different aggregate (`1fa84935…`); it is stale relative to the canonical hash calculation above and must not be cited as current provenance. No build or test was run for this report.

## Mechanic rules and fixture evidence

### Siege, cannon, powder, and crew

The confirmed D17 rule is four adults per attacking engine/cannon, a seeded **1% per assigned adult per siege turn** loss chance, increased to **1.25%** against a garrisoned concentric castle under DEF-09; open-ocean crossings remain closed during E5 and unlock after the civilization's own KEY-6. These are confirmed rules for the draft implementation, not numeric balance approval. `Sieges.BaseSiegeCrewAttrition` is 1/100, and `ConcentricCrewAttritionMultiplier` is 125/100; the keyed per-person roll is in `src/CivSim.Sim/Politics/Raids.cs`. `PoliticsSystem.Sieges.cs` applies it to assigned engine/cannon crews and records diagnostic counts. The garrisoned DEF-09 defense multiplier is the existing castle strongpoint ×1.25 times the concentric upgrade ×1.5 = ×1.875.

`FiefSiegeBalanceExposureTests` cover supplied engine and cannon trials, paid powder and wall breach, conserved powder on relief, snapshot continuation, and terminal trace plumbing. `FiefSiegeWeaponTests` cover the powder-breach wall-only modifier and keyed casualty-rate mechanics. The 100,000-person-turn test observed 1,006 losses at 1% (95% Wilson interval 0.945–1.071%) and 1,244 at 1.25% (1.176–1.315%). This validates the keyed roll implementation, not ordinary-match exposure or balance. Standing stone walls remain unassaultable before a breach; cannon breach requires a paid shot, and each shot consumes 0.25 L powder.

### Sea rules and fixture evidence

`PoliticsSystem.Voyage.CanSail` requires SEA-04 generally and, in Fief, additionally requires that civilization's KEY-6. The closed-sea rule is therefore enforced in the route/travel eligibility path. SEA-10 cargo capacity is 12 L for the plank-boat baseline and 24 L after SEA-10, only for shipments that actually cross water; land routes are uncapped by this modifier. `FiefShipCargoTests` cover 12/24 L, route eligibility, repeat shipments, delivery payment, Fief-only behavior, and land-route isolation. `FiefSeaSpeedTests` cover the Fief sea-speed combination and land/Iron isolation. These are fixture-level plumbing checks.

## Natural evidence and provenance limits

The frozen 20-seed natural siege trace is at `m12-e5-8-f-t5-natural-output-2026-11-15/`, summarized by `m12-e5-8-f-t5-natural-exposure-diagnostic-2026-11-15.md`; its manifest is `m12-e5-8-f-t5-natural-seeds-2026-11-15.txt` (SHA-256 `f787766662b46319f7f0807eacd27592aa8c2af4163f366d52ddec07a3e3a786`). All 20 130-minute runs exited 0, but observed zero investment-seasons, cannon shots, powder payment, breaches, assigned crew-seasons, or DEF-09 active observations. Terminal exposure was `none` for all 20. The records contain terminal assault/capture events, but unavailable siege investment detail; they cannot be interpreted as siege exposure.

Crucially, this cohort was run against an earlier snapshot: aggregate `87996d7a3751c48ecdf40be72c83f903dae45c1370ec383f33cb46d160476857`, pack `994fb7801aca466b0da3d9dabfab3cc90fb024a029100bcb50f76393fbe9812f`, Harness `27a9d425047af6c7f1cfb85a42b714145b2449ce1d81182bfeff3816de390ccb`, and Sim `fc7959501299122543905e790b2294d5306f7b3d4d3f723f0e40b9bf13010063`. It remains a useful diagnostic of missing natural exposure, but it is **not current-candidate acceptance evidence**.

Controlled F-T5 tests prove mechanic wiring and conservation but use supplied engines/cannons, powder, force, and targets. The isolated WP-E5-8 scratch output is also not acceptance evidence: its loaded Sim hash was `2e6e031c…`, while the current candidate Sim is `a8cb7666…`; its earlier SEA-10 rows had zero sea and walk dispatches in all four rows. The subsequent fixture repair and smoke must be provenance-audited after completion; any outcome from that mismatched Sim remains provisional regardless of reported pass status.

## Minimum remaining gates

1. On the current frozen candidate, obtain an isolated natural Fief cohort with traceable acquisition and sustained availability of engines/cannons/powder; record siege-turn crew exposure in both base and DEF-09 strata, shots and powder conservation, breach state, capture, relief, and right-censor reasons. Retain all seeds, including no-exposure seeds; do not select seeds after outcomes.
2. Establish enough assigned-adult-turn exposure in both attrition strata to interpret the confirmed 1% and 1.25% roll rates. Fixture Monte Carlo is a mechanics check; it does not replace natural exposure.
3. Preserve whole-match SEA-10 evidence with positive dispatch and cargo movement on actual water routes, showing the 12/24 L cap, subsequent shipments of surplus, and delivered payment. The current zero-dispatch fixture rows do not pass this gate.
4. Verify the closed-sea invariant before own KEY-6 and route accessibility after KEY-6 in source-matched tests or traceable runs. Keep all results bound to a pre-run source aggregate, pack, Harness, and adjacent Sim hash; no stale or wrong-binary result closes a gate.
5. Review siege/castle and sea-throughput balance against explicitly accepted thresholds in the broader WP-E5-11 process. The existing F-T5 documents specify observations and exposure requirements but do not establish final numeric acceptance bands. Do not promote E5 catalog rows or refresh goldens until the applicable gates and approvals are complete.

Current D20, F-T1/F-T2, and isolated WP-E5-8 workloads were active during this audit. No new simulation, build, or test was launched.
