"""Live Faith & Fief (E5) anchors used by the E6 oracles. One place to edit when a newer natural-play run is recorded.

Each anchor names its source run so a reader can tell how stale it is. E5 is still being balanced (numbers only), so every value
here is a moving input: re-run the E6 oracles after each E5 rebase.
"""

# F-T1 held-out cohort `final3-ft1-holdout-7300` (docs/MVP/balance/m12-e5-ft1-holdout-7300-analysis-2026-11-16.md): 80 worlds, 320 seats,
# natural play, four archetypes, E5 era base 7,300 (the registry at the time of writing says 6,400; the holdout is the latest measured pace).
LIVE_E5 = {
    "label": "F-T1 holdout 7300 (2026-11-16), 80 worlds, 283 of 320 seats completed KEY-5 to KEY-6",
    "e5_era_base": 7300,
    "key5_to_key6_median_minutes": 24.30,     # completed-seat median; p10 21.02, p25 22.75, p75 26.20, p90 28.38, range 17.40-31.50
    "key5_to_key6_p10_p90": (21.02, 28.38),
    "completion_share": 283 / 320,            # 24 seats right-censored after KEY-5, 13 never reached KEY-5
    "e5_target_minutes": 25.0,                # docs/MVP/27 D8
    "growth_per_minute": 0.06,                # carried from the Iron anchor (e5_anchors.LIVE_E4): Insight ~ population^0.85 grows ~6% a minute
}

# E6 plan (docs/MVP/38 §6): same 25-minute target as Iron and Faith & Fief.
E6_TARGET_MINUTES = 25.0
E6_ERA_BASE = 32000
