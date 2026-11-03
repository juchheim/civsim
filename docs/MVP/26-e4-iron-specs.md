# 26 — Iron (E4) Content Specs: Registries, Effects and All 56 Projects

> **Approved WP2/WP3 baseline.** The scope/design in [25 — Iron Age](25-iron-age.md) is approved, and this spec guides WP2 oracle work and WP3 registry/typed-draft preparation. All E4 numbers marked **P** remain provisional, unfit, and not evidence of oracle/live performance; every E4 project stays `status: draft` pending WP5 mode-aware live and forecast eligibility and later runtime implementation. Existing E0–E3 entries, runtime behavior, thresholds, modes and goldens remain unchanged. Structural conventions follow [23 — E3 specs](23-e3-bronze-specs.md); research semantics follow [02](02-research-structure.md), gates and current graph [04 §5.2](04-research-paths.md#52-e4-iron--key-5-feudal-order), and catalog data is in `content/catalog/*.yaml`.
>
> **Scope discrepancy remains unresolved:** audited source contains **56 regular E4 drafts** (Craft 15, Kin 17 including ART-08, Reach 24) plus **KEY-5**, for 57 E4 nodes total. The M11 brief says 59 projects. The source table in [03 §4.1](03-research-catalog.md#41-projects-per-era-and-theme) also gives E4 as 56 and assigns 59 to E1 Hunters, a plausible but unconfirmed source of the brief's number. No two projects are invented to force that count. Q1 in [25 §15](25-iron-age.md#15-risks-and-unresolved-decisions) remains open; this document specifies the audited 56 + 1 only.

## 1. Notation, status, scope and stacking

### 1.1 How to read these specs

- **P** marks a provisional numeric interpretation. It is not a tuned value and makes no acceptance claim. Catalog phrases that do not identify a unique safe numeric meaning have a bounded interpretation here and retain a pointer to the relevant open question in 25.
- Effects use the existing typed primitives described in [23 §1](23-e3-bronze-specs.md#1-notation-new-effect-kinds-and-stacking): `method`, `mod`, `cap`, `build`, `wonder`, `good`, `role`, `unlock_action`, `tier`, `cohesion`, `adopt`, and bounded behavioral `rule`, `claim`, `reach`, `reveal`, `diplo`, `combat`, `defense`, `gate`, `legacy`. No effect is applied solely because it is researched if it requires a local good, kit, structure, role, route, or terrain.
- `pct` is a fractional delta (−0.25 = −25%); `mult` is a factor; `flat` an additive amount; `set` a replacement. Across projects, independent percentage multipliers combine multiplicatively; within a project same-stat deltas combine additively. Apply set, flat, pct, then mult, consistent with 23. Tool performance belongs to the actual carried tool tier and is never multiplied again by knowing the project.
- `[Seen]` blocks research until exposure; `[Has]` blocks use without local/traded access; `[Near]` limits use to valid terrain. Gates do not otherwise change research state. Existing prerequisites are copied verbatim, including `requires_any`.
- All costs are the list cost before diffusion, EraLag, Know-how, and soft-gate discounts. There are no per-project overrides.
- Units: season, **ws** (one worker-season of work), **R** (ration), **L** (bulk volume), tile, and population are as in 10/23. Simulation remains 1:1; no population/Insight/labor scaling is implied by the E4 catalog's legacy `rep_scale: 2` (25 Q3).

### 1.2 E4 base and type costs

Use the current registry value `EraBase[E4] = 1200` and existing type multipliers from 02 §4. These list costs are **P** as content inputs, not a fitted cadence claim.

| Type | D | M | S | R | I | W | K |
|---|---:|---:|---:|---:|---:|---:|---:|
| Multiplier | 1.0 | 1.0 | 1.2 | 0.8 | 1.5 | 2.0 | 2.5 |
| E4 cost | 1200 | 1200 | 1440 | 960 | 1800 | 2400 | 3000 |

Audit tally: Craft 15 / Kin 17 / Reach 24 = **56 regulars**; type mix D 1, M 25, S 13, I 15, W 2, R 0, plus KEY-5 K. Regular list-cost sum: **81,720 P** (Craft 18,720; Kin 26,520; Reach 36,480). KEY-5 list cost: **3,000 P**. This is not a closure sum or a time estimate; the historic 04 closure summary is not an oracle result.

### 1.3 Shared semantic choices and bounded interpretations

The values below narrow catalog prose without claiming that an unavailable effect system already exists. Effects are civilization-wide unless their row names a local object, service, terrain or scope.

| Phrase / effect | Numeric/content interpretation for this draft | Limit / open decision |
|---|---|---|
| `Overreach −x%` | `admin.overreach: x`, combined with other reductions as `1 − Π(1−x)` following 23's admin semantics. Full-civ census visibility is a separate planner capability, not more Insight. | Province administration remains staffed and connected (25 Q4). |
| `Insight +x%` | `mod` on the named pool only (Commons, Scholar, or all named MED effects), not global research and not compounded on base role Insight. | Check all pools in live model before promotion. |
| `build speed +x%` | Labor multiplier `1/(1+x)` for eligible construction work. Where prose says “×2 build,” multiplier is 0.5 labor. | Do not stack duplicate applicability to claim universal speed. |
| `cohesion +` | Explicit target delta / distance strain reduction, never direct ownership or claim transfer. | Numeric target scales should be validated against E3 trajectories. |
| “+” / “common” / “better allocation” with no amount | Use a bounded, explicit effect below or unlock-only behavior; do not silently translate it into an arbitrary bonus. | Open Q4–Q9 in 25 remain applicable. |
| New structures and Wonders | Unlocking makes a build option legal; it does not construct it or provide materials, staff, capacity, or a free city. Great Dome is **unlocked only**, construction deferred (Q7). | No Wonder construction cost/score change specified here. |

## 2. Registry additions (proposed)

These are additions for later transcription; append-only. Existing E3 bloomery, `G-IRON`, charcoal recipe, actions, structures, goods and world generation remain as-is. Proposed recipes use the existing `recipes` schema/order convention from 23 §5. Quantities are batch quantities; all output must be debited from inputs before transfer, upgrades, capture, or shipment.

### 2.1 Goods

| ID | Name / category | First from | Durability / load / barter | Notes |
|---|---|---|---|---|
| `G-WROUGHT` | Forged wrought iron / metal | MET-09 | durable / 1 / 18 **P** | Intermediate; made from bloom. |
| `G-STEEL` | Carburized steel edge stock / metal | MET-10 | durable / 1 / 30 **P** | Edge stock, not bulk industrial steel. |
| `G-TOOL11` | Wrought iron tool kit (T11) / tool | MET-09 | 24 tool-ws / 0.3 / 14 **P** | Broad iron kit. |
| `G-TOOL12` | Tempered steel-edge kit (T12) / tool | MET-10 | 48 tool-ws / 0.3 / 24 **P** | T12 service is exactly twice T11. |
| `G-ARMS3` | Iron arms / arms | WAR-08 | 24 seasons / 0.5 / 18 **P** | Weapon strength **2.4 P**; consumes actual iron and wood/leather. |
| `G-ARMS4` | Tempered-edge arms / arms | MET-10 | 48 seasons / 0.5 / 30 **P** | Weapon strength **3.0 P** (= 2.4 × 1.25 from MET-10 once); no duplicate WAR-08/MET-10 +25% stack. |
| `G-FBRICK` | Fired brick / material | CER-08 | durable / 1 / 0.4 **P** | Separate from E2 mudbrick. |
| `G-TILE` | Roof tile / material | CER-08 | durable / 1 / 0.6 **P** | Town fire-resistant roofing. |
| `G-PLANK` | Saw-cut plank / material | WOD-08 | durable / 0.5 / 0.5 **P** | Defined output of `saw_planks`; no free construction material. |
| `G-BLOWNGLASS` | Blown glass / processed | CER-07 | durable / 0.2 / 16 **P** | Distinct stock from E3 glass beads/vessels where needed for production accounting. |
| `G-BARREL` | Barrel / container | WOD-07 | 40 seasons / 0.4 / 4 **P** | Reusable container; does not create capacity without transport. |
| `G-GRAPE` | Grapes / food | FOR-08 | 20% spoilage / 0.5 / 1 **P** | Harvested only from grafted grape orchard job; 1 L per orchard ws on a grape orchard **P**. |
| `G-OLIVE` | Olives / food | FOR-08 | 10% spoilage / 0.5 / 1 **P** | Harvested only from grafted olive orchard job; 1 L per orchard ws on an olive orchard **P**. |
| `G-WINE` | Wine / food | FOR-08 | 20% spoilage / 0.5 / 3 **P** | Made from G-GRAPE with pressing labor. |
| `G-OILPLANT` | Pressed olive oil / food | FOR-08 | 10% spoilage / 0.5 / 3 **P** | Made from G-OLIVE with pressing labor. |
| `G-ENGINE` | Counterweighted siege engine / tool | WAR-09 | 12 seasons / 0 load / 12 **P** | One assembled engine, carried by train, requires 4 crew; costs/upkeep in §3.2. |

### 2.2 Tool tiers and tool-work multipliers

As with 23 §4, performance is per equipped kit, using its modifiers once. Do not implement an extra civilization-wide `mod` for the same rates. All new rates are **P**, except where the intention is explicitly named by the catalog (`×1.4` saw and T12 durability ×2). Relative baseline for the T11 entry is the current best T10 rates; harvest and construction modifiers use lower labor as stated.

| Tier | Kit / output | Unlock | Service | Per-kit mods |
|---|---|---|---:|---|
| T11 | G-TOOL11, 1 kit | MET-09 | 24 ws | Chop yield/rate ×7.2 vs untooled base (and ×1.2 vs T10 ×6); mine ×1.5; quarry ×1.5; harvest labor ×0.75; timber construction labor ×0.80. **P** |
| T12 | G-TOOL12, 1 kit | MET-10 | 48 ws | Same base work-rate modifiers as T11; service life ×2 only. **P** durability intention ×2. |
| Saw attachment | WOD-08 on T12 only | WOD-08 + MET-10 | T12 kit wear | Chop ×1.4 relative to the T12 iron axe; `saw_planks` outputs 2 G-PLANK from 1 G-WOOD vs 1 baseline. The ×1.4 is catalog intention; plank yield is bounded to ×2 per work batch. **P** labor accounting. |

Rates compose by the equipped kit: a T12 kit replaces T11, and WOD-08 saw modifies T12 where the saw job is equipped. Harvest `×0.75 labor` is applied to harvest work only; timber construction `×0.80 labor` applies timber structures only. No free tools, auto-upgrade, or metal refund on retirement. Mixed kits wear independently.

### 2.3 Recipes and embodied input ledger

All quantities and labor values are **P**. Existing E3 `bloomery` remains unchanged: 2 G-IRONORE + 3 G-CHAR → 0.5 G-IRON, 0.8 ws. `burn_charcoal` remains unchanged: 6 G-WOOD → 1 G-CHAR, 0.4 ws. Fuel is physical; water power reduces eligible mechanical labor only and does not make charcoal.

| Recipe | Inputs → outputs | Labor | Order / structure | Unlock |
|---|---|---:|---|---|
| `forge_wrought` | 0.5 G-IRON + 0.25 G-CHAR → 0.4 G-WROUGHT | 0.4 ws | 40 / forge | MET-09 |
| `carburize_steel` | 0.4 G-WROUGHT + 0.2 G-CHAR → 0.4 G-STEEL | 0.4 ws | 41 / forge | MET-10 |
| `make_tool11` | 0.15 G-WROUGHT + 0.15 G-CHAR → 1 G-TOOL11 | 0.2 ws | 42 / forge | MET-09 |
| `make_tool12` | 0.15 G-STEEL + 0.10 G-CHAR → 1 G-TOOL12 | 0.3 ws | 42 / forge | MET-10 |
| `make_iron_arms` | 0.25 G-WROUGHT + 0.4 G-WOOD + 0.3 G-LEATHER (or hide) → 1 G-ARMS3 | 0.4 ws | 42 / forge | WAR-08 |
| `make_steel_arms` | 0.25 G-STEEL + 0.4 G-WOOD + 0.3 G-LEATHER (or hide) → 1 G-ARMS4 | 0.5 ws | 42 / forge | MET-10 + WAR-08 |
| `blow_glass` | 0.5 G-GLASS + 0.5 G-CHAR → 4 G-BLOWNGLASS | 0.3 ws | 50 / smelter | CER-07 |
| `saw_planks` | 1 G-WOOD → 2 G-PLANK (baseline plank output 1 per batch) | 0.2 ws | 50 / — | WOD-08; T12 saw required |
| `fire_brick_tile` | 1 G-CLAY + 0.2 G-CHAR → 0.8 G-FBRICK + 0.2 G-TILE | 0.3 ws | 50 / kiln | CER-08 |
| `press_wine` | 2 G-GRAPE → 1 G-WINE | 0.2 ws | 50 / press | FOR-08 + MEC-05 |
| `press_oil` | 2 G-OLIVE → 1 G-OILPLANT | 0.2 ws | 50 / press | FOR-08 + MEC-05 |
| `make_siege_engine` | 8 G-WOOD + 4 G-BLOCK + 0.4 G-WROUGHT → 1 G-ENGINE | 24 ws (includes assembly) | 45 / forge | WAR-09 |
| `make_barrel` | 1 G-WOOD + 0.1 G-IRON (or 0.1 G-BRONZE) → 1 G-BARREL | 0.4 ws | 50 / — | WOD-07 |

**Hand check: one T11 kit embodied charcoal/wood (warning, not a pass).** A T11 kit's 0.15 G-WROUGHT input is 0.375 of the `forge_wrought` batch, so its upstream share is **0.1875 G-IRON and 0.09375 G-CHAR**. The kit recipe adds 0.15 G-CHAR, making 0.24375 charcoal for forging and kit fabrication. Locally making the 0.1875 bloom consumes another 0.75 G-IRONORE and 1.125 G-CHAR in the unchanged E3 bloomery. Total is **0.75 G-IRONORE + 1.36875 G-CHAR**. At 6 G-WOOD/charcoal, this is **8.2125 G-WOOD per T11 kit**. Labor: bloomery `0.3 ws` + wrought forging `0.15 ws` + kit making `0.2 ws` + charcoal burning `1.36875 × 0.4 = 0.5475 ws` = **about 1.1975 ws**, excluding mining, hauling, tool wear, and rounding. This fuel burden is a warning requiring oracle/live fit, not a pass. The ledger must avoid double-counting any bloomery charge.

## 3. Actions, structures and roles

### 3.1 Actions / ladders

| Action / capability | E4 addition | Semantics |
|---|---|---|
| `chop` | T11 iron axe rung; T12 is a kit tier, not a separate rung; WOD-08 saw attachment | T11 ×7.2 base; saw ×1.4 relative T12 iron axe. Plank yield ×2 only when making planks. |
| `mine`, `quarry` | T11/T12 equipped-kit modifiers | ×1.5 work throughput vs current method. Access and deposit depletion unchanged. |
| `farm` / harvest | Iron sickle/scythe method, AGR-09 | ×2 harvest output per work (catalog intent); T11 tool-tier labor ×0.75 applies only to eligible harvest jobs. Do not multiply output and labor into unbounded yield: they are distinct work/output semantics. |
| `till` | Mouldboard, AGR-10 | Heavy-clay temperate-forest plots become eligible/prime; no universal food multiplier. Heavy-plough work cost/rate is baseline ×1.0 P pending oracle. |
| `grind` | NRG-03 water mill | ×5 grinding output per eligible work unit, only at built water mill on River [Near]. Existing mechanical jobs remain otherwise. |
| `haul` / `move` | TRN-07 roads; TRN-08 bridges | Road movement ×1.5 on completed roads; bridges remove river road discontinuity; actual cargo/worker movement still costs time. |
| `build` | T11/T12 and listed structures | General wood/stone modifiers are scoped by tool/structure; construction work multipliers stated in nodes. |
| `faith_spread`, `siege_assault`, `province_admin`, `naval_blockade` | New behavioral activities | Deterministic jobs/actions with route, supply, crew and staffing requirements in §7; no instantaneous global unlock effect. |

Each project table uses an existing practice ID from `content/registries/practice.yaml`; each has positive Know-how for that project's lineage. The separate system-job practice rows immediately above are proposed additions only for the new siege, blockade, faith-spread and province-administration activities. The catalog does not have a separate `harvest` action; this draft attaches Scythe ×2 to the existing `farm` method at harvest stage. No other existing action ladder is silently extended. A ladder rung requires a distinct method record consistent with 02 §12 and catalog IDs.

#### Registry rows for new E4 activities and practice

Most jobs reuse current action/practice IDs listed in the project tables. The two new action families below are required for the distinct combat systems; the four practice mappings are appended to `practice.yaml`. Shares sum to 100 and feed their stated lineages. All rates/semantics are **P**.

| Registry | Proposed row | Purpose |
|---|---|---|
| actions | `{id: siege_assault, name: Engine assault, class: activity}` | Resolves a supplied engine assault against a wall; existing `fight` handles ordinary land combat. |
| actions | `{id: naval_blockade, name: Harbor blockade, class: activity}` | A staffed galley maintains a blockade of one known harbor. |
| practice | `{id: siege_assault, knowhow: {WAR: 60, DEF: 40}}` | Optional practice credit while engine assault jobs are performed. |
| practice | `{id: naval_blockade, knowhow: {WAR: 50, SEA: 50}}` | Optional practice credit while a blockade is maintained. |
| practice | `{id: faith_spread, knowhow: {BEL: 80, DIP: 20}}` | Role-work credit for missionaries; the project table continues to use existing `ritual`. |
| practice | `{id: province_admin, knowhow: {GOV: 60, TER: 40}}` | Role-work credit for active governors; projects continue to use `council` / `mark`. |

All other E4 practices use current action IDs from `content/registries/practice.yaml`; no project practice ID points to an unregistered action. `faith_spread` and `province_admin` are role work markers, not requirements that add a separate research project.

### 3.2 Structures

All material/labor/capacity values below are **P**. `Build` unlocks only; costs are paid on each local construction. Staffing/upkeep is required where specified.

| Structure ID / name | Unlock | Materials; labor | Function / bounds |
|---|---|---|---|
| terrace | AGR-08 | 4 G-BLOCK + 2 G-WOOD; 8 ws | Adds one eligible hillside/mountain farm plot; no conversion of impassable rock or food grant. |
| theatre / arena | ART-08 | 12 G-BLOCK + 4 G-WOOD; 24 ws | One civic venue per city; local Cohesion target +15 points **P** while maintained; unrest pressure −10% **P**; no global Cohesion. Q4/Q5. |
| aqueduct | BLD-10 | 20 G-BLOCK + 8 G-ASHLAR + 5 G-WOOD; 40 ws | City water infrastructure, raises water-limited effective city cap by 40% **P** and disease term ×0.85 in served city. No food/housing creation. |
| crane | MEC-06 | 8 G-WOOD + 2 G-IRON; 16 ws | Large-structure/Wonder eligible construction labor ×0.5 (build ×2), only jobs staffed at a settlement with crane. |
| water_mill | NRG-03 | 8 G-WOOD + 6 G-BLOCK + 1 G-WHEEL; 18 ws | Requires river adjacency; enables grind ×5 and drives eligible saw/bellows/trip-hammer jobs. Mechanical labor only; no fuel substitution. |
| harbor | SEA-07 | 12 G-WOOD + 8 G-BLOCK; 24 ws | Coast site; ship cargo capacity ×2, port city population support ×1.2 only when food/water/housing support exists. |
| academy | LRN-05 | 8 G-BLOCK + 2 G-WOOD; 16 ws | Unlocks/staffs Scholar role at current registry rate; registry should append role `scholar` use to Academy; cap 1 per academy. |
| bathhouse / latrines | MED-06 | 8 G-BLOCK + 4 G-WOOD; 20 ws | Staffed/maintained city sanitation; density disease pressure ×0.75. Use the E3 health cause model, not all-cause mortality. |
| gatehouse / towers | DEF-06 | 12 G-BLOCK + 6 G-WOOD; 30 ws | Built on a wall; intact wall defense multiplier rises 3.0→4.5 (1.5×); gate allows friendly trade only along an open, unblocked route. |
| frontier_wall_segment | DEF-07 | 4 G-BLOCK + 2 G-WOOD per segment; 12 ws | Adjacent crossing only: border flip pressure ×0.5 and raid success ×0.3 where crossing uses built segment. Never realm-wide. |
| castle | DEF-08 | 20 G-ASHLAR + 12 G-WOOD + 2 G-IRON; 60 ws | Strongpoint/claim anchor, radius 5 per catalog intent, subject to existing claim rules; garrison required to hold the region. |
| press | MEC-05 | 4 G-WOOD + 2 G-IRON; 8 ws | Enables listed wine/oil press recipes. |
| Deployable siege engine (G-ENGINE) | WAR-09 | Produced once by `make_siege_engine` in §2.3; upkeep 0.2 G-CHAR/season and 4 crew **P**; no second construction charge | Requires supplied train; transported engine cannot be concurrently used for civilian hauling; one engine reduces matched assault duration to 0.4× candidate. |
| bridge | TRN-08 | 6 G-BLOCK + 2 G-ASHLAR + 2 G-WOOD; 16 ws | Maintains road continuity across river at a river crossing; not a transport-capacity multiplier. |
| colonnade / temple_tier2 | STN-12 | Append structure option/cosmetic tier to existing venue/temple; 8 G-ASHLAR + 4 G-WOOD; 16 ws | Adds classical visual; stone eligible construction labor ×0.6 (40% faster), local to stone construction jobs. |
| public_grain_service | FOD-09 | Uses existing granary; no free store | Transfers physical grain from donor stores above winter reserve; city food-security protection and cap target as in §5. |
| great_dome / great_library / great_lighthouse | BLD-11 / LRN-06 / SEA-08 | Wonders' build costs are unspecified here | Unlock only. Great Dome construction specifically deferred pending Q7; no auto-build. Library/Lighthouse use existing Wonder framework if later approved. |

### 3.3 Roles

| Role ID | Unlock | Slots / output | P scope |
|---|---|---|---|
| administrator | GOV-07 | 1 per capital or census office; 0 Insight | Enables census/full-civ planner view and contributes to admin service. Must be staffed. |
| governor | TER-08 | 1 per connected province seat; 0 Insight | Reduces Overreach by 30% in that governed province (admin combination semantics). No benefit if absent/disconnected. |
| soldier | WAR-07 | Capacity: `floor(0.10 × adults)` ordinary; 20% after WAR-08; 0.25 R/person/season upkeep candidate | Standing soldier slots replace a portion of workers only when staffed, trained and supplied; capacity is not a free army. |
| missionary | BEL-09 | 1 per active faith route/mission; 0 Insight | Route-based conversion job; consumes labor, no instant ownership or map reveal. |
| physician | MED-05 | 1 per settlement with medical texts; 0 Insight | Makes MED effects eligible under existing staffing model; no blanket +15% outside named MED project effects. |
| scholar (existing role) | LRN-05 | Existing 48 Insight/year/person; 1 per built Academy **P** | Reuse existing `scholar` registry record; no duplicate role ID or passive free Insight. |
| scholar | LRN-05 / Academy | Current registry 48 Insight/year/person, staffed at 1 per academy **P** | Academic role unlock does not create a scholar or guarantee academy construction. |

## 4. Constants and systems

These are proposed system-facing constants to support the numeric content. Unless explicitly catalog-intention, every number is **P** and must be fitted/tested. No acceptance thresholds are changed by this spec.

| Constant / rule | Proposal | Rationale / boundary |
|---|---:|---|
| E4 `base_cost` | 1200 | Existing era registry value. |
| E4 `rep_scale` | Existing registry says 2; keep simulation population 1:1; do not convert simulation state. | 25 Q3. |
| E4 reach | 20; claim radius 5; claim multiplier 1.3 | Existing era registry; no new worldgen. |
| Food/water/demography | Keep live E3 mechanisms at entry; no automatic E4 fertility, mortality or food grant. | 25 §5. |
| Town fire risk | CER-08: ×0.5 in town with fired tile/brick on the relevant structure. | Bound to building fire cause only. |
| Building durability | CER-08: +25% durability **P** for fired-brick/tile structures. | Catalog “+” otherwise unbounded. |
| City cap | BLD-10 +40% water-limited cap; FOD-09 +25% target cap. | Targets for oracle, not direct runtime multipliers stacked on food/housing. |
| Sanitation | MED-06 density-disease term ×0.75; BLD-10 served disease term ×0.85. | Distinct validated cause modifiers, combine multiplicatively where same cause. |
| City/republic insight | BEL-08 Scholar/Commons inquiry Insight ×1.10; COM-07 Scribe throughput ×2 and Commons Insight ×1.05; GOV-08 Commons ×1.15. | Scope-only mods. Avoid applying same text to role base and output. |
| Natural philosopher | INQ-04 Scholar Insight ×1.20. | Scholar-only. |
| Medical text effect | MED-05 multiplies eligible E0–E4 MED effect magnitudes by 1.15; no multiplicative cascade on the MED-05 modifier itself. | Physician role separate. |
| MEC/NRG cost | INQ-05 costs for MEC and NRG project types ×0.90 after all base/type costs. | Applies to remaining/current costs consistently; do not retroactively credit completed work. |
| Buoyancy | INQ-05 ship cargo capacity ×1.10; combine Harbor ×2 as 2.2 total. | Cargo capacity only, not movement or ship count. |
| BLD-11 build | Eligible construction work ×(1/1.3)=0.76923; harbor/dome construction option. | Great Dome unlock only pending Q7. |
| MEC-04 build | General eligible construction labor ×(1/1.15)=0.869565. | Does not stack as flat additive percentages. |
| NUM-07 infrastructure | Infrastructure labor/output efficiency ×1.15 (equivalent labor ×0.869565 for same completed road/field/aqueduct work). | Surveyed/laid-out fields, roads, aqueducts only. |
| TRD-06 friction | Trade friction ×0.60; coin/tax enables admin capacity +1 span **P**. | No free currency/store; Q5. |
| COM-08 | Overreach reduction 20%; road adoption spread ×2 | Same route/connection conditions as adoption model. |
| TRN-07 | Road movement ×1.5; adoption along road ×2; road claim-strength bonus +10% **P** | Catalog plus is bounded pending Q4/Q9. |
| HUS-09 | caravan route reach across desert ×3 | Gate is Seen: Camels or Llamas; trade route still requires animal/crew capacity. |
| TRD-07 | Eligible route length ×3, only if either required route mode is possible. | Preserve `requires_any: [HUS-09, SEA-07]`; reach is not free cargo/capacity. |
| WAY-06 | Trade-route efficiency ×1.25 | Apply to trade friction/route throughput; no cargo duplication. |
| NRG-03 | Grind ×5 | River gate and built mill; mechanical power, no free fuel. |
| WAR-09 | Target siege-duration intention ×0.40 | Candidate requires supplied engine/crew. Not a global capitulation timer modifier. |
| WAR training | Professional training ×1.5 vs ordinary; replaces ordinary training multiplier, does not stack. | Arms, rations, crew, staffing and population slots still required. |
| WAR-08 soldier capacity | Base 10% adults; after WAR-08 20% adults. | WAR-08 doubles capacity ceiling, not actual headcount. |
| WAR upkeep | 0.25 ration/person/season | Candidate (25 Q8), physical cost. |
| Weapons | WAR-08 iron arms strength 2.4 (P); MET-10 steel-edge arms strength 3.0 = 2.4 × 1.25 once (P). | Do not apply +25% twice or as a civ-wide multiplier. |
| T11/T12 | As §2.2; T12 durability 2× T11; saw ×1.4 chop, plank yield ×2 | Catalog intentions bounded to item/job. |
| Aqueducts / FOD | Separate water cap and food security target; no creation | Existing capacity system. |
| Governance / fealty | Province assignment deterministic each season; active staffed connected seat only. Feudal protection removes distance/span secession pressure; famine, conquest and other crisis secession remain. | Explicit provisional reading; 25 Q4. |
| Dominion tribute | Tributary transfers scoring/tribute flow, never duplicates land or score. | 25 Q5. |
| Faith conversion | Route-mediated, capped influence and conversion progress; claims pressure is soft only. | 25 §6; no instant ownership. |
| Naval blockade | Requires staffed surviving galley, crew, war, known harbor and continuous presence; blocks only that harbor's maritime route. | 25 §7; full bounded feature still Q6. |

## 5. Project records

Each row specifies the existing YAML identity, type, prerequisites and gates exactly, followed by proposed typed effect meaning, practice activity, Moment (scene), Chronicle flavor, visible change, and required sim capability. **All listed numeric interpretations are P** except explicit catalog intentions noted above. “Practice” is the activity mapping to that project's lineage Know-how; one primary mapping is proposed per record. Text is intentionally concise enough to serve as a transcription checklist.

### 5.1 Craft — 15 regular projects

| ID / name | Type / cost | Requirements / gates | Proposed typed effects (P unless stated) | Practice | Moment / flavor | Visible change / sim |
|---|---|---|---|---|---|---|
| BLD-09 True Arch & Vault | M / 1200 | BLD-07, CER-08 | `rule(arch_vault_support)`; enable bridge, aqueduct, large-hall spans with no timber support; construction material/labor still paid. | build | Stones lock over a timberless span. “The arch carries what the forest cannot.” | New arch silhouette; structures/architecture. |
| BLD-10 Aqueducts | S / 1440 | BLD-09, NUM-07 | `build(aqueduct)`; city cap target +40%; served disease term ×0.85. | build | Water pours into a channel above the roofs. “A distant spring enters the city.” | Aqueduct and water/cap overlay; water and health. |
| BLD-11 Concrete & Domes | M / 1200 | BLD-09, CHM-03 | `mod(build_labor, pct:-0.23077)` (equiv ×1.3 speed); unlocks concrete large-span construction and a dome build option deferred per Q7. SEA-07 separately unlocks harbor use and construction. | build | A concrete vault holds after its wooden frame is removed. “Stone takes the shape of the sky.” | Concrete/dome option; construction. |
| CER-07 Glassblowing | M / 1200 | CER-06 | `good(G-BLOWNGLASS)`; `blow_glass` recipe output ×4 per batch as the bounded reading of “Blown ×4”. | craft | A glowing gather twists into a vessel. “The furnace gives sand a clear voice.” | Blown vessels; recipe/goods. |
| CER-08 Fired Brick & Tile | M / 1200 | CER-04, FIR-07 | `good(G-FBRICK,G-TILE)`; town fire risk ×0.5; durability +25% for fired structures. | clay | Roof tiles are fitted across the market. “A fired roof turns sparks aside.” | Red tiled roofs; good and town fire. |
| MEC-04 Pulley & Windlass | M / 1200 | MEC-03, FIB-05 | `mod(build_labor,pct:-0.130435)` (×1.15 speed); unlocks a buildable windlass well (4 G-WOOD, 8 ws **P**) to access a known water source at depth 2 in that settlement; no new water source or territory-wide access. | build | A counterweight lifts a bucket from the dark. “A small pull raises a great weight.” | Windlass/well job; movement/build. |
| MEC-05 Gears & Screws | M / 1200 | MEC-04, NUM-07 | Unlock press, Archimedean screw, water-wheel gearing; presses require MEC-05 and inputs; enable mine drainage. | build | Interlocking gears turn an endless screw. “A measured turn moves water uphill.” | Press/screw/wheel machinery; recipe/action. |
| MEC-06 Treadwheel Crane | S / 1440 | MEC-05 | `build(crane)`; eligible large structure and Wonder labor ×0.5 with crane present. | build | Workers step inside a wheel and raise a block. “The wheel lifts the weight of a wall.” | Crane; construction. |
| MET-09 Iron Tools | M / 1200 | MET-08 | `good(G-WROUGHT,G-TOOL11)`; `method(chop, iron_axe, mult:7.2)`; T11 item mods in §2.2; unlock forge_wrought/make_tool11. | smelt | Red-hot iron is hammered into an axe edge. “Bloom becomes an edge for every hand.” | Iron tools; tool tier/metal recipe. |
| MET-10 Quench & Temper | M / 1200 | MET-09 | `good(G-STEEL,G-TOOL12)`; unlock T12 recipe; T12 durability ×2 T11; arms: WAR-08 iron weapon strength 2.4, and MET-10 steel-edge strength 3.0 (2.4 × 1.25 exactly once). | smelt | A blade hisses as it meets water. “Heat and water set the edge.” | Tempered edge; recipe/tool/arms. |
| MIN-07 Mine Drainage | M / 1200 | MIN-05, MEC-05 | `rule(mine_depth:2,flooded_deposit_access:true)`; each flooded deposit requires a drainage installation of 1 G-WOOD + 0.2 G-CHAR and 2 ws, then upkeep 0.1 G-CHAR per season while staffed; no new deposit. | mine | A screw lifts pooled water from a lower gallery. “The mine descends beyond the waterline.” | Deep mine/drainage work; mining. |
| NRG-03 Water Wheel | S / 1440 | NRG-02, MEC-05; **Near: River** | `build(water_mill)`; grind output ×5; enables water-powered eligible saw/bellows/trip hammer labor. | work_animals | A river turns a wheel beside the mill. “The current lends its strength to the mill.” | River mill; power and grinding; gate checked at use. |
| STN-12 Ashlar & Columns | M / 1200 | STN-11, MET-09 | Stone construction work ×0.60; unlock classical colonnade and Temple tier 2 options. | knap | An iron chisel squares a column drum. “Stone takes a cleaner line.” | Ashlar colonnade; structure tier. |
| WOD-07 Cooperage | M / 1200 | WOD-05, MET-09, FOD-05 | `good(G-BARREL)`; liquid storage/shipping loss only ×0.60; recipe and reuse require container return. | chop | A cooper binds staves with a hoop. “The cask carries a harvest farther.” | Barrels; goods/transport spoilage. |
| WOD-08 Frame & Crosscut Saws | M / 1200 | WOD-07, MET-10 | T12 saw attachment: chop ×1.4 relative T12 axe; plank output ×2. | chop | Two workers draw a long saw through a log. “A straight cut opens the forest.” | Frame saw and planks; item/job modifier. |

### 5.2 Kin — 17 regular projects (including ART-08)

| ID / name | Type / cost | Requirements / gates | Proposed typed effects (P unless stated) | Practice | Moment / flavor | Visible change / sim |
|---|---|---|---|---|---|---|
| AGR-08 Terracing | S / 1440 | AGR-06, STN-11 | `build(terrace)`; creates one farmable plot on eligible hill/mountain tile, materials paid. | farm | A retaining wall makes a level strip on the slope. “The hillside holds a field.” | Terraces/farm plots; construction/farming. |
| AGR-09 Iron Sickle & Scythe | M / 1200 | AGR-05, MET-09 | `method(farm, rung:scythe, name:iron_scythe, mult:2.0, tool:t11)` (catalog intention applies to harvest-stage crop output only); no population-food grant. | farm | A curved iron blade sweeps through ripe grain. “One stroke gathers what many hands once cut.” | Scythe; harvest ladder. |
| AGR-10 Heavy Plough | M / 1200 | AGR-09, NRG-02 | `method(till,mouldboard)`; heavy-clay temperate-forest plot becomes prime farmland; KEY-5 prerequisite. No universal output bonus. | farm | A mouldboard turns thick soil aside. “The heavy ground opens to seed.” | Plough/soil eligibility; farm system. |
| ART-08 Theatre & Arena | S / 1440 | ART-07, BLD-09 | `build(theatre,arena)`; local city Cohesion target +15 points; unrest pressure −10%. | tell_stories | A chorus speaks beneath a stone arch. “A city sees itself on the stage.” | Theatre/arena; local cohesion and unrest. |
| BEL-08 Philosophy & Ethics | I / 1800 | BEL-07, COM-07 | `mod(scholar_insight,pct:0.10)` and a separate `cohesion` diverse-group integration target +10 points **P**; no instant conversion. | ritual | A teacher and students dispute a question in public. “Reason gives strangers a common measure.” | Academy/Commons insight and group-cohesion overlay. |
| BEL-09 Organized Faith | I / 1800 | BEL-08, TRD-07 | `role(missionary)`; one staffed missionary spends 1 ws/season and converts up to 1% of eligible non-adherent population/year along a connected trade route, capped at 25% of each reached settlement; other-civ converts add +5% soft claim pressure **P** in reached settlements while route and conversion persist, but never transfer ownership; KEY-5 prerequisite. | ritual | A traveling teacher is welcomed at a caravan stop. “Belief travels along the roads people use.” | Missionary role; route faith layer, not ownership. |
| FOD-09 Public Grain Supply | I / 1800 | FOD-07, GOV-07 | `rule(grain_transfer)`; city food-security target gains a public reserve target of 1 R/person; cap +25% target. Grain transfers from donor stocks only above their 1-season winter reserve; no grain is created. | preserve | Officials open a granary as bread is rationed. “The reserve is opened in measured shares.” | Public granary logistics and food overlay. |
| FOR-08 Grafting | M / 1200 | FOR-07, MET-09 | Orchard output ×2 only on grafted orchard jobs; unlock wine/oil goods via actual produce and MEC-05 press recipe. | farm | A graft is bound into a fruit tree. “Two harvests grow from a practiced branch.” | Grafted orchard, wine/oil recipes; no free trade goods. |
| GOV-07 Bureaucracy & Census | I / 1800 | GOV-06, NUM-05 | `role(administrator)`; Overreach −25%; census planner view sees full own-civ stats. | council | A clerk tallies stores across a province. “The ruler can count what distant towns hold.” | Administrator slot/census overlay; planner. |
| GOV-08 Republic & Assemblies | I / 1800 | GOV-06, SOC-10 | Commons Insight ×1.15; Cohesion target +10 **P**; voluntary war-declaration cooldown ×1.25 **P**; immediate defense unaffected. | council | Citizens raise hands before a public decision. “The assembly gives its answer slowly.” | Assembly; civic insight and war policy. |
| GOV-09 Feudal Contract | I / 1800 | GOV-07, WAR-07 | `rule(feudal_contract)`; active connected province removes distance/span secession pressure and grants local defense ×1.15 **P**; famine, conquest and other crisis secession remain. KEY-5 prerequisite. | council | A lord accepts a charter and a local banner. “A distant oath binds a nearer defense.” | Contract/banners; staffed province-state rule. Literal immunity stays open Q4. |
| HUS-09 Camels & Caravan Animals | D / 1200 | HUS-07; **Seen: Camels or Llamas** | `reach(caravan_desert_mult:3)`; enables pack-animal caravan jobs only with available animals/crew. | herd | A loaded camel crosses a dry pass. “The caravan crosses ground carts cannot.” | Caravan pack animals; Seen gate preserved. |
| MED-05 Medical Texts | M / 1200 | MED-04, COM-07 | Eligible MED effect magnitudes ×1.15 (no cascade); unlock Physician role. | heal | A physician copies a treatment onto a tablet. “Practice becomes a text others can learn.” | Physician / medical texts; staffed health effects. |
| MED-06 Baths & Latrines | S / 1440 | MED-05, BLD-10 | `build(bathhouse,latrines)`; local density-disease pressure ×0.75. | heal | Water runs past bathers into covered drains. “Clean water enters; waste leaves.” | Bathhouse/drainage; sanitation and health. |
| SOC-10 Citizenship | I / 1800 | SOC-09, GOV-06 | Distance contribution to Cohesion decays at ×0.5 rate; other strain remains. | socialize | A newcomer receives a civic token. “Belonging reaches beyond the household.” | Civic identity marker; distance-cohesion rule. |
| TER-08 Provinces & Governors | I / 1800 | TER-07, GOV-07 | Governor role; active staffed connected province Overreach −30%; stable province assignment each season. | mark | A governor receives a seal beside a boundary map. “The province has a seat and a steward.” | Province map / governor slot; state system. |
| TER-09 Frontier Colonies | I / 1800 | TER-08, SEA-06 | `rule(planned_colony)`; one founding band may establish a colony up to 10 sea tiles from a known owned coast, only at unclaimed habitable land with a safe landing, ship, 2-season rations, and normal founding costs; no teleportation or extra population. | scout | A family boards a ship with seed and tools. “A new settlement is planned beyond the horizon.” | Colony planning; settlement/fission and transport. |

### 5.3 Reach — 24 regular projects

| ID / name | Type / cost | Requirements / gates | Proposed typed effects (P unless stated) | Practice | Moment / flavor | Visible change / sim |
|---|---|---|---|---|---|---|
| NUM-07 Geometry & Surveying | M / 1200 | NUM-06 | `mod(infrastructure_efficiency,mult:1.15)` (equiv labor ×0.869565 for surveyed fields, roads, aqueducts only). | count_stores | Surveyors stretch a cord between marked points. “A line measured on the ground becomes a reliable plan.” | Survey grid; infrastructure layout/labor. |
| COM-07 Alphabet | M / 1200 | COM-06 | Scribe learning/output ×2; Commons Insight ×1.05. | scribe | A learner writes a sound with one letter. “A smaller script carries more speech.” | Alphabet / scribes; literacy. |
| COM-08 Courier Relays | I / 1800 | COM-07, TRN-07 | Overreach −20%; adoption spread along maintained roads ×2. | scribe | A courier changes horses at a road station. “A message arrives before a traveler.” | Road relay; state-distance/adoption. |
| DEF-06 Towers & Gatehouses | S / 1440 | DEF-05, MEC-04 | Build towers/gatehouses on walls; wall multiplier 3.0→4.5; friendly trade allowed at controlled open gate in wartime. | build | A watchman signals from a new tower. “The gate can close without closing the market.” | Tower/gatehouse; defense and route rule. |
| DEF-07 Frontier Walls | S / 1440 | DEF-06, TER-08 | Build border segments; flips ×0.5 and raids ×0.3 only across a segment. | build | Masonry joins two boundary markers. “A border becomes a line that can be held.” | Wall segment; local territory/raid modifiers. |
| DEF-08 Castle Building | S / 1440 | DEF-06, BLD-09 | `build(castle)`; radius 5 strong claim anchor under existing rules; garrison required; KEY-5 prerequisite. | build | A keep rises above the river crossing. “A fortified seat holds the surrounding road.” | Castle and garrison/claim; defense/territory. |
| DIP-06 Tributary States | I / 1800 | DIP-05, WAR-07 | `diplo(vassalage)`; tribute/land scoring transfer up to 25% Dominion contribution from subject, conserved (no score duplication). | meet_strangers | Envoys exchange tribute and hostages at a border. “A weaker ruler keeps a throne under terms.” | Tributary treaty/status; diplomacy and score. |
| DIP-07 Leagues & Confederations | I / 1800 | DIP-05, GOV-08 | `diplo(league)`; multi-civ alliance and shared Wonder work pool only by explicit contributions; no shared ownership/duplicate Wonder. | meet_strangers | Several leaders pledge a common expedition. “Separate banners agree on one undertaking.” | League relation / shared Wonder ledger. |
| INQ-04 Natural Philosophy | I / 1800 | INQ-03, BEL-08, NUM-07 | Scholar Insight ×1.20. | scout | A scholar measures a falling weight beside a teacher. “Nature can be questioned and measured.” | Scholar insight; research. |
| INQ-05 Mechanics & Hydrostatics | M / 1200 | INQ-04, MEC-05 | MEC/NRG costs ×0.90; ship cargo capacity ×1.10. | scout | A vessel displaces water in a marked basin. “A measured principle improves a machine.” | Buoyancy/machine-cost readout; research and ships. |
| LRN-05 Academies | S / 1440 | LRN-04, BEL-08 | Build Academy; unlock Scholar role, current registry rate 48 Insight/year/person, staffing 1 per academy. | teach | Students gather in a purpose-built hall. “Learning has a place and a teacher.” | Academy and staffed scholars. |
| LRN-06 Great Library | W / 2400 | LRN-05, COM-06 | Wonder Great Library; Insight ×1.20; diffusion-discount contribution doubles, capped at existing floor 0.5 (no negative cost). | teach | Scrolls from many cities fill a new hall. “Knowledge gathered becomes knowledge shared.” | Wonder progress; insight/diffusion. |
| SEA-07 Harbors | S / 1440 | SEA-06, BLD-09 | Build Harbor; ship capacity ×2; port-city cap support ×1.20 conditional on food/water/housing; satisfies TRD-07 OR. | sail | Stone piers reach into a sheltered bay. “The harbor makes a coast a meeting place.” | Harbor/ship routes/port city; construction. |
| SEA-08 Great Lighthouse | W / 2400 | SEA-07, BLD-11 | Wonder Lighthouse; shipwreck risk ×0.5 civ-wide for known, maintained routes; sea trade throughput ×1.20; landmark claim uses existing wonder rules. | sail | A beacon burns above a harbor mouth. “A fixed light marks the coast at night.” | Lighthouse progress and sea-route safety. |
| SEA-09 War Galleys | M / 1200 | SEA-07, WAR-07 | `unlock_action(naval_combat,blockade)`; ship/crew combat and blockade require WAR; blockade requirements in §4. | sail | A galley crew rows in formation past the harbor chain. “The harbor is no longer beyond an army's reach.” | War galley / harbor blockade; naval system. |
| TRD-06 Coinage | M / 1200 | TRD-05, MET-07 | Trade friction ×0.60; tax/coin admin span +1 **P**; no currency stock/grant. | trade | A stamped coin is weighed at the market. “A shared mark settles an exchange.” | Coin/market accounting; trade/admin. |
| TRD-07 Long-Distance Trade Routes | I / 1800 | TRD-06; `requires_any: [HUS-09, SEA-07]` | Route range ×3 for caravan or harbor-to-harbor mode; only available mode is active; no free cargo/route or import. | trade | A caravan and ship exchange cargo at a distant port. “A chain of routes joins far markets.” | Extended routes; route pathfinding. Preserve OR exactly. |
| TRN-07 Paved Roads | S / 1440 | TRN-05, STN-11 | Build Road; move ×1.5 on built road; adoption ×2 along road; claim strength +10% along road **P**. | haul | Stone paving reaches beyond the town gate. “A road keeps its promise in every season.” | Road; movement, claim and adoption. |
| TRN-08 Stone Bridges | S / 1440 | TRN-07, BLD-09 | Build Bridge; road may cross river where bridge physically built; no general river movement bonus. | haul | A road crosses the river on stone arches. “The two banks share one road.” | Bridge; road routing. |
| WAR-07 Professional Armies | I / 1800 | WAR-05, GOV-07 | Soldier role; professional training replaces ordinary multiplier with ×1.5; base soldier ceiling 10% adults; garrison claims require staffing/supply. | guard | Recruits drill in a common formation. “Practice makes a standing force.” | Soldier role / drill; combat and population labor. |
| WAR-08 Iron Weapons | M / 1200 | WAR-07, MET-09 | Unlock G-ARMS3; soldier capacity ceiling rises from 10% to 20% adults; iron weapon strength 2.4; MET-10 steel edge raises it once to 3.0 (×1.25). | fight | An iron sword is issued beside a ration. “More people can be equipped if the state can supply them.” | Iron arms; unit equipment/capacity. |
| WAR-09 Siege Engines | M / 1200 | WAR-07, MEC-05 | Unlock supplied engine; siege duration intention ×0.40 only in matched engine assault; engine, crew, transport, supply and time required. | guard | A counterweighted arm throws a stone against a wall. “A wall now has a measured weakness.” | Siege engine; assault/investment distinction. |
| WAY-06 Periplus & Itineraries | M / 1200 | WAY-05, COM-07 | Trade-route efficiency ×1.25; route guides require known path/ports. | scout | A pilot marks bearings in a written itinerary. “A coast can be followed by name and measure.” | Route guide; efficiency. |
| WAY-07 Grid Cartography | M / 1200 | WAY-06, NUM-07, LRN-05 | `reveal(coastline_outlines,known_world)`; beacon range +2 tiles **P**. Reveal only known-world coast, not unexplored interior. | scout | A coastline is drawn onto a gridded chart. “The map gives the shore a measured form.” | Coastline reveal / beacon range. |

### 5.4 KEY-5 Feudal Order

| ID / name | Type / cost | Requirements / breadth | Proposed effects (P) | Practice | Moment / flavor | Visible change / sim |
|---|---|---|---|---|---|---|
| KEY-5 Feudal Order | K / 3000 | era 4; `requires: [AGR-10, GOV-09, BEL-09, DEF-08]`; breadth `{era:4,min:21}` | Leaves E4, enters E5; normal keystone event/score only. No free soldiers, castles, population, or E5 systems. Keep representation behavior from explicit match rules; do not scale simulation from `rep_scale`. | council | A plough, charter, altar and castle banner are raised together. “Countryside, lordship, faith and defense hold one realm.” | Keystone transition and era state; separate IronChapter mode boundary (25 §3). |

## 6. Full E4 catalog gates and prerequisite integrity

This checklist is intentionally redundant with §5 so a YAML transcription can compare every entry. `—` means no gate. `Seen` and `Near` spellings/targets are the current source values.

| ID | Type | `requires` (all-of) | `requires_any` | Gates |
|---|---|---|---|---|
| NUM-07 | M | NUM-06 | — | — |
| BLD-09 | M | BLD-07, CER-08 | — | — |
| BLD-10 | S | BLD-09, NUM-07 | — | — |
| BLD-11 | M | BLD-09, CHM-03 | — | — |
| CER-07 | M | CER-06 | — | — |
| CER-08 | M | CER-04, FIR-07 | — | — |
| MEC-04 | M | MEC-03, FIB-05 | — | — |
| MEC-05 | M | MEC-04, NUM-07 | — | — |
| MEC-06 | S | MEC-05 | — | — |
| MET-09 | M | MET-08 | — | — |
| MET-10 | M | MET-09 | — | — |
| MIN-07 | M | MIN-05, MEC-05 | — | — |
| NRG-03 | S | NRG-02, MEC-05 | — | Near: River |
| STN-12 | M | STN-11, MET-09 | — | — |
| WOD-07 | M | WOD-05, MET-09, FOD-05 | — | — |
| WOD-08 | M | WOD-07, MET-10 | — | — |
| AGR-08 | S | AGR-06, STN-11 | — | — |
| AGR-09 | M | AGR-05, MET-09 | — | — |
| AGR-10 | M | AGR-09, NRG-02 | — | — |
| ART-08 | S | ART-07, BLD-09 | — | — |
| BEL-08 | I | BEL-07, COM-07 | — | — |
| BEL-09 | I | BEL-08, TRD-07 | — | — |
| FOD-09 | I | FOD-07, GOV-07 | — | — |
| FOR-08 | M | FOR-07, MET-09 | — | — |
| GOV-07 | I | GOV-06, NUM-05 | — | — |
| GOV-08 | I | GOV-06, SOC-10 | — | — |
| GOV-09 | I | GOV-07, WAR-07 | — | — |
| HUS-09 | D | HUS-07 | — | Seen: Camels or Llamas |
| MED-05 | M | MED-04, COM-07 | — | — |
| MED-06 | S | MED-05, BLD-10 | — | — |
| SOC-10 | I | SOC-09, GOV-06 | — | — |
| TER-08 | I | TER-07, GOV-07 | — | — |
| TER-09 | I | TER-08, SEA-06 | — | — |
| COM-07 | M | COM-06 | — | — |
| COM-08 | I | COM-07, TRN-07 | — | — |
| DEF-06 | S | DEF-05, MEC-04 | — | — |
| DEF-07 | S | DEF-06, TER-08 | — | — |
| DEF-08 | S | DEF-06, BLD-09 | — | — |
| DIP-06 | I | DIP-05, WAR-07 | — | — |
| DIP-07 | I | DIP-05, GOV-08 | — | — |
| INQ-04 | I | INQ-03, BEL-08, NUM-07 | — | — |
| INQ-05 | M | INQ-04, MEC-05 | — | — |
| LRN-05 | S | LRN-04, BEL-08 | — | — |
| LRN-06 | W | LRN-05, COM-06 | — | — |
| SEA-07 | S | SEA-06, BLD-09 | — | — |
| SEA-08 | W | SEA-07, BLD-11 | — | — |
| SEA-09 | M | SEA-07, WAR-07 | — | — |
| TRD-06 | M | TRD-05, MET-07 | — | — |
| TRD-07 | I | TRD-06 | HUS-09 OR SEA-07 | — |
| TRN-07 | S | TRN-05, STN-11 | — | — |
| TRN-08 | S | TRN-07, BLD-09 | — | — |
| WAR-07 | I | WAR-05, GOV-07 | — | — |
| WAR-08 | M | WAR-07, MET-09 | — | — |
| WAR-09 | M | WAR-07, MEC-05 | — | — |
| WAY-06 | M | WAY-05, COM-07 | — | — |
| WAY-07 | M | WAY-06, NUM-07, LRN-05 | — | — |
| KEY-5 | K | AGR-10, GOV-09, BEL-09, DEF-08 | — | breadth 21 regular E4; no new hard gate |

The HUS-09 Seen gate is an optional route blocker, not a KEY-5 critical gate: preserve SEA-07 as TRD-07's alternative. Research and the 21-completion threshold must remain achievable without camels/llamas. NRG-03's Near: River blocks local use/build only, not research. Existing opening/critical paths must be recalculated with the OR edge and gates before promotion; historic closure count 153 in 04 is not proof of current reachability/time.

## 7. Required behavior and simulation work

This is an integration checklist for a future approved implementation, not a code authorization. It preserves the boundaries and proposed B4 series in 25 §2/§14.

| Capability | Required behavior for these effects | Minimum verification evidence before promotion |
|---|---|---|
| Research and match rules | E4 remains Hidden/inert in BronzeChapter and before a civ's own KEY-4 in IronChapter. E4 effects activate only per-civ after own KEY-4 and local conditions. KEY-5 exits chapter; no E5 systems run. | Old Bronze snapshots/goldens and default checksums unchanged; Iron mode replay/save cases; B4-01/04. |
| Metal/material ledger | Append goods/recipes/tiers without mutating E3 bloomery. Track bloom→wrought→steel, charcoal, wood, tools, arms and trade transfers; upgrade/capture cannot duplicate inputs. | Per-batch conservation, local/imported supply, low-wood, mixed-kit and exhaustion traces; B4-06. |
| Tool action selection | Per-figure kit tier determines performance once; T12 replaces T11; saw is a T12 attachment; wear and fallback continue deterministically. | T11/T12 deterministic fixtures and live cohorts; embodied-fuel check reproduced; no false dormancy/pass claim. |
| Food, city capacity and health | Farm actions/plots create food through labor; aqueduct water and public grain are separate; donor reserve persists; health applies by cause. | Resource conservation, city cap/disease exposure reporting; B4-07. |
| Construction | Builds require valid site, materials, labor, local capacity, and optionally staff/upkeep. Queue completion is distinct from effect. | Construction ledgers, orphaned/unfinished jobs, captured structure, no free build; B4-08/09. |
| Trade and routes | Route length/capacity, road relays, barrels, coinage, faith and imports require physical route/cargo/crew. `requires_any` survives schema/compiler and runtime. | Land route with HUS route, sea route with SEA route, no-camel/no-coast population, blockaded route and loss cases. |
| Governance and diplomacy | Administrators/governors staff connected jurisdiction; tributary scores conserve totals; league contributions are explicit; contracts do not remove famine/conquest secession. | Province connection/staff stress, captured governor, treaty/score conservation; B4-09. |
| Military / siege / sea | Soldiers use slots, equipment, food, training and garrison; wall assault needs supplied engine and crew; blockade is bounded to one known port. | Matched investment/engine, relief, capture, blockade, crew/supply and retreat replays; B4-08. |
| Research AI/forecast | `ForecastCatalog` currently marks projects numeric directly from content and fills future-era planning arrays (`ForecastCatalog.cs:135`). Before numeric E4 promotion, add mode-aware forecast filtering or an explicit promotion-order gate so forecast cannot plan E4 while `ResearchSystem` still hides it. Preserve Bronze-mode forecasts after KEY-4. Add E4 KEY-5 closure/ETA and gate alternatives separately from KEY-4 fields; do not retarget M10 branches. Era-indexed reach/claim lookups already consume E4 registry rows after KEY-4; preserve those values and do not globally clamp or reinterpret them. | Tests for forecast/system visibility agreement across BronzeChapter and IronChapter, after KEY-4 and KEY-5; uncensored/censored exposures, alternative route closure, B4-01/04/11/12. |
| Scale / UI / persistence | Preserve existing match mode defaults, save/hash/replay compatibility and display-only choices; show new goods, local gates, structures and requirements. | B4-01/03/04/14/15, legacy bronze state. |

## 8. Hand checks and warnings

1. **Counts:** 56 regular (15 + 17 + 24) and one KEY-5. Brief's 59 remains unreconciled (Q1).
2. **Costs (recounted against the 56 rows):** Craft has 12 M + 3 S = 18,720; Kin has 4 M + 3 S + 9 I + 1 D = 26,520; Reach has 9 M + 7 S + 6 I + 2 W = 36,480. Totals are M 25, S 13, I 15, D 1, W 2 and **81,720 regular list-cost**; KEY-5 is 3,000. This is simple catalog arithmetic only, not a closure or timing result.
3. **T11 embodied fuel:** the complete ore-to-kit chain is estimated at 0.75 ore and 8.2125 wood via 1.36875 charcoal with about 1.1975 ws, excluding mining/haul. The substantial fuel burden is a warning requiring oracle/live fit, not a pass.
4. **No performance conclusions:** no target cadence, yield, annual Insight, unit balance, city population, route coverage, map fill, score timing, performance, or AI quality is measured by this document.
5. **KEY-5 route:** source requires four named projects plus 21 E4 completions. Candidate routes in 25 and current path graph should be recomputed with OR/gate semantics, but no threshold is changed.
6. **Potential remaining ambiguity:** barrel return accounting, E4 Wonder construction costs, diplomacy/league implementation, exact provincial service radius, and sea-combat scope need explicit system design before YAML/code promotion. Bounds above are proposals, not silently closed decisions.

## 9. Open decisions preserved from 25

This spec proposes numbers to make review concrete; it does not resolve these questions:

- **Q1:** 56 regular + KEY-5 vs 59-project brief.
- **Q2/Q3:** IronChapter activation/end rules and 1:1 simulation vs display-only scale. This spec does not enable a mode or representation conversion.
- **Q4:** Feudal contract scope; proposed distance/span relief keeps famine, conquest and crisis secession possible.
- **Q5:** Tributary Dominion/tribute flow and scoring conservation.
- **Q6:** Full naval blockade, league co-building and missionary influence scope.
- **Q7:** Great Dome buildable in E4; this draft unlocks only and defers build.
- **Q8:** Approve P values, B4 protocols and performance budget only after oracle and live fit.
- **Q9:** Whether existing iron geography supports all starts; no worldgen edit proposed.

None of these open decisions alters current E0–E3 YAML, acceptance thresholds, E3 bloomery, existing default match behavior, tests or goldens. No implementation, validation, or balance claim is authorized by this design draft.
