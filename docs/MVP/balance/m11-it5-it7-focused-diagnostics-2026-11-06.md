# M11 I-T5 / I-T7 focused diagnostics (2026-11-06)

**Status: evidence update only; neither target is passed.** Baseline is `c93de1e` (`codex/it5-it7-siege-governance`). This report preserves the approved targets in 25 §13 and the E3 behavior. It does not establish tolerances where none are approved.

## Reproduction

From the repository root:

```sh
python3 docs/MVP/balance/tools/e4_combat_oracle.py > /tmp/it5-oracle.txt
python3 docs/MVP/balance/tools/e4_combat_oracle.py --check
uv run --no-project --with numpy python docs/MVP/balance/tools/cohesion_sim.py --check > /tmp/it7-cohesion.txt
dotnet test src/CivSim.Tests/CivSim.Tests.csproj --filter 'FullyQualifiedName~IronSiegeTests|FullyQualifiedName~WP8bSiegeLifecycleTests' --logger 'console;verbosity=minimal'
dotnet test src/CivSim.Tests/CivSim.Tests.csproj --no-restore --filter 'FullyQualifiedName~Iron_chapter_cohesion_terms_apply_only_where_their_scope_holds|FullyQualifiedName~An_administrator_and_a_governor_are_staffed_roles|FullyQualifiedName~A_feudal_contract_removes_distance_and_span_strain' --logger 'console;verbosity=minimal'
```

The Python tool's direct `cohesion_sim.py --check` invocation needs NumPy; the `uv` command above supplies it without changing project dependencies. Results on this baseline: oracle check passed; cohesion model check passed; siege/lifecycle focused .NET tests passed **15/15**; governance/cohesion scoped .NET tests passed **3/3**.

## I-T5 siege

The fixed-point combat oracle reproduces its intended stack semantics: wrought iron `2.4 × 1.5`, steel `3.0 × 1.5` with the steel bonus applied once, and professional training replacing ordinary `×1.3`. Its siege sensitivity table gives 12 integrity / 3 damage per season = 4 breach seasons; against the explicitly assumed 10-season investment this is 0.40. Nearby assumptions range from 0.20 (8 / 4) through 1.20 (12 / 1). This is arithmetic on hypothetical inputs, not a duration measurement or acceptance result.

The live controlled lifecycle tests pass, including a 16-seed sample over a generated world and an explicitly constructed besieger/defender state. The fixture adjusts target stores, pins post-ration siege stores, and holds a valid supply route; that is useful for checking lifecycle ordering and fixed-state timing, but does not model matched investment vs engine assault under ordinary economy, relief, repair and attrition. I-T5's required comparison remains open. Its 60% intent has no approved tolerance, so the tests cannot be labeled an I-T5 pass.

Useful existing evidence: `docs/MVP/balance/tools/e4_combat_oracle.py`, `src/CivSim.Tests/IronSiegeTests.cs`, `src/CivSim.Tests/WP8bSiegeLifecycleTests.cs`, and `docs/MVP/balance/m11-wp8-war.md`.

## I-T7 governance and cohesion

The E3 `cohesion_sim.py --check` reproduces the current model's 32 civ layouts and semantic assertions. The nominal modeled case is mean Cohesion 71.1, p10/p90 62/81, 0.000 secessions per civ/E3. R7's one-capture-per-civ stress case is mean 67.9, 6.2% of settlements below 35, and 0.027 secessions per civ/E3; its cascades remain 0.00%. These results are within the model's *existing E3 tool diagnostics* and show crisis secession is possible in the severe capture case. They are neither E4 output nor an approved I-T7 band.

Runtime-focused tests verify that scoped cohesion terms do not apply without their local condition, administrator/governor relief depends on staffed roles, and GOV-09 removes only distance/span pressure in a governed province. They passed 3/3 here. No per-season live E4 settlement cohort or I-T7 acceptance tolerance (“near E3 bands”) is defined. Therefore I-T7 remains untested as a balance target despite these implementation checks and E3-model context.

Useful existing evidence: `docs/MVP/balance/tools/cohesion_sim.py`, `src/CivSim.Tests/IronChainTests.cs`, `docs/MVP/balance/m11-wp7-state-layer.md`, and `docs/MVP/balance/m11-wp2-oracle-fit.md`.

## Remaining evidence needed

- I-T5: define the matched investment reference and compare repeated supplied engine trials on isolated wall/tower/castle cases, then add live relief/attrition and ordinary-economy cases. Record breach/capture outcomes and censoring. The target's 0.40 intent remains unapproved as a tolerance.
- I-T7: define the E3 comparison statistic/window and acceptable distance from its distribution before examining an E4 result. Then record per-season exposures for normal cores, remote provinces and captures, including incomplete exposure, crisis secession and cascades. Do not infer an E4 pass from the E3 simulator or role fixtures.

## Candidate diagnostic contracts (proposal only; no new cutoffs)

These contracts make the next evidence reproducible. They do not resolve the open target definitions, add sample-count requirements, or create acceptance thresholds.

### I-T5 matched siege trials

The current focused tests establish mechanics but do not yet provide the matched comparison dataset. The safe next increment is an opt-in test/diagnostic runner built around the existing prepared siege fixture, with paired arms sharing seed, target settlement, initial wall and defender state, besieger party, route, and opening supply. In the engine arm, record the actual engines taken and each season's supply/engine operation; in the investment reference arm, keep the same besieging party and opening defender state but run the existing investment path without engines. This is a candidate reference definition for review, not an approved interpretation of “matched investment.”

Run explicit wall configurations separately (bare wall, gatehouse/tower, and castle where the fixture can legally construct one) and retain each seed/configuration pair as its own observation. Emit start/breach/end seasons, total seasons, engine-active seasons, engines/crew, opening and seasonal stores/need, attacker and defender casualties, repair events if any, and one terminal disposition: captured, capitulated, relieved, sally resolved, attacker lifted/deserted, or still active at the horizon. A breach that is followed by a failed storm is not a capture. Report medians and ranges by arm/configuration and the paired duration difference/ratio descriptively; censor unresolved cases and show their count. Do not classify “materially beats” or 0.40 until a comparison meaning and tolerance are approved.

After this deterministic fixture screen, the next independent layer is a bounded live-match observation command that logs naturally started investments through their outcome. Use the same outcome/disposition fields plus ordinary supply, relief arrival, attrition/desertion and wall repair. This can measure feasibility and selection bias but is observational: no natural engine assaults means no engine-versus-investment estimate. The existing `IronMatchCommand.cs` is under I-T6 edits, so put this in a separate command/helper or wait until that work is integrated.

### I-T7 per-season cohesion exposure

The current E3 `cohesion_sim.py` is useful model context, but it is not the live runtime baseline and its assumed event rates must not be reused as E4 evidence. A separate, read-only harness trace should emit one row per settlement-season while `CohesionLive` is true, containing seed/mode/civ/settlement, season and match end, population, tier, current Cohesion, target Cohesion, consecutive low-Cohesion seasons, conquered term, exact walking and road-adjusted distance to capital, capital flag, administrator/governor role counts, relevant GOV completions, and owner/free-folk state. Keep these exact distances and flags; do not invent a remote cutoff. Add capture and secession event rows keyed to settlement and tick so a seat's exposure can be separated into never-captured, captured/recovering, and seceded trajectories.

Summarize both settlement-season weighted distributions (N, mean, median, p10/p90, range, and exposure counts below 15 and below 35 as separate diagnostics) and per-settlement trajectories (minimum, seasons below each marker, final value, secession tick or censored end). Below 15 is the current runtime secession floor; below 35 is the existing E3 stress-summary marker, not a runtime crisis trigger. Report secessions and cascades per civ and per exposed settlement, with the exposure denominator and horizon. The current 15-point/eight-season secession rule remains a mechanics diagnostic; it is not an approved “near E3 bands” threshold. Compare Iron and E3 using the same runtime sampling and the same selected seeds/horizon before choosing an acceptance statistic or allowable deviation.

This telemetry is low risk because it reads existing `CivicReadout`, `CohesionStatus`, settlement ownership and politics events; it does not need to alter Cohesion, capture, or secession behavior. One small read-only API addition may be needed: the current `RoleHeads` exposes Priests/Scribes but not Administrator/Governor counts, so expose those staffing counts for the trace rather than infer staffing from research or settlement tier. A bounded first run can use the existing E3 seed/layout corpus in matched E3 and Iron modes and report incomplete exposure explicitly. Do not call that run acceptance-quality until the owner approves the E3 comparison statistic/window and the meaning of “normal core” and “near.”
