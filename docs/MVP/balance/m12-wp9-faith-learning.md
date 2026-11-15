# M12 WP-E5-6: faith, learning, and hospital first slices

**Status:** A bounded first implementation of BEL-10 is implemented and covered by focused tests and the passing Release suite (2026-11-14). This report covers the monastery shell, four real monk posts, and the role's research input. It is not an acceptance result, a balance fit, or completion of WP-E5-6. No E5 row is promoted.

**Follow-up, 2026-11-14:** The university staffing slice and a narrow MED-07 hospital effect are now implemented. The integrated Release suite passes, including the hospital snapshot continuation test. Fief snapshots are version 34; Bronze and Iron remain at version 30 and retain their original Iron structure block length. No E5 row is promoted and no balance acceptance is claimed.

**WP6 mortality diagnostic, 2026-11-14:** A controlled matched-seed fixture measured one forced Plague season in 16 seed pairs (`20261114`–`20261129`), with 200 starting people and MED-07. Both arms built and staffed the same hospital through an identical setup season; immediately before the measured season the control removed the structure, leaving only the hospital arm staffed. A test-only callback now counts infected residents at the end of `StartOutbreaks`, before mortality resolution; its null default has no effect, and the callback field/count are not serialized or checksummed. Infection exposures match exactly between arms for each seed. The death ledger recorded **1,317 exposures, 587 unstaffed deaths, and 476 staffed deaths** (pooled death risks 44.57% and 36.14%, 18.91% relative reduction). Per-seed paired results are:

| Seed | Exposures | Unstaffed deaths | Staffed deaths | Deaths prevented |
| --- | ---: | ---: | ---: | ---: |
| 20261114 | 90 | 51 | 41 | 10 |
| 20261115 | 90 | 33 | 23 | 10 |
| 20261116 | 77 | 34 | 27 | 7 |
| 20261117 | 82 | 34 | 26 | 8 |
| 20261118 | 79 | 35 | 28 | 7 |
| 20261119 | 76 | 31 | 27 | 4 |
| 20261120 | 95 | 41 | 29 | 12 |
| 20261121 | 89 | 38 | 30 | 8 |
| 20261122 | 80 | 38 | 30 | 8 |
| 20261123 | 83 | 36 | 33 | 3 |
| 20261124 | 83 | 31 | 24 | 7 |
| 20261125 | 84 | 42 | 36 | 6 |
| 20261126 | 82 | 38 | 31 | 7 |
| 20261127 | 75 | 35 | 29 | 6 |
| 20261128 | 69 | 32 | 28 | 4 |
| 20261129 | 83 | 38 | 34 | 4 |

The paired mean absolute reduction was **6.94 epidemic deaths per seed**; the 95% paired t interval was **5.63 to 8.25 deaths per seed** (16 pairs, 15 degrees of freedom; an approximate interval assuming roughly normal seed-pair differences). This is a bounded forced-outbreak fixture result, not whole-match or balance acceptance evidence. It does not change the draft 20% hazard multiplier, which remains subject to broader cohort measurement.

**WP6 institution opportunity diagnostic, 2026-11-14:** A separate matched Fief cohort compared no institution, one monastery, and one university across 16 fixed seeds (`20261114`–`20261129`), with the same `KEY-5`, `BEL-10`, `LRN-05`, and `LRN-08` projects and one common setup season. All arms had 559 working-age residents total across the cohort (34.94 per fixture). The baseline assigned 0 monks and 0 scholars; monastery assigned 64 monks total (4.00 per fixture); university assigned 16 scholars total (1.00 per fixture). The institution assignments consume those resident adult slots from ordinary work. Source inputs recorded 0 monk/scholar equivalent heads in baseline, 4 monk heads in monastery, and 1.5 scholar-equivalent heads per university (the staffed scholar's existing +50% Fief university input). Catalog-weighted institution role Insight input was 0, 16, and 72 per year per fixture, respectively, before downstream research-system calibration/modifiers; the sum of source role heads times catalog Insight coefficients was 20, 36, and 92 per year per fixture. These are local, one-season fixture outputs only. The sample does not estimate ordinary work's lost output, construction/upkeep costs over time, or whole-match research pacing, and it does not promote draft role values or institution costs.

## Implemented slice

- A monastery is a Fief-only Iron-work entry unlocked by BEL-10. The current first-pass gate requires a town with at least 50 residents, permits at most three monasteries per civilization, and uses the registry's 30 work, 12 block, and 6 wood costs. The three-per-civilization cap is provisional and bounds the first role-output pass; it has not been balanced.
- Each monastery opens four Monk posts. Monks are resident adults assigned real roles and removed from other work. Existing role staffing reserves at least four additional adults for ordinary work; scholars have higher assignment priority. An unstaffed monastery has no monk output.
- The monk role contributes 4 Insight per head per year in the registry, so four staffed posts supply 16 base Insight per year before existing research archetype and role scales. The role is wired from the economy's staffed head count into research inputs. It is inert outside Fief.
- An empty monastery is included in the existing idle-institution count.

The focused monastery tests cover Fief-only construction gating, population/town/project and civilization-cap conditions, four-head staffing, research-role input, non-Fief inertness, and snapshot continuation. University tests cover the added Fief scholar post, local 1.5x Insight effect, and snapshot continuation. The added hospital tests cover its Fief/MED-07 gate, one-physician/two-attendant staffing, the 20% hazard effect on existing outbreak mortality, no effect while unstaffed, and snapshot continuation. The 16-pair forced-Plague diagnostic now records pre-mortality exposures and paired deaths, but is not whole-match or balance acceptance evidence. The 16-seed institution diagnostic records assigned adult roles and source Insight inputs but not lost ordinary work output or whole-match pacing. The Fief economy snapshot version is now 34 for the appended hospital and concentric-castle structures. Bronze and Iron remain at version 30 and retain their original Iron structure block length.

The hospital is a Fief-only, MED-07 town structure with the current first-pass 24 work, 8 block, and 4 wood cost. The staff roles are one Physician and two Attendants, assigned as resident adults off other work. While all three posts are staffed, infected figures in that settlement receive 0.8x the existing epidemic hazard. It does not change outbreak incidence, infection, spread, or duration. The costs and reduction are draft values, and no quarantine or route-import rule is included.

## Explicitly deferred


## Next checks

1. Measure the food/work opportunity cost and whether staffed monasteries change KEY-6 pacing in a bounded Fief cohort; the 16-seed role-slot/input diagnostic above does not measure lost work output. Then fit or revise the 3-monastery cap and 4-Insight role value.
2. Measure university construction and staffing opportunity costs and LRN-08 pacing; the 16-seed diagnostic records one staffed scholar's source Insight contribution but does not establish match-level pacing or acceptance.
3. Expand hospital mortality measurement beyond the forced one-season Plague fixture and establish an acceptance band before fitting the 20% effect or construction/staff costs. Quarantine and imported-outbreak controls remain separate work.
4. Complete remaining WP-E5-6 systems only after their mechanics and role budgets have specific acceptance measurements: local Know-how retention, wilderness farming, pilgrimage, cathedral realm effects, cathedral schools, paper and movable type.
