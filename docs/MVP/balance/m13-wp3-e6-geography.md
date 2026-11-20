# M13 WP-E6-3: E6 geography audit (coal, sugarcane, the sea)

**Status:** audit only. No world generation, worldgen golden, simulation golden or content was changed. Baseline: branch `codex/e6-coal-audit`, cut from `codex/e6-discovery` at `94a375f`. Raw output: [m13-wp3-e6-geography-output.txt](m13-wp3-e6-geography-output.txt); every number below is copied from it unless marked as derived from the per-start CSV or from the counterfactual run in "Fix check".

## Result in brief (seeds 1–10,000; 40,000 starts, 20,000 geometrically distinct)

| Question | Answer |
|---|---|
| **Can every start satisfy `[Seen: Coal]` (MIN-09, on the KEY-7 path)?** | **No. 28.7% of starts (11,494 of 40,000) have no coal sign within reach 52 (reach 35 with WAY-01 x1.5), and 30.1% (12,046) none within 35 (no WAY-01).** In **28.3% of worlds (2,834 of 10,000)** there is no dark-seam sign anywhere on the map, so all four civilizations there can never see coal through their settlements and MIN-09 stays Hidden. Under today's gate that is the share for which **KEY-7 is unreachable by settlement exposure alone**. |
| Why? | Not geography: **a world-generation defect.** `EnsureBarrenEvidence` is meant to guarantee a dark seam near the starts, but the `OilSeep` guarantee that runs straight after it can pick the same tile and overwrites it. Confirmed by a throwaway patch (see "Fix check"); the generator in this branch is untouched. |
| Is there a rescue already in the sim? | Yes, but it needs an action: an **Expedition** (needs WAY-04) that crosses a coal *deposit* tile marks Coal seen (`PoliticsSystem.CommitReveal`). A coal deposit lies within 35 of **100%** of starts and within 25 of 99.6%, and the expedition range is 2 x reach = 70, so the rescue is geometrically always available. Whether the AI sends such an expedition is not audited here. Barter cannot help: the world is symmetric, so a world with no seam has none on either continent. |
| Is there coal to mine? | **Always.** Exactly 24 coal deposit tiles in every world (all on organic-basin ground), on both continents. Nearest coal: median 8 hex, p95 18, p99 23, max 35. **31.5%** of starts have an uncontested own-continent coal tile inside the claim radius 5; 92.0% within 16; **99.7%** within 35. No start lacks coal on its own continent; no start needs a sea crossing for coal. |
| What would MIN-08 (E5 mine surveying) have to reveal? | Today it reveals **nothing** (prose-only `reveal` effect, no radius, no code). As written ("own territory", radius 5) it would name coal for 31.5% of starts. To close the gap with the seam path: radius **16 -> 98.1%**, **25 -> 99.9%**, **35 (the era-6 reach) -> 100%** of starts. |
| Sugarcane (FOD-10 `[Has: Sugarcane]`) | **79.0% of worlds have no sugarcane at all** (tiles are 0 or 12 per world). 21.0% of starts have some on their own continent, 19.8% within 35, 14.3% uncontested within 35, 1.1% inside the claim radius. Overseas-only: 0% (it is symmetric). **The sim does not evaluate this gate today** (`HasMask` defaults to all-ones; only Tin and Arsenic ore are decided), so FOD-10 is open to everyone now. |
| Overseas | The continents are **31–39 tiles apart** (median 35; doc 21 said 33). The nearest foreign start is 43–125 hex away (median 97) and **100% are reachable by the existing outrigger rule in exactly 2 legs** (one island stepping stone), at 24–103 tiles of outrigger cost = **3–11 seasons**. A **one-leg** direct crossing needs an open-sea reach of **15–19** (median 17); reach 16 does it in 28.5% of worlds, **20 in 100%**. |

## What it means for E6

1. **KEY-7 can soft-lock on geography, and the cause is a one-line generator bug, not the design.** The gate reads the dark *seam* sign, and 28.3% of worlds lost theirs. This is exactly the case [doc 38 §3.2](../38-discovery-age.md) and D13 warned about, and the answer is material: roughly 3 starts in 10 are affected, whole worlds at a time.
2. **The coal itself is not scarce.** Whoever can see coal can mine it nearby (typically 8 tiles). The hard cases for coal are the *knowing* and the *depth* (every coal tile is depth 2 or 3, so drainage from NRG-06 matters), not the distance. Only 94 starts (0.2%) see a seam but have no uncontested own coal within 35.
3. **A seam is not a coal tile.** Only 0–8 of the 24 coal tiles carry a seam sign (at least one does in 7.3% of worlds), so the gate and the mine need separate coverage; the table above gives both.
4. **"Oceans open" is already open for contact.** A foreign start is 3–11 seasons away by outrigger in 2 legs, at the existing reach of 11. E6's reach rule therefore changes *how* the sea is used (one leg, a safer voyage, colonies), not *whether*, as doc 38 §12 suspected. If SEA-12 is meant to cross in one leg it needs a reach of 20 (22 is 2 x 11); 16 would work in under a third of worlds.
5. **Sugarcane is a trade or world-gen question, not a distance one.** With no sugar in 79% of worlds, a `[Has: Sugarcane]` gate would lock FOD-10 for all four civilizations in four worlds out of five. FOD-10 is not on the KEY-7 path (doc 38 §3.1), so it degrades gracefully, but the E6 design should not promise sugar to everyone.
6. **Saltpeter** was audited in E5 ([m12-wp3](m12-wp3-e5-geography.md)): 12 deposits every world, 74.1% of starts have it on their own ground. Reused, not redone.

## Recommendation for D13 (coal visibility)

Do **not** weaken the `[Seen: Coal]` gate. Pick the smallest fix that closes the 28.7%:

| Option | Closes | Cost and risk |
|---|---|---|
| **A. Type MIN-08's reveal: coal deposits within the civilization's reach become known (use the era reach, 25 at E5 and 35 at E6), feeding the same Seen mask.** | 99.9% at 25 (a start is always on E5 reach when MIN-08 lands), **100% at 35** | A few lines in `LivingEconomySource` (add coal deposit tiles to the Coal target list once MIN-08 is done) plus the typed MIN-08 value. Touches only E5/E6 behavior; worldgen and the E0–E4 goldens are untouched. Matches doc 38's own default ("MIN-08 surveying plus contact and trade reveal coal"). **Recommended.** |
| B. Fix the generator (the oil guarantee must skip a tile that already carries evidence) | seams in every world; seam within 35 for 99.7% of starts and within 52 for 100% (2,000-seed check, 0% of worlds without a seam) | One line, but it moves the **worldgen golden and every golden downstream of world generation** in about 28% of worlds. Right thing to fix, wrong thing to do inside E6. Schedule it for a deliberate golden refresh. |
| C. Rely on the explorers' path only | 100% geometrically | Needs a player or AI action to dispatch an Expedition at a coal tile; a human who does not know about it is silently locked out of KEY-7. Keep it as the second path, not the answer. |
| D. MIN-08 as literally written (own territory, radius 5) | 80.5% (seam within 52 or coal within 5) | Leaves about one start in five locked. Not enough. |

With A, the numbers for the union of "seam within reach" and "MIN-08 names a coal tile within R" (share of all starts): R = 5: 80.5% (seam 52) / 79.5% (seam 35); R = 8: 87.8% / 87.2%; R = 16: 98.1% / 97.9%; **R = 25: 99.9% / 99.9%; R = 35: 100% / 100%.** Among the 12,046 starts with no seam within 35, a reveal of radius 5 rescues 31.9%, 8: 57.5%, 16: 93.1%, 25: 99.6%, 35: 100% (derived from the CSV).

The AI forecast must read the same rule, so it never plans around a gate it cannot satisfy (the E5 lesson for `[Seen: Wild horses]`).

## Method (definitions fixed before any result was read)

- **Code:** [E6GeographyAudit.cs](../../../src/CivSim.Harness/E6GeographyAudit.cs) (new, read-only: it calls `WorldGenerator.Generate` and writes nothing back), a dispatch line in `Program.cs`, and [E6GeographyAuditTests.cs](../../../src/CivSim.Tests/E6GeographyAuditTests.cs) (thread-count independence, pinned digest, every printed share within 0–100%, counter consistency per start, mirror symmetry, worldgen golden unchanged). Pattern: [E5GeographyAudit.cs](../../../src/CivSim.Harness/E5GeographyAudit.cs).
- **How the sim evaluates `[Seen: Coal]`** (read from source): `LivingEconomySource` builds the "Coal" target list from tiles whose `SurfaceEvidence` is `DarkSeam`, **not** from coal deposits. The gate opens when such a tile is within the civilization's era reach (35 at E6, x1.5 with WAY-01 = 52) of any settlement, by hex distance, across water and borders. Other paths: an Expedition's reveal marks Coal when it covers a coal *deposit* tile (WAY-04 required); barter and contact share it (Coal is a traded good; `KnownByContact` makes a project known when a contact completed it). A `[Seen]` project with the gate unmet is **Hidden**, not Glimpsed, so KEY-7 can sit on the horizon forever.
- **Seam** = a land tile with `SurfaceEvidence == DarkSeam`. **Coal deposit** = `Deposit == Coal`. **Sugarcane** = `Deposit == Sugarcane`.
- **Distances** are hex distances ignoring terrain, as the sim measures `[Seen]`; walking cost is the least land cost (biome costs of 12 §4, one-tile straits crossable) in tiles; sail adds the existing outrigger rule (`OpenSeaReach = 11`, `SeaCostTenths = 4`).
- **Reach and radii:** claim radius 5, mining reach 8 and 16, E5 reach 25, E6 reach 35, WAY-01 52 (`floor(35 x 1.5)`), one fission hop 14. **Uncontested** = same continent and strictly nearer this start than any other start (as in E5).
- **Coal ladder** (own-ground reading, as the E5 audit's): 1 uncontested within the claim radius, 2 within 16, 3 within 35, 4 same continent within 35 but only nearer a rival, 5 same continent beyond 35, 6 another continent only, 7 none on the map. Result: **31.5 / 60.6 / 7.7 / 0.3 / 0 / 0 / 0%**.
- **Sea legs:** landmasses are the two continents (one node each) and each island; a *leg* is a hop between two landmasses in which every water tile lies within R of one of the two (so a hop never leans on a third coast). Fewest legs from continent 0 to continent 1 at R = 11, 16, 22. The one-leg reach is the least R for the direct hop with islands unusable. The continuous-sail reach (islands as ordinary coast, the existing rule) is the least over all water routes of the largest distance-from-land met (5–7).
- **Static starts only:** each start is a lone settlement; the sim measures `[Seen]` from every settlement, so real exposure grows with the realm (one fission hop adds up to 14 tiles).
- **Reproduce:**

```bash
export DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec
dotnet run --project src/CivSim.Harness -c Release -- e6-audit --first 1 --seeds 10000 --csv /tmp/e6-audit.csv   # about 10 to 25 minutes
```

  Digest over all 40,000 per-start records: `74471f3a7feb6c9cdb327e4a934b613bcbfd3c9c4f55339a7daee2becbeef1b9`. The two mirrored start pairs agree in all 20,000 pairs.

## Fix check (counterfactual, not committed)

To confirm the cause and size option B, a throwaway copy of the source outside the repository skipped tiles that already carry surface evidence in the `OilSeep` guarantee (one added condition in `EnsureBarrenEvidence`) and re-ran the audit over seeds 1–2,000 (8,000 starts). Result: **0.0% of worlds without a seam** (was 28.3%); seam within 35 for 99.7% of starts (28 starts, all with a mate that sees one), within 52 for 100%. The branch's `WorldGenerator.cs` was not modified, and its worldgen golden is unchanged.

## Other numbers kept for the E6 plan

- Coal deposits: nearest by least walking cost median 8 tiles, p95 23, p99 33, max 52; by outrigger rule max 47. Summed deposit quantity (1–3 per tile) within 35 and uncontested: median 11 (p5 5, p95 17). Starts with no walking route to any coal: 0%.
- Seam signs per world: median 4, max 76; 28.3% have none. Of the 30 further worlds (0.3%) that have a seam but none within 52 of any start, and 19 mixed worlds, the pair geometry decides; they are too few to matter.
- Foreign land is **never** within the E6 reach of 35 by hex distance, and within 52 for 28.5% of starts. Nearest island tile 18–55 hex (11–57 by outrigger cost), nearest tile of the other continent 36–76 hex.
- First contact over the sea needs 3–11 seasons of travel at 10 tiles a season once SEA-04 is known; in doc 21's eight-seed games the first cross-continent contact fell at season 123–140 because the AI learns SEA-04 late. D-T5 should measure *when* the AI crosses, not *whether it can*.

## Limits

- Start positions only; no expansion or rivals' growth, no contact or barter modeled beyond the mate and world rows. The expedition rescue is shown as geometry (a coal tile within range), not as an event that actually happens in play.
- Deposit **knowledge** versus deposit **existence** is kept apart on purpose: the gate reads seams, the mine needs deposits. WP-E6-5's `MineCheck` does not list coal yet.
- All four starts per world are two geometric configurations (civ 2 and 3 mirror civ 0 and 1), so "per world" figures rest on 20,000 distinct starts, not 40,000 independent ones.
- The WAY-01 reach (52) assumes WAY-01 (E0) is done by E6; the strict reading without it is the 35 row.

## Decisions this suggests (for you, plain English)

1. **Coal visibility (D13).** About 3 starts in 10 can never *see* coal under today's rule, because the map generator accidentally erases the coal sign in 28% of worlds. Recommend making MIN-08 (mine surveying) reveal coal within the civilization's reach, so every start learns of its nearby coal one way or the other (100% at the E6 reach). Keep the `[Seen: Coal]` gate as written.
2. **The generator bug.** Recommend fixing the one-line oil/coal sign collision at the next deliberate golden refresh, not inside E6, because it changes every world-based golden.
3. **Sugar.** Recommend treating sugarcane as a trade good that most worlds lack (79%) and not gating anything important on it; or ask for a guaranteed sugar region if you want every civilization to be able to refine it.
4. **SEA-12 reach.** Recommend a one-leg ocean reach of 20 (22 would be the "double outrigger"), since 16 only crosses in about a quarter of worlds and the existing reach already crosses in two legs.

None of these is built; they are inputs for WP-E6-5 onward.
