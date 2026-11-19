# F-T6 institution-status trace

Use the opt-in `--fief-institution-trace` switch to emit one read-only institution ledger after each completed 150-tick Fief season:

```sh
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll fief-match \
  --pack /path/to/natural-fief.pack --seed "$seed" --minutes 130 \
  --fief-institution-trace
```

`fief-institution-trace` rows contain `season`, `civ`, `settlement`, `institution`, `state`, `builtCount`, `staffedCount`, `dormantCount`, and `reason`. Built rows use `working` or `dormant`; unbuilt BuildOutlook candidates in `Current`, `Short`, or `Waiting` are `queued`, with the BuildStatus, typed BuildCause/BuildWhy, required project, and any short materials/supply type in the reason. Each settlement is sampled after its season has completed. Passive Gothic Cathedrals and Clock Towers are reported as working when built; water/wind mills use the existing power ledger's active source and power-hour state.

The six cohort categories are monastery, university, hospital, Gothic Cathedral, Clock Tower, and mill (watermill and windmill are mill subtypes). The trace producer checks each category at every settlement, emitting a row when it is built or appears in the planner's current/short/waiting set. Every emitted row must have a reason. The F-T6 cohort analyzer requires exactly 20 frozen seeds, a trace and summary in every seed, no duplicate settlement/institution/season rows, all six categories represented across the cohort, and reconciliation of the per-civ-season summaries against staffed trace rows and health-trace civ-seasons. Its completeness status is PASS only with zero unresolved staffing and zero summary mismatches. Dormancy and queue levels are descriptive, not acceptance thresholds. Run `python3 -m unittest docs/MVP/balance/tools/test_fief_t6_t7_cohort.py` for the pure-Python analyzer/runner checks; this does not invoke matches.

The `fief-institution-summary` row is per season and civilization. Its dormant rate is `dormantStaffed / (workingStaffed + dormantStaffed) * 100` for built monasteries, universities, and hospitals. `unresolvedStaffing` is reported separately and excluded from that denominator. This denominator is explicit so passive institutions and ambiguous staffing assignments do not distort the rate.

Monasteries use resident monk presence; hospitals use the existing exact 1-physician/2-attendant staffed predicate. For university reporting, each resident scholar is assigned to at most one institution using a stable priority: reserve one scholar for each built academy and cathedral school, then each monastery when LRN-07 is known, then assign remaining scholars to universities up to their built count. University rows report the assigned count, so multiple universities cannot claim the same scholar; remaining built universities are dormant with the competing-institution reason. This is a diagnostic allocation only and does not change the simulation's role assignment or Insight effects. Clock Tower and Gothic Cathedral have no post-construction staff role to read. Mill statuses report actual active power-source availability; an unresearched windmill reports that NRG-05 is required for wind power.

All rows are diagnostic-only. Without the switch, no institution rows are appended and no simulation state or checksum is changed. No cohort run is part of this instrumentation.
