# M12 WP-E5-2: typing the Faith & Fief rows (2026-11-05)

**Status:** content only, on branch `codex/e5-fief` (worktree `../civsim-e5`, cut from `main` at `43a1648`). All 49 regular E5 projects and KEY-6 now carry typed effects, practice, flavor and a Moment. **Every row is still `status: draft`**, so a Bronze or Iron match cannot reach any of it. Numbers marked P in effect notes are provisional and come from [27](../27-faith-and-fief-age.md) and the oracle screens in [m12-wp1-oracle-fit](m12-wp1-oracle-fit.md).

## What changed

| Area | Change |
|---|---|
| Catalog | 32 files: the 50 E5 rows go from one `prose` effect to 1 to 5 typed effects each (`build`, `good`, `role`, `mod`, `hazard`, `cohesion`, `admin`, `adopt`, `defense`, `unlock_action`, `unlock`, `reveal`, `diplo`, `combat`, `wonder`, `era_advance`, `legacy`, `rule`). Ids, names, types, requirements and gates are untouched. |
| Goods (16) | G-SALTPETER, G-SULFUR, G-PIG, G-CAST, G-CSTEEL, G-ARMS5, G-POWDER, G-CANNON, G-CROSSBOW, G-HORSESHOE, G-SPIRITS, G-PORCELAIN, G-SPECTACLES, G-PAPER, G-BOOK, G-MANUSCRIPT (all `introduced_era: 5`). |
| Structures (11) | windmill, sawmill, blast_furnace, powder_mill, monastery, university, hospital, clock_tower, print_shop, concentric_castle, gothic_cathedral (Wonder). |
| Roles (4) | monk, master_mason, knight, gunner. |
| Actions and practice | fire_cannon and pilgrimage, with Know-how mappings. |
| Recipes (14) | pig iron, cast goods, cannon, crucible steel and arms, powder, crossbow, horseshoes, spirits, porcelain, spectacles, paper, printed book, manuscript. Quantities follow the oracle screens (for example the furnace at half the bloomery's charcoal per litre). |
| Validator | New check **V22** (E5 integrity), the twin of V21: E5 rows are promoted together, typed only, with a computed cost, flavor and Moment. |
| Sim | One-token change in two places (`EconomyContent.Bronze.cs`, `LivingEconomyModel.cs`): the legacy Bronze fingerprint now represents E5 draft rows by one prose placeholder, exactly as it already did for E4. |
| Golden | `tests/golden/content-pack.txt` refreshed (projects 519; bytes 371,971; hash `73cdb308...`). |

## The finding worth knowing

Typing the rows first **moved the research and politics determinism goldens**, even though E5 is unreachable. The cause: the economy's content fingerprint hashes every project's effects, with a special case that keeps E4 draft rows looking like their old single prose line. E5 had no such case, so typing it changed Bronze checksums. Bisecting showed registries alone were harmless and any typed E5 effect triggered it. The fix is the same special case for E5. This also means that, when Fief mode is activated later, it needs its own full-content fingerprint, as Iron mode does ([25 §2.2](../25-iron-age.md)).

## Decisions baked into the rows (all P, all for the oracle to refit)

- **Power:** effects name power units (wheel 5, windmill 4 at 80%, horse gin 2); no mill multiplier applies without a unit.
- **Blast furnace:** ×4 iron at half the bloomery's charcoal per litre; cast iron is capped by recipes, not open-ended.
- **Cannon:** twice an engine's damage to wall integrity (reading B of the oracle), with the "−70% walls" kept as a storm modifier, not a one-season delete button.
- **Castle:** concentric multiplier 1.5, below the 1.9 storm crossover.
- **Claim upkeep (confirmed D4):** a Fief-chapter rule of 10% of farm-tile yield paid as grain to the non-farming staff; TER-10 multiplies it by 0.70.
- **Crucible arms:** strength 3.6 (3.0 × 1.2 once, never stacked on the steel edge).
- **Guilds:** craft quality means durability and waste of non-kit goods, so tool service life stays exactly 2:1 between T11 and T12.
- **Knowledge preservation (BEL-10):** a monastery keeps a copy of the settlement's Know-how pool; there is no global tech loss.
- **Clock tower:** +5% scheduled work and a small Cohesion gain within 6 tiles only, not a global multiplier.

## Checks run

`civsim validate --strict` 0 errors, 0 warnings (509 projects + 10 keystones, 248 draft, 271 numeric). Toy, research (12 min, grower) and politics (24 min) determinism checks pass against the new pack, so no simulation golden changed. The full Release test suite result is recorded in the session summary.

## Not done

- No E5 row is numeric, and nothing in the simulation reads E5 effects yet; the effects are the spec for WP-E5-4 onward.
- No row has an `ack` or cost override; costs are `EraBase 6000 × type multiplier` (KEY-6 15,000).
- The Fief-mode economy fingerprint, the economy profile (D3) and the rest of 27 §9 are untouched.
