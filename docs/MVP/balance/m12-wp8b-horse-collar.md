# M12 WP-E5-5 (third slice): horse collar (2026-11-13)

**Status:** NRG-04's provisional draft-work model is implemented and fixture-tested on `codex/e5-fief`. It is active only in Fief, after the settlement's civilization completes KEY-5, researches NRG-04, and actually holds at least one tame horse. Bronze and Iron behavior stays unchanged.

## Model

- **Ploughing:** Each held horse can plough twice the field-strip capacity of an NRG-02 ox pair (6 strips); without NRG-02 it uses the base pair capacity. Labor is halved only for horse-ploughed active strips. Existing ox teams are assigned first, so the same strip does not receive both discounts. The rule composes with Fief's baseline and AGR-11 crop shares.
- **Hauling:** The labor charged for a modeled distance-based haul is halved when the settlement has a horse collar and a tame horse. The saving applies to the haul component only; cutting, digging, and base material labor are unchanged. It covers outer-range civic timber, civic stone procurement, Iron ore delivery between settlements, charcoal gathering, and mining-camp ore delivery. Transfers without an existing haul charge remain unchanged.
- No new persistent or hashed state is introduced. The existing per-settlement tame-horse count is the only animal state used.

The catalog's draft-power ×2 supports the relative capacity and half-labor rule. The binary haul rule (one or more held horses saves half of each modeled haul charge, without a per-horse cargo cap) is a bounded provisional interpretation; the land/economy oracle should fit it before E5 is promoted.

## Evidence

`FiefHorseCollarTests` covers the field labor reduction, no benefit without a held horse, no effect in Bronze and Iron fixtures, and a physical outer-range timber gathering path where the horse reduces haul labor but leaves woodcutting labor intact.

Whole-match A/B: seed `33`, promoted temporary E5 pack, Fief mode, `--grant-e5`, 130-minute horizon. KEY-6 times were unchanged at 92.4, 92.4, 93.1 and 93.6 minutes. End populations changed from 2,233 / 3,493 / 2,813 / 2,148 before the collar to 2,189 / 3,347 / 2,781 / 2,157 after it. This is a one-seed smoke diagnostic, not a balance sample: the harness does not report each settlement's tame-horse count, so it cannot say which rule engaged or separate ploughing effects from haul savings. See the committed land-rotation report [m12-wp8a-land-rotation](m12-wp8a-land-rotation.md) for its separate seed-20261019 diagnostic.

## Still open in WP-E5-5

Selective breeding, game reserves, TER-10 claim upkeep, timber framing, cast-iron goods, the cannon/powder chain, the land/food oracle, and multi-seed whole-match balance remain open.
