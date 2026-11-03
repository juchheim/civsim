# M11 WP7 — The state layer (batch 2 notes)

What the Iron chapter's government does in the economy, the provisional numbers, and what is still to build. Everything runs only in the Iron chapter; the Bronze economy and its hashes are unchanged. Decisions Q4 and Q5 (2026-11-03) are applied: a feudal lord removes distance and span pressure only, and tribute moves points once and never counts as land.

## Findings that shaped this batch
- **The E3 state layer already reads `admin` effects generically** (Admin span and Overreach relief are sums over completed projects). Left alone, the Iron chapter's GOV-07 (−25%) and TER-08 (−30%) would have applied to every settlement the moment they were researched, with no administrator and no governor. They are now scoped (below).
- **Cohesion terms were built only from E3 projects**, so the Iron chapter's GOV-08, BEL-08 and ART-08 cohesion effects did nothing. E4 terms are now built and read only in the Iron chapter, with two scopes named: ART-08's `local_city` (only a town with a theatre) and BEL-08's `diverse_group_integration` (only a town still carrying the penalty of having been conquered). Before they were named they would have fallen through to "everywhere".

## Built
- **State roles.** Two roles join Priest and Scribe in `StaffRoles`: an Administrator at the capital (GOV-07) and a Governor at each province seat (TER-08). A role holder works the role all season (practice `council`), is kept from season to season, and never takes the last four working adults from a settlement.
- **Census (GOV-07).** The Overreach relief of −25% applies civilization-wide only while the capital has an administrator in post. A vacant office gives nothing.
- **Provinces (TER-08).** A town other than the capital, joined to it by land, keeps a governor if it is itself beyond the free Overreach distance or has a member settlement within 12 walking tiles that is. A settlement is governed by the nearest seat (walking tiles, then lowest index) within 12 tiles, joined by land, whose governor is in post. The assignment is read from the staffed roles each time, so it is rebuilt every season and a seat that loses its governor, or is cut off or captured, projects nothing. A governed settlement gets −30% Overreach.
- **Feudal contract (GOV-09).** In a governed province, distance and Admin-span strain on Cohesion are removed. Famine, defeat, raids, crowding and the rest are untouched, so a contract never makes a province secession-proof (Q4). The catalog's local defense ×1.15 waits for the military batch.
- **Citizenship (SOC-10).** The distance strain on Cohesion is halved civilization-wide (it reads the catalog's mod).
- **Coinage (TRD-06).** Its Admin span +1 already applied through the generic span sum; trade friction ×0.6 waits for the trade batch.
- **Republic (GOV-08), philosophy (BEL-08), theatre (ART-08).** Cohesion targets +10, +10 (integration) and +15 (a town with a theatre). The theatre is a new Iron structure (12 blocks, 4 wood, 24 ws), wanted by a town of 50 or more with none.

## Tests
`IronChainTests`: cohesion terms apply only where their scope holds; an administrator and a governor are staffed and their relief depends on being in post; a feudal contract zeroes distance and span strain in a governed province and a province without it still feels both.

## Still to build in the state layer
- **Roads and bridges (TRN-07/08) and courier relays (COM-08):** relays' Overreach relief is deliberately not applied yet, because it is defined along maintained roads.
- **Colonies (TER-09)**, **tributary states (DIP-06)** and **leagues (DIP-07)**.
- The Insight cost of a feudal contract ("less central extraction") has no number in the catalog or 26 and is not modelled.
- The civ-wide `voluntary_war_declaration_cooldown` of GOV-08 and the unrest-pressure mod of ART-08 belong to the politics batch.
