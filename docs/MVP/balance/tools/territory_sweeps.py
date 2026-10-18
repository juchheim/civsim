"""Scenario sweeps for the four-civilization territory model (07b).

Runs every scenario quoted in 07b-territory-findings.md and returns plain tables
(lists of dicts). build_territory_xlsx.py writes them to territory-model.xlsx.

    python territory_sweeps.py [--cache out.json]   # run all, print tables
    python territory_sweeps.py contact    # one group only

Takes ~10 min. Each run is deterministic (fixed map seeds, no RNG in the sim).
"""
import json
import statistics as st
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import territory_sim as T  # noqa: E402

SEEDS = [1, 3, 4, 6]          # symmetric maps whose paired starts have even room (±5%)
BASE = dict(spacing=25)      # recommended start rule (07b R1)
REC_T = dict(T=0.7)          # recommended ownership threshold (07b R3)
REC_RADIUS = [3, 3, 4, 4, 5, 5, 5, 7, 9, 9]
COUPLED = dict(Territory=[60, 200, 500, 900, 1400, 1600, 1600, 1600, 1600, 1600],
               FoodCap=[22, 30, 60, 120, 250, 500, 1000, 4000, 9600, 19200],
               HousingCap=[60, 60, 100, 200, 400, 800, 1600, 6400, 16000, 32000])
MAPS = {2: (112, 72), 3: (140, 86), 4: (160, 100), 6: (196, 122), 8: (226, 142)}  # ~4,000 tiles per civ


def era_at(res, minute, civ=0):
    return T.contact_era(res, minute, civ) if minute is not None else None


def pair_contacts(res):
    return sorted(v["contact"] for v in res["contact"].values() if "contact" in v)


def first_border(res):
    b = [v["border"] for v in res["contact"].values() if "border" in v]
    return min(b) if b else None


def at_minute(res, minute):
    for r in res["rows"]:
        if r["minute"] >= minute:
            return r
    return res["rows"][-1]


def fill_minute(res, level=0.9):
    return next((r["minute"] for r in res["rows"] if r["claimed"] >= level), None)


def med(xs):
    xs = [x for x in xs if x is not None]
    return st.median(xs) if xs else None


def rng(xs):
    xs = [x for x in xs if x is not None]
    return (min(xs), max(xs)) if xs else (None, None)


# ---- groups -------------------------------------------------------------------------
def base_runs():
    """Standard map, 4× Balanced: v0.1 (max-spread starts) and recommended spacing."""
    out = {}
    snaps = (117, 230, 372, 461, 686)  # end of E2, E4, E6 (~min 93), E7, E9
    for key, kw in [("maxspread", dict(spacing=None)), ("paired", BASE)]:
        r = T.scenario(key, snaps=snaps, **kw)
        out[key] = r
    return out


def contact_sweep():
    rows = []
    for D in [20, 22, 25, 28, 30, 35, 40, 50, None]:
        firsts, nears, borders = [], [], []
        for seed in SEEDS:
            r = T.scenario("c", spacing=D, seed=seed)
            fc = T.first_contact(r)
            firsts.append(fc)
            pc = pair_contacts(r)
            nears.append(pc[1] if len(pc) > 1 else None)  # second contact: both pairs have met
            borders.append(first_border(r))
        rows.append(dict(spacing="max" if D is None else D, first_contact_med=med(firsts),
                         first_contact_min=rng(firsts)[0], first_contact_max=rng(firsts)[1],
                         first_contact_era=era_at(r, med(firsts)), both_pairs_med=med(nears),
                         first_border_med=med(borders)))
    for mult in [0.75, 1.25]:
        firsts, nears = [], []
        for seed in SEEDS:
            t = T.tp(Reach=[round(x * mult) for x in T.TERA["Reach"]])
            r = T.scenario("c", seed=seed, t=t, **BASE)
            nears.append(None)
            firsts.append(T.first_contact(r))
        rows.append(dict(spacing=f"25, reach ×{mult}", first_contact_med=med(firsts),
                         first_contact_min=rng(firsts)[0], first_contact_max=rng(firsts)[1],
                         first_contact_era=era_at(r, med(firsts))))
    return rows


def early_sweep():
    """How full the map is at minute 30 (end of E2) under early-radius and presence variants."""
    variants = [("v0.1 radius 2/3/3", {}),
                ("radius 3/3/4", dict(Radius=REC_RADIUS)),
                ("radius 3/4/4", dict(Radius=[3, 4, 4] + REC_RADIUS[3:])),
                ("radius 3/3/4 + presence 0.6", dict(Radius=REC_RADIUS, Presence=0.6)),
                ("T 0.7 + radius 3/3/4", dict(Radius=REC_RADIUS, T=0.7)),
                ("T 0.5 + radius 3/3/4", dict(Radius=REC_RADIUS, T=0.5))]
    rows = []
    for name, kw in variants:
        acc = dict(own30=[], claimed30=[], share30=[], fc=[], border=[], own75=[], own17=[], share_e4=[], own2=[])
        for seed in SEEDS:
            r = T.scenario("e", seed=seed, t=T.tp(**kw), **BASE)
            a = at_minute(r, 29.25)
            acc["own30"].append(st.mean(a["owned"]))
            acc["claimed30"].append(a["claimed"])
            acc["share30"].append(st.mean(a["share"]))
            acc["own75"].append(st.mean(at_minute(r, 7.5)["owned"]))
            acc["own2"].append(st.mean(at_minute(r, 2.0)["owned"]))
            acc["own17"].append(st.mean(at_minute(r, 17.5)["owned"]))
            acc["share_e4"].append(st.mean(at_minute(r, 57.0)["share"]))
            acc["fc"].append(T.first_contact(r))
            acc["border"].append(first_border(r))
        rows.append(dict(variant=name, owned_min2=med(acc["own2"]), owned_min7_5=med(acc["own75"]), owned_min17_5=med(acc["own17"]),
                         owned_min30=med(acc["own30"]), share_min30=med(acc["share30"]),
                         claimed_min30=med(acc["claimed30"]), share_min57=med(acc["share_e4"]),
                         first_contact=med(acc["fc"]), first_border=med(acc["border"])))
    return rows


def ownership_sweep():
    variants = [("as written: T 1.0, lead 20%, tail 1", {}),
                ("T 0.5", dict(T=0.5)), ("T 2.0", dict(T=2.0)), ("T 4.0", dict(T=4.0)),
                ("lead 10%", dict(Lead=0.10)), ("lead 35%", dict(Lead=0.35)),
                ("tail 0.5", dict(FalloffTail=0.5)), ("tail 2", dict(FalloffTail=2.0)),
                ("incumbent holds", dict(IncumbentHolds=1)),
                ("no flip delay", dict(FlipSeasons=0))]
    rows = []
    for name, kw in variants:
        t = T.tp(**kw)
        r = T.scenario("o", t=t, **BASE)
        e0 = at_minute(r, 2.0)
        e2 = at_minute(r, 29.25)
        e7 = at_minute(r, 115.0)
        fin = r["rows"][-1]
        rows.append(dict(variant=name, owned_min2=st.mean(e0["owned"]), owned_min30=st.mean(e2["owned"]),
                         contested_e7=e7["contested"], contested_end=fin["contested"],
                         civ_flips=sum(x["flips"] for x in r["rows"]), first_border=first_border(r),
                         share_spread_end=max(fin["share"]) - min(fin["share"]),
                         claimed_end=fin["claimed"]))
    return rows


def dominion_sweep():
    rows = []
    mixes = [["Balanced"] * 4, ["Wanderer", "Balanced", "Balanced", "Balanced"],
             ["Grower", "Sage", "Sage", "Sage"], ["Wanderer", "Sage", "Grower", "Builder"],
             ["Warlord", "Builder", "Builder", "Builder"]]
    for archs in mixes:
        mx, w40 = [], []
        for seed in SEEDS[:2]:
            r = T.scenario("d", archs=archs, seed=seed, **BASE)
            mx.append(max(max(x["share"]) for x in r["rows"]))
            w40.append(T.crossing(r, 0.40)[0])
        rows.append(dict(scenario="peaceful: " + "/".join(archs), max_share=max(mx), cross40=med(w40),
                         cross50=None, win=None))
    # conquest stand-in: civ 0 takes its paired neighbour (civ 1) whole, then k of civ 3
    for when, label in [(57.25, "E5 start"), (74.0, "E6 start"), (93.25, "E7 start")]:
        r = T.scenario("d", captures=[(when, 0, 1, 99)], **BASE)
        rows.append(dict(scenario=f"conquest: whole neighbour at {label}",
                         max_share=max(x["share"][0] for x in r["rows"]), cross40=T.crossing(r, 0.40)[0],
                         cross50=T.crossing(r, 0.50)[0], win=T.dominion_win(r)[0]))
    for k in [2, 4, 8]:
        r = T.scenario("d", captures=[(74.0, 0, 1, 99), (93.25, 0, 3, k)], **BASE)
        rows.append(dict(scenario=f"conquest: neighbour at E6 + {k} settlements of a 3rd civ at E7",
                         max_share=max(x["share"][0] for x in r["rows"]), cross40=T.crossing(r, 0.40)[0],
                         cross50=T.crossing(r, 0.50)[0], win=T.dominion_win(r)[0],
                         win_2x=dominion_win_2x(r)[0]))
    return rows


def dominion_win_2x(res, level=0.5, years=5, ratio=2.0):
    """Proposed rule: >= level AND >= ratio × the runner-up, for `years` consecutive years."""
    need = years * 4
    nc = len(res["rows"][0]["share"])
    run_ = [0] * nc
    for r in res["rows"]:
        for c in range(nc):
            others = max(x for i, x in enumerate(r["share"]) if i != c)
            ok = r["share"][c] >= level and r["share"][c] >= ratio * others
            run_[c] = run_[c] + 1 if ok else 0
            if run_[c] >= need:
                return r["minute"], c
    return None, None


def mapsize_sweep():
    rows = []
    for n, (W, H) in MAPS.items():
        done = 0
        for seed in SEEDS + [2, 5, 7, 8]:
            if done == 2:
                break
            sym = n % 2 == 0
            layout = "pangaea" if n == 2 else "continents"
            m = T.make_map(W, H, 0.40, seed, layout, sym)
            if T.place_starts(m, n, BASE["spacing"], 0 if sym else 16) is None:
                continue
            done += 1
            r = T.scenario(f"n{n}", W=W, H=H, n=n, seed=seed, sym=sym, fair=0 if sym else 16,
                           layout=layout, **BASE)
            m = r["map"]
            fin = r["rows"][-1]
            pc = pair_contacts(r)
            rows.append(dict(players=n, map=f"{W}×{H}", tiles=W * H, seed=seed, land=int(m.land.sum()),
                             hab_per_civ=m.total_hab / n, first_contact=pc[0] if pc else None,
                             all_met=pc[-1] if len(pc) == n * (n - 1) // 2 else None,
                             fill90=fill_minute(r), max_share=max(fin["share"]), min_share=min(fin["share"]),
                             win_as_written=T.dominion_win(r)[0], win_2x=dominion_win_2x(r)[0],
                             blocked=sum(c.blocked for c in r["civs"])))
    return rows


def legacy_by_era(res):
    tot, by_era = T.legacy_dominion(res)
    n = len(tot)
    return [dict(era=f"E{e}", share_of_dominion_legacy=sum(by_era[c][e] for c in range(n)) / sum(tot))
            for e in range(10)]


def coupling():
    """balance_sim with Territory = the map's room, with and without denser late caps."""
    B, P = T.B, T.P
    rows = []
    # The live params already carry R6 (07c §2), so compare the live run with the v0.1 territory
    # assumption instead of re-applying COUPLED (which would double the late caps).
    for name, ov in [("live: Territory 1,600 + R6 density steps (07c)", {}),
                     ("same, Territory as v0.1 (2,200–2,500 tiles)", dict(Territory=P.ERA["Territory"]["v01"]))]:
        s = P.scenario("tuned", "Standard", "Balanced", ov)
        rws, D = B.run(s)
        res, ff, tot = B.summarize(rws, D, s)
        rows.append(dict(scenario=name, arc=tot, E8=res[8]["minutes"], E9=res[9]["minutes"],
                         pop_E9=res[9]["pop_end"], S_E7=res[7]["S_end"], S_E9=res[9]["S_end"]))
    return rows


def recommended():
    """Recommended constants together (incl. seasonal camps, 07b §9.4), live trajectory, 4 seeds."""
    t = T.tp(Radius=REC_RADIUS, Camps=1, **REC_T)
    out = []
    # The live params now carry R6 (Territory 1,600; late caps as per-project steps), so COUPLED's
    # per-era cap tables would double-apply. Pass COUPLED only with CIVSIM_BALANCE_DIR set to the
    # frozen WS-A part-1 copy that 07b §6 used.
    ov = COUPLED if __import__("os").environ.get("CIVSIM_BALANCE_DIR") else None
    for seed in SEEDS:
        r = T.scenario("rec", seed=seed, t=t, overrides=ov, snaps=(117, 372, 686), **BASE)
        out.append(r)
    return out


def table(rows, out=print):
    if isinstance(rows, dict):  # grouped results (live groups)
        for k, v in rows.items():
            out(f"# {k}")
            table(v, out)
        return
    if not rows:
        return
    keys = list(rows[0].keys())
    for k in rows[1:]:
        for kk in k:
            if kk not in keys:
                keys.append(kk)
    out(" | ".join(keys))
    for r in rows:
        out(" | ".join(_fmt(r.get(k)) for k in keys))


def _fmt(v):
    if v is None:
        return "-"
    if isinstance(v, float):
        return f"{v:.3f}" if abs(v) < 1 else f"{v:.1f}"
    return str(v)


def legacy_claimed(res):
    """07b R8 scoring: per year, 100 × your share of *claimed* habitable land (Free Folk excluded here)."""
    nc = len(res["rows"][0]["share"])
    tot = [0.0] * nc
    by_era = [[0.0] * 10 for _ in range(nc)]
    for r in res["rows"]:
        claimed = sum(r["share"])
        for c in range(nc):
            v = 100 * (r["share"][c] / claimed if claimed > 0 else 0) / 4
            tot[c] += v
            by_era[c][r["eras"][c]] += v
    return tot, by_era


def live(camps=0):
    """07c follow-up: 07b's recommended rules on the LIVE trajectory (params already carry R6 as
    per-project caps and Territory 1,600, so no COUPLED override; COUPLED would double-apply)."""
    t = T.tp(Radius=REC_RADIUS, Camps=camps, **REC_T)
    rows, recs = [], []
    for seed in SEEDS:
        r = T.scenario("live", seed=seed, t=t, snaps=(117, 372, 686), **BASE)
        recs.append(r)
        pc = pair_contacts(r)
        fin = r["rows"][-1]
        tot_h, by_h = T.legacy_dominion(r)
        tot_c, by_c = legacy_claimed(r)
        n = len(tot_h)
        early = lambda by, tot: sum(by[c][e] for c in range(n) for e in range(4)) / sum(tot)
        late = lambda by, tot: sum(by[c][e] for c in range(n) for e in range(7, 10)) / sum(tot)
        rows.append(dict(seed=seed, first_contact=pc[0] if pc else None,
                         contact_era=era_at(r, pc[0]) if pc else None,
                         all_pairs_met=pc[-1] if len(pc) == 6 else None, first_border=first_border(r),
                         fill90=fill_minute(r), max_share=max(max(x["share"]) for x in r["rows"]),
                         end_min_share=min(fin["share"]), end_max_share=max(fin["share"]),
                         end_claimed=fin["claimed"], blocked=sum(c.blocked for c in r["civs"]),
                         max_short_seasons=max(sum(1 for x in r["rows"] if x["S_target"][c] > x["S"][c]) for c in range(len(r["civs"]))),
                         legacy_E0_3_hab=early(by_h, tot_h), legacy_E7_9_hab=late(by_h, tot_h),
                         legacy_E0_3_claimed=early(by_c, tot_c), legacy_E7_9_claimed=late(by_c, tot_c)))
    # era-end table for seed 1 (civ 0) plus share range over seeds
    ends = []
    for e in range(10):
        per = [T.era_end_rows(r).get(e) for r in recs]
        per = [p for p in per if p]
        if not per:
            continue
        p0 = per[0]
        shares = [s for p in per for s in p["share"]]
        ends.append(dict(era=f"E{e}", end_min=p0["minute"], pop_per_civ=p0["pops"][0], settl=p0["S"][0],
                         owned_per_civ=p0["owned"][0], camps=p0.get("camps", [0])[0], share_lo=min(shares), share_hi=max(shares),
                         claimed_lo=min(p["claimed"] for p in per), claimed_hi=max(p["claimed"] for p in per)))
    # peaceful archetype mixes and conquest stand-ins, recommended rules, live trajectory
    dom = []
    for archs in [["Balanced"] * 4, ["Wanderer", "Balanced", "Balanced", "Balanced"],
                  ["Grower", "Sage", "Sage", "Sage"], ["Wanderer", "Sage", "Grower", "Builder"],
                  ["Warlord", "Builder", "Builder", "Builder"]]:
        mx = [max(max(x["share"]) for x in T.scenario("d", archs=archs, seed=s, t=t, **BASE)["rows"])
              for s in SEEDS[:2]]
        dom.append(dict(scenario="peaceful: " + "/".join(archs), max_share=max(mx)))
    for caps, label in [([(57.25, 0, 1, 99)], "whole neighbour at E5 start"),
                        ([(74.0, 0, 1, 99)], "whole neighbour at E6 start"),
                        ([(74.0, 0, 1, 99), (93.25, 0, 3, 2)], "neighbour at E6 + 2 settl. of 3rd civ at E7"),
                        ([(74.0, 0, 1, 99), (93.25, 0, 3, 4)], "neighbour at E6 + 4 settl. of 3rd civ at E7")]:
        r = T.scenario("d", captures=caps, t=t, **BASE)
        dom.append(dict(scenario="conquest: " + label, max_share=max(x["share"][0] for x in r["rows"]),
                        cross40=T.crossing(r, 0.40)[0], cross50=T.crossing(r, 0.50)[0],
                        win_2x=dominion_win_2x(r)[0]))
    return dict(seeds=rows, eras=ends, dominion=dom)


GROUPS = dict(contact=contact_sweep, early=early_sweep, ownership=ownership_sweep,
              dominion=dominion_sweep, mapsize=mapsize_sweep, coupling=coupling, live=live,
              live_camps=lambda: live(camps=1))


def run_all(groups=None, cache=None):
    res = {}
    for g in groups or GROUPS:
        t0 = time.time()
        res[g] = GROUPS[g]()
        print(f"\n## {g} ({time.time() - t0:.0f} s)")
        table(res[g])
    if cache:
        Path(cache).write_text(json.dumps(res, indent=1, default=float))
    return res


if __name__ == "__main__":
    args = sys.argv[1:]
    cache = None
    if "--cache" in args:
        i = args.index("--cache")
        cache = args[i + 1]
        args = args[:i] + args[i + 2:]
    run_all(args or None, cache=cache)
