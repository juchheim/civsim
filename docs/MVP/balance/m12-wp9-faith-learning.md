# M12 WP-E5-6 (first slice): monastery staffing and Insight

**Status:** A bounded first implementation of BEL-10 is implemented and covered by focused tests and the passing Release suite (2026-11-14). This report covers the monastery shell, four real monk posts, and the role's research input. It is not an acceptance result, a balance fit, or completion of WP-E5-6. No E5 row is promoted.

## Implemented slice

- A monastery is a Fief-only Iron-work entry unlocked by BEL-10. The current first-pass gate requires a town with at least 50 residents, permits at most three monasteries per civilization, and uses the registry's 30 work, 12 block, and 6 wood costs. The three-per-civilization cap is provisional and bounds the first role-output pass; it has not been balanced.
- Each monastery opens four Monk posts. Monks are resident adults assigned real roles and removed from other work. Existing role staffing reserves at least four additional adults for ordinary work; scholars have higher assignment priority. An unstaffed monastery has no monk output.
- The monk role contributes 4 Insight per head per year in the registry, so four staffed posts supply 16 base Insight per year before existing research archetype and role scales. The role is wired from the economy's staffed head count into research inputs. It is inert outside Fief.
- An empty monastery is included in the existing idle-institution count.

The focused tests cover Fief-only construction gating, the population/town/project and civilization-cap conditions, four-head staffing, the research-role input, non-Fief inertness, and snapshot continuation with staffed monks. Fief economy snapshots use version 32 after appending the university to the Fief Iron-structure block; Bronze and Iron keep version 30 and the Iron structure block retains its original length.

## Explicitly deferred

The simulation has no local Know-how loss/copy mechanism, so this slice does not implement the proposal that monasteries preserve and restore local knowledge. It also does not implement wilderness farming, hospital/quarantine, pilgrimage, cathedral realm effects, or the F-T6 institution-status view. A narrow university follow-up is now implemented: one extra Fief scholar slot per university after LRN-08, and a 1.5x Insight multiplier for staffed scholars in that settlement. Its labor/material costs and effect values remain proposals; no balance acceptance is claimed. This does not model cathedral schools, paper, movable type, zero or algebra. The current proposal to preserve/restore local Know-how needs an actual loss/capture mechanism and regression cases before it can be accepted.

## Next checks

1. Measure the food/work opportunity cost and whether staffed monasteries change KEY-6 pacing in a bounded Fief cohort; then fit or revise the 3-monastery cap and 4-Insight role value.
2. Measure university construction and staffing opportunity costs, scholar Insight contribution, and LRN-08 pacing before considering balance acceptance.
3. Complete remaining WP-E5-6 systems only after their mechanics and role budgets have specific acceptance measurements.
