# M12 WP-E5-7 DIP-08: peaceful Free Folk inheritance slice

**Status (2026-11-14):** A Fief-only DIP-08 draft slice now covers peaceful Free Folk inheritance and alliance continuity across leader succession. The 2% rate, two-season confirmation interval, and neutral seniority rule are unfit hypotheses; this is not balance acceptance or content promotion.

In Fief, a civilization with completed DIP-08 and at least one alliance gets one keyed 2%-per-season roll for each live neutral Free Folk settlement within eight hexes of one of its own or allied settlements. On a hit, the claim remains pending for two seasons. At confirmation, the civilization, alliance, range, and Free Folk status are checked again before ownership transfers. The path does not require war, score rank, a compact, or a payment. This peaceful inheritance path and its draft 2%/two-season values are unchanged by the leader lifecycle slice.

The inheritance draw uses its own Pcg32.ForStream key from match seed, claimant civilization, Free Folk settlement, and season. Pending claimant and confirmation season are hashed and saved in a Fief-only politics tail. Fief civilizations also have a persisted leader `FigureId`, initially elected from living adults (age 14+) by greatest age, then lowest FigureId. This is an explicit neutral seniority convention, not a historical family model. The existing seasonal death events drive succession: if the leader dies, the same election selects a living adult successor; if none exists, the leader id becomes `-1`. A civilization with DIP-08 retains its active alliances. Without DIP-08, its active alliances lapse and Relations remain at the alliance floor (40), preventing immediate re-formation by the Civ Mind. No spouse, heir, or kinship relationship is inferred. Leader IDs participate only in Fief hash/snapshot state; the Fief snapshot version is now 40, while Iron and Bronze stay at version 30 and do not include this state. `FiefDynasticInheritanceTests` continue to cover peaceful inheritance eligibility, timing, and persistence; `FiefLeaderSuccessionTests` cover initial/successor seniority, alliance retention/lapse, no eligible successor, snapshot persistence, and Iron/Bronze isolation.

**Explicitly deferred:** Historical dynastic marriage, spouse links, parent/child links, inheritance by kin, and historical leader identities remain outside this minimal rule. The typed effect requires alliance continuity at leader death; it does not require a genealogical simulation.

## Lifecycle feasibility audit

The population model provides real figure identities, ages, and last-season death events. The draft now uses those existing quantities and events directly: leader assignment follows explicit seniority, and succession consumes only a death event whose FigureId matches that civilization's stored leader. It does not require parent/child or spouse links.

This is the smallest direct implementation of DIP-08's alliance-survival promise that avoids synthetic kinship: store a real leader id, elect by documented seniority, and react to the existing death event. It does not claim a family succession model. The peaceful Free Folk transfer rule remains a separate path.

The values and confirmation timing remain draft. Measure inheritance frequency and consequences in a broader Fief cohort before fitting or promoting the row.
