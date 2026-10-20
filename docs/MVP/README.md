# CivSim — MVP Design Documents

> A mostly hands-off civilization simulation from the **Paleolithic to post-modern times**. You choose what your people learn (three research projects at a time: Primary, Secondary, Tertiary), and hundreds of small autonomous figures live out the consequences on a shared world map, competing with rival peoples for land and for being first.

**Status:** design draft v0.1. **No implementation yet.**

## Reading order

| # | Document | What's in it |
|---|----------|--------------|
| 01 | [Game Structure](01-game-structure.md) | Vision, pillars, setting (grounded stylized realism), the player's levers, match flow, the 10 eras, scoring and victory, the three themes, MVP scope |
| 02 | [Research System Structure](02-research-structure.md) | Project anatomy and types, the 4:2:1 focus slots, Insight and costs, gates, Know-how (learning by doing), adoption across the map, keystones, Firsts and diffusion, the auto-pick Council |
| 03 | [Research Catalog: Overview](03-research-catalog.md) | 33 lineages at a glance, counts by era, theme and type, hub projects, gates, historical sources |
| 03a | [Catalog: CRAFT](03a-research-craft.md) | 11 lineages, 170 projects: stone, fire, wood, fiber, ceramics, building, mining, metal, machines, energy, chemistry |
| 03b | [Catalog: KIN](03b-research-kin.md) | 11 lineages, 161 projects: foraging, hunting, agriculture, husbandry, food, medicine, society, governance, belief, arts, learning |
| 03c | [Catalog: REACH](03c-research-reach.md) | 11 lineages, 178 projects plus the 10 Keystones: wayfinding, transport, seafaring, commerce, communication, number, claims, defense, warfare, diplomacy, science |
| 04 | [Research Paths](04-research-paths.md) | Keystone spine, critical paths, theme weave, full maps of E0–E2, keystone maps for E3–E9, method ladders across eras, validated strategy openings, forks |
| 05 | [Simulation Structure](05-simulation-structure.md) | World and worldgen, figures and representation scale, the AI hierarchy, method resolution, economy, population and fission, cohesion and overreach, territory, diplomacy and war, disease, pollution, the research↔sim interface, determinism, performance |
| 06 | [Next Steps](06-next-steps.md) | Status, decisions to review, remaining design workstreams, milestones, paper prototypes, acceptance criteria, risks, definition of done |
| 07 | [Balance Findings](07-balance-findings.md) | WS-A single-civ balance model: v0.1 pacing failures, the tuned constants (Insight scale, EraBase, Know-how, pace, fertility, fission, representation scale) and the change list applied to 01–06; workbook and tools in [balance/](balance/balance-model.xlsx) |
| 07b | [Territory Findings](07b-territory-findings.md) | WS-A part 2: 4-civ spatial model of claims, first contact, Dominion share and map fill; recommended start placement, map sizes, claim radii, ownership threshold and Dominion rules |
| 07c | [E0–E2 Checks](07c-e0-e2-checks.md) | 07b R6 adopted in the balance model (arc 173.5 min); 11 §6 checks C1–C5 against docs 10 and 11, with fixes: range and stock limits, late-winter share, hazard scaling, EraBase alignment |
| 08 | [UI & World Display](08-ui-and-world-display.md) | Camera and zoom levels, figure readability, the look of each era, territory and palette, overlays, the main HUD, focus bar and research-web wireframes, notifications, follow cam and Legends, onboarding, Director mode, the late-era generic visual kit |
| 09 | [Population & Health](09-population-and-health.md) | WS-H demography spec: cohorts, births and deaths per era, winter, the three population caps by project, fission, disease constants; feeds the balance model |
| 10 | [E0–E2 Actions & Goods](10-e0-e2-actions-and-goods.md) | Units, every method ladder for the first three eras (forage, hunt, fish, farm, herd, chop, knap, build, haul…), tool tiers, structures, warmth, goods registry, Know-how mapping |
| 11 | [E0–E2 Project Specs](11-e0-e2-project-specs.md) | All 145 Dawn–Roots projects with numeric effect primitives, costs, practice, innovator Moments and flavor; acceptance checks |
| 12 | [World Generation](12-world-generation.md) | Map sizes, pipeline, biomes and yields, water, fauna, deposits, paired start placement, guarantees, fairness pass |
| 13 | [Contact & Conflict](13-contact-and-conflict.md) | Scouting and awareness, first contact, relations, gifts and barter, raids, defense, combat numbers, war and settlement capture |
| 14 | [AI Opponents](14-ai-opponents.md) | The smart-AI design: forecast-driven research planning, opponent reading under fog, war/raid/diplomacy intelligence, difficulty without cheats, the AI harness and its acceptance targets |
| 15 | [Architecture Decision](15-architecture-decision.md) | Requirements, questions and answers, options compared, the recommended stack (Godot 4 .NET + a deterministic C# sim library), visual style, determinism rules, threading, saves and replays, repo layout, risks |
| 16 | [Catalog Format & Tooling](16-catalog-format-and-tooling.md) | YAML schema for projects and registries, content pack and hash, the 18-check validator and CLI, migration from markdown, golden numbers |
| 17 | [Vertical Slice](17-vertical-slice.md) | First-playable scope (E0–E2, one player vs. three AI), what is stubbed or cut, milestones M0–M9 with gates, the consolidated acceptance checklist, a demo script |
| 18 | [View Layer](18-view-layer.md) | The sim-to-renderer contract: frame fields and units, fog and memory for the viewer, figure layout, the lock-free hand-off and interpolation, the event stream and text keys, player commands, budgets, using it from Godot |

## The game in ten lines
1. Every player starts as a band of **12 people** in the Paleolithic.
2. The only constant decision: which **3 research projects** to pursue, ranked **Primary (4/7 of Insight), Secondary (2/7), Tertiary (1/7)**.
3. **509 projects in 33 lineages** across three themes: **CRAFT** (output efficiency), **KIN** (people and cohesion), **REACH** (territory and contact).
4. Projects add rungs to **method ladders**, so figures do the same jobs ever more efficiently, from pebble chopper to mechanized harvester.
5. People **practise** what they do, which makes related research cheaper. Geography shapes the tree.
6. Groups that outgrow their social cap **fission** and found new camps. That is how territory spreads.
7. Territory is a **claim field**. Holding land over time scores **Dominion**.
8. Rival peoples (human or AI) meet, trade, ally, raid and war, all autonomously, steered by research, **Beacons** and **Posture**.
9. Ten eras, **Dawn → Information**, each entered by a **Keystone** project. A Standard match is about 3 hours; Quick is about 90 minutes.
10. Win by **Legacy** (Dominion + Mastery + Firsts + Wonders), by **Dominion** (50% of the world for 5 years), or by the **Apex** (*Planetary Network*).

## Glossary

| Term | Meaning |
|------|---------|
| **Spark** | The player's role: their people's collective curiosity |
| **Focus slots** | Primary / Secondary / Tertiary research slots (4 : 2 : 1 Insight split) |
| **Insight** | Research currency produced by people, specialists and institutions |
| **Know-how** | Per-lineage pool built by practising related activities; converts into research progress |
| **Theme** | CRAFT, KIN or REACH |
| **Lineage** | A line of development within a theme (e.g. MET Metallurgy) |
| **Method ladder** | Ordered ways to perform an action; research adds rungs |
| **Keystone** | Era-transition project (KEY-1 … KEY-9) and the Apex (KEY-10) |
| **Breadth** | Number of current-era projects a Keystone requires |
| **Gate** | World condition on a project: `[Seen]` blocks research; `[Has]`, `[Near]` and `[Climate]` affect use |
| **Glimpsed** | A project visible in silhouette because it is one step from available |
| **Adoption** | Spread of a new method from the innovator to the rest of the population |
| **Fission** | An over-cap group splitting off to found a new settlement |
| **Cohesion** | How well a settlement holds together; modulates the group cap, work and secession |
| **Overreach** | Cohesion drain from distance to the capital |
| **Claim** | A civilization's strength of hold on a tile; the highest claim (with margin) owns it |
| **Dominion** | Score from owned habitable land over time |
| **Legacy** | Overall score: Dominion + Mastery + Firsts + Wonders |
| **Firsts** | Bonus for being first in the world to complete a project |
| **Diffusion** | Cost discount for projects already known by civilizations you've contacted |
| **Beacon** | Player-placed marker that biases expansion and scouting |
| **Posture** | Civilization-wide stance: Expand / Prosper / Fortify / Raid |
| **Council of Elders** | Auto-pick that fills empty slots using the player's Leaning |
| **Civ Mind / Settlement Planner** | The AI layers that run every civilization below the research level |
| **Representation scale** | How many people one figure stands for (1 → 500 across eras) |
| **Free Folk** | Neutral peoples formed when settlements secede |
| **Legend** | A notable individual surfaced by the simulation and named in the Chronicle |
