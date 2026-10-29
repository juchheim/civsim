# "Your people" sheet: tabs for Stores, Bottlenecks and Research Payoff

Design proposal, read-only investigation of the working tree as of 2026-10-29 (including uncommitted edits to
`FrameBuilder.cs`, `MatchFrame.cs`, `LivingEconomyModel.Belief.cs`). Paths are repo-relative; line numbers are current.

---

## 0. TL;DR

* The People window (`game/Scripts/Hud/Game.People.cs`) today shows only head counts by job, a few need chips, Know-how
  and scouting limits. **The frame carries no stockpile, no rate, no cap and no ledger at all**: food is a 4-step band
  (`MatchFrame.SettlementFood`, `MatchFrame.cs:127-130`), nothing else about stores reaches the UI.
* The economy is **seasonal**: production, eating, spoilage, storage trim, building and births all happen in one tick at
  the season boundary (`LivingEconomyModel.EndSeason`, `LivingEconomyModel.cs:757-817`). Only hearth/fire fuel is burned
  per tick (`AdvanceCampFires`, `:699-755`). So every rate in this design is **per season**, and every "trend" is a
  per-season series. That is also why the window can be made exact cheaply: one snapshot per season explains everything.
* Proposal: turn the sheet into **4 tabs: Overview · Stores · Work · Research**, with a **scope selector** (All
  settlements / Settlement N) in the header shared by all tabs.
  * **Overview** answers "what is slowing us down?" with a ranked, rule-generated bottleneck list plus the three growth
    caps (Food / Housing / Group) per scope.
  * **Stores** is the resources tab: every good in stock, storage cap, last-season in/out, runway, 16-season sparkline,
    and a food waterfall (start → gathered by source → eaten → spoiled → lost to full stores → end).
  * **Work** holds the current People content (job counts, scouting limits, needs) plus a labor-split bar and
    tool/land limits.
  * **Research** answers "is it paying off?": in-progress slots with projected effect at today's flows, and every
    learned project with a status chip (Paying off / Partly blocked / Blocked / Unused / Superseded / Too early / Not
    measured) and a quantified "would lose without it" value per season and cumulative.
* All numbers are computed **sim-side read-only** (deterministic, Fix64/ints, under SourceRulesTests), cached once per
  season in `FrameBuilder`, copied into frames only when their version changes. Diagnosis rules live in
  `CivSim.Sim/View` and emit **codes + int params**; Godot maps codes to text (the sim never writes prose, as with
  `ChronicleKey`).
* New economy state is **diagnostic only**: not hashed, not snapshotted, never read by decisions, so **goldens and
  replays are unchanged**. Precedents exist: `_lastSeasonReachableFoodStores` / `_lastSeasonFoodNeed`
  (`LivingEconomyModel.cs:251-254`, "diagnostic-only state") and `CivicState.Trace*` (`LivingEconomyModel.Civic.cs:88`).
* History (trend ring) and cumulative research payoff live in `FrameBuilder` view state, saved by `WriteState`
  (`FrameBuilder.State.cs`, `StateVersion` 4 → 5; v4 saves load with empty history).
* Phase 1 = Stores tab + Overview bottlenecks + research usage status. **Phase 2 completes the core ask** (quantified
  research payoff, labor/tool/land limits, build blockers). Phase 3 = levers, depletion, polish.

---

## 1. What exists today

### 1.1 The window

| Aspect | Today | Source |
|---|---|---|
| Open | Top-bar people icon (`Game.TopBar.cs:48`), research web button "People & needs" (`Game.ResearchWeb.cs:228`), `--ui-open=people` (`Game.Hud.cs:137`) | |
| Shell | Full-rect scrim + centered `SheetPanel` 1100 × 705 (clamped to window − 2·Margin) | `Game.People.cs:55-71, 257-271` |
| Header | "Your people" (Title) + date caption + close icon | `:72-80` |
| Metric cards | 4 `InnerCard`s: YOUR PEOPLE, SETTLEMENTS, RESEARCH SPEED, AGE PROGRESS (Eyebrow + NumTitle + Caption) | `:82-101` |
| Body | Two `WellPanel`s at 0.618 / 0.382: left = 12 job bars, scouting limits (settlement `OptionButton` + Auto/0/1/2 chips), other-activity chips; right = research context, role chips, top-3 Know-how bars, needs chips | `:103-214` |
| Footer | caption + Close / "Back to Research" | `:216-221` |
| Refresh | `RefreshHud` calls `RefreshPeoplePanel(frame)` on **every new frame even when closed** (frames every 10 ticks) | `Game.Hud.cs:186-197`, `SimulationSession.cs:23` |
| Data | Loops `frame.Figure*` for counts/flags; settlement bands for Food short / Winter risk / Strained; `FocusKnowHow` | `Game.People.cs:303-416` |

Visual vocabulary available (reuse, don't invent): `UiSegmented` tabs (`UiKit.cs:203-249`, used for Help tabs
`Game.Modals.cs:176` with `_helpPanes[]` toggled), `UiProgressBar` (`UiKit.cs:86-128`), `UiKit.Chip`,
`InteractiveChip`, `UiRowButton` (pooled list row, `UiRowButton.cs`), `UiProjectCard` (Card/Row/Chip modes),
`UiPopover`, `UiIcon` glyphs (`Warning`, `Danger`, `Info`, `Check`, `Spring..Winter`, `Hut`, `People`), tokens
`Success #5FC49B`, `Warning #F57F3A`, `Danger #F2786A`, `Info #9FC3E0`, `Accent #E9BE4E` (`UiTokens.cs`), label styles
`Eyebrow`, `EyebrowTertiary`, `Caption`, `CaptionTertiary`, `CaptionWarning`, `CaptionSuccess`, `Num`, `NumStrong`,
`NumTitle` (`UiTheme.cs:101-122`). Missing: a sparkline, a stacked/waterfall bar, a `CaptionDanger` style.

### 1.2 Frame data that already exists and is reused

| Need | Field | Notes |
|---|---|---|
| Job head counts, age, roles, flags (hungry, cold, ill, wounded, away, warrior) | `Figure*` | `MatchFrame.cs:65-94` |
| Tool fallbacks | `FigureToolRung`, `FigureKnownToolRung`, `FigureMethodEfficiencyPermille`, `FigureMethodFallback` (`MethodFallbackReason`: MissingTool, HuntParty, NoTin, NoArsenic, NoOre, NoFuel), `FigureMethodFallbackTool` | from `MethodReadout`, `LivingEconomyModel.View.cs:133-194` |
| Food bands | `SettlementFood`, `SettlementWinterForecast` (bands from `FoodBandOf`, `FrameBuilder.cs:990`) | exact values are not exposed |
| Cohesion | `SettlementCohesion` band, own `SettlementOverreach`, `SettlementSpanPenalty` | |
| Flags | `FireLit`, `FissionPending`, `Corvee`, `LevyTarget`, `Own`, ... | `FrameBuilder.cs:958-979` |
| Build | `SettlementBuildKind`, `SettlementBuildPermille`, `SettlementStructures`, `Halls`, `Temples`, `Ziggurats` | |
| Livestock | `SettlementSmallStock/Cattle/Pigs` | |
| Scouting | `SettlementScoutPolicy` | |
| Trade | `Route*` (good, last delivery volume, deliveries) | `MatchFrame.cs:240-251` |
| Civ | `CivPopulation`, `CivSettlements`, `CivInsightPerYear`, `CivProjectsDone`, Mandate | |
| Research | `FocusSlotProject/Permille`, `FocusQueue`, `FocusProjectState[]`, `FocusKnowHow[]`, Mandate/Decree fields | `MatchFrame.cs:287-316` |
| Events | `Births`, `Deaths` (by cause), `ProjectCompleted`, `FamineRisk`, `FireOut`, `StructureBuilt` | `ViewTypes.cs:177-268` |

### 1.3 Simulation data that exists but is not exposed

| Data | Where | Cost to read |
|---|---|---|
| Every good in stock (41 E0–E2 goods + Bronze metal goods in one slot space) | `Stores[]`, `MetalState.Goods[]`; unified `SlotCount`, `SlotOf`, `Stock(s, slot)` (`LivingEconomyModel.Metal.cs:21, 106-116`); public `GoodStock(i, id)` (`Bronze.cs:499`) | trivial |
| Food stores in rations | `FoodStores(i)` (`LivingEconomyModel.cs:493, 3861`) | trivial |
| Food storage cap = 12 + 15·pits + 80·granaries rations | inline in `SpoilFood` (`:2744`) | trivial (needs accessor) |
| Exact last-season food security, late-winter forecast | `SettlementLite` (`LivingEconomyModel.Politics.cs:62`) | trivial |
| Season-start stores, season need, stores before eating | `LastSeasonReachableFoodStores`, `LastSeasonFoodNeed`, `LastSeasonFoodStoresBeforeConsumption` (`:387-401`) | trivial, already diagnostic |
| The three caps + lean cap | `Settlement(i)` → `SettlementView` FoodCap, HousingCap, GroupCap, EffectivePopulationCap, LeanFoodCapacity (`:444-457`) | several O(figures) loops per settlement; **once per season only** |
| Sustainable annual food income by source | `SustainableFoodIncomeBreakdown(i)` (`:536-543`) | once per season |
| Birth brake factors | crowding = (pop / min(FoodCap, HousingCap))^6 (`:2857-2863`), prior FS, `FertilityHealthFactor` (`:2904`, private) | once per season |
| Food need next season | `FoodNeed(s, winter)` (`:3891`, private) | O(figures) |
| Fuel draw | hearth 1.5 wood/season, 3 in winter; unattended fire 1/season (`:56-58`); E3 `E3FuelNeed` (`Food.cs:259`) | trivial |
| Method readout per job | `MethodReadout`, `MethodRung` | already used |
| Metal economy | `Metal(i)` readout, `MetalOutputRate(civ)` last season ×4 (`Bronze.cs:305-415`) | trivial |
| Civic | `Civic(i)` → tier, roles, outbreak, sanitation (`Civic.cs:1400`) | trivial |
| Research | `CompletedTick(civ,p)`, `AdoptionFraction(civ,p)`, `EffectiveCost`, `CommonsPerYear`, `InsightPerYear`, `ModifierMultiplier` (`ResearchSystem.cs:156-235`) | `AdoptionFraction` loops 128 settlements: once per season |
| Deaths by civ × cause, births by civ (last season) | already tallied in builder: `_deathsByCivCause`, `_birthsByCiv` (`FrameBuilder.Events.cs:419-451`) | free |

### 1.4 Data that does not exist and must be computed sim-side

| Data | Why it is needed | Where to capture |
|---|---|---|
| Per-season food ledger (gathered, eaten, spoiled, discarded over cap, other) | waterfall, runway, storage-cap callout | three snapshots inside `EndSeason`/`SpoilFood` |
| Per-good flow (produced net of crafting, used by building, fuel, other) for materials | wood/stone runway, "builders waiting on wood" | stores snapshots at phase boundaries + a fuel accumulator in `AdvanceCampFires` |
| Per action (forage, hunt, fish, farm, chop, gather, herd): work assigned, work applied after tools, output requested, output harvested | "tool-limited", "land-limited", yield per worker-season, research attribution | `PlanAndWork` (`:1306-1633`); `Harvest` returns harvested vs requested (`:1958-1983`); `LimitActionWorkByTool` returns applied work (`:4277-4295`) |
| Labor split and the food-share rule that fired (0.85 / 0.70 / 0.48 / 0.9 / city cap) | "almost all hands on food" | `PlanAndWork` `:1363-1384` |
| Build blocker (dwelling or store wanted, good short) | "builders waiting for wood: pit house needs 4, have 1.2" | new pure function mirroring `SelectBuildCandidate` (`:2434-2501`) |
| Expected hazard by cause **per civ** last season | deaths averted by hazard research | `RecordExpectedHazards` today sums all civs only (`:3143`) |
| Range stock vs capacity (forage, game, fish, wood) | depletion callout | read of `_forageStock` etc. over `RangeTiles` (Phase 3) |

---

## 2. Design principles

1. **Answer a decision, not a curiosity.** Every block maps to one of the player's real levers: research slots, the
   scout policy (in this window already), Beacons/Posture, and from KEY-3 Decrees (found village, expedition, mine,
   levy, corvée, compact). Labor allocation is automatic (`PlanAndWork`), so the UI explains it rather than offering
   sliders.
2. **Per season, said out loud.** Units: **R** = ration (one adult's food for a season), **ws** = worker-season
   (`docs/MVP/10` §1), goods in their registry unit. Rates are "last season" and "per season". Never "per minute".
3. **Exact where the sim is exact, labeled estimate where it is a counterfactual.** Prefix estimates with "≈" and give
   the formula in the tooltip.
4. **Own civilization only.** Every new block is filled only for settlements with `IsOwn` (`FrameBuilder.cs:317`), or
   for `FocusCiv` in omniscient mode. Rivals stay fogged.
5. **Never write sim state from the view.** In particular never call `HasBuildCandidate` (it sets `BuildTarget`,
   `:2379-2385`). `Modifier()` and `ReachableForageShare()` fill lazy derived caches; that is benign (same values the sim
   would compute) and happens on the sim thread, but new accessors should prefer cache-free paths.

---

## 3. Information architecture

### 3.1 Shell changes

```
┌ Your people ─────────────────────────────────────────────────────────────── [×] ┐
│ Year 4 · Autumn · last season's numbers                                         │
│ [ Overview | Stores | Work | Research ]          Showing: [All settlements  ▾ ] │
│─────────────────────────────────────────────────────────────────────────────────│
│  (tab pane, ScrollContainer, fills)                                             │
│─────────────────────────────────────────────────────────────────────────────────│
│ Numbers are for the last full season. ≈ marks estimates.             [ Close ]  │
└─────────────────────────────────────────────────────────────────────────────────┘
```

* Size: keep 1100 × 705 (`LayoutPeoplePanel`). The four metric cards move into Overview, which frees the header.
* Tabs: `UiSegmented(new[]{"Overview","Stores","Work","Research"}, 0, SelectPeopleTab, 110)`, same pattern as Help
  (`Game.Modals.cs:176`); panes are pre-built `Control`s toggled visible. Remember the last tab for the session. Keys:
  `1`–`4` while the sheet is the top layer; Tab/Shift+Tab already move focus.
* Scope: an `OptionButton` (`Showing: All settlements`, then `Settlement N · pop` rows, ascending settlement index) at
  the right of the tab row. It is the same list the scouting control builds (`RefreshPeopleScoutControls`,
  `Game.People.cs:418-448`); extract it into one `RebuildOwnSettlementOptions()` used by both. The scouting control in
  the Work tab follows the scope when a single settlement is chosen; with "All" it keeps its own selector.
* Deep links: `OpenPeoplePanel(PeopleTab tab = Last, int settlement = -1)`. Callers: top-bar people icon (Last), the
  research web button (Research), a future click on the food chip or a Food-warning toast (Stores, that settlement),
  tile card of an own settlement ("Details…", Overview scoped to it).
* Rename the research web button "People & needs" → "Your people". Top-bar tooltip: "Your people: stores, work and
  research payoff".
* Cost: gate `RefreshPeoplePanel` with `if (!_peopleOpen) return;` and refresh on open. Today it runs every frame
  while closed (`Game.Hud.cs:192`). Within the open sheet refresh only the visible tab, and refresh the per-season blocks
  only when `frame.EconVersion` changes (§7.4).

### 3.2 Tab set

| Tab | Question it answers | Why it is not thin |
|---|---|---|
| **Overview** | What is slowing us down, and where? Is the civilization growing, and what caps it? | metric cards + ranked bottlenecks + caps + wellbeing + last season's births/deaths |
| **Stores** | What do we have, how fast is it changing, how long will it last, where does food go? | ~10–25 goods rows from day one (food kinds, wood, stone, fiber, hide, bone, tools), food waterfall, sparklines |
| **Work** | Who does what, why is labor split this way, and where is labor wasted (tools, land, idleness)? | today's People content plus labor split and limits |
| **Research** | Is research paying off? What is in progress and what will it change? Which project would fix my bottleneck? | slots + learned list grows to 100+ |

A separate "Settlements" tab was considered and rejected: with one settlement (the first ~10 minutes) it would be a
single row. Instead Overview shows a compact settlements table when there are 2+ own settlements, and the scope
selector gives every tab a per-settlement view.

---

## 4. Tab layouts

Severity semantics everywhere: **Critical** = `Danger` + `UiGlyph.Danger`; **Warning** = `Warning` + `UiGlyph.Warning`;
**Info** = `Info` + `UiGlyph.Info`; **Good** = `Success` + `UiGlyph.Check`. Numbers use `Num`/`NumStrong`; an alarming
number takes its severity color.

### 4.1 Overview

```
┌ PEOPLE ───────┐┌ FOOD ──────────┐┌ GROWTH LIMIT ──┐┌ RESEARCH SPEED ┐
│ 46            ││ 1.4 seasons    ││ Food: 46 / 52  ││ 38 /yr         │
│ +4 this year  ││ stores 41 R ·  ││ births at 47%  ││ +12% vs last yr│
│ +3 born −1 died│ fed 100% last  ││ of normal      ││ 2 slots active │
└───────────────┘└────────────────┘└────────────────┘└────────────────┘
┌ WHAT'S SLOWING YOU DOWN (0.618) ─────────────┐┌ GROWTH LIMITS (0.382) ──────────┐
│ ▲ Stores are full                    Warning │ │ Food cap   ████████████░░ 46/52 │
│   Lost 9 R to full stores last season.       │ │ Housing    ███████░░░░░░░ 46/80 │
│   Storage 27 of 27 R · Settlements 1, 3      │ │ Group size █████████████░ 46/50 │
│   Helps: [Granary ▸ Mudbrick (BLD-..)]       │ │ Binding: food. Births slowed to │
│ ● Foragers lack baskets              Warning │ │ 47% (crowding), split likely    │
│   6 of 9 foragers on hand gathering; …       │ │ next winter.                    │
│ ● …                                          │ │ WELLBEING  [Hungry 0][Cold 4]…  │
│                                              │ │ LAST SEASON  +3 born · −1 died  │
│                                              │ │   (1 illness)                   │
└──────────────────────────────────────────────┘└─────────────────────────────────┘
┌ SETTLEMENTS (only with 2+) ─────────────────────────────────────────────────────┐
│ Settlement  People  Food   Winter  Stores  Cap(bind)  Cohesion  Top issue       │
│ 1           31      100%   ok      27 R    food 31/34 steady    Stores full  ▸ │
│ 3           15      72% ▲  short ▲ 3 R     food 15/18 strained  Starving    ▸ │
└─────────────────────────────────────────────────────────────────────────────────┘
```

Metric cards (scope-aware; "All" sums, ratios recomputed from sums):

| Card | Big number | Caption | Data |
|---|---|---|---|
| PEOPLE | population | "+N this year" (vs ring sample 4 seasons ago); "+b born −d died" last season | `SettlementPopulation`, history, `FocusBirths`, `FocusDeaths[]` |
| FOOD | runway "x.x seasons" (stores ÷ next season's need, capped "8+") | "stores S R · fed F% last season"; colored by FS band | `SettlementFoodStores`, `SettlementFoodNeed`, `SettlementFoodSecurityPermille` |
| GROWTH LIMIT | "<cap name>: pop / cap" for the binding of Food/Housing | "births at B% of normal" where B = FS × health × (1 − crowding) | §6 fields |
| RESEARCH SPEED | `CivInsightPerYear` | "±x% vs last year · n slots active" | history ring Insight series |

Bottleneck list: `UiRowButton`-like pooled rows (max 6 visible, scroll), sorted by score (§5.3). Each row:
severity glyph, headline (BodyStrong), detail line with numbers (Caption), "where" (settlement list, ≤3 then "+n"),
"Helps:" up to 2 project chips (`UiProjectCard` Chip mode; click opens the research web at that project) or a lever
chip (e.g. "Set scouting to Auto" which issues the existing scout command). Hover: tooltip with the rule and thresholds
("Shown when food lost to full stores > 0 last season or stores ≥ 95% of storage"). Clicking the row with a settlement
sets the scope to it.

Empty state (no Warning/Critical): a single Good row: "Nothing is holding your people back. Growth is limited by the
land: 46 of the 52 people it feeds." (Uses the binding cap.) Before the first season boundary: "Numbers appear after
the first season ends (in N s)."

Growth limits: three `UiProgressBar`s (pop/cap, cap = 100%), the binding one in Accent with "Binding" eyebrow; caption
explains consequence: food/housing binding → "births slowed to B%"; group binding (pop ≥ 85% of group cap) → "a daughter
band will split soon" (or the `FissionPending` flag: "splits off next season"). Lean cap shown in tooltip of Food.

### 4.2 Stores

```
Scope: All settlements                                   [ per season | per year ]
┌ FOOD · 41 R in store · storage 54 R ───────────────┐┌ WHERE FOOD WENT LAST SEASON ──┐
│ Good          Stock   In    Out   Net   Lasts Trend ││ Start          38 R           │
│ Nuts & seeds  14.0   +9.1  −5.0  +4.1  …    ▁▂▃▅▆ ││ + Foraging     +22 ███        │
│ Meat           3.2   +6.0  −6.8  −0.8       ▅▃▂▂▁ ││ + Hunting       +7 █          │
│ Plant food     0.4   +12   −11.6 …                ││ + Fishing       +3            │
│ …                                                  ││ − Eaten        −27 ████       │
│ Total (R)     41     +32   −29   +3    1.4s  ▃▄▅▆▆ ││ − Spoiled       −4 █          │
├ MATERIALS ─────────────────────────────────────────┤│ − Full stores   −1            │
│ Wood          6.3    +8.0  −7.5  +0.5  4.2s fuel   ││ = End          41 R           │
│ Stone         2.0    +1.2  −0.5 …                  ││ Need next season 29 R (winter)│
│ Fiber, Hide, Bone, Cord, Leather, Clay, Mudbrick…  ││ Land supports ≈ 112 R/yr:     │
├ TOOLS (stock · wear) ───────────────────────────────┤│ plants 60 · meat 30 · fish 22 │
│ Baskets 2 (61%) · Digging sticks 4 · Spears 3 …    │└──────────────────────────────┘
├ LIVESTOCK ─ Goats 6 · Cattle 0 · Pigs 2 ───────────┤
├ METALS (from KEY-3) ─ Copper ore, Tin, Bronze, Charcoal, kits ─────────────────────┤
└─────────────────────────────────────────────────────────────────────────────────────┘
```

Rows show goods with stock > 0 or any flow in the last 4 seasons, plus goods the civ unlocked (`kind: good` effects of
done projects) so "0" is visible when it matters (e.g. Baskets 0). Order: food (by stock desc), materials (registry
order), tools, livestock, metals. Category from the goods registry (`content/registries/goods.yaml`: `category`).

Columns:

| Column | Definition | Source | Warning state |
|---|---|---|---|
| Stock | current units (live, so fuel burn shows) | `SettlementStores` | 0 while needed (unlocked tool, fuel) → Warning |
| In | last season produced, net of crafting inputs | ledger `Produced` (§7.2) | — |
| Out | eaten + spoiled + over-cap + building + fuel (food: per good from snapshots) | ledger | — |
| Net | End − Start last season (includes "Other": trade, raids, gifts, a fission split) | ledger | negative and Lasts < 2 → Warning |
| Lasts | food total: stores ÷ next-season need; wood: stock ÷ fuel per season; others: "—" unless Net < 0 then stock ÷ −Net | §6 | < 1 season Critical, < 2 Warning |
| Trend | 16-season sparkline of stock at season end; winter samples drawn with a tick | history ring | — |
| Cap | food only: storage capacity row in the section eyebrow "storage 54 R" | `SettlementFoodCapacity` | stores ≥ 95% cap → Warning |

Tooltips per row: the decomposition (e.g. Wood: "+8.0 chopped · −4.5 hearth fuel · −3.0 pit house · −0 other").
Food total tooltip: food value note ("rations are food value: cooking and storage projects change it").

Waterfall: new `UiWaterfall : Control` drawing horizontal bars per line (gains Success, losses Warning/Danger
for over-cap). The "Land supports" caption is `SustainableFoodIncomeBreakdown` (annual) so the player sees whether
the land or the labor is short: if gathered last season × 4 ≪ land supports → labor/tools are the limit; if ≈ → land.

Toggle "per season | per year" multiplies flows by 4 except for seasonal goods (label "≈" for per-year).

Empty states: before the first boundary ("Flows appear after the first season ends"); Metals section hidden before
the civ's KEY-3 (`IsBronzeCivilization`); Livestock hidden when all zero and no HUS project done.

### 4.3 Work

Left (0.618): the current WORK ASSIGNMENTS list (unchanged), then a new **LABOR LAST SEASON** stacked bar in ws:
Food · Building · Materials (chop/gather/craft) · Herding · Upkeep (fire, weir, orchard, canal) · Care · Roles · Scouts
· Metal · Civic · Unused, with a caption from the food-share rule:

| Rule (`PlanAndWork` `:1363-1368`) | Caption |
|---|---|
| FS < 0.8 → 0.90 | "Hunger last season: 90% of hands go to food." (Warning) |
| stores < need → 0.85 | "Stores below one season's need: 85% of hands go to food." (Warning) |
| stores < 2× need → 0.70 | "Stores below two seasons' need: 70% of hands go to food." |
| else → 0.48 | "Stores are comfortable: half the hands are free for building and crafts." (Good) |
| city-town hold → ≤0.75 | "Town keeps 25% of hands for civic work." |

Then **WHERE WORK WAS LOST** (Phase 2): per action, `assigned − applied` ws ("Foraging: 2.1 ws without tools —
baskets 0") and `requested − harvested` ("Hunting found 6.0 of 9.4 R: game in range is thin"). Then scouting limits
(unchanged) and Other activities.

Right (0.382): NEEDS & WELLBEING chips (moved from today), TOOLS & METHODS: workers on an older method grouped by
reason and tool ("6 foragers: no Baskets", "2 hunters: party too small", "4 woodcutters: no tin") from
`FigureMethodFallback/Tool`; and SKILL/ROLES chips (priests, scribes, warriors).

### 4.4 Research

```
┌ IN PROGRESS ─────────────────────────────────────────────────────────────────────┐
│ (1) PRIMARY  Baskets & Carrying   ███████░░ 71%  ETA 0:21                        │
│     Foraging method ×1.8 · needs Baskets (craft: fiber 3 in store)               │
│     ≈ +14 R per season at last season's foraging   [Targets: Food short]         │
│ (2) SECONDARY …                                                                  │
├ COULD FIX YOUR BOTTLENECKS (Available / Almost ready) ───────────────────────────┤
│ [Storage Pits ▸ Stores are full] [Healing Herbs ▸ Illness]                       │
├ LEARNED · sorted by: [Value ▾ | Recent | Status]                                 │
│ Project            Learned   Status          Last season       Since learned    │
│ Plant Lore         Y1 Su     Paying off      ≈ +7.9 R          ≈ 92 R           │
│ Digging Stick      Y1 Au     Superseded      by Baskets        ≈ 31 R           │
│ Spear Hunting      Y2 Sp     Partly blocked  ≈ +2.1 R · 2/3    ≈ 18 R           │
│ Fire Making        Y1 Wi     Not measured    keeps fires lit   —                │
│ Healing Herbs      Y3 Sp     Paying off      ≈ 0.4 lives       ≈ 1.6 lives      │
│ Lean-tos           Y1 Sp     Used            4 built           housing +16      │
├ RESEARCH SPEED: 38 /yr · commons 30 · elders 4 · roles 4 · KNOW-HOW (top 3) ─────┤
└──────────────────────────────────────────────────────────────────────────────────┘
```

* IN PROGRESS: one row per focus slot (`FocusSlotProject/Permille`), ETA via existing `FormatEta`
  (`Game.FocusBar.cs:390`), effect lines via `UiText.DescribeEffects` (`UiText.cs`), the **projected benefit** line
  (§6.3, Phase 2) and "Targets: <bottleneck>" chips when its effects match an active diagnosis (§5.4). Queue entries
  below in a compact list.
* COULD FIX YOUR BOTTLENECKS: up to 4 Available/AlmostReady projects matched by §5.4; click opens the web at it.
* LEARNED: pooled rows; columns Learned (`CompletedTick` → "Y# Season"), Status chip, Last season value, Since learned
  cumulative. Hover popover: designed effect text, how it's measured (formula), adoption % (`AdoptionFraction`),
  where blocked (settlements + reason), and the before/after productivity sparkline with a marker at completion.
  Default sort: Value desc (most valuable first), ties by completion tick ascending. Projects with only prose/rule
  effects collapse into "Not measured (n)" group at the bottom, expandable.
* RESEARCH SPEED + KNOW-HOW: moved from today's right panel.

Empty states: no slots → "No research in progress. Open the research web to choose (T)." No completed projects →
"Nothing learned yet. Payoff appears one season after a project completes."

---

## 5. Bottleneck diagnosis

### 5.1 Mechanism

* `CivSim.Sim/View/Diagnosis.cs`: `static int Evaluate(in DiagnosisInputs inputs, Span<DiagnosisEntry> into)`,
  pure, integer/Fix64 only, no allocation; inputs are the cached per-settlement readouts of §7. Runs once per season (and
  when a live-only rule input changes: fire lit, scout policy, slot empty, food stock bands).
* Output per entry: `DiagCode Code` (ushort, append-only enum), `Severity` (byte 0 Info, 1 Warning, 2 Critical),
  `Settlement` (economy index, −1 civ-wide), `A, B, C` (int params, units in the code's doc comment), `Score` (int).
* Godot maps `Code` → headline/detail templates in `UiText` (e.g. `UiText.Diagnosis(code, a, b, c)`), aggregates
  per-settlement entries of the same code for "All settlements" (worst instance's numbers + list of settlements).
* Unit tests: one fixture per rule (fixtures exist: `LivingEconomyModel.Fixtures.cs`), plus a harness print in
  `FrameDump` so research-v1 / politics-v1 runs show which diagnoses fire.

### 5.2 Rules (thresholds are starting values; tune against the harness)

Notation: FS = last-season food security (0–1), Need = next season's food need (R), S = food stores (R), Cap_store =
storage capacity, Pop, FoodCap/HousingCap/GroupCap from `Settlement(i)`, ledger lines from §7.2.

| # | Code | Rule | Sev | Params | Detail text | Lever | Phase |
|---|---|---|---|---|---|---|---|
| B1 | Starving | FS < 0.70 | Crit | FS‰, Pop, deaths by starvation | "Only 64% of food needs met last season." | scouting → Auto/0 if scouts out; food research | 1 |
| B2 | FoodShort | 0.70 ≤ FS < 0.90 | Warn | FS‰ | "Food fell short: 84% of needs met." | food research | 1 |
| B3 | WinterRisk | season ∈ {autumn, winter} and forecast < 0.90 (Crit < 0.70) | Warn/Crit | forecast‰ | "Late winter forecast: 76% of needs." | storage/drying research | 1 |
| B4 | StoresRunningOut | S / Need < 1.0 and last net food < 0 | Warn (Crit if < 0.5) | S, Need, net | "Stores cover 0.6 seasons and fell 8 R last season." | — | 1 |
| B5 | StoresFull | OverCap > 0 last season, or S ≥ 0.95 Cap_store | Warn (Info if OverCap < 1 R) | OverCap, S, Cap_store | "Lost 9 R to full stores. Storage 27 of 27 R." | FOD-03 Storage Pits / granary (build) | 1 |
| B6 | Spoilage | Spoiled ≥ 25% of Gathered and Spoiled ≥ 2 R | Info (Warn ≥ 40%) | Spoiled, % | "Spoilage took 31% of what was gathered." | drying racks, pits, granary, `spoilage_of_stored_food` | 1 |
| B7 | CrowdingFood / CrowdingHousing | Pop ≥ 0.85 × min(FoodCap, HousingCap); code by the smaller cap | Info (Warn when birth factor < 50%) | Pop, cap, birth‰ | "Births at 47% of normal: the land feeds about 52, you have 46." / "…housing for 48" | food research / dwelling research | 1 |
| B8 | SplitComing | `FissionPending`, or Pop ≥ 0.85 GroupCap | Info | Pop, GroupCap | "A daughter band will leave next season." (expansion, not a fault) | Posture changes threshold | 1 |
| B9 | BuildWaiting | build blocker readout: a dwelling/store is wanted but its good is short | Warn | structure, good, have, need (×100) | "Builders wait for wood: a pit house needs 4, stores hold 1.2." | chopping happens automatically; research wood yield | 2 |
| B10 | FuelLow / FireOut | hearth and wood / fuel-per-season < 1 (FuelLow, Warn); `FireLit` false while FIR-01/02 known (FireOut, Crit) | Warn/Crit | wood, fuel/season | "Hearth fuel for 0.8 seasons." / "The fire is out: cooked food is worth less and people are colder." | — | 1 |
| B11 | ToolsMissing | for a job, workers with `FigureMethodFallback == MissingTool` ≥ 25% of that job's workers (min 2) | Warn | job, n, total, tool | "6 of 9 foragers have no Baskets; foraging at 62% of what you know." (efficiency from `FigureMethodEfficiencyPermille`) | crafting research / materials | 1 |
| B12 | HuntPartySmall | any hunter with fallback HuntParty | Info | n | "Hunters too few for the new hunting method (needs 3)." | — | 1 |
| B13 | MetalAccess | fallback NoTin/NoArsenic/NoOre/NoFuel on any worker | Warn | reason | "No tin: bronze tools cannot be cast." | Decree: open mine / tin compact | 1 |
| B14 | Cold | Cold figures ≥ 10% of pop (min 2) | Warn (Crit ≥ 30% in winter) | n | "11 people were cold last season: shelter or clothing short." | shelter/clothing research | 1 |
| B15 | Illness | (Ill + Wounded) ≥ 15% of pop (min 3), or civic outbreak | Warn | ill, wounded | "7 sick, 2 wounded." | MED research | 1 |
| B16 | CohesionLow | band Strained/Fraying | Warn/Crit | overreach, span | "Cohesion strained: −6 from distance to the capital, −3 beyond Admin span." | GOV research | 1 |
| B17 | FoodLaborSqueeze | food-share rule ∈ {0.85, 0.90} for 2 consecutive seasons | Warn | share‰, build ws | "85% of hands on food two seasons running: little left for building." | food yield research | 2 |
| B18 | ToolLimitedWork | assigned − applied ≥ 1 ws for an action | Warn | action, ws lost | "2.1 ws of foraging went unused for want of baskets." | crafting | 2 |
| B19 | LandLimited | harvested < 0.8 × requested for an action (≥ 2 R short) | Warn | action, got, wanted | "Hunting found 6 of 9.4 R: game in range is thin." | herd/hunt-share research, Beacon to expand | 2 |
| B20 | ResearchIdle | all three slots −1 and queue empty and Council off | Crit | — | "Nothing is being researched." | open web | 1 |
| B21 | ScoutsWhileHungry | scouts active at a settlement with FS < 0.9 and policy One/Two | Warn | scouts | "2 scouts are away while food is short." | button: set scouting to Auto | 1 |
| B22 | MandateFull | `FocusMandateUnlocked` and Mandate == cap | Info | seasons at cap | "Mandate is full; Decrees go unspent." | open Decrees | 1 |
| B23 | Depleted | range stock < 40% of capacity for forage/game/fish/wood | Warn | resource, ‰ | "Game near Settlement 1 at 28% of normal." | Beacon / found village | 3 |

### 5.3 Ranking

`Score = Severity × 100 000 + Impact`, where Impact = people affected × 1000 / civ population (B1–B4, B7, B14–B15
use affected = settlement pop; tool/labor rules use workers or ws × 100; B8, B12, B22 use 0). Sort Score desc, then
settlement index asc, then code asc (deterministic). Keep ≤ 32 entries per frame; Overview shows the top 6 aggregated
codes. Hysteresis: a rule clears only when its metric is 5 points beyond the threshold for one season (store previous
season's fired codes in builder memory), so callouts don't flicker at band edges.

### 5.4 Levers (Godot-side, presentation only)

A curated table `DiagCode → effect predicates` over `ResearchCatalog` `EffectDef`s (`ResearchCatalog.cs:56-70`):
e.g. StoresFull → `Build(structure: storage_pit|granary)`; Spoilage → `Build(drying_rack|storage_pit|granary)` or
`Mod(stat: spoilage_of_stored_food)`; ToolsMissing(forage, Basket) → `Good(id: G-BASKET)`; Cold → `Build(lean_to|
hide_tent|pit_house|…)`, `Good(G-WRAP|G-TAILORED)`; Illness → `Hazard(cause: endemic|epidemic|injury)`; CohesionLow →
`Cohesion`, `Admin`; CrowdingFood → `Method(action: forage|hunt|fish)`, `Mod(stat: forage.yield|hunt|field_yield)`.
Candidates are projects with state Available/AlmostReady/in a slot/queued (`FocusProjectState`); prefer in-slot, then
lowest effective cost. **Only predicates on effects the sim actually applies** (see risk R6). If the matching project
is already done, show "Already learned — improves as builders catch up" instead of a chip.

---

## 6. Research payoff ("is it paying off?")

### 6.1 Principle

For each completed project, per measurable effect, compute the **marginal counterfactual**: "what would last season
have lost if only this project were un-learned, everything else unchanged". For a multiplier m on a flow F that is
`F × (1 − 1/m)`. Marginal values do not add up to the total change when several multipliers stack; say so in the
tooltip ("each project's share is what you'd lose without it alone"). Accumulate each season into a running total
since completion (builder memory, saved). Never claim a number the sim does not mechanically produce.

### 6.2 Attribution rules by effect kind (sim-side `LivingEconomyModel.Payoff.cs`, read-only)

| Effect | Measure (last season) | Unit | Status logic |
|---|---|---|---|
| `method` (action a, rung r) | for each own settlement where the effective rung for a = r: `Output_a,s × (1 − Rate(next lower known & usable rung)/Rate(r))` (`MethodRate`, `View.cs:220-232`) | R (forage/hunt/fish), wood | r < effective everywhere → **Superseded** (value 0); r > effective at some settlements → **Partly blocked** with reason (`MethodReadout`), all → **Blocked**; adoption < 50% (`AdoptionFraction`) or < 1 season → **Too early** |
| `mod` yield stats: `forage.yield`, `autumn_forage` (autumn only), `hunt`, `wood_yield`, `field_yield`, `irrigated_yield`, `smelt_yield` | `Flow × (1 − 1/m)` (pct → m = 1 + v) | R, wood, metal | Paying off if > 0 |
| `mod` food value: `food_value`, `food_value_of_meat_roots_cereals`, `cereal_food_value` | `Eaten_relevant goods (in R) × (1 − 1/m)` (per-good eaten from the food ledger) | R | |
| `mod spoilage_of_stored_food` (m < 1) | `Spoiled × (1/m − 1)` = food saved | R | |
| `mod` insight stats: `commons_insight`, `elder_insight`, `priest_insight`, `scribe_output`, `role_insight` | `CommonsPerYear` (or role output) × (1 − 1/m) | Insight/yr | research-on-research payoff |
| `hazard` (cause, m < 1) | `ExpectedHazard_civ,cause × (1/m − 1)` = deaths averted | people | needs per-civ expected hazards (new) |
| `cap group` | "Group limit V (was W); largest settlement P" | people | **Used** if any settlement pop > W × cohesion factor |
| `cap food/lean/housing` | not shown as value: these are mean-field calibration targets (`EconomyBalanceCalibration.cs`); the live sim derives FoodCap from yields. Text: "Counted in its other effects." | — | Not measured |
| `build` structure | count standing across own settlements + its function (housing +N people, storage +15 R, …) | structures | **Unused** if 0 built after 4 seasons |
| `good` | in stock / in use (tools: workers whose rung uses it) | units | **Unused** if 0 for 4 seasons |
| `unlock_action` | workers doing the action last season | workers | |
| `role` | holders and their Insight | people / Insight | |
| `cohesion` (E0–E2 one-off nudge) | "+x Cohesion when learned" | points | Used |
| everything else (prose, rule, claim, reveal, diplo, mods with no flow mapping) | "Not measured — <description>" | — | Not measured |

Project status = worst-meaningful across its measured effects: Paying off (any value > 0, none blocked) > Partly
blocked > Blocked > Unused > Superseded > Too early > Not measured. Value column = sum of effects with the same unit
(R summed; mixed units listed "≈ +7.9 R · 0.4 lives").

Context, not a ratio: show "Cost 140 Insight (3.7 seasons of research then)" in the popover. Research cost and food
are incommensurable; an invented payback ratio would mislead.

### 6.3 In-progress and projected-vs-realized

The same engine with hypothetical completion: mod → `Flow × (m − 1)`; method rung above current → `Flow × (Rate(r) /
Rate(effective) − 1)` if its tool requirement is met, else "needs <tool> (stock 0)"; hazard → `Expected × (1 − m)`;
group cap → settlements ≥ 85% of current cap. At completion the builder freezes the last projection for that project
(view memory), so the learned row's popover can show "Projected +14 R/season · realized +11 R last season".

### 6.4 Before/after productivity

The history ring keeps civ-level yield per worker-season for forage, hunt, fish, chop (Phase 2 ledger: output ÷
applied ws). The Research popover and Stores tooltips draw that sparkline with Accent tick marks at each completion
season of projects touching that action. This is observational ("changed since learned, not all due to it") and
complements §6.2's model-based number.

---

## 7. Data plumbing

### 7.1 New read-only economy accessors (Phase 1)

In a new partial `src/CivSim.Sim/Economy/LivingEconomyModel.Readouts.cs` (no state writes, no RNG):

```csharp
public static int StoreSlotCount => SlotCount;                       // Good.Count + MetalGood.Count
public static string StoreSlotId(int slot);                          // GoodId / MetalGoodId → registry id
public Fix64 StoreAt(int settlementIndex, int slot) => Stock(_settlements[settlementIndex], slot);
public Fix64 FoodStorageCapacity(int settlementIndex);               // 12 + 15·pits + 80·granaries; SpoilFood uses it too
public Fix64 FoodNeedNextSeason(int settlementIndex);                // FoodNeed(s, nextIsWinter) × 0.75 under investment
public Fix64 FuelPerSeason(int settlementIndex);                     // hearth 1.5/3, or unattended 1, + E3FuelNeed
public readonly record struct CapsReadout(int FoodCap, int HousingCap, int GroupCap, int LeanCap,
    int FoodSecurityPermille, int ForecastPermille, int HealthPermille, int CrowdingFreePermille);
public CapsReadout Caps(int settlementIndex);                        // one FoodNeed pair, one SustainableFoodIncome
public FoodLedger LastFoodLedger(int settlementIndex);               // §7.2
```

`Caps` must compute FoodCap and HousingCap the way `UpdateNeedsAndPopulation` does (`:2857-2863`) and expose
`FertilityHealthFactor` (`:2904`) so the "births at B%" number is the sim's own.

### 7.2 Diagnostic ledgers in the economy (Phase 1 food, Phase 2 the rest)

Add to `SettlementState` a `SeasonLedger? Ledger` (allocated on first use; **not hashed, not snapshotted, never read by
any decision**; comment block like `:251-252`):

* **Food (Phase 1)**, in ration value at capture time:
  `Start` (= existing `_lastSeasonReachableFoodStores`), `BeforeEat` (= existing
  `LastSeasonFoodStoresBeforeConsumption`), `Eaten` (= min(available, need) in `ConsumeFood` `:2688-2700`),
  `AfterSpoil` (snapshot in `SpoilFood` between the spoil loop and `TrimFoodToCapacity` `:2729-2746`), `End` (after trim).
  Derived: Gathered = BeforeEat − Start; Spoiled = BeforeEat − Eaten − AfterSpoil; OverCap = AfterSpoil − End; Other =
  next season's Start − End (trade, raids, gifts, fission, GOV-04 redistribution).
  Per-good eaten/spoiled come for free by keeping `Fix64[] StoresAtStart/BeforeEat/AfterEat/AfterSpoil/End` copies of the
  41 E0–E2 slots (5 × `Array.Copy` per settlement per season).
* **Materials (Phase 2)**: the same snapshots plus `AfterBuild` (after `BuildIfReady`) and a `FuelBurned` accumulator
  added in `AdvanceCampFires` and `BurnE3Fuel`. Per slot: Produced = BeforeEat − Start + FuelBurned(this season) (net of
  crafting inputs), Built = AfterSpoil − AfterBuild, Fuel = FuelBurned, Other = residual. Reconciles exactly by
  construction.
* **Actions (Phase 2)**, indexed {forage, hunt, fish, farm, chop, gather, herd}: `Assigned` ws, `Applied` ws (after
  `LimitActionWorkByTool`, e.g. `:1530`), `Requested` output (work × rate, before stock limits), `Harvested` output
  (return of `Harvest`/`HuntIndividualHerds`/`HarvestFish`/`WeirCatch`; fields and herds by snapshot around
  `HarvestFields`/`HerdAnimals`/`AdvanceDomesticHerds`), plus output in R.
* **Labor (Phase 2)**: total work capacity and each reservation in `PlanAndWork` (fire, care, roles, scouts, weir,
  orchard, canal, metal, civic, food, build, material, herd, unused) and the `FoodShareRule` byte (`:1363-1368`).
* **Expected hazard by civ × cause (Phase 2)**: `_lastExpectedHazardsByCivAndCause` filled next to
  `RecordExpectedHazards` (`:3143`), not hashed.
* **Build blocker (Phase 2)**: `public (int Structure, int Slot, Fix64 Have, Fix64 Need) BuildBlocker(int i)` — a pure
  copy of `SelectBuildCandidate`'s wants (needsHousing, warmth deficit, storage) evaluated with stock checks removed;
  returns the first wanted structure whose payment fails. Must not touch `BuildTarget`.

Guard test: run research-v1 with ledgers enabled and with an `internal bool DiagnosticsEnabled = false` switch; assert
identical `ComputeChecksum` every season. Also extend `SourceRulesTests` scope implicitly (new files are under
`src/CivSim.Sim`).

### 7.3 New `MatchFrame` fields

All "own" blocks are filled for own settlements only (`IsOwn`), zero elsewhere; sized in `EnsureSettlements`
(`MatchFrame.cs:398-412`) and reserved in `Reserve`. Fix64 values are raw longs like the rest of the frame.

**Live (every build; cheap):**

| Field | Type | Source |
|---|---|---|
| `static int StoreSlots` | `LivingEconomyModel.StoreSlotCount` | |
| `SettlementStores` | `long[w × StoreSlots]` | `StoreAt` |
| `SettlementFoodStores` | `long[]` (R) | `FoodStores(i)` |
| `SettlementFireTicksLeft`? (no) — use `SettlementFuelPerSeason` | `long[]` | `FuelPerSeason` |

**Per-season cache (copied when `EconVersion` differs from the target's):**

| Field | Type | Source |
|---|---|---|
| `EconVersion` | `int` | builder counter; bumps on season change, project completion, settlement count change |
| `SettlementFoodCapacity` | `int[]` (R) | `FoodStorageCapacity` |
| `SettlementFoodNeed` | `long[]` (R, next season) | `FoodNeedNextSeason` |
| `SettlementFoodSecurityPermille`, `SettlementForecastPermille` | `short[]` | `SettlementLite` |
| `SettlementFoodCap`, `SettlementHousingCap`, `SettlementGroupCap`, `SettlementLeanCap` | `int[]` (people) | `Caps` |
| `SettlementBirthFactors` | `short[w × 3]` (FS, health, 1−crowding ‰) | `Caps` |
| `static int FoodLedgerLines = 12` | Start, Gathered, Eaten, Spoiled, OverCap, Other, End, GatheredForage, Hunt, Fish, Fields, Orchard+Herds | |
| `SettlementFoodLedger` | `long[w × FoodLedgerLines]` | §7.2 |
| `SettlementGoodFlows` (P2) | `long[w × TrackedGoods × 4]` (Produced, Built, Fuel, Other) for `static string[] TrackedGoods` (wood, stone, flint, clay, fiber, hide, bone, cord, leather, mudbrick, straw, sapling, charcoal, copper, tin, bronze) | §7.2 |
| `SettlementActions` (P2) | `long[w × 7 × 5]` (Assigned, Applied, Requested, Harvested, OutputR) | §7.2 |
| `SettlementLabor` (P2) | `long[w × 14]` + `byte[] SettlementFoodShareRule` | §7.2 |
| `SettlementBuildWant` (P2) | `sbyte[]` structure, `byte[]` slot, `int[]` have×100, `int[]` need×100 | `BuildBlocker` |
| `FocusBirths`, `FocusDeaths[8]` | `int`, `int[8]` | builder `_birthsByCiv`, `_deathsByCivCause` |
| `FocusInsightCommons` | `long` | `CommonsPerYear` |
| `FocusProjectAdoptionPermille` | `short[projects]` | `AdoptionFraction` for done projects |
| `FocusProjectDoneSeason` | `int[projects]` (−1) | `CompletedTick / 150` |
| `DiagnosisCount`, `DiagCode` `ushort[]`, `DiagSeverity` `byte[]`, `DiagSettlement` `int[]`, `DiagA/B/C` `int[]`, `DiagScore` `int[]` (32) | | §5 |
| `PayoffCount` and `PayoffProject` `int[]`, `PayoffEffect` `byte[]`, `PayoffUnit` `byte[]`, `PayoffStatus` `byte[]`, `PayoffReason` `byte[]`, `PayoffActivePermille` `short[]`, `PayoffLast` `long[]`, `PayoffTotal` `long[]`, `PayoffProjected` `long[]` (P2; ≤ 512) | | §6 |

**History (copied when `HistoryVersion` differs):**

| Field | Type | Notes |
|---|---|---|
| `HistoryLength = 16`, `HistoryCount`, `HistoryFirstSeason` | `int` | ring unrolled oldest→newest |
| `SettlementHistory` | `int[w × 16 × Series]` | Series (scaled ×100 ints): Pop, FoodStores, Gathered, Eaten, Spoiled, OverCap, FS‰, Wood, Stone, FoodCap, HousingCap |
| `CivHistory` | `int[16 × CivSeries]` | Pop, FoodStores, Insight/yr, yield R/ws for forage/hunt/fish, wood/ws (P2), projects done |

### 7.4 FrameBuilder changes

* New partial `FrameBuilder.Economy.cs`: `RefreshEconomyCache(match, season)` keyed on (season, `CivProjectsDone`,
  settlement count), like `_claimSeason` (`FrameBuilder.cs` caches block). Fills builder-owned arrays, runs
  `Diagnosis.Evaluate` and the payoff engine, bumps `_econVersion`. `FillEconomy(target)` copies only if
  `target.EconVersion != _econVersion` (three frame buffers → three copies per season).
* At the season boundary already detected in `DeriveEconomyEvents` (`FrameBuilder.Events.cs:419`), push one history
  sample per own settlement and civ, and add each payoff's last-season value to its running total.
* View state: `StateVersion` 5 appends history ring, payoff totals/projections, previous-season diagnosis codes
  (hysteresis) to `WriteState`/`ReadState` (`FrameBuilder.State.cs:14-56`); `ReadState` accepts 1–5 and leaves the new
  memory empty for older saves. Remember the doc's guarantee "a resumed save produces frames identical, field for
  field" (`docs/MVP/18` §8): the economy ledgers are not snapshotted, so after a load the frame's ledger fields must
  come from builder memory (the last history entry), not from the economy, until the next boundary. Store the last
  full food ledger in view state to keep frames identical.
* Budgets (`docs/MVP/18` §9): steady-state frame cost adds one 41–72-slot store copy per own settlement (< 0.02 ms).
  Season-boundary cost adds `Caps` per own settlement (O(own settlements × figures), ~1 ms worst case at 2,400 figures,
  32 own settlements); the doc already accepts heavier boundary frames. Zero allocation: all arrays sized in Ensure*,
  no `List`, strings or LINQ; `ViewStoreTests` must stay green.

### 7.5 Godot classes

* `Game.People.cs`: becomes the shell (header, tabs, scope, footer) and `OpenPeoplePanel(tab, settlement)`.
* New partials: `Game.People.Overview.cs`, `Game.People.Stores.cs`, `Game.People.Work.cs` (today's body moved),
  `Game.People.Research.cs`.
* New UI controls in `game/Scripts/Ui/`: `UiSparkline` (polyline + last-point dot + optional event ticks, 16 points,
  height 20), `UiStackedBar` (labor split), `UiWaterfall` (food), `UiStatusChip` (chip with severity color), `UiDiagRow`
  (pooled bottleneck row). Add `CaptionDanger`, `NumWarning`, `NumDanger` label styles in `UiTheme`.
* `UiText`: `DiagnosisHeadline/Detail(code, a, b, c)`, `PayoffStatusName`, `GoodName(slotId)` (registry names
  already loaded, `UiText.cs:58`), unit formatting (`FormatRations`, `FormatSeasons`).
* `PeopleLevers.cs`: the §5.4 predicate table and catalog search.

---

## 8. Phased plan

### Phase 1 — Stores tab, Overview bottlenecks, research usage (highest value, smallest change)

Sim: §7.1 accessors; food ledger snapshots; per-season cache; history ring + view-state v5; `Diagnosis` rules B1–B8,
B10–B16, B20–B22; frame fields marked live/per-season/history above except those marked P2; `FocusBirths/Deaths`,
adoption, done season. Tests: checksum guard, one test per rule, zero-alloc, save/load frame identity, FrameDump
lines.
Godot: shell with tabs + scope; Overview (cards, bottlenecks with lever chips from §5.4, growth limits, wellbeing,
settlements table); Stores (goods table, food waterfall, sparklines, runway); Work (today's content + tools & methods
grouping); Research (in progress with effect text + "Targets" chips, Learned list with status from **usage**: method
in use/blocked/superseded via figure rungs, structures built, goods in stock, adoption; values only for hazard-free,
directly countable items). Refresh gating.

Covers: the Resources tab fully; "what is slowing us down" for food, storage, spoilage, crowding/caps, fuel, tools,
cold, illness, cohesion, scouts; research "is it in use" but not "how much it's worth".

### Phase 2 — Quantified payoff and labor (core ask fully covered here)

Sim: action ledger, labor ledger and food-share rule, materials flows, build blocker, per-civ expected hazards, payoff
engine §6.2 + projections §6.3 + running totals; rules B9, B17–B19; civ yield-per-ws history.
Godot: Research values, projected vs realized, before/after sparklines with completion ticks; Work tab labor bar and
"where work was lost"; Stores tooltips with material decomposition.

### Phase 3 — Levers and depth

Range depletion readout + B23; research web inspector "Payoff" section reusing the Research tab popover; deep links
from toasts/top bar food chip/tile card; Bronze: metal-chain flows (`MetalOutputRate`, `Metal(i)`), compact health per
route, Mandate use; settlement comparison sort/filter; harness acceptance checks (e.g. research-v1 grower must show
StoresFull or CrowdingFood by minute 12, never ResearchIdle).

---

## 9. Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Diagnostic state accidentally read by a decision → golden drift | separate partial file, `Diag`/`Ledger` naming, not hashed/snapshotted, checksum-equality test with diagnostics disabled |
| R2 | View calls a mutating economy method (`HasBuildCandidate` sets `BuildTarget`, `:2379-2385`) | new pure `BuildBlocker`; review list in the PR |
| R3 | Frame budget / allocation (`docs/MVP/18` §9, `ViewStoreTests`) | per-season cache + version-stamped copies; pre-sized arrays |
| R4 | Save/replay frame identity after load | builder memory holds the last ledger and history; `StateVersion` 5 with v1–4 fallback |
| R5 | Attribution misread as causation | "≈", tooltips with formula, "would lose without it alone", observational sparklines labeled "since learned" |
| R6 | Many catalog effects have no measurable mechanic: of 108 E0–E3 `mod` effects, 65 use stat names no sim code reads by name (e.g. `skill_gain`, `seasonal_yields`, `storage_capacity_per_storage_building`, `storage_losses_to_mismanagement`, `craft.speed`, `carry`, `knap.speed`, `fertility`); some are implemented via project-id checks instead, some may be unimplemented. Food/lean/housing `cap` effects are calibration targets only (`EconomyBalanceCalibration.cs`; only `cap: group` is read, `:3848-3859`) | show "Not measured"; lever table only uses effects with a known mechanic; recommend a content audit test listing stat names without a reader |
| R7 | Exposing a design gap: the planner builds at most one storage pit and one granary (`SelectBuildCandidate`, `:2446` checks `== 0`), so food storage caps at 27 R or 107 R per settlement; the new "lost to full stores" line may fire constantly with no lever | decide before Phase 1 ships (Q3) |
| R8 | Work beyond tool capacity is dropped (`LimitActionWorkByTool` returns min(work, tool work), `:4277-4295`); surfacing it may reveal sizable hidden losses | that is the point of B18; check balance impact with the Python oracle before tuning |
| R9 | Seasonal lumpiness: fields harvest once a year in autumn (`:1609-1612`), production is one tick per season | label per-season vs per-year; use same-season-last-year comparisons in runway text once 4+ samples exist |
| R10 | UI clutter in a 1100 × 705 sheet | each tab scrolls; Overview capped at 6 callouts; collapsed "Not measured" group |

---

## 10. Open questions for the user

1. **Storage cap rule.** Is "one storage pit, one granary per settlement" intended? If the planner should add pits when
   stores hit the cap, that is a sim change (golden change) separate from this UI work.
2. **Unmeasured research.** Should projects whose effects have no measurable mechanic show as "Not measured" (honest,
   but it reveals that some research does little in the current build), or be hidden from the payoff list?
3. **Advice strength.** Is suggesting specific research projects for a bottleneck ("Helps: Storage Pits") welcome, or
   too prescriptive for a hands-off game? Recommendation: yes, as chips, never auto-queued.
4. **Toasts.** Keep bottlenecks panel-only (recommended; the existing "Food warning" toast already covers famine), or
   add a toast when a new Critical callout appears?
5. **Window naming.** Rename "People & needs" to "Your people" with the four tabs?
6. **History length.** 16 seasons (4 years) of trend per settlement is the proposed default; longer costs only memory.

---

## Status (2026-10-29)

**Phases 1 and 2 are built (uncommitted).**
- Phase 1: the four tabs and scope selector; food ledger, store readouts, growth caps and 16-season history
  (`LivingEconomyModel.Readouts.cs`, `FrameBuilder.Economy.cs`, `EconomyView.cs`); bottleneck rules in `DiagnosisEngine`
  (`Diagnosis.cs`); research "lever" chips.
- Phase 2: per-action work and output ledgers, the labor split with the food-share rule, expected deaths per cause, and a
  read-only `BuildBlocker` (`LivingEconomyModel.Payoff.cs`); the payoff engine (`Payoff`, `Projection`) counting methods,
  yield and food-value modifiers, spoilage and hazard effects; running totals since learned and a projection for each
  research slot kept in view state (v6; v5 saves load with the cache emptied); rules B9, B17, B18 and B19; the Work tab labor
  split and "where work was lost"; the Research tab's last-season and since-learned columns, "Worth most" sort and
  projected gain for research in progress.
- Not built: the frozen "projected vs realized" pair, the before/after productivity sparkline, per-good material flows with
  tooltip decomposition (B9 reads stocks directly instead), range depletion (B23), insight and group-cap payoff, hysteresis.
- Everything in the economy is diagnostic only: not hashed, not snapshotted, never read by a decision. The toy, research and
  politics golden outputs are byte-identical with and without it.

**Open-question calls made:** keep the one-pit/one-granary rule (no sim change); unmeasured projects show "No direct
measure"; lever chips are suggestions only; no toasts; window named "Your people"; 16 seasons of history.
