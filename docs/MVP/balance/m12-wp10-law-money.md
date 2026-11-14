# M12 WP-E5-7 (first slice): law and money

**Status:** One existing typed rule is verified through the live economy and its focused test passes in Release: GOV-11 adds one point of administrative span when complete in Fief. This is a narrow mechanics check, not completion or balance signoff for law and money. GOV-11 and all other E5 rows remain draft.

## Implemented slice

- GOV-11 already contains an `admin span: 1` effect. The generic admin-span path sums completed effects, so the Fief economy receives the additional span without new saved state or a second rule implementation.
- `FiefLawMoneyTests` compares the same seeded Fief fixture with and without GOV-11 and checks the +1. An Iron fixture with the numeric test pack but without the E5 completion retains its baseline span. Existing Fief chapter tests separately cover the mode ceiling that hides E5 projects outside Fief.

## Deferred

- GOV-11's `city_growth ×1.1` applies to chartered towns, but the economy has no chartered-town state or selection rule. Applying it to all towns would change the proposal.
- GOV-10's dispute/crime event multiplier has no crime/dispute event model.
- TRD-08 credit and bills have no credit ledger; trade currently exchanges existing goods. Do not implement credit as minted inventory.
- The other charter, contract, apprenticeship and guild effects need their own bounded rules and measurements. E4 signoff remains a dependency for promotion or balance claims.

## Next checks

1. Measure whether one extra span changes cohesion pressure in a controlled multi-settlement Fief fixture; retain the result as a diagnostic.
2. Specify a chartered-town state and a non-minting credit ledger before implementing the two larger proposal effects.
