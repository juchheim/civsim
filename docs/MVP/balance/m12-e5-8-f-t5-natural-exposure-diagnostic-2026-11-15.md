# F-T5 natural exposure diagnostic (2026-11-15)

**Result: inconclusive for F-T5 siege gates.** The frozen 20-seed natural cohort completed successfully, but none of the matches observed a siege investment. This scratch numeric-pack run does not close F-T5 or promote any numeric row.

## Frozen inputs and provenance

- Pack: `/private/tmp/civsim-e5-status-pair-20261115/numeric-e5.pack`, SHA-256 `994fb7801aca466b0da3d9dabfab3cc90fb024a029100bcb50f76393fbe9812f`.
- Seed manifest: [m12-e5-8-f-t5-natural-seeds-2026-11-15.txt](m12-e5-8-f-t5-natural-seeds-2026-11-15.txt), SHA-256 `f787766662b46319f7f0807eacd27592aa8c2af4163f366d52ddec07a3e3a786` (20 unique seeds).
- Source HEAD: `bd0f4b8c6d9c13a0c960be8d55a6d3af8cb3874c`; E5 source/content/game workspace aggregate SHA-256 `87996d7a3751c48ecdf40be72c83f903dae45c1370ec383f33cb46d160476857`.
- Release Harness DLL SHA-256: `27a9d425047af6c7f1cfb85a42b714145b2449ce1d81182bfeff3816de390ccb`.
- Adjacent Release Sim DLL SHA-256 at run start: `fc7959501299122543905e790b2294d5306f7b3d4d3f723f0e40b9bf13010063`.
- Run metadata: [run_metadata.json](m12-e5-8-f-t5-natural-output-2026-11-15/run_metadata.json), SHA-256 `092bc554d641929af72922e8abc035abadd023f0270e8927124cab410ce10661`. All 20 raw-file hashes, exact commands and exit codes are recorded there; every run exited 0.
- Raw output directory: [m12-e5-8-f-t5-natural-output-2026-11-15](m12-e5-8-f-t5-natural-output-2026-11-15), 20 files / 116,043 bytes. Deterministic raw bundle SHA-256 (sorted filename plus each raw-file SHA-256): `957aad25f6c63f29b6b2660f3c66b3a65eda018f28dc86c2e44d44a1379d23b1`.
- Analyzer report: [analysis.md](m12-e5-8-f-t5-natural-output-2026-11-15/analysis.md), SHA-256 `b54f1f4a99ffb7115f28e90b8d67e45642df2c17bbba76815ac48aabc14ea92a`.

## Observations

- All 20 matches reached the 130-minute horizon with exit code 0.
- Seasonal siege investment rows: 0. Observed cannon shots: 0. Powder paid: 0.00. Powder-breach rows: 0.
- Terminal investment exposure: 0 observed, 20 no-exposure. There were no siege spells to classify as right-censored.
- The raw traces contain 39 unique terminal siege-event keys (78 two-sided event rows: 42 `assaultresolved`, 36 `captured`), but those records have unavailable engine, crew, shot, powder, exposure, integrity and breach fields. They do not establish siege-gate behavior.
- Assigned crew-seasons, observed attrition deaths, and DEF-09 active observations: 0. Both base and DEF-09 attrition strata are inconclusive due to no exposure.

The analyzer's “raw files with trace rows: 20/20” count includes terminal siege-event records; it does not mean any seasonal investment or combat-detail observation was present. Treat this run as a natural exposure diagnostic only. Controlled breach/relief fixtures and the focused regression suite remain separate evidence; broader natural siege exposure and the F-T5 whole-match gates remain open.
