# F-T6 / F-T7 natural cohort report — prepared 2026-11-15

## Reproducibility

- Frozen seeds: {{SEED_COUNT}}; manifest SHA-256: `{{MANIFEST_SHA256}}`
- Natural content pack SHA-256: `{{PACK_SHA256}}`
- Harness DLL SHA-256: `{{HARNESS_SHA256}}`
- F-T7 post-own-KEY-5 seed contributions with nonzero expected hazard: {{POST_KEY5_SEEDS}} / {{SEED_COUNT}}
- F-T7 post-own-KEY-5 health rows: {{POST_KEY5_ROWS}}
- Censored before own KEY-5: {{CENSORED_NO_KEY5}}
- KEY-5 completed but no post-completion season captured: {{CENSORED_NO_POST_SEASON}}

## F-T7 health

Rows are filtered with `seasonOffsetFromOwnKey5 >= 0`. The cohort expected epidemic share is pooled as total expected epidemic hazard divided by total expected all-cause hazard over included rows. The 95% interval resamples eligible seeds with replacement, then pools each resample's cause-specific hazard totals. This preserves equal seed sampling while respecting each seed's hazard exposure. Percentiles use linear interpolation over the sorted bootstrap ratios.

- Pooled expected epidemic hazard share: {{EXPECTED_SHARE}}
- Seeded-bootstrap 95% interval: {{EXPECTED_SHARE_CI}} (`{{BOOTSTRAP_REPLICATES}}` replicates; RNG seed `{{BOOTSTRAP_SEED}}`; eligible seeds {{BOOTSTRAP_ELIGIBLE_SEEDS}})
- Observed epidemic deaths / adult-equivalent person-season: {{EPIDEMIC_DEATH_RATE}} (`{{TOTAL_EPIDEMIC_DEATHS}}` deaths / `{{TOTAL_ADULT_EQ}}` exposure)
- Observed total deaths / adult-equivalent person-season: {{TOTAL_DEATH_RATE}} (`{{TOTAL_DEATHS}}` deaths / `{{TOTAL_ADULT_EQ}}` exposure)
- Observed epidemic deaths attributed to active Pox / Plague: {{POX_DEATHS}} / {{PLAGUE_DEATHS}} (unclassified epidemic deaths: {{UNCLASSIFIED_EPIDEMIC_DEATHS}})
- MED-08 quarantine civ-seasons: {{QUARANTINED_CIV_SEASONS}}; valid route-import attempts / keyed blocks / accepted imports: {{IMPORT_ATTEMPTS}} / {{IMPORT_BLOCKED}} / {{IMPORT_SUCCESSES}}

| Seed | Seasons captured | Post-KEY-5 rows | Expected epidemic | Expected total | Seed share | Epidemic deaths | Total deaths | Adult-equivalent person-seasons |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
{{HEALTH_SEED_DETAIL}}

## F-T6 institution status

Counts are institution-seasons, weighted by `builtCount` for built rows; queued rows count one candidate row. The dormant rate covers only built monastery, university, and hospital rows with a known working/dormant state. Unresolved university staffing is excluded from the rate and reported separately. The cohort completeness check requires a reason on every emitted row, no duplicate built/queued identity, and representation of all six requested categories across the fixed 20-seed cohort (monastery, university, hospital, Gothic Cathedral, Clock Tower, and mill; watermill/windmill are mill subtypes). It also reconciles every staffed summary row and requires one summary per civ-season with a staffed trace row. Dormancy and queue counts/rates are descriptive; they are not rejection thresholds.

An institution with no row was neither built nor in the planner's current/short/waiting set; this is outside the requested built-or-queued denominator, not a missing trace record. BuildOutlook `Blocked` and `NotNeeded` items are not classified as queued.

- Working staffed institution-seasons: {{STAFFED_WORKING}}
- Dormant staffed institution-seasons: {{STAFFED_DORMANT}}
- Unresolved staffed institution-seasons: {{STAFFED_UNRESOLVED}}
- Known-status staffed denominator: {{STAFFED_DENOMINATOR}}
- Aggregate dormant rate: {{DORMANT_RATE}}
- Queued institution rows: {{QUEUED_ROWS}}
- Built-or-queued trace rows: {{INSTITUTION_ROWS}}; represented categories: {{INSTITUTION_TYPES}}
- Per-civ-season institution summary rows: {{SUMMARY_ROW_COUNT}}
- Per-civ-season summary/row consistency mismatches: {{SUMMARY_MISMATCHES}}
- F-T6 trace completeness gate (zero unresolved staffing, zero mismatches, all six categories represented): {{INSTITUTION_COVERAGE_STATUS}}

| Institution | State | Built institution-seasons / queued candidate rows |
| --- | --- | ---: |
{{INSTITUTION_DETAIL}}

Unresolved reasons:

| Reason | Institution-seasons |
| --- | ---: |
{{UNRESOLVED_DETAIL}}

## Acceptance and limits

For F-T6 instrumentation acceptance, the cohort covers every row the opt-in trace reports for built/queued institutions with a nonempty reason, reconciles the per-civ-season staffed summaries with zero unresolved staffing, has zero summary mismatches, and represents all six categories across 20 seeds. Dormancy and queue levels remain descriptive. Do not change F-T7 thresholds in this report. Compare the expected hazard-share estimate and interval with the current proposed 10–14% band and CI-includes-12% gate, while preserving any existing exposure, cohort-size, or censoring requirements. Pox/Plague attribution counts infected epidemic deaths at the season boundary while the settlement has that active outbreak class; other epidemic deaths remain unclassified. Quarantine and import totals include post-own-KEY-5 civ-seasons only. Import attempts count valid route-import calls and reconcile as keyed blocks plus accepted imports; invalid classes or destinations rejected before the gate are excluded.

No outcome-based seed selection was performed; all 20 frozen manifest seeds are retained in this analysis, with post-own-KEY-5 filtering and censoring counts shown above. No D21 threshold proposal is made here.
