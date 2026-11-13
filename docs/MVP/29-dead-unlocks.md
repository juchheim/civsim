# 29. Unlocks that did nothing

Status: implemented 2026-11-07. Numbers marked (P) are provisional and not fitted to any acceptance band.

A player can research a project and be told it unlocks a building, and in eight cases nothing in the simulation read
that building. The design was drawn up by Opus and checked against the code before it was built; the full design text
with citations is in the session scratchpad (`dead-unlocks-design.md`) and is summarised here.

## 1. What was actually dead

The brief said eight. The check found seven, and two research effects that rode on the same unlocks:

| Unlock | Era, project | State before |
|---|---|---|
| Seasonal camp | E1, WAY-02 | **Not dead.** Camps are placed by the politics system (`PlaceCamps`), claim land, are saved and drawn. Only the registry cost (0.2 ws) was unused. Registry text now says so. |
| Totem | E1, BEL-02 | no structure, no claim |
| Marker | E1, TER-03 | no structure, no claim |
| Shrine | E2, BEL-05 | no structure; the BEL-05 +3 Cohesion term only counted distance to the capital |
| Signal post | E2, COM-04 | no structure; `Defenses.SignalFires` was set from the research alone and read only by the AI |
| Potter's workshop | E3, CER-04 | no structure |
| Water mill | E4, NRG-03 | no structure |
| Temple tier 2 (colonnade) | E4, STN-12 | no structure; its x0.6 stone labor already applies civilization-wide from the research |

Also found, not fixed here: the built quern is never read, and CER-03's +50% storage is never read.

## 2. What each one does now

| Unlock | Built when | Effect |
|---|---|---|
| Totem | once per settlement of 8 or more, 0.3 ws, 1 L wood (P) | +0.3 claim within 2 tiles (BEL-02 catalog); a one-off +2 stored Cohesion at home (P). Flammable. |
| Marker | up to 2 per settlement of 10 or more, 0.3 ws, 1 L stone or wood | each marker makes one of the settlement's seasonal camps claim 30% more (registry); a settlement with markers and no camps claims the extra from home. Flammable. |
| Shrine | once per settlement of 20 or more, 2 ws, 4 L stone, at least 6 tiles from another own shrine (P) | +0.5 claim within 3 tiles (catalog); a one-off +2 Cohesion to own settlements within 20 tiles (P); from KEY-3 the BEL-05 +3 Cohesion target term also applies within 20 tiles of an own shrine, not only of the capital. |
| Signal post | once per settlement of 12 or more, 0.5 ws, 1 L wood (P) | with COM-04, a post here or at an own settlement within 20 tiles (catalog rule) raises the share of defenders who reach a surprise raid from 40% to 48.4% ("response time -70%", P reading). An alerted raid is unchanged. Flammable. |
| Potter's workshop | once per town of 25 or more with a kiln, 2 ws, 3 L wood, 2 L clay | stored food spoils a further 20% less there (P: pots were folded into CER-02's -25%, so the wheel's output lands on the same thing). |
| Water mill (Iron only) | once, in a town of tier 1 or more on a river with 6 or more fields, 18 ws, 6 block, 8 wood, 1 wheel | grain food value +10% for up to 200 people (P, bakery shape); saw, bellows and Iron forge recipes take half the labor (P), charcoal unchanged. |
| Colonnade (Iron only) | once, in a town of tier 1 or more with a Temple, 16 ws, 8 ashlar, 4 wood | +4 Cohesion target there (P: two thirds of the Temple's own +6). |

Everything above uses existing saved arrays (five more entries in `Structures`, two more in `IronWorks`); no new saved field.

## 3. Hashing and snapshots

* `Structure.Count` doubled as the "nothing queued" marker in the Bronze checksum. The hashed value is pinned to its old
  value (21), and the new kinds enter the checksum only while they stand, so a match that builds none keeps its checksum.
* The Iron structure slots and the Iron shelf do the same (the two new kinds are hashed only while non-zero).
* `SnapshotVersion` 27 to 29 (28 for the Iron slots, 29 for the structure kinds).
* Registry text for the camp, marker, shrine, totem, signal post, potter's workshop and temple tier 2 now matches the code.

## 4. Measured effect

Twelve four-seed Iron matches (seeds 20264001 to 20264012, 48 civilizations), each arm built on the one before:

| | Frontier fix (28 §10) | + water mill, colonnade | + totem, marker, shrine, signal post, potter's workshop |
|---|---|---|---|
| Water mills / colonnades built | 0 / 0 | 15 / 102 | not tracked separately |
| All Iron works | 2742 | 2852 | 2966 |
| People per civilization | 1746 | 1762 | 1795 |
| Settlements per civilization | 23.8 | 23.6 | 24.0 |
| Own KEY-4 to KEY-5, mean | 25.1 min | 24.9 | 25.3 |
| Chapter length, median | 95.8 min | 95.9 | 96.8 |

A separate 90-minute census of one four-civilization match shows every new structure kind being built in the field
(108 towns at minute 90: totems 50, markers 99, signal posts 43, shrines 17, potter's workshops 20).

The additions are small and local, as designed. They do not move the pace you approved, and population is slightly up
(+3%, inside the run-to-run noise of a changed match). The extra claim is not measured as claimed tiles or raids
turned back; the Bronze acceptance gates that look at those were not rerun.

## 5. Goldens and tests

* `toy-v1` and `research-v1` unchanged. `politics-v1` changed on purpose: the 24-minute match now builds totems, markers
  and signal posts. `content-pack` changed (constants, registry text, one catalog effect).
* New tests: five in `IronChainTests` (water mill want, grain value, forge labor, wheel, colonnade) and seven in
  `EarlyUnlockTests` (totem, marker, shrine spacing and the BEL-05 term, potter's workshop, signal fires, claim sources,
  camp markers).

## 6. Not done

* The quern and CER-03 storage effects remain unread.
* Wars, raids and the research pace were not re-measured at acceptance level; the Bronze and Iron acceptance gates were not rerun.
* The Godot side was not touched: no art or view code was added for the five basic structures or the two Iron works, and
  how the renderer treats kinds it does not name was not checked.
