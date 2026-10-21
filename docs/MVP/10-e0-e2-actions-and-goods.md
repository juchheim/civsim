# 10 — Actions, Method Ladders and Goods for E0–E2 (WS-C, WS-D)

> The build-ready spec for **what figures can do** in the first three eras (Dawn, Hunters, Roots), **how fast** they do it at each rung of each ladder, and **what goods** flow through the economy.
> Project-by-project effects that unlock these rungs are in [11-e0-e2-project-specs.md](11-e0-e2-project-specs.md). The world they act on is in [12-world-generation.md](12-world-generation.md).
> Targets come from the balance work: per-settlement caps from [09 §5](09-population-and-health.md#5-caps-raised-project-by-project), mortality causes from [09 §4](09-population-and-health.md#4-deaths), and pacing from [07](07-balance-findings.md). **The rates below are tuned so an average settlement reproduces 09's FoodCap trajectory (20 → 27 → 41 → 88 people across E0–E2) within ±10%.** That is the acceptance check for this document.
> Normative design specification. The E0–E2 economy implementation is complete; its acceptance results are in [07c](07c-e0-e2-checks.md) and tracked in [17](17-vertical-slice.md).

---

## 1. Units

| Unit | Symbol | Definition |
|------|--------|-----------|
| **Season** | — | 150 sim ticks. Its real-time length depends on pace (7.5 s Quick, 15 s Standard, 26 s Epic, [07](07-balance-findings.md) F7). All rates are per season, so pace never changes the economy. |
| **Worker-season** | ws | The work time one healthy adult has in a season after rest, eating and social time (≈ 70% of the season). Children 0–9: 0; ages 10–13: 0.3 ws; elders (45+): 0.5 ws. Health scales it (05 §7.3). |
| **Ration** | R | The food one adult eats in a season. Child 0.6 R, elder 0.8 R. A figure without enough warmth in Winter needs ×1.15. |
| **Load** | L | One figure's carrying capacity at rung 0. 1 L ≈ 2 R of fresh food, one bundle of firewood, one block of building stone, or 10 cord. |
| **Tile** | — | One hex. Walking across an open tile takes 3 ticks at rung 0. |

Representation scale is **1 : 1 through E3** ([07](07-balance-findings.md) §5 #11), so every number here is per real person in E0–E2.

## 2. The labor budget of a band (sanity anchor)

A typical E0 band of **20** at 09's age structure (48% children by the end of E0, [07c](07c-e0-e2-checks.md)): **8 adults, 10 children, 2 elders** (the starting band of 12 is adult-heavy: 8 / 2 / 2).
- **Labor:** 8 × 1.0 + 2 × 0.5 + ~2.5 children aged 10–13 × 0.3 = **≈ 9.8 ws** per season.
- **Food need:** 8 + 10 × 0.6 + 2 × 0.8 = **15.6 R** per season (≈ 18 R in a cold Winter).
- **Non-food work** (fire, shelter, tools, childcare, hauling, ritual) takes about 30% of labor in E0, so ~6.8 ws goes to food. The band needs **≈ 2.3 R per food-ws**. With a 70/30 forage/scavenge work split, rung-0 output is `0.8 × (0.70 × 3.2 + 0.30 × 0.9) ≈ 1.9 R/ws`; that feeds the adult-heavy starting band (≈1.7 needed) but not a grown, child-heavy one. FOR-01, WOD-01 and HNT-01 raise effective food output before the band nears 20. Labor is a viability check, not the cap: need and labor both scale with band size.
- **The cap is set by stocks.** A band forages and hunts within its **range** (§3.1, radius 3 = 37 tiles). Its sustainable plant regrowth (37 × 2.65 R/yr × 40% reachable = 39 R) plus its share of the herds' growth (37 × 0.04 herds × 46 R/yr × 40% = 27 R) feeds ≈ 18 people at the starting band's 3.6 R per person per year. That is **FoodBase ≈ 20** (09 §6.1), within 10%.
- **Winter:** plant regrowth falls to 0.15 R/tile, and fresh plants lose 40% a season in store. The band covers ≈ 76% of late-winter need (§4.6, 07c C2), which is **LeanBase 0.77** (09 §6.2). The starting season of stores covers the first winter.

## 3. How a method is chosen and how output is computed

As 05 §7.2–7.3, restated with E0–E2 terms:
```
Output per ws = BaseRate[action] × MethodMult[rung] × ToolQuality × SkillMult × HealthMult
              × BiomeMult[biome, action] × SeasonMult[season, climate, action]
              × Π research modifiers × Legend/posture modifiers
```
- **Rung** = the highest rung the civilization has unlocked, the figure *knows* (adoption, 05 §16.3), and the figure has the tool and inputs for. Otherwise it falls back one rung at a time.
- **ToolQuality** = 1.2 (new) → 0.5 (worn), falling linearly with use. A tool breaks at 0 durability. Planners replace tools at 0.7.
- **SkillMult** = 0.8 + 0.08 × skill level (0–10). Skill rises by 1 level per ~4 ws of practice, slower above 6.
- **Stocks bind.** Gathering actions draw from a tile's stock (§4). When a stock runs out, output drops to its regrowth rate, so research that raises *yield per tile* (lore, tending, fields) matters as much as research that raises *speed*.

### 3.1 Range and stock limits (what sets FoodCap)
These four constants turn the rates below into 09's FoodCap trajectory. They were added by [07c](07c-e0-e2-checks.md) C1, which derives 18 → 28 → 42 → 88 people per average settlement against the target 20 → 27 → 41 → 88.

| Constant | Value (E0–E2) | Note |
|---|---|---|
| **Range** | radius **3** around the settlement (37 tiles) for daily foraging, hunting and fields | Same as the camp claim radius (05 §9.5). TER-04's radius-4 village claims more land but doesn't lengthen the daily walk. Ranges of your own settlements don't overlap (fission sites sit ≥ 2r + 1 apart, 07b). |
| **Herd share** | a band can take **40%** of the sustainable yield (herd growth, §4.2) of herds whose path lies in its range: scavenging and HNT-01 **0.40**, HNT-02 **0.42**, HNT-03 **0.44**, HNT-04 drives **0.48**, dogs (HUS-01) ×1.1 | Predators and the herds' own range take the rest. Hunting rungs mostly raise R per ws (labor), not the stock. Taking more than the share shrinks the herd (§4.2). |
| **Field limit** | at most **25%** of range tiles under fields in E2 (≈ 9 tiles) | Good soil, not wet or steep, within reach. E3 projects (AGR-05 plough, AGR-06 irrigation) raise it. A field tile feeds ~4–5 people once cooking, boiling, grinding and fermentation stack, so this limit, not field labor, sets E2's cap. |
| **Storable share** | FOR-03: **40%** of an autumn plant take is nuts and seeds; FOD-02: **30%** of autumn meat is dried | Feeds the late-winter check (09 §6.2). |

## 4. Food actions

### 4.1 Forage (plants)
**Tile stock:** Forage regrowth per tile per season (R, Temperate forest): **Spring 0.6, Summer 1.0, Autumn 0.9, Winter 0.15**. The stock caps at 2 seasons of regrowth. Biome multipliers are in [12 §4](12-world-generation.md#4-biomes-and-their-yields).

| Rung | Method | Unlocked by | Tool / input | MethodMult | Notes |
|------|--------|-------------|--------------|-----------:|-------|
| 0 | Hand gathering | start | — | 1.0 | Base rate **3.2 R/ws** while stock lasts; 40% of the stock is reachable by hand |
| 1 | Digging stick | WOD-01 (+FOR-02 for roots) | Digging stick (durability 4 ws) | 1.5 | Roots and tubers open: reachable stock +25%; Winter stock ×2 (tubers keep) |
| 2 | Baskets | FIB-05 + FOR-03 | Basket (8 ws) | 1.8 | Carry ×1.5 for forage; nuts and seeds become **storable** (spoilage 5%/season) |
| 3 | Wild-grass sickle | FOR-04 + STN-06 | Microlith sickle (6 ws) | 2.4 | Only on grass-stand tiles (grassland, steppe, savanna, open woodland): +60% stock on those tiles in Summer–Autumn |
| 4 | Tended stands | FOR-06 | — | 2.4 | Stand tiles regrow ×2 (proto-cultivation); creates a visible "tended" tile state |

**Modifiers:** FOR-01 Plant Lore: +30% yield and reachable stock 40% → 52%; poisoning deaths −80%. FIR-05 Landscape Burning: a burned tile's forage +40% and game +30% for 3 years (12 §7).

### 4.2 Scavenge and hunt
**Herds** are group-agents ([12 §6](12-world-generation.md#6-fauna)). A kill yields meat by species: small game 1 R, deer or reindeer 4 R, horse 10 R, aurochs or bison 15 R. Plus hides (1 per kill above small game) and bone/antler (0.3 L).

| Rung | Method | Unlocked by | Party | Tool | Effective yield (R/ws) | Injury risk per ws |
|------|--------|-------------|-------|------|------------------------:|-------------------:|
| 0 | Opportunistic scavenging | start | 1–3 | — | **0.9** (finds 0.3 carcasses per ws × 3 R) | 1% |
| 0+ | Power scavenging | HNT-01 | 3+ | clubs | **1.35** (×1.5) | 1.5% |
| 1 | Wooden-spear ambush | HNT-02 (+FIR-04 hardened +10%) | 2–4 | Wooden spear (5 ws) | **1.6** | 2.0% |
| 2 | Stone-tipped spears | HNT-03 | 2–4 | Hafted spear (8 ws) | **2.2** | 1.6% |
| 3 | Cooperative drives | HNT-04 | **≥ 6** | Hafted spears | **3.2** on large herds (aurochs, bison, horse, reindeer) | 1.6% |
| 4 | Spear-thrower | HNT-05 | 2–4 | Atlatl + darts (8 ws) | **2.8** | 1.1% |
| 5 | Bow & arrow | HNT-06 | **1–2** | Bow + arrows (10 ws) | **3.0**; small and fast game added as a prey class (always present) | 0.8% |

- **Herd response:** each kill adds *fear* to the herd. Above a threshold the herd moves 3–6 tiles away. Drives (rung 3) add 3× fear and can push a herd off the home range for a season. Over-hunting (kills > herd growth of 20–30%/yr) shrinks herds, and herds can go locally extinct. That is visible, and the AI must model it ([14 §8.3](14-ai-opponents.md#83-herds-and-stocks)).
- **Dogs** (HUS-01): +20% to every hunt rung; scavenging carcass-finding ×1.3.
- **Tracking** (INQ-01): hunters see herds within 2× sight, cutting search time: +20%.

### 4.3 Fish
Fish stock per water tile per season: river 1.2 R, lake 1.0 R, coast 1.5 R. **Salmon-run rivers** give ×3 in Autumn ([12 §5](12-world-generation.md#5-water)).

| Rung | Method | Unlocked by | Tool / structure | Yield (R/ws) | Notes |
|------|--------|-------------|------------------|-------------:|-------|
| 1 | Harpoons & fish spears | HNT-07 | Harpoon (6 ws) | 1.6 | Shore tiles only |
| 2 | Hooks, lines & nets | HNT-08 | Fishing kit (8 ws), net (12 ws) | 3.2 | |
| 3 | Weir | HNT-09 | **Weir** structure on a river tile | **passive 2.0 R/season** per weir, ×3 in salmon runs | Needs 0.1 ws/season upkeep; max 1 per river tile |
| + | Boat access | SEA-02 / SEA-03 | Canoe / dugout | +50% (deeper water), lake and coast tiles 2 tiles out | |

### 4.4 Farm (E2)
| Rung | Method | Unlocked by | Yield per field tile per year (R) | Labor per field tile per year |
|------|--------|-------------|----------------------------------:|-------------------------------|
| 1 | Cultivation of wild-type crops | AGR-01 | **4.0** (harvested in Autumn) | clear (§4.4.1) + sow 0.3 + tend 0.3 + harvest 0.4 ws |
| 2 | Domesticated staples | AGR-02 | **8.0** | same |
| + | Legume pairing | AGR-03 | ×1.08; soil fertility decays 50% slower | +0.1 ws |
| + | Tended stands carry over | FOR-06 | first-year field yield +25% on former stand tiles | — |

- **Soil fertility** (0–100) starts by biome ([12 §4](12-world-generation.md#4-biomes-and-their-yields)). Each harvest costs 6 points (3 with AGR-03), and a fallow year restores 10. Yield × fertility/100 (minimum 0.3).
- **Field tiles** turn yellow-green, then gold at harvest (08 §3).
- **Staple by biome:** the AI and the planner don't choose a crop. The biome gives one: wheat or barley (temperate and arid-with-river), rice (warm wetland and river), millet (steppe and savanna), maize or tubers (tropical). Yields are equalized; only the art differs.

#### 4.4.1 Clearing land
| Cover | By hand (E1) | Polished axe (STN-08) | Slash-and-burn (AGR-04) |
|-------|------------:|---------------------:|-----------------------:|
| Grassland or stand | 0.5 ws | 0.5 ws | 0.3 ws |
| Open woodland | 2 ws | 1 ws | 0.5 ws |
| Forest | 5 ws | 2.5 ws | 1 ws (fields shift every 4 years; a burned tile regrows as woodland) |

### 4.5 Herd (E2)
| Rung | Method | Unlocked by | Effect |
|------|--------|-------------|--------|
| 0 | Herd following | HUS-02 (E1) | Wild herd stays on the home range (fear decays ×2); drives can't push it off |
| 1 | Goats & sheep | HUS-03 | **+Action: Herd.** Tame 0.5 ws per animal from a wild herd in range (max 20% of the herd per year). A pasture tile carries 6 goats or sheep. Yield per animal per year: 0.8 R meat (culling) + 0.2 hide |
| 2 | Cattle & pigs | HUS-04 | A pasture tile carries 2 cattle; per animal per year 2.5 R + 0.3 hide; pigs 1.2 R and eat 0.5 R of waste. Cattle enable draft (NRG-01) |
| + | Dogs | HUS-01 | Herders handle ×1.5 animals |

Herding labor: 0.1 ws per 10 small stock or 4 cattle per season. Domestic herds grow 25%/yr if pasture allows.

### 4.6 Cook, preserve, store
| Rung / project | Effect |
|---------------|--------|
| FOD-01 Cooking | **Food value +30%** for meat, roots and cereals; food-borne disease −40% (09 §4.2); 0.02 ws per 10 R; needs a fire |
| FOD-02 Smoking & drying | Converts fresh meat or fish into **Dried food**: spoilage 60–70% → 10% per season; 0.05 ws per 4 R |
| FOD-03 Storage pits | **Storage pit**: 15 R, spoilage ×0.5 |
| FOD-04 Boiling | Food value +20% (cumulative with cooking); infant survival (09 §4.1: age-0 hazard ×0.85); needs pots |
| FOD-05 Fermentation | Grain → beer or bread: food value +15%; **feasts** cost 30% less food for the same Cohesion |
| FOD-06 Granary | **Granary**: 80 R, spoilage ×0.25, pests −50% |
| FOR-05 Seed grinding / STN-09 Querns | Cereal food value +25% (by hand-stone), then +30% (quern structure; does not stack, quern replaces) |

**Spoilage per season (unstored):** fresh meat 60%, fish 70%, fresh plants 40%, roots 15%, nuts and seeds 5%, wild grain 8%, dried food 10%, domestic grain 5% (+10% pests outside a granary). Pots (CER-02) cut spoilage of anything stored in them by a further 25%.

## 5. Material actions

### 5.1 Chop wood (the template ladder, 02 §12)
**Base rate 2.0 L of wood per ws.** Forest tiles hold 30 L of standing wood and regrow 0.5 L/season (×2 with WOD-04 Coppicing). Deadwood: 2 L/tile/year.

| Rung | Method | Unlocked by | Tool | MethodMult | Wood / ws |
|------|--------|-------------|------|-----------:|----------:|
| 0 | Break & gather deadwood | start | — | 1.0 | 2.0 (deadwood only) |
| 1 | Pebble chopper | STN-01 | T1 | 1.5 | 3.0 (saplings) |
| 2 | Hand axe | STN-02 | T2 | 2.0 | 4.0 (small trees) |
| 3 | Hafted stone axe | STN-04 | T4 | 3.0 | 6.0 |
| 4 | Polished stone axe | STN-08 | T7 | 4.0 | 8.0 (clearing practical) |

**Wood demand (E0–E2):** hearth 1.5 L/season (3 L in Winter); kiln firing 4 L per batch; charcoal 6 L → 1 L; building (§6).

### 5.2 Knap, carve, grind: tool tiers
| Tier | Tool kit | Unlocked by | Make time (ws) | Inputs | Durability (ws of use) | Enables |
|------|----------|-------------|---------------:|--------|----------------------:|---------|
| T0 | Hands, sticks | start | — | — | — | rung 0 everywhere |
| T1 | Pebble choppers & flakes | STN-01 | 0.05 | stone 0.3 L | 1.0 | chop 1, butcher +25% |
| T2 | Hand axe kit | STN-02 | 0.08 | stone 0.4 L | 2.0 | chop 2, butcher +45% (incl. +20% carcass) |
| T3 | Prepared-core kit (flakes, points) | STN-03 | 0.08 | stone **0.28 L** | 2.0 | stone points |
| T4 | Hafted kit (axe, spear) | STN-04 | 0.12 | stone 0.3, cord 0.1, wood 0.1 | 3.0 (×1.4 with CHM-02 tar) | chop 3, hunt 2 |
| T5 | Blade kit | STN-05 (+FIR-06 heat-treated) | **0.07** (0.05) | flint 0.25 L | 4.5 | durable everyday tools |
| T6 | Composite microlith kit | STN-06 | 0.09 | flint 0.2, wood 0.1, tar or cord | 4.0 | arrows, sickles |
| T7 | Polished axe | STN-08 | **0.3** (grinding) | stone 0.4 L | **10** | chop 4, clearing |
| — | Bone & antler set (needles, awls, harpoons, flutes) | STN-07 | 0.1 | bone/antler 0.3 L | 6 | tailoring, harpoons, flutes |
| — | Copper awls & ornaments | MET-01 / MET-02 | 0.2 | copper 0.1 L | 8 (awl) | prestige, trade |

**Flint vs. stone:** T1–T4 use any stone. T5–T6 need **flint or chert** (MIN-01 reveals it; MIN-02 mines it). With no flint, they fall back to T4 quality.

**Hunting and fishing tools:** HNT-05 makes one atlatl-and-dart set for 8 ws from 0.5 wood and 0.1 cord; HNT-06 makes one bow-and-arrow set for 10 ws from 0.3 wood and 0.1 flint; HNT-08 makes one fishing kit for 8 ws from 0.1 bone and 0.1 cord. FIB-05 makes one net for 12 ws from 20 cord (as in §4.3 and the goods table).

### 5.3 Fiber, hide, clay, textile
| Good | Unlocked by | Recipe | Time (ws) |
|------|-------------|--------|----------:|
| Cord ×10 | FIB-01 | plant fiber 0.5 L | 0.1 |
| Rawhide | FIB-02 | 1 hide | 0.1 |
| Leather | FIB-03 | 1 rawhide + smoke (hearth) | 0.2 |
| Hide wrap (warmth +1) | FIB-02 | 2 rawhide | 0.1 |
| Tailored clothing (warmth +2) | FIB-04 | 3 leather + bone needle set | 0.3 |
| Sapling ×1 | FOR-07 | wood 0.1 L | 0.1 |
| Basket | FIB-05 | plant fiber 0.5 L | 0.15 |
| Net | FIB-05 | cord ×20 | 12 |
| Clay | CER-01 (or BLD-05) | gather on clay tiles: 3 L/ws | — |
| Figurine | CER-01 | clay 0.1 L, fired in a hearth | 0.05 |
| Pot ×4 | CER-02 | clay 1 L; open firing breaks 40%, kiln 20% | 0.3 |
| Storage jar ×2 | CER-03 | clay 1 L, kiln | 0.3 |
| Yarn ×1 | FIB-06 | wool or flax 0.5 L | 0.3 |
| Cloth ×1 (warmth +1, trade) | FIB-07 | yarn ×2 | 0.5 |
| Mudbrick ×10 | BLD-05 | clay 1 L + straw 1 L, sun-dried 1 season | 0.3 |
| Lime ×1 | CHM-03 | stone 1 L + wood 2 L in a kiln | 0.3 |
| Pigment ×1 | CHM-01 | ochre 0.2 L | 0.05 |
| Birch tar ×1 | CHM-02 | bark 0.5 L, controlled heat | 0.1 |
| Charcoal ×1 L | FIR-08 | wood 6 L | 0.4 |
| Copper ×0.5 L | MET-02 | copper ore 2 L + charcoal 2 L, kiln | 0.5 |

### 5.4 Mine and quarry (E1–E2)
| Action | Unlocked by | Output per ws | Notes |
|--------|-------------|--------------:|-------|
| Collect flint and ochre (surface) | MIN-01 | flint 1 L, ochre 0.5 L | Reveal radius = sight |
| Mine flint (pits) | MIN-02 | flint 3 L | Never runs out locally |
| Quarry blocks | MIN-03 | stone blocks 2 L | Needs a stone deposit tile |
| Collect native copper | MET-01 | 0.2 L | `[Seen: Native copper]` tile |
| Dig copper ore (shallow) | MET-02 | 1 L | Deeper ore needs MIN-04 (E3) |

## 6. Build: structures available in E0–E2

| Structure | Unlocked by | Labor (ws) | Materials | Capacity / effect | Upkeep |
|-----------|-------------|-----------:|-----------|-------------------|--------|
| Hearth | FIR-03 | 0.2 | stone 0.5 L | Warmth +1 within the camp; evening gathering: +Cohesion, +10% Commons Insight | wood 1.5 L/season |
| Lean-to | BLD-01 | 0.3 | wood 1 L | Houses 4; warmth +1 | — |
| Hide tent | BLD-02 | 0.4 | hide 3, wood 1 | Houses 5; warmth +1; **packs to 1 L** (moves with the band) | — |
| Pit house | BLD-03 | 2.0 | wood 4 L | Houses 6; warmth +2 | rebuild every 10 yrs |
| Wattle & daub house | BLD-04 | 2.5 | wood 3 L, clay 2 L | Houses 8; warmth +2 | — |
| Mudbrick house | BLD-05 | 3.0 | mudbrick 60 | Houses 8; warmth +2; fire-resistant | — |
| Longhouse | BLD-06 | 8.0 | wood 12 L, clay 4 L | Houses 25; warmth +2; +Cohesion | — |
| Storage pit | FOD-03 | 0.5 | — | 15 R, spoilage ×0.5 | — |
| Drying rack | FOD-02 | 0.2 | wood 0.5 L | Enables smoking/drying at this camp | — |
| Granary | FOD-06 | 4.0 | mudbrick 80 | 80 R, spoilage ×0.25 | — |
| Kiln | FIR-07 | 2.0 | clay 3 L, stone 2 L | Pottery breakage 20%; enables lime and copper | — |
| Grinding stone / quern | STN-09 | 0.5 | stone 1 L | Cereal food value +30% for 10 people | — |
| Thorn barrier | DEF-01 | 0.6 | wood 2 L | Predator attacks −60%; raiders slowed ×0.5 entering | rebuild yearly |
| Palisade | DEF-03 | 6.0 per hamlet ring (×2 village) | wood 20 L (×2) | Settlement defense ×2 (13 §6) | 0.2 ws/yr |
| Lookout post | DEF-02 | 0.5 | wood 1 L | +3 sight; raid warning (13 §5) | — |
| Seasonal camp | WAY-02 | 0.2 | hides or brush | 2 per settlement founded in E1–E3, placed 1–3 tiles beyond its claim radius; claims radius 2 at 50% of the band's strength ([05 §11.1](05-simulation-structure.md#111-claim-field)); used by the seasonal round | — |
| Marker (cairn or totem) | TER-03 | 0.3 | stone or wood 1 L | Claim +30% within radius 2 | — |
| Shrine | BEL-05 | 2.0 | stone 4 L | Pilgrimage site: inter-band Cohesion; claim +0.5 in radius 3 | — |
| Weir | HNT-09 | 1.5 | wood 3 L, stone 2 L | §4.3 | 0.1 ws/season |
| Field | AGR-01 | clearing (§4.4.1) | — | §4.4 | yearly labor |
| Pasture | HUS-03 | 0.2 per tile (fence) | wood 1 L | Holds herd | — |
| Orchard | FOR-07 | 1.0 per tile | saplings | Matures after 6 yrs, then 3 R/tile/yr for 50 yrs | 0.1 ws/yr |
| **Megalith** (Wonder) | STN-10 | 120 | stone blocks 60 L; rollers (MEC-02) | 10.1 Wonder; Cohesion within 8; seasonal festival | — |

Housing is **build-limited** (09 §6.3): the planner builds ahead of demand when labor allows.

## 7. Move and haul

| Rung | Method | Unlocked by | Effect |
|------|--------|-------------|--------|
| 0 | Arms | start | 1 L per figure; walking 3 ticks per open tile |
| 1 | Tumpline & carry frame | TRN-01 | **1.3 L** |
| + | Baskets | FIB-05 | ×1.5 for forage and food (not stone or wood) |
| 2 | Dog travois | TRN-02 + HUS-01 | +2 L per dog (1 dog per 4 people); camp moves 2× faster |
| + | Snowshoes & skis | TRN-03 | Snow tiles ×1 instead of ×2 cost (×0.7 cost in Cold climate) |
| 3 | Pack animals | TRN-04 | Goat or cattle pack: +3 L per animal |
| + | Rollers & sledges | MEC-02 | Heavy items (blocks, logs) moved at 0.5 speed instead of impossible |

**Terrain cost multipliers** (on the 3-tick base): grassland 1.0, open woodland 1.2, forest 1.5, hills 2.0, wetland 2.0, snow 2.0, desert 1.5, mountain 4.0, peak impassable. **Trails** (WAY-01) form after ~20 crossings: ×0.8.

**Watercraft:**

| Craft | Unlocked by | Capacity | Speed | Range |
|-------|-------------|---------:|-------|-------|
| Raft | SEA-01 | 3 L / 3 people | ×0.7 | Rivers, lakes, straits of 1 tile |
| Bark/hide canoe | SEA-02 | 2 L / 2 people | ×2 on rivers | Rivers, lakes |
| Dugout | SEA-03 | 4 L / 4 people | ×2 | + coastal water within 2 tiles of land |
| Outrigger | SEA-04 | 4 L / 4 people | ×2.5 | + open-sea voyages: water within 11 tiles of land, islands up to 22 tiles across, shipwreck risk (21) |

## 8. People actions (heal, teach, ritual, govern, scout)

| Action | Unlocked by | Effect |
|--------|-------------|--------|
| **Tend the sick** | MED-01 | A healer spends 0.1 ws per patient-season: disease death hazard ×0.75 for that patient; recovery 1 season sooner |
| **Set bones & dress wounds** | MED-02 | Injury death ×0.7 |
| **Midwife** (role) | MED-03 | Age-0 hazard ×0.75; maternal deaths ×0.6 |
| **Tell stories** (evening, no ws cost) | ART-02 | Know-how gain +20%; Cohesion +1/season at a hearth |
| **Demonstrate** | LRN-01 | `p_teach` ×1.5 |
| **Bury the dead** | BEL-01 | 0.05 ws per burial; Cohesion loss from a death ×0.5; burial ground claim (11) |
| **Feast** | SOC-05 | Consumes 0.5 R per guest (0.35 with FOD-05): Cohesion +8 for a year; a Legend chance for the host |
| **Council** | GOV-01 | Elders resolve disputes: violence hazard ×0.5 (09 §4.2) |
| **Scout** | start (WAY-01 +50%) | Routine reach by era 6 · 9 · 12 tiles (E0–E2) around settlements; scouts go beyond toward Beacons ([13 §2](13-contact-and-conflict.md#2-reach-scouting-and-awareness)) |
| **Gift / barter** | TRD-01 / TRD-02 | [13 §4](13-contact-and-conflict.md#4-gifts-barter-and-exchange) |
| **Raid / fight / guard** | WAR-01…04, DEF | [13 §5–7](13-contact-and-conflict.md) |

## 9. Warmth (the cold rule)

Each figure's **warmth tiers** = fire (+1 when at a camp with a hearth or kept fire) + shelter (lean-to/tent +1, pit house or better +2) + clothing (hide wrap +1, tailored +2, cloth +1). The highest of each category counts. Shelter places and available whole garments are assigned in ascending figure-id order, with warmer shelter and clothing first; each figure receives at most one shelter and one garment.

| Climate band | Winter tiers needed | Summer |
|--------------|-------------------:|-------:|
| Tropical | 0 | 0 |
| Warm / Arid | 1 | 0 |
| Temperate | 2 | 0 |
| Cold | 3 (so **FIB-04** is needed to live there) | 1 |

Each missing warmth tier raises the active share of the exposure cause budget described in 09 §4.2–4.3; the live hazard is scaled by `deficit / required tiers`, doubled in Cold climates (capped at 2), concentrated in Winter, and multiplied by completed exposure-cause catalog modifiers. It is not a second hazard added on top of the Mort* target. Food need also rises ×1.15 while warmth is short. **Fire Keeping (FIR-01)** fire can go out: if the camp's fire-keeper is absent for 10 ticks without fuel, the fire dies, and only a wildfire (12 §7) or a neighbor gives it back. The headless E0–E2 model assigns the keeper deterministically and budgets 0.1 work-season to tending; while the keeper is absent, one stored wood fuels one season. **Fire Making (FIR-02)** removes the loss risk. A hearth provides fire warmth without a keeper.

**Warmth tiers are the only source of cold deaths in the live simulation.** The `Hazard(exposure, …)` values in [11](11-e0-e2-project-specs.md#1-effect-primitives-syntax-used-in-the-tables) are population averages for the balance model and the C3 check. They describe what these tiers should produce across a whole population; they are never applied on top of them.

## 10. Goods registry (E0–E2)

| Id | Good | Category | First from | Spoilage / durability | Load | Barter value |
|----|------|----------|-----------|----------------------|-----:|-------------:|
| G-MEAT | Meat (fresh) | Food | start | 60%/season | 0.5 per R | 1 |
| G-PLANT | Plant food (fresh) | Food | start | 40% | 0.5 | 0.8 |
| G-ROOT | Roots & tubers | Food | FOR-02 | 15% | 0.5 | 0.8 |
| G-NUTS | Nuts & seeds | Food | FOR-03 | 5% | 0.5 | 1.2 |
| G-WGRAIN | Wild grain | Food | FOR-04 | 8% | 0.4 | 1.2 |
| G-GRAIN | Grain (domestic) | Food | AGR-02 | 5% (+10% pests) | 0.4 | 1.2 |
| G-FISH | Fish (fresh) | Food | HNT-07 | 70% | 0.5 | 1 |
| G-DRIED | Dried / smoked meat & fish | Food | FOD-02 | 10% | 0.3 | 1.5 |
| G-BREW | Beer & bread | Food (feast) | FOD-05 | 15% | 0.5 | 2 |
| G-WOOD | Wood / fuel | Material | start | — | 1 | 0.3 |
| G-STONE | Stone (knapping) | Material | start | — | 1 | 0.2 |
| G-FLINT | Flint / chert | Material | MIN-01 | — | 1 | 1 |
| G-OCHRE | Ochre | Material | MIN-01 | — | 1 | 2 |
| G-FIBER | Plant fiber | Material | FIB-01 | 10% | 1 | 0.3 |
| G-HIDE | Hide (raw) | Material | start | 30% | 0.5 | 0.6 |
| G-BONE | Bone & antler | Material | start | — | 1 | 0.3 |
| G-CORD | Cord | Processed | FIB-01 | durable | 0.1 per 10 | 0.5 per 10 |
| G-RAWHIDE / G-LEATHER | Rawhide / Leather | Processed | FIB-02 / FIB-03 | 5% / durable | 0.5 | 1 / 2 |
| G-CLAY | Clay | Material | CER-01 / BLD-05 | — | 1 | 0.2 |
| G-BRICK | Mudbrick | Processed | BLD-05 | durable | 0.1 per 10 | 0.2 per 10 |
| G-YARN / G-CLOTH | Yarn / Cloth | Processed | FIB-06 / FIB-07 | durable | 0.2 | 1 / 4 |
| G-LIME | Lime | Processed | CHM-03 | durable | 1 | 1.5 |
| G-PIGMENT | Pigment | Processed | CHM-01 | durable | 0.1 | 2 |
| G-TAR | Birch tar | Processed | CHM-02 | durable | 0.1 | 1.5 |
| G-CHAR | Charcoal | Fuel | FIR-08 | durable | 1 | 1.5 |
| G-COPPER | Copper | Metal | MET-02 | durable | 1 | 20 |
| G-TOOL1…7 | Tool kits T1–T7 | Tools | STN lineage | §5.2 | 0.3 | 1 · 1.5 · 2 · 3 · 4 · 5 · 8 |
| G-SPEAR / G-ATLATL / G-BOW | Hunting weapons | Tools | HNT lineage | 5 / 8 / 10 ws | 0.3 | 2 / 3 / 4 |
| G-HARPOON / G-FISHKIT / G-NET | Fishing gear | Tools | HNT-07/08 | 6 / 8 / 12 ws | 0.3 | 2 / 2 / 4 |
| G-SICKLE | Microlith sickle | Tools | FOR-04 | 6 ws | 0.2 | 2 |
| G-STICK | Digging stick | Tools | WOD-01 | 4 ws | 0.2 | 0.5 |
| G-NEEDLE | Bone needle set | Tools | STN-07 | 6 ws | 0.1 | 3 |
| G-BASKET / G-POT / G-JAR | Containers | Tools | FIB-05 / CER-02 / CER-03 | 8 ws / 10% breakage per move / same | 0.3 | 1 / 1.5 / 2 |
| G-WRAP / G-TAILORED / G-GARMENT | Clothing | Clothing | FIB-02 / FIB-04 / FIB-07 | 8 / 12 / 16 seasons | 0.3 | 2 / 6 / 6 |
| G-BEADS | Beads | Luxury | ART-01 | durable | 0.05 | 5 |
| G-FIGURINE | Figurine | Luxury | CER-01 | durable | 0.1 | 4 |
| G-CARVING | Carving | Luxury | WOD-02 | durable | 0.1 | 4 |
| G-POLAXE | Polished axe (prestige) | Luxury / tool | STN-08 | 10 ws | 0.4 | 12 |
| G-COPORN | Copper ornament | Luxury | MET-01 | durable | 0.05 | 25 |
| G-TENT | Tent kit | Shelter | BLD-02 | 30 seasons | 1 | 6 |

**Barter value** is the base for trade (13 §4): the exchange rate between two civilizations is base value × local scarcity (stock ÷ need). Luxuries carry Cohesion: each luxury in stores adds +0.1 Cohesion per 10 people, up to +5.

## 11. Know-how mapping (practice → lineage), E0–E2

Practice events (05 §16.2) are generated per completed work step. Each action family splits its Know-how across lineages. The generation rule is from [07](07-balance-findings.md) §5 #7: 35% of the practising figure's Commons Insight.

| Action | Lineage split |
|--------|---------------|
| Forage | FOR 80%, FIB 10% (gathering fiber), INQ 10% |
| Scavenge / hunt | HNT 70%, WAR 15%, INQ 15% (tracking) |
| Fish | HNT 70%, SEA 30% |
| Farm | AGR 85%, FOR 15% |
| Herd | HUS 90%, TRN 10% |
| Chop | WOD 70%, STN 30% (until E2), then WOD 85%, STN 15% |
| Knap / grind | STN 85%, MIN 15% |
| Fiber / hide / textile | FIB 90%, CHM 10% |
| Clay / pottery | CER 80%, FIR 20% |
| Tend fire, cook | FIR 50%, FOD 50% |
| Preserve, store | FOD 100% |
| Build | BLD 80%, MEC 10%, DEF 10% (if defensive) |
| Haul, travel | TRN 70%, WAY 30% |
| Paddle, sail | SEA 100% |
| Mine, quarry | MIN 80%, STN 20% |
| Smelt | MET 70%, FIR 30% |
| Heal | MED 100% |
| Teach, tell stories | LRN 40%, ART 30%, COM 30% |
| Ritual, burial | BEL 80%, ART 20% |
| Feast, socialize | SOC 70%, FOD 30% |
| Council | GOV 100% |
| Scout | WAY 70%, TER 20%, INQ 10% |
| Mark, patrol | TER 80%, DEF 20% |
| Trade, gift | TRD 70%, DIP 30% |
| Meet strangers | DIP 70%, COM 30% |
| Guard, fight, raid | WAR 60%, DEF 40% |
| Count stores (with NUM-01) | NUM 100% of a 0.2 bonus on store work |

## 12. Efficiency check: E0 → E2 growth

| Action | Start | End E0 | End E1 | End E2 | Target (02 §12: ×1.5–2 per era) |
|--------|------:|-------:|-------:|-------:|-------|
| Forage R/ws | 1.6 | 3.1 (lore + stick) | 4.5–7.0 (baskets, sickle, stand tiles) | 7.0 + fields | ✅ |
| Meat R/ws | 0.9 | 2.2 | 3.0–3.6 (bow, drives, dogs) | 3.6 + herds | ✅ (flattens in E2 by design: herding takes over) |
| Wood L/ws | 2.0 | 6.0 | 6.0 (durability ×1.5) | 8.0 | ✅ (front-loaded in E0) |
| Food per tile (settlement land) | 1 | 1.3 | 2.0 | 4–8 (fields) | ✅ the Neolithic jump |
| Carry L per figure | 1.0 | 1.3 | 1.95–3.3 | 4.3 + pack animals | ✅ |
