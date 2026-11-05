"""Live Iron (E4) anchors used by the E5 oracles. One place to edit when a newer natural-play run is recorded.

Each anchor names its source run so a reader can tell how stale it is. `e5_anchor_check.py` prints the active block and checks the
copied constants against the source; `e5_research_oracle.py` reads `LIVE_E4` for its pacing sections.
"""

# Natural play, four Warlord seats (Builder, Grower, Warlord, Sage), default Iron limits, `iron-match --seed 20261019 --seeds 8`,
# sim at main 67c50ca (renderer-only changes after 43a1648). 32 civ-seats; 31 reached KEY-4; 16 reached KEY-5 before the chapter
# ended (15 were still researching, median 17.2 min after their KEY-4), so interval statistics are right-censored.
# Source: balance/m12-wp4-reanchor.md.
LIVE_E4 = {
    "label": "m12 re-anchor (2026-11-05), natural play, seeds 20261019-26, 16 of 32 seats finished E4",
    "key4_median_minute": 59.8,
    "key4_to_key5_minutes": 20.0,          # median of finishers 19.6 (range 17.5-24.1); censored, so 20 is a floor-ish value
    "adults_at_key5_median": 617,
    "settlements_at_key5_median": 29,
    "scholars_at_key5_median": 1,
    "people_at_key5_median": 1376,
    "soldiers_share_of_adults_median": 0.096,   # 51.5 soldiers of 617 adults
    "missionaries_median": 3,
    "idle_scholars_median": 2,
    "t11_kits_per_adult_median": 0.79,
    # Population growth per minute (1 minute = 1 game year at Standard 1x) implied by the 09 model: 7% to 8% a year through E4.
    "growth_per_minute": 0.06,             # Insight grows ~pop^0.85 => ~6%/min
}
