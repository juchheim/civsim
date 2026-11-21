# M13 WP-E6-7 slice 2: LRN-09 learned societies (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted. Second slice of WP-E6-7 ([38 §5.3](../38-discovery-age.md)). Everything tests `DiscoveryChain` or `Rules.IsDiscovery`; Bronze, Iron and Fief bytes, hashes and snapshots are unchanged and nothing new is stored (the society is a new `IronStructure` kind written only in Discovery snapshots). Content is unchanged, so the content-pack golden is untouched. All numbers are the row's typed values or provisional.

## What the slice does

| Part | How |
|---|---|
| **Learned society** (LRN-09) | New structure kind `learned_society` (24 work-seasons, 10 block, 4 wood, 4 planks: the registry's materials). Hard gate: a town with LRN-09, one to a town, at most two to a civilization. The planner wants one only in a town that already has an academy or a university (`NoScholarsHere`). |
| **Scholars** | A society seats **two scholars**, added to the town's scholar slots beside the academy and university. **An empty society has no effect**: the bonuses need a scholar actually at work in one (`SocietyStaffed`, read by research through the source interface). |
| **Know-how spillover** | In a Discovery match with a staffed society, a project may draw on **a tenth of every other lineage's Know-how** (the row's typed 0.1, read from the catalog), on top of its own lineage's pool. The same two caps as ordinary Know-how apply: at most the Insight put in this step, at most half the project's cost. The donor lineage pays **ten for each one given** (the "counts toward" wording), and the retention buckets are debited to match. |
| **Diffusion bonus** | LRN-09's x1.1 on the diffusion step already flowed in through the typed `diffusion_discount` modifier once COM-10 stood, with no condition. It now applies **only while a society is staffed**: otherwise the research side divides the row's own factor (adoption and pioneer scaling included) back out. The floor still holds. |

## Design choices worth knowing

1. **The research reads the economy through one new source member** (`IResearchSource.SocietyStaffed`, default false), the same pattern as the Know-how retention members. Sources without societies are unchanged.
2. **DIP-09's discount stays unconditional.** A resident embassy needs a diplomatic action and an embassy object that do not exist; its x1.1 keeps flowing from the typed modifier alone, and the "embassy" stays prose. It is the next candidate in this work package.
3. **Two scholar seats a society** is a provisional number (an academy gives one). The scholars' own Insight (48 each) is the society's other, larger effect, and is not new machinery.
4. **Cap of two** is by works standing when a town considers building: simultaneous builds can overshoot. In a 130-minute natural run the four civilizations built 10 societies (with 20 academies and 40 universities), so the stock AI uses it freely.

## Tests (4 in `DiscoverySocietyTests`, all pass)

The gate needs LRN-09, is wanted only where scholars gather and is one to a town, not in Fief; a society seats scholars and works only while one sits in it; the diffusion bonus applies only with a staffed society (an empty one's share is taken back out to within rounding); a staffed society lets a tenth of other lineages' Know-how count toward a project and the donors pay ten for one.

## Checks

Full suite 1,614 passed with the 5 known failures; toy, research and politics goldens unchanged.

## Not done

DIP-09 embassies, tower sight (SEA/WAY), the instrument and glass goods (WAY-10's chronometer good), COM-11 newspapers, ART-10 patronage, BLD-13 Palace Wonder, SOC-12 goods demand; views.
