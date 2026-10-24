"""Live-sim trajectories for the E3 oracle (07d §2).

The live measurements come from a scratch build of the harness with one extra command,
`probe` (source: tools/probe/ProbeCommand.cs.txt; add `"probe" => ProbeCommand.Run(args[1..])`
to Program.cs). It plays four Balanced civilizations on the Council and prints one line per
civilization per minute. Never build it in the main checkout: copy the repo to a scratch
directory first (07d §9).

    dotnet CivSim.Harness.dll probe --pack civsim.pack --seed 777 --minutes 64 > probe-777.txt
    uv run --no-project --with numpy python live_data.py ingest DIR   # probe-*.txt -> ../live-e3-baseline.csv
    uv run --no-project --with numpy python live_data.py              # summary table (07d §2)

The CSV in docs/MVP/balance/live-e3-baseline.csv is the committed measurement (commit 4cb188d,
content pack hash 62cf1b01…, seeds 20261019, 777, 1–6, 64 minutes). Rows: seed, minute, civ,
era, done, pop, children, adults, elders, settlements, settlement populations (sorted, ';'),
effective caps (';'), food caps (';'), Insight/yr, Commons/yr, role heads, progress spent,
Know-how applied, claimed % of habitable land, own share %, KEY-1/2/3 minutes.
"""
import csv
import statistics as st
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
CSV = HERE.parent / "live-e3-baseline.csv"
FIELDS = ["seed", "minute", "civ", "era", "done", "pop", "ch", "ad", "el", "S", "pops", "effcaps", "foodcaps",
          "ins", "com", "roles", "spent", "kh", "claimed", "share", "k1", "k2", "k3"]


def ingest(folder):
    rows = []
    for p in sorted(Path(folder).glob("probe-*.txt")):
        seed = int(p.stem.split("-")[1])
        for line in p.read_text().splitlines():
            if not line.startswith("min="):
                continue
            kv = dict(x.split("=", 1) for x in line.split())
            pops = sorted((int(v) for v in kv["pops"].split(",") if v), reverse=True)
            caps = [c.split("/") for c in kv["caps"].split(",") if c]
            rows.append(dict(seed=seed, minute=int(kv["min"]), civ=int(kv["civ"]), era=int(kv["era"]), done=int(kv["done"]),
                             pop=int(kv["pop"]), ch=int(kv["ch"]), ad=int(kv["ad"]), el=int(kv["el"]), S=int(kv["S"]),
                             pops=";".join(map(str, pops)), effcaps=";".join(c[0] for c in caps),
                             foodcaps=";".join(c[1] for c in caps), ins=kv["ins"], com=kv["com"], roles=kv["roles"],
                             spent=kv["spent"], kh=kv["kh"], claimed=kv["claimed"], share=kv["share"],
                             k1=kv["k1"], k2=kv["k2"], k3=kv["k3"]))
    with CSV.open("w", newline="") as f:
        w = csv.DictWriter(f, FIELDS)
        w.writeheader()
        w.writerows(rows)
    print(f"wrote {CSV} ({len(rows)} rows)")


def load():
    out = []
    with CSV.open() as f:
        for r in csv.DictReader(f):
            d = {k: r[k] for k in FIELDS}
            for k in ("seed", "minute", "civ", "era", "done", "pop", "ch", "ad", "el", "S"):
                d[k] = int(d[k])
            for k in ("ins", "com", "roles", "spent", "kh", "claimed", "share"):
                d[k] = float(d[k])
            for k in ("k1", "k2", "k3"):
                d[k] = float(d[k]) if d[k] != "-" else None
            d["pops"] = [int(x) for x in d["pops"].split(";") if x]
            d["effcaps"] = [float(x) for x in d["effcaps"].split(";") if x]
            d["foodcaps"] = [float(x) for x in d["foodcaps"].split(";") if x]
            out.append(d)
    return out


def by_minute(rows, minute):
    return [r for r in rows if r["minute"] == minute]


def keystones(rows):
    last = max(r["minute"] for r in rows)
    fin = by_minute(rows, last)
    return {k: sorted(r[k] for r in fin if r[k] is not None) for k in ("k1", "k2", "k3")}


def mean_curve(rows, key, minutes):
    return [st.mean(r[key] for r in by_minute(rows, m)) for m in minutes]


def q(v, p):
    v = sorted(v)
    i = (len(v) - 1) * p
    lo = int(i)
    return v[lo] + (v[min(lo + 1, len(v) - 1)] - v[lo]) * (i - lo)


def summary(rows=None, out=print):
    rows = rows or load()
    seeds = sorted({r["seed"] for r in rows})
    ks = keystones(rows)
    out(f"Live baseline: {len(seeds)} seeds × 4 Balanced civs on the Council (E3 projects inert: MaxEra 2)")
    for k, v in ks.items():
        out(f"  {k.upper()}: median {st.median(v):5.1f}  range {v[0]:.1f}-{v[-1]:.1f}  (n={len(v)})")
    out("  min   pop/civ (p10-p90)   adults%  settl/civ (p10-p90)  max settl  eff cap  food cap  Insight/yr  roles%  KH%   claimed%")
    for m in (20, 25, 30, 35, 40, 43, 45, 50, 55, 60, 64):
        rs = by_minute(rows, m)
        if not rs:
            continue
        pops = [r["pop"] for r in rs]
        S = [r["S"] for r in rs]
        ad = sum(r["ad"] for r in rs) / sum(r["pop"] for r in rs)
        mx = st.mean(max(r["pops"]) for r in rs)
        eff = st.mean(c for r in rs for c in r["effcaps"])
        food = st.mean(c for r in rs for c in r["foodcaps"])
        ins = st.mean(r["ins"] for r in rs)
        roles = st.mean((r["ins"] - r["com"]) / max(1e-9, r["ins"]) for r in rs)
        kh = st.mean(r["kh"] / max(1e-9, r["spent"]) for r in rs)
        cl = st.mean({(r["seed"]): r["claimed"] for r in rs}.values())
        out(f"  {m:3d}  {st.mean(pops):6.0f} ({q(pops, .1):4.0f}-{q(pops, .9):4.0f})     {100 * ad:4.1f}   "
            f"{st.mean(S):5.1f} ({q(S, .1):4.1f}-{q(S, .9):4.1f})      {mx:5.1f}    {eff:5.1f}   {food:6.1f}   {ins:8.0f}   "
            f"{100 * roles:5.1f}  {100 * kh:4.1f}   {cl:5.1f}")
    return ks


if __name__ == "__main__":
    if len(sys.argv) > 2 and sys.argv[1] == "ingest":
        ingest(sys.argv[2])
    else:
        summary()
