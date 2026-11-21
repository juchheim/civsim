# M13 WP-E6-8 slice 3: FOR-09 botanical gardens, MED-09 anatomy and HUS-11 manure (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted. Third slice of WP-E6-8 ([38 §5.4](../38-discovery-age.md)). Everything is inert outside a Discovery match (the garden gate, the botanist role and both bonuses test `DiscoveryChain`); Bronze, Iron and Fief bytes, hashes and snapshots are unchanged and nothing new is stored: the bonuses read the garden structure and the roles that already exist. The content did not change, so the content-pack golden is untouched. All numbers are the rows' typed values (provisional).

## What the slice does

| Part | How |
|---|---|
| **Botanical garden** (FOR-09) | New structure kind `botanical_garden` (20 work-seasons, 6 stone, 4 wood; the registry's materials). A town with FOR-09 may raise one. The planner wants one only where a **staffed hospital** can use its bonus (`HospitalNotNeeded` otherwise). |
| **Botanist** | New role byte 15, one per garden, taken from the town's adults by the same role pass as the broker (the town keeps four other working adults). **An empty garden has no effect**: the bonus reads a botanist actually at work. |
| **Medical effects x1.15** (FOR-09) | In a settlement with a staffed garden every care multiplier m becomes 1 - (1 - m) x 1.15, the same form as MED-05's. It reaches remedies for illness (0.75), the epidemic hazard's MED-01 term (0.9), midwives (0.6) and wound care (0.7). |
| **Surgery x1.3** (MED-09) | Wound care in a settlement with a **staffed hospital** (physician and two attendants) becomes 1 - (1 - m) x 1.3 after the garden's term: 0.70 becomes 0.61 alone and about 0.56 with a staffed garden. |

| **Stall-fed manure** (HUS-11) | Decision D14 taken as option (b), the recommendation, under standing design autonomy: a Discovery town with HUS-11 and any cattle or sheep gets its field yield x1.05 (the row's `field_fertility`). The winter-loss cut stays prose because domestic herds have no winter loss. 1 test. |

## Design choices worth knowing

1. **Surgery rides on wound care.** The sim has no operation roll; the only treated-injury effect is the wound multiplier. "Surgery success" is therefore a stronger wound treatment in a staffed hospital, which is what the row says ("a hospital with a physician").
2. **MED-09's medical-training speed x2 has no hook.** Physicians are assigned from the town's adults with no training delay, so there is nothing to halve. It stays a typed stat with no consumer; if a physician-training delay is ever added it should read it.
3. **Garden effects are additive to the hospital, not a second hospital.** The garden only strengthens care that already exists; it does nothing in a town with no care roles, which is why the planner asks for a staffed hospital first.
4. **No crop-trial effect yet.** The row says imported crops are trialed in the garden before they spread (FOR-10). FOR-10's x1.2 field bonus already applies without a garden; making it wait for one would gate a whole slice 5 effect behind a third structure. Left as prose.
5. **Cost.** A staffed-garden check scans a settlement's residents only in a Discovery town that has a garden (the rare case), so the care terms add nothing elsewhere.

## Tests (3 in `DiscoveryHealthTests`, all pass)

The garden gate needs FOR-09, is wanted only with a staffed hospital, is one to a town and does not exist in Fief; a garden works only with a botanist and then strengthens care by 15% (an emptied garden does nothing); anatomy strengthens wound care only in a staffed hospital with MED-09, never in Fief.

## Not done

1. **HUS-11 winter loss:** none exists to cut (see D14 below, taken as option b).
2. **Crop trials in the garden** and the **training-speed x2** (see 2 and 4 above).
3. **Views:** no card shows the garden or the botanist (WP-E6-11).
4. **Natural-play measurement:** whether the stock AI raises gardens and what they save is a WP-E6-12 measurement.

## D14 (HUS-11), decided: skip the loss, keep the manure

Domestic herds have no winter loss today, so a x0.2 cut is meaningless until one exists. Options: (a) **add a Discovery-only winter loss** of a fifth of each herd for towns without HUS-11, cut to a twentieth with it, paid in fodder from grain stores; this makes HUS-11 matter but makes E6 harsher for a civilization that has not researched it. (b) **Keep only the manure half** (field_fertility x1.05 where herds and fields share a town) and treat the winter row as prose: no new loss, no fodder good, tiny effect. Recommendation: (b), because the E6 age is already long on new systems and a new loss rule would touch every herding town for one era-six project. Reversible: (a) is a small follow-up if you want it.
