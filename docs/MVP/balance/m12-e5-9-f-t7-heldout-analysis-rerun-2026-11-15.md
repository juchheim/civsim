# F-T6 / F-T7 natural cohort report — prepared 2026-11-15

## Reproducibility

- Frozen seeds: 20; manifest SHA-256: `b9a6207bd5d72810ef58559dba063926a45fdc19f095fd65bb539e7cb0ef4a20`
- Natural content pack SHA-256: `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`
- Harness DLL SHA-256: `aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f`
- F-T7 post-own-KEY-5 seed contributions with nonzero expected hazard: 20 / 20
- F-T7 post-own-KEY-5 health rows: 11582
- Censored before own KEY-5: none
- KEY-5 completed but no post-completion season captured: none

## Calibration-to-held-out source relation

The calibration used source aggregate `e6aa4e53775842f86148ce19e4eb2fed1bd468367b35f4f25983801b3fe22a29` with draft scale `0.34794`. The fitted candidate used for this held-out cohort is a later frozen source aggregate, `e42e3605be2de8bd73a4cc049b0219cf92ed0a43994cf11064486958b2cb7144`, carrying scale `0.282944` and its matching focused-test expectation. The aggregate difference is the deliberate calibration-to-candidate change; both cohorts used the same numeric pack (`f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`) and Release harness (`aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f`), and the held-out manifest was disjoint and frozen before execution. The held-out outcomes were not used to tune the candidate.

## F-T7 health

Rows are filtered with `seasonOffsetFromOwnKey5 >= 0`. The cohort expected epidemic share is pooled as total expected epidemic hazard divided by total expected all-cause hazard over included rows. The 95% interval resamples eligible seeds with replacement, then pools each resample's cause-specific hazard totals. This preserves equal seed sampling while respecting each seed's hazard exposure. Percentiles use linear interpolation over the sorted bootstrap ratios.

- Pooled expected epidemic hazard share: 12.04%
- Seeded-bootstrap 95% interval: [11.345648%, 12.716459%] (`20000` replicates; RNG seed `20261115`; eligible seeds 20)
- Observed epidemic deaths / adult-equivalent person-season: 0.000993 (`24901` deaths / `25086053.50` exposure)
- Observed total deaths / adult-equivalent person-season: 0.008217 (`206130` deaths / `25086053.50` exposure)
- Observed epidemic deaths attributed to active Pox / Plague: 0 / 13881 (unclassified epidemic deaths: 11020)
- MED-08 quarantine civ-seasons: 3921; valid route-import attempts / keyed blocks / accepted imports: 0 / 0 / 0

| Seed | Seasons captured | Post-KEY-5 rows | Expected epidemic | Expected total | Seed share | Epidemic deaths | Total deaths | Adult-equivalent person-seasons |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 21100448 | 2080 | 452 | 1041.216780 | 10006.173671 | 10.405744% | 1063 | 9861 | 1200476.50 |
| 21102371 | 2080 | 592 | 1402.752517 | 10774.310140 | 13.019418% | 1375 | 10751 | 1328969.00 |
| 21105959 | 2080 | 551 | 948.089684 | 10067.358239 | 9.417462% | 948 | 10220 | 1272884.00 |
| 21109757 | 2080 | 630 | 1486.316835 | 11272.533415 | 13.185295% | 1456 | 11235 | 1392409.00 |
| 21121649 | 2080 | 607 | 1105.019622 | 8834.554621 | 12.507927% | 1116 | 8929 | 1142462.00 |
| 21126706 | 2080 | 634 | 1274.172823 | 10175.827262 | 12.521565% | 1304 | 10232 | 1253806.00 |
| 21128485 | 2080 | 486 | 1008.662498 | 7987.128792 | 12.628599% | 1037 | 8062 | 983390.50 |
| 21129504 | 2080 | 593 | 1304.936013 | 10898.964850 | 11.973027% | 1239 | 10885 | 1268462.00 |
| 21130044 | 2080 | 651 | 1173.647853 | 11055.956972 | 10.615525% | 1180 | 10894 | 1391851.00 |
| 21141430 | 2080 | 633 | 1332.626983 | 10120.006310 | 13.168243% | 1363 | 10221 | 1275597.50 |
| 21144942 | 2080 | 625 | 1350.455399 | 11878.458711 | 11.368945% | 1295 | 12036 | 1435286.50 |
| 21149977 | 2080 | 594 | 1237.415077 | 10466.122614 | 11.823052% | 1263 | 10561 | 1228833.50 |
| 21155510 | 2080 | 591 | 1506.606819 | 10331.494947 | 14.582660% | 1511 | 10269 | 1259761.00 |
| 21164838 | 2080 | 579 | 1285.388241 | 10128.630465 | 12.690642% | 1341 | 10350 | 1228953.50 |
| 21171259 | 2080 | 584 | 1114.175975 | 11243.017584 | 9.909937% | 1126 | 11264 | 1302038.00 |
| 21186001 | 2080 | 560 | 1376.635987 | 8911.716470 | 15.447484% | 1354 | 8910 | 1116648.00 |
| 21188762 | 2080 | 628 | 1527.526849 | 12136.994972 | 12.585709% | 1588 | 12149 | 1411820.00 |
| 21191972 | 2080 | 412 | 808.796692 | 9043.225755 | 8.943675% | 801 | 9002 | 1162370.50 |
| 21192182 | 2080 | 610 | 1517.625939 | 11514.618469 | 13.179993% | 1582 | 11626 | 1305242.50 |
| 21198641 | 2080 | 570 | 930.709613 | 8635.781527 | 10.777364% | 959 | 8673 | 1124792.50 |

## F-T6 institution status

Counts are institution-seasons, weighted by `builtCount` for built rows; queued rows count one candidate row. The dormant rate covers only built monastery, university, and hospital rows with a known working/dormant state. Unresolved university staffing is excluded from the rate and reported separately.

An institution with no row was neither built nor in the planner's current/short/waiting set; this is outside the requested built-or-queued denominator, not a missing trace record. BuildOutlook `Blocked` and `NotNeeded` items are not classified as queued.

- Working staffed institution-seasons: 23845
- Dormant staffed institution-seasons: 354
- Unresolved staffed institution-seasons: 0
- Known-status staffed denominator: 24199
- Aggregate dormant rate: 1.46%
- Queued institution rows: 217169
- Per-civ-season summary/row consistency mismatches: 0

| Institution | State | Built institution-seasons / queued candidate rows |
| --- | --- | ---: |
| clock_tower | queued | 53105 |
| clock_tower | working | 4055 |
| gothic_cathedral | queued | 3440 |
| hospital | dormant | 173 |
| hospital | queued | 43145 |
| hospital | working | 3710 |
| monastery | dormant | 175 |
| monastery | queued | 32677 |
| monastery | working | 19216 |
| university | dormant | 6 |
| university | queued | 52046 |
| university | working | 919 |
| windmill | queued | 32756 |
| windmill | working | 4178 |

Unresolved reasons:

| Reason | Institution-seasons |
| --- | ---: |
| (none) | 0 |

## Acceptance and limits

Do not change thresholds in this report. Compare the expected hazard-share estimate and interval with the current proposed 10–14% band and CI-includes-12% gate, while preserving any existing exposure, cohort-size, or censoring requirements. Pox/Plague attribution counts infected epidemic deaths at the season boundary while the settlement has that active outbreak class; other epidemic deaths remain unclassified. Quarantine and import totals include post-own-KEY-5 civ-seasons only. Import attempts count valid route-import calls and reconcile as keyed blocks plus accepted imports; invalid classes or destinations rejected before the gate are excluded.

No outcome-based seed selection was performed; all 20 frozen manifest seeds are retained in this analysis, with post-own-KEY-5 filtering and censoring counts shown above. No D21 threshold proposal is made here.
