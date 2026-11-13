# M12 WP-E5-5 (second slice): three-field rotation (2026-11-13)

**Status:** AGR-11's field schedule is implemented and fixture-tested on `codex/e5-fief`. It is gated on Fief mode and the civilization's own KEY-5. The schedule is derived from the settlement's ordered field tiles, year and completed projects, so it adds no saved or hashed state. Bronze and Iron continue to crop every field as before.

## What changed

- Before AGR-11, the Fief-era baseline crops one of every two field strips each year. With AGR-11, two of every three strips are cropped; the others lie fallow and recover soil.
- AGR-11 adds the provisional 1.125 legume yield on cropped strips. `(2/3 × 1.125) / 1/2 = 1.5`, matching the catalog's +50% land-productivity target.
- Annual farm labor need scales with the cropped share, so the same share of field work can cover the scheduled crop area.
- Flooded plots, terraces, existing soil, irrigation, field-food accounting and straw output continue through their existing paths.

## Evidence

`FiefLandTests` checks the exact six-year schedule (each strip is cropped three times under the two-field baseline and four times under AGR-11), the 4:3 farm-labor need change, the 1.5 productivity decomposition, an observed six-year grain-yield gain within 1.35–1.65, and no effect before KEY-5 or in Bronze and Iron modes.

One whole-match A/B used the same promoted temporary content pack, seed `20261019`, Fief mode, `--grant-e5` and a 130-minute horizon. KEY-6 timing was effectively unchanged: 90.8–92.9 minutes before the change and 90.9–92.9 after. End populations rose 8–16% across the four civilizations. No other E5 project or content changed. This is a smoke-scale result, not a balance sample; it does not establish a safe long-run food cap or an era-wide population target.

## Follow-up

The remaining land and food mechanics are recorded in [m12-wp8b-horse-collar](m12-wp8b-horse-collar.md) and [m12-wp8c-land-systems](m12-wp8c-land-systems.md). The five-geography fixture oracle is in [m12-wp8d-land-food-oracle](m12-wp8d-land-food-oracle.md); matched natural-start anchors and multi-seed whole-match balance still need to run before E5 promotion.
