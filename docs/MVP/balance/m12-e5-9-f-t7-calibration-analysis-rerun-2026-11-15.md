# F-T7 draft-scale calibration rerun — 2026-11-15

## Calibration decision (not acceptance)

This natural 20-world calibration cohort used the frozen scale `0.34794`, source/content/game aggregate `e6aa4e53775842f86148ce19e4eb2fed1bd468367b35f4f25983801b3fe22a29`, numeric-status pack `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`, and Release harness `aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f`. All 20 seeds completed with zero censors and zero institution-summary mismatches. The runner was resumed after seed 17; it verified the completed raw-file hashes and exact frozen inputs, retained those 17 results, and reran the interrupted seed plus the remaining two.

The observed pooled expected epidemic-hazard share was 14.360695% (seed-bootstrap 95% interval 13.531929–15.163160%), so the `0.34794` hypothesis misses both the proposed 10–14% band and the interval-includes-12% check. Under the same common-scale/fixed-non-epidemic-hazard fit used in the original diagnosis, the calibration estimate gives a new candidate scale `0.282944` for a 12.00% pooled point estimate; transforming the calibration interval projects 11.289512–12.690098%. The source now carries `0.282944` as an unpromoted draft hypothesis. The projected interval is only a fit calculation; use a fresh build, frozen source/pack/harness hashes and the disjoint held-out manifest for independent validation.

The cohort observed zero route-import attempts. MED-08 import behavior therefore remains supported by focused keyed-gate tests rather than natural whole-match exposure; do not claim the natural run validated the import mechanic.

## Reproducibility

- Frozen seeds: 20; manifest SHA-256: `9a675be767becdb8e9cc4511ace1c8be95000ea14ff7a0c844d8064567da836d`
- Natural content pack SHA-256: `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`
- Harness DLL SHA-256: `aa7b830c2ce5847666c67b31dd08150649d4e92cee51c21b6565ef5b290efa3f`
- F-T7 post-own-KEY-5 seed contributions with nonzero expected hazard: 20 / 20
- F-T7 post-own-KEY-5 health rows: 11427
- Censored before own KEY-5: none
- KEY-5 completed but no post-completion season captured: none

## F-T7 health

Rows are filtered with `seasonOffsetFromOwnKey5 >= 0`. The cohort expected epidemic share is pooled as total expected epidemic hazard divided by total expected all-cause hazard over included rows. The 95% interval resamples eligible seeds with replacement, then pools each resample's cause-specific hazard totals. This preserves equal seed sampling while respecting each seed's hazard exposure. Percentiles use linear interpolation over the sorted bootstrap ratios.

- Pooled expected epidemic hazard share: 14.36%
- Seeded-bootstrap 95% interval: [13.531929%, 15.163160%] (`20000` replicates; RNG seed `20261115`; eligible seeds 20)
- Observed epidemic deaths / adult-equivalent person-season: 0.001193 (`29016` deaths / `24330518.00` exposure)
- Observed total deaths / adult-equivalent person-season: 0.008384 (`203991` deaths / `24330518.00` exposure)
- Observed epidemic deaths attributed to active Pox / Plague: 0 / 15635 (unclassified epidemic deaths: 13381)
- MED-08 quarantine civ-seasons: 3591; valid route-import attempts / keyed blocks / accepted imports: 0 / 0 / 0

| Seed | Seasons captured | Post-KEY-5 rows | Expected epidemic | Expected total | Seed share | Epidemic deaths | Total deaths | Adult-equivalent person-seasons |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 21021417 | 2080 | 576 | 1501.450126 | 10623.547045 | 14.133228% | 1445 | 10687 | 1277746.50 |
| 21023198 | 2080 | 587 | 1406.768822 | 10279.628472 | 13.685016% | 1459 | 10243 | 1254289.00 |
| 21027967 | 2080 | 586 | 1723.499770 | 11654.100614 | 14.788784% | 1718 | 11655 | 1341591.50 |
| 21035513 | 2080 | 645 | 1623.023775 | 10170.245656 | 15.958550% | 1692 | 10285 | 1295326.00 |
| 21039090 | 2080 | 618 | 1549.613427 | 9349.677090 | 16.573978% | 1573 | 9379 | 1177853.00 |
| 21046515 | 2080 | 558 | 1251.622774 | 11164.328201 | 11.210910% | 1255 | 11362 | 1316583.50 |
| 21048181 | 2080 | 453 | 1012.971284 | 7624.457015 | 13.285815% | 993 | 7693 | 928202.50 |
| 21048376 | 2080 | 469 | 1469.676784 | 9193.369858 | 15.986268% | 1478 | 9081 | 1123186.00 |
| 21050706 | 2080 | 625 | 1685.515063 | 11111.311786 | 15.169362% | 1706 | 11173 | 1306585.00 |
| 21057285 | 2080 | 536 | 1859.370140 | 12749.220009 | 14.584187% | 1854 | 12621 | 1271493.50 |
| 21058444 | 2080 | 688 | 1868.255728 | 12730.568285 | 14.675352% | 1779 | 12802 | 1464215.00 |
| 21059418 | 2080 | 401 | 561.173018 | 6635.413186 | 8.457243% | 515 | 6634 | 851456.50 |
| 21065015 | 2080 | 564 | 1153.938205 | 8470.529748 | 13.622976% | 1132 | 8261 | 1038850.50 |
| 21065180 | 2080 | 623 | 1518.150108 | 10635.464409 | 14.274413% | 1459 | 10636 | 1297191.50 |
| 21066348 | 2080 | 618 | 1309.456393 | 9976.101484 | 13.125933% | 1347 | 10144 | 1311570.50 |
| 21072301 | 2080 | 503 | 1254.863935 | 9970.850766 | 12.585325% | 1280 | 10168 | 1239420.00 |
| 21080953 | 2080 | 633 | 1381.420189 | 9984.325666 | 13.835889% | 1369 | 10098 | 1260888.00 |
| 21081729 | 2080 | 613 | 1876.921371 | 11601.765710 | 16.177894% | 1773 | 11542 | 1344594.00 |
| 21083123 | 2080 | 553 | 1886.628530 | 10058.033123 | 18.757430% | 1815 | 9822 | 1122850.50 |
| 21086761 | 2080 | 578 | 1370.283175 | 9799.695717 | 13.982916% | 1374 | 9705 | 1106625.00 |

## F-T6 institution status

Counts are institution-seasons, weighted by `builtCount` for built rows; queued rows count one candidate row. The dormant rate covers only built monastery, university, and hospital rows with a known working/dormant state. Unresolved university staffing is excluded from the rate and reported separately.

An institution with no row was neither built nor in the planner's current/short/waiting set; this is outside the requested built-or-queued denominator, not a missing trace record. BuildOutlook `Blocked` and `NotNeeded` items are not classified as queued.

- Working staffed institution-seasons: 24873
- Dormant staffed institution-seasons: 341
- Unresolved staffed institution-seasons: 0
- Known-status staffed denominator: 25214
- Aggregate dormant rate: 1.35%
- Queued institution rows: 207803
- Per-civ-season summary/row consistency mismatches: 0

| Institution | State | Built institution-seasons / queued candidate rows |
| --- | --- | ---: |
| clock_tower | queued | 52618 |
| clock_tower | working | 5546 |
| gothic_cathedral | queued | 3137 |
| hospital | dormant | 161 |
| hospital | queued | 40369 |
| hospital | working | 4723 |
| monastery | dormant | 168 |
| monastery | queued | 32484 |
| monastery | working | 17986 |
| university | dormant | 12 |
| university | queued | 48696 |
| university | working | 2164 |
| windmill | queued | 30499 |
| windmill | working | 4894 |

Unresolved reasons:

| Reason | Institution-seasons |
| --- | ---: |
| (none) | 0 |

## Acceptance and limits

Do not change thresholds in this report. Compare the expected hazard-share estimate and interval with the current proposed 10–14% band and CI-includes-12% gate, while preserving any existing exposure, cohort-size, or censoring requirements. Pox/Plague attribution counts infected epidemic deaths at the season boundary while the settlement has that active outbreak class; other epidemic deaths remain unclassified. Quarantine and import totals include post-own-KEY-5 civ-seasons only. Import attempts count valid route-import calls and reconcile as keyed blocks plus accepted imports; invalid classes or destinations rejected before the gate are excluded.

No outcome-based seed selection was performed; all 20 frozen manifest seeds are retained in this analysis, with post-own-KEY-5 filtering and censoring counts shown above. No D21 threshold proposal is made here.
