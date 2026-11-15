"""Live Iron (E4) anchors used by the E5 oracles. One place to edit when a newer natural-play run is recorded.

Each anchor names its source run so a reader can tell how stale it is. `e5_anchor_check.py` prints the active block and checks the
copied constants against the source; `e5_research_oracle.py` reads `LIVE_E4` for its pacing sections.
"""

# Natural play, four Warlord seats (Builder, Grower, Warlord, Sage), default Iron chapter, `iron-match --seed 20261019 --seeds 8`, sim at
# main daf2ed5 merged into codex/e5-fief (Iron closeout, 48-season countdown, no year cap, late-project doubling, settlement cores).
# 32 civ-seats; all 32 reached KEY-4 and 31 reached KEY-5 (the last had 10.9 minutes of exposure left), so the interval is now almost
# uncensored. Source: balance/m12-wp5-merge-reanchor.md. The previous anchor (2026-11-05, 20 minutes, 617 adults) is superseded.
LIVE_E4 = {
    "label": "m12 merge re-anchor (2026-11-13), natural play, seeds 20261019-26, 31 of 32 seats finished E4",
    "key4_median_minute": 71.8,
    "key5_median_minute": 92.2,
    "key4_to_key5_minutes": 20.0,          # median 20.0, mean 20.3, p25-p75 18.8-22.0, range 16.3-25.2 (n=31)
    "iron_target_minutes": 25.0,           # the user's stated Iron target (docs/MVP/28 §7a decision, 2026-11-07); measured is lower
    "e4_projects_done_median": 56,         # every Iron project is done by KEY-5, so Iron's Insight spend is the whole age
    "adults_at_key5_median": 1049,
    "settlements_at_key5_median": 26,
    "scholars_at_key5_median": 4,
    "people_at_key5_median": 1925,
    "soldiers_share_of_adults_median": 0.12,   # 122 soldiers of 1,049 adults
    "missionaries_median": 7,
    "idle_scholars_median": 11,
    "t11_kits_per_adult_median": 0.24,
    "t12_kits_per_adult_median": 0.68,
    # Population growth per minute (1 minute = 1 game year at Standard 1x) implied by the 09 model: 7% to 8% a year through E4.
    "growth_per_minute": 0.06,             # Insight grows ~pop^0.85 => ~6%/min
}


def apply_iron_registry(e4m):
    """Point the imported e4_metal_oracle at the CURRENT registry values for the T11/T12 kits, so the E5 oracles never run on stale Iron inputs.

    The Iron balance closeout (2026-11-13) changed make_tool11 inputs and the T11/T12 service lives; e4_metal_oracle.py itself belongs to the
    Iron work and is left as it was. `e5_anchor_check.py` reports that file's defaults as INFO when they differ from the registries.
    """
    import re
    from pathlib import Path

    root = Path(__file__).resolve().parents[4]
    recipes = (root / "content" / "registries" / "recipes.yaml").read_text()
    tools = (root / "content" / "registries" / "tools.yaml").read_text()

    def recipe(rid):
        line = next(l for l in recipes.splitlines() if re.search(rf"\{{id: {rid},", l))
        ins = {k: float(v) for k, v in re.findall(r"(G-[A-Z0-9]+): ([0-9.]+)", line.split("inputs:")[1].split("outputs:")[0])}
        lab = float(re.search(r"labor: ([0-9.]+)", line).group(1))
        return ins, lab

    def service(tid):
        line = next(l for l in tools.splitlines() if re.search(rf"\{{id: {tid},", l))
        return float(re.search(r"service_ws: ([0-9.]+)", line).group(1))

    ins11, lab11 = recipe("make_tool11")
    ins12, lab12 = recipe("make_tool12")
    e4m.MAKE_T11 = dict(wrought=ins11["G-WROUGHT"], charcoal=ins11["G-CHAR"], kits=1.0, ws=lab11)
    e4m.MAKE_T12 = dict(steel=ins12["G-STEEL"], charcoal=ins12["G-CHAR"], kits=1.0, ws=lab12)
    e4m.SERVICE_WS = {"T10": service("t10"), "T11": service("t11"), "T12": service("t12")}
    return e4m
