"""Fit the tuned constants against the 02 §8.3 pacing targets.

1. InsightScale k: Commons coefficient (and every role/structure output, so the
   ratios in 02 §8.1 survive) so that E0 lasts the target midpoint at EraBase 20.
2. EraBase[E1..E9], one era at a time, so each era lasts its target midpoint.
Prints raw fitted values; the recommended constants are these rounded to
"nice" numbers (params.py, scenario "tuned").

    python fit.py              # fit k, then EraBase E1..E9 (as WS-A did)
    python fit.py Standard base   # keep the tuned Insight scale; fit EraBase E0..E9
    python fit.py live            # current live-mode projection: EraBase[3] for a 20-min E3, both housing regimes
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import balance_sim as B  # noqa: E402
import params as P  # noqa: E402


def era_minutes(s, e):
    rows, D = B.run(s)
    res, _, _ = B.summarize(rows, D, s)
    r = res[e]
    if r is None or not r["finished"]:
        return 1e9
    return r["minutes"]


def bisect(f, lo, hi, target, iters=40, increasing=True):
    for _ in range(iters):
        mid = (lo * hi) ** 0.5
        v = f(mid)
        if (v < target) == increasing:
            lo = mid
        else:
            hi = mid
    return (lo * hi) ** 0.5


def fit_base(pace="Standard"):
    """Hold CommonsCoef and RoleScale at their tuned values; fit every EraBase (WS-H)."""
    s = P.scenario("tuned", pace)
    mid = [(a + b) / 2 for a, b in zip(s["era"]["TargetMinLo"], s["era"]["TargetMinHi"])]
    for e in range(10):
        def f(base, e=e):
            s["era"]["EraBase"][e] = base
            return era_minutes(s, e)
        lo = s["era"]["EraBase"][e - 1] if e else 2.0
        base = bisect(f, lo, lo * 20, mid[e])
        s["era"]["EraBase"][e] = base
        ratio = f"(×{base / s['era']['EraBase'][e - 1]:.2f} vs previous)" if e else ""
        print(f"EraBase E{e} = {base:12.1f}   {ratio}")
    return s["era"]["EraBase"]


def fit_live(target=20.0):
    """Fit EraBase[3] on the live-mode projection (InsightCalibration 1.6, E3 roles by structure, Council overhead)."""
    out = {}
    for regime in ("village", "towns"):
        raw = B.fit_live_erabase(dict(Regime=regime), target)
        out[regime] = raw
        r = B.live_summary(*B.run_live(dict(Regime=regime, EraBase=B.LIVE["EraBase"][:3] + [round(raw / 10) * 10]), minutes=75))
        print(f"EraBase E3 ({regime:7s} housing) = {raw:6.1f} -> {int(round(raw / 10) * 10)}: E3 {r['e3']:.2f} min, "
              f"KEY-3 {r['k3']:.2f}, KEY-4 {r['k4']:.2f}")
    return out


def fit(pace="Standard", mode="k"):
    if pace == "live":
        return fit_live()
    if mode == "base":
        return fit_base(pace)
    s = P.scenario("tuned", pace)
    mid = [(a + b) / 2 for a, b in zip(s["era"]["TargetMinLo"], s["era"]["TargetMinHi"])]

    def with_k(k):
        t = P.scenario("tuned", pace)
        t["CommonsCoef"] = 0.6 * k
        t["RoleScale"] = k
        return t

    k = bisect(lambda k: era_minutes(with_k(k), 0), 1, 100, mid[0], increasing=False)
    s = with_k(k)
    print(f"InsightScale k = {k:.3f}  -> CommonsCoef {0.6 * k:.3f}")
    for e in range(1, 10):
        def f(base, e=e):
            s["era"]["EraBase"][e] = base
            return era_minutes(s, e)
        base = bisect(f, s["era"]["EraBase"][e - 1], s["era"]["EraBase"][e - 1] * 20, mid[e])
        s["era"]["EraBase"][e] = base
        ratio = base / s["era"]["EraBase"][e - 1]
        print(f"EraBase E{e} = {base:12.1f}   (×{ratio:.2f} vs previous)")
    return k, s["era"]["EraBase"]


if __name__ == "__main__":
    fit(*(sys.argv[1:]))
