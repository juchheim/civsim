# M12 WP-E5-7 (first slice): law and money

**Status:** One existing typed rule is verified through the live economy and its focused test passes in Release: GOV-11 adds one point of administrative span when complete in Fief. A follow-up feasibility audit traced TRD-08 through caravan dispatch and settlement. It did not add a credit rule: the current ledger has no bounded repayment rule that can be applied without inventing economic behavior. This is not completion or balance signoff for law and money. GOV-11 and all other E5 rows remain draft; every numeric behavior below remains provisional.

## Implemented slice

- GOV-11 already contains an `admin span: 1` effect. The generic admin-span path sums completed effects, so the Fief economy receives the additional span without new saved state or a second rule implementation.
- `FiefLawMoneyTests` compares the same seeded Fief fixture with and without GOV-11 and checks the +1. An Iron fixture with the numeric test pack but without the E5 completion retains its baseline span. Existing Fief chapter tests separately cover the mode ceiling that hides E5 projects outside Fief.

## Deferred

- GOV-11's `city_growth ×1.1` applies to chartered towns, but the economy has no chartered-town state or selection rule. Applying it to all towns would change the proposal.
- GOV-10's dispute/crime event multiplier has no crime/dispute event model.
- **TRD-08 credit feasibility audit (2026-11-14): no runtime change.** Trade does already escrow an existing seller good in the persisted caravan `Expedition.Cargo`; on arrival, `ArriveCaravan` takes existing payment goods from the buyer, credits them to the seller, and delivers only the matching fraction of cargo. This conserves goods for current barter. A credit bill could record that unpaid amount on the persisted `Compact`, but no existing rule defines which future resource services the claim, when collection occurs, what food reserve is protected, or what happens when a route ends before repayment. Scarcity prices also vary by settlement stock, so revaluing an outstanding bill later would not preserve the original claim. Adding a balance field alone would defer accounting without settling a resource claim. We therefore do not approximate money creation or an arbitrary repayment schedule.
- **Concrete seam required before implementation:** define a Fief + completed-TRD-08 eligibility check; fix the bill's unit and origination value; choose an existing repayment good and a rule that protects its forecast need; define partial payment, route termination, and debtor/seller settlement-loss behavior; then append the liability to the Compact snapshot/hash schema. Required regression coverage must show the shipped good moves from seller escrow to buyer, each repayment moves existing stock from debtor to creditor, unpaid balances reconcile, Fief-with-TRD-08 is the only active case, and Iron/Bronze checksums and behavior stay unchanged. Snapshot/hash continuation must cover an outstanding bill and partial repayment. Until those rules are specified, existing immediate barter remains the only supported path.
- E4 sign-off was confirmed by the user on 2026-11-14. The other charter, contract, apprenticeship and guild effects still need bounded rules and measurements before WP-E5-11 promotion or balance acceptance.

## Next checks

1. Measure whether one extra span changes cohesion pressure in a controlled multi-settlement Fief fixture; retain the result as a diagnostic.
2. Specify a chartered-town state before implementing the remaining GOV-11 effect. For TRD-08, resolve the repayment seam above before any credit ledger is implemented.
