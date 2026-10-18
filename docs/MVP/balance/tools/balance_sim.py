"""Season-by-season single-civilization balance model (WS-A).

This is the reference implementation of the formulas written into
balance-model.xlsx by build_xlsx.py. The two must stay in step: build_xlsx.py
--verify evaluates the workbook and compares it with this module.

Usage:
    python balance_sim.py                 # v0.1 vs tuned summary, Standard pace
    python balance_sim.py tuned Quick     # one scenario at one pace
"""
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import catalog_stats  # noqa: E402
import params as P  # noqa: E402

MAX_SEASONS = 2400  # 10 hours at 1×


def catalog():
    projects = catalog_stats.load()
    rows = catalog_stats.era_table(projects)
    out = []
    for r in rows:
        units = [0.0] * 10
        for b in r["backfill_ids"]:
            units[projects[b]["era"]] += catalog_stats.TYPE_MULT[projects[b]["type"]]
        out.append(dict(r, backfill_units=units))
    return out


CAT = catalog()


def learning_mults(era):
    """Cumulative multiplier per kind for era index `era` (half weight in own era)."""
    m = {k: 0.0 for k in P.KINDS}
    for _id, _name, e, kind, pct, prob in P.LEARNING:
        w = 1.0 if e < era else (0.5 if e == era else 0.0)
        m[kind] += w * math.log(1 + pct * prob)
    return {k: math.exp(v) for k, v in m.items()}


CAP_LN = [(e + at, kind, prob * math.log(mult)) for _id, _n, e, at, kind, mult, prob in P.CAPS]


def cap_mults(pos, on):
    """Cap multipliers per kind once research has reached position `pos` (era + share
    of the era's requirement done). `on` = CapsByProject (0 turns every multiplier to 1)."""
    m = {k: 0.0 for k in P.CAP_KINDS}
    for point, kind, ln in CAP_LN:
        if point <= pos:
            m[kind] += ln
    return {k: math.exp(on * v) for k, v in m.items()}


def caps(s, e, pos):
    """Food, lean-season, housing, group and settlement caps for one settlement."""
    E, A_ = s["era"], s["arch"]
    m = cap_mults(pos, s["CapsByProject"])
    food = E["FoodCap"][e] * A_["FoodCap"] * m["Food"]
    lean = food * min(1.0, s["LeanBase"] * m["Lean"])
    housing = E["HousingCap"][e] * m["Housing"]
    group = E["GroupCap"][e] * s["CohesionFactor"] * m["Group"]
    return dict(pos=pos, food=food, lean=lean, housing=housing, group=group, sett=min(food, housing, group),
                material=min(food, housing))


def era_derived(s):
    """Per-era derived values: settlement limits, costs and requirements."""
    E = s["era"]
    out = []
    for e in range(10):
        c = CAT[e]
        r = E["ClaimRadius"][e]
        tiles = 3 * r * (r + 1) + 1
        smax = max(1, math.floor(E["Territory"][e] * s["arch"]["Territory"] / (tiles * s["SettlementPacking"])))
        cap = caps(s, e, e)["sett"]  # settlement cap at the start of the era (display only)
        n = E["BreadthN"][e]
        pick_units = c["crit_units"] + max(0, n - c["crit_n"]) * c["free_avg_tm"]
        unit = s["pace"] * E["CostMod"][e] * s["DiffusionMult"] * s["arch"]["Diffusion"]
        avg_cost = unit * E["EraBase"][e] * pick_units / n
        key_cost = unit * E["EraBase"][e] * s["KeystoneMult"]
        slot_total = s["SlotPrimary"] + s["SlotSecondary"] + s["SlotTertiary"]
        key_spend = key_cost * slot_total / s["SlotPrimary"]
        backfill = (1 - s["BackfillOverlap"]) * unit * sum(
            c["backfill_units"][src] * E["EraBase"][src] * s["EraLag"] ** (e - src) for src in range(e))
        req = unit * E["EraBase"][e] * pick_units + key_spend + backfill
        lm = learning_mults(e)
        out.append(dict(smax=smax, sett_cap=cap, avg_cost=avg_cost, key_cost=key_cost, key_spend=key_spend,
                        backfill=backfill, req=req, lm=lm,
                        boon=(1 + s["EraBoonPct"] * s["EraBoonTake"]) ** e,
                        spill=(key_spend - key_cost) / avg_cost,
                        backfill_n=c["backfill_n"] * (1 - s["BackfillOverlap"])))
    return out


def run(s, max_seasons=MAX_SEASONS):
    D = era_derived(s)
    E = s["era"]
    A_ = s["arch"]
    rows = []
    C, A, El = s["StartChildren"], s["StartAdults"], s["StartElders"]
    S = 1
    era, prog, pool = 0, 0.0, 0.0
    c0 = caps(s, 0, 0.0)
    rows.append(dict(t=0, minute=0.0, era=0, C=C, A=A, El=El, pop=C + A + El, S=S, insight=0.0,
                     kh=0.0, progress=0.0, prog=0.0, req=D[0]["req"], fission=0, pool=0.0,
                     commons=0, roles=0, structs=0, cap=c0, wfis=0))
    for t in range(1, max_seasons + 1):
        prev = rows[-1]
        # era advance (from the previous row's progress)
        adv = 1 if (prev["era"] <= 9 and prev["prog"] >= prev["req"]) else 0
        era = prev["era"] + adv
        if era > 9:  # Apex done: hold state
            rows.append(dict(prev, t=t, minute=t * s["SeasonSeconds"] / 60, era=10, insight=0, kh=0,
                             progress=0, prog=0, req=0, fission=0, wfis=0))
            continue
        e = era
        d = D[e]
        winter = (t % 4 == 0)
        W = E["WinterMult"][e] if winter else 1.0
        pop = prev["pop"]
        # caps from research position at the end of last season (project by project)
        pos = min(10.0, prev["era"] + min(1.0, prev["prog"] / prev["req"]))
        cp = caps(s, e, pos)
        cap = prev["S"] * cp["sett"]
        # births are damped by food and housing; v0.1 also counted the group cap (GroupInCrowding)
        mcap = prev["S"] * (cp["sett"] if s["GroupInCrowding"] else cp["material"])
        crowd = min(1.0, (pop / mcap) ** s["CrowdingExp"])
        births = prev["A"] * s["WomenShare"] * s["BaseFertility"] * E["FertMult"][e] * A_["Fertility"] * (1 - crowd)
        dC = prev["C"] * E["MortChild"][e] / 4 * W
        dA = prev["A"] * E["MortAdult"][e] * A_["AdultMort"] / 4 * W
        dE = prev["El"] * E["MortElder"][e] / 4 * W
        over = max(0.0, pop - cap) * s["OvercapLoss"]
        oshare = over / pop if pop > 0 else 0
        aCA = prev["C"] / (s["ChildYears"] * 4)
        aAE = prev["A"] / (s["AdultYears"] * 4)
        C = prev["C"] + births - dC - aCA - prev["C"] * oshare
        A = prev["A"] + aCA - aAE - dA - prev["A"] * oshare
        El = prev["El"] + aAE - dE - prev["El"] * oshare
        newpop = C + A + El
        # fission (from the previous row's settlement pressure)
        # a settlement splits when it passes 85% of its cap, or when a late-winter
        # check finds it larger than its lean-season food (09 §7.2); daughter must be viable
        per = pop / prev["S"]
        fission = 0
        wfis = 1 if (s["WinterTrigger"] and winter and per >= cp["lean"]) else 0
        if (per >= s["FissionTrigger"] * cp["sett"] or wfis) and per * s["DaughterShare"] >= s["MinDaughter"]:
            fission = max(1, math.floor(prev["S"] * s["FissionRate"] + 0.5))  # Excel ROUND
        Snew = max(1, min(d["smax"], prev["S"] + fission))
        fission = max(0, Snew - prev["S"])
        # Insight (per year, then per season)
        lm = d["lm"]
        ties = 1.0 if e >= 1 else (1 + s["LowTiesShare"] * (Snew - 1)) / Snew
        eff = (A + s["ElderCommons"] * lm["Elder"] * El) * ties
        commons = s["CommonsCoef"] * eff ** s["CommonsExp"] * lm["Commons"]
        rs = s["RoleScale"] * A_["Roles"]
        roles = rs * (Snew * (E["ElderRolePerSett"][e] * s["OutElder"] * lm["Elder"]
                              + E["ShamanPerSett"][e] * s["OutShaman"])
                      + A * (E["PriestShare"][e] * s["OutPriest"]
                             + E["ScribeShare"][e] * s["OutScribe"] * lm["Scribe"]
                             + E["ScholarShare"][e] * s["OutScholar"] * lm["Scholar"]
                             + E["ResearcherShare"][e] * s["OutResearcher"] * lm["Researcher"]))
        structs = rs * Snew * E["MonasteryPerSett"][e] * s["OutMonastery"] * lm["Monastery"]
        insight = (commons + roles + structs) / 4 * lm["All"] * d["boon"] * A_["Insight"]
        kh_gen = (s["KHPerAdult"] * A + s["KHOfCommons"] * commons / 4 * lm["All"]) * s["KHUsable"] * A_["KH"]
        avail = prev["pool"] + kh_gen
        kh = min(avail, insight * s["KHMaxRatio"])
        pool = avail - kh
        progress = insight + kh
        carry = (prev["prog"] - prev["req"]) if adv else prev["prog"]
        prog = carry + progress
        rows.append(dict(t=t, minute=t * s["SeasonSeconds"] / 60, era=e, C=C, A=A, El=El, pop=newpop, S=Snew,
                         insight=insight, kh=kh, progress=progress, prog=prog, req=d["req"], fission=fission,
                         pool=pool, commons=commons, roles=roles, structs=structs, cap=cp,
                         wfis=wfis if fission else 0))
    return rows, D


def summarize(rows, D, s):
    res = []
    for e in range(10):
        seasons = [r for r in rows if r["era"] == e and r["t"] > 0]
        if not seasons:
            res.append(None)
            continue
        start = seasons[0]["t"]
        dur = len(seasons)
        finished = any(r["era"] > e for r in rows)
        end = rows[start + dur - 1]
        ins = sum(r["insight"] for r in seasons)
        kh = sum(r["kh"] for r in seasons)
        mins = dur * s["SeasonSeconds"] / 60
        done = s["era"]["BreadthN"][e] + D[e]["spill"]
        res.append(dict(era=e, start_min=(start - 1) * s["SeasonSeconds"] / 60, minutes=mins, finished=finished,
                        pop_end=end["pop"], S_end=end["S"], ins_per_min=ins / mins, kh_share=kh / max(1e-9, ins + kh),
                        done=done, backfill_n=D[e]["backfill_n"],
                        sec_per_done=mins * 60 / (done + D[e]["backfill_n"]),
                        figures=end["pop"] / s["era"]["RepScale"][e],
                        roles_share=sum(r["roles"] + r["structs"] for r in seasons)
                        / max(1e-9, sum(r["commons"] + r["roles"] + r["structs"] for r in seasons)),
                        primary_min=D[e]["avg_cost"] / (s["SlotPrimary"] / (s["SlotPrimary"] + s["SlotSecondary"] + s["SlotTertiary"]) * (ins + kh) / mins)))
    first_fission = next((r["minute"] for r in rows if r["fission"] > 0), None)
    total = next(((r["t"] - 1) * s["SeasonSeconds"] / 60 for r in rows if r["era"] == 10), None)
    return res, first_fission, total


def report(name="tuned", pace="Standard", arch="Balanced", **kw):
    s = P.scenario(name, pace, arch, kw or None)
    rows, D = run(s)
    res, ff, total = summarize(rows, D, s)
    print(f"== {name} / {pace} / {arch}: total {total} min, first fission at {ff} min")
    print(" era  start   mins  target     done bfill  sec/c  prim  pop_end  sett  figs  Ins/min   KH%  roles%")
    for r in res:
        if r is None:
            continue
        e = r["era"]
        print(f"  E{e} {r['start_min']:6.1f} {r['minutes']:6.1f} {s['era']['TargetMinLo'][e]:3d}-{s['era']['TargetMinHi'][e]:<3d}"
              f" {r['done']:6.1f} {r['backfill_n']:5.1f} {r['sec_per_done']:6.0f} {r['primary_min']:5.2f} {r['pop_end']:8.0f}"
              f" {r['S_end']:5d} {r['figures']:5.0f} {r['ins_per_min']:8.1f} {100*r['kh_share']:5.1f} {100*r['roles_share']:5.1f}"
              + ("" if r["finished"] else "  (unfinished)"))
    return res, ff, total


if __name__ == "__main__":
    args = sys.argv[1:]
    if args:
        report(*args)
    else:
        report("v01")
        report("tuned")
