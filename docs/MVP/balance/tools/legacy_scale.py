"""Legacy split over the Bronze chapter (docs/MVP/22 §3.1, §3.3; §15 T24; findings in 07d §8).

Per civilization at the chapter end (first KEY-4 in the world + 12 seasons, or Year 64):
  Dominion  100 × share of claimed land per year (07b R8), from territory_sim's E3 runs on the live trajectories
  Mastery   2 × L(e) per completed project, L(e) = e + 1 (keystones count in the era they close)
  First     3 × L(e) per project first in the world; 12 × L(e) for keystones and Wonders
  Wonder    25 × L(e) per built Wonder (Megalith, Ziggurat)
Completions follow the live keystone times (tools/live_data.py) and the live-mode E3 length; within an era each
civilization's projects complete in a random order (seeded), so Firsts go to whoever is ahead in that lineage.
Old rule for comparison: First = EraBase × 0.5 (×3 keystones and Wonders), Mastery = e + 1.

    uv run --no-project --with numpy python legacy_scale.py           # T24 table
    uv run --no-project --with numpy python legacy_scale.py --check   # assertions
"""
import random
import statistics as st
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import live_data as L  # noqa: E402

COUNTS = {0: 34, 1: 59, 2: 52, 3: 65}       # projects per era (03a–c; E3 = 22 §12)
WONDERS = {2: "STN-10", 3: "BLD-08"}
E3_DONE = 24.0          # in-era E3 completions at KEY-4 (balance_sim live: N 21 + 1.6 from the keystone phase)
E3_AFTER_PER_MIN = 0.8  # E3 leftovers per minute in the Iron threshold (EraLag 0.7 picks; 30 s a pick at half speed)
WONDER_BUILT = {2: 0.5, 3: 0.25}   # share of civs that build the Megalith / the Ziggurat by the chapter end (ASSUMED)
ERABASE = [22, 50, 75, 140]


def units(new=True):
    def mastery(e):
        return 2 * (e + 1) if new else (e + 1)

    def first(e, big=False):
        if new:
            return (12 if big else 3) * (e + 1)
        return ERABASE[e] * 0.5 * (3 if big else 1)

    def wonder(e):
        return 25 * (e + 1) if new else 0.0
    return mastery, first, wonder


def chapter(seed, e3_minutes=13.0, dominion=None, new=True, rng_seed=1):
    """One live seed: per-civ Legacy components at the chapter end."""
    rows = [r for r in L.load() if r["seed"] == seed]
    last = max(r["minute"] for r in rows)
    fin = {r["civ"]: r for r in rows if r["minute"] == last}
    ks = {c: [fin[c]["k1"], fin[c]["k2"], fin[c]["k3"]] for c in fin}
    med2 = st.median(ks[c][2] - ks[c][1] for c in ks)
    k4 = {c: ks[c][2] + e3_minutes * (ks[c][2] - ks[c][1]) / med2 for c in ks}   # a fast E2 civ is fast in E3
    end = min(min(k4.values()) + 3.0, 64.0)
    mastery, first, wonder = units(new)
    rng = random.Random(rng_seed * 1000 + seed)
    starts = {c: [0.0, ks[c][0], ks[c][1], ks[c][2]] for c in ks}
    ends = {c: [ks[c][0], ks[c][1], ks[c][2], k4[c]] for c in ks}
    done_time = {c: {} for c in ks}
    for e in range(4):
        n = COUNTS[e]
        for c in ks:
            # E0–E2: every project done by KEY-3 + 15 min (live: all 148 by minute ~50); E3: E3_DONE by KEY-4, more after
            if e < 3:
                span = (starts[c][e], ends[c][e] + (8.0 if e < 2 else 15.0))
                times = sorted(rng.uniform(*span) for _ in range(n))
                order = list(range(n))
                rng.shuffle(order)
                for p, tm in zip(order, times):
                    done_time[c][(e, p)] = tm
            else:
                extra = max(0.0, end - k4[c]) * E3_AFTER_PER_MIN
                k = int(round(E3_DONE + extra))
                pool = list(range(n))
                # 18 closure projects first, then the civ's own free picks
                closure = list(range(18))
                free = pool[18:]
                rng.shuffle(free)
                chosen = closure + free[:max(0, k - 18)]
                times = sorted(rng.uniform(starts[c][3], max(starts[c][3] + 0.1, min(end, k4[c] + extra / E3_AFTER_PER_MIN if extra else k4[c])))
                               for _ in chosen)
                rng.shuffle(chosen)
                for p, tm in zip(chosen, times):
                    if tm <= end:
                        done_time[c][(e, p)] = tm
    comp = {c: dict(mastery=0.0, first=0.0, wonder=0.0, dominion=0.0) for c in ks}
    for c in ks:
        for (e, p), tm in done_time[c].items():
            if tm <= end:
                comp[c]["mastery"] += mastery(e)
        for key in range(1, 5):
            if (key < 4 and ks[c][key - 1] <= end) or (key == 4 and k4[c] <= end):
                comp[c]["mastery"] += mastery(key - 1)
    # Firsts: the earliest completer of each project
    allp = {k for c in ks for k in done_time[c]}
    for k in allp:
        tms = [(done_time[c][k], c) for c in ks if k in done_time[c] and done_time[c][k] <= end]
        if tms:
            e = k[0]
            comp[min(tms)[1]]["first"] += first(e, big=(k[1] == 0 and e in WONDERS))
    for key in range(1, 5):
        tms = [(ks[c][key - 1] if key < 4 else k4[c], c) for c in ks]
        tms = [x for x in tms if x[0] is not None and x[0] <= end]
        if tms:
            comp[min(tms)[1]]["first"] += first(key - 1, big=True)
    # Wonders built: the Megalith and the Ziggurat, by expected share (spread evenly as an expectation)
    for c in ks:
        for e, sh in WONDER_BUILT.items():
            comp[c]["wonder"] += sh * wonder(e)
    if dominion is not None:
        for c in ks:
            comp[c]["dominion"] = dominion.get(c, 0.0)
    return comp, end, k4


def dominion_from_territory(seed, map_seed, end):
    """Σ 100 × (own claimed ÷ claimed by anyone) / 4 per season to the chapter end (07b R8 scoring)."""
    import territory_sim as T
    res = T.run_e3(seed, map_seed, "village", True, minutes=int(end) + 1)
    out = {c: 0.0 for c in range(4)}
    for r in res["rows"]:
        if r["minute"] > end:
            break
        if r["claimed"] <= 0:
            continue
        for c in range(4):
            out[c] += 100 * r["share"][c] / r["claimed"] / 4
    return out


def report(out=print, check=False, territory=True):
    seeds = sorted({r["seed"] for r in L.load()})
    maps = [1, 3, 4, 6]
    shares, shares_old, ends, totals = [], [], [], []
    for i, sd in enumerate(seeds):
        comp, end, _ = chapter(sd)
        dom = dominion_from_territory(sd, maps[i % 4], end) if territory else {c: 25.0 * end for c in comp}
        comp, end, _ = chapter(sd, dominion=dom)
        old, _, _ = chapter(sd, dominion=dom, new=False)
        ends.append(end)
        for c in comp:
            tot = sum(comp[c].values())
            totals.append((comp[c]["dominion"], comp[c]["mastery"], comp[c]["first"], comp[c]["wonder"], tot))
            shares.append({k: v / tot for k, v in comp[c].items()})
            to = sum(old[c].values())
            shares_old.append({k: v / to for k, v in old[c].items()})
    out(f"== T24 Legacy split at the chapter end ({len(shares)} civs, {len(seeds)} live seeds; chapter end median "
        f"{st.median(ends):.1f} min, range {min(ends):.1f}–{max(ends):.1f})")
    for k, tgt in (("dominion", "45–55%"), ("mastery", "25–35%"), ("first", "12–20%"), ("wonder", "0–8%")):
        v = [s[k] for s in shares]
        vo = [s[k] for s in shares_old]
        out(f"  {k:9s} mean {100 * st.mean(v):5.1f}%  (p10 {100 * L.q(v, .1):4.1f}, p90 {100 * L.q(v, .9):4.1f})   target {tgt:7s}"
            f"   old rule {100 * st.mean(vo):5.1f}%")
    out("  per civ (mean): Dominion {:.0f}, Mastery {:.0f}, Firsts {:.0f}, Wonders {:.0f}, total {:.0f}".format(
        *[st.mean(t[i] for t in totals) for i in range(5)]))
    fs = sorted((t[2] for t in totals), reverse=True)
    out(f"  Firsts spread: most {fs[0]:.0f}, median {st.median(fs):.0f}, least {fs[-1]:.0f}")
    # sensitivity to a 20% error
    out("  sensitivity (mean shares Dominion / Mastery / Firsts / Wonders):")
    global E3_DONE, WONDER_BUILT
    base_done, base_w = E3_DONE, dict(WONDER_BUILT)
    for name, fn in (("E3 completions ×1.2", lambda: globals().__setitem__("E3_DONE", base_done * 1.2)),
                     ("Wonders built ×2", lambda: globals().__setitem__("WONDER_BUILT", {2: 1.0, 3: 0.5})),
                     ("Dominion ×0.8", None), ("E3 length ×1.2 (15.6 min)", None)):
        sh = []
        for i, sd in enumerate(seeds):
            if fn:
                fn()
            e3m = 15.6 if "length" in name else 13.0
            comp, end, _ = chapter(sd, e3_minutes=e3m)
            scale = 0.8 if "Dominion" in name else 1.0
            dom = {c: 25.0 * end * scale * 0.95 for c in comp}   # territory-free approximation for the sweep
            comp, end, _ = chapter(sd, e3_minutes=e3m, dominion=dom)
            for c in comp:
                tot = sum(comp[c].values())
                sh.append([comp[c][k] / tot for k in ("dominion", "mastery", "first", "wonder")])
            E3_DONE, WONDER_BUILT = base_done, dict(base_w)
            globals()["E3_DONE"], globals()["WONDER_BUILT"] = base_done, dict(base_w)
        out(f"    {name:26s} " + " / ".join(f"{100 * st.mean(x[i] for x in sh):4.1f}%" for i in range(4)))
    if check:
        m = {k: st.mean(s[k] for s in shares) for k in shares[0]}
        assert 0.45 <= m["dominion"] <= 0.55 and 0.25 <= m["mastery"] <= 0.35, m
        assert 0.12 <= m["first"] <= 0.20 and m["wonder"] <= 0.08, m
        print("legacy_scale check passed")
    return shares


if __name__ == "__main__":
    report(check="--check" in sys.argv, territory="--fast" not in sys.argv)
