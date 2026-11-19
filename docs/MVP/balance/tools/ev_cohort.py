#!/usr/bin/env python3
"""Sharded, provenance-recording cohort runner and analyzers for the E5 F-T1/F-T2/F-T3 closeout.

`run` shards a seed manifest across N harness processes (fief-match is sequential per process),
records source/pack/harness/Sim-DLL/manifest hashes before and after, and writes one metadata JSON.
`ft1` / `ft2` analyze the raw shard outputs without imputing censored seats.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import re
import statistics
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
PROVENANCE = REPO / "docs/MVP/balance/m12-e5-final-source-provenance-2026-11-16.json"
HARNESS = REPO / "src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll"
SIM = REPO / "src/CivSim.Harness/bin/Release/net10.0/CivSim.Sim.dll"
DOTNET = "/opt/homebrew/bin/dotnet"


def sha(path: Path) -> str:
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def source_aggregate() -> str:
    """Re-hash every recorded file in the isolated copy and recompute the canonical aggregate."""
    d = json.loads(PROVENANCE.read_text())
    files = []
    for f in d["files"]:
        p = REPO / f["path"]
        files.append({"path": f["path"], "status": f["status"],
                      "sha256": sha(p) if p.is_file() else None})
    payload = {"head": d["head"], "files": files}
    return hashlib.sha256(json.dumps(payload, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def seeds_of(path: Path) -> list[int]:
    return [int(x) for x in (l.strip() for l in Path(path).read_text().splitlines())
            if x and not x.startswith("#") and x.lower() != "seed"]


def cmd_run(a: argparse.Namespace) -> int:
    out = Path(a.out)
    if out.exists():
        print(f"refusing to overwrite {out}", file=sys.stderr)
        return 2
    out.mkdir(parents=True)
    seeds = seeds_of(Path(a.seeds_file))
    before = source_aggregate()
    meta = {
        "name": a.name, "startedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "headCommit": json.loads(PROVENANCE.read_text())["head"],
        "sourceAggregateBefore": before,
        "packPath": str(Path(a.pack).resolve()), "packSha256": sha(Path(a.pack)),
        "harnessDll": str(HARNESS), "harnessDllSha256": sha(HARNESS),
        "simDll": str(SIM), "simDllSha256": sha(SIM),
        "manifestPath": str(Path(a.seeds_file).resolve()), "manifestSha256": sha(Path(a.seeds_file)),
        "seeds": seeds, "minutes": a.minutes, "shards": a.shards, "extraArgs": a.extra,
        "command": a.command,
    }
    procs = []
    for i in range(a.shards):
        shard_seeds = seeds[i::a.shards]
        if not shard_seeds:
            continue
        sf = out / f"shard-{i}.seeds"
        sf.write_text("\n".join(map(str, shard_seeds)) + "\n")
        raw = out / f"shard-{i}.txt"
        cmd = [DOTNET, str(HARNESS), a.command, "--pack", a.pack, "--minutes", str(a.minutes),
               "--seeds-file", str(sf), *a.extra]
        fh = open(raw, "w")
        procs.append((i, cmd, subprocess.Popen(cmd, stdout=fh, stderr=subprocess.STDOUT), raw, fh, sf))
    meta["shardCommands"] = {str(i): c for i, c, *_ in procs}
    exits = {}
    for i, _, p, raw, fh, sf in procs:
        exits[str(i)] = p.wait()
        fh.close()
    meta["shardExitCodes"] = exits
    meta["sourceAggregateAfter"] = source_aggregate()
    meta["rawSha256"] = {f"shard-{i}.txt": sha(raw) for i, _, _, raw, _, _ in procs}
    meta["shardSeedFileSha256"] = {f"shard-{i}.seeds": sha(sf) for i, _, _, _, _, sf in procs}
    meta["packShaAfter"] = sha(Path(a.pack))
    meta["harnessShaAfter"] = sha(HARNESS)
    meta["simShaAfter"] = sha(SIM)
    meta["completedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
    ok = all(v == 0 for v in exits.values()) and meta["sourceAggregateAfter"] == before \
        and meta["packShaAfter"] == meta["packSha256"] and meta["harnessShaAfter"] == meta["harnessDllSha256"]
    meta["status"] = "complete" if ok else "failed"
    (out / "metadata.json").write_text(json.dumps(meta, indent=2, sort_keys=True) + "\n")
    print(meta["status"], exits)
    return 0 if ok else 1


MATCH_RE = re.compile(r"^== fief-match seed (\d+): ([\d.]+) minutes simulated, chapter end (.*?) at season (.*?) \(ended: (True|False)\)")
ROW_RE = re.compile(r"^\s*c([0-3])\s+(\w+)\s+([\d.\-]+|-)\s+([\d.\-]+|-)\s")
PROG_RE = re.compile(r"^\s*c([0-3]) (\w+): (\d+) E5 projects, KEY-5→KEY-6 (.*)$")
DONE_RE = re.compile(r"([\d.]+) min from KEY-5 \(at ([\d.]+)\)")
CENS_RE = re.compile(r"not complete; observed ([\d.]+) min after KEY-5")


def parse_matches(raw_files: list[Path]) -> dict[int, dict]:
    matches: dict[int, dict] = {}
    for rf in raw_files:
        cur = None
        for line in rf.read_text().splitlines():
            m = MATCH_RE.match(line)
            if m:
                cur = {"seed": int(m[1]), "minutes": float(m[2]), "end": m[3], "seats": {}, "key5": {}}
                matches[cur["seed"]] = cur
                continue
            if cur is None:
                continue
            r = ROW_RE.match(line)
            if r and r[2] in ("Builder", "Grower", "Warlord", "Sage") and r[1] not in cur["key5"]:
                k5 = r[4]
                cur["key5"][r[1]] = None if k5 in ("-", "") else float(k5)
            p = PROG_RE.match(line)
            if p:
                civ, arch, n, detail = p[1], p[2], int(p[3]), p[4]
                d, c = DONE_RE.search(detail), CENS_RE.search(detail)
                if d:
                    cur["seats"][civ] = {"arch": arch, "status": "complete", "interval": float(d[1]), "key6_at": float(d[2]), "projects": n}
                elif c:
                    cur["seats"][civ] = {"arch": arch, "status": "censored", "observed": float(c[1]), "projects": n}
                else:
                    cur["seats"][civ] = {"arch": arch, "status": "no_key5", "detail": detail.strip(), "projects": n}
    return matches


def quantile(xs: list[float], q: float) -> float:
    xs = sorted(xs)
    if not xs:
        return float("nan")
    pos = (len(xs) - 1) * q
    lo, hi = int(pos), min(int(pos) + 1, len(xs) - 1)
    return xs[lo] + (xs[hi] - xs[lo]) * (pos - lo)


def cmd_ft1(a: argparse.Namespace) -> int:
    d = Path(a.dir)
    meta = json.loads((d / "metadata.json").read_text())
    matches = parse_matches(sorted(d.glob("shard-*.txt")))
    want = meta["seeds"]
    missing = [s for s in want if s not in matches]
    seats = [(s, c, seat) for s in want if s in matches for c, seat in sorted(matches[s]["seats"].items())]
    total = len(want) * 4
    done = [x[2]["interval"] for x in seats if x[2]["status"] == "complete"]
    cens = [x for x in seats if x[2]["status"] == "censored"]
    nok5 = [x for x in seats if x[2]["status"] == "no_key5"]
    lines = [f"# F-T1 cohort `{meta['name']}`", "",
             f"Status: {meta['status']}; seeds {len(want)}; seat denominator {total} (every missing/failed seat counts as incomplete).", "",
             f"- seeds with no parsed match output: {len(missing)} {missing}",
             f"- complete: {len(done)}/{total} ({100*len(done)/total:.1f}%)",
             f"- right-censored after KEY-5: {len(cens)}",
             f"- KEY-5 not reached/unparsed: {len(nok5)} + {4*len(missing)} seats from missing matches"]
    if done:
        lines += [f"- completed-seat median {statistics.median(done):.2f} min; p10/p25/p75/p90 "
                  f"{quantile(done,.1):.2f}/{quantile(done,.25):.2f}/{quantile(done,.75):.2f}/{quantile(done,.9):.2f}; "
                  f"min/max {min(done):.2f}/{max(done):.2f}"]
        lines.append("")
        lines.append("| archetype | complete | censored | no KEY-5 | median (completed) |")
        lines.append("|---|---:|---:|---:|---:|")
        for arch in ("Builder", "Grower", "Warlord", "Sage"):
            s = [x for x in seats if x[2]["arch"] == arch]
            dd = [x[2]["interval"] for x in s if x[2]["status"] == "complete"]
            lines.append(f"| {arch} | {len(dd)} | {sum(1 for x in s if x[2]['status']=='censored')} | {sum(1 for x in s if x[2]['status']=='no_key5')} | "
                         f"{statistics.median(dd):.2f} |" if dd else f"| {arch} | 0 | - | - | - |")
    med = statistics.median(done) if done else float("nan")
    lines += ["", f"D21 gate: completion floor 256/320 -> {'PASS' if len(done) >= 0.8*total else 'FAIL'} ({len(done)}/{total}, floor {0.8*total:.0f}); "
              f"median 22.5-27.5 -> {'PASS' if 22.5 <= med <= 27.5 else 'FAIL'} ({med:.2f}). (Applies as an acceptance gate only to the 80-world holdout.)", "",
              "| seed | match min | seat | arch | status | KEY-5→KEY-6 / observed |", "|---:|---:|---:|---|---|---:|"]
    for s, c, seat in seats:
        v = seat.get("interval", seat.get("observed", ""))
        lines.append(f"| {s} | {matches[s]['minutes']:.1f} | c{c} | {seat['arch']} | {seat['status']} | {v} |")
    out = "\n".join(lines) + "\n"
    if a.output:
        Path(a.output).write_text(out)
    print("\n".join(lines[:16]))
    return 0



LEDGER_HEAD = re.compile(r"completed town windows=(\d+); censored=(\d+); weighted grain coverage=([\d.]+|n/a)%?.*?town windows meeting 60%=(\d+)/(\d+)")
LEDGER_POW = re.compile(r"power-hours offered=([\d.]+); allocated grind/saw/craft=([\d.]+)/([\d.]+)/([\d.]+); unused=([\d.]+); residual=([\d.\-]+)")
LEDGER_WIN = re.compile(r"^\s+s(\d+) c(\d) seasons (\d+)[–-](\d+): ([\d.]+)%, sources water/wind (\d+)/(\d+)")


def cmd_ft2(a: argparse.Namespace) -> int:
    d = Path(a.dir)
    meta = json.loads((d / "metadata.json").read_text())
    out = [f"# F-T2 ledger cohort `{meta['name']}`", "", f"Status: {meta['status']}; seeds {len(meta['seeds'])}; extra args {meta['extraArgs']}", "",
           "| seed | complete | censored | windows >=60% | weighted coverage | offered | grind/saw/craft | unused | residual | 2+ same-type windows |", "|---:|---:|---:|---:|---:|---:|---|---:|---:|---:|"]
    tot = dict(c=0, cen=0, ok=0, two=0, win=0)
    fails, twos, missing = [], [], []
    cur_seed = None
    per = {}
    for rf in sorted(d.glob("shard-*.txt")):
        for line in rf.read_text().splitlines():
            m = MATCH_RE.match(line)
            if m:
                cur_seed = int(m[1]); per[cur_seed] = dict(head=None, pow=None, wins=[])
                continue
            if cur_seed is None:
                continue
            h = LEDGER_HEAD.search(line)
            if h:
                per[cur_seed]["head"] = h.groups(); continue
            pw = LEDGER_POW.search(line)
            if pw:
                per[cur_seed]["pow"] = pw.groups(); continue
            w = LEDGER_WIN.match(line)
            if w:
                per[cur_seed]["wins"].append(w.groups())
    for s in meta["seeds"]:
        r = per.get(s)
        if not r or not r["head"]:
            missing.append(s); out.append(f"| {s} | missing | | | | | | | | |"); continue
        c, cen, cov, ok, n = r["head"]; pw = r["pow"] or ("", "", "", "", "", "")
        two = sum(1 for w in r["wins"] if int(w[5]) >= 2 or int(w[6]) >= 2)
        for w in r["wins"]:
            if float(w[4]) < 60.0: fails.append((s, w))
            if int(w[5]) >= 2 or int(w[6]) >= 2: twos.append((s, w))
        tot["c"] += int(c); tot["cen"] += int(cen); tot["ok"] += int(ok); tot["two"] += two
        out.append(f"| {s} | {c} | {cen} | {ok}/{n} | {cov}% | {pw[0]} | {pw[1]}/{pw[2]}/{pw[3]} | {pw[4]} | {pw[5]} | {two} |")
    out += ["", f"Totals: {tot['c']} complete windows, {tot['cen']} censored (excluded, not imputed), {tot['ok']} meeting 60%; seeds with no ledger: {len(missing)} {missing}.",
            f"Below-60% complete windows: {len(fails)}" + ("".join(f"\n- seed {s} s{w[0]} c{w[1]} seasons {w[2]}-{w[3]}: {w[4]}% sources water/wind {w[5]}/{w[6]}" for s, w in fails)),
            f"Windows with two or more same-type sources (second-source cases): {len(twos)}" + ("".join(f"\n- seed {s} s{w[0]} c{w[1]}: {w[4]}% water/wind {w[5]}/{w[6]}" for s, w in twos))]
    text = "\n".join(out) + "\n"
    if a.output: Path(a.output).write_text(text)
    print("\n".join(out[-6:]))
    return 0


def cmd_hash(a: argparse.Namespace) -> int:
    print(source_aggregate())
    return 0


def main() -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    r = sub.add_parser("run")
    r.add_argument("--name", required=True)
    r.add_argument("--command", default="fief-match")
    r.add_argument("--pack", required=True)
    r.add_argument("--seeds-file", required=True)
    r.add_argument("--minutes", type=int, default=130)
    r.add_argument("--shards", type=int, default=4)
    r.add_argument("--out", required=True)
    r.add_argument("extra", nargs="*")
    r.set_defaults(fn=cmd_run)
    f1 = sub.add_parser("ft1")
    f1.add_argument("--dir", required=True)
    f1.add_argument("--output")
    f1.set_defaults(fn=cmd_ft1)
    f2 = sub.add_parser("ft2")
    f2.add_argument("--dir", required=True)
    f2.add_argument("--output")
    f2.set_defaults(fn=cmd_ft2)
    h = sub.add_parser("hash")
    h.set_defaults(fn=cmd_hash)
    a = ap.parse_args()
    return a.fn(a)


if __name__ == "__main__":
    sys.exit(main())
