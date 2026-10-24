"""Acceptance checks C1–C5 of docs/MVP/11-e0-e2-project-specs.md §6.

Reads the per-project numbers in 10 (rates, stocks, food values) and 11 (costs,
Hazard primitives), derives what an average civilization gets from them in a
mean-field way, and compares the result with the targets in 09 and 07. Writes
nothing; the findings are in docs/MVP/07c-e0-e2-checks.md.

    python check_e0e2.py            # all checks, as specified in 10/11
    python check_e0e2.py --fixed    # with the fixes 07c recommends
"""
import math
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import balance_sim as B  # noqa: E402
import catalog_stats  # noqa: E402
import params as P  # noqa: E402

MVP = Path(__file__).resolve().parents[2]
FIXED = "--fixed" in sys.argv


# ---- doc 11: costs and Hazard primitives -------------------------------------------
def load_specs():
    specs = {}
    for line in (MVP / "11-e0-e2-project-specs.md").read_text().splitlines():
        m = re.match(r"^\| ((?:[A-Z]{3}|KEY)-\d+) \| ([^|]+) \| ([DMSRIWK]) \| (\d+) \|([^|]*)\|([^|]*)\|", line)
        if m:
            pid, name, typ, cost, req, eff = m.groups()
            specs[pid] = dict(id=pid, name=name.strip(), type=typ, cost=int(cost), effects=eff.strip())
    return specs


SPECS = load_specs()
PROJ = catalog_stats.load()


def hazards(pid):
    """[(target, mult)] from Hazard(cause, ×k) primitives; target may be 'injury of hunters'."""
    out = []
    for cause, k in re.findall(r"Hazard\(([^,]+), ×([0-9.]+)", SPECS[pid]["effects"]):
        out.append((cause.strip(), float(k)))
    return out


# ---- when an average civilization completes a project (research position) ----------
CLOSURES = {k: catalog_stats.closure(PROJ, f"KEY-{k}") for k in range(1, 11)}
CAP_AT = {}
for _id, _n, e, at, kind, mult, prob in P.CAPS:
    CAP_AT.setdefault(_id, (e + at, prob))


def position(pid):
    """(research position, take probability). Cap projects use 09's placement; other
    projects: on their own era's keystone closure -> mid-era; forced backfill for a
    later keystone -> 0.15 into that era; otherwise optional at mid-era, prob 0.5."""
    if pid in CAP_AT:
        return CAP_AT[pid]
    e = PROJ[pid]["era"]
    if pid in CLOSURES[e + 1]:
        return e + 0.5, 1.0
    if pid in CLOSURES[e + 2]:          # backfill forced by the next keystone but one
        return e + 1.15, 1.0
    return e + 0.5, 0.5                 # forced much later, or optional: taken in its era half the time


# ---- C3: hazards by cause -----------------------------------------------------------
# 09 §4.2 cause shares at E0 (adults / children). Elders: 40% senescence (no E0–E2
# project acts on it; ASSUMED), the other 60% split like adults.
SHARES = {"endemic": (0.30, 0.45), "epidemic": (0.0, 0.0), "starvation": (0.15, 0.20),
          "exposure": (0.15, 0.20), "injury": (0.25, 0.10), "predators": (0.05, 0.05),
          "violence": (0.10, 0.0)}
HUNTER_INJURY = 0.6   # share of adult injury deaths that happen hunting (E0–E2; ASSUMED)
AGE0_SHARE = 3 / 18   # 09 §4.1: age 0 carries 3× the child hazard, ages 1–4 1.5× -> 1/6 of child deaths
MATERNAL = 0.06       # childbirth share of the adult hazard (half of women's injury share; ASSUMED)
TARGET = {"child": [0.036, 0.033, 0.035], "adult": [0.012, 0.011, 0.012], "elder": [0.053, 0.053, 0.057]}

# 07c fixes: population-average Hazard multipliers = (doc 11 value)^0.25, rounded, plus the
# Neolithic rise on KEY-2 Sedentism (09 §4.1 "Neolithic paradox", §10 epidemics from E2).
FIX_HAZ = {
    "FIR-01": [("exposure", 0.92), ("predators", 0.88)],
    "FIR-02": [("exposure", 0.95)],
    "FIB-02": [("exposure", 0.96)],
    "BLD-01": [("exposure", 0.93)],
    "FOR-01": [("endemic", 0.98)],
    "HNT-03": [("injury of hunters", 0.90)],
    "FOD-01": [("endemic", 0.93)],
    "SOC-01": [("child", 0.97)],
    "SOC-02": [("starvation", 0.92)],
    "COM-01": [("predators", 0.95)],
    "DEF-01": [("predators", 0.80)],
    "FIB-04": [("exposure", 0.84)],
    "BLD-03": [("exposure", 0.88)],
    "HNT-05": [("injury of hunters", 0.92)],
    "HNT-06": [("injury of hunters", 0.92)],
    "HUS-01": [("predators", 0.92)],
    "FOD-04": [("age 0", 0.96)],
    "MED-01": [("endemic", 0.96)],
    "MED-02": [("injury", 0.92)],
    "GOV-01": [("violence", 0.84)],
    "WAY-02": [("starvation", 0.92)],
    "CHM-03": [("endemic", 0.99)],
    "MED-03": [("age 0", 0.93), ("maternal", 0.88)],
    "GOV-03": [("violence", 0.84)],
    "KEY-2": [("endemic", 1.30), ("epidemic+", 1.0)],
}
EPIDEMIC_E2 = 0.03  # 09 §10.6: epidemics are 3% of E2 deaths (added share, from KEY-2)


def hazard_ratio(pos, band, fixed):
    """Hazard of `band` at research position `pos`, relative to the E0 midpoint."""
    def mults(at_pos):
        m = {c: 1.0 for c in SHARES}
        m.update(child=1.0, age0=1.0, maternal=1.0, hunters=1.0, epi=0.0)
        for pid in SPECS:
            if pid not in PROJ or pid.startswith("KEY-"):
                if pid != "KEY-2":
                    continue
            p_at, prob = position(pid) if pid in PROJ else (2.0, 1.0)
            if p_at > at_pos:
                continue
            for cause, k in (FIX_HAZ.get(pid) if fixed and pid in FIX_HAZ else hazards(pid)):
                f = k ** prob
                if cause == "injury of hunters":
                    m["hunters"] *= f
                elif cause == "age 0":
                    m["age0"] *= f
                elif cause == "epidemic+":
                    m["epi"] = EPIDEMIC_E2
                elif cause in m:
                    m[cause] *= f
        return m

    def h(m):
        a = sum(s[0] * m[c] for c, s in SHARES.items())
        a += SHARES["injury"][0] * m["injury"] * HUNTER_INJURY * (m["hunters"] - 1)  # hunters' part
        a -= MATERNAL * (1 - m["maternal"])
        ch = sum(s[1] * m[c] for c, s in SHARES.items()) * m["child"]
        ch *= 1 - AGE0_SHARE * (1 - m["age0"])
        el = 0.4 + 0.6 * a
        out = {"adult": a, "child": ch, "elder": el}
        return {k: v / (1 - m["epi"]) for k, v in out.items()}

    return h(mults(pos))[band] / h(mults(0.5))[band]


def check_c3(fixed):
    print("\nC3 — hazards at era midpoints, relative to E0 (product of Hazard primitives vs 09 §4.1)")
    print("  band    E1 got  E1 tgt   E2 got  E2 tgt")
    worst = 0
    for band in ("child", "adult", "elder"):
        t = TARGET[band]
        g1, g2 = hazard_ratio(1.5, band, fixed), hazard_ratio(2.5, band, fixed)
        r1, r2 = t[1] / t[0], t[2] / t[0]
        worst = max(worst, abs(g1 / r1 - 1), abs(g2 / r2 - 1))
        print(f"  {band:6s}  {g1:6.3f}  {r1:6.3f}   {g2:6.3f}  {r2:6.3f}   "
              f"(E1 {100 * (g1 / r1 - 1):+.0f}%, E2 {100 * (g2 / r2 - 1):+.0f}%)")
    print(f"  worst deviation {100 * worst:.0f}% (target ±10%)")
    return worst


# ---- C5: keystone timing with the 04 openings ---------------------------------------
OPENINGS = {
    "Grower": (["FOR-01", "SOC-01", "FIR-01", "FOD-01", "SOC-02", "STN-01", "WOD-01", "FOR-02", "FIB-01", "COM-01",
                "COM-02", "ART-01", "ART-02", "STN-02", "FIR-02", "FIR-03", "STN-03", "STN-04", "WAY-01"],
               ["SOC-03", "WAY-02", "FIB-05", "FOR-03", "STN-05", "FOD-02", "STN-06", "FOR-04", "FOD-03", "MED-01",
                "FOR-05", "FOR-06", "FIB-02", "BLD-01", "WOD-02", "BLD-02", "BLD-03", "LRN-01", "LRN-02"]),
    "Builder": (["STN-01", "FIR-01", "FOR-01", "STN-02", "WOD-01", "FIB-01", "FIR-02", "STN-03", "FIR-03", "FIR-04",
                 "BLD-01", "STN-04", "FIB-02", "COM-01", "COM-02", "SOC-01"],
                ["STN-05", "FIR-06", "STN-06", "CHM-01", "CHM-02", "ART-01", "WOD-02", "MEC-01", "BLD-02", "BLD-03",
                 "FIB-03", "FIB-05", "MIN-01", "FOR-02", "WAY-01", "WAY-02", "FOR-03", "FOR-04", "FOD-01", "FOD-02",
                 "FOD-03"]),
    "Wanderer": (["WAY-01", "COM-01", "HNT-01", "TER-01", "DIP-01", "COM-02", "STN-01", "FIR-01", "FOR-01", "SOC-01",
                  "STN-02", "FIB-01", "FIR-02", "STN-03", "STN-04"],
                 ["WOD-01", "FOR-02", "WAY-02", "TER-02", "INQ-02", "WAY-03", "SEA-01", "HNT-02", "HNT-03", "INQ-01",
                  "HNT-04", "HUS-01", "TRN-01", "TRN-02", "SOC-02", "SOC-03", "DIP-02", "FIB-05", "FOR-03", "STN-05",
                  "STN-06", "FOR-04", "FIR-03", "FOD-01", "FOD-02", "FOD-03", "FIB-02", "BLD-01", "BLD-02", "ART-01",
                  "WOD-02", "BLD-03"]),
}


def cost_at(pid, era_now, base):
    """List cost from doc 11, rescaled to the EraBase in use, with EraLag for older eras."""
    p = SPECS[pid]
    e = PROJ[pid]["era"] if pid in PROJ else int(pid.split("-")[1]) - 1
    spec_base = [20, 45, 70][e]
    return p["cost"] * base[e] / spec_base * 0.7 ** max(0, era_now - e)


def check_c5(base, label):
    s = P.scenario("tuned")
    rows, D = B.run(s)
    cum = []  # cumulative research progress (Insight + Know-how), by season
    tot = 0.0
    for r in rows:
        tot += r["progress"]
        cum.append(tot)

    def minute_at(need):
        for t, c in enumerate(cum):
            if c >= need:
                return (t - 1) * s["SeasonSeconds"] / 60 + s["SeasonSeconds"] / 60 * (1 - (c - need) / max(1e-9, rows[t]["progress"]))
        return None

    print(f"\nC5 — keystone timing from the 04 openings at doc 11 list costs ({label}); target 7.5 / 17.5 / 29 ±25%")
    res, _, _ = B.summarize(rows, D, s)
    print(f"  balance model (N picks at average cost, EraBase {s['era']['EraBase'][:3]}): "
          f"KEY-1 {res[1]['start_min']:.2f}  KEY-2 {res[2]['start_min']:.2f}  KEY-3 {res[3]['start_min']:.2f}")
    k = [base[0] * 2.5, base[1] * 2.5, base[2] * 2.5]
    for name, (pre1, pre2) in OPENINGS.items():
        c1 = sum(cost_at(p, 0, base) for p in pre1) + k[0]
        c2 = c1 + sum(cost_at(p, 1, base) for p in pre2) + k[1]
        print(f"  {name:8s}: {len(pre1):2d} + KEY-1 = {c1:6.0f} Insight -> minute {minute_at(c1):5.2f};  "
              f"{len(pre2):2d} more + KEY-2 = {c2:6.0f} -> minute {minute_at(c2):5.2f}")
    return rows, s


# ---- C1 / C2: food caps from doc 10's stocks and yields ------------------------------
# Quantities doc 10 / 12 give (temperate forest, the 1.0 reference biome):
REGROWTH = {"Sp": 0.6, "Su": 1.0, "Au": 0.9, "Wi": 0.15}      # R per tile per season (10 §4.1)
HERDS_PER_TILE = 4 / 100                                        # 12 §4
HERD_YIELD = (22 * 0.30 * 4 + 30 * 0.18 * 15 + 15 * 0.40 * 5) / 3  # R/yr per herd at herd growth: deer, aurochs, boar (12 §6)
FERTILITY = 0.70                                                # soil, temperate forest (12 §4)
STAND_SHARE = 0.15      # grass-stand tiles in an average start's range (12 §4: open woodland 25%, forest 0)
# Quantities doc 10 does not give. "spec" = the neutral reading of 10/12 as written
# (ASSUMED where silent); "fixed" = the constants 07c recommends adding to 10.
CONST = {
    "spec": dict(
        RADIUS=[2, 2, 2],          # 09 §6.1: "a radius-2 home range (19 tiles)"; 10 never says
        REACH0=0.40, REACH_FOR01=0.52, REACH_FOR02=1.25,   # 10 §4.1
        ACCESS={"start": 0.25, "HNT-01": 0.375, "HNT-02": 0.45, "HNT-03": 0.55, "HNT-04": 0.70},  # ASSUMED; 10 gives only rates
        FIELD_SHARE=1.0,           # 10 sets no limit on field tiles: every range tile can be cleared
        FIELD_RAMP=0.7,            # fields reach full extent ~0.7 era after AGR-01
        NUT_SHARE=0.40, DRIED_SHARE=0.30, LEAN_OK=1.0),
    "fixed": dict(
        RADIUS=[3, 3, 3],          # daily forage/hunt range: radius 3 through E2 (the camp claim radius after 07b R3)
        REACH0=0.40, REACH_FOR01=0.52, REACH_FOR02=1.25,
        ACCESS={"start": 0.40, "HNT-01": 0.40, "HNT-02": 0.42, "HNT-03": 0.44, "HNT-04": 0.48},
        FIELD_SHARE=0.25,          # at most 25% of range tiles under fields in E2
        FIELD_RAMP=0.7,
        NUT_SHARE=0.40, DRIED_SHARE=0.30, LEAN_OK=0.85),  # 0.85 = the cap trigger: below it, winter decides
}


def food_cap(pos, age, c):
    """People one average settlement can feed at research position `pos` (stock-limited:
    sustainable regrowth and herd growth in range × food value ÷ per-capita need)."""
    done = lambda pid: position(pid)[0] <= pos  # noqa: E731
    e = max(0, min(2, math.ceil(pos) - 1)) if pos > 0 else 0
    r = c["RADIUS"][e]
    tiles = 3 * r * (r + 1) + 1
    cook = 1.3 if done("FOD-01") else 1.0       # meat, roots, cereals (10 §4.6)
    boil = 1.2 if done("FOD-04") else 1.0       # FOD-04 food value +20%
    grind = 1.25 if done("FOR-05") else 1.0     # cereals
    ferment = 1.15 if done("FOD-05") else 1.0   # grain
    # plants
    reach = c["REACH_FOR01"] if done("FOR-01") else c["REACH0"]
    roots = done("FOR-02")
    if roots:
        reach *= c["REACH_FOR02"]
    annual = sum(REGROWTH.values()) + (REGROWTH["Wi"] if roots else 0)   # FOR-02: winter stock ×2
    field_tiles = 0.0
    if done("AGR-01"):
        field_tiles = tiles * c["FIELD_SHARE"] * min(1.0, (pos - position("AGR-01")[0]) / c["FIELD_RAMP"])
    stand = 1.0
    if done("FOR-04"):
        stand += STAND_SHARE * 0.60 * (REGROWTH["Su"] + REGROWTH["Au"]) / annual
    if done("FOR-06"):
        stand += STAND_SHARE * 1.0
    plants = (tiles - field_tiles) * annual * reach * stand
    root_share = (c["REACH_FOR02"] - 1) / c["REACH_FOR02"] if roots else 0
    grain_share = (stand - 1) / stand + (STAND_SHARE * 0.4 if done("FOR-04") else 0)
    plants *= 1 + root_share * (cook * boil - 1) + grain_share * (cook * boil * grind - 1)
    # game: share of herds' sustainable yield the band takes, by its best hunting rung
    access = c["ACCESS"]["start"]
    for pid in ("HNT-01", "HNT-02", "HNT-03", "HNT-04"):
        if done(pid):
            access = c["ACCESS"][pid]
    if done("HUS-01"):
        access *= 1.10
    game = tiles * HERDS_PER_TILE * HERD_YIELD * access * cook * boil
    # fish (HNT-07, take 0.6): 2 river tiles in range at 1.2 R/season, half reachable with harpoons
    # FOD-01 cooking applies only to meat, roots and cereals; FOD-04's general food-value
    # bonus still applies to fish. Keep the fixed reference aligned with 10 §4.6.
    fish = (0.6 * 2 * 1.2 * 4 * 0.5 * boil) if done("HNT-07") else 0.0
    if done("HNT-09"):
        fish += 0.4 * 2.0 * 4 * boil                          # one weir, expected take 0.4
    # fields (10 §4.4)
    fields = 0.0
    if field_tiles:
        y = 8.0 if done("AGR-02") else 4.0
        if done("AGR-03"):
            y *= 1.08 ** 0.5
        fields = field_tiles * y * FERTILITY * cook * boil * grind * ferment
    ch, ad, el = age
    need = 4 * (0.6 * ch + 1.0 * ad + 0.8 * el)   # rations per person per year (10 §1)
    total = plants + game + fish + fields
    return total / need, dict(tiles=tiles, plants=plants, game=game, fish=fish, fields=fields, need=need,
                              field_tiles=field_tiles)


def ages(rows, minute):
    r = min(rows, key=lambda x: abs(x["minute"] - minute))
    return (r["C"] / r["pop"], r["A"] / r["pop"], r["El"] / r["pop"])


def check_c1(rows, fixed):
    c = CONST["fixed" if fixed else "spec"]
    print("\nC1 — FoodCap of an average settlement from doc 10's stocks and yields (target 20 → 27 → 41 → 88 ±10%)")
    marks = [("E0 start", 0.0, 0.0, 20), ("E0 end", 0.999, 7.75, 27), ("E1 end", 1.999, 17.75, 41), ("E2 end", 2.999, 29.5, 88)]
    worst = 0
    for lab, pos, minute, tgt in marks:
        cap, b = food_cap(pos, ages(rows, minute), c)
        worst = max(worst, abs(cap / tgt - 1))
        print(f"  {lab:8s} {cap:6.1f} vs {tgt:3d} ({100 * (cap / tgt - 1):+4.0f}%)   tiles {b['tiles']:3d}  plants {b['plants']:5.1f}"
              f"  game {b['game']:5.1f}  fish {b['fish']:4.1f}  fields {b['fields']:6.1f} ({b['field_tiles']:.0f} tiles) R/yr;"
              f" need {b['need']:.2f} R/person/yr")
    print(f"  worst deviation {100 * worst:.0f}%")
    return worst


def lean_share(pos, c):
    """Late-winter food ÷ winter need for a band at its FoodCap (C2).
    At the cap, a year's production equals a year's need, so the Spring–Autumn surplus
    is exactly the Winter shortfall. Lean = w + (1 − w) × keep, where w is Winter
    production ÷ Winter need and keep is the share of the stored surplus that survives
    ~1.5 seasons of storage (10 §4.6 spoilage)."""
    done = lambda pid: position(pid)[0] <= pos  # noqa: E731
    cap, b = food_cap(pos, (0.5, 0.4, 0.1), c)
    tot = b["plants"] + b["game"] + b["fish"] + b["fields"]
    annual = sum(REGROWTH.values()) + (REGROWTH["Wi"] if done("FOR-02") else 0)
    winter_plants = 4 * REGROWTH["Wi"] * (2 if done("FOR-02") else 1) / annual   # winter season ÷ average season
    w = (b["plants"] * winter_plants + b["game"] + b["fish"] * 0.5) / tot     # fish runs stop in winter (half)
    if b["fields"]:
        w = (b["plants"] * winter_plants + b["game"] + b["fish"] * 0.5) / tot   # fields give nothing in winter
    # what the surplus is made of, and how much of it survives to late winter
    spoil = {"greens": 0.40, "roots": 0.15, "nuts": 0.05, "grain": 0.08, "dried": 0.10, "domgrain": 0.05}
    mix = {"greens": 1.0}
    if done("FOR-02"):
        mix = {"greens": 0.75, "roots": 0.25}
    if done("FOR-03"):
        mix = {k: v * (1 - c["NUT_SHARE"]) for k, v in mix.items()}
        mix["nuts"] = c["NUT_SHARE"]
    if done("FOR-04"):
        mix = {k: v * 0.85 for k, v in mix.items()}
        mix["grain"] = 0.15
    if done("FOD-02"):
        mix = {k: v * (1 - c["DRIED_SHARE"]) for k, v in mix.items()}
        mix["dried"] = c["DRIED_SHARE"]
    if b["fields"]:
        mix = {"domgrain": 0.7, **{k: v * 0.3 for k, v in mix.items()}}
    pit = 0.5 if done("FOD-03") else 1.0
    pot = 0.75 if done("CER-02") else 1.0
    keep = sum(v * (1 - spoil[k] * pit * pot) ** 1.5 for k, v in mix.items())
    return w + (1 - w) * keep, w, keep


def check_c2(fixed):
    print("\nC2 — late-winter food share (target: reaches 0.85 by E1 ≈ 0.4; 09 LeanBase 0.77)")
    c = CONST["fixed" if fixed else "spec"]
    out = [(pos, *lean_share(pos, c)) for pos in (0.0, 0.5, 1.0, 1.3, 1.4, 1.5, 1.7, 2.0, 2.9)]
    print("  position " + "  ".join(f"{p:4.1f}" for p, *_ in out))
    print("  share    " + "  ".join(f"{v:4.2f}" for _, v, *_ in out))
    print("  winter w " + "  ".join(f"{w:4.2f}" for _, _, w, _ in out))
    print("  keep     " + "  ".join(f"{k:4.2f}" for *_, k in out))
    first = next((p for p, v, *_ in out if v >= c["LEAN_OK"]), None)
    print(f"  reaches {c['LEAN_OK']:.2f} at position {first}")
    return first


def check_c4():
    print("\nC4 — mean-field first-fission calibration (late-winter trigger), Standard, every archetype (target minute 7–9; live integration is reported in 07c §6)")
    for a in P.ARCHETYPES:
        s = P.scenario("tuned", "Standard", a)
        rows, D = B.run(s)
        _, ff, _ = B.summarize(rows, D, s)
        print(f"  {a:8s} {ff}")


if __name__ == "__main__":
    print("Mode:", "with 07c fixes" if FIXED else "as specified in 10/11")
    check_c3(FIXED)
    rows, s = check_c5(s_base := P.ERA["EraBase"]["tuned"], "model EraBase 22/50/90")
    check_c5([20, 45, 70], "doc 11 EraBase 20/45/70")
    check_c1(rows, FIXED)
    check_c2(FIXED)
    check_c4()
