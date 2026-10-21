# 21 — Island crossing: boats that carry parties and settlers

> Status: **decided**, 2026-10-21 (scope, range and AI use were settled with the owner; numbers below are first values to tune in
> Phase 5). Implements the one use of boats the MVP map actually has: reaching the islands. It changes simulation code
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
- The match ends at era 3, before Sail (E4), so crossing between continents never matters in the MVP. 12 §3.1 promised that
  islands are "reachable by outriggers (SEA-04, E2)", but SEA-04 allowed "hops of up to 3 tiles" and the nearest island is 12 away.

So in the MVP, **ferrying has exactly one job: the islands.** Rafts and canoes would only matter for rivers and straits if rivers
became barriers, which is a larger design change and **out of scope** here (decision 1).

## 2. Decisions (owner, 2026-10-21)

1. **Islands only.** Rivers stay non-blocking; SEA-01 to SEA-03 keep their present (cosmetic and unmodelled) effects.
2. **Extend the outrigger range; do not move the islands.** Regenerating the map would change the worldgen golden and every
   balance number. SEA-04 changes from "hops of up to 3 tiles" to a **range measured from land** (§3), with a shipwreck risk.
3. **AI uses boats.** Not by a separate AI layer: every civilization's daughter bands go through the same fission siting, so a
   civilization that knows SEA-04 colonizes islands (the player's too, as bands already settle automatically).

## 3. Rules

**Outrigger voyages (SEA-04).** A civilization that knows SEA-04 can travel over open water. A water tile is enterable by sea if it is
at most `OpenSeaReach = 11` tiles from the nearest land (hex distance). The farthest island is 21 tiles from a continent, so its
midpoint is 10.5 from land: reachable. Between the continents the midpoint is 16.5: **not** reachable, so nobody crosses to the
other continent before Sail. In the cost graph (`HexGrid.WalkingCostsFrom`, tenths of a tile) a sea tile costs
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
   continent never; walking routes unchanged; shipwreck rate matches the formula.
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
