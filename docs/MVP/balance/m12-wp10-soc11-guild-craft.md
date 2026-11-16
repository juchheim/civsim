# WP-E5-7 SOC-11 guild craft slice

**Status:** Minimal Fief-only quantities now exist for recipe quality, recipe input waste, and craft-role training exposure. The figures below are explicit fitting hypotheses, not balance acceptance or catalog promotion. SOC-11's practice share now includes the Society lineage so the eventual numeric row passes strict validation; its status/effect values and the content golden remain unchanged.

## Draft model

- Non-kit Fief recipes carry a 5% input-loss allowance per input quantity. This is an intentionally small, uniform starting hypothesis because the current recipe registry has no measured per-recipe loss data. Successful batches add the modeled lost input to the settlement's `GuildRecipeWaste` readout. Actual inputs are increased by the same allowance; output quantities and recipe labor are unchanged.
- SOC-11 raises the craft-quality readout from 1.0 to 1.2 per successful batch. The implementation expresses the listed +20% as a quality index, not an output multiplier. It reduces the 5% input-loss allowance by dividing it by 1.2 (4.167%); it does not touch kit recipes, tool durability, or T12 service life.
- Crafting accumulates one craft-role training work-season per recipe labor work-season. SOC-11 multiplies this training exposure by 1.5. This is a minimal progress quantity because the simulation has no named artisan roles or apprentice completion thresholds; it does not grant a new role or alter production rate.
- Waste, quality units, and training exposure live in Fief civic state, participate in its hash and snapshot, and migrate older Fief snapshots with zeroed values. The current Fief snapshot is v44 (readers accept v40–v43); Iron/Bronze state paths remain at v30 and unchanged.

## Verification and open fit

`FiefGuildCraftTests` covers the guild multipliers, Iron isolation, and snapshot continuation (2/2 pass). `FiefUniversityTests` also passed 5/5 after rebuilding the updated Sim assembly. The full Release suite now passes 1,423/1,423 tests, including Fief snapshot migration and save/replay integration.

The 5% loss baseline, the interpretation of craft-role training as accumulated work exposure, and per-recipe variation remain unvalidated. Before promotion, fit them on an explicit recipe selection and assess their production and resource effects under WP-E5-11. This slice is observable and isolated; it does not close the broader guild balance gate.

## Paired durability measurement addendum — 2026-11-16

`SOC11_trace_observer_is_exact_state_neutral_for_tool_durability_and_T12_life` now exercises one unit of actual T12 kit wear before advancing matched trace-off and trace-on Fief states. It asserts the kit's dedicated service life is 60 work-seasons, the SOC-11 registry service-life multiplier for `G-TOOL12` is exactly 1.0 in both states, and the full serialized economy state/checksum (including each figure's remaining kit service and age-wearing recipe goods) is identical after the season. A separate treatment/control test equips an otherwise matched adult with a T12 kit, applies ten units of wear in each state, and confirms equal remaining service with and without SOC-11. The paired traced season also verifies non-kit crafted `G-ARMS3` age wear occurs; the existing SOC-11 treatment/control test confirms eligible registry craft goods receive the 1.2 service-life multiplier. Focused Release tests for `FiefGuildCraftTests` pass 7/7, and `git diff --check` passes.

This closes the instrumentation/control gap for tool durability and T12 life: those are explicit no-effect boundaries of SOC-11's non-kit craft-quality rule, not an unmeasured positive effect. SOC-11 calibration, recipe-stratified acceptance, exposure, and holdout precision remain open; no catalog values are promoted.
