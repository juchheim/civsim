"""Sensitivity sweeps for the findings doc.

  python sensitivity.py exponent   # refit per Commons exponent; robustness of each
  python sensitivity.py final      # tuned scenario: archetypes, paces, fertility ±15%
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import balance_sim as B  # noqa: E402
import params as P  # noqa: E402
import fit as F  # noqa: E402


def total(s):
    rows, D = B.run(s)
    res, ff, tot = B.summarize(rows, D, s)
    return tot, res, ff


def refit(exp):
    base = P.scenario("tuned")
    base["CommonsExp"] = exp
    mid = [(a + b) / 2 for a, b in zip(base["era"]["TargetMinLo"], base["era"]["TargetMinHi"])]

    def with_k(k):
        t = P.scenario("tuned")
        t["CommonsExp"] = exp
        t["CommonsCoef"], t["RoleScale"] = 0.6 * k, k
        return t
    k = F.bisect(lambda k: F.era_minutes(with_k(k), 0), 0.5, 200, mid[0], increasing=False)
    s = with_k(k)
    for e in range(1, 10):
        def f(b, e=e):
            s["era"]["EraBase"][e] = b
            return F.era_minutes(s, e)
        s["era"]["EraBase"][e] = F.bisect(f, s["era"]["EraBase"][e - 1], s["era"]["EraBase"][e - 1] * 30, mid[e])
    return s


def perturb(s, **kw):
    t = {k: (dict(v) if isinstance(v, dict) else (list(v) if isinstance(v, list) else v)) for k, v in s.items()}
    t["era"] = {k: list(v) for k, v in s["era"].items()}
    for k, v in kw.items():
        if k == "arch":
            t["arch"] = dict(zip(P.ARCH_FIELDS, P.ARCHETYPES[v]))
        elif k == "pace":
            t["pace"], t["SeasonSeconds"] = P.PACES[v][s["name"]]
        else:
            t[k] = v
    return t


def exponent_sweep():
    print("exp    k    ratios E1..E9                                   max/min  fert-15%  fert+15%  Grower  Sage  Wanderer  Quick")
    for exp in (0.85, 0.75, 0.70, 0.65):
        s = refit(exp)
        eb = s["era"]["EraBase"]
        ratios = [eb[i] / eb[i - 1] for i in range(1, 10)]
        lo = total(perturb(s, BaseFertility=s["BaseFertility"] * 0.85))[0]
        hi = total(perturb(s, BaseFertility=s["BaseFertility"] * 1.15))[0]
        g = total(perturb(s, arch="Grower"))[0]
        sa = total(perturb(s, arch="Sage"))[0]
        w = total(perturb(s, arch="Wanderer"))[0]
        q = total(perturb(s, pace="Quick"))[0]
        print(f"{exp:.2f} {s['CommonsCoef'] / 0.6:5.2f}  " + " ".join(f"{r:4.2f}" for r in ratios)
              + f"   {max(ratios) / min(ratios):5.2f}   {lo}  {hi}  {g}  {sa}  {w}  {q}   E9 base {eb[9]:,.0f}")


def final():
    s = P.scenario("tuned")
    for name in ("v01", "tuned"):
        for pace in P.PACES:
            for arch in P.ARCHETYPES:
                t = P.scenario(name, pace, arch)
                tot, res, ff = total(t)
                eras = " ".join(f"{r['minutes']:5.1f}" if r else "  -  " for r in res)
                print(f"{name:5s} {pace:8s} {arch:8s} total {str(tot):>7s}  fission {ff}  | {eras}")
    for f in (0.85, 1.15):
        t = P.scenario("tuned")
        t["BaseFertility"] *= f
        print(f"tuned fertility ×{f}: total {total(t)[0]}")


if __name__ == "__main__":
    {"exponent": exponent_sweep, "final": final}[sys.argv[1]]()
