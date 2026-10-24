# 23 — Bronze Age (E3) Specs: Registries, Ladders, Constants and the 66 Project Specs (WP1)

> **Current cadence update (2026-10-24):** the original provisional EraBase 200 and the later live fit of 150 are historical. Current Bronze EraBase is **400**; list costs below are doubled to match [07c §10.1](07c-e0-e2-checks.md#101-roots-and-bronze-still-finish-too-quickly-2026-10-24). The longer timing is projected, not freshly accepted in the live harness.
> The build-ready content spec for **E3 Bronze**: every new good, tool tier, arms kit, recipe, structure, role, action, ladder rung, practice split and constant the sim needs, and **all 65 E3 projects plus KEY-4** with typed effects, cost, practice, Moment, flavor and visible change. It is the E3 counterpart of [10](10-e0-e2-actions-and-goods.md) (actions and goods) and [11](11-e0-e2-project-specs.md) (project specs), and work package **WP1** of [22 §20](22-bronze-age.md#20-work-breakdown).
> The design is [22](22-bronze-age.md); its §5–§12 and §15 are binding here, and its §19 questions are taken as resolved (Tin Folk appear at the first KEY-3 in the world; the faster live map fill is accepted; art starts as placeholders). Where this document has to fix or extend 22, it says so in place and lists every case in §21.
> Everything below is written so WP3 can transcribe it into `content/` without design decisions: registry entries and project effects are given as the YAML lines they become. Other *provisional* design values still inherit 22's status; the current research costs are the updated values in §14.
> Bronze project and system implementation remains pending; the EraBase registry cost was updated to the current cadence above.

---

## 1. Notation, new effect kinds and stacking

### 1.1 Effect notation
Project effects are written as the catalog's YAML flow maps (`content/catalog/*.yaml`, kinds from `src/CivSim.Content/EffectSpecs.cs`). The primitives of [11 §1](11-e0-e2-project-specs.md#1-effect-primitives-syntax-used-in-the-tables) map one-to-one: `Method(…)` → `method`, `Mod(…)` → `mod`, `Cap(…)` → `cap`, `Hazard(…)` → `hazard`, `Build(…)` → `build`, `Wonder(…)` → `wonder`, `Good(…)` → `good`, `Role(…)` → `role`, `Action+(…)` → `unlock_action`, `Tier(…)` → `tier`, `Cohesion(…)` → `cohesion`, `Adopt(…)` → `adopt`, and the text kinds (`rule`, `claim`, `reach`, `reveal`, `diplo`, `combat`, `lever`, `know_how`, `legacy`, `risk`, `gate`, `event`, `unlock`). `mod` values follow the existing convention: `pct` is a fraction (−0.25 = −25%), `mult` a factor, `flat` an addition, `set` a new value.

### 1.2 New and extended kinds (WP3 adds them to `EffectSpecs`)
22 §12 introduces `Admin`, `Mandate`, `Sanitation` and a typed `Defense`; E3 also needs two existing kinds widened. Exact schemas and semantics:

| Kind | Schema (YAML fields) | Semantics | VisibleChange |
|---|---|---|---|
| **`admin`** (new) | `span`: Int 0–20 (opt) · `overreach`: Num 0–1 (opt) · `note` (opt); at least one of `span`/`overreach` | `AdminSpan += span`. `overreach` is a reduction *r* combined as `AdminReduction = 1 − Π(1 − r)` over every completed `admin` effect (22 §8.3). Both are civilization-wide integers/Fix64 in the research state | Overlay (span and Overreach readouts in the Cohesion overlay and tile card) |
| **`mandate`** (new) | `start`: Int 0–10 · `cap`: Int −5–10 · `recharge`: Int −10–10 · `decree`: Enum(found, expedition, mine, levy, corvee, compact) · `cost`: Int 0–5 · `note`; all optional, at least one | All values are **deltas summed from zero** over completed projects: KEY-3 gives `start 1, cap 2, recharge 8`; later projects add (`cap +1`, `recharge −2`). Recharge is floored at `MandateRechargeMin` (4). `decree` + `cost` *sets* that Decree's Mandate cost (the lowest set value wins) | Overlay (Mandate pips) |
| **`sanitation`** (new) | `value`: Num 0–1 | `Sanitation += value`; density pressure × (1 − Sanitation) (05 §13, 09 §10). Applies in settlements that hold ≥ 1 `G-SOAP` per 20 people (the soap is used up at 1 per 20 people a season); elsewhere half the value | Behavior |
| **`defense`** (typed; was text only) | `structure`: Str · `mult`: Num 1–10 · `loot`: Num 0–1 (opt) · `refuge_radius`: Int (opt) · `site_within`: Int (opt) · `assault`: Bool (opt, default true) · `text`: Str (opt) | The structure's defense multiplier on defenders' strength (13 §6.2). **Barrier multipliers do not stack with each other:** thorn barrier 1.3, palisade 2.0, hillfort 2.5, walls 3.0, the highest present applies; site, home ground and archery still stack (13 §6.2). `loot` multiplies what raiders take; `assault: false` turns assaults into investment (22 §7.4). A text-only `defense` (WAR-03) stays valid | Behavior |
| **`tier`** (extended) | `tier`: Str · `min_pop`: Int (opt) · `needs`: List(Str) any-of structures (opt) · `radius`: Int (opt) · `claim_mult`: Num (opt) · `note` | A settlement holds the tier while `pop ≥ min_pop` and it has one of `needs`; it loses it after `TierDemoteSeasons` (4) consecutive seasons below `TierDemoteShare × min_pop` (0.85) or without the structure. `radius` replaces the claim radius; `claim_mult` multiplies Emit within it. Existing `{tier: hamlet}` and `{tier: village}` are unchanged | Tier |
| **`cohesion`** (extended) | `value`: Int −50–50 · `scope`: Enum(all, here, radius, capital) (opt, default all) · `radius`: Int (opt, with `radius`) · `note` | **From each civilization's KEY-3 every `cohesion` effect of an E3 project is a term of the Cohesion *target*** (22 §8.3) for the settlements in its scope: `here` = settlements that hold the project's structure, `radius` = settlements within *radius* walking tiles of one, `capital` = the capital. E0–E2 `cohesion` values are folded into the target's base 60 (every civilization has them by KEY-3) except the terms 22 §8.3 names (hearth, feast, shrine, GOV-03) | Behavior |

### 1.3 Stacking, scope and tool tiers
- **Across projects** effects multiply; **within one project** they add ([11 §1](11-e0-e2-project-specs.md#1-effect-primitives-syntax-used-in-the-tables)). For one stat the order is `set`, then `flat`, then `pct`, then `mult` (needed for sea reach: SEA-06 `flat +4` then WAY-05 `×2` gives 11 → 15 → 30).
- **Tool-tier multipliers are per figure, by the kit in hand, never stacked from projects.** They live in the tools registry (§4) as typed `mods`; MET-03/04/05 carry only the `good`, the Chop rung and a `rule` that points at the tier. A figure with a tin-bronze kit gets T10's multipliers once, whether its civilization knows MET-04, MET-05 or both.
- **`Cap(…)` values are targets** (as in 11): what the real effects should produce on an average settlement, checked by C1-E3 (§20.6). They are never applied directly, except `cap: group` steps.
- **Hazard values** are population averages (11 §1); MET-04's arsenic is a per-figure `risk` for that reason.
- **Scope of E3 effects:** civilization-wide unless the effect names a structure, tier or radius. `[Near]`/`[Has]` gates block use per settlement (02 §6, D10), never research.

---

## 2. Units and the E3 labor anchor
Units are those of [10 §1](10-e0-e2-actions-and-goods.md#1-units) (season, ws, R, L, tile), representation **1 : 1** (22 §3.4). The anchor for every per-capita number below is 22 §5.6's **settlement of 100 people**: ~35 adults (live age mix), **~38 ws a season**, of which ~60% (≈ 23 ws) is **tool work** (chop, farm, build, mine, quarry, craft) and the metal chain takes ≈ 3.5% of labor in steady state.

---

## 3. Goods registry (E3)

36 new goods. Barter values in **bold** are 22 §6.1's; the others are set here from their inputs (input value + labor at ~1 R per ws, rounded). Two new categories, `ore` and `arms`, are added to the goods schema enum (WP3). Luxuries measured in L (gold) count **1 luxury item per 0.1 L** for the luxury Cohesion term.

| Id | Good | Category | First from | Spoilage / durability | Load | Barter |
|---|---|---|---|---|---:|---:|
| G-CUORE | Copper ore | ore | KEY-3 | durable | 1 | 2 |
| G-ASORE | Arsenical copper ore | ore | MET-04 | durable | 1 | 3 |
| G-TINORE | Tin ore | ore | MET-05 | durable | 1 | 15 |
| G-IRONORE | Iron ore | ore | MET-08 | durable | 1 | 1 |
| G-BLOCK | Stone blocks (quarried; declared by KEY-3) | material | MIN-03 | durable | 1 | 0.5 |
| G-ASHLAR | Cut stone | processed | STN-11 | durable | 1 | **1** |
| G-SALT | Salt | material | MIN-06 | durable | 1 | **6** |
| G-ABRONZE | Arsenical bronze | metal | MET-04 | durable | 1 | 25 |
| G-TIN | Tin | metal | MET-05 | durable | 1 | **40** |
| G-BRONZE | Tin bronze | metal | MET-05 | durable | 1 | **30** |
| G-IRON | Iron bloom | metal | MET-08 | durable | 1 | **15** |
| G-GOLD | Gold and silver | luxury | MET-07 | durable | 1 | **40** |
| G-OIL | Blubber oil | fuel | HNT-10 | durable | 1 | 2 |
| G-MILK | Milk | food | HUS-05 | 50%/season | 0.5 | 0.8 |
| G-CHEESE | Cheese | food | HUS-05 | 5% | 0.4 | 2 |
| G-SALTED | Salted meat and fish | food | FOD-07 | 3% | 0.4 | 2 |
| G-WOOL | Wool | material | HUS-06 | durable | 1 | 1 |
| G-WOOLGARMENT | Wool garment | clothing | HUS-06 | 16 seasons | 0.3 | 6 |
| G-DYE | Dye | processed | CHM-04 | durable | 0.1 | 2 |
| G-SOAP | Soap | processed | CHM-05 | durable | 0.1 | 1 |
| G-WHEEL | Wheel | processed | MEC-03 | durable | 1 | 3 |
| G-CART | Ox cart | tool | TRN-05 | 40 seasons | 0 | 10 |
| G-HORSE | Horse | animal | HUS-07 | grazes; a pasture tile carries 2 | 0 | 15 |
| G-TOOL8 | Copper kit (T8) | tool | MET-03 | 12 ws | 0.3 | 6 |
| G-TOOL9 | Arsenical bronze kit (T9) | tool | MET-04 | 16 ws | 0.3 | 8 |
| G-TOOL10 | Tin bronze kit (T10) | tool | MET-05 | 24 ws | 0.3 | 10 |
| G-ARMS1 | Copper spear and dagger | arms | MET-03 | 24 seasons | 0.3 | 6 |
| G-ARMS2A | Arsenical bronze arms and shield | arms | WAR-05 | 24 seasons | 0.5 | 12 |
| G-ARMS2T | Tin bronze arms and shield | arms | WAR-05 | 24 seasons | 0.5 | 14 |
| G-CHARIOT | Chariot | arms | WAR-06 | 40 seasons | 0 | 40 |
| G-TURNED | Turned goods | luxury | WOD-06 | durable | 0.2 | **3** |
| G-FAIENCE | Faience | luxury | CER-05 | durable | 0.05 | **8** |
| G-GLASS | Glass beads and vessels | luxury | CER-06 | durable | 0.1 | **12** |
| G-DYEDCLOTH | Dyed cloth | luxury | FIB-08 | durable | 0.2 | **8** |
| G-SILK | Silk | luxury | FIB-09 | durable | 0.1 | **25** |
| G-FINEBRONZE | Fine bronze (statues, vessels) | luxury | MET-06 | durable | 0.3 | **30** |

Existing `G-COPPER` (barter **20**) and `G-CHAR` are unchanged. Arms durability is service life; an arms kit is also lost when its carrier dies or is captured (wounded keep it).

```yaml
# content/registries/goods.yaml — append
  - {id: G-CUORE, name: Copper ore, category: ore, first_from: [KEY-3], durability: durable, load: 1, barter: 2}
  - {id: G-ASORE, name: Arsenical copper ore, category: ore, first_from: [MET-04], durability: durable, load: 1, barter: 3}
  - {id: G-TINORE, name: Tin ore, category: ore, first_from: [MET-05], durability: durable, load: 1, barter: 15}
  - {id: G-IRONORE, name: Iron ore, category: ore, first_from: [MET-08], durability: durable, load: 1, barter: 1}
  - {id: G-BLOCK, name: Stone blocks, category: material, first_from: [MIN-03], durability: durable, load: 1, barter: 0.5}
  - {id: G-ASHLAR, name: Cut stone, category: processed, first_from: [STN-11], durability: durable, load: 1, barter: 1}
  - {id: G-SALT, name: Salt, category: material, first_from: [MIN-06], durability: durable, load: 1, barter: 6}
  - {id: G-ABRONZE, name: Arsenical bronze, category: metal, first_from: [MET-04], durability: durable, load: 1, barter: 25}
  - {id: G-TIN, name: Tin, category: metal, first_from: [MET-05], durability: durable, load: 1, barter: 40}
  - {id: G-BRONZE, name: Tin bronze, category: metal, first_from: [MET-05], durability: durable, load: 1, barter: 30}
  - {id: G-IRON, name: Iron bloom, category: metal, first_from: [MET-08], durability: durable, load: 1, barter: 15}
  - {id: G-GOLD, name: Gold and silver, category: luxury, first_from: [MET-07], durability: durable, load: 1, barter: 40}
  - {id: G-OIL, name: Blubber oil, category: fuel, first_from: [HNT-10], durability: durable, load: 1, barter: 2}
  - {id: G-MILK, name: Milk, category: food, first_from: [HUS-05], spoilage: 0.5, load: 0.5, barter: 0.8}
  - {id: G-CHEESE, name: Cheese, category: food, first_from: [HUS-05], spoilage: 0.05, load: 0.4, barter: 2}
  - {id: G-SALTED, name: Salted meat and fish, category: food, first_from: [FOD-07], spoilage: 0.03, load: 0.4, barter: 2}
  - {id: G-WOOL, name: Wool, category: material, first_from: [HUS-06], durability: durable, load: 1, barter: 1}
  - {id: G-WOOLGARMENT, name: Wool garment, category: clothing, first_from: [HUS-06], durability: "16 seasons", load: 0.3, barter: 6}
  - {id: G-DYE, name: Dye, category: processed, first_from: [CHM-04], durability: durable, load: 0.1, barter: 2}
  - {id: G-SOAP, name: Soap, category: processed, first_from: [CHM-05], durability: durable, load: 0.1, barter: 1}
  - {id: G-WHEEL, name: Wheel, category: processed, first_from: [MEC-03], durability: durable, load: 1, barter: 3}
  - {id: G-CART, name: Ox cart, category: tool, first_from: [TRN-05], durability: "40 seasons", load: 0, barter: 10}
  - {id: G-HORSE, name: Horse, category: animal, first_from: [HUS-07], durability: "grazes; a pasture tile carries 2", load: 0, barter: 15}
  - {id: G-TOOL8, name: Copper kit (T8), category: tool, first_from: [MET-03], durability: "12 ws", load: 0.3, barter: 6}
  - {id: G-TOOL9, name: Arsenical bronze kit (T9), category: tool, first_from: [MET-04], durability: "16 ws", load: 0.3, barter: 8}
  - {id: G-TOOL10, name: Tin bronze kit (T10), category: tool, first_from: [MET-05], durability: "24 ws", load: 0.3, barter: 10}
  - {id: G-ARMS1, name: Copper spear and dagger, category: arms, first_from: [MET-03], durability: "24 seasons", load: 0.3, barter: 6}
  - {id: G-ARMS2A, name: Arsenical bronze arms and shield, category: arms, first_from: [WAR-05], durability: "24 seasons", load: 0.5, barter: 12}
  - {id: G-ARMS2T, name: Tin bronze arms and shield, category: arms, first_from: [WAR-05], durability: "24 seasons", load: 0.5, barter: 14}
  - {id: G-CHARIOT, name: Chariot, category: arms, first_from: [WAR-06], durability: "40 seasons", load: 0, barter: 40}
  - {id: G-TURNED, name: Turned goods, category: luxury, first_from: [WOD-06], durability: durable, load: 0.2, barter: 3}
  - {id: G-FAIENCE, name: Faience, category: luxury, first_from: [CER-05], durability: durable, load: 0.05, barter: 8}
  - {id: G-GLASS, name: Glass beads and vessels, category: luxury, first_from: [CER-06], durability: durable, load: 0.1, barter: 12}
  - {id: G-DYEDCLOTH, name: Dyed cloth, category: luxury, first_from: [FIB-08], durability: durable, load: 0.2, barter: 8}
  - {id: G-SILK, name: Silk, category: luxury, first_from: [FIB-09], durability: durable, load: 0.1, barter: 25}
  - {id: G-FINEBRONZE, name: Fine bronze, category: luxury, first_from: [MET-06], durability: durable, load: 0.3, barter: 30}
```

---

## 4. Tool tiers T8–T10, arms kits and vehicles

### 4.1 Tool tiers (22 §5.4)

| Tier | Kit | Project | Make (ws) | Metal per kit | Service life (ws) | Enables | Per-figure multipliers (`mods`) |
|---|---|---|---:|---|---:|---|---|
| T7 | Polished axe | STN-08 | 0.3 | — | 10 | chop 4, clearing | (E2 reference) |
| **T8** | Copper kit | MET-03 | **0.1** (cast) | 0.12 L G-COPPER | **12** | chop 5 | quarry ×1.1 · mine ×1.1 · timber build labor ×0.95 |
| **T9** | Arsenical bronze kit | MET-04 | 0.1 | 0.15 L G-ABRONZE | **16** | chop 6 | quarry ×1.3 · mine ×1.3 · timber build labor ×0.85 · harvest labor ×0.8 · clearing labor ×0.8 (clearing ×1.25) |
| **T10** | Tin bronze kit | MET-05 | 0.1 | 0.15 L G-BRONZE | **24** | chop 6 | as T9 |

**Service life (decided here).** The durability of T8–T10 is the ws of tool work from a new kit to its **replacement** at ToolQuality 0.7 (10 §3); quality falls linearly 1.2 → 0.7 across it, so a supplied settlement never works a kit to breaking. This is the reading that reproduces 22 §5.6's metal demand (23 ws ÷ 20 ws × 0.15 L). A replaced kit is handed to non-tool use and discarded: **no metal returns**, so T17 holds. Two exceptions:
- **Upgrade remelt:** when the planner replaces a lower-tier kit early because a better tier can be cast, the old kit returns **50%** of its metal (arsenical bronze returns as copper, its arsenic burned off). Without it the copper-then-bronze re-kit of E3 would strip most of a settlement's standing wood (§20.8).
- **Dormancy tail:** a settlement that cannot cast its tier keeps using kits past 0.7 down to 0.5, where they break: ×1.4 the service life.

A worker does ~0.66 ws of tool work a season (23 ws ÷ 35 adults), so a T10 kit serves ~36 seasons, T9 ~24, T8 ~18. **22 §5.7's "existing kits keep working 1–3 years" is corrected to: the first figures fall back within a season of losing access (kits are of mixed age), a third within ~3 years (T9) to ~4 years (T10), all within 6–9 years.** The squeeze is slow, as 22 intends, and B12 ("rung 6 falls back within 12 seasons") is read as the fallback beginning and being visible, which this satisfies.

### 4.2 Arms kits and vehicles (22 §7.1)

| Kit | Project | Make | Strength (weapon) | Other | Service |
|---|---|---|---:|---|---|
| G-ARMS1 copper spear and dagger | MET-03 | 0.2 ws: 0.2 L copper + 0.5 L wood, forge | **1.8** | — | 24 seasons |
| G-ARMS2A arsenical bronze arms and shield | WAR-05 | 0.3 ws: 0.3 L arsenical bronze + 1 L wood + 1 leather (or hide) | **2.1** | shield | 24 seasons |
| G-ARMS2T tin bronze arms and shield | WAR-05 | 0.3 ws: 0.3 L tin bronze + 1 L wood + 1 leather (or hide) | **2.3** | shield | 24 seasons |
| G-CHARIOT | WAR-06 | 2 ws: 2 wheels + 2 L wood + 0.2 L bronze (either alloy) + 1 leather (or hide), forge; **2 horses** assigned | crew 2, each **×2 on open terrain** | crew casualties ×0.5; no effect against walls | 40 seasons |
| G-CART ox cart | TRN-05 | 1 ws (0.7 with WOD-06): 2 wheels + 2 L wood; one ox pair | — | 6 L (9 with NRG-02), haul ×5 on flat and trail tiles | 40 seasons |

- **Shields** (`casualties_taken 0.7`) act per side, linearly: a side's casualties × (1 − 0.3 × share of its fighters carrying an ARMS2 kit).
- **Open terrain** for chariots: grassland, steppe, savanna, desert, cleared land and trails; not forest, hills, wetland, mountains, snow, and never against walls or hillforts.
- **Militia arms (decided here):** the planner keeps **one arms kit per warrior plus 2 spares** per settlement; militia carry the spares, then the best stocked spear (22 §12 WAR-05). Under **Posture Fortify** the spare target is **25% of adults**. This keeps 22 §5.6's 0.15 L of bronze a year for arms in normal play and makes "bronze militia" (22 §7.5 rows 3–5) a Fortify choice.

```yaml
# content/registries/tools.yaml — append (new optional fields: metal, metal_per_kit, service_ws, mods, strength,
# casualties_taken, open_terrain_mult, crew, horses; WP3 adds them to the tools schema)
  - {id: t8, name: Copper kit, unlocked_by: [MET-03], make_time: 0.1, inputs: copper 0.12 L, durability: "12", enables: "chop 5", metal: G-COPPER, metal_per_kit: 0.12, service_ws: 12, mods: {quarry: 1.1, mine: 1.1, timber_build_labor: 0.95}}
  - {id: t9, name: Arsenical bronze kit, unlocked_by: [MET-04], make_time: 0.1, inputs: arsenical bronze 0.15 L, durability: "16", enables: "chop 6", metal: G-ABRONZE, metal_per_kit: 0.15, service_ws: 16, mods: {quarry: 1.3, mine: 1.3, timber_build_labor: 0.85, harvest_labor: 0.8, clearing_labor: 0.8}}
  - {id: t10, name: Tin bronze kit, unlocked_by: [MET-05], make_time: 0.1, inputs: tin bronze 0.15 L, durability: "24", enables: "chop 6", metal: G-BRONZE, metal_per_kit: 0.15, service_ws: 24, mods: {quarry: 1.3, mine: 1.3, timber_build_labor: 0.85, harvest_labor: 0.8, clearing_labor: 0.8}}
  - {id: arms1, name: Copper spear and dagger, unlocked_by: [MET-03], make_time: 0.2, inputs: "copper 0.2 L, wood 0.5 L", durability: "24 seasons", enables: "weapon 1.8", strength: 1.8}
  - {id: arms2a, name: Arsenical bronze arms and shield, unlocked_by: [WAR-05], make_time: 0.3, inputs: "arsenical bronze 0.3 L, wood 1 L, leather or hide 1", durability: "24 seasons", enables: "weapon 2.1, shield", strength: 2.1, casualties_taken: 0.7}
  - {id: arms2t, name: Tin bronze arms and shield, unlocked_by: [WAR-05], make_time: 0.3, inputs: "tin bronze 0.3 L, wood 1 L, leather or hide 1", durability: "24 seasons", enables: "weapon 2.3, shield", strength: 2.3, casualties_taken: 0.7}
  - {id: chariot, name: Chariot, unlocked_by: [WAR-06], make_time: 2, inputs: "2 wheels, wood 2 L, bronze 0.2 L, leather or hide 1; 2 horses", durability: "40 seasons", enables: "crew ×2 on open terrain", open_terrain_mult: 2, casualties_taken: 0.5, crew: 2, horses: 2}
  - {id: ox_cart, name: Ox cart, unlocked_by: [TRN-05], make_time: 1, inputs: "2 wheels, wood 2 L; 1 ox pair", durability: "40 seasons", enables: "haul 1 (×5 on flat and trail tiles), 6 L"}
```

---

## 5. Recipes: the metal chain and the other E3 crafts (new registry)

22 §5.3 as a registry, plus every other E3 good that is made from inputs. **New file `content/registries/recipes.yaml`** (WP3 adds its schema: `id`, `name`, `unlocked_by` [any-of], `structure` [any-of, optional], `inputs`/`outputs` maps of good → quantity (Fix64), `labor` ws, `order` Int, `practice` id, optional `bellows: true`, `note`). An input key `A|B` takes A first, then B. The planner runs recipes in the craft step **in ascending `order`, then id** (ore 0 → charcoal 10 → smelt 20 → alloy 30 → cast 40 → finish 50), each limited by its scarcest input (22 §5.3).

**Bellows (FIR-09).** A recipe flagged `bellows: true` run at a **Smelter** by a civilization with FIR-09 gets FIR-09's `smelt_yield ×1.4` and `charcoal_per_smelt ×0.8`: copper 2 L ore + **1.6** L charcoal → **0.7** L. Tin smelting is not a bellows recipe (22 §5.3 gives it one rate); the bloomery needs a Smelter anyway.

| Recipe | Inputs → output | Labor | Needs | Unlocked by | Order |
|---|---|---:|---|---|---:|
| burn_charcoal | 6 L wood (standing stock of a forest tile in range or at a camp) → 1 L charcoal | 0.4 | clamp site (§7.4) | FIR-08 | 10 |
| smelt_copper | 2 L copper ore + 2 L charcoal → 0.5 L copper (bellows: 1.6 → 0.7) | 0.5 | Kiln or Smelter | MET-02 | 20 |
| cosmelt_arsenical | 2 L arsenical ore + 2 L charcoal → 0.5 L arsenical bronze (bellows: 1.6 → 0.7) | 0.5 | Kiln or Smelter | MET-04 | 20 |
| smelt_tin | 1 L tin ore + 1 L charcoal → 0.5 L tin | 0.3 | Kiln or Smelter | MET-05 | 20 |
| bloomery | 2 L iron ore + 3 L charcoal → 0.5 L iron bloom | 0.8 | Smelter | MET-08 | 20 |
| alloy_bronze | 0.9 L copper + 0.1 L tin + 0.5 L charcoal → 1 L tin bronze | 0.2 | Kiln or Smelter | MET-05 | 30 |
| cast_t8 · cast_t9 · cast_t10 | 0.12 L copper · 0.15 L arsenical bronze · 0.15 L tin bronze → 1 kit | 0.1 | Forge | MET-03 · MET-04 · MET-05 | 40 |
| cast_arms1 | 0.2 L copper + 0.5 L wood → 1 G-ARMS1 | 0.2 | Forge | MET-03 | 40 |
| cast_arms2a · cast_arms2t | 0.3 L arsenical or tin bronze + 1 L wood + 1 leather (or hide) → 1 kit | 0.3 | Forge | WAR-05 | 40 |
| fine_bronze | 0.2 L bronze (tin, else arsenical) + 0.2 L clay + 0.3 L charcoal → 1 G-FINEBRONZE | 0.5 | Forge | MET-06 | 40 |
| dress_stone | 1 L stone blocks → 1 L cut stone | 0.5 (1.0 without a T8+ kit) | — | STN-11 | 50 |
| make_wheel | 1 L wood → 1 wheel | 1.0 (0.7 with WOD-06) | — | MEC-03 | 50 |
| build_cart | 2 wheels + 2 L wood → 1 ox cart | 1.0 (0.7 with WOD-06) | — | TRN-05 | 50 |
| build_chariot | 2 wheels + 2 L wood + 0.2 L bronze (tin, else arsenical) + 1 leather (or hide) → 1 chariot | 2.0 | Forge | WAR-06 | 50 |
| turned_goods | 0.5 L wood → 1 G-TURNED | 0.2 | — | WOD-06 | 50 |
| faience | 0.5 L stone + 0.2 L lime + 0.02 L copper + 0.5 L charcoal → 1 G-FAIENCE | 0.3 | Kiln | CER-05 | 50 |
| glass | 0.5 L stone + 0.2 L lime + 1 L charcoal → 1 G-GLASS | 0.4 | Smelter | CER-06 | 50 |
| dye | 0.5 L plant fiber + 0.1 pigment → 1 G-DYE | 0.1 | — | CHM-04 | 50 |
| dyed_cloth | 1 cloth + 1 dye → 1 G-DYEDCLOTH | 0.2 | — | FIB-08 | 50 |
| soap | 0.2 L lime + 0.2 R meat (rendered fat) → 1 G-SOAP | 0.1 | — | CHM-05 | 50 |
| cheese | 6 R milk → 4 R cheese | 0.05 | jars (CER-03) | HUS-05 | 50 |
| salt_food | 4 R fresh meat, fish or roots + 0.1 L salt → 4 R salted | 0.05 | — | FOD-07 | 50 |
| wool_garment | 2 yarn (wool) → 1 G-WOOLGARMENT | 0.5 | — | HUS-06 | 50 |

```yaml
# content/registries/recipes.yaml — new
schema: 1
recipes:
  - {id: burn_charcoal, name: Burn charcoal, unlocked_by: [FIR-08], inputs: {G-WOOD: 6}, outputs: {G-CHAR: 1}, labor: 0.4, order: 10, practice: burn_charcoal, note: "wood from the standing stock of one forest tile in range or at a mining camp (23 §7.4)"}
  - {id: smelt_copper, name: Smelt copper, unlocked_by: [MET-02], structure: [kiln, smelter], inputs: {G-CUORE: 2, G-CHAR: 2}, outputs: {G-COPPER: 0.5}, labor: 0.5, order: 20, practice: smelt, bellows: true}
  - {id: cosmelt_arsenical, name: Co-smelt arsenical bronze, unlocked_by: [MET-04], structure: [kiln, smelter], inputs: {G-ASORE: 2, G-CHAR: 2}, outputs: {G-ABRONZE: 0.5}, labor: 0.5, order: 20, practice: smelt, bellows: true}
  - {id: smelt_tin, name: Smelt tin, unlocked_by: [MET-05], structure: [kiln, smelter], inputs: {G-TINORE: 1, G-CHAR: 1}, outputs: {G-TIN: 0.5}, labor: 0.3, order: 20, practice: smelt}
  - {id: bloomery, name: Bloomery iron, unlocked_by: [MET-08], structure: [smelter], inputs: {G-IRONORE: 2, G-CHAR: 3}, outputs: {G-IRON: 0.5}, labor: 0.8, order: 20, practice: smelt}
  - {id: alloy_bronze, name: Alloy tin bronze, unlocked_by: [MET-05], structure: [kiln, smelter], inputs: {G-COPPER: 0.9, G-TIN: 0.1, G-CHAR: 0.5}, outputs: {G-BRONZE: 1}, labor: 0.2, order: 30, practice: smelt}
  - {id: cast_t8, name: Cast copper kit, unlocked_by: [MET-03], structure: [forge], inputs: {G-COPPER: 0.12}, outputs: {G-TOOL8: 1}, labor: 0.1, order: 40, practice: smelt}
  - {id: cast_t9, name: Cast arsenical bronze kit, unlocked_by: [MET-04], structure: [forge], inputs: {G-ABRONZE: 0.15}, outputs: {G-TOOL9: 1}, labor: 0.1, order: 40, practice: smelt}
  - {id: cast_t10, name: Cast tin bronze kit, unlocked_by: [MET-05], structure: [forge], inputs: {G-BRONZE: 0.15}, outputs: {G-TOOL10: 1}, labor: 0.1, order: 40, practice: smelt}
  - {id: cast_arms1, name: Cast copper spear and dagger, unlocked_by: [MET-03], structure: [forge], inputs: {G-COPPER: 0.2, G-WOOD: 0.5}, outputs: {G-ARMS1: 1}, labor: 0.2, order: 40, practice: smelt}
  - {id: cast_arms2a, name: Cast arsenical bronze arms, unlocked_by: [WAR-05], structure: [forge], inputs: {G-ABRONZE: 0.3, G-WOOD: 1, "G-LEATHER|G-HIDE": 1}, outputs: {G-ARMS2A: 1}, labor: 0.3, order: 40, practice: smelt}
  - {id: cast_arms2t, name: Cast tin bronze arms, unlocked_by: [WAR-05], structure: [forge], inputs: {G-BRONZE: 0.3, G-WOOD: 1, "G-LEATHER|G-HIDE": 1}, outputs: {G-ARMS2T: 1}, labor: 0.3, order: 40, practice: smelt}
  - {id: fine_bronze, name: Lost-wax casting, unlocked_by: [MET-06], structure: [forge], inputs: {"G-BRONZE|G-ABRONZE": 0.2, G-CLAY: 0.2, G-CHAR: 0.3}, outputs: {G-FINEBRONZE: 1}, labor: 0.5, order: 40, practice: craft}
  - {id: dress_stone, name: Dress stone, unlocked_by: [STN-11], inputs: {G-BLOCK: 1}, outputs: {G-ASHLAR: 1}, labor: 0.5, order: 50, practice: knap, note: "1.0 ws without a T8+ kit"}
  - {id: make_wheel, name: Make a wheel, unlocked_by: [MEC-03], inputs: {G-WOOD: 1}, outputs: {G-WHEEL: 1}, labor: 1, order: 50, practice: craft, note: "×0.7 labor with WOD-06"}
  - {id: build_cart, name: Build an ox cart, unlocked_by: [TRN-05], inputs: {G-WHEEL: 2, G-WOOD: 2}, outputs: {G-CART: 1}, labor: 1, order: 50, practice: craft, note: "×0.7 labor with WOD-06"}
  - {id: build_chariot, name: Build a chariot, unlocked_by: [WAR-06], structure: [forge], inputs: {G-WHEEL: 2, G-WOOD: 2, "G-BRONZE|G-ABRONZE": 0.2, "G-LEATHER|G-HIDE": 1}, outputs: {G-CHARIOT: 1}, labor: 2, order: 50, practice: craft}
  - {id: turned_goods, name: Turn wood, unlocked_by: [WOD-06], inputs: {G-WOOD: 0.5}, outputs: {G-TURNED: 1}, labor: 0.2, order: 50, practice: craft}
  - {id: faience, name: Glaze faience, unlocked_by: [CER-05], structure: [kiln], inputs: {G-STONE: 0.5, G-LIME: 0.2, G-COPPER: 0.02, G-CHAR: 0.5}, outputs: {G-FAIENCE: 1}, labor: 0.3, order: 50, practice: clay}
  - {id: glass, name: Make glass, unlocked_by: [CER-06], structure: [smelter], inputs: {G-STONE: 0.5, G-LIME: 0.2, G-CHAR: 1}, outputs: {G-GLASS: 1}, labor: 0.4, order: 50, practice: craft}
  - {id: dye, name: Make dye, unlocked_by: [CHM-04], inputs: {G-FIBER: 0.5, G-PIGMENT: 0.1}, outputs: {G-DYE: 1}, labor: 0.1, order: 50, practice: craft}
  - {id: dyed_cloth, name: Dye cloth, unlocked_by: [FIB-08], inputs: {G-CLOTH: 1, G-DYE: 1}, outputs: {G-DYEDCLOTH: 1}, labor: 0.2, order: 50, practice: fiber}
  - {id: soap, name: Make soap, unlocked_by: [CHM-05], inputs: {G-LIME: 0.2, G-MEAT: 0.2}, outputs: {G-SOAP: 1}, labor: 0.1, order: 50, practice: craft}
  - {id: cheese, name: Make cheese, unlocked_by: [HUS-05], inputs: {G-MILK: 6}, outputs: {G-CHEESE: 4}, labor: 0.05, order: 50, practice: preserve}
  - {id: salt_food, name: Salt food, unlocked_by: [FOD-07], inputs: {"G-MEAT|G-FISH|G-ROOT": 4, G-SALT: 0.1}, outputs: {G-SALTED: 4}, labor: 0.05, order: 50, practice: preserve}
  - {id: wool_garment, name: Make a wool garment, unlocked_by: [HUS-06], inputs: {G-YARN: 2}, outputs: {G-WOOLGARMENT: 1}, labor: 0.5, order: 50, practice: fiber}
```

---

## 6. Actions and method ladders

### 6.1 New actions

| Action | Class | Base rate | Unlocked by | Practice | Notes |
|---|---|---|---|---|---|
| `mine` Mine ore | family | **1 L/ws** at rung 0 (surface digging) | KEY-3 | mine | Output × **deposit factor** (§7.1) × rung × tool `mine` mod. The shallow copper-ore digging 10 §5.4 lists under MET-02 becomes a live job at KEY-3 |
| `pottery` Make pottery | family | 13.3 pots/ws (10 §5.3: 4 pots per 0.3 ws) | CER-02 (rung 0) | clay | E3 adds rung 1 |
| `haul` Haul | family | 40 L·tiles/ws (0.1 ws per L per 4 tiles, 22 §5.2) | start (rung 0) | haul | Carry mods of 10 §7 apply at rung 0 |
| `dress_stone` Dress stone | activity | 2 L/ws (1 without T8+) | STN-11 | knap | Recipe §5 |
| `salt` Make salt | activity | 1 L/ws | MIN-06 | mine | Salt deposits all year; coastal pans (coast tile, Warm, Arid or Temperate) in Summer only |
| `pan_gold` Work gold and silver | activity | 0.1 L/ws | MET-07 | mine | Gold/silver deposit tiles |
| `marine_hunt` Marine hunt | activity | 4 R/ws | HNT-10 | hunt | Water within 2 tiles of shore; injury 1.5%/ws; 1 L oil per 10 R caught |
| `tame_horses` Tame horses | activity | 1 animal/ws | HUS-07 | herd | ≤ 15% of a wild horse herd in range a year |
| `reel_silk` Reel silk | activity | 0.1 L/ws | FIB-09 | fiber | Needs a silk-moth region tile in range |

```yaml
# content/registries/actions.yaml — append
  - {id: mine, name: Mine ore, class: family, base_rate: 1, unit: L/ws}
  - {id: pottery, name: Make pottery, class: family, base_rate: 13.3, unit: pots/ws}
  - {id: haul, name: Haul, class: family, base_rate: 40, unit: "L·tiles/ws"}
  - {id: dress_stone, name: Dress stone, class: activity, base_rate: 2, unit: L/ws}
  - {id: salt, name: Make salt, class: activity, base_rate: 1, unit: L/ws}
  - {id: pan_gold, name: Work gold and silver, class: activity, base_rate: 0.1, unit: L/ws}
  - {id: marine_hunt, name: Marine hunt, class: activity, base_rate: 4, unit: R/ws}
  - {id: tame_horses, name: Tame horses, class: activity, base_rate: 1, unit: animals/ws}
  - {id: reel_silk, name: Reel silk, class: activity, base_rate: 0.1, unit: L/ws}
```

### 6.2 Ladders
The validator (V11) requires each ladder to start at rung 0 or 1 and never skip a rung. 22 §12 numbered some new rungs as if inside an older ladder (Mine r3, Till r3, Pottery r2, Haul r4, Sail r5); they are renumbered here (§21 C1). Rates are what the rung resolves to (05 §7.2), before tool, skill, health and research modifiers.

**Chop wood** (10 §5.1, base 2.0 L/ws)

| Rung | Method | Project | Tool | MethodMult | Wood/ws |
|---|---|---|---|---:|---:|
| 4 | Polished stone axe | STN-08 | T7 | 4.0 | 8.0 |
| **5** | **Copper axe** | MET-03 | T8 | **4.5** | 9.0 |
| **6** | **Bronze axe** | MET-04 *or* MET-05 | T9 or T10 | **6.0** | 12.0 |

Rung 6 is defined by both MET-04 and MET-05 (02 §12). **V11 gains an alternate-route rule (WP3):** two projects may define the same rung of a ladder when neither requires the other, some project lists both in one `requires_any` (MET-06, MET-08 and WAR-05 do), and the rungs have the same `mult`/`rate`. The rung resolves if either project is complete and the figure carries a T9 or T10 kit.

**Mine ore** (new family, base 1 L/ws × deposit factor)

| Rung | Method | Project | MethodMult | Notes |
|---|---|---|---:|---|
| 0 | Surface digging | KEY-3 | 1.0 | depth-1 deposits only |
| **1** | **Shaft** | MIN-04 | **1.5** | opens **depth-2** deposits (arsenical copper, tin lode, deep copper); needs a **Mine head** at the tile |
| **2** | **Fire-setting** | MIN-05 | **2.25** | ×1.5 on Shaft; burns **0.5 L wood per L ore** from the nearest forest stock |

Copper picks (T8) ×1.1, bronze picks (T9/T10) ×1.3 multiply on top (tool `mine` mod): shaft and bronze give 1.95, fire-set and bronze 2.9 (22 §5.4: ≈ ×2).

**Farm** (10 §4.4; rate per field tile per year)

| Rung | Method | Project | Rate | Labor per field tile per year |
|---|---|---|---:|---|
| 2 | Domesticated staples | AGR-02 | 8 R | sow 0.3 + tend 0.3 + harvest 0.4 = 1.0 ws |
| **3** | **Ard plough** | AGR-05 | **8 R** (yield unchanged) | **×0.5** (0.5 ws) while the settlement has **one ox pair per 4 fields** (6 with NRG-02); fields beyond its pairs work at rung 2 |

Ard is a labor rung: 22's "Till r3" is placed on `farm`, whose rung 2 already has the same rate (V11 allows equal). Irrigation, fallow and calendar act on yield and soil (§16–§17: AGR-06, AGR-07, NUM-06).

**Paddle** (watercraft, 10 §7 and 21 §3; MethodMult is the sea and river speed, `SeaCostTenths = 10 ÷ mult`)

| Rung | Method | Project | MethodMult | Notes |
|---|---|---|---:|---|
| 3 | Outrigger | SEA-04 | 2.5 | open sea within 11 tiles of land (21) |
| **4** | **Plank boat** | SEA-05 | **2.5** | cargo **12 L**; water routes ×3 capacity |
| **5** | **Square sail** | SEA-06 | **5.0** | sea speed ×2 (SeaCostTenths 4 → 2); open-sea reach 11 → 15 |

**Make pottery** (new family)

| Rung | Method | Project | MethodMult | Notes |
|---|---|---|---:|---|
| 0 | Hand-built | CER-02 | 1.0 | 13.3 pots/ws |
| **1** | **Potter's wheel** | CER-04 | **3.0** | only by a Potter at a Potter's workshop |

**Haul** (new family; MethodMult multiplies L·tiles per ws)

| Rung | Method | Project | MethodMult | Notes |
|---|---|---|---:|---|
| 0 | Carrying (arms, tumpline, travois, pack animals: 10 §7's carry mods) | start | 1.0 | 0.1 ws per L per 4 tiles |
| **1** | **Ox cart** | TRN-05 | **5.0** | on flat and trail tiles (grassland, steppe, savanna, desert, open woodland, any trail tile); off them the haul falls back to rung 0. Needs a G-CART and an ox pair. Water haul by plank boat ÷3 (SEA-05) |

### 6.3 Efficiency check (C6-E3, B11)

| Action | End E2 | End E3 | Growth | Target |
|---|---:|---:|---|---|
| Chop L/ws | 8.0 | 12.0 | ×1.5 | ✅ ×1.5–2 |
| Mine L ore/ws (copper) | 1.0 | 1.95 (shaft, bronze) · 2.9 (fire-setting) | ×1.95–2.9 | ✅ ×1.5–2 (fire-setting is a free pick that trades wood for it) |
| Field labor ws per tile per year | 1.0 | 0.5 × (0.6 + 0.4 × 0.8) = **0.46** | −54% | ✅ B11 ≥ 40% |
| Field yield R per tile per year | 8 | 8 × 1.5 irrigated × 1.1 calendar = 13.2 | ×1.65 (river) | ✅ |
| Haul L·tiles/ws | 40 | 200 on flat with carts | ×5 | ✅ (front-loaded, like E0's chop) |

---

## 7. Mining, camps, fuel and access

### 7.1 Deposits (22 §5.1; amounts are WP4's)

| Deposit | Mined as | Factor at rung 0 | Depth | Needs |
|---|---|---:|---|---|
| copper_ore | G-CUORE | 1.0 | 1 (depth 2 deposits need Shaft) | KEY-3 |
| arsenical_copper | G-ASORE | 1.0 | 2 | MET-04 + MIN-04 |
| tin (placer) · tin (lode) | G-TINORE | 0.5 | 1 · 2 | MET-05 (+ MIN-04 for the lode) |
| iron | G-IRONORE | 1.0 | 1–2 | MET-08 |
| gold_silver | G-GOLD (`pan_gold`, 0.1 L/ws) | — | 1–2 | MET-07 |
| salt | G-SALT (`salt`, 1 L/ws) | — | 1 | MIN-06 |

Deposits deplete by the ore taken (tracked; effectively inexhaustible in the chapter, 22 §5.6). WP3 adds `has_gate: "Arsenic ore"` to `arsenical_copper` and `has_gate: Tin` to `tin` in `deposits.yaml` so the validator can map MET-04's and MET-05's `[Has]` gates to deposits.

### 7.2 Mining jobs and mining camps (22 §5.2)
- **In range** (radius 3 of a settlement) a known deposit is worked as a settlement job. **Beyond it** a **mining camp** is needed: opened by the *Open a mine* Decree on any known deposit within mining reach (8 walking tiles; 12 with TRN-05; 16 along rivers and coasts with SEA-05), or by the planner on deposits inside its own owned tiles.
- A camp is a `mining_camp` structure (2 ws, 2 L wood) with **4 miners** (6 with MIN-04) from the parent, counted in the parent's cohorts. **Before MIN-04 it works depth-1 deposits from a surface pit; with MIN-04 it builds a `mine_head` (4 ws, 6 L wood) to dig depth-2 ore at the Shaft rung** (22 put the mine head on every camp, but also made it MIN-04's unlock; §21 C3).
- Claim: radius 1 at 75% of a six-person band's emission (Emit = √6 × 0.75 ≈ 1.84 ≥ T 0.7 across its radius), radius 2 with TER-06. Not a settlement for fission, Insight, Admin span or Dominion weight; a raid target (22 §7.5). Closes when the deposit is exhausted or fewer than 2 miners remain.
- **Hauling** ore to the parent is the `haul` job: 0.1 ws per L per 4 walking tiles at rung 0, ÷5 by cart on flat and trail tiles, ÷3 by plank boat on water.

### 7.3 Access and dormancy (22 §5.7)
- Each season each settlement **has** an alloy's metal if its own stores, or its polity's shared stores within 15 tiles (SOC-07) or 20 tiles (GOV-04), hold ≥ **0.05 L** of the metal (G-TIN or G-TINORE for tin bronze; G-ASORE or G-ABRONZE for arsenical).
- Without access no kit of that alloy is cast (`cast_t10` needs G-BRONZE, which needs G-TIN). Kits in hand keep working (§4.1 dormancy tail); figures fall back one rung at a time on replacement: **T10 → T9 if arsenical access → T8 if copper → T7**. The Methods overlay shows the reason ("no tin").
- MET-04/MET-05 show **Dormant** (02 §7) while **no** own settlement has access; per-settlement fallback is shown on the map regardless.
- **Upgrade push.** When a better tier becomes castable the planner re-kits its tool users, oldest kits first (remelt §4.1), with metal jobs capped at **10% of the settlement's labor** (`MetalPushLaborCap`); in steady state the chain takes ≈ 3.5%. T18 (70% on rung 6 within 12 seasons) needs ≈ 9% for 12 seasons in a settlement of 100 (§20.8).

### 7.4 Fuel and forests (22 §5.5)
- Forest tiles: standing stock **30 L**, regrowth **0.5 L/season** (×2 with WOD-04 within settlement radius), deadwood 2 L/tile/year (10 §5.1).
- **Clearing:** a forest tile whose standing stock falls below **5 L** becomes *cleared woodland* (moves and forages as open woodland); below **1 L for 4 seasons** it becomes grassland. It returns to forest at **15 L**. `ForestBand` for the view: 0 (< 5 L), 1 (5–14), 2 (15–24), 3 (≥ 25).
- **Charcoal clamps** are a work-site flag on the forest tile being burned (a view marker, not a registry structure): any forest tile in range or within 8 tiles (12 with TRN-05) of a settlement or a mining camp, at the haul cost. Charcoal wood comes from standing stock only (never deadwood); fire-setting (MIN-05) draws the same way.
- **Blubber oil** (HNT-10): settlements burn oil in hearths first, 1 L oil = 3 L wood.
- Other wood demand as 10 §5.1 (hearth 1.5 L/season, 3 L in Winter; kiln 4 L per batch), plus **bakery 2 L/season** and smelter firing (already in its charcoal).

---

## 8. Structures

16 new registry structures. Materials avoid goods a builder may not yet have: the Palace (GOV-05, whose closure lacks STN-11) uses **stone blocks**, and cut stone 1 : 1 with STN-11's −25% stone-building labor; the Market, Bakery, School and Caravanserai use no mudbrick (BLD-05 may still be a backfill when they are built).

| Structure | Unlocked by | Labor (ws) | Materials | Capacity / effect | Upkeep | Allowed at |
|---|---|---:|---|---|---|---|
| Mining camp | KEY-3 | 2 | wood 2 L | §7.2: 4–6 miners, claim radius 1 | — | known deposit tile beyond range |
| Mine head | MIN-04 | 4 | wood 6 L | Shaft rung; depth-2 deposits; spoil heap | — | deposit tile (camp or in range) |
| Smelter (bellows furnace) | FIR-09 | 3 | clay 4 L, stone 3 L, leather 2 | Bellows recipes; bloomery; glass | clay 1 L/yr (relining) | any settlement |
| Forge | MET-03 | 2 | stone 2 L, clay 2 L | Casting (kits, arms, fine bronze, chariot fittings); one Smith | — | any settlement |
| Potter's workshop | CER-04 | 2 | wood 3 L, clay 2 L | Pottery rung 1; one Potter | — | any settlement |
| Hall | BLD-07 | 10 | mudbrick 120, wood 6 L, cut stone 4 L | Houses 30; warmth +2 | — | any settlement |
| Temple | BEL-06 | 20 | mudbrick 120, cut stone 10 L | One Priest; Cohesion +6 here, +2 within 10 | — | Town or City, 1 each |
| Palace | GOV-05 | 30 | mudbrick 200, stone blocks or cut stone 20 L | Capital; King; the capital's Scribe sits here | — | the capital, 1 per civ |
| School | LRN-04 | 8 | wood 6 L, clay 4 L, stone 4 L | Two Scribes | — | Town or City, 1 each |
| Market | TRD-04 | 6 | wood 8 L, stone 2 L | Town tier at pop ≥ 60; one Trader; internal distribution +30% | — | settlement ≥ 40 people, 1 each |
| Caravanserai | TRD-05 | 8 | wood 6 L, stone 6 L | A Town on ≥ 2 routes: food cap +25%; caravans rest here | — | Town or City on ≥ 2 routes |
| Bakery | FOD-08 | 4 | clay 4 L, stone 4 L | Grain food value +20% for 40 people; one Baker | wood 2 L/season | Town or City |
| Canal | AGR-06 | 1 per field tile | — | Irrigated field: yield ×1.5 (×2 Arid or Desert, soil counts 100); flood loss ×0.5 | 0.1 ws/tile/yr | field tile within 2 of a river |
| Hillfort | DEF-04 | 15 | wood 10 L | Defense ×2.5; refuge for non-combatants and stores of own settlements within 6: loot ×0.25 | 0.2 ws/yr | hill tile within 4 of the settlement |
| Stone walls | DEF-05 | 30 per Village ring (×2 Town, ×3 City) | cut stone 60 L per Village ring (×2, ×3) | Defense ×3; no assault (investment only); loot ×0.25 outside the walls; do not burn | 0.5 ws/yr per ring | any settlement |
| Ziggurat (Wonder) | BLD-08 | 200 | cut stone 80 L, mudbrick 400 | Cohesion +15 within 8, +5 everywhere; claim +1.5 radius 8 | — | a City, 1 per civ |

```yaml
# content/registries/structures.yaml — append
  - {id: mining_camp, name: Mining camp, class: structure, unlocked_by: KEY-3, labor: 2, materials: wood 2 L, effect: "4 miners (6 with MIN-04) on one deposit tile beyond range; claim radius 1 at 75% of a six-person band (radius 2 with TER-06); raid target; closes when exhausted or under 2 miners", upkeep: "—"}
  - {id: mine_head, name: Mine head, class: structure, unlocked_by: MIN-04, labor: 4, materials: wood 6 L, effect: "Shaft rung and depth-2 deposits at this tile; spoil heap", upkeep: "—"}
  - {id: smelter, name: Smelter, class: structure, unlocked_by: FIR-09, labor: 3, materials: "clay 4 L, stone 3 L, leather 2", effect: "Bellows recipes (yield ×1.4, charcoal ×0.8); bloomery iron; glass", upkeep: clay 1 L/yr}
  - {id: forge, name: Forge, class: structure, unlocked_by: MET-03, labor: 2, materials: "stone 2 L, clay 2 L", effect: "Casting: tool kits, arms, fine bronze, chariot fittings; one Smith", upkeep: "—"}
  - {id: potters_workshop, name: "Potter's workshop", class: structure, unlocked_by: CER-04, labor: 2, materials: "wood 3 L, clay 2 L", effect: "Pottery rung 1 (wheel ×3); one Potter", upkeep: "—"}
  - {id: hall, name: Hall, class: structure, unlocked_by: BLD-07, labor: 10, materials: "mudbrick 120, wood 6 L, cut stone 4 L", effect: "Houses 30; warmth +2", upkeep: "—"}
  - {id: temple, name: Temple, class: structure, unlocked_by: BEL-06, labor: 20, materials: "mudbrick 120, cut stone 10 L", effect: "Town or City, one each; one Priest; Cohesion +6 here, +2 within 10", upkeep: "—"}
  - {id: palace, name: Palace, class: structure, unlocked_by: GOV-05, labor: 30, materials: "mudbrick 200, stone blocks or cut stone 20 L", effect: "The capital, one per civilization; the King; the capital's Scribe", upkeep: "—"}
  - {id: school, name: School, class: structure, unlocked_by: LRN-04, labor: 8, materials: "wood 6 L, clay 4 L, stone 4 L", effect: "Town or City, one each; two Scribes", upkeep: "—"}
  - {id: market, name: Market, class: structure, unlocked_by: TRD-04, labor: 6, materials: "wood 8 L, stone 2 L", effect: "Settlement of 40 or more, one each; Town tier at 60; one Trader; internal distribution +30%", upkeep: "—"}
  - {id: caravanserai, name: Caravanserai, class: structure, unlocked_by: TRD-05, labor: 8, materials: "wood 6 L, stone 6 L", effect: "Town or City on 2 or more routes: food cap +25%; route capacity ×2 through it", upkeep: "—"}
  - {id: bakery, name: Bakery, class: structure, unlocked_by: FOD-08, labor: 4, materials: "clay 4 L, stone 4 L", effect: "Town or City; grain food value +20% for 40 people; one Baker", upkeep: wood 2 L/season}
  - {id: canal, name: Canal, class: structure, unlocked_by: AGR-06, labor_note: "1.0 per field tile within 2 of a river", materials: "—", effect: "Irrigated field: yield ×1.5 (×2 on Arid and Desert, soil counts 100); flood loss ×0.5", upkeep: "0.1 ws/tile/yr"}
  - {id: hillfort, name: Hillfort, class: structure, unlocked_by: DEF-04, labor: 15, materials: wood 10 L, effect: "On a hill within 4; defense ×2.5; refuge within 6: raid loot ×0.25", upkeep: "0.2 ws/yr"}
  - {id: stone_walls, name: Stone walls, class: structure, unlocked_by: DEF-05, labor_note: "30 per Village ring (×2 Town, ×3 City)", materials: "cut stone 60 L per Village ring (×2, ×3)", effect: "Defense ×3; no assault in E3 (investment only); loot ×0.25 outside the walls; do not burn", upkeep: "0.5 ws/yr per ring"}
  - {id: ziggurat, name: Ziggurat (Wonder), class: wonder, unlocked_by: BLD-08, labor: 200, materials: "cut stone 80 L, mudbrick 400", effect: "At a City; Cohesion +15 within 8, +5 everywhere; claim +1.5 radius 8", upkeep: "—"}
```

---

## 9. Roles

Roles are **per structure** from E3 (22 §8.4, overriding 09 §9's shares). Insight per figure per year at Standard pace, before `InsightCalibration` 1.6; SOC-08 adds +10% to role Insight. New optional field `staffing` (WP3).

| Role | Unlocked by | Insight/yr | Staffing | Does |
|---|---|---:|---|---|
| Smith (existing) | MET-02 | 0 | 1 per Forge | runs casting; +30% work rate as a full-time specialist in Towns and Cities (SOC-08) |
| Collier (existing) | FIR-08 | 0 | 1 per settlement burning charcoal | burns clamps |
| **Miner** | MIN-04 | 0 | every worker on a `mine` job | full-time: skill gain ×1.5 on mine (as LRN-03); pick outfit |
| **Potter** | CER-04 | 0 | 1 per Potter's workshop | works the wheel (pottery rung 1) |
| **Priest** | BEL-06 | **16** (+4 NUM-06) | 1 per Temple | ritual; calendar keeping |
| **Scribe** | **COM-05** (registry says LRN-04: corrected) | **24** (×1.5 COM-06) | **1 at the capital** (in the Palace once built) **+ 2 per School** | records; literacy (adoption without distance lag) |
| **King** | GOV-05 | 0 | 1 at the capital | the Palace figure; the capital marker |
| **Trader** | TRD-04 | 0 | 1 per Market | leads the market's caravans; a Compact needs a Trader or an Envoy |
| **Baker** | FOD-08 | 0 | 1 per Bakery | runs the bakery |
| **Envoy** | DIP-04 | 0 | 1 per civilization, at the capital | carries pacts, treaties and compacts |

**COM-05's Scribe:** 22 gives "one per Palace", but the Palace comes two projects later (GOV-05), so COM-05 alone would have no role and no visible change. The capital gets its Scribe at COM-05; the Palace houses that same Scribe, it does not add one (§21 C5). **Capital before GOV-05:** the civilization's oldest settlement (its start); with GOV-05 the settlement with the Palace.

```yaml
# content/registries/roles.yaml — change scribe, add the rest (new optional field: staffing)
  - {id: priest, name: Priest, insight: 16, unlocked_by: BEL-06, staffing: "1 per temple"}
  - {id: scribe, name: Scribe, insight: 24, unlocked_by: COM-05, staffing: "1 at the capital; 2 per school"}
  - {id: smith, name: Smith, insight: 0, unlocked_by: MET-02, staffing: "1 per forge"}
  - {id: collier, name: Collier, insight: 0, unlocked_by: FIR-08, staffing: "1 per settlement burning charcoal"}
  - {id: miner, name: Miner, insight: 0, unlocked_by: MIN-04, staffing: "every worker on a mine job"}
  - {id: potter, name: Potter, insight: 0, unlocked_by: CER-04, staffing: "1 per potter's workshop"}
  - {id: king, name: King, insight: 0, unlocked_by: GOV-05, staffing: "1 at the capital"}
  - {id: trader, name: Trader, insight: 0, unlocked_by: TRD-04, staffing: "1 per market"}
  - {id: baker, name: Baker, insight: 0, unlocked_by: FOD-08, staffing: "1 per bakery"}
  - {id: envoy, name: Envoy, insight: 0, unlocked_by: DIP-04, staffing: "1 per civilization, at the capital"}
```

---

## 10. Practice (Know-how) for E3

New practice ids used by the E3 projects (22 §12 names `scribe`, `sail` and `ride`) and the charcoal job:

```yaml
# content/registries/practice.yaml — append
  - {id: scribe, knowhow: {COM: 60, NUM: 20, LRN: 20}}
  - {id: sail, knowhow: {SEA: 70, WAY: 30}}
  - {id: ride, knowhow: {TRN: 60, HUS: 30, WAR: 10}}
  - {id: burn_charcoal, knowhow: {FIR: 60, WOD: 40}}
```

Which practice each E3 job generates (05 §16.2; 35% of the practising figure's Commons Insight, 10 §11):

| Job | Practice | Lineages |
|---|---|---|
| mine, salt, pan_gold, haul ore from a camp (the mining leg) | mine | MIN 80, STN 20 |
| smelt, alloy, cast (recipes order 20–40), bloomery | smelt | MET 70, FIR 30 |
| burn_charcoal, fire-setting | burn_charcoal | FIR 60, WOD 40 |
| dress_stone | knap | STN 85, MIN 15 |
| pottery wheel, faience | clay | CER 80, FIR 20 |
| glass, dyes, soap, wheels, carts, chariots, turned goods, fine bronze | craft | (10's craft split) |
| dyed cloth, wool garments, silk | fiber | FIB 90, CHM 10 |
| cheese, salting | preserve | FOD 100 |
| bakery work | cook | FIR 50, FOD 50 |
| marine hunt | hunt | HNT 70, WAR 15, INQ 15 |
| tame horses, milking, shearing | herd | HUS 90, TRN 10 |
| riding (scouts, messengers, herders, expeditions) | ride | TRN 60, HUS 30, WAR 10 |
| ox carts, caravans | haul | TRN 70, WAY 30 |
| plank boats (rivers, coast) | paddle | SEA 100 |
| sailing and open-sea voyages | sail | SEA 70, WAY 30 |
| market trade, compacts | trade | TRD 70, DIP 30 |
| Scribes (records), school | scribe | COM 60, NUM 20, LRN 20 |
| Priests (ritual), processions | ritual | BEL 80, ART 20 |
| King, council, redistribution | council | GOV 100 |
| building walls and hillforts | build (defensive) | BLD 80, MEC 10, DEF 10 |
| guards at walls, investment, levy | guard / fight | WAR 60, DEF 40 |

---

## 11. Caps: FoodCap deltas and EffectivePopCap by tier

### 11.1 FoodCap trajectory (C1-E3, 09 §5.2–5.3)
Cap targets of the steps on KEY-4's closure plus the take-weighted free picks, at their 09 §5.2 times (share of the era):

| At | Step | × | Average settlement FoodCap |
|---:|---|---:|---:|
| 0 | E2 end | — | 88 |
| .05–.20 | HUS-02 · HUS-03 · HUS-04 (backfills) | 1.04 · 1.08 · 1.05 | 103.8 |
| .15 | FOD-06 Granaries (backfill) | 1.05 | 109.0 |
| .35 | AGR-05 Ard Plough | 1.17 | 127.5 |
| .40 | TRN-05 Wheeled Carts | 1.06 | 135.2 |
| .50 | HUS-05 Dairying | 1.08^0.5 | 140.5 |
| .55 | AGR-06 Irrigation Canals | 1.17 | 164.4 |
| .55 | TRD-05 Market Towns | 1.05 | 172.6 |
| .60 | AGR-07 Fallow & Manuring | 1.10^0.6 | 182.7 |
| .70 | GOV-04 Tribute & Redistribution | 1.04 | **190.0** |

Product **×2.16** (88 → 190, 22 §9). **Of it, ×1.24 comes from the four E1–E2 backfills** KEY-4's closure forces (HUS-02/03/04, FOD-06); the E3 projects alone give **×1.74**. 22 §9 attributes all of ×2.16 to E3 steps; the total is right (§21 C7). FOD-08 (a Town-only +20% for 40 people), NUM-06 (+10% field yield) and FOD-07/HUS-05's Lean steps are outside the average-settlement product, as in 09.

**Lean:** HUS-05 ×1.05, FOD-07 ×1.05. **Housing** (never binds): 395 → ×1.5 BLD-05 (backfill) → ×1.05 WOD-05 → ×1.6 SOC-09 = **995** (09's 948 plus WOD-05); BLD-07's hall ×1.2 where taken. **Group:** 2,000 → 10,000 (SOC-09) × F.

### 11.2 EffectivePopCap by tier
`EffectivePopCap = min(FoodCap, HousingCap, GroupCap × F)` (05 §9.1). Food binds in every tier in E3.

| Tier | Holds when | FoodCap (fixture) | HousingCap | GroupCap × F | Binds | Live band at KEY-4 (22 §10) |
|---|---|---|---|---|---|---|
| Village | (E2 rule) | 190 average settlement at KEY-4 | ~995 | 2,000 × F (SOC-07) | Food | 20–60 |
| Town | pop ≥ 60 and a Market (TRD-04) | ×1.19 on ≥ 2 routes (TRD-05's +25% net of the 1.05 already averaged) ≈ 226; Bakery ≈ ×1.04 more | ×1.6 (SOC-09 dense dwellings in Towns and Cities) | 2,000 × F; 10,000 × F with SOC-09 | Food | 60–150 |
| City | pop ≥ 120 and a Temple or Palace (SOC-09) | as Town; an irrigated river City ≈ ×1.05 more than the average (its whole field share is irrigated) | as Town | 10,000 × F | Food | 120–250 |
| Capital | the Palace (GOV-05) | as its tier | as its tier | as its tier | Food | — |

The fixture numbers are the mean-field "average settlement" that C1-E3 checks. Live settlements sit below them because they fission at 85% of a *local* cap on uneven, overlapping ranges (22 §1.4 finding 3); T10/T11 are therefore set on the live sim, not on this table.

---

## 12. Constants

New constants for `content/constants/balance.yaml` (`{id, value, unit, source}`). Values already carried by an effect or registry entry are not repeated here. *P* marks 22's provisional values.

| Id | Value | Unit | Source |
|---|---:|---|---|
| **Research, scoring, chapter** | | | |
| LegacyMasteryPerEra | 2 | × (era + 1) per project | 22 §3.3 |
| LegacyFirstPerEra | 3 | × (era + 1) | 22 §3.3 |
| LegacyFirstKeystoneMult | 4 | × First | 22 §3.3 (Keystones and Wonders) |
| LegacyWonderBuiltPerEra | 25 | × (era + 1) | 22 §3.3 |
| ChapterCountdownSeasons | 12 | seasons | 22 §3.1 |
| ChapterCapYear | 64 | year | 22 §3.1 |
| OutCity | 1 | Insight/yr (× RoleScale 8 = 8) | 22 §8.1 (SOC-09) |
| **Mandate and Decrees** (base Mandate: KEY-3's `mandate` effect) | | | |
| MandateRechargeMin | 4 | seasons | 22 §4.2 |
| DecreeCostFound · Expedition · Mine · Levy · Corvee · Compact | 2 · 1 · 1 · 1 · 1 · 1 | Mandate | 22 §4.3 (Compact 0 with DIP-04) |
| FoundDonorMinPop · FoundDonorMinFill | 20 · 0.6 | people · × EffectivePopCap | 22 §4.3 |
| FoundShare · FoundMinDaughter | 0.4 · 8 | share · people | 22 §4.3 |
| SettleReach · SettleReachCartBonus · SettleReachSea | 14 · 6 · 24 | tiles | 22 §4.3, 21 §3 |
| FoundOwnMinDist · FoundRivalMinDist | r + 1 · 9 | tiles | 22 §4.3 |
| FoundClaimBoost · FoundClaimBoostYears · FoundStartCohesion | 1.5 · 2 · 60 | × · years · C | 22 §4.3 (TER-05) |
| ExpeditionSize · ExpeditionRangeMult | 4 · 2 | adults · × Reach | 22 §4.3 (Reach E3 15) |
| ExpeditionSight · ExpeditionRevealRadius · ExpeditionContactRadius | 3 (+1 riding) · 5 · 2 | tiles | 22 §4.3 |
| RidingSpeedMult · RidingRangeMult | 2 · 1.5 | × | 22 §4.3, TRN-06 |
| MineReach · MineReachCarts · MineReachWater | 8 · 12 · 16 | walking tiles | 22 §4.3 |
| LevySeasons · LevyOtherAdultsShare · LevyTravelSeasons | 2 · 0.5 · 2 | seasons · share · seasons | 22 §4.3 |
| CorveeSeasons · CorveeLaborMult · CorveeCohesion | 2 · 1.5 · −3 | seasons · × · C | 22 §4.3 |
| **Metal, mining and fuel** | | | |
| MiningCampCrew · MiningCampCrewShaft · MiningCampMinCrew | 4 · 6 · 2 | miners | 22 §5.2 |
| MiningCampBand · MiningCampEmit · MiningCampRadius | 6 · 0.75 · 1 | people · × · tiles (2 with TER-06) | 22 §5.2 |
| HaulWaterDiv | 3 | ÷ haul labor on water | 22 §5.2 (SEA-05) |
| MetalAccessMin | 0.05 | L | 22 §5.7 |
| AccessShareReach · AccessShareReachGOV04 | 15 · 20 | tiles | 22 §5.7 |
| KitReplaceQuality · KitBreakQuality | 0.7 · 0.5 | ToolQuality | 10 §3; §4.1 |
| UpgradeRemeltShare | 0.5 | share of kit metal | §4.1 (decided here) |
| MetalPushLaborCap | 0.10 | share of labor | §7.3 (decided here) |
| WarriorSpareKits · MilitiaArmsShareFortify | 2 · 0.25 | kits · × adults | §4.2 (decided here) |
| FireSettingWoodPerL | 0.5 | L wood per L ore | 22 §5.2 |
| ForestStock · ForestRegrowth | 30 · 0.5 | L · L/season | 10 §5.1 |
| ForestClearedBelow · ForestGrassBelow · ForestGrassSeasons · ForestRestoreAt | 5 · 1 · 4 · 15 | L · L · seasons · L | 22 §5.5; §7.4 |
| CharcoalReach · CharcoalReachCarts | 8 · 12 | tiles | 22 §5.5 |
| OilWoodEquiv · OilPerR | 3 · 0.1 | L wood per L · L oil per R | 22 §12 (HNT-10) |
| **Food** | | | |
| ArdFieldsPerPair · ArdFieldsPerPairNRG02 | 4 · 6 | fields per ox pair | 22 §9; NRG-02 |
| CanalRiverDist · CanalLabor | 2 · 1 | tiles · ws per tile | 22 §9 |
| ManureCattlePer4 · ManureSheepPer4 | 2 · 6 | animals per 4 fields | 22 §9 |
| MarketTownFoodCap · MarketTownRoutes | 1.25 · 2 | × · routes | 22 §8.5 (TRD-05) |
| BakeryGrainValue · BakeryPeople | 0.2 · 40 | share · people | 22 §12 (FOD-08) |
| HorseTameShare · HorsePerPasture · HorseTameLabor | 0.15 · 2 · 1 | per year · per tile · ws | 22 §12 (HUS-07) |
| MarineHuntShore · MarineHuntInjury | 2 · 0.015 | tiles · per ws | 22 §12 (HNT-10) |
| PartySupplySeasons | 1 | seasons (2 with FOD-07) | 22 §7.4, §21 C4 |
| **State and Cohesion** | | | |
| CohesionBase · CohesionStep | 60 · 0.25 | C · share per season | 22 §8.3 |
| CohesionWorkMid · CohesionWorkRange · CohesionWorkSlope | 65 · 35 · 0.15 | C · C · × | 22 §8.3 (clamped 0.85–1.15) |
| CohesionFissionBelow · CohesionFissionTrigger | 35 · 0.75 | C · × cap | 22 §8.3 |
| SecessionBelow · SecessionSeasons · SecessionRelations | 15 · 8 · 20 | C · seasons · Relations | 22 §8.3 |
| CohesionRaided · CohesionRaidedBEL01 · CohesionRaidYears | −10 · −5 · 2 | C · C · years | 22 §8.3 |
| CohesionFamine · CohesionLossPer2Pct · CohesionCrowding | −20 · −1 · −10 | C per unit | 22 §8.3 |
| OverreachFreeTiles · OverreachDivisor · SpanPenalty | 8 · 2 · 3 | tiles · ÷ · C per settlement | 22 §8.2–8.3 |
| ConqueredCohesion · ConqueredRecovery · CaptureAssimilationSiege | −20 · 2 · −0.10 | C · C/season · share | 13 §8.3, 22 §7.4 |
| TownMinPop · CityMinPop · TierDemoteShare · TierDemoteSeasons | 60 · 120 · 0.85 · 4 | people · people · × · seasons | 22 §8.1; §1.2 |
| HinterlandRadius · HinterlandEmit · CapitalClaimMult | 7 · 0.5 · 1.25 | tiles · × · × | 22 §8.1 |
| SpecialistRateMult | 1.3 | × | 22 §8.5 (SOC-08) |
| **War** | | | |
| WoundedDeathShareMED04 | 0.32 | share | 22 §7.1 (base 0.40, 13 §7.2) |
| HillfortSiteWithin · HillfortRefugeRadius · RefugeLootMult | 4 · 6 · 0.25 | tiles · tiles · × | 22 §7.2 |
| ChariotSurprise · ChariotTravel | 10 · 1.5 | points · × | 22 §7.1 |
| InvestRatio · InvestCapitulateLow · InvestCapitulateZero | 1.2 · 0.35 · 0.70 | × · per season · per season | 22 §7.4 |
| InvestCohesionTarget · InvestCohesionHome | −5 · −2 | C per season | 22 §7.4 |
| SallyDefMult · SallyThreshold | 1.1 · 0.8 | × · × besiegers | 22 §7.4 |
| SiegeForageShare · SiegeDesertion · SiegeSupplyTiles | 0.5 · 0.10 · 8 | share · per season · tiles | 22 §7.4 |
| RaidPartyMin · RaidPartyShareMin · RaidPartyShareMax | 4 · 0.15 · 0.30 | figures · × adults | 22 §7.5 |
| **Trade and diplomacy** | | | |
| BarterGain | 0.10 | share (0.05 with NUM-04) | 13 §4.2 |
| ScarcityClampLow · ScarcityClampHigh | 0.5 · 3 | × | 13 §4.2 |
| CompactHorizonSeasons · CompactMinRelations | 4 · 0 | seasons · Relations (−30 with DIP-05) | 22 §6.2 |
| EmbargoRelations | −10 | Relations | 22 §4.3 |
| InviteSeasons · InviteRelations · InviteGift | 8 · 30 · 10 | seasons · Relations · value | 22 §4.3 |
| CaravanRaidRelations · CaravanRaidReputation · CaravanEscort | −30 · −10 · 2 | Relations · reputation · warriors | 22 §6.3 |
| PactYears · PactBreakRelations · PactBreakReputation · TreatyYears | 8 · −60 · −30 · 10 | years · · · years | 22 §12 (DIP-04, DIP-05) |
| TinFolkAdults · TinFolkElders · TinFolkChildren | 9 · 2 · 13 | people | 22 §6.4 |
| TinFolkRadius · TinFolkMinDist · FreeFolkDefense | 3 · 8 · 0.8 | tiles · tiles · × | 22 §6.4, §11 |
| TinFolkRaidReputation · TinFolkCaptureReputation · TinFolkTraderRelations | −5 · −10 · −20 | | 22 §6.4 |
| **Health** | | | |
| PlagueR0 · PlagueCFR | 2.0 · 0.35 | — · share | 22 §10, 09 §10 |
| PoxEndemicPop | 1,000 | people in one settlement | 22 §10 |

**Era registry:** `eras.yaml` E3 `base_cost: 180 → 200` (*P*, 22 §15 T4); breadth 21, claim radius 4 (villages; Town 5 and City 6 come from `tier`), reach 15 unchanged.

---

## 13. Changes to existing content

| File | Change | Why |
|---|---|---|
| `catalog/keystones.yaml` KEY-3 | **append effects** (below) | E3's state switches on at each civilization's KEY-3 (22 §4.2, §5.2, §8.2–8.3, §6.4, §10). KEY-3 completes near minute 30, so the 12- and 24-minute goldens are untouched |
| `catalog/keystones.yaml` KEY-4 | status `draft → numeric`, effects §17 | 22 §3.1 |
| 65 E3 projects | status `draft → numeric`; replace the `prose` effect by §15–§17's effects, add `practice`, `flavor`, `moment` | B25 |
| `registries/eras.yaml` | E3 `base_cost 400` | current cadence, 07c §10.1 |
| `registries/roles.yaml` | `scribe.unlocked_by LRN-04 → COM-05`; staffing; new roles | §9 |
| `registries/deposits.yaml` | `has_gate` on `arsenical_copper` and `tin` | §7.1 |
| goods, tools, actions, structures, practice, constants | appends (§3–§12) | — |
| `registries/recipes.yaml` | new file | §5 |
| `src/CivSim.Content` (WP3) | `EffectSpecs`: `admin`, `mandate`, `sanitation`, typed `defense`, extended `tier` and `cohesion` (§1.2). Goods category enum + `ore`, `arms`. Tools schema optional `metal`, `metal_per_kit`, `service_ws`, `mods`, `strength`, `casualties_taken`, `open_terrain_mult`, `crew`, `horses`. Roles optional `staffing`. Recipes schema. **V11 alternate-route rule** (§6.2). New check: every `[Has]` target maps to a deposit `has_gate`; every `mandate.decree` is a known Decree | §1.2, §6.2 |

```yaml
# KEY-3: append to effects
      - {kind: mandate, start: 1, cap: 2, recharge: 8, note: "Mandate and the six Decrees, plus the Embargo toggle (22 §4)"}
      - {kind: admin, span: 4, note: "Admin span base: many villages, one voice (22 §8.2)"}
      - {kind: unlock_action, action: mine}
      - {kind: good, id: G-CUORE}
      - {kind: good, id: G-BLOCK, note: "quarried blocks become their own good (MIN-03's output; the E2 economy books them as stone)"}
      - {kind: build, structure: mining_camp}
      - {kind: rule, text: "Cohesion becomes live for this civilization: target-seeking, Overreach and Admin span apply (22 §8.3)"}
      - {kind: rule, text: "plague becomes possible (R0 2.0, CFR 35%, 09 §10); compacts and caravans carry it"}
      - {kind: event, text: "at the first KEY-3 in the world, Tin Folk appear on free far and island tin placers (22 §6.4)"}
```

---

## 14. Costs

`Cost = EraBase × TypeMult` (02 §8.3), current **EraBase E3 = 400**: D/M **400**, S **480**, R **320**, I **600**, W **800**, K **1,000**. **No overrides** (no `cost_reason` needed). List costs; play reduces them by Diffusion, EraLag and Know-how.

| Theme | Projects | M | D | S | I | W | Sum of list costs |
|---|---:|---:|---:|---:|---:|---:|---:|
| Craft | 24 | 15 | 8 | 0 | 0 | 1 | 10,000 |
| Kin | 21 | 7 | 2 | 4 | 8 | 0 | 10,320 |
| Reach | 20 | 11 | 1 | 3 | 5 | 0 | 9,240 |
| **E3** | **65** | **33** | **11** | **7** | **13** | **1** | **29,560** (average 454.8) |
| KEY-4 | 1 | | | | | | 1,000 |

Against [11 §2](11-e0-e2-project-specs.md#2-costs): current E2 is 52 projects, 8,925 list cost, average 171.6. KEY-4's closure costs **8,560** (18 projects) + 1,000 at current list prices.

---

## 15. E3 Craft (24)

Columns: **Sim** = weight (● new system · ◐ new parameters on a WP-built system · ○ goods or modifiers only) and the capability it needs (§19): **MC** metal chain, **TL** tool tiers and ladders, **FU** fuel and forests, **GD** goods by recipe, **FD** food and herds, **ST** tiers, roles by structure and Cohesion, **HL** health, **CB** combat and fortification, **TR** trade and compacts, **DP** pacts and treaties, **MD** Mandate and Decrees, **MV** movement, sea and reach, **RS** research, Legacy and the chapter. **Visible change** gives the `VisibleChange` class (ResearchCatalog) and what the player sees within a season (B4).

| ID | Project | Type | Cost | Requires | Practice | Visible change | Sim |
|---|---|---|---:|---|---|---|---|
| STN-11 | Dressed Masonry | M | 200 | STN-08, MIN-03 | build 50 · mine 50 | NewAction: masons dress blocks at quarries; stone buildings take ashlar sprites | ◐ GD |
| FIR-09 | Bellows & Tuyères | M | 200 | FIR-08, FIB-03 | smelt 50 · tend_fire 50 | Structure: smelter with working bellows | ◐ MC |
| WOD-05 | Joinery | M | 200 | WOD-03, MET-03 | build 50 · chop 50 | Behavior: framed-timber houses; build-speed badge | ○ |
| WOD-06 | Bow Lathe | M | 200 | WOD-05, MEC-03 | craft 50 · chop 50 | Good: turner at a bow lathe; turned goods in stores | ○ GD |
| CER-04 | Potter's Wheel | M | 200 | CER-03, MEC-03 | clay 100 | ToolOrMethod: potter's wheel at a workshop; Potter outfit | ○ TL |
| CER-05 | Glazes & Faience | M | 200 | CER-04, CHM-03 | clay 50 · tend_fire 50 | Good: turquoise faience in ornaments and stores | ○ GD |
| CER-06 | Glassmaking | D | 200 | CER-05, FIR-09 | tend_fire 50 · craft 50 | Good: glass beads and vessels; coloured smelter glow | ○ GD |
| BLD-07 | Post-and-Lintel | M | 200 | BLD-05, STN-11 | build 100 | Structure: pillared hall | ○ ST |
| BLD-08 | Stepped Pyramid | W | 400 | BLD-07, BEL-06 | build 50 · ritual 50 | Structure: ziggurat site, staged by build progress | ● RS, ST |
| MIN-04 | Shaft Mining | M | 200 | MIN-03, MET-03, SOC-07 | mine 100 | ToolOrMethod: mine head and spoil heap; miners with picks | ● MC |
| MIN-05 | Fire-Setting | M | 200 | MIN-04, FIR-08 | mine 50 · tend_fire 50 | ToolOrMethod: fire and smoke at mine faces | ◐ MC, FU |
| MIN-06 | Salt Works | D | 200 | MIN-03, FOD-02, [Seen: Salt] | mine 100 | NewAction: salt pans and salt workers | ◐ FD |
| MET-03 | Casting & Moulds | M | 200 | MET-02, CER-03 | smelt 50 · craft 50 | ToolOrMethod: **orange copper tint** on tools; forge | ● MC, TL |
| MET-04 | Arsenical Bronze | M | 200 | MET-03, [Has: Arsenic ore] | smelt 50 · mine 50 | ToolOrMethod: **pale-gold bronze tint** on tools | ● MC, TL |
| MET-05 | Tin Bronze | M | 200 | MET-03, [Has: Tin] | smelt 50 · trade 50 | ToolOrMethod: **gold bronze tint**; tin highlighted in the Trade overlay | ● MC, TL, TR |
| MET-06 | Lost-Wax Casting | M | 200 | ART-06, (MET-04 \| MET-05) | craft 50 · smelt 50 | Good: a bronze statue at the capital; fine bronze in stores | ○ GD, ST |
| MET-07 | Gold & Silver Working | D | 200 | MET-03, TRD-03, [Seen: Gold or Silver] | mine 50 · craft 50 | NewAction: gold panners; gold on the chief | ○ GD, ST |
| MET-08 | Bloomery Iron | D | 200 | FIR-09, (MET-04 \| MET-05) | smelt 100 | Overlay: iron deposits revealed; bloomery smoke | ◐ MC |
| MEC-03 | The Wheel | D | 200 | MEC-02, WOD-05 | build 50 · haul 50 | Good: wheelwright; wheels in stores | ○ GD |
| NRG-02 | Harness & Yoke | M | 200 | NRG-01, FIB-03 | work_animals 50 · farm 50 | Behavior: yoked oxen at the ard and cart | ○ FD |
| CHM-04 | Mordants & Dyes | D | 200 | CHM-01, FIB-07 | craft 100 | Good: dye vats, coloured cloth on drying lines | ○ GD |
| CHM-05 | Soap & Natron | D | 200 | CHM-03, FOD-05 | craft 50 · heal 50 | Good: washing at the river; sanitation readout | ◐ HL |
| FIB-08 | Dyed Cloth | M | 200 | FIB-07, CHM-04 | fiber 100 | Good: dyed robes on chief, king and priests | ○ GD, ST |
| FIB-09 | Silk | D | 200 | FIB-07, HUS-03, [Seen: Silk moths] | fiber 100 | NewAction: silk reeling | ○ GD, TR |

```yaml
# Craft: effects, practice, flavor and moment (status: numeric)
  - id: STN-11
    effects:
      - {kind: good, id: G-ASHLAR, note: "1 L stone blocks + 0.5 ws with a T8+ kit (1.0 ws with stone tools)"}
      - {kind: unlock_action, action: dress_stone}
      - {kind: mod, stat: stone_building_labor, op: mult, value: 0.75, note: "stone building labor −25%"}
      - {kind: unlock, text: "stone walls (DEF-05); the hall, temple, palace and ziggurat masonry"}
    practice: [{action: build, share: 50}, {action: mine, share: 50}]
    flavor: "Every face square, every joint tight."
    moment: "A mason draws a copper chisel along a scored line and the block's face comes away flat as still water"
  - id: FIR-09
    effects:
      - {kind: build, structure: smelter}
      - {kind: mod, stat: smelt_yield, op: mult, value: 1.4, note: "bellows recipes at a Smelter: copper and arsenical 0.5 → 0.7 L per 2 L ore"}
      - {kind: mod, stat: charcoal_per_smelt, op: mult, value: 0.8, note: "bellows recipes: 2 → 1.6 L charcoal"}
      - {kind: unlock, text: "bloomery iron (MET-08) and glass (CER-06) need a Smelter"}
    practice: [{action: smelt, share: 50}, {action: tend_fire, share: 50}]
    flavor: "The fire learned to breathe."
    moment: "Two boys work leather bellows in turn; the clay pipe at the furnace mouth glows white"
  - id: WOD-05
    effects:
      - {kind: mod, stat: timber_building_labor, op: mult, value: 0.7, note: "timber building labor −30%"}
      - {kind: cap, cap: housing, mult: 1.05, note: "housing is build-limited (09 §6.3): produced by the faster building"}
      - {kind: unlock, text: "plank boats (SEA-05), wheels (MEC-03)"}
    practice: [{action: build, share: 50}, {action: chop, share: 50}]
    flavor: "No cord, no peg: the wood holds itself."
    moment: "A tenon slides into its mortise and the frame stands without a single lashing"
  - id: WOD-06
    effects:
      - {kind: good, id: G-TURNED}
      - {kind: mod, stat: wheel_and_cart_labor, op: mult, value: 0.7, note: "wheel, cart and chariot making −30%"}
    practice: [{action: craft, share: 50}, {action: chop, share: 50}]
    flavor: "The wood spins, and the shape is already inside it."
    moment: "A bow saws back and forth and a spinning chair leg curls shavings onto the turner's feet"
  - id: CER-04
    effects:
      - {kind: method, action: pottery, rung: 1, name: "Potter's wheel", mult: 3, note: "by a Potter at a Potter's workshop"}
      - {kind: role, id: potter}
      - {kind: build, structure: potters_workshop}
    practice: [{action: clay, share: 100}]
    flavor: "Clay rises between turning hands."
    moment: "A lump of clay climbs into a tall jar on a wheel that a child keeps turning"
  - id: CER-05
    effects:
      - {kind: good, id: G-FAIENCE, note: "luxury, barter 8; recipe faience at a Kiln"}
    practice: [{action: clay, share: 50}, {action: tend_fire, share: 50}]
    flavor: "Blue as the river at noon, and it never fades."
    moment: "A tray of beads comes out of the kiln glazed a bright turquoise"
  - id: CER-06
    effects:
      - {kind: good, id: G-GLASS, note: "luxury, barter 12; needs a Smelter"}
    practice: [{action: tend_fire, share: 50}, {action: craft, share: 50}]
    flavor: "Sand and fire made a stone you can see through."
    moment: "A glassworker winds a thread of hot blue glass around a clay core"
  - id: BLD-07
    effects:
      - {kind: build, structure: hall, note: "houses 30; the post-and-lintel frame temples and palaces use"}
      - {kind: cap, cap: housing, mult: 1.2}
      - {kind: unlock, text: "Temple (BEL-06) and Palace (GOV-05) frames"}
    practice: [{action: build, share: 100}]
    flavor: "Two posts and a beam: a door for giants."
    moment: "A great beam is lowered onto two stone pillars and the crowd walks under it"
  - id: BLD-08
    effects:
      - {kind: wonder, structure: ziggurat, note: "at a City: 200 ws, cut stone 80 L, mudbrick 400"}
      - {kind: cohesion, value: 15, scope: radius, radius: 8, note: "the Wonder term of 22 §8.3 (×1.25 with ART-06)"}
      - {kind: cohesion, value: 5, scope: all}
      - {kind: claim, text: "+1.5, radius 8"}
      - {kind: legacy, text: "Wonder: First 48, built 100 (22 §3.3)"}
    practice: [{action: build, share: 50}, {action: ritual, share: 50}]
    flavor: "A mountain made by hands, so the gods could come down."
    moment: "Lines of carriers climb the terraces at dawn and a fire is lit in the shrine on the summit"
  - id: MIN-04
    effects:
      - {kind: method, action: mine, rung: 1, name: Shaft, mult: 1.5, note: "opens depth-2 deposits; needs a Mine head"}
      - {kind: role, id: miner}
      - {kind: build, structure: mine_head}
      - {kind: mod, stat: mining_camp_crew, op: set, value: 6, note: "Open a mine sends 6 miners"}
    practice: [{action: mine, share: 100}]
    flavor: "We followed the green vein into the dark."
    moment: "A miner is lowered on a rope into a timbered shaft, a lamp glowing below him"
  - id: MIN-05
    effects:
      - {kind: method, action: mine, rung: 2, name: Fire-setting, mult: 2.25, note: "×1.5 on Shaft; 0.5 L wood per L ore"}
    practice: [{action: mine, share: 50}, {action: tend_fire, share: 50}]
    flavor: "Fire cracks what picks cannot."
    moment: "A fire roars against a rock face; water is thrown on it and the stone splits with a crack"
  - id: MIN-06
    effects:
      - {kind: unlock_action, action: salt, note: "1 L/ws at salt deposits; coastal pans in Summer"}
      - {kind: good, id: G-SALT}
      - {kind: unlock, text: "salting (FOD-07)", project: FOD-07}
      - {kind: gate, text: "[Seen: Salt]"}
    practice: [{action: mine, share: 100}]
    flavor: "White gold from the earth and the sea."
    moment: "Women rake white crystals from a shallow pan as the last of the water dries"
  - id: MET-03
    effects:
      - {kind: good, id: G-TOOL8, note: "copper kit: 0.12 L, 12 ws"}
      - {kind: method, action: chop, rung: 5, name: Copper axe, mult: 4.5}
      - {kind: good, id: G-ARMS1, note: "copper spear and dagger, weapon 1.8"}
      - {kind: build, structure: forge}
      - {kind: rule, text: "tool tier t8 (tools registry): quarry and mine ×1.1, timber build labor ×0.95"}
    practice: [{action: smelt, share: 50}, {action: craft, share: 50}]
    flavor: "Pour the metal, and the mould remembers."
    moment: "Molten copper runs into a stone mould and an axe-head cools from red to orange"
  - id: MET-04
    effects:
      - {kind: good, id: G-ASORE}
      - {kind: good, id: G-ABRONZE, note: "recipe cosmelt_arsenical"}
      - {kind: good, id: G-TOOL9, note: "0.15 L, 16 ws"}
      - {kind: method, action: chop, rung: 6, name: Bronze axe, mult: 6.0, note: "alternate route: MET-05 defines the same rung"}
      - {kind: rule, text: "tool tier t9: harvest labor ×0.8, timber build labor ×0.85, mine and quarry ×1.3, clearing ×1.25"}
      - {kind: risk, text: "smiths' injury hazard ×1.3 per figure (arsenic fumes)"}
      - {kind: gate, text: "[Has: Arsenic ore]"}
    practice: [{action: smelt, share: 50}, {action: mine, share: 50}]
    flavor: "The garlic smoke made the hardest metal yet."
    moment: "A smith coughs in grey smoke as a pale golden blade cools on the stone"
  - id: MET-05
    effects:
      - {kind: good, id: G-TINORE}
      - {kind: good, id: G-TIN, note: "barter 40"}
      - {kind: good, id: G-BRONZE, note: "recipe alloy_bronze: 9 copper to 1 tin"}
      - {kind: good, id: G-TOOL10, note: "0.15 L, 24 ws"}
      - {kind: method, action: chop, rung: 6, name: Bronze axe, mult: 6.0, note: "alternate route: MET-04 defines the same rung"}
      - {kind: rule, text: "mine works tin deposits (factor 0.5); recipes smelt_tin and alloy_bronze"}
      - {kind: rule, text: "tool tier t10: as t9, service life 24 ws (a third less metal, ore and charcoal per year)"}
      - {kind: gate, text: "[Has: Tin]"}
    practice: [{action: smelt, share: 50}, {action: trade, share: 50}]
    flavor: "One part tin to nine of copper, and the metal turns to gold."
    moment: "A smith drops a pinch of grey tin into the crucible and the melt flashes gold"
  - id: MET-06
    effects:
      - {kind: good, id: G-FINEBRONZE, note: "luxury, barter 30"}
      - {kind: mod, stat: wonder_build_labor, op: mult, value: 0.9}
      - {kind: rule, text: "a bronze statue commemorates a second Legend per City, who keeps the bonus after death (ART-06's relief keeps one per settlement)"}
    practice: [{action: craft, share: 50}, {action: smelt, share: 50}]
    flavor: "The wax melts away; the bronze remembers every line."
    moment: "A clay mould is broken open and a small bronze dancer stands inside it"
  - id: MET-07
    effects:
      - {kind: unlock_action, action: pan_gold}
      - {kind: good, id: G-GOLD, note: "luxury, barter 40"}
      - {kind: mod, stat: gold_luxury_weight, op: set, value: 2, note: "gold and silver count double in the luxury Cohesion term"}
      - {kind: gate, text: "[Seen: Gold or Silver]"}
    practice: [{action: mine, share: 50}, {action: craft, share: 50}]
    flavor: "The metal that never tarnishes, for those who must not be forgotten."
    moment: "Gold dust settles in a fleece laid in a cold stream; a circlet is hammered from it"
  - id: MET-08
    effects:
      - {kind: good, id: G-IRONORE}
      - {kind: good, id: G-IRON, note: "iron bloom, barter 15; recipe bloomery at a Smelter"}
      - {kind: reveal, text: "iron deposits within 20 tiles of every own settlement"}
      - {kind: rule, text: "mine works iron deposits; no iron tools before E4 (MET-09)"}
    practice: [{action: smelt, share: 100}]
    flavor: "A black, spitting lump that would one day outlast bronze."
    moment: "A smith breaks open a clay furnace and drags out a glowing, sputtering bloom"
  - id: MEC-03
    effects:
      - {kind: good, id: G-WHEEL}
      - {kind: unlock, text: "carts (TRN-05), potter's wheel (CER-04), lathe (WOD-06), chariots (WAR-06)"}
    practice: [{action: build, share: 50}, {action: haul, share: 50}]
    flavor: "Roll it; do not drag it."
    moment: "A solid wooden disc on an axle rolls downhill while children chase it"
  - id: NRG-02
    effects:
      - {kind: mod, stat: draft_animal_work, op: mult, value: 1.5, note: "ard: one ox pair per 6 fields instead of 4; cart load 6 → 9 L"}
    practice: [{action: work_animals, share: 50}, {action: farm, share: 50}]
    flavor: "The yoke shares the load across two necks."
    moment: "A wooden yoke is fitted across two oxen and the ard bites deeper"
  - id: CHM-04
    effects:
      - {kind: good, id: G-DYE}
      - {kind: unlock, text: "dyed cloth (FIB-08)", project: FIB-08}
    practice: [{action: craft, share: 100}]
    flavor: "The colour holds, wash after wash."
    moment: "Cloth is lifted dripping from a vat, deep red, and the colour does not run"
  - id: CHM-05
    effects:
      - {kind: good, id: G-SOAP}
      - {kind: sanitation, value: 0.10}
      - {kind: hazard, cause: endemic, mult: 0.96}
      - {kind: hazard, cause: injury, mult: 0.97, note: "wound infection"}
    practice: [{action: craft, share: 50}, {action: heal, share: 50}]
    flavor: "Clean hands, fewer fevers."
    moment: "A woman stirs ash and fat in a pot until foam spills over the rim"
  - id: FIB-08
    effects:
      - {kind: good, id: G-DYEDCLOTH, note: "luxury, barter 8"}
      - {kind: cohesion, value: 2, scope: capital}
    practice: [{action: fiber, share: 100}]
    flavor: "Red for the chief, blue for the gods."
    moment: "A chief walks through the market in a striped red and blue robe"
  - id: FIB-09
    effects:
      - {kind: unlock_action, action: reel_silk}
      - {kind: good, id: G-SILK, note: "luxury, barter 25"}
      - {kind: rule, text: "importers value silk ×1.5 in compacts"}
      - {kind: gate, text: "[Seen: Silk moths]"}
    practice: [{action: fiber, share: 100}]
    flavor: "Cloth spun by worms, lighter than air."
    moment: "A girl unwinds a single shining thread from a cocoon in a bowl of hot water"
```

---

## 16. E3 Kin (21)

| ID | Project | Type | Cost | Requires | Practice | Visible change | Sim |
|---|---|---|---:|---|---|---|---|
| AGR-05 | Ard Plough | M | 200 | AGR-02, NRG-01 | farm 50 · herd 50 | ToolOrMethod: ox teams ploughing; straighter field strips | ◐ FD |
| AGR-06 | Irrigation Canals | S | 240 | AGR-05, NUM-04, [Near: River] | farm 50 · build 50 | Structure: blue canal lines on field tiles | ● FD |
| AGR-07 | Fallow & Manuring | M | 200 | AGR-05, HUS-04 | farm 50 · herd 50 | Behavior: herds grazing resting fields | ◐ FD |
| HNT-10 | Seal & Whale Hunting | D | 200 | HNT-09, SEA-04, [Near: Coast] | hunt 50 · paddle 50 | NewAction: marine hunters at sea; oil lamps | ◐ FD, FU |
| HUS-05 | Dairying | M | 200 | HUS-04, CER-03 | herd 50 · preserve 50 | Good: milking; cheeses in stores | ◐ FD |
| HUS-06 | Wool Breeds | M | 200 | HUS-03, FIB-06 | herd 50 · fiber 50 | Good: shearing; woolly sheep; wool garments | ○ FD |
| HUS-07 | Horse Taming | D | 200 | HUS-04, [Seen: Wild horses] | herd 100 | NewAction: horse taming; horses in pastures | ◐ FD |
| HUS-08 | Pastoral Nomadism | I | 300 | HUS-07, TER-05 | herd 50 · mark 50 | Overlay: wider claims on grassland; camps move with herds | ◐ ST |
| FOD-07 | Salting & Pickling | M | 200 | FOD-06, MIN-06 | preserve 100 | Good: salt jars; parties' supply readout | ◐ FD, CB |
| FOD-08 | Ovens & Bakeries | S | 240 | FOD-05, FIR-07 | cook 50 · build 50 | Structure: domed bakery with smoke | ○ FD, ST |
| MED-04 | Surgery & Trepanation | M | 200 | MED-03, MET-03 | heal 100 | Behavior: healer with a copper knife after battles | ○ HL, CB |
| SOC-08 | Craft Specialization | I | 300 | SOC-07, TRD-04 | craft 34 · trade 33 · socialize 33 | Role: specialist outfits and workshops in Towns | ◐ ST |
| SOC-09 | Urbanism | I | 300 | SOC-08, AGR-06 | socialize 50 · build 50 | Tier: **City** banner and city sprite set | ● ST |
| GOV-04 | Tribute & Redistribution | I | 300 | GOV-02, FOD-06 | council 50 · haul 50 | Overlay: tribute flows between own settlements | ◐ ST |
| GOV-05 | Kingship & Palace | I | 300 | GOV-04, SOC-09 | council 50 · build 50 | Structure: Palace at the capital; the King | ● ST, MD |
| GOV-06 | Code of Law | I | 300 | GOV-05, COM-05 | council 50 · scribe 50 | Behavior: law stele at the capital; Cohesion readout | ◐ ST, MD |
| BEL-06 | Temples & Priesthood | S | 240 | BEL-05, BLD-07 | ritual 50 · build 50 | Structure: temple; Priest in a tall hat | ◐ ST |
| BEL-07 | State Cult | I | 300 | BEL-06, GOV-05 | ritual 50 · council 50 | Behavior: New Year procession, palace to temple | ○ ST, MD |
| ART-06 | Monumental Reliefs | M | 200 | ART-03, STN-11 | craft 50 · ritual 50 | Behavior: relief panels on halls, temples and the palace | ○ ST |
| ART-07 | Epic Poetry | I | 300 | ART-04, COM-05 | teach 100 | Overlay: +1 Beacon; a bard at feasts | ○ ST |
| LRN-04 | Scribal Schools | S | 240 | LRN-03, COM-05 | teach 50 · scribe 50 | Structure: school; scribes with tablets | ◐ ST |

```yaml
# Kin: effects, practice, flavor and moment (status: numeric)
  - id: AGR-05
    effects:
      - {kind: method, action: farm, rung: 3, name: Ard plough, rate: 8, unit: R/tile/yr, note: "field labor ×0.5 with one ox pair per 4 fields; yield unchanged"}
      - {kind: mod, stat: field_limit, op: set, value: 0.35, note: "field limit 25% → 35% of range tiles"}
      - {kind: cap, cap: food, mult: 1.17}
    practice: [{action: farm, share: 50}, {action: herd, share: 50}]
    flavor: "The ox breaks the ground the hoe could not."
    moment: "A pair of oxen drag a crooked wooden ard and a straight furrow opens behind them"
  - id: AGR-06
    effects:
      - {kind: build, structure: canal}
      - {kind: mod, stat: irrigated_yield, op: mult, value: 1.5, note: "×2 on Arid and Desert tiles, where soil counts 100"}
      - {kind: mod, stat: flood_loss, op: mult, value: 0.5}
      - {kind: cap, cap: food, mult: 1.17}
      - {kind: gate, text: "[Near: River]"}
    practice: [{action: farm, share: 50}, {action: build, share: 50}]
    flavor: "We led the river to the fields."
    moment: "A reed dam is cut and water runs down a new ditch into dry furrows"
  - id: AGR-07
    effects:
      - {kind: mod, stat: soil_fertility_loss, op: mult, value: 0.4, note: "per harvest 6 → 2.4 on fields with 2 cattle or 6 sheep per 4 fields (with AGR-03: 3 → 1.2)"}
      - {kind: cap, cap: food, mult: 1.10, take: 0.6}
    practice: [{action: farm, share: 50}, {action: herd, share: 50}]
    flavor: "The herds feed the fields that feed the herds."
    moment: "Cattle graze a resting field while a boy spreads dung along last year's furrows"
  - id: HNT-10
    effects:
      - {kind: unlock_action, action: marine_hunt, note: "4 R/ws within 2 tiles of shore; injury 1.5%/ws"}
      - {kind: good, id: G-OIL, note: "1 L per 10 R caught; 1 L = 3 L of hearth wood"}
      - {kind: gate, text: "[Near: Coast]"}
    practice: [{action: hunt, share: 50}, {action: paddle, share: 50}]
    flavor: "The sea's great beasts kept our lamps burning."
    moment: "Paddlers close on a basking seal and the harpoon line snaps taut"
  - id: HUS-05
    effects:
      - {kind: mod, stat: herd_food_without_slaughter, op: mult, value: 1.5, note: "half the herd's food comes as milk"}
      - {kind: good, id: G-MILK}
      - {kind: good, id: G-CHEESE, note: "5% spoilage"}
      - {kind: cap, cap: food, mult: 1.08, take: 0.5}
      - {kind: cap, cap: lean, mult: 1.05}
    practice: [{action: herd, share: 50}, {action: preserve, share: 50}]
    flavor: "Food from the living herd, every day."
    moment: "A woman milks a goat into a clay jar while cheeses hang drying in cloth"
  - id: HUS-06
    effects:
      - {kind: good, id: G-WOOL, note: "0.5 L per sheep a year by shearing"}
      - {kind: good, id: G-WOOLGARMENT}
      - {kind: mod, stat: yarn_output, op: mult, value: 1.5}
      - {kind: rule, text: "a wool garment (warmth +1) stacks with a hide wrap or tailored clothing in Cold climates (clothing at most +3)"}
    practice: [{action: herd, share: 50}, {action: fiber, share: 50}]
    flavor: "The sheep grew coats for us."
    moment: "Shears flash and a whole fleece falls away in one piece"
  - id: HUS-07
    effects:
      - {kind: unlock_action, action: tame_horses, note: "1 ws an animal, at most 15% of a wild herd a year; a pasture carries 2"}
      - {kind: good, id: G-HORSE}
      - {kind: unlock, text: "riding (TRN-06), chariots (WAR-06)"}
      - {kind: gate, text: "[Seen: Wild horses]"}
    practice: [{action: herd, share: 100}]
    flavor: "The fastest animal on the plain came when we called."
    moment: "A youth clings to the mane of a half-wild horse circling a corral"
  - id: HUS-08
    effects:
      - {kind: claim, text: "+1 radius on grassland, steppe and savanna tiles"}
      - {kind: rule, text: "seasonal camps follow the herds each season (best pasture within r+3) and claim at 75%"}
      - {kind: rule, text: "settlements whose range is at least half grassland, steppe or savanna pay half Overreach"}
    practice: [{action: herd, share: 50}, {action: mark, share: 50}]
    flavor: "Where the grass is, there is our home."
    moment: "A whole camp moves across the steppe behind its herds, tents lashed to horses"
  - id: FOD-07
    effects:
      - {kind: good, id: G-SALTED, note: "3% spoilage; recipe salt_food"}
      - {kind: mod, stat: party_supply_seasons, op: set, value: 2, note: "raids, levies, expeditions and investments carry 2 seasons of food"}
      - {kind: cap, cap: lean, mult: 1.05}
    practice: [{action: preserve, share: 100}]
    flavor: "Salt keeps what the season gives."
    moment: "Fish are packed in layers of salt in a tall jar and the lid is sealed with clay"
  - id: FOD-08
    effects:
      - {kind: build, structure: bakery, note: "Towns and Cities: grain food value +20% for 40 people"}
      - {kind: role, id: baker}
    practice: [{action: cook, share: 50}, {action: build, share: 50}]
    flavor: "Bread for the whole street from one oven."
    moment: "A baker slides flat loaves out of a domed oven on a long paddle"
  - id: MED-04
    effects:
      - {kind: hazard, cause: injury, mult: 0.92}
      - {kind: rule, text: "combat wounded die 32% instead of 40%"}
    practice: [{action: heal, share: 100}]
    flavor: "A copper blade, a steady hand, and the wound closes."
    moment: "A healer scrapes a neat circle in a skull; days later the patient sits up"
  - id: SOC-08
    effects:
      - {kind: rule, text: "full-time Smith, Potter, Miner and Trader in each Town and City: their work rates +30%"}
      - {kind: mod, stat: role_insight, op: pct, value: 0.1}
      - {kind: cohesion, value: -5, scope: all, note: inequality}
    practice: [{action: craft, share: 34}, {action: trade, share: 33}, {action: socialize, share: 33}]
    flavor: "Each to their craft, and all are fed."
    moment: "A street of workshops: a smith, a potter and a weaver, each at a separate door"
  - id: SOC-09
    effects:
      - {kind: tier, tier: city, min_pop: 120, needs: [temple, palace], radius: 6}
      - {kind: cap, cap: group, value: 10000}
      - {kind: cap, cap: housing, mult: 1.6, note: "dense dwellings: mudbrick houses and halls in Towns and Cities house ×1.6"}
      - {kind: rule, text: "+8 Insight a year per City (OutCity × RoleScale, then InsightCalibration)"}
    practice: [{action: socialize, share: 50}, {action: build, share: 50}]
    flavor: "Strangers became neighbours behind one wall."
    moment: "Streets cross in a grid and a crowd fills a square before the temple steps"
  - id: GOV-04
    effects:
      - {kind: rule, text: "stores rebalance between own settlements within 20 tiles each season (SOC-07: 15); local famine deaths −50%"}
      - {kind: admin, span: 2, overreach: 0.10}
      - {kind: cap, cap: food, mult: 1.04}
    practice: [{action: council, share: 50}, {action: haul, share: 50}]
    flavor: "The granary of one is the granary of all."
    moment: "Carriers bring grain sacks to the chief's storehouse and a tally is cut for each"
  - id: GOV-05
    effects:
      - {kind: build, structure: palace, note: "the capital: 30 ws, mudbrick 200, stone blocks or cut stone 20 L"}
      - {kind: role, id: king}
      - {kind: tier, tier: capital, needs: [palace], claim_mult: 1.25, note: "claim +25% within the capital's radius"}
      - {kind: mod, stat: wonder_build_rate, op: pct, value: 0.2}
      - {kind: admin, span: 3}
      - {kind: mandate, cap: 1}
    practice: [{action: council, share: 50}, {action: build, share: 50}]
    flavor: "One house above all houses."
    moment: "A king is seated on a raised throne in a pillared hall while envoys bow"
  - id: GOV-06
    effects:
      - {kind: cohesion, value: 5, scope: all}
      - {kind: hazard, cause: violence, mult: 0.85}
      - {kind: admin, span: 2, overreach: 0.15}
      - {kind: mandate, cap: 1}
    practice: [{action: council, share: 50}, {action: scribe, share: 50}]
    flavor: "The law is written, so the judge cannot forget."
    moment: "A stone stele is raised in the square, the laws carved beneath the king's image"
  - id: BEL-06
    effects:
      - {kind: build, structure: temple, note: "Towns and Cities: 20 ws, mudbrick 120, cut stone 10 L"}
      - {kind: role, id: priest, insight: 16, note: "one per Temple"}
      - {kind: cohesion, value: 6, scope: here}
      - {kind: cohesion, value: 2, scope: radius, radius: 10}
    practice: [{action: ritual, share: 50}, {action: build, share: 50}]
    flavor: "A house for the god at the heart of the town."
    moment: "Priests in tall hats carry offerings up the steps of a new temple"
  - id: BEL-07
    effects:
      - {kind: cohesion, value: 4, scope: all}
      - {kind: admin, overreach: 0.10}
      - {kind: mandate, recharge: -1}
    practice: [{action: ritual, share: 50}, {action: council, share: 50}]
    flavor: "The king speaks for the gods, and the gods for the king."
    moment: "The king leads the New Year procession from the palace to the temple"
  - id: ART-06
    effects:
      - {kind: mod, stat: wonder_cohesion, op: mult, value: 1.25}
      - {kind: mod, stat: wonder_radius, op: mult, value: 1.25, note: "Wonder Cohesion radius 8 → 10"}
      - {kind: rule, text: "one Legend per settlement keeps its bonus after death (a carved relief)"}
    practice: [{action: craft, share: 50}, {action: ritual, share: 50}]
    flavor: "Carved in stone, the deed outlives the doer."
    moment: "A sculptor chisels a lion hunt along a palace wall; the hunter has the king's face"
  - id: ART-07
    effects:
      - {kind: cohesion, value: 5, scope: all}
      - {kind: lever, text: "Beacon +1"}
      - {kind: know_how, text: "storytelling Know-how +10%"}
    practice: [{action: teach, share: 100}]
    flavor: "The hero's name is sung in every village."
    moment: "A bard recites the founder's deeds to a silent hall, the fire burning low"
  - id: LRN-04
    effects:
      - {kind: build, structure: school, note: "two Scribes"}
      - {kind: adopt, mult: 1.25}
    practice: [{action: teach, share: 50}, {action: scribe, share: 50}]
    flavor: "The tablet house makes scribes of farmers' sons."
    moment: "Rows of boys copy signs onto clay tablets while a master corrects them"
```

---

## 17. E3 Reach (20) and KEY-4

| ID | Project | Type | Cost | Requires | Practice | Visible change | Sim |
|---|---|---|---:|---|---|---|---|
| WAY-05 | Celestial Navigation | M | 200 | WAY-04, NUM-06 | sail 50 · scout 50 | Overlay: open-sea reach ring doubles | ○ MV |
| TRN-05 | Wheeled Carts | M | 200 | TRN-04, MEC-03, NRG-01 | haul 100 | ToolOrMethod: ox carts on routes | ● TL, MV, TR |
| TRN-06 | Horseback Riding | M | 200 | TRN-04, HUS-07 | ride 50 · herd 50 | Behavior: riders (scouts, messengers, herders) | ◐ MV |
| SEA-05 | Plank Boats | M | 200 | SEA-04, WOD-05 | paddle 50 · build 50 | ToolOrMethod: plank boats on rivers and coasts | ◐ MV, TR |
| SEA-06 | Sail | M | 200 | SEA-05, FIB-07 | sail 100 | ToolOrMethod: square sails at sea | ◐ MV |
| TRD-04 | Marketplaces | S | 240 | TRD-03, SOC-07 | trade 100 | Structure and Tier: market; **Town** banner | ● ST, TR |
| TRD-05 | Market Towns | I | 300 | TRD-04, TRN-05 | trade 50 · haul 50 | Structure: caravanserai; routes thicken | ◐ TR, FD |
| COM-05 | Writing | D | 200 | COM-03, NUM-03 | scribe 50 · count_stores 50 | Role: the capital's Scribe with tablet and stylus | ● ST, MD, TR |
| COM-06 | Papyrus & Parchment | M | 200 | COM-05, FIB-03 | scribe 100 | Behavior: messengers with scrolls between settlements | ○ ST, MD |
| NUM-04 | Standard Weights & Measures | M | 200 | NUM-03, TRD-04 | trade 50 · count_stores 50 | Behavior: scales at markets; trade-threshold readout | ○ TR |
| NUM-05 | Place-Value Arithmetic | M | 200 | NUM-04, COM-05 | count_stores 50 · scribe 50 | Overlay: Admin span +2 in the Cohesion overlay | ○ ST |
| NUM-06 | Solar Calendar & Astronomy | M | 200 | NUM-05, BEL-06 | count_stores 50 · ritual 50 | Behavior: priests watching sunrise on temple roofs; yield badge | ○ FD, ST |
| TER-06 | Boundary Stones & Land Rights | I | 300 | TER-05, GOV-03 | mark 100 | Overlay: boundary stones on border tiles | ◐ ST |
| TER-07 | City-States & Hinterlands | I | 300 | TER-06, SOC-09 | mark 100 | Overlay: hinterland ring around Cities | ◐ ST |
| DEF-04 | Earthworks & Hillforts | S | 240 | DEF-03, MIN-03 | build 50 · guard 50 | Structure: hillfort rings on a hill | ● CB |
| DEF-05 | Stone Walls | S | 240 | DEF-04, STN-11 | build 50 · guard 50 | Structure: stone wall rings | ● CB |
| DIP-04 | Envoys & Oaths | I | 300 | DIP-03, GOV-04 | meet_strangers 100 | Role: the Envoy; pacts in the Diplomacy drawer | ◐ DP, MD |
| DIP-05 | Written Treaties | I | 300 | DIP-04, COM-05 | meet_strangers 50 · scribe 50 | Overlay: treaty lines (when signed); drawer entry | ◐ DP |
| WAR-05 | Bronze Arms & Shields | M | 200 | WAR-04, (MET-04 \| MET-05) | fight 50 · smelt 50 | Good: bronze soldier kit (spear, shield, helmet) | ● CB, MC |
| WAR-06 | Chariots | M | 200 | WAR-05, TRN-05, HUS-07 | fight 50 · ride 50 | Good: chariots with two horses | ● CB |
| **KEY-4** | **Iron & Law** | K | **500** | MET-08, COM-05, GOV-06, TRD-05; breadth **21** of E3 | — | EraCelebration: "The Iron Age dawns"; chapter end or countdown | ● RS |

```yaml
# Reach and KEY-4: effects, practice, flavor and moment (status: numeric)
  - id: WAY-05
    effects:
      - {kind: mod, stat: open_sea_reach, op: mult, value: 2, note: "11 → 22 tiles from land; with Sail 15 → 30"}
      - {kind: mod, stat: shipwreck_risk, op: mult, value: 0.7}
    practice: [{action: sail, share: 50}, {action: scout, share: 50}]
    flavor: "The stars do not wander; steer by them."
    moment: "A navigator lies on the deck at night and measures a star against her outstretched hand"
  - id: TRN-05
    effects:
      - {kind: method, action: haul, rung: 1, name: Ox cart, mult: 5, note: "6 L a cart; on flat and trail tiles; needs a cart and an ox pair"}
      - {kind: good, id: G-CART, note: "recipe build_cart"}
      - {kind: mod, stat: mining_reach, op: set, value: 12, note: "mining and charcoal reach 8 → 12"}
      - {kind: mod, stat: settle_reach, op: flat, value: 6, note: "Found a village: 14 → 20 on land"}
      - {kind: admin, overreach: 0.10}
      - {kind: cap, cap: food, mult: 1.06}
    practice: [{action: haul, share: 100}]
    flavor: "The road remembers the wheel."
    moment: "An ox cart piled with grain creaks into the village, children riding on top"
  - id: TRN-06
    effects:
      - {kind: reach, text: "scouts and expeditions: speed ×2, range ×1.5; expedition sight +1"}
      - {kind: admin, overreach: 0.10}
      - {kind: mod, stat: herders_handle, op: mult, value: 1.5}
    practice: [{action: ride, share: 50}, {action: herd, share: 50}]
    flavor: "The horizon came closer."
    moment: "A rider gallops over a rise and back before the dust has settled"
  - id: SEA-05
    effects:
      - {kind: method, action: paddle, rung: 4, name: Plank boat, mult: 2.5, note: "cargo 12 L"}
      - {kind: mod, stat: water_route_capacity, op: mult, value: 3}
      - {kind: mod, stat: mining_reach_water, op: set, value: 16, note: "mining reach along rivers and coasts"}
      - {kind: mod, stat: fishing, op: pct, value: 0.25}
    practice: [{action: paddle, share: 50}, {action: build, share: 50}]
    flavor: "A hull of fitted planks, and cargo for a whole village."
    moment: "A sewn-plank boat slides down log rollers into the river"
  - id: SEA-06
    effects:
      - {kind: method, action: paddle, rung: 5, name: Square sail, mult: 5, note: "sea speed ×2 (SeaCostTenths 4 → 2)"}
      - {kind: mod, stat: open_sea_reach, op: flat, value: 4, note: "11 → 15 tiles from land"}
      - {kind: mod, stat: shipwreck_risk, op: mult, value: 0.7}
      - {kind: rule, text: "sea routes up to 40 tiles"}
    practice: [{action: sail, share: 100}]
    flavor: "The wind became our oarsmen."
    moment: "A square sail fills; the rowers ship their oars and laugh"
  - id: TRD-04
    effects:
      - {kind: build, structure: market}
      - {kind: role, id: trader}
      - {kind: tier, tier: town, min_pop: 60, needs: [market], radius: 5}
      - {kind: mod, stat: internal_distribution, op: pct, value: 0.3}
    practice: [{action: trade, share: 100}]
    flavor: "Bring what you have, find what you need."
    moment: "Stalls fill a square at dawn; a woman trades a jar of oil for copper beads"
  - id: TRD-05
    effects:
      - {kind: build, structure: caravanserai}
      - {kind: rule, text: "a Town on 2 or more trade routes: food cap +25%"}
      - {kind: mod, stat: route_capacity, op: mult, value: 2}
      - {kind: cap, cap: food, mult: 1.05}
    practice: [{action: trade, share: 50}, {action: haul, share: 50}]
    flavor: "Where roads meet, a town grows."
    moment: "Caravans from three directions unload in a walled yard beside the market"
  - id: COM-05
    effects:
      - {kind: role, id: scribe, insight: 24, note: "one at the capital (the Palace's scribe once it stands); two per School"}
      - {kind: admin, span: 2, overreach: 0.20}
      - {kind: mandate, recharge: -2}
      - {kind: rule, text: "written compacts and pacts: Relations decay between the partners is halved"}
      - {kind: rule, text: "a settlement with a Scribe adopts new methods from the capital without the distance lag"}
    practice: [{action: scribe, share: 50}, {action: count_stores, share: 50}]
    flavor: "Words that stay when the speaker is gone."
    moment: "A stylus presses wedges into wet clay: a count of oil jars, and a name"
  - id: COM-06
    effects:
      - {kind: mod, stat: scribe_output, op: mult, value: 1.5}
      - {kind: admin, overreach: 0.10}
      - {kind: mandate, recharge: -1}
    practice: [{action: scribe, share: 100}]
    flavor: "Writing that travels."
    moment: "A runner tucks a rolled sheet under his arm and sets off from the palace gate"
  - id: NUM-04
    effects:
      - {kind: mod, stat: trade_gain_threshold, op: set, value: 0.05, note: "barter and compacts need 5% gain instead of 10%"}
      - {kind: mod, stat: construction_labor, op: mult, value: 0.9}
    practice: [{action: trade, share: 50}, {action: count_stores, share: 50}]
    flavor: "One stone weight, the same in every market."
    moment: "A merchant's bronze weight balances the inspector's on a pair of scales"
  - id: NUM-05
    effects:
      - {kind: admin, span: 2, overreach: 0.20}
    practice: [{action: count_stores, share: 50}, {action: scribe, share: 50}]
    flavor: "Sixty, and sixty times sixty: the tablet holds them all."
    moment: "A scribe totals a whole province's barley on a single tablet"
  - id: NUM-06
    effects:
      - {kind: mod, stat: field_yield, op: pct, value: 0.1}
      - {kind: mod, stat: flood_loss, op: mult, value: 0.5}
      - {kind: mod, stat: priest_insight, op: flat, value: 4}
    practice: [{action: count_stores, share: 50}, {action: ritual, share: 50}]
    flavor: "The sun rose behind the marker peak, and we sowed."
    moment: "Priests on the temple roof watch the sun rise exactly behind a distant peak"
  - id: TER-06
    effects:
      - {kind: rule, text: "own tiles flip only after a challenger leads by 30% for 2 seasons"}
      - {kind: admin, overreach: 0.10}
      - {kind: claim, text: "mining camps radius 2"}
    practice: [{action: mark, share: 100}]
    flavor: "This stone says whose field it is, and the king agrees."
    moment: "A carved boundary stone is set at a field's corner before witnesses"
  - id: TER-07
    effects:
      - {kind: claim, text: "each City: a hinterland ring to radius 7 at 50% emission"}
    practice: [{action: mark, share: 100}]
    flavor: "The city's fields reach as far as its towers can see."
    moment: "Villages around a city bring the first harvest to its gate"
  - id: DEF-04
    effects:
      - {kind: build, structure: hillfort}
      - {kind: defense, structure: hillfort, mult: 2.5, loot: 0.25, refuge_radius: 6, site_within: 4, text: "replaces the palisade; non-combatants and stores of own settlements within 6 take refuge"}
    practice: [{action: build, share: 50}, {action: guard, share: 50}]
    flavor: "When raiders come, the whole valley climbs the hill."
    moment: "Families drive cattle up through the gate of an earth-ringed hilltop"
  - id: DEF-05
    effects:
      - {kind: build, structure: stone_walls}
      - {kind: defense, structure: stone_walls, mult: 3.0, loot: 0.25, assault: false, text: "cannot be assaulted in E3: investment only (22 §7.4); raiders loot outside the walls"}
    practice: [{action: build, share: 50}, {action: guard, share: 50}]
    flavor: "Let them come. The walls will still be here."
    moment: "The last stone is set atop a gate tower and people cheer from the ramparts"
  - id: DIP-04
    effects:
      - {kind: diplo, text: "non-aggression pact: 8 years; breaking it costs −60 Relations and reputation −30"}
      - {kind: role, id: envoy}
      - {kind: mandate, decree: compact, cost: 0}
    practice: [{action: meet_strangers, share: 100}]
    flavor: "We swore by both our gods, and the oath held."
    moment: "Two envoys clasp hands over an offering while the kings' words are spoken"
  - id: DIP-05
    effects:
      - {kind: diplo, text: "border treaty: the current line is fixed for 10 years; claims across it do not count"}
      - {kind: rule, text: "compacts survive Relations down to −30"}
    practice: [{action: meet_strangers, share: 50}, {action: scribe, share: 50}]
    flavor: "The border is written in two hands, and both say the same."
    moment: "Two scribes copy the treaty onto twin tablets, one for each king"
  - id: WAR-05
    effects:
      - {kind: good, id: G-ARMS2A, note: "0.3 L arsenical bronze, weapon 2.1, shield"}
      - {kind: good, id: G-ARMS2T, note: "0.3 L tin bronze, weapon 2.3, shield"}
      - {kind: combat, text: "shields: casualties taken ×(1 − 0.3 × share of the side's fighters with an ARMS2 kit)"}
      - {kind: rule, text: "militia carry the settlement's spare arms kits, then its best stocked spear (spares: 2, or 25% of adults under Fortify)"}
    practice: [{action: fight, share: 50}, {action: smelt, share: 50}]
    flavor: "Bronze in the hand, the shield on the arm."
    moment: "A line of warriors locks shields; spear points gleam gold above them"
  - id: WAR-06
    effects:
      - {kind: good, id: G-CHARIOT, note: "2 horses, 2 wheels, 2 L wood, 0.2 L bronze"}
      - {kind: combat, text: "chariot crew ×2 strength on open terrain, crew casualties ×0.5; no effect against walls or hillforts"}
      - {kind: rule, text: "raids with chariots: surprise +10 points, travel ×1.5"}
    practice: [{action: fight, share: 50}, {action: ride, share: 50}]
    flavor: "Thunder on the open plain."
    moment: "A chariot wheels across the plain, the archer braced as the driver lashes the horses"
# keystones.yaml
  - id: KEY-4
    status: numeric
    effects:
      - {kind: era_advance, era: 4}
      - {kind: legacy, text: "Keystone: First 48 (22 §3.3)"}
      - {kind: rule, text: "Iron threshold: E4 projects stay Hidden; E0–E3 leftovers at EraLag 0.7; Dominion and Mastery keep scoring (22 §3.1)"}
      - {kind: rule, text: "chapter end: the human's KEY-4 ends the match at the end of the season; the first rival KEY-4 starts the 12-season countdown (22 §3.1)"}
    flavor: "Iron in the furnace, law on the stone: the Bronze Age is ending."
    moment: "A smith hands a dark iron blade to a king standing before the law stele"
```

---

## 18. Headline Moments

Ten E3 Moments get the **headline** treatment: a long picture-in-picture take (**9 s** instead of 4–6, two beats), Director-mode weight **8** (the era-transition weight, 08 §11.2), an illuminated Chronicle entry, a world announcement when it is a First, and a short music sting. The camera is never hijacked outside Director mode (08 §10.1). A headline plays **once per civilization** and only for the viewer's own civilization, except a First, which every player sees as a toast.

| # | Trigger | Beat 1 | Beat 2 | Caption | Sting |
|---|---|---|---|---|---|
| H1 | MET-03 completes (first casting) | Copper pours into a stone mould at the new forge | The innovator swings the orange axe; a tree that took four strokes falls in three | "The first metal tools" | anvil chime |
| H2 | The first of MET-04 or MET-05 | The melt flashes gold in the crucible | The tint ripples across the settlement's tools as figures adopt | "Bronze" | bright horn |
| H3 | COM-05 completes | The stylus presses the first wedges | The tablet is laid in the capital's store beside the jars it counts | "Words that stay" | soft drum |
| H4 | First Town (TierReached Town) | Stalls open on the new market square | Caravans arrive and the Town banner rises | "A town at the crossroads" | market crowd swell |
| H5 | First City (TierReached City) | The crowd fills the square before the temple | Pull back over the grid of streets to the claim ring widening | "The first city" | full theme phrase |
| H6 | GOV-05's Palace completed | The king is seated in the pillared hall | Envoys bow; the capital marker appears | "A king" | brass fanfare |
| H7 | First stone wall ring closed (WallsBuilt) | The last stone on the gate tower | The ring tint closes around the settlement; a raider scout turns back | "The walls" | deep drum |
| H8 | WAR-05 completes | Shields lock in a line | Spear points rise gold; the Warriors' kit changes | "Bronze arms" | war horn |
| H9 | Ziggurat completed (WonderBuilt) | Carriers climb the terraces at dawn | The summit fire is lit; Cohesion rings pulse outward 8 tiles | "A mountain made by hands" | choir |
| H10 | KEY-4 | The iron bloom is dragged from the furnace | The law stele and the king; "The Iron Age dawns" | "Iron & Law" | the chapter theme |

Non-project event cards that share the headline style (not Moments): TinFolkAppeared, the first InvestmentCapitulated, Secession, ChapterCountdown.

---

## 19. E3 sim requirements

### 19.1 Effect kinds and registries → work package

| # | Surface | Kind / registry | Used by | WP | Contract |
|---|---|---|---|---|---|
| 1 | New and extended effect kinds | `admin`, `mandate`, `sanitation`, typed `defense`, extended `tier` and `cohesion` | §1.2 | **WP3** schema; semantics in the rows below | §1.2 |
| 2 | Alternate-route rungs | V11 rule | MET-04/MET-05 chop 6 | **WP3** | §6.2 |
| 3 | Registry schemas | goods enum (`ore`, `arms`), tools fields, roles `staffing`, `recipes.yaml`, deposits `has_gate` | §3–§9 | **WP3** (+WP4 deposits) | §3–§9, §13 |
| 4 | Chop rungs 5–6; tool tiers t8–t10 and their per-figure `mods`; kit wear, service life, dormancy tail, upgrade remelt | `method`, tools registry | MET-03/04/05 | **WP6** | §4.1, §6.2 |
| 5 | Mine family (rungs 0–2), deposit factors, depth 2, mine head, mining camps and their claims, ore haul | `method`, `unlock_action`, `build`, structures | KEY-3, MIN-04, MIN-05 | **WP6** | §6.2, §7.1–7.2 |
| 6 | Recipe execution (ordered craft step), kiln / smelter / forge, bellows flag | `recipes.yaml`, `build` | FIR-08/09, MET-02–08, crafts | **WP6** | §5 |
| 7 | Access, Dormant state, fallback with reason, upgrade push | `gate` (`[Has]`) | MET-04, MET-05 | **WP6** (+WP5 Dormant display) | §7.3 |
| 8 | Forest stock, clearing, ForestBand, clamps, fire-setting wood, oil as fuel | constants | FIR-08, MIN-05, WOD-04, HNT-10 | **WP6** | §7.4 |
| 9 | Haul family rung 1, carts, mining and charcoal reach | `method`, `mod` | TRN-05, SEA-05, NRG-02 | **WP6** | §6.2 |
| 10 | Pottery family rung 1 | `method` | CER-04 | **WP6** | §6.2 |
| 11 | Farm rung 3, field limit, canals and irrigation, flood loss, fallow, dairy and cheese, wool, horses, salt, salting, bakery, marine hunt | `method`, `mod`, `build`, `unlock_action` | AGR-05/06/07, HUS-05/06/07, MIN-06, FOD-07/08, HNT-10, NRG-02, NUM-06 | **WP7** | §5, §6, §8, §11 |
| 12 | Town, City and capital tiers; hinterland ring; roles by structure; City Insight | `tier`, `role`, `claim` | TRD-04, SOC-09, GOV-05, TER-07, BEL-06, COM-05, LRN-04 | **WP7** | §1.2, §8, §9 |
| 13 | Cohesion target with scoped terms, Admin span and Overreach, secession to Free Folk | `cohesion`, `admin`, KEY-3 `rule` | §1.2 | **WP7** (Free Folk politics in WP8) | 22 §8.2–8.3 |
| 14 | Sanitation and soap use, plague on routes, E3 hazards | `sanitation`, `hazard`, KEY-3 `rule` | CHM-05, MED-04, GOV-06 | **WP7** | §1.2 |
| 15 | Cap targets (C1-E3) | `cap` | §11.1 | **WP2** (check_e3) + **WP7** (live fixture) | §11 |
| 16 | Mandate state and recharge | `mandate` | KEY-3, GOV-05/06, COM-05/06, BEL-07 | **WP5** | §1.2 |
| 17 | Decree execution and costs (`decree` + `cost`), forecast card | `mandate`, constants | KEY-3, DIP-04, TRN-05 (`settle_reach`) | **WP8** | 22 §4 |
| 18 | Fortification multipliers, refuge, no-assault walls, investment | typed `defense` | DEF-04, DEF-05 | **WP8** | §1.2, 22 §7.4 |
| 19 | Arms kits, shields, chariots, MED-04 death share, militia stock, party supply | tools registry, `combat`, `mod` | MET-03, WAR-05/06, MED-04, FOD-07 | **WP8** | §4.2 |
| 20 | Goods barter values, Compacts, Embargo, caravans, route capacity, trade threshold, silk and gold rules, Tin Folk | goods registry, `rule`, `mod` | MET-05, TRD-04/05, NUM-04, SEA-05, FIB-09, KEY-3 `event` | **WP8** | 22 §6 |
| 21 | Pacts, border treaties, written compacts | `diplo`, `rule` | DIP-04, DIP-05, COM-05 | **WP8** | 22 §6.5 |
| 22 | Sea reach, shipwreck, paddle rungs 4–5, riding reach, expedition reach | `method`, `mod`, `reach` | WAY-05, SEA-05/06, TRN-06 | **WP8** (on 21's sea graph) | §6.2 |
| 23 | Legacy units, KEY-4, Iron threshold, chapter end, Wonder built | `legacy`, `era_advance`, `rule` | KEY-4, BLD-08 | **WP5** | 22 §3 |
| 24 | Wonder construction at a City, Wonder mods | `wonder`, `mod` | BLD-08, GOV-05, MET-06, ART-06 | **WP7** | §8 |
| 25 | New practice ids, scribe adoption rule, `adopt` | practice registry, `rule`, `adopt` | §10, COM-05, LRN-04 | **WP5** (Know-how), **WP7** (adoption) | §10 |
| 26 | VisibleChange for 66 nodes, figure kits and tints, 16 structures, ForestBand, headline Moments | events, frame | §15–§18 | **WP10** (B4 test in WP11) | §18 |
| 27 | AI use of every lever above | — | — | **WP9** | 22 §13 |

### 19.2 New `mod` stats

| Stat | Project | WP | | Stat | Project | WP |
|---|---|---|---|---|---|---|
| stone_building_labor | STN-11 | WP7 | | herd_food_without_slaughter | HUS-05 | WP7 |
| smelt_yield, charcoal_per_smelt | FIR-09 | WP6 | | yarn_output | HUS-06 | WP7 |
| timber_building_labor | WOD-05 | WP7 | | party_supply_seasons | FOD-07 | WP8 |
| wheel_and_cart_labor | WOD-06 | WP6 | | role_insight | SOC-08 | WP7 |
| mining_camp_crew | MIN-04 | WP6 | | wonder_build_rate | GOV-05 | WP7 |
| wonder_build_labor | MET-06 | WP7 | | wonder_cohesion, wonder_radius | ART-06 | WP7 |
| gold_luxury_weight | MET-07 | WP7 | | open_sea_reach, shipwreck_risk | WAY-05, SEA-06 | WP8 |
| draft_animal_work | NRG-02 | WP7 | | mining_reach, mining_reach_water | TRN-05, SEA-05 | WP6 |
| field_limit | AGR-05 | WP7 | | settle_reach | TRN-05 | WP8 |
| irrigated_yield, flood_loss | AGR-06, NUM-06 | WP7 | | herders_handle | TRN-06 | WP7 |
| soil_fertility_loss (existing) | AGR-07 | WP7 | | water_route_capacity, route_capacity | SEA-05, TRD-05 | WP8 |
| field_yield (existing) | NUM-06 | WP7 | | internal_distribution | TRD-04 | WP7 |
| fishing | SEA-05 | WP7 | | scribe_output, priest_insight | COM-06, NUM-06 | WP7 |
| construction_labor | NUM-04 | WP7 | | trade_gain_threshold | NUM-04 | WP8 |

---

## 20. Hand checks

### 20.1 Prerequisite graph (run over all 519 catalog nodes)
- **Acyclic:** a depth-first search over `requires` and `requires_any` finds no cycle.
- **Reachable:** all 66 E3 nodes are reachable from the starter projects (any-of satisfied by one member).
- **Era order (V4/V5):** every E3 prerequisite is from E0–E3; KEY-4's four named prerequisites are all E3 and span the three themes (MET-08 Craft, GOV-06 Kin, COM-05 and TRD-05 Reach).
- **Gates:** no `[Seen]` gate on KEY-4's closure except Wolves (HUS-01) and Wild goats or sheep (HUS-03), both start-guaranteed (02 §6.1). E3's own `[Seen]` gates (Salt, Gold or Silver, Silk moths, Wild horses) are on optional branches; AGR-06's `[Near: River]`, HNT-10's `[Near: Coast]` and MET-04/05's `[Has]` block use only.
- **Longest in-era chain:** 7 deep, TRD-04 → NUM-04 → AGR-06 → SOC-09 → GOV-05 → GOV-06 → KEY-4: the keystone phase is a 6-project march through three slots.
- **Leaves:** BLD-08, FIB-09, FOD-08, HUS-05, HUS-08, WAR-06 and WOD-06 are required by nothing in the catalog (fine; they are destination picks).

### 20.2 Breadth for KEY-4 (N 21)
KEY-4's closure has **18 in-era projects** either way of the bronze fork: AGR-05, AGR-06, COM-05, FIR-09, GOV-04, GOV-05, GOV-06, MEC-03, MET-03, MET-04 *or* MET-05, MET-08, NUM-04, SOC-08, SOC-09, TRD-04, TRD-05, TRN-05, WOD-05. **N 21 − 18 = 3 free picks**, exactly V9's minimum (no warning). Forced backfills beyond KEY-3's closure: **20** (TRN-01; COM-03, FIB-03, FOD-02, FOD-03, HNT-04, HUS-01, HUS-02, MEC-01, TRN-02; BLD-05, CER-03, FIR-08, FOD-06, HUS-03, HUS-04, MEC-02, NRG-01, TRN-04, WOD-03). All of this matches 22 §12.

### 20.3 Counts (03 §4.1)
**Craft 24 · Kin 21 · Reach 20 = 65**, plus KEY-4. Per lineage: AGR 3 · ART 2 · BEL 2 · BLD 2 · CER 3 · CHM 2 · COM 2 · DEF 2 · DIP 2 · FIB 2 · FIR 1 · FOD 2 · GOV 3 · HNT 1 · HUS 4 · LRN 1 · MEC 1 · MED 1 · MET 6 · MIN 3 · NRG 1 · NUM 3 · SEA 2 · SOC 2 · STN 1 · TER 2 · TRD 2 · TRN 2 · WAR 2 · WAY 1 · WOD 2 (31 lineages; FOR and INQ have no E3 project). Types: M 33, I 13, D 11, S 7, W 1.

### 20.4 Costs against 22 §15
Progress an average civilization needs from its KEY-3 to its KEY-4:
- In era: closure 4,280 + **4.7 free picks** (3 + 1.7 from the keystone phase, 07 F9) × 223 (the mean non-closure E3 cost) + KEY-4 500 ≈ **5,830**.
- Backfills: the 20 forced ones cost 768 at EraLag (E2 765 × 0.7, E1 460 × 0.49, E0 22 × 0.34); with ~35% taken before KEY-3 and a few voluntary ones, T8's 11–20 backfills ≈ **650**.
- Total ≈ **6,480** progress; Know-how supplies ~17% (T6), so Insight ≈ **5,380**.

| E3-window Insight per minute | Source | E3 duration |
|---|---|---:|
| 400 | 22 §1.4 live mean, E2 tech | 13.4 min |
| 435 | the same + E3 roles ramping to 12–20% (§20.9) | **12.4 min** |
| 528 | T5's linear ramp 330 → 725 | 10.2 min |

The 12.4-minute leader estimate and ~11.5–12-minute Council estimate are historical results at the earlier cost assumptions. The current model projects an E3 of about 19.75 minutes at EraBase 400; the new cadence needs live acceptance before replacing the provisional timing gates. A typical civilization completes 22.7 of 65 E3 projects (35%) in the historical run; recompute this at the new cadence.

### 20.5 Ladders (V11)
chop 0–6 (rung 6 shared by the alternate rule) · mine 0–2 · farm 1–3 (3 equal in rate to 2) · paddle 1–5 (4 equal to 3) · pottery 0–1 · haul 0–1. No skip, no ladder starting above 1, no duplicate outside the alternate rule.

### 20.6 FoodCap (C1-E3)
§11.1 reproduces 88 → **190.0** (×2.159) from 09 §5.2's steps; E3 projects alone ×1.74. Mechanism cross-check (22 §9): fields carry ~45% of E2-end food; the ard's field limit ×1.4 gives ≈ ×1.18 (target 1.17); after the ard, fields carry ~53% of food, so irrigation reaches AGR-06's ×1.17 only if **~64% of all fields** are irrigated (fewer where Arid and Desert tiles get ×2); a river settlement irrigating half its fields gets ×1.13 (22 §9 says ≈ ×1.2). WP2's check_e3 measures the irrigated share; if it is lower, the AGR-06 target falls short (§22 item 4).

### 20.7 Administration and Mandate sums
- **Admin span:** 4 (KEY-3) + 2 GOV-04 + 3 GOV-05 + 2 COM-05 + 2 NUM-05 + 2 GOV-06 = **15**; KEY-4's closure alone 4 + 2 + 3 + 2 + 2 = **13** ✓ 22 §8.2.
- **AdminReduction:** closure (GOV-04 0.10, COM-05 0.20, GOV-06 0.15, TRN-05 0.10) = 1 − 0.9 × 0.8 × 0.85 × 0.9 = **0.449**; a colony 24 tiles out loses (24 − 8) ÷ 2 × 0.551 = **4.4** ✓ 22 §8.3. All nine = 0.711.
- **Mandate:** cap 2 + GOV-05 + GOV-06 = **4**; recharge 8 − 2 COM-05 − 1 COM-06 − 1 BEL-07 = **4** = the floor ✓ 22 §4.2. The 52-season / 7–8 Mandate estimate was for the earlier 13-minute cadence; the current 19.75-minute projection gives about 13.2 and needs a budget review (07c §10.1).

### 20.8 Metal and fuel (T17–T19)
- **Steady state** reproduces 22 §5.6 by construction (service life §4.1): tools 92 ws/yr ÷ 24 × 0.15 = 0.58 L (tin), ÷ 16 = 0.86 L (arsenical); arms 0.15 L; ≈ 0.85 L bronze per 100 people a year.
- **The re-kit is the fuel event.** Re-kitting 35 tool users with T10 takes 5.25 L tin bronze: 4.7 L copper + 0.53 L tin + 2.6 L charcoal to alloy. Kiln: 18.9 L copper ore and 22.6 L charcoal in all (**135 L wood**); bellows: 13.5 L ore, 14.5 L charcoal (**87 L wood**). The earlier copper re-kit (4.2 L copper, kiln) is another ~100 L. Against ~390 L standing in a radius-3 range (13 forest tiles × 30 L), the two re-kits alone are **~60%** of it; the 50% upgrade remelt (§4.1) cuts the bronze re-kit's copper by 2.1 L and the pair to **~47%**. **T19's 30–50% drawdown therefore comes mostly from adoption, not from steady demand**, and lands at the top of its band unless charcoal is drawn from beyond range (the 8–12 tile clamp reach) or WOD-04 is taken. WP2's metal_sim must model the re-kit and the remelt.
- **T18 feasibility:** 70% of a settlement of 100 re-kitted in 12 seasons needs ≈ 3.7 L bronze → ≈ 40 ws of mining, charcoal, smelting and hauling, **≈ 9% of labor** for 12 seasons, under the 10% `MetalPushLaborCap`.

### 20.9 Role Insight at KEY-4 (T7)
Per civilization at KEY-4: the capital's Scribe 24 × 1.6 = 38; ~2 Temples' Priests 51; a School in ~half of civilizations (2 Scribes) ≈ 38; one City 13; ≈ **140 Insight/yr** of ~700 at KEY-4 = **20%**, the top of T7's 12–20% (era mean lower). If WP2 finds it above 20%, the first lever is School staffing (2 → 1), not role outputs.

---

## 21. Decisions beyond 22, and corrections to 22

### 21.1 Corrections (22 needs these edits)

| # | 22 says | This document | Why |
|---|---|---|---|
| C1 | Method(Mine r3), Method(Till r3), Method(Pottery r2), Method(Haul r4), Method(Sail r5) | mine r1–r2 (Fire-setting a rung, not a mod), farm r3, pottery r1, haul r1, **paddle r5** | V11: ladders start at 0/1 and never skip; one watercraft ladder |
| C2 | Chop r6 from MET-04 *and* MET-05 | kept; V11 gains an alternate-route rule | V11 forbids two projects on one rung |
| C3 | Every mining camp has a mine head; the mine head is MIN-04's | camp = surface pit from KEY-3; mine head (MIN-04) for depth 2 | otherwise no camp could open before MIN-04 |
| C4 | Besiegers carry 1 season, 2 with FOD-02 or FOD-07 | 1 season (dried food, FOD-02, is the base); **2 with FOD-07** | FOD-02 is on every civilization's closure, which would void FOD-07's effect |
| C5 | Scribe: one per Palace | one at the capital from COM-05 (the Palace's once built) | COM-05 precedes GOV-05; otherwise COM-05 has no role and no visible change (B4) |
| C6 | Kits "keep working until worn (1–3 years)" | first fallbacks within a season, a third in ~3–4 years, all in 6–9 | 22's own durabilities at ~0.66 ws of tool work a season (§4.1) |
| C7 | E3 steps give ×2.16 food | ×2.16 total, of which ×1.24 is the four forced backfills; E3 projects ×1.74 | §11.1 |
| C8 | Palace: cut stone 20 | stone blocks or cut stone 20 L | GOV-05's closure lacks STN-11 |
| C9 | Arms kit: bronze + wood + 1 leather | leather **or hide** | FIB-03 may still be a backfill when WAR-05 lands |
| C10 | MET-06 and ART-06 both "a Legend keeps its bonus" | ART-06 one per settlement; MET-06 a second per City | duplicate effect |
| C11 | HUS-08 "camps exempt from Overreach" | settlements on mostly open grassland pay half Overreach | camps never paid Overreach (22 §8.2) |
| C12 | Roles registry: Scribe unlocked by LRN-04 | COM-05 | 22 §8.4 |
| C13 | "Capital" before GOV-05 undefined | the oldest settlement until a Palace stands | Overreach is live from KEY-3 |
| C14 | 21 §1 calls Sail E4 | SEA-06 is E3 in the catalog | doc 21 note only |
| C15 | Practice: WOD-06 craft; MEC-03 craft, haul; NRG-02 herd, farm; SOC-08 craft, trade | WOD-06 craft · chop; MEC-03 build · haul; NRG-02 work_animals · farm; SOC-08 craft · trade · socialize | V14 warns (and `--strict` fails) when no practice feeds the project's own lineage |

### 21.2 Decisions beyond 22
- **Registries:** 36 goods (ores, metals, foods, luxuries, vehicles, horses), arms split by alloy (G-ARMS2A/2T), **two new structures** (Forge for casting at MET-03; Caravanserai as TRD-05's visible change), a **recipes registry** with a fixed craft order, the **staffing** field for roles, `has_gate` on deposits.
- **Effect semantics:** delta-summed `mandate`; scoped `cohesion` as target terms; barrier multipliers that do not stack; tier demotion hysteresis (0.85 × min_pop for 4 seasons); soap consumption for sanitation; mod order set → flat → pct → mult.
- **Metal rules:** service life to replacement, 50% remelt on upgrade only, dormancy tail ×1.4, upgrade push capped at 10% of labor, deposit factors (tin 0.5), FIR-09's bonus only on copper and arsenical smelts, tin dug and smelted only with MET-05, KEY-3 hosting the E3 switches (mine action, mining camp, Mandate, Admin span 4, Cohesion live, plague, Tin Folk).
- **War:** militia arms stock (2 spares; 25% of adults under Fortify), shield effect linear in the share of shielded fighters, open-terrain list for chariots.
- **Numbers not in 22:** barter values of the goods 22 did not price, structure costs, Bakery fuel, wool and oil yields, cheese and salting recipes, ForestBand thresholds, wheel/cart/chariot recipes.

---

## 22. Open issues
1. **Historical Insight ramp (T5) vs. measured mean (§1.4)** disagreed by ~25% over the earlier E3 window. Current EraBase is 400 by the cadence follow-up; rerun the live-mode fit and T5 checks against the new timing projection.
2. **The re-kit dominates fuel** (§20.8): T19 may exceed 50% drawdown without WOD-04. If metal_sim confirms, the levers are a larger remelt share (0.5 → 0.7) or slower adoption (T18's 12 seasons → 16), not durability (which T17 fixes).
3. **Floods are not simulated yet** (12 §7 lists them as a living-world event). AGR-06's and NUM-06's `flood_loss ×0.5` do nothing until WP7 adds the 5%-a-year river flood; the food-cap targets do not depend on it.
4. **AGR-06's ×1.17 target** assumes ~64% of all fields are irrigated (§20.6); if check_e3 measures fewer river settlements, C1-E3 lands near 180 and the shortfall belongs to geography (22's intended river fork), not a tuning bug. Re-state the target per river share rather than raise the number.
5. **Open-sea reach 22–30 (WAY-05, Sail) makes the 33-tile continental gap crossable in one leg** (midpoint 16.5 from land, 21 §3). Intercontinental contact, raids and compacts in late E3 follow; 21's "no carve-outs" decision accepts it, but T15/T23 and the AI's threat model should be measured with it on.
6. **Role Insight at the top of T7** (§20.9); School staffing is the first lever.
7. **Silk** needs `[Seen: Silk moths]` regions (12 §8 "4 regions per map") to exist on the live map; WP4 should confirm the placement, or FIB-09 is unreachable on most seeds (it is optional).
8. **KEY-3 gains effects** (§13). The 12- and 24-minute goldens do not reach KEY-3, but the content-pack golden changes with WP3's transcription (one deliberate refresh).
