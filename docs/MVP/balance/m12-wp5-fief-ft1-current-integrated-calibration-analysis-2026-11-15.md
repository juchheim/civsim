# F-T1 calibration cohort analysis

> Calibration only; not the frozen acceptance holdout and not F-T1 closure. Incomplete KEY-6 intervals are not imputed.

## Provenance

- Frozen manifest SHA-256: `ed96e046472d8fc84bb9251cd9ccd62c10e7e937978f52d7b27339af00c1b19e` (20 seeds).
- Pack SHA-256: `994fb7801aca466b0da3d9dabfab3cc90fb024a029100bcb50f76393fbe9812f`; E5 base cost source/pack `6000/6000` (canonical record SHA-256 `85180312495f72ce543a2087b76219bacb96c5ed9ae4e1424a8281bfef7ff92a` / `85180312495f72ce543a2087b76219bacb96c5ed9ae4e1424a8281bfef7ff92a`).
- Release harness SHA-256: `27a9d425047af6c7f1cfb85a42b714145b2449ce1d81182bfeff3816de390ccb`; era registry SHA-256: `940293b45b531f91555e47d590348f66adcefd1d6fffbba4659651c298b407a9`.
- Runner completed `20/20` seeds, each exit code 0, at the 130-minute horizon with natural Fief progression.
- Combined raw-output SHA-256: `93e38e6b36f83a238c4dfefc6593512502af78fb49288af20cf44544cb90da67`.

## Summary

- Seats: 80 total; 77 completed KEY-6; 3 right-censored after KEY-5; 0 did not reach KEY-5.
- Completed-seat KEY-5→KEY-6 interval (minutes): median 17.70; p10/p25/p75/p90 16.00/16.60/18.90/20.10.
- Quantiles use completed seats only. Censored and no-KEY-5 seats remain in the completion counts and are not assigned interval values.

## Every seat

| Seed | Civ | Archetype | KEY-5 minute | KEY-6 minute | KEY-5→KEY-6 minutes | State | Observed after KEY-5 | E5 projects |
| ---: | ---: | --- | ---: | ---: | ---: | --- | ---: | ---: |
| 20500859 | 0 | Builder | 106.7 | 122.6 | 15.9 | complete | 15.9 | 49 |
| 20500859 | 1 | Grower | 97.5 | 115.2 | 17.7 | complete | 17.7 | 49 |
| 20500859 | 2 | Warlord | 116.3 | — | — | right_censored_after_KEY5 | 13.7 | 16 |
| 20500859 | 3 | Sage | 97.0 | 114.2 | 17.1 | complete | 17.1 | 49 |
| 20501724 | 0 | Builder | 104.2 | 124.7 | 20.4 | complete | 20.4 | 48 |
| 20501724 | 1 | Grower | 99.5 | 117.9 | 18.4 | complete | 18.4 | 49 |
| 20501724 | 2 | Warlord | 98.7 | 117.1 | 18.4 | complete | 18.4 | 49 |
| 20501724 | 3 | Sage | 98.2 | 121.4 | 23.2 | complete | 23.2 | 49 |
| 20524750 | 0 | Builder | 91.3 | 107.7 | 16.4 | complete | 16.4 | 49 |
| 20524750 | 1 | Grower | 88.2 | 107.7 | 19.5 | complete | 19.5 | 49 |
| 20524750 | 2 | Warlord | 100.0 | 114.5 | 14.5 | complete | 14.5 | 49 |
| 20524750 | 3 | Sage | 86.5 | 103.7 | 17.1 | complete | 17.1 | 49 |
| 20527573 | 0 | Builder | 87.7 | 105.4 | 17.7 | complete | 17.7 | 49 |
| 20527573 | 1 | Grower | 97.1 | 121.5 | 24.4 | complete | 24.4 | 49 |
| 20527573 | 2 | Warlord | 103.3 | 121.4 | 18.1 | complete | 18.1 | 49 |
| 20527573 | 3 | Sage | 100.3 | 115.9 | 15.6 | complete | 15.6 | 49 |
| 20528045 | 0 | Builder | 95.5 | 114.0 | 18.6 | complete | 18.6 | 49 |
| 20528045 | 1 | Grower | 96.2 | 114.1 | 17.9 | complete | 17.9 | 49 |
| 20528045 | 2 | Warlord | 98.6 | 116.0 | 17.3 | complete | 17.3 | 49 |
| 20528045 | 3 | Sage | 89.7 | 105.9 | 16.2 | complete | 16.2 | 49 |
| 20542167 | 0 | Builder | 108.3 | 124.8 | 16.6 | complete | 16.6 | 46 |
| 20542167 | 1 | Grower | 87.8 | 104.0 | 16.3 | complete | 16.3 | 49 |
| 20542167 | 2 | Warlord | 96.4 | 113.0 | 16.6 | complete | 16.6 | 49 |
| 20542167 | 3 | Sage | 126.7 | — | — | right_censored_after_KEY5 | 3.3 | 0 |
| 20544215 | 0 | Builder | 97.0 | 116.2 | 19.2 | complete | 19.2 | 49 |
| 20544215 | 1 | Grower | 86.7 | 104.1 | 17.5 | complete | 17.5 | 49 |
| 20544215 | 2 | Warlord | 110.5 | 124.4 | 13.9 | complete | 13.9 | 46 |
| 20544215 | 3 | Sage | 103.5 | 124.1 | 20.6 | complete | 20.6 | 45 |
| 20547720 | 0 | Builder | 94.6 | 112.0 | 17.4 | complete | 17.4 | 49 |
| 20547720 | 1 | Grower | 94.9 | 112.5 | 17.6 | complete | 17.6 | 49 |
| 20547720 | 2 | Warlord | 98.4 | 117.3 | 18.9 | complete | 18.9 | 49 |
| 20547720 | 3 | Sage | 89.3 | 107.7 | 18.4 | complete | 18.4 | 49 |
| 20553022 | 0 | Builder | 93.7 | 111.8 | 18.1 | complete | 18.1 | 49 |
| 20553022 | 1 | Grower | 93.7 | 110.6 | 16.9 | complete | 16.9 | 49 |
| 20553022 | 2 | Warlord | 92.6 | 109.1 | 16.4 | complete | 16.4 | 49 |
| 20553022 | 3 | Sage | 89.4 | 109.0 | 19.6 | complete | 19.6 | 49 |
| 20565793 | 0 | Builder | 86.7 | 104.8 | 18.1 | complete | 18.1 | 49 |
| 20565793 | 1 | Grower | 95.1 | 112.9 | 17.8 | complete | 17.8 | 49 |
| 20565793 | 2 | Warlord | 100.9 | 118.1 | 17.3 | complete | 17.3 | 49 |
| 20565793 | 3 | Sage | 90.4 | 107.5 | 17.1 | complete | 17.1 | 49 |
| 20566091 | 0 | Builder | 104.0 | 120.0 | 16.0 | complete | 16.0 | 49 |
| 20566091 | 1 | Grower | 95.4 | 112.7 | 17.3 | complete | 17.3 | 49 |
| 20566091 | 2 | Warlord | 98.9 | 117.0 | 18.1 | complete | 18.1 | 49 |
| 20566091 | 3 | Sage | 88.0 | 104.4 | 16.4 | complete | 16.4 | 49 |
| 20566313 | 0 | Builder | 97.9 | 117.0 | 19.1 | complete | 19.1 | 49 |
| 20566313 | 1 | Grower | 99.3 | 120.4 | 21.1 | complete | 21.1 | 49 |
| 20566313 | 2 | Warlord | 102.2 | 118.2 | 16.0 | complete | 16.0 | 49 |
| 20566313 | 3 | Sage | 92.0 | 109.3 | 17.3 | complete | 17.3 | 49 |
| 20568340 | 0 | Builder | 89.7 | 106.8 | 17.1 | complete | 17.1 | 49 |
| 20568340 | 1 | Grower | 95.2 | 114.0 | 18.9 | complete | 18.9 | 49 |
| 20568340 | 2 | Warlord | 99.6 | 115.1 | 15.5 | complete | 15.5 | 49 |
| 20568340 | 3 | Sage | 93.6 | 114.0 | 20.4 | complete | 20.4 | 49 |
| 20575287 | 0 | Builder | 85.5 | 105.2 | 19.7 | complete | 19.7 | 49 |
| 20575287 | 1 | Grower | 119.1 | — | — | right_censored_after_KEY5 | 10.9 | 4 |
| 20575287 | 2 | Warlord | 95.2 | 114.1 | 18.9 | complete | 18.9 | 49 |
| 20575287 | 3 | Sage | 96.4 | 115.5 | 19.1 | complete | 19.1 | 49 |
| 20575326 | 0 | Builder | 90.4 | 108.1 | 17.6 | complete | 17.6 | 46 |
| 20575326 | 1 | Grower | 88.0 | 106.0 | 18.0 | complete | 18.0 | 46 |
| 20575326 | 2 | Warlord | 97.6 | 113.9 | 16.4 | complete | 16.4 | 46 |
| 20575326 | 3 | Sage | 86.8 | 105.9 | 19.1 | complete | 19.1 | 46 |
| 20582332 | 0 | Builder | 85.4 | 101.5 | 16.1 | complete | 16.1 | 49 |
| 20582332 | 1 | Grower | 90.1 | 107.8 | 17.7 | complete | 17.7 | 49 |
| 20582332 | 2 | Warlord | 94.6 | 111.5 | 16.9 | complete | 16.9 | 49 |
| 20582332 | 3 | Sage | 92.2 | 109.2 | 17.0 | complete | 17.0 | 49 |
| 20583288 | 0 | Builder | 88.9 | 105.1 | 16.2 | complete | 16.2 | 49 |
| 20583288 | 1 | Grower | 94.1 | 117.6 | 23.6 | complete | 23.6 | 49 |
| 20583288 | 2 | Warlord | 96.6 | 112.2 | 15.7 | complete | 15.7 | 49 |
| 20583288 | 3 | Sage | 93.2 | 113.6 | 20.4 | complete | 20.4 | 49 |
| 20584815 | 0 | Builder | 87.5 | 106.2 | 18.7 | complete | 18.7 | 46 |
| 20584815 | 1 | Grower | 98.7 | 116.2 | 17.5 | complete | 17.5 | 46 |
| 20584815 | 2 | Warlord | 105.0 | 122.1 | 17.1 | complete | 17.1 | 46 |
| 20584815 | 3 | Sage | 98.7 | 113.0 | 14.3 | complete | 14.3 | 46 |
| 20590125 | 0 | Builder | 86.6 | 106.6 | 19.9 | complete | 19.9 | 49 |
| 20590125 | 1 | Grower | 87.0 | 105.8 | 18.8 | complete | 18.8 | 49 |
| 20590125 | 2 | Warlord | 97.1 | 115.0 | 17.9 | complete | 17.9 | 49 |
| 20590125 | 3 | Sage | 98.5 | 115.0 | 16.5 | complete | 16.5 | 49 |
| 20599629 | 0 | Builder | 90.9 | 110.1 | 19.2 | complete | 19.2 | 49 |
| 20599629 | 1 | Grower | 95.5 | 112.0 | 16.5 | complete | 16.5 | 49 |
| 20599629 | 2 | Warlord | 91.5 | 110.8 | 19.3 | complete | 19.3 | 49 |
| 20599629 | 3 | Sage | 92.5 | 111.3 | 18.9 | complete | 18.9 | 49 |
