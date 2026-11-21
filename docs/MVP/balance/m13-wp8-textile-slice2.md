# M13 WP-E6-8 slice 2: FIB-12 shuttle and jenny, AGR-12 four-course (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted (on top of the committed WP-E6-7 and TRD-10/11 slices). Everything tests `DiscoveryChain`, so Bronze, Iron and Fief bytes, hashes and snapshots are unchanged and nothing new is stored (the rotation is derived from field order, year and research, as AGR-11's was). FIB-12 is unblocked by decision D12 (TRD-10 now exists). All numbers are provisional.

## What the slice does

| Part | How |
|---|---|
| **FIB-12 jenny** | `spin_yarn` takes **a third of the labor** (x3), by hand, on top of the wheel's x3 (FIB-11). |
| **FIB-12 shuttle** | `weave_cloth` labor is divided by `1 + 2c`, where `c` is the craft coverage of the shared power ledger: **x1 with no power (no gain over the treadle loom), x3 with the ledger fully lending power hours**, on top of the treadle loom (FIB-10). A shuttle "draws its share of the shared power ledger" exactly as the row says. Other recipes are untouched. |
| **AGR-12 rotation** | Four courses (wheat, turnips, barley, clover): **half the strips bear grain each year** (a strip is cropped every other year, exactly one of two consecutive years), and each grain strip yields **9/5 of a strip**, an average of **0.9 grain per strip-year against the three-field's 0.75: the row's field_output x1.2**. The yield and crop share feed the same functions the harvest, the labor plan and the site forecast already use. |
| **Enclosure** | The hands the fields need fall to **0.85** of the cropped labor (the row's farm_labor_per_output 0.85). The planner moves the adults it frees to other work; they are not erased. |

## Design choices worth knowing

1. **FIB-12 stacks on the E5 tools,** not in place of them: wheel x3 and loom x2 are the hand baseline, the jenny and the powered shuttle are the E6 step. The shuttle's x3 is the fully powered ceiling; a town with no mill gets nothing from it, which is the row's "drawing its share of the ledger" read literally.
2. **The jenny does not need power.** The row says "hand or water powered, never both for the same hours"; the hand reading is the floor and a water-powered jenny would share the same ledger hours the shuttle does, so no second term is added.
3. **The four-course gain is derived, not typed.** The oracle's finding (x0.9 to x1.3 over three-field; x1.2 chosen) is reproduced as 1/2 cropped x 9/5 = 0.9 against 2/3 x 9/8 = 0.75. The fodder and legume strips carry the manure and fed livestock that HUS-11 will use; without HUS-11 they simply rest the soil.
4. **Cottage cloth as a trade good** is not built: cloth already moves as a trade good through the existing trade code; a separate cottage-industry good would need its own price and buyer.

## Tests (6 in `DiscoveryTextileTests` and `DiscoveryFourCourseTests`, all pass)

The jenny spins three times as much for the labor and leaves other recipes alone; the shuttle gains nothing without power and weaves faster, never more than three times, with a mill; FIB-12 does nothing in Fief; four-course crops half the strips at 9/5 for x1.2 over the three-field; enclosure cuts the field labor to 0.85 of the cropped need; every strip bears grain in exactly one of two consecutive years; the rotation is inert in Fief.

## Not done in WP-E6-8

1. **HUS-11 stall-feeding** (winter herd loss to a fifth, paid from a stored fodder stock; manure +5% on fields): needs a fodder good and the winter herd-loss path.
2. **FOR-09 botanical garden** (staffed, medical +15%, trials crops) and **MED-09 anatomy** (hospital surgery x1.3, training x2): both extend E5 staffing and the hospital.
3. Urban migration beyond the staffing draw; a soot penalty on fields.
