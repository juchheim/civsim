# 21 — Island crossing: boats that carry parties and settlers

> Status: **decided and implemented** (phases 1 to 5), 2026-10-21 (scope, range and AI use were settled with the owner; numbers
> below are first values, checked in §7). Implements the one use of boats the MVP map actually has: reaching the islands. It changes simulation code
> (travel, contact and daughter-village siting for civilizations that know SEA-04) and **appends** event kinds; it changes no
> command, no replay format and no existing numbering. Sources checked: 05 §6.5 and §9.4, 12 §3.1 and §9.3, 13 §3 to §5, 14 §8,
> `PoliticsSystem*.cs`, `LivingEconomyModel.cs` (`ChooseFissionSite`), `HexGrid.cs`, `FrameBuilder.cs`.

## 1. Why this, and why only this

What the sim does today, measured over five seeds and 20 to 30 minutes of play:

- The map is **two continents of about 3,150 tiles and twelve islands of 7 tiles**. The continents are 33 tiles apart; the islands
  lie 12 to 21 tiles off the nearest continent and 26 to 37 tiles from the nearest start village.
- **Nobody ever crosses water.** No figure and no party was ever on a water tile, and no village was ever founded on a different
  landmass from its civilization's first. Walkers wade the shallows next to land (`HexGrid.BuildWalkingPassability`) and nothing
  else.
- The SEA projects declare effects (`raft_crossing`, a `paddle` method) that **nothing reads**. Fishing is rare (two or three
  fishers per game, all on river tiles) and the economy does not simulate paddling.
- `ChooseFissionSite` filters daughter-village sites by habitability and a hex distance of 14, with **no reachability test**. It
  has never put a village across water only because the islands are too far and too poor; the code allows it.
- The match ends at era 3, before Sail (E4), so no one crosses by Sail in the MVP. 12 §3.1 promised that
  islands are "reachable by outriggers (SEA-04, E2)", but SEA-04 allowed "hops of up to 3 tiles" and the nearest island is 12 away.

So in the MVP, **ferrying has exactly one job: the islands.** Rafts and canoes would only matter for rivers and straits if rivers
became barriers, which is a larger design change and **out of scope** here (decision 1).

## 2. Decisions (owner, 2026-10-21)

1. **Islands only.** Rivers stay non-blocking; SEA-01 to SEA-03 keep their present (cosmetic and unmodelled) effects.
2. **Extend the outrigger range; do not move the islands.** Regenerating the map would change the worldgen golden and every
   balance number. SEA-04 changes from "hops of up to 3 tiles" to a **range measured from land** (§3), with a shipwreck risk.
3. **AI uses boats.** Not by a separate AI layer: every civilization's daughter bands go through the same fission siting, so a
   civilization that knows SEA-04 colonizes islands (the player's too, as bands already settle automatically).
4. **No carve-outs.** If boats can cross, they cross for everything: gifts, barter, raids, assaults and capture, first contact and
   settlers alike, from island to island and on to the other continent. A first version kept the continents apart with a "realm"
   rule (a boat may land only on its own continent and that continent's islands); the owner rejected it as a rule that breaks
   logic to preserve a status quo, and it was removed. What the game does with the extra reach is measured in §7, not prevented.

## 3. Rules

**Outrigger voyages (SEA-04).** A civilization that knows SEA-04 can travel over open water. A water tile is enterable by sea if it is
at most `OpenSeaReach = 11` tiles from the nearest land (hex distance). The farthest island is 21 tiles from a continent, so its
midpoint is 10.5 from land: reachable. The 33-tile gap between the continents is not crossable in one leg (its midpoint is 16.5
from land), but islands are land too, and an island 20 tiles from one continent can sit 13 from the other: **islands are ordinary
stepping stones**, and a boat that can sail there can sail on. There is **no special rule** for them (see decision 4). In the cost graph (`HexGrid.WalkingCostsFrom`, tenths of a tile) a sea tile costs
`SeaCostTenths = 4` (outrigger method multiplier 2.5), against 10 to 40 for land. Everything is integer and deterministic.

**Walking stays the default.** A route is first looked up on the existing walking graph; the sea graph is used **only when that
finds no route and the traveller knows SEA-04**. Every existing walkable route therefore keeps exactly its length, so goldens
move only when someone actually sails.

**What sails.** Gift, barter, raid and assault parties, first-contact detection, and daughter bands (settlers). There is no
amphibious combat penalty in the MVP.

**Boats are implicit.** No new structure, no wood bookkeeping: SEA-04 plus a route is enough. (The catalog's "4 L" of wood per
dugout is left to a later economy pass.) Visually the moored fleet from the boats feature stands for the boats.

**Shipwreck.** Each voyage rolls once, at arrival (parties) or when the band sets out (settlers), on its own Pcg32 stream
(`politics.voyage`, keyed by origin, target and season, so replays and reloads agree):

`risk = min(400, 30 + 8 × open-sea tiles on the route)` permille: about 20 % for the 21-tile crossing to the farthest island, about
12 % to the nearest.

A lost voyage **turns back**: parties return home with their goods, the daughter band stays and may try again, nobody dies, and a
`VoyageLost` event is logged. (Deaths and lost goods are a later balance lever.)

**Settling.** `SettleReach` (the hex-distance cap for daughter sites) is 14 on land and **24** for a civilization with SEA-04. A site
that cannot be walked to is allowed only by sea. Island sites get an **×1.5 site-score bonus** ("early-reach prizes", 12 §3.1) while
no village of the civilization stands on that island. Rival-owned ground and the rival-doorstep rule still apply (`Allows`).

**Contact.** First contact and Awareness may also come over the sea route, for the same reach rules, when either side knows SEA-04.

## 4. What changes

| Area | Change |
|------|--------|
| `World/HexGrid.cs` | `LandDistances` (hex distance to nearest land); `WalkingCostsFrom` takes optional sea reach and sea cost |
| `Politics/PoliticsSystem*.cs` | `Travel` (walk first, sail as fallback), cached per origin per season; `TravelSeasons`, `CanReach`, voyage hazard; the five `Walk` call sites; contact over sea; `IFissionSiteAdvisor` gains `SettleReach`, `Reachable`, `VoyageLost`; island site bonus |
| `Economy/LivingEconomyModel*.cs` | `ChooseFissionSite` asks the advisor for reach and reachability; a lost voyage cancels the fission for the season |
| `Politics/PoliticsSystem.cs` | `PoliticsEventKind.VoyageLost` (appended) |
| `View/*` | `ChronicleKey.VoyageLost`, `IslandSettled` (appended); the frame's party travel time comes from `TravelSeasons`; settlements founded on an island flag the event |
| `content/catalog` (SEA-04) | note text: range from land instead of "3 tiles"; content-pack golden refreshed |
| `game/` | a party on a water tile is drawn in a boat of its civilization's tier; Chronicle texts |
| docs | 12 §3.1 (range, not hops), this note |

No `Command`, no replay field and no snapshot layout changes: the fate of a voyage is recomputed from its key, not stored.

## 5. Phases

1. **Reachability honesty.** A route function and a reachability check for daughter sites and parties; no gameplay change. Gate:
   all goldens unchanged, a test that nothing is settled or sent somewhere unreachable.
2. **Crossing by sea.** Sea cost graph, `Travel`, hazard, contact over sea. Gate: islands reachable only with SEA-04; the other
   continent only through the islands; walking routes unchanged; shipwreck rate matches the formula.
3. **Use it.** Island siting bonus, `SettleReach`, events. Gate: with SEA-04, a civilization founds island villages; without it
   none ever does.
4. **View.** Party boat, Chronicle texts, island flag on the founding event. Gate: scripted screenshots of a voyage and an island
   village.
5. **Balance and verification.** Run the aggregate models for the island prize, fog fairness across water, refresh
   `politics-v1`/`research-v1` only if AI behaviour moved (explained in the commit), add a voyage replay to the corpus, extend the
   M9 protocol with an island scenario.

## 6. Risks

- **Golden churn.** Phases 1 and 2 are written so existing walkable routes are untouched; Phase 3 may move `politics-v1` once AI
  civilizations learn SEA-04 and colonize. Check, then explain.
- **Island snowball.** Twelve 7-tile islands, a ×1.5 siting bonus and a 20 % loss risk are first values; if one civilization
  snowballs, lower the bonus or raise the risk (Phase 5).
- **Fog.** The AI sees only its own view; siting uses the true world, as it already does for land. Revisit if fairness tests object.
- **Free Folk.** A civilization's island village can later become Free Folk like any other; no special case.

## 7. As built, and Phase 5 results (2026-10-21)

**As built.** All five phases. Differences from the plan above, decided while building:

- Islands are ordinary **stepping stones** (decision 4). The first version had a realm rule against hopping continent to continent
  through an island 13 tiles from the far shore; it was removed, and a test now asserts the opposite.
- Only the **outbound** leg of a trip rolls for shipwreck; a barter party's return leg does not.
- Settlers' crossing is **instant**, like every daughter band (the economy moves them when the band is founded); only the
  shipwreck roll and the new village are visible, plus the flotilla of boats the fleet shows at the island village. Parties
  (gift, barter, raid, assault) really travel, and are drawn in outriggers on open water.
- The `VoyageLost` view event and an `IslandSettled` founding flag feed the Chronicle and two toasts.

**Balance check.** Eight seeds, 35 minutes, four civilizations (the player's council plus three AI):

| Condition | Island villages found (8 seeds) | Most held by one civilization | Voyages lost |
|-----------|---------------------------------|-------------------------------|--------------|
| SEA-04 given to everyone at the start | 20 in four seeds | 3 of 12 islands | 3 |
| No grant (the AI learns it when it learns it) | 4 (AI civilizations, in four seeds) | 1 | 1 |

Totals of population by civilization stay in the same range with and without the grant, so islands are a prize, not a
runaway. The shipwreck rate matches the formula in a 4,000-roll test. First values to revisit after human playtests: the x1.5
siting bonus and the 12 to 20 % risk.

**Gates.** Goldens `toy-v1`, `research-v1` and `politics-v1` unchanged; `content-pack.txt` refreshed (SEA-04's note). Tests
(`IslandCrossingTests`): islands, and through them the other continent, reachable only by outrigger; walkable routes keep their length; the
risk formula and reproducible rolls; no island village without a civilization that sails; and a raid from an island colony is seen
on water tiles in the frame.

**Dev aid.** `--start-minute=25 --dev-island-colony --scenario=voyage` (debug builds only) puts a large colony on an island and
sends a raid at a rival, to look at a sailing party. It writes the economy directly, outside the command path, so such a match is
never a valid replay.

**Free crossing between continents (decision 4), measured.** Eight seeds, 35 minutes, the same four civilizations, compared with the
version that had the realm rule:

| | Realm rule | Free crossing | Free crossing, SEA-04 given to everyone |
|---|---|---|---|
| Cross-continent contacts (of 32 possible pairs) | 0 | 7, first at season 123 to 140 | 8, first at season 31 to 134 |
| Gifts / barter trips (8 games) | 49 / 53 | 55 / 53 | 86 / 68 |
| Raids, wars, captures | 0 | 0 | 0 |
| Villages on a foreign continent | 0 | 0 | 0 |
| Voyages lost | 1 | 2 | 20 |

In a normal game the AI learns SEA-04 late, so the continents meet around minute 31 to 35, too late for anything to happen:
no raid, war or capture occurred in any run, including on the shared continent. Given outriggers at the start, contact comes
earlier (season 31 at best) and brings more trade and more lost voyages, still without conflict. No civilization settled the
far continent in 35 minutes (bands settle at most 24 tiles from a parent). Raids and wars are rare in 35-minute games of this
kind generally, so this says the extra reach costs nothing visible, not that conflict across the sea has been exercised; the
Phase 5 replay corpus should add a long game with SEA-04 given early and war ordered.
