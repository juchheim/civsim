"""C1-E3 FoodCap and C3-E3 hazards (docs/MVP/22 §9–§10, §15 T12–T13; findings in 07d §4).

Extends check_e0e2.py (the E0–E2 stock model of 07c §3 and the cause-share proxy of 07c §5) through E3:

C1-E3: an average settlement's FoodCap from stocks and yields at the end of E3, against 09's 88 → 190 (±10%).
  Mechanisms modeled: the ard's field limit 25% → 35% of the 37-tile range (AGR-05), irrigation ×1.5 on half the
  fields of river settlements (AGR-06; river share ASSUMED 0.5), manuring raising the field soil equilibrium
  (AGR-07: soil loss 6 → 2.4 per harvest, take 0.6), pastures for goats, sheep and cattle (10 §4.5 yields,
  pasture share ASSUMED 15% of the range), dairying ×1.5 on herd food (take 0.5), granaries ending grain pests
  (FOD-06), the solar calendar +10% on fields (take 0.3). Carts (TRN-05), redistribution (GOV-04) and market towns
  (TRD-05) have no stock mechanism yet in 10/23 and pass their 09 targets through (marked "target").
C3-E3: E3/E0 annual-effective child and adult hazard ratios and e45, from the E2 catalog proxy plus the E3 Hazard
  primitives of doc 22 §10 applied to 09 §4.2's E3 cause mix, the E3 epidemic share (plague from E3, 09 §10.6:
  10% of deaths) and WinterMult 1.25.

    uv run --no-project --with numpy python check_e3.py                   # as written in doc 22
    uv run --no-project --with numpy python check_e3.py --fixed           # with 07d R4 (field limit 40%, canals 75%)
    uv run --no-project --with numpy python check_e3.py --fixed --check   # assertions
"""
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import check_e0e2 as C  # noqa: E402

CONST = C.CONST["fixed"]
RANGE_TILES = 37
FIELD_LIMIT = {2: 0.25, 3: 0.35}
IRRIGABLE = 0.5            # share of a river settlement's fields within 2 tiles of the river (22 §9 "half"; 07d R4: 0.75)
RIVER_SHARE = 0.5          # ASSUMED: share of settlements [Near: River] (12 §7 rivers; doc 22 §9 "half the fields")
PASTURE_SHARE = 0.15       # ASSUMED: share of range tiles under pasture once HUS-03/04 are herded
PASTURE_YIELD = (6 * 0.8 + 2 * 2.5) / 2   # R per pasture tile per year: goats & sheep 6 × 0.8, cattle 2 × 2.5 (10 §4.5)
SOIL_REGEN = 14.0          # soil index regained per harvest cycle; equilibrium 100 r/(r + loss) = 70 at loss 6 (12 §4)
# research position at which each E3 mechanism is in place, and its take probability (09 §5.2 placement, 22 §12)
AT = {"HUS-02": (3.15, 1.0), "HUS-03": (3.15, 1.0), "HUS-04": (3.15, 1.0), "FOD-06": (3.15, 1.0),
      "AGR-05": (3.35, 1.0), "TRN-05": (3.40, 1.0), "HUS-05": (3.50, 0.5), "AGR-06": (3.55, 1.0),
      "TRD-05": (3.60, 1.0), "AGR-07": (3.60, 0.6), "GOV-04": (3.70, 1.0), "NUM-06": (3.80, 0.3)}
TARGET_PASS = {"TRN-05": 1.06, "GOV-04": 1.04, "TRD-05": 1.05}
TARGET = {3.0: 88.0, 3.999: 190.0}


def soil(loss):
    return SOIL_REGEN / (SOIL_REGEN + loss)


def food_cap_e3(pos, ages, verbose=False):
    """People an average settlement feeds at research position pos (>= 3), plus a breakdown in R/yr."""
    cap2, b = C.food_cap(2.999, ages, CONST)
    done = lambda pid: AT[pid][1] if pos >= AT[pid][0] else 0.0  # noqa: E731
    plants_per_tile = b["plants"] / (RANGE_TILES - b["field_tiles"])
    stack = b["fields"] / (b["field_tiles"] * C.FERTILITY * 8 * 1.08 ** 0.5)  # cook × boil × grind × ferment
    meat = 1.3 * 1.2                                                          # cooking and boiling on meat
    field_share = FIELD_LIMIT[3] if done("AGR-05") else FIELD_LIMIT[2]
    field_tiles = RANGE_TILES * field_share
    past_tiles = RANGE_TILES * PASTURE_SHARE * (1.0 if done("HUS-03") or done("HUS-04") else 0.0)
    plant_tiles = RANGE_TILES - field_tiles - past_tiles
    plants = plant_tiles * plants_per_tile
    game = b["game"] * (1.10 if done("HUS-02") else 1.0)     # herd following: wild herds stay (fear decay ×2)
    fish = b["fish"]
    fert = C.FERTILITY
    if done("AGR-07"):
        fert = C.FERTILITY * (1 + done("AGR-07") * (soil(2.4) / soil(6.0) - 1))
    y = 8.0 * 1.08 ** 0.5
    y *= 1 + 0.10 * done("NUM-06")
    y *= 1 + 0.5 * IRRIGABLE * RIVER_SHARE * done("AGR-06")  # ×1.5 on the irrigable fields of river settlements
    granary = 1.07 if done("FOD-06") else 1.0                # grain pests +10% a stored season, ~0.7 seasons stored
    fields = field_tiles * y * fert * stack * granary
    pasture = past_tiles * PASTURE_YIELD * meat * (1.5 ** done("HUS-05"))
    total = plants + game + fish + fields + pasture
    passthrough = 1.0
    for pid, m in TARGET_PASS.items():
        passthrough *= m ** done(pid)
    ch, ad, el = ages
    need = 4 * (0.6 * ch + 1.0 * ad + 0.8 * el)
    cap = total * passthrough / need
    return cap, dict(plants=plants, game=game, fish=fish, fields=fields, pasture=pasture, field_tiles=field_tiles,
                     pasture_tiles=past_tiles, need=need, passthrough=passthrough, fert=fert)


def check_c1(out=print):
    # age mix at the end of E2 and E3: 09's balance model at E2 end (07c §3: need 3.0), live mode at KEY-4
    ages_e2 = (0.58, 0.36, 0.06)
    ages_e3 = (0.62, 0.34, 0.04)   # live mode at KEY-4 (balance_sim live: 33% adults, few elders)
    out("C1-E3 — FoodCap of an average settlement (target 88 → 190 ±10%, 09 §5.3)")
    rows = []
    for lab, pos, ages, tgt in (("E3 start", 3.0, ages_e2, 88.0), ("E3 mid", 3.5, ages_e3, None), ("E3 end", 3.999, ages_e3, 190.0)):
        cap, b = food_cap_e3(pos, ages)
        rows.append((lab, cap, tgt))
        dev = f"({100 * (cap / tgt - 1):+.0f}%)" if tgt else ""
        out(f"  {lab:8s} {cap:6.1f} {('vs ' + str(int(tgt))) if tgt else '':6s} {dev:7s} plants {b['plants']:5.1f}  game {b['game']:5.1f}"
            f"  fish {b['fish']:4.1f}  fields {b['fields']:6.1f} ({b['field_tiles']:.1f} tiles, soil {b['fert']:.2f})"
            f"  pasture {b['pasture']:5.1f} ({b['pasture_tiles']:.1f} tiles)  ×{b['passthrough']:.3f} target pass-through;"
            f" need {b['need']:.2f}")
    # what each mechanism contributes at E3 end (one at a time removed)
    base, _ = food_cap_e3(3.999, ages_e3)
    out("  contribution at E3 end (cap without it -> ratio):")
    for pid in AT:
        saved = AT[pid]
        AT[pid] = (9.9, saved[1])
        c, _ = food_cap_e3(3.999, ages_e3)
        AT[pid] = saved
        tgt = {"AGR-05": 1.17, "AGR-06": 1.17, "AGR-07": 1.10 ** 0.6, "HUS-05": 1.08 ** 0.5, "FOD-06": 1.05,
               "HUS-02": 1.04, "HUS-03": 1.08, "HUS-04": 1.05, "NUM-06": 1.0, **TARGET_PASS}.get(pid)
        out(f"    {pid}: ×{base / c:.3f}" + (f"  (09 target ×{tgt:.3f})" if tgt else ""))
    end = rows[-1][1]
    out(f"  E3 end {end:.1f} vs 190: {100 * (end / 190 - 1):+.1f}% ({'pass' if abs(end / 190 - 1) <= 0.10 else 'FAIL'} ±10%)")
    return rows


# ---- C3-E3 -----------------------------------------------------------------------------
# 09 §4.2 cause shares at E3 (adults / children); elders: 40% senescence, 60% adult mix (07c §5)
SHARES_E3 = {"endemic": (0.35, 0.50), "epidemic": (0.10, 0.10), "starvation": (0.10, 0.15), "exposure": (0.10, 0.10),
             "injury": (0.20, 0.10), "predators": (0.0, 0.0), "violence": (0.15, 0.05)}
# E3 Hazard primitives (22 §10, §12): (cause, multiplier, take probability)
E3_HAZ = [("injury", 0.92, 1.0, "MED-04"), ("endemic", 0.96, 0.5, "CHM-05"), ("injury", 0.97, 0.5, "CHM-05"),
          ("violence", 0.85, 1.0, "GOV-06"), ("injury", 1 + 0.03 * 0.3, 0.5, "MET-04 smiths ×1.3 (3% of adults)"),
          ("starvation", 0.75, 1.0, "GOV-04 local famine deaths −50% (half of starvation is local famine)"),
          ("epidemic", 0.90, 0.5, "CHM-05 sanitation 0.10 on outbreak pressure")]
E3_POS = {"MED-04": 0.5, "CHM-05": 0.5, "GOV-06": 0.85, "MET-04": 0.4, "GOV-04": 0.7}


def e45(h45, slope=0.085, cap=4.0):
    """Remaining life expectancy at 45 under h(a) = h45 e^(slope (a − 45)), annual steps."""
    s, e = 1.0, 0.0
    for a in range(45, 120):
        h = min(cap, h45 * math.exp(slope * (a - 45)))
        q = 1 - math.exp(-h)
        e += s * (1 - q / 2)
        s *= 1 - q
    return e


def check_c3(out=print):
    out("\nC3-E3 — hazards at the E3 midpoint relative to E0, annual-effective (09 §4.1 ×(3 + WinterMult)/4)")
    # E2 proxy (07c §5, current doc 11 values), as a ratio of non-winter hazards
    e2 = {band: C.hazard_ratio(2.5, band, False) for band in ("child", "adult", "elder")}

    def mult(band, mid=True):
        idx = 1 if band == "child" else 0
        tot = sum(v[idx] for v in SHARES_E3.values())
        m = {c: 1.0 for c in SHARES_E3}
        for cause, k, prob, src in E3_HAZ:
            key = src.split()[0]
            at = E3_POS.get(key, 0.5)
            if mid and at > 0.5:
                continue
            m[cause] *= k ** prob
        return sum(SHARES_E3[c][idx] * m[c] for c in SHARES_E3) / tot

    # the E3 epidemic share is part of the 09 table (inside the anchors), so E3 base = E2 proxy × E3 project effects
    winter = {0: 2.0, 3: 1.25}
    tgt_child = 0.034 * (3 + 1.25) / (0.036 * (3 + 2.0))
    tgt_adult = 0.011 * (3 + 1.25) / (0.012 * (3 + 2.0))
    res = {}
    for band, tgt in (("child", tgt_child), ("adult", tgt_adult)):
        nonwinter = e2[band] * mult(band) * (0.034 / 0.035 if band == "child" else 0.011 / 0.012) ** 0  # projects only
        got = nonwinter * (3 + winter[3]) / (3 + winter[0])
        res[band] = (got, tgt)
        out(f"  {band:6s} E3/E0 {got:.3f} vs target {tgt:.3f} ({100 * (got / tgt - 1):+.1f}%)"
            f"  [E2 proxy {e2[band]:.3f} × E3 projects {mult(band):.3f} × winter {(3 + 1.25) / 5:.3f}]")
    # e45: elder = 40% senescence (no E3 project acts on it) + 60% adult mix
    adult_end = mult("adult", mid=False)
    h45_e2 = 0.021
    h45_e3 = h45_e2 * (0.4 + 0.6 * adult_end) * (3 + 1.25) / (3 + 1.4)
    ex = e45(h45_e3)
    out(f"  e45: h45 {h45_e2} × (0.4 + 0.6 × {adult_end:.3f}) × winter {(3 + 1.25) / (3 + 1.4):.3f} = {h45_e3:.4f} -> e45 "
        f"{ex:.1f} vs 17 ±10% ({'pass' if abs(ex / 17 - 1) <= 0.10 else 'FAIL'}); 09's h45 0.018 gives {e45(0.018):.1f}")
    res["e45"] = (ex, 17.0)
    out("  (live C3-E3 is a harness measurement: run_headless_balance.py with E3 completions once MaxEra is 3)")
    return res


if __name__ == "__main__":
    if "--fixed" in sys.argv:          # 07d R4
        IRRIGABLE = 0.75
        FIELD_LIMIT[3] = 0.40
    print("Mode:", "with 07d R4 (field limit 40%, canals reach 75% of a river settlement's fields)" if "--fixed" in sys.argv
          else "as written in doc 22 (field limit 35%, canals on half the fields)")
    rows = check_c1()
    res = check_c3()
    if "--check" in sys.argv:
        assert abs(rows[-1][1] / 190 - 1) <= 0.10, rows
        for band in ("child", "adult", "e45"):
            got, tgt = res[band]
            assert abs(got / tgt - 1) <= 0.10, (band, got, tgt)
        print("check_e3 passed")
