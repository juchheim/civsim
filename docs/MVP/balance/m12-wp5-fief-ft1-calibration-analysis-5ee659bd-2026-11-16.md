# F-T1 calibration cohort analysis

> Calibration only; not the frozen acceptance holdout and not F-T1 closure. Incomplete KEY-6 intervals are not imputed.

## Provenance

- Frozen manifest SHA-256: `ed96e046472d8fc84bb9251cd9ccd62c10e7e937978f52d7b27339af00c1b19e` (20 seeds).
- Pack SHA-256: `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`; E5 base cost source/pack `6000/6000` (canonical record SHA-256 `85180312495f72ce543a2087b76219bacb96c5ed9ae4e1424a8281bfef7ff92a` / `85180312495f72ce543a2087b76219bacb96c5ed9ae4e1424a8281bfef7ff92a`).
- Release harness SHA-256: `26fd3451b52bf342c757118324fa2d6dd8b881d4dbda81435438e9285037d100`; era registry SHA-256: `940293b45b531f91555e47d590348f66adcefd1d6fffbba4659651c298b407a9`.
- Runner completed `20/20` seeds, each exit code 0, at the 130-minute horizon with natural Fief progression.

## Summary

- Seats: 80 total; 73 completed KEY-6; 6 right-censored after KEY-5; 1 did not reach KEY-5.
- Completed-seat KEY-5→KEY-6 interval (minutes): median 22.30; p10/p25/p75/p90 19.52/20.10/23.70/25.38.
- Quantiles use completed seats only. Censored and no-KEY-5 seats remain in the completion counts and are not assigned interval values.

## Every seat

| Seed | Civ | Archetype | KEY-5 minute | KEY-6 minute | KEY-5→KEY-6 minutes | State | Observed after KEY-5 | E5 projects |
| ---: | ---: | --- | ---: | ---: | ---: | --- | ---: | ---: |
| 20500859 | 0 | Builder | 102.0 | 125.5 | 23.5 | complete | 23.5 | 49 |
| 20500859 | 1 | Grower | 98.3 | 118.4 | 20.1 | complete | 20.1 | 49 |
| 20500859 | 2 | Warlord | — | — | — | KEY5_not_reached | — | 0 |
| 20500859 | 3 | Sage | 94.2 | 113.9 | 19.7 | complete | 19.7 | 49 |
| 20501724 | 0 | Builder | 102.1 | 124.1 | 22.0 | complete | 22.0 | 49 |
| 20501724 | 1 | Grower | 99.4 | 121.8 | 22.3 | complete | 22.3 | 49 |
| 20501724 | 2 | Warlord | 95.3 | 121.7 | 26.3 | complete | 26.3 | 49 |
| 20501724 | 3 | Sage | 91.4 | 112.8 | 21.4 | complete | 21.4 | 49 |
| 20524750 | 0 | Builder | 90.2 | 116.2 | 26.0 | complete | 26.0 | 49 |
| 20524750 | 1 | Grower | 88.1 | 108.3 | 20.1 | complete | 20.1 | 49 |
| 20524750 | 2 | Warlord | 101.0 | 126.9 | 25.9 | complete | 25.9 | 49 |
| 20524750 | 3 | Sage | 87.5 | 110.0 | 22.5 | complete | 22.5 | 49 |
| 20527573 | 0 | Builder | 86.2 | 110.8 | 24.6 | complete | 24.6 | 49 |
| 20527573 | 1 | Grower | 93.1 | 116.4 | 23.3 | complete | 23.3 | 49 |
| 20527573 | 2 | Warlord | 104.7 | 128.9 | 24.2 | complete | 24.2 | 48 |
| 20527573 | 3 | Sage | 101.7 | 120.7 | 19.1 | complete | 19.1 | 49 |
| 20528045 | 0 | Builder | 95.6 | 117.1 | 21.6 | complete | 21.6 | 49 |
| 20528045 | 1 | Grower | 98.1 | 118.1 | 20.0 | complete | 20.0 | 49 |
| 20528045 | 2 | Warlord | 95.2 | 119.9 | 24.7 | complete | 24.7 | 49 |
| 20528045 | 3 | Sage | 88.5 | 109.5 | 21.0 | complete | 21.0 | 49 |
| 20542167 | 0 | Builder | 107.2 | — | — | right_censored_after_KEY5 | 22.8 | 34 |
| 20542167 | 1 | Grower | 90.3 | 109.6 | 19.3 | complete | 19.3 | 49 |
| 20542167 | 2 | Warlord | 92.3 | 117.8 | 25.5 | complete | 25.5 | 49 |
| 20542167 | 3 | Sage | 110.2 | — | — | right_censored_after_KEY5 | 19.8 | 15 |
| 20544215 | 0 | Builder | 89.0 | 113.2 | 24.2 | complete | 24.2 | 49 |
| 20544215 | 1 | Grower | 87.3 | 106.4 | 19.1 | complete | 19.1 | 49 |
| 20544215 | 2 | Warlord | 109.0 | — | — | right_censored_after_KEY5 | 21.0 | 30 |
| 20544215 | 3 | Sage | 102.0 | 121.6 | 19.6 | complete | 19.6 | 49 |
| 20547720 | 0 | Builder | 93.8 | 115.7 | 21.9 | complete | 21.9 | 49 |
| 20547720 | 1 | Grower | 93.0 | 112.8 | 19.8 | complete | 19.8 | 49 |
| 20547720 | 2 | Warlord | 95.9 | 122.2 | 26.3 | complete | 26.3 | 49 |
| 20547720 | 3 | Sage | 86.3 | 103.6 | 17.2 | complete | 17.2 | 49 |
| 20553022 | 0 | Builder | 93.9 | 118.8 | 24.9 | complete | 24.9 | 49 |
| 20553022 | 1 | Grower | 91.4 | 113.4 | 22.0 | complete | 22.0 | 49 |
| 20553022 | 2 | Warlord | 91.6 | 116.5 | 24.9 | complete | 24.9 | 49 |
| 20553022 | 3 | Sage | 91.7 | 115.0 | 23.3 | complete | 23.3 | 49 |
| 20565793 | 0 | Builder | 87.1 | 110.5 | 23.4 | complete | 23.4 | 49 |
| 20565793 | 1 | Grower | 92.1 | 115.6 | 23.5 | complete | 23.5 | 49 |
| 20565793 | 2 | Warlord | 93.9 | 117.6 | 23.7 | complete | 23.7 | 49 |
| 20565793 | 3 | Sage | 89.8 | 110.6 | 20.7 | complete | 20.7 | 49 |
| 20566091 | 0 | Builder | 115.5 | — | — | right_censored_after_KEY5 | 14.5 | 4 |
| 20566091 | 1 | Grower | 93.5 | 112.1 | 18.6 | complete | 18.6 | 49 |
| 20566091 | 2 | Warlord | 92.5 | 115.0 | 22.6 | complete | 22.6 | 49 |
| 20566091 | 3 | Sage | 86.6 | 106.1 | 19.5 | complete | 19.5 | 49 |
| 20566313 | 0 | Builder | 94.5 | 117.1 | 22.6 | complete | 22.6 | 49 |
| 20566313 | 1 | Grower | 104.1 | — | — | right_censored_after_KEY5 | 25.9 | 29 |
| 20566313 | 2 | Warlord | 100.9 | — | — | right_censored_after_KEY5 | 29.1 | 35 |
| 20566313 | 3 | Sage | 90.3 | 110.1 | 19.8 | complete | 19.8 | 49 |
| 20568340 | 0 | Builder | 84.7 | 108.0 | 23.3 | complete | 23.3 | 49 |
| 20568340 | 1 | Grower | 97.7 | 118.4 | 20.7 | complete | 20.7 | 49 |
| 20568340 | 2 | Warlord | 93.8 | 120.5 | 26.6 | complete | 26.6 | 49 |
| 20568340 | 3 | Sage | 91.9 | 112.8 | 20.8 | complete | 20.8 | 49 |
| 20575287 | 0 | Builder | 88.5 | 110.7 | 22.2 | complete | 22.2 | 49 |
| 20575287 | 1 | Grower | 94.0 | 121.4 | 27.3 | complete | 27.3 | 49 |
| 20575287 | 2 | Warlord | 95.7 | 118.5 | 22.8 | complete | 22.8 | 49 |
| 20575287 | 3 | Sage | 97.1 | 116.9 | 19.7 | complete | 19.7 | 49 |
| 20575326 | 0 | Builder | 89.9 | 110.9 | 20.9 | complete | 20.9 | 46 |
| 20575326 | 1 | Grower | 89.1 | 109.5 | 20.4 | complete | 20.4 | 46 |
| 20575326 | 2 | Warlord | 96.3 | 119.7 | 23.4 | complete | 23.4 | 46 |
| 20575326 | 3 | Sage | 87.3 | 107.1 | 19.9 | complete | 19.9 | 46 |
| 20582332 | 0 | Builder | 84.2 | 108.7 | 24.5 | complete | 24.5 | 49 |
| 20582332 | 1 | Grower | 86.9 | 110.6 | 23.6 | complete | 23.6 | 49 |
| 20582332 | 2 | Warlord | 94.1 | 117.6 | 23.5 | complete | 23.5 | 49 |
| 20582332 | 3 | Sage | 93.6 | 115.3 | 21.7 | complete | 21.7 | 49 |
| 20583288 | 0 | Builder | 91.7 | 114.6 | 22.9 | complete | 22.9 | 49 |
| 20583288 | 1 | Grower | 89.6 | 112.6 | 23.0 | complete | 23.0 | 49 |
| 20583288 | 2 | Warlord | 93.2 | 117.5 | 24.4 | complete | 24.4 | 49 |
| 20583288 | 3 | Sage | 91.5 | 114.2 | 22.8 | complete | 22.8 | 49 |
| 20584815 | 0 | Builder | 89.3 | 113.8 | 24.5 | complete | 24.5 | 46 |
| 20584815 | 1 | Grower | 101.1 | 121.0 | 19.9 | complete | 19.9 | 46 |
| 20584815 | 2 | Warlord | 102.7 | 125.1 | 22.3 | complete | 22.3 | 46 |
| 20584815 | 3 | Sage | 97.2 | 115.2 | 18.0 | complete | 18.0 | 46 |
| 20590125 | 0 | Builder | 83.6 | 107.3 | 23.7 | complete | 23.7 | 49 |
| 20590125 | 1 | Grower | 87.1 | 107.0 | 20.0 | complete | 20.0 | 49 |
| 20590125 | 2 | Warlord | 97.8 | 118.5 | 20.6 | complete | 20.6 | 49 |
| 20590125 | 3 | Sage | 96.3 | 115.2 | 18.9 | complete | 18.9 | 49 |
| 20599629 | 0 | Builder | 88.8 | 114.5 | 25.7 | complete | 25.7 | 49 |
| 20599629 | 1 | Grower | 88.6 | 110.7 | 22.1 | complete | 22.1 | 49 |
| 20599629 | 2 | Warlord | 91.6 | 115.8 | 24.2 | complete | 24.2 | 49 |
| 20599629 | 3 | Sage | 89.0 | 109.0 | 20.0 | complete | 20.0 | 49 |
