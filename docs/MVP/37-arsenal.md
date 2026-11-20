# 37. The Arsenal (a single building for cannons, powder and the siege train)

Status: **ported into the main line (2026-11-20, branch `codex/arsenal-port`).** The Arsenal building, its recipes, supply reach, train staging, the conquest-prospect
trigger, the retired powder mill (enum slot kept; cannot be built or run recipes), Fief siege supply reach 10, relief range 4 for walled targets and Fief snapshot version 48
(47 and older still load) are in. **Not ported:** the strategist "leave Defend" changes of section 12 and the balance reports. All numbers are provisional and must be fitted (see §9).
A 150-minute Fief match with E5 granted (seed 123) built exactly one Arsenal per civilization and produced cannons and powder; no war occurred in it, so siege use is not yet exercised.

## 1. The short version (what to accept)

- **One new Fief building, the Arsenal,** unlocked by **WAR-12**, one per civilization. It **casts cannons and mills powder itself**,
  so a cannon no longer needs a blast furnace, a powder mill, saltpeter and sulfur to meet in one town.
- **It folds the powder mill in.** `make_powder` moves from the powder mill to the Arsenal and the powder mill structure retires.
  The blast furnace stays (it still multiplies iron by four); it stops being a gate on cannons.
- **It draws its inputs from your other towns** through its own supply reach, which **grows with carts, roads and the horseshoe**.
- **It is a second source of siege engines,** at lower labor. Engines stay forge-made from WAR-09 (they are an E4 weapon).
- **The AI builds one only when it is on a conquest plan** against a walled target. A human player builds it when they choose to.
- **The player sees one named building** with one tooltip that lists what it needs and which towns can supply it.

## 2. The problem, in numbers (natural play, 20 seeds, 80 civilizations, 2,236 towns, 180 minutes, E5 granted)

- A cannon needs, **in one town**, a blast furnace, a powder mill, saltpeter and sulfur in stock or in reach. Blast furnaces stand in
  3.7% of towns, powder mills in 6.5%, saltpeter access in 35.8%, sulfur in 28.8%. **25 towns (1.1%) in 20 of 80 civilizations**
  meet all four. At the horizon the whole sample held **16 cannons, 4.5 L of powder and 1 engine**.
- Why so few: the builder gives each town one structure at a time and takes the first open one in a fixed list (defense works first).
  The blast furnace sits after windmill and sawmill; the powder mill sits near the end, after monastery, university, hospital and
  concentric castle. Nothing asks "could this town make a cannon?", so the four-part set appears only by chance.
- The blast furnace alone needs a town of 80 or more with a water mill or windmill, a forge and a smelter in the same town (about 19% of
  towns have a mill). None of this is shown to a human player.
- The AI does research the prerequisites (25 of 30 civilizations finished all 49 non-keystone E5 projects by minute 150, with only 16
  needed for KEY-6), but it **never decides** to go for cannons. A stocked train is one of two signals the strategist reads before it
  will besiege a walled town, and the other (a sighted, distressed target) does not need any train at all.
- **Timing is not the blocker (measured, 2 natural no-grant matches, 8 civilizations, 180 minutes).** KEY-5 came at minutes 87-118. CHM-07
  finished at 98-127, MET-12 at 111-137, WAR-12 at 106-135 (median about 118), within a few minutes of KEY-6. After WAR-12 the civilization
  has about 60 minutes. Powder mills appeared quickly (1-5 towns per civilization by minute 140), but the **first blast furnace appeared at
  minute 160-170 and only in 3 of 8 civilizations**, and the **first cannon (one, with 0.25 L of powder) at minute 170 in 1 of 8**. Engines:
  0 or 1 at any sample. So research delivers the techs on time; the build order and the blast furnace's own prerequisites are what delay the train.
- **Walls arrive before the means to break them.** DEF-05 (stone walls) finished at minutes 50-71, WAR-09 (siege engines) at 83-113. The
  only natural assaults in the whole cohort came at minutes 40-63, before walls; by the time engines exist every civilization is already on
  Defend (switched at minutes 52-91) and stays there. That gap, not hostility, is why the late game has no wars.

## 3. The Arsenal

| | |
|---|---|
| Unlock | **WAR-12** (cannons). CHM-07 (gunpowder) is still needed to know how to make powder; the Arsenal's powder line requires it. |
| Limit | One per civilization (like the three-academy and three-monastery limits, but one). Rebuilding a lost one is allowed. |
| Where | Any settlement with a population of **60 or more** (Town tier) and a **forge**. No Market, Temple or Palace is required; nor are a mill, blast furnace, smelter or local deposit. |
| Cost (provisional) | **Labor 100**, **ashlar 24**, **blocks 12**, **wood 12**, **iron 4**. Above a castle (labor 60, ashlar 20, wood 12, iron 2), below a wonder (Great Library labor 150, blocks 60). A large stone work. |
| Lines | **Cannon** (`cast_cannon`), **gunpowder** (`make_powder`), **siege engine** (`make_siege_engine`, half the forge labor). |
| Cast iron | Open choice, §8 item 1. Recommended: the Arsenal casts its own cast iron for cannons at the existing `cast_iron_goods` numbers but double labor, so **no blast furnace is needed**. |
| Siting flavor | The old "stands apart from houses" spark rule for the powder mill is kept only as a tooltip line; it is not a placement rule. |

**What I recommend for the two open questions:** (a) *placement* - a forge and size 60, nothing hidden; the point of this building is to
remove hidden prerequisites. (b) *powder mill* - fold it in: gunpowder is used only for cannon shots (the only recipe that makes it is
`make_powder`; the only consumer is the cannon shot), so a separate civilian use is not lost.

## 4. Supply reach (its own, growing with transport)

The Arsenal pulls iron, saltpeter, sulfur and charcoal from your towns within its reach, and its cannons and powder go back to a
rally town the same way. Reach is in walking tiles and is its own number, separate from the 15/20-tile sister-settlement sharing of
SOC-07/GOV-04, so the Fief supply chain can grow on its own timeline.

| Step | Reach (provisional) |
|---|---|
| At WAR-12 | 8 tiles |
| + Wheeled Carts (TRN-05, E3) | +4 |
| + Paved Roads (TRN-07, E4), counted on a route with a completed road at both ends | +4 |
| + Stirrup & Horseshoe (TRN-09, E5) | +2 |

That reaches 18 for a road-and-cart civilization. The numbers are placeholders to be fitted (§9) against how far apart a civilization's
own towns actually are, which has not been measured. The sister-share network reaches farther than this only in name: it covers
metals, charcoal, kits and defense wood, and I have **not** checked whether Fief goods (saltpeter, sulfur, cast iron, powder, cannon) use it today.

## 5. What the AI does

- **Trigger (corrected after the screen, see §10):** the strategist sees a **conquest prospect**: a hostile **walled** rival within supply reach, an adequate force ratio, no relief veto, and the techs. It
  raises an "arsenal wanted" flag. The first version of this note said "on a Conquer plan"; the screen showed that deadlocks (§10). No Arsenal exists, and no conquest plan is chosen without one being feasible, so see §7 for the
  companion changes that let a conquest plan exist at all.
- **Builder:** an Arsenal sits with the defense works at the front of the build list (the "defense first" group), at the forge town
  nearest the target with the best supply score. Other towns keep their ordinary list.
- **Use:** once built, a stocked train counts as the "favorable" signal already in the siege evaluation, so a distressed target is no
  longer the only way in. The Arsenal never declares a war.
- **Not on a conquest plan:** the AI does not build one. A civilization under Defend or Prosper keeps building what it builds today.

## 6. What the player sees

- **One building, one tooltip:** "Arsenal - casts cannons, mills gunpowder and builds siege engines. Needs a town of 60 with a forge.
  Draws iron, saltpeter and sulfur from towns within N tiles (grows with carts and roads)." The tooltip lists the towns in reach and
  which of them hold each input.
- **Why-not text:** the build gates already return a cause for every block (for example "needs a forge", "needs soldiers"); the build
  menu shows it. I have not checked what the build menu shows today.
- **WAR-12's description** names the Arsenal as how cannons are made, so the technology points at the building.
- **Art:** built as meshes (docs/MVP/32 §4.13): the working, idle and site Arsenal in `art_fief_arsenal.gd`, with the sign, the owner's banner, a cannon, a counterweight engine and a crane; not yet wired into the frame or the layout.
- **A nudge:** when a player has a walled hostile town in supply reach and WAR-12, the plate offers "Arsenal recommended". Optional.

## 7. Companion changes (the Arsenal alone does not start sieges)

From the trace (Addenda 3 and 4): every civilization ends on **Defend**, because walled targets are almost never feasible
(supply fails in 92% of checks, relief threat 69%, no sighted distress 49%). The Arsenal fixes the missing train, not the gates:

1. **Fief siege supply reach 8 -> 10 tiles** - screened: necessary, not sufficient (supply gate 92% -> 5%, still zero investments).
2. **AI relief range for walled targets 8 -> 4 tiles** - unscreened.
3. **Let Defend end for Conquer when a feasible option exists** - unscreened.
Parts 2 and 3 plus the Arsenal are the next screen. Without them a civilization may have the building and never plan a conquest.

## 8. Open items

1. **Cast iron:** the Arsenal either takes cast iron from a blast-furnace town through the network, or makes it itself (recommended, no
   blast furnace needed). Needs a small cost check against the power oracle (the blast furnace is a fuel hog; a foundry in the Arsenal
   must not reopen that crunch).
2. **Engines:** whether the Arsenal's discount (half labor) is the right lever, given that only 1 engine stood at the horizon and the
   cause (never built, or worn out in its 12-season store) is **not yet measured**.
3. **Retiring the powder mill:** the Fief structure enum is append-only, so the powder mill keeps its number as an unused entry; the
   content registries, `structures.yaml`, `recipes.yaml`, the catalog text and the content-pack golden all change in one commit.
4. **Saved games and replays:** Fief is a draft chapter, but the structure and its stock hash must be appended, not renumbered.
5. **Art:** one building model and a sign (generic kit, doc 32); not designed here.

## 9. How the numbers get fixed, and the screen

- **Measure first:** the completion minutes of WAR-12, CHM-07 and MET-12 are measured (§2, two matches; a larger natural sample would
  tighten them). Still to measure: the distance between a civilization's own forge towns and its saltpeter, sulfur and iron towns, to set the
  reach tiers; and why engines are not built (never built, or worn out in storage).
- **Fit with the oracle:** cannon and powder costs inside the existing `e5_combat_oracle.py` and `e5_power_oracle.py` readings.
- **Screen:** the same 20 seeds as the siege cohort with the Arsenal, reach 10, relief range 4 and the Defend release, measuring the
  share of civilizations that build an Arsenal, cannon and powder stock over time, natural investments, and whether crew, powder and
  breach rules finally see natural exposure.
- **Gate:** F-T5 stays open until natural exposure exists or the owner accepts the controlled fixtures for the crew, powder and breach
  rules. This note promotes no catalog row and enables no Fief selection.

## 10. Screen result (2026-11-17): the first trigger deadlocks

Screened in the isolated copy with the companion changes on (supply reach 10, relief range 4, Defend release), 9 of 20 seeds before it was stopped
(Addendum 5 of the report). No civilization built an Arsenal, the flag fired in 3 civilizations (last at minute 45), there were 0 investments and
all 36 civilizations ended on Defend. After minute 100, 89.8% of the rejected walled-target evaluations lacked the "favorable" sign (no sighted
distress, no cannon plus powder in stock). The Arsenal was wanted only on a Conquer plan, and a Conquer plan needs a feasible option, which needs
that sign: the building that supplies the train was gated behind the plan that needs the train. **Change the trigger** to a conquest prospect (§5), or
let the plan form with the train pending and delay the declaration until it is stocked. The production side of the Arsenal is still untested.

## 11. What is built in the isolated copy (2026-11-17)

Built as real Fief-only rules in `civsim-e5-wpe58-iso` (no flags; Bronze and Iron are untouched):

- **Arsenal structure.** New `IronStructure.Arsenal` (appended; `PowderMill` keeps its number as a retired entry), spec labor 100, ashlar 24, blocks 12, wood 12, iron 4, large stone work, unlocked by WAR-12. Gate: Fief only, a town, size 60 or more, a forge in the town, one per civilization, a suppliable saltpeter and sulfur source in reach, built in the civilization's best-supplied eligible forge town. The planner wants it only on a **conquest prospect**.
- **Conquest prospect** (`PoliticsSystem.ConquestProspect`): WAR-12 known, and a hostile (relations below -30, or at war), contacted, not allied rival holds a walled settlement within siege supply reach of one of our towns, and 60% of our field strength beats that town's defenders (x1.1) plus nearby relief by 1.5. It reads **true state** (the AI's beliefs are not used; a simplification) and stores nothing, so a restored match answers it like an uninterrupted one. The economy asks it through `ConquestProspectProbe`, wired in `MatchModel`.
- **Content.** `structures.yaml`: arsenal added, powder_mill removed. `recipes.yaml`: `make_powder` and `cast_cannon` run at the Arsenal; `cast_iron_goods` runs at a blast furnace or the Arsenal (its own foundry, so no blast furnace is needed for cannons). Catalog text for CHM-07 and WAR-12 updated. Content pack golden refreshed (bytes 396,154 -> 396,500; hash bf208c3c... -> 0be8b678...); strict validation 0 errors, 0 warnings. CHM-07 alone no longer lets a civilization mill powder: it needs WAR-12 and an Arsenal.
- **Production and supply (corrected after review).** In the Arsenal town the cannon want is two cannons and two shots of powder and needs no blast furnace or powder mill. It does need saltpeter and sulfur to be **suppliable**: the **gate** refuses a site unless some own town within the Arsenal's reach (the site included) holds, camps or can dig each ore, and the Arsenal goes to the **best-supplied** eligible forge town (most diggable sources in reach, then the larger town). The Arsenal's own reach is 8 tiles, +4 carts (TRN-05), +4 paved roads (TRN-07), +2 horseshoe (TRN-09). **Other towns in range dig and ship:** each own town within reach that can dig saltpeter or sulfur gets a want for it (on the ordinary ore planner, so its own camps and labor do the digging), and every season the towns in reach carry all their saltpeter and sulfur to the Arsenal (nothing else uses them) and send it the iron it lacks, up to a two-cannon target of 4, from stock above their own bloom need. Nothing is created: each move debits the sender as it credits the Arsenal; order is ascending town id. Haul labor is not charged.
- **Train.** When a host is raised the Arsenal hauls its cannons and powder to the host's home town if that town is within the Arsenal's reach (`StageFiefTrain`); the AI's siege evaluation counts the Arsenal's stock as available to a town within reach (`FiefTrainStockAt`).
- **Companion rules.** Fief siege supply reach 10 (`Sieges.FiefSupplyReachTiles`; the simulation and the AI both use it); AI relief range for walled targets 4 (`StrategistTuning.FiefReliefRange`); in Fief, Defend no longer holds against a feasible Conquer plan.
- **Tests.** New `ArsenalTests` (gate, retired powder mill and Arsenal-made powder, reach tiers, supply reach, conquest prospect). Updated `FiefSiegeWeaponTests` (powder at the Arsenal) and `SettlementCoreGrowthTests` (arsenal classed with the large works, powder mill gone). Focused subset (Fief, siege, build, content, strategist, settlement-core and Arsenal tests, 523) after the supply fix: 522 passed and the one failing new test (sister shipping) was fixed and rerun green (6 Arsenal tests pass); toy, research and politics goldens still pass.

**Not built:** the Arsenal's siege-engine line (engines stay forge-made); the doubled labor for its own cast iron; charging haul labor for the shipments; the build-menu tooltip, why-not text, WAR-12 description change in the UI and the plate nudge; art; a full-suite run (the full suite was started and stopped for time; only the focused subset above was run); a saved-game round trip with an Arsenal standing.

## 12. Leaving Defend (2026-11-17, Fief only)

Three strategist changes, all gated on the Fief chapter so Bronze and Iron decisions and goldens are unchanged:

1. **Third-civ veto becomes a counter-attack check.** A third civilization at threat 0.30 or more no longer blocks a campaign outright. The strategist runs the same `CounterThreat` it uses for the target: the launch is refused only if that third civ could take one of our settlements after the campaign leaves, counting walls, residual garrison and sister relief. Outside Fief the blanket veto stays.
2. **Defense urgency shrinks with walls.** `FiefWallDiscountedUrgency`: urgency × (1 − 0.5 × share of settlements with stone walls or a castle). A fully walled realm halves the pull of Defend (1.3 × urgency).
3. **Opportunistic Conquer for every archetype.** `ConquestAggression`: Builder, Grower, Sage and the rest keep their temper (0.8) unless the target is in its window of weakness or the force ratio is at least 1.3 times what the assault needs; then they play the conquest at neutral 1.0. Warlord (1.3) and Balanced/Wanderer (1.0) are unchanged.

Tests: `FiefStrategistOpportunismTests` (arithmetic and four scenarios). Not yet measured in a whole match.


### 12.1 First natural result (3 matches, 180 min, seeds 30006879/30013794/30198005)

Raw: `docs/MVP/balance/m12-e5-8-wpe58-controlled-siege-sea10-2026-11-15/leave-defend-raw/` (analysis-leave-defend-war.md). Cohort K before the change: 80 of 80 civilizations ended in Defend. After: 4 Defend, 5 SecureTin, 2 Prosper, 1 Coalition of 12. No civilization chose Conquer, no wars after minute 60, no sieges, 3 unwalled assaults early. "threatens us" rejections fell from 2451 to 0. The counter-attack check is now the main late blocker (294 rejections in three matches; 27% of walled-target evaluations pass every siege gate and die there). The aggregated trace cannot say whether the exposing army is the target's or a third civ's. Three matches are not a verdict.

### 12.2 Who exposes us (same three matches rerun with the `fief-war-exposure` row; last plans identical, so the trace is neutral)

Counter-attack rejections by exposing army, after minute 100: 147 from the target's own army, 147 from a third civilization (60-minute and 100-minute windows: 23/0 and 95/15). Mean gathered rival army 1248 (target) and 1171 (third), against our own thin post-departure garrisons. In seed 30006879 one third civ per Arsenal civ accounts for all 80 and 65 late rejections. Raw: `leave-defend-exposure-raw/`.

### 12.3 Weaker-neighbour exemption and field reach cap (Fief only)

- A third civilization blocks a launch only if it is a current threat (0.30), its counter-attack would take one of our settlements, and its army exceeds our whole attacking strength (`ThirdCivBlocks`, `FiefStrongerNeighbourMargin` 1.0). A weaker neighbour no longer stops us.
- `CounterThreat` counts a rival settlement only if it lies within `FiefCounterReachTiles` (10, the Fief siege supply reach) of the town it would attack, for both the origin and every contributor. Applies to the target's own counter-attack and the third civ's.
- Tests: `FiefStrategistOpportunismTests` (13). Natural result: `leave-defend-reach-raw/`.

### 12.4 First natural result of 12.3 (3 matches, 180 min, seeds 30006879/30013794/30198005)

Raw: `leave-defend-reach-raw/`, `analysis-leave-defend-reach-war.md`. Before 12.3 (12.1): 0 wars and 0 sieges after minute 60. After: 41 wars and 96 assaults after minute 100 (47 and 125 over the whole match); 25 walled launches that became investments; 36 investments ended in capitulation, 14 relieved; 272 captures. Two investments ran with 2 cannons (seed 30006879, seasons 620 to 659), no engine investments, and the whole match shows only 3 arrivals with a cannon at the origin. Final plans: 2 Defend, 4 SecureTin, 5 Prosper, 1 Coalition; plan churn between Conquer, SecureTin and Defend is high (27 SecureTin to Conquer, 21 Conquer to SecureTin, 18 Conquer to Defend after minute 100). Counter-attack rejections remain the largest single late reason (264), now almost all the target's own army. Three matches only.

### 12.5 Twenty-match cohort (cohort K seeds, 180 min, frozen build with 12.3)

Raw: `leave-defend-cohort-raw/`; analyses `analysis-leave-defend-cohort-war.md`, `-assault.md`. Baseline: cohort K (`arsenal-supply-raw/`), same seeds. Last plans: K 80 of 80 Defend; now 15 Defend, 26 SecureTin, 28 Prosper, 7 Coalition, 4 Conquer (of 80). Launches: K 15 (all unwalled); now 945 unwalled and 195 walled; 1080 captures; 290 investments capitulated, 98 relieved, 22 sallied; 23 investments launched from an origin holding a cannon (K 0); 102 cannons in stock at the horizon (K 44). End-state spread (largest over smallest civ settlements): mean 4.37 (K 2.08), worst 39.0 (seed 30424177: 37/0/39/7, one civilization eliminated); smallest civ averages 15.2 settlements (K 19.2); total settlements 100.8 (K 111.7). The three Fief-off goldens (toy, research, politics) still pass. Legacy outcomes are not in these traces.

### 12.6 The 39:1 match (seed 30424177), from existing rows

50 captures, all in two bilateral wars: civ 0 against civ 1 (13 to 7 for civ 0) and civ 2 against civ 3 (11 and 7), with civ 2 and civ 3 each also taking from civ 0 later. Civ 0 started taking from civ 1 at minute 30 to 60 and never stopped; civ 1 never completed WAR-09 or WAR-12 and ended with 0 settlements (eliminated); civ 3 fell to 7 and civ 2 rose to 39. No rule slowed either war as the loser shrank, and the Coalition plan (7 of 80 final plans) did not stop it. Nothing here shows the new rules misfiring; they let two long wars run to the end. Whether that is acceptable is a design call.

## 13. Player choice and connected Arsenal logistics (2026-11-18 candidate)

- **Siting.** WAR-12, Town tier/60 people, a local forge, the construction cost, and one Arsenal per civilization remain. A player may commission it in any eligible forge town. Nearby powder-ore deposits and winning a civilization-wide site ranking are no longer construction gates.
- **Automatic siting.** The AI still selects one site: it values connected saltpeter/sulfur sources and short hauls, prefers a town without a competing long investment, discounts recently raided sites, values walls/castles/garrison, and favors access to the civilization's own frontier rally towns. A conquest prospect makes the Arsenal wanted before CHM-07 and moves it ahead of other Iron works; otherwise the AI starts preparing after CHM-07. This uses owned settlement facts for siting; the existing conquest-prospect signal is used only as an urgency trigger.
- **Land logistics.** Populated settlements of the same civilization are relay nodes. A leg requires a land path and lies within Arsenal reach (8 tiles, extended by TRN-05/07/09); a road-linked pair shortens the walking distance by one third. Ore sources beyond the direct Arsenal radius can feed it through a chain of settlements. While the Arsenal is being built, it can also quarry raw stone in the 8-tile catchments of connected own settlements; long hauls use the same cart-wear rule. Its site recommendation now requires enough reachable stone for the block and ashlar cost, and it skips a site the player has set aside. Cargo remains conserved: transfers debit the source and credit the Arsenal.
- **Cart wear.** Direct/local service remains free. A longer route wears one cart per 4,000 extra unit-tiles; cannon shipments use 20 times the cart wear. Connected cart stocks pay the wear in stable settlement order, and insufficient cart stock limits the shipment. These values are provisional. The same route and wear budget govern cannon/powder delivery to an army's home town.
- **Independent production.** Cannon wants require WAR-12 and the Arsenal; cast iron still needs MET-12 and actual iron/clay inputs. Powder wants separately require CHM-07 and actual saltpeter/sulfur/charcoal. Missing powder inputs no longer suppress cannon production.
- **Player guidance.** After WAR-12, the Building panel shows the planner's recommended site, connected saltpeter/sulfur source towns, disconnected ore towns, relevant research, site stock, and cart stock. It explains that connected towns can relay production inputs and construction stone, and that remote hauls wear carts. Setting the current Arsenal aside releases its one-per-civilization reservation so another eligible supplied town can take it; invested work remains shelved at the old site.
- **Remaining limitation.** The network only connects own populated settlements by land. A civilization with no owned source of a powder ore must acquire a deposit or bring ore stock into a connected settlement; this change does not add a new import or trade system. Exact enemy-target forecasting for the AI site is also not implemented; owned frontier infrastructure is the proxy.
