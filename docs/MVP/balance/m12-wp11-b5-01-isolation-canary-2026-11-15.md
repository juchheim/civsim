# B5-01 paired-pack isolation canary — 2026-11-15

**Status:** the Bronze legacy-golden canary now passes with both matched packs, and a short Iron parity canary matches. These checks close only this compatibility slice; the full B5-01 isolation gate remains open.

## Evidence

The matched packs were built by `build_e5_status_pack_pair.py`. Their only structural difference is the status field for the 50 E5 project rows. The draft pack SHA-256 is `58e4fbe629ae116fb5ed35d2c10fd9a6844d8cd37975bec3dc63f5430db56bb3`; the numeric pack SHA-256 is `994fb7801aca466b0da3d9dabfab3cc90fb024a029100bcb50f76393fbe9812f`.

Using Release harness DLL SHA-256 `85e33706ac5858561c672ecfc136ac2b3768fd0cd5a65ab233407ff291f0ca31`, both packs passed:

```sh
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll research \
  --pack <draft-or-numeric-pack> --check-bronze-golden tests/golden/bronze-v1.txt
```

Each canary performed two fresh full-chapter runs and matched `tests/golden/bronze-v1.txt` exactly: seed `20261019`, 44,850 ticks, rival-countdown end at tick 44,850, civ 1, countdown start season 274, checksum `f5879601ca18dca7`. The golden file SHA-256 remained `804e39699cb16efd80226746852e8a25f5e8cebd8151814e03ca9f8bb14a87a8`; it was not refreshed.

Both packs also produced the same 600-tick Iron result at seed `20261019`: checksum `add8e46a5f5c3b87`, digest `2f2e03f5effabaaa`. This is a paired-pack parity check, not a historical Iron-golden comparison.

The Bronze drift was caused by `OreLitres` initializing Fief-only saltpeter and sulfur in the shared ore array as soon as Bronze mining activated at KEY-3. Their fitted quantities are now included only when `FiefChain` is active. The focused Release test filter covering the deposit quantities, E5 mode isolation, and chapter rules passed 14/14.

## Still required for B5-01

The canary does not establish every Toy, world-generation, research, politics, Bronze, and Iron baseline check required by the B5-01 row in `27-faith-and-fief-age.md`. Preserve those remaining comparisons; do not update other golden files based on this result.
