# 25 — Iron (E4): Tools, Roads and the Reach of the State

> **WP1 scope/design approved; WP2 and WP3 authorized.** This is the approved design baseline for those work packages. Numeric proposals in [26 — E4 specs](26-e4-iron-specs.md) remain provisional; every E4 project stays `status: draft` pending WP5 mode-aware live and forecast eligibility and later runtime implementation. Audit baseline: `3395518`, 2026-11-02, branch `codex/e4-iron`. The design follows [22](22-bronze-age.md), but does **not** inherit its grant of design autonomy or treat its open questions as resolved. Existing E0–E3 content, behavior, acceptance thresholds, match endings and goldens remain binding.
>
> **Count discrepancy requiring review:** this checkout contains **56 regular E4 draft projects (15 Craft, 17 Kin, 24 Reach), plus KEY-5 = 57 nodes**. The milestone brief says 59 projects. The source-of-truth table in [03 §4.1](03-research-catalog.md#41-projects-per-era-and-theme) also says E4 has 56; it lists **E1 Hunters** as the era with 59. That is a plausible source of the brief's number, but may not explain its provenance. Section 2 inventories every actual E4 entry; no missing projects have been invented. Scope is provisional until the user confirms whether the brief's count is stale or identifies omitted nodes.

## 1. What Iron should feel like

Bronze concentrates scarce metal and power. Iron makes useful tools and arms available across a wider countryside, but that promise depends on charcoal, smith time and transport. Roads and grain routes let a city draw support from many settlements; physicians and water infrastructure help it survive density. Professional soldiers and siege engines make walls contestable, while provinces and contracts let an extended state govern beyond its capital. The player still chooses three research slots, uses the existing Decrees sparingly, and watches autonomous planners carry out the consequences.

Four tensions organize the era:

| Tension | Material pressure | Choices that answer it |
|---|---|---|
| Accessible ore, costly tools | Bloomery output, charcoal and smith labor constrain replacement | T11 iron versus T12 steel edges; imports versus local mining; coppicing and water power |
| Dense core, wide state | Grain, water, route capacity and administration constrain cities | Aqueducts, public grain, roads, citizenship, provinces; each has a real cost |
| Wall, engine, relief | A defended city is attackable but requires supply, crew and time | Engines versus investment; towers, relief, blockade and peace |
| Learned institutions, actual capacity | Research does not staff a governor, feed a soldier or complete a bathhouse | Civilian labor versus specialists, garrisons, construction and maintenance |

This is the game era “Iron Age and Classical antiquity,” not a claim that all cultures followed one chronology. The existing Heavy Plough, Feudal Contract and Castle nodes mark the transition into Faith & Fief. Renaming or moving these nodes is outside this draft.

## 2. Audit before design

### 2.1 Catalog and paths

Source of truth: top-level project `era: 4` fields in `content/catalog/*.yaml` (not nested `era_advance` or breadth fields), compared against E4 rows of [03a](03a-research-craft.md), [03b](03b-research-kin.md), [03c](03c-research-reach.md), and [04 §5.2](04-research-paths.md#52-e4-iron--key-5-feudal-order). Every E4 row must remain `status: draft`; existing E4 draft rows may be transcribed in place with typed effects, practice, flavor, and Moment fields. “Unlock” summarizes existing intent; numeric interpretation is proposed in 26 and may narrow ambiguous prose. Direct downstream prerequisite links are in 26's requirements column.

| Lineage | Draft project | Existing unlock / effect intent |
|---|---|---|
| AGR | [AGR-08 Terracing](../../content/catalog/agr-agriculture.yaml) | Build: Terraces. Hills and mountains become farmland. |
| AGR | [AGR-09 Iron Sickle & Scythe](../../content/catalog/agr-agriculture.yaml) | ▸ Harvest: Scythe ×2. |
| AGR | [AGR-10 Heavy Plough](../../content/catalog/agr-agriculture.yaml) | ▸ Till: Mouldboard. Heavy clay soils (temperate forest biomes) become prime farmland. *KEY-5 prerequisite.* |
| ART | [ART-08 Theatre & Arena](../../content/catalog/art-arts-culture.yaml) | Build: Theatre or arena. City Cohesion +15%; unrest −. |
| BEL | [BEL-08 Philosophy & Ethics](../../content/catalog/bel-belief-ritual.yaml) | Reasoned inquiry: Insight +10%; diverse groups cohere (conquered peoples integrate faster). |
| BEL | [BEL-09 Organized Faith](../../content/catalog/bel-belief-ritual.yaml) | Universal religion spreads along trade routes: Cohesion with *other* civilizations' converts (soft influence, claim pressure abroad). Role: Missionary. *KEY-5 prerequisite.* |
| BLD | [BLD-09 True Arch & Vault](../../content/catalog/bld-building-architecture.yaml) | Spans without timber: bridges, aqueducts, large halls. |
| BLD | [BLD-10 Aqueducts](../../content/catalog/bld-building-architecture.yaml) | Build: Aqueduct. City population cap +40% (water), disease −15%. |
| BLD | [BLD-11 Concrete & Domes](../../content/catalog/bld-building-architecture.yaml) | Roman concrete: build speed +30%; harbor and dome construction. Wonder: Great Dome (unlocked here, built later). |
| CER | [CER-07 Glassblowing](../../content/catalog/cer-ceramics-glass.yaml) | ▸ Glass: Blown ×4. Glass becomes common: storage, trade, early windows. |
| CER | [CER-08 Fired Brick & Tile](../../content/catalog/cer-ceramics-glass.yaml) | Good: Fired brick and roof tile. Fire risk in towns −50%; building durability +. |
| COM | [COM-07 Alphabet](../../content/catalog/com-language-writing-communication.yaml) | Literacy easy to learn: scribes ×2; Commons Insight +5%. |
| COM | [COM-08 Courier Relays](../../content/catalog/com-language-writing-communication.yaml) | Postal relays on roads: Overreach −20%; adoption across distance ×2. |
| DEF | [DEF-06 Towers & Gatehouses](../../content/catalog/def-defense-fortification.yaml) | Wall defense +50%; controlled gates (trade not disrupted in wartime). |
| DEF | [DEF-07 Frontier Walls](../../content/catalog/def-defense-fortification.yaml) | Build: Border wall segments. Border tiles resist flips −50%; raids across walls −70%. |
| DEF | [DEF-08 Castle Building](../../content/catalog/def-defense-fortification.yaml) | Build: Castle. Holds a region (claim radius 5, strong); garrisons. *KEY-5 prerequisite.* |
| DIP | [DIP-06 Tributary States](../../content/catalog/dip-contact-diplomacy.yaml) | Diplo: Vassalage (a weaker civilization pays tribute; its land counts 25% toward your Dominion). |
| DIP | [DIP-07 Leagues & Confederations](../../content/catalog/dip-contact-diplomacy.yaml) | Diplo: League (multi-civ alliance; shared Wonder build). |
| FOD | [FOD-09 Public Grain Supply](../../content/catalog/fod-food-craft-storage.yaml) | State grain distribution: city food security +; city population cap +25%. |
| FOR | [FOR-08 Grafting](../../content/catalog/for-foraging-plant-lore.yaml) | Orchard yield ×2; wine and oil trade goods. |
| GOV | [GOV-07 Bureaucracy & Census](../../content/catalog/gov-governance-law.yaml) | Role: Administrator. Overreach −25%; planners see full-civilization stats (better allocation). |
| GOV | [GOV-08 Republic & Assemblies](../../content/catalog/gov-governance-law.yaml) | Citizen participation: Commons Insight +15%, Cohesion +; slower war declarations. |
| GOV | [GOV-09 Feudal Contract](../../content/catalog/gov-governance-law.yaml) | Vassal lords hold distant land: remote regions don't secede; local defense +. *KEY-5 prerequisite.* |
| HUS | [HUS-09 Camels & Caravan Animals](../../content/catalog/hus-animal-husbandry.yaml) | Desert and mountain caravans: trade range across deserts ×3. |
| INQ | [INQ-04 Natural Philosophy](../../content/catalog/inq-inquiry-natural-science.yaml) | Rational inquiry: Scholar Insight +20%. |
| INQ | [INQ-05 Mechanics & Hydrostatics](../../content/catalog/inq-inquiry-natural-science.yaml) | MEC and NRG project costs −10%; buoyancy (ships +10% cargo). |
| KEY | [KEY-5 Feudal Order](../../content/catalog/keystones.yaml) | Faith & Fief era; scale 1:5. |
| LRN | [LRN-05 Academies](../../content/catalog/lrn-learning-education.yaml) | Build: Academy. Role: Scholar (Insight 6). |
| LRN | [LRN-06 Great Library](../../content/catalog/lrn-learning-education.yaml) | Wonder: Great Library. Insight +20%; diffusion discount from contacted civilizations doubles. |
| MEC | [MEC-04 Pulley & Windlass](../../content/catalog/mec-mechanics-machines.yaml) | Lift loads: tall buildings, deep wells (water access +). Build speed +15%. |
| MEC | [MEC-05 Gears & Screws](../../content/catalog/mec-mechanics-machines.yaml) | Presses (olive oil, wine: food & trade +), Archimedes' screw, water-wheel gearing. |
| MEC | [MEC-06 Treadwheel Crane](../../content/catalog/mec-mechanics-machines.yaml) | Build: Crane. Large structures and wonders build ×2. |
| MED | [MED-05 Medical Texts](../../content/catalog/med-medicine-health.yaml) | Codified medicine: all MED effects +15%; Role: Physician. |
| MED | [MED-06 Baths & Latrines](../../content/catalog/med-medicine-health.yaml) | Build: Bathhouse, latrines. Density disease −25%. |
| MET | [MET-09 Iron Tools](../../content/catalog/met-metallurgy.yaml) | ▸ Chop: Iron axe; ▸ Till: Iron share; ▸ Quarry: Iron pick. Tools for everyone, not just elites. |
| MET | [MET-10 Quench & Temper](../../content/catalog/met-metallurgy.yaml) | Steel edges: tool durability ×2, weapons +25%. |
| MIN | [MIN-07 Mine Drainage](../../content/catalog/min-mining-quarrying.yaml) | Archimedes' screw and bucket wheels: flooded deposits usable; depth tier 2. |
| NRG | [NRG-03 Water Wheel](../../content/catalog/nrg-energy-power.yaml) | Build: Water mill. Grinding ×5; powers sawmills, bellows and trip hammers. |
| NUM | [NUM-07 Geometry & Surveying](../../content/catalog/num-number-measure-time.yaml) | Land surveying: fields, roads and aqueducts laid out optimally (+15% infrastructure efficiency). |
| SEA | [SEA-07 Harbors](../../content/catalog/sea-watercraft-seafaring.yaml) | Build: Harbor. Ship capacity ×2; port cities grow +20%. |
| SEA | [SEA-08 Great Lighthouse](../../content/catalog/sea-watercraft-seafaring.yaml) | Wonder: Lighthouse. Shipwrecks −50% civilization-wide; sea trade +20%; landmark claim. |
| SEA | [SEA-09 War Galleys](../../content/catalog/sea-watercraft-seafaring.yaml) | Naval combat; blockade enemy harbors. |
| SOC | [SOC-10 Citizenship](../../content/catalog/soc-kinship-society.yaml) | Shared civic identity: Cohesion decays 50% slower with distance from capital. |
| STN | [STN-12 Ashlar & Columns](../../content/catalog/stn-stoneworking.yaml) | Iron chisels: +40% stone build speed. Unlocks classical colonnades (Theatre, Temple tier 2). |
| TER | [TER-08 Provinces & Governors](../../content/catalog/ter-claim-settlement.yaml) | Role: Governor. Overreach −30% in governed provinces. |
| TER | [TER-09 Frontier Colonies](../../content/catalog/ter-claim-settlement.yaml) | Planned colonies across water or far away: fission bands can found overseas settlements. |
| TRD | [TRD-06 Coinage](../../content/catalog/trd-exchange-commerce.yaml) | Money: trade friction −40%; taxes (admin capacity +). |
| TRD | [TRD-07 Long-Distance Trade Routes](../../content/catalog/trd-exchange-commerce.yaml) | Caravan (overland) or maritime (harbor-to-harbor) trade: route length ×3; Silk-Road / Indian-Ocean–style network. |
| TRN | [TRN-07 Paved Roads](../../content/catalog/trn-land-transport.yaml) | Build: Road. Move ×1.5 on roads; claim strength along roads +; adoption spreads along roads ×2. |
| TRN | [TRN-08 Stone Bridges](../../content/catalog/trn-land-transport.yaml) | Build: Bridge. Rivers no longer block roads. |
| WAR | [WAR-07 Professional Armies](../../content/catalog/war-arms-warfare.yaml) | Role: Soldier. Standing army: drilled units ×1.5; garrisons hold claims. |
| WAR | [WAR-08 Iron Weapons](../../content/catalog/war-arms-warfare.yaml) | Cheap iron arms: army size cap ×2. |
| WAR | [WAR-09 Siege Engines](../../content/catalog/war-arms-warfare.yaml) | Assault walls: siege time −60%. |
| WAY | [WAY-06 Periplus & Itineraries](../../content/catalog/way-wayfinding-cartography.yaml) | Written route guides: trade route efficiency +25%. |
| WAY | [WAY-07 Grid Cartography](../../content/catalog/way-wayfinding-cartography.yaml) | Coordinate maps: Reveal: coastline outlines of the known world; Beacon range +. |
| WOD | [WOD-07 Cooperage](../../content/catalog/wod-woodworking.yaml) | Good: Barrels. Bulk liquid storage and shipping: food spoilage in transit −40%. |
| WOD | [WOD-08 Frame & Crosscut Saws](../../content/catalog/wod-woodworking.yaml) | ▸ Chop: Saw (×1.4 over iron axe); plank output ×2. |

Lineages with no E4 project: FIR, FIB, CHM and HNT. KEY-5 is a transition node, not a 57th regular project. The exact inventory is 56 + 1; promoting 59 would fail reconciliation.

The keystone remains **KEY-5**, leaves E4, enters E5, requires **AGR-10, GOV-09, BEL-09, DEF-08**, and **21 regular E4 completions**. The land path through HUS-09 has a Seen gate; TRD-07's SEA-07 alternative must remain viable when camels/llamas are absent. Research geography cannot soft-lock the keystone. `04`'s closure counts are historical catalog summaries, not proof of a live completion time. Recalculate closure with AND/OR alternatives before content promotion.

### 2.2 Code assumptions and integration boundaries

Paths below are relative to the repository. Line references identify the audited baseline and must be refreshed after rebase. This is an integration inventory, not permission to change these files. Literal KEY-4 references in tests/reports are often intentional M10 contracts, not bugs.

| Boundary | Evidence | Consequence for E4 |
|---|---|---|
| Research ceiling | `src/CivSim.Sim/Research/ResearchSystem.cs:47–50,641` declares `LastResearchEra = 3` and returns Hidden above it, even for numeric content | Keep Bronze ceiling; an explicit per-match rules object must carry E4's ceiling. Numeric promotion alone cannot enable play |
| Economy ceiling | `src/CivSim.Sim/Economy/LivingEconomyModel.cs:213–214,877,3974` declares `MaxEra = 3`, filters project completion and clamps era | Separate from research; both must agree on mode, including debug grants and replay restoration |
| Chapter end | `src/CivSim.Sim/Research/MatchEnd.cs:17–19,56–75`; `MatchModel.cs:110–113` | End uses `KeystoneLeaving(LastResearchEra)`, catalog cap and countdown. Raising the global constant would silently retarget every M10 match |
| Council | `ResearchSystem.Council.cs:41–44,168–169` stops keystone chase beyond the ceiling; E3 critical-path/civic preference remains | Add E4 mode-specific planning; preserve Bronze leftover behavior and E3 preferences |
| Spark and forecasts | `Ai/Spark/ResearchSpark.cs:100–104,889–971,1025`; `Ai/Spark/SparkTuning.cs:20–22`; `Ai/Forecast/ResearchForecast.cs:22–23,547,583–590` | Named Key1…Key4 ETA fields and KEY-4 diagnostics are not an E4 forecast. Do not reinterpret an E3 critical-path branch by raising LastResearchEra. New KEY-5 ETA and closure policy need separate fields/tests |
| Civic effects | `Economy/LivingEconomyModel.Civic.cs:187` filters `p.Era != 3` | E4 institutions cannot be assumed to apply through the Bronze aggregator; new effects need an additive E4 calculation |
| Metal inventory and tools | `Economy/LivingEconomyModel.Metal.cs:230,568,629,699,1260,1886` maps only kits 8–10 and requests a one-unit iron bloom stock | Append kit/good identities and E4 recipes; never let a default switch map T11 to T10. Existing MET-08 is bloom production, not iron tools |
| M10 timing and exposures | `src/CivSim.Harness/ResearchM10.cs:200–224`; `PoliticsM10.cs:177,311–329,877–941` | Capture chapter-end state separately from continuation used to observe own KEY-4. Keep fixed KEY-4 and 21-E3 breadth semantics |
| M10 resource and score reporting | `ResearchE3.cs:230`, `ResearchDecrees.cs:129`, `ResearchMetal.cs:372–415`, `B29ForecastDiagnostic.cs:101–132` | Preserve own-KEY-4 baselines and horizon-censoring. Add Iron reports rather than retarget Bronze reports |
| AI tournament stopping | `src/CivSim.Harness/Tournament.cs:1362,2253,3039`; `AiCommand.cs:919–926` | Existing duels stop at Bronze chapter; `--check-m10` must retain the mode and protocol. Iron tournaments opt in explicitly |
| Replay/soak acceptance | `src/CivSim.Harness/AcceptanceCommand.cs:177,194–206,247–254` | Exact chapter-end checks and golden-producing runs remain Bronze. New Iron corpus must carry its mode |
| Game end/countdown | `game/Scripts/SimulationSession.cs:24,214–215,434`; `game/Scripts/Hud/Game.Modals.cs:636` | Current UI routes through MatchModel but text assumes Iron dawn / KEY-4. New mode must label KEY-5 and preserve Bronze text |
| UI research visibility | Research-web era layout is dynamic; displayed nodes still depend on research Hidden state | Do not expose E4 by extending a column alone. Mode and local civilization advancement control visibility |
| Debug scenario grant cap | `game/Scripts/Hud/Game.Scenarios.cs:403` grants only numeric non-keystone projects with `Era <= 3` | Keep existing debug scenario Bronze; add an explicit Iron scenario, with no bypass of mode/use requirements |
| Demography and hazard attribution | `Economy/LivingEconomyModel.cs:3699` defaults late-era elder baseline to E3; `:3838,3856` has exact `era == 3` child/adult cause-share tables, while E4 would fall through to earlier shares if unclamped | Unclamping E4 without an explicit health policy would regress attributed cause shares. E4 needs its own calibrated health path; do not casually replace `==` with `>=` |
| Fission and seasonal camps | `Politics/PoliticsSystem.cs:497–499,653` uses `era == 3` for Expand 0.83, otherwise 0.80, and stops new camps for `era > 3` | Existing Iron-threshold behavior is already affected by raw research era. Preserve it in Bronze; explicitly fit E4 fission/camp policy |
| Raw-era reach and claims | `Research/LivingEconomySource.cs:402`; `Politics/PoliticsSystem.cs:643,660–666` index era reach, claim and radius by research era, not the economy clamp | Existing Bronze threshold may read E4 registry values. Mode plumbing must preserve the old path; E4 additions cannot assume all era-4 lookups were inert |
| Forecast catalog eligibility | `Ai/Forecast/ForecastCatalog.cs:135–136`; `ResearchForecast.cs:99–101,225,306,870` loads numeric nodes and prepares later-era forecasts | E4 numeric promotion can affect AI before live State exposes a node. Mode-aware future-eligibility filtering is a prerequisite to promotion, not a later cleanup |
| Strategic era boundaries | `Ai/Strategist/Strategist.cs:114,315`; `Strategist.Assess.cs:90,272`; `Strategist.SecureTin.cs:14`; `Ai/BronzeDefensePolicy.cs:36,82,122`; `Ai/OwnView.cs:114`; `Ai/StrategistDecreePolicy.cs:363` | E3-plus threat/bronze activation predicates usually should continue; the `<=3` frontier priors deliberately change after KEY-4. Audit each rule rather than bulk-changing comparisons |
| Worldgen's aggregate oracle | `World/TerritoryFairnessSimulator.cs:165,248,289` stops camp additions after era 3 and allows sea level 2 / other continents at era 4 | This is fairness simulation, not the live economy. Preserve worldgen output and its golden; a new E4 geography screen must not retune this old helper implicitly |
| End-screen era test and tips | `game/Scripts/Hud/Game.Modals.cs:504` labels Iron dawn at `CivEra >= 4`; `Game.Notify.cs:553` has E3-plus coaching | Mode-specific text/tips need their own gates; an Iron match must not show the Bronze ending at KEY-5 |
| Array audit: no four-era truncation found in the research tables | `ResearchCatalog.cs:149,162–168,241` uses `EraCount = 10`; `CivResearch.cs:29`, `ForecastStart.cs:102`, `ForecastCatalog.cs:105,130`, `ResearchForecast.cs:99–101,132` use EraCount | The named Key1–Key4 output remains the actual ceiling. `ResearchForecast.cs:92–93` arrays of 4 are power-cache slots with fallback at 457, not eras; replay arrays of 4 are civilization seats, and investment arrays of 5 are age bands. Do not resize these unrelated arrays |
| Regression contracts | `src/CivSim.Tests/BronzeResearchTests.cs:354–407,546–712`; `ContentGoldenTests.cs:49–52`; `ResearchSystemTests.cs:19`; `ForecastKey4Tests.cs`; `E3ContentTests.cs:34,67` | Explicit Iron-threshold, 214-numeric-node and KEY-4 cost assumptions require additive Iron tests; keep Bronze behavior assertions. Content-count assertions may need approved additive adjustment only during promotion |
| Shared constants / saved state | `content/registries/eras.yaml` E4 has base 1200, rep scale 2, reach 20; KEY-4 prose explicitly keeps E4 hidden; enum/array/save/hash consumers accompany the above state | Presence in the catalog is not activation. No silent global era-row reuse, representation conversion, command renumbering or unversioned save change |

Repeat the audit after each rebase with `rg -n 'MaxEra|LastResearchEra|KEY-4|Key4|Iron threshold|EndOfMatch|ChapterEnd' src game`, then inspect era-indexed arrays, literal era-3 filters, kit switches, command schemas and snapshots. Distinguish meaningful semantics from comments and diagnostic strings. A mode bit alone is insufficient unless **all** research, economy, AI, end-rule, serialization and view paths carry it.

### 2.3 E3 findings that constrain this design

[07d](07d-e3-balance-findings.md) is a moving evidence log. The 2026-11-02 sections are later than the original handoff in [24](24-m10-handoff-wp8a.md) and [m10-resume-prompt](m10-resume-prompt.md). No status in this draft certifies M10 closed.

- **A3:** clean survival did not imply winning; founder/daughter age composition, food labor and expansion compounded before attacks. A one-seed scout fix did not generalize. The later prepared-withdrawal policy reached 81%/79% on two 80-match blocks. E4 needs food/labor/casualty ledgers and whole-match comparisons, not just a siege success rate.
- **A5:** 5–30% growth differences accumulated into Dominion dominance; changing one archetype moved wins to another. Use all archetypes together, rotation-balanced training and held-out cohorts, at least 80 matches for noisy conclusions. Do not import E3 multipliers as an E4 fit and do not use rank-dependent assistance.
- **B17:** a safety filter stopped founding toward rivals while total land hardly changed. City, food, fission, land and border contact are separate outcomes. The later capital-food work exposed incomplete own-KEY-4 trajectories caused by founder losses, not research costs. Report incomplete exposures explicitly; never replace them with zero or silently drop them.
- **B28:** queued defenses are not completed defenses. Research, wood, build labor and arrival time each bind. A 1/1 or 4/4 qualifying cohort is unsampled for conclusions. E4 defense/engine telemetry must show all four bottlenecks and the qualifying denominator.
- **B31:** Legend did not reliably beat Warlord; the latest small M10 report is 31%, not a reliable new estimate of ability. Larger paired cohorts must precede tuning conclusions; more forecast depth cannot compensate for an economy/planner mismatch.

## 3. Match structure and compatibility contract

**Proposal requiring approval:** introduce explicit `IronChapter` match rules, default off. Existing `BronzeChapter` stays the default in the game, harness, tournaments and saved games. All existing M10 acceptance commands retain Bronze rules even after E4 content becomes numeric. Do not change the user default until separately authorized.

| Mode / civilization state | Research | Economy / systems | Ending |
|---|---|---|---|
| BronzeChapter, before or after KEY-4 | Existing E0–E3 behavior; E4 always Hidden | Existing MaxEra 3, including Iron threshold | Human KEY-4 or first rival KEY-4 + countdown; no year cap (28 §17) |
| IronChapter, before own KEY-4 | E4 Hidden and unavailable to Council/AI/commands | No E4 effects, structures, recipes or AI spending | KEY-4 does not end this opt-in mode |
| IronChapter, after own KEY-4 | E4 becomes available/glimpsed according to prerequisites and gates | E4 eligible; research unlock still needs local inputs/adoption/staffing | Human KEY-5 or first rival KEY-5 + `IronCountdownSeasons`; no year cap (28 §17) |
| IronChapter, after own KEY-5 | E5 Hidden; E0–E4 leftovers use existing EraLag | E4 economy continues, no E5 systems | Countdown as above |

**Update 2026-11-08: the year cap is removed entirely** ([28 §17](28-production-gap.md)); the Year-96 and Year-130 figures below are history. The Iron chapter ends only by the human's KEY-5 or the countdown (`IronCountdownSeasons`, now 48 seasons). **Update 2026-11-07:** the Iron cap is now Year 130 and the countdown after the first rival KEY-5 is 72 seasons (18 years), set in [28 §6](28-production-gap.md) so the latter half of the age lasts long enough to build what was researched; the Year-96 cap and 12-season grace below are the original proposal. The proposed Year-96 cap and 12-season grace are **candidate values**, not approved thresholds or measurements. For pacing, [02 §8.4](02-research-structure.md) gives an older **14–16-minute** E4 target; [07c §10](07c-e0-e2-checks.md) projected 16.5 minutes, superseded by **17 minutes in §10.1** at base 1200. Use that latest projection as the baseline to refit. These are **simulated elapsed minutes at Standard 1×** (one game year per minute), not headless runtime or wall-clock time at another speed. No new duration band is approved; the oracle must fit costs and propose one before accepting the Year-96 cap. End evaluation occurs at season end, with the same deterministic tie ordering as Bronze. Headless omits the human clause. One mode governs the whole match; advancement and effects remain per civilization. A Bronze save never becomes Iron implicitly. A rules-version field must be persisted in a new versioned envelope without changing the bytes/hashes produced for legacy Bronze runs.

The chapter **does not play E5 after KEY-5**. Feudal Order is its final threshold; there is no post-KEY-5 epilogue system beyond the countdown for other civilizations. Dominion victory retains its existing rule unless the user explicitly chooses otherwise (§10). A future full-era match is separate scope.

## 4. Iron geography and fuel

Use E3's existing iron deposits and MET-08 bloomery output first. Audit 10,000 existing seeds for reachable known iron, mineability, camp capacity, wood regeneration and import alternatives before proposing new placement. E3 world generation and its golden must stay byte-identical. If existing generation is insufficient, an Iron-only, separately seeded deterministic placement overlay is a later proposal; it must not alter any E0–E3 trajectory and cannot be activated merely because the first rival reaches Iron.

The proposed guarantee is **a viable iron route for every start**, not equal ore at every start: reachable mine or contact-based trade, with the denominator including landlocked/low-wood starts. Its exact distance/stock bound awaits the audit. Tin remains useful for stocked bronze tools and trade; Iron does not delete the E3 economy.

Chain: existing iron ore → existing bloomery bloom (`G-IRON`) → forged iron (`G-WROUGHT`) → T11 tools/iron arms; carbon-rich charcoal plus forged iron → steel edge stock → T12 tools/tempered arms. “Steel” is a game material for carburized, quenched edges, not a blast furnace or industrial bulk steel. No coal, coke or free metal conversion is introduced.

T11 improves availability and versatility more than raw output; T12 doubles T11 service life at extra processing cost. Saws require T12-quality edges. Kit performance follows the equipped item, not a civilization-wide research multiplier. A prosperous capital cannot grant remote settlements iron tools without a delivered kit or input chain. Imports and existing kits remain useful if local ore or fuel is interrupted.

Every production, transport, upkeep and upgrade consumes ledgered inputs and labor. No input exists twice after an upgrade, capture or shipment. Count mixed-age kit wear when measuring dormancy; log first fallback, coverage distribution and full exhaustion separately. Charcoal retains its E3 recipe. WOD-04 remains a meaningful fuel answer; water wheels save mechanical labor, not thermodynamic fuel by default. The recipe hand check in 26 shows why fuel is a central risk, not an assumed benefit of iron.

## 5. Cities, food, population and labor

The proposed E4 economy remains **one simulated person per figure**, pending explicit approval because the old era catalog says 1:2. Representation is not population growth: changing a display ratio must never double adults, births, Insight, labor or food need. If 1:2 presentation is wanted, aggregate only the view with stable selection semantics. A demographic conversion is not authorized.

Iron shares and scythes reduce field labor; terraces and heavy ploughs add productive sites, not free food. Orchards, pressing and mills change outputs only when their inputs/jobs exist. Grain supply redistributes physical stores under a capacity limit and protects donors' winter reserve. Aqueducts raise a separate water-limited city capacity; they do not multiply food or housing. Baths reduce the density disease term rather than every cause of death. Food-cap values are oracle targets, never extra runtime multipliers piled on top of the mechanisms.

No automatic E4 fertility jump, minimum population, protection for the losing side or catch-up grant. Retain live E3 demography at entry until E4 effects and their targets have been fitted; old era-row mortality is an historical target, not a switch to apply atop health projects. Report births, deaths by cause, adult labor, food use, stores, fission cohorts and city concentration by exposure from own KEY-4. Do not tune only at chapter end, where early leaders get longer exposure.

Military and administrative jobs compete with food, charcoal and construction. “Available soldier/physician/governor slots” and “staffed roles” are separate frame values. Food emergencies may suspend optional roles under the same deterministic planner rules for humans and AI; an empty academy produces no Insight.

## 6. The state layer and Feudal Order

E3 Cohesion, Admin span, Overreach, capture penalties and secession remain the baseline. E4 adds physically staffed administration and connected provinces. Census gives an administrator slot; a provincial seat governs nearby connected settlements. Province assignment is stable, uses known own geography, and is rebuilt only at season cadence. Captured/unreachable seats cannot project free administration. Citizenship and relay networks reduce distance strain; they do not remove food, conquest or war stress.

**Feudal Contract proposal:** delegated provinces receive local defense and relief from distance strain, with less central extraction/Insight (§26 constants). The catalog's absolute “remote regions don't secede” is not silently implemented: proposed protection removes the **distance/span contribution** for an active contract but preserves famine, conquest and other crisis secession. The user must choose between this interpretation and literal immunity. Contracts are internal administration, distinct from DIP-06's treaty between sovereign civilizations.

Republic is a research institution, not a mutually exclusive government selection unless separately requested. It raises Commons Insight and Cohesion while delaying voluntary war declarations; it cannot prevent immediate defense/relief. Organized Faith spreads through actual routes with capped influence and conversion progress; missionary jobs cost labor. Faith does not expose fog or transfer ownership instantly. See 26 for deterministic spread and limits. All institution benefits stop when their relevant local staff/structure/connection is absent where specified.

KEY-5 recognizes the combined countryside/state/faith/defense path already in the catalog. It awards the normal E4 keystone score and enters the E5 threshold; it grants no E5 population scaling or free castles.

## 7. Siege, armies and naval conflict

E3 stone walls remain unassaultable to forces without E4 engines, even when another civilization has entered E4. WAR-09 unlocks construction; it does not make every levy an engine. A supplied engine train with crew must arrive, then reduce barrier integrity over seasons. On breach, the existing assault resolver evaluates the reduced barrier; casualties, militia, shields, home/site and relief still matter. An unbreached wall never becomes a normal assault just because an Iron project exists.

Towers increase the intact wall multiplier from 3 to 4.5; castles are local strongpoints and claim anchors. Frontier walls protect segments actually crossed, never an entire realm by knowing the project. Gatehouses allow friendly trade during war only if the route remains physically open; investment/blockade still interrupts it. Professionally staffed armies receive training, use real arms and draw real rations; the “army size ×2” text becomes a capacity ceiling, not two soldiers per citizen.

Investment remains an alternative when engines are absent. Carry/supply rules, relief, sallies, recall, negotiated peace, conquest and secession all need end-to-end tests. Siege time −60% is a **catalog performance intention** to test against a controlled matched investment, not a random 0.4 multiplier on all capitulation timers. Engine cost and breach time in 26 are provisional; do not claim that they satisfy it until the oracle and live trials agree.

War galleys add a bounded naval engagement and harbor blockade, not tactical naval control. A blockade requires a surviving ship, crew, declared war and continuous presence at a known harbor; it blocks that harbor's maritime route only. Relief/escort and shipwreck loss remain possible. A ship cannot simultaneously blockade two ports or carry its crew as civilian workers. This substantial scope is an approval item, not a cosmetic “naval combat” effect.

## 8. Roads, trade, diplomacy and knowledge

Roads and bridges spend materials/labor per tile and carry movement and adoption only over completed connected segments. They cannot reveal an unseen rival. Periplus reduces route handling cost; coinage reduces economic friction but is not a minted-gold money simulation. Long routes must pay travel, cargo, spoilage, interception and plague costs; barrels reduce spoilage in transit, not all food loss. A harbor doubles eligible ship capacity once and increases its city's food access potential without generating food.

DIP-06 tributary treaties use material/strategic expected value, never score rank. Proposed terms transfer a fixed share of actual positive surplus and assign a **25% share of tributary Dominion flow** to the overlord while removing the same flow from the tributary. This conservation reading avoids duplicate Legacy; whether tributary land counts toward Dominion *victory* requires approval (§10). No nested or cyclic tributary chains.

DIP-07 leagues are voluntary, bounded to existing civilizations. Shared Wonder labor is delivered labor/material contributions, not multiplied output; ownership and score attribution are explicit in 26. Treaties reuse existing command/forecast conventions; any new command numbers are append-only. All automatic offers read belief state. No new negotiation screen is assumed.

Alphabet, academies and philosophy increase Insight through distinct sources. Scholar output “6” is ambiguous beside the calibrated E3 role output, so 26 makes the unit and multiplier explicit as a proposal. Great Library diffusion remains capped by the existing global discount cap; it cannot make costs zero. Charting reveals only coast already observed/shared under the same visibility rules; “known world” is not omniscience.

## 9. Legacy

Extend [22 §3.3](22-bronze-age.md#33-legacy-units-per-era) without rescaling earlier eras: `L(4)=5`; E4 completion Mastery **10**, ordinary First **15**, KEY-5 or Wonder First **60**, constructed E4 Wonder **125**. Researching a Wonder and building it are distinct events. KEY-5 is an E4 project, not scored at E5's scale. Great Dome is an unlock inside BLD-11, not an invented extra research node; its construction score/uniqueness needs the same explicit registry rule as Library/Lighthouse, and does not grant a second First for BLD-11.

Dominion continues the existing flow from claimed land. Record controlled land, scored land and tributary transfer separately. No double counting by treaty chains or league membership. Proposed chapter-end distribution is a **diagnostic aspiration** near 45–55 / 25–35 / 12–20 / 0–10 percent (Dominion/Mastery/Firsts/Wonders), not a new acceptance test until fitted and signed off. The longer chapter may make Dominion dominate even without any new claim radius; do not solve that by quietly rescaling E3 points.

## 10. Territory and Dominion timing

Re-anchor on live own-KEY-4 trajectories after every E3 rebase. The historical model of map fill in E5–E7 and Dominion around minute 120 is not a basis for E4 gates. Measure expansion fronts, actual border contact, neutral/Free Folk claims, city food catchments and capture separately. Do not add a new global radius increase at KEY-4; preserve existing Bronze-threshold raw-era lookups identified in §2.2. Specific roads, castles and provinces supply bounded E4 additions in 26.

Existing Dominion victory requires ≥50% of all habitable land, ≥2× runner-up, held for five years. Preserve it in Bronze; E4 may make it reachable. Proposal: retain it for direct ownership in IronChapter, with tributary transfers affecting Legacy only. This needs approval because the DIP-06 prose says “Dominion” without distinguishing flow and victory. Report early Dominion terminations separately from KEY-5/cap outcomes. Never discard them to improve keystone timing statistics.

## 11. AI and forecast design

One rules source must feed live and forecast arithmetic. E4 planning needs four concurrent goals: an affordable local metal replacement chain; enough civilian food and building labor; connected administration/transport; and a feasible KEY-5 closure with useful breadth. No rank-based research bonus or hidden resource knowledge.

| Planner | Required capability | Evidence exposed |
|---|---|---|
| Settlement | Compare delivered kit cost/service; reserve essential food, defense and replacement work; staff roles only when sustainable | Requested/assigned work, input shortages, completion ETA, active versus queued facilities |
| Spark | Forecast KEY-5 and breadth; choose land/sea closure alternative by observed feasibility; value use-gated tech by likely access | Closure blockers, population/Insight trajectory, uncensored ETA and uncertainty |
| Strategist | Supply engines, assess wall integrity/relief, choose investment/assault/peace, protect grain and fuel routes | Expected losses, stores, crew, travel, counter-exposure, launch/refusal reason |
| Diplomacy | Evaluate iron/grain trades, tributary terms and league contributions without score-rank assistance | Own cost, believed counterparty benefit, treaty duration/termination |
| Difficulties | Shared legal actions and economy; different planning quality | A1–A8 and B31 measured again in the Iron mode, without changing M10 protocols |

New blunders: engine research mistaken for a built engine; launch without crew/supply; starving donors for public grain; replacing every intact bronze kit at once; idle staffed institutions; treating queued roads/walls as complete; forecasting across an unknown blocked route; infinite declined treaty retry. Maintain A8 fuzz tests for unseen deposits, fleets, stockpiles, faith and governors.

## 12. View layer

Build views last, from frame data and commands. Add an opt-in IronChapter choice only after activation approval. Show the mode in saves/end screens. E4's visual read is dark iron and steel edges, forge smoke, road/bridge networks, aqueducts and dense city roofs, academies, garrisons and slow engine trains.

Required cards: tool coverage and replacement inputs; mine/forge/fuel bottlenecks; grain donors/deliveries and reserve; water and sanitation; province membership/staff/distance strain; siege integrity, crew, supply, breach ETA and relief; naval blockade route; tributary transfer and league contribution; KEY-5 countdown. Each research completion gets a Moment plus an honest visible outcome within a season: built/working, queued, or dormant with a reason. Research does not fabricate a completed structure merely to satisfy a visual gate. Terrain/resources/foreign cards obey fog; the UI never feeds back into sim state.

## 13. Provisional balance targets and oracle work

Numbers marked P in 26 are hypotheses. The [2026-11-04 preliminary balance screen](balance/m11-iron-balance-screen-2026-11-04.md) contains initial live and fixture results; the observations do not by themselves fit or approve targets. The existing era-base 1200 is a catalog input for a first oracle run, not a fitted cost. The following remain proposed screens to approve, then fit, except where approval is recorded in a linked report; they do not amend any B1–B31 threshold.

| ID | Proposed target / diagnostic | Required measurement |
|---|---|---|
| I-T1 | Own KEY-4→KEY-5: refit the current 17-minute Standard 1× projection; acceptance band TBD; candidate cap rate ≤10% | Full starts and own-entry exposures; 80+ matches, held-out block; show censored/captured/eliminated seats |
| I-T2 | At least 70% of tool workers equipped T11+ within 12 seasons of a sustainable route | Source-backed, per-settlement coverage for civilization-controlled settlements; censor Free Folk and captured/owner-changed sites because they are outside the civilization's kit network; report access research, delivery and mixed-age upgrade separately |
| I-T3 | T12 lifetime exactly 2× T11; no simultaneous kit/project double multiplier | Deterministic fixture plus live wear distribution |
| I-T4 | FoodCap on a matched E3 anchor rises 1.4–1.8×; city concentration rises without free food | Non-river, river, hill, inland and coastal fixtures; physical mass balance |
| I-T5 | Engine assault materially beats matched investment; test catalog's 60% time reduction | Supplied isolated wall/tower/castle trials and live relief/attrition; no claimed pass from formula alone |
| I-T6 | **Prior candidate passed; current refitted source still open:** full-chain work ≤15% in every mature rolling 12-season window; zero post-MET-09 Iron-attributed 5 L clearing crossings; fuel-short rate ≤5% and unmet planned charcoal ≤1% | The fourth candidate passed its preregistered 80-match block (280 mature work-evaluable seats) and 8-seed low-wood, no-Coppicing, GOV-04, import-loss and kit-wear screens; one late-route fuel sample is censored, not a pass. See the [whole-match acceptance and approved clearing/fuel protocol](balance/m11-it6-whole-match-2026-11-05.md). After pacing refits, the v45 block failed its approved fuel cutoff at seed `20263667`: the plan assumed full Iron production at a site with no harvestable wood, although productive supply existed elsewhere outside the local fuel catchment. The v47 reachable-wood demand cap clears that target replay and the fixed 13-seed diagnostic screen. Its fresh 80-match acceptance then stopped at an I-T2 coverage miss in seed `20264334`; all qualified I-T6 cutoffs passed in the 42 natural matches completed before the stop. The failure and follow-up are recorded in the [B4-06 record](balance/m11-b4-06-metal-fuel-acceptance-2026-11-06.md); the gate remains open. |
| I-T7 | Admin keeps normal settled cores near E3 Cohesion bands while crisis secession remains possible | Per-season exposure, remote/captured settlements, count incomplete exposure and cascade rate |
| I-T8 | Map fill, Dominion share and City share | **Diagnostic only** until current E3 trajectories are re-anchored; no guessed E4 population/land acceptance band |

Oracle extensions: metal/fuel mass balance and wear cohorts; food/city/demography live mode; route transport and local resource access; siege/relief/supply; state/distance/faith; score/treaty flow; research closure and cadence. Match the C# equations and fixed-point rounding. Log inputs, baseline commit, seed sets, content hash, mode/version and every candidate parameter. Small screens select hypotheses; ≥80 matches or sufficient qualifying cases establish noisy outcomes. Never change a target after seeing a miss without user sign-off.

## 14. Proposed M11 acceptance series (B4-xx)

The new prefix distinguishes **E4** gates from M10 B1–B31. All rows remain proposed and unapproved. The [2026-11-04 screening report](balance/m11-iron-balance-screen-2026-11-04.md) records preliminary observations but does not complete the proposed acceptance protocols; approval still fixes thresholds and protocols. Existing gates retain their exact identifiers, thresholds and protocols. A larger robustness run supplements rather than silently replaces an official M10 cohort.

| Gate | Exit condition / proposed protocol |
|---|---|
| B4-01 Isolation | Existing toy/worldgen/research/politics/bronze golden files and default-run checksums byte-identical; E4 hidden/inert before own KEY-4 and throughout Bronze mode, even with numeric E4 pack; no E0–E3 YAML changes |
| B4-02 Content completeness | Approved scope count reconciled, every node has typed effects/cost/requirements/practice/Moment/visible change; strict validator clean; all registry references resolve |
| B4-03 Determinism | 1,000 Iron matches replay exactly, snapshot continuation exact, macOS arm64/Windows x64 same iron-v1; zero crashes/invalid states |
| B4-04 Mode/end correctness | Unit and replay cases for all three end clauses, ties, human/headless, KEY-4 continuation, KEY-5 threshold, old-save Bronze mode and mode-bearing new saves |
| B4-05 Pacing | I-T1 after oracle fit and user-approved band; no imputation of uncompleted KEY-5; publish cap and Dominion endings |
| B4-06 Metal and fuel | I-T2/I-T3/I-T6; exact input/output conservation; imported-kit, mine-loss, fuel-loss, captured-forge, mixed-wear and upgrade cases |
| B4-07 Food and health | I-T4; no mass creation; cause-specific hazard targets fitted before approval; food donor reserve never violated by grain jobs |
| B4-08 Siege | No E3 wall assault; engine/crew/supply necessary; fixed-ratio combat tables within approved oracle tolerance (candidate ±10 points); investment, breach, sally, relief, repair, recall and capture replays |
| B4-09 State | Deterministic province assignment; no unstaffed/isolated benefit; famine/conquest crisis still works under selected contract rule; no treaty/faith ownership or score duplication |
| B4-10 World/territory | Existing generator unchanged; 10,000-seed iron-route audit; separately approved new geography only if required; own-era exposure and actual border contacts reported |
| B4-11 AI quality | Iron-mode A1–A8 and Legend ≥60% against Warlord, same meaning as existing targets; ≥80 mirrored duels for noisy conclusions and ≥120 rotated FFA matches plus held-out cohort; sparse event gates remain incomplete |
| B4-12 Forecast/blunders | KEY-5 ETA within 15% for ≥90% of uncensored predictions, median ≤6% (candidate); 100% resolved or explicitly censored; zero unsupplied siege/suicide launch blunders; defense completion distinguished from queues |
| B4-13 Performance | Mean tick ≤4 ms and GC ≤16 ms over ten minutes; render 60 fps at actual E4 p95 peak population; candidate full Iron chapter ≤45 s headless. Approve hardware/figure budget after population fit |
| B4-14 Human comprehension | ≥5/8 uncoached players explain a metal bottleneck, distinguish investment/breach, explain one institution/AI decision and want to continue; preserve three-slot comprehension; moderator protocol approved before testing |
| B4-15 Integration | Full Release suite and strict validation green; E3 acceptance owners confirm compatibility; all golden changes isolated/explained; user approves merge |

## 15. Risks and unresolved decisions

| Review ID | Question / recommendation | Why sign-off matters |
|---|---|---|
| Q1 | Is scope 56 regular + KEY-5, or are projects missing from the brief's 59? Recommend actual catalog scope pending identification | Completeness and validator counts |
| Q2 | Approve default-off IronChapter through KEY-5, provisional Year 96/+12 seasons? Keep default Bronze | Ending/acceptance semantics cannot change silently |
| Q3 | Keep simulation 1:1, optionally aggregate E4 presentation 1:2? | Catalog has 1:2 at E4 and KEY-5 prose 1:5; unreviewed scaling corrupts economy |
| Q4 | Do contracts remove distance strain rather than grant absolute secession immunity? **Resolved 2026-11-03: yes, distance and span pressure only; famine, conquest and other crisis secession remain.** | Explicit narrowing of existing prose |
| Q5 | Does tributary Dominion transfer flow only, conserving points, and exclude victory land? **Resolved 2026-11-03: yes, points move once and never count as land.** | Prevents duplicated score and early automatic domination |
| Q6 | Include full bounded naval blockade, league co-building and missionary influence now? **Resolved 2026-11-03: yes. The user asked for the entire Iron Age to be complete and working as a whole before playtesting; no catalog promise is deferred.** | All are catalog promises, each adds state/AI/tests; deferral requires explicit scope change |
| Q7 | Make Great Dome buildable in E4 or retain “built later”? Proposal: unlock only in E4, construction deferred unless approved. **Resolved 2026-11-03: buildable in E4, as a normal Wonder with its own cost (to be set with the other P values).** | Otherwise a new Wonder is silently introduced through an M-type project |
| Q8 | Confirm P numeric baselines, new B4 protocols/performance budget and a pacing band fitted around the latest 17-minute Standard projection before implementation | No values are oracle-fitted; no gates have passed |
| Q9 | If existing iron geography cannot support all starts, approve a mode-isolated overlay or accept trade dependence? **Resolved 2026-11-03: accept trade/expansion for the 0.68% tail; no overlay** ([WP4 audit](balance/m11-wp4-iron-geography.md)). | Shared worldgen changes would move E3 goldens |

Fuel may make cheap iron economically worse than tin bronze; I-T6 must catch that before promotion. City infrastructure may consume the same labor needed to build defense; B28's lesson applies twice. Stacked knowledge effects may collapse cadence; stacked distance reductions may trivialize state management. Road/faith/castle claim additions may make Dominion arrive much earlier than the old late-era model. Steel weapons plus professional training can multiply into an excessive military advantage; compare full force tables, not isolated +25% text. E3 rebases can invalidate every aggregate baseline.

There is **no automatic cut list** that removes promised catalog effects while declaring all nodes complete. Possible reductions (naval tactical detail, bespoke art, Great Dome construction) must be explicitly accepted; core metal, physical food/fuel, wall transition, state costs, visibility, determinism and compatibility cannot be substituted with prose-only unlocks.

## 16. Work breakdown after sign-off

| WP | Deliverable | Depends on | Exit |
|---|---|---|---|
| WP1 | Reconcile scope and approve 25/26, all Q decisions and gate protocols | This review | User sign-off; still no code beforehand |
| WP2 | Extend and fit Python oracles; refresh live E3 anchor after rebase | WP1 | Reproducible candidate targets, ledger checks and fit report |
| WP3 | Prepare E4 registries, type existing E4 draft rows in place, and add validator support; keep every E4 row draft until WP5 implements mode-aware live **and forecast** eligibility | WP2; WP5 isolation before promotion | B4-01/02; content-pack golden deliberately reviewed; no E0–E3 YAML changes |
| WP4 | Audit iron geography; implement an approved isolated mode solution only if needed | WP2 | B4-10; existing worldgen golden unchanged |
| WP5 | Rules-mode plumbing, research/forecast ceilings, KEY-5, scoring, saves and chapter end | WP3 schemas/drafts; isolation precedes numeric promotion | B4-01/04; old defaults and acceptance paths unchanged |
| WP6 | Forging, carbon/steel, tool wear, fuel and transport | WP3/4/5 | B4-06 |
| WP7 | Food, water, health, institutions/provinces/faith and physical roles | WP2/3/5/6 | B4-07/09 |
| WP8 | Professional forces, engines, fortifications, naval routes and treaties | WP6/7 | B4-08/09, conserved resource/score ledgers |
| WP9 | Forecast, Spark, Strategist, Council, new blunder telemetry | WP5–8 | B4-11/12; fog tests |
| WP10 | Frame/events and game presentation, last | Headless systems pass | B4-13 presentation; all nodes legible |
| WP11 | Iron corpus/golden, soak, cross-platform/performance and nightly reports | WP5–10 | B4-03/13; E3 regressions still exact |
| WP12 | Large-sample balance, held-out verification, human playtest | WP2–11 | B4-05/14/15; explicit merge authorization |

Shared-file changes later must be small and additive; new E4 systems belong in new files. Rebase at least daily and before merge, re-read 07d, and rerun compatibility. No push or merge is authorized by this draft. The next action is review of both documents, not simulation code.
