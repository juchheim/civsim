# 27 — Faith & Fief (E5): Mills, Monasteries, Cannon and the Closing of the Old World

> **Planning draft, 2026-11-05; updated 2026-11-13 after the Iron closeout merged. Confirmed by the user: D1 (E5 as a default-off FiefChapter including E4), D4 (Fief-only claim upkeep), and on 2026-11-13 D8 (E5 aims for **25 minutes** from KEY-5 to KEY-6, the Iron target, not Iron's measured 20), D9 (E5 late projects cost double, as in E0–E4), D10 (the Thriving Legacy score applies) and D11 (no rival countdown). WP-E5-5's first-pass systems A, B and C are implemented across [the power/furnace](balance/m12-wp8-power-furnace.md), [rotation](balance/m12-wp8a-land-rotation.md), [horse-collar](balance/m12-wp8b-horse-collar.md) and [land-systems](balance/m12-wp8c-land-systems.md) slices. F-T2's shared power-hour ledger, the full F-T4 oracle and multi-seed match balance remain open; all E5 values remain unfitted and draft.** It is the E5 counterpart of [25 — Iron Age](25-iron-age.md): scope, audit, mechanisms, acceptance gates and a work breakdown. It was written while the Iron chapter (E4) is still being balanced by another session, so it is built to **start without touching any file that balance work is changing** (§9) and to **wait for E4 sign-off before anything numeric is promoted**.
>
> Audit baseline: `main` at `6d6df44` (since then `43a1648` made the Iron chapter the game's default for new matches; the sim default in `ChapterRules` is unchanged, and Fief stays opt-in on top of it). Every claim marked *(verified)* was read from the catalog, registries or source on that commit. Claims marked *(assumed)* come from older design docs or from the E4 notes and must be re-checked after the E4 balance lands.

## 1. Which age is next, and why

The Iron chapter ends at the player's own **KEY-5 Feudal Order** ([25 §3](25-iron-age.md)). KEY-5 *enters* **E5 Faith & Fief** (the Medieval era, [01 §8](01-game-structure.md)), which is therefore the next age, and **KEY-6 Printing Revolution** is the keystone that leaves it.

| Check | Finding |
|---|---|
| Newest completed age | E4 Iron: all 57 nodes promoted to `numeric` ([m11-wp11](balance/m11-wp11-whole-match.md)); under whole-match balancing by another session. |
| Is any E5 content live? | No. `ChapterRules` has two modes, Bronze (ceiling 3) and Iron (ceiling 4) *(verified, `src/CivSim.Sim/Research/ChapterRules.cs`)*. In Iron mode E5 is Hidden. |
| E5 catalog state | 49 regular projects plus KEY-6, all `status: draft`, with **prose-only effects** (`kind: prose`) and no costs, practice, flavor or Moments *(verified, e.g. MET-12, BLD-12)*. E4 rows were typed the same way in its WP3. |
| Era row | `content/registries/eras.yaml` E5: base cost 6,000, breadth 16, representation 1:5, claim radius 5, reach 25, child/adult/elder mortality 0.029 / 0.0105 / 0.051 *(verified)*. |

The working name for the new match rules is **FiefChapter** (E4 and E5 both play; KEY-6 ends the match). Milestone name: **M12**.

## 2. What Faith & Fief should feel like

Iron spread tools and gave the state reach. Faith & Fief is where the countryside, the church and the town **start to pull against each other**, and where machines and gunpowder arrive. The player still sets three research slots and watches planners carry out the result.

| Tension | What pushes | What answers it |
|---|---|---|
| **Land against coin** | Manors, three-field farming, heavy cavalry and castles bind power to land. Guilds, banking, charters and parliaments bind it to towns and money. | Manorial estates cheapen rural claims; charters and banking grow cities and fund Wonders. Each has a real upkeep. |
| **Faith as an institution** | Monasteries preserve and farm, pilgrims move trade, a cathedral lifts the whole realm, hospitals and courts take over duties the state did alone. | Monks and scholars are real adults taken off other work. An empty monastery produces nothing (the lesson of E4's idle academies). |
| **Wheel, wind and furnace** | Mills turn rivers and wind into work; the blast furnace multiplies iron ×4. | Mills need a site and a gear train. The blast furnace is **a charcoal hog**: E5 is the second, harder fuel crunch (§5.2). |
| **Wall against powder** | Concentric castles double a castle's defense; gunpowder makes walls 70% less effective, but needs saltpeter and a cast-iron foundry. | Whoever has the powder chain breaks walls; whoever lacks it must keep investment, relief or peace as options. |

Also in this era: epidemics bite harder as towns grow (plague weight rises, [09 §10.2](09-population-and-health.md)), and open-sea ships appear (cogs, lateen sails, compass), but **the oceans open only at KEY-6** (catalog text), so E5 prepares sea power rather than playing it out.

## 3. Scope audit

### 3.1 Inventory *(verified from `content/catalog`)*

**49 regular projects (Craft 18, Kin 13, Reach 18) + KEY-6 = 50 nodes.** This matches [03 §4.1](03-research-catalog.md) exactly, so unlike E4 there is **no count discrepancy to resolve**. The 49 fall naturally into seven systems; each system becomes one work package (§9).

| System | Projects (id: intent in a few words) |
|---|---|
| **A. Power and machines** (7) | MEC-07 crank & camshaft (mill-driven crafts ×2) · NRG-04 horse collar (draft ×2) · NRG-05 windmill (milling without rivers, drainage) · WOD-09 water sawmill (saw mill ×4) · FIB-10 treadle loom (×2) · FIB-11 spinning wheel (×3) · MEC-08 mechanical clock (work +5%, clock tower) |
| **B. Iron, fire, powder, glass** (8) | MET-11 crucible steel (arms +20%, luxury blades) · MET-12 blast furnace & cast iron (iron ×4, cannon, parts) · FIR-10 chimneys (winter deaths −40%) · CHM-06 distillation · CHM-07 gunpowder (**needs Saltpeter**) · CER-09 porcelain (luxury export) · CER-10 lens grinding (spectacles, Elder Insight +25%) · MIN-08 mine surveying (reveal veins, yield +25%) |
| **C. Land and food** (5) | AGR-11 three-field rotation (land productivity +50%) · HUS-10 selective breeding · HNT-11 game reserves & falconry · TER-10 manorial estates (farm-tile claim upkeep −30%) · WOD-10 timber framing (building size +50%) |
| **D. Faith, learning, craft of the mind** (14) | BEL-10 monasticism · BEL-11 pilgrimage routes · ART-09 illuminated manuscripts · LRN-07 cathedral schools · LRN-08 universities · BLD-12 **Gothic Cathedral (Wonder)** · STN-13 master masons · COM-09 paper · COM-10 movable type · NUM-08 zero & positional numerals · NUM-09 algebra · INQ-06 optics & magnetism · MED-07 hospitals · MED-08 quarantine |
| **E. Law, money, guilds** (6) | GOV-10 canon & common law · GOV-11 charters & parliaments · SOC-11 guilds · TRD-08 banking · TRD-09 double-entry bookkeeping · DIP-08 dynastic marriages |
| **F. War** (5) | DEF-09 concentric castles · WAR-10 heavy cavalry · WAR-11 crossbows & longbows · WAR-12 gunpowder weapons · TRN-09 stirrup & horseshoe |
| **G. Sea and maps** (4) | SEA-10 keeled ships & cogs · SEA-11 lateen sail & sternpost rudder · WAY-08 magnetic compass · WAY-09 portolan charts & astrolabe |

### 3.2 KEY-6 closure *(verified by hand from `requires`)*

KEY-6 requires **COM-10, TRD-09, WAY-08, LRN-08** and **16 completed E5 projects**. The in-era critical path is exactly the 12 projects COM-09, COM-10, NUM-08, NUM-09, TRD-08, TRD-09, INQ-06, WAY-08, BEL-10, GOV-10, LRN-07, LRN-08, which matches [04 §2](04-research-paths.md) (12 critical, 4 free picks).

Two findings that shape the design:

1. **No Craft project is on the path.** All 12 are Reach or Kin. The 18 Craft projects (mills, blast furnace, looms, powder) are optional, so natural play will reach them only through the Council's leaning and free picks, **and E4's whole-match run showed that optional systems barely run when research finishes in the last minutes** ([m11-wp11](balance/m11-wp11-whole-match.md)). E5 therefore needs the same two answers E4 got: a `--grant-e5` harness mode to exercise systems when research is not the bottleneck, and pacing that gives them time to matter.
2. **Closure leaves E3/E4 alone.** COM-10 needs MEC-05 and MET-06, WAY-08 needs WAY-07, LRN-08 needs LRN-05/GOV-10, so a civilization that skipped Roman-style learning in E4 still has an EraLag-discounted route, but the *land* route through HUS and the *sea* route through SEA-07 must both stay viable (the E4 rule that geography cannot soft-lock a keystone applies again).

### 3.3 Code ceilings *(verified unless noted)*

| Boundary | State on `6d6df44` | Consequence for E5 |
|---|---|---|
| Chapter modes | `ChapterMode` = Bronze 0, Iron 1 | Add `Fief = 2`, default off, append-only. |
| Research ceiling | `ChapterRules.LastResearchEra` is 3 (Bronze) or 4 (Iron) | Fief carries 5. Hidden above it stays the rule. |
| Economy ceiling | `EconomyMaxEra` returns 3 **in both modes**; Iron is delivered through an `IronChain` flag and a `ProjectCeiling = MaxEra + 1` shim *(WP5 audit; re-verify)* | **Do not add a second flag.** Era-4 and era-5 demography, fission and reach rows need an explicit per-chapter economy profile. This is a shared table the E4 balancer may also be editing, so it is decided jointly (§9, D3). |
| Forecast / Spark | `ForecastResult` has `Key1..Key5`; `Key5` is eligible only inside an Iron ceiling | Add `Key6` and a KEY-6 closure policy as new fields, not by reinterpreting Key5. |
| Replay / save / hash | Iron uses replay format 2, save version 5, extra hash and snapshot trailer; Bronze bytes unchanged | Fief appends the next versions; Bronze and Iron bytes must stay exact. |
| Frame / view | `IronFrame` is empty unless Iron | Add a `FiefFrame` block under the same rule; arrays only grow. |
| Generic visual kit | [08 §13](08-ui-and-world-display.md): bespoke Moments only for E0–E4 | E5 uses the generic kit (badge burst, tint, modular building kit), see §8. |

## 4. What E4 leaves for E5 to build on

| E5 project | Builds on (E4 system) | What to watch |
|---|---|---|
| DEF-09 concentric castles | DEF-08 castle + garrison, WAR-09 engine train | Castles sit uncrewed in natural E4 play; ×2 defense must require the garrison. |
| WAR-12 gunpowder weapons | WAR-09 engines, wall integrity (1000 = intact) | Cannon is a new breaching tool on the same integrity track, not a second siege system. |
| MET-12 blast furnace | MET-10 steel, forge chain, I-T6 fuel accounting | The I-T6 fuel screen is the template; ×4 iron must be paid for in charcoal. |
| BEL-10 monasticism | BEL-09 missionaries, LRN-05 academies | Academies rarely got a scholar in E4; monasteries must not repeat that. |
| BLD-12 Gothic Cathedral | Wonder mechanism (Library, Lighthouse, Dome) | **No Wonder was finished in natural E4 play**: stone and wood in a town's range ran out. |
| MED-07 / MED-08 | MED-06 baths, outbreak model | Plague weight is 0.1 at E4, 0.15 at E6; hospital −20%, quarantine ×0.75 roll. |
| TER-10 manorial estates, GOV-10/11 | Provinces, governors, feudal contract (distance strain only) | The catalog's "claim upkeep" has **no counterpart in the sim today** *(verified: no `ClaimUpkeep` in `src/CivSim.Sim`)*; it needs a definition (§5.3). |
| SEA-10/11, WAY-08/09 | Harbors, galleys, blockade | Open-sea routes are new; island-crossing rules are in [21](21-island-crossing.md). |
| NRG-05, WOD-09, FIB-10/11, MEC-07 | E4 water mill | The water mill now has a grinding model on main ([29](29-dead-unlocks.md): `MillGrainValue` 0.1, `MillPeople` 200, `MillLaborMult` 0.5, all provisional). The E5 power ledger (§5.1) must extend those constants, not replace them. |

## 5. New mechanisms (proposed bounded interpretations)

Each is a hypothesis for the E5 oracle to fit, in the same style as 25/26. "Physical" means it spends or moves ledgered goods, labor or sites; nothing is a bare multiplier.

### 5.1 Mechanical power ledger (system A)
A mill is a **site plus a power source plus a job**. Sources: water wheel (river tile, E4), windmill (open tile, NRG-05), horse gin (animals, NRG-04). Each source yields *power hours* per season; jobs (grinding, sawing, hammering, fulling, bellows) consume power hours and multiply the labor they replace. Treadle loom and spinning wheel are hand-powered kits that raise textile output per worker, not mills. Mills in the same town share a power budget so ×2 and ×4 effects cannot stack without sources. This also fixes the E4 water-mill gap (a grinding job to multiply). The clock tower is a civic structure with a small Cohesion effect, not a global +5% multiplier on everything. The first-pass implementation selects one available water or wind source and applies its factor to modeled jobs; it does not yet allocate a shared power-hour budget. F-T2 remains open until that ledger and the grain-coverage measure are implemented.

NRG-04 also improves direct draft work. In Fief after KEY-5, it halves the labor for the share of active field strips ploughed by tame horses; each horse has twice an NRG-02 ox pair's field capacity. Existing ox teams are credited first, so the same field labor is not saved twice. Where the economy already charges a distance-based haul component, a horse-holding town pays half that component. Both effects require a horse in that settlement's existing herd state; the haul factor is a provisional binary capacity rule pending the land oracle. See [m12-wp8b-horse-collar](balance/m12-wp8b-horse-collar.md).

### 5.2 Blast furnace and the second fuel crunch (system B)
The WP-E5-5 first pass makes a mill-driven blast-furnace batch yield ×4 G-IRON from ×4 ore, ×2 charcoal and ×2 labor, so it halves charcoal and labor per litre. The first draft's separate pig-iron good was dropped; cast-iron goods and cannon-body recipes need their own goods slots and are deferred to WP-E5-8. No coal exists until E6 (MIN-09), so the furnace competes with houses and ships for forest. The I-T6 protocol (full-chain work share, clearing crossings, unmet planned charcoal) is reused as **F-T3**. Crucible steel (MET-11) is a luxury sub-chain (clay crucibles from CER, trade goods), not a bulk upgrade.

### 5.3 Land, estates and food (system C)
- **Three-field rotation (+50% land productivity):** Fief's post-KEY-5 farm uses a two-field baseline, with half the strips cropped each year. AGR-11 changes the deterministic field schedule to two of every three strips and adds a 1.125 legume yield, so `(2/3 × 1.125) / 1/2 = 1.5`; fallow strips recover soil. The schedule is derived from field order, year and completed research, not a free multiplier or saved state. The first implementation is reported in [m12-wp8a-land-rotation](balance/m12-wp8a-land-rotation.md); its balance evidence is one whole-match seed only.
- **Manorial estates:** D4 defines Fief-only upkeep as 10% of realized farm yield from owned field tiles; TER-10 cuts the rate to 7%. Grain moves to the nearest staffed same-civilization town or city. Work-capable adults stand in for non-farming staff because the economy pools occupations and stores by settlement.
- **Selective breeding, game reserves and timber framing** use existing herd, ownership, cohesion and hall-capacity primitives. Their provisional rules and limits are in [m12-wp8c-land-systems](balance/m12-wp8c-land-systems.md).

### 5.4 Faith, learning and knowledge (system D)
- **Monasteries (BEL-10)** are real structures staffed by monks (adults off other work). Insight +16 each is paid only while staffed. "Knowledge never lost" has no counterpart in the sim today *(verified: no knowledge-loss rule)*. Proposal: a settlement capture or collapse can lose a **local** Know-how pool and a scholar; a monastery keeps a copy that restores it. This gives the sentence a real meaning without inventing global tech loss.
- **Universities, cathedral schools, paper, movable type, zero, algebra:** distinct Insight sources with the same staffing rule and the existing global diffusion-discount cap (a mechanism cannot reach zero cost). Movable type's "diffusion ×1.5" is subject to that cap.
- **Gothic Cathedral** is a normal Wonder (like the Great Dome in E4), with explicit stone, wood and master-mason labor. Because no E4 Wonder finished in natural play, the Wonder stone/wood problem is solved **once, before** adding a fifth Wonder (§9, WP-E5-0).
- **Hospitals and quarantine** are staffed structures and a route-level rule on the existing outbreak model; plague is not scripted.
- **Pilgrimage routes** are real trips along completed roads, with the trade bonus paid only on traveled routes.

### 5.5 Law, money and guilds (system E)
Banking and double-entry change **trade friction and Wonder funding** without minting money: credit moves existing goods earlier; it creates none. Guild "craft quality +20%" is bounded to tool service life and recipe waste, not output; apprentice speed ×1.5 is a training-time rule. Charters and parliaments raise city growth and admin capacity with the E4 rule that benefits stop when the staff or structure is absent. Dynastic marriages protect alliances across leader change and give a bounded chance of inheriting a Free Folk region; it must not transfer land instantly or by score rank.

### 5.6 War (system F)
Heavy cavalry, crossbow/longbow and gunpowder weapons extend the existing combat tables by fixed-ratio rows (the E3/E4 oracle approach, `combat_e3.py`, `e4_combat_oracle.py`). Heavy cavalry needs horses in the herd and the stirrup/horseshoe kit; crossbows and longbows need wood/iron and a trained levy; gunpowder needs the powder chain (saltpeter deposit, charcoal, sulfur, a foundry). Walls are still not assaulted without a breach: the cannon joins engines as a way to reduce integrity, with powder as a consumed good per shot. Concentric castles ×2 apply to a **garrisoned** castle only.

### 5.7 Sea and maps (system G)
Cogs, lateen sails and the compass raise cargo, speed and reduce losses on routes the harbors already open; portolan charts extend the E4 "observed coast only" visibility rule (no omniscience). Open-sea crossing is **prepared, not delivered**: KEY-6 text says "oceans open", so the new crossing rules arrive with E6, and E5 only reduces shipwreck losses and speeds routes that already work.

## 6. Match structure: the FiefChapter contract

| Mode / civilization state | Research | Economy / systems | Ending |
|---|---|---|---|
| Bronze, Iron | exactly as today | exactly as today | unchanged (Bronze KEY-4, Iron KEY-5) |
| Fief, before own KEY-4 | E4 and E5 Hidden | nothing past E3 | KEY-5 does **not** end this mode |
| Fief, after own KEY-4 | E4 opens as in Iron | Iron systems active | unchanged until a civilization has KEY-5 |
| Fief, after own KEY-5 | E5 opens (or glimpses) by prerequisite and gate | E5 systems eligible; each still needs its local inputs, site, staff | the human completing KEY-6; **no rival countdown and no year cap** (D11, see below) |
| Fief, after own KEY-6 | E6 Hidden | E5 economy continues, no E6 systems | as above |

- **Ending (D11, user 2026-11-13: "no countdown"):** main removed the year cap on 2026-11-08 ([28 §17](28-production-gap.md)) and Iron ends on the human's KEY-5 or 48 seasons after the first rival's. For Fief, a rival completing KEY-6 **starts no clock**; the chapter ends only when the human completes KEY-6. The user confirmed this reading on 2026-11-13. Headless and AI-only runs are bounded by their own `--minutes` horizon, exactly as an Iron run that nobody finishes is (28 §17).
- **Length (D8, user 2026-11-13: "aim for 25"):** E5 aims for **25 minutes from own KEY-5 to own KEY-6**, the Iron target of [28 §7a](28-production-gap.md). Iron itself measures about 20 on today's build, so this is a deliberate step up. The calibrated forward estimate in [m12-wp5](balance/m12-wp5-merge-reanchor.md) says E5 as typed (era base 6,000) lands near 19 minutes if every civilization researches the whole age, so **the E5 era base is a pending raise to about 9,500** (about 7,000 at 4% growth, 13,000 at 8%). It is a catalog input (`content/registries/eras.yaml`), left at 6,000 until the rows are promoted and a live E5 run can refit it; changing it now would move the content golden and the docs 02 and 07c cost tables for no behavior, because every E5 row is still draft.
- **Late projects (D9):** the 28 §7 and §16 rule (non-keystone projects with at least three projects in their own era's prerequisite chain cost twice the derived cost) applies to E5. By that definition only **7 of the 49** E5 projects qualify (BLD-12, GOV-11, LRN-08, SEA-11, TRD-09, WAR-12, WAY-09), two of them on the KEY-6 path, because most E5 prerequisites sit in E4. The doubling is therefore a small lever for E5; the era base is the real one.
- **Legacy (D10):** E5 uses main's current Legacy rules unchanged: Firsts pay nothing, Mastery 2 × (era + 1) per project, Wonder built 25 × (era + 1), and the Thriving score (√people ÷ 5 per season, less famine and disease). No E5-specific rescale.
- Dominion victory stays as it is (direct ownership, ≥50% and 2× runner-up for 5 years; tribute never counts as land), and early Dominion endings are reported separately from KEY-6 and cap endings.
- A Bronze or Iron save never becomes Fief implicitly; Fief writes the next replay, save and snapshot versions, and Bronze and Iron bytes and hashes stay identical.
- Representation: keep **the simulation at 1:1** as in E4's Q3. The catalog's 1:5 is applied only to the **view** if the figure budget (≤ 600 per civilization) is exceeded; the old model has 2,559 people per civilization at the end of E5, so this is a measurement the first `--grant-e5` runs must make.

## 7. Provisional targets (F-xx; none fitted, none approved)

| ID | Target / diagnostic | Measurement |
|---|---|---|
| F-T1 | Own KEY-5 → KEY-6 about **25 minutes** (D8), with the E5 era base (pending raise to about 9,500) and the late-project doubling as the levers; share of seats not finished by the run horizon reported | 80+ matches, held-out block, censored/captured seats shown |
| F-T2 | Mill coverage: ≥ 60% of grain milled by water or wind within 12 seasons of the power source existing in towns that have a site | Per-town power-hour ledger; no ×-stacking without a source |
| F-T3 | Blast furnace: iron ×4 only inside a measured charcoal budget; full-chain work share ≤ 15% in every mature 12-season window; zero blast-furnace-attributed forest-clearing crossings; fuel-short ≤ 5% | The I-T6 protocol reused on the same fixtures plus low-wood starts |
| F-T4 | Food: three-field gives +50% productivity on rotated tiles with the fallow cost shown; matched anchors rise inside the oracle cap | Physical mass balance, five geography fixtures |
| F-T5 | Gunpowder walls −70% only with powder, cannon and a built breach; concentric ×2 only when garrisoned; engines/investment still viable | Supplied isolated trials plus live relief/attrition |
| F-T6 | Every E5 institution (monastery, university, hospital, cathedral, clock tower, mill) shows staffed/working, queued, or dormant with a reason within a season | Blunder counter; "idle institution" rate near zero |
| F-T7 | Plague/pox deaths follow the 09 §10 shares; quarantine halves imports; no scripted event | Per-cause attribution by exposure from own KEY-5 |
| F-T8 | Map fill, Dominion and City share | Diagnostic only until re-anchored on the live E4 trajectory |

## 8. View layer

Build views last. E5 uses the **generic kit** of [08 §13](08-ui-and-world-display.md): badge burst for Moments, tool tint for method rungs, the modular era building kit with a type sign for structures, banner swaps for institutions. The visual read of E5 is manors and three-field strips, windmills on ridges, monasteries and a cathedral skyline, castle towers, cogs on sea lanes, and a few smoking blast furnaces. Cards needed: mill power by town, blast furnace and charcoal budget, monastery/university staffing, hospital and outbreak map, cannon and siege integrity, guild/bank/charter effect, KEY-6 countdown. The game text must say KEY-6 / Printing Revolution only in Fief mode.

## 9. Work breakdown and what can start now

The E4 balancer is changing Iron constants, fuel/economy code and goldens. E5 work is split so the first stage touches **only new files**:

| WP | Deliverable | Needs E4 sign-off? | Files |
|---|---|---|---|
| **WP-E5-0 Wonder and range fix** | **Done 2026-11-13** ([report](balance/m12-wp7-build-wood.md)): the shortage was distribution, not supply (forests 93% intact). Timber is now requested for Iron structures, felling reaches 8 tiles, and sister towns lend stone; Wonders finished go from 0 to 11 in 8 matches (11 of 12 in long ones). Iron-chapter behavior changed; Bronze and the goldens did not | Changes Iron behavior; the frozen B4-06 block was not rerun | `LivingEconomyModel.BuildWood.cs`, one line each in the planner, felling and sharing |
| WP-E5-1 Scope and oracle | **First pass done 2026-11-05** ([report](balance/m12-wp1-oracle-fit.md)): `e5_research_oracle.py`, `e5_power_oracle.py` (power and blast-furnace fuel), `e5_combat_oracle.py`, `e5_economy_oracle.py`. Re-run and re-anchor on the live E4 trajectory after each E4 rebase | Reads E4 results; **no** | new tool files only |
| WP-E5-2 Content typing | **Done 2026-11-05 on branch `codex/e5-fief`**: all 49 + KEY-6 rows typed in place (typed effects, practice, flavor, moment; every row stays `draft`), 16 goods, 11 structures, 4 roles, 2 actions, 2 practice rows and 14 recipes added, validator rule V22 (E5 promoted together, typed only), content-pack golden refreshed. Details in [m12-wp2-content-typing](balance/m12-wp2-content-typing.md) | No (draft rows are inert) | `content/catalog/*.yaml` E5 rows, registries |
| WP-E5-3 Geography audit | **Done 2026-11-05** ([report](balance/m12-wp3-e5-geography.md)): `e5-audit` over seeds 1–10,000. Saltpeter on own ground for 74.1% of starts (no start has it locally in 15.1% of worlds), rivers never limit, 12.6% of worlds have no wild horses; no world generation changed | No (read-only) | new harness command and report |
| WP-E5-4 Mode plumbing | **Done 2026-11-13** ([report](balance/m12-wp6-mode-plumbing.md)): `ChapterMode.Fief = 2`, ceiling 5, KEY-6 ends it with no rival countdown, mode byte in replays, snapshots and the research hash, `FiefChain` economy flag, `Key6` forecast field, `--mode fief` in the harness, 9 isolation tests. The power / furnace and field-rotation first slices now read the flag; the game does not offer the mode yet (WP-E5-10) | Small and additive, yes review | `ChapterRules`, `MatchEnd`, `MatchModel`, `MatchReplay`, `LivingEconomyModel.Fief.cs` |
| WP-E5-5 Power, food, fire | **First-pass mechanics implemented 2026-11-13** across the four WP8 reports: mills/furnace and textiles, AGR-11 rotation, NRG-04 collar, HUS-10 selective breeding, HNT-11 game reserves, TER-10 upkeep and WOD-10 framing. F-T3's 200-person low-wood fixture screen is ≤15% in all 8 mature seeds, with one no-kit seed censored and two low-demand seeds censored for fuel. F-T2 power-hours, full F-T4 geography/oracle, and wider Fief match balance remain open; E5 stays draft. | Yes (builds on the fuel chain) | `LivingEconomyModel.*` partials, harness, focused tests and reports |
| WP-E5-6 Faith and learning | System D | Yes (staffing rule from E4) | |
| WP-E5-7 Law and money | System E | Yes | |
| WP-E5-8 War and sea | Systems F, G | Yes | |
| WP-E5-9 AI and forecast | Spark KEY-6 closure, Strategist powder/cannon logic, Council, blunder telemetry | Yes | |
| WP-E5-10 View and game | `FiefFrame`, cards, text, generic kit | Last | |
| WP-E5-11 Promotion and whole-match | Promote all 50 rows together (no half-promoted age), `fief-match` and `--grant-e5` | Yes | |
| WP-E5-12 Large sample and playtest | 80+ matches, held-out cohort, human test | Yes, and only when the whole age works | |

**Safe to start today, in parallel with E4 balancing:** WP-E5-1, WP-E5-2 (on its own branch, see D6) and WP-E5-3. Everything else waits for the E4 balance sign-off.

## 10. Acceptance series B5-xx (all proposed)

| Gate | Condition |
|---|---|
| B5-01 Isolation | Toy, worldgen, research, politics, Bronze **and Iron** goldens and checksums byte-identical; E5 inert before own KEY-5 and throughout Bronze and Iron modes even with a numeric E5 pack |
| B5-02 Completeness | All 50 nodes typed, requirements/costs/practice/flavor/moment present (V22); strict validator clean; registry references resolve. **Typing done; promotion to numeric is still ahead** |
| B5-03 Determinism | 1,000 Fief matches replay exactly; snapshot continuation exact; macOS arm64 and Windows x64 produce the same fief-v1 golden |
| B5-04 Mode and end | All three end clauses, ties, human/headless, KEY-5 continuation, KEY-6 threshold, old-save modes, mode-bearing new saves |
| B5-05 Pacing | F-T1 (about 25 minutes, D8); no imputation of uncompleted KEY-6 |
| B5-06 Power, fuel | F-T2, F-T3; exact input/output conservation; mill loss, site loss, captured furnace |
| B5-07 Food, health | F-T4, F-T7; no mass creation |
| B5-08 Siege | F-T5; no wall assault without breach; powder consumption conserved |
| B5-09 Institutions | F-T6; no unstaffed benefit; knowledge-copy and capture cases |
| B5-10 AI | A1–A8 in Fief mode, Legend ≥ 60% vs Warlord, ≥ 80 mirrored duels; fog fuzz for deposits, powder stock, fleets |
| B5-11 Performance | Mean tick ≤ 4 ms, GC ≤ 16 ms; figure budget at E5 p95 population |
| B5-12 Comprehension | ≥ 5/8 uncoached players explain a mill/fuel bottleneck and one faith or money effect |
| B5-13 Integration | Full Release suite and strict validation green; golden changes isolated and explained; user approves merge |

## 11. Decisions

| ID | Question | Recommendation | Needs the user? |
|---|---|---|---|
| D1 | Is the next age E5 Faith & Fief, as a default-off FiefChapter that includes E4? | Yes | **Confirmed 2026-11-05** |
| D2 | Finish the whole age before any playtest, as you decided for Iron | Yes (matches the standing rule) | No |
| D3 | Raise the economy ceiling through a per-chapter economy profile instead of another flag | Yes; coordinate with the E4 balancer on the shared tables | Only if the balancer disagrees |
| D4 | Define "claim upkeep" for TER-10 in Fief mode only | Yes, a small per-tile grain/labor cost | **Confirmed 2026-11-05** (size to be fitted by the oracle) |
| D5 | Keep the simulation at 1:1; use 1:5 only to draw if the figure budget breaks | Yes | No |
| D6 | Branch and worktree: do E5 work in its own worktree and branch (like `civsim-e4`), never in the shared main tree | Yes; content-pack golden is refreshed only at merge time | No |
| D8 | How long should E5 be? | About 25 minutes, Iron's target rather than its measured 20. **Confirmed 2026-11-13**; the era base follows (§6) | No |
| D9 | Do E5 late projects cost double? | Yes. **Confirmed 2026-11-13** (7 of 49 by the 28 §7 definition) | No |
| D10 | Use the Thriving Legacy score? | Yes. **Confirmed 2026-11-13** | No |
| D11 | A countdown after the first rival's KEY-6? | None; only the human's KEY-6 ends Fief. **Confirmed 2026-11-13** | No |
| D7 | Which cut is acceptable if time runs short (tactical cavalry detail, bespoke art)? | None by default, same rule as 25 §15: no catalog promise is dropped silently | No |

## 12. Risks

- **E4 may change under E5.** A new E4 era base, cap or fuel rule moves every E5 baseline. Re-anchor after each E4 rebase, and keep E5 numbers as oracle inputs until then.
- **The content-pack golden is shared.** Typing E5 YAML changes the pack hash, so two sessions editing YAML will collide on `tests/golden/content-pack.txt`. Refresh it once, at merge.
- **Craft is optional, so Craft systems may never run.** Mitigation: `--grant-e5`, plus keeping the Council's leaning for Craft honest in the oracle.
- **Blast furnace can break the fuel balance again.** Fit it with the I-T6 protocol before promoting MET-12.
- **Too many institutions.** Monastery, university, hospital, cathedral, mills all need adults; E4's idle-academy result says to check staffing before adding more.
- **Gunpowder can erase siege decisions.** Compare full force tables, not isolated −70% text.
- **Plague may need a story.** The historical Black Death is not scripted here; if playtests want one, add it as a bounded shock after the base rates are fitted.
