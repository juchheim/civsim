# 30. What a town is waiting on, and what the player can do about it

Status: the readout, the panel and the build-focus order are implemented (2026-11-08). The other options in §7 are proposals.
Numbers marked (P) are provisional.

## 1. The problem

A town builds from three separate lists, each with its own planner and its own single slot, and the player could see none of it:

| Slot | Planner | What it picks |
|---|---|---|
| Homes, stores, early works, defenses | `SelectBuildCandidate` (basic list, 25 rules) | the first rule, in a fixed order, whose structure is learned, wanted by the town and **paid for from the stores right now** |
| Iron works | `IronBuildWants` / `NextWantedIronKind` | the first wanted work in enum order (gatehouse, frontier wall, crane, castle first), then it **holds the slot** until built |
| Civic buildings | `PlanCivic` / `ChooseCivic` | the first civic building the town qualifies for, then it **holds the slot** until built; the City path (Temple or Palace) overrides |

Consequences found in the code and measured in §5:

* On the basic list a structure that cannot be paid for is simply skipped, so "waiting on materials" is invisible: the town builds
  something else, or nothing.
* The materials are not gathered *for* the structure. Gatherers work to fixed stock levels (clay to 10, stone to 20, bricks to 200,
  wood to a fuel reserve), so a structure whose price is above those levels (the granary at 80 bricks) is never reached by design.
* A town builds **at most one basic structure a season**, so even affordable cheap works queue for years.
* Some rules cannot be met by any town on some sites (no river for a weir, no hill for a hillfort, a village cannot raise a Town
  building), and nothing said so.

## 2. The readout

`LivingEconomyModel.BuildOutlookFor(settlementIndex)` (new, `LivingEconomyModel.BuildOutlook.cs`) returns one immutable `BuildOutlook`
for a town: a `BuildItem` for every structure its civilization has learned and the town could ever raise, across all three slots,
plus a headline (`Building`, `WaitingOnMaterials`, `NothingWanted`). It reads the planners' own rules and changes nothing: no state,
no random numbers. The frame carries it for the viewer's own towns (`MatchFrame.SettlementBuild`), refreshed once a season and when
the player's focus changes, but only while the Building tab is open (`FrameBuilder.WantBuildOutlooks`): an outlook is a fresh object
graph per town per season, and the existing test that frame building allocates nothing once warm (`ViewStoreTests`) caught it when
it was always on.

It replaces the old `BuildBlocker` (which mirrored 10 of the 25 basic rules by hand). The planner and the readout now share one
table, so they cannot disagree:

* **Basic slot.** `SelectBuildCandidate` is the loop `for rule in BasicRule: learned && BasicRuleWanted && BasicNeedsMet`. The readout
  asks the same three questions, plus `BasicGate` for the hard rules (cap, size, site, prerequisite).
* **Iron slot.** `IronBuildWanted` is now "`IronGate` is open with the town's appetite included". The original expression is kept
  in `LivingEconomyModel.Fixtures.cs` as an oracle and a test checks the two agree over a matrix of towns.
* **Civic slot.** `ChooseCivic` is the loop over `CivicOrder` asking `CivicGate`.

### 2.1 What it distinguishes

Each item has a status and, when something is in the way, a cause (`BuildCause`) and a specific reason (`BuildWhy`, with a number
and a project id where relevant):

| Status | Cause | Meaning | Examples (reason) |
|---|---|---|---|
| Current | Materials / Labor | the slot's target; a good is short, or the work is still to do | Temple: cut stone 0 of 10; granary 2 of 4 work-seasons |
| Short | Materials | wanted, allowed, a good is short | granary: brick 0 of 80 |
| Waiting | Ahead | wanted, allowed, paid for, but another structure has the slot | lookout post behind the pasture; anything behind a City-path Temple |
| NotNeeded | NotNeeded | allowed, but the town sees no need | a house when there is room; a crane with no large work |
| Blocked | Locked | the unlocking project is not learned (listed only in the diagnostic census) | |
| Blocked | Requires | another structure, project or condition first | potter's workshop needs a kiln; gatehouse needs walls; bakery needs AGR-02; caravanserai needs 2 trade routes |
| Blocked | Site | the ground or neighbours | weir with no free river; hillfort with no high ground; shrine within 6 tiles of another; castle with no rival within 12 |
| Blocked | Size | too small or too low a tier | shrine under 20 people; bathhouse in a village |
| Blocked | Taken | only one, or reserved for another town | Palace, Wonders, Markets (largest towns only), academies (at most 3) |
| Done | Cap | built, or at its limit | |

For every short good the readout also says where it would come from (`GoodSupply`): `Flowing` (the town already makes it),
`NeedsProject` (and which), `NoSource` (no timber, stone or clay within reach, with the reach in tiles) or `NeedsOutside` (iron
bloom with no ore reachable). The tests pin this: e.g. stone with none in reach is `NoSource`; cut stone before STN-11 is
`NeedsProject STN-11`.

## 3. Levers: what the player can do, from the code

The levers below are the ones the code really offers. None is invented. Where there is none, the panel says so, with the exact cause.

| Blocker | Levers (what works) | If none |
|---|---|---|
| A good needs a project | **Learn the project** (the exact one: CER-01 clay, BLD-05 bricks, FOR-07 saplings, MIN-03 quarrying, STN-11 dressing, MET-09 iron) | |
| Wood short (basic list) | **Ask for the structure** (build focus): the town's material workers go to the axe for it, up to all of the material share instead of half | |
| Iron bloom short, no ore | **Buy iron** by Compact; **Open a mine** on iron ore | |
| Stone, timber or clay with none in reach | | "There is no stone left within three tiles of this town, and stone does not grow back" and similar. The only routes are new ground: **Found a village** near the deposit, which the panel names but cannot do for you. |
| Clay with no deposit, before the Bronze Age | | "no clay deposit within three tiles, and clay can be dug from the ground only once your civilization reaches the Bronze Age (KEY-3)" |
| Labor, civic building or stone walls | **Corvée** at the town (builders work half again as fast for two seasons; opens the existing Decree forecast) | |
| A structure holds the slot while stuck | **Ask for another** (build focus), or **set it aside** (Iron and civic) | |
| Needs another structure (kiln, walls, harbor, road, temple, market) | **Ask the town to build the prerequisite first** | |
| Too small, wrong site, rank, one per civilization | | the rule in words, e.g. "The town needs 20 people first." / "Markets go to your civilization's largest towns, and this is not among them." |
| Slow supply (a flowing good, nothing to order) | | "Nothing you can order speeds this up; it is a matter of seasons." Redirect is still offered. |

Not levers, and said so: trade cannot move wood, stone, clay or bricks (a Compact carries only tin, copper, bronze, salt, gold,
food, luxuries and iron). The material share of a town's work (about 18%) and its split between timber, clay and crafts is not
player-controlled.

## 4. Redirect: `SetBuildFocus` (CommandKind 27)

`A` = own settlement index, `B` = a focus code (basic structure index; Iron work `64 + kind`; civic building `128 + kind`) or -1 to
clear, `C` = 1 to set that structure aside instead of preferring it. Free, no Mandate, like `SetScoutPolicy`. Applied by the economy
(`MatchModel` routes it with `CompleteProject` and `SetScoutPolicy`). The AI never issues it.

**Validation** (`CanFocusBuild`, also what the panel greys out by): the settlement is the issuer's, not Free Folk, populated; the
structure is learned; its hard rules pass (cap, size, site, prerequisite); it is not a Wonder; for a prefer order on the basic list it
is not one of the foundation structures; the Temple or Palace that makes the town a City comes first (a civic focus is refused
while the City path is open, and the City-path building cannot be set aside). The town's *appetite* does not count against it: the
player may ask for a house the town does not yet need. A refused order changes nothing and leaves the earlier focus in place.

**What it does, per slot**

| Slot | Prefer (`C` = 0) | Skip (`C` = 1) |
|---|---|---|
| Basic | goes into the planner's order right after the foundation rules (storage pit, drying rack, granary, warm homes, lean-to, hearth, first fields) *if it is learned, allowed and paid for*; otherwise the planner carries on as before. Holds no materials and no labor. If it is short of wood, the wood target rises by what it lacks and all (not half) of the material work may cut timber | not offered (an unaffordable structure is already skipped) |
| Iron | the slot switches to it at once; the labor in the one it replaces is shelved and returns when that work is chosen again; it is exempt from the 8-season patience rule, because the player chose to wait | the slot goes to the next wanted work, or empties, with the labor shelved; the skipped work is not picked again |
| Civic | the civic slot switches to it; **the labor already in the previous civic building is lost** (as with a Corvée; the panel says so) | the slot is re-chosen without it |

**Guards**

* The foundation rules always come first, so a focus can never leave a town cold, unhoused or without food storage or fields. It
  does not change the food share of the town's labor at all, so it cannot cause hunger.
* The City path (the Temple or Palace that makes a Town a City) outranks a civic focus.
* `DefenseFirst` works (gatehouse, frontier wall, crane, castle) are not protected from a prefer order; the panel shows them as
  waiting. Their labor is shelved, not lost.
* One focus per town. It lasts at most `BuildFocusSeasons` = 24 seasons (6 years); it also ends when the structure stands
  (built count above the count at the time), when the settlement changes hands or becomes Free Folk, or when cleared.
* The focus is saved state (`SnapshotVersion` 29 to 30). It is hashed only while it exists, so a match that never issues the order
  has the checksum it always had.

## 5. What was measured

`civsim-harness build-census --pack civsim.pack` (new; `BuildCensusCommand.cs`) plays whole AI matches (four Warlord-level AIs, as
`iron-match`) and reads every town's `BuildOutlook` once every four seasons. It changes nothing in the match.

First run: seed 20264001, Iron chapter, 60 minutes (so before the Iron works unlock), 37 towns, 779 town-samples.

### 5.1 What towns are doing

| | Share of town-samples |
|---|---|
| Something under way and fully supplied | 78.6% |
| Waiting on a short good | 13.2% |
| Nothing wanted | 8.2% |
| No structure under way in any slot | 19.1% (85 of those 149 had a wanted structure short of a good) |

### 5.2 Structures learned and what happened to them

Share of town-samples in each state, and how many towns hold one at the end. Houses and fields can be built repeatedly and never read "built".

| Structure | Built | Short of a good | Affordable but waiting | Ruled out here | Held at the end | Unlock to first built (median seasons) |
|---|---|---|---|---|---|---|
| Drying rack | 0% | 100% (meat) | | | 0 / 31 | never |
| Granary | 1% | 98% (bricks) | | | 2 / 29 | 64 (1 civ in 3 never) |
| Kiln | 16% | 72% (clay, then stone) | 12% | | 14 / 31 | 80 (2 civs in 4 never) |
| Orchard | 0% | 46% (saplings) | 53% | | 0 / 29 | never |
| Quern | 21% | 46% (stone) | 32% | | 7 / 26 | 8 |
| Shrine | 0% | 70% (stone) | 2% | 28% (needs 20 people) | 0 / 26 | never |
| Weir | 0% | 42% | 10% | 47% (no free river) | 0 / 29 | never |
| Hillfort | 0% | 21% | 7% | 71% (no high ground) | 0 / 26 | never |
| Lookout post | 9% | 12% | **78%** | | 9 / 31 | 92 (2 civs in 4 never) |
| Thorn barrier | 8% | 22% | **69%** | | 9 / 31 | 112 (2 in 4 never) |
| Totem | 8% | 12% | **78%** | | 8 / 31 | 96 (2 in 4 never) |
| Marker | 9% | 7% | **81%** | | 8 / 31 | 84 (2 in 4 never) |
| Signal post | 9% | 16% | **72%** | | 7 / 31 | 56 (2 in 4 never) |
| Pasture | 2% | 21% | **75%** | | 4 / 31 | 68 (2 in 4 never) |
| Market | 16% | 1% | | 69% (only the largest towns get one) | 7 / 26 | 4 |
| Caravanserai | 0% | | | 100% (needs 2 trade routes) | 0 / 11 | never |
| Palace | 0% | | 10% | 90% (only the capital site) | 0 / 11 | never |
| Hall | 0% | 7% | 7% | 87% (needs a Market) | 0 / 15 | never |

Most common single stops (town-samples): drying rack short of meat 634; granary short of bricks 359; kiln short of clay 261; orchard short
of saplings 166; shrine short of stone 145; thorn barrier short of wood 137; weir ruled out for no river 157; hillfort ruled out for
no high ground 108; Market ruled out because the town is not among the largest 152.

### 5.3 Why researched structures are not built

1. **A rule that cannot be met here.** No river for a weir, no high ground for a hillfort, a village too small for a shrine, not the
   capital site for a Palace, not among the largest towns for a Market, fewer than two routes for a caravanserai, no Market for a Hall.
   These are correct rules, but nothing told the player. The panel now does, with the cause.
2. **The structure's price is above what the gatherers work to (granary, mudbrick house, longhouse, kiln, orchard).** Gatherers keep
   fixed stock levels (clay 10, stone 20, wood for fuel), not what the structure needs. The granary at 80 bricks was reached in 1% of
   town-samples. There is no lever for bricks; the honest options are to learn the project that adds the input, or to build something else.
3. **One structure a season.** `BuildIfReady` completes at most one basic structure a season. Cheap affordable works (lookout, thorn
   barrier, totem, marker, signal post, pasture) sit "affordable but waiting" in 70% to 80% of samples, and in half the civilizations
   none was built in the whole match. The census also prints how often labor was already enough for one more at the end of a season.
4. **A rule that is a dead letter: the drying rack.** It is chosen only when 2 rations of *fresh meat* are in store, but meat is the
   first thing the town eats, so the stock is never there: 0 built in 702 samples. (Proposed fix in §8, not changed here.)
5. **Slot order.** The civic and Iron slots hold their target until built; a Temple that needs 120 bricks and cut stone holds the
   civic slot while a School or Bakery the town could build waits. Redirect addresses this.

Checked and ruled out: a town whose ground cannot take its allowance of fields would pick "field" forever and starve the rest of the
list. The planner picked a field in 159 samples and never with ground missing.

Not measured: the Iron works and late civic buildings (schools, bakeries) in a full-length match. A three-seed Iron-length census was
started but did not finish on the heavily loaded machine; rerun it with
`build-census --pack civsim.pack --seeds 3 --minutes 140`.

## 6. What the player can now do

See §3 for the levers and §4 for the order. The Building tab of the "Your people" sheet shows, for one town:

* what each of the three slots is building and how far it is;
* what the town is waiting on, with the good, what it holds, where it would come from, and either the levers or the plain statement
  that nothing the player can order changes it, with the cause;
* what is ruled out here and why;
* every structure the town can be asked for, its price, whether it can pay now, and a "Build next" button; the chosen one with a
  "Clear" button;
* across all the towns: the learned structures none has built, with the commonest reason.

## 7. Options for agency over what towns build

The goal: the player can see what they researched being built, without the sim overriding itself, and without micro-management.
Clicks are per town per era (an era is 20 to 30 minutes).

| # | Option | Complexity, determinism, snapshot | UI burden / clicks | Helps "researched, never seen" | Interaction with AI and balance |
|---|---|---|---|---|---|
| 1 | **Per-town build focus** (built) | one saved list, hashed only while used; an ordinary replayed command; read in three planners | one tab; 1 click to ask, 1 to clear; about 0 to 3 per town per era | yes, one town at a time | AI never issues it; goldens unchanged; a player who spams it only reorders |
| 2 | **Civilization-wide wishlist** (up to N starred structures; every eligible town prefers them) | one civ-level bitmask in the same saved block; the planners already read a "preferred kind", so it is the same hook with a wider scope | a star per structure in one list; 3 to 6 clicks per era for the whole realm | **best**: one click and every town raises it when it can | none for the AI (no command); can pull a whole realm into walls, so keep the foundation guard and cap N |
| 3 | **Show-me commission**: a one-off Decree that pays for one named structure at a named town, cost shown first | the sim must supply or reserve the missing materials (a goods ledger): the largest change | a decree dialog with forecast, like Corvée | yes, guarantees one | Mandate is the price; it must not invent goods, so it would have to move them from other towns |
| 4 | **Surface "researched but built nowhere"** with the reason (built) | read-only | one list; 0 clicks | yes, it is the explanation | none |
| 5 | **Raise planner priority for structures researched in the last N years** | one comparator in three planners | none | partly | changes AI behavior (same planners) unless human-only, which is an invisible perk: against "no sim overrides" |
| 6 | **Bounded queue per town (depth 1 to 3)** | an ordered list instead of one entry | more clicks and a queue editor | yes | none |
| 7 | **Per-structure "never build this"** (civilization-wide) | one bitmask | a toggle per structure | no (stops wasted labor on works the player does not want) | none for the AI |
| 8 | **Build-out / Balanced / Safe stances** priced in Mandate | a civ setting that changes planner order and build share | one control | yes, but opaque | touches gates A1 to A7 if the AI can pick one; human-only is a hidden bonus |
| 9 | **Relax specific planner overrides** (more than one cheap work a season when labor allows; a drying-rack rule that can be met) | small code changes | none | yes, directly, for cheap works and the rack | changes every civilization, AI included: the A-gates and the pacing check must be rerun |

## 8. Recommendation

1. **Keep the per-town focus (option 1) and the "built nowhere" list (option 4).** They are in. They let the player see why and steer a
   town, at no cost to goldens or the AI.
2. **Next, add the civilization-wide wishlist (option 2) on the same state.** It answers "I researched the Shrine and never saw one"
   with one click rather than one per town and adds no new planner hook. Bound it at 3 starred structures, keep the foundation guard,
   and expire a star after 6 years as the focus does.
3. **Treat option 9 as a balance patch, not an agency feature.** The one-a-season limit is the largest measured cause for cheap works and
   the drying rack's meat rule is a plain bug. Both change all civilizations, so measure them with the M10 gates and the I-T1 pacing
   check, as docs/28 did for its changes.
4. **Do not build options 5 and 8:** each hides a perk or an opaque stance, which is what the player objected to. Build option 3 only if
   playtests show the wishlist is not enough.

## 9. What was built and what was not

Built: the readout (`BuildOutlook`, `BuildText`), the Building tab, the `SetBuildFocus` command and its hooks in the three planners, the
wood lever, the "built nowhere" list, the `build-census` harness command, tests (`BuildOutlookTests`, `BuildFocusTests`,
`BuildTextTests`) and this document.

Proposed, not built: options 2, 3, 6, 7, 8, 9; a Corvée that aims at a basic structure other than walls; a shelf for civic progress
so that switching the civic building does not lose labor; per-good icons (the UI kit has none, so goods are named in words).

Not changed: the planners' rules and order, the labor split, the one-structure-a-season limit, the drying rack rule, balance constants,
the content pack.

### 9.0 Screenshots

`docs/MVP/ui-screens/1600x900-build-*.png`, taken in Godot with `--scenario=build-panel` (live matches: `--start-minute=30 --viewer=1` and
`--start-minute=35 --viewer=2`). `01` and `05` are a town short of wood and the result of asking for a totem (taken before the final
wording pass); `02` is the clay case with its research lever. **`03` and `04` (a stop nothing can fix, and the rules that rule structures
out) use a hand-made outlook drawn through the real renderer**, because no town in the captured matches was in such a state at
minute 14 to 35; the live "built in none of your towns" list beside it is real data. The unresolvable wording itself is pinned by
`BuildTextTests` on live fixture towns.

### 9.1 Goldens and snapshot

* `toy-v1`, `research-v1`, `politics-v1` and the content-pack golden are unchanged. The refactor of the planners into tables and gates was
  checked by the three goldens (the 24-minute politics match reaches the Iron Age) and by a test that compares the new Iron gate with the
  original expression over a matrix of towns.
* `SnapshotVersion` 29 to 30 (the focus list is written after the Iron block). Older saves and replays no longer load, as with earlier bumps.
* A match that never issues `SetBuildFocus` hashes exactly as before: the list is empty and hashed only when it is not.
