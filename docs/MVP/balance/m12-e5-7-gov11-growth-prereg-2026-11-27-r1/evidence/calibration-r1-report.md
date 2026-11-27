# GOV-11 calibration r1 — 2026-11-27

## Run integrity

- Candidate: `b075e1747c86153b03096db9b46f391040183726` (tree `f5aa856810c1f0ae67dac942f4407638274298c6`); source stayed stable for all 20 runs.
- Source aggregate: `299b39031074567c110817f38fdb3180fa2d54c3f5662acdcf1240ecbf7207a6`.
- Numeric E5 pack SHA-256: `ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90`.
- Release Harness / Sim SHA-256: `c1217adce70fa205a84fcec8e8ec920bcab2bcafd78b7e4af2628a193d3a5a18` / `230f427c2674d6823d3bddcc513759bab602b13e5eee2adcb3858278e678d42a`.
- Calibration seed manifest SHA-256: `9c5ec7210aba37b9f2be2b9e69c352c0c7d7ae311e0574c49655be72581b696a`.
- Runner / provenance helper SHA-256: `ea7af356ff34536d908fb108ecc78c4209228988f989662f7566b5ee31330c00` / `496f1064b4b02f3e28c8aaff33140a8b2e13d7f8e1a785a68a8cd32f0138a10a`.
- Outcome: 20/20 matches completed and replay-verified; no failures or censored seeds.

## Literal decimal analysis

The preregistered analyzer reports **FAIL** against the literal requirement that the bootstrap interval contain decimal `1.10`:

- Geometric-mean treatment/control conception-probability ratio: `1.099999997633`.
- Seed-clustered 95% interval: `1.099999997520`–`1.099999997751`.
- GOV-11 exposure: 20/20 seeds, 2,085,272 paired opportunity rows; 903,853 had nonzero control chance and 1,181,419 had zero chance under both conditions.
- Expected conceptions: 49,385.350 control; 54,323.885 treatment. Paired realized outcome delta: 5,034.

The interval's upper end is about `2.25e-9` below decimal `1.10`. This is at fixed-point arithmetic scale, not a meaningful balance shortfall. `Fix64.FromRatio(11, 10)` is represented in Q32 as raw `4,724,464,025`, and the probability multiplication truncates the resulting raw chance. A row-level audit of all 903,853 nonzero control opportunities found the treatment chance exactly equals that Q32 multiplication on every row. The raw measured fail is retained in [`calibration-literal-1.10-analysis.json`](calibration-literal-1.10-analysis.json).

The reproducible row audit is [`audit_fief_gov11_q32_effect.py`](../../tools/audit_fief_gov11_q32_effect.py), SHA-256 `45853850ce386105a6758a912ce1c8386cb1ddc87ad8dab2c73d3766068e95ed`. Its full result is [`calibration-q32-audit.json`](calibration-q32-audit.json), SHA-256 `307c56d3904faa0f06485bfafeabbd9bd36d5b2b67bd2f7f847216522e59e475`.

## Disposition

This calibration matches the implementation's representable 11/10 rule across all observed opportunities. It does not justify changing gameplay values. The literal-decimal analyzer fail remains visible; the calibration is fit for a holdout only under the fixed-point interpretation of the same 11/10 rule. Population endpoint remains a secondary diagnostic. No holdout has been run yet.
