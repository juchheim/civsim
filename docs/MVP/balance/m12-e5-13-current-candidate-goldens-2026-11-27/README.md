# Current candidate validation and golden checks — 2026-11-27

Candidate `a966ec377a9d650b2b90b4467827c777e115c603` (tree `aa860512e8f97f6e1c35ebe5548e32f5159e73ef`), pack SHA-256 `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`.

All checks passed on the exact current candidate: strict content validation, content-pack golden, toy golden, research golden, and politics golden. The full Release suite is tracked separately; one timing assertion failed while concurrent simulation cohorts were consuming CPU and requires a quiet rerun.

- Harness SHA-256: `1cd422d06fe28e9383eebb0d00dd4de551d3a7b81eee11429f50586cc4e8baf7`
- Sim SHA-256: `90fe2bed466ec7dc6903e27e4d6a1324d26af5fc5c1d082641e2fd7dd687c86d`
- Strict validation: 509 projects + 10 keystones; 0 errors, 0 warnings.
- All four golden checks passed.

Raw command output and SHA-256 pins for each log are included in this folder. The generated numeric pack is not committed; its exact SHA-256 is recorded above and in `pins.json`.
