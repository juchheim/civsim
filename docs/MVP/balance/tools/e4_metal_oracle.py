"""Provisional E4 metallurgy/tool/fuel ledger (26 §§2.1–2.3; 25 §13).

Standalone batch accounting. Existing E3 bloomery and charcoal recipes are held fixed.
This is an embodied-input ledger, not a viability or live-economy claim.
"""
import argparse
import math

# Existing E3 recipes, as specified in 26 §2.3.
BLOOMERY = dict(ore=2.0, charcoal=3.0, iron=0.5, ws=0.8)
CHARCOAL = dict(wood=6.0, charcoal=1.0, ws=0.4)

# Proposed E4 batch recipes, 26 §2.3.
FORGE_WROUGHT = dict(iron=0.5, charcoal=0.25, wrought=0.4, ws=0.4)
CARBURIZE = dict(wrought=0.4, charcoal=0.2, steel=0.4, ws=0.4)
MAKE_T11 = dict(wrought=0.15, charcoal=0.15, kits=1.0, ws=0.2)
MAKE_T12 = dict(steel=0.15, charcoal=0.10, kits=1.0, ws=0.3)

# 22 §5.6 defaults carried by metal_sim.py; rate applied to tool-work only.
TOOL_WORK_SHARE = 0.60
WS_PER_ADULT_SEASON = 38 / 35
SERVICE_WS = {"T10": 24.0, "T11": 24.0, "T12": 48.0}


def embodied(tier):
    """Return fully reconciled per-kit inputs, separating recipe-direct and upstream."""
    if tier == "T11":
        wrought = MAKE_T11["wrought"] / FORGE_WROUGHT["wrought"]
        iron = wrought * FORGE_WROUGHT["iron"]
        bloom_batches = iron / BLOOMERY["iron"]
        ore = bloom_batches * BLOOMERY["ore"]
        char_bloom = bloom_batches * BLOOMERY["charcoal"]
        char_wrought = wrought * FORGE_WROUGHT["charcoal"]
        charcoal = char_bloom + char_wrought + MAKE_T11["charcoal"]
        ws = (bloom_batches * BLOOMERY["ws"] + wrought * FORGE_WROUGHT["ws"] + MAKE_T11["ws"]
              + charcoal * CHARCOAL["ws"])
        return dict(ore=ore, charcoal=charcoal, wood=charcoal * CHARCOAL["wood"], ws=ws,
                    direct_charcoal=MAKE_T11["charcoal"], upstream_charcoal=char_bloom + char_wrought,
                    bloomery_ws=bloom_batches * BLOOMERY["ws"], wrought_ws=wrought * FORGE_WROUGHT["ws"],
                    final_recipe_ws=MAKE_T11["ws"], charcoal_burning_ws=charcoal * CHARCOAL["ws"])
    if tier == "T12":
        steel = MAKE_T12["steel"] / CARBURIZE["steel"]
        wrought = steel * CARBURIZE["wrought"]
        wrought_batches = wrought / FORGE_WROUGHT["wrought"]
        iron = wrought_batches * FORGE_WROUGHT["iron"]
        bloom_batches = iron / BLOOMERY["iron"]
        ore = bloom_batches * BLOOMERY["ore"]
        char_bloom = bloom_batches * BLOOMERY["charcoal"]
        char_wrought = wrought_batches * FORGE_WROUGHT["charcoal"]
        char_steel = steel * CARBURIZE["charcoal"]
        charcoal = char_bloom + char_wrought + char_steel + MAKE_T12["charcoal"]
        ws_bloom = bloom_batches * BLOOMERY["ws"]
        ws_wrought = wrought_batches * FORGE_WROUGHT["ws"]
        ws_steel = steel * CARBURIZE["ws"]
        ws_make = MAKE_T12["ws"]
        ws_char = charcoal * CHARCOAL["ws"]
        return dict(ore=ore, charcoal=charcoal, wood=charcoal * CHARCOAL["wood"],
                    ws=ws_bloom + ws_wrought + ws_steel + ws_make + ws_char,
                    direct_charcoal=MAKE_T12["charcoal"], upstream_charcoal=char_bloom + char_wrought + char_steel,
                    bloomery_ws=ws_bloom, wrought_ws=ws_wrought, carburize_ws=ws_steel,
                    final_recipe_ws=ws_make, charcoal_burning_ws=ws_char)
    raise ValueError(f"unknown tier: {tier}")


def replacement(tier, adults=100.0):
    """Steady replacement per season for a group of adults; no startup/research costs."""
    kits = adults * WS_PER_ADULT_SEASON * TOOL_WORK_SHARE / SERVICE_WS[tier]
    unit = embodied(tier)
    return dict(kits=kits, adults=adults, ore=kits * unit["ore"], charcoal=kits * unit["charcoal"],
                wood=kits * unit["wood"], ws=kits * unit["ws"], labor_share=kits * unit["ws"] /
                (adults * WS_PER_ADULT_SEASON))


def cohort_screen(deliveries=None, *, adults=100.0, tier="T11", access_end=None, horizon=12,
                  initial_t10_age_ws=(18.0, 12.0, 6.0, 0.0)):
    """Track adult kit cohorts; deliveries replace the lowest tier one-for-one.

    Delivery schedule keys are zero-based seasons. `access_end` is the first season
    without route/access. A sustainable-route claim is right-censored if access ends
    before the horizon. T10's 24 tool-ws service and replacement fallback are explicit.
    """
    if tier not in ("T11", "T12"):
        raise ValueError("tier must be T11 or T12")
    deliveries = dict(deliveries or {})
    if any(s < 0 or n < 0 for s, n in deliveries.items()):
        raise ValueError("delivery seasons and counts must be nonnegative")
    cohorts = [["T10", adults / len(initial_t10_age_ws), SERVICE_WS["T10"] - age]
               for age in initial_t10_age_ws]
    total_delivered = 0.0
    history = []
    work_ws = WS_PER_ADULT_SEASON * TOOL_WORK_SHARE
    for season in range(horizon):
        supplied = deliveries.get(season, 0.0) if access_end is None or season < access_end else 0.0
        total_delivered += supplied
        need = supplied
        replaced = []
        kept = []
        for c_tier, count, remaining in sorted(cohorts, key=lambda c: (c[0], c[2])):
            take = min(count, need) if need > 1e-12 else 0.0
            if take:
                replaced.append([tier, take, SERVICE_WS[tier]])
                need -= take
            if count - take > 1e-12:
                kept.append([c_tier, count - take, remaining])
        if need > 1e-8:
            raise ValueError(f"deliveries exceed population by {need}")
        cohorts = kept + replaced
        advanced = []
        for c_tier, count, remaining in cohorts:
            remaining -= work_ws
            if remaining <= 1e-12:
                # On breakage, revert to a fresh T10 kit (including excess work).
                remaining = SERVICE_WS["T10"] + remaining
                c_tier = "T10"
            advanced.append([c_tier, count, remaining])
        merged = {}
        for c_tier, count, remaining in advanced:
            key = (c_tier, round(remaining, 12))
            merged[key] = merged.get(key, 0.0) + count
        cohorts = [[t, n, rem] for (t, rem), n in merged.items()]
        coverage = sum(n for t, n, _ in cohorts if t in ("T11", "T12")) / adults
        history.append(dict(season=season + 1, coverage=coverage, delivered=supplied))
    censored = access_end is not None and access_end < horizon
    return dict(cohorts=cohorts, delivered=total_delivered, history=history, endpoint=history[-1],
                right_censored=censored, censor_season=access_end if censored else None)


def minimum_deliveries_by_12(tier="T11", adults=100.0, target=0.70):
    """Minimum one-time season-1 deliveries meeting target at season 12, access held."""
    lo, hi = 0, math.ceil(adults)
    while lo < hi:
        mid = (lo + hi) // 2
        if cohort_screen({0: mid}, adults=adults, tier=tier)["endpoint"]["coverage"] >= target:
            hi = mid
        else:
            lo = mid + 1
    return lo


def cohort_report():
    print("\nI-T2 cohort screen — deterministic sensitivity, not an unspecified-supply pass")
    print("  assumptions: 100 adults; four equal T10 age cohorts (18/12/6/0 ws already used); "
          "0.65143 tool-ws/adult/season; T10/T11 service 24 tool-ws, T12 service 48 tool-ws.")
    print("  route held accessible through season 12; new kits replace lowest tiers first.")
    for tier in ("T11", "T12"):
        n = minimum_deliveries_by_12(tier)
        result = cohort_screen({0: n}, tier=tier)
        print(f"  {tier}: minimum {n} deliveries by season 12; season-12 T11+ coverage "
              f"{100*result['endpoint']['coverage']:.1f}% (one-time delivery in season 1)")
    stopped = cohort_screen({0: 70, 1: 10}, tier="T11", access_end=2)
    print(f"  censoring example: 80 delivered by season 2, access ends before season 3; "
          f"season-12 observed coverage {100*stopped['endpoint']['coverage']:.1f}%; "
          f"sustainable-route result right-censored at season {stopped['censor_season']}.")


def check():
    t11, t12 = embodied("T11"), embodied("T12")
    expected = (0.75, 1.36875, 8.2125, 1.1975)
    actual = tuple(t11[k] for k in ("ore", "charcoal", "wood", "ws"))
    assert all(abs(a - e) < 1e-12 for a, e in zip(actual, expected)), actual
    assert abs(t12["ore"] - 0.75) < 1e-12
    assert abs(t12["charcoal"] - 1.39375) < 1e-12
    assert abs(t12["wood"] - 8.3625) < 1e-12
    assert abs(t12["ws"] - 1.4575) < 1e-12
    for tier in ("T11", "T12"):
        d = replacement(tier)
        assert abs(d["kits"] * SERVICE_WS[tier] - 100 * WS_PER_ADULT_SEASON * TOOL_WORK_SHARE) < 1e-12
    mixed = cohort_screen({0: 30, 1: 20}, tier="T11")
    assert abs(sum(n for _, n, _ in mixed["cohorts"]) - 100) < 1e-9
    assert mixed["delivered"] == 50
    # Replacement retires an old kit; each delivered kit appears once with one service clock.
    single = cohort_screen({0: 1}, tier="T11")
    t11_cohorts = [(n, rem) for t, n, rem in single["cohorts"] if t == "T11"]
    assert abs(sum(n for n, _ in t11_cohorts) - 1) < 1e-12
    assert len(t11_cohorts) == 1 and abs(t11_cohorts[0][1] -
                                         (SERVICE_WS["T11"] - 12 * WS_PER_ADULT_SEASON * TOOL_WORK_SHARE)) < 1e-9
    assert abs(sum(n for _, n, _ in single["cohorts"]) - 100) < 1e-9
    stopped = cohort_screen({0: 70}, tier="T11", access_end=1)
    assert stopped["right_censored"] and stopped["censor_season"] == 1
    assert abs(stopped["endpoint"]["coverage"] - 0.70) < 1e-12
    print("E4 metal oracle check passed (T11 and T12 embodied ledgers; replacement reconciliation)")


def report():
    print("E4 metallurgy/tool/fuel oracle — PROVISIONAL batch accounting; not a viability claim")
    print("Fixed upstream recipes: E3 bloomery 2 ore + 3 charcoal -> 0.5 iron (0.8 ws); charcoal 6 wood -> 1 (0.4 ws).")
    for tier in ("T11", "T12"):
        d = embodied(tier)
        print(f"\n{tier} per kit: ore {d['ore']:.5f}, charcoal {d['charcoal']:.5f} "
              f"(direct {d['direct_charcoal']:.5f}; upstream {d['upstream_charcoal']:.5f}), "
              f"wood {d['wood']:.5f}, labor {d['ws']:.5f} ws")
        parts = [f"bloomery {d['bloomery_ws']:.5f}"]
        if tier == "T11":
            parts.append(f"wrought forge {d['wrought_ws']:.5f}")
        else:
            parts.extend((f"wrought forge {d['wrought_ws']:.5f}", f"carburize {d['carburize_ws']:.5f}"))
        parts.extend((f"kit recipe {d['final_recipe_ws']:.5f}", f"charcoal burning {d['charcoal_burning_ws']:.5f}"))
        print("  labor components (ws): " + "; ".join(parts))
    print("\nSteady kit replacement per season, per 100 adults; excludes mining, hauling, startup, and all non-kit demand")
    print(f"  defaults: tool-work share {TOOL_WORK_SHARE:.0%}; {WS_PER_ADULT_SEASON:.5f} ws/adult/season; "
          f"replacement rate = tool-work ws / kit service life")
    for tier in ("T11", "T12"):
        d = replacement(tier)
        print(f"  {tier}: {d['kits']:.5f} kits; ore {d['ore']:.5f}; charcoal {d['charcoal']:.5f}; "
              f"wood {d['wood']:.5f}; labor {d['ws']:.5f} ws ({100*d['labor_share']:.3f}% of adult work)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="run deterministic ledger self-checks")
    args = parser.parse_args()
    if args.check:
        check()
    else:
        report()
        cohort_report()
