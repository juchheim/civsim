"""Cohesion, Overreach, Admin span and secession in E3 (docs/MVP/22 §8.2–8.3, doc 23 §1.2, §12, §20.7; §15 T25;
findings in 07d §9).

Settlement layouts come from territory_sim's E3 runs on the live trajectories (8 seeds × 4 civs; walking distance
of each settlement to the capital, the civilization's start until a Palace stands, doc 23 C13), settlement count
over time from the same trajectories, research positions from the live-mode E3 (13 minutes). Each season each
settlement's C (0–100) moves a quarter of the way to its target (integer steps toward zero, at least one point while different,
22 §8.3); it secedes
after 8 consecutive seasons below 15. Stochastic events per settlement (ASSUMED rates, 07d §9): harsh-winter food
shortfall, epidemics by class (09 §10), raids (T23), captures (T23). A capture adds the conquered Cohesion term; it does
not itself count as a raid.

    uv run --no-project --with numpy python cohesion_sim.py            # T25 table, variants and sensitivity
    uv run --no-project --with numpy python cohesion_sim.py --check
"""
import math
import random
import statistics as st
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

# research position (share of E3) and take probability of each Cohesion/administration project (22 §12, closure = 1)
PROJ = {"COM-05": (0.40, 1.0), "TRN-05": (0.40, 1.0), "SOC-08": (0.45, 1.0), "BEL-06": (0.55, 0.6),
        "NUM-05": (0.60, 0.4), "GOV-04": (0.70, 1.0), "COM-06": (0.70, 0.4), "ART-07": (0.70, 0.3),
        "TER-06": (0.70, 0.3), "TRN-06": (0.65, 0.3), "GOV-05": (0.75, 1.0), "GOV-06": (0.85, 1.0),
        "BEL-07": (0.90, 0.3)}
SPAN = {"COM-05": 2, "GOV-04": 2, "GOV-05": 3, "NUM-05": 2, "GOV-06": 2}
REDUCE = {"GOV-04": 0.10, "COM-05": 0.20, "COM-06": 0.10, "NUM-05": 0.20, "GOV-06": 0.15, "BEL-07": 0.10,
          "TER-06": 0.10, "TRN-05": 0.10, "TRN-06": 0.10}
EV = dict(
    p_shrine=0.5, p_gov03=0.5, luxury=1.0, legends=0.3,          # E2 optional terms (BEL-05, GOV-03), luxuries/10 people
    p_harsh=0.30, p_short_if_harsh=0.15, short_mean=0.20,         # villages sit at ~1/3 of FoodCap: shortfalls are rare
    epi_per_year=0.049,                                           # 09 §10.6 E3 outbreaks per settlement per year
    epi_classes=((0.50, 0.017), (0.30, 0.055), (0.15, 0.147), (0.05, 0.28)),  # fever, flux, pox, plague: share, deaths
    raid_per_year=0.03,                                           # T23: 3–12 raids a chapter over ~28 settlements in E3
    capture_per_civ=0.10,                                         # T23: ≥ 1 capture in 20–50% of matches, 4 civs
    megalith_p=0.5,
)
RULE = dict(base=60, hearth=5, feast=8, shrine=3, gov03=3, temple=6, temple_near=2, state_cult=4, gov06=5, epic=5,
            legend=2, megalith=10, famine=20, loss_per2=1, raided=10, inequality=5, overreach_free=8, overreach_div=2,
            span_pen=3, conquered=20, conquered_recovery=2, secede_below=15, secede_seasons=8, start_c=67,
            found_c=60, step=0.25, culture_years=0, capture_c=40)


def layouts():
    """Per civ: settlement walking distances to the capital (by founding order) and the S(t) curve from KEY-3."""
    import territory_sim as T
    import live_data as L
    seeds = sorted({r["seed"] for r in L.load()})
    maps = [1, 3, 4, 6]
    out = []
    for i, sd in enumerate(seeds):
        res = T.run_e3(sd, maps[i % 4], "village", True)
        m = res["map"]
        for c in range(4):
            k3 = res["trajs"][c]["k3"]
            if k3 is None:
                continue
            cv = res["civs"][c]
            wd = m.walk_dist([cv.setts[0]], 0, 80)
            dist = [int(min(80, wd[x])) for x in cv.setts]
            S = [r["S"][c] for r in res["rows"]]
            out.append(dict(seed=sd, civ=c, k3=k3, dist=dist, S=S))
    return out


def run_civ(lay, rng, rule=None, ev=None, seasons=64, e3_seasons=52, admin=True, force_capture=False):
    rule = dict(RULE, **(rule or {}))
    ev = dict(EV, **(ev or {}))
    k3_step = int(round(lay["k3"] * 4))
    taken = {p: rng.random() < pr for p, (at, pr) in PROJ.items()}
    shrine = rng.random() < ev["p_shrine"]
    gov03 = rng.random() < ev["p_gov03"]
    mega = rng.random() < ev["megalith_p"]
    setts = []      # dicts: C, low, raided_until, conquered, famine, losses
    seceded, seced_steps = 0, []
    cs = []
    capture_step = (k3_step + rng.randrange(e3_seasons)) if (force_capture or rng.random() < ev["capture_per_civ"]) else None
    for k in range(seasons):
        step = k3_step + k
        x = k / e3_seasons
        S = lay["S"][min(step, len(lay["S"]) - 1)]
        while len(setts) < min(S, len(lay["dist"])):
            i = len(setts)
            setts.append(dict(C=rule["start_c"] if i < 3 else rule["found_c"], low=0, raided=-99, conq=0, famine=0.0,
                              loss=0.0, dist=lay["dist"][i], alive=True))
        done = lambda p: x >= PROJ[p][0] and taken[p] and admin  # noqa: E731
        span = 4 + sum(v for p, v in SPAN.items() if done(p))
        red = 1 - math.prod(1 - r for p, r in REDUCE.items() if done(p))
        alive = [s for s in setts if s["alive"]]
        n_alive = len(alive)
        winter = k % 4 == 3
        for i, s in enumerate(setts):
            if not s["alive"]:
                continue
            if winter:
                s["famine"] = 0.0
                if rng.random() < ev["p_harsh"] and rng.random() < ev["p_short_if_harsh"]:
                    s["famine"] = min(1.0, rng.expovariate(1 / ev["short_mean"]))
                s["loss"] = 0.0
            if rng.random() < ev["epi_per_year"] / 4:
                u, acc = rng.random(), 0.0
                for share, d in ev["epi_classes"]:
                    acc += share
                    if u <= acc:
                        s["loss"] += d
                        break
            if rng.random() < ev["raid_per_year"] / 4 and i > 0:
                s["raided"] = k
            cap = i == 0
            culture = not (rule["culture_years"] and s["conq"] > 0)   # R7: the conquered do not share our hearths and feasts
            t = rule["base"] + culture * (rule["hearth"] + rule["feast"] + rule["shrine"] * shrine + rule["gov03"] * gov03)
            t += min(5, ev["luxury"]) + rule["legend"] * ev["legends"]
            if done("BEL-06") and cap:
                t += rule["temple"]
            elif done("BEL-06") and s["dist"] <= 10:
                t += rule["temple_near"]
            t += rule["state_cult"] * done("BEL-07") + rule["gov06"] * done("GOV-06") + rule["epic"] * done("ART-07")
            if mega and s["dist"] <= 8:
                t += rule["megalith"]
            t -= rule["famine"] * s["famine"]
            t -= rule["loss_per2"] * max(0.0, (s["loss"] - 0.03) * 100 / 2)
            if k - s["raided"] < 8:
                t -= rule["raided"]
            t -= rule["inequality"] * (x >= PROJ["SOC-08"][0])
            if not cap:
                t -= max(0, s["dist"] - rule["overreach_free"]) / rule["overreach_div"] * (1 - red)
                t -= rule["span_pen"] * max(0, n_alive - span)
            t -= s["conq"]
            s["conq"] = max(0, s["conq"] - rule["conquered_recovery"])
            t = max(0, min(100, int(t)))
            diff = t - s["C"]
            delta = int(diff * rule["step"])
            if diff < 0:
                delta = -int(-diff * rule["step"])
            if delta == 0 and diff != 0:
                delta = 1 if diff > 0 else -1
            s["C"] = max(0, min(100, s["C"] + delta))
            s["low"] = s["low"] + 1 if s["C"] < rule["secede_below"] else 0
            if s["low"] >= rule["secede_seasons"] and not cap:
                s["alive"] = False
                # T25 is measured over KEY-3→KEY-4 exposure, not the post-KEY-4 chapter tail.
                if k < e3_seasons:
                    seceded += 1
                    seced_steps.append(k)
            if k < e3_seasons:
                cs.append(s["C"])
        # Live order is Economy.CivicSeasonEnd, then Politics capture handling. The new settlement starts at C25 / −35
        # after the current cohesion update, so its first target step, term recovery and low-season check are next season.
        if capture_step is not None and step == capture_step:
            setts.append(dict(C=rule["capture_c"], low=0, raided=-99, conq=rule["conquered"], famine=0.0, loss=0.05,
                              dist=rng.randint(25, 40), alive=True))
            if k < e3_seasons:
                cs.append(rule["capture_c"])
    burst = any(sum(1 for t2 in seced_steps if t1 <= t2 < t1 + 8) > 2 for t1 in seced_steps)
    return seceded, cs, burst


def mc(lays, runs=30, seed=7, **kw):
    rng = random.Random(seed)
    sec, cs, bursts, n = [], [], 0, 0
    for _ in range(runs):
        for lay in lays:
            s, c, b = run_civ(lay, rng, **kw)
            sec.append(s)
            cs += c
            bursts += b
            n += 1
    cs.sort()
    return dict(sec_per_civ=st.mean(sec), p_any=sum(1 for v in sec if v) / n, burst=bursts / n,
                c_mean=st.mean(cs), c10=cs[len(cs) // 10], c90=cs[9 * len(cs) // 10], c_low=sum(1 for v in cs if v < 35) / len(cs))


def check_model_semantics():
    """Small deterministic checks for capture order, capture/raid distinction and T25 exposure boundary."""
    layout = {"k3": 0, "dist": [0], "S": [1] * 100}
    quiet_rule = dict(base=0, hearth=0, feast=0, shrine=0, gov03=0, temple=0, temple_near=0,
                      state_cult=0, gov06=0, epic=0, legend=0, megalith=0, famine=0, loss_per2=0,
                      inequality=0, overreach_free=100, overreach_div=2, span_pen=0, conquered=0,
                      conquered_recovery=0, secede_below=15, secede_seasons=8, start_c=0, found_c=0,
                      step=0.25, culture_years=0, capture_c=0)
    quiet_events = dict(p_shrine=0, p_gov03=0, p_harsh=0, p_short_if_harsh=0, epi_per_year=0,
                        raid_per_year=0, capture_per_civ=0, megalith_p=0, luxury=0, legends=0)
    capture = run_civ(layout, random.Random(19), rule=quiet_rule, ev=quiet_events,
                      seasons=64, e3_seasons=52, force_capture=True)
    no_raid_term = run_civ(layout, random.Random(19), rule=dict(quiet_rule, raided=0), ev=quiet_events,
                           seasons=64, e3_seasons=52, force_capture=True)
    assert capture == no_raid_term, (capture, no_raid_term)

    class CaptureAt(random.Random):
        def __init__(self, offset):
            super().__init__(0)
            self.offset = offset

        def random(self):
            return 0.999

        def randrange(self, stop):
            return self.offset

        def randint(self, a, b):
            return 30

    # With no pre-existing settlements, a capture at season 0 is observed at C25 after that season's cohesion pass.
    # The first post-capture pass still targets 25 (−35 term), then recovery shifts the next target to 26.
    capture_only = {"k3": 0, "dist": [], "S": [0] * 100}
    captured_c = run_civ(capture_only, CaptureAt(0), rule=dict(quiet_rule, base=60, conquered=35,
                              conquered_recovery=1, capture_c=25), ev=quiet_events,
                         seasons=4, e3_seasons=4, force_capture=True)[1]
    assert captured_c == [25, 25, 26, 27], captured_c

    counted = run_civ(layout, CaptureAt(43), rule=quiet_rule, ev=quiet_events,
                      seasons=54, e3_seasons=52, force_capture=True)[0]
    # Capture at season 43 gets exactly eight post-capture updates (44–51). At season 44 it gets only seven;
    # counting the capture season itself would incorrectly turn that into eight and secede before KEY-4.
    capture_season = run_civ(layout, CaptureAt(44), rule=quiet_rule, ev=quiet_events,
                             seasons=54, e3_seasons=52, force_capture=True)[0]
    outside = run_civ(layout, CaptureAt(45), rule=quiet_rule, ev=quiet_events,
                      seasons=54, e3_seasons=52, force_capture=True)[0]
    assert counted == 1 and capture_season == 0 and outside == 0, (counted, capture_season, outside)


def report(out=print, check=False):
    lays = layouts()
    d_end = [d for l_ in lays for d in l_["dist"][1:]]
    out(f"== T25 Cohesion in E3 ({len(lays)} civ layouts from territory_sim e3; distance to the capital: median "
        f"{st.median(d_end)}, p90 {sorted(d_end)[9 * len(d_end) // 10]} walking tiles)")

    def line(name, r):
        out(f"  {name:52s} C mean {r['c_mean']:5.1f} (p10 {r['c10']}, p90 {r['c90']}; {100 * r['c_low']:4.1f}% below 35)  "
            f"secessions/civ/E3 {r['sec_per_civ']:.3f}  (> 2 in 2 years: {100 * r['burst']:.2f}%)")
    base = mc(lays)
    line("22 §8.3 as written (rates 07d §9)", base)
    line("no administration projects (span 4, no Overreach cuts)", mc(lays, admin=False))
    line("every civ captures one distant settlement", mc(lays, ev=dict(capture_per_civ=1.0)))
    line("stress: famine ×3, raids ×3, epidemics ×2", mc(lays, ev=dict(p_short_if_harsh=0.45, raid_per_year=0.09, epi_per_year=0.098)))
    out("  -- variants (07d R7):")
    v1 = mc(lays, rule=dict(conquered=35, conquered_recovery=1))
    line("conquered −35 recovering +1 a season", v1)
    v3 = mc(lays, rule=dict(base=50))
    line("base 50 instead of 60", v3)
    r7 = dict(conquered=35, conquered_recovery=1, culture_years=1, capture_c=25)
    line("R7: captured at C 25, −35 (+1/season), no culture terms", mc(lays, rule=r7))
    line("  R7, every civ captures one", mc(lays, rule=r7, ev=dict(capture_per_civ=1.0)))
    line("  R7, captures 0.15 per civ, stress famine ×3 raids ×3", mc(lays, rule=r7, ev=dict(capture_per_civ=0.15, p_short_if_harsh=0.45, raid_per_year=0.09)))
    out("  -- sensitivity to a 20% error (as written):")
    for name, kw in (("famine weight 24", dict(rule=dict(famine=24))), ("Overreach divisor 1.6", dict(rule=dict(overreach_div=1.6))),
                     ("span penalty 3.6", dict(rule=dict(span_pen=3.6))), ("raid rate ×1.2", dict(ev=dict(raid_per_year=0.036))),
                     ("base 48 (−20% of 60)", dict(rule=dict(base=48)))):
        line(name, mc(lays, runs=30, **kw))
    out("  targets: typical 60–80; 0.03–0.08 secessions per civ per E3; > 2 in one civ within 2 years in < 2% of matches")
    if check:
        check_model_semantics()
        assert 60 <= base["c_mean"] <= 80, base
        assert base["burst"] < 0.02, base
        print("cohesion_sim check passed")
    return base


if __name__ == "__main__":
    report(check="--check" in sys.argv)
