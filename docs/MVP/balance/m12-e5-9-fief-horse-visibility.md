# WP-E5-9 Fief horse-visibility planning gate (2026-11-14)

Fief AI belief now tracks wild horse source knowledge as `Unknown`, `Seen`, or `ConfirmedAbsent`. The belief builder checks a herd's species and live animal count only after its current tile is explored. `ConfirmedAbsent` is emitted only when every map tile has been explored and no live horse herd was observed. `Seen` is retained once learned. These rules preserve fog-of-war and leave unseen horse locations indistinguishable from other unexplored terrain.

While horse knowledge is `Unknown` or `ConfirmedAbsent`, the Fief Spark keeps HUS-07 and projects transitively requiring it out of its candidate and forecast paths; this includes TRN-09 and WAR-10 in the typed catalog. Once a horse source is seen, the normal prerequisite and catalog gates apply. The gate affects research planning only; it does not alter project completion or acceptance semantics. Horse knowledge adds a tail field to Fief belief hashing and snapshots; Iron and Bronze retain their prior hash and snapshot bytes.

Focused tests cover an unexplored live horse remaining unknown, sighting a horse after its tile is explored, absence staying unknown until full exploration, Fief snapshot continuation and Iron hash/snapshot invariance, and blocking/unblocking the horse-dependent research chain. Validation: `dotnet test src/CivSim.Tests/CivSim.Tests.csproj --no-restore --filter 'FullyQualifiedName~FiefHorseSourceKnowledgeTests'` passed 3/3 tests.

This is a narrow planning/fog gate, not evidence for the 80-duel or A1–A8 Fief acceptance gates. Alliance survival after leader death remains unmodeled because the game has no leader lifecycle; no behavior for that case is claimed here.
