# F-T6 / F-T7 natural cohort report — prepared 2026-11-15

## Reproducibility

- Frozen seeds: 20; manifest SHA-256: `a92dd049c1c56836dd2bffad3106985694be2eb755f15b4665c0937ae318f038`
- Natural content pack SHA-256: `994fb7801aca466b0da3d9dabfab3cc90fb024a029100bcb50f76393fbe9812f`
- Harness DLL SHA-256: `27a9d425047af6c7f1cfb85a42b714145b2449ce1d81182bfeff3816de390ccb`
- F-T7 post-own-KEY-5 seed contributions with nonzero expected hazard: 20 / 20
- F-T7 post-own-KEY-5 health rows: 10492
- Censored before own KEY-5: none
- KEY-5 completed but no post-completion season captured: none

## F-T7 health

Rows are filtered with `seasonOffsetFromOwnKey5 >= 0`. The cohort expected epidemic share is pooled as total expected epidemic hazard divided by total expected all-cause hazard over included rows. The 95% interval resamples eligible seeds with replacement, then pools each resample's cause-specific hazard totals. This preserves equal seed sampling while respecting each seed's hazard exposure. Percentiles use linear interpolation over the sorted bootstrap ratios.

- Pooled expected epidemic hazard share: 28.16%
- Seeded-bootstrap 95% interval: [26.509932%, 29.545495%] (`20000` replicates; RNG seed `20261115`; eligible seeds 20)
- Observed epidemic deaths / adult-equivalent person-season: 0.002916 (`56269` deaths / `19293902.00` exposure)
- Observed total deaths / adult-equivalent person-season: 0.010364 (`199968` deaths / `19293902.00` exposure)
- Observed epidemic deaths attributed to active Pox / Plague: 0 / 27846 (unclassified epidemic deaths: 28423)
- MED-08 quarantine civ-seasons: 3742; valid route-import attempts / keyed blocks / accepted imports: 0 / 0 / 0

| Seed | Seasons captured | Post-KEY-5 rows | Expected epidemic | Expected total | Seed share | Epidemic deaths | Total deaths | Adult-equivalent person-seasons |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 20703321 | 2080 | 598 | 3202.892943 | 11332.426993 | 28.263080% | 3202 | 11356 | 1116063.00 |
| 20706066 | 2080 | 657 | 3695.290217 | 12126.704656 | 30.472336% | 3725 | 12295 | 1191348.00 |
| 20712445 | 2080 | 619 | 2901.830249 | 9562.532728 | 30.345833% | 2913 | 9458 | 925075.00 |
| 20721164 | 2080 | 436 | 2041.898481 | 9569.239934 | 21.338147% | 2069 | 9469 | 1081152.00 |
| 20723249 | 2080 | 570 | 2705.112588 | 9404.895176 | 28.762815% | 2659 | 9287 | 955678.50 |
| 20725212 | 2080 | 441 | 3048.119428 | 9951.749938 | 30.628979% | 3110 | 9904 | 860527.50 |
| 20731432 | 2080 | 531 | 2714.968132 | 8456.034195 | 32.106873% | 2728 | 8419 | 780338.00 |
| 20739431 | 2080 | 452 | 2381.806677 | 9644.198765 | 24.696781% | 2337 | 9601 | 924359.50 |
| 20746087 | 2080 | 537 | 2952.412514 | 9490.844694 | 31.108006% | 2876 | 9461 | 890808.50 |
| 20759281 | 2080 | 579 | 3331.308118 | 11573.236163 | 28.784586% | 3343 | 11711 | 1173985.50 |
| 20762350 | 2080 | 493 | 1359.400757 | 7216.173812 | 18.838249% | 1332 | 7229 | 779855.00 |
| 20767669 | 2080 | 669 | 4397.658615 | 15235.731505 | 28.864112% | 4425 | 15220 | 1301729.00 |
| 20779299 | 2080 | 543 | 3312.428112 | 9908.990453 | 33.428512% | 3270 | 9920 | 891850.00 |
| 20782415 | 2080 | 621 | 3471.189683 | 11711.887706 | 29.638174% | 3405 | 11678 | 1167928.50 |
| 20786650 | 2080 | 496 | 2564.685840 | 9193.493462 | 27.896749% | 2626 | 9201 | 865670.00 |
| 20791412 | 2080 | 528 | 2931.981283 | 9961.434193 | 29.433325% | 2870 | 9846 | 922178.50 |
| 20791923 | 2080 | 323 | 2470.163892 | 10019.645486 | 24.653206% | 2493 | 10121 | 915399.00 |
| 20792058 | 2080 | 568 | 3009.495347 | 10053.051791 | 29.936137% | 3042 | 10047 | 1034978.00 |
| 20794130 | 2080 | 300 | 1101.769857 | 6381.487451 | 17.265095% | 1096 | 6403 | 645583.50 |
| 20797132 | 2080 | 531 | 2723.792805 | 9221.688340 | 29.536813% | 2748 | 9342 | 869395.00 |

## F-T6 institution status

Counts are institution-seasons, weighted by `builtCount` for built rows; queued rows count one candidate row. The dormant rate covers only built monastery, university, and hospital rows with a known working/dormant state. Unresolved university staffing is excluded from the rate and reported separately.

An institution with no row was neither built nor in the planner's current/short/waiting set; this is outside the requested built-or-queued denominator, not a missing trace record. BuildOutlook `Blocked` and `NotNeeded` items are not classified as queued.

- Working staffed institution-seasons: 27034
- Dormant staffed institution-seasons: 375
- Unresolved staffed institution-seasons: 0
- Known-status staffed denominator: 27409
- Aggregate dormant rate: 1.37%
- Queued institution rows: 190912
- Per-civ-season summary/row consistency mismatches: 0

| Institution | State | Built institution-seasons / queued candidate rows |
| --- | --- | ---: |
| clock_tower | queued | 45337 |
| clock_tower | working | 3237 |
| gothic_cathedral | queued | 3658 |
| hospital | dormant | 167 |
| hospital | queued | 36391 |
| hospital | working | 4096 |
| monastery | dormant | 192 |
| monastery | queued | 23436 |
| monastery | working | 20146 |
| university | dormant | 16 |
| university | queued | 54081 |
| university | working | 2792 |
| windmill | queued | 28009 |
| windmill | working | 5749 |

Unresolved reasons:

| Reason | Institution-seasons |
| --- | ---: |
| (none) | 0 |

## Acceptance and limits

Do not change thresholds in this report. Compare the expected hazard-share estimate and interval with the current proposed 10–14% band and CI-includes-12% gate, while preserving any existing exposure, cohort-size, or censoring requirements. Pox/Plague attribution counts infected epidemic deaths at the season boundary while the settlement has that active outbreak class; other epidemic deaths remain unclassified. Quarantine and import totals include post-own-KEY-5 civ-seasons only. Import attempts count valid route-import calls and reconcile as keyed blocks plus accepted imports; invalid classes or destinations rejected before the gate are excluded.

No outcome-based seed selection was performed; all 20 frozen manifest seeds are retained in this analysis, with post-own-KEY-5 filtering and censoring counts shown above. No D21 threshold proposal is made here.
