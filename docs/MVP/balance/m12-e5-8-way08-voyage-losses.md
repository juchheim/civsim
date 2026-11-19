# WP-E5-8 first slices: WAY-08 and DEF-09 (2026-11-14)

**Status:** WAY-08, the DEF-09 concentric-castle defense slice, SEA-10 cargo cap, and later powder/cannon/crew mechanics have focused regression coverage. The full integrated Release suite has not yet passed. E5 rows remain draft. E4 sign-off was confirmed by the user on 2026-11-14.

**Status note:** This is the initial WAY-08/DEF-09 slice report. The later user-confirmed crew, powder, cannon, and KEY-6 sailing rules supersede its older “remaining” section; see [the WP-E5-8 powder/cannon follow-up](m12-wp8-power-furnace.md#wp-e5-8-implementation-slice-powder-cannon-and-siege-crews-2026-11-14).

## SEA-10 ship cargo fit (2026-11-14)

Fief sea-routed caravans now have a per-shipment physical load limit grounded in the SEA-05 catalog note: **12 L** for the plank-boat baseline. Completing SEA-10 doubles the limit to **24 L**. The limit is applied only when the route actually crosses water by boat; land routes are unchanged. Food cargo uses the docs/10 unit conversion of 1 L ≈ 2 R, so its limits are 24 R and 48 R. It applies only in Fief, and Fief's existing D17 gate still disables sea crossings until that civilization completes KEY-6. SEA-10's E5 catalog row remains draft.

The Compact's trade acceptance and scarcity valuation are unchanged. Each eligible season may send another shipment: only the capped amount is removed from seller stock, and remaining surplus can travel in later deliveries. The loaded amount is stored in the existing `Expedition.Cargo`, so arrival payments and partial-payment delivery calculations use the actual capped load. No additional shipment state or snapshot format change was needed. `FiefShipCargoTests` checks route eligibility, 12/24 L limits, uncapped land routes, Fief-only behavior, repeat deliveries, and payment for the loaded amount. SEA-07's Iron harbor capacity effect is not changed by this slice.

## Scope

`PoliticsSystem.Voyage` applies the same risk calculation to trip forecasts, outbound expeditions, and settling voyages. WAY-08 reduces the calculated permille risk by integer division by two, before the existing Great Lighthouse modifier. This retains the established deterministic voyage stream and makes the reported forecast agree with the risk used by the actual loss rolls. The Fief guard keeps Iron and Bronze chapter behavior unchanged.

The WAY-08 focused test uses a pack where WAY-08 alone is made numeric for the fixture; it does not promote or edit the E5 content rows. It checks the baseline formula, exact halving, unchanged reachability and sea-tile route, repeatable outcomes for the same route and season, and that halving cannot add a loss for the same deterministic roll. Walking travel remains risk-free.

DEF-09 is limited to Fief and enters the existing settlement defense multiplier only when DEF-09 is complete, that same settlement has a built concentric-castle upgrade, and its castle is garrisoned. The Fief planner can construct the upgrade for the registry's 80 work, 24 ashlar, 12 wood and 2 iron, after the project and garrisoned castle are present. It multiplies the existing provisional `CastleStrongpoint` x1.25 by a provisional x1.5, for x1.875 total. The `e5_combat_oracle.py` storm sweep reports the attacker/defender crossover at x1.9; its checked fixtures show x1.25 and x1.5 below that point while the naive x1.25 × 2 = x2.5 reading flips the post-breach storm to defender wins. This supports x1.5 as a bounded implementation candidate, not a final balance result. The change leaves the existing raid and assault resolver path and siege wall-integrity calculations intact; it does not implement DEF-09's separate engine/cannon-crew attrition rule.

## Acceptance limits and next work

These are mechanic slices, not acceptance of WP-E5-8. The WAY-08 test exercises deterministic seeded loss rolls but does not estimate a population-level loss rate across natural Fief matches. E5 still does not open sea crossings; Fief sea trips remain gated until own KEY-6. `FiefSeaSpeedTests` now verifies that WAY-09 and SEA-11 combine to ×1.82 on Fief sea routes with one rounding step, leave land routes unchanged, and remain inert in Iron. SEA-10 cargo tests verify the fitted per-shipment cap and payment path, but do not measure whole-match trade throughput or balance; that remains in whole-match WP-E5-8 / WP-E5-11 integration. The separate powder/cannon follow-up implements four-adult crews and keyed 1.00% / 1.25% per-adult casualty rolls; 100,000-exposure mechanic diagnostics are recorded in the [powder/cannon report](m12-wp8-power-furnace.md#wp-e5-8-implementation-slice-powder-cannon-and-siege-crews-2026-11-14), but do not estimate whole-match casualties. Remaining WP-E5-8 work includes whole-match war balance, the other warfare projects, and whole-match sea trade throughput. The concentric-castle multiplier remains provisional pending combat/siege balance review.

Validation completed in the shared Release run:

```bash
dotnet build CivSim.slnx -c Release
dotnet test CivSim.slnx -c Release
dotnet run --project src/CivSim.Cli -c Release -- validate --strict
```

SEA-10 slice validation on 2026-11-14: after the cargo and food-unit conversion changes, the focused cargo/voyage/Iron sea-trade filter passed 12/12; `dotnet build CivSim.slnx -c Release` succeeded with zero warnings/errors; strict content validation reported 0 errors and 0 warnings. A prior full `dotnet test CivSim.slnx -c Release` run on the already-modified shared tree completed with 1,295 passed and 4 failed: strategist investment expectation, Bronze snapshot byte expectation (35 vs 36), content golden hash, and CLI content golden hash. No golden was refreshed.

The dedicated `FiefSeaSpeedTests` regression passed 1/1 on 2026-11-14, covering the combined WAY-09 / SEA-11 route modifier and Fief/Iron isolation.
