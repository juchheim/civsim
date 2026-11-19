# B5-01 paired-pack isolation canary — 2026-11-15

**Status:** B5-01 isolation passes on the current E5 source snapshot against the current `main` baseline. Current-main full Bronze and Iron periodic checksum files match byte-for-byte under the same numeric E5 pack; the current E5 build also passes Toy, worldgen, research, and politics legacy checks, 24 focused isolation tests, and strict content validation. The current-main Bronze source produces the new result on both branches; the old checked-in `main` Bronze golden is stale and is reanchored on this branch with the reason recorded below.

> Earlier canary sections record historical attempts and are superseded where they conflict with the current-source closure record at the end of this report.

## Evidence

The matched packs were built by `build_e5_status_pack_pair.py`. Their only structural difference is the status field for the 50 E5 project rows. The draft pack SHA-256 is `58e4fbe629ae116fb5ed35d2c10fd9a6844d8cd37975bec3dc63f5430db56bb3`; the numeric pack SHA-256 is `994fb7801aca466b0da3d9dabfab3cc90fb024a029100bcb50f76393fbe9812f`.

Using Release harness DLL SHA-256 `85e33706ac5858561c672ecfc136ac2b3768fd0cd5a65ab233407ff291f0ca31`, both packs passed:

```sh
dotnet src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll research \
  --pack <draft-or-numeric-pack> --check-bronze-golden tests/golden/bronze-v1.txt
```

Each canary performed two fresh full-chapter runs and matched `tests/golden/bronze-v1.txt` exactly: seed `20261019`, 44,850 ticks, rival-countdown end at tick 44,850, civ 1, countdown start season 274, checksum `f5879601ca18dca7`. The golden file SHA-256 remained `804e39699cb16efd80226746852e8a25f5e8cebd8151814e03ca9f8bb14a87a8`; it was not refreshed.

Both packs also produced the same 600-tick Iron result at seed `20261019`: checksum `add8e46a5f5c3b87`, digest `2f2e03f5effabaaa`. This is a paired-pack parity check, not a historical Iron-golden comparison.

## Additional unchanged-golden checks

The following paired-pack checks used the current Release harness DLL, SHA-256 `27a9d425047af6c7f1cfb85a42b714145b2449ce1d81182bfeff3816de390ccb`, and `DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec`. The matched draft and numeric pack hashes are respectively `58e4fbe629ae116fb5ed35d2c10fd9a6844d8cd37975bec3dc63f5430db56bb3` and `994fb7801aca466b0da3d9dabfab3cc90fb024a029100bcb50f76393fbe9812f`.

- `research --pack PACK --minutes 12 --bot grower --check-golden tests/golden/research-v1.txt` passed with either pack: seed `20261019`, 7,200 ticks, 27 commands, final `c31d04fb1eb338a2`, digest `b63b802ab03443ee`. `research-v1.txt` SHA-256: `bf36303ff80f08cd0377edab32ed0fa58f2f3c0ca49b8b0c4cbfae152538526a`.
- `research --pack PACK --check-bronze-golden tests/golden/bronze-v1.txt` passed with either pack, with two fresh full-chapter runs identical: seed `20261019`, 44,850 ticks, rival-countdown end at tick 44,850, civ 1, countdown start season 274, checksum `f5879601ca18dca7`. `bronze-v1.txt` SHA-256: `804e39699cb16efd80226746852e8a25f5e8cebd8151814e03ca9f8bb14a87a8`.
- `politics --pack PACK --minutes 24 --check-golden tests/golden/politics-v1.txt` passed with either pack: seed `20261019`, 14,400 ticks, archetypes `Balanced,Balanced,Balanced,Balanced`, final `7f27cfbd63dcad30`, digest `5cac435289106fc8`. `politics-v1.txt` SHA-256: `276594927ab640fdd259d18f475885b2d72c491233a819258f502582bc547c27`.

Pack-independent checks on the current Release harness also passed: Toy reported `seed=20261018 ticks=3000 checksumInterval=50 commands=28`, `final=d04203e0dca88cdc`, `digest=36f28821bf7e78a5`, `determinism check passed`; world generation reported `seed=42 fingerprint=ea2cbb3fd75bd7c2`, `world generation fingerprint check passed`, with `seeds=1 accepted=1 failures=0`. These runs did not use either matched pack.

The current-versus-clean-baseline full Iron comparison completed with identical `iron-match` output. Both runs used seed `20261019`, requested 110 minutes, and the same numeric E5 pack (SHA-256 `994fb7801aca466b0da3d9dabfab3cc90fb024a029100bcb50f76393fbe9812f`); both simulated 102.5 minutes and ended by Iron `RivalCountdown` at season 409. The clean baseline was HEAD `9be5d659ad30f501150b53202b6f86194ddd7ea9` with Release harness DLL SHA-256 `00eb9a9b101d36d8e1fe00b3b5a123f787b7f32e581b92e3228e868c3813a79a`. The current workspace aggregate was `87996d7a3751c48ecdf40be72c83f903dae45c1370ec383f33cb46d160476857` with Release harness DLL SHA-256 `27a9d425047af6c7f1cfb85a42b714145b2449ce1d81182bfeff3816de390ccb`. `iron-match` emitted no checksum or digest fields in either run; its complete stdout summaries matched. This is a diagnostic current-versus-baseline comparison, not historical Iron-golden acceptance. The earlier 600-tick Iron pair remains pack parity only.

## Signed-off E4 baseline checksum comparison

The stronger baseline is the user-signed-off E4 promotion commit `b5a862515fdba3355c4fddb7363f22011a06fb00` (tree `bf2c76804ded736e1908bb4b1fc41a38da92e30d`). Its own CLI built a baseline pack with SHA-256 `920c92f3ae2da5e6d0f0a0d9e5f4e1d2880a96dd384c3896287742ec6b6eef44`. The current numeric E5 pack lacks the one constant required by that baseline, `ChapterCapYear`; the exact baseline object is `{ "id": "ChapterCapYear", "source": "22 §3.1", "unit": "year", "value": 64 }`. For this supplemental probe only, that object was added to a temporary JSON copy of the current numeric pack. The compatibility copy's SHA-256 is `288a9a67f19c535b9678418fcd5400dbf6ae18ac838982491fd2672349908efd`; all 50 E5 project rows remain numeric, all 12 current-only constants and all other parsed pack fields are preserved unchanged. The original current numeric pack remains unmodified.

Identical temporary checksum probes referenced the baseline Release `CivSim.Sim.dll` SHA-256 `3ec4541e74ca1480abd81d814ec97e931ad3bc6a9c81b44c93b3bbca3fc3cfe6` and current attached Release `CivSim.Sim.dll` SHA-256 `fc7959501299122543905e790b2294d5306f7b3d4d3f723f0e40b9bf13010063`, respectively. Both ran seed `20261019`, Iron rules, 66,000 ticks (110 minutes), against the same compatibility pack and recorded all 1,320 50-tick checksums. Baseline ended at checksum `acd8026d2cf5dc71`, digest `00e4321fe8f2ece2`; current ended at checksum `c53a21e1cb0ac222`, digest `fb0053284c63c44e`. The first periodic checksum differs at tick 50: baseline `5e574b76fdd723d2`, current `5b7b7f83daa0bce1`. At that tick, economy component checksums differ (`76cd19c7c13e5742` baseline; `df734671221e8c8a` current), while research (`a0a5c5bf6d89082d`), politics (`f07c0f61f2f7490f`), and AI (`6c8bf21c895130c1`) component checksums match.

A component check before advancing any ticks shows the economy checksum already differs at initialization (`144f96cac849e89b` baseline; `5dc3195a041865e4` current). Research (`0c8d3b5da7e13f51`), politics (`88380a4757bea57e`), and AI (`6c8bf21c895130c1`) match at tick 0. The combined initial match hashes differ (`9d5a65af117aa236` baseline; `7b267c49953676e0` current). Source comparison isolates the difference to the economy's `_contentFingerprint`, initialized by `FingerprintContent(content)` and included by the economy `ComputeChecksum()` stream. At `b5a8625`, Era-5 projects are fingerprinted as typed effects; current code intentionally treats Era-5 rows like Era-4 legacy placeholders in both the effect count and effect loop. On the compatibility pack, all 50 Era-5 rows are numeric, so the two versions hash different content fingerprints before any ticks run. Fief Know-how and the other E5 additions are inert in Iron, and the current guidance hash contributes nothing for the all-Balanced setup. This is an initialization/checksum metadata mismatch, not evidence that those E5 runtime effects execute in Iron.

**Superseding source note:** the diagnosis above reflects the earlier historical probe. The current candidate fixes the E5 fingerprint leak in Iron: typed E5 effects are fingerprinted only in Fief, matching current `main` behavior. A current-source 66,000-tick Iron comparison is recorded below and matches current `main` at every periodic checkpoint.

As a historical bridge check, current code on the unmodified numeric pack produced checksum `e1444d4881dafdb1` and digest `9a4dcf58c7328b81`; adding only `ChapterCapYear` changes the current checksum stream, first diverging from the unmodified-pack run at tick 26,500. The compatibility pack was not a neutral bridge, so that run is excluded from acceptance. Direct unmodified-pack execution was schema-incompatible: baseline lacked `LegacyThrivingDivisor`; current lacked `ChapterCapYear`. The current-main checksum comparison below supersedes this bridge attempt.

The Bronze drift was caused by `OreLitres` initializing Fief-only saltpeter and sulfur in the shared ore array as soon as Bronze mining activated at KEY-3. Their fitted quantities are now included only when `FiefChain` is active. The focused Release test filter covering the deposit quantities, E5 mode isolation, and chapter rules passed 14/14.

## Historical candidate isolation tests (superseded)

On the earlier frozen F-T7 held-out source snapshot (`content/src/game` aggregate `e42e3605be2de8bd73a4cc049b0219cf92ed0a43994cf11064486958b2cb7144`), a no-build Release filter for `E5ModeIsolationTests`, `BronzeFingerprintCompatibilityTests`, and `MetalEconomyTests` passed 24/24. It used the numeric-status pack `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`. This earlier test is retained as history; current-source test evidence is recorded below.

## B5-01 current-source closure — 2026-11-15

The current candidate source aggregate is `7decf87ac45f924efeedb8fb28cd87c8f8d0476b93b438344c931c07fa4947d6` (HEAD `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`, including the live working-tree source). The current-main baseline is HEAD `ec31d5cedd2450bb1ccac480cbf0fc29c016f0d3`. Both builds were verified from their current checkouts: baseline `CivSim.Sim.dll` SHA-256 `d2aa7444dbf03161a9445243681bd888f7e4da7aae657640a8720e4fe2849723`; candidate DLL SHA-256 `a8cb7666042cf6651a6b9558b3d6db7b1f00566b5cc30041077e67b86540ad50`. The shared numeric E5 pack SHA-256 is `f360a3d93047ac248d97574af3bff24e9ffb28c13217a578c473e7dd02a0a05a`.

The current-main and E5 checksum CSVs are byte-identical at both complete horizons:

- Bronze, seed `20261019`, 45,000 ticks, 900 periodic checkpoints, final checksum `411819856468d38b`, digest `51082e1fd75fd332`; both CSVs SHA-256 `3d850930c90d0176b00ca177c891a3525b65b11d1f070ea14024f6610b99130d`.
- Iron, seed `20261019`, 66,000 ticks, 1,320 periodic checkpoints, final checksum `d047c1d567af60fb`, digest `10bd5757c4129683`; both CSVs SHA-256 `a9ce11f30c4e0d249175d5859332853504a022d00e6743cef81e776e8fe1a974`.

The four CSVs and complete run metadata are under [current-main isolation evidence](m12-wp11-b5-01-current-main-isolation-2026-11-15/). Each file includes tick, match, economy, research, politics, and AI checksum components. Direct `cmp` comparisons return equal for both pairs. The metadata row counts include the CSV header and tick-zero row: 902 Bronze rows and 1,322 Iron rows.

Fresh current-candidate and current-main checks both pass Toy (`d04203e0dca88cdc` / `36f28821bf7e78a5`), worldgen seed 42 (`ea2cbb3fd75bd7c2`), research-v1 with the shared numeric pack (`c31d04fb1eb338a2` / `b63b802ab03443ee`), and politics-v1 at 24 minutes (`7f27cfbd63dcad30` / `5cac435289106fc8`). E5 additionally passed the reanchored full Bronze check twice; the run ends at tick 45,000, countdown season 275, checksum `290d986285dad52b`. The same current-main source and numeric pack produce that result, while the old `main`-branch `bronze-v1.txt` still encodes the pre-`51ee9e3` seed-20261019 world. This branch's golden update is limited to that current-main reanchor and is explained in `run_metadata.json`.

The current-source Release filter `E5ModeIsolationTests|BronzeFingerprintCompatibilityTests|MetalEconomyTests` passed 24/24, and `validate --strict` reported 509 projects + 10 keystones with 0 errors and 0 warnings. These checks establish legacy-mode inertness and checksum parity for the tested requirements. B5-01 is closed. This does not close whole-match balance, promote E5 rows, or enable ordinary/release Fief selection; those remain gated by WP-E5-11/12.
