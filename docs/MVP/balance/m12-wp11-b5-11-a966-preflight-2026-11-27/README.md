# B5-11 current candidate preflight — 2026-11-27

Candidate `a966ec377a9d650b2b90b4467827c777e115c603` (tree `aa860512e8f97f6e1c35ebe5548e32f5159e73ef`) is clean and pinned to source aggregate `e0b43203a6622829816f85b924e986995bffd474a82a78ce819272025a117048`. The numeric pack, adjacent Release Harness and Sim, guarded cohort runner, provenance helper, and both manifest hashes were rechecked. The two external output roots are absent.

**Status: ready, waiting for an idle host.** M10 and WP-E5-12 simulation cohorts are using this host. Run natural and E5-granted cohorts sequentially only after they stop. Each cohort contains 20 unscreened seeds at 130 simulated minutes with a 5-minute warmup. Keep the existing thresholds: mean tick time ≤4 ms and GC pause ≤16 ms per measured tick. The 600-figure p95 remains diagnostic and requires investigation if exceeded.

Full pins, paths, manifests, and output roots are in `pins.json`. Do not build or modify the candidate between the two runs. This preflight does not claim B5-11 acceptance.
