# I-T1 pacing evidence reconciliation — 2026-11-06

**Status: pacing fit open; no target pass/fail.** The approved I-T1 definition in [25 §13](../25-iron-age.md#13-provisional-balance-targets-and-oracle-work) requires refitting own KEY-4→KEY-5 pacing, with an acceptance band still TBD, and a candidate year-cap ending rate at or below 10%. Existing E3 behavior and the provisional Iron chapter rules are unchanged by this report.

## Evidence reviewed

This is a reconciliation of the existing [2026-11-04 whole-match screen](m11-iron-balance-screen-2026-11-04.md) and its per-seat [CSV](m11-iron-balance-screen-2026-11-04.csv), not a new simulation. That screen records 78 calibration matches and 16 held-out matches (94 total), four seats per match, natural Iron mode with no E4 grants, and a candidate countdown/cap. Its source baselines, pack hash and seed lists are recorded in the underlying report.

Counts below were recomputed from the CSV with the following input and definitions:

```sh
python3 - <<'PY'
import csv
from collections import Counter
rows = list(csv.DictReader(open('docs/MVP/balance/m11-iron-balance-screen-2026-11-04.csv')))
for cohort in ('calibration', 'held-out'):
    seats = [r for r in rows if r['cohort'] == cohort]
    matches = {r['seed']: r for r in seats}
    print(cohort, len(matches), len(seats),
          sum(bool(r['key4_min']) for r in seats),
          sum(bool(r['key5_min']) for r in seats),
          Counter(r['ending'] for r in matches.values()))
PY
```

| Cohort | Matches / seats | Reached KEY-4 | Reached KEY-5 | Uncensored own KEY-4→KEY-5 | Year-cap endings |
|---|---:|---:|---:|---:|---:|
| Calibration | 78 / 312 | 292 | 176 | 176 | 2/78 (2.6%) |
| Held out | 16 / 64 | 61 | 35 | 35 | 0/16 (0.0%) |
| Combined | 94 / 376 | 353 | 211 | 211 | 2/94 (2.1%) |

For seats reaching both thresholds, the source report gives KEY-4→KEY-5 mean/median of 19.5/19.4 minutes in calibration, 19.7/19.3 minutes held out, and 19.6/19.4 minutes combined. The combined range is 15.7–24.0 minutes. The candidate ≤10% year-cap diagnostic is observed below 10% in both cohorts. The previous 17-minute projection is shorter than the combined mean, but without an approved acceptance band this is descriptive refit evidence, not a pacing pass or failure.

## Censoring and limits

At match end, 211 seats had reached KEY-5; 142 had reached KEY-4 but not KEY-5, and 23 had never reached KEY-4. Of those 23, 13 ended with zero population and 10 remained alive. Thus KEY-4→KEY-5 timing is observed for 211 seats and right-censored or absent for the other 165; no time is imputed for them. The CSV does not identify unique seats that lost settlements, so it cannot classify captured-seat outcomes separately from other alive, censored seats. The source screen reports 245 settlement-capture incidents (490 event records), but the requested unique captured-seat breakdown is unresolved.

The existing sample satisfies the proposed 80+ match volume and includes a held-out cohort, and its candidate cap-rate diagnostic is under 10%. It does not establish an approved pacing band, nor does it complete the required captured/censored/eliminated outcome accounting. Accordingly I-T1 remains open. The next acceptance run needs a user-approved own-entry timing band and a telemetry format that tracks unique seat capture/elimination history without treating an uncompleted KEY-5 as a timing observation.
