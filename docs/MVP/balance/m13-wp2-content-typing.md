# M13 WP-E6-2: typing the Discovery rows (2026-11-20)

**Status:** content only, on branch `codex/e6-discovery` (worktree `civsim-e6`, from `main` at `deba515`). All 41 regular E6 projects and KEY-7 now carry typed effects, practice, flavor and a Moment. **Every row is still `status: draft`**, so no match mode can reach any of it. Numbers marked P in effect notes are provisional and come from [38](../38-discovery-age.md) §5; none is fitted. Design: [38 §9](../38-discovery-age.md).

## What changed

| Area | Change |
|---|---|
| Catalog | 32 files: the 42 E6 rows go from one `prose` effect to 1 to 5 typed effects each (`good`, `build`, `role`, `mod`, `unlock_action`, `cohesion`, `admin`, `defense`, `combat`, `diplo`, `lever`, `risk`, `wonder`, `era_advance`, `legacy`, `rule`). Ids, names, types, requirements and gates are untouched (MIN-09 keeps `[Seen: Coal]`, FOD-10 keeps `has Sugarcane`). |
| Late-project doubling (D9) | FIB-12, FIR-11, FIR-12, INQ-09, MET-13 cost 64,000 (2 x 32,000 x M 1.0); MEC-10, TRD-11 cost 96,000 (2 x 32,000 x I 1.5). Each carries a `cost_reason` citing 38 §6 and 28 §16. The era base stays 32,000. |
| Goods (12) | G-COAL, G-COKE, G-CANE, G-SUGAR, G-COD, G-PLATEGLASS, G-PARTS, G-ACID, G-ALKALI, G-PAMPHLET, G-CHRONOMETER, G-TELESCOPE (all `introduced_era: 6`). |
| Structures (10) | coal_mine, coke_oven, atmospheric_engine, navigation_canal, factory, bastion_fort, botanical_garden, learned_society, exchange_house, renaissance_palace (Wonder). |
| Roles (3) | engineer, navigator, botanist. |
| Actions and practice (4) | ocean_voyage, colonize, tend_engine, factory_work, each with a Know-how mapping summing to 100. |
| Recipes (11) | make_coke, coke_smelt (no charcoal), refine_sugar, salt_cod, make_plate_glass, make_parts, make_acid, make_alkali, print_pamphlet, make_chronometer, make_telescope. |
| Validator | New check **V23** (E6 integrity), the twin of V22: E6 rows are promoted together, typed only, with a computed cost, flavor and Moment. |
| Tests | Counts and classification updated for the new content: late-project doubling test knows E6 (7 rows), recipes 57 to 68, structures 71 to 81 with each new structure given a core size class (bastion fort, learned society and the Palace with the castles and universities; engine, factory, garden and exchange house with the workshops; coke oven small; coal mine and canal with the area structures). The Discovery economy test now asserts only that the payload grows. |
| Golden | `tests/golden/content-pack.txt` refreshed (projects 519; bytes 422,388; hash `933956e7...`). |

## Isolation

The content fingerprint already treats E6 rows as one prose placeholder outside Discovery (WP-E6-4), so typing them moved no Bronze, Iron or Fief checksum. Checked: the toy golden passes, and the research (12 min, grower) and politics (24 min) runs print exactly the `final` and `digest` values they printed before the typing (`208923cede594c56` and `94fcdd6cfaff687a`). The checked-in `research-v1` and `politics-v1` files still mismatch exactly as they do on a clean `main` (see [m13-wp4](m13-wp4-mode-plumbing.md)).

## Decisions baked into the rows (all P, all for the oracle to refit)

- **Coal and coke:** coal is a deposit good dug at a staffed coal mine; firing halves heat wood only where coal arrives; a coke oven makes 1 coke from 1.5 coal; coke smelting burns no charcoal (6 ore + 3 coke to 3 iron). Coke smelting's "x3" is read as the same iron for far less wood, not minted iron.
- **Steam:** the atmospheric engine is a staffed source in the shared E5 power ledger and the pump that makes deep coal mining work; idle without coal or an engineer.
- **Ocean:** SEA-12 sets open-sea reach to 24 tiles from land (was 11), enough to cross the 33-tile channel in one leg; risk rolls are reduced by WAY-10 (-70%) and TRD-11 (-50% spread across settlements). No omniscient discovery.
- **Colonies:** distance Overreach strain plus a seeded independence risk, never score-based.
- **Credit:** TRD-10 and TRD-11 are written in kind (pooled goods, spread losses) and mint no money; the implementation waits on decision D12.
- **Bastion fort:** multiplier 2.0 and half the gunpowder wall penalty, to be fitted against the F-T5 force tables.
- **Four-course rotation:** x1.5 derived from cropped share 2/3 to 1, measured against the AGR-11 baseline, not stacked on it.
- **Factory:** craft output x2 scaled by the power the factory actually receives; first local pollution.
- **Science costs:** INQ-08 and NUM-11 cost cuts go through the existing diffusion-discount floor.

## Not done

- No E6 row is numeric and nothing in the simulation reads E6 effects yet; the effects are the spec for WP-E6-5 onward.
- No row has an `ack`; costs are `EraBase 32000 x type multiplier` except the seven doubled rows (KEY-7 80,000).
- The `mod` stat names (`field_output`, `open_sea_reach`, `furnace_iron_output` and the rest) are labels for the systems to read, not yet bound to code.
