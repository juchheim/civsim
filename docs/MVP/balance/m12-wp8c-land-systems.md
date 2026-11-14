# M12 WP-E5-5 (fourth slice): land, herds and claimed grain (2026-11-13)

**Status:** The remaining first-pass system C mechanics are implemented on `codex/e5-fief`, alongside the earlier rotation, horse-collar, and power/furnace slices. These rules are gated on Fief mode after KEY-5; no new persistent or hashed state was added. E5 catalog rows remain draft.

## Rules

| Project | First-pass effect | Scope and limits |
|---|---|---|
| HUS-10 Selective Breeding | Domestic meat, milk and wool yield ×1.5; ox and collar-horse draft strength ×1.2. | Fief after KEY-5 only. Wild-game harvest is unchanged. |
| HNT-11 Game Reserves & Falconry | Game on a tile owned by a Fief civilization with HNT-11 keeps at least half its local carrying capacity; other civilizations cannot hunt or tame it. The owner needs two assigned adults to hunt there, representing a hunter and falconer. | Reserve ownership is derived from tile ownership and the herd's location; there is no separate reserve building. A nearby claimed reserve adds +2 local cohesion. |
| TER-10 Manorial Estates | 10% of actual harvest from owned field tiles is transferred as grain to the nearest staffed same-civilization town or city. TER-10 changes the rate to 7%. | A work-capable adult is the proxy for non-farming staff; jobs and grain stores are pooled by settlement. No payment is made when no other staffed town or city can receive it. |
| WOD-10 Timber Framing & Trusses | Hall housing capacity ×1.5. | Fief after KEY-5 only. Halls are the existing modeled building-size surface; separate barn, shipyard and workshop size caps do not exist yet. |

AGR-11's deterministic three-field rotation and NRG-04's horse collar remain as described in [m12-wp8a](m12-wp8a-land-rotation.md) and [m12-wp8b](m12-wp8b-horse-collar.md). The existing power/furnace work is described in [m12-wp8-power-furnace](m12-wp8-power-furnace.md).

## Evidence

- `FiefLandSystemsTests` covers HUS-10 factors and Fief isolation, actual 10%/7% inter-settlement grain transfers, HNT-11 ownership/floor/hunter-party/cohesion behavior, and WOD-10 hall capacity with Iron isolation.
- The F-T3 screen used a temporary pack with all 50 E5 rows promoted, 9 matched seeds (`20261019–20261027`), a fixed 200-person smelting town, four 30 L forest tiles, Coppicing and a 64-season run. Output is in [m12-wp8c-fief-fuel-output](m12-wp8c-fief-fuel-output.txt). Eight seeds had mature windows; all eight stayed below 15% metal-chain work. The maximum was 14.3%. NoFuel was 0% in eligible windows, forest-ledger residuals were 0.000 L, and the furnace added no forest-clear crossings. Seed `20261026` produced no kit or charcoal-demand seasons, so it is censored. Two seeds in total did not meet the I-T6 minimum charcoal-demand-season count.
- A one-seed whole-match A/B used a temporary promoted E5 pack, Fief mode, `--grant-e5`, seed `20261019` and 130 minutes. KEY-6 remained at 90.9 / 92.8 / 92.9 / 92.5 minutes across the four civilizations. Final populations changed from 2,145 / 3,845 / 2,735 / 3,652 to 2,464 / 4,119 / 2,913 / 3,899. This is a smoke comparison, not balance signoff.
- Two Iron matches at 130 minutes (`20261019–20261020`) produced byte-identical output before and after this slice. The ordinary E5-draft pack was used in both runs.
- The Release build passed; the full suite passed 1,211/1,211; strict content validation and the content-pack, toy, research and politics goldens passed. No E5 content status was changed.

## Still open before E5 promotion

- F-T2: follow up on the 9 of 90 town windows below 60% in the four-seed power-ledger screen; all nine had spent their full available source budget.
- F-T4: the simulation-backed five-geography fixture screen, first four-seed all-seat screen and focused eight-seed natural-start follow-up are recorded in [m12-wp8d-land-food-oracle](m12-wp8d-land-food-oracle.md). The fixtures establish physical grain output and full-staffing sustainable food capacity for the controlled profiles. The focused run gives 8/8 identical-state Builder anchors; no-river, deep-inland, other-archetype starts and broader whole-match balance remain open.
- F-T3: the fixture screen is promising but not a broad balance result; retain the censored seeds in follow-up runs.
- Cast-iron goods, powder and cannon belong to WP-E5-8. Full E5 match balance, promotion and playtesting remain in WP-E5-11/12.

No rival countdown was added or changed; D11 remains the user's confirmed rule.
