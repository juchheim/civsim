# M13 WP-E6-7 slice 5: CER-11 glazed windows and the greenhouse (2026-11-22)

**Status:** built and tested on `codex/e6-discovery`, uncommitted. Gives CER-11's two remaining row effects a consumer ([38 §5.3](../38-discovery-age.md)). Inert outside Discovery; Bronze, Iron and Fief bytes, hashes and snapshots are unchanged (nothing is stored). All numbers are provisional.

## What the slice does

| Part | How |
|---|---|
| **Glazed windows** | A town holding at least one plate glass, with CER-11 learned, has its housing capacity multiplied by the row's `housing_comfort` (x1.05, scaled by the civilization's adoption like any modifier). |
| **Greenhouse** | A town that is glazed *and* has a staffed botanical garden (FOR-09, a botanist in post) needs **6% less winter food** (`GreenhouseWinterShare = 6/100`, provisional). It is applied where the sim already trims need for under-investment, in `ConsumeFood`, so the food ledger and security math are unchanged. |
| **Demand** | A town with CER-11 and a staffed garden wants one plate glass in store. The glass is held, not used up. |

## Design choices worth knowing

1. **The row gives no winter number**, so 6% is mine: a few glazed beds on top of a field economy. If playtests show winter hunger is not the issue, it is a small, safe value to change.
2. **Need, not production.** Cutting the need instead of adding food keeps the ledger conserved and needs no new stock. The winter forecast (`ForecastWinterFood`) does not know about it yet, so planners see slightly more winter risk than there is.
3. **Plate glass stays contested.** The Palace spends six from the realm's stores, parts and telescopes spend some, and the glazing wants one per garden town. Salt remains the limit on making any.

## Tests (3 in `DiscoveryGreenhouseTests`, all pass)

The greenhouse needs a staffed garden and held glass together (losing the botanist stops it); glass without CER-11, or outside Discovery, does nothing; a glazed town's housing is the plain value times the factor, between 1 and 1.05.

## Not done

The winter forecast, any view of the greenhouse, and the remaining no-consumer rows (ART-10, COM-11, NUM-10, INQ-09, DIP-09 embassy, BLD-13 layout bonus).
