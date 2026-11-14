# M12 WP-E5-6: faith, learning, and hospital first slices

**Status:** A bounded first implementation of BEL-10 is implemented and covered by focused tests and the passing Release suite (2026-11-14). This report covers the monastery shell, four real monk posts, and the role's research input. It is not an acceptance result, a balance fit, or completion of WP-E5-6. No E5 row is promoted.

**Follow-up, 2026-11-14:** The university staffing slice and a narrow MED-07 hospital effect are now implemented. The integrated Release suite passes, including the hospital snapshot continuation test. Fief snapshots are version 34; Bronze and Iron remain at version 30 and retain their original Iron structure block length. No E5 row is promoted and no balance acceptance is claimed.

## Implemented slice

- A monastery is a Fief-only Iron-work entry unlocked by BEL-10. The current first-pass gate requires a town with at least 50 residents, permits at most three monasteries per civilization, and uses the registry's 30 work, 12 block, and 6 wood costs. The three-per-civilization cap is provisional and bounds the first role-output pass; it has not been balanced.
- Each monastery opens four Monk posts. Monks are resident adults assigned real roles and removed from other work. Existing role staffing reserves at least four additional adults for ordinary work; scholars have higher assignment priority. An unstaffed monastery has no monk output.
- The monk role contributes 4 Insight per head per year in the registry, so four staffed posts supply 16 base Insight per year before existing research archetype and role scales. The role is wired from the economy's staffed head count into research inputs. It is inert outside Fief.
- An empty monastery is included in the existing idle-institution count.

The focused monastery tests cover Fief-only construction gating, population/town/project and civilization-cap conditions, four-head staffing, research-role input, non-Fief inertness, and snapshot continuation. University tests cover the added Fief scholar post, local 1.5x Insight effect, and snapshot continuation. The added hospital tests cover its Fief/MED-07 gate, one-physician/two-attendant staffing, the 20% effect on existing outbreak mortality, no effect while unstaffed, and snapshot continuation; all pass in the integrated Release suite. The Fief economy snapshot version is now 34 for the appended hospital and concentric-castle structures. Bronze and Iron remain at version 30 and retain their original Iron structure block length.

The hospital is a Fief-only, MED-07 town structure with the current first-pass 24 work, 8 block, and 4 wood cost. The staff roles are one Physician and two Attendants, assigned as resident adults off other work. While all three posts are staffed, infected figures in that settlement receive 0.8x the existing epidemic hazard. It does not change outbreak incidence, infection, spread, or duration. The costs and reduction are draft values, and no quarantine or route-import rule is included.

## Explicitly deferred


## Next checks

1. Measure the food/work opportunity cost and whether staffed monasteries change KEY-6 pacing in a bounded Fief cohort; then fit or revise the 3-monastery cap and 4-Insight role value.
2. Measure university construction and staffing opportunity costs, scholar Insight contribution, and LRN-08 pacing before considering balance acceptance.
3. Measure whether staffed hospitals reduce epidemic deaths as intended, and establish an acceptance band before fitting the 20% effect or construction/staff costs. Quarantine and imported-outbreak controls remain separate work.
4. Complete remaining WP-E5-6 systems only after their mechanics and role budgets have specific acceptance measurements: local Know-how retention, wilderness farming, pilgrimage, cathedral realm effects, cathedral schools, paper and movable type.
