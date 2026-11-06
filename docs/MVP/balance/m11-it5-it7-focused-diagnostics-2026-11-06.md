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
