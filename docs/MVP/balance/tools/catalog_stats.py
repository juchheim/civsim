"""Parse the research catalog (03a/03b/03c) and compute the per-era numbers the
balance model needs: project counts, type mix, average type multiplier, keystone
closures (cheapest side of each one-of), in-era critical paths and the earlier-era
projects each keystone forces the civilization to backfill.

Design tooling only (feeds docs/MVP/balance/balance-model.xlsx). Usage:
    python catalog_stats.py            # prints a summary
    import catalog_stats; catalog_stats.load()
"""
import json
import re
from pathlib import Path

MVP = Path(__file__).resolve().parents[2]
FILES = ["03a-research-craft.md", "03b-research-kin.md", "03c-research-reach.md"]
TYPE_MULT = {"D": 1.0, "M": 1.0, "S": 1.2, "R": 0.8, "I": 1.5, "W": 2.0, "K": 2.5}
ROW = re.compile(r"^\|\s*((?:[A-Z]{3}|KEY)-\d+)\s*\|")


def era_num(s):
    s = s.strip()
    return 10 if s == "Apex" else int(s[1:])


def parse_requires(text):
    text = re.sub(r"\[[^\]]*\]", "", text)  # drop gates
    all_of, any_of = [], []
    for grp in re.findall(r"\(([^)]*)\)", text):
        any_of.append([x.strip() for x in grp.split("|") if x.strip()])
    text = re.sub(r"\([^)]*\)", "", text)
    for tok in re.split(r"[,\s]+", text):
        if re.fullmatch(r"(?:[A-Z]{3}|KEY)-\d+", tok):
            all_of.append(tok)
    return all_of, any_of


def load():
    projects = {}
    for fn in FILES:
        for line in (MVP / fn).read_text().splitlines():
            if not ROW.match(line):
                continue
            cells = [c.strip() for c in line.strip().strip("|").split("|")]
            pid, name, era, typ, req = cells[0], cells[1], cells[2], cells[3], cells[4]
            all_of, any_of = parse_requires(req)
            projects[pid] = dict(id=pid, name=name, era=era_num(era), type=typ,
                                 all_of=all_of, any_of=any_of, effect=cells[-1])
    # Keystone KEY-n sits in the era it leaves (n-1) for breadth purposes.
    for pid, p in projects.items():
        if pid.startswith("KEY-"):
            p["era_from"] = int(pid.split("-")[1]) - 1
    return projects


def closure(projects, root):
    """All projects that must be completed for root, choosing the any-of option
    with the smallest own closure (ties: by id) -- the validator's rule."""
    memo = {}

    def clo(pid):
        if pid in memo:
            return memo[pid]
        p = projects[pid]
        s = set()
        for r in p["all_of"]:
            s |= {r} | clo(r)
        for grp in p["any_of"]:
            best = min(sorted(grp), key=lambda g: len(clo(g) | {g}))
            s |= {best} | clo(best)
        memo[pid] = s
        return s

    return clo(root)


def era_table(projects):
    rows = []
    done_by = set()  # projects in earlier keystone closures
    for k in range(1, 11):
        key = projects[f"KEY-{k}"]
        e = k - 1
        clo = closure(projects, key["id"])
        clo_proj = {c for c in clo if not c.startswith("KEY-")}
        in_era = sorted(c for c in clo_proj if projects[c]["era"] == e)
        backfill = sorted(c for c in clo_proj if projects[c]["era"] < e and c not in done_by)
        era_ps = [p for p in projects.values() if p["era"] == e and not p["id"].startswith("KEY-")]
        types = {t: sum(1 for p in era_ps if p["type"] == t) for t in "DMSRIW"}
        avg_tm = sum(TYPE_MULT[p["type"]] for p in era_ps) / len(era_ps)
        # cheapest-first free picks: average multiplier of the cheapest in-era
        # projects not on the critical path (the Council divides by cost).
        free = sorted(TYPE_MULT[p["type"]] for p in era_ps if p["id"] not in in_era)
        breadth = int(re.search(r"(\d+)", {1: "13", 2: "14", 3: "20", 4: "22", 5: "21", 6: "16",
                                          7: "14", 8: "14", 9: "18", 10: "17"}[k]).group(1))
        rows.append(dict(
            era=e, key=key["id"], key_name=key["name"], count=len(era_ps), types=types,
            avg_tm=round(avg_tm, 4),
            crit_n=len(in_era), crit_units=round(sum(TYPE_MULT[projects[c]["type"]] for c in in_era), 3),
            free_avg_tm=round(sum(free) / len(free), 4),
            breadth=breadth,
            backfill_n=len(backfill),
            backfill_ids=backfill, crit_ids=in_era, closure_n=len(clo_proj),
        ))
        done_by |= clo_proj
    return rows


if __name__ == "__main__":
    P = load()
    print("projects:", sum(1 for p in P if not p.startswith("KEY-")), "keystones:", sum(1 for p in P if p.startswith("KEY-")))
    for r in era_table(P):
        print({k: v for k, v in r.items() if k not in ("backfill_ids", "crit_ids")})
